namespace Solar.CA
{
    partial class wfLandPlan
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(wfLandPlan));
            this.splitter2 = new System.Windows.Forms.Splitter();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.btnSave = new System.Windows.Forms.Button();
            this.dgR = new System.Windows.Forms.DataGridView();
            this.dgR_Chk = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.dgR_Name = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgR_Code = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgR_Lvl1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgR_Lvl2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.wbr = new System.Windows.Forms.WebBrowser();
            this.splitter1 = new System.Windows.Forms.Splitter();
            this.chkApsl = new System.Windows.Forms.CheckBox();
            this.panel2 = new System.Windows.Forms.Panel();
            this.label2 = new System.Windows.Forms.Label();
            this.chkPid = new System.Windows.Forms.CheckBox();
            this.txtPid = new System.Windows.Forms.TextBox();
            this.dg = new System.Windows.Forms.DataGridView();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnSrch = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.axAcroPDF1 = new AxAcroPDFLib.AxAcroPDF();
            this.groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgR)).BeginInit();
            this.panel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dg)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.axAcroPDF1)).BeginInit();
            this.SuspendLayout();
            // 
            // splitter2
            // 
            this.splitter2.Dock = System.Windows.Forms.DockStyle.Right;
            this.splitter2.Location = new System.Drawing.Point(1493, 0);
            this.splitter2.Name = "splitter2";
            this.splitter2.Size = new System.Drawing.Size(3, 961);
            this.splitter2.TabIndex = 9;
            this.splitter2.TabStop = false;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.btnSave);
            this.groupBox2.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox2.Location = new System.Drawing.Point(0, 0);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(388, 50);
            this.groupBox2.TabIndex = 5;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "지역규제";
            // 
            // btnSave
            // 
            this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSave.Location = new System.Drawing.Point(307, 17);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(75, 23);
            this.btnSave.TabIndex = 0;
            this.btnSave.Text = "저 장";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // dgR
            // 
            this.dgR.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgR.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.dgR_Chk,
            this.dgR_Name,
            this.dgR_Code,
            this.dgR_Lvl1,
            this.dgR_Lvl2});
            this.dgR.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgR.Location = new System.Drawing.Point(0, 50);
            this.dgR.Name = "dgR";
            this.dgR.RowTemplate.Height = 23;
            this.dgR.Size = new System.Drawing.Size(388, 911);
            this.dgR.TabIndex = 6;
            this.dgR.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgR_CellContentClick);
            // 
            // dgR_Chk
            // 
            this.dgR_Chk.FalseValue = "0";
            this.dgR_Chk.HeaderText = "";
            this.dgR_Chk.Name = "dgR_Chk";
            this.dgR_Chk.TrueValue = "1";
            // 
            // dgR_Name
            // 
            this.dgR_Name.HeaderText = "Name";
            this.dgR_Name.Name = "dgR_Name";
            this.dgR_Name.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.dgR_Name.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // dgR_Code
            // 
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dgR_Code.DefaultCellStyle = dataGridViewCellStyle1;
            this.dgR_Code.HeaderText = "Code";
            this.dgR_Code.Name = "dgR_Code";
            // 
            // dgR_Lvl1
            // 
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dgR_Lvl1.DefaultCellStyle = dataGridViewCellStyle2;
            this.dgR_Lvl1.HeaderText = "Lvl-1";
            this.dgR_Lvl1.Name = "dgR_Lvl1";
            this.dgR_Lvl1.Width = 50;
            // 
            // dgR_Lvl2
            // 
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dgR_Lvl2.DefaultCellStyle = dataGridViewCellStyle3;
            this.dgR_Lvl2.HeaderText = "Lvl-2";
            this.dgR_Lvl2.Name = "dgR_Lvl2";
            this.dgR_Lvl2.Width = 50;
            // 
            // wbr
            // 
            this.wbr.CausesValidation = false;
            this.wbr.Dock = System.Windows.Forms.DockStyle.Fill;
            this.wbr.Location = new System.Drawing.Point(3, 3);
            this.wbr.MinimumSize = new System.Drawing.Size(20, 20);
            this.wbr.Name = "wbr";
            this.wbr.Size = new System.Drawing.Size(704, 929);
            this.wbr.TabIndex = 7;
            // 
            // splitter1
            // 
            this.splitter1.Location = new System.Drawing.Point(772, 0);
            this.splitter1.Name = "splitter1";
            this.splitter1.Size = new System.Drawing.Size(3, 961);
            this.splitter1.TabIndex = 6;
            this.splitter1.TabStop = false;
            // 
            // chkApsl
            // 
            this.chkApsl.AutoSize = true;
            this.chkApsl.Location = new System.Drawing.Point(12, 21);
            this.chkApsl.Name = "chkApsl";
            this.chkApsl.Size = new System.Drawing.Size(112, 16);
            this.chkApsl.TabIndex = 1;
            this.chkApsl.Text = "감정평가서 열기";
            this.chkApsl.UseVisualStyleBackColor = true;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.dgR);
            this.panel2.Controls.Add(this.groupBox2);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Right;
            this.panel2.Location = new System.Drawing.Point(1496, 0);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(388, 961);
            this.panel2.TabIndex = 8;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(348, 22);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(50, 12);
            this.label2.TabIndex = 4;
            this.label2.Text = "* TID > ";
            // 
            // chkPid
            // 
            this.chkPid.AutoSize = true;
            this.chkPid.Location = new System.Drawing.Point(402, 21);
            this.chkPid.Name = "chkPid";
            this.chkPid.Size = new System.Drawing.Size(15, 14);
            this.chkPid.TabIndex = 3;
            this.chkPid.UseVisualStyleBackColor = true;
            // 
            // txtPid
            // 
            this.txtPid.Location = new System.Drawing.Point(420, 18);
            this.txtPid.Name = "txtPid";
            this.txtPid.Size = new System.Drawing.Size(100, 21);
            this.txtPid.TabIndex = 2;
            // 
            // dg
            // 
            this.dg.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dg.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dg.Location = new System.Drawing.Point(0, 50);
            this.dg.Name = "dg";
            this.dg.ReadOnly = true;
            this.dg.RowTemplate.Height = 23;
            this.dg.Size = new System.Drawing.Size(772, 911);
            this.dg.TabIndex = 1;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.chkPid);
            this.groupBox1.Controls.Add(this.txtPid);
            this.groupBox1.Controls.Add(this.chkApsl);
            this.groupBox1.Controls.Add(this.btnSrch);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox1.Location = new System.Drawing.Point(0, 0);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(772, 50);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "물건검색";
            // 
            // btnSrch
            // 
            this.btnSrch.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSrch.Location = new System.Drawing.Point(691, 17);
            this.btnSrch.Name = "btnSrch";
            this.btnSrch.Size = new System.Drawing.Size(75, 23);
            this.btnSrch.TabIndex = 0;
            this.btnSrch.Text = "검 색";
            this.btnSrch.UseVisualStyleBackColor = true;
            this.btnSrch.Click += new System.EventHandler(this.btnSrch_Click);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.dg);
            this.panel1.Controls.Add(this.groupBox1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Left;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(772, 961);
            this.panel1.TabIndex = 5;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(775, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(718, 961);
            this.tabControl1.TabIndex = 10;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.wbr);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(710, 935);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "토지이용계획";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.axAcroPDF1);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(710, 935);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "감정평가서";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // axAcroPDF1
            // 
            this.axAcroPDF1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.axAcroPDF1.Enabled = true;
            this.axAcroPDF1.Location = new System.Drawing.Point(3, 3);
            this.axAcroPDF1.Name = "axAcroPDF1";
            this.axAcroPDF1.OcxState = ((System.Windows.Forms.AxHost.State)(resources.GetObject("axAcroPDF1.OcxState")));
            this.axAcroPDF1.Size = new System.Drawing.Size(704, 929);
            this.axAcroPDF1.TabIndex = 0;
            // 
            // wfLandPlan
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1884, 961);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.splitter2);
            this.Controls.Add(this.splitter1);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel1);
            this.Name = "wfLandPlan";
            this.Text = "경매-토지용도지역지구";
            this.groupBox2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgR)).EndInit();
            this.panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dg)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.axAcroPDF1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Splitter splitter2;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.DataGridView dgR;
        private System.Windows.Forms.WebBrowser wbr;
        private System.Windows.Forms.Splitter splitter1;
        private System.Windows.Forms.CheckBox chkApsl;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.CheckBox chkPid;
        private System.Windows.Forms.TextBox txtPid;
        private System.Windows.Forms.DataGridView dg;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button btnSrch;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.DataGridViewCheckBoxColumn dgR_Chk;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgR_Name;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgR_Code;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgR_Lvl1;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgR_Lvl2;
        private AxAcroPDFLib.AxAcroPDF axAcroPDF1;
    }
}