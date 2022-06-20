using Atom.CA;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Atom
{
    internal static class Program
    {
        /// <summary>
        /// 해당 애플리케이션의 주 진입점입니다.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // 관리자권한으로 재실행
            if (!IsAdministrator())
            {
                try
                {
                    var pi = new ProcessStartInfo();
                    pi.UseShellExecute = true;
                    pi.FileName = Application.ExecutablePath;
                    pi.WorkingDirectory = Environment.CurrentDirectory;
                    pi.Verb = "runas";
                    Process.Start(pi);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                return;
            }
            //
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length == 0)
            {
                //Application.Run(new CA.fPreNoti());
                //Application.Run(new CA.fCaNoti());
                //Application.Run(new CA.fAnmt());
                //Application.Run(new CA.fState());
                //Application.Run(new CA.fDoc());
                //Application.Run(new CA.fSkd());
                //Application.Run(new CA.fMerg());
                //Application.Run(new CA.fApslMinAmt());
                //Application.Run(new CA.fLPlanPrice());
                //Application.Run(new CA.fSucbAfter());
                //Application.Run(new CA.fBidTm());
                //Application.Run(new CA.fFbChk());
                //Application.Run(new CA.fRgstErr());
                //Application.Run(new CA.fSkdReady());      //경매-예정물건 매각준비상태 체크

                //Application.Run(new PA.fPaNoti());
                //Application.Run(new PA.fPrptLs());

                //Application.Run(new CA.fChatl());

                //Application.Run(new Etc.fmAsSync());

                //Application.Run(new fDeploy());

                //Application.Run(new CA.fBldgDoc());
                //Application.Run(new PA.fRgst());
                //Application.Run(new Etc.fmCarAdrs());

                //Application.Run(new CA.fDpslStmtCmp());                

                //cStateSMS auctState = new CA.cStateSMS();
                //auctState.sendSMS(null);

                //Application.Run(new PA.fPaOld());                
                //Application.Run(new PA.fTList());
                //Application.Run(new CA.fRgstMdfy());

                //cTblClean tblClean = new cTblClean();
                //tblClean.Proc();

                Application.Run(new Comn.fRgstAuto());

                return;
            }

            switch (args[0])
            {
                case "경매-예정수집":
                    Application.Run(new CA.fSkd());
                    break;

                case "경매-신건수집":
                    Application.Run(new CA.fCaNoti());
                    break;

                case "경매-선행공고":
                    Application.Run(new CA.fPreNoti());
                    break;

                case "경매-문서수집":
                    Application.Run(new CA.fDoc());
                    break;

                case "경매-감정최저":
                    Application.Run(new CA.fApslMinAmt());
                    break;

                case "경매-물건상태":
                    Application.Run(new CA.fState());
                    break;

                case "경매-공고수집":
                    Application.Run(new CA.fAnmt());
                    break;

                case "경매-중복병합":
                    Application.Run(new CA.fMerg());
                    break;

                case "경매-매각처리":
                    Application.Run(new CA.fSucbAfter());
                    break;

                case "경매-매각일시":
                    Application.Run(new CA.fBidTm());
                    break;

                case "경매-유찰확인":
                    Application.Run(new CA.fFbChk());
                    break;

                case "경매-토공역세":
                    Application.Run(new CA.fLPlanPrice());
                    break;

                /*case "경매-등기누락":
                    Application.Run(new CA.fRgstErr());
                    break;*/

                case "경매-매각준비":
                    Application.Run(new CA.fSkdReady());
                    break;

                case "경매-등기변동":
                    Application.Run(new CA.fRgstMdfy());
                    break;

                case "공매-일괄처리":
                    Application.Run(new PA.fPaNoti());
                    break;

                case "공매-재산명세":
                    Application.Run(new PA.fPrptLs());
                    break;

                /*case "공매-등기수집":
                    Application.Run(new PA.fRgst());
                    break;*/

                case "공매-신탁공매":
                    Application.Run(new PA.fTList());
                    break;

                case "동산-물건수집":
                    Application.Run(new CA.fChatl());
                    break;

                case "문자-상태변경":
                    cStateSMS auctState = new CA.cStateSMS();
                    auctState.sendSMS(null);
                    break;

                case "문자-경매개시":
                    cNearBySMS auctNear = new CA.cNearBySMS();
                    auctNear.sendSMS(null);
                    break;

                case "공통-등기발급":
                    Application.Run(new Comn.fRgstAuto());
                    break;

                case "기타-DB정리":
                    cTblClean tblClean = new cTblClean();
                    tblClean.Proc();
                    break;

                case "기타-GD":           //임시(tid-pid, analy, special)
                    Application.Run(new Etc.fmAsSync());
                    break;

                default:
                    Application.Run(new fDeploy());
                    break;
            }
        }

        private static bool IsAdministrator()
        {
            var wi = WindowsIdentity.GetCurrent();
            if (wi == null) return false;

            var wp = new WindowsPrincipal(wi);
            return wp.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
