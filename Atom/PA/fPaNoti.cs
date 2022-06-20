using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Text.RegularExpressions;
using HtmlAgilityPack;
using hap = HtmlAgilityPack;
using HAPDoc = HtmlAgilityPack.HtmlDocument;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using Solar;
using System.Threading;
using System.Diagnostics;
using System.Collections;

namespace Atom.PA
{
    public partial class fPaNoti : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AuctCd auctCd = new AuctCd();
        ApiUtil api = new ApiUtil();
        AtomLog atomLog = new AtomLog(200);

        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 1500, webCnt = 0;

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        DataTable dtCatCd, dtStateCd;   //물건종별, 진행상태 코드

        string filePath;    //로컬 파일저장 경로

        public fPaNoti()
        {
            InitializeComponent();
            this.Shown += FPaNoti_Shown;
        }

        private void FPaNoti_Shown(object sender, EventArgs e)
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
            //물건종별 코드
            dtCatCd = db.ExeDt("select ctgr_nm as cat3_nm, ctgr_cd as cat3_cd from tb_cd_cat where ctgr_lvl=4 and sumr5=1");

            //진행상태 코드
            dtStateCd = db.ExeDt("select sta1_cd, sta2_cd, sta1_nm, sta2_nm from ta_cd_sta order by sta2_cd");

