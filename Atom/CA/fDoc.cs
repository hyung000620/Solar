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
using System.Diagnostics;
using System.Collections;

namespace Atom.CA
{
    public partial class fDoc : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AuctCd auctCd = new AuctCd();
        AtomLog atomLog = new AtomLog(106);     //로그 생성
        string cnvTool = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\pdftohtml.exe";

        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 1500, webCnt = 0;

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        int cmpDvsnCd = 13;     //문건 키워드(RptDvsn: 13)

        string filePath;    //로컬 파일저장 경로
        string vmNm = Environment.MachineName;
                
        FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "CA/", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);

        public fDoc()
        {
            InitializeComponent();
            this.Shown += FDoc_Shown;
        }

        private void FDoc_Shown(object sender, EventArgs e)
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

            if (!File.Exists(cnvTool))
            {
                File.WriteAllBytes(cnvTool, Properties.Resources.pdftohtml);
            }
        }

        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            string dir;
            string[] docArr = null;
            switch (vmNm)
            {
                case "VM-5":
                    docArr = new string[] { "매물명세" };
                    break;
                case "VM-6":
                    docArr = new string[] { "사건내역" };
                    break;
                case "VM-7":
                    docArr = new string[] { "기일내역" };
                    break;
                case "VM-8":
                    docArr = new string[] { "문건송달" };
                    break;
                case "VM-9":
                    docArr = new string[] { "현황조사", "표시목록" };
                    break;
                case "VM-10":
                    docArr = new string[] { "물건상세" };
                    break;
                default:
                    docArr = new string[] { "매물명세" };
                    break;
            }

            foreach (string doc in docArr)
            {
                dir = filePath + @"\" + doc;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                
                switch (doc)
                {
                    case "매물명세":
                        Prc_DpslStmt(dir, "AG");
                        break;
                    case "사건내역":
                        Prc_Event(dir, "AA");
                        break;
                    case "기일내역":
                        Prc_BidDt(dir, "AB");
                        break;
                    case "문건송달":
                        Prc_Dlvry(dir, "AC");
                        break;
                    case "현황조사":
                        Prc_StatIvst(dir, "AD");
                        break;
                    case "표시목록":
                        Prc_ReList(dir, "AE");
                        break;
                    //case "매각공고":    //-> fAnmt.cs 에서 처리
                        //Prc_DpslNt(dir, "AI");
                        //break;
                    case "물건상세":
                        Prc_PdDtl(dir, "AJ");
                        break;
                }
            }
        }

        /// <summary>
        /// 매각물건명세서-모든 파일 다운로드후 업로드-미사용
        /// </summary>
        private void _Prc_DpslStmt(string dir, string ctgr)
        {
            string tid, sql, cvp, cdtn, url, jiwonNm, saNo, maemulSer, maeGiil, jpDeptCd, html, htmlFile,
                locFile = "", rmtFile = "", spt, year, sn, sn1, sn2, pn, fileNm, tbl;
            decimal totCnt = 0, curCnt = 0, dlCnt = 0, ulCnt = 0, dnFailCnt = 0;

            //cdtn = "sta1=11 and bid_dt >= '" + DateTime.Now.ToShortDateString() + "' and bid_dt <= '" + DateTime.Now.AddDays(7).ToShortDateString() + "'";
            cdtn = "sta1=11 and bid_dt='2021-01-13'";
            sql = "select tid, spt, dpt, sn1, sn2, pn, bid_dt from ta_list where " + cdtn + " order by tid";
            //sql = "select tid, spt, dpt, sn1, sn2, pn, bid_dt from ta_list where tid in (197,260,286) order by tid";
            DataTable dt = db.ExeDt(sql);
            Regex rx = new Regex(@"downMaemulMyungDoc\('(.*)?'\)", rxOptM);
            Match match;
            Dictionary<string, string> dicFileRslt;

            totCnt = dt.Rows.Count;
            atomLog.AddLog(string.Format("매각물건명세서 수집시작 대상-{0}", totCnt));
            txtState.AppendText(string.Format("\r\n>>>>> 매각물건명세서 수집시작 대상-{0}", totCnt));    //화면에 진행상태 표시

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                //webCnt++;
                if (webCnt > 0 && webCnt % setCnt == 0) Thread.Sleep(setSleep);

                tid = row["tid"].ToString();
                txtState.AppendText(string.Format("\r\n> tid -> {0} ^ {1} / {2}", tid, curCnt, totCnt));    //화면에 진행상태 표시

                locFile = string.Format(@"{0}\{1}-{2}-{3}{4}-{5}.pdf", dir, ctgr, row["spt"], row["sn1"], row["sn2"].ToString().PadLeft(6, '0'), row["pn"].ToString().PadLeft(4, '0'));
                if (File.Exists(locFile)) continue;
                else webCnt++;

                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                maemulSer = (row["pn"].ToString() == "0") ? "1" : row["pn"].ToString();
                maeGiil = string.Format("{0:yyyyMMdd}", row["bid_dt"]);
                jpDeptCd = row["dpt"].ToString();
                url = "http://www.courtauction.go.kr/RetrieveMobileEstMgakMulMseo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&ordHoi=&orgSaNo=" + saNo + "&maemulSer=" + maemulSer + "&maeGiil=" + maeGiil + "&mGakMulMseoYN=Y&corCanYN=N&jpDeptCd=" + jpDeptCd;
                html = net.GetHtml(url);
                match = rx.Match(html);
                if (match.Success == false)
                {
                    dnFailCnt++;
                    txtState.AppendText(" -> FAIL-1");

                    //문서 매칭오류 Error CD-1
                    db.Open();
                    sql = "insert into db_tank.tx_seq_err set tid=" + tid + ", cd=1, wdt=curdate() ON DUPLICATE KEY UPDATE cd=1, wdt=curdate()";
                    db.ExeQry(sql);
                    db.Close();

                    continue;
                }
                url = match.Groups[1].Value;                
                dicFileRslt = net.DnFile(url, locFile);
                if (dicFileRslt["result"] == "success")
                {
                    dlCnt++;
                    txtState.AppendText(" -> OK");
                }
                else
                {
                    dnFailCnt++;
                    txtState.AppendText(" -> FAIL-2");

                    //다운로드 실패 Error CD-2
                    db.Open();
                    sql = "insert into db_tank.tx_seq_err set tid=" + tid + ", cd=2, wdt=curdate() ON DUPLICATE KEY UPDATE cd=2, wdt=curdate()";
                    db.ExeQry(sql);
                    db.Close();
                }                
            }

            atomLog.AddLog(string.Format("다운로드 성공/실패-{0}/{1}", dlCnt, dnFailCnt));
                        
            DirectoryInfo directory = new DirectoryInfo(dir);
            rx = new Regex(@"(\d+)회\s+(\d{4}.\d{2}.\d{2})\s+(\d{1,3}(,\d{3})+)", rxOptM);    //차회 기일 및 최저가, 보증금율 구하기
            
            List<MySqlParameter> sp = new List<MySqlParameter>();
            List<string> fileList = directory.GetFiles("*.pdf").Where(f => f.CreationTime.Date == DateTime.Today.Date).Select(f => f.FullName).ToList();
            curCnt = 0;
            totCnt = fileList.Count;
            atomLog.AddLog(string.Format("업로드/HTML 변환 {0}", totCnt));
            foreach (string file in fileList)
            {
                curCnt++;
                txtState.AppendText(string.Format("\r\n[업로드-변환] {0} / {1}", curCnt, totCnt));    //화면에 진행상태 표시

                //FTP 업로드
                locFile = file;
                tid = string.Empty;

                match = Regex.Match(locFile, @"\w{2}\-(\d{4})\-(\d{4})(\d{6})\-(\d{4}).*", rxOptM);
                if (match.Success == false)
                {
                    //
                    continue;
                }
                spt = match.Groups[1].Value;
                year = match.Groups[2].Value;
                sn1 = year;
                sn2= match.Groups[3].Value;
                sn = sn1 + sn2;
                pn = match.Groups[4].Value;
                fileNm = match.Value;
                rmtFile = string.Format("{0}/{1}/{2}/{3}", ctgr, spt, year, fileNm);
                sql = "select tid from ta_list where spt='" + spt + "' and sn1='" + sn1 + "' and sn2='" + sn2 + "' and pn='" + pn + "' limit 1";
                db.Open();
                MySqlDataReader dr = db.ExeRdr(sql);
                dr.Read();
                if (dr.HasRows)
                {
                    tid = dr["tid"].ToString();
                }                
                dr.Close();
                db.Close();
                if (tid == string.Empty) continue;

                if (ftp1.Upload(locFile, rmtFile))
                {
                    //DB 처리
                    tbl = (Convert.ToDecimal(year) > 2004) ? ("ta_f" + year) : "ta_f2004";
                    cvp = "ctgr='" + ctgr + "', spt='" + spt + "', tid='" + tid + "', sn='" + sn + "', file='" + fileNm + "', wdt=curdate()";
                    sql = "insert into " + tbl + " set " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                    DB_Proc(sql);
                }

                htmlFile = file.Replace("pdf", "html");
                if (File.Exists(htmlFile) == false)
                {
                    Process proc = new Process();
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = cnvTool;
                    psi.Arguments = @"-c -i -noframes -zoom 1 -enc UTF-8 """ + file + "\"";
                    psi.WorkingDirectory = @"c:\";
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    psi.RedirectStandardInput = true;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    proc.EnableRaisingEvents = false;
                    proc.StartInfo = psi;
                    proc.Start();
                    proc.StandardInput.Write(Environment.NewLine);
                    proc.StandardInput.Close();

                    if (proc.StandardError.ReadToEnd() != string.Empty)
                    {
                        //pdf -> html 변환 실패 Error CD-3
                        db.Open();
                        sql = "insert into db_tank.tx_seq_err set tid=" + tid + ", cd=3, wdt=curdate() ON DUPLICATE KEY UPDATE cd=3, wdt=curdate()";
                        db.ExeQry(sql);
                        db.Close();

                        //proc.WaitForExit();
                        //proc.Close();
                        //continue;
                        //뒤에 다른 pdf 파일이 병합 된 경우 -> 그러나 뒷부분 빼고 구문분석은 가능 하므로 패스하지 않음
                    }
                    proc.WaitForExit();
                    proc.Close();
                }

                Stream stream = File.OpenRead(htmlFile);
                StreamReader sr = new StreamReader(stream, Encoding.UTF8);
                html = sr.ReadToEnd();
                sr.Close();
                sr.Dispose();
                stream.Close();
                stream.Dispose();

                MatchCollection mc = rx.Matches(html);
                if (mc.Count == 0)
                {
                    //회차 정보가 없는 사건 Error CD-4
                    db.Open();
                    sql = "insert into db_tank.tx_seq_err set tid=" + tid + ", cd=4, wdt=curdate() ON DUPLICATE KEY UPDATE cd=4, wdt=curdate()";
                    db.ExeQry(sql);
                    db.Close();
                    continue;
                }

                //삭제하고 무조건 갱신
                db.Open();
                sql = "delete from ta_seq where tid=" + tid;
                db.ExeQry(sql);
                foreach (Match m in mc)
                {
                    GroupCollection grps = m.Groups;
                    sql = "insert into ta_seq set tid=@tid, seq=@seq, bid_dt=@bid_dt, minb_amt=@minb_amt, wdt=CURDATE()";
                    sp.Add(new MySqlParameter("@tid", tid));
                    sp.Add(new MySqlParameter("@seq", grps[1].Value));
                    sp.Add(new MySqlParameter("@bid_dt", grps[2].Value));
                    sp.Add(new MySqlParameter("@minb_amt", grps[3].Value.Replace(",", string.Empty)));
                    db.ExeQry(sql, sp);
                    sp.Clear();
                }
                db.Close();

                if (File.Exists(htmlFile))
                {
                    File.Delete(htmlFile);
                }
            }
        }

        /// <summary>
        /// 매각물건명세서-개별 다운로드/업로드, 회차정보 처리
        /// </summary>
        private void Prc_DpslStmt(string dir, string ctgr)
        {
            string tid, sql, cvp, cdtn, url, jiwonNm, saNo, maemulSer, maeGiil, jpDeptCd, html, htmlFile,
                locFile = "", rmtFile = "", spt, year, sn, sn1, sn2, pn, fileNm, tbl;
            string oldDoc, newDoc;
            decimal totCnt = 0, curCnt = 0, dlCnt = 0, ulCnt = 0, dnFailCnt = 0, mvCnt = 0;

            if (DateTime.Now.ToShortDateString() == "0000-00-00")
            {
                //법원 사이트 중단시
                cdtn = "sta1=11 and bid_dt = '" + DateTime.Now.AddDays(7).ToShortDateString() + "'";
            }
            else
            {
                cdtn = "sta1=11 and bid_dt >= '" + DateTime.Now.ToShortDateString() + "' and bid_dt <= '" + DateTime.Now.AddDays(7).ToShortDateString() + "'";
            }           
            sql = "select tid, spt, dpt, sn1, sn2, pn, bid_dt from ta_list where " + cdtn + " order by tid";
            DataTable dt = db.ExeDt(sql);
            Regex rx1 = new Regex(@"downMaemulMyungDoc\('(.*)?'\)", rxOptM);
            Regex rx2 = new Regex(@"(\d+)회\s+(\d{4}.\d{2}.\d{2})\s+(\d{1,3}(,\d{3})+)", rxOptM);    //차회 기일 및 최저가, 보증금율 구하기
            Match match;
            Dictionary<string, string> dicFileRslt;
            List<MySqlParameter> sp = new List<MySqlParameter>();
            
            totCnt = dt.Rows.Count;
            atomLog.AddLog(string.Format("매각물건명세서 수집시작 대상-{0}", totCnt));
            txtState.AppendText(string.Format("\r\n>>>>> 매각물건명세서 수집시작 대상-{0}", totCnt));    //화면에 진행상태 표시
            
            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                if (webCnt > 0 && webCnt % setCnt == 0) Thread.Sleep(setSleep);

                tid = row["tid"].ToString();
                spt = row["spt"].ToString();
                sn1 = row["sn1"].ToString();
                sn2 = row["sn2"].ToString().PadLeft(6, '0');
                sn = sn1 + sn2;
                pn = row["pn"].ToString().PadLeft(4, '0');
                txtState.AppendText(string.Format("\r\n> tid -> {0} ^ {1} / {2}", tid, curCnt, totCnt));    //화면에 진행상태 표시

                locFile = string.Format(@"{0}\{1}-{2}-{3}{4}-{5}.pdf", dir, ctgr, spt, sn1, sn2, pn);
                if (File.Exists(locFile)) continue;
                else webCnt++;

                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                maemulSer = (row["pn"].ToString() == "0") ? "1" : row["pn"].ToString();
                maeGiil = string.Format("{0:yyyyMMdd}", row["bid_dt"]);
                jpDeptCd = row["dpt"].ToString();
                url = "http://www.courtauction.go.kr/RetrieveMobileEstMgakMulMseo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&ordHoi=&orgSaNo=" + saNo + "&maemulSer=" + maemulSer + "&maeGiil=" + maeGiil + "&mGakMulMseoYN=Y&corCanYN=N&jpDeptCd=" + jpDeptCd;
                html = net.GetHtml(url);
                match = rx1.Match(html);
                if (match.Success == false)
                {
                    //문서 매칭오류 Error CD-1
                    dnFailCnt++;
                    txtState.AppendText(" -> FAIL-1");
                    sql = "insert into db_tank.tx_seq_err set tid=" + tid + ", cd=1, wdt=curdate() ON DUPLICATE KEY UPDATE cd=1, wdt=curdate()";
                    DB_Proc(sql);
                    continue;
                }
                url = match.Groups[1].Value;
                dicFileRslt = net.DnFile(url, locFile);
                if (dicFileRslt["result"] != "success")
                {
                    //다운로드 실패 Error CD-2
                    dnFailCnt++;
                    txtState.AppendText(" -> FAIL-2");
                    sql = "insert into db_tank.tx_seq_err set tid=" + tid + ", cd=2, wdt=curdate() ON DUPLICATE KEY UPDATE cd=2, wdt=curdate()";
                    DB_Proc(sql);
                    continue;
                }

                //50KB 미만 재시도
                FileInfo fi = new FileInfo(locFile);
                if ((fi.Length / 1024) < 50)
                {
                    net.DnFile(url, locFile);
                }

                fi = new FileInfo(locFile);
                if ((fi.Length / 1024) < 50)
                {
                    //파일크기 미달 Error CD-3
                    dnFailCnt++;
                    txtState.AppendText(" -> FAIL-3");
                    sql = "insert into db_tank.tx_seq_err set tid=" + tid + ", cd=3, wdt=curdate() ON DUPLICATE KEY UPDATE cd=3, wdt=curdate()";
                    DB_Proc(sql);
                    continue;
                }

                //다운로드 성공/FTP 업로드
                dlCnt++;
                fileNm = string.Format(@"{0}-{1}-{2}{3}-{4}.pdf", ctgr, spt, sn1, sn2, pn);
                rmtFile = string.Format("{0}/{1}/{2}/{3}", ctgr, spt, sn1, fileNm);
                
                if (ftp1.Upload(locFile, rmtFile))
                {
                    //DB 처리
                    tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
                    
                    sql = $"delete from {tbl} where ctgr='{ctgr}' and tid='{tid}'";     //[매각물건명세서]는 무조건 단일 파일만 기록한다.(물번 합침 등으로 인한 2개 이상 파일이 있을 수 있음)
                    DB_Proc(sql);

                    cvp = "ctgr='" + ctgr + "', spt='" + spt + "', tid='" + tid + "', sn='" + sn + "', file='" + fileNm + "', wdt=curdate()";
                    sql = "insert into " + tbl + " set " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                    DB_Proc(sql);
                    ulCnt++;
                }

                //PDF -> HTML 변환
                htmlFile = locFile.Replace("pdf", "html");
                if (File.Exists(htmlFile) == false)
                {
                    Process proc = new Process();
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = cnvTool;
                    psi.Arguments = @"-c -i -noframes -zoom 1 -enc UTF-8 """ + locFile + "\"";
                    psi.WorkingDirectory = @"c:\";
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    psi.RedirectStandardInput = true;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    proc.EnableRaisingEvents = false;
                    proc.StartInfo = psi;
                    proc.Start();
                    proc.StandardInput.Write(Environment.NewLine);
                    proc.StandardInput.Close();

                    if (proc.StandardError.ReadToEnd() != string.Empty)
                    {
                        //변환 실패 Error CD-4
                        sql = "insert into db_tank.tx_seq_err set tid=" + tid + ", cd=4, wdt=curdate() ON DUPLICATE KEY UPDATE cd=4, wdt=curdate()";
                        DB_Proc(sql);
                        //뒤에 다른 pdf 파일이 병합 된 경우 -> 그러나 뒷부분 빼고 구문분석은 가능 하므로 패스하지 않음
                    }
                    proc.WaitForExit();
                    proc.Close();
                }

                if (File.Exists(htmlFile) == false)
                {
                    //HTML 생성 Error CD-6
                    sql = "insert into db_tank.tx_seq_err set tid=" + tid + ", cd=6, wdt=curdate() ON DUPLICATE KEY UPDATE cd=6, wdt=curdate()";
                    DB_Proc(sql);
                    continue;
                }

                //회차 정보 추출
                Stream stream = File.OpenRead(htmlFile);
                StreamReader sr = new StreamReader(stream, Encoding.UTF8);
                html = sr.ReadToEnd();
                sr.Close();
                sr.Dispose();
                stream.Close();
                stream.Dispose();

                MatchCollection mc = rx2.Matches(html);
                if (mc.Count == 0)
                {
                    //회차 정보가 없는 사건 Error CD-5
                    sql = "insert into db_tank.tx_seq_err set tid=" + tid + ", cd=5, wdt=curdate() ON DUPLICATE KEY UPDATE cd=5, wdt=curdate()";
                    DB_Proc(sql);
                    continue;
                }

                //삭제하고 무조건 갱신
                db.Open();
                sql = "delete from ta_seq where tid=" + tid;
                db.ExeQry(sql);
                foreach (Match m in mc)
                {
                    GroupCollection grps = m.Groups;
                    sql = "insert into ta_seq set tid=@tid, seq=@seq, bid_dt=@bid_dt, minb_amt=@minb_amt, wdt=CURDATE()";
                    sp.Add(new MySqlParameter("@tid", tid));
                    sp.Add(new MySqlParameter("@seq", grps[1].Value));
                    sp.Add(new MySqlParameter("@bid_dt", grps[2].Value));
                    sp.Add(new MySqlParameter("@minb_amt", grps[3].Value.Replace(",", string.Empty)));
                    db.ExeQry(sql, sp);
                    sp.Clear();
                }
                db.Close();
                if (File.Exists(htmlFile))
                {
                    File.Delete(htmlFile);
                }
            }
            atomLog.AddLog(string.Format("매각물건 명세서(PDF) 수집-{0}건, 실패-{1}건", ulCnt, dnFailCnt));
            
            atomLog.AddLog("(구)매각물건 명세서(html) 수집시작");
            dlCnt = 0;
            HAPDoc doc = new HAPDoc();
            List<string> lst = new List<string>();
            string dataStr;
            foreach (DataRow row in dt.Rows)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                txtState.AppendText(string.Format("\r\n> 매물비교 {0} / {1}", webCnt, totCnt));

                lst.Clear();
                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                maemulSer = (row["pn"].ToString() == "0") ? "1" : row["pn"].ToString();
                url = "http://www.courtauction.go.kr/RetrieveRealEstMgakMulMseo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&maemulSer=" + maemulSer + "&boGbn=B";
                html = net.GetHtml(url);
                if (html.Contains("매각물건명세서가 없습니다") || html.Contains("HttpWebException")) continue;

                doc.LoadHtml(html);
                HtmlNodeCollection nc = doc.DocumentNode.SelectNodes("//table[@class='Ltbl_dt' or @class='Ltbl_list']");
                foreach (HtmlNode nd in nc)
                {
                    lst.Add(nd.OuterHtml);
                }
                if (lst.Count == 0) continue;

                tid = row["tid"].ToString();
                dataStr = string.Join("\r\n", lst.ToArray());
                dataStr = Regex.Replace(dataStr, @"^\s+", string.Empty, RegexOptions.Multiline);
                sql = "insert into db_tank.ta_dpsl_html set tid=@tid, new=@new, new_dt=curdate() ON DUPLICATE KEY UPDATE old=new, new=@new, old_dt=new_dt, new_dt=curdate()";
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@new", dataStr));
                db.Open();
                db.ExeQry(sql, sp);
                sp.Clear();
                db.Close();
                dlCnt++;
            }
            
            sql = "select H.* from db_main.ta_list L , db_tank.ta_dpsl_html H where L.tid=H.tid and H.new_dt=curdate() and old_dt != '0000-00-00' and old != new";
            dt = db.ExeDt(sql);
            if (dt.Rows.Count > 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    oldDoc = Regex.Replace(row["old"].ToString(), @"<th>작성일자</th>\s+<td>[\d\.]{4,}</td>", string.Empty, rxOptM);
                    newDoc = Regex.Replace(row["new"].ToString(), @"<th>작성일자</th>\s+<td>[\d\.]{4,}</td>", string.Empty, rxOptM);
                    if (oldDoc == newDoc) continue;

                    sql = "insert into db_tank.ta_dpsl_cmp set tid=@tid, old=@old, new=@new, old_dt=@old_dt, new_dt=@new_dt, wdt=curdate()";
                    sp.Add(new MySqlParameter("@tid", row["tid"]));
                    sp.Add(new MySqlParameter("@old", row["old"]));
                    sp.Add(new MySqlParameter("@new", row["new"]));
                    sp.Add(new MySqlParameter("@old_dt", row["old_dt"]));
                    sp.Add(new MySqlParameter("@new_dt", row["new_dt"]));
                    db.Open();
                    db.ExeQry(sql, sp);
                    sp.Clear();
                    db.Close();
                    mvCnt++;
                }
            }
            atomLog.AddLog(string.Format("(구)매각물건 명세서(html) 수집-{0}건, 변동-{1}건", dlCnt, mvCnt));
            
            //자동화 스케쥴링 보류
            /*
            string bidDt = DateTime.Now.AddDays(7).ToShortDateString();
            string cat1, cat2, cat3;

            sql = "select tid, cat1, cat2, cat3 from ta_list where sta2='1110' and bid_dt='" + bidDt + "'";
            dt = db.ExeDt(sql);
            foreach (DataRow row in dt.Rows)
            {
                tid = row["tid"].ToString();
                cat1 = row["cat1"].ToString();
                cat2 = row["cat2"].ToString();
                cat3 = row["cat3"].ToString();

                Prc_DpslStmtAnaly(tid, cat1, cat2, cat3);
            }
            */
            atomLog.AddLog("실행 완료", 1);
        }

        /// <summary>
        /// 매각물건 명세서(html) 내용 추출
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="cat1"></param>
        /// <param name="cat2"></param>
        /// <param name="cat3"></param>
        private void Prc_DpslStmtAnaly(string tid, string cat1, string cat2, string cat3)
        {
            int tdCnt = 0, eqIdx = 0, lsCnt = 0;
            UInt64 _deposit = 0, _mMoney = 0, _tMoney = 0;
            string sql, html, lsNo = "0", rgstNote = "", leasNote = "";
            string prsn, prsn2, part, src, invType, useType, useCd = "", term, deposit, mMoney, tMoney, mvDt, fxDt, shrDt;
            string Nm, Nm2, prevNm;
            bool findFlag = false, highFlag = false, shrDtFlag = false, jnsFlag = false, imcFlag = false;

            string ptrnMny1 = @"^1[차: ]+(.*?)[원]*[,/ ]+2[차: ]+(.*)";
            string ptrnMny2 = @"([\d,]{3,})원\(1차\)[, ]+([\d,]{3,})원\(2차";
            string ptrnMny3 = @"\d{4}[. ]\d+[. ]\d+[. ]*(.*?)원[, ]+\d{4}[. ]\d+[. ]\d+[. ]*(.*?)원";

            List<MySqlParameter> sp = new List<MySqlParameter>();

            //물건 목록
            sql = "select no, adrs, dvsn from ta_ls where tid=" + tid;
            DataTable dtM = db.ExeDt(sql);

            HAPDoc doc = new HAPDoc();

            //매물명세서
            DataTable dtS = new DataTable();
            dtS.Columns.Add("prsn", typeof(string));    //점유자 성명
            dtS.Columns.Add("part", typeof(string));    //점유 부분
            dtS.Columns.Add("src", typeof(string));     //정보 출처 구분
            dtS.Columns.Add("ust", typeof(string));     //점유의 권원
            dtS.Columns.Add("term", typeof(string));    //임대차기간(점유기간)
            dtS.Columns.Add("deposit", typeof(string)); //보증금
            dtS.Columns.Add("mMoney", typeof(string));   //차임
            dtS.Columns.Add("mvDt", typeof(string));    //전입신고 일자,사업자등록 신청일자
            dtS.Columns.Add("fxDt", typeof(string));    //확정 일자
            dtS.Columns.Add("shrDt", typeof(string));   //배당 요구여부(배당요구일자)
            dtS.Columns.Add("highFlag", typeof(bool));  //상위 레벨 문서 포함여부

            //임차인현황(ta_leas)
            DataTable dtL = new DataTable();
            dtL.Columns.Add("idx", typeof(string));     //idx
            dtL.Columns.Add("tid", typeof(string));     //TID
            dtL.Columns.Add("lsNo", typeof(string));    //목록 번호
            dtL.Columns.Add("prsn", typeof(string));    //점유인
            dtL.Columns.Add("invType", typeof(string)); //당사자 구분
            dtL.Columns.Add("part", typeof(string));    //점유 부분
            dtL.Columns.Add("useType", typeof(string)); //점유의 근원
            dtL.Columns.Add("useCd", typeof(string));   //용도코드
            dtL.Columns.Add("term", typeof(string));    //점유 기간
            dtL.Columns.Add("deposit", typeof(string)); //보증(전세)금
            dtL.Columns.Add("mMoney", typeof(string));  //월세(차임)
            dtL.Columns.Add("tMoney", typeof(string));  //사글세(차임)
            dtL.Columns.Add("tMnth", typeof(string));   //사글세 개월수
            dtL.Columns.Add("biz", typeof(string));     //사업자 여부
            dtL.Columns.Add("mvDt", typeof(string));    //전입신고 일자,사업자등록 신청일자
            dtL.Columns.Add("fxDt", typeof(string));    //확정 일자
            dtL.Columns.Add("shrDt", typeof(string));   //배당 신청일자
            dtL.Columns.Add("note", typeof(string));    //기타

            sql = "select *, date_format(mv_dt,'%Y-%m-%d') as mvDt, date_format(fx_dt,'%Y-%m-%d') as fxDt, date_format(shr_dt,'%Y-%m-%d') as shrDt from ta_leas where tid=" + tid + " order by prsn";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                DataRow row = dtL.NewRow();
                row["idx"] = dr["idx"].ToString();
                row["tid"] = dr["tid"].ToString();
                row["lsNo"] = dr["ls_no"].ToString();
                row["prsn"] = dr["prsn"].ToString();
                row["invType"] = dr["inv_type"].ToString();
                row["part"] = dr["part"].ToString();
                row["useType"] = dr["use_type"].ToString();
                row["useCd"] = dr["use_cd"].ToString();
                row["term"] = dr["term"].ToString();
                row["deposit"] = dr["deposit"].ToString();
                row["mMoney"] = dr["m_money"].ToString();
                row["tMoney"] = dr["t_money"].ToString();
                row["tMnth"] = dr["t_mnth"].ToString();
                row["biz"] = dr["biz"].ToString();
                row["mvDt"] = dr["mvDt"].ToString();
                row["fxDt"] = dr["fxDt"].ToString();
                row["shrDt"] = dr["shrDt"].ToString();
                row["note"] = dr["note"].ToString();
                dtL.Rows.Add(row);
            }
            dr.Close();
            db.Close();

            //임차인현황-원본 복사(ta_leas)
            DataTable dtC = dtL.Copy();

            sql = "select new from db_tank.ta_dpsl_html where tid=" + tid;
            db.Open();
            dr = db.ExeRdr(sql);
            if (dr.HasRows == false)
            {
                dr.Close();
                db.Close();
                //MessageBox.Show("추출할 수 없는 물건 입니다.");
                return;
            }
            dr.Read();
            html = dr["new"].ToString();
            dr.Close();
            db.Close();

            doc.LoadHtml(html);
            if (cat1 == "30" || cat2 == "4010")
            {
                HtmlNode ndTh = doc.DocumentNode.SelectSingleNode("//table[@summary='매각물건명세서 기본정보 표']/tr/th[contains(text(), '최선순위 설정일자')]");
                if (ndTh != null)
                {
                    rgstNote = ndTh.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                    if (rgstNote != string.Empty)
                    {
                        //기존 값에 추가하여 저장하도록 수정하여야 함
                        rgstNote = "▶최선순위설정일자: " + rgstNote;
                        sql = "update ta_dtl set rgst_note=@rgst_note where tid=" + tid;    //등기부 권리관계 기타
                        sp.Add(new MySqlParameter("@rgst_note", rgstNote));
                        db.Open();
                        db.ExeQry(sql, sp);
                        sp.Clear();
                        db.Close();
                        return;
                    }
                }
            }

            HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='매각물건명세서 상세표']/tbody/tr");
            foreach (HtmlNode tr in ncTr)
            {
                highFlag = true;
                prsn = ""; part = ""; src = ""; useType = ""; term = ""; deposit = ""; mMoney = ""; mvDt = ""; fxDt = ""; shrDt = "";

                HtmlNodeCollection ncTd = tr.SelectNodes("./td");
                tdCnt = ncTd.Count;
                if (tdCnt == 10)
                {
                    prsn = ncTd[0].InnerText.Trim();
                    part = ncTd[1].InnerText.Trim();
                    src = ncTd[2].InnerText.Trim();
                    useType = ncTd[3].InnerText.Trim();
                    term = ncTd[4].InnerText.Trim();
                    deposit = ncTd[5].InnerText.Trim();
                    mMoney = ncTd[6].InnerText.Trim();
                    mvDt = ncTd[7].InnerText.Trim();
                    fxDt = ncTd[8].InnerText.Trim();
                    shrDt = ncTd[9].InnerText.Trim();
                    if (ncTd[0].OuterHtml.Contains("rowspan=\"1\"") && src.Contains("현황조사"))
                    {
                        highFlag = false;
                    }
                }
                else if (tdCnt == 9)
                {
                    part = ncTd[0].InnerText.Trim();
                    src = ncTd[1].InnerText.Trim();
                    useType = ncTd[2].InnerText.Trim();
                    term = ncTd[3].InnerText.Trim();
                    deposit = ncTd[4].InnerText.Trim();
                    mMoney = ncTd[5].InnerText.Trim();
                    mvDt = ncTd[6].InnerText.Trim();
                    fxDt = ncTd[7].InnerText.Trim();
                    shrDt = ncTd[8].InnerText.Trim();
                }
                else if (tdCnt == 1)
                {
                    leasNote = ncTd[0].InnerHtml;
                    leasNote = leasNote.Replace(@"&lt; 비고 &gt; &nbsp;", string.Empty);
                    leasNote = leasNote.Replace("<br>", "\r\n* ").Trim();
                    continue;
                }
                else
                {
                    continue;
                }

                if (Regex.IsMatch(mvDt, "^무|^없음|불능|불명|무상|무임|미신고|미등록|미전입|무료|이사감|미필|안받았음|미존재|확인불능|퇴사|받지[ ]*않음|전입[ ]*안됨|사업자[ ]*없음")) mvDt = "0000-00-01";
                else if (Regex.IsMatch(mvDt, "미상|미확인") || string.IsNullOrEmpty(mvDt)) mvDt = "0000-00-03";
                else mvDt = getDateParse(mvDt);

                if (Regex.IsMatch(fxDt, "^무|^없음|불능|불명|무상|무임|미신고|미등록|미전입|무료|이사감|미필|안받았음|미존재|확인불능|퇴사|받지[ ]*않음|전입[ ]*안됨|사업자[ ]*없음")) fxDt = "0000-00-01";
                else if (Regex.IsMatch(fxDt, "미상|미확인") || string.IsNullOrEmpty(fxDt)) fxDt = "0000-00-03";
                else fxDt = getDateParse(fxDt);

                if (string.IsNullOrEmpty(shrDt)) shrDt = "0000-00-01";
                else shrDt = getDateParse(shrDt);

                dtS.Rows.Add(prsn, part, src, useType, term, deposit, mMoney, mvDt, fxDt, shrDt, highFlag);
            }
            //MessageBox.Show(leasNote);

            Nm = string.Empty;
            prevNm = string.Empty;

            foreach (DataRow row in dtS.Rows)
            {
                findFlag = false;
                eqIdx = -1;
                mMoney = "0"; tMoney = "0";

                prsn = row["prsn"].ToString();
                src = row["src"].ToString();
                highFlag = Convert.ToBoolean(row["highFlag"]);
                if (string.IsNullOrEmpty(prsn) == false) Nm = prsn;

                deposit = row["deposit"].ToString();
                mMoney = row["mMoney"].ToString();
                mMoney = Regex.Replace(mMoney, @"^(매년|년세|연세|연|일년|1년)", string.Empty);

                if (Regex.IsMatch(deposit, @"^없음")) deposit = "1";
                else if (deposit == string.Empty || deposit == "0" || Regex.IsMatch(deposit, @"미상|해당[ ]*없음")) deposit = "3";
                else
                {
                    if (Regex.IsMatch(deposit, ptrnMny1)) deposit = MoneyChk(Regex.Match(deposit, ptrnMny1).Groups[2].Value);
                    else if (Regex.IsMatch(deposit, ptrnMny2)) deposit = MoneyChk(Regex.Match(deposit, ptrnMny2).Groups[2].Value);
                    else if (Regex.IsMatch(deposit, ptrnMny3)) deposit = MoneyChk(Regex.Match(deposit, ptrnMny3).Groups[2].Value);
                    else deposit = MoneyChk(deposit);
                }
                if (deposit == "") deposit = "0";

                if (Regex.IsMatch(mMoney, ptrnMny1)) mMoney = MoneyChk(Regex.Match(mMoney, ptrnMny1).Groups[2].Value);
                else if (Regex.IsMatch(mMoney, ptrnMny2)) mMoney = MoneyChk(Regex.Match(mMoney, ptrnMny2).Groups[2].Value);
                else if (Regex.IsMatch(mMoney, ptrnMny3)) mMoney = MoneyChk(Regex.Match(mMoney, ptrnMny3).Groups[2].Value);
                else mMoney = MoneyChk(mMoney);

                if (Regex.IsMatch(row["mMoney"].ToString(), @"^(매년|년세|연세|연|일년|1년)"))
                {
                    tMoney = mMoney;
                    mMoney = "0";
                }
                //if (tMoney == "") tMoney = "0";
                //if (mMoney == "") mMoney = "0";

                mvDt = row["mvDt"].ToString();
                fxDt = row["fxDt"].ToString();
                shrDt = row["shrDt"].ToString();

                foreach (DataRow r in dtL.Rows)
                {
                    prsn2 = Regex.Replace(r["prsn"].ToString(), @"\(주\)|주식회사|\s+", string.Empty);
                    if (prsn2.Contains("(")) prsn2 = prsn2.Remove(prsn2.IndexOf("("));

                    Nm2 = Regex.Replace(Nm, @"\(주\)|주식회사|\s+", string.Empty);
                    if (Nm2.Contains("(")) Nm2 = Nm2.Remove(Nm2.IndexOf("("));

                    if (r["prsn"].ToString() == Nm || (Nm2.Length >= 2 && prsn2 == Nm2))
                    {
                        findFlag = true;
                        eqIdx = dtL.Rows.IndexOf(r);

                        //MessageBox.Show(string.Format("진입 -> Nm:{0} / Prsn:{1}", Nm, r["prsn"]));
                        shrDtFlag = (r["invType"].ToString().Contains("등기자")) ? false : true;       //삭제예정
                        jnsFlag = (r["invType"].ToString().Contains("전세권등기자")) ? true : false;
                        imcFlag = (r["invType"].ToString().Contains("임차권등기자")) ? true : false;

                        if (src.Contains("현황조사"))
                        {
                            continue;
                        }

                        r["part"] = row["part"];
                        r["useType"] = row["ust"];
                        r["term"] = row["term"];
                        r["deposit"] = deposit;
                        r["tMnth"] = "";

                        if (jnsFlag)
                        {
                            if (mvDt.Contains("0000-00") == false) r["mvDt"] = mvDt;
                            if (fxDt.Contains("0000-00") == false) r["fxDt"] = fxDt;
                            if (r["shrDt"].ToString() == "0000-00-00" && shrDt != "0000-00-01") r["shrDt"] = shrDt;
                            r["mMoney"] = mMoney;
                            r["tMoney"] = tMoney;
                        }
                        else if (imcFlag)
                        {
                            if (mvDt.Contains("0000-00") == false)
                            {
                                if (r["mvDt"].ToString() != "0000-00-00")
                                {
                                    if (Convert.ToDateTime(r["mvDt"]) > Convert.ToDateTime(mvDt)) r["mvDt"] = mvDt;
                                }
                            }
                            if (fxDt.Contains("0000-00") == false)
                            {
                                if (r["fxDt"].ToString() != "0000-00-00")
                                {
                                    if (Convert.ToDateTime(r["fxDt"]) > Convert.ToDateTime(fxDt)) r["fxDt"] = fxDt;
                                }
                            }
                            if (r["shrDt"].ToString() == "0000-00-00" && shrDt != "0000-00-01") r["shrDt"] = shrDt;
                            if (mMoney != "" && mMoney != "0") r["mMoney"] = mMoney;
                            if (tMoney != "" && tMoney != "0") r["tMoney"] = tMoney;
                        }
                        else
                        {
                            r["mvDt"] = mvDt;
                            r["fxDt"] = fxDt;
                            r["shrDt"] = shrDt;
                            r["mMoney"] = mMoney;
                            r["tMoney"] = tMoney;
                        }

                        if (src.Contains("권리신고")) break;
                    }
                }

                //현황조사만 있는 경우
                if (findFlag && highFlag == false && (eqIdx > -1) && src.Contains("현황조사"))
                {
                    //MessageBox.Show(Nm);
                    shrDtFlag = (dtL.Rows[eqIdx]["invType"].ToString().Contains("등기자")) ? false : true;

                    dtL.Rows[eqIdx]["term"] = row["term"];
                    dtL.Rows[eqIdx]["deposit"] = deposit;
                    dtL.Rows[eqIdx]["mMoney"] = mMoney;
                    dtL.Rows[eqIdx]["tMoney"] = tMoney;
                    dtL.Rows[eqIdx]["tMnth"] = "";
                    dtL.Rows[eqIdx]["mvDt"] = mvDt;
                    dtL.Rows[eqIdx]["fxDt"] = fxDt;
                    if (shrDtFlag) dtL.Rows[eqIdx]["shrDt"] = shrDt;

                    //임차인현황 DB원본(복제본) 갱신
                    var xRow = dtC.Rows.Cast<DataRow>().Where(t => t["idx"].ToString() == dtL.Rows[eqIdx]["idx"]?.ToString()).FirstOrDefault();
                    if (xRow != null)
                    {
                        xRow["term"] = row["term"];
                        xRow["deposit"] = deposit;
                        xRow["mMoney"] = mMoney;
                        xRow["tMoney"] = tMoney;
                        xRow["tMnth"] = "";
                        xRow["mvDt"] = mvDt;
                        xRow["fxDt"] = fxDt;
                        if (shrDtFlag) xRow["shrDt"] = shrDt;
                    }
                }

                if (findFlag == false)
                {
                    useType = row["ust"].ToString();
                    if (Regex.IsMatch(useType, @"주거(임차인|임차권자|점유자|전세권자|주택임차권자)|주민등록|(전입신고)+.*임차인|미확인[ ]*전입자|전입자점유자")) useType = "주거";
                    else if (Regex.IsMatch(useType, @"주거[ ]*및[ ]*.*[^농지](임차인|점유자)")) useType = "주거및점포";
                    else if (Regex.IsMatch(useType, @"점포(임차인|점유자|전세권자|임차권자)|(시설)+.*임차인")) useType = "점포";
                    else if (Regex.IsMatch(useType, @"공장(임차인|점유자|전세권자)|공장[ ]*및[ ]*사무실[ ]*임차인")) useType = "공장";
                    else if (Regex.IsMatch(useType, @"^사무[실소등추정 ]+(임차인|점유자)|관리사무소")) useType = "사무실";
                    else if (Regex.IsMatch(useType, @"^(대지|토지|농지|농업|밭|과수원|경작|재배|수목)(임대)*(임대차)*(임차인|점유자)|전\([\w ]+\)임차인|야적장임차인|재배임차인|(토지|건부지)점유자|전[ ]*및[ ]*온실")) useType = "토지";
                    else if (Regex.IsMatch(useType, @"미상")) useType = "미상";

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
                        if (cat3 == "201013" || cat3 == "201014" || cat3 == "201015") useCd = "1";
                    }
                    else useCd = "0";

                    lsCnt = dtM.Rows.Count;

                    if (lsCnt == 0) lsNo = "0";
                    else if (lsCnt == 1)
                    {
                        lsNo = dtM.Rows[0]["no"].ToString();
                    }
                    else
                    {
                        if (Regex.IsMatch(row["part"].ToString(), @"\d+호"))
                        {
                            Match match = Regex.Match(row["part"].ToString(), @"\d+호");
                            var xRow = dtM.Rows.Cast<DataRow>().Where(t => t["adrs"].ToString().Contains(match.Value)).FirstOrDefault();
                            if (xRow != null)
                            {
                                lsNo = xRow["no"].ToString();
                            }
                        }
                        else
                        {
                            lsNo = "0";
                        }

                        if (lsNo == "0" && lsCnt == 2)
                        {
                            if (dtM.Rows[0]["dvsn"].ToString() == "토지" && dtM.Rows[1]["dvsn"].ToString().Contains("건물"))
                            {
                                lsNo = dtM.Rows[1]["no"].ToString();
                            }
                            else if (dtM.Rows[0]["dvsn"].ToString() == "건물" && dtM.Rows[1]["dvsn"].ToString().Contains("토지"))
                            {
                                lsNo = dtM.Rows[0]["no"].ToString();
                            }
                        }
                    }

                    invType = row["ust"].ToString();
                    Match m = Regex.Match(invType, @"전점유자|주택임차권자|전세권자|임차권자|점유자|임차인");
                    if (m.Success) invType = m.Value;
                    else invType = "";

                    DataRow rN = dtL.NewRow();
                    rN["idx"] = "";
                    rN["tid"] = tid;
                    rN["lsNo"] = lsNo;
                    rN["prsn"] = Nm;
                    rN["invType"] = invType;
                    rN["part"] = row["part"];
                    rN["useType"] = row["ust"];
                    rN["useCd"] = useCd;
                    rN["term"] = row["term"];
                    rN["deposit"] = deposit;
                    rN["mMoney"] = mMoney;
                    rN["tMoney"] = tMoney;
                    rN["tMnth"] = "";
                    rN["biz"] = (useCd == "2" || useCd == "3" || useCd == "8" || useCd == "9") ? "1" : "0";
                    rN["mvDt"] = mvDt;
                    rN["fxDt"] = fxDt;
                    rN["shrDt"] = shrDt;
                    dtL.Rows.Add(rN);
                }
                prevNm = Nm;
            }

            //임차인현황 DB원본(복제본)과 최종 dtL 비교 후 변동 값 기타에 기록
            //MessageBox.Show(dtL.Rows.Count.ToString());
            List<string> lsNote = new List<string>();
            foreach (DataRow r in dtC.Rows)
            {
                lsNote.Clear();
                var xRow = dtL.Rows.Cast<DataRow>().Where(t => t["idx"].ToString() == r["idx"]?.ToString()).FirstOrDefault();
                if (xRow != null)
                {
                    if (r["deposit"].ToString() != xRow["deposit"].ToString() && r["deposit"].ToString() != "0")
                    {
                        _deposit = Convert.ToUInt64(r["deposit"]);
                        if (_deposit > 10000) lsNote.Add(string.Format("보:{0}만원", string.Format("{0:N0}", (_deposit / 10000))));
                        else lsNote.Add(string.Format("보:{0}원", r["deposit"]));
                    }
                    if (r["mMoney"].ToString() != xRow["mMoney"].ToString() && r["mMoney"].ToString() != "0")
                    {
                        _mMoney = Convert.ToUInt64(r["mMoney"]);
                        if (_mMoney > 10000) lsNote.Add(string.Format("차:{0}만원", (_mMoney / 10000)));
                        else lsNote.Add(string.Format("차:{0}원", r["mMoney"]));
                    }
                    if (r["tMoney"].ToString() != xRow["tMoney"].ToString() && r["tMoney"].ToString() != "0")
                    {
                        _tMoney = Convert.ToUInt64(r["tMoney"]);
                        if (_tMoney > 10000) lsNote.Add(string.Format("차:{0}만원", (_tMoney / 10000)));
                        else lsNote.Add(string.Format("차:{0}원", r["tMoney"]));
                    }

                    if (r["mvDt"].ToString() != xRow["mvDt"].ToString() && r["mvDt"].ToString() != "0000-00-00")
                    {
                        if (r["biz"].ToString() == "1") lsNote.Add(string.Format("사:{0}", r["mvDt"]));
                        else lsNote.Add(string.Format("전:{0}", r["mvDt"]));
                    }
                    if (r["fxDt"].ToString() != xRow["fxDt"].ToString() && r["fxDt"].ToString() != "0000-00-00") lsNote.Add(string.Format("확:{0}", r["fxDt"]));
                    //if (r["shrDt"].ToString() != xRow["shrDt"].ToString() && r["shrDt"].ToString() != "0000-00-00") lsNote.Add(string.Format("배:{0}", r["shrDt"]));
                    if (lsNote.Count > 0)
                    {
                        xRow["note"] = (xRow["note"].ToString() + "\r\n[현황서상 " + string.Join(", ", lsNote.ToArray()) + "]").Trim();
                    }
                }
            }

            //DB 업데이트
            string cvp;
            
            foreach (DataRow r in dtL.Rows)
            {
                cvp = "idx=@idx, tid=@tid, ls_no=@ls_no, prsn=@prsn, inv_type=@inv_type, part=@part, use_cd=@use_cd, term=@term, deposit=@deposit, m_money=@m_money, t_money=@t_money, biz=@biz, mv_dt=@mv_dt, fx_dt=@fx_dt, shr_dt=@shr_dt, note=@note";
                sql = "insert into ta_leas " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                sp.Add(new MySqlParameter("@idx", r["idx"]));
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@ls_no", r["lsNo"]));
                sp.Add(new MySqlParameter("@prsn", r["prsn"]));
                sp.Add(new MySqlParameter("@inv_type", r["invType"]));
                sp.Add(new MySqlParameter("@part", r["part"]));
                sp.Add(new MySqlParameter("@use_cd", r["useCd"]));
                sp.Add(new MySqlParameter("@term", r["term"]));
                sp.Add(new MySqlParameter("@deposit", r["deposit"]));
                sp.Add(new MySqlParameter("@m_money", r["mMoney"]));
                sp.Add(new MySqlParameter("@t_money", r["tMoney"]));
                sp.Add(new MySqlParameter("@biz", r["biz"]));
                sp.Add(new MySqlParameter("@mv_dt", r["mvDt"]));
                sp.Add(new MySqlParameter("@fx_dt", r["fxDt"]));
                sp.Add(new MySqlParameter("@shr_dt", r["shrDt"]));
                sp.Add(new MySqlParameter("@note", r["note"]));
                db.Open();
                db.ExeQry(sql, sp);
                sp.Clear();
                db.Close();
            }

            //임차인 기타
            sql = "select leas_note from ta_dtl where tid=" + tid;
            db.Open();
            dr = db.ExeRdr(sql);
            dr.Read();
            leasNote = (dr["leas_note"].ToString() + "\r\n" + leasNote).Trim();
            dr.Close();
            db.Close();

            sql = "update ta_dtl set leas_note=@leas_note where tid=" + tid;
            sp.Add(new MySqlParameter("@leas_note", leasNote));
            db.Open();
            db.ExeQry(sql, sp);
            sp.Clear();
            db.Close();
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
                        /*if (DecimalChar[(Level - 1) / 4] != string.Empty
                           && strValue.Substring(i, 1) == "1")
                            NumToKorea = NumToKorea + DecimalChar[(Level - 1) / 4];
                        else
                            NumToKorea = NumToKorea
                                              + NumberChar[int.Parse(strValue.Substring(i, 1))]
                                              + DecimalChar[(Level - 1) / 4];*/
                        NumToKorea = NumToKorea
                                              + NumberChar[int.Parse(strValue.Substring(i, 1))]
                                              + DecimalChar[(Level - 1) / 4];
                        UseDecimal = false;
                    }
                    else
                    {
                        /*if (strValue.Substring(i, 1) == "1")
                            NumToKorea = NumToKorea
                                               + LevelChar[(Level - 1) % 4];
                        else*/
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
        /// 사건내역
        /// </summary>
        private void Prc_Event(string dir, string ctgr)
        {
            string tid, sql, cvp, cdtn, url, jiwonNm, saNo, html, locFile, rmtFile, spt, year, sn, fileNm, tbl;
            decimal totCnt = 0, curCnt = 0, dlCnt = 0, ulCnt = 0, dnFailCnt = 0;
            string stripTag = @"[</]+(a|img).*?>";
            HAPDoc doc = new HAPDoc();

            cdtn = "sta1 IN (11,13)";   //진행+미진행
            sql = "select tid, spt, dpt, sn1, sn2, pn, bid_dt from ta_list where " + cdtn + " order by tid";
            DataTable dt = db.ExeDt(sql);

            totCnt = dt.Rows.Count;
            atomLog.AddLog(string.Format("사건내역 수집시작 대상-{0}", totCnt));
            txtState.AppendText(string.Format("\r\n>>>>> 사건내역 수집시작 대상-{0}", totCnt));    //화면에 진행상태 표시

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                tid = row["tid"].ToString();
                txtState.AppendText(string.Format("\r\n> tid -> {0} ^ {1} / {2}", tid, curCnt, totCnt));    //화면에 진행상태 표시

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
                        txtState.AppendText(" -> OK");
                    }
                    else
                    {
                        dnFailCnt++;
                        txtState.AppendText(" -> FAIL-1");
                        continue;
                    }
                }
                else
                {
                    dnFailCnt++;
                    txtState.AppendText(" -> FAIL-2");
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
                    DB_Proc(sql);
                    ulCnt++;
                }
            }
            atomLog.AddLog(string.Format("수집된 사건내역-{0}건", ulCnt));
            atomLog.AddLog("실행 완료", 1);
        }

        /// <summary>
        /// 기일내역
        /// </summary>
        private void Prc_BidDt(string dir, string ctgr)
        {
            string tid, sql, cvp, cdtn, url, jiwonNm, saNo, html, locFile, rmtFile, spt, year, sn, fileNm, tbl;
            decimal totCnt = 0, curCnt = 0, dlCnt = 0, ulCnt = 0, dnFailCnt = 0;
            string stripTag = @"[</]+(a|img).*?>";
            HAPDoc doc = new HAPDoc();

            cdtn = "sta1 IN (11,13)";   //진행+미진행
            sql = "select tid, spt, dpt, sn1, sn2, pn, bid_dt from ta_list where " + cdtn + " order by tid";
            DataTable dt = db.ExeDt(sql);

            totCnt = dt.Rows.Count;
            atomLog.AddLog(string.Format("기일내역 수집시작 대상-{0}", totCnt));
            txtState.AppendText(string.Format("\r\n>>>>> 기일내역 수집시작 대상-{0}", totCnt));    //화면에 진행상태 표시

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                tid = row["tid"].ToString();
                txtState.AppendText(string.Format("\r\n> tid -> {0} ^ {1} / {2}", tid, curCnt, totCnt));    //화면에 진행상태 표시

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
                        txtState.AppendText(" -> OK");
                    }
                    else
                    {
                        dnFailCnt++;
                        txtState.AppendText(" -> FAIL-1");
                        continue;
                    }
                }
                else
                {
                    dnFailCnt++;
                    txtState.AppendText(" -> FAIL-2");
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
                    DB_Proc(sql);
                    ulCnt++;
                }
            }
            atomLog.AddLog(string.Format("수집된 기일내역-{0}건", ulCnt));
            atomLog.AddLog("실행 완료", 1);
        }

        /// <summary>
        /// 문건/송달내역
        /// 특수 조건 검출 작업필요 -> 임금채권, 유치권 접수/배제/취하/철회
        /// </summary>
        private void Prc_Dlvry(string dir, string ctgr)
        {
            string tid, sql, cvp, cdtn, url, jiwonNm, saNo, html, locFile, rmtFile, spt, year, sn, fileNm, tbl, rcpNote = "", kw = "", matchVal = "";
            decimal totCnt = 0, curCnt = 0, dlCnt = 0, ulCnt = 0, dnFailCnt = 0;
            string stripTag = @"[</]+(a|img).*?>";
            bool chkKeyword = false;
            DateTime rcpDt, cmpDt;

            HAPDoc doc = new HAPDoc();

            cdtn = "sta1 IN (11,13)";   //진행+미진행
            sql = "select tid, spt, dpt, sn1, sn2, pn, bid_dt from ta_list where " + cdtn + " order by tid desc";
            DataTable dt = db.ExeDt(sql);

            totCnt = dt.Rows.Count;
            atomLog.AddLog(string.Format("문건/송달내역 수집시작 대상-{0}", totCnt));
            txtState.AppendText(string.Format("\r\n>>>>> 문건/송달내역 수집시작 대상-{0}", totCnt));    //화면에 진행상태 표시
            cmpDt = DateTime.Now.AddDays(-7);
            rcpDt = DateTime.Now;
            ArrayList alKw = new ArrayList();

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                chkKeyword = false;
                alKw.Clear();
                kw = string.Empty;

                tid = row["tid"].ToString();
                txtState.AppendText(string.Format("\r\n> tid -> {0} ^ {1} / {2}", tid, curCnt, totCnt));    //화면에 진행상태 표시

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
                        chkKeyword = true;
                        txtState.AppendText(" -> OK");
                    }
                    else
                    {
                        dnFailCnt++;
                        txtState.AppendText(" -> FAIL-1");
                        continue;
                    }
                }
                else
                {
                    dnFailCnt++;
                    txtState.AppendText(" -> FAIL-2");
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
                    DB_Proc(sql);
                    ulCnt++;
                }

                if (!chkKeyword) continue;

                //특수조건관련 키워드 검출
                if (html.Contains("검색결과가 없습니다")) continue;
                nc = doc.DocumentNode.SelectNodes("//table[@summary='문건처리내역 표']/tbody/tr");
                if (nc == null) continue;
                foreach (HtmlNode nd in nc)
                {
                    HtmlNodeCollection ncTd = nd.SelectNodes("./td");
                    rcpDt = Convert.ToDateTime(ncTd[0].InnerText.Trim());
                    if (rcpDt > cmpDt)
                    {
                        rcpNote = ncTd[1].InnerText.Trim();
                        match = Regex.Match(rcpNote, @"공매|권리신고|기각|납부|대위변제|말소|무상|배당배제|배당요구|연기|변경|우선매수|유익비|유치권|임금채권자|잉여|재평가|채무부존재|철회|취하|항고|항소|현황조사", rxOptM);
                        if (match.Success)
                        {
                            matchVal = match.Value;
                            if (matchVal == "말소")
                            {
                                if (Regex.IsMatch(rcpNote, @"말소[\w\s]*촉탁", rxOptS) == true) continue;
                            }
                            else if (matchVal == "연기")
                            {
                                if (Regex.IsMatch(rcpNote, @"기일[ ]*연기|경매절차[ ]*연기|기일[ ]*변경\(연기\)", rxOptS) == false) continue;
                            }
                            else if (matchVal == "변경")
                            {
                                if (Regex.IsMatch(rcpNote, @"기일[ ]*변경|채권자[\w\s\(\)O]+변경|승계인[\w\s\(\)O]+변경", rxOptS) == false) continue;
                            }
                            else if (matchVal == "유치권")
                            {
                                matchVal = (rcpNote.Contains("배제")) ? "유치권배제" : "유치권";
                            }
                            else if (matchVal == "취하")
                            {
                                if (Regex.IsMatch(rcpNote, @"채권자[\w\s\(\)O]+취하", rxOptS) == false) continue;
                            }
                            if (alKw.Contains(matchVal)) continue;
                            alKw.Add(matchVal);
                        }
                    }
                }
                if (alKw.Count > 0)
                {
                    kw = string.Join(",", alKw.ToArray());
                    DataRow[] rows = dt.Select("spt='" + row["spt"].ToString() + "' and sn1='" + row["sn1"].ToString() + "' and sn2='" + row["sn2"].ToString() + "'");
                    if (rows == null) continue;

                    db.Open();
                    foreach (DataRow r in rows)
                    {
                        sql = "select idx from db_tank.tx_rpt where dvsn='" + cmpDvsnCd + "' and tid='" + r["tid"].ToString() + "' and nt_dt='" + rcpDt.ToString() + "' limit 1";
                        bool exist = db.ExistRow(sql);
                        if (!exist)
                        {
                            if (tid != string.Empty)
                            {
                                sql = "insert into db_tank.tx_rpt set tid='" + r["tid"].ToString() + "', dvsn='" + cmpDvsnCd + "', nt_dt='" + rcpDt.ToString() + "', nt_note='" + kw + "', wdt=curdate()";
                                db.ExeQry(sql);
                            }
                        }
                    }
                    db.Close();
                }
            }
            //
            //중복병합사건 판별-미처리
            //
            atomLog.AddLog(string.Format("수집된 문건/송달내역-{0}건", ulCnt));
            atomLog.AddLog("실행 완료", 1);
        }

        /// <summary>
        /// 현황조사서
        /// </summary>
        private void Prc_StatIvst(string dir, string ctgr)
        {
            string tid, sql, cvp, cdtn, url, jiwonNm, saNo, html0, html, locFile, seq, rmtFile, spt, year, sn, fileNm, tbl;
            decimal totCnt = 0, curCnt = 0, dlCnt = 0, ulCnt = 0, dnFailCnt = 0;
            string stripTag = @"[</]+(a|img).*?>";
            HAPDoc doc = new HAPDoc();
            Dictionary<string, string> dicHtml = new Dictionary<string, string>();

            cdtn = "sta1=11 and (cat1 IN (10,20) or cat2=3012)";  //현황조사서는 토지, 건물, 선박만 제공
            sql = "select tid, spt, dpt, sn1, sn2, pn, bid_dt from ta_list where " + cdtn + " group by spt, sn1, sn2 order by tid";
            DataTable dt = db.ExeDt(sql);

            totCnt = dt.Rows.Count;
            atomLog.AddLog(string.Format("현황조사서 수집시작 대상-{0}", totCnt));
            txtState.AppendText(string.Format("\r\n>>>>> 현황조사서 수집시작 대상-{0}", totCnt));    //화면에 진행상태 표시

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                dicHtml.Clear();
                tid = row["tid"].ToString();
                txtState.AppendText(string.Format("\r\n> tid -> {0} ^ {1} / {2}", tid, curCnt, totCnt));    //화면에 진행상태 표시

                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                url = "http://www.courtauction.go.kr/RetrieveRealEstSaHjosa.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo;
                html0 = net.GetHtml(url);
                if (html0.Contains("잘못된 접근입니다") || html0.Contains("현황조사서가 없습니다"))
                {
                    dnFailCnt++;
                    txtState.AppendText(" -> FAIL-0");
                    continue;
                }

                if (html0.Contains("중복병합사건</th>")) continue;

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
                            txtState.AppendText(" -> FAIL-0");
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
                            txtState.AppendText(" -> OK");
                        }
                        else
                        {
                            dnFailCnt++;
                            txtState.AppendText(" -> FAIL-1");
                            continue;
                        }
                    }
                    else
                    {
                        dnFailCnt++;
                        txtState.AppendText(" -> FAIL-2");
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
                        DB_Proc(sql);
                        ulCnt++;
                    }
                }
                //
                //중복병합사건 판별-미처리
                //                
            }
            atomLog.AddLog(string.Format("수집된 현황조사서-{0}건", ulCnt));
        }

        /// <summary>
        /// 부동산표시목록
        /// </summary>
        private void Prc_ReList(string dir, string ctgr)
        {
            string tid, sql, cvp, cdtn, url, jiwonNm, saNo, html0, html, locFile, seq, rmtFile, spt, year, sn, fileNm, tbl;
            decimal totCnt = 0, curCnt = 0, dlCnt = 0, ulCnt = 0, dnFailCnt = 0;
            string stripTag = @"[</]+(a|img).*?>";
            HAPDoc doc = new HAPDoc();
            Dictionary<string, string> dicHtml = new Dictionary<string, string>();

            cdtn = "sta1=11 and (cat1 IN (10,20) or cat2=3012)";  //현황조사서는 토지, 건물, 선박만 제공
            sql = "select tid, spt, dpt, sn1, sn2, pn, bid_dt from ta_list where " + cdtn + " group by spt, sn1, sn2 order by tid";
            DataTable dt = db.ExeDt(sql);

            totCnt = dt.Rows.Count;
            atomLog.AddLog(string.Format("부동산표시목록 수집시작 대상-{0}", totCnt));
            txtState.AppendText(string.Format("\r\n>>>>> 부동산표시목록 수집시작 대상-{0}", totCnt));    //화면에 진행상태 표시

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                dicHtml.Clear();
                tid = row["tid"].ToString();
                txtState.AppendText(string.Format("\r\n> tid -> {0} ^ {1} / {2}", tid, curCnt, totCnt));    //화면에 진행상태 표시

                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row["spt"]));
                saNo = string.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                url = "http://www.courtauction.go.kr/RetrieveRealEstHjosaDispMokrok.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo;
                html0 = net.GetHtml(url);
                if (html0.Contains("잘못된 접근입니다") || html0.Contains("현황조사서가 없습니다"))
                {
                    dnFailCnt++;
                    txtState.AppendText(" -> FAIL-0");
                    continue;
                }

                if (html0.Contains("중복병합사건</th>")) continue;

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
                            txtState.AppendText(" -> FAIL-0");
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
                            txtState.AppendText(" -> OK");
                        }
                        else
                        {
                            dnFailCnt++;
                            txtState.AppendText(" -> FAIL-1");
                            continue;
                        }
                    }
                    else
                    {
                        dnFailCnt++;
                        txtState.AppendText(" -> FAIL-2");
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
                        DB_Proc(sql);
                        ulCnt++;
                    }
                }
                //
                //중복병합사건 판별-미처리
                //
            }
            atomLog.AddLog(string.Format("수집된 부동산 표시목록-{0}건", ulCnt));
            atomLog.AddLog("실행 완료", 1);
        }

        /// <summary>
        /// 매각 공고-미사용
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="v"></param>
        private void _Prc_DpslNt(string dir, string ctgr)
        {
            string curMnth, nxtMnth, jiwonNm, date, url, html, locFile, rmtFile, sql, cvp, spt, dpt, bidDt, year, fileNm, tbl, curDtHour;
            decimal totCnt = 0, curCnt = 0, dlCnt = 0, ulCnt = 0, dnFailCnt = 0;
            string stripTag = @"[</]+(a|img).*?>";

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

            string testArea = "수원";
            txtState.AppendText(string.Format("\r\n>>>>> 매각공고 수집시작"));    //화면에 진행상태 표시

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
                        Match m = Regex.Match(clickStr, @"showDetail\('[\w.]+',\s+'(\w+)',\s+'(\d{8})',\s+'[\w]*',\s+'[\w]*',\s+'(\d{4})',\s+'(경매[\d-]+계)'", rxOptM);     //1-법원명, 2-입찰일, 3-계코드, 4-담당계                        
                        dtCal.Rows.Add(row["csCd"].ToString(), m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value);
                    }
                }
            }

            curDtHour = string.Format("{0:yyyyMMddHH}", DateTime.Now);
            foreach (DataRow row in dtCal.Rows)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                txtState.AppendText(string.Format("\r\n> {0} / {1} / {2}", row["lawNm"], row["bidDt"], row["dptNm"]));    //화면에 진행상태 표시

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
                        txtState.AppendText(" -> OK");
                    }
                    else
                    {
                        dnFailCnt++;
                        txtState.AppendText(" -> FAIL-1");
                        continue;
                    }
                }
                else
                {
                    dnFailCnt++;
                    txtState.AppendText(" -> FAIL-2");
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
                    DB_Proc(sql);
                }
            }
        }

        /// <summary>
        /// 물건 상세
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="v"></param>
        private void Prc_PdDtl(string dir, string ctgr)
        {
            string tid, sql, cvp, cdtn, url, jiwonNm, saNo, pn, html, locFile, rmtFile, spt, year, sn, fileNm, tbl;
            decimal totCnt = 0, curCnt = 0, dlCnt = 0, ulCnt = 0, dnFailCnt = 0;
            string stripTag = @"[</]+(a|img).*?>";
            HAPDoc doc = new HAPDoc();

            cdtn = "sta1=11";
            sql = "select tid, spt, dpt, sn1, sn2, pn, bid_dt from ta_list where " + cdtn + " order by tid";
            DataTable dt = db.ExeDt(sql);

            totCnt = dt.Rows.Count;
            atomLog.AddLog(string.Format("물건상세 수집시작 대상-{0}", totCnt));
            txtState.AppendText(string.Format("\r\n>>>>> 물건상세 수집시작 대상-{0}", totCnt));    //화면에 진행상태 표시

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                tid = row["tid"].ToString();
                txtState.AppendText(string.Format("\r\n> tid -> {0} ^ {1} / {2}", tid, curCnt, totCnt));    //화면에 진행상태 표시

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
                        txtState.AppendText(" -> OK");
                    }
                    else
                    {
                        dnFailCnt++;
                        txtState.AppendText(" -> FAIL-1");
                        continue;
                    }
                }
                else
                {
                    dnFailCnt++;
                    txtState.AppendText(" -> FAIL-2");
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

                    sql = $"delete from {tbl} where ctgr='{ctgr}' and tid='{tid}'";     //[물건상세]는 무조건 단일 파일만 기록한다.(물번 합침 등으로 인한 2개 이상 파일이 있을 수 있음)
                    DB_Proc(sql);

                    cvp = "ctgr='" + ctgr + "', spt='" + spt + "', tid='" + tid + "', sn='" + sn + "', file='" + fileNm + "', wdt=curdate()";
                    sql = "insert into " + tbl + " set " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                    DB_Proc(sql);
                    ulCnt++;
                }
            }
            atomLog.AddLog(string.Format("수집된 물건상세-{0}건", ulCnt));
            atomLog.AddLog("실행 완료", 1);
        }

        /// <summary>
        /// DB 처리
        /// </summary>
        /// <param name="sql"></param>
        private void DB_Proc(string sql)
        {
            db.Open();
            db.ExeQry(sql);
            db.Close();
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
