using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Text.RegularExpressions;
using HtmlAgilityPack;
using hap = HtmlAgilityPack;
using HAPDoc = HtmlAgilityPack.HtmlDocument;
using MySql.Data.MySqlClient;
using System.Collections;
using System.IO;
using System.Threading;
using System.Net;
using System.Drawing.Imaging;

namespace Solar.Comn
{
    public partial class wfPolicyNews : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        NetUtil net = new NetUtil();
        AuctCd auctCd = new AuctCd();
        ApiUtil api = new ApiUtil();

        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 1500, webCnt = 0;

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        //RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        string filePath;    //로컬 파일저장 경로
        List<MySqlParameter> sp = new List<MySqlParameter>();

        FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "NEWS", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);

        DataTable dtDataDvsn, dtSrcSite, dtCtgr, dtSidoCd;

        decimal totRowCnt = 0;
        string cdtn = "";

        public wfPolicyNews()
        {
            InitializeComponent();
            init();
        }

        /// <summary>
        /// 기존 데이터 마이그레이션
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnMig_Click(object sender, EventArgs e)
        {
            string sql, cvp, dvsn, ctgr, refIdx;

            Hashtable htDv = new Hashtable();
            htDv.Add(101, 10);
            htDv.Add(102, 11);
            htDv.Add(103, 12);
            htDv.Add(104, 13);
            htDv.Add(108, 14);
            htDv.Add(109, 15);

            Hashtable htCt = new Hashtable();
            htCt.Add(11, 1010);
            htCt.Add(12, 1011);
            htCt.Add(13, 1012);
            htCt.Add(14, 1013);
            htCt.Add(15, 1099);            
            htCt.Add(31, 1210);
            htCt.Add(32, 1211);
            htCt.Add(33, 1212);
            htCt.Add(34, 1213);
            htCt.Add(35, 1214);
            htCt.Add(36, 1215);
            htCt.Add(37, 1299);            
            htCt.Add(41, 1310);
            htCt.Add(42, 1311);
            htCt.Add(43, 1312);
            htCt.Add(44, 1313);
            htCt.Add(45, 1399);            
            htCt.Add(91, 1510);
            htCt.Add(92, 1511);
            htCt.Add(93, 1599);

            db.Open();
            db.ExeQry("TRUNCATE table tx_news");
            db.ExeQry("TRUNCATE table tx_news_attach");
            db.Close();

            DataTable dt = db.ExeDt("select * from db_tank.tc_policy order by idx");
            db.Open();
            foreach (DataRow row in dt.Rows)
            {
                dvsn = htDv[Convert.ToInt32(row["sector"])].ToString();
                ctgr = (row["code_kind"].ToString() == "0" || row["code_kind"].ToString() == "21") ? "0" : htCt[Convert.ToInt32(row["code_kind"])].ToString();

                cvp = "dvsn=@dvsn, src=@src, ctgr=@ctgr, org_nm=@org_nm, dpt=@dpt, title=@title, sub_title=@sub_title, lnk_url=@lnk_url, contents=@contents, si_cd=@si_cd, gu_cd=@gu_cd, dn_cd=@dn_cd, rdt=@rdt, wdt=@wdt, cover_img=@cover_img, vis=@vis";
                sql = "insert into tx_news set " + cvp;
                sp.Add(new MySqlParameter("@dvsn", dvsn));
                sp.Add(new MySqlParameter("@src", string.Empty));
                sp.Add(new MySqlParameter("@ctgr", ctgr));
                sp.Add(new MySqlParameter("@org_nm", row["company"]));
                sp.Add(new MySqlParameter("@dpt", row["office"]));
                sp.Add(new MySqlParameter("@title", row["title"]));
                sp.Add(new MySqlParameter("@sub_title", row["sub_title"]));
                sp.Add(new MySqlParameter("@lnk_url", row["p_link"]));
                sp.Add(new MySqlParameter("@contents", row["content"]));
                sp.Add(new MySqlParameter("@si_cd", row["si_cd"]));
                sp.Add(new MySqlParameter("@gu_cd", row["gu_cd"]));
                sp.Add(new MySqlParameter("@dn_cd", row["dn_cd"]));
                sp.Add(new MySqlParameter("@rdt", row["sdate"]));
                sp.Add(new MySqlParameter("@wdt", row["wdate"]));
                sp.Add(new MySqlParameter("@cover_img", row["photo"]));
                sp.Add(new MySqlParameter("@vis", 1));
                db.ExeQry(sql, sp);
                sp.Clear();

                if (row["p_file"].ToString() == string.Empty) continue;

                refIdx = ((UInt64)db.LastId()).ToString();
                sql = "insert into tx_news_attach set ref_idx='" + refIdx + "', file_nm='" + row["p_file"].ToString() + "', save_nm='" + row["p_file"].ToString() + "', wdt='" + row["wdate"].ToString() + "'";
                db.ExeQry(sql);
            }
            db.Close();

            MessageBox.Show("ok");
        }

        private void init()
        {
            int i = 0;

            ui.DgSetRead(dg, 0);
            ui.DgSetRead(dgF, 0);
            ui.DgSetRead(dgU, 0);
            ui.DgSetRead(dgn, 0);
            ui.DgSetRead(dgc, 0);
            ui.DgSetRead(dgS, 0);
            ui.DgSetRead(dgM, 0);
            ui.SetPagn(panPagn);

            //물건복사-전체선택/해제 체크박스
            dgn.CellPainting += (s, e) =>
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
                    chkAll.CheckedChanged += new EventHandler(dgnChkAll_CheckedChanged);
                    chkAll.Name = "HeaderChkAll";
                    ((DataGridView)s).Controls.Add(chkAll);
                    e.Handled = true;
                }
            };

            //파일저장 디렉토리 생성
            filePath = @"C:\정책뉴스\" + DateTime.Today.ToShortDateString();
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }

            //자료 구분
            dtDataDvsn = new DataTable();
            dtDataDvsn.Columns.Add("cd");
            dtDataDvsn.Columns.Add("nm");
            dtDataDvsn.Rows.Add(0, "-선택-");
            dtDataDvsn.Rows.Add(10, "정책알림");
            dtDataDvsn.Rows.Add(11, "도시계획");
            dtDataDvsn.Rows.Add(12, "경매판례");
            dtDataDvsn.Rows.Add(13, "법률/법령");
            dtDataDvsn.Rows.Add(14, "뉴스광장");
            dtDataDvsn.Rows.Add(15, "카페/블로그");
            dtDataDvsn.Rows.Add(16, "유튜브");
            dtDataDvsn.Rows.Add(90, "탱크소식");

            //자료 분류
            dtCtgr = new DataTable();
            dtCtgr.Columns.Add("dvsn");
            dtCtgr.Columns.Add("cd");
            dtCtgr.Columns.Add("nm");
            dtCtgr.Rows.Add(0, 0, "-선택-");
                //정책알림
            dtCtgr.Rows.Add(10, 1010, "국가");
            dtCtgr.Rows.Add(10, 1011, "교통");
            dtCtgr.Rows.Add(10, 1012, "주거");
            dtCtgr.Rows.Add(10, 1013, "주택");
            dtCtgr.Rows.Add(10, 1099, "기타");
                //경매판례
            dtCtgr.Rows.Add(12, 1210, "절차");
            dtCtgr.Rows.Add(12, 1211, "임대차");
            dtCtgr.Rows.Add(12, 1212, "법지권");
            dtCtgr.Rows.Add(12, 1213, "유치권");
            dtCtgr.Rows.Add(12, 1214, "배당");
            dtCtgr.Rows.Add(12, 1215, "명도");
            dtCtgr.Rows.Add(12, 1299, "기타");
                //법률/법령
            dtCtgr.Rows.Add(13, 1310, "법령");
            dtCtgr.Rows.Add(13, 1311, "규칙");
            dtCtgr.Rows.Add(13, 1312, "예규");
            dtCtgr.Rows.Add(13, 1313, "선례");
            dtCtgr.Rows.Add(13, 1399, "기타");
                //카페/블로그
            dtCtgr.Rows.Add(15, 1510, "카페");
            dtCtgr.Rows.Add(15, 1511, "블로그");
            dtCtgr.Rows.Add(15, 1599, "기타");

            //수집 사이트
            dtSrcSite = db.ExeDt("select dvsn, idx AS cd, trim(concat(site,' ',menu)) AS nm, url, pri from tx_news_site order by site, menu");
            DataRow row = dtSrcSite.NewRow();
            row["dvsn"] = 0;
            row["cd"] = 0;
            row["nm"] = "-선택-";
            row["url"] = "";
            row["pri"] = 0;
            dtSrcSite.Rows.InsertAt(row, 0);
            /*
            dtSrcSite.Columns.Add("dvsn");
            dtSrcSite.Columns.Add("cd");
            dtSrcSite.Columns.Add("nm");
            dtSrcSite.Columns.Add("url");
            dtSrcSite.Rows.Add(0, 0, "-선택-", "");
            
                //정책알림
            dtSrcSite.Rows.Add(10, 10, "정부24", "https://www.gov.kr/portal/gvrnPolicy?policyType=G00301&Mcode=11143");
            dtSrcSite.Rows.Add(10, 11, "한국부동산원", "https://www.reb.or.kr/reb/main.do");
            dtSrcSite.Rows.Add(10, 12, "통계청", "https://kostat.go.kr/portal/korea/kor_nw/1/10/1/index.board");
                //도시계획
            dtSrcSite.Rows.Add(11, 10, "서울생활권", "https://planning.seoul.go.kr/plan/main.do");
            dtSrcSite.Rows.Add(11, 11, "부산", "https://www.busan.go.kr/depart/agora00");
            dtSrcSite.Rows.Add(11, 12, "대구", "https://www.daegu.go.kr/build/index.do?menu_id=00933155");
            dtSrcSite.Rows.Add(11, 13, "인천", "https://www.incheon.go.kr/index");
            dtSrcSite.Rows.Add(11, 14, "울산", "https://www.ulsan.go.kr/u/metro/contents.ulsan?mId=001002003000000000");
                //경매판례
            dtSrcSite.Rows.Add(12, 10, "법원판례", "https://glaw.scourt.go.kr/wsjo/panre/sjo050.do");
                //법률/법령
            dtSrcSite.Rows.Add(13, 10, "법원법령", "https://glaw.scourt.go.kr/wsjo/lawod/sjo120.do");
                //뉴스광장
            dtSrcSite.Rows.Add(14, 10, "네이버부동산", "https://land.naver.com/news/");
            dtSrcSite.Rows.Add(14, 11, "다음부동산", "https://realestate.daum.net/news");
            dtSrcSite.Rows.Add(14, 12, "네이트부동산", "https://estate.nate.com/cp/news/open.asp?only=345&m_=6&g_=&silk_gnb=");
            */
            //정부24 분류
            dgc.Rows.Add(0, "공공질서 및 안전");
            dgc.Rows.Add(0, "과학기술");
            dgc.Rows.Add(0, "교육");
            dgc.Rows.Add(1, "교통 및 물류");
            dgc.Rows.Add(0, "국방");
            dgc.Rows.Add(0, "농림");
            dgc.Rows.Add(0, "문화체육관광");
            dgc.Rows.Add(0, "보건");
            dgc.Rows.Add(0, "사회복지");
            dgc.Rows.Add(0, "산업·통상·중소기업");
            dgc.Rows.Add(0, "일반공공행정");
            dgc.Rows.Add(1, "재정·세제·금융");
            dgc.Rows.Add(1, "지역개발");
            dgc.Rows.Add(0, "통신");
            dgc.Rows.Add(0, "통일·외교");
            dgc.Rows.Add(0, "해양수산");
            dgc.Rows.Add(0, "환경");
            dgc.Rows.Add(0, "종합일반");
            dgc.ClearSelection();

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

            cbxDataDvsn.DataSource = dtDataDvsn;
            cbxDataDvsn.DisplayMember = "nm";
            cbxDataDvsn.ValueMember = "cd";

            cbxSrchDataDvsn.DataSource = dtDataDvsn.Copy();
            cbxSrchDataDvsn.DisplayMember = "nm";
            cbxSrchDataDvsn.ValueMember = "cd";

            cbxScrapDataDvsn.DataSource = dtDataDvsn.Copy();
            cbxScrapDataDvsn.DisplayMember = "nm";
            cbxScrapDataDvsn.ValueMember = "cd";

            cbxSiteDataDvsn.DataSource = dtDataDvsn.Copy();
            cbxSiteDataDvsn.DisplayMember = "nm";
            cbxSiteDataDvsn.ValueMember = "cd";

            //cbxSrcSite.DataSource = dtSrcSite;
            //cbxSrcSite.DisplayMember = "nm";
            //cbxSrcSite.ValueMember = "cd";

            //자료구분-검색 > 수집사이트, 분류
            cbxSrchDataDvsn.SelectedIndexChanged += (s, e) =>
            {
                DataTable dt = dtSrcSite.Rows.Cast<DataRow>().Where(t => (t["dvsn"].ToString() == cbxSrchDataDvsn.SelectedValue.ToString() || t["dvsn"].ToString() == "0")).CopyToDataTable();                
                cbxSrchSrc.DataSource = dt;
                cbxSrchSrc.DisplayMember = "nm";
                cbxSrchSrc.ValueMember = "cd";

                dt = dtCtgr.Rows.Cast<DataRow>().Where(t => (t["dvsn"].ToString() == cbxSrchDataDvsn.SelectedValue.ToString() || t["dvsn"].ToString() == "0")).CopyToDataTable();
                cbxSrchCtgr.DataSource = dt;
                cbxSrchCtgr.DisplayMember = "nm";
                cbxSrchCtgr.ValueMember = "cd";
            };

            //자료구분-상세 > 수집사이트, 분류
            cbxDataDvsn.SelectedIndexChanged += (s, e) =>
            {
                DataTable dt = dtSrcSite.Rows.Cast<DataRow>().Where(t => (t["dvsn"].ToString() == cbxDataDvsn.SelectedValue.ToString() || t["dvsn"].ToString() == "0")).CopyToDataTable();
                cbxSrc.DataSource = dt;
                cbxSrc.DisplayMember = "nm";
                cbxSrc.ValueMember = "cd";

                dt = dtCtgr.Rows.Cast<DataRow>().Where(t => (t["dvsn"].ToString() == cbxDataDvsn.SelectedValue.ToString() || t["dvsn"].ToString() == "0")).CopyToDataTable();
                cbxCtgr.DataSource = dt;
                cbxCtgr.DisplayMember = "nm";
                cbxCtgr.ValueMember = "cd";
            };

            //자료구분-수집 > 수집사이트
            cbxScrapDataDvsn.SelectedIndexChanged += (s, e) =>
            {
                DataTable dt = dtSrcSite.Rows.Cast<DataRow>().Where(t => (t["dvsn"].ToString() == cbxScrapDataDvsn.SelectedValue.ToString() || t["dvsn"].ToString() == "0")).OrderByDescending(t => t["pri"]).CopyToDataTable();
                
                Hashtable htPri = new Hashtable();
                htPri.Add(0, "☆");
                htPri.Add(1, "★");
                htPri.Add(2, "★★");
                htPri.Add(3, "★★★");
                htPri.Add(4, "★★★★");
                htPri.Add(5, "★★★★★");

                dgS.Rows.Clear();
                foreach (DataRow r in dt.Rows)
                {
                    if (r["cd"].ToString() == "0") continue;

                    i = dgS.Rows.Add();
                    dgS["dgS_No", i].Value = i + 1;
                    dgS["dgS_Nm", i].Value = r["nm"];
                    dgS["dgS_Cd", i].Value = r["cd"];
                    dgS["dgS_Url", i].Value = r["url"];
                    dgS["dgS_Pri", i].Value = htPri[Convert.ToInt32(r["pri"])];
                }
                dgS.ClearSelection();
            };
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

            if (cbxSi.SelectedValue != null)
            {
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
                    try
                    {
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
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
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
        }

        private void btnSrch_Click(object sender, EventArgs e)
        {
            string sql;

            cdtn = "1";
            dg.Rows.Clear();

            List<string> cdtnList = new List<string>();

            if (cbxSrchDataDvsn.SelectedIndex > 0)
            {
                cdtnList.Add("dvsn=" + cbxSrchDataDvsn.SelectedValue.ToString());
            }
            if (cbxSrchCtgr.SelectedIndex > 0)
            {
                cdtnList.Add("ctgr=" + cbxSrchCtgr.SelectedValue.ToString());
            }
            if (cbxSrchSrc.SelectedIndex > 0)
            {
                cdtnList.Add("src=" + cbxSrchSrc.SelectedValue.ToString());
            }
            if (dtpRdtBgn.Checked)
            {
                cdtnList.Add($"rdt >= '{dtpRdtBgn.Value.ToShortDateString()}'");
            }
            if (dtpRdtEnd.Checked)
            {
                cdtnList.Add($"rdt <= '{dtpRdtEnd.Value.ToShortDateString()}'");
            }
            if (dtpWdtBgn.Checked)
            {
                cdtnList.Add($"wdt >= '{dtpWdtBgn.Value.ToShortDateString()}'");
            }
            if (dtpWdtEnd.Checked)
            {
                cdtnList.Add($"wdt <= '{dtpWdtEnd.Value.ToShortDateString()}'");
            }
            if (chkCoverErr.Checked)
            {
                cdtnList.Add("cover_img=''");
            }
            if (cbxSrchVis.SelectedIndex > 0)
            {
                if (cbxSrchVis.SelectedIndex == 1) cdtnList.Add("vis=1");
                else cdtnList.Add("vis=0");
            }

            if (cdtnList.Count > 0) cdtn = string.Join(" and ", cdtnList.ToArray());

            sql = "select COUNT(*) from tx_news where " + cdtn;

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
            string sql = "", order = "", dvsn = "", ctgr = "", src = "", fix;

            dg.Rows.Clear();

            ComboBox cbxPage = (ComboBox)sender;
            NumericUpDown listScale = (NumericUpDown)panPagn.Controls["_nudList"];
            startRow = (decimal.Parse(cbxPage.Text) - 1) * listScale.Value;
            dg.SelectionChanged -= dg_SelectionChanged;

            order = "fix desc, idx desc";
            sql = "select * from tx_news";
            sql += " where " + cdtn + " order by " + order + " limit " + startRow + "," + listScale.Value;

            this.Cursor = Cursors.WaitCursor;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {

                var xRow = dtSrcSite.Rows.Cast<DataRow>().Where(t => (t["dvsn"].ToString() == dr["dvsn"].ToString() && t["cd"].ToString() == dr["src"].ToString())).SingleOrDefault();
                if (xRow == null) src = string.Empty;
                else
                {
                    src = (dr["src"].ToString() == "0") ? string.Empty : xRow["nm"].ToString();
                }
                xRow = dtCtgr.Rows.Cast<DataRow>().Where(t => (t["dvsn"].ToString() == dr["dvsn"].ToString() && t["cd"].ToString() == dr["ctgr"].ToString())).SingleOrDefault();
                if (xRow == null) ctgr = string.Empty;
                else
                {
                    ctgr = (dr["ctgr"].ToString() == "0") ? string.Empty : xRow["nm"].ToString();
                }
                xRow = dtDataDvsn.Rows.Cast<DataRow>().Where(t => t["cd"].ToString() == dr["dvsn"].ToString()).SingleOrDefault();
                if (xRow == null) dvsn = string.Empty;
                else
                {
                    dvsn = (dr["dvsn"].ToString() == "0") ? string.Empty : xRow["nm"].ToString();
                }

                i = dg.Rows.Add();
                dg["dg_No", i].Value = totRowCnt - startRow - i;
                dg["dg_Dvsn", i].Value = dvsn;
                dg["dg_Ctgr", i].Value = ctgr;
                dg["dg_OrgNm", i].Value = dr["org_nm"];
                dg["dg_Title", i].Value = dr["title"];
                dg["dg_Src", i].Value = src;
                dg["dg_Rdt", i].Value = (dr["rdt"].ToString().Contains("0001")) ? string.Empty : $"{dr["rdt"]:yyyy.MM.dd}";
                dg["dg_Wdt", i].Value = $"{dr["wdt"]:yyyy-MM-dd}";
                dg["dg_Hit", i].Value = dr["hit"];
                dg["dg_Idx", i].Value = dr["idx"];

                if (dr["fix"].ToString() == "1") dg.Rows[i].DefaultCellStyle.BackColor = Color.PeachPuff;
                else dg.Rows[i].DefaultCellStyle.BackColor = Color.White;
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
            string sql, idx, coverImg, siCd, guCd, dnCd;

            //ui.FormClear(tabDtl);
            tbcR.SelectedTab = tabDtl;

            dgF.Rows.Clear();
            dgU.Rows.Clear();
            txtCoverImg.Text = string.Empty;

            if (dg.CurrentRow == null) return;
            i = dg.CurrentRow.Index;
            idx = dg["dg_Idx", i].Value.ToString();
            txtIdx.Text = idx;

            sql = "select * from tx_news where idx=" + idx;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            cbxDataDvsn.SelectedValue = dr["dvsn"];
            cbxSrc.SelectedValue = dr["src"];
            cbxCtgr.SelectedValue = dr["ctgr"];
            txtOrgNm.Text = dr["org_nm"].ToString();
            txtDptNm.Text = dr["dpt"].ToString();
            txtTitle.Text = dr["title"].ToString();
            txtLnkUrl.Text = dr["lnk_url"].ToString();
            txtContents.Text = dr["contents"].ToString();
            dtpRdt.Value = (dr["rdt"].ToString().Contains("0001")) ? DateTime.Now : Convert.ToDateTime(dr["rdt"]);
            chkVis.Checked = (dr["vis"].ToString() == "1") ? true : false;
            chkFix.Checked = (dr["fix"].ToString() == "1") ? true : false;
            siCd = dr["si_cd"].ToString();
            guCd = dr["gu_cd"].ToString();
            dnCd = dr["dn_cd"].ToString();
            coverImg = dr["cover_img"].ToString();
            if (coverImg == String.Empty) coverImg = "noimg.png";
            dr.Close();
            db.Close();

            cbxSi.SelectedValue = Convert.ToDecimal(siCd);
            cbxGu.SelectedValue = Convert.ToDecimal(guCd);
            cbxDn.SelectedValue = Convert.ToDecimal(dnCd);

            //첨부 파일
            db.Open();
            sql = "select * from tx_news_attach where ref_idx=" + idx;
            dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                i = dgF.Rows.Add();
                dgF["dgF_No", i].Value = i + 1;
                dgF["dgF_FileNm", i].Value = dr["file_nm"];
                dgF["dgF_SaveNm", i].Value = dr["save_nm"];
                dgF["dgF_Idx", i].Value = dr["idx"];
                dgF["dgF_Del", i].Value = "삭제";
            }            
            dr.Close();
            db.Close();
            dgF.ClearSelection();

            //커버 이미지
            if (coverImg == string.Empty)
            {
                pbxCover.Image = null;
                return;
            }

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://www.tankauction.com/FILE/NEWS/cover/" + coverImg);
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
                pbxCover.Image = img;
            }
            catch
            {                
                pbxCover.Image = null;
            }
        }

        /// <summary>
        /// 저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSave_Click(object sender, EventArgs e)
        {
            string refIdx, sql, cvp;
            string orgNm, dpt, title, subTitle, lnkUrl, contents, rDt, vis, fix, siCd, guCd, dnCd;
            string fileNm, saveNm, ext, locFile, rmtFile;

            refIdx = txtIdx.Text;

            if (cbxDataDvsn.SelectedIndex == 0)
            {
                MessageBox.Show("자료구분을 선택 해 주세요");
                return;
            }

            db.Open();
            cvp = "dvsn=@dvsn, src=@src, ctgr=@ctgr, org_nm=@org_nm, dpt=@dpt, title=@title, sub_title=@sub_title, lnk_url=@lnk_url, contents=@contents, si_cd=@si_cd, gu_cd=@gu_cd, dn_cd=@dn_cd, rdt=@rdt, vis=@vis, fix=@fix";
            if (refIdx == string.Empty)
            {
                sql = "insert into tx_news set " + cvp + ", wdt=curdate()";
            }
            else
            {
                sql = "update tx_news set " + cvp + " where idx=" + refIdx;
            }

            orgNm = txtOrgNm.Text.Trim();
            dpt = txtDptNm.Text.Trim();
            title = txtTitle.Text.Trim();
            subTitle = txtSubTitle.Text.Trim();
            lnkUrl = txtLnkUrl.Text.Trim();
            contents = txtContents.Text.Trim();
            rDt = dtpRdt.Value.ToShortDateString();
            vis = (chkVis.Checked) ? "1" : "0";
            fix = (chkFix.Checked) ? "1" : "0";
            siCd = cbxSi.SelectedValue.ToString();
            guCd = (cbxGu.SelectedValue == null) ? "0" : cbxGu.SelectedValue.ToString();
            dnCd = (cbxDn.SelectedValue == null) ? "0" : cbxDn.SelectedValue.ToString();

            sp.Add(new MySqlParameter("@dvsn", cbxDataDvsn.SelectedValue));
            sp.Add(new MySqlParameter("@src", cbxSrc.SelectedValue));
            sp.Add(new MySqlParameter("@ctgr", cbxCtgr.SelectedValue));
            sp.Add(new MySqlParameter("@org_nm", orgNm));
            sp.Add(new MySqlParameter("@dpt", dpt));
            sp.Add(new MySqlParameter("@title", title));
            sp.Add(new MySqlParameter("@sub_title", subTitle));
            sp.Add(new MySqlParameter("@lnk_url", lnkUrl));
            sp.Add(new MySqlParameter("@contents", contents));
            sp.Add(new MySqlParameter("@si_cd", siCd));
            sp.Add(new MySqlParameter("@gu_cd", guCd));
            sp.Add(new MySqlParameter("@dn_cd", dnCd));
            sp.Add(new MySqlParameter("@rdt", rDt));
            sp.Add(new MySqlParameter("@vis", vis));
            sp.Add(new MySqlParameter("@fix", fix));
            db.ExeQry(sql, sp);
            sp.Clear();

            if (refIdx == string.Empty)
            {
                refIdx = ((UInt64)db.LastId()).ToString();
            }
            db.Close();

            //커버 이미지
            if (txtCoverImg.Text != string.Empty)
            {
                locFile = txtCoverImg.Text;
                ext = Regex.Match(locFile, @"\.(\w{3,4})$", rxOptM).Groups[1].Value;
                saveNm = $"{refIdx.PadLeft(6, '0')}.{ext}";

                FileInfo fi = new FileInfo(locFile);
                fileNm = fi.Name;
                rmtFile = "/cover/" + saveNm;
                try
                {
                    if (ftp1.Upload(locFile, rmtFile))
                    {
                        sql = "update tx_news set cover_img='" + saveNm + "' where idx=" + refIdx;
                        db.Open();
                        db.ExeQry(sql);
                        db.Close();
                    }
                    else
                    {
                        //
                    }
                }
                catch
                {
                    //
                }
            }

            //파일 업로드
            if (dgU.Rows.Count > 0)
            {
                foreach (DataGridViewRow row in dgU.Rows)
                {
                    locFile = row.Cells["dgU_LocNm"].Value.ToString();
                    ext = Regex.Match(locFile, @"\.(\w{3,4})$", rxOptM).Groups[1].Value;
                    saveNm = $"{refIdx.PadLeft(6, '0')}-{DateTimeOffset.Now.ToUnixTimeMilliseconds()}.{ext}";

                    FileInfo fi = new FileInfo(locFile);
                    fileNm = fi.Name;
                    rmtFile = saveNm;
                    try
                    {
                        if (ftp1.Upload(locFile, rmtFile))
                        {
                            sql = "insert into tx_news_attach set ref_idx='" + refIdx + "', file_nm=@file_nm, save_nm='" + saveNm + "', wdt=curdate() ON DUPLICATE KEY UPDATE file_nm=@file_nm, wdt=curdate()";
                            sp.Add(new MySqlParameter("@file_nm", fileNm));
                            db.Open();
                            db.ExeQry(sql, sp);
                            sp.Clear();
                            db.Close();
                        }
                        else
                        {
                            row.Cells["dgU_Rslt"].Value = "업로드 실패";
                        }
                    }
                    catch (Exception ex)
                    {
                        row.Cells["dgU_Rslt"].Value = ex.Message;
                        continue;
                    }
                }
            }

            MessageBox.Show("저장 되었습니다.");
            //dg_SelectionChanged(null, null);
        }

        /// <summary>
        /// 원문 보기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkSrcUrl_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string lnkUrl;

            lnkUrl = txtLnkUrl.Text.Trim();
            if (lnkUrl == string.Empty) return;

            tbcL.SelectedTab = tabWbr1;
            //net.Nvgt(wbr1, lnkUrl);
            wbr1.Navigate(lnkUrl);
        }

        /// <summary>
        /// 파일 클릭(보기/개별 삭제)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgF_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int rowIdx;
            string sql, fileIdx, saveNm, url;

            rowIdx = e.RowIndex;
            saveNm = dgF["dgF_SaveNm", rowIdx].Value.ToString();

            if (dgF.Columns[e.ColumnIndex].CellType == typeof(DataGridViewLinkCell))
            {
                fileIdx = dgF["dgF_Idx", rowIdx].Value.ToString();
                if (MessageBox.Show("파일을 삭제 하시겠습니까?", "파일 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No)
                {
                    return;
                }                
                ftp1.FtpDelete(saveNm);
                sql = "delete from tx_news_attach where idx=" + fileIdx;
                db.Open();
                db.ExeQry(sql);
                db.Close();

                MessageBox.Show("삭제 되었습니다.");
                dg_SelectionChanged(null, null);
            }
            else
            {
                tbcL.SelectedTab = tabWbr1;
                url = "https://www.tankauction.com/FILE/NEWS/" + saveNm;
                wbr1.Navigate(url);
            }
        }

        private void dgU_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            string url, saveNm;

            saveNm = dgU["dgU_LocNm", e.RowIndex].Value.ToString().Replace("\\", "/");
            tbcL.SelectedTab = tabWbr1;
            url = "file:///" + saveNm;
            wbr1.Navigate(url);
        }

        /// <summary>
        /// 커버 이미지 찾기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOpenCover_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "이미지 (*.jpg,*.png,*.gif)|*.jpg;*.png;*.gif";
            ofd.FilterIndex = 3;
            ofd.Multiselect = false;
            if (ofd.ShowDialog() != DialogResult.OK) return;
            txtCoverImg.Text = ofd.FileName;
        }

        /// <summary>
        /// 업로드할 파일 찾기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOpenFiles_Click(object sender, EventArgs e)
        {
            int i = 0;
            dgU.Rows.Clear();

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "사진 (*.jpg)|*.jpg|문서 (*.pdf,*.html)|*.pdf;*.html|사진 및 문서 (*.jpg,*.pdf,*.html)|*.jpg;*.pdf;*.html";
            ofd.FilterIndex = 3;
            ofd.Multiselect = true;
            if (ofd.ShowDialog() != DialogResult.OK) return;

            foreach (string locFile in ofd.FileNames)
            {
                i = dgU.Rows.Add();
                dgU["dgU_No", i].Value = i + 1;
                dgU["dgU_LocNm", i].Value = locFile;
            }

            dgU.ClearSelection();
        }

        /// <summary>
        /// 뉴스 삭제
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnDel_Click(object sender, EventArgs e)
        {
            string sql, idx, coverNm;

            if (MessageBox.Show("자료를 삭제 하시겠습니까?", "자료 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            idx = txtIdx.Text;
            
            db.Open();
            sql = "select * from tx_news where idx=" + idx;
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            coverNm = dr["cover_img"].ToString();
            dr.Close();

            sql = "delete from tx_news where idx=" + idx;            
            db.ExeQry(sql);
            db.Close();

            DataTable dtF = db.ExeDt("select * from tx_news_attach where ref_idx=" + idx);
            db.Open();
            foreach (DataRow row in dtF.Rows)
            {
                ftp1.FtpDelete(row["save_name"].ToString());
                sql = "delete from tx_news_attach where idx=" + row["idx"].ToString();
                db.ExeQry(sql);
            }
            db.Close();

            if (coverNm != string.Empty)
            {
                ftp1.FtpDelete("/cover/" + coverNm);
            }

            MessageBox.Show("삭제 되었습니다.");
            //btnSrch_Click(null, null);
        }

        /// <summary>
        /// 신규 폼 리셋
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnNew_Click(object sender, EventArgs e)
        {
            ui.FormClear(tabDtl);
            chkVis.Checked = true;

            txtIdx.Text = string.Empty;
            if (pbxCover.Image != null) pbxCover.Image.Dispose();
            pbxCover.Image = null;
        }

        private void dgnChkAll_CheckedChanged(object sender, EventArgs e)
        {
            //
        }

        private void btnCrawlList_Click(object sender, EventArgs e)
        {
            string siteCd, url0, url, html, bgnDt, endDt;
            string title, orgn, wdt, ctgr, viewIdx;
            int i = 0;
            decimal n = 0, pgCnt = 0, totCnt = 0;

            dgn.Rows.Clear();
            btnCrawlList.Enabled = false;
            //siteCd = cbxSrcSite.SelectedValue.ToString();
            siteCd = "10";

            HAPDoc doc = new HAPDoc();
            bgnDt = string.Format("{0:yyyy.MM.dd}", dtpBgn.Value);
            endDt = string.Format("{0:yyyy.MM.dd}", dtpEnd.Value);

            switch (siteCd)
            {
                case "10" :     //정부24
                    List<string> lsCtgr = new List<string>();
                    foreach (DataGridViewRow row in dgc.Rows)
                    {
                        if (row.Cells[0].Value.ToString() == "1")
                        {
                            lsCtgr.Add(row.Cells[1].Value.ToString());
                        }
                    }

                    url0 = $"https://www.gov.kr/portal/gvrnPolicy?policyType=G00301&Mcode=11143&srchPeriodOption=direct&srchStDtFmt={bgnDt}&srchEdDtFmt={endDt}&searchField=3";
                    html = net.GetHtml(url0, Encoding.UTF8);
                    doc.LoadHtml(html);

                    HtmlNode nd = doc.DocumentNode.SelectNodes("//li[contains(@title,'정책뉴스 선택됨')]/a/span/strong/text()")[0];
                    totCnt = Convert.ToDecimal(Regex.Replace(nd.InnerText, @"[(,)]", string.Empty, rxOptM));
                    if (totCnt == 0)
                    {
                        btnCrawlList.Enabled = true;
                        MessageBox.Show("검색 결과가 없습니다.");
                        return;
                    }
                    pgCnt = Math.Ceiling(totCnt / (decimal)10);
                    //pgCnt = 1;

                    for (n = 1; n <= pgCnt; n++)
                    {
                        if (n > 1)
                        {
                            url = $"{url0}&pageIndex={n}";
                            html = net.GetHtml(url, Encoding.UTF8);
                            doc.LoadHtml(html);
                        }
                        HtmlNodeCollection ncLs = doc.DocumentNode.SelectNodes("//div[contains(@class,'right_detail')]");
                        foreach (HtmlNode ndLs in ncLs)
                        {
                            title = ndLs.SelectSingleNode("./dl/dt/a").InnerText.Trim();
                            orgn = ndLs.SelectNodes("./div/span/text()")[0].InnerText.Trim();
                            orgn = Regex.Replace(orgn, @"\s+", string.Empty, rxOptM);
                            wdt = ndLs.SelectNodes("./div/span/text()")[1].InnerText.Trim();
                            ctgr = ndLs.SelectNodes("./div/span/text()")[2].InnerText.Trim().Replace("&gt;", ">");
                            viewIdx = Regex.Match(ndLs.InnerHtml, @"goViewSubmit\('(\w+)'\)", rxOptM).Groups[1].Value;

                            if (!lsCtgr.Contains(Regex.Replace(ctgr, @">.*", string.Empty, rxOptM).Trim()) || orgn.Contains("KTV")) continue;

                            i = dgn.Rows.Add();
                            dgn["dgn_No", i].Value = i + 1;
                            dgn["dgn_Title", i].Value = title;
                            dgn["dgn_Ctgr", i].Value = ctgr;
                            dgn["dgn_Orgn", i].Value = orgn;
                            dgn["dgn_Wdt", i].Value = wdt;
                            dgn["dgn_Lnk", i].Value = viewIdx;
                            if (!dgn.Rows[i].Displayed) dgn.FirstDisplayedScrollingRowIndex = i;
                            Application.DoEvents();
                        }
                    }
                    break;
            }

            dgn.ClearSelection();
            btnCrawlList.Enabled = true;

            MessageBox.Show("목록검색이 완료 되었습니다.");
        }

        private void btnCrawl_Click(object sender, EventArgs e)
        {
            var chkRows = from DataGridViewRow row in dgn.Rows
                          where Convert.ToBoolean(row.Cells[0].Value) == true
                          select row;
            if (chkRows.Count() == 0)
            {
                MessageBox.Show("수집할 뉴스를 체크 해 주세요.");
                return;
            }

            if (MessageBox.Show("선택한 " + chkRows.Count().ToString() + "개의 뉴스를 수집 하시겠습니까?", "뉴스 수집", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

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
            string url, html, sql, refIdx;
            string ctgr = "", orgNm = "", rDt = "", title = "", contents = "";
            string fileNm, saveNm, ext, locFile, rmtFile;
            int seq = 0, sucCnt = 0, failCnt = 0;

            Dictionary<string, string> dicFileRslt;

            var chkRows = from DataGridViewRow row in dgn.Rows
                          where Convert.ToBoolean(row.Cells[0].Value) == true
                          select row;

            HAPDoc doc = new HAPDoc();

            dgn.ClearSelection();

            foreach (DataGridViewRow row in chkRows)
            {
                sucCnt = 0; failCnt = 0;

                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                row.Cells["dgn_No"].Style.BackColor = Color.LightGreen;     //현재 수집중인 Row 표시

                url = "https://www.gov.kr/portal/gvrnPolicy/view/" + row.Cells["dgn_Lnk"].Value.ToString();
                html = net.GetHtml(url, Encoding.UTF8);
                doc.LoadHtml(html);
                HtmlNode ndDtl = doc.DocumentNode.SelectSingleNode("//div[@class='tbl-view gallery-detail']");
                if (ndDtl == null) continue;
                html = ndDtl.InnerHtml;
                HtmlNode nd = ndDtl.SelectSingleNode("//li/span[contains(text(),'분류')]");
                if (nd != null) ctgr = nd.NextSibling.InnerText.Trim();
                nd = ndDtl.SelectSingleNode("//li/span[contains(text(),'원문출처')]");
                if (nd != null) orgNm = nd.NextSibling.InnerText.Trim();
                nd = ndDtl.SelectSingleNode("//li/span[contains(text(),'등록일')]");
                if (nd != null) rDt = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
                HtmlNodeCollection nc = ndDtl.SelectNodes("//div[@class='view-contents active']/div");
                if (nc.Count == 2)
                {
                    contents = nc[0].InnerText.Trim();
                    nd = nc[1].SelectSingleNode("./a");
                    if (nd != null) url = nd.GetAttributeValue("href", "");
                }                
                //contents = Regex.Replace(contents, @"^\s+", string.Empty, rxOptM);
                
                title = row.Cells["dgn_Title"].Value.ToString();

                db.Open();
                sql = "insert into tx_news set dvsn=@dvsn, src=@src, org_nm=@org_nm, title=@title, lnk_url=@lnk_url, contents=@contents, rdt=@rdt, wdt=curdate(), vis=1";
                sp.Add(new MySqlParameter("@dvsn", 10));
                //sp.Add(new MySqlParameter("@src", cbxSrcSite.SelectedValue));
                sp.Add(new MySqlParameter("@src", ""));
                sp.Add(new MySqlParameter("@org_nm", orgNm));
                sp.Add(new MySqlParameter("@title", title));
                sp.Add(new MySqlParameter("@lnk_url", url));
                //sp.Add(new MySqlParameter("@contents", contents));
                sp.Add(new MySqlParameter("@contents", string.Empty));
                sp.Add(new MySqlParameter("@rdt", rDt));
                db.ExeQry(sql, sp);
                sp.Clear();
                
                refIdx = ((UInt64)db.LastId()).ToString();
                db.Close();

                //첨부파일
                HtmlNodeCollection ncFile = ndDtl.SelectNodes("//li/span[@class='download']/a");
                if (ncFile != null)
                {
                    foreach (HtmlNode ndFile in ncFile)
                    {
                        webCnt++;
                        if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                        seq++;
                        url = ndFile.GetAttributeValue("href", "");
                        fileNm = ndFile.InnerText.Trim();
                        //MessageBox.Show(fileNm);
                        ext = Regex.Match(fileNm, @"\.(\w{3,4})$", rxOptM).Groups[1].Value;
                        //saveNm = $"{refIdx}_{seq}.{ext}";
                        saveNm = $"{refIdx.PadLeft(6, '0')}-{DateTimeOffset.Now.ToUnixTimeMilliseconds()}.{ext}";
                        locFile = $@"{filePath}\{saveNm}";
                        if (File.Exists(locFile)) continue;

                        rmtFile = saveNm;
                        try
                        {
                            //url = url.Replace("http://", "https://");
                            dicFileRslt = net.DnFile(url, locFile);                            
                            if (dicFileRslt["result"] == "success")
                            {
                                if (ftp1.Upload(locFile, rmtFile))
                                {
                                    sql = "insert into tx_news_attach set ref_idx='" + refIdx + "', file_nm=@file_nm, save_nm='" + saveNm + "', wdt=curdate() ON DUPLICATE KEY UPDATE file_nm=@file_nm, wdt=curdate()";
                                    sp.Add(new MySqlParameter("@file_nm", fileNm));
                                    db.Open();
                                    db.ExeQry(sql, sp);
                                    sp.Clear();
                                    db.Close();
                                    sucCnt++;
                                }
                            }
                            else
                            {
                                failCnt++;
                            }
                        }
                        catch
                        {
                            failCnt++;
                        }
                    }
                }

                if (!row.Displayed) dgn.FirstDisplayedScrollingRowIndex = row.Index;
                row.Cells["dgn_Rslt"].Value = $"성공-{sucCnt}, 실패-{failCnt}";
                if (failCnt > 0) row.Cells["dgn_Rslt"].Style.BackColor = Color.HotPink;
            }
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            MessageBox.Show("수집이 완료 되었습니다.");
        }

        private void dgn_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int rowIdx = 0, colIdx = 0;

            colIdx = e.ColumnIndex;
            rowIdx = e.RowIndex;

            if (colIdx == 0) return;

            tbcR.SelectedTab = tabWbr2;
            net.Nvgt(wbr2, "https://www.gov.kr/portal/gvrnPolicy/view/" + dgn["dgn_Lnk", rowIdx].Value.ToString());
        }

        /// <summary>
        /// 커버 이미지 캡처
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCapt_Click(object sender, EventArgs e)
        {
            int W = 0, H = 0;
            int x1, y1, x2, y2;
            string fileNm = string.Empty;
            
            ImageCodecInfo myImageCodecInfo = null;
            System.Drawing.Imaging.Encoder myEncoder = null;
            EncoderParameter myEncoderParameter = null;
            EncoderParameters myEncoderParameters = null;

            //image format & quality
            myImageCodecInfo = GetEncoderInfo("image/jpeg");
            myEncoder = System.Drawing.Imaging.Encoder.Quality;
            myEncoderParameter = new EncoderParameter(myEncoder, 95L);  // Save the bitmap as a JPEG file with quality level 75.
            myEncoderParameters = new EncoderParameters(1);
            myEncoderParameters.Param[0] = myEncoderParameter;

            x1 = markTL.Location.X + 22 + tbcL.Left + 3;
            y1 = markTL.Location.Y + 22 + tbcL.Top + 69;

            x2 = markBR.Location.X + 22 + tbcL.Left + 3;
            y2 = markBR.Location.Y + 22 + tbcL.Top + 69;

            W = x2 - x1 - 5;
            H = y2 - y1 - 5;

            try
            {
                if (pbxCover.Image != null) pbxCover.Image.Dispose();
                pbxCover.Image = null;
                fileNm = $@"{filePath}\C{DateTimeOffset.Now.ToUnixTimeMilliseconds()}.jpg";
                Bitmap bitmap = new Bitmap(W, H);
                Graphics g = Graphics.FromImage(bitmap);
                //g.DrawRectangle(new Pen(Color.Red), 0, 0, W, H);

                g.CopyFromScreen(new Point(x1 + 3, y1 + 3), new Point(0, 0), bitmap.Size);
                //bitmap.Save(@"C:\works\cover.jpg", myImageCodecInfo, myEncoderParameters);
                Bitmap bmpCover = new Bitmap(bitmap, 760, 1075);
                bmpCover.Save(fileNm, myImageCodecInfo, myEncoderParameters);
                pbxCover.Image = Image.FromFile(fileNm);
                txtCoverImg.Text = fileNm;
                
                //bitmap.Dispose();
                //bitmap = null;
                //bmpCover.Dispose();
                //bmpCover = null;
                //g.Dispose();
                //g = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }
            
            MessageBox.Show("커버 이미지가 생성 되었습니다.");
        }

        /// <summary>
        /// 이미지 코덱
        /// </summary>
        /// <param name="mimeType"></param>
        /// <returns></returns>
        private static ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            int j;
            ImageCodecInfo[] encoders;
            encoders = ImageCodecInfo.GetImageEncoders();
            for (j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }

        /// <summary>
        /// 수동 수집-해당 사이트 이동
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgS_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            string url;

            if (dgS.SelectedRows.Count == 0)
            {
                MessageBox.Show("사이트를 선택 해 주세요");
                return;
            }

            url = dgS.SelectedRows[0].Cells["dgS_Url"].Value.ToString();
            //wbr3.Navigate(url);
            webView2.Source = new Uri(url);
        }

        /// <summary>
        /// 수동 수집
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnScrap_Click(object sender, EventArgs e)
        {
            //string url;

            txtIdx.Text = String.Empty;

            btnNew_Click(null, null);

            if (dgS.SelectedRows.Count > 0)
            {
                cbxDataDvsn.SelectedValue= cbxScrapDataDvsn.SelectedValue;
                cbxSrc.SelectedValue = dgS.SelectedRows[0].Cells["dgS_Cd"].Value.ToString();
            }
            //url = wbr3.Url.ToString();
            //txtLnkUrl.Text = url;
            txtLnkUrl.Text = webView2.Source.ToString();
        }

        /// <summary>
        /// 사이트 관리-목록
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSiteList_Click(object sender, EventArgs e)
        {
            int i;
            string sql, cdtn, dvsn;

            dgM.SelectionChanged -= dgM_SelectionChanged;
            //btnSiteNew_Click(null, null);
            dgM.Rows.Clear();

            Hashtable htPri = new Hashtable();
            htPri.Add(0, "☆");
            htPri.Add(1, "★");
            htPri.Add(2, "★★");
            htPri.Add(3, "★★★");
            htPri.Add(4, "★★★★");
            htPri.Add(5, "★★★★★");

            cdtn = "1";
            if (cbxSiteDataDvsn.SelectedIndex > 0) cdtn += " and dvsn=" + cbxSiteDataDvsn.SelectedValue.ToString();

            sql = "select * from tx_news_site where " + cdtn + " order by idx desc";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                var xRow = dtDataDvsn.Rows.Cast<DataRow>().Where(t => t["cd"].ToString() == dr["dvsn"].ToString()).SingleOrDefault();
                if (dr["dvsn"].ToString() == "0") dvsn = string.Empty;
                else
                {
                    dvsn = (xRow == null) ? string.Empty : xRow["nm"].ToString();
                }

                i = dgM.Rows.Add();
                dgM["dgM_No", i].Value = i + 1;
                dgM["dgM_Dvsn", i].Value = dvsn;
                dgM["dgM_SiteNm", i].Value = dr["site"];
                dgM["dgM_MenuNm", i].Value = dr["menu"];
                dgM["dgM_Url", i].Value = dr["url"];
                dgM["dgM_Pri", i].Value = htPri[Convert.ToInt32(dr["pri"])];
                dgM["dgM_Idx", i].Value = dr["idx"];
                dgM["dgM_Wdt", i].Value = $"{dr["wdt"]:yyyy.MM.dd (ddd)}";
            }
            dr.Close();
            db.Close();

            dgM.ClearSelection();
            dgM.SelectionChanged += dgM_SelectionChanged;
        }

        /// <summary>
        /// 사이트 관리-신규
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSiteNew_Click(object sender, EventArgs e)
        {
            ui.FormClear(pnlSiteMgmt);
        }

        /// <summary>
        /// 사이트 관리-수정모드
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgM_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            string sql, idx;
            int rowIdx;

            rowIdx = dgM.CurrentRow.Index;
            if (rowIdx == -1) return;

            idx = dgM["dgM_Idx", rowIdx].Value.ToString();
            sql = "select * from tx_news_site where idx=" + idx;

            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            cbxSiteDataDvsn.SelectedValue = dr["dvsn"];
            txtSiteIdx.Text = dr["idx"].ToString();
            txtSiteNm.Text = dr["site"].ToString();
            txtMenuNm.Text = dr["menu"].ToString();
            txtSiteUrl.Text = dr["url"].ToString();
            nudPri.Value = Convert.ToDecimal(dr["pri"]);
            dr.Close();
            db.Close();
        }

        private void dgM_SelectionChanged(object sender, EventArgs e)
        {
            if (e == null) return;
            dgM_CellClick(null, null);
        }

        /// <summary>
        /// 사이트관리-저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSiteSave_Click(object sender, EventArgs e)
        {
            string sql, cvp, dvsn, siteNm, menuNm, url, idx;

            List<MySqlParameter> sp = new List<MySqlParameter>();

            if (cbxSiteDataDvsn.SelectedIndex < 1)
            {
                MessageBox.Show("사이트 구분을 선택 해 주세요.");
                return;
            }

            idx = txtSiteIdx.Text;
            dvsn = cbxSiteDataDvsn.SelectedValue.ToString();
            siteNm = txtSiteNm.Text.Trim();
            menuNm = txtMenuNm.Text.Trim();
            url = txtSiteUrl.Text.Trim();

            if (siteNm == string.Empty || url == string.Empty)
            {
                MessageBox.Show("사이트명과 URL을 입력 해 주세요");
                return;
            }

            cvp = "idx=@idx, dvsn=@dvsn, site=@site, menu=@menu, url=@url, pri=@pri";
            sql = "insert into tx_news_site set " + cvp + ", wdt=curdate() ON DUPLICATE KEY UPDATE " + cvp;
            sp.Add(new MySqlParameter("@idx", idx));
            sp.Add(new MySqlParameter("@dvsn", dvsn));
            sp.Add(new MySqlParameter("@site", siteNm));
            sp.Add(new MySqlParameter("@menu", menuNm));
            sp.Add(new MySqlParameter("@url", url));
            sp.Add(new MySqlParameter("@pri", nudPri.Value));
            db.Open();
            db.ExeQry(sql, sp);
            sp.Clear();
            db.Close();

            MessageBox.Show("저장 되었습니다.");

            btnSiteNew_Click(null, null);
            btnSiteList_Click(null, null);
        }
    }
}
