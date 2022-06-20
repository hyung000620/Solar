using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Solar.CA
{
    public partial class wfRgstMgmt : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        AuctCd auctCd = new AuctCd();
        NetUtil net = new NetUtil();

        DataTable dtLawCd, dtDptCd; //법원, 계
        DataTable dtCatCdAll, dtCatCd, dtLandUseCd;  //물건 종별
        DataTable dtStateCd;    //진행 상태        
        DataTable dtSidoCd;     //법정동 시/도 코드
        DataTable dtEtcCd;      //기타 모든 코드
        DataTable dtDpslCd;     //매각 구분
        DataTable dtExpIncCd;   //제시외 매각포함 여부
        DataTable dtRgstYn;     //등기 유무
        DataTable dtRgstAdtn;   //토지 별도등기
        DataTable dtFileCd;     //파일 구분

        decimal totRowCnt = 0;
        string cdtn = "";

        BackgroundWorker bgwork;

        string myWeb = Properties.Settings.Default.myWeb;
        FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "CA/", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);

        public wfRgstMgmt()
        {
            InitializeComponent();
            init();
        }

        private void init()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            lnkTid.Text = string.Empty;

            ui.DgSetRead(dg, 0);
            ui.DgSetRead(dgL, 0);
            ui.DgSetRead(dgF, 0);
            ui.DgSetRead(dgU, 0);
            ui.DgSetRead(dgCp, 0);            

            ui.SetPagn(panPagn, rows: 200, min: 50, inc: 50);

            //기타 모든 코드
            dtEtcCd = db.ExeDt("select * from ta_cd_etc order by seq, cd");

            //전체 법원별 계코드 DataTable
            dtDptCd = db.ExeDt("select C.ca_cd, crt_cd, spt_cd, cs_cd, dpt_cd, dpt_nm from ta_cd_cs C , ta_cd_dpt D where C.ca_cd=D.ca_cd order by dpt_cd");

            //법원 전체 코드
            dtLawCd = auctCd.DtLawInfo();
            DataRow row = dtLawCd.NewRow();
            row["csNm"] = "-선택-";
            row["csCd"] = "";
            dtLawCd.Rows.InsertAt(row, 0);
            cbxSrchCs.DataSource = dtLawCd;
            cbxSrchCs.DisplayMember = "csNm";
            cbxSrchCs.ValueMember = "csCd";
            cbxSrchCs.SelectedIndexChanged += CbxSrchCs_SelectedIndexChanged;
            CbxSrchCs_SelectedIndexChanged(null, null);

            //등기유무
            dtRgstYn = dtEtcCd.Select("dvsn=19").CopyToDataTable();
            row = dtRgstYn.NewRow();
            row["cd"] = 0;
            row["nm"] = "-선택-";
            dtRgstYn.Rows.InsertAt(row, 0);
            cbxRgstYn.DataSource = dtRgstYn;
            cbxRgstYn.DisplayMember = "nm";
            cbxRgstYn.ValueMember = "cd";

            //매각 구분
            dtDpslCd = dtEtcCd.Select("dvsn=10").CopyToDataTable();

            //물건종별 및 토지 지목
            dtCatCdAll = db.ExeDt("select cat1_cd, cat2_cd, cat3_cd, cat2_nm, cat3_nm, hide, bldg_type from ta_cd_cat where cat3_cd > 0 order by cat3_cd");
            var x = from DataRow r in dtCatCdAll.Rows
                    where r["hide"].ToString() == "0"
                    select r;
            dtCatCd = x.CopyToDataTable();
            row = dtCatCd.NewRow();
            row["cat2_cd"] = 0;
            row["cat2_nm"] = "";
            row["cat3_cd"] = 0;
            row["cat3_nm"] = "-선택-";
            dtCatCd.Rows.InsertAt(row, 0);
            cbxSrchCat.DataSource = dtCatCd.Copy();
            cbxSrchCat.DisplayMember = "cat3_nm";
            cbxSrchCat.ValueMember = "cat3_cd";

            //진행 상태
            dtStateCd = db.ExeDt("select sta1_cd, sta2_cd, sta1_nm, sta2_nm from ta_cd_sta order by sta2_cd");
            row = dtStateCd.NewRow();
            row["sta1_cd"] = 0;
            row["sta1_nm"] = "-선택-";
            row["sta2_cd"] = 0;
            row["sta2_nm"] = "-선택-";
            dtStateCd.Rows.InsertAt(row, 0);
            cbxSrchSta1.DataSource = dtStateCd.Rows.Cast<DataRow>().GroupBy(g => g.Field<byte>("sta1_cd")).Select(t => t.First()).CopyToDataTable();
            cbxSrchSta1.DisplayMember = "sta1_nm";
            cbxSrchSta1.ValueMember = "sta1_cd";
            cbxSrchSta1.SelectedIndexChanged += CbxSrchSta1_SelectedIndexChanged;
            cbxSrchSta1.SelectedValue = 11;

            //파일 구분
            dtFileCd = db.ExeDt("select * from ta_cd_file order by cd");

            //토지 별도등기 구분
            dtRgstAdtn = new DataTable();
            dtRgstAdtn.Columns.Add("cd");
            dtRgstAdtn.Columns.Add("nm");
            dtRgstAdtn.Rows.Add(0, string.Empty);
            dtRgstAdtn.Rows.Add(1, "토지별도등기있음");
            dtRgstAdtn.Rows.Add(2, "대지권없음");
            dtRgstAdtn.Rows.Add(3, "미등기감정가격포함");
            dtRgstAdtn.Rows.Add(4, "대지권미등기");
            dtRgstAdtn.Rows.Add(5, "토지별도등기인수조건");
            dtRgstAdtn.Rows.Add(6, "미등기가격포함+토지별도등기");

            //일주일후 입찰일정
            DataTable dtBidSkd = db.ExeDt("SELECT bid_dt,concat(date_format(bid_dt,'%m.%d'),'(', substr(_UTF8'일월화수목금토',dayofweek(bid_dt),1),')') as day_ow FROM `ta_skd` where bid_dt BETWEEN date_add(curdate(),interval 7 day) and date_add(curdate(),interval 21 day) GROUP by bid_dt ORDER by bid_dt");
            row=dtBidSkd.NewRow();
            row["bid_dt"] = DateTime.Now.AddDays(7).ToShortDateString();
            row["day_ow"] = "-선택-";
            dtBidSkd.Rows.InsertAt(row, 0);
            cbxSrchBidDt.DataSource = dtBidSkd;
            cbxSrchBidDt.DisplayMember = "day_ow";
            cbxSrchBidDt.ValueMember = "bid_dt";
            cbxSrchBidDt.SelectedIndexChanged += CbxSrchBidDt_SelectedIndexChanged;

            //검색-Enter 키
            txtSrchTid.KeyDown += TxtEnter_KeyDown;
            txtSrchSn.KeyDown += TxtEnter_KeyDown;

            //등기 파일복사-전체선택/해제 체크박스
            dgCp.CellPainting += (s, e) =>
            {
                if (e.ColumnIndex == 0 && e.RowIndex == -1)
                {
                    e.PaintBackground(e.ClipBounds, false);
                    Point pt = e.ClipBounds.Location;
                    int nChkBoxWidth = 15;
                    int nChkBoxHeight = 15;
                    int offsetX = (e.CellBounds.Width - nChkBoxWidth) / 2 + 1;
                    int offsetY = (e.CellBounds.Height - nChkBoxHeight) / 2;
                    pt.X += offsetX;
                    pt.Y += offsetY + 1;

                    CheckBox chkAll = new CheckBox();
                    chkAll.Size = new Size(nChkBoxWidth, nChkBoxHeight);
                    chkAll.Location = pt;
                    chkAll.CheckedChanged += new EventHandler(dgCpChkAll_CheckedChanged);
                    chkAll.Name = "HeaderChkAll";
                    ((DataGridView)s).Controls.Add(chkAll);
                    e.Handled = true;
                }
            };

            //ComboBox 마우스휠 무력화
            List<ComboBox> lstCbx = new List<ComboBox>();
            ComboBox[] cbxArr = new ComboBox[] { cbxSrchCs, cbxSrchDpt, cbxSrchCat, cbxSrchSta1, cbxSrchSta2, cbxSrchBidDt, cbxSrchSort, cbxRgstYn };
            lstCbx.AddRange(cbxArr);
            CbxMouseWheelDisable(lstCbx);
        }

        private void CbxSrchBidDt_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbxSrchBidDt.SelectedIndex == 0)
            {
                dtpBidDtBgn.Checked = false;
                dtpBidDtEnd.Checked = false;
            }
            else
            {
                dtpBidDtBgn.Checked = true;
                dtpBidDtEnd.Checked = true;
                dtpBidDtBgn.Value = dtpBidDtEnd.Value = Convert.ToDateTime(cbxSrchBidDt.SelectedValue);
            }

            //btnSrch_Click(null, null);
        }

        /// <summary>
        /// ComboBox 마우스 휠 무력화
        /// </summary>
        /// <param name="lstCbx"></param>
        private void CbxMouseWheelDisable(List<ComboBox> lstCbx)
        {
            foreach (ComboBox cbx in lstCbx)
            {
                cbx.MouseWheel += (s, e) => { ((HandledMouseEventArgs)e).Handled = true; };
            }
        }

        /// <summary>
        /// 등기 파일복사-전체선택/해제
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgCpChkAll_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox chk = (CheckBox)sender;
            bool chkState = chk.Checked;
            foreach (DataGridViewRow row in dgCp.Rows)
            {
                row.Cells[0].Value = chkState;
            }
            btnCpSrch.Focus();   //focus를 바꿔주지 않으면 current row 에는 체크유무가 표시 안됨!!!
        }

        /// <summary>
        /// 검색-법원별 담당계
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CbxSrchCs_SelectedIndexChanged(object sender, EventArgs e)
        {
            string spt = "0";

            if (cbxSrchCs.SelectedIndex > 0)
            {
                spt = cbxSrchCs.SelectedValue.ToString();
            }
            DataView dvDpt = dtDptCd.DefaultView;
            dvDpt.RowFilter = string.Format("spt_cd='{0}'", spt);
            DataTable dtDpt = dvDpt.ToTable();
            DataRow row = dtDpt.NewRow();
            row["dpt_nm"] = "-선택-";
            row["dpt_cd"] = "";
            dtDpt.Rows.InsertAt(row, 0);
            cbxSrchDpt.DataSource = dtDpt;
            cbxSrchDpt.DisplayMember = "dpt_nm";
            cbxSrchDpt.ValueMember = "dpt_cd";
        }

        /// <summary>
        /// 검색-진행상태
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CbxSrchSta1_SelectedIndexChanged(object sender, EventArgs e)
        {
            DataTable dt = dtStateCd.Rows.Cast<DataRow>().Where(t => t["sta1_cd"].ToString() == cbxSrchSta1.SelectedValue.ToString()).CopyToDataTable();
            if (cbxSrchSta1.SelectedIndex > 0)
            {
                DataRow row = dt.NewRow();
                row["sta2_cd"] = 0;
                row["sta2_nm"] = "-선택-";
                dt.Rows.InsertAt(row, 0);
            }
            cbxSrchSta2.DataSource = dt;
            cbxSrchSta2.DisplayMember = "sta2_nm";
            cbxSrchSta2.ValueMember = "sta2_cd";
        }

        /// <summary>
        /// 리셋
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnReset_Click(object sender, EventArgs e)
        {
            ui.FormClear(tpnlSrch);
            cbxSrchSta1.SelectedIndex = 2;
        }

        /// <summary>
        /// 물건 검색-엔터키
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TxtEnter_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnSrch_Click(null, null);
            }
        }

        /// <summary>
        /// 물건 검색
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void btnSrch_Click(object sender, EventArgs e)
        {
            string sql = "";

            cdtn = "1";
            dg.Rows.Clear();
            dgU.Rows.Clear();
            dgCp.Rows.Clear();
            //ui.FormClear(tabDtl, new string[] { "cbxCrtSpt", "cbxDpt" });
            lnkTid.Text = string.Empty;

            List<string> cdtnList = new List<string>();

            txtSrchTid.Text = txtSrchTid.Text.Replace("_", string.Empty).Trim();
            if (txtSrchSn.Text.Trim() != "")
            {
                Match match = Regex.Match(txtSrchSn.Text.Trim(), @"^(\d+)[\-]*(\d+)*[\-]*(\d+)*", RegexOptions.Multiline);
                if (match.Groups[3].Value != "") cdtnList.Add("sn1=" + match.Groups[1].Value + " and sn2=" + match.Groups[2].Value + " and pn=" + match.Groups[3].Value);   //2018-4567-8
                else if (match.Groups[2].Value != "") cdtnList.Add("sn1=" + match.Groups[1].Value + " and sn2=" + match.Groups[2].Value);   //2018-4567
                else if (match.Groups[1].Value != "") cdtnList.Add("sn2=" + match.Groups[1].Value);     //4567
            }            
            if (cbxSrchCs.SelectedIndex > 0) cdtnList.Add("spt=" + cbxSrchCs.SelectedValue.ToString());
            if (cbxSrchDpt.SelectedIndex > 0) cdtnList.Add("dpt=" + cbxSrchDpt.SelectedValue.ToString());
            if (cbxSrchSta1.SelectedIndex > 0) cdtnList.Add("sta1=" + cbxSrchSta1.SelectedValue.ToString());
            if (cbxSrchSta2.SelectedIndex > 0) cdtnList.Add("sta2=" + cbxSrchSta2.SelectedValue.ToString());
            if (cbxSrchCat.SelectedIndex > 0) cdtnList.Add("cat3=" + cbxSrchCat.SelectedValue.ToString());

            if (chkLandOnly.Checked && chkBldgOnly.Checked)
            {
                cdtnList.Add("dpsl_dvsn in (13,16,17,22)");
            }
            else
            {
                if (chkLandOnly.Checked) cdtnList.Add("dpsl_dvsn in (13,16)");
                if (chkBldgOnly.Checked) cdtnList.Add("dpsl_dvsn in (17,22)");
            }            
            if (chkLandRgstAdtn.Checked) cdtnList.Add("sp_rgst in (1,6)");
            if (chkExCarShip.Checked) cdtnList.Add("cat1 != 30");

            if (dtpBidDtBgn.Checked) cdtnList.Add("bid_dt >= '" + dtpBidDtBgn.Value.ToShortDateString() + "'");
            if (dtpBidDtEnd.Checked) cdtnList.Add("bid_dt <= '" + dtpBidDtEnd.Value.ToShortDateString() + "'");

            if (cdtnList.Count > 0) cdtn = string.Join(" and ", cdtnList.ToArray());
            if (txtSrchTid.Text.Trim() != "")
            {
                cdtn = "tid IN (" + Regex.Replace(txtSrchTid.Text.Trim(), @"\D+", ",") + ")";   //TID 검색일 경우 모든 조건 무시
            }

            sql = "select COUNT(*) from ta_list where " + cdtn;

            db.Open();
            totRowCnt = (decimal)((Int64)db.RowCnt(sql));
            db.Close();

            ComboBox cbx = (ComboBox)panPagn.Controls["_cbxPagn"];
            cbx.SelectedIndexChanged -= gotoPageList;
            ui.InitPagn(panPagn, totRowCnt);
            cbx.SelectedIndexChanged += gotoPageList;
            if (cbx.Items.Count > 0) cbx.SelectedIndex = 0;
        }

        /// <summary>
        /// 물건 목록
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void gotoPageList(object sender, EventArgs e)
        {
            int i = 0;
            decimal startRow = 0;
            string sql = "", csCd = "", dpt = "", order = "", sort = "", state = "", cat = "", dpsl = "", rgstYN = "", rgstAdtn = "";
            string tbl, sn1, tid;
            bool rgstLand, rgstBldg;
            int rgstLandCnt = 0, rgstBldgCnt = 0;

            dg.Rows.Clear();

            //DataTable dt = new DataTable();
            //dt.Columns.Add("No");
            ComboBox cbxPage = (ComboBox)sender;
            NumericUpDown listScale = (NumericUpDown)panPagn.Controls["_nudList"];
            startRow = (decimal.Parse(cbxPage.Text) - 1) * listScale.Value;
            dg.SelectionChanged -= dg_SelectionChanged;

            sort = cbxSrchSort.Text;
            if (sort == "용도")
            {
                order = (chkSortAsc.Checked) ? "cat3, spt, dpt, sn1, sn2, pn" : "cat3 desc, spt, dpt, sn1, sn2, pn";
            }
            else if (sort == "사건번호")
            {
                order = (chkSortAsc.Checked) ? "spt, dpt, sn1, sn2, pn" : "spt, dpt, sn1 desc, sn2 desc, pn asc";
            }
            else if (sort == "유찰수")
            {
                order = (chkSortAsc.Checked) ? "fb_cnt" : "fb_cnt desc";
            }
            else if (sort == "입찰일")
            {
                order = (chkSortAsc.Checked) ? "bid_dt, spt, dpt, sn1, sn2, pn" : "bid_dt desc, spt, dpt, sn1, sn2, pn";
            }
            else if (sort == "감정가")
            {
                order = (chkSortAsc.Checked) ? "apsl_amt" : "apsl_amt desc";
            }
            else if (sort == "최저가")
            {
                order = (chkSortAsc.Checked) ? "minb_amt" : "minb_amt desc";
            }
            else
            {
                order = "tid desc";
            }

            if (txtSrchTid.Text.Trim() != "")
            {
                order = "tid asc";
            }

            sql = "select * from ta_list";
            sql += " where " + cdtn + " order by " + order + " limit " + startRow + "," + listScale.Value;

            this.Cursor = Cursors.WaitCursor;
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
                var xDpsl = dtDpslCd.Rows.Cast<DataRow>().Where(t => t["cd"].ToString() == dr["dpsl_dvsn"].ToString()).SingleOrDefault();
                dpsl = (xDpsl == null || dr["dpsl_dvsn"].ToString() == "0") ? string.Empty : xDpsl.Field<string>("nm");
                var xRgstYN = dtRgstYn.Rows.Cast<DataRow>().Where(t => t["cd"].ToString() == dr["rgst_yn"].ToString()).SingleOrDefault();
                rgstYN = (xRgstYN == null || dr["rgst_yn"].ToString() == "0") ? string.Empty : xRgstYN.Field<string>("nm");
                var xRgstAdtn = dtRgstAdtn.Rows.Cast<DataRow>().Where(t => t["cd"].ToString() == dr["sp_rgst"].ToString()).SingleOrDefault();
                rgstAdtn = (xRgstAdtn == null || dr["sp_rgst"].ToString() == "0") ? string.Empty : xRgstAdtn.Field<string>("nm");

                i = dg.Rows.Add();
                dg["dg_No", i].Value = totRowCnt - startRow - i;
                dg["dg_Tid", i].Value = dr["tid"];
                dg["dg_CS", i].Value = auctCd.FindCsNm(csCd);
                dg["dg_Dpt", i].Value = dpt;
                dg["dg_SN", i].Value = (dr["pn"].ToString() == "0") ? string.Format("{0}-{1}", dr["sn1"], dr["sn2"]) : string.Format("{0}-{1}({2})", dr["sn1"], dr["sn2"], dr["pn"]);
                dg["dg_Adrs", i].Value = dr["adrs"];
                dg["dg_BidDt", i].Value = dr["bid_dt"].ToString().Contains("0001") ? "" : string.Format("{0:MM.dd (ddd)}", dr["bid_dt"]);
                dg["dg_State", i].Value = state;
                dg["dg_Cat", i].Value = cat;
                dg["dg_Dpsl", i].Value = dpsl;
                dg["dg_Owner", i].Value = dr["owner"];
                dg["dg_RgstYN", i].Value = rgstYN;
                dg["dg_RgstAdtn", i].Value = rgstAdtn;
            }
            dr.Close();
            db.Close();

            //등기 유/무 표시
            db.Open();
            foreach (DataGridViewRow row in dg.Rows)
            {
                rgstLand = false;
                rgstBldg = false;
                rgstLandCnt = 0;
                rgstBldgCnt = 0;

                tid = row.Cells["dg_Tid"].Value.ToString();
                sn1 = row.Cells["dg_SN"].Value.ToString().Substring(0, 4);
                tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
                sql = $"select ctgr from {tbl} where tid={tid} and ctgr in ('DA','DB')";
                dr = db.ExeRdr(sql);
                while (dr.Read())
                {
                    if ($"{dr["ctgr"]}" == "DA")
                    {
                        rgstLand = true;
                        rgstLandCnt++;
                    }
                    if ($"{dr["ctgr"]}" == "DB")
                    {
                        rgstBldg = true;
                        rgstBldgCnt++;
                    }
                }
                dr.Close();

                if (rgstLand)
                {
                    row.Cells["dg_RgstLand"].Value = (rgstLandCnt > 1) ? $"토{rgstLandCnt}" : "토";
                    row.Cells["dg_RgstLand"].Style.BackColor = Color.SandyBrown;
                }
                if (rgstBldg)
                {
                    row.Cells["dg_RgstBldg"].Value = (rgstBldgCnt > 1) ? $"건{rgstBldgCnt}" : "건";
                    row.Cells["dg_RgstBldg"].Style.BackColor = Color.LightGray;
                }
            }
            db.Close();

            dg.ClearSelection();
            this.Cursor = Cursors.Default;

            dg.SelectionChanged += dg_SelectionChanged;
        }

        private void dg_SelectionChanged(object sender, EventArgs e)
        {
            int i = 0, apslCnt = 0;
            string sql = "", tid = "", spt = "", sn1 = "", sn2 = "", pn = "", filePath = "", sn = "", rgstCls, tbl, regnAdrs;

            this.Cursor = Cursors.WaitCursor;
            dgL.Rows.Clear();
            dgF.Rows.Clear();

            if (dg.CurrentRow == null)
            {
                this.Cursor = Cursors.Default;
                return;
            }

            i = dg.CurrentRow.Index;
            tid = dg["dg_Tid", i].Value.ToString();
            lnkTid.Text = tid;
            lblSaNo.Text = dg["dg_SN", i].Value.ToString();

            sql = $"select * from ta_list where tid={tid} limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            spt = dr["spt"].ToString();
            sn1 = dr["sn1"].ToString();
            sn2 = dr["sn2"].ToString();
            cbxRgstYn.SelectedValue = dr["rgst_yn"];
            dr.Close();
            db.Close();

            //관련 파일(토지/건물등기, 건축물대장)
            tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
            sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));            
            sql = "select * from " + tbl + " where (tid=" + tid + " and ctgr in ('DA','DB','EC')) or (spt=" + spt + " and sn='" + sn + "' and tid=0 and ctgr='AF') order by ctgr";
            db.Open();
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                i = dgF.Rows.Add();
                dgF["dgF_Ctgr", i].Value = dtFileCd.Rows.Cast<DataRow>().Where(t => t["cd"].ToString() == dr["ctgr"].ToString()).FirstOrDefault()["nm"].ToString();
                dgF["dgF_FileNm", i].Value = dr["file"];
                dgF["dgF_Wdt", i].Value = (dr["wdt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["wdt"]);
                dgF["dgF_Tbl", i].Value = tbl;
                dgF["dgF_Idx", i].Value = dr["idx"];
            }
            dr.Close();
            db.Close();
            dgF.ClearSelection();

            //건물 현황
            db.Open();
            sql = "select S.*, B.sqm, B.tot_shr_sqm from ta_ls S , ta_bldg B where S.tid=B.tid and S.tid=" + tid + " and S.no=B.ls_no and B.dvsn=1 order by S.no";
            //sql = $"select S.*, B.sqm, B.tot_shr_sqm from ta_ls S LEFT JOIN ta_bldg B ON S.tid=B.tid and B.dvsn=1 and S.no=B.ls_no where S.tid={tid} and S.dvsn not in ('토지') ORDER by S.no";
            //sql = $"select S.*, B.sqm, B.tot_shr_sqm from ta_ls S LEFT JOIN ta_bldg B ON S.tid=B.tid and B.dvsn=1 and S.no=B.ls_no where S.tid={tid} ORDER by S.no";
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                i = dgL.Rows.Add();
                dgL["dgL_LsNo", i].Value = dr["no"];
                dgL["dgL_Adrs", i].Value = dr["adrs"];
                dgL["dgL_Dvsn", i].Value = dr["dvsn"];
                if (dr["sqm"] is DBNull || dr["tot_shr_sqm"] is DBNull)
                {
                    dgL["dgL_Sqm", i].Value = "-";
                }
                else
                {
                    dgL["dgL_Sqm", i].Value = (Convert.ToDecimal(dr["tot_shr_sqm"]) < 1) ? dr["sqm"] : dr["tot_shr_sqm"];
                }                
                dgL["dgL_Pin", i].Value = dr["pin"];
                dgL["dgL_Note", i].Value = dr["note"];
                dgL["dgL_Pnu", i].Value = dr["pnu"];
            }
            dr.Close();
            db.Close();

            //건물-지번 주소 연동
            db.Open();
            foreach (DataGridViewRow row in dgL.Rows)
            {
                Match match = Regex.Match(row.Cells["dgL_Pnu"].Value.ToString(), @"(\d{2})(\d{3})(\d{3})(\d{2})(\d{1})(\d{4})(\d{4})");                
                if (!match.Success) continue;
                sql = $"select * from tx_cd_adrs where si_cd={match.Groups[1].Value} and gu_cd={match.Groups[2].Value} and dn_cd={match.Groups[3].Value} and ri_cd={match.Groups[4].Value} and hide=0 limit 1";                
                dr = db.ExeRdr(sql);
                if (dr.HasRows)
                { 
                    dr.Read();
                    regnAdrs = $"{dr["si_nm"]} {dr["gu_nm"]} {dr["dn_nm"]} {dr["ri_nm"]}".Trim();
                    regnAdrs += (match.Groups[5].Value == "2") ? " 산" : " ";
                    regnAdrs += $"{Convert.ToInt16(match.Groups[6].Value) * 1}";
                    regnAdrs += (match.Groups[7].Value != "0000") ? $"-{Convert.ToInt16(match.Groups[7].Value) * 1}" : "";
                    row.Cells["dgL_Adrs"].Value = $"{row.Cells["dgL_Adrs"].Value}\r\n▶{regnAdrs}";
                }
                dr.Close();
            }
            db.Close();

            //토지 현황
            db.Open();
            sql = "select S.*, B.sqm from ta_ls S , ta_land B where S.tid=B.tid and S.tid=" + tid + " and S.no=B.ls_no order by S.no";
            //sql = $"select S.*, B.sqm from ta_ls S LEFT JOIN ta_land B ON S.tid=B.tid and S.no=B.ls_no where S.tid={tid} and S.dvsn in ('토지') ORDER by S.no";
            //sql = $"select S.*, B.sqm from ta_ls S LEFT JOIN ta_land B ON S.tid=B.tid and S.no=B.ls_no where S.tid={tid} ORDER by S.no";
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                i = dgL.Rows.Add();
                dgL["dgL_LsNo", i].Value = dr["no"];
                dgL["dgL_Adrs", i].Value = dr["adrs"];
                dgL["dgL_Dvsn", i].Value = dr["dvsn"];
                if (dr["sqm"] is DBNull)
                {
                    dgL["dgL_Sqm", i].Value = "-";
                }
                else
                {
                    dgL["dgL_Sqm", i].Value = dr["sqm"];
                }                    
                dgL["dgL_Pin", i].Value = dr["pin"];
                dgL["dgL_Note", i].Value = dr["note"];
                dgL.Rows[i].DefaultCellStyle.BackColor = Color.LightGray;
            }
            dr.Close();
            db.Close();
            dgL.ClearSelection();

            Clipboard.SetText($"{tid}_");
            dgU.Rows.Clear();
            dgCp.Rows.Clear();

            this.Cursor = Cursors.Default;

            LnkCA_LinkClicked(lnkCA_Sagun, null);
        }

        /// <summary>
        /// 탱크 링크-물건창(웹)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkTid_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string tid, url;
            tid = lnkTid.Text;

            if (string.IsNullOrEmpty(tid))
            {
                MessageBox.Show("선택한 물건이 없습니다.");
                return;
            }

            tbcL.SelectedTab = tabWbr1;
            url = "/ca/caView.php?tid=" + lnkTid.Text;
            net.TankWebView(wbr1, url);
        }

        /// <summary>
        /// 물건창(관리)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkCaMgmt_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string tid;
            tid = lnkTid.Text;

            if (string.IsNullOrEmpty(tid))
            {
                MessageBox.Show("선택한 물건이 없습니다.");
                return;
            }

            wfCaMgmt caMgmt = new wfCaMgmt() { Owner = this };
            caMgmt.StartPosition = FormStartPosition.CenterScreen;
            caMgmt.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            caMgmt.txtSrchTid.Text = tid;
            caMgmt.btnSrch_Click(null, null);
            caMgmt.Show();
        }

        /// <summary>
        /// 법원 링크-감정평가서, 현황조사서, 부동산표시목록, 물건상세
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LnkCA_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            int i = 0;
            string url = "", tid, sql, spt, sn, sn1, sn2, jiwonNm, saNo, pn, maemulSer, maeGiil, jpDeptCd;

            LinkLabel lnkLbl = sender as LinkLabel;
            if (dg.CurrentRow == null)
            {
                this.Cursor = Cursors.Default;
                return;
            }

            //tbcL.SelectedTab = tabWbr1;

            i = dg.CurrentRow.Index;
            tid = dg["dg_Tid", i].Value.ToString();

            sql = $"select * from ta_list where tid='{tid}' limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            spt = dr["spt"].ToString();
            sn1 = dr["sn1"].ToString();
            sn2 = dr["sn2"].ToString();
            sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));

            jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", dr["spt"]));
            saNo = string.Format("{0}0130{1}", dr["sn1"], dr["sn2"].ToString().PadLeft(6, '0'));
            maemulSer = (dr["pn"].ToString() == "0") ? "1" : dr["pn"].ToString();
            maeGiil = string.Format("{0:yyyyMMdd}", dr["bid_dt"]);
            jpDeptCd = dr["dpt"].ToString();
            pn = (dr["pn"].ToString() == "0") ? "1" : dr["pn"].ToString();
            dr.Close();
            db.Close();

            if (lnkLbl == lnkCA_Sagun)
            {
                url = "RetrieveRealEstDetailInqSaList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&srnID=PNO101005&_SRCH_SRNID=PNO101005";
            }
            else if (lnkLbl == lnkCA_Photo)
            {
                url = "RetrieveSaPhotoInfo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&mvSaNo=&maemulSer=" + pn + "&_NAVI_CMD=&_NAVI_SRNID=&_SRCH_SRNID=PNO102025&_CUR_CMD=InitMulSrch.laf&_CUR_SRNID=PNO102025&_NEXT_CMD=&_NEXT_SRNI=&_PRE_SRNID=&_LOGOUT_CHK=&_FORM_YN=";
            }
            else if (lnkLbl == lnkCA_Ls)
            {
                url = "RetrieveRealEstHjosaDispMokrok.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo;
            }
            else if (lnkLbl == lnkCA_Noti)
            {
                url = "RetrieveRealEstMgakNotifySrchGyulgwa.laf?ipchalGbnCd=000331&jiwonNm=" + jiwonNm + "&maeGiil=" + maeGiil + "&jpDeptCd=" + jpDeptCd;
            }
            else
            {
                return;
            }

            url = "http://www.courtauction.go.kr/" + url;

            if (lnkLbl == lnkCA_Sagun && e == null)
            {
                net.Nvgt(wbr3, url);
            }
            else
            {
                tbcL.SelectedTab = tabWbr2;                
                net.Nvgt(wbr2, url);
            }            
        }

        /// <summary>
        /// 등기 유무 저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRgstYnSave_Click(object sender, EventArgs e)
        {
            string tid, sql;
            tid = lnkTid.Text;

            if (string.IsNullOrEmpty(tid))
            {
                MessageBox.Show("선택한 물건이 없습니다.");
                return;
            }

            sql = $"update ta_list set rgst_yn='{cbxRgstYn.SelectedValue}' where tid='{tid}'";
            db.Open();
            db.ExeQry(sql);
            db.Close();

            MessageBox.Show("저장 되었습니다.");
        }

        private void btnOpenFiles_Click(object sender, EventArgs e)
        {
            int i = 0;
            string tid, ctgr, rmtNm, shr;

            dgU.Rows.Clear();

            OpenFileDialog ofd = new OpenFileDialog();
            //ofd.Filter = "사진 (*.jpg)|*.jpg|문서 (*.pdf,*.html)|*.pdf;*.html|사진 및 문서 (*.jpg,*.pdf,*.html)|*.jpg;*.pdf;*.html";
            ofd.Filter = "등기부등본 (*.pdf)|*_4*.pdf;*_5*.pdf";
            ofd.FilterIndex = 3;
            ofd.Multiselect = true;
            if (ofd.ShowDialog() != DialogResult.OK) return;

            foreach (string fullNm in ofd.FileNames)
            {
                tid = string.Empty;
                ctgr = string.Empty;
                shr = string.Empty;
                if (fullNm.Contains("T_")) continue;

                rmtNm = getRmtNm(fullNm);
                if (!rmtNm.Contains("오류"))
                {
                    Match match = Regex.Match(fullNm, @"(\d+)_(\d+)\-*(\d+)*.\w+", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                    tid = match.Groups[1].Value;
                    ctgr = dtFileCd.Rows.Cast<DataRow>().Where(t => t["cd"].ToString() == rmtNm.Substring(0, 2)).FirstOrDefault()["nm"].ToString();

                    //공유사진 판별(xxxxx_xx-0.jpg)
                    if (rmtNm.Substring(0, 1) == "B" && fullNm.Contains("-0."))
                    {
                        shr = "Y";
                    }
                }

                i = dgU.Rows.Add();
                dgU["dgU_No", i].Value = i + 1;
                dgU["dgU_LocFile", i].Value = fullNm;
                dgU["dgU_Ctgr", i].Value = ctgr;
                dgU["dgU_Tid", i].Value = tid;
                //dgU["dgU_Shr", i].Value = shr;
                dgU["dgU_RmtFile", i].Value = rmtNm;
            }
            dgU.ClearSelection();
        }

        /// <summary>
        /// 서버에 업로드할 파일명
        /// </summary>
        /// <param name="fullNm"></param>
        /// <returns></returns>
        private string getRmtNm(string fullNm)
        {
            int mainNo, subNo;
            string fileNm, ext, extType, tid, ctgr, sql, spt, sn, pn, seqNo, rmtNm;

            Dictionary<int, string> dicDoc = new Dictionary<int, string>();
            dicDoc.Add(13, "AA");
            dicDoc.Add(14, "AB");
            dicDoc.Add(15, "AC");
            dicDoc.Add(2, "AD");
            dicDoc.Add(3, "AE");
            dicDoc.Add(1, "AF");
            dicDoc.Add(12, "AG");
            dicDoc.Add(16, "AH");
            dicDoc.Add(20, "AI");
            dicDoc.Add(21, "AJ");
            dicDoc.Add(4, "DA");
            dicDoc.Add(5, "DB");
            dicDoc.Add(11, "EA");
            dicDoc.Add(10, "EB");
            dicDoc.Add(9, "EC");
            dicDoc.Add(7, "ED");
            dicDoc.Add(6, "EE");
            dicDoc.Add(8, "EF");
            dicDoc.Add(18, "EG");
            dicDoc.Add(19, "EH");
            dicDoc.Add(30, "EI");
            dicDoc.Add(31, "EJ");
            dicDoc.Add(32, "EK");
            dicDoc.Add(500, "FA");
            dicDoc.Add(600, "FB");
            dicDoc.Add(700, "FC");

            FileInfo fi = new FileInfo(fullNm);
            fileNm = fi.Name;
            ext = fi.Extension?.Substring(1) ?? "";

            Match match = Regex.Match(fileNm, @"(\d+)_(\d+)\-*(\d+)*.\w+", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return "오류-파일명";
            }

            tid = match.Groups[1].Value;
            mainNo = Convert.ToInt32(match.Groups[2].Value);
            subNo = string.IsNullOrEmpty(match.Groups[3].Value) ? 1 : Convert.ToInt32(match.Groups[3].Value);
            if (ext == "jpg" || ext == "png" || ext == "gif")
            {
                extType = "img";
                if (mainNo >= 21 && mainNo <= 80) ctgr = "BA";
                else if (mainNo == 9) ctgr = "BB";
                else if (mainNo == 11) ctgr = "BC";
                else if (mainNo == 10) ctgr = "BD";
                else if (mainNo >= 81 && mainNo <= 100) ctgr = "BE";
                else if (mainNo == 6) ctgr = "BF";
                else
                {
                    return "오류-사진 MainNo";
                }
            }
            else if (ext == "html" || ext == "pdf")
            {
                extType = "doc";
                if (dicDoc.ContainsKey(mainNo)) ctgr = dicDoc[mainNo];
                else
                {
                    return "오류-문서 MainNo";
                }
            }
            else
            {
                return "오류-확장자";
            }

            sql = "select spt, sn1, sn2, pn from ta_list where tid=" + tid + " limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            if (dr.HasRows)
            {
                spt = dr["spt"].ToString();
                sn = string.Format("{0}{1}", dr["sn1"], dr["sn2"].ToString().PadLeft(6, '0'));
                pn = dr["pn"].ToString().PadLeft(4, '0');

                if (extType == "img")
                {
                    seqNo = mainNo.ToString().PadLeft(4, '0');
                    //rmtNm = string.Format("{0}-{1}-{2}-{3}.{4}", ctgr, spt, sn, seqNo, ext);
                    rmtNm = string.Format("{0}-{1}-{2}-{3}-{4}.{5}", ctgr, spt, sn, pn, seqNo, ext);
                }
                else
                {
                    seqNo = subNo.ToString().PadLeft(4, '0');
                    if (ctgr == "AG")    //개별문서-> 매각물건명세서
                    {
                        rmtNm = string.Format("{0}-{1}-{2}-{3}.{4}", ctgr, spt, sn, pn, ext);
                    }
                    else if (ctgr == "DA" || ctgr == "DB" || ctgr.Substring(0, 1) == "E")   //개별문서-> 등기, 기타문서
                    {
                        rmtNm = string.Format("{0}-{1}-{2}-{3}-{4}.{5}", ctgr, spt, sn, pn, seqNo, ext);
                    }
                    else
                    {
                        rmtNm = string.Format("{0}-{1}-{2}-{3}.{4}", ctgr, spt, sn, seqNo, ext);
                    }
                }
            }
            else
            {
                rmtNm = "오류-해당 물건 없음(" + tid + ")";
            }
            dr.Close();
            db.Close();

            return rmtNm;
        }

        /// <summary>
        /// 파일 업로드/썸네일 생성
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnUpLoad_Click(object sender, EventArgs e)
        {
            CheckForIllegalCrossThreadCalls = false;
            bgwork = new BackgroundWorker();
            bgwork.DoWork += Bgwork_DoWork;
            bgwork.RunWorkerCompleted += Bgwork_RunWorkerCompleted;
            bgwork.WorkerReportsProgress = false;
            bgwork.WorkerSupportsCancellation = true;

            bgwork.RunWorkerAsync();
        }

        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            string locFile, rmtFile, rmtNm, thumb, locThumbFile, rmtThumbFile, fileNm, ext, rmtPath;
            string sql, tbl, tid, ctgr, spt, sn, year, cvp, shr;

            foreach (DataGridViewRow row in dgU.Rows)
            {
                thumb = "N"; locThumbFile = ""; rmtThumbFile = ""; shr = "";
                rmtNm = row.Cells["dgU_RmtFile"].Value.ToString();
                if (rmtNm.Contains("오류")) continue;

                tid = row.Cells["dgU_Tid"].Value.ToString();
                //shr = row.Cells["dgU_Shr"].Value.ToString();
                locFile = row.Cells["dgU_LocFile"].Value.ToString();
                FileInfo fi = new FileInfo(locFile);
                fileNm = fi.Name;
                //ext = fi.Extension ?? "";
                ctgr = rmtNm.Substring(0, 1);
                if (ctgr == "B" || ctgr == "C")
                {
                    locThumbFile = string.Format(@"{0}\T_{1}", fi.DirectoryName, fileNm);
                    //thumb = PrcSub_Thumb(locFile, locThumbFile);
                }
                Match match = Regex.Match(rmtNm, @"([A-F].)\-(\d{4})\-(\d{10})", RegexOptions.IgnoreCase);
                ctgr = match.Groups[1].Value;
                spt = match.Groups[2].Value;
                sn = match.Groups[3].Value;
                year = sn.Substring(0, 4);
                rmtPath = string.Format(@"{0}/{1}/{2}", ctgr, spt, year);
                rmtFile = string.Format(@"{0}/{1}", rmtPath, rmtNm);
                if (ftp1.Upload(locFile, rmtFile))
                {
                    if (thumb == "Y")
                    {
                        rmtThumbFile = string.Format(@"{0}/T_{1}", rmtPath, rmtNm);
                        ftp1.Upload(locThumbFile, rmtThumbFile);
                    }
                    //DB 처리
                    tbl = (Convert.ToDecimal(year) > 2004) ? ("ta_f" + year) : "ta_f2004";
                    /*if (ctgr == "AG" || ctgr == "DA" || ctgr == "DB" || ctgr.Substring(0, 1) == "E")    //개별문서-> 매각물건명세서, 등기, 기타문서
                    {
                        cvp = "ctgr='" + ctgr + "', spt='" + spt + "', tid='" + tid + "', sn='" + sn + "', file='" + fileNm + "', wdt=curdate()";
                    }
                    else
                    {
                        cvp = "ctgr='" + ctgr + "', spt='" + spt + "', sn='" + sn + "', file='" + fileNm + "', wdt=curdate()";
                    }*/
                    if (ctgr.Substring(0, 1) == "B" && shr == "Y")  //사진 공유
                    {
                        cvp = "ctgr='" + ctgr + "', spt='" + spt + "', sn='" + sn + "', file='" + rmtNm + "', wdt=curdate()";
                    }
                    else if (ctgr == "AA" || ctgr == "AB" || ctgr == "AC" || ctgr == "AD" || ctgr == "AE" || ctgr == "AF" || ctgr == "AH")  //물건 통합
                    {
                        cvp = "ctgr='" + ctgr + "', spt='" + spt + "', sn='" + sn + "', file='" + rmtNm + "', wdt=curdate()";
                    }
                    else
                    {
                        cvp = "ctgr='" + ctgr + "', spt='" + spt + "', tid='" + tid + "', sn='" + sn + "', file='" + rmtNm + "', wdt=curdate()";
                    }
                    sql = "insert into " + tbl + " set " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();
                    row.Cells["dgU_Rslt"].Value = "성공";
                    row.DefaultCellStyle.BackColor = Color.LightGreen;
                }
                else
                {
                    row.Cells["dgU_Rslt"].Value = "실패";
                    row.DefaultCellStyle.BackColor = Color.PaleVioletRed;
                }
            }
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            MessageBox.Show("작업이 완료 되었습니다.");
        }

        /// <summary>
        /// 파일 삭제
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnDelFile_Click(object sender, EventArgs e)
        {
            string sql;
            string tbl, idx, ctgr, spt, sn1, fileNm, rmtFile;

            if (dgF.SelectedRows.Count == 0)
            {
                MessageBox.Show("삭제할 파일을 선택 해 주세요.");
                return;
            }

            if (MessageBox.Show("선택한 파일을 삭제 하시겠습니까?", "파일 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            DataGridViewRow row = dgF.SelectedRows[0];

            tbl = row.Cells["dgF_Tbl"].Value.ToString();
            idx = row.Cells["dgF_Idx"].Value.ToString();

            db.Open();
            MySqlDataReader dr = db.ExeRdr($"select * from {tbl} where idx='{idx}' limit 1");
            dr.Read();
            ctgr = dr["ctgr"].ToString();
            spt = dr["spt"].ToString();
            sn1 = dr["sn"].ToString().Substring(0, 4);
            fileNm = dr["file"].ToString();
            db.Close();

            rmtFile = $"{ctgr}/{spt}/{sn1}/{fileNm}";
            ftp1.FtpDelete(rmtFile);

            db.Open();
            sql = $"delete from {tbl} where idx='{idx}'";
            db.ExeQry(sql);
            db.Close();

            MessageBox.Show("삭제 되었습니다.");
            dg_SelectionChanged(null, null);
        }

        /// <summary>
        /// 등기 자동발급 추가
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgL_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            string pin, tid, lsNo, lsDvsn;

            if (e.ColumnIndex < 0) return;

            int rowIdx = e.RowIndex;
            if (rowIdx == -1) return;
            string colNm = dgL.Columns[e.ColumnIndex].Name;
            if (dgL[e.ColumnIndex, rowIdx].Value == null) return;
            if (colNm != "dgL_Pin") return;
            pin = dgL[e.ColumnIndex, rowIdx].Value.ToString();
            if (pin == string.Empty) return;

            int noExtr = 0;
            string msg = "등기 자동발급에 추가 하시겠습니까?";
            if (chkNoExtr.Checked)
            {
                noExtr = 1;
                msg += "\r\n\r\n ※※※ 주의!!! 등기추출 안함 ※※※";
            }
            if (MessageBox.Show(msg, "등기발급", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No) return;

            tid = lnkTid.Text;
            lsNo = dgL["dgL_LsNo", rowIdx].Value.ToString();
            lsDvsn = dgL["dgL_Dvsn", rowIdx].Value.ToString();
            db.Open();
            if (db.ExistRow($"select idx from db_tank.tx_rgst_auto where tid='{tid}' and pin='{pin}' and wdt > date_sub(curdate(),INTERVAL 10 day) and ul=0"))
            {
                MessageBox.Show("이미 발급 또는 대기 상태의 등기 입니다.");
            }
            else
            {
                db.ExeQry($"insert into db_tank.tx_rgst_auto set dvsn=10, tid='{tid}', ls_no='{lsNo}', ls_type='{lsDvsn}', pin='{pin}', no_extr='{noExtr}', wdt=curdate(), wtm=curtime(), staff='{Properties.Settings.Default.USR_ID}'");
                MessageBox.Show("추가 되었습니다.");
            }
            db.Close();
        }

        private void btnWorkType1_Click(object sender, EventArgs e)
        {
            DateTime targetDt;

            targetDt = DateTime.Now.AddDays(14);
            cbxSrchSta1.SelectedValue = 11;
            cbxSrchSta2.SelectedValue = 1110;

            dtpBidDtBgn.Checked = true;
            dtpBidDtEnd.Checked = true;
            dtpBidDtBgn.Value = targetDt;
            dtpBidDtEnd.Value = targetDt;
        }

        /// <summary>
        /// PDF 파일 보기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgF_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int i;
            string tbl, idx, ctgr, spt, sn1, fileNm, url;

            i = e.RowIndex;
            if(i < 0)
            {
                MessageBox.Show("선택한 파일이 없습니다.");
                return;
            }

            tbl = dgF["dgF_Tbl", i].Value.ToString();
            idx = dgF["dgF_Idx", i].Value.ToString();

            db.Open();
            MySqlDataReader dr = db.ExeRdr($"select * from {tbl} where idx='{idx}' limit 1");
            dr.Read();
            ctgr = dr["ctgr"].ToString();
            spt = dr["spt"].ToString();
            sn1 = dr["sn"].ToString().Substring(0, 4);
            fileNm = dr["file"].ToString();
            db.Close();

            tbcL.SelectedTab = tabPdf;
            url = $"{myWeb}FILE/CA/{ctgr}/{spt}/{sn1}/{fileNm}";
            axAcroPDF1.src = url;
        }

        /// <summary>
        /// 물건복사-대상물건 검색
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCpSrch_Click(object sender, EventArgs e)
        {
            int i = -1;
            string tid, spt, sn1, sn2, pn, state, sql;

            dgCp.Rows.Clear();

            if (lnkTid.Text == string.Empty) return;
            tid = lnkTid.Text;

            db.Open();
            MySqlDataReader dr = db.ExeRdr($"select * from ta_list where tid='{tid}' limit 1");
            dr.Read();
            spt = dr["spt"].ToString();
            sn1 = dr["sn1"].ToString();
            sn2 = dr["sn2"].ToString();
            pn = dr["pn"].ToString();
            db.Close();

            sql = $"select tid, sn1, sn2, pn, sta2 from ta_list where spt='{spt}' and sn1='{sn1}' and sn2='{sn2}' and pn !='{pn}' and sta1 in (11,13) order by pn";
            db.Open();
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                var xState = dtStateCd.Rows.Cast<DataRow>().Where(t => t["sta2_cd"].ToString() == dr["sta2"].ToString()).SingleOrDefault();
                state = (xState == null || dr["sta2"].ToString() == "0") ? string.Empty : xState.Field<string>("sta2_nm");

                i = dgCp.Rows.Add();
                dgCp["dgCp_Sn", i].Value = string.Format("{0}-{1}", dr["sn1"], dr["sn2"]);
                dgCp["dgCp_Pn", i].Value = dr["pn"];
                dgCp["dgCp_State", i].Value = state;
                dgCp["dgCp_Tid", i].Value = dr["tid"];
            }
            dr.Close();
            db.Close();

            if (i == -1)
            {
                MessageBox.Show("관련물건이 없습니다.");
            }
            dgCp.ClearSelection();
        }

        /// <summary>
        /// 등기 파일복사
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCpRgstFile_Click(object sender, EventArgs e)
        {
            string spt, tid, sn, sn1, sn2, pn, seqNo, tbl, sql, cvp, ctgr, locFile, locFileCp, rmtFile, rmtNm, rgstDnPath;
            bool dnRslt = false;
            RgstAnalyNew rgstAnalyCA = new RgstAnalyNew();

            rgstDnPath = @"C:\등기파일\";
            if (!Directory.Exists(rgstDnPath))
            {
                Directory.CreateDirectory(rgstDnPath);
            }

            if (lnkTid.Text == string.Empty) return;
            tid = lnkTid.Text;

            db.Open();
            MySqlDataReader dr = db.ExeRdr($"select * from ta_list where tid='{tid}' limit 1");
            dr.Read();
            spt = dr["spt"].ToString();
            sn1 = dr["sn1"].ToString();
            sn2 = dr["sn2"].ToString();
            pn = dr["pn"].ToString();
            db.Close();

            sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));
            seqNo = "01";
            tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
            ctgr = (rdoCpRgstLand.Checked) ? "DA" : "DB";

            sql = $"select * from {tbl} where tid={tid} and ctgr='{ctgr}' limit 1";
            db.Open();
            dr = db.ExeRdr(sql);
            if (dr.HasRows)
            {
                dr.Read();
                rmtFile = $"{dr["ctgr"]}/{dr["spt"]}/{sn1}/{dr["file"]}";
                locFile = $@"{rgstDnPath}\{dr["file"]}";
                dnRslt = ftp1.Download(rmtFile, locFile, true);
            }
            else
            {
                MessageBox.Show("해당 등기파일이 없습니다.");
                dr.Close();
                db.Close();
                return;
            }
            dr.Close();
            db.Close();

            if (!dnRslt)
            {
                MessageBox.Show("해당 등기파일의 다운로드가 실패 했습니다.");
                return;
            }

            if (dgCp.Rows.Count == 0)
            {
                MessageBox.Show("[복사대상] 물건을 검색 해 주세요.");
                return;
            }

            if (string.Format("{0}-{1}", sn1, sn2) != dgCp["dgCp_Sn", 0].Value.ToString())
            {
                MessageBox.Show("현재 물건과 [복사대상] 물건의 사건번호가 일치하지 않습니다.");
                return;
            }

            var chkRows = from DataGridViewRow row in dgCp.Rows
                          where Convert.ToBoolean(row.Cells[0].Value) == true
                          select row;
            if (chkRows.Count() == 0)
            {
                MessageBox.Show("[복사대상] 물건을 체크 해 주세요.");
                return;
            }

            if (MessageBox.Show(chkRows.Count().ToString() + " 개의 물건으로 등기파일 복사를 하시겠습니까?", "등기파일 복사", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            foreach (DataGridViewRow row in chkRows)
            {
                tid = row.Cells["dgCp_Tid"].Value.ToString();
                pn = row.Cells["dgCp_Pn"].Value.ToString().PadLeft(4, '0');
                rmtNm = $"{ctgr}-{spt}-{sn}-{pn}-{seqNo}.pdf";
                rmtFile = $"{ctgr}/{spt}/{sn1}/{rmtNm}";
                cvp = $"ctgr='{ctgr}', spt='{spt}', tid='{tid}', sn='{sn}', file='{rmtNm}', wdt=curdate()";

                locFileCp = $@"{rgstDnPath}\{rmtNm}";
                File.Copy(locFile, locFileCp, true);
                //MessageBox.Show($"{locFile} || {rmtFile}");

                if (ftp1.Upload(locFile, rmtFile))
                {
                    sql = $"insert into {tbl} set {cvp} ON DUPLICATE KEY UPDATE {cvp}";
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();

                    if (chkCpRgstAnaly.Checked)
                    {
                        rgstAnalyCA.Proc(locFileCp, true, false);
                    }
                    row.Cells["dgCp_Note"].Value = "복사 완료";
                }
                else
                {
                    row.Cells["dgCp_Note"].Value = "실패";
                    row.DefaultCellStyle.BackColor = Color.LightPink;
                }
            }
            MessageBox.Show("처리 되었습니다.");
        }
    }
}
