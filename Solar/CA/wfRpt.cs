using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MySql.Data.MySqlClient;
using HtmlAgilityPack;
using hap = HtmlAgilityPack;
using HAPDoc = HtmlAgilityPack.HtmlDocument;

namespace Solar.CA
{
    public partial class wfRpt : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        AuctCd auctCd = new AuctCd();
        NetUtil net = new NetUtil();

        DataTable dtRptDvsn, dtPrcState;  //작업 구분, 처리 상태

        DataTable dtLawCd, dtDptCd; //법원, 계
        DataTable dtCatCdAll, dtCatCd;  //물건 종별
        DataTable dtStateCd;    //진행 상태
        DataTable dtLeasUseCd;  //임차인-용도 코드
        DataTable dtReDpstRate; //법원별 재매각 보증금율

        DataTable dtImptCtgr;   //물건주요변동내역-구분
        DataTable dtImptSrc;    //물건주요변동내역-출처

        Dictionary<decimal, string> dictPriReg = new Dictionary<decimal, string>();     //우선매수 신고
        Dictionary<decimal, string> dictDpstType = new Dictionary<decimal, string>();   //보증금율 구분
        ContextMenuStrip dgMenu = new ContextMenuStrip();

        string myWeb = Properties.Settings.Default.myWeb;

        ImageList imgList = new ImageList();

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 1500, webCnt = 0;

        string STA1 = string.Empty, STA2 = string.Empty, newSTA1 = string.Empty, newSTA2 = string.Empty;  //현재 DB상 및 수정 후 물건상태 코드 비교 값(문자발송용)

        public wfRpt()
        {
            InitializeComponent();

            init();
        }

