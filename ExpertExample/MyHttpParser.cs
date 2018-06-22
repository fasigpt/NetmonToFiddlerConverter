using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.NetworkMonitor;
using Microsoft.NetworkMonitor.Automation;
using System.Linq;

namespace ExpertExample
{
    public enum HTTPFrameType { Invalid, Payload, Request, RequestPayload, Response, ResponsePayload, Undeterministic };

    public class HTTPFrame
    {
        #region Constants
            private const ulong ERROR_SUCCESS = 0;
            private const ulong ERROR_NOT_FOUND = 1168;
            private const ulong ERROR_RESOURCE_NOT_AVAILABLE = 5006;
            private const uint BUFFER_SIZE = 512;            
        #endregion

        #region Members
            public uint Number { get; set; }
            public DateTime TimeStamp { get; set; }
            public string SourceIP { get; set; }
            public uint SourcePort { get; set; }
            public string DestinationIP { get; set; }
            public uint DestinationPort { get; set; }
            public IntPtr hParsedFrame { get; set; }      
            public uint payloadID {get;set;}
            public int frameFieldCount {get;set;}    
            public IntPtr hFrameParser { get; set; }
            public IntPtr hRawFrame { get; set; }
            public uint tcpPayloadLengthID { get; set; }
            public uint tcpSrcOffset { get; set; }
            public uint tcpHeaderSize { get; set; }
            public uint paylen { get; set; }
            public uint AckNumber { get; set; }
            public uint SequenceStart { get; set; }
            public uint SequenceEnd { get; set; }

        #endregion

        #region Constructor
        public HTTPFrame(MyHttpParser parser, uint frameNumber)
        {
            hParsedFrame = parser.hParsedFrame;
            hFrameParser = parser.hFrameParser;
            hRawFrame = parser.hRawFrame;
            tcpPayloadLengthID = parser.TCPPayloadLengthID;
            tcpSrcOffset = parser.tcpsrcoffset;
            tcpHeaderSize = parser.tcpheadersize;
            paylen = parser.payLen;

            string FieldValue;
            string FieldValueAck;
            string Sequence;
            uint errno;
            ulong timestamp;
            Number = frameNumber;
            #region Set TimeStamp
            errno = NetmonAPI.NmGetFrameTimeStamp(hParsedFrame, out timestamp);
            if (errno == ERROR_SUCCESS)
            {
                TimeStamp = DateTime.FromFileTimeUtc((long)timestamp);
            }
            else
                TimeStamp = DateTime.MinValue;
            #endregion


            #region Set Acknowledge Number
            FieldValueAck = parser.GetFieldValueByFieldName(hParsedFrame, parser.GetFieldName(".tcp.acknowledgementnumber"));
            if (FieldValueAck.Contains("("))
            {
                

               AckNumber = Convert.ToUInt32(FieldValueAck.Substring(0,(FieldValueAck.IndexOf('(')-1)));
            }
            else
                AckNumber = Convert.ToUInt32(FieldValueAck);
            #endregion


            #region Set Sequence Number
            Sequence = parser.GetFieldValueByFieldName(hParsedFrame, parser.GetFieldName(".tcp.sequencenumber"));
            if (Sequence.Contains("("))
            {

                SequenceStart = Convert.ToUInt32(Sequence.Substring(0, (Sequence.IndexOf('(') - 1)));
                SequenceEnd = SequenceStart + paylen;
            }
            else
            {
                SequenceStart = Convert.ToUInt32(FieldValueAck);
                SequenceEnd = SequenceStart + paylen;
            }
            #endregion



            #region Set Source IP
            var keys = from KeyValuePair<uint, string> pair in parser.FrameFields
                       where (
                           (pair.Value.ToLower().Contains("sourceaddress")) &&
                           (pair.Value.ToLower().Contains("ip"))
                           )
                       select pair.Key;
            if (keys.Count() != 0)
                //SourceIP = GetFieldValueByIndex(hParsedFrame, keys.ElementAt(0));
                SourceIP =  parser.GetFieldValueByFieldName(hParsedFrame, parser.FrameFields[keys.ElementAt(0)]);
            else
                SourceIP = string.Empty;
            #endregion

            #region Set Source Port
            FieldValue = parser.GetFieldValueByFieldName(hParsedFrame, parser.GetFieldName("Tcp.SrcPort")); 
            if (FieldValue.Contains("("))
            {
                SourcePort = Convert.ToUInt32(FieldValue.Substring(FieldValue.IndexOf('(') + 1, FieldValue.IndexOf(')') - FieldValue.IndexOf('(') - 1));

            }
            else
                SourcePort = Convert.ToUInt32(FieldValue);
            #endregion

            #region Set Destination IP
            var keys1 = from KeyValuePair<uint, string> pair in parser.FrameFields
                        where (
                            (pair.Value.ToLower().Contains("destinationaddress")) &&
                            (pair.Value.ToLower().Contains("ip"))
                            )
                        select pair.Key;
            if (keys1.Count() != 0)
                //DestinationIP = GetFieldValueByIndex(hParsedFrame, keys.ElementAt(0));
                DestinationIP = parser.GetFieldValueByFieldName(hParsedFrame, parser.FrameFields[keys1.ElementAt(0)]);
            else
                DestinationIP = string.Empty;
            #endregion

            #region Set Destination Port
            FieldValue = parser.GetFieldValueByFieldName(hParsedFrame, parser.GetFieldName("Tcp.DstPort"));
            if (FieldValue.Contains("("))
            {
                DestinationPort = Convert.ToUInt32(FieldValue.Substring(FieldValue.IndexOf('(') + 1, FieldValue.IndexOf(')') - FieldValue.IndexOf('(') - 1));

            }
            else
                DestinationPort = Convert.ToUInt32(FieldValue);
            #endregion

            #region get the frameid from Framfields which contains the raw payload

            var keys2 = from KeyValuePair<uint, string> pair in parser.FrameFields
                        where (
                            (pair.Value.ToLower().Contains("tcp.tcppayload.http.chunkedpayload.chunkbody.chunkpayloadcontinuation")) ||
                            (pair.Value.ToLower().Contains(".tcp.tcpcontinuationdata"))||                            
                            (pair.Value.ToLower().Contains(".tcp.tcppayload.http.httpincompletepayload"))
                            )
                        select pair.Key;

            if (keys2.Count() != 0)
                payloadID = keys2.ElementAt(0);
            else
                payloadID = 0; 
            #endregion

            frameFieldCount = parser.FrameFields.Count;


           

        }