            //파일저장 디렉토리 생성
            filePath = @"C:\Atom\PA\" + DateTime.Today.ToShortDateString();
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
                Directory.CreateDirectory(filePath + @"\_thumb");
            }
        }

        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            Proc_Noti();    //신규 공고
            Proc_Cltr();    //물건 수집
            Proc_Coord();   //주소 좌표
                        
            Prc_RgstIssueAdd(); //등기 자동발급 대상 추가

            Proc_BidRslt(); //입찰 결과
            Proc_File();    //파일 수집
            Proc_ShrLsd();  //압류재산-권리분석 기초정보(입찰시작 7일전)
            Proc_Stat();    //물건 상태

            Proc_Station(); //역세권 매칭

            Proc_SpCdtn();  //특수 물건
            Proc_AptCd();   //아파트 코드
            
            Proc_AttachPl();    //첨부파일-공고
            Proc_AttachCl();    //첨부파일-물건

            atomLog.AddLog("실행 완료", 1);
        }

        /// <summary>
        /// 신규공고 확인
        /// </summary>
        private void Proc_Noti()
        {
            string url, html, sql, bgnPlnmDt, clsPlnmDt, bgnPbctDt, clsPbctDt;
            string plnmNo, pbctNo, plnmMnmtNo, plnmDt;
            string plnmNm = "", prptDvsnNm = "", orgNm = "", rsbyDept = "", pscgNm = "", pscgTpno = "", pscgEmalAdrs = "", plnmKindNm = "", plnmYr = "", plnmSeq = "", bidDvsnNm = "", bidMtdNm = "", cptnMtdNm = "", totAmtUnpcDvsnNm = "", plnmDoc = "", dpslMtdNm = "", fileNm="";
            string pbctSeq, pbctDgr, tdpsRt, pbctBgnDtm, pbctClsDtm, pbctExctDtm;
            string jsSkd, jsFile;
            //string atchFilePtcsNo, atchSeq;
            decimal i = 0, pdCnt = 0, pgCnt = 0, totCnt = 0, curCnt = 0;

            DataTable dtNoti = new DataTable();
            dtNoti.Columns.Add("plnmNo");
            dtNoti.Columns.Add("pbctNo");
            dtNoti.Columns.Add("plnmMnmtNo");
            dtNoti.Columns.Add("plnmDt");

            DateTime now = DateTime.Now;
            bgnPlnmDt = now.AddDays(-3).ToShortDateString();
            clsPlnmDt = now.ToShortDateString();
            bgnPbctDt = now.ToShortDateString();
            clsPbctDt = now.AddDays(90).ToShortDateString();

            atomLog.AddLog(string.Format("신규공고 확인 {0}~{1}", bgnPlnmDt, clsPlnmDt));

            url = "http://www.onbid.co.kr/op/ppa/plnmmn/publicAnnounceNewRlstList.do?searchBegnPlnmDt=" + bgnPlnmDt + "&searchClsPlnmDt=" + clsPlnmDt + "&searchPbctBegnDtm=" + bgnPbctDt + "&searchPbctClsDtm=" + clsPbctDt + "&pageUnit=100";
            html = net.GetHtml(url + "&pageIndex=1", Encoding.UTF8);
            Match match = Regex.Match(html, @"<p>\[총\s+(\d+)건\]</p>", rxOptM);
            if (!match.Success) return;
            pdCnt = Convert.ToInt16(match.Groups[1].Value);
            if (pdCnt == 0) return;
            pgCnt = Math.Ceiling(pdCnt / (decimal)100);

            txtState.AppendText(string.Format("\r\n\r\n■■■■■     [공고목록] {0} ~ {1}    ■■■■■", bgnPlnmDt, clsPlnmDt));    //화면에 진행상태 표시
            HAPDoc doc = new HAPDoc();
            List<MySqlParameter> sp = new List<MySqlParameter>();
            Dictionary<string, string> dicFileRslt;

            for (i = 1; i <= pgCnt; i++)
            {
                txtState.AppendText(string.Format("\r\n[Page] {0}", i));    //화면에 진행상태 표시
                if (i > 1)
                {
                    webCnt++;
                    if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                    html = net.GetHtml(url + "&pageIndex=" + i.ToString(), Encoding.UTF8);
                }
                doc.LoadHtml(html);
                HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[contains(@class,'op_tbl_type1')]/tbody/tr");        
                if (ncTr == null) continue;

                db.Open();
                foreach (HtmlNode ndTr in ncTr)
                {
                    match = Regex.Match(ndTr.InnerHtml, @"fn_selectDetail\((\d+),[\s]*(\d+)\)", rxOptM);
                    pbctNo = match.Groups[1].Value;
                    plnmNo = match.Groups[2].Value;
                    plnmMnmtNo = ndTr.SelectSingleNode("./td/dl/dt/a").InnerText.Trim();
                    plnmDt = ndTr.SelectSingleNode(".//td[3]").InnerText.Trim();
                    sql = "select idx from tb_noti where plnm_no='" + plnmNo + "' and pbct_no='" + pbctNo + "' and plnm_mnmt_no='" + plnmMnmtNo + "' limit 1";
                    if (db.ExistRow(sql)) continue;

                    DataRow row = dtNoti.NewRow();
                    row["plnmNo"] = plnmNo;
                    row["pbctNo"] = pbctNo;
                    row["plnmMnmtNo"] = plnmMnmtNo;
                    row["plnmDt"] = plnmDt;
                    dtNoti.Rows.Add(row);
                }
                db.Close();
            }

            totCnt = dtNoti.Rows.Count;
            foreach (DataRow row in dtNoti.Rows)
            {
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                                
                plnmNo = row["plnmNo"].ToString();
                pbctNo = row["pbctNo"].ToString();
                plnmMnmtNo = row["plnmMnmtNo"].ToString();
                plnmDt = row["plnmDt"].ToString();

                txtState.AppendText(string.Format("\r\n[공고] {0} -> {1} / {2}", plnmNo, curCnt, totCnt));    //화면에 진행상태 표시

                url = "http://www.onbid.co.kr/op/ppa/plnmmn/publicAnnounceRlstDetail.do?pbctNo=" + pbctNo + "&plnmNo=" + plnmNo;
                html = net.GetHtml(url, Encoding.UTF8);
                doc.LoadHtml(html);
                HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[contains(@class,'op_tbl_type10')]/tbody/tr");
                foreach (HtmlNode ndTr in ncTr)
                {
                    HtmlNodeCollection ncTd = ndTr.SelectNodes("./th|./td");
                    if (ncTd == null) continue;
                    if (ncTd[0].InnerText.Trim() == "공고종류")
                    {
                        plnmKindNm = ncTd[0].SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                    }
                    if (ncTd[0].InnerText.Trim() == "공고회차")
                    {
                        match = Regex.Match(ncTd[0].SelectSingleNode("following-sibling::*[1]").InnerText.Trim(), @"(\d+)년도 (\d+)회차", rxOptM);
                        plnmYr = match.Groups[1].Value;
                        plnmSeq = match.Groups[2].Value;
                    }
                    if (ncTd[0].InnerText.Trim() == "처분방식")
                    {
                        dpslMtdNm = ncTd[0].SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                    }
                    if (ncTd[0].InnerText.Trim() == "공고기관")
                    {
                        orgNm = ncTd[0].SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                    }
                    if (ncTd[0].InnerText.Trim() == "담당자정보")
                    {
                        match = Regex.Match(ncTd[0].SelectSingleNode("following-sibling::*[1]").InnerText.Trim(), @"(.*) \| (.*) \| (.*) \| (.*)", rxOptM);
                        rsbyDept = match.Groups[1].Value;
                        pscgNm = match.Groups[2].Value;
                        pscgTpno = match.Groups[3].Value;
                        pscgEmalAdrs = match.Groups[4].Value;
                    }

                    if (ncTd.Count == 4 && ncTd[2].InnerText.Trim() == "자산구분")
                    {
                        prptDvsnNm = ncTd[2].SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                    }
                }

                //공고제목, 공고문
                plnmNm = doc.GetElementbyId("plnmNm").GetAttributeValue("value", "")?.Trim() ?? "";
                plnmDoc = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'content_show')]/div")?.InnerHtml.Trim() ?? "";

                //탭-공고물건 입찰정보
                HtmlNode ndTab2 = doc.GetElementbyId("tab_02");
                HtmlNodeCollection ncTr2 = ndTab2.SelectNodes(".//table[contains(@class,'op_tbl_type2')]/tbody/tr");
                foreach (HtmlNode ndTr2 in ncTr2)
                {
                    HtmlNodeCollection ncTd = ndTr2.SelectNodes("./th|./td");                    
                    if (ncTd[0].InnerText.Trim() == "입찰구분")
                    {
                        bidDvsnNm = ncTd[0].SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                    }
                    if (ncTd[0].InnerText.Trim() == "총액/단가 구분")
                    {
                        totAmtUnpcDvsnNm = ncTd[0].SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                    }
                    if (ncTd.Count == 4 && ncTd[2].InnerText.Trim() == "입찰방식/경쟁방식")
                    {
                        match = Regex.Match(ncTd[2].SelectSingleNode("following-sibling::*[1]").InnerText.Trim(), @"(\w+)\((\w+)\)", rxOptM);
                        bidMtdNm = match.Groups[1].Value;
                        cptnMtdNm = match.Groups[2].Value;
                    }
                }                
                
                //입찰 일정
                var jaSkd = new JArray();
                //ncTr2 = ndTab2.SelectNodes(".//table[contains(@class,'op_tbl_type5')]/tbody/tr");
                ncTr2 = ndTab2.SelectNodes(".//h4[text()[contains(.,'입찰일시')]]/following-sibling::div/table[contains(@class,'op_tbl_type5')]/tbody/tr");
                if (ncTr2 != null)
                {
                    foreach (HtmlNode ndTr2 in ncTr2)
                    {
                        HtmlNodeCollection ncTd = ndTr2.SelectNodes("./td");
                        match = Regex.Match(ncTd[0].InnerText.Trim(), @"(\d+)/(\d+)", rxOptM);
                        pbctSeq = match.Groups[1].Value;
                        pbctDgr = match.Groups[2].Value;
                        tdpsRt = Regex.Match(ncTd[1].InnerText.Trim(), @"(\d+)%", rxOptM).Groups[1].Value;
                        match = Regex.Match(ncTd[2].InnerText.Trim(), @"([\d\-\s:]+\s\d+:\d+) ~ ([\d\-\s:]+\s\d+:\d+)", rxOptM);
                        pbctBgnDtm = match.Groups[1].Value + ":00";
                        pbctClsDtm = match.Groups[2].Value + ":00";
                        pbctExctDtm = Regex.Match(ncTd[3].InnerText.Trim(), @"(\d{4}-\d{2}-\d{2} \d{2}:\d{2})", rxOptM).Groups[1].Value + ":00";
                        var obj = new JObject();
                        obj.Add("pbctSeq", pbctSeq);
                        obj.Add("pbctDgr", pbctDgr);
                        obj.Add("tdpsRt", tdpsRt);
                        obj.Add("pbctBgnDtm", pbctBgnDtm);
                        obj.Add("pbctClsDtm", pbctClsDtm);
                        obj.Add("pbctExctDtm", pbctExctDtm);
                        jaSkd.Add(obj);
                    }
                }                
                jsSkd = (jaSkd.Count > 0) ? JsonConvert.SerializeObject(jaSkd) : "[]";

                //공고 첨부파일-저장 보류
                /*
                var jaFile = new JArray();
                HtmlNodeCollection ncFile = doc.DocumentNode.SelectNodes("//div[contains(@class,'op_desc_file')]/p/a");
                if (ncFile != null)
                {
                    foreach (HtmlNode ndFile in ncFile)
                    {
                        match = Regex.Match(ndFile.OuterHtml, @"fn_downloadAttachFile\('(\d+)','(\d+)'\)", rxOptM);
                        atchFilePtcsNo = match.Groups[1].Value;
                        atchSeq = match.Groups[2].Value;
                        url = "http://www.onbid.co.kr/op/common/downloadFile.do?atchFilePtcsNo=" + atchFilePtcsNo + "&atchSeq=" + atchSeq;
                        fileNm = string.Format(@"{0}\E{1}_{2}.hwp", filePath, atchFilePtcsNo, atchSeq);
                        dicFileRslt = net.DnFile(url, fileNm);
                        if (dicFileRslt["result"] == "success")
                        {
                            fileNm = dicFileRslt["fileNm"];
                            var obj = new JObject();
                            obj.Add("dpFileNm", ndFile.InnerText.Trim());
                            obj.Add("svFileNm", fileNm);
                            jaFile.Add(obj);
                        }
                    }
                }
                jsFile = (jaFile.Count > 0) ? JsonConvert.SerializeObject(jaFile) : "[]";
                */
            jsFile = "[]";

                db.Open();
                sql = "insert into tb_noti set plnm_no=@plnm_no, pbct_no=@pbct_no, prpt_dvsn_nm=@prpt_dvsn_nm, plnm_nm=@plnm_nm, plnm_mnmt_no=@plnm_mnmt_no, plnm_dt=@plnm_dt, org_nm=@org_nm, rsby_dept=@rsby_dept, pscg_nm=@pscg_nm, pscg_tpno=@pscg_tpno, pscg_emal_adrs=@pscg_emal_adrs, " +
                    "plnm_kind_nm=@plnm_kind_nm, plnm_yr=@plnm_yr, plnm_seq=@plnm_seq, bid_dvsn_nm=@bid_dvsn_nm, bid_mtd_nm=@bid_mtd_nm, dpsl_mtd_nm=@dpsl_mtd_nm, cptn_mtd_nm=@cptn_mtd_nm, tot_amt_unpc_dvsn_nm=@tot_amt_unpc_dvsn_nm, plnm_doc=@plnm_doc, js_skd=@js_skd, js_file=@js_file, wdt=curdate()";
                sp.Add(new MySqlParameter("@plnm_no", plnmNo));
                sp.Add(new MySqlParameter("@pbct_no", pbctNo));
                sp.Add(new MySqlParameter("@prpt_dvsn_nm", prptDvsnNm));
                sp.Add(new MySqlParameter("@plnm_nm", plnmNm));
                sp.Add(new MySqlParameter("@plnm_mnmt_no", plnmMnmtNo));
                sp.Add(new MySqlParameter("@plnm_dt", plnmDt));
                sp.Add(new MySqlParameter("@org_nm", orgNm));
                sp.Add(new MySqlParameter("@rsby_dept", rsbyDept));
                sp.Add(new MySqlParameter("@pscg_nm", pscgNm));
                sp.Add(new MySqlParameter("@pscg_tpno", pscgTpno));
                sp.Add(new MySqlParameter("@pscg_emal_adrs", pscgEmalAdrs));
                sp.Add(new MySqlParameter("@plnm_kind_nm", plnmKindNm));
                sp.Add(new MySqlParameter("@plnm_yr", plnmYr));
                sp.Add(new MySqlParameter("@plnm_seq", plnmSeq));
                sp.Add(new MySqlParameter("@bid_dvsn_nm", bidDvsnNm));
                sp.Add(new MySqlParameter("@bid_mtd_nm", bidMtdNm));
                sp.Add(new MySqlParameter("@dpsl_mtd_nm", dpslMtdNm));
                sp.Add(new MySqlParameter("@cptn_mtd_nm", cptnMtdNm));
                sp.Add(new MySqlParameter("@tot_amt_unpc_dvsn_nm", totAmtUnpcDvsnNm));
                sp.Add(new MySqlParameter("@plnm_doc", plnmDoc));
                sp.Add(new MySqlParameter("@js_skd", jsSkd));
                sp.Add(new MySqlParameter("@js_file", jsFile));
                db.ExeQry(sql, sp);
                sp.Clear();
                db.Close();
            }

            atomLog.AddLog(string.Format("신규공고 {0}건", totCnt));
        }

        /// <summary>
        /// 물건 신규/갱신
        /// </summary>
        private void Proc_Cltr()
        {
            string html = "", url = "", urlTab = "", jsonData = "", sql = "", cvp = "";
            string plnmNo, pbctNo, cltrNo, cltrHstrNo, pbctCdtnNo, statNm;            
            string ctgrFullNm = "", bidMnmtNo = "", cltrNm = "", cltrMnmtNo1 = "", ldnmAdrs = "", dpslMtdCd = "", bidMtdNm = "", minbAmt="", apslAsesAvgAmt = "", pbctBegnDtm = "", pbctLastClsDtm = "", uscbdCnt = "", iqryCnt = "", areaInfo = "";
            string orgNm = "", orgDvsn = "", deptNm = "", pscgNm = "", pscgTpNo = "", prptDvsn = "", dlgtOrg = "", shrDt = "", iniDt = "", landSqms = "", bldSqms = "", atchFilePtcsNo = "", plnmAtchFilePtcsNo = "";
            string nmrdAdrs = "", posiEnvPscd = "", utlzPscd = "", etcDtlCntn = "", atctIvstDt = "", esctYn = "", elvtYn = "", shrYn = "", pkltYn = "", bldgNm = "", dong = "", flr = "", hous = "", qty = "", pcmtPymtEpdtCntn = "", bidPrgnNft = "", dlvrRsby = "", icdlCdtn = "";
            string eltrGrtDocUseYn = "", nextRnkRqstPsblYn = "", comnBidPmsnYn = "", twpsLsthUsbdYn = "", subtBidPmsnYn = "", twtmGthrBidPsblYn = "", othrCltrBidPsblYn = "", bidPsblCltrCnt = "", prcdBuyTgtYn = "";
            string cat1 = "", cat2 = "", cat3 = "";
            string sezNote1 = "", sezNote2 = "", sezNote3 = "";
            decimal i = 0, pdCnt = 0, pgCnt = 0, cmp = 0, totCnt = 0, curCnt = 0;
            decimal nCnt = 0, uCnt = 0;

            HAPDoc doc = new HAPDoc();
            List<MySqlParameter> sp = new List<MySqlParameter>();
            List<string> fileList = new List<string>();
            MySqlDataReader dr;

            DataTable dtLs = new DataTable();
            dtLs.Columns.Add("cltrNo");
            dtLs.Columns.Add("prcMode");
            dtLs.Columns.Add("plnmNo");
            dtLs.Columns.Add("pbctNo");
            dtLs.Columns.Add("cltrHstrNo");
            dtLs.Columns.Add("pbctCdtnNo");
            dtLs.Columns.Add("statNm");
            //dtLs.Columns.Add("minbPcnt");
            dtLs.PrimaryKey = new DataColumn[] { dtLs.Columns["cltrNo"] };

            sql = "select plnm_no, pbct_no, plnm_mnmt_no, plnm_dt from tb_noti where wdt >= date_sub(curdate(),interval 3 day) order by plnm_no";
            DataTable dtNt = db.ExeDt(sql);

            txtState.AppendText(string.Format("\r\n\r\n■■■■■     [공고-물건목록 확인]     ■■■■■"));    //화면에 진행상태 표시

            //물건 목록
            foreach (DataRow rowNt in dtNt.Rows)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                plnmNo = rowNt["plnm_no"].ToString();
                pbctNo = rowNt["pbct_no"].ToString();
                txtState.AppendText(string.Format("\r\n > 공고No - {0}", plnmNo));    //화면에 진행상태 표시

                url = "http://www.onbid.co.kr/op/cta/cltrdtl/collateralRealEstateplnmCltrPopup.do?pbctNo=" + pbctNo + "&plnmNo=" + plnmNo + "&scrnGrpCd=0001&noCancelView=N&pageUnit=100";
                html = net.GetHtml(url + "&pageIndex=1", Encoding.UTF8);
                Match match = Regex.Match(html, @"<p>\[총\s+(\d+)건\]</p>", rxOptM);
                if (!match.Success || html.Contains("조회결과가 없습니다")) continue;
                pdCnt = Convert.ToInt16(match.Groups[1].Value);
                if (pdCnt == 0) continue;
                pgCnt = Math.Ceiling(pdCnt / (decimal)100);
                for (i = 1; i <= pgCnt; i++)
                {
                    if (i > 1)
                    {
                        webCnt++;
                        if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                        html = net.GetHtml(url + "&pageIndex=" + i.ToString(), Encoding.UTF8);
                    }
                    doc.LoadHtml(html);
                    HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[contains(@class,'op_tbl_type1')]/tbody/tr");
                    if (ncTr == null) continue;

                    db.Open();
                    foreach (HtmlNode tr in ncTr)
                    {
                        HtmlNodeCollection ncTd = tr.SelectNodes("./td");
                        if (Regex.IsMatch(ncTd[0].InnerText, @"유가증권|회원권|재산권|자동차")) continue;

                        match = Regex.Match(ncTd[0].InnerHtml, @"fn_selectDetail\('(\d+)','(\d+)','(\d+)','(\d+)','(\d+)','(\d+)'\)", rxOptM);                        
                        cltrHstrNo = match.Groups[1].Value;
                        cltrNo = match.Groups[2].Value;
                        plnmNo = match.Groups[3].Value;
                        pbctNo = match.Groups[4].Value;
                        pbctCdtnNo = match.Groups[6].Value;
                        statNm = Regex.Replace(ncTd[4].InnerHtml, @"<br.*", string.Empty, rxOptS).Trim();
                        if (dtLs.Rows.Find(cltrNo) != null) continue;

                        sql = "select plnm_no, pbct_no from tb_list where cltr_no='" + cltrNo + "' limit 1";
                        dr = db.ExeRdr(sql);
                        dr.Read();
                        cmp = 1;    //신건
                        if (dr.HasRows)
                        {
                            cmp = (dr["plnm_no"].ToString() == plnmNo) ? 0 : 2;     //0-해당없음, 2-갱신
                        }
                        dr.Close();
                        if (cmp == 0) continue;
                        DataRow rowLs = dtLs.NewRow();
                        rowLs["cltrNo"] = cltrNo;
                        rowLs["prcMode"] = cmp;
                        rowLs["plnmNo"] = plnmNo;
                        rowLs["pbctNo"] = pbctNo;
                        rowLs["cltrHstrNo"] = cltrHstrNo;
                        rowLs["pbctCdtnNo"] = pbctCdtnNo;
                        rowLs["statNm"] = statNm;
                        //rowLs["minbPcnt"] = "";
                        dtLs.Rows.Add(rowLs);
                    }
                    db.Close();
                }
            }

            txtState.AppendText(string.Format("\r\n\r\n■■■■■     [물건등록/갱신]     ■■■■■"));    //화면에 진행상태 표시
            totCnt = dtLs.Rows.Count;

            //물건 상세
            foreach (DataRow row in dtLs.Rows)
            {
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                cltrNo = row["cltrNo"].ToString();
                plnmNo = row["plnmNo"].ToString();
                pbctNo = row["pbctNo"].ToString();
                cltrHstrNo = row["cltrHstrNo"].ToString();
                pbctCdtnNo = row["pbctCdtnNo"].ToString();
                statNm = row["statNm"].ToString();
                txtState.AppendText(string.Format("\r\n > 물건등록/갱신 cltrNo - {0} -> {1} / {2}", cltrNo, curCnt, totCnt));    //화면에 진행상태 표시

                url = "http://www.onbid.co.kr/op/cta/cltrdtl/collateralRealEstateDetail.do?cltrHstrNo=" + cltrHstrNo + "&cltrNo=" + cltrNo + "&plnmNo=" + plnmNo + "&pbctNo=" + pbctNo + "&scrnGrpCd=0001&pbctCdtnNo=" + pbctCdtnNo;
                html = net.GetHtml(url, Encoding.UTF8);
                doc.LoadHtml(html);
                HtmlNodeCollection ncHd = doc.DocumentNode.SelectNodes("//form[@id='sendMailFrm' or @id='frm']/input[@type='hidden']");
                if (ncHd == null) continue;
                foreach (HtmlNode hd in ncHd)
                {
                    if (hd.Attributes["id"] == null || hd.Attributes["value"] == null) continue;
                    string id = hd.Attributes["id"].Value;
                    string val = hd.Attributes["value"].Value.Trim();
                    if (id == "ctgrFullNm") ctgrFullNm = val;
                    if (id == "bidMnmtNo") bidMnmtNo = val;
                    if (id == "cltrNm") cltrNm = val;
                    if (id == "cltrMnmtNo1") cltrMnmtNo1 = val;
                    if (id == "ldnmAdrs") ldnmAdrs = val;
                    if (id == "dpslMtdCd")
                    {
                        dpslMtdCd = val;
                        dpslMtdCd = (dpslMtdCd == "0001") ? "1" : "2";
                    }
                    if (id == "apslAsesAvgAmt") apslAsesAvgAmt = val.Replace(",", string.Empty);
                    if (id == "minBidPrc") minbAmt = val.Replace(",", string.Empty);
                    if (id == "pbctBegnDtm") pbctBegnDtm = val + ":00";
                    if (id == "pbctLastClsDtm") pbctLastClsDtm = val + ":00";
                    if (id == "uscbdCnt") uscbdCnt = val;
                    if (id == "landSqms") landSqms = val.Replace(",", string.Empty);
                    if (id == "bldSqms") bldSqms = val.Replace(",", string.Empty);
                    if (id == "atchFilePtcsNo") atchFilePtcsNo = val;
                    if (id == "plnmAtchFilePtcsNo") plnmAtchFilePtcsNo = val;
                }

                fileList.Clear();
                if (atchFilePtcsNo != string.Empty)
                {
                    MatchCollection mc = Regex.Matches(html, @"onClick=""fn_goPicsPopup\('(\d+)',(\d+)\).*?(사진|지적도|위치도)", rxOptM);
                    foreach (Match m in mc)
                    {
                        if (m.Groups[2].Value == "0") continue;
                        if (m.Groups[3].Value == "사진") fileList.Add("A");
                        if (m.Groups[3].Value == "지적도") fileList.Add("B");
                        if (m.Groups[3].Value == "위치도") fileList.Add("C");
                    }
                    if (fileList.Count > 0)
                    {
                        fileList.Insert(0, atchFilePtcsNo);
                    }
                }

                HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@class='op_tbl_type10_1']/tbody/tr");
                if (ncTr == null) continue;
                foreach (HtmlNode tr in ncTr)
                {
                    HtmlNodeCollection ncTd = tr.SelectNodes("./th|./td");
                    if (ncTd == null) continue;
                    if (ncTd[0].InnerText.Trim() == "처분방식 / 자산구분")
                    {
                        Match match = Regex.Match(ncTd[0].SelectSingleNode("following-sibling::*[1]").InnerText.Trim(), @"(.*)/(.*)", rxOptM);
                        prptDvsn = match.Groups[2].Value.Trim();
                    }
                    if (ncTd[0].InnerText.Trim() == "면적")
                    {
                        areaInfo = ncTd[0].SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                        areaInfo = Regex.Replace(areaInfo, @"\s{2,}", " ", rxOptS).Trim();
                    }
                    if (ncTd[0].InnerText.Trim() == "입찰방식")
                    {
                        bidMtdNm = ncTd[0].SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                    }
                    if (ncTd[0].InnerText.Trim() == "배분요구종기")
                    {
                        shrDt = ncTd[0].SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                    }
                    if (ncTd[0].InnerText.Trim() == "최초공고일자")
                    {
                        iniDt = ncTd[0].SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                    }
                    if (ncTd[0].InnerText.Trim() == "공매대행의뢰기관")
                    {
                        dlgtOrg = ncTd[0].SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                    }
                    if (ncTd[0].InnerText.Trim() == "집행기관")
                    {
                        orgNm = ncTd[0].SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                        orgDvsn = (orgNm == "한국자산관리공사") ? "1" : "0";
                    }
                    if (ncTd[0].InnerText.Trim() == "담당자정보")
                    {
                        Match match = Regex.Match(ncTd[0].SelectSingleNode("following-sibling::*[1]").InnerText.Trim(), @"(.*)/(.*)/(.*)", rxOptS);
                        if (match.Success)
                        {
                            deptNm = match.Groups[1].Value.Trim();
                            pscgNm = match.Groups[2].Value.Trim();
                            pscgTpNo = match.Groups[3].Value.Trim();
                        }
                        else
                        {
                            match = Regex.Match(ncTd[0].SelectSingleNode("following-sibling::*[1]").InnerText.Trim(), @"(.*)/(.*)", rxOptS);
                            if (match.Success)
                            {
                                deptNm = match.Groups[1].Value.Trim();
                                pscgTpNo = match.Groups[2].Value.Trim();
                            }
                        }
                    }
                }

                //캠코 압류재산-입찰 전 알아야 할 주요사항
                //var jObjCaut = new JObject();
                if (prptDvsn == "압류재산(캠코)")
                {
                    HtmlNode ndPbx = doc.DocumentNode.SelectSingleNode("//div[@class='point_box']");
                    if (ndPbx != null)
                    {
                        sezNote1 = ndPbx.SelectSingleNode(".//span[@id='nonErsrRgtCntn']").InnerText.Trim();       //매각 유효내용
                        sezNote2 = ndPbx.SelectSingleNode(".//span[@id='stryGrndRgtVldCntn']").InnerText.Trim();   //매수인 자격내용
                        sezNote3 = ndPbx.SelectSingleNode(".//span[@id='pytnMtrs']").InnerText.Trim();             //유의 사항
                        //jObjCaut.Add("nonErsrRgtCntn", sezNote1);
                        //jObjCaut.Add("stryGrndRgtVldCntn", sezNote2);
                        //jObjCaut.Add("pytnMtrs", sezNote3);
                    }
                }
                //var jsCaut = (jObjCaut.Count > 0) ? JsonConvert.SerializeObject(jObjCaut) : "{}";

                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                urlTab = "http://www.onbid.co.kr/op/cta/cltrdtl/collateralRealEstateTabDetail.do?cltrHstrNo=" + cltrHstrNo + "&cltrNo=" + cltrNo + "&plnmNo=" + plnmNo + "&pbctNo=" + pbctNo + "&pbctCdtnNo=" + pbctCdtnNo + "&bidMnmtNo=" + bidMnmtNo + "&selectValue=&";

                //물건탭-물건 세부 정보(001)
                url = urlTab + "dtButtonTab=001";
                jsonData = net.GetHtml(url, Encoding.UTF8);
                if (jsonData.Contains("요청하신 페이지를 찾을 수 없거나") || jsonData.Contains("작업 시간이 초과")) continue;
                dynamic x = JsonConvert.DeserializeObject(jsonData);
                var LandVO = x["resultLandVO"];
                var jsApsl = x["resultApslList"];
                var jsArea = x["resultBuildingList"];
                if (jsApsl != null) jsApsl = Regex.Replace(jsApsl.ToString(), @"\s{2,}", " ", rxOptS);
                //else jsApsl = "[]";
                if (jsArea != null) jsArea = Regex.Replace(jsArea.ToString(), @"\s{2,}", " ", rxOptS);
                if (LandVO != null)
                {
                    nmrdAdrs = (LandVO.nmrdAdrs == null) ? "" : LandVO.nmrdAdrs;
                    posiEnvPscd = (LandVO.posiEnvPscd == null) ? "" : LandVO.posiEnvPscd;
                    utlzPscd = (LandVO.utlzPscd == null) ? "" : LandVO.utlzPscd;
                    etcDtlCntn = (LandVO.etcDtlCntn == null) ? "" : LandVO.etcDtlCntn;
                    atctIvstDt = (LandVO.atctIvstDt == null) ? "" : LandVO.atctIvstDt;
                    esctYn = (LandVO.esctYn == null) ? "" : LandVO.esctYn;
                    elvtYn = (LandVO.elvtYn == null) ? "" : LandVO.elvtYn;
                    shrYn = (LandVO.shrYn == null) ? "" : LandVO.shrYn;
                    pkltYn = (LandVO.pkltYn == null) ? "" : LandVO.pkltYn;
                    bldgNm = (LandVO.bldgNm == null) ? "" : LandVO.bldgNm;
                    dong = (LandVO.dong == null) ? "" : LandVO.dong;
                    flr = "";
                    hous = "";
                    qty = "";
                    pcmtPymtEpdtCntn = (LandVO.pcmtPymtEpdtCntn == null) ? "" : LandVO.pcmtPymtEpdtCntn;
                    bidPrgnNft = (LandVO.bidPrgnNft == null) ? "" : LandVO.bidPrgnNft;
                    dlvrRsby = (LandVO.dlvrRsby == null) ? "" : LandVO.dlvrRsby;
                    icdlCdtn = (LandVO.icdlCdtn == null) ? "" : LandVO.icdlCdtn;
                }

                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                //물건탭-입찰 정보(002)
                url = urlTab + "dtButtonTab=002";
                jsonData = net.GetHtml(url, Encoding.UTF8);
                if (jsonData.Contains("요청하신 페이지를 찾을 수 없거나") || jsonData.Contains("작업 시간이 초과")) continue;
                x = JsonConvert.DeserializeObject(jsonData);
                var PvctVO = x["resultPvctVO"];
                var jsPbct = x["resultPbctlList"];
                if (jsPbct != null) jsPbct = Regex.Replace(jsPbct.ToString(), @"\s{2,}", " ", rxOptS);

                var jObjRstn = new JObject();
                if (PvctVO != null)
                {
                    eltrGrtDocUseYn = (PvctVO.eltrGrtDocUseYn == null) ? "" : PvctVO.eltrGrtDocUseYn;           //전자보증서 사용 가능 여부
                    nextRnkRqstPsblYn = (PvctVO.nextRnkRqstPsblYn == null) ? "" : PvctVO.nextRnkRqstPsblYn;     //차순위 매수신청 가능 여부
                    comnBidPmsnYn = (PvctVO.comnBidPmsnYn == null) ? "" : PvctVO.comnBidPmsnYn;                 //공동입찰 가능 여부
                    twpsLsthUsbdYn = (PvctVO.twpsLsthUsbdYn == null) ? "" : PvctVO.twpsLsthUsbdYn;              //2인 미만 유찰 여부(N-1인이 입찰하더라도 유효한 입찰로 성립, Y-2인 이상 입찰자가 있는 경우에만 유효한 입찰로 성립)
                    subtBidPmsnYn = (PvctVO.subtBidPmsnYn == null) ? "" : PvctVO.subtBidPmsnYn;                 //대리입찰 가능 여부
                    twtmGthrBidPsblYn = (PvctVO.twtmGthrBidPsblYn == null) ? "" : PvctVO.twtmGthrBidPsblYn;     //2회 이상 입찰 가능 여부(Y-동일물건 2회 이상 입찰 가능)                        
                    prcdBuyTgtYn = (PvctVO.prcdBuyTgtYn == null) ? "" : PvctVO.prcdBuyTgtYn;                    //공유자 우선매수 가능 여부(압류재산)
                    othrCltrBidPsblYn = (PvctVO.othrCltrBidPsblYn == null) ? "" : PvctVO.othrCltrBidPsblYn;     //공고 내 타물건 추첨신청 가능여부(Y-동일공고 내 N개 물건 입찰가능, N-동일공고 내 N개 물건만 입찰가능) -> 미사용
                    bidPsblCltrCnt = (PvctVO.bidPsblCltrCnt == null) ? "" : PvctVO.bidPsblCltrCnt;              //위의 N개 -> 미사용

                    jObjRstn.Add("eltrGrtDocUseYn", eltrGrtDocUseYn);
                    jObjRstn.Add("nextRnkRqstPsblYn", nextRnkRqstPsblYn);
                    jObjRstn.Add("comnBidPmsnYn", comnBidPmsnYn);
                    jObjRstn.Add("twpsLsthUsbdYn", twpsLsthUsbdYn);
                    jObjRstn.Add("subtBidPmsnYn", subtBidPmsnYn);
                    jObjRstn.Add("twtmGthrBidPsblYn", twtmGthrBidPsblYn);
                    jObjRstn.Add("prcdBuyTgtYn", prcdBuyTgtYn);
                    jObjRstn.Add("othrCltrBidPsblYn", othrCltrBidPsblYn);
                    jObjRstn.Add("bidPsblCltrCnt", bidPsblCltrCnt);
                }
                var jsRstn = (jObjRstn.Count > 0) ? JsonConvert.SerializeObject(jObjRstn) : null;

                if (ctgrFullNm != string.Empty)
                {
                    Match match = Regex.Match(ctgrFullNm, @"^\w+[\s]*/[\s]*(.*)");
                    var xCat = dtCatCd.Rows.Cast<DataRow>().Where(t => t["cat3_nm"].ToString() == match.Groups[1].Value).SingleOrDefault();
                    cat3 = (xCat == null) ? string.Empty : xCat.Field<ulong>("cat3_cd").ToString();
                    if (cat3 != string.Empty)
                    {
                        cat2 = cat3.Substring(0, 8);
                        cat1 = cat3.Substring(0, 4);
                    }
                }

                db.Open();
                cvp = "plnm_no=@plnm_no, pbct_no=@pbct_no, cdtn_no=@cdtn_no, hstr_no=@hstr_no, minb_amt=@minb_amt, stat_nm=@stat_nm, "+
                    "org_dvsn=@org_dvsn, bmgmt_no=@bmgmt_no, cltr_nm=@cltr_nm, cmgmt_no=@cmgmt_no, land_adrs=@land_adrs, road_adrs=@road_adrs, dpsl_cd=@dpsl_cd, apsl_amt=@apsl_amt, bgn_dtm=@bgn_dtm, cls_dtm=@cls_dtm, " +
                    "cat1=@cat1, cat2=@cat2, cat3=@cat3, fb_cnt=@fb_cnt, iqry_cnt=@iqry_cnt, goods_nm=@goods_nm, org_nm=@org_nm, dpt_nm=@dpt_nm, prpt_dvsn=@prpt_dvsn, land_sqm=@land_sqm, bldg_sqm=@bldg_sqm, _ctgr=@ctgr, sucb_amt=0, sucb_rate='' ";
                sql = "insert into tb_list set cltr_no=@cltr_no, 1st_dt=curdate(), " + cvp + " ON DUPLICATE KEY UPDATE " + cvp + ", mod_dtm=now()";
                sp.Add(new MySqlParameter("@cltr_no", cltrNo));
                sp.Add(new MySqlParameter("@plnm_no", plnmNo));
                sp.Add(new MySqlParameter("@pbct_no", pbctNo));
                sp.Add(new MySqlParameter("@cdtn_no", pbctCdtnNo));
                sp.Add(new MySqlParameter("@hstr_no", cltrHstrNo));
                sp.Add(new MySqlParameter("@minb_amt", minbAmt));
                sp.Add(new MySqlParameter("@stat_nm", statNm));
                sp.Add(new MySqlParameter("@org_dvsn", orgDvsn));
                sp.Add(new MySqlParameter("@bmgmt_no", bidMnmtNo));
                sp.Add(new MySqlParameter("@cltr_nm", cltrNm.Trim()));
                sp.Add(new MySqlParameter("@cmgmt_no", cltrMnmtNo1));
                sp.Add(new MySqlParameter("@land_adrs", ldnmAdrs.Trim()));
                sp.Add(new MySqlParameter("@road_adrs", nmrdAdrs.Trim()));
                sp.Add(new MySqlParameter("@dpsl_cd", dpslMtdCd));
                sp.Add(new MySqlParameter("@apsl_amt", apslAsesAvgAmt));
                sp.Add(new MySqlParameter("@bgn_dtm", pbctBegnDtm));
                sp.Add(new MySqlParameter("@cls_dtm", pbctLastClsDtm));
                sp.Add(new MySqlParameter("@cat1", cat1));
                sp.Add(new MySqlParameter("@cat2", cat2));
                sp.Add(new MySqlParameter("@cat3", cat3));
                sp.Add(new MySqlParameter("@fb_cnt", uscbdCnt));
                sp.Add(new MySqlParameter("@iqry_cnt", iqryCnt));
                sp.Add(new MySqlParameter("@goods_nm", areaInfo));
                sp.Add(new MySqlParameter("@org_nm", orgNm));
                sp.Add(new MySqlParameter("@dpt_nm", deptNm));
                sp.Add(new MySqlParameter("@prpt_dvsn", prptDvsn));
                sp.Add(new MySqlParameter("@land_sqm", landSqms));
                sp.Add(new MySqlParameter("@bldg_sqm", bldSqms));
                sp.Add(new MySqlParameter("@ctgr", ctgrFullNm));
                db.ExeQry(sql, sp);
                sp.Clear();

                cvp = "bid_nm=@bid_nm, pscg_nm=@pscg_nm, pscg_tpno=@pscg_tpno, dlgt_org=@dlgt_org, posi_env=@posi_env, utlz_pscd=@utlz_pscd, etc_dtl=@etc_dtl, ivst_dt=@ivst_dt, esct_yn=@esct_yn, elvt_yn=@elvt_yn, shr_yn=@shr_yn, park_yn=@park_yn, " +
                    "bldg_nm=@bldg_nm, dong=@dong, flr=@flr, hous=@hous, qty=@qty, pymt_epdt=@pymt_epdt, bid_nft=@bid_nft, dlvr_rsby=@dlvr_rsby, icdl_cdtn=@icdl_cdtn, shr_dt=@shr_dt, ini_dt=@ini_dt, sez_note1=@sez_note1, sez_note2=@sez_note2, sez_note3=@sez_note3, file_no=@file_no, attach_no=@attach_no";
                    //"js_area=@js_area, js_apsl=@js_apsl, js_pbct=@js_pbct, js_rstn=@js_rstn";

                //if (jsArea != null) jsArea = jsArea.ToString().Replace("[]", string.Empty);
                //if (jsApsl != null) jsApsl = jsApsl.ToString().Replace("[]", string.Empty);
                //if (jsPbct != null) jsPbct = jsPbct.ToString().Replace("[]", string.Empty);
                //if (jsRstn != null) jsRstn = jsRstn.ToString().Replace("[]", string.Empty);

                if (jsArea == null) jsArea = "[]";
                if (jsApsl == null) jsApsl = "[]";
                if (jsPbct == null) jsPbct = "[]";
                if (jsRstn == null) jsRstn = "{}";

                sql = "insert into tb_dtl set cltr_no=@cltr_no, " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                sp.Add(new MySqlParameter("@cltr_no", cltrNo));
                sp.Add(new MySqlParameter("@bid_nm", bidMtdNm));
                sp.Add(new MySqlParameter("@pscg_nm", pscgNm));
                sp.Add(new MySqlParameter("@pscg_tpno", pscgTpNo));
                sp.Add(new MySqlParameter("@dlgt_org", dlgtOrg));
                sp.Add(new MySqlParameter("@posi_env", posiEnvPscd.Trim()));
                sp.Add(new MySqlParameter("@utlz_pscd", utlzPscd.Trim()));
                sp.Add(new MySqlParameter("@etc_dtl", etcDtlCntn.Trim()));
                sp.Add(new MySqlParameter("@ivst_dt", atctIvstDt));
                sp.Add(new MySqlParameter("@esct_yn", esctYn));
                sp.Add(new MySqlParameter("@elvt_yn", elvtYn));
                sp.Add(new MySqlParameter("@shr_yn", shrYn));
                sp.Add(new MySqlParameter("@park_yn", pkltYn));
                sp.Add(new MySqlParameter("@bldg_nm", bldgNm.Trim()));
                sp.Add(new MySqlParameter("@dong", dong));
                sp.Add(new MySqlParameter("@flr", flr));
                sp.Add(new MySqlParameter("@hous", hous));
                sp.Add(new MySqlParameter("@qty", qty));
                sp.Add(new MySqlParameter("@pymt_epdt", pcmtPymtEpdtCntn.Trim()));
                sp.Add(new MySqlParameter("@bid_nft", bidPrgnNft));
                sp.Add(new MySqlParameter("@dlvr_rsby", dlvrRsby.Trim()));
                sp.Add(new MySqlParameter("@icdl_cdtn", icdlCdtn.Trim()));
                sp.Add(new MySqlParameter("@shr_dt", shrDt));
                sp.Add(new MySqlParameter("@ini_dt", iniDt));
                sp.Add(new MySqlParameter("@sez_note1", sezNote1));
                sp.Add(new MySqlParameter("@sez_note2", sezNote2));
                sp.Add(new MySqlParameter("@sez_note3", sezNote3));
                //sp.Add(new MySqlParameter("@js_area", jsArea));
                //sp.Add(new MySqlParameter("@js_apsl", jsApsl));
                //sp.Add(new MySqlParameter("@js_pbct", jsPbct));
                //sp.Add(new MySqlParameter("@js_rstn", jsRstn));
                sp.Add(new MySqlParameter("@file_no", string.Join("|", fileList.ToArray())));
                sp.Add(new MySqlParameter("@attach_no", plnmAtchFilePtcsNo));
                db.ExeQry(sql, sp);
                sp.Clear();

                if (row["prcMode"].ToString() == "1") nCnt++;   //신건
                else uCnt++;    //갱신

                //면적정보
                sql = "delete from tb_area where cltr_no='" + cltrNo + "'";
                db.ExeQry(sql);
                if (!string.IsNullOrEmpty(jsArea))
                {
                    JArray jaArea = JArray.Parse(jsArea);
                    foreach (JObject item in jaArea)
                    {
                        sql = "insert into tb_area set cltr_no=@cltr_no, cltr_seq=@cltr_seq, hstr_no=@hstr_no, rlst_cltr_no=@rlst_cltr_no, usg_nm=@usg_nm, sqms=@sqms, unit=@unit, shr_rt=@shr_rt, dvsn_cd=@dvsn_cd, dvsn_nm=@dvsn_nm, note=@note, del_yn=@del_yn, pin=@pin";
                        sp.Add(new MySqlParameter("@cltr_no", cltrNo));
                        sp.Add(new MySqlParameter("@cltr_seq", item["dtlsCltrSeq"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@hstr_no", item["cltrHstrNo"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@rlst_cltr_no", item["rlstDtlsCltrNo"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@usg_nm", item["cltrUsgNm"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@sqms", item["sqms"]?.ToString().Replace(",", "") ?? ""));
                        sp.Add(new MySqlParameter("@unit", item["unit"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@shr_rt", item["shrRt"]?.ToString().Replace(",", "") ?? ""));
                        sp.Add(new MySqlParameter("@dvsn_cd", item["dtlsCltrAstDvsnCd"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@dvsn_nm", item["dtlsCltrAstDvsnNm"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@note", item["dtlsNm"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@del_yn", item["delYn"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@pin", item["rlstRgstNo"]?.ToString() ?? ""));    //2021-09-23 등기 pin 추가
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                    jaArea.Clear();
                }

                //감정평가정보
                sql = "delete from tb_apsl where cltr_no='" + cltrNo + "'";
                db.ExeQry(sql);
                if (!string.IsNullOrEmpty(jsApsl))
                {
                    JArray jaApsl = JArray.Parse(jsApsl);
                    foreach (JObject item in jaApsl)
                    {
                        sql = "insert into tb_apsl set cltr_no=@cltr_no, hstr_no=@hstr_no, ases_ptcs_no=@ases_ptcs_no, amt=@amt, dt=@dt, org_nm=@org_nm, atch_file_no=@atch_file_no, atch_seq=@atch_seq";
                        sp.Add(new MySqlParameter("@cltr_no", cltrNo));
                        sp.Add(new MySqlParameter("@hstr_no", item["cltrHstrNo"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@ases_ptcs_no", item["apslAsesPtcsNo"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@amt", item["apslAsesAmt"]?.ToString().Replace(",", "") ?? ""));
                        sp.Add(new MySqlParameter("@dt", item["apslAsesDt"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@org_nm", item["apslAsesOrgNm"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@atch_file_no", item["atchFilePtcsNo"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@atch_seq", item["atchSeq"]?.ToString() ?? ""));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                    jaApsl.Clear();
                }

                //입찰일정
                sql = "delete from tb_pbct where cltr_no='" + cltrNo + "'";
                db.ExeQry(sql);
                if (!string.IsNullOrEmpty(jsPbct))
                {
                    JArray jaPbct = JArray.Parse(jsPbct);
                    foreach (JObject item in jaPbct)
                    {
                        sql = "insert into tb_pbct set cltr_no=@cltr_no, hstr_no=@hstr_no, plnm_no=@plnm_no, pbct_no=@pbct_no, pbct_cdtn_no=@pbct_cdtn_no, bid_mnmt_no=@bid_mnmt_no, pbct_seq=@pbct_seq, pbct_dgr=@pbct_dgr, minb_amt=@minb_amt, " +
                            "bid_dvsn_nm=@bid_dvsn_nm, pymt_mtd_cntn=@pymt_mtd_cntn, pymt_epdt_cntn=@pymt_epdt_cntn, bgn_dtm=@bgn_dtm, cls_dtm=@cls_dtm, exct_dtm=@exct_dtm, dcsn_dtm=@dcsn_dtm, opbd_plc_cntn=@opbd_plc_cntn";
                        sp.Add(new MySqlParameter("@cltr_no", cltrNo));
                        sp.Add(new MySqlParameter("@hstr_no", item["cltrHstrNo"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@plnm_no", item["plnmNo"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@pbct_no", item["pbctNo"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@pbct_cdtn_no", item["pbctCdtnNo"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@bid_mnmt_no", item["bidMnmtNo"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@pbct_seq", item["pbctSeq"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@pbct_dgr", item["pbctDgr"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@minb_amt", item["minBidPrc"]?.ToString().Replace(",", "") ?? ""));
                        sp.Add(new MySqlParameter("@bid_dvsn_nm", item["bidDvsnNm"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@pymt_mtd_cntn", item["pcmtPymtMtdCntn"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@pymt_epdt_cntn", item["pcmtPymtEpdtCntn"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@bgn_dtm", item["pbctBegnDtm"]?.ToString() + ":00" ?? ""));
                        sp.Add(new MySqlParameter("@cls_dtm", item["pbctClsDtm"]?.ToString() + ":00" ?? ""));
                        sp.Add(new MySqlParameter("@exct_dtm", item["pbctExctDtm"]?.ToString() + ":00" ?? ""));
                        sp.Add(new MySqlParameter("@dcsn_dtm", item["dpslDcsnDtm"]?.ToString() + ":00" ?? ""));
                        sp.Add(new MySqlParameter("@opbd_plc_cntn", item["opbdPlcCntn"]?.ToString() ?? ""));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                    jaPbct.Clear();
                }
                db.Close();

                if (prptDvsn != "압류재산(캠코)") continue;

                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                //물건탭-압류재산 정보(011)
                url = urlTab + "dtButtonTab=011";
                jsonData = net.GetHtml(url, Encoding.UTF8);
                x = JsonConvert.DeserializeObject(jsonData);

                //1-임대차 정보
                dynamic jsLeas = "[]";
                if (x["paginationInfo1"] != null)
                {
                    pgCnt = (int)x["paginationInfo1"]["totalPageCount"];
                    jsLeas = x["resultLeasImfoList"];
                    if (jsLeas != null)
                    {
                        jsLeas = Regex.Replace(jsLeas.ToString(), @"[\[\]]", string.Empty, rxOptS).Trim();
                        if (pgCnt > 1)
                        {
                            for (i = 2; i <= pgCnt; i++)
                            {
                                jsonData = net.GetHtml(urlTab + "&pageJsFunction=fn_changTab&pageType=paging&gubun=paging1&pageIndex1=" + i.ToString() + "&dtButtonTab=011", Encoding.UTF8);
                                x = JsonConvert.DeserializeObject(jsonData);
                                //jsLeas = x["resultLeasImfoList"];
                                if (x["resultLeasImfoList"] != null)
                                {
                                    jsLeas += "," + Regex.Replace(x["resultLeasImfoList"].ToString(), @"[\[\]]", string.Empty, rxOptS).Trim();
                                }
                            }
                        }
                        jsLeas = "[" + jsLeas + "]";
                    }
                }

                //2-등기 주요정보
                dynamic jsRgst = "[]";
                if (x["paginationInfo2"] != null)
                {
                    pgCnt = (int)x["paginationInfo2"]["totalPageCount"];
                    jsRgst = x["resultRgstImfoList"];
                    if (jsRgst != null)
                    {
                        jsRgst = Regex.Replace(jsRgst.ToString(), @"[\[\]]", string.Empty, rxOptS).Trim();
                        if (pgCnt > 1)
                        {
                            for (i = 2; i <= pgCnt; i++)
                            {
                                jsonData = net.GetHtml(urlTab + "&pageJsFunction=fn_changTab&pageType=paging&gubun=paging2&pageIndex2=" + i.ToString() + "&dtButtonTab=011", Encoding.UTF8);
                                x = JsonConvert.DeserializeObject(jsonData);
                                //jsRgst = x["resultRgstImfoList"];
                                if (x["resultRgstImfoList"] != null)
                                {
                                    jsRgst += "," + Regex.Replace(x["resultRgstImfoList"].ToString(), @"[\[\]]", string.Empty, rxOptS).Trim();
                                }
                            }
                        }
                        jsRgst = "[" + jsRgst + "]";
                    }
                }
                /*
                //3-권리분석 기초정보(배분요구 및 채권신고 현황)-입찰시작 7일전부터 제공
                dynamic jsShr = "[]";
                if (x["paginationInfo3"] != null)
                {
                    pgCnt = (int)x["paginationInfo3"]["totalPageCount"];
                    jsShr = x["resultShrImfoList"];
                    if (jsShr != null)
                    {
                        jsShr = Regex.Replace(jsShr.ToString(), @"[\[\]]", string.Empty, rxOptS).Trim();
                        if (pgCnt > 1)
                        {
                            for (i = 2; i <= pgCnt; i++)
                            {
                                jsonData = net.GetHtml(urlTab + "&pageJsFunction=fn_changTab&pageType=paging&gubun=paging3&pageIndex3=" + i.ToString() + "&dtButtonTab=011", Encoding.UTF8);
                                x = JsonConvert.DeserializeObject(jsonData);
                                //jsShr = x["resultShrImfoList"];
                                if (x["resultShrImfoList"] != null)
                                {
                                    jsShr += "," + Regex.Replace(x["resultShrImfoList"].ToString(), @"[\[\]]", string.Empty, rxOptS).Trim();
                                }
                            }
                        }
                        jsShr = "[" + jsShr + "]";
                    }
                }

                //4-권리분석 기초정보(점유관계)-입찰시작 7일전부터 제공
                dynamic jsLsd = "[]";
                if (x["paginationInfo4"] != null)
                {
                    pgCnt = (int)x["paginationInfo4"]["totalPageCount"];
                    jsLsd = x["resultLsdImfoList"];
                    if (jsLsd != null)
                    {
                        jsLsd = Regex.Replace(jsLsd.ToString(), @"[\[\]]", string.Empty, rxOptS).Trim();
                        if (pgCnt > 1)
                        {
                            for (i = 2; i <= pgCnt; i++)
                            {
                                jsonData = net.GetHtml(urlTab + "&pageJsFunction=fn_changTab&pageType=paging&gubun=paging4&pageIndex4=" + i.ToString() + "&dtButtonTab=011", Encoding.UTF8);
                                x = JsonConvert.DeserializeObject(jsonData);
                                //jsLsd = x["resultLsdImfoList"];
                                if (x["resultLsdImfoList"] != null)
                                {
                                    jsLsd += "," + Regex.Replace(x["resultLsdImfoList"].ToString(), @"[\[\]]", string.Empty, rxOptS).Trim();
                                }
                            }
                        }
                        jsLsd = "[" + jsLsd + "]";
                    }
                }                    
                colVal = "js_caut=@js_caut, js_leas=@js_leas, js_rgst=@js_rgst, js_shr=@js_shr, js_lsd=@js_lsd, wdt=curdate()";
                */
                /*
                cvp = "js_caut=@js_caut, js_leas=@js_leas, js_rgst=@js_rgst, wdt=curdate()";
                sql = "insert into tb_sez set cltr_no=@cltr_no, " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                sp.Add(new MySqlParameter("@cltr_no", cltrNo));
                sp.Add(new MySqlParameter("@js_caut", jsCaut));
                sp.Add(new MySqlParameter("@js_leas", jsLeas));
                sp.Add(new MySqlParameter("@js_rgst", jsRgst));
                //sp.Add(new MySqlParameter("@js_shr", jsShr));
                //sp.Add(new MySqlParameter("@js_lsd", jsLsd));
                db.Open();
                db.ExeQry(sql, sp);
                sp.Clear();
                db.Close();
                */
                db.Open();
                //임대차 정보
                sql = "delete from tb_leas where cltr_no='" + cltrNo + "'";
                db.ExeQry(sql);
                if (!string.IsNullOrEmpty(jsLeas))
                {
                    JArray jaLeas = JArray.Parse(jsLeas);
                    foreach (JObject item in jaLeas)
                    {
                        sql = "insert into tb_leas set cltr_no=@cltr_no, hstr_no=@hstr_no, row_no=@row_no, irst_dvsn_nm=@irst_dvsn_nm, irst_irps_nm=@irst_irps_nm, tdps_amt=@tdps_amt, mthr_amt=@mthr_amt, conv_grt_mony=@conv_grt_mony, fix_dt=@fix_dt, mvn_dt=@mvn_dt";
                        sp.Add(new MySqlParameter("@cltr_no", cltrNo));
                        sp.Add(new MySqlParameter("@hstr_no", item["cltrHstrNo"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@row_no", item["rn"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@irst_dvsn_nm", item["irstDvsnNm"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@irst_irps_nm", item["irstIrpsNm"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@tdps_amt", item["tdpsAmt"]?.ToString().Replace(",", "") ?? ""));
                        sp.Add(new MySqlParameter("@mthr_amt", item["mthrAmt"]?.ToString().Replace(",", "") ?? ""));
                        sp.Add(new MySqlParameter("@conv_grt_mony", item["convGrtMony"]?.ToString().Replace(",", "") ?? ""));
                        sp.Add(new MySqlParameter("@fix_dt", item["fixDt"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@mvn_dt", item["mvnDt"]?.ToString() ?? ""));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                    jaLeas.Clear();
                }

                //등기 주요정보
                sql = "delete from tb_rgst where cltr_no='" + cltrNo + "'";
                db.ExeQry(sql);
                if (!string.IsNullOrEmpty(jsRgst))
                {
                    JArray jaRgst = JArray.Parse(jsRgst);
                    foreach (JObject item in jaRgst)
                    {
                        sql = "insert into tb_rgst set cltr_no=@cltr_no, row_no=@row_no, irst_dvsn_nm=@irst_dvsn_nm, irst_irps_nm=@irst_irps_nm, rgst_dt=@rgst_dt, stup_amt=@stup_amt";
                        sp.Add(new MySqlParameter("@cltr_no", cltrNo));
                        sp.Add(new MySqlParameter("@row_no", item["rn"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@irst_dvsn_nm", item["irstDvsnNm"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@irst_irps_nm", item["irstIrpsNm"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@rgst_dt", item["rgstDt"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@stup_amt", item["stupAmt"]?.ToString().Replace(",", "") ?? ""));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                    jaRgst.Clear();
                }
                db.Close();
            } //dtLs.Rows 끝

            atomLog.AddLog(string.Format("물건등록 신건-{0}, 갱신-{1}", nCnt, uCnt));
        }

        /// <summary>
        /// 주소/좌표/주민센터/관할법원 처리
        /// </summary>
        private void Proc_Coord()
        {
            string sql, sDt, cltrNo;
            string landAdrs, roadAdrs, sidoCd = "", gugunCd = "", dongCd = "", riCd = "", hCd = "", pnu = "", zoneNo = "", x = "", y = "", csCd = "", siguCd = "", mt = "";
            decimal year = 0, mnth = 0;
            decimal sCnt = 0, fCnt = 0;
            int totCnt = 0, curCnt = 0;
                        
            HAPDoc doc = new HAPDoc();
            List<MySqlParameter> sp = new List<MySqlParameter>();

            IDictionary<string, string> dict = new Dictionary<string, string>();

            //관할법원-시/도코드
            Dictionary<string, string> dicCS = new Dictionary<string, string>();
            //sql = "select concat(crt_cd,spt_cd) as cs_cd, sigu_cd from ta_cd_cs";
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

            sDt = string.Format("{0}-{1}-{2}", year, mnth.ToString().PadLeft(2, '0'), "01");
            sql = "select cltr_no, land_adrs, road_adrs from tb_list where 1st_dt=curdate() and x=0 order by cltr_no";
            //sql = "select cltr_no, land_adrs, road_adrs from tb_list where 1st_dt='2021-12-15' and x=0 order by cltr_no";
            DataTable dtL = db.ExeDt(sql);
            totCnt = dtL.Rows.Count;

            txtState.AppendText(string.Format("\r\n\r\n■■■■■     [주소/좌표/행정구역]     ■■■■■"));    //화면에 진행상태 표시
            foreach (DataRow row in dtL.Rows)
            {
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                cltrNo = row["cltr_no"].ToString();
                txtState.AppendText(string.Format("\r\n[XY] {0} -> {1} / {2}", cltrNo, curCnt, totCnt));    //화면에 진행상태 표시

                dict.Clear();
                landAdrs = Regex.Replace(row["land_adrs"].ToString(), @"[\s]{2,}", " ");
                roadAdrs = Regex.Replace(row["road_adrs"].ToString(), @"[\s]{2,}", " ").Trim();
                dict = api.DaumSrchAdrs(landAdrs);
                sidoCd = dict["sidoCd"];
                if (sidoCd == "" && roadAdrs != "")
                {
                    dict = api.DaumSrchAdrs(roadAdrs);
                    sidoCd = dict["sidoCd"];
                }
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
                    AdrsParser parser = new AdrsParser(landAdrs);
                    dict = api.DaumSrchAdrs(parser.AdrsM);
                    sidoCd = dict["sidoCd"];
                    if (sidoCd == "" && roadAdrs != "")
                    {
                        parser = new AdrsParser(roadAdrs);
                        dict = api.DaumSrchAdrs(parser.AdrsM);
                        sidoCd = dict["sidoCd"];
                    }
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

                db.Open();
                sql = "update tb_list set si_cd=@si_cd, gu_cd=@gu_cd, dn_cd=@dn_cd, ri_cd=@ri_cd, hj_cd=@hj_cd, pnu=@pnu, x=@x, y=@y, zone_no=@zone_no, cs_cd=@cs_cd," +
                    "mt=@mt, m_adrs_no=@m_adrs_no, s_adrs_no=@s_adrs_no, m_bldg_no=@m_bldg_no, s_bldg_no=@s_bldg_no, bldg_nm=@bldg_nm, road_nm=@road_nm where cltr_no=" + cltrNo;
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
                sp.Add(new MySqlParameter("@mt", mt));
                sp.Add(new MySqlParameter("@m_adrs_no", dict["jbNoM"]));
                sp.Add(new MySqlParameter("@s_adrs_no", dict["jbNoS"]));
                sp.Add(new MySqlParameter("@m_bldg_no", dict["bldgNoM"]));
                sp.Add(new MySqlParameter("@s_bldg_no", dict["bldgNoS"]));
                sp.Add(new MySqlParameter("@bldg_nm", dict["bldgNm"]));
                sp.Add(new MySqlParameter("@road_nm", dict["rdNm"]));
                db.ExeQry(sql, sp);
                sp.Clear();
                db.Close();

                if (x == "" || x == "0") fCnt++;
                else sCnt++;
            }

            atomLog.AddLog(string.Format("좌표매칭 성공-{0}, 실패-{1}", sCnt, fCnt));
        }

        /// <summary>
        /// 등기 자동발급 대상처리
        /// </summary>
        private void Prc_RgstIssueAdd()
        {
            string sql, tid, pinLand, pinBldg, pin, prevPin, prevTid = "";
            bool autoExist;
            int landCnt = 0, bldgCnt = 0, issueCnt = 0;
            string autoDvsn = "21";     //발급 구분 -> 캠코 물건

            DataTable dt = new DataTable();
            dt.Columns.Add("tid");
            dt.Columns.Add("lsType");
            dt.Columns.Add("pin");

            sql = "select L.cltr_no as tid, _ctgr, A.sqms, A.dvsn_nm as dvsn, A.pin from tb_list L, tb_dtl D, tb_area A " +
                "where L.cltr_no=D.cltr_no and D.cltr_no=A.cltr_no and " +
                "1st_dt=curdate() and org_dvsn=1 and dpsl_cd=1 and cat3 > 0 and A.pin != '' and pin_land='' and pin_bldg=''  and (A.dvsn_nm in ('토지','건물')) " +
                "order by L.cltr_no";
            //sql = "select L.cltr_no as tid, _ctgr, A.sqms, A.dvsn_nm as dvsn, A.pin from tb_list L, tb_dtl D, tb_area A where L.cltr_no=D.cltr_no and D.cltr_no=A.cltr_no and 1st_dt='2022-04-27' and org_dvsn=1 and dpsl_cd=1 and cat3 > 0 and A.pin != '' and (A.dvsn_nm in ('토지','건물')) order by L.cltr_no";
            DataTable dtLs = db.ExeDt(sql);

            foreach (DataRow row in dtLs.Rows)
            {
                tid = row["tid"].ToString();
                if (tid == prevTid) continue;

                DataRow[] rows = dtLs.Select($"tid={tid}");
                if (rows.Count() == 1)
                {
                    DataRow r = rows[0];
                    dt.Rows.Add(new object[] { r["tid"], r["dvsn"], r["pin"] });    //대상

                    prevTid = tid;
                    continue;
                }

                DataTable dtS = rows.CopyToDataTable();
                DataTable dtL = dtS.Clone();
                DataTable dtB = dtS.Clone();
                if (dtS.Rows.Cast<DataRow>().Where(t => t["dvsn"].ToString() == "토지").Count() > 0)
                {
                    dtL = dtS.Rows.Cast<DataRow>().Where(t => t["dvsn"].ToString() == "토지").OrderByDescending(t => t["sqms"])?.CopyToDataTable();
                }
                if (dtS.Rows.Cast<DataRow>().Where(t => t["dvsn"].ToString() == "건물").Count() > 0)
                {
                    dtB = dtS.Rows.Cast<DataRow>().Where(t => t["dvsn"].ToString() == "건물").OrderByDescending(t => t["sqms"])?.CopyToDataTable();
                }
                landCnt = dtL.Rows.Count;
                bldgCnt = dtB.Rows.Count;

                //A-토지만
                if (landCnt > 0 && bldgCnt == 0)
                {
                    if (dtL.Rows.Count > 0)
                    {
                        DataRow r = dtL.Rows[0];
                        if (r != null)
                        {
                            dt.Rows.Add(new object[] { r["tid"], r["dvsn"], r["pin"] });    //대상
                        }
                    }
                }

                //B-건물만
                if (bldgCnt > 0 && landCnt == 0)
                {
                    if (dtB.Rows.Count > 0)
                    {
                        DataRow r = dtB.Rows[0];
                        if (r != null)
                        {
                            dt.Rows.Add(new object[] { r["tid"], r["dvsn"], r["pin"] });    //대상
                        }
                    }
                }

                //토지건물 각각 단일
                if (landCnt == 1 && bldgCnt == 1)
                {
                    pinLand = dtL.Rows[0]["pin"].ToString();
                    pinBldg = dtB.Rows[0]["pin"].ToString();
                    if (pinLand == pinBldg)
                    {
                        dt.Rows.Add(new object[] { tid, "건물", pinBldg });    //대상
                    }
                    else
                    {
                        dt.Rows.Add(new object[] { tid, "토지", pinLand });    //대상
                        dt.Rows.Add(new object[] { tid, "건물", pinBldg });    //대상
                    }
                }
                else
                {
                    prevPin = String.Empty;
                    bool singlePin = true;
                    foreach (DataRow dr in dtS.Rows)
                    {
                        pin = dr["pin"].ToString();
                        if (prevPin != String.Empty && pin != prevPin)
                        {
                            singlePin = false;
                            break;
                        }
                        prevPin = pin;
                    }

                    if (singlePin)
                    {
                        dt.Rows.Add(new object[] { tid, "건물", prevPin });    //대상
                    }
                }

                prevTid = tid;
            }

            //대상 db 일괄 추가
            if (dt.Rows.Count > 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    if (Regex.IsMatch(row["pin"].ToString(), @"\d{14}") == false) continue;

                    db.Open();
                    autoExist = db.ExistRow($"select idx from db_tank.tx_rgst_auto where (dvsn between 20 and 23) and tid='{row["tid"]}' and pin='{row["pin"]}' and wdt=curdate() limit 1");
                    if (!autoExist)
                    {
                        if (DateTime.Now.Hour < 8)
                        {
                            db.ExeQry($"insert into db_tank.tx_rgst_auto set dvsn='{autoDvsn}', tid='{row["tid"]}', ls_no='', ls_type='{row["lsType"]}', pin='{row["pin"]}', wdt=curdate(), wtm='07:30:00'");
                        }
                        else
                        {
                            db.ExeQry($"insert into db_tank.tx_rgst_auto set dvsn='{autoDvsn}', tid='{row["tid"]}', ls_no='', ls_type='{row["lsType"]}', pin='{row["pin"]}', wdt=curdate(), wtm=curtime()");
                        }

                        if (row["lsType"].ToString() == "토지")
                        {
                            db.ExeQry($"update db_main.tb_dtl set pin_land='{row["pin"]}' where cltr_no='{row["tid"]}'");
                        }
                        else if (row["lsType"].ToString() == "건물")
                        {
                            db.ExeQry($"update db_main.tb_dtl set pin_bldg='{row["pin"]}' where cltr_no='{row["tid"]}'");
                        }

                        issueCnt++;
                    }
                    db.Close();
                }
            }

            atomLog.AddLog($" > 발급 대상-{issueCnt}");
        }

        /// <summary>
        /// 입찰결과 처리
        /// </summary>
        private void Proc_BidRslt()
        {
            string html = "", url = "", sql = "";
            string sDt = "", eDt = "";
            string dbSucbAmt = "", dbHstrNo = "";
            string cltrNo = "", cltrHstrNo = "", plnmNo = "", pbctNo = "", pbctCdtnNo = "", pbctSeq = "", pbctDgr = "", sucbAmt = "", sucbRate = "", pbctCltrStatNm = "", pbctExctDtm = "";
            decimal i = 0, pdCnt = 0, pgCnt = 0;
            decimal sbCnt = 0, fbCnt = 0, obCnt = 0;
            bool pass;

            HAPDoc doc = new HAPDoc();
            List<MySqlParameter> sp = new List<MySqlParameter>();
            MySqlDataReader dr;

            sDt = DateTime.Today.AddDays(-2).ToShortDateString();
            eDt = DateTime.Today.ToShortDateString();
            url = "http://www.onbid.co.kr/op/bda/bidrslt/collateralRealEstateBidResultList.do?menuId=2051&searchCtgrId1=10000&searchCltrAdrsType=road&searchBidDateFrom=" + sDt + "&searchBidDateTo=" + eDt + "&searchOrderBy=01&pageUnit=100";

            webCnt++;
            if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
            txtState.AppendText(string.Format("\r\n\r\n■■■■■     [입찰결과] {0} ~ {1}    ■■■■■", sDt, eDt));    //화면에 진행상태 표시

            html = net.GetHtml(url + "&pageIndex=1", Encoding.UTF8);
            Match match = Regex.Match(html, @"<p>\[총\s+(\d+)건\]</p>", rxOptM);
            if (!match.Success) return;
            pdCnt = Convert.ToInt16(match.Groups[1].Value);
            if (pdCnt == 0) return;
            pgCnt = Math.Ceiling(pdCnt / (decimal)100);
            for (i = 1; i <= pgCnt; i++)
            {
                txtState.AppendText(string.Format("\r\n\r\n■■■■■     [Page] {0}    ■■■■■", i));    //화면에 진행상태 표시
                if (i > 1)
                {
                    webCnt++;
                    if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                    html = net.GetHtml(url + "&pageIndex=" + i.ToString(), Encoding.UTF8);
                }
                doc.LoadHtml(html);
                HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[contains(@class,'op_tbl_type1')]/tbody/tr");
                if (ncTr == null) continue;
                db.Open();
                foreach (HtmlNode tr in ncTr)
                {
                    HtmlNodeCollection ncTd = tr.SelectNodes("./td");
                    match = Regex.Match(ncTd[0].InnerHtml, @"fn_selectDetail\('(\d+)','(\d+)','(\d+)','(\d+)','(\d+)','(\d+)','(\d+|.)','(\d+|.)'\)", rxOptM);
                    cltrHstrNo = match.Groups[1].Value;
                    cltrNo = match.Groups[2].Value;
                    plnmNo = match.Groups[3].Value;
                    pbctNo = match.Groups[4].Value;
                    pbctCdtnNo = match.Groups[6].Value;
                    pbctSeq = match.Groups[7].Value;
                    pbctDgr = match.Groups[8].Value;

                    sucbAmt = Regex.Replace(ncTd[2].InnerText, @"[\-\,]", "").Trim();
                    sucbRate = Regex.Replace(ncTd[3].InnerText, @"[\-\,%]", "").Trim();
                    pbctCltrStatNm = ncTd[4].InnerText.Trim();
                    pbctExctDtm = ncTd[5].InnerText.Trim() + ":00";

                    pass = false;
                    sql = "select sucb_amt, hstr_no from tb_list where cltr_no=@cltr_no and plnm_no=@plnm_no and pbct_no=@pbct_no limit 1";
                    sp.Add(new MySqlParameter("@cltr_no", cltrNo));
                    sp.Add(new MySqlParameter("@plnm_no", plnmNo));
                    sp.Add(new MySqlParameter("@pbct_no", pbctNo));
                    dr = db.ExeRdr(sql, sp);
                    sp.Clear();
                    dr.Read();
                    if (dr.HasRows)
                    {
                        dbSucbAmt = dr["sucb_amt"].ToString();
                        dbHstrNo = dr["hstr_no"].ToString();
                    }
                    else
                    {
                        //추후 없는 사건은 따로 기록 할 필요가 있다.
                        pass = true;
                    }
                    if (string.IsNullOrEmpty(cltrHstrNo)) pass = true;
                    else
                    {
                        if (dbSucbAmt != "0" || Convert.ToDecimal(dbHstrNo) > Convert.ToDecimal(cltrHstrNo))
                        {
                            pass = true;
                        }
                    }                    
                    dr.Close();
                    if (pass == true) continue;

                    if (pbctCltrStatNm.Contains("낙찰"))
                    {
                        sql = "update tb_list set sucb_amt=@sucb_amt, sucb_rate=@sucb_rate, stat_nm=@stat_nm, exct_dtm=@exct_dtm where cltr_no=@cltr_no";
                        sp.Add(new MySqlParameter("@cltr_no", cltrNo));
                        sp.Add(new MySqlParameter("@sucb_amt", sucbAmt));
                        sp.Add(new MySqlParameter("@sucb_rate", sucbRate));
                        sp.Add(new MySqlParameter("@stat_nm", pbctCltrStatNm));
                        sp.Add(new MySqlParameter("@exct_dtm", pbctExctDtm));
                        db.ExeQry(sql, sp);
                        sp.Clear();

                        sql = "update tb_pbct set suc_flag=1 where cltr_no=@cltr_no and hstr_no=@hstr_no";
                        sp.Add(new MySqlParameter("@cltr_no", cltrNo));
                        sp.Add(new MySqlParameter("@hstr_no", cltrHstrNo));
                        db.ExeQry(sql, sp);
                        sp.Clear();

                        sbCnt++;
                    }
                    else if (pbctCltrStatNm == "유찰" || pbctCltrStatNm == "현장입찰마감" || pbctCltrStatNm == "인터넷입찰마감" || pbctCltrStatNm == "입회검사완료")
                    {
                        sql = "select * from tb_pbct where cltr_no='" + cltrNo + "' and hstr_no > '" + cltrHstrNo + "' limit 1";
                        dr = db.ExeRdr(sql);
                        dr.Read();
                        if (dr.HasRows)
                        {
                            string nxtPbctNo = dr["pbct_no"].ToString();
                            string nxtPbctCdtnNo = dr["pbct_cdtn_no"].ToString();
                            string nxtCltrHstrNo = dr["hstr_no"].ToString();
                            string nxtBidMnmtNo = dr["bid_mnmt_no"].ToString();
                            string nxtMinbAmt = dr["minb_amt"].ToString();
                            string nxtPbctBgnDtm = dr["bgn_dtm"].ToString();
                            string nxtPbctClsDtm = dr["cls_dtm"].ToString();
                            string nxtPbctExctDtm = dr["exct_dtm"].ToString();
                            dr.Close();

                            sql = "update tb_list set stat_nm=@stat_nm, pbct_no=@pbct_no, cdtn_no=@cdtn_no, hstr_no=@hstr_no, bmgmt_no=@bmgmt_no, minb_amt=@minb_amt, bgn_dtm=@bgn_dtm, cls_dtm=@cls_dtm, exct_dtm=@exct_dtm, sucb_amt=@sucb_amt, sucb_rate=@sucb_rate, mod_dtm=now() where cltr_no='" + cltrNo + "'";
                            sp.Add(new MySqlParameter("@stat_nm", pbctCltrStatNm));
                            sp.Add(new MySqlParameter("@pbct_no", nxtPbctNo));
                            sp.Add(new MySqlParameter("@cdtn_no", nxtPbctCdtnNo));
                            sp.Add(new MySqlParameter("@hstr_no", nxtCltrHstrNo));
                            sp.Add(new MySqlParameter("@bmgmt_no", nxtBidMnmtNo));
                            sp.Add(new MySqlParameter("@minb_amt", nxtMinbAmt));
                            sp.Add(new MySqlParameter("@sucb_amt", string.Empty));
                            sp.Add(new MySqlParameter("@sucb_rate", string.Empty));
                            sp.Add(new MySqlParameter("@bgn_dtm", DateTime.Parse(nxtPbctBgnDtm)));
                            sp.Add(new MySqlParameter("@cls_dtm", DateTime.Parse(nxtPbctClsDtm)));
                            sp.Add(new MySqlParameter("@exct_dtm", DateTime.Parse(nxtPbctExctDtm)));
                            db.ExeQry(sql, sp);
                            sp.Clear();
                        }
                        else
                        {
                            dr.Close();
                            sql = "update tb_list set stat_nm=@stat_nm, sucb_amt=@sucb_amt, sucb_rate=@sucb_rate, exct_dtm=@exct_dtm, mod_dtm=now() where cltr_no='" + cltrNo + "'";
                            sp.Add(new MySqlParameter("@stat_nm", pbctCltrStatNm));
                            sp.Add(new MySqlParameter("@sucb_amt", string.Empty));
                            sp.Add(new MySqlParameter("@sucb_rate", string.Empty));
                            sp.Add(new MySqlParameter("@exct_dtm", string.Empty));
                            db.ExeQry(sql, sp);
                            sp.Clear();
                        }

                        fbCnt++;
                    }
                    else
                    {
                        sql = "update tb_list set stat_nm=@stat_nm, sucb_amt=@sucb_amt, sucb_rate=@sucb_rate, exct_dtm=@exct_dtm, mod_dtm=now() where cltr_no='" + cltrNo + "'";
                        sp.Add(new MySqlParameter("@stat_nm", pbctCltrStatNm));
                        sp.Add(new MySqlParameter("@sucb_amt", string.Empty));
                        sp.Add(new MySqlParameter("@sucb_rate", string.Empty));
                        sp.Add(new MySqlParameter("@exct_dtm", string.Empty));
                        db.ExeQry(sql, sp);
                        sp.Clear();

                        obCnt++;
                    }
                }
                db.Close();
            }

            atomLog.AddLog(string.Format("입찰결과 낙찰-{0}, 유찰-{1}, 기타-{2}", sbCnt, fbCnt, obCnt));
        }

        /// <summary>
        /// 물건상태 확인
        /// </summary>
        private void Proc_Stat()
        {
            string url, html, sql, dbStatNm;
            string cltrNo, plnmNo, pbctNo, pbctCdtnNo, pbctCltrStatNm;
            decimal totCnt = 0, curCnt = 0, sCnt = 0;

            List<MySqlParameter> sp = new List<MySqlParameter>();
            sql = "select cltr_no, plnm_no, pbct_no, cdtn_no, stat_nm from tb_list where cls_dtm > curdate()";
            DataTable dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;

            txtState.AppendText(string.Format("\r\n\r\n■■■■■     [물건상태 확인]     ■■■■■"));    //화면에 진행상태 표시

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                txtState.AppendText(string.Format("\r\n[물건상태] {0} / {1}", curCnt, totCnt));    //화면에 진행상태 표시

                dbStatNm = row["stat_nm"].ToString();
                if (dbStatNm.Contains("낙찰") || dbStatNm.Contains("취소")) continue;

                cltrNo = row["cltr_no"].ToString();
                plnmNo = row["plnm_no"].ToString();
                pbctNo = row["pbct_no"].ToString();
                pbctCdtnNo = row["cdtn_no"].ToString();
                url = "http://www.onbid.co.kr/op/cta/cltrdtl/collateralRealEstateDetail.do?cltrNo=" + cltrNo + "&plnmNo=" + plnmNo + "&pbctNo=" + pbctNo + "&scrnGrpCd=0001&pbctCdtnNo=" + pbctCdtnNo;
                html = net.GetHtml(url, Encoding.UTF8);
                pbctCltrStatNm = Regex.Match(html, @"<span>물건상태\s\:\s<em>(.*?)</em></span>", rxOptM).Groups[1].Value;
                if (pbctCltrStatNm == null || pbctCltrStatNm?.Trim() == string.Empty) continue;
                if (dbStatNm == pbctCltrStatNm) continue;

                db.Open();
                sql = "update tb_list set stat_nm=@stat_nm, mod_dtm=now() where cltr_no='" + cltrNo + "'";
                sp.Add(new MySqlParameter("@stat_nm", pbctCltrStatNm));
                db.ExeQry(sql, sp);
                sp.Clear();
                db.Close();

                sCnt++;
            }

            atomLog.AddLog(string.Format("물건상태 대상-{0}, 변경-{1}", dt.Rows.Count, sCnt));
        }
        
        /// <summary>
        /// 특수조건 매칭
        /// </summary>
        private void Proc_SpCdtn()
        {
            string sql, inputStr, cd, ptrn, rslt, cltrNo;
            int totCnt = 0, curCnt = 0, sucCnt = 0;

            List<string> lsRslt = new List<string>();
            
            sql = "select cd, rx from tb_cd_etc where dvsn=18 order by cd";
            DataTable dtSp = db.ExeDt(sql);

            sql = "select D.cltr_no, utlz_pscd, etc_dtl, sez_note1, sez_note2, sez_note3 from tb_list L , tb_dtl D where L.cltr_no=D.cltr_no and 1st_dt=curdate() and sp_cdtn=''";
            DataTable dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;

            txtState.AppendText(string.Format("\r\n\r\n■■■■■     [특수조건 매칭]     ■■■■■"));    //화면에 진행상태 표시

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                lsRslt.Clear();
                rslt = string.Empty;
                txtState.AppendText(string.Format("\r\n> {0} / {1}", curCnt, totCnt));    //화면에 진행상태 표시

                cltrNo = row["cltr_no"].ToString();
                inputStr = string.Format("{0}\r\n{1}\r\n{2}\r\n{3}\r\n{4}", row["utlz_pscd"], row["etc_dtl"], row["sez_note1"], row["sez_note2"], row["sez_note3"]);
                foreach (DataRow r in dtSp.Rows)
                {
                    cd = r["cd"].ToString();
                    ptrn = r["rx"].ToString().Trim();
                    if (ptrn == string.Empty) continue;

                    Match match = Regex.Match(inputStr, ptrn);
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

                if (rslt == string.Empty) continue;

                sql = "update tb_list set sp_cdtn='" + rslt + "' where cltr_no=" + cltrNo;
                db.Open();
                db.ExeQry(sql);
                db.Close();
                sucCnt++;
            }
            atomLog.AddLog(string.Format("특수조건 매칭-{0}", sucCnt));
        }

        /// <summary>
        /// 역세권 매칭
        /// </summary>
        private void Proc_Station()
        {
            int mvCnt = 0;
            string sql, tid, cd;
            double lat_p = 0, lng_p = 0, lat_s = 0, lng_s = 0, distance = 0;

            txtState.Text = "# 역세권 매칭 #";

            CoordCal cc = new CoordCal();

            sql = "select * from tx_railroad order by local_cd,line_cd,station_cd";
            DataTable dtR = db.ExeDt(sql);

            sql = "select cltr_no as tid, x, y from tb_list where 1st_dt=curdate() and x > 0 and station_prc=0 order by cltr_no";
            //sql = "select cltr_no as tid, x, y from tb_list where 1st_dt='2021-12-15' and x > 0 and station_prc=0 order by cltr_no";
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
                        sql = "insert ignore into tb_railroad set tid='" + tid + "', cd='" + cd + "', distance='" + distance.ToString() + "', wdt=curdate()";                        
                        db.ExeQry(sql);
                        sql = "update tb_list set station_prc=1 where cltr_no='" + tid + "'";
                        db.ExeQry(sql);
                        db.Close();
                        mvCnt++;
                    }
                }
            }
            atomLog.AddLog(string.Format("역세권 매칭-{0}", mvCnt));
        }

        /// <summary>
        /// 아파트 코드 매칭
        /// </summary>
        private void Proc_AptCd()
        {
            int i = 0, totCnt = 0, sucCnt = 0;
            string sql, cltrNo, pnu, aptNm, bunji, aptCd = string.Empty;

            sql = "select * from tx_apt where match_type in (1,3)";
            DataTable dtA = db.ExeDt(sql);

            sql = "select * from tb_list where cat2 in (10001001,10001002,10001004) and cat3 != 100010010004 and apt_cd=0 and pnu != '' and 1st_dt=curdate() order by cltr_no";
            DataTable dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;

            txtState.AppendText(string.Format("\r\n\r\n■■■■■     [아파트 코드 매칭]     ■■■■■"));    //화면에 진행상태 표시

            Regex rx = new Regex(@"\d{2}\d{3}\d{3}\d{2}(\d{1})(\d{4})(\d{4})");
            foreach (DataRow row in dt.Rows)
            {
                i++;
                txtState.AppendText(string.Format("\r\n> {0} / {1}", i, totCnt));    //화면에 진행상태 표시

                aptCd = string.Empty;

                cltrNo = row["cltr_no"].ToString();
                pnu = row["pnu"].ToString();
                Match match = rx.Match(row["pnu"].ToString());
                bunji = (Convert.ToDecimal(match.Groups[2].Value) * 1).ToString();
                if (match.Groups[1].Value == "2") bunji = "산" + bunji;
                if ((Convert.ToDecimal(match.Groups[3].Value) * 1) > 0) bunji = bunji + "-" + (Convert.ToDecimal(match.Groups[3].Value) * 1).ToString();
                DataRow[] aptRows = dtA.Select(string.Format("si_key='{0}' and gu_key='{1}' and dong_key='{2}' and ri_key='{3}' and bunji='{4}'", row["si_cd"], row["gu_cd"], row["dn_cd"], row["ri_cd"], bunji));
                if (aptRows.Count() == 0) continue;
                foreach (DataRow aptRow in aptRows)
                {
                    aptNm = aptRow["dj_name"].ToString();
                    if (row["land_adrs"].ToString().Contains(aptNm) || row["road_adrs"].ToString().Contains(aptNm))
                    {
                        aptCd = aptRow["apt_code"].ToString();
                    }
                }
                if (aptCd == string.Empty) continue;
                sql = "update tb_list set apt_cd='" + aptCd + "' where cltr_no=" + cltrNo;
                db.Open();
                db.ExeQry(sql);
                db.Close();
                txtState.AppendText("-> success");
                sucCnt++;
            }
            atomLog.AddLog(string.Format("아파트코드 매칭-{0}", sucCnt));
        }

        /// <summary>
        /// 파일 수집
        /// </summary>
        private void Proc_File()
        {
            string html, url, sql, cltrNo, cltrHstrNo, fileRef, atchFileNo, seq, cvp;
            string fullNm, fileNm, dirNo, jsFile = "", thumb;
            int totCnt = 0, curCnt = 0, prcCnt = 0, failCnt = 0;
            string locFile, rmtFile;
            bool upRslt1, upThRslt1, dbFlag;
            Dictionary<string, string> dicFileRslt;

            HAPDoc doc = new HAPDoc();
            List<MySqlParameter> sp = new List<MySqlParameter>();
            List<string> fileList = new List<string>();
            var jaFile = new JArray();

            sql = "select L.cltr_no, hstr_no, file_no, cltr_nm from tb_list L , tb_dtl D where L.cltr_no=D.cltr_no and 1st_dt=curdate() and file_prc=0 order by L.cltr_no";
            //sql = "select L.cltr_no, hstr_no, file_no, cltr_nm from tb_list L , tb_dtl D where L.cltr_no=D.cltr_no and 1st_dt='2021-12-15' and file_prc=0 order by L.cltr_no";
            DataTable dtL = db.ExeDt(sql);
            totCnt = dtL.Rows.Count;

            if (totCnt > 0)
            {
                atomLog.AddLog(string.Format("파일 다운로드 시작 대상물건-{0}", dtL.Rows.Count));
            }

            FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "PA/", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);

            string today = DateTime.Today.ToShortDateString();
            txtState.AppendText(string.Format("\r\n\r\n■■■■■     [파일수집]     ■■■■■"));    //화면에 진행상태 표시
            foreach (DataRow row in dtL.Rows)
            {
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                jaFile.Clear();

                cltrNo = row["cltr_no"].ToString();
                cltrHstrNo = row["hstr_no"].ToString();
                fileRef = row["file_no"].ToString();
                if (fileRef == string.Empty) continue;
                atchFileNo = Regex.Match(fileRef, @"\d+").Value;
                dirNo = (Math.Ceiling(Convert.ToDecimal(cltrNo) / 100000) * 100000).ToString().PadLeft(7, '0');

                txtState.AppendText(string.Format("\r\n[File] {0} -> {1} / {2}", cltrNo, curCnt, totCnt));    //화면에 진행상태 표시

                //사진
                if (fileRef.Contains("A"))
                {
                    webCnt++;
                    if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                    url = "http://www.onbid.co.kr/op/cta/cltrdtl/collateralRealEstateDetailPicPopup.do?cltrHstrNo=" + cltrHstrNo + "&cltrNo=" + cltrNo + "&docKindCd=2005&atchFilePtcsNo=" + atchFileNo;
                    html = net.GetHtml(url, Encoding.UTF8);
                    MatchCollection mc = Regex.Matches(html, @"fn_selectImg\('(\d+)','(\d+)','(\d+)', '(\d+)'\)", rxOptM);
                    foreach (Match m in mc)
                    {
                        seq = m.Groups[2].Value;
                        url = "http://download.onbid.co.kr/filecon/imageView.do?atchFilePtcsNo=" + m.Groups[1].Value + "&atchSeq=" + seq + "&thnlNm=null&thnImgDownloadFlag=false&DownloadImageKind=PHYS_FILE_NM&rgsrNo=1&usrDvsnCd=null";                        
                        fileNm = string.Format("A{0}_{1}.jpg", cltrNo, seq);
                        fullNm = string.Format(@"{0}\{1}", filePath, fileNm);
                        dicFileRslt = net.DnFile(url, fullNm);
                        if (dicFileRslt["result"] == "success")
                        {
                            fileNm = dicFileRslt["fileNm"];
                            thumb = SubProc_Thumb(fileNm);
                            var obj = new JObject();
                            obj.Add("fullNm", string.Format("A/{0}/{1}", dirNo, fileNm));
                            obj.Add("ctgr", "A");
                            obj.Add("thumb", thumb);
                            obj.Add("rgstDt", today);
                            jaFile.Add(obj);
                        }
                    }
                }

                //지적도
                if (fileRef.Contains("B"))
                {
                    webCnt++;
                    if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                    url = "http://www.onbid.co.kr/op/cta/cltrdtl/collateralRealEstateDetailPicPopup.do?cltrHstrNo=" + cltrHstrNo + "&cltrNo=" + cltrNo + "&docKindCd=2003&atchFilePtcsNo=" + atchFileNo;
                    html = net.GetHtml(url, Encoding.UTF8);
                    MatchCollection mc = Regex.Matches(html, @"fn_downloadAttachFile\('(\d+)','(\d+)'\)", rxOptM);
                    foreach (Match m in mc)
                    {
                        seq = m.Groups[2].Value;
                        url = "http://download.onbid.co.kr/filecon/imageView.do?atchFilePtcsNo=" + m.Groups[1].Value + "&atchSeq=" + seq + "&thnlNm=null&thnImgDownloadFlag=false&DownloadImageKind=null&rgsrNo=1&usrDvsnCd=null";
                        fileNm = string.Format("B{0}_{1}.jpg", cltrNo, seq);
                        fullNm = string.Format(@"{0}\{1}", filePath, fileNm);
                        dicFileRslt = net.DnFile(url, fullNm);
                        if (dicFileRslt["result"] == "success")
                        {
                            fileNm = dicFileRslt["fileNm"];
                            thumb = SubProc_Thumb(fileNm);
                            var obj = new JObject();
                            obj.Add("fullNm", string.Format("B/{0}/{1}", dirNo, fileNm));
                            obj.Add("ctgr", "B");
                            obj.Add("thumb", thumb);
                            obj.Add("rgstDt", today);
                            jaFile.Add(obj);
                        }
                    }
                }

                //위치도
                if (fileRef.Contains("C"))
                {
                    webCnt++;
                    if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                    url = "http://www.onbid.co.kr/op/cta/cltrdtl/collateralRealEstateDetailPicPopup.do?cltrHstrNo=" + cltrHstrNo + "&cltrNo=" + cltrNo + "&docKindCd=2002&atchFilePtcsNo=" + atchFileNo;
                    html = net.GetHtml(url, Encoding.UTF8);
                    MatchCollection mc = Regex.Matches(html, @"fn_downloadAttachFile\('(\d+)','(\d+)'\)", rxOptM);
                    foreach (Match m in mc)
                    {
                        seq = m.Groups[2].Value;
                        url = "http://download.onbid.co.kr/filecon/imageView.do?atchFilePtcsNo=" + m.Groups[1].Value + "&atchSeq=" + seq + "&thnlNm=null&thnImgDownloadFlag=false&DownloadImageKind=PHYS_FILE_NM&rgsrNo=1&usrDvsnCd=null";
                        fileNm = string.Format("C{0}_{1}.jpg", cltrNo, seq);
                        fullNm = string.Format(@"{0}\{1}", filePath, fileNm);
                        dicFileRslt = net.DnFile(url, fullNm);
                        if (dicFileRslt["result"] == "success")
                        {
                            fileNm = dicFileRslt["fileNm"];
                            thumb = SubProc_Thumb(fileNm);
                            var obj = new JObject();
                            obj.Add("fullNm", string.Format("C/{0}/{1}", dirNo, fileNm));
                            obj.Add("ctgr", "C");
                            obj.Add("thumb", thumb);
                            obj.Add("rgstDt", today);
                            jaFile.Add(obj);
                        }
                    }
                }
                
                //감정평가서
                sql = "select atch_file_no, atch_seq from tb_apsl where cltr_no='" + cltrNo + "'";
                DataTable dtA = db.ExeDt(sql);
                foreach (DataRow rowA in dtA.Rows)
                {
                    if (row["cltr_nm"].ToString().Contains("분재")) continue; //임시

                    if (rowA["atch_file_no"].ToString() == "" || rowA["atch_seq"].ToString() == "") continue;
                    seq = rowA["atch_seq"].ToString();
                    url = "http://www.onbid.co.kr/op/common/downloadFile.do?atchFilePtcsNo=" + rowA["atch_file_no"].ToString() + "&atchSeq=" + seq;
                    fileNm = string.Format("D{0}_{1}.pdf", cltrNo, seq);
                    fullNm = string.Format(@"{0}\{1}", filePath, fileNm);
                    dicFileRslt = net.DnFile(url, fullNm);
                    if (dicFileRslt["result"] == "success")
                    {
                        fileNm = dicFileRslt["fileNm"];
                        var obj = new JObject();
                        obj.Add("fullNm", string.Format("D/{0}/{1}", dirNo, fileNm));
                        obj.Add("ctgr", "D");
                        obj.Add("thumb", "N");
                        obj.Add("rgstDt", today);
                        jaFile.Add(obj);
                    }
                }

                if (jaFile.Count == 0) continue;

                //FTP 업로드
                dbFlag = true;
                jsFile = jaFile.ToString();
                foreach (JObject item in jaFile)
                {
                    rmtFile = item["fullNm"].ToString();
                    locFile = filePath + "/" + Regex.Match(rmtFile, @".*/(.*)$", RegexOptions.IgnoreCase).Groups[1].Value;
                    upRslt1 = ftp1.Upload(locFile, rmtFile);
                    if (upRslt1 == false)
                    {
                        dbFlag = false;
                    }

                    if (item["thumb"].ToString() == "Y")
                    {
                        rmtFile = "_thumb/" + rmtFile;
                        locFile = filePath + "/_thumb/" + Regex.Match(rmtFile, @".*/(.*)$", RegexOptions.IgnoreCase).Groups[1].Value;
                        upThRslt1 = ftp1.Upload(locFile, rmtFile);
                        if (upThRslt1 == false)
                        {
                            dbFlag = false;
                        }
                    }

                    if (dbFlag==false)
                    {
                        failCnt++;
                        break;
                    }
                }

                //DB처리
                if (dbFlag == true)
                {
                    cvp = "js_abcd=@js_abcd";
                    sql = "insert into tb_file set cltr_no=@cltr_no, " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                    sp.Add(new MySqlParameter("@cltr_no", cltrNo));
                    sp.Add(new MySqlParameter("@js_abcd", jsFile));
                    db.Open();
                    db.ExeQry(sql, sp);
                    sp.Clear();

                    sql = "update tb_list set file_prc=1 where cltr_no='" + cltrNo + "'";
                    db.ExeQry(sql);
                    db.Close();

                    prcCnt++;
                }                
            } //dtLs.Rows 끝

            if (totCnt > 0)
            {
                atomLog.AddLog(string.Format("파일 다운/업로드 완료 처리물건-{0}, 업로드실패-{1}", prcCnt, failCnt));
            }
        }

        /// <summary>
        /// 썸네일 생성
        /// </summary>
        /// <param name="fileNm"></param>
        /// <returns></returns>
        private string SubProc_Thumb(string fileNm)
        {
            string result;
            string fullNm = string.Format(@"{0}\{1}", filePath, fileNm);
            if (!File.Exists(fullNm) || !Regex.IsMatch(fileNm, @"bmp|gif|jpg|png|tiff"))
            {
                result = "N";
            }
            else
            {
                try
                {
                    Image image = Image.FromFile(fullNm);
                    Image thumb = image.GetThumbnailImage(200, 150, () => false, IntPtr.Zero);
                    thumb.Save(string.Format(@"{0}\_thumb\{1}", filePath, fileNm));
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
        /// 압류재산-권리분석 기초정보(입찰시작 7일전부터 제공)
        /// </summary>
        private void Proc_ShrLsd()
        {
            string url, urlTab, jsonData, sql, cdtn;
            string cltrHstrNo, cltrNo, plnmNo, pbctNo, pbctCdtnNo, bidMnmtNo;
            decimal i = 0, pgCnt = 0, totCnt = 0, curCnt = 0;
            decimal shrCnt = 0, lsdCnt = 0;
                        
            List<MySqlParameter> sp = new List<MySqlParameter>();

            cdtn = "prpt_dvsn='압류재산(캠코)' and cls_dtm >= CURDATE() and curdate() >= DATE_SUB(bgn_dtm,INTERVAL (7*24-10) HOUR) and sez_udt < DATE_SUB(CURDATE(),INTERVAL 7 DAY)";
            sql = "select cltr_no, plnm_no, pbct_no, cdtn_no, hstr_no, bmgmt_no from tb_list where " + cdtn;
            DataTable dtL = db.ExeDt(sql);
            totCnt = dtL.Rows.Count;

            txtState.AppendText(string.Format("\r\n\r\n■■■■■     [압류재산]     ■■■■■"));    //화면에 진행상태 표시

            foreach (DataRow row in dtL.Rows)
            {
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                cltrNo = row["cltr_no"].ToString();
                cltrHstrNo = row["hstr_no"].ToString();
                plnmNo = row["plnm_no"].ToString();
                pbctNo = row["pbct_no"].ToString();
                pbctCdtnNo = row["cdtn_no"].ToString();
                bidMnmtNo = row["bmgmt_no"].ToString();

                txtState.AppendText(string.Format("\r\n[압류재산] {0} -> {1} / {2}", cltrNo, curCnt, totCnt));    //화면에 진행상태 표시

                urlTab = "http://www.onbid.co.kr/op/cta/cltrdtl/collateralRealEstateTabDetail.do?cltrHstrNo=" + cltrHstrNo + "&cltrNo=" + cltrNo + "&plnmNo=" + plnmNo + "&pbctNo=" + pbctNo + "&pbctCdtnNo=" + pbctCdtnNo + "&bidMnmtNo=" + bidMnmtNo + "&selectValue=&";
                url = urlTab + "dtButtonTab=011";
                jsonData = net.GetHtml(url, Encoding.UTF8);
                dynamic x = JsonConvert.DeserializeObject(jsonData);

                //배분요구 및 채권신고 현황
                dynamic jsShr = "[]";
                if (x["paginationInfo3"] != null)
                {
                    pgCnt = (int)x["paginationInfo3"]["totalPageCount"];
                    jsShr = x["resultShrImfoList"];
                    if (jsShr != null)
                    {
                        jsShr = Regex.Replace(jsShr.ToString(), @"[\[\]]", string.Empty, rxOptS).Trim();
                        if (pgCnt > 1)
                        {
                            for (i = 2; i <= pgCnt; i++)
                            {
                                jsonData = net.GetHtml(urlTab + "&pageJsFunction=fn_changTab&pageType=paging&gubun=paging3&pageIndex3=" + i.ToString() + "&dtButtonTab=011", Encoding.UTF8);
                                x = JsonConvert.DeserializeObject(jsonData);
                                //jsShr = x["resultShrImfoList"];
                                if (x["resultShrImfoList"] != null)
                                {
                                    jsShr += "," + Regex.Replace(x["resultShrImfoList"].ToString(), @"[\[\]]", string.Empty, rxOptS).Trim();
                                }
                            }
                        }
                        jsShr = "[" + jsShr + "]";
                    }
                }

                if (!string.IsNullOrEmpty(jsShr))
                {
                    db.Open();
                    sql = "delete from tb_shr where cltr_no='" + cltrNo + "'";
                    db.ExeQry(sql);

                    JArray jaShr = JArray.Parse(jsShr);
                    foreach (JObject item in jaShr)
                    {
                        sql = "insert into tb_shr set cltr_no=@cltr_no, row_no=@row_no, irst_dvsn_nm=@irst_dvsn_nm, irst_irps_nm=@irst_irps_nm, rgst_dt=@rgst_dt, stup_amt=@stup_amt, rqr_amt=@rqr_amt, rqr_dt=@rqr_dt";
                        sp.Add(new MySqlParameter("@cltr_no", cltrNo));
                        sp.Add(new MySqlParameter("@row_no", item["rn"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@irst_dvsn_nm", item["rgtRltnCdNm"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@irst_irps_nm", item["nm"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@rgst_dt", item["stupDt"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@stup_amt", item["stupAmt"]?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@rqr_amt", item["shrRqrBondAmt"]?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@rqr_dt", item["shrRqrDt"]?.ToString() ?? ""));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                    db.Close();
                    shrCnt++;
                }

                //점유관계
                dynamic jsLsd = "[]";
                if (x["paginationInfo4"] != null)
                {
                    pgCnt = (int)x["paginationInfo4"]["totalPageCount"];
                    jsLsd = x["resultLsdImfoList"];
                    if (jsLsd != null)
                    {
                        jsLsd = Regex.Replace(jsLsd.ToString(), @"[\[\]]", string.Empty, rxOptS).Trim();
                        if (pgCnt > 1)
                        {
                            for (i = 2; i <= pgCnt; i++)
                            {
                                jsonData = net.GetHtml(urlTab + "&pageJsFunction=fn_changTab&pageType=paging&gubun=paging4&pageIndex4=" + i.ToString() + "&dtButtonTab=011", Encoding.UTF8);
                                x = JsonConvert.DeserializeObject(jsonData);
                                //jsLsd = x["resultLsdImfoList"];
                                if (x["resultLsdImfoList"] != null)
                                {
                                    jsLsd += "," + Regex.Replace(x["resultLsdImfoList"].ToString(), @"[\[\]]", string.Empty, rxOptS).Trim();
                                }
                            }
                        }
                        jsLsd = "[" + jsLsd + "]";
                    }
                }

                if (!string.IsNullOrEmpty(jsLsd))
                {
                    db.Open();
                    sql = "delete from tb_lsd where cltr_no='" + cltrNo + "'";
                    db.ExeQry(sql);

                    JArray jaLsd = JArray.Parse(jsLsd);
                    foreach (JObject item in jaLsd)
                    {
                        sql = "insert into tb_lsd set cltr_no=@cltr_no, row_no=@row_no, shr_rltn_nm=@shr_rltn_nm, nm=@nm, ctrt_dt=@ctrt_dt, mvn_dt=@mvn_dt, fix_dt=@fix_dt, tdps=@tdps, rent=@rent, lsd_part=@lsd_part";
                        sp.Add(new MySqlParameter("@cltr_no", cltrNo));
                        sp.Add(new MySqlParameter("@row_no", item["rn"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@shr_rltn_nm", item["shrRltnCdNm"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@nm", item["nm"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@ctrt_dt", item["ctrtDt"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@mvn_dt", item["mvnDt"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@fix_dt", item["fixDt"]?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@tdps", item["tdps"]?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@rent", item["rent"]?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@lsd_part", item["lsdPart"]?.ToString() ?? ""));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                    db.Close();
                }

                if (!string.IsNullOrEmpty(jsShr) || !string.IsNullOrEmpty(jsLsd))
                {
                    db.Open();
                    sql = "update tb_list set sez_udt=curdate() where cltr_no='" + cltrNo + "'";
                    db.ExeQry(sql);
                    db.Close();
                    lsdCnt++;
                }
            }

            atomLog.AddLog(string.Format("압류재산 갱신 배분요구-{0}, 점유관계-{1}", shrCnt, lsdCnt));
        }

        /// <summary>
        /// 첨부파일-공고
        /// </summary>
        private void Proc_AttachPl()
        {
            string sql, url, html, filePath;
            string plnmNo, pbctNo, fileNm, saveNm, ext, locFile, rmtFile, ptcsNo, seq;
            int totCnt = 0, curCnt = 0, sucCnt = 0;

            atomLog.AddLog("▼ 첨부파일-공고");
            Dictionary<string, string> dicFileRslt;

            filePath = @"C:\Atom\PA\" + DateTime.Today.ToShortDateString() + @"\첨부파일";
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }

            FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "PA/_atpl", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);

            HAPDoc doc = new HAPDoc();
            List<MySqlParameter> sp = new List<MySqlParameter>();

            sql = "SELECT * FROM tb_noti where wdt=curdate() order by plnm_no";
            //sql = "SELECT * FROM tb_noti where wdt='2021-12-15' order by plnm_no";
            DataTable dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                plnmNo = row["plnm_no"].ToString();
                pbctNo = row["pbct_no"].ToString();

                txtState.AppendText(string.Format("\r\n[첨부파일-공고] {0} -> {1} / {2}", plnmNo, curCnt, totCnt));    //화면에 진행상태 표시

                url = "http://www.onbid.co.kr/op/ppa/plnmmn/publicAnnounceRlstDetail.do?pbctNo=" + pbctNo + "&plnmNo=" + plnmNo;
                html = net.GetHtml(url, Encoding.UTF8);
                doc.LoadHtml(html);

                HtmlNodeCollection nc = doc.DocumentNode.SelectNodes("//a[contains(@href,'fn_downloadAttachFile')]");
                if (nc == null) continue;

                foreach (HtmlNode nd in nc)
                {
                    Match match = Regex.Match(nd.Attributes["href"].Value, @"'(\d+)','(\d+)'", rxOptM);
                    ptcsNo = match.Groups[1].Value;
                    seq = match.Groups[2].Value;
                    fileNm = nd.InnerText.Trim();
                    //MessageBox.Show($"{plnmAtchFilePtcsNo}\n{seq}\n{fileNm}");
                    ext = Regex.Match(fileNm, @"\.(\w{3,4})$", rxOptM).Groups[1].Value;
                    saveNm = $"P{plnmNo}-{ptcsNo}_{seq}.{ext}";
                    locFile = $@"{filePath}\{saveNm}";
                    if (File.Exists(locFile)) continue;

                    rmtFile = saveNm;
                    url = $"https://www.onbid.co.kr/op/common/downloadFile.do?atchFilePtcsNo={ptcsNo}&atchSeq={seq}";
                    dicFileRslt = net.DnFile(url, locFile);

                    if (dicFileRslt["result"] == "success")
                    {
                        if (ftp1.Upload(locFile, rmtFile))
                        {
                            sql = "insert into tb_attach set plnm_no='" + plnmNo + "', ptcs_no='" + ptcsNo + "', seq='" + seq + "', file_nm=@file_nm, save_nm='" + saveNm + "', wdt=curdate() ON DUPLICATE KEY UPDATE file_nm=@file_nm, wdt=curdate()";
                            sp.Add(new MySqlParameter("@file_nm", fileNm));
                            db.Open();
                            db.ExeQry(sql, sp);
                            sp.Clear();
                            db.Close();
                            sucCnt++;
                        }
                    }
                }
            }

            atomLog.AddLog($"> {sucCnt}건");
        }

        /// <summary>
        /// 첨부파일-물건
        /// </summary>
        private void Proc_AttachCl()
        {
            string sql, url, html, jsonData, filePath;
            string cltrNo, cltrHstrNo, atchFilePtcsNo = "", plnmAtchFilePtcsNo = "", ptcsNo, seq, fileNm, saveNm, ext, locFile, rmtFile;
            int totCnt = 0, curCnt = 0, sucCnt = 0;

            atomLog.AddLog("▼ 첨부파일-물건");
            Dictionary<string, string> dicFileRslt;

            filePath = @"C:\Atom\PA\" + DateTime.Today.ToShortDateString() + @"\첨부파일";
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }

            FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "PA/_atcl", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);

            sql = "select * from tb_list where org_dvsn=0 and 1st_dt=curdate() order by cltr_no";
            //sql = "select * from tb_list where org_dvsn=0 and 1st_dt='2021-12-15' order by cltr_no";
            DataTable dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;

            HAPDoc doc = new HAPDoc();
            List<MySqlParameter> sp = new List<MySqlParameter>();

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;

                cltrNo = row["cltr_no"].ToString();
                txtState.AppendText(string.Format("\r\n[첨부파일-물건] {0} -> {1} / {2}", cltrNo, curCnt, totCnt));    //화면에 진행상태 표시

                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                url = "http://www.onbid.co.kr/op/cta/cltrdtl/collateralRealEstateDetail.do?cltrHstrNo=" + row["hstr_no"].ToString() + "&cltrNo=" + cltrNo + "&plnmNo=" + row["plnm_no"].ToString() + "&pbctNo=" + row["pbct_no"].ToString() + "&scrnGrpCd=0001&pbctCdtnNo=" + row["cdtn_no"].ToString();
                html = net.GetHtml(url, Encoding.UTF8);
                if (html.Contains("요청하신 페이지를 찾을 수 없거나")) continue;
                doc.LoadHtml(html);
                HtmlNode nd = doc.DocumentNode.SelectSingleNode("//input[@id='cltrHstrNo']");
                cltrHstrNo = nd.Attributes["value"].Value.Trim();

                nd = doc.DocumentNode.SelectSingleNode("//input[@id='atchFilePtcsNo']");
                atchFilePtcsNo = nd.Attributes["value"].Value.Trim();

                nd = doc.DocumentNode.SelectSingleNode("//input[@id='plnmAtchFilePtcsNo']");
                plnmAtchFilePtcsNo = nd.Attributes["value"].Value.Trim();
                
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                //url = "http://www.onbid.co.kr/op/cta/cltrdtl/collateralRealEstateTabDetail.do?cltrHstrNo=" + cltrHstrNo + "&cltrNo=" + cltrNo + "&plnmNo=" + row["plnm_no"].ToString() + "&pbctNo=" + row["pbct_no"].ToString() + "&pbctCdtnNo=" + row["cdtn_no"].ToString() + "&bidMnmtNo=" + row["bmgmt_no"].ToString() + "&selectValue=&atchFilePtcsNo=" + atchFilePtcsNo + "&plnmAtchFilePtcsNo=" + plnmAtchFilePtcsNo + "&dtButtonTab=002";
                url = "http://www.onbid.co.kr/op/cta/cltrdtl/collateralRealEstateTabDetail.do?cltrHstrNo=" + cltrHstrNo + "&cltrNo=" + cltrNo + "&plnmNo=" + row["plnm_no"].ToString() + "&pbctNo=" + row["pbct_no"].ToString() + "&pbctCdtnNo=" + row["cdtn_no"].ToString() + "&bidMnmtNo=" + row["bmgmt_no"].ToString() + "&selectValue=&atchFilePtcsNo=" + atchFilePtcsNo + "&dtButtonTab=002";
                jsonData = net.GetHtml(url, Encoding.UTF8);
                if (jsonData.Contains("요청하신 페이지를 찾을 수 없거나")) continue;
                dynamic x = JsonConvert.DeserializeObject(jsonData);
                var jsFile = x["resultFileList"];
                if (jsFile != null) jsFile = Regex.Replace(jsFile.ToString(), @"\s{2,}", " ", rxOptS);
                if (string.IsNullOrEmpty(jsFile)) continue;

                JArray jaFile = JArray.Parse(jsFile);
                if (jaFile.Count == 0) continue;

                foreach (JObject item in jaFile)
                {
                    ptcsNo = item["atchFilePtcsNo"]?.ToString() ?? string.Empty;
                    seq = item["atchSeq"]?.ToString() ?? string.Empty;
                    fileNm = item["atchFileNm"]?.ToString() ?? string.Empty;
                    if (ptcsNo == string.Empty || seq == string.Empty) continue;

                    ext = Regex.Match(fileNm, @"\.(\w{3,4})$", rxOptM).Groups[1].Value;
                    saveNm = $"C{cltrNo}-{ptcsNo}_{seq}.{ext}";
                    locFile = $@"{filePath}\{saveNm}";
                    if (File.Exists(locFile)) continue;

                    rmtFile = saveNm;
                    url = $"https://www.onbid.co.kr/op/common/downloadFile.do?atchFilePtcsNo={ptcsNo}&atchSeq={seq}";
                    dicFileRslt = net.DnFile(url, locFile);

                    if (dicFileRslt["result"] == "success")
                    {
                        if (ftp1.Upload(locFile, rmtFile))
                        {
                            sql = "insert into tb_attach set cltr_no='" + cltrNo + "', ptcs_no='" + ptcsNo + "', seq='" + seq + "', file_nm=@file_nm, save_nm='" + saveNm + "', wdt=curdate() ON DUPLICATE KEY UPDATE file_nm=@file_nm, wdt=curdate()";
                            sp.Add(new MySqlParameter("@file_nm", fileNm));
                            db.Open();
                            db.ExeQry(sql, sp);
                            sp.Clear();
                            db.Close();
                            sucCnt++;
                        }
                    }
                }
            }

            atomLog.AddLog($"> {sucCnt}건");
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {   
            bgwork.Dispose();
            
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = @"C:\Atom\Atom.exe";
            psi.Arguments = "공매-재산명세";
            Process.Start(psi);
            
            this.Dispose();
            this.Close();
        }
    }
}
