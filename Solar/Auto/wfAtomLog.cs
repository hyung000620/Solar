using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Solar.Auto
{
    public partial class wfAtomLog : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();

        DataTable dtAtomCd;

        public wfAtomLog()
        {
            InitializeComponent();

            ui.DgSetRead(dg);
            //dg.CellClick += (s, e) => { Dg_SelectionChanged(null, null); };

            dtAtomCd = db.ExeDt("select cd, nm from db_tank.tx_cd_atom order by cd");
            DataRow row = dtAtomCd.NewRow();
            row["cd"] = "0";
            row["nm"] = "-선택-";
            dtAtomCd.Rows.InsertAt(row, 0);
            cbxPrcDvsn.DataSource = dtAtomCd;
            cbxPrcDvsn.DisplayMember = "nm";
            cbxPrcDvsn.ValueMember = "cd";

            cbxVM.SelectedIndex = 0;
            dtpBgn.Checked = true;
            dtpEnd.Checked = true;
        }

        private void btnSrch_Click(object sender, EventArgs e)
        {
            int i, no = 0;
            string sql, cdtn, prcNm;
            
            dg.SelectionChanged -= Dg_SelectionChanged;
            dg.Rows.Clear();
            lsv.Items.Clear();
            
            cdtn = "1";
            if (cbxVM.SelectedIndex > 0) cdtn += " and vm_nm='" + cbxVM.Text + "'";
            if (cbxPrcDvsn.SelectedIndex > 0) cdtn += " and prc_cd=" + cbxPrcDvsn.SelectedValue.ToString();
            if (dtpBgn.Checked) cdtn += " and bgn_dtm >= '" + string.Format("{0} 00:00:00", dtpBgn.Value.ToShortDateString()) + "'";
            if (dtpEnd.Checked) cdtn += " and bgn_dtm <= '" + string.Format("{0} 23:59:59", dtpEnd.Value.ToShortDateString()) + "'";

            sql = "select idx, prc_no, prc_cd, vm_nm, bgn_dtm, end_dtm, inet_ntoa(ip) as ipAdrs from db_tank.tx_atom where " + cdtn + " order by idx desc limit 0, 300";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                var xRow = dtAtomCd.Rows.Cast<DataRow>().Where(t => t["cd"].ToString() == dr["prc_cd"].ToString()).SingleOrDefault();
                prcNm = (xRow == null || dr["prc_cd"].ToString() == "0") ? string.Empty : xRow.Field<string>("nm");

                i = dg.Rows.Add();
                no = i + 1;
                dg["dg_No", i].Value = no;
                //dg["dg_PrcNo", i].Value = dr["prc_no"];
                dg["dg_PrcNm", i].Value = prcNm;
                dg["dg_VmNm", i].Value = dr["vm_nm"];
                dg["dg_IP", i].Value = dr["ipAdrs"];
                dg["dg_BgnDtm", i].Value = string.Format("{0:MM.dd (ddd) HH:mm:ss}", dr["bgn_dtm"]);
                dg["dg_EndDtm", i].Value = string.Format("{0:MM.dd (ddd) HH:mm:ss}", dr["end_dtm"]);
                if (dr["end_dtm"].ToString().Contains("0001-01-01"))
                {
                    dg["dg_EndDtm", i].Value = string.Empty;
                    dg["dg_RunTm", i].Value = string.Empty;
                    dg.Rows[i].DefaultCellStyle.BackColor = Color.LightGreen;
                }
                else
                {
                    TimeSpan diff = Convert.ToDateTime(dr["end_dtm"]) - Convert.ToDateTime(dr["bgn_dtm"]);
                    dg["dg_RunTm", i].Value = string.Format("{0}.{1}.{2}", diff.Hours, diff.Minutes.ToString().PadLeft(2, '0'), diff.Seconds.ToString().PadLeft(2, '0'));
                }
                dg["dg_Idx", i].Value = dr["idx"];
            }
            dr.Close();
            db.Close();

            dg.ClearSelection();
            dg.SelectionChanged += Dg_SelectionChanged;

            if (no == 0)
            {
                MessageBox.Show("검색 결과가 없습니다.");
            }
        }

        private void Dg_SelectionChanged(object sender, EventArgs e)
        {
            int i;
            string sql, note = string.Empty;

            if (dg.CurrentRow == null) return;
            i = dg.CurrentRow.Index;

            lsv.Items.Clear();

            db.Open();
            sql = "select note from db_tank.tx_atom where idx=" + dg["dg_Idx", i].Value.ToString();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            if (dr.HasRows) note = dr["note"].ToString();
            dr.Close();
            db.Close();

            if (note == string.Empty) return;
            MatchCollection mc = Regex.Matches(note, @"(\d+:\d+:\d+) (.*)", RegexOptions.Multiline);
            foreach (Match m in mc)
            {
                ListViewItem item = new ListViewItem(new string[] { m.Groups[1].Value, m.Groups[2].Value });
                lsv.Items.Add(item);
            }
        }
    }
}