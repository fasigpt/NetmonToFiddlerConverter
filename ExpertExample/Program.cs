   //-----------------------------------------------------------------------
// <copyright file="Program.cs" company="Microsoft">
//     Copyright (c) 2009 Microsoft. All rights reserved.
// </copyright>
// <author>Michael A. Hawker</author>
//-----------------------------------------------------------------------
using System;
using System.IO;


[assembly: CLSCompliant(false)]
namespace ExpertExample
{
    using System.Collections.Generic;
    using System.Runtime.InteropServices.ComTypes;
    using System.Text;
    using Microsoft.NetworkMonitor;
    using Microsoft.NetworkMonitor.Automation;
    using System.Linq;
    using System.IO.Compression;

    /// <summary>
    /// Provides a quick example of accessing parameters received from the Network Monitor UI
    /// and processing a capture file looking only at the frames selected from the user.
    /// </summary>
    [CLSCompliant(false)]
    public sealed class Program
    {
        /// <summary>
        /// Returned by API functions when they are successful
        /// </summary>
        private const ulong ERROR_SUCCESS = 0;

        /// <summary>
        /// Reference to a file, file path, handle, or data field is incorrect.
        /// </summary>
        private const ulong ERROR_NOT_FOUND = 1168;

        /// <summary>
        /// The field is a container, so the content is not available.
        /// </summary>
        private const ulong ERROR_RESOURCE_NOT_AVAILABLE = 5006;

        /// <summary>
        /// Size of Buffer to use when retrieving field values
        /// </summary>
        private const uint BUFFER_SIZE = 512;

        /// <summary>
        /// Used to signal the API has been loaded
        /// </summary>
        private static bool initialized;

        /// <summary>
        /// Used to hold the Parser Error Callback Function Pointer
        /// </summary>
        private static ParserCallbackDelegate pErrorCallBack = new ParserCallbackDelegate(ParserCallback);

        /// <summary>
        /// Prevents a default instance of the Program class from being created
        /// </summary>
        private Program()
        {
        }

        /// <summary>
        /// Called when the Parser Engine has information or an error message1
        /// </summary>
        /// <param name="pCallerContext">Called Context given to the Parsing Engine</param>
        /// <param name="ulStatusCode">Message Status Code</param>
        /// <param name="lpDescription">Description Text of Error</param>
        /// <param name="ulType">Type of Message</param>
        public static void ParserCallback(IntPtr pCallerContext, uint ulStatusCode, string lpDescription, NmCallbackMsgType ulType)
        {
            if (ulType == NmCallbackMsgType.Error)
            {
                Console.WriteLine("ERROR: " + lpDescription);
            }
            else
            {
                Console.WriteLine(lpDescription);
            }
        }




