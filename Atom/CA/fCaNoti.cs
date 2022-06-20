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
using mshtml;
using System.Collections;
using System.Xml;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Diagnostics;

namespace Atom.CA
{
    public partial class fCaNoti : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AuctCd auctCd = new AuctCd();
        ApiUtil api = new ApiUtil();
        SpCdtnChk spCdtnChk = new SpCdtnChk();
        AtomLog atomLog = new AtomLog(100);     //로그 생성

        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 1500, webCnt = 0;

        int udtCnt = 0, newCnt = 0;     //금일 신규 물건수(신건, 본물건 전환)

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        DataTable dtDptCd, dtCatCd, dtStateCd, dtFlrCd, dtLeasUseCd;         //법원계, 물건종별, 진행상태, 건물층, 임차인-용도코드
        DataTable dtCarCoCd, dtCarMoCd, dtCarFuel, dtCarTrans;  //차량-제조사, 모델그룹, 사용연료, 변속기형식
        DataTable dtSpcCd;      //특수 조건
        DataTable dtEtcCd;      //기타 모든 코드

        string filePath;    //로컬 파일저장 경로

        //토지 패턴
        string landPtrn = "대|전|답|과수원|목장용지|임야|광천지|염전|대지|공장용지|학교용지|주차장|주유소용지|창고용지|도로|철도용지|제방|하천|구거|유지|양어장|수도용지|공원|체육용지|유원지|종교용지|사적지|묘지|잡종지";

        //집합 건물 카테고리(cat3)
        private decimal[] multiBldgArr;

        //숨김 물건종별 카테고리(cat3)
        private readonly decimal[] hideCatArr = new decimal[] { 201012, 201017, 201019, 201115, 201119, 201120, 201124, 201125, 201126, 201127, 201129 };

        FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "CA/", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);

        public fCaNoti()
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
            dtDptCd = db.ExeDt("select * from ta_cd_dpt");

            //물건종별 코드
            dtCatCd = db.ExeDt("select cat1_cd, cat2_cd, cat3_cd, cat3_nm, hide from ta_cd_cat where cat3_cd > 0");

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

            //파일저장 디렉토리 생성
            filePath = @"C:\Atom\CA\" + DateTime.Today.ToShortDateString();
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }
        }

        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            //선행공고의 본물건 등록일(2nd_dt)을 오늘자로 업데이트
            atomLog.AddLog("▼ 선행공고 본물건 등록일 업데이트");
            Prc_Pre2ndDt(); //-> 추가

            //법원별 공고 및 물건 기본정보(매각공고->물건목록->사건내역)
            atomLog.AddLog("▼ 공고확인 및 물건 기본정보");
            Prc_Nt();

            //물건번호별 상세정보
            atomLog.AddLog("▼ 물건번호별 상세정보");
            Prc_Dtl();

            //현황조사서(임대차 관계)
            atomLog.AddLog("▼ 현황조사서(임대차 관계)");
            Prc_StatIvst();

            //등기 자동발급 대상 추가
            atomLog.AddLog("▼ 등기 자동발급 대상");
            Prc_RgstIssueAdd();

            //역세권 매칭
            atomLog.AddLog("▼ 역세권");
            Prc_Station();

            //집합건물(아파트)코드 매칭
            atomLog.AddLog("▼ 집합건물코드");
            Prc_AptCd();
            
            //파일-물건사진, 감정평가서
            atomLog.AddLog("▼ 파일수집(사진,감평)");
            Prc_PhotoFile();

            //사용승인일자
            atomLog.AddLog("▼ 사용승인일자");
            Prc_AprvDt();

            //토지이용계획(용도지역/지구)
            atomLog.AddLog("▼ 토지이용계획");
            Prc_LandUse();

            //개별공시지가
            atomLog.AddLog("▼ 개별공시지가");
            Prc_LandPrice();

            //파일-문서(사건내역, 기일내역, 문건송달, 현황조사, 표시목록, 물건상세)
            atomLog.AddLog("▼ 파일수집(문서)");
            Prc_DocsFile();
            
            //감정평가서 OCR 연동
            atomLog.AddLog("▼ 감정평가서 OCR");
            Prc_ApslOcr();
           
            //Prc_ApslOcrTest();
            
            atomLog.AddLog("실행 완료", 1);
        }

        /// <summary>
        /// 선행공고 본물건 등록일(2nd_dt) 업데이트
        /// </summary>
        private void Prc_Pre2ndDt()
        {
            string sql, targetDt;
            int rowCnt = 0;

            progrsView("선행공고 본물건 등록일 업데이트");   //진행상태

            targetDt = DateTime.Now.AddDays(14).ToShortDateString();

            sql = "select count(*) as cnt from ta_list where bid_dt='" + targetDt + "' and 2nd_dt='0000-00-00' and pre_dt > '0000-00-00'";
            db.Open();
            rowCnt = Convert.ToInt32(db.RowCnt(sql));
            db.Close();

            db.Open();
            sql = "update ta_list set 2nd_dt=curdate() where bid_dt='" + targetDt + "' and 2nd_dt='0000-00-00' and pre_dt > '0000-00-00'";
            db.ExeQry(sql);
            db.Close();
                        
            atomLog.AddLog(string.Format("▶▶▶ 선행공고 등록일 업데이트-{0}건", rowCnt));   //로그기록
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
        /// 각 법원별 신규공고 확인
        /// </summary>
        private void Prc_Nt()
        {
            string jiwonNm = "", url = "", html = "", curMnth = "", nxtMnth = "", date = "", targetDt = "", sql = "", cvp = "";
            string saNo = "", pdNo = "", apslAmt = "", minbAmt = "", crt = "", spt = "", sn1 = "", sn2 = "";
            string caNm = "", bidDt = "", dptCd = "", dptNm = "", bidTm1 = "", bidTm2 = "", bidTm3 = "";
            int tdCnt = 0, bidCnt = 0;

            targetDt = DateTime.Now.AddDays(14).ToShortDateString().Replace("-", string.Empty);

            DataTable dtLaw = auctCd.DtLawInfo();
            List<string> mnthList = new List<string>();
            curMnth = DateTime.Now.ToShortDateString().Substring(0, 7).Replace("-", string.Empty);
            nxtMnth = targetDt.Substring(0, 6);
            mnthList.Add(curMnth);
            if (curMnth != nxtMnth) mnthList.Add(nxtMnth);

            HAPDoc doc = new HAPDoc();

            //법원-공고일정(캘린더)
            DataTable dtCal = new DataTable();
            dtCal.Columns.Add("csCd");
            dtCal.Columns.Add("lawNM");
            dtCal.Columns.Add("bidDt");
            dtCal.Columns.Add("dptCd");
            dtCal.Columns.Add("dptNm");

            progrsView("공고확인 및 물건 기본정보");   //진행상태

            foreach (DataRow row in dtLaw.Rows)
            {
                progrsView(string.Format("[{0}]", row["lawNm"]), 1);    //진행상태

                jiwonNm = auctCd.LawNmEnc(row["lawNm"]);
                
                foreach (string ym in mnthList)
                {
                    webCnt++;
                    if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                    date = ym.Substring(0, 4) + "." + ym.Substring(4);
                    url = "http://www.courtauction.go.kr/RetrieveRealEstMgakNotifySrch.laf?date=" + date + "&inqYear=&inqMnth=&inqYearMnth=" + ym + "&srnID=PNO101001&ipchalGbnCd=000331&jiwonNm=" + jiwonNm;
                    html = net.GetHtml(url);                    
                    //
                    //alert("해당 IP는 비정상적인 접속으로 보안정책에 의하여 차단되었습니다.");
                    //---> 발견시 모든 작업 Break -> SMS
                    //
                    doc.LoadHtml(html);
                    HtmlNodeCollection ncDiv = doc.DocumentNode.SelectNodes("//div[contains(@class,'cal_schedule')]");
                    if (ncDiv == null) continue;

                    foreach (HtmlNode div in ncDiv)
                    {
                        bidTm1 = ""; bidTm2 = ""; bidTm3 = "";
                        string clickStr = div.SelectSingleNode("./a").GetAttributeValue("onclick", "null");
                        
                        MatchCollection mc = Regex.Matches(clickStr, @"'(.*?)'", rxOptM);
                        caNm = mc[1].Groups[1].Value.Trim();
                        bidDt = mc[2].Groups[1].Value.Trim();
                        dptCd = mc[5].Groups[1].Value.Trim();
                        dptNm = mc[6].Groups[1].Value.Trim();
                        bidTm1 = mc[9].Groups[1].Value.Trim();
                        if (bidTm1 != string.Empty) bidTm1 += "00";
                        bidTm2 = mc[10].Groups[1].Value.Trim();
                        if (bidTm2 != string.Empty) bidTm2 += "00";
                        bidTm3 = mc[11].Groups[1].Value.Trim();
                        if (bidTm3 != string.Empty) bidTm3 += "00";

                        if (bidTm3 != string.Empty) bidCnt = 3;
                        else if (bidTm2 != string.Empty) bidCnt = 2;
                        else bidCnt = 1;
                        if (bidDt != targetDt) continue;

                        //경매일정 추가
                        cvp = "spt=" + row["csCd"].ToString() + ", dpt=" + dptCd + ", bid_dt='" + bidDt + "', bid_cnt=" + bidCnt.ToString() + ", bid_tm1='" + bidTm1 + "', bid_tm2='" + bidTm2 + "', bid_tm3='" + bidTm3 + "', wdt=curdate()";
                        sql = "insert into ta_skd set " + cvp + " ON DUPLICATE KEY update " + cvp;
                        db.Open();
                        db.ExeQry(sql);
                        db.Close();

                        //신설 경매계 추가 및 관리자에게 알림
                        if (dtDptCd.Select("ca_nm='" + caNm + "' and dpt_cd='" + dptCd + "'").Count() == 0)
                        {
                            atomLog.AddLog(string.Format("경매계 신설됨 {0} {1} 신설(코드-{2})", caNm, dptNm, dptCd));   //로그기록
                            db.Open();
                            sql = "insert ignore into ta_cd_dpt set ca_nm='" + caNm + "', ca_cd='" + row["lawCd"].ToString() + "', cs_cd='" + row["csCd"].ToString() + "', dpt_cd='" + dptCd + "', dpt_nm='" + dptNm.Replace("경매", string.Empty) + "', wdt=curdate()";
                            db.ExeQry(sql);
                            db.Close();
                        }
                        dtCal.Rows.Add(row["csCd"].ToString(), caNm, bidDt, dptCd, dptNm);
                        progrsView(dptNm + " 발견", 2);     //진행상태
                        atomLog.AddLog(caNm + " " + dptNm); //로그기록
                    }
                }
            }

            //해당 공고 사건 목록(사건번호, 물건번호, 감정가, 최저가)
            DataTable dtSa = new DataTable();
            dtSa.Columns.Add("saNo");
            dtSa.Columns.Add("pdNo");
            dtSa.Columns.Add("apslAmt");
            dtSa.Columns.Add("minbAmt");

            progrsView("법원별 사건 기본정보 처리", 1);    //진행상태
            atomLog.AddLog(string.Format("▶▶▶ 신규 공고-{0}건", dtCal.Rows.Count));   //로그기록

            foreach (DataRow row in dtCal.Rows)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                dtSa.Rows.Clear();

                progrsView(string.Format("{0}/{1}/{2}", row["lawNm"], row["bidDt"], row["dptNm"]), 2);  //진행상태

                jiwonNm = auctCd.LawNmEnc(row["lawNm"]);
                url = "http://www.courtauction.go.kr/RetrieveRealEstMgakNotifySrchGyulgwa.laf?ipchalGbnCd=000331&jiwonNm=" + jiwonNm + "&maeGiil=" + row["bidDt"].ToString() + "&jpDeptCd=" + row["dptCd"].ToString();
                html = net.GetHtml(url);
                doc.LoadHtml(html);

                HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='원공고내역 표']/tbody/tr");
                db.Open();
                foreach (HtmlNode tr in ncTr)
                {
                    tdCnt = tr.SelectNodes("./td").Count;
                    if (tdCnt == 1) continue;
                    if (tdCnt == 6)
                    {
                        saNo = tr.SelectNodes("./td")[0].SelectSingleNode("./a/b").InnerText;
                        pdNo = tr.SelectNodes("./td")[1].InnerText;
                        apslAmt = tr.SelectNodes("./td")[4].SelectNodes("./div")[0].InnerText;
                        minbAmt = tr.SelectNodes("./td")[4].SelectNodes("./div")[1].InnerText;
                    }
                    if (tdCnt == 5)
                    {
                        pdNo = tr.SelectNodes("./td")[0].InnerText;
                        apslAmt = tr.SelectNodes("./td")[3].SelectNodes("./div")[0].InnerText;
                        minbAmt = tr.SelectNodes("./td")[3].SelectNodes("./div")[1].InnerText;
                    }
                    apslAmt = apslAmt.Replace(",", string.Empty);
                    Match match = Regex.Match(minbAmt, @"(\d{1,3}(,\d{3})+)", rxOptM);
                    minbAmt = match.Groups[1].Value.Replace(",", string.Empty);

                    match = Regex.Match(saNo, @"(\d+)타경(\d+)", RegexOptions.Multiline);
                    sn1 = match.Groups[1].Value;
                    sn2 = match.Groups[2].Value;

                    crt = row["csCd"].ToString().Substring(0, 2);
                    spt = row["csCd"].ToString();
                    if (pdNo == "1")
                        sql = "select tid, pn, bid_dt from ta_list where spt=" + spt + " and sn1=" + sn1 + " and sn2=" + sn2 + " and sta1 > 10 and pn in (0,1) limit 1";
                    else
                        sql = "select tid, pn, bid_dt from ta_list where spt=" + spt + " and sn1=" + sn1 + " and sn2=" + sn2 + " and pn=" + pdNo + " and sta1 > 10 limit 1";
                    if (db.ExistRow(sql)) continue;
                    dtSa.Rows.Add(saNo, pdNo, apslAmt, minbAmt);
                }
                db.Close();

                if (dtSa.Rows.Count > 0)
                {
                    Prc_Lst(row, dtSa);
                }
            }

            atomLog.AddLog(string.Format("▶▶▶ 신규 등록-{0}, 본물건 전환-{1}, 합계-{2}건", newCnt, udtCnt, (newCnt + udtCnt)));
        }

        /// <summary>
        /// 입찰일/법원별 사건목록 및 기본정보 수집
        /// </summary>
        /// <param name="lawRow">법원/입찰일 Row</param>
        /// <param name="dtSa">사건목록 DataTable</param>
        private void Prc_Lst(DataRow lawRow, DataTable dtSa)
        {
            string jiwonNm = "", url = "", html = "", saNo = "", sn1 = "", sn2 = "", fileNm = "";
            string auctNm = "", rcptDt = "", iniDt = "", billAmt = "", appeal = "", endRslt = "", endDt = "", sta1 = "", sta2 = "", auctType = "", frmlType = "";
            string sql = "", cvp = "", lsNo = "", adrs = "", adrsType, regnAdrs, mt, pin = "", sidoCd = "", gugunCd = "", dongCd = "", riCd = "", x = "", y = "";
            string dbMode = "", crt = "", spt = "", dpt = "", bidDt = "", shrDt = "", tid = "", pdNote = "", dpstRate = "", creditor = "", debtor = "", owner = "";
            int rowIdx = 0, lotCnt = 0, hoCnt = 0, creditorCnt = 0, debtorCnt = 0, ownerCnt = 0;
            bool eqFlag = false;

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

            jiwonNm = auctCd.LawNmEnc(lawRow["lawNm"]);
            crt = lawRow["csCd"].ToString().Substring(0, 2);
            spt = lawRow["csCd"].ToString();
            dpt = lawRow["dptCd"].ToString();
            bidDt = lawRow["bidDt"].ToString();

            foreach (DataRow row in dtSa.Rows)
            {
                rowIdx = dtSa.Rows.IndexOf(row);
                eqFlag = false;

                dtLs.Rows.Clear();
                dtPrsn.Rows.Clear();
                dtRCase.Rows.Clear();
                dictShr.Clear();
                pdNote = ""; creditor = ""; debtor = ""; owner = ""; auctNm = "";

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
                if (ncTbl == null || doc.DocumentNode.InnerText.Contains("등록된 물건내역이 없습니다"))
                {
                    //해당 사건이 종결/정지/중복/병합 기타 사유로 물건내역이 존재하지 않으므로 목록내역에서 취한다.
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
                }
                else
                {
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
                                dtLs.Rows.Add(dr);
                            }
                            if (ncTd[0].Name == "th" && colName.Contains("물건비고"))
                            {
                                pdNote = ncTd[1].InnerText.Trim();
                            }
                        }
                    }
                }

                if (ncTbl == null || (ncTbl.Count == 1 && row["pdNo"].ToString() == "1")) row["pdNo"] = 0;  //물건내역표가 없을 경우 또는 물건번호 1번의 단일 물건일 경우 물건번호를 0 으로 한다.

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

                db.Open();
                sql = "select tid from ta_list where spt=" + spt + " and sn1=" + sn1 + " and sn2=" + sn2 + " and sta1=10 limit 1";
                MySqlDataReader mdr = db.ExeRdr(sql);

                cvp = "crt=@crt, spt=@spt, dpt=@dpt, sn1=@sn1, sn2=@sn2, pn=@pn, apsl_amt=@apsl_amt, minb_amt=@minb_amt, rcp_dt=@rcp_dt, ini_dt=@ini_dt, shr_dt=@shr_dt, end_dt=@end_dt, bid_dt=@bid_dt, " +
                    "creditor=@creditor, debtor=@debtor, owner=@owner, dpst_type=@dpst_type, dpst_rate=@dpst_rate, auct_type=@auct_type, frml_type=@frml_type, " +
                    "adrs=@adrs, adrs_type=@adrs_type, regn_adrs=@regn_adrs, mt=@mt, m_adrs_no=@m_adrs_no, s_adrs_no=@s_adrs_no, road_adrs=@road_adrs, m_bldg_no=@m_bldg_no, s_bldg_no=@s_bldg_no, bldg_nm=@bldg_nm, road_nm=@road_nm, " +
                    "ls_no=@ls_no, si_cd=@si_cd, gu_cd=@gu_cd, dn_cd=@dn_cd, ri_cd=@ri_cd, x=@x, y=@y, sta1=@sta1, sta2=@sta2, sp_cdtn=@sp_cdtn";
                if (mdr.HasRows)
                {
                    dbMode = "Update";
                    mdr.Read();
                    tid = mdr["tid"].ToString();
                    sql = "update ta_list set " + cvp + ", 2nd_dt=CURDATE(), auto_prc=1 where tid='" + tid + "'";
                    udtCnt++;
                }
                else
                {
                    dbMode = "Insert";
                    sql = "insert into ta_list set " + cvp + ", 1st_dt=CURDATE(), 2nd_dt=CURDATE(), auto_prc=1";
                    newCnt++;
                }
                mdr.Close();

                sp.Add(new MySqlParameter("@crt", crt));
                sp.Add(new MySqlParameter("@spt", spt));
                sp.Add(new MySqlParameter("@dpt", dpt));
                sp.Add(new MySqlParameter("@sn1", sn1));
                sp.Add(new MySqlParameter("@sn2", sn2));
                sp.Add(new MySqlParameter("@pn", row["pdNo"]));
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
                db.Close();

                //sms 발송대상 물건 저장
                if (dbMode == "Update")
                {
                    sql = "insert ignore into db_tank.tx_sms set tid='" + tid + "', state='신건', wdt=curdate(), wtm=curtime()";
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();
                }
            }
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
        /// [물건번호]별 상세수집
        /// - 선행공고 통합(주변환경 및 차량/중기현황 업데이트)
        /// </summary>
        private void Prc_Dtl()
        {
            int totCnt = 0, curCnt = 0;
            string sql = "", cvp = "", url = "", jiwonNm = "", saNo = "", pn = "", tid = "", html = "", catKind = "", catStr = "", cat0 = "", cat1 = "", cat2 = "", cat3, listCatStr = "", pdNote = "", dpslDvsn = "", spRgst = "", frmlType = "";
            string bidCnt, bidDt, preDt, bidTm1, bidTm2, bidTm3, minbAmt1, minbAmt2;
            string loca = "", tfc = "", landShp = "", adjRoad = "", useSta = "", diff = "", faci = "";
            bool landBldgFlag, preNtFlag;

            progrsView("물건번호별 상세정보");   //진행상태

            sql = "select tid, crt, spt, sn1, sn2, pn, bid_dt, pre_dt, minb_amt, frml_type from ta_list where 2nd_dt=curdate() and ((pre_dt='0000-00-00' and auto_prc=1) or (pre_dt > '0000-00-00' and auto_prc=2)) order by tid";
            DataTable dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;

            HAPDoc doc = new HAPDoc();
            List<MySqlParameter> sp = new List<MySqlParameter>();
            List<string> listCat = new List<string>();
            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                progrsView(string.Format("[물건상세] {0}-{1} -> {2} / {3}", row["sn1"], row["sn2"], curCnt, totCnt), 1);  //진행상태

                listCat.Clear();
                catKind = ""; cat0 = ""; cat1 = ""; cat2 = ""; cat3 = ""; pdNote = "";
                loca = ""; tfc = ""; landShp = ""; adjRoad = ""; useSta = ""; diff = ""; faci = "";
                bidCnt = "1"; bidTm1 = ""; bidTm2 = ""; bidTm3 = ""; minbAmt2 = "";

                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                pn = (row["pn"].ToString() == "0") ? "1" : row["pn"].ToString();
                url = "https://www.courtauction.go.kr/RetrieveRealEstCarHvyMachineMulDetailInfo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&maemulSer=" + pn;
                html = net.GetHtml(url);
                if (html.Contains("공고중인 물건이 아닙니다") || html.Contains("존재하지 않는 페이지입니다")) continue;

                doc.LoadHtml(html);

                tid = row["tid"].ToString();
                frmlType = row["frml_type"].ToString();
                minbAmt1 = row["minb_amt"].ToString();
                bidDt = string.Format("{0:yyyy-MM-dd}", row["bid_dt"]);
                preDt = row["pre_dt"].ToString();
                preNtFlag = (preDt.Contains("0001")) ? false : true;

                //물건기본정보-종별 판단
                HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='물건기본정보 표']/tr");
                foreach (HtmlNode tr in ncTr)
                {
                    HtmlNodeCollection ncTd = tr.SelectNodes("th|td");

                    if (Regex.IsMatch(ncTd[0].InnerText, "감정평가액"))
                    {
                        if (ncTd[3].InnerHtml.Contains("img"))
                        {
                            MatchCollection matches = Regex.Matches(ncTd[3].InnerText, @"(\d{1,3}(,\d{3})+)원\s*\((\d{2}:\d{2})\)", rxOptM);
                            if (matches.Count > 0)
                            {
                                bidCnt = matches.Count.ToString();
                                bidTm1 = matches[0].Groups[3].Value + ":00";
                                bidTm2 = matches[1].Groups[3].Value + ":00";
                                minbAmt2 = matches[1].Groups[1].Value.Replace(",", string.Empty);
                                if (matches.Count > 2)
                                {
                                    bidTm3 = matches[2].Groups[3].Value + ":00";
                                }
                            }
                        }
                    }

                    if (Regex.IsMatch(ncTd[0].InnerText, "사건번호"))
                    {
                        catKind = ncTd[5].InnerText.Trim(); //물건종류
                    }

                    if (Regex.IsMatch(ncTd[0].InnerText, "매각기일"))
                    {
                        Match match = Regex.Match(ncTd[1].InnerText, @"(\d{2}:\d{2})", rxOptM);
                        bidTm1 = match.Groups[1].Value + ":00";
                    }

                    if (Regex.IsMatch(ncTd[0].InnerText, @"목록\d+[ ]*(소재지|사용본거지)"))  //2021-11-09 차량->소재지에서 사용본거지로 멘트 변경됨
                    {
                        Match match = Regex.Match(ncTd[1].InnerText.Trim(), @"^\(([\w/]+)\)", rxOptM);  //(주/상용건물), (주/산용건물)
                        if (match.Success)
                        {
                            catStr = match.Groups[1].Value;
                            listCat.Add(catStr);
                        }
                    }

                    if (Regex.IsMatch(ncTd[0].InnerText, "물건비고"))
                    {
                        pdNote = ncTd[1].InnerText.Trim();
                    }
                }

                catStr = string.Empty;
                if (listCat.Count == 1) catStr = listCat[0];
                else if (listCat.Count > 1)
                {
                    foreach (string str in listCat)
                    {
                        if (Regex.IsMatch(str, landPtrn) == false)
                        {
                            catStr = str;
                            break;
                        }
                    }
                    if (catStr == string.Empty)
                    {
                        foreach (string str in listCat)
                        {
                            if (Regex.IsMatch(str, landPtrn) == true)
                            {
                                catStr = str;
                                break;
                            }
                        }
                    }
                }

                if (catStr != string.Empty)
                {
                    var x = from DataRow r in dtCatCd.Rows
                            where r["cat3_nm"].ToString() == catStr
                            select r;
                    if (x.Count() > 0)
                    {
                        DataTable dtCatRslt = x.CopyToDataTable();
                        cat1 = dtCatRslt.Rows[0]["cat1_cd"].ToString();
                        cat2 = dtCatRslt.Rows[0]["cat2_cd"].ToString();
                        cat3 = dtCatRslt.Rows[0]["cat3_cd"].ToString();
                    }
                }

                landBldgFlag = false;
                HtmlNode tblApsl = doc.DocumentNode.SelectSingleNode("//table[@summary='감정평가요항표']");
                if (listCat.Count > 0)
                {
                    listCatStr = string.Join(",", listCat.ToArray());
                    Match matchCar = Regex.Match(listCatStr, "승용차|승합차|버스|화물차|기타차량", rxOptM);
                    Match matchHeavyEquip = Regex.Match(listCatStr, "덤프트럭|굴삭기|지게차|기타중기", rxOptM);
                    Match matchShip = Regex.Match(listCatStr, "선박", rxOptM);
                    Match matchAir = Regex.Match(listCatStr, "항공기", rxOptM);
                    Match matchBike = Regex.Match(listCatStr, "이륜차", rxOptM);

                    Match matchFish = Regex.Match(listCatStr, "어업권", rxOptM);
                    Match matchMine = Regex.Match(listCatStr, "광업권", rxOptM);

                    if (matchCar.Success) PrcDtlSub_Car(tid, doc, matchCar.Value, tblApsl, preNtFlag);
                    else if (matchHeavyEquip.Success) PrcDtlSub_Car(tid, doc, matchHeavyEquip.Value, tblApsl, preNtFlag);
                    else if (matchShip.Success)
                    {
                        if (preNtFlag == false)     //선행공고가 아닐 경우만
                        {
                            PrcDtlSub_Ship(tid, doc);
                        }
                    }
                    else if (matchAir.Success) PrcDtlSub_Air(tid, doc);
                    else if (matchBike.Success) PrcDtlSub_Bike(tid, doc);
                    else if (matchFish.Success)
                    {
                        if (preNtFlag == false)     //선행공고가 아닐 경우만
                        {
                            PrcDtlSub_Fish(tid, doc, tblApsl, pdNote);
                        }
                    }
                    else if (matchMine.Success)
                    {
                        if (preNtFlag == false)     //선행공고가 아닐 경우만
                        {
                            PrcDtlSub_Mine(tid, doc, tblApsl);
                        }
                    }
                    else
                    {
                        landBldgFlag = true;
                        if (preNtFlag == false)     //선행공고가 아닐 경우만
                        {
                            PrcDtlSub_LandBldg(tid, doc, tblApsl, preNtFlag);
                        }
                    }
                }
                else
                {
                    landBldgFlag = true;
                    if (preNtFlag == false)     //선행공고가 아닐 경우만
                    {
                        PrcDtlSub_LandBldg(tid, doc, tblApsl, preNtFlag);
                    }   
                }

                if (landBldgFlag && tblApsl != null)
                {
                    //HtmlNode nd = tblApsl.SelectSingleNode(".//li/*[text()[contains(.,'1) 위치 및 주위환경')]]");    //가능
                    HtmlNode nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'1) 위치 및 주위환경')]]");
                    if (nd == null) nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'1) 위치 및 부근의 상황')]]"); //-> 공장감정평가요항표
                    if (nd != null) loca = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                    nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'2) 교통상황')]]");
                    if (nd != null) tfc = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                    nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'3) 형태 및 이용상태')]]");
                    if (nd != null) landShp = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                    nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'4) 인접 도로상태')]]");
                    if (nd != null) adjRoad = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                    nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'7) 공부와의 차이')]]");
                    if (nd != null) diff = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();

                    nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'3) 설비내역')]]");
                    if (nd == null) nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'5) 설비내역')]]");
                    if (nd != null) faci = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();

                    nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'4) 이용상태')]]");
                    if (nd != null) useSta = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                    nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'6) 토지의 형상 및 이용상태')]]");
                    if (nd == null) nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'2) 토지의 상황')]]");      //-> 공장감정평가요항표
                    if (nd != null) landShp = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                    nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'7) 인접 도로상태등')]]");
                    if (nd != null) adjRoad = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                }

                HtmlNode tblLs = null;
                if (cat3 != string.Empty)
                {
                    decimal cat = Convert.ToDecimal(cat3);
                    string apslTxt = (tblApsl == null) ? string.Empty : tblApsl.InnerText;
                    if (hideCatArr.Contains(cat))
                    {
                        cat0 = cat.ToString();
                        if (cat == 201012)
                        {
                            cat3 = "201011";    //다중주택 -> 다가구주택
                        }
                        else if (cat == 201017)
                        {
                            cat3 = "201015";    //빌라 -> 다세대주택
                        }
                        else if (cat == 201019)
                        {
                            if (useSta.Contains("주거")) cat3 = "201020";     //오피스텔 -> 오피스텔(주거)
                            else cat3 = "201111";   //오피스텔 -> 오피스텔(상업)
                        }
                        else if (cat == 201120)
                        {
                            if (apslTxt.Contains("구분건물감정평가요항표")) cat3 = "201130";   //운동시설 -> 근린상가(집합건물일 경우)
                            else cat3 = "201110";   //운동시설 -> 근린생활시설(토지/건물일 경우)
                        }
                        else if (cat == 201124)
                        {
                            if (apslTxt.Contains("구분건물감정평가요항표")) cat3 = "201130";   //위락시설 -> 근린상가(집합건물일 경우)
                            else cat3 = "201110";   //위락시설 -> 근린생활시설(토지/건물일 경우)
                        }
                        else if (cat == 201115 || cat == 201119 || cat == 201125 || cat == 201126 || cat == 201127 || cat == 201129)
                        {
                            cat3 = "201132";    //운수시설, 수련시설, 교정및군사시설, 방송통신시설, 발전시설, 관광휴게시설 -> 기타(cat2: 상업용및업무용)
                        }
                        cat1 = cat3.Substring(0, 2);
                        cat2 = cat3.Substring(0, 4);
                    }
                    if (cat == 201122)
                    {
                        if (apslTxt.Contains("구분건물감정평가요항표"))
                        {
                            cat0 = cat.ToString();
                            cat1 = "20";
                            cat2 = "2011";
                            cat3 = "201123";    //숙박시설 -> 숙박(콘도등)
                        }
                    }
                    else if (cat == 201110 || cat == 201114)
                    {
                        if (apslTxt.Contains("구분건물감정평가요항표"))
                        {
                            cat0 = cat.ToString();
                            cat1 = "20";
                            cat2 = "2011";
                            cat3 = "201130";    //근린생활시설(201110), 판매시설(201114) -> 근린상가
                        }
                    }
                    else if (cat == 201210)
                    {
                        if (apslTxt.Contains("구분건물감정평가요항표"))
                        {
                            cat0 = cat.ToString();
                            cat1 = "20";
                            cat2 = "2012";
                            cat3 = "201216";    //공장 -> 지식산업센터(아파트형공장)
                        }
                    }
                    else if (cat == 201121)
                    {
                        if (apslTxt.Contains("구분건물감정평가요항표"))
                        {
                            cat0 = cat.ToString();
                            cat1 = "20";
                            cat2 = "2011";
                            cat3 = "201111";    //업무시설 -> 오피스텔(상업)
                        }
                    }
                    if (useSta.Contains("도시형생활주택"))
                    {
                        cat0 = cat.ToString();
                        cat1 = "20";
                        cat2 = "2010";
                        cat3 = "201022";        //이용상태가 도시형생활주택인 경우
                    }

                    //HtmlNode lsNode = doc.DocumentNode.SelectSingleNode("//table[@summary='목록내역 표']");
                    tblLs = doc.DocumentNode.SelectSingleNode("//table[@summary='목록내역 표']");
                    dpslDvsn = "0";
                    if (tblLs != null)
                    {
                        dpslDvsn = Dpsl_DvsnCd(pdNote, tblLs);
                    }

                    spRgst = "0";
                    if (pdNote != "" && multiBldgArr.Contains(Convert.ToDecimal(cat3)))
                    {
                        spRgst = Sp_RgstCd(pdNote);
                    }

                    if (cat1 == "30" && frmlType != "0")
                    {
                        frmlType = "6";
                    }

                    if (preDt.Contains("0001"))     //선행공고가 아닐 경우만
                    {
                        sql = "update ta_list set bid_cnt=@bid_cnt, bid_tm=@bid_tm, bid_tm1=@bid_tm1, bid_tm2=@bid_tm2, bid_tm3=@bid_tm3, cat0=@cat0, cat1=@cat1, cat2=@cat2, cat3=@cat3, dpsl_dvsn=@dpsl_dvsn, sp_rgst=@sp_rgst, frml_type=@frml_type where tid=@tid";
                        db.Open();
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@bid_cnt", bidCnt));
                        sp.Add(new MySqlParameter("@bid_tm", bidTm1));
                        sp.Add(new MySqlParameter("@bid_tm1", bidTm1));
                        sp.Add(new MySqlParameter("@bid_tm2", bidTm2));
                        sp.Add(new MySqlParameter("@bid_tm3", bidTm3));
                        sp.Add(new MySqlParameter("@cat0", cat0));
                        sp.Add(new MySqlParameter("@cat1", cat1));
                        sp.Add(new MySqlParameter("@cat2", cat2));
                        sp.Add(new MySqlParameter("@cat3", cat3));
                        sp.Add(new MySqlParameter("@dpsl_dvsn", dpslDvsn));
                        sp.Add(new MySqlParameter("@sp_rgst", spRgst));
                        sp.Add(new MySqlParameter("@frml_type", frmlType));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                        db.Close();
                    }                    
                }
                else
                {
                    cat0 = ""; cat1 = ""; cat2 = ""; cat3 = "";
                    if (catKind != string.Empty)
                    {
                        var x = from DataRow r in dtCatCd.Rows
                                where r["cat3_nm"].ToString() == catKind
                                select r;
                        if (x.Count() > 0)
                        {
                            DataTable dtCatRslt = x.CopyToDataTable();
                            cat1 = dtCatRslt.Rows[0]["cat1_cd"].ToString();
                            cat2 = dtCatRslt.Rows[0]["cat2_cd"].ToString();
                            cat3 = dtCatRslt.Rows[0]["cat3_cd"].ToString();
                            cat0 = "0";
                        }
                    }
                    if (preDt.Contains("0001"))     //선행공고가 아닐 경우만
                    {
                        sql = "update ta_list set bid_cnt=@bid_cnt, bid_tm=@bid_tm, bid_tm1=@bid_tm1, bid_tm2=@bid_tm2, bid_tm3=@bid_tm3, cat0=@cat0, cat1=@cat1, cat2=@cat2, cat3=@cat3 where tid=@tid";
                        db.Open();
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@bid_cnt", bidCnt));
                        sp.Add(new MySqlParameter("@bid_tm", bidTm1));
                        sp.Add(new MySqlParameter("@bid_tm1", bidTm1));
                        sp.Add(new MySqlParameter("@bid_tm2", bidTm2));
                        sp.Add(new MySqlParameter("@bid_tm3", bidTm3));
                        sp.Add(new MySqlParameter("@cat0", cat0));
                        sp.Add(new MySqlParameter("@cat1", cat1));
                        sp.Add(new MySqlParameter("@cat2", cat2));
                        sp.Add(new MySqlParameter("@cat3", cat3));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                        db.Close();
                    }                    
                }

                db.Open();
                cvp = "loca=@loca, land_shp=@land_shp, adj_road=@adj_road, diff=@diff, faci=@faci";
                sql = "insert into ta_dtl set tid=@tid, " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@loca", loca + "\r\n" + tfc));
                sp.Add(new MySqlParameter("@land_shp", landShp));
                sp.Add(new MySqlParameter("@adj_road", adjRoad));
                sp.Add(new MySqlParameter("@diff", diff));
                sp.Add(new MySqlParameter("@faci", faci));
                db.ExeQry(sql, sp);
                sp.Clear();
                db.Close();

                //매각 일정 등록
                if (preDt.Contains("0001"))     //선행공고가 아닐 경우만
                {
                    db.Open();
                    sql = "delete from ta_hist where tid=" + tid;
                    db.ExeQry(sql);

                    sql = "insert into ta_hist set tid=@tid, bid_dt=@bid_dt, bid_tm=@bid_tm, sta=1110, amt=@amt";
                    sp.Add(new MySqlParameter("@tid", tid));
                    sp.Add(new MySqlParameter("@bid_dt", bidDt));
                    sp.Add(new MySqlParameter("@bid_tm", bidTm1));
                    sp.Add(new MySqlParameter("@amt", minbAmt1));
                    db.ExeQry(sql, sp);
                    sp.Clear();
                    if (bidTm2 != "" && minbAmt2 != "") //2회차
                    {
                        sql = "insert into ta_hist set tid=@tid, bid_dt=@bid_dt, bid_tm=@bid_tm, sta=1110, amt=@amt";
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@bid_dt", bidDt));
                        sp.Add(new MySqlParameter("@bid_tm", bidTm2));
                        sp.Add(new MySqlParameter("@amt", minbAmt2));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }

                    sql = "update ta_list set auto_prc=2 where tid=" + tid;
                    db.ExeQry(sql);
                    db.Close();
                }

                //건물현황-이용상태
                PrcDtlSub_BldgUseState(tid, tblApsl, tblLs);
            }
        }

        /// <summary>
        /// 건물현황-이용상태
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="tblApsl">감정평가요항표</param>
        /// <param name="tblLs">목록내역표</param>
        private void PrcDtlSub_BldgUseState(string tid, HtmlNode tblApsl, HtmlNode tblLs)
        {
            string sql, useStr, state = "";
            int lsCnt = 0;
            
            sql = "select B.idx, B.ls_no, B.state, L.dvsn from ta_ls L, ta_bldg B where L.tid=B.tid and L.no=B.ls_no and L.tid=" + tid + " and L.dvsn in ('건물','집합건물') and B.dvsn=1";
            DataTable dtLs = db.ExeDt(sql);
            lsCnt = dtLs.Rows.Count;
            if (lsCnt == 0) return;
            /*
            sql = "select cat3 from ta_list where tid=" + tid;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            cat = dr["cat3"].ToString();
            dr.Close();
            db.Close();
            */
            Match match;

            //감정평가요항표에서
            if (tblApsl != null)
            {
                useStr = Regex.Match(tblApsl.InnerHtml, @"<li><p class=""law_title"">\d+\)[ ]* 이용상태</p>\s+<ul><li><span.*?>(.*?)</span>", rxOptS).Groups[1].Value.Trim();
                if (useStr != string.Empty)
                {
                    if (Regex.IsMatch(useStr, @"(기호|번호|\d+\-[\(]*[가-하][\)]*)", rxOptM))
                    {
                        //
                    }
                    else
                    {
                        if (Regex.IsMatch(useStr, @"\d+층", rxOptM) && lsCnt > 1)
                        {
                            //
                        }
                        else
                        {
                            List<string> ptrnList = new List<string>();
                            ptrnList.Add(@"(.*?)[으로서]+[ ]*(이용|사용|내부)");
                            ptrnList.Add(@"용도는[ ]*(.*?)[으로]+");
                            ptrnList.Add(@"^((다세대주택|아파트)(\([\w\d, ]{5,}\))*)$");
                            ptrnList.Add(@"^[-]*[""]*(\w+(\(\w+\))*)[""]*[임\.]*$");
                            ptrnList.Add(@"본건은[ ]*(.*?)임");
                            ptrnList.Add(@"^현황[ ]*(.*?)임");
                            ptrnList.Add(@"(.*?)입니다");
                            ptrnList.Add(@"대장상[ ]*(\w+)이나");
                            ptrnList.Add(@"((아파트|연립주택)[\w\d,\( \)]*?)[임\.]");

                            foreach (string ptrn in ptrnList)
                            {
                                match = Regex.Match(useStr, ptrn, rxOptM);
                                if (match.Success)
                                {
                                    state = match.Groups[1].Value;
                                    break;
                                }
                            }

                            List<string> trimList = new List<string>();
                            trimList.Add(@".*결과");   //건축물대장 현황도면 및 탐문조사 결과 오피스텔
                            trimList.Add(@"이나.*");   //상가(소매점)이나 현재 공실 상태
                            trimList.Add(@"^(현황|\d+\)|기준시점 현재)");     //현황 다세대주택, 기준시점 현재 다세대주택, #) 연립주택
                            trimList.Add(@"집합건축물대장도면[ ]*상");        //아파트(집합건축물대장도면 상 방3, 욕실, 주방겸식당 등)
                            trimList.Add(@"(임|단위세대|\d+세대|\d+개호)$");  //아파트임, 아파트 단위세대, 아파트 1세대, 다세대주택 1개호
                            trimList.Add(@"\((별첨|후면)[ ]*내부구조도[ ]*참조\)");   //아파트(별첨 내부구조도참조), 아파트(후면 내부구조도 참조)

                            state = Regex.Replace(state, @"본[ ]*건은|공히|집합건축물대장상|전체를|공동주택으로서|^공부상 \w+ 현황|""|^\-|\(후첨.*\)|\(\d+층[ ]*\d+호\)|\d+층[ ]*\d+호|^[가-하]\)|구조$|\(내부.*|^(공부상|현황)|용도로서.*", string.Empty).Trim();
                            if (state != string.Empty)
                            {
                                foreach (string ptrn in trimList)
                                {
                                    state = Regex.Replace(state, ptrn, string.Empty, rxOptM);
                                }
                            }
                            if (state != string.Empty && !state.Contains("공실") && useStr.Contains("공실"))
                            {
                                state = $"{state}(현황:공실)";
                            }
                            if (state == "공동주택(아파트)") state = "아파트";
                            else if (state == "공동주택(연립주택)") state = "연립주택";
                            if (state != string.Empty)
                            {
                                db.Open();
                                foreach (DataRow row in dtLs.Rows)
                                {
                                    if (row["state"].ToString() != string.Empty) continue;
                                    sql = "update ta_bldg set state='" + state + "' where idx=" + row["idx"].ToString();
                                    db.ExeQry(sql);
                                }
                                db.Close();
                            }
                        }
                    }
                }
            }

            if (state != string.Empty) return;
            if (tblLs == null) return;

            //목록내역에서
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
        /// 매각구분 코드
        /// </summary>
        /// <param name="pdNote">물건비고</param>
        /// <param name="lsNode"></param>
        /// <returns></returns>
        private string Dpsl_DvsnCd(string pdNote, HtmlNode ndTbl)
        {
            decimal retCd = 0;
            string lsDvsn = string.Empty, dvsn = string.Empty, dtlAllStr = string.Empty, dtlEaStr = string.Empty;
            bool flagLand = false, flagBldg = false, flagMultBldg = false;
            bool flagLandShr = false, flagBldgShr = false, flagMultShr = false;

            dtlAllStr = ndTbl.InnerText;
            HtmlNodeCollection ncTr = ndTbl.SelectNodes("./tbody/tr");
            if (ncTr != null)
            {
                foreach (HtmlNode ndTr in ncTr)
                {
                    HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                    lsDvsn = ncTd[1].InnerText.Trim();
                    dtlEaStr = ncTd[2].InnerText.Trim();
                    if (lsDvsn == "토지")
                    {
                        flagLand = true;
                        if (dtlEaStr.Contains("매각지분")) flagLandShr = true;
                    }
                    if (lsDvsn == "건물")
                    {
                        flagBldg = true;
                        if (dtlEaStr.Contains("매각지분")) flagBldgShr = true;
                    }
                    if (lsDvsn == "집합건물")
                    {
                        flagMultBldg = true;
                        if (dtlEaStr.Contains("매각지분")) flagMultShr = true;
                    }
                }
            }

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
        /// 물건상세Sub-면적/대지권/현황/구조(토지,건물,제시외)
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="doc"></param>
        private void PrcDtlSub_LandBldg(string tid, HAPDoc doc, HtmlNode tblApsl, bool preNtFlag)
        {
            string sql = "", lsNo = "", lsType = "", catNm = "", catCd = "", floor = "", flrCd = "", shrStr0 = "", shrStr = "", frtn = "", dtlStr = "", etcStr = "", use = "", strt = "", area = "", lotCnt = "";
            string landSection = "", bldgSection = "";
            double sqm = 0, totSqm = 0, bldgSqm = 0, totShrSqm = 0, dt = 0, nt = 0;
            double sumLandSqm = 0, sumLandTotSqm = 0, sumRtSqm = 0, rtTotSqm = 0, sumBldgSqm = 0, sumBldgTotSqm = 0;
            bool macExist = false;
            int i = 0;

            string bldgPtrn = @"([지하옥탑상일이삼사오육칠팔구십단\d]+[층실])[ ]*(.*?[소실조택고장당원설점\)])*[ ]*(\d[\d\.\,]*)[ ]*㎡";
            string etcPtrn1 = @"\d+[\.\s]+\(용도\)(.*)\s+\(구조\)(.*)\s+\(면적\)[\D ]*(\d[\d\.\,]*)[ ]*([㎡주식개\d\*\(\)\w\, ]+)";     //제시외 패턴-1 (1-용도, 2-구조, 3-면적, 4-단위 및 기타)
            string etcPtrn2 = @"\d+[\.\s]+\(용도\)(.*)\s+\(구조\)(.*)";                                                                 //제시외 패턴-2 (1-용도, 2-구조) -> 패턴-1과 용도와 구조는 동일하나 면적부분이 없음
            string macPtrn = @"기계기구|[a-z]{4,}|\d{4}|\w+[\d]*\-\d+|kw|kva|ton|mm|kg";
            //string frtnPtrn1 = @"(\d+[\.\d]*)[ ]*분의[ ]*(\d+[\.\d]*)";   //분수 패턴-1(오류: 매각지분  : 1. 소유권대지권 42165.6분의 38.1945.) //2021-05-13
            string frtnPtrn1 = @"(\d+[\.]*[\d]*)[ ]*분의[ ]*(\d+[\.]*[\d]*)";   //분수 패턴-1
            string frtnPtrn2 = @"(\d+[\.]*[\d]*)/(\d+[\.]*[\d]*)";              //분수 패턴-2

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

            //제시외
            DataTable dtE = new DataTable();
            dtE.Columns.Add("lsNo");
            dtE.Columns.Add("state");
            dtE.Columns.Add("struct");
            dtE.Columns.Add("sqm");

            HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='목록내역 표']/tbody/tr");
            if (ncTr == null) return;

            List<MySqlParameter> sp = new List<MySqlParameter>();

            foreach (HtmlNode tr in ncTr)
            {
                sqm = 0; totSqm = 0; bldgSqm = 0; totShrSqm = 0; dt = 0; nt = 0;
                floor = ""; shrStr0 = ""; shrStr = ""; etcStr = ""; use = ""; strt = ""; area = "";

                HtmlNodeCollection ncTd = tr.SelectNodes("./td");
                if (ncTd.Count != 3) continue;
                lsNo = ncTd[0].InnerText.Trim();
                lsType = ncTd[1].InnerText.Trim();
                dtlStr = ncTd[2].InnerText.Replace("&nbsp;", string.Empty).Trim();
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
                    match = Regex.Match(dtlStr, @"매각지분[ ]*:[ ]*(.*)", rxOptS);

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

                    //제시외
                    if (etcStr != string.Empty)
                    {
                        MatchCollection mc = Regex.Matches(etcStr, etcPtrn1, rxOptM);
                        if (mc.Count > 0)
                        {
                            foreach (Match m in mc)
                            {
                                use = m.Groups[1].Value.Trim();
                                strt = m.Groups[2].Value.Trim();
                                area = m.Groups[3].Value.Trim();
                                if (use.Contains("기계기구"))
                                {
                                    macExist = true;
                                    continue;
                                }
                                if (m.Value.Contains("수목") || m.Value.Contains("나무") || m.Value.Contains("관정"))
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
                                foreach (Match m in mc)
                                {
                                    use = m.Groups[1].Value.Trim();
                                    strt = m.Groups[2].Value.Trim();
                                    if (use.Contains("기계기구"))
                                    {
                                        macExist = true;
                                        continue;
                                    }
                                    if (m.Value.Contains("수목") || m.Value.Contains("나무") || m.Value.Contains("관정"))
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
                else if (lsType == "건물")
                {
                    int eIndex = dtlStr.IndexOf("제시외");
                    if (eIndex > -1)
                    {
                        etcStr = dtlStr.Substring(eIndex);
                        dtlStr = dtlStr.Remove(eIndex);
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
                            dtB.Rows.Add(lsNo, 0, floor, sqm, totShrSqm, "", match.Value, "");
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
                                dtB.Rows.Add(lsNo, 0, floor, sqm, totShrSqm, "", match.Value, "");
                            }
                        }
                    }
                    Match matchShr = Regex.Match(dtlStr, @"매각지분[ ]*:[ ]*(.*)", rxOptS);
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

                    //제시외
                    if (etcStr != string.Empty)
                    {
                        MatchCollection mc = Regex.Matches(etcStr, etcPtrn1, rxOptM);
                        if (mc.Count > 0)
                        {
                            foreach (Match m in mc)
                            {
                                use = m.Groups[1].Value.Trim();
                                strt = m.Groups[2].Value.Trim();
                                area = m.Groups[3].Value.Trim();
                                if (use.Contains("기계기구"))
                                {
                                    macExist = true;
                                    continue;
                                }
                                if (m.Value.Contains("수목") || m.Value.Contains("나무") || m.Value.Contains("관정"))
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
                                foreach (Match m in mc)
                                {
                                    use = m.Groups[1].Value.Trim();
                                    strt = m.Groups[2].Value.Trim();
                                    if (use.Contains("기계기구"))
                                    {
                                        macExist = true;
                                        continue;
                                    }
                                    if (m.Value.Contains("수목") || m.Value.Contains("나무") || m.Value.Contains("관정"))
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
                                dtB.Rows.Add(lsNo, 1, floor, bldgSqm, "", "", "", "");

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
                                dtB.Rows.Add(lsNo, 1, floor, bldgSqm, "", "", "", "");
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
                                dtB.Rows.Add(lsNo, 1, floor, bldgSqm, "", "", "", "");
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
                                dtB.Rows.Add(lsNo, 1, floor, bldgSqm, "", "", "", "");
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
                                dtB.Rows.Add(lsNo, 1, floor, bldgSqm, "", "", "", "");
                            }
                        }
                    }
                    else
                    {
                        //
                    }

                    if (dtlStr.Contains("매각지분"))
                    {
                        Match match1 = Regex.Match(dtlStr, @"매각지분[ ]*:[ ]*(.*)", rxOptS);
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
                                    /*
                                    if (totShrSqm != bldgSqm)
                                    {
                                        dtB.Rows[dtB.Rows.Count - 1]["totShrSqm"] = totShrSqm;
                                        dtB.Rows[dtB.Rows.Count - 1]["sqm"] = bldgSqm.ToString();
                                        dtB.Rows[dtB.Rows.Count - 1]["shrStr"] = string.Format("전체면적 {0}㎡중 {1}", totShrSqm, match1.Groups[1].Value.Trim());
                                    }
                                    */
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

                    //제시외
                    if (etcStr != string.Empty)
                    {
                        MatchCollection mc = Regex.Matches(etcStr, etcPtrn1, rxOptM);
                        if (mc.Count > 0)
                        {
                            foreach (Match m in mc)
                            {
                                use = m.Groups[1].Value.Trim();
                                strt = m.Groups[2].Value.Trim();
                                area = m.Groups[3].Value.Trim();
                                if (use.Contains("기계기구"))
                                {
                                    macExist = true;
                                    continue;
                                }
                                if (m.Value.Contains("수목") || m.Value.Contains("나무") || m.Value.Contains("관정"))
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
                                foreach (Match m in mc)
                                {
                                    use = m.Groups[1].Value.Trim();
                                    strt = m.Groups[2].Value.Trim();
                                    if (use.Contains("기계기구"))
                                    {
                                        macExist = true;
                                        continue;
                                    }
                                    if (m.Value.Contains("수목") || m.Value.Contains("나무") || m.Value.Contains("관정"))
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
                else if (lsType == "자동차")
                {
                    PrcDtlSub_Car(tid, doc, lsType, tblApsl, preNtFlag);
                    break;
                }
                else if (lsType == "건설기계,중기")
                {
                    PrcDtlSub_Car(tid, doc, lsType, tblApsl, preNtFlag);
                    break;
                }
                else if (lsType == "선박")
                {
                    PrcDtlSub_Ship(tid, doc);
                    break;
                }
                else if (lsType == "항공기")
                {
                    //
                }
                else if (lsType == "기타")
                {
                    //
                }
                else
                {
                    continue;
                }
            }

            sql = "select no, adrs from ta_ls where tid=" + tid;
            DataTable dtLs = db.ExeDt(sql);

            db.Open();
            sql = "delete from ta_land where tid=" + tid;
            db.ExeQry(sql);

            sql = "delete from ta_bldg where tid=" + tid;
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
                    var xFlr = dtFlrCd.Rows.Cast<DataRow>().Where(t => t["flr_nm"].ToString() == r["floor"].ToString()).SingleOrDefault();
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

                sql = "insert into ta_bldg (tid, ls_no, dvsn, flr, tot_flr, sqm, tot_shr_sqm, shr_str) values (@tid, @ls_no, @dvsn, @flr, @tot_flr, @sqm, @tot_shr_sqm, @shr_str)";
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@ls_no", r["lsNo"]));
                sp.Add(new MySqlParameter("@dvsn", 1));
                sp.Add(new MySqlParameter("@flr", flrCd));
                sp.Add(new MySqlParameter("@tot_flr", r["totFlr"]));
                sp.Add(new MySqlParameter("@sqm", r["sqm"]));
                sp.Add(new MySqlParameter("@tot_shr_sqm", r["totShrSqm"]));
                sp.Add(new MySqlParameter("@shr_str", r["shrStr"]));
                db.ExeQry(sql, sp);
                sp.Clear();

                if (string.IsNullOrEmpty(r["sqm"].ToString()) == false) sumBldgSqm += Convert.ToDouble(r["sqm"]);                //총합-건물
                if (string.IsNullOrEmpty(r["totShrSqm"].ToString()) == false) sumBldgTotSqm += Convert.ToDouble(r["totShrSqm"]); //총합-건물지분
            }

            //제시외건물
            foreach (DataRow r in dtE.Rows)
            {
                sql = "insert into ta_bldg (tid, ls_no, dvsn, sqm, state, struct) values (@tid, @ls_no, @dvsn, @sqm, @state, @struct)";
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@ls_no", r["lsNo"]));
                sp.Add(new MySqlParameter("@dvsn", 2));
                sp.Add(new MySqlParameter("@sqm", r["sqm"].ToString().Trim()));
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

            //목록구분이 집합건물만 있는 경우 필지수 계산
            if (lsType == "집합건물" && ncTr.Count == 1 && landSection != string.Empty)
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
        /// 물건상세Sub-목록내역(차량-승용차, 승합차, 버스, 화물차, 기타차량 / 중기-덤프트럭, 굴삭기, 지게차, 기타중기)
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="doc"></param>
        /// <param name="cat"></param>
        private void PrcDtlSub_Car(string tid, HAPDoc doc, string cat, HtmlNode tblApsl, bool preNtFlag)
        {
            string sql, apslTxt, cvp;
            string lsNo = "", apslAmt = "", carNm = "", carType = "", regNo = "", carYear = "", cmpy = "", trans = "", fuelType = "", mtr = "", aprvNo = "", idNo = "", dspl = "", dist = "", park = "";
            string coCd = "", moCd = "", transCd = "", fuelCd = "", color = "", term = "", etc = "", pdNote = "";
            string adrs, sidoCd, gugunCd, dongCd, riCd, x, y, hCd, pnu, zoneNo, adrsType, regnAdrs, mt;
            StringBuilder sb = new StringBuilder();

            IDictionary<string, string> dict = new Dictionary<string, string>();

            if (tblApsl != null)
            {
                apslTxt = tblApsl.InnerText;
                /*
                if (apslTxt.Contains("기호") == false)
                { 
                    //
                }
                */
                if (apslTxt.Contains("자동차감정평가요항표"))
                {
                    HtmlNode nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'2)색상')]]");
                    if (nd != null)
                    {
                        color = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                        Match match = Regex.Match(color, @"\w+색[투톤]*|검정|빨강|노랑|파랑|초록|문라이트블루");
                        color = (match.Success) ? match.Value : string.Empty;
                    }

                    nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'5)유효검사기간')]]");
                    if (nd != null)
                    {
                        term = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                        term = term.Replace(" ", string.Empty);
                        Match match = Regex.Match(term, @"(\d{4}.\d{1,2}.\d{1,2})[.일]{0,1}[\s\-\~\∼]*(\d{4}.\d{1,2}.\d{1,2})[.일]{0,1}", rxOptM);
                        if (match.Success)
                        {
                            term = string.Format("{0}~{1}", match.Groups[1].Value, match.Groups[2].Value);
                            term = Regex.Replace(term, @"[\-년월]", ".");
                        }
                        else
                        {
                            term = string.Empty;
                        }
                    }

                    nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'3)관리상태')]]");
                    if (nd != null)
                    {
                        sb.AppendLine(nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim());
                    }
                    nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'6)기타')]]");
                    if (nd != null)
                    {
                        etc = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                        if (!etc.Contains("없습니다"))
                        {
                            sb.AppendLine(etc);
                        }
                    }
                    pdNote = sb.ToString().Trim();
                }
            }

            List<MySqlParameter> sp = new List<MySqlParameter>();
            HtmlNodeCollection tblCars = doc.DocumentNode.SelectNodes("//table[@summary='목록내역 표']");
            if (tblCars == null) return;

            sql = "delete from ta_cars where tid=" + tid;
            db.Open();
            db.ExeQry(sql);
            db.Close();

            foreach (HtmlNode tblCar in tblCars)
            {
                HtmlNodeCollection ncTr = tblCar.SelectNodes("./tr");
                if (ncTr == null) continue;
                foreach (HtmlNode tr in ncTr)
                {
                    HtmlNodeCollection ncTd = tr.SelectNodes("./th|./td");
                    //if (ncTd == null) return;
                    if (ncTd == null) continue;
                    foreach (HtmlNode td in ncTd)
                    {
                        if (td.InnerText == "목록번호") lsNo = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                        if (td.InnerText == "감정평가액") apslAmt = td.SelectSingleNode("following-sibling::*[1]").InnerText.Replace(",", string.Empty).Replace("원", string.Empty).Trim();
                        if (td.InnerText == "차명") carNm = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                        if (td.InnerText == "차종") carType = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                        if (td.InnerText == "등록번호") regNo = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                        if (td.InnerText == "연식") carYear = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                        if (td.InnerText == "제조사") cmpy = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                        if (td.InnerText == "연료종류") fuelType = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                        if (td.InnerText == "변속기" && td.OuterHtml.Contains("th")) trans = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();  //<th>변속기</th><td>변속기</td> 이런 케이스 있음
                        if (td.InnerText == "원동기형식") mtr = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                        if (td.InnerText == "승인번호") aprvNo = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                        if (td.InnerText == "차대번호") idNo = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                        if (td.InnerText == "배기량") dspl = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                        if (td.InnerText == "주행거리") dist = td.SelectSingleNode("following-sibling::*[1]").InnerText.Replace(",", string.Empty).Trim();
                        if (td.InnerText == "보관장소") park = Regex.Replace(td.SelectSingleNode("following-sibling::*[1]").InnerText, @"\s{2,}", " ").Trim();
                    }
                }

                if (cmpy != "")
                {
                    foreach (DataRow row in dtCarCoCd.Rows)
                    {
                        string tmpCmpy = Regex.Replace(cmpy, @"(주)|주식회사|\s", string.Empty, rxOptM).Trim();
                        Match match = Regex.Match(tmpCmpy, row["rx"].ToString(), rxOptM);
                        if (match.Success)
                        {
                            coCd = row["co_cd"].ToString();
                            break;
                        }
                    }
                }
                if (carNm != "" && coCd != "")
                {
                    DataTable dt = dtCarMoCd.Rows.Cast<DataRow>().Where(t => t["co_cd"].ToString() == coCd)?.CopyToDataTable();
                    if (dt != null)
                    {
                        foreach (DataRow row in dt.Rows)
                        {
                            Match match = Regex.Match(carNm, row["rx"].ToString(), rxOptM);
                            if (match.Success)
                            {
                                moCd = row["mo_cd"].ToString();
                                break;
                            }
                        }
                    }
                }
                if (trans != "")
                {
                    DataRow row = dtCarTrans.Rows.Cast<DataRow>().Where(t => t["nm"].ToString() == trans).SingleOrDefault();
                    transCd = (row == null) ? "" : row["cd"].ToString();
                }
                if (fuelType != "")
                {
                    DataRow row = dtCarFuel.Rows.Cast<DataRow>().Where(t => t["nm"].ToString() == fuelType).SingleOrDefault();
                    fuelCd = (row == null) ? "" : row["cd"].ToString();
                }
                if (coCd == "") coCd = "6";     //기타 제조사

                cvp = "dvsn=@dvsn, car_nm=@car_nm, car_type=@car_type, reg_no=@reg_no, car_year=@car_year, cmpy=@cmpy, fuel=@fuel, trans=@trans, mtr=@mtr, aprv_no=@aprv_no, id_no=@id_no, dspl=@dspl, dist=@dist, park=@park, " +
                    "co_cd=@co_cd, mo_cd=@mo_cd, color=@color, term=@term";
                sql = "insert into ta_cars set tid=@tid, ls_no=@ls_no, " + cvp;
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@ls_no", lsNo));
                sp.Add(new MySqlParameter("@dvsn", 1));
                //sp.Add(new MySqlParameter("@apsl_amt", apslAmt));
                sp.Add(new MySqlParameter("@car_nm", carNm));
                sp.Add(new MySqlParameter("@car_type", carType));
                sp.Add(new MySqlParameter("@reg_no", regNo));
                sp.Add(new MySqlParameter("@car_year", carYear));
                sp.Add(new MySqlParameter("@cmpy", cmpy));
                sp.Add(new MySqlParameter("@mtr", mtr));
                sp.Add(new MySqlParameter("@aprv_no", aprvNo));
                sp.Add(new MySqlParameter("@id_no", idNo));
                sp.Add(new MySqlParameter("@dspl", dspl));
                sp.Add(new MySqlParameter("@dist", dist));
                sp.Add(new MySqlParameter("@park", park));
                sp.Add(new MySqlParameter("@color", color));
                sp.Add(new MySqlParameter("@term", term));
                sp.Add(new MySqlParameter("@co_cd", coCd));
                sp.Add(new MySqlParameter("@mo_cd", moCd));
                sp.Add(new MySqlParameter("@trans", transCd));
                sp.Add(new MySqlParameter("@fuel", fuelCd));
                db.Open();
                db.ExeQry(sql, sp);
                sp.Clear();
                db.Close();
            }

            if (park != "" && preNtFlag == false)   //선행공고가 아닌 경우만 주소관련 업데이트
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
                sql= "update ta_list set adrs=@adrs, adrs_type=@adrs_type, regn_adrs=@regn_adrs, mt=@mt, m_adrs_no=@m_adrs_no, s_adrs_no=@s_adrs_no, road_adrs=@road_adrs, m_bldg_no=@m_bldg_no, s_bldg_no=@s_bldg_no, bldg_nm=@bldg_nm, road_nm=@road_nm, " +
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

            if (pdNote != "")
            {
                sql = "update ta_dtl set pd_note='" + pdNote + "' where tid='" + tid + "'";
                db.Open();
                db.ExeQry(sql);
                db.Close();
            }
        }

        /// <summary>
        /// 물건상세Sub-목록내역(선박)
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="doc"></param>
        private void PrcDtlSub_Ship(string tid, HAPDoc doc)
        {
            string sql, lsNo;
            string shipDtl, shipType, shipNm, shipNo, shipMatl, shipWt, launchDt, prpl, mtr, park;

            List<MySqlParameter> sp = new List<MySqlParameter>();
            HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='목록내역 표']/tbody/tr");
            if (ncTr == null) return;

            sql = "delete from ta_cars where tid=" + tid;
            db.Open();
            db.ExeQry(sql);
            db.Close();
            foreach (HtmlNode tr in ncTr)
            {
                HtmlNodeCollection ncTd = tr.SelectNodes("./td");
                if (ncTd.Count != 3) continue;

                lsNo = ncTd[0].InnerText.Trim();
                shipDtl = ncTd[2].InnerText.Trim();
                shipType = Regex.Match(shipDtl, @"선박의[\s]*종류와[\s]*명칭 : (\w+선)\s+(\w+)", rxOptM).Groups[1].Value.Trim();
                shipNm = Regex.Match(shipDtl, @"선박의[\s]*종류와[\s]*명칭 : (\w+선)\s+(\w+)", rxOptM).Groups[2].Value.Trim();
                shipNo = Regex.Match(shipDtl, @"어선번호 : ([\d\-]+)", rxOptM).Groups[1].Value.Trim();
                shipMatl = Regex.Match(shipDtl, @"선질 : ([\w\s]+)", rxOptM).Groups[1].Value.Trim();
                shipWt = Regex.Match(shipDtl, @"총톤수 : ([\d\.]+)", rxOptM).Groups[1].Value.Trim() + "톤";
                mtr = Regex.Match(shipDtl, @"기관의[\s]*종류와[\s]*수 : ([\w\.\s]+)", rxOptM).Groups[1].Value.Trim();
                prpl = Regex.Match(shipDtl, @"추진기의[\s]*종류와[\s]*수 : ([\w\.\s]+)", rxOptM).Groups[1].Value.Trim();
                launchDt = Regex.Match(shipDtl, @"진수년월일 : (\d+년\d+월\d+일)", rxOptM).Groups[1].Value.Trim();
                if (launchDt != string.Empty) launchDt = getDateParse(launchDt);
                park = Regex.Match(shipDtl, @"정박지[\s]*또는[\s]*보관장소 : ([\w\s]+)", rxOptM).Groups[1].Value.Trim();

                sql = "insert into ta_cars (tid, ls_no, dvsn, car_nm, car_type, reg_dt, aprv_no, id_no, mtr, dspl, prpl, park) values (@tid, @ls_no, @dvsn, @car_nm, @car_type, @reg_dt, @aprv_no, @id_no, @mtr, @dspl, @prpl, @park)";
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@ls_no", lsNo));
                sp.Add(new MySqlParameter("@dvsn", 2));
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
            }
        }

        /// <summary>
        /// 물건상세Sub-목록내역(항공기)
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="doc"></param>
        private void PrcDtlSub_Air(string tid, HAPDoc doc)
        {
            //
        }

        /// <summary>
        /// 물건상세Sub-목록내역(이륜차)
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="doc"></param>
        private void PrcDtlSub_Bike(string tid, HAPDoc doc)
        {
            //
        }

        /// <summary>
        /// 물건상세Sub-목록내역(어업권)
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="doc"></param>
        /// <param name="tblApsl"></param>
        private void PrcDtlSub_Fish(string tid, HAPDoc doc, HtmlNode tblApsl, string note)
        {
            string sql, apslTxt, cvp;
            string lsNo, lsType, tmpStr, licenseNo = "", licenseDt = "", licenseTerm = "", fisheryNm = "", fisheryTime = "", fisheryMtd = "", sqmStr = "", shrStr = "";
            string pdNote = "", loca = "", etc = "";
            double totSqm = 0, shrSqm = 0;

            StringBuilder sb = new StringBuilder();
            if (note != string.Empty)
            {
                sb.AppendLine(note);
            }

            if (tblApsl != null)
            {
                apslTxt = tblApsl.InnerText;
                if (apslTxt.Contains("어업권감정평가요항표"))
                {
                    HtmlNode nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'2) 입지조건')]]");
                    if (nd != null)
                    {
                        sb.AppendLine(nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim());
                    }
                    nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'7) 기타참고사항')]]");
                    if (nd != null)
                    {
                        etc = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                        if (!etc.Contains("없습니다"))
                        {
                            sb.AppendLine(etc);
                        }
                    }
                    pdNote = sb.ToString().Trim();
                }
            }

            HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='목록내역 표']/tbody/tr");
            if (ncTr == null) return;

            List<MySqlParameter> sp = new List<MySqlParameter>();

            sql = "delete from ta_cars where tid=" + tid;
            db.Open();
            db.ExeQry(sql);
            db.Close();
            foreach (HtmlNode tr in ncTr)
            {
                HtmlNodeCollection ncTd = tr.SelectNodes("./td");
                if (ncTd.Count != 3) continue;
                lsNo = ncTd[0].InnerText.Trim();
                lsType = ncTd[1].InnerText.Trim();
                tmpStr = ncTd[2].InnerHtml;
                if (lsType != "어업권") continue;

                licenseNo = Regex.Match(tmpStr, @"[면먼]허번호[\s:]*(\w+)", rxOptM).Groups[1].Value.Trim();
                Match match = Regex.Match(tmpStr.Replace(" ", string.Empty), @"[면먼]허년월일[\s:]*(\d{4}).(\d{1,2}).(\d{1,2})[.일]{0,1}", rxOptM);
                licenseDt = string.Format("{0}-{1}-{2}", match.Groups[1].Value, match.Groups[2].Value.PadLeft(2, '0'), match.Groups[3].Value.PadLeft(2, '0'));

                //match = Regex.Match(tmpStr.Replace(" ", string.Empty), @"[면먼]허기간[\s:]*(\d{4}.\d{1,2}.\d{1,2})[.일]{0,1}[\s\-\~\∼]*(\d{4}.\d{1,2}.\d{1,2})[.일]{0,1}", rxOptM);
                match = Regex.Match(tmpStr, @"[어업권의존속기간\s]{8,}[ ]*:[ ]*(\d{4}[년.\s]+\d{1,2}[월.\s]+\d{1,2})[.일]{0,1}[부터\s\-\~\∼]*(\d{4}[년.\s]+\d{1,2}[월.\s]+\d{1,2})[.일]{0,1}", rxOptM);
                licenseTerm = string.Format("{0}~{1}", match.Groups[1].Value, match.Groups[2].Value);
                licenseTerm = Regex.Replace(licenseTerm, @"[\-년월]", ".");
                licenseTerm = licenseTerm.Replace(" ", string.Empty);

                fisheryNm = Regex.Match(tmpStr, @"어업및어구의명칭[\s:]*([\w\s]*)", rxOptM).Groups[1].Value.Trim();
                fisheryTime = Regex.Match(tmpStr, @"어업의시기[\s:]*([\w\s]*)", rxOptM).Groups[1].Value.Trim();
                fisheryMtd = Regex.Match(tmpStr, @"어업의방법[\s:]*([\w\s]*)", rxOptM).Groups[1].Value.Trim();

                match = Regex.Match(tmpStr, @"어장면적[\s:]*([\d.]*)(\w+)", rxOptM);
                if (match.Groups[1].Value != string.Empty)
                {
                    try
                    {
                        totSqm = Convert.ToDouble(match.Groups[1].Value);
                        if (match.Groups[2].Value.ToLower().Contains("ha"))
                        {
                            totSqm *= 10000;
                        }
                        match = Regex.Match(tmpStr, @"지분[\s]*([\d.]*)[\s]*분의[\s]*([\d.]*)", rxOptS);
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

                sql = "insert into ta_cars set dvsn=4, tid=@tid, ls_no=@ls_no, reg_no=@reg_no, reg_dt=@reg_dt, term=@term, car_nm=@car_nm, id_no=@id_no, mtr=@mtr, dist=@dist";
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@ls_no", lsNo));
                sp.Add(new MySqlParameter("@reg_no", licenseNo));
                sp.Add(new MySqlParameter("@reg_dt", licenseDt));
                sp.Add(new MySqlParameter("@term", licenseTerm));
                sp.Add(new MySqlParameter("@car_nm", fisheryNm));
                sp.Add(new MySqlParameter("@id_no", fisheryTime));
                sp.Add(new MySqlParameter("@mtr", fisheryMtd));
                sp.Add(new MySqlParameter("@dist", string.Format("{0}㎡", shrSqm)));
                db.Open();
                db.ExeQry(sql, sp);
                sp.Clear();
                db.Close();
            }

            if (pdNote != "")
            {
                sql = "update ta_dtl set pd_note='" + pdNote + "' where tid='" + tid + "'";
                db.Open();
                db.ExeQry(sql);
                db.Close();
            }
        }

        /// <summary>
        /// 물건상세Sub-목록내역(광업권)
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="doc"></param>
        /// <param name="tblApsl"></param>
        private void PrcDtlSub_Mine(string tid, HAPDoc doc, HtmlNode tblApsl)
        {
            //
        }

        /// <summary>
        /// 현황조사서 수집-임대차 관계
        /// - 선행공고 통합
        /// </summary>
        private void Prc_StatIvst()
        {
            int totCnt = 0, curCnt = 0;
            string sql = "", url = "", jiwonNm = "", saNo = "", html = "";
            string tid = "", lsNo = "", adrs = "", lsDvsn = "", etc = "", prsn = "", invType = "", part = "", useType = "", term = "", deposit = "", fee = "", mvDt = "", fxDt = "", useCd = "", biz = "", cat = "", note = "";
            ArrayList alEtc = new ArrayList();
            bool landOnly = false;

            progrsView("현황조사(임대차 관계)");   //진행상태

            sql = "select tid,crt,spt,sn1,sn2,pn,cat3 from ta_list where 2nd_dt=curdate() and auto_prc=2 order by crt,spt,sn1,sn2";
            DataTable dtList = db.ExeDt(sql);
            totCnt = dtList.Rows.Count;

            DataTable dtT = new DataTable();
            dtT.Columns.Add("lsNo");
            dtT.Columns.Add("prsn");
            dtT.Columns.Add("invType");
            dtT.Columns.Add("part");
            dtT.Columns.Add("useType");
            dtT.Columns.Add("term");
            dtT.Columns.Add("deposit");
            dtT.Columns.Add("fee");
            dtT.Columns.Add("mvDt");
            dtT.Columns.Add("fxDt");

            DataTable dtLs = new DataTable();

            HAPDoc doc = new HAPDoc();
            List<MySqlParameter> sp = new List<MySqlParameter>();
            foreach (DataRow row in dtList.Rows)
            {
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                dtT.Rows.Clear();
                alEtc.Clear();

                progrsView(string.Format("[현황조사] {0}-{1} -> {2} / {3}", row["sn1"], row["sn2"], curCnt, totCnt), 1);  //진행상태

                tid = row["tid"].ToString();
                cat = row["cat3"].ToString();

                db.Open();
                sql = "update ta_list set auto_prc=3 where tid='" + tid + "'";
                db.ExeQry(sql);
                db.Close();

                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                //url = "http://www.courtauction.go.kr/RetrieveRealEstSaHjosa.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&ordHoi=1";
                url = "http://www.courtauction.go.kr/RetrieveRealEstSaHjosa.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo;
                html = net.GetHtml(url);
                if (html.Contains("현황조사서가 없습니다")) continue;

                doc.LoadHtml(html);
                HtmlNodeCollection ncTbl = doc.DocumentNode.SelectNodes("//table[@summary='임차 목적물의 용도 및 임대차 계약등의 내용 표 ']");
                //if (ncTbl == null) continue;

                sql = "select no,dvsn from ta_ls where tid='" + tid + "'";
                dtLs = db.ExeDt(sql);
                if (dtLs.Rows.Count == 0) continue;

                landOnly = (dtLs.Select("dvsn='토지'").Count() == dtLs.Rows.Count) ? true : false;
                
                if (ncTbl != null)
                {
                    foreach (HtmlNode ndTbl in ncTbl)
                    {
                        prsn = ""; invType = ""; part = ""; useType = ""; term = ""; deposit = ""; fee = ""; mvDt = ""; fxDt = "";
                        HtmlNodeCollection ncTr = ndTbl.SelectNodes("./tr");

                        foreach (HtmlNode tr in ncTr)
                        {
                            HtmlNodeCollection ncTd = tr.SelectNodes("./th|./td");
                            foreach (HtmlNode td in ncTd)
                            {
                                if (td.InnerText.Contains("[소재지]")) lsNo = Regex.Match(td.InnerText, @"\[소재지\]\s+(\d+)\.", RegexOptions.Multiline).Groups[1].Value;
                                if (td.InnerText == "점유인") prsn = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                                if (td.InnerText == "당사자구분") invType = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                                if (td.InnerText == "점유부분") part = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                                if (td.InnerText == "용도") useType = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                                if (td.InnerText == "점유기간") term = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                                if (td.InnerText == "보증(전세)금") deposit = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                                if (td.InnerText == "차임") fee = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                                if (td.InnerText == "전입일자") mvDt = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                                if (td.InnerText == "확정일자") fxDt = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                            }
                        }
                        if (dtLs.Select("no='" + lsNo + "'").Count() == 0) continue;

                        DataRow dr = dtT.NewRow();
                        dr["lsNo"] = lsNo;
                        dr["prsn"] = prsn;
                        dr["invType"] = invType;
                        dr["part"] = part;
                        dr["useType"] = useType;
                        dr["term"] = term;
                        dr["deposit"] = MoneyChk(deposit);
                        dr["fee"] = MoneyChk(fee);
                        dr["mvDt"] = DtChk(mvDt);
                        dr["fxDt"] = DtChk(fxDt);
                        dtT.Rows.Add(dr);
                    }
                }

                ncTbl = doc.DocumentNode.SelectNodes("//table[@summary='부동산의 점유관계 표']");
                if (ncTbl != null)
                {
                    foreach (HtmlNode ndTbl in ncTbl)
                    {
                        prsn = ""; invType = ""; part = ""; useType = ""; term = ""; deposit = ""; fee = ""; mvDt = ""; fxDt = "";
                        HtmlNodeCollection ncTr = ndTbl.SelectNodes("./tr");

                        foreach (HtmlNode tr in ncTr)
                        {
                            HtmlNodeCollection ncTd = tr.SelectNodes("./th|./td");
                            foreach (HtmlNode td in ncTd)
                            {
                                if (td.InnerText == "소재지")
                                {
                                    adrs = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                                    lsNo = Regex.Match(adrs, @"^(\d+)\.", RegexOptions.Multiline).Groups[1].Value;
                                }
                                if (td.InnerText == "점유관계") useType = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                                if (td.InnerText == "기타")
                                {
                                    etc = td.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                                }
                            }
                        }
                        if (dtLs.Select("no='" + lsNo + "'").Count() > 0)
                        {
                            if (!alEtc.Contains(etc)) alEtc.Add(etc);
                        }
                        
                        if (dtLs.Select("no='" + lsNo + "'").Count() == 0) continue;

                        lsDvsn = dtLs.Select("no='" + lsNo + "'")[0]["dvsn"].ToString();
                        if (landOnly == false)
                        {
                            if (!lsDvsn.Contains("건물")) continue;
                        }
                                                
                        if (dtT.Select("lsNo='" + lsNo + "'").Count() > 0) continue;

                        DataRow dr = dtT.NewRow();
                        dr["lsNo"] = lsNo;
                        dr["prsn"] = "";
                        dr["invType"] = "";
                        dr["part"] = "";
                        dr["useType"] = useType;
                        dr["term"] = "";
                        dr["deposit"] = "";
                        dr["fee"] = "";
                        dr["mvDt"] = "";
                        dr["fxDt"] = "";
                        dtT.Rows.Add(dr);          
                    }
                }

                db.Open();
                if (alEtc.Count > 0)
                {
                    etc = string.Join("\r\n", alEtc.ToArray());
                    etc = Regex.Replace(etc, @"\t", string.Empty);
                    etc = Regex.Replace(etc, @"\s{2,}", "\r\n").Trim();
                    sql = "update ta_dtl set leas_note=trim(concat(leas_note,'" + etc + "')) where tid='" + tid + "'";
                    db.ExeQry(sql);
                }

                string[] bizArr = new string[] { "2", "3", "8", "9" };  //점포, 사무, 공장, 영업         
                List<string> lsNote = new List<string>();
                foreach (DataRow data in dtT.Rows)
                {
                    if (dtLs.Select("no='" + data["lsNo"].ToString() + "'").Count() == 0) continue;

                    useType = data["useType"].ToString().Replace(" ", string.Empty).Trim();

                    lsNote.Clear();
                    note = string.Empty;
                    decimal decDeposit = 0;
                    decimal decFee = 0;

                    bool isNumDeposit = decimal.TryParse(data["deposit"].ToString(), out decDeposit);
                    if (!isNumDeposit) lsNote.Append(string.Format("보:{0}", data["deposit"]));

                    bool isNumFee = decimal.TryParse(data["fee"].ToString(), out decFee);
                    if (!isNumFee) lsNote.Append(string.Format("차:{0}", data["fee"]));

                    if (lsNote.Count > 0) note = string.Join(",", lsNote.ToArray());

                    if (useType == "" || useType == "미상") useCd = "10";
                    else if (useType == "채무자(소유자)점유") useCd = "7";
                    else if (useType == "주거") useCd = "1";
                    else if (useType == "점포") useCd = "2";
                    else if (useType == "공장") useCd = "8";
                    else if (useType == "주거및점포") useCd = "4";
                    else if (useType == "사무실") useCd = "3";
                    else if (useType == "토지") useCd = "13";
                    else if (useType == "기타-미상")
                    {
                        if (cat == "201013" || cat == "201014" || cat == "201015") useCd = "1";
                    }
                    else useCd = "0";

                    biz = (bizArr.Contains(useCd)) ? "1" : "0";
                    sql = "insert into ta_leas (tid, ls_no, prsn, inv_type, part, use_type, use_cd, term, deposit, m_money, mv_dt, fx_dt, biz, note) ";
                    sql += "values (@tid, @ls_no, @prsn, @inv_type, @part, @use_type, @use_cd, @term, @deposit, @m_money, @mv_dt, @fx_dt, @biz, @note)";
                    sp.Add(new MySqlParameter("@tid", tid));
                    sp.Add(new MySqlParameter("@ls_no", data["lsNo"]));
                    sp.Add(new MySqlParameter("@prsn", data["prsn"]));
                    sp.Add(new MySqlParameter("@inv_type", data["invType"]));
                    sp.Add(new MySqlParameter("@part", data["part"]));
                    sp.Add(new MySqlParameter("@use_type", data["useType"]));
                    sp.Add(new MySqlParameter("@use_cd", useCd));
                    sp.Add(new MySqlParameter("@term", data["term"]));
                    sp.Add(new MySqlParameter("@deposit", decDeposit));
                    sp.Add(new MySqlParameter("@m_money", decFee));
                    sp.Add(new MySqlParameter("@mv_dt", data["mvDt"]));
                    sp.Add(new MySqlParameter("@fx_dt", data["fxDt"]));
                    sp.Add(new MySqlParameter("@biz", biz));
                    sp.Add(new MySqlParameter("@note", note));
                    db.ExeQry(sql, sp);
                    sp.Clear();
                }
                db.Close();
            }
        }

        /// <summary>
        /// 등기 다운/업/추출(굿옥션)-사용안함
        /// </summary>
        private void Prc_RgstAnaly()
        {
            return;

            int i = 0, curCnt = 0, totCnt = 0;
            string sql, url, jsData, gdLawCd, spt, sn, sn1, sn2, pn, tid;
            string ctgr, fileNm, fileUrl, locFile, rmtFile, tbl, cvp;
            string rgstDnPath, tkFileNm, errMsg, spRgst;
            //bool analyFlag = false;

            rgstDnPath = filePath + @"\등기";
            if (!Directory.Exists(rgstDnPath))
            {
                Directory.CreateDirectory(rgstDnPath);
            }

            progrsView("등기수집");   //진행상태

            sql = "select spt_cd, _gd_cd from ta_cd_cs";
            DataTable dtCs = db.ExeDt(sql);

            sql = "select tid,spt,sn1,sn2,pn,sp_rgst from ta_list where 2nd_dt=curdate() and pre_dt='0000-00-00' and cat1 in (10,20) order by tid";
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
                        errMsg = "파일정보 없음";
                        atomLog.AddLog(string.Format("TID-{0} : {1}", tid, errMsg));

                        sql = "insert ignore into db_tank.tx_rgst_err set tid='" + tid + "', dvsn=1, wdt=curdate()";
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
                }
                catch (Exception ex)
                {
                    atomLog.AddLog(string.Format("TID-{0} : {1}", tid, ex.Message));
                }
            }
        }

        /// <summary>
        /// 등기 자동발급 대상 추가
        /// </summary>
        private void Prc_RgstIssueAdd()
        {
            string sql, tid, tbl, prevTid = "";
            bool fileExist, autoExist;
            int landCnt = 0, bldgCnt = 0, multiCnt = 0, issueCnt = 0;
            string autoDvsn = "11";     //발급 구분 -> 일반 공고

            progrsView("등기 자동발급 대상");   //진행상태

            DataTable dt = new DataTable();
            dt.Columns.Add("tid");
            dt.Columns.Add("lsIdx");
            dt.Columns.Add("lsNo");
            dt.Columns.Add("lsType");
            dt.Columns.Add("pin");

            //사건별 물건번호 최대값 산출
            sql = "select max(pn) as maxPN,L.tid,spt,sn1,sn2,pn,cat3, S.idx,no,dvsn,pin,pnu from ta_list L, ta_ls S where L.tid=S.tid and sta2=1110 and cat1!=30 and 2nd_dt=curdate() and pre_dt='0000-00-00' GROUP by spt,sn1,sn2 HAVING maxPN > 20";
            DataTable dtMax = db.ExeDt(sql);

            sql = "select L.tid,spt,sn1,sn2,cat3,pn, 0 as 'maxPN', S.idx,no,dvsn,pin,pnu from ta_list L, ta_ls S where L.tid=S.tid and sta2=1110 and cat1!=30 and 2nd_dt=curdate() and pre_dt='0000-00-00' and works=0 and S.note='미종국' order by L.tid";
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
                                dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["dvsn"], r["pin"] });    //대상
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
        /// 파일수집-사진/감정평가서
        /// - 선행공고 통합(감정평가서만 -> 사진은 제외)
        /// </summary>
        private void Prc_PhotoFile()
        {
            int i = 0, curCnt = 0, totCnt = 0;
            string sql, tbl, cvp, url, jiwonNm, spt, dpt, sn, sn1, sn2, bidDt, preDt, saNo, pn, html, html0, alt, dir, ctgr, year, fileNm, locFile, rmtFile, thumb, locThumbFile, rmtThumbFile, seq;
            string photoNo, dtlUrl, photoSrc, photoNote;
            bool photoExist = false;

            dir = filePath + @"\사진감평";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            progrsView("파일수집-사진/감정평가서");   //진행상태

            sql = "select tid,crt,spt,dpt,sn1,sn2,pn,bid_dt,pre_dt from ta_list where 2nd_dt=curdate() and auto_prc in (2,3) group by crt,spt,sn1,sn2 order by spt,dpt,sn1,sn2";
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

                progrsView(string.Format("[사진감평] {0}-{1} -> {2} / {3}", row["sn1"], row["sn2"], curCnt, totCnt), 1);     //진행상태

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
                preDt = row["pre_dt"].ToString();

                //물건 사진(B*)-해당 사건이 최초 본물건인 경우만 수집한다.
                //sql = "select tid from ta_list where spt=" + spt + " and sn1=" + sn1 + " and sn2=" + sn2 + " and sta1 > 10 and 2nd_dt < curdate()"; // [and sta1 > 10] 추가(2021/08/03)
                sql = "select tid from ta_list where spt=" + spt + " and sn1=" + sn1 + " and sn2=" + sn2 + " and sta1 > 10 and ((2nd_dt > '0000-00-00' and 2nd_dt < curdate()) or (pre_dt > '0000-00-00' and pre_dt < curdate()))";
                db.Open();
                MySqlDataReader dr = db.ExeRdr(sql);
                photoExist = dr.HasRows;
                dr.Close();
                db.Close();
                if (photoExist == false && preDt.Contains("0001") == true)  //선행공고로 등록건은 제외
                {
                    url = "https://www.courtauction.go.kr/RetrieveRealEstCarHvyMachineMulDetailInfo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&maemulSer=" + pn;
                    html = net.GetHtml(url);
                    doc.LoadHtml(html);

                    HtmlNode tblPhoto = doc.DocumentNode.SelectSingleNode("//table[@summary='물건기본정보 사진정보 표']");
                    if (tblPhoto == null) continue;
                    HtmlNodeCollection ncImg = tblPhoto.SelectNodes(".//li/div/a/img");
                    if (ncImg == null) continue;

                    dtlUrl = "http://www.courtauction.go.kr/RetrieveSaPhotoInfo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&boGbn=B&boCd=B000240&pageSpec=photoPage&targetRow=";

                    foreach (HtmlNode ndImg in ncImg)
                    {
                        i++;
                        webCnt++;
                        photoNo = ""; photoSrc = ""; photoNote = "";

                        if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                        HtmlNode ndA = ndImg.ParentNode;
                        string clickStr = ndA.GetAttributeValue("onclick", "null");
                        MatchCollection mc = Regex.Matches(clickStr, @"'(.*?)'", rxOptM);
                        photoNo = mc[2].Groups[1].Value;

                        alt = ndImg.GetAttributeValue("alt", "null");
                        url = ndImg.GetAttributeValue("src", "null");
                        if (alt.Contains("전경도")) ctgr = "BA";
                        else if (alt.Contains("내부구조도")) ctgr = "BB";
                        else if (alt.Contains("위치도")) ctgr = "BC";
                        else if (alt.Contains("개황도")) ctgr = "BD";
                        else if (alt.Contains("관련사진")) ctgr = "BE";
                        else if (alt.Contains("지적도")) ctgr = "BF";
                        else if (alt.Contains("지번약도")) ctgr = "BG";
                        else ctgr = "BZ";  //기타

                        if (url == "null") continue;
                        url = "http://www.courtauction.go.kr" + url.Replace("&amp;", "&").Replace("T_", string.Empty);

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
                        html = net.GetHtml(dtlUrl + photoNo);
                        Match match = Regex.Match(html, @"<div class=""\w+"">사진출처\s+:\s+(.*?)</div>", rxOptM);
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

                //감정평가서(AF)
                dicHtml.Clear();
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                url = "http://www.courtauction.go.kr/RetrieveMobileEstSaGamEvalSeo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&orgSaNo=" + saNo + "&maemulSer=" + pn + "&maeGiil=" + bidDt + "&mGakMulMseoYN=Y&corCanYN=N&jpDeptCd=" + dpt;
                html0 = net.GetHtml(url);
                if (html0.Contains("잘못된 접근입니다") || html0.Contains("감정평가서가 없습니다"))
                {
                    //dnFailCnt++;
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
                        url = "http://www.courtauction.go.kr/RetrieveMobileEstSaGamEvalSeo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&orgSaNo=" + saNo + "&maemulSer=" + pn + "&maeGiil=" + bidDt + "&mGakMulMseoYN=Y&corCanYN=N&jpDeptCd=" + dpt + "&ordHoi=" + seq;
                        html = net.GetHtml(url);
                        if (html.Contains("잘못된 접근입니다") || html.Contains("감정평가서가 없습니다"))
                        {
                            //dnFailCnt++;
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
                    ctgr = "AF";
                    html = kvp.Value;
                    locFile = string.Format(@"{0}\{1}-{2}-{3}-{4}.pdf", dir, ctgr, spt, sn, kvp.Key.PadLeft(2, '0'));
                    if (File.Exists(locFile)) continue;

                    Match match = Regex.Match(html, @"downGamEvalSeo\('(.*)?'\)", rxOptM);
                    if (match.Success == false) continue;
                    url = match.Groups[1].Value;
                    html = net.GetHtml(url);
                    match = Regex.Match(html, @"'\/(.*)?'", RegexOptions.Multiline);
                    if (match.Success == false) continue;
                    url = match.Groups[1].Value;
                    Dictionary<string, string> apslRslt = net.DnFile(@"http://ca.kapanet.or.kr/" + url, locFile);
                    if (apslRslt["result"] == "fail") continue;

                    //FTP 업로드
                    if (!File.Exists(locFile))
                    {
                        //
                        continue;
                    }
                    FileInfo fi = new FileInfo(locFile);
                    if ((fi.Length / 1024) < 50)    //50KB 작다면 오류로 판단하여 DB에 기록
                    {
                        sql = "insert ignore into db_tank.tx_apsl_err set spt='" + spt + "', sn1='" + sn1 + "', sn2='" + sn2 + "', seq='" + kvp.Key + "', wdt=curdate()";
                        db.Open();
                        db.ExeQry(sql);
                        db.Close();
                        continue;
                    }

                    match = Regex.Match(locFile, @"[\w\d\-]*.pdf$", rxOptM);
                    fileNm = match.Value;
                    if (match.Success == false)
                    {
                        //
                        continue;
                    }
                    rmtFile = string.Format("{0}/{1}/{2}/{3}", ctgr, spt, year, fileNm);
                    if (ftp1.Upload(locFile, rmtFile))
                    {
                        //DB 처리
                        tbl = (Convert.ToDecimal(year) > 2004) ? ("ta_f" + year) : "ta_f2004";
                        cvp = "ctgr='" + ctgr + "', spt='" + spt + "', sn='" + sn + "', file='" + fileNm + "', wdt=curdate()";
                        sql = "insert into " + tbl + " set " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                        db.Open();
                        db.ExeQry(sql);
                        db.Close();
                    }
                }
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
            //string fullNm = string.Format(@"{0}\{1}", filePath, fileNm);
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
                    //thumb.Save(string.Format(@"{0}\_thumb\{1}", filePath, fileNm));
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
        /// - 선행공고 통합(문서 전부 업데이트 -> 현황조사, 물건상세는 신규)
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

            //현황조사
            PrcFileSub_StatIvst(dir, "AD");

            //표시목록
            PrcFileSub_ReList(dir, "AE");

            //물건상세
            PrcFileSub_PdDtl(dir, "AJ");
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

            cdtn = "sta1=11 and 2nd_dt=curdate()";
            //cdtn = "spt='1610' and sta1=11 and 2nd_dt='2021-06-01'";
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

            cdtn = "sta1=11 and 2nd_dt=curdate()";
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

            cdtn = "sta1=11 and 2nd_dt=curdate()";
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
        /// 파일 수집Sub-현황조사
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="v"></param>
        private void PrcFileSub_StatIvst(string dir, string ctgr)
        {
            string tid, sql, cvp, cdtn, url, jiwonNm, saNo, html0, html, locFile, seq, rmtFile, spt, year, sn, fileNm, tbl;
            decimal totCnt = 0, curCnt = 0, dlCnt = 0, ulCnt = 0, dnFailCnt = 0;
            string stripTag = @"[</]+(a|img).*?>";
            HAPDoc doc = new HAPDoc();
            Dictionary<string, string> dicHtml = new Dictionary<string, string>();

            cdtn = "sta1=11 and 2nd_dt=curdate() and (cat1 IN (10,20) or cat2=3012)";  //현황조사서는 토지, 건물, 선박만 제공
            sql = "select tid, spt, dpt, sn1, sn2, pn, bid_dt from ta_list where " + cdtn + " group by spt, sn1, sn2 order by tid";
            DataTable dt = db.ExeDt(sql);

            totCnt = dt.Rows.Count;
            atomLog.AddLog(string.Format(" > 현황조사서 수집시작 대상-{0}", totCnt));

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                dicHtml.Clear();
                tid = row["tid"].ToString();
                progrsView(string.Format("[현황조사] TID -> {0} ^ {1} / {2}", tid, curCnt, totCnt), 1);  //진행상태

                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                url = "http://www.courtauction.go.kr/RetrieveRealEstSaHjosa.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo;
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

                        url = "http://www.courtauction.go.kr/RetrieveRealEstSaHjosa.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&ordHoi=" + seq;
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
                            if (nd.GetAttributeValue("summary", "") == "현황조사서 기본내역 표" || nd.InnerText.Contains("사진정보"))
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

            cdtn = "sta1=11 and 2nd_dt=curdate() and (cat1 IN (10,20) or cat2=3012)";  //현황조사서는 토지, 건물, 선박만 제공
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
        /// 파일 수집Sub-물건상세
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="v"></param>
        private void PrcFileSub_PdDtl(string dir, string ctgr)
        {
            string tid, sql, cvp, cdtn, url, jiwonNm, saNo, pn, html, locFile, rmtFile, spt, year, sn, fileNm, tbl;
            decimal totCnt = 0, curCnt = 0, dlCnt = 0, ulCnt = 0, dnFailCnt = 0;
            string stripTag = @"[</]+(a|img).*?>";
            HAPDoc doc = new HAPDoc();

            cdtn = "sta1=11 and 2nd_dt=curdate()";
            sql = "select tid, spt, dpt, sn1, sn2, pn, bid_dt from ta_list where " + cdtn + " order by tid";
            DataTable dt = db.ExeDt(sql);

            totCnt = dt.Rows.Count;
            atomLog.AddLog(string.Format(" > 물건상세 수집시작 대상-{0}", totCnt));  //로그기록

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                tid = row["tid"].ToString();
                progrsView(string.Format("[물건상세] TID -> {0} ^ {1} / {2}", tid, curCnt, totCnt), 1);  //진행상태

                locFile = string.Format(@"{0}\{1}-{2}-{3}{4}-{5}.html", dir, ctgr, row["spt"], row["sn1"], row["sn2"].ToString().PadLeft(6, '0'), row["pn"].ToString().PadLeft(4, '0'));
                if (File.Exists(locFile)) continue;

                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                pn = (row["pn"].ToString() == "0") ? "1" : row["pn"].ToString();
                url = "https://www.courtauction.go.kr/RetrieveRealEstCarHvyMachineMulDetailInfo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&maemulSer=" + pn;
                html = net.GetHtml(url);
                doc.LoadHtml(html);
                HtmlNodeCollection nc = doc.DocumentNode.SelectNodes("//div[@class='table_title']|//table[@class='Ltbl_dt' or @class='Ltbl_list']");
                if (nc != null)
                {
                    //var nodeList = new List<string>(nc.Select(node => node.OuterHtml));
                    var nodeList = new List<string>(nc.Where<HtmlNode>(t => t.InnerHtml.Contains("사진정보") == false && t.InnerHtml.Contains("인근매각") == false).Select(node => node.OuterHtml));

                    foreach (string str in nodeList)
                    {
                        //
                    }
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
                    cvp = "ctgr='" + ctgr + "', spt='" + spt + "', tid='" + tid + "', sn='" + sn + "', file='" + fileNm + "', wdt=curdate()";
                    sql = "insert into " + tbl + " set " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                    PrcFileSub_DB(sql);
                }
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

            sql = "select L.tid, L.idx, L.prps_nm, S.pnu from ta_list T, ta_ls S, ta_land L where T.tid=S.tid and S.tid=L.tid and S.no=L.ls_no and sta1=11 and plan_prc in (0,2) and cat3 not in (0,201013,201014,201015,201017,201019,201022,201130,201216,201123,201020,201111) and 2nd_dt=curdate() and pre_dt='0000-00-00'";
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

            sql = "select L.tid, L.idx, S.pnu from ta_list T, ta_ls S, ta_land L where T.tid=S.tid and S.tid=L.tid and S.no=L.ls_no and sta1=11 and price_prc in (0,2) and cat3 not in (0,201013,201014,201015,201017,201019,201022,201130,201216,201123,201020,201111) and 2nd_dt=curdate() and pre_dt='0000-00-00'";
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

            sql = "select tid, x, y from ta_list where 2nd_dt=curdate() and pre_dt='0000-00-00' and x > 0 and station_prc=0 order by tid";
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

            sql = "select * from ta_list where 2nd_dt=curdate() and pre_dt='0000-00-00' and apt_cd=0 and cat3 in (201013,201015,201020,201111,201123,201130,201216)";  //아파트, 다세대주택, 오피스텔(주거), 오피스텔(상업), 숙박(콘도)등, 근린상가, 지식산업센터(아파트형공장)
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
        /// 사용승인일자,엘리베이터-국토교통부_건축물대장 표제부 조회
        /// </summary>
        private void Prc_AprvDt()
        {
            int totalCnt, mvCnt = 0, eqCnt = 0;
            string sql, url, xml, serviceKey, tid, lsNo, adrs0, adrs, pnu, platGbCd, bun, ji, newPlatPlc, aprvDt, flrCnt, dongNm, elvtCnt, idx;

            progrsView("[사용승인일자]");
            //DataTable dtTest = new DataTable();
            //dtTest.Columns.Add("idx");
            //dtTest.Columns.Add("aprvDt");

            Dictionary<string, string> dic = new Dictionary<string, string>();  //승인일자, 총층수
            Dictionary<string, string> dic2 = new Dictionary<string, string>(); //동명칭, 승인일자

            sql = "SELECT L.tid,S.no,S.pnu,S.adrs,B.tot_flr,B.elvt,B.idx from ta_list L , ta_ls S , ta_bldg B WHERE L.tid=S.tid and S.tid=B.tid and S.no=B.ls_no and L.2nd_dt=curdate() and L.pre_dt='0000-00-00' and S.dvsn in ('건물','집합건물') and B.dvsn=1";
            DataTable dt = db.ExeDt(sql);

            XmlDocument doc = new XmlDocument();

            serviceKey = api.RndSrvKey();

            foreach (DataRow row in dt.Rows)
            {
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

                sql = "update ta_bldg set aprv_dt='" + aprvDt + "', elvt='" + elvtCnt + "' where idx='" + idx + "' and tid='" + tid + "'";
                db.Open();
                db.ExeQry(sql);
                db.Close();
                mvCnt++;
            }
            //MessageBox.Show(dtTest.Rows.Count.ToString());
            atomLog.AddLog(string.Format(" > 매칭-{0}", mvCnt));
        }

        /// <summary>
        /// 감정평가서 OCR 연동
        /// </summary>
        private void Prc_ApslOcr()
        {
            string today, path, html, locFile, rmtFile, fileNm;
            string ctgr, spt, sn1, sn2, sn, year, seq, tbl, cvp, sql;
            string apslNm, apslDt;
            int totCnt = 0, curCnt = 0, sucCnt = 0;

            today = DateTime.Now.ToShortDateString();
            path = @"C:\Atom\CA\" + today + @"\사진감평";
            if (Directory.Exists(path) == false) return;

            string[] files = Directory.GetFiles(path, "*.pdf");
            totCnt = files.Count();
            
            ProcessStartInfo psi = new ProcessStartInfo();

            ctgr = "EI";
            Regex rx = new Regex(@"AF\-(\d{4})\-(\d{4})(\d{6})\-(\d{2})", rxOptM);
            Match match;
            foreach (string pdf in files)
            {
                curCnt++;
                if (curCnt % 50 == 0) atomLog.AddLog($" > {curCnt} / {totCnt}");  //50개 처리때 마다 기록
                progrsView(string.Format("[감평서 OCR] {0} ^ {1} / {2}", pdf, curCnt, totCnt), 1);  //진행상태

                FileInfo fi = new FileInfo(pdf);
                if ((fi.Length / 1024) < 50)    //50KB 작다면 오류로 취급
                {
                    //
                    continue;
                }
                
                match = rx.Match(pdf);
                spt = match.Groups[1].Value;
                year = match.Groups[2].Value;
                sn1 = year;
                sn2 = match.Groups[3].Value;
                sn = match.Groups[2].Value + match.Groups[3].Value;
                seq = match.Groups[4].Value;
                
                html = pdf.Replace("AF-", "EI-");
                html = html.Replace(".pdf", ".html");
                try
                {
                    psi.FileName = @"C:\Program Files (x86)\ABBYY FineReader 15\FineCmd.exe";
                    psi.Arguments = string.Format("{0} /lang Korean English /out {1}", pdf, html);
                    var process = Process.Start(psi);
                    process.WaitForExit();
                }
                catch(Exception ex)
                {
                    atomLog.AddLog($" >{spt} {sn1}-{sn2} {ex.Message}");
                    continue;
                }

                locFile = html;
                match = Regex.Match(locFile, @"[\w\d\-]*.html$", rxOptM);
                fileNm = match.Value;
                if (match.Success == false)
                {
                    //
                    continue;
                }

                rmtFile = string.Format("{0}/{1}/{2}/{3}", ctgr, spt, year, fileNm);
                if (ftp1.Upload(locFile, rmtFile))
                {
                    //DB 처리
                    tbl = (Convert.ToDecimal(year) > 2004) ? ("ta_f" + year) : "ta_f2004";
                    cvp = "ctgr='" + ctgr + "', spt='" + spt + "', sn='" + sn + "', file='" + fileNm + "', wdt=curdate()";
                    sql = "insert into " + tbl + " set " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();
                    sucCnt++;
                }

                try
                {
                    //감평서 OCR-감정원, 가격시점
                    ApslNmDt(spt, sn1, sn2, html);

                    //감평서 OCR-단가, 평가액
                    DataTable dtL = db.ExeDt("select tid from ta_list where spt='" + spt + "' and sn1='" + sn1 + "' and sn2='" + sn2 + "' and 2nd_dt='" + today + "'");
                    foreach (DataRow row in dtL.Rows)
                    {
                        ApslPrice(row["tid"].ToString(), html);
                    }
                }
                catch
                {
                    continue;
                }
            }

            atomLog.AddLog(string.Format(" > OCR-{0}", sucCnt));
        }

        /// <summary>
        /// 감정평가서 OCR 연동-로컬테스트
        /// </summary>
        private void Prc_ApslOcrTest()
        {
            string today, path, html, locFile, rmtFile, fileNm;
            string ctgr, spt, sn1, sn2, sn, year, seq, tbl, cvp, sql;
            string apslNm, apslDt;
            int totCnt = 0, curCnt = 0, sucCnt = 0;

            //today = DateTime.Now.ToShortDateString();
            today = "2021-11-03";
            path = @"C:\Atom\CA\" + today + @"\사진감평";
            if (Directory.Exists(path) == false) return;

            string[] files = Directory.GetFiles(path, "*.pdf");
            totCnt = files.Count();

            ProcessStartInfo psi = new ProcessStartInfo();

            ctgr = "EI";
            Regex rx = new Regex(@"AF\-(\d{4})\-(\d{4})(\d{6})\-(\d{2})", rxOptM);
            Match match;
            foreach (string pdf in files)
            {
                curCnt++;
                progrsView(string.Format("[감평서 OCR] {0} ^ {1} / {2}", pdf, curCnt, totCnt), 1);  //진행상태

                FileInfo fi = new FileInfo(pdf);
                if ((fi.Length / 1024) < 50)    //50KB 작다면 오류로 취급
                {
                    //
                    continue;
                }

                match = rx.Match(pdf);
                spt = match.Groups[1].Value;
                year = match.Groups[2].Value;
                sn1 = year;
                sn2 = match.Groups[3].Value;
                sn = match.Groups[2].Value + match.Groups[3].Value;
                seq = match.Groups[4].Value;

                html = pdf.Replace("AF-", "EI-");
                html = html.Replace(".pdf", ".html");
                
                try
                {
                    //감평서 OCR-단가, 평가액
                    DataTable dtL = db.ExeDt("select tid from ta_list where spt='" + spt + "' and sn1='" + sn1 + "' and sn2='" + sn2 + "' and 2nd_dt='" + today + "'");
                    foreach (DataRow row in dtL.Rows)
                    {
                        ApslPrice(row["tid"].ToString(), html);
                    }
                }
                catch
                {
                    continue;
                }
            }

            atomLog.AddLog(string.Format(" > OCR-{0}", sucCnt));
        }

        /// <summary>
        /// 감평서 OCR-감정원, 가격시점
        /// </summary>
        /// <param name="spt"></param>
        /// <param name="sn1"></param>
        /// <param name="sn2"></param>
        /// <param name="filePath">html 파일 로컬경로</param>
        private void ApslNmDt(string spt, string sn1, string sn2, string filePath)
        {
            string html, html2, sumryTbl, apslNm = "", apslDt = "", sql, today;
            int i = 0;

            today = DateTime.Now.ToShortDateString();

            Stream stream = File.OpenRead(filePath);
            StreamReader sr = new StreamReader(stream, Encoding.UTF8);
            html = sr.ReadToEnd();
            sr.Close();
            sr.Dispose();
            stream.Close();
            stream.Dispose();

            HAPDoc doc = new HAPDoc();
            Match match, m;
            MatchCollection mc;

            match = Regex.Match(html, @"(\w+)감정평가(사사무소|사합동사무소|법인|사무소|사사)|[(주)쥐취（）]{2,}[ ]*감정평가법인[ ]*(\w+)|감정평가법인[ ]*(\w+)[(주)쥐취（）]{2,}", rxOptM);
            if (match.Groups[1].Value != string.Empty) apslNm = match.Groups[1].Value;
            else if (match.Groups[3].Value != string.Empty) apslNm = match.Groups[3].Value;
            else if (match.Groups[4].Value != string.Empty) apslNm = match.Groups[4].Value;

            if (apslNm == string.Empty)
            {
                match = Regex.Match(html, @">(\w+).*?감정평가사사무소|감정평가사사무소[ ]*(\w+)", rxOptM);
                if (match.Groups[1].Value != string.Empty) apslNm = match.Groups[1].Value;
                else if (match.Groups[2].Value != string.Empty) apslNm = match.Groups[2].Value;
            }

            if (apslNm != string.Empty)
            {
                apslNm = Regex.Replace(apslNm, @"^[주쥐취]|푸이", string.Empty);
                apslNm = apslNm.Replace("세종의", "세종");
                apslNm = apslNm.Replace("오남도", "남도");
                apslNm = apslNm.Replace("삼참", "삼창");
                apslNm = apslNm.Replace("삼챵", "삼창");
                apslNm = apslNm.Replace("롱일", "통일");
                apslNm = apslNm.Replace("신에", "신애");
                apslNm = apslNm.Replace("이득", "이목");

                if (apslNm == "가이") apslNm = "가이야";
                if (apslNm == "돗") apslNm = "동행";

                if ((apslNm == "비" || apslNm == "앤비") && html.Contains("TNB")) apslNm = "티앤비";
                if (apslNm == "현" && html.Contains("현산")) apslNm = "현산";
                if (apslNm == "일" && html.Contains("통일감정")) apslNm = "통일";
                if (apslNm == "뱅크" && html.Contains("리얼티")) apslNm = "리얼티뱅크";
                if ((apslNm == "효" || apslNm == "성") && html.Contains("효성")) apslNm = "효성";

                if (apslNm.Contains("국토")) apslNm = "국토";
                else if (apslNm.Contains("대교")) apslNm = "대교";  //대교의

                if (html.Contains("JK Lee")) apslNm = "이종경";
                else if (html.Contains("K-land")) apslNm = "K-land";
                else if (html.Contains("대한감정평가법인")) apslNm = "대한";
                else if (html.Contains("주원감정평가")) apslNm = "주원";
                else if (html.Contains("주영감정평가")) apslNm = "주영";
                else if (html.Contains("대일감정원")) apslNm = "대일";
                else if (html.Contains("에이원감정평가")) apslNm = "에이원";
                else if (html.Contains("FIRST APPRAISAL ")) apslNm = "제일";
                else if (html.Contains("L.H")) apslNm = "L.H";
                else if (html.Contains("법인 에이블")) apslNm = "에이블";                
            }
            else
            {
                if (html.IndexOf("REM", StringComparison.Ordinal) > -1) apslNm = "REM";
                else if (html.Contains("Samchang")) apslNm = "삼창";
                else if (html.Contains("JEONG AN")) apslNm = "정안";
                else if (html.Contains("효성감정평")) apslNm = "효성";
                else if (html.Contains("제일감정평")) apslNm = "제일";
                else if (html.Contains("윤슬")) apslNm = "윤슬";
                else if (Regex.IsMatch(html, @"나[ ]*라[ ]*감[ ]*정")) apslNm = "나라";

                if (apslNm == string.Empty)
                {
                    html2 = html.Replace(" ", string.Empty);
                    match = Regex.Match(html2, @"(\w+)감정평가(사사무소|사합동사무소|법인)|[(주)쥐취（）]{2,}감정평가법인(\w+)|감정평가법인(\w+)[(주)쥐취（）]{2,}", rxOptM);
                    if (match.Groups[1].Value != string.Empty) apslNm = match.Groups[1].Value;
                    else if (match.Groups[3].Value != string.Empty) apslNm = match.Groups[3].Value;
                    else if (match.Groups[4].Value != string.Empty) apslNm = match.Groups[4].Value;
                }
            }

            if (apslNm.Contains("감정원"))
            {
                apslNm = apslNm.Remove(apslNm.IndexOf("감정원"));
            }

            if (apslNm != string.Empty)
            {
                apslNm = Regex.Replace(apslNm, @"^[一-龥]", string.Empty);
                if ((apslNm == "효" || apslNm == "성") && html.Contains("효성")) apslNm = "효성";
            }

            match = Regex.Match(html, @"감[ ]*정[ ]*평[ ]*가[ ]*표.*?(<table.*?</table>)", rxOptS);
            if (match.Success)
            {
                sumryTbl = match.Groups[1].Value;
                m = Regex.Match(sumryTbl, @"(20\d{2})[.,년/\-][ ]*(\d+){1,2}[.,월/\-][ ]*(\d+){1,2}[.,일]*", rxOptM);
                if (m.Success)
                {
                    apslDt = string.Format("{0}-{1}-{2}", m.Groups[1].Value, m.Groups[2].Value.PadLeft(2, '0'), m.Groups[3].Value.PadLeft(2, '0'));
                }
            }

            if (apslDt == string.Empty)
            {
                html2 = html.Replace(" ", string.Empty);
                mc = Regex.Matches(html2, @"<table.*?</table>", rxOptS);
                foreach (Match mt in mc)
                {
                    if (Regex.IsMatch(mt.Value, @"기[ ]*준[ ]*시[ ]*점", rxOptM))
                    {
                        sumryTbl = mt.Value;
                        m = Regex.Match(mt.Value, @"(20\d{2})[.,년/\-][ ]*(\d+){1,2}[.,월/\-][ ]*(\d+){1,2}[.,일]*", rxOptM);
                        if (m.Success)
                        {
                            apslDt = string.Format("{0}-{1}-{2}", m.Groups[1].Value, m.Groups[2].Value.PadLeft(2, '0'), m.Groups[3].Value.PadLeft(2, '0'));
                        }
                        break;
                    }
                }

                //그래도 없다면 제일 처음 나오는 날짜를 취한다.
                if (apslDt == string.Empty)
                {
                    m = Regex.Match(html2, @"(20\d{2})[.,][ ]*(\d+){1,2}[.,][ ]*(\d+){1,2}[.,]*", rxOptM);
                    if (m.Success)
                    {
                        apslDt = string.Format("{0}-{1}-{2}", m.Groups[1].Value, m.Groups[2].Value.PadLeft(2, '0'), m.Groups[3].Value.PadLeft(2, '0'));
                    }
                }
            }

            DataTable dtL = db.ExeDt("select tid from ta_list where spt='" + spt + "' and sn1='" + sn1 + "' and sn2='" + sn2 + "' and 2nd_dt='" + today + "'");
            db.Open();
            foreach (DataRow r in dtL.Rows)
            {
                sql = "update ta_dtl set apsl_nm='" + apslNm + "', apsl_dt='" + apslDt + "' where tid='" + r["tid"].ToString() + "' and apsl_dt='0000-00-00'";
                db.ExeQry(sql);
            }
            db.Close();
        }

        /// <summary>
        /// 감평서 OCR-단가, 평가액
        /// </summary>
        /// <param name="spt"></param>
        /// <param name="sn1"></param>
        /// <param name="sn2"></param>
        /// <param name="filePath">html 파일 로컬경로</param>
        private void ApslPrice(string tid, string filePath)
        {
            string sql, html0, html, docTxt0, docTxt, htmlMb, htmlLand, htmlLandBldg;
            string apslType = string.Empty;
            string no, adrs, jibun, use, a1, a2, p1, p2;
            int i = 0, lsCnt = 0, landCnt = 0, bldgCnt = 0, etcCnt = 0;
            int cA1, cA2, cP1, cP2; //칼럼 인덱스No-공부면적, 사정면적, 단가, 금액
            decimal apslAmt = 0, landAmtSum = 0, bldgAmtSum = 0, amtSum = 0, amt = 0, unitPrc = 0;
            double mSqm = 0, gSqm = 0;  //명세표 사정면적, DG면적

            sql = "select * from ta_list where tid=" + tid + " limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            apslAmt = Convert.ToDecimal(dr["apsl_amt"]);
            dr.Close();
            db.Close();

            //목록내역
            sql = "select * from ta_ls where tid=" + tid;
            DataTable dtLs = db.ExeDt(sql);
            lsCnt = dtLs.Rows.Count;
            if (lsCnt == 0)
            {
                return;
            }

            //토지현황
            sql = "select * from ta_land where tid=" + tid;
            DataTable dtLand = db.ExeDt(sql);
            landCnt = dtLand.Rows.Count;

            //건물현황
            sql = "select * from ta_bldg where tid=" + tid + " and dvsn=1";
            DataTable dtBldg = db.ExeDt(sql);
            bldgCnt = dtBldg.Rows.Count;

            //제시외현황
            sql = "select * from ta_bldg where tid=" + tid + " and dvsn=2";
            DataTable dtEtc = db.ExeDt(sql);
            etcCnt = dtEtc.Rows.Count;

            List<string> lsDvsn = new List<string>();
            foreach (DataRow row in dtLs.Rows)
            {
                lsDvsn.Add(row["dvsn"].ToString());
            }

            if (lsCnt == 1)
            {
                if (lsDvsn[0] == "집합건물") apslType = "집합단일";
                else if (lsDvsn[0] == "토지") apslType = "토지";
                else if (lsDvsn[0] == "건물") apslType = "건물";
            }
            else
            {
                if (lsDvsn.Contains("집합건물") && lsDvsn.Contains("토지") && lsDvsn.Contains("건물")) apslType = "집합토지건물";     //제외
                else if (lsDvsn.Contains("집합건물") && lsDvsn.Contains("토지")) apslType = "집합토지";   //제외
                else if (lsDvsn.Contains("집합건물") && lsDvsn.Contains("건물")) apslType = "집합건물";   //제외
                else if (lsDvsn.Contains("토지") && lsDvsn.Contains("건물")) apslType = "토지건물";
                else if (lsDvsn.Contains("집합건물")) apslType = "집합";
                else if (lsDvsn.Contains("토지")) apslType = "토지";
                else if (lsDvsn.Contains("건물")) apslType = "건물";
            }

            if (apslType == string.Empty)
            {
                return;
            }

            HAPDoc doc = new HAPDoc();
            StringBuilder sb = new StringBuilder();

            Stream stream = File.OpenRead(filePath);
            StreamReader sr = new StreamReader(stream, Encoding.UTF8);
            html0 = sr.ReadToEnd();
            sr.Close();
            sr.Dispose();
            stream.Close();
            stream.Dispose();

            doc.LoadHtml(html0);
            docTxt0 = doc.DocumentNode.InnerText.Trim();

            //명세표
            MatchCollection mcTbl = Regex.Matches(html0, @"[（(]*(토지|토지[,및.• ]*건물|부동산|구분건물|아파트|오피스텔|토지건물)[)）]*[ ]*[감정]*평가[ ]*명[ ]*세[ ]*표.*?<table border=""1"">.*?</table>", rxOptS);
            foreach (Match maTbl in mcTbl)
            {
                sb.Append(maTbl.Value);
            }
            if (mcTbl.Count == 0)
            {
                HtmlNodeCollection ncTbl = doc.DocumentNode.SelectNodes("//table[contains(.,'공부') or contains(.,'공 부')]");
                if (ncTbl != null)
                {
                    foreach (HtmlNode ndTbl in ncTbl)
                    {
                        sb.Append(ndTbl.OuterHtml);
                    }
                }
            }
            html = sb.ToString();
            sb.Clear();

            doc.LoadHtml(html);
            docTxt = doc.DocumentNode.InnerText.Trim();
            html = doc.DocumentNode.InnerHtml.Trim();

            //집합건물-토지/건물 평가액 패턴
            List<string> mbPtrn = new List<string>();
            mbPtrn.Add(@"배분내역\s+[토지\:\s]{3,}(\d[\d,]{4,})\s+[건물\:\s]{3,}(\d[\d,]{4,})");
            mbPtrn.Add(@"배분내역\s+[토지가액\:\s]{5,}(\d[\d,]{4,})\s+[건물가액\:\s]{5,}(\d[\d,]{4,})");
            mbPtrn.Add(@"배분내역[\]］>＞\s]*(\d[\d,]{4,})[\s]*(\d[\d,]{4,})");
            mbPtrn.Add(@"배분가격[\s]*(\d[\d,]{4,})[\s]*(\d[\d,]{4,})");
            mbPtrn.Add(@"배분가액[：:\s]+(\d[\d,]{4,})[：:\s]+(\d[\d,]{4,})");
            mbPtrn.Add(@"가격[\s]*배분[)\s]*(\d[\d,]{4,})[\s]*(\d[\d,]{4,})");
            mbPtrn.Add(@"토지[,.]건물[ ]*배분가격[\s]+[토지건물\s]{5,}[\s]+(\d[\d,]{4,})[\s]*(\d[\d,]{4,})");
            mbPtrn.Add(@"[배분가격a-z\s]{5,}[토지건물\s]{5,}[\s]+(\d[\d,]{4,})[\s]*(\d[\d,]{4,})");
            mbPtrn.Add(@"[토지건물배분\s,:]{8,}내역\s+(\d[\d,]{4,})[\s]*(\d[\d,]{4,})");
            mbPtrn.Add(@"배분내역[\]］\s]+토지[:\s]+(\d[\d,]{4,})[\s]*건물[:\s]+(\d[\d,]{4,})");
            mbPtrn.Add(@"[토지건물배분\s]{6,}토지[\s:]+건물[\s:]+(\d[\d,]{4,})[\s]+(\d[\d,]{4,})");
            mbPtrn.Add(@"배분가액[ ]*토[ ]*지[\s:]+건[ ]*물[\s:]+(\d[\d,]{4,})[\s]+(\d[\d,]{4,})");
            mbPtrn.Add(@"토지/건물[\s]*비율[\s]*(\d[\d,]{4,})[\s]*(\d[\d,]{4,})");

            mbPtrn.Add(@"배분내역[\]］\s]+건물[:\s]+(\d[\d,]{4,})[\s]*토지[:\s]+(\d[\d,]{4,})");     //건물, 토지 -> 토지, 건물 역순
            mbPtrn.Add(@"[토지건물의배분내역은\s.,•]{12,}토지[\s:]+(\d[\d,]{4,})원[,\s]+건물[\s:]+(\d[\d,]{4,})원");    //명세표외 부분에서-토지 • 건물의 배분내역은 토지 : 29,160,000원, 건물 : 24,840,000원임

            if (apslType == "집합단일" && landCnt == 1 && bldgCnt == 1)
            {
                //명세표에서
                foreach (string ptrn in mbPtrn)
                {
                    MatchCollection mc = Regex.Matches(docTxt, ptrn, rxOptM);
                    if (mc.Count == 0) continue;
                    foreach (Match match in mc)
                    {
                        if (Regex.IsMatch(ptrn, @"건물.*토지.*", rxOptM))
                        {
                            landAmtSum = (string.IsNullOrEmpty(match.Groups[2].Value)) ? 0 : Convert.ToDecimal(match.Groups[2].Value);
                            bldgAmtSum = (string.IsNullOrEmpty(match.Groups[1].Value)) ? 0 : Convert.ToDecimal(match.Groups[1].Value);
                        }
                        else
                        {
                            landAmtSum = (string.IsNullOrEmpty(match.Groups[1].Value)) ? 0 : Convert.ToDecimal(match.Groups[1].Value);
                            bldgAmtSum = (string.IsNullOrEmpty(match.Groups[2].Value)) ? 0 : Convert.ToDecimal(match.Groups[2].Value);
                        }
                        amtSum = landAmtSum + bldgAmtSum;
                        if (amtSum == apslAmt) goto EXIT;
                    }
                }

                //문서전체에서-Multi Line
                if (amtSum == 0)
                {
                    foreach (string ptrn in mbPtrn)
                    {
                        MatchCollection mc = Regex.Matches(docTxt0, ptrn, rxOptM);
                        if (mc.Count == 0) continue;
                        foreach (Match match in mc)
                        {
                            landAmtSum = (string.IsNullOrEmpty(match.Groups[1].Value)) ? 0 : Convert.ToDecimal(match.Groups[1].Value);
                            bldgAmtSum = (string.IsNullOrEmpty(match.Groups[2].Value)) ? 0 : Convert.ToDecimal(match.Groups[2].Value);
                            amtSum = landAmtSum + bldgAmtSum;
                            if (amtSum == apslAmt) goto EXIT;
                        }
                    }
                }

                //문서전체에서-Single Line
                if (amtSum == 0)
                {
                    Match match = Regex.Match(docTxt0, @"배분[가격내역]{2}.*?토지[:\s]+(\d[\d,]{4,}).*?건물[:\s]+(\d[\d,]{4,})", rxOptS);
                    if (match.Success)
                    {
                        landAmtSum = (string.IsNullOrEmpty(match.Groups[1].Value)) ? 0 : Convert.ToDecimal(match.Groups[1].Value);
                        bldgAmtSum = (string.IsNullOrEmpty(match.Groups[2].Value)) ? 0 : Convert.ToDecimal(match.Groups[2].Value);
                        amtSum = landAmtSum + bldgAmtSum;
                    }
                }

            EXIT:
                if (amtSum == apslAmt)
                {
                    db.Open();
                    sql = "update ta_land set amt='" + landAmtSum.ToString().Replace(",", string.Empty) + "' where tid='" + tid + "'";
                    db.ExeQry(sql);

                    sql = "update ta_bldg set amt='" + bldgAmtSum.ToString().Replace(",", string.Empty) + "' where tid='" + tid + "' and dvsn=1";
                    db.ExeQry(sql);

                    sql = "update ta_dtl set apsl_land='" + landAmtSum.ToString().Replace(",", string.Empty) + "', apsl_bldg='" + bldgAmtSum.ToString().Replace(",", string.Empty) + "' where tid='" + tid + "'";
                    db.ExeQry(sql);
                    db.Close();
                }
                else
                {
                    landAmtSum = 0;
                    bldgAmtSum = 0;
                    amtSum = 0;
                }
            }
            else if (apslType == "토지" || apslType == "건물" || apslType == "토지건물")
            {
                landAmtSum = 0;
                bldgAmtSum = 0;

                DataTable dtM = new DataTable();
                dtM.Columns.Add("no");      //일련번호
                dtM.Columns.Add("adrs");    //소재지
                dtM.Columns.Add("jibun");   //지번
                dtM.Columns.Add("use");     //지목 및 용도
                dtM.Columns.Add("a1");      //면적(공부)
                dtM.Columns.Add("a2");      //면적(사정)
                dtM.Columns.Add("p1");      //감정평가액(단가)
                dtM.Columns.Add("p2");      //감정평가액(금액)
                dtM.Columns.Add("etcFlag", typeof(Boolean));     //제시외 시작 플래그

                HtmlNodeCollection nct = doc.DocumentNode.SelectNodes(".//table");
                if (nct == null)
                {
                    return;
                }

                //방법-1 (라인단위 텍스트 그룹으로 해석)==============================================================================================================================
                foreach (HtmlNode ndt in nct)
                {
                    HtmlNodeCollection ncTr = ndt.SelectNodes("./tr");
                    if (ncTr == null) continue;
                    if (ncTr.Count < 3) continue;

                    i = 0;
                    string preNo = "0";
                    bool etcFlag = false;
                    foreach (HtmlNode ndTr in ncTr)
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        if (ncTd.Count < 8) continue;
                        no = ncTd[0].InnerText.Replace(".", string.Empty).Trim();
                        if (no == "일련 번호" || no == "일련" || no == "번호") continue;
                        no = Regex.Match(no, @"\d+", rxOptM).Value;
                        if (no == string.Empty)
                        {
                            no = (i == 0) ? "1" : preNo;
                        }
                        string[] txtArr = ndTr.InnerText.Trim().Split('\n');
                        int txtArrCnt = txtArr.Count();
                        if (ndTr.InnerText.Contains("제시외")) etcFlag = true;

                        //토지 현황
                        foreach (DataRow r in dtLand.Rows)
                        {
                            try
                            {
                                p1 = r["unit_prc"].ToString();
                                if (p1 != "0") continue;
                                gSqm = Convert.ToDouble(r["sqm"].ToString());
                                int fidx = Array.FindLastIndex(txtArr, x => Math.Abs(((Double.TryParse(x.Trim(), out mSqm)) ? mSqm : 0) - gSqm) < 1);
                                if (fidx > -1 && r["ls_no"].ToString() == no)
                                {
                                    p1 = txtArr[fidx + 1].Replace(".", string.Empty);   //,를 .로 해석하는 경우
                                    p1 = Regex.Replace(p1, @"[^\d,]", string.Empty, rxOptM).Trim();
                                    unitPrc = Convert.ToDecimal(p1 ?? "0");
                                    if (unitPrc % 10 != 0 || unitPrc < 100) continue;   //단가가 1원 단위 또는 100원 보다 작을 때는 오류로 판단
                                    r["unit_prc"] = unitPrc;
                                    r.AcceptChanges();
                                    dtLand.AcceptChanges();
                                    amt = Convert.ToDecimal(r["sqm"].ToString()) * unitPrc;
                                    landAmtSum += amt;

                                    db.Open();
                                    sql = "update ta_land set unit_prc='" + p1.Replace(",", string.Empty) + "', amt='" + amt.ToString().Replace(",", string.Empty) + "' where idx='" + r["idx"].ToString() + "' and tid='" + tid + "'";
                                    db.ExeQry(sql);
                                    db.Close();
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                //MessageBox.Show(ex.Message);
                                continue;
                            }
                        }

                        //건물 현황
                        foreach (DataRow r in dtBldg.Rows)
                        {
                            try
                            {
                                p1 = r["unit_prc"].ToString();
                                if (p1 != "0") continue;
                                gSqm = Convert.ToDouble(r["sqm"].ToString());
                                int fidx = Array.FindLastIndex(txtArr, x => Math.Abs(((Double.TryParse(x.Trim(), out mSqm)) ? mSqm : 0) - gSqm) < 1);
                                if (fidx > -1 && r["ls_no"].ToString() == no)
                                {
                                    p1 = txtArr[fidx + 1].Replace(".", string.Empty);   //,를 .로 해석하는 경우
                                    p1 = Regex.Replace(p1, @"[^\d,]", string.Empty, rxOptM).Trim();
                                    unitPrc = Convert.ToDecimal(p1 ?? "0");
                                    if (unitPrc % 10 != 0 || unitPrc < 100) continue;   //단가가 1원 단위 또는 100원 보다 작을 때는 오류로 판단
                                    r["unit_prc"] = unitPrc;
                                    r.AcceptChanges();
                                    dtBldg.AcceptChanges();
                                    amt = Convert.ToDecimal(r["sqm"].ToString()) * unitPrc;
                                    bldgAmtSum += amt;

                                    db.Open();
                                    sql = "update ta_bldg set unit_prc='" + p1.Replace(",", string.Empty) + "', amt='" + amt.ToString().Replace(",", string.Empty) + "' where idx='" + r["idx"].ToString() + "' and tid='" + tid + "'";
                                    db.ExeQry(sql);
                                    db.Close();
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                //MessageBox.Show(ex.Message);
                                continue;
                            }
                        }

                        if (!etcFlag) continue;

                        //제시외 현황
                        foreach (DataRow r in dtEtc.Rows)
                        {
                            try
                            {
                                p1 = r["unit_prc"].ToString();
                                if (p1 != "0") continue;
                                gSqm = Convert.ToDouble(r["sqm"].ToString());
                                int fidx = Array.FindLastIndex(txtArr, x => Math.Abs(((Double.TryParse(x.Trim(), out mSqm)) ? mSqm : 0) - gSqm) < 1);
                                if (fidx > -1 && r["ls_no"].ToString() == no)
                                {
                                    p1 = txtArr[fidx + 1].Replace(".", string.Empty);   //,를 .로 해석하는 경우
                                    p1 = Regex.Replace(p1, @"[^\d,]", string.Empty, rxOptM).Trim();
                                    unitPrc = Convert.ToDecimal(p1 ?? "0");
                                    if (unitPrc % 10 != 0 || unitPrc < 100) continue;   //단가가 1원 단위 또는 100원 보다 작을 때는 오류로 판단
                                    r["unit_prc"] = unitPrc;
                                    r.AcceptChanges();
                                    dtEtc.AcceptChanges();
                                    amt = Convert.ToDecimal(r["sqm"].ToString()) * unitPrc;
                                    //bldgAmtSum += amt;

                                    db.Open();
                                    sql = "update ta_bldg set unit_prc='" + p1.Replace(",", string.Empty) + "', amt='" + amt.ToString().Replace(",", string.Empty) + "' where idx='" + r["idx"].ToString() + "' and tid='" + tid + "'";
                                    db.ExeQry(sql);
                                    db.Close();
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                //MessageBox.Show(ex.Message);
                                continue;
                            }
                        }

                        preNo = no;
                    }
                }

                //방법-2 (구조화된 셀단위로 해석)==============================================================================================================================
                foreach (HtmlNode ndt in nct)
                {
                    HtmlNodeCollection ncTr = ndt.SelectNodes("./tr");
                    if (ncTr == null) continue;
                    if (ncTr.Count < 3) continue;

                    bool etcFlag = false;
                    HtmlNodeCollection nodes = ncTr[1].SelectNodes("./td");
                    if (nodes.Count == 4)
                    {
                        cA1 = 5;
                        cA2 = 6;
                        cP1 = 7;
                        cP2 = 8;
                    }
                    else
                    {
                        string colsStr = ncTr[1].InnerText.Trim();
                        colsStr = Regex.Replace(colsStr, @"번[ ]*호|용[ ]*도|[및 ]*구[ ]*조", string.Empty, rxOptM).Trim();
                        string[] colsArr = colsStr.Split('\n');
                        try
                        {
                            int idx = Array.FindIndex(colsArr, r => r.Contains("부"));
                            cA1 = 4 + idx + 1;
                            idx = Array.FindIndex(colsArr, r => r.Contains("정"));
                            cA2 = 4 + idx + 1;
                            idx = Array.FindIndex(colsArr, r => r.Contains("가"));
                            cP1 = 4 + idx + 1;
                            idx = Array.FindIndex(colsArr, r => r.Contains("액") || r.Contains("애"));
                            cP2 = 4 + idx + 1;
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    foreach (HtmlNode ndTr in ncTr)
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        if (ncTd.Count < 8) continue;
                        no = ncTd[0].InnerText.Replace(".", string.Empty).Trim();
                        if (no == "일련 번호" || no == "일련" || no == "번호") continue;

                        if (ndTr.InnerText.Contains("제시외")) etcFlag = true;
                        try
                        {
                            adrs = ncTd[1].InnerText.Trim();
                            jibun = ncTd[2].InnerText.Trim();
                            use = ncTd[3].InnerText.Trim();
                            a1 = ncTd[cA1].InnerText.Trim();
                            a2 = ncTd[cA2].InnerText.Trim();
                            p1 = ncTd[cP1].InnerText.Trim().Replace(".", string.Empty);
                            p2 = ncTd[cP2].InnerText.Trim().Replace(".", string.Empty);

                            DataRow row = dtM.NewRow();
                            row["no"] = no;
                            row["adrs"] = adrs;
                            row["jibun"] = jibun;
                            row["use"] = use;
                            row["a1"] = Regex.Replace(a1, @"[^\d,.]", string.Empty, rxOptM).Trim();
                            row["a2"] = Regex.Replace(a2, @"[^\d,.]", string.Empty, rxOptM).Trim();
                            row["p1"] = Regex.Replace(p1, @"[^\d,.]", string.Empty, rxOptM).Trim();
                            row["p2"] = Regex.Replace(p2, @"[^\d,.]", string.Empty, rxOptM).Trim();
                            row["etcFlag"] = etcFlag;
                            dtM.Rows.Add(row);
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }

                //명세표가 단일목록에 일련번호가 없을 경우 목록번호 부여
                if (lsCnt == 1 && dtM.Rows.Count == 1)
                {
                    if (dtM.Rows[0]["no"].ToString() == string.Empty)
                    {
                        dtM.Rows[0]["no"] = dtLs.Rows[0]["no"];
                    }
                }

                string prvNo = "0";
                foreach (DataRow row in dtM.Rows)
                {
                    try
                    {
                        no = row["no"].ToString();
                        if (no != string.Empty) prvNo = no;
                        if (no == string.Empty) no = prvNo;
                        mSqm = Convert.ToDouble(row["a2"]?.ToString() ?? "0");
                        p1 = row["p1"].ToString();
                        unitPrc = Convert.ToDecimal(p1 ?? "0");
                        if (unitPrc % 10 != 0 || unitPrc < 100) continue;   //단가가 1원 단위 또는 100원 보다 작을 때는 오류로 판단

                        //토지 현황
                        foreach (DataRow r in dtLand.Rows)
                        {
                            if (r["unit_prc"].ToString() != "0") continue;
                            gSqm = Convert.ToDouble(r["sqm"].ToString());
                            if (r["ls_no"].ToString() == no && (Math.Abs(mSqm - gSqm) < 1))
                            {                                
                                r["unit_prc"] = unitPrc;
                                r.AcceptChanges();
                                dtLand.AcceptChanges();
                                amt = Convert.ToDecimal(r["sqm"].ToString()) * unitPrc;
                                landAmtSum += amt;

                                db.Open();
                                sql = "update ta_land set unit_prc='" + p1.Replace(",", string.Empty) + "', amt='" + amt.ToString().Replace(",", string.Empty) + "' where idx='" + r["idx"].ToString() + "' and tid='" + tid + "'";
                                db.ExeQry(sql);
                                db.Close();
                                break;
                            }
                        }

                        //건물 현황
                        foreach (DataRow r in dtBldg.Rows)
                        {
                            if (r["unit_prc"].ToString() != "0") continue;
                            gSqm = Convert.ToDouble(r["sqm"].ToString());
                            if (r["ls_no"].ToString() == no && (Math.Abs(mSqm - gSqm) < 1))
                            {
                                r["unit_prc"] = unitPrc;
                                r.AcceptChanges();
                                dtBldg.AcceptChanges();
                                amt = Convert.ToDecimal(r["sqm"].ToString()) * unitPrc;
                                bldgAmtSum += amt;

                                db.Open();
                                sql = "update ta_bldg set unit_prc='" + p1.Replace(",", string.Empty) + "', amt='" + amt.ToString().Replace(",", string.Empty) + "' where idx='" + r["idx"].ToString() + "' and tid='" + tid + "'";
                                db.ExeQry(sql);
                                db.Close();
                                break;
                            }
                        }

                        if (Convert.ToBoolean(row["etcFlag"]) != true) continue;

                        //제시외 현황
                        foreach (DataRow r in dtEtc.Rows)
                        {
                            if (r["unit_prc"].ToString() != "0") continue;
                            gSqm = Convert.ToDouble(r["sqm"].ToString());
                            if (r["ls_no"].ToString() == no && (Math.Abs(mSqm - gSqm) < 1))
                            {
                                r["unit_prc"] = unitPrc;
                                r.AcceptChanges();
                                dtEtc.AcceptChanges();
                                amt = Convert.ToDecimal(r["sqm"].ToString()) * unitPrc;
                                //bldgAmtSum += amt;

                                db.Open();
                                sql = "update ta_bldg set unit_prc='" + p1.Replace(",", string.Empty) + "', amt='" + amt.ToString().Replace(",", string.Empty) + "' where idx='" + r["idx"].ToString() + "' and tid='" + tid + "'";
                                db.ExeQry(sql);
                                db.Close();
                                break;
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                if ((landAmtSum + bldgAmtSum) == apslAmt)
                {
                    db.Open();
                    sql = "update ta_dtl set apsl_land='" + landAmtSum.ToString().Replace(",", string.Empty) + "',  apsl_bldg='" + bldgAmtSum.ToString().Replace(",", string.Empty) + "' where tid='" + tid + "'";
                    db.ExeQry(sql);
                    db.Close();
                }
            }
        }

        /// <summary>
        /// 날짜형 포멧 매칭
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private string DtChk(string str)
        {
            string dt = "";

            Match m = Regex.Match(str, @"(\d+)[\./\s]+(\d+)[\./\s]+(\d+)[\.]*");
            if (m.Success)
            {
                dt = string.Format("{0}-{1}-{2}", m.Groups[1].Value, m.Groups[2].Value.PadLeft(2, '0'), m.Groups[3].Value.PadLeft(2, '0'));
            }
            else dt = str;

            return dt;
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

        /// <summary>
        /// 보증금, 차임 금액 정리
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private string MoneyChk(string str)
        {
            string money = "", kor = "";

            string mixPtrn = @"(\d+)([십백천만억조]+)";
            string korPtrn = @"[일이삼사오육칠팔구만]+";

            StringBuilder sb = new StringBuilder();

            if (str.IndexOf("(") > -1)
            {
                str = str.Remove(str.IndexOf("("));
            }

            str = Regex.Replace(str, @"[금원정월매\,\s]", string.Empty).Trim();
            if (Regex.IsMatch(str, mixPtrn))
            {
                MatchCollection mc = Regex.Matches(str, mixPtrn);
                foreach (Match match in mc)
                {
                    kor = NumToKor(Convert.ToInt64(match.Groups[1].Value));
                    sb.Append(kor + match.Groups[2].Value);
                }
                str = sb.ToString();
            }

            if (Regex.IsMatch(str, korPtrn))
            {
                if (Regex.IsMatch(str, @"[^일이삼사오육칠팔구십백천만억조]")) money = str;
                else
                {
                    money = KorToNum(str);
                }
            }
            else
            {
                money = str;
            }

            return money;
        }

        /// <summary>
        /// 한글 -> 숫자
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private string KorToNum(string input)
        {
            long result = 0;
            long tmpResult = 0;
            long num = 0;
            //MessageBox.Show(input);
            string number = "영일이삼사오육칠팔구";
            string unit = "십백천만억조";
            long[] unit_num = { 10, 100, 1000, 10000, (long)Math.Pow(10, 8), (long)Math.Pow(10, 12) };

            string[] arr = Regex.Split(input, @"(십|백|천|만|억|조)");    //괄호로 감싸주면 분할시 delimiters 포함한다.
            for (int i = 0; i < arr.Length; i++)
            {
                string token = arr[i];
                int check = number.IndexOf(token);
                if (check == -1)    //단위일 경우
                {
                    if ("만억조".IndexOf(token) == -1)
                    {
                        tmpResult += (num != 0 ? num : 1) * unit_num[unit.IndexOf(token)];
                    }
                    else
                    {
                        tmpResult += num;
                        result += (tmpResult != 0 ? tmpResult : 1) * unit_num[unit.IndexOf(token)];
                        tmpResult = 0;
                    }
                    num = 0;
                }
                else
                {
                    num = check;
                }
            }
            result = result + tmpResult + num;

            return result.ToString();
        }

        /// <summary>
        /// 숫자 -> 한글
        /// </summary>
        /// <param name="lngNumber"></param>
        /// <returns></returns>
        private string NumToKor(long lngNumber)
        {
            //string kor = "";

            string[] NumberChar = new string[] { "", "일", "이", "삼", "사", "오", "육", "칠", "팔", "구" };
            string[] LevelChar = new string[] { "", "십", "백", "천" };
            string[] DecimalChar = new string[] { "", "만", "억", "조", "경" };

            string strMinus = string.Empty;

            if (lngNumber < 0)
            {
                strMinus = "마이너스";
                lngNumber *= -1;
            }

            string strValue = string.Format("{0}", lngNumber);
            string NumToKorea = string.Empty;
            bool UseDecimal = false;

            if (lngNumber == 0) return "영";

            for (int i = 0; i < strValue.Length; i++)
            {
                int Level = strValue.Length - i;
                if (strValue.Substring(i, 1) != "0")
                {
                    UseDecimal = true;
                    if (((Level - 1) % 4) == 0)
                    {
                        if (DecimalChar[(Level - 1) / 4] != string.Empty
                           && strValue.Substring(i, 1) == "1")
                            NumToKorea = NumToKorea + DecimalChar[(Level - 1) / 4];
                        else
                            NumToKorea = NumToKorea
                                              + NumberChar[int.Parse(strValue.Substring(i, 1))]
                                              + DecimalChar[(Level - 1) / 4];
                        UseDecimal = false;
                    }
                    else
                    {
                        if (strValue.Substring(i, 1) == "1")
                            NumToKorea = NumToKorea
                                               + LevelChar[(Level - 1) % 4];
                        else
                            NumToKorea = NumToKorea
                                               + NumberChar[int.Parse(strValue.Substring(i, 1))]
                                               + LevelChar[(Level - 1) % 4];
                    }
                }
                else
                {
                    if ((Level % 4 == 0) && UseDecimal)
                    {
                        NumToKorea = NumToKorea + DecimalChar[Level / 4];
                        UseDecimal = false;
                    }
                }
            }

            return strMinus + NumToKorea;
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
