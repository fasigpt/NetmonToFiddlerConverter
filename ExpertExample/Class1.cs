using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.NetworkMonitor;
using System.IO;
using System.Xml;
using System.Diagnostics;

namespace ExpertExample
{

    public class cookie
    {
        public string name;
        public string value;
        public string path = null;
        public string httpOnly = "false";
        public string _secure = "false";
        public string domain = null;
        public string expires = null;
        string[] sep = { ";" };
        public cookie(string cookies)
        {
            string[] List = cookies.Split(sep, StringSplitOptions.None);

            for (int i = 0; i < List.Count(); i++)
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
        //public enum RequestHeaders { method, url, httpVersion, cookies, headers, queryString, headerSize, bodySize }
        //public enum ResponseHeaders { status, statusText, httpVersion, cookies, headers, content, redirectionURL, headerSize, bodySize }

        //public Dictionary<string, string> requestCookies = new Dictionary<string, string>();
        //public Dictionary<string, string> requestHeaders = new Dictionary<string, string>();
        //public Dictionary<string, string> responseCookies = new Dictionary<string, string>();
        //public Dictionary<string, string> responseHeaders = new Dictionary<string, string>();
        //public Dictionary<string, string> queryStringList = new Dictionary<string, string>();

        public static void GenerateOutput(Dictionary<uint, HTTPRequestFrame> Requests, Dictionary<uint, HTTPResponseFrame> Responses, Dictionary<uint, HTTPPayloadFrame> Payloads, string outputFile, string captureFileName)
        {

            #region Constants

            string size = null;//this contains the contentlength
            string mimeType = null;
            String outputFileName = Environment.CurrentDirectory + "\\Fiddler" + "\\ImportFiddler" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".xml";
            int totalFrames = Requests.Count;
            string method = null;
            string url = null;
            string reqhttpVersion = null;
            string reshttpVersion = null;
            string queryString = null;
            Dictionary<string, string> requestCookies = new Dictionary<string, string>();
            Dictionary<string, string> requestHeaders = new Dictionary<string, string>();
            Dictionary<int, string> responseCookies = new Dictionary<int, string>();
            Dictionary<string, string> responseHeaders = new Dictionary<string, string>();
            Dictionary<string, string> queryStringList = new Dictionary<string, string>();
            Dictionary<string, string> requesttempHeaders = new Dictionary<string, string>();
            Dictionary<string, string> responsetempHeaders = new Dictionary<string, string>();
            string status = null;
            string statusText = null;
            string[] statusCodeText = null;
            string[] separators = { ",", ";" };
            Dictionary<int, HTTPHeader> tempHeaderList = null;
            StringBuilder requestRawStream = null;
            StringBuilder responseRawStream = null;

            List<cookie> responsecookies2 = null;

            // cookie[] responsecookies2= new cookie


            #endregion

            if (Requests.Count != 0)
            {
                using (XmlWriter writer = XmlWriter.Create(outputFileName))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("log");
                    writer.WriteStartElement("entries");
                    foreach (KeyValuePair<uint, HTTPRequestFrame> item in Requests)
                    {

                        if (item.Key == 188)
                            Debugger.Break();

                        requestRawStream = new StringBuilder();
                        responseRawStream = new StringBuilder();
                        string SIP = Requests[item.Key].SourceIP;
                        string DIP = Requests[item.Key].DestinationIP;
                        uint SPort = Requests[item.Key].SourcePort;
                        uint DPort = Requests[item.Key].DestinationPort;
                        uint AckNum = Requests[item.Key].AckNumber;
                        uint SeqEnd = Requests[item.Key].SequenceEnd;

                        method = Requests[item.Key].Method;
                        url = Requests[item.Key].URL;
                        queryString = Requests[item.Key].QueryString;
                        reqhttpVersion = Requests[item.Key].protocolVersion;


                        tempHeaderList = Requests[item.Key].Headers;


                        requestCookies = item.Value.cookieDict;
                        queryStringList = item.Value.queryStrDict;


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


                        string reqtempstring = ReadFileIOStream(item.Key, "Request", out requesttempHeaders);
                        if (reqtempstring != null)
                            requestRawStream.Append(reqtempstring);

                        requestHeaders = requesttempHeaders;


                        if (keyRequestPayload.Count() != 0)
                        {

                            foreach (KeyValuePair<uint, HTTPPayloadFrame> key in keyRequestPayload)
                            {
                                string reqtempstring1 = ReadFileIOStream(key.Key, "Payload", out requesttempHeaders);
                                if (reqtempstring1 != null)
                                    requestRawStream.Append(reqtempstring1);

                            }

                        }



                        //Get HTTPRESPONSE for this Request
                        var keyResponses = (from KeyValuePair<uint, HTTPResponseFrame> response in Responses
                                            where (
                                            (response.Value.SourceIP == DIP) &&
                                            (response.Value.SourcePort == DPort) &&
                                            (response.Value.DestinationIP == SIP) &&
                                            (response.Value.DestinationPort == SPort) &&
                                            (response.Value.AckNumber == SeqEnd)
                                            )
                                            select response);

                        tempHeaderList = null;
                        if (keyResponses.Count() > 0)
                        {
                            tempHeaderList = keyResponses.First().Value.Headers;
                            reshttpVersion = keyResponses.First().Value.protocolVersion;
                            status = keyResponses.First().Value.StatusCode;
                            statusCodeText = status.Split(separators, StringSplitOptions.None);
                            if (statusCodeText[0] != null)
                                status = statusCodeText[0];
                            if (statusCodeText[1] != null)
                                statusText = statusCodeText[1];

                            foreach (KeyValuePair<int, HTTPHeader> item1 in tempHeaderList)
                            {
                                if (item1.Value.HeaderName.ToLower() == "contentlength")
                                {
                                    size = item1.Value.HeaderValue;
                                }
                                else if (item1.Value.HeaderName.ToLower() == "contentype")
                                {
                                    mimeType = item1.Value.HeaderValue;
                                }


                            }

                            responseCookies = keyResponses.First().Value.cookieDict;
                            responsecookies2 = new List<cookie>();
                            foreach (KeyValuePair<int, string> cookies in responseCookies)
                            {
                                responsecookies2.Add(new cookie(cookies.Value.ToString()));
                            }

                        }


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

                            //responseRawStream= GetRequestResponseRawStream(keyResponses.First().Value.hParsedFrame, keyResponses.First().Value.payloadID, keyResponses.First().Value.frameFieldCount);

                            string restempstring = ReadFileIOStream(keyResponses.First().Key, "Response", out responsetempHeaders);
                            if (restempstring != null)
                                responseRawStream.Append(restempstring);
                            responseHeaders = responsetempHeaders;

                            if (keyResponsePayload.Count() != 0)
                            {

                                foreach (KeyValuePair<uint, HTTPPayloadFrame> key in keyResponsePayload)
                                {
                                    //logic to get the RAW http data

                                    string restempstring1 = ReadFileIOStream(key.Key, "Payload", out responsetempHeaders);
                                    if (restempstring1 != null)
                                        responseRawStream.Append(restempstring1);

                                }

                            }
                        }



                        //Write XML outputHere

                        writer.WriteStartElement("entry");

                        #region Request Part for each Entry

                        writer.WriteStartElement("request");
                        writer.WriteElementString("method", method);
                        writer.WriteElementString("url", url);
                        writer.WriteElementString("httpVersion", reqhttpVersion);

                        writer.WriteStartElement("cookies");
                        foreach (KeyValuePair<String, String> cookie in requestCookies)
                        {
                            writer.WriteStartElement("cookie");
                            writer.WriteElementString("name", cookie.Key);
                            writer.WriteElementString("value", cookie.Value);
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();

                        writer.WriteStartElement("headers");
                        foreach (KeyValuePair<String, String> cookie in requestHeaders)
                        {
                            writer.WriteStartElement("header");
                            writer.WriteElementString("name", cookie.Key);
                            writer.WriteElementString("value", cookie.Value);
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();


                        writer.WriteElementString("queryString", queryString);
                        writer.WriteElementString("headersSize", size);
                        writer.WriteElementString("bodySize", requestRawStream.Length.ToString());

                        writer.WriteEndElement();
                        #endregion


                        #region Response part for each entry
                        writer.WriteStartElement("response");
                        writer.WriteElementString("status", status);
                        writer.WriteElementString("statusText", statusText);
                        writer.WriteElementString("httpVersion", "");

                        writer.WriteStartElement("cookies");
                        foreach (cookie cookie in responsecookies2)
                        {
                            writer.WriteStartElement("cookie");

                            writer.WriteElementString("name", cookie.name);
                            writer.WriteElementString("value", cookie.value);

                            if (cookie.path != null)
                                writer.WriteElementString("path", cookie.path);

                            if (cookie.domain != null)
                                writer.WriteElementString("domain", cookie.domain);

                            if (cookie.expires != null)
                                writer.WriteElementString("expires", cookie.expires);

                            writer.WriteElementString("httpOnly", cookie.httpOnly);
                            writer.WriteElementString("_secure", cookie._secure);

                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();

                        writer.WriteStartElement("headers");
                        foreach (KeyValuePair<String, String> cookie in responseHeaders)
                        {
                            writer.WriteStartElement("header");
                            writer.WriteElementString("name", cookie.Key);
                            writer.WriteElementString("value", cookie.Value);
                            writer.WriteEndElement();
                        }
                        writer.WriteEndElement();

                        writer.WriteStartElement("content");
                        writer.WriteElementString("size", responseRawStream.Length.ToString());
                        writer.WriteElementString("mimeType", mimeType);
                        //writer.WriteElementString("text", responseRawStream.ToString());
                        writer.WriteElementString("text", "test");
                        writer.WriteEndElement();

                        writer.WriteEndElement();

                        #endregion

                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
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


        public static StringBuilder GetRequestResponseRawStream(IntPtr hParsedFrame, uint payLodId, int frameCount)
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


        public static string ReadFileIOStream(uint Frame, string type, out Dictionary<string, string> headerList)
        {
            string tempstring = null;
            string headerString = null;

            headerList = new Dictionary<string, string>();

            string[] sep = { "\r\n" };
            string[] sep1 = { ":" };
            //string[] List= cookies.Split(sep, StringSplitOptions.None);

            try
            {
                string myFile = Environment.CurrentDirectory + "\\Fiddler" + "\\" + type + "\\" + Frame.ToString() + ".txt";
                byte[] rawStream = null;
                if (File.Exists(myFile))
                {
                    rawStream = File.ReadAllBytes(myFile);
                    tempstring = Encoding.UTF8.GetString(rawStream, 0, rawStream.Length);
                }

                if (type != "Payload")
                {  // headerString = headerList.Last().Value.HeaderName + ":" + headerList.Last().Value.HeaderValue;
                    headerString = "\r\n\r\n";

                    String[] List = tempstring.Split(sep, StringSplitOptions.None);

                    foreach (String header in List)
                    {
                        if (header.Contains(":"))
                        {
                            string[] sepList = header.Split(sep1, StringSplitOptions.None);

                            if (sepList.Length == 2)
                            {
                                if (sepList[0].ToLower() == "cookie")
                                    continue;
                                headerList.Add(sepList[0], sepList[1]);
                            }
                            else if (sepList.Length == 1)
                            {
                                if (sepList[0].ToLower() == "cookie")
                                    continue;
                                headerList.Add(sepList[0], String.Empty);
                            }
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error Reading Fiddler Files   " + ex.ToString());
            }

            if (type == "Payload")
            {
                return tempstring;
            }
            else
            {
                if (tempstring == null)
                {
                    return tempstring;
                }
                else
                    return (tempstring.Substring(tempstring.IndexOf(headerString) + headerString.Length));
            }
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