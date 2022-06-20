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
    public partial class fSkd : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AtomLog atomLog = new AtomLog(101);
        AuctCd auctCd = new AuctCd();
        ApiUtil api = new ApiUtil();

        BackgroundWorker bgwork;
        int setCnt = 10, setSleep = 1500, webCnt = 0;

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        //RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        DataTable dtCatCd, dtBrCd;

        public fSkd()
        {
            InitializeComponent();
            this.Shown += FSkd_Shown;
        }

        private void FSkd_Shown(object sender, EventArgs e)
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
            //물건종별 코드
            dtCatCd = db.ExeDt("select cat1_cd, cat2_cd, cat3_cd, cat3_nm, hide from ta_cd_cat where cat3_cd > 0");
             
            //건축물용도 코드
            dtBrCd = db.ExeDt("select prps_cd, cat_cd from ta_cd_br");

            Prc_List();
            Prc_Dtl();
            Prc_RgstSMs();

            //-> 예정물건 매각준비 상태 체크에서 실행한다.(fSkdReady.cs)
            //Prc_RgstIssueAdd();
        }

        /// <summary>
        /// 오늘 등록된 예정물건 신건발송용 SMS 등록
        /// </summary>
        private void Prc_RgstSMs()
        {
            string sql, tid;

            sql = "select tid from ta_list where sta1=10 and cat1 != 30 and 1st_dt=curdate()";
            DataTable dt = db.ExeDt(sql);

            db.Open();
            foreach (DataRow row in dt.Rows)
            {
                tid = row["tid"].ToString();
                sql = "insert ignore into db_tank.tx_sms set tid='" + tid + "', state='경매개시', dvsn=1, wdt=curdate(), wtm=curtime()";
                db.ExeQry(sql);
            }
            db.Close();
        }

        /// <summary>
        /// 목록 수집
        /// </summary>
        private void Prc_List()
        {
            string url = "", html = "", jiwonNm = "", dpt = "", sql = "";
            string tid = "", crt = "", spt = "", sn1 = "", sn2 = "", owner = "", debtor = "", iniDt = "", shrDt = "";
            string adrs = "", adrsType, regnAdrs, mt;
            int cnt = 0;

            atomLog.AddLog("목록 수집 시작");
            txtState.AppendText("\r\n # 목록 수집 시작 #");

            IDictionary<string, string> dict = new Dictionary<string, string>();
            List<MySqlParameter> sp = new List<MySqlParameter>();
            DataTable dtDpt = db.ExeDt("select ca_nm, cs_cd, dpt_cd from ta_cd_dpt order by cs_cd, dpt_cd");
            //string testArea = "서울중앙|인천|광주";
            //string testArea = "서울서부|수원|대전|포항";
            //string testArea = "서울북부|대구지방법원|부산지방법원";
            HAPDoc doc = new HAPDoc();
            foreach (DataRow row in dtDpt.Rows)
            {
                //if (Regex.IsMatch(row["ca_nm"].ToString(), testArea) == false) continue;    //Test 범위 제한

                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                crt = row["cs_cd"].ToString().Substring(0, 2);
                spt = row["cs_cd"].ToString();
                jiwonNm = auctCd.LawNmEnc(row["ca_nm"]);
                dpt = row["dpt_cd"].ToString();
                url = "http://www.courtauction.go.kr/RetrieveBdangYoguJonggiNotify.laf?srnID=PNO101005&srchMthd=1&jiwonNm=" + jiwonNm + "&jpDeptCd=" + dpt;
                html = net.GetHtml(url);
                if (html.Contains("검색결과가 없습니다")) continue;

                doc.LoadHtml(html);
                HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='배당요구종기공고 표']/tbody/tr");
                if (ncTr == null) continue;
                                
                db.Open();
                foreach (HtmlNode ndTr in ncTr)
                {
                    HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                    if (ncTd.Count == 6)
                    {
                        sn1 = ""; sn2 = ""; adrs = ""; owner = ""; debtor = ""; iniDt = ""; shrDt = "";
                        Match match = Regex.Match(ncTd[0].InnerText, @"(\d+)타경(\d+)", rxOptM);
                        sn1 = match.Groups[1].Value;
                        sn2 = match.Groups[2].Value;
                        adrs = Regex.Replace(ncTd[1].InnerText, @"[\r\n\t]", string.Empty).Trim();
                        owner = Regex.Replace(ncTd[2].InnerText, @"[\r\n\t]", string.Empty).Trim();
                        continue;
                    }
                    if (ncTd.Count == 3)
                    {
                        debtor = ncTd[0].InnerText.Trim();
                        iniDt = ncTd[1].InnerText.Trim();
                        shrDt = ncTd[2].InnerText.Trim();
                    }
                    
                    sql = "select tid from ta_list where spt=" + spt + " and sn1=" + sn1 + " and sn2=" + sn2 + " limit 1";
                    if (db.ExistRow(sql)) continue;

                    txtState.AppendText(string.Format("\r\nLST> {0} {1}-{2}", row["ca_nm"], sn1, sn2));    //화면에 진행상태 표시

                    dict.Clear();
                    dict = api.DaumSrchAdrs(adrs);
                    if (dict["totalCnt"] == string.Empty || dict["totalCnt"] == "0")
                    {
                        adrsType = "0";
                        regnAdrs = adrs;
                        mt = "0";
                    }
                    else
                    {
                        adrsType = (dict["adrsType"].Contains("ROAD_ADDR")) ? "2" : "1";
                        regnAdrs = (dict["jbAdrsNm"] == "") ? adrs : dict["jbAdrsNm"];
                        mt = dict["mt"];
                    }

                    sql = "insert into ta_list set crt=@crt, spt=@spt, dpt=@dpt, sn1=@sn1, sn2=@sn2, debtor=@debtor, owner=@owner, ini_dt=@ini_dt, shr_dt=@shr_dt, sta1=10, sta2=1010, " +
                        "adrs=@adrs, adrs_type=@adrs_type, regn_adrs=@regn_adrs, mt=@mt, m_adrs_no=@m_adrs_no, s_adrs_no=@s_adrs_no, road_adrs=@road_adrs, m_bldg_no=@m_bldg_no, s_bldg_no=@s_bldg_no, bldg_nm=@bldg_nm, road_nm=@road_nm, " +
                        "si_cd=@si_cd, gu_cd=@gu_cd, dn_cd=@dn_cd, ri_cd=@ri_cd, x=@x, y=@y, 1st_dt=curdate()";
                    sp.Add(new MySqlParameter("@crt", crt));
                    sp.Add(new MySqlParameter("@spt", spt));
                    sp.Add(new MySqlParameter("@dpt", dpt));
                    sp.Add(new MySqlParameter("@sn1", sn1));
                    sp.Add(new MySqlParameter("@sn2", sn2));
                    sp.Add(new MySqlParameter("@debtor", debtor));
                    sp.Add(new MySqlParameter("@owner", owner));
                    sp.Add(new MySqlParameter("@ini_dt", iniDt));
                    sp.Add(new MySqlParameter("@shr_dt", shrDt));
                    sp.Add(new MySqlParameter("@adrs", adrs));
                    sp.Add(new MySqlParameter("@adrs_type", adrsType));
                    sp.Add(new MySqlParameter("@regn_adrs", regnAdrs));
                    sp.Add(new MySqlParameter("@mt", mt));
                    sp.Add(new MySqlParameter("@m_adrs_no", dict["jbNoM"]));
                    sp.Add(new MySqlParameter("@s_adrs_no", dict["jbNoS"]));
                    sp.Add(new MySqlParameter("@road_adrs", dict["rdAdrsNm"]));
                    sp.Add(new MySqlParameter("@m_bldg_no", dict["bldgNoM"]));
                    sp.Add(new MySqlParameter("@s_bldg_no", dict["bldgNoS"]));
                    sp.Add(new MySqlParameter("@bldg_nm", dict["bldgNm"]));
                    sp.Add(new MySqlParameter("@road_nm", dict["rdNm"]));
                    sp.Add(new MySqlParameter("@si_cd", dict["sidoCd"]));
                    sp.Add(new MySqlParameter("@gu_cd", dict["gugunCd"]));
                    sp.Add(new MySqlParameter("@dn_cd", dict["dongCd"]));
                    sp.Add(new MySqlParameter("@ri_cd", dict["riCd"]));
                    sp.Add(new MySqlParameter("@x", dict["x"]));
                    sp.Add(new MySqlParameter("@y", dict["y"]));
                    db.ExeQry(sql, sp);
                    sp.Clear();

                    tid = ((UInt64)db.LastId()).ToString();
                    sql = "insert into ta_dtl (tid) values(" + tid + ")";
                    db.ExeQry(sql);
                    cnt++;
                }
                db.Close();
            }

            atomLog.AddLog(string.Format("목록 수집 완료-{0}건", cnt));
            txtState.AppendText(string.Format("\r\n목록 수집 완료-{0}건", cnt));
        }

        /// <summary>
        /// 상세 수집
        /// </summary>
        private void Prc_Dtl()
        {
            string url = "", html = "", jiwonNm = "", saNo = "", sql = "";
            string auctNm = "", rcptDt = "", iniDt = "", billAmt = "", appeal = "", endRslt = "", endDt = "", creditor = "", debtor = "", owner = "", auctType = "", frmlType = "";
            string spt, sn1, sn2, tid = "", lsNo = "", adrs = "", pin = "", cat3 = "";
            int creditorCnt = 0, debtorCnt = 0, ownerCnt = 0, cnt = 0;

            atomLog.AddLog("상세 수집 시작");

            IDictionary<string, string> dict = new Dictionary<string, string>();
            List<MySqlParameter> sp = new List<MySqlParameter>();

            //당사자내역
            DataTable dtPrsn = new DataTable();
            dtPrsn.Columns.Add("dvsn");
            dtPrsn.Columns.Add("prsn");

            //관련사건내역 
            string[] rCaseArr = new string[] { "판결정본", "지급명령", "민사본안", "개시결정이의", "이행권고결정", "화해권고결정", "이송전사건", "이송후사건" };
            DataTable dtRCase = new DataTable();
            dtRCase.Columns.Add("crtNm");
            dtRCase.Columns.Add("caseNo");
            dtRCase.Columns.Add("dvsn");

            DataTable dtLs = new DataTable();
            dtLs.Columns.Add("no");
            dtLs.Columns.Add("adrs");
            dtLs.Columns.Add("pin");
            dtLs.Columns.Add("dvsn");
            dtLs.Columns.Add("note");
            dtLs.Columns.Add("sidoCd");
            dtLs.Columns.Add("gugunCd");
            dtLs.Columns.Add("dongCd");
            dtLs.Columns.Add("riCd");
            dtLs.Columns.Add("hCd");
            dtLs.Columns.Add("pnu");
            dtLs.Columns.Add("zoneNo");
            dtLs.Columns.Add("x");
            dtLs.Columns.Add("y");

            DataTable dtList = db.ExeDt("select tid,crt,spt,sn1,sn2,cat3 from ta_list where sta1=10 and rcp_dt='0000-00-00' and ini_dt <= date_sub(curdate(),interval 15 day)");
            HAPDoc doc = new HAPDoc();
            foreach (DataRow row in dtList.Rows)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                dtLs.Rows.Clear();
                dtPrsn.Rows.Clear();
                dtRCase.Rows.Clear();
                creditor = ""; debtor = ""; owner = ""; auctNm = "";

                tid = row["tid"].ToString();
                spt = row["spt"].ToString();
                sn1 = row["sn1"].ToString();
                sn2 = row["sn2"].ToString();
                cat3 = row["cat3"].ToString();
                jiwonNm = auctCd.FindLawNm(string.Format("{0}", spt), true);
                saNo = sn1 + "0130" + sn2.PadLeft(6, '0');
                url = "http://www.courtauction.go.kr/RetrieveRealEstDetailInqSaList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&srnID=PNO101005&_SRCH_SRNID=PNO101005";
                html = net.GetHtml(url);
                if (html.Contains("검색결과없음")) continue;
                doc.LoadHtml(html);

                //사건기본내역
                auctType = "1";  //임의경매(default)
                frmlType = "0";
                HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='사건기본내역 표']/tr");
                if (ncTr != null)
                {
                    foreach (HtmlNode tr in ncTr)
                    {
                        HtmlNodeCollection ncTd = tr.SelectNodes("th|td");
                        foreach (HtmlNode td in ncTd)
                        {
                            if (td.InnerText == "사건명") auctNm = td.SelectSingleNode("following-sibling::*[1]").InnerText;
                            if (td.InnerText == "접수일자") rcptDt = td.SelectSingleNode("following-sibling::*[1]").InnerText;
                            if (td.InnerText == "개시결정일자") iniDt = td.SelectSingleNode("following-sibling::*[1]").InnerText;
                            if (td.InnerText == "청구금액") billAmt = td.SelectSingleNode("following-sibling::*[1]").InnerText.Replace(",", string.Empty).Replace("원", string.Empty).Trim();
                            if (td.InnerText == "사건항고/정지여부") appeal = td.SelectSingleNode("following-sibling::*[1]").InnerText;
                            if (td.InnerText == "종국결과") endRslt = td.SelectSingleNode("following-sibling::*[1]").InnerText;
                            if (td.InnerText == "종국일자") endDt = td.SelectSingleNode("following-sibling::*[1]").InnerText;
                        }
                    }
                    if (auctNm.Contains("강제")) auctType = "2";
                    if (auctNm.Contains("부동산임의경매") || auctNm.Contains("부동산강제경매"))
                    {
                        if (Convert.ToDecimal(billAmt) <= 1000) frmlType = "3";     //소액 청구금액
                    }
                    else
                    {
                        if (auctNm.Contains("공유물")) frmlType = "1";
                        else if (Regex.IsMatch(auctNm, @"자동차.*형식", rxOptM)) frmlType = "6";
                        else if (Regex.IsMatch(auctNm, @"파산|회생", rxOptM)) frmlType = "7";
                        else if (Regex.IsMatch(auctNm, @"청산|처분|환가|한정|상속\s재산|재산\s분할", rxOptM)) frmlType = "4";
                        else if (auctNm.Contains("유치권")) frmlType = "5";
                        else if (auctNm.Contains("형식적")) frmlType = "2";
                    }
                }

                //목록내역
                ncTr = doc.DocumentNode.SelectNodes("//table[@summary='목록내역 표']/tbody/tr");
                if (ncTr != null)
                {
                    foreach (HtmlNode ndTr in ncTr)
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        lsNo = Regex.Match(ncTd[0].InnerText, @"\d+").Value;
                        adrs = ncTd[1].InnerText.Trim();
                        pin = Regex.Match(ncTd[1].InnerHtml, @"regiBu\('(\d+)'\)", RegexOptions.IgnoreCase).Groups[1].Value;

                        dict.Clear();
                        dict = api.DaumSrchAdrs(adrs);
                        DataRow dr = dtLs.NewRow();
                        dr["no"] = lsNo;
                        dr["adrs"] = adrs;
                        dr["pin"] = pin;
                        dr["dvsn"] = ncTd[2].InnerText.Trim();
                        dr["note"] = ncTd[3].InnerText.Trim();
                        dr["sidoCd"] = dict["sidoCd"];
                        dr["gugunCd"] = dict["gugunCd"];
                        dr["dongCd"] = dict["dongCd"];
                        dr["riCd"] = dict["riCd"];
                        dr["hCd"] = dict["hCd"];
                        dr["pnu"] = dict["pnu"];
                        dr["zoneNo"] = dict["zoneNo"];
                        dr["x"] = dict["x"];
                        dr["y"] = dict["y"];
                        dtLs.Rows.Add(dr);
                    }
                }

                //관련사건내역
                ncTr = doc.DocumentNode.SelectNodes("//table[@summary='관련사건내역 표']/tbody/tr");
                if (ncTr != null)
                {
                    foreach (HtmlNode ndTr in ncTr)
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        if (ncTd[0].InnerText.Contains("없습니다")) break;
                        if (ncTd.Count != 3) continue;
                        if (!rCaseArr.Contains(ncTd[2].InnerText.Trim())) continue;
                        dtRCase.Rows.Add(ncTd[0].InnerText.Trim(), ncTd[1].InnerText.Trim(), ncTd[2].InnerText.Trim());
                    }
                }

                //당사자내역       
                ncTr = doc.DocumentNode.SelectNodes("//table[@summary='당사자내역 표']/tbody/tr");
                if (ncTr != null)
                {
                    foreach (HtmlNode ndTr in ncTr)
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        dtPrsn.Rows.Add(ncTd[0].InnerText, ncTd[1].InnerText.Replace(@"(선정당사자)", string.Empty).Replace(@"(선정자)", string.Empty).Trim());
                        if (ncTd[3].InnerText.Trim() == string.Empty) break;
                        dtPrsn.Rows.Add(ncTd[3].InnerText, ncTd[4].InnerText.Replace(@"(선정당사자)", string.Empty).Replace(@"(선정자)", string.Empty).Trim());
                    }

                    if (dtPrsn.Rows.Count > 0)
                    {
                        //채권자
                        var xRow = dtPrsn.Rows.Cast<DataRow>().Where(t => t["dvsn"].ToString().Contains("신청인"));
                        creditorCnt = xRow?.Count() ?? 0;
                        if (creditorCnt > 0)
                        {
                            var tRow = xRow.FirstOrDefault();
                            creditor = (creditorCnt > 1) ? string.Format("{0} 외{1}", tRow["prsn"], creditorCnt - 1) : tRow["prsn"].ToString();
                        }
                        else
                        {
                            xRow = dtPrsn.Rows.Cast<DataRow>().Where(t => t["dvsn"].ToString() == "채권자");   //[임금채권자]도 있으므로(.Contains 사용안함)
                            creditorCnt = xRow?.Count() ?? 0;
                            if (creditorCnt > 0)
                            {
                                var tRow = xRow.FirstOrDefault();
                                creditor = (creditorCnt > 1) ? string.Format("{0} 외{1}", tRow["prsn"], creditorCnt - 1) : tRow["prsn"].ToString();
                            }
                        }

                        //채무자
                        xRow = dtPrsn.Rows.Cast<DataRow>().Where(t => t["dvsn"].ToString().Contains("상대방"));
                        debtorCnt = xRow?.Count() ?? 0;
                        if (debtorCnt > 0)
                        {
                            var tRow = xRow.FirstOrDefault();
                            debtor = (debtorCnt > 1) ? string.Format("{0} 외{1}", tRow["prsn"], debtorCnt - 1) : tRow["prsn"].ToString();
                        }
                        else
                        {
                            xRow = dtPrsn.Rows.Cast<DataRow>().Where(t => t["dvsn"].ToString().Contains("채무자"));
                            debtorCnt = xRow?.Count() ?? 0;
                            if (debtorCnt > 0)
                            {
                                var tRow = dtPrsn.Rows.Cast<DataRow>().Where(t => t["dvsn"].ToString().Contains("채무자겸소유자"));
                                if (tRow?.Count() > 0)
                                {
                                    debtor = (debtorCnt > 1) ? string.Format("{0} 외{1}", tRow.FirstOrDefault()["prsn"], debtorCnt - 1) : tRow.FirstOrDefault()["prsn"].ToString();
                                }
                                else
                                {
                                    debtor = (debtorCnt > 1) ? string.Format("{0} 외{1}", xRow.FirstOrDefault()["prsn"], debtorCnt - 1) : xRow.FirstOrDefault()["prsn"].ToString();
                                }
                            }
                        }

                        //소유자
                        xRow = dtPrsn.Rows.Cast<DataRow>().Where(t => t["dvsn"].ToString().Contains("소유자"));
                        ownerCnt = xRow?.Count() ?? 0;
                        if (ownerCnt > 0)
                        {
                            var tRow = dtPrsn.Rows.Cast<DataRow>().Where(t => t["dvsn"].ToString().Contains("채무자겸소유자"));
                            if (tRow?.Count() > 0)
                            {
                                owner = (ownerCnt > 1) ? string.Format("{0} 외{1}", tRow.FirstOrDefault()["prsn"], ownerCnt - 1) : tRow.FirstOrDefault()["prsn"].ToString();
                            }
                            else
                            {
                                owner = (ownerCnt > 1) ? string.Format("{0} 외{1}", xRow.FirstOrDefault()["prsn"], ownerCnt - 1) : xRow.FirstOrDefault()["prsn"].ToString();
                            }
                        }
                    }
                }

                txtState.AppendText(string.Format("\r\nDTL> {0}", tid));    //화면에 진행상태 표시

                //DB처리
                
                db.Open();
                sql = "update ta_list set rcp_dt=@rcp_dt, ini_dt=@ini_dt, end_dt=@end_dt, creditor=@creditor, debtor=@debtor, owner=@owner, auct_type=@auct_type, frml_type=@frml_type where tid=@tid";
                sp.Add(new MySqlParameter("@rcp_dt", rcptDt));
                sp.Add(new MySqlParameter("@ini_dt", iniDt));
                sp.Add(new MySqlParameter("@end_dt", endDt));
                sp.Add(new MySqlParameter("@creditor", creditor));
                sp.Add(new MySqlParameter("@debtor", debtor));
                sp.Add(new MySqlParameter("@owner", owner));
                sp.Add(new MySqlParameter("@auct_type", auctType));
                sp.Add(new MySqlParameter("@frml_type", frmlType));
                sp.Add(new MySqlParameter("@tid", tid));
                db.ExeQry(sql, sp);
                sp.Clear();

                sql = "update ta_dtl set auct_nm=@auct_nm, bill_amt=@bill_amt where tid=@tid";
                sp.Add(new MySqlParameter("@auct_nm", auctNm.Replace("부동산", string.Empty)));
                sp.Add(new MySqlParameter("@bill_amt", billAmt));                
                sp.Add(new MySqlParameter("@tid", tid));
                db.ExeQry(sql, sp);
                sp.Clear();

                foreach (DataRow r in dtLs.Rows)
                {
                    sql = "insert into ta_ls (tid, no, adrs, pin, dvsn, note, si_cd, gu_cd, dn_cd, ri_cd, hj_cd, pnu, x, y, zone_no) " +
                        "values (@tid, @no, @adrs, @pin, @dvsn, @note, @si_cd, @gu_cd, @dn_cd, @ri_cd, @hj_cd, @pnu, @x, @y, @zone_no)";
                    sp.Add(new MySqlParameter("@tid", tid));
                    sp.Add(new MySqlParameter("@no", r["no"]));
                    sp.Add(new MySqlParameter("@adrs", r["adrs"]));
                    sp.Add(new MySqlParameter("@pin", r["pin"]));
                    sp.Add(new MySqlParameter("@dvsn", r["dvsn"]));
                    sp.Add(new MySqlParameter("@note", r["note"]));
                    sp.Add(new MySqlParameter("@si_cd", r["sidoCd"]));
                    sp.Add(new MySqlParameter("@gu_cd", r["gugunCd"]));
                    sp.Add(new MySqlParameter("@dn_cd", r["dongCd"]));
                    sp.Add(new MySqlParameter("@ri_cd", r["riCd"]));
                    sp.Add(new MySqlParameter("@hj_cd", r["hCd"]));
                    sp.Add(new MySqlParameter("@pnu", r["pnu"]));
                    sp.Add(new MySqlParameter("@x", r["x"]));
                    sp.Add(new MySqlParameter("@y", r["y"]));
                    sp.Add(new MySqlParameter("@zone_no", r["zoneNo"]));
                    db.ExeQry(sql, sp);
                    sp.Clear();
                }

                //관련사건내역 저장
                foreach (DataRow r in dtRCase.Rows)
                {
                    sql = "insert ignore into ta_rcase set spt=@spt, sn1=@sn1, sn2=@sn2, crt_nm=@crt_nm, case_no=@case_no, dvsn=@dvsn, wdt=CURDATE()";
                    sp.Add(new MySqlParameter("@spt", spt));
                    sp.Add(new MySqlParameter("@sn1", sn1));
                    sp.Add(new MySqlParameter("@sn2", sn2));
                    sp.Add(new MySqlParameter("@crt_nm", r["crtNm"]));
                    sp.Add(new MySqlParameter("@case_no", r["caseNo"]));
                    sp.Add(new MySqlParameter("@dvsn", r["dvsn"]));
                    db.ExeQry(sql, sp);
                    sp.Clear();
                }

                //당사자내역
                foreach (DataRow r in dtPrsn.Rows)
                {
                    sql = "insert into ta_prsn (tid, dvsn, nm) values (@tid, @dvsn, @nm)";
                    sp.Add(new MySqlParameter("@tid", tid));
                    sp.Add(new MySqlParameter("@dvsn", r["dvsn"]));
                    sp.Add(new MySqlParameter("@nm", r["prsn"]));
                    db.ExeQry(sql, sp);
                    sp.Clear();
                }
                db.Close();
                
                cnt++;

                //부동산일 경우만-토지융합정보와 건축물대장 연동
                if (cat3 == "0" || cat3 == "401110")
                {
                    Prc_Cat(tid, dtLs);
                }
            }

            atomLog.AddLog(string.Format("상세 수집 완료-{0}건", cnt));
        }

        /// <summary>
        /// 물건종별 판단(API 연동)
        /// - 토지 -> 국토교통부_토지융합정보
        /// - 건물 -> 국토교통부_건축물대장 표제부 조회
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="dtLs"></param>
        private void Prc_Cat(string tid, DataTable dtLs)
        {
            int rowCnt, totalCnt;
            string dvsn, pnu, adrs, adrsBldNm = "", adrsDongNm = "", url, urlLand, urlBldg, xml, sql, cat1 = "", cat2 = "", cat3 = "", bldgNm = "";
            string lndcgrCodeNm = "", mainPurpsCd = "", bldNm = "", dongNm = "";
            string aptPtrn = "아파트|아이파크|푸르지오|롯데캐슬|[이e-]+편한세상|두산위브|휴먼시아|우방유쉘|에스케이뷰|센트레빌|유앤아이|엘에이치|아크로리버파크|해링턴|스카이뷰|맨션|데시앙|힐스테이트|래미안|호반베르디움|선수촌|메트로|타워팰리스|꿈에그린|자이|렉스힐|금호타운|" +
                "우방타운|우미린|해피트리|월드메르디앙|예다음|쌍용예가|화성파크드림|부영|보성|동일하이빌|이다음|스위첸|센트럴하이츠|리버[ ]*뷰|캐스빌|현대[홈]*타운|롯데인벤스|우방아이유쉘|메르빌|리버팰리스|골드캐슬|센트럴타운|에스클래스|풍림|청구하이츠|청구타운|뉴타운|풍경채|포레스트|" +
                "센트럴파크|유보라|코아루|휴포레|서희스타힐스|강변타운|무지개타운|삼도뷰엔빌|삼성쉐르빌|성원상떼빌|뜨란채|하늘채|화성타운|숲속마을|태왕하이츠|호반리젠시|삼성명가|현진에버빌|쌍용스윗|노르웨이숲|블루밍|휴플러스|진아리채|코아루|백년가약|수자인|베르디움|" +
                @"더[샵샾]|\d+단지";
            string vilPtrn = "빌라|빌리지";
            string ofiPtrn = "오피스텔";
            string twhPtrn = "연립|주택";

            rowCnt = dtLs.Rows.Count;
            if (rowCnt == 0) return;

            urlLand = "http://apis.data.go.kr/1611000/nsdi/LandMoveService/attr/getLandMoveAttr?serviceKey=" + api.RndSrvKey() + "&numOfRows=10&pageNo=1&pnu=";
            urlBldg = "http://apis.data.go.kr/1613000/BldRgstService_v2/getBrTitleInfo?serviceKey=" + api.RndSrvKey() + "&numOfRows=100&pageNo=1&sigunguCd=";

            XmlDocument doc = new XmlDocument();
            Dictionary<string, string> dict = new Dictionary<string, string>();     //목록 구분(토지, 건물, 집합건물, 자동차, 선박, 어업권 ...
            ArrayList alPrps = new ArrayList();     //mainPurpsCd

            foreach (DataRow row in dtLs.Rows)
            {
                dvsn = row["dvsn"].ToString();
                pnu = string.Format("{0}|{1}", row["pnu"], row["adrs"]);
                if (dict.ContainsKey(dvsn)) continue;

                dict.Add(dvsn, pnu);
            }

            if (dict.ContainsKey("자동차"))
            {
                adrs = dict["자동차"].Split('|')[1];
                Match match = Regex.Match(adrs, @"등록번호[\s\:]+(\d+)(\w)");
                if (match.Success)
                {
                    decimal no = Convert.ToDecimal(match.Groups[1].Value);
                    if (no >= 70 && no <= 79)
                    {
                        if (Regex.IsMatch(adrs, @"서울|대전|대구|부산|광주|울산|인천|제주|세종|경기|강원|경북|경남|전북|전남|충북|충남"))
                        {
                            cat3 = "301012";      //버스
                        }
                        else
                        {
                            cat3 = "301011";      //승합차
                        }
                    }
                    else if (no >= 80 && no <= 97) cat3 = "301013"; //화물차
                    else if (no == 98 || no == 99) cat3 = "301014"; //특수차 -> 기타차량
                    else cat3 = "301010";   //승용차
                }
                else
                {
                    cat3 = "301014";    //기타차량
                }
            }
            else if (dict.ContainsKey("건설기계,중기"))
            {
                adrs = dict["건설기계,중기"].Split('|')[1];
                if (adrs.Contains("덤프트럭")) cat3 = "301110";
                else if (adrs.Contains("굴삭기")) cat3 = "301111";
                else if (adrs.Contains("지게차")) cat3 = "301112";
                else cat3 = "301113";   //기타중기
            }
            else if (dict.ContainsKey("선박"))
            {
                cat3 = "301210";
            }
            else if (dict.ContainsKey("항공기"))
            {
                cat3 = "301310";
            }
            else if (dict.ContainsKey("어업권"))
            {
                cat3 = "401010";
            }
            else if (dict.ContainsKey("광업권"))
            {
                cat3 = "401011";
            }
            else if (dict.ContainsKey("농업권"))
            {
                cat3 = "401012";
            }
            else
            {
                if (dict.Count == 1)
                {
                    if (dict.ContainsKey("토지"))
                    {
                        pnu = dict["토지"].Split('|')[0];
                        url = urlLand + pnu;
                        if (pnu == string.Empty) return;

                        xml = net.GetHtml(url, Encoding.UTF8);
                        if (xml.Contains("totalCount") == false)
                        {
                            return;
                        }

                        doc.LoadXml(xml);
                        XmlNamespaceManager nsmg = new XmlNamespaceManager(doc.NameTable);
                        nsmg.AddNamespace("n", doc.DocumentElement.NamespaceURI);
                        XmlNode nd_total = doc.SelectSingleNode("/n:response/n:totalCount", nsmg);
                        totalCnt = Convert.ToInt16(nd_total.InnerText);
                        if (totalCnt == 0)
                        {
                            return;
                        }

                        foreach (XmlNode item in doc.SelectNodes("/n:response/n:fields/n:field", nsmg))
                        {
                            lndcgrCodeNm = (item.SelectSingleNode("lndcgrCodeNm", nsmg) == null) ? "" : item.SelectSingleNode("lndcgrCodeNm", nsmg).InnerText;
                        }

                        if (lndcgrCodeNm != string.Empty)
                        {
                            //MessageBox.Show(lndcgrCodeNm);
                            if (lndcgrCodeNm == "대") lndcgrCodeNm = "대지";
                            var xRow = dtCatCd.Rows.Cast<DataRow>().Where(t => t["cat3_nm"].ToString() == lndcgrCodeNm).SingleOrDefault();
                            cat3 = (xRow == null) ? string.Empty : xRow["cat3_cd"].ToString();
                            //MessageBox.Show(cat3);
                        }
                    }
                    else if (dict.ContainsKey("건물") || dict.ContainsKey("집합건물"))
                    {
                        if (dict.ContainsKey("건물"))
                        {
                            pnu = dict["건물"].Split('|')[0];
                            adrs = dict["건물"].Split('|')[1];
                        }
                        else
                        {
                            pnu = dict["집합건물"].Split('|')[0];
                            adrs = dict["집합건물"].Split('|')[1];
                        }                        

                        if (Regex.IsMatch(adrs, aptPtrn))
                        {
                            cat3 = "201013";
                        }
                        else if (Regex.IsMatch(adrs, vilPtrn))
                        {
                            cat3 = "201015";
                        }
                        else if (Regex.IsMatch(adrs, ofiPtrn))
                        {
                            cat3 = "201111";
                        }
                        else if (Regex.IsMatch(adrs, twhPtrn))
                        {
                            cat3 = "201014";
                        }
                        else
                        {
                            if (pnu == string.Empty) return;
                            string platGbCd = (Convert.ToDecimal(pnu.Substring(10, 1)) - 1).ToString();
                            string bun = pnu.Substring(11, 4);
                            string ji = pnu.Substring(15, 4);
                            url = urlBldg + pnu.Substring(0, 5) + "&bjdongCd=" + pnu.Substring(5, 5) + "&platGbCd=" + platGbCd + "&bun=" + bun + "&ji=" + ji;

                            xml = net.GetHtml(url, Encoding.UTF8);
                            if (xml.Contains("totalCount") == false)
                            {
                                return;
                            }

                            doc.LoadXml(xml);
                            XmlNamespaceManager nsmg = new XmlNamespaceManager(doc.NameTable);
                            nsmg.AddNamespace("n", doc.DocumentElement.NamespaceURI);
                            XmlNode nd_total = doc.SelectSingleNode("/n:response/n:body/n:totalCount", nsmg);
                            totalCnt = Convert.ToInt16(nd_total.InnerText);
                            if (totalCnt == 0)
                            {
                                return;
                            }

                            if (totalCnt == 1)
                            {
                                bldNm = doc.SelectSingleNode("/n:response/n:body/n:items/n:item/n:bldNm", nsmg).InnerText.Trim();
                                dongNm = doc.SelectSingleNode("/n:response/n:body/n:items/n:item/n:dongNm", nsmg).InnerText.Trim();
                                mainPurpsCd = doc.SelectSingleNode("/n:response/n:body/n:items/n:item/n:mainPurpsCd", nsmg).InnerText.Trim();
                                var xRow = dtBrCd.Rows.Cast<DataRow>().Where(t => t["prps_cd"].ToString() == mainPurpsCd).SingleOrDefault();
                                cat3 = (xRow == null) ? string.Empty : xRow["cat_cd"].ToString();
                            }
                            else
                            {
                                Match match = Regex.Match(adrs, @"\(\w+동,([\w\d]+)\)"); //(xx동, 건물명)
                                if (match.Success)
                                {
                                    adrsBldNm = match.Groups[1].Value.Replace(" ", string.Empty);
                                    adrsDongNm = Regex.Match(adrs, @"(\d+)동").Groups[1].Value;
                                    foreach (XmlNode item in doc.SelectNodes("/n:response/n:body/n:items/n:item", nsmg))
                                    {
                                        bldNm = (item.SelectSingleNode("bldNm", nsmg) == null) ? "" : item.SelectSingleNode("bldNm", nsmg).InnerText.Replace(" ", string.Empty);
                                        dongNm = (item.SelectSingleNode("dongNm", nsmg) == null) ? "" : item.SelectSingleNode("dongNm", nsmg).InnerText;
                                        mainPurpsCd = (item.SelectSingleNode("mainPurpsCd", nsmg)) == null ? "" : item.SelectSingleNode("mainPurpsCd", nsmg).InnerText;
                                        if (bldNm == adrsBldNm && dongNm == adrsDongNm)
                                        {
                                            var xRow = dtBrCd.Rows.Cast<DataRow>().Where(t => t["prps_cd"].ToString() == mainPurpsCd).SingleOrDefault();
                                            cat3 = (xRow == null) ? string.Empty : xRow["cat_cd"].ToString();
                                        }
                                        if (!alPrps.Contains(mainPurpsCd)) alPrps.Add(mainPurpsCd);
                                    }
                                }
                                else
                                {
                                    foreach (XmlNode item in doc.SelectNodes("/n:response/n:body/n:items/n:item", nsmg))
                                    {
                                        mainPurpsCd = (item.SelectSingleNode("mainPurpsCd", nsmg)) == null ? "" : item.SelectSingleNode("mainPurpsCd", nsmg).InnerText;
                                        if (!alPrps.Contains(mainPurpsCd)) alPrps.Add(mainPurpsCd);
                                    }
                                }
                            }
                        }

                        if (cat3 == string.Empty)
                        {
                            if (alPrps.Count == 1)
                            {
                                var xRow = dtBrCd.Rows.Cast<DataRow>().Where(t => t["prps_cd"].ToString() == alPrps[0].ToString()).SingleOrDefault();
                                cat3 = (xRow == null) ? string.Empty : xRow["cat_cd"].ToString();
                            }
                            else
                            {
                                if (dict.ContainsKey("건물"))
                                {
                                    //
                                }
                                else
                                {
                                    if (Regex.IsMatch(adrs.Replace(" ", string.Empty), @"[\w\d]+상가"))
                                    {
                                        cat3 = "201130";    //근린상가
                                    }
                                    if (cat3 == string.Empty && adrsBldNm == string.Empty)
                                    {
                                        Match match = Regex.Match(adrs, @"(\d+)층[ ]*(\d+)호");
                                        if (match.Success)
                                        {
                                            if (Convert.ToDecimal(match.Groups[1].Value) >= 7) cat3 = "201013";     //아파트-7층 이상
                                            else cat3 = "201015";   //다세대-7층 미만
                                        }
                                    }
                                }
                            }

                            if (cat3 == string.Empty)
                            {
                                Match match = Regex.Match(adrs, @"\s[1234가나다라ABCD에이비시씨디]{1,}동[ ]*(\d+층|지하층)", RegexOptions.IgnoreCase);
                                if (match.Success) cat3 = "201015";     //다세대
                            }
                        }

                        if (cat3 == "201013" && adrs.Contains("상가")) cat3 = "201130";             //아파트 -> 근린상가
                        if (cat3 == "201110" && dict.ContainsKey("집합건물")) cat3 = "201130";      //근린생활시설 -> 근린상가
                        if (cat3 == "201210" && dict.ContainsKey("집합건물")) cat3 = "201216";      //공장 -> 아파트형공장(지식산업센터)
                    }
                }
                else
                {
                    if (dict.ContainsKey("건물") || dict.ContainsKey("집합건물"))
                    {
                        if (dict.ContainsKey("집합건물"))
                        {
                            pnu = dict["집합건물"].Split('|')[0];
                            adrs = dict["집합건물"].Split('|')[1];
                        }
                        else
                        {
                            pnu = dict["건물"].Split('|')[0];
                            adrs = dict["건물"].Split('|')[1];
                        }

                        if (Regex.IsMatch(adrs, aptPtrn))
                        {
                            cat3 = "201013";
                        }
                        else if (Regex.IsMatch(adrs, vilPtrn))
                        {
                            cat3 = "201015";
                        }
                        else if (Regex.IsMatch(adrs, ofiPtrn))
                        {
                            //cat3 = "201019";
                            cat3 = "201020";    //기본 오피스텔(주거)로 잡는다. 2021-10-15
                        }
                        else
                        {
                            if (pnu == string.Empty) return;
                            string platGbCd = (Convert.ToDecimal(pnu.Substring(10, 1)) - 1).ToString();
                            string bun = pnu.Substring(11, 4);
                            string ji = pnu.Substring(15, 4);
                            url = urlBldg + pnu.Substring(0, 5) + "&bjdongCd=" + pnu.Substring(5, 5) + "&platGbCd=" + platGbCd + "&bun=" + bun + "&ji=" + ji;

                            xml = net.GetHtml(url, Encoding.UTF8);
                            if (xml.Contains("totalCount") == false)
                            {
                                return;
                            }

                            doc.LoadXml(xml);
                            XmlNamespaceManager nsmg = new XmlNamespaceManager(doc.NameTable);
                            nsmg.AddNamespace("n", doc.DocumentElement.NamespaceURI);
                            XmlNode nd_total = doc.SelectSingleNode("/n:response/n:body/n:totalCount", nsmg);
                            totalCnt = Convert.ToInt16(nd_total.InnerText);
                            if (totalCnt == 0)
                            {
                                return;
                            }

                            if (totalCnt == 1)
                            {
                                bldNm = doc.SelectSingleNode("/n:response/n:body/n:items/n:item/n:bldNm", nsmg).InnerText.Trim();
                                dongNm = doc.SelectSingleNode("/n:response/n:body/n:items/n:item/n:dongNm", nsmg).InnerText.Trim();
                                mainPurpsCd = doc.SelectSingleNode("/n:response/n:body/n:items/n:item/n:mainPurpsCd", nsmg).InnerText.Trim();
                                var xRow = dtBrCd.Rows.Cast<DataRow>().Where(t => t["prps_cd"].ToString() == mainPurpsCd).SingleOrDefault();
                                cat3 = (xRow == null) ? string.Empty : xRow["cat_cd"].ToString();
                            }
                            else
                            {
                                Match match = Regex.Match(adrs, @"\(\w+동,([\w\d]+)\)");
                                if (match.Success)
                                {
                                    adrsBldNm = match.Groups[1].Value.Replace(" ", string.Empty);
                                    adrsDongNm = Regex.Match(adrs, @"(\d+)동").Groups[1].Value;
                                    foreach (XmlNode item in doc.SelectNodes("/n:response/n:body/n:items/n:item", nsmg))
                                    {
                                        bldNm = (item.SelectSingleNode("bldNm", nsmg) == null) ? "" : item.SelectSingleNode("bldNm", nsmg).InnerText.Replace(" ", string.Empty);
                                        dongNm = (item.SelectSingleNode("dongNm", nsmg) == null) ? "" : item.SelectSingleNode("dongNm", nsmg).InnerText;
                                        mainPurpsCd = (item.SelectSingleNode("mainPurpsCd", nsmg)) == null ? "" : item.SelectSingleNode("mainPurpsCd", nsmg).InnerText;
                                        if (bldNm == adrsBldNm && dongNm == adrsDongNm)
                                        {
                                            var xRow = dtBrCd.Rows.Cast<DataRow>().Where(t => t["prps_cd"].ToString() == mainPurpsCd).SingleOrDefault();
                                            cat3 = (xRow == null) ? string.Empty : xRow["cat_cd"].ToString();
                                        }
                                        if (!alPrps.Contains(mainPurpsCd)) alPrps.Add(mainPurpsCd);
                                    }
                                }
                                else
                                {
                                    foreach (XmlNode item in doc.SelectNodes("/n:response/n:body/n:items/n:item", nsmg))
                                    {
                                        mainPurpsCd = (item.SelectSingleNode("mainPurpsCd", nsmg)) == null ? "" : item.SelectSingleNode("mainPurpsCd", nsmg).InnerText;
                                        if (!alPrps.Contains(mainPurpsCd)) alPrps.Add(mainPurpsCd);
                                    }
                                }
                            }
                        }

                        if (cat3 == string.Empty)
                        {
                            if (alPrps.Count == 1)
                            {
                                var xRow = dtBrCd.Rows.Cast<DataRow>().Where(t => t["prps_cd"].ToString() == alPrps[0].ToString()).SingleOrDefault();
                                cat3 = (xRow == null) ? string.Empty : xRow["cat_cd"].ToString();
                            }
                            else
                            {
                                if (dict.ContainsKey("건물"))
                                {
                                    //
                                }
                                else
                                {
                                    if (Regex.IsMatch(adrs.Replace(" ", string.Empty), @"[\w\d]+상가"))
                                    {
                                        cat3 = "201130";    //근린상가
                                    }
                                    if (cat3 == string.Empty && adrsBldNm == string.Empty)
                                    {
                                        Match match = Regex.Match(adrs, @"(\d+)층[ ]*(\d+)호");
                                        if (match.Success)
                                        {
                                            if (Convert.ToDecimal(match.Groups[1].Value) >= 7) cat3 = "201013";     //아파트-7층 이상
                                            else cat3 = "201015";   //다세대-7층 미만
                                        }
                                    }
                                }
                            }

                            if (cat3 == string.Empty)
                            {
                                Match match = Regex.Match(adrs, @"\s[1234가나다라ABCD에이비시씨디]{1,}동[ ]*(\d+층|지하층)", RegexOptions.IgnoreCase);
                                if (match.Success) cat3 = "201015";     //다세대
                            }
                        }

                        if (cat3 == "201013" && adrs.Contains("상가")) cat3 = "201130";             //아파트 -> 근린상가
                        if (cat3 == "201110" && dict.ContainsKey("집합건물")) cat3 = "201130";      //근린생활시설 -> 근린상가
                        if (cat3 == "201210" && dict.ContainsKey("집합건물")) cat3 = "201216";      //공장 -> 아파트형공장(지식산업센터)
                    }
                }
            }

            if (cat3 == string.Empty && dict.ContainsKey("기타"))
            {
                cat3 = "201210";    //공장-대부분 기계/기구 이므로 공장으로 분류
            }

            if (cat3 == string.Empty)
            {
                db.Open();
                sql = "select bldg_nm from ta_list where tid=" + tid;
                MySqlDataReader dr = db.ExeRdr(sql);
                dr.Read();
                if (dr.HasRows)
                {
                    bldgNm = dr["bldg_nm"].ToString().Trim();
                    if (bldgNm != string.Empty)
                    {
                        if (Regex.IsMatch(bldgNm, aptPtrn))
                        {
                            cat3 = "201013";
                        }
                        else if (Regex.IsMatch(bldgNm, vilPtrn))
                        {
                            cat3 = "201015";
                        }
                        else if (Regex.IsMatch(bldgNm, ofiPtrn))
                        {
                            cat3 = "201111";
                        }
                        else if (Regex.IsMatch(bldgNm, twhPtrn))
                        {
                            cat3 = "201014";
                        }
                    }
                }
                dr.Close();
                db.Close();
            }

            if (cat3 == string.Empty)
            {
                cat3 = "401110";    //기타-기타
            }

            cat1 = cat3.Substring(0, 2);
            cat2 = cat3.Substring(0, 4);
            db.Open();
            sql = "update ta_list set cat1=" + cat1 + ", cat2=" + cat2 + ", cat3=" + cat3 + " where tid=" + tid;
            db.ExeQry(sql);
            db.Close();
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
            int landCnt = 0, bldgCnt = 0, multiCnt = 0, issueCnt = 0;
            string autoDvsn = "14";     //발급 구분 -> 예정 물건

            DataTable dt = new DataTable();
            dt.Columns.Add("tid");
            dt.Columns.Add("lsIdx");
            dt.Columns.Add("lsNo");
            dt.Columns.Add("pin");
                        
            sql = "select L.tid,spt,sn1,sn2,cat3, S.idx,no,dvsn,pin,pnu from ta_list L, ta_ls S, ta_dtl D where L.tid=S.tid and S.tid=D.tid and sta2 in (1011,1012) and cat1!=30 and D.pin_land='' and D.pin_bldg='' order by L.tid";
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
                    dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["pin"] });    //대상
                }

                if (rows.Count() == 2)
                {
                    DataTable dtS = rows.CopyToDataTable();
                    landCnt = dtS.Select("dvsn='토지'").Count();
                    bldgCnt = dtS.Select("dvsn='건물'").Count();
                    if (landCnt == 1 && bldgCnt == 1)
                    {
                        DataRow r = dtS.Rows[0];
                        if ($"{r["pin"]}" != string.Empty) dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["pin"] });     //대상
                        r = dtS.Rows[1];
                        if ($"{r["pin"]}" != string.Empty) dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["pin"] });     //대상
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
                        db.ExeQry($"insert into db_tank.tx_rgst_auto set dvsn='{autoDvsn}', tid='{row["tid"]}', ls_no='{row["lsNo"]}', pin='{row["pin"]}', wdt=curdate(), wtm=curtime()");
                        issueCnt++;
                    }
                    db.Close();
                }
            }
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //MessageBox.Show("실행 완료");
            atomLog.AddLog("실행 완료", 1);
            bgwork.Dispose();

            this.Dispose();
            this.Close();
        }
    }
}
