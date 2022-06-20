using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using HtmlAgilityPack;
using hap = HtmlAgilityPack;
using HAPDoc = HtmlAgilityPack.HtmlDocument;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Solar.CA;

namespace Solar.PA
{
    public partial class wfTrust : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        AuctCd auctCd = new AuctCd();
        NetUtil net = new NetUtil();

        DataTable dtTrust;

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        decimal totRowCnt = 0;
        string cdtn = "";

        public wfTrust()
        {
            InitializeComponent();

            init();
        }

        private void init()
        {
            string sql;

            ui.DgSetRead(dg, 0);
            ui.DgSetRead(dgS, 0);
            ui.SetPagn(panPagn);
            dgS.MultiSelect = true;
            
            sql = "select * from td_cd";
            dtTrust = db.ExeDt(sql);
            DataRow row = dtTrust.NewRow();
            row["co_cd"] = 0;
            row["co_nm"] = "-선택-";
            dtTrust.Rows.InsertAt(row, 0);
            cbxCoCd.DataSource = dtTrust;
            cbxCoCd.DisplayMember = "co_nm";
            cbxCoCd.ValueMember = "co_cd";

            cbxSrchCoCd.DataSource = dtTrust.Copy();
            cbxSrchCoCd.DisplayMember = "co_nm";
            cbxSrchCoCd.ValueMember = "co_cd";
        }

        private void btnSrch_Click(object sender, EventArgs e)
        {
            string sql;

            cdtn = "1";
            dg.Rows.Clear();

            List<string> cdtnList = new List<string>();

            if (cbxSrchCoCd.SelectedIndex > 0)
            {
                cdtnList.Add("co_cd=" + cbxSrchCoCd.SelectedValue.ToString());
            }
            if (dtpRdtBgn.Checked)
            {
                cdtnList.Add($"rdt >= '{dtpRdtBgn.Value.ToShortDateString()}'");
            }
            if (dtpRdtEnd.Checked)
            {
                cdtnList.Add($"rdt <= '{dtpRdtEnd.Value.ToShortDateString()}'");
            }
            if (chkCoordErr.Checked)
            {
                cdtnList.Add("x=0");
            }
            if (chkCltrNoErr.Checked)
            {
                cdtnList.Add("cltr_no=0");
            }
            if (cbxSrchState.SelectedIndex > 0)
            {
                if (cbxSrchState.SelectedIndex == 1)
                {
                    cdtnList.Add("state in ('진행','진행중','계약진행','공매진행','공매진행중','수의계약진행','수의계약진행중','수의계약가능','일부낙찰','유찰','공매유찰')");
                }
                else
                {
                    cdtnList.Add("state not in ('진행','진행중','계약진행','공매진행','공매진행중','수의계약진행','수의계약진행중','수의계약가능','일부낙찰','유찰','공매유찰')");
                }
            }
            if (chkExKyoBo.Checked)
            {
                cdtnList.Add("co_cd != 11");
            }

            if (cdtnList.Count > 0) cdtn = string.Join(" and ", cdtnList.ToArray());

            sql = "select COUNT(*) from td_list where " + cdtn;

            db.Open();
            totRowCnt = (decimal)((Int64)db.RowCnt(sql));
            db.Close();

            ComboBox cbx = (ComboBox)panPagn.Controls["_cbxPagn"];
            cbx.SelectedIndexChanged -= gotoPageList;
            ui.InitPagn(panPagn, totRowCnt);
            cbx.SelectedIndexChanged += gotoPageList;
            if (cbx.Items.Count > 0) cbx.SelectedIndex = 0;
        }

