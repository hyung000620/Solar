using MySql.Data.MySqlClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Solar.CA
{
    public partial class wfLandPlan : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        AuctCd auctCd = new AuctCd();
        NetUtil net = new NetUtil();

        private decimal[] multiBldgArr; //집합건물

        public wfLandPlan()
        {
            InitializeComponent();
            init();
        }

        private void init()
        {
            string sql;
            ui.DgSetRead(dg);
            ui.DgSetRead(dgR);

            multiBldgArr = auctCd.multiBldgArr;

            //sql = "select prps_cd, prps_nm from ta_cd_prps where req=1";
            sql = "select prps_cd, prps_nm, level1, level2 from tx_cd_use where level3 > 0 order by level1, level2, level3";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                dgR.Rows.Add(false, dr["prps_nm"], dr["prps_cd"], dr["level1"], dr["level2"]);
            }
            dr.Close();
            db.Close();
            dgR.ClearSelection();
        }

        private void btnSrch_Click(object sender, EventArgs e)
        {
            string sql, multiBldg;

            dg.DataSource = null;
            dg.SelectionChanged -= Dg_SelectionChanged;

            multiBldg = "0," + string.Join(",", Array.ConvertAll(multiBldgArr, x => x.ToString()));
            //MessageBox.Show(multiBldg);
            //sql = "select P.tid, sn1, sn2, pn, adrs, L.idx, crt, spt, prps_nm from ta_list P , ta_land L where P.tid=L.tid and cat3 not in (" + multiBldg + ") and ";
            sql = "select P.tid, sn1, sn2, pn, S.adrs, L.idx, spt, prps_nm from ta_list P , ta_land L , ta_ls S where P.tid=L.tid and L.tid=S.tid and L.ls_no=S.no and cat3 not in (" + multiBldg + ") and ";
            if (chkPid.Checked && txtPid.Text != string.Empty)
            {
                sql += "L.tid='" + txtPid.Text + "'";
            }
            else
            {
                sql += "(plan_prc=2 or (plan_prc=0 and price_prc=2))";
            }
            sql += " group by L.idx order by spt, sn1, sn2, pn";

            DataTable dt = db.ExeDt(sql);
            dg.DataSource = dt;
            dg.ClearSelection();
            dgR.ClearSelection();

            if (dt.Rows.Count == 0)
            {
                MessageBox.Show("미처리된 물건이 없습니다.");
                return;
            }

            dg.SelectionChanged += Dg_SelectionChanged;
        }

        private void Dg_SelectionChanged(object sender, EventArgs e)
        {
            int rowIdx = 0, tid = 0;
            string url, sql, idx, pnu = string.Empty;
            string selSido, selSgg, selUmd, selRi, landGbn, bobn, bubn;
            string sn, sn1, sn2, spt;

            if (dg.CurrentRow == null) return;
            dgR.ClearSelection();

            rowIdx = dg.CurrentRow.Index;
            DataGridViewRow row = dg.Rows[rowIdx];
            tid = Convert.ToInt32(row.Cells["tid"].Value);
            idx = row.Cells["idx"].Value.ToString();

            sql = "select prps_cd, prps_nm, pnu, adrs from ta_land D , ta_ls L where D.tid=L.tid and D.ls_no=L.no and D.idx='" + idx + "' limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            string[] codeArr = dr["prps_cd"].ToString().Split(new char[] { ',' });
            pnu = dr["pnu"].ToString();
            dr.Close();
            db.Close();

            foreach (DataGridViewRow r in dgR.Rows)
            {
                if (codeArr.Contains(r.Cells["dgR_Code"].Value.ToString()))
                {
                    r.Cells["dgR_Chk"].Value = "1";
                    r.DefaultCellStyle.BackColor = Color.LightGreen;
                }
                else
                {
                    r.Cells["dgR_Chk"].Value = "0";
                    r.DefaultCellStyle.BackColor = Color.White;
                }
            }

            if (pnu == string.Empty)
            {
                MessageBox.Show("PNU 코드가 없습니다.\r\n수동으로 주소를 검색하세요~");
                url = "http://www.eum.go.kr";
            }
            else
            {
                selSido = pnu.Substring(0, 2);
                selSgg = pnu.Substring(2, 3);
                //selUmd = pnu.Substring(5, 3).PadLeft(4, '0');
                selUmd = pnu.Substring(4, 4);
                selRi = pnu.Substring(8, 2);
                landGbn = pnu[10].ToString();
                bobn = (Convert.ToDecimal(pnu.Substring(11, 4)) * 1).ToString();
                bubn = (Convert.ToDecimal(pnu.Substring(15)) * 1).ToString();                
                url= "http://www.eum.go.kr/web/ar/lu/luLandDet.jsp?mode?selGbn=umd&isNoScr=script&s_type=1&pnu=" +
                    pnu + "&mode=search&landGbnExt=1&add=land&selSido=" + selSido + "&selSgg=" + selSgg + "&selUmd=" + selUmd + "&selRi=" + selRi + "&landGbn=" + landGbn + "&bobn=" + bobn + "&bubn=" + bubn;
            }
            wbr.Navigate(url);

            //감정평가서 로드
            sn1 = row.Cells["sn1"].Value.ToString();
            sn2 = row.Cells["sn2"].Value.ToString();
            spt = row.Cells["spt"].Value.ToString();

            string tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
            sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));
            sql = "select * from " + tbl + " where spt=" + spt + " and sn='" + sn + "' and ctgr='AF' order by idx limit 1";
            DataTable dt = db.ExeDt(sql);

            if (dt.Rows.Count == 0)
            {
                MessageBox.Show("수집된 감정평가서가 없습니다.");
                return;
            }

            url = string.Format(Properties.Settings.Default.myWeb + "FILE/CA/AF/{0}/{1}/{2}", spt, sn1, dt.Rows[0]["file"]);
            axAcroPDF1.src = url;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            int rowIdx = 0;
            string code = "", name = "", sql = "", idx = "";

            if (dg.CurrentRow == null) return;

            ArrayList alCode = new ArrayList();
            ArrayList alName = new ArrayList();

            foreach (DataGridViewRow row in dgR.Rows)
            {
                if (row.Cells["dgR_Chk"].Value.ToString() == "1")
                {
                    alCode.Add(row.Cells["dgR_Code"].Value);
                    alName.Add(row.Cells["dgR_Name"].Value);
                }
            }
            if (alCode.Count == 0)
            {
                if (MessageBox.Show("해당되는 지역규제 항목이 없습니까?", "항목확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No)
                {
                    return;
                }
            }

            code = String.Join(",", alCode.ToArray());
            name = String.Join(",", alName.ToArray());

            rowIdx = dg.CurrentRow.Index;
            idx = dg["idx", rowIdx].Value.ToString();
            /*
            if (dg["prps_nm", rowIdx].Value.ToString().Trim() == string.Empty)
            {
                sql = "update ta_land set plan_prc=3, prps_cd='" + code + "', prps_nm='" + name + "' where idx='" + idx + "'";
            }
            else
            {
                sql = "update ta_land set plan_prc=3, prps_cd='" + code + "' where idx='" + idx + "'";
            }
            */
            //2021-12-29 수정 지현,민영
            sql = "update ta_land set plan_prc=3, prps_cd='" + code + "', prps_nm='" + name + "' where idx='" + idx + "'";
            db.Open();
            db.ExeQry(sql);
            db.Close();

            MessageBox.Show("저장 되었습니다.");
        }

        private void dgR_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != 0) return;

            DataGridViewCheckBoxCell cell = (DataGridViewCheckBoxCell)dgR[0, e.RowIndex];
            if (cell.Value == cell.TrueValue)
            {
                dgR.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.White;
            }
            else
            {
                dgR.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.LightGreen;
            }
        }
    }
}
