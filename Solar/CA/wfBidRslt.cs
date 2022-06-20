using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

using HtmlAgilityPack;
using hap = HtmlAgilityPack;
using HAPDoc = HtmlAgilityPack.HtmlDocument;
using System.Threading;

namespace Solar.CA
{
    public partial class wfBidRslt : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        AuctCd auctCd = new AuctCd();
        NetUtil net = new NetUtil();

        DataTable dtCrtSpt;     //법원
        DataTable dtDptCd;      //계
        DataTable dtCat;        //물건 종류
        DataTable dtStateCd;    //진행 상태
        DataTable dtBidRate;    //법원별 기본 유찰율

        //RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;
        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;

        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 1500, webCnt = 0;

        public wfBidRslt()
        {
            InitializeComponent();

            Init();
        }

        private void Init()
        {
            ui.DgSetRead(dgNt, 0);
            ui.DgSetRead(dgLs, 0);
            ui.DgSetRead(dgSeq, 0);
            ui.DgSetRead(dgA, 0);
            ui.DgSetRead(dgT, 0);
            ui.DgSetRead(dgS, 0);
            ui.DgSetRead(dgR, 0);
            ui.DgSetRead(dg, 0);
            dgS.MultiSelect = true;
            dgR.MultiSelect = true;

            dtCrtSpt = db.ExeDt("select * from ta_cd_cs");

            //전체 법원별 계코드 DataTable
            dtDptCd = db.ExeDt("select C.ca_cd, crt_cd, spt_cd, cs_cd, dpt_cd, dpt_nm from ta_cd_cs C , ta_cd_dpt D where C.ca_cd=D.ca_cd order by dpt_cd");

            //진행 상태
            dtStateCd = db.ExeDt("select sta1_cd, sta2_cd, sta1_nm, sta2_nm from ta_cd_sta order by sta2_cd");

            //물건종별 및 토지 지목
            dtCat = db.ExeDt("select cat1_cd, cat2_cd, cat3_cd, cat2_nm, cat3_nm, hide, bldg_type from ta_cd_cat where cat3_cd > 0 order by cat3_cd");

            //법원별 기본 유찰율
            dtBidRate = db.ExeDt("select spt_cd, fbid_rate1, fbid_rate2 from ta_cd_cs");

            btnSrch.Select();
            dtpNxtDt.Value = DateTime.Now.AddDays(28);
        }

