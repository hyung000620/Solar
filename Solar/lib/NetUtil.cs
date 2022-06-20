using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using mshtml;

namespace Solar
{
    public class NetUtil
    {
        List<string> usrAgntList;

        public NetUtil()
        {
            usrAgntList = new List<string>
            {
                "Mozilla/5.0 (Windows NT 6.3; Trident/7.0; Touch; rv:11.0) like Gecko",
                "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; rv:11.0) like Gecko",
                "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)",
                "Mozilla/5.0 (Windows NT 6.1; Trident/7.0; rv:11.0) like Gecko",
                "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0)",
                "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 6.2; WOW64; Trident/6.0; .NET4.0E; .NET4.0C; .NET CLR 3.5.30729; .NET CLR 2.0.50727; .NET CLR 3.0.30729; InfoPath.3; SMJB)",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/51.0.2704.79 Safari/537.36 Edge/14.14393",
                "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; Touch; rv:11.0) like Gecko",
                "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; Trident/4.0; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; OfficeLiveConnector.1.3; OfficeLivePatch.0.0; InfoPath.2)",
                "Mozilla/5.0 (Windows NT 6.1; WOW64; Trident/7.0; rv:11.0) like Gecko",
                "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; Trident/4.0; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; InfoPath.3; .NET4.0C; .NET4.0E)",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/59.0.3071.104 Safari/537.36",
                "Mozilla/5.0 (Windows NT 6.3; WOW64; Trident/7.0; rv:11.0) like Gecko",
                "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36",
                "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/59.0.3071.115 Safari/537.36",
                "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; WOW64; Trident/6.0)",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/59.0.3071.115 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; Touch; EzRun1.0.0.4; rv:11.0) like Gecko",
                "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; Trident/6.0)"
            };
        }

        public string RndAgnt()
        {
            Random rnd = new Random();
            int rndIdx = rnd.Next(0, usrAgntList.Count);

            return usrAgntList[rndIdx];
        }

        public string GetHtml(string url, Encoding encode = null, string referer = null)
        {
            string html = string.Empty;

            HttpWebRequest req = null;
            HttpWebResponse res = null;
            try
            {
                req = (HttpWebRequest)WebRequest.Create(url);                
                req.UserAgent = RndAgnt();                
                req.Referer = referer;
                
                //req.Headers.Add("ReferrerPolicy", "strict-origin-when-cross-origin");
                //req.Method = "POST";
                //req.CookieContainer = new CookieContainer();
                //req.ContentType = "application/x-www-form-urlencoded";
                //req.MaximumAutomaticRedirections = 1;
                //req.AllowAutoRedirect = true;
                //req.Host = "www.re-in.co.kr";
                //req.Headers.Add("UpgradeInsecureRequests", "1");
                req.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9";

                req.Timeout = 30000;    //30초
                res = (HttpWebResponse)req.GetResponse();
                if (encode == null) encode = Encoding.Default;
                StreamReader sr = new StreamReader(res.GetResponseStream(), encode);
                html = sr.ReadToEnd();
                sr.Close();
                res.Close();
            }
            catch (Exception ex)
            {
                html = "HttpWebException-" + ex.Message;
            }

            return html;
        }

        public void DnHtml(string url, string fileName, Encoding encode = null, string referer = null)
        {
            string html = string.Empty, dir = string.Empty;

            if (encode == null) encode = Encoding.Default;
            html = GetHtml(url, encode, referer);
            
            FileStream fs = new FileStream(fileName, FileMode.Create);
            StreamWriter sw = new StreamWriter(fs, encode);
            sw.Write(html);
            sw.Close();
            fs.Close();
        }

