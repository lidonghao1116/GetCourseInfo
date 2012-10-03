using System;
using System.Text;
using System.Net;
using System.IO;

namespace GetCourseInfoV2
{
    public class HTTPHelper
    {
        public CookieContainer _cookieContainer;
        readonly int _timeout;
        const string _userAgentStr = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; )";
        readonly IWebProxy _proxy = null;

        public HTTPHelper(int timeout)
        {
            _cookieContainer = new CookieContainer();
            _timeout = timeout;
            ServicePointManager.ServerCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback(CheckValidationResult);
        }

        public bool CheckValidationResult(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certificate, System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors errors)
        {
            return true;
        }

        static byte[] HTTPGetResponse(HttpWebRequest request)
        {
            if (request == null) 
                throw new ArgumentNullException("request");
            
            var response = (HttpWebResponse)request.GetResponse();
            var stream = response.GetResponseStream();

            if (stream == null)
                return new byte[0];

            var data = new byte[response.ContentLength];
            stream.Read(data, 0, (int)response.ContentLength);

            stream.Close();

            return data;
        }

        static string HTTPGetResponseTxt(HttpWebRequest request)
        {
            if (request == null)
                throw new ArgumentNullException("request");

            var response = (HttpWebResponse) request.GetResponse();
            var stream = response.GetResponseStream();

            Encoding encode;
            if (response.CharacterSet.ToLowerInvariant().Contains("utf-8"))
                encode = Encoding.UTF8;
            else
                encode = Encoding.Default;

            if (stream == null)
                return "";

            var reader = new StreamReader(stream, encode);

            var html = reader.ReadToEnd();

            reader.Close();
            stream.Close();

            return html;
        }

        public string HTTPGetTxt(string url)
        {
            HttpWebRequest request;
            var requestUrl = url;
            while (true)
            {
                request = (HttpWebRequest)WebRequest.Create(requestUrl);
                request.Method = "GET";
                request.Headers.Add("Accept-Encoding", "gzip,deflate");
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                request.Headers.Add("Accept-Charset", "GB2312,utf-8");
                request.UserAgent = _userAgentStr;
                request.Timeout = _timeout;
                request.CookieContainer = _cookieContainer;
                request.Proxy = _proxy;

                if (request.RequestUri != request.Address)//转向
                    requestUrl = request.Address.AbsoluteUri;
                else
                    break;
            }
            return HTTPGetResponseTxt(request);
        }

        public byte[] HTTPGet(string url)
        {
            HttpWebRequest request;
            var requestUrl = url;
            while (true)
            {
                request = (HttpWebRequest)WebRequest.Create(requestUrl);
                request.Method = "GET";
                request.Headers.Add("Accept-Encoding", "gzip,deflate");
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                request.UserAgent = _userAgentStr;
                request.Timeout = _timeout;
                request.CookieContainer = _cookieContainer;
                request.Proxy = _proxy;

                if (request.RequestUri != request.Address)//转向
                    requestUrl = request.Address.AbsoluteUri;
                else
                    break;
            }

            return HTTPGetResponse(request);
        }

        public string HTTPPostTxt(string url, string poststring)
        {
            var request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.Method = "POST";
            request.UserAgent = _userAgentStr;
            request.Headers.Add("Accept-Encoding", "gzip,deflate");
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Headers.Add("Accept-Charset", "GB2312,utf-8");
            request.CookieContainer = _cookieContainer;
            request.Timeout = _timeout;
            request.ContentType = "application/x-www-form-urlencoded";
            request.Proxy = _proxy;

            var postdata = Encoding.ASCII.GetBytes(poststring);
            request.ContentLength = postdata.Length;
            var RequestStream = request.GetRequestStream();
            RequestStream.Write(postdata, 0, postdata.Length);
            RequestStream.Close();

            return HTTPGetResponseTxt(request);
        }
    }
}
