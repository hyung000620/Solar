
namespace Solar.PA
{
    partial class wfLeasTk
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(wfLeasTk));
            this.dgF = new System.Windows.Forms.DataGridView();
            this.F_Chk = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.F_No = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.F_Src = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.F_CltrNo = new System.Windows.Forms.DataGridViewLinkColumn();
            this.F_Local = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.F_Remote = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.F_S1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.F_Msg = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnOpenFiles = new System.Windows.Forms.Button();
            this.btnStart = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tbcF = new System.Windows.Forms.TabControl();
            this.tabWbr = new System.Windows.Forms.TabPage();
            this.wbr1 = new System.Windows.Forms.WebBrowser();
            this.tabPdf = new System.Windows.Forms.TabPage();
            this.axAcroPDF1 = new AxAcroPDFLib.AxAcroPDF();
            ((System.ComponentModel.ISupportInitialize)(this.dgF)).BeginInit();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tbcF.SuspendLayout();
            this.tabWbr.SuspendLayout();
            this.tabPdf.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.axAcroPDF1)).BeginInit();
            this.SuspendLayout();
            // 
            // dgF
            // 
            this.dgF.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgF.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.F_Chk,
            this.F_No,
            this.F_Src,
            this.F_CltrNo,
            this.F_Local,
            this.F_Remote,
            this.F_S1,
            this.F_Msg});
            this.dgF.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgF.Location = new System.Drawing.Point(0, 46);
            this.dgF.Name = "dgF";
            this.dgF.ReadOnly = true;
            this.dgF.RowTemplate.Height = 23;
            this.dgF.Size = new System.Drawing.Size(872, 965);
            this.dgF.TabIndex = 5;
            this.dgF.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgF_CellClick);
            // 
            // F_Chk
            // 
            this.F_Chk.FalseValue = "F";
            this.F_Chk.HeaderText = "선택";
            this.F_Chk.Name = "F_Chk";
            this.F_Chk.ReadOnly = true;
            this.F_Chk.TrueValue = "T";
            this.F_Chk.Width = 50;
            // 
            // F_No
            // 
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.F_No.DefaultCellStyle = dataGridViewCellStyle1;
            this.F_No.HeaderText = "No";
            this.F_No.Name = "F_No";
            this.F_No.ReadOnly = true;
            this.F_No.Width = 50;
            // 
            // F_Src
            // 
            this.F_Src.HeaderText = "원본";
            this.F_Src.Name = "F_Src";
            this.F_Src.ReadOnly = true;
            this.F_Src.Width = 250;
            // 
            // F_CltrNo
            // 
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.F_CltrNo.DefaultCellStyle = dataGridViewCellStyle2;
            this.F_CltrNo.HeaderText = "CltrNo";
            this.F_CltrNo.Name = "F_CltrNo";
            this.F_CltrNo.ReadOnly = true;
            this.F_CltrNo.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.F_CltrNo.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // F_Local
            // 
            this.F_Local.HeaderText = "로컬파일";
            this.F_Local.Name = "F_Local";
            this.F_Local.ReadOnly = true;
            this.F_Local.Width = 250;
            // 
            // F_Remote
            // 
            this.F_Remote.HeaderText = "원격파일";
            this.F_Remote.Name = "F_Remote";
            this.F_Remote.ReadOnly = true;
            this.F_Remote.Width = 250;
            // 
            // F_S1
            // 
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.F_S1.DefaultCellStyle = dataGridViewCellStyle3;
            this.F_S1.HeaderText = "S-1";
            this.F_S1.Name = "F_S1";
            this.F_S1.ReadOnly = true;
            this.F_S1.Width = 40;
            // 
            // F_Msg
            // 
            this.F_Msg.HeaderText = "비고";
            this.F_Msg.Name = "F_Msg";
            this.F_Msg.ReadOnly = true;
            this.F_Msg.Width = 300;
            // 
            // btnOpenFiles
            // 
            this.btnOpenFiles.Location = new System.Drawing.Point(12, 13);
            this.btnOpenFiles.Name = "btnOpenFiles";
            this.btnOpenFiles.Size = new System.Drawing.Size(75, 23);
            this.btnOpenFiles.TabIndex = 2;
            this.btnOpenFiles.Text = "파일열기...";
            this.btnOpenFiles.UseVisualStyleBackColor = true;
            this.btnOpenFiles.Click += new System.EventHandler(this.btnOpenFiles_Click);
            // 
            // btnStart
            // 
            this.btnStart.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnStart.Location = new System.Drawing.Point(785, 13);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(75, 23);
            this.btnStart.TabIndex = 0;
            this.btnStart.Text = "처리시작";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.btnOpenFiles);
            this.panel1.Controls.Add(this.btnStart);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(872, 46);
            this.panel1.TabIndex = 4;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.tbcF);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.dgF);
            this.splitContainer1.Panel2.Controls.Add(this.panel1);
            this.splitContainer1.Size = new System.Drawing.Size(1884, 1011);
            this.splitContainer1.SplitterDistance = 1008;
            this.splitContainer1.TabIndex = 6;
            // 
            // tbcF
            // 
            this.tbcF.Controls.Add(this.tabWbr);
            this.tbcF.Controls.Add(this.tabPdf);
            this.tbcF.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbcF.Location = new System.Drawing.Point(0, 0);
            this.tbcF.Name = "tbcF";
            this.tbcF.SelectedIndex = 0;
            this.tbcF.Size = new System.Drawing.Size(1008, 1011);
            this.tbcF.TabIndex = 0;
            // 
            // tabWbr
            // 
            this.tabWbr.Controls.Add(this.wbr1);
            this.tabWbr.Location = new System.Drawing.Point(4, 22);
            this.tabWbr.Name = "tabWbr";
            this.tabWbr.Padding = new System.Windows.Forms.Padding(3);
            this.tabWbr.Size = new System.Drawing.Size(1000, 985);
            this.tabWbr.TabIndex = 2;
            this.tabWbr.Text = "웹";
            this.tabWbr.UseVisualStyleBackColor = true;
            // 
            // wbr1
            // 
            this.wbr1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.wbr1.Location = new System.Drawing.Point(3, 3);
            this.wbr1.MinimumSize = new System.Drawing.Size(20, 20);
            this.wbr1.Name = "wbr1";
            this.wbr1.ScriptErrorsSuppressed = true;
            this.wbr1.Size = new System.Drawing.Size(994, 979);
            this.wbr1.TabIndex = 0;
            // 
            // tabPdf
            // 
            this.tabPdf.Controls.Add(this.axAcroPDF1);
            this.tabPdf.Location = new System.Drawing.Point(4, 22);
            this.tabPdf.Name = "tabPdf";
            this.tabPdf.Padding = new System.Windows.Forms.Padding(3);
            this.tabPdf.Size = new System.Drawing.Size(1000, 985);
            this.tabPdf.TabIndex = 3;
            this.tabPdf.Text = "PDF";
            this.tabPdf.UseVisualStyleBackColor = true;
            // 
            // axAcroPDF1
            // 
            this.axAcroPDF1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.axAcroPDF1.Enabled = true;
            this.axAcroPDF1.Location = new System.Drawing.Point(3, 3);
            this.axAcroPDF1.Name = "axAcroPDF1";
            this.axAcroPDF1.OcxState = ((System.Windows.Forms.AxHost.State)(resources.GetObject("axAcroPDF1.OcxState")));
            this.axAcroPDF1.Size = new System.Drawing.Size(994, 979);
            this.axAcroPDF1.TabIndex = 0;
            // 
            // wfLeasTk
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1884, 1011);
            this.Controls.Add(this.splitContainer1);
            this.Name = "wfLeasTk";
            this.Text = "공매-세대열람";
            ((System.ComponentModel.ISupportInitialize)(this.dgF)).EndInit();
            this.panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.tbcF.ResumeLayout(false);
            this.tabWbr.ResumeLayout(false);
            this.tabPdf.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.axAcroPDF1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.DataGridView dgF;
        private System.Windows.Forms.Button btnOpenFiles;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TabControl tbcF;
        private System.Windows.Forms.TabPage tabWbr;
        private System.Windows.Forms.WebBrowser wbr1;
        private System.Windows.Forms.DataGridViewCheckBoxColumn F_Chk;
        private System.Windows.Forms.DataGridViewTextBoxColumn F_No;
        private System.Windows.Forms.DataGridViewTextBoxColumn F_Src;
        private System.Windows.Forms.DataGridViewLinkColumn F_CltrNo;
        private System.Windows.Forms.DataGridViewTextBoxColumn F_Local;
        private System.Windows.Forms.DataGridViewTextBoxColumn F_Remote;
        private System.Windows.Forms.DataGridViewTextBoxColumn F_S1;
        private System.Windows.Forms.DataGridViewTextBoxColumn F_Msg;
        private System.Windows.Forms.TabPage tabPdf;
        private AxAcroPDFLib.AxAcroPDF axAcroPDF1;
    }
}