using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Security.Permissions;
using System.Runtime.InteropServices;
using HtmlAgilityPack;
using hap = HtmlAgilityPack;
using HAPDoc = HtmlAgilityPack.HtmlDocument;
using System.Diagnostics;

namespace Solar.CA
{
    [ComVisible(true)]

    public partial class wfFileMgmt : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        AuctCd auctCd = new AuctCd();
        NetUtil net = new NetUtil();
        ApiUtil api = new ApiUtil();

        DataTable dtLawCd, dtDptCd; //법원, 계
        DataTable dtCatCdAll, dtCatCd;  //물건 종별
        DataTable dtStateCd;    //진행 상태
        DataTable dtEtcCd;      //기타 모든 코드
        DataTable dtFileCd;     //파일 종류

        //정규식 기본형태
        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        //TANK-Web
        private CookieCollection Cookies;
        private CookieContainer cookieContainer;
        private string TankCook = string.Empty;
        //TANK-Web

        decimal totRowCnt = 0;
        string cdtn = "";

        BackgroundWorker bgwork;

        string myWeb = Properties.Settings.Default.myWeb;
        FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "CA/", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        
        public wfFileMgmt()
        {
            InitializeComponent();

            init();
        }

        private void wfFileMgmt_Load(object sender, EventArgs e)
        {
            wbr2.ObjectForScripting = true;
        }

        private void init()
        {
            ui.DgSetRead(dg, 0);
            ui.DgSetRead(dgP, 0);
            ui.DgSetRead(dgL, 0);
            ui.DgSetRead(dgF, 0);
            ui.DgSetRead(dgU, 0);
            ui.DgSetRead(dgP2, 0);
            ui.SetPagn(panPagn);
            
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
            cbxSrchCat.DataSource = dtCatCd;
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
            dtFileCd = db.ExeDt("select cd, nm from ta_cd_file order by cd");

            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            this.SetStyle(ControlStyles.UserPaint, true);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);

            //검색-Enter 키
            txtSrchTid.KeyDown += TxtEnter_KeyDown;
            txtSrchSn.KeyDown += TxtEnter_KeyDown;

            //상용 어구
            cbxPhrase1.SelectedIndexChanged += CbxPhrase_SelectedIndexChanged;

            //작업 유형별 검색조건 적용
            btnWorkType1.Click += BtnWorkType_Click;

            //tankCert();