        private void gotoPageList(object sender, EventArgs e)
        {
            int i = 0;
            decimal startRow = 0;
            string sql = "", order = "", coNm = "";

            dg.Rows.Clear();

            ComboBox cbxPage = (ComboBox)sender;
            NumericUpDown listScale = (NumericUpDown)panPagn.Controls["_nudList"];
            startRow = (decimal.Parse(cbxPage.Text) - 1) * listScale.Value;
            dg.SelectionChanged -= dg_SelectionChanged;

            order = "rdt desc";
            sql = "select * from td_list";
            sql += " where " + cdtn + " order by " + order + " limit " + startRow + "," + listScale.Value;

            this.Cursor = Cursors.WaitCursor;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {                
                var xRow = dtTrust.Rows.Cast<DataRow>().Where(t => t["co_cd"].ToString() == dr["co_cd"].ToString()).SingleOrDefault();
                coNm = (xRow == null || dr["co_cd"].ToString() == "0") ? string.Empty : xRow.Field<string>("co_nm");

                i = dg.Rows.Add();
                dg["dg_No", i].Value = totRowCnt - startRow - i;
                dg["dg_CoNm", i].Value = coNm;
                dg["dg_Adrs", i].Value = dr["adrs"];
                dg["dg_state", i].Value = dr["state"];
                dg["dg_Rdt", i].Value = $"{dr["rdt"]:yyyy.MM.dd}";
                dg["dg_Wdt", i].Value = $"{dr["wdt"]:yyyy-MM-dd}";
                dg["dg_RefIdx", i].Value = dr["ref_idx"];
                dg["dg_CltrNo", i].Value = dr["cltr_no"];
                dg["dg_Idx", i].Value = dr["idx"];
            }
            dr.Close();
            db.Close();
            dg.ClearSelection();
            this.Cursor = Cursors.Default;
            dg.SelectionChanged += dg_SelectionChanged;
        }

        private void dg_SelectionChanged(object sender, EventArgs e)
        {
            int i = 0;
            string sql, idx;

            ui.FormClear(tabDtl);
            dgS.Rows.Clear();

            if (dg.CurrentRow == null) return;
            i = dg.CurrentRow.Index;
            idx = dg["dg_Idx", i].Value.ToString();
            lnkIdx.Text = idx;

            sql = "select * from td_list where idx=" + idx;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            cbxCoCd.SelectedValue = dr["co_cd"];
            txtRefIdx.Text = dr["ref_idx"].ToString();
            txtCltrNo.Text = dr["cltr_no"].ToString();
            txtAdrs.Text = dr["adrs"].ToString();
            txtState.Text = dr["state"].ToString();
            dtpRdt.Value = Convert.ToDateTime(dr["rdt"]);
            txtSiCd.Text = dr["si_cd"].ToString();
            txtGuCd.Text = dr["gu_cd"].ToString();
            txtDnCd.Text = dr["dn_cd"].ToString();
            txtRiCd.Text = dr["ri_cd"].ToString();
            txtCoordX.Text = dr["x"].ToString();
            txtCoordY.Text = dr["y"].ToString();

            Match match = Regex.Match(txtAdrs.Text, @"(\w+[읍면동가]\b)*[ ]*(\w+리\b)*[ ]*(\([一-龥]*\))*[ ]*(산)*(\d+)*[-]*(\d+)", rxOptM);
            if (match.Success)
            {
                txtSrchAdrs.Text = match.Value.Trim();
            }
            else
            {
                txtSrchAdrs.Text = dr["adrs"].ToString();
            }            
            dr.Close();

            if (txtCltrNo.Text != String.Empty)
            {
                sql = "select * from tb_list where cltr_no in (" + txtCltrNo.Text + ")";
                dr=db.ExeRdr(sql);
                while (dr.Read())
                {
                    i = dgS.Rows.Add();
                    dgS["dgS_No", i].Value = i + 1;
                    dgS["dgS_CltrNo", i].Value = dr["cltr_no"];
                    dgS["dgS_CltrNm", i].Value = dr["cltr_nm"];
                    dgS["dgS_BgnDt", i].Value = $"{dr["bgn_dtm"]:yyyy.MM.dd}";
                    dgS["dgS_OrgNm", i].Value = dr["org_nm"];
                }
                dr.Close();
                dgS.ClearSelection();
            }
            db.Close();
        }

        /// <summary>
        /// 웹-해당업체 물건상세 페이지
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkIdx_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string url, coNm, state;

            var xRow = dtTrust.Rows.Cast<DataRow>().Where(t => t["co_cd"].ToString() == cbxCoCd.SelectedValue.ToString()).SingleOrDefault();
            if (xRow == null || cbxCoCd.SelectedValue.ToString() == "0")
            {
                MessageBox.Show("신탁회사 코드가 없습니다.");
                return;
            }

            coNm = xRow["co_nm"].ToString();
            state = txtState.Text;

            if (coNm == "교보자산신탁")
            {                
                MessageBox.Show("[교보자산신탁]은 상세 페이지를 볼 수 없습니다.");
                return;
            }
            if (coNm == "대신자산신탁" && (state == "취소" || state == "매각완료" || state == "종결"))
            {
                MessageBox.Show("[대신자산신탁]은 진행상태가 [취소/매각완료/종결]인 경우 상세 페이지를 볼 수 없습니다.");
                return;
            }

