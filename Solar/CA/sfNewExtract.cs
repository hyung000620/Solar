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

using System.Text.RegularExpressions;
using HtmlAgilityPack;
using hap = HtmlAgilityPack;
using HAPDoc = HtmlAgilityPack.HtmlDocument;
using System.Xml;

namespace Solar.CA
{
    public partial class sfNewExtract : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AuctCd auctCd = new AuctCd();
        ApiUtil api = new ApiUtil();

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        DataTable dtDptCd, dtCatCd, dtStateCd, dtFlrCd, dtLeasUseCd;         //법원계, 물건종별, 진행상태, 건물층, 임차인-용도코드
        DataTable dtCarCoCd, dtCarMoCd, dtCarFuel, dtCarTrans;  //차량-제조사, 모델그룹, 사용연료, 변속기형식        
        DataTable dtEtcCd;      //기타 모든 코드

        DataGridView dgL;
        //토지 패턴
        string landPtrn = "대|전|답|과수원|목장용지|임야|광천지|염전|대지|공장용지|학교용지|주차장|주유소용지|창고용지|도로|철도용지|제방|하천|구거|유지|양어장|수도용지|공원|체육용지|유원지|종교용지|사적지|묘지|잡종지";

        public sfNewExtract()
        {
            InitializeComponent();

            init();
        }

        private void init()
        {
            //물건종별 코드
            dtCatCd = db.ExeDt("select cat1_cd, cat2_cd, cat3_cd, cat3_nm, hide from ta_cd_cat where cat3_cd > 0");

            //진행상태 코드
            dtStateCd = db.ExeDt("select sta1_cd, sta2_cd, sta1_nm, sta2_nm from ta_cd_sta order by sta2_cd");

            //층수 코드
            dtFlrCd = db.ExeDt("select flr_cd, flr_nm from ta_cd_flr");

            //기타 모든 코드
            dtEtcCd = db.ExeDt("select * from ta_cd_etc order by seq, cd");

            //차량-제조사, 모델그룹, 변속기형식, 사용연료 코드
            dtCarCoCd = db.ExeDt("select co_cd, rx from ta_cd_carco where co_cd != 6");
            dtCarMoCd = db.ExeDt("select co_cd, mo_cd, rx from ta_cd_carmo");

            dtCarTrans = dtEtcCd.Select("dvsn=14").CopyToDataTable();
            dtCarFuel = dtEtcCd.Select("dvsn=15").CopyToDataTable();

            this.Shown += SfNewExtract_Shown;
        }

        private void SfNewExtract_Shown(object sender, EventArgs e)
        {
            wfCaMgmt prnt = (wfCaMgmt)this.Owner;

            txtTid.Text = prnt.lnkTid.Text;
            dgL = prnt.dgL;
        }

        private void btnExtract_Click(object sender, EventArgs e)
        {
            string sql, tid, bidDt, staCd, extrCase, url, html;
            string spt, sn1, sn2, pn, jiwonNm, saNo;

            bool flagLs = false, flagState = false;

            foreach (Control ctrl in this.Controls)
            {
                if (ctrl.GetType() != typeof(CheckBox)) continue;
                CheckBox chk = (CheckBox)ctrl;
                if (chk == chkLs)
                {
                    if (chk.Checked) flagLs = true;
                }
                else
                {
                    if (chk.Checked) flagState = true;
                }
            }
            if (!flagLs && !flagState)
            {
                MessageBox.Show("추출할 항목을 체크 해 주세요.");
                return;
            }

            tid = txtTid.Text;
            sql = "select * from ta_list where tid=" + tid;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            spt = dr["spt"].ToString();
            sn1 = dr["sn1"].ToString();
            sn2 = dr["sn2"].ToString();
            pn = dr["pn"].ToString();
            staCd = dr["sta2"].ToString();
            bidDt = dr["bid_dt"].ToString();
            dr.Close();
            db.Close();

            extrCase = (DateTime.Now.AddDays(+14) >= Convert.ToDateTime(bidDt) || staCd == "1111") ? "A" : "B";    //A-본공고(유찰포함), B-선행공고
            HAPDoc doc = new HAPDoc();

            jiwonNm = auctCd.LawNmEnc(csCd: string.Format("{0}", spt));
            saNo = string.Format("{0}0130{1}", sn1, sn2.PadLeft(6, '0'));
            pn = (pn == "0") ? "1" : pn;

            if (extrCase == "A")
            {
                if (flagLs)
                {
                    url = "http://www.courtauction.go.kr/RetrieveRealEstDetailInqSaList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
                    html = net.GetHtml(url);
                    if (html.Contains("공고중인 물건이 아닙니다") || html.Contains("존재하지 않는 페이지입니다"))
                    {
                        MessageBox.Show("법원에서 [사건내역] 페이지를 볼 수 없습니다.");
                        //return;
                    }
                    else
                    {
                        doc.LoadHtml(html);
                        list_A(tid, pn, doc);
                    }                    
                }

                if (flagState)
                {
                    url = "https://www.courtauction.go.kr/RetrieveRealEstCarHvyMachineMulDetailInfo.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&maemulSer=" + pn;
                    html = net.GetHtml(url);
                    if (html.Contains("공고중인 물건이 아닙니다") || html.Contains("존재하지 않는 페이지입니다"))
                    {
                        MessageBox.Show("법원에서 [물건상세] 페이지를 볼 수 없습니다.");
                        return;
                    }
                    else
                    {
                        doc.LoadHtml(html);
                        landBldg_A(tid, doc);
                        envrnUseState(tid, doc);    //주변환경 및 이용상태
                    }
                }
            }
            else
            {
                MessageBox.Show("[정식공고]전인 물건 입니다.");
                return;
                //landBldg_B(tid);
            }

            if (flagState)
            {
                aprvDt(tid);    //사용승인일자
                bldgStruct(tid, jiwonNm, saNo);     //건물구조
            }

            MessageBox.Show("처리 되었습니다.");
            ((wfCaMgmt)this.Owner).dg_SelectionChanged(null, null);

            //this.Dispose();
            //this.Close();            
        }

