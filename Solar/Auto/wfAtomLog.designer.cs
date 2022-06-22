namespace Solar.Auto
{
    partial class wfAtomLog
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle9 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle10 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle11 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle12 = new System.Windows.Forms.DataGridViewCellStyle();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.dg = new System.Windows.Forms.DataGridView();
            this.dg_No = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_PrcNm = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_VmNm = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_IP = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_BgnDtm = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_EndDtm = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_RunTm = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_Idx = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.cbxVM = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.cbxPrcDvsn = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.label62 = new System.Windows.Forms.Label();
            this.dtpBgn = new System.Windows.Forms.DateTimePicker();
            this.dtpEnd = new System.Windows.Forms.DateTimePicker();
            this.btnSrch = new System.Windows.Forms.Button();
            this.lsv = new System.Windows.Forms.ListView();
            this.lsv_Tm = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.lsv_Dtl = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dg)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.dg);
            this.splitContainer1.Panel1.Controls.Add(this.groupBox1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.lsv);
            this.splitContainer1.Size = new System.Drawing.Size(1884, 961);
            this.splitContainer1.SplitterDistance = 944;
            this.splitContainer1.TabIndex = 0;
            // 
            // dg
            // 
            this.dg.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dg.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.dg_No,
            this.dg_PrcNm,
            this.dg_VmNm,
            this.dg_IP,
            this.dg_BgnDtm,
            this.dg_EndDtm,
            this.dg_RunTm,
            this.dg_Idx});
            this.dg.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dg.Location = new System.Drawing.Point(0, 75);
            this.dg.Name = "dg";
            this.dg.RowTemplate.Height = 23;
            this.dg.Size = new System.Drawing.Size(944, 886);
            this.dg.TabIndex = 1;
            // 
            // dg_No
            // 
            dataGridViewCellStyle7.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dg_No.DefaultCellStyle = dataGridViewCellStyle7;
            this.dg_No.HeaderText = "No";
            this.dg_No.Name = "dg_No";
            // 
            // dg_PrcNm
            // 
            this.dg_PrcNm.HeaderText = "처리구분";
            this.dg_PrcNm.Name = "dg_PrcNm";
            // 
            // dg_VmNm
            // 
            dataGridViewCellStyle8.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dg_VmNm.DefaultCellStyle = dataGridViewCellStyle8;
            this.dg_VmNm.HeaderText = "가상머신";
            this.dg_VmNm.Name = "dg_VmNm";
            // 
            // dg_IP
            // 
            this.dg_IP.HeaderText = "아이피";
            this.dg_IP.Name = "dg_IP";
            // 
            // dg_BgnDtm
            // 
            dataGridViewCellStyle9.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dg_BgnDtm.DefaultCellStyle = dataGridViewCellStyle9;
            this.dg_BgnDtm.HeaderText = "시작 시간";
            this.dg_BgnDtm.Name = "dg_BgnDtm";
            // 
            // dg_EndDtm
            // 
            dataGridViewCellStyle10.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dg_EndDtm.DefaultCellStyle = dataGridViewCellStyle10;
            this.dg_EndDtm.HeaderText = "완료 시간";
            this.dg_EndDtm.Name = "dg_EndDtm";
            // 
            // dg_RunTm
            // 
            dataGridViewCellStyle11.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dg_RunTm.DefaultCellStyle = dataGridViewCellStyle11;
            this.dg_RunTm.HeaderText = "소요 시간";
            this.dg_RunTm.Name = "dg_RunTm";
            // 
            // dg_Idx
            // 
            dataGridViewCellStyle12.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.dg_Idx.DefaultCellStyle = dataGridViewCellStyle12;
            this.dg_Idx.HeaderText = "idx";
            this.dg_Idx.Name = "dg_Idx";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.cbxVM);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.cbxPrcDvsn);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.label62);
            this.groupBox1.Controls.Add(this.dtpBgn);
            this.groupBox1.Controls.Add(this.dtpEnd);
            this.groupBox1.Controls.Add(this.btnSrch);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox1.Location = new System.Drawing.Point(0, 0);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(944, 75);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "<검 색>";
            // 
            // cbxVM
            // 
            this.cbxVM.FormattingEnabled = true;
            this.cbxVM.Items.AddRange(new object[] {
            "-선택-",
            "SOLAR",
            "VM-1",
            "VM-2",
            "VM-3",
            "VM-4",
            "VM-5",
            "VM-6",
            "VM-7",
            "VM-8",
            "VM-9",
            "VM-10",
            "VM-11",
            "VM-12"});
            this.cbxVM.Location = new System.Drawing.Point(674, 30);
            this.cbxVM.Name = "cbxVM";
            this.cbxVM.Size = new System.Drawing.Size(121, 20);
            this.cbxVM.TabIndex = 13;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(615, 34);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(53, 12);
            this.label3.TabIndex = 12;
            this.label3.Text = "가상머신";
            // 
            // cbxPrcDvsn
            // 
            this.cbxPrcDvsn.FormattingEnabled = true;
            this.cbxPrcDvsn.Location = new System.Drawing.Point(449, 30);
            this.cbxPrcDvsn.Name = "cbxPrcDvsn";
            this.cbxPrcDvsn.Size = new System.Drawing.Size(138, 20);
            this.cbxPrcDvsn.TabIndex = 11;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(390, 34);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(53, 12);
            this.label2.TabIndex = 10;
            this.label2.Text = "처리구분";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(8, 34);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(53, 12);
            this.label1.TabIndex = 9;
            this.label1.Text = "실행일자";
            // 
            // label62
            // 
            this.label62.AutoSize = true;
            this.label62.Location = new System.Drawing.Point(209, 34);
            this.label62.Name = "label62";
            this.label62.Size = new System.Drawing.Size(14, 12);
            this.label62.TabIndex = 8;
            this.label62.Text = "~";
            // 
            // dtpBgn
            // 
            this.dtpBgn.Checked = false;
            this.dtpBgn.CustomFormat = "yyyy.MM.dd (ddd)";
            this.dtpBgn.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dtpBgn.Location = new System.Drawing.Point(69, 30);
            this.dtpBgn.Name = "dtpBgn";
            this.dtpBgn.ShowCheckBox = true;
            this.dtpBgn.Size = new System.Drawing.Size(137, 21);
            this.dtpBgn.TabIndex = 6;
            // 
            // dtpEnd
            // 
            this.dtpEnd.Checked = false;
            this.dtpEnd.CustomFormat = "yyyy.MM.dd (ddd)";
            this.dtpEnd.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dtpEnd.Location = new System.Drawing.Point(226, 30);
            this.dtpEnd.Name = "dtpEnd";
            this.dtpEnd.ShowCheckBox = true;
            this.dtpEnd.Size = new System.Drawing.Size(137, 21);
            this.dtpEnd.TabIndex = 7;
            // 
            // btnSrch
            // 
            this.btnSrch.Location = new System.Drawing.Point(853, 29);
            this.btnSrch.Name = "btnSrch";
            this.btnSrch.Size = new System.Drawing.Size(75, 23);
            this.btnSrch.TabIndex = 0;
            this.btnSrch.Text = "검색";
            this.btnSrch.UseVisualStyleBackColor = true;
            this.btnSrch.Click += new System.EventHandler(this.btnSrch_Click);
            // 
            // lsv
            // 
            this.lsv.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.lsv_Tm,
            this.lsv_Dtl});
            this.lsv.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lsv.FullRowSelect = true;
            this.lsv.GridLines = true;
            this.lsv.HideSelection = false;
            this.lsv.Location = new System.Drawing.Point(0, 0);
            this.lsv.Name = "lsv";
            this.lsv.Size = new System.Drawing.Size(936, 961);
            this.lsv.TabIndex = 0;
            this.lsv.UseCompatibleStateImageBehavior = false;
            this.lsv.View = System.Windows.Forms.View.Details;
            // 
            // lsv_Tm
            // 
            this.lsv_Tm.Text = "처리 시간";
            this.lsv_Tm.Width = 100;
            // 
            // lsv_Dtl
            // 
            this.lsv_Dtl.Text = "상세 내용";
            this.lsv_Dtl.Width = 600;
            // 
            // wfAtomLog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1884, 961);
            this.Controls.Add(this.splitContainer1);
            this.Name = "wfAtomLog";
            this.Text = "wfAtomLog";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dg)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.DataGridView dg;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button btnSrch;
        private System.Windows.Forms.ListView lsv;
        private System.Windows.Forms.ColumnHeader lsv_Tm;
        private System.Windows.Forms.ColumnHeader lsv_Dtl;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label62;
        private System.Windows.Forms.DateTimePicker dtpBgn;
        private System.Windows.Forms.DateTimePicker dtpEnd;
        private System.Windows.Forms.ComboBox cbxPrcDvsn;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox cbxVM;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_No;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_PrcNm;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_VmNm;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_IP;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_BgnDtm;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_EndDtm;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_RunTm;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_Idx;
    }
}