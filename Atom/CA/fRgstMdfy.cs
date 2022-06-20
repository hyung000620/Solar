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

namespace Atom.CA
{
    public partial class fRgstMdfy : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AuctCd auctCd = new AuctCd();
        AtomLog atomLog = new AtomLog(112);     //로그 생성

        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 2000, webCnt = 0;

        string vmNm = Environment.MachineName;

        ChromeDriverService drvSvc;
        ChromeDriver drv = null;

        public fRgstMdfy()
        {
            InitializeComponent();
            this.Shown += FRgstMdfy_Shown;
        }

        private void FRgstMdfy_Shown(object sender, EventArgs e)
        {
            CheckForIllegalCrossThreadCalls = false;
            bgwork = new BackgroundWorker();
            bgwork.DoWork += Bgwork_DoWork;
            bgwork.RunWorkerCompleted += Bgwork_RunWorkerCompleted;
            bgwork.WorkerReportsProgress = false;
            bgwork.WorkerSupportsCancellation = true;

            init();

            bgwork.RunWorkerAsync();
        }

        private void init()
        {
            drvSvc = ChromeDriverService.CreateDefaultService(@"C:\Atom", "chromedriver.exe");
            drvSvc.HideCommandPromptWindow = true;
            ChromeOptions chOpt = new ChromeOptions();
            //chOpt.AddArgument("--headless");
            chOpt.AddArgument("--window-size=1000,1000");
            chOpt.AddArgument("--disable-gpu");
            chOpt.AddArgument("--no-sandbox");
            chOpt.AddArgument("--disable-dev-shm-usage");
            
            try
            {
                drv = new ChromeDriver(drvSvc, chOpt);
                drv.Navigate().GoToUrl("http://www.iros.go.kr");
                Thread.Sleep(3000);
            }
            catch
            {
                atomLog.AddLog("ChromeDriver 에러");
            }
        }

        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            string sql;

            //처리 대기중인건
            if (vmNm == "VM-9")
            {
                sql = "select M.tid, ls_idx as idx, pin, M.ls_no as no from db_tank.tx_rgst_mdfy M, db_main.ta_list L where M.tid=L.tid and (sta1 in (11,13) or sta2=1011) and enable=0";
                DataTable dt = db.ExeDt(sql);
                procState(dt, 1);
            }

            //진행/미진행/예정물건-매각준비
            if (vmNm == "VM-9" || vmNm == "VM-10") 
            {
                if (vmNm == "VM-9")
                {
                    //sql = "select L.tid, S.idx, S.pin, S.no from ta_list L, ta_ls S where L.tid=S.tid and sta1 in (11,13) and cat1 in (10,20,40) and pin != '' and ex_rgst=0 and (S.idx % 2 = 1) order by L.tid";
                    sql = "select L.tid, S.idx, S.pin, S.no from ta_list L, ta_ls S where L.tid=S.tid and (sta1 in (11,13) or sta2=1011) and cat1 in (10,20,40) and pin != '' and ex_rgst=0 and (S.idx % 2 = 1) order by L.tid";
                }
                else
                {
                    //sql = "select L.tid, S.idx, S.pin, S.no from ta_list L, ta_ls S where L.tid=S.tid and sta1 in (11,13) and cat1 in (10,20,40) and pin != '' and ex_rgst=0 and (S.idx % 2 = 0) order by L.tid";
                    sql = "select L.tid, S.idx, S.pin, S.no from ta_list L, ta_ls S where L.tid=S.tid and (sta1 in (11,13) or sta2=1011) and cat1 in (10,20,40) and pin != '' and ex_rgst=0 and (S.idx % 2 = 0) order by L.tid";
                }
                DataTable dt = db.ExeDt(sql);
                procState(dt, 0);
            }

            drv.Quit();

