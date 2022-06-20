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
using mshtml;

namespace Solar.CA
{
    public partial class wfCaMgmt : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        AuctCd auctCd = new AuctCd();
        NetUtil net = new NetUtil();
        ApiUtil api = new ApiUtil();
        SpCdtnChk spCdtnChk = new SpCdtnChk();

        DataTable dtLawCd, dtDptCd; //법원, 계
        DataTable dtCatCdAll, dtCatCd, dtLandUseCd;  //물건 종별
        DataTable dtStateCd;    //진행 상태
        DataTable dtRgstCd, dtRgstTakeCd, dtRgstSect, dtRgstYn;     //등기목적(권리), 인수여부(수동처리시), 갑구(1)/을구(2) 구분, 등기유무
        DataTable dtFlrCd;      //층별 코드
        DataTable dtSidoCd;     //법정동 시/도 코드

        DataTable dtCarCoCd;    //차량-제조사
        DataTable dtCarMoCd;    //차량-모델
        DataTable dtCarFuelCd;  //차량-연료
        DataTable dtCarTransCd; //차량-변속기 형식

        DataTable dtEtcCd;      //기타 모든 코드
        DataTable dtDpslCd;     //매각 구분
        DataTable dtFrmlCd;     //형식적 경매
        DataTable dtLeasUseCd;  //임차인-용도 코드
        DataTable dtExpIncCd;   //제시외 매각포함 여부
        DataTable dtFileCd;     //파일 구분
        DataTable dtSpcCd;      //특수 조건
        DataTable dtImptCtgr;   //물건주요변동내역-구분
        DataTable dtImptSrc;    //물건주요변동내역-출처

        Dictionary<decimal, string> dictAdrsMtType = new Dictionary<decimal, string>(); //지번 유형
        Dictionary<decimal, string> dictAucdType = new Dictionary<decimal, string>();   //경매 형식
        Dictionary<decimal, string> dictDpstType = new Dictionary<decimal, string>();   //보증금율 구분
        Dictionary<decimal, string> dictBidTmType = new Dictionary<decimal, string>();  //입찰시간 구분
        Dictionary<decimal, string> dictPriReg = new Dictionary<decimal, string>();     //우선매수 신고
        Dictionary<decimal, string> dictAplsType = new Dictionary<decimal, string>();   //감정가격 기준
        Dictionary<decimal, string> dictSpRgst = new Dictionary<decimal, string>();     //토지현황-별도등기(집합건물)
        ContextMenuStrip dgMenu = new ContextMenuStrip();

        //정규식 기본형태
        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        private decimal[] multiBldgArr; //집합 건물 카테고리

        decimal totRowCnt = 0;
        string cdtn = "";

        ImageList imgList = new ImageList();

        //TANK-Web
        private CookieCollection Cookies;
        private CookieContainer cookieContainer;
        private string GoodCook = string.Empty;
        private string TankCook = string.Empty;
        //TANK-Web

        //목록내역 50건 이상 실시간 분석(토지/건물 현황)
        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 1500, webCnt = 0;

        string STA1 = "0", STA2 = "0", newSTA1 = "0", newSTA2 = "0";  //현재 DB상 및 수정 후 물건상태 코드 비교 값(문자발송용)

        string myWeb = Properties.Settings.Default.myWeb;
        FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "CA/", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);

        //토지 패턴
        string landPtrn = "대|전|답|과수원|목장용지|임야|광천지|염전|대지|공장용지|학교용지|주차장|주유소용지|창고용지|도로|철도용지|제방|하천|구거|유지|양어장|수도용지|공원|체육용지|유원지|종교용지|사적지|묘지|잡종지";

        //핫키등록
        [DllImport("user32.dll")]
        private static extern int RegisterHotKey(int hwnd, int id, int fsModifiers, int vk);

        //핫키삭제
        [DllImport("user32.dll")]
        private static extern int UnregisterHotKey(int hwnd, int id);

        private void wfCaMgmt_Load(object sender, EventArgs e)
        {
            applyHotKey("ON");
            //RegisterHotKey((int)this.Handle, 0, 0x0, (int)Keys.Pause);
        }

        private void wfCaMgmt_FormClosing(object sender, FormClosingEventArgs e)
        {
            applyHotKey("OFF");
            //UnregisterHotKey((int)this.Handle, 0);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == (int)0x312)
            {
                /*
                if (m.WParam == (IntPtr)0x0)    //핫키 ON/OFF
                {
                    if (HotKeyState.Text == "OFF") applyHotKey("ON");
                    else applyHotKey("OFF");
                }
                */
                if (m.WParam == (IntPtr)0x1) { lnkTK_LinkClicked(lnkTK_Giil, null); }     //기일내역(내부)
                if (m.WParam == (IntPtr)0x2) { lnkTK_LinkClicked(lnkTK_Sagun, null); }    //사건내역(내부)

                if (m.WParam == (IntPtr)0x0B) { lnkCA_LinkClicked(lnkCA_Giil, null); }   //기일내역(법원)
                if (m.WParam == (IntPtr)0x0C) { lnkCA_LinkClicked(lnkCA_Sagun, null); }   //사건내역(법원)

                if (m.WParam == (IntPtr)0x14) { ImptCopyNPaste(); }     //주요변동내역 붙여넣기

                if (m.WParam == (IntPtr)0x15) lnkGotoBtm.Select();   //맨위로
                if (m.WParam == (IntPtr)0x16) lnkGotoTop.Select();   //맨아래로
            }
        }

        private void applyHotKey(string state)
        {
            if (state == "ON")
            {
                RegisterHotKey((int)this.Handle, 1, 0x0, (int)Keys.F1);     //기일내역(내부)
                RegisterHotKey((int)this.Handle, 2, 0x0, (int)Keys.F2);     //사건내역(내부)

                RegisterHotKey((int)this.Handle, 11, 0x0, (int)Keys.F9);     //기일내역(법원)
                RegisterHotKey((int)this.Handle, 12, 0x0, (int)Keys.F10);    //사건내역(법원)

                RegisterHotKey((int)this.Handle, 20, 0x0, (int)Keys.F11);   //주요변동내역에 붙여넣기

                RegisterHotKey((int)this.Handle, 21, 0x0, (int)Keys.PageUp);     //맨위로-HEX(0x15)
                RegisterHotKey((int)this.Handle, 22, 0x0, (int)Keys.PageDown);   //맨아래로-HEX(0x16)

                //RegisterHotKey((int)this.Handle, 3, 0x2, (int)Keys.D1);         //Ctrl+1
                /*
                HotKeyState.Text = "ON";
                HotKeyState.BackColor = Color.Lime;
                */
            }
            else
            {
                UnregisterHotKey((int)this.Handle, 1);
                UnregisterHotKey((int)this.Handle, 2);

                UnregisterHotKey((int)this.Handle, 11);
                UnregisterHotKey((int)this.Handle, 12);

                UnregisterHotKey((int)this.Handle, 20);

                UnregisterHotKey((int)this.Handle, 21);
                UnregisterHotKey((int)this.Handle, 22);
                /*
                HotKeyState.Text = "OFF";
                HotKeyState.BackColor = Color.LightGray;
                */
            }
        }

        public wfCaMgmt()
        {
            InitializeComponent();

            init();
            //tankCert();
        }

        private void tankCert()
        {
            if (TankCook != string.Empty) return;

            wbr1.Navigate("https://www.tankauction.com/Mgmt");
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

        private void init()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            
            lnkTid.Text = string.Empty;

            ui.DgSetRead(dg, 0);
            ui.DgSetRead(dgSeq);
            ui.DgSetRead(dgRC, 0);
            ui.DgSetRead(dgMR, 0);
            ui.DgSetRead(dgF, 0);
            ui.DgSetRead(dgU, 0);
            ui.DgSetRead(dgNt);
            ui.DgSetRead(dgCp, 0);
            ui.DgSetRead(dgPn);
            ui.DgSetRead(dgPr);
            ui.DgSetRead(dgImpt, 0);
            ui.DgSetRead(dgRCA, 0);
            ui.DgSetRead(dgRCB, 0);

            ui.DgSetEdit(dgH);
            ui.DgSetEdit(dgI);
            ui.DgSetEdit(dgL);
            ui.DgSetEdit(dgB);
            ui.DgSetEdit(dgE);
            ui.DgSetEdit(dgM);
            ui.DgSetEdit(dgC);
            ui.DgSetEdit(dgT);
            ui.DgSetEdit(dgRB);
            ui.DgSetEdit(dgRL);
            dgL.MultiSelect = true;
            dgB.MultiSelect = true;
            dgE.MultiSelect = true;
            dgRB.MultiSelect = true;
            dgRL.MultiSelect = true;
            dgCp.MultiSelect = true;
            dgRCB.MultiSelect = true;

            ui.SetPagn(panPagn);
            imgList.ImageSize = new Size(10, 20);
            lvSpc.SmallImageList = imgList;

            //집합건물 카테고리
            multiBldgArr = auctCd.multiBldgArr;

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

            cbxCrtSpt.DataSource = dtLawCd.Copy();
            cbxCrtSpt.DisplayMember = "csNm";
            cbxCrtSpt.ValueMember = "csCd";
            
            int y = (int)DateTime.Now.Year;
            for (int i = y; i >= 2010; i--)
            {
                cbxSn1.Items.Add(i);
            }

            //법정동 시/도 코드
            dtSidoCd = new DataTable();
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

            //등기 목적(권리)
            dtRgstCd = db.ExeDt("select rg_cd, rg_nm from ta_cd_rgst order by rg_cd");
            row = dtRgstCd.NewRow();
            row["rg_cd"] = 0;
            row["rg_nm"] = "-선택-";
            dtRgstCd.Rows.InsertAt(row, 0);
            dgRL_RgCd.DataSource = dtRgstCd;
            dgRL_RgCd.DisplayMember = "rg_nm";
            dgRL_RgCd.ValueMember = "rg_cd";
            dgRL_RgCd.DefaultCellStyle.NullValue = "-선택-";

            dgRB_RgCd.DataSource = dtRgstCd.Copy();
            dgRB_RgCd.DisplayMember = "rg_nm";
            dgRB_RgCd.ValueMember = "rg_cd";
            dgRB_RgCd.DefaultCellStyle.NullValue = "-선택-";

            dgRCA_RgCd.DataSource = dtRgstCd.Copy();
            dgRCA_RgCd.DisplayMember = "rg_nm";
            dgRCA_RgCd.ValueMember = "rg_cd";
            dgRCA_RgCd.DefaultCellStyle.NullValue = "-선택-";

            dgRCB_RgCd.DataSource = dtRgstCd.Copy();
            dgRCB_RgCd.DisplayMember = "rg_nm";
            dgRCB_RgCd.ValueMember = "rg_cd";
            dgRCB_RgCd.DefaultCellStyle.NullValue = "-선택-";

            //등기 인수여부(수동처리시)
            dtRgstTakeCd = dtEtcCd.Select("dvsn=17").CopyToDataTable();
            row = dtRgstTakeCd.NewRow();
            row["cd"] = 0;
            row["nm"] = "-선택-";
            dtRgstTakeCd.Rows.InsertAt(row, 0);
            dgRL_Take.DataSource = dtRgstTakeCd;
            dgRL_Take.DisplayMember = "nm";
            dgRL_Take.ValueMember = "cd";
            dgRL_Take.DefaultCellStyle.NullValue = "-선택-";

            dgRB_Take.DataSource = dtRgstTakeCd.Copy();
            dgRB_Take.DisplayMember = "nm";
            dgRB_Take.ValueMember = "cd";
            dgRB_Take.DefaultCellStyle.NullValue = "-선택-";

            dgRCA_Take.DataSource = dtRgstTakeCd.Copy();
            dgRCA_Take.DisplayMember = "nm";
            dgRCA_Take.ValueMember = "cd";
            dgRCA_Take.DefaultCellStyle.NullValue = "-선택-";

            dgRCB_Take.DataSource = dtRgstTakeCd.Copy();
            dgRCB_Take.DisplayMember = "nm";
            dgRCB_Take.ValueMember = "cd";
            dgRCB_Take.DefaultCellStyle.NullValue = "-선택-";

            //등기 갑구/을구 구분
            dtRgstSect = new DataTable();
            dtRgstSect.Columns.Add("cd");
            dtRgstSect.Columns.Add("nm");
            dtRgstSect.Rows.Add("0", "-선택-");
            dtRgstSect.Rows.Add("1", "갑구");
            dtRgstSect.Rows.Add("2", "을구");

            dgRL_Sect.DataSource = dtRgstSect;
            dgRL_Sect.DisplayMember = "nm";
            dgRL_Sect.ValueMember = "cd";
            dgRL_Sect.DefaultCellStyle.NullValue = "-선택-";

            dgRB_Sect.DataSource = dtRgstSect.Copy();
            dgRB_Sect.DisplayMember = "nm";
            dgRB_Sect.ValueMember = "cd";
            dgRB_Sect.DefaultCellStyle.NullValue = "-선택-";

            dgRCA_Sect.DataSource = dtRgstSect.Copy();
            dgRCA_Sect.DisplayMember = "nm";
            dgRCA_Sect.ValueMember = "cd";
            dgRCA_Sect.DefaultCellStyle.NullValue = "-선택-";

            dgRCB_Sect.DataSource = dtRgstSect.Copy();
            dgRCB_Sect.DisplayMember = "nm";
            dgRCB_Sect.ValueMember = "cd";
            dgRCB_Sect.DefaultCellStyle.NullValue = "-선택-";

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
            row = dtDpslCd.NewRow();
            row["cd"] = 0;
            row["nm"] = "-선택-";
            dtDpslCd.Rows.InsertAt(row, 0);
            cbxDpslDvsn.DataSource = dtDpslCd;
            cbxDpslDvsn.DisplayMember = "nm";
            cbxDpslDvsn.ValueMember = "cd";

            cbxSrchDpslDvsn.DataSource = dtDpslCd.Copy();
            cbxSrchDpslDvsn.DisplayMember = "nm";
            cbxSrchDpslDvsn.ValueMember = "cd";

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
            cbxCat3.DataSource = dtCatCd;
            cbxCat3.DisplayMember = "cat3_nm";
            cbxCat3.ValueMember = "cat3_cd";

            cbxCat3Rec.DataSource = dtCatCd.Copy();
            cbxCat3Rec.DisplayMember = "cat3_nm";
            cbxCat3Rec.ValueMember = "cat3_cd";

            cbxSrchCat.DataSource = dtCatCd.Copy();
            cbxSrchCat.DisplayMember = "cat3_nm";
            cbxSrchCat.ValueMember = "cat3_cd";
            //특정 항목 배경색 변경
            /*
            cbxSrchCat.DrawMode = DrawMode.OwnerDrawFixed;
            cbxSrchCat.DrawItem += (s, e) =>
            {
                e.DrawBackground();
                string text = ((DataRowView)((ComboBox)s).Items[e.Index])["cat3_nm"].ToString();                                
                if (e.Index == 3)
                {
                    var g = e.Graphics;
                    var rect = e.Bounds;
                    g.FillRectangle(Brushes.Silver, rect.X, rect.Y, rect.Width, rect.Height);
                }
                e.Graphics.DrawString(text, ((Control)s).Font, Brushes.Black, e.Bounds.X, e.Bounds.Y);
            };
            */
            x = from DataRow r in dtCatCd.Rows
                    where r["cat2_cd"].ToString() == "1010"
                    select r;
            dtLandUseCd = x.CopyToDataTable();
            row = dtLandUseCd.NewRow();
            row["cat3_cd"] = 0;
            row["cat3_nm"] = "-선택-";
            dtLandUseCd.Rows.InsertAt(row, 0);
            dgL_Cat.DataSource = dtLandUseCd;
            dgL_Cat.DisplayMember = "cat3_nm";
            dgL_Cat.ValueMember = "cat3_cd";
            dgL_Cat.DefaultCellStyle.NullValue = "-선택-";

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

            cbxState.DataSource = dtStateCd.Copy();
            cbxState.DisplayMember = "sta2_nm";
            cbxState.ValueMember = "sta2_cd";

            DataTable dtHisState = dtStateCd.Copy();
            //dgH_State.DataSource = dtStateCd.Copy();
            row = dtHisState.Select("sta2_cd=1010").FirstOrDefault();
            row.Delete();
            row = dtHisState.Select("sta2_cd=1011").FirstOrDefault();
            row.Delete();
            row = dtHisState.Select("sta2_cd=1110").FirstOrDefault();
            row["sta2_nm"] = "예정";            
            dgH_State.DataSource = dtHisState;
            dgH_State.DisplayMember = "sta2_nm";
            dgH_State.ValueMember = "sta2_cd";
            dgH_State.DefaultCellStyle.NullValue = "-선택-";            
            //dgH_State.DefaultCellStyle.NullValue = dtStateCd.Rows[0]["sta2_cd"];

            //층별 코드
            dtFlrCd = db.ExeDt("select flr_cd, flr_nm from ta_cd_flr order by seq, flr_cd");
            row = dtFlrCd.NewRow();
            row["flr_cd"] = 0;
            row["flr_nm"] = "-선택-";
            dtFlrCd.Rows.InsertAt(row, 0);
            dgB_Flr.DataSource = dtFlrCd;
            dgB_Flr.DisplayMember = "flr_nm";
            dgB_Flr.ValueMember = "flr_cd";
            dgB_Flr.DefaultCellStyle.NullValue = "-선택-";

            dgE_Flr.DataSource = dtFlrCd.Copy();
            dgE_Flr.DisplayMember = "flr_nm";
            dgE_Flr.ValueMember = "flr_cd";
            dgE_Flr.DefaultCellStyle.NullValue = "-선택-";

            //차량-제조사 / 모델
            dtCarMoCd = db.ExeDt("select * from ta_cd_carmo order by mo_nm");
            //row = dtCarMoCd.NewRow();
            //row["co_cd"] = 0;
            //row["mo_cd"] = 0;
            //row["mo_nm"] = "-선택-";
            //dtCarMoCd.Rows.InsertAt(row, 0);
            //cbxCarMoCd.DataSource = dtCarMoCd;
            //cbxCarMoCd.DisplayMember = "mo_nm";
            //cbxCarMoCd.ValueMember = "mo_cd";

            dtCarCoCd = db.ExeDt("select * from ta_cd_carco");
            row = dtCarCoCd.NewRow();
            row["co_cd"] = 0;
            row["co_nm"] = "-선택-";
            dtCarCoCd.Rows.InsertAt(row, 0);
            cbxCarCoCd.DataSource = dtCarCoCd;
            cbxCarCoCd.DisplayMember = "co_nm";
            cbxCarCoCd.ValueMember = "co_cd";
            cbxCarCoCd.SelectedIndexChanged += (s, e) =>
            {
                //if (cbxCarCoCd.SelectedIndex > 0)
                //{
                    string coCd = cbxCarCoCd.SelectedValue.ToString();
                    DataView dvModel = dtCarMoCd.DefaultView;
                    dvModel.RowFilter = string.Format("co_cd='{0}'", coCd);
                    DataTable dtModel = dvModel.ToTable();
                    DataRow rowMo = dtModel.NewRow();
                    rowMo["mo_nm"] = "-선택-";
                    rowMo["mo_cd"] = 0;
                    dtModel.Rows.InsertAt(rowMo, 0);
                    cbxCarMoCd.DataSource = dtModel;
                    cbxCarMoCd.DisplayMember = "mo_nm";
                    cbxCarMoCd.ValueMember = "mo_cd";
                //}
            };

            //파일 구분
            //dtFileCd = db.ExeDt("select cd, nm from ta_cd_file order by cd");
            dtFileCd = db.ExeDt("select * from ta_cd_file order by cd");
            row = dtFileCd.NewRow();
            row["cd"] = 0;
            row["nm"] = "-선택-";
            dtFileCd.Rows.InsertAt(row, 0);
            cbxFileCtgr.DataSource = dtFileCd;
            cbxFileCtgr.DisplayMember = "nm";
            cbxFileCtgr.ValueMember = "cd";

            //보증금율 구분
            dictDpstType.Add(0, "선택");
            dictDpstType.Add(1, "최저");
            dictDpstType.Add(2, "재입");
            dictDpstType.Add(3, "최매");
            cbxDpstType.DataSource = new BindingSource(dictDpstType, null);
            cbxDpstType.DisplayMember = "Value";
            cbxDpstType.ValueMember = "Key";

            //경매형식
            dictAucdType.Add(0, "선택(경매형식)");
            dictAucdType.Add(1, "임의경매");
            dictAucdType.Add(2, "강제경매");
            cbxAuctType.DataSource = new BindingSource(dictAucdType, null);
            cbxAuctType.DisplayMember = "Value";
            cbxAuctType.ValueMember = "Key";

            //형식적 경매 구분
            dtFrmlCd = dtEtcCd.Select("dvsn=12").CopyToDataTable();
            row = dtFrmlCd.NewRow();
            row["cd"] = 0;
            row["nm"] = "-선택(형식적경매)-";
            dtFrmlCd.Rows.InsertAt(row, 0);
            cbxFrmlType.DataSource = dtFrmlCd;
            cbxFrmlType.DisplayMember = "nm";
            cbxFrmlType.ValueMember = "cd";
            
            //우선매수 신고
            dictPriReg.Add(0, "선택");
            dictPriReg.Add(1, "공유자");
            dictPriReg.Add(2, "임차인");
            dgH_PriReg.DataSource = new BindingSource(dictPriReg, null);
            dgH_PriReg.DisplayMember = "Value";
            dgH_PriReg.ValueMember = "Key";
            dgH_PriReg.DefaultCellStyle.NullValue = dictPriReg[0];

            //감정가격 기준
            dictAplsType.Add(0, "-선택-");
            dictAplsType.Add(1, "일괄");
            dictAplsType.Add(2, "비준");
            cbxApslType.DataSource= new BindingSource(dictAplsType, null);
            cbxApslType.DisplayMember = "Value";
            cbxApslType.ValueMember = "Key";

            //토지현황-별도등기(집합건물)
            dictSpRgst.Add(0, "-별도등기 선택-");
            dictSpRgst.Add(1, "토지별도등기있음");
            dictSpRgst.Add(2, "대지권없음");
            dictSpRgst.Add(3, "미등기감정가격포함");
            dictSpRgst.Add(4, "대지권미등기");
            dictSpRgst.Add(5, "토지별도등기인수조건");
            dictSpRgst.Add(6, "미등기가격포함+토지별도등기");
            cbxSpRgst.DataSource = new BindingSource(dictSpRgst, null);
            cbxSpRgst.DisplayMember = "Value";
            cbxSpRgst.ValueMember = "Key";
            cbxSpRgst.SelectedValue = 0;

            //지번 유형
            dictAdrsMtType.Add(0, "선택");
            dictAdrsMtType.Add(1, "일반");
            dictAdrsMtType.Add(2, "산");
            cbxAdrsMt.DataSource = new BindingSource(dictAdrsMtType, null);
            cbxAdrsMt.DisplayMember = "Value";
            cbxAdrsMt.ValueMember = "Key";
            cbxAdrsMt.SelectedValue = 0;

            //제시외 매각포함여부
            dtExpIncCd = dtEtcCd.Select("dvsn=13").CopyToDataTable();
            row = dtExpIncCd.NewRow();
            row["cd"] = 0;
            row["nm_as"] = "-선택-";
            dtExpIncCd.Rows.InsertAt(row, 0);
            dgE_Inc.DataSource = new BindingSource(dtExpIncCd, null);
            dgE_Inc.DisplayMember = "nm_as";
            dgE_Inc.ValueMember = "cd";
            dgE_Inc.DefaultCellStyle.NullValue = dtExpIncCd.Rows[0]["nm_as"];

            //임차인-용도코드
            dtLeasUseCd = dtEtcCd.Select("dvsn=16").CopyToDataTable();
            row = dtLeasUseCd.NewRow();
            row["cd"] = 0;
            row["nm_as"] = "-선택-";
            dtLeasUseCd.Rows.InsertAt(row, 0);
            dgT_UseCd.DataSource = new BindingSource(dtLeasUseCd, null);
            dgT_UseCd.DisplayMember = "nm_as";
            dgT_UseCd.ValueMember = "cd";
            dgT_UseCd.DefaultCellStyle.NullValue = dtLeasUseCd.Rows[0]["nm_as"];

            //차량-변속기 형식
            dtCarTransCd = dtEtcCd.Select("dvsn=14").CopyToDataTable();
            row = dtCarTransCd.NewRow();
            row["cd"] = 0;
            row["nm"] = "-선택-";
            dtCarTransCd.Rows.InsertAt(row, 0);
            dgC_Trans.DataSource = new BindingSource(dtCarTransCd, null);
            dgC_Trans.DisplayMember = "nm";
            dgC_Trans.ValueMember = "cd";
            dgC_Trans.DefaultCellStyle.NullValue = dtCarTransCd.Rows[0]["nm"];

            //차량-사용 연료
            dtCarFuelCd = dtEtcCd.Select("dvsn=15").CopyToDataTable();
            row = dtCarFuelCd.NewRow();
            row["cd"] = 0;
            row["nm"] = "-선택-";
            dtCarFuelCd.Rows.InsertAt(row, 0);
            dgC_Fuel.DataSource = new BindingSource(dtCarFuelCd, null);
            dgC_Fuel.DisplayMember = "nm";
            dgC_Fuel.ValueMember = "cd";
            dgC_Fuel.DefaultCellStyle.NullValue = dtCarFuelCd.Rows[0]["nm"];

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

            //물건주요변동내역 구분 및 출처
            dtImptCtgr = dtEtcCd.Select("dvsn=20").CopyToDataTable();
            row = dtImptCtgr.NewRow();
            row["cd"] = 0;
            row["nm"] = "-선택-";
            dtImptCtgr.Rows.InsertAt(row, 0);
            cbxImptCtgr.DataSource = dtImptCtgr;
            cbxImptCtgr.DisplayMember = "nm";
            cbxImptCtgr.ValueMember = "cd";

            dtImptSrc = dtEtcCd.Select("dvsn=21").CopyToDataTable();
            row = dtImptSrc.NewRow();
            row["cd"] = 0;
            row["nm"] = "-선택-";
            dtImptSrc.Rows.InsertAt(row, 0);
            cbxImptSrc.DataSource = dtImptSrc;
            cbxImptSrc.DisplayMember = "nm";
            cbxImptSrc.ValueMember = "cd";

            //TextBox 숫자만 허용
            //txtSrchTid.KeyPress += TxtNum_KeyPress;

            //검색-Enter 키
            txtSrchTid.KeyDown += TxtEnter_KeyDown;
            txtSrchSn.KeyDown += TxtEnter_KeyDown;

            //DG ContextMenuStrip
            dgMenu.BackColor = Color.Beige;            
            dgMenu.Items.Add("+ 행추가");
            dgMenu.Items.Add("+ 행추가(5)");
            dgMenu.Items.Add("- 행삭제");
            dgMenu.Items.Add("일괄삭제");
            dgMenu.Items.Add("-");
            dgMenu.Items.Add("채권자");
            dgMenu.Items.Add("채무자");
            dgMenu.Items.Add("소유자");
            dgMenu.Items.Add("채무자+소유자");
            dgMenu.Items.Add("-");
            dgMenu.Items.Add("임차인추가");
            dgMenu.Items.Add("현황복사");
            dgMenu.ItemClicked += DgMenu_ItemClicked;
            
            dgH.MouseUp += Dg_MouseUp;
            dgI.MouseUp += Dg_MouseUp;
            dgL.MouseUp += Dg_MouseUp;
            dgB.MouseUp += Dg_MouseUp;
            dgE.MouseUp += Dg_MouseUp;
            dgM.MouseUp += Dg_MouseUp;
            dgC.MouseUp += Dg_MouseUp;
            dgT.MouseUp += Dg_MouseUp;
            dgRB.MouseUp += Dg_MouseUp;
            dgRL.MouseUp += Dg_MouseUp;
            
            //DG Row 삭제시
            dgH.UserDeletingRow += Dg_UserDeletingRow;
            dgI.UserDeletingRow += Dg_UserDeletingRow;
            dgL.UserDeletingRow += Dg_UserDeletingRow;
            dgB.UserDeletingRow += Dg_UserDeletingRow;
            dgE.UserDeletingRow += Dg_UserDeletingRow;
            dgM.UserDeletingRow += Dg_UserDeletingRow;
            dgC.UserDeletingRow += Dg_UserDeletingRow;
            dgT.UserDeletingRow += Dg_UserDeletingRow;
            dgRB.UserDeletingRow += Dg_UserDeletingRow;
            dgRL.UserDeletingRow += Dg_UserDeletingRow;

            //DG (면적*단가) 계산 -> 토지,건물,제시외,기계기구 현황
            dgL.EditingControlShowing += Dg_EditingControlShowing;
            dgL.CellEndEdit += DgAmtCal_CellEndEdit;

            dgB.EditingControlShowing += Dg_EditingControlShowing;
            dgB.CellEndEdit += DgAmtCal_CellEndEdit;

            dgE.EditingControlShowing += Dg_EditingControlShowing;
            dgE.CellEndEdit += DgAmtCal_CellEndEdit;
            dgE.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgE.IsCurrentCellDirty)
                {
                    dgE.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };

            dgM.EditingControlShowing += Dg_EditingControlShowing;
            dgM.CellEndEdit += DgAmtCal_CellEndEdit;

            //임차인 현황-용도코드 변경시 사업자 체크
            dgT.EditingControlShowing += DgT_EditingControlShowing;

            //등기-권리코드 변경시 권리명칭 연동
            dgRL.EditingControlShowing += DgRg_EditingControlShowing;
            dgRB.EditingControlShowing += DgRg_EditingControlShowing;

            //입찰일정 금액입력 관련
            dgH.EditingControlShowing += DgH_EditingControlShowing;

            //ComboBox 마우스휠 무력화            
            List<ComboBox> lstCbx = new List<ComboBox>();
            ComboBox[] cbxArr = new ComboBox[] { cbxSrchCs, cbxSrchDpt, cbxSrchCat, cbxSrchSta1, cbxSrchSta2, cbxPhrase0, cbxPhrase1, cbxPhrase2, cbxPhrase3, cbxPhrase4, cbxPhrase5, cbxPhrase6,
                cbxCrtSpt, cbxDpt, cbxSn1, cbxCat3, cbxCat3Rec, cbxState, cbxBidCnt, cbxDpstRate, cbxDpstType, cbxDpslDvsn, cbxSpRgst, cbxCarCoCd, cbxCarMoCd, cbxApslType, cbxAuctType, cbxFrmlType, cbxRgstYn };
            lstCbx.AddRange(cbxArr);
            CbxMouseWheelDisable(lstCbx);
            /*
            foreach (Control ctrl in tpnlBasic.Controls)
            {
                if (ctrl.GetType() == typeof(ComboBox))
                {
                    ComboBox cbx = (ComboBox)ctrl;
                    cbx.MouseWheel += (s, e) => { ((HandledMouseEventArgs)e).Handled = true; };
                }
            }
            */

            //DataGridViewComboBoxColumn 마우스휠 무력화
            List<DataGridView> lstDgv = new List<DataGridView>();
            lstDgv.Add(dgH);
            lstDgv.Add(dgL);
            lstDgv.Add(dgB);
            lstDgv.Add(dgE);
            lstDgv.Add(dgC);
            lstDgv.Add(dgT);
            lstDgv.Add(dgRL);
            lstDgv.Add(dgRB);
            DgvCbxMouseWheelDisable(lstDgv);

            //인터넷 등기열람(해당물건)
            dgI.CellClick += DgI_CellClick;
            //wbr2.Navigate("http://www.iros.go.kr/");

            //제시외 매각포함여부
            rdoPres0.CheckedChanged += RdoPres_CheckedChanged;
            rdoPres1.CheckedChanged += RdoPres_CheckedChanged;
            rdoPres2.CheckedChanged += RdoPres_CheckedChanged;

            //상용어구
            cbxPhrase0.SelectedIndexChanged += CbxPhrase_SelectedIndexChanged;
            cbxPhrase1.SelectedIndexChanged += CbxPhrase_SelectedIndexChanged;
            cbxPhrase2.SelectedIndexChanged += CbxPhrase_SelectedIndexChanged;
            cbxPhrase3.SelectedIndexChanged += CbxPhrase_SelectedIndexChanged;
            cbxPhrase4.SelectedIndexChanged += CbxPhrase_SelectedIndexChanged;
            cbxPhrase5.SelectedIndexChanged += CbxPhrase_SelectedIndexChanged;
            cbxPhrase6.SelectedIndexChanged += CbxPhrase_SelectedIndexChanged;

            //맨위,아래로 스크롤
            lnkGotoTop.Click += (s, e) => { lnkGotoBtm.Select(); };
            lnkGotoBtm.Click += (s, e) => { lnkGotoTop.Select(); };

            //TextBox Drag & Drop
            TextBox[] dndTxtArr = new TextBox[] { txtLoca, txtLandShp, txtDiff, txtLeasNote, txtEtcNote, txtAdjRoad, txtFaci, txtPdNote, txtAttnNote1, txtAttnNote2, txtRgstNote, txtAnalyNote };
            foreach (TextBox tbx in dndTxtArr)
            {
                tbx.AllowDrop = true;
                tbx.DragEnter += txtBox_DragEnter;
                tbx.DragDrop += txtBox_DragDrop;
            }

            //물건복사-전체선택/해제 체크박스
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

            //물건복사-항목체크 색상반전
            foreach (Control ctrl in gbxCp1.Controls)
            {
                CheckBox chk = ctrl as CheckBox;
                chk.CheckedChanged += ChkCp_CheckedChanged;
            }
            foreach (Control ctrl in gbxCp2.Controls)
            {
                CheckBox chk = ctrl as CheckBox;
                chk.CheckedChanged += ChkCp_CheckedChanged;
            }
            foreach (Control ctrl in gbxCp3.Controls)
            {
                CheckBox chk = ctrl as CheckBox;
                chk.CheckedChanged += ChkCp_CheckedChanged;
            }

            //중복병합/관련사건 보임/숨김
            dgMR.CellContentClick += DgMR_CellContentClick;
            dgRC.CellContentClick += DgRC_CellContentClick;

            //작업 유형별 검색조건 적용
            btnWorkType1.Click += BtnWorkType_Click;
            btnWorkType2.Click += BtnWorkType_Click;
            btnWorkType3.Click += BtnWorkType_Click;

            //각 부분별/전체 DB 저장
            btnSaveAll.Click += SaveData;
            btnSaveAll2.Click += SaveData;
            btnSaveBase.Click += SaveData;
            btnSaveLs.Click += SaveData;
            btnSaveArea.Click += SaveData;
            btnSaveCarShip.Click += SaveData;
            btnSaveLeas.Click += SaveData;
            btnSaveLandRgst.Click += SaveData;
            btnSaveBldgRgst.Click += SaveData;
            btnSaveHist.Click += SaveData;

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

            dg.CellClick += Dg_CellClick;

            //속도가 느려 사용 안함
            //dg.CellMouseMove += Dg_CellMouseMove;
            //dg.CellMouseLeave += Dg_CellMouseLeave;            
        }

        private void Dg_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            dg_SelectionChanged(null, null);
        }

        /// <summary>
        /// 관련사건 보임/숨김
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DgRC_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            string sql, idx, hide;

            if (dgRC.Columns[e.ColumnIndex].Name != "dgRC_Hide") return;

            idx = dgRC["dgRC_Idx", e.RowIndex].Value.ToString();
            hide = (dgRC["dgRC_Hide", e.RowIndex].Value.ToString() == "보임") ? "0" : "1";
            sql = "update ta_rcase set hide='" + hide + "' where idx=" + idx;
            db.Open();
            db.ExeQry(sql);
            db.Close();

            dg_SelectionChanged(null, null);
        }

        /// <summary>
        /// 중복/병합사건 보임/숨김
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DgMR_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            string sql, idx, hide;

            if (dgMR.Columns[e.ColumnIndex].Name != "dgMR_Hide") return;

            idx = dgMR["dgMR_Idx", e.RowIndex].Value.ToString();
            hide = (dgMR["dgMR_Hide", e.RowIndex].Value.ToString() == "보임") ? "0" : "1";
            sql = "update ta_merg set hide='" + hide + "' where idx=" + idx;
            db.Open();
            db.ExeQry(sql);
            db.Close();

            dg_SelectionChanged(null, null);
        }

        /// <summary>
        /// Mouse Over 색상 반전
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Dg_CellMouseMove(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex > -1)
            {
                //dg.Rows[e.RowIndex].DefaultCellStyle.BackColor = SystemColors.GradientActiveCaption;
                dg.Rows[e.RowIndex].Cells["dg_Tid"].Style.BackColor = SystemColors.GradientActiveCaption;
            }
        }

        /// <summary>
        /// Mouse Out 기본 색상
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Dg_CellMouseLeave(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex > -1)
            {
                //dg.Rows[e.RowIndex].DefaultCellStyle.BackColor = dg.DefaultCellStyle.BackColor;
                dg.Rows[e.RowIndex].Cells["dg_Tid"].Style.BackColor = dg.DefaultCellStyle.BackColor;
            }
        }

        /// <summary>
        /// 물건복사-체크항목 색상반전
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChkCp_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox chk = sender as CheckBox;
            chk.BackColor = (chk.Checked) ? Color.Orange : Color.Transparent;
        }

        /// <summary>
        /// 물건복사-전체선택/해제
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
            gbxCp1.Focus();   //focus를 바꿔주지 않으면 current row 에는 체크유무가 표시 안됨!!!
        }

        /// <summary>
        /// TextBox Drag & Drop Step-1
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtBox_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.Text) && (e.AllowedEffect & DragDropEffects.Copy) != 0)
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        /// <summary>
        /// TextBox Drag & Drop Step-2
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtBox_DragDrop(object sender, DragEventArgs e)
        {
            TextBox txtBox = (TextBox)sender;
            txtBox.Text += "\r\n" + (string)e.Data.GetData(DataFormats.Text);
            txtBox.Text = txtBox.Text.Trim();
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
            if (cbx == cbxPhrase0) tbx = txtLoca;
            else if (cbx == cbxPhrase1) tbx = txtEtcNote;
            else if (cbx == cbxPhrase2) tbx = txtAttnNote1;
            else if (cbx == cbxPhrase3) tbx = txtRgstNote;
            else if (cbx == cbxPhrase4) tbx = txtAttnNote2;
            else if (cbx == cbxPhrase5) tbx = txtAnalyNote;
            else tbx = txtLeasNote;

            tbx.Text += "\r\n" + phrase;
            tbx.Text = tbx.Text.Trim();
        }

        /// <summary>
        /// 입찰일정-금액입력시 콤마 처리
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DgH_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            string val, kor;

            if (dgH.CurrentCell.ColumnIndex != 4) return;
            //if (dgH.Columns[dgH.CurrentCell.ColumnIndex] != dgH_Amt) return;
            TextBox tbx = e.Control as TextBox;
            if (tbx != null)
            {
                tbx.KeyPress += (s, ev) =>
                {
                    if (dgH.CurrentCell.ColumnIndex != 4) return;
                    if (!(char.IsControl(ev.KeyChar) || char.IsDigit(ev.KeyChar) || ev.KeyChar == ','))
                    {
                        ev.Handled = true;
                    }
                    /*val = tbx.Text.Replace(",", "");
                    if (!string.IsNullOrEmpty(val))
                    {
                        tbx.Text = string.Format("{0:#,##0}", Convert.ToDouble(val));
                        tbx.SelectionStart = tbx.TextLength;
                        tbx.SelectionLength = 0;
                    }*/
                };
                tbx.TextChanged += (s, ev) => 
                {
                    if (dgH.CurrentCell.ColumnIndex != 4) return;
                    val = tbx.Text.Replace(",", "");
                    if (!string.IsNullOrEmpty(val))
                    {
                        tbx.Text = string.Format("{0:#,##0}", Convert.ToDouble(val));
                        tbx.SelectionStart = tbx.TextLength;
                        tbx.SelectionLength = 0;
                        kor = NumToKor(Convert.ToInt64(val));
                        kor = Regex.Replace(kor, @"([십백천만억])", "$1 ");
                        lblHangulAmt.Text = string.Format("> {0} 원", kor);
                    }
                    else
                    {
                        lblHangulAmt.Text = "> 한글금액";
                    }
                };
            }
        }

        /// <summary>
        /// 보증금, 차임 금액 정리
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private string MoneyChk(string str)
        {
            string money = "", kor = "";

            string mixPtrn = @"(\d+)([십백천만억조]+)";
            string korPtrn = @"[일이삼사오육칠팔구만]+";

            StringBuilder sb = new StringBuilder();

            if (str.IndexOf("(") > -1)
            {
                str = str.Remove(str.IndexOf("("));
            }

            str = Regex.Replace(str, @"[금원정월매\,\s]", string.Empty).Trim();
            if (Regex.IsMatch(str, mixPtrn))
            {
                MatchCollection mc = Regex.Matches(str, mixPtrn);
                foreach (Match match in mc)
                {
                    kor = NumToKor(Convert.ToInt64(match.Groups[1].Value));
                    sb.Append(kor + match.Groups[2].Value);
                }
                str = sb.ToString();
            }

            if (Regex.IsMatch(str, korPtrn))
            {
                if (Regex.IsMatch(str, @"[^일이삼사오육칠팔구십백천만억조]")) money = str;
                else
                {
                    money = KorToNum(str);
                }
            }
            else
            {
                money = str;
            }

            return money;
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
        /// 한글 -> 숫자
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private string KorToNum(string input)
        {
            long result = 0;
            long tmpResult = 0;
            long num = 0;
            //MessageBox.Show(input);
            string number = "영일이삼사오육칠팔구";
            string unit = "십백천만억조";
            long[] unit_num = { 10, 100, 1000, 10000, (long)Math.Pow(10, 8), (long)Math.Pow(10, 12) };

            string[] arr = Regex.Split(input, @"(십|백|천|만|억|조)");    //괄호로 감싸주면 분할시 delimiters 포함한다.
            for (int i = 0; i < arr.Length; i++)
            {
                string token = arr[i];
                int check = number.IndexOf(token);
                if (check == -1)    //단위일 경우
                {
                    if ("만억조".IndexOf(token) == -1)
                    {
                        tmpResult += (num != 0 ? num : 1) * unit_num[unit.IndexOf(token)];
                    }
                    else
                    {
                        tmpResult += num;
                        result += (tmpResult != 0 ? tmpResult : 1) * unit_num[unit.IndexOf(token)];
                        tmpResult = 0;
                    }
                    num = 0;
                }
                else
                {
                    num = check;
                }
            }
            result = result + tmpResult + num;

            return result.ToString();
        }

        /// <summary>
        /// 임차인 현황-용도코드 변경시 사업자 체크 Step 1
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DgT_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (dgT.CurrentCell.ColumnIndex == 6 && e.Control is ComboBox)
            {
                ComboBox cbx = e.Control as ComboBox;
                cbx.SelectedIndexChanged -= DgT_UseSelectionChanged;
                cbx.SelectedIndexChanged += DgT_UseSelectionChanged;
            }
        }

        /// <summary>
        /// 임차인 현황-용도코드 변경시 사업자 체크 Step 2
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DgT_UseSelectionChanged(object sender, EventArgs e)
        {
            var curCell = dgT.CurrentCellAddress;
            var sendingCB = sender as DataGridViewComboBoxEditingControl;
            //DataGridViewTextBoxCell cell = (DataGridViewTextBoxCell)dgT.Rows[curCell.Y].Cells[0];
            //cell.Value = sendingCB.EditingControlFormattedValue.ToString();
            DataGridViewCheckBoxCell cell = (DataGridViewCheckBoxCell)dgT.Rows[curCell.Y].Cells["dgT_ChkBiz"];
            if (sendingCB.SelectedValue == null) return;
            if (sendingCB.SelectedValue.ToString() == "System.Data.DataRowView") return;
            //MessageBox.Show(sendingCB.SelectedValue.ToString());
            string[] bizArr = new string[] { "2", "3", "8", "9" };  //점포, 사무, 공장, 영업
            cell.Value = (bizArr.Contains(sendingCB.SelectedValue.ToString())) ? 1 : 0;
        }

        /// <summary>
        /// 등기-권리코드 변경시 권리명칭 연동 Step 1
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DgRg_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            DataGridView dgv = sender as DataGridView;
            if (dgv.CurrentCell.ColumnIndex == 5 && e.Control is ComboBox)
            {
                ComboBox cbx = e.Control as ComboBox;
                cbx.SelectedIndexChanged -= DgRg_UseSelectionChanged;
                cbx.SelectedIndexChanged += DgRg_UseSelectionChanged;
            }
        }

        /// <summary>
        /// 등기-권리코드 변경시 권리명칭 연동 Step 2
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DgRg_UseSelectionChanged(object sender, EventArgs e)
        {
            DataGridViewComboBoxEditingControl cbx = ((DataGridViewComboBoxEditingControl)sender);
            DataGridView dgv = (cbx.Parent.Parent.Name == "dgRL") ? dgRL : dgRB;
            var curCell = dgv.CurrentCellAddress;
            var sendingCB = sender as DataGridViewComboBoxEditingControl;
            DataGridViewTextBoxCell cell = (DataGridViewTextBoxCell)dgv.Rows[curCell.Y].Cells[dgv.Name + "_RgNm"];
            if (sendingCB.SelectedValue == null) return;
            if (sendingCB.SelectedValue.ToString() == "System.Data.DataRowView") return;
            cell.Value = (sendingCB.Text.Contains("선택")) ? string.Empty : sendingCB.Text;
        }

        private void RdoPres_CheckedChanged(object sender, EventArgs e)
        {
            Byte val = 0;
            int rowCnt = 0;

            RadioButton rdo = (RadioButton)sender;
            if (rdo == rdoPres0) val = 0;
            else if (rdo == rdoPres1) val = 1;
            else val = 2;

            rowCnt = dgE.Rows.Count - 1;
            foreach (DataGridViewRow row in dgE.Rows)
            {
                if (row.Index == rowCnt) continue;
                ((DataGridViewComboBoxCell)row.Cells["dgE_Inc"]).Value = val;
            }
        }

        /// <summary>
        /// 소재지 검색
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CbxAdrsCd_SelectedIndexChanged(object sender, EventArgs e)
        {
            string sql;
            ComboBox cbx = ((ComboBox)sender);

            if (cbx == cbxSi)
            {
                if (cbxSi.SelectedValue.ToString() == "36")
                {
                    //세종시
                    sql = "select gu_nm, gu_cd from tx_cd_adrs where si_cd=36 and gu_cd=110 and dn_cd > 0 limit 1";
                }
                else
                {
                    sql = "select gu_nm, gu_cd from tx_cd_adrs where si_cd=" + cbxSi.SelectedValue.ToString() + " and gu_cd > 0 and dn_cd=0 and hide=0 order by gu_nm";
                }                
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
        /// DataGridViewComboBoxColumn 마우스휠 무력화
        /// </summary>
        /// <param name="dgvLst"></param>
        private void DgvCbxMouseWheelDisable(List<DataGridView> dgvLst)
        {
            foreach (DataGridView dgv in dgvLst)
            {
                dgv.EditingControlShowing += (s, e) =>
                {
                    DataGridViewComboBoxEditingControl editingControl = e.Control as DataGridViewComboBoxEditingControl;
                    if (editingControl != null)
                    {
                        editingControl.MouseWheel += (s2, e2) =>
                        {
                            if (!editingControl.DroppedDown)
                            {
                                ((HandledMouseEventArgs)e2).Handled = true;
                                //dgv.Focus();
                            }
                        };
                    }
                };
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
        /// 목록내역-등기Pin 클릭시 자동발급 대상으로 추가
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DgI_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            string pin, regt_no, url, pnu, serviceKey;
            string sql, tid, lsNo, lsDvsn;

            if (e.ColumnIndex < 0) return;

            DataGridView dgv = sender as DataGridView;
            int rowIdx = e.RowIndex;
            if (rowIdx == -1) return;
            string colNm = dgv.Columns[e.ColumnIndex].Name;
            if (dgv[e.ColumnIndex, rowIdx].Value == null) return;

            if (colNm == "dgI_Pin")
            {
                pin = dgv[e.ColumnIndex, rowIdx].Value.ToString();
                if (pin != string.Empty)
                {
                    //regt_no = pin.Substring(0, 4);
                    //wbr2.Navigate("http://www.iros.go.kr/iris/index.jsp?inpSvcCls=on&selkindcls=&e001admin_regn1=&e001admin_regn3=&a312lot_no=&a301buld_name=&a301buld_no_buld=&a301buld_no_room=&pin=" + pin + "&regt_no=" + regt_no + "&svc_cls=VW&fromjunja=Y", null, null, @"Referer: http://www.iros.go.kr/iris/hom/RHOMDetailSelect.jsp?pin=" + pin + "&regt_no=" + regt_no + "&from=CA");
                    int noExtr = 0;
                    string msg = "등기 자동발급에 추가 하시겠습니까?";
                    if (chkNoExtr.Checked)
                    {
                        noExtr = 1;
                        msg += "\r\n\r\n ※※※ 주의!!! 등기추출 안함 ※※※";
                    }
                    if (MessageBox.Show(msg, "등기발급", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No) return;
                    
                    tid = lnkTid.Text;
                    lsNo = dgI["dgI_LsNo", rowIdx].Value.ToString();
                    lsDvsn = dgI["dgI_Dvsn", rowIdx].Value.ToString();
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
            }
            else if (colNm == "dgI_Pnu")
            {
                pnu = dgv["dgI_Pnu", rowIdx].Value?.ToString();
                serviceKey = api.RndSrvKey();
                if (dgv["dgI_Dvsn", rowIdx].Value?.ToString() == "토지")
                {
                    //토지융합정보
                    url = "http://apis.data.go.kr/1611000/nsdi/LandMoveService/attr/getLandMoveAttr?serviceKey=" + serviceKey + "&pnu=" + pnu + "&numOfRows=10&pageNo=1";
                    Process.Start("IExplore.exe", url);
                }
                else if (dgv["dgI_Dvsn", rowIdx].Value?.ToString()=="건물" || dgv["dgI_Dvsn", rowIdx].Value?.ToString() == "집합건물")
                {
                    //건축물대장정보-표제부조회
                    string platGbCd = (Convert.ToDecimal(pnu.Substring(10, 1)) - 1).ToString();
                    string bun = pnu.Substring(11, 4);
                    string ji = pnu.Substring(15, 4);
                    url = "http://apis.data.go.kr/1613000/BldRgstService_v2/getBrTitleInfo?serviceKey=" + serviceKey + "&sigunguCd=" + pnu.Substring(0, 5) + "&bjdongCd=" + pnu.Substring(5, 5) + "&platGbCd=" + platGbCd + "&bun=" + bun + "&ji=" + ji + "&numOfRows=100&pageNo=1";
                    Process.Start("IExplore.exe", url);
                }
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
        /// DataGridView MouseUp-ContextMenu Show
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Dg_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) return;
            DataGridView dgv = sender as DataGridView;
            //if (dgv.SelectedRows.Count == 0) return;
            dgMenu.Show(dgv, dgv.PointToClient(Control.MousePosition));
        }

        /// <summary>
        /// DataGridView ContextMenu Item Click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DgMenu_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            int i = 0;
            string creditor = "", debtor = "", owner = "", prsnCnt = "", prsn = "", rgCd = "";

            ContextMenuStrip menuItem = sender as ContextMenuStrip;            
            Control ctrl = menuItem.SourceControl;
            if (ctrl.GetType() != typeof(DataGridView)) return;
            DataGridView dgv = ctrl as DataGridView;
            if (dgv.CurrentRow == null) return;

            int rowIdx = dgv.CurrentRow.Index;
            string menu = e.ClickedItem.Text;
            if (menu == "+ 행추가")
            {
                dgv.Rows.Insert(rowIdx, 1);
            }
            else if (menu == "+ 행추가(5)")
            {
                dgv.Rows.Insert(rowIdx, 5);
            }
            else if (menu == "- 행삭제")
            {
                DgRow_DeletePrc(dgv, rowIdx);
                if (dgv.Rows.Count == (rowIdx + 1)) return;     //커밋되지 않은 새 행은 삭제할 수 없습니다
                dgv.Rows.RemoveAt(rowIdx);
            }
            else if (menu == "일괄삭제")
            {
                DgRow_MultiDeletePrc(dgv);
                //if (dgv.Rows.Count == (rowIdx + 1)) return;     //커밋되지 않은 새 행은 삭제할 수 없습니다
                //dgv.Rows.RemoveAt(rowIdx);
            }
            else if (menu == "채권자")
            {
                if (dgv == dgRL || dgv == dgRB)
                {
                    prsnCnt = Regex.Match(txtCreditor.Text, @"외[ ]*\d+").Value;
                    creditor = (dgv == dgRL) ? dgv.CurrentRow.Cells["dgRL_Prsn"].Value?.ToString() : dgv.CurrentRow.Cells["dgRB_Prsn"].Value?.ToString();
                    txtCreditor.Text = (creditor + " " + prsnCnt).Trim();
                }
            }
            else if (menu == "채무자")
            {
                if (dgv == dgRL || dgv == dgRB)
                {
                    prsnCnt = Regex.Match(txtDebtor.Text, @"외[ ]*\d+").Value;
                    debtor = (dgv == dgRL) ? dgv.CurrentRow.Cells["dgRL_Prsn"].Value?.ToString() : dgv.CurrentRow.Cells["dgRB_Prsn"].Value?.ToString();
                    txtDebtor.Text = (debtor + " " + prsnCnt).Trim();
                }
            }
            else if (menu == "소유자")
            {
                if (dgv == dgRL || dgv == dgRB)
                {
                    prsnCnt = Regex.Match(txtOwner.Text, @"외[ ]*\d+").Value;
                    owner = (dgv == dgRL) ? dgv.CurrentRow.Cells["dgRL_Prsn"].Value?.ToString() : dgv.CurrentRow.Cells["dgRB_Prsn"].Value?.ToString();
                    txtOwner.Text = (owner + " " + prsnCnt).Trim();
                }
            }
            else if (menu == "채무자+소유자")
            {
                if (dgv == dgRL || dgv == dgRB)
                {
                    prsnCnt = Regex.Match(txtDebtor.Text, @"외[ ]*\d+").Value;
                    debtor = (dgv == dgRL) ? dgv.CurrentRow.Cells["dgRL_Prsn"].Value?.ToString() : dgv.CurrentRow.Cells["dgRB_Prsn"].Value?.ToString();
                    txtDebtor.Text = (debtor + " " + prsnCnt).Trim();
                }
                if (dgv == dgRL || dgv == dgRB)
                {
                    prsnCnt = Regex.Match(txtOwner.Text, @"외[ ]*\d+").Value;
                    owner = (dgv == dgRL) ? dgv.CurrentRow.Cells["dgRL_Prsn"].Value?.ToString() : dgv.CurrentRow.Cells["dgRB_Prsn"].Value?.ToString();
                    txtOwner.Text = (owner + " " + prsnCnt).Trim();
                }
            }
            else if (menu == "임차인추가")
            {
                if (dgv == dgRB)
                {
                    foreach (DataGridViewRow row in dgv.SelectedRows.Cast<DataGridViewRow>().Reverse())
                    {
                        rgCd = row.Cells["dgRB_RgCd"].Value?.ToString();
                        prsn = row.Cells["dgRB_Prsn"].Value?.ToString();

                        if (rgCd != "8" && rgCd != "9" && rgCd != "28") continue;

                        i = dgT.Rows.Add();
                        dgT["dgT_Prsn", i].Value = prsn;

                        switch (rgCd)
                        {
                            case "8":   //전세권
                                dgT["dgT_InvType", i].Value = "전세권등기자";
                                dgT["dgT_Deposit", i].Value = row.Cells["dgRB_CAmt"].Value;
                                break;

                            case "9":   //주택임차권
                                dgT["dgT_InvType", i].Value = "임차권등기자";
                                dgT["dgT_UseCd", i].Value = Convert.ToByte(1);
                                dgT["dgT_Deposit", i].Value = row.Cells["dgRB_CAmt"].Value;
                                dgT["dgT_MvDt", i].Value = row.Cells["dgRB_MvDt"].Value;
                                dgT["dgT_FxDt", i].Value = row.Cells["dgRB_FxDt"].Value;
                                break;

                            case "28":  //상가건물임차권
                                dgT["dgT_InvType", i].Value = "상가임차권등기자";
                                dgT["dgT_UseCd", i].Value = Convert.ToByte(2);
                                dgT["dgT_Deposit", i].Value = row.Cells["dgRB_CAmt"].Value;
                                dgT["dgT_MvDt", i].Value = row.Cells["dgRB_MvDt"].Value;
                                dgT["dgT_FxDt", i].Value = row.Cells["dgRB_FxDt"].Value;
                                dgT["dgT_ChkBiz", i].Value = 1;
                                break;
                        }
                    }
                }
            }
            else if (menu == "현황복사")
            {
                if (dgv == dgL || dgv == dgB || dgv == dgE || dgv == dgT)
                {
                    sfStateCopy sfStateCopy = new sfStateCopy() { Owner = this };
                    sfStateCopy.StartPosition = FormStartPosition.CenterScreen;
                    sfStateCopy.FormBorderStyle = FormBorderStyle.FixedToolWindow;
                    sfStateCopy.ShowDialog();
                    sfStateCopy.Dispose();
                }
            }
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
        /// 회원 관심물건 이동
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnIntrPrc_Click(object sender, EventArgs e)
        {
            sfIntrMove sfIntr = new sfIntrMove() { Owner = this };
            sfIntr.StartPosition = FormStartPosition.CenterScreen;
            sfIntr.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            sfIntr.ShowDialog();
            sfIntr.Dispose();
        }

        /// <summary>
        /// 선택 일괄삭제
        /// </summary>
        /// <param name="dgv"></param>
        /// <param name="rowIdx"></param>
        private void DgRow_MultiDeletePrc(DataGridView dgv)
        {
            int dbIdx;
            string idxCellNm = "", sql;
            /*
            if (dgv != dgRL && dgv != dgRB && dgv != dgE)
            {
                MessageBox.Show("[토지/건물]등기, 제시외 에서만 일괄 삭제 할 수 있습니다.");
                return;
            }
            */
            if (MessageBox.Show("선택한 행을 일괄 삭제 하시겠습니까?", "삭제 확인!!!", MessageBoxButtons.YesNo) == DialogResult.No)
            {
                return;
            }

            if (dgv == dgRL) idxCellNm = "dgRL_Idx";
            else if (dgv == dgRB) idxCellNm = "dgRB_Idx";
            else if (dgv == dgE) idxCellNm = "dgE_Idx";
            else if (dgv == dgL) idxCellNm = "dgL_Idx";
            else if (dgv == dgB) idxCellNm = "dgB_Idx";

            db.Open();
            foreach (DataGridViewRow row in dgv.SelectedRows)
            {
                if (row.Cells[idxCellNm].Value != null)
                {
                    if (row.Cells[idxCellNm].Value.ToString() != string.Empty)
                    {
                        sql = string.Empty;
                        dbIdx = Convert.ToInt32(row.Cells[idxCellNm].Value);
                        if (dgv == dgL)
                        {
                            sql = "delete from ta_land where idx='" + dbIdx + "'";
                        }
                        else if (dgv == dgB || dgv == dgE)
                        {
                            sql = "delete from ta_bldg where idx='" + dbIdx + "'";
                        }
                        else if (dgv == dgRL || dgv == dgRB)
                        {
                            sql = "delete from ta_rgst where idx='" + dbIdx + "'";
                        }
                        else
                        {
                            continue;
                        }
                        db.ExeQry(sql);
                    }                    
                }
                try
                {
                    dgv.Rows.RemoveAt(row.Index);
                }
                catch { }
            }
            db.Close();
        }

        /// <summary>
        /// DataGridView 행삭제-Delete 키 사용
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Dg_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
        {
            DataGridView dgv = sender as DataGridView;
            DgRow_DeletePrc(dgv, e.Row.Index);
        }

        /// <summary>
        /// 행삭제 전 DB처리
        /// </summary>
        /// <param name="dgv"></param>
        /// <param name="rowIdx"></param>
        private void DgRow_DeletePrc(DataGridView dgv, int rowIdx)
        {
            string sql = "", dbIdx = "", tbl = "", idxCellNm = "";

            Dictionary<string, string> dict = new Dictionary<string, string>();
            dict.Add("dgH", "ta_hist");
            dict.Add("dgI", "ta_ls");
            dict.Add("dgL", "ta_land");
            dict.Add("dgB", "ta_bldg");
            dict.Add("dgE", "ta_bldg");
            dict.Add("dgM", "ta_bldg");
            dict.Add("dgC", "ta_cars");
            dict.Add("dgT", "ta_leas");
            dict.Add("dgRB", "ta_rgst");
            dict.Add("dgRL", "ta_rgst");

            idxCellNm = dgv.Name + "_Idx";

            //if (dgv.Rows[rowIdx].Cells[0].Value == null) return;
            //idx = dgv.Rows[rowIdx].Cells[0].Value.ToString();
            if (dgv.Rows[rowIdx].Cells[idxCellNm].Value == null) return;
            if (dgv.Rows[rowIdx].Cells[idxCellNm].Value.ToString() == string.Empty) return;

            dbIdx = dgv.Rows[rowIdx].Cells[idxCellNm].Value.ToString();
            //MessageBox.Show(dgv.Name + " -> " + dbIdx);            
            db.Open();
            tbl = dict[dgv.Name];
            sql = "delete from " + tbl + " where idx='" + dbIdx + "'";
            db.ExeQry(sql);
            db.Close();
        }

        /// <summary>
        /// DataGridView Cell 편집을 위한 컨트롤이 표시될 때
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Dg_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            string colName = "";
            List<string> lstNumCol = new List<string>();
            lstNumCol.Add("dgL_UnitPrc");
            lstNumCol.Add("dgL_Sqm");
            lstNumCol.Add("dgL_RtSqm");
            lstNumCol.Add("dgB_UnitPrc");
            lstNumCol.Add("dgB_Sqm");
            lstNumCol.Add("dgB_ActlSqm");
            lstNumCol.Add("dgE_UnitPrc");
            lstNumCol.Add("dgE_Sqm");
            lstNumCol.Add("dgM_UnitPrc");
            lstNumCol.Add("dgM_Amt");

            lstNumCol.Add("dgL_TotRtSqm");
            lstNumCol.Add("dgL_TotShrSqm");
            lstNumCol.Add("dgB_TotShrSqm");
            lstNumCol.Add("dgE_TotShrSqm");
            e.Control.KeyPress -= new KeyPressEventHandler(DgColNum_KeyPress);

            DataGridView dgv = (DataGridView)sender;
            colName = dgv.Columns[dgv.CurrentCell.ColumnIndex].Name;
            if (lstNumCol.Contains(colName))
            {
                TextBox tbx = e.Control as TextBox;
                if (tbx != null)
                {
                    tbx.KeyPress += new KeyPressEventHandler(DgColNum_KeyPress);
                }
            }
        }

        /// <summary>
        /// DataGridView Cell 값을 숫자, 콤마, 소숫점만 허용
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DgColNum_KeyPress(object sender, KeyPressEventArgs e)
        {
            //if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            if (!(char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar) || e.KeyChar == ',' || e.KeyChar == '.'))
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// 토지,건물,제시외,기계/기구 현황-평가액 계산(면적 * 단가)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DgAmtCal_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            string dgvNm = "", colNm = "";
            //decimal amt = 0;
            bool multiBldg = false;
            decimal cat3 = 0;

            //decimal[] multBldgArr = new decimal[] { 201013, 201014, 201015, 201017, 201019, 201022, 201130, 201216, 201123, 201020, 201111 };

            if (cbxCat3.SelectedIndex == 0)
            {
                MessageBox.Show("[물건종별]을 선택 해 주세요.");
                return;
            }

            if (chkAllowAnyVal.Checked)     //임의값적용
            {
                return;
            }

            cat3 = Convert.ToDecimal(cbxCat3.SelectedValue);
            if (multiBldgArr.Contains(cat3)) multiBldg = true;
            if (chkMultiBldg.Checked) multiBldg = true;
            //MessageBox.Show(multiBldg.ToString());
            DataGridView dgv = (DataGridView)sender;
            dgvNm = dgv.Name;
            colNm = dgv.Columns[e.ColumnIndex].Name;
            
            if (dgv == dgM || multiBldg)
            {
                if (colNm.Contains("_Amt"))
                {
                    dgv[colNm, e.RowIndex].Value = string.Format("{0:N0}", Convert.ToDecimal(dgv[colNm, e.RowIndex].Value));
                }
            }
            else
            {
                /*
                if (dgv == dgB)
                {
                    if (colNm.Contains("_UnitPrc") || colNm.Contains("_Sqm") || colNm.Contains("_ActlSqm"))
                    {
                        if (dgv[colNm, e.RowIndex].Value == null || dgv[colNm, e.RowIndex].Value.ToString() == string.Empty) dgv[colNm, e.RowIndex].Value = "0";
                        dgv[colNm, e.RowIndex].Value = (colNm.Contains("_Sqm")) ? string.Format("{0:N4}", Convert.ToDecimal(dgv[colNm, e.RowIndex].Value)) : string.Format("{0:N0}", Convert.ToDecimal(dgv[colNm, e.RowIndex].Value));
                        dgv[dgvNm + "_Amt", e.RowIndex].Value = Convert.ToDecimal(dgv[dgvNm + "_Sqm", e.RowIndex].Value) * Convert.ToDecimal(dgv[dgvNm + "_UnitPrc", e.RowIndex].Value);
                    }
                }
                else
                {
                    if (colNm.Contains("_UnitPrc") || colNm.Contains("_Sqm"))
                    {
                        if (dgv[colNm, e.RowIndex].Value == null || dgv[colNm, e.RowIndex].Value.ToString() == string.Empty) dgv[colNm, e.RowIndex].Value = "0";
                        dgv[colNm, e.RowIndex].Value = (colNm.Contains("_Sqm")) ? string.Format("{0:N4}", Convert.ToDecimal(dgv[colNm, e.RowIndex].Value)) : string.Format("{0:N0}", Convert.ToDecimal(dgv[colNm, e.RowIndex].Value));
                        dgv[dgvNm + "_Amt", e.RowIndex].Value = Convert.ToDecimal(dgv[dgvNm + "_Sqm", e.RowIndex].Value) * Convert.ToDecimal(dgv[dgvNm + "_UnitPrc", e.RowIndex].Value);
                    }
                }
                */

                if (dgv == dgB || dgv == dgE)
                {
                    if (colNm.Contains("_UnitPrc") || colNm.Contains("_Sqm") || colNm.Contains("_ActlSqm"))
                    {                        
                        if (dgv[colNm, e.RowIndex].Value == null || dgv[colNm, e.RowIndex].Value.ToString() == string.Empty) dgv[colNm, e.RowIndex].Value = "0";
                        dgv[colNm, e.RowIndex].Value = (colNm.Contains("_Sqm") || colNm.Contains("_ActlSqm")) ? string.Format("{0:N4}", Convert.ToDecimal(dgv[colNm, e.RowIndex].Value)) : string.Format("{0:N0}", Convert.ToDecimal(dgv[colNm, e.RowIndex].Value));
                        //amt = (dgv[dgvNm + "_Amt", e.RowIndex].Value == null || dgv[dgvNm + "_Amt", e.RowIndex].Value?.ToString() == string.Empty) ? 0 : Convert.ToDecimal(dgv[dgvNm + "_Amt", e.RowIndex].Value);
                        //if (amt == 0)
                        //{   
                            if(Convert.ToDecimal(dgv[dgvNm + "_ActlSqm", e.RowIndex].Value) > 0) dgv[dgvNm + "_Amt", e.RowIndex].Value = Convert.ToDecimal(dgv[dgvNm + "_ActlSqm", e.RowIndex].Value) * Convert.ToDecimal(dgv[dgvNm + "_UnitPrc", e.RowIndex].Value);
                            else dgv[dgvNm + "_Amt", e.RowIndex].Value = Convert.ToDecimal(dgv[dgvNm + "_Sqm", e.RowIndex].Value) * Convert.ToDecimal(dgv[dgvNm + "_UnitPrc", e.RowIndex].Value);
                        //}                        
                    }
                }
                else
                {
                    if (colNm.Contains("_UnitPrc") || colNm.Contains("_Sqm"))
                    {
                        if (dgv[colNm, e.RowIndex].Value == null || dgv[colNm, e.RowIndex].Value.ToString() == string.Empty) dgv[colNm, e.RowIndex].Value = "0";
                        dgv[colNm, e.RowIndex].Value = (colNm.Contains("_Sqm")) ? string.Format("{0:N4}", Convert.ToDecimal(dgv[colNm, e.RowIndex].Value)) : string.Format("{0:N0}", Convert.ToDecimal(dgv[colNm, e.RowIndex].Value));
                        dgv[dgvNm + "_Amt", e.RowIndex].Value = Convert.ToDecimal(dgv[dgvNm + "_Sqm", e.RowIndex].Value) * Convert.ToDecimal(dgv[dgvNm + "_UnitPrc", e.RowIndex].Value);
                    }
                }
            }

            Sum_SqmAmt(dgvNm);
        }

        /// <summary>
        /// 면적 및 평가액 합계 계산
        /// </summary>
        /// <param name="dgvNm"></param>
        private void Sum_SqmAmt(string dgvNm)
        {
            decimal sumSqm = 0, sumSqmA = 0, sumSqmB = 0, sumTotSqm = 0, sumRtSqm = 0, sumTotSqmA = 0, sumTotSqmB = 0, sumAmt = 0, sumAmtA = 0, sumAmtB = 0, sumApsl = 0;

            if (dgvNm == "dgL")
            {
                sumSqm = dgL.Rows.Cast<DataGridViewRow>().Where(t => t.Cells["dgL_Sqm"].Value?.ToString() != string.Empty).Sum(t => Convert.ToDecimal(t.Cells["dgL_Sqm"].Value));
                sumAmt = dgL.Rows.Cast<DataGridViewRow>().Where(t => t.Cells["dgL_Amt"].Value?.ToString() != string.Empty).Sum(t => Convert.ToDecimal(t.Cells["dgL_Amt"].Value));
                sumTotSqm = dgL.Rows.Cast<DataGridViewRow>().Where(t => t.Cells["dgL_TotShrSqm"].Value?.ToString() != string.Empty).Sum(t => Convert.ToDecimal(t.Cells["dgL_TotShrSqm"].Value));
                sumRtSqm = dgL.Rows.Cast<DataGridViewRow>().Where(t => t.Cells["dgL_RtSqm"].Value?.ToString() != string.Empty).Sum(t => Convert.ToDecimal(t.Cells["dgL_RtSqm"].Value));
                txtDgLSqmSum.Text = string.Format("{0:N4}", sumSqm);
                txtDgLTotSqmSum.Text = string.Format("{0:N4}", sumTotSqm);                
                txtDgLAmtSum.Text = string.Format("{0:N0}", sumAmt);
                txtLandApslAmt.Text = string.Format("{0:N0}", sumAmt);
                txtLandSqm.Text = sumSqm.ToString();
                txtLandTotSqm.Text = string.Format("{0:N4}", sumTotSqm);
                txtLandPy.Text = string.Format("{0:N4}", SqmToPyng(sumSqm));
                txtRtSqm.Text = sumRtSqm.ToString();
            }
            else if (dgvNm == "dgB" || dgvNm == "dgE")
            {
                if (dgvNm == "dgB")
                {                    
                    sumAmt = dgB.Rows.Cast<DataGridViewRow>().Where(t => t.Cells["dgB_Amt"].Value?.ToString() != string.Empty).Sum(t => Convert.ToDecimal(t.Cells["dgB_Amt"].Value));                    
                    txtDgBAmtSum.Text = string.Format("{0:N0}", sumAmt);                    
                    txtBldgApslAmt.Text = string.Format("{0:N0}", sumAmt);                    
                }
                else
                {                    
                    sumAmtA = dgE.Rows.Cast<DataGridViewRow>().Where(t => t.Cells["dgE_Amt"].Value?.ToString() != string.Empty && t.Cells["dgE_Inc"].Value?.ToString() == "1").Sum(t => Convert.ToDecimal(t.Cells["dgE_Amt"].Value));
                    sumAmtB = dgE.Rows.Cast<DataGridViewRow>().Where(t => t.Cells["dgE_Amt"].Value?.ToString() != string.Empty && t.Cells["dgE_Inc"].Value?.ToString() == "2").Sum(t => Convert.ToDecimal(t.Cells["dgE_Amt"].Value));                    
                    txtDgEAmtSumInc.Text = string.Format("{0:N0}", sumAmtA);
                    txtDgEAmtSumDec.Text = string.Format("{0:N0}", sumAmtB);
                    txtPresApslAmtInc.Text = string.Format("{0:N0}", sumAmtA);
                    txtPresApslAmtDec.Text = string.Format("{0:N0}", sumAmtB);                    
                }
                sumSqmA = dgB.Rows.Cast<DataGridViewRow>().Where(t => t.Cells["dgB_Sqm"].Value?.ToString() != string.Empty).Sum(t => Convert.ToDecimal(t.Cells["dgB_Sqm"].Value));
                sumSqmB = dgE.Rows.Cast<DataGridViewRow>().Where(t => t.Cells["dgE_Sqm"].Value?.ToString() != string.Empty && t.Cells["dgE_Inc"].Value?.ToString() == "1").Sum(t => Convert.ToDecimal(t.Cells["dgE_Sqm"].Value));
                sumTotSqmA = dgB.Rows.Cast<DataGridViewRow>().Where(t => t.Cells["dgB_TotShrSqm"].Value?.ToString() != string.Empty).Sum(t => Convert.ToDecimal(t.Cells["dgB_TotShrSqm"].Value));
                sumTotSqmB = dgE.Rows.Cast<DataGridViewRow>().Where(t => t.Cells["dgE_TotShrSqm"].Value?.ToString() != string.Empty && t.Cells["dgE_Inc"].Value?.ToString() == "1").Sum(t => Convert.ToDecimal(t.Cells["dgE_TotShrSqm"].Value));
                txtDgBSqmSum.Text = string.Format("{0:N4}", sumSqmA);
                txtDgESqmSum.Text = string.Format("{0:N4}", sumSqmB);
                txtDgBTotSqmSum.Text = string.Format("{0:N4}", sumTotSqmA);
                txtDgETotSqmSum.Text = string.Format("{0:N4}", sumTotSqmB);
                sumSqm = sumSqmA + sumSqmB;
                sumTotSqm = sumTotSqmA + sumTotSqmB;
                txtBldgSqm.Text = sumSqm.ToString();
                txtBldgPy.Text = string.Format("{0:N4}", SqmToPyng(sumSqm));
                txtBldgTotSqm.Text = sumTotSqm.ToString();
            }
            else if (dgvNm == "dgM")
            {
                sumAmt = dgM.Rows.Cast<DataGridViewRow>().Where(t => t.Cells["dgM_Amt"].Value?.ToString() != string.Empty).Sum(t => Convert.ToDecimal(t.Cells["dgM_Amt"].Value));
                txtDgMAmtSum.Text = string.Format("{0:N0}", sumAmt);
                txtMachApslAmt.Text = string.Format("{0:N0}", sumAmt);
            }

            if (txtLandApslAmt.Text == string.Empty) txtLandApslAmt.Text = "0";
            if (txtBldgApslAmt.Text == string.Empty) txtBldgApslAmt.Text = "0";
            if (txtPresApslAmtInc.Text == string.Empty) txtPresApslAmtInc.Text = "0";
            if (txtMachApslAmt.Text == string.Empty) txtMachApslAmt.Text = "0";

            sumApsl = Convert.ToDecimal(txtLandApslAmt.Text.Replace(",",string.Empty)) + 
                Convert.ToDecimal(txtBldgApslAmt.Text.Replace(",", string.Empty)) + 
                Convert.ToDecimal(txtPresApslAmtInc.Text.Replace(",", string.Empty)) + 
                Convert.ToDecimal(txtMachApslAmt.Text.Replace(",", string.Empty));
            txtApslSum.Text = string.Format("{0:N0}", sumApsl);
        }

        private decimal PyngToSqm(decimal pyng)
        {
            decimal sqm = 0;

            sqm = pyng * Convert.ToDecimal(3.3058);
            return sqm;
        }

        private decimal SqmToPyng(decimal sqm)
        {
            decimal pyng = 0;

            pyng = sqm * Convert.ToDecimal(0.3025);
            return pyng;
        }

        /// <summary>
        /// TextBox 콤마 형식
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TxtComma_TextChanged(object sender, EventArgs e)
        {
            string val = "";

            TextBox tbx = (TextBox)sender;
            val = tbx.Text.Replace(",", "");
            if (val == "") return;
            tbx.Text = string.Format("{0:#,##0}", Convert.ToDouble(val));
            tbx.SelectionStart = tbx.TextLength;
            tbx.SelectionLength = 0;
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
        /// 물건 검색
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void btnSrch_Click(object sender, EventArgs e)
        {
            string sql = "";
                        
            cdtn = "1";
            dg.Rows.Clear();
            ui.FormClear(tabDtl, new string[] { "cbxCrtSpt", "cbxDpt" });
            //lnkTid.Text = "TID";
            lnkTid.Text = string.Empty;

            List<string> cdtnList = new List<string>();

            //등기파일 유/무에 따른 칼럼(토,건) 보임/숨김
            if (chkRgst.Checked)
            {
                dg.Columns["dg_RgstLand"].Visible = true;
                dg.Columns["dg_RgstBldg"].Visible = true;
            }
            else
            {
                dg.Columns["dg_RgstLand"].Visible = false;
                dg.Columns["dg_RgstBldg"].Visible = false;
            }

            //if (txtSrchTid.Text.Trim() != "") cdtnList.Add("tid=" + txtSrchTid.Text.Trim());
            txtSrchTid.Text = txtSrchTid.Text.Replace("_", string.Empty).Trim();
            if (txtSrchSn.Text.Trim() != "")
            {
                Match match = Regex.Match(txtSrchSn.Text.Trim(), @"^(\d+)[\-]*(\d+)*[\-]*(\d+)*", RegexOptions.Multiline);
                if (match.Groups[3].Value != "") cdtnList.Add("sn1=" + match.Groups[1].Value + " and sn2=" + match.Groups[2].Value + " and pn=" + match.Groups[3].Value);   //2018-4567-8
                else if (match.Groups[2].Value != "") cdtnList.Add("sn1=" + match.Groups[1].Value + " and sn2=" + match.Groups[2].Value);   //2018-4567
                else if (match.Groups[1].Value != "") cdtnList.Add("sn2=" + match.Groups[1].Value);     //4567
            }
            //if (cbxSrchCs.SelectedIndex > 0) condList.Add("crt=" + cbxSrchCs.SelectedValue.ToString().Substring(0, 2) + " and spt=" + cbxSrchCs.SelectedValue.ToString().Substring(2, 2));
            if (cbxSrchCs.SelectedIndex > 0) cdtnList.Add("spt=" + cbxSrchCs.SelectedValue.ToString());
            if (cbxSrchDpt.SelectedIndex > 0) cdtnList.Add("dpt=" + cbxSrchDpt.SelectedValue.ToString());
            if (cbxSrchSta1.SelectedIndex > 0) cdtnList.Add("sta1=" + cbxSrchSta1.SelectedValue.ToString());
            if (cbxSrchSta2.SelectedIndex > 0) cdtnList.Add("sta2=" + cbxSrchSta2.SelectedValue.ToString());
            if (cbxSrchCat.SelectedIndex > 0) cdtnList.Add("cat3=" + cbxSrchCat.SelectedValue.ToString());
            
            if (cbxSi.SelectedIndex > 0) cdtnList.Add("si_cd=" + cbxSi.SelectedValue.ToString());
            if (cbxGu.SelectedIndex > 0) cdtnList.Add("gu_cd=" + cbxGu.SelectedValue.ToString());
            if (cbxDn.SelectedIndex > 0) cdtnList.Add("dn_cd=" + cbxDn.SelectedValue.ToString());

            if (dtpBidDtBgn.Checked) cdtnList.Add("bid_dt >= '" + dtpBidDtBgn.Value.ToShortDateString() + "'");
            if (dtpBidDtEnd.Checked) cdtnList.Add("bid_dt <= '" + dtpBidDtEnd.Value.ToShortDateString() + "'");

            if (chkMerg.Checked)
            {
                if (dtp1stDtBgn.Checked) cdtnList.Add("wdt >= '" + dtp1stDtBgn.Value.ToShortDateString() + "'");
                if (dtp1stDtEnd.Checked) cdtnList.Add("wdt <= '" + dtp1stDtEnd.Value.ToShortDateString() + "'");
            }
            else
            {
                if (chkPreNt.Checked)
                {
                    if (dtp1stDtBgn.Checked) cdtnList.Add("pre_dt >= '" + dtp1stDtBgn.Value.ToShortDateString() + "'");
                    if (dtp1stDtEnd.Checked) cdtnList.Add("pre_dt <= '" + dtp1stDtEnd.Value.ToShortDateString() + "'");
                }
                else
                {
                    if (dtp1stDtBgn.Checked) cdtnList.Add("(1st_dt >= '" + dtp1stDtBgn.Value.ToShortDateString() + "' or 2nd_dt >= '" + dtp1stDtBgn.Value.ToShortDateString() + "' or pre_dt >= '" + dtp1stDtBgn.Value.ToShortDateString() + "')");
                    if (dtp1stDtEnd.Checked) cdtnList.Add("(1st_dt <= '" + dtp1stDtEnd.Value.ToShortDateString() + "' or 2nd_dt <= '" + dtp1stDtEnd.Value.ToShortDateString() + "' or pre_dt <= '" + dtp1stDtEnd.Value.ToShortDateString() + "')");
                }                
            }
            if (chkPreNt.Checked) cdtnList.Add("bid_dt > date_add(curdate(),interval 14 day) and pre_dt > '0000-00-00'");   //선행공고
            if (chkNoCat.Checked) cdtnList.Add("cat3=0");
            if (cbxSrchSta1.SelectedValue.ToString() == "10" && chkIniDt.Checked)
            {
                cdtnList.Add("ini_dt <= date_sub(curdate(),interval 15 day)");
            }

            if (cbxSrchDpslDvsn.SelectedIndex > 0) cdtnList.Add("dpsl_dvsn=" + cbxSrchDpslDvsn.SelectedValue.ToString());
            if (chkSplSrch.Checked) cdtnList.Add("sp_cdtn > 0");
            if (chkCoordErr.Checked) cdtnList.Add("x=0");
            if (chkAptErr.Checked) cdtnList.Add("apt_cd=0");
            if (chkSpRgst.Checked) cdtnList.Add("sp_rgst in (1,5)");

            //내부 작업상태
            if (cbxSrchWorks.SelectedIndex > 0)
            {
                if (cbxSrchWorks.Text == "입력완료") cdtnList.Add("works=1");
                else cdtnList.Add("works=0");
            }

            if (cdtnList.Count > 0) cdtn = string.Join(" and ", cdtnList.ToArray());
            if (txtSrchTid.Text.Trim() != "")
            {
                cdtn = "tid IN (" + Regex.Replace(txtSrchTid.Text.Trim(),@"\D+",",") + ")";   //TID 검색일 경우 모든 조건 무시
            }

            if (chkMerg.Checked)
            {
                sql = "select count(*) from ta_list L, ta_merg M where L.tid=M.mtid and " + cdtn;
            }
            /*else if (chkRgstErr.Checked)
            {                
                sql = "select count(*) from db_main.ta_list L, db_tank.tx_rgst_err R where L.tid=R.tid and works=0 and proc=1";
            }*/
            else if (dtpRgstMdfyDtBgn.Checked || dtpRgstMdfyDtEnd.Checked)
            {
                //sql = "select count(C.idx) as cnt from" +
                //    " (select R.idx, count(R.idx) from db_main.ta_list L, db_tank.tx_rgst_auto R where L.tid=R.tid and R.dvsn=13 and ul=1 and sta1 in (11,13)";
                sql = "select count(C.idx) as cnt from" +
                    " (select R.idx, count(R.idx) from db_main.ta_list L, db_tank.tx_rgst_auto R where L.tid=R.tid and R.dvsn=13 and ul=1";
                if (dtpRgstMdfyDtBgn.Checked) sql += " and wdt >='" + dtpRgstMdfyDtBgn.Value.ToShortDateString() + "'";
                if (dtpRgstMdfyDtEnd.Checked) sql += " and wdt <='" + dtpRgstMdfyDtEnd.Value.ToShortDateString() + "'";
                if (txtSrchTid.Text.Trim() != "")
                {
                   sql += " and L.tid IN (" + Regex.Replace(txtSrchTid.Text.Trim(), @"\D+", ",") + ")";
                }
                sql += " group by L.tid) as C";
            }
            else if (chkRtSqmErr.Checked)
            {
                sql = "SELECT count(*) FROM ta_list WHERE rt_sqm > rt_tot_sqm";
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
                targetDt = DateTime.Now.AddDays(0);
                if (chkWorkType.Checked) cbxSrchSta1.SelectedValue = 0;
                else cbxSrchSta1.SelectedValue = 11;
                cbxSrchSta2.SelectedValue = 0;
            }

            dtpBidDtBgn.Checked = true;
            dtpBidDtEnd.Checked = true;
            dtpBidDtBgn.Value = targetDt;
            dtpBidDtEnd.Value = targetDt;

            btnSrch_Click(null, null);
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
            string sql = "", csCd = "", dpt = "", order = "", sort = "", state = "", cat = "", dpsl = "", sta1;
            string tbl, sn1, tid;
            bool rgstLand, rgstBldg;
            int rgstLandCnt = 0, rgstBldgCnt = 0;

            dg.Rows.Clear();

            DataTable dt = new DataTable();
            dt.Columns.Add("No");
            ComboBox cbxPage = (ComboBox)sender;
            NumericUpDown listScale = (NumericUpDown)panPagn.Controls["_nudList"];
            startRow = (decimal.Parse(cbxPage.Text) - 1) * listScale.Value;
            dg.SelectionChanged -= dg_SelectionChanged;
            
            sort = cbxSrchSort.Text;
            if (sort == "사건번호")
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

            if (chkMerg.Checked)
            {
                sql = "select L.* from ta_list L , ta_merg M";
                sql += " where L.tid=M.mtid and " + cdtn + " order by " + order + " limit " + startRow + "," + listScale.Value;
            }
            /*else if (chkRgstErr.Checked)
            {
                sql = "select L.*,R.proc from db_main.ta_list L , db_tank.tx_rgst_err R where L.tid=R.tid and works=0 and proc=1";
            }*/
            else if (dtpRgstMdfyDtBgn.Checked || dtpRgstMdfyDtEnd.Checked)
            {
                order = "idtm desc";
                //sql = "select L.* from db_main.ta_list L, db_tank.tx_rgst_auto R where L.tid=R.tid and R.dvsn=13 and ul=1 and sta1 in (11,13)";
                sql = "select L.* from db_main.ta_list L, db_tank.tx_rgst_auto R where L.tid=R.tid and R.dvsn=13 and ul=1";
                if (dtpRgstMdfyDtBgn.Checked) sql += " and wdt >='" + dtpRgstMdfyDtBgn.Value.ToShortDateString() + "'";
                if (dtpRgstMdfyDtEnd.Checked) sql += " and wdt <='" + dtpRgstMdfyDtEnd.Value.ToShortDateString() + "'";
                if (txtSrchTid.Text.Trim() != "")
                {
                    sql += " and L.tid IN (" + Regex.Replace(txtSrchTid.Text.Trim(), @"\D+", ",") + ")";
                }
                sql += " group by L.tid";
                sql += " order by " + order + " limit " + startRow + "," + listScale.Value;
            }
            else if (chkRtSqmErr.Checked)
            {
                sql = "select * FROM ta_list WHERE rt_sqm > rt_tot_sqm";
            }
            else
            {
                sql = "select * from ta_list";
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
                var xCat= dtCatCd.Rows.Cast<DataRow>().Where(t => t["cat3_cd"].ToString() == dr["cat3"].ToString()).SingleOrDefault();
                    cat = (xCat == null || dr["cat3"].ToString() == "0") ? string.Empty : xCat.Field<string>("cat3_nm");
                var xDpt = dtDptCd.Rows.Cast<DataRow>().Where(t => t["cs_cd"].ToString() == csCd && t["dpt_cd"].ToString() == dr["dpt"].ToString()).SingleOrDefault();
                    dpt = (xDpt == null || dr["dpt"].ToString() == "0") ? string.Empty : xDpt.Field<string>("dpt_nm");
                var xDpsl = dtDpslCd.Rows.Cast<DataRow>().Where(t => t["cd"].ToString() == dr["dpsl_dvsn"].ToString()).SingleOrDefault();
                    dpsl = (xDpsl == null || dr["dpsl_dvsn"].ToString() == "0") ? string.Empty : xDpsl.Field<string>("nm");

                i = dg.Rows.Add();
                dg["dg_No", i].Value = totRowCnt - startRow - i;
                dg["dg_Tid", i].Value = dr["tid"];
                dg["dg_Gid", i].Value = dr["_pid"];
                dg["dg_CS", i].Value = auctCd.FindCsNm(csCd);
                dg["dg_Dpt", i].Value = dpt;
                dg["dg_SN", i].Value = (dr["pn"].ToString() == "0") ? string.Format("{0}-{1}", dr["sn1"], dr["sn2"]) : string.Format("{0}-{1}({2})", dr["sn1"], dr["sn2"], dr["pn"]);
                dg["dg_Adrs", i].Value = dr["adrs"];
                dg["dg_BidDt", i].Value = dr["bid_dt"].ToString().Contains("0001") ? "" : string.Format("{0:yyyy.MM.dd}", dr["bid_dt"]);
                dg["dg_State", i].Value = state;
                dg["dg_Cat", i].Value = cat;
                dg["dg_Dpsl", i].Value = dpsl;
                dg["dg_FbCnt", i].Value = dr["fb_cnt"];
                dg["dg_ApslAmt", i].Value = string.Format("{0:N0}", dr["apsl_amt"]);
                dg["dg_MinbAmt", i].Value = string.Format("{0:N0}", dr["minb_amt"]);
                dg["dg_2ndDt", i].Value = dr["2nd_dt"].ToString().Contains("0001") ? string.Format("{0:yyyy.MM.dd}", dr["1st_dt"]) : string.Format("{0:yyyy.MM.dd}", dr["2nd_dt"]);
                
                if (dr["works"].ToString() == "0")
                {
                    dg.Rows[i].Cells[1].Style.BackColor = Color.DimGray;     //입력 미완료건
                    dg.Rows[i].Cells[1].Style.ForeColor = Color.WhiteSmoke;
                }

                //등기변동 조건 선택시
                if (dtpRgstMdfyDtBgn.Checked || dtpRgstMdfyDtEnd.Checked)
                {
                    sta1 = dr["sta1"].ToString();
                    if (sta1 == "10") dg.Rows[i].Cells[0].Style.BackColor = Color.LightCyan;          //예정
                    else if (sta1 == "12") dg.Rows[i].Cells[0].Style.BackColor = Color.MistyRose;     //매각
                    else if (sta1 == "14") dg.Rows[i].Cells[0].Style.BackColor = Color.Gainsboro;     //종국
                }
            }
            dr.Close();
            db.Close();

            if (chkRgst.Checked)
            {
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
            }

            dg.ClearSelection();
            this.Cursor = Cursors.Default;

            dg.SelectionChanged += dg_SelectionChanged;
        }

        /// <summary>
        /// 물건 보기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void dg_SelectionChanged(object sender, EventArgs e)
        {
            int i = 0, n = 0, apslCnt = 0;
            string sql = "", tid = "", spt = "", sn = "", sn1 = "", sn2 = "", cat0 = "", cat1 = "", cat3 = "", mergSign = "";
            //string bldgType="";
            //string carshipPtrn = "승용차|승합차|버스|화물차|기타차량|덤프트럭|굴삭기|지게차|기타중기|선박|항공기|이륜차";
            
            //ui.FormReset(tabDtl, new string[] { "cbxCrtSpt", "cbxDpt" });
            ui.FormClear(tabDtl, new string[] { "tpnlBasic", "nudShrCalPoint"});
            ui.FormClear(tabFile);
            ui.FormClear(tabSeq);

            btnOldFormDel.Visible = false;
            rdoPres0.Checked = true;
            cbxImptCtgr.SelectedIndex = 1;
            cbxImptSrc.SelectedIndex = 1;

            cbxCrtSpt.SelectedIndexChanged -= CbxCrtSpt_SelectedIndexChanged;

            dgH.CellValueChanged -= Dg_CellValueChanged;
            dgI.CellValueChanged -= Dg_CellValueChanged;
            dgL.CellValueChanged -= Dg_CellValueChanged;
            dgB.CellValueChanged -= Dg_CellValueChanged;
            dgE.CellValueChanged -= Dg_CellValueChanged;
            dgM.CellValueChanged -= Dg_CellValueChanged;
            dgC.CellValueChanged -= Dg_CellValueChanged;
            dgT.CellValueChanged -= Dg_CellValueChanged;
            dgRL.CellValueChanged -= Dg_CellValueChanged;
            dgRB.CellValueChanged -= Dg_CellValueChanged;
            lvSpc.ItemChecked -= LvSpc_ItemChecked;

            cbxCrtSpt.SelectedIndexChanged += CbxCrtSpt_SelectedIndexChanged;

            foreach (ListViewItem item in lvSpc.Items)
            {
                item.Checked = false;
                item.BackColor = Color.White;
            }
            
            nudShrCalPoint.Value = 2;

            if (dg.CurrentRow == null) return;
            i = dg.CurrentRow.Index;
            tid = dg["dg_Tid", i].Value.ToString();                        
            sql = "select * from ta_list L , ta_dtl D where L.tid=D.tid and L.tid=" + tid + " limit 1";

            if (chkCpTid.Checked)
            {
                Clipboard.SetText(tid + "_");
            }
            
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            if (!dr.HasRows)
            {
                dr.Close();
                db.Close();
                return;
            }

            this.Cursor = Cursors.WaitCursor;
            dr.Read();
            lnkTid.Text = dr["tid"].ToString();
            lblDtInfo.Text = string.Format("{0:yy.MM.dd} / {1:yy.MM.dd} / {2:yy.MM.dd}", dr["1st_dt"], (dr["2nd_dt"].ToString().Contains("0001")) ? "-" : dr["2nd_dt"], (dr["pre_dt"].ToString().Contains("0001")) ? "-" : dr["pre_dt"]);
            cat1 = dr["cat1"].ToString();
            cat3 = dr["cat3"].ToString();
            spt = dr["spt"].ToString();
            sn1 = dr["sn1"].ToString();
            sn2 = dr["sn2"].ToString();
            sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));

            //부동산 및 차량/중기/선박, 권리에 따른 Panel(Dgv) 보임/숨김            
            Panel[] pnlRE = new Panel[] { pnlLand, pnlEtc, pnlRegLand, pnlRegBldg };
            if (cat1 == "30" || cat1 == "40")
            {
                pnlCar.Enabled = true;
                foreach (Panel pnl in pnlRE)
                {
                    pnl.Enabled = false;
                }
            }
            else
            {
                pnlCar.Enabled = false;
                foreach (Panel pnl in pnlRE)
                {
                    pnl.Enabled = true;
                }
            }

            cat0 = dr["cat0"].ToString();
            if (cat0 != "0")
            {
                var xRow = dtCatCdAll.Rows.Cast<DataRow>().Where(t => t["cat3_cd"].ToString() == cat0).FirstOrDefault();
                cat0 = xRow["cat3_nm"].ToString();
            }
            else cat0 = string.Empty;
            /*
            DataView dvDpt = dtDptCd.DefaultView;            
            dvDpt.RowFilter = string.Format("spt_cd='{0}'", dr["spt"]);
            cbxDpt.DataSource = dvDpt;
            cbxDpt.DisplayMember = "dpt_nm";
            cbxDpt.ValueMember = "dpt_cd";
            cbxDpt.SelectedValue = dr["dpt"];
            */
            cbxCrtSpt.SelectedValue = dr["spt"].ToString();
            cbxDpt.SelectedValue = dr["dpt"];

            cbxSn1.Text = dr["sn1"].ToString();
            txtSn2.Text = dr["sn2"].ToString();
            txtPn.Text = dr["pn"].ToString();
            txtApslAmt.Text = string.Format("{0:#,##0}", dr["apsl_amt"]);
            lblApslAmt.Text = string.Format("{0:#,##0}", dr["apsl_amt"]);
            txtMinbAmt.Text = string.Format("{0:#,##0}", dr["minb_amt"]);
            txtSucbAmt.Text = string.Format("{0:#,##0}", dr["sucb_amt"]);
            txtBillAmt.Text = string.Format("{0:#,##0}", dr["bill_amt"]);
            txtFirstAmt.Text = string.Format("{0:#,##0}", dr["1st_amt"]);
            txtFbCnt.Text = dr["fb_cnt"].ToString();
            cbxCat3.SelectedValue = dr["cat3"];
            cbxCat3Rec.SelectedValue = dr["cat3_rec"];
            cbxRgstYn.SelectedValue = dr["rgst_yn"];
            chkMultiBldg.Checked = (dr["mbldg"].ToString() == "1") ? true : false;
            chkAllowAnyVal.Checked = (dr["allow_anyval"].ToString() == "1") ? true : false;
            lblCat0.Text = cat0;
            cbxDpstType.SelectedValue = Convert.ToDecimal(dr["dpst_type"]);
            cbxState.SelectedValue = dr["sta2"];
            cbxDpslDvsn.SelectedValue = dr["dpsl_dvsn"];
            cbxDpstRate.Text = dr["dpst_rate"].ToString();
            cbxAuctType.SelectedValue = Convert.ToDecimal(dr["auct_type"]);
            cbxFrmlType.SelectedValue = dr["frml_type"];
            cbxApslType.SelectedValue = Convert.ToDecimal(dr["apsl_type"]);
            chkWorksCplt.Checked = (dr["works"].ToString() == "1") ? true : false;
            chkWorksCplt2.Checked = (dr["works"].ToString() == "1") ? true : false;
            if (dr["works"].ToString() == "1")
            {
                chkWorksCplt.Checked = true;
                chkWorksCplt2.Checked = true;
                chkWorksCplt.BackColor = Color.Transparent;
                chkWorksCplt2.BackColor = Color.Transparent;
                chkWorksCplt.ForeColor = Color.Black;
                chkWorksCplt2.ForeColor = Color.Black;
            }
            else
            {
                chkWorksCplt.Checked = false;
                chkWorksCplt2.Checked = false;
                chkWorksCplt.BackColor = Color.DimGray;
                chkWorksCplt2.BackColor = Color.DimGray;
                chkWorksCplt.ForeColor = Color.WhiteSmoke;
                chkWorksCplt2.ForeColor = Color.WhiteSmoke;
            }

            STA1 = dr["sta1"].ToString();
            STA2 = dr["sta2"].ToString();

            mtxtBidDt.Text = (dr["bid_dt"].ToString().Contains("0001")) ? "" : dr["bid_dt"].ToString();
            cbxBidCnt.Text = dr["bid_cnt"].ToString();
            mtxtBidTm.Text = dr["bid_tm"].ToString();
            mtxtBidTm1.Text = dr["bid_tm1"].ToString();
            mtxtBidTm2.Text = dr["bid_tm2"].ToString();
            mtxtBidTm3.Text = dr["bid_tm3"].ToString();
            mtxtRcpDt.Text = (dr["rcp_dt"].ToString().Contains("0001")) ? "" : dr["rcp_dt"].ToString();
            mtxtIniDt.Text = (dr["ini_dt"].ToString().Contains("0001")) ? "" : dr["ini_dt"].ToString();
            mtxtShrDt.Text = (dr["shr_dt"].ToString().Contains("0001")) ? "" : dr["shr_dt"].ToString();
            mtxtEndDt.Text = (dr["end_dt"].ToString().Contains("0001")) ? "" : dr["end_dt"].ToString();
            mtxtSucbDt.Text = (dr["sucb_dt"].ToString().Contains("0001")) ? "" : dr["sucb_dt"].ToString();
            txtPrsvDt.Text = (dr["prsv_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["prsv_dt"]);
            txtPrsvDtRead.Text = (dr["prsv_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["prsv_dt"]);
            txtApslDt.Text = (dr["apsl_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["apsl_dt"]);

            txtApslNm.Text = dr["apsl_nm"].ToString();
            txtCreditor.Text = dr["creditor"].ToString();
            txtDebtor.Text = dr["debtor"].ToString();
            txtOwner.Text = dr["owner"].ToString();
            txtAuctNm.Text = dr["auct_nm"].ToString();            
            txtLoca.Text = dr["loca"].ToString();
            txtLandShp.Text = dr["land_shp"].ToString();
            txtAdjRoad.Text = dr["adj_road"].ToString();
            txtDiff.Text = dr["diff"].ToString();            
            txtFaci.Text = dr["faci"].ToString();            
            txtPdNote.Text = dr["pd_note"].ToString();
            txtLeasNote.Text = dr["leas_note"].ToString();
            txtEtcNote.Text = dr["etc_note"].ToString();
            txtRgstNote.Text = dr["rgst_note"].ToString();
            txtAttnNote1.Text = dr["attn_note1"].ToString();
            txtAttnNote2.Text = dr["attn_note2"].ToString();
            txtAnalyNote.Text = dr["analy_note"].ToString();

            txtLandSqm.Text = string.Format("{0:N4}", dr["land_sqm"]);
            txtLandTotSqm.Text = string.Format("{0:N4}", dr["land_tot_sqm"]);
            txtBldgSqm.Text = string.Format("{0:N4}", dr["bldg_sqm"]);
            txtBldgTotSqm.Text = string.Format("{0:N4}", dr["bldg_tot_sqm"]);
            txtRtSqm.Text = string.Format("{0:N4}", dr["rt_sqm"]);
            txtRtTotSqm.Text = string.Format("{0:N4}", dr["rt_tot_sqm"]);
            txtLandPy.Text = (Convert.ToDouble(dr["land_sqm"]) * 0.3025).ToString();
            txtBldgPy.Text = (Convert.ToDouble(dr["bldg_sqm"]) * 0.3025).ToString();

            txtLandApslAmt.Text = string.Format("{0:N0}", dr["apsl_land"]);
            txtBldgApslAmt.Text = string.Format("{0:N0}", dr["apsl_bldg"]);
            txtPresApslAmtInc.Text = string.Format("{0:N0}", dr["apsl_pres_inc"]);
            txtPresApslAmtDec.Text = string.Format("{0:N0}", dr["apsl_pres_dec"]);
            txtMachApslAmt.Text = string.Format("{0:N0}", dr["apsl_mach"]);

            txtDgLSqmSum.Text = string.Format("{0:N4}", dr["land_sqm"]);
            txtDgLTotSqmSum.Text = string.Format("{0:N4}", dr["land_tot_sqm"]);
            txtDgLAmtSum.Text = string.Format("{0:N0}", dr["apsl_land"]);
            //txtDgBSqmSum.Text = string.Format("{0:N4}", dr["bldg_sqm"]);
            //txtDgBTotSqmSum.Text = string.Format("{0:N4}", dr["bldg_tot_sqm"]);
            txtDgBAmtSum.Text = string.Format("{0:N0}", dr["apsl_bldg"]);            
            txtDgEAmtSumInc.Text = string.Format("{0:N0}", dr["apsl_pres_inc"]);
            txtDgEAmtSumDec.Text = string.Format("{0:N0}", dr["apsl_pres_dec"]);
            txtDgMAmtSum.Text = string.Format("{0:N0}", dr["apsl_mach"]);

            lblRgstPinLand.Text = dr["pin_land"].ToString();
            lblRgstPinBldg.Text = dr["pin_bldg"].ToString();

            txtAdrs.Text = dr["adrs"].ToString();
            txtLotCnt.Text = dr["lot_cnt"].ToString();
            txtHoCnt.Text = dr["ho_cnt"].ToString();
            txtCoordX.Text = dr["x"].ToString();
            txtCoordY.Text = dr["y"].ToString();
            txtSiCd.Text = dr["si_cd"].ToString();
            txtGuCd.Text = dr["gu_cd"].ToString();
            txtDnCd.Text = dr["dn_cd"].ToString();
            txtRiCd.Text = dr["ri_cd"].ToString();
            txtRegnAdrs.Text = dr["regn_adrs"].ToString();            
            txtAdrsNoM.Text = dr["m_adrs_no"].ToString();
            txtAdrsNoS.Text = dr["s_adrs_no"].ToString();
            txtRoadAdrs.Text = dr["road_adrs"].ToString();
            txtBldgNoM.Text = dr["m_bldg_no"].ToString();
            txtBldgNoS.Text = dr["s_bldg_no"].ToString();
            txtBldgNm.Text = dr["bldg_nm"].ToString();
            txtRoadNm.Text = dr["road_nm"].ToString();
            txtAptCd.Text = dr["apt_cd"].ToString();
            cbxAdrsMt.SelectedValue = Convert.ToDecimal(dr["mt"]);

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
            cbxSpRgst.SelectedValue = Convert.ToDecimal(dr["sp_rgst"]);
            dr.Close();

            //입찰 내역
            n = 0;
            sql = "select * from ta_hist where tid=" + tid + " order by seq";
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                n = dgH.Rows.Add();
                dgH["dgH_Seq", n].Value = (n + 1);
                ((DataGridViewComboBoxCell)dgH["dgH_State", n]).Value = dr["sta"];
                dgH["dgH_BidDt", n].Value = string.Format("{0:yyyy-MM-dd}", dr["bid_dt"]);
                dgH["dgH_BidTm", n].Value = dr["bid_tm"].ToString().Substring(0, 5);
                dgH["dgH_Amt", n].Value = string.Format("{0:N0}", dr["amt"]);
                dgH["dgH_BidrCnt", n].Value = string.Format("{0:N0}", dr["bidr_cnt"]);
                dgH["dgH_SucBidr", n].Value = dr["sucb_nm"];
                dgH["dgH_Area", n].Value = dr["sucb_area"];
                dgH["dgH_2ndReg", n].Value = dr["2nd_reg"];
                ((DataGridViewComboBoxCell)dgH["dgH_PriReg", n]).Value = Convert.ToDecimal(dr["pri_reg"]);
                dgH["dgH_Idx", n].Value = dr["idx"];
            }
            dr.Close();

            //목록 내역
            n = 0;
            sql = "select * from ta_ls where tid=" + tid + " order by no";
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                n = dgI.Rows.Add();
                dgI["dgI_Idx", n].Value = dr["idx"];
                dgI["dgI_LsNo", n].Value = dr["no"];
                dgI["dgI_Adrs", n].Value = dr["adrs"];
                dgI["dgI_Dvsn", n].Value = dr["dvsn"];              
                dgI["dgI_Note", n].Value = dr["note"];                
                dgI["dgI_Pnu", n].Value = dr["pnu"];
                dgI["dgI_x", n].Value = dr["x"];
                dgI["dgI_y", n].Value = dr["y"];
                dgI["dgI_ZoneNo", n].Value = dr["zone_no"];
                dgI["dgI_HjCd", n].Value = dr["hj_cd"];
                dgI["dgI_Pin", n].Value = dr["pin"];
                dgI["dgI_ExRgst", n].Value = (dr["ex_rgst"].ToString() == "1") ? "Y" : "";
                if (dr["pre_err"].ToString() == "1")
                {
                    dgI["dgI_Adrs", n].Style.BackColor = Color.LightGray;
                }
            }
            dr.Close();

            if (cat1 == "30" || cat1 == "40")
            {
                //차량/중기/선박
                n = 0;
                sql = "select * from ta_cars where tid=" + tid + " order by ls_no";
                dr = db.ExeRdr(sql);
                while (dr.Read())
                {
                    n = dgC.Rows.Add();
                    dgC["dgC_Idx", n].Value = dr["idx"];
                    dgC["dgC_LsNo", n].Value = dr["ls_no"];
                    dgC["dgC_Adrs", n].Value = dr["adrs"];
                    dgC["dgC_Nm", n].Value = dr["car_nm"];
                    dgC["dgC_CarType", n].Value = dr["car_type"];
                    dgC["dgC_RegNo", n].Value = dr["reg_no"];
                    dgC["dgC_Year", n].Value = dr["car_year"];
                    dgC["dgC_Cmpy", n].Value = dr["cmpy"];
                    ((DataGridViewComboBoxCell)dgC["dgC_Fuel", n]).Value = dr["fuel"];
                    ((DataGridViewComboBoxCell)dgC["dgC_Trans", n]).Value = dr["trans"];
                    dgC["dgC_Mtr", n].Value = dr["mtr"];
                    dgC["dgC_AprvNo", n].Value = dr["aprv_no"];
                    dgC["dgC_IdNo", n].Value = dr["id_no"];
                    dgC["dgC_Dspl", n].Value = dr["dspl"];
                    dgC["dgC_Dist", n].Value = dr["dist"];
                    dgC["dgC_Prpl", n].Value = dr["prpl"];
                    dgC["dgC_Park", n].Value = dr["park"];
                    dgC["dgC_Color", n].Value = dr["color"];
                    dgC["dgC_MfDt", n].Value = (dr["mf_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["mf_dt"]);
                    dgC["dgC_RegDt", n].Value = (dr["reg_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["reg_dt"]);
                    dgC["dgC_Term", n].Value = dr["term"];
                    dgC["dgC_Hp", n].Value = dr["hp"];
                    dgC["dgC_Rpm", n].Value = dr["rpm"];
                    dgC["dgC_Note", n].Value = dr["note"];
                    cbxCarCoCd.SelectedValue = dr["co_cd"];
                    cbxCarMoCd.SelectedValue = dr["mo_cd"];
                }                
                dr.Close();
                dgC.ClearSelection();
            }
            else
            {
                //토지 현황
                n = 0;
                sql = "select * from ta_land where tid=" + tid + " order by ls_no, idx";
                dr = db.ExeRdr(sql);
                while (dr.Read())
                {
                    n = dgL.Rows.Add();
                    dgL["dgL_No", n].Value = n + 1;
                    dgL["dgL_Idx", n].Value = dr["idx"];
                    dgL["dgL_LsNo", n].Value = dr["ls_no"];
                    ((DataGridViewComboBoxCell)dgL["dgL_Cat", n]).Value = dr["cat_cd"];
                    dgL["dgL_Sqm", n].Value = dr["sqm"];
                    dgL["dgL_TotShrSqm", n].Value = dr["tot_shr_sqm"];
                    dgL["dgL_RtSqm", n].Value = dr["rt_sqm"];
                    dgL["dgL_TotRtSqm", n].Value = dr["tot_rt_sqm"];
                    dgL["dgL_UnitPrc", n].Value = dr["unit_prc"];
                    dgL["dgL_Amt", n].Value = dr["amt"];
                    dgL["dgL_ShrStr", n].Value = dr["shr_str"];
                    dgL["dgL_PrpsNm", n].Value = dr["prps_nm"];
                    dgL["dgL_Note", n].Value = dr["note"];
                    dgL["dgL_Adrs", n].Value = dr["adrs_s"];
                }
                dr.Close();

                //건물 현황
                n = 0;
                sql = "select *, date_format(aprv_dt,'%Y-%m-%d') as aprvDt from ta_bldg where tid=" + tid + " and dvsn=1 order by ls_no, idx";
                dr = db.ExeRdr(sql);
                while (dr.Read())
                {
                    n = dgB.Rows.Add();
                    dgB["dgB_No", n].Value = n + 1;
                    dgB["dgB_Idx", n].Value = dr["idx"];
                    dgB["dgB_LsNo", n].Value = dr["ls_no"];
                    dgB["dgB_TotFlr", n].Value = dr["tot_flr"];
                    ((DataGridViewComboBoxCell)dgB["dgB_Flr", n]).Value = Convert.ToUInt16(dr["flr"]);
                    dgB["dgB_Sqm", n].Value = dr["sqm"];
                    dgB["dgB_TotShrSqm", n].Value = dr["tot_shr_sqm"];
                    dgB["dgB_ActlSqm", n].Value = dr["actl_sqm"];
                    dgB["dgB_UnitPrc", n].Value = dr["unit_prc"];
                    dgB["dgB_Amt", n].Value = dr["amt"];
                    dgB["dgB_ShrStr", n].Value = dr["shr_str"];
                    dgB["dgB_State", n].Value = dr["state"];
                    dgB["dgB_Struct", n].Value = dr["struct"];
                    //dgB["dgB_AprvDt", n].Value = (dr["aprv_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["aprv_dt"]);
                    dgB["dgB_AprvDt", n].Value = dr["aprvDt"];
                    dgB["dgB_ElvtCnt", n].Value = dr["elvt"];
                    dgB["dgB_Note", n].Value = dr["note"];
                    dgB["dgB_Adrs", n].Value = dr["adrs_s"];
                }
                dr.Close();

                //제시외 건물/수목
                //dgE.CellValueChanged -= DgE_CellValueChanged;
                n = 0;
                sql = "select * from ta_bldg where tid=" + tid + " and dvsn=2 order by ls_no";
                dr = db.ExeRdr(sql);
                while (dr.Read())
                {
                    n = dgE.Rows.Add();
                    dgE["dgE_No", n].Value = n + 1;
                    dgE["dgE_Idx", n].Value = dr["idx"];
                    dgE["dgE_LsNo", n].Value = dr["ls_no"];
                    ((DataGridViewComboBoxCell)dgE["dgE_Flr", n]).Value = Convert.ToUInt16(dr["flr"]);
                    dgE["dgE_Sqm", n].Value = dr["sqm"];
                    dgE["dgE_TotShrSqm", n].Value = dr["tot_shr_sqm"];
                    dgE["dgE_ActlSqm", n].Value = dr["actl_sqm"];
                    dgE["dgE_UnitPrc", n].Value = dr["unit_prc"];
                    dgE["dgE_Amt", n].Value = dr["amt"];
                    dgE["dgE_ShrStr", n].Value = dr["shr_str"];
                    dgE["dgE_State", n].Value = dr["state"];
                    dgE["dgE_Struct", n].Value = dr["struct"];
                    dgE["dgE_Note", n].Value = dr["note"];
                    ((DataGridViewComboBoxCell)dgE["dgE_Inc", n]).Value = Convert.ToByte(dr["inc"]);
                    dgE["dgE_Adrs", n].Value = dr["adrs_s"];
                }
                dr.Close();
                //dgE.CellValueChanged += DgE_CellValueChanged;

                //기계, 기구
                n = 0;
                sql = "select * from ta_bldg where tid=" + tid + " and dvsn=3";
                dr = db.ExeRdr(sql);
                while (dr.Read())
                {
                    n = dgM.Rows.Add();
                    dgM["dgM_Idx", n].Value = dr["idx"];
                    dgM["dgM_Nm", n].Value = dr["state"];
                    dgM["dgM_Amt", n].Value = dr["amt"];                    
                    dgM["dgM_Note", n].Value = dr["note"];
                    dgM["dgM_Adrs", n].Value = dr["adrs_s"];
                }
                dr.Close();
            }

            //임차인 현황(매물작업시 정렬 점유자순으로)
            n = 0;
            //sql = "select *, date_format(mv_dt,'%Y-%m-%d') as mvDt, date_format(fx_dt,'%Y-%m-%d') as fxDt, date_format(shr_dt,'%Y-%m-%d') as shrDt from ta_leas where tid=" + tid + " order by ls_no, prsn";
            sql = "select *, date_format(mv_dt,'%Y-%m-%d') as mvDt, date_format(fx_dt,'%Y-%m-%d') as fxDt, date_format(shr_dt,'%Y-%m-%d') as shrDt from ta_leas where tid=" + tid + " order by idx";
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                n = dgT.Rows.Add();
                dgT["dgT_No", n].Value = n + 1;
                dgT["dgT_Idx", n].Value = dr["idx"];
                dgT["dgT_LsNo", n].Value = dr["ls_no"];
                dgT["dgT_Prsn", n].Value = dr["prsn"];
                dgT["dgT_InvType", n].Value = dr["inv_type"];
                dgT["dgT_Part", n].Value = dr["part"];                
                ((DataGridViewComboBoxCell)dgT["dgT_UseCd", n]).Value = dr["use_cd"];
                dgT["dgT_Term", n].Value = dr["term"];
                dgT["dgT_ShopNm", n].Value = dr["shop_nm"];
                dgT["dgT_Deposit", n].Value = dr["deposit"];
                dgT["dgT_MMoney", n].Value = dr["m_money"];
                dgT["dgT_TMoney", n].Value = dr["t_money"];
                dgT["dgT_TMnth", n].Value = dr["t_mnth"];
                dgT["dgT_ChkBiz", n].Value = dr["biz"];
                dgT["dgT_Note", n].Value = dr["note"];
                dgT["dgT_MvDt", n].Value = dr["mvDt"];
                dgT["dgT_FxDt", n].Value = dr["fxDt"];
                dgT["dgT_ShrDt", n].Value = dr["shrDt"];
                dgT["dgT_Hide", n].Value = dr["hide"];
                if (dr["hide"].ToString() == "1") dgT.Rows[n].DefaultCellStyle.BackColor = Color.LightGray;    //숨김
            }
            dr.Close();

            //토지 등기
            n = 0;
            sql = "select * from ta_rgst where tid=" + tid + " and rg_dvsn=1 order by rc_dt, rc_no";
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                n = dgRL.Rows.Add();
                dgRL["dgRL_Idx", n].Value = dr["idx"];
                ((DataGridViewComboBoxCell)dgRL["dgRL_Sect", n]).Value = dr["sect"].ToString();
                dgRL["dgRL_Rank", n].Value = (dr["rank_s"].ToString() != "0") ? string.Format("{0}-{1}", dr["rank"], dr["rank_s"]) : dr["rank"];
                dgRL["dgRL_RcDt", n].Value = (dr["rc_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["rc_dt"]);
                dgRL["dgRL_EKey", n].Value = dr["ekey"];
                dgRL["dgRL_RgNm", n].Value = dr["rg_nm"];
                dgRL["dgRL_RcNo", n].Value = dr["rc_no"];
                dgRL["dgRL_CAmt", n].Value = string.Format("{0:N0}", dr["c_amt"]);
                ((DataGridViewComboBoxCell)dgRL["dgRL_Take", n]).Value = dr["take"];
                dgRL["dgRL_Prsn", n].Value = dr["prsn"];
                dgRL["dgRL_RgNo", n].Value = dr["rg_no"];
                dgRL["dgRL_MvDt", n].Value = (dr["mv_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["mv_dt"]);
                dgRL["dgRL_FxDt", n].Value = (dr["fx_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["fx_dt"]);
                dgRL["dgRL_BgnDt", n].Value = (dr["bgn_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["bgn_dt"]);
                dgRL["dgRL_EndDt", n].Value = (dr["end_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["end_dt"]);
                dgRL["dgRL_REno", n].Value = dr["r_eno"];
                dgRL["dgRL_Aply", n].Value = dr["aply"];
                dgRL["dgRL_BAmt", n].Value = string.Format("{0:N0}", dr["b_amt"]);
                dgRL["dgRL_Note", n].Value = dr["note"];
                dgRL["dgRL_Adrs", n].Value = dr["adrs"];
                dgRL["dgRL_Brch", n].Value = dr["brch"];
                dgRL["dgRL_Hide", n].Value = dr["hide"];
                ((DataGridViewComboBoxCell)dgRL["dgRL_RgCd", n]).Value = dr["rg_cd"];                
                if (dr["take"].ToString() == "1") dgRL.Rows[n].DefaultCellStyle.BackColor = Color.MistyRose;    //인수(수동체크)
                if (dr["hide"].ToString() == "1") dgRL.Rows[n].DefaultCellStyle.BackColor = Color.LightGray;    //등기추출에서 숨김처리된 등기항목
                if ((dr["rg_cd"].ToString() == "4" || dr["rg_cd"].ToString() == "5") && (dr["note"].ToString().Contains(txtSn2.Text) || dr["r_eno"].ToString().Contains(txtSn2.Text)))     //임의경매 또는 강제경매 && 해당 사건번호 포함
                {
                    dgRL.Rows[n].DefaultCellStyle.BackColor = Color.PeachPuff;
                }
                if (dr["ekey"].ToString() == "1") dgRL.Rows[n].DefaultCellStyle.BackColor = Color.LightBlue;    //말소기준권리
            }
            dr.Close();
            
            //건물 등기
            n = 0;
            sql = "select * from ta_rgst where tid=" + tid + " and rg_dvsn in (2,3) order by rc_dt, rc_no";
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                n = dgRB.Rows.Add();
                dgRB["dgRB_Idx", n].Value = dr["idx"];
                ((DataGridViewComboBoxCell)dgRB["dgRB_Sect", n]).Value = dr["sect"].ToString();
                dgRB["dgRB_Rank", n].Value = (dr["rank_s"].ToString() != "0") ? string.Format("{0}-{1}", dr["rank"], dr["rank_s"]) : dr["rank"];
                dgRB["dgRB_RcDt", n].Value = string.Format("{0:yyyy-MM-dd}", dr["rc_dt"]);
                dgRB["dgRB_EKey", n].Value = dr["ekey"];
                dgRB["dgRB_RgNm", n].Value = dr["rg_nm"];
                dgRB["dgRB_RcNo", n].Value = dr["rc_no"];
                dgRB["dgRB_CAmt", n].Value = string.Format("{0:N0}", dr["c_amt"]);
                ((DataGridViewComboBoxCell)dgRB["dgRB_Take", n]).Value = dr["take"];
                dgRB["dgRB_Prsn", n].Value = dr["prsn"];
                dgRB["dgRB_RgNo", n].Value = dr["rg_no"];
                dgRB["dgRB_MvDt", n].Value = (dr["mv_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["mv_dt"]);
                dgRB["dgRB_FxDt", n].Value = (dr["fx_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["fx_dt"]);
                dgRB["dgRB_BgnDt", n].Value = (dr["bgn_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["bgn_dt"]);
                dgRB["dgRB_EndDt", n].Value = (dr["end_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["end_dt"]);
                dgRB["dgRB_REno", n].Value = dr["r_eno"];
                dgRB["dgRB_Aply", n].Value = dr["aply"];
                dgRB["dgRB_BAmt", n].Value = string.Format("{0:N0}", dr["b_amt"]);
                dgRB["dgRB_Note", n].Value = dr["note"];
                dgRB["dgRB_Adrs", n].Value = dr["adrs"];
                dgRB["dgRB_Brch", n].Value = dr["brch"];
                dgRB["dgRB_Hide", n].Value = dr["hide"];
                ((DataGridViewComboBoxCell)dgRB["dgRB_RgCd", n]).Value = dr["rg_cd"];                
                if (dr["take"].ToString() == "1") dgRB.Rows[n].DefaultCellStyle.BackColor = Color.MistyRose;    //인수(수동체크)
                if (dr["hide"].ToString() == "1") dgRB.Rows[n].DefaultCellStyle.BackColor = Color.LightGray;    //등기추출에서 숨김처리된 등기항목
                if ((dr["rg_cd"].ToString() == "4" || dr["rg_cd"].ToString() == "5") && (dr["note"].ToString().Contains(txtSn2.Text) || dr["r_eno"].ToString().Contains(txtSn2.Text)))     //임의경매 또는 강제경매
                {
                    dgRB.Rows[n].DefaultCellStyle.BackColor = Color.PeachPuff;
                }
                if (dr["ekey"].ToString() == "1") dgRB.Rows[n].DefaultCellStyle.BackColor = Color.LightBlue;    //말소기준권리
                if (n == 1)
                {
                    if (dr["rg_dvsn"].ToString() == "2") rdoRgstDvsn2.Checked = true;
                    else if (dr["rg_dvsn"].ToString() == "3") rdoRgstDvsn3.Checked = true;
                }
            }
            dr.Close();

            //관련사건내역
            sql = "select * from ta_rcase where spt='" + spt + "' and sn1='" + sn1 + "' and sn2='" + sn2 + "'";
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                n = dgRC.Rows.Add();
                dgRC["dgRC_No", n].Value = n + 1;                
                dgRC["dgRC_CrtNm", n].Value = dr["crt_nm"];
                dgRC["dgRC_CaseNo", n].Value = dr["case_no"];
                dgRC["dgRC_Dvsn", n].Value = dr["dvsn"];
                dgRC["dgRC_Wdt", n].Value = string.Format("{0:yyyy-MM-dd}", dr["wdt"]);
                dgRC["dgRC_Idx", n].Value = dr["idx"];
                dgRC["dgRC_Hide", n].Value = (Convert.ToBoolean(dr["hide"])) ? "보임" : "숨김";
                dgRC.Rows[n].DefaultCellStyle.BackColor = (Convert.ToBoolean(dr["hide"])) ? Color.LightGray : Color.White;
            }
            dr.Close();
            dgRC.ClearSelection();

            //중복/병합 사건
            sql = "select * from ta_merg where spt='" + spt + "' and (mno='" + sn + "' or cno='" + sn + "')";
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                n = dgMR.Rows.Add();
                dgMR["dgMR_No", n].Value = n + 1;
                dgMR["dgMR_MNo", n].Value = string.Format("{0}타경 {1}", dr["mno"].ToString().Substring(0, 4), dr["mno"].ToString().Substring(4));
                dgMR["dgMR_CNo", n].Value = string.Format("{0}타경 {1}", dr["cno"].ToString().Substring(0, 4), dr["cno"].ToString().Substring(4));
                dgMR["dgMR_Dvsn", n].Value = (dr["dvsn"].ToString() == "1") ? "병합" : "중복";
                dgMR["dgMR_Wdt", n].Value = (dr["wdt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy.MM.dd}", dr["wdt"]);
                dgMR["dgMR_Idx", n].Value = dr["idx"];
                dgMR["dgMR_Hide", n].Value = (Convert.ToBoolean(dr["hide"])) ? "보임" : "숨김";
                dgMR.Rows[n].DefaultCellStyle.BackColor = (Convert.ToBoolean(dr["hide"])) ? Color.LightGray : Color.White;
                mergSign = "Y";
            }
            dr.Close();
            lblMgSign.BackColor = (mergSign == "Y") ? Color.Coral : Color.LightGray;
            dgMR.ClearSelection();

            //관리비 체납내역
            sql = "select * from db_tank.tx_arrears where tid=" + tid + " and dvsn=1";
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

            //회차 정보
            if (chkSeqInfo.Checked)
            {
                sql = "select * from ta_seq where tid=" + tid + " order by seq";
                dr = db.ExeRdr(sql);
                while (dr.Read())
                {
                    n = dgSeq.Rows.Add();
                    dgSeq["dgSeq_Idx", n].Value = dr["idx"];
                    dgSeq["dgSeq_Seq", n].Value = dr["seq"];
                    dgSeq["dgSeq_BidDt", n].Value = (dr["bid_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy.MM.dd}", dr["bid_dt"]);
                    dgSeq["dgSeq_BidTm", n].Value = dr["bid_tm"];
                    dgSeq["dgSeq_MinbAmt", n].Value = string.Format("{0:N0}", dr["minb_amt"]);
                    dgSeq["dgSeq_Wdt", n].Value = string.Format("{0:yyyy-MM-dd}", dr["wdt"]);
                }
                dr.Close();
            }

            //구폼(텍스트형식)있는지 체크
            sql = "select * from ta_old_form where tid=" + tid;
            if (db.ExistRow(sql))
            {
                btnOldFormDel.Visible = true;
            }

            db.Close();

            lblMSign.BackColor = (multiBldgArr.Contains(Convert.ToDecimal(cbxCat3.SelectedValue)) || chkMultiBldg.Checked) ? Color.Green : Color.LightGray;

            //물건 주요 변동내역
            LoadImptHist(tid);

            //파일 정보
            LoadFileInfo();

            dgL.ClearSelection();
            dgB.ClearSelection();
            dgE.ClearSelection();
            dgT.ClearSelection();
            dgRL.ClearSelection();
            dgRB.ClearSelection();
            dgRC.ClearSelection();
            dgMR.ClearSelection();
            dgRB.ClearSelection();            
            dgSeq.ClearSelection();

            dgH.CellValueChanged += Dg_CellValueChanged;
            dgI.CellValueChanged += Dg_CellValueChanged;
            dgL.CellValueChanged += Dg_CellValueChanged;
            dgB.CellValueChanged += Dg_CellValueChanged;
            dgE.CellValueChanged += Dg_CellValueChanged;
            dgM.CellValueChanged += Dg_CellValueChanged;
            dgC.CellValueChanged += Dg_CellValueChanged;
            dgT.CellValueChanged += Dg_CellValueChanged;
            dgRL.CellValueChanged += Dg_CellValueChanged;
            dgRB.CellValueChanged += Dg_CellValueChanged;

            //lvSpc.ItemChecked += LvSpc_ItemChecked;
            this.Cursor = Cursors.Default;
        }

        /// <summary>
        /// 파일 정보
        /// </summary>
        private void LoadFileInfo()
        {
            int i = 0, n = 0, apslCnt = 0;
            string tid, tbl, spt, sn1, sn2, sn, sql, landRgstWDt = "", bldgRgstWdt = "";
            DateTime cmpDt = DateTime.Now.AddDays(-7);

            dgF.Rows.Clear();
            cbxApslDocCnt.Items.Clear();
            lblLandRgstWdt.Text = String.Empty;
            lblBldgRgstWdt.Text = String.Empty;
            lblLandRgstWdt.BackColor = SystemColors.Control;
            lblBldgRgstWdt.BackColor = SystemColors.Control;

            tid = lnkTid.Text;
            sn1 = cbxSn1.Text;
            sn2 = txtSn2.Text;
            spt = cbxCrtSpt.SelectedValue.ToString();
            sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));
            tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
            sql = "select * from " + tbl + " where tid=" + tid + " or (spt=" + spt + " and sn='" + sn + "' and tid=0) order by ctgr";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
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

                //등기부등본 파일등록일
                if (dr["ctgr"].ToString() == "DA")
                {
                    landRgstWDt = $"{dr["wdt"]:yyyy.MM.dd}";
                    lblLandRgstWdt.Text = landRgstWDt;
                    if (dr["wdt"].ToString().Contains("0001") == false)
                    {
                        if (Convert.ToDateTime(dr["wdt"]) >= cmpDt && (dtpRgstMdfyDtBgn.Checked || dtpRgstMdfyDtEnd.Checked))
                        {
                            lblLandRgstWdt.BackColor = Color.Orange;
                        }
                    }
                }
                if (dr["ctgr"].ToString() == "DB")
                {
                    bldgRgstWdt = $"{dr["wdt"]:yyyy.MM.dd}";
                    lblBldgRgstWdt.Text = bldgRgstWdt;
                    if (dr["wdt"].ToString().Contains("0001") == false)
                    {
                        if (Convert.ToDateTime(dr["wdt"]) >= cmpDt && (dtpRgstMdfyDtBgn.Checked || dtpRgstMdfyDtEnd.Checked))
                        {
                            lblBldgRgstWdt.BackColor = Color.Orange;
                        }
                    }
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

        private void CbxCrtSpt_SelectedIndexChanged(object sender, EventArgs e)
        {
            string spt = "0";

            if (cbxCrtSpt.SelectedIndex > 0)
            {
                spt = cbxCrtSpt.SelectedValue.ToString();
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

        /// <summary>
        /// DataGridView 값이 변경된 셀 배경색 바꿈
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Dg_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            DataGridView dgv = (DataGridView)sender;
            dgv[e.ColumnIndex, e.RowIndex].Style.BackColor = Color.PaleGreen;

            if (dgv == dgE)
            {
                if (dgE.Columns[e.ColumnIndex].Name == "dgE_Inc")
                {
                    Sum_SqmAmt("dgE");
                }
            }
        }
        
        /// <summary>
        /// 지분 계산기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnShrCal_Click(object sender, EventArgs e)
        {
            string str = "", frtn = "", roundTxt = "";
            decimal inputVal = 0, nt = 0, dt = 0, shrSqm = 0, rsltVal = 0;
            int decPoint = 0, mdNum = 0;

            txtShrCalArea.Text = txtShrCalArea.Text.Trim();
            txtShrCalStr.Text = txtShrCalStr.Text.Trim();
            if (txtShrCalArea.Text == string.Empty || txtShrCalStr.Text == string.Empty)
            {
                MessageBox.Show("면적과 지분문자열을 입력 해 주세요.");
                return;
            }

            inputVal = Convert.ToDecimal(txtShrCalArea.Text);
            if (cbxShrCalUnit.SelectedIndex == 1)
            {
                inputVal = PyngToSqm(inputVal);
            }

            str = txtShrCalStr.Text.Trim();
            decPoint = Convert.ToInt32(nudShrCalPoint.Value);
            roundTxt = cbxShrCalRound.Text;

            List<string> lsPtrn = new List<string>();
            lsPtrn.Add(@"(\d+[\.\d]*)[ ]*분의[ ]*(\d+[\.\d]*)");
            lsPtrn.Add(@"(\d+[\.\d]*)/(\d+[\.\d]*)");

            MatchCollection mc;
            if (Regex.IsMatch(str, lsPtrn[0]))
            {
                mc = Regex.Matches(str, lsPtrn[0]);
                if (mc.Count == 1)
                {
                    nt = Convert.ToDecimal(mc[0].Groups[2].Value);
                    dt = Convert.ToDecimal(mc[0].Groups[1].Value);
                }
            }
            else if (Regex.IsMatch(str, lsPtrn[1]))
            {
                mc = Regex.Matches(str, lsPtrn[1]);
                if (mc.Count == 1)
                {
                    nt = Convert.ToDecimal(mc[0].Groups[1].Value);
                    dt = Convert.ToDecimal(mc[0].Groups[2].Value);
                }
            }
            if (nt > 0 && dt > 0)
            {
                frtn = string.Format("{0}/{1}", nt, dt);
                shrSqm = inputVal * nt / dt;
            }

            if (decPoint == 0)
            {
                if (roundTxt == "반올림") rsltVal = Math.Round(shrSqm);
                else if (roundTxt == "올림") rsltVal = Math.Ceiling(shrSqm);
                else rsltVal = Math.Floor(shrSqm);
            }
            else
            {
                mdNum = Convert.ToInt32("1".PadRight((decPoint + 1), '0'));
                if (roundTxt == "반올림") rsltVal = Math.Round(shrSqm, decPoint);
                else if (roundTxt == "올림") rsltVal = Math.Ceiling(shrSqm * mdNum) / mdNum;
                else rsltVal = Math.Floor(shrSqm * mdNum) / mdNum;
            }

            //txtShrCalRslt.Text = rsltVal.ToString();
            txtShrCalRslt.Text = string.Format("{0:N4}", rsltVal);
        }

        /// <summary>
        /// 지분 계산기-리셋
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnShrCalRst_Click(object sender, EventArgs e)
        {
            txtShrCalArea.Text = string.Empty;
            txtShrCalStr.Text = string.Empty;
            txtShrCalRslt.Text = string.Empty;
        }

        /// <summary>
        /// 단위 환산-평,홉,작/정,단,무,보 -> ㎡
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOldAreaCal_Click(object sender, EventArgs e)
        {
            string landUnitPtrn1 = @"([\d.,]+)평[ ]*((\d+)홉)*[ ]*((\d+)작)*[ ]*((\d+)재)*";  //평홉작재(1-평, 3-홉, 5-작, 7-재)
            string landUnitPtrn2 = @"([\d.,]+)정[ ]*((\d+)단)*[ ]*((\d+)무)*[ ]*(\d+)*보";    //정단무보(1-정, 3-단, 5-무, 6-보)
            double sqm = 0, phj = 0, jdm = 0;
            int decPoint = 0;
            string str;
            
            txtSqmRslt.Text = string.Empty;
            str = txtOldAreaStr.Text.Trim();
            str = str.Replace(",", string.Empty);
            decPoint = Convert.ToInt32(nudOldAreaCalPoint.Value);
            if (str == string.Empty)
            {
                MessageBox.Show("환산할 면적문자열(평홉작재/정단무보)을 입력 해 주세요.");
                return;
            }

            Match m1 = Regex.Match(str, landUnitPtrn1, rxOptM);
            Match m2 = Regex.Match(str, landUnitPtrn2, rxOptM);

            if (m1.Success)
            {
                phj = Convert.ToDouble(string.IsNullOrEmpty(m1.Groups[1].Value) ? "0" : m1.Groups[1].Value) +
                    (Convert.ToDouble(string.IsNullOrEmpty(m1.Groups[3].Value) ? "0" : m1.Groups[3].Value) * 0.1) +
                    (Convert.ToDouble(string.IsNullOrEmpty(m1.Groups[5].Value) ? "0" : m1.Groups[5].Value) * 0.01) +
                    (Convert.ToDouble(string.IsNullOrEmpty(m1.Groups[7].Value) ? "0" : m1.Groups[7].Value) * 0.001);

                if (phj > 0)
                {
                    //sqm = phj * Convert.ToDouble(3.3058);
                    sqm = phj * Convert.ToDouble(3.305785);
                }
            }
            else if (m2.Success)
            {
                jdm = (Convert.ToDouble(string.IsNullOrEmpty(m2.Groups[1].Value) ? "0" : m2.Groups[1].Value) * 3000) +
                        (Convert.ToDouble(string.IsNullOrEmpty(m2.Groups[3].Value) ? "0" : m2.Groups[3].Value) * 300) +
                        (Convert.ToDouble(string.IsNullOrEmpty(m2.Groups[5].Value) ? "0" : m2.Groups[5].Value) * 30) +
                        (Convert.ToDouble(string.IsNullOrEmpty(m2.Groups[6].Value) ? "0" : m2.Groups[6].Value) * 1);
                if (jdm > 0)
                {
                    //sqm = jdm * Convert.ToDouble(3.3058);
                    sqm = jdm * Convert.ToDouble(3.305785);
                }
            }
            else
            {
                MessageBox.Show("입력한 면적문자열(평홉작/정단무보)의 형식이 올바르지 않습니다.\r\n다시 확인 해 주세요.");
                return;
            }

            txtSqmRslt.Text = (Math.Round(sqm, decPoint)).ToString();
        }

        /// <summary>
        /// 단위 환산-리셋
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOldAreaRst_Click(object sender, EventArgs e)
        {
            txtOldAreaStr.Text = string.Empty;
            txtSqmRslt.Text = string.Empty;
        }

        /// <summary>
        /// 좌표/주소 코드 재매칭
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCoord_Click(object sender, EventArgs e)
        {
            string adrs;
            /*
            adrs = txtAdrs.Text.Trim();
            if (adrs == string.Empty)
            {
                MessageBox.Show("소재지를 입력 해 주세요");
                return;
            }

            IDictionary<string, string> dict = new Dictionary<string, string>();
            dict = api.DaumSrchAdrs(adrs);
            if (dict["x"] == string.Empty)
            {
                MessageBox.Show("좌표 정보를 찾을 수 없습니다.");
                return;
            }
            else
            {
                txtCoordX.Text = dict["x"];
                txtCoordY.Text = dict["y"];
                txtSiCd.Text = dict["sidoCd"];
                txtGuCd.Text = dict["gugunCd"];
                txtDnCd.Text = dict["dongCd"];
                txtRiCd.Text = dict["riCd"];
            } 
            */
            if (lnkTid.Text == "TID" || lnkTid.Text == string.Empty) return;

            sfMap sfMap = new sfMap() { Owner = this };
            sfMap.StartPosition = FormStartPosition.CenterScreen;
            //sfMap.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            sfMap.FormBorderStyle = FormBorderStyle.Sizable;
            sfMap.ShowDialog();
            sfMap.Dispose();
        }

        /// <summary>
        /// 날짜 형식 변환
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private string getDateParse(string str, string cellNm = null)
        {
            string dt = string.Empty;

            str = str.Replace(" ", string.Empty).Trim();

            Match m = Regex.Match(str, @"(\d{4})[.년/\-](\d+)[.월/\-](\d+)[.일]*", rxOptM);
            if (m.Success)
            {
                dt = string.Format("{0}-{1}-{2}", m.Groups[1].Value, m.Groups[2].Value.PadLeft(2, '0'), m.Groups[3].Value.PadLeft(2, '0'));
            }
            else
            {
                if (str.Length == 8)
                {
                    dt = string.Format("{0}-{1}-{2}", str.Substring(0, 4), str.Substring(4, 2), str.Substring(6, 2));
                }
                else if (str.Length == 6)
                {
                    dt = string.Format("20{0}-{1}-{2}", str.Substring(0, 2), str.Substring(2, 2), str.Substring(4, 2));
                }
            }

            if (!string.IsNullOrEmpty(cellNm))
            {
                if (str == "1") dt = "0000-00-01";
                else if (str == "3") dt = "0000-00-03";
            }
            
            return dt;
        }

        /// <summary>
        /// 시간 형식 변환
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private string getTimeParse(string str)
        {
            string tm = string.Empty;
            int strLen = 0;

            str = str.Replace(" ", string.Empty).Trim();
            strLen = str.Length;

            if (strLen == 2)
            {
                tm = string.Format("{0}:00:00", str);
            }
            else if (strLen == 4)
            {
                tm = string.Format("{0}:{1}:00", str.Substring(0, 2), str.Substring(2, 2));
            }
            else
            {
                Match m = Regex.Match(str, @"(\d{2}):(\d{2})", rxOptM);
                tm = string.Format("{0}:{1}:00", m.Groups[1].Value, m.Groups[2].Value);
            }

            return tm;
        }

        /// <summary>
        /// 집합건물 형태표시
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void chkMultiBldg_CheckedChanged(object sender, EventArgs e)
        {
            lblMSign.BackColor = (multiBldgArr.Contains(Convert.ToDecimal(cbxCat3.SelectedValue)) || chkMultiBldg.Checked) ? Color.Green : Color.LightGray;
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
        /// 탱크 링크-물건 상세창
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
            
            tbcL.SelectedTab = tabWbr1;
            //url = myWeb + "ca/caView.php?tid=" + lnkTid.Text;
            //wbr1.Document.Cookie = TankCook;
            //net.Nvgt(wbr1, url);
            
            url = "/ca/caView.php?tid=" + lnkTid.Text;
            net.TankWebView(wbr1, url);
        }

        /// <summary>
        /// 법원 링크-사건 항목별 웹페이지
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkCA_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string lnkTxt = "", url = "", jiwonNm = "", saNo = "", maemulSer = "";

            lnkTxt = ((LinkLabel)sender).Text;
            jiwonNm = auctCd.FindLawNm(cbxCrtSpt.SelectedValue.ToString(), true);
            saNo = string.Format("{0}0130{1}", cbxSn1.Text, txtSn2.Text.PadLeft(6, '0'));
            maemulSer = (txtPn.Text == "0") ? "1" : txtPn.Text;

            if (lnkTxt == "사건내역" || lnkTxt == "사건") url = "RetrieveRealEstDetailInqSaList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&srnID=PNO101005&_SRCH_SRNID=PNO101005";
            else if (lnkTxt == "기일내역" || lnkTxt == "기일") url = "RetrieveRealEstSaDetailInqGiilList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
            else if (lnkTxt == "문건/송달" || lnkTxt == "문건") url = "RetrieveRealEstSaDetailInqMungunSongdalList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
            else if (lnkTxt == "물건상세" || lnkTxt == "상세") url = "RetrieveRealEstCarHvyMachineMulDetailInfo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&maemulSer=" + maemulSer;
            else if (lnkTxt == "현황조사" || lnkTxt == "현황") url = "RetrieveRealEstSaHjosa.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo;
            else if (lnkTxt == "표시목록" || lnkTxt == "목록") url = "RetrieveRealEstHjosaDispMokrok.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo;
            else if (lnkTxt == "매각공고" || lnkTxt == "공고") url = "RetrieveRealEstMgakNotifySrchGyulgwa.laf?ipchalGbnCd=000331&jiwonNm=" + jiwonNm + "&maeGiil=" + mtxtBidDt.Text + "&jpDeptCd=" + cbxDpt.SelectedValue.ToString();
            else if (lnkTxt == "매물H") url = "RetrieveRealEstMgakMulMseo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&maemulSer=" + maemulSer;

            //MessageBox.Show(url);
            //Process.Start("IExplore.exe", url);
            url = "http://www.courtauction.go.kr/" + url;
            if (lnkTxt == "매각공고")
            {
                //tbcR.SelectedTab = tabWbr3;
                //net.Nvgt(wbr3, url);
                Process.Start("IExplore.exe", url);
            }
            else
            {
                tbcL.SelectedTab = tabWbr1;                
                net.Nvgt(wbr1, url);
            }            
        }

        /// <summary>
        /// 탱크 링크-내부 저장된 파일 보기(문서)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkTK_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string lnkTxt, url, spt, tid, sn, tbl, sql, idx="", ctgr = "";

            lnkTxt = ((LinkLabel)sender).Text;

            if (lnkTxt == "사건") ctgr = "AA";
            else if (lnkTxt == "기일") ctgr = "AB";
            else if (lnkTxt == "문건") ctgr = "AC";
            else if (lnkTxt == "현황조사") ctgr = "AD";
            else if (lnkTxt == "상세") ctgr = "AJ";
            else if (lnkTxt == "건축") ctgr = "EC";
            else if (lnkTxt == "토지등기") ctgr = "DA";
            else if (lnkTxt == "건물등기") ctgr = "DB";

            tid = lnkTid.Text;            
            sn = string.Format("{0}{1}", cbxSn1.Text, txtSn2.Text.PadLeft(6, '0'));
            spt = cbxCrtSpt.SelectedValue.ToString();
            tbl = (Convert.ToDecimal(cbxSn1.Text) > 2004) ? "ta_f" + cbxSn1.Text : "ta_f2004";

            sql = "select idx from " + tbl + " where (tid=" + tid + " or (spt=" + spt + " and sn='" + sn + "' and tid=0)) and ctgr='" + ctgr + "' limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            if (dr.HasRows)
            {
                dr.Read();
                idx = dr["idx"].ToString();
            }
            dr.Close();
            db.Close();

            if (idx == "")
            {
                if (lnkTxt == "건축")
                {
                    MessageBox.Show("저장된 [" + lnkTxt + "] 파일이 없습니다.");
                }
                else
                {
                    MessageBox.Show("저장된 [" + lnkTxt + "] 파일이 없어 법원으로 이동 합니다.");
                    LinkLabel lnkCA = this.Controls.Find((sender as LinkLabel).Name.Replace("TK", "CA"), true)[0] as LinkLabel;
                    lnkCA_LinkClicked(lnkCA, null);
                }
                return;
            }

            tbcL.SelectedTab = tabWbr2;
            url = string.Format(myWeb + "SOLAR/caFileViewer.php?tid={0}&idx={1}", tid, idx);
            wbr2.Navigate(url);
        }

        /// <summary>
        /// 탱크 링크-내부 저장 공고파일 보기-1
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkTK_NtIntra_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            int no = 0;
            string sql, dth;
            Regex rx = new Regex(@"(\d{4})(\d{2})(\d{2})(\d{2}).html");

            dgNt.Rows.Clear();
            wbr4.Navigate("about:blank");

            sql = "select * from ta_fnoti where spt='" + cbxCrtSpt.SelectedValue.ToString() + "' and dpt='" + cbxDpt.SelectedValue.ToString() + "' and bid_dt='" + mtxtBidDt.Text + "' order by idx desc";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                no++;
                Match m = rx.Match(dr["file"].ToString());
                dth = string.Format("{0}.{1}.{2} {3}시", m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value);
                dgNt.Rows.Add(no, dth, dr["idx"]);
            }
            dr.Close();
            db.Close();            

            tbcL.SelectedTab = tabNtIntra;
            dgNt.ClearSelection();
        }

        /// <summary>
        /// 탱크 링크-내부 저장 공고파일 보기-2
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgNt_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            string idx, url;

            idx = dgNt["dgNt_Idx", e.RowIndex].Value.ToString();
            url = string.Format(myWeb + "SOLAR/caFileNtViewer.php?idx={0}", idx);
            wbr4.Navigate(url);
        }

        private void tbcNote_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tbcNote.SelectedIndex == 0 || tbcNote.SelectedIndex == 1)
            {
                lvSpc.ItemChecked -= LvSpc_ItemChecked;
            }
            else
            {
                lvSpc.ItemChecked += LvSpc_ItemChecked;
            }
        }

        /// <summary>
        /// 단어/기호 복사
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lblCpWord_Click(object sender, EventArgs e)
        {
            Label lbl = sender as Label;
            Clipboard.SetText(lbl.Text);            
        }

        /// <summary>
        /// 물건복사-대상물건 검색
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCpSrch_Click(object sender, EventArgs e)
        {
            int i = -1;
            string sql, state;

            dgCp.Rows.Clear();

            if (lnkTid.Text == string.Empty) return;

            //sql = "select tid, sn1, sn2, pn from ta_list where spt=" + cbxCrtSpt.SelectedValue.ToString() + " and sn1=" + cbxSn1.Text + " and sn2=" + txtSn2.Text + " and pn !=" + txtPn.Text + " and sta1=11 order by pn";
            sql = "select tid, sn1, sn2, pn, sta2 from ta_list where spt=" + cbxCrtSpt.SelectedValue.ToString() + " and sn1=" + cbxSn1.Text + " and sn2=" + txtSn2.Text + " and pn !=" + txtPn.Text + " and sta1 in (11,13) order by pn";     //2021-12-15 민영이 요청
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
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
        /// 물건복사-내용 복사 처리
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCpPrc_Click(object sender, EventArgs e)
        {
            string chkTxt, sql, tid, cvp1 = "", cvp2 = "", cvp3 = "";
            string cat1, cat2, cat3, totFlr, flr, state, aprvDt;
            string rgDvsn = "", rank, rankSub, rgCd, rgNo, rcNo, lsNo;       //등기관련
            int dgTCnt = 0, dgRLCnt = 0, dgRBCnt = 0;
            List<string> ls1 = new List<string>();
            List<string> ls2 = new List<string>();
            List<string> ls3 = new List<string>();
            dgTCnt = dgT.Rows.Count - 1;
            dgRLCnt = dgRL.Rows.Count - 1;
            dgRBCnt = dgRB.Rows.Count - 1;

            foreach (Control ctrl in gbxCp1.Controls)
            {
                CheckBox chk = ctrl as CheckBox;
                if (chk.Checked == false) continue;
                chkTxt = chk.Text;
                if (chkTxt == "물건종별")
                {
                    cat3 = cbxCat3.SelectedValue.ToString();
                    cat2 = cat3.Substring(0, 4);
                    cat1 = cat3.Substring(0, 2);
                    ls1.Add("cat1=" + cat1 + ", cat2=" + cat2 + ", cat3=" + cat3);
                }
                else if (chkTxt == "매각구분") ls1.Add("dpsl_dvsn=" + cbxDpslDvsn.SelectedValue.ToString());
                else if (chkTxt == "채권자") ls1.Add("creditor='" + txtCreditor.Text.Trim() + "'");
                else if (chkTxt == "채무자") ls1.Add("debtor='" + txtDebtor.Text.Trim() + "'");
                else if (chkTxt == "소유자") ls1.Add("owner='" + txtOwner.Text.Trim() + "'");
                else if (chkTxt == "공무상종별") ls1.Add("cat3_rec=" + cbxCat3Rec.SelectedValue.ToString());
                else if (chkTxt == "필지수") ls1.Add("lot_cnt='" + txtLotCnt.Text.Trim() + "'");
                else if (chkTxt == "배당종기") ls1.Add("shr_dt='" + getDateParse(mtxtShrDt.Text) + "'");
                else if (chkTxt == "토지-별도등기") ls1.Add("sp_rgst='" + cbxSpRgst.SelectedValue.ToString() + "'");

                else if (chkTxt == "감정원") ls2.Add("apsl_nm='" + txtApslNm.Text.Trim().Replace("\\", string.Empty).Replace("'", "\\'") + "'");
                else if (chkTxt == "가격시점") ls2.Add("apsl_dt='" + getDateParse(txtApslDt.Text) + "'");
                else if (chkTxt == "일괄비준") ls2.Add("apsl_type=" + cbxApslType.SelectedValue.ToString());
                else if (chkTxt == "보존등기") ls2.Add("prsv_dt='" + getDateParse(txtPrsvDtRead.Text) + "'");
            }

            foreach (Control ctrl in gbxCp2.Controls)
            {
                CheckBox chk = ctrl as CheckBox;
                if (chk.Checked == false) continue;
                chkTxt = chk.Text;
                if (chkTxt == "위치/환경") ls2.Add("loca='" + txtLoca.Text.Trim().Replace("\\", string.Empty).Replace("'", "\\'") + "'");
                else if (chkTxt == "이용상태") ls2.Add("land_shp='" + txtLandShp.Text.Trim().Replace("\\", string.Empty).Replace("'", "\\'") + "'");
                else if (chkTxt == "도로상태") ls2.Add("adj_road='" + txtAdjRoad.Text.Trim().Replace("\\", string.Empty).Replace("'", "\\'") + "'");
                else if (chkTxt == "참고사항") ls2.Add("etc_note='" + txtEtcNote.Text.Trim().Replace("\\", string.Empty).Replace("'", "\\'") + "'");
                else if (chkTxt == "기타주의-1") ls2.Add("attn_note1='" + txtAttnNote1.Text.Trim().Replace("\\", string.Empty).Replace("'", "\\'") + "'");
                else if (chkTxt == "기타주의-2") ls2.Add("attn_note2='" + txtAttnNote2.Text.Trim().Replace("\\", string.Empty).Replace("'", "\\'") + "'");
                else if (chkTxt == "설비내역") ls2.Add("faci='" + txtFaci.Text.Trim().Replace("\\", string.Empty).Replace("'", "\\'") + "'");
                else if (chkTxt == "공부와의 차이") ls2.Add("diff='" + txtDiff.Text.Trim().Replace("\\", string.Empty).Replace("'", "\\'") + "'");
                else if (chkTxt == "임차인기타") ls2.Add("leas_note='" + txtLeasNote.Text.Trim().Replace("\\", string.Empty).Replace("'", "\\'") + "'");
                else if (chkTxt == "등기부권리관계") ls2.Add("rgst_note='" + txtRgstNote.Text.Trim().Replace("\\", string.Empty).Replace("'", "\\'") + "'");
            }

            if (dgB.Rows.Count > 1)
            {
                DataGridViewRow row = dgB.Rows[0];
                totFlr = row.Cells["dgB_TotFlr"].Value?.ToString() ?? "";
                flr = row.Cells["dgB_Flr"].Value?.ToString() ?? "";
                state = row.Cells["dgB_State"].Value?.ToString() ?? "";
                aprvDt = getDateParse(row.Cells["dgB_AprvDt"].Value?.ToString() ?? "");
                foreach (Control ctrl in gbxCp3.Controls)
                {
                    CheckBox chk = ctrl as CheckBox;
                    if (chk.Checked == false) continue;
                    chkTxt = chk.Text;
                    if (chkTxt == "총층수") ls3.Add("tot_flr='" + totFlr + "'");
                    else if (chkTxt == "층수") ls3.Add("flr='" + flr + "'");
                    else if (chkTxt == "현황") ls3.Add("state='" + state + "'");
                    else if (chkTxt == "사용승인") ls3.Add("aprv_dt='" + aprvDt + "'");
                }
            }
            
            if (ls1.Count > 0)
            {
                cvp1 = string.Join(", ", ls1.ToArray());                
            }

            if (ls2.Count > 0)
            {
                cvp2 = string.Join(", ", ls2.ToArray());
            }

            if (ls3.Count > 0)
            {
                cvp3 = string.Join(", ", ls3.ToArray());
            }

            if (cvp1 == string.Empty && cvp2 == string.Empty && cvp3 == string.Empty && chkCpLeas.Checked == false && chkCpRL.Checked == false && chkCpRB.Checked == false && chkCpImpt.Checked == false)
            {
                MessageBox.Show("복사할 항목을 선택 해 주세요.");
                return;
            }

            if (dgCp.Rows.Count == 0)
            {
                MessageBox.Show("[복사대상] 물건을 검색 해 주세요.");
                return;
            }

            if (string.Format("{0}-{1}", cbxSn1.Text, txtSn2.Text) != dgCp["dgCp_Sn", 0].Value.ToString())
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

            if (MessageBox.Show("선택한 항목을 " + chkRows.Count().ToString() + " 개의 물건으로 내용복사 하시겠습니까?", "내용 복사", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            db.Open();
            foreach (DataGridViewRow row in chkRows)
            {
                tid = row.Cells["dgCp_Tid"].Value.ToString();
                if (cvp1 != string.Empty)
                {
                    sql = "update ta_list set " + cvp1 + " where tid=" + tid;
                    db.ExeQry(sql);
                }

                if (cvp2 != string.Empty)
                {
                    sql = "update ta_dtl set " + cvp2 + " where tid=" + tid;
                    db.ExeQry(sql);
                }

                if (cvp3 != string.Empty)
                {
                    sql = "update ta_bldg set " + cvp3 + " where tid=" + tid + " and dvsn=1";
                    db.ExeQry(sql);
                }

                row.Cells["dgCp_Note"].Value = "복사 완료";
                Application.DoEvents();
            }
            db.Close();

            //임차인/토지/건물등기 복사
            List<MySqlParameter> sp = new List<MySqlParameter>();
            foreach (DataGridViewRow chkRow in chkRows)
            {
                tid = chkRow.Cells["dgCp_Tid"].Value.ToString();
                if (chkCpLeas.Checked)
                {
                    db.Open();
                    sql = "delete from ta_leas where tid=" + tid;
                    db.ExeQry(sql);
                    foreach (DataGridViewRow row in dgT.Rows)
                    {
                        if (row.Index == dgTCnt) break;
                        lsNo = row.Cells["dgT_LsNo"].Value.ToString();                        
                        sql = "insert into ta_leas set tid=@tid, ls_no=@ls_no, prsn=@prsn, inv_type=@inv_type, part=@part, use_cd=@use_cd, shop_nm=@shop_nm, term=@term, deposit=@deposite, m_money=@m_money, t_money=@t_money, t_mnth=@t_mnth, mv_dt=@mv_dt, fx_dt=@fx_dt, shr_dt=@shr_dt, biz=@biz, note=@note, hide=@hide";                        
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@ls_no", lsNo));
                        sp.Add(new MySqlParameter("@prsn", row.Cells["dgT_Prsn"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@inv_type", row.Cells["dgT_InvType"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@part", row.Cells["dgT_Part"].Value?.ToString() ?? ""));                        
                        sp.Add(new MySqlParameter("@use_cd", row.Cells["dgT_UseCd"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@shop_nm", row.Cells["dgT_ShopNm"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@term", row.Cells["dgT_Term"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@deposite", row.Cells["dgT_Deposit"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@m_money", row.Cells["dgT_MMoney"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@t_money", row.Cells["dgT_TMoney"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@t_mnth", row.Cells["dgT_TMnth"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@mv_dt", getDateParse(row.Cells["dgT_MvDt"].Value?.ToString() ?? "", "dgT_MvDt")));
                        sp.Add(new MySqlParameter("@fx_dt", getDateParse(row.Cells["dgT_FxDt"].Value?.ToString() ?? "", "dgT_FxDt")));
                        sp.Add(new MySqlParameter("@shr_dt", getDateParse(row.Cells["dgT_ShrDt"].Value?.ToString() ?? "", "dgT_ShrDt")));
                        sp.Add(new MySqlParameter("@note", row.Cells["dgT_Note"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@biz", ((DataGridViewCheckBoxCell)row.Cells["dgT_ChkBiz"]).Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@hide", ((DataGridViewCheckBoxCell)row.Cells["dgT_Hide"]).Value?.ToString() ?? ""));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                    db.Close();
                }
                if (chkCpRL.Checked)
                {
                    db.Open();
                    sql = "delete from ta_rgst where tid=" + tid + " and rg_dvsn=1";
                    db.ExeQry(sql);
                    foreach (DataGridViewRow row in dgRL.Rows)
                    {
                        if (row.Index == dgRLCnt) break;
                        rank = row.Cells["dgRL_Rank"].Value.ToString();
                        if (rank.Contains("-"))
                        {
                            Match match = Regex.Match(rank, @"(\d+)\-(\d+)", rxOptM);
                            rank = match.Groups[1].Value;
                            rankSub = match.Groups[2].Value;
                        }
                        else rankSub = "0";

                        rgCd = row.Cells["dgRL_RgCd"].Value?.ToString() ?? string.Empty;
                        rgNo = row.Cells["dgRL_RgNo"].Value?.ToString() ?? string.Empty;
                        rcNo = row.Cells["dgRL_RcNo"].Value?.ToString() ?? string.Empty;
                        rgNo = Regex.Replace(rgNo, @"[\-\*]+", string.Empty);
                        rcNo = Regex.Replace(rcNo, @"[제호]", string.Empty);

                        sql = "insert into ta_rgst set tid=@tid, rg_dvsn=@rg_dvsn, sect=@sect, rank=@rank, rank_s=@rank_s, rg_cd=@rg_cd, rg_nm=@rg_nm, rc_dt=@rc_dt, rc_no=@rc_no, b_amt=@b_amt, c_amt=@c_amt, prsn=@prsn, rg_no=@rg_no, mv_dt=@mv_dt, fx_dt=@fx_dt, bgn_dt=@bgn_dt, end_dt=@end_dt, ";
                        sql += "r_eno=@r_eno, aply=@aply, ekey=@ekey, take=@take, note=@note, adrs=@adrs, brch=@brch, hide=@hide";                        
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@rg_dvsn", "1"));
                        sp.Add(new MySqlParameter("@sect", row.Cells["dgRL_Sect"].Value?.ToString() ?? string.Empty));
                        sp.Add(new MySqlParameter("@rank", rank));
                        sp.Add(new MySqlParameter("@rank_s", rankSub));
                        sp.Add(new MySqlParameter("@rg_cd", rgCd));
                        sp.Add(new MySqlParameter("@b_amt", row.Cells["dgRL_BAmt"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@c_amt", row.Cells["dgRL_CAmt"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@rg_nm", ReNamePrsn(row.Cells["dgRL_RgNm"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@rc_no", rcNo));
                        sp.Add(new MySqlParameter("@prsn", ReNamePrsn(row.Cells["dgRL_Prsn"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@rg_no", rgNo));
                        sp.Add(new MySqlParameter("@rc_dt", getDateParse(row.Cells["dgRL_RcDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@mv_dt", getDateParse(row.Cells["dgRL_MvDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@fx_dt", getDateParse(row.Cells["dgRL_FxDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@bgn_dt", getDateParse(row.Cells["dgRL_BgnDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@end_dt", getDateParse(row.Cells["dgRL_EndDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@r_eno", row.Cells["dgRL_REno"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@note", ReNamePrsn(row.Cells["dgRL_Note"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@adrs", row.Cells["dgRL_Adrs"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@brch", row.Cells["dgRL_Brch"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@aply", ((DataGridViewCheckBoxCell)row.Cells["dgRL_Aply"]).Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@ekey", ((DataGridViewCheckBoxCell)row.Cells["dgRL_EKey"]).Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@take", ((DataGridViewComboBoxCell)row.Cells["dgRL_Take"]).Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@hide", ((DataGridViewCheckBoxCell)row.Cells["dgRL_Hide"]).Value?.ToString() ?? ""));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                    db.Close();
                }
                if (chkCpRB.Checked)
                {
                    db.Open();
                    if (rdoRgstDvsn2.Checked) rgDvsn = "2";
                    else rgDvsn = "3";
                    sql = "delete from ta_rgst where tid=" + tid + " and rg_dvsn=" + rgDvsn;
                    db.ExeQry(sql);                    
                    foreach (DataGridViewRow row in dgRB.Rows)
                    {
                        if (row.Index == dgRBCnt) break;
                        rank = row.Cells["dgRB_Rank"].Value.ToString();
                        if (rank.Contains("-"))
                        {
                            Match match = Regex.Match(rank, @"(\d+)\-(\d+)", rxOptM);
                            rank = match.Groups[1].Value;
                            rankSub = match.Groups[2].Value;
                        }
                        else rankSub = "0";

                        rgCd = row.Cells["dgRB_RgCd"].Value?.ToString() ?? string.Empty;
                        rgNo = row.Cells["dgRB_RgNo"].Value?.ToString() ?? string.Empty;
                        rcNo = row.Cells["dgRB_RcNo"].Value?.ToString() ?? string.Empty;
                        rgNo = Regex.Replace(rgNo, @"[\-\*]+", string.Empty);
                        rcNo = Regex.Replace(rcNo, @"[제호]", string.Empty);

                        sql = "insert into ta_rgst set tid=@tid, rg_dvsn=@rg_dvsn, sect=@sect, rank=@rank, rank_s=@rank_s, rg_cd=@rg_cd, rg_nm=@rg_nm, rc_dt=@rc_dt, rc_no=@rc_no, b_amt=@b_amt, c_amt=@c_amt, prsn=@prsn, rg_no=@rg_no, mv_dt=@mv_dt, fx_dt=@fx_dt, bgn_dt=@bgn_dt, end_dt=@end_dt, ";
                        sql += "r_eno=@r_eno, aply=@aply, ekey=@ekey, take=@take, note=@note, adrs=@adrs, brch=@brch, hide=@hide";
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@rg_dvsn", rgDvsn));
                        sp.Add(new MySqlParameter("@sect", row.Cells["dgRB_Sect"].Value?.ToString() ?? string.Empty));
                        sp.Add(new MySqlParameter("@rank", rank));
                        sp.Add(new MySqlParameter("@rank_s", rankSub));
                        sp.Add(new MySqlParameter("@rg_cd", rgCd));
                        sp.Add(new MySqlParameter("@b_amt", row.Cells["dgRB_BAmt"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@c_amt", row.Cells["dgRB_CAmt"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@rg_nm", ReNamePrsn(row.Cells["dgRB_RgNm"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@rc_no", rcNo));
                        sp.Add(new MySqlParameter("@prsn", ReNamePrsn(row.Cells["dgRB_Prsn"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@rg_no", rgNo));
                        sp.Add(new MySqlParameter("@rc_dt", getDateParse(row.Cells["dgRB_RcDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@mv_dt", getDateParse(row.Cells["dgRB_MvDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@fx_dt", getDateParse(row.Cells["dgRB_FxDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@bgn_dt", getDateParse(row.Cells["dgRB_BgnDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@end_dt", getDateParse(row.Cells["dgRB_EndDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@r_eno", row.Cells["dgRB_REno"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@note", ReNamePrsn(row.Cells["dgRB_Note"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@adrs", row.Cells["dgRB_Adrs"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@brch", row.Cells["dgRB_Brch"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@aply", ((DataGridViewCheckBoxCell)row.Cells["dgRB_Aply"]).Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@ekey", ((DataGridViewCheckBoxCell)row.Cells["dgRB_EKey"]).Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@take", ((DataGridViewComboBoxCell)row.Cells["dgRB_Take"]).Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@hide", ((DataGridViewCheckBoxCell)row.Cells["dgRB_Hide"]).Value?.ToString() ?? ""));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                    db.Close();
                }
                if (chkCpImpt.Checked)
                {
                    DataTable dtImpt = db.ExeDt("select * from ta_impt_rec where tid='" + lnkTid.Text + "' order by idx desc limit 1");
                    if (dtImpt.Rows.Count == 0)
                    {
                        MessageBox.Show("현 사건에 등록된 주요변동내역이 없습니다.");
                        return;
                    }
                    DataRow dr = dtImpt.Rows[0];
                    db.Open();
                    sql = "insert into ta_impt_rec set tid=@tid, ctgr=@ctgr, src=@src, note=@note, wdt=curdate()";
                    sp.Add(new MySqlParameter("@tid", tid));
                    sp.Add(new MySqlParameter("@ctgr", dr["ctgr"]));
                    sp.Add(new MySqlParameter("@src", dr["src"]));
                    sp.Add(new MySqlParameter("@note", dr["note"]));
                    db.ExeQry(sql, sp);
                    sp.Clear();
                    db.Close();
                }

                chkRow.Cells["dgCp_Note"].Value = "복사 완료";
                Application.DoEvents();
            }
            
            MessageBox.Show("복사 처리 완료");
        }

        /// <summary>
        /// 물건복사-그룹 전체 선택/해제
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void chkCpAll_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox chkAll = sender as CheckBox;
            bool chkFlag = (chkAll.Checked) ? true : false;

            GroupBox gbx = chkAll.Parent as GroupBox;
            
            foreach (Control ctrl in gbx.Controls)
            {
                CheckBox chk = ctrl as CheckBox;
                chk.Checked = chkFlag;
            }
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

            spt = cbxCrtSpt.SelectedValue.ToString();
            tid = lnkTid.Text;
            sn1 = cbxSn1.Text;
            sn2 = txtSn2.Text;
            sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));            
            seqNo = "01";
            tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
            ctgr = (rdoCpRgstLand.Checked) ? "DA" : "DB";

            sql = $"select * from {tbl} where tid={tid} and ctgr='{ctgr}' limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            if (dr.HasRows)
            {
                dr.Read();
                rmtFile = $"{dr["ctgr"]}/{dr["spt"]}/{sn1}/{dr["file"]}";
                locFile = $@"{rgstDnPath}\{dr["file"]}";
                dnRslt = ftp1.Download(rmtFile, locFile, true);
                //if (dnRslt) MessageBox.Show("ok");
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

            if (string.Format("{0}-{1}", cbxSn1.Text, txtSn2.Text) != dgCp["dgCp_Sn", 0].Value.ToString())
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

        /// <summary>
        /// 주변환경/참고사항/임차인 등 글정리
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkTxtClean_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string content;
            
            LinkLabel lnk = sender as LinkLabel;
            TextBox[] tbxs;
            if (lnk == lnkTxtClean1)
            {
                tbxs = new TextBox[] { txtLoca, txtLandShp, txtAdjRoad, txtEtcNote, txtFaci, txtDiff, txtPdNote };
            }
            else
            {
                tbxs = new TextBox[] { txtLeasNote };
            }

            foreach (TextBox tbx in tbxs)
            {
                content = tbx.Text.Trim();
                if (content == string.Empty) continue;

                if (content == Regex.Match(content, @"[\-\.]+").Value)
                {
                    content = string.Empty;
                }
                //content = Regex.Replace(content, @"^[A-Zⓐⓑⓒⓓⓔⓕⓖⓗ①②③④⑤⑥⑦⑧⑨○\-](\.)*[ ]*|\t|^본건은[ ]*|^[가-하]\.[ ]*|^[1-9]\.[ ]*", string.Empty, rxOptM);
                MatchCollection mc = Regex.Matches(content, @"(^[A-Z\W][\. ]*?)[^동호 ]", rxOptM);
                foreach (Match match in mc)
                {
                    content = content.Replace(match.Groups[1].Value, string.Empty).Trim();
                }
                
                content = Regex.Replace(content, @"\t|^본건은[ ]*|^[가-하]\.[ ]*|^[1-9]\.[ ]*", string.Empty, rxOptM);
                content = Regex.Replace(content, @"[습읍]니다", "음");
                content = Regex.Replace(content, @"입니다", "임");
                content = Regex.Replace(content, @"합니다", "함");
                content = Regex.Replace(content, @"됩니다", "됨");
                content = Regex.Replace(content, @"바랍니다", "바람");

                content = Regex.Replace(content, @"[ ]*(첨부|별첨|별지)[ ]*(된)*[ ]*사진과[ ]*같이[ ]*", " ");
                content = Regex.Replace(content, @"[ ]*(첨부|별첨)[ ]*된[ ]*", " ");
                content = Regex.Replace(content, @"[ ]*첨부[ ]+", " ");
                content = Regex.Replace(content, @"[ ]*별지[ ]*첨부*[ ]*사진과[ ]*같이[ ]*", " ");
                content = Regex.Replace(content, @"[ ]*별지[ ]*조사[ ]*된[ ]*", " ");
                content = Regex.Replace(content, @"[ ]*별지와[ ]*같(이|은)[ ]*", " ");
                content = Regex.Replace(content, @"[ ]*별지[ ]*", " ");
                content = Regex.Replace(content, @"[ ]*별첨과[ ]*같이[ ]*", " ");
                content = Regex.Replace(content, @"[ ]*별첨[ ]*", " ");
                content = Regex.Replace(content, @"[ ]*덧붙인[ ]*", " ");
                content = Regex.Replace(content, @"[ ]*덧붙임[ ]+", " ");
                content = Regex.Replace(content, @"\(별지\)", string.Empty);

                content = "* " + content;
                content = Regex.Replace(content, @"\r\n", "\r\n* ");
                content = Regex.Replace(content, @"[ ]{2,}", string.Empty);
                content = Regex.Replace(content, @"(\* ){2,}", "* ");
                content = content.Replace("없 음", "없음");
                if (content.Trim() == "*") content = string.Empty;
                content = content.Trim();                
                tbx.Text = content;
            }
        }

        /// <summary>
        /// 특수조건 멘트 삽입
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LvSpc_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            string itemNm, apndStr = "";

            if (!e.Item.Checked) return;

            itemNm = e.Item.Text;
            itemNm = Regex.Replace(itemNm, @"\d+.", string.Empty);
            if (itemNm == "맹지") return;

            if (itemNm.Contains("유치권") || itemNm.Contains("법정지상권") || itemNm.Contains("분묘기지권"))
            {
                if (itemNm == "유치권") apndStr = "유치권 여지 있음";
                else if (itemNm == "유치권배제") apndStr = "유치권 배제 신청";
                else if (itemNm == "법정지상권") apndStr = "법정지상권";
                else if (itemNm == "분묘기지권") apndStr = "분묘기지권";
                txtAttnNote1.AppendText("\r\n▶[" + apndStr + "]");
            }
            else if (itemNm == "위반건축물")
            {
                apndStr = "건축물대장상위반건축물임";
                txtEtcNote.AppendText("\r\n▶[" + apndStr + "]");
            }
            else
            {
                if (itemNm == "공유자우선매수") apndStr = "공유자우선매수";
                else if (itemNm == "농지취득자격증명") apndStr = "농지취득자격증명";
                else if (itemNm == "채권자매수청구") apndStr = "채권자매수신고";
                else if (itemNm == "대위변제") apndStr = "대위변제";
                else if (itemNm == "항고사건") apndStr = "항고접수";
                else if (itemNm == "임금채권자") apndStr = "임금채권자";
                else if (itemNm == "임차인우선매수신고") apndStr = "임차인우선매수신고";
                txtAttnNote2.AppendText("\r\n▶[" + apndStr + "]");
            }
        }

        /// <summary>
        /// 등기 행(Row) 복사
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRgstCopy_Click(object sender, EventArgs e)
        {
            int n = 0;
            string colNm;
            Button btn = (Button)sender;
            DataGridView dgvS = (btn.Name.Contains("btnRL")) ? dgRL : dgRB;     //Source
            DataGridView dgvT = (btn.Name.Contains("btnRL")) ? dgRB : dgRL;     //Target
            DataGridViewSelectedRowCollection rows = dgvS.SelectedRows;
            
            if (rows.Count == 0)
            {
                MessageBox.Show("복사할 행을 먼저 선택 해 주세요");
                return;
            }

            if (btn.Name.Contains("Cp1"))
            {
                foreach (DataGridViewRow row in rows.Cast<DataGridViewRow>().Reverse())
                {
                    n = dgvS.Rows.Add();
                    foreach (DataGridViewColumn col in dgvS.Columns)
                    {
                        colNm = col.Name;
                        dgvS[col.Name, n].Value = (colNm.Contains("Idx")) ? string.Empty : row.Cells[colNm].Value;
                    }
                    dgvS.Rows[n].DefaultCellStyle.BackColor = Color.AliceBlue;
                }
                if (dgvS.Rows[n].Displayed == false) dgvS.FirstDisplayedScrollingRowIndex = n;
            }
            else
            {
                foreach (DataGridViewRow row in rows.Cast<DataGridViewRow>().Reverse())
                {
                    n = dgvT.Rows.Add();
                    foreach (DataGridViewColumn col in dgvT.Columns)
                    {
                        colNm = (col.Name.Contains("RL")) ? col.Name.Replace("RL", "RB") : col.Name.Replace("RB", "RL");
                        dgvT[col.Name, n].Value = (colNm.Contains("Idx")) ? string.Empty : row.Cells[colNm].Value;
                    }
                    dgvT.Rows[n].DefaultCellStyle.BackColor = Color.AliceBlue;
                }
                if (dgvT.Rows[n].Displayed == false) dgvT.FirstDisplayedScrollingRowIndex = n;
            }
        }

        /// <summary>
        /// 토지/건물-목록내역 50건 이상 해석
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnk50Analy_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            decimal i = 0, pgCnt = 0, findCnt = 0, ndCnt = 0;
            string tid, sql, url, html, jiwonNm, saNo, maemulSer;

            if (MessageBox.Show("[목록내역50+] 현황 추출을 하시겠습니까?", "대량 현황추출", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            this.Cursor = Cursors.WaitCursor;

            tid = lnkTid.Text;
            jiwonNm = auctCd.LawNmEnc(csCd: cbxCrtSpt.SelectedValue.ToString());
            saNo = string.Format("{0}0130{1}", cbxSn1.Text, txtSn2.Text.Trim().PadLeft(6, '0'));
            maemulSer = (txtPn.Text == string.Empty || txtPn.Text == "0") ? "1" : txtPn.Text.Trim();
            url = "https://www.courtauction.go.kr/RetrieveRealEstMulDetailInfoMokrokList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&maemulSer=" + maemulSer + "&page=default40";
            net.Nvgt(wbr2, url);            
            html = net.GetHtml(url);
            if (html.Contains("검색결과가 없습니다"))
            {
                this.Cursor = Cursors.Default;
                MessageBox.Show("검색결과가 없습니다");                
                return;
            }

            MatchCollection mc = Regex.Matches(html, @"goPage\('(\d+)'\)", rxOptM);
            if (mc.Count > 0) pgCnt = Math.Ceiling(Convert.ToDecimal(mc[mc.Count - 1].Groups[1].Value) / 40);
            else pgCnt = 1;

            db.Open();
            sql = "delete from ta_land where tid=" + tid;
            db.ExeQry(sql);

            sql = "delete from ta_bldg where tid=" + tid;
            db.ExeQry(sql);
            db.Close();

            HAPDoc doc = new HAPDoc();
            for (i = 1; i <= pgCnt; i++)
            {
                if (i > 1)
                {
                    webCnt++;
                    if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                    html = net.GetHtml(url + "&targetRow=" + ((i - 1) * 40 + 1).ToString());
                    if (html.Contains("검색결과가 없습니다")) continue;
                }
                doc.LoadHtml(html);
                PrcDtlSub_LandBldg(doc);
            }

            this.Cursor = Cursors.Default;

            dg_SelectionChanged(null, null);
        }

        /// <summary>
        /// 감정평가서, 매물명세서, 등기부등본, 세대열람 보기(PDF 문서)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkPdflDoc_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            int docNo = 0;
            string lnkSrc = "", url, html, tid, sql, spt, sn, sn1, sn2, jiwonNm, saNo, pn, maemulSer, maeGiil, jpDeptCd, fileNm;
            //axAcroPDF1.LoadFile(@"D:\xxx.pdf");
            LinkLabel lnkLbl = sender as LinkLabel;
            string lnkTxt = lnkLbl.Text;
            string lnkNm = lnkLbl.Name;
            tid = lnkTid.Text;

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

            if (lnkLbl == lnkCA_Stmt)
            {
                url = "http://www.courtauction.go.kr/RetrieveMobileEstMgakMulMseo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&ordHoi=&orgSaNo=" + saNo + "&maemulSer=" + maemulSer + "&maeGiil=" + maeGiil + "&mGakMulMseoYN=Y&corCanYN=N&jpDeptCd=" + jpDeptCd;
                html = net.GetHtml(url);
                Regex rx = new Regex(@"downMaemulMyungDoc\('(.*)?'\)", rxOptM);
                Match match = rx.Match(html);
                if (match.Success)
                {
                    lnkSrc = match.Groups[1].Value;
                    axAcroPDF1.src = lnkSrc;
                }
                else
                {
                    MessageBox.Show("[법원-매각물건명세서]가 없습니다.");
                    return;
                }
            }
            else if (lnkLbl == lnkCA_Apsl)
            {
                List<string> listSeq = new List<string>();
                url = "http://www.courtauction.go.kr/RetrieveMobileEstSaGamEvalSeo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&orgSaNo=" + saNo + "&maemulSer=" + pn + "&maeGiil=" + maeGiil + "&mGakMulMseoYN=Y&corCanYN=N&jpDeptCd=" + jpDeptCd;
                html = net.GetHtml(url);
                if (html.Contains("잘못된 접근"))
                {
                    MessageBox.Show("법원메시지-잘못된 접근");
                    return;
                }
                HAPDoc doc = new HAPDoc();
                doc.LoadHtml(html);
                HtmlNodeCollection ncSeq = doc.DocumentNode.SelectNodes("//*[@id='idOrdHoi']/option");
                foreach (HtmlNode ndSeq in ncSeq)
                {
                    listSeq.Add(ndSeq.InnerText.Trim());
                }
                listSeq.Reverse();
                if (listSeq.Count == 1)
                {
                    Match match = Regex.Match(html, @"downGamEvalSeo\('(.*)?'\)", rxOptM);
                    if (match.Success == false)
                    {
                        MessageBox.Show("법원 매칭오류-1");
                        return;
                    }
                    url = match.Groups[1].Value;
                    html = net.GetHtml(url);
                    match = Regex.Match(html, @"'\/(.*)?'", RegexOptions.Multiline);
                    if (match.Success == false)
                    {
                        MessageBox.Show("법원 매칭오류-2");
                        return;
                    }
                    url = match.Groups[1].Value;
                    lnkSrc = @"http://ca.kapanet.or.kr/" + url;
                    axAcroPDF1.src = lnkSrc;
                }
                else
                {
                    //
                }
            }
            else if (lnkLbl == lnkTK_Stmt || lnkLbl == lnkTK_Stmt2)
            {
                string tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
                //sql = "select * from " + tbl + " where (tid=" + tid + " or (spt=" + spt + " and sn='" + sn + "' and tid=0)) and ctgr='AG' limit 1";
                sql = "select * from " + tbl + " where (tid=" + tid + " or (spt=" + spt + " and sn='" + sn + "' and tid=0)) and ctgr='AG' order by wdt desc limit 1";     //임시처리-구파일 5T 일괄 후 중복건 미처리로 임시, 처리 후 윗줄 사용
                db.Open();
                dr = db.ExeRdr(sql);
                if (dr.HasRows)
                {
                    dr.Read();
                    url = string.Format(myWeb + "FILE/CA/AG/{0}/{1}/{2}", spt, sn1, dr["file"]);
                    axAcroPDF1.src = url;
                }
                else
                {
                    MessageBox.Show("저장된 [매각물건 명세서] 파일이 없습니다.");
                }
                dr.Close();
                db.Close();
            }
            else if (lnkLbl == lnkTK_Apsl || lnkLbl == lnkTK_ApslOcr)
            {
                sql = "select * from ta_list where tid=" + tid + " limit 1";
                db.Open();
                dr = db.ExeRdr(sql);
                dr.Read();
                spt = dr["spt"].ToString();
                sn1 = dr["sn1"].ToString();
                sn2 = dr["sn2"].ToString();
                dr.Close();
                db.Close();

                string tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
                sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));
                sql = "select * from " + tbl + " where spt=" + spt + " and sn='" + sn + "' and ctgr IN ('AF','EI') order by idx";
                DataTable dt = db.ExeDt(sql);

                if (dt.Select("ctgr='AF'").Count() == 0)
                {
                    MessageBox.Show("수집된 감정평가서가 없습니다.");
                    return;
                }

                docNo = Convert.ToInt32(cbxApslDocCnt.Text) - 1;
                fileNm = dt.Rows[docNo]["file"].ToString();
                url = string.Format(myWeb + "FILE/CA/AF/{0}/{1}/{2}", spt, sn1, fileNm);
                if (lnkLbl == lnkTK_Apsl)
                {
                    axAcroPDF1.src = url;                    
                }
                else
                {
                    wbr5.Navigate(url);
                    fileNm = fileNm.Replace("AF", "EI");
                    fileNm = fileNm.Replace("pdf", "html");
                    DataRow[] rows = dt.Select("file='" + fileNm + "'");
                    if (rows.Count() > 0)
                    {
                        url = string.Format(myWeb + "SOLAR/caFileViewer.php?tid={0}&idx={1}", tid, rows[0]["idx"]);
                        wbrOcr.Navigate(url);
                    }
                    else
                    {
                        wbrOcr.Navigate("about:blank");
                    }
                }
            }
            else if (lnkLbl == lnkTK_LandRgst || lnkLbl == lnkTK_LandRgst_R)
            {
                string tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
                //sql = "select * from " + tbl + " where (tid=" + tid + " or (spt=" + spt + " and sn='" + sn + "' and tid=0)) and ctgr='DA' limit 1";
                sql = "select * from " + tbl + " where tid=" + tid + " and ctgr='DA' limit 1";
                db.Open();
                dr = db.ExeRdr(sql);
                if (dr.HasRows)
                {
                    dr.Read();
                    url = string.Format(myWeb + "FILE/CA/DA/{0}/{1}/{2}", spt, sn1, dr["file"]);
                    if (lnkLbl == lnkTK_LandRgst_R) axAcroPDF2.src = url;
                    else axAcroPDF1.src = url;
                }
                else
                {
                    MessageBox.Show("저장된 [토지 등기] 파일이 없습니다.");
                }
                dr.Close();
                db.Close();
            }
            else if (lnkLbl == lnkTK_BldgRgst || lnkLbl == lnkTK_BldgRgst_R)
            {
                string tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
                //sql = "select * from " + tbl + " where (tid=" + tid + " or (spt=" + spt + " and sn='" + sn + "' and tid=0)) and ctgr='DB' limit 1";
                sql = "select * from " + tbl + " where tid=" + tid + " and ctgr='DB' limit 1";
                db.Open();
                dr = db.ExeRdr(sql);
                if (dr.HasRows)
                {
                    dr.Read();
                    url = string.Format(myWeb + "FILE/CA/DB/{0}/{1}/{2}", spt, sn1, dr["file"]);
                    //if (lnkLbl == lnkTK_BldgRgst_R) pdfRgst.src = url;
                    //else axPdf.src = url;
                    if (lnkLbl == lnkTK_BldgRgst_R) axAcroPDF2.src = url;
                    else axAcroPDF1.src = url;
                }
                else
                {
                    MessageBox.Show("저장된 [건물 등기] 파일이 없습니다.");
                }
                dr.Close();
                db.Close();
            }
            else if (lnkLbl == lnkTK_Sedae)
            {
                string tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
                sql = "select * from " + tbl + " where tid=" + tid + " and ctgr='EA' limit 1";
                db.Open();
                dr = db.ExeRdr(sql);
                if (dr.HasRows)
                {
                    dr.Read();
                    url = string.Format(myWeb + "FILE/CA/EA/{0}/{1}/{2}", spt, sn1, dr["file"]);
                    axAcroPDF1.src = url;
                }
                else
                {
                    MessageBox.Show("저장된 [세대 열람] 파일이 없습니다.");
                }
                dr.Close();
                db.Close();
            }

            if (lnkLbl == lnkTK_ApslOcr)
            {
                tbcL.SelectedTab = tabOCR;
            }
            else
            {
                if (lnkLbl == lnkTK_LandRgst_R || lnkLbl == lnkTK_BldgRgst_R) tbcR.SelectedTab = tabRgst;
                else tbcL.SelectedTab = tabPdf1;
            }
        }

        /// <summary>
        /// 각 부분별/전체 DB 저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveData(object sender, EventArgs e)
        {
            int seq = 0, dgHCnt = 0, dgLCnt = 0, dgBCnt = 0, dgECnt = 0, dgICnt = 0, dgMCnt = 0, dgCCnt = 0, dgTCnt = 0, dgRLCnt = 0, dgRBCnt = 0, rowIdx = 0;
            bool dgHValid = true, dgLValid = true, dgBValid = true, dgEValid = true, dgIValid = true, dgMValid = true, dgCValid = true, dgTValid = true, dgRLValid = true, dgRBValid = true;

            string sectDvsn, idx, mode, lsNo, sql, tid;
            string cat1, cat2, cat3, cat3Rec, sta1, sta2, landSqm, landTotSqm, bldgSqm, bldgTotSqm, rtSqm, rtTotSqm, spCdtn;
            decimal billAmt = 0, firstAmt = 0, apslAmt = 0, minbAmt = 0, sucbAmt = 0, sumApsl = 0;

            string state;       //입찰일정
            string bldgDvsn;    //건물, 제시외, 기계/기구 관련
            string rgDvsn = "", rank, rankSub, rgCd, rgNo, rcNo;       //등기관련
            
            tid = lnkTid.Text;
            if (tid == string.Empty || tid == "TID")
            {
                MessageBox.Show("선택된 물건이 없습니다.");
                return;
            }

            List<MySqlParameter> sp = new List<MySqlParameter>();
            sectDvsn = ((Button)sender).Text.Replace("저장", string.Empty);

            //값 유효성 체크
            if (sectDvsn == "기본정보" || sectDvsn == "현황/면적" || sectDvsn == "차량/선박" || sectDvsn == "전체")
            {
                apslAmt = Convert.ToDecimal(txtApslAmt.Text.Replace(",", string.Empty).Trim());
                minbAmt = Convert.ToDecimal(txtMinbAmt.Text.Replace(",", string.Empty).Trim());
                sucbAmt = Convert.ToDecimal(txtSucbAmt.Text.Replace(",", string.Empty).Trim());
                billAmt = Convert.ToDecimal(txtBillAmt.Text.Replace(",", string.Empty).Trim());
                firstAmt = Convert.ToDecimal(txtFirstAmt.Text.Replace(",", string.Empty).Trim());
                if (minbAmt > apslAmt)
                {
                    if (MessageBox.Show("최저가 > 감정가 입니다.\r\n적용 하시겠습니까?", "최저가 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No)
                    {
                        return;
                    }
                }

                if (sucbAmt > 0 && sucbAmt < minbAmt)
                {
                    MessageBox.Show("낙찰가 < 최저가 입니다.");
                    return;
                }
            }

            dgHCnt = dgH.Rows.Count - 1;
            dgICnt = dgI.Rows.Count - 1;
            dgLCnt = dgL.Rows.Count - 1;
            dgBCnt = dgB.Rows.Count - 1;
            dgECnt = dgE.Rows.Count - 1;
            dgMCnt = dgM.Rows.Count - 1;
            dgCCnt = dgC.Rows.Count - 1;
            dgTCnt = dgT.Rows.Count - 1;
            dgRLCnt = dgRL.Rows.Count - 1;
            dgRBCnt = dgRB.Rows.Count - 1;

            if (sectDvsn == "진행일정" || sectDvsn == "전체")
            {                
                foreach (DataGridViewRow row in dgH.Rows)
                {
                    if (row.Index == dgHCnt) break;
                    if (string.IsNullOrEmpty(row.Cells["dgH_State"].Value?.ToString()) || (row.Cells["dgH_State"].Value?.ToString() == "0") || string.IsNullOrEmpty(row.Cells["dgH_BidDt"].Value?.ToString()))
                    {
                        dgHValid = false;
                        break;
                    }
                }
                if (dgHValid == false)
                {
                    MessageBox.Show("[입찰일정]에서 <진행상태>, <일자>는 필수 입니다.");
                    return;
                }
            }
            
            if (sectDvsn == "목록내역" || sectDvsn == "전체")
            {                
                foreach (DataGridViewRow row in dgI.Rows)
                {
                    if (row.Index == dgICnt) break;
                    if (string.IsNullOrEmpty(row.Cells["dgI_LsNo"].Value?.ToString()) || string.IsNullOrEmpty(row.Cells["dgI_Adrs"].Value?.ToString()) || string.IsNullOrEmpty(row.Cells["dgI_Dvsn"].Value?.ToString()))
                    {
                        dgIValid = false;
                        break;
                    }
                }
                if (dgIValid == false)
                {
                    MessageBox.Show("[목록내역]에서 <LsNo>, <구분>, <소재지>는 필수 입니다.");
                    return;
                }
            }
            
            if (sectDvsn == "현황/면적" || sectDvsn == "전체")
            {
                if (txtLandApslAmt.Text == string.Empty) txtLandApslAmt.Text = "0";
                if (txtBldgApslAmt.Text == string.Empty) txtBldgApslAmt.Text = "0";
                if (txtPresApslAmtInc.Text == string.Empty) txtPresApslAmtInc.Text = "0";
                if (txtMachApslAmt.Text == string.Empty) txtMachApslAmt.Text = "0";

                sumApsl = Convert.ToDecimal(txtLandApslAmt.Text.Replace(",", string.Empty)) +
                    Convert.ToDecimal(txtBldgApslAmt.Text.Replace(",", string.Empty)) +
                    Convert.ToDecimal(txtPresApslAmtInc.Text.Replace(",", string.Empty)) +
                    Convert.ToDecimal(txtMachApslAmt.Text.Replace(",", string.Empty));

                if (sumApsl > 0)
                {
                    if (sumApsl != apslAmt)
                    {
                        MessageBox.Show(string.Format("{0}\r\n\r\n{1}\r\n{2}",
                            "<가격평가합계>와 <감정가>가 일치하지 않습니다.",
                            "* 가격평가합계 > " + string.Format("{0:N0}", sumApsl),
                            "* 가격차이 > " + string.Format("{0:N0}", (sumApsl - apslAmt))));
                    }
                }

                //토지, 건물, 제시외, 기계기구 현황                
                foreach (DataGridViewRow row in dgL.Rows)
                {
                    if (row.Index == dgLCnt) break;
                    if (string.IsNullOrEmpty(row.Cells["dgL_LsNo"].Value?.ToString()) || string.IsNullOrEmpty(row.Cells["dgL_Cat"].Value?.ToString()))
                    {
                        dgLValid = false;
                        break;
                    }
                }
                if (dgLValid == false)
                {
                    MessageBox.Show("[토지현황]에서 <LsNo>와 <지목>은 필수 입니다.");
                    return;
                }

                foreach (DataGridViewRow row in dgB.Rows)
                {
                    if (row.Index == dgBCnt) break;
                    if (string.IsNullOrEmpty(row.Cells["dgB_LsNo"].Value?.ToString()))
                    {
                        dgBValid = false;
                        break;
                    }
                }
                if (dgBValid == false)
                {
                    MessageBox.Show("[건물현황]에서 <LsNo>는 필수 입니다.");
                    return;
                }

                foreach (DataGridViewRow row in dgE.Rows)
                {
                    if (row.Index == dgECnt) break;
                    if (string.IsNullOrEmpty(row.Cells["dgE_LsNo"].Value?.ToString()))
                    {
                        dgEValid = false;
                        break;
                    }
                }
                if (dgEValid == false)
                {
                    MessageBox.Show("[제시외]에서 <LsNo>는 필수 입니다.");
                    return;
                }

                foreach (DataGridViewRow row in dgM.Rows)
                {
                    if (row.Index == dgMCnt) break;
                    if (string.IsNullOrEmpty(row.Cells["dgM_Nm"].Value?.ToString()) || string.IsNullOrEmpty(row.Cells["dgM_Amt"].Value?.ToString()))
                    {
                        dgMValid = false;
                        break;
                    }
                }
                if (dgMValid == false)
                {
                    MessageBox.Show("[기계기구]에서 <명칭>과 <평가액>은 필수 입니다.");
                    return;
                }
            }
            
            if (sectDvsn == "차량/선박" || sectDvsn == "전체")
            {                
                foreach (DataGridViewRow row in dgC.Rows)
                {
                    if (row.Index == dgCCnt) break;
                    if (string.IsNullOrEmpty(row.Cells["dgC_LsNo"].Value?.ToString()))
                    {
                        dgCValid = false;
                        break;
                    }
                }
                if (dgCValid == false)
                {
                    MessageBox.Show("[차량선박]에서 <LsNo>는 필수 입니다.");
                    return;
                }
            }
            
            if (sectDvsn == "임차인" || sectDvsn == "전체")
            {   
                foreach (DataGridViewRow row in dgT.Rows)
                {
                    if (row.Index == dgTCnt) break;
                    if (string.IsNullOrEmpty(row.Cells["dgT_LsNo"].Value?.ToString()) || string.IsNullOrEmpty(row.Cells["dgT_UseCd"].Value?.ToString()))
                    {
                        dgTValid = false;
                        break;
                    }
                }
                if (dgTValid == false)
                {
                    MessageBox.Show("[임차인]에서 <LsNo>, <용도코드>는 필수 입니다.");
                    return;
                }
            }
            
            if (sectDvsn == "토지등기" || sectDvsn == "전체")
            {                
                foreach (DataGridViewRow row in dgRL.Rows)
                {
                    if (row.Index == dgRLCnt) break;
                    if (string.IsNullOrEmpty(row.Cells["dgRL_Sect"].Value?.ToString()) || string.IsNullOrEmpty(row.Cells["dgRL_Rank"].Value?.ToString()))
                    {
                        dgRLValid = false;
                        break;
                    }
                }
                if (dgRLValid == false)
                {
                    MessageBox.Show("[토지등기]에서 <갑/을구분>과 <순위>는 필수 입니다.");
                    return;
                }
            }
            
            if (sectDvsn == "건물등기" || sectDvsn == "전체")
            {                
                foreach (DataGridViewRow row in dgRB.Rows)
                {
                    if (row.Index == dgRBCnt) break;
                    if (string.IsNullOrEmpty(row.Cells["dgRB_Sect"].Value?.ToString()) || string.IsNullOrEmpty(row.Cells["dgRB_Rank"].Value?.ToString()))
                    {
                        dgRBValid = false;
                        break;
                    }
                }
                if (dgRBValid == false)
                {
                    MessageBox.Show("[건물등기]에서 <갑/을구분>과 <순위>는 필수 입니다.");
                    return;
                }
                else
                {
                    if (rdoRgstDvsn2.Checked) rgDvsn = "2";
                    else if (rdoRgstDvsn3.Checked) rgDvsn = "3";
                    else
                    {
                        if (dgRBCnt > 0 && pnlCar.Enabled == false)
                        {
                            MessageBox.Show("등기 구분을 선택 해 주세요.");
                            return;
                        }                        
                    }
                }                
            }

            //DB 처리
            db.Open();
            if (sectDvsn == "기본정보" || sectDvsn == "현황/면적" || sectDvsn == "전체")
            {
                cat3 = cbxCat3.SelectedValue.ToString();
                if (cat3 == "0")
                {
                    cat2 = "0";
                    cat1 = "0";
                }
                else
                {
                    cat2 = dtCatCd.Rows.Cast<DataRow>().Where(t => t["cat3_cd"].ToString() == cat3).FirstOrDefault().Field<UInt16>("cat2_cd").ToString();
                    cat1 = dtCatCd.Rows.Cast<DataRow>().Where(t => t["cat2_cd"].ToString() == cat2).FirstOrDefault().Field<byte>("cat1_cd").ToString();
                }
                cat3Rec = cbxCat3Rec.SelectedValue.ToString();
                sta2 = cbxState.SelectedValue.ToString();
                sta1 = dtStateCd.Rows.Cast<DataRow>().Where(t => t["sta2_cd"].ToString() == sta2).FirstOrDefault().Field<byte>("sta1_cd").ToString();                
                landSqm = txtLandSqm.Text.Replace(",", string.Empty).Trim();
                landTotSqm = txtLandTotSqm.Text.Replace(",", string.Empty).Trim();
                bldgSqm = txtBldgSqm.Text.Replace(",", string.Empty).Trim();
                bldgTotSqm = txtBldgTotSqm.Text.Replace(",", string.Empty).Trim();
                rtSqm = txtRtSqm.Text.Replace(",", string.Empty).Trim();
                rtTotSqm = txtRtTotSqm.Text.Replace(",", string.Empty).Trim();

                List<string> lstSpCdtn = new List<string>();
                foreach (ListViewItem item in lvSpc.CheckedItems)
                {
                    lstSpCdtn.Add(item.Text.Remove(item.Text.IndexOf(".")));
                }
                spCdtn = string.Join(",", lstSpCdtn.ToArray());

                sql = "update ta_list set crt=@crt, spt=@spt, dpt=@dpt, sn1=@sn1, sn2=@sn2, pn=@pn, dpsl_dvsn=@dpsl_dvsn, cat1=@cat1, cat2=@cat2, cat3=@cat3, cat3_rec=@cat3_rec, mbldg=@mbldg, sta1=@sta1, sta2=@sta2, apsl_amt=@apsl_amt, 1st_amt=@1st_amt, minb_amt=@minb_amt, sucb_amt=@sucb_amt, " +
                    "fb_cnt=@fb_cnt, dpst_type=@dpst_type, dpst_rate=@dpst_rate, creditor=@creditor, debtor=@debtor, owner=@owner, land_sqm=@land_sqm, land_tot_sqm=@land_tot_sqm, bldg_sqm=@bldg_sqm, bldg_tot_sqm=@bldg_tot_sqm, rt_sqm=@rt_sqm, rt_tot_sqm=@rt_tot_sqm, " +
                    "rcp_dt=@rcp_dt, ini_dt=@ini_dt, shr_dt=@shr_dt, bid_dt=@bid_dt, end_dt=@end_dt, sucb_dt=@sucb_dt, bid_cnt=@bid_cnt, bid_tm=@bid_tm, bid_tm1=@bid_tm1, bid_tm2=@bid_tm2, bid_tm3=@bid_tm3, " +
                    "adrs=@adrs, lot_cnt=@lot_cnt, ho_cnt=@ho_cnt, regn_adrs=@regn_adrs, road_adrs=@road_adrs, m_adrs_no=@m_adrs_no, s_adrs_no=@s_adrs_no, mt=@mt, m_bldg_no=@m_bldg_no, s_bldg_no=@s_bldg_no, bldg_nm=@bldg_nm, road_nm=@road_nm, apt_cd=@apt_cd, si_cd=@si_cd, gu_cd=@gu_cd, dn_cd=@dn_cd, ri_cd=@ri_cd, x=@x, y=@y, " +
                    "auct_type=@auct_type, frml_type=@frml_type, sp_cdtn=@sp_cdtn, rgst_yn=@rgst_yn" +
                    " where tid='" + tid + "'";
                sp.Add(new MySqlParameter("@crt", cbxCrtSpt.SelectedValue?.ToString().Substring(0, 2) ?? ""));
                sp.Add(new MySqlParameter("@spt", cbxCrtSpt.SelectedValue?.ToString() ?? ""));
                sp.Add(new MySqlParameter("@dpt", cbxDpt.SelectedValue));
                sp.Add(new MySqlParameter("@sn1", cbxSn1.Text));
                sp.Add(new MySqlParameter("@sn2", txtSn2.Text.Trim()));
                sp.Add(new MySqlParameter("@pn", txtPn.Text.Trim()));
                sp.Add(new MySqlParameter("@dpsl_dvsn", cbxDpslDvsn.SelectedValue));
                sp.Add(new MySqlParameter("@cat1", cat1));
                sp.Add(new MySqlParameter("@cat2", cat2));
                sp.Add(new MySqlParameter("@cat3", cat3));
                sp.Add(new MySqlParameter("@cat3_rec", cat3Rec));
                sp.Add(new MySqlParameter("@mbldg", (chkMultiBldg.Checked) ? "1" : "0"));
                sp.Add(new MySqlParameter("@sta1", sta1));
                sp.Add(new MySqlParameter("@sta2", sta2));
                sp.Add(new MySqlParameter("@apsl_amt", apslAmt));
                sp.Add(new MySqlParameter("@1st_amt", firstAmt));
                sp.Add(new MySqlParameter("@minb_amt", minbAmt));
                sp.Add(new MySqlParameter("@sucb_amt", sucbAmt));
                sp.Add(new MySqlParameter("@fb_cnt", txtFbCnt.Text.Trim()));
                sp.Add(new MySqlParameter("@dpst_type", cbxDpstType.SelectedValue));
                sp.Add(new MySqlParameter("@dpst_rate", cbxDpstRate.Text));
                sp.Add(new MySqlParameter("@creditor", ReNamePrsn(txtCreditor.Text)));
                sp.Add(new MySqlParameter("@debtor", ReNamePrsn(txtDebtor.Text)));
                sp.Add(new MySqlParameter("@owner", ReNamePrsn(txtOwner.Text)));
                sp.Add(new MySqlParameter("@land_sqm", landSqm));
                sp.Add(new MySqlParameter("@land_tot_sqm", landTotSqm));
                sp.Add(new MySqlParameter("@bldg_sqm", bldgSqm));
                sp.Add(new MySqlParameter("@bldg_tot_sqm", bldgTotSqm));
                sp.Add(new MySqlParameter("@rt_sqm", rtSqm));
                sp.Add(new MySqlParameter("@rt_tot_sqm", rtTotSqm));
                sp.Add(new MySqlParameter("@rcp_dt", mtxtRcpDt.Text));
                sp.Add(new MySqlParameter("@ini_dt", mtxtIniDt.Text));
                sp.Add(new MySqlParameter("@shr_dt", mtxtShrDt.Text));
                sp.Add(new MySqlParameter("@bid_dt", mtxtBidDt.Text));
                sp.Add(new MySqlParameter("@end_dt", mtxtEndDt.Text));
                sp.Add(new MySqlParameter("@sucb_dt", mtxtSucbDt.Text));
                sp.Add(new MySqlParameter("@bid_cnt", cbxBidCnt.Text));
                sp.Add(new MySqlParameter("@bid_tm", mtxtBidTm.Text + ":00"));
                sp.Add(new MySqlParameter("@bid_tm1", mtxtBidTm1.Text + ":00"));
                sp.Add(new MySqlParameter("@bid_tm2", mtxtBidTm2.Text + ":00"));
                sp.Add(new MySqlParameter("@bid_tm3", mtxtBidTm3.Text + ":00"));
                sp.Add(new MySqlParameter("@adrs", txtAdrs.Text.Trim()));
                sp.Add(new MySqlParameter("@lot_cnt", txtLotCnt.Text.Trim()));
                sp.Add(new MySqlParameter("@ho_cnt", txtHoCnt.Text.Trim()));
                sp.Add(new MySqlParameter("@regn_adrs", txtRegnAdrs.Text.Trim()));
                sp.Add(new MySqlParameter("@road_adrs", txtRoadAdrs.Text.Trim()));
                sp.Add(new MySqlParameter("@m_adrs_no", txtAdrsNoM.Text.Trim()));
                sp.Add(new MySqlParameter("@s_adrs_no", txtAdrsNoS.Text.Trim()));
                sp.Add(new MySqlParameter("@mt", cbxAdrsMt.SelectedValue.ToString()));
                sp.Add(new MySqlParameter("@m_bldg_no", txtBldgNoM.Text.Trim()));
                sp.Add(new MySqlParameter("@s_bldg_no", txtBldgNoS.Text.Trim()));
                sp.Add(new MySqlParameter("@bldg_nm", txtBldgNm.Text.Trim()));
                sp.Add(new MySqlParameter("@road_nm", txtRoadNm.Text.Trim()));
                sp.Add(new MySqlParameter("@apt_cd", txtAptCd.Text.Trim()));
                sp.Add(new MySqlParameter("@si_cd", txtSiCd.Text.Trim()));
                sp.Add(new MySqlParameter("@gu_cd", txtGuCd.Text.Trim()));
                sp.Add(new MySqlParameter("@dn_cd", txtDnCd.Text.Trim()));
                sp.Add(new MySqlParameter("@ri_cd", txtRiCd.Text.Trim()));
                sp.Add(new MySqlParameter("@x", txtCoordX.Text.Trim()));
                sp.Add(new MySqlParameter("@y", txtCoordY.Text.Trim()));
                sp.Add(new MySqlParameter("@auct_type", cbxAuctType.SelectedValue));
                sp.Add(new MySqlParameter("@frml_type", cbxFrmlType.SelectedValue));
                sp.Add(new MySqlParameter("@sp_cdtn", spCdtn));
                sp.Add(new MySqlParameter("@rgst_yn", cbxRgstYn.SelectedValue));
                db.ExeQry(sql, sp);
                sp.Clear();

                sql = "update ta_dtl set apsl_nm=@apsl_nm, apsl_dt=@apsl_dt, apsl_type=@apsl_type, apsl_land=@apsl_land, apsl_bldg=@apsl_bldg, apsl_pres_inc=@apsl_pres_inc, apsl_pres_dec=@apsl_pres_dec, apsl_mach=@apsl_mach," +
                    "auct_nm=@auct_nm, bill_amt=@bill_amt, prsv_dt=@prsv_dt, pd_note=@pd_note, loca=@loca, land_shp=@land_shp, adj_road=@adj_road, diff=@diff, faci=@faci, " +
                    "leas_note=@leas_note, etc_note=@etc_note, rgst_note=@rgst_note, attn_note1=@attn_note1, attn_note2=@attn_note2, analy_note=@analy_note" +
                    " where tid='" + tid + "'";
                sp.Add(new MySqlParameter("@apsl_nm", txtApslNm.Text.Trim()));
                sp.Add(new MySqlParameter("@apsl_dt", getDateParse(txtApslDt.Text)));
                sp.Add(new MySqlParameter("@apsl_type", cbxApslType.SelectedValue));
                sp.Add(new MySqlParameter("@apsl_land", txtLandApslAmt.Text.Replace(",", string.Empty)));
                sp.Add(new MySqlParameter("@apsl_bldg", txtBldgApslAmt.Text.Replace(",", string.Empty)));
                sp.Add(new MySqlParameter("@apsl_pres_inc", txtPresApslAmtInc.Text.Replace(",", string.Empty)));
                sp.Add(new MySqlParameter("@apsl_pres_dec", txtPresApslAmtDec.Text.Replace(",", string.Empty)));
                sp.Add(new MySqlParameter("@apsl_mach", txtMachApslAmt.Text.Replace(",", string.Empty)));
                sp.Add(new MySqlParameter("@prsv_dt", getDateParse(txtPrsvDtRead.Text)));
                sp.Add(new MySqlParameter("@auct_nm", txtAuctNm.Text.Trim()));
                sp.Add(new MySqlParameter("@bill_amt", txtBillAmt.Text.Replace(",", string.Empty)));
                sp.Add(new MySqlParameter("@pd_note", txtPdNote.Text.Trim()));
                sp.Add(new MySqlParameter("@loca", txtLoca.Text.Trim()));
                sp.Add(new MySqlParameter("@land_shp", txtLandShp.Text.Trim()));
                sp.Add(new MySqlParameter("@adj_road", txtAdjRoad.Text.Trim()));
                sp.Add(new MySqlParameter("@diff", txtDiff.Text.Trim()));
                sp.Add(new MySqlParameter("@faci", txtFaci.Text.Trim()));
                sp.Add(new MySqlParameter("@leas_note", txtLeasNote.Text.Trim()));
                sp.Add(new MySqlParameter("@etc_note", txtEtcNote.Text.Trim()));
                sp.Add(new MySqlParameter("@rgst_note", txtRgstNote.Text.Trim()));
                sp.Add(new MySqlParameter("@attn_note1", txtAttnNote1.Text.Trim()));
                sp.Add(new MySqlParameter("@attn_note2", txtAttnNote2.Text.Trim()));
                sp.Add(new MySqlParameter("@analy_note", txtAnalyNote.Text.Trim()));
                db.ExeQry(sql, sp);
                sp.Clear();

                newSTA1 = sta1;
                newSTA2 = sta2;
            }
            
            if (sectDvsn == "진행일정" || sectDvsn == "전체")
            {
                int fbCnt = 0;
                int bidCnt = 1;
                foreach (DataGridViewRow row in dgH.Rows)
                {
                    seq++;
                    if (row.Index == dgHCnt) break;
                    bool dbPrc = false;
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        if (cell.Style.BackColor == Color.PaleGreen)
                        {
                            dbPrc = true;
                        }
                    }
                    //if (dbPrc == true)
                    //{                        
                    //seq 때문에 모두 저장
                    state = row.Cells["dgH_State"].Value?.ToString() ?? string.Empty;
                    idx = row.Cells["dgH_Idx"].Value?.ToString() ?? string.Empty;
                    mode = (idx == string.Empty) ? "insert into" : "update";
                    sql = mode + " ta_hist set tid=@tid, seq=@seq, bid_dt=@bid_dt, bid_tm=@bid_tm, sta=@sta, amt=@amt, bidr_cnt=@bidr_cnt, sucb_nm=@sucb_nm, sucb_area=@sucb_area, 2nd_reg=@2nd_reg, pri_reg=@pri_reg";
                    if (mode == "update") sql += " where idx=" + idx;
                    sp.Add(new MySqlParameter("@tid", tid));
                    sp.Add(new MySqlParameter("@seq", seq));                    
                    sp.Add(new MySqlParameter("@bid_dt", getDateParse(row.Cells["dgH_BidDt"].Value?.ToString() ?? "")));
                    sp.Add(new MySqlParameter("@bid_tm", getTimeParse(row.Cells["dgH_BidTm"].Value?.ToString() ?? "")));
                    sp.Add(new MySqlParameter("@sta", row.Cells["dgH_State"].Value?.ToString() ?? ""));
                    sp.Add(new MySqlParameter("@amt", row.Cells["dgH_Amt"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                    sp.Add(new MySqlParameter("@bidr_cnt", row.Cells["dgH_BidrCnt"].Value?.ToString() ?? ""));
                    sp.Add(new MySqlParameter("@sucb_nm", row.Cells["dgH_SucBidr"].Value?.ToString() ?? ""));
                    sp.Add(new MySqlParameter("@sucb_area", row.Cells["dgH_Area"].Value?.ToString() ?? ""));
                    //sp.Add(new MySqlParameter("@2nd_reg", ((DataGridViewCheckBoxCell)row.Cells["dgH_2ndReg"]).Value?.ToString() ?? ""));
                    sp.Add(new MySqlParameter("@2nd_reg", ((row.Cells["dgH_2ndReg"].Value?.ToString() == "True" || (row.Cells["dgH_2ndReg"].Value?.ToString() == "1")) ? 1 : 0)));    //2021-12-22 오류수정 민영
                    sp.Add(new MySqlParameter("@pri_reg", row.Cells["dgH_PriReg"].Value?.ToString() ?? ""));
                    db.ExeQry(sql, sp);
                    sp.Clear();

                    if (state == "1111") fbCnt++;
                    //}
                }
                if (sectDvsn == "진행일정")
                {
                    if (dgHCnt >= 1)
                    {
                        rowIdx = dgHCnt - 1;
                        DataGridViewRow row = dgH.Rows[rowIdx];
                        string hSta = row.Cells["dgH_State"].Value?.ToString() ?? string.Empty;
                        string hDt = getDateParse(row.Cells["dgH_BidDt"].Value?.ToString() ?? "");
                        string hTm = getTimeParse(row.Cells["dgH_BidTm"].Value?.ToString() ?? "");
                        string hAmt = row.Cells["dgH_Amt"].Value?.ToString().Replace(",", string.Empty) ?? "0";
                        sta1 = hSta.Substring(0, 2);
                        sta2 = hSta;
                        string bidTm = "";
                        string bidTm1 = "";
                        string bidTm2 = "";
                        string preSta;
                        sql = "update ta_list set sta1='" + sta1 + "'";
                        if (sta2 == "1110")    //입찰예정(신건 또는 유찰)
                        {
                            sta2 = (fbCnt > 0) ? "1111" : "1110";
                            if (dgHCnt > 2)
                            {
                                preSta = dgH.Rows[rowIdx - 1].Cells["dgH_State"].Value.ToString();
                                //if (hSta == dgH.Rows[rowIdx - 1].Cells["dgH_State"].Value.ToString() && hDt == dgH.Rows[rowIdx - 1].Cells["dgH_BidDt"].Value.ToString())
                                if (hSta == "1110" && (preSta == "1110" || preSta == "1111") && hDt == getDateParse(dgH.Rows[rowIdx - 1].Cells["dgH_BidDt"].Value?.ToString() ?? ""))
                                {
                                    bidCnt = 2;
                                    bidTm1 = getTimeParse(dgH.Rows[rowIdx - 1].Cells["dgH_BidTm"].Value?.ToString() ?? "");
                                    bidTm2 = hTm;
                                    bidTm = (preSta == "1111") ? bidTm2 : bidTm1;
                                    if (preSta == "1110")
                                    {
                                        hAmt = dgH.Rows[rowIdx - 1].Cells["dgH_Amt"].Value?.ToString().Replace(",", string.Empty) ?? "0";
                                    }                                    
                                }
                                else
                                {
                                    bidTm = hTm;
                                    bidTm1 = hTm;
                                    bidCnt = 1;
                                }
                            }
                            else
                            {
                                bidTm = hTm;
                                bidTm1 = hTm;
                                bidCnt = 1;
                            }
                            sql += ", minb_amt='" + hAmt + "', bid_dt='" + hDt + "', fb_cnt=" + fbCnt.ToString() + ", bid_cnt=" + bidCnt.ToString() + ", bid_tm='" + bidTm + "', bid_tm1='" + bidTm1 + "', bid_tm2='" + bidTm2 + "'";
                        }
                        else if (hSta == "1210")    //낙찰
                        {
                            sql += ", sucb_amt='" + hAmt + "', sucb_dt='" + hDt + "'";
                        }
                        else if (sta1 == "14")  //종국물건
                        {
                            //sql += ", end_dt='" + hDt + "'";
                        }
                        sql += ", sta2='" + sta2 + "' where tid=" + tid;
                        //MessageBox.Show(sql);
                        db.ExeQry(sql);

                        newSTA1 = sta1;
                        newSTA2 = sta2;
                    }
                }                
            }
            
            if (sectDvsn == "목록내역" || sectDvsn == "전체")
            {
                foreach (DataGridViewRow row in dgI.Rows)
                {
                    if (row.Index == dgICnt) break;
                    bool dbPrc = false;
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        if (cell.Style.BackColor == Color.PaleGreen)
                        {
                            dbPrc = true;
                        }
                    }
                    if (dbPrc == true)
                    {
                        lsNo = row.Cells["dgI_LsNo"].Value.ToString();
                        idx = row.Cells["dgI_Idx"].Value?.ToString() ?? string.Empty;
                        mode = (idx == string.Empty) ? "insert into" : "update";
                        sql = mode + " ta_ls set tid=@tid, no=@ls_no, adrs=@adrs, pin=@pin, dvsn=@dvsn, note=@note, x=@x, y=@y, zone_no=@zone_no, hj_cd=@hj_cd, pnu=@pnu";
                        if (mode == "update") sql += " where idx=" + idx;
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@ls_no", lsNo));
                        sp.Add(new MySqlParameter("@adrs", row.Cells["dgI_Adrs"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@pin", row.Cells["dgI_Pin"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@dvsn", row.Cells["dgI_Dvsn"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@note", row.Cells["dgI_Note"].Value?.ToString() ?? ""));                        
                        sp.Add(new MySqlParameter("@x", row.Cells["dgI_X"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@y", row.Cells["dgI_Y"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@zone_no", row.Cells["dgI_ZoneNo"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@hj_cd", row.Cells["dgI_HjCd"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@pnu", row.Cells["dgI_Pnu"].Value?.ToString() ?? ""));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                }
            }
            
            if (sectDvsn == "현황/면적" || sectDvsn == "전체")
            {
                //토지현황
                foreach (DataGridViewRow row in dgL.Rows)
                {
                    if (row.Index == dgLCnt) break;
                    bool dbPrc = false;
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        if (cell.Style.BackColor == Color.PaleGreen)
                        {
                            dbPrc = true;
                        }
                    }
                    if (dbPrc == true)
                    {
                        lsNo = row.Cells["dgL_LsNo"].Value.ToString();
                        idx = row.Cells["dgL_Idx"].Value?.ToString() ?? string.Empty;
                        mode = (idx == string.Empty) ? "insert into" : "update";
                        sql = mode + " ta_land set tid=@tid, ls_no=@ls_no, cat_cd=@cat_cd, sqm=@sqm, tot_shr_sqm=@tot_shr_sqm, rt_sqm=@rt_sqm, tot_rt_sqm=@tot_rt_sqm, unit_prc=@unit_prc, amt=@amt, shr_str=@shr_str, adrs_s=@adrs_s, note=@note";
                        if (mode == "update") sql += " where idx=" + idx;
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@ls_no", lsNo));
                        sp.Add(new MySqlParameter("@cat_cd", row.Cells["dgL_Cat"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@sqm", row.Cells["dgL_Sqm"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@tot_shr_sqm", row.Cells["dgL_TotShrSqm"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@rt_sqm", row.Cells["dgL_RtSqm"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@tot_rt_sqm", row.Cells["dgL_TotRtSqm"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@unit_prc", row.Cells["dgL_UnitPrc"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@amt", row.Cells["dgL_Amt"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@shr_str", row.Cells["dgL_ShrStr"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@adrs_s", row.Cells["dgL_Adrs"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@note", row.Cells["dgL_Note"].Value?.ToString() ?? ""));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                }

                //건물현황
                bldgDvsn = "1";
                foreach (DataGridViewRow row in dgB.Rows)
                {
                    if (row.Index == dgBCnt) break;
                    bool dbPrc = false;
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        if (cell.Style.BackColor == Color.PaleGreen)
                        {
                            dbPrc = true;
                        }
                    }
                    if (dbPrc == true)
                    {
                        lsNo = row.Cells["dgB_LsNo"].Value.ToString();
                        idx = row.Cells["dgB_Idx"].Value?.ToString() ?? string.Empty;
                        mode = (idx == string.Empty) ? "insert into" : "update";
                        sql = mode + " ta_bldg set tid=@tid, ls_no=@ls_no, dvsn=@dvsn, flr=@flr, tot_flr=@tot_flr, sqm=@sqm, tot_shr_sqm=@tot_shr_sqm, actl_sqm=@actl_sqm, unit_prc=@unit_prc, amt=@amt, state=@state, struct=@struct, aprv_dt=@aprv_dt, shr_str=@shr_str, adrs_s=@adrs_s, elvt=@elvt, note=@note";
                        if (mode == "update") sql += " where idx=" + idx;
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@ls_no", lsNo));
                        sp.Add(new MySqlParameter("@flr", row.Cells["dgB_Flr"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@tot_flr", row.Cells["dgB_TotFlr"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@dvsn", bldgDvsn));
                        sp.Add(new MySqlParameter("@sqm", row.Cells["dgB_Sqm"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@tot_shr_sqm", row.Cells["dgB_TotShrSqm"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@actl_sqm", row.Cells["dgB_ActlSqm"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@unit_prc", row.Cells["dgB_UnitPrc"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@amt", row.Cells["dgB_Amt"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@state", row.Cells["dgB_State"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@struct", row.Cells["dgB_Struct"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@aprv_dt", getDateParse(row.Cells["dgB_AprvDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@shr_str", row.Cells["dgB_ShrStr"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@adrs_s", row.Cells["dgB_Adrs"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@elvt", row.Cells["dgB_ElvtCnt"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@note", row.Cells["dgB_Note"].Value?.ToString() ?? ""));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                }

                //제시외
                bldgDvsn = "2";
                foreach (DataGridViewRow row in dgE.Rows)
                {
                    if (row.Index == dgECnt) break;
                    bool dbPrc = false;
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        if (cell.Style.BackColor == Color.PaleGreen)
                        {
                            dbPrc = true;
                        }
                    }
                    if (dbPrc == true)
                    {
                        lsNo = row.Cells["dgE_LsNo"].Value.ToString();
                        idx = row.Cells["dgE_Idx"].Value?.ToString() ?? string.Empty;
                        mode = (idx == string.Empty) ? "insert into" : "update";
                        sql = mode + " ta_bldg set tid=@tid, ls_no=@ls_no, dvsn=@dvsn, flr=@flr, sqm=@sqm, tot_shr_sqm=@tot_shr_sqm, actl_sqm=@actl_sqm, unit_prc=@unit_prc, amt=@amt, state=@state, struct=@struct, shr_str=@shr_str, adrs_s=@adrs_s, note=@note, inc=@inc";
                        if (mode == "update") sql += " where idx=" + idx;
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@ls_no", lsNo));
                        sp.Add(new MySqlParameter("@dvsn", bldgDvsn));
                        sp.Add(new MySqlParameter("@flr", row.Cells["dgE_Flr"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@sqm", row.Cells["dgE_Sqm"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@tot_shr_sqm", row.Cells["dgE_TotShrSqm"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@actl_sqm", row.Cells["dgE_ActlSqm"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@unit_prc", row.Cells["dgE_UnitPrc"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@amt", row.Cells["dgE_Amt"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@state", row.Cells["dgE_State"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@struct", row.Cells["dgE_Struct"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@shr_str", row.Cells["dgE_ShrStr"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@adrs_s", row.Cells["dgE_Adrs"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@note", row.Cells["dgE_Note"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@inc", row.Cells["dgE_Inc"].Value?.ToString() ?? ""));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                }

                //기계기구
                bldgDvsn = "3";
                foreach (DataGridViewRow row in dgM.Rows)
                {
                    if (row.Index == dgMCnt) break;
                    bool dbPrc = false;
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        if (cell.Style.BackColor == Color.PaleGreen)
                        {
                            dbPrc = true;
                        }
                    }
                    if (dbPrc == true)
                    {
                        //lsNo = row.Cells["dgM_LsNo"].Value.ToString();
                        idx = row.Cells["dgM_Idx"].Value?.ToString() ?? string.Empty;
                        mode = (idx == string.Empty) ? "insert into" : "update";
                        sql = mode + " ta_bldg set tid=@tid, dvsn=@dvsn, state=@nm, amt=@amt, note=@note, adrs_s=@adrs_s";
                        if (mode == "update") sql += " where idx=" + idx;
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@dvsn", bldgDvsn));
                        sp.Add(new MySqlParameter("@nm", row.Cells["dgM_Nm"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@amt", row.Cells["dgM_Amt"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@adrs_s", row.Cells["dgM_Adrs"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@note", row.Cells["dgM_Note"].Value?.ToString() ?? ""));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                }

                //면적/가격 합계 정보
                landSqm = txtLandSqm.Text.Replace(",", string.Empty).Trim();
                landTotSqm = txtLandTotSqm.Text.Replace(",", string.Empty).Trim();
                bldgSqm = txtBldgSqm.Text.Replace(",", string.Empty).Trim();
                bldgTotSqm = txtBldgTotSqm.Text.Replace(",", string.Empty).Trim();
                rtSqm = txtRtSqm.Text.Replace(",", string.Empty).Trim();
                rtTotSqm = txtRtTotSqm.Text.Replace(",", string.Empty).Trim();

                sql = "update ta_list set land_sqm=@land_sqm, land_tot_sqm=@land_tot_sqm, bldg_sqm=@bldg_sqm, bldg_tot_sqm=@bldg_tot_sqm, rt_sqm=@rt_sqm, rt_tot_sqm=@rt_tot_sqm, sp_rgst=@sp_rgst where tid='" + tid + "'";
                sp.Add(new MySqlParameter("@land_sqm", landSqm));
                sp.Add(new MySqlParameter("@land_tot_sqm", landTotSqm));
                sp.Add(new MySqlParameter("@bldg_sqm", bldgSqm));
                sp.Add(new MySqlParameter("@bldg_tot_sqm", bldgTotSqm));
                sp.Add(new MySqlParameter("@rt_sqm", rtSqm));
                sp.Add(new MySqlParameter("@rt_tot_sqm", rtTotSqm));
                sp.Add(new MySqlParameter("@sp_rgst", cbxSpRgst.SelectedValue));
                db.ExeQry(sql, sp);
                sp.Clear();

                sql = "update ta_dtl set allow_anyval=@allow_anyval, apsl_land=@apsl_land, apsl_bldg=@apsl_bldg, apsl_pres_inc=@apsl_pres_inc, apsl_pres_dec=@apsl_pres_dec, apsl_mach=@apsl_mach, leas_note=@leas_note, rgst_note=@rgst_note where tid='" + tid + "'";
                sp.Add(new MySqlParameter("@allow_anyval", (chkAllowAnyVal.Checked) ? "1" : "0"));
                sp.Add(new MySqlParameter("@apsl_land", txtLandApslAmt.Text.Replace(",", string.Empty)));
                sp.Add(new MySqlParameter("@apsl_bldg", txtBldgApslAmt.Text.Replace(",", string.Empty)));
                sp.Add(new MySqlParameter("@apsl_pres_inc", txtPresApslAmtInc.Text.Replace(",", string.Empty)));
                sp.Add(new MySqlParameter("@apsl_pres_dec", txtPresApslAmtDec.Text.Replace(",", string.Empty)));
                sp.Add(new MySqlParameter("@apsl_mach", txtMachApslAmt.Text.Replace(",", string.Empty)));
                sp.Add(new MySqlParameter("@leas_note", txtLeasNote.Text.Trim()));
                sp.Add(new MySqlParameter("@rgst_note", txtRgstNote.Text.Trim()));
                db.ExeQry(sql, sp);
                sp.Clear();
            }
            
            if (sectDvsn == "차량/선박" || sectDvsn == "전체")
            {
                foreach (DataGridViewRow row in dgC.Rows)
                {
                    if (row.Index == dgCCnt) break;
                    bool dbPrc = false;
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        if (cell.Style.BackColor == Color.PaleGreen)
                        {
                            dbPrc = true;
                        }
                    }
                    //임시처리->제조사 및 모델 코드가 반영 안되므로 무조건 저장
                    dbPrc = true;
                    if (dbPrc == true)
                    {
                        lsNo = row.Cells["dgC_LsNo"].Value.ToString();
                        idx = row.Cells["dgC_Idx"].Value?.ToString() ?? string.Empty;
                        mode = (idx == string.Empty) ? "insert into" : "update";
                        sql = mode + " ta_cars set tid=@tid, ls_no=@ls_no, adrs=@adrs, co_cd=@co_cd, mo_cd=@mo_cd, car_nm=@car_nm, car_type=@car_type, reg_no=@reg_no, car_year=@car_year, cmpy=@cmpy, fuel=@fuel, trans=@trans, mtr=@mtr, " +
                            "aprv_no=@aprv_no, id_no=@id_no, dspl=@dspl, dist=@dist, prpl=@prpl, park=@park, color=@color, term=@term, mf_dt=@mf_dt, reg_dt=@reg_dt, hp=@hp, rpm=@rpm, note=@note";
                        if (mode == "update") sql += " where idx=" + idx;
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@ls_no", lsNo));
                        sp.Add(new MySqlParameter("@adrs", row.Cells["dgC_Adrs"].Value?.ToString() ?? ""));
                        //sp.Add(new MySqlParameter("@apsl_amt", row.Cells["dgC_ApslAmt"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@car_nm", row.Cells["dgC_Nm"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@car_type", row.Cells["dgC_CarType"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@reg_no", row.Cells["dgC_RegNo"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@car_year", row.Cells["dgC_Year"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@cmpy", row.Cells["dgC_Cmpy"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@fuel", row.Cells["dgC_Fuel"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@trans", row.Cells["dgC_Trans"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@mtr", row.Cells["dgC_Mtr"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@aprv_no", row.Cells["dgC_AprvNo"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@id_no", row.Cells["dgC_IdNo"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@dspl", row.Cells["dgC_Dspl"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@dist", row.Cells["dgC_Dist"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@prpl", row.Cells["dgC_Prpl"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@park", row.Cells["dgC_Park"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@color", row.Cells["dgC_Color"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@term", row.Cells["dgC_Term"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@hp", row.Cells["dgC_Hp"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@rpm", row.Cells["dgC_Rpm"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@mf_dt", getDateParse(row.Cells["dgC_MfDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@reg_dt", getDateParse(row.Cells["dgC_RegDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@note", row.Cells["dgC_Note"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@co_cd", cbxCarCoCd.SelectedValue?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@mo_cd", cbxCarMoCd.SelectedValue?.ToString() ?? ""));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                }
            }
            
            if (sectDvsn == "임차인" || sectDvsn == "전체")
            {
                foreach (DataGridViewRow row in dgT.Rows)
                {
                    if (row.Index == dgTCnt) break;
                    bool dbPrc = false;
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        if (cell.Style.BackColor == Color.PaleGreen)
                        {
                            dbPrc = true;
                        }
                    }
                    if (dbPrc == true)
                    {
                        lsNo = row.Cells["dgT_LsNo"].Value.ToString();
                        idx = row.Cells["dgT_Idx"].Value?.ToString() ?? string.Empty;
                        mode = (idx == string.Empty) ? "insert into" : "update";
                        sql = mode + " ta_leas set tid=@tid, ls_no=@ls_no, prsn=@prsn, inv_type=@inv_type, part=@part, use_cd=@use_cd, shop_nm=@shop_nm, term=@term, deposit=@deposite, m_money=@m_money, t_money=@t_money, t_mnth=@t_mnth, mv_dt=@mv_dt, fx_dt=@fx_dt, shr_dt=@shr_dt, biz=@biz, note=@note, hide=@hide";
                        if (mode == "update") sql += " where idx=" + idx;
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@ls_no", lsNo));
                        sp.Add(new MySqlParameter("@prsn", row.Cells["dgT_Prsn"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@inv_type", row.Cells["dgT_InvType"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@part", row.Cells["dgT_Part"].Value?.ToString() ?? ""));
                        //sp.Add(new MySqlParameter("@use_type", row.Cells["dgT_UseType"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@use_cd", row.Cells["dgT_UseCd"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@shop_nm", row.Cells["dgT_ShopNm"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@term", row.Cells["dgT_Term"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@deposite", row.Cells["dgT_Deposit"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@m_money", row.Cells["dgT_MMoney"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@t_money", row.Cells["dgT_TMoney"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@t_mnth", row.Cells["dgT_TMnth"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@mv_dt", getDateParse(row.Cells["dgT_MvDt"].Value?.ToString() ?? "", "dgT_MvDt")));
                        sp.Add(new MySqlParameter("@fx_dt", getDateParse(row.Cells["dgT_FxDt"].Value?.ToString() ?? "", "dgT_FxDt")));
                        sp.Add(new MySqlParameter("@shr_dt", getDateParse(row.Cells["dgT_ShrDt"].Value?.ToString() ?? "", "dgT_ShrDt")));
                        sp.Add(new MySqlParameter("@note", row.Cells["dgT_Note"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@biz", ((DataGridViewCheckBoxCell)row.Cells["dgT_ChkBiz"]).Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@hide", ((DataGridViewCheckBoxCell)row.Cells["dgT_Hide"]).Value?.ToString() ?? ""));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                }
                //sql = "update ta_dtl set leas_note='" + txtLeasNote.Text.Trim() + "', rgst_note='" + txtRgstNote.Text.Trim() + "' where tid='" + tid + "'";
                sql = "update ta_dtl set leas_note=@leas_note, rgst_note=@rgst_note where tid='" + tid + "'";   //2021-12-22 따옴표 오류-민영
                sp.Add(new MySqlParameter("@leas_note", txtLeasNote.Text.Trim()));
                sp.Add(new MySqlParameter("@rgst_note", txtRgstNote.Text.Trim()));
                db.ExeQry(sql, sp);
                sp.Clear();
            }
            
            if (sectDvsn == "토지등기" || sectDvsn == "전체")
            {
                foreach (DataGridViewRow row in dgRL.Rows)
                {
                    if (row.Index == dgRLCnt) break;
                    bool dbPrc = false;
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        if (cell.Style.BackColor == Color.PaleGreen)
                        {
                            dbPrc = true;
                        }
                    }
                    if (dbPrc == true)
                    {
                        rank = row.Cells["dgRL_Rank"].Value.ToString();
                        if (rank.Contains("-"))
                        {
                            Match match = Regex.Match(rank, @"(\d+)\-(\d+)", rxOptM);
                            rank = match.Groups[1].Value;
                            rankSub = match.Groups[2].Value;
                        }
                        else rankSub = "0";

                        rgCd = row.Cells["dgRL_RgCd"].Value?.ToString() ?? string.Empty;
                        rgNo = row.Cells["dgRL_RgNo"].Value?.ToString() ?? string.Empty;
                        rcNo = row.Cells["dgRL_RcNo"].Value?.ToString() ?? string.Empty;
                        rgNo = Regex.Replace(rgNo, @"[\-\*]+", string.Empty);
                        rcNo = Regex.Replace(rcNo, @"[제호]", string.Empty);

                        idx = row.Cells["dgRL_Idx"].Value?.ToString() ?? string.Empty;
                        mode = (idx == string.Empty) ? "insert into" : "update";
                        sql = mode + " ta_rgst set tid=@tid, rg_dvsn=@rg_dvsn, sect=@sect, rank=@rank, rank_s=@rank_s, rg_cd=@rg_cd, rg_nm=@rg_nm, rc_dt=@rc_dt, rc_no=@rc_no, b_amt=@b_amt, c_amt=@c_amt, prsn=@prsn, rg_no=@rg_no, mv_dt=@mv_dt, fx_dt=@fx_dt, bgn_dt=@bgn_dt, end_dt=@end_dt, ";
                        sql += "r_eno=@r_eno, aply=@aply, ekey=@ekey, take=@take, note=@note, adrs=@adrs, brch=@brch, hide=@hide";
                        if (mode == "update") sql += " where idx=" + idx;
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@rg_dvsn", "1"));
                        sp.Add(new MySqlParameter("@sect", row.Cells["dgRL_Sect"].Value?.ToString() ?? string.Empty));
                        sp.Add(new MySqlParameter("@rank", rank));
                        sp.Add(new MySqlParameter("@rank_s", rankSub));
                        sp.Add(new MySqlParameter("@rg_cd", rgCd));
                        sp.Add(new MySqlParameter("@b_amt", row.Cells["dgRL_BAmt"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@c_amt", row.Cells["dgRL_CAmt"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@rg_nm", ReNamePrsn(row.Cells["dgRL_RgNm"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@rc_no", rcNo));
                        sp.Add(new MySqlParameter("@prsn", ReNamePrsn(row.Cells["dgRL_Prsn"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@rg_no", rgNo));
                        sp.Add(new MySqlParameter("@rc_dt", getDateParse(row.Cells["dgRL_RcDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@mv_dt", getDateParse(row.Cells["dgRL_MvDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@fx_dt", getDateParse(row.Cells["dgRL_FxDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@bgn_dt", getDateParse(row.Cells["dgRL_BgnDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@end_dt", getDateParse(row.Cells["dgRL_EndDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@r_eno", row.Cells["dgRL_REno"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@note", ReNamePrsn(row.Cells["dgRL_Note"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@adrs", row.Cells["dgRL_Adrs"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@brch", row.Cells["dgRL_Brch"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@aply", ((DataGridViewCheckBoxCell)row.Cells["dgRL_Aply"]).Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@ekey", ((DataGridViewCheckBoxCell)row.Cells["dgRL_EKey"]).Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@take", ((DataGridViewComboBoxCell)row.Cells["dgRL_Take"]).Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@hide", ((DataGridViewCheckBoxCell)row.Cells["dgRL_Hide"]).Value?.ToString() ?? ""));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                }
            }
            
            if (sectDvsn == "건물등기" || sectDvsn == "전체")
            {
                foreach (DataGridViewRow row in dgRB.Rows)
                {
                    if (row.Index == dgRBCnt) break;
                    bool dbPrc = false;
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        if (cell.Style.BackColor == Color.PaleGreen)
                        {
                            dbPrc = true;
                        }
                    }
                    if (dbPrc == true)
                    {
                        rank = row.Cells["dgRB_Rank"].Value.ToString();
                        if (rank.Contains("-"))
                        {
                            Match match = Regex.Match(rank, @"(\d+)\-(\d+)", rxOptM);
                            rank = match.Groups[1].Value;
                            rankSub = match.Groups[2].Value;
                        }
                        else rankSub = "0";

                        rgCd = row.Cells["dgRB_RgCd"].Value?.ToString() ?? string.Empty;
                        rgNo = row.Cells["dgRB_RgNo"].Value?.ToString() ?? string.Empty;
                        rcNo = row.Cells["dgRB_RcNo"].Value?.ToString() ?? string.Empty;
                        rgNo = Regex.Replace(rgNo, @"[\-\*]+", string.Empty);
                        rcNo = Regex.Replace(rcNo, @"[제호]", string.Empty);

                        idx = row.Cells["dgRB_Idx"].Value?.ToString() ?? string.Empty;
                        mode = (idx == string.Empty) ? "insert into" : "update";
                        sql = mode + " ta_rgst set tid=@tid, rg_dvsn=@rg_dvsn, sect=@sect, rank=@rank, rank_s=@rank_s, rg_cd=@rg_cd, rg_nm=@rg_nm, rc_dt=@rc_dt, rc_no=@rc_no, b_amt=@b_amt, c_amt=@c_amt, prsn=@prsn, rg_no=@rg_no, mv_dt=@mv_dt, fx_dt=@fx_dt, bgn_dt=@bgn_dt, end_dt=@end_dt, ";
                        sql += "r_eno=@r_eno, aply=@aply, ekey=@ekey, take=@take, note=@note, adrs=@adrs, brch=@brch, hide=@hide";
                        if (mode == "update") sql += " where idx=" + idx;
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@rg_dvsn", rgDvsn));
                        sp.Add(new MySqlParameter("@sect", row.Cells["dgRB_Sect"].Value?.ToString() ?? string.Empty));
                        sp.Add(new MySqlParameter("@rank", rank));
                        sp.Add(new MySqlParameter("@rank_s", rankSub));
                        sp.Add(new MySqlParameter("@rg_cd", rgCd));
                        sp.Add(new MySqlParameter("@b_amt", row.Cells["dgRB_BAmt"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@c_amt", row.Cells["dgRB_CAmt"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@rg_nm", ReNamePrsn(row.Cells["dgRB_RgNm"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@rc_no", rcNo));
                        sp.Add(new MySqlParameter("@prsn", ReNamePrsn(row.Cells["dgRB_Prsn"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@rg_no", rgNo));
                        sp.Add(new MySqlParameter("@rc_dt", getDateParse(row.Cells["dgRB_RcDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@mv_dt", getDateParse(row.Cells["dgRB_MvDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@fx_dt", getDateParse(row.Cells["dgRB_FxDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@bgn_dt", getDateParse(row.Cells["dgRB_BgnDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@end_dt", getDateParse(row.Cells["dgRB_EndDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@r_eno", row.Cells["dgRB_REno"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@note", ReNamePrsn(row.Cells["dgRB_Note"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@adrs", row.Cells["dgRB_Adrs"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@brch", row.Cells["dgRB_Brch"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@aply", ((DataGridViewCheckBoxCell)row.Cells["dgRB_Aply"]).Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@ekey", ((DataGridViewCheckBoxCell)row.Cells["dgRB_EKey"]).Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@take", ((DataGridViewComboBoxCell)row.Cells["dgRB_Take"]).Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@hide", ((DataGridViewCheckBoxCell)row.Cells["dgRB_Hide"]).Value?.ToString() ?? ""));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                }
                sql = "update ta_dtl set prsv_dt='" + getDateParse(txtPrsvDt.Text) + "' where tid=" + tid;
                db.ExeQry(sql);
            }

            //임차인 및 등기에서 특수조건 검출
            if (sectDvsn == "임차인" || sectDvsn == "토지등기" || sectDvsn == "건물등기" || sectDvsn == "전체")
            {
                cat3 = cbxCat3.SelectedValue.ToString();
                if (cat3 == "0")
                {
                    cat2 = "0";
                    cat1 = "0";
                }
                else
                {
                    cat2 = dtCatCd.Rows.Cast<DataRow>().Where(t => t["cat3_cd"].ToString() == cat3).FirstOrDefault().Field<UInt16>("cat2_cd").ToString();
                    cat1 = dtCatCd.Rows.Cast<DataRow>().Where(t => t["cat2_cd"].ToString() == cat2).FirstOrDefault().Field<byte>("cat1_cd").ToString();
                }
                if (cat1 == "10" || cat1 == "20")
                {
                    spCdtnChk.RgstLeas(lnkTid.Text);
                }
            }            
            db.Close();

            //sms 발송대상 물건 저장
            string staMsg = string.Empty;
            if (STA1 != string.Empty && newSTA1 != string.Empty && STA1 != newSTA1)
            {
                if (newSTA1 == "11") staMsg = "진행";
                else if (newSTA1 == "12") staMsg = "매각";
                else if (newSTA1 == "13") staMsg = "미진행";
                else if (newSTA1 == "14") staMsg = "종국";

                if (staMsg != string.Empty)
                {
                    sql = "insert ignore into db_tank.tx_sms set tid='" + tid + "', state='" + staMsg + "', wdt=curdate(), wtm=curtime()";
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();
                }
            }

            MessageBox.Show("저장되었습니다.");
            dg_SelectionChanged(null, null);
        }

        /// <summary>
        /// 내부 작업상태-입력완료
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void chkWorksCplt_Click(object sender, EventArgs e)
        {
            string tid, sql;
            int works;

            tid = lnkTid.Text;
            if (tid == string.Empty || tid == "TID")
            {
                MessageBox.Show("선택된 물건이 없습니다.");
                return;
            }

            CheckBox cbx = sender as CheckBox;

            works = (cbx.Checked) ? 1 : 0;
            if (cbx == chkWorksCplt)
            {
                chkWorksCplt2.Checked = (cbx.Checked) ? true : false;
            }
            else
            {
                chkWorksCplt.Checked = (cbx.Checked) ? true : false;
            }

            if (works == 1)
            {
                chkWorksCplt.BackColor = Color.Transparent;
                chkWorksCplt.ForeColor = Color.Black;
                chkWorksCplt2.BackColor = Color.Transparent;
                chkWorksCplt2.ForeColor = Color.Black;
            }
            else
            {
                chkWorksCplt.BackColor = Color.DimGray;
                chkWorksCplt.ForeColor = Color.WhiteSmoke;
                chkWorksCplt2.BackColor = Color.DimGray;
                chkWorksCplt2.ForeColor = Color.WhiteSmoke;
            }

            sql = "update ta_list set works=" + works.ToString() + " where tid=" + tid;
            db.Open();
            db.ExeQry(sql);
            db.Close();

            dg.CurrentRow.Cells[1].Style.BackColor = (works == 1) ? Color.White : Color.DimGray;
            dg.CurrentRow.Cells[1].Style.ForeColor = (works == 1) ? Color.Black : Color.WhiteSmoke;
        }

        /// <summary>
        /// 관리비 체납내역 저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSaveArrears_Click(object sender, EventArgs e)
        {
            string tid, sql, cvp;

            tid = lnkTid.Text;
            if (tid == string.Empty || tid == "TID")
            {
                MessageBox.Show("선택된 물건이 없습니다.");
                return;
            }

            List<MySqlParameter> sp = new List<MySqlParameter>();

            cvp = "tid=@tid, dvsn=1, amt=@amt, period=@period, note=@note, wdt=@wdt";
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

            MessageBox.Show("[저장] 되었습니다.");
            dg_SelectionChanged(null, null);
        }

        /// <summary>
        /// 관리비 체납내역 삭제
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnDelArrears_Click(object sender, EventArgs e)
        {
            string tid, sql;

            tid = lnkTid.Text;
            if (tid == string.Empty || tid == "TID")
            {
                MessageBox.Show("선택된 물건이 없습니다.");
                return;
            }

            if (MessageBox.Show("※ 조사내역을 삭제 하시겠습니까?", "조사내역 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            sql = "delete from db_tank.tx_arrears where dvsn=1 and tid=" + tid;
            db.Open();
            db.ExeQry(sql);
            db.Close();

            MessageBox.Show("[삭제] 되었습니다.");
            dg_SelectionChanged(null, null);
        }

        /// <summary>
        /// 물건 주요 변동내역 목록
        /// </summary>
        /// <param name="tid"></param>
        private void LoadImptHist(string tid)
        {
            int i = 0, n = 0;
            string sql;

            dgImpt.Rows.Clear();

            sql = "select * from ta_impt_rec where tid=" + tid + " order by idx desc";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                n++;
                i = dgImpt.Rows.Add();
                //dgImpt["dgImpt_No", i].Value = i + 1;
                dgImpt["dgImpt_Ctgr", i].Value = (dr["ctgr"].ToString() == "0") ? string.Empty : dtImptCtgr.Rows.Cast<DataRow>().Where(x => x["cd"].ToString() == dr["ctgr"].ToString()).FirstOrDefault()["nm"].ToString();
                dgImpt["dgImpt_Src", i].Value = (dr["src"].ToString() == "0") ? string.Empty : dtImptSrc.Rows.Cast<DataRow>().Where(x => x["cd"].ToString() == dr["src"].ToString()).FirstOrDefault()["nm"].ToString();
                dgImpt["dgImpt_Note", i].Value = dr["note"];
                dgImpt["dgImpt_Idx", i].Value = dr["idx"];
                dgImpt["dgImpt_Wdt", i].Value = string.Format("{0:yyyy.MM.dd}", dr["wdt"]);
            }
            dr.Close();
            db.Close();

            if (n > 0)
            {
                foreach (DataGridViewRow row in dgImpt.Rows)
                {
                    row.Cells["dgImpt_No"].Value = n;
                    n--;
                }
            }

            dgImpt.ClearSelection();
        }

        /// <summary>
        /// 물건 주요 변동내역-신규(폼리셋)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnImptNew_Click(object sender, EventArgs e)
        {
            txtImptIdx.Text = string.Empty;
            txtImptNote.Text = string.Empty;
            cbxImptCtgr.SelectedIndex = 1;
            cbxImptSrc.SelectedIndex = 1;
        }

        /// <summary>
        /// 물건 주요 변동내역내역-삭제
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnImptDel_Click(object sender, EventArgs e)
        {
            string hisIdx, sql, tid;

            tid = lnkTid.Text;
            hisIdx = txtImptIdx.Text;
            if (hisIdx == string.Empty) return;

            if (MessageBox.Show("내역을 삭제 하시겠습니까?", "내역 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            sql = "delete from ta_impt_rec where idx=" + hisIdx;
            db.Open();
            db.ExeQry(sql);
            db.Close();

            MessageBox.Show("삭제 되었습니다.");
            btnImptNew_Click(null, null);

            LoadImptHist(tid);
        }

        /// <summary>
        /// 물건 주요 변동내역-내용 보기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgImpt_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int rowIdx = 0;
            string idx, sql;

            rowIdx = e.RowIndex;
            idx = dgImpt["dgImpt_Idx", rowIdx].Value.ToString();
            txtImptIdx.Text = idx;

            sql = "select * from ta_impt_rec where idx=" + idx;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            txtImptNote.Text = dr["note"].ToString();
            cbxImptCtgr.SelectedValue = dr["ctgr"];
            cbxImptSrc.SelectedValue = dr["src"];
            dr.Close();
            db.Close();
        }

        /// <summary>
        /// 물건 주요 변동내역-저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnImptSave_Click(object sender, EventArgs e)
        {
            string tid, hisIdx, hisNote, sql, cvp;

            tid = lnkTid.Text;
            hisIdx = txtImptIdx.Text;
            hisNote = txtImptNote.Text.Trim();

            if (tid == string.Empty)
            {
                MessageBox.Show("선택된 물건이 없습니다.");
                return;
            }

            if (hisNote == string.Empty)
            {
                MessageBox.Show("변동내용을 입력 해 주세요.");
                return;
            }

            hisNote = hisNote.Replace("▶", string.Empty).Trim();

            List<MySqlParameter> sp = new List<MySqlParameter>();

            cvp = "idx=@idx, tid=@tid, ctgr=@ctgr, src=@src, note=@note";
            sql = "insert into ta_impt_rec set " + cvp + ", wdt=curdate() ON DUPLICATE KEY UPDATE " + cvp;
            sp.Add(new MySqlParameter("@idx", hisIdx));
            sp.Add(new MySqlParameter("@tid", tid));
            sp.Add(new MySqlParameter("@ctgr", cbxImptCtgr.SelectedValue));
            sp.Add(new MySqlParameter("@src", cbxImptSrc.SelectedValue));
            sp.Add(new MySqlParameter("@note", hisNote));
            db.Open();
            db.ExeQry(sql, sp);
            sp.Clear();
            db.Close();

            MessageBox.Show("저장 되었습니다.");
            btnImptNew_Click(null, null);

            LoadImptHist(tid);
        }

        /// <summary>
        /// 물건 주요 변동내역-상용어구
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lbxImptPhrase_DoubleClick(object sender, EventArgs e)
        {
            txtImptNote.Text = lbxImptPhrase.SelectedItem.ToString().Replace("▶", string.Empty).Trim();
        }

        /// <summary>
        /// 물건 주요 변동내역-단축키 <F11>로 복사&붙여넣기
        /// </summary>
        private void ImptCopyNPaste()
        {
            string selTxt = string.Empty;
            
            foreach (Control ctrl in tabNote.Controls)
            {
                if (ctrl.GetType() == typeof(TextBox))
                {
                    TextBox mTxt = (TextBox)ctrl;
                    selTxt = mTxt.SelectedText.Trim();
                    if (selTxt != string.Empty)
                    {
                        btnImptNew_Click(null, null);
                        tbcNote.SelectedTab = tabImptRec;
                        txtImptNote.Text = $"{selTxt}";
                        break;
                    }
                }
            }

            selTxt = txtLeasNote.SelectedText.Trim();
            if (selTxt != string.Empty)
            {
                btnImptNew_Click(null, null);
                tbcNote.SelectedTab = tabImptRec;
                txtImptNote.Text = $"{selTxt}";
            }
        }

        /// <summary>
        /// 물건 주요 변동내역-구분 가이드
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkSolarHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            tbcL.SelectedTab = tabWbr3;
            wbr3.Navigate("https://www.tankauction.com/SOLAR/SolarHelp.php");
        }

        /// <summary>
        /// 권리자명 치환
        /// </summary>
        /// <param name="prsn"></param>
        /// <returns></returns>
        private string ReNamePrsn(string prsn)
        {
            string reName = prsn;

            if (reName.Contains("은행") && reName.Contains("주식회사")) reName = reName.Replace("주식회사", string.Empty).Trim();

            Dictionary<string, string> dict = new Dictionary<string, string>();
            dict.Add("조흥은행|동화은행", "신한은행");
            dict.Add("평화은행|한국상업은행|한빛은행|한일은행", "우리은행");
            dict.Add("대한보증보험|한국보증보험", "서울보증보험");
            dict.Add("한국주택은행|대동은행|동남은행", "국민은행");
            dict.Add("대구주택할부금융|우리주택할부금융", "우리캐피탈");
            dict.Add("서울은행|서울신탁|보람은행|한국외환은행", "하나은행");
            dict.Add("성업공사", "한국자산관리공사");
            dict.Add("sk생명|국민생명보험", "미래에셋생명보험");
            dict.Add("한미은행", "한국씨티은행");
            dict.Add("농어촌진흥공사|농업기반공사", "한국농촌공사");
            dict.Add("lg화재보험|lig손해보험", "KB손해보험");
            dict.Add("lg카드|엘지카드", "신한카드");
            dict.Add("금강고려화학", "케이씨씨");
            dict.Add("농업협동조합|농협협동조합", "농협");
            dict.Add("신용협동조합", "신협");
            dict.Add("수산업협동조합", "수협");
            dict.Add("축산업협동조합", "축협");
            dict.Add("어업협동조합", "어협");
            dict.Add("주택금융신용보증기금", "한국주택금융공사");
            dict.Add("(^제일은행)|한국스탠다드차타드은행|sc은행", "한국스탠다드차타드제일은행");
            dict.Add("동부화재해상보험주식회사", "디비손해보험주식회사");
            dict.Add(@"[\s]*주식회사[\s]*", "(주)");

            foreach (KeyValuePair<string, string> kvp in dict)
            {
                reName = Regex.Replace(reName, kvp.Key, kvp.Value);
            }

            return reName.Trim();
        }

        /// <summary>
        /// 물건 삭제
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnDelThing_Click(object sender, EventArgs e)
        {
            string sql, tid, spt, sn1, sn2, pn;

            if (MessageBox.Show("물건을 삭제 하시겠습니까?", "물건 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            tid = lnkTid.Text;
            sql = "select spt, sn1, sn2, pn from ta_list where tid=" + tid;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            spt = dr["spt"].ToString();
            sn1 = dr["sn1"].ToString();
            sn2 = dr["sn2"].ToString();
            pn = dr["pn"].ToString();
            dr.Close();

            sql = "insert into db_tank.tx_del_things set tid='" + tid + "', spt='" + spt + "', sn1='" + sn1 + "', sn2='" + sn2 + "', pn='" + pn + "', staff='" + Properties.Settings.Default.USR_ID + "', wdt=now()";
            db.ExeQry(sql);
            db.Close();


            string[] tbls = { "ta_list", "ta_dtl", "ta_ls", "ta_hist", "ta_bldg", "ta_land", "ta_cars", "ta_leas", "ta_prsn", "ta_rgst", "ta_seq", "ta_ilp" };

            db.Open();
            foreach (string tbl in tbls)
            {
                sql = "delete from " + tbl + " where tid=" + tid;
                db.ExeQry(sql);
            }
            db.Close();

            MessageBox.Show("물건이 삭제 되었습니다.");
            btnSrch_Click(null, null);
        }

        /// <summary>
        /// 물건 추가
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnNewThing_Click(object sender, EventArgs e)
        {
            int i = 0;
            string sql, tid;

            if (MessageBox.Show("새로운 물건을 생성 하시겠습니까?", "물건 생성", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            db.Open();
            sql = "insert into ta_list (tid, sta1, sta2, dpst_type, dpst_rate, 1st_dt, 2nd_dt) values (null, 11, 1110, 1, 10, curdate(), curdate())";
            db.ExeQry(sql);
            
            tid = ((UInt64)db.LastId()).ToString();
            sql = "insert into ta_dtl set tid=" + tid;
            db.ExeQry(sql);
            db.Close();

            i = dg.Rows.Add();
            dg["dg_Tid", i].Value = tid;

            MessageBox.Show("새로운 물건 고유번호(TID)가 생성 되었습니다.");
            dg.Focus();
            dg.Rows[i].Selected = true;
            //dg_SelectionChanged(null, null);
        }

        /// <summary>
        /// 목록내역 50건 이상 현황 실시간 분석-물건상세Sub-면적/대지권/현황/구조(토지,건물,제시외)
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="doc"></param>
        private void PrcDtlSub_LandBldg(HAPDoc doc)
        {
            string sql = "", lsNo = "", lsType = "", catNm = "", catCd = "", floor = "", flrCd = "", shrStr0 = "", shrStr = "", frtn = "", dtlStr = "", etcStr = "", use = "", strt = "", area = "", lotCnt = "";
            string landSection = "", bldgSection = "";
            double sqm = 0, totSqm = 0, bldgSqm = 0, totShrSqm = 0, dt = 0, nt = 0;
            double sumLandSqm = 0, sumLandTotSqm = 0, sumRtSqm = 0, rtTotSqm = 0, sumBldgSqm = 0, sumBldgTotSqm = 0;
            bool macExist = false;
            int i = 0;

            string tid = lnkTid.Text;

            string bldgPtrn = @"([지하옥탑상일이삼사오육칠팔구십단\d]+[층실])[ ]*(.*?[소실조택고장당원설점\)])*[ ]*(\d[\d\.\,]*)[ ]*㎡";
            //string etcPtrn1 = @"\d+[\.\s]+\(용도\)([\w\s\(\)\d\.\-\&\:\,]+)\(구조\)([\w\s\(\)\d\.\-\&\:\,]+)\(면적\)[약각 ]*(\d[\d\.\,]*)[ ]*([㎡주식개\d\*\(\)\w\, ]+)";  //제시외 패턴-1 (1-용도, 2-구조, 3-면적, 4-단위 및 기타)
            //string etcPtrn2 = @"\d+[\.\s]+\(용도\)([\w\s\(\)\d\.\-\&\:\,]+)\(구조\)([\w\(\)\d\.\-\&\:\, ]+)";                                                              //제시외 패턴-2 (1-용도, 2-구조) -> 패턴-1과 용도와 구조는 동일하나 면적부분이 없음
            string etcPtrn1 = @"\d+[\.\s]+\(용도\)(.*)\s+\(구조\)(.*)\s+\(면적\)[약각 ]*(\d[\d\.\,]*)[ ]*([㎡주식개\d\*\(\)\w\, ]+)";   //제시외 패턴-1 (1-용도, 2-구조, 3-면적, 4-단위 및 기타)
            string etcPtrn2 = @"\d+[\.\s]+\(용도\)(.*)\s+\(구조\)(.*)";                                                                 //제시외 패턴-2 (1-용도, 2-구조) -> 패턴-1과 용도와 구조는 동일하나 면적부분이 없음
            string macPtrn = @"기계기구|[a-z]{4,}|\d{4}|\w+[\d]*\-\d+|kw|kva|ton|mm|kg";
            string frtnPtrn1 = @"(\d+[\.\d]*)[ ]*분의[ ]*(\d+[\.\d]*)";   //분수 패턴-1
            string frtnPtrn2 = @"(\d+[\.\d]*)/(\d+[\.\d]*)";              //분수 패턴-2

            //토지용
            DataTable dtL = new DataTable();
            dtL.Columns.Add("lsNo");
            dtL.Columns.Add("multi");
            dtL.Columns.Add("catNm");
            dtL.Columns.Add("catCd");
            dtL.Columns.Add("sqm");
            dtL.Columns.Add("rtSqm");
            dtL.Columns.Add("totShrSqm");
            dtL.Columns.Add("totRtSqm");
            dtL.Columns.Add("frtn");
            dtL.Columns.Add("shrStr");

            //건물용
            DataTable dtB = new DataTable();
            dtB.Columns.Add("lsNo");
            dtB.Columns.Add("multi");
            dtB.Columns.Add("floor");
            dtB.Columns.Add("sqm");
            dtB.Columns.Add("totShrSqm");
            dtB.Columns.Add("shrStr");
            dtB.Columns.Add("tmpStr");
            dtB.Columns.Add("totFlr");

            //제시외
            DataTable dtE = new DataTable();
            dtE.Columns.Add("lsNo");
            dtE.Columns.Add("state");
            dtE.Columns.Add("struct");
            dtE.Columns.Add("sqm");

            HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='목록내역 표']/tbody/tr");
            if (ncTr == null) return;

            List<MySqlParameter> sp = new List<MySqlParameter>();

            foreach (HtmlNode tr in ncTr)
            {
                sqm = 0; totSqm = 0; bldgSqm = 0; totShrSqm = 0; dt = 0; nt = 0;
                floor = ""; shrStr0 = ""; shrStr = ""; etcStr = ""; use = ""; strt = ""; area = "";

                HtmlNodeCollection ncTd = tr.SelectNodes("./td");
                if (ncTd.Count != 3) continue;
                lsNo = ncTd[0].InnerText.Trim();
                lsType = ncTd[1].InnerText.Trim();
                dtlStr = ncTd[2].InnerText.Replace("&nbsp;", string.Empty).Trim();
                dtlStr = Regex.Replace(dtlStr, @"[ ]*평방[ ]*미터|[ ]*제곱[ ]*미터", "㎡");
                //dtlStr = Regex.Replace(ncTd[2].InnerHtml, @"<[ㄱ-힣]+", string.Empty);  //처리불가 - 매각지분 : <경매할지분 공유자지분중 724분의215(갑구4) 엘티산업㈜ 소유지분>

                if (lsType == "토지")
                {
                    int eIndex = dtlStr.IndexOf("제시외");
                    if (eIndex > -1)
                    {
                        etcStr = dtlStr.Substring(eIndex);
                        dtlStr = dtlStr.Remove(eIndex);
                    }
                    dtlStr = landUnitConvert("토지", dtlStr);
                    Match match = Regex.Match(dtlStr, @"(" + landPtrn + "|null" + @")[ ]*(\d[\d\.\,]*)[ ]*㎡", rxOptM);
                    if (match.Success == false) continue;
                    catNm = match.Groups[1].Value.Trim();
                    if (catNm == "대") catNm = "대지";
                    var x = from DataRow r in dtCatCd.Rows
                            where r["cat2_cd"].ToString() == "1010" && r["cat3_nm"].ToString() == catNm
                            select r;
                    if (x.Count() > 0) catCd = x.CopyToDataTable().Rows[0]["cat3_cd"].ToString();
                    else catCd = "0";
                    totSqm = Convert.ToDouble(match.Groups[2].Value.Replace(",", string.Empty));
                    //match = Regex.Match(dtlStr, @"매각지분[ ]*:[ ]*(.*)", rxOptS);
                    match = Regex.Match(dtlStr, @"매각지분[ \:]*(.*)", rxOptS);

                    if (match.Success)
                    {
                        shrStr = match.Groups[1].Value;
                        shrStr = Regex.Replace(shrStr, @"제시외.*", string.Empty, rxOptS).Trim();
                        MatchCollection mc1 = Regex.Matches(shrStr, frtnPtrn1, rxOptS);
                        MatchCollection mc2 = Regex.Matches(shrStr, frtnPtrn2, rxOptS);
                        totShrSqm = totSqm;
                        sqm = 0;
                        if (mc1 != null)
                        {
                            foreach (Match m in mc1)
                            {
                                dt = Convert.ToDouble(m.Groups[1].Value);
                                nt = Convert.ToDouble(m.Groups[2].Value);
                                sqm += totShrSqm * nt / dt;
                            }
                        }
                        if (mc2 != null)
                        {
                            foreach (Match m in mc2)
                            {
                                dt = Convert.ToDouble(m.Groups[2].Value);
                                nt = Convert.ToDouble(m.Groups[1].Value);
                                sqm += totShrSqm * nt / dt;
                            }
                        }
                        if (mc1.Count == 0 && mc2.Count == 0)
                        {
                            totShrSqm = 0;
                            sqm = totSqm;
                        }
                        else
                        {
                            if (totShrSqm == sqm)
                            {
                                totShrSqm = 0;
                                shrStr = string.Empty;
                            }
                            else
                            {
                                shrStr = string.Format("전체면적 {0}㎡중 {1}", totShrSqm, shrStr);
                            }
                        }
                    }
                    else
                    {
                        sqm = totSqm;
                    }
                    dtL.Rows.Add(lsNo, 0, catNm, catCd, sqm, "", totShrSqm, 0, frtn, shrStr);

                    //제시외
                    if (etcStr != string.Empty)
                    {
                        MatchCollection mc = Regex.Matches(etcStr, etcPtrn1, rxOptM);
                        if (mc != null)
                        {
                            foreach (Match m in mc)
                            {
                                use = m.Groups[1].Value.Trim();
                                strt = m.Groups[2].Value.Trim();
                                area = m.Groups[3].Value.Trim();
                                if (use.Contains("기계기구"))
                                {
                                    macExist = true;
                                    continue;
                                }
                                if (m.Value.Contains("수목") || m.Value.Contains("나무") || m.Value.Contains("관정"))
                                {
                                    area = string.Empty;
                                }
                                else
                                {
                                    if (Regex.IsMatch(strt, macPtrn, rxOptM))
                                    {
                                        macExist = true;
                                        continue;
                                    }
                                }
                                dtE.Rows.Add(lsNo, use, strt, area);
                            }
                        }
                        else
                        {
                            mc = Regex.Matches(etcStr, etcPtrn2, rxOptM);
                            if (mc != null)
                            {
                                foreach (Match m in mc)
                                {
                                    use = m.Groups[1].Value.Trim();
                                    strt = m.Groups[2].Value.Trim();
                                    if (use.Contains("기계기구"))
                                    {
                                        macExist = true;
                                        continue;
                                    }
                                    if (m.Value.Contains("수목") || m.Value.Contains("나무") || m.Value.Contains("관정"))
                                    {
                                        area = string.Empty;
                                    }
                                    else
                                    {
                                        if (Regex.IsMatch(strt, macPtrn, rxOptM))
                                        {
                                            macExist = true;
                                            continue;
                                        }
                                    }
                                    dtE.Rows.Add(lsNo, use, strt, "");
                                }
                            }
                        }
                    }
                }
                else if (lsType == "건물")
                {
                    int eIndex = dtlStr.IndexOf("제시외");
                    if (eIndex > -1)
                    {
                        etcStr = dtlStr.Substring(eIndex);
                        dtlStr = dtlStr.Remove(eIndex);
                    }
                    dtlStr = landUnitConvert("건물", dtlStr);
                    dtlStr = Regex.Replace(dtlStr, @"\(.*?\)", string.Empty, rxOptS);   //하위 세부내역 면적 제외
                    string[] strArr = dtlStr.Split('\n');
                    foreach (string s in strArr)
                    {
                        floor = ""; sqm = 0; totShrSqm = 0; totSqm = 0;
                        string str = s.Replace("&nbsp;", string.Empty).Trim();
                        if (str == string.Empty) continue;

                        Match match = Regex.Match(str, bldgPtrn, RegexOptions.Multiline);
                        if (match.Success)
                        {
                            floor = match.Groups[1].Value;
                            totSqm = Convert.ToDouble(match.Groups[3].Value.Replace(",", string.Empty));
                            sqm = totSqm;
                            dtB.Rows.Add(lsNo, 0, floor, sqm, totShrSqm, "", match.Value, "");
                        }
                        else
                        {
                            match = Regex.Match(str, @"(\d[\d\.\,]+)[\s]*㎡", RegexOptions.Multiline);
                            if (match.Success)
                            {
                                totSqm = Convert.ToDouble(match.Groups[1].Value.Replace(",", string.Empty));
                                sqm = totSqm;
                                dtB.Rows.Add(lsNo, 0, floor, sqm, totShrSqm, "", match.Value, "");
                            }
                        }
                    }
                    Match matchShr = Regex.Match(dtlStr, @"매각지분[ ]*:[ ]*(.*)", rxOptS);
                    if (matchShr.Success)
                    {
                        shrStr0 = matchShr.Groups[1].Value;
                        shrStr0 = Regex.Replace(shrStr0, @"제시외.*", string.Empty, rxOptS).Trim();
                        MatchCollection mc1 = Regex.Matches(shrStr0, frtnPtrn1, rxOptS);
                        MatchCollection mc2 = Regex.Matches(shrStr0, frtnPtrn2, rxOptS);
                        foreach (DataRow row in dtB.Rows)
                        {
                            if (row["lsNo"].ToString() == lsNo)
                            {
                                if (mc1 == null && mc2 == null) continue;
                                totShrSqm = Convert.ToDouble(row["sqm"]);
                                sqm = 0;
                                if (mc1 != null)
                                {
                                    foreach (Match m in mc1)
                                    {
                                        dt = Convert.ToDouble(m.Groups[1].Value);
                                        nt = Convert.ToDouble(m.Groups[2].Value);
                                        sqm += totShrSqm * nt / dt;
                                    }
                                }
                                if (mc2 != null)
                                {
                                    foreach (Match m in mc2)
                                    {
                                        dt = Convert.ToDouble(m.Groups[2].Value);
                                        nt = Convert.ToDouble(m.Groups[1].Value);
                                        sqm += totShrSqm * nt / dt;
                                    }
                                }
                                if (mc1.Count == 0 && mc2.Count == 0)
                                {
                                    sqm = totShrSqm;
                                    totShrSqm = 0;
                                    shrStr = string.Empty;
                                }
                                else
                                {
                                    if (totShrSqm == sqm)
                                    {
                                        totShrSqm = 0;
                                        shrStr = string.Empty;
                                    }
                                    else
                                    {
                                        shrStr = string.Format("전체면적 {0}㎡중 {1}", totShrSqm, shrStr0);
                                    }
                                }
                                row["sqm"] = sqm;
                                row["totShrSqm"] = totShrSqm;
                                row["shrStr"] = shrStr;
                            }
                        }
                    }

                    //제시외
                    if (etcStr != string.Empty)
                    {
                        MatchCollection mc = Regex.Matches(etcStr, etcPtrn1, rxOptM);
                        if (mc != null)
                        {
                            foreach (Match m in mc)
                            {
                                use = m.Groups[1].Value.Trim();
                                strt = m.Groups[2].Value.Trim();
                                area = m.Groups[3].Value.Trim();
                                if (use.Contains("기계기구"))
                                {
                                    macExist = true;
                                    continue;
                                }
                                if (m.Value.Contains("수목") || m.Value.Contains("나무") || m.Value.Contains("관정"))
                                {
                                    area = string.Empty;
                                }
                                else
                                {
                                    if (Regex.IsMatch(strt, macPtrn, rxOptM))
                                    {
                                        macExist = true;
                                        continue;
                                    }
                                }
                                dtE.Rows.Add(lsNo, use, strt, area);
                            }
                        }
                        else
                        {
                            mc = Regex.Matches(etcStr, etcPtrn2, rxOptM);
                            if (mc != null)
                            {
                                foreach (Match m in mc)
                                {
                                    use = m.Groups[1].Value.Trim();
                                    strt = m.Groups[2].Value.Trim();
                                    if (use.Contains("기계기구"))
                                    {
                                        macExist = true;
                                        continue;
                                    }
                                    if (m.Value.Contains("수목") || m.Value.Contains("나무") || m.Value.Contains("관정"))
                                    {
                                        area = string.Empty;
                                    }
                                    else
                                    {
                                        if (Regex.IsMatch(strt, macPtrn, rxOptM))
                                        {
                                            macExist = true;
                                            continue;
                                        }
                                    }
                                    dtE.Rows.Add(lsNo, use, strt, "");
                                }
                            }
                        }
                    }
                }
                else if (lsType == "집합건물")
                {
                    bldgSection = string.Empty; landSection = string.Empty;
                    catNm = ""; catCd = ""; frtn = "";

                    int eIndex = dtlStr.IndexOf("제시외");
                    if (eIndex > -1)
                    {
                        etcStr = dtlStr.Substring(eIndex);
                    }

                    Match match = Regex.Match(dtlStr, @"전유부분의 건물의 표시(.*)대지권의 목적인 토지의 표시(.*)", rxOptS);
                    if (match.Success)
                    {
                        bldgSection = match.Groups[1].Value.Trim();
                        landSection = match.Groups[2].Value.Trim();
                    }
                    else
                    {
                        match = Regex.Match(dtlStr, @"전유부분의 건물의 표시(.*)", rxOptS);
                        if (match.Success)
                        {
                            bldgSection = match.Groups[1].Value.Trim();
                        }
                    }

                    if (bldgSection == string.Empty && landSection == string.Empty) continue;

                    if (bldgSection != string.Empty && landSection != string.Empty)
                    {
                        Match match3 = Regex.Match(dtlStr, @"건물의[ ]*번호[ ]*:[ ]*(.*?)구조[ ]*:[ ]*(.*?)면적[ ]*:[ ]*(.*?)대지권의 목적인 토지의 표시", rxOptS);
                        Match match4 = Regex.Match(dtlStr, @"건물의[ ]*번호[ ]*:[ ]*(.*?)구조[ ]*:[ ]*(.*?)대지권의 목적인 토지의 표시", rxOptS);
                        if (match3.Success)
                        {
                            MatchCollection mc = Regex.Matches(match3.Groups[2].Value + match3.Groups[3].Value, @"(\d[\d\.\,]*)[\s]*㎡", rxOptS);
                            if (mc != null)
                            {
                                foreach (Match m in mc)
                                {
                                    bldgSqm += Convert.ToDouble(m.Groups[1].Value);
                                }
                                dtB.Rows.Add(lsNo, 1, floor, bldgSqm, "", "", "", "");

                            }
                        }
                        else if (match4.Success)
                        {
                            MatchCollection mc = Regex.Matches(match4.Groups[2].Value, @"(\d[\d\.\,]*)[\s]*㎡", rxOptS);
                            if (mc != null)
                            {
                                foreach (Match m in mc)
                                {
                                    bldgSqm += Convert.ToDouble(m.Groups[1].Value);
                                }
                                dtB.Rows.Add(lsNo, 1, floor, bldgSqm, "", "", "", "");
                            }
                        }

                        Match match5 = Regex.Match(dtlStr, @"토[ ]*지[ ]*의[ ]*표시[ ]*:[ ]*(.*?)대지권의 종류[ ]*:[ ]*(.*?)대지권의 비율[ ]*:[ ]*(.*)", rxOptS);
                        Match match6 = Regex.Match(dtlStr, @"토[ ]*지[ ]*의[ ]*표시[ ]*:[ ]*(.*?)매각지분", rxOptS);
                        if (match5.Success)
                        {
                            Dictionary<string, string> dict = LandShrAreaCal(match5.Groups[1].Value, match5.Groups[3].Value);
                            totSqm = Convert.ToDouble(dict["rtTotSqm"]);
                            sqm = Convert.ToDouble(dict["rtSqm"]);
                            frtn = dict["frtn"];
                            catCd = dict["catCd"];
                            dtL.Rows.Add(lsNo, 1, catNm, catCd, "", sqm, "", totSqm, frtn, "");
                        }
                        else if (match6.Success)
                        {
                            Dictionary<string, string> dict = LandShrAreaCal(match6.Groups[1].Value, string.Empty);
                            totSqm = Convert.ToDouble(dict["rtTotSqm"]);
                            sqm = Convert.ToDouble(dict["rtSqm"]);
                            frtn = dict["frtn"];
                            catCd = dict["catCd"];
                            dtL.Rows.Add(lsNo, 1, catNm, catCd, "", sqm, "", totSqm, frtn, "");
                        }
                    }
                    else if (bldgSection != string.Empty)
                    {
                        Match match1 = Regex.Match(dtlStr, @"전유부분의[ ]*건물의[ ]*표시.*건물의[ ]*번호[ ]*:[ ]*(.*?)구조[ ]*:[ ]*(.*?)면적[ ]*:[ ]*(.*?)대지권의 종류[ ]*:[ ]*(.*?)대지권의 비율[ ]*:[ ]*(.*)", rxOptS);
                        Match match2 = Regex.Match(dtlStr, @"전유부분의[ ]*건물의[ ]*표시.*건물의[ ]*번호[ ]*:[ ]*(.*?)구조[ ]*:[ ]*(.*?)대지권의 종류[ ]*:[ ]*(.*?)대지권의 비율[ ]*:[ ]*(.*)", rxOptS);
                        Match match7 = Regex.Match(dtlStr, @"전유부분의[ ]*건물의[ ]*표시.*건물의[ ]*번호[ ]*:[ ]*(.*?)구조[ ]*:[ ]*(.*)", rxOptS);
                        if (match1.Success)
                        {
                            MatchCollection mc = Regex.Matches(match1.Groups[3].Value, @"(\d[\d\.\,]*)[\s]*㎡", rxOptS);
                            if (mc != null)
                            {
                                foreach (Match m in mc)
                                {
                                    bldgSqm += Convert.ToDouble(m.Groups[1].Value);
                                }
                                dtB.Rows.Add(lsNo, 1, floor, bldgSqm, "", "", "", "");
                            }
                            Dictionary<string, string> dict = LandShrAreaCal(string.Empty, match1.Groups[5].Value);
                            totSqm = Convert.ToDouble(dict["rtTotSqm"]);
                            sqm = Convert.ToDouble(dict["rtSqm"]);
                            frtn = dict["frtn"];
                            catCd = dict["catCd"];
                            dtL.Rows.Add(lsNo, 1, catNm, catCd, "", sqm, "", totSqm, frtn, "");
                        }
                        else if (match2.Success)
                        {
                            MatchCollection mc = Regex.Matches(match2.Groups[2].Value, @"(\d[\d\.\,]*)[\s]*㎡", rxOptS);
                            if (mc != null)
                            {
                                foreach (Match m in mc)
                                {
                                    bldgSqm += Convert.ToDouble(m.Groups[1].Value);
                                }
                                dtB.Rows.Add(lsNo, 1, floor, bldgSqm, "", "", "", "");
                            }
                            Dictionary<string, string> dict = LandShrAreaCal(string.Empty, match2.Groups[4].Value);
                            totSqm = Convert.ToDouble(dict["rtTotSqm"]);
                            sqm = Convert.ToDouble(dict["rtSqm"]);
                            frtn = dict["frtn"];
                            catCd = dict["catCd"];
                            dtL.Rows.Add(lsNo, 1, catNm, catCd, "", sqm, "", totSqm, frtn, "");
                        }
                        else if (match7.Success)
                        {
                            MatchCollection mc = Regex.Matches(match7.Groups[2].Value, @"(\d[\d\.\,]*)[\s]*㎡", rxOptS);
                            if (mc != null)
                            {
                                foreach (Match m in mc)
                                {
                                    bldgSqm += Convert.ToDouble(m.Groups[1].Value);
                                }
                                dtB.Rows.Add(lsNo, 1, floor, bldgSqm, "", "", "", "");
                            }
                        }
                    }
                    else
                    {
                        //
                    }

                    if (dtlStr.Contains("매각지분"))
                    {
                        Match match1 = Regex.Match(dtlStr, @"매각지분[ ]*:[ ]*(.*)", rxOptS);
                        shrStr = match1.Groups[1].Value;
                        MatchCollection mc1 = Regex.Matches(shrStr, frtnPtrn1, rxOptS);
                        MatchCollection mc2 = Regex.Matches(shrStr, frtnPtrn2, rxOptS);
                        if (mc1 != null || mc2 != null)
                        {
                            if (dtL.Rows.Count > 0)
                            {
                                totShrSqm = sqm;
                                sqm = 0;
                                if (mc1 != null)
                                {
                                    foreach (Match m in mc1)
                                    {
                                        dt = Convert.ToDouble(m.Groups[1].Value);
                                        nt = Convert.ToDouble(m.Groups[2].Value);
                                        sqm += totShrSqm * nt / dt;
                                    }
                                }
                                if (mc2 != null)
                                {
                                    foreach (Match m in mc2)
                                    {
                                        dt = Convert.ToDouble(m.Groups[2].Value);
                                        nt = Convert.ToDouble(m.Groups[1].Value);
                                        sqm += totShrSqm * nt / dt;
                                    }
                                }
                                if (mc1.Count > 0 || mc2.Count > 0)
                                {
                                    if (totShrSqm != sqm)
                                    {
                                        dtL.Rows[dtL.Rows.Count - 1]["totShrSqm"] = totShrSqm;
                                        dtL.Rows[dtL.Rows.Count - 1]["rtSqm"] = sqm;
                                        dtL.Rows[dtL.Rows.Count - 1]["shrStr"] = string.Format("전체면적 {0}㎡중 {1}", totShrSqm, match1.Groups[1].Value.Trim());
                                    }
                                }
                            }

                            if (dtB.Rows.Count > 0)
                            {
                                totShrSqm = bldgSqm;
                                bldgSqm = 0;
                                if (mc1 != null)
                                {
                                    foreach (Match m in mc1)
                                    {
                                        dt = Convert.ToDouble(m.Groups[1].Value);
                                        nt = Convert.ToDouble(m.Groups[2].Value);
                                        bldgSqm += totShrSqm * nt / dt;
                                    }
                                }
                                if (mc2 != null)
                                {
                                    foreach (Match m in mc2)
                                    {
                                        dt = Convert.ToDouble(m.Groups[2].Value);
                                        nt = Convert.ToDouble(m.Groups[1].Value);
                                        bldgSqm += totShrSqm * nt / dt;
                                    }
                                }
                                if (mc1.Count > 0 || mc2.Count > 0)
                                {
                                    if (totShrSqm == bldgSqm)
                                    {
                                        dtB.Rows[dtB.Rows.Count - 1]["totShrSqm"] = 0;
                                        dtB.Rows[dtB.Rows.Count - 1]["sqm"] = bldgSqm.ToString();
                                        dtB.Rows[dtB.Rows.Count - 1]["shrStr"] = string.Empty;
                                    }
                                    else
                                    {
                                        dtB.Rows[dtB.Rows.Count - 1]["totShrSqm"] = totShrSqm;
                                        dtB.Rows[dtB.Rows.Count - 1]["sqm"] = bldgSqm.ToString();
                                        dtB.Rows[dtB.Rows.Count - 1]["shrStr"] = string.Format("전체면적 {0}㎡중 {1}", totShrSqm, match1.Groups[1].Value.Trim());
                                    }
                                    /*
                                    if (totShrSqm != bldgSqm)
                                    {
                                        dtB.Rows[dtB.Rows.Count - 1]["totShrSqm"] = totShrSqm;
                                        dtB.Rows[dtB.Rows.Count - 1]["sqm"] = bldgSqm.ToString();
                                        dtB.Rows[dtB.Rows.Count - 1]["shrStr"] = string.Format("전체면적 {0}㎡중 {1}", totShrSqm, match1.Groups[1].Value.Trim());
                                    }
                                    */
                                }
                            }
                        }
                    }

                    //총층수
                    eIndex = dtlStr.IndexOf("전유부분의 건물의 표시");
                    if (eIndex > -1)
                    {
                        MatchCollection mc = Regex.Matches(dtlStr.Remove(eIndex), @"(\d+)층", rxOptM);
                        if (mc != null)
                        {
                            Dictionary<string, int> dict = new Dictionary<string, int>();
                            foreach (Match m in mc)
                            {
                                if (!dict.ContainsKey(m.Value)) dict.Add(m.Value, Convert.ToInt32(m.Groups[1].Value));
                            }
                            if (dict.Count > 0)
                            {
                                dtB.Rows[dtB.Rows.Count - 1]["totFlr"] = dict.Values.Max();
                            }
                        }
                    }

                    //제시외
                    if (etcStr != string.Empty)
                    {
                        MatchCollection mc = Regex.Matches(etcStr, etcPtrn1, rxOptM);
                        if (mc != null)
                        {
                            foreach (Match m in mc)
                            {
                                use = m.Groups[1].Value.Trim();
                                strt = m.Groups[2].Value.Trim();
                                area = m.Groups[3].Value.Trim();
                                if (use.Contains("기계기구"))
                                {
                                    macExist = true;
                                    continue;
                                }
                                if (m.Value.Contains("수목") || m.Value.Contains("나무") || m.Value.Contains("관정"))
                                {
                                    area = string.Empty;
                                }
                                else
                                {
                                    if (Regex.IsMatch(strt, macPtrn, rxOptM))
                                    {
                                        macExist = true;
                                        continue;
                                    }
                                }
                                dtE.Rows.Add(lsNo, use, strt, area);
                            }
                        }
                        else
                        {
                            mc = Regex.Matches(etcStr, etcPtrn2, rxOptM);
                            if (mc != null)
                            {
                                foreach (Match m in mc)
                                {
                                    use = m.Groups[1].Value.Trim();
                                    strt = m.Groups[2].Value.Trim();
                                    if (use.Contains("기계기구"))
                                    {
                                        macExist = true;
                                        continue;
                                    }
                                    if (m.Value.Contains("수목") || m.Value.Contains("나무") || m.Value.Contains("관정"))
                                    {
                                        area = string.Empty;
                                    }
                                    else
                                    {
                                        if (Regex.IsMatch(strt, macPtrn, rxOptM))
                                        {
                                            macExist = true;
                                            continue;
                                        }
                                    }
                                    dtE.Rows.Add(lsNo, use, strt, "");
                                }
                            }
                        }
                    }
                }
                else
                {
                    continue;
                }
            }

            sql = "select no, adrs from ta_ls where tid=" + tid;
            DataTable dtLs = db.ExeDt(sql);

            db.Open();
            
            //토지현황            
            foreach (DataRow r in dtL.Rows)
            {
                i++;
                //if (r["multi"].ToString() == "1") continue;
                sql = "insert into ta_land (tid, ls_no, cat_cd, sqm, tot_shr_sqm, rt_sqm, tot_rt_sqm, shr_str) values (@tid, @ls_no, @cat_cd, @sqm, @tot_shr_sqm, @rt_sqm, @tot_rt_sqm, @shr_str)";
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@ls_no", r["lsNo"]));
                sp.Add(new MySqlParameter("@cat_cd", r["catCd"]));
                sp.Add(new MySqlParameter("@sqm", r["sqm"]));
                sp.Add(new MySqlParameter("@tot_shr_sqm", r["totShrSqm"]));
                sp.Add(new MySqlParameter("@rt_sqm", r["rtSqm"]));
                sp.Add(new MySqlParameter("@tot_rt_sqm", r["totRtSqm"]));
                sp.Add(new MySqlParameter("@shr_str", r["shrStr"]));
                db.ExeQry(sql, sp);
                sp.Clear();

                if (string.IsNullOrEmpty(r["sqm"].ToString()) == false) sumLandSqm += Convert.ToDouble(r["sqm"]);                    //총합-토지
                if (string.IsNullOrEmpty(r["totShrSqm"].ToString()) == false) sumLandTotSqm += Convert.ToDouble(r["totShrSqm"]);     //총합-토지지분/대지권지분
                if (string.IsNullOrEmpty(r["rtSqm"].ToString()) == false) sumRtSqm += Convert.ToDouble(r["rtSqm"]);                  //총합-대지권
                if (string.IsNullOrEmpty(r["rtSqm"].ToString()) == false && i == 1) rtTotSqm = Convert.ToDouble(r["totRtSqm"]);      //대지권전체
            }

            //건물현황
            foreach (DataRow r in dtB.Rows)
            {
                //if (r["multi"].ToString() == "1") continue;
                flrCd = "0";
                if (r["floor"]?.ToString() != "")
                {
                    var xFlr = dtFlrCd.Rows.Cast<DataRow>().Where(t => t["flr_nm"].ToString() == r["floor"].ToString()).SingleOrDefault();
                    flrCd = (xFlr == null) ? "0" : xFlr.Field<UInt16>("flr_cd").ToString();
                }
                if (flrCd == "0")
                {
                    var xRow = dtLs.Rows.Cast<DataRow>().Where(t => t["no"].ToString() == r["lsNo"].ToString()).FirstOrDefault();
                    if (xRow != null)
                    {
                        string adrs = xRow["adrs"].ToString();
                        Match match = Regex.Match(adrs, @"\w+층", rxOptM);
                        var xFlr = dtFlrCd.Rows.Cast<DataRow>().Where(t => t["flr_nm"].ToString() == match.Value).SingleOrDefault();
                        flrCd = (xFlr == null) ? "0" : xFlr.Field<UInt16>("flr_cd").ToString();
                    }
                }

                sql = "insert into ta_bldg (tid, ls_no, dvsn, flr, tot_flr, sqm, tot_shr_sqm, shr_str) values (@tid, @ls_no, @dvsn, @flr, @tot_flr, @sqm, @tot_shr_sqm, @shr_str)";
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@ls_no", r["lsNo"]));
                sp.Add(new MySqlParameter("@dvsn", 1));
                sp.Add(new MySqlParameter("@flr", flrCd));
                sp.Add(new MySqlParameter("@tot_flr", r["totFlr"]));
                sp.Add(new MySqlParameter("@sqm", r["sqm"]));
                sp.Add(new MySqlParameter("@tot_shr_sqm", r["totShrSqm"]));
                sp.Add(new MySqlParameter("@shr_str", r["shrStr"]));
                db.ExeQry(sql, sp);
                sp.Clear();

                if (string.IsNullOrEmpty(r["sqm"].ToString()) == false) sumBldgSqm += Convert.ToDouble(r["sqm"]);                //총합-건물
                if (string.IsNullOrEmpty(r["totShrSqm"].ToString()) == false) sumBldgTotSqm += Convert.ToDouble(r["totShrSqm"]); //총합-건물지분
            }

            //제시외건물
            foreach (DataRow r in dtE.Rows)
            {
                sql = "insert into ta_bldg (tid, ls_no, dvsn, sqm, state, struct) values (@tid, @ls_no, @dvsn, @sqm, @state, @struct)";
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@ls_no", r["lsNo"]));
                sp.Add(new MySqlParameter("@dvsn", 2));
                sp.Add(new MySqlParameter("@sqm", r["sqm"].ToString().Trim()));
                sp.Add(new MySqlParameter("@state", r["state"].ToString().Trim()));
                sp.Add(new MySqlParameter("@struct", r["struct"].ToString().Trim()));
                db.ExeQry(sql, sp);
                sp.Clear();
            }

            //제시외-기계/기구 존재시
            if (macExist)
            {
                sql = "insert into ta_bldg set tid=" + tid + ", dvsn=3, state='기계/기구'";
                db.ExeQry(sql);
            }

            //목록구분이 집합건물만 있는 경우 필지수 계산
            if (lsType == "집합건물" && ncTr.Count == 1 && landSection != string.Empty)
            {
                MatchCollection mc = Regex.Matches(landSection, @"(\d[\d\.\,]*)[\s]*㎡", rxOptS);
                if (mc != null)
                {
                    if (mc.Count > 1)
                    {
                        sql = "update ta_list set lot_cnt='" + mc.Count + "' where tid=" + tid;
                        db.ExeQry(sql);
                    }
                }
            }

            sql = "update ta_list set land_sqm=@land_sqm, land_tot_sqm=@land_tot_sqm, bldg_sqm=@bldg_sqm, bldg_tot_sqm=@bldg_tot_sqm, rt_sqm=@rt_sqm, rt_tot_sqm=@rt_tot_sqm where tid=" + tid;
            sp.Add(new MySqlParameter("@land_sqm", double.IsInfinity(sumLandSqm) ? 0 : sumLandSqm));
            sp.Add(new MySqlParameter("@land_tot_sqm", double.IsInfinity(sumLandTotSqm) ? 0 : sumLandTotSqm));
            sp.Add(new MySqlParameter("@bldg_sqm", double.IsInfinity(sumBldgSqm) ? 0 : sumBldgSqm));
            sp.Add(new MySqlParameter("@bldg_tot_sqm", double.IsInfinity(sumBldgTotSqm) ? 0 : sumBldgTotSqm));
            sp.Add(new MySqlParameter("@rt_sqm", double.IsInfinity(sumRtSqm) ? 0 : sumRtSqm));
            sp.Add(new MySqlParameter("@rt_tot_sqm", double.IsInfinity(rtTotSqm) ? 0 : rtTotSqm));
            db.ExeQry(sql, sp);
            sp.Clear();

            db.Close();
        }

        /// <summary>
        /// 매물 명세 추출 테스트
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkDpslStmtAnaly_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            int tdCnt = 0, i = 0, eqIdx = 0, lsCnt = 0;
            UInt64 _deposit = 0, _mMoney = 0, _tMoney = 0;
            string sql, tid, html, cat1, cat2, cat3, lsNo = "0", rgstNote = "", leasNote = "";
            string prsn, prsn2, part, src, invType, useType, useCd = "", term, deposit, mMoney, tMoney, mvDt, fxDt, shrDt;
            string Nm, Nm2, prevNm;
            bool findFlag = false, highFlag = false, shrDtFlag = false, jnsFlag = false, imcFlag = false;

            string ptrnMny1 = @"^1[차: ]+(.*?)[원]*[,/ ]+2[차: ]+(.*)";
            string ptrnMny2 = @"([\d,]{3,})원\(1차\)[, ]+([\d,]{3,})원\(2차";
            string ptrnMny3 = @"\d{4}[. ]\d+[. ]\d+[. ]*(.*?)원[, ]+\d{4}[. ]\d+[. ]\d+[. ]*(.*?)원";

            tid = lnkTid.Text;

            //물건 종별
            sql = "select cat1, cat2, cat3 from ta_list where tid=" + tid;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            cat1 = dr["cat1"].ToString();
            cat2 = dr["cat2"].ToString();
            cat3 = dr["cat3"].ToString();
            dr.Close();
            db.Close();

            //물건 목록
            sql = "select no, adrs, dvsn from ta_ls where tid=" + tid;
            DataTable dtM = db.ExeDt(sql);

            HAPDoc doc = new HAPDoc();

            //매물명세서
            DataTable dtS = new DataTable();
            dtS.Columns.Add("prsn", typeof(string));    //점유자 성명
            dtS.Columns.Add("part", typeof(string));    //점유 부분
            dtS.Columns.Add("src", typeof(string));     //정보 출처 구분
            dtS.Columns.Add("ust", typeof(string));     //점유의 권원
            dtS.Columns.Add("term", typeof(string));    //임대차기간(점유기간)
            dtS.Columns.Add("deposit", typeof(string)); //보증금
            dtS.Columns.Add("mMoney", typeof(string));   //차임
            dtS.Columns.Add("mvDt", typeof(string));    //전입신고 일자,사업자등록 신청일자
            dtS.Columns.Add("fxDt", typeof(string));    //확정 일자
            dtS.Columns.Add("shrDt", typeof(string));   //배당 요구여부(배당요구일자)
            dtS.Columns.Add("highFlag", typeof(bool));  //상위 레벨 문서 포함여부

            //임차인현황(ta_leas)
            DataTable dtL = new DataTable();
            dtL.Columns.Add("idx", typeof(string));     //idx
            dtL.Columns.Add("lsNo", typeof(string));    //목록 번호
            dtL.Columns.Add("prsn", typeof(string));    //점유인
            dtL.Columns.Add("invType", typeof(string)); //당사자 구분
            dtL.Columns.Add("part", typeof(string));    //점유 부분
            dtL.Columns.Add("useType", typeof(string)); //점유의 근원
            dtL.Columns.Add("useCd", typeof(string));   //용도코드
            dtL.Columns.Add("term", typeof(string));    //점유 기간
            dtL.Columns.Add("deposit", typeof(string)); //보증(전세)금
            dtL.Columns.Add("mMoney", typeof(string));  //월세(차임)
            dtL.Columns.Add("tMoney", typeof(string));  //사글세(차임)
            dtL.Columns.Add("tMnth", typeof(string));   //사글세 개월수
            dtL.Columns.Add("biz", typeof(string));     //사업자 여부
            dtL.Columns.Add("mvDt", typeof(string));    //전입신고 일자,사업자등록 신청일자
            dtL.Columns.Add("fxDt", typeof(string));    //확정 일자
            dtL.Columns.Add("shrDt", typeof(string));   //배당 신청일자
            dtL.Columns.Add("note", typeof(string));    //기타

            sql = "select *, date_format(mv_dt,'%Y-%m-%d') as mvDt, date_format(fx_dt,'%Y-%m-%d') as fxDt, date_format(shr_dt,'%Y-%m-%d') as shrDt from ta_leas where tid=" + tid + " order by prsn";            
            db.Open();
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                DataRow row = dtL.NewRow();
                row["idx"] = dr["idx"].ToString();
                row["lsNo"] = dr["ls_no"].ToString();
                row["prsn"] = dr["prsn"].ToString();
                row["invType"] = dr["inv_type"].ToString();
                row["part"] = dr["part"].ToString();
                row["useType"] = dr["use_type"].ToString();
                row["useCd"] = dr["use_cd"].ToString();
                row["term"] = dr["term"].ToString();
                row["deposit"] = dr["deposit"].ToString();
                row["mMoney"] = dr["m_money"].ToString();
                row["tMoney"] = dr["t_money"].ToString();
                row["tMnth"] = dr["t_mnth"].ToString();
                row["biz"] = dr["biz"].ToString();
                row["mvDt"] = dr["mvDt"].ToString();
                row["fxDt"] = dr["fxDt"].ToString();
                row["shrDt"] = dr["shrDt"].ToString();
                row["note"] = dr["note"].ToString();
                dtL.Rows.Add(row);
            }
            dr.Close();
            db.Close();

            //임차인현황-원본 복사(ta_leas)
            DataTable dtC = dtL.Copy();

            sql = "select new from db_tank.ta_dpsl_html where tid=" + tid;
            db.Open();
            dr = db.ExeRdr(sql);
            if (dr.HasRows == false)
            {
                dr.Close();
                db.Close();
                MessageBox.Show("추출할 수 없는 물건 입니다.");
                return;
            }
            dr.Read();
            html = dr["new"].ToString();
            dr.Close();
            db.Close();
                        
            doc.LoadHtml(html);
            if (cat1 == "30" || cat2 == "4010")
            {
                HtmlNode ndTh = doc.DocumentNode.SelectSingleNode("//table[@summary='매각물건명세서 기본정보 표']/tr/th[contains(text(), '최선순위 설정일자')]");
                if (ndTh != null)
                {
                    rgstNote = ndTh.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                    if (rgstNote != string.Empty)
                    {
                        rgstNote = "▶최선순위설정일자: " + rgstNote;
                    }
                }
                //MessageBox.Show(rgstNote);
            }

            HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='매각물건명세서 상세표']/tbody/tr");
            foreach (HtmlNode tr in ncTr)
            {
                highFlag = true;
                prsn = ""; part = ""; src = ""; useType = ""; term = ""; deposit = ""; mMoney = ""; mvDt = ""; fxDt = ""; shrDt = "";

                HtmlNodeCollection ncTd = tr.SelectNodes("./td");
                tdCnt = ncTd.Count;
                if (tdCnt == 10)
                {
                    prsn = ncTd[0].InnerText.Trim();
                    part = ncTd[1].InnerText.Trim();
                    src = ncTd[2].InnerText.Trim();
                    useType = ncTd[3].InnerText.Trim();
                    term = ncTd[4].InnerText.Trim();
                    deposit = ncTd[5].InnerText.Trim();
                    mMoney = ncTd[6].InnerText.Trim();
                    mvDt = ncTd[7].InnerText.Trim();
                    fxDt = ncTd[8].InnerText.Trim();
                    shrDt = ncTd[9].InnerText.Trim();
                    if (ncTd[0].OuterHtml.Contains("rowspan=\"1\"") && src.Contains("현황조사"))
                    {
                        highFlag = false;
                    }
                }
                else if (tdCnt == 9)
                {
                    part = ncTd[0].InnerText.Trim();
                    src = ncTd[1].InnerText.Trim();
                    useType = ncTd[2].InnerText.Trim();
                    term = ncTd[3].InnerText.Trim();
                    deposit = ncTd[4].InnerText.Trim();
                    mMoney = ncTd[5].InnerText.Trim();
                    mvDt = ncTd[6].InnerText.Trim();
                    fxDt = ncTd[7].InnerText.Trim();
                    shrDt = ncTd[8].InnerText.Trim();
                }
                else if (tdCnt == 1)
                {
                    leasNote = ncTd[0].InnerHtml;
                    leasNote = leasNote.Replace(@"&lt; 비고 &gt; &nbsp;", string.Empty);
                    leasNote = leasNote.Replace("<br>", "\r\n* ").Trim();
                    continue;
                }
                else
                {
                    continue;
                }

                if (Regex.IsMatch(mvDt, "^무|^없음|불능|불명|무상|무임|미신고|미등록|미전입|무료|이사감|미필|안받았음|미존재|확인불능|퇴사|받지[ ]*않음|전입[ ]*안됨|사업자[ ]*없음")) mvDt = "0000-00-01";
                else if (Regex.IsMatch(mvDt, "미상|미확인|해당[ ]*없음") || string.IsNullOrEmpty(mvDt)) mvDt = "0000-00-03";
                else mvDt = getDateParse(mvDt);

                if (Regex.IsMatch(fxDt, "^무|^없음|불능|불명|무상|무임|미신고|미등록|미전입|무료|이사감|미필|안받았음|미존재|확인불능|퇴사|받지[ ]*않음|전입[ ]*안됨|사업자[ ]*없음")) fxDt = "0000-00-01";
                else if (Regex.IsMatch(fxDt, "미상|미확인|해당[ ]*없음") || string.IsNullOrEmpty(fxDt)) fxDt = "0000-00-03";
                else fxDt = getDateParse(fxDt);

                if (string.IsNullOrEmpty(shrDt)) shrDt = "0000-00-01";
                else shrDt = getDateParse(shrDt);

                dtS.Rows.Add(prsn, part, src, useType, term, deposit, mMoney, mvDt, fxDt, shrDt, highFlag);
            }
            //MessageBox.Show(leasNote);

            Nm = string.Empty;
            prevNm = string.Empty;
                        
            foreach (DataRow row in dtS.Rows)
            {
                findFlag = false;
                eqIdx = -1;
                mMoney = "0"; tMoney = "0";

                prsn = row["prsn"].ToString();
                src = row["src"].ToString();
                highFlag = Convert.ToBoolean(row["highFlag"]);
                if (string.IsNullOrEmpty(prsn) == false) Nm = prsn;

                deposit = row["deposit"].ToString();
                mMoney = row["mMoney"].ToString();
                mMoney = Regex.Replace(mMoney, @"^(매년|년세|연세|연|일년|1년)", string.Empty);

                if (Regex.IsMatch(deposit,@"^없음")) deposit = "1";
                else if (deposit == string.Empty || deposit == "0" || Regex.IsMatch(deposit, @"미상|해당[ ]*없음")) deposit = "3";
                else
                {
                    if (Regex.IsMatch(deposit, ptrnMny1)) deposit = MoneyChk(Regex.Match(deposit, ptrnMny1).Groups[2].Value);
                    else if (Regex.IsMatch(deposit, ptrnMny2)) deposit = MoneyChk(Regex.Match(deposit, ptrnMny2).Groups[2].Value);
                    else if (Regex.IsMatch(deposit, ptrnMny3)) deposit = MoneyChk(Regex.Match(deposit, ptrnMny3).Groups[2].Value);
                    else deposit = MoneyChk(deposit);
                }
                if (deposit == "") deposit = "0";
                
                if (Regex.IsMatch(mMoney, ptrnMny1)) mMoney = MoneyChk(Regex.Match(mMoney, ptrnMny1).Groups[2].Value);
                else if (Regex.IsMatch(mMoney, ptrnMny2)) mMoney = MoneyChk(Regex.Match(mMoney, ptrnMny2).Groups[2].Value);
                else if (Regex.IsMatch(mMoney, ptrnMny3)) mMoney = MoneyChk(Regex.Match(mMoney, ptrnMny3).Groups[2].Value);
                else mMoney = MoneyChk(mMoney);

                if (Regex.IsMatch(row["mMoney"].ToString(), @"^(매년|년세|연세|연|일년|1년)"))
                {
                    tMoney = mMoney;
                    mMoney = "0";
                }
                //if (tMoney == "") tMoney = "0";
                //if (mMoney == "") mMoney = "0";

                mvDt = row["mvDt"].ToString();
                fxDt = row["fxDt"].ToString();
                shrDt = row["shrDt"].ToString();
                                
                foreach (DataRow r in dtL.Rows)
                {
                    prsn2 = Regex.Replace(r["prsn"].ToString(), @"\(주\)|주식회사|\s+", string.Empty);
                    if (prsn2.Contains("(")) prsn2 = prsn2.Remove(prsn2.IndexOf("("));
                    
                    Nm2 = Regex.Replace(Nm, @"\(주\)|주식회사|\s+", string.Empty);
                    if (Nm2.Contains("(")) Nm2 = Nm2.Remove(Nm2.IndexOf("("));

                    if (r["prsn"].ToString() == Nm || (Nm2.Length >= 2 && prsn2 == Nm2))
                    {
                        findFlag = true;
                        eqIdx = dtL.Rows.IndexOf(r);

                        //MessageBox.Show(string.Format("진입 -> Nm:{0} / Prsn:{1}", Nm, r["prsn"]));
                        shrDtFlag = (r["invType"].ToString().Contains("등기자")) ? false : true;       //삭제예정
                        jnsFlag= (r["invType"].ToString().Contains("전세권등기자")) ? true : false;
                        imcFlag = (r["invType"].ToString().Contains("임차권등기자")) ? true : false;

                        if (src.Contains("현황조사"))
                        {
                            continue;
                        }

                        r["part"] = row["part"];
                        r["useType"] = row["ust"];
                        r["term"] = row["term"];
                        r["deposit"] = deposit;                        
                        r["tMnth"] = "";
                        
                        if (jnsFlag)
                        {
                            if (mvDt.Contains("0000-00") == false) r["mvDt"] = mvDt;
                            if (fxDt.Contains("0000-00") == false) r["fxDt"] = fxDt;
                            if (r["shrDt"].ToString() == "0000-00-00" && shrDt != "0000-00-01") r["shrDt"] = shrDt;
                            r["mMoney"] = mMoney;
                            r["tMoney"] = tMoney;
                        }
                        else if (imcFlag)
                        {
                            if (mvDt.Contains("0000-00") == false)
                            {
                                if (r["mvDt"].ToString() != "0000-00-00")
                                {
                                    if (Convert.ToDateTime(r["mvDt"]) > Convert.ToDateTime(mvDt)) r["mvDt"] = mvDt;
                                }
                            }
                            if (fxDt.Contains("0000-00") == false)
                            {
                                if (r["fxDt"].ToString() != "0000-00-00")
                                {
                                    if (Convert.ToDateTime(r["fxDt"]) > Convert.ToDateTime(fxDt)) r["fxDt"] = fxDt;
                                }
                            }
                            if (r["shrDt"].ToString() == "0000-00-00" && shrDt != "0000-00-01") r["shrDt"] = shrDt;
                            if (mMoney != "" && mMoney != "0") r["mMoney"] = mMoney;
                            if (tMoney != "" && tMoney != "0") r["tMoney"] = tMoney;
                        }
                        else
                        {
                            r["mvDt"] = mvDt;
                            r["fxDt"] = fxDt;
                            r["shrDt"] = shrDt;
                            r["mMoney"] = mMoney;
                            r["tMoney"] = tMoney;
                        }

                        if (src.Contains("권리신고")) break;
                    }
                }

                //현황조사만 있는 경우
                if (findFlag && highFlag == false && (eqIdx > -1) && src.Contains("현황조사"))
                {
                    //MessageBox.Show(Nm);
                    shrDtFlag = (dtL.Rows[eqIdx]["invType"].ToString().Contains("등기자")) ? false : true;

                    dtL.Rows[eqIdx]["term"] = row["term"];
                    dtL.Rows[eqIdx]["deposit"] = deposit;
                    dtL.Rows[eqIdx]["mMoney"] = mMoney;
                    dtL.Rows[eqIdx]["tMoney"] = tMoney;
                    dtL.Rows[eqIdx]["tMnth"] = "";
                    dtL.Rows[eqIdx]["mvDt"] = mvDt;
                    dtL.Rows[eqIdx]["fxDt"] = fxDt;
                    if (shrDtFlag) dtL.Rows[eqIdx]["shrDt"] = shrDt;

                    //임차인현황 DB원본(복제본) 갱신
                    var xRow = dtC.Rows.Cast<DataRow>().Where(t => t["idx"].ToString() == dtL.Rows[eqIdx]["idx"]?.ToString()).FirstOrDefault();
                    if (xRow != null)
                    {
                        xRow["term"] = row["term"];
                        xRow["deposit"] = deposit;
                        xRow["mMoney"] = mMoney;
                        xRow["tMoney"] = tMoney;
                        xRow["tMnth"] = "";
                        xRow["mvDt"] = mvDt;
                        xRow["fxDt"] = fxDt;
                        if (shrDtFlag) xRow["shrDt"] = shrDt;
                    }
                }
                
                if (findFlag == false)
                {
                    useType = row["ust"].ToString();
                    if (Regex.IsMatch(useType, @"주거(임차인|임차권자|점유자|전세권자|주택임차권자)|주민등록|(전입신고)+.*임차인|미확인[ ]*전입자|전입자점유자")) useType = "주거";
                    else if (Regex.IsMatch(useType, @"주거[ ]*및[ ]*.*[^농지](임차인|점유자)")) useType = "주거및점포";
                    else if (Regex.IsMatch(useType, @"점포(임차인|점유자|전세권자|임차권자)|(시설)+.*임차인")) useType = "점포";
                    else if (Regex.IsMatch(useType, @"공장(임차인|점유자|전세권자)|공장[ ]*및[ ]*사무실[ ]*임차인")) useType = "공장";
                    else if (Regex.IsMatch(useType, @"^사무[실소등추정 ]+(임차인|점유자)|관리사무소")) useType = "사무실";
                    else if (Regex.IsMatch(useType, @"^(대지|토지|농지|농업|밭|과수원|경작|재배|수목)(임대)*(임대차)*(임차인|점유자)|전\([\w ]+\)임차인|야적장임차인|재배임차인|(토지|건부지)점유자|전[ ]*및[ ]*온실")) useType = "토지";
                    else if (Regex.IsMatch(useType, @"미상")) useType = "미상";

                    if (useType == "" || useType == "미상") useCd = "10";
                    else if (useType == "채무자(소유자)점유") useCd = "7";
                    else if (useType == "주거") useCd = "1";
                    else if (useType == "점포") useCd = "2";
                    else if (useType == "공장") useCd = "8";
                    else if (useType == "주거및점포") useCd = "4";
                    else if (useType == "사무실") useCd = "3";
                    else if (useType == "토지") useCd = "13";
                    else if (useType == "기타-미상")
                    {
                        if (cat3 == "201013" || cat3 == "201014" || cat3 == "201015") useCd = "1";
                    }
                    else useCd = "0";

                    lsCnt = dtM.Rows.Count;

                    if (lsCnt == 0) lsNo = "0";
                    else if (lsCnt == 1)
                    {
                        lsNo = dtM.Rows[0]["no"].ToString();
                    }
                    else
                    {
                        if (Regex.IsMatch(row["part"].ToString(), @"\d+호"))
                        {
                            Match match = Regex.Match(row["part"].ToString(), @"\d+호");
                            var xRow = dtM.Rows.Cast<DataRow>().Where(t => t["adrs"].ToString().Contains(match.Value)).FirstOrDefault();
                            if (xRow != null)
                            {
                                lsNo = xRow["no"].ToString();
                            }
                        }
                        else
                        {
                            lsNo = "0";
                        }

                        if (lsNo == "0" && lsCnt == 2)
                        {
                            if (dtM.Rows[0]["dvsn"].ToString() == "토지" && dtM.Rows[1]["dvsn"].ToString().Contains("건물"))
                            {
                                lsNo = dtM.Rows[1]["no"].ToString();
                            }
                            else if (dtM.Rows[0]["dvsn"].ToString() == "건물" && dtM.Rows[1]["dvsn"].ToString().Contains("토지"))
                            {
                                lsNo = dtM.Rows[0]["no"].ToString();
                            }
                        }
                    }

                    invType = row["ust"].ToString();
                    Match m = Regex.Match(invType, @"전점유자|주택임차권자|전세권자|임차권자|점유자|임차인");
                    if (m.Success) invType = m.Value;
                    else invType = "";

                    DataRow rN = dtL.NewRow();
                    rN["lsNo"] = lsNo;
                    rN["prsn"] = Nm;
                    rN["invType"] = invType;
                    rN["part"] = row["part"];
                    rN["useType"] = row["ust"];
                    rN["useCd"] = useCd;
                    rN["term"] = row["term"];
                    rN["deposit"] = deposit;
                    rN["mMoney"] = mMoney;
                    rN["tMoney"] = tMoney;
                    rN["tMnth"] ="";
                    rN["biz"] = (useCd == "2" || useCd == "3" || useCd == "8" || useCd == "9") ? "1" : "0";
                    rN["mvDt"] = mvDt;
                    rN["fxDt"] = fxDt;
                    rN["shrDt"] = shrDt;
                    dtL.Rows.Add(rN);
                }
                prevNm = Nm;
            }

            //임차인현황 DB원본(복제본)과 최종 dtL 비교 후 변동 값 기타에 기록
            //MessageBox.Show(dtL.Rows.Count.ToString());
            List<string> lsNote = new List<string>();
            foreach (DataRow r in dtC.Rows)
            {
                lsNote.Clear();
                var xRow = dtL.Rows.Cast<DataRow>().Where(t => t["idx"].ToString() == r["idx"]?.ToString()).FirstOrDefault();
                if (xRow != null)
                {
                    if (r["deposit"].ToString() != xRow["deposit"].ToString() && r["deposit"].ToString() != "0")
                    {
                        _deposit = Convert.ToUInt64(r["deposit"]);
                        if (_deposit > 10000) lsNote.Add(string.Format("보:{0}만원", string.Format("{0:N0}", (_deposit / 10000))));
                        else lsNote.Add(string.Format("보:{0}원", r["deposit"]));
                    }
                    if (r["mMoney"].ToString() != xRow["mMoney"].ToString() && r["mMoney"].ToString() != "0")
                    {
                        _mMoney = Convert.ToUInt64(r["mMoney"]);
                        if(_mMoney > 10000) lsNote.Add(string.Format("차:{0}만원", (_mMoney / 10000)));
                        else lsNote.Add(string.Format("차:{0}원", r["mMoney"]));
                    }
                    if (r["tMoney"].ToString() != xRow["tMoney"].ToString() && r["tMoney"].ToString() != "0")
                    {
                        _tMoney = Convert.ToUInt64(r["tMoney"]);
                        if (_tMoney > 10000) lsNote.Add(string.Format("차:{0}만원", (_tMoney / 10000)));
                        else lsNote.Add(string.Format("차:{0}원", r["tMoney"]));
                    }

                    if (r["mvDt"].ToString() != xRow["mvDt"].ToString() && r["mvDt"].ToString() != "0000-00-00")
                    {
                        if(r["biz"].ToString()=="1") lsNote.Add(string.Format("사:{0}", r["mvDt"]));
                        else lsNote.Add(string.Format("전:{0}", r["mvDt"]));
                    }
                    if (r["fxDt"].ToString() != xRow["fxDt"].ToString() && r["fxDt"].ToString() != "0000-00-00") lsNote.Add(string.Format("확:{0}", r["fxDt"]));
                    //if (r["shrDt"].ToString() != xRow["shrDt"].ToString() && r["shrDt"].ToString() != "0000-00-00") lsNote.Add(string.Format("배:{0}", r["shrDt"]));
                    if (lsNote.Count > 0)
                    {
                        xRow["note"] = (xRow["note"].ToString() + "\r\n[현황서상 " + string.Join(", ", lsNote.ToArray()) + "]").Trim();
                    }
                }
            }            

            //--------------------------------------------------------------------------------------------------------------//

            //임차인현황 그리드뷰와 비교
            txtRgstNote.Text = (txtRgstNote.Text + "\r\n" + rgstNote).Trim();   //등기부 권리관계 기타
            txtLeasNote.Text = (txtLeasNote.Text + "\r\n" + leasNote).Trim();   //임차인 기타

            foreach (DataGridViewRow r in dgT.Rows)
            {
                var xRow = dtL.Rows.Cast<DataRow>().Where(t => t["idx"].ToString() == r.Cells["dgT_Idx"].Value?.ToString()).FirstOrDefault();
                if (xRow != null)
                {
                    if (UInt64.TryParse(xRow["deposit"].ToString(), out _deposit) == false) _deposit = 0;
                    if (UInt64.TryParse(xRow["mMoney"].ToString(), out _mMoney) == false) _mMoney = 0;
                    if (UInt64.TryParse(xRow["tMoney"].ToString(), out _tMoney) == false) _tMoney = 0;

                    r.Cells["dgT_Part"].Value = xRow["part"];
                    r.Cells["dgT_Term"].Value = xRow["term"];
                    r.Cells["dgT_Deposit"].Value = string.Format("{0:N0}", _deposit);
                    r.Cells["dgT_MMoney"].Value = string.Format("{0:N0}", _mMoney);
                    r.Cells["dgT_TMoney"].Value = string.Format("{0:N0}", _tMoney);
                    r.Cells["dgT_MvDt"].Value = xRow["mvDt"];
                    r.Cells["dgT_FxDt"].Value = xRow["fxDt"];
                    r.Cells["dgT_ShrDt"].Value = xRow["shrDt"];
                    r.Cells["dgT_Note"].Value = xRow["note"];
                }
            }

            var xRows = dtL.Rows.Cast<DataRow>().Where(t => string.IsNullOrEmpty(t["idx"]?.ToString()));
            if (xRows != null)
            {
                foreach (DataRow xRow in xRows)
                {
                    if (UInt64.TryParse(xRow["deposit"].ToString(), out _deposit) == false) _deposit = 0;
                    if (UInt64.TryParse(xRow["mMoney"].ToString(), out _mMoney) == false) _mMoney = 0;
                    if (UInt64.TryParse(xRow["tMoney"].ToString(), out _tMoney) == false) _tMoney = 0;

                    i = dgT.Rows.Add();
                    dgT["dgT_LsNo", i].Value = xRow["lsNo"];
                    dgT["dgT_Prsn", i].Value = xRow["prsn"];
                    dgT["dgT_InvType", i].Value = xRow["invType"];
                    dgT["dgT_Part", i].Value = xRow["part"];
                    dgT["dgT_Term", i].Value = xRow["term"];
                    dgT["dgT_Deposit", i].Value = string.Format("{0:N0}", _deposit);
                    ((DataGridViewComboBoxCell)dgT["dgT_UseCd", i]).Value = Convert.ToByte(xRow["useCd"]);
                    dgT["dgT_MMoney", i].Value = string.Format("{0:N0}", _mMoney);
                    dgT["dgT_TMoney", i].Value = string.Format("{0:N0}", _tMoney);
                    dgT["dgT_ChkBiz", i].Value = xRow["biz"];
                    dgT["dgT_MvDt", i].Value = xRow["mvDt"];
                    dgT["dgT_FxDt", i].Value = xRow["fxDt"];
                    dgT["dgT_ShrDt", i].Value = xRow["shrDt"];
                }
            }
            //MessageBox.Show("OK");
        }

        /// <summary>
        /// [참고사항] 선택구문 강조
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkEmp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string etcNote,selTxt;

            LinkLabel lnkLbl = sender as LinkLabel;

            selTxt = txtEtcNote.SelectedText.Trim();
            etcNote = txtEtcNote.Text;
            if (selTxt == string.Empty || etcNote == string.Empty) return;

            if(lnkLbl == lnkEmpBold)
                txtEtcNote.Text = etcNote.Replace(selTxt, string.Format("<b>{0}</b>", selTxt));
            else if(lnkLbl==lnkEmpBlue)
                txtEtcNote.Text = etcNote.Replace(selTxt, string.Format("<font color='blue'>{0}</font>", selTxt));
            else if (lnkLbl == lnkEmpRed)
                txtEtcNote.Text = etcNote.Replace(selTxt, string.Format("<font color='red'>{0}</font>", selTxt));
        }

        /// <summary>
        /// 토지e음-예정물건 종별 매칭
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkEum_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string pnu, url, siCd, guCd, dnCd, riCd, mt, mNo, sNo;
            
            siCd = txtSiCd.Text.PadLeft(2, '0');
            guCd = txtGuCd.Text.PadLeft(3, '0');
            dnCd = txtDnCd.Text.PadLeft(3, '0');
            riCd = txtRiCd.Text.PadLeft(2, '0');
            mt = cbxAdrsMt.SelectedValue.ToString();
            mNo = txtAdrsNoM.Text.PadLeft(4, '0');
            sNo = txtAdrsNoS.Text.PadLeft(4, '0');
            pnu = siCd + guCd + dnCd + riCd + mt + mNo + sNo;

            tbcL.SelectedTab = tabWbr3;
            url = "https://www.eum.go.kr/web/ar/lu/luLandDet.jsp?mode=search&selGbn=umd&isNoScr=script&pnu=" + pnu;
            net.Nvgt(wbr3, url);
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
        /// 감평서 OCR 에서 브라우저내 문자열 검색
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnWbrFind_Click(object sender, EventArgs e)
        {
            string text = cbxWbrSrchTxt.Text;

            Button btn = sender as Button;
            if (btn == btnWbrFind1st)
            {
                FindFirst(text);
            }
            else
            {
                FindNext(text);
            }
        }

        private bool FindFirst(string text)
        {
            IHTMLDocument2 doc = (IHTMLDocument2)wbrOcr.Document.DomDocument;
            IHTMLSelectionObject sel = (IHTMLSelectionObject)doc.selection;
            sel.empty(); // get an empty selection, so we start from the beginning
            IHTMLTxtRange rng = (IHTMLTxtRange)sel.createRange();

            string text2 = string.Join(" ", text.ToCharArray().Select(c => c.ToString()).ToArray());
            if (rng.findText(text, 1000000000, 0) || rng.findText(text2, 1000000000, 0))
            {
                rng.select();
                return true;
            }
            return false;
        }

        private bool FindNext(string text)
        {
            IHTMLDocument2 doc = (IHTMLDocument2)wbrOcr.Document.DomDocument;
            IHTMLSelectionObject sel = (IHTMLSelectionObject)doc.selection;
            IHTMLTxtRange rng = (IHTMLTxtRange)sel.createRange();
            rng.collapse(false); // collapse the current selection so we start from the end of the previous range

            string text2 = string.Join(" ", text.ToCharArray().Select(c => c.ToString()).ToArray());
            if (rng.findText(text, 1000000000, 0) || rng.findText(text2, 1000000000, 0))
            {
                rng.select();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 물건 번호가 있는 신건중 물건번호가 없는 동일사건 검출-1
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnPnSrch_Click(object sender, EventArgs e)
        {
            int i = 0;
            string sql, tid, spt, sn1, sn2, pn;
            bool exist;

            dgPn.Rows.Clear();
            dgPr.Rows.Clear();

            sql = "select tid, spt, sn1, sn2, pn from ta_list where (2nd_dt='" + dtp2ndDt.Value.ToShortDateString() + "' or pre_dt='" + dtp2ndDt.Value.ToShortDateString() + "') and pn > 0 group by spt, sn1, sn2";
            DataTable dt = db.ExeDt(sql);

            foreach (DataRow row in dt.Rows)
            {
                tid = row["tid"].ToString();
                spt = row["spt"].ToString();
                sn1 = row["sn1"].ToString();
                sn2 = row["sn2"].ToString();
                pn = row["pn"].ToString();

                sql = "select tid from ta_list where spt='" + spt + "' and sn1='" + sn1 + "' and sn2='" + sn2 + "' and pn=0 limit 1";
                db.Open();
                exist = db.ExistRow(sql);
                db.Close();
                if (!exist) continue;

                i = dgPn.Rows.Add();
                dgPn["dgPn_No", i].Value = (i + 1);
                dgPn["dgPn_Tid", i].Value = tid;
                dgPn["dgPn_SptNm", i].Value = auctCd.FindCsNm(row["spt"].ToString());
                dgPn["dgPn_SN",i].Value= (row["pn"].ToString() == "0") ? string.Format("{0}-{1}", row["sn1"], row["sn2"]) : string.Format("{0}-{1}({2})", row["sn1"], row["sn2"], row["pn"]);
            }
            dgPn.ClearSelection();

            MessageBox.Show("확인 완료");
        }

        /// <summary>
        /// 물건 번호가 있는 신건중 물건번호가 없는 동일사건 검출-2
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgPn_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int i, rowIdx;
            string sql, tid, spt, sn1, sn2, pn;

            dgPr.Rows.Clear();

            rowIdx = e.RowIndex;
            tid = dgPn["dgPn_Tid", rowIdx].Value.ToString();
            sql = "select spt, sn1, sn2, pn from ta_list where tid=" + tid;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            spt = dr["spt"].ToString();
            sn1 = dr["sn1"].ToString();
            sn2 = dr["sn2"].ToString();
            dr.Close();
            db.Close();

            sql = "select tid, spt, sn1, sn2, pn from ta_list where spt='" + spt + "' and sn1='" + sn1 + "' and sn2='" + sn2 + "' and pn=0";
            db.Open();
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                i = dgPr.Rows.Add();
                dgPr["dgPr_No", i].Value = (i + 1);
                dgPr["dgPr_Tid", i].Value = dr["tid"].ToString();
                dgPr["dgPr_SptNm", i].Value = auctCd.FindCsNm(dr["spt"].ToString());
                dgPr["dgPr_SN", i].Value = (dr["pn"].ToString() == "0") ? string.Format("{0}-{1}", dr["sn1"], dr["sn2"]) : string.Format("{0}-{1}({2})", dr["sn1"], dr["sn2"], dr["pn"]);
            }
            dr.Close();
            db.Close();
            dgPr.ClearSelection();
        }

        /// <summary>
        /// 물건 번호가 있는 신건중 물건번호가 없는 동일사건 검출-3
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgPr_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int rowIdx;
            string tid;

            rowIdx = e.RowIndex;
            tid = dgPr["dgPr_Tid", rowIdx].Value.ToString();
            txtSrchTid.Text = tid;
            btnSrch_Click(null, null);
        }

        private string landUnitConvert(string dvsn, string str)
        {
            string landUnitPtrn1 = @"([\d.,]+)평[ ]*((\d+)홉)*[ ]*((\d+)작)*[ ]*((\d+)재)*";  //평홉작재(1-평, 3-홉, 5-작, 7-재)
            string landUnitPtrn2 = @"([\d.,]+)정[ ]*((\d+)단)*[ ]*((\d+)무)*[ ]*(\d+)*보";    //정단무보(1-정, 3-단, 5-무, 6-보)
            double sqm = 0, phj = 0, jdm = 0;

            str = str.Replace(",", string.Empty);
            string dtlStr = str;

            MatchCollection mc = Regex.Matches(str, landUnitPtrn1, rxOptM);
            foreach (Match m in mc)
            {
                phj = 0;
                phj = Convert.ToDouble(string.IsNullOrEmpty(m.Groups[1].Value) ? "0" : m.Groups[1].Value) +
                    (Convert.ToDouble(string.IsNullOrEmpty(m.Groups[3].Value) ? "0" : m.Groups[3].Value) * 0.1) +
                    (Convert.ToDouble(string.IsNullOrEmpty(m.Groups[5].Value) ? "0" : m.Groups[5].Value) * 0.01) +
                    (Convert.ToDouble(string.IsNullOrEmpty(m.Groups[7].Value) ? "0" : m.Groups[7].Value) * 0.001);

                if (phj > 0)
                {
                    //sqm = phj * Convert.ToDouble(3.3058);
                    sqm = phj * Convert.ToDouble(3.305785);
                    dtlStr = dtlStr.Replace(m.Value, string.Format("{0}㎡", sqm));
                }
            }

            if (dvsn == "토지")
            {
                mc = Regex.Matches(str, landUnitPtrn2, rxOptM);
                foreach (Match m in mc)
                {
                    jdm = 0;
                    jdm = (Convert.ToDouble(string.IsNullOrEmpty(m.Groups[1].Value) ? "0" : m.Groups[1].Value) * 3000) +
                        (Convert.ToDouble(string.IsNullOrEmpty(m.Groups[3].Value) ? "0" : m.Groups[3].Value) * 300) +
                        (Convert.ToDouble(string.IsNullOrEmpty(m.Groups[5].Value) ? "0" : m.Groups[5].Value) * 30) +
                        (Convert.ToDouble(string.IsNullOrEmpty(m.Groups[6].Value) ? "0" : m.Groups[6].Value) * 1);
                    if (jdm > 0)
                    {
                        //sqm = jdm * Convert.ToDouble(3.3058);
                        sqm = jdm * Convert.ToDouble(3.305785);
                        dtlStr = dtlStr.Replace(m.Value, string.Format("{0}㎡", sqm));
                    }
                }
            }

            return dtlStr;
        }

        private Dictionary<string, string> LandShrAreaCal(string landStr, string ratioStr)
        {
            string frtn = "", catNm = "", catCd = "";
            double landNo = 0, nt = 0, dt = 0, shrSqm = 0, rtTotSqm = 0, rtSqm = 0;

            Dictionary<string, string> dict = new Dictionary<string, string>();
            //dict["catCd"] = string.Empty;
            dict["catCd"] = "101017";   //대지-집합일 경우 Default
            dict["rtTotSqm"] = string.Empty;
            dict["rtSqm"] = string.Empty;
            dict["totShrSqm"] = string.Empty;
            dict["frtn"] = string.Empty;

            DataTable dtLand = new DataTable();
            dtLand.Columns.Add("landNo");
            dtLand.Columns.Add("catCd");
            dtLand.Columns.Add("area");

            DataTable dtRatio = new DataTable();
            dtRatio.Columns.Add("no");  //no
            dtRatio.Columns.Add("dt");  //분모
            dtRatio.Columns.Add("nt");  //분자

            List<string> lsPtrn = new List<string>();
            lsPtrn.Add(@"(\d+)\.[ ]*(\d+[\.\d]*)[ ]*분의[ ]*(\d+[\.\d]*)");
            lsPtrn.Add(@"(\d+)\.[ ]*(\d+[\.\d]*)/(\d+[\.\d]*)");

            foreach (string ptrn in lsPtrn)
            {
                MatchCollection mc = Regex.Matches(ratioStr, ptrn, rxOptS);
                if (mc != null)
                {
                    foreach (Match m in mc)
                    {
                        if (ptrn.Contains("분의")) dtRatio.Rows.Add(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
                        else dtRatio.Rows.Add(m.Groups[1].Value, m.Groups[3].Value, m.Groups[2].Value);
                    }
                }
            }

            if (landStr == string.Empty)
            {
                if (dtRatio.Rows.Count > 0)
                {
                    dict["rtTotSqm"] = dtRatio.Rows[0]["dt"].ToString();
                    dict["rtSqm"] = dtRatio.Rows[0]["nt"].ToString();
                }
            }
            else
            {
                MatchCollection mc = Regex.Matches(landStr, @"(\d+)\.[ ]*(.*?)[\s]*(" + landPtrn + "|null" + @")[ ]*(\d[\d\.\,]*)[ ]*㎡", rxOptM);
                if (mc != null)
                {
                    foreach (Match m in mc)
                    {
                        landNo = Convert.ToDouble(m.Groups[1].Value);
                        catNm = m.Groups[1].Value.Trim();
                        if (catNm == "대" || catNm == "null") catNm = "대지";
                        var x = from DataRow r in dtCatCd.Rows
                                where r["cat2_cd"].ToString() == "1010" && r["cat3_nm"].ToString() == catNm
                                select r;
                        if (x.Count() > 0) catCd = x.CopyToDataTable().Rows[0]["cat3_cd"].ToString();
                        else catCd = "101017";
                        dtLand.Rows.Add(landNo, catCd, m.Groups[4].Value.Replace(",", string.Empty));
                    }
                    foreach (DataRow row in dtLand.Rows)
                    {
                        rtTotSqm += Convert.ToDouble(row["area"]);
                    }
                }
                if (dtRatio.Rows.Count > 0)
                {
                    dt = Convert.ToDouble(dtRatio.Rows[0]["dt"]);
                    if (rtTotSqm == dt) rtSqm = Convert.ToDouble(dtRatio.Rows[0]["nt"]);
                    else
                    {
                        rtTotSqm = 0;
                        rtSqm = 0;
                        foreach (DataRow row in dtRatio.Rows)
                        {
                            var xRow = dtLand.Rows.Cast<DataRow>().Where(t => t["landNo"].ToString() == row["no"].ToString()).FirstOrDefault();
                            if (xRow != null)
                            {
                                rtTotSqm += Convert.ToDouble(xRow["area"]);
                                rtSqm += (Convert.ToDouble(xRow["area"]) * Convert.ToDouble(row["nt"])) / Convert.ToDouble(row["dt"]);
                            }
                        }
                    }
                }
                dict["rtTotSqm"] = rtTotSqm.ToString();
                dict["rtSqm"] = rtSqm.ToString();
                if (dtLand.Rows.Count > 0)
                {
                    dict["catCd"] = dtLand.Rows[0]["catCd"].ToString();
                }
            }

            return dict;
        }

        /// <summary>
        /// 파일 업로드-파일찾기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

            tid = lnkTid.Text;
            foreach (string fullNm in ofd.FileNames)
            {
                ctgr = string.Empty;
                shr = string.Empty;
                if (fullNm.Contains("T_")) continue;

                rmtNm = getRmtNm(fullNm);
                if (!rmtNm.Contains("오류"))
                {
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
            if (ext == "jpg" || ext == "png" || ext == "gif")
            {
                extType = "img";
            }
            else if (ext == "html" || ext == "pdf")
            {
                extType = "doc";
            }
            else
            {
                return "오류-확장자";
            }

            tid = lnkTid.Text;
            spt = cbxCrtSpt.SelectedValue.ToString();
            sn = string.Format("{0}{1}", cbxSn1.Text, txtSn2.Text.Trim().PadLeft(6, '0'));
            pn = txtPn.Text.PadLeft(4, '0');
            if (cbxFileCtgr.SelectedIndex == 0)
            {
                Match match = Regex.Match(fileNm, @"(\d+)_(\d+)\-*(\d+)*.\w+", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                if (!match.Success)
                {
                    return "오류-파일명";
                }
                //
                //tid = match.Groups[1].Value;
                mainNo = Convert.ToInt32(match.Groups[2].Value);
                subNo = string.IsNullOrEmpty(match.Groups[3].Value) ? 1 : Convert.ToInt32(match.Groups[3].Value);
                if (extType == "img")
                {
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
                else if (extType == "doc")
                {
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

                if (extType == "img")
                {
                    seqNo = mainNo.ToString().PadLeft(4, '0');
                }
                else
                {
                    seqNo = subNo.ToString().PadLeft(4, '0');
                }
            }
            else
            {
                ctgr = cbxFileCtgr.SelectedValue.ToString();
                Match match1 = Regex.Match(fileNm, @"\-(\d+)\.\w+");
                Match match2 = Regex.Match(fileNm, @"^(\d+)\.\w+");
                if (match1.Success) seqNo = match1.Groups[1].Value.PadLeft(4, '0');
                else if(match2.Success) seqNo = match2.Groups[1].Value.PadLeft(4, '0');
                else seqNo = "0000";
            }

            if (extType == "img")
            {
                rmtNm = string.Format("{0}-{1}-{2}-{3}-{4}.{5}", ctgr, spt, sn, pn, seqNo, ext);
            }
            else
            {
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

            return rmtNm;
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
        /// 파일 업로드-FTP/DB 처리
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnUpLoad_Click(object sender, EventArgs e)
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

                    //개별 업로드 기록
                    //
                }
                else
                {
                    row.Cells["dgU_Rslt"].Value = "실패";
                    row.DefaultCellStyle.BackColor = Color.PaleVioletRed;
                }

                Application.DoEvents();
            }

            //파일 정보 갱신
            LoadFileInfo();
        }

        /// <summary>
        /// 파일 삭제
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

            year = cbxSn1.Text;
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
                    bool rslt = ftp1.FtpDelete(rmtFile);
                    //MessageBox.Show(rslt.ToString());
                    ftp1.FtpDelete("T_" + rmtFile);
                }
                dr.Close();

                sql = "delete from " + tbl + " where idx=" + idx;
                db.ExeQry(sql);
                db.Close();
            }

            MessageBox.Show("삭제 되었습니다.");

            //파일 정보 갱신
            LoadFileInfo();
        }

        /// <summary>
        /// 웹브라우저에서 법원문서 파일저장/업로드
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCaDocSave_Click(object sender, EventArgs e)
        {
            string url, html;
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
                        
            spt = cbxCrtSpt.SelectedValue.ToString();
            sn1 = cbxSn1.Text;
            sn2 = txtSn2.Text;
            pn = txtPn.Text;

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
                LoadFileInfo();
            }

            if (!ulFlag)
            {
                MessageBox.Show("파일 업로드 실패");
            }
        }

        /// <summary>
        /// 구폼형식(텍스트형) 데이터 삭제
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOldFormDel_Click(object sender, EventArgs e)
        {
            string sql, tid;

            tid = lnkTid.Text;
            if (tid == string.Empty) return;

            if (MessageBox.Show("구폼 데이터를 삭제 하시겠습니까?", "구폼 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No) return;

            sql = "delete from ta_old_form where tid=" + tid;
            db.Open();
            db.ExeQry(sql);
            db.Close();

            MessageBox.Show("[삭제] 되었습니다.");
            dg_SelectionChanged(null, null);
        }


        /// <summary>
        /// Test-물건현황_이용상태 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkBldgStateTest_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string tid, url, jiwonNm, saNo, maemulSer, html, sql, cat;
            string useStr, state = "";
            int lsCnt = 0;

            HAPDoc doc = new HAPDoc();

            tid = lnkTid.Text;
            
            jiwonNm = auctCd.FindLawNm(cbxCrtSpt.SelectedValue.ToString(), true);
            saNo = string.Format("{0}0130{1}", cbxSn1.Text, txtSn2.Text.PadLeft(6, '0'));
            maemulSer = (txtPn.Text == "0") ? "1" : txtPn.Text;
            url = "http://www.courtauction.go.kr/RetrieveRealEstCarHvyMachineMulDetailInfo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&maemulSer=" + maemulSer;
            html = net.GetHtml(url);
            
            doc.LoadHtml(html);
            HtmlNode tblApsl = doc.DocumentNode.SelectSingleNode("//table[@summary='감정평가요항표']");
            HtmlNode tblLs = doc.DocumentNode.SelectSingleNode("//table[@summary='목록내역 표']");
            if (tblApsl == null) return;

            useStr = Regex.Match(tblApsl.InnerHtml, @"<li><p class=""law_title"">\d+\)[ ]* 이용상태</p>\s+<ul><li><span.*?>(.*?)</span>", rxOptS).Groups[1].Value.Trim();
            if (useStr == string.Empty) return;

            sql = "select B.idx, B.ls_no, B.state, L.dvsn from ta_ls L, ta_bldg B where L.tid=B.tid and L.no=B.ls_no and L.tid=" + tid + " and L.dvsn in ('건물','집합건물') and B.dvsn=1";
            DataTable dtLs = db.ExeDt(sql);
            lsCnt = dtLs.Rows.Count;
            if (lsCnt == 0) return;

            sql = "select cat3 from ta_list where tid=" + tid;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            cat = dr["cat3"].ToString();
            dr.Close();
            db.Close();

            Match match;

            if (Regex.IsMatch(useStr, @"(기호|번호|\d+\-[가-하])", rxOptM))
            {
                //
            }
            else
            {
                if (Regex.IsMatch(useStr, @"\d+층", rxOptM) && lsCnt > 1)
                {
                    //
                }
                else
                {
                    List<string> ptrnList = new List<string>();
                    ptrnList.Add(@"(.*?)[으로서]+[ ]*(이용|사용|내부)");
                    ptrnList.Add(@"용도는[ ]*(.*?)[으로]+");
                    ptrnList.Add(@"^((다세대주택|아파트)(\([\w\d, ]{5,}\))*)$");
                    ptrnList.Add(@"^[-]*[""]*(\w+(\(\w+\))*)[""]*[임\.]*$");
                    ptrnList.Add(@"본건은[ ]*(.*?)임");
                    ptrnList.Add(@"^현황[ ]*(.*?)임");                    
                    ptrnList.Add(@"(.*?)입니다");
                    ptrnList.Add(@"대장상[ ]*(\w+)이나");
                    ptrnList.Add(@"((아파트|연립주택)[\w\d,\( \)]*?)[임\.]");
                                        
                    foreach (string ptrn in ptrnList)
                    {
                        match = Regex.Match(useStr, ptrn, rxOptM);
                        if (match.Success)
                        {
                            state = match.Groups[1].Value;
                            break;
                        }                        
                    }                    
                    state = Regex.Replace(state, @"본[ ]*건은|공히|집합건축물대장상|전체를|공동주택으로서|^공부상 \w+ 현황|""|^\-|\(후첨.*\)|\(\d+층[ ]*\d+호\)|\d+층[ ]*\d+호|^[가-하]\)|구조$|\(내부.*|^(공부상|현황)|용도로서.*", string.Empty).Trim();
                    if (state != string.Empty && !state.Contains("공실") && useStr.Contains("공실"))
                    {
                        state = $"{state}(현황:공실)";
                    }
                    if (state == "공동주택(아파트)") state = "아파트";
                    else if (state == "공동주택(연립주택)") state = "연립주택";
                    /*
                    if (state == string.Empty) return;
                    
                    db.Open();
                    foreach (DataRow row in dtLs.Rows)
                    {
                        if (row["state"].ToString() != string.Empty) continue;
                        sql = "update ta_bldg set state='" + state + "' where idx=" + row["idx"].ToString();
                        db.ExeQry(sql);
                    }
                    db.Close();
                    */
                }
            }
            //MessageBox.Show(state);
            if (state != string.Empty) return;
            HtmlNodeCollection ncTr = tblLs.SelectNodes("./tbody/tr");
            //MessageBox.Show(ncTr.Count.ToString());
            foreach (HtmlNode ndTr in ncTr)
            {
                HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");

            }
        }

        /// <summary>
        /// OCR 연동-감평서 금액입력
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkOcrTest_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string tid, spt, sn1, sn2, sn, sql;
            string fileNm, url, html0, html, docTxt0, docTxt, htmlMb, htmlLand, htmlLandBldg;
            string apslType = string.Empty;
            string no, adrs, jibun, use, a1, a2, p1, p2;
            int docNo, caseNo = 0, lsCnt = 0, landCnt = 0, bldgCnt = 0;
            int i, cA1, cA2, cP1, cP2; //칼럼 인덱스No-공부면적, 사정면적, 단가, 금액
            decimal apslAmt = 0, landAmtSum = 0, bldgAmtSum = 0, amtSum = 0, unitPrc = 0;
            double mSqm = 0, gSqm = 0;  //명세표 사정면적, DG면적
            bool pnFlag = false;

            tid = lnkTid.Text;
            sql = "select * from ta_list where tid=" + tid + " limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            spt = dr["spt"].ToString();
            sn1 = dr["sn1"].ToString();
            sn2 = dr["sn2"].ToString();
            apslAmt = Convert.ToDecimal(dr["apsl_amt"]);
            if (dr["pn"].ToString() != "0") pnFlag = true;
            dr.Close();
            db.Close();

            //목록내역
            sql = "select * from ta_ls where tid=" + tid;
            DataTable dtLs = db.ExeDt(sql);
            lsCnt = dtLs.Rows.Count;
            if (lsCnt == 0)
            {
                MessageBox.Show("목록내역이 없습니다.");
                return;
            }

            //토지현황
            sql = "select * from ta_land where tid=" + tid;
            DataTable dtLand = db.ExeDt(sql);
            landCnt = dtLand.Rows.Count;

            //건물현황
            sql = "select * from ta_bldg where tid=" + tid;
            DataTable dtBldg = db.ExeDt(sql);
            bldgCnt = dtBldg.Rows.Count;

            List<string> lsDvsn = new List<string>();
            foreach (DataRow row in dtLs.Rows)
            {
                lsDvsn.Add(row["dvsn"].ToString());
            }
            
            if (lsCnt == 1)
            {
                if (lsDvsn[0] == "집합건물") apslType = "집합단일";
                else if (lsDvsn[0] == "토지") apslType = "토지";
                else if (lsDvsn[0] == "건물") apslType = "건물";
            }
            else
            {
                if (lsDvsn.Contains("집합건물") && lsDvsn.Contains("토지") && lsDvsn.Contains("건물")) apslType = "집합토지건물";
                else if (lsDvsn.Contains("집합건물") && lsDvsn.Contains("토지")) apslType = "집합토지";
                else if (lsDvsn.Contains("집합건물") && lsDvsn.Contains("건물")) apslType = "집합건물";
                else if (lsDvsn.Contains("토지") && lsDvsn.Contains("건물")) apslType = "토지건물";
                else if (lsDvsn.Contains("집합건물")) apslType = "집합";
                else if (lsDvsn.Contains("토지")) apslType = "토지";
                else if (lsDvsn.Contains("건물")) apslType = "건물";
            }

            if (apslType == string.Empty)
            {
                MessageBox.Show("목록구분을 판단할 수 없습니다.");
                return;
            }

            string tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
            sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));
            sql = "select * from " + tbl + " where spt=" + spt + " and sn='" + sn + "' and ctgr IN ('AF','EI') order by idx";
            DataTable dt = db.ExeDt(sql);

            if (dt.Select("ctgr='AF'").Count() == 0)
            {
                MessageBox.Show("수집된 감정평가서가 없습니다.");
                return;
            }

            docNo = Convert.ToInt32(cbxApslDocCnt.Text) - 1;
            fileNm = dt.Rows[docNo]["file"].ToString();            
            fileNm = fileNm.Replace("AF", "EI");
            fileNm = fileNm.Replace("pdf", "html");
            DataRow[] rows = dt.Select("file='" + fileNm + "'");

            if (rows.Count() == 0)
            {
                MessageBox.Show("OCR 파일이 없습니다.");
                return;
            }

            HAPDoc doc = new HAPDoc();
            StringBuilder sb = new StringBuilder();

            url = string.Format(myWeb + "FILE/CA/EI/{0}/{1}/{2}", spt, sn1, fileNm);
            html0 = net.GetHtml(url);
            doc.LoadHtml(html0);
            docTxt0 = doc.DocumentNode.InnerText.Trim();
            txtTest0.Text = docTxt0;

            //명세표
            MatchCollection mcTbl = Regex.Matches(html0, @"[（(]*(토지|토지[,및.• ]*건물|부동산|구분건물|아파트|오피스텔|토지건물)[)）]*[ ]*[감정]*평가[ ]*명[ ]*세[ ]*표.*?<table border=""1"">.*?</table>", rxOptS);
            foreach (Match maTbl in mcTbl)
            {
                sb.Append(maTbl.Value);
            }
            if (mcTbl.Count == 0)
            {
                HtmlNodeCollection ncTbl = doc.DocumentNode.SelectNodes("//table[contains(.,'공부') or contains(.,'공 부')]");
                if (ncTbl != null)
                {
                    foreach (HtmlNode ndTbl in ncTbl)
                    {
                        sb.Append(ndTbl.OuterHtml);
                    }
                }
            }            
            html = sb.ToString();
            sb.Clear();

            doc.LoadHtml(html);
            docTxt = doc.DocumentNode.InnerText.Trim();
            html = doc.DocumentNode.InnerHtml.Trim();
            txtTest.Text = html;

            //집합건물-(토지/건물) 평가액 패턴
            List<string> mbPtrn = new List<string>();
            mbPtrn.Add(@"배분내역\s+[토지\:\s]{3,}(\d[\d,]{4,})\s+[건물\:\s]{3,}(\d[\d,]{4,})");
            mbPtrn.Add(@"배분내역\s+[토지가액\:\s]{5,}(\d[\d,]{4,})\s+[건물가액\:\s]{5,}(\d[\d,]{4,})");
            mbPtrn.Add(@"배분내역[\]］>＞\s]*(\d[\d,]{4,})[\s]*(\d[\d,]{4,})");
            mbPtrn.Add(@"배분가격[\s]*(\d[\d,]{4,})[\s]*(\d[\d,]{4,})");
            mbPtrn.Add(@"배분가액[：:\s]+(\d[\d,]{4,})[：:\s]+(\d[\d,]{4,})");
            mbPtrn.Add(@"가격[\s]*배분[)\s]*(\d[\d,]{4,})[\s]*(\d[\d,]{4,})");
            mbPtrn.Add(@"토지[,.]건물[ ]*배분가격[\s]+[토지건물\s]{5,}[\s]+(\d[\d,]{4,})[\s]*(\d[\d,]{4,})");
            mbPtrn.Add(@"[배분가격a-z\s]{5,}[토지건물\s]{5,}[\s]+(\d[\d,]{4,})[\s]*(\d[\d,]{4,})");
            mbPtrn.Add(@"[토지건물배분\s,:]{8,}내역\s+(\d[\d,]{4,})[\s]*(\d[\d,]{4,})");
            mbPtrn.Add(@"배분내역[\]］\s]+토지[:\s]+(\d[\d,]{4,})[\s]*건물[:\s]+(\d[\d,]{4,})");
            mbPtrn.Add(@"[토지건물배분\s]{6,}토지[\s:]+건물[\s:]+(\d[\d,]{4,})[\s]+(\d[\d,]{4,})");
            mbPtrn.Add(@"배분가액[ ]*토[ ]*지[\s:]+건[ ]*물[\s:]+(\d[\d,]{4,})[\s]+(\d[\d,]{4,})");
            mbPtrn.Add(@"토지/건물[\s]*비율[\s]*(\d[\d,]{4,})[\s]*(\d[\d,]{4,})");

            mbPtrn.Add(@"배분내역[\]］\s]+건물[:\s]+(\d[\d,]{4,})[\s]*토지[:\s]+(\d[\d,]{4,})");     //건물, 토지 -> 토지, 건물 역순
            mbPtrn.Add(@"[토지건물의배분내역은\s.,•]{12,}토지[\s:]+(\d[\d,]{4,})원[,\s]+건물[\s:]+(\d[\d,]{4,})원");    //명세표외 부분에서-토지 • 건물의 배분내역은 토지 : 29,160,000원, 건물 : 24,840,000원임

            if (apslType == "집합단일" && landCnt == 1 && bldgCnt == 1)
            {
                //명세표에서
                foreach (string ptrn in mbPtrn)
                {
                    MatchCollection mc = Regex.Matches(docTxt, ptrn, rxOptM);
                    if (mc.Count == 0) continue;
                    foreach (Match match in mc)
                    {
                        if (Regex.IsMatch(ptrn, @"건물.*토지.*", rxOptM))
                        {
                            landAmtSum = (string.IsNullOrEmpty(match.Groups[2].Value)) ? 0 : Convert.ToDecimal(match.Groups[2].Value);
                            bldgAmtSum = (string.IsNullOrEmpty(match.Groups[1].Value)) ? 0 : Convert.ToDecimal(match.Groups[1].Value);
                        }
                        else
                        {
                            landAmtSum = (string.IsNullOrEmpty(match.Groups[1].Value)) ? 0 : Convert.ToDecimal(match.Groups[1].Value);
                            bldgAmtSum = (string.IsNullOrEmpty(match.Groups[2].Value)) ? 0 : Convert.ToDecimal(match.Groups[2].Value);
                        }
                        amtSum = landAmtSum + bldgAmtSum;
                        caseNo = 1;
                        if (amtSum == apslAmt) goto EXIT;
                    }
                }

                //문서전체에서-Multi Line
                if (amtSum == 0)
                {
                    foreach (string ptrn in mbPtrn)
                    {
                        MatchCollection mc = Regex.Matches(docTxt0, ptrn, rxOptM);
                        if (mc.Count == 0) continue;
                        foreach (Match match in mc)
                        {
                            landAmtSum = (string.IsNullOrEmpty(match.Groups[1].Value)) ? 0 : Convert.ToDecimal(match.Groups[1].Value);
                            bldgAmtSum = (string.IsNullOrEmpty(match.Groups[2].Value)) ? 0 : Convert.ToDecimal(match.Groups[2].Value);
                            amtSum = landAmtSum + bldgAmtSum;
                            caseNo = 2;
                            if (amtSum == apslAmt) goto EXIT;
                        }
                    }
                }

                //문서전체에서-Single Line
                if (amtSum == 0)
                {
                    Match match = Regex.Match(docTxt0, @"배분[가격내역]{2}.*?토지[:\s]+(\d[\d,]{4,}).*?건물[:\s]+(\d[\d,]{4,})", rxOptS);
                    if (match.Success)
                    {
                        landAmtSum = (string.IsNullOrEmpty(match.Groups[1].Value)) ? 0 : Convert.ToDecimal(match.Groups[1].Value);
                        bldgAmtSum = (string.IsNullOrEmpty(match.Groups[2].Value)) ? 0 : Convert.ToDecimal(match.Groups[2].Value);
                        amtSum = landAmtSum + bldgAmtSum;
                        caseNo = 3;
                    }
                }

            EXIT:
                if (amtSum == apslAmt)
                {
                    //MessageBox.Show($"Case-{caseNo}\n토지-{landAmtSum,15:N0}\n건물-{bldgAmtSum,15:N0}\n합계-{amtSum,15:N0}");                    
                    dgL["dgL_Amt", 0].Value = landAmtSum;
                    dgB["dgB_Amt", 0].Value = bldgAmtSum;
                    Sum_SqmAmt("dgL");
                    Sum_SqmAmt("dgB");
                }
                else
                {
                    landAmtSum = 0;
                    bldgAmtSum = 0;
                    amtSum = 0;
                    MessageBox.Show("실패");
                }
            }
            else if (apslType == "토지" || apslType == "건물" || apslType == "토지건물")
            {
                DataTable dtM = new DataTable();
                dtM.Columns.Add("no");      //일련번호
                dtM.Columns.Add("adrs");    //소재지
                dtM.Columns.Add("jibun");   //지번
                dtM.Columns.Add("use");   //지목 및 용도
                dtM.Columns.Add("a1");      //면적(공부)
                dtM.Columns.Add("a2");      //면적(사정)
                dtM.Columns.Add("p1");      //감정평가액(단가)
                dtM.Columns.Add("p2");      //감정평가액(금액)

                HtmlNodeCollection nct = doc.DocumentNode.SelectNodes(".//table");
                if (nct == null)
                {
                    MessageBox.Show("[토지 감정평가 명세표]를 찾지 못했습니다.");
                    return;
                }
                foreach (HtmlNode ndt in nct)
                {
                    HtmlNodeCollection ncTr = ndt.SelectNodes("./tr");
                    if (ncTr == null) continue;
                    if (ncTr.Count < 3) continue;

                    HtmlNodeCollection nodes = ncTr[1].SelectNodes("./td");
                    if (nodes.Count == 4)
                    {
                        cA1 = 5;
                        cA2 = 6;
                        cP1 = 7;
                        cP2 = 8;
                    }
                    else
                    {
                        string colsStr = ncTr[1].InnerText.Trim();
                        colsStr = Regex.Replace(colsStr, @"번[ ]*호|용[ ]*도|[및 ]*구[ ]*조", string.Empty, rxOptM).Trim();
                        string[] colsArr = colsStr.Split('\n');
                        try
                        {
                            int idx = Array.FindIndex(colsArr, r => r.Contains("부"));
                            cA1 = 4 + idx + 1;
                            idx = Array.FindIndex(colsArr, r => r.Contains("정"));
                            cA2 = 4 + idx + 1;
                            idx = Array.FindIndex(colsArr, r => r.Contains("가"));
                            cP1 = 4 + idx + 1;
                            idx = Array.FindIndex(colsArr, r => r.Contains("액") || r.Contains("애"));
                            cP2 = 4 + idx + 1;
                        }
                        catch
                        {
                            continue;
                        }
                    }                    
                    foreach (HtmlNode ndTr in ncTr)
                    {   
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        if (ncTd.Count < 8) continue;
                        no = ncTd[0].InnerText.Replace(".", string.Empty).Trim();
                        if (no == "일련 번호" || no == "일련" || no == "번호") continue;                        
                        
                        try
                        {
                            adrs = ncTd[1].InnerText.Trim();
                            jibun = ncTd[2].InnerText.Trim();
                            use = ncTd[3].InnerText.Trim();
                            a1 = ncTd[cA1].InnerText.Trim();
                            a2 = ncTd[cA2].InnerText.Trim();
                            p1 = ncTd[cP1].InnerText.Trim().Replace(".", string.Empty);
                            p2 = ncTd[cP2].InnerText.Trim().Replace(".", string.Empty);

                            DataRow row = dtM.NewRow();
                            row["no"] = no;
                            row["adrs"] = adrs;
                            row["jibun"] = jibun;
                            row["use"] = use;
                            row["a1"] = Regex.Replace(a1, @"[^\d,.]", string.Empty, rxOptM).Trim();
                            row["a2"] = Regex.Replace(a2, @"[^\d,.]", string.Empty, rxOptM).Trim();
                            row["p1"] = Regex.Replace(p1, @"[^\d,.]", string.Empty, rxOptM).Trim();
                            row["p2"] = Regex.Replace(p2, @"[^\d,.]", string.Empty, rxOptM).Trim();
                            dtM.Rows.Add(row);
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    //# 다른 방법 시도
                    i = 0;
                    string preNo = "0";
                    bool etcFlag = false;
                    foreach (HtmlNode ndTr in ncTr)
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        if (ncTd.Count < 8) continue;
                        no = ncTd[0].InnerText.Replace(".", string.Empty).Trim();
                        if (no == "일련 번호" || no == "일련" || no == "번호") continue;
                        no = Regex.Match(no, @"\d+", rxOptM).Value;
                        if (no == string.Empty)
                        {
                            no = (i == 0) ? "1" : preNo;
                        }
                        string[] txtArr = ndTr.InnerText.Trim().Split('\n');
                        int txtArrCnt = txtArr.Count();
                        if (ndTr.InnerText.Contains("제시외")) etcFlag = true;

                        //토지 현황
                        foreach (DataGridViewRow r in dgL.Rows)
                        {
                            if (string.IsNullOrEmpty(r.Cells["dgL_LsNo"].Value?.ToString())) break;
                            try
                            {
                                p1 = r.Cells["dgL_UnitPrc"].Value?.ToString() ?? string.Empty;
                                //if (p1 != string.Empty && p1 != "0") continue;
                                gSqm = Convert.ToDouble(r.Cells["dgL_Sqm"].Value?.ToString() ?? "0");                                
                                int fidx = Array.FindLastIndex(txtArr, x => Math.Abs(((Double.TryParse(x.Trim(),out mSqm)) ? mSqm : 0) - gSqm) < 1);
                                if (fidx > -1 && r.Cells["dgL_LsNo"].Value?.ToString() == no)
                                {
                                    p1 = txtArr[fidx + 1].Replace(".", string.Empty);   //,를 .로 해석하는 경우
                                    p1 = Regex.Replace(p1, @"[^\d,]", string.Empty, rxOptM).Trim();
                                    unitPrc = Convert.ToDecimal(p1 ?? "0");
                                    if (unitPrc % 10 != 0 || unitPrc < 100) continue;   //단가가 1원 단위 또는 100원 보다 작을 때는 오류로 판단
                                    r.Cells["dgL_UnitPrc"].Value = p1;
                                    r.Cells["dgL_Amt"].Value = Convert.ToDecimal(r.Cells["dgL_Sqm"].Value?.ToString() ?? "0") * unitPrc;
                                    break;
                                }
                            }
                            catch(Exception ex)
                            {
                                //MessageBox.Show(ex.Message);
                            }
                        }

                        //건물 및 제시외 현황
                        if (apslType == "건물" || apslType == "토지건물")
                        {
                            //건물 현황
                            foreach (DataGridViewRow r in dgB.Rows)
                            {
                                if (string.IsNullOrEmpty(r.Cells["dgB_LsNo"].Value?.ToString())) break;
                                try
                                {
                                    p1 = r.Cells["dgB_UnitPrc"].Value?.ToString() ?? string.Empty;
                                    //if (p1 != string.Empty && p1 != "0") continue;
                                    gSqm = Convert.ToDouble(r.Cells["dgB_Sqm"].Value?.ToString() ?? "0");
                                    int fidx = Array.FindLastIndex(txtArr, x => Math.Abs(((Double.TryParse(x.Trim(), out mSqm)) ? mSqm : 0) - gSqm) < 1);
                                    if (fidx > -1 && r.Cells["dgB_LsNo"].Value?.ToString() == no)
                                    {
                                        p1 = txtArr[fidx + 1].Replace(".", string.Empty);   //,를 .로 해석하는 경우
                                        p1 = Regex.Replace(p1, @"[^\d,]", string.Empty, rxOptM).Trim();
                                        unitPrc = Convert.ToDecimal(p1 ?? "0");
                                        if (unitPrc % 10 != 0 || unitPrc < 100) continue;   //단가가 1원 단위 또는 100원 보다 작을 때는 오류로 판단
                                        r.Cells["dgB_UnitPrc"].Value = p1;
                                        r.Cells["dgB_Amt"].Value = Convert.ToDecimal(r.Cells["dgB_Sqm"].Value?.ToString() ?? "0") * unitPrc;
                                        break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    //MessageBox.Show(ex.Message);
                                }
                            }

                            //제시외 현황
                            if (etcFlag)
                            {
                                foreach (DataGridViewRow r in dgE.Rows)
                                {
                                    if (string.IsNullOrEmpty(r.Cells["dgE_LsNo"].Value?.ToString())) break;
                                    try
                                    {
                                        p1 = r.Cells["dgE_UnitPrc"].Value?.ToString() ?? string.Empty;
                                        //if (p1 != string.Empty && p1 != "0") continue;
                                        gSqm = Convert.ToDouble(r.Cells["dgE_Sqm"].Value?.ToString() ?? "0");
                                        int fidx = Array.FindLastIndex(txtArr, x => Math.Abs(((Double.TryParse(x.Trim(), out mSqm)) ? mSqm : 0) - gSqm) < 1);
                                        //if (fidx > -1 && r.Cells["dgE_LsNo"].Value?.ToString() == no)
                                        if (fidx > -1)
                                        {
                                            p1 = txtArr[fidx + 1].Replace(".", string.Empty);   //,를 .로 해석하는 경우
                                            p1 = Regex.Replace(p1, @"[^\d,]", string.Empty, rxOptM).Trim();
                                            unitPrc = Convert.ToDecimal(p1 ?? "0");
                                            if (unitPrc % 10 != 0 || unitPrc < 100) continue;   //단가가 1원 단위 또는 100원 보다 작을 때는 오류로 판단
                                            r.Cells["dgE_UnitPrc"].Value = p1;
                                            r.Cells["dgE_Amt"].Value = Convert.ToDecimal(r.Cells["dgE_Sqm"].Value?.ToString() ?? "0") * unitPrc;
                                            break;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        //MessageBox.Show(ex.Message);
                                    }
                                }
                            }                            
                        }
                        preNo = no;
                    }
                    //#
                }
                Sum_SqmAmt("dgL");

                /*
                //명세표가 단일목록에 일련번호가 없을 경우 목록번호 부여
                if (lsCnt == 1 && dtM.Rows.Count == 1)
                {
                    if (dtM.Rows[0]["no"].ToString() == string.Empty)
                    {
                        dtM.Rows[0]["no"] = dtLs.Rows[0]["no"];
                    }
                }
                
                string prvNo = "0";
                foreach (DataRow row in dtM.Rows)
                {
                    try
                    {
                        no = row["no"].ToString();
                        if (no != string.Empty) prvNo = no;
                        if (no == string.Empty) no = prvNo;
                        mSqm = Convert.ToDouble(row["a2"]?.ToString() ?? "0");
                        p1 = row["p1"].ToString();
                        foreach (DataGridViewRow r in dgL.Rows)
                        {
                            if (string.IsNullOrEmpty(r.Cells["dgL_LsNo"].Value?.ToString())) break;
                            gSqm = Convert.ToDouble(r.Cells["dgL_Sqm"].Value?.ToString() ?? "0");
                            if (mSqm != gSqm)
                            {
                                if (Math.Abs(mSqm - gSqm) < 1)
                                {
                                    gSqm = Math.Round(gSqm, 2);
                                }
                            }
                            //if (r.Cells["dgL_LsNo"].Value?.ToString() == no && mSqm == gSqm)
                            if (r.Cells["dgL_LsNo"].Value?.ToString() == no && (Math.Abs(mSqm - gSqm) < 0.01))
                            {
                                r.Cells["dgL_UnitPrc"].Value = p1;
                                r.Cells["dgL_Amt"].Value = Convert.ToDecimal(r.Cells["dgL_Sqm"].Value?.ToString() ?? "0") * Convert.ToDecimal(p1 ?? "0");
                                break;
                            }
                        }
                    }
                    catch 
                    {
                        continue;
                    }
                }
                Sum_SqmAmt("dgL");
                */
            }
        }

        /// <summary>
        /// 목록 및 현황 새로 추출
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnNewExtract_Click(object sender, EventArgs e)
        {
            decimal.TryParse(lnkTid.Text, out decimal tid);
            
            if (tid > 0)
            {
                sfNewExtract sfNewExt = new sfNewExtract() { Owner = this };
                sfNewExt.StartPosition = FormStartPosition.CenterScreen;
                sfNewExt.FormBorderStyle = FormBorderStyle.FixedToolWindow;
                sfNewExt.ShowDialog();
                sfNewExt.Dispose();
            }
            else
            {
                MessageBox.Show("선택된 물건이 없습니다.");
                return;
            }
        }

        /// <summary>
        /// 등기 Pin 누락사건 Pin 추출
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRgstPin_Click(object sender, EventArgs e)
        {
            string url, html, jiwonNm, saNo, lsNo, pin;
            int sucCnt = 0;

            decimal.TryParse(lnkTid.Text, out decimal tid);
            if (tid < 1) return;

            jiwonNm = auctCd.LawNmEnc(csCd: $"{cbxCrtSpt.SelectedValue}");
            saNo = $"{cbxSn1.Text}0130{txtSn2.Text.PadLeft(6, '0')}";
            url = "http://www.courtauction.go.kr/RetrieveRealEstDetailInqSaList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
            html = net.GetHtml(url);
            if (html.Contains("공고중인 물건이 아닙니다") || html.Contains("존재하지 않는 페이지입니다"))
            {
                MessageBox.Show("법원에서 [사건내역] 페이지를 볼 수 없습니다.");
            }

            HAPDoc doc = new HAPDoc();
            doc.LoadHtml(html);

            HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='목록내역 표']/tbody/tr");
            if (ncTr == null)
            {
                MessageBox.Show("[목록내역]이 없습니다.");
                return;
            }

            foreach (DataGridViewRow row in dgI.Rows)
            {
                foreach (HtmlNode ndTr in ncTr)
                {
                    HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                    lsNo = Regex.Match(ncTd[0].InnerText, @"\d+").Value;                    
                    pin = Regex.Match(ncTd[1].InnerHtml, @"regiBu\('(\d+)'\)", RegexOptions.IgnoreCase).Groups[1].Value;

                    if ($"{row.Cells["dgI_LsNo"].Value}" == lsNo)
                    {
                        sucCnt++;
                        row.Cells["dgI_Pin"].Value = pin;
                        break;
                    }
                }
            }

            if (sucCnt > 0)
            {
                MessageBox.Show("등기 Pin 이 추출 되었습니다.");
            }
            else
            {
                MessageBox.Show("등기 Pin 추출 실패");
            }
        }

        /// <summary>
        /// 등기변동 재발급시 새로 추출(TK)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LnkRgstAnaly_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string sql, fileCdtn, sn1, tbl, locFile, rmtFile, rgstDnPath;
            int landCnt = 0, bldgCnt = 0;

            rgstDnPath = @"C:\등기파일\";
            if (!Directory.Exists(rgstDnPath))
            {
                Directory.CreateDirectory(rgstDnPath);
            }

            if (MessageBox.Show("최근(7일내) 등록된 파일로 새로 추출하시겠습니까?\r\n(주의-기존 내용이 삭제됩니다.)", "등기 추출", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            decimal.TryParse(lnkTid.Text, out decimal tid);
            if (tid < 1) return;

            RgstAnalyNew rgstAnaly = new RgstAnalyNew();

            sn1 = cbxSn1.Text;
            tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";

            if (rdoRgstAnalyAll.Checked) fileCdtn = "ctgr in ('DA','DB')";  //토지+건물
            else if (rdoRgstAnalyLand.Checked) fileCdtn = "ctgr='DA'";      //토지
            else fileCdtn = "ctgr='DB'";                                    //건물
                        
            sql = $"select * from {tbl} where tid={tid} and {fileCdtn} and wdt >= date_sub(curdate(), interval 7 day)";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                rmtFile = $"{dr["ctgr"]}/{dr["spt"]}/{sn1}/{dr["file"]}";
                if (rmtFile.Contains("-01.pdf") == false && rmtFile.Contains("-0001.pdf") == false) continue;

                locFile = $@"{rgstDnPath}\{dr["file"]}";
                if (ftp1.Download(rmtFile, locFile, true))
                {
                    string analyRslt = rgstAnaly.Proc(locFile, true);
                    if (analyRslt == "success")
                    { 
                        if(dr["ctgr"].ToString()=="DA") landCnt++;
                        else bldgCnt++;
                    }
                }
            }
            dr.Close();
            db.Close();

            if (landCnt == 0 && bldgCnt == 0)
            {
                MessageBox.Show("추출된 파일이 없습니다.");
                return;
            }

            MessageBox.Show($"토지등기-{landCnt}, 건물(집합)등기-{bldgCnt} 추출 되었습니다.");
        }

        /// <summary>
        /// 등기변동 전/후 데이터 비교
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRgstCmp_Click(object sender, EventArgs e)
        {
            int n = 0;
            string sql, tid;
            MySqlDataReader dr;
            DataGridView dg;
            DataTable dtA, dtB;

            tbcL.SelectedTab = tabRgstCmp;
            dgRCA.Rows.Clear();
            dgRCB.Rows.Clear();
                        
            tid = lnkTid.Text;

            Button btn = sender as Button;            
            if (btn == btnRgstLandCmp)
            {
                dg = dgRL;
                //lnkPdflDoc_LinkClicked(lnkTK_LandRgst, null);

                //토지 등기-전
                n = 0;
                sql = "select * from ta_rgst where tid=" + tid + " and rg_dvsn=1 order by rc_dt, rc_no";
                dtA = db.ExeDt(sql);
                db.Open();
                dr = db.ExeRdr(sql);
                while (dr.Read())
                {
                    n = dgRCA.Rows.Add();
                    dgRCA["dgRCA_Idx", n].Value = dr["idx"];
                    ((DataGridViewComboBoxCell)dgRCA["dgRCA_Sect", n]).Value = dr["sect"].ToString();
                    dgRCA["dgRCA_Rank", n].Value = (dr["rank_s"].ToString() != "0") ? string.Format("{0}-{1}", dr["rank"], dr["rank_s"]) : dr["rank"];
                    dgRCA["dgRCA_RcDt", n].Value = (dr["rc_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["rc_dt"]);
                    dgRCA["dgRCA_EKey", n].Value = dr["ekey"];
                    dgRCA["dgRCA_RgNm", n].Value = dr["rg_nm"];
                    dgRCA["dgRCA_RcNo", n].Value = dr["rc_no"];
                    dgRCA["dgRCA_CAmt", n].Value = string.Format("{0:N0}", dr["c_amt"]);
                    ((DataGridViewComboBoxCell)dgRCA["dgRCA_Take", n]).Value = dr["take"];
                    dgRCA["dgRCA_Prsn", n].Value = dr["prsn"];
                    dgRCA["dgRCA_RgNo", n].Value = dr["rg_no"];
                    dgRCA["dgRCA_MvDt", n].Value = (dr["mv_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["mv_dt"]);
                    dgRCA["dgRCA_FxDt", n].Value = (dr["fx_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["fx_dt"]);
                    dgRCA["dgRCA_BgnDt", n].Value = (dr["bgn_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["bgn_dt"]);
                    dgRCA["dgRCA_EndDt", n].Value = (dr["end_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["end_dt"]);
                    dgRCA["dgRCA_REno", n].Value = dr["r_eno"];
                    dgRCA["dgRCA_Aply", n].Value = dr["aply"];
                    dgRCA["dgRCA_BAmt", n].Value = string.Format("{0:N0}", dr["b_amt"]);
                    dgRCA["dgRCA_Note", n].Value = dr["note"];
                    dgRCA["dgRCA_Adrs", n].Value = dr["adrs"];
                    dgRCA["dgRCA_Brch", n].Value = dr["brch"];
                    dgRCA["dgRCA_Hide", n].Value = dr["hide"];
                    ((DataGridViewComboBoxCell)dgRCA["dgRCA_RgCd", n]).Value = dr["rg_cd"];
                    /*
                    if (dr["take"].ToString() == "1") dgRCA.Rows[n].DefaultCellStyle.BackColor = Color.MistyRose;    //인수(수동체크)
                    if (dr["hide"].ToString() == "1") dgRCA.Rows[n].DefaultCellStyle.BackColor = Color.LightGray;    //등기추출에서 숨김처리된 등기항목
                    if ((dr["rg_cd"].ToString() == "4" || dr["rg_cd"].ToString() == "5") && (dr["note"].ToString().Contains(txtSn2.Text) || dr["r_eno"].ToString().Contains(txtSn2.Text)))     //임의경매 또는 강제경매 && 해당 사건번호 포함
                    {
                        dgRCA.Rows[n].DefaultCellStyle.BackColor = Color.PeachPuff;
                    }
                    if (dr["ekey"].ToString() == "1") dgRCA.Rows[n].DefaultCellStyle.BackColor = Color.LightBlue;    //말소기준권리
                    */
                }
                dr.Close();
                db.Close();

                //토지 등기-후
                n = 0;
                sql = "select * from db_tank.tx_rgst_cmp where tid=" + tid + " and rg_dvsn=1 order by rc_dt, rc_no";
                dtB = db.ExeDt(sql);
                db.Open();
                dr = db.ExeRdr(sql);
                while (dr.Read())
                {
                    n = dgRCB.Rows.Add();
                    dgRCB["dgRCB_Idx", n].Value = dr["idx"];
                    ((DataGridViewComboBoxCell)dgRCB["dgRCB_Sect", n]).Value = dr["sect"].ToString();
                    dgRCB["dgRCB_Rank", n].Value = (dr["rank_s"].ToString() != "0") ? string.Format("{0}-{1}", dr["rank"], dr["rank_s"]) : dr["rank"];
                    dgRCB["dgRCB_RcDt", n].Value = (dr["rc_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["rc_dt"]);
                    dgRCB["dgRCB_EKey", n].Value = dr["ekey"];
                    dgRCB["dgRCB_RgNm", n].Value = dr["rg_nm"];
                    dgRCB["dgRCB_RcNo", n].Value = dr["rc_no"];
                    dgRCB["dgRCB_CAmt", n].Value = string.Format("{0:N0}", dr["c_amt"]);
                    ((DataGridViewComboBoxCell)dgRCB["dgRCB_Take", n]).Value = dr["take"];
                    dgRCB["dgRCB_Prsn", n].Value = dr["prsn"];
                    dgRCB["dgRCB_RgNo", n].Value = dr["rg_no"];
                    dgRCB["dgRCB_MvDt", n].Value = (dr["mv_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["mv_dt"]);
                    dgRCB["dgRCB_FxDt", n].Value = (dr["fx_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["fx_dt"]);
                    dgRCB["dgRCB_BgnDt", n].Value = (dr["bgn_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["bgn_dt"]);
                    dgRCB["dgRCB_EndDt", n].Value = (dr["end_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["end_dt"]);
                    dgRCB["dgRCB_REno", n].Value = dr["r_eno"];
                    dgRCB["dgRCB_Aply", n].Value = dr["aply"];
                    dgRCB["dgRCB_BAmt", n].Value = string.Format("{0:N0}", dr["b_amt"]);
                    dgRCB["dgRCB_Note", n].Value = dr["note"];
                    dgRCB["dgRCB_Adrs", n].Value = dr["adrs"];
                    dgRCB["dgRCB_Brch", n].Value = dr["brch"];
                    dgRCB["dgRCB_Hide", n].Value = dr["hide"];
                    ((DataGridViewComboBoxCell)dgRCB["dgRCB_RgCd", n]).Value = dr["rg_cd"];
                    /*
                    if (dr["take"].ToString() == "1") dgRCB.Rows[n].DefaultCellStyle.BackColor = Color.MistyRose;    //인수(수동체크)
                    if (dr["hide"].ToString() == "1") dgRCB.Rows[n].DefaultCellStyle.BackColor = Color.LightGray;    //등기추출에서 숨김처리된 등기항목
                    if ((dr["rg_cd"].ToString() == "4" || dr["rg_cd"].ToString() == "5") && (dr["note"].ToString().Contains(txtSn2.Text) || dr["r_eno"].ToString().Contains(txtSn2.Text)))     //임의경매 또는 강제경매 && 해당 사건번호 포함
                    {
                        dgRCB.Rows[n].DefaultCellStyle.BackColor = Color.PeachPuff;
                    }
                    if (dr["ekey"].ToString() == "1") dgRCB.Rows[n].DefaultCellStyle.BackColor = Color.LightBlue;    //말소기준권리
                    */
                }
                dr.Close();
                db.Close();
            }
            else
            {
                dg = dgRB;
                //lnkPdflDoc_LinkClicked(lnkTK_BldgRgst, null);

                //건물 등기-전
                n = 0;
                sql = "select * from ta_rgst where tid=" + tid + " and rg_dvsn in (2,3) order by rc_dt, rc_no";
                dtA = db.ExeDt(sql);
                db.Open();
                dr = db.ExeRdr(sql);
                while (dr.Read())
                {
                    n = dgRCA.Rows.Add();
                    dgRCA["dgRCA_Idx", n].Value = dr["idx"];
                    ((DataGridViewComboBoxCell)dgRCA["dgRCA_Sect", n]).Value = dr["sect"].ToString();
                    dgRCA["dgRCA_Rank", n].Value = (dr["rank_s"].ToString() != "0") ? string.Format("{0}-{1}", dr["rank"], dr["rank_s"]) : dr["rank"];
                    dgRCA["dgRCA_RcDt", n].Value = string.Format("{0:yyyy-MM-dd}", dr["rc_dt"]);
                    dgRCA["dgRCA_EKey", n].Value = dr["ekey"];
                    dgRCA["dgRCA_RgNm", n].Value = dr["rg_nm"];
                    dgRCA["dgRCA_RcNo", n].Value = dr["rc_no"];
                    dgRCA["dgRCA_CAmt", n].Value = string.Format("{0:N0}", dr["c_amt"]);
                    ((DataGridViewComboBoxCell)dgRCA["dgRCA_Take", n]).Value = dr["take"];
                    dgRCA["dgRCA_Prsn", n].Value = dr["prsn"];
                    dgRCA["dgRCA_RgNo", n].Value = dr["rg_no"];
                    dgRCA["dgRCA_MvDt", n].Value = (dr["mv_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["mv_dt"]);
                    dgRCA["dgRCA_FxDt", n].Value = (dr["fx_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["fx_dt"]);
                    dgRCA["dgRCA_BgnDt", n].Value = (dr["bgn_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["bgn_dt"]);
                    dgRCA["dgRCA_EndDt", n].Value = (dr["end_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["end_dt"]);
                    dgRCA["dgRCA_REno", n].Value = dr["r_eno"];
                    dgRCA["dgRCA_Aply", n].Value = dr["aply"];
                    dgRCA["dgRCA_BAmt", n].Value = string.Format("{0:N0}", dr["b_amt"]);
                    dgRCA["dgRCA_Note", n].Value = dr["note"];
                    dgRCA["dgRCA_Adrs", n].Value = dr["adrs"];
                    dgRCA["dgRCA_Brch", n].Value = dr["brch"];
                    dgRCA["dgRCA_Hide", n].Value = dr["hide"];
                    ((DataGridViewComboBoxCell)dgRCA["dgRCA_RgCd", n]).Value = dr["rg_cd"];
                    /*
                    if (dr["take"].ToString() == "1") dgRCA.Rows[n].DefaultCellStyle.BackColor = Color.MistyRose;    //인수(수동체크)
                    if (dr["hide"].ToString() == "1") dgRCA.Rows[n].DefaultCellStyle.BackColor = Color.LightGray;    //등기추출에서 숨김처리된 등기항목
                    if ((dr["rg_cd"].ToString() == "4" || dr["rg_cd"].ToString() == "5") && (dr["note"].ToString().Contains(txtSn2.Text) || dr["r_eno"].ToString().Contains(txtSn2.Text)))     //임의경매 또는 강제경매
                    {
                        dgRCA.Rows[n].DefaultCellStyle.BackColor = Color.PeachPuff;
                    }
                    if (dr["ekey"].ToString() == "1") dgRCA.Rows[n].DefaultCellStyle.BackColor = Color.LightBlue;    //말소기준권리
                    if (n == 1)
                    {
                        if (dr["rg_dvsn"].ToString() == "2") rdoRgstDvsn2.Checked = true;
                        else if (dr["rg_dvsn"].ToString() == "3") rdoRgstDvsn3.Checked = true;
                    }
                    */
                }
                dr.Close();
                db.Close();

                //건물 등기-후
                n = 0;
                sql = "select * from db_tank.tx_rgst_cmp where tid=" + tid + " and rg_dvsn in (2,3) order by rc_dt, rc_no";
                dtB = db.ExeDt(sql);
                db.Open();
                dr = db.ExeRdr(sql);
                while (dr.Read())
                {
                    n = dgRCB.Rows.Add();
                    dgRCB["dgRCB_Idx", n].Value = dr["idx"];
                    ((DataGridViewComboBoxCell)dgRCB["dgRCB_Sect", n]).Value = dr["sect"].ToString();
                    dgRCB["dgRCB_Rank", n].Value = (dr["rank_s"].ToString() != "0") ? string.Format("{0}-{1}", dr["rank"], dr["rank_s"]) : dr["rank"];
                    dgRCB["dgRCB_RcDt", n].Value = string.Format("{0:yyyy-MM-dd}", dr["rc_dt"]);
                    dgRCB["dgRCB_EKey", n].Value = dr["ekey"];
                    dgRCB["dgRCB_RgNm", n].Value = dr["rg_nm"];
                    dgRCB["dgRCB_RcNo", n].Value = dr["rc_no"];
                    dgRCB["dgRCB_CAmt", n].Value = string.Format("{0:N0}", dr["c_amt"]);
                    ((DataGridViewComboBoxCell)dgRCB["dgRCB_Take", n]).Value = dr["take"];
                    dgRCB["dgRCB_Prsn", n].Value = dr["prsn"];
                    dgRCB["dgRCB_RgNo", n].Value = dr["rg_no"];
                    dgRCB["dgRCB_MvDt", n].Value = (dr["mv_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["mv_dt"]);
                    dgRCB["dgRCB_FxDt", n].Value = (dr["fx_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["fx_dt"]);
                    dgRCB["dgRCB_BgnDt", n].Value = (dr["bgn_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["bgn_dt"]);
                    dgRCB["dgRCB_EndDt", n].Value = (dr["end_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["end_dt"]);
                    dgRCB["dgRCB_REno", n].Value = dr["r_eno"];
                    dgRCB["dgRCB_Aply", n].Value = dr["aply"];
                    dgRCB["dgRCB_BAmt", n].Value = string.Format("{0:N0}", dr["b_amt"]);
                    dgRCB["dgRCB_Note", n].Value = dr["note"];
                    dgRCB["dgRCB_Adrs", n].Value = dr["adrs"];
                    dgRCB["dgRCB_Brch", n].Value = dr["brch"];
                    dgRCB["dgRCB_Hide", n].Value = dr["hide"];
                    ((DataGridViewComboBoxCell)dgRCB["dgRCB_RgCd", n]).Value = dr["rg_cd"];
                    /*
                    if (dr["take"].ToString() == "1") dgRCB.Rows[n].DefaultCellStyle.BackColor = Color.MistyRose;    //인수(수동체크)
                    if (dr["hide"].ToString() == "1") dgRCB.Rows[n].DefaultCellStyle.BackColor = Color.LightGray;    //등기추출에서 숨김처리된 등기항목
                    if ((dr["rg_cd"].ToString() == "4" || dr["rg_cd"].ToString() == "5") && (dr["note"].ToString().Contains(txtSn2.Text) || dr["r_eno"].ToString().Contains(txtSn2.Text)))     //임의경매 또는 강제경매
                    {
                        dgRCB.Rows[n].DefaultCellStyle.BackColor = Color.PeachPuff;
                    }
                    if (dr["ekey"].ToString() == "1") dgRCB.Rows[n].DefaultCellStyle.BackColor = Color.LightBlue;    //말소기준권리
                    if (n == 1)
                    {
                        if (dr["rg_dvsn"].ToString() == "2") rdoRgstDvsn2.Checked = true;
                        else if (dr["rg_dvsn"].ToString() == "3") rdoRgstDvsn3.Checked = true;
                    }
                    */
                }
                dr.Close();
                db.Close();
            }            

            dgRCA.ClearSelection();
            dgRCB.ClearSelection();

            //전 -> 후 (삭제 대상)
            foreach (DataGridViewRow row in dgRCA.Rows)
            {
                DataRow[] rows = dtB.Select($"rc_no='{row.Cells["dgRCA_RcNo"].Value}'");
                if (rows.Count() == 0)
                {
                    row.Cells["dgRCA_Chk"].Value = 1;
                    row.DefaultCellStyle.BackColor = Color.LightGray;
                }
            }

            //후 -> 전 (추가 대상)
            foreach (DataGridViewRow row in dgRCB.Rows)
            {
                DataRow[] rows = dtA.Select($"rc_no='{row.Cells["dgRCB_RcNo"].Value}'");
                if (rows.Count() == 0)
                {
                    row.Cells["dgRCB_Chk"].Value = 1;
                    row.DefaultCellStyle.BackColor = Color.LightGreen;
                }
            }

            //전 -> 후 (변경 대상)
            foreach (DataGridViewRow rowA in dgRCA.Rows)
            {
                foreach (DataGridViewRow rowB in dgRCB.Rows)
                {
                    if ($"{rowA.Cells["dgRCA_RcNo"].Value}" == $"{rowB.Cells["dgRCB_RcNo"].Value}")
                    {
                        if ($"{rowA.Cells["dgRCA_CAmt"].Value}" != $"{rowB.Cells["dgRCB_CAmt"].Value}" || $"{rowA.Cells["dgRCA_Prsn"].Value}" != $"{rowB.Cells["dgRCB_Prsn"].Value}")
                        {
                            rowA.DefaultCellStyle.BackColor = Color.PowderBlue;
                            rowB.DefaultCellStyle.BackColor = Color.PowderBlue;
                            break;
                        }
                    }
                }
            }

            txtRgstDvsn.Text = (dg == dgRL) ? "토지등기" : "건물등기";
        }

        /// <summary>
        /// 등기변동 체크 삭제(-)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRgstCmpDel_Click(object sender, EventArgs e)
        {
            string sql, mIdx, tid, note;

            tid = lnkTid.Text;
            List<string> ls = new List<string>();

            var xRows = dgRCA.Rows.Cast<DataGridViewRow>().Where(r => r.Cells["dgRCA_Chk"].Value?.ToString() == "1");
            if (xRows.Count() == 0)
            {
                MessageBox.Show("체크된 행이 없습니다.");
                return;
            }

            db.Open();
            foreach (DataGridViewRow row in xRows)
            {
                mIdx = row.Cells["dgRCA_Idx"].Value.ToString();
                sql = "delete from db_main.ta_rgst where idx=" + mIdx;
                db.ExeQry(sql);

                if (row.Cells["dgRCA_Hide"].Value?.ToString() == "1") continue; //숨김은 주요변동내역에서 제외

                ls.Add($"{(row.Cells["dgRCA_Sect"] as DataGridViewComboBoxCell).FormattedValue}{row.Cells["dgRCA_Rank"].Value}번 " +
                    $"{row.Cells["dgRCA_RcDt"].Value.ToString().Replace("-", ".")} " +
                    $"{(row.Cells["dgRCA_RgCd"] as DataGridViewComboBoxCell).FormattedValue} 말소");
            }

            if (ls.Count() > 0)
            {
                note = $"[{txtRgstDvsn.Text}] {string.Join(", ", ls.ToArray())}";
                sql = "insert into db_main.ta_impt_rec set tid=" + tid + ", ctgr=4, src=4, note='" + note + "', wdt=curdate()";
                db.ExeQry(sql);
            }
            db.Close();

            MessageBox.Show("삭제 되었습니다.");
            dg_SelectionChanged(null, null);
        }

        /// <summary>
        /// 등기변동 체크 추가(+)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRgstCmpAdd_Click(object sender, EventArgs e)
        {
            string sql, mIdx, tid, note;

            tid = lnkTid.Text;
            List<string> ls = new List<string>();
            
            var xRows = dgRCB.Rows.Cast<DataGridViewRow>().Where(r => r.Cells["dgRCB_Chk"].Value?.ToString() == "1");
            if (xRows.Count() == 0)
            {
                MessageBox.Show("체크된 행이 없습니다.");
                return;
            }
                        
            db.Open();
            foreach (DataGridViewRow row in xRows)
            {
                mIdx = row.Cells["dgRCB_Idx"].Value.ToString();
                sql = "insert into db_main.ta_rgst (tid, rg_dvsn, sect, rank, rank_s, rg_cd, rg_nm, rc_dt, rc_no, b_amt, c_amt, prsn, rg_no, mv_dt, fx_dt, bgn_dt, end_dt, r_eno, aply, ekey, take, note, adrs, brch, hide) " +
                    "select tid, rg_dvsn, sect, rank, rank_s, rg_cd, rg_nm, rc_dt, rc_no, b_amt, c_amt, prsn, rg_no, mv_dt, fx_dt, bgn_dt, end_dt, r_eno, aply, ekey, take, note, adrs, brch, hide from db_tank.tx_rgst_cmp where idx=" + mIdx;
                db.ExeQry(sql);

                if (row.Cells["dgRCB_Hide"].Value?.ToString() == "1") continue; //숨김은 주요변동내역에서 제외

                ls.Add($"{(row.Cells["dgRCB_Sect"] as DataGridViewComboBoxCell).FormattedValue}{row.Cells["dgRCB_Rank"].Value}번 " +
                    $"{row.Cells["dgRCB_RcDt"].Value.ToString().Replace("-", ".")} " +
                    $"{(row.Cells["dgRCB_RgCd"] as DataGridViewComboBoxCell).FormattedValue} 추가");
            }

            if (ls.Count() > 0)
            {
                note = $"[{txtRgstDvsn.Text}] {string.Join(", ", ls.ToArray())}";
                sql = "insert into db_main.ta_impt_rec set tid=" + tid + ", ctgr=4, src=4, note='" + note + "', wdt=curdate()";
                db.ExeQry(sql);
            }            
            db.Close();

            //MessageBox.Show(sb.ToString().Trim());
            MessageBox.Show("저장 되었습니다.");
            dg_SelectionChanged(null, null);
        }

        /// <summary>
        /// 등기변동 선택 업데이트
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRgstCmpUdt_Click(object sender, EventArgs e)
        {
            string sql, tid;
            string rgDvsn, rank, rankSub, rgCd, rgNo, rcNo;       //등기관련

            tid = lnkTid.Text;
            DataGridViewSelectedRowCollection rows=dgRCB.SelectedRows;
            if (rows.Count == 0)
            {
                MessageBox.Show("선택된 행이 없습니다.");
                return;
            }

            List<MySqlParameter> sp = new List<MySqlParameter>();

            if (txtRgstDvsn.Text == "토지등기") rgDvsn = "1";
            else
            {
                if (rdoRgstDvsn2.Checked) rgDvsn = "2";
                else rgDvsn = "3";
            }

            db.Open();
            foreach (DataGridViewRow row in rows)
            {                
                rank = row.Cells["dgRCB_Rank"].Value.ToString();
                if (rank.Contains("-"))
                {
                    Match match = Regex.Match(rank, @"(\d+)\-(\d+)", rxOptM);
                    rank = match.Groups[1].Value;
                    rankSub = match.Groups[2].Value;
                }
                else rankSub = "0";

                rgCd = row.Cells["dgRCB_RgCd"].Value?.ToString() ?? string.Empty;
                rgNo = row.Cells["dgRCB_RgNo"].Value?.ToString() ?? string.Empty;
                rcNo = row.Cells["dgRCB_RcNo"].Value?.ToString() ?? string.Empty;
                rgNo = Regex.Replace(rgNo, @"[\-\*]+", string.Empty);
                rcNo = Regex.Replace(rcNo, @"[제호]", string.Empty);

                sql = "update ta_rgst set sect=@sect, rank=@rank, rank_s=@rank_s, rg_cd=@rg_cd, rg_nm=@rg_nm, rc_dt=@rc_dt, b_amt=@b_amt, c_amt=@c_amt, prsn=@prsn, rg_no=@rg_no, mv_dt=@mv_dt, fx_dt=@fx_dt, bgn_dt=@bgn_dt, end_dt=@end_dt, ";
                sql += "r_eno=@r_eno, aply=@aply, ekey=@ekey, take=@take, note=@note, adrs=@adrs, brch=@brch, hide=@hide";
                sql += " where tid=@tid and rg_dvsn=@rg_dvsn and rc_no=@rc_no";
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@rg_dvsn", rgDvsn));
                sp.Add(new MySqlParameter("@rc_no", rcNo));
                sp.Add(new MySqlParameter("@sect", row.Cells["dgRCB_Sect"].Value?.ToString() ?? string.Empty));
                sp.Add(new MySqlParameter("@rank", rank));
                sp.Add(new MySqlParameter("@rank_s", rankSub));
                sp.Add(new MySqlParameter("@rg_cd", rgCd));
                sp.Add(new MySqlParameter("@b_amt", row.Cells["dgRCB_BAmt"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                sp.Add(new MySqlParameter("@c_amt", row.Cells["dgRCB_CAmt"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                sp.Add(new MySqlParameter("@rg_nm", ReNamePrsn(row.Cells["dgRCB_RgNm"].Value?.ToString() ?? "")));                
                sp.Add(new MySqlParameter("@prsn", ReNamePrsn(row.Cells["dgRCB_Prsn"].Value?.ToString() ?? "")));
                sp.Add(new MySqlParameter("@rg_no", rgNo));
                sp.Add(new MySqlParameter("@rc_dt", getDateParse(row.Cells["dgRCB_RcDt"].Value?.ToString() ?? "")));
                sp.Add(new MySqlParameter("@mv_dt", getDateParse(row.Cells["dgRCB_MvDt"].Value?.ToString() ?? "")));
                sp.Add(new MySqlParameter("@fx_dt", getDateParse(row.Cells["dgRCB_FxDt"].Value?.ToString() ?? "")));
                sp.Add(new MySqlParameter("@bgn_dt", getDateParse(row.Cells["dgRCB_BgnDt"].Value?.ToString() ?? "")));
                sp.Add(new MySqlParameter("@end_dt", getDateParse(row.Cells["dgRCB_EndDt"].Value?.ToString() ?? "")));
                sp.Add(new MySqlParameter("@r_eno", row.Cells["dgRCB_REno"].Value?.ToString() ?? ""));
                sp.Add(new MySqlParameter("@note", ReNamePrsn(row.Cells["dgRCB_Note"].Value?.ToString() ?? "")));
                sp.Add(new MySqlParameter("@adrs", row.Cells["dgRCB_Adrs"].Value?.ToString() ?? ""));
                sp.Add(new MySqlParameter("@brch", row.Cells["dgRCB_Brch"].Value?.ToString() ?? ""));
                sp.Add(new MySqlParameter("@aply", ((DataGridViewCheckBoxCell)row.Cells["dgRCB_Aply"]).Value?.ToString() ?? ""));
                sp.Add(new MySqlParameter("@ekey", ((DataGridViewCheckBoxCell)row.Cells["dgRCB_EKey"]).Value?.ToString() ?? ""));
                sp.Add(new MySqlParameter("@take", ((DataGridViewComboBoxCell)row.Cells["dgRCB_Take"]).Value?.ToString() ?? ""));
                sp.Add(new MySqlParameter("@hide", ((DataGridViewCheckBoxCell)row.Cells["dgRCB_Hide"]).Value?.ToString() ?? ""));
                db.ExeQry(sql, sp);
                sp.Clear();
            }
            db.Close();

            MessageBox.Show("저장 되었습니다.");
            dg_SelectionChanged(null, null);
        }
    }
}
