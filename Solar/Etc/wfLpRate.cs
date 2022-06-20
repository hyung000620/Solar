using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Solar.Etc
{
    public partial class wfLpRate : Form
    {
        DbUtil db = new DbUtil();
        UiUtil ui = new UiUtil();
        NetUtil net = new NetUtil();
        AuctCd auctCd = new AuctCd();
        ApiUtil api = new ApiUtil();

        public wfLpRate()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 연단위 전국/시도/시구군 지가변동률-1
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCsvFind_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = false;
            ofd.Filter = "CSV 파일(*.csv)|*.csv";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                txtCsvFile.Text = ofd.FileName;
            }
        }

        /// <summary>
        /// 연단위 전국/시도/시구군 지가변동률-2
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCsvProc_Click(object sender, EventArgs e)
        {
            //source (지가변동률 > 연도별 지역별 - Excel 편집 - csv)
            //http://www.r-one.co.kr/rone/resis/statistics/statisticsViewer.do?menuId=LFR_12200#

            //전국,,,1.964,2.402,2.7
            //서울,,,2.662,2.688,2.974
            //서울,종로구,,2.41,1.903,2.199
            //세종,,,4.527,4.572,4.775
            //경기,,,1.236,1.733,2.231
            //경기,성남시,중원구,0.744,1.212,2.399
            //경기,의정부시,,1.158,1.162,1.839
            //경기,안양시,만안구,1.265,1.895,2.509
            //경기,부천시,,1.516,1.899,1.304
            //제주,,,3.728,7.568,8.332
            //제주,제주시,,3.27,7.317,8.05

            string SQL = string.Empty, SQL_H = string.Empty, column = string.Empty, line_str = string.Empty, val = string.Empty, record = string.Empty, bulk_data = string.Empty;
            string siNm = "", guNm = "", guNm2 = "";
            StringBuilder sb = null;
            ArrayList al = new ArrayList();
            int i = 0, dataLen = 0;

            if (txtCsvFile.Text == string.Empty)
            {
                MessageBox.Show("업로드 할 파일을 선택 해 주세요.");
                return;
            }

            column = "si_nm,gu_nm,base_year,rate,wdt";
            SQL_H = "INSERT INTO ta_lp_rate (" + column + ") VALUES ";

            Stream stream = File.OpenRead(txtCsvFile.Text);
            StreamReader sr = new StreamReader(stream, Encoding.Default);
            while (sr.Peek() >= 0)
            {
                //i++;
                line_str = sr.ReadLine();
                if (line_str.Trim() == string.Empty) continue;

                sb = new StringBuilder();
                string[] dataArr = line_str.Split(',');
                dataLen = dataArr.Length;
                if (dataArr[0] != string.Empty)
                {
                    siNm = dataArr[0];
                    guNm = string.Empty;
                }
                if (dataArr[1] != string.Empty) guNm = dataArr[1];
                guNm2 = (dataArr[2] != string.Empty) ? $"{guNm} {dataArr[2]}" : guNm;

                for (i = 3; i < dataLen; i++)
                {
                    if (i > 3) sb.Append(",\r\n");
                    sb.Append("(");
                    sb.Append("'" + siNm + "',");
                    sb.Append("'" + guNm2 + "',");
                    sb.Append("'" + (i + Convert.ToInt16(txtCsvYear.Text) - 3).ToString() + "',");    //수집 시작연도
                    sb.Append("'" + dataArr[i] + "',");
                    sb.Append("CURDATE()");
                    sb.Append(")");
                }
                record = sb.ToString();

                al.Add(record);

                bulk_data = String.Join(",", (string[])al.ToArray(Type.GetType("System.String")));
                //MessageBox.Show(bulk_data);                

                SQL = SQL_H + bulk_data;
                db.Open();
                db.ExeQry(SQL);
                db.Close();

                al.Clear();
                bulk_data = string.Empty;
            }

            sr.Close();
            sr.Dispose();
            stream.Close();
            stream.Dispose();

            //return;
                        
            SQL = "SELECT si_nm, gu_nm, si_cd, gu_cd FROM tx_cd_adrs WHERE hide=0 GROUP BY si_cd, gu_cd";
            DataTable dt = db.ExeDt(SQL);

            db.Open();
            foreach (DataRow row in dt.Rows)
            {
                SQL = "UPDATE ta_lp_rate SET si_cd=" + row["si_cd"].ToString() + ", gu_cd=" + row["gu_cd"].ToString() + " WHERE si_nm='" + row["si_nm"].ToString() + "' AND gu_nm='" + row["gu_nm"].ToString() + "'";
                db.ExeQry(SQL);
            }
            db.Close();

            MessageBox.Show("ok");
        }
    }
}
