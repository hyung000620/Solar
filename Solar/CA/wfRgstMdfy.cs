using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.IE;
using OpenQA.Selenium.Support.UI;
using System.Threading;
using AutoItX3Lib;
using SeleniumExtras.WaitHelpers;

namespace Solar.CA
{
    public partial class wfRgstMdfy : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        NetUtil net = new NetUtil();
        AuctCd auctCd = new AuctCd();
        RgstPinTid rgstPinTid = new RgstPinTid();
        MouseCtrl mouse = new MouseCtrl();
        AutoItX3 at = new AutoItX3();

        DataTable dtDptCd, dtStateCd, dtCatCd, dtDpslCd, dtFileCd;

        string cdtn = "";
        decimal totRowCnt = 0;

        BackgroundWorker bgwork;
        ChromeDriverService drvSvc;
        ChromeDriver drv = null;

        InternetExplorerDriverService idrvSvc;
        IWebDriver idrv = null;

        string myWeb = Properties.Settings.Default.myWeb;
        FTPclient ftp1 = new FTPclient(Properties.Settings.Default.myFTP + "CA/", Properties.Settings.Default.myFTPUsr, Properties.Settings.Default.myFTPPwd);

        ProcessStartInfo psi = new ProcessStartInfo();

        //인터넷 등기소 계정/캐쉬/테스트 인쇄/파일저장 경로
        private string irosId = "";
        private string irosPwd = "";
        private const string irosEMoneyNo1 = "X8497440";
        private const string irosEMoneyNo2 = "5621";
        private const string irosEMoneyPwd = "jins3816";
        private bool cashBalance = true;
        private bool printTest = false;
        private int irosLoginCnt = 0;
        private string pdfSavePath = $@"C:\경매등기\변동\{DateTime.Now.ToShortDateString()}";
        //

        public wfRgstMdfy()
        {
            InitializeComponent();
            init();
        }

        private void init()
        {
            ui.DgSetRead(dg, 0);
            ui.SetPagn(panPagn);
            ui.DgSetRead(dgF, 0);
            ui.DgSetRead(dgU, 0);
            dg.MultiSelect = true;

            //전체 법원별 계코드 DataTable
            dtDptCd = db.ExeDt("select C.ca_cd, crt_cd, spt_cd, cs_cd, dpt_cd, dpt_nm from ta_cd_cs C , ta_cd_dpt D where C.ca_cd=D.ca_cd order by dpt_cd");

            //진행 상태
            dtStateCd = db.ExeDt("select sta1_cd, sta2_cd, sta1_nm, sta2_nm from ta_cd_sta order by sta2_cd");

            //물건종별 및 토지 지목
            dtCatCd = db.ExeDt("select cat1_cd, cat2_cd, cat3_cd, cat2_nm, cat3_nm, hide, bldg_type from ta_cd_cat where cat3_cd > 0 and hide=0 order by cat3_cd");

            //매각 구분
            dtDpslCd = db.ExeDt("select * from ta_cd_etc where dvsn=10 order by seq, cd");

            //파일 구분
            dtFileCd = db.ExeDt("select cd, nm from ta_cd_file order by cd");

            //인터넷 등기소 계정정보
            string staffId = Properties.Settings.Default.USR_ID;
            if (staffId == "zzangjin")
            {
                irosId = "rkwurk";
                irosPwd = "rkwurk08`";
            }
            else if (staffId == "solar" || staffId == "f22")
            {
                irosId = "gosegero";
                irosPwd = "palau7695~";
            }
            else if (staffId == "hy1224")
            {
                irosId = "drsli123";
                irosPwd = "model25!";
            }
            else if (staffId == "jiwon5338")
            {
                irosId = "won5338";
                irosPwd = "wl329246!!";
            }
            else if (staffId == "kjh5852")
            {
                irosId = "ji2147";
                irosPwd = "jin2147";
            }
            else
            {
                irosId = "rkwurk";
                irosPwd = "rkwurk08`";
            }
            
            //WinTitle 에서 포함하는 문자열로 셋팅
            at.AutoItSetOption("WinTitleMatchMode", 2);

            //열람 pdf 파일 저장폴더 생성
            if (!Directory.Exists(pdfSavePath))
            {
                Directory.CreateDirectory(pdfSavePath);
            }
        }