            atomLog.AddLog("실행 완료", 1);
        }

        private void procState(DataTable dt, int dvsn)
        {
            string sql, url, tid, lsNo, lsIdx, pin, regt_no, html;
            decimal totCnt = 0, curCnt = 0, failCnt = 0, waitCnt = 0, enableCnt = 0;

            IWebElement element;
            totCnt = dt.Rows.Count;
            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                if (curCnt % 1000 == 0) atomLog.AddLog($" > {curCnt} / {totCnt}");  //1000개 처리때 마다 기록

                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                tid = row["tid"].ToString();
                pin = row["pin"].ToString();
                regt_no = pin.Substring(0, 4);
                lsNo = row["no"].ToString();
                lsIdx = row["idx"].ToString();
                txtState.AppendText($"{curCnt} / {totCnt} -> {tid} \r\n");

                url = "http://www.iros.go.kr/frontservlet?cmd=RISUConfirmPinC&selkindcls=&vAddrCls=3&txt_addr_cls=false&e001admin_regn1=&e001admin_regn2=&e001admin_regn3=&a312lot_no=&e001rd_name=&rd_buld_no=&rd_buld_no2=&a301buld_name=&a301buld_no_buld=&a301buld_no_room=&y202pay_no_docs=1&y202cmort_flag=N&y202issue_cls=1&y202trade_seq_flag=N&fromSms=null&fromPub=&y906alt_svc_gb=0&cls_flag=&txt_simple_address=&connCls=1&MenuID=IR010001&pinFlag=N&ENTRY=VW&elecCase=N&Pass=";
                try
                {
                    drv.SwitchTo().Alert().Accept();
                }
                catch 
                {
                    //MessageBox.Show(ex.Message);
                }

                //핀번호 입력
                drv.Navigate().GoToUrl(url);
                By by = By.XPath(@"//*[@id='inpPinNo']");
                if (WaitVisible(drv, by))
                {
                    element = drv.FindElement(by);
                    element.SendKeys(pin);
                }
                else
                {
                    failCnt++;
                    continue;
                }

                //핀번호 검색
                by = By.XPath(@"/html/body/div/form/div/div/div/div/fieldset/div/table/tbody/tr[3]/td[3]/button");
                element = drv.FindElement(by);
                element.Click();

                //소재지번 선택
                by = By.XPath(@"/html/body/div[2]/div[2]/table/tbody/tr[2]/td[5]/button");
                if (WaitVisible(drv, by))
                {
                    element = drv.FindElement(by);
                    element.Click();
                }
                else
                {
                    failCnt++;
                    continue;
                }

                try
                {
                    drv.SwitchTo().Alert().Accept();
                }
                catch
                {
                    //MessageBox.Show(ex.Message);
                }

                html = drv.PageSource;
                if (html.Contains("과다등기부"))
                {
                    sql = $"update ta_ls set ex_rgst=1 where idx={lsIdx}";
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();
                    continue;
                }

                //등기기록 유형 선택
                by = By.XPath(@"/html/body/div/form/div[4]/button");
                if (WaitVisible(drv, by))
                {
                    element = drv.FindElement(by);
                    element.Click();
                }
                else
                {
                    failCnt++;
                    continue;
                }

                html = drv.PageSource;
                if (html.Contains("신청사건 처리중인 등기부"))
                {
                    txtRslt.AppendText($"{tid} -> {lsNo}\r\n");
                    if (dvsn == 0)
                    {
                        sql = $"insert ignore into db_tank.tx_rgst_mdfy set tid={tid}, ls_no={lsNo}, ls_idx={lsIdx}, pin={pin}, proc=0, wdt=curdate()";
                        db.Open();
                        db.ExeQry(sql);
                        db.Close();
                        waitCnt++;
                    }
                }
                else
                {
                    txtRslt.AppendText($"{tid} -> {lsNo} (가능)\r\n");
                    if (dvsn == 1)
                    {
                        sql = $"update db_tank.tx_rgst_mdfy set enable=1 where ls_idx={lsIdx}";
                        db.Open();
                        db.ExeQry(sql);
                        db.Close();
                        enableCnt++;
                    }
                }
            }

            if (dvsn == 0)
            {
                atomLog.AddLog($"처리중-{waitCnt}, 실패-{failCnt}");
            }
            else
            {
                atomLog.AddLog($"열람가능-{enableCnt}, 실패-{failCnt}");

                //자동 발급대상 등록
                sql = "select M.*, S.adrs, S.dvsn, sn1, sn2, pn, spt, dpt, cat3, dpsl_dvsn, sta2, bid_dt, owner, pin_land, pin_bldg from db_tank.tx_rgst_mdfy M, db_main.ta_list L, db_main.ta_dtl D, db_main.ta_ls S " +
                    "where M.tid=L.tid and L.tid=D.tid and D.tid=S.tid and M.ls_idx=S.idx and " +
                    "enable=1 and (sta1 in (11,13) or sta2=1011) and proc=0 and hide=0 and (M.pin=pin_land or M.pin=pin_bldg)";
                DataTable tmpDt = db.ExeDt(sql);

                db.Open();
                foreach (DataRow row in tmpDt.Rows)
                {
                    sql = $"insert into db_tank.tx_rgst_auto set dvsn=13, tid={row["tid"]}, ls_no={row["ls_no"]}, ls_type='{row["dvsn"]}', pin={row["pin"]}, wdt=curdate(), wtm=curtime()";
                    db.ExeQry(sql);

                    sql = $"update db_tank.tx_rgst_mdfy set proc=1, pdt=curdate() where idx={row["idx"]}";  //미리 발급완료로 처리
                    db.ExeQry(sql);
                }
                db.Close();
            }
        }

        private static bool WaitVisible(IWebDriver drv, By by)
        {
            WebDriverWait wait = new WebDriverWait(drv, TimeSpan.FromSeconds(3000));
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
