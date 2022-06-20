using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Solar
{
    public class AuctCd
    {
        DataTable dtLaw;

        public AuctCd()
        {
            dtLaw = new DataTable();
            dtLaw.Columns.Add("lawNm");            
            dtLaw.Columns.Add("lawCd");
            dtLaw.Columns.Add("csNm");
            dtLaw.Columns.Add("csCd");
            dtLaw.Columns.Add("_gdCd");

            dtLaw.Rows.Add("서울중앙지방법원", "000210", "서울-중앙", "1010", "1010");
            dtLaw.Rows.Add("서울동부지방법원", "000211", "서울-동부", "1110", "1110");
            dtLaw.Rows.Add("서울서부지방법원", "000215", "서울-서부", "1210", "1210");
            dtLaw.Rows.Add("서울남부지방법원", "000212", "서울-남부", "1310", "1310");
            dtLaw.Rows.Add("서울북부지방법원", "000213", "서울-북부", "1410", "1410");

            dtLaw.Rows.Add("의정부지방법원", "000214", "의정부-본원", "1510", "1510");
            dtLaw.Rows.Add("고양지원", "214807", "의정부-고양", "1511", "1511");
            dtLaw.Rows.Add("남양주지원", "214804", "의정부-남양주", "1512", "1512");   //2022-03-08 추가

            dtLaw.Rows.Add("인천지방법원", "000240", "인천-본원", "1610", "1610");
            dtLaw.Rows.Add("부천지원", "000241", "인천-부천", "1611", "1611");

            dtLaw.Rows.Add("수원지방법원", "000250", "수원-본원", "1710", "1710");
            dtLaw.Rows.Add("성남지원", "000251", "수원-성남", "1711", "1711");
            dtLaw.Rows.Add("여주지원", "000252", "수원-여주", "1712", "1712");
            dtLaw.Rows.Add("평택지원", "000253", "수원-평택", "1713", "1713");
            dtLaw.Rows.Add("안산지원", "250826", "수원-안산", "1714", "1714");
            dtLaw.Rows.Add("안양지원", "000331", "수원-안양", "1715", "1715");
            
            dtLaw.Rows.Add("춘천지방법원", "000260", "춘천-본원", "1810", "1810");
            dtLaw.Rows.Add("강릉지원", "000261", "춘천-강릉", "1811", "1811");
            dtLaw.Rows.Add("원주지원", "000262", "춘천-원주", "1812", "1812");
            dtLaw.Rows.Add("속초지원", "000263", "춘천-속초", "1813", "1813");
            dtLaw.Rows.Add("영월지원", "000264", "춘천-영월", "1814", "1814");
            
            dtLaw.Rows.Add("청주지방법원", "000270", "청주-본원", "1910", "2610");
            dtLaw.Rows.Add("충주지원", "000271", "청주-충주", "1911", "2611");
            dtLaw.Rows.Add("제천지원", "000272", "청주-제천", "1912", "2612");
            dtLaw.Rows.Add("영동지원", "000273", "청주-영동", "1913", "2613");
            
            dtLaw.Rows.Add("대전지방법원", "000280", "대전-본원", "2010", "1910");
            dtLaw.Rows.Add("홍성지원", "000281", "대전-홍성", "2011", "1911");
            dtLaw.Rows.Add("논산지원", "000282", "대전-논산", "2012", "1912");
            dtLaw.Rows.Add("천안지원", "000283", "대전-천안", "2013", "1913");
            dtLaw.Rows.Add("공주지원", "000284", "대전-공주", "2014", "1914");
            dtLaw.Rows.Add("서산지원", "000285", "대전-서산", "2015", "1915");
            
            dtLaw.Rows.Add("대구지방법원", "000310", "대구-본원", "2110", "2010");
            dtLaw.Rows.Add("대구서부지원", "000320", "대구-서부", "2111", "2018");
            dtLaw.Rows.Add("안동지원", "000311", "대구-안동", "2112", "2011");
            dtLaw.Rows.Add("경주지원", "000312", "대구-경주", "2113", "2012");
            dtLaw.Rows.Add("김천지원", "000313", "대구-김천", "2114", "2013");
            dtLaw.Rows.Add("상주지원", "000314", "대구-상주", "2115", "2014");
            dtLaw.Rows.Add("의성지원", "000315", "대구-의성", "2116", "2015");
            dtLaw.Rows.Add("영덕지원", "000316", "대구-영덕", "2117", "2016");
            dtLaw.Rows.Add("포항지원", "000317", "대구-포항", "2118", "2017");
            
            dtLaw.Rows.Add("부산지방법원", "000410", "부산-본원", "2210", "2110");
            dtLaw.Rows.Add("부산동부지원", "000412", "부산-동부", "2211", "2111");
            dtLaw.Rows.Add("부산서부지원", "000414", "부산-서부", "2212", "2112");
            
            dtLaw.Rows.Add("울산지방법원", "000411", "울산-본원", "2310", "2210");
            
            dtLaw.Rows.Add("창원지방법원", "000420", "창원-본원", "2410", "2310");
            dtLaw.Rows.Add("마산지원", "000431", "창원-마산", "2411", "2315");
            dtLaw.Rows.Add("진주지원", "000421", "창원-진주", "2412", "2311");
            dtLaw.Rows.Add("통영지원", "000422", "창원-통영", "2413", "2312");
            dtLaw.Rows.Add("밀양지원", "000423", "창원-밀양", "2414", "2313");
            dtLaw.Rows.Add("거창지원", "000424", "창원-거창", "2415", "2314");
            
            dtLaw.Rows.Add("광주지방법원", "000510", "광주-본원", "2510", "2410");
            dtLaw.Rows.Add("목포지원", "000511", "광주-목포", "2511", "2411");
            dtLaw.Rows.Add("장흥지원", "000512", "광주-장흥", "2512", "2412");
            dtLaw.Rows.Add("순천지원", "000513", "광주-순천", "2513", "2413");
            dtLaw.Rows.Add("해남지원", "000514", "광주-해남", "2514", "2414");
            
            dtLaw.Rows.Add("전주지방법원", "000520", "전주-본원", "2610", "2510");
            dtLaw.Rows.Add("군산지원", "000521", "전주-군산", "2611", "2511");
            dtLaw.Rows.Add("정읍지원", "000522", "전주-정읍", "2612", "2512");
            dtLaw.Rows.Add("남원지원", "000523", "전주-남원", "2613", "2513");
            
            dtLaw.Rows.Add("제주지방법원", "000530", "제주-본원", "2710", "2710");
        }

        /// <summary>
        /// 전체 법원정보(법원명, 법원코드, ls코드)
        /// </summary>
        /// <returns>전체법원 DataTable</returns>
        public DataTable DtLawInfo()
        {
            return dtLaw;
        }

        /// <summary>
        /// 법원 그룹별
        /// </summary>
        /// <returns></returns>
        public DataTable DtCsGrp()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("lawNm");
            dt.Columns.Add("lawCd");
            dt.Columns.Add("csNm");
            dt.Columns.Add("csCd");

            dt.Rows.Add("서울지법", "", "서울지법", "1010,1110,1210,1310,1410");
            dt.Rows.Add("서울중앙지방법원", "000210", "서울-중앙", "1010");
            dt.Rows.Add("서울동부지방법원", "000211", "서울-동부", "1110");
            dt.Rows.Add("서울서부지방법원", "000215", "서울-서부", "1210");
            dt.Rows.Add("서울남부지방법원", "000212", "서울-남부", "1310");
            dt.Rows.Add("서울북부지방법원", "000213", "서울-북부", "1410");

            dt.Rows.Add("의정부지법", "", "의정부지법", "1510,1511");
            dt.Rows.Add("의정부지방법원", "000214", "의정부-본원", "1510");
            dt.Rows.Add("고양지원", "214807", "의정부-고양", "1511");

            dt.Rows.Add("인천지법", "", "인천지법", "1610,1611");
            dt.Rows.Add("인천지방법원", "000240", "인천-본원", "1610");
            dt.Rows.Add("부천지원", "000241", "인천-부천", "1611");

            dt.Rows.Add("수원지법", "", "수원지법", "1710,1711,1712,1713,1714,1715");
            dt.Rows.Add("수원지방법원", "000250", "수원-본원", "1710");
            dt.Rows.Add("성남지원", "000251", "수원-성남", "1711");
            dt.Rows.Add("여주지원", "000252", "수원-여주", "1712");
            dt.Rows.Add("평택지원", "000253", "수원-평택", "1713");
            dt.Rows.Add("안산지원", "250826", "수원-안산", "1714");
            dt.Rows.Add("안양지원", "000331", "수원-안양", "1715");

            dt.Rows.Add("춘천지법", "", "춘천지법", "1810,1811,1812,1813,1814");
            dt.Rows.Add("춘천지방법원", "000260", "춘천-본원", "1810");
            dt.Rows.Add("강릉지원", "000261", "춘천-강릉", "1811");
            dt.Rows.Add("원주지원", "000262", "춘천-원주", "1812");
            dt.Rows.Add("속초지원", "000263", "춘천-속초", "1813");
            dt.Rows.Add("영월지원", "000264", "춘천-영월", "1814");

            dt.Rows.Add("청주지법", "", "청주지법", "1910,1911,1912,1913");
            dt.Rows.Add("청주지방법원", "000270", "청주-본원", "1910");
            dt.Rows.Add("충주지원", "000271", "청주-충주", "1911");
            dt.Rows.Add("제천지원", "000272", "청주-제천", "1912");
            dt.Rows.Add("영동지원", "000273", "청주-영동", "1913");

            dt.Rows.Add("대전지법", "", "대전지법", "2010,2011,2012,2013,2014,2015");
            dt.Rows.Add("대전지방법원", "000280", "대전-본원", "2010");
            dt.Rows.Add("홍성지원", "000281", "대전-홍성", "2011");
            dt.Rows.Add("논산지원", "000282", "대전-논산", "2012");
            dt.Rows.Add("천안지원", "000283", "대전-천안", "2013");
            dt.Rows.Add("공주지원", "000284", "대전-공주", "2014");
            dt.Rows.Add("서산지원", "000285", "대전-서산", "2015");

            dt.Rows.Add("대구지법", "", "대구지법", "2110,2111,2112,2113,2114,2115,2116,2117,2118");
            dt.Rows.Add("대구지방법원", "000310", "대구-본원", "2110");
            dt.Rows.Add("대구서부지원", "000320", "대구-서부", "2111");
            dt.Rows.Add("안동지원", "000311", "대구-안동", "2112");
            dt.Rows.Add("경주지원", "000312", "대구-경주", "2113");
            dt.Rows.Add("김천지원", "000313", "대구-김천", "2114");
            dt.Rows.Add("상주지원", "000314", "대구-상주", "2115");
            dt.Rows.Add("의성지원", "000315", "대구-의성", "2116");
            dt.Rows.Add("영덕지원", "000316", "대구-영덕", "2117");
            dt.Rows.Add("포항지원", "000317", "대구-포항", "2118");

            dt.Rows.Add("부산지법", "", "부산지법", "2210,2211,2212");
            dt.Rows.Add("부산지방법원", "000410", "부산-본원", "2210");
            dt.Rows.Add("부산동부지원", "000412", "부산-동부", "2211");
            dt.Rows.Add("부산서부지원", "000414", "부산-서부", "2212");

            dt.Rows.Add("울산지법", "", "울산지법", "2310");
            dt.Rows.Add("울산지방법원", "000411", "울산-본원", "2310");

            dt.Rows.Add("창원지법", "", "창원지법", "2410,2411,2412,2413,2414,2415");
            dt.Rows.Add("창원지방법원", "000420", "창원-본원", "2410");
            dt.Rows.Add("마산지원", "000431", "창원-마산", "2411");
            dt.Rows.Add("진주지원", "000421", "창원-진주", "2412");
            dt.Rows.Add("통영지원", "000422", "창원-통영", "2413");
            dt.Rows.Add("밀양지원", "000423", "창원-밀양", "2414");
            dt.Rows.Add("거창지원", "000424", "창원-거창", "2415");

            dt.Rows.Add("광주지법", "", "광주지법", "2510,2511,2512,2513,2514");
            dt.Rows.Add("광주지방법원", "000510", "광주-본원", "2510");
            dt.Rows.Add("목포지원", "000511", "광주-목포", "2511");
            dt.Rows.Add("장흥지원", "000512", "광주-장흥", "2512");
            dt.Rows.Add("순천지원", "000513", "광주-순천", "2513");
            dt.Rows.Add("해남지원", "000514", "광주-해남", "2514");

            dt.Rows.Add("전주지법", "", "전주지법", "2610,2611,2612,2613");
            dt.Rows.Add("전주지방법원", "000520", "전주-본원", "2610");
            dt.Rows.Add("군산지원", "000521", "전주-군산", "2611");
            dt.Rows.Add("정읍지원", "000522", "전주-정읍", "2612");
            dt.Rows.Add("남원지원", "000523", "전주-남원", "2613");

            dt.Rows.Add("제주지법", "", "제주지법", "2710");
            dt.Rows.Add("제주지방법원", "000530", "제주-본원", "2710");

            return dt;
        }

        /// <summary>
        /// 법원명 반환(법원 크롤링)
        /// </summary>
        /// <param name="csCd">cs코드</param>
        /// <param name="enc">인코딩 여부</param>
        /// <returns>법원명</returns>
        public string FindLawNm(string csCd, bool enc = false)
        {
            string lawNm = string.Empty;

            lawNm = dtLaw.Select("csCd='" + csCd + "'")[0]["lawNm"].ToString();
            if (enc) lawNm = System.Web.HttpUtility.UrlEncode(lawNm, Encoding.Default);

            return lawNm;
        }

        /// <summary>
        /// 법원-지원명 반환
        /// </summary>
        /// <param name="csCd"></param>
        /// <returns></returns>
        public string FindCsNm(string csCd)
        {
            string csNm = string.Empty;

            if (dtLaw.Select("csCd='" + csCd + "'").Count() > 0)
            {
                csNm = dtLaw.Select("csCd='" + csCd + "'")[0]["csNm"].ToString();
            }

            return csNm;
        }

        /// <summary>
        /// 법원명 인코딩
        /// </summary>
        /// <param name="lawNm">법원명</param>
        /// <returns>법원명Enc</returns>
        public string LawNmEnc(object lawNm = null, string csCd = null)
        {
            if (lawNm == null)
            {
                if (string.IsNullOrEmpty(csCd))
                {
                    lawNm = string.Empty;
                }
                else
                {
                    lawNm = dtLaw.Select("csCd='" + csCd + "'")[0]["lawNm"];
                }
            }

            return System.Web.HttpUtility.UrlEncode(lawNm.ToString(), Encoding.Default);
        }

        //집합 건물 카테고리(cat3)
        public decimal[] multiBldgArr = new decimal[] { 201013, 201014, 201015, 201017, 201019, 201020, 201022, 201111, 201123, 201130, 201216 };
    }
}
