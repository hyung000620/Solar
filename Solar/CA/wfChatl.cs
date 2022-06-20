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

namespace Solar.CA
{
    public partial class wfChatl : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        AuctCd auctCd = new AuctCd();
        NetUtil net = new NetUtil();

        //매각장소유형
        Dictionary<string, string> dicPlace = new Dictionary<string, string>();

        //물품종류
        Dictionary<string, string> dicCat = new Dictionary<string, string>();

        public wfChatl()
        {
            InitializeComponent();

            init();
        }

        private void init()
        {
            ui.DgSetRead(dg, 0);
            ui.DgSetRead(dgL, 0);

            //매각장소유형
            dicPlace.Add("0", "");
            dicPlace.Add("1", "공장");
            dicPlace.Add("2", "동식물사육재배장");
            dicPlace.Add("3", "소매점");
            dicPlace.Add("4", "도매점");
            dicPlace.Add("5", "가정집");
            dicPlace.Add("6", "사무실");
            dicPlace.Add("7", "서비스제공시설");
            dicPlace.Add("8", "보관시설");

            //물품종류
            dicCat.Add("1", "농수축임산물");
            dicCat.Add("2", "예술/수집품");
            dicCat.Add("3", "가전/생활용품");
            dicCat.Add("4", "사무/가구");
            dicCat.Add("5", "식음료");
            dicCat.Add("6", "의약품");
            dicCat.Add("7", "의류/잡화");
            dicCat.Add("8", "귀금속");
            dicCat.Add("9", "운송/장비/기계");
            dicCat.Add("10", "컴퓨터/전기/통신기계");
            dicCat.Add("11", "회원권/유가증권");
            dicCat.Add("12", "기타권리");
            
            dgL.AutoGenerateColumns = false;
        }

        private void btnSrch_Click(object sender, EventArgs e)
        {
            int i = 0;
            decimal totRowCnt = 0;
            string sql = "", cond = "";

            dg.SelectionChanged -= dg_SelectionChanged;
            dg.Rows.Clear();
            this.Cursor = Cursors.WaitCursor;

            db.Open();
            sql = "select count(*) from tc_list";
            totRowCnt = (decimal)((Int64)(db.RowCnt(sql)));

            sql = "select *, date_format(bid_dt,'%Y-%m-%d') as bid_dt_as, date_format(wdt,'%Y-%m-%d') as wdt_as from tc_list order by tid desc";
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                i = dg.Rows.Add();
                dg["dg_No", i].Value = totRowCnt - i;
                dg["dg_Tid", i].Value = dr["tid"];
                dg["dg_SN", i].Value = string.Format("{0}-{1} ({2})", dr["sn1"], dr["sn2"], dr["pn"]);
                dg["dg_Adrs", i].Value = dr["adrs"];
                dg["dg_BidDt", i].Value = dr["bid_dt_as"];                
                dg["dg_Place", i].Value = dicPlace[dr["place_type"].ToString()];
                dg["dg_Stop", i].Value = (dr["stop"].ToString() == "1") ? "Y" : "";
                dg["dg_Wdt", i].Value = dr["wdt_as"];
            }
            dr.Close();
            db.Close();
            dg.ClearSelection();            
            ui.FormClear(tabDtl);

            dgL.DataSource = null;
            this.Cursor = Cursors.Default;

            dg.SelectionChanged += dg_SelectionChanged;
        }

        private void dg_SelectionChanged(object sender, EventArgs e)
        {
            int i = 0, n = 0;
            string sql = "", tid = "", cat1 = "";

            ui.FormClear(tabDtl);

            //dgL.DataSource = null;

            if (dg.CurrentRow == null) return;
            i = dg.CurrentRow.Index;
            tid = dg["dg_Tid", i].Value.ToString();
                        
            sql = "select * from tc_list where tid=" + tid + " limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            if (!dr.HasRows)
            {
                dr.Close();
                db.Close();
                return;
            }
            dr.Read();
            lblTid.Text = tid;
            lblWdt.Text = string.Format("{0:yyyy.MM.dd (ddd)}", dr["wdt"]);

            txtSaNo.Text = dr["sn"].ToString();
            txtPdNo.Text = dr["pn"].ToString();
            txtCSCd.Text = string.Format("{0}{1}", dr["crt"], dr["spt"]);
            txtCltrNo.Text = dr["cltr_no"].ToString();
            txtBidDtm.Text = string.Format("{0:yyyy.MM.dd}", dr["bid_dt"]) + " " + dr["bid_tm"].ToString().Substring(0, 5);
            txtPlaceType.Text = dicPlace[dr["place_type"].ToString()];
            txtLsTitle.Text = dr["ls_title"].ToString();
            txtCatSrch.Text = dr["cat_srch"].ToString();
            txtAdrs.Text = dr["adrs"].ToString();            
            txtCoordX.Text = dr["x"].ToString();
            txtCoordY.Text = dr["y"].ToString();
            txtNote.Text = dr["pd_note"].ToString();
            txtApslAmt.Text = string.Format("{0:N0}", dr["apsl_amt"]);
            txtMinbAmt.Text = string.Format("{0:N0}", dr["minb_amt"]);
            dr.Close();
            db.Close();

            sql = "select no, pd_nm, qty, std, amt, cat, note from tc_ls where tid=" + tid + " order by idx";
            DataTable dt = db.ExeDt(sql);
            dgL.DataSource = dt;

            dgL.ClearSelection();
        }

        private void lnkCaEno_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string url = "", jiwonNm = "", cltrNo = "", pdNo = "";

            tabcSrch.SelectedTab = tabWbr;
            jiwonNm = auctCd.FindLawNm(txtCSCd.Text, true);
            cltrNo = txtCltrNo.Text;
            pdNo = txtPdNo.Text;

            url = "http://www.courtauction.go.kr/RetrieveMvEstMulDetailInfo.laf?";
            url += "jiwonNm=" + jiwonNm + "&srnID=PNO102004&saNo=" + cltrNo + "&page=default40&maemulSer=" + pdNo;

            net.Nvgt(wbr, url);
        }
    }
}
