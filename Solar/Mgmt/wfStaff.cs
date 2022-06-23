using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MySql.Data.MySqlClient;

namespace Solar.Mgmt
{
    public partial class wfStaff : Form
    {
        UiUtil ui = new UiUtil();
        DbUtil db = new DbUtil();
        Dictionary<int, string> dict = new Dictionary<int, string>();
        
        public wfStaff()
        {
            InitializeComponent();

            ui.DgSetRead(dg);
            dg.CellClick += (s, e) => { dg_SelectionChanged(null, null); };
            cbxState.SelectedIndex= 0;
            cbxTeam.SelectedIndex = 0;
        }
        private void btnSave_Click(object sender, EventArgs e)
        {
            string sql, usrId, usrNm, pwd, usrIdx, level, cvp;

            usrIdx = txtUsrIdx.Text;
            usrNm = txtUsrNm.Text.Trim();
            usrId = txtUsrId.Text.Trim();
            level = txtLevel.Text.Trim();
            pwd = txtPwd.Text.Trim();

            if(usrNm == String.Empty)
            {
                MessageBox.Show("직원명을 입력해주세요.");
                return;
            }

            if (usrId == String.Empty)
            {
                MessageBox.Show("아이디를 입력해주세요.");
                return;
            }



            List<MySqlParameter> sp = new List<MySqlParameter>();
            
            cvp = "name=@nm, id=@id, level=@level";

            if (usrIdx == string.Empty || chkPwdMdfy.Checked)
            {
                cvp += ", passwd=sha2('" + pwd + "',256)";
            }

            if (usrIdx == string.Empty)
            {
                cvp += ",staff_menu='1010|1011|1012|1013|1015|1016|1019|1020|1023|1030|1110|1111|1112|1113|1210|1310|1510|1511|1512|1513|1912|1914|1916|1917|1918|1919|1922|1923|1924|1927|1928|1929|1930|2010|2110|2212|2213|2214|2215|2410|2510', start_dt=CURDATE()";
                sql = "insert into db_tank.tz_staff set " + cvp;
            }
            else
            {
                sql = "update db_tank.tz_staff set " + cvp + " where idx=" + usrIdx;
            }

            sp.Add(new MySqlParameter("@nm", usrNm));
            sp.Add(new MySqlParameter("@id", usrId));
            sp.Add(new MySqlParameter("@level", level));

            db.Open();
            db.ExeQry(sql, sp);
            sp.Clear();
            db.Close();

            MessageBox.Show("저장 되었습니다.");

            ui.FormClear(tabInfo);
            btnSrch_Click(null, null);
        }

        private void btnSrch_Click(object sender, EventArgs e)
        {
            int i = 0, rowCnt = 0;
            string sql, cond;
            cond = "1 ";
            if (cbxState.SelectedIndex == 1)
            {
                cond += "and level != 0";
            } 
            else if (cbxState.SelectedIndex == 2)
            {
                cond += "and level = 0";
            }
            dg.SelectionChanged -= dg_SelectionChanged;
            dg.Rows.Clear();

            sql = "select * from db_tank.tz_staff where " + cond + " order by idx desc";
            db.Open();
            rowCnt = Convert.ToInt32(db.RowCnt(sql));
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                i = dg.Rows.Add();
                dg["dg_No", i].Value = rowCnt;
                dg["dg_Nm", i].Value = dr["name"];
                dg["dg_Id", i].Value = dr["id"];
                dg["dg_Level", i].Value = dr["level"];
                dg["dg_Idx", i].Value = dr["idx"];
                if (Convert.ToInt32(dr["level"])==0)
                {
                    dg["dg_State", i].Value = "퇴사";
                    dg.Rows[i].DefaultCellStyle.BackColor = Color.Gray;
                }
                else
                {
                    dg["dg_State", i].Value = "재직중";
                }
                rowCnt--;
            }
            dr.Close();
            db.Close();

            dg.ClearSelection();
            dg.SelectionChanged += dg_SelectionChanged;
        }

        private void dg_SelectionChanged(object sender, EventArgs e)
        {
            int i = 0;
            string sql, idx;

            ui.FormClear(tabInfo);
            if (dg.CurrentRow == null) return;
            i = dg.CurrentRow.Index;

            idx = dg["dg_Idx", i].Value.ToString();
            sql = "select * from db_tank.tz_staff where idx=" + idx;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            txtUsrIdx.Text = idx;
            txtUsrNm.Text = dr["name"].ToString();
            txtUsrId.Text = dr["id"].ToString();
            txtLevel.Text = dr["level"].ToString();
            dr.Close();
            db.Close();
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            ui.FormClear(tabInfo);
        }

        private void btnLeave_Click(object sender, EventArgs e)
        {
            string sql, usrId;

            usrId = txtUsrId.Text.Trim();
            if (usrId.Length == 0)
            {
                MessageBox.Show("선택된 아이디가 없습니다.");
                return;
            }
            DialogResult result = MessageBox.Show("퇴사처리 하시겠습니까?", "", MessageBoxButtons.YesNo);
            if(result == DialogResult.Yes)
            {
                List<MySqlParameter> sp = new List<MySqlParameter>();

                sql = "update db_tank.tz_staff set level=0, staff_menu='', resign_dt=CURDATE(), team=0 where id=@id";

                sp.Add(new MySqlParameter("@id", usrId));

                db.Open();
                db.ExeQry(sql, sp);
                sp.Clear();
                db.Close();
                MessageBox.Show("퇴사처리 되었습니다.");
            }
            else
            {
                MessageBox.Show("취소되었습니다.");
                return;
            }


            ui.FormClear(tabInfo);
            btnSrch_Click(null, null);
        }
    }
}
