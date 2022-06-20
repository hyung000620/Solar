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
    public partial class fState : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AuctCd auctCd = new AuctCd();
        ApiUtil api = new ApiUtil();
        AtomLog atomLog = new AtomLog(104);     //로그 생성

        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 1500, webCnt = 0;

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        //RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        int cmpDvsnCd = 11;     //물건상태 비교(RptDvsn: 11)
        string vmNm = Environment.MachineName;

        public fState()
        {
            InitializeComponent();
            this.Shown += FState_Shown;
        }

        private void FState_Shown(object sender, EventArgs e)
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
            decimal totCnt = 0, curCnt = 0;
            string url, sql = "", tid, jiwonNm, saNo, sta1, prevSaNo = "", htmlSagun = "", htmlGiil = "";

            txtPrgs.AppendText("\r\n ### 물건상태 비교 ### \r\n");

            switch (vmNm)
            {
                case "VM-1":
                    sql = "select tid, spt, sn1, sn2, pn, bid_dt, end_dt, sta1, sta2 from ta_list where sta1=11 order by crt, spt, sn1, sn2, pn";   //진행
                    break;

                case "VM-2":
                    sql = "select tid, spt, sn1, sn2, pn, bid_dt, end_dt, sta1, sta2 from ta_list where sta1=13 order by crt, spt, sn1, sn2, pn";   //미진행
                    break;

                case "VM-3":
                    sql = "select tid, spt, sn1, sn2, pn, bid_dt, end_dt, sta1, sta2 from ta_list where sta1=10 and ini_dt <= date_sub(curdate(),interval 15 day) and (tid % 2)=1 order by crt, spt, sn1, sn2";     //예정-TID 홀수
                    break;

                case "VM-4":
                    sql = "select tid, spt, sn1, sn2, pn, bid_dt, end_dt, sta1, sta2 from ta_list where sta1=10 and ini_dt <= date_sub(curdate(),interval 15 day) and (tid % 2)=0 order by crt, spt, sn1, sn2";     //예정-TID 짝수
                    break;

                default:
                    sql = "select tid, spt, sn1, sn2, pn, bid_dt, end_dt, sta1, sta2 from ta_list where sta1=13 order by crt, spt, sn1, sn2, pn";   //미진행
                    break;
            }

            DataTable dtL = db.ExeDt(sql);
            dtL.Columns.Add("clsState");
            dtL.Columns.Add("clsDt");
            dtL.Columns.Add("appeal");
            dtL.Columns.Add("pdNote");
            dtL.Columns.Add("pdState");
            dtL.Columns.Add("giilRslt");
            dtL.Columns.Add("wrFlag");

            totCnt = dtL.Rows.Count;
            atomLog.AddLog(string.Format("확인 대상 - {0}건", totCnt));

            atomLog.AddLog("# 사건내역 비교 시작 #");
            //사건내역 검사(모두-진행, 예정, 미진행)
            foreach (DataRow row in dtL.Rows)
            {
                curCnt++;
                txtPrgs.AppendText(string.Format("\r\n> 사건내역 {0} / {1}", curCnt, totCnt));

                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                sta1 = row["sta1"].ToString();
                if (saNo != prevSaNo)
                {
                    webCnt++;
                    if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                    url = "http://www.courtauction.go.kr/RetrieveRealEstDetailInqSaList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
                    htmlSagun = net.GetHtml(url);
                }
                if (htmlSagun.Contains("14일이 지나지 않았거나") || htmlSagun.Contains("잘못된 번호입니다"))
                {
                    prevSaNo = saNo;
                    continue;
                }
                if (htmlSagun.Contains("JSP/Servlet Error"))
                {
                    prevSaNo = saNo;
                    continue;
                }
                Prc_Sagun(htmlSagun, row);                
                prevSaNo = saNo;
            }
            atomLog.AddLog("# 사건내역 비교 끝 #");

            atomLog.AddLog("# 기일내역 비교 시작 #");
            //기일내역 검사(진행, 미진행)
            curCnt = 0;
            foreach (DataRow row in dtL.Rows)
            {
                curCnt++;
                txtPrgs.AppendText(string.Format("\r\n> 기일내역 {0} / {1}", curCnt, totCnt));

                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                sta1 = row["sta1"].ToString();
                if (sta1 == "10" || sta1 == "12" || row["clsDt"].ToString() != string.Empty || row["appeal"].ToString() != string.Empty)
                {
                    prevSaNo = saNo;
                    continue;
                }
                if (saNo != prevSaNo)
                {
                    webCnt++;
                    if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                    url = "http://www.courtauction.go.kr/RetrieveRealEstSaDetailInqGiilList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
                    htmlGiil = net.GetHtml(url);
                }
                if (htmlGiil.Contains("검색결과가 없습니다"))
                {
                    prevSaNo = saNo;
                    continue;
                }                
                Prc_Giil(htmlGiil, row);
                prevSaNo = saNo;
            }
            atomLog.AddLog("# 기일내역 비교 끝 #");

            DataRow[] rows = dtL.Select("wrFlag=1");
            if (rows.Count() == 0)
            {
                atomLog.AddLog("비교 결과 없음");
                return;
            }
            /*
            List<MySqlParameter> sp = new List<MySqlParameter>();

            atomLog.AddLog("# DB 결과기록 시작 #");
            db.Open();
            foreach (DataRow row in rows)
            {
                tid = row["tid"].ToString();
                sql = "insert into db_tank.tx_rpt set tid=@tid, dvsn=@dvsn, cls_rslt=@cls_rslt, cls_dt=@cls_dt, appeal=@appeal, pd_note=@pd_note, pd_state=@pd_state, state=@state, wdt=curdate()";
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@dvsn", cmpDvsnCd));
                sp.Add(new MySqlParameter("@cls_rslt", row["clsState"].ToString()));
                sp.Add(new MySqlParameter("@cls_dt", row["clsDt"].ToString()));
                sp.Add(new MySqlParameter("@appeal", row["appeal"].ToString()));
                sp.Add(new MySqlParameter("@pd_note", row["pdNote"].ToString()));
                sp.Add(new MySqlParameter("@pd_state", row["pdState"].ToString()));
                sp.Add(new MySqlParameter("@state", row["giilRslt"].ToString()));
                db.ExeQry(sql, sp);
                sp.Clear();
            }
            db.Close();
            atomLog.AddLog(string.Format("# DB 결과기록 끝 # - {0}건 검출", rows.Count()));
            */
            atomLog.AddLog(string.Format("▶▶▶ {0}건 검출", rows.Count()));
            atomLog.AddLog("실행 완료", 1);
        }

        /// <summary>
        /// 사건내역 확인
        /// </summary>
        /// <param name="html"></param>
        /// <param name="row"></param>
        /// <returns></returns>
        private void Prc_Sagun(string html, DataRow row)
        {
            string pn, sta1, clsState, clsDt, appeal, sql;
            string pdNo, pdNote = "", pdState = "";

            pn = row["pn"].ToString();
            if (pn == "0") pn = "1";
            sta1 = row["sta1"].ToString();

            List<MySqlParameter> sp = new List<MySqlParameter>();

            HAPDoc doc = new HAPDoc();
            doc.LoadHtml(html);
            HtmlNode ndTbl = doc.DocumentNode.SelectSingleNode("//table[@summary='사건기본내역 표']");
            if (ndTbl == null) return;

            clsState = ndTbl.SelectSingleNode(".//th[text()='종국결과']").SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
            clsDt = ndTbl.SelectSingleNode(".//th[text()='종국일자']").SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
            appeal = ndTbl.SelectSingleNode(".//th[text()='사건항고/정지여부']").SelectSingleNode("following-sibling::*[1]").InnerText.Trim();            
            if (appeal != string.Empty)
            {
                if (Regex.IsMatch(appeal, @"취하|기각|각하|배당종결|기타", rxOptM) == false) appeal = string.Empty;
            }
            if (clsState == "미종국") clsState = string.Empty;

            HtmlNodeCollection ncTbl = doc.DocumentNode.SelectNodes("//table[@summary='물건내역 표']");
            //if (ncTbl == null || doc.DocumentNode.InnerText.Contains("등록된 물건내역이 없습니다")) return;
            //2021-05-27 -> 물건내역이 없으면서 종국인 경우
            if (ncTbl == null || doc.DocumentNode.InnerText.Contains("등록된 물건내역이 없습니다"))
            {
                if (clsState != string.Empty && clsDt != string.Empty)
                {
                    db.Open();
                    sql = "insert into db_tank.tx_rpt set tid=@tid, dvsn=@dvsn, cls_rslt=@cls_rslt, cls_dt=@cls_dt, appeal=@appeal, pd_note=@pd_note, pd_state=@pd_state, state=@state, wdt=curdate()";
                    sp.Add(new MySqlParameter("@tid", row["tid"].ToString()));
                    sp.Add(new MySqlParameter("@dvsn", cmpDvsnCd));
                    sp.Add(new MySqlParameter("@cls_rslt", clsState));
                    sp.Add(new MySqlParameter("@cls_dt", clsDt.Replace(".", "-")));
                    sp.Add(new MySqlParameter("@appeal", string.Empty));
                    sp.Add(new MySqlParameter("@pd_note", string.Empty));
                    sp.Add(new MySqlParameter("@pd_state", string.Empty));
                    sp.Add(new MySqlParameter("@state", string.Empty));
                    db.ExeQry(sql, sp);
                    sp.Clear();
                    db.Close();
                }

                return;
            }

            foreach (HtmlNode tbl in ncTbl)
            {
                if (tbl.InnerText.Contains("검색결과가 없습니다")) break;

                pdNo = tbl.SelectSingleNode(".//th[text()='물건번호']").SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                if (pdNo == pn)
                {
                    HtmlNodeCollection ncNote = tbl.SelectNodes(".//th[text()='비고']");
                    if (ncNote != null)
                    {
                        foreach (HtmlNode ndNote in ncNote)
                        {
                            pdNote = ndNote.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                            if (pdNote != "미종국") break; 
                        }
                    }
                    if (pdNote == "미종국") pdNote = string.Empty;

                    if (sta1 == "10" || sta1 == "13")   //예정 또는 미진행 물건
                    {
                        pdState = tbl.SelectSingleNode(".//th[text()='물건상태']").SelectSingleNode("following-sibling::*[1]").InnerHtml.Trim();
                        Match match = Regex.Match(pdState, @"<b>(대급납부|매각)</b>", rxOptM);
                        pdState = (match.Success) ? match.Groups[1].Value : string.Empty;
                    }
                    break;
                }
            }

            if (clsState != string.Empty || appeal != string.Empty || pdNote != string.Empty || pdState != string.Empty)
            {
                row["clsState"] = clsState;
                row["clsDt"] = clsDt.Replace(".", "-");
                row["appeal"] = appeal;
                row["pdNote"] = pdNote;
                row["pdState"] = pdState;
                row["wrFlag"] = 1;
            }
                        
            if (row["wrFlag"].ToString() == "1")
            {
                db.Open();
                sql = "insert into db_tank.tx_rpt set tid=@tid, dvsn=@dvsn, cls_rslt=@cls_rslt, cls_dt=@cls_dt, appeal=@appeal, pd_note=@pd_note, pd_state=@pd_state, state=@state, wdt=curdate()";
                sp.Add(new MySqlParameter("@tid", row["tid"].ToString()));
                sp.Add(new MySqlParameter("@dvsn", cmpDvsnCd));
                sp.Add(new MySqlParameter("@cls_rslt", row["clsState"].ToString()));
                sp.Add(new MySqlParameter("@cls_dt", row["clsDt"].ToString()));
                sp.Add(new MySqlParameter("@appeal", row["appeal"].ToString()));
                sp.Add(new MySqlParameter("@pd_note", row["pdNote"].ToString()));
                sp.Add(new MySqlParameter("@pd_state", row["pdState"].ToString()));
                sp.Add(new MySqlParameter("@state", row["giilRslt"].ToString()));
                db.ExeQry(sql, sp);
                sp.Clear();
                db.Close();
            }
        }

        /// <summary>
        /// 기일내역 확인
        /// </summary>
        /// <param name="htmlGiil"></param>
        /// <param name="row"></param>
        private void Prc_Giil(string html, DataRow row)
        {
            string pn, sta1, sta2, sql;
            string pdNo = "", lastRslt = "", bidDt, dtDvsn, bidRslt = "";
            bool find = false;

            pn = row["pn"].ToString();
            if (pn == "0") pn = "1";
            sta1 = row["sta1"].ToString();
            sta2 = row["sta2"].ToString();

            HAPDoc doc = new HAPDoc();
            doc.LoadHtml(html);
            HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='기일내역 표']/tbody/tr[@class='Ltbl_list_lvl0' or @class='Ltbl_list_lvl1']");
            if (ncTr == null) return;

            foreach (HtmlNode ndTr in ncTr)
            {
                HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                if (ncTd.Count == 7)
                {
                    if (ncTd[0].InnerText.Trim() == string.Empty) continue;

                    if (ncTd[0].FirstChild != null)
                    {
                        pdNo = ncTd[0].FirstChild.InnerText.Trim();
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

                if (pn == pdNo)
                {
                    lastRslt = bidRslt;
                    find = true;
                }

                if (find && pn != pdNo) break;
            }

            if (sta1 == "11")
            {
                if (Regex.IsMatch(lastRslt, @"변경|납부"))
                {
                    row["giilRslt"] = lastRslt;
                    row["wrFlag"] = 1;
                }
            }
            else
            {
                if (lastRslt == string.Empty)
                {
                    if (find == true)
                    {
                        row["giilRslt"] = "공란";
                        row["wrFlag"] = 1;
                    }
                    else
                    {
                        row["giilRslt"] = "X";
                        row["wrFlag"] = 1;
                    }
                }
                else
                {
                    if (sta2 == "1314" && lastRslt != "최고가매각불허가결정")
                    {
                        row["giilRslt"] = lastRslt;
                        row["wrFlag"] = 1;
                    }
                    if (lastRslt.Contains("납부") && row["giilRslt"].ToString() == string.Empty)
                    {
                        row["giilRslt"] = lastRslt;
                        row["wrFlag"] = 1;
                    }
                }
            }

            List<MySqlParameter> sp = new List<MySqlParameter>();
            if (row["wrFlag"].ToString() == "1")
            {
                db.Open();
                sql = "insert into db_tank.tx_rpt set tid=@tid, dvsn=@dvsn, cls_rslt=@cls_rslt, cls_dt=@cls_dt, appeal=@appeal, pd_note=@pd_note, pd_state=@pd_state, state=@state, wdt=curdate()";
                sp.Add(new MySqlParameter("@tid", row["tid"].ToString()));
                sp.Add(new MySqlParameter("@dvsn", cmpDvsnCd));
                sp.Add(new MySqlParameter("@cls_rslt", row["clsState"].ToString()));
                sp.Add(new MySqlParameter("@cls_dt", row["clsDt"].ToString()));
                sp.Add(new MySqlParameter("@appeal", row["appeal"].ToString()));
                sp.Add(new MySqlParameter("@pd_note", row["pdNote"].ToString()));
                sp.Add(new MySqlParameter("@pd_state", row["pdState"].ToString()));
                sp.Add(new MySqlParameter("@state", row["giilRslt"].ToString()));
                db.ExeQry(sql, sp);
                sp.Clear();
                db.Close();
            }
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //atomLog.AddLog("실행 완료", 1);
            bgwork.Dispose();

            this.Dispose();
            this.Close();
        }
    }
}
