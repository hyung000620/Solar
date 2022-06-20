using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using hap = HtmlAgilityPack;
using HAPDoc = HtmlAgilityPack.HtmlDocument;

namespace Solar
{
    public class ApslAnaly
    {
        DbUtil db = new DbUtil();
        AuctCd auctCd = new AuctCd();
        NetUtil net = new NetUtil();
        ApiUtil api = new ApiUtil();

        static RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        static RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        public string tid { get; set; }
        public string saNo { get; set; }
        public bool dbPrc { get; set; } = false;
        public string analyRslt { get; set; }
        public DataTable dtRgCd { get; set; }

        public ApslAnaly()
        {
            //
        }

        public static (Dictionary<string, string> dic, DataTable dt) Proc(string tid, string htmlFile)
        {
            string html, html2, sumryTbl = "", apslTbl = "", apslNm = "", apslDt = "";
            int i = 0;

            Dictionary<string, string> dic = new Dictionary<string, string>();
            dic.Add("apslNm", string.Empty);
            dic.Add("apslDt", string.Empty);

            DataTable dt = new DataTable();
            dt.Columns.Add("lsNo");     //일련번호
            dt.Columns.Add("adrs");     //소재지
            dt.Columns.Add("jibun");    //지번
            //dt.Columns.Add("jimok");    //지목 및 용도
            dt.Columns.Add("area1");    //공부면적
            dt.Columns.Add("area2");    //사정면적
            dt.Columns.Add("price");    //단가
            dt.Columns.Add("amt");      //금액

            Stream stream = File.OpenRead(htmlFile);
            StreamReader sr = new StreamReader(stream, Encoding.UTF8);
            html = sr.ReadToEnd();
            sr.Close();
            sr.Dispose();
            stream.Close();
            stream.Dispose();

            HAPDoc doc = new HAPDoc();
            Match match, m;
            MatchCollection mc;
            
            match = Regex.Match(html, @"(\w+)감정평가(사사무소|사합동사무소|법인|사무소|사사)|[(주)쥐취（）]{2,}[ ]*감정평가법인[ ]*(\w+)|감정평가법인[ ]*(\w+)[(주)쥐취（）]{2,}", rxOptM);
            if (match.Groups[1].Value != string.Empty) apslNm = match.Groups[1].Value;
            else if (match.Groups[3].Value != string.Empty) apslNm = match.Groups[3].Value;
            else if (match.Groups[4].Value != string.Empty) apslNm = match.Groups[4].Value;

            if (apslNm == string.Empty)
            {
                match = Regex.Match(html, @">(\w+).*?감정평가사사무소|감정평가사사무소[ ]*(\w+)", rxOptM);
                if (match.Groups[1].Value != string.Empty) apslNm = match.Groups[1].Value;
                else if (match.Groups[2].Value != string.Empty) apslNm = match.Groups[2].Value;
            }

            if (apslNm != string.Empty)
            {
                apslNm = Regex.Replace(apslNm, @"^[주쥐취]|푸이", string.Empty);
                apslNm = apslNm.Replace("세종의", "세종");
                apslNm = apslNm.Replace("오남도", "남도");
                apslNm = apslNm.Replace("삼참", "삼창");
                apslNm = apslNm.Replace("삼챵", "삼창");
                apslNm = apslNm.Replace("롱일", "통일");
                apslNm = apslNm.Replace("신에", "신애");
                apslNm = apslNm.Replace("이득", "이목");

                if (apslNm == "가이") apslNm = "가이야";
                if (apslNm == "돗") apslNm = "동행";

                if ((apslNm == "비" || apslNm == "앤비") && html.Contains("TNB")) apslNm = "티앤비";
                if (apslNm == "현" && html.Contains("현산")) apslNm = "현산";
                if (apslNm == "일" && html.Contains("통일감정")) apslNm = "통일";
                if (apslNm == "뱅크" && html.Contains("리얼티")) apslNm = "리얼티뱅크";
                if ((apslNm == "효" || apslNm == "성") && html.Contains("효성")) apslNm = "효성";

                if (apslNm.Contains("국토")) apslNm = "국토";
                else if (apslNm.Contains("대교")) apslNm = "대교";  //대교의

                if (html.Contains("JK Lee")) apslNm = "이종경";
                else if (html.Contains("K-land")) apslNm = "K-land";
                else if (html.Contains("대한감정평가법인")) apslNm = "대한";
                else if (html.Contains("주원감정평가")) apslNm = "주원";
                else if (html.Contains("주영감정평가")) apslNm = "주영";
                else if (html.Contains("FIRST APPRAISAL ")) apslNm = "제일";
                else if (html.Contains("L.H")) apslNm = "L.H";
                else if (html.Contains("법인 에이블")) apslNm = "에이블";
            }
            else
            {
                if (html.IndexOf("REM", StringComparison.Ordinal) > -1) apslNm = "REM";
                else if (html.Contains("Samchang")) apslNm = "삼창";
                else if (html.Contains("JEONG AN")) apslNm = "정안";
                else if (html.Contains("효성감정평")) apslNm = "효성";
                else if (html.Contains("제일감정평")) apslNm = "제일";
                else if (html.Contains("윤슬")) apslNm = "윤슬";
                else if (Regex.IsMatch(html, @"나[ ]*라[ ]*감[ ]*정")) apslNm = "나라";

                if (apslNm == string.Empty)
                {
                    html2 = html.Replace(" ", string.Empty);
                    match = Regex.Match(html2, @"(\w+)감정평가(사사무소|사합동사무소|법인)|[(주)쥐취（）]{2,}감정평가법인(\w+)|감정평가법인(\w+)[(주)쥐취（）]{2,}", rxOptM);
                    if (match.Groups[1].Value != string.Empty) apslNm = match.Groups[1].Value;
                    else if (match.Groups[3].Value != string.Empty) apslNm = match.Groups[3].Value;
                    else if (match.Groups[4].Value != string.Empty) apslNm = match.Groups[4].Value;
                }
            }

            if (apslNm.Contains("감정원"))
            {
                apslNm = apslNm.Remove(apslNm.IndexOf("감정원"));
            }

            if (apslNm != string.Empty)
            {
                apslNm = Regex.Replace(apslNm, @"^[一-龥]", string.Empty);
                if ((apslNm == "효" || apslNm == "성") && html.Contains("효성")) apslNm = "효성";
            }

            dic["apslNm"] = apslNm;

            match = Regex.Match(html, @"감[ ]*정[ ]*평[ ]*가[ ]*표.*?(<table.*?</table>)", rxOptS);
            if (match.Success)
            {
                sumryTbl = match.Groups[1].Value;
                //m = Regex.Match(sumryTbl, @">(20\d{2})[.,년/\-][ ]*(\d+){1,2}[.,월/\-][ ]*(\d+){1,2}[.,일]*<", rxOptM);
                m = Regex.Match(sumryTbl, @"(20\d{2})[.,년/\-][ ]*(\d+){1,2}[.,월/\-][ ]*(\d+){1,2}[.,일]*", rxOptM);
                if (m.Success)
                {
                    apslDt = string.Format("{0}-{1}-{2}", m.Groups[1].Value, m.Groups[2].Value.PadLeft(2, '0'), m.Groups[3].Value.PadLeft(2, '0'));
                    dic["apslDt"] = apslDt;
                }
            }

            if (apslDt == string.Empty)
            {
                html2 = html.Replace(" ", string.Empty);
                mc = Regex.Matches(html2, @"<table.*?</table>", rxOptS);
                foreach (Match mt in mc)
                {
                    if (Regex.IsMatch(mt.Value, @"기[ ]*준[ ]*시[ ]*점", rxOptM))
                    {
                        sumryTbl = mt.Value;
                        //m = Regex.Match(mt.Value, @">(20\d{2})[.,년/\-][ ]*(\d+){1,2}[.,월/\-][ ]*(\d+){1,2}[.,일]*", rxOptM);
                        m = Regex.Match(mt.Value, @"(20\d{2})[.,년/\-][ ]*(\d+){1,2}[.,월/\-][ ]*(\d+){1,2}[.,일]*", rxOptM);
                        if (m.Success)
                        {
                            apslDt = string.Format("{0}-{1}-{2}", m.Groups[1].Value, m.Groups[2].Value.PadLeft(2, '0'), m.Groups[3].Value.PadLeft(2, '0'));
                            dic["apslDt"] = apslDt;
                        }
                        break;
                    }
                }

                //그래도 없다면 제일 처음 나오는 날짜를 취한다.
                if (apslDt == string.Empty)
                {
                    m = Regex.Match(html2, @"(20\d{2})[.,][ ]*(\d+){1,2}[.,][ ]*(\d+){1,2}[.,]*", rxOptM);
                    if (m.Success)
                    {
                        apslDt = string.Format("{0}-{1}-{2}", m.Groups[1].Value, m.Groups[2].Value.PadLeft(2, '0'), m.Groups[3].Value.PadLeft(2, '0'));
                        dic["apslDt"] = apslDt;
                    }
                }
            }

            //감정평가표
            if (sumryTbl != string.Empty)
            { 
                //
            }

            //감정평가명세표
            mc = Regex.Matches(html, @"평가[ ]*명세표.*?(<table.*?</table>)", rxOptS);
            if (mc.Count > 0)
            {
                foreach (Match mt in mc)
                {
                    apslTbl = mt.Groups[1].Value;
                    doc.LoadHtml(apslTbl);
                    HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table/tr");
                    if (ncTr == null) continue;
                    foreach (HtmlNode ndTr in ncTr)
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        if (ncTd.Count < 10) continue;

                        DataRow row = dt.NewRow();
                        row["lsNo"] = ncTd[0].InnerText.Trim();
                        row["adrs"] = ncTd[1].InnerText.Trim();
                        row["jibun"] = ncTd[2].InnerText.Trim();
                        row["area1"] = ncTd[5].InnerText.Trim();
                        dt.Rows.Add(row);
                    }
                }
            }

            return (dic, dt);
        }
    }
}
