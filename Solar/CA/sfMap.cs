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
using Solar.PA;

namespace Solar.CA
{
    public partial class sfMap : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        AuctCd auctCd = new AuctCd();
        NetUtil net = new NetUtil();
        ApiUtil api = new ApiUtil();

        public sfMap()
        {
            InitializeComponent();
            this.Shown += SfMap_Shown;
        }

        private void SfMap_Shown(object sender, EventArgs e)
        {
            string url;

            url = "https://www.tankauction.com/SOLAR/mapCoord.php";
            wbr.Navigate(url);

            ui.DgSetRead(dgF);

            //법정동 시/도 코드
            DataTable dtSidoCd = new DataTable();
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
            cbxDn.SelectedIndexChanged += CbxAdrsCd_SelectedIndexChanged;

            string prntNm = this.Owner.Name;
            if (prntNm == "wfCaMgmt")
            {
                wfCaMgmt prnt = (wfCaMgmt)this.Owner;
                lnkTid.Text = prnt.lnkTid.Text;
                txtAdrs.Text = prnt.txtAdrs.Text;
                txtRegnAdrs.Text = prnt.txtRegnAdrs.Text;
                txtRoadAdrs.Text = prnt.txtRoadAdrs.Text;
                txtCoordX.Text = prnt.txtCoordX.Text;
                txtCoordY.Text = prnt.txtCoordY.Text;

                cbxSi.SelectedValue = prnt.txtSiCd.Text;
                cbxGu.SelectedValue = prnt.txtGuCd.Text;
                cbxDn.SelectedValue = prnt.txtDnCd.Text;
                cbxRi.SelectedValue = prnt.txtRiCd.Text;

                LoadFileInfo(prnt);
            }
            else if (prntNm == "wfPaMgmt")
            {
                wfPaMgmt prnt = (wfPaMgmt)this.Owner;
                lnkTid.Text = prnt.lnkCltrNo.Text;
                txtAdrs.Text = prnt.txtRegnAdrs.Text;
                txtRegnAdrs.Text = prnt.txtRegnAdrs.Text;
                txtRoadAdrs.Text = prnt.txtRoadAdrs.Text;
                txtCoordX.Text = prnt.txtCoordX.Text;
                txtCoordY.Text = prnt.txtCoordY.Text;

                cbxSi.SelectedValue = prnt.txtSiCd.Text;
                cbxGu.SelectedValue = prnt.txtGuCd.Text;
                cbxDn.SelectedValue = prnt.txtDnCd.Text;
                cbxRi.SelectedValue = prnt.txtRiCd.Text;
            }
            else if (prntNm == "wfTrust")
            {
                wfTrust prnt = (wfTrust)this.Owner;
                lnkTid.Text = prnt.lnkIdx.Text;
                txtAdrs.Text = prnt.txtAdrs.Text;
                txtCoordX.Text = prnt.txtCoordX.Text;
                txtCoordY.Text = prnt.txtCoordY.Text;

                cbxSi.SelectedValue = prnt.txtSiCd.Text;
                cbxGu.SelectedValue = prnt.txtGuCd.Text;
                cbxDn.SelectedValue = prnt.txtDnCd.Text;
                cbxRi.SelectedValue = prnt.txtRiCd.Text;
            }
        }

        /// <summary>
        /// 파일 정보
        /// </summary>
        private void LoadFileInfo(wfCaMgmt prnt)
        {
            int i = 0, n = 0, apslCnt = 0;
            string tid, tbl, spt, sn1, sn2, sn, sql;

            DataTable dtFileCd = db.ExeDt("select * from ta_cd_file order by cd");

            dgF.Rows.Clear();

            tid = lnkTid.Text;
            sn1 = prnt.cbxSn1.Text;
            sn2 = prnt.txtSn2.Text;
            spt = prnt.cbxCrtSpt.SelectedValue.ToString();
            sn = string.Format("{0}{1}", sn1, sn2.PadLeft(6, '0'));
            tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
            sql = "select * from " + tbl + " where (tid=" + tid + " or (spt=" + spt + " and sn='" + sn + "' and tid=0)) and ctgr like 'B%' order by ctgr desc";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                n = dgF.Rows.Add();
                dgF["dgF_No", n].Value = n + 1;
                dgF["dgF_Ctgr", n].Value = dtFileCd.Rows.Cast<DataRow>().Where(t => t["cd"].ToString() == dr["ctgr"].ToString()).FirstOrDefault()["nm"].ToString();
                dgF["dgF_FileNm", n].Value = dr["file"];
                dgF["dgF_Idx", n].Value = dr["idx"];
            }
            dr.Close();
            db.Close();

