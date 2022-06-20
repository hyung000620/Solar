
namespace Solar.Comn
{
    partial class sfSrchAdrsCd
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle11 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle12 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle13 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle14 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle15 = new System.Windows.Forms.DataGridViewCellStyle();
            this.panel1 = new System.Windows.Forms.Panel();
            this.btnSrch = new System.Windows.Forms.Button();
            this.txtSrchAdrs = new System.Windows.Forms.TextBox();
            this.dg = new System.Windows.Forms.DataGridView();
            this.label1 = new System.Windows.Forms.Label();
            this.dgNo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgRetAdrs = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgSiNm = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgGuNm = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgDnNm = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgRiNm = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgSiCd = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgGuCd = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgDnCd = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgRiCd = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dg)).BeginInit();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.btnSrch);
            this.panel1.Controls.Add(this.txtSrchAdrs);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(784, 44);
            this.panel1.TabIndex = 0;
            // 
            // btnSrch
            // 
            this.btnSrch.Location = new System.Drawing.Point(695, 9);
            this.btnSrch.Name = "btnSrch";
            this.btnSrch.Size = new System.Drawing.Size(75, 23);
            this.btnSrch.TabIndex = 1;
            this.btnSrch.Text = "검색";
            this.btnSrch.UseVisualStyleBackColor = true;
            this.btnSrch.Click += new System.EventHandler(this.btnSrch_Click);
            // 
            // txtSrchAdrs
            // 
            this.txtSrchAdrs.ImeMode = System.Windows.Forms.ImeMode.Hangul;
            this.txtSrchAdrs.Location = new System.Drawing.Point(487, 10);
            this.txtSrchAdrs.Name = "txtSrchAdrs";
            this.txtSrchAdrs.Size = new System.Drawing.Size(202, 21);
            this.txtSrchAdrs.TabIndex = 0;
            this.txtSrchAdrs.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtSchAddr_KeyPress);
            // 
            // dg
            // 
            this.dg.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dg.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.dgNo,
            this.dgRetAdrs,
            this.dgSiNm,
            this.dgGuNm,
            this.dgDnNm,
            this.dgRiNm,
            this.dgSiCd,
            this.dgGuCd,
            this.dgDnCd,
            this.dgRiCd});
            this.dg.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dg.Location = new System.Drawing.Point(0, 44);
            this.dg.Name = "dg";
            this.dg.RowTemplate.Height = 23;
            this.dg.Size = new System.Drawing.Size(784, 517);
            this.dg.TabIndex = 1;
            this.dg.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dg_CellClick);
            this.dg.CellMouseLeave += new System.Windows.Forms.DataGridViewCellEventHandler(this.dg_CellMouseLeave);
            this.dg.CellMouseMove += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dg_CellMouseMove);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(388, 15);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(95, 12);
            this.label1.TabIndex = 2;
            this.label1.Text = "* 검색 할 주소 >";
            // 
            // dgNo
            // 
            dataGridViewCellStyle11.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dgNo.DefaultCellStyle = dataGridViewCellStyle11;
            this.dgNo.HeaderText = "No";
            this.dgNo.Name = "dgNo";
            this.dgNo.Width = 60;
            // 
            // dgRetAdrs
            // 
            this.dgRetAdrs.HeaderText = "주소";
            this.dgRetAdrs.Name = "dgRetAdrs";
            this.dgRetAdrs.Width = 250;
            // 
            // dgSiNm
            // 
            this.dgSiNm.HeaderText = "시/도";
            this.dgSiNm.Name = "dgSiNm";
            // 
            // dgGuNm
            // 
            this.dgGuNm.HeaderText = "시/구/군";
            this.dgGuNm.Name = "dgGuNm";
            // 
            // dgDnNm
            // 
            this.dgDnNm.HeaderText = "읍/면/동";
            this.dgDnNm.Name = "dgDnNm";
            // 
            // dgRiNm
            // 
            this.dgRiNm.HeaderText = "리";
            this.dgRiNm.Name = "dgRiNm";
            // 
            // dgSiCd
            // 
            dataGridViewCellStyle12.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dgSiCd.DefaultCellStyle = dataGridViewCellStyle12;
            this.dgSiCd.HeaderText = "SiCd";
            this.dgSiCd.Name = "dgSiCd";
            this.dgSiCd.Width = 50;
            // 
            // dgGuCd
            // 
            dataGridViewCellStyle13.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dgGuCd.DefaultCellStyle = dataGridViewCellStyle13;
            this.dgGuCd.HeaderText = "GuCd";
            this.dgGuCd.Name = "dgGuCd";
            this.dgGuCd.Width = 50;
            // 
            // dgDnCd
            // 
            dataGridViewCellStyle14.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dgDnCd.DefaultCellStyle = dataGridViewCellStyle14;
            this.dgDnCd.HeaderText = "DnCd";
            this.dgDnCd.Name = "dgDnCd";
            this.dgDnCd.Width = 50;
            // 
            // dgRiCd
            // 
            dataGridViewCellStyle15.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dgRiCd.DefaultCellStyle = dataGridViewCellStyle15;
            this.dgRiCd.HeaderText = "RiCd";
            this.dgRiCd.Name = "dgRiCd";
            this.dgRiCd.Width = 50;
            // 
            // sfSrchAdrsCd
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 561);
            this.Controls.Add(this.dg);
            this.Controls.Add(this.panel1);
            this.Name = "sfSrchAdrsCd";
            this.Text = "주소코드 찾기";
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dg)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.DataGridView dg;
        private System.Windows.Forms.Button btnSrch;
        private System.Windows.Forms.TextBox txtSrchAdrs;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgNo;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgRetAdrs;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgSiNm;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgGuNm;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgDnNm;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgRiNm;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgSiCd;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgGuCd;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgDnCd;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgRiCd;
    }
}