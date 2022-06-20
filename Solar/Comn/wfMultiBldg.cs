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

namespace Solar.Comn
{
    public partial class wfMultiBldg : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        AuctCd auctCd = new AuctCd();
        NetUtil net = new NetUtil();
        ApiUtil api = new ApiUtil();

        decimal totRowCnt = 0;
        string cdtn = "";

        DataTable dtCat;
        Dictionary<int, string> dictMatchDvsn = new Dictionary<int, string>();

        public wfMultiBldg()
        {
            InitializeComponent();

            init();
        }

        private void init()
        {
            string sql;

            ui.DgSetRead(dg, 0);
            ui.SetPagn(panPagn);

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

            sql = "select _gd_cd, cat3_nm from ta_cd_cat where _gd_cd > 0 and cat1_cd=20 and bldg_type=1 and hide=0";
            dtCat = db.ExeDt(sql);
            DataRow row = dtCat.NewRow();
            row["_gd_cd"] = 0;
            row["cat3_nm"] = "-선택-";
            dtCat.Rows.InsertAt(row, 0);

            cbxCat.DataSource = new BindingSource(dtCat, null);            
            cbxCat.DisplayMember = "cat3_nm";
            cbxCat.ValueMember = "_gd_cd";

            cbxSrchCat.DataSource = new BindingSource(dtCat, null);
            cbxSrchCat.DisplayMember = "cat3_nm";
            cbxSrchCat.ValueMember = "_gd_cd";

            dictMatchDvsn.Add(-1, "-선택-");
            //dictMatchDvsn.Add(0, "분류전");
            dictMatchDvsn.Add(1, "텍스트");
            //dictMatchDvsn.Add(2, "이미지");
            dictMatchDvsn.Add(3, "텍스트+이미지");

            cbxSrchDvsn.DataSource = new BindingSource(dictMatchDvsn, null);
            cbxSrchDvsn.DisplayMember = "Value";
            cbxSrchDvsn.ValueMember = "Key";

            cbxMatchDvsn.DataSource = new BindingSource(dictMatchDvsn, null);
            cbxMatchDvsn.DisplayMember = "Value";
            cbxMatchDvsn.ValueMember = "Key";
        }