        /*
        public string DnFile(string url, string fileName, string referer = null)
        {
            HttpWebRequest req = null;
            HttpWebResponse res = null;
            string cntnDpsn = string.Empty;
            string resFileExt = string.Empty;
            string result = string.Empty;

            try
            {
                req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = RndAgnt();
                req.Referer = referer;
                //req.Timeout = 300000; //(5분)
                res = (HttpWebResponse)req.GetResponse();
                cntnDpsn = res.GetResponseHeader("Content-Disposition").Trim();

                if (cntnDpsn != string.Empty && Regex.IsMatch(fileName, @"\.(\w+)$") == false)
                {
                    fileName += Regex.Match(cntnDpsn, @"\.\w+").Value.ToLower();
                    MessageBox.Show(fileName);
                }

                if (res.ContentLength <= 0)
                {
                    result = "Content-Lenght:0";
                    res.Close();
                }
                else
                {
                    FileStream fs = new FileStream(fileName, FileMode.Create);
                    Stream stream = res.GetResponseStream();
                    stream.Flush();
                    int buffsize = 4096;
                    byte[] buff = new byte[buffsize];
                    while ((buffsize = stream.Read(buff, 0, buffsize)) > 0)
                    {
                        fs.Flush();
                        fs.Write(buff, 0, buffsize);
                    }
                    stream.Close();
                    fs.Close();
                    res.Close();
                    stream.Dispose();
                    fs.Dispose();
                    result = "success";
                }
            }
            catch (Exception ex)
            {
                result = ex.Message;
                res.Close();
                return result;
            }

            return result;
        }
        */

        public Dictionary<string, string> DnFile(string url, string fullNm, string referer = null)
        {
            string fileNm;

            HttpWebRequest req = null;
            HttpWebResponse res = null;
            string cntnDpsn = string.Empty;
            string resFileExt = string.Empty;
            string result = string.Empty;
            string argExt = "", resExt = "";

            fileNm = Regex.Match(fullNm, @".*\\(.*)$", RegexOptions.IgnoreCase).Groups[1].Value;

            Dictionary<string, string> retDic = new Dictionary<string, string>();            
            retDic.Add("result", "fail");
            retDic.Add("resultMsg", "");
            retDic.Add("fileNm", fileNm);
            retDic.Add("fullNm", fullNm);

            try
            {
                req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = RndAgnt();
                req.Referer = referer;
                //req.Timeout = 300000; //(5분)
                //req.Method = "GET";
                //req.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9";
                //req.Host = "";
                res = (HttpWebResponse)req.GetResponse();
                cntnDpsn = res.GetResponseHeader("Content-Disposition").Trim();

                argExt = Regex.Match(fullNm, @"\.(\w+)$").Groups[1].Value;

                if (cntnDpsn != string.Empty)
                {
                    if (Regex.IsMatch(cntnDpsn, @"\.(\w+)$"))
                    {
                        resExt = Regex.Match(cntnDpsn, @"\.(\w+)$").Groups[1].Value.ToLower();
                        if (argExt != resExt)
                        {
                            fileNm = fileNm.Replace(argExt, resExt);
                            fullNm = fullNm.Replace(argExt, resExt);
                            retDic["fileNm"] = fileNm;
                            retDic["fullNm"] = fullNm;
                            retDic["resultMsg"] = "확장자 변경";
                        }
                    }
                }

                if (res.ContentLength <= 0)
                {                    
                    retDic["resultMsg"] = "Content-Lenght:0";
                    res.Close();
                }
                else
                {
                    FileStream fs = new FileStream(fullNm, FileMode.Create);
                    Stream stream = res.GetResponseStream();
                    stream.Flush();
                    int buffsize = 4096;
                    byte[] buff = new byte[buffsize];
                    while ((buffsize = stream.Read(buff, 0, buffsize)) > 0)
                    {
                        fs.Flush();
                        fs.Write(buff, 0, buffsize);
                    }
                    stream.Close();
                    fs.Close();
                    res.Close();
                    stream.Dispose();
                    fs.Dispose();
                    retDic["result"] = "success";
                }
            }
            catch (Exception ex)
            {
                retDic["resultMsg"] = ex.Message;
                if (res != null) res.Close();
                return retDic;
            }

            return retDic;
        }

