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
using System.Collections;
using System.Xml;
using Solar;
using System.Threading;
using System.IO;
using System.Diagnostics;

namespace Atom.CA
{
    public partial class fAnmt : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AuctCd auctCd = new AuctCd();
        ApiUtil api = new ApiUtil();
        AtomLog atomLog = new AtomLog(105);     //로그 생성

        BackgroundWorker bgwork;
        int setCnt = 3, setSleep = 3000, webCnt = 0;

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        int cmpDvsnCd = 12;     //공고상태 비교(RptDvsn: 12)

        string filePath;    //로컬 파일저장 경로
        string vmNm = Environment.MachineName;

        FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "CA/", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);

        public fAnmt()
        {
            InitializeComponent();
            this.Shown += FAnmt_Shown;
        }

        private void FAnmt_Shown(object sender, EventArgs e)
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
            //파일저장 디렉토리 생성
            filePath = @"C:\Atom\CA\" + DateTime.Today.ToShortDateString();
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
                //Directory.CreateDirectory(filePath + @"\upload");
            }
        }

        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            string curMnth, nxtMnth, jiwonNm, date, url, html, locFile, rmtFile, sql, cvp, spt, dpt, year, fileNm, tbl, curDtHour;
            string caNm, bidDt, dptCd, dptNm, dir;
            decimal totCnt = 0, curCnt = 0, dlCnt = 0, ulCnt = 0, dnFailCnt = 0;
            string ctgr = "AI";
            string stripTag = @"[</]+(a|img).*?>";

            dir = filePath + @"\매각공고";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            DataTable dtLaw = auctCd.DtLawInfo();
            List<string> mnthList = new List<string>();
            curMnth = DateTime.Now.ToShortDateString().Substring(0, 7).Replace("-", string.Empty);
            nxtMnth = DateTime.Now.AddDays(14).ToShortDateString().Substring(0, 7).Replace("-", string.Empty);
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

            //string testArea = "인천";

            foreach (DataRow row in dtLaw.Rows)
            {
                jiwonNm = auctCd.LawNmEnc(row["lawNm"]);
                //if (Regex.IsMatch(row["lawNm"].ToString(), testArea) == false) continue;    //Test 범위 제한
                foreach (string ym in mnthList)
                {
                    webCnt++;
                    if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                    date = ym.Substring(0, 4) + "." + ym.Substring(4);
                    url = "http://www.courtauction.go.kr/RetrieveRealEstMgakNotifySrch.laf?date=" + date + "&inqYear=&inqMnth=&inqYearMnth=" + ym + "&srnID=PNO101001&ipchalGbnCd=000331&jiwonNm=" + jiwonNm;
                    html = net.GetHtml(url);

                    doc.LoadHtml(html);
                    HtmlNodeCollection ncDiv = doc.DocumentNode.SelectNodes("//div[contains(@class,'cal_schedule')]");
                    if (ncDiv == null) continue;

                    foreach (HtmlNode div in ncDiv)
                    {
                        string clickStr = div.SelectSingleNode("./a").GetAttributeValue("onclick", "null");
                        MatchCollection mc = Regex.Matches(clickStr, @"'(.*?)'", rxOptM);
                        caNm = mc[1].Groups[1].Value.Trim();
                        bidDt = mc[2].Groups[1].Value.Trim();
                        dptCd = mc[5].Groups[1].Value.Trim();
                        dptNm = mc[6].Groups[1].Value.Trim();
                        dtCal.Rows.Add(row["csCd"].ToString(), caNm, bidDt, dptCd, dptNm);

                        txtPrgs.AppendText(string.Format("\r\n> {0} - {1} - {2}", bidDt, caNm, dptNm));
                    }
                }
            }

            curDtHour = string.Format("{0:yyyyMMddHH}", DateTime.Now);
            foreach (DataRow row in dtCal.Rows)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                txtPrgs.AppendText(string.Format("\r\n> {0} / {1} / {2}", row["lawNm"], row["bidDt"], row["dptNm"]));    //화면에 진행상태 표시

                locFile = string.Format(@"{0}\{1}-{2}-{3}-{4}-{5}.html", dir, ctgr, row["csCd"], row["dptCd"], row["bidDt"], curDtHour);
                if (File.Exists(locFile)) continue;

                jiwonNm = auctCd.LawNmEnc(row["lawNm"]);
                url = "http://www.courtauction.go.kr/RetrieveRealEstMgakNotifySrchGyulgwa.laf?ipchalGbnCd=000331&jiwonNm=" + jiwonNm + "&maeGiil=" + row["bidDt"].ToString() + "&jpDeptCd=" + row["dptCd"].ToString();
                html = net.GetHtml(url);
                doc.LoadHtml(html);
                HtmlNodeCollection nc = doc.DocumentNode.SelectNodes("//div[@class='table_title']|//table[@class='Ltbl_list']");
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
                        txtPrgs.AppendText(" -> OK");

                        AnmtMdfyChk(row, A1);   //기일변경/취하/취소공고, 정정공고 확인
                    }
                    else
                    {
                        dnFailCnt++;
                        txtPrgs.AppendText(" -> FAIL-1");
                        continue;
                    }
                }
                else
                {
                    dnFailCnt++;
                    txtPrgs.AppendText(" -> FAIL-2");
                }

                //FTP 업로드
                if (!File.Exists(locFile))
                {
                    //
                    continue;
                }
                Match match = Regex.Match(locFile, @"\w{2}\-(\d{4})\-(\d{4})\-(\d{8}).*", rxOptM);
                if (match.Success == false)
                {
                    //
                    continue;
                }
                spt = match.Groups[1].Value;
                dpt = match.Groups[2].Value;
                year = match.Groups[3].Value.Substring(0, 4);
                bidDt = match.Groups[3].Value;
                fileNm = match.Value;
                rmtFile = string.Format("{0}/{1}/{2}/{3}", ctgr, spt, year, fileNm);
                if (ftp1.Upload(locFile, rmtFile))
                {
                    //DB 처리
                    tbl = "ta_fnoti";
                    cvp = "spt='" + spt + "', dpt='" + dpt + "', bid_dt='" + bidDt + "', file='" + fileNm + "', wdt=now()";
                    sql = "insert into " + tbl + " set " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();
                    ulCnt++;
                }
            }
            atomLog.AddLog(string.Format("\r\n▶ 수집된 공고-{0}건", ulCnt));
        }

        /// <summary>
        /// 기일변경/취하/취소공고, 정정공고 확인
        /// </summary>
        /// <param name="row"></param>
        /// <param name="a1"></param>
        private void AnmtMdfyChk(DataRow row, string html)
        {
            int tdCnt = 0;
            string ntDt = "", saNo = "", sn1 = "", sn2 = "", pn = "", ntNote = "", spt = "", tid = "", sql;

            HAPDoc doc = new HAPDoc();
            doc.LoadHtml(html);

            HtmlNodeCollection ncTr=doc.DocumentNode.SelectNodes("//table[@summary='기일변경/취하/취소 공고내역 표']/tbody/tr|//table[@summary='정정공고내역 표']/tbody/tr");
            if (ncTr == null) return;

            foreach (HtmlNode ndTr in ncTr)
            {
                HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                tdCnt = ncTd.Count;
                if (tdCnt == 1 && ncTd[0].InnerText.Contains("공고일자"))
                {
                    ntDt = Regex.Match(ncTd[0].InnerText, @"\d{4}.\d{2}.\d{2}", rxOptM).Value.Replace(".", "-");
                    saNo = string.Empty;
                    pn = string.Empty;
                    ntNote = string.Empty;
                }
                if (tdCnt == 3)
                {
                    saNo = Regex.Match(ncTd[0].OuterHtml, @"<b>(\d+타경\d+)</b>", rxOptM).Groups[1].Value;
                    if (ncTd[1].InnerText.Trim() == "정정내용") ntNote = "정정공고";
                    else ntNote = ncTd[2].InnerText.Trim();
                }
                if (tdCnt == 5)
                {                    
                    pn = ncTd[0].InnerText;
                    //MessageBox.Show(string.Format("{0} / {1} / {2} / {3}", dt, saNo, pn, state));
                    spt = row["csCd"].ToString();
                    Match match = Regex.Match(saNo, @"(\d+)타경(\d+)", rxOptM);
                    sn1 = match.Groups[1].Value;
                    sn2 = match.Groups[2].Value;
                    if (pn == "1") pn = "0,1";
                    tid = string.Empty;

                    db.Open();
                    sql = "select tid from ta_list where spt='" + spt + "' and sn1='" + sn1 + "' and sn2='" + sn2 + "' and pn IN (" + pn + ") limit 1";
                    MySqlDataReader dr = db.ExeRdr(sql);
                    dr.Read();
                    if (dr.HasRows)
                    {
                        tid = dr["tid"].ToString();
                    }
                    dr.Close();

                    sql = "select idx from db_tank.tx_rpt where dvsn='" + cmpDvsnCd + "' and tid='" + tid + "' and nt_dt='" + ntDt + "' limit 1";
                    bool exist = db.ExistRow(sql);
                    if (!exist)
                    {
                        if (tid != string.Empty)
                        {
                            sql = "insert into db_tank.tx_rpt set tid='" + tid + "', dvsn='" + cmpDvsnCd + "', nt_dt='" + ntDt + "', nt_note='" + ntNote + "', wdt=curdate()";
                            db.ExeQry(sql);
                        }
                    }
                    db.Close();
                }
            }
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            atomLog.AddLog("실행 완료", 1);
            bgwork.Dispose();

            this.Dispose();
            this.Close();
        }
    }
}
