using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Media;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Solar
{
    public partial class wfMdiPrnt : Form
    {
        DbUtil db = new DbUtil();
        MenuStrip ms = new MenuStrip();
        ToolStripMenuItem ts = null;
        //decimal staff_level = fmCert.StaffLevel;
        string myId = Properties.Settings.Default.USR_ID;

        DataTable dtStaff;

        Timer notiTimer = new Timer();

        public wfMdiPrnt()
        {
            InitializeComponent();
            MenuInit();

            NotiTimer_Tick(null, null);

            notiTimer.Interval = 30000;  //갱신 30초
            notiTimer.Tick += NotiTimer_Tick;
            notiTimer.Start();
        }

        private void NotiTimer_Tick(object sender, EventArgs e)
        {
            string sql, senderNm = "", title = "";
            bool notiFlag = false;
            int i = 0, unReadCnt = 0;

            sql = $"select * from db_tank.tz_note where rid='{myId}' and rcnt=0 and rdel=0 order by idx";
            db.Open();
            MySqlDataReader dr = db.ExeRdr(sql);
            while (dr.Read())
            {
                unReadCnt++;
                if (dr["noti"].ToString() == "0") notiFlag = true;
                if (i == 0)
                {
                    senderNm = dtStaff.Rows.Cast<DataRow>().Where(t => t["id"].ToString() == dr["sid"].ToString()).FirstOrDefault()["name"].ToString();
                    title = dr["title"].ToString();
                } 
            }
            dr.Close();            
            db.ExeQry($"update db_tank.tz_note set noti=1 where rid='{myId}' and rcnt=0 and noti=0");
            db.Close();

            if (notiFlag)
            {
                popNoti.Size = new Size(250, 150);
                //popNoti.AnimationDuration = 5000;
                popNoti.Image = Properties.Resources.speaker_16_red.ToBitmap();
                //popNoti.ImageSize = new Size(16, 16);
                popNoti.ImagePadding = new Padding(5);
                popNoti.TitleText = $"     {DateTime.Now:yyyy.MM.dd(ddd) HH:mm:ss}";
                popNoti.TitleColor = Color.Blue;
                popNoti.TitlePadding = new Padding(5);                
                popNoti.ContentPadding = new Padding(5);
                popNoti.ContentText = $"보낸 사람 : [{senderNm}] 님\r\n\r\n{title}";                
                popNoti.BodyColor = Color.LightYellow;
                popNoti.ContentColor = Color.Gray;
                popNoti.Popup();
                SystemSounds.Beep.Play();
            }

            if (unReadCnt > 0)
            {                
                ms.Items["Mgmt.wfNote"].Text = $" {unReadCnt}";
                ms.Items["Mgmt.wfNote"].Image = Properties.Resources.msg_16_blue.ToBitmap();
                //ms.Items["Mgmt.wfNote"].ForeColor = Color.White;
                //ms.Items["Mgmt.wfNote"].BackColor = Color.RoyalBlue;
            }
            else
            {
                ms.Items["Mgmt.wfNote"].Text = $"";
                ms.Items["Mgmt.wfNote"].Image = Properties.Resources.msg_16_black.ToBitmap();
                //ms.Items["Mgmt.wfNote"].ForeColor = Color.Black;
                //ms.Items["Mgmt.wfNote"].BackColor = SystemColors.Control;
            }                        
        }

        private void MenuInit()
        {
            ToolStripMenuItem[] tsi = null;

            ToolStripSeparator sp = new ToolStripSeparator();
            ts = new ToolStripMenuItem("경매정보");
            tsi = new ToolStripMenuItem[]
            {
                new ToolStripMenuItem("물건 관리", null, ShowForm, "CA.wfCaMgmt"),
                new ToolStripMenuItem("등기 추출", null, ShowForm, "CA.wfRgst"),
                new ToolStripMenuItem("일괄 유찰/낙찰/변경", null, ShowForm, "CA.wfBidRslt"),
                new ToolStripMenuItem("비교결과 레포트", null, ShowForm, "CA.wfRpt"),
                new ToolStripMenuItem("물건수 비교", null, ShowForm, "CA.wfPdCnt"),
                new ToolStripMenuItem("일정 관리", null, ShowForm, "CA.wfSkd"),
                new ToolStripMenuItem("등기 관리", null, ShowForm, "CA.wfRgstMgmt"),
                new ToolStripMenuItem("파일 관리", null, ShowForm, "CA.wfFileMgmt"),
                new ToolStripMenuItem("토지-용도지역지구", null, ShowForm, "CA.wfLandPlan"),
                new ToolStripMenuItem("세대 열람", null, ShowForm, "CA.wfLeasTaein"),
                new ToolStripMenuItem("임시 업로드", null, ShowForm, "CA.wfUpload"),
                new ToolStripMenuItem("매각물건명세서 변동", null, ShowForm, "CA.wfDpslStmtCmp"),
                new ToolStripMenuItem("신건수집-수동", null, ShowForm, "CA.wfNtManual"),
                new ToolStripMenuItem("건축물대장 발급", null, ShowForm, "CA.wfBldgRgst"),
                new ToolStripMenuItem("등기 변동사건", null, ShowForm, "CA.wfRgstMdfy"),
                new ToolStripMenuItem("등기 일괄추출", null, ShowForm, "CA.wfRgstAnaly")
            };
            //ts.DropDownItems.AddRange(tsi);
            foreach (ToolStripMenuItem item in tsi)
            {
                if (item.Text == "[자료]-사진작업") ts.DropDownItems.Add(new ToolStripSeparator());
                ts.DropDownItems.Add(item);
            }
            
            ms.Items.Add(ts);
            ts = new ToolStripMenuItem("공매정보");
            tsi = new ToolStripMenuItem[]
            {
                new ToolStripMenuItem("물건 관리", null, ShowForm, "PA.wfPaMgmt"),
                new ToolStripMenuItem("세대 열람", null, ShowForm, "PA.wfLeasTk"),
                new ToolStripMenuItem("신탁 공매", null, ShowForm, "PA.wfTrust")
            };
            ts.DropDownItems.AddRange(tsi);
            ms.Items.Add(ts);

            ts = new ToolStripMenuItem("동산정보");
            tsi = new ToolStripMenuItem[]
            {
                new ToolStripMenuItem("물건 관리", null, ShowForm, "CA.wfChatl")
            };
            ts.DropDownItems.AddRange(tsi);
            ms.Items.Add(ts);

            ts = new ToolStripMenuItem("공용데이터");
            tsi = new ToolStripMenuItem[]
            {
                new ToolStripMenuItem("집합건물 관리",null,ShowForm,"Comn.wfMultiBldg"),
                new ToolStripMenuItem("정보광장",null,ShowForm,"Comn.wfPolicyNews"),
                new ToolStripMenuItem("도시철도",null,ShowForm,"Comn.wfRailRoad")
            };
            ts.DropDownItems.AddRange(tsi);
            ms.Items.Add(ts);

            ts = new ToolStripMenuItem("기타 작업");
            tsi = new ToolStripMenuItem[]
            {
                //new ToolStripMenuItem("경/공매 코드처리", null, ShowForm, "Etc.wfCourtCd"),
                //new ToolStripMenuItem("경매사건 원본(메뉴얼용)", null, ShowForm, "Etc.wfAuctSrc"),
                new ToolStripMenuItem("자동차 코드관리", null, ShowForm, "Etc.wfCarCd"),
                new ToolStripMenuItem("문자-경매알림", null, ShowForm, "Etc.wfAuctSms"),
                //new ToolStripMenuItem("[수집]-공인중개사", null, ShowForm, "Etc.wfReAgent"),
                //new ToolStripMenuItem("[MG]-GD_MIG", null, ShowForm, "Etc.wfGdMig"),
                //new ToolStripMenuItem("[MG]-TK_MIG", null, ShowForm, "Etc.wfTkMig"),
                //new ToolStripMenuItem("감평테스트", null, ShowForm, "CA.wfApslTest"),
                new ToolStripMenuItem("전국-공시지가변동율",null,ShowForm,"Etc.wfLpRate")                
            };
            ts.DropDownItems.AddRange(tsi);
            ms.Items.Add(ts);

            ts = new ToolStripMenuItem("자동화");
            tsi = new ToolStripMenuItem[]
            {
                new ToolStripMenuItem("필수 작업", null, ShowForm, "Auto.wfAtomLog"),
                new ToolStripMenuItem("등기 발급", null, ShowForm, "Auto.wfRgstAuto")
            };
            ts.DropDownItems.AddRange(tsi);
            ms.Items.Add(ts);

            //사내 쪽지
            ts = new ToolStripMenuItem("", Properties.Resources.msg_16_black.ToBitmap(), ShowForm, "Mgmt.wfNote");
            //ts.Alignment=ToolStripItemAlignment.Right;
            ts.Alignment = ToolStripItemAlignment.Left;
            ts.Margin = new Padding(50, 0, 0, 0);
            ms.Items.Add(ts);

            if (myId == "solar")
            {
                ts = new ToolStripMenuItem("SOLAR");
                ts.BackColor = Color.LightGreen;
                tsi = new ToolStripMenuItem[]
                {
                    new ToolStripMenuItem("[사내]-직원관리", null, ShowForm, "Mgmt.wfStaff")
                };
                ts.DropDownItems.AddRange(tsi);
                ms.Items.Add(ts);
            }

            if (myId == "f22")
            {
                ts = new ToolStripMenuItem("HANS");
                ts.BackColor = Color.Purple;
                ts.ForeColor = Color.White;
                ts.Anchor = AnchorStyles.Right;
                ts.Margin = new Padding(50, 0, 0, 0);
                tsi = new ToolStripMenuItem[]
                {
                    new ToolStripMenuItem("데이터 추가/갱신", null, ShowForm, "Hans.wfDataUdt"),
                    new ToolStripMenuItem("[사내]-직원관리", null, ShowForm, "Mgmt.wfStaff")
                };
                ts.DropDownItems.AddRange(tsi);
                ms.Items.Add(ts);
            }
            if (myId == "dksms10")
            {
                ts = new ToolStripMenuItem("LEE");
                ts.BackColor = Color.LightGreen;
                tsi = new ToolStripMenuItem[]
                {
                    new ToolStripMenuItem("[사내]-직원관리", null, ShowForm, "Mgmt.wfStaff")
                };
                ts.DropDownItems.AddRange(tsi);
                ms.Items.Add(ts);
            }
            this.MainMenuStrip = ms;
            this.Controls.Add(ms);

            dtStaff = db.ExeDt("select id, name, team from db_tank.tz_staff where team > 0");
        }

        private void ShowForm(object sender, EventArgs e)
        {
            bool ActState = false;
            ToolStripMenuItem item = (ToolStripMenuItem)sender;

            Form[] Mdiform = ActiveForm.MdiChildren;
            Form OpenedForm = null;
            foreach (Form ActForm in Mdiform)
            {
                if (ActForm.GetType().ToString() == "Solar." + item.Name)
                {
                    OpenedForm = ActForm;
                    ActState = true;
                    break;
                }
            }

            if (ActState == true)
            {
                OpenedForm.Activate();
            }
            else
            {
                ObjectHandle handle = Activator.CreateInstance(null, "Solar." + item.Name);
                Form fm = (Form)handle.Unwrap();
                fm.MdiParent = this;
                //fm.StartPosition = FormStartPosition.CenterScreen;
                fm.WindowState = FormWindowState.Maximized;
                fm.Show();
            }
        }

        private void wfMdiPrnt_Load(object sender, EventArgs e)
        {

        }
    }
}
