using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Solar
{
    public class UiUtil
    {
        //
        public UiUtil()
        {
            //
        }

        public void DgSetRead(DataGridView dg, int autoSize = 1)
        {
            dg.AllowUserToAddRows = false;
            dg.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            foreach (DataGridViewColumn col in dg.Columns)
            {
                if (col.CellType != typeof(DataGridViewCheckBoxCell))
                {
                    col.ReadOnly = true;
                }
            }
            dg.MultiSelect = false;
            dg.RowTemplate.Height = 20;
            dg.RowHeadersWidth = 25;
            if (autoSize == 1)
            {
                dg.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            }                
            dg.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            Type dgType = dg.GetType();
            PropertyInfo pi = dgType.GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.SetProperty);
            pi.SetValue(dg, true, null);
        }

        public void DgSetEdit(DataGridView dg)
        {
            dg.AllowUserToAddRows = true;
            dg.AllowUserToResizeRows = false;
            /*dg.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            foreach (DataGridViewColumn col in dg.Columns)
            {
                if (col.CellType != typeof(DataGridViewCheckBoxCell))
                {
                    col.ReadOnly = true;
                }
            }*/
            dg.MultiSelect = false;
            dg.RowTemplate.Height = 20;
            dg.RowHeadersWidth = 25;
            //dg.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            dg.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            Type dgType = dg.GetType();
            PropertyInfo pi = dgType.GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.SetProperty);
            pi.SetValue(dg, true, null);
        }

        /// <summary>
        /// 폼 리셋
        /// </summary>
        /// <param name="c"></param>
        /// <param name="exArr"></param>
        public void FormClear(Control c, string[] exArr = null)
        {
            if (exArr == null || (exArr != null && !exArr.Contains(c.Name)))
            {                
                if (c.GetType() == typeof(TextBox) || c.GetType() == typeof(MaskedTextBox) || c.GetType() == typeof(RichTextBox)) c.Text = string.Empty;
                if (c.GetType() == typeof(DataGridView))
                {
                    DataGridView dg = (DataGridView)c;
                    dg.Rows.Clear();
                }
                if (c.GetType() == typeof(ComboBox))
                {
                    ComboBox cb = (ComboBox)c;
                    if (cb.Items.Count > 0)
                    {
                        cb.SelectedIndex = 0;
                    }                    
                }
                if (c.GetType() == typeof(NumericUpDown))
                {
                    NumericUpDown nud = (NumericUpDown)c;
                    nud.Value = nud.Minimum;
                }
                if (c.GetType() == typeof(CheckBox))
                {
                    CheckBox chk = (CheckBox)c;
                    chk.Checked = false;
                }
                if (c.GetType() == typeof(DateTimePicker))
                {
                    ((DateTimePicker)c).Value = DateTime.Now;
                    ((DateTimePicker)c).Checked = false;
                }
            }
            
            if (c.HasChildren)
            {
                foreach (Control child in c.Controls)
                {
                    if (exArr != null && exArr.Contains(child.Name)) continue;
                    FormClear(child, exArr);
                }
            }
        }

        /// <summary>
        /// PageNavi-동적 버튼
        /// </summary>
        /// <param name="pan"></param>
        public void SetPagn(FlowLayoutPanel pan, int rows = 500, int min = 100, int max = 3000, int inc = 100)
        {
            pan.Height = 40;
            //pan.Padding = new Padding(3, 5, 3, 3);
            pan.Padding = new Padding((pan.Width - 425) / 2, 5, 3, 3);

            NumericUpDown nud = new NumericUpDown();
            nud.Size = new Size(50, 25);
            nud.Name = "_nudList";
            nud.Minimum = min;
            nud.Maximum = max;
            nud.Increment = inc;
            nud.Value = rows;
            nud.Margin = new Padding(3, 5, 3, 3);
            nud.TextAlign = HorizontalAlignment.Right;
            nud.BackColor = Color.White;
            pan.Controls.Add(nud);

            Button btn = new Button();
            btn.Size = new Size(60, 25);
            btn.Name = "_btnFrst";
            btn.Text = "<< 처음";
            btn.UseVisualStyleBackColor = true;
            btn.Click += new EventHandler((sender, e) => BtnPagn_Click(sender, e, pan));
            pan.Controls.Add(btn);

            btn = new Button();
            btn.Size = new Size(75, 25);
            btn.Name = "_btnPrev";
            btn.Text = "< 이전";
            btn.UseVisualStyleBackColor = true;
            btn.Click += new EventHandler((sender, e) => BtnPagn_Click(sender, e, pan));
            pan.Controls.Add(btn);

            ComboBox cbx = new ComboBox();
            cbx.Size = new Size(75, 25);
            cbx.Name = "_cbxPagn";
            cbx.DropDownStyle = ComboBoxStyle.DropDownList;
            cbx.Margin = new Padding(3, 5, 3, 3);
            //cbx.SelectedIndexChanged += _cbxPagn_SelectedIndexChanged;
            cbx.BackColor = Color.White;
            pan.Controls.Add(cbx);

            btn = new Button();
            btn.Size = new Size(75, 25);
            btn.Name = "_btnNext";
            btn.Text = "다음 >";
            btn.UseVisualStyleBackColor = true;
            btn.Click += new EventHandler((sender, e) => BtnPagn_Click(sender, e, pan));
            pan.Controls.Add(btn);

            btn = new Button();
            btn.Size = new Size(60, 25);
            btn.Name = "_btnLast";
            btn.Text = "끝 >>";
            btn.UseVisualStyleBackColor = true;
            btn.Click += new EventHandler((sender, e) => BtnPagn_Click(sender, e, pan));
            pan.Controls.Add(btn);
        }

        /// <summary>
        /// PageNavi-버튼 클릭
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <param name="flPan"></param>
        public void BtnPagn_Click(object sender, EventArgs e, FlowLayoutPanel flPan)
        {
            Button btn = (Button)sender;
            ComboBox cbx = (ComboBox)flPan.Controls["_cbxPagn"];
            if (cbx.Items.Count == 0) return;
            if (btn.Name == "_btnPrev" || btn.Name == "_btnFrst")
            {
                if (cbx.SelectedIndex == 0) return;
            }
            if (btn.Name == "_btnNext" || btn.Name == "_btnLast")
            {
                if (cbx.SelectedIndex == cbx.Items.Count - 1) return;
            }
            if (btn.Name == "_btnPrev") cbx.SelectedIndex = cbx.SelectedIndex - 1;
            if (btn.Name == "_btnNext") cbx.SelectedIndex = cbx.SelectedIndex + 1;
            if (btn.Name == "_btnFrst") cbx.SelectedIndex = 0;
            if (btn.Name == "_btnLast") cbx.SelectedIndex = cbx.Items.Count - 1;
        }

        /// <summary>
        /// PageNavi-초기화
        /// </summary>
        /// <param name="flPan"></param>
        /// <param name="totalRecord"></param>
        public void InitPagn(FlowLayoutPanel flPan, decimal totalRecord)
        {
            NumericUpDown listScale = (NumericUpDown)flPan.Controls["_nudList"];
            ComboBox cbx = (ComboBox)flPan.Controls["_cbxPagn"];
            cbx.Items.Clear();
            if (totalRecord == 0)
            {
                MessageBox.Show("검색 결과가 없습니다.");
            }
            else
            {
                decimal totalPage = Math.Ceiling(totalRecord / listScale.Value);
                cbx.Items.AddRange(Enumerable.Range(1, (int)totalPage).Cast<object>().ToArray());
                //cbx.SelectedIndex = 0;
            }
        }
    }
}
