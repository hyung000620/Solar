using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Solar.Comn
{
    public partial class wfRailRoad : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();

        DataTable dtLocalCd, dtLineCd;

        public wfRailRoad()
        {
            InitializeComponent();
            init();
        }

        private void init()
        {
            ui.DgSetRead(dg, 0);
            ui.DgSetRead(dgR, 0);

            //지역 코드
            dtLocalCd = db.ExeDt("select local_cd, line_cd, local_nm, line_nm from tx_railroad group by local_cd, line_cd order by local_cd, line_cd");
            DataRow row = dtLocalCd.NewRow();
            row["local_cd"] = 0;
            row["local_nm"] = "-선택-";
            row["line_cd"] = 0;
            row["line_nm"] = "-선택-";
            dtLocalCd.Rows.InsertAt(row, 0);
            cbxSrchLocal.DataSource = dtLocalCd.Rows.Cast<DataRow>().GroupBy(g => g.Field<byte>("local_cd")).Select(t => t.First()).CopyToDataTable();
            cbxSrchLocal.DisplayMember = "local_nm";
            cbxSrchLocal.ValueMember = "local_cd";
            cbxSrchLocal.SelectedIndexChanged += (s, e) =>
            {
                DataTable dt = dtLocalCd.Rows.Cast<DataRow>().Where(t => t["local_cd"].ToString() == cbxSrchLocal.SelectedValue.ToString()).CopyToDataTable();
                if (cbxSrchLocal.SelectedIndex > 0)
                {
                    row = dt.NewRow();
                    row["line_cd"] = 0;
                    row["line_nm"] = "-선택-";
                    dt.Rows.InsertAt(row, 0);
                }
                cbxSrchLine.DataSource = dt;
                cbxSrchLine.DisplayMember = "line_nm";
                cbxSrchLine.ValueMember = "line_cd";
            };
            cbxSrchLocal.SelectedValue = 0;

            cbxLocal.DataSource = dtLocalCd.Rows.Cast<DataRow>().GroupBy(g => g.Field<byte>("local_cd")).Select(t => t.First()).CopyToDataTable();
            cbxLocal.DisplayMember = "local_nm";
            cbxLocal.ValueMember = "local_cd";
            cbxLocal.SelectedIndexChanged += (s, e) =>
            {
                DataTable dt = dtLocalCd.Rows.Cast<DataRow>().Where(t => t["local_cd"].ToString() == cbxLocal.SelectedValue.ToString()).CopyToDataTable();
                if (cbxLocal.SelectedIndex > 0)
                {
                    row = dt.NewRow();
                    row["line_cd"] = 0;
                    row["line_nm"] = "-선택-";
                    dt.Rows.InsertAt(row, 0);
                }
                cbxLine.DataSource = dt;
                cbxLine.DisplayMember = "line_nm";
                cbxLine.ValueMember = "line_cd";
            };
        }

        private void wbr_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            HtmlDocument html = wbr.Document;
            if (html.GetElementById("map") != null)
            {
                html.GetElementById("map").Click += (s, ev) =>
                {
                    HtmlDocument doc = wbr.Document;
                    txtCoordX.Text = doc.GetElementById("x").GetAttribute("value");
                    txtCoordY.Text = doc.GetElementById("y").GetAttribute("value");
                };
            }
        }

        private void btnSrch_Click(object sender, EventArgs e)
        {
            int i = 0;
            string cdtn, sql;

            cdtn = "1";
            dg.Rows.Clear();
            dg.SelectionChanged -= dg_SelectionChanged;

            List<string> cdtnList = new List<string>();

            if (cbxSrchLocal.SelectedIndex > 0) cdtnList.Add($"local_cd={cbxSrchLocal.SelectedValue}");
            if (cbxSrchLine.SelectedIndex > 0) cdtnList.Add($"line_cd={cbxSrchLine.SelectedValue}");

            if (cdtnList.Count > 0) cdtn = string.Join(" and ", cdtnList.ToArray());

            sql = $"select * from tx_railroad where {cdtn} order by local_cd, line_cd, station_cd";

            this.Cursor = Cursors.WaitCursor;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                i = dg.Rows.Add();
                dg["dg_No", i].Value = i + 1;
                dg["dg_LocalNm", i].Value = dr["local_nm"];
                dg["dg_LineNm", i].Value = dr["line_nm"];
                dg["dg_StationNm", i].Value = dr["station_nm"];
                dg["dg_LocalCd", i].Value = dr["local_cd"];
                dg["dg_LineCd", i].Value = dr["line_cd"].ToString().PadLeft(2, '0');
                dg["dg_StationCd", i].Value = dr["station_cd"].ToString().PadLeft(3, '0');
                dg["dg_X", i].Value = dr["x"];
                dg["dg_Y", i].Value = dr["y"];
                dg["dg_Idx", i].Value = dr["idx"];
            }
            dr.Close();
            db.Close();
            this.Cursor = Cursors.Default;

            dg.SelectionChanged += dg_SelectionChanged;
        }

        private void dg_SelectionChanged(object sender, EventArgs e)
        {
            int i = 0;
            string sql, idx;

            //cbxLocal.SelectedIndexChanged -= CbxLocal_SelectedIndexChanged;

            if (dg.CurrentRow == null) return;
            i = dg.CurrentRow.Index;
            idx = dg["dg_Idx", i].Value.ToString();

            sql = $"select * from tx_railroad where idx={idx} limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            txtIdx.Text = dr["idx"].ToString();
            cbxLocal.SelectedValue = dr["local_cd"];
            cbxLine.SelectedValue = dr["line_cd"];
            txtStationCd.Text = dr["station_cd"].ToString();
            txtStationNm.Text = dr["station_nm"].ToString();
            txtCoordX.Text = dr["x"].ToString();
            txtCoordY.Text = dr["y"].ToString();

            wbr.Navigate($"https://www.tankauction.com/SOLAR/mapCoord.php?dvsn=3&tid={dr["idx"]}");
            dr.Close();
            db.Close();
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            ui.FormClear(panDtl, new string[] { "cbxLocal", "cbxLine" });
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            string sql, cvp;
            int stationCd;

            if (cbxLocal.SelectedIndex == 0 || cbxLine.SelectedIndex == 0 || txtStationNm.Text == string.Empty)
            {
                MessageBox.Show("[지역/선로/역명]을 입력 해 주세요.");
                return;
            }

            if (txtStationCd.Text == String.Empty)
            {
                db.Open();
                sql = $"select max(station_cd) as maxCd from tx_railroad where local_cd='{cbxLocal.SelectedValue}' and line_cd='{cbxLine.SelectedValue}'";
                MySqlDataReader dr = db.ExeRdr(sql);
                dr.Read();
                if (dr["maxCd"] == DBNull.Value)
                {
                    stationCd = 1;
                }
                else
                {
                    stationCd = Convert.ToInt32(dr["maxCd"].ToString()) + 1;
                }
                dr.Close();
                db.Close();
            }
            else
            {
                stationCd = Convert.ToInt32(txtStationCd.Text);
            }

            cvp = $"local_nm='{cbxLocal.Text}', line_nm='{cbxLine.Text}', station_nm='{txtStationNm.Text.Trim()}', local_cd='{cbxLocal.SelectedValue}', line_cd='{cbxLine.SelectedValue}', station_cd='{stationCd}', x='{txtCoordX.Text}', y='{txtCoordY.Text}'";
            //MessageBox.Show(cvp);
            if (txtIdx.Text == String.Empty)
            {
                sql = $"insert into tx_railroad set {cvp}";
            }
            else
            {
                sql = $"update tx_railroad set {cvp} where idx={txtIdx.Text}";
            }
            db.Open();
            db.ExeQry(sql);
            db.Close();

            MessageBox.Show("저장 되었습니다.");
            btnSrch_Click(null, null);
        }

        private void btnMatchAdd_Click(object sender, EventArgs e)
        {
            if (txtCoordX.Text == String.Empty || txtCoordX.Text == "0" || txtIdx.Text == String.Empty)
            {
                MessageBox.Show("먼저 역좌표 저장 후 추가 해 주세요");
                return;
            }

            dgR.Rows.Add(txtStationNm.Text, txtIdx.Text);
            dgR.ClearSelection();
        }

        private void btnMatchDel_Click(object sender, EventArgs e)
        {
            dgR.Rows.Remove(dgR.Rows[dgR.SelectedRows[0].Index]);
            dgR.ClearSelection();
        }

        /// <summary>
        /// 경/공매 역세권 매칭(진행건)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnMatch_Click(object sender, EventArgs e)
        {
            int mvCaCnt = 0, mvPaCnt = 0;
            string sql, tid, cd;
            double lat_p = 0, lng_p = 0, lat_s = 0, lng_s = 0, distance = 0;

            if (dgR.Rows.Count == 0)
            {
                MessageBox.Show("매칭할 역을 추가 해 주세요");
                return;
            }

            if (MessageBox.Show("신설된 역의 물건매칭을 시작 하시겠습니까?", "역세권 매칭", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            this.Cursor = Cursors.WaitCursor;
            btnMatch.Enabled = false;

            CoordCal cc = new CoordCal();

            List<string> list = new List<string>();
            foreach (DataGridViewRow row in dgR.Rows)
            {
                if (list.Contains(row.Cells["dgR_Idx"].Value.ToString())) continue;
                list.Add(row.Cells["dgR_Idx"].Value.ToString());
            }

            sql = $"select * from tx_railroad where idx in ({string.Join(",", list.ToArray())}) order by local_cd,line_cd,station_cd";
            DataTable dtR = db.ExeDt(sql);

            //경매
            sql = "select tid, x, y from ta_list where sta1 in (11,13) order by tid";
            DataTable dt = db.ExeDt(sql);
            foreach (DataRow row in dt.Rows)
            {
                tid = row["tid"].ToString();
                lng_p = Convert.ToDouble(row["x"]);     //경도
                lat_p = Convert.ToDouble(row["y"]);     //위도

                foreach (DataRow srow in dtR.Rows)
                {
                    lng_s = Convert.ToDouble(srow["x"]);
                    lat_s = Convert.ToDouble(srow["y"]);
                    distance = cc.calDistance(lat_p, lng_p, lat_s, lng_s);
                    if (distance >= 0 && distance <= 1000)
                    {
                        cd = string.Format("{0}{1}{2}", srow["local_cd"], srow["line_cd"].ToString().PadLeft(2, '0'), srow["station_cd"].ToString().PadLeft(3, '0'));
                        db.Open();
                        sql = "insert ignore into ta_railroad set tid='" + tid + "', cd='" + cd + "', distance='" + distance.ToString() + "', wdt=curdate()";
                        db.ExeQry(sql);
                        db.Close();
                        mvCaCnt++;
                    }
                }
            }

            //공매
            sql = "select cltr_no as tid, x, y from tb_list where stat_nm IN ('입찰준비중','인터넷입찰진행중','인터넷입찰마감','수의계약가능','입찰공고중','현장입찰진행중') and x > 0 order by cltr_no";
            dt = db.ExeDt(sql);
            foreach (DataRow row in dt.Rows)
            {
                tid = row["tid"].ToString();
                lng_p = Convert.ToDouble(row["x"]);     //경도
                lat_p = Convert.ToDouble(row["y"]);     //위도

                foreach (DataRow srow in dtR.Rows)
                {
                    lng_s = Convert.ToDouble(srow["x"]);
                    lat_s = Convert.ToDouble(srow["y"]);
                    distance = cc.calDistance(lat_p, lng_p, lat_s, lng_s);
                    if (distance >= 0 && distance <= 1000)
                    {
                        cd = string.Format("{0}{1}{2}", srow["local_cd"], srow["line_cd"].ToString().PadLeft(2, '0'), srow["station_cd"].ToString().PadLeft(3, '0'));
                        db.Open();
                        sql = "insert ignore into tb_railroad set tid='" + tid + "', cd='" + cd + "', distance='" + distance.ToString() + "', wdt=curdate()";
                        db.ExeQry(sql);
                        db.Close();
                        mvPaCnt++;
                    }
                }
            }

            btnMatch.Enabled = true;
            this.Cursor = Cursors.Default;

            MessageBox.Show($"경매-{mvCaCnt}건, 공매-{mvPaCnt}건 매칭 되었습니다.");
        }
    }
}
