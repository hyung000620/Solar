using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Solar.PA;

namespace Solar.CA
{
    public partial class sfAptCd : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();

        DataTable dtCat;
        Dictionary<int, string> matchDvsn = new Dictionary<int, string>();

        public sfAptCd()
        {
            InitializeComponent();

            init();
        }

        private void init()
        {
            string sql;

            ui.DgSetRead(dg, 0);

            DataTable dtSidoCd = new DataTable();
            dtSidoCd.Columns.Add("siCd");
            dtSidoCd.Columns.Add("siNm");
            dtSidoCd.Rows.Add(0, "-시/도-");
            dtSidoCd.Rows.Add(11, "서울");
            dtSidoCd.Rows.Add(26, "부산");
            dtSidoCd.Rows.Add(27, "대구");
            dtSidoCd.Rows.Add(28, "인천");
            dtSidoCd.Rows.Add(29, "광주");
            dtSidoCd.Rows.Add(30, "대전");
            dtSidoCd.Rows.Add(31, "울산");
            dtSidoCd.Rows.Add(36, "세종");
            dtSidoCd.Rows.Add(41, "경기");
            dtSidoCd.Rows.Add(42, "강원");
            dtSidoCd.Rows.Add(43, "충북");
            dtSidoCd.Rows.Add(44, "충남");
            dtSidoCd.Rows.Add(45, "전북");
            dtSidoCd.Rows.Add(46, "전남");
            dtSidoCd.Rows.Add(47, "경북");
            dtSidoCd.Rows.Add(48, "경남");
            dtSidoCd.Rows.Add(50, "제주");
            cbxSi.DataSource = dtSidoCd;
            cbxSi.DisplayMember = "siNm";
            cbxSi.ValueMember = "siCd";
            cbxSi.SelectedIndexChanged += CbxAdrsCd_SelectedIndexChanged;
            cbxGu.SelectedIndexChanged += CbxAdrsCd_SelectedIndexChanged;

            sql = "select _gd_cd, cat3_nm from ta_cd_cat where _gd_cd > 0";
            dtCat = db.ExeDt(sql);

            matchDvsn.Add(0, "분류전");
            matchDvsn.Add(1, "텍스트");
            matchDvsn.Add(2, "이미지");
            matchDvsn.Add(3, "텍스트+이미지");

            this.Shown += SfAptCd_Shown;
        }

        private void SfAptCd_Shown(object sender, EventArgs e)
        {
            string prntNm = this.Owner.Name;
            if (prntNm == "wfCaMgmt")
            {
                wfCaMgmt prnt = (wfCaMgmt)this.Owner;
                cbxSi.SelectedValue = prnt.txtSiCd.Text;
                cbxGu.SelectedValue = prnt.txtGuCd.Text;
                cbxDn.SelectedValue = prnt.txtDnCd.Text;
            }
            else if (prntNm == "wfPaMgmt")
            {
                wfPaMgmt prnt = (wfPaMgmt)this.Owner;
                cbxSi.SelectedValue = prnt.txtSiCd.Text;
                cbxGu.SelectedValue = prnt.txtGuCd.Text;
                cbxDn.SelectedValue = prnt.txtDnCd.Text;
            }
        }

