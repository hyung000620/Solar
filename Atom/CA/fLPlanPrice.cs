using Solar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Text.RegularExpressions;
using HtmlAgilityPack;
using hap = HtmlAgilityPack;
using HAPDoc = HtmlAgilityPack.HtmlDocument;
using MySql.Data.MySqlClient;
using System.Xml;
using Newtonsoft.Json.Linq;
using System.Collections;

namespace Atom.CA
{
    public partial class fLPlanPrice : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AtomLog atomLog = new AtomLog(110);
        AuctCd auctCd = new AuctCd();
        ApiUtil api = new ApiUtil();

        DataTable dtUse;

        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 1500, webCnt = 0;

        public fLPlanPrice()
        {
            InitializeComponent();
            this.Shown += FLPrice_Shown;
        }

        private void FLPrice_Shown(object sender, EventArgs e)
        {
            CheckForIllegalCrossThreadCalls = false;
            bgwork = new BackgroundWorker();
            bgwork.DoWork += Bgwork_DoWork;
            bgwork.RunWorkerCompleted += Bgwork_RunWorkerCompleted;
            bgwork.WorkerReportsProgress = false;
            bgwork.WorkerSupportsCancellation = true;

            string sql = "select * from tx_cd_use where level3 > 0 order by level3";
            dtUse = db.ExeDt(sql);

            bgwork.RunWorkerAsync();
        }

        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            int totCnt = 0, curCnt = 0, mvCnt = 0;
            string sql, tid, idx, pnu, prpsNm, cd;
            double lat_p = 0, lng_p = 0, lat_s = 0, lng_s = 0, distance = 0;

            DataTable dt;
            IDictionary<string, string> dict = new Dictionary<string, string>();
            List<MySqlParameter> sp = new List<MySqlParameter>();
            
            sql = "select S.* FROM ta_list L, ta_ls S WHERE L.tid=S.tid and sta1 > 10 and pnu='' and dvsn in ('토지','건물','집합건물','기타')";
            dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;

            txtState.AppendText("##### PNU / 좌표 매칭 #####\r\n");
            atomLog.AddLog("# PNU/좌표매칭");
            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                tid = row["tid"].ToString();

                dict.Clear();
                dict = api.DaumSrchAdrs(row["adrs"].ToString());
                txtState.AppendText(string.Format("\r\n >>> {0} / {1} / {2}", tid, curCnt, totCnt));