            wbr2.Navigate(Properties.Settings.Default.myWeb + "/SOLAR/ApslHtml.html");
        }

        /// <summary>
        /// 작업 유형별 검색조건 적용
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnWorkType_Click(object sender, EventArgs e)
        {
            string workType = "";
            DateTime targetDt = DateTime.Now;
            Button btnWorkType = (Button)sender;
            workType = btnWorkType.Text;

            if (workType == "신건")
            {
                targetDt = DateTime.Now.AddDays(14);
                cbxSrchSta1.SelectedValue = 11;
                cbxSrchSta2.SelectedValue = 1110;
            }
            else if (workType == "매물")
            {
                targetDt = DateTime.Now.AddDays(7);
                cbxSrchSta1.SelectedValue = 11;
                cbxSrchSta2.SelectedValue = 1110;
            }
            else
            {
                //targetDt = DateTime.Now.AddDays(0);
                //if (chkWorkType.Checked) cbxSrchSta1.SelectedValue = 0;
                //else cbxSrchSta1.SelectedValue = 11;
                //cbxSrchSta2.SelectedValue = 0;
            }

            dtpBidDtBgn.Checked = true;
            dtpBidDtEnd.Checked = true;
            dtpBidDtBgn.Value = targetDt;
            dtpBidDtEnd.Value = targetDt;

            btnSrch_Click(null, null);
        }

        private void tankCert()
        {
            if (TankCook != string.Empty) return;

            wbr2.Navigate("https://www.tankauction.com/Mgmt");
            string ssUrl = "https://www.tankauction.com/Mgmt/cert_staff.php?staff_id=solar&staff_pwd=tank1544";
            this.Cookies = new CookieCollection();
            this.cookieContainer = new CookieContainer();

            HttpWebRequest hwr = (HttpWebRequest)WebRequest.Create(ssUrl);
            hwr.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.97 Safari/537.36";
            hwr.CookieContainer = this.cookieContainer;
            HttpWebResponse hwrsp = (HttpWebResponse)hwr.GetResponse();
            hwrsp.Cookies = hwr.CookieContainer.GetCookies(hwr.RequestUri);
            Cookies.Add(hwrsp.Cookies);

            foreach (Cookie cook in Cookies)
            {
                TankCook += (cook.Name + "=" + cook.Value + "; expires=" + cook.Expired + "; path=/ ;");
            }
        }

        /// <summary>
        /// 상용어구 입력
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CbxPhrase_SelectedIndexChanged(object sender, EventArgs e)
        {
            string phrase, ymd;

            TextBox tbx;
            ComboBox cbx = (ComboBox)sender;
            if (cbx.SelectedIndex == 0) return;

            ymd = string.Format("{0:yyyy.MM.dd}", DateTime.Now);
            phrase = cbx.Text;
            phrase = phrase.Replace("ymd", ymd);
            /*if (cbx == cbxPhrase0) tbx = txtLoca;
            else if (cbx == cbxPhrase1) tbx = txtEtcNote;
            else if (cbx == cbxPhrase2) tbx = txtAttnNote1;
            else if (cbx == cbxPhrase3) tbx = txtRgstNote;
            else if (cbx == cbxPhrase4) tbx = txtAttnNote2;
            else if (cbx == cbxPhrase5) tbx = txtAnalyNote;
            else tbx = txtLeasNote;*/

            tbx = txtEtcNote;

            tbx.Text += "\r\n" + phrase;
            tbx.Text = tbx.Text.Trim();
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
        /// 물건 검색
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSrch_Click(object sender, EventArgs e)
        {
            string sql = "";

            cdtn = "1";
            dg.Rows.Clear();
            dgP.Rows.Clear();
            dgL.Rows.Clear();
            dgF.Rows.Clear();
            dgP2.Rows.Clear();
            lblSaNo.Text = string.Empty;

            List<string> cdtnList = new List<string>();

            txtSrchTid.Text = txtSrchTid.Text.Replace("_", string.Empty).Trim();
            if (txtSrchSn.Text.Trim() != "")
            {
                Match match = Regex.Match(txtSrchSn.Text.Trim(), @"^(\d+)[\-]*(\d+)*[\-]*(\d+)*", RegexOptions.Multiline);
                if (match.Groups[3].Value != "") cdtnList.Add("sn1=" + match.Groups[1].Value + " and sn2=" + match.Groups[2].Value + " and pn=" + match.Groups[3].Value);   //2018-4567-8
                else if (match.Groups[2].Value != "") cdtnList.Add("sn1=" + match.Groups[1].Value + " and sn2=" + match.Groups[2].Value);   //2018-4567
                else if (match.Groups[1].Value != "") cdtnList.Add("sn2=" + match.Groups[1].Value);     //4567
            }

            if (chkOnlyPn.Checked) cdtnList.Add("pn > 0");

            if (cbxSrchCs.SelectedIndex > 0)
            { 
                if(chkMerg.Checked) cdtnList.Add("L.spt=" + cbxSrchCs.SelectedValue.ToString());
                else cdtnList.Add("spt=" + cbxSrchCs.SelectedValue.ToString());
            }
            if (cbxSrchDpt.SelectedIndex > 0) cdtnList.Add("dpt=" + cbxSrchDpt.SelectedValue.ToString());
            if (cbxSrchSta1.SelectedIndex > 0) cdtnList.Add("sta1=" + cbxSrchSta1.SelectedValue.ToString());
            if (cbxSrchSta2.SelectedIndex > 0) cdtnList.Add("sta2=" + cbxSrchSta2.SelectedValue.ToString());
            if (cbxSrchCat.SelectedIndex > 0) cdtnList.Add("cat3=" + cbxSrchCat.SelectedValue.ToString());

            if (dtpBidDtBgn.Checked) cdtnList.Add("bid_dt >= '" + dtpBidDtBgn.Value.ToShortDateString() + "'");
            if (dtpBidDtEnd.Checked) cdtnList.Add("bid_dt <= '" + dtpBidDtEnd.Value.ToShortDateString() + "'");

            if (dtp1stDtBgn.Checked) cdtnList.Add("(1st_dt >= '" + dtp1stDtBgn.Value.ToShortDateString() + "' or 2nd_dt >= '" + dtp1stDtBgn.Value.ToShortDateString() + "' or pre_dt >= '" + dtp1stDtBgn.Value.ToShortDateString() + "')");
            if (dtp1stDtEnd.Checked) cdtnList.Add("(1st_dt <= '" + dtp1stDtEnd.Value.ToShortDateString() + "' or 2nd_dt <= '" + dtp1stDtEnd.Value.ToShortDateString() + "' or pre_dt <= '" + dtp1stDtEnd.Value.ToShortDateString() + "')");

            if (chkMainX.Checked) cdtnList.Add("img = ''");

            if (cdtnList.Count > 0) cdtn = string.Join(" and ", cdtnList.ToArray());
            if (txtSrchTid.Text.Trim() != "")
            {
                cdtn = "tid IN (" + Regex.Replace(txtSrchTid.Text.Trim(), @"\D+", ",") + ")";   //TID 검색일 경우 모든 조건 무시
            }

            if (chkMerg.Checked)
            {
                sql = "select COUNT(distinct L.tid) from ta_list L , ta_merg M where L.tid=M.mtid and " + cdtn;
            }
            else
            {
                sql = "select COUNT(*) from ta_list where " + cdtn;
            }

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
            string sql = "", csCd = "", dpt = "", order = "", sort = "", state = "", cat = "";

            dg.Rows.Clear();
            dgP.Rows.Clear();
            dgL.Rows.Clear();
            dgF.Rows.Clear();
            dgP2.Rows.Clear();
            lblSaNo.Text = string.Empty;

            DataTable dt = new DataTable();
            dt.Columns.Add("No");
            ComboBox cbxPage = (ComboBox)sender;
            NumericUpDown listScale = (NumericUpDown)panPagn.Controls["_nudList"];
            startRow = (decimal.Parse(cbxPage.Text) - 1) * listScale.Value;
            dg.SelectionChanged -= dg_SelectionChanged;

            sort = cbxSrchSort.Text;
            if (sort == "사건번호")
            {
                order = (chkSortAsc.Checked) ? "L.spt, dtp, sn1, sn2, pn" : "L.spt, dpt, sn1 desc, sn2 desc, pn asc";
            }
            else if (sort == "유찰수")
            {
                order = (chkSortAsc.Checked) ? "fb_cnt" : "fb_cnt desc";
            }
            else if (sort == "입찰일")
            {
                order = (chkSortAsc.Checked) ? "bid_dt" : "bid_dt desc";
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

            if (chkMerg.Checked)
            {
                sql = "select L.* from ta_list L , ta_merg M";
                sql += " where L.tid=M.mtid and " + cdtn + " group by L.tid order by " + order + " limit " + startRow + "," + listScale.Value;
            }
            else
            {
                sql = "select * from ta_list L";
                sql += " where " + cdtn + " order by " + order + " limit " + startRow + "," + listScale.Value;
            }
            
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
                
                i = dg.Rows.Add();
                dg["dg_No", i].Value = totRowCnt - startRow - i;
                dg["dg_Tid", i].Value = dr["tid"];                
                dg["dg_CS", i].Value = auctCd.FindCsNm(csCd);
                dg["dg_Dpt", i].Value = dpt;
                dg["dg_SN", i].Value = (dr["pn"].ToString() == "0") ? string.Format("{0}-{1}", dr["sn1"], dr["sn2"]) : string.Format("{0}-{1} ({2})", dr["sn1"], dr["sn2"], dr["pn"]);
                dg["dg_Adrs", i].Value = dr["adrs"];
                dg["dg_BidDt", i].Value = dr["bid_dt"].ToString().Contains("0001") ? "" : string.Format("{0:yyyy.MM.dd}", dr["bid_dt"]);
                dg["dg_State", i].Value = state;
                dg["dg_Cat", i].Value = cat;                
                dg["dg_FbCnt", i].Value = dr["fb_cnt"];
                dg["dg_ApslAmt", i].Value = string.Format("{0:N0}", dr["apsl_amt"]);
                dg["dg_MinbAmt", i].Value = string.Format("{0:N0}", dr["minb_amt"]);
                dg["dg_2ndDt", i].Value = dr["2nd_dt"].ToString().Contains("0001") ? string.Format("{0:yyyy.MM.dd}", dr["1st_dt"]) : string.Format("{0:yyyy.MM.dd}", dr["2nd_dt"]);
                if (!dr["pre_dt"].ToString().Contains("0001"))
                {
                    dg.Rows[i].Cells[0].Style.BackColor = Color.LightGreen;     //선행공고로 등록건
                }
                if (dr["img"].ToString() != string.Empty)
                {
                    dg.Rows[i].Cells["dg_Tid"].Style.BackColor = Color.PeachPuff;
                }
            }
            dr.Close();
            db.Close();
            dg.ClearSelection();
            this.Cursor = Cursors.Default;

            dg.SelectionChanged += dg_SelectionChanged;
        }

        /// <summary>
        /// 물건 보기(물번 통합)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dg_SelectionChanged(object sender, EventArgs e)
        {
            int i = 0, shrCntS = 0, shrCntA = 0;
            string sql = "", tid = "", spt = "", sn1 = "", sn2 = "", pn = "", state = "", filePath = "", sn = "", tbl;
            
            this.Cursor = Cursors.WaitCursor;
            flowPnl.Controls.Clear();
            pbxZoom.Image = null;
            dgP.Rows.Clear();
            dgL.Rows.Clear();
            dgF.Rows.Clear();
            dgP2.Rows.Clear();
            lblSaNo.Text = string.Empty;
            txtEtcNote.Text = string.Empty;
            
            if (dg.CurrentRow == null)
            {
                this.Cursor = Cursors.Default;
                return;
            }

            i = dg.CurrentRow.Index;
            tid = dg["dg_Tid", i].Value.ToString();
            sql = "select spt,sn1,sn2,pn,img,etc_note,cat1,adrs,regn_adrs from ta_list L , ta_dtl D where L.tid=D.tid and L.tid=" + tid + " limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            spt = dr["spt"].ToString();
            sn1 = dr["sn1"].ToString();
            sn2 = dr["sn2"].ToString();
            pn = dr["pn"].ToString();
            filePath = dr["img"].ToString();
            lblEtcNotePn.Text = pn;
            txtEtcNote.Text = dr["etc_note"].ToString();

            if (dr["cat1"].ToString() == "30")
            {
                pnlCarAdrs.Visible = true;
                lblCarAdrs.Text = string.Format("{0} / {1}", dr["adrs"], dr["regn_adrs"]);
            }
            else
            {
                pnlCarAdrs.Visible = false;
                lblCarAdrs.Text = string.Empty;
            }
            dr.Close();

            //물건 번호
            List<string> lstTid = new List<string>();
            sql = "select tid, pn, sta2 from ta_list where spt=" + spt + " and sn1=" + sn1 + " and sn2=" + sn2 + " order by pn";
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                var xState = dtStateCd.Rows.Cast<DataRow>().Where(t => t["sta2_cd"].ToString() == dr["sta2"].ToString()).SingleOrDefault();
                state = (xState == null || dr["sta2"].ToString() == "0") ? string.Empty : xState.Field<string>("sta2_nm");
                i = dgP.Rows.Add();
                dgP["dgP_Pn", i].Value = dr["pn"];
                dgP["dgP_Tid", i].Value = dr["tid"];
                dgP["dgP_State", i].Value = state;

                dgP2.Rows.Add();
                dgP2["dgP2_Pn", i].Value = dr["pn"];
                dgP2["dgP2_Tid", i].Value = dr["tid"];
                dgP2["dgP2_State", i].Value = state;

                lstTid.Add(dr["tid"].ToString());
            }
            dr.Close();
            dgP.ClearSelection();
            db.Close();

            //사진 개수(현황/감평)
            sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));
            tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
            sql = "select * from " + tbl + " where ((tid in (" + string.Join(",", lstTid.ToArray()) + ")) or (spt=" + spt + " and sn='" + sn + "' and tid=0)) and ctgr in ('BA','BE') order by ctgr,file";
            DataTable dt = db.ExeDt(sql);
            if (dt.Rows.Count > 0)
            {
                shrCntS = dt.AsEnumerable().Where(x => (x["tid"].ToString() == "0" && x["ctgr"].ToString() == "BA")).Count();
                shrCntA = dt.AsEnumerable().Where(x => (x["tid"].ToString() == "0" && x["ctgr"].ToString() == "BE")).Count();
            }
            foreach (DataGridViewRow row in dgP.Rows)
            {
                row.Cells["dgP_CntS"].Value = shrCntS + dt.AsEnumerable().Where(x => (x["tid"].ToString() == row.Cells["dgP_Tid"].Value.ToString() && x["ctgr"].ToString() == "BA")).Count();
                row.Cells["dgP_CntA"].Value = shrCntA + dt.AsEnumerable().Where(x => (x["tid"].ToString() == row.Cells["dgP_Tid"].Value.ToString() && x["ctgr"].ToString() == "BE")).Count();
            }

            //목록 번호
            db.Open();
            sql = "select T.tid, pn, sta2, L.no, L.adrs, L.dvsn, L.pnu, L.note from ta_list T , ta_ls L where T.tid=L.tid and spt=" + spt + " and sn1=" + sn1 + " and sn2=" + sn2 + " order by pn, no";
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                i = dgL.Rows.Add();
                dgL["dgL_Pn", i].Value = dr["pn"];
                dgL["dgL_LsNo", i].Value = dr["no"];
                dgL["dgL_Adrs", i].Value = dr["adrs"];
                dgL["dgL_Dvsn", i].Value = dr["dvsn"];
                dgL["dgL_Note", i].Value = dr["note"];
                dgL["dgL_Pnu", i].Value = dr["pnu"];
                if ($"{dr["note"]}" != "미종국")
                {
                    dgL.Rows[i].DefaultCellStyle.BackColor = Color.Silver;
                }
            }
            dr.Close();            
            dgL.ClearSelection();

            //중복/병합 사건
            sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));
            sql = "select * from ta_merg where spt='" + spt + "' and (mno='" + sn + "' or cno='" + sn + "')";
            dr = db.ExeRdr(sql);
            if (dr.HasRows) lblMgSign.BackColor = Color.Coral;
            else lblMgSign.BackColor = Color.LightGray;
            dr.Close();            
            db.Close();

            lblSaNo.Text = string.Format("{0}-{1}", sn1, sn2);
            lblSaNo2.Text = string.Format("{0}-{1} ({2})", sn1, sn2, pn);
            lnkTid.Text = tid;
            lnkTid2.Text = tid;
            txtSptCd.Text = spt;

            GenPbxItem(spt, sn1, sn2);
            LoadFileInfo(tid);

            dgP2.ClearSelection();
            Clipboard.SetText(tid + "_");

            LoadMainImg(filePath);

            this.Cursor = Cursors.Default;
        }

        /// <summary>
        /// 개별 물건 번호
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgP_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int i = 0;
            string sql, tid, spt, sn1, sn2, pn, filePath = "", sn = "";

            this.Cursor = Cursors.WaitCursor;
            flowPnl.Controls.Clear();
            pbxZoom.Image = null;
            dgF.Rows.Clear();

            if (dgP.CurrentRow == null) return;

            i = dgP.CurrentRow.Index;
            tid = dgP["dgP_Tid", i].Value.ToString();
            sql = "select spt,sn1,sn2,pn,img,etc_note from ta_list L , ta_dtl D where L.tid=D.tid and L.tid=" + tid + " limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            spt = dr["spt"].ToString();
            sn1 = dr["sn1"].ToString();
            sn2 = dr["sn2"].ToString();
            pn = dr["pn"].ToString();
            filePath = dr["img"].ToString();
            lblEtcNotePn.Text = pn;
            txtEtcNote.Text = dr["etc_note"].ToString();
            dr.Close();
            
            //중복/병합 사건
            sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));
            sql = "select * from ta_merg where spt='" + spt + "' and (mno='" + sn + "' or cno='" + sn + "')";
            dr = db.ExeRdr(sql);
            if (dr.HasRows) lblMgSign.BackColor = Color.Coral;
            else lblMgSign.BackColor = Color.LightGray;
            dr.Close();
            db.Close();

            lblSaNo.Text = string.Format("{0}-{1} ({2})", sn1, sn2, pn);
            lblSaNo2.Text = string.Format("{0}-{1} ({2})", sn1, sn2, pn);
            lnkTid.Text = tid;
            lnkTid2.Text = tid;
            GenPbxItem(spt, sn1, sn2, tid);
            LoadFileInfo(tid);

            Clipboard.SetText(tid + "_");

            LoadMainImg(filePath);

            this.Cursor = Cursors.Default;
        }

        /// <summary>
        /// 대표 사진 보기
        /// </summary>
        /// <param name="filePath"></param>
        private void LoadMainImg(string filePath)
        {
            if (filePath == string.Empty)
            {
                pbxMain.Image = null;
                return;
            }

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://www.tankauction.com/FILE/CA/" + filePath);
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/59.0.3071.104 Safari/537.36";
            req.Method = "GET";
            req.CookieContainer = new CookieContainer();
            req.ContentType = "application/x-www-form-urlencoded";
            req.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9";
            Stream stream = null;
            try
            {
                HttpWebResponse res = (HttpWebResponse)req.GetResponse();
                stream = res.GetResponseStream();
                Bitmap img = Bitmap.FromStream(stream) as Bitmap;
                pbxMain.Image = img;
            }
            catch
            {
                //MessageBox.Show("오류 발생");
                pbxMain.Image = null;
            }            
        }

        /// <summary>
        /// 파일 정보
        /// </summary>
        private void LoadFileInfo(string tid)
        {
            int i = 0, n = 0, apslCnt = 0;
            string tbl, spt, sn1, sn2, sn, sql;

            dgF.Rows.Clear();
            cbxApslDocCnt.Items.Clear();

            sql = "select spt, sn1, sn2 from ta_list where tid=" + tid;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            sn1 = dr["sn1"].ToString();
            sn2 = dr["sn2"].ToString();
            spt = dr["spt"].ToString();
            dr.Close();
            db.Close();
                        
            sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));
            tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
            sql = "select * from " + tbl + " where tid=" + tid + " or (spt=" + spt + " and sn='" + sn + "' and tid=0) order by ctgr,file";
            db.Open();
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                n = dgF.Rows.Add();
                dgF["dgF_No", n].Value = n + 1;
                dgF["dgF_Ctgr", n].Value = dtFileCd.Rows.Cast<DataRow>().Where(t => t["cd"].ToString() == dr["ctgr"].ToString()).FirstOrDefault()["nm"].ToString();
                dgF["dgF_FileNm", n].Value = dr["file"];
                dgF["dgF_Src", n].Value = dr["src"];
                dgF["dgF_Note", n].Value = dr["note"];
                dgF["dgF_Wdt", n].Value = (dr["wdt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["wdt"]);
                dgF["dgF_Idx", n].Value = dr["idx"];

                //감정평가서 개수
                if (dr["ctgr"].ToString() == "AF")
                {
                    apslCnt++;
                }
            }
            dr.Close();
            db.Close();

            if (apslCnt > 0)
            {
                for (i = apslCnt; i > 0; i--)
                {
                    cbxApslDocCnt.Items.Add(i);
                }
                cbxApslDocCnt.SelectedIndex = 0;
            }
            else
            {
                cbxApslDocCnt.Text = string.Empty;
            }
            dgF.ClearSelection();
        }

        /// <summary>
        /// PictureBox 관련 폼 동적 생성
        /// </summary>
        /// <param name="spt"></param>
        /// <param name="sn1"></param>
        /// <param name="sn2"></param>
        /// <param name="tid"></param>
        private void GenPbxItem(string spt, string sn1, string sn2, string tid = null)
        {
            int n = 0, i = 0, apslCnt = 0;
            string sql, sn;
            string idx, imgUrl, fileCtgr, src;
            HttpWebRequest req;
            HttpWebResponse res;
            PictureBox pbx;

            cbxApslDocCnt.Items.Clear();

            Font titleFont = new Font("맑은 고딕", 8);

            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            string tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
            sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));
            if (tid == null)
            {
                sql = "select * from " + tbl + " where spt=" + spt + " and sn='" + sn + "' and tid=0 order by seq, ctgr";
            }
            else
            {
                sql = "select * from " + tbl + " where tid=" + tid + " or (spt=" + spt + " and sn='" + sn + "' and tid=0) order by seq, tid desc, ctgr";
            }
            
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                /*
                n = dgF.Rows.Add();
                dgF["dgF_No", n].Value = n + 1;
                dgF["dgF_Ctgr", n].Value = dtFileCd.Rows.Cast<DataRow>().Where(t => t["cd"].ToString() == dr["ctgr"].ToString()).FirstOrDefault()["nm"].ToString();
                dgF["dgF_FileNm", n].Value = dr["file"];
                dgF["dgF_Src", n].Value = dr["src"];
                dgF["dgF_Note", n].Value = dr["note"];
                dgF["dgF_Wdt", n].Value = string.Format("{0:yyyy.MM.dd}", dr["wdt"]);
                dgF["dgF_Idx", n].Value = dr["idx"];
                */
                if (dr["ctgr"].ToString() == "AF")
                {
                    apslCnt++;
                }

                fileCtgr = dr["ctgr"].ToString();
                if (fileCtgr.Substring(0, 1) != "B") continue;

                idx = dr["idx"].ToString();
                if (dr["src"].ToString() == string.Empty) src = string.Empty;
                else
                {
                    src = (dr["src"].ToString() == "감정평가서") ? "감" : "현";
                }

                //imgUrl = string.Format(Properties.Settings.Default.myWeb + "/FILE/CA/{0}/{1}/{2}/T_{3}", dr["ctgr"], dr["spt"], sn1, dr["file"]);
                imgUrl = string.Format(Properties.Settings.Default.myWeb + "/FILE/CA/{0}/{1}/{2}/{3}", dr["ctgr"], dr["spt"], sn1, dr["file"]);

                req = (HttpWebRequest)WebRequest.Create(imgUrl);
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/59.0.3071.104 Safari/537.36";
                req.Method = "GET";
                req.CookieContainer = new CookieContainer();
                req.ContentType = "application/x-www-form-urlencoded";
                req.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9";
                Stream stream = null;
                try
                {
                    res = (HttpWebResponse)req.GetResponse();
                    stream = res.GetResponseStream();
                }
                catch 
                {
                    continue;
                }
                               

                //개별 이미지 정보 담은 Panel
                Panel panel = new Panel();
                panel.Name = "pnl_" + idx;
                panel.Width = 340;
                panel.Height = 300;
                panel.BorderStyle = BorderStyle.FixedSingle;

                //체크 박스
                CheckBox chk = new CheckBox();
                chk.Name = "chk_" + idx;
                chk.Size = new Size(15, 20);
                chk.Location = new Point(10, 2);
                chk.CheckedChanged += Chk_CheckedChanged;

                //출처 및 설명 Label
                Label lbl = new Label();
                lbl.Width = 320;
                lbl.Text = string.Format("{0}> {1}", src, dr["note"]);
                lbl.Font = titleFont;
                lbl.Location = new Point(25, 5);
                                
                //사진 PictureBox
                pbx = new PictureBox();
                pbx.Name = "pbx_" + idx;
                pbx.BackColor = Color.Transparent;
                Bitmap img = Bitmap.FromStream(stream) as Bitmap;
                if (dr["hide"].ToString() == "1")
                {
                    Bitmap bmp = new Bitmap(img.Width, img.Height);
                    Graphics graphics = Graphics.FromImage(bmp);
                    ColorMatrix matrix = new ColorMatrix();
                    matrix.Matrix33 = 0.3f;
                    ImageAttributes attributes = new ImageAttributes();
                    attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    graphics.DrawImage(img, new Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, attributes);
                    graphics.Dispose();
                    pbx.Image = bmp;
                }
                else
                {
                    pbx.Image = img;
                }                
                pbx.Width = 320;
                pbx.Height = 240;
                pbx.SizeMode = PictureBoxSizeMode.StretchImage;
                pbx.Location = new Point(10, 25);
                pbx.Click += Pbx_Click;
                //pbx.MouseHover += Pbx_MouseHover;

                //
                TextBox tbxSeq = new TextBox();
                tbxSeq.Name = "tbxSeq_" + idx;
                tbxSeq.Size = new Size(40, 30);
                tbxSeq.Location = new Point(10, 270);
                tbxSeq.TextAlign = HorizontalAlignment.Center;
                tbxSeq.BackColor = (dr["tid"].ToString() == "0") ? Color.White : Color.GreenYellow;
                tbxSeq.Text = dr["seq"].ToString();

                //메인사진 선택 Button
                Button btnMain = new Button();
                btnMain.Name = "btnMain_" + idx;
                btnMain.Size = new Size(70, 25);
                btnMain.Location = new Point(60, 268);
                btnMain.Text = "대표사진";
                btnMain.Click += BtnMain_Click;

                TextBox tbxPno = new TextBox();
                tbxPno.Name = "tbxPno_" + idx;
                tbxPno.Size = new Size(180, 30);                
                tbxPno.Location = new Point(150, 270);

                panel.Controls.Add(chk);
                panel.Controls.Add(tbxSeq);
                panel.Controls.Add(tbxPno);
                panel.Controls.Add(pbx);
                panel.Controls.Add(lbl);
                panel.Controls.Add(btnMain);
                flowPnl.Controls.Add(panel);
            }
            dr.Close();
            db.Close();
            /*
            dgF.ClearSelection();
            
            //감정평가서 개수
            if (apslCnt > 0)
            {
                for (i = apslCnt; i > 0; i--)
                {
                    cbxApslDocCnt.Items.Add(i);
                }
                cbxApslDocCnt.SelectedIndex = 0;
            }
            else
            {
                cbxApslDocCnt.Text = string.Empty;
            }
            */
        }

        /// <summary>
        /// 물건 대표 사진 지정
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnMain_Click(object sender, EventArgs e)
        {
            string idx, tbl, year, sql, tid, filePath;

            tbcL.SelectedTab = tabSrchList;

            Button btn = sender as Button;
            idx = btn.Name.Replace("btnMain_", string.Empty);
            tid = lnkTid.Text;
            year = Regex.Match(lblSaNo.Text, @"(\d+)\-\d+").Groups[1].Value;
            tbl = (Convert.ToDecimal(year) > 2004) ? ("ta_f" + year) : "ta_f2004";
            
            sql = "select * from " + tbl + " where idx=" + idx;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            filePath = string.Format("{0}/{1}/{2}/T_{3}", dr["ctgr"], dr["spt"], year, dr["file"]);
            dr.Close();
            db.Close();

            sql = "update ta_list set img='" + filePath + "' where tid=" + tid;
            db.Open();
            db.ExeQry(sql);
            db.Close();

            LoadMainImg(filePath);
        }

        /*
        private void Pbx_MouseHover(object sender, EventArgs e)
        {
           PictureBox pbx = sender as PictureBox;
           pbxZoom.Image = pbx.Image;
        }
        */

        /// <summary>
        /// 사진 선택/해제-색상
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Chk_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox chk = sender as CheckBox;

            Panel panel = (Panel)this.Controls.Find(chk.Name.Replace("chk", "pnl"), true)[0];

            panel.BackColor = (chk.Checked) ? Color.Orange : Color.Gainsboro;
        }
        
        /// <summary>
        /// 사진 크게 보기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Pbx_Click(object sender, EventArgs e)
        {
            string idx, tbl, year, sql, note;

            tbcL.SelectedTab = tabSrchList;

            PictureBox pbx = sender as PictureBox;
            pbxZoom.Image = pbx.Image;
            idx = pbx.Name.Replace("pbx_", string.Empty);
            txtImgIdx.Text = idx;

            year = Regex.Match(lblSaNo.Text, @"(\d+)\-\d+").Groups[1].Value;
            tbl = (Convert.ToDecimal(year) > 2004) ? ("ta_f" + year) : "ta_f2004";
            txtImgTbl.Text = tbl;

            sql = "select * from " + tbl + " where idx=" + idx;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            txtImgNote.Text = dr["note"].ToString();
            lblImgFileNm.Text = dr["file"].ToString();
            dr.Close();
            db.Close();
        }

        /// <summary>
        /// 전체 선택/해제
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void chkPbxAll_CheckedChanged(object sender, EventArgs e)
        {
            foreach (Control ctrlBox in flowPnl.Controls)
            {
                if (ctrlBox.GetType() != typeof(Panel)) continue;
                foreach (Control ctrl in ctrlBox.Controls)
                {
                    if (ctrl.GetType() == typeof(CheckBox) && ctrl.Name.Contains("chk"))
                    {
                        CheckBox chk = ctrl as CheckBox;
                        chk.Checked = chkPbxAll.Checked;
                    }
                }
            }
        }

        /// <summary>
        /// 체크 삭제
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnChkDel_Click(object sender, EventArgs e)
        {
            string sql, tbl, idx, year, rmtFile;

            year = Regex.Match(lblSaNo.Text, @"(\d+)\-\d+").Groups[1].Value;
            tbl = (Convert.ToDecimal(year) > 2004) ? ("ta_f" + year) : "ta_f2004";

            foreach (Control ctrlBox in flowPnl.Controls)
            {
                if (ctrlBox.GetType() != typeof(Panel)) continue;
                foreach (Control ctrl in ctrlBox.Controls)
                {
                    if (ctrl.GetType() == typeof(CheckBox) && ctrl.Name.Contains("chk"))
                    {
                        CheckBox chk = ctrl as CheckBox;
                        if (!chk.Checked) continue;

                        idx = Regex.Match(chk.Name, @"_(\d+)").Groups[1].Value;
                        sql = "select * from " + tbl + " where idx=" + idx;
                        db.Open();
                        MySqlDataReader dr = db.ExeRdr(sql);
                        dr.Read();
                        rmtFile = string.Format("{0}/{1}/{2}/{3}", dr["ctgr"], dr["spt"], year, dr["file"]);
                        //MessageBox.Show("대상-" + rmtFile);
                        if (ftp1.FtpFileExists(rmtFile))
                        {
                            ftp1.FtpDelete(rmtFile);
                            ftp1.FtpDelete("T_" + rmtFile);
                        }
                        dr.Close();

                        sql = "delete from " + tbl + " where idx=" + idx;
                        db.ExeQry(sql);
                        db.Close();
                    }
                }
            }

            MessageBox.Show("삭제 되었습니다.");
            chkPbxAll.Checked = false;
            dg_SelectionChanged(null, null);
        }

        /// <summary>
        /// 체크 숨김 처리
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnChkHide_Click(object sender, EventArgs e)
        {
            string sql, tbl, idx, year;

            year = Regex.Match(lblSaNo.Text, @"(\d+)\-\d+").Groups[1].Value;
            tbl = (Convert.ToDecimal(year) > 2004) ? ("ta_f" + year) : "ta_f2004";

            foreach (Control ctrlBox in flowPnl.Controls)
            {
                if (ctrlBox.GetType() != typeof(Panel)) continue;
                foreach (Control ctrl in ctrlBox.Controls)
                {
                    if (ctrl.GetType() == typeof(CheckBox) && ctrl.Name.Contains("chk"))
                    {
                        CheckBox chk = ctrl as CheckBox;
                        if (!chk.Checked) continue;

                        idx = Regex.Match(chk.Name, @"_(\d+)").Groups[1].Value;
                        sql = "update " + tbl + " set hide=1 where idx=" + idx;
                        db.Open();
                        db.ExeQry(sql);
                        db.Close();
                    }
                }
            }

            MessageBox.Show("숨김처리 되었습니다.");
            chkPbxAll.Checked = false;
            dg_SelectionChanged(null, null);
        }

        /// <summary>
        /// 체크 숨김 해제
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnChkNoHide_Click(object sender, EventArgs e)
        {
            string sql, tbl, idx, year;

            year = Regex.Match(lblSaNo.Text, @"(\d+)\-\d+").Groups[1].Value;
            tbl = (Convert.ToDecimal(year) > 2004) ? ("ta_f" + year) : "ta_f2004";

            foreach (Control ctrlBox in flowPnl.Controls)
            {
                if (ctrlBox.GetType() != typeof(Panel)) continue;
                foreach (Control ctrl in ctrlBox.Controls)
                {
                    if (ctrl.GetType() == typeof(CheckBox) && ctrl.Name.Contains("chk"))
                    {
                        CheckBox chk = ctrl as CheckBox;
                        if (!chk.Checked) continue;

                        idx = Regex.Match(chk.Name, @"_(\d+)").Groups[1].Value;
                        sql = "update " + tbl + " set hide=0 where idx=" + idx;
                        db.Open();
                        db.ExeQry(sql);
                        db.Close();
                    }
                }
            }

            MessageBox.Show("숨김해제 되었습니다.");
            chkPbxAll.Checked = false;
            dg_SelectionChanged(null, null);
        }

        private void btnOpenFiles_Click(object sender, EventArgs e)
        {
            int i = 0;
            string tid, ctgr, rmtNm, shr;

            dgU.Rows.Clear();

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "사진 (*.jpg)|*.jpg|문서 (*.pdf,*.html)|*.pdf;*.html|사진 및 문서 (*.jpg,*.pdf,*.html)|*.jpg;*.pdf;*.html";
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
                dgU["dgU_Shr", i].Value = shr;
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
                thumb = "N"; locThumbFile = ""; rmtThumbFile = "";
                rmtNm = row.Cells["dgU_RmtFile"].Value.ToString();
                if (rmtNm.Contains("오류")) continue;

                tid = row.Cells["dgU_Tid"].Value.ToString();
                shr = row.Cells["dgU_Shr"].Value.ToString();
                locFile = row.Cells["dgU_LocFile"].Value.ToString();
                FileInfo fi = new FileInfo(locFile);
                fileNm = fi.Name;
                //ext = fi.Extension ?? "";
                ctgr = rmtNm.Substring(0, 1);
                if (ctgr == "B" || ctgr == "C")
                {
                    locThumbFile = string.Format(@"{0}\T_{1}", fi.DirectoryName, fileNm);
                    thumb = PrcSub_Thumb(locFile, locThumbFile);
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
        /// 개별 물건 번호-2
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgP2_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int i = 0, n = 0;
            string sql, tid, spt, sn, sn1, sn2, pn;

            this.Cursor = Cursors.WaitCursor;
            flowPnl.Controls.Clear();
            pbxZoom.Image = null;
            dgF.Rows.Clear();

            if (dgP2.CurrentRow == null) return;

            i = dgP2.CurrentRow.Index;
            tid = dgP2["dgP2_Tid", i].Value.ToString();
            sql = "select * from ta_list where tid=" + tid + " limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            spt = dr["spt"].ToString();
            sn1 = dr["sn1"].ToString();
            sn2 = dr["sn2"].ToString();
            pn = dr["pn"].ToString();
            dr.Close();
            db.Close();

            lblSaNo2.Text = string.Format("{0}-{1} ({2})", sn1, sn2, pn);
            //GenPbxItem(spt, sn1, sn2, tid);
            string tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
            sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));
            sql = "select * from " + tbl + " where tid=" + tid + " or (spt=" + spt + " and sn='" + sn + "' and tid=0) order by seq, tid desc, ctgr";
            db.Open();
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                n = dgF.Rows.Add();
                dgF["dgF_No", n].Value = n + 1;
                dgF["dgF_Ctgr", n].Value = dtFileCd.Rows.Cast<DataRow>().Where(t => t["cd"].ToString() == dr["ctgr"].ToString()).FirstOrDefault()["nm"].ToString();
                dgF["dgF_FileNm", n].Value = dr["file"];
                dgF["dgF_Src", n].Value = dr["src"];
                dgF["dgF_Note", n].Value = dr["note"];
                dgF["dgF_Wdt", n].Value = string.Format("{0:yyyy.MM.dd}", dr["wdt"]);
                dgF["dgF_Idx", n].Value = dr["idx"];

                if (dr["tid"].ToString() == "0") dgF.Rows[n].DefaultCellStyle.BackColor = Color.White;
                else dgF.Rows[n].DefaultCellStyle.BackColor = Color.LightGreen;
            }
            dr.Close();
            db.Close();

            dgF.ClearSelection();

            Clipboard.SetText(tid + "_");

            this.Cursor = Cursors.Default;
        }

        /// <summary>
        /// 썸네일 생성
        /// </summary>
        /// <param name="fileNm"></param>
        /// <returns></returns>
        private string PrcSub_Thumb(string fullNm, string thumbNm)
        {
            string result;
            //string fullNm = string.Format(@"{0}\{1}", filePath, fileNm);
            if (!File.Exists(fullNm) || !Regex.IsMatch(fullNm, @"bmp|gif|jpg|png|tiff"))
            {
                result = "N";
            }
            else
            {
                try
                {
                    Image image = Image.FromFile(fullNm);
                    Image thumb = image.GetThumbnailImage(200, 150, () => false, IntPtr.Zero);
                    //thumb.Save(string.Format(@"{0}\_thumb\{1}", filePath, fileNm));
                    thumb.Save(thumbNm);
                    result = "Y";
                }
                catch
                {
                    result = "N";
                }
            }

            return result;
        }

        /// <summary>
        /// 파일 일괄 삭제
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnDelFiles_Click(object sender, EventArgs e)
        {
            string idx, tbl, sql, year, rmtFile;

            var chkRows = from DataGridViewRow row in dgF.Rows
                          where Convert.ToBoolean(row.Cells[0].Value) == true
                          select row;
            if (chkRows.Count() == 0)
            {
                MessageBox.Show("삭제할 파일을 체크 해 주세요.");
                return;
            }

            if (MessageBox.Show("선택한 " + chkRows.Count().ToString() + "개의 파일을 삭제 하시겠습니까?", "파일 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            year = Regex.Match(lblSaNo2.Text, @"(\d+)\-\d+").Groups[1].Value;
            tbl = (Convert.ToDecimal(year) > 2004) ? ("ta_f" + year) : "ta_f2004";
                        
            foreach (DataGridViewRow row in chkRows)
            {
                idx = row.Cells["dgF_Idx"].Value.ToString();
                sql = "select * from " + tbl + " where idx=" + idx;
                db.Open();
                MySqlDataReader dr = db.ExeRdr(sql);
                dr.Read();
                rmtFile = string.Format("{0}/{1}/{2}/{3}", dr["ctgr"], dr["spt"], year, dr["file"]);
                //MessageBox.Show("대상-" + rmtFile);
                if (ftp1.FtpFileExists(rmtFile))
                {
                    ftp1.FtpDelete(rmtFile);
                    ftp1.FtpDelete("T_" + rmtFile);
                }
                dr.Close();

                sql = "delete from " + tbl + " where idx=" + idx;
                db.ExeQry(sql);
                db.Close();
            }

            MessageBox.Show("삭제 되었습니다.");

            dgP2_CellClick(null, null);
        }

        /// <summary>
        /// 사진 설명 저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSaveNote_Click(object sender, EventArgs e)
        {
            string sql;

            sql = "update " + txtImgTbl.Text + " set note='" + txtImgNote.Text.Trim() + "' where idx=" + txtImgIdx.Text;
            db.Open();
            db.ExeQry(sql);
            db.Close();

            MessageBox.Show("저장 되었습니다.");
        }

        /// <summary>
        /// 저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSave_Click(object sender, EventArgs e)
        {
            decimal n, noS, noE;
            string sql, pns, idx, pnRange, year, tbl;
            string ctgr, spt, tid, sn, sn1, sn2, file, src, note, seq = "";

            List<string> pnList = new List<string>();
            //Dictionary<string, string> dic = new Dictionary<string, string>();

            spt = txtSptCd.Text;

            Match match = Regex.Match(lblSaNo.Text, @"(\d+)\-(\d+)");
            sn1 = match.Groups[1].Value;
            sn2 = match.Groups[2].Value;
            tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";

            List<MySqlParameter> sp = new List<MySqlParameter>();

            sql = "select tid, pn from ta_list where spt=" + spt + " and sn1=" + sn1 + " and sn2=" + sn2;
            DataTable dt = db.ExeDt(sql);

            foreach (Control ctrlBox in flowPnl.Controls)
            {
                pnList.Clear();
                if (ctrlBox.GetType() != typeof(Panel)) continue;
                Panel panel = ctrlBox as Panel;
                idx = Regex.Match(panel.Name, @"_(\d+)").Groups[1].Value;                
                foreach (Control ctrl in panel.Controls)
                {
                    if (ctrl.GetType() == typeof(TextBox) && ctrl.Name.Contains("tbxPno"))
                    {
                        TextBox txtPns = ctrl as TextBox;
                        pns = txtPns.Text.Trim();
                        if (pns == string.Empty) continue;

                        string[] pnArr = pns.Split(new char[] { ',', '.', ' ' });
                        foreach (string pn in pnArr)
                        {
                            if (pn == string.Empty) continue;
                            if (pn.Contains("-") || pn.Contains("~"))
                            {
                                Match m = Regex.Match(pn, @"(\d+)[\-\~](\d+)");
                                if (!m.Success) continue;
                                noS = Convert.ToDecimal(m.Groups[1].Value);
                                noE = Convert.ToDecimal(m.Groups[2].Value);
                                if (noE < noS) continue;
                                for (n = noS; n <= noE; n++)
                                {
                                    if (!pnList.Contains(n.ToString()))
                                    {
                                        pnList.Add(n.ToString());
                                    }
                                }
                            }
                            else
                            {
                                if (!pnList.Contains(pn))
                                {
                                    pnList.Add(pn);
                                }
                            }
                        }                       
                    }
                    if (ctrl.GetType() == typeof(TextBox) && ctrl.Name.Contains("tbxSeq"))
                    {
                        TextBox txtSeq = ctrl as TextBox;
                        seq = txtSeq.Text.Trim();
                    }
                }
                //MessageBox.Show(pnList.Count.ToString());
                //if (pnList.Count == 0) continue;
                /*if (pnList.Count == 1)
                {
                    var xRow = dt.Rows.Cast<DataRow>().Where(t => t["pn"].ToString() == pnList[0]).FirstOrDefault();
                    if (xRow == null) continue;
                    tid = xRow["tid"].ToString();
                    sql = "update " + tbl + " set tid=" + tid + " where idx=" + idx;
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();
                }
                else
                {
                    sql = "select * from " + tbl + " where idx=" + idx;
                    DataTable dtF = db.ExeDt(sql);
                    DataRow row = dtF.Rows[0];
                    db.Open();
                    foreach (string pn in pnList)
                    {
                        var xRow = dt.Rows.Cast<DataRow>().Where(t => t["pn"].ToString() == pn).FirstOrDefault();
                        if (xRow == null) continue;
                        tid = xRow["tid"].ToString();

                        sql = "insert into " + tbl + " set ctgr=@ctgr, spt=@spt, tid=@tid, sn=@sn, file=@file, src=@src, note=@note, wdt=@wdt";
                        sp.Add(new MySqlParameter("@ctgr", row["ctgr"]));
                        sp.Add(new MySqlParameter("@spt", row["spt"]));
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@sn", row["sn"]));
                        sp.Add(new MySqlParameter("@file", row["file"]));
                        sp.Add(new MySqlParameter("@src", row["seq"]));
                        sp.Add(new MySqlParameter("@note", row["note"]));
                        sp.Add(new MySqlParameter("@wdt", row["wdt"]));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                    sql = "delete from " + tbl + " where idx=" + idx;
                    db.ExeQry(sql);
                    sp.Clear();
                    db.Close();
                }*/
                if (pnList.Count == 0)
                {
                    sql = "update " + tbl + " set seq=" + seq + " where idx=" + idx;
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();
                }
                else
                {
                    sql = "select * from " + tbl + " where idx=" + idx;
                    DataTable dtF = db.ExeDt(sql);
                    DataRow row = dtF.Rows[0];
                    db.Open();
                    foreach (string pn in pnList)
                    {
                        var xRow = dt.Rows.Cast<DataRow>().Where(t => t["pn"].ToString() == pn).FirstOrDefault();
                        if (xRow == null) continue;
                        tid = xRow["tid"].ToString();

                        sql = "insert into " + tbl + " set ctgr=@ctgr, spt=@spt, tid=@tid, sn=@sn, file=@file, src=@src, note=@note, wdt=@wdt";
                        sp.Add(new MySqlParameter("@ctgr", row["ctgr"]));
                        sp.Add(new MySqlParameter("@spt", row["spt"]));
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@sn", row["sn"]));
                        sp.Add(new MySqlParameter("@file", row["file"]));
                        sp.Add(new MySqlParameter("@src", row["src"]));
                        sp.Add(new MySqlParameter("@note", row["note"]));
                        sp.Add(new MySqlParameter("@wdt", row["wdt"]));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                    sql = "delete from " + tbl + " where idx=" + idx;
                    db.ExeQry(sql);
                    sp.Clear();
                    db.Close();
                }                
            }

            MessageBox.Show("저장 되었습니다.");
            chkPbxAll.Checked = false;

            if (lblSaNo.Text.Contains("("))
            {
                dgP_CellClick(null, null);
            }
            else
            {
                dg_SelectionChanged(null, null);
            }
        }

        /// <summary>
        /// 참고사항 저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSaveEtcNote_Click(object sender, EventArgs e)
        {
            int i = 0;
            string tid, sql;

            if (dg.CurrentRow == null)
            {
                return;
            }

            List<MySqlParameter> sp = new List<MySqlParameter>();

            //i = dg.CurrentRow.Index;
            //tid = dg["dg_Tid", i].Value.ToString();
            tid = lnkTid.Text;
            sql = "update ta_dtl set etc_note=@etc_note where tid=" + tid;
            sp.Add(new MySqlParameter("@etc_note", txtEtcNote.Text.Trim()));
            db.Open();
            db.ExeQry(sql, sp);
            sp.Clear();
            db.Close();

            MessageBox.Show("참고사항이 저장 되었습니다.");
        }

        private void lnkTid_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string url;
            if (string.IsNullOrEmpty(lnkTid.Text))
            {
                MessageBox.Show("선택한 물건이 없습니다.");
                return;
            }
            
            tbcL.SelectedTab = tabWbr2;            
            url = "/ca/caView.php?tid=" + lnkTid.Text;
            net.TankWebView(wbr2, url);
        }

        /// <summary>
        /// 감정평가서 열기(내부-명령회차 연동)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkTK_Apsl_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            int i = 0, docNo = 0;
            string sql, tid, spt, sn1, sn2, sn, url;
                        
            if (dg.CurrentRow == null) return;

            tbcL.SelectedTab = tabPdf;

            i = dg.CurrentRow.Index;
            tid = dg["dg_Tid", i].Value.ToString();
            sql = "select * from ta_list where tid=" + tid + " limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            spt = dr["spt"].ToString();
            sn1 = dr["sn1"].ToString();
            sn2 = dr["sn2"].ToString();
            dr.Close();
            db.Close();

            string tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
            sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));
            sql = "select * from " + tbl + " where spt=" + spt + " and sn='" + sn + "' and ctgr='AF' and tid=0 order by idx";
            DataTable dt = db.ExeDt(sql);
            
            if (dt.Rows.Count == 0)
            {
                MessageBox.Show("수집된 감정평가서가 없습니다.");
                return;
            }

            docNo = Convert.ToInt32(cbxApslDocCnt.Text) - 1;
            url = string.Format(myWeb + "FILE/CA/AF/{0}/{1}/{2}", spt, sn1, dt.Rows[docNo]["file"]);
            axAcroPDF1.src = url;
        }

        /// <summary>
        /// 탱크 링크-내부 저장된 파일 보기(문서, 사진 등)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgF_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            string url;
            if (e.ColumnIndex == 0) return;

            tbcL.SelectedTab = tabWbr2;
            url = string.Format(myWeb + "SOLAR/caFileViewer.php?tid={0}&idx={1}", lnkTid.Text, dgF["dgF_Idx", e.RowIndex].Value.ToString());
            if (chkNewIE.Checked)
            {
                Process.Start("IExplore", url);
            }
            else
            {
                wbr2.Navigate(url);
            }
        }

        /// <summary>
        /// 이미지 회전
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRote_Click(object sender, EventArgs e)
        {
            string sql, fileNm, locFile, locThumb, rmtPath, rmtFile, rmtThumb, thumb;
            string path = @"C:\SolarTmp";
            bool successFlag = false;

            if (Directory.Exists(path) == false)
            {
                Directory.CreateDirectory(path);
            }

            sql = "select * from " + txtImgTbl.Text + " where idx=" + txtImgIdx.Text;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            fileNm = dr["file"].ToString();
            locFile = string.Format("{0}/{1}.jpg", path, fileNm);
            rmtPath = string.Format("{0}/{1}/{2}", dr["ctgr"], dr["spt"], dr["sn"].ToString().Substring(0, 4));
            rmtFile = string.Format("{0}/{1}", rmtPath, fileNm);
            dr.Close();
            db.Close();

            Button btn = sender as Button;
            Bitmap bmp = new Bitmap(pbxZoom.Image);
            //MessageBox.Show(bmp.Size.ToString());
            if (btn == btnRoteW)
                bmp.RotateFlip(RotateFlipType.Rotate90FlipNone);
            else
                bmp.RotateFlip(RotateFlipType.Rotate270FlipNone);
            
            pbxZoom.Image = bmp;
            bmp.Save(locFile);

            locThumb = string.Format("{0}/T_{1}", path, fileNm);
            thumb = PrcSub_Thumb(locFile, locThumb);
            
            if (ftp1.Upload(locFile, rmtFile))
            {
                if (thumb == "Y")
                {
                    rmtThumb = string.Format(@"{0}/T_{1}", rmtPath, fileNm);
                    ftp1.Upload(locThumb, rmtThumb);
                    successFlag = true;
                }
            }
            
            if(successFlag)
                MessageBox.Show("서버에 [저장] 되었습니다.");
            else
                MessageBox.Show("작업중 오류가 발생 되었습니다.");
        }

        /// <summary>
        /// 토지이용계획열람-토지e음
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgL_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int i = 0;
            string url, pnu;

            if (dgL.CurrentRow == null) return;

            i = dgL.CurrentRow.Index;
            pnu = dgL["dgL_Pnu", i].Value.ToString();
            if (pnu == string.Empty || pnu == "0")
            {
                return;
            }

            tbcL.SelectedTab = tabWbr1;
            url = "https://www.eum.go.kr/web/ar/lu/luLandDet.jsp?mode=search&selGbn=umd&isNoScr=script&pnu=" + pnu;
            net.Nvgt(wbr1, url);
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

            tbcL.SelectedTab = tabWbr1;

            i = dg.CurrentRow.Index;
            tid = dg["dg_Tid", i].Value.ToString();

            sql = "select * from ta_list where tid=" + tid;
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

            if (lnkLbl == lnkCA_Apsl)
            {
                //url = "RetrieveRealEstSaGamEvalSeo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&orgSaNo=" + saNo + "&maemulSer=" + pn + "&maeGiil=" + maeGiil + "&mGakMulMseoYN=Y&corCanYN=N&jpDeptCd=" + jpDeptCd;
                jiwonNm = auctCd.FindLawNm(spt);
                wbr2.Document.InvokeScript("showGamEvalSeo", new string[] { jiwonNm, saNo, pn, maeGiil });
                return;
            }
            else if (lnkLbl == lnkCA_Sagun)
            {
                url = "RetrieveRealEstDetailInqSaList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&srnID=PNO101005&_SRCH_SRNID=PNO101005";
            }
            else if (lnkLbl == lnkCA_Sta)
            {
                //url = "RetrieveRealEstSaHjosa.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&ordHoi=1";
                url = "RetrieveRealEstSaHjosa.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo;
            }
            else if (lnkLbl == lnkCA_Ls)
            {
                url = "RetrieveRealEstHjosaDispMokrok.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo;
            }
            else if (lnkLbl == lnkCA_DtlInfo)
            {
                url = "RetrieveRealEstCarHvyMachineMulDetailInfo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&maemulSer=" + maemulSer;
            }
            else if (lnkLbl == lnkCA_Photo)
            {   
                url = "/RetrieveSaPhotoInfo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&mvSaNo=&maemulSer=" + pn + "&_NAVI_CMD=&_NAVI_SRNID=&_SRCH_SRNID=PNO102025&_CUR_CMD=InitMulSrch.laf&_CUR_SRNID=PNO102025&_NEXT_CMD=&_NEXT_SRNI=&_PRE_SRNID=&_LOGOUT_CHK=&_FORM_YN=";
            }

            url = "http://www.courtauction.go.kr/" + url;
            net.Nvgt(wbr1, url);
        }

        /// <summary>
        /// 웹브라우저에서 법원문서 파일저장/업로드
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCaDocSave_Click(object sender, EventArgs e)
        {
            int i = 0;
            string url, html, tid;
            string ctgr, filter, spt, year, sn1, sn2, sn, pn, seq, fileNm, locFile, rmtFile, tbl, cvp, sql;
            string dir = @"C:\경매문서\" + DateTime.Today.ToShortDateString();
            string stripTag = @"[</]+(a|img).*?>";
            bool dnFlag = false, ulFlag = false;

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Stream stream = wbr1.DocumentStream;
            StreamReader sr = new StreamReader(stream, Encoding.Default);
            html = sr.ReadToEnd();
            sr.Close();
            sr.Dispose();
            stream.Close();
            stream.Dispose();

            HAPDoc doc = new HAPDoc();
            Match match;

            url = wbr1.Url.ToString();
            if (url.Contains("/RetrieveRealEstDetailInqSaList.laf"))
            {
                ctgr = "AA";
                filter = "사건내역";
            }
            else if (url.Contains("/RetrieveRealEstSaDetailInqGiilList.laf"))
            {
                ctgr = "AB";
                filter = "기일내역";
            }
            else if (url.Contains("/RetrieveRealEstSaDetailInqMungunSongdalList.laf"))
            {
                ctgr = "AC";
                filter = "문건/송달내역";
            }
            else if (url.Contains("/RetrieveRealEstSaHjosa.laf"))
            {
                ctgr = "AD";
                filter = "현황조사내역";
            }
            else if (url.Contains("/RetrieveRealEstHjosaDispMokrok.laf"))
            {
                ctgr = "AE";
                filter = "부동산표시목록";
            }
            else
            {
                MessageBox.Show("수집대상 법원문서가 아닙니다.");
                return;
            }

            //spt = cbxCrtSpt.SelectedValue.ToString();
            //sn1 = cbxSn1.Text;
            //sn2 = txtSn2.Text;
            //pn = txtPn.Text;

            LinkLabel lnkLbl = sender as LinkLabel;
            if (dg.CurrentRow == null)
            {
                this.Cursor = Cursors.Default;
                return;
            }

            tbcL.SelectedTab = tabWbr1;

            i = dg.CurrentRow.Index;
            tid = dg["dg_Tid", i].Value.ToString();

            sql = "select * from ta_list where tid=" + tid;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            spt = dr["spt"].ToString();
            sn1 = dr["sn1"].ToString();
            sn2 = dr["sn2"].ToString();
            dr.Close();
            db.Close();

            if (ctgr == "AC" || ctgr == "AD" || ctgr == "AE")
            {
                dgF.Sort(dgF_FileNm, ListSortDirection.Descending);
                //DataGridViewRow row = dgF.Rows.Cast<DataGridViewRow>().Where(r => r.Cells["dgF_Ctgr"].Value.ToString().Equals("현황조사")).FirstOrDefault();
                DataGridViewRow row = dgF.Rows.Cast<DataGridViewRow>().Where(r => r.Cells["dgF_Ctgr"].Value.ToString().Contains(filter)).FirstOrDefault();
                if (row == null)
                {
                    seq = "1";
                }
                else
                {
                    match = Regex.Match(row.Cells["dgF_FileNm"].Value.ToString(), @"\-(\d{2,4})\.\w+");
                    if (match.Success)
                    {
                        seq = (Convert.ToInt32(match.Groups[1].Value) + 1).ToString();
                    }
                    else
                    {
                        seq = "1";
                    }
                }
                seq = seq.PadLeft(2, '0');
                locFile = string.Format(@"{0}\{1}-{2}-{3}{4}-{5}.html", dir, ctgr, spt, sn1, sn2.PadLeft(6, '0'), seq);
            }
            else
            {
                locFile = string.Format(@"{0}\{1}-{2}-{3}{4}.html", dir, ctgr, spt, sn1, sn2.PadLeft(6, '0'));
            }

            doc.LoadHtml(html);

            HtmlNodeCollection nc = doc.DocumentNode.SelectNodes("//div[@class='table_title' or @class='tbl_txt']|//table[@class='Ltbl_dt' or @class='Ltbl_list']");
            if (nc != null)
            {
                List<int> rmNode = new List<int>();
                foreach (HtmlNode nd in nc)
                {
                    if (nd.GetAttributeValue("summary", "") == "현황조사서 기본내역 표" || nd.InnerText.Contains("사진정보"))
                    {
                        rmNode.Add(nc.IndexOf(nd));
                    }
                }
                rmNode.Reverse();
                foreach (int ndIdx in rmNode)
                {
                    nc.RemoveAt(ndIdx);
                }
                var nodeList = new List<string>(nc.Select(node => node.OuterHtml));
                if (nodeList.Count > 0)
                {
                    string A1 = string.Join("\r\n", nodeList.ToArray());
                    A1 = Regex.Replace(A1, stripTag, string.Empty, rxOptS);
                    A1 = Regex.Replace(A1, @"^[\r\n\s]+", string.Empty, rxOptM);
                    File.WriteAllText(locFile, A1);
                    dnFlag = true;
                }
            }

            if (!dnFlag)
            {
                MessageBox.Show("파일 다운로드 실패");
                return;
            }
            //MessageBox.Show("ok");
            //return;

            //FTP 업로드
            match = Regex.Match(locFile, @"\w{2}\-(\d{4})\-(\d{4})(\d{6}).*", rxOptM);
            sn = string.Format("{0}{1}", match.Groups[2].Value, match.Groups[3].Value);
            year = match.Groups[2].Value;
            fileNm = match.Value;
            rmtFile = string.Format("{0}/{1}/{2}/{3}", ctgr, spt, year, fileNm);
            if (ftp1.Upload(locFile, rmtFile))
            {
                ulFlag = true;

                //DB 처리
                tbl = (Convert.ToDecimal(year) > 2004) ? ("ta_f" + year) : "ta_f2004";
                cvp = "ctgr='" + ctgr + "', spt='" + spt + "', sn='" + sn + "', file='" + fileNm + "', wdt=curdate()";
                sql = "insert into " + tbl + " set " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                db.Open();
                db.ExeQry(sql);
                db.Close();

                MessageBox.Show("파일이 서버에 저장 되었습니다.");

                //파일 정보 갱신
                LoadFileInfo(tid);
            }

            if (!ulFlag)
            {
                MessageBox.Show("파일 업로드 실패");
            }
        }
    }
}
