namespace Atom
{
    partial class fDeploy
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
            this.dg = new System.Windows.Forms.DataGridView();
            this.dg_No = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_VM = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.db_Rslt = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnDeploy = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.txtFilePath = new System.Windows.Forms.TextBox();
            this.nudVmBgn = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.nudVmEnd = new System.Windows.Forms.NumericUpDown();
            this.label3 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.dg)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudVmBgn)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudVmEnd)).BeginInit();
            this.SuspendLayout();
            // 
            // dg
            // 
            this.dg.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dg.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.dg_No,
            this.dg_VM,
            this.db_Rslt});
            this.dg.Dock = System.Windows.Forms.DockStyle.Left;
            this.dg.Location = new System.Drawing.Point(0, 0);
            this.dg.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.dg.Name = "dg";
            this.dg.RowHeadersWidth = 51;
            this.dg.RowTemplate.Height = 27;
            this.dg.Size = new System.Drawing.Size(423, 561);
            this.dg.TabIndex = 0;
            // 
            // dg_No
            // 
            this.dg_No.HeaderText = "No";
            this.dg_No.MinimumWidth = 6;
            this.dg_No.Name = "dg_No";
            this.dg_No.Width = 125;
            // 
            // dg_VM
            // 
            this.dg_VM.HeaderText = "VM";
            this.dg_VM.MinimumWidth = 6;
            this.dg_VM.Name = "dg_VM";
            this.dg_VM.Width = 125;
            // 
            // db_Rslt
            // 
            this.db_Rslt.HeaderText = "결과";
            this.db_Rslt.MinimumWidth = 6;
            this.db_Rslt.Name = "db_Rslt";
            this.db_Rslt.Width = 125;
            // 
            // btnDeploy
            // 
            this.btnDeploy.Location = new System.Drawing.Point(699, 110);
            this.btnDeploy.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnDeploy.Name = "btnDeploy";
            this.btnDeploy.Size = new System.Drawing.Size(75, 23);
            this.btnDeploy.TabIndex = 1;
            this.btnDeploy.Text = "Atom배포";
            this.btnDeploy.UseVisualStyleBackColor = true;
            this.btnDeploy.Click += new System.EventHandler(this.btnDeploy_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(438, 69);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(53, 12);
            this.label1.TabIndex = 2;
            this.label1.Text = "파일위치";
            // 
            // txtFilePath
            // 
            this.txtFilePath.Location = new System.Drawing.Point(502, 65);
            this.txtFilePath.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.txtFilePath.Name = "txtFilePath";
            this.txtFilePath.Size = new System.Drawing.Size(272, 21);
            this.txtFilePath.TabIndex = 3;
            this.txtFilePath.Text = "C:\\Atom\\Atom.exe";
            // 
            // nudVmBgn
            // 
            this.nudVmBgn.Location = new System.Drawing.Point(502, 110);
            this.nudVmBgn.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.nudVmBgn.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.nudVmBgn.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.nudVmBgn.Name = "nudVmBgn";
            this.nudVmBgn.Size = new System.Drawing.Size(46, 21);
            this.nudVmBgn.TabIndex = 4;
            this.nudVmBgn.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.nudVmBgn.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(557, 114);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(14, 12);
            this.label2.TabIndex = 5;
            this.label2.Text = "~";
            // 
            // nudVmEnd
            // 
            this.nudVmEnd.Location = new System.Drawing.Point(582, 110);
            this.nudVmEnd.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.nudVmEnd.Maximum = new decimal(new int[] {
            12,
            0,
            0,
            0});
            this.nudVmEnd.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.nudVmEnd.Name = "nudVmEnd";
            this.nudVmEnd.Size = new System.Drawing.Size(46, 21);
            this.nudVmEnd.TabIndex = 4;
            this.nudVmEnd.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.nudVmEnd.Value = new decimal(new int[] {
            12,
            0,
            0,
            0});
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(441, 114);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(46, 12);
            this.label3.TabIndex = 6;
            this.label3.Text = "VM-No";
            // 
            // fDeploy
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 561);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.nudVmEnd);
            this.Controls.Add(this.nudVmBgn);
            this.Controls.Add(this.txtFilePath);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnDeploy);
            this.Controls.Add(this.dg);
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.Name = "fDeploy";
            this.Text = "fDeploy";
            ((System.ComponentModel.ISupportInitialize)(this.dg)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudVmBgn)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudVmEnd)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView dg;
        private System.Windows.Forms.Button btnDeploy;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_No;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_VM;
        private System.Windows.Forms.DataGridViewTextBoxColumn db_Rslt;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtFilePath;
        private System.Windows.Forms.NumericUpDown nudVmBgn;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NumericUpDown nudVmEnd;
        private System.Windows.Forms.Label label3;
    }
}