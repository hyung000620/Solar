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
    public partial class fFbChk : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AtomLog atomLog = new AtomLog(109);     //유찰확인
        AuctCd auctCd = new AuctCd();
        ApiUtil api = new ApiUtil();

        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 1500, webCnt = 0;

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        int cmpDvsnCd = 16;     //유찰확인(RptDvsn: 16)

        public fFbChk()
        {
            InitializeComponent();
            this.Shown += FFbChk_Shown;
        }

        private void FFbChk_Shown(object sender, EventArgs e)
        {
            CheckForIllegalCrossThreadCalls = false;
            bgwork = new BackgroundWorker();
            bgwork.DoWork += Bgwork_DoWork;
            bgwork.RunWorkerCompleted += Bgwork_RunWorkerCompleted;
            bgwork.WorkerReportsProgress = false;
            bgwork.WorkerSupportsCancellation = true;

            //BaseDtInit();

            bgwork.RunWorkerAsync();
        }

        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            string url = "", cmprDt, sql, tid, jiwonNm, lawNm, saNo, sn, sn1, sn2, pn = "", dtDvsn = "", sucbAmt, law, prevLaw = "", bidDt = "", bidRslt = "", prevSaNo = "", html = "";
            string bidDtT = "", bidDtC = "";

            if (DateTime.Now.DayOfWeek == DayOfWeek.Monday)
            {
                cmprDt = DateTime.Now.AddDays(-3).ToShortDateString();
            }
            else
            {
                cmprDt = DateTime.Now.AddDays(-1).ToShortDateString();
            }
            sql = "select L.tid, spt, sn1, sn2, if(pn=0,1,pn) as pnum, date_format(L.bid_dt,'%Y.%m.%d') as nxtBidDt from ta_list L, ta_hist H where L.tid=H.tid and sta2=1111 and H.bid_dt='" + cmprDt + "' order by spt, sn1, sn2, pn";
            DataTable dt = db.ExeDt(sql);

            HAPDoc doc = new HAPDoc();
            HtmlNodeCollection ncTr = null;
            cmprDt = cmprDt.Replace("-", ".");

            foreach (DataRow row in dt.Rows)
            {
                tid = row["tid"].ToString();
                bidDtC = string.Empty;
                bidDtT = row["nxtBidDt"].ToString();

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

                    if (pn == row["pnum"].ToString() && dtDvsn == "매각기일" && Convert.ToDateTime(bidDt) > Convert.ToDateTime(cmprDt))
                    {
                        bidDtC = bidDt;
                        break;
                    }
                }

                if (bidDtC == string.Empty) continue;
                if (bidDtT != bidDtC)
                {
                    //MessageBox.Show(string.Format("bidDtT-{0} / bidDtC-{1}", bidDtT, bidDtC));
                    sql = "insert into db_tank.tx_rpt set dvsn='" + cmpDvsnCd + "', tid='" + tid + "', bid_dt='" + bidDtC + "', wdt=CURDATE()";
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();
                }
                txtPrgs.AppendText(string.Format("\r\n> 기일내역 {0}-{1}", row["sn1"], row["sn2"]));
                prevSaNo = saNo;
            }
            atomLog.AddLog("실행 완료", 1);
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {            
            bgwork.Dispose();

            this.Dispose();
            this.Close();
        }
    }
}
