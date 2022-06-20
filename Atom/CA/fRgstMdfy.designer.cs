
namespace Atom.CA
{
    partial class fRgstMdfy
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
            this.txtState = new System.Windows.Forms.TextBox();
            this.txtRslt = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // txtState
            // 
            this.txtState.BackColor = System.Drawing.Color.Black;
            this.txtState.Dock = System.Windows.Forms.DockStyle.Left;
            this.txtState.ForeColor = System.Drawing.Color.LimeGreen;
            this.txtState.Location = new System.Drawing.Point(0, 0);
            this.txtState.Multiline = true;
            this.txtState.Name = "txtState";
            this.txtState.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtState.Size = new System.Drawing.Size(532, 561);
            this.txtState.TabIndex = 3;
            // 
            // txtRslt
            // 
            this.txtRslt.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtRslt.Location = new System.Drawing.Point(532, 0);
            this.txtRslt.Multiline = true;
            this.txtRslt.Name = "txtRslt";
            this.txtRslt.Size = new System.Drawing.Size(252, 561);
            this.txtRslt.TabIndex = 4;
            // 
            // fRgstMdfy
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 561);
            this.Controls.Add(this.txtRslt);
            this.Controls.Add(this.txtState);
            this.Name = "fRgstMdfy";
            this.Text = "fRgstMdfy";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtState;
        private System.Windows.Forms.TextBox txtRslt;
    }
}