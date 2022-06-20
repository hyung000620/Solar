using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using MySql.Data.MySqlClient;

namespace Solar.CA
{
    public partial class wfRgstAnaly : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();

        RgstAnalyNew RA = new RgstAnalyNew();

        DataTable dtA, dtB;
        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        public wfRgstAnaly()
        {
            InitializeComponent();

            init();
        }

        private void init()
        {
            ui.DgSetRead(dgF, 0);
            ui.DgSetRead(dgA, 0);
            ui.DgSetRead(dgB, 0);

            dgA.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            //dgA.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            //dgA.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            //dgA.SelectionMode = DataGridViewSelectionMode.CellSelect;

            dgB.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            //dgB.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            //dgB.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            //dgB.SelectionMode = DataGridViewSelectionMode.CellSelect;

            dtA = RA.dtA;  //분석-전
            dtB = RA.dtB;  //분석-후

            foreach (DataColumn col in dtA.Columns)
            {
                DataGridViewColumn dgc = new DataGridViewTextBoxColumn();
                dgc.Name = "dgA_" + col.ColumnName;
                dgc.HeaderText = col.ColumnName;
                dgc.DataPropertyName = col.ColumnName;
                dgc.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                dgA.Columns.Add(dgc);
            }
            dgA.Columns["dgA_sect"].Width = 50;
            dgA.Columns["dgA_rank"].Width = 50;
            dgA.Columns["dgA_prps"].Width = 180;
            dgA.Columns["dgA_rcpt"].Width = 180;
            dgA.Columns["dgA_resn"].Width = 180;
            dgA.Columns["dgA_prsn"].Width = 400;

            foreach (DataColumn col in dtB.Columns)
            {
                DataGridViewColumn dgc = new DataGridViewTextBoxColumn();
                dgc.Name = "dgB_" + col.ColumnName;
                dgc.HeaderText = col.ColumnName;
                dgc.DataPropertyName = col.ColumnName;
                dgc.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                dgB.Columns.Add(dgc);
            }
            Font f8 = new Font("돋움", 8);
            dgB.Columns["dgB_sect"].Width = 50;
            dgB.Columns["dgB_rank"].Width = 50;
            dgB.Columns["dgB_rgCd"].Width = 40;
            dgB.Columns["dgB_rcDt"].Width = 70;
            dgB.Columns["dgB_rcNo"].Width = 50;
            dgB.Columns["dgB_cAmt"].Width = 80;
            dgB.Columns["dgB_shrStr"].Width = 80;
            dgB.Columns["dgB_adrs"].Width = 200;
            dgB.Columns["dgB_mvDt"].Width = 70;
            dgB.Columns["dgB_bzDt"].Width = 70;
            dgB.Columns["dgB_fxDt"].Width = 70;
            dgB.Columns["dgB_bgnDt"].Width = 70;
            dgB.Columns["dgB_endDt"].Width = 70;
            dgB.Columns["dgB_ekey"].Width = 40;
            dgB.Columns["dgB_siCd"].Width = 40;
            dgB.Columns["dgB_guCd"].Width = 40;
            dgB.Columns["dgB_dnCd"].Width = 40;
            dgB.Columns["dgB_hide"].Width = 40;
            dgB.Columns["dgB_rpt"].Width = 40;
            dgB.Columns["dgB_del"].Width = 40;
            dgB.Columns["dgB_rgCd"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            dgB.Columns["dgB_rcNo"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            dgB.Columns["dgB_cAmt"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            dgB.Columns["dgB_adrs"].DefaultCellStyle.Font = f8;
            dgB.Columns["dgB_brch"].DefaultCellStyle.Font = f8;
            dgB.Columns["dgB_note"].DefaultCellStyle.Font = f8;
        }

        private void btnOpenFiles_Click(object sender, EventArgs e)
        {
            int n = 0;
            string tid = "", sn, sn1 = string.Empty, sn2 = string.Empty, pn, sql, today;
            dgF.SelectionChanged -= DgF_SelectionChanged;
            dgF.Rows.Clear();

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "pdf 문서(*.pdf)|*.pdf";
            ofd.Multiselect = true;

            if (ofd.ShowDialog() == DialogResult.Cancel) return;

            Regex rx1 = new Regex(@"[DAB]{2}\-(\d{4})\-(\d{4})(\d{6})\-(\d{4})\-\d{2}.pdf", rxOptM);
            Regex rx2 = new Regex(@"(\d+)_(\d+)\.pdf", rxOptM);
            Regex rx3 = new Regex(@"(\d{14})\.pdf", rxOptM);

            db.Open();
            foreach (string fileNm in ofd.FileNames)
            {
                n++;
                pn = String.Empty;
                Match match1 = rx1.Match(fileNm);
                Match match2 = rx2.Match(fileNm);
                Match match3 = rx3.Match(fileNm);
                if (match1.Success)
                {
                    sn1 = match1.Groups[2].Value;
                    sn2 = match1.Groups[3].Value;
                    sql = "select tid from ta_list where spt='" + match1.Groups[1].Value + "' and sn1='" + sn1 + "' and sn2='" + sn2 + "' and pn='" + match1.Groups[4].Value + "' limit 1";
                    MySqlDataReader dr = db.ExeRdr(sql);
                    dr.Read();
                    if (dr.HasRows) tid = dr["tid"].ToString();
                    else tid = string.Empty;
                    dr.Close();
                }
                else if (match2.Success)
                {
                    tid = Regex.Match(fileNm, @"(\d+)_(\d+)\.pdf", rxOptM).Groups[1].Value;
                    sql = "select sn1, sn2, pn from ta_list where tid='" + tid + "'";
                    MySqlDataReader dr = db.ExeRdr(sql);
                    dr.Read();
                    if (dr.HasRows)
                    {
                        sn1 = dr["sn1"].ToString();
                        sn2 = dr["sn2"].ToString();
                        pn = dr["pn"].ToString();
                    }
                    else
                    {
                        sn1 = string.Empty;
                        sn2 = string.Empty;
                        pn = string.Empty;
                    }
                    dr.Close();
                }
                else if (match3.Success)
                {
                    sql = $"select * from db_tank.tx_rgst_auto R, db_main.ta_list L where R.tid=L.tid and wdt=curdate() and pin='{match3.Groups[1].Value}' and R.dvsn > 0";
                    MySqlDataReader dr = db.ExeRdr(sql);
                    dr.Read();
                    if (dr.HasRows)
                    {
                        tid = dr["tid"].ToString();
                        sn1 = dr["sn1"].ToString();
                        sn2 = dr["sn2"].ToString();
                        pn = dr["pn"].ToString();
                    }
                    else
                    {
                        tid = string.Empty;
                        sn1 = string.Empty;
                        sn2 = string.Empty;
                        pn = string.Empty;
                    }
                    dr.Close();
                }
                else continue;

                if (tid == string.Empty || sn1 == string.Empty || sn2 == string.Empty) continue;

                //sn = string.Format("{0}타경{1}", sn1, Convert.ToDecimal(sn2));
                sn = $"{sn1}-{sn2}";
                if (pn != String.Empty && pn != "0") sn += $"({pn})";
                dgF.Rows.Add(n, fileNm, tid, sn);
            }
            db.Close();
            dgF.ClearSelection();
            dgF.SelectionChanged += DgF_SelectionChanged;
        }

        private void DgF_SelectionChanged(object sender, EventArgs e)
        {
            dgF_CellClick(null, null);
        }

        private void dgF_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            DataGridViewRow row = dgF.CurrentRow;

            this.Cursor = Cursors.WaitCursor;
            
            string analyRslt = RA.Proc($"{row.Cells["dgF_Nm"].Value}");

            row.Cells["dgF_Creator"].Value = RA.pdfCreator;
            row.Cells["dgF_Dvsn"].Value = RA.rgstDvsn;
            row.Cells["dgF_IdNo"].Value = RA.rgstIdNo;            
            if (analyRslt == "success")
            {
                dgA.DataSource = RA.dtA;
                dgB.DataSource = RA.dtB;
                foreach (DataGridViewRow r in dgB.Rows)
                {
                    if (r.Cells["dgB_del"].Value.ToString() == "1") r.DefaultCellStyle.ForeColor = Color.LightGray;
                    if (r.Cells["dgB_ekey"].Value.ToString() == "1") r.DefaultCellStyle.BackColor = Color.LightGreen;
                }
                dgA.ClearSelection();
                dgB.ClearSelection();
            }
            else
            {
                row.Cells["dgF_Note"].Value = RA.analyRslt;
                dgA.DataSource = null;
                dgB.DataSource = null;
            }

            if (chkViewFile.Checked)
            {
                axAcroPDF1.src = row.Cells["dgF_Nm"].Value.ToString();
            }
            this.Cursor = Cursors.Default;
        }

        /// <summary>
        /// 단일 저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSave_Click(object sender, EventArgs e)
        {
            DataGridViewRow row = dgF.CurrentRow;
                        
            string analyRslt = RA.Proc($"{row.Cells["dgF_Nm"].Value}", true);

            row.Cells["dgF_Creator"].Value = RA.pdfCreator;
            row.Cells["dgF_Dvsn"].Value = RA.rgstDvsn;
            row.Cells["dgF_IdNo"].Value = RA.rgstIdNo;
            if (analyRslt == "success")
            {
                dgA.DataSource = RA.dtA;
                dgB.DataSource = RA.dtB;
                foreach (DataGridViewRow r in dgB.Rows)
                {
                    if (r.Cells["dgB_del"].Value.ToString() == "1") r.DefaultCellStyle.ForeColor = Color.LightGray;
                    if (r.Cells["dgB_ekey"].Value.ToString() == "1") r.DefaultCellStyle.BackColor = Color.LightGreen;
                }
                dgA.ClearSelection();
                dgB.ClearSelection();

                MessageBox.Show("저장 성공 ^^");
            }
            else
            {
                row.Cells["dgF_Note"].Value = RA.analyRslt;
                dgA.DataSource = null;
                dgB.DataSource = null;

                MessageBox.Show("저장 실패 TT");
            }            
        }

        /// <summary>
        /// 일괄 저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnBatProc_Click(object sender, EventArgs e)
        {
            string analyRslt;

            this.Cursor = Cursors.WaitCursor;
            foreach (DataGridViewRow r in dgF.Rows)
            {
                if (!r.Displayed) dgF.FirstDisplayedScrollingRowIndex = r.Index;

                analyRslt = RA.Proc($"{r.Cells["dgF_Nm"].Value}", true);
                r.Cells["dgF_Creator"].Value = RA.pdfCreator;
                r.Cells["dgF_Dvsn"].Value = RA.rgstDvsn;
                r.Cells["dgF_IdNo"].Value = RA.rgstIdNo;

                if (analyRslt == "success")
                {
                    r.DefaultCellStyle.BackColor = Color.LightGreen;
                }
                else
                {
                    r.Cells["dgF_Note"].Value = RA.analyRslt;
                    r.DefaultCellStyle.BackColor = Color.LightPink;
                }
            }
            this.Cursor= Cursors.Default;

            MessageBox.Show("처리 되었습니다.");
        }
    }
}
