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

namespace Atom.CA
{
    public partial class fChatl : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AuctCd auctCd = new AuctCd();
        ApiUtil api = new ApiUtil();
        AtomLog atomLog = new AtomLog(300);     //로그 생성

        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 1500, webCnt = 0;
        int nCnt = 0, uCnt = 0;

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        public fChatl()
        {
            InitializeComponent();
            this.Shown += FChatl_Shown;
        }

        private void FChatl_Shown(object sender, EventArgs e)
        {
            CheckForIllegalCrossThreadCalls = false;
            bgwork = new BackgroundWorker();
            bgwork.DoWork += Bgwork_DoWork;
            bgwork.RunWorkerCompleted += Bgwork_RunWorkerCompleted;
            bgwork.WorkerReportsProgress = false;
            bgwork.WorkerSupportsCancellation = true;

            bgwork.RunWorkerAsync();
        }

        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            decimal i = 0, pdCnt = 0, pgCnt = 0;
            string jiwonNm = "", url = "", html = "", termStartDt = "", termEndDt = "";

            DataTable dtLaw = auctCd.DtLawInfo();
            termStartDt = String.Format("{0:yyyy.MM.dd}", DateTime.Now);
            termEndDt = String.Format("{0:yyyy.MM.dd}", DateTime.Now.AddDays(30));

            //string testArea = "서울중앙|대전|광주";
            //경매물건 -> 물건상세검색 -> 동산
            foreach (DataRow row in dtLaw.Rows)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                txtState.AppendText(string.Format("\r\n\r\n■■■■■     {0}     ■■■■■", row["lawNm"]));    //화면에 진행상태 표시

                jiwonNm = auctCd.LawNmEnc(row["lawNm"]);
                //if (Regex.IsMatch(row["lawNm"].ToString(), testArea) == false) continue;    //Test 범위 제한
                url = "http://www.courtauction.go.kr/RetrieveMvEstDetailSrchMulList.laf?";
                url += "jiwonNm=" + jiwonNm + "&mvRealGbncd=00031M&maeMokmulNm=%B9%B0%C7%B0%B8%ED%C0%BB+%C0%D4%B7%C2%C7%CF%BC%BC%BF%E4&_CUR_SRNID=PNO102003&_NEXT_SRNID=PNO102004&pageSpec=default40&_SRCH_SRNID=PNO102003&termEndDt=" + termEndDt + "&_FORM_YN=Y&page=default40&jibhgwanOffMgakPlcGubun=1&srnID=PNO102003&termStartDt=" + termStartDt + "&saGubun=%BA%BB&_CUR_CMD=InitMulSrch.laf&saYear=2020";
                html = net.GetHtml(url + "&targetRow=1");
                Match match = Regex.Match(html, @"물건수\s+\:\s+(\d+)건", rxOptM);
                if (!match.Success) continue;

