using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Solar.CA
{
    public partial class sfStateCopy : Form
    {
        public wfCaMgmt caMgmt;

        public sfStateCopy()
        {
            InitializeComponent();
            this.Shown += SfStateCopy_Shown;            
        }

        private void SfStateCopy_Shown(object sender, EventArgs e)
        {
            caMgmt = (wfCaMgmt)this.Owner;
            rdo_dgL.Checked = false;
            rdo_dgB.Checked = false;
            rdo_dgE.Checked = false;
            rdo_dgT.Checked = false;
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            int srcIdx = 0, bgnIdx = 0, endIdx = 0, i = 0, srcNo = 0, bgnNo = 0, endNo = 0, dgvRowCnt = 0;
            string colNm;

            DataGridView dgv;

            if (rdo_dgL.Checked) dgv = caMgmt.dgL;
            else if (rdo_dgB.Checked) dgv = caMgmt.dgB;
            else if (rdo_dgE.Checked) dgv = caMgmt.dgE;
            else if (rdo_dgT.Checked) dgv = caMgmt.dgT;
            else
            {
                MessageBox.Show("현황 구분을 선택 해 주세요.");
                return;
            }
            if (txtSrcNo.Text.Trim() == string.Empty)
            {
                MessageBox.Show("원본No를 입력 해 주세요.");
                return;
            }
            if (txtTgtNoS.Text.Trim() == string.Empty || txtTgtNoE.Text.Trim() == string.Empty)
            {
                MessageBox.Show("대상No를 입력 해 주세요.");
                return;
            }

            srcNo = Convert.ToInt32(txtSrcNo.Text.Trim());
            bgnNo = Convert.ToInt32(txtTgtNoS.Text.Trim());
            endNo = Convert.ToInt32(txtTgtNoE.Text.Trim());

            srcIdx = srcNo - 1;
            bgnIdx = bgnNo - 1;
            endIdx = endNo - 1;
            DataGridViewRow srcRow = dgv.Rows[srcIdx];

            List<string> exList = new List<string>(new string[] 
            { 
                "_No",
                "_LsNo",
                "_UnitPrc",
                "_Amt",
                "_PrpsNm",
                "_Prsn",
                "_ShopNm",
                "_Idx"
            });

            dgvRowCnt = dgv.Rows.Count - 1;
            for (i = bgnIdx; i <= endIdx; i++)
            {
                if (i == dgvRowCnt) break;
                foreach (DataGridViewColumn col in dgv.Columns)
                {
                    colNm = Regex.Replace(col.Name, @"dg[LBET]", string.Empty);
                    if (exList.Contains(colNm)) continue;
                    dgv[col.Index, i].Value = srcRow.Cells[col.Index].Value;
                }   
            }
        }
    }
}
