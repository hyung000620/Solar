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
using System.Collections;
using System.Xml;

namespace Atom.CA
{
    public partial class fSucbAfter : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AtomLog atomLog = new AtomLog(107);
        AuctCd auctCd = new AuctCd();
        ApiUtil api = new ApiUtil();

        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 1500, webCnt = 0;

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        int cmpDvsnCd = 14;     //낙찰후처리(RptDvsn: 14)

        DataTable dtLaw;
        DataTable dtL, dtR, dtH;
        //DataTable dtR = new DataTable();
        //DataTable dtH = new DataTable();

        public fSucbAfter()
        {
            InitializeComponent();
            this.Shown += FSucbAfter_Shown;
        }

        private void FSucbAfter_Shown(object sender, EventArgs e)
        {
            CheckForIllegalCrossThreadCalls = false;
            bgwork = new BackgroundWorker();
            bgwork.DoWork += Bgwork_DoWork;
            bgwork.RunWorkerCompleted += Bgwork_RunWorkerCompleted;
            bgwork.WorkerReportsProgress = false;
            bgwork.WorkerSupportsCancellation = true;

            BaseDtInit();

            bgwork.RunWorkerAsync();
        }

        private void BaseDtInit()
        {
            dtLaw = auctCd.DtLawInfo();
            dtH = new DataTable();
        }

        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            bool flag = false;
            int totCnt = 0, curCnt = 0;
            int rowIdx = 0, state = 0;
            string sql = "", htmlSagun = "", htmlGiil = "", url = "", jiwonNm = "", saNo = "", prevSaNo = "", pn = "", tblSagun = "", tblGiil = "", clsState = "", clsDt = "", tid = "", spt = "";

            string endPatn = @"배당종결|취하|기각|각하|취소|기타";

            txtPrgs.AppendText("\r\n ### 낙찰후처리 ### \r\n");

            sql = "select tid, state, sucb_amt, minb_amt, dpst_rate, " +
                "date_format(bid_dt,'%Y-%m-%d') as bidDt, date_format(aprv_dt,'%Y-%m-%d') as aprvDt, date_format(limit_dt,'%Y-%m-%d') as limitDt, date_format(pay_dt,'%Y-%m-%d') as payDt, date_format(shr_dt,'%Y-%m-%d') as shrDt, date_format(cls_dt,'%Y-%m-%d') as clsDt, " +
                "cls_rslt from db_tank.tx_rpt where dvsn=" + cmpDvsnCd + " and prc=0";
            dtR = db.ExeDt(sql);

            sql = "select tid, spt, sn1, sn2, pn, sta2, bid_dt as bidDt from ta_list where sta1=12 and sta2 != 1219 order by spt, sn1 ,sn2, pn";
            //sql = "select tid, spt, sn1, sn2, pn, sta2, bid_dt as bidDt from ta_list where tid=1934425";
            dtL = db.ExeDt(sql);
            dtL.Columns.AddRange(new DataColumn[]
            {
                new DataColumn("No"),
                new DataColumn("giilRslt"),
                new DataColumn("aprvDt"),
                new DataColumn("limitDt"),
                new DataColumn("payDt"),
                new DataColumn("shrDt"),
                new DataColumn("clsState"),
                new DataColumn("clsDt"),
                new DataColumn("sucbAmtS"),
                new DataColumn("minbAmtS"),
                new DataColumn("bidDtS"),
                new DataColumn("dpstRate"),
                new DataColumn("rslt")
            });
            
            totCnt = dtL.Rows.Count;
            atomLog.AddLog(string.Format("확인 대상 - {0}건", totCnt));

