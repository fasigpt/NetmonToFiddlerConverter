using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.NetworkMonitor;
using System.IO;
using System.Xml;
using System.Diagnostics;
using System.IO.Compression;


namespace ExpertExample
{

    public class cookie
    {
        public string name;
        public string value;
        public string path=null;
        public string httpOnly="false";
        public string _secure="false";
        public string domain=null;
        public string expires=null;
        string[] sep = { ";" };

        public cookie(string cookies)
        {
            string[] List = cookies.Split(sep, StringSplitOptions.None);
           
            for(int i=0;i<List.Count();i++)
            {
                if (List[i].ToLower().IndexOf("domain") > 0)
                {
                    domain = List[i].Substring(7);
                }
                else if (List[i].ToLower().IndexOf("expires") > 0)
                {
                    expires = List[i].Substring(8);
                }
                else if (List[i].ToLower().IndexOf("path") > 0)
                {
                    path = List[i].Substring(5);
                }
                else if (List[i].ToLower().IndexOf("httponly") > 0)
                {
                    httpOnly = "true";
                }
                else if (List[i].ToLower().IndexOf("secure") > 0)
                {
                    _secure = "true";
                }
                else
                {
                    int index = List[i].ToLower().IndexOf("=");
                    value = List[i].Substring(index + 1);
                    name = List[i].Substring(0, index);
                }

            }

        }
   
    }

    class FiddlerOutput
    {


        public static void GenerateOutput(Dictionary<uint, HTTPRequestFrame> Requests, Dictionary<uint, HTTPResponseFrame> Responses, Dictionary<uint, HTTPPayloadFrame> Payloads, string outputFile, string captureFileName)
        {

            #region Constants

           
            String outputFileName = Environment.CurrentDirectory + "\\Fiddler" + "\\ImportFiddler" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".xml";
            int totalFrames = Requests.Count;              
            string[] separators = { ",", ";" };           
            byte[] requestRawStream = null;
            byte[] responseRawStream = null;         
            int index = 0;            
            string myFile = null;

            #endregion

            if (Requests.Count != 0)
            {
            
                foreach (KeyValuePair<uint, HTTPRequestFrame> item in Requests)
                {

                    #region Locals
                    index++;
                    requestRawStream = null;
                    responseRawStream = null;
                    string SIP = Requests[item.Key].SourceIP;
                    string DIP = Requests[item.Key].DestinationIP;
                    uint SPort = Requests[item.Key].SourcePort;
                    uint DPort = Requests[item.Key].DestinationPort;
                    uint AckNum = Requests[item.Key].AckNumber;
                    uint SeqEnd = Requests[item.Key].SequenceEnd; 
                    #endregion

                     //Get HTTPREQUEST payload for this REQUEST (for POST and others)
                    var keyRequestPayload = (from KeyValuePair<uint, HTTPPayloadFrame> response in Payloads
                                             where (
                                             (response.Value.SourceIP == SIP) &&
                                             (response.Value.SourcePort == SPort) &&
                                             (response.Value.DestinationIP == DIP) &&
                                             (response.Value.DestinationPort == DPort) &&
                                             (response.Value.Type == HTTPFrameType.RequestPayload) &&
                                             (response.Value.AckNumber == AckNum)
                                             )
                                             select response);


                   requestRawStream = ReadFileIOStream(item.Key, "Request");                  

                    if (keyRequestPayload.Count() != 0)
                    {

                        foreach (KeyValuePair<uint, HTTPPayloadFrame> key in keyRequestPayload)
                        {
                            byte[] reqtempstring1 = ReadFileIOStream(key.Key, "Payload");
                            if (reqtempstring1 != null)                                
                                requestRawStream = Combinebytearray(requestRawStream, reqtempstring1);

                        }

                    }


                    //Get HTTPRESPONSE for this Request
                    var keyResponses = (from KeyValuePair<uint, HTTPResponseFrame> response in Responses
                                        where (
                                        (response.Value.SourceIP == DIP) &&
                                        (response.Value.SourcePort == DPort) &&
                                        (response.Value.DestinationIP == SIP) &&
                                        (response.Value.DestinationPort == SPort) &&
                                        (response.Value.SequenceStart == AckNum)
                                        )
                                        select response);

                  

                    //get HTTPRESPONSE payload fot this request
                    var keyResponsePayload = (from KeyValuePair<uint, HTTPPayloadFrame> response in Payloads
                                              where (
                                              (response.Value.SourceIP == DIP) &&
                                              (response.Value.SourcePort == DPort) &&
                                              (response.Value.DestinationIP == SIP) &&
                                              (response.Value.DestinationPort == SPort) &&
                                              (response.Value.Type == HTTPFrameType.ResponsePayload) &&
                                              (response.Value.AckNumber == keyResponses.First().Value.AckNumber)
                                              )
                                              select response);

                    
                    if (keyResponses.Count() != 0)
                    {

                        

                       responseRawStream = ReadFileIOStream(keyResponses.First().Key, "Response");                                   

                        if (keyResponsePayload.Count() != 0)
                        {

                            foreach (KeyValuePair<uint, HTTPPayloadFrame> key in keyResponsePayload)
                            {
                                //logic to get the RAW http data
                                byte[] restempstring1 = ReadFileIOStream(key.Key, "Payload");
                                if (restempstring1 != null)                                    
                                    responseRawStream = Combinebytearray(responseRawStream, restempstring1);

                            }

                        }
                    }


                    if (requestRawStream != null)
                    {
                        myFile = Environment.CurrentDirectory + "\\Fiddler" + "\\raw\\raw\\" + index + "_c.txt";
                        FileStream stream = new FileStream(myFile, FileMode.Append, FileAccess.Write);
                        stream.Write(requestRawStream, 0, requestRawStream.Length);
                        stream.Close();
                    }

                    if (responseRawStream != null)
                    {
                        myFile = Environment.CurrentDirectory + "\\Fiddler" + "\\raw\\raw\\" + index + "_s.txt";
                        FileStream stream2 = new FileStream(myFile, FileMode.Append, FileAccess.Write);
                        stream2.Write(responseRawStream, 0, responseRawStream.Length);
                        stream2.Close();
                    }
                }


                //ZipFile zip = new ZipFile();
                //zip.AddDirectory(Environment.CurrentDirectory + "\\Fiddler" + "\\raw");
                //zip.Save(Environment.CurrentDirectory + "\\Fiddler" + "\\raw.zip");
                //File.Move(Environment.CurrentDirectory + "\\Fiddler" + "\\raw.zip", Environment.CurrentDirectory + "\\raw.saz");

                ZipFile.CreateFromDirectory(Environment.CurrentDirectory + "\\Fiddler" + "\\raw", Environment.CurrentDirectory + "\\Fiddler" + "\\raw.zip");
                File.Move(Environment.CurrentDirectory + "\\Fiddler" + "\\raw.zip", Environment.CurrentDirectory + "\\raw.saz");
                

            }
        }