        #endregion

        #region Virtual Functions that should be implemented by the derived classes
        public virtual void Display()
        {
            Console.WriteLine("Frame Number\t: {0}", Number);
            Console.WriteLine("Time Stamp\t: {0}", TimeStamp);
            Console.WriteLine("Src Address\t: {0}:{1}", SourceIP, SourcePort);
            Console.WriteLine("Dest Address\t: {0}:{1}", DestinationIP, DestinationPort);
        }
        #endregion
    }

    public class HTTPHeader
    {
        public string HeaderName { get; set; }
        public string HeaderValue { get; set; }
        public HTTPHeader() { HeaderName = ""; HeaderValue = ""; }
        public HTTPHeader(string Name, string headerValue)
        {
            HeaderName = Name; HeaderValue = headerValue;
        }
    }

    public class NonPayLoadHTTPFrame : HTTPFrame
    {
        public Dictionary<int, HTTPHeader> Headers { get; set; }
        
        //public Dictionary<string, string> Headers { get; set; }

        /// <summary>
        /// Constructor for a Non PayLoad HTTP Frame, here we initialize the Headers collection for Request and Response HTTP Frames
        /// </summary>
        /// <param name="parser">Will use the parser object to get the field values</param>
        /// <param name="frameNumber">The Frame number that needs to be parsed and its fields be populated</param>
        #region Constructor
        public NonPayLoadHTTPFrame(MyHttpParser parser, uint frameNumber)
            : base(parser, frameNumber)
        {
            IntPtr hParsedFrame = parser.hParsedFrame;
            Headers = new Dictionary<int, HTTPHeader>();
            var keys = from KeyValuePair<uint, string> pair in parser.FrameFields where pair.Value.Contains(".headerfields.") select pair.Key;
            if (keys.Count() > 0)
            {
                int index = 1;
                foreach (uint fid in keys)
                {
                    string headerName = parser.FrameFields[fid].Substring(parser.FrameFields[fid].LastIndexOf("headerfields.") + 13).ToLower();
                    if (headerName == "contentlength")
                    {
                        Headers.Add(index, new HTTPHeader(headerName, MyHttpParser.PrintParsedFrameFieldValue(hParsedFrame, fid)));
                    }
                    else
                    {
                        Headers.Add(index, new HTTPHeader(headerName, parser.GetFieldValueByIndex(hParsedFrame, fid)));
                    }
                    index++;
                }
            }
            #region comment
            /*
            Headers = new Dictionary<string, string>();
            var keys = from KeyValuePair<uint, string> pair in parser.FrameFields where pair.Value.Contains(".headerfields.") select pair.Key;
            if (keys.Count() > 0)
            {
                foreach (uint fid in keys)
                {
                    //Set the header collection here. Make sure to factor for the situation when the same header is encountered twice.
                    //In that case, find the earlier value and append this value to it seperated by a ";"
                    //if (parser.FrameFields[fid].EndsWith("http.request.headerfields"))
                    //    continue;
                    //else

                    //There is a problem here
                    // if there are multiple set-cookie header comming from the server
                    // then these should be added as different headers and not appended
                    // So I need to modify this dictionary so that the Key is actually an int and the value is a name value pair
                    // This will mean that I also have to modify the display logic where I am retrieving the headers
                    
                    {
                        string header = parser.FrameFields[fid].Substring(parser.FrameFields[fid].LastIndexOf("headerfields.") + 13).ToLower();
                        string str ;
                        str = Headers.FirstOrDefault(x =>
                                         (
                                         header.Contains(x.Key+".")
                                         )
                             ).Key;
                        if (str != null)
                        {
                            switch (str)
                            {
                                case "contenttype":
                                    break;
                                default:
                                    Headers[str] = Headers[str] + ";" + parser.GetFieldValueByIndex(hParsedFrame, fid);
                                    break;
                            }
                            
                        }
                        else
                        {
                            switch(header)
                            {
                                case "host":
                                    Headers.Add(header, parser.GetFieldValueByIndex(hParsedFrame, fid).Replace(" ",""));
                                    break;
                                default:
                                    Headers.Add(header, parser.GetFieldValueByIndex(hParsedFrame, fid));
                                    break;
                            }
                            
                        }
                    }
                }
            }
    */
            #endregion
            
        }
        #endregion

