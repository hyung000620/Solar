using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
using mshtml;
using Microsoft.Web.WebView2.WinForms;

namespace Solar.PA
{
    public partial class wfPaMgmt : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        AuctCd auctCd = new AuctCd();
        NetUtil net = new NetUtil();

        ImageList imgList = new ImageList();

        BackgroundWorker bgwork;
        ManualResetEvent _busy = new ManualResetEvent(true);  //bgwork [PAUSE] or [RESUME]

        DataTable dtCatCd;      //물건 종별
        DataTable dtStateCd;    //진행 상태
        DataTable dtFileCd;     //파일 종류
        DataTable dtSpcCd;      //특수 조건
        DataTable dtEtcCd;      //기타 모든 코드

        //정규식 기본형태
        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        Dictionary<decimal, string> dictAdrsMtType = new Dictionary<decimal, string>(); //지번 유형
        Dictionary<string, string> dicFileDvsn;     //파일 구분
        decimal totRowCnt = 0;
        string cdtn = "";
        string myWeb = Properties.Settings.Default.myWeb;
        FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "PA/", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);

        public wfPaMgmt()
        {
            InitializeComponent();

            init();
        }

        private void init()
        {
            ui.DgSetRead(dg, 0);
            ui.DgSetRead(dgAr, 0);
            ui.DgSetRead(dgAp, 0);
            ui.DgSetRead(dgT, 0);
            ui.DgSetRead(dgR, 0);
            ui.DgSetRead(dgH, 0);
            ui.DgSetRead(dgF, 0);
            ui.DgSetRead(dgU, 0);

            ui.SetPagn(panPagn, rows: 100, min: 20, inc: 20);
            imgList.ImageSize = new Size(10, 20);
            lvSpc.SmallImageList = imgList;

            //기타 모든 코드
            dtEtcCd = db.ExeDt("select * from tb_cd_etc order by seq, cd");

            //물건종별 및 토지 지목
            dtCatCd = db.ExeDt("select ctgr_nm as cat3_nm, ctgr_cd as cat3_cd from tb_cd_cat where ctgr_lvl=4 and sumr5=1");
            DataRow row = dtCatCd.NewRow();
            row["cat3_cd"] = 0;
            row["cat3_nm"] = "-선택-";
            dtCatCd.Rows.InsertAt(row, 0);
            cbxCat3.DataSource = dtCatCd;
            cbxCat3.DisplayMember = "cat3_nm";
            cbxCat3.ValueMember = "cat3_cd";

            cbxSrchCat.DataSource = dtCatCd.Copy();
            cbxSrchCat.DisplayMember = "cat3_nm";
            cbxSrchCat.ValueMember = "cat3_cd";

            //진행 상태
            //dtStateCd = db.ExeDt("select sta1_cd, sta2_cd, sta1_nm, sta2_nm from ta_cd_sta order by sta2_cd");
            dtStateCd = new DataTable();
            dtStateCd.Columns.Add("sta1_cd");
            dtStateCd.Columns.Add("sta1_nm");
            row = dtStateCd.NewRow();
            row["sta1_cd"] = 0;
            row["sta1_nm"] = "-선택-";
            //row["sta2_cd"] = 0;
            //row["sta2_nm"] = "-선택-";
            dtStateCd.Rows.InsertAt(row, 0);
            row = dtStateCd.NewRow();
            row["sta1_cd"] = 1;
            row["sta1_nm"] = "진행";
            dtStateCd.Rows.InsertAt(row, 1);
            row = dtStateCd.NewRow();
            row["sta1_cd"] = 3;
            row["sta1_nm"] = "낙찰";
            dtStateCd.Rows.InsertAt(row, 2);
            row = dtStateCd.NewRow();
            row["sta1_cd"] = 9;
            row["sta1_nm"] = "기타";
            dtStateCd.Rows.InsertAt(row, 3);

            //cbxSrchSta1.DataSource = dtStateCd.Rows.Cast<DataRow>().GroupBy(g => g.Field<byte>("sta1_cd")).Select(t => t.First()).CopyToDataTable();
            cbxSrchSta1.DataSource = dtStateCd;
            cbxSrchSta1.DisplayMember = "sta1_nm";
            cbxSrchSta1.ValueMember = "sta1_cd";
            //cbxSrchSta1.SelectedIndexChanged += CbxSrchSta1_SelectedIndexChanged;
            cbxSrchSta1.SelectedValue = 1;

            //cbxState.DataSource = dtStateCd.Copy();
            //cbxState.DisplayMember = "sta2_nm";
            //cbxState.ValueMember = "sta2_cd";

            //지번 유형
            dictAdrsMtType.Add(0, "선택");
            dictAdrsMtType.Add(1, "일반");
            dictAdrsMtType.Add(2, "산");
            cbxAdrsMt.DataSource = new BindingSource(dictAdrsMtType, null);
            cbxAdrsMt.DisplayMember = "Value";
            cbxAdrsMt.ValueMember = "Key";
            cbxAdrsMt.SelectedValue = 0;

            //파일 구분
            dicFileDvsn = new Dictionary<string, string>();
            dicFileDvsn.Add("A", "사진");
            dicFileDvsn.Add("B", "지적도");
            dicFileDvsn.Add("C", "위치도");
            dicFileDvsn.Add("D", "감정평가서");
            dicFileDvsn.Add("F", "재산명세서");
            dicFileDvsn.Add("H", "세대열람내역");
            dicFileDvsn.Add("I", "토지등기");
            dicFileDvsn.Add("J", "건물등기");
            dicFileDvsn.Add("K", "건축물대장");

            //파일 구분
            dtFileCd = db.ExeDt("select cd, nm from tb_cd_file order by cd");

            //TextBox 숫자만 허용
            //txtSrchCltrNo.KeyPress += TxtNum_KeyPress;

            //Enter 검색
            txtSrchCltrNo.KeyDown += TxtEnter_KeyDown;
            txtSrchMgmtNo.KeyDown += TxtEnter_KeyDown;
            
            //ComboBox 마우스휠 무력화
            cbxCat3.MouseWheel += Cbx_MouseWheel;
            cbxState.MouseWheel += Cbx_MouseWheel;

            //인터넷 등기열람(해당물건)
            //dgI.CellClick += DgI_CellClick;
            //wbr2.Navigate("http://www.iros.go.kr/");

            //관리비 체납금액
            txtArrearsAmt.TextChanged += (s, ev) =>
            {
                TextBox tbx = s as TextBox;
                string kor = string.Empty;
                string val = tbx.Text.Replace(",", "");
                if (!string.IsNullOrEmpty(val))
                {
                    tbx.Text = string.Format("{0:#,##0}", Convert.ToDouble(val));
                    tbx.SelectionStart = tbx.TextLength;
                    tbx.SelectionLength = 0;
                    kor = NumToKor(Convert.ToInt64(val));
                    kor = Regex.Replace(kor, @"([십백천만억])", "$1 ");
                    lblArrearsAmtHan.Text = string.Format("> {0} 원", kor);
                }
                else
                {
                    lblArrearsAmtHan.Text = "> ";
                }
            };
            cbxArrearsMnth.SelectedIndexChanged += (s, ev) =>
            {
                txtArrearsPeriod.Text = (s as ComboBox).Text;
            };
            cbxArrearsMent1.SelectedIndexChanged += (s, ev) =>
            {
                txtArrearsNote.AppendText("\r\n" + (s as ComboBox).Text);
            };
            cbxArrearsMent2.SelectedIndexChanged += (s, ev) =>
            {
                txtArrearsNote.AppendText("\r\n" + (s as ComboBox).Text);
            };

            //특수 조건
            dtSpcCd = dtEtcCd.Select("dvsn=18").CopyToDataTable();
            foreach (DataRow r in dtSpcCd.Rows)
            {
                lvSpc.Items.Add(string.Format("{0}.{1}", r["cd"], r["nm_as"]));
            }
            Font spAutofont = new Font("맑은고딕", 10, FontStyle.Italic);
            foreach (ListViewItem item in lvSpc.Items)
            {
                if (item.SubItems[0].Text.Contains("A"))
                {
                    item.ForeColor = Color.Gray;
                    item.Font = spAutofont;
                }
            }
            
            //인터넷 등기소 등기핀 검출용-드래그, 우클릭 활성화
            webRgst.NavigationCompleted += (s, ev) => 
            {
                WebView2 wbr = (WebView2)s;                
                wbr.CoreWebView2.ExecuteScriptAsync("javascript:function r(d){d.oncontextmenu=null;d.ondragstart=null;d.onselectstart=null;d.onkeydown=null;d.onmousedown=null;d.body.oncontextmenu=null;d.body.ondragstart=null;d.body.onselectstart=null;d.body.onkeydown=null; d.body.onmousedown=null};function unify(w){r(w.document);if(w.frames.length>0){for(var i=0;i<w.frames.length;i++){try{unify(w.frames[i].window);}catch(e){}};};};unify(self);");
                wbr.CoreWebView2.FrameNavigationCompleted += (s2, ev2) => 
                {
                    Microsoft.Web.WebView2.Core.CoreWebView2 wbr2 = (Microsoft.Web.WebView2.Core.CoreWebView2)s2;
                    wbr2.ExecuteScriptAsync("javascript:function r(d){d.oncontextmenu=null;d.ondragstart=null;d.onselectstart=null;d.onkeydown=null;d.onmousedown=null;d.body.oncontextmenu=null;d.body.ondragstart=null;d.body.onselectstart=null;d.body.onkeydown=null; d.body.onmousedown=null};function unify(w){r(w.document);if(w.frames.length>0){for(var i=0;i<w.frames.length;i++){try{unify(w.frames[i].window);}catch(e){}};};};unify(self);");
                };
            };
        }

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
        /// 콤보박스 마우스 휠 무력화
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cbx_MouseWheel(object sender, MouseEventArgs e)
        {
            ((HandledMouseEventArgs)e).Handled = true;
        }

        private void DgI_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            string pin = "", regt_no = "";

            if (e.ColumnIndex < 0) return;

            DataGridView dgv = sender as DataGridView;
            int rowIdx = e.RowIndex;
            string colNm = dgv.Columns[e.ColumnIndex].Name;
            if (dgv[e.ColumnIndex, rowIdx].Value == null) return;

            if (colNm == "dgI_Pin")
            {
                pin = dgv[e.ColumnIndex, rowIdx].Value.ToString();
                regt_no = pin.Substring(0, 4);
                //wbr2.Navigate("http://www.iros.go.kr/iris/index.jsp?inpSvcCls=on&selkindcls=&e001admin_regn1=&e001admin_regn3=&a312lot_no=&a301buld_name=&a301buld_no_buld=&a301buld_no_room=&pin=" + pin + "&regt_no=" + regt_no + "&svc_cls=VW&fromjunja=Y", null, null, @"Referer: http://www.iros.go.kr/iris/hom/RHOMDetailSelect.jsp?pin=" + pin + "&regt_no=" + regt_no + "&from=CA");
            }
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
        /// TextBox 숫자만
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TxtNum_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!(char.IsDigit(e.KeyChar) || e.KeyChar == Convert.ToChar(Keys.Back)))
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// 물건 검색
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSrch_Click(object sender, EventArgs e)
        {
            string sql = "", sta1 = "1";

            cdtn = "1";
            dg.Rows.Clear();
            //ui.FormClear(tabDtl, new string[] { "cbxCrtSpt", "cbxDpt" });


            //등기작업 셋팅
            if (chkWorksRgstOrg.Checked)
            {
                cbxDpsl.Text = "매각";
                cbxSrchOrg.Text = "이용기관";
                //dtpBidDtBgn.Checked = true;
                //dtpBidDtBgn.Value = DateTime.Now.AddDays(+1);
            }
            if (chkWorksRgstKamco.Checked)
            {
                cbxDpsl.Text = "매각";
                cbxSrchOrg.Text = "캠코";
            }
            if (chkWorksRgstOrg.Checked && chkWorksRgstKamco.Checked)
            {
                cbxDpsl.Text = "매각";
                cbxSrchOrg.SelectedIndex = 0;
            }

            List<string> cdtnList = new List<string>();

            //if (txtSrchCltrNo.Text.Trim() != "") condList.Add("cltr_no=" + txtSrchCltrNo.Text.Trim());
            if (txtSrchMgmtNo.Text.Trim() != "") cdtnList.Add("cmgmt_no='" + txtSrchMgmtNo.Text.Trim() + "'");
            if (cbxSrchCat.SelectedIndex > 0) cdtnList.Add("cat3=" + cbxSrchCat.SelectedValue.ToString());
            if (cbxDpsl.SelectedIndex > 0) cdtnList.Add("dpsl_cd=" + cbxDpsl.SelectedIndex.ToString());
            if (cbxSrchOrg.SelectedIndex > 0) cdtnList.Add("org_dvsn=" + (cbxSrchOrg.SelectedIndex - 1).ToString());
            if (dtpBidDtBgn.Checked) cdtnList.Add("bgn_dtm >= '" + dtpBidDtBgn.Value.ToShortDateString() + "'");
            if (dtpBidDtEnd.Checked) cdtnList.Add("bgn_dtm <= '" + dtpBidDtEnd.Value.ToShortDateString() + "'");
            if (dtp1stDtBgn.Checked) cdtnList.Add("1st_dt >= '" + dtp1stDtBgn.Value.ToShortDateString() + "'");
            if (dtp1stDtEnd.Checked) cdtnList.Add("1st_dt <= '" + dtp1stDtEnd.Value.ToShortDateString() + "'");
            /*
            if (cbxSrchCsCd.SelectedIndex > 0) condList.Add("crt=" + cbxSrchCsCd.SelectedValue.ToString().Substring(0, 2) + " and spt=" + cbxSrchCsCd.SelectedValue.ToString().Substring(2, 2));            
            if (cbxSrchSta2.SelectedIndex > 0) condList.Add("sta2=" + cbxSrchSta2.SelectedValue.ToString());            
            */
            if (cbxSrchSta1.SelectedIndex > 0)
            {
                sta1 = cbxSrchSta1.SelectedValue.ToString();
                if (sta1 == "1") cdtnList.Add("stat_nm IN ('입찰준비중','인터넷입찰진행중','인터넷입찰마감','수의계약가능','입찰공고중','현장입찰진행중') and cls_dtm >= NOW()");
                else if (sta1 == "3") cdtnList.Add("stat_nm IN ('낙찰','낙찰(공유자매각결정)','낙찰(해제)')");
                else cdtnList.Add("stat_nm NOT IN ('입찰준비중','인터넷입찰진행중','낙찰','수의계약가능')");
            }
            if (chkSplSrch.Checked) cdtnList.Add("sp_cdtn > 0");

            if (chkCoordErr.Checked) cdtnList.Add("x=0");
            if (chkAptErr.Checked) cdtnList.Add("apt_cd=0");

            //건축물대장 작업용 종별제한
            if (chkWorksBldg.Checked)
            {
                cdtnList.Add("cat2 in (10001001,10001002,10001003,10001004) and cat3 not in(100010010001,100010010002,100010010003,100010010006,100010010008,100010010009,100010040003)");
            }

            if (cdtnList.Count > 0) cdtn = string.Join(" and ", cdtnList.ToArray());

            if (txtSrchCltrNo.Text.Trim() != "")
            {
                cdtn = "L.cltr_no IN (" + Regex.Replace(txtSrchCltrNo.Text.Trim(), @"\D+", ",") + ")";   //cltrNo 검색일 경우 모든 조건 무시
            }
            sql = "select COUNT(*) from tb_list L LEFT JOIN tb_file F ON L.cltr_no=F.cltr_no where " + cdtn;

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
            string sql = "", order = "", state = "", cat = "";

            dg.Rows.Clear();

            ComboBox cbxPage = (ComboBox)sender;
            NumericUpDown listScale = (NumericUpDown)panPagn.Controls["_nudList"];
            startRow = (decimal.Parse(cbxPage.Text) - 1) * listScale.Value;
            dg.SelectionChanged -= dg_SelectionChanged;

            //order = "cltr_no desc";
            order = "1st_dt desc, cltr_no desc";
            sql = "select L.*,date_format(bgn_dtm,'%Y-%m-%d') as bid_dt_as, date_format(1st_dt,'%Y-%m-%d') as 1st_dt_as,F.rgst, F.bldg_rgst from tb_list L LEFT JOIN tb_file F ON L.cltr_no=F.cltr_no";
            sql += " where " + cdtn + " order by " + order + " limit " + startRow + "," + listScale.Value;

            this.Cursor = Cursors.WaitCursor;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                //var xState = dtStateCd.Rows.Cast<DataRow>().Where(t => t["sta2_cd"].ToString() == dr["sta2"].ToString()).SingleOrDefault();
                //state = (xState == null || dr["sta2"].ToString() == "0") ? string.Empty : xState.Field<string>("sta2_nm");
                var xCat = dtCatCd.Rows.Cast<DataRow>().Where(t => t["cat3_cd"].ToString() == dr["cat3"].ToString()).SingleOrDefault();
                cat = (xCat == null || dr["cat3"].ToString() == "0") ? string.Empty : xCat.Field<string>("cat3_nm");

                i = dg.Rows.Add();
                dg["dg_No", i].Value = totRowCnt - startRow - i;
                dg["dg_CltrNo", i].Value = dr["cltr_no"];
                dg["dg_MgmtNo", i].Value = dr["cmgmt_no"];
                dg["dg_CltrNm", i].Value = dr["cltr_nm"];
                dg["dg_BidDt", i].Value = string.Format("{0:yyyy.MM.dd}", dr["bid_dt_as"]);
                dg["dg_State", i].Value = dr["stat_nm"];
                dg["dg_Cat", i].Value = cat;
                dg["dg_1stDt", i].Value = string.Format("{0:yyyy.MM.dd}", dr["1st_dt_as"]);
                
                //파일유무 색상구분
                if (dr["rgst"].ToString().Contains("I"))
                {
                    dg["dg_Frgst1", i].Value = "토";
                    dg["dg_Frgst1", i].Style.BackColor = Color.SandyBrown;                    
                }
                else
                {
                    dg["dg_Frgst1", i].Value = string.Empty;
                    dg["dg_Frgst1", i].Style.BackColor = Color.White;                    
                }

                if (dr["rgst"].ToString().Contains("J"))
                {
                    dg["dg_Frgst2", i].Value = "건";
                    dg["dg_Frgst2", i].Style.BackColor = Color.LightGray;
                }
                else
                {
                    dg["dg_Frgst2", i].Value = string.Empty;
                    dg["dg_Frgst2", i].Style.BackColor = Color.White;
                }

                if (dr["bldg_rgst"].ToString().Contains("K"))
                {
                    dg["dg_Fbldg", i].Value = "축";
                    dg["dg_Fbldg", i].Style.BackColor = Color.PaleGreen;
                }
                else
                {
                    dg["dg_Fbldg", i].Value = string.Empty;
                    dg["dg_Fbldg", i].Style.BackColor = Color.White;
                }
            }
            dr.Close();
            db.Close();
            dg.ClearSelection();
            this.Cursor = Cursors.Default;
            dg.SelectionChanged += dg_SelectionChanged;
        }

        /// <summary>
        /// 온비드 해당사건
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkOnbid_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            tbcL.SelectedTab = tabWbr1;

            string cltrNo = lnkCltrNo.Text;
            string cltrHstrNo = txtHstrNo.Text;
            string plnmNo = txtPlnmNo.Text;
            string pbctNo = txtPbctNo.Text;
            string pbctCdtnNo = txtCdtnNo.Text;
            string url = string.Format("http://www.onbid.co.kr/op/cta/cltrdtl/collateralRealEstateDetail.do?cltrNo={0}&cltrHstrNo={1}&plnmNo={2}&pbctNo={3}&scrnGrpCd=0001&pbctCdtnNo={4}", cltrNo, cltrHstrNo, plnmNo, pbctNo, pbctCdtnNo);            
            net.Nvgt(wbr1, url);
        }

        /// <summary>
        /// 물건 상세 정보
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dg_SelectionChanged(object sender, EventArgs e)
        {
            int i = 0;
            string sql, cltrNo;
            //string sql = "", cltrNo = "", js_pbct = "", js_area = "", js_apsl = "";
            //string js_caut = "", js_leas = "", js_rgst = "", js_shr = "", js_lsd = "";
            string js_abcd = "", js_rgst = "", js_bldg = "";
                        
            ui.FormClear(tabDtl, new string[] { "tpnlBasic" });
            ui.FormClear(tabBldgRgst);

            foreach (ListViewItem item in lvSpc.Items)
            {
                item.Checked = false;
                item.BackColor = Color.White;
            }

            if (dg.CurrentRow == null) return;
            i = dg.CurrentRow.Index;
            cltrNo = dg["dg_CltrNo", i].Value.ToString();
            lnkCltrNo.Text = cltrNo;
            lnkCpCltrNo.Text = cltrNo;

            Clipboard.SetText(cltrNo + "_");

            sql = "select * from tb_list L , tb_dtl D where L.cltr_no=D.cltr_no and L.cltr_no='" + cltrNo + "' limit 1";
            db.Open();            
            MySqlDataReader dr = db.ExeRdr(sql);
            if (!dr.HasRows)
            {
                dr.Close();
                dr.Dispose();
                db.Close();
                return;
            }
            dr.Read();
            lblMgmtNo.Text = dr["cmgmt_no"].ToString();
            lblDtInfo.Text = string.Format("{0:yyyy.MM.dd} / {1:yyyy.MM.dd HH:mm}", dr["1st_dt"], dr["mod_dtm"]);
            txtOrgNm.Text = dr["org_nm"].ToString();
            txtDptNm.Text = dr["dpt_nm"].ToString();
            txtFbCnt.Text = dr["fb_cnt"].ToString();
            if (dr["dpsl_cd"].ToString() == "1") rdoDpsl1.Checked = true;
            else rdoDpsl2.Checked = true;
            cbxCat3.SelectedValue = dr["cat3"];
            txtPrptDvsn.Text = dr["prpt_dvsn"].ToString();
            txtLandTotSqm.Text = dr["land_sqm"].ToString();
            txtBldgTotSqm.Text = dr["bldg_sqm"].ToString();
            mtxtBgnDtm.Text = string.Format("{0:yyyy-MM-dd HH:mm}", dr["bgn_dtm"]);
            mtxtClsDtm.Text = string.Format("{0:yyyy-MM-dd HH:mm}", dr["cls_dtm"]);
            mtxtExctDtm.Text = string.Format("{0:yyyy-MM-dd HH:mm}", dr["exct_dtm"]);
            txtApslAmt.Text = string.Format("{0:#,##0}", dr["apsl_amt"]);
            txtMinbAmt.Text = string.Format("{0:#,##0}", dr["minb_amt"]);
            txtSucbAmt.Text = string.Format("{0:#,##0}", dr["sucb_amt"]);
            txtCoordX.Text = dr["x"].ToString();
            txtCoordY.Text = dr["y"].ToString();
            txtSiCd.Text = dr["si_cd"].ToString();
            txtGuCd.Text = dr["gu_cd"].ToString();
            txtDnCd.Text = dr["dn_cd"].ToString();
            txtRiCd.Text = dr["ri_cd"].ToString();
            txtRegnAdrs.Text = dr["land_adrs"].ToString();
            txtAdrsNoM.Text = dr["m_adrs_no"].ToString();
            txtAdrsNoS.Text = dr["s_adrs_no"].ToString();
            txtRoadAdrs.Text = dr["road_adrs"].ToString();
            txtBldgNoM.Text = dr["m_bldg_no"].ToString();
            txtBldgNoS.Text = dr["s_bldg_no"].ToString();
            txtBldgNm.Text = dr["bldg_nm"].ToString();
            txtRoadNm.Text = dr["road_nm"].ToString();
            txtAptCd.Text = dr["apt_cd"].ToString();
            cbxAdrsMt.SelectedValue = Convert.ToDecimal(dr["mt"]);

            txtHjCd.Text = dr["hj_cd"].ToString();
            txtPnu.Text = dr["pnu"].ToString();
            txtDlvr.Text = dr["dlvr_rsby"].ToString();
            
            txtPosiEnv.Text = dr["posi_env"].ToString();
            txtUtlzPscd.Text = dr["utlz_pscd"].ToString();
            txtEtcDtl.Text = dr["etc_dtl"].ToString();
            txtIcdlCdtn.Text = dr["icdl_cdtn"].ToString();
            txtSezNote1.Text = dr["sez_note1"].ToString();
            txtSezNote2.Text = dr["sez_note2"].ToString();
            txtSezNote3.Text = dr["sez_note3"].ToString();

            txtHstrNo.Text = dr["hstr_no"].ToString();
            txtPlnmNo.Text = dr["plnm_no"].ToString();
            txtPbctNo.Text = dr["pbct_no"].ToString();
            txtCdtnNo.Text = dr["cdtn_no"].ToString();

            txtCpLandAdrs.Text = dr["land_adrs"].ToString();
            txtCpRoadAdrs.Text = dr["road_adrs"].ToString();

            txtPINLand.Text = dr["pin_land"].ToString();
            txtPINBldg.Text = dr["pin_bldg"].ToString();

            //특수조건
            if (dr["sp_cdtn"].ToString() != string.Empty)
            {
                string[] splArr = dr["sp_cdtn"].ToString().Split(',');
                foreach (ListViewItem item in lvSpc.Items)
                {
                    if (splArr.Contains(item.Text.Remove(item.Text.IndexOf("."))))
                    {
                        item.Checked = true;
                        item.BackColor = Color.LightGreen;
                    }
                    else item.Checked = false;
                }
            }
            dr.Close();

            //입찰 일정
            i = 0;
            sql = "select * from tb_pbct where cltr_no=" + cltrNo + " order by hstr_no";
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                i = dgH.Rows.Add();
                dgH["dgH_MnmtNo", i].Value = dr["bid_mnmt_no"];
                dgH["dgH_SeqDgr", i].Value = string.Format("{0}/{1}", dr["pbct_seq"], dr["pbct_dgr"]);
                dgH["dgH_BegnDtm", i].Value = dr["bgn_dtm"];
                dgH["dgH_ClsDtm", i].Value = dr["cls_dtm"];
                dgH["dgH_ExctDtm", i].Value = dr["exct_dtm"];
                dgH["dgH_MinPrc", i].Value = string.Format("{0:N0}", dr["minb_amt"]);
            }
            dr.Close();

            //면적 정보
            i = 0;
            sql = "select * from tb_area where cltr_no=" + cltrNo;
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                i = dgAr.Rows.Add();
                dgAr["dgAr_No", i].Value = i + 1;
                dgAr["dgAr_Dvsn", i].Value = string.Format("{0} > {1}", dr["dvsn_nm"], dr["usg_nm"]);
                dgAr["dgAr_Area", i].Value = string.Format("{0}{1}", dr["sqms"], dr["unit"]);
                dgAr["dgAr_Shr", i].Value = dr["shr_rt"];
                dgAr["dgAr_Note", i].Value = dr["note"];
                dgAr["dgAr_PIN", i].Value = dr["pin"];
            }
            dr.Close();

            //감정평가 정보
            i = 0;
            sql = "select * from tb_apsl where cltr_no=" + cltrNo;
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                i = dgAp.Rows.Add();
                dgAp["dgAp_No", i].Value = i + 1;
                dgAp["dgAp_Org", i].Value = dr["org_nm"];
                dgAp["dgAp_Dt", i].Value = dr["dt"];
                dgAp["dgAp_Amt", i].Value = string.Format("{0:N0}", dr["amt"]);
            }
            dr.Close();

            //임대차 정보(압류재산)
            i = 0;
            sql = "select * from tb_leas where cltr_no=" + cltrNo + " order by row_no";
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                i = dgT.Rows.Add();
                dgT["dgT_No", i].Value = i + 1;
                dgT["dgT_Dvsn", i].Value = dr["irst_dvsn_nm"];
                dgT["dgT_Irps", i].Value = dr["irst_irps_nm"];
                dgT["dgT_TdpsAmt", i].Value = dr["tdps_amt"];
                dgT["dgT_MnthAmt", i].Value = dr["mthr_amt"];
                dgT["dgT_ConvAmt", i].Value = dr["conv_grt_mony"];
                dgT["dgT_FxDt", i].Value = dr["fix_dt"];
                dgT["dgT_MvDt", i].Value = dr["mvn_dt"];
            }
            dr.Close();

            //등기사항 주요 정보(압류재산)
            i = 0;
            sql = "select * from tb_rgst where cltr_no=" + cltrNo + " order by row_no";
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                i = dgR.Rows.Add();
                dgR["dgR_No", i].Value = i + 1;
                dgR["dgR_Dvsn", i].Value = dr["irst_dvsn_nm"];
                dgR["dgR_Irps", i].Value = dr["irst_irps_nm"];
                dgR["dgR_Dt", i].Value = dr["rgst_dt"];
                dgR["dgR_Amt", i].Value = dr["stup_amt"];
            }
            dr.Close();

            //배분요구 및 채권신고현황(압류재산)
            i = 0;
            sql = "select * from tb_shr where cltr_no=" + cltrNo + " order by row_no";
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                i = dgS.Rows.Add();
                dgS["dgS_No", i].Value = i + 1;
            }
            dr.Close();

            //점유 관계(압류재산)
            i = 0;
            sql = "select * from tb_shr where cltr_no=" + cltrNo + " order by row_no";
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                i = dgO.Rows.Add();
                dgO["dgO_No", i].Value = i + 1;
            }
            dr.Close();

            //관리비 체납내역
            sql = "select * from db_tank.tx_arrears where tid=" + cltrNo + " and dvsn=2";
            dr = db.ExeRdr(sql);
            dr.Read();
            if (dr.HasRows)
            {
                txtArrearsAmt.Text = string.Format("{0:N0}", dr["amt"]);
                txtArrearsPeriod.Text = dr["period"].ToString();
                txtArrearsNote.Text = dr["note"].ToString();
                dtpArrearsWdt.Value = Convert.ToDateTime(dr["wdt"]);
            }
            dr.Close();

            //파일정보            
            //if (!chkFileInfo.Checked) return;
            dgF.Rows.Clear();
            sql = "select * from tb_file where cltr_no=" + cltrNo;            
            dr = db.ExeRdr(sql);
            if (dr.HasRows)
            {
                dr.Read();
                js_abcd = dr["js_abcd"].ToString();
                if (!string.IsNullOrEmpty(js_abcd))
                {
                    JArray jaAbcd = JArray.Parse(js_abcd);
                    foreach (JObject item in jaAbcd)
                    {
                        i = dgF.Rows.Add();
                        dgF["dgF_No", i].Value = i + 1;
                        dgF["dgF_Dvsn", i].Value = dicFileDvsn[item["ctgr"].ToString()];
                        dgF["dgF_Path", i].Value = item["fullNm"].ToString();
                        dgF["dgF_Thumb", i].Value = item["thumb"].ToString();
                        dgF["dgF_RgstDt", i].Value = item["rgstDt"].ToString();
                    }
                    jaAbcd.Clear();
                    //dgF.ClearSelection();
                }
                js_rgst = dr["rgst"].ToString();
                if (!string.IsNullOrEmpty(js_rgst))
                {
                    JArray jaRgst = JArray.Parse(js_rgst);
                    foreach (JObject item in jaRgst)
                    {
                        i = dgF.Rows.Add();
                        dgF["dgF_No", i].Value = i + 1;
                        dgF["dgF_Dvsn", i].Value = dicFileDvsn[item["ctgr"].ToString()];
                        dgF["dgF_Path", i].Value = item["fullNm"].ToString();
                        dgF["dgF_Thumb", i].Value = "N";
                        dgF["dgF_RgstDt", i].Value = item["rgstDt"].ToString();
                    }
                    jaRgst.Clear();
                    //dgF.ClearSelection();
                }
                if (!string.IsNullOrEmpty(dr["prpt_ls"].ToString()))
                {
                    JObject jObj = JObject.Parse(dr["prpt_ls"].ToString());
                    i = dgF.Rows.Add();
                    dgF["dgF_No", i].Value = i + 1;
                    dgF["dgF_Dvsn", i].Value = dicFileDvsn["F"];
                    dgF["dgF_Path", i].Value = jObj["fullNm"].ToString();
                    dgF["dgF_Thumb", i].Value = "N";
                    dgF["dgF_RgstDt", i].Value = jObj["rgstDt"].ToString();
                }
                if (!string.IsNullOrEmpty(dr["household"].ToString()))
                {
                    JObject jObj = JObject.Parse(dr["household"].ToString());
                    i = dgF.Rows.Add();
                    dgF["dgF_No", i].Value = i + 1;
                    dgF["dgF_Dvsn", i].Value = dicFileDvsn["H"];
                    dgF["dgF_Path", i].Value = jObj["fullNm"].ToString();
                    dgF["dgF_Thumb", i].Value = "N";
                    dgF["dgF_RgstDt", i].Value = jObj["rgstDt"].ToString();
                }
                js_bldg = dr["bldg_rgst"].ToString();
                if (!string.IsNullOrEmpty(js_bldg))
                {
                    JArray jaBldg = JArray.Parse(js_bldg);
                    foreach (JObject item in jaBldg)
                    {
                        i = dgF.Rows.Add();
                        dgF["dgF_No", i].Value = i + 1;
                        dgF["dgF_Dvsn", i].Value = dicFileDvsn[item["ctgr"].ToString()];
                        dgF["dgF_Path", i].Value = item["fullNm"].ToString();
                        dgF["dgF_Thumb", i].Value = "N";
                        dgF["dgF_RgstDt", i].Value = item["rgstDt"].ToString();
                    }
                    jaBldg.Clear();
                    //dgF.ClearSelection();
                }
            }
            dgH.ClearSelection();
            dgF.ClearSelection();
            dgAp.ClearSelection();
            dgAr.ClearSelection();
            dgT.ClearSelection();
            dgR.ClearSelection();
            dr.Close();
            dr.Dispose();
            db.Close();
        }
                
        /// <summary>
        /// 파일보기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgF_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            string fileNm = "", imgUrl = "";
                        
            //fileNm = dgF["dgF_Path", e.RowIndex].Value.ToString().Replace("/", "\\");
            fileNm = dgF["dgF_Path", e.RowIndex].Value.ToString();
            if (Regex.IsMatch(fileNm, @"pdf"))
            {
                tbcF.SelectedTab = tabPdf;
                axAcroPDF1.src = myWeb + "FILE/PA/" + fileNm;
            }
            else if (Regex.IsMatch(fileNm, @"bmp|jpg|jpeg|gif|png", RegexOptions.IgnoreCase))
            {
                imgUrl = myWeb + "FILE/PA/" + fileNm;
                //pbx.ImageLocation = imgUrl;    //오류(엑박)
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(imgUrl);
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
                    tbcF.SelectedTab = tabPbx;
                    pbx.Image = img;
                }
                catch(Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            else
            {
                MessageBox.Show("지원되지 않는 파일 형식 입니다.");
            }
        }

        private void lnkCltrNo_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string url;
            if (string.IsNullOrEmpty(lnkCltrNo.Text))
            {
                MessageBox.Show("선택한 물건이 없습니다.");
                return;
            }

            tbcL.SelectedTab = tabWbr1;
            //url = myWeb + "pa/paView.php?cltrNo=" + lnkCltrNo.Text;
            //wbr1.Document.Cookie = TankCook;
            //net.Nvgt(wbr1, url);
            url = "/pa/paView.php?cltrNo=" + lnkCltrNo.Text;
            net.TankWebView(wbr1, url);
        }

        /// <summary>
        /// 숫자 -> 한글
        /// </summary>
        /// <param name="lngNumber"></param>
        /// <returns></returns>
        private string NumToKor(long lngNumber)
        {
            //string kor = "";

            string[] NumberChar = new string[] { "", "일", "이", "삼", "사", "오", "육", "칠", "팔", "구" };
            string[] LevelChar = new string[] { "", "십", "백", "천" };
            string[] DecimalChar = new string[] { "", "만", "억", "조", "경" };

            string strMinus = string.Empty;

            if (lngNumber < 0)
            {
                strMinus = "마이너스";
                lngNumber *= -1;
            }

            string strValue = string.Format("{0}", lngNumber);
            string NumToKorea = string.Empty;
            bool UseDecimal = false;

            if (lngNumber == 0) return "영";

            for (int i = 0; i < strValue.Length; i++)
            {
                int Level = strValue.Length - i;
                if (strValue.Substring(i, 1) != "0")
                {
                    UseDecimal = true;
                    if (((Level - 1) % 4) == 0)
                    {
                        /*if (DecimalChar[(Level - 1) / 4] != string.Empty
                           && strValue.Substring(i, 1) == "1")
                            NumToKorea = NumToKorea + DecimalChar[(Level - 1) / 4];
                        else
                            NumToKorea = NumToKorea
                                              + NumberChar[int.Parse(strValue.Substring(i, 1))]
                                              + DecimalChar[(Level - 1) / 4];*/
                        NumToKorea = NumToKorea
                                              + NumberChar[int.Parse(strValue.Substring(i, 1))]
                                              + DecimalChar[(Level - 1) / 4];
                        UseDecimal = false;
                    }
                    else
                    {
                        /*if (strValue.Substring(i, 1) == "1")
                            NumToKorea = NumToKorea
                                               + LevelChar[(Level - 1) % 4];
                        else*/
                        NumToKorea = NumToKorea
                                           + NumberChar[int.Parse(strValue.Substring(i, 1))]
                                           + LevelChar[(Level - 1) % 4];
                    }
                }
                else
                {
                    if ((Level % 4 == 0) && UseDecimal)
                    {
                        NumToKorea = NumToKorea + DecimalChar[Level / 4];
                        UseDecimal = false;
                    }
                }
            }

            return strMinus + NumToKorea;
        }

        /// <summary>
        /// 관리비 체납내역 저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSaveArrears_Click(object sender, EventArgs e)
        {
            string tid, sql, cvp;

            tid = lnkCltrNo.Text;
            if (tid == string.Empty || tid == "CltrNo")
            {
                MessageBox.Show("선택된 물건이 없습니다.");
                return;
            }

            List<MySqlParameter> sp = new List<MySqlParameter>();

            cvp = "tid=@tid, dvsn=2, amt=@amt, period=@period, note=@note, wdt=@wdt";
            sql = "insert into db_tank.tx_arrears set " + cvp + ", staff=@staff ON DUPLICATE KEY UPDATE " + cvp;
            sp.Add(new MySqlParameter("@tid", tid));
            sp.Add(new MySqlParameter("@amt", txtArrearsAmt.Text.Replace(",", string.Empty).Trim()));
            sp.Add(new MySqlParameter("@period", txtArrearsPeriod.Text.Trim()));
            sp.Add(new MySqlParameter("@note", txtArrearsNote.Text.Trim()));
            sp.Add(new MySqlParameter("@wdt", dtpArrearsWdt.Value.ToShortDateString()));
            sp.Add(new MySqlParameter("@staff", Properties.Settings.Default.USR_ID));
            db.Open();
            db.ExeQry(sql, sp);
            sp.Clear();
            db.Close();

            MessageBox.Show("저장되었습니다.");
            dg_SelectionChanged(null, null);
        }

        /// <summary>
        /// 주소정보 저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSaveAdrs_Click(object sender, EventArgs e)
        {
            string cltrNo, sql, cvp;

            cltrNo = lnkCltrNo.Text;
            if (cltrNo == string.Empty || cltrNo == "CltrNo")
            {
                MessageBox.Show("선택된 물건이 없습니다.");
                return;
            }

            List<MySqlParameter> sp = new List<MySqlParameter>();

            sql = "update tb_list set land_adrs=@land_adrs, road_adrs=@road_adrs, m_adrs_no=@m_adrs_no, s_adrs_no=@s_adrs_no, mt=@mt, m_bldg_no=@m_bldg_no, s_bldg_no=@s_bldg_no, bldg_nm=@bldg_nm, road_nm=@road_nm, " +
                "si_cd=@si_cd, gu_cd=@gu_cd, dn_cd=@dn_cd, ri_cd=@ri_cd, x=@x, y=@y, apt_cd=@apt_cd " +
                "where cltr_no=" + cltrNo;
            sp.Add(new MySqlParameter("@land_adrs", txtRegnAdrs.Text.Trim()));
            sp.Add(new MySqlParameter("@road_adrs", txtRoadAdrs.Text.Trim()));
            sp.Add(new MySqlParameter("@m_adrs_no", txtAdrsNoM.Text.Trim()));
            sp.Add(new MySqlParameter("@s_adrs_no", txtAdrsNoS.Text.Trim()));
            sp.Add(new MySqlParameter("@mt", cbxAdrsMt.SelectedValue.ToString()));
            sp.Add(new MySqlParameter("@m_bldg_no", txtBldgNoM.Text.Trim()));
            sp.Add(new MySqlParameter("@s_bldg_no", txtBldgNoS.Text.Trim()));
            sp.Add(new MySqlParameter("@bldg_nm", txtBldgNm.Text.Trim()));
            sp.Add(new MySqlParameter("@road_nm", txtRoadNm.Text.Trim()));
            
            sp.Add(new MySqlParameter("@si_cd", txtSiCd.Text.Trim()));
            sp.Add(new MySqlParameter("@gu_cd", txtGuCd.Text.Trim()));
            sp.Add(new MySqlParameter("@dn_cd", txtDnCd.Text.Trim()));
            sp.Add(new MySqlParameter("@ri_cd", txtRiCd.Text.Trim()));
            sp.Add(new MySqlParameter("@x", txtCoordX.Text.Trim()));
            sp.Add(new MySqlParameter("@y", txtCoordY.Text.Trim()));
            sp.Add(new MySqlParameter("@apt_cd", txtAptCd.Text.Trim()));

            db.Open();
            db.ExeQry(sql, sp);
            sp.Clear();
            db.Close();

            MessageBox.Show("저장되었습니다.");
            dg_SelectionChanged(null, null);
        }

        /// <summary>
        /// 특수조건 저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSaveSpCdtn_Click(object sender, EventArgs e)
        {
            string spCdtn, sql, cltrNo;

            cltrNo = lnkCltrNo.Text;
            if (cltrNo == string.Empty || cltrNo == "CltrNo")
            {
                MessageBox.Show("선택된 물건이 없습니다.");
                return;
            }

            List<string> lstSpCdtn = new List<string>();
            foreach (ListViewItem item in lvSpc.CheckedItems)
            {
                lstSpCdtn.Add(item.Text.Remove(item.Text.IndexOf(".")));
            }
            spCdtn = string.Join(",", lstSpCdtn.ToArray());

            sql = "update tb_list set sp_cdtn='" + spCdtn + "' where cltr_no=" + cltrNo;
            db.Open();
            db.ExeQry(sql);
            db.Close();

            MessageBox.Show("저장되었습니다.");
            dg_SelectionChanged(null, null);
        }

        /// <summary>
        /// 전체 저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSaveAll_Click(object sender, EventArgs e)
        {
            string sql, cltrNo;
            //
        }

        /// <summary>
        /// 좌표/주소 코드 재매칭
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCoord_Click(object sender, EventArgs e)
        {
            string adrs;
            
            if (lnkCltrNo.Text == "CltrNo" || lnkCltrNo.Text == string.Empty) return;

            sfMap sfMap = new sfMap() { Owner = this };
            sfMap.StartPosition = FormStartPosition.CenterScreen;
            //sfMap.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            sfMap.FormBorderStyle = FormBorderStyle.Sizable;
            sfMap.ShowDialog();
            sfMap.Dispose();
        }

        /// <summary>
        /// 아파트 코드 찾기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnFindAptCd_Click(object sender, EventArgs e)
        {
            sfAptCd sfAptCd = new sfAptCd() { Owner = this };
            sfAptCd.StartPosition = FormStartPosition.CenterScreen;
            sfAptCd.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            sfAptCd.ShowDialog();
            sfAptCd.Dispose();
        }

        /// <summary>
        /// 목록의 이전/다음 사건 선택
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnListPrvNxt_Click(object sender, EventArgs e)
        {
            int rowCnt = 0, curIdx = 0, goIdx = 0, lastIdx = 0;
            Button btn = sender as Button;

            rowCnt = dg.Rows.Count;
            if (rowCnt == 0) return;

            lastIdx = rowCnt - 1;
            curIdx = dg.CurrentRow.Index;
            if (dg.CurrentRow.Index == -1) curIdx = 0;

            if (dg.SelectedRows.Count == 0) goIdx = 0;
            else
            {
                if (btn == btnListPrv)
                {
                    goIdx = curIdx - 1;
                    if (goIdx < 0)
                    {
                        MessageBox.Show("목록의 처음 입니다.");
                        return;
                    }
                }
                else
                {
                    goIdx = curIdx + 1;
                    if (goIdx > lastIdx)
                    {
                        MessageBox.Show("목록의 마지막 입니다.");
                        return;
                    }
                }
            }

            dg.SelectionChanged -= dg_SelectionChanged;

            dg.Rows[goIdx].Selected = true;
            dg.CurrentCell = dg.Rows[goIdx].Cells[0];
            dg_SelectionChanged(null, null);

            dg.SelectionChanged += dg_SelectionChanged;
        }

        /// <summary>
        /// CltrNo 복사
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkCpCltrNo_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {            
            Clipboard.SetText(lnkCpCltrNo.Text+"_9");
        }

        /// <summary>
        /// 주소복사-건축물대장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtCpAdrs_Click(object sender, EventArgs e)
        {
            TextBox tbx = sender as TextBox;
            if (tbx.Text == null || tbx.Text == string.Empty) return;

            tbx.Select(0, tbx.Text.Length);
            Clipboard.SetText(tbx.Text);
        }

        private void btnOpenFiles_Click(object sender, EventArgs e)
        {
            int i = 0;
            string cltrNo, ctgr, rmtNm, shr;

            dgU.Rows.Clear();

            OpenFileDialog ofd = new OpenFileDialog();
            //ofd.Filter = "사진 (*.jpg)|*.jpg|문서 (*.pdf,*.html)|*.pdf;*.html|사진 및 문서 (*.jpg,*.pdf,*.html)|*.jpg;*.pdf;*.html";
            //ofd.FilterIndex = 3;
            ofd.Filter = "건축물대장 및 등기 (*.pdf)|*.pdf";
            ofd.Multiselect = true;
            if (ofd.ShowDialog() != DialogResult.OK) return;

            foreach (string fullNm in ofd.FileNames)
            {
                cltrNo = string.Empty;
                ctgr = string.Empty;
                
                rmtNm = getRmtNm(fullNm);
                if (!rmtNm.Contains("오류"))
                {
                    Match match = Regex.Match(fullNm, @"(\d+)_(\d+)\-*(\d+)*.\w+", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                    cltrNo = match.Groups[1].Value;
                    ctgr = dtFileCd.Rows.Cast<DataRow>().Where(t => t["cd"].ToString() == rmtNm.Substring(0, 1)).FirstOrDefault()["nm"].ToString();
                }

                i = dgU.Rows.Add();
                dgU["dgU_No", i].Value = i + 1;
                dgU["dgU_LocFile", i].Value = fullNm;
                dgU["dgU_Ctgr", i].Value = ctgr;
                dgU["dgU_CltrNo", i].Value = cltrNo;
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
            string fileNm, ext, extType, cltrNo, ctgr, sql, spt, sn, pn, seqNo, rmtNm;

            Dictionary<int, string> dicDoc = new Dictionary<int, string>();
            dicDoc.Add(4, "I");
            dicDoc.Add(5, "J");
            dicDoc.Add(9, "K");

            FileInfo fi = new FileInfo(fullNm);
            fileNm = fi.Name;
            ext = fi.Extension?.Substring(1) ?? "";

            Match match = Regex.Match(fileNm, @"(\d+)_(\d+)\-*(\d+)*.\w+", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return "오류-파일명";
            }

            cltrNo = match.Groups[1].Value;
            mainNo = Convert.ToInt32(match.Groups[2].Value);
            subNo = string.IsNullOrEmpty(match.Groups[3].Value) ? 1 : Convert.ToInt32(match.Groups[3].Value);
            if (ext == "jpg" || ext == "png" || ext == "gif")
            {
                extType = "img";
                ctgr = "";
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

            sql = "select * from tb_list where cltr_no=" + cltrNo + " limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            if (dr.HasRows)
            {                
                if (extType == "img")
                {
                    rmtNm = "";
                }
                else
                {
                    seqNo = subNo.ToString().PadLeft(2, '0');
                    rmtNm = string.Format("{0}{1}-{2}.{3}", ctgr, cltrNo, seqNo, ext);
                }
            }
            else
            {
                rmtNm = "오류-해당 물건 없음(" + cltrNo + ")";
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
            string locFile, rmtFile, rmtNm, thumb, locThumbFile, rmtThumbFile, fileNm, ext, rmtPath, today;
            string sql, tbl, cltrNo, ctgr, dirNo, cvp = "", rgstInfo = "", bldgInfo = "";

            RgstAnalyPa rgstAnaly = new RgstAnalyPa();

            today = DateTime.Today.ToShortDateString();
            List<MySqlParameter> sp = new List<MySqlParameter>();

            foreach (DataGridViewRow row in dgU.Rows)
            {
                thumb = "N"; locThumbFile = ""; rmtThumbFile = "";
                rmtNm = row.Cells["dgU_RmtFile"].Value.ToString();
                if (rmtNm.Contains("오류")) continue;

                cltrNo = row.Cells["dgU_CltrNo"].Value.ToString();
                locFile = row.Cells["dgU_LocFile"].Value.ToString();
                FileInfo fi = new FileInfo(locFile);
                fileNm = fi.Name;
                //ext = fi.Extension ?? "";
                ctgr = rmtNm.Substring(0, 1);
                if (ctgr == "A" || ctgr == "B" || ctgr == "C")
                {
                    //locThumbFile = string.Format(@"{0}\T_{1}", fi.DirectoryName, fileNm);
                    //thumb = PrcSub_Thumb(locFile, locThumbFile);
                }

                dirNo = (Math.Ceiling(Convert.ToDecimal(cltrNo) / 100000) * 100000).ToString().PadLeft(7, '0');
                rmtFile = string.Format(@"{0}/{1}/{2}", ctgr, dirNo, rmtNm);
                                
                if (ftp1.Upload(locFile, rmtFile))
                {
                    sql = "select * from tb_file where cltr_no=" + cltrNo + " limit 1";
                    db.Open();
                    MySqlDataReader dr= db.ExeRdr(sql);
                    bool dbExist = dr.HasRows;
                    dr.Read();
                    if (dbExist)
                    {
                        rgstInfo = dr["rgst"].ToString().Trim();
                        bldgInfo = dr["bldg_rgst"].ToString().Trim();
                    }
                    else
                    {
                        rgstInfo = string.Empty;
                        bldgInfo = string.Empty;
                    }
                    dr.Close();
                    db.Close();

                    if (ctgr == "I" || ctgr == "J")     //등기(토지, 건물(집합))
                    {
                        var jaFile = new JArray();
                        if (!dbExist || rgstInfo == string.Empty)
                        {
                            var obj = new JObject();
                            obj.Add("fullNm", rmtFile);
                            obj.Add("ctgr", ctgr);
                            obj.Add("rgstDt", today);
                            jaFile.Add(obj);
                        }
                        else
                        {
                            bool newItem = true;
                            JArray jaRgst = JArray.Parse(rgstInfo);                            
                            foreach (JObject item in jaRgst)
                            {
                                var obj = new JObject();
                                obj.Add("fullNm", item["fullNm"].ToString());
                                obj.Add("ctgr", item["ctgr"].ToString());
                                if (item["fullNm"].ToString() == rmtFile)
                                {
                                    newItem = false;
                                    obj.Add("rgstDt", today);
                                }
                                else
                                {
                                    obj.Add("rgstDt", item["rgstDt"]);
                                }                                
                                jaFile.Add(obj);
                            }
                            if (newItem)
                            {
                                var obj = new JObject();
                                obj.Add("fullNm", rmtFile);
                                obj.Add("ctgr", ctgr);
                                obj.Add("rgstDt", today);
                                jaFile.Add(obj);
                            }
                        }
                        cvp = "rgst=@rgst";
                        sp.Add(new MySqlParameter("@rgst", jaFile.ToString()));
                    }
                    else if (ctgr == "K")       //건축물대장
                    {
                        var jaFile = new JArray();
                        if (!dbExist || bldgInfo == string.Empty)
                        {
                            var obj = new JObject();
                            obj.Add("fullNm", rmtFile);
                            obj.Add("ctgr", ctgr);
                            obj.Add("rgstDt", today);
                            jaFile.Add(obj);
                        }
                        else
                        {
                            bool newItem = true;
                            JArray jaBldg = JArray.Parse(bldgInfo);                            
                            foreach (JObject item in jaBldg)
                            {
                                var obj = new JObject();
                                obj.Add("fullNm", item["fullNm"].ToString());
                                obj.Add("ctgr", ctgr);
                                if (item["fullNm"].ToString() == rmtFile)
                                {
                                    newItem = false;
                                    obj.Add("rgstDt", today);
                                }
                                else
                                {
                                    obj.Add("rgstDt", item["rgstDt"]);
                                }
                                jaFile.Add(obj);
                            }
                            if (newItem)
                            {
                                var obj = new JObject();
                                obj.Add("fullNm", rmtFile);
                                obj.Add("ctgr", ctgr);
                                obj.Add("rgstDt", today);
                                jaFile.Add(obj);
                            }
                        }
                        cvp = "bldg_rgst=@bldg_rgst";
                        sp.Add(new MySqlParameter("@bldg_rgst", jaFile.ToString()));
                    }

                    sql = "insert into tb_file set cltr_no=@cltr_no, " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
                    sp.Add(new MySqlParameter("@cltr_no", cltrNo));

                    db.Open();
                    db.ExeQry(sql, sp);
                    sp.Clear();
                    db.Close();
                    row.Cells["dgU_Rslt"].Value = "성공";
                    row.DefaultCellStyle.BackColor = Color.LightGreen;

                    //등기추출
                    if (ctgr == "I" || ctgr == "J")
                    {                        
                        string analyRslt = rgstAnaly.Proc(locFile, true);
                        if (analyRslt == "success")
                        {
                            row.Cells["dgU_RgstAnaly"].Value = "Y";
                        }
                        else
                        {
                            row.Cells["dgU_RgstAnaly"].Value = analyRslt;
                            row.Cells["dgU_RgstAnaly"].Style.BackColor = Color.HotPink;
                        }
                    }
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

        private void btnTmpChJson_Click(object sender, EventArgs e)
        {
            string sql;

            sql = "select * from tb_file where bldg_rgst != '' order by cltr_no";
            DataTable dt = db.ExeDt(sql);

            foreach (DataRow row in dt.Rows)
            {
                var jaFile = new JArray();
                var obj = new JObject();

                JObject o = JObject.Parse(row["bldg_rgst"].ToString());
                obj.Add("fullNm", o["fullNm"].ToString());
                obj.Add("ctgr", "K");
                obj.Add("rgstDt", o["rgstDt"].ToString());
                jaFile.Add(obj);

                db.Open();
                sql = "update tb_file set bldg_rgst2='" + jaFile.ToString() + "' where cltr_no=" + row["cltr_no"].ToString();
                db.ExeQry(sql);
                db.Close();
            }

            MessageBox.Show("ok");
        }

        /// <summary>
        /// 업로드 후 물건창보기(web)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgU_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            string url, cltrNo;

            cltrNo = dgU["dgU_CltrNo", e.RowIndex].Value?.ToString() ?? string.Empty;

            if (string.IsNullOrEmpty(cltrNo))
            {
                MessageBox.Show("물건 고유번호(cltrNo)를 찾을 수 없습니다.");
                return;
            }

            tbcL.SelectedTab = tabWbr1;
            url = "/pa/paView.php?cltrNo=" + cltrNo;
            net.TankWebView(wbr1, url);
        }

        /// <summary>
        /// 등기PIN 저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSavePIN_Click(object sender, EventArgs e)
        {
            string sql, cltrNo, pinLand, pinBldg;

            cltrNo = lnkCltrNo.Text;

            pinLand = Regex.Replace(txtPINLand.Text, @"[^\d]", string.Empty);
            pinBldg = Regex.Replace(txtPINBldg.Text, @"[^\d]", string.Empty);

            if (pinLand != String.Empty)
            {
                if (Regex.IsMatch(pinLand, @"\d{14}") == false)
                {
                    MessageBox.Show("토지등기의 PIN이 14자리의 숫자가 아닙니다.");
                    return;
                }
            }
            if (pinBldg != String.Empty)
            {
                if (Regex.IsMatch(pinBldg, @"\d{14}") == false)
                {
                    MessageBox.Show("건물등기의 PIN이 14자리의 숫자가 아닙니다.");
                    return;
                }
            }

            db.Open();
            sql = $"update tb_dtl set pin_land='{pinLand}', pin_bldg='{pinBldg}' where cltr_no='{cltrNo}'";
            db.ExeQry(sql);

            if (chkRgstAdd.Checked)
            {                
                if (pinLand != String.Empty)
                {
                    //if (!db.ExistRow($"select idx from db_tank.tx_rgst_auto where dvsn >= 20 and tid='{cltrNo}' and pin='{pinLand}' and wdt > date_sub(curdate(),INTERVAL 10 day) and ul=0"))
                    if (!db.ExistRow($"select idx from db_tank.tx_rgst_auto where dvsn >= 20 and tid='{cltrNo}' and pin='{pinLand}'"))
                    {
                        db.ExeQry($"insert into db_tank.tx_rgst_auto set dvsn=20, tid='{cltrNo}', ls_type='토지', pin='{pinLand}', wdt=curdate(), wtm=curtime(), staff='{Properties.Settings.Default.USR_ID}'");
                    }
                }

                if (pinBldg != String.Empty)
                {
                    //if (!db.ExistRow($"select idx from db_tank.tx_rgst_auto where dvsn >= 20 and tid='{cltrNo}' and pin='{pinBldg}' and wdt > date_sub(curdate(),INTERVAL 10 day) and ul=0"))
                    if (!db.ExistRow($"select idx from db_tank.tx_rgst_auto where dvsn >= 20 and tid='{cltrNo}' and pin='{pinBldg}'"))
                    {
                        db.ExeQry($"insert into db_tank.tx_rgst_auto set dvsn=20, tid='{cltrNo}', ls_type='건물', pin='{pinBldg}', wdt=curdate(), wtm=curtime(), staff='{Properties.Settings.Default.USR_ID}'");
                    }
                }                
            }
            db.Close();

            MessageBox.Show("등기PIN 정보가 저장 되었습니다.");
        }

        /// <summary>
        /// 등기PIN 복사
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgAr_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (dgAr.Columns[e.ColumnIndex].Name != "dgAr_PIN") return;

            Clipboard.SetText(dgAr["dgAr_PIN", e.RowIndex].Value.ToString());
        }

        /// <summary>
        /// 파일 삭제
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnDelFiles_Click(object sender, EventArgs e)
        {
            string cltrNo, ctgrNm, sql, delFileNm, rgDvsn, rgstInfo, bldgInfo, rgst, bldgRgst;

            if (dgF.SelectedRows.Count == 0)
            {
                MessageBox.Show("삭제할 [등기/건축물대장] 파일을 선택 해 주세요");
                return;
            }

            if (MessageBox.Show("선택한 파일을 삭제 하시겠습니까?", "파일 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            cltrNo = lnkCltrNo.Text;
            ctgrNm = dgF.SelectedRows[0].Cells["dgF_Dvsn"].Value.ToString();
            delFileNm =dgF.SelectedRows[0].Cells["dgF_Path"].Value.ToString();

            sql = $"select rgst, bldg_rgst from tb_file where cltr_no='{cltrNo}' limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            rgstInfo = dr["rgst"].ToString();
            bldgInfo = dr["bldg_rgst"].ToString();
            dr.Close();
            db.Close();

            if (ctgrNm.Contains("등기"))
            {
                var jaNewRgst = new JArray();
                JArray jaRgst = JArray.Parse(rgstInfo);
                foreach (JObject item in jaRgst)
                {
                    if (item["fullNm"].ToString() == delFileNm)
                    {
                        //등기추출/핀 삭제
                        if (chkDelRgstAnaly.Checked)
                        {
                            if (ctgrNm.Contains("토지")) rgDvsn = "1";
                            else rgDvsn = "2,3";
                            db.Open();
                            sql = $"delete from tb_regist where cltr_no='{cltrNo}' and rg_dvsn in ({rgDvsn})";
                            //MessageBox.Show(sql);
                            db.ExeQry(sql);
                            if (ctgrNm.Contains("토지")) sql = $"update tb_dtl set pin_land='' where cltr_no='{cltrNo}'";
                            else sql = $"update tb_dtl set pin_bldg='' where cltr_no='{cltrNo}'";
                            //MessageBox.Show(sql);
                            db.ExeQry(sql);
                            db.Close();
                        }

                        //파일 삭제
                        //MessageBox.Show(delFileNm);
                        ftp1.FtpDelete(delFileNm);
                        continue;
                    }

                    var obj = new JObject();
                    obj.Add("fullNm", item["fullNm"]);
                    obj.Add("ctgr", item["ctgr"]);
                    obj.Add("rgstDt", item["rgstDt"]);
                    jaNewRgst.Add(obj);
                }
                rgst = (jaNewRgst.Count == 0) ? string.Empty : jaNewRgst.ToString();
                sql = $"update tb_file set rgst='{rgst}' where cltr_no='{cltrNo}'";
            }
            else if (ctgrNm.Contains("건축물대장"))
            {
                var jaNewBldg = new JArray();
                JArray jaBldg = JArray.Parse(bldgInfo);
                foreach (JObject item in jaBldg)
                {
                    if (item["fullNm"].ToString() == delFileNm)
                    {
                        //파일 삭제
                        //MessageBox.Show(delFileNm);
                        ftp1.FtpDelete(delFileNm);
                        continue;
                    }

                    var obj = new JObject();
                    obj.Add("fullNm", item["fullNm"]);
                    obj.Add("ctgr", item["ctgr"]);
                    obj.Add("rgstDt", item["rgstDt"]);
                    jaNewBldg.Add(obj);
                }
                bldgRgst = (jaNewBldg.Count == 0) ? string.Empty : jaNewBldg.ToString();
                sql = $"update tb_file set bldg_rgst='{bldgRgst}' where cltr_no='{cltrNo}'";
            }
            else
            {
                MessageBox.Show("선택한 파일이 [등기/건축물대장]이 아닙니다.");
                return;
            }
            
            //MessageBox.Show(sql);
            db.Open();
            db.ExeQry(sql);
            db.Close();

            MessageBox.Show("삭제 되었습니다.");
            dg_SelectionChanged(null, null);
        }
    }
}
