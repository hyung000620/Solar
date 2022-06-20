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
using System.IO;
using mshtml;
using System.Collections;
using System.Xml;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Solar;
using System.Threading;


namespace Atom.PA
{
    public partial class fRgst : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AtomLog atomLog = new AtomLog(202);     //공매등기
        AuctCd auctCd = new AuctCd();
        ApiUtil api = new ApiUtil();

        BackgroundWorker bgwork;
        int setCnt = 3, setSleep = 1500, webCnt = 0;

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        string filePath;    //로컬 파일저장 경로

        FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "PA/", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);

        public fRgst()
        {
            InitializeComponent();
            this.Shown += FRgst_Shown;
        }

        private void FRgst_Shown(object sender, EventArgs e)
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
            filePath = @"C:\Atom\PA\" + DateTime.Today.ToShortDateString();
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }
        }

        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            return;

            int i = 0, curCnt = 0, totCnt = 0, prcCnt = 0;
            string sql, url, jsData, cltrNo, dirNo, jsFile;
            string ctgr, fileNm, fileUrl, locFile, rmtFile, tbl, cvp;
            string rgstDnPath, tkFileNm, errMsg;
            bool dbFlag = false;

            rgstDnPath = filePath + @"\등기";
            if (!Directory.Exists(rgstDnPath))
            {
                Directory.CreateDirectory(rgstDnPath);
            }

            List<MySqlParameter> sp = new List<MySqlParameter>();

            sql = "select L.cltr_no from tb_list L , tb_file F where L.cltr_no=F.cltr_no and org_dvsn=1 and dpsl_cd=1 and prpt_dvsn='압류재산(캠코)' and rgst='' and stat_nm IN ('입찰준비중','인터넷입찰진행중','인터넷입찰마감','수의계약가능','입찰공고중','현장입찰진행중') and cls_dtm >= NOW()";
            //sql = "SELECT cltr_no FROM tb_list WHERE cmgmt_no in ('2019-08283-001','2019-14916-002','2019-05612-001','2020-02440-001','2020-02444-001','2020-02563-001','2020-05902-001','2020-07139-001','2020-07817-004','2020-07817-004','2020-08544-001','2020-08544-001','2020-08648-003','2020-08648-003','2020-08485-015','2020-11299-005','2020-12368-001','2020-18053-001','2020-18628-001','2020-18709-001','2021-00546-001','2020-18823-001','2020-19012-001','2020-19012-002','2020-19012-003','2020-19012-004','2020-19124-001','2020-19136-001','2021-01736-002','2020-17715-003','2020-17715-004','2020-19246-001','2020-19245-001','2020-19247-001','2021-02323-001','2021-02440-001','2021-02493-001','2021-02920-003','2021-02847-002','2021-03341-001','2021-03832-001','2020-04161-039','2021-04113-001','2021-04841-001','2021-04841-002','2020-08806-003','2020-08806-003','2021-04356-003','2021-04356-003','2021-05925-001','2021-05925-002','2021-05925-003','2021-05925-004','2021-05925-005','2021-05925-006','2021-05925-007','2021-06303-003','2021-03922-003','2021-06614-001','2021-06563-001','2021-04913-001','2021-07152-001','2021-04997-001','2021-07313-001','2021-06332-001','2021-07037-001','2021-06332-002','2021-03097-003','2021-07124-003','2021-02920-004','2020-04161-041','2020-04161-043')";
            DataTable dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;

            var jaFile = new JArray();
            string today = DateTime.Today.ToShortDateString();
            txtPrgs.AppendText(string.Format("등기 확인 대상-{0}건", totCnt));

            RgstAnalyPa rgstAnaly = new RgstAnalyPa();

            foreach (DataRow row in dt.Rows)
            {
                i = 0;
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                jaFile.Clear();

                cltrNo = row["cltr_no"].ToString();
                txtPrgs.AppendText(string.Format("\r\n> {0} -> {1} / {2}", cltrNo, curCnt, totCnt));     //진행상태                                
                try
                {                    
                    url = string.Format("https://intra.auction1.co.kr/partner/tk/pa_fi.php?cltr_no={0}", cltrNo);
                    jsData = net.GetHtml(url);
                    dynamic x = JsonConvert.DeserializeObject(jsData);
                    var items = x["item"];
                    if (items == null)
                    {
                        errMsg = "파일정보 없음";
                        txtPrgs.AppendText(errMsg);
                        continue;
                    }

                    dbFlag = true;
                    
                    Regex rx = new Regex(@"_(\d).pdf", rxOptM);
                    JArray jsArr = JArray.Parse(items.ToString());
                    foreach (JObject item in jsArr)
                    {
                        ctgr = item["ctgr"].ToString();
                        fileNm = item["fileNm"].ToString();
                        Match m = rx.Match(fileNm);
                        fileUrl = item["fileUrl"].ToString();
                        tkFileNm = string.Format("{0}{1}.pdf", ctgr, cltrNo);
                        locFile = string.Format(@"{0}\{1}", rgstDnPath, tkFileNm);
                        Dictionary<string, string> dnRslt = net.DnFile(fileUrl, locFile);
                        if (dnRslt["result"] == "fail")
                        {
                            errMsg = "파일 다운로드 실패";
                            txtPrgs.AppendText(errMsg);
                            dbFlag = false;
                            continue;
                        }

                        dirNo = (Math.Ceiling(Convert.ToDecimal(cltrNo) / 100000) * 100000).ToString().PadLeft(7, '0');
                        rmtFile = string.Format("{0}/{1}/{2}", ctgr, dirNo, tkFileNm);
                        
                        if (ftp1.Upload(locFile, rmtFile))
                        {
                            var obj = new JObject();
                            obj.Add("fullNm", rmtFile);
                            obj.Add("ctgr", ctgr);
                            obj.Add("rgstDt", today);
                            jaFile.Add(obj);

                            //등기추출
                            string analyRslt = rgstAnaly.Proc(locFile, true);
                            if (analyRslt != "success")
                            {
                                errMsg = analyRslt;
                                txtPrgs.AppendText(errMsg);
                                continue;
                            }
                        }
                        else
                        {
                            errMsg = "파일 업로드 실패";
                            txtPrgs.AppendText(errMsg);
                            dbFlag = false;
                            continue;
                        }                        
                    }
                    
                    if (jaFile.Count == 0) continue;
                    
                    if (dbFlag == true)
                    {
                        jsFile = jaFile.ToString();

                        cvp = "rgst=@rgst";
                        sql = "insert into tb_file set cltr_no=@cltr_no, " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                        sp.Add(new MySqlParameter("@cltr_no", cltrNo));
                        sp.Add(new MySqlParameter("@rgst", jsFile));
                        db.Open();
                        db.ExeQry(sql, sp);
                        sp.Clear();
                        db.Close();

                        prcCnt++;
                    }
                }
                catch (Exception ex)
                {
                    txtPrgs.AppendText(ex.Message);
                }
            }
            atomLog.AddLog(string.Format("파일업로드 {0}", prcCnt), 1);
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            bgwork.Dispose();

            this.Dispose();
            this.Close();
        }
    }
}
