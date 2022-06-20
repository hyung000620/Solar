using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Solar
{
    public class SpCdtnChk
    {
        DbUtil db = new DbUtil();

        public SpCdtnChk()
        { 
            
        }

        public void RgstLeas(string tid)
        {
            string spCdtn = "", sql;
            string landErDt = "", bldgErDt = "", rgCd, dpslCd = "0";
            DateTime erDt, mvDt;
            decimal useCd, biz, deposit;

            //bool 선순위임차권 = false;
            bool 선순위전세권 = false, 선순위가등기 = false, 선순위가처분 = false, 선순위임차권설정 = false, 임차권등기 = false, 대항력 = false;
            
            DataTable dtL = new DataTable();
            DataTable dtB = new DataTable();
            List<string> list = new List<string>();

            sql = $"select dpsl_dvsn from ta_list where tid='{tid}' limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            dpslCd=dr["dpsl_dvsn"].ToString();
            dr.Close();
            db.Close();

            sql = "select rg_dvsn, ekey, date_format(rc_dt,'%Y-%m-%d') as rcDt, rg_cd from ta_rgst where tid=" + tid + " order by rc_dt,rc_no,rank,rank_s";
            DataTable dt = db.ExeDt(sql);
            if (dt.Rows.Count == 0)
            {
                return;
            }

            //토지등기
            DataRow[] dataRowsL = dt.Select("rg_dvsn=1");
            if (dataRowsL.Count() > 0)
            {
                dtL = dataRowsL.CopyToDataTable();
                /*DataRow eRow = dtL.Select("ekey=1").FirstOrDefault();
                if (eRow != null)
                {
                    landErDt = Convert.ToDateTime(eRow["rc_dt"]);
                }*/
                foreach (DataRow row in dtL.Rows)
                {
                    rgCd = row["rg_cd"].ToString();
                    if (row["ekey"].ToString() == "1")
                    {
                        landErDt = row["rcDt"].ToString();
                    }
                    if (landErDt == "")
                    {
                        if (rgCd == "7" || rgCd == "8" || rgCd == "30" || rgCd == "45") 선순위전세권 = true;
                        else if (rgCd == "11" || rgCd == "12" || rgCd == "22" || rgCd == "25") 선순위가등기 = true;
                        else if (rgCd == "13" || rgCd == "14") 선순위가처분 = true;
                        else if (rgCd == "27") 선순위임차권설정 = true;
                    }
                    if (rgCd == "9" || rgCd == "28") 임차권등기 = true;
                }
            }

            //2022-03-31 추가 지현/민영
            if (선순위가등기 || 선순위가처분)
            {
                if (dpslCd == "17" || dpslCd == "22")   //건물만매각, 건물만 매각이며 지분매각 인 경우는 제외
                {
                    선순위가등기 = false;
                    선순위가처분 = false;
                }
            }

            //건물등기
            DataRow[] dataRowsB = dt.Select("rg_dvsn in (2,3)");
            if (dataRowsB.Count() > 0)
            {
                dtB = dataRowsB.CopyToDataTable();
                foreach (DataRow row in dtB.Rows)
                {
                    rgCd = row["rg_cd"].ToString();
                    if (row["ekey"].ToString() == "1")
                    {
                        bldgErDt = row["rcDt"].ToString();
                    }
                    if (bldgErDt == "")
                    {
                        if (rgCd == "7" || rgCd == "8" || rgCd == "30" || rgCd == "45") 선순위전세권 = true;
                        else if (rgCd == "11" || rgCd == "12" || rgCd == "22" || rgCd == "25") 선순위가등기 = true;
                        else if (rgCd == "13" || rgCd == "14") 선순위가처분 = true;
                        else if (rgCd == "27") 선순위임차권설정 = true;
                    }
                    if (rgCd == "9" || rgCd == "28") 임차권등기 = true;
                }
            }

            //2022-03-31 추가 지현/민영
            if (선순위가등기 || 선순위가처분)
            {
                if (dpslCd == "13" || dpslCd == "16" || dpslCd == "20")   //토지만매각, 토지만 매각이며 지분매각, 토지만매각 지분매각(건물x) 인 경우는 제외
                {
                    선순위가등기 = false;
                    선순위가처분 = false;
                }
            }

            if (선순위가등기) list.Add("14");
            if (선순위가처분) list.Add("15");
            if (선순위전세권) list.Add("16");
            if (선순위임차권설정) list.Add("17");
            if (임차권등기) list.Add("18");
            //if (선순위임차권) list.Add("19");

            if (list.Count > 0)
            {
                spCdtn = string.Join(",", list.ToArray());
            }

            if (bldgErDt == "" && landErDt == "")
            {
                DbProc(tid, spCdtn);
                return;
            }

            erDt = (bldgErDt != "") ? Convert.ToDateTime(bldgErDt) : Convert.ToDateTime(landErDt);

            if (erDt <= Convert.ToDateTime("1900-01-01"))
            {
                DbProc(tid, spCdtn);
                return;
            }

            sql = "select use_cd, date_format(mv_dt,'%Y-%m-%d') as mvDt, deposit, biz from ta_leas where tid=" + tid + " and mv_dt between '1900-01-01' and '" + erDt + "' and use_cd not in (6,7,10,12) order by ls_no";
            dt = db.ExeDt(sql);
            if (dt.Rows.Count == 0)
            {
                DbProc(tid, spCdtn);
                return;
            }

            foreach (DataRow row in dt.Rows)
            {
                useCd = Convert.ToDecimal(row["use_cd"]);
                biz = Convert.ToDecimal(row["biz"]);
                deposit = Convert.ToDecimal(row["deposit"]);
                mvDt = Convert.ToDateTime(row["mvDt"]);
                if (erDt > mvDt)
                {
                    if (useCd == 1 || useCd == 4)
                    {
                        대항력 = true;
                    }
                    else
                    {
                        if (biz == 1 && erDt >= Convert.ToDateTime("2002-11-01") && (useCd == 2 || useCd == 3 || useCd == 5 || useCd == 8 || useCd == 9 || useCd == 11))
                        {
                            대항력 = true;
                        }
                        else
                        {
                            if (biz != 1)
                            {
                                대항력 = true;
                            }
                        }
                    }

                    if (대항력 == true) break;
                }
            }

            if (대항력)
            {
                list.Add("19");
                spCdtn = string.Join(",", list.ToArray());
            }

            DbProc(tid, spCdtn);
        }

        public void DbProc(string tid, string spCdtn)
        {
            int cd;
            string sql, oldCdtn, newCdtn;
            
            List<string> list = new List<string>();

            sql = "select sp_cdtn from ta_list where tid=" + tid + " limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            if (dr.HasRows == false)
            {
                dr.Close();
                db.Close();
                return;
            }

            oldCdtn = dr["sp_cdtn"].ToString();
            dr.Close();
            db.Close();

            string[] newArr = spCdtn.Split(new char[] { ',' });
            if (oldCdtn == string.Empty) newCdtn = spCdtn;
            else
            {
                string[] oldArr = oldCdtn.Split(new char[] { ',' });
                foreach (string str in oldArr)
                {
                    cd = Convert.ToInt32(str);
                    if (cd < 14 || cd > 20)
                    {
                        list.Add(str);
                    }
                }
            }
            foreach (string str in newArr)
            {
                list.Add(str);
            }

            newCdtn = string.Join(",", list.ToArray());
            //Console.WriteLine(newCdtn);
            sql = "update ta_list set sp_cdtn='" + newCdtn + "' where tid=" + tid;
            db.Open();
            db.ExeQry(sql);
            db.Close();
        }
    }
}
