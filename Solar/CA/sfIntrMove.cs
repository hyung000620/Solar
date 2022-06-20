using MySql.Data.MySqlClient;
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

namespace Solar.CA
{
    public partial class sfIntrMove : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        AuctCd auctCd = new AuctCd();
        NetUtil net = new NetUtil();

        DataTable dtDptCd;      //계
        DataTable dtStateCd;    //진행 상태

        string prntSpt;

        public sfIntrMove()
        {
            InitializeComponent();
            init();
        }

        private void init()
        {
            ui.DgSetRead(dgSS);
            ui.DgSetRead(dgST);
            ui.DgSetRead(dgS);
            ui.DgSetRead(dgT);

            dgSS.MultiSelect = true;

            //전체 법원별 계코드
            dtDptCd = db.ExeDt("select C.ca_cd, crt_cd, spt_cd, cs_cd, dpt_cd, dpt_nm from ta_cd_cs C , ta_cd_dpt D where C.ca_cd=D.ca_cd order by dpt_cd");

            //진행 상태
            dtStateCd = db.ExeDt("select sta1_cd, sta2_cd, sta1_nm, sta2_nm from ta_cd_sta order by sta2_cd");

            this.Shown += SfIntrMove_Shown;
        }

        private void SfIntrMove_Shown(object sender, EventArgs e)
        {
            wfCaMgmt prnt = (wfCaMgmt)this.Owner;
            prntSpt = prnt.cbxCrtSpt.SelectedValue.ToString();
        }

