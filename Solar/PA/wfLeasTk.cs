using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Solar.PA
{
    public partial class wfLeasTk : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        AuctCd auctCd = new AuctCd();
        NetUtil net = new NetUtil();

        BackgroundWorker bgwork;
        ManualResetEvent _busy = new ManualResetEvent(true);  //bgwork [PAUSE] or [RESUME]

        DataTable dt = new DataTable();

        //TANK-Web
        private CookieCollection Cookies;
        private CookieContainer cookieContainer;
        private string GoodCook = string.Empty;
        private string TankCook = string.Empty;
        //TANK-Web

        public wfLeasTk()
        {
            InitializeComponent();

            ui.DgSetRead(dgF);
            dgF.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            foreach (DataGridViewColumn col in dgF.Columns)
            {
                col.SortMode = DataGridViewColumnSortMode.NotSortable;
            }

            tankCert();
        }

        private void tankCert()
        {
            if (TankCook != string.Empty) return;

            wbr1.Navigate("https://www.tankauction.com/Mgmt");
            string ssUrl = "https://www.tankauction.com/Mgmt/cert_staff.php?staff_id=solar&staff_pwd=tank1544";
            this.Cookies = new CookieCollection();
            this.cookieContainer = new CookieContainer();

            HttpWebRequest hwr = (HttpWebRequest)WebRequest.Create(ssUrl);
            hwr.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.97 Safari/537.36";
            hwr.CookieContainer = this.cookieContainer;
            HttpWebResponse hwrsp = (HttpWebResponse)hwr.GetResponse();
            hwrsp.Cookies = hwr.CookieContainer.GetCookies(hwr.RequestUri);
            Cookies.Add(hwrsp.Cookies);

            foreach (Cookie cook in Cookies)
            {
                TankCook += (cook.Name + "=" + cook.Value + "; expires=" + cook.Expired + "; path=/ ;");
            }
        }

        private void btnOpenFiles_Click(object sender, EventArgs e)
        {
            int i = 0;
            string mgmtNo, cltrNo, sql, errMsg;
            
            dgF.Rows.Clear();
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "jpg (*.jpg)|*.jpg";
            ofd.FilterIndex = 3;
            ofd.Multiselect = true;

            if (ofd.ShowDialog() != DialogResult.OK) return;

            List<string> lstFile = new List<string>();
            lstFile.AddRange(ofd.FileNames.OrderBy(f => Path.GetFullPath(f)));

            //Regex rx = new Regex(@"(\d+)_\d+");
            Regex rx = new Regex(@"(\d+\-\d+\-\d+)-*(\w+)*.jpg");
            //무조건 파일명으로 소트한다.
            db.Open();
            foreach (var file in lstFile.Select(f => Path.GetFullPath(f)))
            {
                cltrNo = string.Empty;
                errMsg = string.Empty;

                Match match = rx.Match(file);
                if (match.Success)
                {
                    mgmtNo = match.Groups[1].Value;
                    sql = "select cltr_no from tb_list where cmgmt_no='" + mgmtNo + "' limit 1";
                    MySqlDataReader dr = db.ExeRdr(sql);
                    if (dr.HasRows)
                    {
                        dr.Read();
                        cltrNo = dr["cltr_no"].ToString();
                    }
                    else
                    {
                        errMsg = "제외 -> 없는 사건";
                    }
                    dr.Close();
                }
                else
                {
                    errMsg = "제외 -> 파일명 패턴 오류";
                }
                
                i = dgF.Rows.Add();
                dgF["F_No", i].Value = i + 1;
                dgF["F_Src", i].Value = file;
                dgF["F_CltrNo", i].Value = cltrNo;
                dgF["F_Msg", i].Value = errMsg;
            }
            db.Close();
            dgF.ClearSelection();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (dgF.Rows.Count == 0)
            {
                MessageBox.Show("대상 파일이 없습니다.");
                return;
            }
            dgF.ClearSelection();
            this.ActiveControl = btnStart;
            btnStart.BackColor = Color.PaleVioletRed;

            CheckForIllegalCrossThreadCalls = false;    //크로스스레드 에러 무시
            bgwork = new BackgroundWorker();
            bgwork.DoWork += bgwork_DoWork;
            bgwork.RunWorkerCompleted += bgwork_RunWorkerCompleted;
            bgwork.WorkerReportsProgress = false;
            bgwork.WorkerSupportsCancellation = true;
            bgwork.RunWorkerAsync();
        }

        private void bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            //Valid_Prc();
            Convert_Prc();
            Merge_Prc();
            Upload_Prc();
        }

        /// <summary>
        /// 파일 유효성 체크
        /// </summary>
        private void Valid_Prc()
        {
            string sql, cltrNo;

            //Regex reg = new Regex(@"(\w+)\-(\w+)\-(\w+)\-(\w+)\-(\w+)\-*(\w+)*.jpg", RegexOptions.IgnoreCase);    //법원-N1-N2-PN-소재지번호-순차번호
            //Match match;

            foreach (DataGridViewRow row in dgF.Rows)
            {
                cltrNo = row.Cells["F_CltrNo"].Value.ToString();

                sql = "select cltr_no from tb_list where cltr_no='" + cltrNo + "' limit 1";
                db.Open();
                MySqlDataReader dr = db.ExeRdr(sql);
                dr.Read();
                if (dr.HasRows)
                {
                    //
                }
                else
                {
                    row.Cells["F_CltrNo"].Value = "";
                    row.Cells["F_Msg"].Value = "제외 -> 해당 사건 없음";
                    row.DefaultCellStyle.BackColor = Color.Yellow;
                }
                dr.Close();
                db.Close();
            }
        }

        /// <summary>
        /// PDF 변환
        /// </summary>
        private void Convert_Prc()
        {
            string cltrNo, dirNo, srcFile, fileNm, locFile, rmtFile;

            foreach (DataGridViewRow row in dgF.Rows)
            {
                if (!row.Displayed) dgF.FirstDisplayedScrollingRowIndex = row.Index;

                if (row.Cells["F_Msg"].Value != null)
                {
                    if (row.Cells["F_Msg"].Value.ToString().Contains("제외"))
                    {
                        row.Cells["F_Chk"].Value = "F";
                        continue;
                    }
                }

                cltrNo = row.Cells["F_CltrNo"].Value.ToString();
                srcFile = row.Cells["F_Src"].Value.ToString();
                locFile = Regex.Replace(srcFile, @".jpg", @".pdf", RegexOptions.IgnoreCase);
                fileNm = string.Format("H{0}.pdf", cltrNo);
                dirNo = (Math.Ceiling(Convert.ToDecimal(cltrNo) / 100000) * 100000).ToString().PadLeft(7, '0');
                rmtFile = string.Format("H/{0}/{1}", dirNo, fileNm);

                row.Cells["F_Local"].Value = locFile;
                row.Cells["F_Remote"].Value = rmtFile;
                row.Cells["F_Chk"].Value = "T";

                //PdfSharp 에서는 한글 미지원으로 Graphics 로 처리
                //Image img = Image.FromFile(srcFile);                
                Image img = (Image)new Bitmap(Image.FromFile(srcFile), new Size(1024, 768));
                Graphics g = Graphics.FromImage(img);
                
                g.DrawString("TANK AUCTION",
                   new Font("Arial", 80, FontStyle.Bold),
                   new SolidBrush(Color.FromArgb(20, Color.Gray)),
                   45,
                   100);
                g.DrawString("※ 본 정보는 발급처의 도로명주소 입력오류 등으로 일부 사실과 다를 수 있으며, 참고용 정보입니다.",
                    new Font("돋움체", 10, FontStyle.Regular),
                    new SolidBrush(Color.Gray),
                    150,
                    720);

                MemoryStream strm = new MemoryStream();
                img.Save(strm, System.Drawing.Imaging.ImageFormat.Png);

                PdfDocument doc = new PdfDocument();
                PdfPage page = doc.AddPage();
                page.Orientation = PageOrientation.Landscape;

                XGraphics gfx = XGraphics.FromPdfPage(page);
                XImage ximg = XImage.FromStream(strm);
                gfx.DrawImage(ximg, 60, 20);

                doc.Save(locFile);
            }
        }

        /// <summary>
        /// 동일 사건 PDF 병합
        /// </summary>
        private void Merge_Prc()
        {
            int rowIdx = 0;
            string curTid = "", preTid = "", mrgTid = "", dir = "", mrgNm = "";

            List<string> lstTid = new List<string>();
            foreach (DataGridViewRow row in dgF.Rows)
            {
                rowIdx = row.Index;
                //if (rowIdx == 0) continue;
                if (row.Cells["F_Msg"].Value != null)
                {
                    if (row.Cells["F_Msg"].Value.ToString().Contains("제외")) continue;
                }

                //preTid = dgF["F_CltrNo", rowIdx - 1].Value.ToString();
                curTid = row.Cells["F_CltrNo"].Value.ToString();
                if (curTid != preTid) mrgTid = curTid;
                if (curTid == preTid)
                {
                    if (lstTid.Contains(mrgTid) == false) lstTid.Add(mrgTid);
                    row.Cells["F_Msg"].Value = "병합";
                    row.Cells["F_Chk"].Value = "F";
                    row.DefaultCellStyle.ForeColor = Color.Gray;
                }
                preTid = curTid;
            }

            foreach (string tid in lstTid)
            {
                var rows = from DataGridViewRow row in dgF.Rows
                           where (row.Cells["F_CltrNo"].Value != null && row.Cells["F_CltrNo"].Value.Equals(tid))
                           select row;

                PdfDocument mrgDoc = new PdfDocument();
                foreach (DataGridViewRow row in rows)
                {
                    if (row.Cells["F_Msg"].Value != null)
                    {
                        if (row.Cells["F_Msg"].Value.ToString().Contains("제외")) continue;
                    }
                    PdfDocument doc = PdfReader.Open(row.Cells["F_Local"].Value.ToString(), PdfDocumentOpenMode.Import);
                    CopyPages(doc, mrgDoc);
                }

                rowIdx = rows.First().Index;
                FileInfo fi = new FileInfo(dgF["F_Local", rowIdx].Value.ToString());
                dir = fi.DirectoryName;
                mrgNm = dir + @"\" + tid + "_11.pdf";
                mrgDoc.Save(mrgNm);
                dgF["F_Local", rowIdx].Value = mrgNm;
                dgF.Rows[rowIdx].DefaultCellStyle.ForeColor = Color.Blue;
            }
        }

        /// <summary>
        /// PDF 병합-Sub
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        private void CopyPages(PdfDocument from, PdfDocument to)
        {
            for (int i = 0; i < from.PageCount; i++)
            {
                to.AddPage(from.Pages[i]);
            }
        }

        private void Upload_Prc()
        {
            string locFile = "", rmtFile = "", fileNm = "", sql = "", tbl = "", cvp, cltrNo, today;
            bool result1 = false, fail = false;

            today = DateTime.Today.ToShortDateString();
            List<MySqlParameter> sp = new List<MySqlParameter>();
            FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "PA/", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);

            foreach (DataGridViewRow row in dgF.Rows)
            {
                if (!row.Displayed) dgF.FirstDisplayedScrollingRowIndex = row.Index;

                if (row.Cells["F_Msg"].Value != null)
                {
                    if (row.Cells["F_Msg"].Value.ToString().Contains("제외") || row.Cells["F_Msg"].Value.ToString().Contains("병합")) continue;
                }

                DataGridViewCheckBoxCell chk = (DataGridViewCheckBoxCell)row.Cells[0];
                if (chk.Value != chk.TrueValue) continue;
                if (row.Cells["F_CltrNo"].Value == null) continue;
                if (row.Cells["F_CltrNo"].Value.ToString() == string.Empty || row.Cells["F_CltrNo"].Value.ToString() == "0") continue;

                locFile = row.Cells["F_Local"].Value.ToString();
                rmtFile = row.Cells["F_Remote"].Value.ToString();
                //seq = row.Cells["F_Seq"].Value.ToString();

                result1 = true;

                var obj = new JObject();
                obj.Add("fullNm", rmtFile);
                obj.Add("rgstDt", today);

                result1 = ftp1.Upload(locFile, rmtFile);
                if (result1)
                {
                    chk.Value = chk.FalseValue;
                    row.Cells["F_S1"].Value = 1;
                    row.DefaultCellStyle.BackColor = Color.LightGreen;

                    //DB 처리
                    cltrNo = row.Cells["F_CltrNo"].Value.ToString();

                    cvp = "household=@household";
                    sql = "insert into tb_file set cltr_no=@cltr_no, " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                    sp.Add(new MySqlParameter("@cltr_no", cltrNo));
                    sp.Add(new MySqlParameter("@household", obj.ToString()));
                    db.Open();
                    db.ExeQry(sql, sp);
                    sp.Clear();
                    db.Close();
                }
                else
                {
                    fail = true;
                    if (result1) row.Cells["F_S1"].Value = 1;
                    row.DefaultCellStyle.BackColor = Color.LightGray;
                }
            }

            if (fail)
            {
                if (MessageBox.Show("업로드 실패건이 있습니다. 다시 시도 하시겠습니까?", "업로드 재시도", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.Yes)
                {
                    Upload_Prc();
                }
            }
        }

        private void bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            btnStart.BackColor = Color.LimeGreen;
            MessageBox.Show("처리 완료");
        }

        /// <summary>
        /// 웹물건창/업로드 자료 보기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgF_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            string url, cltrNo;
            string myWeb = Properties.Settings.Default.myWeb;
            DataGridViewColumn col = dgF.Columns[e.ColumnIndex];

            if (dgF.CurrentRow == null) return;

            DataGridViewRow row = dgF.CurrentRow;

            if (col == F_Local || col == F_Remote)
            {
                tbcF.SelectedTab = tabPdf;
                if (string.IsNullOrEmpty(row.Cells["F_Remote"].Value.ToString())) return;
                axAcroPDF1.src = myWeb + "FILE/PA/" + row.Cells["F_Remote"].Value.ToString();
            }
            else
            {
                tbcF.SelectedTab = tabWbr;
                cltrNo = row.Cells["F_CltrNo"].Value.ToString();
                url = myWeb + "pa/paView.php?cltrNo=" + cltrNo;                
                wbr1.Document.Cookie = TankCook;
                net.Nvgt(wbr1, url);
            }
        }
    }
}
