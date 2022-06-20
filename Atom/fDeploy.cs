using Solar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Atom
{
    public partial class fDeploy : Form
    {
        UiUtil ui = new UiUtil();

        public fDeploy()
        {
            InitializeComponent();

            ui.DgSetRead(dg);
        }

        private void btnDeploy_Click(object sender, EventArgs e)
        {
            decimal i = 0, n = 0;
            string vmNm, filePath, rmtPath, fileNm;

            dg.Rows.Clear();

            filePath = txtFilePath.Text;
            FileInfo fi = new FileInfo(filePath);
            fileNm = fi.Name;

            for (i = nudVmBgn.Value; i <= nudVmEnd.Value; i++)
            {
                n++;
                vmNm = string.Format("VM-{0}", i);

                try
                {
                    rmtPath = string.Format(@"\\{0}\Atom\{1}", vmNm, fileNm);
                    fi.CopyTo(rmtPath, true);
                    dg.Rows.Add(n, vmNm, "OK");
                }
                catch (Exception ex)
                {
                    dg.Rows.Add(n, vmNm, ex.Message);
                }
            }

            MessageBox.Show("배포 완료!");
        }
    }
}
