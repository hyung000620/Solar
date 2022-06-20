using Solar;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.IE;
using OpenQA.Selenium.Support.UI;
using System.Threading;
using AutoItX3Lib;
using SeleniumExtras.WaitHelpers;
using Timer = System.Windows.Forms.Timer;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices;

namespace Atom.Comn
{
    public partial class fRgstAuto : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        NetUtil net = new NetUtil();
        AuctCd auctCd = new AuctCd();
        AuctSms auctSms = new AuctSms();
        RgstPinTid rgstPinTid = new RgstPinTid();
        SpCdtnChk spCdtnChk = new SpCdtnChk();

        AutoItX3 at = new AutoItX3();

        //AtomLog atomLog = new AtomLog(0);     //로그 생성

        BackgroundWorker bgwork;
        ChromeDriverService drvSvc;
        ChromeDriver drv = null;

        InternetExplorerDriverService idrvSvc;
        IWebDriver idrv = null;

        string myWeb = Properties.Settings.Default.myWeb;
        string machNm = Environment.MachineName;
        FTPclient ftpCA = new FTPclient(Properties.Settings.Default.myFTP + "CA/", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);
        FTPclient ftpPA = new FTPclient(Properties.Settings.Default.myFTP + "PA/", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);
        RgstAnalyNew rgstAnalyCA = new RgstAnalyNew();
        RgstAnalyPa rgstAnalyPA = new RgstAnalyPa();

        ProcessStartInfo psi = new ProcessStartInfo();

        //인터넷 등기소 계정/캐쉬/테스트 인쇄/파일저장 경로
        private string irosId = "";
        private string irosPwd = "";
        private const string irosEMoneyNo1 = "X8497440";
        private const string irosEMoneyNo2 = "5621";
        private const string irosEMoneyPwd = "jins3816";
        private bool cashBalance = true;
        private bool printTest = false;
        private int irosLoginCnt = 0;
        private string pdfSavePath = $@"C:\등기발급\{DateTime.Now.ToShortDateString()}";
        private string RbotTarget = "";
        private int workEndHour = 18;   //오후 6시 이후 작업 종료
        //

        public IJavaScriptExecutor js;
        bool cmortOverPage = false;     //[공동담보/전세목록] 첫번째 체크박스가 100매 이상인 경우 고유번호 찾기에서 검색옵션 선택/해제
        bool tradeOverPage = false;     //[매매목록] 첫번째 체크박스가 100매 이상인 경우 고유번호 찾기에서 검색옵션 선택/해제

        //발급 상태 현황
        int stateCntAll = 0, stateCntSuc = 0, stateCntWait = 0, stateCntFail = 0;

        //AutoIt Image Search
        [DllImport("ImageSearchDLL.dll")]
        private static extern IntPtr ImageSearch(int x, int y, int right, int bottom, [MarshalAs(UnmanagedType.LPStr)] string imagePath);

        public fRgstAuto()
        {
            InitializeComponent();
            this.Shown += FRgstAuto_Shown;
        }

        private void FRgstAuto_Shown(object sender, EventArgs e)
        {
            lblMachNm.Text = machNm;

            CheckForIllegalCrossThreadCalls = false;
            bgwork = new BackgroundWorker();
            bgwork.DoWork += Bgwork_DoWork;
            bgwork.RunWorkerCompleted += Bgwork_RunWorkerCompleted;
            bgwork.WorkerReportsProgress = false;
            bgwork.WorkerSupportsCancellation = true;

            init();

            //UploadDBPrc($@"{pdfSavePath}\15472000000381.pdf");
            //tmpUpload();
            //return;

            bgwork.RunWorkerAsync();
        }

        private void tmpUpload()
        {
            string[] files = Directory.GetFiles(pdfSavePath, "*.pdf");
            foreach (string file in files)
            {
                //MessageBox.Show(file);
                UploadDBPrc(file);
            }
            MessageBox.Show("ok");
        }

        private void init()
        {
            if (machNm == "RBOT-1")
            {
                //idx-홀수 -> 진근
                irosId = "gosegero";
                irosPwd = "tank1544~!";
                RbotTarget = "(idx % 2)=1";
            }
            else if (machNm == "RBOT-2")
            {
                //idx-짝수 -> 제근
                irosId = "shinpo21";
                irosPwd = "7726jkk!@";
                RbotTarget = "(idx % 2)=0";
            }
            else
            {
                //전부
                irosId = "gosegero";
                irosPwd = "tank1544~!";
                RbotTarget = "1";
            }

            //AutoIt 옵션 > WinTitle 에서 포함하는 문자열로 셋팅
            at.AutoItSetOption("WinTitleMatchMode", 2);

            //열람 pdf 파일 저장폴더 생성
            if (!Directory.Exists(pdfSavePath))
            {
                Directory.CreateDirectory(pdfSavePath);
            }

            //실행창 우측하단에 위치
            Rectangle wa = Screen.GetWorkingArea(this);
            this.Location = new Point(wa.Right - Size.Width, wa.Bottom - Size.Height);

            //탐색기(저장위치)
            string explr = $"{DateTime.Now.ToShortDateString()}";
            if (at.WinExists(explr) == 1)
            {
                at.WinActivate(explr);
            }
            else
            {
                Process.Start("explorer.exe", $"{pdfSavePath}");
                at.WinWait(explr);
                at.WinMove(explr, "", (wa.Right - Size.Width), (wa.Top), 800, (wa.Bottom - 600));
            }
        }

        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                irosLoginCnt = 0;
                cashBalance = true;
                txtPrgs.Text = string.Empty;

                IssueStateUpdt();   //전체 발급현황 업데이트

                RgstAuto();

                txtPrgs.Text = "[발급 대기중]";

                if (stateCntWait == 0)
                {
                    Thread.Sleep(1 * 60 * 1000);       //대기건수가 없을 경우 1분 휴식
                }

