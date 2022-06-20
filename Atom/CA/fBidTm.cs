using Solar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Text.RegularExpressions;
using HtmlAgilityPack;
using hap = HtmlAgilityPack;
using HAPDoc = HtmlAgilityPack.HtmlDocument;
using MySql.Data.MySqlClient;
using System.Collections;
using System.Xml;
using System.Diagnostics;

namespace Atom.CA
{
    public partial class fBidTm : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AtomLog atomLog = new AtomLog(108);
        AuctCd auctCd = new AuctCd();
        ApiUtil api = new ApiUtil();

        BackgroundWorker bgwork;
        int setCnt = 3, setSleep = 3000, webCnt = 0;

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        int cmpDvsnCd = 15;     //입찰시간(RptDvsn: 15)

        public fBidTm()
        {
            InitializeComponent();
            this.Shown += FBidTm_Shown;
        }

        private void FBidTm_Shown(object sender, EventArgs e)
        {
            CheckForIllegalCrossThreadCalls = false;
            bgwork = new BackgroundWorker();
            bgwork.DoWork += Bgwork_DoWork;
            bgwork.RunWorkerCompleted += Bgwork_RunWorkerCompleted;
            bgwork.WorkerReportsProgress = false;
            bgwork.WorkerSupportsCancellation = true;

            BaseDtInit();

            bgwork.RunWorkerAsync();
        }

        private void BaseDtInit()
        {
            //
        }

        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            decimal i = 0, pgCnt = 0, findCnt = 0, ndCnt = 0;
            string sql, url, urlPs, lawNm, jiwonNm, maeGiil, jpDeptCd;
            string html, tid, spt, sn, sn1, sn2, pn, bigo, dpstRate, bidTm1 = "", bidTm2 = "";

            DataTable dt = new DataTable();
            dt.Columns.Add("sn1");
            dt.Columns.Add("sn2");
            dt.Columns.Add("pn");
            dt.Columns.Add("bidTm");
            dt.Columns.Add("dpstRate");

            HAPDoc doc = new HAPDoc();

            urlPs = "http://www.courtauction.go.kr/RetrieveRealEstMulDetailList.laf?page=default40&ipchalGbncd=000331&srnID=PNO102005&";

            sql = "select spt, dpt, bid_cnt, date_format(bid_dt,'%Y.%m.%d') as bidDt from ta_skd where bid_dt between '" + DateTime.Now.ToShortDateString() + "' and '" + DateTime.Now.AddDays(14).ToShortDateString() + "' order by bid_dt";
            DataTable dtS = db.ExeDt(sql);

            sql = "select tid, spt, sn1, sn2, if(pn=0,1,pn) as pnum, bid_tm, bid_tm1, bid_tm2, dpst_rate from ta_list where sta1=11 and bid_dt between '" + DateTime.Now.ToShortDateString() + "' and '" + DateTime.Now.AddDays(14).ToShortDateString() + "'";
            DataTable dtL = db.ExeDt(sql);

            foreach (DataRow row in dtS.Rows)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                spt = row["spt"].ToString();
                jiwonNm = auctCd.LawNmEnc(null, row["spt"].ToString());
                maeGiil = row["bidDt"].ToString().Replace(".", string.Empty);
                jpDeptCd = row["dpt"].ToString();

                url = urlPs + "jiwonNm=" + jiwonNm + "&jpDeptCd=" + jpDeptCd + "&maeGiil=" + maeGiil;
                html = net.GetHtml(url);
                if (html.Contains("검색결과가 없습니다")) continue;

                MatchCollection mc = Regex.Matches(html, @"goPage\('(\d+)'\)", rxOptM);
                if (mc.Count > 0) pgCnt = Math.Ceiling(Convert.ToDecimal(mc[mc.Count - 1].Groups[1].Value) / 40);
                else pgCnt = 1;