        private void init()
        {
            ui.DgSetRead(dg, 0);
            ui.DgSetEdit(dgH);
            ui.DgSetRead(dgT);
            ui.DgSetRead(dgImpt, 0);
            dg.MultiSelect = true;

            imgList.ImageSize = new Size(10, 20);
            lvSpc.SmallImageList = imgList;

            //작업 구분
            dtRptDvsn = new DataTable();
            dtRptDvsn.Columns.Add("key");
            dtRptDvsn.Columns.Add("val");
            dtRptDvsn.Rows.Add(0, "-작업 구분-");
            dtRptDvsn.Rows.Add(10, "감정가/최저가/매각결과");
            dtRptDvsn.Rows.Add(11, "물건 상태");
            dtRptDvsn.Rows.Add(12, "공고 상태");
            dtRptDvsn.Rows.Add(13, "문건 키워드");
            dtRptDvsn.Rows.Add(14, "낙찰후처리");
            dtRptDvsn.Rows.Add(15, "입찰 시간");
            dtRptDvsn.Rows.Add(16, "유찰 확인");
            cbxRptDvsn.DataSource = dtRptDvsn;
            cbxRptDvsn.DisplayMember = "val";
            cbxRptDvsn.ValueMember = "key";

            //처리 상태
            dtPrcState = new DataTable();
            dtPrcState.Columns.Add("key");
            dtPrcState.Columns.Add("val");
            dtPrcState.Rows.Add(99, "-처리상태-");
            dtPrcState.Rows.Add(0, "미처리");
            dtPrcState.Rows.Add(1, "자동");
            dtPrcState.Rows.Add(2, "일괄");
            dtPrcState.Rows.Add(3, "개별");
            dtPrcState.Rows.Add(9, "확인대상");
            cbxPrcState.DataSource = dtPrcState;
            cbxPrcState.DisplayMember = "val";
            cbxPrcState.ValueMember = "key";

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

            //법원별 재매각 보증금율
            dtReDpstRate = db.ExeDt("select spt_cd, re_dpst_rate from ta_cd_cs");

            //물건종별 및 토지 지목
            dtCatCdAll = db.ExeDt("select cat1_cd, cat2_cd, cat3_cd, cat2_nm, cat3_nm, hide, bldg_type from ta_cd_cat where cat3_cd > 0 order by cat3_cd");
            var x = from DataRow r in dtCatCdAll.Rows
                    where r["hide"].ToString() == "0"
                    select r;
            dtCatCd = x.CopyToDataTable();

            //진행 상태
            dtStateCd = db.ExeDt("select sta1_cd, sta2_cd, sta1_nm, sta2_nm from ta_cd_sta order by sta2_cd");
            row = dtStateCd.NewRow();
            row["sta1_cd"] = 0;
            row["sta1_nm"] = "-선택-";
            row["sta2_cd"] = 0;
            row["sta2_nm"] = "-선택-";
            dtStateCd.Rows.InsertAt(row, 0);
            cbxState.DataSource = dtStateCd.Copy();
            cbxState.DisplayMember = "sta2_nm";
            cbxState.ValueMember = "sta2_cd";

            //보증금율 구분
            dictDpstType.Add(1, "최저");
            dictDpstType.Add(2, "재입");
            dictDpstType.Add(3, "최매");
            cbxDpstType.DataSource = new BindingSource(dictDpstType, null);
            cbxDpstType.DisplayMember = "Value";
            cbxDpstType.ValueMember = "Key";

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

            //기타 모든 코드
            DataTable dtEtcCd = db.ExeDt("select * from ta_cd_etc order by seq, cd");

            //특수 조건
            DataTable dtSpcCd = dtEtcCd.Select("dvsn=18").CopyToDataTable();
            foreach (DataRow r in dtSpcCd.Rows)
            {
                lvSpc.Items.Add(string.Format("{0}.{1}", r["cd"], r["nm"]));
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

            //우선매수 신고
            dictPriReg.Add(0, "선택");
            dictPriReg.Add(1, "공유자");
            dictPriReg.Add(2, "임차인");
            dgH_PriReg.DataSource = new BindingSource(dictPriReg, null);
            dgH_PriReg.DisplayMember = "Value";
            dgH_PriReg.ValueMember = "Key";
            dgH_PriReg.DefaultCellStyle.NullValue = dictPriReg[0];

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

            //DG ContextMenuStrip
            dgMenu.BackColor = Color.Beige;
            dgMenu.Items.Add("+ 행추가");
            dgMenu.Items.Add("+ 행추가(5)");
            dgMenu.Items.Add("- 행삭제");
            dgMenu.ItemClicked += DgMenu_ItemClicked;

            dgH.MouseUp += Dg_MouseUp;
            dgT.MouseUp += Dg_MouseUp;

            //DG Row 삭제시
            dgH.UserDeletingRow += Dg_UserDeletingRow;
            dgT.UserDeletingRow += Dg_UserDeletingRow;

            //임차인 현황-용도코드 변경시 사업자 체크
            dgT.EditingControlShowing += DgT_EditingControlShowing;

            //입찰일정 금액입력 관련
            dgH.EditingControlShowing += DgH_EditingControlShowing;

            //ComboBox 마우스휠 무력화            
            List<ComboBox> lstCbx = new List<ComboBox>();
            ComboBox[] cbxArr = new ComboBox[] { cbxState, cbxBidCnt };
            lstCbx.AddRange(cbxArr);
            CbxMouseWheelDisable(lstCbx);

            //DataGridViewComboBoxColumn 마우스휠 무력화
            List<DataGridView> lstDgv = new List<DataGridView>();
            lstDgv.Add(dgH);
            lstDgv.Add(dgT);
            DgvCbxMouseWheelDisable(lstDgv);

            btnSaveAll.Click += SaveData;
            //btnSaveLeas.Click += SaveData;
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
            dict.Add("dgT", "ta_leas");

            idxCellNm = dgv.Name + "_Idx";

            //if (dgv.Rows[rowIdx].Cells[0].Value == null) return;
            //idx = dgv.Rows[rowIdx].Cells[0].Value.ToString();
            if (dgv.Rows[rowIdx].Cells[idxCellNm].Value == null) return;

            dbIdx = dgv.Rows[rowIdx].Cells[idxCellNm].Value.ToString();
            //MessageBox.Show(dgv.Name + " -> " + dbIdx);            
            db.Open();
            if (dgv.Name == "dgI")
            {
                MessageBox.Show("목록내역은 삭제 할 수 없습니다.");
                return;
            }
            else
            {
                tbl = dict[dgv.Name];
                sql = "delete from " + tbl + " where idx=" + dbIdx;
                db.ExeQry(sql);
            }
            db.Close();
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
        /// 임차인 현황-용도코드 변경시 사업자 체크 Step 1
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DgT_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (dgT.CurrentCell.ColumnIndex == 5 && e.Control is ComboBox)
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

        private void btnSrch_Click(object sender, EventArgs e)
        {
            int i = 0, srchDvsn = 0;
            string sql, cdtn, csCd, dpt, staT, staC, rptDvsn, rptPrc, srcPrcState;

            //dg.SelectionChanged -= Dg_SelectionChanged;
            dg.CellClick -= Dg_CellClick;
            dg.Rows.Clear();
            dgH.Rows.Clear();

            DataGridViewColumn[] dCols = new DataGridViewColumn[] { dg_No, dg_RptDvsn, dg_Tid, dg_CS, dg_DptNm, dg_SN, dg_Sta, dg_State, dg_RptPrc, dg_BidDt, dg_PrcNote, dg_AplyBtn };
            DataGridViewColumn[] vCols = dCols;

            srchDvsn = Convert.ToInt32(cbxRptDvsn.SelectedValue);
            if (srchDvsn == 10) vCols = new DataGridViewColumn[] { dg_ApslAmt, dg_MinbAmt, dg_SucbAmt };
            else if (srchDvsn == 11) vCols = new DataGridViewColumn[] { dg_ApslAmt, dg_MinbAmt, dg_ClsRslt, dg_ClsDt, dg_Appeal, dg_PdNote, dg_PdState, dg_NxtBidDt };
            else if (srchDvsn == 12 || srchDvsn == 13)
            {
                vCols = new DataGridViewColumn[] { dg_NtDt, dg_NtNote };
                dg_NtDt.HeaderText = (srchDvsn == 12) ? "취/정공고일" : "접수일자";
                dg_NtNote.HeaderText = (srchDvsn == 12) ? "취/정내용" : "검색키워드";
            }
            else if (srchDvsn == 14)
            {
                vCols = new DataGridViewColumn[] { dg_NxtBidDt, dg_MinbAmt, dg_DpstRate, dg_AprvDt, dg_LimitDt, dg_PayDt, dg_ShrDt };
            }
            else if (srchDvsn == 15)
            {
                vCols = new DataGridViewColumn[] { dg_BidTm1, dg_BidTm2 };
            }
            
            foreach (DataGridViewColumn col in dg.Columns)
            {
                if (vCols.Contains(col)) col.Visible = true;
                else
                {
                    col.Visible = dCols.Contains(col) ? true : false;
                }
            }

            List<string> cdtnLst = new List<string>();
            cdtnLst.Add("wdt='" + dtpRptDt.Value.ToShortDateString() + "'");
            if (cbxSrchCs.SelectedIndex > 0) cdtnLst.Add("spt=" + cbxSrchCs.SelectedValue.ToString());
            if (cbxSrchDpt.SelectedIndex > 0) cdtnLst.Add("dpt=" + cbxSrchDpt.SelectedValue.ToString());
            if (dtpBidDtBgn.Checked) cdtnLst.Add("L.bid_dt = '" + dtpBidDtBgn.Value.ToShortDateString() + "'");
            if (chkOnlyCrctNt.Checked) cdtnLst.Add("nt_note='정정공고'");

            if (cbxRptDvsn.SelectedIndex > 0) cdtnLst.Add("dvsn=" + srchDvsn.ToString());
            if (cbxPrcState.SelectedIndex > 0)
            {                
                cdtnLst.Add("prc=" + cbxPrcState.SelectedValue.ToString());
            }
            //cdtnLst.Add("sn2=101560");  //Test
            //cdtnLst.Add("R.tid IN (11018,13937,13742)"); //Test            
            cdtn = string.Join(" and ", cdtnLst.ToArray());
            sql = "select L.tid, L.bid_dt as bidDtT, L.minb_amt as minbAmtT, spt, dpt, sn1, sn2, pn, sta1, sta2, fb_cnt, R.* from db_main.ta_list L , db_tank.tx_rpt R where L.tid=R.tid and " + cdtn + " order by spt, sn1, sn2, pn";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                csCd = dr["spt"].ToString();
                var xDpt = dtDptCd.Rows.Cast<DataRow>().Where(t => t["cs_cd"].ToString() == csCd && t["dpt_cd"].ToString() == dr["dpt"].ToString()).SingleOrDefault();
                dpt = (xDpt == null || dr["dpt"].ToString() == "0") ? string.Empty : xDpt.Field<string>("dpt_nm");

                var xState = dtStateCd.Rows.Cast<DataRow>().Where(t => t["sta2_cd"].ToString() == dr["sta2"].ToString()).SingleOrDefault();
                staT = (xState == null || dr["sta2"].ToString() == "0") ? string.Empty : xState.Field<string>("sta2_nm");

                DataRow xRow = dtRptDvsn.Select("key=" + dr["dvsn"].ToString()).FirstOrDefault();
                rptDvsn = xRow["val"].ToString();

                xRow = dtPrcState.Select("key=" + dr["prc"].ToString()).FirstOrDefault();
                rptPrc = xRow["val"].ToString();

                staC = dr["state"].ToString();

                i = dg.Rows.Add();
                dg["dg_No", i].Value = i + 1;
                dg["dg_RptDvsn", i].Value = rptDvsn;
                dg["dg_RptPrc", i].Value = rptPrc;
                dg["dg_PrcNote", i].Value = dr["prc_note"];
                dg["dg_Tid", i].Value = dr["tid"];
                dg["dg_CS", i].Value = auctCd.FindCsNm(csCd);
                dg["dg_DptNm", i].Value = dpt;

                dg["dg_SN", i].Value = (dr["pn"].ToString() == "0") ? string.Format("{0}-{1}", dr["sn1"], dr["sn2"]) : string.Format("{0}-{1} ({2})", dr["sn1"], dr["sn2"], dr["pn"]);
                dg["dg_BidDt", i].Value = (dr["bidDtT"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["bidDtT"]);
                dg["dg_Sta", i].Value = staT;
                dg["dg_Sta1", i].Value = dr["sta1"];
                dg["dg_Sta2", i].Value = dr["sta2"];
                dg["dg_Spt", i].Value = dr["spt"];
                dg["dg_Dpt", i].Value = dr["dpt"];
                dg["dg_FbCnt", i].Value = dr["fb_cnt"];
                dg["dg_Ridx", i].Value = dr["idx"];

                dg["dg_ApslAmt", i].Value = string.Format("{0:N0}", dr["apsl_amt"]);
                dg["dg_MinbAmt", i].Value = string.Format("{0:N0}", dr["minb_amt"]);
                dg["dg_SucbAmt", i].Value = string.Format("{0:N0}", dr["sucb_amt"]);
                dg["dg_MinbAmtT", i].Value = string.Format("{0:N0}", dr["minbAmtT"]);
                dg["dg_State", i].Value = staC;
                dg["dg_ClsRslt", i].Value = dr["cls_rslt"].ToString();
                dg["dg_ClsDt", i].Value = (dr["cls_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["cls_dt"]);
                dg["dg_Appeal", i].Value = dr["appeal"].ToString();
                dg["dg_PdNote", i].Value = dr["pd_note"].ToString();
                dg["dg_PdState", i].Value = dr["pd_state"].ToString();

                dg["dg_NtDt",i].Value= (dr["nt_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["nt_dt"]);
                dg["dg_NtNote", i].Value = dr["nt_note"];

                dg["dg_NxtBidDt",i].Value= (dr["bid_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["bid_dt"]);
                dg["dg_DpstRate", i].Value = (dr["dpst_rate"].ToString() == "0") ? "" : dr["dpst_rate"];
                dg["dg_AprvDt", i].Value = (dr["aprv_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["aprv_dt"]);
                dg["dg_LimitDt", i].Value = (dr["limit_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["limit_dt"]);
                dg["dg_PayDt", i].Value = (dr["pay_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["pay_dt"]);
                dg["dg_ShrDt", i].Value = (dr["shr_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["shr_dt"]);

                dg["dg_BidTm1", i].Value = dr["bid_tm1"];
                dg["dg_BidTm2", i].Value = dr["bid_tm2"];

                if (rptPrc == "미처리") dg.Rows[i].DefaultCellStyle.BackColor = Color.LightGray;
                else if (rptPrc == "확인대상") dg.Rows[i].DefaultCellStyle.BackColor = Color.LightPink;
                else dg.Rows[i].DefaultCellStyle.BackColor = Color.White;

                //if ((staT == "지급기한" && staC == "기한후납부") || (staT == "지급기한" && staC == "미납(기일)") || (staT == "낙찰" && staC == "불허가") || (staT == "지급기한" && staC == "차순위"))
                if (rptDvsn == "낙찰후처리")
                {
                    if ((staT == "지급기한" && staC == "기한후납부") || (staT == "지급기한" && staC == "미납(기일)") || (staT == "낙찰" && staC == "불허가"))
                    {
                        DataGridViewButtonCell cell = dg["dg_AplyBtn", i] as DataGridViewButtonCell;
                        cell.Style.BackColor = Color.LightSteelBlue;
                    }
                    else
                    {
                        DataGridViewButtonCell cell = dg["dg_AplyBtn", i] as DataGridViewButtonCell;
                        cell.Style = new DataGridViewCellStyle { Padding = new Padding(0, 10, 0, 10) };
                    }
                }
                else if (rptDvsn == "물건 상태")
                {
                    if ((staT == "변경" && (dr["pd_state"].ToString() == "대급납부" || dr["pd_state"].ToString() == "기한후납부")) || (staC == "기한후납부" && (staT == "유찰" || staT == "신건")))
                    {
                        DataGridViewButtonCell cell = dg["dg_AplyBtn", i] as DataGridViewButtonCell;
                        cell.Style.BackColor = Color.Lavender;
                    }
                    else
                    {
                        DataGridViewButtonCell cell = dg["dg_AplyBtn", i] as DataGridViewButtonCell;
                        cell.Style = new DataGridViewCellStyle { Padding = new Padding(0, 10, 0, 10) };
                    }
                }
                else if (rptDvsn == "감정가/최저가/매각결과")
                {
                    if ((dr["apsl_amt"].ToString() != "0" || dr["minb_amt"].ToString() != "0") || (staT == "유찰" && staC == "매각" && dr["sucb_amt"].ToString() != "0"))
                    {
                        DataGridViewButtonCell cell = dg["dg_AplyBtn", i] as DataGridViewButtonCell;
                        cell.Style.BackColor = Color.LightSeaGreen;
                    }
                    else
                    {
                        DataGridViewButtonCell cell = dg["dg_AplyBtn", i] as DataGridViewButtonCell;
                        cell.Style = new DataGridViewCellStyle { Padding = new Padding(0, 10, 0, 10) };
                    }
                }
                else if (rptDvsn == "입찰 시간")
                {
                    DataGridViewButtonCell cell = dg["dg_AplyBtn", i] as DataGridViewButtonCell;
                    cell.Style.BackColor = Color.PeachPuff;
                }
                else
                {
                    DataGridViewButtonCell cell = dg["dg_AplyBtn", i] as DataGridViewButtonCell;
                    cell.Style = new DataGridViewCellStyle { Padding = new Padding(0, 10, 0, 10) };
                }                
            }
            db.Close();
            dg.ClearSelection();

            if (dg.Rows.Count == 0)
            {
                MessageBox.Show("검색된 물건이 없습니다.");
                return;
            }
            else
            {
                //dg.SelectionChanged += Dg_SelectionChanged;
                dg.CellClick += Dg_CellClick;
            }
        }

        /// <summary>
        /// 물건 내용
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Dg_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int i = 0, n = 0;
            string sql = "", tid = "", sn = "", lawNm = "";

            if (dg.Columns[e.ColumnIndex] == dg_AplyBtn)
            {
                DataGridViewButtonCell cell = dg.CurrentCell as DataGridViewButtonCell;
                if (cell.Style.BackColor == Color.Empty) return;
                AplyBtn_Click(e.RowIndex);
                return;
            }

            dgH.CellValueChanged -= Dg_CellValueChanged;
            dgT.CellValueChanged -= Dg_CellValueChanged;
            lvSpc.ItemChecked -= LvSpc_ItemChecked;

            dgH.Rows.Clear();
            dgT.Rows.Clear();
            txtRptIdx.Text = string.Empty;
            cbxImptCtgr.SelectedIndex = 1;
            cbxImptSrc.SelectedIndex = 2;
            txtImptIdx.Text = string.Empty;
            txtImptNote.Text = string.Empty;

            foreach (ListViewItem item in lvSpc.CheckedItems)
            {
                item.Checked = false;
                item.BackColor = Color.White;
            }

            if (dg.CurrentRow == null) return;
            i = dg.CurrentRow.Index;
            tid = dg["dg_Tid", i].Value.ToString();
            sql = "select * from ta_list L , ta_dtl D where L.tid=D.tid and L.tid=" + tid + " limit 1";
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
            lawNm = auctCd.FindLawNm(dr["spt"].ToString());
            sn = (dr["pn"].ToString() == "0") ? string.Format("{0}-{1}", dr["sn1"], dr["sn2"]) : string.Format("{0}-{1} ({2})", dr["sn1"], dr["sn2"], dr["pn"]);
            lblSagunTitle.Text = string.Format("{0} > {1}", lawNm, sn);

            txtSptCd.Text = dr["spt"].ToString();
            txtDptCd.Text = dr["dpt"].ToString();
            txtSn1.Text = dr["sn1"].ToString();
            txtSn2.Text = dr["sn2"].ToString();
            txtPn.Text = dr["pn"].ToString();
            txtFbCnt.Text = dr["fb_cnt"].ToString();

            cbxDpstType.SelectedValue = Convert.ToDecimal(dr["dpst_type"]);
            cbxDpstRate.Text = dr["dpst_rate"].ToString();

            STA1 = dr["sta1"].ToString();
            STA2 = dr["sta2"].ToString();
            cbxState.SelectedValue = dr["sta2"];
            mtxtEndDt.Text = (dr["end_dt"].ToString().Contains("0001")) ? "" : dr["end_dt"].ToString();
            mtxtSucbDt.Text = (dr["sucb_dt"].ToString().Contains("0001")) ? "" : dr["sucb_dt"].ToString();

            txtApslAmt.Text = string.Format("{0:#,##0}", dr["apsl_amt"]);
            txtMinbAmt.Text = string.Format("{0:#,##0}", dr["minb_amt"]);
            txtSucbAmt.Text = string.Format("{0:#,##0}", dr["sucb_amt"]);

            mtxtBidDt.Text = (dr["bid_dt"].ToString().Contains("0001")) ? "" : dr["bid_dt"].ToString();
            cbxBidCnt.Text = dr["bid_cnt"].ToString();
            mtxtBidTm.Text = dr["bid_tm"].ToString();
            mtxtBidTm1.Text = dr["bid_tm1"].ToString();
            mtxtBidTm2.Text = dr["bid_tm2"].ToString();
            mtxtBidTm3.Text = dr["bid_tm3"].ToString();

            txtEtcNote.Text = dr["etc_note"].ToString();
            txtLeasNote.Text = dr["leas_note"].ToString();
            txtAttnNote1.Text = dr["attn_note1"].ToString();
            txtAttnNote2.Text = dr["attn_note2"].ToString();

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

            //임차인 현황
            n = 0;
            sql = "select *, date_format(mv_dt,'%Y-%m-%d') as mvDt, date_format(fx_dt,'%Y-%m-%d') as fxDt, date_format(shr_dt,'%Y-%m-%d') as shrDt from ta_leas where tid=" + tid + " order by ls_no";
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
                dgT["dgT_UseType", n].Value = dr["use_type"];
                ((DataGridViewComboBoxCell)dgT["dgT_UseCd", n]).Value = dr["use_cd"];
                dgT["dgT_Term", n].Value = dr["term"];
                dgT["dgT_ShopNm", n].Value = dr["shop_nm"];
                dgT["dgT_Deposit", n].Value = dr["deposit"];
                dgT["dgT_MMoney", n].Value = dr["m_money"];
                dgT["dgT_TMoney", n].Value = dr["t_money"];
                dgT["dgT_TMnth", n].Value = dr["t_mnth"];
                dgT["dgT_ChkBiz", n].Value = dr["biz"];
                dgT["dgT_Note", n].Value = dr["note"];
                //dgT["dgT_MvDt", n].Value = (dr["mv_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["mv_dt"]);
                //dgT["dgT_FxDt", n].Value = (dr["fx_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["fx_dt"]);
                //dgT["dgT_ShrDt", n].Value = (dr["shr_dt"].ToString().Contains("0001")) ? "" : string.Format("{0:yyyy-MM-dd}", dr["shr_dt"]);
                dgT["dgT_MvDt", n].Value = dr["mvDt"];
                dgT["dgT_FxDt", n].Value = dr["fxDt"];
                dgT["dgT_ShrDt", n].Value = dr["shrDt"];
            }
            dr.Close();
            db.Close();
            dgT.ClearSelection();

            //물건 주요 변동내역
            LoadImptHist(tid);

            txtApslAmt.BackColor = Color.White;
            txtMinbAmt.BackColor = Color.White;

            dgH.CellValueChanged += Dg_CellValueChanged;
            dgT.CellValueChanged += Dg_CellValueChanged;

            lvSpc.ItemChecked += LvSpc_ItemChecked;

            this.Cursor = Cursors.Default;
        }

        /// <summary>
        /// 수정 적용
        /// </summary>
        /// <param name="rowIdx"></param>
        private void AplyBtn_Click(int rowIdx)
        {
            string rptDvsn, tid, staT, staC, apslAmt, minbAmt, sucbAmt;
            string url, jiwonNm, saNo, pn, htmlGiil, sql, tblGiil, bidDt, bidTm1, bidTm2;
            Int16 state;
                        
            DataGridViewRow row = dg.Rows[rowIdx];
            rptDvsn = row.Cells["dg_RptDvsn"].Value.ToString();
            tid = row.Cells["dg_Tid"].Value.ToString();
            staT = row.Cells["dg_Sta"].Value.ToString();
            staC = row.Cells["dg_State"].Value.ToString();
            bidDt = row.Cells["dg_BidDt"].Value.ToString();

            txtRptIdx.Text = row.Cells["dg_Ridx"].Value.ToString();

            switch (rptDvsn)
            {
                case "낙찰후처리":
                    if (staT == "지급기한" && staC == "기한후납부")
                    {
                        var xRow = dgH.Rows.Cast<DataGridViewRow>().Where(x => x.Cells["dgH_State"].Value?.ToString() == "1215").LastOrDefault();
                        if (xRow == null)
                        {
                            MessageBox.Show("처리할 행이 존재하지 않습니다.");
                            return;
                        }
                        //MessageBox.Show(xRow.Cells["dgH_State"].ValueType.ToString());
                        xRow.Cells["dgH_State"].Value = Convert.ToUInt16(1217);         //기한후납부
                        cbxState.SelectedValue = Convert.ToDecimal(1217);
                        if (!string.IsNullOrEmpty(row.Cells["dg_ShrDt"].Value.ToString()))
                        {
                            int addIdx = dgH.Rows.Add();
                            dgH["dgH_State", addIdx].Value = Convert.ToUInt16(1218);    //배당기일
                            dgH["dgH_BidDt", addIdx].Value = row.Cells["dg_ShrDt"].Value;
                            cbxState.SelectedValue = Convert.ToDecimal(1218);
                        }
                    }
                    else if (staT == "지급기한" && staC == "미납(기일)")
                    {
                        var xRow = dgH.Rows.Cast<DataGridViewRow>().Where(x => x.Cells["dgH_State"].Value?.ToString() == "1210").LastOrDefault();
                        if (xRow == null)
                        {
                            MessageBox.Show("처리할 행이 존재하지 않습니다.");
                            return;
                        }
                        xRow.Cells["dgH_State"].Value = Convert.ToUInt16(1316);         //미납
                        for (int i = xRow.Index + 1; i < dgH.Rows.Count - 1; i++)
                        {
                            if (dgH["dgH_State", i].Value.ToString() == "1212") continue;   //차순위는 삭제하지 않는다
                            dgH["dgH_SucBidr", i].Value = "del";
                        }
                        int addIdx = dgH.Rows.Add();
                        dgH["dgH_State", addIdx].Value = Convert.ToUInt16(1110);    //진행예정(신건/유찰)
                        dgH["dgH_BidDt", addIdx].Value = row.Cells["dg_NxtBidDt"].Value;
                        dgH["dgH_Amt", addIdx].Value = row.Cells["dg_MinbAmt"].Value;
                        dgH["dgH_BidTm", addIdx].Value = mtxtBidTm.Text;
                        if (row.Cells["dg_DpstRate"].Value.ToString() == string.Empty)
                        {
                            var rdpRow = dtReDpstRate.Rows.Cast<DataRow>().Where(t => t["spt_cd"].ToString() == row.Cells["dg_Spt"].Value.ToString()).FirstOrDefault();
                            cbxDpstRate.Text = rdpRow["re_dpst_rate"].ToString();
                        }
                        else
                        {
                            cbxDpstRate.Text = row.Cells["dg_DpstRate"].Value.ToString();
                        }                        
                        txtSucbAmt.Text = string.Empty;
                        mtxtSucbDt.Text = string.Empty;
                        cbxDpstType.SelectedValue = Convert.ToDecimal(2);
                        xRow = dgH.Rows.Cast<DataGridViewRow>().Where(x => x.Cells["dgH_State"].Value?.ToString() == "1111").FirstOrDefault();
                        if (xRow == null)
                        {
                            cbxState.SelectedValue = Convert.ToDecimal(1110);
                        }
                        else
                        {
                            cbxState.SelectedValue = Convert.ToDecimal(1111);
                        }
                        mtxtBidDt.Text = row.Cells["dg_NxtBidDt"].Value.ToString();
                        
                    }
                    else if (staT == "낙찰" && staC == "불허가")
                    {
                        var xRow = dgH.Rows.Cast<DataGridViewRow>().Where(x => x.Cells["dgH_State"].Value?.ToString() == "1210").LastOrDefault();
                        if (xRow == null)
                        {
                            MessageBox.Show("처리할 행이 존재하지 않습니다.");
                            return;
                        }
                        xRow.Cells["dgH_State"].Value = Convert.ToUInt16(1314);         //불허가
                        txtSucbAmt.Text = string.Empty;
                        mtxtSucbDt.Text = string.Empty;
                        cbxState.SelectedValue = Convert.ToDecimal(1314);
                    }
                    else if (staT == "지급기한" && staC == "차순위")
                    {
                        //보류
                    }
                    break;

                case "감정가/최저가/매각결과":
                    apslAmt = row.Cells["dg_ApslAmt"].Value.ToString();
                    minbAmt = row.Cells["dg_MinbAmt"].Value.ToString();
                    if (apslAmt != "0" && minbAmt != "0")
                    {
                        if (apslAmt != txtApslAmt.Text)
                        {
                            txtApslAmt.Text = apslAmt;
                            txtApslAmt.BackColor = Color.LightGreen;
                        }
                        if (minbAmt != txtMinbAmt.Text)
                        {
                            txtMinbAmt.Text = minbAmt;
                            txtMinbAmt.BackColor = Color.LightGreen;
                        }
                        var xRow = dgH.Rows.Cast<DataGridViewRow>().Where(x => x.Cells["dgH_State"].Value?.ToString() == "1110").LastOrDefault();
                        if (xRow == null)
                        {
                            MessageBox.Show("처리할 행이 존재하지 않습니다.");
                            return;
                        }
                        xRow.Cells["dgH_Amt"].Value = minbAmt;
                    }
                    else if (staT == "유찰" && staC == "매각")
                    {
                        sucbAmt = row.Cells["dg_SucbAmt"].Value.ToString();
                        if (sucbAmt == "0") return;

                        var xRow = dgH.Rows.Cast<DataGridViewRow>().Where(x => x.Cells["dgH_State"].Value?.ToString() == "1111").LastOrDefault();
                        if (xRow == null)
                        {
                            MessageBox.Show("처리할 행이 존재하지 않습니다.");
                            return;
                        }
                        bidDt = xRow.Cells["dgH_BidDt"].Value.ToString();
                        xRow.Cells["dgH_State"].Value = Convert.ToUInt16(1110);         //예정
                        txtMinbAmt.Text = string.Format("{0:N0}", xRow.Cells["dgH_Amt"].Value);

                        xRow = dgH.Rows.Cast<DataGridViewRow>().Where(x => x.Cells["dgH_State"].Value?.ToString() == "1110").LastOrDefault();                        
                        xRow.Cells["dgH_State"].Value = Convert.ToUInt16(1210);         //낙찰
                        xRow.Cells["dgH_BidDt"].Value = bidDt;
                        xRow.Cells["dgH_BidTm"].Value = "00:00";
                        xRow.Cells["dgH_Amt"].Value = sucbAmt;
                        mtxtBidDt.Text = xRow.Cells["dgH_BidDt"].Value.ToString();                        
                        txtSucbAmt.Text = sucbAmt;
                        mtxtSucbDt.Text = bidDt;
                        cbxState.SelectedValue = Convert.ToDecimal(1210);
                        txtFbCnt.Text = (Convert.ToDecimal(txtFbCnt.Text) - 1).ToString();
                    }
                    break;

                case "물건 상태":
                    if ((staT == "변경" && (row.Cells["dg_PdState"].Value.ToString() == "대급납부" || row.Cells["dg_PdState"].Value.ToString() == "기한후납부")) || (staC == "기한후납부" && (staT == "유찰" || staT == "신건"))) ;
                    {
                        var xRow = dgH.Rows.Cast<DataGridViewRow>().Where(x => x.Cells["dgH_State"].Value?.ToString() == "1316").LastOrDefault();
                        if (xRow == null)
                        {
                            MessageBox.Show("처리할 행이 존재하지 않습니다.");
                            return;
                        }
                        xRow.Cells["dgH_State"].Value = Convert.ToUInt16(1210);             //낙찰
                        mtxtBidDt.Text = xRow.Cells["dgH_BidDt"].Value.ToString();
                        txtSucbAmt.Text = string.Format("{0:N0}", xRow.Cells["dgH_Amt"].Value);
                        mtxtSucbDt.Text = xRow.Cells["dgH_BidDt"].Value.ToString();

                        for (int i = xRow.Index + 1; i < dgH.Rows.Count - 1; i++)
                        {
                            if (dgH["dgH_State", i].Value.ToString() == "1212") continue;   //차순위는 삭제하지 않는다
                            dgH["dgH_SucBidr", i].Value = "del";
                        }
                        bidDt = xRow.Cells["dgH_BidDt"].Value.ToString();

                        sql = "select sn1,sn2,pn,spt,sta2 from ta_list where tid=" + tid;
                        db.Open();
                        MySqlDataReader dr = db.ExeRdr(sql);
                        dr.Read();
                        state = Convert.ToInt16(dr["sta2"]);
                        saNo = String.Format("{0}0130{1}", dr["sn1"], dr["sn2"].ToString().PadLeft(6, '0'));
                        jiwonNm = auctCd.FindLawNm(string.Format("{0}", dr["spt"]), true);
                        pn = dr["pn"].ToString();
                        if (pn == "0") pn = "1";
                        dr.Close();
                        db.Close();

                        url = "http://www.courtauction.go.kr/RetrieveRealEstSaDetailInqGiilList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
                        htmlGiil = net.GetHtml(url);
                        Match match = Regex.Match(htmlGiil, @"<table class=""Ltbl_dt"" summary=""기일내역 표"">.*?</table>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        tblGiil = match.Value;

                        Dictionary<string, string> dic = prcGiil(1210, pn, bidDt, tblGiil);
                        if (dic["매각허가"] != string.Empty)
                        {
                            int addIdx = dgH.Rows.Add();
                            dgH["dgH_State", addIdx].Value = Convert.ToUInt16(1211);    //허가
                            dgH["dgH_BidDt", addIdx].Value = dic["매각허가"];
                        }
                        if (dic["지급기한"] != string.Empty)
                        {
                            int addIdx = dgH.Rows.Add();
                            dgH["dgH_State", addIdx].Value = Convert.ToUInt16(1217);    //기한후납부
                            dgH["dgH_BidDt", addIdx].Value = dic["지급기한"];
                        }
                        if (dic["배당기일"] != string.Empty)
                        {
                            int addIdx = dgH.Rows.Add();
                            dgH["dgH_State", addIdx].Value = Convert.ToUInt16(1218);    //배당기일
                            dgH["dgH_BidDt", addIdx].Value = dic["배당기일"];
                        }
                        cbxState.SelectedValue = dgH["dgH_State", dgH.Rows.Count - 2].Value;
                    }
                    break;

                case "입찰 시간":                    
                    bidTm1 = row.Cells["dg_BidTm1"].Value.ToString();
                    bidTm2 = row.Cells["dg_BidTm2"].Value.ToString();
                    Match m1 = Regex.Match(bidTm1, @"^1[0-7]:[03]0", rxOptM);
                    Match m2 = Regex.Match(bidTm2, @"^[01][0-7]:[03]0", rxOptM);
                    var xRows = dgH.Rows.Cast<DataGridViewRow>().Where(x => x.Cells["dgH_State"].Value?.ToString() == "1110" && x.Cells["dgH_BidDt"].Value?.ToString() == row.Cells["dg_BidDt"].Value.ToString());

                    if (m1.Success && m2.Success)
                    {                        
                        mtxtBidTm.Text = m1.Value;
                        mtxtBidTm1.Text = m1.Value;
                        mtxtBidTm2.Text = m2.Value;
                        int bidCnt = (m2.Value == "00:00") ? 1 : 2;
                        if (xRows.Count() == bidCnt)
                        {
                            if (bidCnt == 1)
                            {
                                xRows.LastOrDefault().Cells["dgH_BidTm"].Value = m1.Value;
                            }
                            else
                            {
                                int dgHCnt = dgH.Rows.Count;
                                dgH["dgH_BidTm", dgHCnt - 3].Value = m1.Value;
                                dgH["dgH_BidTm", dgHCnt - 2].Value = m2.Value;
                            }
                        }
                        else
                        {
                            MessageBox.Show("입찰회수를 확인 해 주세요~");
                        }
                    }
                    else
                    {
                        MessageBox.Show("입찰시간을 확인 해 주세요~");
                    }

                    if (cbxBidCnt.Text == "0" || xRows.Count() != Convert.ToInt32(cbxBidCnt.Text))
                    {
                        MessageBox.Show("입찰회수를 확인 해 주세요~");
                    }
                    break;
            }
        }

        /// <summary>
        /// 기일내역 분석
        /// </summary>
        /// <param name="row"></param>
        /// <param name="tblGiil"></param>
        /// <returns></returns>
        private Dictionary<string, string> prcGiil(Int16 state, string pNum, string bidDt, string tblGiil)
        {
            string pNumS = "", prevPnumS = "";
            string bidDtS = "", bidType = "", bidRst = "", bidS = "", lowS = "", payDt = "";

            string shrPatn = @"(배당기일|일부배당 및 상계|일부배당)";

            DataTable dtShr = new DataTable();
            dtShr.Columns.Add("dt");
            dtShr.Columns.Add("rst");

            RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;
            RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;

            Dictionary<string, string> dic = new Dictionary<string, string>();
            dic.Add("기일결과", "");
            dic.Add("매각허가", "");
            dic.Add("낙찰가", "");
            dic.Add("최저가", "");
            dic.Add("매각기일", "");
            dic.Add("지급기한", "");
            dic.Add("납부일자", "");
            dic.Add("배당기일", "");
            dic.Add("보증금율", "");
                        
            if (pNum == "0") pNum = "1";

            MatchCollection mcTr = Regex.Matches(tblGiil, @"<tr class=""Ltbl_list_lvl[01]"">.*?</tr>", rxOptS);

            //배당기일정보를 미리 구한다.
            foreach (Match maTr in mcTr)
            {
                MatchCollection mcTd = Regex.Matches(maTr.Value, @"<td[\s\w\d=""""]*>(.*?)</td>", rxOptS);
                if (mcTd.Count == 7 && Regex.IsMatch(mcTd[3].Groups[1].Value.Trim(), shrPatn, rxOptS))
                {
                    dtShr.Rows.Add(mcTd[2].Groups[1].Value.Trim().Substring(0, 10).Replace(".", "-"), mcTd[6].Groups[1].Value.Trim());
                }
            }

            foreach (Match maTr in mcTr)
            {
                MatchCollection mcTd = Regex.Matches(maTr.Value, @"<td[\s\w\d=""""]*>(.*?)</td>", rxOptS);
                if (mcTd.Count == 7)
                {
                    pNumS = Regex.Replace(mcTd[0].Groups[1].Value, @"<[^>]*?>", string.Empty, rxOptS).Trim();
                    if (Regex.IsMatch(mcTd[3].Groups[1].Value.Trim(), shrPatn, rxOptS)) continue;   //배당기일 관련 키워드 건너 뜀

                    if (pNumS == string.Empty) pNumS = prevPnumS;
                    if (pNum == pNumS)
                    {
                        bidDtS = mcTd[2].Groups[1].Value.Trim();
                        bidType = mcTd[3].Groups[1].Value.Trim();
                        bidRst = mcTd[6].Groups[1].Value.Trim();
                        lowS = mcTd[5].Groups[1].Value.Trim().Replace("원", string.Empty).Replace(",", string.Empty);
                    }
                    else
                    {
                        pNumS = string.Empty;
                        continue;
                    }
                }
                else if (mcTd.Count == 5)
                {
                    bidDtS = mcTd[0].Groups[1].Value.Trim();
                    bidType = mcTd[1].Groups[1].Value.Trim();
                    bidRst = mcTd[4].Groups[1].Value.Trim();
                    lowS = mcTd[3].Groups[1].Value.Trim().Replace("원", string.Empty).Replace(",", string.Empty);
                }
                prevPnumS = pNumS;

                bidDtS = bidDtS.Substring(0, 10).Replace(".", "-");

                Match match;
                if (pNumS == pNum && Convert.ToDateTime(bidDtS) >= Convert.ToDateTime(bidDt))
                {
                    if (state == 1210 || state == 1212 || state == 1213 || state == 1214)   //낙찰, 차순위, 결정변경, 추후지정
                    {
                        match = Regex.Match(bidRst, @"(불허가|취소|변경|추후|최고가매각허가|차순위매각허가)");
                        if (match.Success)
                        {
                            dic["기일결과"] = match.Groups[1].Value.Replace("매각허가", string.Empty);
                            if (match.Groups[1].Value.Contains("매각허가"))
                            {
                                dic["매각허가"] = bidDtS;
                            }
                        }

                        //차순위가 나올 경우 낙찰가 대소비교를 위해 미리 구한다.
                        if (bidType == "매각기일" && bidRst.Contains("매각"))
                        {
                            bidS = Regex.Match(bidRst, @"\(([0-9]+(,[0-9]+)*)원\)", rxOptM).Groups[1].Value.Replace(",", string.Empty);
                            dic["낙찰가"] = bidS;
                        }
                    }
                                        
                    if (state == 1210 || state == 1211) ;  //허가
                    {
                        /*if (bidType.Contains("매각결정기일"))
                        {                            
                            if (state == 27)
                            {
                                match = Regex.Match(bidRst, @"(최고가매각허가|차순위매각허가)");
                                if (match.Success)
                                {
                                    dic["기일결과"] = match.Groups[1].Value.Replace("매각허가", string.Empty);
                                    dic["매각허가"] = bidDtS;
                                }
                            }
                            else
                            {
                                if(!bidRst.Contains("매각허가") && bidRst != string.Empty) dic["기일결과"] = bidRst;
                            }
                        }*/
                        if (bidType.Contains("대금지급"))
                        {
                            dic["지급기한"] = bidDtS;
                        }
                    }

                    if (state == 1210 || state == 1211 || state == 1215)     //허가, 지급기한
                    {
                        match = Regex.Match(bidRst, @"(납부|미납|차순위|허가취소)");
                        if (match.Success)
                        {
                            if (match.Groups[1].Value == "납부")
                            {
                                dic["기일결과"] = (dic["기일결과"] == "차순위") ? "차순위납부" : "";
                                if (bidRst.Contains("기한후납부"))
                                {
                                    dic["기일결과"] = "기한후납부";
                                }
                                dic["납부일자"] = Regex.Match(bidRst, @"(\d{4}.\d{2}.\d{2})").Groups[1].Value;
                            }
                            else
                            {
                                dic["기일결과"] = match.Groups[1].Value;
                                //dic["납부일자"] = "";
                            }

                            if (match.Groups[1].Value == "차순위" && bidType.Contains("매각결정기일"))
                            {
                                dic["기일결과"] = "차순위";
                                dic["매각허가"] = bidDtS;
                                //dic["납부일자"] = "";
                            }
                        }

                        if (dic["기일결과"] == "미납" && bidType.Contains("매각기일"))
                        {
                            dic["기일결과"] = "미납(기일)";
                            dic["매각기일"] = bidDtS;
                            dic["최저가"] = lowS;
                            //dic["납부일자"] = "";
                            //dic["보증금율"] = prcDpstRate(htmlSagun, pNum);
                        }

                        //차순위가 나올 경우 낙찰가 대소비교를 위해 미리 구한다.
                        if (bidType == "매각기일" && bidRst.Contains("매각"))
                        {
                            bidS = Regex.Match(bidRst, @"\(([0-9]+(,[0-9]+)*)원\)", rxOptM).Groups[1].Value.Replace(",", string.Empty);
                            dic["낙찰가"] = bidS;
                        }
                    }

                    if (state == 1210 || state == 1217 || state == 1215 || state == 1216)    //기한후납부, 지급기한, 납부
                    {
                        match = Regex.Match(bidRst, @"(납부)");
                        if (match.Success)
                        {
                            payDt = Regex.Match(bidRst, @"(\d{4}.\d{2}.\d{2})").Groups[1].Value.Trim().Replace(".", "-");
                        }

                        //기한후납부 일 경우 납부일자가 없음-이 경우에는 해당 기일을 배당기일로 대신한다.(ex-1775009)
                        if (payDt == string.Empty && bidRst.Contains("기한후납부"))
                        {
                            payDt = bidDtS;
                        }

                        foreach (DataRow r in dtShr.Rows)
                        {
                            if (payDt == string.Empty) continue;
                            if (Convert.ToDateTime(r["dt"]) > Convert.ToDateTime(payDt))
                            {
                                dic["배당기일"] = r["dt"].ToString();
                                break;
                            }
                        }

                        if (dic["배당기일"] == string.Empty && bidType.Contains("대금지급및 배당기일"))
                        {
                            dic["배당기일"] = bidDtS;
                        }
                    }

                    //if (state == 19 || state == 28 || state == 29 || state == 34)
                    if (state==1210 || state == 1218)  //배당기일
                    {
                        foreach (DataRow r in dtShr.Rows)
                        {
                            if (r["rst"].ToString() == string.Empty) continue;
                            if (Regex.IsMatch(r["rst"].ToString(), @"추후지정|납부|진행") == false)
                            {
                                dic["기일결과"] = r["rst"].ToString();
                                break;
                            }
                        }
                    }
                }
            }

            return dic;
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
            jiwonNm = auctCd.FindLawNm(txtSptCd.Text, true);
            saNo = string.Format("{0}0130{1}", txtSn1.Text, txtSn2.Text.PadLeft(6, '0'));
            maemulSer = (txtPn.Text == "0") ? "1" : txtPn.Text;

            if (lnkTxt == "사건내역") url = "RetrieveRealEstDetailInqSaList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&srnID=PNO101005&_SRCH_SRNID=PNO101005";
            else if (lnkTxt == "기일내역") url = "RetrieveRealEstSaDetailInqGiilList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
            else if (lnkTxt == "문건/송달") url = "RetrieveRealEstSaDetailInqMungunSongdalList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
            else if (lnkTxt == "물건상세") url = "RetrieveRealEstCarHvyMachineMulDetailInfo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&maemulSer=" + maemulSer;
            else if (lnkTxt == "현황조사") url = "RetrieveRealEstSaHjosa.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&ordHoi=1";
            else if (lnkTxt == "표시목록") url = "RetrieveRealEstHjosaDispMokrok.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&ordHoi=1";
            else if (lnkTxt == "매각공고")
            {
                url = "RetrieveRealEstMgakNotifySrchGyulgwa.laf?ipchalGbnCd=000331&jiwonNm=" + jiwonNm + "&maeGiil=" + mtxtBidDt.Text + "&jpDeptCd=" + txtDptCd.Text;
            }

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
            string lnkTxt, url, spt, tid, sn, tbl, sql, idx = "", ctgr = "";

            lnkTxt = ((LinkLabel)sender).Text;

            if (lnkTxt == "사건내역") ctgr = "AA";
            else if (lnkTxt == "기일내역") ctgr = "AB";
            else if (lnkTxt == "문건/송달") ctgr = "AC";
            else if (lnkTxt == "현황조사") ctgr = "AD";
            else if (lnkTxt == "물건상세") ctgr = "AJ";
            else if (lnkTxt == "건축물") ctgr = "EC";
            else if (lnkTxt == "토지등기") ctgr = "DA";
            else if (lnkTxt == "건물등기") ctgr = "DB";

            tid = lnkTid.Text;
            sn = string.Format("{0}{1}", txtSn1.Text, txtSn2.Text.PadLeft(6, '0'));
            spt = txtSptCd.Text;
            tbl = (Convert.ToDecimal(txtSn1.Text) > 2004) ? "ta_f" + txtSn1.Text : "ta_f2004";

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
                if (lnkTxt == "건축물")
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
        /// 감정평가서, 매물명세서, 등기부등본 보기(PDF 문서)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>        
        private void lnkPdflDoc_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            /*string lnkSrc = "", url, html, tid, sql, spt, sn, sn1, sn2, jiwonNm, saNo, pn, maemulSer, maeGiil, jpDeptCd;
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
                    axPdf.src = url;
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
                }
                else
                {
                    //
                }
            }
            else if (lnkLbl == lnkTK_Stmt)
            {
                string tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
                sql = "select * from " + tbl + " where (tid=" + tid + " or (spt=" + spt + " and sn='" + sn + "' and tid=0)) and ctgr='AG' limit 1";
                db.Open();
                dr = db.ExeRdr(sql);
                if (dr.HasRows)
                {
                    dr.Read();
                    url = string.Format(myWeb + "FILE/CA/AG/{0}/{1}/{2}", spt, sn1, dr["file"]);
                    axPdf.src = url;
                }
                else
                {
                    MessageBox.Show("저장된 [매각물건 명세서] 파일이 없습니다.");
                }
                dr.Close();
                db.Close();
            }
            else if (lnkLbl == lnkTK_Apsl)
            {
                string tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
                sql = "select count(*) as cnt, file from " + tbl + " where (tid=" + tid + " or (spt=" + spt + " and sn='" + sn + "' and tid=0)) and ctgr='AF'";
                db.Open();
                dr = db.ExeRdr(sql);
                dr.Read();
                if (dr["cnt"].ToString() == "0")
                {
                    MessageBox.Show("저장된 [감정평가서] 파일이 없습니다.");
                }
                else
                {
                    url = string.Format(myWeb + "FILE/CA/AF/{0}/{1}/{2}", spt, sn1, dr["file"]);
                    axPdf.src = url;
                    if (dr["cnt"].ToString() != "1")
                    {
                        MessageBox.Show("2건 이상의 [감정평가서] 파일이 있습니다.\r\n파일정보를 참조 해 주세요");
                    }
                }
                dr.Close();
                db.Close();
            }
            else if (lnkLbl == lnkTK_LandRgst)
            {
                string tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
                sql = "select * from " + tbl + " where (tid=" + tid + " or (spt=" + spt + " and sn='" + sn + "' and tid=0)) and ctgr='DA' limit 1";
                db.Open();
                dr = db.ExeRdr(sql);
                if (dr.HasRows)
                {
                    dr.Read();
                    url = string.Format(myWeb + "FILE/CA/DA/{0}/{1}/{2}", spt, sn1, dr["file"]);
                    axPdf.src = url;
                }
                else
                {
                    MessageBox.Show("저장된 [토지 등기] 파일이 없습니다.");
                }
                dr.Close();
                db.Close();
            }
            else if (lnkLbl == lnkTK_BldgRgst)
            {
                string tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
                sql = "select * from " + tbl + " where (tid=" + tid + " or (spt=" + spt + " and sn='" + sn + "' and tid=0)) and ctgr='DB' limit 1";
                db.Open();
                dr = db.ExeRdr(sql);
                if (dr.HasRows)
                {
                    dr.Read();
                    url = string.Format(myWeb + "FILE/CA/DB/{0}/{1}/{2}", spt, sn1, dr["file"]);
                    axPdf.src = url;
                }
                else
                {
                    MessageBox.Show("저장된 [건물 등기] 파일이 없습니다.");
                }
                dr.Close();
                db.Close();
            }
            tbcL.SelectedTab = tabPdf1;
            axPdf.src = lnkSrc;*/
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
        }

        /// <summary>
        /// 일괄 처리
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnPrc_Click(object sender, EventArgs e)
        {
            if (cbxBatPrc.SelectedIndex == 0 || cbxBatPrc.Text.Contains("---"))
            {
                MessageBox.Show("처리구분을 선택 해 주세요.");
                return;
            }

            if (dg.Rows.Count == 0)
            {
                MessageBox.Show("처리할 자료를 검색 해 주세요");
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
            int nxtSeq = 0, curCnt = 0, findCnt = 0, dtIdx = 0, errCnt = 0;
            string batPrc, tid, sta1, sta2, bidDt, bidDtm = "", bidTm, bidTm2, sql, idx, clsDt, clsRslt, clsCd, hisIdx = "", prcNote = "";
            string spt, jiwonNm, url, saNo, tkPn, fbCnt, caPn = "", apslAmtTk = "0", apslAmt = "0", minbAmt = "0", minbAmt2 = "0", sucbAmt = "0", bidCnt = "", dtDvsn = "", bidRslt, prevSaNo = "", html = "";
            string dbBidDt, dbSta;

            Dictionary<string, string> dicSms = new Dictionary<string, string>();

            batPrc = cbxBatPrc.Text;

            if (batPrc == "물건상태-종국")
            {
                var xRows = dg.Rows.Cast<DataGridViewRow>().Where(t => t.Cells["dg_ClsDt"].Value?.ToString() != string.Empty && t.Cells["dg_RptPrc"].Value.ToString() == "미처리");
                if (xRows?.Count() == 0)
                {
                    MessageBox.Show("대상 물건이 없습니다.");
                    return;
                }

                db.Open();
                foreach (DataGridViewRow row in xRows)
                {
                    if (!row.Displayed) dg.FirstDisplayedScrollingRowIndex = row.Index;
                    idx = row.Cells["dg_Ridx"].Value.ToString();
                    tid = row.Cells["dg_Tid"].Value.ToString();
                    sta1 = row.Cells["dg_Sta1"].Value.ToString();
                    bidDt = row.Cells["dg_BidDt"].Value.ToString();
                    clsDt = row.Cells["dg_ClsDt"].Value.ToString();
                    clsRslt = row.Cells["dg_ClsRslt"].Value.ToString();
                    if (clsRslt == "각하") clsCd = "1410";
                    else if (clsRslt == "기각") clsCd = "1411";
                    else if (clsRslt == "기타") clsCd = "1412";
                    else if (clsRslt == "이송") clsCd = "1413";
                    else if (clsRslt == "취소") clsCd = "1414";
                    else if (clsRslt == "취하") clsCd = "1415";
                    else if (clsRslt == "배당종결") clsCd = "1219";
                    else
                    {
                        row.Cells["dg_PrcNote"].Value = "종국코드 없음";
                        continue;
                    }
                    if (clsRslt == "배당종결" && sta1 != "10") continue;

                    sql = "update ta_list set sta1=" + clsCd.Substring(0, 2) + ", sta2=" + clsCd + ", end_dt='" + clsDt + "' where tid='" + tid + "'";
                    db.ExeQry(sql);
                    if (dicSms.ContainsKey(tid) == false) dicSms.Add(tid, "종국");

                    if (sta1 == "10" || sta1 == "14")
                    {
                        sql = "update db_tank.tx_rpt set prc=2 where idx='" + idx + "'";
                        db.ExeQry(sql);
                    }
                    if (sta1 == "10" || sta1 == "14") continue;

                    sql = "select idx,seq from ta_hist where tid='" + tid + "' order by seq desc limit 1";
                    MySqlDataReader dr = db.ExeRdr(sql);
                    dr.Read();
                    if (dr.HasRows == false)
                    {
                        //오류 기록
                        nxtSeq = 1;
                    }
                    else
                    {
                        hisIdx = dr["idx"].ToString();
                        nxtSeq = Convert.ToInt32(dr["seq"].ToString()) + 1;
                    }
                    dr.Close();

                    if (Convert.ToDateTime(clsDt) > Convert.ToDateTime(bidDt))
                    {
                        sql = "insert into ta_hist set tid='" + tid + "', seq='" + nxtSeq + "', sta='" + clsCd + "', bid_dt='" + clsDt + "'";
                        db.ExeQry(sql);
                    }
                    else
                    {
                        sql = "update ta_hist set sta='" + clsCd + "' where idx='" + hisIdx + "'";
                        db.ExeQry(sql);
                    }
                    sql = "update db_tank.tx_rpt set prc=2 where idx='" + idx + "'";
                    db.ExeQry(sql);

                    row.DefaultCellStyle.BackColor = Color.White;
                }
                db.Close();
            }
            else if (batPrc == "물건상태-변경")
            {
                var xRows = dg.Rows.Cast<DataGridViewRow>().Where(t => t.Cells["dg_State"].Value?.ToString() == "변경" && t.Cells["dg_RptPrc"].Value.ToString() == "미처리");
                if (xRows?.Count() == 0)
                {
                    MessageBox.Show("대상 물건이 없습니다.");
                    return;
                }

                db.Open();
                foreach (DataGridViewRow row in xRows)
                {
                    if (!row.Displayed) dg.FirstDisplayedScrollingRowIndex = row.Index;
                    idx = row.Cells["dg_Ridx"].Value.ToString();
                    tid = row.Cells["dg_Tid"].Value.ToString();
                    sta1 = row.Cells["dg_Sta1"].Value.ToString();
                    if (sta1 != "11") continue;

                    sql = "update ta_list set sta1=13, sta2=1310 where tid='" + tid + "'";
                    db.ExeQry(sql);
                    if (dicSms.ContainsKey(tid) == false) dicSms.Add(tid, "미진행");

                    sql = "update ta_hist set sta=1310 where tid='" + tid + "' and bid_dt >= curdate()";
                    db.ExeQry(sql);

                    sql = "update db_tank.tx_rpt set prc=2 where idx='" + idx + "'";
                    db.ExeQry(sql);

                    row.DefaultCellStyle.BackColor = Color.White;
                }
                db.Close();
            }
            else if (batPrc == "물건상태-공란")
            {
                var xRows = dg.Rows.Cast<DataGridViewRow>().Where(t => t.Cells["dg_Sta"].Value?.ToString() != "미진행" && t.Cells["dg_Sta"].Value?.ToString() != "정지" && t.Cells["dg_State"].Value?.ToString() == "공란" && t.Cells["dg_RptPrc"].Value.ToString() == "미처리");
                if (xRows?.Count() == 0)
                {
                    MessageBox.Show("대상 물건이 없습니다.");
                    return;
                }

                Regex rx = new Regex(@"(\d+)\-(\d+)[ ]*(\((\d+)\))*");
                HAPDoc doc = new HAPDoc();
                DateTime curDate = DateTime.Now;

                DataTable dt = new DataTable();
                dt.Columns.Add("apslAmt");
                dt.Columns.Add("bidDt");
                dt.Columns.Add("bidTm");
                dt.Columns.Add("minbAmt");
                dt.Columns.Add("bidRslt");

                foreach (DataGridViewRow row in xRows)
                {
                    curCnt++;
                    findCnt = 0;
                    apslAmt = ""; bidDt = ""; bidTm = ""; minbAmt = ""; bidRslt = ""; bidTm2 = ""; minbAmt2 = ""; bidCnt = ""; dbBidDt = ""; dbSta = "";
                    dt.Rows.Clear();
                    if (!row.Displayed) dg.FirstDisplayedScrollingRowIndex = row.Index;

                    idx = row.Cells["dg_Ridx"].Value.ToString();
                    tid = row.Cells["dg_Tid"].Value.ToString();
                    fbCnt = row.Cells["dg_FbCnt"].Value.ToString();
                    Match match = rx.Match(row.Cells["dg_SN"].Value.ToString());
                    jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", row.Cells["dg_Spt"].Value));
                    saNo = string.Format("{0}0130{1}", match.Groups[1].Value, match.Groups[2].Value.PadLeft(6, '0'));
                    tkPn = match.Groups[4].Value.Trim();
                    if (tkPn == string.Empty) tkPn = "1";

                    if (saNo != prevSaNo)
                    {
                        webCnt++;
                        if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                        url = "http://www.courtauction.go.kr/RetrieveRealEstSaDetailInqGiilList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
                        html = net.GetHtml(url);
                        if (html.Contains("검색결과가 없습니다"))
                        {
                            row.Cells["dg_PrcNote"].Value = "기일내역 없음";
                            continue;
                        }
                    }

                    doc.LoadHtml(html);
                    HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='기일내역 표']/tbody/tr[@class='Ltbl_list_lvl0' or @class='Ltbl_list_lvl1']");
                    if (ncTr == null) continue;

                    foreach (HtmlNode ndTr in ncTr)
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        if (ncTd.Count == 7)
                        {
                            if (ncTd[0].InnerText.Trim() == string.Empty) continue;

                            if (ncTd[0].FirstChild != null)
                            {
                                caPn = ncTd[0].FirstChild.InnerText.Trim();
                            }
                            apslAmt = ncTd[1].InnerText.Trim();
                            bidDtm = ncTd[2].InnerText.Trim();
                            dtDvsn = ncTd[3].InnerText.Trim();
                            minbAmt = ncTd[5].InnerText.Trim();
                            bidRslt = ncTd[6].FirstChild.InnerText.Trim();
                        }
                        else if (ncTd.Count == 5)
                        {
                            bidDtm = ncTd[0].InnerText.Trim();
                            dtDvsn = ncTd[1].InnerText.Trim();
                            minbAmt = ncTd[3].InnerText.Trim();
                            bidRslt = ncTd[4].FirstChild.InnerText.Trim();
                        }
                        else
                        {
                            continue;
                        }

                        if (caPn == tkPn)
                        {
                            if (dtDvsn == "매각기일")
                            {
                                bidDt = bidDtm.Substring(0, 10).Replace(".", "-");
                                bidTm = bidDtm.Substring(11, 5) + ":00";
                                if (Convert.ToDateTime(bidDt) > curDate && bidRslt == "")
                                {
                                    findCnt++;
                                    dt.Rows.Add(apslAmt, bidDt, bidTm, minbAmt, bidRslt);
                                }
                            }
                        }
                        if (findCnt > 0 && caPn != tkPn) break;
                    }
                    prevSaNo = saNo;

                    if (findCnt == 0) continue;
                    /*
                    var tRow = dt.Rows.Cast<DataRow>().Where(t => t["bidRslt"].ToString() == string.Empty).GroupBy(t => t.Field<string>("bidDt"));
                    if (tRow.Count() > 1)
                    {
                        row.Cells["dg_PrcNote"].Value = "기일 오류";
                        continue;
                    }                    
                    if (dt.Select("bidRslt=''").Count() > 1)
                    {
                        row.Cells["dg_PrcNote"].Value = "기일 오류";
                        continue;
                    }
                    */
                    //dtIdx = dt.Rows.Count - 1;
                    //if (dt.Rows[dtIdx]["bidRslt"].ToString() != string.Empty) continue;
                    if (dt.Rows.Count > 1)
                    {
                        var tRow = dt.Rows.Cast<DataRow>().GroupBy(t => t.Field<string>("bidDt"));
                        if (tRow.Count() > 1)
                        {
                            row.Cells["dg_PrcNote"].Value = "기일 오류";
                            continue;
                        }
                    }

                    apslAmt = dt.Rows[0]["apslAmt"].ToString().Replace("원", string.Empty);
                    bidDt = dt.Rows[0]["bidDt"].ToString();
                    bidTm = dt.Rows[0]["bidTm"].ToString();
                    minbAmt = dt.Rows[0]["minbAmt"].ToString().Replace("원", string.Empty);
                    bidCnt = "1";
                    if (dt.Rows.Count > 1)
                    {
                        if (dt.Rows[0]["bidDt"].ToString() == dt.Rows[1]["bidDt"].ToString())
                        {
                            bidTm2 = dt.Rows[1]["bidTm"].ToString();
                            minbAmt2 = dt.Rows[1]["minbAmt"].ToString().Replace("원", string.Empty);
                            bidCnt = "2";
                            row.Cells["dg_PrcNote"].Value = "2회 입찰";
                        }
                    }

                    row.Cells["dg_NxtBidDt"].Value = bidDt;
                    row.Cells["dg_ApslAmt"].Value = apslAmt;
                    row.Cells["dg_MinbAmt"].Value = minbAmt;

                    //감정가가 서로 다를 경우 관리자 확인으로 넘긴다.
                    sql = "select apsl_amt from ta_list where tid=" + tid + " limit 1";
                    db.Open();
                    MySqlDataReader dr = db.ExeRdr(sql);
                    dr.Read();
                    apslAmtTk = dr["apsl_amt"].ToString();
                    dr.Close();
                    db.Close();
                    if (apslAmtTk != apslAmt.Replace(",", string.Empty))
                    {
                        row.Cells["dg_PrcNote"].Value = "감정가 상이함";
                        sql = "update db_tank.tx_rpt set prc=9 where idx='" + idx + "'";
                        db.Open();
                        db.ExeQry(sql);
                        db.Close();
                        continue;
                    }

                    //DB 처리
                    db.Open();                    
                    sql = "select seq, bid_dt, sta from ta_hist where tid='" + tid + "' order by seq desc limit 1";
                    dr = db.ExeRdr(sql);
                    dr.Read();
                    if (dr.HasRows)
                    {
                        nxtSeq = Convert.ToInt32(dr["seq"].ToString()) + 1;
                        dbBidDt = string.Format("{0:yyyy-MM-dd}", dr["bid_dt"]);
                        dbSta = dr["sta"].ToString();
                    }
                    else
                    {
                        nxtSeq = 1;
                    }
                    dr.Close();

                    if (bidDt == dbBidDt && dbSta == "1310")
                    {
                        sql = "update db_tank.tx_rpt set prc=9 where idx='" + idx + "'";
                        db.ExeQry(sql);
                        row.Cells["dg_PrcNote"].Value = "변경/예정 동일자";
                    }
                    else
                    {
                        sql = "insert into ta_hist set tid='" + tid + "', seq='" + nxtSeq.ToString() + "', sta='1110', amt='" + minbAmt.Replace(",", string.Empty) + "', bid_dt='" + bidDt + "', bid_tm='" + bidTm + "'";
                        db.ExeQry(sql);
                        if (bidTm2 != "")
                        {
                            sql = "insert into ta_hist set tid='" + tid + "', seq='" + (nxtSeq + 1).ToString() + "', sta='1110', amt='" + minbAmt2.Replace(",", string.Empty) + "', bid_dt='" + bidDt + "', bid_tm='" + bidTm2 + "'";
                            db.ExeQry(sql);
                        }
                        sta1 = "11";
                        sta2 = (fbCnt == "0") ? "1110" : "1111";
                        sql = "update ta_list set sta1=" + sta1 + ", sta2=" + sta2 + ", bid_dt='" + bidDt + "', apsl_amt='" + apslAmt.Replace(",", string.Empty) + "', minb_amt='" + minbAmt.Replace(",", string.Empty) + "', bid_tm='" + bidTm + "', bid_cnt='" + bidCnt + "', bid_tm1='" + bidTm + "', bid_tm2='" + bidTm2 + "' where tid='" + tid + "'";
                        db.ExeQry(sql);
                        if (dicSms.ContainsKey(tid) == false) dicSms.Add(tid, "진행");

                        sql = "update db_tank.tx_rpt set prc=2 where idx='" + idx + "'";
                        db.ExeQry(sql);
                    }
                    db.Close();

                    row.DefaultCellStyle.BackColor = Color.White;
                }
            }
            else if (batPrc == "공고상태-변경")
            {
                if (dg.SelectedRows.Count == 0)
                {
                    MessageBox.Show("선택된 물건이 없습니다.");
                    return;
                }

                db.Open();
                foreach (DataGridViewRow row in dg.SelectedRows.Cast<DataGridViewRow>().Reverse())
                {
                    if (!row.Displayed) dg.FirstDisplayedScrollingRowIndex = row.Index;
                    idx = row.Cells["dg_Ridx"].Value.ToString();
                    tid = row.Cells["dg_Tid"].Value.ToString();
                    sta1 = row.Cells["dg_Sta1"].Value.ToString();
                    if (sta1 != "11") continue;

                    sql = "update ta_list set sta1=13, sta2=1310 where tid='" + tid + "'";
                    db.ExeQry(sql);
                    if (dicSms.ContainsKey(tid) == false) dicSms.Add(tid, "미진행");

                    sql = "update ta_hist set sta=1310 where tid='" + tid + "' and bid_dt >= curdate()";
                    db.ExeQry(sql);

                    sql = "update db_tank.tx_rpt set prc=2 where idx='" + idx + "'";
                    db.ExeQry(sql);

                    row.DefaultCellStyle.BackColor = Color.White;
                }
                db.Close();
            }
            else if (batPrc == "매각결과-낙찰가")
            {
                var xRows = dg.Rows.Cast<DataGridViewRow>().Where(t => t.Cells["dg_Sta"].Value?.ToString() == "낙찰" && t.Cells["dg_State"].Value?.ToString() == "매각" && t.Cells["dg_SucbAmt"].Value.ToString() != "0");
                if (xRows?.Count() == 0)
                {
                    MessageBox.Show("대상 물건이 없습니다.");
                    return;
                }

                db.Open();
                foreach (DataGridViewRow row in xRows)
                {
                    if (!row.Displayed) dg.FirstDisplayedScrollingRowIndex = row.Index;
                    idx = row.Cells["dg_Ridx"].Value.ToString();
                    tid = row.Cells["dg_Tid"].Value.ToString();
                    bidDt = row.Cells["dg_BidDt"].Value.ToString();
                    sucbAmt = row.Cells["dg_SucbAmt"].Value.ToString().Replace(",", string.Empty);

                    //낙찰가 < 최저가
                    if (Convert.ToDecimal(sucbAmt) < Convert.ToDecimal(row.Cells["dg_MinbAmtT"].Value.ToString().Replace(",", string.Empty)))
                    {
                        errCnt++;
                        row.DefaultCellStyle.BackColor = Color.HotPink;
                        continue;
                    }

                    sql = "update ta_hist set amt=" + sucbAmt + " where tid=" + tid + " and sta=1210 and bid_dt='" + bidDt + "'";
                    db.ExeQry(sql);

                    sql = "update ta_list set sucb_amt=" + sucbAmt + " where tid=" + tid;
                    db.ExeQry(sql);

                    sql = "update db_tank.tx_rpt set prc=2 where idx='" + idx + "'";
                    db.ExeQry(sql);

                    row.DefaultCellStyle.BackColor = Color.White;
                }
                db.Close();
            }

            //sms 발송대상 물건 저장
            if (dicSms.Count > 0)
            {
                db.Open();
                foreach (KeyValuePair<string, string> kvp in dicSms)
                {
                    sql = "insert ignore into db_tank.tx_sms set tid='" + kvp.Key + "', state='" + kvp.Value + "', wdt=curdate(), wtm=curtime()";
                    db.ExeQry(sql);
                }
                db.Close();
            }

            if (errCnt > 0)
            {
                MessageBox.Show($"오류-{errCnt}건 있습니다.");
            }
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            MessageBox.Show("일괄처리 완료");
        }

        /// <summary>
        /// 각 부분별/전체 DB 저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveData(object sender, EventArgs e)
        {
            int seq = 0, dgHCnt = 0, dgTCnt = 0;
            bool dgHValid = true, dgTValid = true;

            string sectDvsn, idx, mode, lsNo, sql, tid, sta1, sta2, spCdtn;
            decimal apslAmt = 0, minbAmt = 0, sucbAmt = 0;

            string state, sucBidr;       //입찰일정

            tid = lnkTid.Text;
            if (tid == string.Empty || tid == "TID")
            {
                MessageBox.Show("선택된 물건이 없습니다.");
                return;
            }

            List<MySqlParameter> sp = new List<MySqlParameter>();
            sectDvsn = ((Button)sender).Text.Replace("저장", string.Empty);

            //값 유효성 체크
            if (sectDvsn == "전체")
            {
                apslAmt = Convert.ToDecimal(txtApslAmt.Text.Replace(",", string.Empty).Trim());
                minbAmt = Convert.ToDecimal(txtMinbAmt.Text.Replace(",", string.Empty).Trim());
                
                if (minbAmt > apslAmt)
                {                    
                    if (MessageBox.Show("최저가 > 감정가 입니다.\r\n적용 하시겠습니까?", "최저가 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No)
                    {
                        return;
                    }
                }
                
                if (string.IsNullOrEmpty(txtSucbAmt.Text.Replace(",", string.Empty).Trim()) == false)
                {
                    sucbAmt = Convert.ToDecimal(txtSucbAmt.Text.Replace(",", string.Empty).Trim());
                    if (sucbAmt > 0 && sucbAmt < minbAmt)
                    {
                        MessageBox.Show("낙찰가 < 최저가 입니다.");
                        return;
                    }
                }                
            }

            dgHCnt = dgH.Rows.Count - 1;
            //dgTCnt = dgT.Rows.Count - 1;

            if (sectDvsn == "전체")
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
            /*
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
            */
            //DB 처리
            db.Open();
            if (sectDvsn == "전체")
            {
                sta2 = cbxState.SelectedValue.ToString();
                sta1 = dtStateCd.Rows.Cast<DataRow>().Where(t => t["sta2_cd"].ToString() == sta2).FirstOrDefault().Field<byte>("sta1_cd").ToString();

                List<string> lstSpCdtn = new List<string>();
                foreach (ListViewItem item in lvSpc.CheckedItems)
                {
                    lstSpCdtn.Add(item.Text.Remove(item.Text.IndexOf(".")));
                }
                spCdtn = string.Join(",", lstSpCdtn.ToArray());

                sql = "update ta_list set sta1=@sta1, sta2=@sta2, apsl_amt=@apsl_amt, minb_amt=@minb_amt, sucb_amt=@sucb_amt, fb_cnt=@fb_cnt, dpst_type=@dpst_type, dpst_rate=@dpst_rate, " +
                    "bid_dt=@bid_dt, end_dt=@end_dt, sucb_dt=@sucb_dt, bid_cnt=@bid_cnt, bid_tm=@bid_tm, bid_tm1=@bid_tm1, bid_tm2=@bid_tm2, bid_tm3=@bid_tm3, sp_cdtn=@sp_cdtn where tid='" + tid + "'";
                sp.Add(new MySqlParameter("@sta1", sta1));
                sp.Add(new MySqlParameter("@sta2", sta2));
                sp.Add(new MySqlParameter("@apsl_amt", apslAmt));
                sp.Add(new MySqlParameter("@minb_amt", minbAmt));
                sp.Add(new MySqlParameter("@sucb_amt", sucbAmt));
                sp.Add(new MySqlParameter("@bid_dt", mtxtBidDt.Text));
                sp.Add(new MySqlParameter("@end_dt", mtxtEndDt.Text));
                sp.Add(new MySqlParameter("@sucb_dt", mtxtSucbDt.Text));
                sp.Add(new MySqlParameter("@bid_cnt", cbxBidCnt.Text));
                sp.Add(new MySqlParameter("@bid_tm", mtxtBidTm.Text + ":00"));
                sp.Add(new MySqlParameter("@bid_tm1", mtxtBidTm1.Text + ":00"));
                sp.Add(new MySqlParameter("@bid_tm2", mtxtBidTm2.Text + ":00"));
                sp.Add(new MySqlParameter("@bid_tm3", mtxtBidTm3.Text + ":00"));
                sp.Add(new MySqlParameter("@fb_cnt", txtFbCnt.Text.Trim()));
                sp.Add(new MySqlParameter("@dpst_type", cbxDpstType.SelectedValue));
                sp.Add(new MySqlParameter("@dpst_rate", cbxDpstRate.Text));
                sp.Add(new MySqlParameter("@sp_cdtn", spCdtn));
                db.ExeQry(sql, sp);
                sp.Clear();

                sql = "update ta_dtl set leas_note=@leas_note, attn_note1=@attn_note1, attn_note2=@attn_note2, etc_note=@etc_note where tid='" + tid + "'"; 
                sp.Add(new MySqlParameter("@leas_note", txtLeasNote.Text.Trim()));
                sp.Add(new MySqlParameter("@attn_note1", txtAttnNote1.Text.Trim()));
                sp.Add(new MySqlParameter("@attn_note2", txtAttnNote2.Text.Trim()));
                sp.Add(new MySqlParameter("@etc_note", txtEtcNote.Text.Trim()));
                db.ExeQry(sql, sp);
                sp.Clear();

                newSTA1 = sta1;
                newSTA2 = sta2;
            }

            if (sectDvsn == "전체")
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
                    
                    //seq 때문에 모두 저장
                    state = row.Cells["dgH_State"].Value?.ToString() ?? string.Empty;
                    idx = row.Cells["dgH_Idx"].Value?.ToString() ?? string.Empty;
                    sucBidr = row.Cells["dgH_SucBidr"].Value?.ToString() ?? "";
                    if (sucBidr.Contains("del"))
                    {
                        sql = "delete from ta_hist where idx=" + idx;
                        db.ExeQry(sql);
                    }
                    else
                    {
                        mode = (idx == string.Empty) ? "insert into" : "update";
                        sql = mode + " ta_hist set tid=@tid, seq=@seq, bid_dt=@bid_dt, bid_tm=@bid_tm, sta=@sta, amt=@amt, bidr_cnt=@bidr_cnt, sucb_nm=@sucb_nm, sucb_area=@sucb_area, 2nd_reg=@2nd_reg, pri_reg=@pri_reg";
                        if (mode == "update") sql += " where idx=" + idx;
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@seq", seq));
                        //sp.Add(new MySqlParameter("@bid_dt", row.Cells["dgH_BidDt"].Value?.ToString() ?? ""));
                        //sp.Add(new MySqlParameter("@bid_tm", row.Cells["dgH_BidTm"].Value?.ToString() + ":00" ?? ""));
                        sp.Add(new MySqlParameter("@bid_dt", getDateParse(row.Cells["dgH_BidDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@bid_tm", getTimeParse(row.Cells["dgH_BidTm"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@sta", row.Cells["dgH_State"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@amt", row.Cells["dgH_Amt"].Value?.ToString().Replace(",", string.Empty) ?? ""));
                        sp.Add(new MySqlParameter("@bidr_cnt", row.Cells["dgH_BidrCnt"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@sucb_nm", sucBidr));
                        sp.Add(new MySqlParameter("@sucb_area", row.Cells["dgH_Area"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@2nd_reg", ((DataGridViewCheckBoxCell)row.Cells["dgH_2ndReg"]).Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@pri_reg", row.Cells["dgH_PriReg"].Value?.ToString() ?? ""));
                        db.ExeQry(sql, sp);
                        sp.Clear();

                        if (state == "1111") fbCnt++;
                    }   
                }

                //[자동수정]버튼이 아닌 경우 물건 기본정보 관련 처리
                if (dgHCnt >= 1 && txtRptIdx.Text == string.Empty)
                {
                    int rowIdx = dgHCnt - 1;
                    DataGridViewRow row = dgH.Rows[rowIdx];
                    string hSta = row.Cells["dgH_State"].Value?.ToString() ?? string.Empty;
                    //string hDt = row.Cells["dgH_BidDt"].Value?.ToString() ?? "";
                    //string hTm = row.Cells["dgH_BidTm"].Value?.ToString() + ":00" ?? "";
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
                            //if (hSta == "1110" && (preSta == "1110" || preSta == "1111") && hDt == dgH.Rows[rowIdx - 1].Cells["dgH_BidDt"].Value.ToString())
                            if (hSta == "1110" && (preSta == "1110" || preSta == "1111") && hDt == getDateParse(dgH.Rows[rowIdx - 1].Cells["dgH_BidDt"].Value?.ToString() ?? ""))
                            {
                                bidCnt = 2;
                                //bidTm1 = dgH.Rows[rowIdx - 1].Cells["dgH_BidTm"].Value?.ToString() + ":00" ?? "";
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
            /*
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
                        sql = mode + " ta_leas set tid=@tid, ls_no=@ls_no, prsn=@prsn, inv_type=@inv_type, part=@part, use_cd=@use_cd, shop_nm=@shop_nm, term=@term, deposit=@deposite, m_money=@m_money, t_money=@t_money, t_mnth=@t_mnth, mv_dt=@mv_dt, fx_dt=@fx_dt, shr_dt=@shr_dt, biz=@biz, note=@note";
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
                        sp.Add(new MySqlParameter("@mv_dt", getDateParse(row.Cells["dgT_MvDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@fx_dt", getDateParse(row.Cells["dgT_FxDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@shr_dt", getDateParse(row.Cells["dgT_ShrDt"].Value?.ToString() ?? "")));
                        sp.Add(new MySqlParameter("@note", row.Cells["dgT_Note"].Value?.ToString() ?? ""));
                        sp.Add(new MySqlParameter("@biz", ((DataGridViewCheckBoxCell)row.Cells["dgT_ChkBiz"]).Value?.ToString() ?? ""));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }
                }
            }
            */
            sql = "update db_tank.tx_rpt set prc=3 where tid=" + tid + " and idx=" + dg.CurrentRow.Cells["dg_Ridx"].Value.ToString();
            db.ExeQry(sql);
            db.Close();

            MessageBox.Show("저장되었습니다.");
            dg.CurrentRow.DefaultCellStyle.BackColor = Color.White;

            //sms 발송대상 물건 저장
            string staMsg = string.Empty;
            if (STA1 != string.Empty && newSTA1 != string.Empty && STA1 != newSTA1)
            {
                if (newSTA1 == "11") staMsg = "진행";
                else if (newSTA1 == "12") staMsg = "매각";
                else if (newSTA1 == "13") staMsg = "미진행";
                else if (newSTA1 == "14")
                {
                    if (newSTA2 == "1416") staMsg = "모사건종결";
                    else staMsg = "종국";
                }

                if (staMsg != string.Empty)
                {
                    sql = "insert ignore into db_tank.tx_sms set tid='" + tid + "', state='" + staMsg + "', wdt=curdate(), wtm=curtime()";
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();
                }
            }
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

            foreach (Control ctrl in tblNote.Controls)
            {
                if (ctrl.GetType() == typeof(TextBox))
                {
                    TextBox mTxt = (TextBox)ctrl;
                    selTxt = mTxt.SelectedText.Trim();
                    if (selTxt != string.Empty)
                    {
                        btnImptNew_Click(null, null);
                        txtImptNote.Text = $"{selTxt}";
                        break;
                    }
                }
            }

            selTxt = txtLeasNote.SelectedText.Trim();
            if (selTxt != string.Empty)
            {
                btnImptNew_Click(null, null);                
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
        /// 날짜 형식 변환
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private string getDateParse(string str)
        {
            string dt = string.Empty;

            str = str.Replace(" ", string.Empty).Trim();

            Match m = Regex.Match(str, @"(\d{4})[.년\-](\d+)[.월\-](\d+)[.일]*", rxOptM);
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
    }
}