        private void btnS_Srch_Click(object sender, EventArgs e)
        {
            int i;
            string sql, srchTid, cdtn = "", csCd, dpt, state;

            dgSS.Rows.Clear();

            srchTid = txtSTid.Text.Trim();

            if (srchTid != "")
            {
                if (srchTid.Contains("-"))
                {
                    Match match = Regex.Match(srchTid, @"(\d+)\-(\d+)(\-)*(\d+)*");
                    cdtn = "spt='" + prntSpt + "' and sn1='" + match.Groups[1].Value + "' and sn2='" + match.Groups[2].Value + "'";
                    if (match.Groups[4].Value != string.Empty)
                    {
                        cdtn += " and pn='" + match.Groups[4].Value + "'";
                    }
                }
                else
                {
                    cdtn = "tid IN (" + Regex.Replace(srchTid, @"\D+", ",") + ")";   //TID 검색일 경우 모든 조건 무시
                }                
            }
            else
            {
                MessageBox.Show("[TID 또는 사건번호]를 입력 해 주세요.");
                return;
            }

            sql = "select * from ta_list where " + cdtn;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                csCd = dr["spt"].ToString();
                var xDpt = dtDptCd.Rows.Cast<DataRow>().Where(t => t["cs_cd"].ToString() == csCd && t["dpt_cd"].ToString() == dr["dpt"].ToString()).SingleOrDefault();
                dpt = (xDpt == null || dr["dpt"].ToString() == "0") ? string.Empty : xDpt.Field<string>("dpt_nm");
                var xState = dtStateCd.Rows.Cast<DataRow>().Where(t => t["sta2_cd"].ToString() == dr["sta2"].ToString()).SingleOrDefault();
                state = (xState == null || dr["sta2"].ToString() == "0") ? string.Empty : xState.Field<string>("sta2_nm");

                i = dgSS.Rows.Add();
                dgSS["dgSS_No", i].Value = i + 1;
                dgSS["dgSS_Tid", i].Value = dr["tid"];
                dgSS["dgSS_CS", i].Value = auctCd.FindCsNm(csCd);
                dgSS["dgSS_Dpt", i].Value = dpt;
                dgSS["dgSS_SN", i].Value = (dr["pn"].ToString() == "0") ? string.Format("{0}-{1}", dr["sn1"], dr["sn2"]) : string.Format("{0}-{1}({2})", dr["sn1"], dr["sn2"], dr["pn"]);
                dgSS["dgSS_State", i].Value = state;
            }
            dr.Close();
            db.Close();
            dgSS.ClearSelection();
        }

        private void btnSAdd_Click(object sender, EventArgs e)
        {
            int i;
                        
            foreach (DataGridViewRow row in dgSS.SelectedRows)
            {
                i = dgS.Rows.Add();
                dgS["dgS_No", i].Value = i + 1;
                dgS["dgS_Tid", i].Value = row.Cells["dgSS_Tid"].Value;
                dgS["dgS_CS", i].Value = row.Cells["dgSS_CS"].Value;
                dgS["dgS_Dpt", i].Value = row.Cells["dgSS_Dpt"].Value;
                dgS["dgS_SN", i].Value = row.Cells["dgSS_SN"].Value;
                dgS["dgS_State", i].Value = row.Cells["dgSS_State"].Value;
            }
            dgS.ClearSelection();
        }

        private void btnSDel_Click(object sender, EventArgs e)
        {            
            foreach (DataGridViewRow row in dgS.SelectedRows)
            {
                dgS.Rows.RemoveAt(row.Index);
            }
        }

        private void btnT_Srch_Click(object sender, EventArgs e)
        {
            int i;
            string sql, srchTid, cdtn = "", csCd, dpt, state;

            dgST.Rows.Clear();

            srchTid = txtTTid.Text.Trim();

            if (srchTid != "")
            {
                if (srchTid.Contains("-"))
                {
                    Match match = Regex.Match(srchTid, @"(\d+)\-(\d+)(\-)*(\d+)*");
                    cdtn = "spt='" + prntSpt + "' and sn1='" + match.Groups[1].Value + "' and sn2='" + match.Groups[2].Value + "'";
                    if (match.Groups[4].Value != string.Empty)
                    {
                        cdtn += " and pn='" + match.Groups[4].Value + "'";
                    }
                }
                else
                {
                    cdtn = "tid IN (" + Regex.Replace(srchTid, @"\D+", ",") + ")";   //TID 검색일 경우 모든 조건 무시
                }
            }
            else
            {
                MessageBox.Show("[TID 또는 사건번호]를 입력 해 주세요.");
                return;
            }

            sql = "select * from ta_list where " + cdtn;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                csCd = dr["spt"].ToString();
                var xDpt = dtDptCd.Rows.Cast<DataRow>().Where(t => t["cs_cd"].ToString() == csCd && t["dpt_cd"].ToString() == dr["dpt"].ToString()).SingleOrDefault();
                dpt = (xDpt == null || dr["dpt"].ToString() == "0") ? string.Empty : xDpt.Field<string>("dpt_nm");
                var xState = dtStateCd.Rows.Cast<DataRow>().Where(t => t["sta2_cd"].ToString() == dr["sta2"].ToString()).SingleOrDefault();
                state = (xState == null || dr["sta2"].ToString() == "0") ? string.Empty : xState.Field<string>("sta2_nm");

                i = dgST.Rows.Add();
                dgST["dgST_No", i].Value = i + 1;
                dgST["dgST_Tid", i].Value = dr["tid"];
                dgST["dgST_CS", i].Value = auctCd.FindCsNm(csCd);
                dgST["dgST_Dpt", i].Value = dpt;
                dgST["dgST_SN", i].Value = (dr["pn"].ToString() == "0") ? string.Format("{0}-{1}", dr["sn1"], dr["sn2"]) : string.Format("{0}-{1}({2})", dr["sn1"], dr["sn2"], dr["pn"]);
                dgST["dgST_State", i].Value = state;
            }
            dr.Close();
            db.Close();
            dgST.ClearSelection();
        }

        private void btnTAdd_Click(object sender, EventArgs e)
        {
            int i;

            foreach (DataGridViewRow row in dgST.SelectedRows)
            {
                i = dgT.Rows.Add();
                dgT["dgT_No", i].Value = i + 1;
                dgT["dgT_Tid", i].Value = row.Cells["dgST_Tid"].Value;
                dgT["dgT_CS", i].Value = row.Cells["dgST_CS"].Value;
                dgT["dgT_Dpt", i].Value = row.Cells["dgST_Dpt"].Value;
                dgT["dgT_SN", i].Value = row.Cells["dgST_SN"].Value;
                dgT["dgT_State", i].Value = row.Cells["dgST_State"].Value;
            }
            dgT.ClearSelection();
        }

        private void btnTDel_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in dgT.SelectedRows)
            {
                dgT.Rows.RemoveAt(row.Index);
            }
        }

        /// <summary>
        /// 관심물건 이동처리
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnIntrMove_Click(object sender, EventArgs e)
        {
            string sql, srcList, tgtList, staff;

            List<string> lsSrc = new List<string>();
            List<string> lsTgt = new List<string>();
            List<MySqlParameter> sp = new List<MySqlParameter>();

            staff = Properties.Settings.Default.USR_ID;

            if (rdoDvsn.Checked == false && rdoMerg.Checked == false)
            {
                MessageBox.Show("처리구분을 선택 해 주세요.");
                return;
            }

            if (MessageBox.Show("관심물건 처리를 하시겠습니까?", "관심물건 처리", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            foreach (DataGridViewRow row in dgS.Rows)
            {
                lsSrc.Add(row.Cells["dgS_Tid"].Value.ToString());
            }
            srcList = string.Join(",", lsSrc.ToArray());

            foreach (DataGridViewRow row in dgT.Rows)
            {
                lsTgt.Add(row.Cells["dgT_Tid"].Value.ToString());
            }

            if (rdoDvsn.Checked)
            {
                if (lsSrc.Count != 1 || lsTgt.Count == 0)
                {
                    MessageBox.Show("[물번 분리] 처리 구조에 맞지 않습니다.");
                    return;
                }
            }
            else
            {
                if (lsTgt.Count != 1 || lsSrc.Count == 0)
                {
                    MessageBox.Show("[물번 합침] 처리 구조에 맞지 않습니다.");
                    return;
                }
            }

            sql = "select * from db_tank.tm_interest where itype=1 and tid in (" + srcList + ")";
            DataTable dtS = db.ExeDt(sql);

            foreach (DataRow row in dtS.Rows)
            {
                if (rdoDvsn.Checked)
                {
                    db.Open();
                    foreach (string tgt in lsTgt)
                    {
                        //관심 추가
                        sql = "insert ignore into db_tank.tm_interest set id=@id, itype=1, tid=@tid, priority=@priority, gubun=@gubun, memo=@memo, mv=1, wdate=curdate()";
                        sp.Add(new MySqlParameter("@id", row["id"]));
                        sp.Add(new MySqlParameter("@tid", tgt));
                        sp.Add(new MySqlParameter("@priority", row["priority"]));
                        sp.Add(new MySqlParameter("@gubun", row["gubun"]));
                        sp.Add(new MySqlParameter("@memo", row["memo"]));
                        db.ExeQry(sql, sp);
                        sp.Clear();

                        //기록(추가)
                        sql = "insert into db_tank.tx_inter_things set id=@id, itype=1, tid=@tid, priority=@priority, gubun=@gubun, memo=@memo, wdate=curdate(), dvsn=1, staff=@staff, dtm=now()";
                        sp.Add(new MySqlParameter("@id", row["id"]));
                        sp.Add(new MySqlParameter("@tid", tgt));
                        sp.Add(new MySqlParameter("@priority", row["priority"]));
                        sp.Add(new MySqlParameter("@gubun", row["gubun"]));
                        sp.Add(new MySqlParameter("@memo", row["memo"]));
                        sp.Add(new MySqlParameter("@staff", staff));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                    db.Close();
                }
                else if (rdoMerg.Checked)
                {
                    db.Open();
                    //기록(삭제)
                    sql = "insert into db_tank.tx_inter_things set id=@id, itype=1, tid=@tid, priority=@priority, gubun=@gubun, memo=@memo, wdate=@wdate, dvsn=0, staff=@staff, dtm=now()";
                    sp.Add(new MySqlParameter("@id", row["id"]));
                    sp.Add(new MySqlParameter("@tid", row["tid"]));
                    sp.Add(new MySqlParameter("@priority", row["priority"]));
                    sp.Add(new MySqlParameter("@gubun", row["gubun"]));
                    sp.Add(new MySqlParameter("@memo", row["memo"]));
                    sp.Add(new MySqlParameter("@wdate", row["wdate"]));
                    sp.Add(new MySqlParameter("@staff", staff));
                    db.ExeQry(sql, sp);
                    sp.Clear();

                    //관심 삭제
                    sql = "delete from db_tank.tm_interest where idx=@idx and id=@id";
                    sp.Add(new MySqlParameter("@idx", row["idx"]));
                    sp.Add(new MySqlParameter("@id", row["id"]));
                    db.ExeQry(sql, sp);
                    sp.Clear();

                    foreach (string tgt in lsTgt)
                    {
                        //관심 추가
                        sql = "insert ignore into db_tank.tm_interest set id=@id, itype=1, tid=@tid, priority=@priority, gubun=@gubun, memo=@memo, mv=1, wdate=curdate()";
                        sp.Add(new MySqlParameter("@id", row["id"]));
                        sp.Add(new MySqlParameter("@tid", tgt));
                        sp.Add(new MySqlParameter("@priority", row["priority"]));
                        sp.Add(new MySqlParameter("@gubun", row["gubun"]));
                        sp.Add(new MySqlParameter("@memo", row["memo"]));
                        db.ExeQry(sql, sp);
                        sp.Clear();

                        //기록(추가)
                        sql = "insert into db_tank.tx_inter_things set id=@id, itype=1, tid=@tid, priority=@priority, gubun=@gubun, memo=@memo, wdate=curdate(), dvsn=1, staff=@staff, dtm=now()";
                        sp.Add(new MySqlParameter("@id", row["id"]));
                        sp.Add(new MySqlParameter("@tid", tgt));
                        sp.Add(new MySqlParameter("@priority", row["priority"]));
                        sp.Add(new MySqlParameter("@gubun", row["gubun"]));
                        sp.Add(new MySqlParameter("@memo", row["memo"]));
                        sp.Add(new MySqlParameter("@staff", staff));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                    db.Close();
                }
                else
                { 
                    //
                }
            }

            MessageBox.Show("처리 되었습니다.");
        }
    }
}
