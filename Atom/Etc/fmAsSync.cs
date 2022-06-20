using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Solar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Atom.Etc
{
    public partial class fmAsSync : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        //AtomLog atomLog = new AtomLog(108);
        AuctCd auctCd = new AuctCd();
        ApiUtil api = new ApiUtil();

        BackgroundWorker bgwork;
        int setCnt = 10, setSleep = 1500, webCnt = 0;

        RegexOptions rxOptM = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        RegexOptions rxOptS = RegexOptions.Singleline | RegexOptions.IgnoreCase;

        public fmAsSync()
        {
            InitializeComponent();
            this.Shown += FmAsSync_Shown;
        }

        private void FmAsSync_Shown(object sender, EventArgs e)
        {
            CheckForIllegalCrossThreadCalls = false;
            bgwork = new BackgroundWorker();
            bgwork.DoWork += Bgwork_DoWork;
            bgwork.RunWorkerCompleted += Bgwork_RunWorkerCompleted;
            bgwork.WorkerReportsProgress = false;
            bgwork.WorkerSupportsCancellation = true;

            bgwork.RunWorkerAsync();
        }

        private void Bgwork_DoWork(object sender, DoWorkEventArgs e)
        {
            int i = 0;
            string sql, url, jsData, cvp, idx, gdLawCd = "0000";
            string law, spt, tid, pid;

            List<string> lst = new List<string>();

            sql = "select tid,spt,sn1,sn2,pn from ta_list where _pid=0 and 2nd_dt > '0000-00-00' order by tid";
            DataTable dt = db.ExeDt(sql);

            foreach (DataRow row in dt.Rows)
            {
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                sql = "select _gd_cd from ta_cd_cs where spt_cd='" + row["spt"].ToString() + "' limit 1";
                db.Open();
                MySqlDataReader dr = db.ExeRdr(sql);
                bool gdSpt = dr.HasRows;
                dr.Read();
                gdLawCd = dr["_gd_cd"].ToString();
                dr.Close();
                db.Close();
                if (!gdSpt) continue;

                tid = row["tid"].ToString();
                law = gdLawCd.Substring(0, 2);
                spt = gdLawCd.Substring(2, 2);
                url = string.Format("https://intra.auction1.co.kr/partner/tk/getPid.php?law={0}&spt={1}&sn1={2}&sn2={3}&pn={4}", law, spt, row["sn1"], row["sn2"], row["pn"]);
                pid = net.GetHtml(url, Encoding.UTF8).Trim();
                if (pid != string.Empty)
                {
                    i++;
                    db.Open();
                    sql = "update ta_list set _pid=" + pid + " where tid=" + tid;
                    db.ExeQry(sql);
                    db.Close();

                    txtState.AppendText(string.Format("\r\n {0}) T:{1} / P:{2}", i, tid, pid));
                }
            }

            url = string.Format("https://intra.auction1.co.kr/partner/tk/asSync.php");
            jsData = net.GetHtml(url);
            dynamic x = JsonConvert.DeserializeObject(jsData);

            var itemsD = x["itemsD"];
            if (itemsD != null && itemsD.Count > 0)
            {
                JArray jsArr = JArray.Parse(itemsD.ToString());
                foreach (JObject item in jsArr)
                {
                    idx = item["idx"].ToString();
                    sql = "delete from ta_analysis where idx=" + idx;
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();

                    i++;
                    txtState.AppendText(string.Format("\r\n D-{0}", i));
                }
            }

            i = 0;
            var itemsA = x["itemsA"];
            if (itemsA != null && itemsA.Count > 0)
            {
                JArray jsArr = JArray.Parse(itemsA.ToString());
                foreach (JObject item in jsArr)
                {
                    cvp = string.Empty;
                    lst.Clear();
                    foreach (JProperty prop in item.Properties())
                    {
                        lst.Add(string.Format("{0}='{1}'", prop.Name, prop.Value));
                    }
                    cvp = String.Join(",", lst.ToArray());

                    sql = "insert into ta_analysis set " + cvp + " ON DUPLICATE KEY update " + cvp;
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();

                    i++;
                    txtState.AppendText(string.Format("\r\n A-{0}", i));
                }
            }

            i = 0;
            var itemsS = x["itemsS"];
            if (itemsS != null && itemsS.Count > 0)
            {
                JArray jsArr = JArray.Parse(itemsS.ToString());
                foreach (JObject item in jsArr)
                {
                    cvp = string.Empty;
                    lst.Clear();
                    foreach (JProperty prop in item.Properties())
                    {
                        lst.Add(string.Format("{0}='{1}'", prop.Name, prop.Value));
                    }
                    cvp = String.Join(",", lst.ToArray());

                    sql = "insert into ta_prod_special set " + cvp + " ON DUPLICATE KEY update " + cvp;
                    db.Open();
                    db.ExeQry(sql);
                    db.Close();

                    i++;
                    txtState.AppendText(string.Format("\r\n S-{0}", i));
                }
            }

            //MessageBox.Show("완료");
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            bgwork.Dispose();

            this.Dispose();
            this.Close();
        }
    }
}
