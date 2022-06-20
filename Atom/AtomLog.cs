using MySql.Data.MySqlClient;
using Solar;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Atom
{
    public class AtomLog
    {
        DbUtil db = new DbUtil();
        public readonly string prcNo;
        public readonly string vmNm;
        public readonly string vmIp;

        /// <summary>
        /// 로그 생성자
        /// </summary>
        /// <param name="prcCd">처리 종류 코드</param>
        public AtomLog(int prcCd = 0)
        {
            string prcId, sql;

            prcNo = DateTime.Now.ToString("yyMMddHHmmss");
            prcId = Process.GetCurrentProcess().Id.ToString();
            prcNo += prcId.PadLeft(5, '0');

            vmNm = Environment.MachineName;
            try
            {
                IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
                foreach (IPAddress ip in ipHost.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        vmIp = ip.ToString();
                        break;
                    }
                }
                if (vmIp.Contains("192.168") || vmIp.Contains("172.21"))
                {
                    vmIp = new WebClient().DownloadString(Properties.Settings.Default.myWeb + "SOLAR/ip.php").Trim();
                }
            }
            catch
            {
                vmIp = "0.0.0.0";
            }
            //vmIp = "0.0.0.0";
            List<MySqlParameter> sp = new List<MySqlParameter>();

            db.Open();
            sql = "insert into db_tank.tx_atom set prc_no=@prc_no, prc_cd=@prc_cd, vm_nm=@vm_nm, ip=INET_ATON('" + vmIp + "'), bgn_dtm=now(), note=@note";
            sp.Add(new MySqlParameter("@prc_no", prcNo));
            sp.Add(new MySqlParameter("@prc_cd", prcCd));
            sp.Add(new MySqlParameter("@vm_nm", vmNm));
            sp.Add(new MySqlParameter("@note", string.Format(@"{0:HH:mm:ss} {1}", DateTime.Now, "실행 시작")));
            db.ExeQry(sql, sp);
            sp.Clear();
            db.Close();
        }

        /// <summary>
        /// 로그 추가
        /// </summary>
        /// <param name="note">내용</param>
        /// <param name="statCd">처리코드(1:실행완료)</param>
        public void AddLog(string note, int statCd = 0)
        {
            string sql, cvp;

            List<MySqlParameter> sp = new List<MySqlParameter>();

            cvp = @"note=concat(note,'\r\n','" + string.Format(@"{0:HH:mm:ss} {1}", DateTime.Now, note) + "')";
            if (statCd == 1) cvp += ", end_dtm=now()";

            db.Open();
            sql = "update db_tank.tx_atom set " + cvp + " where prc_no=@prc_no";
            sp.Add(new MySqlParameter("@prc_no", prcNo));
            db.ExeQry(sql, sp);
            sp.Clear();
            db.Close();
        }
    }
}
