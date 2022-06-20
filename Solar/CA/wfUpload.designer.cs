
namespace Solar.CA
{
    partial class wfUpload
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
            this.dgU = new System.Windows.Forms.DataGridView();
            this.panel3 = new System.Windows.Forms.Panel();
            this.btnUpLoad = new System.Windows.Forms.Button();
            this.btnOpenFiles = new System.Windows.Forms.Button();
            this.dgU_No = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgU_LocFile = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgU_Ctgr = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgU_RmtFile = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgU_RmtSize = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgU_Rslt = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.dgU)).BeginInit();
            this.panel3.SuspendLayout();
            this.SuspendLayout();
            // 
            // dgU
            // 
            this.dgU.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgU.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.dgU_No,
            this.dgU_LocFile,
            this.dgU_Ctgr,
            this.dgU_RmtFile,
            this.dgU_RmtSize,
            this.dgU_Rslt});
            this.dgU.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgU.Location = new System.Drawing.Point(0, 56);
            this.dgU.Name = "dgU";
            this.dgU.RowTemplate.Height = 23;
            this.dgU.Size = new System.Drawing.Size(1884, 955);
            this.dgU.TabIndex = 3;
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.btnUpLoad);
            this.panel3.Controls.Add(this.btnOpenFiles);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel3.Location = new System.Drawing.Point(0, 0);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(1884, 56);
            this.panel3.TabIndex = 2;
            // 
            // btnUpLoad
            // 
            this.btnUpLoad.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnUpLoad.BackColor = System.Drawing.Color.LightGreen;
            this.btnUpLoad.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btnUpLoad.Location = new System.Drawing.Point(1792, 16);
            this.btnUpLoad.Name = "btnUpLoad";
            this.btnUpLoad.Size = new System.Drawing.Size(75, 23);
            this.btnUpLoad.TabIndex = 1;
            this.btnUpLoad.Text = "업로드";
            this.btnUpLoad.UseVisualStyleBackColor = false;
            this.btnUpLoad.Click += new System.EventHandler(this.btnUpLoad_Click);
            // 
            // btnOpenFiles
            // 
            this.btnOpenFiles.Location = new System.Drawing.Point(15, 17);
            this.btnOpenFiles.Name = "btnOpenFiles";
            this.btnOpenFiles.Size = new System.Drawing.Size(75, 23);
            this.btnOpenFiles.TabIndex = 0;
            this.btnOpenFiles.Text = "파일찾기";
            this.btnOpenFiles.UseVisualStyleBackColor = true;
            this.btnOpenFiles.Click += new System.EventHandler(this.btnOpenFiles_Click);
            // 
            // dgU_No
            // 
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dgU_No.DefaultCellStyle = dataGridViewCellStyle1;
            this.dgU_No.HeaderText = "No";
            this.dgU_No.Name = "dgU_No";
            this.dgU_No.Width = 50;
            // 
            // dgU_LocFile
            // 
            this.dgU_LocFile.HeaderText = "파일명-로컬";
            this.dgU_LocFile.Name = "dgU_LocFile";
            this.dgU_LocFile.Width = 300;
            // 
            // dgU_Ctgr
            // 
            this.dgU_Ctgr.HeaderText = "종류";
            this.dgU_Ctgr.Name = "dgU_Ctgr";
            // 
            // dgU_RmtFile
            // 
            this.dgU_RmtFile.HeaderText = "파일명-원격";
            this.dgU_RmtFile.Name = "dgU_RmtFile";
            this.dgU_RmtFile.Width = 300;
            // 
            // dgU_RmtSize
            // 
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.dgU_RmtSize.DefaultCellStyle = dataGridViewCellStyle2;
            this.dgU_RmtSize.HeaderText = "원격크기";
            this.dgU_RmtSize.Name = "dgU_RmtSize";
            // 
            // dgU_Rslt
            // 
            this.dgU_Rslt.HeaderText = "결과";
            this.dgU_Rslt.Name = "dgU_Rslt";
            // 
            // wfUpload
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1884, 1011);
            this.Controls.Add(this.dgU);
            this.Controls.Add(this.panel3);
            this.Name = "wfUpload";
            this.Text = "wfUpload";
            ((System.ComponentModel.ISupportInitialize)(this.dgU)).EndInit();
            this.panel3.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dgU;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Button btnUpLoad;
        private System.Windows.Forms.Button btnOpenFiles;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgU_No;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgU_LocFile;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgU_Ctgr;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgU_RmtFile;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgU_RmtSize;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgU_Rslt;
    }
}