        #region Methods
        public string GetHeaderValue(string headerName)
        {
            headerName = headerName.ToLower();
            int index = GetHeaderIndex(headerName);
            if (index > 0)
            {
                return this.Headers[index].HeaderValue;
            }
            else
                return "";
        }
        public int GetHeaderIndex(string headerName)
        {
            headerName = headerName.ToLower();            
            int returnValue = Headers.FirstOrDefault( x=>
                    (
                    x.Value.HeaderName == headerName
                    )
                ).Key;
            return returnValue;
        }

        public override void Display()
        {
            base.Display();
        }
        #endregion

        
    }

    public class HTTPRequestFrame : NonPayLoadHTTPFrame
    {
        public string URL { get; set; }
        public string Method { get; set; }
        public string QueryString { get; set; }
        public string protocolVersion { get; set; }
        public Dictionary<string, string> cookieDict = new Dictionary<string, string>();
        public Dictionary<string, string> queryStrDict = new Dictionary<string, string>();

        #region Constructor
        public HTTPRequestFrame(MyHttpParser parser, uint frameNumber)
            : base(parser, frameNumber)
        {
            IntPtr hParsedFrame = parser.hParsedFrame;


            #region set protocolVersion
            protocolVersion = parser.GetFieldValueByFieldName(hParsedFrame, parser.GetFieldName("http.request.protocolversion")); 
            #endregion

            #region Set HTTPMethod of the request
            Method = parser.GetFieldValueByFieldName(hParsedFrame, parser.GetFieldName("Http.Request.Command"));
            #endregion

            #region Populate URL of the request
            URL = parser.GetFieldValueByFieldName(hParsedFrame, parser.GetFieldName("Http.Request.URI.Uri.Location"));
            #endregion

            #region Set Query String
            QueryString = "";
            
            var keys = from KeyValuePair<uint, string> pair in parser.FrameFields where pair.Value.Contains("http.request.uri.uri.parameters") select pair.Key;
            if (keys.Count() > 0)
            {
                QueryString = "?";
                foreach (uint fid in keys)
                {                   
                    if (parser.FrameFields[fid].EndsWith("http.request.uri.uri.parameters")
                        || (parser.FrameFields[fid].EndsWith("http.request.uri.uri.parameters.fields")))
                        continue;
                    else
                    {
                        queryStrDict.Add((parser.FrameFields[fid].Substring(parser.FrameFields[fid].IndexOf("fields.") + 7)),parser.GetFieldValueByIndex(hParsedFrame, fid));
                        QueryString += "&" + parser.FrameFields[fid].Substring(parser.FrameFields[fid].IndexOf("fields.") + 7) + "=" + parser.GetFieldValueByIndex(hParsedFrame, fid) ;
                    }
                }
                QueryString = QueryString.Replace("?&","?");
            }
            
            #endregion

            #region Format Request Headers

            #region Make Sure that the HOST header does not contain any spaces
            int headerIndex = this.GetHeaderIndex("host");
            if(headerIndex>0)
                Headers[headerIndex].HeaderValue =  this.GetHeaderValue("host").Replace(" ", "");
            #endregion

            #region Make sure that the Request Cookies header are all ";" seperated and duplicate headers are removed
            var keys1 = from KeyValuePair<int,HTTPHeader> pair in Headers where
                       (
                       pair.Value.HeaderName.Contains("cookie.cookies")
                       )
                       select pair.Key;

            if (keys1.Count() > 0)
            {
                StringBuilder cookieValues = new StringBuilder();
                foreach (int index in keys1)
                {
                    if (Headers[index].HeaderName != "cookie.cookies")
                    {
                        string temp = Headers[index].HeaderName.Replace("cookie.cookies.", "");
                        cookieDict.Add(temp, Headers[index].HeaderValue);
                        cookieValues.Append(temp + "=" + Headers[index].HeaderValue + ";");
                        
                    }
                    
                }
                string cookieValue = cookieValues.ToString();
                Headers[this.GetHeaderIndex("cookie")].HeaderValue = cookieValue.Substring(0,cookieValue.Length-1);

                var keys2 = (from KeyValuePair<int, HTTPHeader> pair in Headers
                             where
                                 (
                                 pair.Value.HeaderName.Contains("cookie.cookies")
                                 )
                             select pair.Key).FirstOrDefault();

                while (keys2 != 0)
                {
                    Headers.Remove(keys2);
                    keys2 = (from KeyValuePair<int, HTTPHeader> pair in Headers
                             where
                                 (
                                 pair.Value.HeaderName.Contains("cookie.cookies")
                                 )
                             select pair.Key).FirstOrDefault();
                }
            }

           

            #endregion

            #region Make sure that the Content Type header is handled properly and is present only once
            var keys3 = (from KeyValuePair<int, HTTPHeader> pair in Headers
                         where
                             (
                             pair.Value.HeaderName.Contains("contenttype.")
                             )
                         select pair.Key).FirstOrDefault();

            while (keys3 != 0)
            {
                Headers.Remove(keys3);
                keys3 = (from KeyValuePair<int, HTTPHeader> pair in Headers
                         where
                             (
                             pair.Value.HeaderName.Contains("contenttype.")
                             )
                         select pair.Key).FirstOrDefault();
            }
            #endregion
            #endregion
        }
        #endregion

