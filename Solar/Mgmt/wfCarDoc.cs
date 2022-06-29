using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Solar.Mgmt
{
    public partial class wfCarDoc : Form
    {
        DbUtil db = new DbUtil();
        DataTable dtFileCd;     //파일 종류
        BackgroundWorker bgwork;

        FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "CA/", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);

        public wfCarDoc()
        {
            InitializeComponent();
            dtFileCd = db.ExeDt("select cd, nm from ta_cd_file order by cd");

        }

        private void fileSrch_Click(object sender, EventArgs e)
        {
            int i = 0;
            string tid, ctgr, rmtNm, shr;

            dgU.Rows.Clear();

            //사용자가 파일을 열도록 요청하는 표준 대화 상자를 표시합니다.
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

                i = dgU.Rows.Add();
                dgU["dgU_No", i].Value = i + 1;
                dgU["dgU_LocFile", i].Value = fullNm; // 파일명- 로컬
                dgU["dgU_Ctgr", i].Value = ctgr; // 종류
                dgU["dgU_Tid", i].Value = tid; // TID
                dgU["dgU_Shr", i].Value = shr; // 공유
                dgU["dgU_RmtFile", i].Value = rmtNm; // 파일명 - 원격
            }
            dgU.ClearSelection();
        }

        private string getRmtNm(string fullNm)
        {
            int mainNo, subNo;
            string fileNm, ext, extType, tid, ctgr, sql, spt, sn, pn, seqNo, rmtNm;

            Dictionary<int, string> dicDoc = new Dictionary<int, string>();
            dicDoc.Add(13, "AA");
            dicDoc.Add(14, "AB");
            dicDoc.Add(15, "AC");
            dicDoc.Add(2, "AD");
            dicDoc.Add(3, "AE");
            dicDoc.Add(1, "AF");
            dicDoc.Add(12, "AG");
            dicDoc.Add(16, "AH");
            dicDoc.Add(20, "AI");
            dicDoc.Add(21, "AJ");
            dicDoc.Add(4, "DA");
            dicDoc.Add(5, "DB");
            dicDoc.Add(11, "EA");
            dicDoc.Add(10, "EB");
            dicDoc.Add(9, "EC");
            dicDoc.Add(7, "ED");
            dicDoc.Add(6, "EE");
            dicDoc.Add(8, "EF");
            dicDoc.Add(18, "EG");
            dicDoc.Add(19, "EH");
            dicDoc.Add(30, "EI");
            dicDoc.Add(31, "EJ");
            dicDoc.Add(32, "EK");
            dicDoc.Add(500, "FA");
            dicDoc.Add(600, "FB");
            dicDoc.Add(700, "FC");

            FileInfo fi = new FileInfo(fullNm);
            fileNm = fi.Name;
            ext = fi.Extension?.Substring(1) ?? "";

            Match match = Regex.Match(fileNm, @"(\d+)_(\d+)\-*(\d+)*.\w+", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return "오류-파일명";
            }

            tid = match.Groups[1].Value;
            mainNo = Convert.ToInt32(match.Groups[2].Value);
            subNo = string.IsNullOrEmpty(match.Groups[3].Value) ? 1 : Convert.ToInt32(match.Groups[3].Value);
            if (ext == "jpg" || ext == "png" || ext == "gif")
            {
                extType = "img";
                if (mainNo >= 21 && mainNo <= 80) ctgr = "BA";
                else if (mainNo == 9) ctgr = "BB";
                else if (mainNo == 11) ctgr = "BC";
                else if (mainNo == 10) ctgr = "BD";
                else if (mainNo >= 81 && mainNo <= 100) ctgr = "BE";
                else if (mainNo == 6) ctgr = "BF";
                else
                {
                    return "오류-사진 MainNo";
                }
            }
            else if (ext == "html" || ext == "pdf")
            {
                extType = "doc";
                if (dicDoc.ContainsKey(mainNo)) ctgr = dicDoc[mainNo];
                else
                {
                    return "오류-문서 MainNo";
                }
            }
            else
            {
                return "오류-확장자";
            }

            sql = "select spt, sn1, sn2, pn from ta_list where tid=" + tid + " limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            if (dr.HasRows)
            {
                spt = dr["spt"].ToString();
                sn = string.Format("{0}{1}", dr["sn1"], dr["sn2"].ToString().PadLeft(6, '0'));
                pn = dr["pn"].ToString().PadLeft(4, '0');

                if (extType == "img")
                {
                    seqNo = mainNo.ToString().PadLeft(4, '0');
                    //rmtNm = string.Format("{0}-{1}-{2}-{3}.{4}", ctgr, spt, sn, seqNo, ext);
                    rmtNm = string.Format("{0}-{1}-{2}-{3}-{4}.{5}", ctgr, spt, sn, pn, seqNo, ext);
                }
                else
                {
                    seqNo = subNo.ToString().PadLeft(4, '0');
                    if (ctgr == "AG")    //개별문서-> 매각물건명세서
                    {
                        rmtNm = string.Format("{0}-{1}-{2}-{3}.{4}", ctgr, spt, sn, pn, ext);
                    }
                    else if (ctgr == "DA" || ctgr == "DB" || ctgr.Substring(0, 1) == "E")   //개별문서-> 등기, 기타문서
                    {
                        rmtNm = string.Format("{0}-{1}-{2}-{3}-{4}.{5}", ctgr, spt, sn, pn, seqNo, ext);
                    }
                    else
                    {
                        rmtNm = string.Format("{0}-{1}-{2}-{3}.{4}", ctgr, spt, sn, seqNo, ext);
                    }
                }
            }
            else
            {
                rmtNm = "오류-해당 물건 없음(" + tid + ")";
            }
            dr.Close();
            db.Close();
            
            return rmtNm;
        }

        private void fileUpload_Click(object sender, EventArgs e)
        {
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
                /*thumb = "N"; locThumbFile = ""; rmtThumbFile = "";
                rmtNm = row.Cells["dgU_RmtFile"].Value.ToString();
                if (rmtNm.Contains("오류")) continue;*/

                //tid = row.Cells["dgU_Tid"].Value.ToString();
                /*shr = row.Cells["dgU_Shr"].Value.ToString();
                locFile = row.Cells["dgU_LocFile"].Value.ToString();
                FileInfo fi = new FileInfo(locFile);
                fileNm = fi.Name;
                //ext = fi.Extension ?? "";
                ctgr = rmtNm.Substring(0, 1);
                if (ctgr == "B" || ctgr == "C")
                {
                    locThumbFile = string.Format(@"{0}\T_{1}", fi.DirectoryName, fileNm);
                    thumb = PrcSub_Thumb(locFile, locThumbFile);
                }
                Match match = Regex.Match(rmtNm, @"([A-F].)\-(\d{4})\-(\d{10})", RegexOptions.IgnoreCase);
                ctgr = match.Groups[1].Value;
                spt = match.Groups[2].Value;
                sn = match.Groups[3].Value;
                year = sn.Substring(0, 4);
                rmtPath = string.Format(@"{0}/{1}/{2}", ctgr, spt, year);
                rmtFile = string.Format(@"{0}/{1}", rmtPath, rmtNm);*/
                //tidBox.Text = tid;
                /*if (ftp1.Upload(locFile, rmtFile))
                {
                    if (thumb == "Y")
                    {
                        rmtThumbFile = string.Format(@"{0}/T_{1}", rmtPath, rmtNm);
                        ftp1.Upload(locThumbFile, rmtThumbFile);
                    }
                    //DB 처리
                    tbl = (Convert.ToDecimal(year) > 2004) ? ("ta_f" + year) : "ta_f2004";
                    *//*if (ctgr == "AG" || ctgr == "DA" || ctgr == "DB" || ctgr.Substring(0, 1) == "E")    //개별문서-> 매각물건명세서, 등기, 기타문서
                    {
                        cvp = "ctgr='" + ctgr + "', spt='" + spt + "', tid='" + tid + "', sn='" + sn + "', file='" + fileNm + "', wdt=curdate()";
                    }
                    else
                    {
                        cvp = "ctgr='" + ctgr + "', spt='" + spt + "', sn='" + sn + "', file='" + fileNm + "', wdt=curdate()";
                    }*//*
                    if (ctgr.Substring(0, 1) == "B" && shr == "Y")  //사진 공유
                    {
                        cvp = "ctgr='" + ctgr + "', spt='" + spt + "', sn='" + sn + "', file='" + rmtNm + "', wdt=curdate()";
                    }
                    else if (ctgr == "AA" || ctgr == "AB" || ctgr == "AC" || ctgr == "AD" || ctgr == "AE" || ctgr == "AF" || ctgr == "AH")  //물건 통합
                    {
                        cvp = "ctgr='" + ctgr + "', spt='" + spt + "', sn='" + sn + "', file='" + rmtNm + "', wdt=curdate()";
                    }
                    else
                    {
                        cvp = "ctgr='" + ctgr + "', spt='" + spt + "', tid='" + tid + "', sn='" + sn + "', file='" + rmtNm + "', wdt=curdate()";
                    }
                    sql = "insert into " + tbl + " set " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();
                    row.Cells["dgU_Rslt"].Value = "성공";
                    row.DefaultCellStyle.BackColor = Color.LightGreen;
                }
                else
                {
                    row.Cells["dgU_Rslt"].Value = "실패";
                    row.DefaultCellStyle.BackColor = Color.PaleVioletRed;
                }*/
            }
        }

        private string PrcSub_Thumb(string fullNm, string thumbNm)
        {
            string result;
            //string fullNm = string.Format(@"{0}\{1}", filePath, fileNm);
            if (!File.Exists(fullNm) || !Regex.IsMatch(fullNm, @"bmp|gif|jpg|png|tiff"))
            {
                result = "N";
            }
            else
            {
                try
                {
                    Image image = Image.FromFile(fullNm);
                    Image thumb = image.GetThumbnailImage(200, 150, () => false, IntPtr.Zero);
                    //thumb.Save(string.Format(@"{0}\_thumb\{1}", filePath, fileNm));
                    thumb.Save(thumbNm);
                    result = "Y";
                }
                catch
                {
                    result = "N";
                }
            }

            return result;
        }
        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            MessageBox.Show("작업이 완료 되었습니다.");
        }
    }
}