        public bool DnImg(string url, string fileName)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = RndAgnt();
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            bool bImage = response.ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase);
            if ((response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Moved || response.StatusCode == HttpStatusCode.Redirect) && bImage)
            {
                using (Stream inputStream = response.GetResponseStream())
                using (Stream outputStream = File.OpenWrite(fileName))
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead;
                    do
                    {
                        bytesRead = inputStream.Read(buffer, 0, buffer.Length);
                        outputStream.Write(buffer, 0, bytesRead);
                    } while (bytesRead != 0);
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Nvgt(WebBrowser wbr, string url)
        {
            string userAgent = "";

            userAgent = RndAgnt();
            wbr.Navigate(url, null, null, "User-Agent: " + userAgent);
            wbr.DocumentCompleted += Wbr_DocumentCompleted;
        }

        private void Wbr_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            WebBrowser wbr = (WebBrowser)sender;
            HtmlElement head = wbr.Document.GetElementsByTagName("head")[0];
            HtmlElement scriptEl = wbr.Document.CreateElement("script");
            IHTMLScriptElement element = (IHTMLScriptElement)scriptEl.DomElement;
            element.text = "javascript:function r(d){d.oncontextmenu=null;d.ondragstart=null;d.onselectstart=null;d.onkeydown=null;d.onmousedown=null;d.body.oncontextmenu=null;d.body.ondragstart=null;d.body.onselectstart=null;d.body.onkeydown=null; d.body.onmousedown=null};function unify(w){r(w.document);if(w.frames.length>0){for(var i=0;i<w.frames.length;i++){try{unify(w.frames[i].window);}catch(e){}};};};unify(self);";
            head.AppendChild(scriptEl);

            Form frm = wbr.FindForm();
            if ((frm.Name == "wfCaMgmt" || frm.Name == "wfFileMgmt") && wbr.Name == "wbr1")
            {
                Control[] ctrls = wbr.Parent.Controls.Find("btnCaDocSave", true);
                ctrls[0].Visible = false;       //문서저장 버튼 숨김

                if (wbr.Url.ToString().Contains("courtauction.go.kr") && wbr.Url.ToString().Contains("RetrieveRealEstDetailInqSaList") == false && wbr.Url.ToString().Contains("tankauction.com") == false)
                {
                    HtmlElement scriptEl2 = wbr.Document.CreateElement("script");
                    IHTMLScriptElement element2 = (IHTMLScriptElement)scriptEl2.DomElement;
                    element2 = (IHTMLScriptElement)scriptEl2.DomElement;
                    element2.text = "function actSubmit(oform, action, target) {if (action) oform.action = action; oform.method = 'post'; oform.submit();}";
                    head.AppendChild(scriptEl2);
                    ctrls[0].Visible = true;    //문서저장 버튼 노출
                }
            }
        }

        /// <summary>
        /// 탱크 웹페이지 보기 전용-(구)브라우저
        /// </summary>
        /// <param name="wbr"></param>
        /// <param name="url"></param>
        public void TankWebView(WebBrowser wbr, string url)
        {
            Control[] ctrls = wbr.Parent.Controls.Find("btnCaDocSave", true);
            if(ctrls.Count() > 0) ctrls[0].Visible = false;       //문서저장 버튼 숨김

            var timeStamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
            wbr.Navigate(Properties.Settings.Default.myWeb + "SOLAR/webView.php?url=" + System.Web.HttpUtility.UrlEncode(url), null, null, "CALL-FROM:SOLAR\r\nCALL-TIME:" + timeStamp);
        }

        /// <summary>
        /// 웹 페이지 메시지(Message from webpage) 안띄우기
        /// </summary>
        public void InjectAlertBlocker(WebBrowser wbr)
        {
            HtmlElement head = wbr.Document.GetElementsByTagName("head")[0];
            HtmlElement scriptEl = wbr.Document.CreateElement("script");
            IHTMLScriptElement element = (IHTMLScriptElement)scriptEl.DomElement;
            string alertBlocker = "window.alert = function () { }";
            element.text = alertBlocker;
            head.AppendChild(scriptEl);
        }

        public string TxtToFile(string txt, string fileName, Encoding encode = null)
        {
            string result = string.Empty;

            try
            {
                if (encode == null) encode = Encoding.Default;
                FileStream fs = new FileStream(fileName, FileMode.Create);
                StreamWriter sw = new StreamWriter(fs, encode);
                //StreamWriter sw = new StreamWriter(fs, encode);
                sw.Write(txt);
                sw.Close();
                fs.Close();
                result = "success";
            }
            catch (Exception ex)
            {
                result = ex.Message;
            }

            return result;
        }
    }
}