            url = $"{xRow["home"]}{xRow["dtl_url"]}";
            url = url.Replace("xxx", txtRefIdx.Text);
            webView.Source = new Uri(url);
        }

        /// <summary>
        /// 신규
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnNew_Click(object sender, EventArgs e)
        {
            ui.FormClear(tabDtl);

            lnkIdx.Text = string.Empty;
        }

        /// <summary>
        /// 저장하기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSave_Click(object sender, EventArgs e)
        {
            string sql, cvp;

            List<MySqlParameter> sp = new List<MySqlParameter>();

            if (cbxCoCd.SelectedIndex == 0)
            {
                MessageBox.Show("신탁회사를 선택 해 주세요");
                return;
            }
            if (txtRefIdx.Text.Trim() == string.Empty)
            {
                MessageBox.Show("고유번호(업체)를 입력 해 주세요");
                return;
            }
            if (txtAdrs.Text.Trim() == string.Empty)
            {
                MessageBox.Show("제목(소재지)을 입력 해 주세요");
                return;
            }
            if (txtState.Text.Trim() == string.Empty)
            {
                MessageBox.Show("진행상태를 입력 해 주세요");
                return;
            }

            cvp = "co_cd=@co_cd, ref_idx=@ref_idx, cltr_no=@cltr_no, adrs=@adrs, state=@state, rdt=@rdt," +
                "si_cd=@si_cd, gu_cd=@gu_cd, dn_cd=@dn_cd, ri_cd=@ri_cd, x=@x, y=@y";
            sql = "insert into td_list set " + cvp + ", wdt=curdate() ON DUPLICATE KEY UPDATE " + cvp;
            sp.Add(new MySqlParameter("@co_cd", cbxCoCd.SelectedValue));
            sp.Add(new MySqlParameter("@ref_idx", txtRefIdx.Text.Trim()));
            sp.Add(new MySqlParameter("@cltr_no", txtCltrNo.Text.Trim()));
            sp.Add(new MySqlParameter("@adrs", txtAdrs.Text.Trim()));
            //sp.Add(new MySqlParameter("@ctgr", ctgr));
            sp.Add(new MySqlParameter("@state", txtState.Text.Trim()));
            sp.Add(new MySqlParameter("@rdt", dtpRdt.Value.ToShortDateString()));
            sp.Add(new MySqlParameter("@si_cd", txtSiCd.Text.Trim()));
            sp.Add(new MySqlParameter("@gu_cd", txtGuCd.Text.Trim()));
            sp.Add(new MySqlParameter("@dn_cd", txtDnCd.Text.Trim()));
            sp.Add(new MySqlParameter("@ri_cd", txtRiCd.Text.Trim()));
            sp.Add(new MySqlParameter("@x", txtCoordX.Text.Trim()));
            sp.Add(new MySqlParameter("@y", txtCoordY.Text.Trim()));
            db.Open();
            db.ExeQry(sql, sp);
            sp.Clear();
            db.Close();

            MessageBox.Show("저장 되었습니다.");

            if (dg.CurrentRow != null)
            {
                dg["dg_CltrNo", dg.CurrentRow.Index].Value = txtCltrNo.Text.Trim();
            }
            //dg_SelectionChanged(null, null);
            btnNew_Click(null, null);
        }

        private void lnkTrust_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            LinkLabel lnk = sender as LinkLabel;

            var xRow = dtTrust.Rows.Cast<DataRow>().Where(t => t["co_nm"].ToString().Contains(lnk.Text)).SingleOrDefault();
            
            webViewTrust.Source = new Uri($"{xRow["home"]}{xRow["lst_url"]}");            
        }

        /// <summary>
        /// 개별 수동수집-한국토지신탁, 코람코자산신탁
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void btnEaCrawl_Click(object sender, EventArgs e)
        {
            string url, html;
            string refIdx, adrs, ctgr=string.Empty, rgstDt, state;
            int coCd = 0;

            btnNew_Click(null, null);

            url = webViewTrust.Source.AbsoluteUri;
            html = await webViewTrust.CoreWebView2.ExecuteScriptAsync("document.documentElement.outerHTML");
            html = System.Web.Helpers.Json.Decode(html);
            
            HAPDoc doc = new HAPDoc();
            doc.LoadHtml(html);

            if (html.Contains("한국토지신탁"))
            {
                coCd = 18;                
                try
                {
                    refIdx = Regex.Match(url, @"seq=(\d+)", rxOptM).Groups[1].Value;
                    HtmlNode nd0 = doc.DocumentNode.SelectSingleNode("//div[@class='bbs-view vendue-view']");
                    adrs = nd0.SelectSingleNode(".//li[@class='subj']/dl/dd").InnerText.Trim();
                    rgstDt = nd0.SelectSingleNode(".//li[@class='date']/dl/dd").InnerText.Trim();
                    state = nd0.SelectSingleNode(".//li[@class='division']/dl/dd").InnerText.Trim();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    return;
                }
            }
            else if (html.Contains("코람코자산신탁"))
            {
                coCd = 20;
                try
                {
                    refIdx = Regex.Match(url, @"post_id=(\d+)", rxOptM).Groups[1].Value;
                    HtmlNode nd0 = doc.DocumentNode.SelectSingleNode("//section[@class='layout-bbs-view']/header");
                    string ndTxt = nd0.SelectSingleNode("./h2").InnerText.Trim();
                    Match match = Regex.Match(ndTxt, @"^\[(\w+)\](.*)", rxOptM);
                    state = match.Groups[1].Value.Trim();
                    adrs = match.Groups[2].Value.Trim();
                    rgstDt = nd0.SelectSingleNode("./p").InnerText.Trim();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    return;
                }                
            }
            else
            {
                MessageBox.Show("수동처리 대상 사이트가 아닙니다.");
                return;
            }

            cbxCoCd.SelectedValue = coCd;
            txtRefIdx.Text = refIdx;
            txtAdrs.Text = adrs;
            txtState.Text = state;
            dtpRdt.Value = Convert.ToDateTime(rgstDt);
        }

        /// <summary>
        /// 좌표/주소 코드 재매칭
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCoord_Click(object sender, EventArgs e)
        {
            if (lnkIdx.Text == "IDX" || lnkIdx.Text == string.Empty) return;

            sfMap sfMap = new sfMap() { Owner = this };
            sfMap.StartPosition = FormStartPosition.CenterScreen;
            //sfMap.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            sfMap.FormBorderStyle = FormBorderStyle.Sizable;
            sfMap.ShowDialog();
            sfMap.Dispose();
        }

        /// <summary>
        /// 주소로 공매물건 검색
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSrchPA_Click(object sender, EventArgs e)
        {
            int i;
            string sql, adrs;

            dgS.Rows.Clear();

            adrs =txtSrchAdrs.Text.Trim();
            if (adrs == string.Empty)
            {
                MessageBox.Show("검색할 주소를 입력 해 주세요.");
                return;
            }

            sql = "select * from tb_list where land_adrs like '%" + adrs + "%' or road_adrs like '%" + adrs + "%' order by bgn_dtm desc";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                i = dgS.Rows.Add();
                dgS["dgS_No", i].Value = i + 1;
                dgS["dgS_CltrNo", i].Value = dr["cltr_no"];
                dgS["dgS_CltrNm", i].Value = dr["cltr_nm"];
                dgS["dgS_BgnDt", i].Value = $"{dr["bgn_dtm"]:yyyy.MM.dd}";
                dgS["dgS_OrgNm", i].Value = dr["org_nm"];
            }
            dr.Close();
            dgS.ClearSelection();            
            db.Close();

            if (dgS.Rows.Count == 0)
            {
                MessageBox.Show("검색 결과가 없습니다.");
            }
        }

        /// <summary>
        /// 검색결과에서 cltrNo 적용
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAplyPA_Click(object sender, EventArgs e)
        {
            string cltrNo;

            if (dgS.SelectedRows.Count == 0)
            {
                MessageBox.Show("선택된 물건이 없습니다.");
                return;
            }

            List<string> lsCltrNo = new List<string>();

            txtCltrNo.Text = txtCltrNo.Text.Trim();
            if (txtCltrNo.Text.Length > 0)
            {
                lsCltrNo.AddRange(txtCltrNo.Text.Split(','));
            }

            foreach (DataGridViewRow row in dgS.SelectedRows)
            {
                cltrNo = row.Cells["dgS_CltrNo"].Value.ToString();
                if (lsCltrNo.Contains(cltrNo)) continue;

                lsCltrNo.Add(cltrNo);
            }

            txtCltrNo.Text = string.Join(",", lsCltrNo);
        }

        /// <summary>
        /// cltrNo 텍스트박스 비우기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkClearCltrNo_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            txtCltrNo.Text = String.Empty;
        }
    }
}