        public static string GetFramePayload(IntPtr RawFrame, uint TCPSrcOffset, uint TCPHeaderSize, uint paylen)
        {
            #region comm
            //string tempstring = string.Empty;
            //uint errno = 0;
            //byte[] result = null;

            //NM_NPL_PROPERTY_INFO propinfo = new NM_NPL_PROPERTY_INFO();
            //propinfo.Size = (ushort)System.Runtime.InteropServices.Marshal.SizeOf(propinfo);
            //errno = NetmonAPI.NmGetPropertyInfo(mFrameParser, mTCPPayLoadLengthID, ref propinfo);
            //if (errno != 0)
            //{
            //    Console.WriteLine("Error NmGetPropertyInfo Frame # error : " + errno.ToString());

            //}

            //byte[] val = new byte[propinfo.ValueSize];
            //uint retlen;
            //NmPropertyValueType vtype;
            //unsafe
            //{
            //    fixed (byte* pstr = val)
            //    {
            //        errno = NetmonAPI.NmGetPropertyById(mFrameParser, mTCPPayLoadLengthID, propinfo.ValueSize, pstr, out retlen, out vtype, 0, null);
            //    }
            //}

            //if (errno != 0)
            //{
            //    Console.WriteLine("Error NmGetPropertyById Frame" + errno.ToString());

            //}

            //uint paylen = (uint)val[0] + ((uint)val[1] << 8) + ((uint)val[2] << 16) + ((uint)val[3] << 24);

            //// Get the Data Offset, used to determine the TCP header size
            //byte TCPHeaderSize;
            //errno = NetmonAPI.NmGetFieldValueNumber8Bit(hParsedFrame, mTCPDataOffsetID, out TCPHeaderSize);
            //if (errno != 0)
            //{
            //    Console.WriteLine("Error NmGetFieldValueNumber8Bit Frame #error : " + errno.ToString());
            //}

            //// Get the Offset of TCP.SrcPort which is the first field in TCP.
            //uint TCPSrcOffset;
            //uint TCPSrcSize;
            //errno = NetmonAPI.NmGetFieldOffsetAndSize(hParsedFrame, mTCPSrcPortID, out TCPSrcOffset, out TCPSrcSize);
            //if (errno != 0)
            //{
            //    Console.WriteLine("Error NmGetFieldValueNumber8Bit Frame # error : " + errno.ToString());
            //} 
            #endregion

            uint retlen;
            uint errno = 0;
            byte[] result = null;
            string tempstring = string.Empty;
            if (paylen > 0)
            {
                result = new byte[paylen];
                unsafe
                {
                    fixed (byte* pstr = result)
                    {
                        errno = NetmonAPI.NmGetPartialRawFrame(RawFrame, (uint)(TCPSrcOffset / 8 + TCPHeaderSize * 4), paylen, pstr, out retlen);
                        // errno = NetmonAPI.NmGetPartialRawFrame(RawFrame, (uint)(TCPSrcOffset / 8 ), paylen, pstr, out retlen);
                    }
                }

                if (errno != 0)
                {
                    Console.WriteLine("Error NmGetFieldValueNumber8Bit Frame #error : " + errno.ToString());
                    result = null;
                }
                else
                {
                     tempstring = Encoding.UTF8.GetString(result, 0, result.Length);
                }
            }
            else
                retlen = 0;

            return tempstring;

        }


