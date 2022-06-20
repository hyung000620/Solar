using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MySql.Data.MySqlClient;

namespace Solar
{
    public partial class wfLogin : Form
    {
        Thread thread;
        DbUtil db = new DbUtil();
        NetUtil net = new NetUtil();

        public wfLogin()
        {
            InitializeComponent();

            Info();
        }

        /// <summary>
        /// Solar 버전 및 현접속 IP
        /// </summary>
        private void Info()
        {
            string ip = "", ver = "";
            ip = net.GetHtml(Properties.Settings.Default.myWeb + "SOLAR/ip.php").Trim();
            lblConnIP.Text += ip;

            try
            {
                //ClickOnce의 버젼 취득
                ver = System.Deployment.Application.ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString();
            }
            catch (System.Deployment.Application.DeploymentException ex)
            {
                //ClickOnce배포가 아니므로 어셈블리버젼을 취득
                string ex_msg = ex.Message;
                ver = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
            catch
            {
                ver = "x";
            }
            lblVersion.Text += ver;
            
            if (Properties.Settings.Default.SAV_ID == true)
            {
                chkSaveID.Checked = true;
                txtUsrId.Text = Properties.Settings.Default.USR_ID;
                txtPassWd.Select();
            }

            this.Text = "SOLAR - " + string.Format("{0:yyyy.MM.dd(ddd)}", DateTime.Now);
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            string sql, usrId, passWd, connIp;

            usrId = txtUsrId.Text.Trim();
            passWd = txtPassWd.Text.Trim();
            connIp = Regex.Match(lblConnIP.Text, @"\d+.\d+.\d+.\d+").Value;

            if (usrId == string.Empty || passWd == string.Empty)
            {
                MessageBox.Show("아이디와 비밀번호를 입력하세요.");
                return;
            }

            sql = "select idx from db_tank.tz_staff where id='" + usrId + "' and passwd=sha2('" + passWd + "',256) limit 1";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            dr.Read();
            if (!dr.HasRows)
            {
                MessageBox.Show("관리자 정보가 일치하지 않습니다.");
                dr.Close();
                db.Close();
                return;
            }
            dr.Close();

            if (usrId != "solar")
            {
                sql = $"insert into db_tank.tm_conn set id='{usrId}', ip='{connIp}', conn_dtm=now(), conn_dvsn=2";
                db.ExeQry(sql);

                sql = $"update db_tank.tz_staff set ip='{connIp}', conn_dtm=now() where id='{usrId}'";
                db.ExeQry(sql);
            }
            
            db.Close();
            
            Properties.Settings.Default.USR_ID = usrId.ToLower();
            Properties.Settings.Default.SAV_ID = (chkSaveID.Checked) ? true : false;
            Properties.Settings.Default.Save();
                        
            thread = new Thread(openMdiForm);
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            this.Close();
        }

        private void openMdiForm()
        {
            try
            {
                wfMdiPrnt prnt = new wfMdiPrnt();
                Application.Run(prnt);
            }
            catch { }
        }
    }
}
