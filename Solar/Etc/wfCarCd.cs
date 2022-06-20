using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

namespace Solar.Etc
{
    public partial class wfCarCd : Form
    {
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();
        AuctCd auctCd = new AuctCd();
        ApiUtil api = new ApiUtil();
        UiUtil ui = new UiUtil();

        DataTable dtCo, dtMo;
        BackgroundWorker bgwork;
        int setCnt = 5, setSleep = 1500, webCnt = 0;

        private delegate void deleGridUpdate(Dictionary<string, string> dict);

        public wfCarCd()
        {
            InitializeComponent();

            ui.DgSetRead(dgCo);
            ui.DgSetRead(dgMo, 0);

            dtCo = db.ExeDt("select co_cd from ta_cd_carco");
            dtMo = db.ExeDt("select co_cd, mo_cd, mo_nm from ta_cd_carmo");
        }

        /// <summary>
        /// 차량 제조사
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCmpy_Click(object sender, EventArgs e)
        {
            int i = 0, newCnt = 0;
            string url, sql, jsonData, coCd, coNm;

            dgCo.Rows.Clear();
            txtNewCmpyCnt.Text = "0";

            url = "https://www.kcar.com/search/api/getIntegratedList.do?car_kind=국산차&v_car_type=KOR";
            jsonData = net.GetHtml(url, Encoding.UTF8);
            dynamic x = JsonConvert.DeserializeObject(jsonData);
            JArray jaCmpy = x["result"]["makeList"];

            db.Open();
            foreach (JObject item in jaCmpy)
            {
                i++;
                coCd = item["v_makecd"].ToString();
                coNm = item["v_makenm"].ToString();
                dgCo.Rows.Add(i, coNm, coCd, "1");
                Application.DoEvents();
                if (dtCo.Select("co_cd='" + coCd + "'").Count() > 0) continue;

                newCnt++;
                txtNewCmpyCnt.Text = newCnt.ToString();
                sql = "insert ignore into ta_cd_carco set co_cd='" + coCd + "', co_nm='" + coNm + "', dmst=1";
                db.ExeQry(sql);                
            }

            url = "https://www.kcar.com/search/api/getIntegratedList.do?car_kind=수입차&v_car_type=IMP";
            jsonData = net.GetHtml(url, Encoding.UTF8);
            x = JsonConvert.DeserializeObject(jsonData);
            jaCmpy = x["result"]["makeList"];

            foreach (JObject item in jaCmpy)
            {
                i++;
                coCd = item["v_makecd"].ToString();
                coNm = item["v_makenm"].ToString();
                dgCo.Rows.Add(i, coNm, coCd, string.Empty);
                Application.DoEvents();
                if (dtCo.Select("co_cd='" + coCd + "'").Count() > 0) continue;

                newCnt++;
                txtNewCmpyCnt.Text = newCnt.ToString();
                sql = "insert ignore into ta_cd_carco set co_cd='" + coCd + "', co_nm='" + coNm + "', dmst=0";
                db.ExeQry(sql);
            }
            db.Close();

            MessageBox.Show("제조사 완료");
            dgCo.ClearSelection();
        }

        /// <summary>
        /// 차량 모델그룹
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnModel_Click(object sender, EventArgs e)
        {
            dgMo.Rows.Clear();
            txtNewModelCnt.Text = "0";

            if (dgCo.Rows.Count == 0)
            {
                btnCmpy_Click(null, null);
            }

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
            int i = 0, newCnt = 0, rowIdx;
            string carKind, carType, url, sql, jsonData, coCd, coNm, moCd, moNm;
            decimal curCnt = 0;
                        
            deleGridUpdate dgUpdt = new deleGridUpdate(dgvUpdate);

            foreach (DataGridViewRow row in dgCo.Rows)
            {
                curCnt++;
                webCnt++;
                if (webCnt % setCnt == 0) Thread.Sleep(setSleep);

                row.DefaultCellStyle.BackColor = Color.LightGreen;

                coCd = row.Cells["dgCo_Cd"].Value.ToString();
                coNm = row.Cells["dgCo_Nm"].Value.ToString();
                if (row.Cells["dgCo_Dmst"].Value.ToString() == "1")
                {
                    carKind = "국산차";
                    carType = "KOR";
                }
                else
                {
                    carKind = "수입차";
                    carType = "IMP";
                }
                url = "https://www.kcar.com/search/api/getIntegratedList.do?car_kind=" + carKind + "&v_car_type=" + carType + "&v_makecd=" + coCd;

                jsonData = net.GetHtml(url, Encoding.UTF8);
                dynamic x = JsonConvert.DeserializeObject(jsonData);
                JArray jaModel = x["result"]["modelGrpList"];
                if (jaModel.Count == 0) continue;
                db.Open();
                foreach (JObject item in jaModel)
                {
                    i++;
                    moCd = item["v_model_grp_cd"].ToString();
                    moNm = item["v_model_grp_nm"].ToString();
                    /*
                    rowIdx = dgMo.Rows.Add(i, coNm, coCd, moNm, moCd);
                    if (dgMo.Rows[rowIdx].Displayed == false)
                    {
                        dgMo.FirstDisplayedScrollingRowIndex = rowIdx;
                    }
                    */
                    Dictionary<string, string> dict = new Dictionary<string, string>();
                    dict["No"] = i.ToString();
                    dict["coNm"] = coNm;
                    dict["coCd"] = coCd;
                    dict["moNm"] = moNm;
                    dict["moCd"] = moCd;
                    dgMo.Invoke(dgUpdt, dict);

                    //if (dtMo.Select("co_cd='" + coCd + "' and mo_cd='" + moCd + "'").Count() > 0) continue;
                    if (dtMo.Select("co_cd='" + coCd + "' and mo_nm='" + moNm + "'").Count() > 0) continue;

                    newCnt++;
                    txtNewModelCnt.Text = newCnt.ToString();
                    //dgMo.Rows[rowIdx].DefaultCellStyle.BackColor = Color.LightPink;
                    sql = "insert ignore into ta_cd_carmo set co_cd='" + coCd + "', mo_cd='" + moCd + "', mo_nm='" + moNm + "', rx='" + moNm + "', wdt=curdate()";
                    db.ExeQry(sql);                    
                }
                db.Close();
            }
        }

        private void dgvUpdate(Dictionary<string, string> dict)
        {
            int i = dgMo.Rows.Add();
            dgMo["dgMo_No", i].Value = dict["No"];
            dgMo["dgMo_CoNm", i].Value = dict["coNm"];
            dgMo["dgMo_CoCd", i].Value = dict["coCd"];
            dgMo["dgMo_MoNm", i].Value = dict["moNm"];
            dgMo["dgMo_MoCd", i].Value = dict["moCd"];

            if (!dgMo.Rows[i].Displayed) dgMo.FirstDisplayedScrollingRowIndex = i;
        }

        private void Bgwork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {            
            MessageBox.Show("모델그룹 완료");
            dgMo.ClearSelection();
        }
    }
}