        public static StringBuilder GetRequestResponseRawStream(IntPtr hParsedFrame,uint payLodId,int frameCount)
        {
            
            byte[] rawHTTPByte;
            char[] rawHTTPChar;
            UInt32 pulFieldOffset;
            UInt32 pulFieldBitLength;
            UInt32 ulReturnLength;
            uint errno = 0;
            string tempstring = null;
            StringBuilder rawHTTPStringRequest = new StringBuilder();
            StringBuilder rawHTTPStringResponse = new StringBuilder();

            if (payLodId != 0)
            {
                errno = NetmonAPI.NmGetFieldOffsetAndSize(hParsedFrame, payLodId, out pulFieldOffset, out pulFieldBitLength);
                if (errno == 0)
                {
                    rawHTTPByte = new byte[pulFieldBitLength];
                    unsafe
                    {
                        fixed (byte* pstr = rawHTTPByte)
                        {
                            errno = NetmonAPI.NmGetFieldValueByteArray(hParsedFrame, payLodId, pulFieldBitLength, pstr, out ulReturnLength);
                            tempstring = Encoding.UTF8.GetString(rawHTTPByte, 0, rawHTTPByte.Length);
                            rawHTTPStringRequest.Append(tempstring);
                        }
                    }
                }
            }
            else
            {
                for (uint j = 0; j < frameCount; j++)
                {
                    errno = NetmonAPI.NmGetFieldOffsetAndSize(hParsedFrame, j, out pulFieldOffset, out pulFieldBitLength);
                    rawHTTPChar = new char[pulFieldBitLength];

                    unsafe
                    {

                        fixed (char* pstr = rawHTTPChar)
                        {
                            errno = NetmonAPI.NmGetFieldValueString(hParsedFrame, j, pulFieldBitLength, pstr);


                            if (errno == 0)
                            {

                                rawHTTPStringRequest.Append(rawHTTPChar);
                            }
                        }
                    }
                }
            }

            return rawHTTPStringRequest;
        }