                if (DateTime.Now.Hour >= workEndHour)
                {
                    break;
                }
            }
        }

        private void RgstAuto()
        {
            int i = 0, rowCnt = 0;
            string sql, file;
            string currentWindowHandle = "";
            
            Size scrSize = Screen.PrimaryScreen.Bounds.Size;

            string today = DateTime.Now.ToShortDateString();
            string cDtm = $"{DateTime.Now.AddHours(-3):yyyy-MM-dd HH:mm:ss}";

            //오늘 등록건 + 이전 조사대기 사건(마지막 체크후 3시간 경과)
            sql = "select idx, dvsn, tid, pin from db_tank.tx_rgst_auto where dvsn > 0 and pay=0 and ul=0 and rdtm='0000-00-00 00:00:00' and (" +
                "(err_cd < 20 and wdt=curdate() and wtm < curtime()) or " +
                $"(err_cd=20 and wdt > date_sub(curdate(),INTERVAL 10 day) and wdt < curdate() and cdtm < '{cDtm}')" +
                $") and {RbotTarget} order by dvsn desc, idx desc";
            DataTable dt = db.ExeDt(sql);
            rowCnt = dt.Rows.Count;
            
            if (rowCnt == 0)
            {
                goto PDF_EXPORT;
            }
            
            //크롬드라이버 준비 및 로그인
            drvSvc = ChromeDriverService.CreateDefaultService(@"C:\Atom", "chromedriver.exe");
            drvSvc.HideCommandPromptWindow = true;
            ChromeOptions chOpt = new ChromeOptions();
            //chOpt.AddArgument("--headless");
            chOpt.AddArgument($"--window-size=1000,{scrSize.Height}");
            chOpt.AddArgument("--disable-gpu");
            chOpt.AddArgument("--no-sandbox");
            chOpt.AddArgument("--disable-dev-shm-usage");

            /*
            chOpt.AddArgument("--allow-insecure-localhost");
            //chOpt.AddArgument("--allow-insecure-origin=http://www.iros.go.kr");
            chOpt.AddArgument("--disable-web-security");            
            chOpt.AddArgument("--allow-file-access-from-files");
            chOpt.AddArgument("--allow-running-insecure-content");
            chOpt.AddArgument("--allow-cross-origin-auth-prompt");
            chOpt.AddArgument("--allow-file-access");
            chOpt.AddArgument("--ignore-certificate-errors");
            chOpt.AddArgument("--unsafely-treat-insecure-origin-as-secure=http://www.iros.go.kr");
            */
            
            drv = new ChromeDriver(drvSvc, chOpt);
            this.js = (IJavaScriptExecutor)this.drv;
            drv.Navigate().GoToUrl("http://www.iros.go.kr");
            currentWindowHandle = drv.CurrentWindowHandle;   //메인 윈도우            
            Thread.Sleep(20000);    //보안 프로그램이 로딩될 때 까지 충분히 여유 시간을 준다.
            
            try
            {
                RgstLogin(drv);     //로그인
            }
            catch (Exception ex)
            {
                if (drv != null)
                { 
                    drv.Close();
                    drv.Quit();
                    RgstAuto();
                }
            }

            //팝업창 닫기
            if (drv.WindowHandles.Count > 0)
            {
                foreach (string winNm in drv.WindowHandles)
                {
                    drv.SwitchTo().Window(winNm);
                    if (drv.Url.Contains("popupid"))
                    {
                        drv.Close();
                    }
                }
                drv.SwitchTo().Window(currentWindowHandle);
            }

            //Step-1 > 장바구니 담기 및 결제-Chrome 사용            
            foreach (DataRow row in dt.Rows)
            {
                i++;
                txtPrgs.AppendText($"\r\n[장바구니/결제] - {i}/{rowCnt}");

                RgstCartPay(row, i, rowCnt);

                IssueStateUpdt();   //전체 발급현황 업데이트

                //전자민원캐시의 잔액이 부족한 경우-문자발송(진근, 현진) 후 발급처리로 바로 이동
                if (cashBalance == false)
                {
                    auctSms.SendSms("f22", "한진근", "01049242195", "전자민원캐시 잔액 부족");
                    auctSms.SendSms("tankson", "손현진", "01091212879", "전자민원캐시 잔액 부족");
                    break;
                }
            }
            drv.Quit();

        PDF_EXPORT:

            //Step-2 > 미발급 내역이 있는 경우 PDF 파일로 저장(인쇄)-InternetExplorer 사용            
            sql = "select idx, dvsn, tid, pin from db_tank.tx_rgst_auto where dvsn > 0 and ul=0 and rdtm='0000-00-00 00:00:00' and (" +
                "(err_cd < 20 and wdt=curdate() and wtm < curtime()) or " +
                "(wdt > date_sub(curdate(),INTERVAL 10 day) and wdt < curdate() and err_cd=20 and pay=1)" +
                $") and {RbotTarget}";
            dt = db.ExeDt(sql);
            if (dt.Rows.Count > 0)
            {
                RgstIssue();
            }

            //Step-3 > 발급 후 서버 업로드 실패(발급 후 10분 지난 건)-가끔 발생
            sql = $"select * from db_tank.tx_rgst_auto where idtm like '{today}%' and ul=0 and timestampdiff(minute,idtm,now()) > 10 and {RbotTarget}";
            dt = db.ExeDt(sql);
            if (dt.Rows.Count > 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    file = $@"{pdfSavePath}\{row["pin"]}.pdf";
                    if (File.Exists(file))
                    {
                        UploadDBPrc(file);
                    }
                }
            }

            //Step-4 > 열람클릭 후 발급실패-재열람에서 발급시도(클릭 후 10분 지난 건)-미구현
            sql = $"select idx, dvsn, tid, pin from db_tank.tx_rgst_auto where dvsn > 0 and rdtm like '{today}%' and idtm='0000-00-00 00:00:00' and timestampdiff(minute,rdtm,now()) > 10 and {RbotTarget}";
            dt = db.ExeDt(sql);
            if (dt.Rows.Count > 0)
            {
                //IssueFailRetry(dt);
            }
        }

        /// <summary>
        /// 인터넷등기소 로그인
        /// </summary>
        /// <param name="drv"></param>
        private void RgstLogin(ChromeDriver drv)
        {
            irosLoginCnt++;

            txtPrgs.AppendText("[로그인]");

            if (irosLoginCnt > 5)
            {
                //MessageBox.Show("로그인 실패");
            }

            if (WaitVisible(drv, By.XPath("//*[@id='id_user_id']")))
            {
                this.js.ExecuteScript("javascript:$('#id_user_id').val('" + irosId + "');", Array.Empty<object>());
                this.js.ExecuteScript("javascript:$('#password').val('" + irosPwd + "');", Array.Empty<object>());
                //drv.FindElement(By.XPath(@"//*[@id='leftS']/div[2]/form/div[1]/ul/li[4]/a/img")).Click();
                this.js.ExecuteScript("f_gosubmit();return false;", Array.Empty<object>());
                Thread.Sleep(5000);
                if (!drv.PageSource.Contains("로그아웃"))
                {
                    RgstLogin(drv);
                }
            }
            else
            {
                RgstLogin(drv);
            }
        }

        /// <summary>
        /// 장바구니 담기 및 결제
        /// </summary>
        /// <param name="row"></param>
        private void RgstCartPay(DataRow row, int rowNo, int rowCnt)
        {
            string tid, pin, idx, html, msg, sql;
            bool prcRslt = true, cmortOver = false, tradeOver = false;
            int lsNo = 0, errCd = 0;

            idx = row["idx"].ToString();
            tid = row["tid"].ToString();
            pin = row["pin"].ToString();            

            drv.Navigate().GoToUrl("http://www.iros.go.kr/iris/index.jsp?isu_view=view");
            string currentWindowHandle = drv.CurrentWindowHandle;   //메인 윈도우

            try
            {
                drv.SwitchTo().Frame("inputFrame");
                this.js.ExecuteScript("f_goPin_click();return false;", Array.Empty<object>());
            }
            catch (Exception ex)
            {
                prcRslt = false;
                msg = "탭이동 오류-" + ex.Message;
                RgstAutoErr(msg, idx, 10);

                cmortOverPage = false;
                tradeOverPage = false;
            }
            finally
            {
                drv.SwitchTo().Window(currentWindowHandle);
            }
            if (!prcRslt) return;

            Thread.Sleep(1000);
            try
            {
                drv.SwitchTo().Frame("resultFrame");
                drv.SwitchTo().Frame("frmOuterModal");
                this.js.ExecuteScript("javascript:$('#inpPinNo').val('" + pin + "');", Array.Empty<object>());
                if (!cmortOverPage)
                {
                    drv.FindElement(By.Id("y202cmort_check")).Click();
                    Thread.Sleep(700);
                }
                if (!tradeOverPage)
                {
                    drv.FindElement(By.Id("y202trade_check")).Click();
                    Thread.Sleep(700);
                }                
                this.js.ExecuteScript("return f_search(this.form, 1, 0, 0);", Array.Empty<object>());                
            }
            catch (Exception ex)
            {
                prcRslt = false;
                msg = "고유번호 입력 오류-" + ex.Message;
                RgstAutoErr(msg, idx, 11);                
            }
            finally
            {
                drv.SwitchTo().Window(currentWindowHandle);
                cmortOverPage = false;
                tradeOverPage = false;
            }
            if (!prcRslt) return;

            Thread.Sleep(2000);

            try
            {
                if (drv.SwitchTo().Alert().Text.Contains("고유번호에 해당하는"))     //고유번호에 해당하는 소재지번을 확인할 수 없습니다.
                {
                    prcRslt = false;
                    drv.SwitchTo().Alert().Accept();
                    msg = "고유번호 오류";
                    RgstAutoErr(msg, idx, 22);
                }
                drv.SwitchTo().Alert().Accept();
            }
            catch { }
            if (!prcRslt) return;

            try
            {
                drv.SwitchTo().Frame("resultFrame");
                drv.SwitchTo().Frame("frmOuterModal");
                drv.FindElement(By.CssSelector("td.noline_rt-tx_ct > button")).Click();
            }
            catch (Exception ex)
            {
                prcRslt = false;
                msg = "고유번호 선택 오류-" + ex.Message;
                RgstAutoErr(msg, idx, 23);                
            }
            finally
            {
                drv.SwitchTo().Window(currentWindowHandle);
            }
            if (!prcRslt) return;

            Thread.Sleep(1000);

            try
            {
                drv.SwitchTo().Alert().Accept();    //소유 관계의 정확한 파악은 지상의 전유부분(토지 위의 집합건물)을 추가로 발급/열람하여 확인할 수 있습니다.
            }
            catch { }

            try
            {
                drv.SwitchTo().Frame("resultFrame");
                drv.SwitchTo().Frame("frmOuterModal");
                html = drv.PageSource;
                if (html.Contains("과다등기부"))
                {
                    if (drv.FindElement(By.CssSelector("button.btn_bg02_action")).Displayed)
                    {
                        //[다음] 버튼이 있으면 발급 진행
                        drv.FindElement(By.CssSelector("button.btn_bg02_action")).Click();
                    }
                    else
                    {
                        errCd = 30;
                        throw new Exception("과다등기");
                    }
                }
            }
            catch (Exception ex)
            {
                prcRslt = false;
                if (errCd == 0) errCd = 39;
                RgstAutoErr(ex.Message, idx, errCd);
                try
                {
                    drv.SwitchTo().Alert().Accept();
                }
                catch { }
            }
            finally
            {
                drv.SwitchTo().Window(currentWindowHandle);
            }
            if (!prcRslt) return;

            Thread.Sleep(1000);
            try
            {
                drv.SwitchTo().Frame("resultFrame");
                drv.SwitchTo().Frame("frmOuterModal");
                this.js.ExecuteScript("javascript:return f_continue()", Array.Empty<object>());
            }
            catch (Exception ex)
            {
                prcRslt = false;
                msg = "등기기록 유형 선택 오류-" + ex.Message;
                RgstAutoErr(msg, idx, 13);
            }
            finally
            {
                drv.SwitchTo().Window(currentWindowHandle);
            }
            if (!prcRslt) return;

            Thread.Sleep(2000);            
            try
            {
                drv.SwitchTo().Frame("resultFrame");
                drv.SwitchTo().Frame("frmOuterModal");
                html = drv.PageSource;
                if (html.Contains("신청사건 처리중인 등기부"))
                {
                    //조대로 상태 변경
                    /*
                    sql = "update db_tank.tx_rgst_mdfy set enable=0 where idx=" + idx;
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();
                    */
                    errCd = 20;
                    throw new Exception("처리중인 사건");
                }
                else if (html.Contains("이미 결제"))
                {
                    errCd = 21;
                    throw new Exception("결제한 사건");
                }

                IReadOnlyCollection<IWebElement> cmorts = drv.FindElements(By.Name("y202cmort_flag"));
                int cmortsCnt = cmorts.Count();
                if (cmortsCnt > 0)
                {
                    foreach (IWebElement cmort in cmorts)
                    {
                        cmort.Click();
                        Thread.Sleep(1000);
                        try
                        {
                            if (drv.SwitchTo().Alert().Text.Contains("100매"))
                            {
                                drv.SwitchTo().Alert().Accept();
                                cmortOver = true;
                                if (lsNo == 0 && cmortOver) cmortOverPage = true;
                                break;
                            }
                            drv.SwitchTo().Alert().Accept();
                        }
                        catch { }
                        lsNo++;
                    }
                }

                lsNo = 0;
                IReadOnlyCollection<IWebElement> trades = drv.FindElements(By.Name("y202trade_seq_flag"));
                int tradesCnt = trades.Count();
                if (tradesCnt > 0)
                {
                    foreach (IWebElement trade in trades)
                    {
                        trade.Click();
                        Thread.Sleep(1000);
                        try
                        {
                            if (drv.SwitchTo().Alert().Text.Contains("100매"))
                            {
                                drv.SwitchTo().Alert().Accept();
                                tradeOver = true;
                                if (lsNo == 0 && tradeOver) tradeOverPage = true;
                                break;
                            }
                            drv.SwitchTo().Alert().Accept();
                        }
                        catch { }
                        lsNo++;
                    }
                }

                //[공동담보/전세목록] 또는 [매매목록] 첫번째 체크박스가 100매 이상인 경우 처음부터 옵션에서 선택 해제
                if (cmortOverPage || tradeOverPage)
                {
                    RgstCartPay(row, rowNo, rowCnt);
                    return;
                }

                //[공동담보/전세목록] 또는 [매매목록]이 100매 이상일 경우 각각 첫번째 목록만 체크
                if (cmortOver || tradeOver)
                {
                    if (cmortOver)
                    {
                        foreach (IWebElement cmort in cmorts)
                        {
                            if (cmort.Selected == false) continue;
                            cmort.Click();
                            Thread.Sleep(1000);
                            try
                            {
                                drv.SwitchTo().Alert().Accept();
                            }
                            catch { }
                        }
                        cmorts.ElementAt(0).Click();
                        try
                        {
                            drv.SwitchTo().Alert().Accept();
                        }
                        catch { }
                    }
                    if (tradeOver)
                    {
                        foreach (IWebElement trade in trades)
                        {
                            if (trade.Selected == false) continue;
                            trade.Click();
                            Thread.Sleep(1000);
                            try
                            {
                                drv.SwitchTo().Alert().Accept();
                            }
                            catch { }
                        }
                        trades.ElementAt(0).Click();
                        try
                        {
                            drv.SwitchTo().Alert().Accept();
                        }
                        catch { }
                    }
                }

                drv.FindElement(By.CssSelector("button.btn_bg02_action")).Click();
            }
            catch (Exception ex)
            {
                prcRslt = false;
                if (errCd == 0) errCd = 29;
                RgstAutoErr(ex.Message, idx, errCd);
                try
                {
                    drv.SwitchTo().Alert().Accept();
                }
                catch { }
            }
            finally
            {
                drv.SwitchTo().Window(currentWindowHandle);
            }
            if (!prcRslt) return;
            /*
            if (errCd == 20 && (rowNo % 5 == 0 || rowNo == rowCnt))
            {
                //결제를 건너띄게 됨으로 통과하게 한다.
            }
            else
            {
                if (!prcRslt) return;
            }
            */
            
            Thread.Sleep(2000);
            try
            {
                drv.SwitchTo().Frame("resultFrame");
                drv.FindElement(By.CssSelector("button.btn1_up_bg02_action")).Click();
            }
            catch (Exception ex)
            {
                prcRslt = false;
                msg = "결제대상 부동산 오류-" + ex.Message;
                RgstAutoErr(msg, idx, 15);
            }
            finally
            {
                drv.SwitchTo().Window(currentWindowHandle);
            }
            if (!prcRslt) return;

            //결제
            if (rowNo % 5 == 0 || rowNo == rowCnt)
            {
                Thread.Sleep(3000);
                try
                {
                    drv.FindElement(By.Id("inpMtdCls3")).Click();
                    this.js.ExecuteScript("javascript:$('#inpEMoneyNo1').val('" + irosEMoneyNo1 + "');", Array.Empty<object>());
                    this.js.ExecuteScript("javascript:$('#inpEMoneyNo2').val('" + irosEMoneyNo2 + "');", Array.Empty<object>());
                    this.js.ExecuteScript("javascript:$('#inpEMoneyPswd').val('" + irosEMoneyPwd + "');", Array.Empty<object>());
                    Thread.Sleep(1000);
                    if (drv.FindElement(By.Id("chk_term_agree_all_emoney")).Selected == false)
                    {
                        drv.FindElement(By.Id("chk_term_agree_all_emoney")).Click();
                    }
                    drv.FindElement(By.Name("inpComplete")).Click();
                    try
                    {
                        drv.SwitchTo().Alert().Accept();
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    prcRslt = false;
                    msg = "결제선택 입력/동의 오류-" + ex.Message;
                    RgstAutoErr(msg);
                }
                if (!prcRslt) return;

                Thread.Sleep(3500);
                try
                {
                    //drv.SwitchTo().Window(drv.WindowHandles.Last());      //잦은 오류
                    //http://www.iros.go.kr/iris/fee/RFEEChkPaySuccJ.jsp
                    //http://www.iros.go.kr/iris/fee/RFEEChkPayFailJ.jsp
                    foreach (string winNm in drv.WindowHandles)
                    {
                        drv.SwitchTo().Window(winNm);
                        if (drv.Url.Contains("/fee/RFEEChkPay"))
                        {
                            html = drv.PageSource;
                            /*
                            if (html.Contains("이미 결제"))
                            {
                                //중복결제건
                                throw new Exception("중복 결제");
                            }
                            */
                            if (html.Contains("잔액이 부족"))
                            {
                                //전자민원캐시의 잔액이 부족합니다
                                cashBalance = false;
                                throw new Exception("잔액 부족");
                            }
                            else
                            {
                                ClickElementSafe(drv, drv.FindElement(By.CssSelector("button.btn_bg02_action")), 5);
                                txtPrgs.AppendText(" -> 결제");
                            }
                        }
                    }                    
                }
                catch (Exception ex)
                {
                    prcRslt = false;
                    msg = "결제성공 확인 오류-" + ex.Message;
                    RgstAutoErr(msg);
                }
                finally
                {
                    drv.SwitchTo().Window(currentWindowHandle);                    
                }
                Thread.Sleep(3000);

                //MessageBox.Show(drv.Url.ToString());

                //결제 기록(결제완료 첫페이지만 기록하므로 참고용으로 사용한다.)-실제로는 미열람/미발급 메뉴가 아닌 별도 페이지
                if (!drv.Url.Contains("RFEECompletePayProcC_unisvw")) return;                
                IReadOnlyCollection<IWebElement> ecTr = drv.FindElements(By.XPath(@"//*[@id='Lcontent']/form[1]/div[5]/table/tbody/tr"));
                if (ecTr == null) return;
                if (ecTr.Count() < 2) return;

                lsNo = 0;
                db.Open();
                foreach (IWebElement el in ecTr)
                {
                    lsNo++;
                    if (lsNo == 1) continue;
                    try
                    {
                        IWebElement elPin = el.FindElements(By.TagName("td"))[4];
                        pin = Regex.Replace(elPin.Text, @"[^\d]", string.Empty);
                        //sql = $"update db_tank.tx_rgst_auto set pay=1 where wdt=curdate() and pin='{pin}'";
                        sql = $"update db_tank.tx_rgst_auto set pay=1 where wdt > date_sub(curdate(),INTERVAL 10 day) and pin='{pin}'";
                        //MessageBox.Show(sql);
                        db.ExeQry(sql);
                    }
                    catch 
                    {
                        continue;
                    }
                }
                db.Close();
            }
        }

        /// <summary>
        /// 등기 발급-1 (미열람/미발급)
        /// </summary>
        private void RgstIssue()
        {
            string currentWindowHandle = "";

            Size scrSize = Screen.PrimaryScreen.Bounds.Size;

            idrvSvc = InternetExplorerDriverService.CreateDefaultService(@"C:\Atom", "IEDriverServer.exe");
            idrvSvc.HideCommandPromptWindow = true;
            InternetExplorerOptions ieOpt = new InternetExplorerOptions();
            
            try
            {
                idrv = new InternetExplorerDriver(idrvSvc, ieOpt);
                this.js = (IJavaScriptExecutor)this.idrv;
                idrv.Navigate().GoToUrl("http://www.iros.go.kr");
                currentWindowHandle = idrv.CurrentWindowHandle;   //메인 윈도우
                this.js.ExecuteScript("javascript:window.moveTo(0,0);", Array.Empty<object>());
                this.js.ExecuteScript($"javascript:window.resizeTo(1000,{scrSize.Height});", Array.Empty<object>());
                Thread.Sleep(5000);

                this.js.ExecuteScript("javascript:$('#id_user_id').val('" + irosId + "');", Array.Empty<object>());
                this.js.ExecuteScript("javascript:$('#password').val('" + irosPwd + "');", Array.Empty<object>());
                idrv.FindElement(By.XPath(@"//*[@id='leftS']/div[2]/form/div[1]/ul/li[4]/a/img")).Click();
                Thread.Sleep(5000);
                
                //팝업창 닫기
                if (idrv.WindowHandles.Count > 0)
                {
                    foreach (string winNm in idrv.WindowHandles)
                    {
                        idrv.SwitchTo().Window(winNm);
                        if (idrv.Url.Contains("popupid"))
                        {
                            idrv.Close();
                        }
                    }
                    idrv.SwitchTo().Window(currentWindowHandle);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                idrv.Quit();
                return;
            }

            idrv.Navigate().GoToUrl("http://www.iros.go.kr/frontservlet?cmd=RISURetrieveUnissuedListC&unvRfrYn=Y");
            Thread.Sleep(5000);

            while (true)
            {
                if (idrv == null) break;                
                try
                {
                    if (idrv.PageSource.Contains("열람/발급가능한 부동산이 존재하지 않습니다")) break;
                    IReadOnlyCollection<IWebElement> ecTr = idrv.FindElements(By.XPath(@"//*[@id='Lcontent']/form[1]/div[5]/table/tbody/tr"));
                    if (ecTr == null) break;
                    if (ecTr.Count() < 2) break;
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("삭제된 개체"))
                    {
                        //
                        break;
                    }
                }

                RgstPrintPdf(currentWindowHandle);
                
                try
                {
                    idrv.Navigate().Refresh();
                    Thread.Sleep(3000);
                }
                catch { }
            }
            idrv.Quit();
        }

        /// <summary>
        /// 등기 발급-2 (PDF 파일로 저장)
        /// </summary>
        private void RgstPrintPdf(string currentWindowHandle)
        {
            int rowCnt = 0, pdfPrcWait = 0;
            long timeStamp = 0;
            string pin, idx, locFile;
            IReadOnlyCollection<IWebElement> ecTr = null;

            Thread.Sleep(1500);
            try
            {
                ecTr = idrv.FindElements(By.XPath(@"//*[@id='Lcontent']/form[1]/div[5]/table/tbody/tr"));
                if (ecTr == null)
                {
                    return;
                }

                rowCnt = ecTr.Count();
                if (rowCnt < 2)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                return;
            }

            IWebElement el = ecTr.ElementAt(1);
            IWebElement elPin = el.FindElements(By.TagName("td"))[4];
            pin = Regex.Replace(elPin.Text, @"[^\d]", string.Empty);
            timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();     //오류가 났을 경우 파일명
            
            locFile = $@"{pdfSavePath}\{pin}.pdf";

            db.Open();
            db.ExeQry($"update db_tank.tx_rgst_auto set rdtm=NOW() where pin='{pin}'");     //열람일시 기록
            db.Close();

            try
            {
                //이전 인쇄창이 닫히지 않았을 경우
                if (at.WinExists("인쇄") == 1 || at.WinExists("Bullzip") == 1 || at.WinExists("인터넷등기소") == 1)
                {
                    if (at.WinExists("Bullzip") == 1)
                    {
                        at.WinActivate("Bullzip");
                        Thread.Sleep(500);
                        at.ControlSetText("Bullzip", "", "TextBoxU15", $@"{pdfSavePath}\E-{timeStamp}.pdf");
                        Thread.Sleep(1000);
                        at.ControlClick("Bullzip", "", "CommandButtonU2");
                        Thread.Sleep(2000);
                    }
                    else
                    {
                        if (at.WinExists("인쇄") == 1)
                        {
                            at.WinActivate("인쇄");
                            pdfPrcWait = Convert.ToInt32((Convert.ToDouble(Regex.Match(at.WinGetText("인쇄"), @"(\d+)\s+인쇄 매수", RegexOptions.Multiline).Groups[1].Value) * 0.5 + 60));   // (총매수 * 0.5 + 60)초   //Bullzip
                            if (pdfPrcWait == 0) pdfPrcWait = 60;   //Bullzip
                            at.ControlClick("인쇄", "", "Button10");

                            if (at.WinExists("Bullzip") != 1) at.WinWaitActive("Bullzip", "", pdfPrcWait);
                            at.WinActivate("Bullzip");
                            Thread.Sleep(500);
                            at.ControlSetText("Bullzip", "", "TextBoxU15", $@"{pdfSavePath}\E-{timeStamp}.pdf");
                            Thread.Sleep(1000);
                            at.ControlClick("Bullzip", "", "CommandButtonU2");
                            Thread.Sleep(2000);
                        }
                    }
                    //throw new Exception("이전 인쇄창 열림"); -> 예외로 처리할 경우 2개의 파일이 누락된다.(이전거+현재 클릭한거)

                    if (at.WinExists("등기사항증명서") == 1) at.WinClose("등기사항증명서");
                    if (at.WinExists("인쇄") == 1) at.WinClose("인쇄");
                    if (at.WinExists("Bullzip") == 1) at.WinClose("Bullzip");
                    if (at.WinExists("인터넷등기소") == 1) at.WinClose("인터넷등기소");
                    Thread.Sleep(5000);
                }

                try
                {
                    //요약 체크 불가가 있음
                    if (el.FindElement(By.Name("chkSummary")).Enabled)
                    {
                        ClickElementSafe(idrv, el.FindElement(By.Name("chkSummary")), 30);  //120 -> 30초로 수정
                        Thread.Sleep(1500);
                    }
                    ClickElementSafe(idrv, el.FindElement(By.CssSelector("td:nth-child(11) > button")), 30);  //120 -> 30초로 수정

                    if (idrv.SwitchTo().Alert().Text.Contains("신청사건"))
                    {
                        idrv.SwitchTo().Alert().Accept();
                        db.Open();
                        MySqlDataReader dr = db.ExeRdr($"select idx from db_tank.tx_rgst_auto where wdt > date_sub(curdate(),INTERVAL 10 day) and pin='{pin}' order by idx desc limit 1");
                        if (dr.HasRows)
                        {
                            dr.Read();
                            idx = dr["idx"].ToString();
                        }
                        else
                        {
                            idx = "";
                        }
                        dr.Close();
                        db.Close();
                        RgstAutoErr($"결제 후 변동사건-{pin}", idx, 50);
                    }
                }
                catch (Exception ex)
                { 
                    //
                }

                if (printTest == false) Thread.Sleep(2000);
                if (at.WinExists("테스트열람") == 1)
                {
                    if (printTest == true)
                    {
                        at.WinClose("테스트열람");
                        return;
                    }
                    at.WinActivate("테스트열람");
                    //idrv.SwitchTo().Window(idrv.WindowHandles.Last());
                    if (idrv.WindowHandles.Count > 0)
                    {
                        foreach (string winNm in idrv.WindowHandles)
                        {
                            idrv.SwitchTo().Window(winNm);
                            if (idrv.Title.Contains("테스트열람"))
                            {
                                Thread.Sleep(1000);
                                //this.js.ExecuteScript("javascript:f_goTestView(); return false;", Array.Empty<object>());
                                idrv.FindElement(By.XPath(@"//*[@id='content1']/div[2]/div/div/a/strong")).Click();
                                Thread.Sleep(3000);

                                if (at.WinExists("인터넷등기소") != 1) at.WinWaitActive("인터넷등기소", "", 30);
                                at.WinActivate("인터넷등기소");
                                at.ControlClick("인터넷등기소", "", "Button1");
                                printTest = true;
                                Thread.Sleep(3000);
                            }
                        }
                        //idrv.SwitchTo().Window(currentWindowHandle);
                    }
                    idrv.SwitchTo().Window(currentWindowHandle);
                    //at.WinActive("인터넷등기소");
                    return;
                }

                if (at.WinExists("등기사항증명서") != 1) at.WinWaitActive("등기사항증명서", "", 30);
                at.WinActivate("등기사항증명서");                
                at.ControlClick("등기사항증명서", "", "Button5");

                if (at.WinExists("인쇄") != 1) at.WinWaitActive("인쇄", "", 10);
                at.WinActivate("인쇄");

                pdfPrcWait = Convert.ToInt32((Convert.ToDouble(Regex.Match(at.WinGetText("인쇄"), @"(\d+)\s+인쇄 매수", RegexOptions.Multiline).Groups[1].Value) * 0.5 + 60));   // (총매수 * 0.5 + 60)초   //Bullzip
                if (pdfPrcWait == 0) pdfPrcWait = 60;   //Bullzip
                at.ControlClick("인쇄", "", "Button10");

                //Bullzip
                if (at.WinExists("Bullzip") != 1) at.WinWaitActive("Bullzip", "", pdfPrcWait);
                at.WinActivate("Bullzip");
                Thread.Sleep(500);
                at.ControlSetText("Bullzip", "", "TextBoxU15", locFile);
                Thread.Sleep(1000);
                at.ControlClick("Bullzip", "", "CommandButtonU2");
                Thread.Sleep(2000);

                if (at.WinExists("인터넷등기소") != 1) at.WinWaitActive("인터넷등기소", "", 40);
                at.WinActivate("인터넷등기소");
                at.ControlClick("인터넷등기소", "", "Button1");                

                at.WinActivate("등기사항증명서");
                at.ControlClick("등기사항증명서", "", "Button11");

                db.Open();
                db.ExeQry($"update db_tank.tx_rgst_auto set idtm=NOW() where pin='{pin}'");     //발급일시 기록
                db.Close();

                if (File.Exists(locFile))
                {
                    UploadDBPrc(locFile);   //FTP 업로드 및 DB 처리
                }
            }
            catch (Exception ex)
            {
                RgstAutoErr(ex.Message);

                //재시도-click timed out after 60 seconds
                if (at.WinExists("등기사항증명서") != 1) at.WinWaitActive("등기사항증명서", "", 30);
                if (at.WinExists("등기사항증명서") == 1)
                {
                    at.WinActivate("등기사항증명서");
                    at.ControlClick("등기사항증명서", "", "Button5");

                    if (at.WinExists("인쇄") != 1) at.WinWaitActive("인쇄", "", 10);
                    at.WinActivate("인쇄");

                    pdfPrcWait = Convert.ToInt32((Convert.ToDouble(Regex.Match(at.WinGetText("인쇄"), @"(\d+)\s+인쇄 매수", RegexOptions.Multiline).Groups[1].Value) * 0.5 + 60));   // (총매수 * 0.5 + 60)초   //Bullzip
                    if (pdfPrcWait == 0) pdfPrcWait = 60;   //Bullzip
                    at.ControlClick("인쇄", "", "Button10");

                    //Bullzip
                    if (at.WinExists("Bullzip") != 1) at.WinWaitActive("Bullzip", "", pdfPrcWait);
                    at.WinActivate("Bullzip");
                    Thread.Sleep(500);
                    at.ControlSetText("Bullzip", "", "TextBoxU15", locFile);
                    Thread.Sleep(1000);
                    at.ControlClick("Bullzip", "", "CommandButtonU2");
                    Thread.Sleep(2000);

                    if (at.WinExists("인터넷등기소") != 1) at.WinWaitActive("인터넷등기소", "", 40);
                    at.WinActivate("인터넷등기소");
                    at.ControlClick("인터넷등기소", "", "Button1");

                    at.WinActivate("등기사항증명서");
                    at.ControlClick("등기사항증명서", "", "Button11");

                    db.Open();
                    db.ExeQry($"update db_tank.tx_rgst_auto set idtm=NOW() where pin='{pin}'");     //발급일시 기록
                    db.Close();

                    if (File.Exists(locFile))
                    {
                        UploadDBPrc(locFile);   //FTP 업로드 및 DB 처리
                    }
                }

                //오류시 열린창 모두 닫고 재발급 시작
                idrv.Quit();
                if (at.WinExists("테스트열람") == 1) at.WinClose("테스트열람");
                if (at.WinExists("등기사항증명서") == 1) at.WinClose("등기사항증명서");
                if (at.WinExists("인쇄") == 1) at.WinClose("인쇄");
                if (at.WinExists("Bullzip") == 1) at.WinClose("Bullzip");
                if (at.WinExists("인터넷등기소") == 1) at.WinClose("인터넷등기소");
                Thread.Sleep(5000);

                RgstIssue();
            }
        }

        /// <summary>
        /// FTP 업로드 및 DB, 동일 PIN 다른 사건 처리
        /// </summary>
        /// <param name="locFile"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void UploadDBPrc(string locFile)
        {
            int autoDvsn = 0;
            string sql, idx, tid, lsType, pin, noExtr;
            string spt, sn1, sn2, sn, pn, sta1, sta2, seqNo, ctgr, rmtNm, rmtFile, tbl, cvp, analyRslt;
            string dirNo, rgstInfo;
            string today = DateTime.Now.ToShortDateString();
            List<MySqlParameter> sp = new List<MySqlParameter>();

            Regex rx1 = new Regex(@"(\d{14})\.pdf");
            Match match1 = rx1.Match(locFile);

            pin = match1.Groups[1].Value;
            //sql = $"select * from db_tank.tx_rgst_auto where wdt=curdate() and pin='{pin}' and dvsn > 0";
            sql = $"select * from db_tank.tx_rgst_auto where wdt > date_sub(curdate(),INTERVAL 10 day) and pin='{pin}' and dvsn > 0 and ul=0";
            DataTable dt = db.ExeDt(sql);
            if (dt.Rows.Count == 0) return;

            foreach (DataRow row in dt.Rows)
            {
                idx = row["idx"].ToString();
                autoDvsn = Convert.ToInt32(row["dvsn"]);
                tid = row["tid"].ToString();
                lsType = row["ls_type"].ToString();
                noExtr = row["no_extr"].ToString();
                if (tid == string.Empty) continue;

                try
                {
                    if (autoDvsn < 20)
                    {
                        //경매
                        db.Open();
                        sql = $"select spt, sn1, sn2, pn, sta1, sta2 from db_main.ta_list where tid='{tid}' limit 1";
                        MySqlDataReader dr = db.ExeRdr(sql);
                        dr.Read();
                        spt = dr["spt"].ToString();
                        sn1 = dr["sn1"].ToString();
                        sn2 = dr["sn2"].ToString();
                        pn = dr["pn"].ToString();
                        sta1 = dr["sta1"].ToString();
                        sta2 = dr["sta2"].ToString();
                        dr.Close();

                        sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));
                        pn = pn.ToString().PadLeft(4, '0');
                        seqNo = "01";
                        ctgr = (lsType == "토지") ? "DA" : "DB";
                        rmtNm = string.Format("{0}-{1}-{2}-{3}-{4}.pdf", ctgr, spt, sn, pn, seqNo);
                        rmtFile = $"{ctgr}/{spt}/{sn1}/{rmtNm}";
                        tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
                        cvp = $"ctgr='{ctgr}', spt='{spt}', tid='{tid}', sn='{sn}', file='{rmtNm}', wdt=curdate()";
                        db.Close();

                        if (ftpCA.Upload(locFile, rmtFile))
                        {
                            db.Open();
                            //파일 DB
                            sql = $"insert into {tbl} set {cvp} ON DUPLICATE KEY UPDATE {cvp}";
                            db.ExeQry(sql);

                            //자동발급 DB-1
                            //sql = $"update db_tank.tx_rgst_auto set ul=1 where idx='{idx}'";
                            //db.ExeQry(sql);

                            //등기변동 체크 DB
                            sql = $"update db_tank.tx_rgst_mdfy set proc=1, pdt=curdate() where tid='{tid}' and pin='{pin}' and proc=0";
                            db.ExeQry(sql);

                            //발급일 갱신 DB
                            sql = $"update db_main.ta_list set rgst_udt=curdate() where tid='{tid}'";
                            db.ExeQry(sql);
                            db.Close();

                            analyRslt = String.Empty;

                            //등기 추출
                            if (autoDvsn == 13)
                            {
                                if (sta1 == "10") analyRslt = rgstAnalyCA.Proc(locFile, true, false);   //예정물건(매각준비)인 경우 기존내용 업데이트
                                else analyRslt = rgstAnalyCA.Proc(locFile, true, true);                 //진행 및 미진행 물건인 경우 변경전/후 비교를 위하여 별도 테이블에 기록
                            }
                            else
                            {
                                if (noExtr == "0")
                                {
                                    analyRslt = rgstAnalyCA.Proc(locFile, true, false);
                                }
                            }

                            //자동발급 DB-2
                            //sql = $"update db_tank.tx_rgst_auto set analy='{analyRslt}' where idx='{idx}'";
                            sql = $"update db_tank.tx_rgst_auto set ul=1, analy='{analyRslt}' where idx='{idx}'";
                            db.Open();
                            db.ExeQry(sql);
                            db.Close();

                            //특수조건 매칭-일반 및 선행공고
                            if (autoDvsn == 11 || autoDvsn == 12)
                            {
                                spCdtnChk.RgstLeas(tid);
                            }
                        }
                        else
                        {
                            RgstAutoErr("업로드 실패", idx, 51);
                        }
                    }
                    else
                    {
                        //공매
                        ctgr = (lsType == "토지") ? "I" : "J";
                        dirNo = (Math.Ceiling(Convert.ToDecimal(tid) / 100000) * 100000).ToString().PadLeft(7, '0');
                        rmtNm = $"{ctgr}{tid}.pdf";
                        rmtFile = $"{ctgr}/{dirNo}/{rmtNm}";
                        if (ftpPA.Upload(locFile, rmtFile))
                        {
                            sql = $"select * from tb_file where cltr_no='{tid}' limit 1";
                            db.Open();
                            MySqlDataReader dr = db.ExeRdr(sql);
                            bool dbExist = dr.HasRows;
                            dr.Read();
                            if (dbExist)
                            {
                                rgstInfo = dr["rgst"].ToString().Trim();
                                //bldgInfo = dr["bldg_rgst"].ToString().Trim();
                            }
                            else
                            {
                                rgstInfo = string.Empty;
                                //bldgInfo = string.Empty;
                            }
                            dr.Close();
                            db.Close();

                            var jaFile = new JArray();
                            if (!dbExist || rgstInfo == string.Empty)
                            {
                                var obj = new JObject();
                                obj.Add("fullNm", rmtFile);
                                obj.Add("ctgr", ctgr);
                                obj.Add("rgstDt", today);
                                jaFile.Add(obj);
                            }
                            else
                            {
                                bool newItem = true;
                                JArray jaRgst = JArray.Parse(rgstInfo);
                                foreach (JObject item in jaRgst)
                                {
                                    var obj = new JObject();
                                    obj.Add("fullNm", item["fullNm"].ToString());
                                    obj.Add("ctgr", item["ctgr"].ToString());
                                    if (item["fullNm"].ToString() == rmtFile)
                                    {
                                        newItem = false;
                                        obj.Add("rgstDt", today);
                                    }
                                    else
                                    {
                                        obj.Add("rgstDt", item["rgstDt"]);
                                    }
                                    jaFile.Add(obj);
                                }
                                if (newItem)
                                {
                                    var obj = new JObject();
                                    obj.Add("fullNm", rmtFile);
                                    obj.Add("ctgr", ctgr);
                                    obj.Add("rgstDt", today);
                                    jaFile.Add(obj);
                                }
                            }

                            db.Open();
                            //파일 DB
                            cvp = "cltr_no=@cltr_no, rgst=@rgst";                            
                            sql = "insert into tb_file set " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                            sp.Add(new MySqlParameter("@cltr_no", tid));
                            sp.Add(new MySqlParameter("@rgst", jaFile.ToString()));                            
                            db.ExeQry(sql, sp);
                            sp.Clear();

                            //자동발급 DB-1
                            //sql = $"update db_tank.tx_rgst_auto set ul=1 where idx='{idx}'";
                            //db.ExeQry(sql);
                            db.Close();

                            //등기 추출
                            analyRslt = rgstAnalyPA.Proc(locFile, true);

                            //자동발급 DB-2
                            //sql = $"update db_tank.tx_rgst_auto set analy='{analyRslt}' where idx='{idx}'";
                            sql = $"update db_tank.tx_rgst_auto set ul=1, analy='{analyRslt}' where idx='{idx}'";
                            db.Open();
                            db.ExeQry(sql);
                            db.Close();
                        }
                        else
                        {
                            RgstAutoErr("업로드 실패", idx, 52);
                        }
                    }

                    txtPrgs.AppendText($"\r\n[발급/업로드] - {tid} > {pin}");
                }
                catch (Exception ex)
                {
                    RgstAutoErr($"업로드/DB처리 오류-{ex.Message}", idx, 59);
                }
            }

            IssueStateUpdt();   //전체 발급현황 업데이트
        }

        /// <summary>
        /// 열람클릭 후 발급실패-재열람에서 발급시도(클릭 후 10분 지난 건) -> 아직 미구현
        /// </summary>
        /// <param name="dt"></param>
        private void IssueFailRetry(DataTable dt)
        {
            string currentWindowHandle = "";

            drv.Navigate().GoToUrl("http://www.iros.go.kr/frontservlet?cmd=RISURetrieveReviewListC");
            currentWindowHandle = drv.CurrentWindowHandle;
            Thread.Sleep(5000);
            IReadOnlyCollection<IWebElement> ecTr = drv.FindElements(By.XPath(@"//*[@id='Lcontent']/form/div[3]/table/tbody/tr"));
            IWebElement el = ecTr.ElementAt(1);
            ClickElementSafe(drv, el.FindElement(By.Name("chkSummary")), 30);  //120 -> 30초로 수정
            Thread.Sleep(1500);
            ClickElementSafe(drv, el.FindElement(By.Name("inpView")), 30);  //120 -> 30초로 수정
            Thread.Sleep(1500);

            bool r = imgSrchClick(@"C:\Atom\btnXCtrl.bmp");
            MessageBox.Show("ok");
        }

        /// <summary>
        /// 안전한 클릭-click timed out after 60 seconds 오류 방어-작동여부 불확실함
        /// </summary>
        /// <param name="element"></param>
        /// <param name="driver"></param>
        /// <param name="timeout"></param>
        private void ClickElementSafe(IWebDriver driver, IWebElement element, int timeout)
        {
            // wait for it to be clickable
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeout));
            wait.Until(ExpectedConditions.ElementToBeClickable(element));

            // click it
            element.Click();

            //Exception 메시지-입력 문자열의 형식이 잘못되었습니다. ???
        }

        /// <summary>
        /// 해당 엘리먼트가 보일 때 까지 대기(5초)
        /// </summary>
        /// <param name="drv"></param>
        /// <param name="by"></param>
        /// <returns></returns>
        private static bool WaitVisible(IWebDriver drv, By by)
        {
            WebDriverWait wait = new WebDriverWait(drv, TimeSpan.FromSeconds(5));
            try
            {
                //IWebElement element = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementExists(by));
                IWebElement element = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(by));
            }
            catch
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// AutoIt Image Search (32bit) -> C:\Atom\ImageSearchDLL.dll
        /// </summary>
        /// <param name="imgPath"></param>
        /// <returns></returns>
        public bool imgSrchClick(string imgPath)
        {
            int right = Screen.PrimaryScreen.WorkingArea.Right;
            int bottom = Screen.PrimaryScreen.WorkingArea.Bottom;

            IntPtr result = ImageSearch(0, 0, right, bottom, imgPath);
            String res = Marshal.PtrToStringAnsi(result);

            if (string.IsNullOrEmpty(res) || res[0] == '0') return false;    //not found

            String[] data = res.Split('|');
            //0->found, 1->x, 2->y, 3->image width, 4->image height;        

            // Then, you can parse it to get x and y:
            int x; int y;
            int.TryParse(data[1], out x);
            int.TryParse(data[2], out y);
            
            at.MouseClick("LEFT", x, y);

            return true;
        }

        /// <summary>
        /// 자동화 처리 오류
        /// </summary>
        /// <param name="idx"></param>
        /// <param name="msg"></param>
        private void RgstAutoErr(string msg, string idx = "", int errCd = 0)
        {
            string sql, cDtm;
            if (idx == "")
            {
                sql = $"insert into db_tank.tx_rgst_auto set dvsn=0, msg='{msg}', wdt=curdate(), wtm=curtime()";
            }
            else
            {
                cDtm = (errCd == 20) ? "now()" : "''";
                sql = $"update db_tank.tx_rgst_auto set err_cd={errCd}, msg='{msg}', cdtm={cDtm} where idx='{idx}'";
            }
            db.Open();
            db.ExeQry(sql);
            db.Close();
        }

        /// <summary>
        /// 전체 발급현황 업데이트
        /// </summary>
        private void IssueStateUpdt()
        {
            string sql;
            string today = DateTime.Now.ToShortDateString();

            stateCntAll = 0; stateCntSuc = 0; stateCntWait = 0; stateCntFail = 0;
                        
            try
            {
                sql = $"select * from db_tank.tx_rgst_auto where dvsn > 0 and ((wdt=curdate()) or (wdt > date_sub(curdate(),INTERVAL 10 day) and err_cd=20)) and {RbotTarget}";
                DataTable dt = db.ExeDt(sql);

                if (dt.Rows.Count > 0)
                {
                    stateCntAll = dt.Rows.Count;
                    //stateCntSuc = dt.Select("idtm > '2000-01-01 00:00:00' or ul=1").Count();
                    stateCntSuc = dt.Select($"idtm > '{today} 00:00:00' and idtm <= '{today} 23:59:59'").Count();   //오늘 발급건
                    stateCntFail = dt.Select("idtm < '2000-01-01 00:00:00' and err_cd > 0").Count();
                    stateCntWait = dt.Select("idtm < '2000-01-01 00:00:00' and err_cd = 0 and ul=0").Count();
                }
                lblStateCntAll.Text = $"{stateCntAll}";
                lblStateCntSuc.Text = $"{stateCntSuc}";
                lblStateCntWait.Text = $"{stateCntWait}";
                lblStateCntFail.Text = $"{stateCntFail}";
            }
            catch { }
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (DateTime.Now.Hour < workEndHour)
            {
                //비정상적 종료시 작업 재시작
                if (drv != null)
                { 
                    drv.Close();
                    drv.Quit();
                }

                if (idrv != null)
                { 
                    idrv.Close();
                    idrv.Quit();
                }

                bgwork.RunWorkerAsync();
            }
            else
            {
                string explr = $"{DateTime.Now.ToShortDateString()}";
                if (at.WinExists(explr) == 1)
                {
                    //탐색기 종료
                    at.WinClose(explr);
                }

                bgwork.Dispose();
                this.Dispose();
                this.Close();
            }            
        }
    }
}
