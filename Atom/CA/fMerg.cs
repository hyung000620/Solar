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
    public partial class fMerg : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AtomLog atomLog = new AtomLog(102);
        AuctCd auctCd = new AuctCd();
        ApiUtil api = new ApiUtil();

        DataTable dtStateCd, dtCatCd, dtBrCd;

        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 1500, webCnt = 0;

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        string vmNm = Environment.MachineName;

        public fMerg()
        {
            InitializeComponent();
            this.Shown += FMerg_Shown;
        }

        private void FMerg_Shown(object sender, EventArgs e)
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
            //진행상태 코드
            dtStateCd = db.ExeDt("select sta1_cd, sta2_cd, sta1_nm, sta2_nm from ta_cd_sta order by sta2_cd");

            //물건종별 코드
            dtCatCd = db.ExeDt("select cat1_cd, cat2_cd, cat3_cd, cat3_nm, hide from ta_cd_cat where cat3_cd > 0");

            //건축물용도 코드
            dtBrCd = db.ExeDt("select prps_cd, cat_cd from ta_cd_br");
        }

        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            string sql = "", tid, spt, sn1, sn2, jiwonNm, saNo, url, html;
            string mergStr, tfNote, jiwon, dvsn, n1, n2;
            int totCnt = 0, curCnt = 0;

            if (vmNm == "VM-3")
            {
                sql = "select * from ta_list where (sta1=11 or (sta1=10 and rcp_dt > '0000-00-00' and ini_dt <= date_sub(curdate(),interval 15 day))) and (tid % 2)=1 order by tid";
            }
            else if (vmNm == "VM-4")
            {
                sql = "select * from ta_list where (sta1=11 or (sta1=10 and rcp_dt > '0000-00-00' and ini_dt <= date_sub(curdate(),interval 15 day))) and (tid % 2)=0 order by tid";
            }
            else
            {
                //sql = "select * from ta_list where (sta1=11 or (sta1=10 and rcp_dt > '0000-00-00' and ini_dt <= date_sub(curdate(),interval 15 day))) and (tid % 2)=0 and tid=1953160 order by tid";
            }
            
            DataTable dt = db.ExeDt(sql);
            HAPDoc doc = new HAPDoc();

            totCnt = dt.Rows.Count;
            atomLog.AddLog(string.Format("확인 대상-{0}건", totCnt));

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                tid = row["tid"].ToString();
                spt = row["spt"].ToString();
                sn1 = row["sn1"].ToString();
                sn2 = row["sn2"].ToString();

                txtProgrs.AppendText(string.Format("\r\n> {0} -> {1} / {2}", tid, curCnt, totCnt));     //화면에 처리상태 표시

                jiwonNm = auctCd.FindLawNm(string.Format("{0}", spt), true);
                saNo = sn1 + "0130" + sn2.PadLeft(6, '0');
                url = "http://www.courtauction.go.kr/RetrieveRealEstDetailInqSaList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&srnID=PNO101005&_SRCH_SRNID=PNO101005";
                html = net.GetHtml(url);
                if (html.Contains("검색결과없음")) continue;
                if (html.Contains("HttpWebException"))
                {
                    //Error 기록
                    continue;
                }
                doc.LoadHtml(html);

                //사건기본내역
                HtmlNodeCollection ncTd = doc.DocumentNode.SelectNodes("//table[@summary='사건기본내역 표']/tr/*");
                if (ncTd == null) continue;
                HtmlNode nd = ncTd.Cast<HtmlNode>().Where(n => n.InnerText.Contains("중복/병합/이송")).FirstOrDefault();
                if (nd == null) continue;
                
                mergStr = nd.SelectSingleNode("following-sibling::*[1]").InnerText;
                MatchCollection mc = Regex.Matches(mergStr, @"([\w법지원]*)[\s]*(\d+)타경(\d+)\((중복|병합|이송)\)", rxOptM);
                if (mc.Count == 0) continue;

                foreach (Match match in mc)
                {
                    n1 = match.Groups[2].Value;
                    n2 = match.Groups[3].Value;
                    dvsn = match.Groups[4].Value;
                    jiwon = (dvsn == "이송") ? auctCd.LawNmEnc(match.Groups[1].Value) : jiwonNm;
                    tfNote = (dvsn == "이송") ? match.Value : string.Empty;

                    ProcNew(row, dvsn, jiwon, n1, n2, tfNote);
                }
            }

            atomLog.AddLog("실행 완료", 1);
        }

        /// <summary>
        /// 신규 등록
        /// </summary>
        /// <param name="row"></param>
        /// <param name="dvsn"></param>
        /// <param name="jiwonNm"></param>
        /// <param name="cno1"></param>
        /// <param name="cno2"></param>
        /// <param name="tfNote"></param>
        private void ProcNew(DataRow row, string dvsn, string jiwonNm, string cno1, string cno2, string tfNote)
        {
            string crt, spt, dpt, mno, mno1, mno2, mtid, cno, ctid = "";
            string url, html, saNo, merg;
            string auctNm = "", rcptDt = "", iniDt = "", billAmt = "", appeal = "", endRslt = "", endDt = "", sta1 = "", sta2 = "", auctType = "", frmlType = "";
            string sql = "", lsNo = "", adrs = "", adrsType, regnAdrs, mt, pin = "", sidoCd = "", gugunCd = "", dongCd = "", riCd = "", x = "", y = "";
            string shrDt = "", creditor = "", debtor = "", owner = "";
            int creditorCnt = 0, debtorCnt = 0, ownerCnt = 0;
            bool exist = false;

            webCnt++;
            if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
            
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

            Dictionary<string, string> dictShr = new Dictionary<string, string>();

            //당사자내역
            DataTable dtPrsn = new DataTable();
            dtPrsn.Columns.Add("dvsn");
            dtPrsn.Columns.Add("prsn");

            //관련사건내역
            DataTable dtRCase = new DataTable();
            dtRCase.Columns.Add("crtNm");
            dtRCase.Columns.Add("caseNo");
            dtRCase.Columns.Add("dvsn");

            IDictionary<string, string> dict = new Dictionary<string, string>();
            List<MySqlParameter> sp = new List<MySqlParameter>();

            HAPDoc doc = new HAPDoc();

            spt = row["spt"].ToString();
            crt = spt.Substring(0, 2);
            dpt = row["dpt"].ToString();
            mtid = row["tid"].ToString();
            mno1 = row["sn1"].ToString();
            mno2 = row["sn2"].ToString();
            mno = string.Format("{0}{1}", mno1, mno2.PadLeft(6, '0'));
            cno = string.Format("{0}{1}", cno1, cno2.PadLeft(6, '0'));

            if (dvsn == "병합") merg = "1";
            else if (dvsn == "중복") merg = "2";
            else merg = "";

            db.Open();
            sql = "select tid from ta_list where spt='" + spt + "' and sn1='" + cno1 + "' and sn2='" + cno2 + "' limit 1";
            //exist = db.ExistRow(sql);
            MySqlDataReader mdr = db.ExeRdr(sql);
            if (mdr.HasRows)
            {
                mdr.Read();
                ctid = mdr["tid"].ToString();
                exist = true;
            }
            mdr.Close();
            db.Close();
            //if (exist) return;
            //2021-05-19 해당 물건은 있으나 중/병합 테이블에 없는 경우
            if (exist)
            {
                sql = "select idx from ta_merg where spt='" + spt + "' and mno='" + mno + "' and cno='" + cno + "' and dvsn='" + dvsn + "' limit 1";
                db.Open();
                exist = db.ExistRow(sql);
                db.Close();
                if (!exist)
                {
                    //중복/병합 테이블에 등록
                    db.Open();
                    sql = "insert ignore into ta_merg set spt=@spt, mno=@mno, cno=@cno, mtid=@mtid, ctid=@ctid, dvsn=@dvsn, wdt=CURDATE()";
                    sp.Add(new MySqlParameter("@spt", spt));
                    sp.Add(new MySqlParameter("@mno", mno));
                    sp.Add(new MySqlParameter("@cno", cno));
                    sp.Add(new MySqlParameter("@mtid", mtid));
                    sp.Add(new MySqlParameter("@ctid", ctid));
                    sp.Add(new MySqlParameter("@dvsn", merg));
                    db.ExeQry(sql, sp);
                    sp.Clear();
                    db.Close();
                }

                return;
            }

            saNo = string.Format("{0}0130{1}", cno1, cno2.PadLeft(6, '0'));
            url = "http://www.courtauction.go.kr/RetrieveRealEstDetailInqSaList.laf?";
            url += "jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&srnID=PNO101005&_SRCH_SRNID=PNO101005";
            html = net.GetHtml(url);

            if (html.Contains("14일이 지나지 않았거나")) return;
            //if (html.Contains("종국되고 30일이 경과한")) state = "1";
            //else state = "2";

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

            //배당요구종기내역
            ncTr = doc.DocumentNode.SelectNodes("//table[@summary='배당요구종기내역 표']/tbody/tr");
            if (ncTr != null)
            {
                foreach (HtmlNode ndTr in ncTr)
                {
                    HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                    if (ncTd[0].InnerText.Contains("검색결과가 없습니다")) break;
                    dictShr.Add(ncTd[0].InnerText, Regex.Match(ncTd[2].InnerText.Trim(), @"\d{4}.\d{2}.\d{2}").Value);
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

            //목록내역
            ncTr = doc.DocumentNode.SelectNodes("//table[@summary='목록내역 표']/tbody/tr");
            if (ncTr != null)
            {
                foreach (HtmlNode ndTr in ncTr)
                {
                    HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                    lsNo = Regex.Match(ncTd[0].InnerText, @"\d+").Value;
                    adrs = ncTd[1].InnerText.Trim();
                    adrs = Regex.Replace(adrs, @"[\s]{2,}", " ");
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

            //주소 재매칭 시도
            var rows = from DataRow r in dtLs.Rows where r["sidoCd"].ToString() == string.Empty select r;
            if (rows != null)
            {
                foreach (DataRow r in rows)
                {
                    dict.Clear();
                    AdrsParser parser = new AdrsParser(r["adrs"].ToString());
                    //MessageBox.Show(parser.AdrsM);
                    dict = api.DaumSrchAdrs(parser.AdrsM);
                    r["sidoCd"] = dict["sidoCd"];
                    r["gugunCd"] = dict["gugunCd"];
                    r["dongCd"] = dict["dongCd"];
                    r["riCd"] = dict["riCd"];
                    r["hCd"] = dict["hCd"];
                    r["pnu"] = dict["pnu"];
                    r["zoneNo"] = dict["zoneNo"];
                    r["x"] = dict["x"];
                    r["y"] = dict["y"];
                }
            }
            
            adrs = ""; lsNo = ""; sidoCd = ""; gugunCd = ""; dongCd = ""; riCd = ""; x = ""; y = ""; shrDt = "";
            if (dtLs.Rows.Count > 0)
            {
                adrs = dtLs.Rows[0]["adrs"].ToString();
                lsNo = dtLs.Rows[0]["no"].ToString();
                sidoCd = dtLs.Rows[0]["sidoCd"].ToString();
                gugunCd = dtLs.Rows[0]["gugunCd"].ToString();
                dongCd = dtLs.Rows[0]["dongCd"].ToString();
                riCd = dtLs.Rows[0]["riCd"].ToString();
                x = dtLs.Rows[0]["x"].ToString();
                y = dtLs.Rows[0]["y"].ToString();
                if (dictShr.Count > 0)
                {
                    if (dictShr.ContainsKey(lsNo)) shrDt = dictShr[lsNo];
                }
            }

            sta1 = "10"; sta2 = "1010";
            if (endRslt != "미종국")
            {
                var xSta = from DataRow r in dtStateCd.Rows
                           where r["sta2_nm"].ToString() == endRslt
                           select r;
                if (xSta.Count() > 0)
                {
                    sta1 = xSta.CopyToDataTable().Rows[0]["sta1_cd"].ToString();
                    sta2 = xSta.CopyToDataTable().Rows[0]["sta2_cd"].ToString();
                }
                else
                {
                    if (endDt != "")
                    {
                        sta1 = "14";
                        sta2 = "1412";
                    }
                }
            }

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

            db.Open();
            sql = "insert into ta_list set crt=@crt, spt=@spt, dpt=@dpt, sn1=@sn1, sn2=@sn2, debtor=@debtor, owner=@owner, ini_dt=@ini_dt, shr_dt=@shr_dt, si_cd=@si_cd, gu_cd=@gu_cd, dn_cd=@dn_cd, ri_cd=@ri_cd, x=@x, y=@y, sta1=@sta1, sta2=@sta2, " +
                "adrs=@adrs, adrs_type=@adrs_type, regn_adrs=@regn_adrs, mt=@mt, m_adrs_no=@m_adrs_no, s_adrs_no=@s_adrs_no, road_adrs=@road_adrs, m_bldg_no=@m_bldg_no, s_bldg_no=@s_bldg_no, bldg_nm=@bldg_nm, road_nm=@road_nm, " +
                "merg=@merg, rcp_dt=@rcp_dt, end_dt=@end_dt, creditor=@creditor, auct_type=@auct_type, frml_type=@frml_type, 1st_dt=CURDATE()";
            sp.Add(new MySqlParameter("@crt", crt));
            sp.Add(new MySqlParameter("@spt", spt));
            sp.Add(new MySqlParameter("@dpt", dpt));
            sp.Add(new MySqlParameter("@sn1", cno1));
            sp.Add(new MySqlParameter("@sn2", cno2));
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

            sp.Add(new MySqlParameter("@si_cd", sidoCd));
            sp.Add(new MySqlParameter("@gu_cd", gugunCd));
            sp.Add(new MySqlParameter("@dn_cd", dongCd));
            sp.Add(new MySqlParameter("@ri_cd", riCd));
            sp.Add(new MySqlParameter("@x", x));
            sp.Add(new MySqlParameter("@y", y));
            sp.Add(new MySqlParameter("@sta1", sta1));
            sp.Add(new MySqlParameter("@sta2", sta2));
            sp.Add(new MySqlParameter("@merg", merg));
            sp.Add(new MySqlParameter("@rcp_dt", rcptDt));
            sp.Add(new MySqlParameter("@end_dt", endDt));
            sp.Add(new MySqlParameter("@creditor", creditor));
            sp.Add(new MySqlParameter("@auct_type", auctType));
            sp.Add(new MySqlParameter("@frml_type", frmlType));
            db.ExeQry(sql, sp);
            sp.Clear();

            ctid = ((UInt64)db.LastId()).ToString();
            sql = "insert into ta_dtl set tid=@tid, auct_nm=@auct_nm, bill_amt=@bill_amt, etc_note=@etc_note";
            sp.Add(new MySqlParameter("@tid", ctid));
            sp.Add(new MySqlParameter("@auct_nm", auctNm.Replace("부동산", string.Empty)));
            sp.Add(new MySqlParameter("@bill_amt", billAmt));
            sp.Add(new MySqlParameter("@etc_note", tfNote));
            db.ExeQry(sql, sp);
            sp.Clear();

            foreach (DataRow r in dtLs.Rows)
            {
                sql = "insert into ta_ls (tid, no, adrs, pin, dvsn, note, si_cd, gu_cd, dn_cd, ri_cd, hj_cd, pnu, x, y, zone_no) " +
                    "values (@tid, @no, @adrs, @pin, @dvsn, @note, @si_cd, @gu_cd, @dn_cd, @ri_cd, @hj_cd, @pnu, @x, @y, @zone_no)";
                sp.Add(new MySqlParameter("@tid", ctid));
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
                sql = "insert into ta_rcase set spt=@spt, sn1=@sn1, sn2=@sn2, crt_nm=@crt_nm, case_no=@case_no, dvsn=@dvsn, wdt=CURDATE()";
                sp.Add(new MySqlParameter("@spt", spt));
                sp.Add(new MySqlParameter("@sn1", cno1));
                sp.Add(new MySqlParameter("@sn2", cno2));
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
                sp.Add(new MySqlParameter("@tid", ctid));
                sp.Add(new MySqlParameter("@dvsn", r["dvsn"]));
                sp.Add(new MySqlParameter("@nm", r["prsn"]));
                db.ExeQry(sql, sp);
                sp.Clear();
            }
            
            //중복/병합 테이블에 등록
            sql = "insert ignore into ta_merg set spt=@spt, mno=@mno, cno=@cno, mtid=@mtid, ctid=@ctid, dvsn=@dvsn, wdt=CURDATE()";
            sp.Add(new MySqlParameter("@spt", spt));
            sp.Add(new MySqlParameter("@mno", mno));
            sp.Add(new MySqlParameter("@cno", cno));
            sp.Add(new MySqlParameter("@mtid", mtid));
            sp.Add(new MySqlParameter("@ctid", ctid));
            sp.Add(new MySqlParameter("@dvsn", merg));
            db.ExeQry(sql, sp);
            sp.Clear();

            db.Close();


            //부동산일 경우만-토지융합정보와 건축물대장 연동
            Prc_Cat(ctid, dtLs);
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
            string dvsn, pnu, adrs, adrsBldNm = "", adrsDongNm = "", url, urlLand, urlBldg, xml, sql, cat1 = "", cat2 = "", cat3 = "";
            string lndcgrCodeNm = "", mainPurpsCd = "", bldNm = "", dongNm = "";
            string aptPtrn = "아파트|아이파크|푸르지오|롯데캐슬|이편한세상|두산위브더제니스|휴먼시아|우방유쉘|에스케이뷰|센트레빌|유앤아이|엘에이치|아크로리버파크|해링턴|스카이뷰|맨션|데시앙|힐스테이트|래미안|호반베르디움|선수촌|메트로|타워팰리스|꿈에그린|자이|렉스힐|금호타운|" +
                "우방타운|우미린|해피트리|월드메르디앙|예다음|쌍용예가|화성파크드림|부영|보성|동일하이빌|이다음|" +
                @"더[샵샾]|\d+단지";
            string vilPtrn = "빌라|빌리지";
            string ofiPtrn = "오피스텔";
            string twhPtrn = "연립|주택";

            rowCnt = dtLs.Rows.Count;
            if (rowCnt == 0) return;

            urlLand = "http://apis.data.go.kr/1611000/nsdi/LandMoveService/attr/getLandMoveAttr?serviceKey=" + api.RndSrvKey() + "&numOfRows=10&pageNo=1&pnu=";
            //urlBldg = "http://apis.data.go.kr/1611000/BldRgstService/getBrTitleInfo?serviceKey=" + api.RndSrvKey() + "&numOfRows=100&pageNo=1&sigunguCd=";
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
                            cat3 = "201019";
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

            if (cat3 == string.Empty) return;

            cat1 = cat3.Substring(0, 2);
            cat2 = cat3.Substring(0, 4);
            db.Open();
            sql = "update ta_list set cat1=" + cat1 + ", cat2=" + cat2 + ", cat3=" + cat3 + " where tid=" + tid;
            db.ExeQry(sql);
            db.Close();
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //MessageBox.Show("ok");            
            bgwork.Dispose();

            this.Dispose();
            this.Close();
        }
    }
}
