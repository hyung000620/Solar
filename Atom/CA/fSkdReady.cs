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
    public partial class fSkdReady : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AuctCd auctCd = new AuctCd();
        ApiUtil api = new ApiUtil();
        AtomLog atomLog = new AtomLog(111);     //로그 생성

        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 1500, webCnt = 0;

        public fSkdReady()
        {
            InitializeComponent();
            this.Shown += FSkdReady_Shown;
        }

        private void FSkdReady_Shown(object sender, EventArgs e)
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
            atomLog.AddLog("▼ 예정물건-매각준비 체크");
            Prc_Chk1();

            atomLog.AddLog("▼ 예정물건-매각공고 등록 체크");
            Prc_Chk2();

            atomLog.AddLog("▼ 예정물건-매각공고 변경 체크");
            Prc_Chk3();
            
            //등기 자동발급 대상 추가
            atomLog.AddLog("▼ 등기 자동발급 대상");
            Prc_RgstIssueAdd();

            atomLog.AddLog("실행 완료", 1);
        }

        /// <summary>
        /// 매각준비 체크
        /// </summary>
        private void Prc_Chk1()
        {
            int i = 0, totCnt = 0, sucCnt = 0;
            string url, sql, tid, jiwonNm, saNo, html;

            sql = "select tid, spt, sn1, sn2 from ta_list where sta2=1010 and shr_dt < curdate() and ini_dt < date_sub(curdate(),interval 7 day) order by tid";
            DataTable dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;

            foreach (DataRow row in dt.Rows)
            {
                i++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                tid = row["tid"].ToString();
                txtPrgs.AppendText(string.Format("\r\n> {0} / {1} > {2}", i, totCnt, tid));

                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                url = "http://www.courtauction.go.kr/RetrieveRealEstSaDetailInqMungunSongdalList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
                html = net.GetHtml(url);
                if (html.Contains("매각결정기일통지서") == false) continue;

                db.Open();
                sql = "update ta_list set sta2=1011 where tid=" + tid + " and sta1=10";
                db.ExeQry(sql);

                sql = "insert ignore into db_tank.tx_ready set tid=" + tid + ", wdt=curdate()";
                db.ExeQry(sql);
                db.Close();

                sucCnt++;
                txtPrgs.AppendText("-> success");
            }
            atomLog.AddLog(string.Format("> 매각준비 {0}건", sucCnt));
        }

        /// <summary>
        /// 매각공고 등록 체크
        /// </summary>
        private void Prc_Chk2()
        {
            int i = 0, totCnt = 0, sucCnt = 0;
            string url, sql, tid, jiwonNm, saNo, html;
            string appeal, bidDt, dtDvsn, bidRslt;

            sql = "select L.tid,spt,sn1,sn2 from db_main.ta_list L, db_tank.tx_ready R where L.tid=R.tid and sta2=1011 order by tid";
            DataTable dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;
            dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;
            HAPDoc doc = new HAPDoc();

            foreach (DataRow row in dt.Rows)
            {
                i++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                tid = row["tid"].ToString();
                appeal = string.Empty;
                txtPrgs.AppendText(string.Format("\r\n> {0} / {1} > {2}", i, totCnt, tid));

                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                url = "http://www.courtauction.go.kr/RetrieveRealEstDetailInqSaList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
                html = net.GetHtml(url);
                doc.LoadHtml(html);
                HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='사건기본내역 표']/tr");
                if (ncTr == null) continue;
                foreach (HtmlNode tr in ncTr)
                {
                    HtmlNodeCollection ncTd = tr.SelectNodes("th|td");
                    foreach (HtmlNode td in ncTd)
                    {
                        if (td.InnerText == "사건항고/정지여부") appeal = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                    }
                }
                if (appeal != string.Empty) continue;


                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                url = "http://www.courtauction.go.kr/RetrieveRealEstSaDetailInqGiilList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
                html = net.GetHtml(url);
                doc.LoadHtml(html);
                ncTr = doc.DocumentNode.SelectNodes("//table[@summary='기일내역 표']/tbody/tr[@class='Ltbl_list_lvl0' or @class='Ltbl_list_lvl1']");
                if (ncTr == null) continue;

                List<HtmlNode> lsTrNd = ncTr.ToList();
                lsTrNd.Reverse();
                foreach (HtmlNode ndTr in lsTrNd)
                {
                    HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                    if (ncTd.Count == 7)
                    {
                        if (ncTd[0].InnerText.Trim() == string.Empty) continue;
                        bidDt = ncTd[2].FirstChild.InnerText.Trim().Substring(0, 10).Replace(".", "-");
                        dtDvsn = ncTd[3].FirstChild.InnerText.Trim();
                        bidRslt = ncTd[6].FirstChild.InnerText.Trim();
                    }
                    else if (ncTd.Count == 5)
                    {
                        bidDt = ncTd[0].FirstChild.InnerText.Trim().Substring(0, 10).Replace(".", "-");
                        dtDvsn = ncTd[1].FirstChild.InnerText.Trim();
                        bidRslt = ncTd[4].FirstChild.InnerText.Trim();
                    }
                    else 
                    {
                        continue;
                    }

                    if (Convert.ToDateTime(bidDt) >= DateTime.Now && dtDvsn == "매각기일" && bidRslt == string.Empty)
                    {
                        sql = "update ta_list set sta2=1012 where tid=" + tid + " and sta1=10";
                        db.Open();
                        db.ExeQry(sql);
                        db.Close();
                        sucCnt++;
                        break;
                    }
                }
            }

            atomLog.AddLog(string.Format("> 매각공고 등록 {0}건", sucCnt));
        }

        /// <summary>
        /// 매각공고 변경 체크
        /// </summary>
        private void Prc_Chk3()
        {
            int i = 0, totCnt = 0, sucCnt = 0;
            string url, sql, tid, jiwonNm, saNo, html;
            string appeal, bidDt, dtDvsn, bidRslt;
            bool mdfyFlag = false;

            sql = "select L.tid,spt,sn1,sn2 from db_main.ta_list L, db_tank.tx_ready R where L.tid=R.tid and sta2=1012 order by tid";
            DataTable dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;
            dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;
            HAPDoc doc = new HAPDoc();

            foreach (DataRow row in dt.Rows)
            {
                i++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                tid = row["tid"].ToString();
                appeal = string.Empty;
                mdfyFlag = true;
                txtPrgs.AppendText(string.Format("\r\n> {0} / {1} > {2}", i, totCnt, tid));

                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                url = "http://www.courtauction.go.kr/RetrieveRealEstDetailInqSaList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
                html = net.GetHtml(url);
                doc.LoadHtml(html);
                HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='사건기본내역 표']/tr");
                if (ncTr == null) continue;
                foreach (HtmlNode tr in ncTr)
                {
                    HtmlNodeCollection ncTd = tr.SelectNodes("th|td");
                    foreach (HtmlNode td in ncTd)
                    {
                        if (td.InnerText == "사건항고/정지여부") appeal = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                    }
                }

                //매각준비 상태로 강등
                if (appeal != string.Empty)
                {
                    sql = "update ta_list set sta2=1011 where tid=" + tid + " and sta1=10";
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();
                    sucCnt++;
                    continue;
                }

                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                url = "http://www.courtauction.go.kr/RetrieveRealEstSaDetailInqGiilList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
                html = net.GetHtml(url);
                doc.LoadHtml(html);
                ncTr = doc.DocumentNode.SelectNodes("//table[@summary='기일내역 표']/tbody/tr[@class='Ltbl_list_lvl0' or @class='Ltbl_list_lvl1']");
                if (ncTr == null) continue;

                List<HtmlNode> lsTrNd = ncTr.ToList();
                lsTrNd.Reverse();
                foreach (HtmlNode ndTr in lsTrNd)
                {
                    HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                    if (ncTd.Count == 7)
                    {
                        if (ncTd[0].InnerText.Trim() == string.Empty) continue;
                        bidDt = ncTd[2].FirstChild.InnerText.Trim().Substring(0, 10).Replace(".", "-");
                        dtDvsn = ncTd[3].FirstChild.InnerText.Trim();
                        bidRslt = ncTd[6].FirstChild.InnerText.Trim();
                    }
                    else if (ncTd.Count == 5)
                    {
                        bidDt = ncTd[0].FirstChild.InnerText.Trim().Substring(0, 10).Replace(".", "-");
                        dtDvsn = ncTd[1].FirstChild.InnerText.Trim();
                        bidRslt = ncTd[4].FirstChild.InnerText.Trim();
                    }
                    else
                    {
                        continue;
                    }

                    if (Convert.ToDateTime(bidDt) >= DateTime.Now && dtDvsn == "매각기일" && bidRslt == string.Empty)
                    {
                        mdfyFlag = false;                        
                        break;
                    }
                }

                if (mdfyFlag)
                {
                    sql = "update ta_list set sta2=1011 where tid=" + tid + " and sta1=10";
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();
                    sucCnt++;
                }
            }

            atomLog.AddLog(string.Format("> 매각공고 변경 {0}건", sucCnt));
        }

        /// <summary>
        /// 등기 자동발급 대상처리
        /// A-토지, B-건물, C-집합건물
        /// 대상 -> A, B, C, AB (단일 또는 토지/건물)
        /// </summary>
        private void Prc_RgstIssueAdd()
        {
            string sql, tid, tbl, prevTid = "";
            bool fileExist, autoExist;
            int landCnt = 0, bldgCnt = 0, issueCnt = 0;
            string autoDvsn = "14";     //발급 구분 -> 예정 물건

            DataTable dt = new DataTable();
            dt.Columns.Add("tid");
            dt.Columns.Add("lsIdx");
            dt.Columns.Add("lsNo");
            dt.Columns.Add("lsType");
            dt.Columns.Add("pin");
                        
            sql = "select L.tid,spt,sn1,sn2,cat3, S.idx,no,dvsn,pin,pnu from ta_list L, ta_ls S, ta_dtl D where L.tid=S.tid and S.tid=D.tid and sta2 in (1011,1012) and cat1!=30 and D.pin_land='' and D.pin_bldg='' and S.note='미종국' order by L.tid";     //매각준비, 매각공고
            //sql = $"SELECT tid,shr_dt,datediff(curdate(),shr_dt) as diff FROM `ta_list` WHERE `sta1` = 10 and cat1 != 30 and shr_dt < curdate() and datediff(curdate(),shr_dt) > 30 and datediff(curdate(),shr_dt) < 365";
            DataTable dtLs = db.ExeDt(sql);
            foreach (DataRow row in dtLs.Rows)
            {
                tid = row["tid"].ToString();
                if (tid == prevTid) continue;

                //등기파일 유무 체크
                tbl = (Convert.ToDecimal($"{row["sn1"]}") > 2004) ? ("ta_f" + $"{row["sn1"]}") : "ta_f2004";
                sql = "select * from " + tbl + " where ctgr in ('DA','DB') and tid=" + tid + " limit 1";
                db.Open();
                fileExist = db.ExistRow(sql);
                db.Close();
                if (fileExist)
                {
                    prevTid = tid;
                    continue;
                }

                DataRow[] rows = dtLs.Select($"tid={tid}");
                if (rows.Count() == 1)
                {
                    DataRow r = rows[0];
                    if ($"{r["pin"]}" == string.Empty || ($"{r["dvsn"]}" != "토지" && $"{r["dvsn"]}" != "건물" && $"{r["dvsn"]}" != "집합건물"))
                    {
                        prevTid = tid;
                        continue;
                    }
                    dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["dvsn"], r["pin"] });    //대상
                }

                if (rows.Count() == 2)
                {
                    DataTable dtS = rows.CopyToDataTable();
                    landCnt = dtS.Select("dvsn='토지'").Count();
                    bldgCnt = dtS.Select("dvsn='건물'").Count();
                    if (landCnt == 1 && bldgCnt == 1)
                    {
                        DataRow r = dtS.Rows[0];
                        if ($"{r["pin"]}" != string.Empty) dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["dvsn"], r["pin"] });     //대상
                        r = dtS.Rows[1];
                        if ($"{r["pin"]}" != string.Empty) dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["dvsn"], r["pin"] });     //대상
                    }
                }

                prevTid = tid;
            }

            //대상 db 일괄 추가
            if (dt.Rows.Count > 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    db.Open();
                    autoExist = db.ExistRow($"select idx from db_tank.tx_rgst_auto where (dvsn between 10 and 14) and tid='{row["tid"]}' and pin='{row["pin"]}' and wdt=curdate() limit 1");
                    if (!autoExist)
                    {
                        db.ExeQry($"insert into db_tank.tx_rgst_auto set dvsn='{autoDvsn}', tid='{row["tid"]}', ls_no='{row["lsNo"]}', ls_type='{row["lsType"]}', pin='{row["pin"]}', wdt=curdate(), wtm=curtime()");
                        issueCnt++;
                    }
                    db.Close();
                }
            }

            atomLog.AddLog($"> 발급 대상-{issueCnt}");
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            bgwork.Dispose();

            this.Dispose();
            this.Close();
        }
    }
}
