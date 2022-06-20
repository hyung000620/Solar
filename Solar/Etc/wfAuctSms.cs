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

namespace Solar.Etc
{
    public partial class wfAuctSms : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        AuctCd auctCd = new AuctCd();

        DataTable dtCrtSpt;     //법원
        DataTable dtDptCd;      //계
        DataTable dtCatCd;        //물건 종류
        DataTable dtStateCd;    //진행 상태

        public wfAuctSms()
        {
            InitializeComponent();

            init();
        }

        private void init()
        {
            ui.DgSetRead(dg, 0);
            dg.MultiSelect = true;

            dtCrtSpt = db.ExeDt("select * from ta_cd_cs");

            //전체 법원별 계코드 DataTable
            dtDptCd = db.ExeDt("select C.ca_cd, crt_cd, spt_cd, cs_cd, dpt_cd, dpt_nm from ta_cd_cs C , ta_cd_dpt D where C.ca_cd=D.ca_cd order by dpt_cd");

            //진행 상태
            dtStateCd = db.ExeDt("select sta1_cd, sta2_cd, sta1_nm, sta2_nm from ta_cd_sta order by sta2_cd");

            //물건종별 및 토지 지목
            dtCatCd = db.ExeDt("select cat1_cd, cat2_cd, cat3_cd, cat2_nm, cat3_nm, hide, bldg_type from ta_cd_cat where cat3_cd > 0 order by cat3_cd");

            btnSrch.Select();
        }

        private void btnSrch_Click(object sender, EventArgs e)
        {
            int i = 0;
            string sql, csCd, state, cat, dpt, dpsl;

            dg.Rows.Clear();

            sql = "select L.tid, spt, dpt, sn1, sn2, pn, sta2, cat3, adrs, fb_cnt, apsl_amt, minb_amt, bid_dt, S.idx, S.dvsn, S.state, send, short_url, wdt, wtm from db_main.ta_list L , db_tank.tx_sms S where L.tid=S.tid and wdt=curdate()";
            if (rdoDvsn0.Checked) sql += " and S.dvsn=0";
            else if (rdoDvsn1.Checked) sql += " and S.dvsn=1";
            sql += " order by idx desc";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                csCd = dr["spt"].ToString();
                var xState = dtStateCd.Rows.Cast<DataRow>().Where(t => t["sta2_cd"].ToString() == dr["sta2"].ToString()).SingleOrDefault();
                state = (xState == null || dr["sta2"].ToString() == "0") ? string.Empty : xState.Field<string>("sta2_nm");
                var xCat = dtCatCd.Rows.Cast<DataRow>().Where(t => t["cat3_cd"].ToString() == dr["cat3"].ToString()).SingleOrDefault();
                cat = (xCat == null || dr["cat3"].ToString() == "0") ? string.Empty : xCat.Field<string>("cat3_nm");
                var xDpt = dtDptCd.Rows.Cast<DataRow>().Where(t => t["cs_cd"].ToString() == csCd && t["dpt_cd"].ToString() == dr["dpt"].ToString()).SingleOrDefault();
                dpt = (xDpt == null || dr["dpt"].ToString() == "0") ? string.Empty : xDpt.Field<string>("dpt_nm");
                
                i = dg.Rows.Add();
                dg["dg_No", i].Value = i + 1;
                dg["dg_Dvsn", i].Value = (dr["dvsn"].ToString() == "0") ? "상태변경" : "좌표신건";
                dg["dg_Tm", i].Value = dr["wtm"];
                dg["dg_StaMsg", i].Value = dr["state"];
                dg["dg_Send", i].Value = (Convert.ToBoolean(dr["send"])) ? "Y" : string.Empty;
                dg["dg_Tid", i].Value = dr["tid"];
                dg["dg_CS", i].Value = auctCd.FindCsNm(csCd);
                dg["dg_Dpt", i].Value = dpt;
                dg["dg_SN", i].Value = (dr["pn"].ToString() == "0") ? string.Format("{0}-{1}", dr["sn1"], dr["sn2"]) : string.Format("{0}-{1}({2})", dr["sn1"], dr["sn2"], dr["pn"]);
                dg["dg_BidDt", i].Value = dr["bid_dt"].ToString().Contains("0001") ? "" : string.Format("{0:yyyy.MM.dd}", dr["bid_dt"]);
                dg["dg_State", i].Value = state;
                dg["dg_Cat", i].Value = cat;
                dg["dg_FbCnt", i].Value = dr["fb_cnt"];
                dg["dg_ApslAmt", i].Value = string.Format("{0:N0}", dr["apsl_amt"]);
                dg["dg_MinbAmt", i].Value = string.Format("{0:N0}", dr["minb_amt"]);
                dg["dg_Url", i].Value = dr["short_url"];
                dg["dg_Idx", i].Value = dr["idx"];

                Color backColor = (Convert.ToBoolean(dr["send"])) ? Color.LightGray : Color.White;
                dg.Rows[i].DefaultCellStyle.BackColor = backColor;
            }
            dr.Close();
            db.Close();

            dg.ClearSelection();
        }

        private void btnTest_Click(object sender, EventArgs e)
        {
            //AuctSms auctSms = new AuctSms();
            //auctSms.NearBy();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            string sql, idx;

            DataGridViewSelectedRowCollection rc = dg.SelectedRows;
            if (rc.Count == 0)
            {
                MessageBox.Show("선택한 물건이 없습니다.");
                return;
            }

            if (MessageBox.Show("선택건을 발송 취소 하시겠습니까?", "발송 취소", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            db.Open();
            foreach (DataGridViewRow row in rc)
            {
                if (row.Cells["dg_Send"].Value.ToString() == "Y") continue;

                idx = row.Cells["dg_Idx"].Value.ToString();
                sql = "delete from db_tank.tx_sms where idx=" + idx;
                db.ExeQry(sql);
            }
            db.Close();

            MessageBox.Show("처리 되었습니다.");

            btnSrch_Click(null, null);
        }

        private void dg_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int rowIdx;
            string url, tid, idx;

            rowIdx = e.RowIndex;
            if (rowIdx == -1) return;

            tid = dg["dg_Tid", rowIdx].Value.ToString();
            idx = dg["dg_Idx", rowIdx].Value.ToString();
            url = dg["dg_Url", rowIdx].Value.ToString();

            if (string.IsNullOrEmpty(url))
            {
                wbr.Navigate("about:blank");
            }
            else
            {
                wbr.Navigate("https://me2.do/" + url);
            }
        }
    }
}
