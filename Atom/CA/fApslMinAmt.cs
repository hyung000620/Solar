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

namespace Atom.CA
{
    public partial class fApslMinAmt : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AuctCd auctCd = new AuctCd();
        ApiUtil api = new ApiUtil();
        AtomLog atomLog = new AtomLog(103);     //로그 생성

        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 1500, webCnt = 0;

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        //RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        int cmpDvsnCd = 10;     //감정가/최저가/매각결과 비교(RptDvsn: 10)

        public fApslMinAmt()
        {
            InitializeComponent();
            this.Shown += FApslAmt_Shown;
        }

        private void FApslAmt_Shown(object sender, EventArgs e)
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
            //매각 결과
            atomLog.AddLog("# 매각 결과 비교 시작 #");
            Prc_DpslRslt();
            atomLog.AddLog("# 매각 결과 비교 끝 #");

            //감정가/최저가
            atomLog.AddLog("# 감정가/최저가 비교 시작 #");
            Prc_ApslMinAmt();
            atomLog.AddLog("# 감정가/최저가 비교 끝 #");
        }

        /// <summary>
        /// 매각결과
        /// </summary>
        private void Prc_DpslRslt()
        {
            decimal i = 0, pgCnt = 0, totCnt = 0, curCnt = 0, ndCnt = 0;
            string url = "", sql, tid, jiwonNm, lawNm, saNo, sn, sn1, sn2, pn = "", dtDvsn = "", sucbAmt, law, prevLaw = "", bidDt = "", bidRslt = "", prevSaNo = "", html = "";
            
            txtPrgs.AppendText("\r\n ### 매각결과 비교 ### \r\n");

            sql = "select L.tid,spt,sn1,sn2,pn,sta1,sta2,'' as state,sucb_amt,H.bid_dt,H.bid_tm, '' as C_bidDt, '' as C_rslt, '' as C_sucbAmt from ta_list L, ta_hist H " +
                "where L.tid=H.tid and sta2 != 1415 and H.sta in (1110,1111,1210) and H.bid_dt between '" + DateTime.Now.AddDays(-7).ToShortDateString() + "' and '" + DateTime.Now.AddDays(-1).ToShortDateString() + "' group by L.tid order by spt,sn1,sn2";
            DataTable dtL = db.ExeDt(sql);
            HAPDoc doc = new HAPDoc();
            HtmlNodeCollection ncTr = null;
            totCnt = dtL.Rows.Count;

            foreach (DataRow row in dtL.Rows)
            {
                pn = row["pn"].ToString();
                if (pn == "0") row["pn"] = "1";
                if (row["sta2"].ToString() == "1111") row["state"] = "유찰";
                else if (row["sta2"].ToString() == "1210") row["state"] = "매각";
                else row["state"] = "기타";

                row["C_sucbAmt"] = 0;
            }

            //매각 결과
            foreach (DataRow row in dtL.Rows)
            {
                curCnt++;
                law = row["spt"].ToString();

                if (law != prevLaw)
                {
                    webCnt++;
                    if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                    jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                    //saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                    url = "http://www.courtauction.go.kr/RetrieveRealEstMgakGyulgwaMulList.laf?page=default40&jiwonNm=" + jiwonNm;
                    html = net.GetHtml(url);
                    if (html.Contains("검색결과가 없습니다")) continue;
                    
                    MatchCollection mc = Regex.Matches(html, @"goPage\('(\d+)'\)", rxOptM);
                    if (mc.Count > 0) pgCnt = Math.Ceiling(Convert.ToDecimal(mc[mc.Count - 1].Groups[1].Value) / 40);
                    else pgCnt = 1;

                    lawNm = auctCd.FindLawNm(row["spt"].ToString());
                    txtPrgs.AppendText("\r\n ---> " + lawNm);

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
                        ncTr = doc.DocumentNode.SelectNodes("//table[@summary='매각결과검색 결과 표']/tbody/tr");
                        if (ncTr == null) continue;
                        foreach (HtmlNode ndTr in ncTr)
                        {
                            HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                            sn = ncTd[1].SelectSingleNode("./a/b").InnerText;
                            Match match = Regex.Match(sn, @"(\d+)타경(\d+)", rxOptM);
                            sn1 = match.Groups[1].Value;
                            sn2 = match.Groups[2].Value;
                            //pn = ncTd[2].SelectNodes("./text()")[0].InnerText;
                            pn = ncTd[2].FirstChild.InnerText;
                            //bidDt = ncTd[6].SelectNodes("./div[1]/text()")[1].InnerText.Trim();
                            ndCnt = ncTd[6].SelectNodes("./div/text()").Count;
                            if (ndCnt < 3) continue;
                            bidDt = ncTd[6].SelectNodes("./div/text()")[1].InnerText.Trim();
                            bidRslt = ncTd[6].SelectNodes("./div/text()")[2].InnerText.Trim();
                            sucbAmt = (ndCnt == 4) ? ncTd[6].SelectNodes("./div/text()")[3].InnerText.Replace(",", string.Empty).Trim() : string.Empty;
                            
                            DataRow xRow = dtL.Select("spt='" + law + "' and sn1='" + sn1 + "' and sn2='" + sn2 + "' and pn='" + pn + "'").FirstOrDefault();
                            if (xRow != null)
                            {
                                xRow["C_bidDt"] = bidDt;
                                xRow["C_rslt"] = bidRslt;
                                xRow["C_sucbAmt"] = sucbAmt;
                            }
                            //txtProgrs.AppendText("\r\n> " + saNo);
                        }
                    }
                }
                txtPrgs.AppendText(string.Format("\r\n> 매각결과 {0}-{1} -> {2} / {3}", row["sn1"], row["sn2"], curCnt, totCnt));
                prevLaw = law;
            }
            
            //기일 내역(결과 없는 사건)
            DataRow[] rows = dtL.Select("C_rslt='' and C_bidDt=''");
            foreach (DataRow row in rows)
            {
                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                if (saNo != prevSaNo)
                {
                    webCnt++;
                    if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                    url = "http://www.courtauction.go.kr/RetrieveRealEstSaDetailInqGiilList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
                    html = net.GetHtml(url);
                }
                if (html.Contains("검색결과가 없습니다")) continue;

                doc.LoadHtml(html);
                ncTr = doc.DocumentNode.SelectNodes("//table[@summary='기일내역 표']/tbody/tr[@class='Ltbl_list_lvl0' or @class='Ltbl_list_lvl1']");
                if (ncTr == null) continue;
                foreach (HtmlNode ndTr in ncTr)
                {
                    HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                    //MessageBox.Show(ncTd.Count.ToString());
                    if (ncTd.Count == 7)
                    {
                        if (ncTd[0].FirstChild != null)
                        {
                            pn = ncTd[0].FirstChild.InnerText.Trim();
                        }                        
                        bidDt = ncTd[2].FirstChild.InnerText.Trim().Substring(0, 10);
                        dtDvsn = ncTd[3].FirstChild.InnerText.Trim();
                        bidRslt = ncTd[6].FirstChild.InnerText.Trim();
                    }
                    else if (ncTd.Count == 5)
                    {
                        bidDt = ncTd[0].FirstChild.InnerText.Trim().Substring(0, 10);
                        dtDvsn = ncTd[1].FirstChild.InnerText.Trim();
                        bidRslt = ncTd[4].FirstChild.InnerText.Trim();
                    }
                    else { }

                    if (pn == row["pn"].ToString() && dtDvsn == "매각기일" && bidRslt != string.Empty && Convert.ToDateTime(bidDt) > DateTime.Now.AddDays(-8) && Convert.ToDateTime(bidDt) <= DateTime.Now.AddDays(-1))
                    {
                        dtL.Rows[dtL.Rows.IndexOf(row)]["C_rslt"] = bidRslt;
                    }
                }
                txtPrgs.AppendText(string.Format("\r\n> 기일내역 {0}-{1}", row["sn1"], row["sn2"]));
                prevSaNo = saNo;
            }

            //상호 비교
            var xRows = dtL.Rows.Cast<DataRow>().Where(row => row["sucb_amt"].ToString() != row["C_sucbAmt"].ToString() || row["state"].ToString() != row["C_rslt"].ToString());
            if (xRows == null) return;

            db.Open();
            foreach (DataRow row in xRows)
            {
                //if (row["C_rslt"].ToString() == string.Empty || row["C_sucbAmt"].ToString() == string.Empty) continue;
                tid = row["tid"].ToString();
                sucbAmt = row["C_sucbAmt"].ToString();
                bidRslt = row["C_rslt"].ToString();
                if ((bidRslt != string.Empty && bidRslt != row["state"].ToString()) || (sucbAmt != string.Empty && sucbAmt != row["sucb_amt"].ToString()))
                {
                    sql = "insert into db_tank.tx_rpt set dvsn='" + cmpDvsnCd + "', tid='" + tid + "', sucb_amt='" + sucbAmt + "', state='" + bidRslt + "', wdt=CURDATE()";
                    db.ExeQry(sql);
                }                                
                //txtProgrs.AppendText(" -> 매각결과 상이");
            }
            db.Close();
        }

        /// <summary>
        /// 감정가/최저가 비교
        /// </summary>
        private void Prc_ApslMinAmt()
        {
            decimal totCnt = 0, curCnt = 0;
            string url = "", prevUrl = "", html, sql, tid, jiwonNm, saNo, pn, apslAmt, minbAmt, bidDt, bidTm;
            string caPn = "", caApslAmt = "", caMinbAmt = "", caDt = "", caTm = "", dtDvsn = "";

            txtPrgs.AppendText("\r\n ### 감정가/최저가 비교 ### \r\n");
                        
            sql = "select tid,spt,sn1,sn2,pn,apsl_amt,minb_amt,bid_dt,bid_tm from ta_list where sta1=11 order by spt, sn1, sn2, pn";
            DataTable dtL = db.ExeDt(sql);
            HAPDoc doc = new HAPDoc();
            HtmlNodeCollection ncTr = null;
            totCnt = dtL.Rows.Count;

            foreach (DataRow row in dtL.Rows)
            {
                curCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                txtPrgs.AppendText(string.Format("\r\n{0}-{1} -> {2} / {3}", row["sn1"], row["sn2"], curCnt, totCnt));

                tid = row["tid"].ToString();
                pn = row["pn"].ToString();
                if (pn == "0") pn = "1";
                apslAmt = row["apsl_amt"].ToString();
                minbAmt = row["minb_amt"].ToString();
                bidDt = string.Format("{0:yyyyMMdd}", row["bid_dt"]);
                bidTm = row["bid_tm"].ToString().Substring(0, 5);

                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                url = "http://www.courtauction.go.kr/RetrieveRealEstSaDetailInqGiilList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
                if (url != prevUrl)
                {
                    webCnt++;
                    html = net.GetHtml(url);
                    if (html.Contains("검색결과가 없습니다")) continue;

                    doc.LoadHtml(html);
                    ncTr = doc.DocumentNode.SelectNodes("//table[@summary='기일내역 표']/tbody/tr");
                    if (ncTr == null) continue;
                }
                prevUrl = url;

                Regex rx = new Regex(@"(\d{4}.\d{2}.\d{2})\((\d{2}:\d{2})\)", rxOptM);

                db.Open();
                foreach (HtmlNode tr in ncTr)
                {
                    HtmlNodeCollection ncTd = tr.SelectNodes("./td");
                    if (ncTd.Count == 7)
                    {                        
                        caPn = ncTd[0].InnerText.Trim();
                        caApslAmt = ncTd[1].InnerText.Trim().Replace(",", string.Empty).Replace("원", string.Empty);
                        caDt = rx.Match(ncTd[2].InnerText.Trim()).Groups[1].Value.Replace(".", string.Empty);
                        caTm = rx.Match(ncTd[2].InnerText.Trim()).Groups[2].Value;
                        dtDvsn = ncTd[3].InnerText.Trim();
                        caMinbAmt = ncTd[5].InnerText.Trim().Replace(",", string.Empty).Replace("원", string.Empty);
                    }
                    else if (ncTd.Count == 5)
                    {
                        caDt = rx.Match(ncTd[0].InnerText.Trim()).Groups[1].Value.Replace(".", string.Empty);
                        caTm = rx.Match(ncTd[0].InnerText.Trim()).Groups[2].Value;
                        dtDvsn = ncTd[1].InnerText.Trim();
                        caMinbAmt = ncTd[3].InnerText.Trim().Replace(",", string.Empty).Replace("원", string.Empty);
                    }
                    if (caPn == pn && dtDvsn == "매각기일" && caDt == bidDt && caTm == bidTm)
                    {
                        if ((apslAmt != caApslAmt) || (minbAmt != caMinbAmt))
                        {
                            sql = "insert into db_tank.tx_rpt set dvsn='" + cmpDvsnCd + "', tid='" + tid + "', apsl_amt='" + caApslAmt + "', minb_amt='" + caMinbAmt + "', wdt=CURDATE()";
                            db.ExeQry(sql);
                        }
                        /*
                        //감정가 비교
                        if (apslAmt != caApslAmt)
                        {
                            db.Open();
                            sql = "insert into db_tank.tx_rpt set dvsn='" + cmpDvsnCd + "', tid='" + tid + "', apsl_amt='" + caApslAmt + "', wdt=CURDATE()";
                            db.ExeQry(sql);
                            db.Close();
                            txtProgrs.AppendText(" -> 감정가 상이");
                        }

                        //최저가 비교
                        if (minbAmt != caMinbAmt)
                        {
                            db.Open();
                            sql = "insert into db_tank.tx_rpt set dvsn='" + cmpDvsnCd + "', tid='" + tid + "', minb_amt='" + caMinbAmt + "', wdt=CURDATE()";
                            db.ExeQry(sql);
                            db.Close();
                            txtProgrs.AppendText(" -> 최저가 상이");
                        }
                        */
                    }
                }
                db.Close();
            }
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            atomLog.AddLog("실행 완료", 1);
            bgwork.Dispose();

            this.Dispose();
            this.Close();
        }
    }
}