        public override void Display()
        {

            base.Display();
            Console.WriteLine("URL\t\t: {0}", URL);
            Console.WriteLine("Query String\t: {0}", QueryString);

        }
    }

    public class HTTPResponseFrame : NonPayLoadHTTPFrame
    {
        public string URL { get; set; }
        public string StatusCode { get; set; }
        public string StatusText { get; set; }
        public string protocolVersion { get; set; }
        public Dictionary<int, string> cookieDict = new Dictionary<int, string>();

        #region Constructor
        public HTTPResponseFrame(MyHttpParser parser, uint frameNumber)
            : base(parser, frameNumber)
        {
            IntPtr hParsedFrame = parser.hParsedFrame;

            //There will be chances that the netmon parser is unable to find the corresponding Request for this Response, ignoring those scenarios for now
            //If we have to fix it, then it needs to be done in the Program.cs, since we dont have access to the Requests dictionary collection in this class
            //There is another good reason to ignore these for now. i.e.. we find the response however the client had already been waiting for some time and 
            //had issued another request, so the request that we end up finding, might not be the actual request for this response packet.
            URL = parser.GetFieldValueByFieldName(hParsedFrame, parser.GetFieldName("Tcp.TCPPayload.Http"));
            protocolVersion = parser.GetFieldValueByFieldName(hParsedFrame, parser.GetFieldName("http.response.protocolversion")); 

            StatusCode = parser.GetFieldValueByFieldName(hParsedFrame, parser.GetFieldName("Http.Response.StatusCode"));
            StatusText = parser.GetFieldValueByFieldName(hParsedFrame, parser.GetFieldName("Http.Response.StatusText"));
            #region Format Response Headers

            #region Make sure that the Content Type header is handled properly and is present only once

            

            var keys1 = (from KeyValuePair<int, HTTPHeader> pair in Headers
                        where
                            (
                            pair.Value.HeaderName.Contains("contenttype.")
                            )
                        select pair.Key).FirstOrDefault();


                while (keys1 != 0)
                {
                    Headers.Remove(keys1);
                    keys1 = (from KeyValuePair<int, HTTPHeader> pair in Headers
                             where
                                 (
                                 pair.Value.HeaderName.Contains("contenttype.")
                                 )
                             select pair.Key).FirstOrDefault();
                }


           
            #endregion


            #endregion

                #region Cookie

                var keys3 = from KeyValuePair<int, HTTPHeader> pair in Headers
                            where
                                (
                                pair.Value.HeaderName.Contains("set-cookie")
                                )
                            select pair.Key;

                if (keys3.Count() > 0)
                {
                    StringBuilder cookieValues = new StringBuilder();
                    foreach (int index in keys3)
                    {
                        cookieDict.Add(index, Headers[index].HeaderValue);

                  

                    }
                   
                } 
                #endregion


        }
        #endregion

        public override void Display()
        {

            base.Display();
            Console.WriteLine("URL\t: {0}", URL);
            Console.WriteLine("Status Code\t: {0}", StatusCode);

        }
    }

    public class HTTPPayloadFrame : HTTPFrame
    {
        public string URL { get; set; }
        public HTTPFrameType Type { get; set; }
        public HTTPPayloadFrame(MyHttpParser parser, uint frameNumber)
            : base(parser, frameNumber)
        {
            IntPtr hParsedFrame = parser.hParsedFrame;
            URL = parser.GetFieldValueByFieldName(hParsedFrame, parser.GetFieldName("Tcp.TCPPayload.Http"));
            
            Type = HTTPFrameType.Invalid;
        }

        public override void Display()
        {
            base.Display();
            Console.WriteLine("URL\t: {0}", URL);
            Console.WriteLine("Type\t: {0}", Type.ToString());
        }
    }
    

    public class MyHttpParser
    {
        #region Constants
            private const ulong ERROR_SUCCESS = 0;
            private const ulong ERROR_NOT_FOUND = 1168;
            private const ulong ERROR_RESOURCE_NOT_AVAILABLE = 5006;
            private const uint BUFFER_SIZE = 512;
        #endregion


