
namespace Solar.CA
{
    partial class sfStateCopy
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
            this.rdo_dgL = new System.Windows.Forms.RadioButton();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.txtSrcNo = new System.Windows.Forms.TextBox();
            this.txtTgtNoS = new System.Windows.Forms.TextBox();
            this.txtTgtNoE = new System.Windows.Forms.TextBox();
            this.btnApply = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.rdo_dgB = new System.Windows.Forms.RadioButton();
            this.rdo_dgE = new System.Windows.Forms.RadioButton();
            this.rdo_dgT = new System.Windows.Forms.RadioButton();
            this.SuspendLayout();
            // 
            // rdo_dgL
            // 
            this.rdo_dgL.AutoSize = true;
            this.rdo_dgL.Location = new System.Drawing.Point(115, 12);
            this.rdo_dgL.Name = "rdo_dgL";
            this.rdo_dgL.Size = new System.Drawing.Size(47, 16);
            this.rdo_dgL.TabIndex = 0;
            this.rdo_dgL.TabStop = true;
            this.rdo_dgL.Text = "토지";
            this.rdo_dgL.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(35, 14);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(69, 12);
            this.label1.TabIndex = 1;
            this.label1.Text = "현황 구분 >";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(19, 47);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(85, 12);
            this.label2.TabIndex = 2;
            this.label2.Text = "복사 원본No >";
            // 
            // txtSrcNo
            // 
            this.txtSrcNo.Location = new System.Drawing.Point(115, 44);
            this.txtSrcNo.Name = "txtSrcNo";
            this.txtSrcNo.Size = new System.Drawing.Size(40, 21);
            this.txtSrcNo.TabIndex = 3;
            this.txtSrcNo.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // txtTgtNoS
            // 
            this.txtTgtNoS.Location = new System.Drawing.Point(268, 44);
            this.txtTgtNoS.Name = "txtTgtNoS";
            this.txtTgtNoS.Size = new System.Drawing.Size(40, 21);
            this.txtTgtNoS.TabIndex = 3;
            this.txtTgtNoS.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // txtTgtNoE
            // 
            this.txtTgtNoE.Location = new System.Drawing.Point(335, 44);
            this.txtTgtNoE.Name = "txtTgtNoE";
            this.txtTgtNoE.Size = new System.Drawing.Size(40, 21);
            this.txtTgtNoE.TabIndex = 3;
            this.txtTgtNoE.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // btnApply
            // 
            this.btnApply.Location = new System.Drawing.Point(168, 85);
            this.btnApply.Name = "btnApply";
            this.btnApply.Size = new System.Drawing.Size(75, 23);
            this.btnApply.TabIndex = 4;
            this.btnApply.Text = "적용";
            this.btnApply.UseVisualStyleBackColor = true;
            this.btnApply.Click += new System.EventHandler(this.btnApply_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(202, 48);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(57, 12);
            this.label3.TabIndex = 2;
            this.label3.Text = "대상No >";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(315, 48);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(14, 12);
            this.label4.TabIndex = 5;
            this.label4.Text = "~";
            // 
            // rdo_dgB
            // 
            this.rdo_dgB.AutoSize = true;
            this.rdo_dgB.Location = new System.Drawing.Point(178, 12);
            this.rdo_dgB.Name = "rdo_dgB";
            this.rdo_dgB.Size = new System.Drawing.Size(47, 16);
            this.rdo_dgB.TabIndex = 0;
            this.rdo_dgB.TabStop = true;
            this.rdo_dgB.Text = "건물";
            this.rdo_dgB.UseVisualStyleBackColor = true;
            // 
            // rdo_dgE
            // 
            this.rdo_dgE.AutoSize = true;
            this.rdo_dgE.Location = new System.Drawing.Point(241, 12);
            this.rdo_dgE.Name = "rdo_dgE";
            this.rdo_dgE.Size = new System.Drawing.Size(59, 16);
            this.rdo_dgE.TabIndex = 0;
            this.rdo_dgE.TabStop = true;
            this.rdo_dgE.Text = "제시외";
            this.rdo_dgE.UseVisualStyleBackColor = true;
            // 
            // rdo_dgT
            // 
            this.rdo_dgT.AutoSize = true;
            this.rdo_dgT.Location = new System.Drawing.Point(316, 12);
            this.rdo_dgT.Name = "rdo_dgT";
            this.rdo_dgT.Size = new System.Drawing.Size(59, 16);
            this.rdo_dgT.TabIndex = 0;
            this.rdo_dgT.TabStop = true;
            this.rdo_dgT.Text = "임차인";
            this.rdo_dgT.UseVisualStyleBackColor = true;
            // 
            // sfStateCopy
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(399, 120);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnApply);
            this.Controls.Add(this.txtTgtNoE);
            this.Controls.Add(this.txtTgtNoS);
            this.Controls.Add(this.txtSrcNo);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.rdo_dgT);
            this.Controls.Add(this.rdo_dgE);
            this.Controls.Add(this.rdo_dgB);
            this.Controls.Add(this.rdo_dgL);
            this.Name = "sfStateCopy";
            this.Text = "경매-현황복사";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.RadioButton rdo_dgL;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtSrcNo;
        private System.Windows.Forms.TextBox txtTgtNoS;
        private System.Windows.Forms.TextBox txtTgtNoE;
        private System.Windows.Forms.Button btnApply;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.RadioButton rdo_dgB;
        private System.Windows.Forms.RadioButton rdo_dgE;
        private System.Windows.Forms.RadioButton rdo_dgT;
    }
}