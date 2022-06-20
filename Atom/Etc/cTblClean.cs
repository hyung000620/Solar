using Solar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using System.Data;

namespace Atom
{
    class cTblClean
    {
        DbUtil db = new DbUtil();
        AtomLog atomLog = new AtomLog(900);     //로그 생성

        public void Proc()
        {
            string sql;
            DataTable dt;

            //경매처리 레포트
            sql = "select R.idx from db_tank.tx_rpt R, db_main.ta_list L where R.tid=L.tid and (L.sta1=14 or L.sta2=1219) and R.wdt < date_sub(curdate(), interval 1 month)";
            dt = db.ExeDt(sql);
            ProcSub(dt, "tx_rpt");
            
            //등기변동 비교-오류 -> 매주 금요일날 테이블 비우기로 개선?
            //sql = "select C.idx from db_tank.tx_rgst_mdfy M, db_tank.tx_rgst_cmp C where M.tid=C.tid and pdt > '0000-00-00' and pdt < date_sub(curdate(), interval 7 day) group by C.idx";
            //dt = db.ExeDt(sql);
            //ProcSub(dt, "tx_rgst_cmp");

            //선행공고 제시외 갱신 대상
            sql = "select E.idx from db_tank.tx_oth_err E, db_main.ta_list L where E.tid=L.tid and L.2nd_dt < curdate()";
            dt = db.ExeDt(sql);
            ProcSub(dt, "tx_oth_err");

            //물건삭제 내역
            sql = "delete from db_tank.tx_del_things where wdt < date_sub(curdate(), interval 3 month)";
            ProcSub(sql, "tx_del_things");

            //관심물건 이동 내역
            sql = "delete from db_tank.tx_inter_things where dtm < date_sub(curdate(), interval 3 month)";
            ProcSub(sql, "tx_inter_things");

            //일괄변경 복구용
            sql = "delete from db_tank.tx_mdfy where wdt < date_sub(curdate(), interval 3 day)";
            ProcSub(sql, "tx_mdfy");

            //예정물건(매각준비상태) 등기처리-GD
            sql = "delete from db_tank.tx_ready where rgst=1";
            ProcSub(sql, "tx_ready");

            //등기추출 내역
            sql = "delete from db_tank.tx_rgst where wdt < date_sub(curdate(), interval 7 day)";
            ProcSub(sql, "tx_rgst");

            //회차정보 오류-매물명세
            sql = "delete from db_tank.tx_seq_err where wdt < date_sub(curdate(), interval 1 month)";
            ProcSub(sql, "tx_seq_err");

            //등기파일 누락-GD
            sql = "delete from db_tank.tx_rgst_err where proc=1 and wdt < date_sub(curdate(), interval 7 day)";
            ProcSub(sql, "tx_rgst_err");

            //Atom 실행 로그
            sql = "delete from db_tank.tx_atom where bgn_dtm < date_sub(curdate(), interval 3 month)";
            ProcSub(sql, "tx_atom");

            //Crontab 실행 내역
            sql = "delete from db_tank.tx_cron where wdtm < date_sub(curdate(), interval 1 month)";
            ProcSub(sql, "tx_cron");

            //Console.ReadLine();
            atomLog.AddLog("실행 완료", 1);
        }

        public void ProcSub(object obj, string tbl)
        {
            int i = 0;
            string sql;
            DataTable dt;

            db.Open();
            if (obj.GetType() == typeof(DataTable))
            {
                dt = obj as DataTable;
                foreach (DataRow row in dt.Rows)
                {
                    i++;
                    sql = $"delete from db_tank.{tbl} where idx={row["idx"]}";
                    //Console.WriteLine(sql);
                    db.ExeQry(sql);
                    //break;
                    if (i % 100 == 0)
                    { 
                        db.Close();
                        db.Open();
                    }
                }
            }
            else
            {
                sql = obj as string;
                //Console.WriteLine(sql);
                db.ExeQry(sql);
            }
            db.ExeQry($"optimize table db_tank.{tbl}");
            db.Close();
        }
    }
}
