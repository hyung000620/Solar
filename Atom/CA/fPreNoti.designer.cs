
namespace Atom.CA
{
    partial class fPreNoti
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
            this.txtProgrs = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // txtProgrs
            // 
            this.txtProgrs.BackColor = System.Drawing.Color.Black;
            this.txtProgrs.Dock = System.Windows.Forms.DockStyle.Left;
            this.txtProgrs.ForeColor = System.Drawing.Color.LimeGreen;
            this.txtProgrs.Location = new System.Drawing.Point(0, 0);
            this.txtProgrs.Multiline = true;
            this.txtProgrs.Name = "txtProgrs";
            this.txtProgrs.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtProgrs.Size = new System.Drawing.Size(532, 561);
            this.txtProgrs.TabIndex = 2;
            // 
            // fPreNoti
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 561);
            this.Controls.Add(this.txtProgrs);
            this.Name = "fPreNoti";
            this.Text = "fPreNoti";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtProgrs;
    }
}