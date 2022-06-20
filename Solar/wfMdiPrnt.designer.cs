namespace Solar
{
    partial class wfMdiPrnt
    {
        /// <summary>
        /// 필수 디자이너 변수입니다.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 사용 중인 모든 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true이고, 그렇지 않으면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        /// <summary>
        /// 디자이너 지원에 필요한 메서드입니다. 
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마세요.
        /// </summary>
        private void InitializeComponent()
        {
            this.popNoti = new Tulpep.NotificationWindow.PopupNotifier();
            this.SuspendLayout();
            // 
            // popNoti
            // 
            this.popNoti.ContentFont = new System.Drawing.Font("Tahoma", 8F);
            this.popNoti.ContentText = null;
            this.popNoti.Image = null;
            this.popNoti.IsRightToLeft = false;
            this.popNoti.OptionsMenu = null;
            this.popNoti.Size = new System.Drawing.Size(400, 100);
            this.popNoti.TitleFont = new System.Drawing.Font("맑은 고딕", 9F);
            this.popNoti.TitleText = null;
            // 
            // wfMdiPrnt
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1904, 1011);
            this.IsMdiContainer = true;
            this.Name = "wfMdiPrnt";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Solar";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.ResumeLayout(false);

        }
        #endregion

        private Tulpep.NotificationWindow.PopupNotifier popNoti;
    }
}



