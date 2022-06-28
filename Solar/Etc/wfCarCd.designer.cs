namespace Solar.Etc
{
    partial class wfCarCd
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.wbr = new System.Windows.Forms.WebBrowser();
            this.dgMo = new System.Windows.Forms.DataGridView();
            this.dgMo_No = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgMo_CoNm = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgMo_CoCd = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgMo_MoNm = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgMo_MoCd = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panel2 = new System.Windows.Forms.Panel();
            this.dgCo = new System.Windows.Forms.DataGridView();
            this.dgCo_No = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgCo_Nm = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgCo_Cd = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgCo_Dmst = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panel1 = new System.Windows.Forms.Panel();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.txtNewModelCnt = new System.Windows.Forms.TextBox();
            this.txtNewCmpyCnt = new System.Windows.Forms.TextBox();
            this.btnModel = new System.Windows.Forms.Button();
            this.btnCmpy = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgMo)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgCo)).BeginInit();
            this.panel1.SuspendLayout();
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
            this.splitContainer1.Panel1.Controls.Add(this.wbr);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.AutoScroll = true;
            this.splitContainer1.Panel2.Controls.Add(this.dgMo);
            this.splitContainer1.Panel2.Controls.Add(this.panel2);
            this.splitContainer1.Panel2.Controls.Add(this.dgCo);
            this.splitContainer1.Panel2.Controls.Add(this.panel1);
            this.splitContainer1.Size = new System.Drawing.Size(1884, 1011);
            this.splitContainer1.SplitterDistance = 908;
            this.splitContainer1.TabIndex = 0;
            // 
            // wbr
            // 
            this.wbr.Dock = System.Windows.Forms.DockStyle.Fill;
            this.wbr.Location = new System.Drawing.Point(0, 0);
            this.wbr.MinimumSize = new System.Drawing.Size(20, 20);
            this.wbr.Name = "wbr";
            this.wbr.Size = new System.Drawing.Size(908, 1011);
            this.wbr.TabIndex = 0;
            this.wbr.Url = new System.Uri("https://www.kcar.com/car/search/car_search_list.do", System.UriKind.Absolute);
            // 
            // dgMo
            // 
            this.dgMo.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgMo.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.dgMo_No,
            this.dgMo_CoNm,
            this.dgMo_CoCd,
            this.dgMo_MoNm,
            this.dgMo_MoCd});
            this.dgMo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgMo.Location = new System.Drawing.Point(393, 51);
            this.dgMo.Name = "dgMo";
            this.dgMo.RowTemplate.Height = 23;
            this.dgMo.Size = new System.Drawing.Size(579, 960);
            this.dgMo.TabIndex = 3;
            // 
            // dgMo_No
            // 
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dgMo_No.DefaultCellStyle = dataGridViewCellStyle1;
            this.dgMo_No.HeaderText = "No";
            this.dgMo_No.Name = "dgMo_No";
            this.dgMo_No.Width = 70;
            // 
            // dgMo_CoNm
            // 
            this.dgMo_CoNm.HeaderText = "제조사명";
            this.dgMo_CoNm.Name = "dgMo_CoNm";
            this.dgMo_CoNm.Width = 150;
            // 
            // dgMo_CoCd
            // 
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dgMo_CoCd.DefaultCellStyle = dataGridViewCellStyle2;
            this.dgMo_CoCd.HeaderText = "제조사코드";
            this.dgMo_CoCd.Name = "dgMo_CoCd";
            this.dgMo_CoCd.Width = 90;
            // 
            // dgMo_MoNm
            // 
            this.dgMo_MoNm.HeaderText = "모델명";
            this.dgMo_MoNm.Name = "dgMo_MoNm";
            this.dgMo_MoNm.Width = 150;
            // 
            // dgMo_MoCd
            // 
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dgMo_MoCd.DefaultCellStyle = dataGridViewCellStyle3;
            this.dgMo_MoCd.HeaderText = "모델코드";
            this.dgMo_MoCd.Name = "dgMo_MoCd";
            this.dgMo_MoCd.Width = 80;
            // 
            // panel2
            // 
            this.panel2.Dock = System.Windows.Forms.DockStyle.Left;
            this.panel2.Location = new System.Drawing.Point(331, 51);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(62, 960);
            this.panel2.TabIndex = 2;
            // 
            // dgCo
            // 
            this.dgCo.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgCo.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.dgCo_No,
            this.dgCo_Nm,
            this.dgCo_Cd,
            this.dgCo_Dmst});
            this.dgCo.Dock = System.Windows.Forms.DockStyle.Left;
            this.dgCo.Location = new System.Drawing.Point(0, 51);
            this.dgCo.Name = "dgCo";
            this.dgCo.RowTemplate.Height = 23;
            this.dgCo.Size = new System.Drawing.Size(331, 960);
            this.dgCo.TabIndex = 1;
            // 
            // dgCo_No
            // 
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dgCo_No.DefaultCellStyle = dataGridViewCellStyle4;
            this.dgCo_No.HeaderText = "No";
            this.dgCo_No.Name = "dgCo_No";
            // 
            // dgCo_Nm
            // 
            this.dgCo_Nm.HeaderText = "제조사";
            this.dgCo_Nm.Name = "dgCo_Nm";
            // 
            // dgCo_Cd
            // 
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dgCo_Cd.DefaultCellStyle = dataGridViewCellStyle5;
            this.dgCo_Cd.HeaderText = "코드";
            this.dgCo_Cd.Name = "dgCo_Cd";
            // 
            // dgCo_Dmst
            // 
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dgCo_Dmst.DefaultCellStyle = dataGridViewCellStyle6;
            this.dgCo_Dmst.HeaderText = "국산";
            this.dgCo_Dmst.Name = "dgCo_Dmst";
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.txtNewModelCnt);
            this.panel1.Controls.Add(this.txtNewCmpyCnt);
            this.panel1.Controls.Add(this.btnModel);
            this.panel1.Controls.Add(this.btnCmpy);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(972, 51);
            this.panel1.TabIndex = 0;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(739, 17);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(29, 12);
            this.label2.TabIndex = 2;
            this.label2.Text = "신규";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(216, 17);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(29, 12);
            this.label1.TabIndex = 2;
            this.label1.Text = "신규";
            // 
            // txtNewModelCnt
            // 
            this.txtNewModelCnt.Location = new System.Drawing.Point(774, 12);
            this.txtNewModelCnt.Name = "txtNewModelCnt";
            this.txtNewModelCnt.Size = new System.Drawing.Size(48, 21);
            this.txtNewModelCnt.TabIndex = 1;
            this.txtNewModelCnt.Text = "0";
            this.txtNewModelCnt.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // txtNewCmpyCnt
            // 
            this.txtNewCmpyCnt.Location = new System.Drawing.Point(251, 12);
            this.txtNewCmpyCnt.Name = "txtNewCmpyCnt";
            this.txtNewCmpyCnt.Size = new System.Drawing.Size(48, 21);
            this.txtNewCmpyCnt.TabIndex = 1;
            this.txtNewCmpyCnt.Text = "0";
            this.txtNewCmpyCnt.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // btnModel
            // 
            this.btnModel.Location = new System.Drawing.Point(632, 12);
            this.btnModel.Name = "btnModel";
            this.btnModel.Size = new System.Drawing.Size(75, 23);
            this.btnModel.TabIndex = 0;
            this.btnModel.Text = "모델그룹";
            this.btnModel.UseVisualStyleBackColor = true;
            this.btnModel.Click += new System.EventHandler(this.btnModel_Click);
            // 
            // btnCmpy
            // 
            this.btnCmpy.Location = new System.Drawing.Point(115, 12);
            this.btnCmpy.Name = "btnCmpy";
            this.btnCmpy.Size = new System.Drawing.Size(75, 23);
            this.btnCmpy.TabIndex = 0;
            this.btnCmpy.Text = "제조사";
            this.btnCmpy.UseVisualStyleBackColor = true;
            this.btnCmpy.Click += new System.EventHandler(this.btnCmpy_Click);
            // 
            // wfCarCd
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1884, 1011);
            this.Controls.Add(this.splitContainer1);
            this.Name = "wfCarCd";
            this.Text = "기타-차량코드관리";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgMo)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgCo)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.WebBrowser wbr;
        private System.Windows.Forms.DataGridView dgMo;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.DataGridView dgCo;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button btnModel;
        private System.Windows.Forms.Button btnCmpy;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgCo_No;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgCo_Nm;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgCo_Cd;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgCo_Dmst;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgMo_No;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgMo_CoNm;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgMo_CoCd;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgMo_MoNm;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgMo_MoCd;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtNewModelCnt;
        private System.Windows.Forms.TextBox txtNewCmpyCnt;
    }
}