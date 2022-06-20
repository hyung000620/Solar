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
    public partial class fPrptLs : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AtomLog atomLog = new AtomLog(201);
        string cnvTool = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\pdftohtml.exe";

        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 1500, webCnt = 0;

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;

        string filePath;    //로컬 파일저장 경로
        string vmNm = Environment.MachineName;

        public fPrptLs()
        {
            InitializeComponent();
            this.Shown += FPrptLs_Shown;
        }

        private void FPrptLs_Shown(object sender, EventArgs e)
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
            filePath = @"C:\Atom\PA\" + DateTime.Today.ToShortDateString() + @"\재산명세";
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }

            if (!File.Exists(cnvTool))
            {
                File.WriteAllBytes(cnvTool, Properties.Resources.pdftohtml);
            }
        }

        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            string sql, url, jsonData, newFileNm, html, dirNo, rmtFile, cvp, htmlFile;
            string cltrNo, cltrHstrNo, plnmNo, pbctNo, pbctCdtnNo, bidMnmtNo;
            string pbctYr, pbctSeq, rgstDeptNo, cltrMnmtNo, pbctDgr;
            string bgnDtm, endDtm, today, cmpDt;
            bool flag = false;

            decimal totCnt = 0, curCnt = 0, dlCnt = 0, ulCnt = 0, dnFailCnt = 0;

            //Atom.exe 실행파일과 같은 경로에 chromedriver.exe, WebDriver.dll, WebDriver.Support.dll -> 3개의 파일을 복사 해 놓는다.
            //(없을 경우 실행시 "경로의 형식이 잘못되었습니다" 오류 발생, Debug 일때는 오류 없음)

            ChromeDriverService drvSvc = ChromeDriverService.CreateDefaultService(@"C:\Atom", "chromedriver.exe");
            drvSvc.HideCommandPromptWindow = true;
            ChromeOptions chOpt = new ChromeOptions();
            //chOpt.AddArgument("--headless");
            chOpt.AddArgument("--window-size=1000,1000");
            chOpt.AddArgument("--disable-gpu");
            chOpt.AddArgument("--no-sandbox");
            chOpt.AddArgument("--disable-dev-shm-usage");

            bgnDtm = DateTime.Now.AddDays(5).ToString("yyyy-MM-dd HH:mm:ss");
            endDtm = DateTime.Now.AddDays(7).AddHours(6).ToString("yyyy-MM-dd HH:mm:ss");
            today = DateTime.Today.ToShortDateString();
            cmpDt = DateTime.Today.AddDays(-30).ToShortDateString();

            atomLog.AddLog(string.Format("대상 기간 {0}~{1}", bgnDtm, endDtm));
            txtState.AppendText(string.Format("\r\n\r\n■■■■■     [대상 기간] {0} ~ {1}    ■■■■■", bgnDtm, endDtm));    //화면에 진행상태 표시

            sql = "select cltr_no, hstr_no, plnm_no, pbct_no, cdtn_no, bmgmt_no, cmgmt_no from tb_list where " +
                "stat_nm IN ('입찰준비중','인터넷입찰진행중','인터넷입찰마감','입찰공고중','수의계약가능') and " +
                "cls_dtm >= NOW() and prpt_dvsn = '압류재산(캠코)' and " +
                //"bgn_dtm <= (NOW()+INTERVAL (24*7+5) HOUR) order by cltr_no";
                "bgn_dtm between '" + bgnDtm + "' and '" + endDtm + "' order by cltr_no";
            
            //TEST
            //sql = "select cltr_no, hstr_no, plnm_no, pbct_no, cdtn_no, bmgmt_no, cmgmt_no from tb_list where " + "cltr_no=1457106";
            DataTable dt = db.ExeDt(sql);
            
            totCnt = dt.Rows.Count;
            atomLog.AddLog(string.Format("확인 대상 {0}", totCnt));
            txtState.AppendText(string.Format("\r\n[확인 대상] {0}", totCnt));    //화면에 진행상태 표시

            webCnt = 1;
            ChromeDriver drv = null;
            try
            {
                drv = new ChromeDriver(drvSvc, chOpt);                
            }
            catch
            {
                atomLog.AddLog("ChromeDriver 에러");
            }
            foreach (DataRow row in dt.Rows)
            {
                curCnt++;                
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                txtState.AppendText(string.Format("\r\n[체크/다운로드] {0} / {1} / {2}", dlCnt, curCnt, totCnt));    //화면에 진행상태 표시

                cltrNo = row["cltr_no"].ToString();
                cltrHstrNo = row["hstr_no"].ToString();
                plnmNo = row["plnm_no"].ToString();
                pbctNo = row["pbct_no"].ToString();
                pbctCdtnNo = row["cdtn_no"].ToString();
                bidMnmtNo = row["bmgmt_no"].ToString();

                flag = false;
                db.Open();
                sql = "select prpt_ls from tb_file where cltr_no='" + cltrNo + "' limit 1";
                MySqlDataReader dr = db.ExeRdr(sql);
                dr.Read();
                if (dr.HasRows)
                {
                    if (dr["prpt_ls"].ToString() == "") flag = true;
                    else
                    {
                        JObject jObj = JObject.Parse(dr["prpt_ls"].ToString());
                        if (Convert.ToDateTime(jObj["rgstDt"].ToString()) < Convert.ToDateTime(cmpDt))  //수집일에서 30일 지나면 갱신
                        {
                            flag = true;
                        }
                    }                    
                }
                else
                {
                    flag = true;
                }
                dr.Close();
                db.Close();

                //flag = true;    //Test
                if (!flag)
                {
                    txtState.AppendText("-----> PASS 30일 미만");
                    continue;
                }

                webCnt++;
                url = "http://www.onbid.co.kr/op/cta/cltrdtl/collateralRealEstateTabDetail.do?cltrHstrNo=" + cltrHstrNo + "&cltrNo=" + cltrNo + "&plnmNo=" + plnmNo + "&pbctNo=" + pbctNo + "&pbctCdtnNo=" + pbctCdtnNo + "&bidMnmtNo=" + bidMnmtNo + "&selectValue=&dtButtonTab=002";
                jsonData = net.GetHtml(url, Encoding.UTF8);
                if (jsonData.Contains("요청하신 페이지를 찾을 수 없거나") || jsonData.Contains("작업 시간이 초과")) continue;
                dynamic x = JsonConvert.DeserializeObject(jsonData);
                var jsPbct = x["resultPbctlList"];
                if (jsPbct == null) continue;

                JArray jaPbct = JArray.Parse(jsPbct.ToString());
                foreach (JObject item in jaPbct)
                {
                    if (item["prptYn"]?.ToString() != "Y" || item["rgstDtm"]?.ToString() == "B") continue;

                    pbctYr = item["pbctYr"]?.ToString() ?? "";
                    pbctSeq = item["pbctSeq"]?.ToString() ?? "";
                    rgstDeptNo = item["rgstDeptNo"]?.ToString() ?? "";
                    cltrMnmtNo = item["cltrMnmtNo"]?.ToString() ?? "";
                    pbctDgr = item["pbctDgr"]?.ToString() ?? "";
                    url = "http://www.onbid.co.kr/op/cmm/rd/reportViewerPopup.do?mrdPath=/op/PbctPrptDtlPortal_new.mrd&mrdParam=/rp%20[" + pbctYr + "]%20[" + pbctSeq + "]%20[" + rgstDeptNo + "]%20[" + cltrMnmtNo + "][" + pbctDgr + "]&mrdCertYn=Y";
                    
                    drv.Navigate().GoToUrl(url);
                    By by = By.XPath(@"//*[@id='crownix-toolbar-save']/button");
                    if (WaitVisible(drv, by))
                    {
                        try
                        {
                            IWebElement element = drv.FindElement(by);
                            element.Click();    //[문서를 실행 할 수 없습니다]-오류 발생하는 경우 있음(팝업창)
                            //drv.FindElementByXPath(@"//*[@id='crownix-toolbar-pdf']/button").Click();     //2021-11-17 오류발생 FindElementByXPath 사용할 수 없음.
                            drv.FindElement(By.XPath(@"//*[@id='crownix-toolbar-pdf']/button")).Click();
                        }
                        catch {
                            dnFailCnt++;
                            txtState.AppendText("----------> Error CD-1");
                        }
                        Thread.Sleep(5000);
                        drv.Navigate().GoToUrl("about:blank");
                        dlCnt++;
                    }
                    else
                    {
                        dnFailCnt++;
                        txtState.AppendText("----------> Error CD-2");
                    }
                    break;
                }
                jaPbct.Clear();
            }
            drv.Quit();
            atomLog.AddLog(string.Format("다운로드 성공/실패-{0}/{1}", dlCnt, dnFailCnt));
            
            string dnPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads";
            DirectoryInfo directory = new DirectoryInfo(dnPath);            
            Regex rx = new Regex(@"\d{4}\-\d{5}\-\d{3}", rxOptM);
            FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "PA/", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);
            List<MySqlParameter> sp = new List<MySqlParameter>();
            List<string> fileList = directory.GetFiles("*.pdf").Where(f => f.CreationTime.Date == DateTime.Today.Date).Select(f => f.FullName).ToList();

            curCnt = 0;
            totCnt = fileList.Count;
            atomLog.AddLog(string.Format("HTML 변환 {0}", totCnt));
            foreach (string fileNm in fileList)
            {
                curCnt++;
                txtState.AppendText(string.Format("\r\n[변환/업로드] {0} / {1}", curCnt, totCnt));    //화면에 진행상태 표시

                Process proc = new Process();
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = cnvTool;
                psi.Arguments = @"-c -i -noframes -zoom 1 -enc UTF-8 """ + fileNm + "\"";
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
                    //throw new Exception("pdf -> html 변환 실패");
                    proc.WaitForExit();
                    proc.Close();
                    continue;
                }
                proc.WaitForExit();
                proc.Close();

                htmlFile = fileNm.Replace("pdf", "html");
                Stream stream = File.OpenRead(htmlFile);
                StreamReader sr = new StreamReader(stream, Encoding.UTF8);
                html = sr.ReadToEnd();
                sr.Close();
                sr.Dispose();
                stream.Close();
                stream.Dispose();

                Match match = rx.Match(html);
                if (match.Success == false) continue;
                
                var xRow = dt.Rows.Cast<DataRow>().Where(t => t["cmgmt_no"].ToString() == match.Value).FirstOrDefault();
                if (xRow == null) continue;

                cltrNo = xRow["cltr_no"].ToString();
                newFileNm = string.Format(@"{0}\F{1}.pdf", dnPath, cltrNo);
                if (!File.Exists(newFileNm))
                {
                    File.Move(fileNm, newFileNm);
                }
                if (File.Exists(htmlFile))
                {
                    File.Delete(htmlFile);
                }
                
                dirNo = (Math.Ceiling(Convert.ToDecimal(cltrNo) / 100000) * 100000).ToString().PadLeft(7, '0');
                rmtFile = string.Format("F/{0}/F{1}.pdf", dirNo, cltrNo);
                var obj = new JObject();
                obj.Add("fullNm", rmtFile);
                obj.Add("rgstDt", today);                
                
                if (ftp1.Upload(newFileNm, rmtFile))
                {
                    cvp = "prpt_ls=@prpt_ls";
                    sql = "insert into tb_file set cltr_no=@cltr_no, " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                    sp.Add(new MySqlParameter("@cltr_no", cltrNo));
                    sp.Add(new MySqlParameter("@prpt_ls", obj.ToString()));
                    db.Open();
                    db.ExeQry(sql, sp);
                    sp.Clear();
                    db.Close();

                    File.Copy(newFileNm, string.Format(@"{0}\F{1}.pdf", filePath, cltrNo), true);
                    File.Delete(newFileNm);

                    ulCnt++;
                }                
            }
            atomLog.AddLog(string.Format("파일업로드 {0}", ulCnt), 1);
        }

        private static bool WaitVisible(IWebDriver drv, By by)
        {
            WebDriverWait wait = new WebDriverWait(drv, TimeSpan.FromSeconds(1000));
            try
            {
                IWebElement element = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(by));
                //IWebElement element = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementExists(by));
            }
            catch
            {
                //MessageBox.Show(ex.Message);
                return false;
            }

            return true;
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            bgwork.Dispose();

            this.Dispose();
            this.Close();
        }
    }
}
