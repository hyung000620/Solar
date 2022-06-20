
namespace Solar.CA
{
    partial class sfAptCd
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
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel34 = new System.Windows.Forms.Panel();
            this.btnSrch = new System.Windows.Forms.Button();
            this.cbxDn = new System.Windows.Forms.ComboBox();
            this.cbxGu = new System.Windows.Forms.ComboBox();
            this.cbxSi = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.dg = new System.Windows.Forms.DataGridView();
            this.dg_No = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_AptCd = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_Nm = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_Cat = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_Adrs = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_Dvsn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panel1.SuspendLayout();
            this.panel34.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dg)).BeginInit();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.Teal;
            this.panel1.Controls.Add(this.panel34);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(784, 40);
            this.panel1.TabIndex = 0;
            // 
            // panel34
            // 
            this.panel34.Controls.Add(this.btnSrch);
            this.panel34.Controls.Add(this.cbxDn);
            this.panel34.Controls.Add(this.cbxGu);
            this.panel34.Controls.Add(this.cbxSi);
            this.panel34.Dock = System.Windows.Forms.DockStyle.Right;
            this.panel34.Location = new System.Drawing.Point(360, 0);
            this.panel34.Margin = new System.Windows.Forms.Padding(0);
            this.panel34.Name = "panel34";
            this.panel34.Size = new System.Drawing.Size(424, 40);
            this.panel34.TabIndex = 31;
            // 
            // btnSrch
            // 
            this.btnSrch.BackColor = System.Drawing.Color.SandyBrown;
            this.btnSrch.Location = new System.Drawing.Point(337, 10);
            this.btnSrch.Name = "btnSrch";
            this.btnSrch.Size = new System.Drawing.Size(75, 23);
            this.btnSrch.TabIndex = 1;
            this.btnSrch.Text = "검 색";
            this.btnSrch.UseVisualStyleBackColor = false;
            this.btnSrch.Click += new System.EventHandler(this.btnSrch_Click);
            // 
            // cbxDn
            // 
            this.cbxDn.FormattingEnabled = true;
            this.cbxDn.Location = new System.Drawing.Point(215, 12);
            this.cbxDn.Name = "cbxDn";
            this.cbxDn.Size = new System.Drawing.Size(100, 20);
            this.cbxDn.TabIndex = 0;
            this.cbxDn.Text = "-읍/면/동-";
            // 
            // cbxGu
            // 
            this.cbxGu.FormattingEnabled = true;
            this.cbxGu.Location = new System.Drawing.Point(109, 12);
            this.cbxGu.Name = "cbxGu";
            this.cbxGu.Size = new System.Drawing.Size(100, 20);
            this.cbxGu.TabIndex = 0;
            this.cbxGu.Text = "-시/구/군-";
            // 
            // cbxSi
            // 
            this.cbxSi.FormattingEnabled = true;
            this.cbxSi.Location = new System.Drawing.Point(3, 12);
            this.cbxSi.Name = "cbxSi";
            this.cbxSi.Size = new System.Drawing.Size(100, 20);
            this.cbxSi.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(170, 16);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(181, 12);
            this.label1.TabIndex = 0;
            this.label1.Text = "▣ 소재지를 선택 해 주세요 >>>";
            // 
            // dg
            // 
            this.dg.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dg.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.dg_No,
            this.dg_AptCd,
            this.dg_Nm,
            this.dg_Cat,
            this.dg_Adrs,
            this.dg_Dvsn});
            this.dg.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dg.Location = new System.Drawing.Point(0, 40);
            this.dg.Name = "dg";
            this.dg.RowTemplate.Height = 23;
            this.dg.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dg.Size = new System.Drawing.Size(784, 521);
            this.dg.TabIndex = 1;
            this.dg.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dg_CellClick);
            this.dg.CellMouseLeave += new System.Windows.Forms.DataGridViewCellEventHandler(this.dg_CellMouseLeave);
            this.dg.CellMouseMove += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dg_CellMouseMove);
            // 
            // dg_No
            // 
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dg_No.DefaultCellStyle = dataGridViewCellStyle1;
            this.dg_No.HeaderText = "No";
            this.dg_No.Name = "dg_No";
            this.dg_No.Width = 60;
            // 
            // dg_AptCd
            // 
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("굴림", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.dg_AptCd.DefaultCellStyle = dataGridViewCellStyle2;
            this.dg_AptCd.HeaderText = "코드";
            this.dg_AptCd.Name = "dg_AptCd";
            this.dg_AptCd.Width = 70;
            // 
            // dg_Nm
            // 
            this.dg_Nm.HeaderText = "단지명";
            this.dg_Nm.Name = "dg_Nm";
            this.dg_Nm.Width = 150;
            // 
            // dg_Cat
            // 
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dg_Cat.DefaultCellStyle = dataGridViewCellStyle3;
            this.dg_Cat.HeaderText = "종류";
            this.dg_Cat.Name = "dg_Cat";
            // 
            // dg_Adrs
            // 
            this.dg_Adrs.HeaderText = "주소";
            this.dg_Adrs.Name = "dg_Adrs";
            this.dg_Adrs.Width = 230;
            // 
            // dg_Dvsn
            // 
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dg_Dvsn.DefaultCellStyle = dataGridViewCellStyle4;
            this.dg_Dvsn.HeaderText = "구분";
            this.dg_Dvsn.Name = "dg_Dvsn";
            // 
            // sfAptCd
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 561);
            this.Controls.Add(this.dg);
            this.Controls.Add(this.panel1);
            this.Name = "sfAptCd";
            this.Text = "[집합건물] 코드 찾기";
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel34.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dg)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.DataGridView dg;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Panel panel34;
        private System.Windows.Forms.Button btnSrch;
        private System.Windows.Forms.ComboBox cbxDn;
        private System.Windows.Forms.ComboBox cbxGu;
        private System.Windows.Forms.ComboBox cbxSi;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_No;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_AptCd;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_Nm;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_Cat;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_Adrs;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_Dvsn;
    }
}