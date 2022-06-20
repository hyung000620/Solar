using MySql.Data.MySqlClient;
using Solar.CA;
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

namespace Solar.Auto
{
    public partial class wfRgstAuto : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        NetUtil net = new NetUtil();
        AuctCd auctCd = new AuctCd();

        DataTable dtFileCd, dtStaff;

        Dictionary<int, string> dicWorkDvsn = new Dictionary<int, string>();

        string cdtn = "";
        decimal totRowCnt = 0;

        string myWeb = Properties.Settings.Default.myWeb;
        Timer timer = new Timer();

        public wfRgstAuto()
        {
            InitializeComponent();
            init();
        }

        private void init()
        {
            ui.DgSetRead(dg, 0);
            ui.SetPagn(panPagn);

            //파일 구분
            dtFileCd = db.ExeDt("select cd, nm from ta_cd_file order by cd");

            //작업 구분
            dicWorkDvsn.Add(99, "-선택-");
            dicWorkDvsn.Add(1, "▣ 경매 전체");
            dicWorkDvsn.Add(2, "● 공매 전체");
            dicWorkDvsn.Add(10, "경매-수동");
            dicWorkDvsn.Add(11, "경매-일반");
            dicWorkDvsn.Add(12, "경매-선행");
            dicWorkDvsn.Add(13, "경매-변동");
            dicWorkDvsn.Add(14, "경매-예정");
            dicWorkDvsn.Add(20, "공매-수동");
            dicWorkDvsn.Add(21, "공매-캠코");
            dicWorkDvsn.Add(22, "공매-기관");
            dicWorkDvsn.Add(23, "공매-변동");
            dicWorkDvsn.Add(0, "기타");

            cbxWorkDvsn.DataSource = new BindingSource(dicWorkDvsn, null);
            cbxWorkDvsn.DisplayMember = "Value";
            cbxWorkDvsn.ValueMember = "Key";
            cbxWorkDvsn.SelectedValue = 99;

            //관리자명-수동 등록자
            dtStaff = db.ExeDt("select id, name from db_tank.tz_staff order by name");

            timer.Interval = 10000;  //화면갱신 10초
            timer.Tick += new EventHandler(timer_Tick);
        }

        private void btnSrch_Click(object sender, EventArgs e)
        {
            string sql;

            cdtn = "1";
            dg.Rows.Clear();
            chkRefresh.CheckedChanged -= chkRefresh_CheckedChanged;

            List<string> cdtnList = new List<string>();

            if (cbxWorkDvsn.SelectedIndex > 0)
            {
                if (cbxWorkDvsn.SelectedValue.ToString() == "1")
                {
                    cdtnList.Add("(dvsn between 10 and 14)");
                }
                else if (cbxWorkDvsn.SelectedValue.ToString() == "2")
                {
                    cdtnList.Add("(dvsn between 20 and 23)");
                }
                else
                {
                    cdtnList.Add($"dvsn='{cbxWorkDvsn.SelectedValue}'");
                }                
            }
            if (cbxMdfy.SelectedIndex > 0)
            {
                cdtnList.Add("err_cd=20");                
                if (cbxMdfy.Text == "발급완료") cdtnList.Add("ul=1");
                else cdtnList.Add("ul=0");
            }
            if (dtpWdtBgn.Checked) cdtnList.Add($"wdt >= '{dtpWdtBgn.Value.ToShortDateString()}'");
            if (dtpWdtEnd.Checked) cdtnList.Add($"wdt <= '{dtpWdtEnd.Value.ToShortDateString()}'");
            if (dtpIdtBgn.Checked) cdtnList.Add($"idtm >= '{dtpIdtBgn.Value.ToShortDateString()} 00:00:00'");
            if (dtpIdtEnd.Checked) cdtnList.Add($"idtm <= '{dtpIdtEnd.Value.ToShortDateString()} 23:59:59'");
            if (chkSrchMe.Checked) cdtnList.Add($"staff='{Properties.Settings.Default.USR_ID}'");
            if (chkSrchErr.Checked) cdtnList.Add("dvsn > 0 and err_cd > 0 and idtm < '2000-01-01 00:00:00'");

            txtSrchTid.Text = txtSrchTid.Text.Replace("_", string.Empty).Trim();            
            if (txtSrchTid.Text.Trim() != "")
            {
                cdtnList.Add("tid IN (" + Regex.Replace(txtSrchTid.Text.Trim(), @"\D+", ",") + ")");
            }
            txtSrchPIN.Text = txtSrchPIN.Text.Replace("-", string.Empty).Trim();
            if (txtSrchPIN.Text != "")
            {
                cdtnList.Add($"pin='{txtSrchPIN.Text}'");
            }

            if (cdtnList.Count > 0) cdtn += " and " + string.Join(" and ", cdtnList.ToArray());

            sql = "select COUNT(*) from db_tank.tx_rgst_auto where " + cdtn;

            db.Open();
            totRowCnt = (decimal)((Int64)db.RowCnt(sql));
            db.Close();

            ComboBox cbx = (ComboBox)panPagn.Controls["_cbxPagn"];
            cbx.SelectedIndexChanged -= gotoPageList;
            ui.InitPagn(panPagn, totRowCnt);
            cbx.SelectedIndexChanged += gotoPageList;
            if (cbx.Items.Count > 0) cbx.SelectedIndex = 0;

            chkRefresh.CheckedChanged += chkRefresh_CheckedChanged;
            if(chkRefresh.Checked) timer.Start();
        }