            #region Variables
            //private static bool initialized;
            private static ParserCallbackDelegate pErrorCallBack = new ParserCallbackDelegate(ParserCallback);
            //Dictionary to hold the index location for fields in a frame
            private static Dictionary<uint, string> _frameFields = new Dictionary<uint, string>();
            public Dictionary<uint, string> FrameFields { get { return _frameFields; } }
            private IntPtr _hParsedFrame = IntPtr.Zero;
            public IntPtr hParsedFrame { get { return _hParsedFrame; } }
            private IntPtr _hFrameParser = IntPtr.Zero;
            public IntPtr hFrameParser { get { return _hFrameParser; } }
            private IntPtr _hRawFrame = IntPtr.Zero;
            public IntPtr hRawFrame { get { return _hRawFrame; } }
            private uint _TCPPayloadLengthID = 0;
            public uint TCPPayloadLengthID { get { return _TCPPayloadLengthID; } }
            public uint srcPortFrameID { get; set; }
            public uint dataOffsetID { get; set; }
            public uint tcpsrcoffset { get; set; }
            public uint tcpheadersize { get; set; }
            public uint payLen { get; set; } 
            #endregion
        
       

       

        public HTTPFrameType FrameType 
        {
            get
            {
                if (_hParsedFrame == IntPtr.Zero)
                {
                    throw new System.InvalidOperationException("Cannot check the value of this property without initializing with _hParsedFrame");
                }
                //Check if this is a valid HTTP over TCP FRAME or not
                if ((!GetFieldName(".tcp.").Equals(string.Empty)) && (!GetFieldName(".http.").Equals(string.Empty)))
                {
                    //Is this a Request Frame ?
                    if (!GetFieldName("Http.Request.Command").Equals(string.Empty))
                        return HTTPFrameType.Request;
                    else
                    {
                        //Is this a Response Frame ?
                        if (!GetFieldName("Http.Response.StatusCode").Equals(string.Empty))
                            return HTTPFrameType.Response;
                        else
                        {
                            //Did the netmon parser identify this packet as a HTTP Payload packet ?
                            if (!GetFieldName("Http._BuildHTTPConversation").Equals(string.Empty))
                                return HTTPFrameType.Payload;
                            else
                            {
                                //This is not a Request, not a Response and does not even contain a Payload field, but was identified as HTTP over TCP
                                //Have not encountered anything of this type, but if there is, then it is an UNDETERMINED type of a packet and 
                                // further investigation of this type of a packet is requiered
                                return HTTPFrameType.Undeterministic;
                            }
                        }
                    }
                }
                else
                {
                    //There are times when netmon parser does not recognizes certain HTTPPayload packets. This logic will cath those as well
                    //We will look for only those packets that contain the TCPContinuationData and are on the ports 80 / 8080. 
                    //This is how netmon classifies a packet as an HTTP packet
                    //In an ideal scenario I should be taking the port on which the website is running on as an input, however this is not a GUI based application 
                    //and certain assumptions are in order

                    string FieldValue = "";
                    string SRCPort = "";
                    FieldValue = GetFieldValueByFieldName(hParsedFrame, GetFieldName("Tcp.SrcPort"));
                    if (FieldValue.Contains("("))
                    {
                        SRCPort = FieldValue.Substring(FieldValue.IndexOf('(') + 1, FieldValue.IndexOf(')') - FieldValue.IndexOf('(') - 1);
                    }
                    else
                        SRCPort = FieldValue;

                    string DstPort = "";
                    FieldValue = GetFieldValueByFieldName(hParsedFrame, GetFieldName("Tcp.DstPort"));
                    if (FieldValue.Contains("("))
                    {
                        DstPort = FieldValue.Substring(FieldValue.IndexOf('(') + 1, FieldValue.IndexOf(')') - FieldValue.IndexOf('(') - 1);
                    }
                    else
                        DstPort = FieldValue;

                    
                    if (
                        (!GetFieldName(".tcp.").Equals(string.Empty)) && 
                        (!GetFieldName(".tcpcontinuationdata").Equals(string.Empty)) &&
                        (SRCPort.Equals("80") || SRCPort.Equals("8080") || (DstPort.Equals("80") || DstPort.Equals("8080") ))
                        )
                        return HTTPFrameType.Payload;
                    else
                        return HTTPFrameType.Invalid;
                }
            }
        }

        #region Constructor
        public MyHttpParser(IntPtr hParsedFrame, IntPtr hFrameParser, IntPtr hRawFrame,uint TCPPayloadLengthID)
        {
            _hParsedFrame = hParsedFrame;
            _hFrameParser = hFrameParser;
            _hRawFrame = hRawFrame;
            _TCPPayloadLengthID = TCPPayloadLengthID;

            #region Initialize the dictionary(FrameFields) which will hold the field names and their index within the frame
                uint ulFieldCount;
                uint errno = NetmonAPI.NmGetFieldCount(_hParsedFrame, out ulFieldCount);
                _frameFields.Clear();
                for (uint fid = 0; fid < ulFieldCount; fid++)
                {
                    #region Get Field Name and add it to the Dictionary along with its index(i.e.. fid)
                        string FieldName = GetFieldName(hParsedFrame, fid);
                        if (!FieldName.Equals(string.Empty))
                            _frameFields.Add(fid, FieldName.ToLower());
                    #endregion
                } 
            #endregion
            
            #region Check if this is an IPv4 OR an IPv6 frame
                //var Keys = from KeyValuePair<uint, string> pair in FrameFields where pair.Value.Contains("Ipv6") select pair.Key;
                //if (Keys.Count() > 0)
                //    IPv4 = false;
                //else
                //    IPv4 = true;
            #endregion


                #region get the Frame id for Srcport

                var keys5 = from KeyValuePair<uint, string> pair in _frameFields
                            where (
                                (pair.Value.ToLower().Contains("ipv4.tcp.srcport"))
                                )
                            select pair.Key;

                if (keys5.Count() != 0)
                    srcPortFrameID = keys5.ElementAt(0);
                else
                    srcPortFrameID = 0;
                #endregion


                #region get the frameid for dataoffset

                var keys6 = from KeyValuePair<uint, string> pair in _frameFields
                            where (
                                (pair.Value.ToLower().Contains(".ipv4.tcp.dataoffset.dataoffset"))
                                )
                            select pair.Key;

                if (keys6.Count() != 0)
                    dataOffsetID = keys6.ElementAt(0);
                else
                    dataOffsetID = 0;
                #endregion


                string tempstring = string.Empty;
                //uint errno = 0;
                

                NM_NPL_PROPERTY_INFO propinfo = new NM_NPL_PROPERTY_INFO();
                propinfo.Size = (ushort)System.Runtime.InteropServices.Marshal.SizeOf(propinfo);
                errno = NetmonAPI.NmGetPropertyInfo(hFrameParser, TCPPayloadLengthID, ref propinfo);
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
                        errno = NetmonAPI.NmGetPropertyById(hFrameParser, TCPPayloadLengthID, propinfo.ValueSize, pstr, out retlen, out vtype, 0, null);
                    }
                }

