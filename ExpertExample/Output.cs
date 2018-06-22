using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
namespace ExpertExample
{
    public class Output
    {
        public enum OutputType { HTML, CSV};
        public static void GenerateOutput(Dictionary<uint, HTTPRequestFrame> Requests, Dictionary<uint, HTTPResponseFrame> Responses, Dictionary<uint, HTTPPayloadFrame> Payloads, string outputFile, string captureFileName, OutputType type)
        {
            switch (type)
            { 
                case OutputType.HTML:
                    GenerateHTMLOutput(Requests, Responses, Payloads, outputFile, captureFileName);
                    break;
                case OutputType.CSV:
                    GenerateCSVOutput(Requests, Responses, Payloads, outputFile, captureFileName);
                    break;
                default:
                    break;
            }
        }

        //Need to fix this to match the new logic introduced in the generation of HTML output
        //No one requires CSV output so not investing time in this for now, will look into this if the need arises
        private static void GenerateCSVOutput(Dictionary<uint, HTTPRequestFrame> Requests, Dictionary<uint, HTTPResponseFrame> Responses, Dictionary<uint, HTTPPayloadFrame> Payloads, string outputFile, string captureFileName)
        {

            /*Print the number of requests and response objects found*/
            Console.WriteLine("Requests : {0}", Requests.Count);
            Console.WriteLine("Responses: {0}", Responses.Count);
            Console.WriteLine("Payloads: {0}", Payloads.Count);
            //Calculate Time taken by a request

            StringBuilder rawRequestHeaders = new StringBuilder();
            StringBuilder rawResponseHeaders = new StringBuilder();

            System.IO.StreamWriter sw = new System.IO.StreamWriter(outputFile);

            #region Prepare the Text File
            sw.Write(@"<HTML><HEAD>");
            sw.Write(@"<TITLE>Netmon HTTPAnalyzer REPORT</TITLE>");
            sw.Write(@"</HEAD>");
            sw.Write(@"<BODY>");            
            sw.Write("HTTP parsed output for the file \"" + captureFileName + "\"<BR/>");
            sw.Write("HTTP parsed output written to the file \"" + outputFile + "\"<BR/>");
            sw.Write(@"<li/>Total Number of HTTP Requests found : " + Requests.Count);
            sw.Write(@"<li/>Total Number of HTTP Responses found : " + Responses.Count);
            sw.Write(@"<li/>Total Number of HTTP Payload packets found : " + Payloads.Count + "<BR/><BR/>");

            sw.Write(@"<TABLE BORDER='1'><THEAD><TR>");
            sw.Write(@"<TH align=center><a title='HTTP Request Frame # in the netmon trace'>Request Frame #</a></TH>");
            sw.Write(@"<TH align=center><a title='Time in the netmon trace when the HTTP request was made'>Time</a></TH>");
            sw.Write(@"<TH align=center><a title='IP and port FROM which the HTTP request was made'>Source IP<BR>(Port)</a></TH>");
            sw.Write(@"<TH align=center>Method</TH>");
            sw.Write(@"<TH align=center><a title='Size of Request headers + ContentLength header (if present) '>Request length</a></TH>");
            sw.Write(@"<TH align=center width = '30%'>Requested URL</TH>");
            sw.Write(@"<TH align=center><a title='IP and port TO which the HTTP request was made'>Destination IP<BR>(Port)</a></TH>");
            sw.Write(@"<TH align=center><a title='HTTP Request Frame # in the netmon trace'>Response Frame #</a></TH>");
            sw.Write(@"<TH align=center>Status</TH>");
            sw.Write(@"<TH align=center><a title='Value of the ContentLength header'>Response length</a></TH>");
            sw.Write(@"<TH align=center><a title='Time between the HTTP Request frame and Last Response Payload frame'>Time-Taken</a></TH>");
            sw.Write(@"<TH align=center>Content-Type</TH>");
            sw.Write(@"<TH align=center><a title='Copy netmon filter for this conversation'>Filter</a></TH>");
            sw.Write(@"<TH align=center>Request Headers</TH>");
            sw.Write(@"<TH align=center>Response Headers</TH>");
            sw.Write(@"</TR></THEAD><tbody>");
            #endregion

            int divId = 1;
            foreach (KeyValuePair<uint, HTTPRequestFrame> request in Requests)
            {

                #region Response Time calculation considering the Response Payload's
                {
                    #region Get the Next Request on the same conversation
                    HTTPRequestFrame nextRequest = Requests.FirstOrDefault(x =>
                                                        (
                                                            (x.Value.Number > request.Value.Number) &&
                                                            (x.Value.SourceIP == request.Value.SourceIP) &&
                                                            (x.Value.SourcePort == request.Value.SourcePort) &&
                                                            (x.Value.DestinationIP == request.Value.DestinationIP) &&
                                                            (x.Value.DestinationPort == request.Value.DestinationPort)
                                                        )
                                                        ).Value;
                    #endregion

                    #region Get the Last Payload packet for the response (there might be more requests on the same conversation's)
                    HTTPPayloadFrame lastPayload = null;
                    if (nextRequest != null)
                    {
                        lastPayload = Payloads.LastOrDefault(x =>
                                                        (
                                                         (x.Value.Number < nextRequest.Number) &&
                                                         (x.Value.Number > request.Value.Number) &&
                                                         (x.Value.SourceIP == nextRequest.DestinationIP) &&
                                                         (x.Value.SourcePort == nextRequest.DestinationPort) &&
                                                         (x.Value.DestinationIP == nextRequest.SourceIP) &&
                                                         (x.Value.DestinationPort == nextRequest.SourcePort) &&
                                                         (x.Value.URL.Contains(request.Value.URL.Substring(0, (request.Value.URL.Length > 100) ? 100 : request.Value.URL.Length)))
                                                        )
                                                    ).Value;

                    }
                    else
                    {
                        lastPayload = Payloads.LastOrDefault(x =>
                                                        (
                                                         (x.Value.Number > request.Value.Number) &&
                                                         (x.Value.SourceIP == request.Value.DestinationIP) &&
                                                         (x.Value.SourcePort == request.Value.DestinationPort) &&
                                                         (x.Value.DestinationIP == request.Value.SourceIP) &&
                                                         (x.Value.DestinationPort == request.Value.SourcePort) &&
                                                         (x.Value.URL.Contains(request.Value.URL.Substring(0, (request.Value.URL.Length > 100) ? 100 : request.Value.URL.Length)))
                                                        )
                                                    ).Value; ;
                    }
                    #endregion

                    #region Find the Response Packet

                    HTTPResponseFrame response;
                    if (nextRequest != null)
                        response = Responses.FirstOrDefault(x =>
                                            (
                                                (x.Value.SourceIP == request.Value.DestinationIP) &&
                                                (x.Value.DestinationIP == request.Value.SourceIP) &&
                                                (x.Value.SourcePort == request.Value.DestinationPort) &&
                                                (x.Value.DestinationPort == request.Value.SourcePort) &&
                                                (x.Value.Number > request.Value.Number) &&
                                                (x.Value.Number < nextRequest.Number) &&
                                                (x.Value.URL.Contains(request.Value.URL.Substring(0, (request.Value.URL.Length > 100) ? 100 : request.Value.URL.Length)))
                                            )
                        ).Value;
                    else
                        response = Responses.FirstOrDefault(x =>
                                            (
                                                (x.Value.SourceIP == request.Value.DestinationIP) &&
                                                (x.Value.DestinationIP == request.Value.SourceIP) &&
                                                (x.Value.SourcePort == request.Value.DestinationPort) &&
                                                (x.Value.DestinationPort == request.Value.SourcePort) &&
                                                (x.Value.Number > request.Value.Number) &&
                                                (x.Value.URL.Contains(request.Value.URL.Substring(0, (request.Value.URL.Length > 100) ? 100 : request.Value.URL.Length)))
                                            )
                        ).Value;
                    #endregion


                    #region Prepare Request Headers

                    rawRequestHeaders.AppendLine(request.Value.Method + " " + request.Value.URL + ((request.Value.QueryString.Length > 0) ? request.Value.QueryString : string.Empty) + "<BR>");
                    foreach (KeyValuePair<int, HTTPHeader> pair in request.Value.Headers)
                    {
                        rawRequestHeaders.AppendLine(pair.Value.HeaderName + ":" + pair.Value.HeaderValue + "<BR>");
                    }

                    #endregion

                    #region Calculate the Response Time and Print the output to the file

                    sw.WriteLine(@"<TR>");
                    sw.WriteLine(@"<TD align=center>" + request.Value.Number + "</TD>");
                    sw.WriteLine(@"<TD align=center>" + request.Value.TimeStamp + "</TD>");
                    sw.WriteLine(@"<TD align=center>" + request.Value.SourceIP + " (" + request.Value.SourcePort.ToString() + ")</TD>");
                    sw.WriteLine("<TD align=center><a id='a_" + (divId - 1) + "' href='#'>" + request.Value.Method + "</a></TD>");

                    #region Print Content-Length from Request headers
                    {
                        int headerLength = Convert.ToInt32(rawRequestHeaders.ToString().Replace("<BR>", "").Length);
                        int contentLenght = request.Value.GetHeaderIndex("contentlength");
                        if (contentLenght > 0)
                        {
                            int contentLengthValue = Convert.ToInt32(request.Value.Headers[contentLenght].HeaderValue);
                            sw.WriteLine(@"<TD align=center><span title='" + headerLength.ToString() + " + " + contentLengthValue.ToString() + "'>" + (headerLength + contentLengthValue).ToString() + "</span></TD>");
                        }
                        else
                        {
                            sw.WriteLine(@"<TD align=center><span title='" + headerLength.ToString() + " + 0'>" + rawRequestHeaders.ToString().Replace("<BR>", "").Length.ToString() + "<span></TD>");
                        }
                    }
                    #endregion

                    #region Print URL of the request

                    if (request.Value.URL.Contains("http:"))
                        sw.WriteLine(@"<TD align=left width='30%'>" + request.Value.URL + ((request.Value.QueryString.Length > 0) ? request.Value.QueryString : string.Empty) + "</TD>");
                    else
                        if (request.Value.GetHeaderValue("host") == "")
                            sw.WriteLine(@"<TD align=left width='30%'>" + request.Value.URL + ((request.Value.QueryString.Length > 0) ? request.Value.QueryString : string.Empty) + "</TD>");
                        else
                            sw.WriteLine(@"<TD align=left width='30%'>http://" + request.Value.GetHeaderValue("host") + request.Value.URL + ((request.Value.QueryString.Length > 0) ? request.Value.QueryString : string.Empty) + "</TD>");

                    #endregion

                    if (response != null)
                    {

                        sw.WriteLine(@"<TD align=center>" + response.SourceIP + " (" + response.SourcePort.ToString() + ")</TD>");
                        sw.WriteLine(@"<TD align=center>" + response.Number + "</TD>");
                        sw.WriteLine(@"<TD align=center><a href='#' id='a_" + divId + "' >" + response.StatusCode + "</a></TD>");
                        #region Print Content-Length from Response headers
                        {
                            int contentLenght = response.GetHeaderIndex("contentlength");
                            if (contentLenght > 0)
                            {
                                sw.WriteLine(@"<TD align=center>" + response.Headers[contentLenght].HeaderValue + "</TD>");
                            }
                            else
                                sw.WriteLine(@"<TD align=center> -- </TD>");
                        }
                        #endregion

                        #region Print time-taken for a request

                        if (lastPayload == null)
                        {
                            sw.WriteLine(@"<TD align=center>" + (response.TimeStamp - request.Value.TimeStamp) + "</TD>");
                        }
                        else
                        {
                            sw.WriteLine(@"<TD align=center>" + (lastPayload.TimeStamp - request.Value.TimeStamp) + "</TD>");
                        }

                        #endregion

                        #region Print Content-Type from Response headers
                        {
                            int contentType = response.GetHeaderIndex("contenttype");
                            if (contentType > 0)
                                sw.WriteLine(@"<TD align=center>" + response.Headers[contentType].HeaderValue + "</TD>");
                            else
                                sw.WriteLine(@"<TD align=center> -- </TD>");
                        }
                        #endregion

                        #region Prepare Response Headers

                        foreach (KeyValuePair<int, HTTPHeader> pair in response.Headers)
                        {
                            rawResponseHeaders.AppendLine(pair.Value.HeaderName + ":" + pair.Value.HeaderValue + "<BR>");
                        }
                        #endregion
                    }
                    else
                    {
                        // No Response found for this request in this trace
                        sw.WriteLine(@"<TD align=center>" + request.Value.DestinationIP + " (" + request.Value.DestinationPort.ToString() + ")</TD>");
                        sw.WriteLine(@"<TD align=center> -- </TD>");
                        sw.WriteLine(@"<TD align=center>NO_RESPONSE</TD>");
                        sw.WriteLine(@"<TD align=center> -- </TD>");
                        sw.WriteLine(@"<TD align=center> -- </TD>");
                        sw.WriteLine(@"<TD align=center> -- </TD>");

                    }
                    #region Create netmon Conversation Filter
                    {
                        string filter = "";
                        if (request.Value.SourceIP.Contains(":"))
                        {
                            filter += "ipv6.Address==" + request.Value.SourceIP;
                        }
                        else
                        {
                            filter += "ipv4.Address==" + request.Value.SourceIP;
                        }
                        if (request.Value.DestinationIP.Contains(":"))
                        {
                            filter += " and ipv6.Address==" + request.Value.DestinationIP;
                        }
                        else
                        {
                            filter += " and ipv4.Address==" + request.Value.DestinationIP;
                        }
                        filter += " and tcp.port == " + request.Value.SourcePort.ToString() + " and tcp.port == " + request.Value.DestinationPort.ToString();
                        sw.WriteLine("<TD>");
                        sw.WriteLine("<input type='button' value='CopyMe' onclick =\"var str = '" + filter + "';if (window.clipboardData){if(clipboardData.setData)clipboardData.setData('text', str);}else{window.prompt('Copy to clipboard: Ctrl+C, Enter', str);}\" />");
                        sw.WriteLine("</TD>");
                    }

                    #endregion

                    sw.WriteLine(@"</TR>");

                    {

                        #region Print Request Headers

                        sw.WriteLine(@"<DIV ID='div_" + (divId - 1) + "' style='DISPLAY: none'>"); divId++;
                        sw.WriteLine(@"<HR>");
                        sw.WriteLine(rawRequestHeaders.ToString());
                        sw.WriteLine(@"<HR>");
                        sw.WriteLine(@"</DIV>");

                        sw.WriteLine("<script>$(document).ready(function(){$(\"#div_" + (divId - 2) + "\").dialog({autoOpen:false,title:'Request Header',width:'80%'});$(\"#a_" + (divId - 2) + "\").click(function(){$(\"#div_" + (divId - 2) + "\").dialog('open');return false;});})</script>");
                        sw.WriteLine("<script>$</script>");

                        rawRequestHeaders.Clear();
                        sw.Flush();
                        #endregion
                    }

                    if (response != null)
                    {
                        #region Print Response Headers

                        sw.WriteLine(@"<DIV ID='div_" + (divId - 1) + "' style='DISPLAY: none'>"); divId++;
                        sw.WriteLine(@"<HR>");
                        sw.WriteLine(rawResponseHeaders.ToString());
                        sw.WriteLine(@"<HR>");
                        sw.WriteLine(@"</DIV>");

                        sw.WriteLine("<script>$(document).ready(function(){$(\"#div_" + (divId - 2) + "\").dialog({autoOpen:false,title:'Response Header',width:'80%'});$(\"#a_" + (divId - 2) + "\").click(function(){$(\"#div_" + (divId - 2) + "\").dialog('open');return false;});})</script>");

                        rawResponseHeaders.Clear();
                        #endregion
                        sw.Flush();
                    }
                    #endregion
                    sw.Flush();
                }
                #endregion
            }
            sw.WriteLine(@"</tbody></TABLE>");
            sw.WriteLine(@"</BODY>");
            sw.WriteLine(@"</HTML>");

            sw.Close();

        }

