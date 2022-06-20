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

namespace Solar.Comn
{
    public partial class sfSrchAdrsCd : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();

        public string siNm = string.Empty;
        public string guNm = string.Empty;
        public string dnNm = string.Empty;
        public string riNm = string.Empty;

        public string siCd = string.Empty;
        public string guCd = string.Empty;
        public string dnCd = string.Empty;
        public string riCd = string.Empty;

        public string SelectedAddress1 = string.Empty;

        public sfSrchAdrsCd()
        {
            InitializeComponent();

            init();
        }

        private void init()
        {
            ui.DgSetRead(dg, 0);
        }

        private void btnSrch_Click(object sender, EventArgs e)
        {
            int i = 0, No = 0;
            string sql = string.Empty, addr = string.Empty, addr2 = string.Empty;
            dg.Rows.Clear();

            List<MySqlParameter> sp = new List<MySqlParameter>();

            sql = "SELECT * FROM tx_cd_adrs WHERE hide=0 AND dn_cd > 0 AND (gu_nm LIKE @str OR dn_nm LIKE @str OR ri_nm LIKE @str) GROUP BY si_cd, gu_cd, dn_cd, ri_cd ORDER BY si_nm, gu_nm, dn_nm, ri_nm";            
            sp.Add(new MySqlParameter("@str", "%" + txtSrchAdrs.Text.Trim() + "%"));
            try
            {
                db.Open();
                MySqlDataReader dr = db.ExeRdr(sql, sp);
                while (dr.Read())
                {
                    i = dg.Rows.Add();
                    No = i + 1;
                    addr = dr["si_nm"].ToString() + " " + dr["gu_nm"].ToString() + " " + dr["dn_nm"].ToString() + " " + dr["ri_nm"].ToString();
                    dg["dgNo", i].Value = No;
                    dg["dgRetAdrs", i].Value = addr.Replace("  ", " ").Trim();
                    dg["dgSiNm", i].Value = dr["si_nm"];
                    dg["dgGuNm", i].Value = dr["gu_nm"];
                    dg["dgDnNm", i].Value = dr["dn_nm"];
                    dg["dgRiNm", i].Value = dr["ri_nm"];

                    dg["dgSiCd", i].Value = dr["si_cd"];
                    dg["dgGuCd", i].Value = dr["gu_cd"];
                    dg["dgDnCd", i].Value = dr["dn_cd"];
                    dg["dgRiCd", i].Value = dr["ri_cd"];
                }
                if (No == 0)
                {
                    MessageBox.Show("검색된 주소가 없습니다!");
                    txtSrchAdrs.Select();
                }
                else
                {
                    dg.Focus();
                }
                dr.Close();
                sp.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                db.Close();
            }
            dg.ClearSelection();
        }


        private void txtSchAddr_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter) btnSrch_Click(null, null);
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

        /// <summary>
        /// 결과 선택
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dg_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            SelectedAddress1 = dg["dgRetAdrs", e.RowIndex].Value.ToString();
            siNm = dg["dgSiNm", e.RowIndex].Value.ToString();
            guNm = dg["dgGuNm", e.RowIndex].Value.ToString();
            dnNm = dg["dgDnNm", e.RowIndex].Value.ToString();
            riNm = dg["dgRiNm", e.RowIndex].Value.ToString();

            siCd = dg["dgSiCd", e.RowIndex].Value.ToString();
            guCd = dg["dgGuCd", e.RowIndex].Value.ToString();
            dnCd = dg["dgDnCd", e.RowIndex].Value.ToString();
            riCd = dg["dgRiCd", e.RowIndex].Value.ToString();
            this.DialogResult = DialogResult.OK;
        }
    }
}
