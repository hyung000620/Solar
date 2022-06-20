using Solar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using hap = HtmlAgilityPack;
using HAPDoc = HtmlAgilityPack.HtmlDocument;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using MySql.Data.MySqlClient;
using System.IO;
using System.Xml;
using System.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Atom.CA
{
    public partial class fPreNoti : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AuctCd auctCd = new AuctCd();
        ApiUtil api = new ApiUtil();
        SpCdtnChk spCdtnChk = new SpCdtnChk();
        AtomLog atomLog = new AtomLog(150);     //로그 생성

        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 1500, webCnt = 0;

        int udtCnt = 0, newCnt = 0;     //금일 신규 물건수(신건, 본물건 전환)

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        DataTable dtDptCd, dtCatCd, dtStateCd, dtFlrCd, dtLeasUseCd;         //법원계, 물건종별, 진행상태, 건물층, 임차인-용도코드
        DataTable dtCarCoCd, dtCarMoCd, dtCarFuel, dtCarTrans;  //차량-제조사, 모델그룹, 사용연료, 변속기형식
        DataTable dtSpcCd;      //특수 조건
        DataTable dtEtcCd;      //기타 모든 코드
        DataTable dtBrCd;       //건축물용도코드

        string filePath;    //로컬 파일저장 경로

        //토지 패턴
        string landPtrn = "대|전|답|과수원|목장용지|임야|광천지|염전|대지|공장용지|학교용지|주차장|주유소용지|창고용지|도로|철도용지|제방|하천|구거|유지|양어장|수도용지|공원|체육용지|유원지|종교용지|사적지|묘지|잡종지";

        //제시외 및 기계기구 패턴
        string etcPtrn1 = @"\d+[\.\s]+\(용도\)(.*)[ ]*\(구조\)(.*)[ ]*\(면적\)[\D ]*(\d[\d\.\,]*)[ ]*([㎡주식개\d\*\(\)\w\, ]+)";     //패턴-1 (1-용도, 2-구조, 3-면적, 4-단위 및 기타)
        string etcPtrn2 = @"\d+[\.\s]+\(용도\)(.*)[ ]*\(구조\)(.*)";                                                                  //패턴-2 (1-용도, 2-구조) -> 패턴-1과 용도와 구조는 동일하나 면적부분이 없음
        string macPtrn = @"기계기구|[a-z]{4,}|\d{4}|\w+[\d]*\-\d+|kw|kva|ton|mm|kg";

        //집합 건물 카테고리(cat3)
        private decimal[] multiBldgArr;

        //숨김 물건종별 카테고리(cat3)
        private readonly decimal[] hideCatArr = new decimal[] { 201012, 201017, 201019, 201115, 201119, 201120, 201124, 201125, 201126, 201127, 201129 };

        FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "CA/", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);

        public fPreNoti()
        {
            InitializeComponent(); 
            this.Shown += FNoti_Shown;
        }

        private void FNoti_Shown(object sender, EventArgs e)
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
            multiBldgArr = auctCd.multiBldgArr;

            //전체 법원별 계코드
            dtDptCd = db.ExeDt("select * from ta_cd_dpt where chk=1");

            //물건종별 코드
            dtCatCd = db.ExeDt("select cat1_cd, cat2_cd, cat3_cd, cat3_nm, hide, bldg_type from ta_cd_cat where cat3_cd > 0");

            //진행상태 코드
            dtStateCd = db.ExeDt("select sta1_cd, sta2_cd, sta1_nm, sta2_nm from ta_cd_sta order by sta2_cd");

            //층수 코드
            dtFlrCd = db.ExeDt("select flr_cd, flr_nm from ta_cd_flr");

            //기타 모든 코드
            dtEtcCd = db.ExeDt("select * from ta_cd_etc order by seq, cd");

            //차량-제조사, 모델그룹, 변속기형식, 사용연료 코드
            dtCarCoCd = db.ExeDt("select co_cd, rx from ta_cd_carco where co_cd != 6");
            dtCarMoCd = db.ExeDt("select co_cd, mo_cd, rx from ta_cd_carmo");

            dtCarTrans = dtEtcCd.Select("dvsn=14").CopyToDataTable();
            dtCarFuel = dtEtcCd.Select("dvsn=15").CopyToDataTable();

            //임차인-용도 코드
            dtLeasUseCd = dtEtcCd.Select("dvsn=16").CopyToDataTable();

            //특수 조건
            dtSpcCd = dtEtcCd.Select("dvsn=18").CopyToDataTable();

            //건축물용도 코드
            dtBrCd = db.ExeDt("select prps_cd, cat_cd from ta_cd_br");

            //파일저장 디렉토리 생성
            filePath = @"C:\Atom\CA\" + DateTime.Today.ToShortDateString();
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }
        }

        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            //선행 공고 유무 체크
            atomLog.AddLog("▼ 선행공고 유무 체크");
            Prc_NtChk();
            
            //토지/건물 현황 pre_prc=2
            atomLog.AddLog("▼ 토지/건물 현황");
            Prc_Dtl();
            
            //제시외 업데이트
            atomLog.AddLog("▼ 제시외 현황 업데이트");
            Prc_Oth();

            //차량/중기/선박 목록주소 수정(단, 물건번호 분리전 물건+작업전 물건)
            atomLog.AddLog("▼ 차량/중기/선박 목록주소 업데이트");
            Prc_CarsAdrs();

            //등기 자동발급 대상 추가
            atomLog.AddLog("▼ 등기 자동발급 대상");
            Prc_RgstIssueAdd();

            //역세권 매칭
            atomLog.AddLog("▼ 역세권");
            Prc_Station();

            //집합건물(아파트)코드 매칭
            atomLog.AddLog("▼ 집합건물코드");
            Prc_AptCd();

            //파일-물건사진 pre_prc=3
            atomLog.AddLog("▼ 파일수집(사진)");
            Prc_PhotoFile();

            //파일-문서(사건내역, 기일내역, 문건송달, 표시목록)
            atomLog.AddLog("▼ 파일수집(문서)");
            Prc_DocsFile();

            //사용승인일자 pre_prc=4
            atomLog.AddLog("▼ 사용승인일자");
            Prc_AprvDt();
            
            //토지이용계획(용도지역/지구)
            atomLog.AddLog("▼ 토지이용계획");
            Prc_LandUse();

            //개별공시지가
            atomLog.AddLog("▼ 개별공시지가");
            Prc_LandPrice();

            atomLog.AddLog("실행 완료", 1);
        }

        /// <summary>
        /// 화면에 진행상태 표시
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="depth"></param>
        private void progrsView(string msg, int depth = 0)
        {
            if (depth == 0) msg = string.Format("\r\n##### {0} #####\r\n", msg);
            else if (depth == 1) msg = string.Format("> {0}", msg);
            else if (depth == 2) msg = string.Format(">> {0}", msg);
            else if (depth == 3) msg = string.Format(">>> {0}", msg);
            else if (depth == 4) msg = string.Format(">>>> {0}", msg);
            else if (depth == 5) msg = string.Format(">>>>> {0}", msg);

            txtProgrs.AppendText("\r\n" + msg);
        }

        /// <summary>
        /// 선행 공고 유무 체크
        /// </summary>
        private void Prc_NtChk()
        {
            int i = 0, bgn = 0, end = 0;
            DateTime today, targetDt;
            string jiwonNm, dptCd, csCd;
            string url, html;            
            List<string> lsDt = new List<string>();
            today = DateTime.Now;

            DataTable dtLaw = auctCd.DtLawInfo();
            HAPDoc doc = new HAPDoc();

            //법원-계-일자별 루프돌면서 선행 공고 있는지 체크
            progrsView("선행 공고 체크");

            bgn = 15;
            end = 21;
            //bgn = 22; end = 28;     //Test
            for (i = bgn; i <= end; i++)
            {
                targetDt = today.AddDays(i);
                if (targetDt.DayOfWeek == DayOfWeek.Saturday || targetDt.DayOfWeek == DayOfWeek.Sunday) continue;

                lsDt.Add(string.Format("{0:yyyyMMdd}", targetDt));
            }

            foreach (string bidDt in lsDt)
            {
                foreach (DataRow lawRow in dtLaw.Rows)
                {
                    jiwonNm = auctCd.LawNmEnc(lawRow["lawNm"]);
                    csCd = lawRow["csCd"].ToString();
                    //if (Convert.ToInt16(csCd) != 2011) continue; //Test
                    DataRow[] rows = dtDptCd.Select("cs_cd='" + csCd + "'");                    
                    foreach (DataRow dptRow in rows)
                    {
                        webCnt++;
                        if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                        progrsView(string.Format("{0}-{1}-{2}", lawRow["lawNm"], dptRow["dpt_nm"], bidDt), 1);

                        dptCd = dptRow["dpt_cd"].ToString();
                        //if (dptCd != "1014") continue;  //Test
                        url = "http://www.courtauction.go.kr/RetrieveRealEstMgakNotifySrchGyulgwa.laf?ipchalGbnCd=000331&jiwonNm=" + jiwonNm + "&maeGiil=" + bidDt + "&jpDeptCd=" + dptCd;
                        html = net.GetHtml(url);
                        if (html.Contains("검색결과가 없습니다") || html.Contains("HttpWebException")) continue;

                        Prc_Nt(csCd, dptCd, bidDt, html);
                    }
                }
            }

            atomLog.AddLog(string.Format("▶▶▶ 신규 등록-{0}, 본물건 전환-{1}, 합계-{2}건", newCnt, udtCnt, (newCnt + udtCnt)));
        }

        /// <summary>
        /// 공고 목록에서 신건 검출
        /// </summary>
        /// <param name="csCd"></param>
        /// <param name="dptCd"></param>
        /// <param name="bidDt"></param>
        /// <param name="html"></param>
        private void Prc_Nt(string csCd, string dptCd, string bidDt, string html)
        {
            int i = 0, tdCnt, trCnt, rowSpan = 0, rowCnt = 0;
            string saNo = "", pdNo = "", sn1 = "", sn2 = "", apslAmt = "", minbAmt = "", minbAmt2 = "", bidTm = "", bidTm2 = "", bidCnt = "", use = "", adrsNdtl = "", note = "";
            string prevSaNo = "", prevPdNo = "";
            string sql;

            DataTable dtSa = new DataTable();
            dtSa.Columns.Add("saNo");
            dtSa.Columns.Add("pdNo");
            dtSa.Columns.Add("pdNoTk");
            dtSa.Columns.Add("apslAmt");
            dtSa.Columns.Add("minbAmt");
            dtSa.Columns.Add("minbAmt2");
            dtSa.Columns.Add("bidTm");
            dtSa.Columns.Add("bidTm2");
            dtSa.Columns.Add("bidCnt");
            dtSa.Columns.Add("use");
            dtSa.Columns.Add("adrsNdtl");
            dtSa.Columns.Add("note");

            Dictionary<int, string> dicBidTm = new Dictionary<int, string>();
            dicBidTm[1] = string.Empty;
            dicBidTm[2] = string.Empty;
            
            HAPDoc doc = new HAPDoc();

            doc.LoadHtml(html);
            HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='원공고내역 표']/tbody/tr");
            trCnt = ncTr.Count;
            db.Open();
            foreach (HtmlNode tr in ncTr)
            {
                minbAmt2 = "";
                bidTm2 = "";

                tdCnt = tr.SelectNodes("./td").Count;
                
                if (tdCnt == 6)
                {
                    saNo = tr.SelectNodes("./td")[0].SelectSingleNode("./a/b").InnerText;
                    pdNo = tr.SelectNodes("./td")[1].InnerText;
                    rowSpan = Convert.ToInt32(tr.SelectNodes("./td")[1].Attributes["rowspan"].Value);
                    HtmlNodeCollection ncAmtTm = tr.SelectNodes("./td")[4].SelectNodes("./div/text()");
                    apslAmt = ncAmtTm[0].InnerText.Trim();
                    minbAmt = ncAmtTm[1].InnerText.Trim();
                    bidTm = ncAmtTm[2].InnerText.Trim();
                    if (ncAmtTm.Count == 5)
                    {
                        minbAmt2 = ncAmtTm[3].InnerText.Trim();
                        bidTm2 = ncAmtTm[4].InnerText.Trim();
                    }
                    use = tr.SelectNodes("./td")[2].InnerHtml;
                    adrsNdtl = "<div>" + tr.SelectNodes("./td")[3].InnerHtml + "</div>";
                    //note = tr.SelectNodes("./td")[5].InnerHtml;
                    note = tr.SelectNodes("./td")[5].InnerText.Replace("&nbsp;", string.Empty).Replace("<br>", string.Empty).Trim();
                }
                else if (tdCnt == 5)
                {
                    pdNo = tr.SelectNodes("./td")[0].InnerText;
                    rowSpan = Convert.ToInt32(tr.SelectNodes("./td")[0].Attributes["rowspan"].Value);
                    HtmlNodeCollection ncAmtTm = tr.SelectNodes("./td")[3].SelectNodes("./div/text()");
                    apslAmt = ncAmtTm[0].InnerText.Trim();
                    minbAmt = ncAmtTm[1].InnerText.Trim();
                    bidTm = ncAmtTm[2].InnerText.Trim();
                    if (ncAmtTm.Count == 5)
                    {
                        minbAmt2 = ncAmtTm[3].InnerText.Trim();
                        bidTm2 = ncAmtTm[4].InnerText.Trim();
                    }
                    use = tr.SelectNodes("./td")[1].InnerHtml;
                    adrsNdtl = "<div>" + tr.SelectNodes("./td")[2].InnerHtml + "</div>";
                    //note = tr.SelectNodes("./td")[4].InnerHtml;
                    note = tr.SelectNodes("./td")[4].InnerText.Replace("&nbsp;", string.Empty).Replace("<br>", string.Empty).Trim();
                }
                else if (tdCnt == 1)
                {
                    adrsNdtl += "\r\n<div>" + tr.SelectNodes("./td")[0].InnerHtml + "</div>";
                }

                i++;
                if (rowSpan == i && ((saNo != prevSaNo) || (saNo == prevSaNo && pdNo != prevPdNo)))
                {
                    apslAmt = apslAmt.Replace(",", string.Empty);
                    Match match = Regex.Match(minbAmt, @"(\d{1,3}(,\d{3})+)", rxOptM);
                    minbAmt = match.Groups[1].Value.Replace(",", string.Empty);

                    match = Regex.Match(saNo, @"(\d+)타경(\d+)", RegexOptions.Multiline);
                    sn1 = match.Groups[1].Value;
                    sn2 = match.Groups[2].Value;

                    if (pdNo == "1")
                        sql = "select tid, pn, bid_dt from ta_list where spt=" + csCd + " and sn1=" + sn1 + " and sn2=" + sn2 + " and sta1 > 10 and pn in (0,1) limit 1";
                    else
                        sql = "select tid, pn, bid_dt from ta_list where spt=" + csCd + " and sn1=" + sn1 + " and sn2=" + sn2 + " and pn=" + pdNo + " and sta1 > 10 limit 1";
                    
                    if (db.ExistRow(sql) == false)
                    {
                        match = Regex.Match(bidTm, @"\d{2}:\d{2}", rxOptM);
                        bidTm = (match.Success) ? match.Value + ":00" : string.Empty;
                        match = Regex.Match(bidTm2, @"\d{2}:\d{2}", rxOptM);
                        bidTm2 = (match.Success) ? match.Value + ":00" : string.Empty;
                        bidCnt = (bidTm2 == string.Empty) ? "1" : "2";

                        DataRow row = dtSa.NewRow();
                        row["saNo"] = saNo;
                        row["pdNo"] = pdNo;
                        row["pdNoTk"] = (pdNo == "1") ? "0" : pdNo;
                        row["apslAmt"] = apslAmt;
                        row["minbAmt"] = minbAmt;
                        row["minbAmt2"] = minbAmt2.Replace(",", string.Empty);
                        row["bidTm"] = bidTm;
                        row["bidTm2"] = bidTm2;
                        row["bidCnt"] = bidCnt;
                        row["use"] = use;
                        row["adrsNdtl"] = adrsNdtl;
                        row["note"] = note;
                        dtSa.Rows.Add(row);

                        if (bidTm != string.Empty) dicBidTm[1] = bidTm;
                        if (bidTm2 != string.Empty) dicBidTm[2] = bidTm2;
                    }
                    i = 0;

                    prevSaNo = saNo;
                    prevPdNo = pdNo;
                }
            }
            db.Close();

            rowCnt = dtSa.Rows.Count;
            if (rowCnt > 0)
            {
                //입찰일정 등록
                bidCnt = (dicBidTm[2] == string.Empty) ? "1" : "2";                
                sql = "insert ignore into ta_skd set spt=" + csCd + ", dpt=" + dptCd + ", bid_dt='" + bidDt + "', bid_cnt=" + bidCnt + ", bid_tm1='" + dicBidTm[1] + "', bid_tm2='" + dicBidTm[2] + "', bid_tm3='', wdt=curdate()";
                db.Open();
                db.ExeQry(sql);
                db.Close();

                //내부 물건번호 0 / 1 판별
                foreach (DataRow row in dtSa.Rows)
                {
                    int rowIdx = dtSa.Rows.IndexOf(row);
                    if (rowIdx == (rowCnt - 1)) break;
                    if (row["pdNo"].ToString() != "1") continue;

                    if (row["saNo"].ToString() == dtSa.Rows[rowIdx + 1]["saNo"].ToString())
                    {
                        row["pdNoTk"] = "1";
                    }
                }

                //대상 사건목록 처리
                Prc_Lst(csCd, dptCd, bidDt, dtSa);
            }
        }

        /// <summary>
        /// 기본 정보 저장-사건내역
        /// </summary>
        /// <param name="spt"></param>
        /// <param name="dpt"></param>
        /// <param name="bidDt"></param>
        /// <param name="dtSa"></param>
        private void Prc_Lst(string spt, string dpt, string bidDt, DataTable dtSa)
        {
            string jiwonNm = "", url = "", html = "", saNo = "", sn1 = "", sn2 = "";
            string auctNm = "", rcptDt = "", iniDt = "", billAmt = "", appeal = "", endRslt = "", endDt = "", sta1 = "", sta2 = "", auctType = "", frmlType = "";
            string sql = "", cvp = "", lsNo = "", adrs = "", adrsType, regnAdrs, mt, pin = "", sidoCd = "", gugunCd = "", dongCd = "", riCd = "", x = "", y = "";
            string dbMode = "", crt = "", shrDt = "", tid = "", useCat = "", pdNote = "", dpstRate = "", creditor = "", debtor = "", owner = "", dpslDvsn = "", adrsNdtl = "";
            int rowIdx = 0, lotCnt = 0, hoCnt = 0, creditorCnt = 0, debtorCnt = 0, ownerCnt = 0;
            int eaNewCnt = 0, eaUdtCnt = 0;
            bool eqFlag = false;

            bool macExist = false, othFlag = false;
            string use, strt, area, etcStr;
            
            //제시외
            DataTable dtE = new DataTable();
            dtE.Columns.Add("lsNo");
            dtE.Columns.Add("state");
            dtE.Columns.Add("struct");
            dtE.Columns.Add("sqm");

            HAPDoc doc = new HAPDoc();

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
            dtLs.Columns.Add("aply");

            Dictionary<string, string> dictShr = new Dictionary<string, string>();

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

            IDictionary<string, string> dict = new Dictionary<string, string>();

            jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", spt));
            crt = spt.Substring(0, 2);
            
            foreach (DataRow row in dtSa.Rows)
            {
                rowIdx = dtSa.Rows.IndexOf(row);
                eqFlag = false;
                macExist = false;
                othFlag = false;

                dtLs.Rows.Clear();
                dtPrsn.Rows.Clear();
                dtRCase.Rows.Clear();
                dtE.Rows.Clear();
                dictShr.Clear();
                creditor = ""; debtor = ""; owner = ""; auctNm = "";

                pdNote = row["note"].ToString();
                adrsNdtl = row["adrsNdtl"].ToString();
                useCat = row["use"].ToString();

                Match m = Regex.Match(row["saNo"].ToString(), @"(\d+)타경(\d+)");
                sn1 = m.Groups[1].Value;
                sn2 = m.Groups[2].Value;
                saNo = string.Format("{0}0130{1}", sn1, sn2.PadLeft(6, '0'));

                progrsView(string.Format("[기본정보] {0}-{1} ({2})", sn1, sn2, row["pdNo"]), 3);      //진행상태

                if (rowIdx > 0)
                {
                    if (row["saNo"].ToString() == dtSa.Rows[rowIdx - 1]["saNo"].ToString()) eqFlag = true;
                }

                if (eqFlag == false)
                {
                    webCnt++;
                    if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                    url = "http://www.courtauction.go.kr/RetrieveRealEstDetailInqSaList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
                    html = net.GetHtml(url);
                    doc.LoadHtml(html);
                }

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
                if (eqFlag == false)
                {
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

                //물건내역(물건번호별)
                HtmlNodeCollection ncTbl = doc.DocumentNode.SelectNodes("//table[@summary='물건내역 표']");
                if (ncTbl == null || doc.DocumentNode.InnerText.Contains("등록된 물건내역이 없습니다") || (ncTbl != null && ncTbl[0].InnerText.Contains("검색결과가 없습니다")))
                {
                    //continue;   //임시
                    //정식 매각공고전 물건번호가 분리안된 경우-공고목록에서 매칭한다.
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
                            dr["aply"] = "N";
                            dtLs.Rows.Add(dr);
                        }                        
                    }

                    //물건번호를 가지는 경우에만 번호별로 목록매칭
                    if (row["pdNoTk"].ToString() == "0")
                    {
                        foreach (DataRow r in dtLs.Rows)
                        {
                            r["aply"] = "Y";
                        }

                        MatchCollection mc = Regex.Matches(adrsNdtl, @"<div>(.*?)</div>", rxOptS);
                        foreach (Match ma in mc)
                        {
                            adrs = ma.Groups[1].Value;
                            adrs = Regex.Replace(adrs, @"<br>.*|&nbsp;|\n", string.Empty, rxOptS);
                            foreach (DataRow r in dtLs.Rows)
                            {
                                if (r["adrs"].ToString().Replace(" ", string.Empty) == adrs.Replace(" ", string.Empty))
                                {
                                    if (ma.Groups[1].Value.Contains("제시외"))
                                    {
                                        etcStr = Regex.Match(ma.Groups[1].Value, @"제시외(.*)", rxOptS).Groups[1].Value.Replace("<br>", "\r\n");
                                        etcStr = etcStr.Replace(" 등", "등");
                                        string[] etcStrArr = etcStr.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                                        foreach (string str in etcStrArr)
                                        {
                                            Match me = Regex.Match(str, @"(\w+)[ ](.*)[ ]([\d.,]+)㎡", rxOptM);
                                            if (me.Success)
                                            {
                                                dtE.Rows.Add(r["no"].ToString(), me.Groups[1].Value, me.Groups[2].Value, me.Groups[3].Value);
                                            }
                                            else
                                            {
                                                if (Regex.IsMatch(str, macPtrn, rxOptM))
                                                {
                                                    macExist = true;
                                                }
                                                else
                                                {
                                                    dtE.Rows.Add(r["no"].ToString(), str, "", "");
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        MatchCollection mc = Regex.Matches(adrsNdtl, @"<div>(.*?)</div>", rxOptS);
                        foreach (Match ma in mc)
                        {
                            adrs = ma.Groups[1].Value;
                            adrs = Regex.Replace(adrs, @"<br>.*|&nbsp;|\n", string.Empty, rxOptS);                            
                            foreach (DataRow r in dtLs.Rows)
                            {
                                if (r["adrs"].ToString().Replace(" ", string.Empty) == adrs.Replace(" ", string.Empty))
                                {
                                    r["aply"] = "Y";
                                    if (ma.Groups[1].Value.Contains("제시외"))
                                    {
                                        etcStr = Regex.Match(ma.Groups[1].Value, @"제시외(.*)", rxOptS).Groups[1].Value.Replace("<br>", "\r\n");
                                        etcStr = etcStr.Replace(" 등", "등");
                                        string[] etcStrArr = etcStr.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                                        foreach (string str in etcStrArr)
                                        {
                                            Match me = Regex.Match(str, @"(\w+)[ ](.*)[ ]([\d.,]+)㎡", rxOptM);
                                            if (me.Success)
                                            {
                                                dtE.Rows.Add(r["no"].ToString(), me.Groups[1].Value, me.Groups[2].Value, me.Groups[3].Value);
                                            }
                                            else
                                            {
                                                if (Regex.IsMatch(str, macPtrn, rxOptM))
                                                {
                                                    macExist = true;
                                                }
                                                else
                                                {
                                                    dtE.Rows.Add(r["no"].ToString(), str, "", "");
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        for (int i = dtLs.Rows.Count - 1; i >= 0; i--)
                        {
                            DataRow r = dtLs.Rows[i];
                            if (r["aply"].ToString() == "N") r.Delete();
                        }
                        dtLs.AcceptChanges();
                    }
                    //제시외물건 갱신 대상
                    othFlag = true;
                }
                else
                {
                    //물건내역이 있는 경우-물건번호가 분리됨
                    //continue;   //Test
                    foreach (HtmlNode tbl in ncTbl)
                    {   
                        if (tbl.SelectSingleNode("./tr/td").InnerText.Trim() != row["pdNo"].ToString()) continue;
                        ncTr = tbl.SelectNodes("./tr");
                        foreach (HtmlNode ndTr in ncTr)
                        {
                            HtmlNodeCollection ncTd = ndTr.SelectNodes("./th|./td");
                            string colName = ncTd[0].InnerText.Trim();
                            if (ncTd[0].Name == "th" && colName.Contains("목록"))
                            {
                                lsNo = Regex.Match(colName, @"\d+").Value;
                                adrs = ncTd[1].InnerText.Trim();
                                adrs = Regex.Replace(adrs, @"[\s]{2,}", " ");
                                pin = Regex.Match(ncTd[1].InnerHtml, @"regiBu\('(\d+)'\)", RegexOptions.IgnoreCase).Groups[1].Value;

                                dict.Clear();
                                dict = api.DaumSrchAdrs(adrs);
                                DataRow dr = dtLs.NewRow();
                                dr["no"] = lsNo;
                                dr["adrs"] = adrs;
                                dr["pin"] = pin;
                                dr["dvsn"] = ncTd[3].InnerText.Trim();
                                dr["note"] = ncTd[5].InnerText.Trim();
                                dr["sidoCd"] = dict["sidoCd"];
                                dr["gugunCd"] = dict["gugunCd"];
                                dr["dongCd"] = dict["dongCd"];
                                dr["riCd"] = dict["riCd"];
                                dr["hCd"] = dict["hCd"];
                                dr["pnu"] = dict["pnu"];
                                dr["zoneNo"] = dict["zoneNo"];
                                dr["x"] = dict["x"];
                                dr["y"] = dict["y"];
                                dr["aply"] = "Y";
                                dtLs.Rows.Add(dr);
                            }
                            if (ncTd[0].Name == "th" && colName.Contains("제시외"))
                            {
                                etcStr = ncTd[1].InnerHtml.Trim().Replace("<br>", "\r\n");
                                MatchCollection mc = Regex.Matches(etcStr, etcPtrn1, rxOptM);
                                if (mc.Count > 0)
                                {
                                    foreach (Match ma in mc)
                                    {
                                        use = ma.Groups[1].Value.Trim();
                                        strt = ma.Groups[2].Value.Trim();
                                        area = ma.Groups[3].Value.Trim();
                                        if (use.Contains("기계기구"))
                                        {
                                            macExist = true;
                                            continue;
                                        }
                                        if (ma.Value.Contains("수목") || ma.Value.Contains("나무") || ma.Value.Contains("관정"))
                                        {
                                            area = string.Empty;
                                        }
                                        else
                                        {
                                            if (Regex.IsMatch(strt, macPtrn, rxOptM))
                                            {
                                                macExist = true;
                                                continue;
                                            }
                                        }
                                        dtE.Rows.Add(lsNo, use, strt, area);
                                    }
                                }
                                else
                                {
                                    mc = Regex.Matches(etcStr, etcPtrn2, rxOptM);
                                    if (mc.Count > 0)
                                    {
                                        foreach (Match ma in mc)
                                        {
                                            use = ma.Groups[1].Value.Trim();
                                            strt = ma.Groups[2].Value.Trim();
                                            if (use.Contains("기계기구"))
                                            {
                                                macExist = true;
                                                continue;
                                            }
                                            if (ma.Value.Contains("수목") || ma.Value.Contains("나무") || ma.Value.Contains("관정"))
                                            {
                                                area = string.Empty;
                                            }
                                            else
                                            {
                                                if (Regex.IsMatch(strt, macPtrn, rxOptM))
                                                {
                                                    macExist = true;
                                                    continue;
                                                }
                                            }
                                            dtE.Rows.Add(lsNo, use, strt, "");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                //continue;   //Test

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

                //DB 기본 사건정보 저장
                List<MySqlParameter> sp = new List<MySqlParameter>();
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

                sta1 = "11"; sta2 = "1110";
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
                }

                Match match = Regex.Match(pdNote, @"보증금.*?(\d+)%", rxOptS);
                dpstRate = (match.Success) ? match.Groups[1].Value : "10";

                //특수 검색조건 키워드 검출
                string spCdtn = Spl_Keyword(pdNote);

                //주소 상세분석
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

                dpslDvsn = Dpsl_DvsnCd(jiwonNm, saNo, pdNote, dtLs);

                db.Open();
                sql = "select tid from ta_list where spt=" + spt + " and sn1=" + sn1 + " and sn2=" + sn2 + " and sta1=10 limit 1";
                MySqlDataReader mdr = db.ExeRdr(sql);

                cvp = "crt=@crt, spt=@spt, dpt=@dpt, sn1=@sn1, sn2=@sn2, pn=@pn, apsl_amt=@apsl_amt, minb_amt=@minb_amt, rcp_dt=@rcp_dt, ini_dt=@ini_dt, shr_dt=@shr_dt, end_dt=@end_dt, bid_dt=@bid_dt, " +
                    "creditor=@creditor, debtor=@debtor, owner=@owner, dpst_type=@dpst_type, dpst_rate=@dpst_rate, auct_type=@auct_type, frml_type=@frml_type, dpsl_dvsn=@dpsl_dvsn, " +
                    "adrs=@adrs, adrs_type=@adrs_type, regn_adrs=@regn_adrs, mt=@mt, m_adrs_no=@m_adrs_no, s_adrs_no=@s_adrs_no, road_adrs=@road_adrs, m_bldg_no=@m_bldg_no, s_bldg_no=@s_bldg_no, bldg_nm=@bldg_nm, road_nm=@road_nm, " +
                    "bid_tm=@bid_tm, bid_tm1=@bid_tm1, bid_tm2=@bid_tm2, bid_cnt=@bid_cnt, pre_dt=CURDATE(), pre_tm=CURTIME(), " +
                    "ls_no=@ls_no, si_cd=@si_cd, gu_cd=@gu_cd, dn_cd=@dn_cd, ri_cd=@ri_cd, x=@x, y=@y, sta1=@sta1, sta2=@sta2, sp_cdtn=@sp_cdtn, auto_prc=1";
                if (mdr.HasRows)
                {
                    dbMode = "Update";
                    mdr.Read();
                    tid = mdr["tid"].ToString();
                    //sql = "update ta_list set " + cvp + ", 2nd_dt=CURDATE() where tid='" + tid + "'";
                    sql = "update ta_list set " + cvp + " where tid='" + tid + "'";
                    udtCnt++;
                    eaUdtCnt++;
                }
                else
                {
                    dbMode = "Insert";
                    //sql = "insert into ta_list set " + cvp + ", 1st_dt=CURDATE(), 2nd_dt=CURDATE()";
                    sql = "insert into ta_list set " + cvp + ", 1st_dt=CURDATE()";
                    newCnt++;
                    eaNewCnt++;
                }
                mdr.Close();

                sp.Add(new MySqlParameter("@crt", crt));
                sp.Add(new MySqlParameter("@spt", spt));
                sp.Add(new MySqlParameter("@dpt", dpt));
                sp.Add(new MySqlParameter("@sn1", sn1));
                sp.Add(new MySqlParameter("@sn2", sn2));
                sp.Add(new MySqlParameter("@pn", row["pdNoTk"]));
                sp.Add(new MySqlParameter("@apsl_amt", row["apslAmt"]));
                sp.Add(new MySqlParameter("@minb_amt", row["minbAmt"]));
                sp.Add(new MySqlParameter("@rcp_dt", rcptDt));
                sp.Add(new MySqlParameter("@ini_dt", iniDt));
                sp.Add(new MySqlParameter("@shr_dt", shrDt));
                sp.Add(new MySqlParameter("@end_dt", endDt));
                sp.Add(new MySqlParameter("@bid_dt", bidDt));                
                sp.Add(new MySqlParameter("@creditor", creditor));
                sp.Add(new MySqlParameter("@debtor", debtor));
                sp.Add(new MySqlParameter("@owner", owner));
                sp.Add(new MySqlParameter("@dpst_type", 1));    //보증금율 구분(기본1-최저가)
                sp.Add(new MySqlParameter("@dpst_rate", dpstRate));
                sp.Add(new MySqlParameter("@auct_type", auctType));
                sp.Add(new MySqlParameter("@frml_type", frmlType));
                sp.Add(new MySqlParameter("@dpsl_dvsn", dpslDvsn));

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

                sp.Add(new MySqlParameter("@ls_no", lsNo));
                sp.Add(new MySqlParameter("@si_cd", sidoCd));
                sp.Add(new MySqlParameter("@gu_cd", gugunCd));
                sp.Add(new MySqlParameter("@dn_cd", dongCd));
                sp.Add(new MySqlParameter("@ri_cd", riCd));
                sp.Add(new MySqlParameter("@x", x));
                sp.Add(new MySqlParameter("@y", y));
                sp.Add(new MySqlParameter("@sta1", sta1));
                sp.Add(new MySqlParameter("@sta2", sta2));
                sp.Add(new MySqlParameter("@sp_cdtn", spCdtn));

                sp.Add(new MySqlParameter("@bid_tm", row["bidTm"]));
                sp.Add(new MySqlParameter("@bid_tm1", row["bidTm"]));
                sp.Add(new MySqlParameter("@bid_tm2", row["bidTm2"]));
                sp.Add(new MySqlParameter("@bid_cnt", row["bidCnt"]));
                db.ExeQry(sql, sp);
                sp.Clear();

                if (dbMode == "Insert")
                {
                    tid = ((UInt64)db.LastId()).ToString();
                    sql = "insert into ta_dtl (tid, bill_amt, auct_nm, pd_note) values(@tid, @bill_amt, @auct_nm, @pd_note)";
                    sp.Add(new MySqlParameter("@tid", tid));
                    sp.Add(new MySqlParameter("@bill_amt", billAmt));
                    sp.Add(new MySqlParameter("@auct_nm", auctNm.Replace("부동산", string.Empty)));
                    sp.Add(new MySqlParameter("@pd_note", pdNote));
                    db.ExeQry(sql, sp);
                    sp.Clear();
                }
                else
                {
                    sql = "update ta_dtl set pd_note=@pd_note where tid=@tid";
                    sp.Add(new MySqlParameter("@tid", tid));
                    sp.Add(new MySqlParameter("@pd_note", pdNote));
                    db.ExeQry(sql, sp);
                    sp.Clear();

                    sql = "delete from ta_ls where tid='" + tid + "'";
                    db.ExeQry(sql);
                }

                lotCnt = 0;
                hoCnt = 0;
                foreach (DataRow r in dtLs.Rows)
                {
                    sql = "insert into ta_ls (tid, no, adrs, pin, dvsn, note, si_cd, gu_cd, dn_cd, ri_cd, hj_cd, pnu, x, y, zone_no) ";
                    sql += "values (@tid, @no, @adrs, @pin, @dvsn, @note, @si_cd, @gu_cd, @dn_cd, @ri_cd, @hj_cd, @pnu, @x, @y, @zone_no)";
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

                    //필지수
                    if (r["dvsn"].ToString() == "토지" && r["note"].ToString() == "미종국")
                    {
                        lotCnt++;
                    }

                    //호개수
                    if (r["dvsn"].ToString() == "집합건물" && r["note"].ToString() == "미종국")
                    {
                        hoCnt++;
                    }
                }

                if (lotCnt > 0)
                {
                    sql = "update ta_list set lot_cnt='" + lotCnt.ToString() + "' where tid='" + tid + "'";
                    db.ExeQry(sql);
                }

                if (hoCnt > 0)
                {
                    sql = "update ta_list set ho_cnt='" + hoCnt.ToString() + "' where tid='" + tid + "'";
                    db.ExeQry(sql);
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

                //매각 일정 등록
                sql = "delete from ta_hist where tid=" + tid;
                db.ExeQry(sql);

                sql = "insert into ta_hist set tid=@tid, bid_dt=@bid_dt, bid_tm=@bid_tm, sta=1110, amt=@amt";
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@bid_dt", bidDt));
                sp.Add(new MySqlParameter("@bid_tm", row["bidTm"]));
                sp.Add(new MySqlParameter("@amt", row["minbAmt"]));
                db.ExeQry(sql, sp);
                sp.Clear();
                if (row["bidTm2"].ToString() != "" && row["minbAmt2"].ToString() != "") //2회차
                {
                    sql = "insert into ta_hist set tid=@tid, bid_dt=@bid_dt, bid_tm=@bid_tm, sta=1110, amt=@amt";
                    sp.Add(new MySqlParameter("@tid", tid));
                    sp.Add(new MySqlParameter("@bid_dt", bidDt));
                    sp.Add(new MySqlParameter("@bid_tm", row["bidTm2"]));
                    sp.Add(new MySqlParameter("@amt", row["minbAmt2"]));
                    db.ExeQry(sql, sp);
                    sp.Clear();
                }
                db.Close();

                //물건 종별
                Prc_Cat(tid, dtLs, useCat, pdNote);

                //자동차,중장비,선박 현황 처리
                if (dtLs.Select("dvsn='자동차' or dvsn='건설기계,중기'").Count() > 0)
                {
                    PrcDtlSub_Car(tid, dtLs, row);
                }
                else if (dtLs.Select("dvsn='선박'").Count() > 0)
                {
                    PrcDtlSub_Ship(tid, dtLs, row);
                }
                else if (dtLs.Select("dvsn='어업권'").Count() > 0)
                {
                    PrcDtlSub_Fish(tid, dtLs, row);
                }
                else if (dtLs.Select("dvsn='광업권'").Count() > 0)
                {
                    PrcDtlSub_Mine(tid, dtLs, row);
                }
                else
                {
                    //토지 및 건물 현황은 Prc_Dtl() 에서 현황조사서->부동산 표시목록과 연동하여 취한다.
                    //제시외건물
                    db.Open();
                    foreach (DataRow r in dtE.Rows)
                    {
                        sql = "insert into ta_bldg (tid, ls_no, dvsn, sqm, state, struct) values (@tid, @ls_no, @dvsn, @sqm, @state, @struct)";
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@ls_no", r["lsNo"]));
                        sp.Add(new MySqlParameter("@dvsn", 2));
                        sp.Add(new MySqlParameter("@sqm", r["sqm"].ToString().Replace(",", string.Empty).Trim()));
                        sp.Add(new MySqlParameter("@state", r["state"].ToString().Trim()));
                        sp.Add(new MySqlParameter("@struct", r["struct"].ToString().Trim()));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }

                    //제시외-기계/기구 존재시
                    if (macExist)
                    {
                        sql = "insert into ta_bldg set tid=" + tid + ", dvsn=3, state='기계/기구'";
                        db.ExeQry(sql);
                    }
                    db.Close();
                }

                //제시외 물건 갱신을 위한 기록(물번이 분리되지 않은 경우)
                if (othFlag)
                {
                    sql = "insert ignore into db_tank.tx_oth_err set tid='" + tid + "', wdt=curdate()";
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();
                }

                //sms 발송대상 물건 저장
                if (dbMode == "Update")
                {
                    sql = "insert ignore into db_tank.tx_sms set tid='" + tid + "', state='신건', wdt=curdate(), wtm=curtime()";
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();
                }
            }

            string csNm = string.Empty, dptNm = string.Empty;

            db.Open();
            sql = "SELECT cs_nm,dpt_nm FROM ta_cd_cs C, ta_cd_dpt D where C.spt_cd=D.cs_cd and C.spt_cd='" + spt + "' and D.dpt_cd='" + dpt + "'";
            MySqlDataReader data = db.ExeRdr(sql);
            data.Read();
            if (data.HasRows)
            {
                csNm = data["cs_nm"].ToString();
                dptNm = data["dpt_nm"].ToString();
            }
            data.Close();
            db.Close();
                        
            //입찰일-법원-계별 개수
            atomLog.AddLog(string.Format(" > {0} / {1} / {2} : 신건-{3}, 전환-{4}", bidDt, csNm, dptNm, eaNewCnt, eaUdtCnt));
        }

        /// <summary>
        /// 제시외 현황 업데이트
        /// </summary>
        private void Prc_Oth()
        {
            string tid, sql, url, jiwonNm, saNo, sn1, sn2, pn, html;
            string lsNo = "", use, strt, area, etcStr;
            int rowIdx, udtCnt = 0, passCnt = 0, totCnt = 0;
            bool eqFlag = false, macExist = false;

            //제시외
            DataTable dtE = new DataTable();
            dtE.Columns.Add("lsNo");
            dtE.Columns.Add("state");
            dtE.Columns.Add("struct");
            dtE.Columns.Add("sqm");

            HAPDoc doc = new HAPDoc();
            List<MySqlParameter> sp = new List<MySqlParameter>();

            sql = "select L.tid, spt, sn1, sn2, pn, concat(spt,sn1,sn2) as spt_sn from db_main.ta_list L , db_tank.tx_oth_err R where L.tid=R.tid and proc=0 and works=0 and pre_dt < curdate() order by spt, sn1, sn2, pn";
            DataTable dt = db.ExeDt(sql);

            totCnt = dt.Rows.Count;
            atomLog.AddLog(string.Format(" > 확인 대상-{0}", totCnt));     //로그기록

            foreach (DataRow row in dt.Rows)
            {
                rowIdx = dt.Rows.IndexOf(row);
                eqFlag = false;
                macExist = false;

                dtE.Rows.Clear();

                tid = row["tid"].ToString();
                sn1 = row["sn1"].ToString();
                sn2 = row["sn2"].ToString();
                pn = (row["pn"].ToString() == "0") ? "1" : row["pn"].ToString();
                saNo = string.Format("{0}0130{1}", sn1, sn2.PadLeft(6, '0'));
                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));

                progrsView(string.Format("[제시외 갱신] {0}-{1} ({2})", sn1, sn2, row["pn"]), 3);      //진행상태

                if (rowIdx > 0)
                {
                    if (row["spt_sn"].ToString() == dt.Rows[rowIdx - 1]["spt_sn"].ToString()) eqFlag = true;
                }

                if (eqFlag == false)
                {
                    webCnt++;
                    if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                    url = "http://www.courtauction.go.kr/RetrieveRealEstDetailInqSaList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
                    html = net.GetHtml(url);
                    doc.LoadHtml(html);
                }

                //물건내역(물건번호별)
                HtmlNodeCollection ncTbl = doc.DocumentNode.SelectNodes("//table[@summary='물건내역 표']");
                if (ncTbl == null || doc.DocumentNode.InnerText.Contains("등록된 물건내역이 없습니다") || (ncTbl != null && ncTbl[0].InnerText.Contains("검색결과가 없습니다")))
                {
                    continue;
                }

                foreach (HtmlNode tbl in ncTbl)
                {
                    if (tbl.SelectSingleNode("./tr/td").InnerText.Trim() != pn) continue;
                    HtmlNodeCollection ncTr = tbl.SelectNodes("./tr");
                    foreach (HtmlNode ndTr in ncTr)
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./th|./td");
                        string colName = ncTd[0].InnerText.Trim();
                        if (ncTd[0].Name == "th" && colName.Contains("목록"))
                        {
                            lsNo = Regex.Match(colName, @"\d+").Value;
                        }
                        if (ncTd[0].Name == "th" && colName.Contains("제시외"))
                        {
                            etcStr = ncTd[1].InnerHtml.Trim().Replace("<br>", "\r\n");
                            MatchCollection mc = Regex.Matches(etcStr, etcPtrn1, rxOptM);
                            if (mc.Count > 0)
                            {
                                foreach (Match ma in mc)
                                {
                                    use = ma.Groups[1].Value.Trim();
                                    strt = ma.Groups[2].Value.Trim();
                                    area = ma.Groups[3].Value.Trim();
                                    if (use.Contains("기계기구"))
                                    {
                                        macExist = true;
                                        continue;
                                    }
                                    if (ma.Value.Contains("수목") || ma.Value.Contains("나무") || ma.Value.Contains("관정"))
                                    {
                                        area = string.Empty;
                                    }
                                    else
                                    {
                                        if (Regex.IsMatch(strt, macPtrn, rxOptM))
                                        {
                                            macExist = true;
                                            continue;
                                        }
                                    }
                                    dtE.Rows.Add(lsNo, use, strt, area);
                                }
                            }
                            else
                            {
                                mc = Regex.Matches(etcStr, etcPtrn2, rxOptM);
                                if (mc.Count > 0)
                                {
                                    foreach (Match ma in mc)
                                    {
                                        use = ma.Groups[1].Value.Trim();
                                        strt = ma.Groups[2].Value.Trim();
                                        if (use.Contains("기계기구"))
                                        {
                                            macExist = true;
                                            continue;
                                        }
                                        if (ma.Value.Contains("수목") || ma.Value.Contains("나무") || ma.Value.Contains("관정"))
                                        {
                                            area = string.Empty;
                                        }
                                        else
                                        {
                                            if (Regex.IsMatch(strt, macPtrn, rxOptM))
                                            {
                                                macExist = true;
                                                continue;
                                            }
                                        }
                                        dtE.Rows.Add(lsNo, use, strt, "");
                                    }
                                }
                            }
                        }
                    }
                }

                if (dtE.Rows.Count == 0)
                {
                    db.Open();
                    sql = "update db_tank.tx_oth_err set proc=9, udt=curdate() where tid=" + tid;
                    db.ExeQry(sql);
                    db.Close();
                    passCnt++;
                    continue;
                }

                db.Open();
                sql = "delete from ta_bldg where tid='" + tid + "' and dvsn in (2,3)";
                db.ExeQry(sql);

                foreach (DataRow r in dtE.Rows)
                {
                    sql = "insert into ta_bldg (tid, ls_no, dvsn, sqm, state, struct) values (@tid, @ls_no, @dvsn, @sqm, @state, @struct)";
                    sp.Add(new MySqlParameter("@tid", tid));
                    sp.Add(new MySqlParameter("@ls_no", r["lsNo"]));
                    sp.Add(new MySqlParameter("@dvsn", 2));
                    sp.Add(new MySqlParameter("@sqm", r["sqm"].ToString().Replace(",", string.Empty).Trim()));
                    sp.Add(new MySqlParameter("@state", r["state"].ToString().Trim()));
                    sp.Add(new MySqlParameter("@struct", r["struct"].ToString().Trim()));
                    db.ExeQry(sql, sp);
                    sp.Clear();
                }

                //제시외-기계/기구 존재시
                if (macExist)
                {
                    sql = "insert into ta_bldg set tid=" + tid + ", dvsn=3, state='기계/기구'";
                    db.ExeQry(sql);
                }

                sql = "update db_tank.tx_oth_err set proc=1, udt=curdate() where tid=" + tid;
                db.ExeQry(sql);
                db.Close();
                udtCnt++;
            }

            atomLog.AddLog(string.Format(" > 업데이트-{0}, 해당없음-{1}", udtCnt, passCnt));
        }

        /// <summary>
        /// 차량/중기/선박 목록주소 수정(단, 물건번호 분리전 물건+작업전 물건)
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        private void Prc_CarsAdrs()
        {
            string tid, sql, url, jiwonNm, saNo, sn1, sn2, pn, html;
            int totCnt, upCnt = 0;

            HAPDoc doc = new HAPDoc();
            List<MySqlParameter> sp = new List<MySqlParameter>();

            sql = "select tid, spt, sn1, sn2, pn from ta_list L where cat1=30 and sta2=1110 and works=0 and pre_dt=curdate() order by spt, sn1, sn2, pn";
            DataTable dt = db.ExeDt(sql);

            totCnt = dt.Rows.Count;
            atomLog.AddLog(string.Format(" > 확인 대상-{0}", totCnt));     //로그기록

            foreach (DataRow row in dt.Rows)
            {
                tid = row["tid"].ToString();
                sn1 = row["sn1"].ToString();
                sn2 = row["sn2"].ToString();
                pn = (row["pn"].ToString() == "0") ? "1" : row["pn"].ToString();
                saNo = string.Format("{0}0130{1}", sn1, sn2.PadLeft(6, '0'));
                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));

                progrsView(string.Format("[차량/중기/선박 목록주소 수정] {0}-{1} ({2})", sn1, sn2, row["pn"]), 3);      //진행상태

                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                url = "http://www.courtauction.go.kr/RetrieveRealEstDetailInqSaList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
                html = net.GetHtml(url);
                doc.LoadHtml(html);

                //물건내역(물건번호별)
                HtmlNodeCollection ncTbl = doc.DocumentNode.SelectNodes("//table[@summary='물건내역 표']");
                if (ncTbl == null || doc.DocumentNode.InnerText.Contains("등록된 물건내역이 없습니다") || (ncTbl != null && ncTbl[0].InnerText.Contains("검색결과가 없습니다")))
                {
                    DataTable dtC = db.ExeDt("select ls_no, adrs from ta_cars where tid=" + tid);
                    db.Open();
                    foreach (DataRow dr in dtC.Rows)
                    {
                        sql = "update ta_ls set adrs=@adrs where tid=@tid and no=@no";
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@no", dr["ls_no"]));
                        sp.Add(new MySqlParameter("@adrs", dr["adrs"]));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                    db.Close();
                    upCnt++;
                }
            }

            atomLog.AddLog(string.Format(" > 업데이트-{0}", upCnt));
        }

        /// <summary>
        /// 매각구분 코드
        /// </summary>
        /// <param name="pdNote"></param>
        /// <param name="dtLs"></param>
        /// <returns></returns>
        private string Dpsl_DvsnCd(string jiwonNm, string saNo, string pdNote, DataTable dtLs)
        {
            decimal retCd = 0;
            string url, html;
            string lsDvsn = string.Empty, dvsn = string.Empty, dtlAllStr = string.Empty, dtlEaStr = string.Empty;
            bool flagLand = false, flagBldg = false, flagMultBldg = false;
            bool flagLandShr = false, flagBldgShr = false, flagMultShr = false;

            DataTable dtR = new DataTable();    //현황조사서->부동산 표시목록
            dtR.Columns.Add("no");
            dtR.Columns.Add("adrs");
            dtR.Columns.Add("useSqm");
            dtR.Columns.Add("note");

            StringBuilder sb = new StringBuilder();

            if (dtLs.Rows.Count == 0) return "0";

            webCnt++;
            if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
            url = "http://www.courtauction.go.kr/RetrieveRealEstHjosaDispMokrok.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo;
            html = net.GetHtml(url);
            if (html.Contains("존재하지 않는 페이지입니다")) return "0";

            HAPDoc doc = new HAPDoc();
            doc.LoadHtml(html);

            HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='부동산 표시목록']/tbody/tr");
            if (ncTr == null) return "0";

            foreach (HtmlNode tr in ncTr)
            {
                HtmlNodeCollection ncTd = tr.SelectNodes("./td");
                if (ncTd.Count != 4) continue;
                dtR.Rows.Add(ncTd[0].InnerText.Trim(), ncTd[1].InnerText.Trim(), ncTd[2].InnerText.Trim(), ncTd[3].InnerText.Trim());
            }
            if (dtR.Rows.Count == 0) return "0";

            foreach (DataRow row in dtLs.Rows)
            {
                lsDvsn = row["dvsn"].ToString();
                if (lsDvsn == "토지") flagLand = true;
                if (lsDvsn == "건물") flagBldg = true;
                if (lsDvsn == "집합건물") flagMultBldg = true;

                foreach (DataRow r in dtR.Rows)
                {
                    if (row["no"].ToString() != r["no"].ToString()) continue;
                    if (flagLand && r["useSqm"].ToString().Contains("매각지분")) flagLandShr = true;
                    if (flagBldg && r["useSqm"].ToString().Contains("매각지분")) flagBldgShr = true;
                    if (flagMultBldg && r["useSqm"].ToString().Contains("매각지분")) flagMultShr = true;
                    sb.Append(r["useSqm"].ToString());
                }
            }
            dtlAllStr = sb.ToString();

            if (flagMultBldg) dvsn = "집합건물";
            else if (flagLand && flagBldg) dvsn = "토지+건물";
            else if (flagLand) dvsn = "토지";
            else if (flagBldg) dvsn = "건물";

            switch (dvsn)
            {
                case "집합건물":
                    if (pdNote == "" || Regex.IsMatch(pdNote, @"제시외[\s\w]+포함", rxOptM))
                    {
                        retCd = 10;      //토지건물 일괄매각
                    }
                    else if (pdNote.Contains("건물만") && !flagMultShr)
                    {
                        retCd = 17;      //건물만 매각
                    }
                    else if (!pdNote.Contains("건물만") && flagMultShr)
                    {
                        retCd = 15;      //토지 및 건물 지분매각
                    }
                    else if (pdNote.Contains("건물만") && flagMultShr)
                    {
                        retCd = 22;      //건물만 매각, 지분매각
                    }
                    else if (!pdNote.Contains("지분") && !dtlAllStr.Contains("지분"))
                    {
                        retCd = 10;      //토지건물 일괄매각
                    }
                    else if (pdNote.Contains("건물만"))
                    {
                        retCd = 17;      //건물만 매각 - 검토?
                    }
                    else if (!flagLand)
                    {
                        retCd = 17;      //건물만 매각 - 검토?
                    }
                    break;

                case "토지+건물":
                    if (pdNote.Contains("일괄매각") && !dtlAllStr.Contains("지분") && Regex.IsMatch(pdNote, @"제시외[\s\w\(\)\~]+포함|토지에 포함", rxOptM))
                    {
                        retCd = 10;      //토지건물 일괄매각
                    }
                    else if (pdNote.Contains("건물만"))
                    {
                        retCd = 17;      //건물만 매각
                    }
                    else if (pdNote.Contains("일괄매각") && flagLandShr && flagBldgShr)
                    {
                        retCd = 15;      //토지 및 건물 지분매각
                    }
                    else if (pdNote.Contains("일괄매각") && !flagLandShr && flagBldgShr)
                    {
                        retCd = 23;      //토지전부, 건물지분
                    }
                    else if (pdNote.Contains("일괄매각") && flagLandShr && !flagBldgShr)
                    {
                        retCd = 18;     //건물전부, 토지지분
                    }
                    else if (!pdNote.Contains("지분") && !dtlAllStr.Contains("지분"))
                    {
                        retCd = 10;      //토지건물 일괄매각
                    }
                    else if (pdNote.Contains("건물만"))
                    {
                        retCd = 17;      //건물만 매각 - 검토?
                    }
                    else if (!flagLand)
                    {
                        retCd = 17;      //건물만 매각 - 검토?
                    }
                    break;

                case "토지":
                    if (pdNote.Contains("일괄매각") && !dtlAllStr.Contains("지분") && Regex.IsMatch(pdNote, @"제시외[\s\w\(\)\~]+포함", rxOptM))
                    {
                        retCd = 10;      //토지건물 일괄매각
                    }
                    else if (!dtlAllStr.Contains("지분") && Regex.IsMatch(pdNote, @"건물[\s\w\(\)\~]+제외|제외되는 제시외[\s건물]+|평가에서 제외|매각대상이 아님", rxOptM))
                    {
                        retCd = 13;      //토지만 매각
                    }
                    else if (!pdNote.Contains("제시외"))
                    {
                        if (!flagLandShr)
                        {
                            retCd = 11;  //토지 매각
                        }
                        else
                        {
                            retCd = 12;  //토지 지분매각
                        }
                    }
                    else if (flagLandShr && Regex.IsMatch(pdNote, @"제시외[\s건물]+포함|일괄매각", rxOptM))
                    {
                        retCd = 18;     //건물전부, 토지지분
                    }
                    else if (flagLandShr && Regex.IsMatch(pdNote, @"제시외[\s건물은매각에서]+제외|매각에서[\s제외되는]+제시외[\s건물]+", rxOptM))
                    {
                        retCd = 16;     //토지만 매각, 지분매각
                    }
                    else if (!dtlAllStr.Contains("지분") && Regex.IsMatch(pdNote, @"제시외[\s\w\,]+제외|매각대상 아닌", rxOptM))
                    {
                        retCd = 14;     //토지만 매각(제시외 기타제외)
                    }
                    else if (flagLandShr && Regex.IsMatch(pdNote, @"제외", rxOptM))
                    {
                        retCd = 20;     //토지만 매각, 지분매각(건물X)
                    }
                    else if (Regex.IsMatch(pdNote, @"지상권[\s만매각]+", rxOptM) && !pdNote.Contains("법정"))
                    {
                        retCd = 25;     //지상권만 매각
                    }
                    else if (!dtlAllStr.Contains("지분") && Regex.IsMatch(pdNote, @"제시외[\s\w\,\(\)]+포함|제시외[\s\w\,\(\)]+매각[\s]*대상", rxOptM))
                    {
                        retCd = 19;     //토지 매각(제시외 기타포함)
                    }
                    else if (dtlAllStr.Contains("지분") && Regex.IsMatch(pdNote, @"제시외[\s\w\,\(\)]+포함|(수목|비닐하우스|컨테이너|관정|정자)[\s]*포함", rxOptM))
                    {
                        retCd = 21;     //토지 지분매각(제시외 기타포함)
                    }
                    break;

                case "건물":
                    if (flagBldgShr)
                    {
                        retCd = 22;     //건물만 매각, 지분매각
                    }
                    else if (pdNote.Contains("건물만"))
                    {
                        retCd = 17;      //건물만 매각 - 검토?
                    }
                    else if (!flagLand)
                    {
                        retCd = 17;      //건물만 매각 - 검토?
                    }
                    break;

                default:
                    retCd = 0;
                    break;
            }

            if (pdNote.Contains("전세권만"))
            {
                retCd = 24;             //전세권만 매각
            }

            return retCd.ToString();
        }

        /// <summary>
        /// 특수 검색조건 키워드
        /// </summary>
        /// <param name="pdNote"></param>
        /// <returns></returns>
        private string Spl_Keyword(string pdNote)
        {
            string rslt = "", ptrn = "", cd = "";

            List<string> lsRslt = new List<string>();

            pdNote = Regex.Replace(pdNote, @"[\s\t]*", string.Empty);
            foreach (DataRow row in dtSpcCd.Rows)
            {
                cd = row["cd"].ToString();
                ptrn = row["rx"].ToString().Trim();
                if (ptrn == string.Empty) continue;
                Match match = Regex.Match(pdNote, ptrn, rxOptM);
                if (match.Success)
                {
                    if (lsRslt.Contains(cd)) continue;
                    lsRslt.Add(cd);
                }
            }

            if (lsRslt.Count > 0)
            {
                rslt = string.Join(",", lsRslt);
            }

            return rslt;
        }

        /// <summary>
        /// 물건종별 판단(API 연동)
        /// - 토지 -> 국토교통부_토지융합정보
        /// - 건물 -> 국토교통부_건축물대장 표제부 조회
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="dtLs"></param>
        private void Prc_Cat(string tid, DataTable dtLs, string useCat, string pdNote)
        {
            int rowCnt, totalCnt;
            string dvsn, pnu, adrs, adrsBldNm = "", adrsDongNm = "", url, urlLand, urlBldg, xml, sql, cat1 = "", cat2 = "", cat3 = "", bldgNm = "", spRgst = "";
            string lndcgrCodeNm = "", mainPurpsCd = "", bldNm = "", dongNm = "";
            string aptPtrn = "아파트|아이파크|푸르지오|롯데캐슬|[이e-]+편한세상|두산위브|휴먼시아|우방유쉘|에스케이뷰|센트레빌|유앤아이|엘에이치|아크로리버파크|해링턴|스카이뷰|맨션|데시앙|힐스테이트|래미안|호반베르디움|선수촌|메트로|타워팰리스|꿈에그린|자이|렉스힐|금호타운|" +
                "우방타운|우미린|해피트리|월드메르디앙|예다음|쌍용예가|화성파크드림|부영|보성|동일하이빌|이다음|스위첸|센트럴하이츠|리버[ ]*뷰|캐스빌|현대[홈]*타운|롯데인벤스|우방아이유쉘|메르빌|리버팰리스|골드캐슬|센트럴타운|에스클래스|풍림|청구하이츠|청구타운|뉴타운|풍경채|포레스트|" +
                "센트럴파크|유보라|코아루|휴포레|서희스타힐스|강변타운|무지개타운|삼도뷰엔빌|삼성쉐르빌|성원상떼빌|뜨란채|하늘채|화성타운|숲속마을|태왕하이츠|호반리젠시|삼성명가|현진에버빌|쌍용스윗|노르웨이숲|블루밍|휴플러스|진아리채|코아루|백년가약|수자인|베르디움|" +
                @"더[샵샾]|\d+단지";
            string vilPtrn = "빌라|빌리지";
            string ofiPtrn = "오피스텔";
            string twhPtrn = "연립|주택";
            bool aptPtrnAply = false;

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
                            aptPtrnAply = true;
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
                            aptPtrnAply = true;
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
                            aptPtrnAply = true;
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
                //cat3 = "401110";
                cat3 = "201132";    //건물-상업용 및 업무용-기타 ?
            }

            if (cat3 != "201013" && useCat.Contains("아파트"))
            {
                cat3 = "201013";
                aptPtrnAply = true;
            }

            //아파트인지 재판별
            if (cat3 == "201013" && aptPtrnAply == false && useCat.Contains("아파트") == false)
            {
                if (useCat.Contains("다세대") || useCat.Contains("빌라")) cat3 = "201015";   //다세대주택
                else if (useCat.Contains("연립주택")) cat3 = "201014";  //연립주택
                else if (useCat.Contains("오피스텔")) cat3 = "201020";  //오피스텔(주거)
            }

            spRgst = "0";
            if (pdNote != "" && multiBldgArr.Contains(Convert.ToDecimal(cat3)))
            {
                spRgst = Sp_RgstCd(pdNote);
            }

            cat1 = cat3.Substring(0, 2);
            cat2 = cat3.Substring(0, 4);
            db.Open();
            sql = "update ta_list set cat1=" + cat1 + ", cat2=" + cat2 + ", cat3=" + cat3 + ", sp_rgst=" + spRgst + " where tid=" + tid;
            db.ExeQry(sql);
            db.Close();
        }

        /// <summary>
        /// 토지별도등기 코드(집합건물)
        /// </summary>
        /// <param name="pdNote"></param>
        /// <returns></returns>
        private string Sp_RgstCd(string pdNote)
        {
            decimal retCd = 0;

            if (Regex.IsMatch(pdNote, @"대지권[\s의]목적.*별도[\s]*등기", rxOptS))
            {
                retCd = 5;  //토지별도등기인수조건
            }
            else if (Regex.IsMatch(pdNote, @"대지권[\s]*미등기.*가격[\s이에는]*포함", rxOptS) && Regex.IsMatch(pdNote, @"별도등기", rxOptS))
            {
                retCd = 6;  //미등기가격포함+토지별도등기
            }
            else if (Regex.IsMatch(pdNote, @"대지권[\s]*미등기.*가격[\s이에는]*포함", rxOptS))
            {
                retCd = 3;  //미등기감정가격포함
            }
            else if (Regex.IsMatch(pdNote, @"대지권[\s]*미등기", rxOptS))
            {
                retCd = 4;  //대지권미등기
            }
            else if (Regex.IsMatch(pdNote, @"대지권[\s이]*없는", rxOptS))
            {
                retCd = 2;  //대지권없음
            }
            else if (Regex.IsMatch(pdNote, @"별도등기", rxOptS))
            {
                retCd = 1;  //토지별도등기있음
            }

            return retCd.ToString();
        }

        /// <summary>
        /// 토지/건물 현황
        /// </summary>
        private void Prc_Dtl()
        {
            string sql, tid, jiwonNm, saNo, cat;

            sql = "select tid, crt, spt, sn1, sn2, pn, cat3, bid_dt, minb_amt, frml_type from ta_list where pre_dt=curdate() and auto_prc=1 order by tid";
            DataTable dt = db.ExeDt(sql);
            foreach (DataRow row in dt.Rows)
            {
                progrsView(string.Format("[상세정보] {0}-{1} ({2})", row["sn1"], row["sn2"], row["pn"]), 3);      //진행상태

                tid = row["tid"].ToString();
                cat = row["cat3"].ToString();
                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));

                sql = "select * from ta_ls where tid=" + tid + " order by no";
                DataTable dtLs = db.ExeDt(sql);
                if (dtLs.Rows.Count > 0)
                {
                    if (dtLs.Select("dvsn='토지' or dvsn='건물' or dvsn='집합건물' or dvsn='기타'").Count() > 0)
                    {
                        PrcDtlSub_LandBldg(tid, jiwonNm, saNo, cat, dtLs);
                    }
                }

                db.Open();
                sql = "update ta_list set auto_prc=2, pre_prc=2 where tid=" + tid;
                db.ExeQry(sql);
                db.Close();
            }
        }

        private void PrcDtlSub_LandBldg(string tid, string jiwonNm, string saNo, string cat, DataTable dtLs)
        {
            string url = "", html = "", sql = "", lsNo = "", lsType = "", lsAdrs = "", catNm = "", catCd = "", floor = "", flrCd = "", flrNm = "", shrStr0 = "", shrStr = "", frtn = "", dtlStr = "", etcStr = "", bldgStruct = "";
            string landSection = "", bldgSection = "";
            double sqm = 0, totSqm = 0, bldgSqm = 0, totShrSqm = 0, dt = 0, nt = 0;
            double sumLandSqm = 0, sumLandTotSqm = 0, sumRtSqm = 0, rtTotSqm = 0, sumBldgSqm = 0, sumBldgTotSqm = 0;            
            int i = 0;

            //string bldgPtrn = @"([지하옥탑상일이삼사오육칠팔구십단\d]+[층실])[ ]*(.*?[소실조택고장당원설점\)])*[ ]*(\d[\d\.\,]*)[ ]*㎡";
            //string bldgPtrn = @"([지하옥탑상일이삼사오육칠팔구십단\d]+[층실])[ ]*(.*?)(\d[\d\.\,]*)[ ]*㎡";
            string bldgPtrn = @"([지하옥탑상일이삼사오육칠팔구십단제\d]+[층실])[ ]*(.*?)(\d[\d\.\,]*)[ ]*㎡";    //2021-11-25 패턴 변경
            string frtnPtrn1 = @"(\d+[\.]*[\d]*)[ ]*분의[ ]*(\d+[\.]*[\d]*)";   //분수 패턴-1
            string frtnPtrn2 = @"(\d+[\.]*[\d]*)/(\d+[\.]*[\d]*)";              //분수 패턴-2
            string structPtrn = @"^\s+(철[근골]|[일반경량]+철골|[적흙변색]*벽돌|[시세]멘|조적조|목조|[브보블][록럭][크]*|연와[조]*|콘크리트|일반목구조|[철강]*파이프|조립식|조적|라멘조|알[.]*씨조|샌드위치|슬래브).*";  //건물구조 패턴

            //토지용
            DataTable dtL = new DataTable();
            dtL.Columns.Add("lsNo");
            dtL.Columns.Add("multi");
            dtL.Columns.Add("catNm");
            dtL.Columns.Add("catCd");
            dtL.Columns.Add("sqm");
            dtL.Columns.Add("rtSqm");
            dtL.Columns.Add("totShrSqm");
            dtL.Columns.Add("totRtSqm");
            dtL.Columns.Add("frtn");
            dtL.Columns.Add("shrStr");

            //건물용
            DataTable dtB = new DataTable();
            dtB.Columns.Add("lsNo");
            dtB.Columns.Add("multi");
            dtB.Columns.Add("floor");
            dtB.Columns.Add("sqm");
            dtB.Columns.Add("totShrSqm");
            dtB.Columns.Add("shrStr");
            dtB.Columns.Add("tmpStr");
            dtB.Columns.Add("totFlr");
            dtB.Columns.Add("struct");

            webCnt++;
            if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

            url = "http://www.courtauction.go.kr/RetrieveRealEstHjosaDispMokrok.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo;
            html = net.GetHtml(url);
            if (html.Contains("존재하지 않는 페이지입니다")) return;

            Dictionary<string, string> dicLs = new Dictionary<string, string>();
            Dictionary<string, string> dicAdrs = new Dictionary<string, string>();

            bool structFlag = (dtCatCd.Select("cat3_cd='" + cat + "' and bldg_type=2").Count() > 0) ? true : false;

            sql = "select * from ta_ls where tid=" + tid;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                dicLs.Add(dr["no"].ToString(), dr["dvsn"].ToString().Trim());
                dicAdrs.Add(dr["no"].ToString(), dr["adrs"].ToString().Trim());
            }
            db.Close();

            HAPDoc doc = new HAPDoc();
            doc.LoadHtml(html);
            
            HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='부동산 표시목록']/tbody/tr");
            if (ncTr == null) return;

            List<MySqlParameter> sp = new List<MySqlParameter>();

            foreach (HtmlNode tr in ncTr)
            {
                sqm = 0; totSqm = 0; bldgSqm = 0; totShrSqm = 0; dt = 0; nt = 0;
                floor = ""; shrStr0 = ""; shrStr = ""; etcStr = "";

                HtmlNodeCollection ncTd = tr.SelectNodes("./td");
                if (ncTd.Count != 4) continue;
                lsNo = ncTd[0].InnerText.Trim();
                lsAdrs = ncTd[1].InnerText.Trim();
                if (dicLs.ContainsKey(lsNo) == false) continue;     //ta_ls 기록 할 수 없음.
                if (dicAdrs[lsNo].Replace(" ", string.Empty) != lsAdrs.Replace(" ", string.Empty))
                {
                    //ta_ls 기록 해서 관리자가 인지 할 수 있도록 한다.
                    db.Open();
                    sql = "update ta_ls set pre_err=1 where tid=" + tid + " and no=" + lsNo;
                    db.ExeQry(sql);
                    db.Close();
                    continue;
                }

                lsType = dicLs[lsNo];
                dtlStr = ncTd[2].InnerText.Replace("&nbsp;", string.Empty).Trim();
                dtlStr = Regex.Replace(dtlStr, @"\(현황\:\w+\)", string.Empty);       //2022-04-13 추가
                dtlStr = Regex.Replace(dtlStr, @"[ ]*평방[ ]*미터|[ ]*제곱[ ]*미터", "㎡");
                //dtlStr = Regex.Replace(ncTd[2].InnerHtml, @"<[ㄱ-힣]+", string.Empty);  //처리불가 - 매각지분 : <경매할지분 공유자지분중 724분의215(갑구4) 엘티산업㈜ 소유지분>

                if (lsType == "토지")
                {
                    int eIndex = dtlStr.IndexOf("제시외");
                    if (eIndex > -1)
                    {
                        etcStr = dtlStr.Substring(eIndex);
                        dtlStr = dtlStr.Remove(eIndex);
                    }
                    dtlStr = landUnitConvert("토지", dtlStr);
                    Match match = Regex.Match(dtlStr, @"(" + landPtrn + "|null" + @")[ ]*(\d[\d\.\,]*)[ ]*㎡", rxOptM);
                    if (match.Success == false) continue;
                    catNm = match.Groups[1].Value.Trim();
                    if (catNm == "대") catNm = "대지";
                    var x = from DataRow r in dtCatCd.Rows
                            where r["cat2_cd"].ToString() == "1010" && r["cat3_nm"].ToString() == catNm
                            select r;
                    if (x.Count() > 0) catCd = x.CopyToDataTable().Rows[0]["cat3_cd"].ToString();
                    else catCd = "0";
                    //totSqm = Convert.ToDouble(match.Groups[2].Value.Replace(",", string.Empty));
                    totSqm = double.TryParse(match.Groups[2].Value.Replace(",", string.Empty), out double val) ? val : 0;
                    //match = Regex.Match(dtlStr, @"매각지분[ ]*:[ ]*(.*)", rxOptS);
                    match = Regex.Match(dtlStr, @"매각지분[ ]*(.*)", rxOptS);

                    if (match.Success)
                    {
                        shrStr = match.Groups[1].Value;
                        shrStr = Regex.Replace(shrStr, @"제시외.*", string.Empty, rxOptS).Trim();
                        MatchCollection mc1 = Regex.Matches(shrStr, frtnPtrn1, rxOptS);
                        MatchCollection mc2 = Regex.Matches(shrStr, frtnPtrn2, rxOptS);
                        totShrSqm = totSqm;
                        sqm = 0;
                        if (mc1 != null)
                        {
                            foreach (Match m in mc1)
                            {
                                dt = Convert.ToDouble(m.Groups[1].Value);
                                nt = Convert.ToDouble(m.Groups[2].Value);
                                sqm += totShrSqm * nt / dt;
                            }
                        }
                        if (mc2 != null)
                        {
                            foreach (Match m in mc2)
                            {
                                dt = Convert.ToDouble(m.Groups[2].Value);
                                nt = Convert.ToDouble(m.Groups[1].Value);
                                sqm += totShrSqm * nt / dt;
                            }
                        }
                        if (mc1.Count == 0 && mc2.Count == 0)
                        {
                            totShrSqm = 0;
                            sqm = totSqm;
                        }
                        else
                        {
                            if (totShrSqm == sqm)
                            {
                                totShrSqm = 0;
                                shrStr = string.Empty;
                            }
                            else
                            {
                                shrStr = string.Format("전체면적 {0}㎡중 {1}", totShrSqm, shrStr);
                            }
                        }
                    }
                    else
                    {
                        sqm = totSqm;
                    }
                    dtL.Rows.Add(lsNo, 0, catNm, catCd, sqm, "", totShrSqm, 0, frtn, shrStr);
                }
                else if (lsType == "건물")
                {
                    int eIndex = dtlStr.IndexOf("제시외");
                    if (eIndex > -1)
                    {
                        etcStr = dtlStr.Substring(eIndex);
                        dtlStr = dtlStr.Remove(eIndex);
                    }

                    //건물구조
                    Match matchStruct = Regex.Match(dtlStr, structPtrn, rxOptM);
                    bldgStruct = matchStruct.Value.Trim();
                    if (bldgStruct != string.Empty)
                    {
                        bldgStruct = Regex.Replace(bldgStruct, @"([단\d]+층|평가건[주택점포]+|위험물저장|제\d종|[\w\.]+?시설|동물).*", string.Empty).Trim();
                    }

                    dtlStr = landUnitConvert("건물", dtlStr);
                    dtlStr = Regex.Replace(dtlStr, @"\(.*?\)", string.Empty, rxOptS);   //하위 세부내역 면적 제외
                    
                    string[] strArr = dtlStr.Split('\n');
                    foreach (string s in strArr)
                    {
                        floor = ""; sqm = 0; totShrSqm = 0; totSqm = 0;
                        string str = s.Replace("&nbsp;", string.Empty).Trim();
                        if (str == string.Empty) continue;

                        Match match = Regex.Match(str, bldgPtrn, RegexOptions.Multiline);
                        if (match.Success)
                        {
                            floor = match.Groups[1].Value;
                            //totSqm = Convert.ToDouble(match.Groups[3].Value.Replace(",", string.Empty));
                            totSqm = double.TryParse(match.Groups[3].Value.Replace(",", string.Empty), out double val) ? val : 0;
                            sqm = totSqm;
                            dtB.Rows.Add(lsNo, 0, floor, sqm, totShrSqm, "", match.Value, "", bldgStruct);
                        }
                        else
                        {
                            match = Regex.Match(str, @"(\d[\d\.\,]+)[\s]*㎡", RegexOptions.Multiline);
                            if (match.Success)
                            {
                                str = match.Groups[1].Value.Replace(",", string.Empty).Trim();
                                str = Regex.Replace(str, @"\.$", string.Empty);     //0.53.㎡
                                //totSqm = Convert.ToDouble(str);
                                totSqm = double.TryParse(str, out double val) ? val : 0;
                                sqm = totSqm;
                                dtB.Rows.Add(lsNo, 0, floor, sqm, totShrSqm, "", match.Value, "", bldgStruct);
                            }
                        }
                    }
                    //Match matchShr = Regex.Match(dtlStr, @"매각지분[ ]*:[ ]*(.*)", rxOptS);
                    Match matchShr = Regex.Match(dtlStr, @"매각지분[ ]*(.*)", rxOptS);
                    if (matchShr.Success)
                    {
                        shrStr0 = matchShr.Groups[1].Value;
                        shrStr0 = Regex.Replace(shrStr0, @"제시외.*", string.Empty, rxOptS).Trim();
                        MatchCollection mc1 = Regex.Matches(shrStr0, frtnPtrn1, rxOptS);
                        MatchCollection mc2 = Regex.Matches(shrStr0, frtnPtrn2, rxOptS);
                        foreach (DataRow row in dtB.Rows)
                        {
                            if (row["lsNo"].ToString() == lsNo)
                            {
                                if (mc1 == null && mc2 == null) continue;
                                totShrSqm = Convert.ToDouble(row["sqm"]);
                                sqm = 0;
                                if (mc1 != null)
                                {
                                    foreach (Match m in mc1)
                                    {
                                        dt = Convert.ToDouble(m.Groups[1].Value);
                                        nt = Convert.ToDouble(m.Groups[2].Value);
                                        sqm += totShrSqm * nt / dt;
                                    }
                                }
                                if (mc2 != null)
                                {
                                    foreach (Match m in mc2)
                                    {
                                        dt = Convert.ToDouble(m.Groups[2].Value);
                                        nt = Convert.ToDouble(m.Groups[1].Value);
                                        sqm += totShrSqm * nt / dt;
                                    }
                                }
                                if (mc1.Count == 0 && mc2.Count == 0)
                                {
                                    sqm = totShrSqm;
                                    totShrSqm = 0;
                                    shrStr = string.Empty;
                                }
                                else
                                {
                                    if (totShrSqm == sqm)
                                    {
                                        totShrSqm = 0;
                                        shrStr = string.Empty;
                                    }
                                    else
                                    {
                                        shrStr = string.Format("전체면적 {0}㎡중 {1}", totShrSqm, shrStr0);
                                    }
                                }
                                row["sqm"] = sqm;
                                row["totShrSqm"] = totShrSqm;
                                row["shrStr"] = shrStr;
                            }
                        }
                    }
                }
                else if (lsType == "집합건물")
                {
                    bldgSection = string.Empty; landSection = string.Empty;
                    catNm = ""; catCd = ""; frtn = "";

                    int eIndex = dtlStr.IndexOf("제시외");
                    if (eIndex > -1)
                    {
                        etcStr = dtlStr.Substring(eIndex);
                    }

                    Match match = Regex.Match(dtlStr, @"전유부분의 건물의 표시(.*)대지권의 목적인 토지의 표시(.*)", rxOptS);
                    if (match.Success)
                    {
                        bldgSection = match.Groups[1].Value.Trim();
                        landSection = match.Groups[2].Value.Trim();
                    }
                    else
                    {
                        match = Regex.Match(dtlStr, @"전유부분의 건물의 표시(.*)", rxOptS);
                        if (match.Success)
                        {
                            bldgSection = match.Groups[1].Value.Trim();
                        }
                    }

                    if (bldgSection == string.Empty && landSection == string.Empty) continue;

                    if (bldgSection != string.Empty && landSection != string.Empty)
                    {
                        Match match3 = Regex.Match(dtlStr, @"건물의[ ]*번호[ ]*:[ ]*(.*?)구조[ ]*:[ ]*(.*?)면적[ ]*:[ ]*(.*?)대지권의 목적인 토지의 표시", rxOptS);
                        Match match4 = Regex.Match(dtlStr, @"건물의[ ]*번호[ ]*:[ ]*(.*?)구조[ ]*:[ ]*(.*?)대지권의 목적인 토지의 표시", rxOptS);
                        if (match3.Success)
                        {
                            MatchCollection mc = Regex.Matches(match3.Groups[2].Value + match3.Groups[3].Value, @"(\d[\d\.\,]*)[\s]*㎡", rxOptS);
                            if (mc != null)
                            {
                                foreach (Match m in mc)
                                {
                                    //bldgSqm += Convert.ToDouble(m.Groups[1].Value);
                                    bldgSqm += double.TryParse(m.Groups[1].Value, out double val) ? val : 0;
                                }
                                dtB.Rows.Add(lsNo, 1, floor, bldgSqm, "", "", "", "", "");

                            }
                        }
                        else if (match4.Success)
                        {
                            MatchCollection mc = Regex.Matches(match4.Groups[2].Value, @"(\d[\d\.\,]*)[\s]*㎡", rxOptS);
                            if (mc != null)
                            {
                                foreach (Match m in mc)
                                {
                                    //bldgSqm += Convert.ToDouble(m.Groups[1].Value);
                                    bldgSqm += double.TryParse(m.Groups[1].Value, out double val) ? val : 0;
                                }
                                dtB.Rows.Add(lsNo, 1, floor, bldgSqm, "", "", "", "", "");
                            }
                        }

                        Match match5 = Regex.Match(dtlStr, @"토[ ]*지[ ]*의[ ]*표시[ ]*:[ ]*(.*?)대지권의 종류[ ]*:[ ]*(.*?)대지권의 비율[ ]*:[ ]*(.*)", rxOptS);
                        Match match6 = Regex.Match(dtlStr, @"토[ ]*지[ ]*의[ ]*표시[ ]*:[ ]*(.*?)매각지분", rxOptS);
                        if (match5.Success)
                        {
                            Dictionary<string, string> dict = LandShrAreaCal(match5.Groups[1].Value, match5.Groups[3].Value);
                            totSqm = Convert.ToDouble(dict["rtTotSqm"]);
                            sqm = Convert.ToDouble(dict["rtSqm"]);
                            frtn = dict["frtn"];
                            catCd = dict["catCd"];
                            dtL.Rows.Add(lsNo, 1, catNm, catCd, "", sqm, "", totSqm, frtn, "");
                        }
                        else if (match6.Success)
                        {
                            Dictionary<string, string> dict = LandShrAreaCal(match6.Groups[1].Value, string.Empty);
                            totSqm = Convert.ToDouble(dict["rtTotSqm"]);
                            sqm = Convert.ToDouble(dict["rtSqm"]);
                            frtn = dict["frtn"];
                            catCd = dict["catCd"];
                            dtL.Rows.Add(lsNo, 1, catNm, catCd, "", sqm, "", totSqm, frtn, "");
                        }
                    }
                    else if (bldgSection != string.Empty)
                    {
                        Match match1 = Regex.Match(dtlStr, @"전유부분의[ ]*건물의[ ]*표시.*건물의[ ]*번호[ ]*:[ ]*(.*?)구조[ ]*:[ ]*(.*?)면적[ ]*:[ ]*(.*?)대지권의 종류[ ]*:[ ]*(.*?)대지권의 비율[ ]*:[ ]*(.*)", rxOptS);
                        Match match2 = Regex.Match(dtlStr, @"전유부분의[ ]*건물의[ ]*표시.*건물의[ ]*번호[ ]*:[ ]*(.*?)구조[ ]*:[ ]*(.*?)대지권의 종류[ ]*:[ ]*(.*?)대지권의 비율[ ]*:[ ]*(.*)", rxOptS);
                        Match match7 = Regex.Match(dtlStr, @"전유부분의[ ]*건물의[ ]*표시.*건물의[ ]*번호[ ]*:[ ]*(.*?)구조[ ]*:[ ]*(.*)", rxOptS);
                        if (match1.Success)
                        {
                            MatchCollection mc = Regex.Matches(match1.Groups[3].Value, @"(\d[\d\.\,]*)[\s]*㎡", rxOptS);
                            if (mc != null)
                            {
                                foreach (Match m in mc)
                                {
                                    //bldgSqm += Convert.ToDouble(m.Groups[1].Value);
                                    bldgSqm += double.TryParse(m.Groups[1].Value, out double val) ? val : 0;
                                }
                                dtB.Rows.Add(lsNo, 1, floor, bldgSqm, "", "", "", "", "");
                            }
                            Dictionary<string, string> dict = LandShrAreaCal(string.Empty, match1.Groups[5].Value);
                            totSqm = Convert.ToDouble(dict["rtTotSqm"]);
                            sqm = Convert.ToDouble(dict["rtSqm"]);
                            frtn = dict["frtn"];
                            catCd = dict["catCd"];
                            dtL.Rows.Add(lsNo, 1, catNm, catCd, "", sqm, "", totSqm, frtn, "");
                        }
                        else if (match2.Success)
                        {
                            MatchCollection mc = Regex.Matches(match2.Groups[2].Value, @"(\d[\d\.\,]*)[\s]*㎡", rxOptS);
                            if (mc != null)
                            {
                                foreach (Match m in mc)
                                {
                                    //bldgSqm += Convert.ToDouble(m.Groups[1].Value);
                                    bldgSqm += double.TryParse(m.Groups[1].Value, out double val) ? val : 0;
                                }
                                dtB.Rows.Add(lsNo, 1, floor, bldgSqm, "", "", "", "", "");
                            }
                            Dictionary<string, string> dict = LandShrAreaCal(string.Empty, match2.Groups[4].Value);
                            totSqm = Convert.ToDouble(dict["rtTotSqm"]);
                            sqm = Convert.ToDouble(dict["rtSqm"]);
                            frtn = dict["frtn"];
                            catCd = dict["catCd"];
                            dtL.Rows.Add(lsNo, 1, catNm, catCd, "", sqm, "", totSqm, frtn, "");
                        }
                        else if (match7.Success)
                        {
                            MatchCollection mc = Regex.Matches(match7.Groups[2].Value, @"(\d[\d\.\,]*)[\s]*㎡", rxOptS);
                            if (mc != null)
                            {
                                foreach (Match m in mc)
                                {
                                    //bldgSqm += Convert.ToDouble(m.Groups[1].Value);
                                    bldgSqm += double.TryParse(m.Groups[1].Value, out double val) ? val : 0;
                                }
                                dtB.Rows.Add(lsNo, 1, floor, bldgSqm, "", "", "", "", "");
                            }
                        }
                    }
                    else
                    {
                        //
                    }

                    if (dtlStr.Contains("매각지분"))
                    {
                        //Match match1 = Regex.Match(dtlStr, @"매각지분[ ]*:[ ]*(.*)", rxOptS);
                        Match match1 = Regex.Match(dtlStr, @"매각지분[ ]*(.*)", rxOptS);
                        shrStr = match1.Groups[1].Value;
                        MatchCollection mc1 = Regex.Matches(shrStr, frtnPtrn1, rxOptS);
                        MatchCollection mc2 = Regex.Matches(shrStr, frtnPtrn2, rxOptS);
                        if (mc1 != null || mc2 != null)
                        {
                            if (dtL.Rows.Count > 0)
                            {
                                totShrSqm = sqm;
                                sqm = 0;
                                if (mc1 != null)
                                {
                                    foreach (Match m in mc1)
                                    {
                                        dt = Convert.ToDouble(m.Groups[1].Value);
                                        nt = Convert.ToDouble(m.Groups[2].Value);
                                        sqm += totShrSqm * nt / dt;
                                    }
                                }
                                if (mc2 != null)
                                {
                                    foreach (Match m in mc2)
                                    {
                                        dt = Convert.ToDouble(m.Groups[2].Value);
                                        nt = Convert.ToDouble(m.Groups[1].Value);
                                        sqm += totShrSqm * nt / dt;
                                    }
                                }
                                if (mc1.Count > 0 || mc2.Count > 0)
                                {
                                    if (totShrSqm != sqm)
                                    {
                                        dtL.Rows[dtL.Rows.Count - 1]["totShrSqm"] = totShrSqm;
                                        dtL.Rows[dtL.Rows.Count - 1]["rtSqm"] = sqm;
                                        dtL.Rows[dtL.Rows.Count - 1]["shrStr"] = string.Format("전체면적 {0}㎡중 {1}", totShrSqm, match1.Groups[1].Value.Trim());
                                    }
                                }
                            }

                            if (dtB.Rows.Count > 0)
                            {
                                totShrSqm = bldgSqm;
                                bldgSqm = 0;
                                if (mc1 != null)
                                {
                                    foreach (Match m in mc1)
                                    {
                                        dt = Convert.ToDouble(m.Groups[1].Value);
                                        nt = Convert.ToDouble(m.Groups[2].Value);
                                        bldgSqm += totShrSqm * nt / dt;
                                    }
                                }
                                if (mc2 != null)
                                {
                                    foreach (Match m in mc2)
                                    {
                                        dt = Convert.ToDouble(m.Groups[2].Value);
                                        nt = Convert.ToDouble(m.Groups[1].Value);
                                        bldgSqm += totShrSqm * nt / dt;
                                    }
                                }
                                if (mc1.Count > 0 || mc2.Count > 0)
                                {
                                    if (totShrSqm == bldgSqm)
                                    {
                                        dtB.Rows[dtB.Rows.Count - 1]["totShrSqm"] = 0;
                                        dtB.Rows[dtB.Rows.Count - 1]["sqm"] = bldgSqm.ToString();
                                        dtB.Rows[dtB.Rows.Count - 1]["shrStr"] = string.Empty;
                                    }
                                    else
                                    {
                                        dtB.Rows[dtB.Rows.Count - 1]["totShrSqm"] = totShrSqm;
                                        dtB.Rows[dtB.Rows.Count - 1]["sqm"] = bldgSqm.ToString();
                                        dtB.Rows[dtB.Rows.Count - 1]["shrStr"] = string.Format("전체면적 {0}㎡중 {1}", totShrSqm, match1.Groups[1].Value.Trim());
                                    }
                                }
                            }
                        }
                    }

                    //총층수
                    eIndex = dtlStr.IndexOf("전유부분의 건물의 표시");
                    if (eIndex > -1)
                    {
                        MatchCollection mc = Regex.Matches(dtlStr.Remove(eIndex), @"(\d+)층", rxOptM);
                        if (mc != null)
                        {
                            Dictionary<string, int> dict = new Dictionary<string, int>();
                            foreach (Match m in mc)
                            {
                                if (!dict.ContainsKey(m.Value)) dict.Add(m.Value, Convert.ToInt32(m.Groups[1].Value));
                            }
                            if (dict.Count > 0 && dtB.Rows.Count > 0)
                            {
                                dtB.Rows[dtB.Rows.Count - 1]["totFlr"] = dict.Values.Max();
                            }
                        }
                    }
                }
                else
                {
                    continue;
                }
            }

            db.Open();
            sql = "delete from ta_land where tid=" + tid;
            db.ExeQry(sql);

            sql = "delete from ta_bldg where tid=" + tid + " and dvsn=1";
            db.ExeQry(sql);

            //토지현황            
            foreach (DataRow r in dtL.Rows)
            {
                i++;
                //if (r["multi"].ToString() == "1") continue;
                sql = "insert into ta_land (tid, ls_no, cat_cd, sqm, tot_shr_sqm, rt_sqm, tot_rt_sqm, shr_str) values (@tid, @ls_no, @cat_cd, @sqm, @tot_shr_sqm, @rt_sqm, @tot_rt_sqm, @shr_str)";
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@ls_no", r["lsNo"]));
                sp.Add(new MySqlParameter("@cat_cd", r["catCd"]));
                sp.Add(new MySqlParameter("@sqm", r["sqm"]));
                sp.Add(new MySqlParameter("@tot_shr_sqm", r["totShrSqm"]));
                sp.Add(new MySqlParameter("@rt_sqm", r["rtSqm"]));
                sp.Add(new MySqlParameter("@tot_rt_sqm", r["totRtSqm"]));
                sp.Add(new MySqlParameter("@shr_str", r["shrStr"]));
                db.ExeQry(sql, sp);
                sp.Clear();

                if (string.IsNullOrEmpty(r["sqm"].ToString()) == false) sumLandSqm += Convert.ToDouble(r["sqm"]);                    //총합-토지
                if (string.IsNullOrEmpty(r["totShrSqm"].ToString()) == false) sumLandTotSqm += Convert.ToDouble(r["totShrSqm"]);     //총합-토지지분/대지권지분
                if (string.IsNullOrEmpty(r["rtSqm"].ToString()) == false) sumRtSqm += Convert.ToDouble(r["rtSqm"]);                  //총합-대지권
                if (string.IsNullOrEmpty(r["rtSqm"].ToString()) == false && i == 1) rtTotSqm = Convert.ToDouble(r["totRtSqm"]);      //대지권전체
            }

            //건물현황
            foreach (DataRow r in dtB.Rows)
            {
                //if (r["multi"].ToString() == "1") continue;
                flrCd = "0";
                if (r["floor"]?.ToString() != "")
                {
                    flrNm = r["floor"].ToString().Trim();
                    if (flrNm == "지1층") flrNm = "지하1층";
                    else if (flrNm == "지2층") flrNm = "지하2층";
                    var xFlr = dtFlrCd.Rows.Cast<DataRow>().Where(t => t["flr_nm"].ToString() == flrNm).SingleOrDefault();
                    flrCd = (xFlr == null) ? "0" : xFlr.Field<UInt16>("flr_cd").ToString();
                }
                if (flrCd == "0")
                {
                    var xRow = dtLs.Rows.Cast<DataRow>().Where(t => t["no"].ToString() == r["lsNo"].ToString()).FirstOrDefault();
                    if (xRow != null)
                    {
                        string adrs = xRow["adrs"].ToString();
                        Match match = Regex.Match(adrs, @"\w+층", rxOptM);
                        var xFlr = dtFlrCd.Rows.Cast<DataRow>().Where(t => t["flr_nm"].ToString() == match.Value).SingleOrDefault();
                        flrCd = (xFlr == null) ? "0" : xFlr.Field<UInt16>("flr_cd").ToString();
                    }
                }

                sql = "insert into ta_bldg (tid, ls_no, dvsn, flr, tot_flr, sqm, tot_shr_sqm, shr_str, struct) values (@tid, @ls_no, @dvsn, @flr, @tot_flr, @sqm, @tot_shr_sqm, @shr_str, @struct)";
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@ls_no", r["lsNo"]));
                sp.Add(new MySqlParameter("@dvsn", 1));
                sp.Add(new MySqlParameter("@flr", flrCd));
                sp.Add(new MySqlParameter("@tot_flr", r["totFlr"]));
                sp.Add(new MySqlParameter("@sqm", r["sqm"]));
                sp.Add(new MySqlParameter("@tot_shr_sqm", r["totShrSqm"]));
                sp.Add(new MySqlParameter("@shr_str", r["shrStr"]));
                sp.Add(new MySqlParameter("@struct", r["struct"]));
                db.ExeQry(sql, sp);
                sp.Clear();

                if (string.IsNullOrEmpty(r["sqm"].ToString()) == false) sumBldgSqm += Convert.ToDouble(r["sqm"]);                //총합-건물
                if (string.IsNullOrEmpty(r["totShrSqm"].ToString()) == false) sumBldgTotSqm += Convert.ToDouble(r["totShrSqm"]); //총합-건물지분
            }

            //목록구분이 집합건물만 있는 경우 필지수 계산
            if (lsType == "집합건물" && ncTr.Count == 2 && landSection != string.Empty)
            {
                MatchCollection mc = Regex.Matches(landSection, @"(\d[\d\.\,]*)[\s]*㎡", rxOptS);
                if (mc != null)
                {
                    if (mc.Count > 1)
                    {
                        sql = "update ta_list set lot_cnt='" + mc.Count + "' where tid=" + tid;
                        db.ExeQry(sql);
                    }
                }
            }

            sql = "update ta_list set land_sqm=@land_sqm, land_tot_sqm=@land_tot_sqm, bldg_sqm=@bldg_sqm, bldg_tot_sqm=@bldg_tot_sqm, rt_sqm=@rt_sqm, rt_tot_sqm=@rt_tot_sqm where tid=" + tid;
            sp.Add(new MySqlParameter("@land_sqm", double.IsInfinity(sumLandSqm) ? 0 : sumLandSqm));
            sp.Add(new MySqlParameter("@land_tot_sqm", double.IsInfinity(sumLandTotSqm) ? 0 : sumLandTotSqm));
            sp.Add(new MySqlParameter("@bldg_sqm", double.IsInfinity(sumBldgSqm) ? 0 : sumBldgSqm));
            sp.Add(new MySqlParameter("@bldg_tot_sqm", double.IsInfinity(sumBldgTotSqm) ? 0 : sumBldgTotSqm));
            sp.Add(new MySqlParameter("@rt_sqm", double.IsInfinity(sumRtSqm) ? 0 : sumRtSqm));
            sp.Add(new MySqlParameter("@rt_tot_sqm", double.IsInfinity(rtTotSqm) ? 0 : rtTotSqm));
            db.ExeQry(sql, sp);
            sp.Clear();

            db.Close();
        }

        private string landUnitConvert(string dvsn, string str)
        {
            string landUnitPtrn1 = @"([\d.,]+)평[ ]*((\d+)홉)*[ ]*((\d+)작)*[ ]*((\d+)재)*";  //평홉작재(1-평, 3-홉, 5-작, 7-재)
            string landUnitPtrn2 = @"([\d.,]+)정[ ]*((\d+)단)*[ ]*((\d+)무)*[ ]*(\d+)*보";    //정단무보(1-정, 3-단, 5-무, 6-보)
            double sqm = 0, phj = 0, jdm = 0;

            str = str.Replace(",", string.Empty);
            string dtlStr = str;

            MatchCollection mc = Regex.Matches(str, landUnitPtrn1, rxOptM);
            foreach (Match m in mc)
            {
                try
                {
                    phj = 0;
                    phj = Convert.ToDouble(string.IsNullOrEmpty(m.Groups[1].Value) ? "0" : m.Groups[1].Value) +
                        (Convert.ToDouble(string.IsNullOrEmpty(m.Groups[3].Value) ? "0" : m.Groups[3].Value) * 0.1) +
                        (Convert.ToDouble(string.IsNullOrEmpty(m.Groups[5].Value) ? "0" : m.Groups[5].Value) * 0.01) +
                        (Convert.ToDouble(string.IsNullOrEmpty(m.Groups[7].Value) ? "0" : m.Groups[7].Value) * 0.001);

                    if (phj > 0)
                    {
                        //sqm = phj * Convert.ToDouble(3.3058);
                        sqm = phj * Convert.ToDouble(3.305785);
                        dtlStr = dtlStr.Replace(m.Value, string.Format("{0}㎡", sqm));
                    }
                }
                catch
                {
                    dtlStr = "";
                }
            }

            if (dvsn == "토지" && mc.Count == 0)
            {
                mc = Regex.Matches(str, landUnitPtrn2, rxOptM);
                foreach (Match m in mc)
                {
                    try
                    {
                        jdm = 0;
                        jdm = (Convert.ToDouble(string.IsNullOrEmpty(m.Groups[1].Value) ? "0" : m.Groups[1].Value) * 3000) +
                            (Convert.ToDouble(string.IsNullOrEmpty(m.Groups[3].Value) ? "0" : m.Groups[3].Value) * 300) +
                            (Convert.ToDouble(string.IsNullOrEmpty(m.Groups[5].Value) ? "0" : m.Groups[5].Value) * 30) +
                            (Convert.ToDouble(string.IsNullOrEmpty(m.Groups[6].Value) ? "0" : m.Groups[6].Value) * 1);
                        if (jdm > 0)
                        {
                            //sqm = jdm * Convert.ToDouble(3.3058);
                            sqm = jdm * Convert.ToDouble(3.305785);
                            dtlStr = dtlStr.Replace(m.Value, string.Format("{0}㎡", sqm));
                        }
                    }
                    catch
                    {
                        dtlStr = "";
                    }
                }
            }

            return dtlStr;
        }

        private Dictionary<string, string> LandShrAreaCal(string landStr, string ratioStr)
        {
            string frtn = "", catNm = "", catCd = "";
            double landNo = 0, nt = 0, dt = 0, shrSqm = 0, rtTotSqm = 0, rtSqm = 0;

            Dictionary<string, string> dict = new Dictionary<string, string>();
            //dict["catCd"] = string.Empty;
            dict["catCd"] = "101017";   //대지-집합일 경우 Default
            dict["rtTotSqm"] = string.Empty;
            dict["rtSqm"] = string.Empty;
            dict["totShrSqm"] = string.Empty;
            dict["frtn"] = string.Empty;

            DataTable dtLand = new DataTable();
            dtLand.Columns.Add("landNo");
            dtLand.Columns.Add("catCd");
            dtLand.Columns.Add("area");

            DataTable dtRatio = new DataTable();
            dtRatio.Columns.Add("no");  //no
            dtRatio.Columns.Add("dt");  //분모
            dtRatio.Columns.Add("nt");  //분자

            List<string> lsPtrn = new List<string>();
            lsPtrn.Add(@"(\d+)\.[ ]*(\d+[\.\d]*)[ ]*분의[ ]*(\d+[\.\d]*)");
            lsPtrn.Add(@"(\d+)\.[ ]*(\d+[\.\d]*)/(\d+[\.\d]*)");

            foreach (string ptrn in lsPtrn)
            {
                MatchCollection mc = Regex.Matches(ratioStr, ptrn, rxOptS);
                if (mc != null)
                {
                    foreach (Match m in mc)
                    {
                        if (ptrn.Contains("분의")) dtRatio.Rows.Add(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
                        else dtRatio.Rows.Add(m.Groups[1].Value, m.Groups[3].Value, m.Groups[2].Value);
                    }
                }
            }

            if (landStr == string.Empty)
            {
                if (dtRatio.Rows.Count > 0)
                {
                    dict["rtTotSqm"] = dtRatio.Rows[0]["dt"].ToString();
                    dict["rtSqm"] = dtRatio.Rows[0]["nt"].ToString();
                }
            }
            else
            {
                MatchCollection mc = Regex.Matches(landStr, @"(\d+)\.[ ]*(.*?)[\s]*(" + landPtrn + "|null" + @")[ ]*(\d[\d\.\,]*)[ ]*㎡", rxOptM);
                if (mc != null)
                {
                    foreach (Match m in mc)
                    {
                        landNo = Convert.ToDouble(m.Groups[1].Value);
                        catNm = m.Groups[1].Value.Trim();
                        if (catNm == "대" || catNm == "null") catNm = "대지";
                        var x = from DataRow r in dtCatCd.Rows
                                where r["cat2_cd"].ToString() == "1010" && r["cat3_nm"].ToString() == catNm
                                select r;
                        if (x.Count() > 0) catCd = x.CopyToDataTable().Rows[0]["cat3_cd"].ToString();
                        else catCd = "101017";
                        dtLand.Rows.Add(landNo, catCd, m.Groups[4].Value.Replace(",", string.Empty));
                    }
                    foreach (DataRow row in dtLand.Rows)
                    {
                        rtTotSqm += Convert.ToDouble(row["area"]);
                    }
                }
                if (dtRatio.Rows.Count > 0)
                {
                    dt = Convert.ToDouble(dtRatio.Rows[0]["dt"]);
                    if (rtTotSqm == dt) rtSqm = Convert.ToDouble(dtRatio.Rows[0]["nt"]);
                    else
                    {
                        rtTotSqm = 0;
                        rtSqm = 0;
                        foreach (DataRow row in dtRatio.Rows)
                        {
                            var xRow = dtLand.Rows.Cast<DataRow>().Where(t => t["landNo"].ToString() == row["no"].ToString()).FirstOrDefault();
                            if (xRow != null)
                            {
                                rtTotSqm += Convert.ToDouble(xRow["area"]);
                                rtSqm += (Convert.ToDouble(xRow["area"]) * Convert.ToDouble(row["nt"])) / Convert.ToDouble(row["dt"]);
                            }
                        }
                    }
                }
                dict["rtTotSqm"] = rtTotSqm.ToString();
                dict["rtSqm"] = rtSqm.ToString();
                if (dtLand.Rows.Count > 0)
                {
                    dict["catCd"] = dtLand.Rows[0]["catCd"].ToString();
                }
            }

            return dict;
        }

        /// <summary>
        /// 자동차/중기 현황
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="dtLs"></param>
        /// <param name="ntRow"></param>
        private void PrcDtlSub_Car(string tid, DataTable dtLs, DataRow ntRow)
        {
            string adrsNdtl, sql;
            string useAdrs, regNo, carNm, carYear, idNo, park, mtr, carType, dist, fuelType, dspl, term;
            string fuelCd = "", cat1 = "", cat2 = "", cat3 = "";
            string adrs, sidoCd, gugunCd, dongCd, riCd, x, y, hCd, pnu, zoneNo, adrsType, regnAdrs, mt;

            IDictionary<string, string> dict = new Dictionary<string, string>();
            List<MySqlParameter> sp = new List<MySqlParameter>();

            adrsNdtl = ntRow["adrsNdtl"].ToString();

            useAdrs = Regex.Match(adrsNdtl, @"[사용본거지]{5,}[ ]*:[ ]*(.*)$", rxOptM).Groups[1].Value.Replace("&nbsp;", string.Empty).Trim();
            park = Regex.Match(adrsNdtl, @"[보관장소\s]{4,}[ ]*:[ ]*(.*?)(<br|$)", rxOptM).Groups[1].Value.Trim();
            regNo = Regex.Match(adrsNdtl, @"[등록번호\s]{4,}[ ]*:[ ]*(.*?)<br", rxOptM).Groups[1].Value.Trim();
            carNm = Regex.Match(adrsNdtl, @"[차명기종\s]{2,}[ ]*:[ ]*(.*?)<br", rxOptM).Groups[1].Value.Trim();
            carYear = Regex.Match(adrsNdtl, @"[연년식\s]{2,}[ ]*:[ ]*(\d{4})", rxOptM).Groups[1].Value.Trim();
            idNo = Regex.Match(adrsNdtl, @"[차대번호\s]{4,}[ ]*:[ ]*([a-z0-9]{15,})", rxOptM).Groups[1].Value.Trim();

            carType = Regex.Match(adrsNdtl, @"[차종\s]{2,}[ ]*:[ ]*(.*)$", rxOptM).Groups[1].Value.Trim();
            mtr = Regex.Match(adrsNdtl, @"[원동기형식]{5,}[ ]*:[ ]*(.*)$", rxOptM).Groups[1].Value.Trim();
            dist = Regex.Match(adrsNdtl, @"[주행거리\s]{4,}[ ]*:[ㄱ-힣\s]+([\d,.]{2,})[ ]*[km㎡]+(<br|$)", rxOptM).Groups[1].Value.Replace(",", string.Empty).Trim();
            dspl = Regex.Match(adrsNdtl, @"[배기량\s]{3,}[ ]*:[ ]*(.*)$", rxOptM).Groups[1].Value.Trim();
            fuelType = Regex.Match(adrsNdtl, @"[사용연료\s]{4,}[ ]*:[ ]*(.*)$", rxOptM).Groups[1].Value.Trim();
            if (fuelType != "")
            {
                DataRow row = dtCarFuel.Rows.Cast<DataRow>().Where(t => t["nm"].ToString() == fuelType).SingleOrDefault();
                fuelCd = (row == null) ? "" : row["cd"].ToString();
            }
            Match match = Regex.Match(adrsNdtl, @"[검사유효기간\s]{6,}[ ]*:[ ]*(\d{4}[.\-\s]+\d{1,2}[.\-\s]+\d{1,2})[.일]{0,1}[\s\-\~\∼]*(\d{4}[.\-\s]+\d{1,2}[.\-\s]+\d{1,2})[.일]{0,1}", rxOptM);
            if (match.Success)
            {
                term = string.Format("{0}~{1}", match.Groups[1].Value, match.Groups[2].Value);
                term = Regex.Replace(term, @"[\-년월]", ".");
                term = term.Replace(" ", string.Empty);
            }
            else
            {
                term = string.Empty;
            }

            if (dtLs.Select("dvsn='자동차'").Count() > 0)
            {
                match = Regex.Match(regNo, @"(\d+)(\w)");
                if (match.Success)
                {
                    decimal no = Convert.ToDecimal(match.Groups[1].Value);
                    if (no >= 70 && no <= 79)
                    {
                        if (Regex.IsMatch(useAdrs, @"서울|대전|대구|부산|광주|울산|인천|제주|세종|경기|강원|경북|경남|전북|전남|충북|충남"))
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
            else if (dtLs.Select("dvsn='건설기계,중기'").Count() > 0)
            {
                if (adrsNdtl.Contains("덤프트럭")) cat3 = "301110";
                else if (adrsNdtl.Contains("굴삭기")) cat3 = "301111";
                else if (adrsNdtl.Contains("지게차")) cat3 = "301112";
                else cat3 = "301113";   //기타중기
            }

            cat1 = cat3.Substring(0, 2);
            cat2 = cat3.Substring(0, 4);

            sql = "delete from ta_cars where tid=" + tid;
            db.Open();
            db.ExeQry(sql);
            db.Close();

            sql = "insert into ta_cars set tid=@tid, ls_no=@ls_no, dvsn=@dvsn, adrs=@use_adrs, car_nm=@car_nm, car_type=@car_type, reg_no=@reg_no, car_year=@car_year, mtr=@mtr, id_no=@id_no, dist=@dist, dspl=@dspl, fuel=@fuel, term=@term, park=@park";
            sp.Add(new MySqlParameter("@tid", tid));
            sp.Add(new MySqlParameter("@ls_no", 1));
            sp.Add(new MySqlParameter("@dvsn", 1));
            sp.Add(new MySqlParameter("@use_adrs", useAdrs));
            sp.Add(new MySqlParameter("@car_nm", carNm));
            sp.Add(new MySqlParameter("@car_type", carType));
            sp.Add(new MySqlParameter("@reg_no", regNo));
            sp.Add(new MySqlParameter("@car_year", carYear));
            sp.Add(new MySqlParameter("@mtr", mtr));
            sp.Add(new MySqlParameter("@id_no", idNo));
            sp.Add(new MySqlParameter("@dist", dist));
            sp.Add(new MySqlParameter("@dspl", dspl));
            sp.Add(new MySqlParameter("@fuel", fuelCd));
            sp.Add(new MySqlParameter("@term", term));
            sp.Add(new MySqlParameter("@park", park));
            db.Open();
            db.ExeQry(sql, sp);
            sp.Clear();
            db.Close();

            if (park != "")
            {
                dict = api.DaumSrchAdrs(park);
                adrs = park;
                sidoCd = dict["sidoCd"];
                gugunCd = dict["gugunCd"];
                dongCd = dict["dongCd"];
                riCd = dict["riCd"];
                hCd = dict["hCd"];
                pnu = dict["pnu"];
                zoneNo = dict["zoneNo"];
                x = dict["x"];
                y = dict["y"];
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
                sql = "update ta_list set adrs=@adrs, adrs_type=@adrs_type, regn_adrs=@regn_adrs, mt=@mt, m_adrs_no=@m_adrs_no, s_adrs_no=@s_adrs_no, road_adrs=@road_adrs, m_bldg_no=@m_bldg_no, s_bldg_no=@s_bldg_no, bldg_nm=@bldg_nm, road_nm=@road_nm, " +
                    "cat1=@cat1, cat2=@cat2, cat3=@cat3, si_cd=@si_cd, gu_cd=@gu_cd, dn_cd=@dn_cd, ri_cd=@ri_cd, x=@x, y=@y where tid='" + tid + "'";
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
                sp.Add(new MySqlParameter("@cat1", cat1));
                sp.Add(new MySqlParameter("@cat2", cat2));
                sp.Add(new MySqlParameter("@cat3", cat3));
                db.Open();
                db.ExeQry(sql, sp);
                sp.Clear();
                db.Close();
            }
        }

        /// <summary>
        /// 선박 현황
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="dtLs"></param>
        /// <param name="ntRow"></param>
        private void PrcDtlSub_Ship(string tid, DataTable dtLs, DataRow ntRow)
        {
            string adrsNdtl, sql;
            string shipType = "", shipNm = "", shipNo, shipMatl, shipWt, launchDt, prpl, mtr, park;
            string adrs, sidoCd, gugunCd, dongCd, riCd, x, y, hCd, pnu, zoneNo, adrsType, regnAdrs, mt;

            IDictionary<string, string> dict = new Dictionary<string, string>();
            List<MySqlParameter> sp = new List<MySqlParameter>();

            adrsNdtl = ntRow["adrsNdtl"].ToString();
                        
            Match match = Regex.Match(adrsNdtl, @"[선박의종류와명칭\s]{8,}[ ]*:[ ]*(.*?)(<br|$)", rxOptM);
            if (match.Success)
            {
                string shipTypeNm = match.Groups[1].Value.Trim();
                shipType = Regex.Match(shipTypeNm, @"(\w+선)\s+(\w+)", rxOptM).Groups[1].Value.Trim();
                shipNm = Regex.Match(shipTypeNm, @"(\w+선)\s+(\w+)", rxOptM).Groups[2].Value.Trim();
            }
            shipNo = Regex.Match(adrsNdtl, @"[어선번호\s]{4,}[ ]*:[ ]*(.*?)(<br|$)", rxOptM).Groups[1].Value.Trim();
            shipMatl = Regex.Match(adrsNdtl, @"[선질\s]{2,}[ ]*:[ ]*(.*)(<br|$)", rxOptM).Groups[1].Value.Trim();
            shipWt = Regex.Match(adrsNdtl, @"[총톤수\s]{3,}[ ]*:[ ]*(.*)(<br|$)", rxOptM).Groups[1].Value.Trim();
            mtr = Regex.Match(adrsNdtl, @"[기관의종류와수\s]{7,}[ ]*:[ ]*(.*?)(<br|$)", rxOptM).Groups[1].Value.Trim();
            prpl = Regex.Match(adrsNdtl, @"[추진기의종류와수\s]{8,}[ ]*:[ ]*(.*?)(<br|$)", rxOptM).Groups[1].Value.Trim();
            launchDt = Regex.Match(adrsNdtl.Replace(" ", string.Empty), @"진수년월일:(\d+년\d+월\d+일)", rxOptM).Groups[1].Value.Trim();
            if (launchDt != string.Empty) launchDt = getDateParse(launchDt);
            park = Regex.Match(adrsNdtl, @"[정박지또는보관장소\s]{3,}[ ]*:[ ]*(.*?)(<br|$)", rxOptM).Groups[1].Value.Trim();
            adrs = (dtLs.Rows.Count > 0) ? dtLs.Rows[0]["adrs"].ToString() : string.Empty;  //선적항

            sql = "insert into ta_cars (tid, ls_no, dvsn, adrs, car_nm, car_type, reg_dt, aprv_no, id_no, mtr, dspl, prpl, park) values (@tid, @ls_no, @dvsn, @adrs, @car_nm, @car_type, @reg_dt, @aprv_no, @id_no, @mtr, @dspl, @prpl, @park)";
            sp.Add(new MySqlParameter("@tid", tid));
            sp.Add(new MySqlParameter("@ls_no", 1));
            sp.Add(new MySqlParameter("@dvsn", 2));
            sp.Add(new MySqlParameter("@adrs", adrs));
            sp.Add(new MySqlParameter("@car_nm", shipNm));
            sp.Add(new MySqlParameter("@car_type", shipType));
            sp.Add(new MySqlParameter("@reg_dt", launchDt));
            sp.Add(new MySqlParameter("@aprv_no", shipMatl));
            sp.Add(new MySqlParameter("@id_no", shipNo));
            sp.Add(new MySqlParameter("@mtr", mtr));
            sp.Add(new MySqlParameter("@dspl", shipWt));
            sp.Add(new MySqlParameter("@prpl", prpl));
            sp.Add(new MySqlParameter("@park", park));
            db.Open();
            db.ExeQry(sql, sp);
            sp.Clear();
            db.Close();

            if (park != "")
            {
                dict = api.DaumSrchAdrs(park);
                adrs = park;
                sidoCd = dict["sidoCd"];
                gugunCd = dict["gugunCd"];
                dongCd = dict["dongCd"];
                riCd = dict["riCd"];
                hCd = dict["hCd"];
                pnu = dict["pnu"];
                zoneNo = dict["zoneNo"];
                x = dict["x"];
                y = dict["y"];
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
                sql = "update ta_list set adrs=@adrs, adrs_type=@adrs_type, regn_adrs=@regn_adrs, mt=@mt, m_adrs_no=@m_adrs_no, s_adrs_no=@s_adrs_no, road_adrs=@road_adrs, m_bldg_no=@m_bldg_no, s_bldg_no=@s_bldg_no, bldg_nm=@bldg_nm, road_nm=@road_nm, " +
                    "si_cd=@si_cd, gu_cd=@gu_cd, dn_cd=@dn_cd, ri_cd=@ri_cd, x=@x, y=@y where tid='" + tid + "'";
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
                db.Open();
                db.ExeQry(sql, sp);
                sp.Clear();
                db.Close();
            }
        }

        /// <summary>
        /// 광업권 현황
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="dtLs"></param>
        private void PrcDtlSub_Mine(string tid, DataTable dtLs, DataRow ntRow)
        {
            string adrsNdtl, sql, dtl;
            string regNo, nm, term, landNo, adrs;
            double totSqm = 0, shrSqm = 0, lsNo = 0;

            List<MySqlParameter> sp = new List<MySqlParameter>();

            dtl = ntRow["adrsNdtl"].ToString();

            MatchCollection mc = Regex.Matches(dtl, @"<div>.*?</div>", rxOptS);
            foreach (Match match in mc)
            {
                term = "";
                shrSqm = 0;

                lsNo++;
                adrsNdtl = match.Value;
                regNo = Regex.Match(adrsNdtl, @"[등록번호\s]{4,}[ ]*:[ ]*(.*?)(<br|$)", rxOptM).Groups[1].Value.Trim();
                adrs = Regex.Match(adrsNdtl, @"[광구소재지\s]{5,}[ ]*:[ ]*(.*?)(<br|$)", rxOptM).Groups[1].Value.Replace("&nbsp;", string.Empty).Trim();
                nm = Regex.Match(adrsNdtl, @"[광물명칭\s]{4,}[ ]*:[ ]*(.*?)(<br|$)", rxOptM).Groups[1].Value.Trim();
                landNo = Regex.Match(adrsNdtl, @"[광업지적\s]{4,}[ ]*:[ ]*(.*?)(<br|$)", rxOptM).Groups[1].Value.Trim();
                Match m = Regex.Match(adrsNdtl, @"[존속기간\s]{8,}[ ]*:[ ]*(\d{4}[년.\s]+\d{1,2}[월.\s]+\d{1,2})[.일]{0,1}[부터\s\-\~\∼]*(\d{4}[년.\s]+\d{1,2}[월.\s]+\d{1,2})[.일]{0,1}", rxOptM);
                if (m.Success)
                {
                    term = string.Format("{0}~{1}", m.Groups[1].Value, m.Groups[2].Value);
                    term = Regex.Replace(term, @"[\-년월]", ".");
                    term = term.Replace(" ", string.Empty);
                }
                m = Regex.Match(adrsNdtl, @"[광구면적\s]{4,}[ ]*:[ ]*([\d.,]+)([^\d]*?)(<br|$)", rxOptM);
                if (m.Groups[1].Value != string.Empty)
                {
                    try
                    {
                        totSqm = Convert.ToDouble(m.Groups[1].Value.Replace(",", string.Empty));
                        if (m.Groups[2].Value.ToLower().Contains("ha"))
                        {
                            totSqm *= 10000;
                        }
                        m = Regex.Match(adrsNdtl, @"지분[\s]*([\d.]*)[\s]*분의[\s]*([\d.]*)", rxOptS);
                        if (m.Success)
                        {
                            shrSqm = totSqm * (Convert.ToDouble(m.Groups[2].Value) / Convert.ToDouble(m.Groups[1].Value));
                        }
                        else
                        {
                            shrSqm = totSqm;
                        }
                    }
                    catch
                    {
                        //
                    }
                }

                sql = "insert into ta_cars set dvsn=@dvsn, tid=@tid, ls_no=@ls_no, adrs=@adrs, reg_no=@reg_no, term=@term, aprv_no=@aprv_no, id_no=@id_no, dist=@dist";
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@ls_no", lsNo));
                sp.Add(new MySqlParameter("@dvsn", 3));
                sp.Add(new MySqlParameter("@adrs", adrs));
                sp.Add(new MySqlParameter("@reg_no", regNo));
                sp.Add(new MySqlParameter("@term", term));
                sp.Add(new MySqlParameter("@aprv_no", nm));
                sp.Add(new MySqlParameter("@id_no", landNo));
                sp.Add(new MySqlParameter("@dist", string.Format("{0}㎡", shrSqm)));
                db.Open();
                db.ExeQry(sql, sp);
                sp.Clear();
                db.Close();
            }
        }

        /// <summary>
        /// 어업권 현황
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="dtLs"></param>
        private void PrcDtlSub_Fish(string tid, DataTable dtLs, DataRow ntRow)
        {
            string adrsNdtl, sql;
            string licenseNo = "", licenseDt = "", term = "", fisheryNm = "", fisheryTime = "", fisheryMtd = "";
            double totSqm = 0, shrSqm = 0;

            List<MySqlParameter> sp = new List<MySqlParameter>();

            adrsNdtl = ntRow["adrsNdtl"].ToString();

            Match match = Regex.Match(adrsNdtl, @"[어업권의존속기간\s]{8,}[ ]*:[ ]*(\d{4}[년.\s]+\d{1,2}[월.\s]+\d{1,2})[.일]{0,1}[부터\s\-\~\∼]*(\d{4}[년.\s]+\d{1,2}[월.\s]+\d{1,2})[.일]{0,1}", rxOptM);
            if (match.Success)
            {
                term = string.Format("{0}~{1}", match.Groups[1].Value, match.Groups[2].Value);
                term = Regex.Replace(term, @"[\-년월]", ".");
                term = term.Replace(" ", string.Empty);
            }
            licenseNo = Regex.Match(adrsNdtl, @"[면먼허번호\s]{4,}[ ]*:[ ]*(.*?)(<br|$)", rxOptM).Groups[1].Value.Trim();
            match = Regex.Match(adrsNdtl, @"[면먼허년월일\s]{5,}[ ]*:[ ]*(\d{4})[년.\s]+(\d{1,2})[월.\s]+(\d{1,2})[.일]{0,1}", rxOptM);
            if (match.Success)
            {
                licenseDt = string.Format("{0}-{1}-{2}", match.Groups[1].Value, match.Groups[2].Value.PadLeft(2, '0'), match.Groups[3].Value.PadLeft(2, '0'));
            }

            fisheryNm = Regex.Match(adrsNdtl, @"[어업및어구의명칭\s]{8,}[ ]*:[ ]*(.*?)(<br|$)", rxOptM).Groups[1].Value.Trim();
            fisheryTime = Regex.Match(adrsNdtl, @"[어업의시기\s]{5,}[ ]*:[ ]*(.*?)(<br|$)", rxOptM).Groups[1].Value.Trim();
            fisheryMtd = Regex.Match(adrsNdtl, @"[어업의방법\s]{5,}[ ]*:[ ]*(.*?)(<br|$)", rxOptM).Groups[1].Value.Trim();

            match = Regex.Match(adrsNdtl, @"[어장면적\s]{4,}[ ]*:[ ]*([\d.,]+)([^\d]*?)(<br|$)", rxOptM);
            if (match.Groups[1].Value != string.Empty)
            {
                try
                {
                    totSqm = Convert.ToDouble(match.Groups[1].Value.Replace(",", string.Empty));
                    if (match.Groups[2].Value.ToLower().Contains("ha"))
                    {
                        totSqm *= 10000;
                    }
                    match = Regex.Match(adrsNdtl, @"지분[\s]*([\d.]*)[\s]*분의[\s]*([\d.]*)", rxOptS);
                    if (match.Success)
                    {
                        shrSqm = totSqm * (Convert.ToDouble(match.Groups[2].Value) / Convert.ToDouble(match.Groups[1].Value));
                    }
                    else
                    {
                        shrSqm = totSqm;
                    }
                }
                catch
                {
                    //
                }
            }

            sql = "insert into ta_cars set dvsn=@dvsn, tid=@tid, ls_no=@ls_no, reg_no=@reg_no, reg_dt=@reg_dt, term=@term, car_nm=@car_nm, id_no=@id_no, mtr=@mtr, dist=@dist";
            sp.Add(new MySqlParameter("@tid", tid));
            sp.Add(new MySqlParameter("@ls_no", 1));
            sp.Add(new MySqlParameter("@dvsn", 4));
            sp.Add(new MySqlParameter("@reg_no", licenseNo));
            sp.Add(new MySqlParameter("@reg_dt", licenseDt));
            sp.Add(new MySqlParameter("@term", term));
            sp.Add(new MySqlParameter("@car_nm", fisheryNm));
            sp.Add(new MySqlParameter("@id_no", fisheryTime));
            sp.Add(new MySqlParameter("@mtr", fisheryMtd));
            sp.Add(new MySqlParameter("@dist", string.Format("{0}㎡", shrSqm)));
            db.Open();
            db.ExeQry(sql, sp);
            sp.Clear();
            db.Close();
        }

        /// <summary>
        /// 등기 다운/업/추출(굿옥션)-사용안함
        /// </summary>
        private void Prc_RgstAnaly()
        {
            return;

            int i = 0, curCnt = 0, totCnt = 0;
            int sucCnt = 0, failCnt = 0;
            string sql, url, jsData, gdLawCd, spt, sn, sn1, sn2, pn, tid;
            string ctgr, fileNm, fileUrl, locFile, rmtFile, tbl, cvp;
            string rgstDnPath, tkFileNm, errMsg, spRgst;
            
            rgstDnPath = filePath + @"\등기";
            if (!Directory.Exists(rgstDnPath))
            {
                Directory.CreateDirectory(rgstDnPath);
            }

            progrsView("등기수집");   //진행상태

            sql = "select spt_cd, _gd_cd from ta_cd_cs";
            DataTable dtCs = db.ExeDt(sql);

            sql = "select tid,spt,sn1,sn2,pn,sp_rgst from ta_list where pre_dt=curdate() and cat1 in (0,10,20,40) and works=0 and pre_prc < 3 order by tid";
            DataTable dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;

            foreach (DataRow row in dt.Rows)
            {
                i = 0;
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                tid = row["tid"].ToString();
                spt = row["spt"].ToString();
                sn1 = row["sn1"].ToString();
                sn2 = row["sn2"].ToString();
                pn = row["pn"].ToString();
                spRgst = row["sp_rgst"].ToString();

                progrsView(string.Format("[등기] {0} -> {1} / {2}", tid, curCnt, totCnt), 1);     //진행상태

                var xRow = dtCs.Rows.Cast<DataRow>().Where(t => t["spt_cd"].ToString() == row["spt"].ToString()).SingleOrDefault();
                if (xRow == null || xRow["_gd_cd"].ToString() == "0")
                {
                    errMsg = "법원코드 매칭 오류";
                    continue;
                }

                try
                {
                    gdLawCd = xRow["_gd_cd"].ToString();
                    sn = string.Format("{0}{1}-{2}", sn1, sn2.PadLeft(6, '0'), pn.PadLeft(4, '0'));
                    url = string.Format("https://intra.auction1.co.kr/partner/f22_fi.php?lawCd={0}&sn1={1}&sn2={2}&pn={3}", gdLawCd, sn1, sn2, pn);
                    jsData = net.GetHtml(url);
                    dynamic x = JsonConvert.DeserializeObject(jsData);
                    var items = x["item"];
                    if (items == null)
                    {
                        failCnt++;
                        errMsg = "파일정보 없음";
                        //atomLog.AddLog(string.Format("TID-{0} : {1}", tid, errMsg));

                        sql = "insert ignore into db_tank.tx_rgst_err set tid='" + tid + "', dvsn=1, pre=1, wdt=curdate()";
                        db.Open();
                        db.ExeQry(sql);
                        db.Close();
                        continue;
                    }

                    RgstAnalyNew rgstAnaly = new RgstAnalyNew();

                    Regex rx = new Regex(@"_(\d).pdf", rxOptM);
                    JArray jsArr = JArray.Parse(items.ToString());
                    foreach (JObject item in jsArr)
                    {
                        //analyFlag = false;
                        ctgr = item["ctgr"].ToString();
                        fileNm = item["fileNm"].ToString();
                        Match m = rx.Match(fileNm);
                        fileUrl = item["fileUrl"].ToString();
                        tkFileNm = string.Format("{0}-{1}-{2}-{3}.pdf", ctgr, spt, sn, m.Groups[1].Value.PadLeft(2, '0'));
                        locFile = string.Format(@"{0}\{1}", rgstDnPath, tkFileNm);
                        Dictionary<string, string> dnRslt = net.DnFile(fileUrl, locFile);
                        if (dnRslt["result"] == "fail")
                        {
                            errMsg = "파일 다운로드 실패";
                            continue;
                        }
                        rmtFile = string.Format("{0}/{1}/{2}/{3}", ctgr, spt, sn1, tkFileNm);
                        
                        if (ftp1.Upload(locFile, rmtFile))
                        {
                            //DB 처리
                            tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
                            cvp = "ctgr='" + ctgr + "', spt='" + spt + "', tid='" + tid + "', sn='" + sn + "', file='" + tkFileNm + "', wdt=curdate()";
                            sql = "insert into " + tbl + " set " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                            db.Open();
                            db.ExeQry(sql);
                            db.Close();
                            sucCnt++;
                        }
                        else
                        {
                            errMsg = "파일 업로드 실패";
                            continue;
                        }

                        if (m.Groups[1].Value != "1") continue;       //등기_1 만 추출
                        if (ctgr == "DA" && (spRgst == "1" || spRgst == "5" || spRgst == "6")) continue;  //토지별도등기/토지별도등기인수조건/미등기가격포함+토지별도등기 는 추출안함

                        string analyRslt = rgstAnaly.Proc(locFile, true);

                        if (analyRslt != "success")
                        {
                            errMsg = analyRslt;
                            continue;
                        }
                    }

                    //임차인 및 등기에서 특수조건 검출
                    spCdtnChk.RgstLeas(tid);

                    db.Open();
                    sql = "update ta_list set pre_prc=3 where tid=" + tid;
                    db.ExeQry(sql);
                    db.Close();
                }
                catch (Exception ex)
                {
                    atomLog.AddLog(string.Format("TID-{0} : {1}", tid, ex.Message));
                }
            }

            atomLog.AddLog(string.Format(" > 성공-{0}, 파일없음-{1}", sucCnt, failCnt));
        }

        /// <summary>
        /// 등기 자동발급 대상 추가
        /// </summary>
        private void Prc_RgstIssueAdd()
        {
            string sql, tid, tbl, prevTid = "";
            bool fileExist, autoExist;
            int landCnt = 0, bldgCnt = 0, multiCnt = 0, issueCnt = 0, dpslDvsn = 0;
            string autoDvsn = "12";     //발급 구분 -> 선행 공고

            progrsView("등기 자동발급 대상");   //진행상태

            DataTable dt = new DataTable();
            dt.Columns.Add("tid");
            dt.Columns.Add("lsIdx");
            dt.Columns.Add("lsNo");
            dt.Columns.Add("lsType");
            dt.Columns.Add("pin");

            //사건별 물건번호 최대값 산출
            sql = "select max(pn) as maxPN,L.tid,spt,sn1,sn2,pn,cat3, S.idx,no,dvsn,pin,pnu from ta_list L, ta_ls S where L.tid=S.tid and sta2=1110 and cat1!=30 and bid_dt >= curdate() and pre_dt=curdate() GROUP by spt,sn1,sn2 HAVING maxPN > 20";
            DataTable dtMax = db.ExeDt(sql);

            //sql = "select L.tid,spt,sn1,sn2,cat3,pn, 0 as 'maxPN', S.idx,no,dvsn,pin,pnu from ta_list L, ta_ls S where L.tid=S.tid and sta2=1110 and cat1!=30 and bid_dt >= curdate() and pre_dt=curdate() and works=0 order by L.tid";
            sql = "select L.tid,spt,sn1,sn2,cat3,pn,dpsl_dvsn, 0 as 'maxPN', S.idx,no,dvsn,pin,pnu from ta_list L, ta_ls S where L.tid=S.tid and sta2=1110 and cat1!=30 and bid_dt >= curdate() and pre_dt=curdate() and works=0 and S.note='미종국' order by L.tid";
            DataTable dtLs = db.ExeDt(sql);
            
            //사건별 비교용 최대값 업데이트
            foreach (DataRow dr in dtMax.Rows)
            {
                foreach (DataRow row in dtLs.Rows)
                {
                    if ($"{row["spt"]}" == $"{dr["spt"]}" && $"{row["sn1"]}" == $"{dr["sn1"]}" && $"{row["sn2"]}" == $"{dr["sn2"]}")
                    {
                        row["maxPN"] = dr["maxPN"];
                    }
                }
            }

            foreach (DataRow row in dtLs.Rows)
            {
                tid = row["tid"].ToString();
                dpslDvsn = Convert.ToInt32(row["dpsl_dvsn"]);
                if (tid == prevTid) continue;

                //등기파일 유무 체크
                tbl = (Convert.ToDecimal($"{row["sn1"]}") > 2004) ? ("ta_f" + $"{row["sn1"]}") : "ta_f2004";
                sql = "select * from " + tbl + " where ctgr in ('DA','DB') and tid=" + tid + " limit 1";
                db.Open();
                fileExist = db.ExistRow(sql);
                db.Close();
                //fileExist = false;  //Test
                if (fileExist)
                {
                    prevTid = tid;
                    continue;
                }

                //해당사건의 최대 물건번호가 20번이 넘는 경우 10번 까지만 발급한다.
                if (Convert.ToInt16(row["maxPN"]) > 20)
                {
                    if (Convert.ToInt16(row["pn"]) > 10)
                    {
                        //MessageBox.Show($"{row["sn1"]}-{row["sn2"]} ({row["pn"]})");
                        prevTid = tid;
                        continue;
                    }
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

                DataTable dtS = rows.CopyToDataTable();
                landCnt = dtS.Select("dvsn='토지'").Count();
                bldgCnt = dtS.Select("dvsn='건물'").Count();
                multiCnt = dtS.Select("dvsn='집합건물'").Count();
                                
                //A,B,C | B,C - 패스
                if (bldgCnt == 1 && multiCnt == 1)
                {
                    prevTid = tid;
                    continue;
                }

                DataTable dtL = db.ExeDt($"select ls_no, sqm from ta_land where tid='{tid}' order by sqm desc");
                DataTable dtB = db.ExeDt($"select ls_no, sqm from ta_bldg where tid='{tid}' and dvsn=1 order by sqm desc");

                //A-토지만
                if (bldgCnt == 0 && multiCnt == 0)
                {
                    if (dtL.Rows.Count > 0)
                    {
                        //DataRow r = dtS.Rows.Cast<DataRow>().OrderByDescending(t => t[""]).FirstOrDefault();
                        var r = dtS.Rows.Cast<DataRow>().Where(t => t["no"].ToString() == dtL.Rows[0]["ls_no"].ToString()).FirstOrDefault();
                        if (r != null)
                        {
                            if (r["pin"].ToString() != String.Empty)
                            {
                                if ((dpslDvsn == 13 || dpslDvsn == 16) && dtL.Rows.Count > 1)
                                {
                                    //토지만 매각, 토지만 매각이며 지분매각 && 목록이 2개이상인 경우는 제외
                                }
                                else
                                {
                                    dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["dvsn"], r["pin"] });    //대상
                                }                                    
                            }
                        }
                    }
                }

                //B-건물만
                if (landCnt == 0 && multiCnt == 0)
                {
                    if (dtB.Rows.Count > 0)
                    {
                        var r = dtS.Rows.Cast<DataRow>().Where(t => t["no"].ToString() == dtB.Rows[0]["ls_no"].ToString()).FirstOrDefault();
                        if (r != null)
                        {
                            if (r["pin"].ToString() != String.Empty)
                            {
                                dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["dvsn"], r["pin"] });    //대상
                            }
                        }
                    }
                }

                //C-집합만
                if (landCnt == 0 && bldgCnt == 0)
                {
                    if (dtB.Rows.Count > 0)
                    {
                        var r = dtS.Rows.Cast<DataRow>().Where(t => t["no"].ToString() == dtB.Rows[0]["ls_no"].ToString()).FirstOrDefault();
                        if (r != null)
                        {
                            if (r["pin"].ToString() != String.Empty)
                            {
                                dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["dvsn"], r["pin"] });    //대상
                            }
                        }
                    }
                }

                //A,B-토지,건물
                if (landCnt > 0 && bldgCnt > 0)
                {
                    if (dtB.Rows.Count > 0)
                    {
                        var r = dtS.Rows.Cast<DataRow>().Where(t => t["no"].ToString() == dtB.Rows[0]["ls_no"].ToString()).FirstOrDefault();
                        if (r != null)
                        {
                            if (r["pin"].ToString() != String.Empty)
                            {
                                /*
                                if (dpslDvsn == 13 || dpslDvsn == 16) 
                                { 
                                    //토지만 매각, 토지만 매각이며 지분매각-건물 제외
                                }
                                else
                                {
                                    dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["dvsn"], r["pin"] });    //대상-건물
                                }
                                */
                                dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["dvsn"], r["pin"] });    //대상-건물
                            }

                            r = dtS.Rows.Cast<DataRow>().Where(t => t["dvsn"].ToString() == "토지" && t["pnu"].ToString() == r["pnu"].ToString()).FirstOrDefault();
                            if (r != null)
                            {
                                if (r["pin"].ToString() != String.Empty)
                                {
                                    dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["dvsn"], r["pin"] });    //대상-토지(건물과 동일PNU)
                                }
                            }
                        }
                    }
                }

                //A,C-토지,집합
                bool findLand = false;
                if (landCnt > 0 && multiCnt > 0)
                {
                    if (dtB.Rows.Count > 0)
                    {
                        var r = dtS.Rows.Cast<DataRow>().Where(t => t["no"].ToString() == dtB.Rows[0]["ls_no"].ToString()).FirstOrDefault();
                        if (r != null)
                        {
                            if (r["pin"].ToString() != String.Empty)
                            {
                                dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["dvsn"], r["pin"] });    //대상-집합
                            }

                            r = dtS.Rows.Cast<DataRow>().Where(t => t["dvsn"].ToString() == "토지" && t["pnu"].ToString() == r["pnu"].ToString()).FirstOrDefault();
                            if (r != null)
                            {
                                if (r["pin"].ToString() != String.Empty)
                                {
                                    dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["dvsn"], r["pin"] });    //대상-토지(집합과 동일PNU)
                                }
                                findLand = true;
                            }
                        }
                    }

                    if (!findLand && dtL.Rows.Count > 0)
                    {
                        var r = dtS.Rows.Cast<DataRow>().Where(t => t["no"].ToString() == dtL.Rows[0]["ls_no"].ToString()).FirstOrDefault();
                        if (r != null)
                        {
                            if (r["pin"].ToString() != String.Empty)
                            {
                                dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["dvsn"], r["pin"] });    //대상-토지
                            }
                        }
                    }
                }

                prevTid = tid;
            }
            
            if (dt.Rows.Count > 0)
            {
                //대상 db 일괄 추가
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
            
            atomLog.AddLog($" > 발급 대상-{issueCnt}");
        }

        /// <summary>
        /// 역세권 매칭
        /// </summary>
        private void Prc_Station()
        {
            int mvCnt = 0;
            string sql, tid, cd;
            double lat_p = 0, lng_p = 0, lat_s = 0, lng_s = 0, distance = 0;

            progrsView("[역세권 매칭]");
            CoordCal cc = new CoordCal();

            sql = "select * from tx_railroad order by local_cd,line_cd,station_cd";
            DataTable dtR = db.ExeDt(sql);

            sql = "select tid, x, y from ta_list where pre_dt=curdate() and x > 0 and station_prc=0 order by tid";
            DataTable dt = db.ExeDt(sql);

            foreach (DataRow row in dt.Rows)
            {
                tid = row["tid"].ToString();
                lng_p = Convert.ToDouble(row["x"]);     //경도
                lat_p = Convert.ToDouble(row["y"]);     //위도

                foreach (DataRow srow in dtR.Rows)
                {
                    lng_s = Convert.ToDouble(srow["x"]);
                    lat_s = Convert.ToDouble(srow["y"]);
                    distance = cc.calDistance(lat_p, lng_p, lat_s, lng_s);
                    if (distance >= 0 && distance <= 1000)
                    {
                        cd = string.Format("{0}{1}{2}", srow["local_cd"], srow["line_cd"].ToString().PadLeft(2, '0'), srow["station_cd"].ToString().PadLeft(3, '0'));

                        db.Open();
                        sql = "insert ignore into ta_railroad set tid='" + tid + "', cd='" + cd + "', distance='" + distance.ToString() + "', wdt=curdate()";
                        db.ExeQry(sql);
                        sql = "update ta_list set station_prc=1 where tid='" + tid + "'";
                        db.ExeQry(sql);
                        db.Close();
                        mvCnt++;
                    }
                }
            }
            atomLog.AddLog(string.Format(" > 매칭-{0}", mvCnt));
        }

        /// <summary>
        /// 집합건물(아파트)코드 매칭
        /// </summary>
        private void Prc_AptCd()
        {
            int mvCnt = 0;
            string sql, tid, aptCd, aptNm, bunji;

            progrsView("[집합건물코드 매칭]");

            sql = "select * from tx_apt where match_type in (1,3)";
            DataTable dtA = db.ExeDt(sql);
                        
            sql = "select * from ta_list where pre_dt=curdate() and apt_cd=0 and cat3 in (201013,201015,201020,201111,201123,201130,201216)";  //아파트, 다세대주택, 오피스텔(주거), 오피스텔(상업), 숙박(콘도)등, 근린상가, 지식산업센터(아파트형공장)
            DataTable dt = db.ExeDt(sql);

            foreach (DataRow row in dt.Rows)
            {
                aptCd = string.Empty;
                tid = row["tid"].ToString();
                bunji = row["m_adrs_no"].ToString();
                if (row["mt"].ToString() == "2") bunji = "산" + bunji;
                if (row["s_adrs_no"].ToString() != "0") bunji += "-" + row["s_adrs_no"].ToString();
                DataRow[] aptRows = dtA.Select(string.Format("si_key='{0}' and gu_key='{1}' and dong_key='{2}' and ri_key='{3}' and bunji='{4}'", row["si_cd"], row["gu_cd"], row["dn_cd"], row["ri_cd"], bunji));
                if (aptRows.Count() == 0) continue;
                foreach (DataRow aptRow in aptRows)
                {
                    aptNm = aptRow["dj_name"].ToString();
                    if (row["adrs"].ToString().Contains(aptNm) || row["bldg_nm"].ToString().Contains(aptNm))
                    {
                        aptCd = aptRow["apt_code"].ToString();
                    }
                }
                if (aptCd == string.Empty) continue;
                sql = "update ta_list set apt_cd='" + aptCd + "' where tid=" + tid;
                db.Open();
                db.ExeQry(sql);
                db.Close();
                mvCnt++;
            }
            atomLog.AddLog(string.Format(" > 매칭-{0}", mvCnt));
        }

        /// <summary>
        /// 사용승인일자-국토교통부_건축물대장 표제부 조회
        /// </summary>
        private void Prc_AprvDt()
        {
            int totalCnt, mvCnt = 0, eqCnt = 0, totCnt, curCnt;
            string sql, url, xml, serviceKey, tid, lsNo, adrs0, adrs, pnu, platGbCd, bun, ji, newPlatPlc, aprvDt, flrCnt, dongNm, elvtCnt, idx;

            Dictionary<string, string> dic = new Dictionary<string, string>();  //승인일자, 총층수
            Dictionary<string, string> dic2 = new Dictionary<string, string>(); //동명칭, 승인일자

            sql = "SELECT L.tid,S.no,S.pnu,S.adrs,B.tot_flr,B.elvt,B.idx from ta_list L , ta_ls S , ta_bldg B WHERE L.tid=S.tid and S.tid=B.tid and S.no=B.ls_no and L.pre_dt=curdate() and S.dvsn in ('건물','집합건물') and B.dvsn=1 and works=0 and pre_prc < 4";
            DataTable dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;
            curCnt = 0;

            atomLog.AddLog(string.Format(" > 대상-{0}", totCnt));     //로그기록

            XmlDocument doc = new XmlDocument();

            serviceKey = api.RndSrvKey();

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                dic.Clear();
                dic2.Clear();
                aprvDt = string.Empty;
                eqCnt = 0;

                idx = row["idx"].ToString();
                tid = row["tid"].ToString();
                lsNo = row["no"].ToString();
                pnu = row["pnu"].ToString();
                adrs0 = row["adrs"].ToString();
                adrs = row["adrs"].ToString();
                flrCnt = row["tot_flr"].ToString();
                if (flrCnt == "0") flrCnt = "1";
                elvtCnt = row["elvt"].ToString();

                progrsView(string.Format("[사용승인] TID -> {0} ^ {1} / {2}", tid, curCnt, totCnt), 1);  //진행상태
                if (pnu == string.Empty || pnu == "0") continue;

                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                platGbCd = (Convert.ToDecimal(pnu.Substring(10, 1)) - 1).ToString();
                bun = pnu.Substring(11, 4);
                ji = pnu.Substring(15, 4);

                url = "http://apis.data.go.kr/1613000/BldRgstService_v2/getBrTitleInfo?serviceKey=" + serviceKey + "&sigunguCd=" + pnu.Substring(0, 5) + "&bjdongCd=" + pnu.Substring(5, 5) + "&platGbCd=" + platGbCd + "&bun=" + bun + "&ji=" + ji + "&numOfRows=100&pageNo=1";
                xml = net.GetHtml(url, Encoding.UTF8);
                if (xml.Contains("totalCount") == false) continue;

                doc.LoadXml(xml);
                XmlNamespaceManager nsmg = new XmlNamespaceManager(doc.NameTable);
                nsmg.AddNamespace("n", doc.DocumentElement.NamespaceURI);
                XmlNode nd_total = doc.SelectSingleNode("/n:response/n:body/n:totalCount", nsmg);
                totalCnt = Convert.ToInt16(nd_total.InnerText);
                if (totalCnt == 0) continue;

                if (totalCnt == 1)
                {
                    aprvDt = (doc.SelectSingleNode("/n:response/n:body/n:items/n:item/n:useAprDay", nsmg) == null) ? string.Empty : doc.SelectSingleNode("/n:response/n:body/n:items/n:item/n:useAprDay", nsmg).InnerText.Trim();
                    elvtCnt = (doc.SelectSingleNode("/n:response/n:body/n:items/n:item/n:rideUseElvtCnt", nsmg) == null) ? string.Empty : doc.SelectSingleNode("/n:response/n:body/n:items/n:item/n:rideUseElvtCnt", nsmg).InnerText.Trim();
                }
                else
                {
                    if (adrs.Contains(",")) adrs = adrs.Remove(adrs.IndexOf(","));
                    if (adrs.Contains("(")) adrs = adrs.Remove(adrs.IndexOf("("));
                    adrs = adrs.Replace(" ", string.Empty).Trim();

                    foreach (XmlNode item in doc.SelectNodes("/n:response/n:body/n:items/n:item", nsmg))
                    {
                        newPlatPlc = (item.SelectSingleNode("newPlatPlc", nsmg) == null) ? string.Empty : item.SelectSingleNode("newPlatPlc", nsmg).InnerText.Replace(" ", string.Empty).Trim();
                        if (newPlatPlc == string.Empty) continue;
                        if (adrs == newPlatPlc)
                        {
                            aprvDt = (item.SelectSingleNode("useAprDay", nsmg) == null) ? string.Empty : item.SelectSingleNode("useAprDay", nsmg).InnerText.Trim();
                            flrCnt = (item.SelectSingleNode("grndFlrCnt", nsmg) == null) ? "0" : item.SelectSingleNode("grndFlrCnt", nsmg).InnerText.Trim();
                            dongNm = (item.SelectSingleNode("dongNm", nsmg) == null) ? string.Empty : item.SelectSingleNode("dongNm", nsmg).InnerText.Trim();
                            elvtCnt = (item.SelectSingleNode("rideUseElvtCnt", nsmg) == null) ? string.Empty : item.SelectSingleNode("rideUseElvtCnt", nsmg).InnerText.Trim();
                            if (aprvDt == string.Empty) continue;
                            if (dic.ContainsKey(aprvDt) == false) dic.Add(aprvDt, flrCnt);
                            if (dic2.ContainsKey(dongNm) == false) dic2.Add(dongNm, aprvDt);
                        }
                    }
                    if (dic.Count == 1) aprvDt = dic.First().Key;
                    else if (dic.Count > 1)
                    {
                        foreach (KeyValuePair<string, string> kvp in dic)
                        {
                            if (kvp.Value == flrCnt)
                            {
                                if (eqCnt == 0)
                                {
                                    aprvDt = kvp.Key;
                                }
                                eqCnt++;
                            }
                        }
                        if (eqCnt > 1)
                        {
                            aprvDt = string.Empty;
                            //Match match = Regex.Match(adrs0, @"\(\w+동,([\w\d]+)\)"); //(xx동, 건물명)
                            Match match = Regex.Match(adrs0, @"\((\w+동)\)"); //(xx동)
                            if (match.Success)
                            {
                                foreach (KeyValuePair<string, string> kvp in dic2)
                                {
                                    if (kvp.Key == match.Groups[1].Value.Trim())
                                    {
                                        aprvDt = kvp.Value;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                if (aprvDt == string.Empty) continue;

                if (aprvDt.Length == 4) aprvDt = aprvDt + "0000";
                else if (aprvDt.Length == 6) aprvDt = aprvDt + "00";

                //dtTest.Rows.Add(idx, aprvDt);
                
                db.Open();
                sql = "update ta_bldg set aprv_dt='" + aprvDt + "', elvt='" + elvtCnt + "' where idx='" + idx + "' and tid='" + tid + "'";
                db.ExeQry(sql);

                sql = "update ta_list set pre_prc=4 where tid=" + tid;
                db.ExeQry(sql);
                db.Close();
                mvCnt++;
            }
            //MessageBox.Show(dtTest.Rows.Count.ToString());
            atomLog.AddLog(string.Format(" > 매칭-{0}", mvCnt));
        }

        /// <summary>
        /// 토지이용계획
        /// </summary>
        private void Prc_LandUse()
        {
            int totCnt = 0, curCnt = 0, mvCnt = 0;
            string sql, tid, idx, pnu, dbPrpsNm;
            int totalCnt = 0;
            string url, xml, prpsCd = "", prpsNm = "", useCdtn = "";
            string prposAreaDstrcCode = "", prposAreaDstrcCodeNm = "";

            DataTable dt;
            XmlDocument doc = new XmlDocument();
            ArrayList alCd = new ArrayList();
            ArrayList alNm = new ArrayList();
            List<string> lsRslt = new List<string>();

            DataTable dtUse = db.ExeDt("select * from tx_cd_use where level3 > 0 order by level3");

            sql = "select L.tid, L.idx, L.prps_nm, S.pnu from ta_list T, ta_ls S, ta_land L where T.tid=S.tid and S.tid=L.tid and S.no=L.ls_no and sta1=11 and plan_prc in (0,2) and cat3 not in (0,201013,201014,201015,201017,201019,201022,201130,201216,201123,201020,201111) and pre_dt=curdate()";
            dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;
            curCnt = 0;

            atomLog.AddLog(string.Format(" > 대상-{0}", totCnt));     //로그기록

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                alCd.Clear();
                alNm.Clear();
                lsRslt.Clear();
                useCdtn = string.Empty;
                tid = row["tid"].ToString();
                idx = row["idx"].ToString();
                pnu = row["pnu"].ToString();
                dbPrpsNm = row["prps_nm"].ToString();

                progrsView(string.Format("[토지이용] TID -> {0} ^ {1} / {2}", tid, curCnt, totCnt), 1);  //진행상태

                if (pnu == "0") continue;

                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                url = "http://apis.data.go.kr/1611000/nsdi/LandUseService/attr/getLandUseAttr?serviceKey=" + api.RndSrvKey() + "&cnflcAt=1&format=xml&numOfRows=50&pageSize=10&pageNo=1&startPage=1&pnu=" + pnu;
                xml = net.GetHtml(url, Encoding.UTF8);
                if (xml.Contains("totalCount") == false)
                {
                    PrcLandUsePrice_Error("plan", idx);
                    continue;
                }

                doc.LoadXml(xml);
                XmlNamespaceManager nsmg = new XmlNamespaceManager(doc.NameTable);
                nsmg.AddNamespace("n", doc.DocumentElement.NamespaceURI);
                XmlNode nd_total = doc.SelectSingleNode("/n:response/n:totalCount", nsmg);
                totalCnt = Convert.ToInt16(nd_total.InnerText);
                if (totalCnt == 0)
                {
                    PrcLandUsePrice_Error("plan", idx);
                    continue;
                }

                foreach (XmlNode item in doc.SelectNodes("/n:response/n:fields/n:field", nsmg))
                {
                    prposAreaDstrcCode = item.SelectSingleNode("prposAreaDstrcCode", nsmg) == null ? "" : item.SelectSingleNode("prposAreaDstrcCode", nsmg).InnerText.Trim();
                    prposAreaDstrcCodeNm = item.SelectSingleNode("prposAreaDstrcCodeNm", nsmg) == null ? "" : item.SelectSingleNode("prposAreaDstrcCodeNm", nsmg).InnerText.Trim();
                    alCd.Add(prposAreaDstrcCode);
                    alNm.Add(prposAreaDstrcCodeNm);

                    foreach (DataRow r in dtUse.Rows)
                    {
                        if (r["prps_cd"].ToString() == prposAreaDstrcCode)
                        {
                            if (lsRslt.Contains(r["level3"].ToString())) continue;
                            lsRslt.Add(r["level3"].ToString());
                        }
                    }
                }

                prpsCd = String.Join(",", alCd.ToArray());
                prpsNm = String.Join(",", alNm.ToArray());

                if (lsRslt.Count > 0)
                {
                    useCdtn = string.Join(",", lsRslt);
                }

                if (dbPrpsNm == string.Empty)
                {
                    sql = "update ta_land set prps_cd='" + prpsCd + "', prps_nm='" + prpsNm + "', use_cdtn='" + useCdtn + "', plan_prc=1 where idx=" + idx;
                }
                else
                {
                    sql = "update ta_land set prps_cd='" + prpsCd + "', use_cdtn='" + useCdtn + "', plan_prc=1 where idx=" + idx;
                }
                db.Open();
                db.ExeQry(sql);
                db.Close();
                mvCnt++;
            }
            atomLog.AddLog(string.Format(" > 수집-{0}", mvCnt));
        }

        /// <summary>
        /// 개별공시지가
        /// </summary>
        private void Prc_LandPrice()
        {
            int totCnt = 0, curCnt = 0, mvCnt = 0;
            string sql, tid, idx, pnu;
            int totalCnt = 0;
            string url, xml, cvp, jsData = "", html, src = "0";
            string ldCodeNm = "", mnnmSlno = "", stdrYear = "", stdrMt = "", pblntfPclnd = "", pblntfDe = "", lastUpdtDt = "";

            DataTable dt;
            var jaPrice = new JArray();
            XmlDocument doc = new XmlDocument();

            sql = "select L.tid, L.idx, S.pnu from ta_list T, ta_ls S, ta_land L where T.tid=S.tid and S.tid=L.tid and S.no=L.ls_no and sta1=11 and price_prc in (0,2) and cat3 not in (0,201013,201014,201015,201017,201019,201022,201130,201216,201123,201020,201111) and pre_dt=curdate()";
            dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;

            atomLog.AddLog(string.Format(" > 대상-{0}", totCnt));     //로그기록

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                jaPrice.Clear();
                tid = row["tid"].ToString();
                idx = row["idx"].ToString();
                pnu = row["pnu"].ToString();

                progrsView(string.Format("[공시지가] TID -> {0} ^ {1} / {2}", tid, curCnt, totCnt), 1);  //진행상태

                if (pnu == "0") continue;

                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                url = "http://apis.data.go.kr/1611000/nsdi/IndvdLandPriceService/attr/getIndvdLandPriceAttr?serviceKey=" + api.RndSrvKey() + "&format=xml&numOfRows=100&pageNo=1&pnu=" + pnu;
                xml = net.GetHtml(url, Encoding.UTF8);
                if (xml.Contains("totalCount") == false)
                {
                    PrcLandUsePrice_Error("price", idx);
                    continue;
                }

                doc.LoadXml(xml);
                XmlNamespaceManager nsmg = new XmlNamespaceManager(doc.NameTable);
                nsmg.AddNamespace("n", doc.DocumentElement.NamespaceURI);
                XmlNode nd_total = doc.SelectSingleNode("/n:response/n:totalCount", nsmg);
                totalCnt = Convert.ToInt16(nd_total.InnerText);
                if (totalCnt == 0)
                {
                    //PrcLandUsePrice_Error("price", idx);
                    //continue;
                    //실패시 2차로 토지e음 연동하여 매칭시도
                    webCnt++;
                    url = "https://www.eum.go.kr/web/ar/lu/luLandDetYearAjax.jsp?pnu=" + pnu;
                    html = net.GetHtml(url).Trim();
                    MatchCollection mc = Regex.Matches(html, @"<tr>\s+<td>(\d{4})/(\d{2})</td>\s+<td>([\d\,]+)원</td></tr>", RegexOptions.Singleline);
                    if (mc != null)
                    {
                        foreach (Match m in mc.Cast<Match>().Reverse())
                        {
                            var obj = new JObject();
                            obj.Add("stdrYear", m.Groups[1].Value);
                            obj.Add("pblntfPclnd", m.Groups[3].Value.Replace(",", string.Empty));
                            obj.Add("stdrMt", m.Groups[2].Value);
                            obj.Add("pblntfDe", string.Empty);
                            jaPrice.Add(obj);
                        }
                        src = "1";
                    }
                }
                else
                {
                    foreach (XmlNode item in doc.SelectNodes("/n:response/n:fields/n:field", nsmg))
                    {
                        ldCodeNm = item.SelectSingleNode("ldCodeNm", nsmg) == null ? "" : item.SelectSingleNode("ldCodeNm", nsmg).InnerText;
                        mnnmSlno = item.SelectSingleNode("mnnmSlno", nsmg) == null ? "" : item.SelectSingleNode("mnnmSlno", nsmg).InnerText;
                        stdrYear = item.SelectSingleNode("stdrYear", nsmg).InnerText;
                        stdrMt = item.SelectSingleNode("stdrMt", nsmg).InnerText;
                        pblntfPclnd = item.SelectSingleNode("pblntfPclnd", nsmg).InnerText;
                        pblntfDe = item.SelectSingleNode("pblntfDe", nsmg) == null ? "" : item.SelectSingleNode("pblntfDe", nsmg).InnerText;
                        lastUpdtDt = item.SelectSingleNode("lastUpdtDt", nsmg).InnerText;

                        var obj = new JObject();
                        obj.Add("stdrYear", stdrYear);
                        obj.Add("pblntfPclnd", pblntfPclnd);
                        obj.Add("stdrMt", stdrMt);
                        obj.Add("pblntfDe", pblntfDe);
                        jaPrice.Add(obj);
                    }
                    src = "0";
                }
                if (jaPrice.Count == 0)
                {
                    PrcLandUsePrice_Error("price", idx);
                    continue;
                }

                jsData = jaPrice.ToString();
                db.Open();
                cvp = "js_data='" + jsData + "', src='" + src + "', wdt=curdate()";
                sql = "insert into ta_ilp set tid=" + tid + ", pnu=" + pnu + ", " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                db.ExeQry(sql);

                sql = "update ta_land set price_prc=1 where idx=" + idx;
                db.ExeQry(sql);
                db.Close();
                mvCnt++;
            }
            atomLog.AddLog(string.Format(" > 수집-{0}", mvCnt));
        }

        /// <summary>
        /// 토지이용계획/개별공시지가 오류처리
        /// </summary>
        /// <param name="dvsn"></param>
        /// <param name="idx"></param>
        private void PrcLandUsePrice_Error(string dvsn, string idx)
        {
            string sql;

            db.Open();
            if (dvsn == "price")
            {
                sql = "update ta_land set price_prc=2 where idx=" + idx;
            }
            else
            {
                sql = "update ta_land set plan_prc=2 where idx=" + idx;
            }
            db.ExeQry(sql);
            db.Close();
        }

        /// <summary>
        /// 파일수집-사진
        /// </summary>
        private void Prc_PhotoFile()
        {
            int i = 0, curCnt = 0, totCnt = 0, photoCnt = 0;
            string sql, tbl, cvp, url, jiwonNm, spt, dpt, sn, sn1, sn2, bidDt, saNo, pn, html, html0, alt, src, dir, ctgr, year, fileNm, locFile, rmtFile, thumb, locThumbFile, rmtThumbFile, seq;
            string photoNo, dtlUrl, photoSrc, photoNote;
            bool photoExist = false;

            dir = filePath + @"\사진";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            progrsView("파일수집-사진");   //진행상태

            sql = "select tid,crt,spt,dpt,sn1,sn2,pn,bid_dt from ta_list where pre_dt=curdate() and auto_prc=2 and pre_prc < 3 group by crt,spt,sn1,sn2 order by spt,dpt,sn1,sn2";
            DataTable dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;

            HAPDoc doc = new HAPDoc();
            List<MySqlParameter> sp = new List<MySqlParameter>();
            Dictionary<string, string> dicHtml = new Dictionary<string, string>();

            foreach (DataRow row in dt.Rows)
            {
                i = 0;
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                photoExist = false;

                progrsView(string.Format("[사진] {0}-{1} -> {2} / {3}", row["sn1"], row["sn2"], curCnt, totCnt), 1);     //진행상태

                spt = row["spt"].ToString();
                sn1 = row["sn1"].ToString();
                sn2 = row["sn2"].ToString();
                year = sn1;
                sn = string.Format("{0}{1}", sn1, sn2.ToString().PadLeft(6, '0'));
                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", spt));
                saNo = string.Format("{0}0130{1}", sn1, sn2.ToString().PadLeft(6, '0'));
                pn = (row["pn"].ToString() == "0") ? "1" : row["pn"].ToString();
                dpt = row["dpt"].ToString();
                bidDt = string.Format("{0:yyyyMMdd}", row["bid_dt"]);

                //물건 사진(B*)-해당 사건이 최초 본물건인 경우만 수집한다.
                sql = "select tid from ta_list where spt=" + spt + " and sn1=" + sn1 + " and sn2=" + sn2 + " and sta1 > 10 and ((2nd_dt > '0000-00-00' and 2nd_dt < curdate()) or (pre_dt > '0000-00-00' and pre_dt < curdate()))";
                db.Open();
                MySqlDataReader dr = db.ExeRdr(sql);
                photoExist = dr.HasRows;
                dr.Close();
                db.Close();
                if (photoExist == false)
                {
                    dtlUrl = "http://www.courtauction.go.kr/RetrieveSaPhotoInfo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&boGbn=B&boCd=B000240&pageSpec=photoPage&targetRow=";
                    html = net.GetHtml(dtlUrl);
                    doc.LoadHtml(html);
                    HtmlNode nd = doc.DocumentNode.SelectSingleNode("//div[@class='page_sum']");
                    if (nd == null) continue;
                    Match match = Regex.Match(nd.InnerText, @"(\d+)건");
                    if (match.Success == false) continue;
                    photoCnt = Convert.ToInt32(match.Groups[1].Value);

                    for (i = 1; i <= photoCnt; i++)
                    {
                        webCnt++;
                        photoSrc = ""; photoNote = "";
                        if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                        if (i > 1)
                        {
                            html = net.GetHtml(dtlUrl + i.ToString());
                        }
                        match = Regex.Match(html, @"<img src=""(/DownFront.*)"" alt=""(.*)""", rxOptM);
                        if (match.Success == false) continue;
                        src = match.Groups[1].Value.Replace("&amp;", "&").Trim();
                        alt = match.Groups[2].Value.Trim();
                        
                        if (alt.Contains("전경도")) ctgr = "BA";
                        else if (alt.Contains("내부구조도")) ctgr = "BB";
                        else if (alt.Contains("위치도")) ctgr = "BC";
                        else if (alt.Contains("개황도")) ctgr = "BD";
                        else if (alt.Contains("관련사진")) ctgr = "BE";
                        else if (alt.Contains("지적도")) ctgr = "BF";
                        else if (alt.Contains("지번약도")) ctgr = "BG";
                        else ctgr = "BZ";  //기타

                        url = "http://www.courtauction.go.kr" + src;
                        locFile = string.Format(@"{0}\{1}-{2}-{3}-{4}.jpg", dir, ctgr, spt, sn, i.ToString().PadLeft(3, '0'));
                        locThumbFile = string.Format(@"{0}\T_{1}-{2}-{3}-{4}.jpg", dir, ctgr, spt, sn, i.ToString().PadLeft(3, '0'));
                        if (File.Exists(locFile)) continue;
                        bool imgRslt = net.DnImg(url, locFile);
                        if (!imgRslt)
                        {
                            //
                            continue;
                        }

                        //사진정보                        
                        match = Regex.Match(html, @"<div class=""\w+"">사진출처\s+:\s+(.*?)</div>", rxOptM);
                        photoSrc = match.Groups[1].Value.Trim().Replace("\\", string.Empty).Replace("'", "\\'");
                        match = Regex.Match(html, @"<td>사진설명\s+:\s+(.*?)</td>", rxOptM);
                        photoNote = match.Groups[1].Value.Trim().Replace("\\", string.Empty).Replace("'", "\\'");

                        //썸네일
                        thumb = PrcSub_Thumb(locFile, locThumbFile);

                        //FTP 업로드
                        if (!File.Exists(locFile))
                        {
                            //
                            continue;
                        }
                        match = Regex.Match(locFile, @"[\w\d\-]*.jpg$", rxOptM);
                        fileNm = match.Value;
                        rmtFile = string.Format("{0}/{1}/{2}/{3}", ctgr, spt, year, fileNm);
                        rmtThumbFile = string.Format("{0}/{1}/{2}/T_{3}", ctgr, spt, year, fileNm);
                        
                        if (ftp1.Upload(locFile, rmtFile))
                        {
                            if (thumb == "Y")
                            {
                                ftp1.Upload(locThumbFile, rmtThumbFile);
                            }
                            //DB 처리
                            tbl = (Convert.ToDecimal(year) > 2004) ? ("ta_f" + year) : "ta_f2004";
                            cvp = "ctgr='" + ctgr + "', spt='" + spt + "', sn='" + sn + "', file='" + fileNm + "', src='" + photoSrc + "', note='" + photoNote + "', wdt=curdate()";
                            sql = "insert into " + tbl + " set " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                            db.Open();
                            db.ExeQry(sql);
                            db.Close();
                        }
                    }
                }

                db.Open();
                sql = "update ta_list set pre_prc=3 where spt='" + spt + "' and sn1='" + sn1 + "' and sn2='" + sn2 + "'";
                db.ExeQry(sql);
                db.Close();
            }
        }

        /// <summary>
        /// 썸네일 생성
        /// </summary>
        /// <param name="fileNm"></param>
        /// <returns></returns>
        private string PrcSub_Thumb(string fullNm, string thumbNm)
        {
            string result;
            
            if (!File.Exists(fullNm) || !Regex.IsMatch(fullNm, @"bmp|gif|jpg|png|tiff"))
            {
                result = "N";
            }
            else
            {
                try
                {
                    Image image = Image.FromFile(fullNm);
                    Image thumb = image.GetThumbnailImage(200, 150, () => false, IntPtr.Zero);
                    thumb.Save(thumbNm);
                    result = "Y";
                }
                catch
                {
                    result = "N";
                }
            }

            return result;
        }

        /// <summary>
        /// 파일 수집-경매 문서
        /// </summary>
        private void Prc_DocsFile()
        {
            string dir;

            dir = filePath + @"\경매문서";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            progrsView("파일수집-경매문서");   //진행상태

            //사건내역
            PrcFileSub_Event(dir, "AA");

            //기일내역
            PrcFileSub_BidDt(dir, "AB");

            //문건송달
            PrcFileSub_Dlvry(dir, "AC");

            //표시목록
            PrcFileSub_ReList(dir, "AE");
        }

        /// <summary>
        /// 파일 수집Sub-사건내역
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="v"></param>
        private void PrcFileSub_Event(string dir, string ctgr)
        {
            string tid, sql, cvp, cdtn, url, jiwonNm, saNo, html, locFile, rmtFile, spt, year, sn, fileNm, tbl;
            decimal totCnt = 0, curCnt = 0, dlCnt = 0, ulCnt = 0, dnFailCnt = 0;
            string stripTag = @"[</]+(a|img).*?>";
            HAPDoc doc = new HAPDoc();

            cdtn = "sta1=11 and pre_dt=curdate()";
            sql = "select tid, spt, dpt, sn1, sn2, pn, bid_dt from ta_list where " + cdtn + " order by tid";
            DataTable dt = db.ExeDt(sql);

            totCnt = dt.Rows.Count;
            atomLog.AddLog(string.Format(" > 사건내역 수집시작 대상-{0}", totCnt));     //로그기록

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                tid = row["tid"].ToString();
                progrsView(string.Format("[사건내역] TID -> {0} ^ {1} / {2}", tid, curCnt, totCnt), 1);  //진행상태

                locFile = string.Format(@"{0}\{1}-{2}-{3}{4}.html", dir, ctgr, row["spt"], row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                if (File.Exists(locFile)) continue;

                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                url = "http://www.courtauction.go.kr/RetrieveRealEstDetailInqSaList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
                html = net.GetHtml(url);
                doc.LoadHtml(html);
                HtmlNodeCollection nc = doc.DocumentNode.SelectNodes("//div[@class='table_title']|//table[@class='Ltbl_dt' or @class='Ltbl_list']");
                if (nc != null)
                {
                    var nodeList = new List<string>(nc.Select(node => node.OuterHtml));
                    if (nodeList.Count > 0)
                    {
                        string A1 = string.Join("\r\n", nodeList.ToArray());
                        A1 = Regex.Replace(A1, stripTag, string.Empty, rxOptS);
                        A1 = Regex.Replace(A1, @"^[\r\n\s]+", string.Empty, rxOptM);
                        File.WriteAllText(locFile, A1);
                        dlCnt++;
                    }
                    else
                    {
                        dnFailCnt++;
                        continue;
                    }
                }
                else
                {
                    dnFailCnt++;
                }

                //FTP 업로드
                if (!File.Exists(locFile))
                {
                    //
                    continue;
                }
                Match match = Regex.Match(locFile, @"\w{2}\-(\d{4})\-(\d{4})(\d{6}).*", rxOptM);
                if (match.Success == false)
                {
                    //
                    continue;
                }
                spt = match.Groups[1].Value;
                year = match.Groups[2].Value;
                sn = string.Format("{0}{1}", match.Groups[2].Value, match.Groups[3].Value);
                fileNm = match.Value;
                rmtFile = string.Format("{0}/{1}/{2}/{3}", ctgr, spt, year, fileNm);
                
                if (ftp1.Upload(locFile, rmtFile))
                {
                    //DB 처리
                    tbl = (Convert.ToDecimal(year) > 2004) ? ("ta_f" + year) : "ta_f2004";
                    cvp = "ctgr='" + ctgr + "', spt='" + spt + "', sn='" + sn + "', file='" + fileNm + "', wdt=curdate()";
                    sql = "insert into " + tbl + " set " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                    PrcFileSub_DB(sql);
                }
            }
        }

        /// <summary>
        /// 파일 수집Sub-기일내역
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="v"></param>
        private void PrcFileSub_BidDt(string dir, string ctgr)
        {
            string tid, sql, cvp, cdtn, url, jiwonNm, saNo, html, locFile, rmtFile, spt, year, sn, fileNm, tbl;
            decimal totCnt = 0, curCnt = 0, dlCnt = 0, ulCnt = 0, dnFailCnt = 0;
            string stripTag = @"[</]+(a|img).*?>";
            HAPDoc doc = new HAPDoc();

            cdtn = "sta1=11 and pre_dt=curdate()";
            sql = "select tid, spt, dpt, sn1, sn2, pn, bid_dt from ta_list where " + cdtn + " order by tid";
            DataTable dt = db.ExeDt(sql);

            totCnt = dt.Rows.Count;
            atomLog.AddLog(string.Format(" > 기일내역 수집시작 대상-{0}", totCnt));     //로그기록

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                tid = row["tid"].ToString();
                progrsView(string.Format("[기일내역] TID -> {0} ^ {1} / {2}", tid, curCnt, totCnt), 1);  //진행상태

                locFile = string.Format(@"{0}\{1}-{2}-{3}{4}.html", dir, ctgr, row["spt"], row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                if (File.Exists(locFile)) continue;

                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                url = "http://www.courtauction.go.kr/RetrieveRealEstSaDetailInqGiilList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
                html = net.GetHtml(url);
                doc.LoadHtml(html);
                HtmlNodeCollection nc = doc.DocumentNode.SelectNodes("//div[@class='table_title']|//table[@class='Ltbl_dt' or @class='Ltbl_list']");
                if (nc != null)
                {
                    var nodeList = new List<string>(nc.Select(node => node.OuterHtml));
                    if (nodeList.Count > 0)
                    {
                        string A1 = string.Join("\r\n", nodeList.ToArray());
                        A1 = Regex.Replace(A1, stripTag, string.Empty, rxOptS);
                        A1 = Regex.Replace(A1, @"^[\r\n\s]+", string.Empty, rxOptM);
                        File.WriteAllText(locFile, A1);
                        dlCnt++;
                    }
                    else
                    {
                        dnFailCnt++;
                        continue;
                    }
                }
                else
                {
                    dnFailCnt++;
                }

                //FTP 업로드
                if (!File.Exists(locFile))
                {
                    //
                    continue;
                }
                Match match = Regex.Match(locFile, @"\w{2}\-(\d{4})\-(\d{4})(\d{6}).*", rxOptM);
                if (match.Success == false)
                {
                    //
                    continue;
                }
                spt = match.Groups[1].Value;
                year = match.Groups[2].Value;
                sn = string.Format("{0}{1}", match.Groups[2].Value, match.Groups[3].Value);
                fileNm = match.Value;
                rmtFile = string.Format("{0}/{1}/{2}/{3}", ctgr, spt, year, fileNm);
                
                if (ftp1.Upload(locFile, rmtFile))
                {
                    //DB 처리
                    tbl = (Convert.ToDecimal(year) > 2004) ? ("ta_f" + year) : "ta_f2004";
                    cvp = "ctgr='" + ctgr + "', spt='" + spt + "', sn='" + sn + "', file='" + fileNm + "', wdt=curdate()";
                    sql = "insert into " + tbl + " set " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                    PrcFileSub_DB(sql);
                }
            }
        }

        /// <summary>
        /// 파일 수집Sub-문건송달
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="v"></param>
        private void PrcFileSub_Dlvry(string dir, string ctgr)
        {
            string tid, sql, cvp, cdtn, url, jiwonNm, saNo, html, locFile, rmtFile, spt, year, sn, fileNm, tbl;
            decimal totCnt = 0, curCnt = 0, dlCnt = 0, ulCnt = 0, dnFailCnt = 0;
            string stripTag = @"[</]+(a|img).*?>";
            HAPDoc doc = new HAPDoc();

            cdtn = "sta1=11 and pre_dt=curdate()";
            sql = "select tid, spt, dpt, sn1, sn2, pn, bid_dt from ta_list where " + cdtn + " order by tid";
            DataTable dt = db.ExeDt(sql);

            totCnt = dt.Rows.Count;
            atomLog.AddLog(string.Format(" > 문건/송달내역 수집시작 대상-{0}", totCnt));

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                tid = row["tid"].ToString();
                progrsView(string.Format("[문건송달] TID -> {0} ^ {1} / {2}", tid, curCnt, totCnt), 1);  //진행상태

                locFile = string.Format(@"{0}\{1}-{2}-{3}{4}.html", dir, ctgr, row["spt"], row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                if (File.Exists(locFile)) continue;

                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                url = "http://www.courtauction.go.kr/RetrieveRealEstSaDetailInqMungunSongdalList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
                html = net.GetHtml(url);
                doc.LoadHtml(html);
                HtmlNodeCollection nc = doc.DocumentNode.SelectNodes("//div[@class='table_title']|//table[@class='Ltbl_dt' or @class='Ltbl_list']");
                if (nc != null)
                {
                    var nodeList = new List<string>(nc.Select(node => node.OuterHtml));
                    if (nodeList.Count > 0)
                    {
                        string A1 = string.Join("\r\n", nodeList.ToArray());
                        A1 = Regex.Replace(A1, stripTag, string.Empty, rxOptS);
                        A1 = Regex.Replace(A1, @"^[\r\n\s]+", string.Empty, rxOptM);
                        File.WriteAllText(locFile, A1);

                        dlCnt++;
                    }
                    else
                    {
                        dnFailCnt++;
                        continue;
                    }
                }
                else
                {
                    dnFailCnt++;
                }

                //FTP 업로드
                if (!File.Exists(locFile))
                {
                    //
                    continue;
                }
                Match match = Regex.Match(locFile, @"\w{2}\-(\d{4})\-(\d{4})(\d{6}).*", rxOptM);
                if (match.Success == false)
                {
                    //
                    continue;
                }
                spt = match.Groups[1].Value;
                year = match.Groups[2].Value;
                sn = string.Format("{0}{1}", match.Groups[2].Value, match.Groups[3].Value);
                fileNm = match.Value;
                rmtFile = string.Format("{0}/{1}/{2}/{3}", ctgr, spt, year, fileNm);
                
                if (ftp1.Upload(locFile, rmtFile))
                {
                    //DB 처리
                    tbl = (Convert.ToDecimal(year) > 2004) ? ("ta_f" + year) : "ta_f2004";
                    cvp = "ctgr='" + ctgr + "', spt='" + spt + "', sn='" + sn + "', file='" + fileNm + "', wdt=curdate()";
                    sql = "insert into " + tbl + " set " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                    PrcFileSub_DB(sql);
                }
            }

            //
            //중복병합사건 판별-미처리
            //
        }
        
        /// <summary>
        /// 파일 수집Sub-표시목록
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="v"></param>
        private void PrcFileSub_ReList(string dir, string ctgr)
        {
            string tid, sql, cvp, cdtn, url, jiwonNm, saNo, html0, html, locFile, seq, rmtFile, spt, year, sn, fileNm, tbl;
            decimal totCnt = 0, curCnt = 0, dlCnt = 0, ulCnt = 0, dnFailCnt = 0;
            string stripTag = @"[</]+(a|img).*?>";
            HAPDoc doc = new HAPDoc();
            Dictionary<string, string> dicHtml = new Dictionary<string, string>();

            cdtn = "sta1=11 and pre_dt=curdate() and (cat1 IN (10,20) or cat2=3012)";  //현황조사서는 토지, 건물, 선박만 제공
            sql = "select tid, spt, dpt, sn1, sn2, pn, bid_dt from ta_list where " + cdtn + " group by spt, sn1, sn2 order by tid";
            DataTable dt = db.ExeDt(sql);

            totCnt = dt.Rows.Count;
            atomLog.AddLog(string.Format(" > 부동산표시목록 수집시작 대상-{0}", totCnt));

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                dicHtml.Clear();
                tid = row["tid"].ToString();
                progrsView(string.Format("[표시목록] TID -> {0} ^ {1} / {2}", tid, curCnt, totCnt), 1);  //진행상태

                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                url = "http://www.courtauction.go.kr/RetrieveRealEstHjosaDispMokrok.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo;
                html0 = net.GetHtml(url);
                if (html0.Contains("잘못된 접근입니다") || html0.Contains("현황조사서가 없습니다"))
                {
                    dnFailCnt++;
                    continue;
                }

                //명령 회차 판별
                doc.LoadHtml(html0);
                if (doc.GetElementbyId("idOrdHoi") == null) continue;
                HtmlNodeCollection ncOrd = doc.GetElementbyId("idOrdHoi").SelectNodes("./option");
                if (ncOrd.Count == 0) continue;
                foreach (HtmlNode nd in ncOrd)
                {
                    seq = nd.GetAttributeValue("value", "").Trim();
                    if (nd.GetAttributeValue("selected", "").Trim() == "selected")
                    {
                        dicHtml.Add(seq, html0);
                    }
                    else
                    {
                        webCnt++;
                        if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                        url = "http://www.courtauction.go.kr/RetrieveRealEstHjosaDispMokrok.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&ordHoi=" + seq;
                        html = net.GetHtml(url);
                        if (html.Contains("잘못된 접근입니다") || html.Contains("현황조사서가 없습니다"))
                        {
                            dnFailCnt++;
                            continue;
                        }
                        else
                        {
                            dicHtml.Add(seq, html);
                        }
                    }
                }

                foreach (KeyValuePair<string, string> kvp in dicHtml)
                {
                    doc.LoadHtml(kvp.Value);
                    locFile = string.Format(@"{0}\{1}-{2}-{3}{4}-{5}.html", dir, ctgr, row["spt"], row["sn1"], row["sn2"].ToString().PadLeft(6, '0'), kvp.Key.PadLeft(2, '0'));
                    if (File.Exists(locFile)) continue;

                    HtmlNodeCollection nc = doc.DocumentNode.SelectNodes("//div[@class='table_title' or @class='tbl_txt']|//table[@class='Ltbl_dt' or @class='Ltbl_list']");
                    if (nc != null)
                    {
                        List<int> rmNode = new List<int>();
                        foreach (HtmlNode nd in nc)
                        {
                            if (nd.GetAttributeValue("summary", "") == "현황조사서 기본내역 표")
                            {
                                rmNode.Add(nc.IndexOf(nd));
                            }
                        }
                        rmNode.Reverse();
                        foreach (int ndIdx in rmNode)
                        {
                            nc.RemoveAt(ndIdx);
                        }
                        var nodeList = new List<string>(nc.Select(node => node.OuterHtml));
                        if (nodeList.Count > 0)
                        {
                            string A1 = string.Join("\r\n", nodeList.ToArray());
                            A1 = Regex.Replace(A1, stripTag, string.Empty, rxOptS);
                            A1 = Regex.Replace(A1, @"^[\r\n\s]+", string.Empty, rxOptM);
                            File.WriteAllText(locFile, A1);
                            dlCnt++;
                        }
                        else
                        {
                            dnFailCnt++;
                            continue;
                        }
                    }
                    else
                    {
                        dnFailCnt++;
                    }

                    //FTP 업로드
                    if (!File.Exists(locFile))
                    {
                        //
                        continue;
                    }
                    Match match = Regex.Match(locFile, @"\w{2}\-(\d{4})\-(\d{4})(\d{6}).*", rxOptM);
                    if (match.Success == false)
                    {
                        //
                        continue;
                    }
                    spt = match.Groups[1].Value;
                    year = match.Groups[2].Value;
                    sn = string.Format("{0}{1}", match.Groups[2].Value, match.Groups[3].Value);
                    fileNm = match.Value;
                    rmtFile = string.Format("{0}/{1}/{2}/{3}", ctgr, spt, year, fileNm);
                    
                    if (ftp1.Upload(locFile, rmtFile))
                    {
                        //DB 처리
                        tbl = (Convert.ToDecimal(year) > 2004) ? ("ta_f" + year) : "ta_f2004";
                        cvp = "ctgr='" + ctgr + "', spt='" + spt + "', sn='" + sn + "', file='" + fileNm + "', wdt=curdate()";
                        sql = "insert into " + tbl + " set " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                        PrcFileSub_DB(sql);
                    }
                }

                //
                //중복병합사건 판별-미처리
                //
            }
        }
                
        /// <summary>
        /// 파일 수집Sub-DB 처리
        /// </summary>
        /// <param name="sql"></param>
        private void PrcFileSub_DB(string sql)
        {
            db.Open();
            db.ExeQry(sql);
            db.Close();
        }

        /// <summary>
        /// 날짜 형식 변환
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private string getDateParse(string str, string cellNm = null)
        {
            string dt = string.Empty;

            str = str.Replace(" ", string.Empty).Trim();

            Match m = Regex.Match(str, @"(\d{4})[.년/\-](\d+)[.월/\-](\d+)[.일]*", rxOptM);
            if (m.Success)
            {
                dt = string.Format("{0}-{1}-{2}", m.Groups[1].Value, m.Groups[2].Value.PadLeft(2, '0'), m.Groups[3].Value.PadLeft(2, '0'));
            }
            else
            {
                if (str.Length == 8)
                {
                    dt = string.Format("{0}-{1}-{2}", str.Substring(0, 4), str.Substring(4, 2), str.Substring(6, 2));
                }
                else if (str.Length == 6)
                {
                    dt = string.Format("20{0}-{1}-{2}", str.Substring(0, 2), str.Substring(2, 2), str.Substring(4, 2));
                }
            }

            if (!string.IsNullOrEmpty(cellNm))
            {
                if (str == "1") dt = "0000-00-01";
                else if (str == "3") dt = "0000-00-03";
            }

            return dt;
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            bgwork.Dispose();

            this.Dispose();
            this.Close();
        }
    }
}