        private void CbxAdrsCd_SelectedIndexChanged(object sender, EventArgs e)
        {
            string sql;
            ComboBox cbx = ((ComboBox)sender);

            if (cbx == cbxSi)
            {
                sql = "select gu_nm, gu_cd from tx_cd_adrs where si_cd=" + cbxSi.SelectedValue.ToString() + " and gu_cd > 0 and dn_cd=0 and hide=0 order by gu_nm";
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
            string sql = "";

            cdtn = "1";
            dg.Rows.Clear();
            ui.FormClear(tabDtl, new string[] { });
            //lnkTid.Text = "TID";
            
            List<string> cdtnList = new List<string>();

            if (cbxSi.SelectedIndex > 0)
            {
                cdtnList.Add("si_key=" + cbxSi.SelectedValue.ToString());
            }
            if (cbxGu.SelectedIndex > 0)
            {
                cdtnList.Add("gu_key=" + cbxGu.SelectedValue.ToString());
            }
            if (cbxDn.SelectedIndex > 0)
            {
                cdtnList.Add("dong_key=" + cbxDn.SelectedValue.ToString());
            }
            if (cbxSrchCat.SelectedIndex > 0)
            {
                cdtnList.Add("pd_type=" + cbxSrchCat.SelectedValue.ToString());
            }
            if (cbxSrchDvsn.SelectedIndex > 0)
            {
                cdtnList.Add("match_type=" + cbxSrchDvsn.SelectedValue.ToString());
            }
            if (dtpDtBgn.Checked)
            {
                cdtnList.Add("wdate >='" + dtpDtBgn.Value.ToShortDateString() + "'");
            }
            if (dtpDtEnd.Checked)
            {
                cdtnList.Add("wdate <='" + dtpDtEnd.Value.ToShortDateString() + "'");
            }
            cdtnList.Add("match_type in (1,3)");

            if (cdtnList.Count > 0) cdtn = string.Join(" and ", cdtnList.ToArray());
            if (txtSrchAptCd.Text.Trim() != "")
            {
                cdtn = "apt_code IN (" + Regex.Replace(txtSrchAptCd.Text.Trim(), @"\D+", ",") + ")";   //TID 검색일 경우 모든 조건 무시
            }

            sql = "select COUNT(*) from tx_apt where " + cdtn;

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
            string sql = "", dvsn = "", order = "", cat = "";

            dg.Rows.Clear();

            DataTable dt = new DataTable();
            dt.Columns.Add("No");
            ComboBox cbxPage = (ComboBox)sender;
            NumericUpDown listScale = (NumericUpDown)panPagn.Controls["_nudList"];
            startRow = (decimal.Parse(cbxPage.Text) - 1) * listScale.Value;
            dg.SelectionChanged -= dg_SelectionChanged;

            //sort = cbxSrchSort.Text;

            order = "apt_code desc";

            if (txtSrchAptCd.Text.Trim() != "")
            {
                order = "apt_code asc";
            }

            sql = "select apt_code, dj_name, pd_type, concat(sido,' ',gugun,' ',dong,' ',ri,' ',bunji) as adrs, match_type, wdate from tx_apt";
            sql += " where " + cdtn + " order by " + order + " limit " + startRow + "," + listScale.Value;

            this.Cursor = Cursors.WaitCursor;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                DataRow xRow = dtCat.Select("_gd_cd='" + dr["pd_type"].ToString() + "'").FirstOrDefault();
                cat = (xRow == null) ? string.Empty : xRow["cat3_nm"].ToString();
                dvsn = dictMatchDvsn[Convert.ToInt32(dr["match_type"])]; ;

                i = dg.Rows.Add();
                dg["dg_No", i].Value = totRowCnt - startRow - i;
                dg["dg_AptCd", i].Value = dr["apt_code"];
                dg["dg_AptNm", i].Value = dr["dj_name"];
                dg["dg_Cat", i].Value = cat;
                dg["dg_Adrs", i].Value = dr["adrs"];
                dg["dg_Dvsn", i].Value = dvsn;
                dg["dg_Wdt", i].Value = string.Format("{0:yyyy-MM-dd}", dr["wdate"]);
            }
            dr.Close();
            db.Close();
            dg.ClearSelection();
            this.Cursor = Cursors.Default;

            dg.SelectionChanged += dg_SelectionChanged;
        }

        private void dg_SelectionChanged(object sender, EventArgs e)
        {
            int i;
            string aptCd, sql;

            ui.FormClear(tabDtl);

            if (dg.CurrentRow == null) return;
            i = dg.CurrentRow.Index;
            aptCd = dg["dg_AptCd", i].Value.ToString();
            sql = "select * from tx_apt where apt_code=" + aptCd + " limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            if (dr.HasRows == false)
            {
                MessageBox.Show("존재하지 않는 코드 입니다.");
                dr.Close();
                db.Close();
                return;
            }
            txtAptCd.Text = aptCd;
            txtAptName.Text = dr["dj_name"].ToString();
            cbxCat.SelectedValue = dr["pd_type"];
            cbxMatchDvsn.SelectedValue = Convert.ToInt32(dr["match_type"]);
            
            txtSiCd.Text = dr["si_key"].ToString();
            txtGuCd.Text = dr["gu_key"].ToString();
            txtDnCd.Text = dr["dong_key"].ToString();
            txtRiCd.Text = dr["ri_key"].ToString();
            txtSiNm.Text = dr["sido"].ToString();
            txtGuNm.Text = dr["gugun"].ToString();
            txtDnNm.Text = dr["dong"].ToString();
            txtRiNm.Text = dr["ri"].ToString();
            txtAddr2.Text = dr["bunji"].ToString();

            txtNaverCd.Text = dr["dj_no"].ToString();            
            txtKaCd.Text = dr["ka_code"].ToString();
            txtMolitCd.Text = dr["molit_code"].ToString();

            txtCntSedae.Text = dr["cnt_sedae"].ToString();
            txtCntDong.Text = dr["cnt_dong"].ToString();
            txtBuildDt.Text = dr["build_date"].ToString();
            txtConstructor.Text = dr["constructor"].ToString();
            txtCntPark.Text = dr["cnt_parking"].ToString();
            txtSedaePark.Text = dr["sedae_parking"].ToString();
            txtHeadType.Text = dr["heat_type"].ToString();
            txtHeatFuel.Text = dr["heat_fuel"].ToString();
            txtFaRatio.Text = dr["fa_ratio"].ToString();
            txtBlRatio.Text = dr["bl_ratio"].ToString();
            txtTopFloor.Text = dr["top_floor"].ToString();
            txtLowFloor.Text = dr["low_floor"].ToString();
            txtAreaKind.Text = dr["area_kind"].ToString();
            txtSubway.Text = dr["subway"].ToString();
            txtBus.Text = dr["bus"].ToString();
            txtRoad.Text = dr["road"].ToString();
            txtComforts.Text = dr["comforts"].ToString();
            txtEdu.Text = dr["education"].ToString();
            txtRelaxPark.Text = dr["relax_park"].ToString();
            txtMedical.Text = dr["medical"].ToString();
            txtPhone.Text = dr["phone"].ToString();
            txtMemo.Text = dr["memo"].ToString();
            dr.Close();
            db.Close();
        }

