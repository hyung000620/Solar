namespace Solar.Mgmt
{
    partial class wfStaff
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle21 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle22 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle23 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle24 = new System.Windows.Forms.DataGridViewCellStyle();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.dg = new System.Windows.Forms.DataGridView();
            this.dg_No = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_Nm = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_Id = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_Level = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_Idx = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_State = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnSrch = new System.Windows.Forms.Button();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabInfo = new System.Windows.Forms.TabPage();
            this.label4 = new System.Windows.Forms.Label();
            this.txtLevel = new System.Windows.Forms.TextBox();
            this.txtUsrIdx = new System.Windows.Forms.TextBox();
            this.btnNew = new System.Windows.Forms.Button();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.chkPwdMdfy = new System.Windows.Forms.CheckBox();
            this.btnSave = new System.Windows.Forms.Button();
            this.txtPwd = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.txtUsrId = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.txtUsrNm = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.button1 = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dg)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabInfo.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
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
            this.splitContainer1.Panel2.Controls.Add(this.tabControl1);
            this.splitContainer1.Size = new System.Drawing.Size(1884, 961);
            this.splitContainer1.SplitterDistance = 794;
            this.splitContainer1.TabIndex = 0;
            // 
            // dg
            // 
            this.dg.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dg.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.dg_No,
            this.dg_Nm,
            this.dg_Id,
            this.dg_Level,
            this.dg_Idx,
            this.dg_State});
            this.dg.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dg.Location = new System.Drawing.Point(0, 104);
            this.dg.Name = "dg";
            this.dg.RowTemplate.Height = 23;
            this.dg.Size = new System.Drawing.Size(794, 857);
            this.dg.TabIndex = 1;
            // 
            // dg_No
            // 
            dataGridViewCellStyle21.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dg_No.DefaultCellStyle = dataGridViewCellStyle21;
            this.dg_No.HeaderText = "No";
            this.dg_No.Name = "dg_No";
            this.dg_No.Width = 50;
            // 
            // dg_Nm
            // 
            dataGridViewCellStyle22.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dg_Nm.DefaultCellStyle = dataGridViewCellStyle22;
            this.dg_Nm.HeaderText = "직원명";
            this.dg_Nm.Name = "dg_Nm";
            // 
            // dg_Id
            // 
            this.dg_Id.HeaderText = "아이디";
            this.dg_Id.Name = "dg_Id";
            // 
            // dg_Level
            // 
            dataGridViewCellStyle23.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dg_Level.DefaultCellStyle = dataGridViewCellStyle23;
            this.dg_Level.HeaderText = "레벨";
            this.dg_Level.Name = "dg_Level";
            this.dg_Level.Width = 60;
            // 
            // dg_Idx
            // 
            dataGridViewCellStyle24.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.dg_Idx.DefaultCellStyle = dataGridViewCellStyle24;
            this.dg_Idx.HeaderText = "idx";
            this.dg_Idx.Name = "dg_Idx";
            this.dg_Idx.Width = 50;
            // 
            // dg_State
            // 
            this.dg_State.HeaderText = "상태";
            this.dg_State.Name = "dg_State";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.btnSrch);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBox1.Location = new System.Drawing.Point(0, 0);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(794, 104);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "groupBox1";
            // 
            // btnSrch
            // 
            this.btnSrch.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSrch.Location = new System.Drawing.Point(713, 67);
            this.btnSrch.Name = "btnSrch";
            this.btnSrch.Size = new System.Drawing.Size(75, 23);
            this.btnSrch.TabIndex = 0;
            this.btnSrch.Text = "검색";
            this.btnSrch.UseVisualStyleBackColor = true;
            this.btnSrch.Click += new System.EventHandler(this.btnSrch_Click);
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabInfo);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(1086, 961);
            this.tabControl1.TabIndex = 0;
            // 
            // tabInfo
            // 
            this.tabInfo.Controls.Add(this.button1);
            this.tabInfo.Controls.Add(this.label4);
            this.tabInfo.Controls.Add(this.txtLevel);
            this.tabInfo.Controls.Add(this.txtUsrIdx);
            this.tabInfo.Controls.Add(this.btnNew);
            this.tabInfo.Controls.Add(this.pictureBox1);
            this.tabInfo.Controls.Add(this.chkPwdMdfy);
            this.tabInfo.Controls.Add(this.btnSave);
            this.tabInfo.Controls.Add(this.txtPwd);
            this.tabInfo.Controls.Add(this.label3);
            this.tabInfo.Controls.Add(this.txtUsrId);
            this.tabInfo.Controls.Add(this.label2);
            this.tabInfo.Controls.Add(this.txtUsrNm);
            this.tabInfo.Controls.Add(this.label1);
            this.tabInfo.Location = new System.Drawing.Point(4, 22);
            this.tabInfo.Name = "tabInfo";
            this.tabInfo.Padding = new System.Windows.Forms.Padding(3);
            this.tabInfo.Size = new System.Drawing.Size(1078, 935);
            this.tabInfo.TabIndex = 0;
            this.tabInfo.Text = "직원정보";
            this.tabInfo.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(74, 193);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(53, 12);
            this.label4.TabIndex = 8;
            this.label4.Text = "관리레벨";
            // 
            // txtLevel
            // 
            this.txtLevel.Location = new System.Drawing.Point(131, 190);
            this.txtLevel.Name = "txtLevel";
            this.txtLevel.Size = new System.Drawing.Size(57, 21);
            this.txtLevel.TabIndex = 7;
            // 
            // txtUsrIdx
            // 
            this.txtUsrIdx.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.txtUsrIdx.Location = new System.Drawing.Point(911, 8);
            this.txtUsrIdx.Name = "txtUsrIdx";
            this.txtUsrIdx.ReadOnly = true;
            this.txtUsrIdx.Size = new System.Drawing.Size(78, 21);
            this.txtUsrIdx.TabIndex = 6;
            this.txtUsrIdx.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.txtUsrIdx.Visible = false;
            // 
            // btnNew
            // 
            this.btnNew.Location = new System.Drawing.Point(6, 8);
            this.btnNew.Name = "btnNew";
            this.btnNew.Size = new System.Drawing.Size(49, 23);
            this.btnNew.TabIndex = 5;
            this.btnNew.Text = "신규+";
            this.btnNew.UseVisualStyleBackColor = true;
            this.btnNew.Click += new System.EventHandler(this.btnNew_Click);
            // 
            // pictureBox1
            // 
            this.pictureBox1.Location = new System.Drawing.Point(470, 79);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(100, 85);
            this.pictureBox1.TabIndex = 4;
            this.pictureBox1.TabStop = false;
            // 
            // chkPwdMdfy
            // 
            this.chkPwdMdfy.AutoSize = true;
            this.chkPwdMdfy.Location = new System.Drawing.Point(237, 145);
            this.chkPwdMdfy.Name = "chkPwdMdfy";
            this.chkPwdMdfy.Size = new System.Drawing.Size(48, 16);
            this.chkPwdMdfy.TabIndex = 3;
            this.chkPwdMdfy.Text = "변경";
            this.chkPwdMdfy.UseVisualStyleBackColor = true;
            // 
            // btnSave
            // 
            this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSave.Location = new System.Drawing.Point(995, 6);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(75, 23);
            this.btnSave.TabIndex = 3;
            this.btnSave.Text = "저장";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // txtPwd
            // 
            this.txtPwd.Location = new System.Drawing.Point(131, 143);
            this.txtPwd.Name = "txtPwd";
            this.txtPwd.Size = new System.Drawing.Size(100, 21);
            this.txtPwd.TabIndex = 2;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(74, 146);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(53, 12);
            this.label3.TabIndex = 0;
            this.label3.Text = "비밀번호";
            // 
            // txtUsrId
            // 
            this.txtUsrId.Location = new System.Drawing.Point(131, 111);
            this.txtUsrId.Name = "txtUsrId";
            this.txtUsrId.Size = new System.Drawing.Size(100, 21);
            this.txtUsrId.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(86, 115);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(41, 12);
            this.label2.TabIndex = 0;
            this.label2.Text = "아이디";
            // 
            // txtUsrNm
            // 
            this.txtUsrNm.Location = new System.Drawing.Point(131, 79);
            this.txtUsrNm.Name = "txtUsrNm";
            this.txtUsrNm.Size = new System.Drawing.Size(100, 21);
            this.txtUsrNm.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(86, 83);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(41, 12);
            this.label1.TabIndex = 0;
            this.label1.Text = "직원명";
            // 
            // tabPage2
            // 
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(1078, 935);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "tabPage2";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(61, 9);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(74, 22);
            this.button1.TabIndex = 9;
            this.button1.Text = "퇴사처리";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.btnLeave_Click);
            // 
            // wfStaff
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1884, 961);
            this.Controls.Add(this.splitContainer1);
            this.Name = "wfStaff";
            this.Text = "Mgmt-직원관리";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dg)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabInfo.ResumeLayout(false);
            this.tabInfo.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.DataGridView dg;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabInfo;
        private System.Windows.Forms.Button btnNew;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.CheckBox chkPwdMdfy;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.TextBox txtPwd;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox txtUsrId;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtUsrNm;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.Button btnSrch;
        private System.Windows.Forms.TextBox txtUsrIdx;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox txtLevel;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_No;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_Nm;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_Id;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_Level;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_Idx;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_State;
        private System.Windows.Forms.Button button1;
    }
}