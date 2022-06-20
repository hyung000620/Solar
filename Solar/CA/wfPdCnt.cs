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
using System.Xml;
using System.Threading;

namespace Solar.CA
{
    public partial class wfPdCnt : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        AuctCd auctCd = new AuctCd();
        NetUtil net = new NetUtil();

        string myWeb = Properties.Settings.Default.myWeb;

        BackgroundWorker bgwork;
        ManualResetEvent _busy = new ManualResetEvent(true);  //bgwork [PAUSE] or [RESUME]

        private delegate void deleGridUpdate();
        DataTable dtR = new DataTable();

        DataSet dsL = new DataSet();
        DataSet dsG = new DataSet();
        DataSet dsR = new DataSet();

        DataTable dtLawCd = new DataTable();

        CheckBox chkAll = new CheckBox();

        int setCnt = 5, setSleep = 1500, webCnt = 0;

        public wfPdCnt()
        {
            InitializeComponent();

            init();
        }

        private void init()
        {
            dgL.CellPainting += (s, e) => 
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

                    chkAll.Size = new Size(nChkBoxWidth, nChkBoxHeight);
                    chkAll.Location = pt;
                    chkAll.CheckedChanged += new EventHandler(dgLChkAll_CheckedChanged);
                    chkAll.Name = "HeaderChkAll";
                    ((DataGridView)s).Controls.Add(chkAll);
                    e.Handled = true;
                }
            };

            ui.DgSetRead(dgL);
            ui.DgSetRead(dgC);
            ui.DgSetRead(dgD);
            dgL.DataSource = auctCd.DtLawInfo();

            DataGridViewCheckBoxColumn chkCol = new DataGridViewCheckBoxColumn();
            chkCol.HeaderText = "";
            chkCol.Width = 30;
            chkCol.Name = "chkAll";
            chkCol.TrueValue = true;
            chkCol.FalseValue = false;
            dgL.Columns.Insert(0, chkCol);
        }

        /// <summary>
        /// 법원 전체 선택/해제
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgLChkAll_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox chk = (CheckBox)sender;
            bool chkState = chk.Checked;
            foreach (DataGridViewRow row in dgL.Rows)
            {
                row.Cells[0].Value = chkState;
            }
            pnlTop.Focus();   //focus를 바꿔주지 않으면 current row 에는 체크유무가 표시 안됨!!!
        }

        /// <summary>
        /// 기간 설정 및 보정
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cbxFTdt_SelectedIndexChanged(object sender, EventArgs e)
        {
            DateTime curDt, bgnDt, endDt;

            string v = cbxFTdt.Text;
            curDt = DateTime.Now;
            switch (v)
            {
                case "오늘":
                    bgnDt = curDt;
                    endDt = curDt;
                    break;
                case "내일":
                    bgnDt = curDt.AddDays(+1);
                    endDt = bgnDt;
                    break;
                case "+7":
                    bgnDt = curDt.AddDays(+7);
                    endDt = bgnDt;
                    break;
                case "~7":
                    bgnDt = curDt.AddDays(+1);
                    endDt = curDt.AddDays(+7);
                    break;
                case "14~":
                    bgnDt = curDt.AddDays(+15);
                    endDt = bgnDt.AddDays(+7);
                    break;
                default:
                    bgnDt = curDt;
                    endDt = curDt;
                    break;
            }

            if (curDt.DayOfWeek == DayOfWeek.Friday)
            {
                if (v == "내일")
                {
                    bgnDt = curDt.AddDays(+3);
                    endDt = bgnDt;
                }
                else if (v == "~7")
                {
                    bgnDt = curDt.AddDays(+3);
                    endDt = curDt.AddDays(+10);
                }
                else if (v == "14~")
                {
                    bgnDt = curDt.AddDays(+17);
                    endDt = bgnDt.AddDays(+7);
                }
            }
            else if (curDt.DayOfWeek == DayOfWeek.Saturday)
            {
                if (v == "오늘")
                {
                    bgnDt = curDt.AddDays(+2);
                    endDt = bgnDt;
                }
                else if (v == "내일")
                {
                    bgnDt = curDt.AddDays(+3);
                    endDt = bgnDt;
                }
                else if (v == "+7")
                {
                    bgnDt = curDt.AddDays(+9);
                    endDt = bgnDt;
                }
                else if (v == "~7")
                {
                    bgnDt = curDt.AddDays(+2);
                    endDt = curDt.AddDays(+9);
                }
                else if (v == "14~")
                {
                    bgnDt = curDt.AddDays(+16);
                    endDt = bgnDt.AddDays(+7);
                }
            }

            dtpBgn.Value = bgnDt;
            dtpEnd.Value = endDt;
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            int i = 0, no = 0;
            string sql = "", bgnDt = "", endDt = "", law = "", spt = "", cdtn = "", prvGdt = "";

            webCnt = 0;

            dsL.Tables.Clear();
            dsG.Tables.Clear();
            dsR.Tables.Clear();

            dgC.Rows.Clear();
            dgD.Rows.Clear();
            dgL.ClearSelection();

            var chkRows = from DataGridViewRow row in dgL.Rows
                          where Convert.ToBoolean(row.Cells[0].Value) == true
                          select row;

            if (chkRows.Count() == 0)
            {
                DialogResult dial = MessageBox.Show("선택한 법원이 없습니다.\n\n[전체법원]을 선택 하시겠습니까?", "법원선택", MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
                if (dial == DialogResult.Cancel) return;

                chkAll.Checked = true;
            }

            dtLawCd = db.ExeDt("select * from ta_cd_dpt where 1");

            ArrayList cdtnArr = new ArrayList();

            bgnDt = dtpBgn.Value.ToShortDateString();
            endDt = dtpEnd.Value.ToShortDateString();

            chkRows = from DataGridViewRow row in dgL.Rows
                      where Convert.ToBoolean(row.Cells[0].Value) == true
                      select row;

            if (chkRows.Count() == dgL.RowCount) cdtn = "1";
            else
            {
                foreach (DataGridViewRow x in chkRows)
                {                    
                    spt = x.Cells["csCd"].Value.ToString();
                    cdtnArr.Add(string.Format("(spt={0})", spt));
                }
                cdtn = "(" + string.Join(" OR ", cdtnArr.ToArray()) + ")";
            }
            cdtn += string.Format(" AND bid_dt BETWEEN '{0}' AND '{1}' order by bid_dt, spt, dpt", bgnDt, endDt);
            sql = "select spt, dpt, bid_dt from ta_skd where " + cdtn;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                if (dr["bid_dt"].ToString() != prvGdt) no = 0;

                no++;
                DataRow x = getLawCd(dr["spt"].ToString(), dr["dpt"].ToString());

                i = dgC.Rows.Add();
                dgC["dgC_No", i].Value = no;
                dgC["dgC_Law", i].Value = x["ca_nm"];
                dgC["dgC_Dpt", i].Value = dr["dpt"];
                dgC["dgC_Tdt", i].Value = string.Format("{0:yyyy.MM.dd}", Convert.ToDateTime(dr["bid_dt"]));
                dgC["dgC_Tcd", i].Value = x["cs_cd"];
                dgC["dgC_Pcd", i].Value = x["dpt_cd"];
                prvGdt = dr["bid_dt"].ToString();
            }
            dr.Close();
            db.Close();

            dgC.ClearSelection();

            CheckForIllegalCrossThreadCalls = false;
            bgwork = new BackgroundWorker();
            bgwork.DoWork += bgwork_DoWork;
            bgwork.RunWorkerCompleted += bgwork_RunWorkerCompleted;
            bgwork.WorkerReportsProgress = false;
            bgwork.WorkerSupportsCancellation = true;

            bgwork.RunWorkerAsync();
        }

        /// <summary>
        /// 법원명/코드 반환
        /// </summary>
        /// <param name="law"></param>
        /// <param name="spt"></param>
        /// <param name="lsn"></param>
        /// <returns></returns>
        private DataRow getLawCd(string spt, string dpt)
        {
            var x = from DataRow row in dtLawCd.Rows
                    where row["cs_cd"].ToString() == spt && row["dpt_cd"].ToString() == dpt
                    select row;

            if (x.CopyToDataTable().Rows.Count == 0)
            {
                MessageBox.Show(string.Format("해당 법원코드가 없습니다-{0}/{1}", spt, dpt));
            }

            return x.CopyToDataTable().Rows[0];
        }

        private void bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            Color clr = Color.Lavender;
            Color clr0 = Color.White;
            Color clr1 = Color.Lavender;

            Font font = new Font("Tahoma", 9, FontStyle.Bold);

            bool ftDtCmp = (dtpBgn.Value == dtpEnd.Value) ? true : false;

            foreach (DataGridViewRow row in dgC.Rows)
            {
                try
                {
                    _busy.WaitOne();
                    if (bgwork.CancellationPending == true)
                    {
                        e.Cancel = true;
                        return;
                    }

                    if (!row.Displayed) dgC.FirstDisplayedScrollingRowIndex = row.Index;

                    getLawCnt(row, e);    //법원 물건
                    getTkCnt(row, e);     //탱크 물건

                    if (!ftDtCmp)
                    {
                        if (row.Cells[0].Value.ToString() == "1")
                        {
                            clr = (clr == clr1) ? clr0 : clr1;
                            row.Cells[0].Style.Font = font;
                            row.Cells[0].Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
                            row.Cells[3].Style.Font = font;
                        }
                        row.Cells[0].Style.BackColor = clr;
                    }
                }
                catch
                {
                    row.DefaultCellStyle.BackColor = Color.LightGray;
                }
            }
        }

        private void getTkCnt(DataGridViewRow row, DoWorkEventArgs e)
        {
            int nxL = 0, nxG = 0;
            string sql = "", state = "", law = "", spt = "", dpt = "", tdt = "", tbl = "";

            state = (rdoPs.Checked) ? "sta1=11" : "1";

            spt = row.Cells["dgC_Tcd"].Value.ToString();
            dpt = row.Cells["dgC_Dpt"].Value.ToString();
            tdt = row.Cells["dgC_Tdt"].Value.ToString().Replace(".", "-");

            tbl = string.Format("tbl_{0}{1}{2}", spt, dpt, tdt.Replace("-", ""));

            //DataTable dt = new DataTable(tbl);            
            sql = "select sn1, sn2, if(pn=0,1,pn) as pnum, tid from ta_list where " + state + " and spt=" + spt + " and dpt=" + dpt + " and bid_dt='" + tdt + "' order by sn1, sn2, pn";
            DataTable dt = db.ExeDt(sql);
            dt.TableName = tbl;
            dsG.Tables.Add(dt);

            row.Cells["dgC_TkCnt"].Value = dt.Rows.Count;

            DataTable dtL = dsL.Tables[tbl];
            DataTable dtG = dsG.Tables[tbl];

            DataTable dtR = new DataTable(string.Format(tbl));
            dtR.Columns.Add("sn1");
            dtR.Columns.Add("sn2");
            dtR.Columns.Add("pnum");
            dtR.Columns.Add("tid");
            dtR.Columns.Add("flag");

            //TK -> 법원 : 법원에 없는 사건들
            foreach (DataRow x in dtG.Rows)
            {
                var r = from DataRow y in dtL.Rows
                        where x["sn1"].ToString() == y["sn1"].ToString() && x["sn2"].ToString() == y["sn2"].ToString() && x["pnum"].ToString() == y["pnum"].ToString()
                        select row;

                if (r.Count() == 0)
                {
                    nxL++;
                    dtR.Rows.Add(x["sn1"], x["sn2"], x["pnum"], x["tid"], 1);
                }
            }

            //법원 -> TK : TK에 없는 사건들
            foreach (DataRow x in dtL.Rows)
            {
                var r = from DataRow y in dtG.Rows
                        where x["sn1"].ToString() == y["sn1"].ToString() && x["sn2"].ToString() == y["sn2"].ToString() && x["pnum"].ToString() == y["pnum"].ToString()
                        select row;

                if (r.Count() == 0)
                {
                    nxG++;
                    dtR.Rows.Add(x["sn1"], x["sn2"], x["pnum"], string.Empty, 2);
                }
            }

            dsR.Tables.Add(dtR);
            row.Cells["dgC_LawNxCnt"].Value = nxL;
            row.Cells["dgC_TkNxCnt"].Value = nxG;
            if (nxL > 0 || nxG > 0)
            {
                row.DefaultCellStyle.BackColor = Color.LightGreen;
            }
        }

        private void getLawCnt(DataGridViewRow row, DoWorkEventArgs e)
        {
            int pgNo = 0, pgCnt = 0, targetRow = 0, n = 0;
            string SQL = "", state = "", law = "", spt = "", dpt = "", tdt = "", num1 = "", num2 = "", pnum = "", prvNo = "";
            string url = "", urlPs = "", urlNt = "", html = "", jiwonNm = "", maeGiil = "", jpDeptCd = "";

            //law = row.Cells["dgC_Gcd"].Value.ToString().Substring(0, 2);
            spt = row.Cells["dgC_Tcd"].Value.ToString();
            dpt = row.Cells["dgC_Dpt"].Value.ToString();
            tdt = row.Cells["dgC_Tdt"].Value.ToString().Replace(".", "-");

            DataTable dt = new DataTable(string.Format("tbl_{0}{1}{2}", spt, dpt, tdt.Replace("-", "")));
            dt.Columns.Add("sn1");
            dt.Columns.Add("sn2");
            dt.Columns.Add("pnum");

            urlNt = "http://www.courtauction.go.kr/RetrieveRealEstMgakNotifySrchGyulgwa.laf?ipchalGbnCd=000331&";
            urlPs = "http://www.courtauction.go.kr/RetrieveRealEstMulDetailList.laf?page=default40&ipchalGbncd=000331&srnID=PNO102005&";

            jiwonNm = System.Web.HttpUtility.UrlEncode(row.Cells["dgC_Law"].Value.ToString(), Encoding.Default);
            maeGiil = row.Cells["dgC_Tdt"].Value.ToString().Replace(".", string.Empty);
            jpDeptCd = row.Cells["dgC_Pcd"].Value.ToString();

            webCnt++;
            _busy.WaitOne();
            if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

            if (rdoPs.Checked)
            {
                url = urlPs + "jiwonNm=" + jiwonNm + "&jpDeptCd=" + jpDeptCd + "&maeGiil=" + maeGiil;
                html = net.GetHtml(url);
                html = Regex.Match(html, "<div class=\"page2\">.*</div>", RegexOptions.Multiline | RegexOptions.IgnoreCase).Value;
                if (html != string.Empty)
                {
                    //MatchCollection mc = Regex.Matches(html, @"<span[\s\w=_""]*>[\s]*(\d+)[\s]*</span>", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                    //pgCnt = Convert.ToInt16(mc[mc.Count - 1].Groups[1].Value);  //-> 로직 오류
                    MatchCollection mc = Regex.Matches(html, @"goPage\('(\d+)'\)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                    if (mc.Count == 0) pgCnt = 1;
                    else
                    {
                        pgCnt = (int)Math.Ceiling(Convert.ToDecimal(mc[mc.Count - 1].Groups[1].Value) / 40);
                    }
                    //MessageBox.Show(pgCnt.ToString());
                    for (pgNo = 1; pgNo <= pgCnt; pgNo++)
                    {
                        webCnt++;
                        _busy.WaitOne();
                        if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                        targetRow = (pgNo - 1) * 40 + 1;
                        html = net.GetHtml(url + "&targetRow=" + targetRow);
                        MatchCollection mcTr = Regex.Matches(html, @"<tr class=""Ltbl_list_lvl[01]"">.*?</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        foreach (Match tr in mcTr)
                        {
                            MatchCollection mcTd = Regex.Matches(tr.Value, @"<td[\s\w\d=_""]*>.*?</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                            Match m = Regex.Match(mcTd[1].Value, @"<b>(\d+)타경(\d+)</b>", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                            num1 = m.Groups[1].Value;
                            num2 = m.Groups[2].Value;
                            pnum = Regex.Match(mcTd[2].Value, @"\d+", RegexOptions.Multiline).Value;
                            if (prvNo == (num1 + num2 + pnum)) continue;
                            prvNo = num1 + num2 + pnum;
                            dt.Rows.Add(num1, num2, pnum);
                        }
                    }
                }
            }
            else
            {
                url = urlNt + "jiwonNm=" + jiwonNm + "&jpDeptCd=" + jpDeptCd + "&maeGiil=" + maeGiil;
                html = net.GetHtml(url);
                html = Regex.Match(html, @"<table class=""Ltbl_list"" summary=""원공고내역 표"">.*?</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase).Value;
                MatchCollection mcTr = Regex.Matches(html, @"<tr class=""Ltbl_list_lvl[01]"">.*?</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                foreach (Match tr in mcTr)
                {
                    pnum = Regex.Match(tr.Value, @">(\d+)</td>", RegexOptions.Multiline | RegexOptions.IgnoreCase).Groups[1].Value;
                    if (pnum == string.Empty) continue;     //물건번호가 없으면 패스(셀병합)
                    Match m = Regex.Match(tr.Value, @"<b>(\d+)타경(\d+)</b>", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        num1 = m.Groups[1].Value;
                        num2 = m.Groups[2].Value;
                    }
                    dt.Rows.Add(num1, num2, pnum);
                }
            }

            dsL.Tables.Add(dt);
            row.Cells["dgC_LawCnt"].Value = dt.Rows.Count;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            if (bgwork == null || !bgwork.IsBusy) return;

            _busy.Reset();

            if (MessageBox.Show("작업을 취소 하시겠습니까?", "작업취소", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                _busy.Set();
                bgwork.CancelAsync();
            }
            else
            {
                _busy.Set();
            }
        }

        private void dgC_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int i = 0;
            string state = "", law = "", spt = "", dpt = "", tdt = "", tbl = "", eNo = "";
            string url = "", jiwonNm = "", maeGiil = "", jpDeptCd = "", jpDeptNm = "";

            dgD.Rows.Clear();

            DataGridViewRow row = dgC.Rows[e.RowIndex];

            //해당공고 web
            jiwonNm = System.Web.HttpUtility.UrlEncode(row.Cells["dgC_Law"].Value.ToString(), Encoding.Default);
            maeGiil = row.Cells["dgC_Tdt"].Value.ToString().Replace(".", string.Empty);
            jpDeptCd = row.Cells["dgC_Pcd"].Value.ToString();
            jpDeptNm = string.Format("경매{0}계", row.Cells["dgC_Dpt"].Value);
            url = "http://www.courtauction.go.kr/RetrieveRealEstMgakNotifySrchGyulgwa.laf?ipchalGbnCd=000331&jiwonNm=" + jiwonNm + "&maeGiil=" + maeGiil + "&jpDeptCd=" + jpDeptCd + "&jpDeptNm=" + jpDeptNm;            
            net.Nvgt(wbrNt, url);
            if (row.Cells["dgC_LawNxCnt"].Value == null) return;
            if (row.Cells["dgC_LawNxCnt"].Value.ToString() == "0" && row.Cells["dgC_TkNxCnt"].Value.ToString() == "0")
            {
                MessageBox.Show("해당 물건이 없습니다.");
                return;
            }

            //상호 누락물건 목록
            spt = row.Cells["dgC_Tcd"].Value.ToString();
            dpt = row.Cells["dgC_Dpt"].Value.ToString();
            tdt = row.Cells["dgC_Tdt"].Value.ToString().Replace(".", "-");

            tbl = string.Format("tbl_{0}{1}{2}", spt, dpt, tdt.Replace("-", ""));
            DataTable dtR = dsR.Tables[tbl];
            foreach (DataRow x in dtR.Rows)
            {
                i = dgD.Rows.Add();
                dgD["dgD_No", i].Value = i + 1;
                dgD["dgD_Law", i].Value = row.Cells["dgC_Law"].Value;
                dgD["dgD_ENo", i].Value = string.Format("{0}-{1} ({2})", x["sn1"], x["sn2"], x["pnum"]);
                dgD["dgD_Tid", i].Value = x["tid"];
                dgD["dgD_NxLaw", i].Value = (x["flag"].ToString() == "1") ? "X" : "○";
                dgD["dgD_NxTk", i].Value = (x["flag"].ToString() == "2") ? "X" : "○";
                dgD.Rows[i].DefaultCellStyle.BackColor = (x["flag"].ToString() == "2") ? Color.Lavender : Color.White;
            }

            dgD.ClearSelection();
        }

        private void dgD_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            string url = "", jiwonNm = "", saNo = "", tid = "";

            int i = e.RowIndex;

            jiwonNm = System.Web.HttpUtility.UrlEncode(dgD["dgD_Law", i].Value.ToString(), Encoding.Default);
            Match match = Regex.Match(dgD["dgD_ENo", i].Value.ToString(), @"(\d+)-(\d+) \((\d+)\)");
            saNo = match.Groups[1].Value + "0130" + match.Groups[2].Value.PadLeft(6, '0');
            tid = dgD["dgD_Tid", i].Value.ToString();
            
            //사건내역
            url = "http://www.courtauction.go.kr/RetrieveRealEstDetailInqSaList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
            net.Nvgt(wbrSagun, url);

            //기일내역
            url = "http://www.courtauction.go.kr/RetrieveRealEstSaDetailInqGiilList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
            net.Nvgt(wbrGiil, url);
        }

        private void bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            bgwork.Dispose();
            dgC.ClearSelection();
            dgD.ClearSelection();

            if (e.Cancelled)
            {
                MessageBox.Show("작업이 취소 되었습니다.");
            }
            else
            {
                MessageBox.Show("작업 완료");
            }
        }
    }
}