        /// <summary>
        /// 좌/우 관련탭 활성화
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbcL_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tbcL.SelectedTab == tabFbSpt)
            {
                tbcR.SelectedTab = tabFb;
            }
            else if (tbcL.SelectedTab == tabMdfySpt)
            {
                tbcR.SelectedTab = tabMdfyEa;
            }
        }

        /// <summary>
        /// 일괄유찰-공고 검색
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSrch_Click(object sender, EventArgs e)
        {
            int i = 0, n = 0, fbCnt = 0;
            string sql, bidDt, spt, dpt;

            dgNt.SelectionChanged -= dgNt_SelectionChanged;
            dgNt.Rows.Clear();
            dgLs.Rows.Clear();
            dgSeq.Rows.Clear();
            txtLsCnt.Text = string.Empty;
            
            bidDt = dtpBidDt.Value.ToShortDateString();
            sql = "select idx, spt, dpt, bid_dt from ta_skd where bid_dt='" + bidDt + "' order by spt, dpt";

            this.Cursor = Cursors.WaitCursor;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                n++;
                var xDpt = dtDptCd.Rows.Cast<DataRow>().Where(t => t["cs_cd"].ToString() == dr["spt"].ToString() && t["dpt_cd"].ToString() == dr["dpt"].ToString()).FirstOrDefault();
                dpt = (xDpt == null || dr["dpt"].ToString() == "0") ? string.Empty : xDpt.Field<string>("dpt_nm");
                i = dgNt.Rows.Add();
                dgNt["dgNt_No", i].Value = i + 1;
                dgNt["dgNt_CS", i].Value = auctCd.FindCsNm(dr["spt"].ToString());
                dgNt["dgNt_Dpt", i].Value = dpt;
                dgNt["dgNt_BidDt", i].Value = string.Format("{0:yyyy-MM-dd}", dr["bid_dt"]);
                dgNt["dgNt_SptCd", i].Value = dr["spt"];
                dgNt["dgNt_DptCd", i].Value = dr["dpt"];
            }
            dr.Close();
            db.Close();
            dgNt.ClearSelection();
            this.Cursor = Cursors.Default;

            if (n == 0)
            {
                MessageBox.Show("검색된 경매일정이 없습니다.");
                return;
            }

            db.Open();
            foreach (DataGridViewRow row in dgNt.Rows)
            {
                spt = row.Cells["dgNt_SptCd"].Value.ToString();
                dpt = row.Cells["dgNt_DptCd"].Value.ToString();
                bidDt = dtpBidDt.Value.ToShortDateString();

                sql = "select count(*) from ta_list where sta1=11 and spt=" + spt + " and dpt=" + dpt + " and bid_dt='" + bidDt + "' order by sn1, sn2, pn";
                fbCnt = Convert.ToInt32(db.RowCnt(sql));
                row.Cells["dgNt_FbCnt"].Value = fbCnt;
            }
            db.Close();

            dgNt.SelectionChanged += dgNt_SelectionChanged;
        }

        /// <summary>
        /// 일괄유찰-물건 목록
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgNt_SelectionChanged(object sender, EventArgs e)
        {
            int i = 0, n = 0;
            string sql, bidDt, spt, dpt, state, cat;

            dgLs.Rows.Clear();
            txtLsCnt.Text = string.Empty;

            if (dgNt.CurrentRow == null) return;
            i = dgNt.CurrentRow.Index;

            spt = dgNt["dgNt_SptCd", i].Value.ToString();
            dpt = dgNt["dgNt_DptCd", i].Value.ToString();
            bidDt = dtpBidDt.Value.ToShortDateString();

            sql = "select tid, sn1, sn2, pn, bid_dt, bid_tm, bid_tm1, bid_tm2, minb_amt, sta2, cat3, bid_cnt from ta_list where sta1=11 and spt=" + spt + " and dpt=" + dpt + " and bid_dt='" + bidDt + "' order by sn1, sn2, pn";
            this.Cursor = Cursors.WaitCursor;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                n++;
                var xState = dtStateCd.Rows.Cast<DataRow>().Where(t => t["sta2_cd"].ToString() == dr["sta2"].ToString()).SingleOrDefault();
                state = (xState == null || dr["sta2"].ToString() == "0") ? string.Empty : xState.Field<string>("sta2_nm");

                var xCat = dtCat.Rows.Cast<DataRow>().Where(t => t["cat3_cd"].ToString() == dr["cat3"].ToString()).SingleOrDefault();
                cat = (xCat == null || dr["cat3"].ToString() == "0") ? string.Empty : xCat.Field<string>("cat3_nm");

                i = dgLs.Rows.Add();
                dgLs["dgLs_No", i].Value = i + 1;
                dgLs["dgLs_Tid", i].Value = dr["tid"];
                dgLs["dgLs_SN", i].Value = (dr["pn"].ToString() == "0") ? string.Format("{0}-{1}", dr["sn1"], dr["sn2"]) : string.Format("{0}-{1} ({2})", dr["sn1"], dr["sn2"], dr["pn"]);
                dgLs["dgLs_BidDt", i].Value = string.Format("{0:yyyy.MM.dd}", dr["bid_dt"]);
                dgLs["dgLs_BidTm", i].Value = dr["bid_tm"];
                dgLs["dgLs_BidCnt", i].Value = dr["bid_cnt"];
                dgLs["dgLs_Tm1", i].Value = dr["bid_tm1"];
                dgLs["dgLs_Tm2", i].Value = dr["bid_tm2"];
                dgLs["dgLs_State", i].Value = state;
                dgLs["dgLs_Cat", i].Value = cat;
                dgLs["dgLs_MinbAmt", i].Value = string.Format("{0:N0}", dr["minb_amt"]);
            }
            dr.Close();
            db.Close();
            dgLs.ClearSelection();
            this.Cursor = Cursors.Default;

            if (n == 0)
            {
                MessageBox.Show("검색된 물건이 없습니다.");
                return;
            }
            else
            {
                txtLsCnt.Text = n.ToString();
            }
        }

        /// <summary>
        /// 일괄유찰-미리 보기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnPreView_Click(object sender, EventArgs e)
        {
            string sql, amt, bidDt, bidTm1, bidTm2, nxtBidDt, nxtBidTm, nxtBidTm1, nxtBidTm2, nxtAmt1, nxtAmt2;
            decimal spt, fbidRate, bidRate1, bidRate2;
            int daysDiff = 0, bidCnt = 0, nxtSeqCnt = 0;
            ArrayList arrLs = new ArrayList();

            RsltInit();
            if (chkManualSet.Checked)
            {
                chkManualSet.Checked = false;
            }

            spt = Convert.ToDecimal(dgNt.CurrentRow.Cells["dgNt_SptCd"].Value);
            List<MySqlParameter> sp = new List<MySqlParameter>();

            DataTable dt = new DataTable();
            dt.Columns.Add("idx");
            dt.Columns.Add("amt");
            dt.Columns.Add("nxtAmt");
            dt.Columns.Add("nxtDt");
            dt.Columns.Add("cnt");

            DataTable dtSeq = new DataTable();
            dtSeq.Columns.Add("bidDt");
            dtSeq.Columns.Add("amt");

            this.Cursor = Cursors.WaitCursor;
            db.Open();
            foreach (DataGridViewRow row in dgLs.Rows)
            {
                dtSeq.Rows.Clear();
                nxtBidTm = "00:00:00";
                nxtAmt1 = "0";
                nxtAmt2 = "0";

                sql = "select * from ta_seq where tid=@tid and minb_amt < @minb_amt order by seq limit 2";
                sp.Add(new MySqlParameter("@tid", row.Cells["dgLs_Tid"].Value));
                sp.Add(new MySqlParameter("@minb_amt", Convert.ToDecimal(row.Cells["dgLs_MinbAmt"].Value.ToString().Replace(",", string.Empty))));
                MySqlDataReader dr = db.ExeRdr(sql, sp);
                sp.Clear();

                while (dr.Read())
                {
                    dtSeq.Rows.Add(string.Format("{0:yyyy.MM.dd}", dr["bid_dt"]), dr["minb_amt"]);
                }
                dr.Close();
                nxtSeqCnt = dtSeq.Rows.Count;
                if (nxtSeqCnt == 0) continue;
                                
                bidDt = row.Cells["dgLs_BidDt"].Value.ToString();
                nxtBidTm1= row.Cells["dgLs_Tm1"].Value.ToString();
                nxtBidTm2= row.Cells["dgLs_Tm2"].Value.ToString();
                nxtBidDt = dtSeq.Rows[0]["bidDt"].ToString();
                nxtAmt1 = string.Format("{0:N0}", Convert.ToDecimal(dtSeq.Rows[0]["amt"]));
                bidCnt = Convert.ToInt32(row.Cells["dgLs_BidCnt"].Value);

                if (arrLs.Contains(nxtBidDt) == false)
                {
                    arrLs.Add(nxtBidDt);
                }

                if (bidCnt == 1)
                {
                    nxtBidTm = nxtBidTm1;
                }
                else if (bidCnt == 2)    //현재 2회 입찰일 경우
                {
                    if (dtSeq.Rows.Count == 1)
                    {
                        nxtBidTm = (nxtBidDt == bidDt) ? nxtBidTm2 : nxtBidTm1;
                    }
                    else
                    {
                        if (nxtBidDt == bidDt)
                        {
                            nxtBidTm = nxtBidTm2;
                        }
                        else if (nxtBidDt == dtSeq.Rows[1]["bidDt"].ToString())     //다음도 2회 입찰일 경우
                        {
                            nxtBidTm = nxtBidTm1;
                            nxtAmt2 = string.Format("{0:N0}", Convert.ToDecimal(dtSeq.Rows[1]["amt"]));
                        }
                        else
                        { 
                            //
                        }
                    }                    
                }
                else
                {
                    //
                }

                fbidRate = 100 - (Convert.ToDecimal(nxtAmt1.Replace(",", string.Empty)) / Convert.ToDecimal(row.Cells["dgLs_MinbAmt"].Value.ToString().Replace(",", string.Empty)) * 100);
                fbidRate = Math.Round(fbidRate);
                amt = row.Cells["dgLs_MinbAmt"].Value.ToString();
                row.Cells["dgLs_NxtBidDt"].Value = nxtBidDt;
                row.Cells["dgLs_NxtBidTm"].Value = nxtBidTm;
                row.Cells["dgLs_NxtBidTm1"].Value = nxtBidTm1;
                row.Cells["dgLs_NxtBidTm2"].Value = nxtBidTm2;
                row.Cells["dgLs_NxtAmt1"].Value = nxtAmt1;
                row.Cells["dgLs_NxtAmt2"].Value = nxtAmt2;
                row.Cells["dgLs_Rate"].Value = fbidRate;

                //다수건 조회를 위해
                var xRow = dt.Rows.Cast<DataRow>().Where(t => t["nxtDt"].ToString() == nxtBidDt).FirstOrDefault();
                if (xRow == null)
                {
                    dt.Rows.Add(row.Index, amt.Replace(",", string.Empty), nxtAmt1.Replace(",", string.Empty), nxtBidDt, 1);
                }
                else
                {
                    dt.Rows[dt.Rows.IndexOf(xRow)]["cnt"] = Convert.ToInt32(dt.Rows[dt.Rows.IndexOf(xRow)]["cnt"]) + 1;
                }
            }
            db.Close();
            this.Cursor = Cursors.Default;

            //미매칭건 체크
            var xRows = dgLs.Rows.Cast<DataGridViewRow>().Where(t => t.Cells["dgLs_NxtBidDt"].Value == null || t.Cells["dgLs_NxtBidDt"].Value?.ToString() == string.Empty);
            if (xRows.Count() == 0) return;

            var fRow = dtBidRate.Rows.Cast<DataRow>().Where(t => t["spt_cd"].ToString() == dgNt["dgNt_SptCd", dgNt.CurrentRow.Index].Value.ToString()).FirstOrDefault();
            bidRate1 = Convert.ToDecimal(fRow["fbid_rate1"]);
            bidRate2 = Convert.ToDecimal(fRow["fbid_rate2"]);
            bool aplyEx = (bidRate1 != bidRate2) ? true : false;

            DataGridViewCellStyle style1 = new DataGridViewCellStyle();
            style1.ForeColor = Color.Green;
            DataGridViewCellStyle style2 = new DataGridViewCellStyle();
            style2.ForeColor = Color.Blue;
            DataGridViewCellStyle style3 = new DataGridViewCellStyle();
            style3.ForeColor = Color.Red;

            //목록 중 차회 매각정보가 한건도 없을 경우
            if (dt.Rows.Count == 0)
            {
                if (MessageBox.Show("저장된 차회 매각정보가 없습니다.\r\n해당 법원의 기본 유찰율을 적용하시겠습니까?", "차회 매각정보 없음", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    foreach (DataGridViewRow row in dgLs.Rows)
                    {
                        fbidRate = (row.Cells["dgLs_State"].Value.ToString() == "유찰") ? bidRate2 : bidRate1;
                        nxtBidDt = string.Format("{0:yyyy.MM.dd}", dtpNxtDt.Value);
                        row.Cells["dgLs_NxtBidDt"].Value = nxtBidDt;
                        row.Cells["dgLs_NxtBidTm"].Value = row.Cells["dgLs_Tm1"].Value.ToString();
                        row.Cells["dgLs_NxtBidTm1"].Value = row.Cells["dgLs_Tm1"].Value.ToString();
                        row.Cells["dgLs_NxtBidTm2"].Value = row.Cells["dgLs_Tm2"].Value.ToString();
                        row.Cells["dgLs_NxtAmt1"].Value = string.Format("{0:N0}", Math.Round(Convert.ToDecimal(row.Cells["dgLs_MinbAmt"].Value) * (100 - fbidRate) / 100000) * 1000);
                        row.Cells["dgLs_Note"].Value = "법원 기본 유찰율 적용";
                        row.Cells["dgLs_Rate"].Value = fbidRate;
                        row.Cells["dgLs_NxtBidDt"].Style = style3;
                        row.Cells["dgLs_NxtBidTm1"].Style = style3;
                        row.Cells["dgLs_NxtAmt1"].Style = style3;
                        if (Convert.ToInt32(row.Cells["dgLs_BidCnt"].Value) == 2)
                        {                            
                            row.Cells["dgLs_NxtAmt2"].Value = string.Format("{0:N0}", Math.Round(Convert.ToDecimal(row.Cells["dgLs_NxtAmt1"].Value) * (100 - fbidRate) / 100000) * 1000);
                        }
                        else
                        {
                            row.Cells["dgLs_NxtAmt2"].Value = "0";
                        }
                        row.Cells["dgLs_NxtBidTm2"].Style = style3;
                        row.Cells["dgLs_NxtAmt2"].Style = style3;
                    }
                }
                return;
            }

            dt.DefaultView.Sort = "cnt DESC";
            nxtBidDt = dt.Rows[0]["nxtDt"].ToString();
            daysDiff = (Convert.ToDateTime(nxtBidDt.Replace(".", "-")).Date - DateTime.Now.Date).Days;
            if (daysDiff < 20 || daysDiff > 50)
            {
                MessageBox.Show("주의 !!!\r\n\r\n다음 입찰일과의 차이가 " + daysDiff + " 일 입니다.");
            }
            fbidRate = 100 - (Convert.ToDecimal(dt.Rows[0]["nxtAmt"]) / Convert.ToDecimal(dt.Rows[0]["amt"]) * 100);
            fbidRate = Math.Round(fbidRate);

            if (aplyEx)
            {
                foreach (DataGridViewRow row in xRows)
                {
                    fbidRate = (row.Cells["dgLs_State"].Value.ToString() == "유찰") ? bidRate2 : bidRate1;
                    row.Cells["dgLs_NxtBidDt"].Value = nxtBidDt;
                    row.Cells["dgLs_NxtBidTm"].Value = row.Cells["dgLs_Tm1"].Value.ToString();
                    row.Cells["dgLs_NxtBidTm1"].Value = row.Cells["dgLs_Tm1"].Value.ToString();
                    row.Cells["dgLs_NxtBidTm2"].Value = row.Cells["dgLs_Tm2"].Value.ToString();
                    row.Cells["dgLs_NxtAmt1"].Value = string.Format("{0:N0}", Math.Round(Convert.ToDecimal(row.Cells["dgLs_MinbAmt"].Value) * (100 - fbidRate) / 100000) * 1000);
                    row.Cells["dgLs_Note"].Value = "유찰율 차등, 매각기일 최다건 적용";
                    row.Cells["dgLs_Rate"].Value = fbidRate;
                    row.Cells["dgLs_NxtBidDt"].Style = style1;
                    row.Cells["dgLs_NxtBidTm1"].Style = style1;
                    row.Cells["dgLs_NxtAmt1"].Style = style1;
                    if (Convert.ToInt32(row.Cells["dgLs_BidCnt"].Value) == 2)
                    {                        
                        row.Cells["dgLs_NxtAmt2"].Value = string.Format("{0:N0}", Math.Round(Convert.ToDecimal(row.Cells["dgLs_NxtAmt1"].Value) * (100 - fbidRate) / 100000) * 1000);
                    }
                    else
                    {
                        row.Cells["dgLs_NxtAmt2"].Value = "0";
                    }
                    row.Cells["dgLs_NxtBidTm2"].Style = style1;
                    row.Cells["dgLs_NxtAmt2"].Style = style1;
                }
            }
            else
            {
                foreach (DataGridViewRow row in xRows)
                {
                    row.Cells["dgLs_NxtBidDt"].Value = nxtBidDt;
                    row.Cells["dgLs_NxtBidTm"].Value = row.Cells["dgLs_Tm1"].Value.ToString();
                    row.Cells["dgLs_NxtBidTm1"].Value = row.Cells["dgLs_Tm1"].Value.ToString();
                    row.Cells["dgLs_NxtBidTm2"].Value = row.Cells["dgLs_Tm2"].Value.ToString();
                    row.Cells["dgLs_NxtAmt1"].Value = string.Format("{0:N0}", Math.Round(Convert.ToDecimal(row.Cells["dgLs_MinbAmt"].Value) * (100 - fbidRate) / 100000) * 1000);
                    row.Cells["dgLs_Note"].Value = "유찰율/매각기일 최다건 적용";
                    row.Cells["dgLs_Rate"].Value = fbidRate;
                    row.Cells["dgLs_NxtBidDt"].Style = style2;
                    row.Cells["dgLs_NxtBidTm1"].Style = style2;
                    row.Cells["dgLs_NxtAmt1"].Style = style2;
                    if (Convert.ToInt32(row.Cells["dgLs_BidCnt"].Value) == 2)
                    {                        
                        row.Cells["dgLs_NxtAmt2"].Value = string.Format("{0:N0}", Math.Round(Convert.ToDecimal(row.Cells["dgLs_NxtAmt1"].Value) * (100 - fbidRate) / 100000) * 1000);
                    }
                    else
                    {
                        row.Cells["dgLs_NxtAmt2"].Value = "0";
                    }
                    row.Cells["dgLs_NxtBidTm2"].Style = style2;
                    row.Cells["dgLs_NxtAmt2"].Style = style2;
                }
            }

            if (arrLs.Count > 1)
            {
                MessageBox.Show("차회 매각기일이 " + arrLs.Count.ToString() + "개 있습니다.\r\n" + string.Join(", ", arrLs.ToArray()));
            }
        }

        /// <summary>
        /// 일괄유찰-매칭 결과 값 초기화
        /// </summary>
        private void RsltInit()
        {
            foreach (DataGridViewRow row in dgLs.Rows)
            {
                row.Cells["dgLs_NxtBidDt"].Value = "";
                row.Cells["dgLs_NxtBidTm"].Value = "";
                row.Cells["dgLs_NxtBidTm1"].Value = "";
                row.Cells["dgLs_NxtAmt1"].Value = "";
                row.Cells["dgLs_NxtBidTm2"].Value = "";
                row.Cells["dgLs_NxtAmt2"].Value = "";
                row.Cells["dgLs_Note"].Value = "";
                row.Cells["dgLs_Rate"].Value = "";
            }
        }

        /// <summary>
        /// 일괄유찰-회차 정보 및 실시간 매각물건명세서(법원) 참조
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgLs_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int i = 0, n = 0;
            string sql, url, tid, html, errMsg;
            string jiwonNm, saNo, maemulSer, maeGiil, jpDeptCd;

            dgSeq.Rows.Clear();
            StringBuilder sb = new StringBuilder();
            if (e.RowIndex < 0) return;

            tid = dgLs["dgLs_Tid", e.RowIndex].Value.ToString();
            sql = "select * from ta_seq where tid=" + tid + " order by seq";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                n++;
                i = dgSeq.Rows.Add();
                dgSeq["dgSeq_Idx", i].Value = dr["idx"];
                dgSeq["dgSeq_Seq", i].Value = dr["seq"];
                dgSeq["dgSeq_BidDt", i].Value = (dr["bid_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy.MM.dd}", dr["bid_dt"]);
                dgSeq["dgSeq_BidTm", i].Value = dr["bid_tm"];
                dgSeq["dgSeq_MinbAmt", i].Value = string.Format("{0:N0}", dr["minb_amt"]);
                dgSeq["dgSeq_Wdt", i].Value = string.Format("{0:yyyy-MM-dd}", dr["wdt"]);
            }
            dr.Close();
            db.Close();
            dgSeq.ClearSelection();

            if (n == 0)
            {
                sb.AppendLine("> 저장된 회차 정보가 없습니다.");
            }

            sql = "select * from ta_list where tid=" + tid;
            db.Open();
            dr = db.ExeRdr(sql);
            dr.Read();
            jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", dr["spt"]));
            saNo = string.Format("{0}0130{1}", dr["sn1"], dr["sn2"].ToString().PadLeft(6, '0'));
            maemulSer = (dr["pn"].ToString() == "0") ? "1" : dr["pn"].ToString();
            maeGiil = string.Format("{0:yyyyMMdd}", dr["bid_dt"]);
            jpDeptCd = dr["dpt"].ToString();
            dr.Close();
            db.Close();

            url = "http://www.courtauction.go.kr/RetrieveMobileEstMgakMulMseo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&ordHoi=&orgSaNo=" + saNo + "&maemulSer=" + maemulSer + "&maeGiil=" + maeGiil + "&mGakMulMseoYN=Y&corCanYN=N&jpDeptCd=" + jpDeptCd;
            html = net.GetHtml(url);
            Regex rx = new Regex(@"downMaemulMyungDoc\('(.*)?'\)", rxOptM);
            Match match = rx.Match(html);
            if (match.Success)
            {
                url = match.Groups[1].Value;
                axAcroPDF1.src = url;
            }
            else
            {
                sb.AppendLine("> [법원-매각물건명세서]가 없습니다.");
            }

            errMsg = sb.ToString();
            if (errMsg != string.Empty)
            {
                MessageBox.Show(errMsg);
            }
        }

        /// <summary>
        /// 일괄유찰-관리자 지정값 적용
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void chkManualSet_CheckedChanged(object sender, EventArgs e)
        {
            string nxtBidDt;
            decimal fbidRate;
            int daysDiff = 0;

            RsltInit();
            if (!chkManualSet.Checked) return;

            nxtBidDt = string.Format("{0:yyyy.MM.dd}", dtpNxtDt.Value);
            daysDiff = (dtpNxtDt.Value.Date - DateTime.Now.Date).Days;
            if (daysDiff < 20 || daysDiff > 50)
            {
                if (MessageBox.Show("다음 입찰일과 차이가 " + daysDiff + " 일 입니다.\r\n적용하시겠습니까?", "날짜 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No)
                {
                    return;
                }
            }

            if (rdo20.Checked) fbidRate = 20;
            else if (rdo30.Checked) fbidRate = 30;
            else fbidRate = 40;

            DataGridViewCellStyle style4 = new DataGridViewCellStyle();
            style4.ForeColor = Color.DarkViolet;

            foreach (DataGridViewRow row in dgLs.Rows)
            {   
                row.Cells["dgLs_NxtBidDt"].Value = nxtBidDt;
                row.Cells["dgLs_NxtBidTm"].Value = row.Cells["dgLs_Tm1"].Value.ToString();
                row.Cells["dgLs_NxtBidTm1"].Value = row.Cells["dgLs_Tm1"].Value.ToString();
                row.Cells["dgLs_NxtBidTm2"].Value = row.Cells["dgLs_Tm2"].Value.ToString();
                row.Cells["dgLs_NxtAmt1"].Value = string.Format("{0:N0}", Math.Round(Convert.ToDecimal(row.Cells["dgLs_MinbAmt"].Value) * (100 - fbidRate) / 100000) * 1000);
                row.Cells["dgLs_Note"].Value = "관리자 지정값 적용";
                row.Cells["dgLs_Rate"].Value = fbidRate;
                row.Cells["dgLs_NxtBidDt"].Style = style4;
                row.Cells["dgLs_NxtBidTm1"].Style = style4;
                row.Cells["dgLs_NxtAmt1"].Style = style4;
                if (Convert.ToInt32(row.Cells["dgLs_BidCnt"].Value) == 2)
                {                    
                    row.Cells["dgLs_NxtAmt2"].Value = string.Format("{0:N0}", Math.Round(Convert.ToDecimal(row.Cells["dgLs_NxtAmt1"].Value) * (100 - fbidRate) / 100000) * 1000);
                }
                else
                {
                    row.Cells["dgLs_NxtAmt2"].Value = "0";
                }
                row.Cells["dgLs_NxtBidTm2"].Style = style4;
                row.Cells["dgLs_NxtAmt2"].Style = style4;
            }
        }

        /// <summary>
        /// 일괄유찰-실제 적용(저장)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSave_Click(object sender, EventArgs e)
        {
            int i = 0, nxtSeq;
            string spt, dpt;
            string sql, tid, bidDt, bidTm, nxtBidDt, nxtBidTm, bidCnt, nxtTm1, nxtTm2, nxtAmt1, nxtAmt2;

            if (MessageBox.Show("실제로 반영 하시겠습니까?", "저장 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            if (dgNt.CurrentRow == null) return;
            i = dgNt.CurrentRow.Index;

            spt = dgNt["dgNt_SptCd", i].Value.ToString();
            dpt = dgNt["dgNt_DptCd", i].Value.ToString();

            ArrayList arrLs = new ArrayList();
            List<MySqlParameter> sp = new List<MySqlParameter>();

            btnSave.Enabled = false;
            this.Cursor = Cursors.WaitCursor;
            db.Open();
            foreach (DataGridViewRow row in dgLs.Rows)
            {
                nxtSeq = 0;
                tid = row.Cells["dgLs_Tid"].Value.ToString();
                bidDt= row.Cells["dgLs_BidDt"].Value.ToString().Replace(".", string.Empty);
                bidTm = row.Cells["dgLs_BidTm"].Value.ToString();
                bidCnt = row.Cells["dgLs_BidCnt"].Value.ToString();

                nxtBidDt = row.Cells["dgLs_NxtBidDt"].Value.ToString().Replace(".", string.Empty);
                nxtBidTm= row.Cells["dgLs_NxtBidTm"].Value.ToString();
                nxtTm1 = row.Cells["dgLs_NxtBidTm1"].Value.ToString();
                nxtAmt1 = row.Cells["dgLs_NxtAmt1"].Value.ToString().Replace(",", string.Empty);
                nxtTm2 = row.Cells["dgLs_NxtBidTm2"].Value.ToString();
                nxtAmt2 = row.Cells["dgLs_NxtAmt2"].Value.ToString().Replace(",", string.Empty);

                sql = "update ta_list set sta1=11, sta2=1111, fb_cnt=(fb_cnt+1), bid_dt=@bid_dt, bid_cnt=@bid_cnt, bid_tm=@bid_tm, bid_tm1=@bid_tm1, bid_tm2=@bid_tm2, minb_amt=@minb_amt where tid=@tid";
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@bid_dt", nxtBidDt));
                sp.Add(new MySqlParameter("@bid_cnt", bidCnt));
                sp.Add(new MySqlParameter("@bid_tm", nxtBidTm));
                sp.Add(new MySqlParameter("@bid_tm1", nxtTm1));
                sp.Add(new MySqlParameter("@bid_tm2", nxtTm2));
                sp.Add(new MySqlParameter("@minb_amt", nxtAmt1));
                db.ExeQry(sql, sp);
                sp.Clear();

                sql = "update ta_hist set sta=1111 where tid=@tid and bid_dt=@bid_dt and bid_tm=@bid_tm and (sta in (1110,1111))";
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@bid_dt", bidDt));
                sp.Add(new MySqlParameter("@bid_tm", bidTm));
                db.ExeQry(sql, sp);
                sp.Clear();

                sql = "select max(seq) as curSeq from ta_hist where tid='" + tid + "'";
                MySqlDataReader dr = db.ExeRdr(sql);
                dr.Read();
                if (dr["curSeq"] == DBNull.Value)
                {
                    //오류 기록
                    nxtSeq = 1;
                }
                else
                {
                    nxtSeq = Convert.ToInt32(dr["curSeq"].ToString()) + 1;
                }
                dr.Close();

                if (nxtBidDt != bidDt)  //2회차 오전일 경우에는 일정 추가 없음
                {
                    sql = "insert into ta_hist set tid=@tid, sta=1110, bid_dt=@bid_dt, bid_tm=@bid_tm, amt=@amt, seq=@seq";
                    sp.Add(new MySqlParameter("@tid", tid));
                    sp.Add(new MySqlParameter("@bid_dt", nxtBidDt));
                    sp.Add(new MySqlParameter("@bid_tm", nxtTm1));
                    sp.Add(new MySqlParameter("@amt", nxtAmt1));
                    sp.Add(new MySqlParameter("@seq", nxtSeq));
                    db.ExeQry(sql, sp);
                    sp.Clear();
                }
                
                if (nxtAmt2 != string.Empty && nxtAmt2 != "0")
                {
                    sql = "insert into ta_hist set tid=@tid, sta=1110, bid_dt=@bid_dt, bid_tm=@bid_tm, amt=@amt, seq=@seq";
                    sp.Add(new MySqlParameter("@tid", tid));
                    sp.Add(new MySqlParameter("@bid_dt", nxtBidDt));
                    sp.Add(new MySqlParameter("@bid_tm", nxtTm2));
                    sp.Add(new MySqlParameter("@amt", nxtAmt2));
                    sp.Add(new MySqlParameter("@seq", nxtSeq));
                    db.ExeQry(sql, sp);
                    sp.Clear();
                }

                row.DefaultCellStyle.BackColor = Color.LightGreen;
                Application.DoEvents();

                if (arrLs.Contains(nxtBidDt) == false)
                {
                    arrLs.Add(nxtBidDt);
                }
            }

            string[] bidDtArr = (string[])arrLs.ToArray(typeof(string));
            foreach (string dt in bidDtArr)
            {
                sql = "insert ignore into ta_skd set spt=" + spt + ", dpt=" + dpt + ", bid_dt='" + dt + "', bid_cnt=1, wdt=curdate()";
                db.ExeQry(sql);
            }

            db.Close();
            this.Cursor = Cursors.Default;
            
            MessageBox.Show("처리 되었습니다.");

            dgNt["dgNt_Rslt", i].Value = "처리완료";
            dgNt.Rows[i].DefaultCellStyle.BackColor = Color.LightGreen;

            dgLs.Rows.Clear();
            btnSave.Enabled = true;
        }

        /// <summary>
        /// 일괄낙찰-결과 API 호출
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSbApi_Click(object sender, EventArgs e)
        {
            return;

            int i = 0, tCnt = 0;
            string url, jsData, bidDt, sql;
            string lawNm, spt, saNo, sucbAmt, nxtAmt, tid, sn1, sn2, pn, bidrCnt, sucbNm, minbAmt;

            dgA.Rows.Clear();

            bidDt = dtpBidDt2.Value.ToShortDateString();
            url = string.Format("https://intra.auction1.co.kr/partner/f22_sb.php?bidDt={0}", bidDt);
            jsData = net.GetHtml(url);
            dynamic x = JsonConvert.DeserializeObject(jsData);
            var items = x["items"];
            if (items == null || items.Count == 0)
            {
                MessageBox.Show("낙찰 정보가 없습니다.");
                return;
            }

            Regex rx = new Regex(@"_(\d).pdf", rxOptM);
            JArray jsArr = JArray.Parse(items.ToString());
            db.Open();
            foreach (JObject item in jsArr)
            {
                var xRow = dtCrtSpt.Rows.Cast<DataRow>().Where(t => t["_gd_cd"].ToString() == string.Format("{0}{1}", item["law"], item["spt"])).SingleOrDefault();
                if (xRow == null)
                {
                    //MessageBox.Show("법원코드 Err-" + dr["idx"].ToString());
                    continue;
                }
                spt = xRow.Field<UInt16>("spt_cd").ToString();
                lawNm = auctCd.FindCsNm(spt);

                //if (lawNm != "대전-천안") continue;

                sn1 = item["sn1"].ToString();
                sn2 = item["sn2"].ToString();
                pn = item["pn"].ToString();
                saNo = string.Format("{0}-{1}", sn1, sn2);
                if (pn != "0") saNo += "(" + pn + ")";
                sucbAmt = item["sucbAmt"].ToString();
                nxtAmt = item["2ndAmt"].ToString();
                minbAmt = item["minbAmt"].ToString();
                sucbNm = item["sucbNm"].ToString();
                bidrCnt = item["bidrCnt"].ToString();

                sql = "select tid, bid_cnt from ta_list where spt='" + spt + "' and sn1='" + sn1 + "' and sn2='" + sn2 + "' and pn='" + pn + "' and sta1=11 limit 1";
                MySqlDataReader dr = db.ExeRdr(sql);
                dr.Read();
                if (dr.HasRows)
                {
                    tCnt++;
                    i = dgA.Rows.Add();
                    tid = dr["tid"].ToString();
                    dgA["dgA_No", i].Value = (i + 1).ToString();
                    dgA["dgA_CS", i].Value = lawNm;
                    dgA["dgA_SaNo", i].Value = saNo;
                    dgA["dgA_SbAmt", i].Value = string.Format("{0:N0}", Convert.ToDecimal(sucbAmt));
                    dgA["dgA_2ndAmt", i].Value = string.Format("{0:N0}", Convert.ToDecimal(nxtAmt));
                    dgA["dgA_MbAmt", i].Value = string.Format("{0:N0}", Convert.ToDecimal(minbAmt));
                    dgA["dgA_SbNm", i].Value = sucbNm;
                    dgA["dgA_BidrCnt", i].Value = bidrCnt;
                    dgA["dgA_BidCnt", i].Value = dr["bid_cnt"].ToString();
                    dgA["dgA_Tid", i].Value = tid;
                    dgA["dgA_Note", i].Value = string.Empty;
                }
                dr.Close();
            }
            db.Close();

            if (tCnt == 0)
            {
                MessageBox.Show("처리할 물건이 없습니다.");
            }
        }

        /// <summary>
        /// 일괄 낙찰 처리
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSucbAll_Click(object sender, EventArgs e)
        {
            return;

            bool sbPrc = false;
            int nxtSeq;
            decimal minbAmt;
            string sql, bidDt, tid, sucbAmt, nxtAmt, sucbNm, bidrCnt, bidCnt;

            Dictionary<string, string> dicSms = new Dictionary<string, string>();

            if (MessageBox.Show("일괄 낙찰 처리를 하시겠습니까?", "일괄 낙찰 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            bidDt = dtpBidDt2.Value.ToShortDateString();

            foreach (DataGridViewRow row in dgA.Rows)
            {
                sbPrc = false;
                tid = row.Cells["dgA_Tid"].Value.ToString();
                sucbAmt = row.Cells["dgA_SbAmt"].Value.ToString().Replace(",", string.Empty);
                nxtAmt = row.Cells["dgA_2ndAmt"].Value.ToString().Replace(",", string.Empty);
                minbAmt = Convert.ToDecimal(row.Cells["dgA_MbAmt"].Value.ToString().Replace(",", string.Empty));
                bidrCnt = row.Cells["dgA_BidrCnt"].Value.ToString();
                bidCnt = row.Cells["dgA_BidCnt"].Value.ToString();
                sucbNm = row.Cells["dgA_SbNm"].Value.ToString();

                db.Open();
                sql = "select max(seq) as curSeq from ta_hist where tid='" + tid + "'";
                MySqlDataReader dr = db.ExeRdr(sql);                
                dr.Read();
                if (dr["curSeq"] == DBNull.Value)
                {
                    //오류 기록
                    nxtSeq = 1;
                }
                else
                {
                    nxtSeq = Convert.ToInt32(dr["curSeq"].ToString()) + 1;
                }
                dr.Close();

                //2중 처리를 막는다-그러나 불확실 하다?
                sql = "select idx, amt from ta_hist where tid=" + tid + " and sta=1210 and bid_dt='" + bidDt + "' limit 1";
                dr = db.ExeRdr(sql);
                if (dr.HasRows) sbPrc = true;
                dr.Close();
                db.Close();

                if (sbPrc == true)
                {
                    row.Cells["dgA_Note"].Value = "낙찰처리된 사건";
                    continue;
                }

                //2회 입찰시 판별 및 처리(당회 최저가로 비교)
                if (bidCnt != "1")
                {
                    sql = "select idx, amt from ta_hist where tid=" + tid + " and bid_dt='" + bidDt + "' order by seq";
                    DataTable dt = db.ExeDt(sql);
                    if (dt.Rows.Count > 1)
                    {
                        db.Open();
                        if (Convert.ToDecimal(dt.Rows[0]["amt"]) == minbAmt)
                        {
                            sql = "delete from ta_hist where tid=" + tid + " and idx='" + dt.Rows[1]["idx"].ToString() + "'";
                            db.ExeQry(sql);
                        }
                        else if (Convert.ToDecimal(dt.Rows[1]["amt"]) == minbAmt)
                        {
                            sql = "update ta_hist set sta=1111 where tid=" + tid + " and idx='" + dt.Rows[0]["idx"].ToString() + "'";
                            db.ExeQry(sql);

                            sql = "update ta_list set fb_cnt=(fb_cnt+1) where tid=" + tid;
                            db.ExeQry(sql);
                        }
                        db.Close();
                    }
                }

                db.Open();
                //낙찰
                sql = "insert into ta_hist set tid='" + tid + "', seq='" + nxtSeq + "', bid_dt='" + bidDt + "', amt='" + sucbAmt + "', bidr_cnt='" + bidrCnt + "', sucb_nm='" + sucbNm + "', sta='1210'";
                db.ExeQry(sql);

                //차순위
                if (nxtAmt != "0")
                {
                    sql = "insert into ta_hist set tid='" + tid + "', seq='" + (nxtSeq+1) + "', bid_dt='" + bidDt + "', amt='" + nxtAmt + "', sta='1212'";
                    db.ExeQry(sql);
                }

                sql = "update ta_list set sta1='12', sta2='1210', sucb_dt='" + bidDt + "', sucb_amt='" + sucbAmt + "' where tid='" + tid + "' and bid_dt='" + bidDt + "'";
                db.ExeQry(sql);
                db.Close();

                row.Cells["dgA_Note"].Value = "처리 완료";
                row.DefaultCellStyle.BackColor = Color.LightGreen;
                if (row.Displayed == false) dgA.FirstDisplayedScrollingRowIndex = row.Index;

                if (dicSms.ContainsKey(tid) == false) dicSms.Add(tid, "매각");
                Application.DoEvents();
            }

            //sms 발송대상 물건 저장
            if (dicSms.Count > 0)
            {
                db.Open();
                foreach (KeyValuePair<string, string> kvp in dicSms)
                {
                    sql = "insert ignore into db_tank.tx_sms set tid='" + kvp.Key + "', state='" + kvp.Value + "', wdt=curdate(), wtm=curtime()";
                    db.ExeQry(sql);
                }
                db.Close();
            }
        }

        /// <summary>
        /// 일괄변경-공고 검색
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSrchNt_Click(object sender, EventArgs e)
        {
            int i = 0, n = 0, fbCnt = 0;
            string sql, bidDt, spt, dpt;

            dgT.SelectionChanged -= dgT_SelectionChanged;
            dgT.Rows.Clear();
            dgS.Rows.Clear();

            bidDt = dtpBidDt3.Value.ToShortDateString();
            sql = "select idx, spt, dpt, bid_dt from ta_skd where bid_dt='" + bidDt + "' order by spt, dpt";

            this.Cursor = Cursors.WaitCursor;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                n++;
                var xDpt = dtDptCd.Rows.Cast<DataRow>().Where(t => t["cs_cd"].ToString() == dr["spt"].ToString() && t["dpt_cd"].ToString() == dr["dpt"].ToString()).FirstOrDefault();
                dpt = (xDpt == null || dr["dpt"].ToString() == "0") ? string.Empty : xDpt.Field<string>("dpt_nm");
                i = dgT.Rows.Add();
                dgT["dgT_No", i].Value = i + 1;
                dgT["dgT_CS", i].Value = auctCd.FindCsNm(dr["spt"].ToString());
                dgT["dgT_Dpt", i].Value = dpt;
                dgT["dgT_BidDt", i].Value = string.Format("{0:yyyy-MM-dd}", dr["bid_dt"]);
                dgT["dgT_SptCd", i].Value = dr["spt"];
                dgT["dgT_DptCd", i].Value = dr["dpt"];
            }
            dr.Close();
            db.Close();
            dgT.ClearSelection();
            this.Cursor = Cursors.Default;

            if (n == 0)
            {
                MessageBox.Show("검색된 경매일정이 없습니다.");
                return;
            }

            db.Open();
            foreach (DataGridViewRow row in dgT.Rows)
            {
                spt = row.Cells["dgT_SptCd"].Value.ToString();
                dpt = row.Cells["dgT_DptCd"].Value.ToString();
                bidDt = dtpBidDt3.Value.ToShortDateString();

                sql = "select count(*) from ta_list where sta1=11 and spt=" + spt + " and dpt=" + dpt + " and bid_dt='" + bidDt + "' order by sn1, sn2, pn";
                fbCnt = Convert.ToInt32(db.RowCnt(sql));
                row.Cells["dgT_FbCnt"].Value = fbCnt;
                if (fbCnt == 0)
                {
                    row.DefaultCellStyle.ForeColor = Color.Silver;
                }
            }
            db.Close();

            dgT.SelectionChanged += dgT_SelectionChanged;
        }

        /// <summary>
        /// 일괄변경-물건 목록
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgT_SelectionChanged(object sender, EventArgs e)
        {
            int i = 0, n = 0;
            string sql, bidDt, spt, dpt, state, cat;

            dgS.Rows.Clear();
            txtGiilChkCnt.Text = "0";

            if (dgT.CurrentRow == null) return;
            i = dgT.CurrentRow.Index;

            spt = dgT["dgT_SptCd", i].Value.ToString();
            dpt = dgT["dgT_DptCd", i].Value.ToString();
            bidDt = dtpBidDt3.Value.ToShortDateString();

            lblMdfyTitle.Text = string.Format("{0} > {1} > {2}", dgT["dgT_CS", i].Value, dgT["dgT_Dpt", i].Value, dgT["dgT_BidDt", i].Value);

            sql = "select tid, sn1, sn2, pn, bid_dt, bid_tm, bid_tm1, bid_tm2, minb_amt, sta2, cat3, bid_cnt from ta_list where sta1=11 and spt=" + spt + " and dpt=" + dpt + " and bid_dt='" + bidDt + "' order by sn1, sn2, pn";
            this.Cursor = Cursors.WaitCursor;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                n++;
                var xState = dtStateCd.Rows.Cast<DataRow>().Where(t => t["sta2_cd"].ToString() == dr["sta2"].ToString()).SingleOrDefault();
                state = (xState == null || dr["sta2"].ToString() == "0") ? string.Empty : xState.Field<string>("sta2_nm");

                var xCat = dtCat.Rows.Cast<DataRow>().Where(t => t["cat3_cd"].ToString() == dr["cat3"].ToString()).SingleOrDefault();
                cat = (xCat == null || dr["cat3"].ToString() == "0") ? string.Empty : xCat.Field<string>("cat3_nm");

                i = dgS.Rows.Add();
                dgS["dgS_No", i].Value = i + 1;
                dgS["dgS_Tid", i].Value = dr["tid"];
                dgS["dgS_SN", i].Value = (dr["pn"].ToString() == "0") ? string.Format("{0}-{1}", dr["sn1"], dr["sn2"]) : string.Format("{0}-{1} ({2})", dr["sn1"], dr["sn2"], dr["pn"]);
                dgS["dgS_BidDt", i].Value = string.Format("{0:yyyy.MM.dd}", dr["bid_dt"]);
                dgS["dgS_State", i].Value = state;
                dgS["dgS_StateC", i].Value = string.Empty;
                dgS["dgS_Spt", i].Value = spt;
                dgS["dgS_Sta", i].Value = dr["sta2"].ToString();
            }
            dr.Close();
            db.Close();
            dgS.ClearSelection();
            this.Cursor = Cursors.Default;

            if (n == 0)
            {
                MessageBox.Show("검색된 물건이 없습니다.");
                return;
            }
        }

        /// <summary>
        /// 일괄변경-법원 기일내역 보기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgS_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int rowIdx = 0;
            string sql, tid, url, jiwonNm, saNo;

            if (dgS.CurrentRow == null) return;
            rowIdx = dgS.CurrentRow.Index;

            tid = dgS.CurrentRow.Cells["dgS_Tid"].Value.ToString();
            sql = "select * from ta_list where tid=" + tid;

            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", dr["spt"]));
            saNo = string.Format("{0}0130{1}", dr["sn1"], dr["sn2"].ToString().PadLeft(6, '0'));
            url = "http://www.courtauction.go.kr/RetrieveRealEstSaDetailInqGiilList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
            net.Nvgt(wbr, url);
            dr.Close();
            db.Close();
        }

        /// <summary>
        /// 일괄변경-법원 기일내역 상태 체크
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkCrtState_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {            
            txtGiilChkCnt.Text = "0";

            if (dgS.SelectedRows.Count == 0)
            {
                MessageBox.Show("선택된 물건이 없습니다.");
                return;
            }

            this.Cursor = Cursors.WaitCursor;

            CheckForIllegalCrossThreadCalls = false;
            bgwork = new BackgroundWorker();
            bgwork.DoWork += Bgwork_DoWork;
            bgwork.RunWorkerCompleted += Bgwork_RunWorkerCompleted;
            bgwork.WorkerReportsProgress = false;
            bgwork.WorkerSupportsCancellation = true;

            bgwork.RunWorkerAsync();
        }

        /// <summary>
        /// 일괄변경-법원 기일내역 상태 체크
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            string sql, tid, url, jiwonNm, saNo, tkPn, pn = "", html = "", prevSaNo = "";
            string bidDt, dtDvsn, bidRslt = "", lastRslt = "";

            Regex rx = new Regex(@"(\d+)\-(\d+)[ ]*(\((\d+)\))*");
            HAPDoc doc = new HAPDoc();

            webCnt = 0;
            foreach (DataGridViewRow row in dgS.SelectedRows.Cast<DataGridViewRow>().Reverse())
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                txtGiilChkCnt.Text = webCnt.ToString();
                
                Match match = rx.Match(row.Cells["dgS_SN"].Value.ToString());
                jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row.Cells["dgS_Spt"].Value));
                saNo = string.Format("{0}0130{1}", match.Groups[1].Value, match.Groups[2].Value.PadLeft(6, '0'));
                tkPn = match.Groups[4].Value.Trim();
                if (tkPn == string.Empty) tkPn = "1";

                if (saNo != prevSaNo)
                {
                    url = "http://www.courtauction.go.kr/RetrieveRealEstSaDetailInqGiilList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
                    html = net.GetHtml(url);
                    if (html.Contains("검색결과가 없습니다"))
                    {
                        row.Cells["dgS_Rslt"].Value = "기일내역 없음";
                        continue;
                    }
                }                

                doc.LoadHtml(html);
                HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='기일내역 표']/tbody/tr[@class='Ltbl_list_lvl0' or @class='Ltbl_list_lvl1']");
                if (ncTr == null) continue;

                lastRslt = "";
                foreach (HtmlNode ndTr in ncTr)
                {
                    HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                    if (ncTd.Count == 7)
                    {
                        if (ncTd[0].FirstChild != null)
                        {
                            pn = ncTd[0].FirstChild.InnerText.Trim();
                        }
                        bidDt = ncTd[2].FirstChild.InnerText.Trim().Substring(0, 10);
                        dtDvsn = ncTd[3].FirstChild.InnerText.Trim();
                        bidRslt = ncTd[6].FirstChild.InnerText.Trim();
                    }
                    else if (ncTd.Count == 5)
                    {
                        bidDt = ncTd[0].FirstChild.InnerText.Trim().Substring(0, 10);
                        dtDvsn = ncTd[1].FirstChild.InnerText.Trim();
                        bidRslt = ncTd[4].FirstChild.InnerText.Trim();
                    }
                    else { }

                    if (pn == tkPn)
                    {
                        lastRslt = bidRslt;
                    }

                    if (lastRslt != "" && pn != tkPn) break;
                }
                row.Cells["dgS_StateC"].Value = lastRslt;
                prevSaNo = saNo;
            }
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            MessageBox.Show("법원 기일 확인 완료");

            this.Cursor = Cursors.Default;
        }

        /// <summary>
        /// 일괄변경-처리
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnMdfy_Click(object sender, EventArgs e)
        {
            string sql, tid, Hidx, bidDt, sta;
            int selCnt = 0;

            DataTable dtH = new DataTable();
            Dictionary<string, string> dicSms = new Dictionary<string, string>();

            selCnt = dgS.SelectedRows.Count;
            if (selCnt == 0)
            {
                MessageBox.Show("변경처리할 물건을 선택 해 주세요.");
                return;
            }

            if (MessageBox.Show("선택하신 " + selCnt.ToString() + "건을 \r\n일괄 변경 처리 하시겠습니까?", "일괄 변경", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            foreach (DataGridViewRow row in dgS.SelectedRows.Cast<DataGridViewRow>().Reverse())
            {
                dtH.Rows.Clear();

                tid = row.Cells["dgS_Tid"].Value.ToString();
                bidDt = row.Cells["dgS_BidDt"].Value.ToString();
                sta = row.Cells["dgS_Sta"].Value.ToString();
                sql = "select idx from ta_hist where tid='" + tid + "' and bid_dt='" + bidDt + "'";
                dtH = db.ExeDt(sql);
                if (dtH.Rows.Count == 0)
                {
                    //Error
                    row.Cells["dgS_Rslt"].Value = "오류!-일정에 없음";
                    row.DefaultCellStyle.BackColor = Color.LightGray;
                    continue;
                }

                db.Open();
                foreach (DataRow r in dtH.Rows)
                {
                    Hidx = r["idx"].ToString();
                    sql = "update ta_hist set sta='1310' where idx='" + Hidx + "'";
                    db.ExeQry(sql);

                    sql = "insert into db_tank.tx_mdfy set tid='" + tid + "', sta='" + sta + "', h_idx='" + Hidx + "', wdt=curdate()";
                    db.ExeQry(sql);
                }
                sql = "update ta_list set sta1='13', sta2='1310' where tid='" + tid + "'";
                db.ExeQry(sql);
                db.Close();

                row.Cells["dgS_Rslt"].Value = "처리 완료";
                row.DefaultCellStyle.BackColor = Color.LightGreen;
                if (row.Displayed == false) dgS.FirstDisplayedScrollingRowIndex = row.Index;

                if (dicSms.ContainsKey(tid) == false) dicSms.Add(tid, "변경");
                Application.DoEvents();
            }

            //sms 발송대상 물건 저장
            if (dicSms.Count > 0)
            {
                db.Open();
                foreach (KeyValuePair<string, string> kvp in dicSms)
                {
                    sql = "insert ignore into db_tank.tx_sms set tid='" + kvp.Key + "', state='" + kvp.Value + "', wdt=curdate(), wtm=curtime()";
                    db.ExeQry(sql);
                }
                db.Close();
            }

            dgS.ClearSelection();
            MessageBox.Show("[변경]처리 되었습니다.");
        }

        /// <summary>
        /// 변경 복원 검색
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSrchRec_Click(object sender, EventArgs e)
        {
            int i = 0, n = 0;
            string sql, dpt,sn1, sn2, pn, saNo, state;

            dgR.Rows.Clear();

            db.Open();
            sql = "select M.*,spt,dpt,sn1,sn2,pn,sta2 from db_tank.tx_mdfy M , ta_list L where M.tid=L.tid and M.wdt='" + dtpRecDt.Value.ToShortDateString() + "' order by idx desc";
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                n++;

                sn1 = dr["sn1"].ToString();
                sn2 = dr["sn2"].ToString();
                pn = dr["pn"].ToString();
                saNo = string.Format("{0}-{1}", sn1, sn2);
                if (pn != "0") saNo += "(" + pn + ")";

                var xDpt = dtDptCd.Rows.Cast<DataRow>().Where(t => t["cs_cd"].ToString() == dr["spt"].ToString() && t["dpt_cd"].ToString() == dr["dpt"].ToString()).FirstOrDefault();
                dpt = (xDpt == null || dr["dpt"].ToString() == "0") ? string.Empty : xDpt.Field<string>("dpt_nm");

                var xState = dtStateCd.Rows.Cast<DataRow>().Where(t => t["sta2_cd"].ToString() == dr["sta2"].ToString()).SingleOrDefault();
                state = (xState == null || dr["sta2"].ToString() == "0") ? string.Empty : xState.Field<string>("sta2_nm");

                i = dgR.Rows.Add();
                dgR["dgR_No", i].Value = i + 1;
                dgR["dgR_Tid", i].Value = dr["tid"].ToString();
                dgR["dgR_CS", i].Value = auctCd.FindCsNm(dr["spt"].ToString());
                dgR["dgR_Dpt", i].Value = dpt;
                dgR["dgR_SN", i].Value = saNo;
                dgR["dgR_State", i].Value = state;
                dgR["dgR_Hidx", i].Value = dr["h_idx"].ToString();
                dgR["dgR_Sta", i].Value = dr["sta"].ToString();
            }
            db.Close();

            if (n == 0)
            {
                MessageBox.Show("검색된 물건이 없습니다.");
            }
        }

        /// <summary>
        /// 변경 복원 처리
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRecover_Click(object sender, EventArgs e)
        {
            int selCnt = 0;
            string sql, sta1, sta2;

            selCnt = dgR.SelectedRows.Count;
            if (selCnt == 0)
            {
                MessageBox.Show("변경처리할 물건을 선택 해 주세요.");
                return;
            }

            if (MessageBox.Show("선택하신 " + selCnt.ToString() + "건을 \r\n일괄 복원 하시겠습니까?", "일괄 복원", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            db.Open();
            foreach (DataGridViewRow row in dgR.SelectedRows.Cast<DataGridViewRow>().Reverse())
            {
                sta2 = row.Cells["dgR_Sta"].Value.ToString();
                sta1 = sta2.Substring(0, 2);

                //sql = "update ta_hist set sta='" + sta2 + "' where idx='" + row.Cells["dgR_Hidx"].Value.ToString() + "'";
                sql = "update ta_hist set sta='1110' where idx='" + row.Cells["dgR_Hidx"].Value.ToString() + "'";
                db.ExeQry(sql);

                sql = "update ta_list set sta1='" + sta1 + "', sta2='" + sta2 + "' where tid='" + row.Cells["dgR_Tid"].Value.ToString() + "'";
                db.ExeQry(sql);

                row.Cells["dgR_Rslt"].Value = "복원 완료";
                row.DefaultCellStyle.BackColor = Color.LightGreen;

                Application.DoEvents();
            }
            db.Close();

            MessageBox.Show("[복원]처리 되었습니다.");
        }


        /// <summary>
        /// 유찰 복원 검색
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnFbSrch_Click(object sender, EventArgs e)
        {
            int i = 0;
            string sql, bidDt, csCd, state, dpt;

            bidDt = dtpFb.Value.ToShortDateString();

            dg.Rows.Clear();
            sql = "select L.tid, crt, spt, dpt, sn1, sn2, pn, sta2, L.bid_dt, fb_cnt, H.idx, H.sta, H.amt, H.bid_dt AS fbDt FROM ta_list L , ta_hist H where L.tid=H.tid and H.bid_dt='" + bidDt + "' and sta=1111 and L.bid_dt > '" + bidDt + "' GROUP by L.tid";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                csCd = dr["spt"].ToString();
                var xState = dtStateCd.Rows.Cast<DataRow>().Where(t => t["sta2_cd"].ToString() == dr["sta2"].ToString()).SingleOrDefault();
                state = (xState == null || dr["sta2"].ToString() == "0") ? string.Empty : xState.Field<string>("sta2_nm");                
                var xDpt = dtDptCd.Rows.Cast<DataRow>().Where(t => t["cs_cd"].ToString() == csCd && t["dpt_cd"].ToString() == dr["dpt"].ToString()).SingleOrDefault();
                dpt = (xDpt == null || dr["dpt"].ToString() == "0") ? string.Empty : xDpt.Field<string>("dpt_nm");
                
                i = dg.Rows.Add();
                dg["dg_No", i].Value = i + 1;
                dg["dg_Tid", i].Value = dr["tid"];
                dg["dg_CS", i].Value = auctCd.FindCsNm(csCd);
                dg["dg_Dpt", i].Value = dpt;
                dg["dg_SN", i].Value = (dr["pn"].ToString() == "0") ? string.Format("{0}-{1}", dr["sn1"], dr["sn2"]) : string.Format("{0}-{1} ({2})", dr["sn1"], dr["sn2"], dr["pn"]);
                dg["dg_BidDt", i].Value = dr["bid_dt"].ToString().Contains("0001") ? "" : string.Format("{0:yyyy-MM-dd}", dr["bid_dt"]);
                dg["dg_FbDt", i].Value = dr["fbDt"].ToString().Contains("0001") ? "" : string.Format("{0:yyyy-MM-dd}", dr["fbDt"]);
                dg["dg_State", i].Value = state;
                dg["dg_FbCnt", i].Value = dr["fb_cnt"];
                dg["dg_MinbAmt", i].Value = string.Format("{0:N0}", dr["amt"]);
                dg["dg_Hidx", i].Value = dr["idx"];
            }
            dr.Close();
            db.Close();
        }

        /// <summary>
        /// 유찰 복원 처리
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnFbRecover_Click(object sender, EventArgs e)
        {
            decimal fbCnt;
            string sql, tid, bidDt, minbAmt, sta1, sta2, Hidx;

            if (MessageBox.Show("유찰 복원을 하시겠습니까?", "유찰 복원", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            db.Open();
            foreach (DataGridViewRow row in dg.Rows)
            {                
                tid = row.Cells["dg_Tid"].Value.ToString();
                fbCnt = Convert.ToDecimal(row.Cells["dg_FbCnt"].Value) - 1;
                minbAmt = row.Cells["dg_MinbAmt"].Value.ToString().Replace(",", string.Empty);
                bidDt = row.Cells["dg_FbDt"].Value.ToString();
                sta2 = (fbCnt == 0) ? "1110" : "1111";
                sta1 = sta2.Substring(0, 2);
                Hidx= row.Cells["dg_Hidx"].Value.ToString();

                sql = "delete from ta_hist where tid='" + tid + "' and bid_dt > '" + bidDt + "'";
                db.ExeQry(sql);
                
                sql = "update ta_hist set sta='1110' where tid='" + tid + "' and idx='" + Hidx + "'";
                db.ExeQry(sql);
                
                sql = "update ta_list set minb_amt='" + minbAmt + "', fb_cnt='" + fbCnt + "', sta1='" + sta1 + "', sta2='" + sta2 + "', bid_dt='" + bidDt + "' where tid='" + tid + "'";
                db.ExeQry(sql);
                
                row.Cells["dg_Rslt"].Value = "완료";
                row.DefaultCellStyle.BackColor = Color.LightGreen;
                if (row.Displayed == false) dg.FirstDisplayedScrollingRowIndex = row.Index;
                Application.DoEvents();
            }
            db.Close();

            MessageBox.Show("처리 되었습니다.");
        }
    }
}