                lawNm = auctCd.FindLawNm(row["spt"].ToString());
                txtPrgs.AppendText("\r\n ---> " + row["bidDt"].ToString() + " > " + lawNm);

                for (i = 1; i <= pgCnt; i++)
                {
                    txtPrgs.AppendText("\r\n > Page " + i.ToString());
                    if (i > 1)
                    {
                        webCnt++;
                        if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                        html = net.GetHtml(url + "&targetRow=" + ((i - 1) * 40 + 1).ToString());
                        if (html.Contains("검색결과가 없습니다")) continue;
                    }
                    doc.LoadHtml(html);
                    HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='물건상세검색 결과 표']/tbody/tr");
                    if (ncTr == null) continue;
                    foreach (HtmlNode ndTr in ncTr)
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        sn = ncTd[1].SelectSingleNode("./a/b").InnerText;
                        Match match = Regex.Match(sn, @"(\d+)타경(\d+)", rxOptM);
                        sn1 = match.Groups[1].Value;
                        sn2 = match.Groups[2].Value;
                        pn = ncTd[2].FirstChild.InnerText;
                        bigo = ncTd[4].InnerText;
                        ndCnt = ncTd[6].SelectNodes("./div/text()").Count;                        
                        if (ndCnt < 3) continue;
                        
                        var val = ncTd[6].SelectSingleNode("./div/a").GetAttributeValue("onclick", "");
                        match = Regex.Match(val, @"showJpDeptInofTitle\('.*',[ ]*'\d+.\d+.\d+ (\d+:\d+)[, ]*(\d+:\d+)*',[ ]*'.*',[ ]*'.*'\)", rxOptM);
                        if (match.Success)
                        {
                            bidTm1 = match.Groups[1].Value + ":00";
                            bidTm2 = (match.Groups[2].Value == string.Empty) ? "00:00:00" : match.Groups[2].Value + ":00";
                        }
                        dpstRate = GetDpstRate(bigo);
                        if (dpstRate == string.Empty) dpstRate = "10";

                        DataRow xRow = dtL.Select("spt='" + spt + "' and sn1='" + sn1 + "' and sn2='" + sn2 + "' and pnum='" + pn + "'").FirstOrDefault();
                        if (xRow != null)
                        {
                            if (xRow["bid_tm1"].ToString() != bidTm1 || xRow["bid_tm2"].ToString() != bidTm2)
                            {
                                tid = xRow["tid"].ToString();
                                sql = "insert into db_tank.tx_rpt set tid='" + tid + "', dvsn='" + cmpDvsnCd + "', bid_tm1='" + bidTm1 + "', bid_tm2='" + bidTm2 + "', wdt=curdate()";
                                db.Open();
                                db.ExeQry(sql);
                                db.Close();
                                findCnt++;
                            }
                            //if (xRow["dpst_rate"].ToString() != dpstRate) { }
                        }
                    }
                }
            }
            atomLog.AddLog(findCnt.ToString() + " 건 발견");
        }

        private string GetDpstRate(string str)
        {
            string rate = "", patn = "";

            patn = @"보증금[은\s]*(최저매각가격의)*[\s]*((\d+)%|(\d+)[분의\s]*(\d+))";

            Match match = Regex.Match(str, patn, rxOptS);
            if (match.Success)
            {
                if (match.Groups[3].Value != string.Empty) rate = match.Groups[3].Value;
                else if (match.Groups[4].Value != string.Empty && match.Groups[5].Value != string.Empty)
                {
                    rate = string.Format("{0:F0}", (decimal.Parse(match.Groups[5].Value) / decimal.Parse(match.Groups[4].Value)) * 100);
                }
            }

            return rate;
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            atomLog.AddLog("실행 완료", 1);
            bgwork.Dispose();
            /*
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = @"C:\Atom\Atom.exe";
            psi.Arguments = "경매-공고수집";
            Process.Start(psi);
            */
            this.Dispose();
            this.Close();
        }
    }
}