        /// <summary>
        /// 본공고-목록내역 추출
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="doc"></param>
        private void list_A(string tid, string pn, HAPDoc doc)
        {
            string lsNo, adrs, pin, sql;
            int lotCnt = 0, hoCnt = 0;
            IDictionary<string, string> dict = new Dictionary<string, string>();

            DataTable dtLs = new DataTable();
            dtLs.Columns.Add("no");
            dtLs.Columns.Add("adrs");
            dtLs.Columns.Add("pin");
            dtLs.Columns.Add("dvsn");
            dtLs.Columns.Add("note");
            dtLs.Columns.Add("sidoCd");
            dtLs.Columns.Add("gugunCd");
            dtLs.Columns.Add("dongCd");
            dtLs.Columns.Add("riCd");
            dtLs.Columns.Add("hCd");
            dtLs.Columns.Add("pnu");
            dtLs.Columns.Add("zoneNo");
            dtLs.Columns.Add("x");
            dtLs.Columns.Add("y");

            HtmlNodeCollection ncTbl = doc.DocumentNode.SelectNodes("//table[@summary='물건내역 표']");
            if (ncTbl == null || doc.DocumentNode.InnerText.Contains("등록된 물건내역이 없습니다"))
            {
                //해당 사건이 종결/정지/중복/병합 기타 사유로 물건내역이 존재하지 않으므로 목록내역에서 취한다.
                HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='목록내역 표']/tbody/tr");
                if (ncTr != null)
                {
                    foreach (HtmlNode ndTr in ncTr)
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./td");
                        lsNo = Regex.Match(ncTd[0].InnerText, @"\d+").Value;
                        adrs = ncTd[1].InnerText.Trim();
                        adrs = Regex.Replace(adrs, @"[\s]{2,}", " ");
                        pin = Regex.Match(ncTd[1].InnerHtml, @"regiBu\('(\d+)'\)", RegexOptions.IgnoreCase).Groups[1].Value;

                        dict.Clear();
                        dict = api.DaumSrchAdrs(adrs);
                        DataRow dr = dtLs.NewRow();
                        dr["no"] = lsNo;
                        dr["adrs"] = adrs;
                        dr["pin"] = pin;
                        dr["dvsn"] = ncTd[2].InnerText.Trim();
                        dr["note"] = ncTd[3].InnerText.Trim();
                        dr["sidoCd"] = dict["sidoCd"];
                        dr["gugunCd"] = dict["gugunCd"];
                        dr["dongCd"] = dict["dongCd"];
                        dr["riCd"] = dict["riCd"];
                        dr["hCd"] = dict["hCd"];
                        dr["pnu"] = dict["pnu"];
                        dr["zoneNo"] = dict["zoneNo"];
                        dr["x"] = dict["x"];
                        dr["y"] = dict["y"];
                        dtLs.Rows.Add(dr);
                    }
                }
            }
            else
            {
                foreach (HtmlNode tbl in ncTbl)
                {
                    if (tbl.SelectSingleNode("./tr/td").InnerText.Trim() != pn) continue;
                    HtmlNodeCollection ncTr = tbl.SelectNodes("./tr");
                    foreach (HtmlNode ndTr in ncTr)
                    {
                        HtmlNodeCollection ncTd = ndTr.SelectNodes("./th|./td");
                        string colName = ncTd[0].InnerText.Trim();
                        if (ncTd[0].Name == "th" && colName.Contains("목록"))
                        {
                            lsNo = Regex.Match(colName, @"\d+").Value;
                            adrs = ncTd[1].InnerText.Trim();
                            adrs = Regex.Replace(adrs, @"[\s]{2,}", " ");
                            pin = Regex.Match(ncTd[1].InnerHtml, @"regiBu\('(\d+)'\)", RegexOptions.IgnoreCase).Groups[1].Value;

                            dict.Clear();
                            dict = api.DaumSrchAdrs(adrs);
                            DataRow dr = dtLs.NewRow();
                            dr["no"] = lsNo;
                            dr["adrs"] = adrs;
                            dr["pin"] = pin;
                            dr["dvsn"] = ncTd[3].InnerText.Trim();
                            dr["note"] = ncTd[5].InnerText.Trim();
                            dr["sidoCd"] = dict["sidoCd"];
                            dr["gugunCd"] = dict["gugunCd"];
                            dr["dongCd"] = dict["dongCd"];
                            dr["riCd"] = dict["riCd"];
                            dr["hCd"] = dict["hCd"];
                            dr["pnu"] = dict["pnu"];
                            dr["zoneNo"] = dict["zoneNo"];
                            dr["x"] = dict["x"];
                            dr["y"] = dict["y"];
                            dtLs.Rows.Add(dr);
                        }
                        if (ncTd[0].Name == "th" && colName.Contains("물건비고"))
                        {
                            //pdNote = ncTd[1].InnerText.Trim();
                        }
                    }
                }
            }

            //주소 재매칭 시도
            var rows = from DataRow r in dtLs.Rows where r["sidoCd"].ToString() == string.Empty select r;
            if (rows != null)
            {
                foreach (DataRow r in rows)
                {
                    dict.Clear();
                    AdrsParser parser = new AdrsParser(r["adrs"].ToString());
                    //MessageBox.Show(parser.AdrsM);
                    dict = api.DaumSrchAdrs(parser.AdrsM);
                    r["sidoCd"] = dict["sidoCd"];
                    r["gugunCd"] = dict["gugunCd"];
                    r["dongCd"] = dict["dongCd"];
                    r["riCd"] = dict["riCd"];
                    r["hCd"] = dict["hCd"];
                    r["pnu"] = dict["pnu"];
                    r["zoneNo"] = dict["zoneNo"];
                    r["x"] = dict["x"];
                    r["y"] = dict["y"];
                }
            }

            List<MySqlParameter> sp = new List<MySqlParameter>();

            db.Open();
            sql = "delete from ta_ls where tid='" + tid + "'";
            db.ExeQry(sql);
            
            foreach (DataRow r in dtLs.Rows)
            {
                sql = "insert into ta_ls (tid, no, adrs, pin, dvsn, note, si_cd, gu_cd, dn_cd, ri_cd, hj_cd, pnu, x, y, zone_no) ";
                sql += "values (@tid, @no, @adrs, @pin, @dvsn, @note, @si_cd, @gu_cd, @dn_cd, @ri_cd, @hj_cd, @pnu, @x, @y, @zone_no)";
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@no", r["no"]));
                sp.Add(new MySqlParameter("@adrs", r["adrs"]));
                sp.Add(new MySqlParameter("@pin", r["pin"]));
                sp.Add(new MySqlParameter("@dvsn", r["dvsn"]));
                sp.Add(new MySqlParameter("@note", r["note"]));
                sp.Add(new MySqlParameter("@si_cd", r["sidoCd"]));
                sp.Add(new MySqlParameter("@gu_cd", r["gugunCd"]));
                sp.Add(new MySqlParameter("@dn_cd", r["dongCd"]));
                sp.Add(new MySqlParameter("@ri_cd", r["riCd"]));
                sp.Add(new MySqlParameter("@hj_cd", r["hCd"]));
                sp.Add(new MySqlParameter("@pnu", r["pnu"]));
                sp.Add(new MySqlParameter("@x", r["x"]));
                sp.Add(new MySqlParameter("@y", r["y"]));
                sp.Add(new MySqlParameter("@zone_no", r["zoneNo"]));
                db.ExeQry(sql, sp);
                sp.Clear();
                
                //필지수
                if (r["dvsn"].ToString() == "토지" && r["note"].ToString() == "미종국")
                {
                    lotCnt++;
                }

                //호개수
                if (r["dvsn"].ToString() == "집합건물" && r["note"].ToString() == "미종국")
                {
                    hoCnt++;
                }
            }

            if (lotCnt > 0)
            {
                sql = "update ta_list set lot_cnt='" + lotCnt.ToString() + "' where tid='" + tid + "'";
                db.ExeQry(sql);
            }

            if (hoCnt > 0)
            {
                sql = "update ta_list set ho_cnt='" + hoCnt.ToString() + "' where tid='" + tid + "'";
                db.ExeQry(sql);
            }

            db.Close();
        }

        /// <summary>
        /// 본공고-물건상세에서 추출
        /// </summary>
        /// <param name="tid"></param>
        private void landBldg_A(string tid, HAPDoc doc)
        {
            string sql = "", lsNo = "", lsType = "", catNm = "", catCd = "", floor = "", flrCd = "", shrStr0 = "", shrStr = "", frtn = "", dtlStr = "", etcStr = "", use = "", strt = "", area = "", lotCnt = "";
            string landSection = "", bldgSection = "";
            double sqm = 0, totSqm = 0, bldgSqm = 0, totShrSqm = 0, dt = 0, nt = 0;
            double sumLandSqm = 0, sumLandTotSqm = 0, sumRtSqm = 0, rtTotSqm = 0, sumBldgSqm = 0, sumBldgTotSqm = 0;
            bool macExist = false;
            int i = 0;

            string bldgPtrn = @"([지하옥탑상일이삼사오육칠팔구십단\d]+[층실])[ ]*(.*?[소실조택고장당원설점\)])*[ ]*(\d[\d\.\,]*)[ ]*㎡";
            string etcPtrn1 = @"\d+[\.\s]+\(용도\)(.*)\s+\(구조\)(.*)\s+\(면적\)[\D ]*(\d[\d\.\,]*)[ ]*([㎡주식개\d\*\(\)\w\, ]+)";     //제시외 패턴-1 (1-용도, 2-구조, 3-면적, 4-단위 및 기타)
            string etcPtrn2 = @"\d+[\.\s]+\(용도\)(.*)\s+\(구조\)(.*)";                                                                 //제시외 패턴-2 (1-용도, 2-구조) -> 패턴-1과 용도와 구조는 동일하나 면적부분이 없음
            string macPtrn = @"기계기구|[a-z]{4,}|\d{4}|\w+[\d]*\-\d+|kw|kva|ton|mm|kg";
            //string frtnPtrn1 = @"(\d+[\.\d]*)[ ]*분의[ ]*(\d+[\.\d]*)";   //분수 패턴-1(오류: 매각지분  : 1. 소유권대지권 42165.6분의 38.1945.) //2021-05-13
            string frtnPtrn1 = @"(\d+[\.]*[\d]*)[ ]*분의[ ]*(\d+[\.]*[\d]*)";   //분수 패턴-1
            string frtnPtrn2 = @"(\d+[\.]*[\d]*)/(\d+[\.]*[\d]*)";              //분수 패턴-2

            //토지용
            DataTable dtL = new DataTable();
            dtL.Columns.Add("lsNo");
            dtL.Columns.Add("multi");
            dtL.Columns.Add("catNm");
            dtL.Columns.Add("catCd");
            dtL.Columns.Add("sqm");
            dtL.Columns.Add("rtSqm");
            dtL.Columns.Add("totShrSqm");
            dtL.Columns.Add("totRtSqm");
            dtL.Columns.Add("frtn");
            dtL.Columns.Add("shrStr");

            //건물용
            DataTable dtB = new DataTable();
            dtB.Columns.Add("lsNo");
            dtB.Columns.Add("multi");
            dtB.Columns.Add("floor");
            dtB.Columns.Add("sqm");
            dtB.Columns.Add("totShrSqm");
            dtB.Columns.Add("shrStr");
            dtB.Columns.Add("tmpStr");
            dtB.Columns.Add("totFlr");

            //제시외
            DataTable dtE = new DataTable();
            dtE.Columns.Add("lsNo");
            dtE.Columns.Add("state");
            dtE.Columns.Add("struct");
            dtE.Columns.Add("sqm");

            //sql = "select * from ta_list where tid=" + tid;

            HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='목록내역 표']/tbody/tr");
            if (ncTr == null) return;

            List<MySqlParameter> sp = new List<MySqlParameter>();

            foreach (HtmlNode tr in ncTr)
            {
                sqm = 0; totSqm = 0; bldgSqm = 0; totShrSqm = 0; dt = 0; nt = 0;
                floor = ""; shrStr0 = ""; shrStr = ""; etcStr = ""; use = ""; strt = ""; area = "";

                HtmlNodeCollection ncTd = tr.SelectNodes("./td");
                if (ncTd.Count != 3) continue;
                lsNo = ncTd[0].InnerText.Trim();
                lsType = ncTd[1].InnerText.Trim();
                dtlStr = ncTd[2].InnerText.Replace("&nbsp;", string.Empty).Trim();
                dtlStr = Regex.Replace(dtlStr, @"[ ]*평방[ ]*미터|[ ]*제곱[ ]*미터", "㎡");
                //dtlStr = Regex.Replace(ncTd[2].InnerHtml, @"<[ㄱ-힣]+", string.Empty);  //처리불가 - 매각지분 : <경매할지분 공유자지분중 724분의215(갑구4) 엘티산업㈜ 소유지분>

                if (lsType == "토지")
                {
                    int eIndex = dtlStr.IndexOf("제시외");
                    if (eIndex > -1)
                    {
                        etcStr = dtlStr.Substring(eIndex);
                        dtlStr = dtlStr.Remove(eIndex);
                    }
                    dtlStr = landUnitConvert("토지", dtlStr);
                    Match match = Regex.Match(dtlStr, @"(" + landPtrn + "|null" + @")[ ]*(\d[\d\.\,]*)[ ]*㎡", rxOptM);
                    if (match.Success == false) continue;
                    catNm = match.Groups[1].Value.Trim();
                    if (catNm == "대") catNm = "대지";
                    var x = from DataRow r in dtCatCd.Rows
                            where r["cat2_cd"].ToString() == "1010" && r["cat3_nm"].ToString() == catNm
                            select r;
                    if (x.Count() > 0) catCd = x.CopyToDataTable().Rows[0]["cat3_cd"].ToString();
                    else catCd = "0";
                    //totSqm = Convert.ToDouble(match.Groups[2].Value.Replace(",", string.Empty));
                    totSqm = double.TryParse(match.Groups[2].Value.Replace(",", string.Empty), out double val) ? val : 0;
                    match = Regex.Match(dtlStr, @"매각지분[ ]*:[ ]*(.*)", rxOptS);

                    if (match.Success)
                    {
                        shrStr = match.Groups[1].Value;
                        shrStr = Regex.Replace(shrStr, @"제시외.*", string.Empty, rxOptS).Trim();
                        MatchCollection mc1 = Regex.Matches(shrStr, frtnPtrn1, rxOptS);
                        MatchCollection mc2 = Regex.Matches(shrStr, frtnPtrn2, rxOptS);
                        totShrSqm = totSqm;
                        sqm = 0;
                        if (mc1 != null)
                        {
                            foreach (Match m in mc1)
                            {
                                dt = Convert.ToDouble(m.Groups[1].Value);
                                nt = Convert.ToDouble(m.Groups[2].Value);
                                sqm += totShrSqm * nt / dt;
                            }
                        }
                        if (mc2 != null)
                        {
                            foreach (Match m in mc2)
                            {
                                dt = Convert.ToDouble(m.Groups[2].Value);
                                nt = Convert.ToDouble(m.Groups[1].Value);
                                sqm += totShrSqm * nt / dt;
                            }
                        }
                        if (mc1.Count == 0 && mc2.Count == 0)
                        {
                            totShrSqm = 0;
                            sqm = totSqm;
                        }
                        else
                        {
                            if (totShrSqm == sqm)
                            {
                                totShrSqm = 0;
                                shrStr = string.Empty;
                            }
                            else
                            {
                                shrStr = string.Format("전체면적 {0}㎡중 {1}", totShrSqm, shrStr);
                            }
                        }
                    }
                    else
                    {
                        sqm = totSqm;
                    }
                    dtL.Rows.Add(lsNo, 0, catNm, catCd, sqm, "", totShrSqm, 0, frtn, shrStr);

                    //제시외
                    if (etcStr != string.Empty)
                    {
                        MatchCollection mc = Regex.Matches(etcStr, etcPtrn1, rxOptM);
                        if (mc.Count > 0)
                        {
                            foreach (Match m in mc)
                            {
                                use = m.Groups[1].Value.Trim();
                                strt = m.Groups[2].Value.Trim();
                                area = m.Groups[3].Value.Trim();
                                if (use.Contains("기계기구"))
                                {
                                    macExist = true;
                                    continue;
                                }
                                if (m.Value.Contains("수목") || m.Value.Contains("나무") || m.Value.Contains("관정"))
                                {
                                    area = string.Empty;
                                }
                                else
                                {
                                    if (Regex.IsMatch(strt, macPtrn, rxOptM))
                                    {
                                        macExist = true;
                                        continue;
                                    }
                                }
                                dtE.Rows.Add(lsNo, use, strt, area);
                            }
                        }
                        else
                        {
                            mc = Regex.Matches(etcStr, etcPtrn2, rxOptM);
                            if (mc.Count > 0)
                            {
                                foreach (Match m in mc)
                                {
                                    use = m.Groups[1].Value.Trim();
                                    strt = m.Groups[2].Value.Trim();
                                    if (use.Contains("기계기구"))
                                    {
                                        macExist = true;
                                        continue;
                                    }
                                    if (m.Value.Contains("수목") || m.Value.Contains("나무") || m.Value.Contains("관정"))
                                    {
                                        area = string.Empty;
                                    }
                                    else
                                    {
                                        if (Regex.IsMatch(strt, macPtrn, rxOptM))
                                        {
                                            macExist = true;
                                            continue;
                                        }
                                    }
                                    dtE.Rows.Add(lsNo, use, strt, "");
                                }
                            }
                        }
                    }
                }
                else if (lsType == "건물")
                {
                    int eIndex = dtlStr.IndexOf("제시외");
                    if (eIndex > -1)
                    {
                        etcStr = dtlStr.Substring(eIndex);
                        dtlStr = dtlStr.Remove(eIndex);
                    }
                    dtlStr = landUnitConvert("건물", dtlStr);
                    dtlStr = Regex.Replace(dtlStr, @"\(.*?\)", string.Empty, rxOptS);   //하위 세부내역 면적 제외
                    string[] strArr = dtlStr.Split('\n');
                    foreach (string s in strArr)
                    {
                        floor = ""; sqm = 0; totShrSqm = 0; totSqm = 0;
                        string str = s.Replace("&nbsp;", string.Empty).Trim();
                        if (str == string.Empty) continue;

                        Match match = Regex.Match(str, bldgPtrn, RegexOptions.Multiline);
                        if (match.Success)
                        {
                            floor = match.Groups[1].Value;
                            //totSqm = Convert.ToDouble(match.Groups[3].Value.Replace(",", string.Empty));
                            totSqm = double.TryParse(match.Groups[3].Value.Replace(",", string.Empty), out double val) ? val : 0;
                            sqm = totSqm;
                            dtB.Rows.Add(lsNo, 0, floor, sqm, totShrSqm, "", match.Value, "");
                        }
                        else
                        {
                            match = Regex.Match(str, @"(\d[\d\.\,]+)[\s]*㎡", RegexOptions.Multiline);
                            if (match.Success)
                            {
                                str = match.Groups[1].Value.Replace(",", string.Empty).Trim();
                                str = Regex.Replace(str, @"\.$", string.Empty);     //0.53.㎡
                                //totSqm = Convert.ToDouble(str);
                                totSqm = double.TryParse(str, out double val) ? val : 0;
                                sqm = totSqm;
                                dtB.Rows.Add(lsNo, 0, floor, sqm, totShrSqm, "", match.Value, "");
                            }
                        }
                    }
                    Match matchShr = Regex.Match(dtlStr, @"매각지분[ ]*:[ ]*(.*)", rxOptS);
                    if (matchShr.Success)
                    {
                        shrStr0 = matchShr.Groups[1].Value;
                        shrStr0 = Regex.Replace(shrStr0, @"제시외.*", string.Empty, rxOptS).Trim();
                        MatchCollection mc1 = Regex.Matches(shrStr0, frtnPtrn1, rxOptS);
                        MatchCollection mc2 = Regex.Matches(shrStr0, frtnPtrn2, rxOptS);
                        foreach (DataRow row in dtB.Rows)
                        {
                            if (row["lsNo"].ToString() == lsNo)
                            {
                                if (mc1 == null && mc2 == null) continue;
                                totShrSqm = Convert.ToDouble(row["sqm"]);
                                sqm = 0;
                                if (mc1 != null)
                                {
                                    foreach (Match m in mc1)
                                    {
                                        dt = Convert.ToDouble(m.Groups[1].Value);
                                        nt = Convert.ToDouble(m.Groups[2].Value);
                                        sqm += totShrSqm * nt / dt;
                                    }
                                }
                                if (mc2 != null)
                                {
                                    foreach (Match m in mc2)
                                    {
                                        dt = Convert.ToDouble(m.Groups[2].Value);
                                        nt = Convert.ToDouble(m.Groups[1].Value);
                                        sqm += totShrSqm * nt / dt;
                                    }
                                }
                                if (mc1.Count == 0 && mc2.Count == 0)
                                {
                                    sqm = totShrSqm;
                                    totShrSqm = 0;
                                    shrStr = string.Empty;
                                }
                                else
                                {
                                    if (totShrSqm == sqm)
                                    {
                                        totShrSqm = 0;
                                        shrStr = string.Empty;
                                    }
                                    else
                                    {
                                        shrStr = string.Format("전체면적 {0}㎡중 {1}", totShrSqm, shrStr0);
                                    }
                                }
                                row["sqm"] = sqm;
                                row["totShrSqm"] = totShrSqm;
                                row["shrStr"] = shrStr;
                            }
                        }
                    }

                    //제시외
                    if (etcStr != string.Empty)
                    {
                        MatchCollection mc = Regex.Matches(etcStr, etcPtrn1, rxOptM);
                        if (mc.Count > 0)
                        {
                            foreach (Match m in mc)
                            {
                                use = m.Groups[1].Value.Trim();
                                strt = m.Groups[2].Value.Trim();
                                area = m.Groups[3].Value.Trim();
                                if (use.Contains("기계기구"))
                                {
                                    macExist = true;
                                    continue;
                                }
                                if (m.Value.Contains("수목") || m.Value.Contains("나무") || m.Value.Contains("관정"))
                                {
                                    area = string.Empty;
                                }
                                else
                                {
                                    if (Regex.IsMatch(strt, macPtrn, rxOptM))
                                    {
                                        macExist = true;
                                        continue;
                                    }
                                }
                                dtE.Rows.Add(lsNo, use, strt, area);
                            }
                        }
                        else
                        {
                            mc = Regex.Matches(etcStr, etcPtrn2, rxOptM);
                            if (mc.Count > 0)
                            {
                                foreach (Match m in mc)
                                {
                                    use = m.Groups[1].Value.Trim();
                                    strt = m.Groups[2].Value.Trim();
                                    if (use.Contains("기계기구"))
                                    {
                                        macExist = true;
                                        continue;
                                    }
                                    if (m.Value.Contains("수목") || m.Value.Contains("나무") || m.Value.Contains("관정"))
                                    {
                                        area = string.Empty;
                                    }
                                    else
                                    {
                                        if (Regex.IsMatch(strt, macPtrn, rxOptM))
                                        {
                                            macExist = true;
                                            continue;
                                        }
                                    }
                                    dtE.Rows.Add(lsNo, use, strt, "");
                                }
                            }
                        }
                    }
                }
                else if (lsType == "집합건물")
                {
                    bldgSection = string.Empty; landSection = string.Empty;
                    catNm = ""; catCd = ""; frtn = "";

                    int eIndex = dtlStr.IndexOf("제시외");
                    if (eIndex > -1)
                    {
                        etcStr = dtlStr.Substring(eIndex);
                    }

                    Match match = Regex.Match(dtlStr, @"전유부분의 건물의 표시(.*)대지권의 목적인 토지의 표시(.*)", rxOptS);
                    if (match.Success)
                    {
                        bldgSection = match.Groups[1].Value.Trim();
                        landSection = match.Groups[2].Value.Trim();
                    }
                    else
                    {
                        match = Regex.Match(dtlStr, @"전유부분의 건물의 표시(.*)", rxOptS);
                        if (match.Success)
                        {
                            bldgSection = match.Groups[1].Value.Trim();
                        }
                    }

                    if (bldgSection == string.Empty && landSection == string.Empty) continue;

                    if (bldgSection != string.Empty && landSection != string.Empty)
                    {
                        Match match3 = Regex.Match(dtlStr, @"건물의[ ]*번호[ ]*:[ ]*(.*?)구조[ ]*:[ ]*(.*?)면적[ ]*:[ ]*(.*?)대지권의 목적인 토지의 표시", rxOptS);
                        Match match4 = Regex.Match(dtlStr, @"건물의[ ]*번호[ ]*:[ ]*(.*?)구조[ ]*:[ ]*(.*?)대지권의 목적인 토지의 표시", rxOptS);
                        if (match3.Success)
                        {
                            MatchCollection mc = Regex.Matches(match3.Groups[2].Value + match3.Groups[3].Value, @"(\d[\d\.\,]*)[\s]*㎡", rxOptS);
                            if (mc != null)
                            {
                                foreach (Match m in mc)
                                {
                                    //bldgSqm += Convert.ToDouble(m.Groups[1].Value);
                                    bldgSqm += double.TryParse(m.Groups[1].Value, out double val) ? val : 0;
                                }
                                dtB.Rows.Add(lsNo, 1, floor, bldgSqm, "", "", "", "");

                            }
                        }
                        else if (match4.Success)
                        {
                            MatchCollection mc = Regex.Matches(match4.Groups[2].Value, @"(\d[\d\.\,]*)[\s]*㎡", rxOptS);
                            if (mc != null)
                            {
                                foreach (Match m in mc)
                                {
                                    //bldgSqm += Convert.ToDouble(m.Groups[1].Value);
                                    bldgSqm += double.TryParse(m.Groups[1].Value, out double val) ? val : 0;
                                }
                                dtB.Rows.Add(lsNo, 1, floor, bldgSqm, "", "", "", "");
                            }
                        }

                        Match match5 = Regex.Match(dtlStr, @"토[ ]*지[ ]*의[ ]*표시[ ]*:[ ]*(.*?)대지권의 종류[ ]*:[ ]*(.*?)대지권의 비율[ ]*:[ ]*(.*)", rxOptS);
                        Match match6 = Regex.Match(dtlStr, @"토[ ]*지[ ]*의[ ]*표시[ ]*:[ ]*(.*?)매각지분", rxOptS);
                        if (match5.Success)
                        {
                            Dictionary<string, string> dict = LandShrAreaCal(match5.Groups[1].Value, match5.Groups[3].Value);
                            totSqm = Convert.ToDouble(dict["rtTotSqm"]);
                            sqm = Convert.ToDouble(dict["rtSqm"]);
                            frtn = dict["frtn"];
                            catCd = dict["catCd"];
                            dtL.Rows.Add(lsNo, 1, catNm, catCd, "", sqm, "", totSqm, frtn, "");
                        }
                        else if (match6.Success)
                        {
                            Dictionary<string, string> dict = LandShrAreaCal(match6.Groups[1].Value, string.Empty);
                            totSqm = Convert.ToDouble(dict["rtTotSqm"]);
                            sqm = Convert.ToDouble(dict["rtSqm"]);
                            frtn = dict["frtn"];
                            catCd = dict["catCd"];
                            dtL.Rows.Add(lsNo, 1, catNm, catCd, "", sqm, "", totSqm, frtn, "");
                        }
                    }
                    else if (bldgSection != string.Empty)
                    {
                        Match match1 = Regex.Match(dtlStr, @"전유부분의[ ]*건물의[ ]*표시.*건물의[ ]*번호[ ]*:[ ]*(.*?)구조[ ]*:[ ]*(.*?)면적[ ]*:[ ]*(.*?)대지권의 종류[ ]*:[ ]*(.*?)대지권의 비율[ ]*:[ ]*(.*)", rxOptS);
                        Match match2 = Regex.Match(dtlStr, @"전유부분의[ ]*건물의[ ]*표시.*건물의[ ]*번호[ ]*:[ ]*(.*?)구조[ ]*:[ ]*(.*?)대지권의 종류[ ]*:[ ]*(.*?)대지권의 비율[ ]*:[ ]*(.*)", rxOptS);
                        Match match7 = Regex.Match(dtlStr, @"전유부분의[ ]*건물의[ ]*표시.*건물의[ ]*번호[ ]*:[ ]*(.*?)구조[ ]*:[ ]*(.*)", rxOptS);
                        if (match1.Success)
                        {
                            MatchCollection mc = Regex.Matches(match1.Groups[3].Value, @"(\d[\d\.\,]*)[\s]*㎡", rxOptS);
                            if (mc != null)
                            {
                                foreach (Match m in mc)
                                {
                                    //bldgSqm += Convert.ToDouble(m.Groups[1].Value);
                                    bldgSqm += double.TryParse(m.Groups[1].Value, out double val) ? val : 0;
                                }
                                dtB.Rows.Add(lsNo, 1, floor, bldgSqm, "", "", "", "");
                            }
                            Dictionary<string, string> dict = LandShrAreaCal(string.Empty, match1.Groups[5].Value);
                            totSqm = Convert.ToDouble(dict["rtTotSqm"]);
                            sqm = Convert.ToDouble(dict["rtSqm"]);
                            frtn = dict["frtn"];
                            catCd = dict["catCd"];
                            dtL.Rows.Add(lsNo, 1, catNm, catCd, "", sqm, "", totSqm, frtn, "");
                        }
                        else if (match2.Success)
                        {
                            MatchCollection mc = Regex.Matches(match2.Groups[2].Value, @"(\d[\d\.\,]*)[\s]*㎡", rxOptS);
                            if (mc != null)
                            {
                                foreach (Match m in mc)
                                {
                                    //bldgSqm += Convert.ToDouble(m.Groups[1].Value);
                                    bldgSqm += double.TryParse(m.Groups[1].Value, out double val) ? val : 0;
                                }
                                dtB.Rows.Add(lsNo, 1, floor, bldgSqm, "", "", "", "");
                            }
                            Dictionary<string, string> dict = LandShrAreaCal(string.Empty, match2.Groups[4].Value);
                            totSqm = Convert.ToDouble(dict["rtTotSqm"]);
                            sqm = Convert.ToDouble(dict["rtSqm"]);
                            frtn = dict["frtn"];
                            catCd = dict["catCd"];
                            dtL.Rows.Add(lsNo, 1, catNm, catCd, "", sqm, "", totSqm, frtn, "");
                        }
                        else if (match7.Success)
                        {
                            MatchCollection mc = Regex.Matches(match7.Groups[2].Value, @"(\d[\d\.\,]*)[\s]*㎡", rxOptS);
                            if (mc != null)
                            {
                                foreach (Match m in mc)
                                {
                                    //bldgSqm += Convert.ToDouble(m.Groups[1].Value);
                                    bldgSqm += double.TryParse(m.Groups[1].Value, out double val) ? val : 0;
                                }
                                dtB.Rows.Add(lsNo, 1, floor, bldgSqm, "", "", "", "");
                            }
                        }
                    }
                    else
                    {
                        //
                    }

                    if (dtlStr.Contains("매각지분"))
                    {
                        Match match1 = Regex.Match(dtlStr, @"매각지분[ ]*:[ ]*(.*)", rxOptS);
                        shrStr = match1.Groups[1].Value;
                        MatchCollection mc1 = Regex.Matches(shrStr, frtnPtrn1, rxOptS);
                        MatchCollection mc2 = Regex.Matches(shrStr, frtnPtrn2, rxOptS);
                        if (mc1 != null || mc2 != null)
                        {
                            if (dtL.Rows.Count > 0)
                            {
                                totShrSqm = sqm;
                                sqm = 0;
                                if (mc1 != null)
                                {
                                    foreach (Match m in mc1)
                                    {
                                        dt = Convert.ToDouble(m.Groups[1].Value);
                                        nt = Convert.ToDouble(m.Groups[2].Value);
                                        sqm += totShrSqm * nt / dt;
                                    }
                                }
                                if (mc2 != null)
                                {
                                    foreach (Match m in mc2)
                                    {
                                        dt = Convert.ToDouble(m.Groups[2].Value);
                                        nt = Convert.ToDouble(m.Groups[1].Value);
                                        sqm += totShrSqm * nt / dt;
                                    }
                                }
                                if (mc1.Count > 0 || mc2.Count > 0)
                                {
                                    if (totShrSqm != sqm)
                                    {
                                        dtL.Rows[dtL.Rows.Count - 1]["totShrSqm"] = totShrSqm;
                                        dtL.Rows[dtL.Rows.Count - 1]["rtSqm"] = sqm;
                                        dtL.Rows[dtL.Rows.Count - 1]["shrStr"] = string.Format("전체면적 {0}㎡중 {1}", totShrSqm, match1.Groups[1].Value.Trim());
                                    }
                                }
                            }

                            if (dtB.Rows.Count > 0)
                            {
                                totShrSqm = bldgSqm;
                                bldgSqm = 0;
                                if (mc1 != null)
                                {
                                    foreach (Match m in mc1)
                                    {
                                        dt = Convert.ToDouble(m.Groups[1].Value);
                                        nt = Convert.ToDouble(m.Groups[2].Value);
                                        bldgSqm += totShrSqm * nt / dt;
                                    }
                                }
                                if (mc2 != null)
                                {
                                    foreach (Match m in mc2)
                                    {
                                        dt = Convert.ToDouble(m.Groups[2].Value);
                                        nt = Convert.ToDouble(m.Groups[1].Value);
                                        bldgSqm += totShrSqm * nt / dt;
                                    }
                                }
                                if (mc1.Count > 0 || mc2.Count > 0)
                                {
                                    if (totShrSqm == bldgSqm)
                                    {
                                        dtB.Rows[dtB.Rows.Count - 1]["totShrSqm"] = 0;
                                        dtB.Rows[dtB.Rows.Count - 1]["sqm"] = bldgSqm.ToString();
                                        dtB.Rows[dtB.Rows.Count - 1]["shrStr"] = string.Empty;
                                    }
                                    else
                                    {
                                        dtB.Rows[dtB.Rows.Count - 1]["totShrSqm"] = totShrSqm;
                                        dtB.Rows[dtB.Rows.Count - 1]["sqm"] = bldgSqm.ToString();
                                        dtB.Rows[dtB.Rows.Count - 1]["shrStr"] = string.Format("전체면적 {0}㎡중 {1}", totShrSqm, match1.Groups[1].Value.Trim());
                                    }
                                    /*
                                    if (totShrSqm != bldgSqm)
                                    {
                                        dtB.Rows[dtB.Rows.Count - 1]["totShrSqm"] = totShrSqm;
                                        dtB.Rows[dtB.Rows.Count - 1]["sqm"] = bldgSqm.ToString();
                                        dtB.Rows[dtB.Rows.Count - 1]["shrStr"] = string.Format("전체면적 {0}㎡중 {1}", totShrSqm, match1.Groups[1].Value.Trim());
                                    }
                                    */
                                }
                            }
                        }
                    }

                    //총층수
                    eIndex = dtlStr.IndexOf("전유부분의 건물의 표시");
                    if (eIndex > -1)
                    {
                        MatchCollection mc = Regex.Matches(dtlStr.Remove(eIndex), @"(\d+)층", rxOptM);
                        if (mc != null)
                        {
                            Dictionary<string, int> dict = new Dictionary<string, int>();
                            foreach (Match m in mc)
                            {
                                if (!dict.ContainsKey(m.Value)) dict.Add(m.Value, Convert.ToInt32(m.Groups[1].Value));
                            }
                            if (dict.Count > 0 && dtB.Rows.Count > 0)
                            {
                                dtB.Rows[dtB.Rows.Count - 1]["totFlr"] = dict.Values.Max();
                            }
                        }
                    }

                    //제시외
                    if (etcStr != string.Empty)
                    {
                        MatchCollection mc = Regex.Matches(etcStr, etcPtrn1, rxOptM);
                        if (mc.Count > 0)
                        {
                            foreach (Match m in mc)
                            {
                                use = m.Groups[1].Value.Trim();
                                strt = m.Groups[2].Value.Trim();
                                area = m.Groups[3].Value.Trim();
                                if (use.Contains("기계기구"))
                                {
                                    macExist = true;
                                    continue;
                                }
                                if (m.Value.Contains("수목") || m.Value.Contains("나무") || m.Value.Contains("관정"))
                                {
                                    area = string.Empty;
                                }
                                else
                                {
                                    if (Regex.IsMatch(strt, macPtrn, rxOptM))
                                    {
                                        macExist = true;
                                        continue;
                                    }
                                }
                                dtE.Rows.Add(lsNo, use, strt, area);
                            }
                        }
                        else
                        {
                            mc = Regex.Matches(etcStr, etcPtrn2, rxOptM);
                            if (mc.Count > 0)
                            {
                                foreach (Match m in mc)
                                {
                                    use = m.Groups[1].Value.Trim();
                                    strt = m.Groups[2].Value.Trim();
                                    if (use.Contains("기계기구"))
                                    {
                                        macExist = true;
                                        continue;
                                    }
                                    if (m.Value.Contains("수목") || m.Value.Contains("나무") || m.Value.Contains("관정"))
                                    {
                                        area = string.Empty;
                                    }
                                    else
                                    {
                                        if (Regex.IsMatch(strt, macPtrn, rxOptM))
                                        {
                                            macExist = true;
                                            continue;
                                        }
                                    }
                                    dtE.Rows.Add(lsNo, use, strt, "");
                                }
                            }
                        }
                    }
                }
                else if (lsType == "자동차")
                {
                    //PrcDtlSub_Car(tid, doc, lsType, tblApsl, preNtFlag);
                    break;
                }
                else if (lsType == "건설기계,중기")
                {
                    //PrcDtlSub_Car(tid, doc, lsType, tblApsl, preNtFlag);
                    break;
                }
                else if (lsType == "선박")
                {
                    //PrcDtlSub_Ship(tid, doc);
                    break;
                }
                else if (lsType == "항공기")
                {
                    //
                }
                else if (lsType == "기타")
                {
                    //
                }
                else
                {
                    continue;
                }
            }

            sql = "select no, adrs from ta_ls where tid=" + tid;
            DataTable dtLs = db.ExeDt(sql);


            //토지현황
            if (chkLand.Checked)
            {
                if (rdoNew.Checked)
                {
                    db.Open();
                    sql = "delete from ta_land where tid=" + tid;
                    db.ExeQry(sql);

                    foreach (DataRow r in dtL.Rows)
                    {
                        i++;
                        sql = "insert into ta_land (tid, ls_no, cat_cd, sqm, tot_shr_sqm, rt_sqm, tot_rt_sqm, shr_str) values (@tid, @ls_no, @cat_cd, @sqm, @tot_shr_sqm, @rt_sqm, @tot_rt_sqm, @shr_str)";
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@ls_no", r["lsNo"]));
                        sp.Add(new MySqlParameter("@cat_cd", r["catCd"]));
                        sp.Add(new MySqlParameter("@sqm", r["sqm"]));
                        sp.Add(new MySqlParameter("@tot_shr_sqm", r["totShrSqm"]));
                        sp.Add(new MySqlParameter("@rt_sqm", r["rtSqm"]));
                        sp.Add(new MySqlParameter("@tot_rt_sqm", r["totRtSqm"]));
                        sp.Add(new MySqlParameter("@shr_str", r["shrStr"]));
                        db.ExeQry(sql, sp);
                        sp.Clear();

                        if (string.IsNullOrEmpty(r["sqm"].ToString()) == false) sumLandSqm += Convert.ToDouble(r["sqm"]);                    //총합-토지
                        if (string.IsNullOrEmpty(r["totShrSqm"].ToString()) == false) sumLandTotSqm += Convert.ToDouble(r["totShrSqm"]);     //총합-토지지분/대지권지분
                        if (string.IsNullOrEmpty(r["rtSqm"].ToString()) == false) sumRtSqm += Convert.ToDouble(r["rtSqm"]);                  //총합-대지권
                        if (string.IsNullOrEmpty(r["rtSqm"].ToString()) == false && i == 1) rtTotSqm = Convert.ToDouble(r["totRtSqm"]);      //대지권전체
                    }
                    db.Close();
                }
                else
                {                    
                    foreach (DataRow r in dtL.Rows)
                    {
                        foreach (DataGridViewRow gr in dgL.Rows)
                        { 
                            //
                        }
                    }
                }
            }

            //건물현황
            if (chkBldg.Checked)
            {
                if (rdoNew.Checked)
                {
                    db.Open();
                    sql = "delete from ta_bldg where tid=" + tid + " and dvsn=1";
                    db.ExeQry(sql);

                    foreach (DataRow r in dtB.Rows)
                    {
                        flrCd = "0";
                        if (r["floor"]?.ToString() != "")
                        {
                            var xFlr = dtFlrCd.Rows.Cast<DataRow>().Where(t => t["flr_nm"].ToString() == r["floor"].ToString()).SingleOrDefault();
                            flrCd = (xFlr == null) ? "0" : xFlr.Field<UInt16>("flr_cd").ToString();
                        }
                        if (flrCd == "0")
                        {
                            var xRow = dtLs.Rows.Cast<DataRow>().Where(t => t["no"].ToString() == r["lsNo"].ToString()).FirstOrDefault();
                            if (xRow != null)
                            {
                                string adrs = xRow["adrs"].ToString();
                                Match match = Regex.Match(adrs, @"\w+층", rxOptM);
                                var xFlr = dtFlrCd.Rows.Cast<DataRow>().Where(t => t["flr_nm"].ToString() == match.Value).SingleOrDefault();
                                flrCd = (xFlr == null) ? "0" : xFlr.Field<UInt16>("flr_cd").ToString();
                            }
                        }

                        sql = "insert into ta_bldg (tid, ls_no, dvsn, flr, tot_flr, sqm, tot_shr_sqm, shr_str) values (@tid, @ls_no, @dvsn, @flr, @tot_flr, @sqm, @tot_shr_sqm, @shr_str)";
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@ls_no", r["lsNo"]));
                        sp.Add(new MySqlParameter("@dvsn", 1));
                        sp.Add(new MySqlParameter("@flr", flrCd));
                        sp.Add(new MySqlParameter("@tot_flr", r["totFlr"]));
                        sp.Add(new MySqlParameter("@sqm", r["sqm"]));
                        sp.Add(new MySqlParameter("@tot_shr_sqm", r["totShrSqm"]));
                        sp.Add(new MySqlParameter("@shr_str", r["shrStr"]));
                        db.ExeQry(sql, sp);
                        sp.Clear();

                        if (string.IsNullOrEmpty(r["sqm"].ToString()) == false) sumBldgSqm += Convert.ToDouble(r["sqm"]);                //총합-건물
                        if (string.IsNullOrEmpty(r["totShrSqm"].ToString()) == false) sumBldgTotSqm += Convert.ToDouble(r["totShrSqm"]); //총합-건물지분
                    }
                    db.Close();
                }
                else
                { 
                    
                }
            }

            //제시외건물
            if (chkEtc.Checked)
            {
                if (rdoNew.Checked)
                {
                    db.Open();
                    sql = "delete from ta_bldg where tid=" + tid + " and dvsn=2";
                    db.ExeQry(sql);
                    foreach (DataRow r in dtE.Rows)
                    {
                        sql = "insert into ta_bldg (tid, ls_no, dvsn, sqm, state, struct) values (@tid, @ls_no, @dvsn, @sqm, @state, @struct)";
                        sp.Add(new MySqlParameter("@tid", tid));
                        sp.Add(new MySqlParameter("@ls_no", r["lsNo"]));
                        sp.Add(new MySqlParameter("@dvsn", 2));
                        sp.Add(new MySqlParameter("@sqm", r["sqm"].ToString().Trim()));
                        sp.Add(new MySqlParameter("@state", r["state"].ToString().Trim()));
                        sp.Add(new MySqlParameter("@struct", r["struct"].ToString().Trim()));
                        db.ExeQry(sql, sp);
                        sp.Clear();
                    }

                    //기계/기구 존재시
                    if (macExist)
                    {
                        sql = "delete from ta_bldg where tid=" + tid + " and dvsn=3";
                        db.ExeQry(sql);

                        sql = "insert into ta_bldg set tid=" + tid + ", dvsn=3, state='기계/기구'";
                        db.ExeQry(sql);
                    }
                    db.Close();
                }
                else
                { 
                
                }
            }

            //목록구분이 집합건물만 있는 경우 필지수 계산
            if (lsType == "집합건물" && ncTr.Count == 1 && landSection != string.Empty)
            {
                MatchCollection mc = Regex.Matches(landSection, @"(\d[\d\.\,]*)[\s]*㎡", rxOptS);
                if (mc != null)
                {
                    if (mc.Count > 1)
                    {
                        db.Open();
                        sql = "update ta_list set lot_cnt='" + mc.Count + "' where tid=" + tid;
                        db.ExeQry(sql);
                        db.Close();
                    }
                }
            }
        }

        private string landUnitConvert(string dvsn, string str)
        {
            string landUnitPtrn1 = @"([\d.,]+)평[ ]*((\d+)홉)*[ ]*((\d+)작)*[ ]*((\d+)재)*";  //평홉작재(1-평, 3-홉, 5-작, 7-재)
            string landUnitPtrn2 = @"([\d.,]+)정[ ]*((\d+)단)*[ ]*((\d+)무)*[ ]*(\d+)*보";    //정단무보(1-정, 3-단, 5-무, 6-보)
            double sqm = 0, phj = 0, jdm = 0;

            str = str.Replace(",", string.Empty);
            string dtlStr = str;

            MatchCollection mc = Regex.Matches(str, landUnitPtrn1, rxOptM);
            foreach (Match m in mc)
            {
                try
                {
                    phj = 0;
                    phj = Convert.ToDouble(string.IsNullOrEmpty(m.Groups[1].Value) ? "0" : m.Groups[1].Value) +
                        (Convert.ToDouble(string.IsNullOrEmpty(m.Groups[3].Value) ? "0" : m.Groups[3].Value) * 0.1) +
                        (Convert.ToDouble(string.IsNullOrEmpty(m.Groups[5].Value) ? "0" : m.Groups[5].Value) * 0.01) +
                        (Convert.ToDouble(string.IsNullOrEmpty(m.Groups[7].Value) ? "0" : m.Groups[7].Value) * 0.001);

                    if (phj > 0)
                    {
                        //sqm = phj * Convert.ToDouble(3.3058);
                        sqm = phj * Convert.ToDouble(3.305785);
                        dtlStr = dtlStr.Replace(m.Value, string.Format("{0}㎡", sqm));
                    }
                }
                catch
                {
                    dtlStr = "";
                }
            }

            if (dvsn == "토지" && mc.Count == 0)
            {
                mc = Regex.Matches(str, landUnitPtrn2, rxOptM);
                foreach (Match m in mc)
                {
                    try
                    {
                        jdm = 0;
                        jdm = (Convert.ToDouble(string.IsNullOrEmpty(m.Groups[1].Value) ? "0" : m.Groups[1].Value) * 3000) +
                            (Convert.ToDouble(string.IsNullOrEmpty(m.Groups[3].Value) ? "0" : m.Groups[3].Value) * 300) +
                            (Convert.ToDouble(string.IsNullOrEmpty(m.Groups[5].Value) ? "0" : m.Groups[5].Value) * 30) +
                            (Convert.ToDouble(string.IsNullOrEmpty(m.Groups[6].Value) ? "0" : m.Groups[6].Value) * 1);
                        if (jdm > 0)
                        {
                            //sqm = jdm * Convert.ToDouble(3.3058);
                            sqm = jdm * Convert.ToDouble(3.305785);
                            dtlStr = dtlStr.Replace(m.Value, string.Format("{0}㎡", sqm));
                        }
                    }
                    catch
                    {
                        dtlStr = "";
                    }
                }
            }

            return dtlStr;
        }

        private Dictionary<string, string> LandShrAreaCal(string landStr, string ratioStr)
        {
            string frtn = "", catNm = "", catCd = "";
            double landNo = 0, nt = 0, dt = 0, shrSqm = 0, rtTotSqm = 0, rtSqm = 0;

            Dictionary<string, string> dict = new Dictionary<string, string>();
            //dict["catCd"] = string.Empty;
            dict["catCd"] = "101017";   //대지-집합일 경우 Default
            dict["rtTotSqm"] = string.Empty;
            dict["rtSqm"] = string.Empty;
            dict["totShrSqm"] = string.Empty;
            dict["frtn"] = string.Empty;

            DataTable dtLand = new DataTable();
            dtLand.Columns.Add("landNo");
            dtLand.Columns.Add("catCd");
            dtLand.Columns.Add("area");

            DataTable dtRatio = new DataTable();
            dtRatio.Columns.Add("no");  //no
            dtRatio.Columns.Add("dt");  //분모
            dtRatio.Columns.Add("nt");  //분자

            List<string> lsPtrn = new List<string>();
            lsPtrn.Add(@"(\d+)\.[ ]*(\d+[\.\d]*)[ ]*분의[ ]*(\d+[\.\d]*)");
            lsPtrn.Add(@"(\d+)\.[ ]*(\d+[\.\d]*)/(\d+[\.\d]*)");

            foreach (string ptrn in lsPtrn)
            {
                MatchCollection mc = Regex.Matches(ratioStr, ptrn, rxOptS);
                if (mc != null)
                {
                    foreach (Match m in mc)
                    {
                        if (ptrn.Contains("분의")) dtRatio.Rows.Add(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
                        else dtRatio.Rows.Add(m.Groups[1].Value, m.Groups[3].Value, m.Groups[2].Value);
                    }
                }
            }

            if (landStr == string.Empty)
            {
                if (dtRatio.Rows.Count > 0)
                {
                    dict["rtTotSqm"] = dtRatio.Rows[0]["dt"].ToString();
                    dict["rtSqm"] = dtRatio.Rows[0]["nt"].ToString();
                }
            }
            else
            {
                MatchCollection mc = Regex.Matches(landStr, @"(\d+)\.[ ]*(.*?)[\s]*(" + landPtrn + "|null" + @")[ ]*(\d[\d\.\,]*)[ ]*㎡", rxOptM);
                if (mc != null)
                {
                    foreach (Match m in mc)
                    {
                        landNo = Convert.ToDouble(m.Groups[1].Value);
                        catNm = m.Groups[1].Value.Trim();
                        if (catNm == "대" || catNm == "null") catNm = "대지";
                        var x = from DataRow r in dtCatCd.Rows
                                where r["cat2_cd"].ToString() == "1010" && r["cat3_nm"].ToString() == catNm
                                select r;
                        if (x.Count() > 0) catCd = x.CopyToDataTable().Rows[0]["cat3_cd"].ToString();
                        else catCd = "101017";
                        dtLand.Rows.Add(landNo, catCd, m.Groups[4].Value.Replace(",", string.Empty));
                    }
                    foreach (DataRow row in dtLand.Rows)
                    {
                        rtTotSqm += Convert.ToDouble(row["area"]);
                    }
                }
                if (dtRatio.Rows.Count > 0)
                {
                    dt = Convert.ToDouble(dtRatio.Rows[0]["dt"]);
                    if (rtTotSqm == dt) rtSqm = Convert.ToDouble(dtRatio.Rows[0]["nt"]);
                    else
                    {
                        rtTotSqm = 0;
                        rtSqm = 0;
                        foreach (DataRow row in dtRatio.Rows)
                        {
                            var xRow = dtLand.Rows.Cast<DataRow>().Where(t => t["landNo"].ToString() == row["no"].ToString()).FirstOrDefault();
                            if (xRow != null)
                            {
                                rtTotSqm += Convert.ToDouble(xRow["area"]);
                                rtSqm += (Convert.ToDouble(xRow["area"]) * Convert.ToDouble(row["nt"])) / Convert.ToDouble(row["dt"]);
                            }
                        }
                    }
                }
                dict["rtTotSqm"] = rtTotSqm.ToString();
                dict["rtSqm"] = rtSqm.ToString();
                if (dtLand.Rows.Count > 0)
                {
                    dict["catCd"] = dtLand.Rows[0]["catCd"].ToString();
                }
            }

            return dict;
        }

        private void landBldg_B(string tid)
        {
            //
        }

        /// <summary>
        /// 사용승인일자
        /// </summary>
        /// <param name="tid"></param>
        private void aprvDt(string tid)
        {
            int totalCnt, mvCnt = 0, eqCnt = 0;
            string sql, url, xml, serviceKey, lsNo, adrs0, adrs, pnu, platGbCd, bun, ji, newPlatPlc, aprvDt, flrCnt, dongNm, elvtCnt, idx;

            Dictionary<string, string> dic = new Dictionary<string, string>();  //승인일자, 총층수
            Dictionary<string, string> dic2 = new Dictionary<string, string>(); //동명칭, 승인일자

            sql = "SELECT L.tid,S.no,S.pnu,S.adrs,B.tot_flr,B.elvt,B.idx from ta_list L , ta_ls S , ta_bldg B WHERE L.tid=S.tid and S.tid=B.tid and S.no=B.ls_no and L.tid='" + tid + "' and S.dvsn in ('건물','집합건물') and B.dvsn=1";
            DataTable dt = db.ExeDt(sql);

            XmlDocument doc = new XmlDocument();

            serviceKey = api.RndSrvKey();

            foreach (DataRow row in dt.Rows)
            {
                dic.Clear();
                dic2.Clear();
                aprvDt = string.Empty;
                eqCnt = 0;

                idx = row["idx"].ToString();
                //tid = row["tid"].ToString();
                lsNo = row["no"].ToString();
                pnu = row["pnu"].ToString();
                adrs0 = row["adrs"].ToString();
                adrs = row["adrs"].ToString();
                flrCnt = row["tot_flr"].ToString();
                if (flrCnt == "0") flrCnt = "1";
                elvtCnt = row["elvt"].ToString();
                if (pnu == string.Empty || pnu == "0") continue;

                //webCnt++;
                //if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

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

                //dtTest.Rows.Add(idx, aprvDt);

                sql = "update ta_bldg set aprv_dt='" + aprvDt + "', elvt='" + elvtCnt + "' where idx='" + idx + "' and tid='" + tid + "'";
                db.Open();
                db.ExeQry(sql);
                db.Close();
                mvCnt++;
            }
        }

        /// <summary>
        /// 주변환경 및 이용상태
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="doc"></param>
        private void envrnUseState(string tid, HAPDoc doc)
        {
            string loca = "", tfc = "", landShp = "", adjRoad = "", diff = "", faci = "", useSta = "";
            string db_loca, db_landShp, db_adjRoad, db_diff, db_faci;
            string useStr = "", state = "";
            string sql, cvp;
            int lsCnt;

            HtmlNode tblApsl = doc.DocumentNode.SelectSingleNode("//table[@summary='감정평가요항표']");
            if (tblApsl == null) return;

            HtmlNode nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'1) 위치 및 주위환경')]]");
            if (nd == null) nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'1) 위치 및 부근의 상황')]]"); //-> 공장감정평가요항표
            if (nd != null) loca = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
            nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'2) 교통상황')]]");
            if (nd != null) tfc = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
            nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'3) 형태 및 이용상태')]]");
            if (nd != null) landShp = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
            nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'4) 인접 도로상태')]]");
            if (nd != null) adjRoad = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
            nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'7) 공부와의 차이')]]");
            if (nd != null) diff = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();

            nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'3) 설비내역')]]");
            if (nd == null) nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'5) 설비내역')]]");
            if (nd != null) faci = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();

            nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'4) 이용상태')]]");
            if (nd != null) useSta = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
            nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'6) 토지의 형상 및 이용상태')]]");
            if (nd == null) nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'2) 토지의 상황')]]");      //-> 공장감정평가요항표
            if (nd != null) landShp = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();
            nd = tblApsl.SelectSingleNode(".//li/p[text()[contains(.,'7) 인접 도로상태등')]]");
            if (nd != null) adjRoad = nd.SelectSingleNode("following-sibling::*[1]").InnerText.Trim();

            db.Open();
            sql = "select * from ta_dtl where tid=" + tid;
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            db_loca = dr["loca"].ToString();
            db_landShp = dr["land_shp"].ToString();
            db_adjRoad = dr["adj_road"].ToString();
            db_diff = dr["diff"].ToString();
            db_faci = dr["faci"].ToString();
            dr.Close();
            db.Close();

            List<string> cvLs = new List<string>();
            List<MySqlParameter> sp = new List<MySqlParameter>();
            if (db_loca == string.Empty)
            {
                cvLs.Add("loca=@loca");
                sp.Add(new MySqlParameter("@loca", loca + "\r\n" + tfc));
            }
            if (db_landShp == string.Empty)
            {
                cvLs.Add("land_shp=@land_shp");
                sp.Add(new MySqlParameter("@land_shp", landShp));
            }
            if (db_adjRoad == string.Empty)
            {
                cvLs.Add("adj_road=@adj_road");
                sp.Add(new MySqlParameter("@adj_road", adjRoad));
            }
            if (db_diff == string.Empty)
            {
                cvLs.Add("diff=@diff");
                sp.Add(new MySqlParameter("@diff", diff));
            }
            if (db_faci == string.Empty)
            {
                cvLs.Add("faci=@faci");
                sp.Add(new MySqlParameter("@faci", faci));
            }

            if (cvLs.Count > 0)
            {
                cvp = String.Join(",", cvLs.ToArray());            
                sql = "update ta_dtl set " + cvp + " where tid=" + tid;
                db.Open();
                db.ExeQry(sql, sp);
                sp.Clear();
                db.Close();
            }

            HtmlNode tblLs = doc.DocumentNode.SelectSingleNode("//table[@summary='목록내역 표']");
            sql = "select B.idx, B.ls_no, B.state, L.dvsn from ta_ls L, ta_bldg B where L.tid=B.tid and L.no=B.ls_no and L.tid=" + tid + " and L.dvsn in ('건물','집합건물') and B.dvsn=1";
            DataTable dtLs = db.ExeDt(sql);
            lsCnt = dtLs.Rows.Count;
            if (lsCnt == 0) return;
            
            Match match;

            //감정평가요항표에서
            if (tblApsl != null)
            {
                useStr = Regex.Match(tblApsl.InnerHtml, @"<li><p class=""law_title"">\d+\)[ ]* 이용상태</p>\s+<ul><li><span.*?>(.*?)</span>", rxOptS).Groups[1].Value.Trim();
                if (useStr != string.Empty)
                {
                    if (Regex.IsMatch(useStr, @"(기호|번호|\d+\-[\(]*[가-하][\)]*)", rxOptM))
                    {
                        //
                    }
                    else
                    {
                        if (Regex.IsMatch(useStr, @"\d+층", rxOptM) && lsCnt > 1)
                        {
                            //
                        }
                        else
                        {
                            List<string> ptrnList = new List<string>();
                            ptrnList.Add(@"(.*?)[으로서]+[ ]*(이용|사용|내부)");
                            ptrnList.Add(@"용도는[ ]*(.*?)[으로]+");
                            ptrnList.Add(@"^((다세대주택|아파트)(\([\w\d, ]{5,}\))*)$");
                            ptrnList.Add(@"^[-]*[""]*(\w+(\(\w+\))*)[""]*[임\.]*$");
                            ptrnList.Add(@"본건은[ ]*(.*?)임");
                            ptrnList.Add(@"^현황[ ]*(.*?)임");
                            ptrnList.Add(@"(.*?)입니다");
                            ptrnList.Add(@"대장상[ ]*(\w+)이나");
                            ptrnList.Add(@"((아파트|연립주택)[\w\d,\( \)]*?)[임\.]");

                            foreach (string ptrn in ptrnList)
                            {
                                match = Regex.Match(useStr, ptrn, rxOptM);
                                if (match.Success)
                                {
                                    state = match.Groups[1].Value;
                                    break;
                                }
                            }

                            List<string> trimList = new List<string>();
                            trimList.Add(@".*결과");   //건축물대장 현황도면 및 탐문조사 결과 오피스텔
                            trimList.Add(@"이나.*");   //상가(소매점)이나 현재 공실 상태
                            trimList.Add(@"^(현황|\d+\)|기준시점 현재)");     //현황 다세대주택, 기준시점 현재 다세대주택, #) 연립주택
                            trimList.Add(@"집합건축물대장도면[ ]*상");        //아파트(집합건축물대장도면 상 방3, 욕실, 주방겸식당 등)
                            trimList.Add(@"(임|단위세대|\d+세대|\d+개호)$");  //아파트임, 아파트 단위세대, 아파트 1세대, 다세대주택 1개호
                            trimList.Add(@"\((별첨|후면)[ ]*내부구조도[ ]*참조\)");   //아파트(별첨 내부구조도참조), 아파트(후면 내부구조도 참조)

                            state = Regex.Replace(state, @"본[ ]*건은|공히|집합건축물대장상|전체를|공동주택으로서|^공부상 \w+ 현황|""|^\-|\(후첨.*\)|\(\d+층[ ]*\d+호\)|\d+층[ ]*\d+호|^[가-하]\)|구조$|\(내부.*|^(공부상|현황)|용도로서.*", string.Empty).Trim();
                            if (state != string.Empty)
                            {
                                foreach (string ptrn in trimList)
                                {
                                    state = Regex.Replace(state, ptrn, string.Empty, rxOptM);
                                }
                            }
                            if (state != string.Empty && !state.Contains("공실") && useStr.Contains("공실"))
                            {
                                state = $"{state}(현황:공실)";
                            }
                            if (state == "공동주택(아파트)") state = "아파트";
                            else if (state == "공동주택(연립주택)") state = "연립주택";
                            if (state != string.Empty)
                            {
                                db.Open();
                                foreach (DataRow row in dtLs.Rows)
                                {
                                    if (row["state"].ToString() != string.Empty) continue;
                                    sql = "update ta_bldg set state='" + state + "' where idx=" + row["idx"].ToString();
                                    db.ExeQry(sql);
                                }
                                db.Close();
                            }
                        }
                    }
                }
            }

            if (state != string.Empty) return;
            if (tblLs == null) return;

            //목록내역에서
        }

        /// <summary>
        /// 건물구조-부동산표시목록에서 취한다.
        /// </summary>
        /// <param name="tid"></param>
        /// <param name="jiwonNm"></param>
        /// <param name="saNo"></param>
        private void bldgStruct(string tid, string jiwonNm, string saNo)
        {
            string url, html, sql;
            string lsNo, lsAdrs, lsType;
            string dtlStr, etcStr, bldgStruct, floor, shrStr0, shrStr, flrCd, flrNm;
            double sqm = 0, totSqm = 0, bldgSqm = 0, totShrSqm = 0, dt = 0, nt = 0;
            double sumLandSqm = 0, sumLandTotSqm = 0, sumRtSqm = 0, rtTotSqm = 0, sumBldgSqm = 0, sumBldgTotSqm = 0;

            string bldgPtrn = @"([지하옥탑상일이삼사오육칠팔구십단\d]+[층실])[ ]*(.*?)(\d[\d\.\,]*)[ ]*㎡";    //2021-08-28 패턴 변경
            string frtnPtrn1 = @"(\d+[\.]*[\d]*)[ ]*분의[ ]*(\d+[\.]*[\d]*)";   //분수 패턴-1
            string frtnPtrn2 = @"(\d+[\.]*[\d]*)/(\d+[\.]*[\d]*)";              //분수 패턴-2
            string structPtrn = @"^\s+(철[근골]|[일반경량]+철골|[적흙변색]*벽돌|[시세]멘|조적조|목조|[브보블][록럭][크]*|연와[조]*|콘크리트|일반목구조|[철강]*파이프|조립식|조적|라멘조|알[.]*씨조|샌드위치|슬래브).*";  //건물구조 패턴

            sql = "select * from ta_ls where tid=" + tid + " order by no";
            DataTable dtLs = db.ExeDt(sql);
            if (dtLs.Rows.Count > 0)
            {
                if (dtLs.Select("dvsn='건물'").Count() == 0) return;
            }

            //건물용
            DataTable dtB = new DataTable();
            dtB.Columns.Add("lsNo");
            dtB.Columns.Add("multi");
            dtB.Columns.Add("floor");
            dtB.Columns.Add("sqm");
            dtB.Columns.Add("totShrSqm");
            dtB.Columns.Add("shrStr");
            dtB.Columns.Add("tmpStr");
            dtB.Columns.Add("totFlr");
            dtB.Columns.Add("struct");

            url = "http://www.courtauction.go.kr/RetrieveRealEstHjosaDispMokrok.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo;
            html = net.GetHtml(url);
            if (html.Contains("존재하지 않는 페이지입니다")) return;

            Dictionary<string, string> dicLs = new Dictionary<string, string>();
            Dictionary<string, string> dicAdrs = new Dictionary<string, string>();

            sql = "select * from ta_ls where tid=" + tid;
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                dicLs.Add(dr["no"].ToString(), dr["dvsn"].ToString().Trim());
                dicAdrs.Add(dr["no"].ToString(), dr["adrs"].ToString().Trim());
            }
            db.Close();

            HAPDoc doc = new HAPDoc();
            doc.LoadHtml(html);

            HtmlNodeCollection ncTr = doc.DocumentNode.SelectNodes("//table[@summary='부동산 표시목록']/tbody/tr");
            if (ncTr == null) return;

            List<MySqlParameter> sp = new List<MySqlParameter>();

            foreach (HtmlNode tr in ncTr)
            {
                HtmlNodeCollection ncTd = tr.SelectNodes("./td");
                if (ncTd.Count != 4) continue;
                lsNo = ncTd[0].InnerText.Trim();
                lsAdrs = ncTd[1].InnerText.Trim();
                if (dicLs.ContainsKey(lsNo) == false) continue;     //ta_ls 기록 할 수 없음.
                if (dicAdrs[lsNo].Replace(" ", string.Empty) != lsAdrs.Replace(" ", string.Empty))
                {
                    continue;
                }

                lsType = dicLs[lsNo];
                dtlStr = ncTd[2].InnerText.Replace("&nbsp;", string.Empty).Trim();
                dtlStr = Regex.Replace(dtlStr, @"[ ]*평방[ ]*미터|[ ]*제곱[ ]*미터", "㎡");

                if (lsType == "건물")
                {
                    int eIndex = dtlStr.IndexOf("제시외");
                    if (eIndex > -1)
                    {
                        etcStr = dtlStr.Substring(eIndex);
                        dtlStr = dtlStr.Remove(eIndex);
                    }

                    //건물구조
                    Match matchStruct = Regex.Match(dtlStr, structPtrn, rxOptM);
                    bldgStruct = matchStruct.Value.Trim();
                    if (bldgStruct != string.Empty)
                    {
                        bldgStruct = Regex.Replace(bldgStruct, @"([단\d]+층|평가건[주택점포]+|위험물저장|제\d종|[\w\.]+?시설|동물).*", string.Empty).Trim();
                    }

                    dtlStr = landUnitConvert("건물", dtlStr);
                    dtlStr = Regex.Replace(dtlStr, @"\(.*?\)", string.Empty, rxOptS);   //하위 세부내역 면적 제외

                    string[] strArr = dtlStr.Split('\n');
                    foreach (string s in strArr)
                    {
                        floor = ""; sqm = 0; totShrSqm = 0; totSqm = 0;
                        string str = s.Replace("&nbsp;", string.Empty).Trim();
                        if (str == string.Empty) continue;

                        Match match = Regex.Match(str, bldgPtrn, RegexOptions.Multiline);
                        if (match.Success)
                        {
                            floor = match.Groups[1].Value;
                            //totSqm = Convert.ToDouble(match.Groups[3].Value.Replace(",", string.Empty));
                            totSqm = double.TryParse(match.Groups[3].Value.Replace(",", string.Empty), out double val) ? val : 0;
                            sqm = totSqm;
                            dtB.Rows.Add(lsNo, 0, floor, sqm, totShrSqm, "", match.Value, "", bldgStruct);
                        }
                        else
                        {
                            match = Regex.Match(str, @"(\d[\d\.\,]+)[\s]*㎡", RegexOptions.Multiline);
                            if (match.Success)
                            {
                                str = match.Groups[1].Value.Replace(",", string.Empty).Trim();
                                str = Regex.Replace(str, @"\.$", string.Empty);     //0.53.㎡
                                //totSqm = Convert.ToDouble(str);
                                totSqm = double.TryParse(str, out double val) ? val : 0;
                                sqm = totSqm;
                                dtB.Rows.Add(lsNo, 0, floor, sqm, totShrSqm, "", match.Value, "", bldgStruct);
                            }
                        }
                    }
                    //Match matchShr = Regex.Match(dtlStr, @"매각지분[ ]*:[ ]*(.*)", rxOptS);
                    Match matchShr = Regex.Match(dtlStr, @"매각지분[ ]*(.*)", rxOptS);
                    if (matchShr.Success)
                    {
                        shrStr0 = matchShr.Groups[1].Value;
                        shrStr0 = Regex.Replace(shrStr0, @"제시외.*", string.Empty, rxOptS).Trim();
                        MatchCollection mc1 = Regex.Matches(shrStr0, frtnPtrn1, rxOptS);
                        MatchCollection mc2 = Regex.Matches(shrStr0, frtnPtrn2, rxOptS);
                        foreach (DataRow row in dtB.Rows)
                        {
                            if (row["lsNo"].ToString() == lsNo)
                            {
                                if (mc1 == null && mc2 == null) continue;
                                totShrSqm = Convert.ToDouble(row["sqm"]);
                                sqm = 0;
                                if (mc1 != null)
                                {
                                    foreach (Match m in mc1)
                                    {
                                        dt = Convert.ToDouble(m.Groups[1].Value);
                                        nt = Convert.ToDouble(m.Groups[2].Value);
                                        sqm += totShrSqm * nt / dt;
                                    }
                                }
                                if (mc2 != null)
                                {
                                    foreach (Match m in mc2)
                                    {
                                        dt = Convert.ToDouble(m.Groups[2].Value);
                                        nt = Convert.ToDouble(m.Groups[1].Value);
                                        sqm += totShrSqm * nt / dt;
                                    }
                                }
                                if (mc1.Count == 0 && mc2.Count == 0)
                                {
                                    sqm = totShrSqm;
                                    totShrSqm = 0;
                                    shrStr = string.Empty;
                                }
                                else
                                {
                                    if (totShrSqm == sqm)
                                    {
                                        totShrSqm = 0;
                                        shrStr = string.Empty;
                                    }
                                    else
                                    {
                                        shrStr = string.Format("전체면적 {0}㎡중 {1}", totShrSqm, shrStr0);
                                    }
                                }
                                row["sqm"] = sqm;
                                row["totShrSqm"] = totShrSqm;
                                row["shrStr"] = shrStr;
                            }
                        }
                    }
                }
            }

            db.Open();
            foreach (DataRow r in dtB.Rows)
            {
                //if (r["multi"].ToString() == "1") continue;
                flrCd = "0";
                if (r["floor"]?.ToString() != "")
                {
                    flrNm = r["floor"].ToString().Trim();
                    if (flrNm == "지1층") flrNm = "지하1층";
                    else if (flrNm == "지2층") flrNm = "지하2층";
                    var xFlr = dtFlrCd.Rows.Cast<DataRow>().Where(t => t["flr_nm"].ToString() == flrNm).SingleOrDefault();
                    flrCd = (xFlr == null) ? "0" : xFlr.Field<UInt16>("flr_cd").ToString();
                }
                if (flrCd == "0")
                {
                    var xRow = dtLs.Rows.Cast<DataRow>().Where(t => t["no"].ToString() == r["lsNo"].ToString()).FirstOrDefault();
                    if (xRow != null)
                    {
                        string adrs = xRow["adrs"].ToString();
                        Match match = Regex.Match(adrs, @"\w+층", rxOptM);
                        var xFlr = dtFlrCd.Rows.Cast<DataRow>().Where(t => t["flr_nm"].ToString() == match.Value).SingleOrDefault();
                        flrCd = (xFlr == null) ? "0" : xFlr.Field<UInt16>("flr_cd").ToString();
                    }
                }

                sql = "update ta_bldg set struct=@struct where tid=@tid and dvsn=1 and ls_no=@ls_no and flr=@flr and sqm=@sqm";
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@ls_no", r["lsNo"]));
                sp.Add(new MySqlParameter("@flr", flrCd));
                sp.Add(new MySqlParameter("@sqm", r["sqm"]));
                sp.Add(new MySqlParameter("@struct", r["struct"]));
                db.ExeQry(sql, sp);
                sp.Clear();
            }
            db.Close();
        }
    }
}
