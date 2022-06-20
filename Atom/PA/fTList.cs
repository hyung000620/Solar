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
using System.IO;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace Atom.PA
{
    public partial class fTList : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();        
        ApiUtil api = new ApiUtil();        
        AtomLog atomLog = new AtomLog(210);     //로그 생성

        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 1500, webCnt = 0;

        int udtCnt = 0, newCnt = 0;     //금일 신규 물건수(신건, 본물건 전환)

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        HAPDoc doc = new HAPDoc();
        List<MySqlParameter> sp = new List<MySqlParameter>();

        string toDay = $"{DateTime.Now:yyyyMMdd}";

        public fTList()
        {
            InitializeComponent();
            this.Shown += FTList_Shown;
        }

        private void FTList_Shown(object sender, EventArgs e)
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

        /// <summary>
        /// 화면에 진행상태 표시
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="depth"></param>
        private void prcView(string msg, int depth = 0)
        {
            if (depth == 0) msg = string.Format("\r\n##### {0} #####\r\n", msg);
            else if (depth == 1) msg = string.Format("> {0}", msg);
            else if (depth == 2) msg = string.Format(">> {0}", msg);
            else if (depth == 3) msg = string.Format(">>> {0}", msg);
            else if (depth == 4) msg = string.Format(">>>> {0}", msg);
            else if (depth == 5) msg = string.Format(">>>>> {0}", msg);

            txtProgrs.AppendText("\r\n" + msg);
        }

        private void BaseDtInit()
        {
            //
        }

        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            //우리자산신탁
            atomLog.AddLog("▼ 우리-10");
            prcView("▼ 우리-10");
            Prc_wooriat(10);
            
            //교보자산신탁
            atomLog.AddLog("▼ 교보-11");
            prcView("▼ 교보-11");
            Prc_kyobotrust(11);

            //무궁화신탁
            atomLog.AddLog("▼ 무궁화-12");
            prcView("▼ 무궁화-12");
            Prc_mgtrust(12);

            //KB부동산신탁
            atomLog.AddLog("▼ KB-13");
            prcView("▼ KB-13");
            Prc_kbret(13);

            //한국자산신탁
            atomLog.AddLog("▼ 한국-14");
            prcView("▼ 한국-14");
            Prc_kait(14);

            //코리아신탁
            atomLog.AddLog("▼ 코리아-15");
            prcView("▼ 코리아-15");
            Prc_ktrust(15);

            //대한토지신탁
            atomLog.AddLog("▼ 대한토지-16");
            prcView("▼ 대한토지-16");
            Prc_reitpia(16);

            //아시아신탁
            atomLog.AddLog("▼ 아시아-17");
            prcView("▼ 아시아-17");
            Prc_asiatrust(17);

            //하나자산신탁
            atomLog.AddLog("▼ 하나-19");
            prcView("▼ 하나-19");
            Prc_hanatrust(19);

            //신영부동산신탁
            atomLog.AddLog("▼ 신영-21");
            prcView("▼ 신영-21");
            Prc_shinyoungret(21);

            //대신자산신탁
            atomLog.AddLog("▼ 대신-22");
            prcView("▼ 대신-22");
            Prc_daishin(22);
            
            //한국투자부동산신탁-신설 2022/03/30 (보류)
            atomLog.AddLog("▼ 한국투자-23");
            prcView("▼ 한국투자-23");
            Prc_kitrust(23);
            
            //Prc_koreit(18);     //한국토지신탁
            //Prc_koramco(20);    //코람코자산신탁
                        
            //예금보험공사
            atomLog.AddLog("▼ 예보공매");
            prcView("▼ 예보공매");
            Prc_bank();

            atomLog.AddLog("▼ 주소 코드 처리");
            prcView("▼ 주소 코드 처리");
            Prc_AdrsCd("td_list");  //신탁
            Prc_AdrsCd("te_list");  //예보
            
            atomLog.AddLog("실행 완료", 1);
        }

        /// <summary>
        /// 주소 코드 처리
        /// </summary>
        private void Prc_AdrsCd(string tbl)
        {
            string idx, sql;
            string adrs, sidoCd = "", gugunCd = "", dongCd = "", riCd = "", hCd = "", pnu = "", zoneNo = "", x = "", y = "", csCd = "", siguCd = "", mt = "";
            int totCnt = 0, curCnt = 0;

            IDictionary<string, string> dict = new Dictionary<string, string>();
            List<MySqlParameter> sp = new List<MySqlParameter>();

            //관할법원-시/도코드
            Dictionary<string, string> dicCS = new Dictionary<string, string>();            
            sql = "select spt_cd, sigu_cd from ta_cd_cs";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                dicCS.Add(dr["spt_cd"].ToString(), dr["sigu_cd"].ToString());
            }
            dr.Close();
            dr.Dispose();
            db.Close();

            sql = "select * from " + tbl + " where si_cd=0 and wdt=curdate() order by idx";
            DataTable dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                prcView($"{curCnt} / {totCnt}", 1);

                idx = row["idx"].ToString();
                adrs = row["adrs"].ToString();

                dict.Clear();
                dict = api.DaumSrchAdrs(adrs);
                sidoCd = dict["sidoCd"];                
                gugunCd = dict["gugunCd"];
                dongCd = dict["dongCd"];
                riCd = dict["riCd"];
                hCd = dict["hCd"];
                pnu = dict["pnu"];
                zoneNo = dict["zoneNo"];
                x = dict["x"];
                y = dict["y"];
                mt = (dict["totalCnt"] == string.Empty || dict["totalCnt"] == "0") ? "0" : dict["mt"];
                if (sidoCd == "")
                {
                    adrs = Regex.Replace(adrs, @"\[\w+\]|외[ ]*\d+.*", string.Empty, rxOptM).Trim();
                    AdrsParser parser = new AdrsParser(adrs);
                    dict = api.DaumSrchAdrs(parser.AdrsM);
                    sidoCd = dict["sidoCd"];                    
                    gugunCd = dict["gugunCd"];
                    dongCd = dict["dongCd"];
                    riCd = dict["riCd"];
                    hCd = dict["hCd"];
                    pnu = dict["pnu"];
                    zoneNo = dict["zoneNo"];
                    x = dict["x"];
                    y = dict["y"];
                    mt = (dict["totalCnt"] == string.Empty || dict["totalCnt"] == "0") ? "0" : dict["mt"];
                }

                csCd = "";
                siguCd = string.Format("{0}{1}", sidoCd, gugunCd);
                if (siguCd.Length == 5)
                {
                    foreach (KeyValuePair<string, string> kv in dicCS)
                    {
                        if (kv.Value.Contains(siguCd))
                        {
                            csCd = kv.Key;
                            break;
                        }
                    }
                }

                sql = "update " + tbl + " set si_cd=@si_cd, gu_cd=@gu_cd, dn_cd=@dn_cd, ri_cd=@ri_cd, hj_cd=@hj_cd, pnu=@pnu, cs_cd=@cs_cd, x=@x, y=@y, zone_no=@zone_no where idx=" + idx;
                sp.Add(new MySqlParameter("@si_cd", sidoCd));
                sp.Add(new MySqlParameter("@gu_cd", gugunCd));
                sp.Add(new MySqlParameter("@dn_cd", dongCd));
                sp.Add(new MySqlParameter("@ri_cd", riCd));
                sp.Add(new MySqlParameter("@x", x));
                sp.Add(new MySqlParameter("@y", y));
                sp.Add(new MySqlParameter("@hj_cd", hCd));
                sp.Add(new MySqlParameter("@pnu", pnu));
                sp.Add(new MySqlParameter("@zone_no", zoneNo));
                sp.Add(new MySqlParameter("@cs_cd", csCd));
                db.Open();
                db.ExeQry(sql, sp);
                sp.Clear();
                db.Close();
            }
        }

        /// <summary>
        /// 예금보험공사
        /// </summary>
        private void Prc_bank()
        {
            string url0, url, html, sql, cvp;
            string refIdx, brptType, adrs, ctgr, state, apslAmt, minbAmt, orgNm, phone;
            decimal i, pgCnt, nCnt = 0, uCnt = 0;

            url0 = "https://www.kdic.or.kr/k-assets/frt/biz/bankrupt/selectBankruptList.do";
            html = net.GetHtml(url0, Encoding.UTF8);
            if (!decimal.TryParse(Regex.Match(html, @"<a href.*pageIndex=(\d+).*마지막", rxOptM).Groups[1].Value, out pgCnt))
            {
                return;
            }
            //MessageBox.Show($"{pgCnt}");
            for (i = 1; i <= pgCnt; i++)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                prcView($"{i} / {pgCnt}", 1);

                if (i > 1)
                {
                    url = $"{url0}?pageIndex={i}";
                    html = net.GetHtml(url, Encoding.UTF8);
                }
                doc.LoadHtml(html);
                HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@class='bbs_list']/tbody/tr");
                if (ncTr == null) continue;

                db.Open();
                foreach (HtmlNode ndTr in ncTr)
                {
                    try
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        HtmlNode ndA = ncTd[2].SelectSingleNode("./a");
                        refIdx = Regex.Match(ndA.OuterHtml, @"fn_view\('([\d\-]+)',[ ]*'(\w+)'\)", rxOptM).Groups[1].Value;
                        brptType = Regex.Match(ndA.OuterHtml, @"fn_view\('([\d\-]+)',[ ]*'(\w+)'\)", rxOptM).Groups[2].Value;
                        adrs = ndA.SelectNodes("./text()")[0].InnerText.Replace("&nbsp;", string.Empty).Replace("  ", " ").Trim();
                        ctgr = ncTd[1].InnerText.Trim();
                        apslAmt = ncTd[3].InnerText.Replace(",", string.Empty).Trim();
                        minbAmt = ncTd[4].InnerText.Replace(",", string.Empty).Trim();
                        apslAmt = Regex.Match(apslAmt, @"\d+").Value;
                        minbAmt = Regex.Match(minbAmt, @"\d+").Value;
                        apslAmt = (apslAmt == string.Empty) ? "0" : (Convert.ToDecimal(apslAmt) * 1000).ToString();
                        minbAmt = (minbAmt == string.Empty) ? "0" : (Convert.ToDecimal(minbAmt) * 1000).ToString();
                        state = ncTd[5].InnerText.Trim();
                        orgNm = ncTd[6].SelectNodes("./text()")[0].InnerText.Trim();
                        phone = ncTd[6].SelectNodes("./text()")[1].InnerText.Trim();
                        //MessageBox.Show($"{refIdx}\n{adrs}\n{ctgr}\n{state}\n{apslAmt}\n{minbAmt}\n{orgNm}\n{phone}");
                        
                        cvp = "org_nm=@org_nm, ref_idx=@ref_idx, brpt_type=@brpt_type, adrs=@adrs, ctgr=@ctgr, apsl_amt=@apsl_amt, minb_amt=@minb_amt, state=@state, phone=@phone";
                        sql = "insert into te_list set " + cvp + ", wdt=curdate() ON DUPLICATE KEY UPDATE " + cvp;
                        sp.Add(new MySqlParameter("@org_nm", orgNm));
                        sp.Add(new MySqlParameter("@ref_idx", refIdx));
                        sp.Add(new MySqlParameter("@brpt_type", brptType));
                        sp.Add(new MySqlParameter("@adrs", adrs));
                        sp.Add(new MySqlParameter("@ctgr", ctgr));
                        sp.Add(new MySqlParameter("@apsl_amt", apslAmt));
                        sp.Add(new MySqlParameter("@minb_amt", minbAmt));
                        sp.Add(new MySqlParameter("@state", state));
                        sp.Add(new MySqlParameter("@phone", phone));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                        //if (rgstDt == toDay) nCnt++;
                        //else uCnt++;
                        uCnt++;
                    }
                    catch (Exception ex)
                    {
                        atomLog.AddLog(ex.Message);
                        continue;
                    }
                }
                db.Close();
            }

            atomLog.AddLog($" > 처리-{uCnt}");
        }

        /// <summary>
        /// 우리자산신탁
        /// </summary>
        /// <param name="v"></param>
        private void Prc_wooriat(int coCd)
        {
            string url0, url, html, sql, cvp;
            string refIdx, adrs, ctgr, rgstDt, state;
            decimal i, pgCnt, nCnt = 0, uCnt = 0;

            url0 = "http://www.wooriat.com/item/vendue/list";
            html = net.GetHtml(url0, Encoding.UTF8);
            if (!decimal.TryParse(Regex.Match(html, @"totalPage[ ]*=[ ]*Number\('(\d+)'\)", rxOptM).Groups[1].Value, out pgCnt))
            {
                //Error
                return;
            }
            //MessageBox.Show($"{pgCnt}");
            for (i = 1; i <= pgCnt; i++)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                prcView($"{i} / {pgCnt}", 1);

                if (i > 1)
                {
                    url = $"{url0}?page={i}";
                    html = net.GetHtml(url, Encoding.UTF8);
                }
                doc.LoadHtml(html);
                HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//div[@class='board_list_type1']/table/tbody/tr");
                if (ncTr == null) continue;

                db.Open();
                foreach (HtmlNode ndTr in ncTr)
                {
                    try
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        HtmlNode ndA = ncTd[2].SelectSingleNode("./a");
                        refIdx = Regex.Match(ndA.OuterHtml, @"/detail/(\d+)", rxOptM).Groups[1].Value;
                        adrs = ndA.SelectNodes("./text()")[0].InnerText.Trim();
                        ctgr = string.Empty;
                        rgstDt = ncTd[4].InnerText.Trim();
                        rgstDt = Regex.Replace(rgstDt, @"[^\d]", string.Empty, rxOptM);
                        state = ncTd[1].InnerText.Trim();
                        //MessageBox.Show($"{refIdx}\n{adrs}\n{ctgr}\n{rgstDt}\n{state}");
                        cvp = "co_cd=@co_cd, ref_idx=@ref_idx, adrs=@adrs, ctgr=@ctgr, state=@state, rdt=@rdt";
                        sql = "insert into td_list set " + cvp + ", wdt=curdate() ON DUPLICATE KEY UPDATE " + cvp;
                        sp.Add(new MySqlParameter("@co_cd", coCd));
                        sp.Add(new MySqlParameter("@ref_idx", refIdx));
                        sp.Add(new MySqlParameter("@adrs", adrs));
                        sp.Add(new MySqlParameter("@ctgr", ctgr));
                        sp.Add(new MySqlParameter("@state", state));
                        sp.Add(new MySqlParameter("@rdt", rgstDt));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                        if (rgstDt == toDay) nCnt++;
                        else uCnt++;
                    }
                    catch(Exception ex)
                    {
                        atomLog.AddLog(ex.Message);
                        continue;
                    }
                }
                db.Close();
            }

            atomLog.AddLog($" > 신규-{nCnt}, 갱신-{uCnt}");
        }

        /// <summary>
        /// 교보자산신탁
        /// </summary>
        /// <param name="v"></param>
        private void Prc_kyobotrust(int coCd)
        {
            string url0, url, html, sql, cvp;
            string refIdx, adrs, ctgr, rgstDt, state;
            decimal i, pgCnt, nCnt = 0, uCnt = 0;

            url0 = "https://www.kyobotrust.co.kr/board/public_sale_list.asp";
            html = net.GetHtml(url0, Encoding.UTF8);
            if (!decimal.TryParse(Regex.Match(html, @"<a href.*p=(\d+).*direction last", rxOptM).Groups[1].Value, out pgCnt))
            {
                //Error
                return;
            }
            //MessageBox.Show($"{pgCnt}");
            for (i = 1; i <= pgCnt; i++)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                prcView($"{i} / {pgCnt}", 1);

                if (i > 1)
                {
                    url = $"{url0}?p={i}";
                    html = net.GetHtml(url, Encoding.UTF8);
                }
                doc.LoadHtml(html);
                HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@class='tbl_st01 publicSaleList']/tbody/tr");
                if (ncTr == null) continue;

                db.Open();
                foreach (HtmlNode ndTr in ncTr)
                {
                    try
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        HtmlNode ndA = ncTd[2].SelectSingleNode("./a");
                        refIdx = Regex.Match(ndA.OuterHtml, @"INTSEQNO=(\d+)", rxOptM).Groups[1].Value;
                        if (ndA.SelectNodes("./text()") == null) continue;
                        adrs = ndA.SelectNodes("./text()")[0].InnerText.Trim();
                        ctgr = string.Empty;
                        rgstDt = ncTd[4].InnerText.Trim();
                        rgstDt = Regex.Replace(rgstDt, @"[^\d]", string.Empty, rxOptM);
                        state = ncTd[1].InnerText.Trim();
                        //MessageBox.Show($"{refIdx}\n{adrs}\n{ctgr}\n{rgstDt}\n{state}");
                        cvp = "co_cd=@co_cd, ref_idx=@ref_idx, adrs=@adrs, ctgr=@ctgr, state=@state, rdt=@rdt";
                        sql = "insert into td_list set " + cvp + ", wdt=curdate() ON DUPLICATE KEY UPDATE " + cvp;
                        sp.Add(new MySqlParameter("@co_cd", coCd));
                        sp.Add(new MySqlParameter("@ref_idx", refIdx));
                        sp.Add(new MySqlParameter("@adrs", adrs));
                        sp.Add(new MySqlParameter("@ctgr", ctgr));
                        sp.Add(new MySqlParameter("@state", state));
                        sp.Add(new MySqlParameter("@rdt", rgstDt));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                        if (rgstDt == toDay) nCnt++;
                        else uCnt++;
                    }                    
                    catch (Exception ex)
                    {
                        atomLog.AddLog(ex.Message);
                        continue;
                    }
                }
                db.Close();
            }

            atomLog.AddLog($" > 신규-{nCnt}, 갱신-{uCnt}");
        }

        /// <summary>
        /// 무궁화신탁
        /// </summary>
        /// <param name="coCd"></param>
        private void Prc_mgtrust(int coCd)
        {
            string url0, url, html, sql, cvp;
            string refIdx, adrs, ctgr, rgstDt, state;
            decimal i, pgCnt, nCnt = 0, uCnt = 0;

            url0 = "http://www.mgtrust.co.kr/auction/disposal/list.do";
            html = net.GetHtml(url0, Encoding.UTF8);
            if (!decimal.TryParse(Regex.Match(html, @"page=(\d+)"">Fast next", rxOptM).Groups[1].Value, out pgCnt))
            {
                //Error
                return;
            }
            //MessageBox.Show($"{pgCnt}");
            for (i = 1; i <= pgCnt; i++)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                prcView($"{i} / {pgCnt}", 1);

                if (i > 1)
                {
                    url = $"{url0}?page={i}";
                    html = net.GetHtml(url, Encoding.UTF8);
                }
                doc.LoadHtml(html);
                HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='매각/공매물건 게시판']/tbody/tr");
                db.Open();
                if (ncTr == null) continue;

                foreach (HtmlNode ndTr in ncTr)
                {
                    try
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        HtmlNode ndA = ncTd[2].SelectSingleNode("./a");
                        refIdx = Regex.Match(ndA.OuterHtml, @"disposal/(\d+)/show", rxOptM).Groups[1].Value;
                        adrs = ndA.SelectNodes("./text()")[0].InnerText.Trim();
                        ctgr = string.Empty;
                        rgstDt = ncTd[4].InnerText.Trim();
                        rgstDt = Regex.Replace(rgstDt, @"[^\d]", string.Empty, rxOptM);
                        state = ncTd[1].InnerText.Trim();
                        //MessageBox.Show($"{refIdx}\n{adrs}\n{ctgr}\n{rgstDt}\n{state}");
                        cvp = "co_cd=@co_cd, ref_idx=@ref_idx, adrs=@adrs, ctgr=@ctgr, state=@state, rdt=@rdt";
                        sql = "insert into td_list set " + cvp + ", wdt=curdate() ON DUPLICATE KEY UPDATE " + cvp;
                        sp.Add(new MySqlParameter("@co_cd", coCd));
                        sp.Add(new MySqlParameter("@ref_idx", refIdx));
                        sp.Add(new MySqlParameter("@adrs", adrs));
                        sp.Add(new MySqlParameter("@ctgr", ctgr));
                        sp.Add(new MySqlParameter("@state", state));
                        sp.Add(new MySqlParameter("@rdt", rgstDt));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                        if (rgstDt == toDay) nCnt++;
                        else uCnt++;
                    }
                    catch (Exception ex)
                    {
                        atomLog.AddLog(ex.Message);
                        continue;
                    }
                }
                db.Close();
            }

            atomLog.AddLog($" > 신규-{nCnt}, 갱신-{uCnt}");
        }

        /// <summary>
        /// KB부동산신탁
        /// </summary>
        private void Prc_kbret(int coCd)
        {
            string url0, url, html, sql, cvp;
            string refIdx, adrs, ctgr, rgstDt, state;
            decimal i, pgCnt, nCnt = 0, uCnt = 0;

            url0 = "https://kbret.co.kr/sale/publicsale.do";
            html = net.GetHtml(url0, Encoding.UTF8);
            if (!decimal.TryParse(Regex.Match(html, @"javascript:goPage\((\d+)\).*마지막", rxOptM).Groups[1].Value, out pgCnt))
            {
                //Error
                return;
            }
            //MessageBox.Show($"{pgCnt}");
            for (i = 1; i <= pgCnt; i++)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                prcView($"{i} / {pgCnt}", 1);

                if (i > 1)
                {
                    url = $"{url0}?page2={i}&smode=location";
                    html = net.GetHtml(url, Encoding.UTF8);
                }
                doc.LoadHtml(html);
                HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='입찰경매 공개매각 물건 정보']/tbody/tr");
                if (ncTr == null) continue;

                db.Open();
                foreach (HtmlNode ndTr in ncTr)
                {
                    try
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        HtmlNode ndA = ncTd[1].SelectSingleNode("./a");
                        refIdx = Regex.Match(ndA.OuterHtml, @"idx=(\d+)", rxOptM).Groups[1].Value;
                        adrs = ndA.SelectNodes("./text()")[0].InnerText.Trim();
                        ctgr = ncTd[2].InnerText.Trim();
                        rgstDt = ncTd[3].InnerText.Trim();
                        rgstDt = Regex.Replace(rgstDt, @"[^\d]", string.Empty, rxOptM);
                        state = ncTd[4].InnerText.Trim();
                        //MessageBox.Show($"{refIdx}\n{adrs}\n{ctgr}\n{rgstDt}\n{state}");
                        cvp = "co_cd=@co_cd, ref_idx=@ref_idx, adrs=@adrs, ctgr=@ctgr, state=@state, rdt=@rdt";
                        sql = "insert into td_list set " + cvp + ", wdt=curdate() ON DUPLICATE KEY UPDATE " + cvp;
                        sp.Add(new MySqlParameter("@co_cd", coCd));
                        sp.Add(new MySqlParameter("@ref_idx", refIdx));
                        sp.Add(new MySqlParameter("@adrs", adrs));
                        sp.Add(new MySqlParameter("@ctgr", ctgr));
                        sp.Add(new MySqlParameter("@state", state));
                        sp.Add(new MySqlParameter("@rdt", rgstDt));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                        if (rgstDt == toDay) nCnt++;
                        else uCnt++;
                    }
                    catch (Exception ex)
                    {
                        atomLog.AddLog(ex.Message);
                        continue;
                    }
                }
                db.Close();
            }

            atomLog.AddLog($" > 신규-{nCnt}, 갱신-{uCnt}");
        }

        /// <summary>
        /// 한국자산신탁
        /// </summary>
        /// <param name="v"></param>
        private void Prc_kait(int coCd)
        {
            string url0, url, html, sql, cvp;
            string refIdx, adrs, ctgr, rgstDt, state;
            decimal i, pgCnt, nCnt = 0, uCnt = 0;

            url0 = "http://www.kait.com/public_sale/01.php";
            html = net.GetHtml(url0, Encoding.UTF8);
            if (!decimal.TryParse(Regex.Match(html, @"<a href='\?sub_cate=1&page=(\d+)&=&make=&search=&make=&search='  class='last'>", rxOptM).Groups[1].Value, out pgCnt))
            {
                //Error
                return;
            }
            //MessageBox.Show($"{pgCnt}");
            for (i = 1; i <= pgCnt; i++)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                prcView($"{i} / {pgCnt}", 1);

                if (i > 1)
                {
                    url = $"{url0}?page={i}&sub_cate=1";
                    html = net.GetHtml(url, Encoding.UTF8);
                }
                doc.LoadHtml(html);
                HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='번호,제목,작성일,조회수로 이루어진 결산공고']/tbody/tr");
                if (ncTr == null) continue;

                db.Open();
                foreach (HtmlNode ndTr in ncTr)
                {
                    try
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        HtmlNode ndA = ncTd[2].SelectSingleNode("./a");
                        refIdx = Regex.Match(ndA.OuterHtml, @"no=(\d+)", rxOptM).Groups[1].Value;
                        adrs = ndA.SelectNodes("./text()")[0].InnerText.Trim();
                        ctgr = string.Empty;
                        rgstDt = ncTd[3].InnerText.Trim();
                        rgstDt = Regex.Replace(rgstDt, @"[^\d]", string.Empty, rxOptM);
                        state = ncTd[1].InnerText.Trim();
                        //MessageBox.Show($"{refIdx}\n{adrs}\n{ctgr}\n{rgstDt}\n{state}");
                        cvp = "co_cd=@co_cd, ref_idx=@ref_idx, adrs=@adrs, ctgr=@ctgr, state=@state, rdt=@rdt";
                        sql = "insert into td_list set " + cvp + ", wdt=curdate() ON DUPLICATE KEY UPDATE " + cvp;
                        sp.Add(new MySqlParameter("@co_cd", coCd));
                        sp.Add(new MySqlParameter("@ref_idx", refIdx));
                        sp.Add(new MySqlParameter("@adrs", adrs));
                        sp.Add(new MySqlParameter("@ctgr", ctgr));
                        sp.Add(new MySqlParameter("@state", state));
                        sp.Add(new MySqlParameter("@rdt", rgstDt));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                        if (rgstDt == toDay) nCnt++;
                        else uCnt++;
                    }
                    catch (Exception ex)
                    {
                        atomLog.AddLog(ex.Message);
                        continue;
                    }
                }
                db.Close();
            }

            atomLog.AddLog($" > 신규-{nCnt}, 갱신-{uCnt}");
        }

        /// <summary>
        /// 코리아신탁
        /// </summary>
        /// <param name="v"></param>
        private void Prc_ktrust(int coCd)
        {
            string url0, url, html, sql, cvp;
            string refIdx, adrs, ctgr, rgstDt, state;
            decimal i, pgCnt, nCnt = 0, uCnt = 0;

            url0 = "http://www.ktrust.co.kr/?pages=thing&subs=thing02";
            html = net.GetHtml(url0, Encoding.UTF8);
            doc.LoadHtml(html);
            HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//div[@class='thing02_list_wrap']/table/tbody/tr");
            if (ncTr == null)
            {
                //
                return;
            }
            HtmlNode ndLineNo = ncTr[0].SelectNodes("./td")[0];
            if (ndLineNo == null)
            {
                //
                return;
            }
            pgCnt = Math.Ceiling(Convert.ToDecimal(ndLineNo.InnerText.Trim()) / 10);
            //MessageBox.Show($"{pgCnt}");
            for (i = 1; i <= pgCnt; i++)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                prcView($"{i} / {pgCnt}", 1);

                if (i > 1)
                {
                    url = $"{url0}&page={i}";
                    html = net.GetHtml(url, Encoding.UTF8);
                }
                doc.LoadHtml(html);
                ncTr = doc.DocumentNode.SelectNodes("//div[@class='thing02_list_wrap']/table/tbody/tr");
                if (ncTr == null) continue;

                db.Open();
                foreach (HtmlNode ndTr in ncTr)
                {
                    try
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        HtmlNode ndA = ncTd[1].SelectSingleNode("./a");
                        refIdx = Regex.Match(ndA.OuterHtml, @"idx=(\d+)", rxOptM).Groups[1].Value;
                        adrs = ndA.SelectNodes("./text()")[0].InnerText.Trim();
                        ctgr = string.Empty;
                        rgstDt = ncTd[3].InnerText.Trim();
                        rgstDt = Regex.Replace(rgstDt, @"[^\d]", string.Empty, rxOptM);
                        state = ncTd[2].InnerText.Trim();
                        //MessageBox.Show($"{refIdx}\n{adrs}\n{ctgr}\n{rgstDt}\n{state}");
                        cvp = "co_cd=@co_cd, ref_idx=@ref_idx, adrs=@adrs, ctgr=@ctgr, state=@state, rdt=@rdt";
                        sql = "insert into td_list set " + cvp + ", wdt=curdate() ON DUPLICATE KEY UPDATE " + cvp;
                        sp.Add(new MySqlParameter("@co_cd", coCd));
                        sp.Add(new MySqlParameter("@ref_idx", refIdx));
                        sp.Add(new MySqlParameter("@adrs", adrs));
                        sp.Add(new MySqlParameter("@ctgr", ctgr));
                        sp.Add(new MySqlParameter("@state", state));
                        sp.Add(new MySqlParameter("@rdt", rgstDt));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                        if (rgstDt == toDay) nCnt++;
                        else uCnt++;
                    }
                    catch (Exception ex)
                    {
                        atomLog.AddLog(ex.Message);
                        continue;
                    }
                }
                db.Close();
            }

            atomLog.AddLog($" > 신규-{nCnt}, 갱신-{uCnt}");
        }

        /// <summary>
        /// 대한토지신탁
        /// </summary>
        /// <param name="v"></param>
        private void Prc_reitpia(int coCd)
        {
            string url0, url, html, sql, cvp;
            string refIdx, adrs, ctgr, rgstDt, state;
            decimal i, pgCnt, nCnt = 0, uCnt = 0;

            url0 = "http://www.reitpia.com/solid/04.php";
            html = net.GetHtml(url0, Encoding.UTF8);
            doc.LoadHtml(html);
            HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@class='board-list_r mt0']/tbody/tr");
            if (ncTr == null)
            {
                //
                return;
            }
            HtmlNode ndLineNo = ncTr[0].SelectNodes("./td")[0];
            if (ndLineNo == null)
            {
                //
                return;
            }
            pgCnt = Math.Ceiling(Convert.ToDecimal(ndLineNo.InnerText.Trim()) / 10);
            //MessageBox.Show($"{pgCnt}");
            for (i = 1; i <= pgCnt; i++)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                prcView($"{i} / {pgCnt}", 1);

                if (i > 1)
                {
                    url = $"{url0}?page={i}";
                    html = net.GetHtml(url, Encoding.UTF8);
                }
                doc.LoadHtml(html);
                ncTr = doc.DocumentNode.SelectNodes("//table[@class='board-list_r mt0']/tbody/tr");
                if (ncTr == null) continue;

                db.Open();
                foreach (HtmlNode ndTr in ncTr)
                {
                    try
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        HtmlNode ndA = ncTd[2].SelectSingleNode("./a");
                        refIdx = Regex.Match(ndA.OuterHtml, @"no=(\d+)", rxOptM).Groups[1].Value;
                        adrs = ndA.SelectNodes("./text()")[0].InnerText.Trim();
                        ctgr = string.Empty;
                        rgstDt = ncTd[4].InnerText.Trim();
                        rgstDt = Regex.Replace(rgstDt, @"[^\d]", string.Empty, rxOptM);
                        //state = ncTd[1].InnerText.Trim();
                        if (ncTd[1].InnerHtml.Contains("ing01")) state = "진행중";
                        else if (ncTd[1].InnerHtml.Contains("ing02")) state = "수의계약진행중";
                        else if (ncTd[1].InnerHtml.Contains("end")) state = "종료";
                        else state = "";
                        //MessageBox.Show($"{refIdx}\n{adrs}\n{ctgr}\n{rgstDt}\n{state}");
                        cvp = "co_cd=@co_cd, ref_idx=@ref_idx, adrs=@adrs, ctgr=@ctgr, state=@state, rdt=@rdt";
                        sql = "insert into td_list set " + cvp + ", wdt=curdate() ON DUPLICATE KEY UPDATE " + cvp;
                        sp.Add(new MySqlParameter("@co_cd", coCd));
                        sp.Add(new MySqlParameter("@ref_idx", refIdx));
                        sp.Add(new MySqlParameter("@adrs", adrs));
                        sp.Add(new MySqlParameter("@ctgr", ctgr));
                        sp.Add(new MySqlParameter("@state", state));
                        sp.Add(new MySqlParameter("@rdt", rgstDt));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                        if (rgstDt == toDay) nCnt++;
                        else uCnt++;
                    }
                    catch (Exception ex)
                    {
                        atomLog.AddLog(ex.Message);
                        continue;
                    }
                }
                db.Close();
            }

            atomLog.AddLog($" > 신규-{nCnt}, 갱신-{uCnt}");
        }

        /// <summary>
        /// 아시아신탁
        /// </summary>
        /// <param name="coCd"></param>
        private void Prc_asiatrust(int coCd)
        {
            string url0, url, html, sql, cvp;
            string refIdx, adrs, ctgr, rgstDt, state;
            decimal i, pgCnt, nCnt = 0, uCnt = 0;

            url0 = "https://www.asiatrust.co.kr/?pages=bizinfo&subpage=bizinfo_01";
            html = net.GetHtml(url0, Encoding.UTF8);
            doc.LoadHtml(html);
            HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@class='bizarea_tbl3']/tr");
            if (ncTr == null)
            {
                //
                return;
            }
            HtmlNode ndLineNo = ncTr[1].SelectNodes("./td")[0];
            if (ndLineNo == null)
            {
                //
                return;
            }
            pgCnt = Math.Ceiling(Convert.ToDecimal(ndLineNo.InnerText.Trim()) / 10);
            //MessageBox.Show($"{pgCnt}");
            for (i = 1; i <= pgCnt; i++)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                prcView($"{i} / {pgCnt}", 1);

                if (i > 1)
                {
                    url = $"{url0}&page={i}";
                    html = net.GetHtml(url, Encoding.UTF8);
                }
                doc.LoadHtml(html);
                ncTr = doc.DocumentNode.SelectNodes("//table[@class='bizarea_tbl3']/tr");
                if (ncTr == null) continue;

                db.Open();
                foreach (HtmlNode ndTr in ncTr)
                {
                    if (ndTr.InnerHtml.Contains("th")) continue;

                    try
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        HtmlNode ndA = ncTd[1].SelectSingleNode("./a");
                        HtmlNode ndS = ncTd[1].SelectSingleNode("./span");
                        if (ndA == null || ndS == null) continue;
                        refIdx = Regex.Match(ndA.OuterHtml, @"idx=(\d+)", rxOptM).Groups[1].Value;
                        adrs = ndA.SelectNodes("./text()")[0].InnerText.Trim();
                        ctgr = string.Empty;
                        rgstDt = ncTd[2].InnerText.Trim();
                        rgstDt = Regex.Replace(rgstDt, @"[^\d]", string.Empty, rxOptM);
                        state = ndS.InnerText.Trim();
                        //MessageBox.Show($"{refIdx}\n{adrs}\n{ctgr}\n{rgstDt}\n{state}");
                        cvp = "co_cd=@co_cd, ref_idx=@ref_idx, adrs=@adrs, ctgr=@ctgr, state=@state, rdt=@rdt";
                        sql = "insert into td_list set " + cvp + ", wdt=curdate() ON DUPLICATE KEY UPDATE " + cvp;
                        sp.Add(new MySqlParameter("@co_cd", coCd));
                        sp.Add(new MySqlParameter("@ref_idx", refIdx));
                        sp.Add(new MySqlParameter("@adrs", adrs));
                        sp.Add(new MySqlParameter("@ctgr", ctgr));
                        sp.Add(new MySqlParameter("@state", state));
                        sp.Add(new MySqlParameter("@rdt", rgstDt));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                        if (rgstDt == toDay) nCnt++;
                        else uCnt++;
                    }
                    catch (Exception ex)
                    {
                        atomLog.AddLog(ex.Message);
                        continue;
                    }
                }
                db.Close();
            }

            atomLog.AddLog($" > 신규-{nCnt}, 갱신-{uCnt}");
        }

        /// <summary>
        /// 하나자산신탁
        /// </summary>
        /// <param name="v"></param>
        private void Prc_hanatrust(int coCd)
        {
            string url0, url, html, sql, cvp;
            string refIdx, adrs, ctgr, rgstDt, state;
            decimal i, pgCnt, nCnt = 0, uCnt = 0;

            pgCnt = 30;     //총 페이지수를 구할 수 없다.

            url0 = "https://www.hanatrust.com/ko/publicSale/goodsForPublicSale";
            for (i = 1; i <= pgCnt; i++)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                prcView($"{i} / {pgCnt}", 1);

                url = $"{url0}?page={i}";
                html = net.GetHtml(url, Encoding.UTF8);

                doc.LoadHtml(html);
                HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//div[@class='auction-sale__content']/ul/li/a[@class='auction-sale__link']");
                if (ncTr == null) continue;

                db.Open();
                foreach (HtmlNode ndTr in ncTr)
                {
                    try
                    {
                        HtmlNode ndInf = ndTr.SelectSingleNode("./div[@class='auction-sale__infomation']");
                        refIdx = Regex.Match(ndTr.OuterHtml, @"seq=(\d+)", rxOptM).Groups[1].Value;
                        adrs = ndInf.SelectSingleNode(".//p[@class='auction-sale__position']").InnerText.Trim();
                        ctgr = ndInf.SelectSingleNode(".//span[@class='auction-sale__purpose']").InnerText.Trim();
                        rgstDt = ndInf.SelectSingleNode(".//span[@class='auction-sale__date']").InnerText.Trim();
                        rgstDt = Regex.Replace(rgstDt, @"[^\d]", string.Empty, rxOptM);
                        state = ndInf.SelectSingleNode(".//span[@class='auction-sale__status']").InnerText.Trim();
                        //MessageBox.Show($"{refIdx}\n{adrs}\n{ctgr}\n{rgstDt}\n{state}");
                        cvp = "co_cd=@co_cd, ref_idx=@ref_idx, adrs=@adrs, ctgr=@ctgr, state=@state, rdt=@rdt";
                        sql = "insert into td_list set " + cvp + ", wdt=curdate() ON DUPLICATE KEY UPDATE " + cvp;
                        sp.Add(new MySqlParameter("@co_cd", coCd));
                        sp.Add(new MySqlParameter("@ref_idx", refIdx));
                        sp.Add(new MySqlParameter("@adrs", adrs));
                        sp.Add(new MySqlParameter("@ctgr", ctgr));
                        sp.Add(new MySqlParameter("@state", state));
                        sp.Add(new MySqlParameter("@rdt", rgstDt));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                        if (rgstDt == toDay) nCnt++;
                        else uCnt++;
                    }
                    catch (Exception ex)
                    {
                        atomLog.AddLog(ex.Message);
                        continue;
                    }
                }
                db.Close();
            }

            atomLog.AddLog($" > 신규-{nCnt}, 갱신-{uCnt}");
        }

        /// <summary>
        /// 신영부동산신탁
        /// </summary>
        /// <param name="v"></param>
        private void Prc_shinyoungret(int coCd)
        {
            string url0, url, html, sql, cvp;
            string refIdx, adrs, ctgr, rgstDt, state;
            decimal i, pgCnt, nCnt = 0, uCnt = 0;

            url0 = "http://www.shinyoungret.com/?pages=public&subs=sub01";
            html = net.GetHtml(url0, Encoding.UTF8);
            doc.LoadHtml(html);
            HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@class='table table-hover bbs_tbl']/tbody/tr");
            if (ncTr == null)
            {
                //
                return;
            }
            HtmlNode ndLineNo = ncTr[0].SelectNodes("./td")[0];
            if (ndLineNo == null)
            {
                //
                return;
            }
            pgCnt = Math.Ceiling(Convert.ToDecimal(ndLineNo.InnerText.Trim()) / 10);
            //MessageBox.Show($"{pgCnt}");
            for (i = 1; i <= pgCnt; i++)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                prcView($"{i} / {pgCnt}", 1);

                if (i > 1)
                {
                    url = $"{url0}&pageno={i}";
                    html = net.GetHtml(url, Encoding.UTF8);
                }
                doc.LoadHtml(html);
                ncTr = doc.DocumentNode.SelectNodes("//table[@class='table table-hover bbs_tbl']/tbody/tr");
                if (ncTr == null) continue;

                db.Open();
                foreach (HtmlNode ndTr in ncTr)
                {
                    try
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        HtmlNode ndA = ncTd[1].SelectSingleNode("./a");
                        refIdx = Regex.Match(ndA.OuterHtml, @"idx=(\d+)", rxOptM).Groups[1].Value;
                        adrs = ndA.SelectNodes("./text()")[0].InnerText.Trim();
                        ctgr = string.Empty;
                        rgstDt = ncTd[4].InnerText.Trim();
                        rgstDt = Regex.Replace(rgstDt, @"[^\d]", string.Empty, rxOptM);
                        state = ncTd[2].InnerText.Trim();
                        //MessageBox.Show($"{refIdx}\n{adrs}\n{ctgr}\n{rgstDt}\n{state}");
                        cvp = "co_cd=@co_cd, ref_idx=@ref_idx, adrs=@adrs, ctgr=@ctgr, state=@state, rdt=@rdt";
                        sql = "insert into td_list set " + cvp + ", wdt=curdate() ON DUPLICATE KEY UPDATE " + cvp;
                        sp.Add(new MySqlParameter("@co_cd", coCd));
                        sp.Add(new MySqlParameter("@ref_idx", refIdx));
                        sp.Add(new MySqlParameter("@adrs", adrs));
                        sp.Add(new MySqlParameter("@ctgr", ctgr));
                        sp.Add(new MySqlParameter("@state", state));
                        sp.Add(new MySqlParameter("@rdt", rgstDt));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                        if (rgstDt == toDay) nCnt++;
                        else uCnt++;
                    }
                    catch (Exception ex)
                    {
                        atomLog.AddLog(ex.Message);
                        continue;
                    }
                }
                db.Close();
            }

            atomLog.AddLog($" > 신규-{nCnt}, 갱신-{uCnt}");
        }

        /// <summary>
        /// 대신자산신탁
        /// </summary>
        /// <param name="v"></param>
        private void Prc_daishin(int coCd)
        {
            string url0, url, html, sql, cvp;
            string refIdx, adrs, ctgr, rgstDt, state;
            decimal i, pgCnt, nCnt = 0, uCnt = 0;

            url0 = "https://trust.daishin.com/?pages=thing&subs=sub01";
            html = net.GetHtml(url0, Encoding.UTF8);
            doc.LoadHtml(html);
            HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@class='table table-hover bbs_tbl']/tbody/tr");
            if (ncTr == null)
            {
                //
                return;
            }
            HtmlNode ndLineNo = ncTr[0].SelectNodes("./td")[0];
            if (ndLineNo == null)
            {
                //
                return;
            }
            pgCnt = Math.Ceiling(Convert.ToDecimal(ndLineNo.InnerText.Trim()) / 10);
            //MessageBox.Show($"{pgCnt}");
            for (i = 1; i <= pgCnt; i++)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                prcView($"{i} / {pgCnt}", 1);

                if (i > 1)
                {
                    url = $"{url0}&pageno={i}";
                    html = net.GetHtml(url, Encoding.UTF8);
                }
                doc.LoadHtml(html);
                ncTr = doc.DocumentNode.SelectNodes("//table[@class='table table-hover bbs_tbl']/tbody/tr");
                if (ncTr == null) continue;

                db.Open();
                foreach (HtmlNode ndTr in ncTr)
                {
                    try
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        HtmlNode ndA = ncTd[1].SelectSingleNode("./a");
                        HtmlNode ndS = ncTd[1].SelectSingleNode("./span");
                        if (ndA == null || ndS == null) continue;
                        refIdx = Regex.Match(ndA.OuterHtml, @"idx=(\d+)", rxOptM).Groups[1].Value;
                        adrs = ndA.SelectNodes("./text()")[0].InnerText.Trim();
                        ctgr = string.Empty;
                        rgstDt = ncTd[2].InnerText.Trim();
                        rgstDt = Regex.Replace(rgstDt, @"[^\d]", string.Empty, rxOptM);
                        state = ndS.InnerText.Trim();
                        //MessageBox.Show($"{refIdx}\n{adrs}\n{ctgr}\n{rgstDt}\n{state}");
                        cvp = "co_cd=@co_cd, ref_idx=@ref_idx, adrs=@adrs, ctgr=@ctgr, state=@state, rdt=@rdt";
                        sql = "insert into td_list set " + cvp + ", wdt=curdate() ON DUPLICATE KEY UPDATE " + cvp;
                        sp.Add(new MySqlParameter("@co_cd", coCd));
                        sp.Add(new MySqlParameter("@ref_idx", refIdx));
                        sp.Add(new MySqlParameter("@adrs", adrs));
                        sp.Add(new MySqlParameter("@ctgr", ctgr));
                        sp.Add(new MySqlParameter("@state", state));
                        sp.Add(new MySqlParameter("@rdt", rgstDt));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                        if (rgstDt == toDay) nCnt++;
                        else uCnt++;
                    }
                    catch (Exception ex)
                    {
                        atomLog.AddLog(ex.Message);
                        continue;
                    }
                }
                db.Close();
            }

            atomLog.AddLog($" > 신규-{nCnt}, 갱신-{uCnt}");
        }

        /// <summary>
        /// 한국투자부동산신탁-2022/03/30 추가, 단일물건밖에 없어 현재 페이징 알 수 없음
        /// </summary>
        /// <param name="v"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void Prc_kitrust(int coCd)
        {
            string url, jsonData, sql, cvp;
            string refIdx, adrs, ctgr, rgstDt, state;
            decimal i, pgCnt, nCnt = 0, uCnt = 0;

            url = "http://www.kitrust.kr/board/userPublicSaleContent.do";
            jsonData = net.GetHtml(url, Encoding.UTF8);
            if (jsonData.Contains("요청하신 페이지를 찾을 수 없거나") || jsonData.Contains("작업 시간이 초과")) return;

            dynamic x = JsonConvert.DeserializeObject(jsonData);            
            if (x["Expired"] == null) return;

            var jsExpired = x["Expired"].ToString();

            if (!string.IsNullOrEmpty(jsExpired))
            {
                JArray jaExpired = JArray.Parse(jsExpired);
                db.Open();
                foreach (JObject item in jaExpired)
                {
                    try
                    {
                        refIdx = item["PUREFITEM"]?.ToString() ?? "";
                        adrs = item["SUBJECT"]?.ToString() ?? "";
                        ctgr = string.Empty;
                        rgstDt = item["ADDEDON"]?.ToString() ?? "";
                        rgstDt = Regex.Replace(rgstDt, @"[^\d]", string.Empty, rxOptM);
                        state = item["STATUS_DETAIL"]?.ToString() ?? "";
                        cvp = "co_cd=@co_cd, ref_idx=@ref_idx, adrs=@adrs, ctgr=@ctgr, state=@state, rdt=@rdt";
                        sql = "insert into td_list set " + cvp + ", wdt=curdate() ON DUPLICATE KEY UPDATE " + cvp;
                        sp.Add(new MySqlParameter("@co_cd", coCd));
                        sp.Add(new MySqlParameter("@ref_idx", refIdx));
                        sp.Add(new MySqlParameter("@adrs", adrs));
                        sp.Add(new MySqlParameter("@ctgr", ctgr));
                        sp.Add(new MySqlParameter("@state", state));
                        sp.Add(new MySqlParameter("@rdt", rgstDt));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                        if (rgstDt == toDay) nCnt++;
                        else uCnt++;
                    }
                    catch (Exception ex)
                    {
                        atomLog.AddLog(ex.Message);
                        continue;
                    }
                }
                db.Close();
                jaExpired.Clear();
            }

            atomLog.AddLog($" > 신규-{nCnt}, 갱신-{uCnt}");
        }

        /// <summary>
        /// 코람코자산신탁
        /// </summary>
        /// <param name="v"></param>
        private void Prc_koramco(int coCd)
        {
            //해결 못함
        }

        /// <summary>
        /// 한국토지신탁
        /// </summary>
        /// <param name="v"></param>
        private void Prc_koreit(int coCd)
        {
            //해결 못함
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            bgwork.Dispose();

            this.Dispose();
            this.Close();
        }
    }
}
