
namespace Solar.CA
{
    partial class sfNewExtract
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
            this.chkLand = new System.Windows.Forms.CheckBox();
            this.chkBldg = new System.Windows.Forms.CheckBox();
            this.chkEtc = new System.Windows.Forms.CheckBox();
            this.chkCar = new System.Windows.Forms.CheckBox();
            this.rdoNew = new System.Windows.Forms.RadioButton();
            this.rdoUdt = new System.Windows.Forms.RadioButton();
            this.btnExtract = new System.Windows.Forms.Button();
            this.txtTid = new System.Windows.Forms.TextBox();
            this.chkLs = new System.Windows.Forms.CheckBox();
            this.chkShip = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // chkLand
            // 
            this.chkLand.AutoSize = true;
            this.chkLand.Location = new System.Drawing.Point(30, 74);
            this.chkLand.Name = "chkLand";
            this.chkLand.Size = new System.Drawing.Size(72, 16);
            this.chkLand.TabIndex = 0;
            this.chkLand.Text = "토지현황";
            this.chkLand.UseVisualStyleBackColor = true;
            // 
            // chkBldg
            // 
            this.chkBldg.AutoSize = true;
            this.chkBldg.Location = new System.Drawing.Point(126, 74);
            this.chkBldg.Name = "chkBldg";
            this.chkBldg.Size = new System.Drawing.Size(72, 16);
            this.chkBldg.TabIndex = 1;
            this.chkBldg.Text = "건물현황";
            this.chkBldg.UseVisualStyleBackColor = true;
            // 
            // chkEtc
            // 
            this.chkEtc.AutoSize = true;
            this.chkEtc.Location = new System.Drawing.Point(222, 74);
            this.chkEtc.Name = "chkEtc";
            this.chkEtc.Size = new System.Drawing.Size(124, 16);
            this.chkEtc.TabIndex = 2;
            this.chkEtc.Text = "제시외(기계/기구)";
            this.chkEtc.UseVisualStyleBackColor = true;
            // 
            // chkCar
            // 
            this.chkCar.AutoSize = true;
            this.chkCar.Enabled = false;
            this.chkCar.Location = new System.Drawing.Point(367, 74);
            this.chkCar.Name = "chkCar";
            this.chkCar.Size = new System.Drawing.Size(78, 16);
            this.chkCar.TabIndex = 3;
            this.chkCar.Text = "차량/중기";
            this.chkCar.UseVisualStyleBackColor = true;
            // 
            // rdoNew
            // 
            this.rdoNew.AutoSize = true;
            this.rdoNew.Checked = true;
            this.rdoNew.Location = new System.Drawing.Point(30, 148);
            this.rdoNew.Name = "rdoNew";
            this.rdoNew.Size = new System.Drawing.Size(153, 16);
            this.rdoNew.TabIndex = 4;
            this.rdoNew.TabStop = true;
            this.rdoNew.Text = "새로등록(기존내용삭제)";
            this.rdoNew.UseVisualStyleBackColor = true;
            // 
            // rdoUdt
            // 
            this.rdoUdt.AutoSize = true;
            this.rdoUdt.Enabled = false;
            this.rdoUdt.Location = new System.Drawing.Point(221, 148);
            this.rdoUdt.Name = "rdoUdt";
            this.rdoUdt.Size = new System.Drawing.Size(71, 16);
            this.rdoUdt.TabIndex = 4;
            this.rdoUdt.Text = "업데이트";
            this.rdoUdt.UseVisualStyleBackColor = true;
            // 
            // btnExtract
            // 
            this.btnExtract.Location = new System.Drawing.Point(255, 194);
            this.btnExtract.Name = "btnExtract";
            this.btnExtract.Size = new System.Drawing.Size(75, 23);
            this.btnExtract.TabIndex = 5;
            this.btnExtract.Text = "추출하기";
            this.btnExtract.UseVisualStyleBackColor = true;
            this.btnExtract.Click += new System.EventHandler(this.btnExtract_Click);
            // 
            // txtTid
            // 
            this.txtTid.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.txtTid.Location = new System.Drawing.Point(472, 12);
            this.txtTid.Name = "txtTid";
            this.txtTid.ReadOnly = true;
            this.txtTid.Size = new System.Drawing.Size(100, 22);
            this.txtTid.TabIndex = 6;
            this.txtTid.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // chkLs
            // 
            this.chkLs.AutoSize = true;
            this.chkLs.Location = new System.Drawing.Point(30, 34);
            this.chkLs.Name = "chkLs";
            this.chkLs.Size = new System.Drawing.Size(72, 16);
            this.chkLs.TabIndex = 7;
            this.chkLs.Text = "목록내역";
            this.chkLs.UseVisualStyleBackColor = true;
            // 
            // chkShip
            // 
            this.chkShip.AutoSize = true;
            this.chkShip.Enabled = false;
            this.chkShip.Location = new System.Drawing.Point(469, 74);
            this.chkShip.Name = "chkShip";
            this.chkShip.Size = new System.Drawing.Size(48, 16);
            this.chkShip.TabIndex = 3;
            this.chkShip.Text = "선박";
            this.chkShip.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(442, 18);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(24, 12);
            this.label1.TabIndex = 8;
            this.label1.Text = "TID";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.ForeColor = System.Drawing.Color.Firebrick;
            this.label2.Location = new System.Drawing.Point(179, 230);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(239, 12);
            this.label2.TabIndex = 9;
            this.label2.Text = "(주의!!! 기존 저장된 내용들이 삭제됩니다.)";
            // 
            // sfNewExtract
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 261);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.chkLs);
            this.Controls.Add(this.txtTid);
            this.Controls.Add(this.btnExtract);
            this.Controls.Add(this.rdoUdt);
            this.Controls.Add(this.rdoNew);
            this.Controls.Add(this.chkShip);
            this.Controls.Add(this.chkCar);
            this.Controls.Add(this.chkEtc);
            this.Controls.Add(this.chkBldg);
            this.Controls.Add(this.chkLand);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "sfNewExtract";
            this.Text = "물건내용 새로추출";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox chkLand;
        private System.Windows.Forms.CheckBox chkBldg;
        private System.Windows.Forms.CheckBox chkEtc;
        private System.Windows.Forms.CheckBox chkCar;
        private System.Windows.Forms.RadioButton rdoNew;
        private System.Windows.Forms.RadioButton rdoUdt;
        private System.Windows.Forms.Button btnExtract;
        public System.Windows.Forms.TextBox txtTid;
        private System.Windows.Forms.CheckBox chkLs;
        private System.Windows.Forms.CheckBox chkShip;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
    }
}