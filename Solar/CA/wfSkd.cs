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

namespace Solar.CA
{
    public partial class wfSkd : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        AuctCd auctCd = new AuctCd();

        DataTable dtLawCd, dtDptCd; //법원, 계

        string myWeb = Properties.Settings.Default.myWeb;

        public wfSkd()
        {
            InitializeComponent();
            this.Shown += WfSkd_Shown;
        }

        private void WfSkd_Shown(object sender, EventArgs e)
        {
            wbr.Navigate(myWeb + "SOLAR/auctSkd.php");
            wbr.DocumentCompleted += Wbr_DocumentCompleted;

            //전체 법원별 계코드 DataTable
            dtDptCd = db.ExeDt("select C.ca_cd, crt_cd, spt_cd, cs_cd, dpt_cd, dpt_nm from ta_cd_cs C , ta_cd_dpt D where C.ca_cd=D.ca_cd order by dpt_cd");

            //법원 전체 코드
            dtLawCd = auctCd.DtLawInfo();
            DataRow row = dtLawCd.NewRow();
            row["csNm"] = "-선택-";
            row["csCd"] = "";
            dtLawCd.Rows.InsertAt(row, 0);

            cbxCs.DataSource = dtLawCd.Copy();
            cbxCs.DisplayMember = "csNm";
            cbxCs.ValueMember = "csCd";

            cbxCs.SelectedIndexChanged += CbxCs_SelectedIndexChanged;
        }

        private void CbxCs_SelectedIndexChanged(object sender, EventArgs e)
        {
            string spt = "0";

            if (cbxCs.SelectedIndex > 0)
            {
                spt = cbxCs.SelectedValue.ToString();
            }
            DataView dvDpt = dtDptCd.DefaultView;
            dvDpt.RowFilter = string.Format("spt_cd='{0}'", spt);
            DataTable dtDpt = dvDpt.ToTable();
            DataRow row = dtDpt.NewRow();
            row["dpt_nm"] = "-선택-";
            row["dpt_cd"] = "";
            dtDpt.Rows.InsertAt(row, 0);
            cbxDpt.DataSource = dtDpt;
            cbxDpt.DisplayMember = "dpt_nm";
            cbxDpt.ValueMember = "dpt_cd";
        }

        private void Wbr_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            HtmlDocument hd = wbr.Document;
            HtmlElementCollection hc = hd.GetElementsByTagName("A");
            foreach (HtmlElement el in hc)
            {
                if (el.GetAttribute("NAME").Contains("skd_"))
                {
                    el.Click += El_Click;
                }
            }
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            ui.FormClear(splitContainer1.Panel2);
        }

        private void btnDel_Click(object sender, EventArgs e)
        {
            string idx, sql;

            idx = txtIdx.Text;
            if (idx == string.Empty)
            {
                MessageBox.Show("선택된 일정이 없습니다.");
                return;
            }

            if (MessageBox.Show("선택한 일정을 정말 삭제 하시겠습니까?", "삭제확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            sql = "select tid from ta_list where sta1=11 and spt='" + cbxCs.SelectedValue.ToString() + "' and dpt='" + cbxDpt.SelectedValue.ToString() + "' and bid_dt='" + dtpBidDt.Value.ToShortDateString() + "' limit 1";
            db.Open();
            bool exist = db.ExistRow(sql);
            db.Close();

            if (exist)
            {
                MessageBox.Show("선택된 일정에 진행사건이 있습니다.\r\n개발자에게 요청하세요~");
                return;
            }

            sql = "delete from ta_skd where idx=" + idx;
            db.Open();
            db.ExeQry(sql);
            db.Close();

            MessageBox.Show("삭제 되었습니다.");
            HtmlElement elForm = wbr.Document.GetElementById("fmSkd");
            if (elForm != null)
            {
                elForm.InvokeMember("submit");
            }
            ui.FormClear(splitContainer1.Panel2);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            string sql, cvp;

            if (cbxCs.SelectedIndex == 0 || cbxDpt.SelectedIndex == 0)
            {
                MessageBox.Show("일정정보를 입력 해 주세요.");
                return;
            }

            List<MySqlParameter> sp = new List<MySqlParameter>();
            cvp = "idx=@idx, spt=@spt, dpt=@dpt, bid_dt=@bid_dt, bid_cnt=@bid_cnt, bid_tm1=@bid_tm1, bid_tm2=@bid_tm2, bid_tm3=@bid_tm3, wdt=curdate()";
            sql = "insert into ta_skd set " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
            sp.Add(new MySqlParameter("@idx", txtIdx.Text));
            sp.Add(new MySqlParameter("@spt", cbxCs.SelectedValue));
            sp.Add(new MySqlParameter("@dpt", cbxDpt.SelectedValue));
            sp.Add(new MySqlParameter("@bid_dt", dtpBidDt.Value.ToShortDateString()));
            sp.Add(new MySqlParameter("@bid_cnt", cbxBidCnt.Text));
            sp.Add(new MySqlParameter("@bid_tm1", mtxtBidTm1.Text + ":00"));
            sp.Add(new MySqlParameter("@bid_tm2", mtxtBidTm2.Text + ":00"));
            sp.Add(new MySqlParameter("@bid_tm3", mtxtBidTm3.Text + ":00"));
            db.Open();
            db.ExeQry(sql, sp);
            sp.Clear();
            db.Close();

            MessageBox.Show("저장 되었습니다.");
            HtmlElement elForm = wbr.Document.GetElementById("fmSkd");
            if (elForm != null)
            {
                elForm.InvokeMember("submit");
            }
        }

        private void El_Click(object sender, HtmlElementEventArgs e)
        {
            string idx, sql;

            cbxCs.SelectedIndexChanged -= CbxCs_SelectedIndexChanged;
            ui.FormClear(splitContainer1.Panel2);

            HtmlElement el = (HtmlElement)sender;
            idx = el.GetAttribute("NAME").Replace("skd_", string.Empty);

            sql = "select * from ta_skd where idx=" + idx;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();

            DataView dvDpt = dtDptCd.DefaultView;
            dvDpt.RowFilter = string.Format("spt_cd='{0}'", dr["spt"]);
            cbxDpt.DataSource = dvDpt;
            cbxDpt.DisplayMember = "dpt_nm";
            cbxDpt.ValueMember = "dpt_cd";
            cbxDpt.SelectedValue = dr["dpt"];
            cbxCs.SelectedValue = dr["spt"].ToString();
            txtIdx.Text = idx;
            dtpBidDt.Value = Convert.ToDateTime(dr["bid_dt"]);            
            cbxBidCnt.Text = dr["bid_cnt"].ToString();
            mtxtBidTm1.Text = dr["bid_tm1"].ToString();
            mtxtBidTm2.Text = dr["bid_tm2"].ToString();
            mtxtBidTm3.Text = dr["bid_tm3"].ToString();
            dr.Close();
            db.Close();
                        
            cbxCs.SelectedIndexChanged += CbxCs_SelectedIndexChanged;
        }
    }
}