        public static byte[] Combinebytearray(byte[] first, byte[] second)
        {
                                
            
            byte[] ret = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, ret, 0, first.Length);
            Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);
            return ret;
        }


        public static byte[] GetBytesFromFile(string fullFilePath)
        {
            // this method is limited to 2^32 byte files (4.2 GB)

            FileStream fs = null;
            try
            {
                fs = File.OpenRead(fullFilePath);
                byte[] bytes = new byte[fs.Length];
                fs.Read(bytes, 0, Convert.ToInt32(fs.Length));
                return bytes;
            }
            finally
            {
                if (fs != null)
                {
                    fs.Close();
                    fs.Dispose();
                }
            }

        }


        public static byte[] ReadFileIOStream(uint Frame,string type)
        {
                   
            byte[] rawStream = null;

            try
            {
                string myFile = Environment.CurrentDirectory + "\\Fiddler" + "\\" + type + "\\" +  Frame.ToString() + ".txt";
                
                if (File.Exists(myFile))
                {
                    FileStream stream = new FileStream(myFile, FileMode.Open, FileAccess.Read);             
                    rawStream = GetBytesFromFile(myFile);
                    
                }

             
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error Reading Fiddler Files   " + ex.ToString());
            }

            return rawStream;
           
        }

        //public static string GetFramePayload(IntPtr hParsedFrame, IntPtr mFrameParser, IntPtr RawFrame, uint mTCPPayLoadLengthID, uint mTCPDataOffsetID, uint mTCPSrcPortID)
        //{
        //    string tempstring = string.Empty;
        //    uint errno = 0;
        //    byte[] result = null;

        //    NM_NPL_PROPERTY_INFO propinfo = new NM_NPL_PROPERTY_INFO();
        //    propinfo.Size = (ushort)System.Runtime.InteropServices.Marshal.SizeOf(propinfo);
        //    errno = NetmonAPI.NmGetPropertyInfo(mFrameParser, mTCPPayLoadLengthID, ref propinfo);
        //    if (errno != 0)
        //    {
        //        Console.WriteLine("Error NmGetPropertyInfo Frame # error : " + errno.ToString());

        //    }

        //    byte[] val = new byte[propinfo.ValueSize];
        //    uint retlen;
        //    NmPropertyValueType vtype;
        //    unsafe
        //    {
        //        fixed (byte* pstr = val)
        //        {
        //            errno = NetmonAPI.NmGetPropertyById(mFrameParser, mTCPPayLoadLengthID, propinfo.ValueSize, pstr, out retlen, out vtype, 0, null);
        //        }
        //    }

        //    if (errno != 0)
        //    {
        //        Console.WriteLine("Error NmGetPropertyById Frame" + errno.ToString());

        //    }

        //    uint paylen = (uint)val[0] + ((uint)val[1] << 8) + ((uint)val[2] << 16) + ((uint)val[3] << 24);

        //    // Get the Data Offset, used to determine the TCP header size
        //    byte TCPHeaderSize;
        //    errno = NetmonAPI.NmGetFieldValueNumber8Bit(hParsedFrame, mTCPDataOffsetID, out TCPHeaderSize);
        //    if (errno != 0)
        //    {
        //        Console.WriteLine("Error NmGetFieldValueNumber8Bit Frame #error : " + errno.ToString());
        //    }

        //    // Get the Offset of TCP.SrcPort which is the first field in TCP.
        //    uint TCPSrcOffset;
        //    uint TCPSrcSize;
        //    errno = NetmonAPI.NmGetFieldOffsetAndSize(hParsedFrame, mTCPSrcPortID, out TCPSrcOffset, out TCPSrcSize);
        //    if (errno != 0)
        //    {
        //        Console.WriteLine("Error NmGetFieldValueNumber8Bit Frame # error : " + errno.ToString());
        //    }

        //    if (paylen > 0)
        //    {
        //        result = new byte[paylen];
        //        unsafe
        //        {
        //            fixed (byte* pstr = result)
        //            {
        //                errno = NetmonAPI.NmGetPartialRawFrame(RawFrame, (uint)(TCPSrcOffset / 8 + TCPHeaderSize * 4), paylen, pstr, out retlen);
        //                // errno = NetmonAPI.NmGetPartialRawFrame(RawFrame, (uint)(TCPSrcOffset / 8 ), paylen, pstr, out retlen);
        //            }
        //        }

        //        if (errno != 0)
        //        {
        //            Console.WriteLine("Error NmGetFieldValueNumber8Bit Frame #error : " + errno.ToString());
        //            result = null;
        //        }
        //        else
        //        {
        //            tempstring = Encoding.UTF8.GetString(result, 0, result.Length);
        //        }
        //    }
        //    else
        //        retlen = 0;

        //    return tempstring;

        //}
    }
}