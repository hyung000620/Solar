using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Solar
{
    public class AuctSms
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AuctCd auctCd = new AuctCd();

        DataTable dtCatCd;

        public AuctSms()
        {
            //물건종별 및 토지 지목
            dtCatCd = db.ExeDt("select cat1_cd, cat2_cd, cat3_cd, cat2_nm, cat3_nm, hide, bldg_type from ta_cd_cat where cat3_cd > 0 order by cat3_cd");
        }

        /// <summary>
        /// 경매-상태변경 문자
        /// </summary>
        public void StateChange()
        {
            string sql, tid, idx, shortUrl, sptNm, saNo, state, cat, sigu, wtm;
            string id, name, mobile, msg;

            //매각(낙찰)-바로발송, 그외-20분 지연발송
            wtm = DateTime.Now.AddMinutes(-20).ToString("HH:mm:ss");
            //sql = "select L.tid, spt, sn1, sn2, pn, cat3, adrs, idx, state from db_main.ta_list L , db_tank.tx_sms S where L.tid=S.tid and S.dvsn=0 and send=0 and wdt=curdate() and wtm < '" + wtm + "' order by idx";
            sql = $"select L.tid, spt, sn1, sn2, pn, cat3, adrs, idx, state from db_main.ta_list L , db_tank.tx_sms S where L.tid=S.tid and S.dvsn=0 and send=0 and wdt=curdate() and ((S.state='매각') or (S.state != '매각' and wtm < '{wtm}')) order by idx";
            DataTable dt = db.ExeDt(sql);

            foreach (DataRow row in dt.Rows)
            {
                idx = row["idx"].ToString();
                tid = row["tid"].ToString();
                sptNm = auctCd.FindCsNm(row["spt"].ToString());
                saNo = string.Format("{0}-{1}", row["sn1"], row["sn2"]);
                if (row["pn"].ToString() != "0") saNo = string.Format("{0}({1})", saNo, row["pn"]);
                state = row["state"].ToString();
                string[] adrs = row["adrs"].ToString().Split(new char[] { ' ' });
                if (adrs.Count() > 2) sigu = adrs[1];
                else sigu = string.Empty;

                var xCat = dtCatCd.Rows.Cast<DataRow>().Where(t => t["cat3_cd"].ToString() == row["cat3"].ToString()).SingleOrDefault();
                cat = (xCat == null || row["cat3"].ToString() == "0") ? string.Empty : xCat.Field<string>("cat3_nm");

                try
                {
                    shortUrl = GetShortenUrl(tid, idx);
                }
                catch
                {
                    shortUrl = string.Empty;
                }

                if (cat == "위험물저장및처리시설") cat = "위험물시설";
                else if (cat == "동물및식물관련시설") cat = "동식물시설";
                else if (cat == "분뇨및쓰레기처리시설") cat = "분뇨및쓰레기시설";
                else if (cat == "지식산업센터(아파트형공장)") cat = "지식산업센터";

                msg = string.Format("▣탱크옥션\n{0} {1}\n☞{2}\n{3}/{4}", sptNm, saNo, state, sigu, cat);

                if (shortUrl != string.Empty)
                {
                    msg = string.Format("{0}\n{1}", msg, "https://me2.do/" + shortUrl);
                }

                sql = "select distinct(M.id), name, mobile from db_tank.tm_member M , db_tank.tm_interest T , db_tank.tm_pay_result P where " +
                    "M.id=T.id and T.id=P.id and T.itype=1 and T.tid='" + tid + "' and T.sms=1 and validity >= curdate() and pay_code=100 and paykind < 5";
                DataTable dtL = db.ExeDt(sql);
                
                foreach (DataRow r in dtL.Rows)
                {
                    id = r["id"].ToString();
                    name = r["name"].ToString();
                    mobile = r["mobile"].ToString();

                    SendSms(id, name, mobile, msg);
                }
                
                sql = "update db_tank.tx_sms set short_url='" + shortUrl + "', send=1 where idx=" + idx;
                db.Open();
                db.ExeQry(sql);
                db.Close();
            }
        }

        /// <summary>
        /// 경/공매-인근 신건등록 문자
        /// </summary>
        public void NearBy()
        {
            string sql, tid, idx, shortUrl, sptNm, saNo, state, cat, sigu, wtm;
            string id, name, mobile, msg, title;
            double lat_p = 0, lng_p = 0, lat_s = 0, lng_s = 0, distance = 0, radius = 0;

            CoordCal cc = new CoordCal();

            sql = "select L.tid, spt, sn1, sn2, cat3, adrs, x, y, idx, state from db_main.ta_list L , db_tank.tx_sms S where L.tid=S.tid and S.dvsn=1 and send=0 and wdt=curdate() order by idx";
            DataTable dt = db.ExeDt(sql);

            foreach (DataRow row in dt.Rows)
            {
                idx = row["idx"].ToString();
                tid = row["tid"].ToString();
                sptNm = auctCd.FindCsNm(row["spt"].ToString());
                saNo = string.Format("{0}-{1}", row["sn1"], row["sn2"]);
                //if (row["pn"].ToString() != "0") saNo = string.Format("{0}({1})", saNo, row["pn"]);
                //state = row["state"].ToString();                
                string[] adrs = row["adrs"].ToString().Split(new char[] { ' ' });
                if (adrs.Count() > 2) sigu = adrs[1];
                else sigu = string.Empty;

                lng_p = Convert.ToDouble(row["x"]);     //경도
                lat_p = Convert.ToDouble(row["y"]);     //위도

                var xCat = dtCatCd.Rows.Cast<DataRow>().Where(t => t["cat3_cd"].ToString() == row["cat3"].ToString()).SingleOrDefault();
                cat = (xCat == null || row["cat3"].ToString() == "0") ? string.Empty : xCat.Field<string>("cat3_nm");

                try
                {
                    shortUrl = GetShortenUrl(tid, idx);
                }
                catch
                {
                    shortUrl = string.Empty;
                }

                if (cat == "위험물저장및처리시설") cat = "위험물시설";
                else if (cat == "동물및식물관련시설") cat = "동식물시설";
                else if (cat == "분뇨및쓰레기처리시설") cat = "분뇨및쓰레기시설";
                else if (cat == "지식산업센터(아파트형공장)") cat = "지식산업센터";

                /*
                msg = string.Format("▣탱크옥션\n{0} {1}\n☞{2}\n{3}/{4}", sptNm, saNo, state, sigu, cat);                

                if (shortUrl != string.Empty)
                {
                    msg = string.Format("{0}\n{1}", msg, "https://me2.do/" + shortUrl);
                }
                */
                sql = "select distinct(T.idx), M.id, name, mobile, x, y, radius, T.title from db_tank.tm_member M , db_tank.tm_nearby T , db_tank.tm_pay_result P where " +
                    "M.id=T.id and T.id=P.id and T.dvsn=0 and T.off=0 and validity >= curdate() and pay_code=100 and paykind < 5";
                DataTable dtL = db.ExeDt(sql);

                foreach (DataRow r in dtL.Rows)
                {
                    lng_s = Convert.ToDouble(r["x"]);
                    lat_s = Convert.ToDouble(r["y"]);
                    distance = cc.calDistance(lat_p, lng_p, lat_s, lng_s);
                    radius = Convert.ToDouble(r["radius"]);

                    if (distance >= 0 && distance <= radius)
                    {
                        id = r["id"].ToString();
                        name = r["name"].ToString();
                        mobile = r["mobile"].ToString();
                        title = r["title"].ToString().Replace(" ", string.Empty).Trim();
                        if (title.Length > 12) title = title.Substring(0, 12);
                        msg = string.Format("★경매개시★\n{0} {1}\n▶{2}", sptNm, saNo, title);
                        if (shortUrl != string.Empty)
                        {
                            msg = string.Format("{0}\n{1}", msg, "https://me2.do/" + shortUrl);
                        }
                        SendSms(id, name, mobile, msg);
                    }                    
                }

                sql = "update db_tank.tx_sms set short_url='" + shortUrl + "', send=1 where idx=" + idx;
                db.Open();
                db.ExeQry(sql);
                db.Close();
            }
        }

        private string GetShortenUrl(string tid, string idx)
        {
            string url, jsData = string.Empty, shortUrl = string.Empty, param = string.Empty;

            param = tid.PadLeft(7, '0') + idx.PadLeft(8, '0');

            url = "https://openapi.naver.com/v1/util/shorturl";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Headers.Add("X-Naver-Client-Id", "kTilJOBmvWOY_tjlLkW9"); // 개발자센터에서 발급받은 Client ID
            request.Headers.Add("X-Naver-Client-Secret", "nydLbVMk3P");       // 개발자센터에서 발급받은 Client Secret
            request.Method = "POST";
            string query = "https://m.tankauction.com/NT/?msgNo=" + param; // 단축할 URL 대상
            byte[] byteDataParams = Encoding.UTF8.GetBytes("url=" + query);
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = byteDataParams.Length;
            Stream st = request.GetRequestStream();
            st.Write(byteDataParams, 0, byteDataParams.Length);
            st.Close();
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            Stream stream = response.GetResponseStream();
            StreamReader reader = new StreamReader(stream, Encoding.UTF8);
            jsData = reader.ReadToEnd();
            stream.Close();
            response.Close();
            reader.Close();

            dynamic x = JsonConvert.DeserializeObject(jsData);
            if (jsData.Contains("exception")) shortUrl = string.Empty;
            else
            {
                var result = x["result"];
                if (result != null)
                {
                    shortUrl = result["hash"];
                }
            }

            return shortUrl;
        }

        public void SendSms(string id, string name, string mobile, string msg)
        {
            string sql;

            MySqlConnection dbSmsCon = new MySqlConnection("SERVER=118.67.130.230;port=3307;DATABASE=db_sms;UID=tanksms;PASSWORD=k2sms1544~!;convert zero datetime=True");

            sql = "insert into SC_TRAN set TR_SENDDATE=now(), TR_SENDSTAT=0, TR_MSGTYPE=0, TR_PHONE='" + mobile + "', TR_CALLBACK='02-456-1544', TR_MSG='" + msg + "', TR_ETC1='tankauction', TR_ETC2='" + id + "', TR_ETC3='solar'";
            MySqlCommand cmd = new MySqlCommand(sql, dbSmsCon);            
            dbSmsCon.Open();
            cmd.ExecuteNonQuery();
            //cmd.Parameters.Clear();
            cmd.Dispose();
            dbSmsCon.Close();
        }
    }
}