        private static void GenerateHTMLOutput(Dictionary<uint,HTTPRequestFrame> Requests, Dictionary<uint,HTTPResponseFrame> Responses, Dictionary<uint,HTTPPayloadFrame> Payloads, string outputFile, string captureFileName)
        {
            /*Print the number of requests and response objects found*/
            Console.WriteLine("Requests : {0}", Requests.Count);
            Console.WriteLine("Responses: {0}", Responses.Count);
            Console.WriteLine("Payloads: {0}", Payloads.Count);
            //Calculate Time taken by a request

            StringBuilder rawRequestHeaders = new StringBuilder();
            StringBuilder rawResponseHeaders = new StringBuilder();

            System.IO.StreamWriter sw = new System.IO.StreamWriter(outputFile);

            #region Prepare the Text File
            sw.Write(@"<HTML><HEAD><meta http-equiv='X-UA-Compatible' content='IE=EmulateIE7'/> ");
            sw.Write(@"<TITLE>Netmon HTTPAnalyzer REPORT</TITLE>");
            sw.Write(@"<script src='http://code.jquery.com/jquery-latest.js'></script>");            

            sw.Write(@"<script>$(document).ready(function(){$('#sortedtable').tablesorter({sortList:[[0,0]], widgets: ['zebra']});});</script>");

            sw.Write(@"<link href='http://ajax.googleapis.com/ajax/libs/jqueryui/1.8/themes/base/jquery-ui.css' rel='stylesheet' type='text/css'/>");
            sw.Write(@"<script src='http://ajax.googleapis.com/ajax/libs/jquery/1.5/jquery.min.js'></script>");
            sw.Write(@"<script src='http://ajax.googleapis.com/ajax/libs/jqueryui/1.8/jquery-ui.min.js'></script>");
            


            //sw.WriteLine(@"<SCRIPT language='JavaScript'>function ShowHide(index){$(index).dialog('open');return false;}</SCRIPT>");
            sw.Write(@"<STYLE>TABLE{font-family: Verdana;font-size:10px;border-collapse:collapse;}BODY{font-family: Verdana;font-size:10px;}A:HOVER {font-weight:bold;text-decoration:underline}A.SSL {color:red}");
            //Style for sorted table
            //sw.Write(@"TH.headerSortUp { background-image: url(../img/small_asc.gif); background-color: #3399FF; } TH.headerSortDown { background-image: url(../img/small_desc.gif); background-color: #3399FF; } TH.header { background-image: url(../img/small.gif);cursor: pointer; font-weight: bold; background-repeat: no-repeat; background-position: center left; padding-left: 20px; border-right: 1px solid black; margin-left: -1px;}");
            sw.Write(@"TH{background:#5B9BD5;border:solid #5B9BD5 1.0pt;}TD {overflow:hidden;border:solid #9CC2E5 1.0pt}.alternateRow{background:#DEEAF6;}");
            sw.Write(@"</STYLE></HEAD>");
            sw.Write(@"<BODY>");
            sw.Write(@"<script  src='http://dev.jquery.com/view/trunk/plugins/tablesorter/2.0/jquery.tablesorter.js'></script>");
            sw.Write("HTTP parsed output for the file \"" + captureFileName + "\"<BR/>");
            sw.Write("HTTP parsed output written to the file \"" + outputFile + "\"<BR/>");
            sw.Write(@"<li/>Total Number of HTTP Requests found : " + Requests.Count );
            sw.Write(@"<li/>Total Number of HTTP Responses found : " + Responses.Count);
            sw.Write(@"<li/>Total Number of HTTP Payload packets found : " + Payloads.Count + "<BR/><BR/>");
            sw.Write("Hover your mouse over the title to see more information about each field<BR><BR/>");
            
            sw.Write(@"<TABLE BORDER='1' id='sortedtable' class='tablesorter' WIDTH='100%'><THEAD><TR>");
            sw.Write(@"<TH align=center><a title='Frame # in the netmon trace where the Request packet can be found'>Request Frame #</a></TH>");
            sw.Write(@"<TH align=center><a title='Time in the netmon trace when the HTTP request was made'>Time</a></TH>");
            sw.Write(@"<TH align=center><a title='IP and port FROM which the HTTP request was made'>Source IP:Port</a></TH>");
            sw.Write(@"<TH align=center>Method</TH>");
            sw.Write(@"<TH align=center><a title='Size of Request headers + ContentLength header (if present) '>Request length</a></TH>");
            sw.Write(@"<TH align=center width = '400px'>Requested URL</TH>");
            sw.Write(@"<TH align=center width = '300px'>Query String</TH>");
            sw.Write(@"<TH align=center><a title='IP and port TO which the HTTP request was made'>Destination IP:Port</a></TH>");
            sw.Write(@"<TH align=center><a title='Frame # in the netmon trace where the first Response packet can be found'>Response Frame #</a></TH>");
            sw.Write(@"<TH align=center>Status</TH>");
            sw.Write(@"<TH align=center><a title='Value of the ContentLength header'>Response length</a></TH>");
            sw.Write(@"<TH align=center><a title='Time between the HTTP Request frame and Last Response Payload frame'>Time-Taken</a></TH>");
            //sw.Write(@"<TH align=center>Content-Type</TH>");
            sw.Write(@"<TH align=center><a title='Copy netmon filter for this conversation'>Filter</a></TH>");
            sw.Write(@"</TR></THEAD><tbody>");
            #endregion

            //int divId = 1;
            int rowId = 0;
            string styleForTR;
            foreach (KeyValuePair<uint, HTTPRequestFrame> request in Requests)
            {
                rowId++;
                if (rowId % 2 == 0)
                    styleForTR = " class='alternateRow' ";
                else
                    styleForTR = "";

                #region Response Time calculation considering the Response Payload's
                {
                    #region Get the Next Request on the same conversation
                    HTTPRequestFrame nextRequest = Requests.FirstOrDefault(x =>
                                                        (
                                                            (x.Value.Number > request.Value.Number) &&
                                                            (x.Value.SourceIP == request.Value.SourceIP) &&
                                                            (x.Value.SourcePort == request.Value.SourcePort) &&
                                                            (x.Value.DestinationIP == request.Value.DestinationIP) &&
                                                            (x.Value.DestinationPort == request.Value.DestinationPort)
                                                        )
                                                        ).Value;
                    #endregion

                    #region Get the Last Payload packet for the response (there might be more requests on the same conversation's)
                    HTTPPayloadFrame lastPayload = null;
                    if (nextRequest != null)
                    {
                        lastPayload = Payloads.LastOrDefault(x =>
                                                       (
                                                        (x.Value.Number < nextRequest.Number) &&
                                                        (x.Value.Number > request.Value.Number) &&
                                                        (x.Value.SourceIP == nextRequest.DestinationIP) &&
                                                        (x.Value.SourcePort == nextRequest.DestinationPort) &&
                                                        (x.Value.DestinationIP == nextRequest.SourceIP) &&
                                                        (x.Value.DestinationPort == nextRequest.SourcePort) &&
                                                        (x.Value.URL == request.Value.URL)
                                                       )
                                                   ).Value;

                    }
                    else
                    {
                        lastPayload = Payloads.LastOrDefault(x =>
                                                        (
                                                         (x.Value.Number > request.Value.Number) &&
                                                         (x.Value.SourceIP == request.Value.DestinationIP) &&
                                                         (x.Value.SourcePort == request.Value.DestinationPort) &&
                                                         (x.Value.DestinationIP == request.Value.SourceIP) &&
                                                         (x.Value.DestinationPort == request.Value.SourcePort) &&
                                                         (x.Value.URL == request.Value.URL)
                                                        )
                                                    ).Value;
                    }
                    #endregion

                    #region Find the Response Packet

                    HTTPResponseFrame response;
                    if (nextRequest != null)
                        response = Responses.FirstOrDefault(x =>
                                            (
                                                (x.Value.SourceIP == request.Value.DestinationIP) &&
                                                (x.Value.DestinationIP == request.Value.SourceIP) &&
                                                (x.Value.SourcePort == request.Value.DestinationPort) &&
                                                (x.Value.DestinationPort == request.Value.SourcePort) &&
                                                (x.Value.Number > request.Value.Number) &&
                                                (x.Value.Number < nextRequest.Number) &&
                                                (x.Value.URL.Contains(request.Value.URL.Substring(0, (request.Value.URL.Length > 100) ? 100 : request.Value.URL.Length)))
                                            )
                        ).Value;
                    else
                        response = Responses.FirstOrDefault(x =>
                                            (
                                                (x.Value.SourceIP == request.Value.DestinationIP) &&
                                                (x.Value.DestinationIP == request.Value.SourceIP) &&
                                                (x.Value.SourcePort == request.Value.DestinationPort) &&
                                                (x.Value.DestinationPort == request.Value.SourcePort) &&
                                                (x.Value.Number > request.Value.Number) &&
                                                (x.Value.URL.Contains(request.Value.URL.Substring(0, (request.Value.URL.Length > 100) ? 100 : request.Value.URL.Length)))
                                            )
                        ).Value;
                    #endregion


                    #region Prepare Request Headers

                    rawRequestHeaders.AppendLine(request.Value.Method + " " + request.Value.URL + ((request.Value.QueryString.Length > 0) ? request.Value.QueryString : string.Empty) + "<BR>");
                    foreach (KeyValuePair<int, HTTPHeader> pair in request.Value.Headers)
                    {
                        rawRequestHeaders.AppendLine(pair.Value.HeaderName + ":" + pair.Value.HeaderValue + "<BR>");
                    }

                    #endregion

                    #region Calculate the Response Time and Print the output to the file

                    sw.WriteLine(@"<TR " + styleForTR + ">");
                    sw.WriteLine(@"<TD align=center>" + request.Value.Number + "</TD>");
                    sw.WriteLine(@"<TD align=center>" + request.Value.TimeStamp + "</TD>");
                    sw.WriteLine(@"<TD align=center>" + request.Value.SourceIP + ":" + request.Value.SourcePort.ToString() + "</TD>");
                    //sw.WriteLine("<TD align=center><a id='a_" + (divId - 1) + "' href='#'>" + request.Value.Method + "</a></TD>");
                    sw.WriteLine("<TD align=center>" + request.Value.Method + "</TD>");

                    #region Print Content-Length from Request headers
                    {
                        int headerLength = Convert.ToInt32(rawRequestHeaders.ToString().Replace("<BR>", "").Length);
                        int contentLenght = request.Value.GetHeaderIndex("contentlength");
                        if (contentLenght > 0)
                        {                            
                            int contentLengthValue = Convert.ToInt32(request.Value.Headers[contentLenght].HeaderValue);
                            sw.WriteLine(@"<TD align=center><span title='" + headerLength.ToString() + " + " + contentLengthValue.ToString() + "'>" + (headerLength + contentLengthValue ).ToString() + "</span></TD>");
                        }
                        else
                        {                            
                            sw.WriteLine(@"<TD align=center><span title='" + headerLength.ToString() + " + 0'>" + rawRequestHeaders.ToString().Replace("<BR>", "").Length.ToString() + "<span></TD>");
                        }
                    }
                    #endregion

                    #region Print URL of the request and the QueryString

                    if (request.Value.URL.Contains("http:"))
                    {
                        //sw.WriteLine(@"<TD align=left width='30%'>" + request.Value.URL + ((request.Value.QueryString.Length > 0) ? request.Value.QueryString : string.Empty) + "</TD>");
                        sw.WriteLine(@"<TD align=left><div style='overflow:hidden;width:400px;word-wrap:break-word;'>" + request.Value.URL + "</div></TD>");
                        sw.WriteLine(@"<TD align=left><div style='overflow:hidden;width:300px;word-wrap:break-word;'>" + ((request.Value.QueryString.Length > 0) ? request.Value.QueryString : "--") + "</div></TD>");
                    }
                    else
                    {
                        if (request.Value.GetHeaderValue("host") == "")
                        {
                            //sw.WriteLine(@"<TD align=left width='30%'>" + request.Value.URL + ((request.Value.QueryString.Length > 0) ? request.Value.QueryString : string.Empty) + "</TD>");
                            sw.WriteLine(@"<TD align=left><div style='overflow:hidden;width:400px;word-wrap:break-word;'>" + request.Value.URL + "</div></TD>");
                            sw.WriteLine(@"<TD align=left><div style='overflow:hidden;width:300px;word-wrap:break-word;'>" + ((request.Value.QueryString.Length > 0) ? request.Value.QueryString : string.Empty) + "</div></TD>");
                        }
                        else
                        {
                            //sw.WriteLine(@"<TD align=left width='30%'>http://" + request.Value.GetHeaderValue("host") + request.Value.URL + ((request.Value.QueryString.Length > 0) ? request.Value.QueryString : string.Empty) + "</TD>");
                            sw.WriteLine(@"<TD align=left><div style='overflow:hidden;width:400px;word-wrap:break-word;'>http://" + request.Value.GetHeaderValue("host") + request.Value.URL + "</div></TD>");
                            sw.WriteLine(@"<TD align=left><div style='overflow:hidden;width:300px;word-wrap:break-word;'>" + ((request.Value.QueryString.Length > 0) ? request.Value.QueryString : string.Empty) + "</div></TD>");
                        }
                    }
                    #endregion

                    if (response != null)
                    {

                        sw.WriteLine(@"<TD align=center>" + response.SourceIP + ":" + response.SourcePort.ToString() + "</TD>");
                        sw.WriteLine(@"<TD align=center>" + response.Number + "</TD>");
                        //sw.WriteLine(@"<TD align=center><a href='#' id='a_" + divId + "' >" + response.StatusCode + "</a></TD>");
                        sw.WriteLine(@"<TD align=center>" + response.StatusCode + "</TD>");

                        #region Print Content-Length from Response headers
                        {
                            #region To-Do Enchancement
                            //Here I am simply displaying the Content-Length header. There are times when the Content-Length header is not present
                            //In such a case, I should add the size of Response and every Response Payload packet and then show the value here
                            //This will require me to add a new field to the HTTPResponseFrame and HTTPPayloadFrame classes and populate them
                            #endregion

                            int contentLenght = response.GetHeaderIndex("contentlength");
                            if (contentLenght > 0)
                            {
                                sw.WriteLine(@"<TD align=center>" + response.Headers[contentLenght].HeaderValue + "</TD>");
                            }
                            else
                                sw.WriteLine(@"<TD align=center> <a alt='Content-Length header not found'>--</a></TD>");
                        }
                        #endregion

                        #region Print time-taken for a request

                        if (lastPayload == null)
                        {
                            
                            sw.WriteLine(@"<TD align=center>" + (response.TimeStamp - request.Value.TimeStamp) + "</TD>");
                        }
                        else
                        {
                            sw.WriteLine(@"<TD align=center>" + (lastPayload.TimeStamp - request.Value.TimeStamp) + "</TD>");
                        }

                        #endregion

                        #region Print Content-Type from Response headers - NOT PRINTING FOR NOW AS IT IS MESSING THE REPORT LAYOUT
                        {/*
                            int contentType = response.GetHeaderIndex("contenttype");
                            if (contentType > 0)
                                sw.WriteLine(@"<TD align=center>" + response.Headers[contentType].HeaderValue + "</TD>");
                            else
                                sw.WriteLine(@"<TD align=center> -- </TD>");
                          */
                        }
                        #endregion

                        #region Prepare Response Headers

                        foreach (KeyValuePair<int, HTTPHeader> pair in response.Headers)
                        {
                            rawResponseHeaders.AppendLine(pair.Value.HeaderName + ":" + pair.Value.HeaderValue + "<BR>");
                        }
                        #endregion
                    }
                    else
                    {
                        // No Response found for this request in this trace
                        sw.WriteLine(@"<TD align=center>" + request.Value.DestinationIP + " (" + request.Value.DestinationPort.ToString() + ")</TD>");
                        sw.WriteLine(@"<TD align=center> -- </TD>");
                        sw.WriteLine(@"<TD align=center>RESPONSE_NOT_FOUND</TD>");
                        sw.WriteLine(@"<TD align=center> -- </TD>");                        
                        sw.WriteLine(@"<TD align=center> -- </TD>");
                        
                    }
                    #region Create netmon Conversation Filter
                    {
                        string filter = "";
                        if (request.Value.SourceIP.Contains(":"))
                        {
                            filter += "ipv6.Address==" + request.Value.SourceIP;
                        }
                        else
                        {
                            filter += "ipv4.Address==" + request.Value.SourceIP;
                        }
                        if (request.Value.DestinationIP.Contains(":"))
                        {
                            filter += " and ipv6.Address==" + request.Value.DestinationIP;
                        }
                        else
                        {
                            filter += " and ipv4.Address==" + request.Value.DestinationIP;
                        }
                        filter += " and tcp.port == " + request.Value.SourcePort.ToString() + " and tcp.port == " + request.Value.DestinationPort.ToString();
                        sw.WriteLine("<TD>");
                        //sw.WriteLine("<input type='button' value='CopyMe' onclick =\"var str = '" + filter + "';if (window.clipboardData){if(clipboardData.setData)clipboardData.setData('text', str);}else{window.prompt('Copy to clipboard: Ctrl+C, Enter', str);}\" />");
                        sw.WriteLine(filter);
                        sw.WriteLine("</TD>");
                    }
                   
                    #endregion

                    sw.WriteLine(@"</TR>");

                    {

                        #region Print Request Headers
                        /*
                         * This is increasing the size of the report like crazy to an extent that IE is unable to even show the report for huge traces!!! So commenting ths part
                        sw.WriteLine(@"<DIV ID='div_" + (divId - 1) + "' style='DISPLAY: none'>"); divId++;
                        sw.WriteLine(@"<HR>");
                        sw.WriteLine(rawRequestHeaders.ToString());
                        sw.WriteLine(@"<HR>");
                        sw.WriteLine(@"</DIV>");

                        sw.WriteLine("<script>$(document).ready(function(){$(\"#div_" + (divId - 2) + "\").dialog({autoOpen:false,title:'Request Header',width:'80%'});$(\"#a_" + (divId - 2) + "\").click(function(){$(\"#div_" + (divId - 2) + "\").dialog('open');return false;});})</script>");
                        sw.WriteLine("<script>$</script>");

                        rawRequestHeaders.Clear();
                        sw.Flush();
                        */ 
                        #endregion
                        
                    }

                    if (response != null)
                    {
                        #region Print Response Headers
                        /*
                         * This is increasing the size of the report like crazy to an extent that IE is unable to even show the report for huge traces!!! So commenting ths part
                        sw.WriteLine(@"<DIV ID='div_" + (divId - 1) + "' style='DISPLAY: none'>"); divId++;
                        sw.WriteLine(@"<HR>");
                        sw.WriteLine(rawResponseHeaders.ToString());
                        sw.WriteLine(@"<HR>");                        
                        sw.WriteLine(@"</DIV>");

                        sw.WriteLine("<script>$(document).ready(function(){$(\"#div_" + (divId - 2) + "\").dialog({autoOpen:false,title:'Response Header',width:'80%'});$(\"#a_" + (divId - 2) + "\").click(function(){$(\"#div_" + (divId - 2) + "\").dialog('open');return false;});})</script>");

                        rawResponseHeaders.Clear();
                        */
                        #endregion
                        sw.Flush();
                    }
                    #endregion
                    sw.Flush();
                }
                #endregion
            }
            sw.WriteLine(@"</tbody></TABLE>");
            sw.WriteLine(@"</BODY>");
            sw.WriteLine(@"</HTML>");

            sw.Close();
        }
    }
}