            foreach (DataRow row in dtL.Rows)
            {
                curCnt++;
                flag = false;
                                
                rowIdx = dtL.Rows.IndexOf(row);                
                tid = row["tid"].ToString();

                txtPrgs.AppendText(string.Format("\r\n> TID - {0}\t\t{1} / {2}", tid, curCnt, totCnt));

                spt = row["spt"].ToString();
                state = Convert.ToInt16(row["sta2"]);
                saNo = String.Format("{0}0130{1}", row["sn1"], row["sn2"].ToString().PadLeft(6, '0'));
                jiwonNm = auctCd.FindLawNm(string.Format("{0}", spt), true);
                pn = row["pn"].ToString();
                if (pn == "0") pn = "1";

                //사건내역
                if (saNo != prevSaNo)
                {                    
                    webCnt++;
                    if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                    url = "http://www.courtauction.go.kr/RetrieveRealEstDetailInqSaList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
                    htmlSagun = net.GetHtml(url);
                    Match match = Regex.Match(htmlSagun, @"<table class=""Ltbl_dt"" summary=""사건기본내역 표"">.*?</table>", rxOptS);
                    tblSagun = match.Value;

                    clsState = Regex.Match(tblSagun, @"<th>종국결과</th>\W+<td[ ]*>(.*)</td>", rxOptM).Groups[1].Value.Trim();
                    clsDt = Regex.Match(tblSagun, @"<th>종국일자</th>\W+<td[ ]*>(.*)</td>", rxOptM).Groups[1].Value.Replace(".", "-");

                    //파일저장
                    /*fileName = path + lawCode + "-" + pid + "_11.html";
                    if (File.Exists(fileName) == false)
                    {
                        fileSave("사건내역", htmlSagun, fileName);
                    }

                    crawlCnt++;
                    if (crawlCnt % setCount == 0) Thread.Sleep(setSleep);*/
                }

                //기일내역               
                if (saNo != prevSaNo)
                {
                    if (Regex.Match(clsState, endPatn).Success == false)
                    {
                        webCnt++;
                        if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                        url = "http://www.courtauction.go.kr/RetrieveRealEstSaDetailInqGiilList.laf?jiwonNm=" + jiwonNm + "&saNo=" + saNo + "&_SRCH_SRNID=PNO101001";
                        htmlGiil = net.GetHtml(url);
                        Match match = Regex.Match(htmlGiil, @"<table class=""Ltbl_dt"" summary=""기일내역 표"">.*?</table>", rxOptS);
                        tblGiil = match.Value;

                        //파일저장
                        /*fileName = path + lawCode + "-" + pid + "_1.html";
                        if (File.Exists(fileName) == false)
                        {
                            fileSave("기일내역", htmlGiil, fileName);
                        }

                        crawlCnt++;
                        if (crawlCnt % setCount == 0) Thread.Sleep(setSleep);*/
                    }
                }

                if (Regex.Match(clsState, endPatn).Success)
                {
                    row["clsState"] = clsState;
                    row["clsDt"] = clsDt;
                    flag = true;
                }
                else if (clsState == "미종국")
                {
                    string eaState = prcSagun(htmlSagun, pn);

                    if (eaState != string.Empty)
                    {
                        row["clsState"] = eaState;
                        row["clsDt"] = string.Empty;
                        flag = true;
                    }
                    else
                    {
                        Dictionary<string, string> dic = prcGiil(row, tblGiil, htmlSagun);
                        row["giilRslt"] = dic["기일결과"];
                        row["aprvDt"] = dic["매각허가"];
                        row["limitDt"] = dic["지급기한"];
                        row["payDt"] = dic["납부일자"];
                        row["shrDt"] = dic["배당기일"];
                        if (dic["기일결과"] == "차순위")
                        {
                            row["sucbAmtS"] = dic["낙찰가"];
                        }
                        if (dic["기일결과"] == "미납(기일)")
                        {
                            row["minbAmtS"] = dic["최저가"];
                            row["bidDtS"] = dic["매각기일"];
                            row["dpstRate"] = dic["보증금율"];
                        }
                        if (dic["기일결과"] != string.Empty || dic["매각허가"] != string.Empty || dic["지급기한"] != string.Empty || dic["납부일자"] != string.Empty || dic["배당기일"] != string.Empty)
                        {
                            flag = true;
                        }
                    }
                }

                prevSaNo = saNo;

                if (flag == false) continue;

                //DB 자동처리
                prcDB(row);
            }
        }

        /// <summary>
        /// 사건내역 물건별 진행 상태 분석
        /// </summary>
        /// <param name="html"></param>
        /// <param name="pNum"></param>
        /// <returns></returns>
        private string prcSagun(string html, string pNum)
        {
            string state = "";

            //사건내역 -> 물건내역
            MatchCollection mcTblPd = Regex.Matches(html, @"<table class=""Ltbl_dt"" summary=""물건내역 표"">.*?</table>", rxOptS);
            foreach (Match maTbl in mcTblPd)
            {
                if (Regex.Match(maTbl.Value, @"<th[ \w\d=""%]+>물건번호</th>\s+<td[ \w\d=""\-:;]+>(\d+)</td>", rxOptM).Groups[1].Value != pNum) continue;

                Match match = Regex.Match(maTbl.Value, @"<th[ \w\d=""%]+>비고</th>\s+<td[ \w\d=""%]+>(.*?)</td>", rxOptS);
                if (match.Groups[1].Value.Trim() != "미종국")
                {
                    state = Regex.Replace(match.Groups[1].Value, @"<[^>]*?>", string.Empty).Trim();
                    break;
                }
            }

            return state;
        }

        /// <summary>
        /// 기일내역 분석
        /// </summary>
        /// <param name="row"></param>
        /// <param name="tblGiil"></param>
        /// <returns></returns>
        private Dictionary<string, string> prcGiil(DataRow row, string tblGiil, string htmlSagun)
        {
            int state = 0;
            string pNum = "", pNumS = "", prevPnumS = "";
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

            state = Convert.ToInt16(row["sta2"]);
            pNum = row["pn"].ToString();
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
                if (pNumS == pNum && Convert.ToDateTime(bidDtS) >= Convert.ToDateTime(row["bidDt"]))
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

                    //if (state == 3 || state == 25 || state == 27)
                    if (state == 1211)  //허가
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

                    if (state == 1211 || state == 1215)     //허가, 지급기한
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
                                dic["납부일자"] = "";
                            }

                            if (match.Groups[1].Value == "차순위" && bidType.Contains("매각결정기일"))
                            {
                                dic["기일결과"] = "차순위";
                                dic["매각허가"] = bidDtS;
                                dic["납부일자"] = "";
                            }
                        }

                        if (dic["기일결과"] == "미납" && bidType.Contains("매각기일"))
                        {
                            dic["기일결과"] = "미납(기일)";
                            dic["매각기일"] = bidDtS;
                            dic["최저가"] = lowS;
                            dic["납부일자"] = "";
                            dic["보증금율"] = prcDpstRate(htmlSagun, pNum);
                        }

                        //차순위가 나올 경우 낙찰가 대소비교를 위해 미리 구한다.
                        if (bidType == "매각기일" && bidRst.Contains("매각"))
                        {
                            bidS = Regex.Match(bidRst, @"\(([0-9]+(,[0-9]+)*)원\)", rxOptM).Groups[1].Value.Replace(",", string.Empty);
                            dic["낙찰가"] = bidS;
                        }
                    }

                    if (state == 1217 || state == 1215 || state == 1216)    //기한후납부, 지급기한, 납부
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
                    if (state == 1218)  //배당기일
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
        /// 사건내역->재매각 보증금율 검출
        /// </summary>
        /// <param name="html"></param>
        /// <param name="pNum"></param>
        /// <returns></returns>
        private string prcDpstRate(string html, string pNum)
        {
            string rate = "", patn = "", matchStr = "";

            patn = @"보증금[은\s]*(최저매각가격의)*[\s]*((\d+)%|(\d+)[분의\s]*(\d+))";

            //사건내역 -> 물건내역
            MatchCollection mcTblPd = Regex.Matches(html, @"<table class=""Ltbl_dt"" summary=""물건내역 표"">.*?</table>", rxOptS);
            foreach (Match maTbl in mcTblPd)
            {
                if (Regex.Match(maTbl.Value, @"<th[ \w\d=""%]+>물건번호</th>\s+<td[ \w\d=""\-:;]+>(\d+)</td>", rxOptM).Groups[1].Value != pNum) continue;

                Match match = Regex.Match(maTbl.Value, @"<th>물건비고</th>\s+<td colspan=""8"">(.*?)</td>", rxOptS);
                if (!match.Success) break;

                matchStr = match.Groups[1].Value;
                match = Regex.Match(matchStr, patn, rxOptS);
                if (match.Success)
                {
                    if (match.Groups[3].Value != string.Empty) rate = match.Groups[3].Value;
                    else if (match.Groups[4].Value != string.Empty && match.Groups[5].Value != string.Empty)
                    {
                        //rate = ((decimal.Parse(match.Groups[5].Value) / decimal.Parse(match.Groups[4].Value)) * 100).ToString();
                        rate = string.Format("{0:F0}", (decimal.Parse(match.Groups[5].Value) / decimal.Parse(match.Groups[4].Value)) * 100);
                    }

                    if (rate != string.Empty) break;
                }
            }

            return rate;
        }

        /// <summary>
        /// 상태별 DB 처리
        /// </summary>
        /// <param name="row"></param>
        private void prcDB(DataRow row)
        {
            int num = 0, prc = 0, cd = 0;
            string sql = "", tid = "", state = "", giilRst = "", aprvDt = "", limitDt = "", payDt = "", shrDt = "", clsDt = "", clsState = "";
            string sucbAmt = "", minbAmt = "", dpstRate = "", bidDtS = "";

            dtH.Rows.Clear();

            tid = row["tid"].ToString();
            state = row["sta2"].ToString();
            giilRst = row["giilRslt"].ToString();
            aprvDt = row["aprvDt"].ToString();
            limitDt = row["limitDt"].ToString();
            payDt = row["payDt"].ToString();
            shrDt = row["shrDt"].ToString();
            clsDt = row["clsDt"].ToString();
            clsState = row["clsState"].ToString();
            sucbAmt = row["sucbAmtS"].ToString();
            minbAmt = row["minbAmtS"].ToString();
            dpstRate = row["dpstRate"].ToString();
            bidDtS = row["bidDtS"].ToString();

            sql = "SELECT seq, sta, DATE_FORMAT(bid_dt,'%Y-%m-%d') AS tdate FROM ta_hist WHERE tid=" + tid + " order by seq";
            dtH = db.ExeDt(sql);

            if (dtH.Rows.Count > 0) num = Convert.ToInt16(dtH.Rows[dtH.Rows.Count - 1]["seq"]) + 1;
            else num = 1;

            //매각허가-낙찰, 차순위, 결정변경, 추후지정
            if ((state == "1210" || state == "1212" || state == "1213" || state == "1214") && giilRst == "최고가" && aprvDt != string.Empty)
            {
                var x = from DataRow r in dtH.Rows
                        where r["sta"].ToString() == "1211" && r["tdate"].ToString() == row["aprvDt"].ToString()    //허가
                        select r;

                if (x.Count() == 0)
                {
                    prc = 1;
                    cd = 1211;  //허가
                    prcDBsub(tid, num, aprvDt, cd);
                }
            }

            //지급기한
            if (state == "1211" && limitDt != string.Empty)    //허가
            {
                prc = 2;
                cd = 1215;      //지급기한
                prcDBsub(tid, num, limitDt, cd);
            }

            //대금납부와 미납
            //기한후납부            
            if (state == "1215")    //지급기한 
            {
                if (giilRst == "기한후납부")
                {
                    //prc = 3;
                    prc = 0;
                    cd = 1217;  //기한후납부
                    //prcDBsub(pid, num, payLimit, cd);
                }
                else if (giilRst == "허가취소")
                {
                    prc = 0;
                }
                else if (giilRst == "미납")
                {
                    prc = 6;
                }
                else
                {
                    //납부일자가 있는 경우
                    if (payDt != string.Empty)
                    {
                        prc = 3;
                        cd = 1216;  //납부
                        prcDBsub(tid, num, payDt, cd);
                    }

                    //납부일자가 없고,배당기일이 있는 경우
                    if (payDt == string.Empty && shrDt != string.Empty)
                    {
                        prc = 3;
                        cd = 1218;  //배당기일
                        prcDBsub(tid, num, shrDt, cd);
                    }
                }
            }

            //배당기일
            if ((state == "1217" || state == "1216") && shrDt != string.Empty)  //기한후납부, 납부
            {
                prc = 4;
                cd = 1218;  //배당기일
                prcDBsub(tid, num, shrDt, cd);
            }

            //배당종결 및 기타 종국처리
            string endPatn = @"배당종결|취하|기각|각하|취소|기타";
            Match match = Regex.Match(clsState, endPatn);

            if (match.Success && clsDt != string.Empty)
            {
                if (match.Value == "배당종결") cd = 1219;
                else if (match.Value == "취하") cd = 1415;
                else if (match.Value == "기각") cd = 1411;
                else if (match.Value == "각하") cd = 1410;
                else if (match.Value == "취소") cd = 1414;
                else cd = 1412;    //기타(8) 및 미지의 나머지

                //wantCnt++;
                prc = 5;
                prcDBsub(tid, num, clsDt, cd);
            }

            if (prc == 6) return;   //미납은 제외

            if (sucbAmt == "") sucbAmt = "0";
            if (minbAmt == "") minbAmt = "0";
            if (dpstRate == "") dpstRate = "0";
            if (bidDtS == "") bidDtS = "0000-00-00";
            if (aprvDt == "") aprvDt = "0000-00-00";
            if (limitDt == "") limitDt = "0000-00-00";
            if (payDt == "") payDt = "0000-00-00";
            if (shrDt == "") shrDt = "0000-00-00";
            if (clsDt == "") clsDt = "0000-00-00";

            var xRow = dtR.Rows.Cast<DataRow>().Where(t => t["tid"].ToString() == tid && t["state"].ToString() == giilRst && t["sucb_amt"].ToString() == sucbAmt && t["minb_amt"].ToString() == minbAmt && t["dpst_rate"].ToString() == dpstRate &&
            t["bidDt"].ToString() == bidDtS && t["aprvDt"].ToString() == aprvDt && t["limitDt"].ToString() == limitDt && t["payDt"].ToString() == payDt && t["shrDt"].ToString() == shrDt && t["clsDt"].ToString() == clsDt && t["cls_rslt"].ToString() == clsState).FirstOrDefault();
            if (xRow != null) return;

            List<MySqlParameter> sp = new List<MySqlParameter>();
            sql = "insert into db_tank.tx_rpt set tid=@tid, dvsn=@dvsn, state=@state, sucb_amt=@sucb_amt, minb_amt=@minb_amt, dpst_rate=@dpst_rate, bid_dt=@bid_dt, aprv_dt=@aprv_dt, limit_dt=@limit_dt, pay_dt=@pay_dt, shr_dt=@shr_dt, cls_dt=@cls_dt, cls_rslt=@cls_rslt, prc=@prc, prc_sub=@prc_sub, wdt=curdate()";
            sp.Add(new MySqlParameter("@tid", tid));
            sp.Add(new MySqlParameter("@dvsn", cmpDvsnCd));
            /*sp.Add(new MySqlParameter("@state", row["giilRslt"]?.ToString() ?? ""));
            sp.Add(new MySqlParameter("@sucb_amt", row["sucbAmtS"]?.ToString() ?? ""));
            sp.Add(new MySqlParameter("@minb_amt", row["minbAmtS"]?.ToString() ?? ""));
            sp.Add(new MySqlParameter("@dpst_rate", row["dpstRate"]?.ToString() ?? ""));
            sp.Add(new MySqlParameter("@bid_dt", row["bidDtS"]?.ToString() ?? ""));
            sp.Add(new MySqlParameter("@aprv_dt", row["aprvDt"]?.ToString() ?? ""));
            sp.Add(new MySqlParameter("@limit_dt", row["limitDt"]?.ToString() ?? ""));
            sp.Add(new MySqlParameter("@pay_dt", row["payDt"]?.ToString() ?? ""));
            sp.Add(new MySqlParameter("@shr_dt", row["shrDt"]?.ToString() ?? ""));
            sp.Add(new MySqlParameter("@cls_dt", row["clsDt"]?.ToString() ?? ""));
            sp.Add(new MySqlParameter("@cls_rslt", row["clsState"]?.ToString() ?? ""));*/
            sp.Add(new MySqlParameter("@state", giilRst));
            sp.Add(new MySqlParameter("@sucb_amt", sucbAmt));
            sp.Add(new MySqlParameter("@minb_amt", minbAmt));
            sp.Add(new MySqlParameter("@dpst_rate", dpstRate));
            sp.Add(new MySqlParameter("@bid_dt", bidDtS));
            sp.Add(new MySqlParameter("@aprv_dt", aprvDt));
            sp.Add(new MySqlParameter("@limit_dt", limitDt));
            sp.Add(new MySqlParameter("@pay_dt", payDt));
            sp.Add(new MySqlParameter("@shr_dt", shrDt));
            sp.Add(new MySqlParameter("@cls_dt", clsDt));
            sp.Add(new MySqlParameter("@cls_rslt", clsState));
            sp.Add(new MySqlParameter("@prc", (prc > 0) ? 1 : 0));
            sp.Add(new MySqlParameter("@prc_sub", prc));
            db.Open();
            db.ExeQry(sql, sp);
            sp.Clear();
            db.Close();
        }

        private void prcDBsub(string tid, int seq, string dt, int cd, string rst = null)
        {
            string sql = "", sta1, sta2, clsDt = "";

            db.Open();
            sql = "insert into ta_hist set tid=" + tid + ", seq=" + seq + ", bid_dt='" + dt + "', sta=" + cd;
            db.ExeQry(sql);

            sta1 = cd.ToString().Substring(0, 2);
            sta2 = cd.ToString();
            clsDt = (cd == 1219) ? dt : string.Empty;   //배당종결
            sql = "update ta_list set sta1=" + sta1 + ", sta2=" + sta2 + ", end_dt='" + clsDt + "' where tid=" + tid;
            db.ExeQry(sql);
            db.Close();
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            atomLog.AddLog("실행 완료", 1);
            bgwork.Dispose();

            this.Dispose();
            this.Close();
        }
    }
}
