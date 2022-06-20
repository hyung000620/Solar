using Solar;
using MySql.Data.MySqlClient;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace Solar.CA
{
    public partial class wfLeasTaein : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        AuctCd auctCd = new AuctCd();
        NetUtil net = new NetUtil();

        BackgroundWorker bgwork;
        ManualResetEvent _busy = new ManualResetEvent(true);  //bgwork [PAUSE] or [RESUME]

        DataTable dt = new DataTable();

        public wfLeasTaein()
        {
            InitializeComponent();

            ui.DgSetRead(dgF);
            dgF.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            FillDtLaw();

            foreach (DataGridViewColumn col in dgF.Columns)
            {
                col.SortMode = DataGridViewColumnSortMode.NotSortable;
            }
            //dgF.Columns["F_Msg"].DefaultCellStyle.NullValue = "";
        }

        private void FillDtLaw()
        {
            dt.Columns.Add("LawNm");
            dt.Columns.Add("tiCd");
            dt.Columns.Add("sptCd");
            dt.PrimaryKey = new DataColumn[] { dt.Columns["tiCd"] };
            dt.Rows.Add("서울중앙지방법원", "101", "1010");
            dt.Rows.Add("서울동부지방법원", "102", "1110");
            dt.Rows.Add("서울서부지방법원", "103", "1210");
            dt.Rows.Add("서울남부지방법원", "104", "1310");
            dt.Rows.Add("서울북부지방법원", "105", "1410");
            dt.Rows.Add("의정부지방법원", "106", "1510");
            dt.Rows.Add("고양지원", "107", "1511");
            dt.Rows.Add("인천지방법원", "201", "1610");
            dt.Rows.Add("부천지원", "202", "1611");
            dt.Rows.Add("수원지방법원", "301", "1710");
            dt.Rows.Add("성남지원", "302", "1711");
            dt.Rows.Add("여주지원", "303", "1712");
            dt.Rows.Add("평택지원", "304", "1713");
            dt.Rows.Add("안산지원", "305", "1714");
            dt.Rows.Add("안양지원", "306", "1715");
            dt.Rows.Add("춘천지방법원", "501", "1810");
            dt.Rows.Add("강릉지원", "503", "1811");
            dt.Rows.Add("원주지원", "502", "1812");
            dt.Rows.Add("속초지원", "504", "1813");
            dt.Rows.Add("영월지원", "505", "1814");
            dt.Rows.Add("청주지방법원", "A01", "1910");
            dt.Rows.Add("충주지원", "A02", "1911");
            dt.Rows.Add("제천지원", "A03", "1912");
            dt.Rows.Add("영동지원", "A04", "1913");
            dt.Rows.Add("대전지방법원", "401", "2010");
            dt.Rows.Add("홍성지원", "405", "2011");
            dt.Rows.Add("논산지원", "406", "2012");
            dt.Rows.Add("천안지원", "402", "2013");
            dt.Rows.Add("공주지원", "403", "2014");
            dt.Rows.Add("서산지원", "404", "2015");
            dt.Rows.Add("대구지방법원", "701", "2110");
            dt.Rows.Add("안동지원", "707", "2112");
            dt.Rows.Add("경주지원", "702", "2113");
            dt.Rows.Add("김천지원", "703", "2114");
            dt.Rows.Add("상주지원", "704", "2115");
            dt.Rows.Add("의성지원", "705", "2116");
            dt.Rows.Add("영덕지원", "706", "2117");
            dt.Rows.Add("포항지원", "708", "2118");
            dt.Rows.Add("대구서부지원", "709", "2111");
            dt.Rows.Add("부산지방법원", "601", "2210");
            dt.Rows.Add("부산동부지원", "602", "2211");
            dt.Rows.Add("부산서부지원", "603", "2212");
            dt.Rows.Add("울산지방법원", "D01", "2310");
            dt.Rows.Add("창원지방법원", "B01", "2410");
            dt.Rows.Add("진주지원", "B05", "2412");
            dt.Rows.Add("통영지원", "B02", "2413");
            dt.Rows.Add("밀양지원", "B04", "2414");
            dt.Rows.Add("거창지원", "B03", "2415");
            dt.Rows.Add("마산지원", "B06", "2411");
            dt.Rows.Add("광주지방법원", "801", "2510");
            dt.Rows.Add("목포지원", "802", "2511");
            dt.Rows.Add("장흥지원", "805", "2512");
            dt.Rows.Add("순천지원", "803", "2513");
            dt.Rows.Add("해남지원", "804", "2514");
            dt.Rows.Add("전주지방법원", "C01", "2610");
            dt.Rows.Add("군산지원", "C03", "2611");
            dt.Rows.Add("정읍지원", "C04", "2612");
            dt.Rows.Add("남원지원", "C02", "2613");
            dt.Rows.Add("제주지방법원", "901", "2710");
        }

        private void btnOpenFiles_Click(object sender, EventArgs e)
        {
            int i = 0;

            dgF.Rows.Clear();
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "jpg (*.jpg)|*.jpg";
            ofd.FilterIndex = 3;
            ofd.Multiselect = true;

            if (ofd.ShowDialog() != DialogResult.OK) return;

            List<string> lstFile = new List<string>();
            lstFile.AddRange(ofd.FileNames.OrderBy(f => Path.GetFullPath(f)));
            
            //무조건 파일명으로 소트한다.
            foreach (var file in lstFile.Select(f => Path.GetFullPath(f)))
            {
                i = dgF.Rows.Add();
                dgF["F_No", i].Value = i + 1;                
                dgF["F_Taein", i].Value = file;                
            }
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
            Valid_Prc();
            Convert_Prc();
            Merge_Prc();
            Upload_Prc();
        }

        /// <summary>
        /// 파일 유효성 체크
        /// </summary>
        private void Valid_Prc()
        {
            int tid = 0;
            string taeNm, dirNm, Tcd, spt, sn1, sn2, pn, seq, sql;

            Regex reg = new Regex(@"(\w+)\-(\w+)\-(\w+)\-(\w+)\-(\w+)\-*(\w+)*.jpg", RegexOptions.IgnoreCase);    //법원-N1-N2-PN-소재지번호-순차번호
            Match match;

            foreach (DataGridViewRow row in dgF.Rows)
            {
                taeNm = row.Cells["F_Taein"].Value.ToString();
                /*
                Bitmap sourceImg = new Bitmap(taeNm);
                if (sourceImg.Width > 1000)
                {
                    row.Cells["F_Msg"].Value = string.Format("제외 -> 이미지 크기 ({0}*{1})", sourceImg.Width, sourceImg.Height);
                    row.DefaultCellStyle.BackColor = Color.Pink;
                    continue;
                }
                */
                FileInfo fi = new FileInfo(taeNm);
                dirNm = fi.DirectoryName;
                match = reg.Match(fi.Name);
                if (!match.Success)
                {
                    row.Cells["F_Msg"].Value = "제외 -> 원본 파일명 패턴 불일치";
                    row.DefaultCellStyle.BackColor = Color.Yellow;
                    continue;
                }
                Tcd = match.Groups[1].Value;
                DataRow[] rows = dt.Select("tiCd='" + Tcd + "'");
                if (rows.Count() < 1)
                {
                    row.Cells["F_Msg"].Value = "제외 -> 원본 파일명 법원 오류";
                    row.DefaultCellStyle.BackColor = Color.Yellow;
                    continue;
                }
                spt = rows[0]["sptCd"].ToString();
                sn1 = match.Groups[2].Value;
                sn2 = match.Groups[3].Value;
                pn = Convert.ToDecimal(match.Groups[4].Value).ToString();
                seq = match.Groups[6].Value;
                row.Cells["F_Seq"].Value = (seq != string.Empty) ? seq : string.Empty;
                row.Cells["F_SN1"].Value = sn1;
                row.Cells["F_SN2"].Value = sn2;
                row.Cells["F_PN"].Value = pn;

                sql = "select tid from ta_list where spt='" + spt + "' and sn1='" + sn1 + "' and sn2='" + sn2 + "' and ";
                if (pn == "1") sql += "pn in (0,1)";
                else sql += "pn='" + pn + "'";
                sql += " limit 1";
                db.Open();
                MySqlDataReader dr = db.ExeRdr(sql);
                dr.Read();
                if (dr.HasRows)
                {
                    tid = Convert.ToInt32(dr["tid"]);
                    row.Cells["F_Spt"].Value = spt;
                }
                else
                {
                    tid = 0;
                    row.Cells["F_Msg"].Value = "제외 -> 탱크 해당 사건 없음";
                    row.DefaultCellStyle.BackColor = Color.Yellow;
                }
                row.Cells["F_TID"].Value = tid.ToString();
                dr.Close();
                db.Close();
            }
        }

        /// <summary>
        /// PDF 변환
        /// </summary>
        private void Convert_Prc()
        {
            int tid = 0;
            string spt, sn1, sn2, pn, taeNm, fileNm, locFile, rmtFile;

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

                tid = Convert.ToInt32(row.Cells["F_TID"].Value);
                spt = row.Cells["F_Spt"].Value.ToString();
                sn1 = row.Cells["F_SN1"].Value.ToString();
                sn2 = row.Cells["F_SN2"].Value.ToString();
                pn = row.Cells["F_PN"].Value.ToString();

                taeNm = row.Cells["F_Taein"].Value.ToString();                
                locFile = Regex.Replace(taeNm, @".jpg", @".pdf", RegexOptions.IgnoreCase);
                fileNm = string.Format("EA-{0}-{1}{2}-{3}.pdf", spt, sn1, sn2.PadLeft(6, '0'), pn.PadLeft(4, '0'));
                rmtFile = string.Format("{0}/{1}/{2}/{3}", "EA", spt, sn1, fileNm);

                row.Cells["F_Local"].Value = locFile;
                row.Cells["F_Remote"].Value = rmtFile;
                row.Cells["F_Chk"].Value = "T";

                //PdfSharp 에서는 한글 미지원으로 Graphics 로 처리
                //Image img = Image.FromFile(taeNm);
                Image img = (Image)new Bitmap(Image.FromFile(taeNm), new Size(1024, 768));
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
                    680);

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
                if (rowIdx == 0) continue;
                if (row.Cells["F_Msg"].Value != null)
                {
                    if (row.Cells["F_Msg"].Value.ToString().Contains("제외")) continue;
                }

                //prePid = dgF["F_PID", rowIdx - 1].Value.ToString();
                curTid = row.Cells["F_TID"].Value.ToString();
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
                           where (row.Cells["F_TID"].Value != null && row.Cells["F_TID"].Value.Equals(tid))
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
            string locFile = "", rmtFile = "", fileNm = "", sql = "", tbl = "", tid, spt, sn, sn1, sn2, pn, cvp, ctgr;
            bool result1 = false, fail = false;

            ctgr = "EA";
            FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "CA/", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);
                        
            foreach (DataGridViewRow row in dgF.Rows)
            {
                if (!row.Displayed) dgF.FirstDisplayedScrollingRowIndex = row.Index;

                if (row.Cells["F_Msg"].Value != null)
                {
                    if (row.Cells["F_Msg"].Value.ToString().Contains("제외") || row.Cells["F_Msg"].Value.ToString().Contains("병합")) continue;
                }

                DataGridViewCheckBoxCell chk = (DataGridViewCheckBoxCell)row.Cells[0];
                if (chk.Value != chk.TrueValue) continue;
                if (row.Cells["F_TID"].Value == null) continue;
                if (row.Cells["F_TID"].Value.ToString() == string.Empty || row.Cells["F_TID"].Value.ToString() == "0") continue;

                locFile = row.Cells["F_Local"].Value.ToString();
                rmtFile = row.Cells["F_Remote"].Value.ToString();
                //seq = row.Cells["F_Seq"].Value.ToString();
                
                result1 = true;
                
                result1 = ftp1.Upload(locFile, rmtFile);
                if (result1)
                {
                    chk.Value = chk.FalseValue;
                    row.Cells["F_S1"].Value = 1;
                    row.DefaultCellStyle.BackColor = Color.LightGreen;

                    //DB 처리
                    tid = row.Cells["F_TID"].Value.ToString();
                    spt = row.Cells["F_Spt"].Value.ToString();                    
                    sn1 = row.Cells["F_SN1"].Value.ToString();
                    sn2 = row.Cells["F_SN2"].Value.ToString();
                    pn = row.Cells["F_PN"].Value.ToString();
                    sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));
                    fileNm = string.Format("{0}-{1}-{2}-{3}.pdf", ctgr, spt, sn, pn.PadLeft(4, '0'));
                    tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
                    cvp = "ctgr='" + ctgr + "', spt='" + spt + "',tid='" + tid + "', sn='" + sn + "', file='" + fileNm + "', wdt=curdate()";
                    sql = "insert into " + tbl + " set " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                    db.Open();
                    db.ExeQry(sql);
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
            string url, tid;
            string myWeb = Properties.Settings.Default.myWeb;
            DataGridViewColumn col = dgF.Columns[e.ColumnIndex];

            if (dgF.CurrentRow == null) return;

            DataGridViewRow row = dgF.CurrentRow;

            if (col == F_Local || col == F_Remote)
            {
                tbcF.SelectedTab = tabPdf;
                axAcroPDF1.src = myWeb + "FILE/CA/" + row.Cells["F_Remote"].Value.ToString();
            }
            else
            {
                tbcF.SelectedTab = tabWbr;
                tid = row.Cells["F_TID"].Value.ToString();
                url = myWeb + "ca/caView.php?tid=" + tid;
                net.Nvgt(wbr1, url);
            }
        }
    }
}
