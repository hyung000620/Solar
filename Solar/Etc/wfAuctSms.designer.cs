
namespace Solar.Etc
{
    partial class wfAuctSms
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle9 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle10 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle11 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle12 = new System.Windows.Forms.DataGridViewCellStyle();
            this.btnSrch = new System.Windows.Forms.Button();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.dg = new System.Windows.Forms.DataGridView();
            this.dg_No = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_Dvsn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_Tm = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_StaMsg = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_Send = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_Tid = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_CS = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_Dpt = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_SN = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_BidDt = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_State = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_Cat = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_FbCnt = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_ApslAmt = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_MinbAmt = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_Url = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_Idx = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panel1 = new System.Windows.Forms.Panel();
            this.label1 = new System.Windows.Forms.Label();
            this.rdoDvsn1 = new System.Windows.Forms.RadioButton();
            this.rdoDvsn0 = new System.Windows.Forms.RadioButton();
            this.rdoDvsnAll = new System.Windows.Forms.RadioButton();
            this.btnTest = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.wbr = new System.Windows.Forms.WebBrowser();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dg)).BeginInit();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnSrch
            // 
            this.btnSrch.Location = new System.Drawing.Point(305, 14);
            this.btnSrch.Name = "btnSrch";
            this.btnSrch.Size = new System.Drawing.Size(75, 23);
            this.btnSrch.TabIndex = 0;
            this.btnSrch.Text = "검색";
            this.btnSrch.UseVisualStyleBackColor = true;
            this.btnSrch.Click += new System.EventHandler(this.btnSrch_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.dg);
            this.splitContainer1.Panel1.Controls.Add(this.panel1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.wbr);
            this.splitContainer1.Size = new System.Drawing.Size(1884, 1011);
            this.splitContainer1.SplitterDistance = 1185;
            this.splitContainer1.TabIndex = 1;
            // 
            // dg
            // 
            this.dg.AllowUserToOrderColumns = true;
            this.dg.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dg.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.dg_No,
            this.dg_Dvsn,
            this.dg_Tm,
            this.dg_StaMsg,
            this.dg_Send,
            this.dg_Tid,
            this.dg_CS,
            this.dg_Dpt,
            this.dg_SN,
            this.dg_BidDt,
            this.dg_State,
            this.dg_Cat,
            this.dg_FbCnt,
            this.dg_ApslAmt,
            this.dg_MinbAmt,
            this.dg_Url,
            this.dg_Idx});
            this.dg.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dg.Location = new System.Drawing.Point(0, 53);
            this.dg.Margin = new System.Windows.Forms.Padding(0);
            this.dg.Name = "dg";
            this.dg.RowTemplate.Height = 23;
            this.dg.Size = new System.Drawing.Size(1181, 954);
            this.dg.TabIndex = 3;
            this.dg.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dg_CellClick);
            // 
            // dg_No
            // 
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.Silver;
            this.dg_No.DefaultCellStyle = dataGridViewCellStyle1;
            this.dg_No.Frozen = true;
            this.dg_No.HeaderText = "No";
            this.dg_No.Name = "dg_No";
            this.dg_No.Width = 60;
            // 
            // dg_Dvsn
            // 
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dg_Dvsn.DefaultCellStyle = dataGridViewCellStyle2;
            this.dg_Dvsn.Frozen = true;
            this.dg_Dvsn.HeaderText = "구분";
            this.dg_Dvsn.Name = "dg_Dvsn";
            // 
            // dg_Tm
            // 
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dg_Tm.DefaultCellStyle = dataGridViewCellStyle3;
            this.dg_Tm.Frozen = true;
            this.dg_Tm.HeaderText = "등록시간";
            this.dg_Tm.Name = "dg_Tm";
            // 
            // dg_StaMsg
            // 
            this.dg_StaMsg.Frozen = true;
            this.dg_StaMsg.HeaderText = "변경상태";
            this.dg_StaMsg.Name = "dg_StaMsg";
            // 
            // dg_Send
            // 
            this.dg_Send.Frozen = true;
            this.dg_Send.HeaderText = "발송";
            this.dg_Send.Name = "dg_Send";
            // 
            // dg_Tid
            // 
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dg_Tid.DefaultCellStyle = dataGridViewCellStyle4;
            this.dg_Tid.Frozen = true;
            this.dg_Tid.HeaderText = "TID";
            this.dg_Tid.Name = "dg_Tid";
            this.dg_Tid.Width = 60;
            // 
            // dg_CS
            // 
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dg_CS.DefaultCellStyle = dataGridViewCellStyle5;
            this.dg_CS.Frozen = true;
            this.dg_CS.HeaderText = "법원";
            this.dg_CS.Name = "dg_CS";
            this.dg_CS.Width = 80;
            // 
            // dg_Dpt
            // 
            this.dg_Dpt.Frozen = true;
            this.dg_Dpt.HeaderText = "계";
            this.dg_Dpt.Name = "dg_Dpt";
            this.dg_Dpt.Width = 40;
            // 
            // dg_SN
            // 
            dataGridViewCellStyle6.BackColor = System.Drawing.SystemColors.Info;
            dataGridViewCellStyle6.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dg_SN.DefaultCellStyle = dataGridViewCellStyle6;
            this.dg_SN.Frozen = true;
            this.dg_SN.HeaderText = "사건번호";
            this.dg_SN.Name = "dg_SN";
            this.dg_SN.Width = 120;
            // 
            // dg_BidDt
            // 
            dataGridViewCellStyle7.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dg_BidDt.DefaultCellStyle = dataGridViewCellStyle7;
            this.dg_BidDt.Frozen = true;
            this.dg_BidDt.HeaderText = "입찰일";
            this.dg_BidDt.Name = "dg_BidDt";
            this.dg_BidDt.Width = 70;
            // 
            // dg_State
            // 
            this.dg_State.Frozen = true;
            this.dg_State.HeaderText = "진행상태";
            this.dg_State.Name = "dg_State";
            this.dg_State.Width = 80;
            // 
            // dg_Cat
            // 
            dataGridViewCellStyle8.Font = new System.Drawing.Font("돋움", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.dg_Cat.DefaultCellStyle = dataGridViewCellStyle8;
            this.dg_Cat.HeaderText = "물건종류";
            this.dg_Cat.Name = "dg_Cat";
            this.dg_Cat.Width = 80;
            // 
            // dg_FbCnt
            // 
            dataGridViewCellStyle9.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.dg_FbCnt.DefaultCellStyle = dataGridViewCellStyle9;
            this.dg_FbCnt.HeaderText = "유찰";
            this.dg_FbCnt.Name = "dg_FbCnt";
            this.dg_FbCnt.Width = 55;
            // 
            // dg_ApslAmt
            // 
            dataGridViewCellStyle10.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.dg_ApslAmt.DefaultCellStyle = dataGridViewCellStyle10;
            this.dg_ApslAmt.HeaderText = "감정가";
            this.dg_ApslAmt.Name = "dg_ApslAmt";
            this.dg_ApslAmt.Width = 80;
            // 
            // dg_MinbAmt
            // 
            dataGridViewCellStyle11.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle11.ForeColor = System.Drawing.Color.Silver;
            this.dg_MinbAmt.DefaultCellStyle = dataGridViewCellStyle11;
            this.dg_MinbAmt.HeaderText = "최저가";
            this.dg_MinbAmt.Name = "dg_MinbAmt";
            this.dg_MinbAmt.Width = 80;
            // 
            // dg_Url
            // 
            this.dg_Url.HeaderText = "url";
            this.dg_Url.Name = "dg_Url";
            // 
            // dg_Idx
            // 
            dataGridViewCellStyle12.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.dg_Idx.DefaultCellStyle = dataGridViewCellStyle12;
            this.dg_Idx.HeaderText = "idx";
            this.dg_Idx.Name = "dg_Idx";
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.rdoDvsn1);
            this.panel1.Controls.Add(this.rdoDvsn0);
            this.panel1.Controls.Add(this.rdoDvsnAll);
            this.panel1.Controls.Add(this.btnTest);
            this.panel1.Controls.Add(this.btnCancel);
            this.panel1.Controls.Add(this.btnSrch);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1181, 53);
            this.panel1.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(18, 19);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(41, 12);
            this.label1.TabIndex = 3;
            this.label1.Text = "구분 >";
            // 
            // rdoDvsn1
            // 
            this.rdoDvsn1.AutoSize = true;
            this.rdoDvsn1.Location = new System.Drawing.Point(215, 17);
            this.rdoDvsn1.Name = "rdoDvsn1";
            this.rdoDvsn1.Size = new System.Drawing.Size(71, 16);
            this.rdoDvsn1.TabIndex = 2;
            this.rdoDvsn1.Text = "경매개시";
            this.rdoDvsn1.UseVisualStyleBackColor = true;
            // 
            // rdoDvsn0
            // 
            this.rdoDvsn0.AutoSize = true;
            this.rdoDvsn0.Location = new System.Drawing.Point(128, 17);
            this.rdoDvsn0.Name = "rdoDvsn0";
            this.rdoDvsn0.Size = new System.Drawing.Size(71, 16);
            this.rdoDvsn0.TabIndex = 2;
            this.rdoDvsn0.Text = "상태변경";
            this.rdoDvsn0.UseVisualStyleBackColor = true;
            // 
            // rdoDvsnAll
            // 
            this.rdoDvsnAll.AutoSize = true;
            this.rdoDvsnAll.Checked = true;
            this.rdoDvsnAll.Location = new System.Drawing.Point(65, 17);
            this.rdoDvsnAll.Name = "rdoDvsnAll";
            this.rdoDvsnAll.Size = new System.Drawing.Size(47, 16);
            this.rdoDvsnAll.TabIndex = 2;
            this.rdoDvsnAll.TabStop = true;
            this.rdoDvsnAll.Text = "전체";
            this.rdoDvsnAll.UseVisualStyleBackColor = true;
            // 
            // btnTest
            // 
            this.btnTest.Location = new System.Drawing.Point(914, 15);
            this.btnTest.Name = "btnTest";
            this.btnTest.Size = new System.Drawing.Size(75, 23);
            this.btnTest.TabIndex = 0;
            this.btnTest.Text = "Test";
            this.btnTest.UseVisualStyleBackColor = true;
            this.btnTest.Click += new System.EventHandler(this.btnTest_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.Location = new System.Drawing.Point(1093, 15);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "발송취소";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // wbr
            // 
            this.wbr.Dock = System.Windows.Forms.DockStyle.Fill;
            this.wbr.Location = new System.Drawing.Point(0, 0);
            this.wbr.MinimumSize = new System.Drawing.Size(20, 20);
            this.wbr.Name = "wbr";
            this.wbr.Size = new System.Drawing.Size(691, 1007);
            this.wbr.TabIndex = 0;
            // 
            // wfAuctSms
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1884, 1011);
            this.Controls.Add(this.splitContainer1);
            this.Name = "wfAuctSms";
            this.Text = "경매-상태변경 문자발송";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dg)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnSrch;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button btnTest;
        private System.Windows.Forms.DataGridView dg;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.WebBrowser wbr;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.RadioButton rdoDvsn1;
        private System.Windows.Forms.RadioButton rdoDvsn0;
        private System.Windows.Forms.RadioButton rdoDvsnAll;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_No;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_Dvsn;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_Tm;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_StaMsg;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_Send;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_Tid;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_CS;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_Dpt;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_SN;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_BidDt;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_State;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_Cat;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_FbCnt;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_ApslAmt;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_MinbAmt;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_Url;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_Idx;
    }
}