            dgF.ClearSelection();
        }

        /// <summary>
        /// 탱크 링크-내부 저장된 파일 보기
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dgF_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            string url;
            if (e.ColumnIndex == 0) return;

            string myWeb = Properties.Settings.Default.myWeb;
            url = string.Format(myWeb + "SOLAR/caFileViewer.php?tid={0}&idx={1}", lnkTid.Text, dgF["dgF_Idx", e.RowIndex].Value.ToString());
            wbr2.Navigate(url);
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
                    sql = "select gu_nm, gu_cd from tx_cd_adrs where si_cd=" + cbxSi.SelectedValue.ToString() + " and gu_cd > 0 and dn_cd=0 and hide=0 and concat(si_cd,'',gu_cd) not in (41110,41130,41170,41270,41280,41460,43110,44130,45110,47110,48120) order by gu_nm";
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
                if (cbxGu.SelectedValue.ToString() == "System.Data.DataRowView") return;
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

            if (cbx == cbxDn)
            {
                if (cbxDn.SelectedValue.ToString() == "System.Data.DataRowView") return;
                DataRowView rowView = cbxDn.SelectedItem as DataRowView;

                sql = "select ri_nm, ri_cd from tx_cd_adrs where si_cd=" + cbxSi.SelectedValue.ToString() + " and gu_cd=" + cbxGu.SelectedValue.ToString() + " and dn_cd=" + rowView["dn_cd"].ToString() + " and ri_cd > 0 and hide=0 order by ri_nm";
                DataTable dtRi = db.ExeDt(sql);
                DataRow row = dtRi.NewRow();
                row["ri_nm"] = "-리-";
                row["ri_cd"] = 0;
                dtRi.Rows.InsertAt(row, 0);

                cbxRi.DataSource = dtRi;
                cbxRi.DisplayMember = "ri_nm";
                cbxRi.ValueMember = "ri_cd";
                cbxRi.SelectedValue = 0;
            }
        }

        private void wbr_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            HtmlDocument html = wbr.Document;
            if (html.GetElementById("map") != null)
            {
                html.GetElementById("map").Click += SfMap_Click;
            }
        }

        private void SfMap_Click(object sender, HtmlElementEventArgs e)
        {
            HtmlDocument html = wbr.Document;
            txtCoordX.Text = html.GetElementById("x").GetAttribute("value");
            txtCoordY.Text = html.GetElementById("y").GetAttribute("value");
        }

        private void btnFindCd_Click(object sender, EventArgs e)
        {
            string adrs, regnAdrs, roadAdrs;

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

                cbxSi.SelectedValue = dict["sidoCd"];
                cbxGu.SelectedValue = dict["gugunCd"];
                cbxDn.SelectedValue = dict["dongCd"];
                cbxRi.SelectedValue = dict["riCd"];
                /*
                adrsType = (dict["adrsType"].Contains("ROAD_ADDR")) ? "2" : "1";
                regnAdrs = (dict["jbAdrsNm"] == "") ? adrs : dict["jbAdrsNm"];
                mt = dict["mt"];

                sp.Add(new MySqlParameter("@adrs", adrs));
                sp.Add(new MySqlParameter("@adrs_type", adrsType));
                sp.Add(new MySqlParameter("@regn_adrs", regnAdrs));
                sp.Add(new MySqlParameter("@mt", mt));
                sp.Add(new MySqlParameter("@m_adrs_no", dict["jbNoM"]));
                sp.Add(new MySqlParameter("@s_adrs_no", dict["jbNoS"]));
                sp.Add(new MySqlParameter("@road_adrs", dict["rdAdrsNm"]));
                sp.Add(new MySqlParameter("@m_bldg_no", dict["bldgNoM"]));
                sp.Add(new MySqlParameter("@s_bldg_no", dict["bldgNoS"]));
                sp.Add(new MySqlParameter("@bldg_nm", dict["bldgNm"]));
                sp.Add(new MySqlParameter("@road_nm", dict["rdNm"]));
                */
            }
        }

        private void btnAply_Click(object sender, EventArgs e)
        {
            string prntNm = this.Owner.Name;
            string sql, tid, cd;
            double lat_p = 0, lng_p = 0, lat_s = 0, lng_s = 0, distance = 0;

            if (prntNm == "wfCaMgmt")
            {
                wfCaMgmt prnt = (wfCaMgmt)this.Owner;
                prnt.txtAdrs.Text = txtAdrs.Text;
                prnt.txtRegnAdrs.Text = txtRegnAdrs.Text;
                prnt.txtRoadAdrs.Text = txtRoadAdrs.Text;
                prnt.txtCoordX.Text = txtCoordX.Text;
                prnt.txtCoordY.Text = txtCoordY.Text;

                prnt.txtSiCd.Text = cbxSi.SelectedValue.ToString();
                prnt.txtGuCd.Text = cbxGu.SelectedValue.ToString();
                prnt.txtDnCd.Text = cbxDn.SelectedValue.ToString();
                prnt.txtRiCd.Text = cbxRi.SelectedValue.ToString();

                //역세권 재매칭
                CoordCal cc = new CoordCal();

                tid = lnkTid.Text;
                if (txtCoordX.Text != string.Empty && txtCoordX.Text != "0")
                {
                    sql = "select * from tx_railroad order by local_cd,line_cd,station_cd";
                    DataTable dtR = db.ExeDt(sql);

                    sql = "delete from ta_railroad where tid=" + tid;
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();

                    lng_p = Convert.ToDouble(txtCoordX.Text);     //경도
                    lat_p = Convert.ToDouble(txtCoordY.Text);     //위도

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
                            sql = "update ta_list set station_prc=1 where tid='" + tid + "'";
                            db.ExeQry(sql);
                            db.Close();
                        }
                    }
                }
            }
            else if (prntNm == "wfPaMgmt")
            {
                wfPaMgmt prnt = (wfPaMgmt)this.Owner;
                prnt.txtRegnAdrs.Text = txtRegnAdrs.Text;
                prnt.txtRoadAdrs.Text = txtRoadAdrs.Text;
                prnt.txtCoordX.Text = txtCoordX.Text;
                prnt.txtCoordY.Text = txtCoordY.Text;

                prnt.txtSiCd.Text = cbxSi.SelectedValue.ToString();
                prnt.txtGuCd.Text = cbxGu.SelectedValue.ToString();
                prnt.txtDnCd.Text = cbxDn.SelectedValue.ToString();
                prnt.txtRiCd.Text = cbxRi.SelectedValue.ToString();

                //역세권 재매칭
                CoordCal cc = new CoordCal();

                tid = lnkTid.Text;
                if (txtCoordX.Text != string.Empty && txtCoordX.Text != "0")
                {
                    sql = "select * from tx_railroad order by local_cd,line_cd,station_cd";
                    DataTable dtR = db.ExeDt(sql);

                    sql = "delete from tb_railroad where tid=" + tid;
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();

                    lng_p = Convert.ToDouble(txtCoordX.Text);     //경도
                    lat_p = Convert.ToDouble(txtCoordY.Text);     //위도

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
                            sql = "update tb_list set station_prc=1 where cltr_no='" + tid + "'";
                            db.ExeQry(sql);
                            db.Close();
                        }
                    }
                }
            }
            else if (prntNm == "wfTrust")
            {
                wfTrust prnt = (wfTrust)this.Owner;
                prnt.txtCoordX.Text = txtCoordX.Text;
                prnt.txtCoordY.Text = txtCoordY.Text;

                prnt.txtSiCd.Text = cbxSi.SelectedValue.ToString();
                prnt.txtGuCd.Text = cbxGu.SelectedValue.ToString();
                prnt.txtDnCd.Text = cbxDn.SelectedValue.ToString();
                prnt.txtRiCd.Text = cbxRi.SelectedValue.ToString();
            }

            this.Close();
        }

        private void lnkAdrs_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string srchAdrs = string.Empty;

            HtmlDocument html = wbr.Document;
            if (html.GetElementById("addr") != null)
            {
                LinkLabel lnk = (LinkLabel)sender;

                if (lnk == lnkAdrs) srchAdrs = txtAdrs.Text.Trim();
                else if (lnk == lnkRegnAdrs) srchAdrs = txtRegnAdrs.Text.Trim();
                else if (lnk == lnkRoadAdrs) srchAdrs = txtRoadAdrs.Text.Trim();

                html.GetElementById("addr").SetAttribute("value", srchAdrs);
                wbr.Document.InvokeScript("search_");
            }
        }
    }
}
