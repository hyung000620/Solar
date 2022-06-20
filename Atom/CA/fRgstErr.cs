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

namespace Atom.CA
{
    public partial class fRgstErr : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AtomLog atomLog = new AtomLog(113);     //등기누락
        AuctCd auctCd = new AuctCd();
        ApiUtil api = new ApiUtil();
        SpCdtnChk spCdtnChk = new SpCdtnChk();

        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 1500, webCnt = 0;

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;

        string filePath;    //로컬 파일저장 경로

        DataTable dtCs;

        FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "CA/", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);

        public fRgstErr()
        {
            InitializeComponent();
            this.Shown += fRgstErr_Shown;
        }

        private void fRgstErr_Shown(object sender, EventArgs e)
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
            }

            dtCs = db.ExeDt("select spt_cd, _gd_cd from ta_cd_cs");
        }

        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            return;

            int totCnt = 0;
            string sql, targetDt;
            
            //본건
            sql = "select R.tid, R.idx, spt, sn1, sn2, pn, sp_rgst from db_main.ta_list L , db_tank.tx_rgst_err R where L.tid=R.tid and R.proc=0 and R.pre=0 and cat1 != 30 and R.wdt=curdate() order by tid";
            DataTable dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;
            txtPrgs.AppendText(string.Format("본건 > 등기 확인 대상-{0}건", totCnt));
            atomLog.AddLog($"▼ 본건 > 대상-{totCnt}");
            Prc_Rgst(dt, 1);

            //선행공고
            dt.Rows.Clear();
            targetDt = DateTime.Now.AddDays(14).ToShortDateString();
            sql = "select R.tid, R.idx, spt, sn1, sn2, pn, sp_rgst from db_main.ta_list L , db_tank.tx_rgst_err R where L.tid=R.tid and R.proc=0 and R.pre=1 and cat1 != 30 and bid_dt >= '" + targetDt + "' order by L.tid";
            dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;
            txtPrgs.AppendText(string.Format("\r\n\r\n선행공고 > 등기 확인 대상-{0}건", totCnt));
            atomLog.AddLog($"▼ 선행 > 대상-{totCnt}");
            Prc_Rgst(dt, 1);

            //예정물건            
            dt.Rows.Clear();
            sql = "select R.tid, R.idx, spt, sn1, sn2, pn, sp_rgst from db_main.ta_list L , db_tank.tx_ready R where L.tid=R.tid and L.sta1=10 and R.rgst=0 and cat1 != 30 order by idx desc";
            dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;
            txtPrgs.AppendText(string.Format("\r\n\r\n예정물건 > 등기 확인 대상-{0}건", totCnt));
            atomLog.AddLog($"▼ 예정 > 대상-{totCnt}");
            Prc_Rgst(dt, 2);

            atomLog.AddLog("실행 완료", 1);
        }

        private void Prc_Rgst(DataTable dt, int prcDvsn)
        {
            int curCnt = 0, totCnt = 0, sucCnt = 0;
            string sql, url, jsData, gdLawCd, spt, sn, sn1, sn2, pn, tid;
            string ctgr, fileNm, fileUrl, locFile, rmtFile, tbl, cvp;
            string rgstDnPath, tkFileNm, errMsg, spRgst;

            rgstDnPath = filePath + @"\등기";
            if (!Directory.Exists(rgstDnPath))
            {
                Directory.CreateDirectory(rgstDnPath);
            }

            totCnt = dt.Rows.Count;

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                tid = row["tid"].ToString();
                spt = row["spt"].ToString();
                sn1 = row["sn1"].ToString();
                sn2 = row["sn2"].ToString();
                pn = row["pn"].ToString();
                spRgst = row["sp_rgst"].ToString();

                txtPrgs.AppendText(string.Format("\r\n> {0} -> {1} / {2} ", tid, curCnt, totCnt));     //진행상태

                var xRow = dtCs.Rows.Cast<DataRow>().Where(t => t["spt_cd"].ToString() == row["spt"].ToString()).SingleOrDefault();
                if (xRow == null || xRow["_gd_cd"].ToString() == "0")
                {
                    errMsg = "법원코드 매칭 오류";
                    txtPrgs.AppendText(errMsg);
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
                        txtPrgs.AppendText(errMsg);
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
                            txtPrgs.AppendText(errMsg + " > " + fileUrl);
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
                            txtPrgs.AppendText(errMsg);
                            continue;
                        }
                        if (m.Groups[1].Value != "1") continue;       //등기_1 만 추출
                        if (ctgr == "DA" && (spRgst == "1" || spRgst == "5" || spRgst == "6")) continue;  //토지별도등기/토지별도등기인수조건/미등기가격포함+토지별도등기 는 추출안함

                        string analyRslt = rgstAnaly.Proc(locFile, true);
                        if (analyRslt != "success")
                        {
                            errMsg = analyRslt;
                            txtPrgs.AppendText(errMsg);
                            continue;
                        }
                        else
                        {
                            if (prcDvsn == 1)
                            {
                                sql = "update db_tank.tx_rgst_err set proc=1 where idx=" + row["idx"].ToString();
                            }
                            else
                            {
                                sql = "update db_tank.tx_ready set rgst=1 where idx=" + row["idx"].ToString();
                            }
                            db.Open();
                            db.ExeQry(sql);
                            db.Close();
                            txtPrgs.AppendText("성공");
                        }
                    }

                    if (prcDvsn == 1)
                    {
                        //임차인 및 등기에서 특수조건 검출
                        spCdtnChk.RgstLeas(tid);
                    }                    
                }
                catch (Exception ex)
                {
                    txtPrgs.AppendText(ex.Message);
                }
            }
            atomLog.AddLog(string.Format(" > 수집-{0}", sucCnt));
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            bgwork.Dispose();

            this.Dispose();
            this.Close();
        }
    }
}