                pdCnt = Convert.ToInt16(match.Groups[1].Value);
                pgCnt = Math.Ceiling(pdCnt / (decimal)40);
                for (i = 0; i < pgCnt; i++)
                {
                    if (i > 0)
                    {
                        webCnt++;
                        if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                        html = net.GetHtml(url + "&targetRow=" + (i * 40 + 1).ToString());
                    }
                    Prc_LstDtl(row, html);
                }
            }

            atomLog.AddLog(string.Format("신규-{0}건, 갱신-{1}건", nCnt, uCnt));

            Prc_Cancel();
            Prc_OldDel();
        }

        /// <summary>
        /// 사건목록 및 물품상세 처리
        /// </summary>
        /// <param name="row"></param>
        /// <param name="htmlLst"></param>
        private void Prc_LstDtl(DataRow row, string htmlLst)
        {
            string url = "", jiwonNm = "", htmlDtl = "", sql = "", crt = "", spt = "", tid = "", dbMode = "";
            string saNo = "", cltrNo = "", sn1 = "", sn2 = "", pdNo = "", apslAmt = "", minbAmt = "", adrs = "", lsTitle = "", placeType = "";
            string dpt = "", balif = "", bidDt = "", bidTm = "", pdNote = "", sidoCd = "", gugunCd = "", dongCd = "", riCd = "", x = "", y = "";

            decimal i = 0, lsCnt = 0, pgCnt = 0;
            string lsNo = "", pdNm = "", qty = "", std = "", amt = "", cat = "", lsNote = "", caseNo = "";
            
            HAPDoc doc = new HAPDoc();
            doc.LoadHtml(htmlLst);
            HAPDoc doc2 = new HAPDoc();
            IDictionary<string, string> dict = new Dictionary<string, string>();

            List<string> lsRslt = new List<string>();

            //매각장소유형
            Dictionary<string, string> dicPlace = new Dictionary<string, string>();
            dicPlace.Add("공장", "1");
            dicPlace.Add("동식물사육재배장", "2");
            dicPlace.Add("소매점", "3");
            dicPlace.Add("도매점", "4");
            dicPlace.Add("가정집", "5");
            dicPlace.Add("사무실", "6");
            dicPlace.Add("서비스제공시설", "7");
            dicPlace.Add("보관시설", "8");

            //물품종류
            Dictionary<string, string> dicCat = new Dictionary<string, string>();
            dicCat.Add("농수축임산물", "1");
            dicCat.Add("예술/수집품", "2");
            dicCat.Add("가전/생활용품", "3");
            dicCat.Add("사무/가구", "4");
            dicCat.Add("식음료", "5");
            dicCat.Add("의약품", "6");
            dicCat.Add("의류/잡화", "7");
            dicCat.Add("귀금속", "8");
            dicCat.Add("운송/장비/기계", "9");
            dicCat.Add("컴퓨터/전기/통신기계", "10");
            dicCat.Add("회원권/유가증권", "11");
            dicCat.Add("기타권리", "12");

            HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='물건상세검색 결과 표']/tbody/tr");
            if (ncTr == null) return;

            List<MySqlParameter> sp = new List<MySqlParameter>();
            crt = row["csCd"].ToString().Substring(0, 2);
            //spt = row["csCd"].ToString().Substring(2, 2);
            spt = row["csCd"].ToString();
            jiwonNm = auctCd.LawNmEnc(row["lawNm"]);

            foreach (HtmlNode tr in ncTr)
            {
                lsRslt.Clear();
                HtmlNodeCollection ncTd = tr.SelectNodes("./td");
                if (ncTd == null || ncTd.Count != 7) continue;

                saNo = ncTd[0].SelectSingleNode("./a/b").InnerText;
                cltrNo = Regex.Match(ncTd[0].InnerHtml, @"\d{14}", rxOptM).Value;
                Match m = Regex.Match(saNo, @"(\d+).(\d+)", rxOptM);
                sn1 = m.Groups[1].Value;
                sn2 = m.Groups[2].Value;
                pdNo = ncTd[1].InnerText.Trim();
                adrs = ncTd[2].SelectSingleNode("./a").InnerText.Trim();
                adrs = Regex.Replace(adrs, @"[\s]{2,}", " ");
                lsTitle = Regex.Match(ncTd[2].InnerText, @"\[(.*)?\]", rxOptM).Groups[1].Value.Trim();
                placeType = ncTd[3].InnerText.Trim();
                placeType = (dicPlace.ContainsKey(placeType)) ? dicPlace[placeType] : "";
                apslAmt = ncTd[5].SelectNodes("./div")[0].InnerText.Replace(",", "");
                minbAmt = ncTd[5].SelectNodes("./div")[1].InnerText.Replace(",", "");
                //if (cltrNo != "20190000006541") continue;   //임시 테스트
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                txtState.AppendText(string.Format("\r\nLST> {0}-{1} ({2})", sn1, sn2, pdNo));    //화면에 진행상태 표시

                url = "http://www.courtauction.go.kr/RetrieveMvEstMulDetailInfo.laf?";
                url += "jiwonNm=" + jiwonNm + "&srnID=PNO102004&saNo=" + cltrNo + "&page=default40&maemulSer=" + pdNo;
                htmlDtl = net.GetHtml(url + "&targetRow=1");
                if (htmlDtl.Contains("공고중인 물건이 아닙니다")) continue;

                doc2.LoadHtml(htmlDtl);
                HtmlNode ndTbl = doc2.DocumentNode.SelectSingleNode("//table[@summary='사건기본내역 표']");
                Match match = Regex.Match(ndTbl.InnerHtml, @">(\d+)부\s+(\w+)<", rxOptM);
                dpt = match.Groups[1].Value;
                balif = match.Groups[2].Value;

                ndTbl = doc2.DocumentNode.SelectSingleNode("//table[@summary='매각정보 표']");
                match = Regex.Match(ndTbl.InnerText, @"(\d{4}.\d{2}.\d{2})\s+(\d+\:\d+)부터", rxOptM);
                bidDt = match.Groups[1].Value.Replace(".", "-");
                bidTm = match.Groups[2].Value + ":00";
                pdNote = Regex.Match(ndTbl.InnerHtml, @"비고</th>\s+<td colspan=""3"">(.*?)</td>", rxOptS).Groups[1].Value.Trim();

                db.Open();
                //sql = "select tid from tc_list where crt=" + crt + " and spt=" + spt + " and sn1=" + sn1 + " and sn2=" + sn2 + " and pn='" + pdNo + "' limit 1";
                sql = "select tid from tc_list where spt=" + spt + " and sn1=" + sn1 + " and sn2=" + sn2 + " and pn='" + pdNo + "' limit 1";
                MySqlDataReader mdr = db.ExeRdr(sql);
                if (mdr.HasRows)
                {
                    uCnt++;
                    dbMode = "Update";
                    mdr.Read();
                    tid = mdr["tid"].ToString();
                    sql = "update tc_list set crt=@crt, spt=@spt, dpt=@dpt, balif=@balif, sn=@sn, sn1=@sn1, sn2=@sn2, pn=@pn, cltr_no=@cltr_no, ls_title=@ls_title, apsl_amt=@apsl_amt, minb_amt=@minb_amt,";
                    sql += "bid_dt=@bid_dt, bid_tm=@bid_tm, place_type=@place_type, adrs=@adrs, pd_note=@pd_note, wdt=curdate() where tid='" + tid + "'";
                }
                else
                {
                    nCnt++;
                    dbMode = "Insert";

                    dict.Clear();
                    dict = api.DaumSrchAdrs(adrs);
                    sidoCd = dict["sidoCd"];
                    gugunCd = dict["gugunCd"];
                    dongCd = dict["dongCd"];
                    riCd = dict["riCd"];
                    x = dict["x"];
                    y = dict["y"];
                    if (sidoCd == "")
                    {
                        AdrsParser parser = new AdrsParser(adrs);
                        dict = api.DaumSrchAdrs(parser.AdrsM);
                        sidoCd = dict["sidoCd"];
                        gugunCd = dict["gugunCd"];
                        dongCd = dict["dongCd"];
                        riCd = dict["riCd"];
                        x = dict["x"];
                        y = dict["y"];
                    }
                    sql = "insert into tc_list (crt, spt, dpt, balif, sn, sn1, sn2, pn, cltr_no, ls_title, apsl_amt, minb_amt, bid_dt, bid_tm, place_type, adrs, si_cd, gu_cd, dn_cd, ri_cd, x, y, pd_note, wdt) " +
                    "values (@crt, @spt, @dpt, @balif, @sn, @sn1, @sn2, @pn, @cltr_no, @ls_title, @apsl_amt, @minb_amt, @bid_dt, @bid_tm, @place_type, @adrs, @si_cd, @gu_cd, @dn_cd, @ri_cd, @x, @y, @pd_note, curdate())";

                    sp.Add(new MySqlParameter("@si_cd", sidoCd));
                    sp.Add(new MySqlParameter("@gu_cd", gugunCd));
                    sp.Add(new MySqlParameter("@dn_cd", dongCd));
                    sp.Add(new MySqlParameter("@ri_cd", riCd));
                    sp.Add(new MySqlParameter("@x", x));
                    sp.Add(new MySqlParameter("@y", y));
                }
                mdr.Close();
                sp.Add(new MySqlParameter("@crt", crt));
                sp.Add(new MySqlParameter("@spt", spt));
                sp.Add(new MySqlParameter("@dpt", dpt));
                sp.Add(new MySqlParameter("@balif", balif));
                sp.Add(new MySqlParameter("@sn", saNo));
                sp.Add(new MySqlParameter("@sn1", sn1));
                sp.Add(new MySqlParameter("@sn2", sn2));
                sp.Add(new MySqlParameter("@pn", pdNo));
                sp.Add(new MySqlParameter("@cltr_no", cltrNo));
                sp.Add(new MySqlParameter("@ls_title", lsTitle));
                sp.Add(new MySqlParameter("@apsl_amt", apslAmt));
                sp.Add(new MySqlParameter("@minb_amt", minbAmt));
                sp.Add(new MySqlParameter("@bid_dt", bidDt));
                sp.Add(new MySqlParameter("@bid_tm", bidTm));
                sp.Add(new MySqlParameter("@place_type", placeType));
                sp.Add(new MySqlParameter("@adrs", adrs));
                sp.Add(new MySqlParameter("@pd_note", pdNote));
                db.ExeQry(sql, sp);
                sp.Clear();

                if (dbMode == "Insert")
                {
                    tid = ((UInt64)db.LastId()).ToString();
                    lsRslt.Clear();

                    //매각물품 세부목록
                    lsCnt = Convert.ToInt16(Regex.Match(htmlDtl, @"총\s+<span>(\d+)</span>건", rxOptM).Groups[1].Value);
                    pgCnt = Math.Ceiling(lsCnt / (decimal)40);
                    for (i = 0; i < pgCnt; i++)
                    {
                        if (i > 0)
                        {
                            webCnt++;
                            if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                            htmlDtl = net.GetHtml(url + "&targetRow=" + (i * 40 + 1).ToString());
                            doc2.LoadHtml(htmlDtl);
                        }

                        HtmlNodeCollection ncTrLs = doc2.DocumentNode.SelectNodes("//table[@summary='매각물품 표']/tbody/tr");
                        if (ncTrLs == null) continue;
                        foreach (HtmlNode trLs in ncTrLs)
                        {
                            HtmlNodeCollection ncTdLs = trLs.SelectNodes("./td");
                            lsNo = ncTdLs[0].InnerText.Trim();
                            pdNm = Regex.Replace(ncTdLs[1].InnerText.Trim(), @"[\s]{2,}", " ", rxOptS);
                            qty = Regex.Replace(ncTdLs[2].InnerText.Trim(), @"[\s]{2,}", " ", rxOptS);
                            std = Regex.Replace(ncTdLs[3].InnerText.Trim(), @"[\s]{2,}", " ", rxOptS);
                            amt = Regex.Replace(ncTdLs[4].InnerText.Trim(), @"[원\,]", "", rxOptS);
                            cat = ncTdLs[5].InnerText.Trim();
                            lsNote = Regex.Replace(ncTdLs[6].InnerText.Trim(), @"[\s]{2,}", " ", rxOptS);

                            sql = "insert into tc_ls (tid, no, pd_nm, qty, std, amt, cat, note) " +
                                "values (@tid, @no, @pd_nm, @qty, @std, @amt, @cat, @note)";
                            sp.Add(new MySqlParameter("@tid", tid));
                            sp.Add(new MySqlParameter("@no", lsNo));
                            sp.Add(new MySqlParameter("@pd_nm", pdNm));
                            sp.Add(new MySqlParameter("@qty", qty));
                            sp.Add(new MySqlParameter("@std", std));
                            sp.Add(new MySqlParameter("@amt", amt));
                            sp.Add(new MySqlParameter("@cat", cat));
                            sp.Add(new MySqlParameter("@note", lsNote));
                            db.ExeQry(sql, sp);
                            sp.Clear();

                            if (dicCat.ContainsKey(cat))
                            {
                                caseNo = dicCat[cat];
                                if (lsRslt.Contains(caseNo)) continue;
                                lsRslt.Add(caseNo);
                            }
                        }
                    }
                    if (lsRslt.Count > 0)
                    {   
                        sql = "update tc_list set cat_srch='" + string.Join(",", lsRslt) + "' where tid='" + tid + "'";
                        db.ExeQry(sql);
                    }
                }
                else
                {
                    //sql = "delete from tc_ls where tid='" + tid + "'";
                    //db.ExeQry(sql);
                }
                db.Close();
            }
        }

        /// <summary>
        /// 동산매각공고에서 [취소]공고 체크
        /// </summary>
        private void Prc_Cancel()
        {
            string jiwonNm = "", url = "", html = "", curMnth = "", nxtMnth = "", date = "", targetDt = "", sql = "";
            string saNo = "", pdNo = "", crt = "", spt = "";

            targetDt = DateTime.Now.AddDays(30).ToShortDateString().Replace("-", string.Empty);

            DataTable dtLaw = auctCd.DtLawInfo();
            List<string> mnthList = new List<string>();
            curMnth = DateTime.Now.ToShortDateString().Substring(0, 7).Replace("-", string.Empty);
            nxtMnth = targetDt.Substring(0, 6);
            mnthList.Add(curMnth);
            if (curMnth != nxtMnth) mnthList.Add(nxtMnth);

            HAPDoc doc = new HAPDoc();

            //공고 일정(캘린더)
            DataTable dtCal = new DataTable();
            dtCal.Columns.Add("jwNm");
            dtCal.Columns.Add("csCd");
            dtCal.Columns.Add("bidDt");
            dtCal.Columns.Add("dptCd");

            txtState.AppendText("\r\n\r\n▶▶▶▶▶     공고[취소] 확인 중     ◀◀◀◀◀");

            foreach (DataRow row in dtLaw.Rows)
            {
                jiwonNm = auctCd.LawNmEnc(row["lawNm"]);
                //if (Regex.IsMatch(row["lawNm"].ToString(), testArea) == false) continue;    //Test 범위 제한

                foreach (string ym in mnthList)
                {
                    webCnt++;
                    if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                    date = ym.Substring(0, 4) + "." + ym.Substring(4);
                    url = "http://www.courtauction.go.kr/RetrieveMvEstMgakNotifySrch.laf?date=" + date + "&inqYear=" + ym.Substring(0, 4) + "&inqMnth=" + ym.Substring(4) + "&srnID=PNO101003&jiwonNm=" + jiwonNm + "&notifyKind=01";
                    html = net.GetHtml(url);
                    doc.LoadHtml(html);
                    HtmlNodeCollection ncImg = doc.DocumentNode.SelectNodes("//div[contains(@class,'cal_schedule')]/a/img[@alt='취소']");
                    if (ncImg == null) continue;

                    foreach (HtmlNode ndImg in ncImg)
                    {
                        string clickStr = ndImg.ParentNode.OuterHtml;
                        Match m = Regex.Match(clickStr, @"'(\d{8})'\,[\s]*'(\d+)'", rxOptM);     //1-입찰일, 2-부서코드
                        dtCal.Rows.Add(jiwonNm, row["csCd"].ToString(), m.Groups[1].Value, m.Groups[2].Value);
                    }
                }
            }

            foreach (DataRow row in dtCal.Rows)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                crt = row["csCd"].ToString().Substring(0, 2);
                //spt = row["csCd"].ToString().Substring(2);
                spt = row["csCd"].ToString();
                jiwonNm = row["jwNm"].ToString();
                string ym = row["bidDt"].ToString();
                date = ym.Substring(0, 4) + "." + ym.Substring(4);
                url = "http://www.courtauction.go.kr/RetrieveMvEstMgakNotifySrchGyulgwa.laf?jiwonNm=" + jiwonNm + "&notifyKind=01&date=" + date + "&inqYear=" + ym.Substring(0, 4) + "&inqMnth=" + ym.Substring(4) + "&maeGiil=" + row["bidDt"].ToString() + "&jpDeptCd=" + row["dptCd"].ToString();
                html = net.GetHtml(url);
                doc.LoadHtml(html);
                HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='취소공고내역 표']/tbody/tr");
                if (ncTr == null) return;

                db.Open();
                foreach (HtmlNode tr in ncTr)
                {
                    HtmlNodeCollection ncTd = tr.SelectNodes("./td");
                    if (ncTd.Count != 5) continue;
                    pdNo = ncTd[1].InnerText.Trim();
                    MatchCollection mc = Regex.Matches(ncTd[0].InnerText, @"(\d+[본가징]\d+)", rxOptM);
                    foreach (Match match in mc)
                    {
                        saNo = match.Groups[1].Value;
                        //sql = "update tc_list set stop=1, wdt=curdate() where crt='" + crt + "' and spt='" + spt + "' and sn='" + saNo + "' and pn='" + pdNo + "'";
                        sql = "update tc_list set stop=1, wdt=curdate() where spt='" + spt + "' and sn='" + saNo + "' and pn='" + pdNo + "'";
                        db.ExeQry(sql);
                    }
                }
                db.Close();
            }
            atomLog.AddLog("취소공고 처리 완료");
        }

        /// <summary>
        /// 매각기일 지난 물건 삭제
        /// </summary>
        private void Prc_OldDel()
        {
            string sql = "", tid = "";

            txtState.AppendText("\r\n\r\n▶▶▶▶▶     매각기일 지난 물건 정리     ◀◀◀◀◀");

            sql = "select tid from tc_list where bid_dt < curdate()";
            DataTable dt = db.ExeDt(sql);

            db.Open();
            foreach (DataRow row in dt.Rows)
            {
                tid = row["tid"].ToString();
                sql = "delete from tc_ls where tid='" + tid + "'";
                db.ExeQry(sql);
                sql = "delete from tc_list where tid='" + tid + "'";
                db.ExeQry(sql);
            }
            sql = "optimize table tc_ls";
            db.ExeQry(sql);
            sql = "optimize table tc_list";
            db.ExeQry(sql);            
            db.Close();

            atomLog.AddLog("매각기일 지난 물건 삭제 완료");
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
