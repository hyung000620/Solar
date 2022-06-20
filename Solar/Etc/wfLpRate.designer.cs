
namespace Solar.Etc
{
    partial class wfLpRate
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
            this.btnCsvFind = new System.Windows.Forms.Button();
            this.txtCsvFile = new System.Windows.Forms.TextBox();
            this.btnCsvProc = new System.Windows.Forms.Button();
            this.txtCsvYear = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // btnCsvFind
            // 
            this.btnCsvFind.Location = new System.Drawing.Point(512, 12);
            this.btnCsvFind.Name = "btnCsvFind";
            this.btnCsvFind.Size = new System.Drawing.Size(99, 23);
            this.btnCsvFind.TabIndex = 0;
            this.btnCsvFind.Text = "파일(csv)찾기";
            this.btnCsvFind.UseVisualStyleBackColor = true;
            this.btnCsvFind.Click += new System.EventHandler(this.btnCsvFind_Click);
            // 
            // txtCsvFile
            // 
            this.txtCsvFile.Location = new System.Drawing.Point(141, 14);
            this.txtCsvFile.Name = "txtCsvFile";
            this.txtCsvFile.Size = new System.Drawing.Size(354, 21);
            this.txtCsvFile.TabIndex = 1;
            // 
            // btnCsvProc
            // 
            this.btnCsvProc.Enabled = false;
            this.btnCsvProc.Location = new System.Drawing.Point(288, 61);
            this.btnCsvProc.Name = "btnCsvProc";
            this.btnCsvProc.Size = new System.Drawing.Size(75, 23);
            this.btnCsvProc.TabIndex = 2;
            this.btnCsvProc.Text = "처리시작";
            this.btnCsvProc.UseVisualStyleBackColor = true;
            this.btnCsvProc.Click += new System.EventHandler(this.btnCsvProc_Click);
            // 
            // txtCsvYear
            // 
            this.txtCsvYear.Location = new System.Drawing.Point(198, 62);
            this.txtCsvYear.Name = "txtCsvYear";
            this.txtCsvYear.Size = new System.Drawing.Size(75, 21);
            this.txtCsvYear.TabIndex = 3;
            this.txtCsvYear.Text = "2021";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(139, 66);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(53, 12);
            this.label1.TabIndex = 4;
            this.label1.Text = "시작연도";
            // 
            // wfLpRate
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtCsvYear);
            this.Controls.Add(this.btnCsvProc);
            this.Controls.Add(this.txtCsvFile);
            this.Controls.Add(this.btnCsvFind);
            this.Name = "wfLpRate";
            this.Text = "wfLpRate";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCsvFind;
        private System.Windows.Forms.TextBox txtCsvFile;
        private System.Windows.Forms.Button btnCsvProc;
        private System.Windows.Forms.TextBox txtCsvYear;
        private System.Windows.Forms.Label label1;
    }
}