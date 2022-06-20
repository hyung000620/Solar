using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Solar
{

    public class RgstAnalyNew
    {
        DbUtil db = new DbUtil();
        AuctCd auctCd = new AuctCd();
        NetUtil net = new NetUtil();
        ApiUtil api = new ApiUtil();
        string cnvTool = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\pdftohtml.exe";

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        decimal exUSD = 0, exJPY = 0, exCAD = 0;    //기준 환율
        string pinLand = string.Empty, pinBldg = string.Empty;

        string rightPtrn = "(채권자|근저당권자|저당권자|권리자|전세권자|지상권자|가등기권자|임차권자|환매권자|수탁자|처분청|소관청|관리청)";
        string adrsPtrn = @"((서울|부산|인천|대구|대전|울산|광주|경기|강원|충남|충북|경남|경북|전남|전북|제주|세종)\s)?\w+[시도구군]\s+\w+[시구군읍면동가로길]";

        public string tid { get; set; }
        public string saNo { get; set; }
        public string rgstDvsn { get; set; }
        public string rgstIdNo { get; set; }
        public string pdfCreator { get; set; }
        public bool dbPrc { get; set; } = false;
        public bool mdfyPrc { get; set; } = false; //등기변동에 의한 재발급시 추출별도 기록
        public string analyRslt { get; set; }
        public DataTable dtA { get; set; }
        public DataTable dtB { get; set; }
        public DataTable dtRgCd { get; set; }

        public RgstAnalyNew()
        {
            this.dtA = new DataTable();  //분석-전
            this.dtB = new DataTable();  //분석-후

            string[] cols1 = new string[] { "sect", "rank", "prps", "rcpt", "resn", "prsn" };

            string[] cols2 = new string[] { "sect", "rank", "prpsOrg", "prps", "rgCd", "rcDt", "rcNo", "rENo", "cAmt", "prsn", "prsnOrg", "rgNo", "shrStr", "adrs", "brch", "mvDt", "bzDt", "fxDt", "bgnDt", "endDt", "note", "aply", "ekey", "siCd", "guCd", "dnCd", "hide", "rpt", "del" };

            foreach (string col in cols1)
            {
                dtA.Columns.Add(col);
            }
            foreach (string col in cols2)
            {
                dtB.Columns.Add(col);
            }

            //등기목적(권리)-코드
            string sql = "select * from ta_cd_rgst order by rg_cd";
            dtRgCd = db.ExeDt(sql);

            //환율정보(매매기준율)
            sql = "select * from tx_exr where usd > 900 order by idx desc limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            exUSD = Convert.ToDecimal(dr["usd"]);
            exJPY = Convert.ToDecimal(dr["jpy"]);
            exCAD = Convert.ToDecimal(dr["cad"]);
            dr.Close();
            db.Close();

            if (!File.Exists(cnvTool))
            {
                File.WriteAllBytes(cnvTool, Properties.Resources.pdftohtml);
            }
        }

        public string Proc(string pdfFile, bool dbPrc = false, bool mdfyPrc = false)
        {
            string sn1 = string.Empty, sn2 = string.Empty, sql, today;

            this.dbPrc = dbPrc;
            this.mdfyPrc = mdfyPrc;

            this.tid = string.Empty;
            this.saNo = string.Empty;
            this.rgstDvsn = string.Empty;
            this.rgstIdNo = string.Empty;
            this.analyRslt = string.Empty;

            this.dtA.Clear();
            this.dtB.Clear();

            Regex rx1 = new Regex(@"[DAB]{2}\-(\d{4})\-(\d{4})(\d{6})\-(\d{4})\-\d{2,4}.pdf", rxOptM);
            Regex rx2 = new Regex(@"(\d+)_(\d+)\.pdf", rxOptM);
            Regex rx3 = new Regex(@"(\d{14})\.pdf", rxOptM);

            try
            {
                db.Open();
                Match match1 = rx1.Match(pdfFile);
                Match match2 = rx2.Match(pdfFile);
                Match match3 = rx3.Match(pdfFile);
                if (match1.Success)
                {
                    sn1 = match1.Groups[2].Value;
                    sn2 = match1.Groups[3].Value;
                    sql = "select tid from ta_list where spt='" + match1.Groups[1].Value + "' and sn1='" + sn1 + "' and sn2='" + sn2 + "' and pn='" + match1.Groups[4].Value + "' limit 1";
                    MySqlDataReader dr = db.ExeRdr(sql);
                    dr.Read();
                    if (dr.HasRows) this.tid = dr["tid"].ToString();
                    else this.tid = string.Empty;
                    dr.Close();
                }
                else if (match2.Success)
                {
                    this.tid = Regex.Match(pdfFile, @"(\d+)_(\d+)\.pdf", rxOptM).Groups[1].Value;
                    sql = "select sn1, sn2 from ta_list where tid='" + tid + "'";
                    MySqlDataReader dr = db.ExeRdr(sql);
                    dr.Read();
                    if (dr.HasRows)
                    {
                        sn1 = dr["sn1"].ToString();
                        sn2 = dr["sn2"].ToString();
                    }
                    else
                    {
                        sn1 = string.Empty;
                        sn2 = string.Empty;
                    }
                    dr.Close();
                }
                else if (match3.Success)
                {                    
                    //sql = $"select * from db_tank.tx_rgst_auto R, db_main.ta_list L where R.tid=L.tid and wdt=curdate() and pin='{match3.Groups[1].Value}' and (R.dvsn between 10 and 14) and ul=0";
                    sql = $"select * from db_tank.tx_rgst_auto R, db_main.ta_list L where R.tid=L.tid and wdt > date_sub(curdate(),INTERVAL 10 day) and pin='{match3.Groups[1].Value}' and (R.dvsn between 10 and 14) and ul=0";
                    MySqlDataReader dr = db.ExeRdr(sql);
                    dr.Read();
                    if (dr.HasRows)
                    {
                        this.tid = dr["tid"].ToString();
                        sn1 = dr["sn1"].ToString();
                        sn2 = dr["sn2"].ToString();
                    }
                    else
                    {
                        this.tid = string.Empty;
                        sn1 = string.Empty;
                        sn2 = string.Empty;
                    }
                    dr.Close();
                }
                else
                {
                    throw new Exception("파일명 매칭 오류");
                }

                if (tid == string.Empty) throw new Exception("해당 TID 없음");
                if (sn1 == string.Empty || sn2 == string.Empty) throw new Exception("해당 사건번호 없음");

                this.saNo = string.Format("{0}타경{1}", sn1, Convert.ToDecimal(sn2));
                db.Close();

                //1단계 시작
                ProcStep1(pdfFile);
            }
            catch (Exception ex)
            {
                this.analyRslt = "fail -> " + ex.Message;
            }
            finally
            {
                db.Close();
            }

            return this.analyRslt;
        }

        public void ProcStep1(string pdfFile)
        {
            string htmlFile, html, rgstIdNo, rgstDvsn, errMsg;
            int i = 0, gapBgnIdx = 0, eulBgnIdx = 0, mmlBgnIdx = 0, gddBgnIdx = 0, sumrABgnIdx = 0, sumrBBgnIdx = 0, sumrCBgnIdx = 0;
            int gapEndIdx = 0, eulEndIdx = 0, sumrAEndIdx = 0, sumrBEndIdx = 0, sumrCEndIdx = 0;

            List<string> listStrip = new List<string>();
            //listStrip.Add(@"^<div.*ft[1234567890]+"">(\[(집합건물|토지|건물)\]|고유번호|관할등기소|[\s\-]+이\s+하\s+여\s+백|[\s\*]+(본 등기사항증명서는|증명서는|실선으로|기록사항 없는)|[<b>]*(출력|열람)일시|[<b>]*\[\s(참 고 사 항|주 의 사 항)\s\]|<b>[본\s]*주요 등기사항|\(\s+(소유권에 관한|소유권 이외의)|가[\.\s]+등기기록에서).*/div>");
            listStrip.Add(@"^<div.*ft[1234567890]+"">(\[(집합건물|토지|건물)\]|고유번호|관할등기소|[\s\-]+이\s+하\s+여\s+백|[\s\*]+(본 등기사항증명서는|증명서는|실선으로|기록사항 없는)|[<b>]*(출력|열람)일시|[<b>]*\[\s(참 고 사 항|주 의 사 항)\s\]|(<b>)*[본\s]*주요 등기사항|\(\s+(소유권에 관한|소유권 이외의)|가[\.\s]+등기기록에서).*/div>");  //bullzip
            listStrip.Add(@"^<div.*ft[2]+"">(열 람 용).*/div>");
            listStrip.Add(@"^<div.*ft[7]+"">부동산등기법 제\d+.*/div>");
            listStrip.Add(@"<div.*>(순위번호|등\s+기\s+목\s+적|접\s+수|등\s+기\s+원\s+인|권리자\s+및\s+기타사항|등기명의인|\(주민\)등록번호|최종지분|주\s+소|등기목적|접수정보|주요등기사항|대상소유자|기록사항\s+없음)<.*/div>");
            //listStrip.Add(@"<div.*><b>\d+/\d+</b><.*/div>");
            listStrip.Add(@"<div.*>(<b>)*\d+/\d+(</b>)*<.*/div>");    //bullzip
            listStrip.Add(@"<div.*>\*\s+(실선으로\s+그어진|기록사항\s+없는|증명서는\s+컬러).*/div>");

            htmlFile = pdfFile.Replace(".pdf", ".html");
            if (!File.Exists(htmlFile))
            {
                Process proc = new Process();
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = cnvTool;
                psi.Arguments = @"-c -i -noframes -zoom 1 -enc UTF-8 """ + pdfFile + "\"";
                psi.WorkingDirectory = @"c:\";
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                proc.EnableRaisingEvents = false;
                proc.StartInfo = psi;
                proc.Start();
                proc.StandardInput.Write(Environment.NewLine);
                proc.StandardInput.Close();
                errMsg = proc.StandardError.ReadToEnd();
                if (proc.StandardError.ReadToEnd() != string.Empty)
                {
                    //throw new Exception("pdf -> html 변환 실패" + errMsg);
                    throw new Exception("pdf -> html 변환 실패");
                }
                proc.WaitForExit();
                proc.Close();
            }

            Stream stream = File.OpenRead(htmlFile);
            StreamReader sr = new StreamReader(stream, Encoding.UTF8);
            html = sr.ReadToEnd();
            sr.Close();
            sr.Dispose();
            stream.Close();
            stream.Dispose();
            //rgstDvsn = Regex.Match(html, @"<br>- (토지|건물|집합건물) -</b>", rxOptM).Groups[1].Value;
            rgstDvsn = Regex.Match(html, @"<br>- (토지|건물|집합건물) -(</b>|</span>)", rxOptM).Groups[1].Value;    //bullzip
            rgstIdNo = Regex.Match(html, @"고유번호 (\d{4}-\d{4}-\d{6})", rxOptM).Groups[1].Value;
            if (rgstDvsn == string.Empty)
            {
                throw new Exception("등기 구분 불가");
            }
            this.rgstDvsn = rgstDvsn;   //등기 구분
            this.rgstIdNo = rgstIdNo;   //등기 고유번호

            //pdf 생성 프로그램(pdfFactory 3.x 또는 Bullzip 11.x)
            PdfDocument pdfDoc = PdfReader.Open(pdfFile);
            this.pdfCreator = pdfDoc.Info.Creator;
            pdfDoc.Close();

            foreach (string str in listStrip)
            {
                html = Regex.Replace(html, str, string.Empty, rxOptM);
            }
            html = Regex.Replace(html, @" {2,}", " ", rxOptM);
            //MatchCollection mc = Regex.Matches(html, @"<div.*ft[13457890]+"">.*</div>", rxOptM);
            MatchCollection mc = Regex.Matches(html, @"<div.*ft[1234567890]+"">.*</div>", rxOptM);
            foreach (Match m in mc)
            {
                if (Regex.IsMatch(m.Value, @"【 갑 구 】", rxOptM)) gapBgnIdx = i;
                else if (Regex.IsMatch(m.Value, @"【 을 구 】", rxOptM)) eulBgnIdx = i;
                else if (Regex.IsMatch(m.Value, @"【 매 매 목 록 】", rxOptM)) mmlBgnIdx = i;
                else if (Regex.IsMatch(m.Value, @"【 공동담보목록 】", rxOptM)) gddBgnIdx = i;
                else if (Regex.IsMatch(m.Value, @"1. 소유지분현황", rxOptM)) sumrABgnIdx = i;
                else if (Regex.IsMatch(m.Value, @"2. 소유지분을 제외한 소유권에 관한 사항", rxOptM)) sumrBBgnIdx = i;
                else if (Regex.IsMatch(m.Value, @"3. \(근\)저당권 및 전세권", rxOptM))
                {
                    sumrCBgnIdx = i;
                    break;
                }
                i++;
            }
            int[] sectIdxArr = new int[] { gapBgnIdx, eulBgnIdx, mmlBgnIdx, gddBgnIdx, sumrABgnIdx, sumrBBgnIdx, sumrCBgnIdx };
            if (gapBgnIdx > 0)
            {
                gapEndIdx = getSectEndIdx(sectIdxArr, gapBgnIdx);
                ProcStep2("갑구", gapBgnIdx, gapEndIdx, mc);
            }
            if (eulBgnIdx > 0)
            {
                eulEndIdx = getSectEndIdx(sectIdxArr, eulBgnIdx);
                ProcStep2("을구", eulBgnIdx, eulEndIdx, mc);
            }
            if (sumrABgnIdx > 0)
            {
                sumrAEndIdx = getSectEndIdx(sectIdxArr, sumrABgnIdx);
                ProcStep2("요약A", sumrABgnIdx, sumrAEndIdx, mc);
            }
            if (sumrBBgnIdx > 0)
            {
                sumrBEndIdx = getSectEndIdx(sectIdxArr, sumrBBgnIdx);
                ProcStep2("요약B", sumrBBgnIdx, sumrBEndIdx, mc);
            }
            if (sumrCBgnIdx > 0)
            {
                sumrCEndIdx = mc.Count;
                ProcStep2("요약C", sumrCBgnIdx, sumrCEndIdx, mc);
            }

            ProcStep3();        //상세 분석

            if (dbPrc)
            {
                ProcStep4();  //DB 처리
            }
        }

        private int getSectEndIdx(int[] sectIdxArr, int sectBgnIdx)
        {
            int sectEndIdx = 0;

            foreach (int idx in sectIdxArr)
            {
                if (idx > sectBgnIdx)
                {
                    sectEndIdx = idx;
                    break;
                }
            }

            return sectEndIdx;
        }

        /// <summary>
        /// 기본 분석
        /// </summary>
        /// <param name="sect"></param>
        /// <param name="bgnIdx"></param>
        /// <param name="endIdx"></param>
        /// <param name="mc"></param>
        private void ProcStep2(string sect, int bgnIdx, int endIdx, MatchCollection mc)
        {
            int i = 0, n = 0, left = 0, top = 0, prev_top = 0;
            string txt = "", rank = "", prps = "", rcpt = "", resn = "", prsn = "";

            //pdfFactory
            int rankEnd = 89, prpsEnd = 190, rcDtEnd = 270, resnEnd = 351;  //순위번호, 등기목적, 접수, 등기원인, 권리자 및 기타사항(마지막 칼럼)
            int prsnEnd = 124, rgNoEnd = 205, shrEnd = 270, adrsEnd = 538;  //등기명의인, (주민)등록번호, 최종지분, 주소, 순위번호(마지막 칼럼)

            //bullzip
            if (pdfCreator.IndexOf("bullzip",StringComparison.CurrentCultureIgnoreCase) > -1)
            {
                rankEnd = 71; prpsEnd = 172; rcDtEnd = 252; resnEnd = 333;  //순위번호, 등기목적, 접수, 등기원인, 권리자 및 기타사항(마지막 칼럼)
                prsnEnd = 106; rgNoEnd = 187; shrEnd = 252; adrsEnd = 520;  //등기명의인, (주민)등록번호, 최종지분, 주소, 순위번호(마지막 칼럼)
            }

            n = 0;
            bgnIdx = bgnIdx + 1;
            for (i = bgnIdx; i < endIdx; i++)
            {
                Match m = Regex.Match(mc[i].Value, @"<div.*top:(\d+);left:(\d+)"">.*<span.*?>(.*)</span>", rxOptM);
                top = Convert.ToInt32(m.Groups[1].Value);
                left = Convert.ToInt32(m.Groups[2].Value);
                txt = m.Groups[3].Value;
                if (txt == string.Empty) continue;

                if (left < resnEnd)
                {
                    txt += "\r\n";
                    txt = txt.Replace("<br>", "\r\n");
                    //txt = m.Groups[3].Value.Replace("<br>", "\r\n");
                }
                else
                {
                    if (txt.Contains("<br>")) txt = txt.Replace("<br>", " ");
                }

                if (sect == "요약A")
                {
                    if (left < prsnEnd && n > 0 && (top - prev_top) > 20)
                    {
                        //if (Regex.IsMatch(txt, @".*\([소공]") && !Regex.IsMatch(txt, @"^\([소공]|^[유자]\)"))
                        //{
                        dtA.Rows.Add(sect, rank, prps.Trim(), rcpt.Trim(), resn.Trim(), prsn.Trim());
                        rank = ""; prps = ""; rcpt = ""; resn = ""; prsn = "";
                        //}
                    }
                    if (left < prsnEnd)
                    {
                        prps += txt;    //등기명의인
                        prev_top = top;
                    }
                    else if (left < rgNoEnd) rcpt += txt;   //(주민)등록번호
                    else if (left < shrEnd) resn += txt;    //최종지분
                    else if (left < adrsEnd) prsn += txt;   //주소
                    else
                    {
                        rank += txt;
                        if (Regex.IsMatch(rank, @"[()]")) rank = string.Empty;
                    }
                }
                else
                {
                    if (left < rankEnd && n > 0)
                    {
                        if (!Regex.IsMatch(txt, @"[()]"))
                        {
                            dtA.Rows.Add(sect, rank.Trim(), prps.Trim(), rcpt.Trim(), resn.Trim(), prsn.Trim());
                            rank = ""; prps = ""; rcpt = ""; resn = ""; prsn = "";
                        }
                    }
                    if (left < rankEnd)
                    {
                        if (!Regex.IsMatch(txt, @"[()]")) rank = txt;
                    }
                    else if (left < prpsEnd) prps += txt;
                    else if (left < rcDtEnd) rcpt += txt;
                    else if (left < resnEnd) resn += txt;
                    else
                    {
                        //prsn += (top == prev_top) ? " " + txt : "\r\n" + txt;
                        if (top == prev_top)
                        {
                            prsn += " " + txt;
                        }
                        else
                        {
                            prsn += (left > resnEnd && !(sect.Contains("요약"))) ? "\r\n>" + txt : "\r\n" + txt;
                        }
                        prev_top = top;
                    }
                }
                n++;
            }
            if (rank != "")
            {
                dtA.Rows.Add(sect, rank.Trim(), prps.Trim(), rcpt.Trim(), resn.Trim(), prsn.Trim());
            }
            else
            {
                int rowIdx = dtA.Rows.Count - 1;
                dtA.Rows[rowIdx]["prps"] = dtA.Rows[rowIdx]["prps"].ToString() + prps.Trim();
                dtA.Rows[rowIdx]["rcpt"] = dtA.Rows[rowIdx]["rcpt"].ToString() + rcpt.Trim();
                dtA.Rows[rowIdx]["resn"] = dtA.Rows[rowIdx]["resn"].ToString() + resn.Trim();
                dtA.Rows[rowIdx]["prsn"] = dtA.Rows[rowIdx]["prsn"].ToString() + prsn.Trim();
            }
        }

        /// <summary>
        /// 상세 분석
        /// </summary>
        private void ProcStep3()
        {
            string sect, rank, rankPrnt, prps, prpsSmr, rcpt, resn, prsnEtc, shrStr, frtn, stdPrsn;
            string ownType = "", prpsOrg = "", rgCd = "", rcDt = "", rcNo = "", prsn = "", rgNo = "", adrs = "", brch = "", cAmt = "", rENo = "", mvDt = "", bzDt = "", fxDt = "", bgnDt = "", endDt = "", chDt = "", tmpDspt = "", note = "", del = "";
            string siCd = "", guCd = "", dnCd = "";
            int rankLen = 0, shrCnt = 0, grpCnt = 0;
            bool chkPrsn = false;
            List<string> lsNote = new List<string>();
            IDictionary<string, string> dict = new Dictionary<string, string>();

            DataRow[] rows = dtA.Select("sect like '요약A'");
            foreach (DataRow row in rows)
            {
                rgNo = ""; adrs = "";

                sect = "갑구";
                rank = row["rank"].ToString().Replace(" ", string.Empty);
                prpsSmr = Regex.Replace(row["prps"].ToString(), @"[\r\n]", string.Empty, rxOptS);
                rcpt = row["rcpt"].ToString();
                //resn = row["resn"].ToString();
                shrStr = row["resn"].ToString();
                prsnEtc = row["prsn"].ToString();
                rgNo = Regex.Replace(rcpt, @"\-?\*+", string.Empty, rxOptS).Trim();
                adrs = Regex.Replace(prsnEtc, @"[\r\n]", string.Empty, rxOptS).Trim();

                string[] rankArr = rank.Split(',');
                rankLen = rankArr.Length;
                foreach (string rankNo in rankArr)
                {
                    lsNote.Clear();
                    ownType = ""; prpsOrg = ""; prps = ""; rgCd = ""; rcDt = ""; rcNo = ""; prsn = ""; brch = ""; cAmt = ""; rENo = ""; mvDt = ""; bzDt = ""; fxDt = ""; bgnDt = ""; endDt = ""; chDt = ""; tmpDspt = ""; note = ""; del = "0";
                    siCd = ""; guCd = ""; dnCd = "";

                    prsn = Regex.Replace(prpsSmr, @"\(소유자\)|[\r\n]", string.Empty, rxOptS).Replace("주식회사", "(주)").Trim();
                    var xRow = dtA.Rows.Cast<DataRow>().Where(t => t["sect"].ToString() == "갑구" && t["rank"].ToString() == rankNo).FirstOrDefault();
                    if (xRow == null) continue;
                    prps = Regex.Replace(xRow["prps"].ToString(), @"[\r\n]", string.Empty, rxOptS).Trim();
                    prpsOrg = prps;
                    rcpt = xRow["rcpt"].ToString();
                    rcDt = getDateParse(rcpt);
                    rcNo = getRcNoParse(rcpt);
                    resn = xRow["resn"].ToString();
                    prsnEtc = xRow["prsn"].ToString();
                    ownType = Regex.Replace(resn, @"\d+년\d+월\d+일", string.Empty, rxOptS).Trim();
                    ownType = Regex.Replace(ownType.Replace("\r\n", string.Empty), @"신탁재산의귀속신탁재산의귀속", "신탁재산의귀속", rxOptS).Trim();
                    if (ownType != "") lsNote.Add(ownType);
                    if (prsnEtc.Contains("거래가액"))
                    {
                        Dictionary<string, string> dicAmt = getAmtParse(prsnEtc);
                        if (dicAmt["amt"] != "")
                        {
                            lsNote.Add("거래가액:" + dicAmt["amt"] + "원");
                            if (dicAmt["unit"] != "" && dicAmt["unit"] != "원") lsNote.Add(string.Format("{0}{1}-적용환율:{2}", dicAmt["amtStr"], dicAmt["unit"], dicAmt["exRate"]));
                        }
                    }

                    if (lsNote.Count > 0)
                    {
                        note = string.Join("\r\n", lsNote.ToArray());
                        note = Regex.Replace(note, @"\r\n>", " ", rxOptS);
                    }
                    if (prsn != "") prsn = ReNamePrsn(prsn);
                    if (prpsOrg.Contains("소유권이전") && prpsOrg.Contains("신탁"))
                    {
                        prpsOrg = "소유권이전";
                    }

                    DataRow dr = dtB.NewRow();
                    dr["sect"] = sect;
                    dr["rank"] = rankNo;
                    dr["prpsOrg"] = prpsOrg;
                    dr["prps"] = prps;
                    dr["rcDt"] = (chDt == "") ? rcDt : chDt;
                    dr["rcNo"] = rcNo;
                    dr["rENo"] = rENo;
                    dr["shrStr"] = shrStr;
                    dr["adrs"] = adrs;
                    dr["brch"] = brch;
                    dr["prsn"] = prsn;
                    dr["prsnOrg"] = prsn;
                    dr["rgNo"] = rgNo;
                    dr["cAmt"] = cAmt;
                    dr["mvDt"] = mvDt;
                    dr["bzDt"] = bzDt;
                    dr["fxDt"] = fxDt;
                    dr["bgnDt"] = bgnDt;
                    dr["endDt"] = endDt;
                    dr["note"] = note;
                    dr["siCd"] = "";
                    dr["guCd"] = "";
                    dr["dnCd"] = "";

                    dr["del"] = del;
                    rgCd = getRgCd(prps);   //등기목적(권리)-코드
                    if (del != "1" && rgCd == "")
                    {
                        if (prps.Contains("지분전부이전")) rgCd = "20";
                        else if (prps.Contains("소유권이전")) rgCd = "10";
                        else if (prps.Contains("소유권경정")) rgCd = "";
                        else rgCd = "20";
                    }
                    dr["rgCd"] = (del == "1") ? "" : rgCd;
                    if (rankLen > 1 || prpsSmr.Contains("공유자"))
                    {
                        dr["rpt"] = "1";
                    }

                    //소유권 보존-접수일 없는 경우
                    if (dr["rcDt"].ToString() == string.Empty)
                    {
                        xRow = dtA.Select("sect='갑구' and rank='" + rankNo + "'").FirstOrDefault();
                        if (xRow != null)
                        {
                            Match match = Regex.Match(xRow["prsn"].ToString(), @"([\w\s]+의하여|[\w\s]+인하여)[\s]*(\d+년\d+월\d+일)");
                            //Match match = Regex.Match(xRow["prsn"].ToString(), @"([\w\s]+의하여)[\s]*(\d+년\d+월\d+일)");
                            if (match.Success)
                            {
                                dr["note"] = (dr["note"].ToString() + " " + match.Groups[1].Value).Trim();
                                dr["rcDt"] = getDateParse(match.Groups[2].Value);
                            }
                        }
                    }

                    dtB.Rows.Add(dr);
                }
            }

            rows = dtA.Select("sect in ('요약B','요약C')");
            foreach (DataRow row in rows)
            {
                sect = row["sect"].ToString();
                rank = row["rank"].ToString();
                prps = Regex.Replace(row["prps"].ToString(), @"[\r\n]", string.Empty, rxOptS);
                rcpt = row["rcpt"].ToString();
                resn = row["resn"].ToString();
                prsnEtc = row["prsn"].ToString();

                lsNote.Clear();
                ownType = ""; prpsOrg = ""; rgCd = ""; rcDt = ""; rcNo = ""; prsn = ""; rgNo = ""; adrs = ""; brch = ""; cAmt = ""; rENo = ""; mvDt = ""; bzDt = ""; fxDt = ""; bgnDt = ""; endDt = ""; chDt = ""; tmpDspt = ""; note = ""; del = "0";
                siCd = ""; guCd = ""; dnCd = "";

                rcDt = getDateParse(rcpt);
                rcNo = getRcNoParse(rcpt);
                sect = (sect == "요약B") ? "갑구" : "을구";
                DataRow xRow = null;
                if (prps.Contains("(")) { xRow = dtA.Rows.Cast<DataRow>().Where(t => t["sect"].ToString() == sect && t["rank"].ToString() == rank && t["prps"].ToString() == prps).FirstOrDefault(); }
                else { xRow = dtA.Rows.Cast<DataRow>().Where(t => t["sect"].ToString() == sect && t["rank"].ToString() == rank).FirstOrDefault(); } //2118657-갑구 8번 2개 존재하여 에러(SingleOrDefault 사용시)
                if (xRow == null) continue;

                prpsOrg = xRow["prps"].ToString();
                prsnEtc = xRow["prsn"].ToString();
                resn = xRow["resn"].ToString();
                rcpt = xRow["rcpt"].ToString();
                rENo = getRefENo(prps, resn);
                Dictionary<string, string> dicAdrsBranch = getAdrsBranch(prsnEtc);
                adrs = dicAdrsBranch["adrs"];
                brch = dicAdrsBranch["brch"];
                Dictionary<string, string> dicPrsnRgNo = getPrsnRgNo(prsnEtc);
                prsn = dicPrsnRgNo["prsn"];
                rgNo = dicPrsnRgNo["rgNo"];
                Dictionary<string, string> dicAmt = getAmtParse(prsnEtc);
                cAmt = dicAmt["amt"];
                if (dicAmt["unit"] != "" && dicAmt["unit"] != "원") lsNote.Add(string.Format("{0}{1}-적용환율:{2}", dicAmt["amtStr"], dicAmt["unit"], dicAmt["exRate"]));
                if (Regex.Match(prps, @"임차권|전세권|지상권|지역권", rxOptM).Success)
                {
                    Dictionary<string, string> dicLease = getLeaseSf(prps, xRow);
                    //prps = dicLease["prps"];
                    mvDt = dicLease["mvDt"];
                    bzDt = dicLease["bzDt"];
                    fxDt = dicLease["fxDt"];
                    bgnDt = dicLease["bgnDt"];
                    endDt = dicLease["endDt"];
                    chDt = dicLease["chDt"];
                    if (dicLease["note"] != "") lsNote.Add(dicLease["note"]);
                }
                if (Regex.IsMatch(prps, @"소유권이전청구권가등기"))
                {
                    lsNote.Add(Regex.Replace(resn, @"\d+년\d+월\d+일", string.Empty).Trim());
                }
                if (Regex.IsMatch(prps, @"가처분"))
                {
                    tmpDspt = Regex.Match(prsnEtc, @"피보전권리[\s]*(.*)", rxOptS).Groups[1].Value;
                    //tmpDspt = Regex.Replace(tmpDspt, rightPtrn + "[ ].*", string.Empty, rxOptS).Trim();
                    if (tmpDspt.Contains("채권자"))
                    {
                        tmpDspt = tmpDspt.Remove(tmpDspt.LastIndexOf("채권자")).Trim();
                    }
                    if (tmpDspt != "") lsNote.Add(tmpDspt);
                }
                if (Regex.IsMatch(prps, @"근저당권설정") && Regex.IsMatch(resn, @"도시및주거환경[\s]*정비사업", rxOptS))
                {
                    chDt = getDateParse(Regex.Match(resn, @"(\d+년\d+월\d+일)[\s]*설정계약", rxOptS).Groups[1].Value);
                    lsNote.Add(string.Format("{0} {1}", rcDt, "도시 및 주거환경정비사업으로 인한 이전고시"));
                }
                if (prps == "보전처분")
                {
                    lsNote.Add(prsnEtc);
                }

                if (lsNote.Count > 0)
                {
                    note = string.Join("\r\n", lsNote.ToArray());
                    note = Regex.Replace(note, @"\r\n>", " ", rxOptS);
                }
                if (prsn == string.Empty)
                {
                    xRow = dtA.Select("sect='" + row["sect"].ToString() + "' and rank='" + rank + "'").FirstOrDefault();
                    if (xRow != null)
                    {
                        Match match = Regex.Match(xRow["resn"].ToString(), rightPtrn + @"[ ]*(.*)", rxOptS);
                        if (match.Success)
                        {
                            prsn = match.Groups[2].Value.Trim();
                        }
                    }
                }
                if (prsn != "") prsn = ReNamePrsn(prsn);
                if (prpsOrg.Contains("소유권이전") && prpsOrg.Contains("신탁"))
                {
                    prpsOrg = "소유권이전";
                }

                DataRow dr = dtB.NewRow();
                dr["sect"] = sect;
                dr["rank"] = rank;
                dr["prpsOrg"] = prpsOrg;
                dr["prps"] = prps;
                dr["rcDt"] = (chDt == "") ? rcDt : chDt;
                dr["rcNo"] = rcNo;
                dr["rENo"] = rENo;
                dr["adrs"] = adrs;
                dr["brch"] = brch;
                dr["prsn"] = prsn;
                dr["prsnOrg"] = prsn;
                dr["rgNo"] = rgNo;
                dr["cAmt"] = cAmt;
                dr["mvDt"] = mvDt;
                dr["bzDt"] = bzDt;
                dr["fxDt"] = fxDt;
                dr["bgnDt"] = bgnDt;
                dr["endDt"] = endDt;
                dr["note"] = note;

                dict.Clear();
                dict = api.DaumSrchAdrs(adrs);
                siCd = dict["sidoCd"];
                if (siCd == "")
                {
                    AdrsParser parser = new AdrsParser(adrs);
                    dict = api.DaumSrchAdrs(parser.AdrsM);
                    siCd = dict["sidoCd"];
                }
                guCd = dict["gugunCd"];
                dnCd = dict["dongCd"];
                dr["siCd"] = siCd;
                dr["guCd"] = guCd;
                dr["dnCd"] = dnCd;

                if (rank.IndexOf("-") > -1 && !Regex.IsMatch(prps, @"(일부)|(가처분)|(질권)|(공매공고)"))
                {
                    if (!Regex.IsMatch(prpsOrg.Replace("\r\n", string.Empty), @"전세권근저당권설정", rxOptS))    //전세권설정의 부기등기는 제외
                    {
                        DataReplace(dr);
                    }
                    del = "1";
                }
                dr["del"] = del;

                rgCd = getRgCd(prps);   //등기목적(권리)-코드
                dr["rgCd"] = (del == "1") ? "" : rgCd;
                //dr["hide"] = (rank.IndexOf("-") > -1 && del != "1") ? "1" : "0";
                if (rank.IndexOf("-") > -1 && del != "1")
                {
                    dr["rpt"] = "1";
                    if (prps.Contains("가처분") || prps.Contains("공매공고")) dr["hide"] = "0";
                    else dr["hide"] = "1";
                }
                else
                {
                    dr["hide"] = "0";
                    dr["rpt"] = "0";
                }

                //요약에서는 [임차권설정]이나 원래 등기목적이 다를 경우
                if (del != "1" && prps == "임차권설정")
                {
                    if (prpsOrg == "주택임차권")
                    {
                        dr["rgCd"] = "9";
                    }
                    else if (prpsOrg == "상가건물임차권")
                    {
                        dr["rgCd"] = "28";
                    }
                }

                //압류부기로 나오는 공매공고는 본등기의 권리자와 동일하게 입력
                if (rank.IndexOf("-") > -1 && prps == "공매공고")
                {
                    rankPrnt = Regex.Replace(dr["rank"].ToString(), @"\-.*", string.Empty);
                    xRow = dtB.Select("sect='" + sect + "' and rank='" + rankPrnt + "'").FirstOrDefault();
                    if (xRow != null)
                    {
                        dr["prsn"] = xRow["prsn"];
                    }
                }

                //근저당권자가 변경되는 경우 -> 등기원인+이전 권리자
                if (rank.IndexOf("-") > -1 && prps.Contains("근저당권이전"))
                {
                    rankPrnt = Regex.Replace(dr["rank"].ToString(), @"\-.*", string.Empty);
                    xRow = dtB.Select("sect='" + sect + "' and rank='" + rankPrnt + "'").FirstOrDefault();
                    if (xRow != null)
                    {
                        if (xRow["prsn"].ToString() != xRow["prsnOrg"].ToString())
                        {
                            xRow["note"] = Regex.Replace(resn, @"\d+년\d+월\d+일", string.Empty, rxOptS).Trim() + "전:" + xRow["prsnOrg"].ToString();
                            chkPrsn = true;
                        }
                    }
                }

                //
                if (prps.Contains("지상권"))
                {
                    if (dr["cAmt"].ToString() != string.Empty && dr["cAmt"].ToString() != "0")
                    {
                        dr["note"] = note + ", 지료:" + dr["cAmt"].ToString() + "원";
                    }
                    dr["cAmt"] = string.Empty;
                }

                dtB.Rows.Add(dr);
            }

            //근저당권 이전 발생시 -> 근저당권 1개, 임의경매 1개 일 경우 권리자명 치환
            if (chkPrsn == true && dtB.Select("sect='갑구' and prps='임의경매개시결정'").Count() == 1 && dtB.Select("sect='을구' and prps='근저당권설정'").Count() == 1)
            {
                DataRow xRow1 = dtB.Select("sect='갑구' and prps='임의경매개시결정'").FirstOrDefault();
                DataRow xRow2 = dtB.Select("sect='을구' and prps='근저당권설정'").FirstOrDefault();
                if (xRow1["prsn"].ToString() == xRow2["prsnOrg"].ToString())
                {
                    xRow1["prsn"] = xRow2["prsn"].ToString();
                }
            }

            //파산선고
            rows = dtA.Select("sect='갑구' and prps='파산선고'");
            foreach (DataRow row in rows)
            {
                /*prsnEtc = row["prsn"].ToString();
                Dictionary<string, string> dicAdrsBranch = getAdrsBranch(prsnEtc);
                adrs = dicAdrsBranch["adrs"];
                brch = dicAdrsBranch["brch"];
                Dictionary<string, string> dicPrsnRgNo = getPrsnRgNo(prsnEtc);
                prsn = dicPrsnRgNo["prsn"];
                rgNo = dicPrsnRgNo["rgNo"];*/

                DataRow dr = dtB.NewRow();
                dr["sect"] = "갑구";
                dr["rank"] = row["rank"].ToString();
                dr["prpsOrg"] = row["prps"].ToString();
                dr["prps"] = row["prps"].ToString();
                dr["rcDt"] = getDateParse(row["rcpt"].ToString());
                dr["rcNo"] = getRcNoParse(row["rcpt"].ToString());
                dr["rENo"] = getRefENo(row["prps"].ToString(), row["resn"].ToString());
                dr["adrs"] = "";
                dr["brch"] = "";
                dr["prsn"] = "";
                dr["rgNo"] = "";
                dr["cAmt"] = "";
                dr["mvDt"] = "";
                dr["bzDt"] = "";
                dr["fxDt"] = "";
                dr["bgnDt"] = "";
                dr["endDt"] = "";
                dr["note"] = Regex.Replace(row["resn"].ToString(), @"\d+년\d+월\d+일|[\r\n]", string.Empty, rxOptS).Trim();
                dr["siCd"] = "";
                dr["guCd"] = "";
                dr["dnCd"] = "";
                dr["hide"] = "1";
                dr["del"] = "0";
                dr["rgCd"] = "29";
                dr["rpt"] = "1";
                dtB.Rows.Add(dr);
            }

            //상호변경 일괄처리
            string newNm, oldNm;
            DataRow[] drs = dtB.Select("del=0 and (prsn like '%변경전%' or prsn like '%합병전%' or prsn like '%양도전%')");
            if (drs != null)
            {
                Match m = null;
                foreach (DataRow dr in drs)
                {
                    if (dr["prsn"].ToString().Contains("변경전"))
                    {
                        m = Regex.Match(dr["prsn"].ToString(), @"(.*)[ ]*\(변경전[상호:\s]*(.*)\)", rxOptM);
                    }
                    else if (dr["prsn"].ToString().Contains("합병전"))
                    {
                        m = Regex.Match(dr["prsn"].ToString(), @"(.*)[ ]*\(합병전[상호:\s]*(.*)\)", rxOptM);
                    }
                    else if (dr["prsn"].ToString().Contains("양도전"))
                    {
                        m = Regex.Match(dr["prsn"].ToString(), @"(.*)[ ]*\(양도전[상호:\s]*(.*)\)", rxOptM);
                    }
                    if (m == null) continue;
                    if (m.Success == false) continue;
                    newNm = m.Groups[1].Value;
                    oldNm = m.Groups[2].Value;
                    foreach (DataRow row in dtB.Rows)
                    {
                        if (row["prsn"].ToString() == oldNm) row["prsn"] = newNm;
                    }
                    if (dr["prsn"].ToString().Contains("변경전"))
                    {
                        dtB.Rows[dtB.Rows.IndexOf(dr)]["note"] = (dtB.Rows[dtB.Rows.IndexOf(dr)]["note"].ToString() + "\r\n변경전:" + oldNm).Trim();
                        dtB.Rows[dtB.Rows.IndexOf(dr)]["prsn"] = Regex.Replace(dtB.Rows[dtB.Rows.IndexOf(dr)]["prsn"].ToString(), @"\(변경전.*", string.Empty, rxOptS).Trim();
                    }
                    else if (dr["prsn"].ToString().Contains("합병전"))
                    {
                        dtB.Rows[dtB.Rows.IndexOf(dr)]["note"] = (dtB.Rows[dtB.Rows.IndexOf(dr)]["note"].ToString() + "\r\n합병전:" + oldNm).Trim();
                        dtB.Rows[dtB.Rows.IndexOf(dr)]["prsn"] = Regex.Replace(dtB.Rows[dtB.Rows.IndexOf(dr)]["prsn"].ToString(), @"\(합병전.*", string.Empty, rxOptS).Trim();
                    }
                    else if (dr["prsn"].ToString().Contains("양도전"))
                    {
                        dtB.Rows[dtB.Rows.IndexOf(dr)]["note"] = (dtB.Rows[dtB.Rows.IndexOf(dr)]["note"].ToString() + "\r\n양도전:" + oldNm).Trim();
                        dtB.Rows[dtB.Rows.IndexOf(dr)]["prsn"] = Regex.Replace(dtB.Rows[dtB.Rows.IndexOf(dr)]["prsn"].ToString(), @"\(양도전.*", string.Empty, rxOptS).Trim();
                    }
                }
            }

            //말소기준등기 체크
            //dtB.DefaultView.Sort = "rcDt asc";
            dtB.DefaultView.Sort = "rcDt asc, rcNo asc";
            //dtB.DefaultView.Sort = "rcDt asc, rcNo asc, rgNo asc";
            DataRow eRow = dtB.Select("del=0 and rgCd in (1,2,3,4,5,8,12,16,25,31)").OrderBy(x => x["rcDt"]).FirstOrDefault();
            if (eRow == null)
            {
                //말소기준등기 없음
            }
            else
            {
                if (eRow["rgCd"].ToString() == "8" || eRow["rgCd"].ToString() == "12")  //8:전세권, 12:소청가(소유권이전청구권가등기)
                {
                    DataRow eRow2 = dtB.Select("del=0 and rgCd='4' and prsn='" + eRow["prsn"].ToString() + "'").FirstOrDefault();     //임의경매 신청자명과 같을 경우에는 말소기준이 될 수 있다.
                    if (eRow2 != null)
                    {
                        dtB.Rows[dtB.Rows.IndexOf(eRow)]["ekey"] = "1";
                    }
                    else
                    {
                        DataRow eRow3 = dtB.Select("del=0 and rgCd in (1,2,3,4,5,16,25,31)").FirstOrDefault();
                        if (eRow3 != null)
                        {
                            dtB.Rows[dtB.Rows.IndexOf(eRow3)]["ekey"] = "1";
                        }
                    }
                }
                else
                {
                    dtB.Rows[dtB.Rows.IndexOf(eRow)]["ekey"] = "1";
                }
            }

            //공유자 정리
            lsNote.Clear();
            frtn = "";
            Regex rx = new Regex(@"(\d+)[ ]*분의[ ]*(\d+)", rxOptM);
            rows = dtB.Select("prsn like '%공유자%'");
            if (rows != null)
            {
                //소유자중 기준인 선정
                DataRow rowStd = dtA.Select("sect='요약B' and (prps='임의경매개시결정' or prps='강제경매개시결정')").FirstOrDefault();
                if (rowStd != null)
                {
                    if (rowStd["prsn"].ToString().Contains("확인불가"))
                    {
                        stdPrsn = string.Empty;
                    }
                    else
                    {
                        stdPrsn = rowStd["prsn"].ToString().Replace(" 등", string.Empty).Trim();
                    }
                }
                else
                {
                    stdPrsn = string.Empty;
                }

                shrCnt = rows.Count();
                if (shrCnt > 1)
                {
                    grpCnt = rows.GroupBy(r => r.Field<string>("shrStr")).Count();
                    foreach (DataRow row in rows)
                    {
                        Match match = rx.Match(row["shrStr"].ToString());
                        frtn = (match.Success) ? string.Format("{0}/{1}", match.Groups[2].Value, match.Groups[1].Value) : string.Empty;
                        prsn = row["prsn"].ToString().Replace("(공유자)", string.Empty).Trim();
                        if (grpCnt == 1)
                        {
                            if (!lsNote.Contains(prsn))
                            {
                                lsNote.Add(string.Format("{0}", prsn));
                            }
                        }
                        else
                        {
                            if (!lsNote.Contains(string.Format("{0} {1}", prsn, frtn)))
                            {
                                lsNote.Add(string.Format("{0} {1}", prsn, frtn));
                            }
                        }
                    }
                    DataRow targetRow = rows[0];
                    if (stdPrsn != string.Empty)
                    {
                        targetRow = rows.Where(r => r["prsn"].ToString().Contains(stdPrsn)).FirstOrDefault();
                        if (targetRow == null) targetRow = rows[0];
                    }

                    if (targetRow["rgCd"].ToString() == "20")
                    {
                        //이전
                        targetRow["prsn"] = targetRow["prsn"].ToString().Replace("(공유자)", string.Empty).Trim();
                    }
                    else
                    {
                        targetRow["prsn"] = string.Format("{0} 외{1}", targetRow["prsn"].ToString().Replace("(공유자)", string.Empty).Trim(), (lsNote.Count - 1));
                    }

                    if (grpCnt == 1)
                    {
                        targetRow["note"] = targetRow["note"].ToString() + " " + string.Join(",", lsNote.ToArray()) + " 각 " + frtn;
                    }
                    else
                    {
                        targetRow["note"] = targetRow["note"].ToString() + " " + string.Join(",", lsNote.ToArray());
                    }
                    foreach (DataRow row in rows)
                    {
                        if (row != targetRow) row["del"] = "1";
                    }
                }
            }

            //소청가+소유권 이전
            rows = dtB.Select("prps='소유권이전청구권가등기소유권이전'");
            if (rows != null)
            {
                foreach (DataRow row in rows)
                {
                    DataRow xRow = dtA.Select("sect='갑구' and rank='" + row["rank"].ToString() + "'").FirstOrDefault();
                    if (xRow != null)
                    {
                        row["prpsOrg"] = "소유권이전";
                        Match m1 = Regex.Match(xRow["rcpt"].ToString(), @"(\d{4}년\d+월\d+일).*(\d{4}년\d+월\d+일)", rxOptS);
                        Match m2 = Regex.Match(xRow["resn"].ToString(), @"(\d{4}년\d+월\d+일)(.*)(\d{4}년\d+월\d+일)(.*)", rxOptS);
                        if (m1.Success && m2.Success)
                        {
                            row["note"] = string.Format("{0}, {1} 가등기에 기한 본등기 이행", m2.Groups[4].Value, m1.Groups[2].Value);
                        }
                        else if (m1.Success)
                        {
                            row["note"] = string.Format("{0} 가등기에 기한 본등기 이행", m1.Groups[2].Value);
                        }
                    }
                }
            }

            this.analyRslt = "success";
        }

        /// <summary>
        /// DB 처리
        /// </summary>
        private void ProcStep4()
        {
            string sql, tbl, cvp, dvsnCd, sect, rank, rank_s, filter = "0";
            string billAmt = "", bAmt = "", aply = "", take = "", note = "", creditor = "", tmpCreditor = "", prsnCnt = "";
            bool eKey = false;
            List<MySqlParameter> sp = new List<MySqlParameter>();
            Match match;

            tbl = (mdfyPrc == true) ? "db_tank.tx_rgst_cmp" : "db_main.ta_rgst";

            if (rgstDvsn == "토지") dvsnCd = "1";
            else if (rgstDvsn == "건물") dvsnCd = "2";
            else if (rgstDvsn == "집합건물") dvsnCd = "3";
            else return;

            db.Open();
            if (mdfyPrc == true)
            {
                if (dvsnCd == "1")
                {
                    sql = "delete from db_tank.tx_rgst_cmp where tid=" + tid + " and rg_dvsn=1";
                }
                else
                {
                    sql = "delete from db_tank.tx_rgst_cmp where tid=" + tid + " and rg_dvsn IN (2,3)";
                }
            }
            else
            {
                if (dvsnCd == "1")
                {
                    sql = "delete from ta_rgst where tid=" + tid + " and rg_dvsn=1";
                }
                else
                {
                    sql = "delete from ta_rgst where tid=" + tid + " and rg_dvsn IN (2,3)";
                }
            }
            db.ExeQry(sql);

            //기저장된 청구금액            
            sql = "select bill_amt from ta_dtl where tid=" + tid + " limit 1";
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            if (dr.HasRows) billAmt = dr["bill_amt"].ToString();
            dr.Close();
            db.Close();

            db.Open();
            foreach (DataRow row in dtB.Rows)
            {
                if (row["del"].ToString() == "1") continue;

                sect = (row["sect"].ToString() == "갑구") ? "1" : "2";
                match = Regex.Match(row["rank"].ToString(), @"(\d+)[\-]*(\d+)*", rxOptS);
                rank = match.Groups[1].Value;
                rank_s = match.Groups[2].Value;
                if (row["rENo"].ToString() == saNo)
                {
                    bAmt = billAmt;
                    aply = "1";
                    creditor = row["prsn"]?.ToString() ?? string.Empty;
                }
                else
                {
                    bAmt = string.Empty;
                    aply = string.Empty;
                }

                note = row["note"].ToString();
                take = (row["rgCd"].ToString() == "13" && (note.Contains("인도") || note.Contains("철거"))) ? "1" : "0";
                sql = "insert into " + tbl + " set tid=@tid, rg_dvsn=@rg_dvsn, sect=@sect, rank=@rank, rank_s=@rank_s, rg_cd=@rg_cd, rg_nm=@rg_nm, rc_dt=@rc_dt, rc_no=@rc_no, b_amt=@b_amt, c_amt=@c_amt, prsn=@prsn, rg_no=@rg_no, mv_dt=@mv_dt, fx_dt=@fx_dt, bgn_dt=@bgn_dt, end_dt=@end_dt, ";
                sql += "r_eno=@r_eno, aply=@aply, take=@take, ekey=@ekey, note=@note, adrs=@adrs, brch=@brch, si_cd=@si_cd, gu_cd=@gu_cd, dn_cd=@dn_cd, hide=@hide";
                sp.Add(new MySqlParameter("@tid", tid));
                sp.Add(new MySqlParameter("@rg_dvsn", dvsnCd));
                sp.Add(new MySqlParameter("@sect", sect));
                sp.Add(new MySqlParameter("@rank", rank));
                sp.Add(new MySqlParameter("@rank_s", rank_s));
                sp.Add(new MySqlParameter("@rg_cd", row["rgCd"]));
                sp.Add(new MySqlParameter("@rg_nm", row["prpsOrg"]));
                sp.Add(new MySqlParameter("@rc_dt", row["rcDt"]));
                sp.Add(new MySqlParameter("@rc_no", row["rcNo"]));
                sp.Add(new MySqlParameter("@b_amt", bAmt));
                sp.Add(new MySqlParameter("@c_amt", row["cAmt"].ToString().Replace(",", string.Empty)));
                sp.Add(new MySqlParameter("@prsn", row["prsn"]));
                sp.Add(new MySqlParameter("@rg_no", row["rgNo"]));
                sp.Add(new MySqlParameter("@mv_dt", row["mvDt"]));
                sp.Add(new MySqlParameter("@fx_dt", row["fxDt"]));
                sp.Add(new MySqlParameter("@bgn_dt", row["bgnDt"]));
                sp.Add(new MySqlParameter("@end_dt", row["endDt"]));
                sp.Add(new MySqlParameter("@r_eno", row["rENo"]));
                sp.Add(new MySqlParameter("@aply", aply));
                sp.Add(new MySqlParameter("@take", take));
                sp.Add(new MySqlParameter("@ekey", row["ekey"]?.ToString() ?? ""));
                sp.Add(new MySqlParameter("@note", row["note"]));
                sp.Add(new MySqlParameter("@adrs", row["adrs"]));
                sp.Add(new MySqlParameter("@brch", row["brch"]));
                sp.Add(new MySqlParameter("@si_cd", row["siCd"]));
                sp.Add(new MySqlParameter("@gu_cd", row["guCd"]));
                sp.Add(new MySqlParameter("@dn_cd", row["dnCd"]));
                sp.Add(new MySqlParameter("@hide", row["hide"]?.ToString() ?? ""));
                db.ExeQry(sql, sp);
                sp.Clear();

                if (row["ekey"]?.ToString() == "1") eKey = true;
                if (row["rpt"]?.ToString() == "1") filter = "1";
            }
            db.Close();

            //등기변동에 의한 재발급 추출인 경우
            if (mdfyPrc == true)
            {
                /*
                db.Open();
                sql = "update db_tank.tx_rgst_mdfy set proc=1, pdt=curdate() where tid='" + tid + "' and pin='" + rgstIdNo.Replace("-", string.Empty) + "' and proc=0";
                db.ExeQry(sql);
                db.Close();
                */
                return;
            }

            if (filter != "1" && !eKey == false) filter = "1";

            db.Open();
            //레포트 등록
            sql = "insert into db_tank.tx_rgst set tid=@tid, dvsn=@dvsn, filter=@filter, wdt=curdate()";
            sp.Add(new MySqlParameter("@tid", tid));
            sp.Add(new MySqlParameter("@dvsn", dvsnCd));
            sp.Add(new MySqlParameter("@filter", filter));
            db.ExeQry(sql, sp);
            sp.Clear();

            //등기추출 정보기록
            rgstIdNo = rgstIdNo.Replace("-", String.Empty);
            if (rgstDvsn == "토지")
            {
                cvp = "pin_land='" + rgstIdNo + "'";
            }
            else
            {
                cvp = "pin_bldg='" + rgstIdNo + "'";
            }
            sql = "update ta_dtl set " + cvp + " where tid=" + tid;
            db.ExeQry(sql);
            db.Close();

            //기저장된 채권자(땡땡 외N) 업데이트
            if (creditor != string.Empty)
            {
                db.Open();
                sql = "select creditor from ta_list where tid=" + tid + " limit 1";
                dr = db.ExeRdr(sql);
                dr.Read();
                if (dr.HasRows) tmpCreditor = dr["creditor"].ToString();
                dr.Close();

                prsnCnt = Regex.Match(tmpCreditor, @"외[ ]*\d+").Value;
                creditor = (creditor + " " + prsnCnt).Trim();
                sql = "update ta_list set creditor='" + creditor + "' where tid=" + tid;
                db.ExeQry(sql);
                db.Close();
            }
        }

        /// <summary>
        /// 등기목적(권리)-코드
        /// </summary>
        /// <param name="prps"></param>
        /// <returns></returns>
        private string getRgCd(string prps)
        {
            string cd = "";

            foreach (DataRow row in dtRgCd.Rows)
            {
                Match m = Regex.Match(prps, row["rx"].ToString());
                if (m.Success)
                {
                    cd = row["rg_cd"].ToString();
                    break;
                }
            }

            return cd;
        }

        /// <summary>
        /// 권리자명 치환
        /// </summary>
        /// <param name="prsn"></param>
        /// <returns></returns>
        private string ReNamePrsn(string prsn)
        {
            string reName = prsn;

            if (reName.Contains("은행") && reName.Contains("주식회사")) reName = reName.Replace("주식회사", string.Empty).Trim();

            Dictionary<string, string> dict = new Dictionary<string, string>();
            dict.Add("조흥은행|동화은행", "신한은행");
            dict.Add("평화은행|한국상업은행|한빛은행|한일은행", "우리은행");
            dict.Add("대한보증보험|한국보증보험", "서울보증보험");
            dict.Add("한국주택은행|대동은행|동남은행", "국민은행");
            dict.Add("대구주택할부금융|우리주택할부금융", "우리캐피탈");
            dict.Add("서울은행|서울신탁|보람은행|한국외환은행", "하나은행");
            dict.Add("성업공사", "한국자산관리공사");
            dict.Add("sk생명|국민생명보험", "미래에셋생명보험");
            dict.Add("한미은행", "한국씨티은행");
            dict.Add("농어촌진흥공사|농업기반공사", "한국농촌공사");
            dict.Add("lg화재보험|lig손해보험", "KB손해보험");
            dict.Add("lg카드|엘지카드", "신한카드");
            dict.Add("금강고려화학", "케이씨씨");
            dict.Add("농업협동조합|농협협동조합", "농협");
            dict.Add("신용협동조합", "신협");
            dict.Add("수산업협동조합", "수협");
            dict.Add("축산업협동조합", "축협");
            dict.Add("어업협동조합", "어협");
            dict.Add("주택금융신용보증기금", "한국주택금융공사");
            dict.Add("(^제일은행)|한국스탠다드차타드은행|sc은행", "한국스탠다드차타드제일은행");
            dict.Add("동부화재해상보험주식회사", "디비손해보험주식회사");
            dict.Add(@"[\s]*주식회사[\s]*", "(주)");

            foreach (KeyValuePair<string, string> kvp in dict)
            {
                reName = Regex.Replace(reName, kvp.Key, kvp.Value);
            }

            return reName;
        }

        /// <summary>
        /// 부기등기(이전/변경/경정) 처리
        /// </summary>
        /// <param name="dr"></param>
        private void DataReplace(DataRow dr)
        {
            string sect, rank, prps, prpsOrg;

            sect = dr["sect"].ToString();
            rank = Regex.Replace(dr["rank"].ToString(), @"\-.*", string.Empty);
            prps = dr["prps"].ToString();

            foreach (DataRow row in dtB.Rows)
            {
                if (row["sect"].ToString() == sect && row["rank"].ToString() == rank)
                {
                    prpsOrg = row["prps"].ToString();
                    if (prpsOrg.Contains("근저당권") && prps.Contains("압류")) return;
                    if (prpsOrg.Contains("소유권이전청구권가등기") && prps.Contains("압류")) return;

                    if (dr["prsn"].ToString() != "")
                    {
                        row["prsn"] = dr["prsn"].ToString();
                        row["rgNo"] = dr["rgNo"].ToString();
                        row["adrs"] = dr["adrs"].ToString();
                        row["brch"] = dr["brch"].ToString();
                    }
                    if (dr["cAmt"].ToString() != "") row["cAmt"] = dr["cAmt"].ToString();
                    if (dr["bgnDt"].ToString() != "") row["bgnDt"] = dr["bgnDt"].ToString();
                    if (dr["endDt"].ToString() != "") row["endDt"] = dr["endDt"].ToString();

                    if (dr["prps"].ToString().Contains("경정") || dr["prps"].ToString().Contains("변경"))
                    {
                        DataRow corrRow = dtA.Select("sect='요약B' and rank='" + dr["rank"].ToString() + "'").FirstOrDefault();
                        if (corrRow != null)
                        {
                            row["note"] = (row["note"].ToString() + " " + corrRow["resn"].ToString().Replace("목적", string.Empty).Replace("\r\n", string.Empty)).Trim();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 임차권/전세권/지상권/지역권
        /// </summary>
        /// <param name="prps"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        private Dictionary<string, string> getLeaseSf(string prps, DataRow row)
        {
            string prpsOrg, rcpt, resn, prsnEtc, dt, bgnDt = "", endDt = "", range = "", chDt = "", srvEst = "";
            string dtPtrn = @"(\d{4})[.년\s]*(\d{1,2})[.월\s]*(\d{1,2})[.일\s]*";
            string erPtrn = @"(공동담보|공동전세|금지사항|도면\s+|도면편철|도면번호|관할등기소|부동산등기|주식회사|분할로|법률상|지분\s+|\(업무수탁|(\d+년.*부기))";
            List<string> lsNote = new List<string>();

            prps = Regex.Match(prps, @"임차권|전세권|지상권|지역권", rxOptM).Value;
            prpsOrg = row["prps"].ToString();
            rcpt = row["rcpt"].ToString();
            resn = row["resn"].ToString();
            prsnEtc = row["prsn"].ToString();

            Dictionary<string, string> dict = new Dictionary<string, string>();
            dict["prps"] = prpsOrg;
            dict["mvDt"] = "";
            dict["bzDt"] = "";
            dict["fxDt"] = "";
            dict["ctDt"] = "";
            dict["szDt"] = "";
            dict["bgnDt"] = "";
            dict["endDt"] = "";
            dict["chDt"] = "";
            dict["note"] = "";

            if (prps == "임차권")
            {
                //dict["prps"] = prpsOrg;
                MatchCollection mc = Regex.Matches(prsnEtc, @"(주민등록일자|사업자등록신청일자|확정일자|임대차계약일자|점유개시일자)[ ]*" + dtPtrn, rxOptM);
                foreach (Match match in mc)
                {
                    dt = string.Format("{0}-{1}-{2}", match.Groups[2].Value, match.Groups[3].Value.PadLeft(2, '0'), match.Groups[4].Value.PadLeft(2, '0'));
                    //if (match.Groups[1].Value == "주민등록일자") dict["mvDt"] = dt;
                    //if (match.Groups[1].Value == "사업자등록신청일자") dict["bzDt"] = dt;
                    if (match.Groups[1].Value == "주민등록일자" || match.Groups[1].Value == "사업자등록신청일자") dict["mvDt"] = dt;
                    if (match.Groups[1].Value == "확정일자") dict["fxDt"] = dt;
                    if (match.Groups[1].Value == "임대차계약일자") dict["ctDt"] = dt;
                    if (match.Groups[1].Value == "점유개시일자") dict["szDt"] = dt;
                }
                if (prsnEtc.Contains("차 임"))
                {
                    Dictionary<string, string> dicLeasAmt = getAmtParse(Regex.Match(prsnEtc, @"차 임.*", rxOptM).Value);
                    if (dicLeasAmt["amt"] != "") lsNote.Add("차임:" + dicLeasAmt["amt"] + "원");
                }
            }
            else if (prps == "전세권" || prps == "지상권")
            {
                //dict["prps"] = prpsOrg;
                prsnEtc = Regex.Replace(prsnEtc, erPtrn + @".*", string.Empty, rxOptS);
                prsnEtc = Regex.Match(prsnEtc, @"존속기간.*", rxOptS).Value;
                int idx = Regex.Match(prsnEtc, @"^[^>].*", rxOptM).Index;
                if (idx > 1) prsnEtc.Remove(idx);
                prsnEtc = Regex.Replace(prsnEtc, @"[>\r\n]", string.Empty, rxOptS);
                Match m = Regex.Match(prsnEtc, @"존속기간[ ]*" + dtPtrn + @"[부터\s]*" + dtPtrn);
                if (m.Success)
                {
                    bgnDt = string.Format("{0}-{1}-{2}", m.Groups[1].Value, m.Groups[2].Value.PadLeft(2, '0'), m.Groups[3].Value.PadLeft(2, '0'));
                    endDt = string.Format("{0}-{1}-{2}", m.Groups[4].Value, m.Groups[5].Value.PadLeft(2, '0'), m.Groups[6].Value.PadLeft(2, '0'));
                }
                m = Regex.Match(prsnEtc, @"존속기간[ ]*" + dtPtrn + @"[부터만\s]*(\d{2})년", rxOptS);
                if (m.Success)
                {
                    bgnDt = string.Format("{0}-{1}-{2}", m.Groups[1].Value, m.Groups[2].Value.PadLeft(2, '0'), m.Groups[3].Value.PadLeft(2, '0'));
                    endDt = Convert.ToDateTime(bgnDt).AddYears(Convert.ToInt32(m.Groups[4].Value)).ToShortDateString();
                    lsNote.Add(m.Groups[4].Value + "년");
                }
                else
                {
                    if (Regex.IsMatch(prsnEtc, @"설정.*(\d{2})년") && Regex.IsMatch(resn, @"\d{4}년\d+월\d+일"))
                    {
                        bgnDt = getDateParse(resn);
                        m = Regex.Match(prsnEtc, @"[부터만\s]*(\d{2})년", rxOptS);
                        if (m.Success)
                        {
                            endDt = Convert.ToDateTime(bgnDt).AddYears(Convert.ToInt32(m.Groups[1].Value)).ToShortDateString();
                            lsNote.Add(m.Groups[1].Value + "년");
                        }
                    }
                    else if (Regex.IsMatch(prsnEtc, @"접수.*(\d{2})년") && Regex.IsMatch(resn, @"\d{4}년\d+월\d+일"))
                    {
                        bgnDt = getDateParse(rcpt);
                        m = Regex.Match(prsnEtc, @"[부터만\s]*(\d{2})년", rxOptS);
                        if (m.Success)
                        {
                            endDt = Convert.ToDateTime(bgnDt).AddYears(Convert.ToInt32(m.Groups[1].Value)).ToShortDateString();
                            lsNote.Add(m.Groups[1].Value + "년");
                        }
                    }
                }
                if (bgnDt != "") dict["bgnDt"] = bgnDt;
                if (endDt != "") dict["endDt"] = endDt;

                //if (prpsOrg == "구분지상권설정")
                if (prps == "지상권")
                {
                    range = Regex.Match(row["prsn"].ToString(), @"범 위.*", rxOptS).Value;
                    range = Regex.Replace(range, @"(지상권자|존속기간).*", string.Empty, rxOptS);
                    if (range != "")
                    {
                        //dict["range"] = range;
                        lsNote.Add(range);
                    }
                }
            }
            else if (prps == "지역권")
            {
                chDt = getDateParse(Regex.Match(prsnEtc, @"\d+년\d+월\d+일\s+등기", rxOptM).Value);
                dict["chDt"] = chDt;
                srvEst = Regex.Match(prsnEtc, @"승역지\s+(.*?)(목 적|범 위)", rxOptS).Groups[1].Value.Trim();
                if (srvEst != "") lsNote.Add("승역지:" + srvEst);
                range = Regex.Match(prsnEtc, @"범 위\s+(.*?)$", rxOptM).Groups[1].Value.Trim();
                if (range != "") lsNote.Add("범위:" + range);
            }

            if (lsNote.Count > 0) dict["note"] = string.Join("\r\n", lsNote.ToArray());

            return dict;
        }

        /// <summary>
        /// 권리자/주민번호/사업자번호/법인번호/기타번호
        /// </summary>
        /// <param name="prsnEtc"></param>
        /// <returns></returns>
        private Dictionary<string, string> getPrsnRgNo(string prsnEtc)
        {
            string prsn = "", rgNo = "", rtCnt = "";
            string rgNoPtrn = @"(\d{6}-\d{7})|(\d{6}-\d{1})|(\d{6}-\*{6,})|(\d{3}-\d{2}-\d{5})|(\d{4}-\d{5})|(\d{5}-\d{5})|(^\d{6}-\d{7})";
            Dictionary<string, string> dict = new Dictionary<string, string>();
            dict["prsn"] = "";
            dict["rgNo"] = "";

            //rgNo = Regex.Match(prsnEtc, rgNoPtrn, rxOptS).Value;
            //rgNo = Regex.Replace(rgNo, @"\-\*{6,}", string.Empty);
            MatchCollection mc = Regex.Matches(prsnEtc, rgNoPtrn, rxOptS);
            if (mc.Count > 0)
            {
                rgNo = Regex.Replace(mc[0].Value, @"\-\*{6,}", string.Empty);
                if (mc.Count > 1) rtCnt = (mc.Count - 1).ToString();
            }

            prsn = Regex.Match(prsnEtc, rightPtrn + @"\s(.*)", rxOptS).Groups[2].Value.Trim();
            prsn = Regex.Replace(prsn, "(" + rgNoPtrn + ").*|>", string.Empty, rxOptS);
            prsn = Regex.Replace(prsn, adrsPtrn + ".*", string.Empty, rxOptS).Trim();
            int idx = Regex.Match(prsn, @"^[^>].*", rxOptM).Index;
            if (idx > 1) prsn.Remove(idx);
            prsn = Regex.Replace(prsn, @"국\r\n(처분청|소관청|관리청)|[\r\n]", string.Empty, rxOptS).Trim();
            prsn = Regex.Replace(prsn, @"지분[ ]*\d+분의[ ]*\d+", string.Empty, rxOptS).Trim();

            if (prsn != "")
            {
                dict["prsn"] = prsn;
                if (rtCnt != "") dict["prsn"] = string.Format("{0}외 {1}명", prsn, rtCnt);
            }
            if (rgNo != "") dict["rgNo"] = rgNo;

            return dict;
        }

        /// <summary>
        /// 주소/지점
        /// </summary>
        /// <param name="prsnEtc"></param>
        /// <returns></returns>
        private Dictionary<string, string> getAdrsBranch(string prsnEtc)
        {
            string adrs = "", branch = "";
            Dictionary<string, string> dict = new Dictionary<string, string>();
            dict["adrs"] = "";
            dict["brch"] = "";

            adrs = Regex.Match(prsnEtc, rightPtrn + @".*", rxOptS).Value;
            adrs = Regex.Replace(adrs, rightPtrn + @".*", string.Empty, rxOptM);
            adrs = Regex.Match(adrs, adrsPtrn + @".*", rxOptS).Value;
            int idx = Regex.Match(adrs, @"^[^>].*", rxOptM).NextMatch().Index;
            if (idx > 1) adrs = adrs.Remove(idx);

            Match matchBrch = Regex.Match(adrs, @"\(([\s]|소관\:)?(\w+[부점팀사단과터관])[\s]?\)", rxOptS);
            if (matchBrch.Success)
            {
                adrs = adrs.Replace(matchBrch.Value, string.Empty).Trim();
                branch = matchBrch.Groups[2].Value.Trim();
            }

            Match matchRmt = Regex.Match(adrs, @"\([\s]?소관.*", rxOptM);
            if (matchRmt.Success)
            {
                adrs = Regex.Replace(adrs, @"\([\s]?소관.*", string.Empty).Trim();
                branch = matchRmt.Value;
                branch = Regex.Replace(branch, @"\(|\)|소관|\:", string.Empty).Trim();
            }

            if (adrs != "") dict["adrs"] = Regex.Replace(adrs, @"[>\r\n]", string.Empty, rxOptS);
            if (branch != "") dict["brch"] = Regex.Replace(branch, @"[>\r\n]", string.Empty, rxOptS);

            return dict;
        }

        /// <summary>
        /// 관련 사건번호
        /// </summary>
        /// <param name="prps"></param>
        /// <param name="resn"></param>
        /// <returns></returns>
        private string getRefENo(string prps, string resn)
        {
            string ENo = "", crtSpt = "";

            resn = Regex.Replace(resn, @"[\r\n]", string.Empty, rxOptS);
            ENo = Regex.Match(resn, @"(\d+[\s]*(타경|카단|즈단|카합|카기|초기)[\s]*\d+)|(한국자산관리공사.*\d\))|(공매공고.*\d\))", rxOptS).Value;
            if (prps.Contains("가처분"))
            {
                crtSpt = Regex.Match(resn, @"\w+지방법원(\w+지원)*", rxOptM).Value;
                crtSpt = Regex.Replace(crtSpt, @"\d+년\d+월\d+일", string.Empty).Trim();
                ENo = string.Format("{0} {1}", crtSpt, ENo);
            }

            return ENo;
        }

        /// <summary>
        /// 유형별 금액
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private Dictionary<string, string> getAmtParse(string str)
        {
            string unit, natn, amtStr, amtPtrn, numStr, amt = string.Empty;

            Dictionary<string, string> dic = new Dictionary<string, string>();
            dic.Add("unit", "");    //통화단위
            dic.Add("amt", "");     //최종(환산)금액-숫자
            dic.Add("exRate", "");  //적용환율
            dic.Add("amtStr", "");  //금액문자열
            dic.Add("amtPtrn", ""); //금액유형(한글,숫자)

            //MatchCollection mc = Regex.Matches(str, @"(금|미화|법화|불화)*[ ]*(([0-9]+(,[0-9]+)*)|[일이삼사오육칠팔구십백천만억]+)[ ]*([원엔불달러]+)", rxOptM);    //원화+외화 혼합형이 존재 함
            Match m = Regex.Match(str, @"(금|미화|법화|불화|캐나다화|청구금액)*[ ]*(([0-9]+(,[0-9]+)*)|[일이삼사오육칠팔구십백천만억]+)[ ]*([원엔불달러]+)([^ㄱ-힣]|$)", rxOptM); // 주식회사 금오이엔지
            if (m.Success)
            {
                natn = m.Groups[1].Value;
                amtStr = m.Groups[2].Value.Trim();
                unit = m.Groups[5].Value;
                amtPtrn = (Regex.IsMatch(amtStr, @"[일이삼사오육칠팔구십백천만억]+", rxOptM)) ? "Kor" : "Num";

                dic["unit"] = unit;
                dic["amtStr"] = amtStr;
                dic["amtPtrn"] = amtPtrn;
                if (amtPtrn == "Kor")
                {
                    numStr = KorToNum(amtStr);
                }
                else numStr = amtStr;
                numStr = Regex.Replace(numStr, @"[, ]", string.Empty, rxOptM).Trim();
                if (!Regex.IsMatch(numStr, @"[^0-9]+", rxOptM))
                {
                    if (unit == "원") amt = numStr;
                    else if (unit == "불" || unit == "달러")
                    {
                        if (natn == "캐나다화")
                        {
                            amt = (Convert.ToDecimal(numStr) * exCAD).ToString();
                            dic["unit"] = string.Format("{0}({1})", unit, "캐나다화");
                        }
                        else amt = (Convert.ToDecimal(numStr) * exUSD).ToString();
                        dic["exRate"] = (natn == "캐나다화") ? exCAD.ToString() : exUSD.ToString();
                    }
                    else if (unit == "엔")
                    {
                        amt = ((Convert.ToDecimal(numStr) * exJPY) / 100).ToString();
                        dic["exRate"] = exJPY.ToString();
                    }
                    if (amt != string.Empty) dic["amt"] = string.Format("{0:N0}", decimal.Parse(amt));
                }
            }

            if (dic["amt"] == "")
            {
                dic["unit"] = "";
                dic["exRate"] = "";
                dic["amtStr"] = "";
                dic["amtPtrn"] = "";
            }

            return dic;
        }

        /// <summary>
        /// 한글 -> 숫자
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private string KorToNum(string input)
        {
            long result = 0;
            long tmpResult = 0;
            long num = 0;
            string number = "영일이삼사오육칠팔구";
            string unit = "십백천만억조";
            long[] unit_num = { 10, 100, 1000, 10000, (long)Math.Pow(10, 8), (long)Math.Pow(10, 12) };

            string[] arr = Regex.Split(input, @"(십|백|천|만|억|조)");    //괄호로 감싸주면 분할시 delimiters 포함한다.
            for (int i = 0; i < arr.Length; i++)
            {
                string token = arr[i];
                int check = number.IndexOf(token);
                if (check == -1)    //단위일 경우
                {
                    if ("만억조".IndexOf(token) == -1)
                    {
                        tmpResult += (num != 0 ? num : 1) * unit_num[unit.IndexOf(token)];
                    }
                    else
                    {
                        tmpResult += num;
                        result += (tmpResult != 0 ? tmpResult : 1) * unit_num[unit.IndexOf(token)];
                        tmpResult = 0;
                    }
                    num = 0;
                }
                else
                {
                    num = check;
                }
            }
            result = result + tmpResult + num;

            return result.ToString();
        }

        /// <summary>
        /// 접수번호
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private string getRcNoParse(string str)
        {
            string rcNo = string.Empty;

            Match m = Regex.Match(str, @"제(\d+)호", rxOptM);
            if (m.Success)
            {
                rcNo = m.Groups[1].Value;
            }

            return rcNo;
        }

        /// <summary>
        /// 접수일자
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private string getDateParse(string str)
        {
            string dt = string.Empty;

            Match m = Regex.Match(str, @"(\d{4})년(\d+)월(\d+)일", rxOptM);
            if (m.Success)
            {
                dt = string.Format("{0}-{1}-{2}", m.Groups[1].Value, m.Groups[2].Value.PadLeft(2, '0'), m.Groups[3].Value.PadLeft(2, '0'));
            }

            return dt;
        }
    }
}