        private void gotoPageList(object sender, EventArgs e)
        {
            int i = 0, dvsn;
            decimal startRow = 0;
            string sql = "", order = "";
            string saNo, csCd, dpt, state, cat, dpsl;
            int stateCntAll = 0, stateCntSuc = 0, stateCntWait = 0, stateCntFail = 0;

            dg.Rows.Clear();

            ComboBox cbxPage = (ComboBox)sender;
            NumericUpDown listScale = (NumericUpDown)panPagn.Controls["_nudList"];
            startRow = (decimal.Parse(cbxPage.Text) - 1) * listScale.Value;
            //dg.SelectionChanged -= dg_SelectionChanged;

            order = "wdt desc, wtm desc, idx desc";
            sql = "select * from db_tank.tx_rgst_auto";
            sql += " where " + cdtn + " order by " + order + " limit " + startRow + "," + listScale.Value;

            this.Cursor = Cursors.WaitCursor;
            DataTable tmpDt = db.ExeDt(sql);
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                dvsn = Convert.ToInt32(dr["dvsn"]);

                i = dg.Rows.Add();
                dg["dg_No", i].Value = totRowCnt - startRow - i;
                dg["dg_WorkDvsn", i].Value = dicWorkDvsn[dvsn];
                dg["dg_Tid", i].Value = dr["tid"];
                dg["dg_Pin", i].Value = dr["pin"];
                dg["dg_LsType", i].Value = dr["ls_type"];
                dg["dg_LsNo", i].Value = dr["ls_no"];
                dg["dg_Wdtm", i].Value = $"{dr["wdt"]:MM.dd} {dr["wtm"]}";
                dg["dg_Pay", i].Value = ($"{dr["pay"]}" == "1") ? "○" : "";
                //dg["dg_Rdtm", i].Value = ($"{dr["rdtm"]}".Contains("0001")) ? "-" : $"{dr["rdtm"]:HH:mm:ss}";
                dg["dg_Idtm", i].Value = ($"{dr["idtm"]}".Contains("0001")) ? "-" : $"{dr["idtm"]:MM.dd HH:mm:ss}";
                dg["dg_UL", i].Value = ($"{dr["ul"]}" == "1") ? "○" : "";
                dg["dg_Msg", i].Value = dr["msg"];
                dg["dg_NoExtr", i].Value = ($"{dr["no_extr"]}" == "1") ? "1" : "";
                dg["dg_Analy", i].Value = dr["analy"];
                dg["dg_Idx", i].Value = dr["idx"];
                if (dr["staff"].ToString() != String.Empty)
                {
                    dg["dg_Staff", i].Value = dtStaff.Select($"id='{dr["staff"]}'")[0]["name"];
                }

                //발급완료건
                if ($"{dg["dg_Idtm", i].Value}" != "-")
                {
                    if ($"{dr["ul"]}" == "1") dg.Rows[i].DefaultCellStyle.BackColor = Color.LightGray;  //업로드 완료
                    else dg.Rows[i].DefaultCellStyle.BackColor = Color.LightPink;   //업로드 실패
                }

                //오류건
                if ($"{dr["dvsn"]}" != "0" && $"{dr["err_cd"]}" != "0" && $"{dr["idtm"]}".Contains("0001"))
                {
                    dg.Rows[i].DefaultCellStyle.BackColor = Color.LightGoldenrodYellow;   //오류건
                }

                //경매건
                if (dvsn > 0 && dvsn < 20)
                {
                    dg.Rows[i].Cells["dg_Tid"].Style.ForeColor = Color.Blue;
                }
            }
            dr.Close();
            db.Close();
            dg.ClearSelection();
            this.Cursor = Cursors.Default;
            //dg.SelectionChanged += dg_SelectionChanged;