        /// <summary>
        /// Simple Expert Example to just spit out command line arguments.
        /// </summary>
        /// <param name="args">arguments from Netmon</param>
        [STAThread]
        public static void Main(string[] args)
        {
            DateTime ProgramStartTime = DateTime.Now;
            Dictionary<uint, HTTPRequestFrame> Requests = new Dictionary<uint, HTTPRequestFrame>();
            Dictionary<uint, HTTPResponseFrame> Responses= new Dictionary<uint, HTTPResponseFrame>();
            Dictionary<uint, HTTPPayloadFrame> Payloads= new Dictionary<uint, HTTPPayloadFrame>();

            //Creating Directory to keep the raw stream pulled from each raw frame
            SetUpDirectory();

            #region Try Load API
            try
            {
                initialized = Program.InitializeNMAPI();
            }
            catch (BadImageFormatException)
            {
                Console.WriteLine("There was an error loading the NMAPI.\n\nPlease ensure you have the correct version installed for your platform.");
            }
            catch (DllNotFoundException)
            {
                Console.WriteLine("There was an error loading the NMAPI DLL.\n\nPlease ensure you have Network Monitor 3.4 installed or try rebooting.");
            }
            #endregion

            CommandLineArguments commandReader = new CommandLineArguments();
            if (commandReader.ParseCommandLineArguments(args))
            {
                if (commandReader.IsNoArguments)
                {
                    Console.WriteLine(CommandLineArguments.GetUsage("ExpertExample"));
                }
                else if (commandReader.IsRequestingHelp)
                {
                    Console.WriteLine(CommandLineArguments.GetUsage("ExpertExample"));
                }
                else if (initialized)
                {
                    Console.WriteLine("Running Test Application with Arguments:");
                    Console.WriteLine("\tCapture File: " + commandReader.CaptureFileName);
                    Console.WriteLine("\tDisplay Filter: " + commandReader.DisplayFilter);
                    Console.WriteLine("\tConversation Filter: " + commandReader.ConversationFilter);
                    Console.WriteLine("\tSelected Frames: " + commandReader.SelectedFramesString);

                    Console.WriteLine();

                    bool loadedparserengine = false;

                    // Configure Parser Engine
                    uint errno;                    
                    IntPtr hNplParser = IntPtr.Zero;
                    IntPtr hFrameParserConfig = IntPtr.Zero;
                    uint conversationFilterId = 0;
                    uint displayFilterId = 0;
                    IntPtr hFrameParser = IntPtr.Zero;
                    uint TCPPayloadLengthID=0;
                    //uint TCPSrcPortID = 0;
                    //uint TCPDataOffsetID = 0;
                    #region Only load the parsing engine if we have to
                    if (!string.IsNullOrEmpty(commandReader.ConversationFilter) || !string.IsNullOrEmpty(commandReader.DisplayFilter))
                    {
                        Console.WriteLine("Loading Parser Engine...");

                        // Passing in null for the path will use the default configuration as specified in the Netmon UI
                        errno = NetmonAPI.NmLoadNplParser(null, NmNplParserLoadingOption.NmAppendRegisteredNplSets, pErrorCallBack, IntPtr.Zero, out hNplParser);
                        if (errno == ERROR_SUCCESS)
                        {
                            // Configure Frame Parser
                            errno = NetmonAPI.NmCreateFrameParserConfiguration(hNplParser, pErrorCallBack, IntPtr.Zero, out hFrameParserConfig);

                            if (errno == ERROR_SUCCESS)
                            {
                                // Enable Conversations
                                errno = NetmonAPI.NmConfigConversation(hFrameParserConfig, NmConversationConfigOption.None, true);
                                if (errno == ERROR_SUCCESS)
                                {
                                    // Add Filters
                                    if (!string.IsNullOrEmpty(commandReader.ConversationFilter))
                                    {
                                        Console.WriteLine("Adding Conversation Filter...");
                                        errno = NetmonAPI.NmAddFilter(hFrameParserConfig, commandReader.ConversationFilter, out conversationFilterId);
                                    }

                                    if (errno == ERROR_SUCCESS)
                                    {
                                        if (!string.IsNullOrEmpty(commandReader.DisplayFilter))
                                        {
                                            Console.WriteLine("Adding Display Filter...");
                                            errno = NetmonAPI.NmAddFilter(hFrameParserConfig, commandReader.DisplayFilter, out displayFilterId);
                                            //to obtain raw data
                                            //errno = NetmonAPI.NmAddField(hFrameParserConfig, "ethernet.ipv4.tcp.tcppayload", out tcpPayloadID);
                                            errno = NetmonAPI.NmAddProperty(hFrameParserConfig, "Property.TCPPayloadLength", out TCPPayloadLengthID);
                                            //errno = NetmonAPI.NmAddField(hFrameParserConfig, "TCP.SrcPort", out TCPSrcPortID);
                                            //errno = NetmonAPI.NmAddField(hFrameParserConfig, "TCP.DataOffset.DataOffset", out TCPDataOffsetID);


                                        }

                                        if (errno == ERROR_SUCCESS)
                                        {
                                            errno = NetmonAPI.NmCreateFrameParser(hFrameParserConfig, out hFrameParser, NmFrameParserOptimizeOption.ParserOptimizeNone);
                                            if (errno == ERROR_SUCCESS)
                                            {
                                                Console.WriteLine("Parser Engine Loaded Successfully!");
                                                Console.WriteLine();

                                                loadedparserengine = true;
                                            }
                                            else
                                            {
                                                Console.WriteLine("Parser Creation Error Number = " + errno);
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Display Filter Creation Error Number = " + errno);
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Conversation Filter Creation Error Number = " + errno);
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("Conversation Error Number = " + errno);
                                }

                                if (!loadedparserengine)
                                {
                                    NetmonAPI.NmCloseHandle(hFrameParserConfig);
                                }
                            }
                            else
                            {
                                Console.WriteLine("Parser Configuration Error Number = " + errno);
                            }

                            if (!loadedparserengine)
                            {
                                NetmonAPI.NmCloseHandle(hNplParser);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Error Loading NMAPI Parsing Engine Error Number = " + errno);
                        }
                    }
                    #endregion
                    // Wait for confirmation
                    //Console.WriteLine("Press any key to continue");
                    //Console.ReadKey(true);

                    // Let's open the capture file
                    // Open Capture File
                    IntPtr captureFile = IntPtr.Zero;
                    errno = NetmonAPI.NmOpenCaptureFile(commandReader.CaptureFileName, out captureFile);
                    if (errno == ERROR_SUCCESS)
                    {
                        // Retrieve the number of frames in this capture file
                        uint frameCount;
                        errno = NetmonAPI.NmGetFrameCount(captureFile, out frameCount);
                        if (errno == ERROR_SUCCESS)
                        {
                            // Loop through capture file
                            for (uint ulFrameNumber = 0; ulFrameNumber < frameCount; ulFrameNumber++)
                            {
                                // Get the Raw Frame data
                                IntPtr hRawFrame = IntPtr.Zero;
                                errno = NetmonAPI.NmGetFrame(captureFile, ulFrameNumber, out hRawFrame);
                                if (errno != ERROR_SUCCESS)
                                {
                                    Console.WriteLine("Error Retrieving Frame #" + (ulFrameNumber + 1) + " from file");
                                    continue;
                                }

                                // Need to parse once to get similar results to the UI
                                if (loadedparserengine)
                                {
                                    // Parse Frame
                                    IntPtr phParsedFrame;
                                    IntPtr phInsertedRawFrame;
                                    errno = NetmonAPI.NmParseFrame(hFrameParser, hRawFrame, ulFrameNumber, NmFrameParsingOption.FieldDisplayStringRequired | NmFrameParsingOption.FieldFullNameRequired | NmFrameParsingOption.DataTypeNameRequired, out phParsedFrame, out phInsertedRawFrame);
                                    if (errno == ERROR_SUCCESS)
                                    {
                                        // Check against Filters
                                        if (!string.IsNullOrEmpty(commandReader.ConversationFilter))
                                        {
                                            bool passed;
                                            errno = NetmonAPI.NmEvaluateFilter(phParsedFrame, conversationFilterId, out passed);
                                            if (errno == ERROR_SUCCESS)
                                            {
                                                if (passed)
                                                {
                                                    if (!string.IsNullOrEmpty(commandReader.DisplayFilter))
                                                    {
                                                        bool passed2;
                                                        errno = NetmonAPI.NmEvaluateFilter(phParsedFrame, displayFilterId, out passed2);
                                                        if (errno == ERROR_SUCCESS)
                                                        {
                                                           
                                                            if (passed2)
                                                            #region My_Logic_In_Case_Of_Conversation_and_Display_Filter
                                                            {
                                                                /*This is where my custom logic goes in case conversation filter is passed along !!!*/
                                                                /*I dont know how to apply a conversation filter here, so I was not able to test if this will work or not*/
                                                                /*In all my test sample's, this code path was never hit*/
                                                                /*This will execute if both the CONVERSATION and DISPLAY are filter applied*/
                                                                #region My_Logic_In_Case_Of_Conversation_and_Display_Filter
                                                                {
                                                                    try
                                                                    {
                                                                        MyHttpParser parser = new MyHttpParser(phParsedFrame,hFrameParser,hRawFrame,TCPPayloadLengthID);
                                                                        HTTPFrame currFrame = parser.GetFrame(phParsedFrame, ulFrameNumber);
                                                                        switch (parser.FrameType.ToString())
                                                                        {
                                                                            case "Request":
                                                                                Requests.Add(ulFrameNumber + 1, currFrame as HTTPRequestFrame);
                                                                                break;
                                                                            case "Response":
                                                                                Responses.Add(ulFrameNumber + 1, currFrame as HTTPResponseFrame);
                                                                                break;
                                                                            case "Payload":
                                                                                HTTPPayloadFrame tempPayload = currFrame as HTTPPayloadFrame;

                                                                                #region Check if this Payload Packet is a Request Payload and match it to its request
                                                                                //Along with classifying the Payloads as Request / Response Payloads we also populate the 
                                                                                //URL field of the Payload to match the URL of their corresponding Request and link them, together
                                                                                //for easy identification / calculations later. The URL that Netmon identifies for them does not 
                                                                                //match the URL we have for the request exactly so we do it here.
                                                                                var key = (from KeyValuePair<uint, HTTPRequestFrame> request in Requests
                                                                                           where (
                                                                                           (request.Value.SourceIP == tempPayload.SourceIP) &&
                                                                                           (request.Value.SourcePort == tempPayload.SourcePort)
                                                                                           )
                                                                                           select
                                                                                            new { PayloadType = HTTPFrameType.RequestPayload, request }
                                                                                          ).LastOrDefault();
                                                                                if (key != null)
                                                                                {
                                                                                    tempPayload.Type = HTTPFrameType.RequestPayload;
                                                                                    tempPayload.URL = key.request.Value.URL;
                                                                                }
                                                                                #endregion

                                                                                if (tempPayload.Type != HTTPFrameType.RequestPayload)
                                                                                {
                                                                                    #region Check if this Payload Packet is a Response Payload and match it to its request
                                                                                    //Dont do anything if we already know this packet to be a Request Payload. 
                                                                                    //However, even though we know this is not a Request Payload here, we still need to check if this 
                                                                                    //packet gets classified as a response payload or not. 
                                                                                    //This packet might turn out to be just some payload packet which got captured for whom 
                                                                                    //neither the corresponding request nor the response packet were captured.
                                                                                    //This will typically happen for payload packets that appear at the start of the trace.

                                                                                    var key1 = (from KeyValuePair<uint, HTTPResponseFrame> response in Responses
                                                                                                where (
                                                                                                (response.Value.SourceIP == tempPayload.SourceIP) &&
                                                                                                (response.Value.SourcePort == tempPayload.SourcePort)
                                                                                                )
                                                                                                select
                                                                                                new { PayloadType = HTTPFrameType.ResponsePayload }
                                                                                              ).LastOrDefault();
                                                                                    if (key1 != null)
                                                                                    {
                                                                                        tempPayload.Type = HTTPFrameType.ResponsePayload;

                                                                                        var key2 = (from KeyValuePair<uint, HTTPRequestFrame> request1 in Requests
                                                                                                    where
                                                                                                        (
                                                                                                            (request1.Value.SourceIP == tempPayload.DestinationIP) &&
                                                                                                            (request1.Value.SourcePort == tempPayload.DestinationPort)
                                                                                                        )
                                                                                                    select new { PayloadType = HTTPFrameType.RequestPayload, request1 }
                                                                                                ).LastOrDefault();

                                                                                        if (key2 != null)
                                                                                        {
                                                                                            tempPayload.URL = key2.request1.Value.URL;
                                                                                        }
                                                                                    }
                                                                                    #endregion
                                                                                }

                                                                                Payloads.Add(ulFrameNumber + 1, tempPayload);
                                                                                break;
                                                                            case "Undeterministic":
                                                                                throw new InvalidOperationException("Undeterministic type of frame encountered at Frame #" + (ulFrameNumber + 1).ToString());
                                                                            default:
                                                                                break;
                                                                        }  
                                                                    }
                                                                    catch (Exception ex)
                                                                    {
                                                                        Console.WriteLine(ex.Message);
                                                                    }
                                                                }
                                                                #endregion
                                                                //PrintParsedFrameInformation(phParsedFrame, ulFrameNumber, commandReader);

                                                            }
                                                            #endregion
                                                            else
                                                            #region My_Logic_In_Case_Of_Conversation_Filter
                                                            {
                                                                /*This is where my custom logic goes in case conversation filter is passed along !!!*/
                                                                /*I dont know how to apply a conversation filter here, so I was not able to test if this will work or not*/
                                                                /*In all my test sample's, this code path was never hit*/
                                                                /*This will execute if only the CONVERSATION filter is applied*/
                                                                #region My_Logic_In_Case_Of_Conversation_Filter
                                                                {
                                                                    try
                                                                    {
                                                                        MyHttpParser parser = new MyHttpParser(phParsedFrame, hFrameParser, hRawFrame, TCPPayloadLengthID);
                                                                        HTTPFrame currFrame = parser.GetFrame(phParsedFrame, ulFrameNumber);
                                                                        switch (parser.FrameType.ToString())
                                                                        {
                                                                            case "Request":
                                                                                Requests.Add(ulFrameNumber + 1, currFrame as HTTPRequestFrame);
                                                                                break;
                                                                            case "Response":
                                                                                Responses.Add(ulFrameNumber + 1, currFrame as HTTPResponseFrame);
                                                                                break;
                                                                            case "Payload":
                                                                                HTTPPayloadFrame tempPayload = currFrame as HTTPPayloadFrame;

                                                                                #region Check if this Payload Packet is a Request Payload and match it to its request
                                                                                //Along with classifying the Payloads as Request / Response Payloads we also populate the 
                                                                                //URL field of the Payload to match the URL of their corresponding Request and link them, together
                                                                                //for easy identification / calculations later. The URL that Netmon identifies for them does not 
                                                                                //match the URL we have for the request exactly so we do it here.
                                                                                var key = (from KeyValuePair<uint, HTTPRequestFrame> request in Requests
                                                                                           where (
                                                                                           (request.Value.SourceIP == tempPayload.SourceIP) &&
                                                                                           (request.Value.SourcePort == tempPayload.SourcePort)
                                                                                           )
                                                                                           select
                                                                                            new { PayloadType = HTTPFrameType.RequestPayload, request }
                                                                                          ).LastOrDefault();
                                                                                if (key != null)
                                                                                {
                                                                                    tempPayload.Type = HTTPFrameType.RequestPayload;
                                                                                    tempPayload.URL = key.request.Value.URL;
                                                                                }
                                                                                #endregion

                                                                                if (tempPayload.Type != HTTPFrameType.RequestPayload)
                                                                                {
                                                                                    #region Check if this Payload Packet is a Response Payload and match it to its request
                                                                                    //Dont do anything if we already know this packet to be a Request Payload. 
                                                                                    //However, even though we know this is not a Request Payload here, we still need to check if this 
                                                                                    //packet gets classified as a response payload or not. 
                                                                                    //This packet might turn out to be just some payload packet which got captured for whom 
                                                                                    //neither the corresponding request nor the response packet were captured.
                                                                                    //This will typically happen for payload packets that appear at the start of the trace.

                                                                                    var key1 = (from KeyValuePair<uint, HTTPResponseFrame> response in Responses
                                                                                                where (
                                                                                                (response.Value.SourceIP == tempPayload.SourceIP) &&
                                                                                                (response.Value.SourcePort == tempPayload.SourcePort)
                                                                                                )
                                                                                                select
                                                                                                new { PayloadType = HTTPFrameType.ResponsePayload }
                                                                                              ).LastOrDefault();
                                                                                    if (key1 != null)
                                                                                    {
                                                                                        tempPayload.Type = HTTPFrameType.ResponsePayload;

                                                                                        var key2 = (from KeyValuePair<uint, HTTPRequestFrame> request1 in Requests
                                                                                                    where
                                                                                                        (
                                                                                                            (request1.Value.SourceIP == tempPayload.DestinationIP) &&
                                                                                                            (request1.Value.SourcePort == tempPayload.DestinationPort)
                                                                                                        )
                                                                                                    select new { PayloadType = HTTPFrameType.RequestPayload, request1 }
                                                                                                ).LastOrDefault();

                                                                                        if (key2 != null)
                                                                                        {
                                                                                            tempPayload.URL = key2.request1.Value.URL;
                                                                                        }
                                                                                    }
                                                                                    #endregion
                                                                                }

                                                                                Payloads.Add(ulFrameNumber + 1, tempPayload);
                                                                                break;
                                                                            case "Undeterministic":
                                                                                throw new InvalidOperationException("Undeterministic type of frame encountered at Frame #" + (ulFrameNumber + 1).ToString());
                                                                            default:
                                                                                break;
                                                                        }  
                                                                    }
                                                                    catch (Exception ex)
                                                                    {
                                                                        Console.WriteLine(ex.Message);
                                                                    }
                                                                }
                                                                #endregion

                                                                //No need to look at the frames that did not pass the initial filter that was applied on them
                                                                //Console.WriteLine("This is me !!!");
                                                                //PrintParsedFrameInformation(phParsedFrame, ulFrameNumber, commandReader);
                                                            }
                                                            #endregion
                                                        }
                                                    }
                                                    else
                                                    {
                                                        PrintParsedFrameInformation(phParsedFrame, ulFrameNumber, commandReader,2,hFrameParser);
                                                    }
                                                }
                                            }
                                        }
                                        else if (!string.IsNullOrEmpty(commandReader.DisplayFilter))
                                        {
                                            bool passed;
                                            errno = NetmonAPI.NmEvaluateFilter(phParsedFrame, displayFilterId, out passed);
                                            if (errno == ERROR_SUCCESS)
                                            {
                                                if (passed)
                                                {
                                                    /*This is where my custom logic goes in case a display filter was applied */
                                                    #region My_Logic_In_Case_Of_Display_Filter
                                                    {
                                                        try
                                                        {
                                                            //hit logic
                                                            MyHttpParser parser = new MyHttpParser(phParsedFrame, hFrameParser, hRawFrame, TCPPayloadLengthID);
                                                            HTTPFrame currFrame = parser.GetFrame(phParsedFrame, ulFrameNumber);
                                                            switch (parser.FrameType.ToString())
                                                            { 
                                                                case "Request":
                                                                    Requests.Add(ulFrameNumber + 1, currFrame as HTTPRequestFrame);
                                                                    GetFramePayload(phParsedFrame, hFrameParser, hRawFrame, TCPPayloadLengthID, parser.dataOffsetID,parser.srcPortFrameID,ulFrameNumber+1,"Request");
                                                                    break;
                                                                case "Response":
                                                                    Responses.Add(ulFrameNumber + 1, currFrame as HTTPResponseFrame);
                                                                    GetFramePayload(phParsedFrame, hFrameParser, hRawFrame, TCPPayloadLengthID, parser.dataOffsetID, parser.srcPortFrameID, ulFrameNumber + 1, "Response");
                                                                    break;
                                                                case "Payload":
                                                                    HTTPPayloadFrame tempPayload = currFrame as HTTPPayloadFrame;

                                                                     #region Check if this Payload Packet is a Request Payload and match it to its request 
                                                                    //Along with classifying the Payloads as Request / Response Payloads we also populate the 
                                                                    //URL field of the Payload to match the URL of their corresponding Request and link them, together
                                                                    //for easy identification / calculations later. The URL that Netmon identifies for them does not 
                                                                    //match the URL we have for the request exactly so we do it here.
                                                                        var key = (from KeyValuePair<uint, HTTPRequestFrame> request in Requests
                                                                                   where (
                                                                                   (request.Value.SourceIP == tempPayload.SourceIP) &&
                                                                                   (request.Value.SourcePort == tempPayload.SourcePort)
                                                                                   )
                                                                                   select
                                                                                    new { PayloadType = HTTPFrameType.RequestPayload, request }
                                                                                  ).LastOrDefault();
                                                                        if (key != null)
                                                                        {
                                                                            tempPayload.Type = HTTPFrameType.RequestPayload;
                                                                            tempPayload.URL = key.request.Value.URL;
                                                                        }
                                                                        #endregion

                                                                        if (tempPayload.Type != HTTPFrameType.RequestPayload)
                                                                        {
                                                                            #region Check if this Payload Packet is a Response Payload and match it to its request
                                                                            //Dont do anything if we already know this packet to be a Request Payload. 
                                                                            //However, even though we know this is not a Request Payload here, we still need to check if this 
                                                                            //packet gets classified as a response payload or not. 
                                                                            //This packet might turn out to be just some payload packet which got captured for whom 
                                                                            //neither the corresponding request nor the response packet were captured.
                                                                            //This will typically happen for payload packets that appear at the start of the trace.
                                                                            
                                                                            var key1 = (from KeyValuePair<uint, HTTPResponseFrame> response in Responses
                                                                                        where (
                                                                                        (response.Value.SourceIP == tempPayload.SourceIP) &&
                                                                                        (response.Value.SourcePort == tempPayload.SourcePort)
                                                                                        )
                                                                                        select
                                                                                        new { PayloadType = HTTPFrameType.ResponsePayload }
                                                                                      ).LastOrDefault();
                                                                            if (key1 != null)
                                                                            {
                                                                                tempPayload.Type = HTTPFrameType.ResponsePayload;

                                                                                var key2 = (from KeyValuePair<uint, HTTPRequestFrame> request1 in Requests
                                                                                            where
                                                                                                (
                                                                                                    (request1.Value.SourceIP == tempPayload.DestinationIP) &&
                                                                                                    (request1.Value.SourcePort == tempPayload.DestinationPort)
                                                                                                )
                                                                                            select new { PayloadType = HTTPFrameType.RequestPayload, request1 }
                                                                                        ).LastOrDefault();

                                                                                if (key2 != null)
                                                                                {
                                                                                    tempPayload.URL = key2.request1.Value.URL;
                                                                                } 
                                                                            }
                                                                            #endregion
                                                                        }

                                                                    Payloads.Add(ulFrameNumber + 1, tempPayload);
                                                                    GetFramePayload(phParsedFrame, hFrameParser, hRawFrame, TCPPayloadLengthID, parser.dataOffsetID, parser.srcPortFrameID, ulFrameNumber + 1, "Payload");
                                                                    break;
                                                                case "Undeterministic":
                                                                    throw new InvalidOperationException("Undeterministic type of frame encountered at Frame #" + (ulFrameNumber+1).ToString());
                                                                default:
                                                                    break;
                                                            }                                                           
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            Console.WriteLine(ex.Message);
                                                        }
                                                        Console.Clear();
                                                        Console.BackgroundColor = ConsoleColor.DarkBlue;                                                        
                                                        Console.WriteLine("Finished Processing Frame {0}", ulFrameNumber + 1);
                                                        
                                                        
                                                    }
                                                    #endregion
                                                    
                                                }
                                            }
                                        }
                                        else
                                        {
                                            //This will execute if some parser is applied, however no display filter has been applied on the trace
                                            //Again, I was not able to test this scenario
                                            #region My_Logic_In_Case_Of_NO_Display_Filter
                                            {
                                                try
                                                {
                                                    MyHttpParser parser = new MyHttpParser(phParsedFrame, hFrameParser, hRawFrame, TCPPayloadLengthID);
                                                    HTTPFrame currFrame = parser.GetFrame(phParsedFrame, ulFrameNumber);
                                                    switch (parser.FrameType.ToString())
                                                    {
                                                        case "Request":
                                                            Requests.Add(ulFrameNumber + 1, currFrame as HTTPRequestFrame);
                                                            break;
                                                        case "Response":
                                                            Responses.Add(ulFrameNumber + 1, currFrame as HTTPResponseFrame);
                                                            break;
                                                        case "Payload":
                                                            HTTPPayloadFrame tempPayload = currFrame as HTTPPayloadFrame;

                                                            #region Check if this Payload Packet is a Request Payload and match it to its request
                                                            //Along with classifying the Payloads as Request / Response Payloads we also populate the 
                                                            //URL field of the Payload to match the URL of their corresponding Request and link them, together
                                                            //for easy identification / calculations later. The URL that Netmon identifies for them does not 
                                                            //match the URL we have for the request exactly so we do it here.
                                                            var key = (from KeyValuePair<uint, HTTPRequestFrame> request in Requests
                                                                       where (
                                                                       (request.Value.SourceIP == tempPayload.SourceIP) &&
                                                                       (request.Value.SourcePort == tempPayload.SourcePort)
                                                                       )
                                                                       select
                                                                        new { PayloadType = HTTPFrameType.RequestPayload, request }
                                                                      ).LastOrDefault();
                                                            if (key != null)
                                                            {
                                                                tempPayload.Type = HTTPFrameType.RequestPayload;
                                                                tempPayload.URL = key.request.Value.URL;
                                                            }
                                                            #endregion

                                                            if (tempPayload.Type != HTTPFrameType.RequestPayload)
                                                            {
                                                                #region Check if this Payload Packet is a Response Payload and match it to its request
                                                                //Dont do anything if we already know this packet to be a Request Payload. 
                                                                //However, even though we know this is not a Request Payload here, we still need to check if this 
                                                                //packet gets classified as a response payload or not. 
                                                                //This packet might turn out to be just some payload packet which got captured for whom 
                                                                //neither the corresponding request nor the response packet were captured.
                                                                //This will typically happen for payload packets that appear at the start of the trace.

                                                                var key1 = (from KeyValuePair<uint, HTTPResponseFrame> response in Responses
                                                                            where (
                                                                            (response.Value.SourceIP == tempPayload.SourceIP) &&
                                                                            (response.Value.SourcePort == tempPayload.SourcePort)
                                                                            )
                                                                            select
                                                                            new { PayloadType = HTTPFrameType.ResponsePayload }
                                                                          ).LastOrDefault();
                                                                if (key1 != null)
                                                                {
                                                                    tempPayload.Type = HTTPFrameType.ResponsePayload;

                                                                    var key2 = (from KeyValuePair<uint, HTTPRequestFrame> request1 in Requests
                                                                                where
                                                                                    (
                                                                                        (request1.Value.SourceIP == tempPayload.DestinationIP) &&
                                                                                        (request1.Value.SourcePort == tempPayload.DestinationPort)
                                                                                    )
                                                                                select new { PayloadType = HTTPFrameType.RequestPayload, request1 }
                                                                            ).LastOrDefault();

                                                                    if (key2 != null)
                                                                    {
                                                                        tempPayload.URL = key2.request1.Value.URL;
                                                                    }
                                                                }
                                                                #endregion
                                                            }

                                                            Payloads.Add(ulFrameNumber + 1, tempPayload);
                                                            break;
                                                        case "Undeterministic":
                                                            throw new InvalidOperationException("Undeterministic type of frame encountered at Frame #" + (ulFrameNumber + 1).ToString());
                                                        default:
                                                            break;
                                                    }  
                                                }
                                                catch (Exception ex)
                                                {
                                                    Console.WriteLine(ex.Message);
                                                }
                                            }
                                            #endregion
                                            //PrintParsedFrameInformation(phParsedFrame, ulFrameNumber, commandReader);
                                        }

                                        NetmonAPI.NmCloseHandle(phInsertedRawFrame);
                                        NetmonAPI.NmCloseHandle(phParsedFrame);
                                    }
                                    else
                                    {
                                        Console.WriteLine("Error Parsing Frame #" + (ulFrameNumber + 1) + " from file");
                                    }
                                }
                                else
                                {
                                    #region If Parser engine did not load
                                    // Just print what I just deleted...
                                    uint pulLength;
                                    errno = NetmonAPI.NmGetRawFrameLength(hRawFrame, out pulLength);
                                    if (errno == ERROR_SUCCESS)
                                    {
                                        if (commandReader.IsSelected(ulFrameNumber))
                                        {
                                            Console.WriteLine("Frame #" + (ulFrameNumber + 1) + " (Selected) Frame Length(bytes): " + pulLength);
                                        }
                                        else
                                        {
                                            Console.WriteLine("Frame #" + (ulFrameNumber + 1) + " Frame Length(bytes): " + pulLength);
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Error Getting Frame Length for Frame #" + (ulFrameNumber + 1));
                                    }
                                    #endregion
                                }

                                NetmonAPI.NmCloseHandle(hRawFrame);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Error Retrieving Capture File Length");
                        }

                        // Close Capture File to Cleanup
                        NetmonAPI.NmCloseHandle(captureFile);
                    }
                    else
                    {
                        Console.WriteLine("Could not open capture file: " + commandReader.CaptureFileName);
                        Console.WriteLine(CommandLineArguments.GetUsage("ExpertExample"));
                    }

                    if (loadedparserengine)
                    {
                        NetmonAPI.NmCloseHandle(hFrameParser);
                        NetmonAPI.NmCloseHandle(hFrameParserConfig);
                        NetmonAPI.NmCloseHandle(hNplParser);
                    }
                }
            }
            else
            {
                Console.WriteLine(commandReader.LastErrorMessage);
                Console.WriteLine(CommandLineArguments.GetUsage("ExpertExample"));
            }

            
            Console.WriteLine("Writing to output file");
            DateTime OutputGenerationTime = DateTime.Now;
            //Output.GenerateOutput(Requests, Responses, Payloads, @"D:\MyData\Projects\NetmonExpertSDK\ExpertExample\out.htm", commandReader.CaptureFileName);

            //Output.GenerateOutput(Requests, Responses, Payloads, Environment.CurrentDirectory + "\\report.htm", commandReader.CaptureFileName, Output.OutputType.HTML);
            //call saz analyzer
            System.Diagnostics.Process.Start(Environment.CurrentDirectory + "\\sazanalyzer.exe", Environment.CurrentDirectory + "\\raw.saz");
            FiddlerOutput.GenerateOutput(Requests, Responses, Payloads, commandReader.OutputFile, commandReader.CaptureFileName);

            Console.WriteLine("Output generated");
          
            // Pause so we can see the results when launched from Network Monitor
            Console.WriteLine();
            Console.WriteLine("It took {0} to generate the output file and {1} to execute the entire code ", (DateTime.Now - OutputGenerationTime), (DateTime.Now - ProgramStartTime));
            Console.WriteLine("Now launching the output files");

            if (File.Exists(Environment.CurrentDirectory + "\\raw.saz"))
                System.Diagnostics.Process.Start(Environment.CurrentDirectory + "\\raw.saz");
            if (File.Exists(Environment.CurrentDirectory + "\\report.htm"))
                System.Diagnostics.Process.Start(Environment.CurrentDirectory + "\\report.htm");
            if (File.Exists(Environment.CurrentDirectory + "\\sazanalyzer.html"))
                System.Diagnostics.Process.Start(Environment.CurrentDirectory + "\\sazanalyzer.html");
            Console.WriteLine("Finished Processing..Thank you for using the tool.Please provide feedback to mofasiul@microsoft.com");
            //Console.ReadKey();

            if (initialized)
            {
                CloseNMAPI();
            }
        }
        
        //fasi
        /// <summary>
        /// Used to ask and then print out extended information about a specific frame
        /// </summary>
        /// <param name="hParsedFrame">Parsed Frame</param>
        /// <param name="frameNumber">Frame Number to Display</param>
        /// <param name="command">Command Line Parameters</param>
        private static void PrintParsedFrameInformation(IntPtr hParsedFrame, uint frameNumber, CommandLineArguments command,uint payloadid,IntPtr hFrameParser)
        {

            Dictionary<string, string> fieldDict = new Dictionary<string, string>();
            


            uint errno;
            uint ulFieldCount;
            string ds = "Frame #" + (frameNumber + 1);

            // Is Selected
            if (command.IsSelected(frameNumber))
            {
                ds += " (Selected)";
            }

            // Get Frame Timestamp
            ulong timestamp;
            errno = NetmonAPI.NmGetFrameTimeStamp(hParsedFrame, out timestamp);
            if (errno == ERROR_SUCCESS)
            {
                ds += " " + DateTime.FromFileTimeUtc((long)timestamp).ToString();
            }
            else
            {
                ds += " Timestamp Couldn't be Retrieved.";
            }

            Console.WriteLine(ds);
            Console.Write("Print Frame Info? (y/n) ");

            char key = Console.ReadKey().KeyChar;
            Console.WriteLine();

            if (key == 'y' || key == 'Y')
            {
                errno = NetmonAPI.NmGetFieldCount(hParsedFrame, out ulFieldCount);

                for (uint fid = 0; fid < ulFieldCount; fid++)
                {
                    // Get Field Name
                    ////char[] name = new char[BUFFER_SIZE * 2];
                    ////unsafe
                    ////{
                    ////    fixed (char* pstr = name)
                    ////    {
                    ////        errno = NetmonAPI.NmGetFieldName(hParsedFrame, fid, NmParsedFieldNames.NamePath, BUFFER_SIZE * 2, pstr);
                    ////    }
                    ////}

                    ////if (errno == ERROR_SUCCESS)
                    ////{
                    ////    keyDict = new string(name).Replace("\0", string.Empty) + ": ";
                    ////    Console.Write(keyDict);
                    ////}
                    ////else
                    ////{ 
                    ////    Console.WriteLine("Error Retrieving Field, NmGetFieldName Returned: " + errno);
                    ////     continue;
                    ////}

                    ////// Get Field Value as displayed in Netmon UI
                    ////name = new char[BUFFER_SIZE];
                    ////unsafe
                    ////{
                    ////    fixed (char* pstr = name)
                    ////    {
                    ////        errno = NetmonAPI.NmGetFieldName(hParsedFrame, fid, NmParsedFieldNames.FieldDisplayString, BUFFER_SIZE, pstr);
                    ////    }
                    ////}

                    ////if (errno == ERROR_SUCCESS)
                    ////{
                    ////    valueDict = new string(name).Replace("\0", string.Empty);
                    ////    Console.WriteLine(valueDict);
                    ////    fieldDict.Add(keyDict, valueDict);
                    ////}
                    ////else if (errno == ERROR_NOT_FOUND)
                    ////{
                    ////    Program.PrintParsedFrameFieldValue(hParsedFrame, fid);
                    ////}
                    ////else
                    ////{
                    ////    Console.WriteLine("Error Retrieving Value, NmGetFieldName Returned: " + errno);
                    ////    continue;
                    ////}


                   


                    //logic to get the RAW http data
                    UInt32 pulFieldOffset;
                    UInt32 pulFieldBitLength;

                    errno = NetmonAPI.NmGetFieldOffsetAndSize(hParsedFrame, fid, out pulFieldOffset, out pulFieldBitLength);
                  
                    //errno = NetmonAPI.NmAddField(hFrameParser, "tcp.tcppayload", out field);

                    byte[] name2 = new byte[pulFieldBitLength];
                     


                }

                Console.WriteLine();
                // errno = NetmonAPI.NmGetFrame(captureFile, ulFrameNumber, out hRawFrame);
                //////////////////////
              //char[]  name2 = new char[1054];
              //  unsafe
              //  {
              //      fixed (char* pstr = name2)
              //       {
              //           uint x = (uint)fieldDict.Count;
              //           //errno = NetmonAPI.NmGetFieldValueString(hParsedFrame, x-1, 1460, pstr);
                         
              //          //errno-NetmonAPI.NmGetFieldOffsetAndSize(hParsedFrame,
              //       }
              //  }

            }
        }

        /// <summary>
        /// Prints out a field's value if the display string couldn't be found.
        /// </summary>
        /// <param name="hParsedFrame">Parsed Frame</param>
        /// <param name="fieldId">Field Number to Display</param>
        private static void PrintParsedFrameFieldValue(IntPtr hParsedFrame, uint fieldId)
        {
            NmParsedFieldInfo parsedField = new NmParsedFieldInfo();
            parsedField.Size = (ushort)System.Runtime.InteropServices.Marshal.SizeOf(parsedField);

            uint errno = NetmonAPI.NmGetParsedFieldInfo(hParsedFrame, fieldId, parsedField.Size, ref parsedField);
            if (errno == ERROR_SUCCESS)
            {
                if (parsedField.NplDataTypeNameLength != 0)
                {
                    char[] name = new char[BUFFER_SIZE];
                    unsafe
                    {
                        fixed (char* pstr = name)
                        {
                            errno = NetmonAPI.NmGetFieldName(hParsedFrame, fieldId, NmParsedFieldNames.DataTypeName, BUFFER_SIZE, pstr);
                        }
                    }

                    Console.Write("(" + new string(name).Replace("\0", string.Empty) + ") ");
                }

                if (parsedField.FieldBitLength > 0)
                {
                    byte number8Bit = 0;
                    ushort number16Bit = 0;
                    uint number32Bit = 0;
                    ulong number64Bit = 0;
                    ulong rl = parsedField.ValueBufferLength;

                    switch (parsedField.ValueType)
                    {
                        case FieldType.VT_UI1:
                            errno = NetmonAPI.NmGetFieldValueNumber8Bit(hParsedFrame, fieldId, out number8Bit);
                            if (errno == ERROR_SUCCESS)
                            {
                                Console.WriteLine(number8Bit);
                            }
                            else
                            {
                                Console.WriteLine("Error " + errno);
                            }

                            break;
                        case FieldType.VT_I1:
                            errno = NetmonAPI.NmGetFieldValueNumber8Bit(hParsedFrame, fieldId, out number8Bit);
                            if (errno == ERROR_SUCCESS)
                            {
                                Console.WriteLine((sbyte)number8Bit);
                            }
                            else
                            {
                                Console.WriteLine("Error " + errno);
                            }

                            break;
                        case FieldType.VT_UI2:
                            errno = NetmonAPI.NmGetFieldValueNumber16Bit(hParsedFrame, fieldId, out number16Bit);
                            if (errno == ERROR_SUCCESS)
                            {
                                Console.WriteLine(number16Bit);
                            }
                            else
                            {
                                Console.WriteLine("Error " + errno);
                            }

                            break;
                        case FieldType.VT_I2:
                            errno = NetmonAPI.NmGetFieldValueNumber16Bit(hParsedFrame, fieldId, out number16Bit);
                            if (errno == ERROR_SUCCESS)
                            {
                                Console.WriteLine((short)number16Bit);
                            }
                            else
                            {
                                Console.WriteLine("Error " + errno);
                            }

                            break;
                        case FieldType.VT_UI4:
                            errno = NetmonAPI.NmGetFieldValueNumber32Bit(hParsedFrame, fieldId, out number32Bit);
                            if (errno == ERROR_SUCCESS)
                            {
                                Console.WriteLine(number32Bit);
                            }
                            else
                            {
                                Console.WriteLine("Error " + errno);
                            }

                            break;
                        case FieldType.VT_I4:
                            errno = NetmonAPI.NmGetFieldValueNumber32Bit(hParsedFrame, fieldId, out number32Bit);
                            if (errno == ERROR_SUCCESS)
                            {
                                Console.WriteLine((int)number32Bit);
                            }
                            else
                            {
                                Console.WriteLine("Error " + errno);
                            }

                            break;
                        case FieldType.VT_UI8:
                            errno = NetmonAPI.NmGetFieldValueNumber64Bit(hParsedFrame, fieldId, out number64Bit);
                            if (errno == ERROR_SUCCESS)
                            {
                                Console.WriteLine(number64Bit);
                            }
                            else
                            {
                                Console.WriteLine("Error " + errno);
                            }

                            break;
                        case FieldType.VT_I8:
                            errno = NetmonAPI.NmGetFieldValueNumber64Bit(hParsedFrame, fieldId, out number64Bit);
                            if (errno == ERROR_SUCCESS)
                            {
                                Console.WriteLine((long)number64Bit);
                            }
                            else
                            {
                                Console.WriteLine("Error " + errno);
                            }

                            break;
                        case FieldType.VT_ARRAY | FieldType.VT_UI1:
                            byte[] byteArray = new byte[BUFFER_SIZE];
                            unsafe
                            {
                                fixed (byte* barr = byteArray)
                                {
                                    errno = NetmonAPI.NmGetFieldValueByteArray(hParsedFrame, fieldId, BUFFER_SIZE, barr, out number32Bit);
                                }
                            }

                            if (errno == ERROR_SUCCESS)
                            {
                                for (uint i = 0; i < number32Bit; i++)
                                {
                                    Console.Write(byteArray[i].ToString("X2") + " ");
                                }

                                if ((parsedField.FieldBitLength >> 3) > number32Bit)
                                {
                                    Console.Write(" ... " + ((parsedField.FieldBitLength >> 3) - number32Bit) + " more bytes not displayed");
                                }

                                Console.WriteLine();
                            }
                            else if (errno == ERROR_RESOURCE_NOT_AVAILABLE)
                            {
                                Console.WriteLine("The field is a container");
                            }

                            break;
                        case FieldType.VT_LPWSTR:
                            char[] name = new char[BUFFER_SIZE];
                            unsafe
                            {
                                fixed (char* pstr = name)
                                {
                                    errno = NetmonAPI.NmGetFieldValueString(hParsedFrame, fieldId, BUFFER_SIZE, pstr);
                                }
                            }

                            if (errno == ERROR_SUCCESS)
                            {
                                Console.WriteLine(new string(name).Replace("\0", string.Empty));
                            }
                            else
                            {
                                Console.WriteLine("String is too long to display");
                            }

                            break;
                        case FieldType.VT_LPSTR:
                            Console.WriteLine("Should not occur");
                            break;
                        case FieldType.VT_EMPTY:
                            Console.WriteLine("Struct or Array types expect description");
                            break;
                        default:
                            Console.WriteLine("Unknown Type " + parsedField.ValueType);
                            break;
                    }
                }
                else
                {
                    Console.WriteLine("Empty");
                }
            }
            else
            {
                Console.WriteLine("Could Not Retrieve Parsed Field Info " + errno);
            }
        }

        public static void GetFramePayload(IntPtr hParsedFrame, IntPtr mFrameParser, IntPtr RawFrame, uint mTCPPayLoadLengthID, uint mTCPDataOffsetID, uint mTCPSrcPortID,uint ulframeNumber,string type)
        {
            
            uint errno=0;
            byte[] result = null;
            byte[] result2=null;
            byte[] decodedstream = null;
            string tempstring = null;
            string temppath = null;
            FileStream fs = null;
            byte[] pattern = new byte[] { 31, 139, 8 };
            List<int> encodedstream=null;
            byte[] headerstream = null;

            NM_NPL_PROPERTY_INFO propinfo = new NM_NPL_PROPERTY_INFO();
            propinfo.Size = (ushort)System.Runtime.InteropServices.Marshal.SizeOf(propinfo);
            errno = NetmonAPI.NmGetPropertyInfo(mFrameParser, mTCPPayLoadLengthID, ref propinfo);
            if (errno != 0)
            {
                Console.WriteLine("Error NmGetPropertyInfo Frame # error : " + errno.ToString());

            }

            byte[] val = new byte[propinfo.ValueSize];
            uint retlen;
            NmPropertyValueType vtype;
            unsafe
            {
                fixed (byte* pstr = val)
                {
                    errno = NetmonAPI.NmGetPropertyById(mFrameParser, mTCPPayLoadLengthID, propinfo.ValueSize, pstr, out retlen, out vtype, 0, null);
                }
            }

            if (errno != 0)
            {
                Console.WriteLine("Error NmGetPropertyById Frame"  + errno.ToString());

            }

            uint paylen = (uint)val[0] + ((uint)val[1] << 8) + ((uint)val[2] << 16) + ((uint)val[3] << 24);

            // Get the Data Offset, used to determine the TCP header size
            byte TCPHeaderSize;
            errno = NetmonAPI.NmGetFieldValueNumber8Bit(hParsedFrame, mTCPDataOffsetID, out TCPHeaderSize);
            if (errno != 0)
            {
                Console.WriteLine("Error NmGetFieldValueNumber8Bit Frame #error : " + errno.ToString());
            }

            // Get the Offset of TCP.SrcPort which is the first field in TCP.
            uint TCPSrcOffset;
            uint TCPSrcSize;
            errno = NetmonAPI.NmGetFieldOffsetAndSize(hParsedFrame, mTCPSrcPortID, out TCPSrcOffset, out TCPSrcSize);
            if (errno != 0)
            {
                Console.WriteLine("Error NmGetFieldValueNumber8Bit Frame # error : " + errno.ToString());
            }

            if (paylen > 0)
            {
                result = new byte[paylen];
                unsafe
                {
                    fixed (byte* pstr = result)
                    {
                        errno = NetmonAPI.NmGetPartialRawFrame(RawFrame, (uint)(TCPSrcOffset / 8 + TCPHeaderSize * 4), paylen, pstr, out retlen);
                       
                    }
                }

                if (errno != 0)
                {
                    Console.WriteLine("Error NmGetFieldValueNumber8Bit Frame #error : " + errno.ToString());
                    result = null;
                }
                else
                {
                    
                    

                    encodedstream = result.IndexOfSequence2(pattern, 0);
                    if (encodedstream.Count >= 10)
                    {
                        result2 = new byte[result.Length - encodedstream[0]];
                        headerstream = new byte[encodedstream[0]];
                        Array.Copy(result, encodedstream[0], result2, 0, result2.Length);
                        Array.Copy(result, 0, headerstream, 0, encodedstream[0]);
                        decodedstream = Decompress(result2);

                        byte[] newarray = new byte[headerstream.Length + decodedstream.Length];
                        Array.Copy(headerstream, newarray, headerstream.Length);
                        Array.Copy(decodedstream, 0, newarray, headerstream.Length, decodedstream.Length);

                        tempstring = Encoding.UTF8.GetString(newarray, 0, newarray.Length);
                    
                        if (tempstring != string.Empty)
                        {
                            temppath = Environment.CurrentDirectory + "\\Fiddler" + "\\" + type + "\\" + ulframeNumber + ".txt";
                            fs = File.Create(temppath);
                            fs.Write(newarray, 0, newarray.Length);
                            fs.Close();


                        }

                    }
                    else
                    {
                        tempstring = Encoding.UTF8.GetString(result, 0, result.Length);
                                             

                        if (tempstring != string.Empty)
                        {
                         temppath = Environment.CurrentDirectory + "\\Fiddler" + "\\" + type + "\\" + ulframeNumber + ".txt";
                        FileStream stream = new FileStream(temppath, FileMode.Append, FileAccess.Write);                        
                        stream.Write(result, 0,result.Length);
                        stream.Close();

                        }


                    }
                }
            }
            else
                retlen = 0;

          
            
    
        }


            


        static byte[] Decompress(byte[] gzip)
        {
            // Create a GZIP stream with decompression mode.
            // ... Then create a buffer and write into while reading from the GZIP stream.
            using (GZipStream stream = new GZipStream(new MemoryStream(gzip), CompressionMode.Decompress))
            {
                const int size = 4096;
                byte[] buffer = new byte[size];
                using (MemoryStream memory = new MemoryStream())
                {
                    int count = 0;
                    do
                    {
                        count = stream.Read(buffer, 0, size);
                        if (count > 0)
                        {
                            memory.Write(buffer, 0, count);
                        }
                    }
                    while (count > 0);
                    return memory.ToArray();
                }
            }
        }


      



        public static StringBuilder GetRequestResponseRawStream(IntPtr hParsedFrame, uint payLodId, int frameCount)
        {
            StringBuilder rawHTTPStringRequest = new StringBuilder();
            try
            {
                byte[] rawHTTPByte;
                char[] rawHTTPChar;
                UInt32 pulFieldOffset;
                UInt32 pulFieldBitLength;
                UInt32 ulReturnLength;
                uint errno = 0;
                string tempstring = null;
               
                StringBuilder rawHTTPStringResponse = new StringBuilder();

                if (payLodId != 0)
                {
                    //rawHTTPByte = new byte[9999999];
                    errno = NetmonAPI.NmGetFieldOffsetAndSize(hParsedFrame, payLodId, out pulFieldOffset, out pulFieldBitLength);
                    if (errno == 0)
                    {
                        rawHTTPByte = new byte[99999999];
                        unsafe
                        {
                            fixed (byte* pstr = rawHTTPByte)
                            {
                                errno = NetmonAPI.NmGetFieldValueByteArray(hParsedFrame, payLodId, pulFieldBitLength, pstr, out ulReturnLength);
                                //errno=NetmonAPI.NmGetPartialRawFrame(hParsedFrame,pulFieldOffset,
                                tempstring = Encoding.UTF8.GetString(rawHTTPByte, 0, rawHTTPByte.Length);
                                rawHTTPStringRequest.Append(tempstring);
                            }
                        }
                    }
                }
                else
                {
                    rawHTTPChar = new char[9999999];
                    for (uint j = 0; j < frameCount; j++)
                    {
                        errno = NetmonAPI.NmGetFieldOffsetAndSize(hParsedFrame, j, out pulFieldOffset, out pulFieldBitLength);
                        if (errno == 0)
                        {
                           //* rawHTTPChar = new char[pulFieldBitLength];

                            unsafe
                            {

                                fixed (char* pstr = rawHTTPChar)
                                {
                                    errno = NetmonAPI.NmGetFieldValueString(hParsedFrame, j, pulFieldBitLength, pstr);

                                    //  NetmonAPI.nmgetfield


                                    if (errno == 0)
                                    {

                                        rawHTTPStringRequest.Append(rawHTTPChar);
                                    }
                                    rawHTTPChar = null;
                                }
                            }
                        }
                    }
                }

                Console.WriteLine("printing the string");
                Console.WriteLine(rawHTTPStringRequest.ToString());
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception:....");
                Console.WriteLine(ex.ToString());
                Console.ReadLine();
            }
            return rawHTTPStringRequest;
        }


       public static void SetUpDirectory()
        {
            
          

            string CurrDirectory = Environment.CurrentDirectory + "\\Fiddler";

            if (File.Exists(Environment.CurrentDirectory + "\\raw.saz"))
                File.Delete(Environment.CurrentDirectory + "\\raw.saz");

            if (File.Exists(Environment.CurrentDirectory + "\\report.htm"))
                File.Delete(Environment.CurrentDirectory + "\\report.htm");

            if (Directory.Exists(CurrDirectory))
            {
                DeleteDirectory(CurrDirectory);
            }

            DirectoryInfo DirecInfo = new DirectoryInfo(CurrDirectory);
            DirecInfo = new DirectoryInfo(CurrDirectory + "\\Request");
            DirecInfo.Create();
            DirecInfo = new DirectoryInfo(CurrDirectory + "\\Response");
            DirecInfo.Create();
            DirecInfo = new DirectoryInfo(CurrDirectory + "\\Payload");
            DirecInfo.Create();
            DirecInfo = new DirectoryInfo(CurrDirectory + "\\raw");
            DirecInfo.Create();
            DirecInfo = new DirectoryInfo(CurrDirectory + "\\raw\\raw");
            DirecInfo.Create();
           
           
        }


       public static void DeleteDirectory(string CurrDirectory)
       {
           
            if(Directory.Exists(CurrDirectory))
            {
                string[] files = Directory.GetFiles(CurrDirectory);
                string[] dirs = Directory.GetDirectories(CurrDirectory);

                foreach (string file in files)
                {
                    File.Delete(file);
                }
                foreach (string dir in dirs)
                {
                    DeleteDirectory(dir);
                }

                Directory.Delete(CurrDirectory);
            }
       }


       #region API Initialization and Cleanup
       /// <summary>
       /// Called to close the Network Monitor API when we're done
       /// </summary>
       private static void CloseNMAPI()
       {
           ulong errno = NetmonAPI.NmApiClose();
           if (errno != ERROR_SUCCESS)
           {
               Console.WriteLine("Error unloading NMAPI Error Number = " + errno);
           }
       }

       /// <summary>
       /// Takes care of initializing the Network Monitor API
       /// </summary>
       /// <returns>true on success</returns>
       private static bool InitializeNMAPI()
       {
           // Initialize the NMAPI          
           NM_API_CONFIGURATION apiConfig = new NM_API_CONFIGURATION();
           apiConfig.Size = (ushort)System.Runtime.InteropServices.Marshal.SizeOf(apiConfig);
           ulong errno = NetmonAPI.NmGetApiConfiguration(ref apiConfig);
           if (errno != ERROR_SUCCESS)
           {
               Console.WriteLine("Failed to Get NMAPI Configuration Error Number = " + errno);
               return false;
           }

           // Set possible configuration values for API Initialization Here
           ////apiConfig.CaptureEngineCountLimit = 4;

           errno = NetmonAPI.NmApiInitialize(ref apiConfig);
           if (errno != ERROR_SUCCESS)
           {
               Console.WriteLine("Failed to Initialize the NMAPI Error Number = " + errno);
               return false;
           }

           return true;
       }
       #endregion
    }
}