        /// <summary>
        /// Mouse Over 색상 반전
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dg_CellMouseMove(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex > -1)
            {
                dg.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.LightGreen;
            }
        }

        /// <summary>
        /// Mouse Out 기본 색상
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dg_CellMouseLeave(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex > -1)
            {
                dg.Rows[e.RowIndex].DefaultCellStyle.BackColor = dg.DefaultCellStyle.BackColor;
            }
        }

        private void CbxAdrsCd_SelectedIndexChanged(object sender, EventArgs e)
        {
            string sql;
            ComboBox cbx = ((ComboBox)sender);

            if (cbx == cbxSi)
            {
                if (cbxSi.SelectedValue.ToString() == "36")
                {
                    //세종시
                    sql = "select gu_nm, gu_cd from tx_cd_adrs where si_cd=36 and gu_cd=110 and dn_cd > 0 limit 1";
                }
                else
                {
                    sql = "select gu_nm, gu_cd from tx_cd_adrs where si_cd=" + cbxSi.SelectedValue.ToString() + " and gu_cd > 0 and dn_cd=0 and hide=0 order by gu_nm";
                }                
                DataTable dtGu = db.ExeDt(sql);
                DataRow row = dtGu.NewRow();
                row["gu_nm"] = "-시/구/군-";
                row["gu_cd"] = 0;
                dtGu.Rows.InsertAt(row, 0);

                cbxGu.DataSource = dtGu;
                cbxGu.DisplayMember = "gu_nm";
                cbxGu.ValueMember = "gu_cd";
                cbxGu.SelectedValue = 0;
            }

            if (cbx == cbxGu)
            {
                //if (cbxGu.SelectedValue.ToString() == "System.Data.DataRowView") return;
                DataRowView rowView = cbxGu.SelectedItem as DataRowView;

                sql = "select dn_nm, dn_cd from tx_cd_adrs where si_cd=" + cbxSi.SelectedValue.ToString() + " and gu_cd=" + rowView["gu_cd"].ToString() + " and dn_cd > 0 and ri_cd=0 and hide=0 order by dn_nm";
                DataTable dtDn = db.ExeDt(sql);
                DataRow row = dtDn.NewRow();
                row["dn_nm"] = "-읍/면/동-";
                row["dn_cd"] = 0;
                dtDn.Rows.InsertAt(row, 0);

                cbxDn.DataSource = dtDn;
                cbxDn.DisplayMember = "dn_nm";
                cbxDn.ValueMember = "dn_cd";
                cbxDn.SelectedValue = 0;
            }
        }

        private void btnSrch_Click(object sender, EventArgs e)
        {
            int i = 0;
            string sql, cdtn, cat, dvsn;

            dg.Rows.Clear();

            if (cbxSi.SelectedIndex == 0 || cbxGu.SelectedIndex == 0)
            {
                MessageBox.Show("검색할 지역을 선택 해 주세요.");
                return;
            }

            cdtn = "si_key=" + cbxSi.SelectedValue.ToString() + " and gu_key=" + cbxGu.SelectedValue.ToString();
            if (cbxDn.SelectedIndex > 0)
            {
                cdtn += " and dong_key=" + cbxDn.SelectedValue.ToString();
            }
            cdtn += " and match_type in (1,3)";
            sql = "select apt_code, dj_name, pd_type, concat(sido,' ',gugun,' ',dong,' ',ri,' ',bunji) as adrs, match_type from tx_apt where " + cdtn + " order by dj_name";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                DataRow xRow = dtCat.Select("_gd_cd='" + dr["pd_type"].ToString() + "'").FirstOrDefault();
                cat = (xRow == null) ? string.Empty : xRow["cat3_nm"].ToString();
                dvsn = matchDvsn[Convert.ToInt32(dr["match_type"])];

                i = dg.Rows.Add();
                dg["dg_No", i].Value = i + 1;
                dg["dg_AptCd", i].Value = dr["apt_code"];
                dg["dg_Nm", i].Value = dr["dj_name"];
                dg["dg_Cat", i].Value = cat;
                dg["dg_Adrs", i].Value = dr["adrs"];
                dg["dg_Dvsn", i].Value = dvsn;
            }
            dr.Close();
            db.Close();
            dg.ClearSelection();
        }

        private void dg_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            //MessageBox.Show(this.Owner.Name);
            if (e.RowIndex == -1) return;

            string prntNm = this.Owner.Name;
            if (prntNm == "wfCaMgmt")
            {
                wfCaMgmt prnt = (wfCaMgmt)this.Owner;
                prnt.txtAptCd.Text = dg["dg_AptCd", e.RowIndex].Value.ToString();
            }
            else
            {
                wfPaMgmt prnt = (wfPaMgmt)this.Owner;
                prnt.txtAptCd.Text = dg["dg_AptCd", e.RowIndex].Value.ToString();
            }

            this.Close();
        }
    }
}