        private void btnSrch_Click(object sender, EventArgs e)
        {
            string sql;

            cdtn = "M.tid=L.tid and L.tid=D.tid and D.tid=S.tid and M.ls_idx=S.idx";
            dg.Rows.Clear();

            List<string> cdtnList = new List<string>();

            if (chkWorks.Checked) cdtnList.Add("enable=1 and (sta1 in (11,13) or sta2=1011) and proc=0 and hide=0 and (M.pin=pin_land or M.pin=pin_bldg)");
            if (cbxSrchProc.SelectedIndex > 0) cdtnList.Add($"M.proc={cbxSrchProc.SelectedIndex - 1}");
            if (cbxVis.SelectedIndex > 0) cdtnList.Add($"M.hide={cbxVis.SelectedIndex - 1}");            
            if (dtpWdtBgn.Checked) cdtnList.Add($"M.wdt >= '{dtpWdtBgn.Value.ToShortDateString()}'");
            if (dtpWdtEnd.Checked) cdtnList.Add($"M.wdt <= '{dtpWdtEnd.Value.ToShortDateString()}'");
            if (dtpPdtBgn.Checked) cdtnList.Add($"M.pdt >= '{dtpPdtBgn.Value.ToShortDateString()}'");
            if (dtpPdtEnd.Checked) cdtnList.Add($"M.pdt <= '{dtpPdtEnd.Value.ToShortDateString()}'");
            txtSrchTid.Text = txtSrchTid.Text.Replace("_", string.Empty).Trim();
            if (txtSrchSn.Text.Trim() != "")
            {
                Match match = Regex.Match(txtSrchSn.Text.Trim(), @"^(\d+)[\-]*(\d+)*[\-]*(\d+)*", RegexOptions.Multiline);
                if (match.Groups[3].Value != "") cdtnList.Add("sn1=" + match.Groups[1].Value + " and sn2=" + match.Groups[2].Value + " and pn=" + match.Groups[3].Value);   //2018-4567-8
                else if (match.Groups[2].Value != "") cdtnList.Add("sn1=" + match.Groups[1].Value + " and sn2=" + match.Groups[2].Value);   //2018-4567
                else if (match.Groups[1].Value != "") cdtnList.Add("sn2=" + match.Groups[1].Value);     //4567
            }
            if (txtSrchTid.Text.Trim() != "")
            {
                cdtnList.Add("L.tid IN (" + Regex.Replace(txtSrchTid.Text.Trim(), @"\D+", ",") + ")");
            }

            if (cdtnList.Count > 0) cdtn += " and " + string.Join(" and ", cdtnList.ToArray());

            sql = "select COUNT(*) from db_tank.tx_rgst_mdfy M, db_main.ta_list L, db_main.ta_dtl D, db_main.ta_ls S where " + cdtn;

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
            string sql = "", order = "";
            string saNo, csCd, dpt, state, cat, dpsl;

            dg.Rows.Clear();

            ComboBox cbxPage = (ComboBox)sender;
            NumericUpDown listScale = (NumericUpDown)panPagn.Controls["_nudList"];
            startRow = (decimal.Parse(cbxPage.Text) - 1) * listScale.Value;
            dg.SelectionChanged -= dg_SelectionChanged;

            //order = "M.idx desc";
            order = "M.wdt desc, sn1, sn2, pn, ls_no";
            sql = "select M.*, S.adrs, S.dvsn, sn1, sn2, pn, spt, dpt, cat3, dpsl_dvsn, sta2, bid_dt, owner, pin_land, pin_bldg from db_tank.tx_rgst_mdfy M, db_main.ta_list L, db_main.ta_dtl D, db_main.ta_ls S";
            sql += " where " + cdtn + " order by " + order + " limit " + startRow + "," + listScale.Value;

            this.Cursor = Cursors.WaitCursor;
            DataTable tmpDt = db.ExeDt(sql);
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                csCd = dr["spt"].ToString();
                saNo = (dr["pn"].ToString() == "0") ? $"{dr["sn1"]}-{dr["sn2"]}" : $"{dr["sn1"]}-{dr["sn2"]}({dr["pn"]})";
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
                dg["dg_Spt", i].Value = dr["spt"];
                dg["dg_Dpt", i].Value = dpt;
                dg["dg_SN", i].Value = saNo;
                dg["dg_State", i].Value = state;
                dg["dg_Cat", i].Value = cat;
                dg["dg_Dpsl", i].Value = dpsl;
                dg["dg_LsNo", i].Value = dr["ls_no"];                
                dg["dg_Pin", i].Value = dr["pin"];
                dg["dg_BidDt", i].Value = (dr["bid_dt"].ToString().Contains("0001")) ? "예정물건" : $"{dr["bid_dt"]:yyyy.MM.dd}";
                dg["dg_Adrs", i].Value = dr["adrs"];
                dg["dg_Owner", i].Value = dr["owner"];
                dg["dg_WDt", i].Value = $"{dr["wdt"]:yyyy.MM.dd}";
                dg["dg_Enable", i].Value = (dr["enable"].ToString() == "0") ? "" : "○";
                dg["dg_Proc", i].Value = (dr["proc"].ToString() == "0") ? "" : "○";
                dg["dg_PDt", i].Value = (dr["pdt"].ToString().Contains("0001")) ? "-" : $"{dr["pdt"]:yyyy.MM.dd}";
                dg["dg_Idx", i].Value = dr["idx"];

                //기존 발급한 등기와 같다면
                if ($"{dr["pin"]}" == $"{dr["pin_land"]}".Replace("-", "") || $"{dr["pin"]}" == $"{dr["pin_bldg"]}".Replace("-", ""))
                {
                    dg["dg_Pin", i].Style.BackColor = ($"{dr["proc"]}" == "1") ? Color.LightGreen : Color.Gold;
                }
                else
                {
                    dg["dg_Pin", i].Style.BackColor = Color.White;
                }
                if ($"{dr["hide"]}" == "1") dg.Rows[i].DefaultCellStyle.BackColor = Color.Silver;
            }
            dr.Close();
            db.Close();
            dg.ClearSelection();
            this.Cursor = Cursors.Default;
            dg.SelectionChanged += dg_SelectionChanged;
            /*
            db.Open();
            foreach (DataRow row in tmpDt.Rows)
            {
                sql = $"insert into db_tank.tx_rgst_auto set dvsn=13, tid={row["tid"]}, ls_no={row["ls_no"]}, ls_type='{row["dvsn"]}', pin={row["pin"]}, wdt=curdate(), wtm=curtime()";
                db.ExeQry(sql);
            }
            db.Close();
            MessageBox.Show("ok");
            */
        }

        private void dg_SelectionChanged(object sender, EventArgs e)
        {
            int i = 0, n = 0;
            string tid, tbl, spt, sn1, sn2, sn, sql;

            dgF.Rows.Clear();

            if (dg.CurrentRow == null) return;
            i = dg.CurrentRow.Index;
            tid = dg["dg_Tid", i].Value.ToString();
            spt = dg["dg_Spt", i].Value.ToString();

            Match match = Regex.Match(dg["dg_SN", i].Value.ToString(), @"(\d+)\-(\d+)");
            sn1 = match.Groups[1].Value;
            sn2 = match.Groups[2].Value;
            sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));
            tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
            sql = "select * from " + tbl + " where ctgr in ('DA','DB') and (tid=" + tid + " or (spt=" + spt + " and sn='" + sn + "' and tid=0)) order by ctgr";
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
            }
            dr.Close();
            db.Close();
            
            dgF.ClearSelection();
        }

        /// <summary>
        /// 처리상태 변경
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnChProc_Click(object sender, EventArgs e)
        {
            string sql, hide = "0";
            
            DataGridViewSelectedRowCollection selRows = dg.SelectedRows;
            if (selRows.Count == 0)
            {
                MessageBox.Show("선택한 사건이 없습니다.");
                return;
            }

            if (cbxChVis.SelectedIndex == 0)
            {
                MessageBox.Show("숨김/보임 상태값을 선택 해 주세요");
                return;
            }

            if (MessageBox.Show("선택한 사건의 상태를 변경하시겠습니까?", "숨김/보임/조대 상태변경", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No) return;

            db.Open();
            if (cbxChVis.Text == "숨김" || cbxChVis.Text == "보임")
            {
                if (cbxChVis.Text == "숨김") hide = "1";
                else if (cbxChVis.Text == "보임") hide = "0";
                                
                foreach (DataGridViewRow row in selRows)
                {
                    sql = "update db_tank.tx_rgst_mdfy set hide='" + hide + "' where idx=" + row.Cells["dg_Idx"].Value.ToString();
                    db.ExeQry(sql);
                }
            }
            else if (cbxChVis.Text == "조대")
            {
                foreach (DataGridViewRow row in selRows)
                {
                    sql = "update db_tank.tx_rgst_mdfy set enable=0 where idx=" + row.Cells["dg_Idx"].Value.ToString();
                    db.ExeQry(sql);
                }
            }
            db.Close();

            MessageBox.Show("변경 되었습니다.");
            btnSrch_Click(null, null);
        }

        /// <summary>
        /// 탱크 링크-내부 저장된 파일 보기(문서, 사진 등)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgF_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            string url, tid;
            if (e.ColumnIndex == 0) return;

            if (dg.CurrentRow == null) return;            
            tid = dg["dg_Tid", dg.CurrentRow.Index].Value.ToString();

            tbcL.SelectedTab = tabWbr;
            url = string.Format(myWeb + "SOLAR/caFileViewer.php?tid={0}&idx={1}", tid, dgF["dgF_Idx", e.RowIndex].Value.ToString());
            wbr2.Navigate(url);
        }

        /// <summary>
        /// 파일 삭제
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnDelFiles_Click(object sender, EventArgs e)
        {
            int i;
            string idx, tbl, sql, year, rmtFile;
            
            if (dg.CurrentRow == null) return;
            i = dg.CurrentRow.Index;

            Match match = Regex.Match(dg["dg_SN", i].Value.ToString(), @"(\d+)\-(\d+)");
            year = match.Groups[1].Value;

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
            dg_SelectionChanged(null, null);
            
            /*
            i = 0;
            DataTable dt = db.ExeDt("select tid, pdt from db_tank.tx_rgst_mdfy where proc=1 order by pdt asc");
            db.Open();
            foreach (DataRow row in dt.Rows)
            {
                i++;
                sql = "update ta_list set rgst_udt='" + row["pdt"].ToString() + "' where tid=" + row["tid"].ToString();
                db.ExeQry(sql);
                break;
            }
            db.Close();
            MessageBox.Show($"ok-{i}");
            */
        }

        /// <summary>
        /// 등기소 장바구니 담기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dg_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            string pin, tid, url;

            if (e.ColumnIndex < 0) return;

            DataGridView dgv = sender as DataGridView;
            int rowIdx = e.RowIndex;
            if (rowIdx == -1) return;
            string colNm = dgv.Columns[e.ColumnIndex].Name;
            if (dgv[e.ColumnIndex, rowIdx].Value == null) return;

            if (colNm == "dg_Pin")
            {
                pin = dgv[e.ColumnIndex, rowIdx].Value.ToString();
                if (pin == string.Empty) return;
                Clipboard.SetText(pin);
                /*
                regt_no = pin.Substring(0, 4);                
                Uri uri = new Uri("http://www.iros.go.kr/iris/index.jsp?inpSvcCls=on&selkindcls=&e001admin_regn1=&e001admin_regn3=&a312lot_no=&a301buld_name=&a301buld_no_buld=&a301buld_no_room=&pin=" + pin + "&regt_no=" + regt_no + "&svc_cls=VW&fromjunja=Y&y202cmort_flag=Y&y202trade_seq_flag=Y&inpCmortCls=Y&inpTradeCls=Y");
                webView.Source = uri;
                                
                if (!psi.FileName.Contains("chrome.exe"))
                {
                    psi.FileName = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
                    psi.Arguments = @"http://www.iros.go.kr/";
                    Process.Start(psi);
                }
                */
            }
            else if (colNm == "dg_Tid")
            {
                tid = dgv[e.ColumnIndex, rowIdx].Value.ToString();
                tbcR.SelectedTab = tabWeb;                
                url = "/ca/caView.php?tid=" + tid;
                net.TankWebView(wbr1, url);
                /*
                wfCaMgmt caMgmt = new wfCaMgmt() { Owner = this };
                caMgmt.StartPosition = FormStartPosition.CenterScreen;
                caMgmt.FormBorderStyle = FormBorderStyle.FixedToolWindow;
                caMgmt.txtSrchTid.Text = tid;
                caMgmt.btnSrch_Click(null, null);
                //caMgmt.ShowDialog();
                //caMgmt.Dispose();
                caMgmt.Show();
                Clipboard.SetText(tid);
                */
            }
        }

        /// <summary>
        /// 등기파일 찾기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOpenFiles_Click(object sender, EventArgs e)
        {
            int i = 0;
            string tid, ctgr, rmtNm, newNm, sql, spt, sn, pn, state, seqNo, rgstDvsn, docNo, pin;

            dgU.Rows.Clear();

            OpenFileDialog ofd = new OpenFileDialog();            
            ofd.Filter = "등기 (*.pdf)|*.pdf";
            ofd.Multiselect = true;
            if (ofd.ShowDialog() != DialogResult.OK) return;
            
            foreach (string fullNm in ofd.FileNames)
            {
                tid = string.Empty;
                state = string.Empty;
                rgstDvsn = string.Empty;
                pin = string.Empty;
                newNm = fullNm;

                try
                {
                    Match match = Regex.Match(fullNm, @"(\d+)_(\d+)\-*(\d+)*.\w+", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        rmtNm = getRmtNm(fullNm);
                        tid = match.Groups[1].Value;
                        rgstDvsn = (match.Groups[2].Value == "4") ? "토지등기" : "건물(집합)등기";
                    }
                    else
                    {
                        Dictionary<string, string> dicRgstPT = rgstPinTid.Proc(fullNm);
                        if (dicRgstPT["result"] == "fail")
                        {
                            rmtNm = "오류";
                        }
                        else
                        {
                            tid = dicRgstPT["tid"];
                            ctgr = dicRgstPT["dvsnCd"];
                            rgstDvsn = dicRgstPT["dvsn"];
                            pin = dicRgstPT["pin"];
                            docNo = (ctgr == "DA") ? "4" : "5";     //작업용No (토지-4, 건물-5)
                            sql = "select spt, sn1, sn2, pn, sta2 from ta_list where tid=" + tid + " limit 1";
                            db.Open();
                            MySqlDataReader dr = db.ExeRdr(sql);
                            dr.Read();
                            if (dr.HasRows)
                            {
                                spt = dr["spt"].ToString();
                                sn = string.Format("{0}{1}", dr["sn1"], dr["sn2"].ToString().PadLeft(6, '0'));
                                pn = dr["pn"].ToString().PadLeft(4, '0');
                                seqNo = "01";
                                rmtNm = string.Format("{0}-{1}-{2}-{3}-{4}.pdf", ctgr, spt, sn, pn, seqNo);
                                var xState = dtStateCd.Rows.Cast<DataRow>().Where(t => t["sta2_cd"].ToString() == dr["sta2"].ToString()).SingleOrDefault();
                                state = (xState == null || dr["sta2"].ToString() == "0") ? string.Empty : xState.Field<string>("sta2_nm");

                                FileInfo fi = new FileInfo(fullNm);
                                //newNm = $@"{fi.DirectoryName}\{tid}_{docNo}-1.pdf";
                                newNm = $@"{fi.DirectoryName}\{tid}_{docNo}.pdf";
                                File.Move(fullNm, newNm);
                            }
                            else
                            {
                                rmtNm = "오류-해당 물건 없음(" + tid + ")";
                            }
                            dr.Close();
                            db.Close();
                        }
                    }

                    i = dgU.Rows.Add();
                    dgU["dgU_No", i].Value = i + 1;
                    dgU["dgU_LocFile", i].Value = newNm;
                    dgU["dgU_Ctgr", i].Value = rgstDvsn;
                    dgU["dgU_Tid", i].Value = tid;
                    dgU["dgU_Pin", i].Value = pin;
                    dgU["dgU_State", i].Value = state;
                    dgU["dgU_RmtFile", i].Value = rmtNm;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message + "-" + fullNm);
                    continue;
                }
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
            string fileNm, tid, ctgr, sql, spt, sn, pn, seqNo, rmtNm;

            Dictionary<string, string> dicRgstPT = rgstPinTid.Proc(fullNm);
            if (dicRgstPT["result"] == "false")
            {
                rmtNm = "오류";
            }

            tid = dicRgstPT["tid"];
            ctgr = dicRgstPT["dvsnCd"];

            FileInfo fi = new FileInfo(fullNm);
            fileNm = fi.Name;

            sql = "select spt, sn1, sn2, pn from ta_list where tid=" + tid + " limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            if (dr.HasRows)
            {
                spt = dr["spt"].ToString();
                sn = string.Format("{0}{1}", dr["sn1"], dr["sn2"].ToString().PadLeft(6, '0'));
                pn = dr["pn"].ToString().PadLeft(4, '0');
                seqNo = "01";
                rmtNm = string.Format("{0}-{1}-{2}-{3}-{4}.pdf", ctgr, spt, sn, pn, seqNo);
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
        /// FTP 업로드
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
            string locFile, rmtFile, rmtNm, fileNm, rmtPath;
            string sql, tbl, tid, ctgr, spt, sn, state, year, cvp, analyRslt, pin;

            //RgstAnaly rgstAnaly = new RgstAnaly();    //pdfFactory
            RgstAnalyNew rgstAnaly = new RgstAnalyNew();

            foreach (DataGridViewRow row in dgU.Rows)
            {
                rmtNm = row.Cells["dgU_RmtFile"].Value.ToString();
                if (rmtNm.Contains("오류")) continue;

                tid = row.Cells["dgU_Tid"].Value.ToString();
                state = row.Cells["dgU_State"].Value.ToString();
                pin = row.Cells["dgU_Pin"].Value.ToString();
                locFile = row.Cells["dgU_LocFile"].Value.ToString();
                FileInfo fi = new FileInfo(locFile);
                fileNm = fi.Name;
                //ext = fi.Extension ?? "";
                ctgr = rmtNm.Substring(0, 1);
                Match match = Regex.Match(rmtNm, @"([A-F].)\-(\d{4})\-(\d{10})", RegexOptions.IgnoreCase);
                ctgr = match.Groups[1].Value;
                spt = match.Groups[2].Value;
                sn = match.Groups[3].Value;
                year = sn.Substring(0, 4);
                rmtPath = string.Format(@"{0}/{1}/{2}", ctgr, spt, year);
                rmtFile = string.Format(@"{0}/{1}", rmtPath, rmtNm);
                if (ftp1.Upload(locFile, rmtFile))
                {
                    //DB 처리
                    db.Open();
                    tbl = (Convert.ToDecimal(year) > 2004) ? ("ta_f" + year) : "ta_f2004";
                    cvp = "ctgr='" + ctgr + "', spt='" + spt + "', tid='" + tid + "', sn='" + sn + "', file='" + rmtNm + "', wdt=curdate()";
                    sql = "insert into " + tbl + " set " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;                    
                    db.ExeQry(sql);
                    db.Close();

                    if (state == "매각준비")
                    {
                        //예정물건(매각준비)인 경우 기존내용 업데이트
                        analyRslt = rgstAnaly.Proc(locFile, true, false);                        
                    }
                    else
                    {
                        //진행 및 미진행 물건인 경우 변경전/후 비교를 위하여 별도 테이블에 기록
                        analyRslt = rgstAnaly.Proc(locFile, true, true);
                    }

                    db.Open();
                    sql = "update db_tank.tx_rgst_mdfy set proc=1, pdt=curdate() where tid='" + tid + "' and pin='" + pin + "' and proc=0";
                    db.ExeQry(sql);

                    sql = "update db_main.ta_list set rgst_udt=curdate() where tid='" + tid + "'";
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
            MessageBox.Show("업로드 완료");
        }

        /// <summary>
        /// 등기발급 클릭
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnIssue_Click(object sender, EventArgs e)
        {
            int selRowCnt = 0;
            /*
            selRowCnt = dg.SelectedRows.Count;
            if (selRowCnt == 0)
            {
                MessageBox.Show("선택된 사건이 없습니다.");
                return;
            }

            if (MessageBox.Show($"{selRowCnt}건을 발급 하시겠습니까?", "발급확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }
            */
            CheckForIllegalCrossThreadCalls = false;
            bgwork = new BackgroundWorker();
            bgwork.DoWork += Bgwork_DoWorkIssue;
            bgwork.RunWorkerCompleted += Bgwork_RunWorkerCompletedIssue;
            bgwork.WorkerReportsProgress = false;
            bgwork.WorkerSupportsCancellation = true;

            bgwork.RunWorkerAsync();
        }

        public IJavaScriptExecutor js;
        

        private void Bgwork_DoWorkIssue(object sender, DoWorkEventArgs e)
        {
            int i = 0, rowCnt = 0;
            string currentWindowHandle = "";
            
            Size scrSize = Screen.PrimaryScreen.Bounds.Size;

            if (dg.SelectedRows.Count == 0)
            {
                goto PDF_EXPORT;
            }

            //Step-1 > 장바구니 담기 및 결제-Chrome 사용
            drvSvc = ChromeDriverService.CreateDefaultService(@"C:\Atom", "chromedriver.exe");
            drvSvc.HideCommandPromptWindow = true;            
            ChromeOptions chOpt = new ChromeOptions();
            //chOpt.AddArgument("--headless");
            chOpt.AddArgument($"--window-size=1000,{scrSize.Height}");
            chOpt.AddArgument("--disable-gpu");
            chOpt.AddArgument("--no-sandbox");
            chOpt.AddArgument("--disable-dev-shm-usage");

            //try
            //{
                drv = new ChromeDriver(drvSvc, chOpt);
                this.js = (IJavaScriptExecutor)this.drv;
                drv.Navigate().GoToUrl("http://www.iros.go.kr");
                currentWindowHandle = drv.CurrentWindowHandle;   //메인 윈도우
                Thread.Sleep(5000);
                RgstLogin(drv);     //로그인
                //Thread.Sleep(5000);

                //팝업창 닫기
                if (drv.WindowHandles.Count > 0)
                {
                    foreach (string winNm in drv.WindowHandles)
                    {
                        drv.SwitchTo().Window(winNm);
                        if (drv.Url.Contains("popupid"))
                        {
                            drv.Close();
                        }
                    }
                    drv.SwitchTo().Window(currentWindowHandle);
                }
            //}
            //catch(Exception ex)
            //{
                /*
                irosLoginCnt++;                
                if (irosLoginCnt < 4)
                {
                    Thread.Sleep(3000);
                    RgstLogin(drv);
                }
                */
                //MessageBox.Show(ex.Message);
                //drv.Quit();
                //return;
            //}
            
            rowCnt = dg.SelectedRows.Count;
            foreach (DataGridViewRow row in dg.SelectedRows.Cast<DataGridViewRow>().Reverse())            
            {
                i++;
                RgstCartPay(row, i, rowCnt);

                //전자민원캐시의 잔액이 부족한 경우
                if (cashBalance == false)
                {
                    break;
                }
            }
            drv.Quit();


            PDF_EXPORT:

            //Step-2 > PDF 파일로 저장(인쇄)-InternetExplorer 사용
            RgstIssue();
        }
                
        /// <summary>
        /// 인터넷등기소 로그인
        /// </summary>
        /// <param name="drv"></param>
        private void RgstLogin(ChromeDriver drv)
        {
            irosLoginCnt++;

            if (irosLoginCnt > 5)
            {
                MessageBox.Show("로그인 실패");
            }
            
            if (WaitVisible(drv, By.XPath("//*[@id='id_user_id']")))
            {
                this.js.ExecuteScript("javascript:$('#id_user_id').val('" + irosId + "');", Array.Empty<object>());
                this.js.ExecuteScript("javascript:$('#password').val('" + irosPwd + "');", Array.Empty<object>());
                //drv.FindElement(By.XPath(@"//*[@id='leftS']/div[2]/form/div[1]/ul/li[4]/a/img")).Click();
                this.js.ExecuteScript("f_gosubmit();return false;", Array.Empty<object>());
                Thread.Sleep(5000);
                if (!drv.PageSource.Contains("로그아웃"))
                {
                    RgstLogin(drv);
                }
                /*
                if (!WaitVisible(drv, By.XPath("//*[@id='leftS']/div[2]/form/div[1]/ul/li[4]/a/img")))
                {
                    RgstLogin(drv);     //로그인 재시도
                }
                */
            }
            else
            {
                RgstLogin(drv);
            }
        }

        /// <summary>
        /// 장바구니 담기 및 결제
        /// </summary>
        /// <param name="row"></param>
        private void RgstCartPay(DataGridViewRow row, int rowNo, int rowCnt)
        {
            string tid, pin, idx, html, msg, sql;
            bool prcRslt = true, cmortOver = false, tradeOver = false;
            int lsNo = 0;

            tid = row.Cells["dg_Tid"].Value.ToString();
            pin = row.Cells["dg_Pin"].Value.ToString();
            idx = row.Cells["dg_Idx"].Value.ToString();

            drv.Navigate().GoToUrl("http://www.iros.go.kr/iris/index.jsp?isu_view=view");
            string currentWindowHandle = drv.CurrentWindowHandle;   //메인 윈도우

            try
            {
                drv.SwitchTo().Frame("inputFrame");
                this.js.ExecuteScript("f_goPin_click();return false;", Array.Empty<object>());
            }
            catch(Exception ex)
            {
                prcRslt = false;
                msg = "탭이동 오류-" + ex.Message;
                RgstAutoErr(tid, pin, msg);
            }
            finally
            {
                drv.SwitchTo().Window(currentWindowHandle);
            }
            if (!prcRslt) return;

            Thread.Sleep(1000);
            try
            {
                drv.SwitchTo().Frame("resultFrame");
                drv.SwitchTo().Frame("frmOuterModal");
                this.js.ExecuteScript("javascript:$('#inpPinNo').val('" + pin + "');", Array.Empty<object>());                
                //this.js.ExecuteScript("javascript:$('#y202cmort_check').prop('checked',true).trigger('change')", Array.Empty<object>());
                //this.js.ExecuteScript("javascript:$('#y202trade_check').prop('checked',true).trigger('change')", Array.Empty<object>());
                drv.FindElement(By.Id("y202cmort_check")).Click();
                Thread.Sleep(700);
                drv.FindElement(By.Id("y202trade_check")).Click();
                Thread.Sleep(700);
                this.js.ExecuteScript("return f_search(this.form, 1, 0, 0);", Array.Empty<object>());
            }
            catch(Exception ex)
            {
                prcRslt = false;
                msg = "고유번호 입력 오류-" + ex.Message;
                RgstAutoErr(tid, pin, msg);
            }
            finally
            {
                drv.SwitchTo().Window(currentWindowHandle);
            }
            if (!prcRslt) return;

            Thread.Sleep(2000);
            try
            {
                drv.SwitchTo().Frame("resultFrame");
                drv.SwitchTo().Frame("frmOuterModal");
                drv.FindElement(By.CssSelector("td.noline_rt-tx_ct > button")).Click();
            }
            catch (Exception ex)
            {
                prcRslt = false;
                msg = "고유번호 선택 오류-" + ex.Message;
                RgstAutoErr(tid, pin, msg);
            }
            finally
            {
                drv.SwitchTo().Window(currentWindowHandle);
            }
            if (!prcRslt) return;

            Thread.Sleep(1000);
            try
            {
                drv.SwitchTo().Frame("resultFrame");
                drv.SwitchTo().Frame("frmOuterModal");
                html = drv.PageSource;
                if (html.Contains("과다등기부"))
                {
                    throw new Exception("과다등기");
                }
            }
            catch (Exception ex)
            {
                prcRslt = false;
                RgstAutoErr(tid, pin, ex.Message);
                try
                {
                    drv.SwitchTo().Alert().Accept();
                }
                catch { }
            }
            finally
            {
                drv.SwitchTo().Window(currentWindowHandle);
            }
            if (!prcRslt) return;

            Thread.Sleep(1000);
            try
            {
                drv.SwitchTo().Frame("resultFrame");
                drv.SwitchTo().Frame("frmOuterModal");
                this.js.ExecuteScript("javascript:return f_continue()", Array.Empty<object>());
            }
            catch (Exception ex)
            {
                prcRslt = false;
                msg = "등기기록 유형 선택 오류-" + ex.Message;
                RgstAutoErr(tid, pin, msg);
            }
            finally
            {
                drv.SwitchTo().Window(currentWindowHandle);
            }
            if (!prcRslt) return;

            Thread.Sleep(2000);
            try
            {
                drv.SwitchTo().Frame("resultFrame");
                drv.SwitchTo().Frame("frmOuterModal");
                html = drv.PageSource;
                if (html.Contains("신청사건 처리중인 등기부"))
                {
                    //조대로 상태 변경
                    sql = "update db_tank.tx_rgst_mdfy set enable=0 where idx=" + idx;
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();

                    throw new Exception("처리중인 사건");
                }
                else if (html.Contains("이미 결제"))
                {
                    throw new Exception("결제한 사건");
                }

                IReadOnlyCollection<IWebElement> cmorts = drv.FindElements(By.Name("y202cmort_flag"));
                int cmortsCnt = cmorts.Count();
                if (cmortsCnt > 0)
                {
                    foreach (IWebElement cmort in cmorts)
                    {
                        cmort.Click();
                        Thread.Sleep(1000);
                        try
                        {                            
                            if (drv.SwitchTo().Alert().Text.Contains("100매"))
                            {
                                drv.SwitchTo().Alert().Accept();
                                cmortOver = true;
                                break;
                            }
                            drv.SwitchTo().Alert().Accept();
                        }
                        catch { }
                        lsNo++;
                    }
                }

                lsNo = 0;
                IReadOnlyCollection<IWebElement> trades = drv.FindElements(By.Name("y202trade_seq_flag"));
                int tradesCnt=trades.Count();
                if (tradesCnt > 0)
                {
                    foreach (IWebElement trade in trades)
                    {
                        trade.Click();
                        Thread.Sleep(1000);
                        try
                        {
                            if (drv.SwitchTo().Alert().Text.Contains("100매"))
                            {
                                drv.SwitchTo().Alert().Accept();
                                tradeOver = true;
                                break;
                            }
                            drv.SwitchTo().Alert().Accept();
                        }
                        catch { }
                        lsNo++;
                    }
                }

                //[공동담보] 또는 [매매목록]이 100매 이상일 경우 각각 첫번째 목록만 체크
                if (cmortOver || tradeOver)
                {
                    if (cmortOver)
                    {
                        foreach (IWebElement cmort in cmorts)
                        {
                            if (cmort.Selected == false) continue;
                            cmort.Click();
                            Thread.Sleep(1000);
                            try
                            {
                                drv.SwitchTo().Alert().Accept();
                            }
                            catch { }
                        }
                        cmorts.ElementAt(0).Click();
                        try
                        {
                            drv.SwitchTo().Alert().Accept();
                        }
                        catch { }
                    }
                    if (tradeOver)
                    {
                        foreach (IWebElement trade in trades)
                        {
                            if (trade.Selected == false) continue;
                            trade.Click();
                            Thread.Sleep(1000);
                            try
                            {
                                drv.SwitchTo().Alert().Accept();
                            }
                            catch { }
                        }
                        trades.ElementAt(0).Click();
                        try
                        {
                            drv.SwitchTo().Alert().Accept();
                        }
                        catch { }
                    }
                }

                drv.FindElement(By.CssSelector("button.btn_bg02_action")).Click();
            }
            catch (Exception ex)
            {
                prcRslt = false;
                RgstAutoErr(tid, pin, ex.Message);
                try
                {
                    drv.SwitchTo().Alert().Accept();
                }
                catch { }
            }
            finally
            {
                drv.SwitchTo().Window(currentWindowHandle);
            }
            if (!prcRslt) return;

            Thread.Sleep(2000);
            try
            { 
                drv.SwitchTo().Frame("resultFrame");
                drv.FindElement(By.CssSelector("button.btn1_up_bg02_action")).Click();
            }
            catch (Exception ex)
            {
                prcRslt = false;
                msg = "결제대상 부동산 오류-" + ex.Message;
                RgstAutoErr(tid, pin, msg);
            }
            finally
            {
                drv.SwitchTo().Window(currentWindowHandle);
            }
            if (!prcRslt) return;

            //결제
            if (rowNo % 10 == 0 || rowNo == rowCnt)
            {
                Thread.Sleep(3000);
                try
                {
                    drv.FindElement(By.Id("inpMtdCls3")).Click();
                    this.js.ExecuteScript("javascript:$('#inpEMoneyNo1').val('" + irosEMoneyNo1 + "');", Array.Empty<object>());
                    this.js.ExecuteScript("javascript:$('#inpEMoneyNo2').val('" + irosEMoneyNo2 + "');", Array.Empty<object>());
                    this.js.ExecuteScript("javascript:$('#inpEMoneyPswd').val('" + irosEMoneyPwd + "');", Array.Empty<object>());
                    Thread.Sleep(1000);
                    if (drv.FindElement(By.Id("chk_term_agree_all_emoney")).Selected == false)
                    {
                        drv.FindElement(By.Id("chk_term_agree_all_emoney")).Click();
                    }                    
                    drv.FindElement(By.Name("inpComplete")).Click();
                    try
                    {
                        drv.SwitchTo().Alert().Accept();
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    prcRslt = false;
                    msg = "결제선택 입력/동의 오류-" + ex.Message;
                    RgstAutoErr(tid, pin, msg);
                }
                if (!prcRslt) return;

                Thread.Sleep(3500);
                try
                {
                    drv.SwitchTo().Window(drv.WindowHandles.Last());
                    html = drv.PageSource;
                    if (html.Contains("이미 결제"))
                    {
                        //중복결제건
                        throw new Exception("중복 결제");
                    }
                    else if (html.Contains("잔액이 부족"))
                    {
                        //전자민원캐시의 잔액이 부족합니다
                        cashBalance = false;
                        throw new Exception("잔액 부족");
                    }
                    else
                    {
                        drv.FindElement(By.CssSelector("button.btn_bg02_action")).Click();
                    }
                }
                catch (Exception ex)
                {
                    prcRslt = false;
                    msg = "결제성공 확인 오류-" + ex.Message;
                    RgstAutoErr(tid, pin, msg);
                }
                finally
                {
                    drv.SwitchTo().Window(currentWindowHandle);
                }
                Thread.Sleep(3000);
            }
        }

        /// <summary>
        /// 등기 발급-1 (미열람/미발급)
        /// </summary>
        private void RgstIssue()
        {
            int i = 0, rowCnt = 0;
            string currentWindowHandle = "";

            idrvSvc = InternetExplorerDriverService.CreateDefaultService(@"C:\Atom", "IEDriverServer.exe");
            idrvSvc.HideCommandPromptWindow = true;
            InternetExplorerOptions ieOpt = new InternetExplorerOptions();

            try
            {
                idrv = new InternetExplorerDriver(idrvSvc, ieOpt);
                this.js = (IJavaScriptExecutor)this.idrv;
                idrv.Navigate().GoToUrl("http://www.iros.go.kr");
                currentWindowHandle = idrv.CurrentWindowHandle;   //메인 윈도우
                Thread.Sleep(5000);

                this.js.ExecuteScript("javascript:$('#id_user_id').val('" + irosId + "');", Array.Empty<object>());
                this.js.ExecuteScript("javascript:$('#password').val('" + irosPwd + "');", Array.Empty<object>());
                idrv.FindElement(By.XPath(@"//*[@id='leftS']/div[2]/form/div[1]/ul/li[4]/a/img")).Click();
                Thread.Sleep(5000);

                //팝업창 닫기
                if (idrv.WindowHandles.Count > 0)
                {
                    foreach (string winNm in idrv.WindowHandles)
                    {
                        idrv.SwitchTo().Window(winNm);
                        if (idrv.Url.Contains("popupid"))
                        {
                            idrv.Close();
                        }
                    }
                    idrv.SwitchTo().Window(currentWindowHandle);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                idrv.Quit();
                return;
            }

            idrv.Navigate().GoToUrl("http://www.iros.go.kr/frontservlet?cmd=RISURetrieveUnissuedListC&unvRfrYn=Y");            
            Thread.Sleep(5000);

            /*
            IReadOnlyCollection<IWebElement> ecTr = idrv.FindElements(By.XPath(@"//*[@id='Lcontent']/form[1]/div[5]/table/tbody/tr"));
            if (ecTr == null)
            {
                idrv.Quit();
                //MessageBox.Show("발급 종료-1");
                return;
            }

            rowCnt = ecTr.Count();
            if (rowCnt < 2)
            {
                idrv.Quit();
                //MessageBox.Show("발급 종료-2");
                return;
            }
                        
            IWebElement el = idrv.FindElement(By.XPath(@"//*[@id='Lcontent']/form[1]/div[6]/div[1]"));
            rowCnt = Convert.ToInt32(Regex.Replace(el.Text, @"[총건\s]", string.Empty).Trim());
            //MessageBox.Show($"총 {rowCnt}건");            
            if (printTest == false) rowCnt += 1;
            for (i = 0; i < rowCnt; i++)
            {
                RgstPrintPdf(currentWindowHandle);
            }
            */

            while (true)
            {
                if (idrv == null) break;                
                try
                {
                    if (idrv.PageSource.Contains("열람/발급가능한 부동산이 존재하지 않습니다")) break;
                    IReadOnlyCollection<IWebElement> ecTr = idrv.FindElements(By.XPath(@"//*[@id='Lcontent']/form[1]/div[5]/table/tbody/tr"));
                    if (ecTr == null) break;
                    if (ecTr.Count() < 2) break;
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("삭제된 개체"))
                    {
                        //
                        break;
                    }
                }                
                RgstPrintPdf(currentWindowHandle);
            }
            idrv.Quit();
        }

        /// <summary>
        /// 등기 발급-2 (PDF 파일로 저장)
        /// </summary>
        private void RgstPrintPdf(string currentWindowHandle)
        {
            int rowCnt = 0, pdfPrcWait = 0;
            long timeStamp = 0;
            string sql, pin = "";
            IReadOnlyCollection<IWebElement> ecTr = null;

            Thread.Sleep(1500);
            try
            {
                ecTr = idrv.FindElements(By.XPath(@"//*[@id='Lcontent']/form[1]/div[5]/table/tbody/tr"));
                if (ecTr == null)
                {
                    //MessageBox.Show("발급 종료-3");
                    return;
                }

                rowCnt = ecTr.Count();
                if (rowCnt < 2)
                {
                    //MessageBox.Show("발급 종료-4");
                    return;
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
                return;
            }

            IWebElement el = ecTr.ElementAt(1);
            IWebElement elPin = el.FindElements(By.TagName("td"))[4];
            pin = Regex.Replace(elPin.Text, @"[^\d]", string.Empty);
            timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds();     //오류가 났을 경우 파일명

            try
            {
                //이전 인쇄창이 닫히지 않았을 경우
                if (at.WinExists("인쇄") == 1 || at.WinExists("Bullzip") == 1 || at.WinExists("인터넷등기소") == 1)
                {
                    if (at.WinExists("Bullzip") == 1)
                    {
                        at.WinActivate("Bullzip");
                        Thread.Sleep(500);
                        at.ControlSetText("Bullzip", "", "TextBoxU15", $@"{pdfSavePath}\E-{timeStamp}.pdf");
                        Thread.Sleep(1000);
                        at.ControlClick("Bullzip", "", "CommandButtonU2");
                        Thread.Sleep(2000);
                    }
                    else
                    {
                        if (at.WinExists("인쇄") == 1)
                        {
                            at.WinActivate("인쇄");
                            pdfPrcWait = Convert.ToInt32((Convert.ToDouble(Regex.Match(at.WinGetText("인쇄"), @"(\d+)\s+인쇄 매수", RegexOptions.Multiline).Groups[1].Value) * 0.5 + 60));   // (총매수 * 0.5 + 60)초   //Bullzip
                            if (pdfPrcWait == 0) pdfPrcWait = 60;   //Bullzip
                            at.ControlClick("인쇄", "", "Button10");

                            if (at.WinExists("Bullzip") != 1) at.WinWaitActive("Bullzip", "", pdfPrcWait);
                            at.WinActivate("Bullzip");
                            Thread.Sleep(500);
                            at.ControlSetText("Bullzip", "", "TextBoxU15", $@"{pdfSavePath}\E-{timeStamp}.pdf");
                            Thread.Sleep(1000);
                            at.ControlClick("Bullzip", "", "CommandButtonU2");
                            Thread.Sleep(2000);
                        }
                    }
                    //throw new Exception("이전 인쇄창 열림"); -> 예외로 처리할 경우 2개의 파일이 누락된다.(이전거+현재 클릭한거)

                    if (at.WinExists("등기사항증명서") == 1) at.WinClose("등기사항증명서");
                    if (at.WinExists("인쇄") == 1) at.WinClose("인쇄");
                    if (at.WinExists("Bullzip") == 1) at.WinClose("Bullzip");
                    if (at.WinExists("인터넷등기소") == 1) at.WinClose("인터넷등기소");
                    Thread.Sleep(5000);
                }

                //el.FindElement(By.Name("chkSummary")).Click();
                ClickElementSafe(idrv, el.FindElement(By.Name("chkSummary")), 120);
                Thread.Sleep(1500);
                //el.FindElement(By.CssSelector("td:nth-child(11) > button")).Click();
                ClickElementSafe(idrv, el.FindElement(By.CssSelector("td:nth-child(11) > button")), 120);

                if (printTest == false) Thread.Sleep(2000);
                if (at.WinExists("테스트열람") == 1)
                {
                    if (printTest == true)
                    {
                        at.WinClose("테스트열람");
                        return;
                    }
                    at.WinActivate("테스트열람");
                    //idrv.SwitchTo().Window(idrv.WindowHandles.Last());
                    if (idrv.WindowHandles.Count > 0)
                    {
                        foreach (string winNm in idrv.WindowHandles)
                        {
                            idrv.SwitchTo().Window(winNm);
                            if (idrv.Title.Contains("테스트열람"))
                            {
                                Thread.Sleep(1000);
                                //this.js.ExecuteScript("javascript:f_goTestView(); return false;", Array.Empty<object>());
                                idrv.FindElement(By.XPath(@"//*[@id='content1']/div[2]/div/div/a/strong")).Click();
                                Thread.Sleep(3000);

                                if (at.WinExists("인터넷등기소") != 1) at.WinWaitActive("인터넷등기소", "", 30);
                                at.WinActivate("인터넷등기소");
                                at.ControlClick("인터넷등기소", "", "Button1");
                                printTest = true;
                                Thread.Sleep(3000);
                            }
                        }
                        //idrv.SwitchTo().Window(currentWindowHandle);
                    }
                    idrv.SwitchTo().Window(currentWindowHandle);                    
                    //at.WinActive("인터넷등기소");
                    return;
                }

                if (at.WinExists("등기사항증명서") != 1) at.WinWaitActive("등기사항증명서", "", 30);
                at.WinActivate("등기사항증명서");
                //Thread.Sleep(2000);
                at.ControlClick("등기사항증명서", "", "Button5");
                                
                if (at.WinExists("인쇄") != 1) at.WinWaitActive("인쇄", "", 10);
                at.WinActivate("인쇄");
                //pdfPrcWait = Convert.ToInt32((Convert.ToDouble(Regex.Match(at.WinGetText("인쇄"), @"(\d+)\s+인쇄 매수", RegexOptions.Multiline).Groups[1].Value) * 0.5 * 1000));   // (총매수 * 0.5 * 1)초  pdfFactory
                //if (pdfPrcWait == 0) pdfPrcWait = 30000; //pdfFactory

                pdfPrcWait = Convert.ToInt32((Convert.ToDouble(Regex.Match(at.WinGetText("인쇄"), @"(\d+)\s+인쇄 매수", RegexOptions.Multiline).Groups[1].Value) * 0.5 + 60));   // (총매수 * 0.5 + 60)초   //Bullzip
                if (pdfPrcWait == 0) pdfPrcWait = 60;   //Bullzip
                at.ControlClick("인쇄", "", "Button10");

                //Bullzip
                if (at.WinExists("Bullzip") != 1) at.WinWaitActive("Bullzip", "", pdfPrcWait);
                at.WinActivate("Bullzip");
                Thread.Sleep(500);
                at.ControlSetText("Bullzip", "", "TextBoxU15", $@"{pdfSavePath}\{pin}.pdf");
                Thread.Sleep(1000);
                at.ControlClick("Bullzip", "", "CommandButtonU2");
                Thread.Sleep(2000);

                if (at.WinExists("인터넷등기소") != 1) at.WinWaitActive("인터넷등기소", "", 40);
                at.WinActivate("인터넷등기소");
                at.ControlClick("인터넷등기소", "", "Button1");
                //Thread.Sleep(pdfPrcWait);     //pdfFactory

                at.WinActivate("등기사항증명서");
                at.ControlClick("등기사항증명서", "", "Button11");
            }
            catch (Exception ex)
            {
                RgstAutoErr(string.Empty, pin, ex.Message);

                //재시도-click timed out after 60 seconds
                if (at.WinExists("등기사항증명서") != 1) at.WinWaitActive("등기사항증명서", "", 30);
                at.WinActivate("등기사항증명서");                
                at.ControlClick("등기사항증명서", "", "Button5");

                if (at.WinExists("인쇄") != 1) at.WinWaitActive("인쇄", "", 10);
                at.WinActivate("인쇄");

                pdfPrcWait = Convert.ToInt32((Convert.ToDouble(Regex.Match(at.WinGetText("인쇄"), @"(\d+)\s+인쇄 매수", RegexOptions.Multiline).Groups[1].Value) * 0.5 + 60));   // (총매수 * 0.5 + 60)초   //Bullzip
                if (pdfPrcWait == 0) pdfPrcWait = 60;   //Bullzip
                at.ControlClick("인쇄", "", "Button10");

                //Bullzip
                if (at.WinExists("Bullzip") != 1) at.WinWaitActive("Bullzip", "", pdfPrcWait);
                at.WinActivate("Bullzip");
                Thread.Sleep(500);
                at.ControlSetText("Bullzip", "", "TextBoxU15", $@"{pdfSavePath}\{pin}.pdf");
                Thread.Sleep(1000);
                at.ControlClick("Bullzip", "", "CommandButtonU2");
                Thread.Sleep(2000);

                if (at.WinExists("인터넷등기소") != 1) at.WinWaitActive("인터넷등기소", "", 40);
                at.WinActivate("인터넷등기소");
                at.ControlClick("인터넷등기소", "", "Button1");                

                at.WinActivate("등기사항증명서");
                at.ControlClick("등기사항증명서", "", "Button11");

                //오류시 열린창 모두 닫고 재발급 시작
                idrv.Quit();
                if (at.WinExists("테스트열람") == 1) at.WinClose("테스트열람");
                if (at.WinExists("등기사항증명서") == 1) at.WinClose("등기사항증명서");
                if (at.WinExists("인쇄") == 1) at.WinClose("인쇄");
                if (at.WinExists("Bullzip") == 1) at.WinClose("Bullzip");
                if (at.WinExists("인터넷등기소") == 1) at.WinClose("인터넷등기소");
                Thread.Sleep(5000);

                RgstIssue();
            }
        }

        /// <summary>
        /// 안전한 클릭-click timed out after 60 seconds 오류 방어
        /// </summary>
        /// <param name="element"></param>
        /// <param name="driver"></param>
        /// <param name="timeout"></param>
        private void ClickElementSafe(IWebDriver driver, IWebElement element, int timeout)
        {
            // wait for it to be clickable
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeout));
            wait.Until(ExpectedConditions.ElementToBeClickable(element));

            // click it
            element.Click();
        }

        /// <summary>
        /// 해당 엘리먼트가 보일 때 까지 대기(5초)
        /// </summary>
        /// <param name="drv"></param>
        /// <param name="by"></param>
        /// <returns></returns>
        private static bool WaitVisible(IWebDriver drv, By by)
        {
            WebDriverWait wait = new WebDriverWait(drv, TimeSpan.FromSeconds(5));
            try
            {
                //IWebElement element = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementExists(by));
                IWebElement element = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(by));                
            }
            catch
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 자동화 처리 오류
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="pin"></param>
        /// <param name="msg"></param>
        private void RgstAutoErr(string tid, string pin, string msg)
        {
            string sql;

            sql = $"insert into db_tank.tx_rgst_auto set dvsn=1, tid='{tid}', pin='{pin}', msg='{msg}', wdtm=now()";
            db.Open();
            db.ExeQry(sql);
            db.Close();
        }

        private void Bgwork_RunWorkerCompletedIssue(object sender, RunWorkerCompletedEventArgs e)
        {
            MessageBox.Show("처리 완료");
        }
    }
}