        /// <summary>
        /// web 링크
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkWeb_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string url = "", sgdKey = "", rletTypeCd = "";

            if (txtDnCd.Text == string.Empty || txtDnCd.Text == "0")
            {
                MessageBox.Show("먼저 주소 검색을 해 주세요.");
                return;
            }

            LinkLabel lnkName = ((LinkLabel)sender);
            if (lnkName == lnkNaver)
            {
                sgdKey = String.Format("{0}{1}{2}00", txtSiCd.Text, txtGuCd.Text, txtDnCd.Text);
                rletTypeCd = (cbxCat.SelectedValue.ToString() == "19") ? "A02" : "A01";
                url = "http://land.naver.com/article/articleList.nhn?rletTypeCd=" + rletTypeCd + "&cortarNo=" + sgdKey;
                tbcL.SelectTab(tabWeb);
                getNaverAptList(url);   //Naver 단지목록
                wbr.Navigate(url);
            }
            else if (lnkName == lnkKapt)
            {
                /*
                fmKaptSrch fmKa = new fmKaptSrch();
                fmKa.StartPosition = FormStartPosition.CenterScreen;
                fmKa.TheParent = this;
                DialogResult dlogResult = fmKa.ShowDialog();
                if (dlogResult == DialogResult.OK)
                {
                    //MessageBox.Show("ok");
                }
                fmKa.Dispose();
                */
            }
            else
            {
                //if (txtKbCode.Text == string.Empty || txtKbCode.Text == "0") url = "";
                //else url = "";
            }
        }



        /// <summary>
        /// Naver 단지목록 파싱 클릭
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnParse_Click(object sender, EventArgs e)
        {
            getNaverAptList(wbr.Url.ToString());
        }

        /// <summary>
        /// Naver 단지목록 파싱
        /// </summary>
        /// <param name="url"></param>
        private void getNaverAptList(string url)
        {
            string contents = "";

            lvNaver.Items.Clear();
            contents = net.GetHtml(url, Encoding.UTF8);
            Match match = Regex.Match(contents, @"<option value=""_"" class=""[\w\-]+"">단지</option>(.*?)</select>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (match.Success == false) return;

            MatchCollection mc = Regex.Matches(match.Groups[1].Value, @"<option value=""(\d+)""[\s]*>(.*?)</option>", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            foreach (Match m in mc)
            {
                ListViewItem item = new ListViewItem(m.Groups[2].Value);
                item.SubItems.Add(m.Groups[1].Value);
                lvNaver.Items.Add(item);
                if (m.Groups[2].Value == txtAptName.Text || m.Groups[2].Value == txtAptName.Text.Replace("아파트", string.Empty))
                {
                    item.BackColor = Color.PeachPuff;
                }
            }
        }

        /// <summary>
        /// Naver 파싱된 단지목록에서 코드 선택(항목 더블클릭)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lvNaver_DoubleClick(object sender, EventArgs e)
        {
            string url = "", contents = "", rletNo = "", infoTbl = "";
            string 총세대수 = "", 총동수 = "", 준공년월 = "", 건설사명 = "", 총주차대수 = "", 세대당주차대수 = "", 난방방식 = "", 난방연료 = "";
            string 용적율 = "", 건폐율 = "", 최고층 = "", 최저층 = "", 면적 = "", 관리사무소 = "";

            rletNo = lvNaver.SelectedItems[0].SubItems[1].Text;
            url = "http://land.naver.com/article/complexInfo.nhn?rletNo=" + rletNo;
            contents = net.GetHtml(url, Encoding.UTF8);
            infoTbl = Regex.Match(contents, @"<table summary=""단지상세 정보"".*?</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase).Value;
            총세대수 = getTblValue(infoTbl, "총세대수[</strong>]*");
            총동수 = getTblValue(infoTbl, "총동수");
            준공년월 = getTblValue(infoTbl, "준공년월");
            건설사명 = getTblValue(infoTbl, "건설사명");
            총주차대수 = getTblValue(infoTbl, "총주차대수");
            세대당주차대수 = getTblValue(infoTbl, "세대당주차대수");
            난방방식 = getTblValue(infoTbl, "난방방식");
            난방연료 = getTblValue(infoTbl, "난방연료");
            용적율 = getTblValue(infoTbl, "용적율");
            건폐율 = getTblValue(infoTbl, "건폐율");
            최고층 = getTblValue(infoTbl, "최고층");
            최저층 = getTblValue(infoTbl, "최저층");
            면적 = getTblValue(infoTbl, "면적");
            관리사무소 = getTblValue(infoTbl, "관리사무소 Tel");

            txtNaverCd.Text = rletNo;
            txtCntSedae.Text = 총세대수;
            txtCntDong.Text = 총동수;
            txtBuildDt.Text = 준공년월;
            txtConstructor.Text = 건설사명;
            txtCntPark.Text = 총주차대수;
            txtSedaePark.Text = 세대당주차대수;
            txtHeadType.Text = 난방방식;
            txtHeatFuel.Text = 난방연료;
            txtFaRatio.Text = 용적율;
            txtBlRatio.Text = 건폐율;
            txtTopFloor.Text = 최고층;
            txtLowFloor.Text = 최저층;
            txtAreaKind.Text = 면적;
            if (txtPhone.Text.Trim() == string.Empty)
            {
                txtPhone.Text = 관리사무소;
            }
        }

        /// <summary>
        /// Naver 단지정보에서 해당 값 추출
        /// </summary>
        /// <param name="html"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        private string getTblValue(string html, string name)
        {
            string value = "";

            Match match = Regex.Match(html, name + @"</th>\W+<td[\s\w=""%]*[\s\w=""%]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (match.Success)
            {
                value = match.Groups[1].Value;
            }
            if (name == "면적")
            {
                value = Regex.Replace(value, @"[\r\n\t]*", string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            }
            value = Regex.Replace(value, @"<[\w\s=""\d\W]*?>|&nbsp;", string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase).Trim();
            value = Regex.Replace(value, @"^%|^\- 대", string.Empty, RegexOptions.Multiline | RegexOptions.IgnoreCase).Trim();

            return value;
        }

        private void btnDel_Click(object sender, EventArgs e)
        {
            string sql, aptCd;

            aptCd = txtAptCd.Text;
            if (aptCd == string.Empty) return;

            if (MessageBox.Show("삭제 하시겠습니까?", "삭제확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No) return;

            sql = "delete from tx_apt where apt_code=" + aptCd;
            db.Open();
            db.ExeQry(sql);
            db.Close();

            MessageBox.Show("삭제 되었습니다.");
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            ui.FormClear(tabDtl);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            string aptCd, sql, cvp;

            List<MySqlParameter> sp = new List<MySqlParameter>();

            aptCd = txtAptCd.Text;

            cvp = "dj_name=@dj_name, pd_type=@pd_type, si_key=@si_key, gu_key=@gu_key, dong_key=@dong_key, ri_key=@ri_key, sido=@sido, gugun=@gugun, dong=@dong, ri=@ri, bunji=@bunji, cnt_sedae=@cnt_sedae, cnt_dong=@cnt_dong,";
            cvp += "build_date=@build_date, constructor=@constructor, cnt_parking=@cnt_parking, sedae_parking=@sedae_parking,heat_type=@heat_type, heat_fuel=@heat_fuel, fa_ratio=@fa_ratio, bl_ratio=@bl_ratio,";
            cvp += "top_floor=@top_floor, low_floor=@low_floor, area_kind=@area_kind, subway=@subway, bus=@bus, road=@road, comforts=@comforts, education=@education, relax_park=@relax_park, medical=@medical, phone=@phone, memo=@memo,";
            cvp += "apt_code=@apt_code, dj_no=@dj_no, molit_code=@molit_code, ka_code=@ka_code, match_type=@match_type";

            sql = "insert into tx_apt SET " + cvp + ", wdate=curdate() ON DUPLICATE KEY UPDATE " + cvp;
            sp.Add(new MySqlParameter("@dj_name", txtAptName.Text));
            sp.Add(new MySqlParameter("@sido", txtSiNm.Text));
            sp.Add(new MySqlParameter("@gugun", txtGuNm.Text));
            sp.Add(new MySqlParameter("@dong", txtDnNm.Text));
            sp.Add(new MySqlParameter("@ri", txtRiNm.Text));
            sp.Add(new MySqlParameter("@bunji", txtAddr2.Text));
            sp.Add(new MySqlParameter("@si_key", txtSiCd.Text));
            sp.Add(new MySqlParameter("@gu_key", txtGuCd.Text));
            sp.Add(new MySqlParameter("@dong_key", txtDnCd.Text));
            sp.Add(new MySqlParameter("@ri_key", txtRiCd.Text));

            sp.Add(new MySqlParameter("@cnt_sedae", txtCntSedae.Text));
            sp.Add(new MySqlParameter("@cnt_dong", txtCntDong.Text));
            sp.Add(new MySqlParameter("@build_date", txtBuildDt.Text));
            sp.Add(new MySqlParameter("@constructor", txtConstructor.Text));
            sp.Add(new MySqlParameter("@cnt_parking", txtCntPark.Text));
            sp.Add(new MySqlParameter("@sedae_parking", txtSedaePark.Text));
            sp.Add(new MySqlParameter("@heat_type", txtHeadType.Text));
            sp.Add(new MySqlParameter("@heat_fuel", txtHeatFuel.Text));
            sp.Add(new MySqlParameter("@fa_ratio", txtFaRatio.Text));
            sp.Add(new MySqlParameter("@bl_ratio", txtBlRatio.Text));
            sp.Add(new MySqlParameter("@top_floor", txtTopFloor.Text));
            sp.Add(new MySqlParameter("@low_floor", txtLowFloor.Text));
            sp.Add(new MySqlParameter("@area_kind", txtAreaKind.Text));
            sp.Add(new MySqlParameter("@subway", txtSubway.Text));
            sp.Add(new MySqlParameter("@bus", txtBus.Text));
            sp.Add(new MySqlParameter("@road", txtRoad.Text));
            sp.Add(new MySqlParameter("@comforts", txtComforts.Text));
            sp.Add(new MySqlParameter("@education", txtEdu.Text));
            sp.Add(new MySqlParameter("@relax_park", txtRelaxPark.Text));
            sp.Add(new MySqlParameter("@medical", txtMedical.Text));
            sp.Add(new MySqlParameter("@phone", txtPhone.Text));
            sp.Add(new MySqlParameter("@memo", txtMemo.Text));

            sp.Add(new MySqlParameter("@apt_code", txtAptCd.Text));
            sp.Add(new MySqlParameter("@dj_no", txtNaverCd.Text));
            sp.Add(new MySqlParameter("@molit_code", txtMolitCd.Text));
            sp.Add(new MySqlParameter("@ka_code", txtKaCd.Text));

            sp.Add(new MySqlParameter("@pd_type", cbxCat.SelectedValue));
            sp.Add(new MySqlParameter("@match_type", cbxMatchDvsn.SelectedValue));

            db.Open();
            db.ExeQry(sql, sp);
            db.Close();

            MessageBox.Show("저장 되었습니다.");
        }

        private void btnAddrSrch_Click(object sender, EventArgs e)
        {
            sfSrchAdrsCd sfAdrsCd = new sfSrchAdrsCd();
            sfAdrsCd.StartPosition = FormStartPosition.CenterScreen;
            DialogResult drst = sfAdrsCd.ShowDialog();
            if (drst == DialogResult.OK)
            {
                txtSiNm.Text = sfAdrsCd.siNm;
                txtGuNm.Text = sfAdrsCd.guNm;
                txtDnNm.Text = sfAdrsCd.dnNm;
                txtRiNm.Text = sfAdrsCd.riNm;

                txtSiCd.Text = sfAdrsCd.siCd;
                txtGuCd.Text = sfAdrsCd.guCd;
                txtDnCd.Text = sfAdrsCd.dnCd;
                txtRiCd.Text = sfAdrsCd.riCd;
                txtAddr2.Focus();
            }
            sfAdrsCd.Dispose();
        }
    }
}