                if (errno != 0)
                {
                    Console.WriteLine("Error NmGetPropertyById Frame" + errno.ToString());

                }

                uint paylen = (uint)val[0] + ((uint)val[1] << 8) + ((uint)val[2] << 16) + ((uint)val[3] << 24);

                // Get the Data Offset, used to determine the TCP header size
                byte TCPHeaderSize;
           
                errno = NetmonAPI.NmGetFieldValueNumber8Bit(hParsedFrame, dataOffsetID, out TCPHeaderSize);
                if (errno != 0)
                {
                    Console.WriteLine("Error NmGetFieldValueNumber8Bit Frame #error : " + errno.ToString());
                }

                // Get the Offset of TCP.SrcPort which is the first field in TCP.
                uint TCPSrcOffset;
                uint TCPSrcSize;
                errno = NetmonAPI.NmGetFieldOffsetAndSize(hParsedFrame, srcPortFrameID, out TCPSrcOffset, out TCPSrcSize);
                if (errno != 0)
                {
                    Console.WriteLine("Error NmGetFieldValueNumber8Bit Frame # error : " + errno.ToString());
                }

                tcpsrcoffset = TCPSrcOffset;
                tcpheadersize = TCPHeaderSize;
                payLen = paylen;

        }
        #endregion

        private static void ParserCallback(IntPtr pCallerContext, uint ulStatusCode, string lpDescription, NmCallbackMsgType ulType)
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
        /// Prints out a field's value if the display string couldn't be found.
        /// </summary>
        /// <param name="hParsedFrame">Parsed Frame</param>
        /// <param name="fieldId">Field Number to Display</param>        
        public static string PrintParsedFrameFieldValue(IntPtr hParsedFrame, uint fieldId)
        {

            StringBuilder sb = new StringBuilder();
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
                    sb.Append((new string(name).Replace("\0", string.Empty)).Replace("AsciiStringTerm", ""));
                    
                    //Console.Write("(" + new string(name).Replace("\0", string.Empty) + ") ");
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
                                sb.Append(number8Bit.ToString());
                                //Console.WriteLine(number8Bit);
                            }
                            else
                            {
                                sb.Append("ERROR:" + errno.ToString());
                                //Console.WriteLine("Error " + errno);
                            }

                            break;
                        case FieldType.VT_I1:
                            errno = NetmonAPI.NmGetFieldValueNumber8Bit(hParsedFrame, fieldId, out number8Bit);
                            if (errno == ERROR_SUCCESS)
                            {
                                sb.Append(((sbyte)number8Bit).ToString());
                                //Console.WriteLine((sbyte)number8Bit);
                            }
                            else
                            {
                                sb.Append("ERROR:" + errno.ToString());
                                //Console.WriteLine("Error " + errno);
                            }

                            break;
                        case FieldType.VT_UI2:
                            errno = NetmonAPI.NmGetFieldValueNumber16Bit(hParsedFrame, fieldId, out number16Bit);
                            if (errno == ERROR_SUCCESS)
                            {
                                sb.Append(number16Bit.ToString());
                                //Console.WriteLine(number16Bit);
                            }
                            else
                            {
                                sb.Append("ERROR:" + errno.ToString());
                                //Console.WriteLine("Error " + errno);
                            }

                            break;
                        case FieldType.VT_I2:
                            errno = NetmonAPI.NmGetFieldValueNumber16Bit(hParsedFrame, fieldId, out number16Bit);
                            if (errno == ERROR_SUCCESS)
                            {
                                sb.Append(((short)number16Bit).ToString());
                                //Console.WriteLine((short)number16Bit);
                            }
                            else
                            {
                                sb.Append("ERROR:" + errno.ToString());
                                //Console.WriteLine("Error " + errno);
                            }

                            break;
                        case FieldType.VT_UI4:
                            errno = NetmonAPI.NmGetFieldValueNumber32Bit(hParsedFrame, fieldId, out number32Bit);
                            if (errno == ERROR_SUCCESS)
                            {
                                sb.Append(number32Bit.ToString());
                                //Console.WriteLine(number32Bit);
                            }
                            else
                            {
                                sb.Append("ERROR:" + errno.ToString());
                                //Console.WriteLine("Error " + errno);
                            }

                            break;
                        case FieldType.VT_I4:
                            errno = NetmonAPI.NmGetFieldValueNumber32Bit(hParsedFrame, fieldId, out number32Bit);
                            if (errno == ERROR_SUCCESS)
                            {
                                sb.Append(((int)number32Bit).ToString());
                                //Console.WriteLine((int)number32Bit);
                            }
                            else
                            {
                                sb.Append("ERROR:" + errno.ToString());
                                //Console.WriteLine("Error " + errno);
                            }

                            break;
                        case FieldType.VT_UI8:
                            errno = NetmonAPI.NmGetFieldValueNumber64Bit(hParsedFrame, fieldId, out number64Bit);
                            if (errno == ERROR_SUCCESS)
                            {
                                sb.Append(number64Bit.ToString());
                                //Console.WriteLine(number64Bit);
                            }
                            else
                            {
                                sb.Append("ERROR:" + errno.ToString());
                                //Console.WriteLine("Error " + errno);
                            }

                            break;
                        case FieldType.VT_I8:
                            errno = NetmonAPI.NmGetFieldValueNumber64Bit(hParsedFrame, fieldId, out number64Bit);
                            if (errno == ERROR_SUCCESS)
                            {
                                sb.Append(((long)number64Bit));
                                //Console.WriteLine((long)number64Bit);
                            }
                            else
                            {
                                sb.Append("ERROR:" + errno.ToString());
                                //Console.WriteLine("Error " + errno);
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
                                    sb.Append(byteArray[i].ToString("X2") + " ");
                                    //Console.Write(byteArray[i].ToString("X2") + " ");
                                }

                                if ((parsedField.FieldBitLength >> 3) > number32Bit)
                                {
                                    sb.Append(" ... " + ((parsedField.FieldBitLength >> 3) - number32Bit) + " more bytes not displayed");
                                    //Console.Write(" ... " + ((parsedField.FieldBitLength >> 3) - number32Bit) + " more bytes not displayed");
                                }

                                //Console.WriteLine();
                            }
                            else if (errno == ERROR_RESOURCE_NOT_AVAILABLE)
                            {
                                sb.Append("");
                                //Console.WriteLine("The field is a container");
                            }

                            break;
                        case FieldType.VT_LPWSTR:
                            char[] name = new char[2048];
                            unsafe
                            {
                                fixed (char* pstr = name)
                                {
                                    errno = NetmonAPI.NmGetFieldValueString(hParsedFrame, fieldId, 2048, pstr);
                                }
                            }

                            if (errno == ERROR_SUCCESS)
                            {
                                sb.Append(new string(name).Replace("\0", string.Empty));
                                //Console.WriteLine(new string(name).Replace("\0", string.Empty));
                            }
                            else
                            {
                                sb.Append("String is too long to display");
                                //Console.WriteLine("String is too long to display");
                            }

                            break;
                        case FieldType.VT_LPSTR:
                            sb.Append("Should not occur");
                            //Console.WriteLine("Should not occur");
                            break;
                        case FieldType.VT_EMPTY:
                            sb.Append("Struct or Array types expect description");
                            //Console.WriteLine("Struct or Array types expect description");
                            break;
                        default:
                            sb.Append("Unknown Type " + parsedField.ValueType);
                            //Console.WriteLine("Unknown Type " + parsedField.ValueType);
                            break;
                    }
                }
                else
                {
                    sb.Append("");
                    //Console.WriteLine("Empty");
                }
            }
            else
            {
                sb.Append("Could Not Retrieve Parsed Field Info " + errno);
            }
            return sb.ToString();
        }        


        private HTTPFrame GetHTTPFrame(IntPtr hParsedFrame, uint frameNumber)
        {
            switch (this.FrameType.ToString())
            {
                case "Request":
                    return new HTTPRequestFrame(this, frameNumber + 1);
                case "Response":
                    return new HTTPResponseFrame(this, frameNumber + 1);
                case "Payload":
                    return new HTTPPayloadFrame(this, frameNumber + 1);
                default:
                    throw new InvalidOperationException("Type of HTTP Frame could not be determined, this is neither a request, nor a response, nor a payload frame");
            }
        }

        /// <summary>
        /// Used while Debugging to view the Raw contents of all the fields
        /// </summary>
        /// <param name="hParsedFrame"></param>
        /// <param name="frameNumber"></param>
        /// <param name="command"></param>
        public void DisplayFrameInformation(IntPtr hParsedFrame, uint frameNumber, CommandLineArguments command)
        {
            char keyOption = 'n';
            if (frameNumber + 1 == 149)
            {

                Console.WriteLine("Print Frame Info? (y/n) ");
                keyOption = 'y';
                //keyOption = Console.ReadKey().KeyChar;
                //Console.WriteLine();
            }

            if (keyOption == 'y' || keyOption == 'Y')
            {
                HTTPFrame thisFrame = GetHTTPFrame(hParsedFrame,frameNumber);
                Console.WriteLine("-------------------------------------------");
               // thisFrame.Display();
                //Console.WriteLine("-------------------------------------------");
                Console.WriteLine();

                #region Parse through all the fields in the frame


                uint ulFieldCount;
                uint errno = NetmonAPI.NmGetFieldCount(_hParsedFrame, out ulFieldCount);
                
                for (uint fid = 0; fid < ulFieldCount; fid++)
                {
                    #region Get Field Name and add it to the Dictionary along with its index(i.e.. fid)
                    string FieldName = GetFieldName(hParsedFrame, fid);
                    if (!FieldName.Equals(string.Empty))
                    {
                        Console.WriteLine(FieldName.ToLower() + ":-(" + fid.ToString() + ")-:" + GetFieldValueByIndex(hParsedFrame, fid));
                        Console.WriteLine(FieldName.ToLower() + ":-(" + fid.ToString() + ")-:" + PrintParsedFrameFieldValue(hParsedFrame, fid));
                        
                    }
                    #endregion
                }
                Console.ReadLine();
                //for (uint fid = 0; fid < ulFieldCount; fid++)
                //{
                // 
                //        //Console.WriteLine("IPv6" + FieldName + ":" + FieldValue);
                //    }
                //    else if (errno == ERROR_NOT_FOUND)
                //    {
                //        //Console.ForegroundColor = ConsoleColor.Red;
                //        //Console.WriteLine("{0}:",FieldName);
                //        //Console.ResetColor();
                //        ////PrintParsedFrameFieldValue(hParsedFrame, fid);
                //    }
                //    else
                //    {
                //        //Console.ForegroundColor = ConsoleColor.Red;
                //        //Console.WriteLine("Error: "+ errno);
                //        ////Console.WriteLine("Error Retrieving Value, NmGetFieldName Returned: " + errno);
                //        //Console.ResetColor();
                //        continue;
                //    }
                //}
                #endregion

               
            }
        }

        public HTTPFrame GetFrame(IntPtr hParsedFrame, uint frameNumber)
        {            
          return GetHTTPFrame(hParsedFrame,frameNumber);
        }

        public string GetFieldName(IntPtr hParsedFrame, uint fid)
        {
            char[] name = new char[BUFFER_SIZE * 2];
            uint errno;
            unsafe
            {
                fixed (char* pstr = name)
                {
                    errno = NetmonAPI.NmGetFieldName(hParsedFrame, fid, NmParsedFieldNames.NamePath, BUFFER_SIZE * 2, pstr);
                }
            }

            if (errno == ERROR_SUCCESS)
            {
                return new string(name).Replace("\0", string.Empty);
            }
            else
            {
                if (errno == ERROR_NOT_FOUND)
                    return PrintParsedFrameFieldValue(hParsedFrame, fid);
                else
                    return string.Empty;
            }
        }

        /// <summary>
        /// Returns the FieldName that matches the string pased.
        /// If there are multiple matches, will always return only the first match
        /// </summary>
        /// <param name="approximateMatch"></param>
        /// <returns></returns>
        public string GetFieldName(string approximateMatch)
        {
            var keys = from KeyValuePair<uint, string> pair in FrameFields where pair.Value.Contains(approximateMatch.ToLower()) select pair.Key;
            if (keys.Count() != 0)
                return FrameFields[keys.ElementAt(0)];
            else
                return string.Empty;
        }

        public string GetFieldValueByIndex(IntPtr hParsedFrame, uint index)
        {
            uint errno;
            char[] name = new char[BUFFER_SIZE];
            unsafe
            {
                fixed (char* pstr = name)
                {
                    errno = NetmonAPI.NmGetFieldName(hParsedFrame, index, NmParsedFieldNames.FieldDisplayString, BUFFER_SIZE, pstr);
                }
            }
            if (errno == ERROR_SUCCESS)
            {
                return new string(name).Replace("\0", string.Empty);
            }
            else
            {
                if (errno == ERROR_NOT_FOUND)
                    return PrintParsedFrameFieldValue(hParsedFrame,index);                    
                else
                    return string.Empty;
            }

        }

        public string GetFieldValueByFieldName(IntPtr hParsedFrame, string FieldName)
        {
            var Keys = from KeyValuePair<uint, string> pair in FrameFields where FieldName.Equals(pair.Value) select pair.Key;
            if (Keys.Count() == 0)
                return string.Empty;
            else
            {
                if (Keys.Count() == 1)
                {
                    #region GetFieldValue at a specific index within the frame
                    return GetFieldValueByIndex(hParsedFrame, Keys.ElementAt(0));
                    #endregion
                }
                else
                {
                    #region GetFieldValue at all index'es within the frame and then return a consolidated string object
                        StringBuilder sb = new StringBuilder();
                        foreach (uint i in Keys)
                        {
                            sb.Append(GetFieldValueByIndex(hParsedFrame, i));
                        }
                        return sb.ToString();
                    #endregion
                }
            }
        }

        private void PlaceHolder()
        {
            ;
        }
    }
}