                sql = "update ta_ls set si_cd=@si_cd, gu_cd=@gu_cd, dn_cd=@dn_cd, ri_cd=@ri_cd, hj_cd=@hj_cd, pnu=@pnu, x=@x, y=@y, zone_no=@zone_no where idx=" + row["idx"].ToString();
                sp.Add(new MySqlParameter("@si_cd", dict["sidoCd"]));
                sp.Add(new MySqlParameter("@gu_cd", dict["gugunCd"]));
                sp.Add(new MySqlParameter("@dn_cd", dict["dongCd"]));
                sp.Add(new MySqlParameter("@ri_cd", dict["riCd"]));
                sp.Add(new MySqlParameter("@hj_cd", dict["hCd"]));
                sp.Add(new MySqlParameter("@pnu", dict["pnu"]));
                sp.Add(new MySqlParameter("@x", dict["x"]));
                sp.Add(new MySqlParameter("@y", dict["y"]));
                sp.Add(new MySqlParameter("@zone_no", dict["zoneNo"]));
                db.Open();
                db.ExeQry(sql, sp);
                sp.Clear();
                db.Close();
            }
            
            sql = "select L.tid, L.idx, S.pnu from ta_list T, ta_ls S, ta_land L where T.tid=S.tid and S.tid=L.tid and S.no=L.ls_no and sta1=11 and price_prc in (0,2) and pnu != '' and cat3 not in (0,201013,201014,201015,201017,201019,201022,201130,201216,201123,201020,201111)";
            //sql = "select L.tid, L.idx, S.pnu from ta_list T, ta_ls S, ta_land L where T.tid=S.tid and S.tid=L.tid and S.no=L.ls_no and T.tid=1908323";
            dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;
            curCnt = 0;

            txtState.AppendText("\r\n\r\n##### 개별공시지가 수집 #####\r\n");
            atomLog.AddLog("# 공시지가");
            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                tid = row["tid"].ToString();
                idx = row["idx"].ToString();
                pnu = row["pnu"].ToString();
                txtState.AppendText(string.Format("\r\n >>> {0} / {1} / {2}", tid, curCnt, totCnt));
                if (pnu == "0") continue;

                Prc_Price(tid, idx, pnu);
            }

            sql = "select L.tid, L.idx, L.prps_nm, S.pnu from ta_list T, ta_ls S, ta_land L where T.tid=S.tid and S.tid=L.tid and S.no=L.ls_no and sta1=11 and plan_prc in (0,2) and pnu != '' and cat3 not in (0,201013,201014,201015,201017,201019,201022,201130,201216,201123,201020,201111)";
            dt = db.ExeDt(sql);
            totCnt = dt.Rows.Count;
            curCnt = 0;

            txtState.AppendText("\r\n\r\n##### 토지이용계획 수집 #####\r\n");
            atomLog.AddLog("# 토지이용계획");
            foreach (DataRow row in dt.Rows)
            {
                curCnt++;
                tid = row["tid"].ToString();
                idx = row["idx"].ToString();
                pnu = row["pnu"].ToString();
                prpsNm = row["prps_nm"].ToString();
                txtState.AppendText(string.Format("\r\n >>> {0} / {1} / {2}", tid, curCnt, totCnt));
                if (pnu == "0") continue;

                Prc_Plan(tid, idx, pnu, prpsNm);
            }

            txtState.AppendText("\r\n\r\n##### 역세권 매칭 #####\r\n");
            atomLog.AddLog("# 역세권매칭");
            CoordCal cc = new CoordCal();
            sql = "select * from tx_railroad order by local_cd,line_cd,station_cd";
            DataTable dtR = db.ExeDt(sql);

            string cmpDt = DateTime.Now.AddDays(-5).ToShortDateString();
            sql = "select tid, x, y from ta_list where 2nd_dt > '" + cmpDt + "' and cat1 in (10,20,40) and x > 0 and station_prc=0 order by tid";
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
                        sql = "insert ignore into ta_railroad set tid='" + tid + "', cd='" + cd + "', distance='" + distance.ToString() + "', wdt=curdate()";
                        db.ExeQry(sql);
                        sql = "update ta_list set station_prc=1 where tid='" + tid + "'";
                        db.ExeQry(sql);
                        db.Close();
                        mvCnt++;
                    }
                }
            }
            atomLog.AddLog("실행 완료", 1);

            //
            //Prc_PriceUdt();     //당해 데이터 업데이트시 사용
            //
        }

        /// <summary>
        /// 토지이용계획 수집
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="idx"></param>
        /// <param name="pnu"></param>
        private void Prc_Plan(string tid, string idx, string pnu, string dbPrpsNm)
        {
            int totalCnt = 0;
            string sql, url, xml, prpsCd = "", prpsNm = "", useCdtn = "";
            string prposAreaDstrcCode = "", prposAreaDstrcCodeNm = "";

            ArrayList alCd = new ArrayList();
            ArrayList alNm = new ArrayList();
            List<string> lsRslt = new List<string>();

            webCnt++;
            if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
            
            url = "http://apis.data.go.kr/1611000/nsdi/LandUseService/attr/getLandUseAttr?serviceKey=" + api.RndSrvKey() + "&cnflcAt=1&format=xml&numOfRows=50&pageSize=10&pageNo=1&startPage=1&pnu=" + pnu;
            xml = net.GetHtml(url, Encoding.UTF8);
            if (xml.Contains("totalCount") == false)
            {
                Prc_Error("plan", idx);
                return;
            }

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            XmlNamespaceManager nsmg = new XmlNamespaceManager(doc.NameTable);
            nsmg.AddNamespace("n", doc.DocumentElement.NamespaceURI);
            XmlNode nd_total = doc.SelectSingleNode("/n:response/n:totalCount", nsmg);
            totalCnt = Convert.ToInt16(nd_total.InnerText);
            if (totalCnt == 0)
            {
                Prc_Error("plan", idx);
                return;
            }

            foreach (XmlNode item in doc.SelectNodes("/n:response/n:fields/n:field", nsmg))
            {
                prposAreaDstrcCode = item.SelectSingleNode("prposAreaDstrcCode", nsmg) == null ? "" : item.SelectSingleNode("prposAreaDstrcCode", nsmg).InnerText.Trim();
                prposAreaDstrcCodeNm = item.SelectSingleNode("prposAreaDstrcCodeNm", nsmg) == null ? "" : item.SelectSingleNode("prposAreaDstrcCodeNm", nsmg).InnerText.Trim();
                alCd.Add(prposAreaDstrcCode);
                alNm.Add(prposAreaDstrcCodeNm);

                foreach (DataRow row in dtUse.Rows)
                {                    
                    if (row["prps_cd"].ToString()== prposAreaDstrcCode)
                    {
                        if (lsRslt.Contains(row["level3"].ToString())) continue;
                        lsRslt.Add(row["level3"].ToString());
                    }
                }
            }

            prpsCd = String.Join(",", alCd.ToArray());
            prpsNm = String.Join(",", alNm.ToArray());

            if (lsRslt.Count > 0)
            {
                useCdtn = string.Join(",", lsRslt);
            }

            if (dbPrpsNm == string.Empty)
            {
                sql = "update ta_land set prps_cd='" + prpsCd + "', prps_nm='" + prpsNm + "', use_cdtn='" + useCdtn + "', plan_prc=1 where idx=" + idx;
            }
            else
            {
                sql = "update ta_land set prps_cd='" + prpsCd + "', use_cdtn='" + useCdtn + "', plan_prc=1 where idx=" + idx;
            }
            db.Open();
            db.ExeQry(sql);
            db.Close();
        }

        /// <summary>
        /// 개별공시지가 수집
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="idx">ta_land 의 idx</param>
        /// <param name="pnu"></param>
        private void Prc_Price(string tid, string idx, string pnu)
        {
            int totalCnt = 0;
            string sql, url, xml, cvp, html, jsData = "", src = "0";
            string ldCodeNm = "", mnnmSlno = "", stdrYear = "", stdrMt = "", pblntfPclnd = "", pblntfDe = "", lastUpdtDt = "";

            var jaPrice = new JArray();

            webCnt++;
            if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

            url = "http://apis.data.go.kr/1611000/nsdi/IndvdLandPriceService/attr/getIndvdLandPriceAttr?serviceKey=" + api.RndSrvKey() + "&format=xml&numOfRows=100&pageNo=1&pnu=" + pnu;
            xml = net.GetHtml(url, Encoding.UTF8);
            if (xml.Contains("totalCount") == false)
            {
                Prc_Error("price", idx);
                return;
            }

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            XmlNamespaceManager nsmg = new XmlNamespaceManager(doc.NameTable);
            nsmg.AddNamespace("n", doc.DocumentElement.NamespaceURI);
            XmlNode nd_total = doc.SelectSingleNode("/n:response/n:totalCount", nsmg);
            totalCnt = Convert.ToInt16(nd_total.InnerText);
            if (totalCnt == 0)
            {
                //Prc_Error("price", idx);
                //return;
                //실패시 2차로 토지e음 연동하여 매칭시도
                url = "https://www.eum.go.kr/web/ar/lu/luLandDetYearAjax.jsp?pnu=" + pnu;
                html = net.GetHtml(url).Trim();
                MatchCollection mc = Regex.Matches(html, @"<tr>\s+<td>(\d{4})/(\d{2})</td>\s+<td>([\d\,]+)원</td></tr>", RegexOptions.Singleline);
                if (mc != null)
                {
                    foreach (Match m in mc.Cast<Match>().Reverse())
                    {
                        var obj = new JObject();
                        obj.Add("stdrYear", m.Groups[1].Value);
                        obj.Add("pblntfPclnd", m.Groups[3].Value.Replace(",", string.Empty));
                        obj.Add("stdrMt", m.Groups[2].Value);
                        obj.Add("pblntfDe", string.Empty);
                        jaPrice.Add(obj);
                    }
                    src = "1";
                }
            }
            else
            {
                foreach (XmlNode item in doc.SelectNodes("/n:response/n:fields/n:field", nsmg))
                {
                    ldCodeNm = item.SelectSingleNode("ldCodeNm", nsmg) == null ? "" : item.SelectSingleNode("ldCodeNm", nsmg).InnerText;
                    mnnmSlno = item.SelectSingleNode("mnnmSlno", nsmg) == null ? "" : item.SelectSingleNode("mnnmSlno", nsmg).InnerText;
                    stdrYear = item.SelectSingleNode("stdrYear", nsmg).InnerText;
                    stdrMt = item.SelectSingleNode("stdrMt", nsmg).InnerText;
                    pblntfPclnd = item.SelectSingleNode("pblntfPclnd", nsmg).InnerText;
                    pblntfDe = item.SelectSingleNode("pblntfDe", nsmg) == null ? "" : item.SelectSingleNode("pblntfDe", nsmg).InnerText;
                    lastUpdtDt = item.SelectSingleNode("lastUpdtDt", nsmg).InnerText;

                    var obj = new JObject();
                    obj.Add("stdrYear", stdrYear);
                    obj.Add("pblntfPclnd", pblntfPclnd);
                    obj.Add("stdrMt", stdrMt);
                    obj.Add("pblntfDe", pblntfDe);
                    jaPrice.Add(obj);
                }
                src = "0";
            }

            if (jaPrice.Count == 0)
            {
                Prc_Error("price", idx);
                return;
            }

            jsData = jaPrice.ToString();
            db.Open();
            cvp = "js_data='" + jsData + "', src='" + src + "', wdt=curdate()";
            sql = "insert into ta_ilp set tid=" + tid + ", pnu=" + pnu + ", " + cvp + " ON DUPLICATE KEY UPDATE " + cvp;
            db.ExeQry(sql);

            sql = "update ta_land set price_prc=1 where idx=" + idx;
            db.ExeQry(sql);
            db.Close();

            txtState.AppendText(" -> OK");
        }

        private void Prc_Error(string dvsn, string idx)
        {
            string sql;

            db.Open();
            if (dvsn == "price")
            {
                sql = "update ta_land set price_prc=2 where idx=" + idx;
            }
            else
            {
                sql = "update ta_land set plan_prc=2 where idx=" + idx;
            }
            db.ExeQry(sql);
            db.Close();

            txtState.AppendText(" -> Fail");
        }

        /// <summary>
        /// 개별공시지가 업데이트(7월말~8월경 당해 데이터 반영)
        /// </summary>
        /// <param name="idx"></param>
        private void Prc_PriceUdt()
        {
            int dtCnt = 0, sucCnt = 0, totalCnt = 0;
            string sql, url, xml, idx, tid, pnu, jsData = "", src = "0";
            string ldCodeNm = "", mnnmSlno = "", stdrYear = "", stdrMt = "", pblntfPclnd = "", pblntfDe = "", lastUpdtDt = "";

            var jaPrice = new JArray();

            //진행 및 변경건
            sql = "SELECT idx,P.tid,P.pnu FROM ta_list L , ta_ilp P where L.tid=P.tid and sta1 in (11,13) and P.wdt < '2021-07-28' and src=0 order by idx";
            DataTable dt = db.ExeDt(sql);
            dtCnt = dt.Rows.Count;
                        
            foreach (DataRow row in dt.Rows)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);
                if (webCnt % 200 == 0) txtState.Text = string.Empty;

                txtState.AppendText(string.Format("\r\n{0} / {1}", webCnt, dtCnt));

                jaPrice.Clear();
                idx = row["idx"].ToString();
                tid = row["tid"].ToString();
                pnu = row["pnu"].ToString();

                url = "http://apis.data.go.kr/1611000/nsdi/IndvdLandPriceService/attr/getIndvdLandPriceAttr?serviceKey=" + api.RndSrvKey() + "&format=xml&numOfRows=100&pageNo=1&pnu=" + pnu;
                xml = net.GetHtml(url, Encoding.UTF8);
                if (xml.Contains("totalCount") == false)
                {
                    continue;
                }

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);
                XmlNamespaceManager nsmg = new XmlNamespaceManager(doc.NameTable);
                nsmg.AddNamespace("n", doc.DocumentElement.NamespaceURI);
                XmlNode nd_total = doc.SelectSingleNode("/n:response/n:totalCount", nsmg);
                totalCnt = Convert.ToInt16(nd_total.InnerText);
                if (totalCnt == 0)
                {
                    /*url = "https://www.eum.go.kr/web/ar/lu/luLandDetYearAjax.jsp?pnu=" + pnu;
                    html = net.GetHtml(url).Trim();
                    MatchCollection mc = Regex.Matches(html, @"<tr>\s+<td>(\d{4})/(\d{2})</td>\s+<td>([\d\,]+)원</td></tr>", RegexOptions.Singleline);
                    if (mc != null)
                    {
                        foreach (Match m in mc.Cast<Match>().Reverse())
                        {
                            var obj = new JObject();
                            obj.Add("stdrYear", m.Groups[1].Value);
                            obj.Add("pblntfPclnd", m.Groups[3].Value.Replace(",", string.Empty));
                            obj.Add("stdrMt", m.Groups[2].Value);
                            obj.Add("pblntfDe", string.Empty);
                            jaPrice.Add(obj);
                        }
                        src = "1";
                    }*/
                    continue;
                }
                else
                {
                    foreach (XmlNode item in doc.SelectNodes("/n:response/n:fields/n:field", nsmg))
                    {
                        ldCodeNm = item.SelectSingleNode("ldCodeNm", nsmg) == null ? "" : item.SelectSingleNode("ldCodeNm", nsmg).InnerText;
                        mnnmSlno = item.SelectSingleNode("mnnmSlno", nsmg) == null ? "" : item.SelectSingleNode("mnnmSlno", nsmg).InnerText;
                        stdrYear = item.SelectSingleNode("stdrYear", nsmg).InnerText;
                        stdrMt = item.SelectSingleNode("stdrMt", nsmg).InnerText;
                        pblntfPclnd = item.SelectSingleNode("pblntfPclnd", nsmg).InnerText;
                        pblntfDe = item.SelectSingleNode("pblntfDe", nsmg) == null ? "" : item.SelectSingleNode("pblntfDe", nsmg).InnerText;
                        lastUpdtDt = item.SelectSingleNode("lastUpdtDt", nsmg).InnerText;

                        var obj = new JObject();
                        obj.Add("stdrYear", stdrYear);
                        obj.Add("pblntfPclnd", pblntfPclnd);
                        obj.Add("stdrMt", stdrMt);
                        obj.Add("pblntfDe", pblntfDe);
                        jaPrice.Add(obj);
                    }
                    src = "0";
                }

                if (jaPrice.Count == 0)
                {
                    continue;
                }

                jsData = jaPrice.ToString();                
                sql = "update ta_ilp set js_data = '" + jsData + "', src = '" + src + "', udt = curdate() where idx=" + idx;
                db.Open();
                db.ExeQry(sql);
                db.Close();
                txtState.AppendText(" -> OK");
                sucCnt++;
            }

            MessageBox.Show("업데이트 완료-" + sucCnt.ToString());
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            bgwork.Dispose();

            this.Dispose();
            this.Close();
        }
    }
}
