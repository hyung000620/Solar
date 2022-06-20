using MySql.Data.MySqlClient;
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

namespace Solar.CA
{
    public partial class wfDpslStmtCmp : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        AuctCd auctCd = new AuctCd();
        NetUtil net = new NetUtil();

        string css = @"<style type=""text/css"">
                        table,div{font-family:돋움;font-size:12px;margin-bottom:10px;line-height:18px}
                        caption,p,.jisi_etc{display:none}
                        .tbl_grid{width:100%;border-top:1px solid #eee;border-left:1px solid #eee;padding:0;border-spacing:0;border-collapse:collapse}
                        .tbl_grid th, .tbl_grid td{border-bottom:1px solid #eee;border-right:1px solid #eee;padding:5px 5px 0 5px;font-weight:normal;height:20px;text-align:left;word-break:break-all}
                        .tbl_grid th{background-color:#f8f8f8;text-align:center}
                        .table_contents{margin-bottom:5px}
                        .green{background-color:#19b723;color:#fff}
                        .blue{background-color:#0086e6;color:#fff}
                    </style>";

        //정규식 기본형태
        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        //RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        public wfDpslStmtCmp()
        {
            InitializeComponent();

            ui.DgSetRead(dg);
            ui.DgSetRead(dgImpt);

            init();
        }

        private void init()
        {
            lblOldDt.Text = string.Empty;
            lblNewDt.Text = string.Empty;

            lnkTID.Text = string.Empty;

            wbrOld.Navigate("about:blank");
            wbrNew.Navigate("about:blank");
        }

        private void btnSrch_Click(object sender, EventArgs e)
        {
            int i, n = 0;
            string sql;

            init();
            dg.SelectionChanged -= dg_SelectionChanged;
            dg.Rows.Clear();
            dgImpt.Rows.Clear();

            sql = "select L.tid, spt, sn1, sn2, pn, old, new from db_main.ta_list L , db_tank.ta_dpsl_cmp C where L.tid=C.tid and C.wdt='" + dtpSrchDt.Value.ToShortDateString() + "'";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                n++;
                i = dg.Rows.Add();
                dg["dg_NO", i].Value = i + 1;
                dg["dg_Tid", i].Value = dr["tid"];
                dg["dg_SaNo", i].Value = (dr["pn"].ToString() == "0") ? string.Format("{0}-{1}", dr["sn1"], dr["sn2"]) : string.Format("{0}-{1}({2})", dr["sn1"], dr["sn2"], dr["pn"]);
                dg["dg_Spt", i].Value = auctCd.FindCsNm(dr["spt"].ToString());
            }
            dr.Close();
            db.Close();
            dg.ClearSelection();
            btnSrch.Focus();

            if (n == 0)
            {
                MessageBox.Show("검색된 사건이 없습니다.");
                return;
            }

            dg.SelectionChanged += dg_SelectionChanged;
        }

        private void dg_SelectionChanged(object sender, EventArgs e)
        {
            int i;
            string sql, tid, oldDoc, newDoc, oldDt, newDt;
            string section1, section2, section3, section4, section5;

            init();
            ui.FormClear(tbc);

            if (dg.CurrentRow == null) return;

            i = dg.CurrentRow.Index;
            tid = dg["dg_Tid", i].Value.ToString();
            lnkTID.Text = tid;

            sql = "select * from db_tank.ta_dpsl_cmp where tid=" + tid;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            oldDoc = dr["old"].ToString();
            newDoc = dr["new"].ToString();
            oldDt = String.Format("{0:yyyy년 MM월 dd일 (ddd)}", dr["old_dt"]);
            newDt = String.Format("{0:yyyy년 MM월 dd일 (ddd)}", dr["new_dt"]);
            dr.Close();
            db.Close();

            oldDoc = Regex.Replace(oldDoc, "(Ltbl_dt)|(Ltbl_list)", "tbl_grid");
            newDoc = Regex.Replace(newDoc, "(Ltbl_dt)|(Ltbl_list)", "tbl_grid");

            oldDoc = Regex.Replace(oldDoc, @"[\r\n\t]", string.Empty);
            newDoc = Regex.Replace(newDoc, @"[\r\n\t]", string.Empty);

            MatchCollection mcOld = Regex.Matches(oldDoc, @"<table.*?</table>", RegexOptions.IgnoreCase);
            MatchCollection mcNew = Regex.Matches(newDoc, @"<table.*?</table>", RegexOptions.IgnoreCase);

            if (mcOld.Count == 5 && mcNew.Count == 5)
            {
                try
                {
                    section1 = compare_fix(mcOld[0].Value, mcNew[0].Value);
                    section2 = compare_list(mcOld[1].Value, mcNew[1].Value);
                    section3 = compare_fix(mcOld[2].Value, mcNew[2].Value);
                    section4 = compare_fix(mcOld[3].Value, mcNew[3].Value);
                    section5 = compare_fix(mcOld[4].Value, mcNew[4].Value);
                    newDoc = section1 + section2 + section3 + section4 + section5;

                    section1 = mcOld[0].Value;
                    section2 = compare_list2(mcOld[1].Value, mcNew[1].Value);
                    section3 = mcOld[2].Value;
                    section4 = mcOld[3].Value;
                    section5 = mcOld[4].Value;
                    oldDoc = section1 + section2 + section3 + section4 + section5;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("※ 비교 오류\r\n\r\n" + ex.Message);
                }
                finally
                {
                    //
                }
            }

            lblOldDt.Text = "과거 >  " + oldDt;
            lblNewDt.Text = "현재 >  " + newDt;
            wbrOld.DocumentText = css + oldDoc;
            wbrNew.DocumentText = css + newDoc;

            LoadImptHist(tid);
        }

        private void LoadImptHist(string tid)
        {
            int i = 0;
            string sql;

            dgImpt.Rows.Clear();

            sql = "select * from ta_impt_rec where tid=" + tid + " order by idx desc";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                i = dgImpt.Rows.Add();
                dgImpt["dgImpt_No", i].Value = i + 1;
                dgImpt["dgImpt_Note", i].Value = dr["note"];
                dgImpt["dgImpt_Idx", i].Value = dr["idx"];
                dgImpt["dgImpt_Wdt", i].Value = string.Format("{0:yyyy.MM.dd}", dr["wdt"]);
            }
            dr.Close();
            db.Close();
            dgImpt.ClearSelection();
        }

        private string compare_fix(string old_section, string new_section)
        {
            int i = 0, cnt = 0;
            string html = string.Empty;

            string[] old_arr = Regex.Split(old_section, @"(</th>)|(</td>)", RegexOptions.IgnoreCase);
            string[] new_arr = Regex.Split(new_section, @"(</th>)|(</td>)", RegexOptions.IgnoreCase);

            cnt = new_arr.Length;

            for (i = 0; i < cnt; i++)
            {
                if (new_arr[i] != old_arr[i])
                {
                    new_arr[i] = Regex.Replace(new_arr[i], @"<td", @"<td class='green'") + "</td>";
                }
            }

            html = String.Join("", new_arr);

            return html;
        }

        /// <summary>
        /// new -> old
        /// </summary>
        /// <param name="old_section"></param>
        /// <param name="new_section"></param>
        /// <returns></returns>
        private string compare_list(string old_section, string new_section)
        {
            bool exist = false;
            int i = 0, j = 0, old_cnt = 0, new_cnt = 0;
            string html = string.Empty, old_txt = string.Empty, new_txt = string.Empty, old_bigo = string.Empty, new_bigo = string.Empty;

            string[] old_arr = Regex.Split(old_section, @"<td rowspan", RegexOptions.IgnoreCase);
            string[] new_arr = Regex.Split(new_section, @"<td rowspan", RegexOptions.IgnoreCase);

            old_cnt = old_arr.Length;
            new_cnt = new_arr.Length;
            for (i = 1; i < new_cnt; i++)
            {
                exist = false;
                new_txt = Regex.Replace(new_arr[i], @"[\r\n\s\t]", string.Empty);
                new_txt = Regex.Replace(new_txt, @"<tdcolspan=""10"".*", string.Empty).Trim();
                for (j = 1; j < old_cnt; j++)
                {
                    old_txt = Regex.Replace(old_arr[j], @"[\r\n\s\t]", string.Empty).Trim();
                    old_txt = Regex.Replace(old_txt, @"<tdcolspan=""10"".*", string.Empty).Trim();

                    if (old_txt == new_txt)
                    {
                        exist = true;
                        break;
                    }
                }

                if (exist == true)
                {
                    new_arr[i] = @"<td rowspan" + new_arr[i];
                }
                else
                {
                    new_arr[i] = @"<td class='green' rowspan" + new_arr[i];
                }
            }

            html = String.Join("", new_arr);

            old_bigo = Regex.Match(old_section, @"<td colspan=""10"" class=""txtleft"">.*", RegexOptions.IgnoreCase).Value;
            new_bigo = Regex.Match(new_section, @"<td colspan=""10"" class=""txtleft"">.*", RegexOptions.IgnoreCase).Value;

            //MessageBox.Show(old_bigo + "\r\n" + new_bigo);

            if (old_bigo != new_bigo)
            {
                html = Regex.Replace(html, @"<td colspan", @"<td class='green' colspan", RegexOptions.IgnoreCase);
            }

            //html = "<div style='margin-bottom:19px'></div>" + html;

            return html;
        }

        /// <summary>
        /// old -> new (임차인 누락건)
        /// </summary>
        /// <param name="p"></param>
        /// <param name="p_2"></param>
        /// <returns></returns>
        private string compare_list2(string old_section, string new_section)
        {
            string html = string.Empty, repl_str = string.Empty, val1 = string.Empty, val2 = string.Empty;

            MatchCollection mc = Regex.Matches(old_section, @"<td rowspan=""\d"">.*?</td>", RegexOptions.IgnoreCase);
            foreach (Match m in mc)
            {
                //MessageBox.Show(m.Value);
                val1 = Regex.Replace(m.Value, @"=""1""", @"=""2""").Replace(@"(", @"\(").Replace(@")", @"\)");
                val2 = Regex.Replace(m.Value, @"=""2""", @"=""1""").Replace(@"(", @"\(").Replace(@")", @"\)");
                //MessageBox.Show(val1);
                if (Regex.IsMatch(new_section, val1)) continue;
                else if (Regex.IsMatch(new_section, val2)) continue;
                else
                {
                    repl_str = Regex.Replace(m.Value, "<td", "<td class='blue'");
                    old_section = Regex.Replace(old_section, m.Value, repl_str, RegexOptions.IgnoreCase);
                }
            }

            html = old_section;

            return html;
        }

        /// <summary>
        /// 물건 수정창 열기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lnkTID_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            //wfCaMgmt caMgmt = new wfCaMgmt();
            wfCaMgmt caMgmt=new wfCaMgmt() { Owner = this };
            caMgmt.StartPosition = FormStartPosition.CenterScreen;
            caMgmt.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            caMgmt.txtSrchTid.Text = this.lnkTID.Text;
            caMgmt.btnSrch_Click(null, null);
            //caMgmt.ShowDialog();
            //caMgmt.Dispose();
            caMgmt.Show();
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
        }

        /// <summary>
        /// 물건 주요 변동내역내역-삭제
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnImptDel_Click(object sender, EventArgs e)
        {
            string hisIdx, sql;

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

            LoadImptHist(lnkTID.Text);
        }

        /// <summary>
        /// 물건 주요 변동내역-내용 보기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgImpt_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int rowIdx = 0;

            rowIdx = e.RowIndex;
            txtImptIdx.Text = dgImpt["dgImpt_Idx", rowIdx].Value.ToString();
            txtImptNote.Text = dgImpt["dgImpt_Note", rowIdx].Value.ToString();
        }

        /// <summary>
        /// 물건 주요 변동내역-저장
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnImptSave_Click(object sender, EventArgs e)
        {
            string tid, hisIdx, hisNote, sql;

            tid = lnkTID.Text;
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

            sql = "insert into ta_impt_rec set idx='" + hisIdx + "', tid='" + tid + "', note='" + hisNote + "', wdt=curdate() ON DUPLICATE KEY UPDATE note='" + hisNote + "'";
            db.Open();
            db.ExeQry(sql);
            db.Close();

            MessageBox.Show("저장 되었습니다.");
            btnImptNew_Click(null, null);

            LoadImptHist(tid);
        }
    }
}
