using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Solar
{
    public class RgstPinTid
    {
        DbUtil db = new DbUtil();
        string cnvTool = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\pdftohtml.exe";

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;

        public RgstPinTid()
        {
            if (!File.Exists(cnvTool))
            {
                File.WriteAllBytes(cnvTool, Properties.Resources.pdftohtml);
            }
        }

        public Dictionary<string, string> Proc(string pdfFile)
        {
            string htmlFile, html, rgstIdNo, rgstDvsn, tid, sql;
            
            Dictionary<string,string> dic=new Dictionary<string,string>();
            dic.Add("result", "fail");
            dic.Add("tid", "");
            dic.Add("dvsn", "");
            dic.Add("dvsnCd", "");
            dic.Add("pin", "");

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
                if (proc.StandardError.ReadToEnd() != string.Empty)
                {
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
            rgstIdNo = Regex.Match(html, @"고유번호 (\d{4}-\d{4}-\d{6})", rxOptM).Groups[1].Value.Replace("-", string.Empty);
            if (rgstDvsn != string.Empty && rgstIdNo != string.Empty)
            {
                sql = "select M.tid from db_tank.tx_rgst_mdfy M, db_main.ta_list L, db_main.ta_dtl D where M.tid=L.tid and L.tid=D.tid and enable=1 and (sta1 in (11,13) or sta2=1011) and proc=0 and (M.pin=pin_land or M.pin=pin_bldg) and M.pin='" + rgstIdNo + "' limit 1";
                db.Open();
                MySqlDataReader dr = db.ExeRdr(sql);
                dr.Read();
                if (dr.HasRows)
                {
                    dic["result"] = "success";
                    dic["tid"] = dr["tid"].ToString();
                    dic["dvsn"] = rgstDvsn;
                    dic["dvsnCd"] = (rgstDvsn == "토지") ? "DA" : "DB";
                    dic["pin"] = rgstIdNo;
                }
                dr.Close();
                db.Close();
            }

            return dic;
        }
    }
}
