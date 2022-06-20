namespace Solar.Comn
{
    partial class wfRailRoad
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.dg = new System.Windows.Forms.DataGridView();
            this.panel1 = new System.Windows.Forms.Panel();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.cbxSrchLine = new System.Windows.Forms.ComboBox();
            this.cbxSrchLocal = new System.Windows.Forms.ComboBox();
            this.btnSrch = new System.Windows.Forms.Button();
            this.wbr = new System.Windows.Forms.WebBrowser();
            this.panDtl = new System.Windows.Forms.Panel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.btnMatch = new System.Windows.Forms.Button();
            this.btnMatchDel = new System.Windows.Forms.Button();
            this.btnMatchAdd = new System.Windows.Forms.Button();
            this.dgR = new System.Windows.Forms.DataGridView();
            this.dgR_StationNm = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgR_Idx = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.label8 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.btnNew = new System.Windows.Forms.Button();
            this.label6 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.btnSave = new System.Windows.Forms.Button();
            this.txtCoordY = new System.Windows.Forms.TextBox();
            this.txtCoordX = new System.Windows.Forms.TextBox();
            this.txtStationCd = new System.Windows.Forms.TextBox();
            this.txtIdx = new System.Windows.Forms.TextBox();
            this.txtStationNm = new System.Windows.Forms.TextBox();
            this.cbxLine = new System.Windows.Forms.ComboBox();
            this.cbxLocal = new System.Windows.Forms.ComboBox();
            this.dg_No = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_LocalNm = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_LineNm = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_StationNm = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_LocalCd = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_LineCd = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_StationCd = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_X = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_Y = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dg_Idx = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dg)).BeginInit();
            this.panel1.SuspendLayout();
            this.panDtl.SuspendLayout();
            this.panel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgR)).BeginInit();
            this.SuspendLayout();
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
            this.splitContainer1.Panel2.Controls.Add(this.panDtl);
            this.splitContainer1.Size = new System.Drawing.Size(1884, 1011);
            this.splitContainer1.SplitterDistance = 690;
            this.splitContainer1.TabIndex = 0;
            // 
            // dg
            // 
            this.dg.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dg.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.dg_No,
            this.dg_LocalNm,
            this.dg_LineNm,
            this.dg_StationNm,
            this.dg_LocalCd,
            this.dg_LineCd,
            this.dg_StationCd,
            this.dg_X,
            this.dg_Y,
            this.dg_Idx});
            this.dg.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dg.Location = new System.Drawing.Point(0, 100);
            this.dg.Name = "dg";
            this.dg.RowTemplate.Height = 23;
            this.dg.Size = new System.Drawing.Size(686, 907);
            this.dg.TabIndex = 1;
            // 
            // panel1
            // 
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.cbxSrchLine);
            this.panel1.Controls.Add(this.cbxSrchLocal);
            this.panel1.Controls.Add(this.btnSrch);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(686, 100);
            this.panel1.TabIndex = 0;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(289, 42);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(41, 12);
            this.label2.TabIndex = 2;
            this.label2.Text = "선로 >";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(89, 42);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(41, 12);
            this.label1.TabIndex = 2;
            this.label1.Text = "지역 >";
            // 
            // cbxSrchLine
            // 
            this.cbxSrchLine.FormattingEnabled = true;
            this.cbxSrchLine.Location = new System.Drawing.Point(336, 38);
            this.cbxSrchLine.Name = "cbxSrchLine";
            this.cbxSrchLine.Size = new System.Drawing.Size(121, 20);
            this.cbxSrchLine.TabIndex = 1;
            this.cbxSrchLine.Text = "-선택-";
            // 
            // cbxSrchLocal
            // 
            this.cbxSrchLocal.FormattingEnabled = true;
            this.cbxSrchLocal.Location = new System.Drawing.Point(136, 38);
            this.cbxSrchLocal.Name = "cbxSrchLocal";
            this.cbxSrchLocal.Size = new System.Drawing.Size(121, 20);
            this.cbxSrchLocal.TabIndex = 1;
            // 
            // btnSrch
            // 
            this.btnSrch.Location = new System.Drawing.Point(481, 37);
            this.btnSrch.Name = "btnSrch";
            this.btnSrch.Size = new System.Drawing.Size(75, 23);
            this.btnSrch.TabIndex = 0;
            this.btnSrch.Text = "검색";
            this.btnSrch.UseVisualStyleBackColor = true;
            this.btnSrch.Click += new System.EventHandler(this.btnSrch_Click);
            // 
            // wbr
            // 
            this.wbr.Dock = System.Windows.Forms.DockStyle.Fill;
            this.wbr.Location = new System.Drawing.Point(0, 100);
            this.wbr.MinimumSize = new System.Drawing.Size(20, 20);
            this.wbr.Name = "wbr";
            this.wbr.Size = new System.Drawing.Size(1186, 907);
            this.wbr.TabIndex = 1;
            this.wbr.Url = new System.Uri("https://www.tankauction.com/SOLAR/mapCoord.php", System.UriKind.Absolute);
            this.wbr.DocumentCompleted += new System.Windows.Forms.WebBrowserDocumentCompletedEventHandler(this.wbr_DocumentCompleted);
            // 
            // panDtl
            // 
            this.panDtl.BackColor = System.Drawing.SystemColors.GradientActiveCaption;
            this.panDtl.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panDtl.Controls.Add(this.panel2);
            this.panDtl.Controls.Add(this.label8);
            this.panDtl.Controls.Add(this.label7);
            this.panDtl.Controls.Add(this.label9);
            this.panDtl.Controls.Add(this.label5);
            this.panDtl.Controls.Add(this.label4);
            this.panDtl.Controls.Add(this.btnNew);
            this.panDtl.Controls.Add(this.label6);
            this.panDtl.Controls.Add(this.label3);
            this.panDtl.Controls.Add(this.btnSave);
            this.panDtl.Controls.Add(this.txtCoordY);
            this.panDtl.Controls.Add(this.txtCoordX);
            this.panDtl.Controls.Add(this.txtStationCd);
            this.panDtl.Controls.Add(this.txtIdx);
            this.panDtl.Controls.Add(this.txtStationNm);
            this.panDtl.Controls.Add(this.cbxLine);
            this.panDtl.Controls.Add(this.cbxLocal);
            this.panDtl.Dock = System.Windows.Forms.DockStyle.Top;
            this.panDtl.Location = new System.Drawing.Point(0, 0);
            this.panDtl.Name = "panDtl";
            this.panDtl.Size = new System.Drawing.Size(1186, 100);
            this.panDtl.TabIndex = 0;
            // 
            // panel2
            // 
            this.panel2.BackColor = System.Drawing.SystemColors.AppWorkspace;
            this.panel2.Controls.Add(this.btnMatch);
            this.panel2.Controls.Add(this.btnMatchDel);
            this.panel2.Controls.Add(this.btnMatchAdd);
            this.panel2.Controls.Add(this.dgR);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Right;
            this.panel2.Location = new System.Drawing.Point(750, 0);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(434, 98);
            this.panel2.TabIndex = 7;
            // 
            // btnMatch
            // 
            this.btnMatch.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnMatch.BackColor = System.Drawing.Color.PeachPuff;
            this.btnMatch.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btnMatch.Location = new System.Drawing.Point(27, 17);
            this.btnMatch.Name = "btnMatch";
            this.btnMatch.Size = new System.Drawing.Size(92, 63);
            this.btnMatch.TabIndex = 7;
            this.btnMatch.Text = "역세권 매칭\r\n(경/공매)";
            this.btnMatch.UseVisualStyleBackColor = false;
            this.btnMatch.Click += new System.EventHandler(this.btnMatch_Click);
            // 
            // btnMatchDel
            // 
            this.btnMatchDel.Location = new System.Drawing.Point(132, 57);
            this.btnMatchDel.Name = "btnMatchDel";
            this.btnMatchDel.Size = new System.Drawing.Size(51, 23);
            this.btnMatchDel.TabIndex = 2;
            this.btnMatchDel.Text = "< 삭제";
            this.btnMatchDel.UseVisualStyleBackColor = true;
            this.btnMatchDel.Click += new System.EventHandler(this.btnMatchDel_Click);
            // 
            // btnMatchAdd
            // 
            this.btnMatchAdd.Location = new System.Drawing.Point(131, 16);
            this.btnMatchAdd.Name = "btnMatchAdd";
            this.btnMatchAdd.Size = new System.Drawing.Size(51, 23);
            this.btnMatchAdd.TabIndex = 1;
            this.btnMatchAdd.Text = "추가 >";
            this.btnMatchAdd.UseVisualStyleBackColor = true;
            this.btnMatchAdd.Click += new System.EventHandler(this.btnMatchAdd_Click);
            // 
            // dgR
            // 
            this.dgR.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgR.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.dgR_StationNm,
            this.dgR_Idx});
            this.dgR.Dock = System.Windows.Forms.DockStyle.Right;
            this.dgR.Location = new System.Drawing.Point(193, 0);
            this.dgR.Name = "dgR";
            this.dgR.RowTemplate.Height = 23;
            this.dgR.Size = new System.Drawing.Size(241, 98);
            this.dgR.TabIndex = 0;
            // 
            // dgR_StationNm
            // 
            this.dgR_StationNm.HeaderText = "역명";
            this.dgR_StationNm.Name = "dgR_StationNm";
            this.dgR_StationNm.Width = 130;
            // 
            // dgR_Idx
            // 
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.dgR_Idx.DefaultCellStyle = dataGridViewCellStyle6;
            this.dgR_Idx.HeaderText = "idx";
            this.dgR_Idx.Name = "dgR_Idx";
            this.dgR_Idx.Width = 50;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(498, 62);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(25, 12);
            this.label8.TabIndex = 2;
            this.label8.Text = "Y >";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(351, 62);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(25, 12);
            this.label7.TabIndex = 2;
            this.label7.Text = "X >";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(539, 23);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(34, 12);
            this.label9.TabIndex = 2;
            this.label9.Text = "idx >";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(388, 23);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(38, 12);
            this.label5.TabIndex = 2;
            this.label5.Text = "역C >";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(169, 23);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(41, 12);
            this.label4.TabIndex = 2;
            this.label4.Text = "선로 >";
            // 
            // btnNew
            // 
            this.btnNew.Location = new System.Drawing.Point(673, 18);
            this.btnNew.Name = "btnNew";
            this.btnNew.Size = new System.Drawing.Size(50, 23);
            this.btnNew.TabIndex = 6;
            this.btnNew.Text = "+신규";
            this.btnNew.UseVisualStyleBackColor = true;
            this.btnNew.Click += new System.EventHandler(this.btnNew_Click);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(22, 62);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(41, 12);
            this.label6.TabIndex = 2;
            this.label6.Text = "역명 >";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(22, 23);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(41, 12);
            this.label3.TabIndex = 2;
            this.label3.Text = "지역 >";
            // 
            // btnSave
            // 
            this.btnSave.BackColor = System.Drawing.Color.LightGreen;
            this.btnSave.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btnSave.Location = new System.Drawing.Point(648, 57);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(75, 23);
            this.btnSave.TabIndex = 5;
            this.btnSave.Text = "저장";
            this.btnSave.UseVisualStyleBackColor = false;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // txtCoordY
            // 
            this.txtCoordY.Location = new System.Drawing.Point(526, 58);
            this.txtCoordY.Name = "txtCoordY";
            this.txtCoordY.Size = new System.Drawing.Size(100, 21);
            this.txtCoordY.TabIndex = 4;
            // 
            // txtCoordX
            // 
            this.txtCoordX.Location = new System.Drawing.Point(379, 58);
            this.txtCoordX.Name = "txtCoordX";
            this.txtCoordX.Size = new System.Drawing.Size(100, 21);
            this.txtCoordX.TabIndex = 4;
            // 
            // txtStationCd
            // 
            this.txtStationCd.BackColor = System.Drawing.Color.Silver;
            this.txtStationCd.Location = new System.Drawing.Point(429, 19);
            this.txtStationCd.Name = "txtStationCd";
            this.txtStationCd.ReadOnly = true;
            this.txtStationCd.Size = new System.Drawing.Size(50, 21);
            this.txtStationCd.TabIndex = 4;
            this.txtStationCd.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // txtIdx
            // 
            this.txtIdx.BackColor = System.Drawing.Color.Silver;
            this.txtIdx.Location = new System.Drawing.Point(576, 19);
            this.txtIdx.Name = "txtIdx";
            this.txtIdx.ReadOnly = true;
            this.txtIdx.Size = new System.Drawing.Size(50, 21);
            this.txtIdx.TabIndex = 3;
            this.txtIdx.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // txtStationNm
            // 
            this.txtStationNm.ImeMode = System.Windows.Forms.ImeMode.Hangul;
            this.txtStationNm.Location = new System.Drawing.Point(66, 58);
            this.txtStationNm.Name = "txtStationNm";
            this.txtStationNm.Size = new System.Drawing.Size(268, 21);
            this.txtStationNm.TabIndex = 2;
            // 
            // cbxLine
            // 
            this.cbxLine.FormattingEnabled = true;
            this.cbxLine.Location = new System.Drawing.Point(213, 19);
            this.cbxLine.Name = "cbxLine";
            this.cbxLine.Size = new System.Drawing.Size(121, 20);
            this.cbxLine.TabIndex = 1;
            this.cbxLine.Text = "-선택-";
            // 
            // cbxLocal
            // 
            this.cbxLocal.FormattingEnabled = true;
            this.cbxLocal.Location = new System.Drawing.Point(66, 19);
            this.cbxLocal.Name = "cbxLocal";
            this.cbxLocal.Size = new System.Drawing.Size(85, 20);
            this.cbxLocal.TabIndex = 1;
            // 
            // dg_No
            // 
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dg_No.DefaultCellStyle = dataGridViewCellStyle1;
            this.dg_No.HeaderText = "No";
            this.dg_No.Name = "dg_No";
            this.dg_No.Width = 60;
            // 
            // dg_LocalNm
            // 
            this.dg_LocalNm.HeaderText = "지역명";
            this.dg_LocalNm.Name = "dg_LocalNm";
            // 
            // dg_LineNm
            // 
            this.dg_LineNm.HeaderText = "선로명";
            this.dg_LineNm.Name = "dg_LineNm";
            this.dg_LineNm.Width = 150;
            // 
            // dg_StationNm
            // 
            this.dg_StationNm.HeaderText = "역명";
            this.dg_StationNm.Name = "dg_StationNm";
            this.dg_StationNm.Width = 150;
            // 
            // dg_LocalCd
            // 
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dg_LocalCd.DefaultCellStyle = dataGridViewCellStyle2;
            this.dg_LocalCd.HeaderText = "지역C";
            this.dg_LocalCd.Name = "dg_LocalCd";
            this.dg_LocalCd.Width = 65;
            // 
            // dg_LineCd
            // 
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dg_LineCd.DefaultCellStyle = dataGridViewCellStyle3;
            this.dg_LineCd.HeaderText = "선로C";
            this.dg_LineCd.Name = "dg_LineCd";
            this.dg_LineCd.Width = 65;
            // 
            // dg_StationCd
            // 
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.dg_StationCd.DefaultCellStyle = dataGridViewCellStyle4;
            this.dg_StationCd.HeaderText = "역C";
            this.dg_StationCd.Name = "dg_StationCd";
            this.dg_StationCd.Width = 65;
            // 
            // dg_X
            // 
            this.dg_X.HeaderText = "X";
            this.dg_X.Name = "dg_X";
            // 
            // dg_Y
            // 
            this.dg_Y.HeaderText = "Y";
            this.dg_Y.Name = "dg_Y";
            // 
            // dg_Idx
            // 
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.dg_Idx.DefaultCellStyle = dataGridViewCellStyle5;
            this.dg_Idx.HeaderText = "idx";
            this.dg_Idx.Name = "dg_Idx";
            this.dg_Idx.Width = 60;
            // 
            // wfRailRoad
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1884, 1011);
            this.Controls.Add(this.splitContainer1);
            this.Name = "wfRailRoad";
            this.Text = "도시철도 관리";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dg)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panDtl.ResumeLayout(false);
            this.panDtl.PerformLayout();
            this.panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgR)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.DataGridView dg;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ComboBox cbxSrchLine;
        private System.Windows.Forms.ComboBox cbxSrchLocal;
        private System.Windows.Forms.Button btnSrch;
        private System.Windows.Forms.WebBrowser wbr;
        private System.Windows.Forms.Panel panDtl;
        private System.Windows.Forms.TextBox txtCoordY;
        private System.Windows.Forms.TextBox txtCoordX;
        private System.Windows.Forms.TextBox txtStationCd;
        private System.Windows.Forms.TextBox txtIdx;
        private System.Windows.Forms.TextBox txtStationNm;
        private System.Windows.Forms.ComboBox cbxLine;
        private System.Windows.Forms.ComboBox cbxLocal;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnNew;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnMatch;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Button btnMatchDel;
        private System.Windows.Forms.Button btnMatchAdd;
        private System.Windows.Forms.DataGridView dgR;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgR_StationNm;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgR_Idx;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_No;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_LocalNm;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_LineNm;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_StationNm;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_LocalCd;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_LineCd;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_StationCd;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_X;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_Y;
        private System.Windows.Forms.DataGridViewTextBoxColumn dg_Idx;
    }
}