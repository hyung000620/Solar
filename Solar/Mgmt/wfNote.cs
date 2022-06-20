using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using Solar.CA;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Remoting;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Solar.Mgmt
{
    public partial class wfNote : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();

        DataTable dtStaff;

        decimal totRowCnt = 0;
        string cdtn = "";
        string myId = Properties.Settings.Default.USR_ID;

        ImageList imgList;

        //BackgroundWorker bgwork;
        FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "INTRA/Note", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);

        public wfNote()
        {
            InitializeComponent();

            init();
            this.Shown += WfNote_Shown;
        }

        private void WfNote_Shown(object sender, EventArgs e)
        {
            btnSrch_Click(null, null);
        }

        private void init()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            string sql;
            CheckBox cbx;

            ui.DgSetRead(dg, 0);
            ui.DgSetRead(dgU, 0);
            dg_Attach.DefaultCellStyle.NullValue = null;
            lblSendDtm.Text = String.Empty;
            lblNoteIdx.Text = String.Empty;

            ui.SetPagn(panPagn, rows: 100, min: 50, inc: 50);

            sql = "select id, name, team from db_tank.tz_staff where team > 0 order by name";
            dtStaff = db.ExeDt(sql);
            
            foreach (DataRow row in dtStaff.Rows)
            {
                cbx = new CheckBox();
                cbx.Name = $"chkStf_{row["id"]}";
                cbx.Text = $"{row["name"]}";
                cbx.Padding = new Padding(10, 0, 0, 0);
                cbx.Width = 70;
                cbx.CheckedChanged += (s, e) => {
                    CheckBox chkMember = s as CheckBox;
                    if (chkMember.Checked) chkMember.BackColor = Color.LightGreen;
                    else chkMember.BackColor = Color.White;
                };
                if ($"{row["team"]}" == "100") flpA.Controls.Add(cbx);
                else if ($"{row["team"]}" == "101") flpB.Controls.Add(cbx);
                else if ($"{row["team"]}" == "102") flpC.Controls.Add(cbx);
                else if ($"{row["team"]}" == "109") flpD.Controls.Add(cbx);
                else if ($"{row["team"]}" == "200") flpE.Controls.Add(cbx);
                else if ($"{row["team"]}" == "201") flpF.Controls.Add(cbx);
            }

            imgList = new ImageList();
            imgList.ImageSize = new Size(16, 16);
            imgList.ColorDepth = ColorDepth.Depth32Bit;
            imgList.Images.Add("icoRecv", Properties.Resources.msg_16_blue);
            imgList.Images.Add("icoSend", Properties.Resources.pencil_16_red);
            tbcR.ImageList = imgList;
            tabRecv.ImageKey = "icoRecv";
            tabSend.ImageKey = "icoSend";
        }

        private void btnSrch_Click(object sender, EventArgs e)
        {
            string sql = "";

            cdtn = "1";
            dg.Rows.Clear();
            ui.FormClear(tabRecv);
            lblSendDtm.Text = String.Empty;
            flpAttach.Controls.Clear();

            List<string> cdtnList = new List<string>();

            if (rdoRecv.Checked)
            {
                cdtnList.Add($"rdel=0");
                cdtnList.Add($"rid='{myId}'");
                dg_SendRecver.HeaderText = "보낸 사람";
            }
            if (rdoSend.Checked)
            {
                cdtnList.Add($"sdel=0");
                cdtnList.Add($"sid='{myId}'");
                dg_SendRecver.HeaderText = "받는 사람";
            }

            if (cdtnList.Count > 0) cdtn = string.Join(" and ", cdtnList.ToArray());
            
            sql = "select COUNT(*) from db_tank.tz_note where " + cdtn;

            db.Open();
            totRowCnt = (decimal)((Int64)db.RowCnt(sql));
            db.Close();

            ComboBox cbx = (ComboBox)panPagn.Controls["_cbxPagn"];
            cbx.SelectedIndexChanged -= gotoPageList;
            ui.InitPagn(panPagn, totRowCnt);
            cbx.SelectedIndexChanged += gotoPageList;
            if (cbx.Items.Count > 0) cbx.SelectedIndex = 0;
        }

        private void gotoPageList(object sender, EventArgs e)
        {
            int i = 0;
            decimal startRow = 0;
            string sql, order;

            dg.Rows.Clear();

            //DataTable dt = new DataTable();
            //dt.Columns.Add("No");
            ComboBox cbxPage = (ComboBox)sender;
            NumericUpDown listScale = (NumericUpDown)panPagn.Controls["_nudList"];
            startRow = (decimal.Parse(cbxPage.Text) - 1) * listScale.Value;
            dg.SelectionChanged -= dg_SelectionChanged;

            order = "idx desc";

            sql = "select * from db_tank.tz_note";
            sql += " where " + cdtn + " order by " + order + " limit " + startRow + "," + listScale.Value;

            this.Cursor = Cursors.WaitCursor;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                i = dg.Rows.Add();
                dg["dg_No", i].Value = totRowCnt - startRow - i;
                dg["dg_Title", i].Value = dr["title"];
                if (rdoRecv.Checked)
                {
                    dg["dg_SendRecver", i].Value = dtStaff.Rows.Cast<DataRow>().Where(t => t["id"].ToString() == dr["sid"].ToString()).FirstOrDefault()["name"];
                }
                else
                {
                    dg["dg_SendRecver", i].Value = dtStaff.Rows.Cast<DataRow>().Where(t => t["id"].ToString() == dr["rid"].ToString()).FirstOrDefault()["name"];
                }
                dg["dg_Sdtm", i].Value = $"{dr["sdtm"]:yy.MM.dd(ddd) HH:mm:ss}";
                if (!string.IsNullOrEmpty(dr["attach"].ToString()))
                {
                    dg["dg_Attach", i].Value = Properties.Resources.save_16;
                }
                //((DataGridViewImageCell)dg["dg_Attach", i]).Value = null;
                dg["dg_Rdtm",i].Value=(dr["rcnt"].ToString()=="0") ? "읽지 않음" : $"{dr["rdtm"]:yy.MM.dd(ddd) HH:mm:ss}";
                dg["dg_Idx", i].Value = dr["idx"];
            }
            dr.Close();
            db.Close();

            dg.ClearSelection();
            this.Cursor = Cursors.Default;

            dg.SelectionChanged += dg_SelectionChanged;
        }

        private void dg_SelectionChanged(object sender, EventArgs e)
        {
            int i, rCnt;
            string sql, idx, ext, rDtm;
            LinkLabel lnkFile;

            if (dg.CurrentRow == null)
            {
                this.Cursor = Cursors.Default;
                return;
            }

            i = dg.CurrentRow.Index;

            this.Cursor = Cursors.WaitCursor;
            ui.FormClear(tabRecv);
            lblSendDtm.Text = String.Empty;
            flpAttach.Controls.Clear();
            tbcR.SelectedTab = tabRecv;

            idx = dg["dg_Idx", i].Value.ToString();
            sql = $"select * from db_tank.tz_note where idx={idx} limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            rCnt = Convert.ToInt32(dr["rcnt"]);

            if (rdoRecv.Checked)
            {
                txtSendRecver.Text = dtStaff.Rows.Cast<DataRow>().Where(t => t["id"].ToString() == dr["sid"].ToString()).FirstOrDefault()["name"].ToString();
            }
            else
            {
                txtSendRecver.Text = dtStaff.Rows.Cast<DataRow>().Where(t => t["id"].ToString() == dr["rid"].ToString()).FirstOrDefault()["name"].ToString();
            }
            
            lblNoteIdx.Text = idx;
            txtRecvTids.Text = dr["ref_tid"].ToString();
            lblSendDtm.Text = $"{dr["sdtm"]:yyyy.MM.dd(ddd) HH:mm:ss}";

            if (dr["ref_id"].ToString() != String.Empty)
            {
                string[] refIds = dr["ref_id"].ToString().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                List<string> refList = new List<string>();
                foreach (string refId in refIds)
                {
                    refList.Add(dtStaff.Rows.Cast<DataRow>().Where(t => t["id"].ToString() == refId).FirstOrDefault()["name"].ToString());
                }
                txtRefList.Text = string.Join(", ", refList.ToArray());
            }

            txtRecvTitle.Text = dr["title"].ToString();
            txtRecvMsg.Text = dr["msg"].ToString();
            if (dr["attach"].ToString() == string.Empty)
            {
                Label lbl = new Label();
                lbl.Text = "※ 첨부 파일이 없습니다.";
                lbl.AutoSize = true;
                lbl.Margin = new Padding(10);
                flpAttach.Controls.Add(lbl);
            }
            else
            {
                JArray jaFile = JArray.Parse(dr["attach"].ToString());
                foreach (JObject item in jaFile)
                {
                    lnkFile = new LinkLabel();
                    lnkFile.AutoSize = true;
                    lnkFile.Dock = DockStyle.Fill;
                    lnkFile.Margin = new Padding(10);
                    lnkFile.Padding = new Padding(20, 0, 0, 0);
                    lnkFile.Name = item["saveNm"].ToString();
                    lnkFile.Text = $"{item["orgnNm"]} ({item["size"]})";
                    Bitmap bmp = new Bitmap(Properties.Resources.save_16.ToBitmap(), new Size(12, 12));
                    lnkFile.Image = bmp;
                    lnkFile.ImageAlign = ContentAlignment.MiddleLeft;
                    //lnkFile.TextAlign = ContentAlignment.MiddleRight;
                    lnkFile.LinkBehavior = LinkBehavior.HoverUnderline;
                    lnkFile.LinkClicked += (s, a) =>
                    {
                        ext = Regex.Match(item["saveNm"].ToString(), @"\.(\w+)$").Groups[1].Value;
                        LinkLabel lnk = s as LinkLabel;
                        SaveFileDialog sfd = new SaveFileDialog();
                        //sfd.InitialDirectory = @"C:\";
                        sfd.Title = "첨부파일 저장 위치 지정";
                        sfd.FileName = $"{item["orgnNm"]}";
                        sfd.DefaultExt = $"{ext}";
                        sfd.Filter = $"{ext.ToUpper()} Files(*.{ext})|*.{ext}";                        
                        if (sfd.ShowDialog() == DialogResult.OK)
                        {
                            ftp1.Download(lnk.Name, sfd.FileName, true);
                        }
                    };
                    flpAttach.Controls.Add(lnkFile);
                }
                jaFile.Clear();
            }
            dr.Close();

            if (rdoRecv.Checked)
            {
                if (rCnt == 0)
                {
                    rDtm = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                    db.ExeQry($"update db_tank.tz_note set rdtm='{rDtm}', rcnt=(rcnt+1) where idx={idx}");
                    dg["dg_Rdtm", i].Value = $"{Convert.ToDateTime(rDtm):yy.MM.dd(ddd) HH:mm:ss}";
                }
                else
                {
                    db.ExeQry($"update db_tank.tz_note set rcnt=(rcnt+1) where idx={idx}");
                }
            }
            db.Close();            
            this.Cursor = Cursors.Default;
        }

        private void chkTeam_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox chkTeam = sender as CheckBox;
            string teamNm = chkTeam.Name;            
            FlowLayoutPanel flp = this.Controls.Find($"flp{teamNm.Substring(teamNm.Length - 1)}", true)[0] as FlowLayoutPanel;
            foreach (Control ctrl in flp.Controls)
            {
                if (ctrl is CheckBox)
                {
                    ((CheckBox)ctrl).Checked = chkTeam.Checked;
                }
            }
        }

        private void btnOpenFiles_Click(object sender, EventArgs e)
        {
            int i = 0;
            string fileSize;

            //dgU.Rows.Clear();

            OpenFileDialog ofd = new OpenFileDialog();
            //ofd.Filter = "사진 (*.jpg)|*.jpg|문서 (*.pdf,*.html)|*.pdf;*.html|사진 및 문서 (*.jpg,*.pdf,*.html)|*.jpg;*.pdf;*.html";
            //ofd.FilterIndex = 3;
            ofd.Multiselect = true;
            if (ofd.ShowDialog() != DialogResult.OK) return;

            foreach (string fullNm in ofd.FileNames)
            {
                FileInfo fi = new FileInfo(fullNm);
                if ((fi.Length / 1048576) > 100)
                {
                    MessageBox.Show($"{fullNm}\r\n☞ 파일크기가 100MB 를 넘습니다.");
                    continue;
                }
                fileSize = GetFileSize(fi.Length);
                
                i = dgU.Rows.Add();
                dgU["dgU_No", i].Value = i + 1;
                dgU["dgU_LocFile", i].Value = fullNm;
                dgU["dgU_Size", i].Value = fileSize;
                //dgF["dgF_RmtFile", i].Value = $"{rmtNm}.{ext}";
            }
            dgU.ClearSelection();
        }

        private string GetFileSize(double byteCount)
        {
            string size = "0 Bytes";

            if (byteCount >= 1073741824.0)
                size = String.Format("{0:##.##}", byteCount / 1073741824.0) + " GB";
            else if (byteCount >= 1048576.0)
                size = String.Format("{0:##.##}", byteCount / 1048576.0) + " MB";
            else if (byteCount >= 1024.0)
                size = String.Format("{0:##.##}", byteCount / 1024.0) + " KB";
            else if (byteCount > 0 && byteCount < 1024.0)
                size = byteCount.ToString() + " Bytes";

            return size;
        }

        private void btnRmvFile_Click(object sender, EventArgs e)
        {
            dgU.SelectedRows.Clear();
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            string fullNm, fileNm, rmtNm, ext, guid, jsFile = string.Empty;
            string sql, title, msg, msgId, refId = "", refTid;
            
            List<string> listRecv = new List<string>();

            Control[] flps = new Control[] { flpA, flpB, flpC, flpD, flpE, flpF };
            foreach (Control flp in flps)
            {
                foreach (Control ctrl in flp.Controls)
                {
                    CheckBox chkMember = ctrl as CheckBox;
                    if (chkMember.Checked)
                    {
                        listRecv.Add(chkMember.Name.Replace("chkStf_", string.Empty));
                    }
                }
            }

            title = txtSendTitle.Text.Trim();
            msg = txtSendMsg.Text.Trim();
            refTid = txtRefTids.Text.Trim();

            if (listRecv.Count == 0)
            {
                MessageBox.Show("받는 사람을 선택 해 주세요.");
                return;
            }
            if (title == String.Empty)
            {
                MessageBox.Show("제목을 입력 해 주세요.");
                return;
            }
            if (msg == String.Empty)
            {
                MessageBox.Show("내용을 입력 해 주세요.");
                return;
            }

            btnSend.Enabled = false;

            if (listRecv.Count > 1)
            {
                List<string> listRef = listRecv.ToList();
                listRef.Remove(myId);
                refId = string.Join(",", listRef.ToArray());
            }

            //파일 업로드 먼저 처리
            if (dgU.Rows.Count == 0) goto EXIT_ATTACH;

            var jaFile = new JArray();
            foreach (DataGridViewRow row in dgU.Rows)
            {
                fullNm = row.Cells["dgU_LocFile"].Value.ToString();
                FileInfo fi = new FileInfo(fullNm);
                fileNm = fi.Name;
                ext = fi.Extension;
                guid = Guid.NewGuid().ToString();
                rmtNm = $"{guid}{ext}";

                bool upRslt = ftp1.Upload(fullNm, rmtNm);
                if (!upRslt)
                {
                    ftp1.Upload(fullNm, rmtNm);
                }

                var obj = new JObject();
                obj.Add("orgnNm", fileNm);
                obj.Add("saveNm", rmtNm);
                obj.Add("size", row.Cells["dgU_Size"].Value.ToString());
                jaFile.Add(obj);
            }
            jsFile = (jaFile.Count > 0) ? jaFile.ToString() : string.Empty;
            //MessageBox.Show(jsFile);

        EXIT_ATTACH:

            msgId = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            //MessageBox.Show(msgId);

            db.Open();
            foreach (string rid in listRecv)
            {
                sql = $"insert into db_tank.tz_note set msg_id='{msgId}', sid='{myId}', rid='{rid}', title='{title}', msg='{msg}', attach='{jsFile}', ref_tid='{refTid}', ref_id='{refId}', sdtm=now()";
                db.ExeQry(sql);
            }
            db.Close();            
            btnSend.Enabled = true;
                        
            MessageBox.Show("발송 되었습니다.");

            ui.FormClear(tabSend);
            rdoSend.Checked = true;
            btnSrch_Click(null, null);
        }

        private void btnDel_Click(object sender, EventArgs e)
        {
            string idx, sql;

            idx = lblNoteIdx.Text;
            if (idx == string.Empty) return;

            if (MessageBox.Show("쪽지를 삭제 하시겠습니까?", "쪽지 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No) return;

            if (rdoRecv.Checked)
            {
                sql = $"update db_tank.tz_note set rdel=1 where idx={idx}";
            }
            else
            {
                sql = $"update db_tank.tz_note set sdel=1 where idx={idx}";
            }

            db.Open();
            db.ExeQry(sql);
            db.Close();

            MessageBox.Show("삭제 되었습니다.");
            btnSrch_Click(null, null);
        }

        private void lnkCaMgmt_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string tid;
            tid = txtRecvTids.Text.Trim();

            if (string.IsNullOrEmpty(tid))
            {
                MessageBox.Show("관련된 물건이 없습니다.");
                return;
            }

            wfCaMgmt caMgmt = new wfCaMgmt() { Owner = this };
            caMgmt.StartPosition = FormStartPosition.CenterScreen;
            caMgmt.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            caMgmt.txtSrchTid.Text = tid;
            caMgmt.btnSrch_Click(null, null);
            caMgmt.Show();
        }
    }
}
