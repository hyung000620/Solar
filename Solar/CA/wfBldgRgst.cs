using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using HtmlAgilityPack;
using hap = HtmlAgilityPack;
using HAPDoc = HtmlAgilityPack.HtmlDocument;
using System.IO;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using System.Net;
using System.Threading;
using System.Drawing.Drawing2D;

namespace Solar.CA
{
    public partial class wfBldgRgst : Form
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
        DataTable dtDpslCd;     //매각 구분
        DataTable dtRgstYn;     //등기 유무

        decimal totRowCnt = 0;
        string cdtn = "";

        BackgroundWorker bgwork;

        //ChromiumWebBrowser cWbr;

        string myWeb = Properties.Settings.Default.myWeb;
        FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "CA/", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);

        public wfBldgRgst()
        {
            InitializeComponent();

            init();
        }

        private void init()
        {
            ui.DgSetRead(dg, 0);
            ui.DgSetRead(dgL, 0);
            ui.DgSetRead(dgF, 0);
            ui.DgSetRead(dgU, 0);
            ui.SetPagn(panPagn, 100);

            //기타 모든 코드
            dtEtcCd = db.ExeDt("select * from ta_cd_etc order by seq, cd");

            //전체 법원별 계코드 DataTable
            dtDptCd = db.ExeDt("select C.ca_cd, crt_cd, spt_cd, cs_cd, dpt_cd, dpt_nm from ta_cd_cs C , ta_cd_dpt D where C.ca_cd=D.ca_cd order by dpt_cd");

            //법원 전체 코드
            //dtLawCd = auctCd.DtLawInfo();
            dtLawCd = auctCd.DtCsGrp();
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

            //등기 유무
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
                        
            //cWbr = new ChromiumWebBrowser("https://cloud.eais.go.kr/moct/awp/abb01/AWPABB01F01?returnUrl=%2F");
            //cWbr.Dock = DockStyle.Fill;
            //this.pnlChrome.Controls.Add(cWbr);

            wbr3.Navigate(Properties.Settings.Default.myWeb + "/SOLAR/ApslHtml.html");
        }

        private void cbxSrchCs_DrawItem(object sender, DrawItemEventArgs e)
        {
            Brush brush = null;
            Color color;
            ComboBox combo;
            Rectangle rect = e.Bounds;
            try
            {
                e.DrawBackground();

                combo = (System.Windows.Forms.ComboBox)sender;

                if (dtLawCd.Rows[e.Index]["csNm"].ToString().Contains("지법"))
                {
                    brush = Brushes.White;
                    color = Color.RoyalBlue;
                }
                else
                {
                    brush = Brushes.Black;
                    color = Color.White;
                }

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillRectangle(new SolidBrush(color), rect.X-1, rect.Y-2, rect.Width-1, rect.Height);
                e.Graphics.DrawString(dtLawCd.Rows[e.Index]["csNm"].ToString(), combo.Font, brush, e.Bounds.X, e.Bounds.Y);
            }
            catch
            {
                //
            }
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
            if (spt.Contains(",") == false)
            {
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
            string sql = "", subCdtn = "";

            cdtn = "1";
            dg.Rows.Clear();
            dgL.Rows.Clear();
            dgF.Rows.Clear();
            cbxApslDocCnt.Items.Clear();

            List<string> cdtnList = new List<string>();
            List<string> subCdtnList = new List<string>();

            txtSrchTid.Text = txtSrchTid.Text.Replace("_", string.Empty).Trim();
            if (txtSrchSn.Text.Trim() != "")
            {
                Match match = Regex.Match(txtSrchSn.Text.Trim(), @"^(\d+)[\-]*(\d+)*[\-]*(\d+)*", RegexOptions.Multiline);
                if (match.Groups[3].Value != "") cdtnList.Add("sn1=" + match.Groups[1].Value + " and sn2=" + match.Groups[2].Value + " and pn=" + match.Groups[3].Value);   //2018-4567-8
                else if (match.Groups[2].Value != "") cdtnList.Add("sn1=" + match.Groups[1].Value + " and sn2=" + match.Groups[2].Value);   //2018-4567
                else if (match.Groups[1].Value != "") cdtnList.Add("sn2=" + match.Groups[1].Value);     //4567
            }

            if (cbxSrchCs.SelectedIndex > 0) cdtnList.Add("spt IN (" + cbxSrchCs.SelectedValue.ToString() + ")");
            if (cbxSrchDpt.SelectedIndex > 0) cdtnList.Add("dpt=" + cbxSrchDpt.SelectedValue.ToString());
            if (cbxSrchSta1.SelectedIndex > 0) cdtnList.Add("sta1=" + cbxSrchSta1.SelectedValue.ToString());
            if (cbxSrchSta2.SelectedIndex > 0) cdtnList.Add("sta2=" + cbxSrchSta2.SelectedValue.ToString());
            if (cbxSrchCat.SelectedIndex > 0) cdtnList.Add("cat3=" + cbxSrchCat.SelectedValue.ToString());

            if (dtpBidDtBgn.Checked) cdtnList.Add("bid_dt >= '" + dtpBidDtBgn.Value.ToShortDateString() + "'");
            if (dtpBidDtEnd.Checked) cdtnList.Add("bid_dt <= '" + dtpBidDtEnd.Value.ToShortDateString() + "'");

            if (cdtnList.Count > 0) cdtn = string.Join(" and ", cdtnList.ToArray());
                        
            if (chkTgtCats.Checked)
            {
                //건축물대장 대상 종별(건물에서 집합제외)
                subCdtnList.Add("(cat1=20 and cat3 not in (201013,201014,201015,201017,201019,201020,201022,201111,201123,201130,201216))");
            }
            if (chkLandOnly.Checked)
            {
                //토지만 매각,토지만 매각(지분매각)
                subCdtnList.Add("(dpsl_dvsn in (13,16))");
            }
            if (chkIllegal.Checked)
            {
                //위반 건축물
                subCdtnList.Add("(FIND_IN_SET(11,sp_cdtn) > 0)");
            }
            if (subCdtnList.Count > 0)
            {
                subCdtn = string.Join(" or ", subCdtnList.ToArray());
                cdtn += " and (" + subCdtn + ")";
            }

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
            string sql = "", csCd = "", dpt = "", order = "", sort = "", state = "", cat = "", dpsl = "";
            string tbl, sn1, sn2, sn, tid;

            dg.Rows.Clear();
            dgL.Rows.Clear();
            dgF.Rows.Clear();
            cbxApslDocCnt.Items.Clear();

            //DataTable dt = new DataTable();
            //dt.Columns.Add("No");
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

            sql = "select * from ta_list L";
            sql += " where " + cdtn + " order by " + order + " limit " + startRow + "," + listScale.Value;

            this.Cursor = Cursors.WaitCursor;
            /*(db.Open();
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
                dg["dg_Dpsl", i].Value = dpsl;
            }
            dr.Close();
            db.Close();*/
            DataTable dt = db.ExeDt(sql);
            db.Open();
            foreach(DataRow dr in dt.Rows)
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
                dg["dg_Dpsl", i].Value = dpsl;

                //건축물대장 유무
                tid = dr["tid"].ToString();
                sn1 = dr["sn1"].ToString();
                sn2 = dr["sn2"].ToString();
                sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));
                tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
                sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));                
                sql = "select * from " + tbl + " where tid=" + tid + " and ctgr='EC' limit 1";
                if (db.ExistRow(sql))
                {
                    dg.Rows[i].DefaultCellStyle.BackColor = Color.Lavender;
                }
                else
                {
                    dg.Rows[i].DefaultCellStyle.BackColor = Color.White;
                }
            }
            db.Close();
            dg.ClearSelection();
            this.Cursor = Cursors.Default;

            dg.SelectionChanged += dg_SelectionChanged;
        }

        /// <summary>
        /// 물건 보기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dg_SelectionChanged(object sender, EventArgs e)
        {
            int i = 0, apslCnt = 0;
            string sql = "", tid = "", spt = "", sn1 = "", sn2 = "", pn = "", filePath = "", sn = "", rgstCls, tbl;

            this.Cursor = Cursors.WaitCursor;
            dgL.Rows.Clear();
            dgF.Rows.Clear();
            cbxApslDocCnt.Items.Clear();
            txtSpCdtn.Text = string.Empty;
            txtEtcNote.Text = string.Empty;
            chkSpIllegal.Checked = false;

            if (dg.CurrentRow == null)
            {
                this.Cursor = Cursors.Default;
                return;
            }

            i = dg.CurrentRow.Index;
            tid = dg["dg_Tid", i].Value.ToString();
            sql = "select spt,sn1,sn2,pn,img,etc_note,sp_cdtn,owner,rgst_yn from ta_list L , ta_dtl D where L.tid=D.tid and L.tid=" + tid + " limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            spt = dr["spt"].ToString();
            sn1 = dr["sn1"].ToString();
            sn2 = dr["sn2"].ToString();
            pn = dr["pn"].ToString();
            filePath = dr["img"].ToString();
            txtSpCdtn.Text = dr["sp_cdtn"].ToString();
            txtEtcNote.Text = dr["etc_note"].ToString();
            lblOwner.Text = string.Format("* 소유자 > " + dr["owner"]);
            
            if (dr["sp_cdtn"].ToString().Contains("11"))
            {
                chkSpIllegal.Checked = true;
            }

            cbxRgstYn.SelectedValue = dr["rgst_yn"];

            rgstCls = dr["rgst_yn"].ToString();
            if (rgstCls == "15") cbxRgstCls.SelectedIndex = 2;
            else if (rgstCls == "16") cbxRgstCls.SelectedIndex = 1;
            else cbxRgstCls.SelectedIndex = 0;
            dr.Close();
            db.Close();

            //관련 파일(토지/건물등기, 건축물대장)
            sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));
            tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
            sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));
            //sql = "select * from " + tbl + " where tid=" + tid + " or (spt=" + spt + " and sn='" + sn + "' and tid=0) order by seq, tid desc, ctgr";
            sql = "select * from " + tbl + " where (tid=" + tid + " and ctgr in ('DA','DB','EC')) or (spt=" + spt + " and sn='" + sn + "' and tid=0 and ctgr='AF') order by ctgr";
            db.Open();
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                if (dr["ctgr"].ToString() == "AF")
                {
                    apslCnt++;
                    continue;
                }

                i = dgF.Rows.Add();
                dgF["dgF_Ctgr", i].Value = dtFileCd.Rows.Cast<DataRow>().Where(t => t["cd"].ToString() == dr["ctgr"].ToString()).FirstOrDefault()["nm"].ToString();
                dgF["dgF_Wdt", i].Value = (dr["wdt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["wdt"]);
                dgF["dgF_Idx", i].Value = dr["idx"];
            }
            dr.Close();
            db.Close();
            dgF.ClearSelection();
            
            //목록 번호
            db.Open();
            //sql = "select S.*, B.sqm from ta_ls S , ta_bldg B where S.tid=B.tid and S.tid=" + tid + " and S.no=B.ls_no and B.dvsn=1 order by B.sqm desc";
            sql = "select S.*, B.sqm, B.tot_shr_sqm from ta_ls S , ta_bldg B where S.tid=B.tid and S.tid=" + tid + " and S.no=B.ls_no and B.dvsn=1 order by S.no";     //2021-12-03 창진이 요청
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                i = dgL.Rows.Add();
                dgL["dgL_LsNo", i].Value = dr["no"];
                dgL["dgL_Adrs", i].Value = dr["adrs"];
                dgL["dgL_Dvsn", i].Value = dr["dvsn"];
                dgL["dgL_Sqm", i].Value = (Convert.ToDecimal(dr["tot_shr_sqm"]) < 1) ? dr["sqm"] : dr["tot_shr_sqm"];
                dgL["dgL_Pnu", i].Value = dr["pnu"];
            }
            dr.Close();

            //sql = "select S.*, B.sqm from ta_ls S , ta_land B where S.tid=B.tid and S.tid=" + tid + " and S.no=B.ls_no order by B.sqm desc";
            sql = "select S.*, B.sqm from ta_ls S , ta_land B where S.tid=B.tid and S.tid=" + tid + " and S.no=B.ls_no order by S.no";     //2021-12-03 창진이 요청
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                i = dgL.Rows.Add();
                dgL["dgL_LsNo", i].Value = dr["no"];
                dgL["dgL_Adrs", i].Value = dr["adrs"];
                dgL["dgL_Dvsn", i].Value = dr["dvsn"];
                dgL["dgL_Sqm", i].Value = dr["sqm"];
                dgL["dgL_Pnu", i].Value = dr["pnu"];
                dgL.Rows[i].DefaultCellStyle.BackColor = Color.LightGray;
            }
            dr.Close();
            db.Close();
            dgL.ClearSelection();

            lnkTid.Text = tid;

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
                        
            Clipboard.SetText(tid + "_");

            this.Cursor = Cursors.Default;
        }

        /// <summary>
        /// TID 복사
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkTidCp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Clipboard.SetText(lnkTid.Text + "_");
        }

        /// <summary>
        /// 참고사항 저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSaveEtcNote_Click(object sender, EventArgs e)
        {   
            string tid, sql, spMsg = string.Empty;

            if (dg.CurrentRow == null)
            {
                return;
            }

            List<MySqlParameter> sp = new List<MySqlParameter>();

            tid = lnkTid.Text;
            sql = "update ta_dtl set etc_note=@etc_note where tid=" + tid;
            sp.Add(new MySqlParameter("@etc_note", txtEtcNote.Text.Trim()));
            db.Open();
            db.ExeQry(sql, sp);
            sp.Clear();
            db.Close();

            if (txtSpCdtn.Text.Contains("11") && chkSpIllegal.Checked == false)
            {
                //특수-위반건축물 제거
                //sql = "update ta_list set sp_cdtn=sp_cdtn-2048 where tid=" + tid;
                sql = "update ta_list set sp_cdtn=replace(sp_cdtn,'11','') where tid=" + tid;
                spMsg = "-> 특수-위반건축물 제거";
            }
            else if (txtSpCdtn.Text.Contains("11") == false && chkSpIllegal.Checked == true)
            {
                //특수-위반건축물 추가
                sql = "update ta_list set sp_cdtn=concat(sp_cdtn,',11') where tid=" + tid;
                spMsg = "-> 특수-위반건축물 추가";
            }

            if (spMsg != string.Empty)
            {
                db.Open();
                db.ExeQry(sql);
                db.Close();
            }

            MessageBox.Show("참고사항이 저장 되었습니다." + spMsg);
            dg_SelectionChanged(null, null);
        }

        /// <summary>
        /// 폐쇄등기 여부 저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSaveRgstCls_Click(object sender, EventArgs e)
        {
            string tid, sql, rgstCls = "";

            if (dg.CurrentRow == null)
            {
                return;
            }

            tid = lnkTid.Text;
            if (cbxRgstCls.SelectedIndex == 1) rgstCls = "16";
            else if (cbxRgstCls.SelectedIndex == 2) rgstCls = "15";
            else rgstCls = "";

            sql = "update ta_list set rgst_yn='" + rgstCls + "' where tid='" + tid + "' and (rgst_yn in (0,15,16,17))";
            db.Open();
            db.ExeQry(sql);
            db.Close();

            MessageBox.Show("폐쇄등기정보가 저장 되었습니다.");
            dg_SelectionChanged(null, null);
        }

        private void webView_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            //안먹힘 ㅠ
            edgeWeb2.Focus();
            SendKeys.Send("{TAB}");
            edgeWeb2.ExecuteScriptAsync("$('#membId').focus()");
            SendKeys.Send("gosegero2195");
            SendKeys.Send("{TAB}");
            //webView.ExecuteScriptAsync("$('#pwd').focus()");
            //SendKeys.Send("{TAB}");
            //webView.ExecuteScriptAsync("$('#membId').val('gosegero2195')");
            SendKeys.Send("hans2195~");
            //webView.ExecuteScriptAsync("$('#pwd').val('palau7695~')");
            //SendKeys.Send("{ENTER}");
        }

        /// <summary>
        /// 목록내역 클릭시 소재지 복사/세움터(웹)로 주소 입력
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgL_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            string srchAdrs;

            if (e.RowIndex == -1) return;

            srchAdrs = dgL["dgL_Adrs", e.RowIndex].Value.ToString().Trim();
            srchAdrs = Regex.Replace(srchAdrs, @",.*|\(.*", string.Empty);
            Clipboard.SetText(srchAdrs);

            edgeWeb2.Focus();    //포커스 중요
            edgeWeb2.ExecuteScriptAsync("$('.multiselect__input').focus()");
            edgeWeb2.ExecuteScriptAsync("$('.multiselect__input').val('')");
            
            //OK
            //webView.ExecuteScriptAsync("document.getElementsByClassName('multiselect__input')[0].focus()");
            //webView.ExecuteScriptAsync("document.getElementsByClassName('multiselect__input')[0].value=''");
            SendKeys.Send(srchAdrs);
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

            tbcL.SelectedTab = tabWbr2;

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

            if (lnkLbl == lnkCA_Sagun)
            {
                url = "RetrieveRealEstDetailInqSaList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&srnID=PNO101005&_SRCH_SRNID=PNO101005";
            }
            else if (lnkLbl == lnkCA_DtlInfo)
            {
                url = "RetrieveRealEstCarHvyMachineMulDetailInfo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&maemulSer=" + maemulSer;
            }

            url = "http://www.courtauction.go.kr/" + url;
            net.Nvgt(wbr2, url);
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
        /// 탱크 링크-내부 저장된 파일 보기(등기/건축물대장)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgF_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            string url;
            //if (e.ColumnIndex == 0) return;

            tbcL.SelectedTab = tabWbr1;
            url = string.Format(myWeb + "SOLAR/caFileViewer.php?tid={0}&idx={1}", lnkTid.Text, dgF["dgF_Idx", e.RowIndex].Value.ToString());
            wbr1.Navigate(url);
        }

        /// <summary>
        /// 탱크 웹-물건창
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkTid_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string url;
            if (string.IsNullOrEmpty(lnkTid.Text))
            {
                MessageBox.Show("선택한 물건이 없습니다.");
                return;
            }

            tbcL.SelectedTab = tabWbr3;            
            url = "/ca/caView.php?tid=" + lnkTid.Text;
            net.TankWebView(wbr3, url);
        }

        /// <summary>
        /// 파일 삭제
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnDelFile_Click(object sender, EventArgs e)
        {
            string year, tbl, idx, sql, rmtFile;

            DataGridViewSelectedRowCollection rows = dgF.SelectedRows;

            if (rows.Count == 0)
            {
                MessageBox.Show("삭제할 파일을 선택 해 주세요.");
                return;
            }

            if (MessageBox.Show("선택한 파일을 삭제 하시겠습니까?", "파일 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            year = dg["dg_SN", dg.CurrentRow.Index].Value.ToString().Substring(0, 4);
            tbl = (Convert.ToDecimal(year) > 2004) ? ("ta_f" + year) : "ta_f2004";

            idx = rows[0].Cells["dgF_Idx"].Value.ToString();
            sql = "select * from " + tbl + " where idx=" + idx;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            rmtFile = string.Format("{0}/{1}/{2}/{3}", dr["ctgr"], dr["spt"], year, dr["file"]);
            //MessageBox.Show("대상-" + rmtFile);
            if (ftp1.FtpFileExists(rmtFile))
            {
                ftp1.FtpDelete(rmtFile);
                //ftp1.FtpDelete("T_" + rmtFile);
            }
            dr.Close();

            sql = "delete from " + tbl + " where idx=" + idx;
            db.ExeQry(sql);
            db.Close();

            MessageBox.Show("삭제 되었습니다.");
            dg_SelectionChanged(null, null);
        }

        /// <summary>
        /// 업로드할 파일 찾기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOpenFiles_Click(object sender, EventArgs e)
        {
            int i = 0;
            string tid, ctgr, rmtNm, shr;

            dgU.Rows.Clear();

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "PDF 문서 (*.pdf)|*.pdf";
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

        private void webView_CoreWebView2InitializationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2InitializationCompletedEventArgs e)
        {
            //webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            //webView.CoreWebView2.WindowCloseRequested += CoreWebView2_WindowCloseRequested;
        }

        private void CoreWebView2_WindowCloseRequested(object sender, object e)
        {
            //
        }

        private void CoreWebView2_NewWindowRequested(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NewWindowRequestedEventArgs e)
        {
            //e.NewWindow = webView21.CoreWebView2;
        }

        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            string locFile, rmtFile, rmtNm, thumb, locThumbFile, rmtThumbFile, fileNm, rmtPath;
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
    }
}
