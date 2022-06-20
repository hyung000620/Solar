using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace Solar
{
    public class ApiUtil
    {
        public List<string> dataKeyList;           //data.go.kr
        public List<string> daumGeoRestKeyList;    //Daum 지도/로컬 REST API
        public ApiUtil()
        {
            dataKeyList = new List<string>
            {
                "2oPHMZ1hwYianSHElR%2FPSOVku9zZKAXL9y3I8sua76J1HebhA4g%2FX17oW3YcZxn81j8pazjAggWF7b1n0C0aRw%3D%3D",   //한진근
                "Uz7zoX%2B4Y52BYtZJBCbW5BARxGbmZfwziqAliIF5eUal6Qt8UgpLGHr55EetdD7ar5LJntya7D5s4wEoeNTGXQ%3D%3D",     //김민영
                "XR4jwbtdOcSM6fslCg1uOTa72tfR4vEL1111LBMsW6XV1zu2es5U4IEFp1GX%2BKh6KXX8yuNLzJTE0W5amrsn6A%3D%3D",     //황예리
                "UAl5t9pQpC04NrK4h9pm9xwThUXO%2FtWwn6kF4plvTy1yICFpdURrKBFh9YB0OdOK545WrdpwNaSJGnS5COiwJA%3D%3D",     //문현희
                "qAoTJKAXurgcC2LutoKAPaujjgRFT7PH%2BYBbGnPg4Sq9d9PAQN1qhqP5dj8MzpPHzvY4R7ECiyhnzZ%2BcbypGfw%3D%3D",   //김연
                "SRcfm4I20SSjKxwkt%2F2HRHkJnf3s7CSRydX2i7cw7rU3Xt42Ty4da5haTRyhf3ogPCCPT9jtbb7GbAa7598Fxw%3D%3D",     //오창진
                "zsbPIZ1BfPEi3D9NiIz4imeOk2Q6dvBEAIoReic8zYebQnsb8UwO2awDgFxctyShJjqn1h07UL2kslKuhcwcWQ%3D%3D",       //김민주
                "s0O3kig4XNNlWE9Dl9o%2Fvg%2FXAbo2Fdae02yQi0alKwFPdRDawMm2%2B5CTJKZL6kpsrbHEU74ZG5XWYMMLiWrptQ%3D%3D"  //김송이
            };

            daumGeoRestKeyList = new List<string>
            {
                "65c959824df84a3466f97c4d9a255be7", //한진근
                "2b6121314da7dde7b7f8dc0e70b86d64", //김범태
                "1e61410b59cc7f30357f470a607a9ffd", //김민영
                "bfc42261c2c134d417f862b5423b6344", //황예리                
                "0118c26bd156945639f05ea7b95a6d19", //문현희
                "54076a96f9f13be07fa136581afcda9e", //김연
                "9b3fed68e2600de364f3dd89fba103ff", //오창진
                "a0385df6172299d47f33857a40903ea8", //김민주
                "1d183b0f9319d28945a0a6d71ecda7cd"  //김송이
            };
        }

        public string RndSrvKey(List<string> keyList = null)
        {
            keyList = (keyList == null) ? dataKeyList : keyList;
            Random rnd = new Random();
            int rndIdx = rnd.Next(keyList.Count);

            return keyList[rndIdx];
        }

        /// <summary>
        /// 네이버 주소 -> 좌표 변환(미사용-다음으로 전환)
        /// </summary>
        /// <param name="addr"></param>
        /// <param name="bunji"></param>
        /// <param name="retXml"></param>
        /// <returns></returns>
        public Dictionary<string, string> GetNaverGeocode(string addr, string bunji, bool retXml = false)
        {
            int total = 0;
            string url = "", content = "", x = "", y = "";

            Dictionary<string, string> dic = new Dictionary<string, string>();
            dic.Add("x", "0");
            dic.Add("y", "0");
            dic.Add("xml", "");

            addr = addr.Trim();
            if (addr.Contains(" 세종시")) addr = addr.Replace(" 세종시", string.Empty);

            HttpWebRequest req = null;
            HttpWebResponse res = null;
            url = "https://openapi.naver.com/v1/map/geocode.xml?query=" + addr;
            try
            {
                req = (HttpWebRequest)WebRequest.Create(url);
                req.Headers.Add("X-Naver-Client-Id", "CB6yYuXTlV1A85E1bGrn");
                req.Headers.Add("X-Naver-Client-Secret", "f1l5tJYMSY");
                res = (HttpWebResponse)req.GetResponse();
                StreamReader sr = new StreamReader(res.GetResponseStream(), Encoding.UTF8);
                content = sr.ReadToEnd();
                sr.Close();
                res.Close();

                if (retXml) dic["xml"] = content;

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(content);
                XmlNamespaceManager nsmg = new XmlNamespaceManager(doc.NameTable);
                nsmg.AddNamespace("n", doc.DocumentElement.NamespaceURI);
                XmlNode node = doc.SelectSingleNode("/n:result/n:total", nsmg);
                total = Convert.ToInt16(node.InnerText);

                if (total == 0) return dic;
                foreach (XmlNode item in doc.SelectNodes("/n:result/n:items/n:item", nsmg))
                {
                    XmlNode node_rest = item.SelectSingleNode("n:addrdetail/n:rest", nsmg);
                    if (node_rest == null)  //xml결과에서 번지 항목이 없을 경우(예외발생)
                    {
                        return dic;
                    }
                    if (node_rest.InnerText.Replace("-0", string.Empty) == bunji)    //번지항목이 동일하면 좌표를 가져온다
                    {
                        XmlNode node_x = item.SelectSingleNode("n:point/n:x", nsmg);
                        XmlNode node_y = item.SelectSingleNode("n:point/n:y", nsmg);
                        x = node_x.InnerText;
                        y = node_y.InnerText;
                        dic["x"] = x;
                        dic["y"] = y;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                content = "HttpWebException-" + ex.Message;
            }

            return dic;
        }

        /// <summary>
        /// (미사용)다음 주소 -> 좌표 변환
        /// </summary>
        /// <param name="addr"></param>
        /// <param name="bunji"></param>
        /// <param name="retXml"></param>
        /// <returns></returns>
        public Dictionary<string, string> GetDaumGeocode(string addr, string jibun, bool retXml = false)
        {
            string url = "", apiKey = "", content = "", daumJibun = "";

            Dictionary<string, string> dic = new Dictionary<string, string>();
            dic.Add("x", "0");
            dic.Add("y", "0");
            dic.Add("xml", "");

            addr = addr.Trim();
            if (addr.Contains(" 세종시")) addr = addr.Replace(" 세종시", string.Empty);

            apiKey = RndSrvKey(daumGeoRestKeyList);
            HttpWebRequest req = null;
            HttpWebResponse res = null;
            url = "https://dapi.kakao.com/v2/local/search/address.json?query=" + addr + "&size=30";
            try
            {
                req = (HttpWebRequest)WebRequest.Create(url);
                req.Headers.Add("Authorization", "KakaoAK " + apiKey);
                res = (HttpWebResponse)req.GetResponse();
                StreamReader sr = new StreamReader(res.GetResponseStream(), Encoding.UTF8);
                content = sr.ReadToEnd();
                sr.Close();
                res.Close();

                JObject jobj = JObject.Parse(content);
                if (retXml) dic["xml"] = jobj.ToString();
                if (jobj["meta"]["total_count"].ToString() == "0")
                {
                    //MessageBox.Show(dic["xml"]);
                    return dic;
                }

                JArray arr = JArray.Parse(jobj["documents"].ToString());
                foreach (JObject item in arr)
                {
                    daumJibun = string.Empty;
                    if (item["address"]["mountain_yn"].ToString() == "Y")
                    {
                        daumJibun = "산";
                    }
                    daumJibun += item["address"]["main_address_no"].ToString();
                    if (item["address"]["sub_address_no"].ToString() != "")
                    {
                        daumJibun += "-" + item["address"]["sub_address_no"].ToString();
                    }
                    //MessageBox.Show(daumBunji + " / " + bunji);
                    if (jibun == daumJibun)
                    {   
                        //mysql float 타입은 소수점 아래 자리수가 많을 경우 정확한 값을 비교 못하므로 7자리만 취하고 절사처리 한다.(원래값은 12자리)
                        dic["x"] = item["address"]["x"].ToString();
                        dic["y"] = item["address"]["y"].ToString();
                        dic["x"] = (Math.Truncate(Convert.ToDouble(dic["x"]) * 10000000) / 10000000).ToString();
                        dic["y"] = (Math.Truncate(Convert.ToDouble(dic["y"]) * 10000000) / 10000000).ToString();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                content = "HttpWebException-" + ex.Message;
                //MessageBox.Show(ex.Message);
                if (retXml) dic["xml"] = content;
            }

            return dic;
        }

        /// <summary>
        /// 다음 주소 검색
        /// </summary>
        /// <param name="adrs"></param>
        public IDictionary<string, string> DaumSrchAdrs(string adrs)
        {
            string url = "", json = "", mt = "";

            IDictionary<string, string> dict = new Dictionary<string, string>()
            {
                { "qry", "" },
                { "retJson", "" },
                { "totalCnt", "" },
                { "adrsType", "" },
                { "x", "" },
                { "y", "" },
                { "jbAdrsNm", "" },
                { "jbD1Nm", "" },
                { "jbD2Nm", "" },
                { "jbD3Nm", "" },
                { "hNm", "" },
                { "hCd", "" },
                { "bCd", "" },
                { "mt", "" },
                { "jbNoM", "" },
                { "jbNoS", "" },
                //{ "zipCd", "" },
                { "rdAdrsNm", "" },
                { "rdD1Nm", "" },
                { "rdD2Nm", "" },
                { "rdD3Nm", "" },
                { "rdNm", "" },
                { "under", "" },
                { "bldgNoM", "" },
                { "bldgNoS", "" },
                { "bldgNm", "" },
                { "zoneNo", "" },
                { "pnu", "" },
                { "sidoCd","" },
                { "gugunCd","" },
                { "dongCd","" },
                { "riCd","" }
            };

            adrs = Regex.Replace(adrs, @"외[ ]*\d+[ ]*건", string.Empty);
            adrs = adrs.Trim();
            url = "https://dapi.kakao.com/v2/local/search/address.json?query=" + adrs + "&size=1";
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Headers.Add("Authorization", "KakaoAK " + RndSrvKey(daumGeoRestKeyList));
                HttpWebResponse res = (HttpWebResponse)req.GetResponse();
                StreamReader sr = new StreamReader(res.GetResponseStream(), Encoding.UTF8);
                json = sr.ReadToEnd();
                sr.Close();
                res.Close();

                JObject obj = JObject.Parse(json);
                dict["retJson"] = obj.ToString();

                if (obj["meta"]["total_count"].ToString().Trim() == "0" || obj["meta"]["total_count"].ToString().Trim() == string.Empty)
                {
                    dict["totalCnt"] = "0";
                    return dict;
                }

                dict["totalCnt"] = obj["meta"]["total_count"].ToString().Trim();

                var jt = obj.SelectToken("documents");
                dict["qry"] = jt[0]["address_name"].ToString();
                dict["adrsType"] = jt[0]["address_type"].ToString();
                dict["x"] = jt[0]["x"].ToString();
                dict["y"] = jt[0]["y"].ToString();
                dict["x"] = (Math.Truncate(Convert.ToDouble(dict["x"]) * 10000000) / 10000000).ToString();
                dict["y"] = (Math.Truncate(Convert.ToDouble(dict["y"]) * 10000000) / 10000000).ToString();

                if (jt[0]["address"].HasValues)
                {
                    var jb = jt[0]["address"];
                    dict["jbAdrsNm"] = jb["address_name"].ToString();
                    dict["jbD1Nm"] = jb["region_1depth_name"].ToString();
                    dict["jbD2Nm"] = jb["region_2depth_name"].ToString();
                    dict["jbD3Nm"] = jb["region_3depth_name"].ToString();
                    dict["hNm"] = jb["region_3depth_h_name"].ToString();
                    dict["hCd"] = jb["h_code"].ToString();
                    dict["bCd"] = jb["b_code"].ToString();
                    dict["mt"] = jb["mountain_yn"].ToString();
                    dict["jbNoM"] = jb["main_address_no"].ToString();
                    dict["jbNoS"] = jb["sub_address_no"].ToString();
                    //dict["zipCd"] = jb["zip_code"].ToString();    //6자리 우편번호 폐기(2020.05.11)

                    if (dict["bCd"] != "")
                    {
                        dict["sidoCd"] = dict["bCd"].Substring(0, 2);
                        dict["gugunCd"] = dict["bCd"].Substring(2, 3);
                        dict["dongCd"] = dict["bCd"].Substring(5, 3);
                        dict["riCd"] = dict["bCd"].Substring(8);
                        if (dict["jbNoM"] != "")
                        {
                            mt = (dict["mt"] == "N") ? "1" : "2";   //1:일반, 2:산, 3...9  https://landmapr.blog.me/221554299792
                            dict["pnu"] = dict["bCd"] + mt + dict["jbNoM"].PadLeft(4, '0') + dict["jbNoS"].PadLeft(4, '0');
                            dict["mt"] = mt;
                        }
                        else
                        {
                            dict["x"] = "";
                            dict["y"] = "";
                        }
                    }
                }
                if (jt[0]["road_address"].HasValues)
                {
                    var rd = jt[0]["road_address"];
                    dict["rdAdrsNm"] = rd["address_name"].ToString();
                    dict["rdD1Nm"] = rd["region_1depth_name"].ToString();
                    dict["rdD2Nm"] = rd["region_2depth_name"].ToString();
                    dict["rdD3Nm"] = rd["region_3depth_name"].ToString();
                    dict["rdNm"] = rd["road_name"].ToString();
                    dict["under"] = rd["underground_yn"].ToString();
                    dict["bldgNoM"] = rd["main_building_no"].ToString();
                    dict["bldgNoS"] = rd["sub_building_no"].ToString();
                    dict["bldgNm"] = rd["building_name"].ToString();
                    dict["zoneNo"] = rd["zone_no"].ToString();
                }
            }
            catch(Exception ex)
            {
                dict["retJson"] = "FAIL-" + ex.Message;
            }

            return dict;
        }
    }
}
