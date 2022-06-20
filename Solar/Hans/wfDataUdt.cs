using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace Solar.Hans
{
    public partial class wfDataUdt : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        NetUtil net = new NetUtil();
        ApiUtil api = new ApiUtil();

        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 1500, webCnt = 0;

        public wfDataUdt()
        {
            InitializeComponent();
        }

        private void btnStart_Click(object sender, EventArgs e)
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
            //Prc_ElvtCnt();

            //Prc_RgstAutoCart();

            //Prc_119();

            MessageBox.Show("처리 완료");
        }

        private void Prc_119()
        {
            string sql, dvsn, tid, tbl, sn1;

            //sql = "SELECT R.tid,spt,sn1 FROM db_tank.tx_rgst_auto R, db_main.ta_list L where R.tid=L.tid and R.dvsn=12 and R.wdt=curdate() GROUP by R.tid";
            sql = "select * from db_tank.tx_rgst_auto where wdt=curdate() and dvsn=14 and ul=0";
            DataTable dt = db.ExeDt(sql);

            foreach (DataRow row in dt.Rows)
            {
                sql = $"select dvsn from db_main.ta_ls where tid='{row["tid"]}' and pin='{row["pin"]}' limit 1";
                db.Open();
                MySqlDataReader dr = db.ExeRdr(sql);
                dr.Read();
                dvsn = dr["dvsn"].ToString();
                dr.Close();
                db.Close();

                db.Open();
                db.ExeQry($"update db_tank.tx_rgst_auto set ls_type='{dvsn}' where idx='{row["idx"]}'");
                db.Close();
                
                /*
                tid = row["tid"].ToString();
                sql = $"delete from ta_rgst where tid={tid}";
                db.Open();
                db.ExeQry(sql);
                db.Close();
                */

                /*
                tid = row["tid"].ToString();
                sn1 = row["sn1"].ToString();
                tbl = (Convert.ToDecimal(sn1) > 2004) ? ("ta_f" + sn1) : "ta_f2004";
                sql = $"delete from {tbl} where tid='{tid}' and (ctgr in ('DA','DB'))";
                db.Open();
                db.ExeQry(sql);
                db.Close();
                */
            }
        }

        /// <summary>
        /// 등기 자동 발급대상 검증용
        /// </summary>
        private void Prc_RgstAutoCart()
        {
            string sql, tid, tbl, prevTid = "";
            bool fileExist, autoExist;
            int landCnt = 0, bldgCnt = 0, multiCnt = 0;
            string autoDvsn;

            string today = DateTime.Now.ToShortDateString();

            DataTable dt = new DataTable();
            dt.Columns.Add("tid");
            dt.Columns.Add("lsIdx");
            dt.Columns.Add("lsNo");
            dt.Columns.Add("pin");

            //
            //경매-예정 물건
            //
            autoDvsn = "14";
            //sql = "select L.tid,spt,sn1,sn2,cat3, S.idx,no,dvsn,pin,pnu from ta_list L, ta_ls S where L.tid=S.tid and sta1=10 and cat1!=30 and rcp_dt='0000-00-00' and ini_dt <= date_sub(curdate(),interval 15 day) order by L.tid";
            sql = "select L.tid,spt,sn1,sn2,cat3, S.idx,no,dvsn,pin,pnu from ta_list L, ta_ls S where L.tid=S.tid and L.tid in (2058812,2057565,2051336,2011146,2095254,2047630,2048277,2075416,2086609,2038379,2055027,2055607,2051887,2082242,2069970,1973883,2076252,2069405,2064037,2059838,2055904,2094566)";
            DataTable dtLs = db.ExeDt(sql);
            foreach (DataRow row in dtLs.Rows)
            {
                tid = row["tid"].ToString();
                if (tid == prevTid) continue;

                //등기파일 유무 체크
                tbl = (Convert.ToDecimal($"{row["sn1"]}") > 2004) ? ("ta_f" + $"{row["sn1"]}") : "ta_f2004";
                sql = "select * from " + tbl + " where ctgr in ('DA','DB') and tid=" + tid + " limit 1";
                db.Open();
                fileExist = db.ExistRow(sql);
                db.Close();
                fileExist = false;  //임시
                if (fileExist)
                {
                    prevTid = tid;
                    continue;
                }

                DataRow[] rows = dtLs.Select($"tid={tid}");
                if (rows.Count() == 1)
                {
                    DataRow r = rows[0];
                    if ($"{r["pin"]}" == string.Empty || ($"{r["dvsn"]}" != "토지" && $"{r["dvsn"]}" != "건물" && $"{r["dvsn"]}" != "집합건물")) continue;
                    dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["pin"] });    //대상
                }

                if (rows.Count() == 2)
                {
                    DataTable dtS = rows.CopyToDataTable();
                    landCnt = dtS.Select("dvsn='토지'").Count();
                    bldgCnt = dtS.Select("dvsn='건물'").Count();
                    if (landCnt == 1 && bldgCnt == 1)
                    {
                        DataRow r = dtS.Rows[0];
                        if ($"{r["pin"]}" != string.Empty) dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["pin"] });     //대상
                        r = dtS.Rows[1];
                        if ($"{r["pin"]}" != string.Empty) dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["pin"] });     //대상
                    }
                }

                prevTid = tid;
            }

            if (dt.Rows.Count == 0) return;

            //대상 db 일괄 추가
            foreach (DataRow row in dt.Rows)
            {
                db.Open();
                autoExist = db.ExistRow($"select idx from db_tank.tx_rgst_auto where (dvsn between 10 and 14) and tid='{row["tid"]}' and pin='{row["pin"]}' and wdt=curdate() limit 1");
                if (!autoExist)
                {
                    db.ExeQry($"insert into db_tank.tx_rgst_auto set dvsn='{autoDvsn}', tid='{row["tid"]}', ls_no='{row["lsNo"]}', pin='{row["pin"]}', wdt=curdate(), wtm=curtime()");
                }
                db.Close();
            }

            //
            //경매-정식/선행 공고
            //
            dt.Rows.Clear();
            autoDvsn = "11";
            //sql = "select L.tid,spt,sn1,sn2,cat3, S.idx,no,dvsn,pin,pnu from ta_list L, ta_ls S where L.tid=S.tid and sta2=1110 and cat1!=30 and bid_dt >= curdate() and (2nd_dt=curdate() or pre_dt >= curdate()) order by L.tid";
            sql = "select L.tid,spt,sn1,sn2,cat3, S.idx,no,dvsn,pin,pnu from ta_list L, ta_ls S where L.tid=S.tid and L.tid in (2027270,2076626,2032148,2024512,1698989,2080042,1992981,2084004,2051013,2030839,2057587,1866649,2036925,2090827,2034601,2024070,2029041,2045440,2027647,2050708,2061561,1901682,2085102,2051939,2013209,2071636,2037731)";
            dtLs = db.ExeDt(sql);
            foreach (DataRow row in dtLs.Rows)
            {
                tid = row["tid"].ToString();
                if (tid == prevTid) continue;

                //등기파일 유무 체크
                tbl = (Convert.ToDecimal($"{row["sn1"]}") > 2004) ? ("ta_f" + $"{row["sn1"]}") : "ta_f2004";
                sql = "select * from " + tbl + " where ctgr in ('DA','DB') and tid=" + tid + " limit 1";
                db.Open();
                fileExist = db.ExistRow(sql);
                db.Close();
                fileExist = false;  //임시
                if (fileExist)
                {
                    prevTid = tid;
                    continue;
                }
                                
                DataRow[] rows = dtLs.Select($"tid={tid}");                    
                if (rows.Count() == 1)
                {
                    DataRow r = rows[0];
                    if ($"{r["pin"]}" == string.Empty || ($"{r["dvsn"]}" != "토지" && $"{r["dvsn"]}" != "건물" && $"{r["dvsn"]}" != "집합건물")) continue;
                    dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["pin"] });    //대상
                }
                                        
                DataTable dtS = rows.CopyToDataTable();
                landCnt = dtS.Select("dvsn='토지'").Count();
                bldgCnt = dtS.Select("dvsn='건물'").Count();
                multiCnt = dtS.Select("dvsn='집합건물'").Count();

                //A,B,C | B,C - 패스
                if (bldgCnt == 1 && multiCnt == 1)
                {
                    prevTid = tid;
                    continue;
                }

                DataTable dtL = db.ExeDt($"select ls_no, sqm from ta_land where tid='{tid}' order by sqm desc");
                DataTable dtB = db.ExeDt($"select ls_no, sqm from ta_bldg where tid='{tid}' and dvsn=1 order by sqm desc");

                //A-토지만
                if (bldgCnt == 0 && multiCnt == 0)
                {
                    if (dtL.Rows.Count > 0)
                    {
                        //DataRow r = dtS.Rows.Cast<DataRow>().OrderByDescending(t => t[""]).FirstOrDefault();
                        var r = dtS.Rows.Cast<DataRow>().Where(t => t["no"].ToString() == dtL.Rows[0]["ls_no"].ToString()).FirstOrDefault();
                        if (r != null)
                        {
                            if (r["pin"].ToString() != String.Empty)
                            {
                                dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["pin"] });    //대상
                            }
                        }
                    }                        
                }

                //B-건물만
                if (landCnt == 0 && multiCnt == 0)
                {
                    if (dtB.Rows.Count > 0)
                    {
                        var r = dtS.Rows.Cast<DataRow>().Where(t => t["no"].ToString() == dtB.Rows[0]["ls_no"].ToString()).FirstOrDefault();
                        if (r != null)
                        {
                            if (r["pin"].ToString() != String.Empty)
                            {
                                dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["pin"] });    //대상
                            }
                        }
                    }
                }

                //C-집합만
                if (landCnt == 0 && bldgCnt == 0)
                {
                    if (dtB.Rows.Count > 0)
                    {
                        var r = dtS.Rows.Cast<DataRow>().Where(t => t["no"].ToString() == dtB.Rows[0]["ls_no"].ToString()).FirstOrDefault();
                        if (r != null)
                        {
                            if (r["pin"].ToString() != String.Empty)
                            {
                                dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["pin"] });    //대상
                            }
                        }
                    }
                }

                //A,B-토지,건물
                if (landCnt > 0 && bldgCnt > 0)
                {
                    if (dtB.Rows.Count > 0)
                    {
                        var r = dtS.Rows.Cast<DataRow>().Where(t => t["no"].ToString() == dtB.Rows[0]["ls_no"].ToString()).FirstOrDefault();
                        if (r != null)
                        {
                            if (r["pin"].ToString() != String.Empty)
                            {
                                dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["pin"] });    //대상-건물
                            }

                            r = dtS.Rows.Cast<DataRow>().Where(t => t["dvsn"].ToString() == "토지" && t["pnu"].ToString() == r["pnu"].ToString()).FirstOrDefault();
                            if (r != null)
                            {
                                if (r["pin"].ToString() != String.Empty)
                                {
                                    dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["pin"] });    //대상-토지(건물과 동일PNU)
                                }
                            }                            
                        }
                    }
                }

                //A,C-토지,집합
                bool findLand = false;
                if (landCnt > 0 && multiCnt > 0)
                {
                    if (dtB.Rows.Count > 0)
                    {
                        var r = dtS.Rows.Cast<DataRow>().Where(t => t["no"].ToString() == dtB.Rows[0]["ls_no"].ToString()).FirstOrDefault();
                        if (r != null)
                        {
                            if (r["pin"].ToString() != String.Empty)
                            {
                                dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["pin"] });    //대상-집합
                            }

                            r = dtS.Rows.Cast<DataRow>().Where(t => t["dvsn"].ToString() == "토지" && t["pnu"].ToString() == r["pnu"].ToString()).FirstOrDefault();
                            if (r != null)
                            {
                                if (r["pin"].ToString() != String.Empty)
                                {
                                    dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["pin"] });    //대상-토지(집합과 동일PNU)
                                }
                                findLand = true;
                            }
                        }
                    }

                    if (!findLand && dtL.Rows.Count > 0)
                    {
                        var r = dtS.Rows.Cast<DataRow>().Where(t => t["no"].ToString() == dtL.Rows[0]["ls_no"].ToString()).FirstOrDefault();
                        if (r != null)
                        {
                            if (r["pin"].ToString() != String.Empty)
                            {
                                dt.Rows.Add(new object[] { r["tid"], r["idx"], r["no"], r["pin"] });    //대상-토지
                            }
                        }
                    }
                }

                prevTid = tid;
            }

            if (dt.Rows.Count == 0) return;

            //대상 db 일괄 추가
            foreach (DataRow row in dt.Rows)
            {
                db.Open();
                autoExist = db.ExistRow($"select idx from db_tank.tx_rgst_auto where (dvsn between 10 and 14) and tid='{row["tid"]}' and pin='{row["pin"]}' and wdt=curdate() limit 1");
                if (!autoExist)
                {
                    db.ExeQry($"insert into db_tank.tx_rgst_auto set dvsn='{autoDvsn}', tid='{row["tid"]}', ls_no='{row["lsNo"]}', pin='{row["pin"]}', wdt=curdate(), wtm=curtime()");
                }
                db.Close();
            }
        }

        /// <summary>
        /// 엘리베이터 유무(건물-승강기대수)
        /// </summary>
        private void Prc_ElvtCnt()
        {
            return;

            int totalCnt, mvCnt = 0, eqCnt = 0, totCnt, curCnt;
            string sql, url, xml, serviceKey, tid, lsNo, adrs0, adrs, pnu, platGbCd, bun, ji, newPlatPlc, aprvDt, flrCnt, dongNm, elvtCnt, idx;

            Dictionary<string, string> dic = new Dictionary<string, string>();  //승인일자, 총층수
            Dictionary<string, string> dic2 = new Dictionary<string, string>(); //동명칭, 승인일자

            sql = "SELECT L.tid,S.no,S.pnu,S.adrs,B.tot_flr,B.elvt,B.idx from ta_list L , ta_ls S , ta_bldg B WHERE L.tid=S.tid and S.tid=B.tid and S.no=B.ls_no and S.dvsn in ('건물','집합건물') and B.dvsn=1 and B.elvt=0 order by L.tid";
            DataTable dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;
            curCnt = 0;

            XmlDocument doc = new XmlDocument();

            serviceKey = api.RndSrvKey();

            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                dic.Clear();
                dic2.Clear();
                aprvDt = string.Empty;
                eqCnt = 0;

                idx = row["idx"].ToString();
                tid = row["tid"].ToString();
                lsNo = row["no"].ToString();
                pnu = row["pnu"].ToString();
                adrs0 = row["adrs"].ToString();
                adrs = row["adrs"].ToString();
                flrCnt = row["tot_flr"].ToString();
                if (flrCnt == "0") flrCnt = "1";
                elvtCnt = row["elvt"].ToString();

                progrsView($"[엘리비에터] TID -> {tid} ^ {curCnt} / {totCnt}", 1);  //진행상태
                if (pnu == string.Empty || pnu == "0") continue;

                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                platGbCd = (Convert.ToDecimal(pnu.Substring(10, 1)) - 1).ToString();
                bun = pnu.Substring(11, 4);
                ji = pnu.Substring(15, 4);

                url = "http://apis.data.go.kr/1613000/BldRgstService_v2/getBrTitleInfo?serviceKey=" + serviceKey + "&sigunguCd=" + pnu.Substring(0, 5) + "&bjdongCd=" + pnu.Substring(5, 5) + "&platGbCd=" + platGbCd + "&bun=" + bun + "&ji=" + ji + "&numOfRows=100&pageNo=1";
                xml = net.GetHtml(url, Encoding.UTF8);
                if (xml.Contains("totalCount") == false) continue;

                doc.LoadXml(xml);
                XmlNamespaceManager nsmg = new XmlNamespaceManager(doc.NameTable);
                nsmg.AddNamespace("n", doc.DocumentElement.NamespaceURI);
                XmlNode nd_total = doc.SelectSingleNode("/n:response/n:body/n:totalCount", nsmg);
                totalCnt = Convert.ToInt16(nd_total.InnerText);
                if (totalCnt == 0) continue;

                if (totalCnt == 1)
                {
                    aprvDt = (doc.SelectSingleNode("/n:response/n:body/n:items/n:item/n:useAprDay", nsmg) == null) ? string.Empty : doc.SelectSingleNode("/n:response/n:body/n:items/n:item/n:useAprDay", nsmg).InnerText.Trim();
                    elvtCnt = (doc.SelectSingleNode("/n:response/n:body/n:items/n:item/n:rideUseElvtCnt", nsmg) == null) ? string.Empty : doc.SelectSingleNode("/n:response/n:body/n:items/n:item/n:rideUseElvtCnt", nsmg).InnerText.Trim();
                }
                else
                {
                    if (adrs.Contains(",")) adrs = adrs.Remove(adrs.IndexOf(","));
                    if (adrs.Contains("(")) adrs = adrs.Remove(adrs.IndexOf("("));
                    adrs = adrs.Replace(" ", string.Empty).Trim();

                    foreach (XmlNode item in doc.SelectNodes("/n:response/n:body/n:items/n:item", nsmg))
                    {
                        newPlatPlc = (item.SelectSingleNode("newPlatPlc", nsmg) == null) ? string.Empty : item.SelectSingleNode("newPlatPlc", nsmg).InnerText.Replace(" ", string.Empty).Trim();
                        if (newPlatPlc == string.Empty) continue;
                        if (adrs == newPlatPlc)
                        {
                            aprvDt = (item.SelectSingleNode("useAprDay", nsmg) == null) ? string.Empty : item.SelectSingleNode("useAprDay", nsmg).InnerText.Trim();
                            flrCnt = (item.SelectSingleNode("grndFlrCnt", nsmg) == null) ? "0" : item.SelectSingleNode("grndFlrCnt", nsmg).InnerText.Trim();
                            dongNm = (item.SelectSingleNode("dongNm", nsmg) == null) ? string.Empty : item.SelectSingleNode("dongNm", nsmg).InnerText.Trim();
                            elvtCnt = (item.SelectSingleNode("rideUseElvtCnt", nsmg) == null) ? string.Empty : item.SelectSingleNode("rideUseElvtCnt", nsmg).InnerText.Trim();
                            if (aprvDt == string.Empty) continue;
                            if (dic.ContainsKey(aprvDt) == false) dic.Add(aprvDt, flrCnt);
                            if (dic2.ContainsKey(dongNm) == false) dic2.Add(dongNm, aprvDt);
                        }
                    }
                    if (dic.Count == 1) aprvDt = dic.First().Key;
                    else if (dic.Count > 1)
                    {
                        foreach (KeyValuePair<string, string> kvp in dic)
                        {
                            if (kvp.Value == flrCnt)
                            {
                                if (eqCnt == 0)
                                {
                                    aprvDt = kvp.Key;
                                }
                                eqCnt++;
                            }
                        }
                        if (eqCnt > 1)
                        {
                            aprvDt = string.Empty;
                            //Match match = Regex.Match(adrs0, @"\(\w+동,([\w\d]+)\)"); //(xx동, 건물명)
                            Match match = Regex.Match(adrs0, @"\((\w+동)\)"); //(xx동)
                            if (match.Success)
                            {
                                foreach (KeyValuePair<string, string> kvp in dic2)
                                {
                                    if (kvp.Key == match.Groups[1].Value.Trim())
                                    {
                                        aprvDt = kvp.Value;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                if (aprvDt == string.Empty) continue;

                if (aprvDt.Length == 4) aprvDt = aprvDt + "0000";
                else if (aprvDt.Length == 6) aprvDt = aprvDt + "00";

                db.Open();
                sql = "update ta_bldg set elvt='" + elvtCnt + "' where idx='" + idx + "' and tid='" + tid + "'";
                db.ExeQry(sql);
                db.Close();
                mvCnt++;
            }

            MessageBox.Show($"처리 완료 -> {mvCnt} 건");
        }

        /// <summary>
        /// 화면에 진행상태 표시
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="depth"></param>
        private void progrsView(string msg, int depth = 0)
        {
            if (depth == 0) msg = string.Format("\r\n##### {0} #####\r\n", msg);
            else if (depth == 1) msg = string.Format("> {0}", msg);
            else if (depth == 2) msg = string.Format(">> {0}", msg);
            else if (depth == 3) msg = string.Format(">>> {0}", msg);
            else if (depth == 4) msg = string.Format(">>>> {0}", msg);
            else if (depth == 5) msg = string.Format(">>>>> {0}", msg);

            txtState.AppendText("\r\n" + msg);
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            bgwork.Dispose();

            MessageBox.Show("실행 종료");
        }
    }
}
