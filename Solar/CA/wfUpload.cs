using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Security.Permissions;
using System.Runtime.InteropServices;
using HtmlAgilityPack;
using hap = HtmlAgilityPack;
using HAPDoc = HtmlAgilityPack.HtmlDocument;
using System.Diagnostics;

namespace Solar.CA
{
    public partial class wfUpload : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        AuctCd auctCd = new AuctCd();
        NetUtil net = new NetUtil();
        ApiUtil api = new ApiUtil();

        DataTable dtLawCd, dtDptCd; //법원, 계
        DataTable dtCatCdAll, dtCatCd;  //물건 종별
        DataTable dtStateCd;    //진행 상태
        DataTable dtEtcCd;      //기타 모든 코드
        DataTable dtFileCd;     //파일 종류

        //정규식 기본형태
        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        //TANK-Web
        private CookieCollection Cookies;
        private CookieContainer cookieContainer;
        private string TankCook = string.Empty;
        //TANK-Web

        decimal totRowCnt = 0;
        string cdtn = "";

        BackgroundWorker bgwork;

        string myWeb = Properties.Settings.Default.myWeb;
        FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "CA/", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);

        public wfUpload()
        {
            InitializeComponent();

            init();
        }

        private void init()
        {
            ui.DgSetRead(dgU, 0);
        }

        private void btnOpenFiles_Click(object sender, EventArgs e)
        {
            return;

            int i = 0;
            string tid, ctgr, rmtNm, shr;

            dgU.Rows.Clear();

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "사진 (*.jpg)|*.jpg|문서 (*.pdf,*.html)|*.pdf;*.html|사진 및 문서 (*.jpg,*.pdf,*.html)|*.jpg;*.pdf;*.html";
            ofd.FilterIndex = 3;
            ofd.Multiselect = true;
            if (ofd.ShowDialog() != DialogResult.OK) return;

            foreach (string fullNm in ofd.FileNames)
            {
                tid = string.Empty;
                ctgr = string.Empty;
                shr = string.Empty;
                /*
                if (fullNm.Contains("T_")) continue;

                rmtNm = getRmtNm(fullNm);
                if (!rmtNm.Contains("오류"))
                {
                    Match match = Regex.Match(fullNm, @"(\d+)_(\d+)\-*(\d+)*.\w+", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                    tid = match.Groups[1].Value;
                    ctgr = dtFileCd.Rows.Cast<DataRow>().Where(t => t["cd"].ToString() == rmtNm.Substring(0, 2)).FirstOrDefault()["nm"].ToString();

                    //공유사진 판별(xxxxx_xx-0.jpg)
                    if (rmtNm.Substring(0, 1) == "B" && fullNm.Contains("-0."))
                    {
                        shr = "Y";
                    }
                }
                */
                Match m = Regex.Match(fullNm, @"([A-F][A-K])\-(\d{4})\-(\d{4})\d{6}.*", rxOptM);
                rmtNm = string.Format("{0}/{1}/{2}/{3}", m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Value);

                i = dgU.Rows.Add();
                dgU["dgU_No", i].Value = i + 1;
                dgU["dgU_LocFile", i].Value = fullNm;
                dgU["dgU_Ctgr", i].Value = ctgr;
                //dgU["dgU_Tid", i].Value = tid;
                //dgU["dgU_Shr", i].Value = shr;
                dgU["dgU_RmtFile", i].Value = rmtNm;
            }
            dgU.ClearSelection();
        }

        private void btnUpLoad_Click(object sender, EventArgs e)
        {
            return;

            CheckForIllegalCrossThreadCalls = false;
            bgwork = new BackgroundWorker();
            bgwork.DoWork += Bgwork_DoWork;
            bgwork.RunWorkerCompleted += Bgwork_RunWorkerCompleted;
            bgwork.WorkerReportsProgress = false;
            bgwork.WorkerSupportsCancellation = true;

            bgwork.RunWorkerAsync();
        }

        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            string locFile, rmtFile, rmtNm, thumb, locThumbFile, rmtThumbFile, fileNm, ext, rmtPath;
            string sql, tbl, tid, ctgr, spt, sn, year, cvp, shr;

            foreach (DataGridViewRow row in dgU.Rows)
            {
                if (!row.Displayed)
                {
                    dgU.FirstDisplayedScrollingRowIndex = row.Index;
                }

                locFile = row.Cells["dgU_LocFile"].Value.ToString();
                rmtFile = row.Cells["dgU_RmtFile"].Value.ToString();
                try
                {
                    if (ftp1.FtpFileExists(rmtFile))
                    {
                        long rmtSize = ftp1.GetFileSize(rmtFile);
                        row.Cells["dgU_RmtSize"].Value = rmtSize;
                        if (rmtSize > 0) continue;
                    }

                    if (ftp1.Upload(locFile, rmtFile))
                    {
                        row.Cells["dgU_Rslt"].Value = "성공";
                        row.DefaultCellStyle.BackColor = Color.LightGreen;
                    }
                    else
                    {
                        row.Cells["dgU_Rslt"].Value = "실패";
                        row.DefaultCellStyle.BackColor = Color.PaleVioletRed;
                    }
                }
                catch (Exception ex)
                {
                    row.Cells["dgU_Rslt"].Value = "오류-"+ex.Message;
                    row.DefaultCellStyle.BackColor = Color.Gray;
                    continue;
                }
            }

            MessageBox.Show("OK");
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            MessageBox.Show("작업 종료");
        }
    }
}
