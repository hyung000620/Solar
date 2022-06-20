
namespace Atom.CA
{
    partial class fSucbAfter
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
            this.txtPrgs = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // txtPrgs
            // 
            this.txtPrgs.BackColor = System.Drawing.Color.Black;
            this.txtPrgs.Dock = System.Windows.Forms.DockStyle.Left;
            this.txtPrgs.ForeColor = System.Drawing.Color.LimeGreen;
            this.txtPrgs.Location = new System.Drawing.Point(0, 0);
            this.txtPrgs.Multiline = true;
            this.txtPrgs.Name = "txtPrgs";
            this.txtPrgs.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtPrgs.Size = new System.Drawing.Size(532, 561);
            this.txtPrgs.TabIndex = 2;
            // 
            // fSucbAfter
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 561);
            this.Controls.Add(this.txtPrgs);
            this.Name = "fSucbAfter";
            this.Text = "fSucbAfter";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtPrgs;
    }
}