            DataTable dt = db.ExeDt($"select * from db_tank.tx_rgst_auto where {cdtn} and dvsn > 0");
            if (dt.Rows.Count > 0)
            {
                stateCntAll = dt.Rows.Count;
                stateCntSuc = dt.Select("idtm > '2000-01-01 00:00:00' or ul=1").Count();
                stateCntFail = dt.Select("idtm < '2000-01-01 00:00:00' and err_cd > 0").Count();
                stateCntWait = dt.Select("idtm < '2000-01-01 00:00:00' and err_cd = 0 and ul=0").Count();
            }
            lblStateCntAll.Text = $"{stateCntAll}";
            lblStateCntSuc.Text = $"{stateCntSuc}";
            lblStateCntWait.Text = $"{stateCntWait}";
            lblStateCntFail.Text = $"{stateCntFail}";
        }

        private void dg_SelectionChanged(object sender, EventArgs e)
        {
            int i = 0, n = 0;
            string dvsn, tid, tbl, spt, sn1, sn2, sn, lsType, rgstCtgr, url, sql;
            
            if (dg.CurrentRow == null) return;
            i = dg.CurrentRow.Index;
            dvsn = dg["dg_WorkDvsn", i].Value.ToString();
            tid = dg["dg_Tid", i].Value.ToString();
            lsType = dg["dg_LsType", i].Value.ToString();
            if (dg["dg_UL", i].Value.ToString() == String.Empty) return;
            
            Clipboard.SetText(tid);

            if (dvsn.Contains("경매"))
            {
                rgstCtgr = (lsType == "토지") ? "DA" : "DB";
                sql = $"select * from ta_list where tid={tid} limit 1";
                db.Open();
                MySqlDataReader dr = db.ExeRdr(sql);
                dr.Read();
                spt = dr["spt"].ToString();
                sn1 = dr["sn1"].ToString();
                dr.Close();
                db.Close();
                tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";

                sql = $"select * from {tbl} where ctgr='{rgstCtgr}' and tid={tid} limit 1";
                db.Open();
                dr = db.ExeRdr(sql);
                if (dr.HasRows)
                {
                    dr.Read();
                    url = $"{myWeb}FILE/CA/{rgstCtgr}/{spt}/{sn1}/{dr["file"]}";
                    axAcroPDF1.src = url;
                }
                dr.Close();
                db.Close();
            }
            else
            {
                rgstCtgr = (lsType == "토지") ? "I" : "J";
                string dirNo = (Math.Ceiling(Convert.ToDecimal(tid) / 100000) * 100000).ToString().PadLeft(7, '0');
                url = $"{myWeb}FILE/PA/{rgstCtgr}/{dirNo}/{rgstCtgr}{tid}.pdf";
                axAcroPDF1.src = url;
            }

            //dgF.ClearSelection();
        }

        /// <summary>
        /// Timer Tick
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timer_Tick(object sender, EventArgs e)
        {
            btnSrch_Click(sender, e);
        }

        private void dg_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int i = 0, n = 0;
            string dvsn, tid, tbl, spt, sn1, sn2, sn, lsType, rgstCtgr, url, sql, pin;

            if (dg.CurrentRow == null) return;
            i = dg.CurrentRow.Index;
            dvsn = dg["dg_WorkDvsn", i].Value.ToString();
            tid = dg["dg_Tid", i].Value.ToString();
            pin = dg["dg_Pin", i].Value.ToString();
            lsType = dg["dg_LsType", i].Value.ToString();
            //if (dg["dg_UL", i].Value.ToString() == String.Empty) return;

            if (dg.Columns[e.ColumnIndex].Name == "dg_Tid" && dvsn.Contains("경매"))
            {
                //경매 물건창 연동
                wfCaMgmt caMgmt = new wfCaMgmt() { Owner = this };
                caMgmt.StartPosition = FormStartPosition.CenterScreen;
                caMgmt.FormBorderStyle = FormBorderStyle.FixedToolWindow;
                caMgmt.txtSrchTid.Text = tid;
                caMgmt.btnSrch_Click(null, null);
                caMgmt.Show();
            }
            else
            {
                if (dg["dg_UL", i].Value.ToString() != String.Empty)
                {
                    if (dvsn.Contains("경매"))
                    {
                        rgstCtgr = (lsType == "토지") ? "DA" : "DB";
                        sql = $"select * from ta_list where tid={tid} limit 1";
                        db.Open();
                        MySqlDataReader dr = db.ExeRdr(sql);
                        dr.Read();
                        spt = dr["spt"].ToString();
                        sn1 = dr["sn1"].ToString();
                        dr.Close();
                        db.Close();
                        tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";

                        sql = $"select * from {tbl} where ctgr='{rgstCtgr}' and tid={tid} limit 1";
                        db.Open();
                        dr = db.ExeRdr(sql);
                        if (dr.HasRows)
                        {
                            dr.Read();
                            url = $"{myWeb}FILE/CA/{rgstCtgr}/{spt}/{sn1}/{dr["file"]}";
                            axAcroPDF1.src = url;
                        }
                        dr.Close();
                        db.Close();
                    }
                    else
                    {
                        rgstCtgr = (lsType == "토지") ? "I" : "J";
                        string dirNo = (Math.Ceiling(Convert.ToDecimal(tid) / 100000) * 100000).ToString().PadLeft(7, '0');
                        url = $"{myWeb}FILE/PA/{rgstCtgr}/{dirNo}/{rgstCtgr}{tid}.pdf";
                        axAcroPDF1.src = url;
                    }
                }                
            }

            if (dg.Columns[e.ColumnIndex].Name == "dg_Tid")
            {
                Clipboard.SetText(tid);
            }
            else
            {
                Clipboard.SetText(pin);
            }
        }

        /// <summary>
        /// 화면 갱신
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void chkRefresh_CheckedChanged(object sender, EventArgs e)
        {
            if(chkRefresh.Checked) timer.Start();
            else timer.Stop();
        }
    }
}
