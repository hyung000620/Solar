using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Solar
{
    public class MouseCtrl
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern int SetCursorPos(int x, int y);

        private const int kMouseEventMove = 0x0001; /* mouse move */
        private const int kMouseEventLeftDown = 0x0002; /* left button down */
        private const int kMouseEventLeftUp = 0x0004; /* left button up */
        private const int kMouseEventRightDown = 0x0008; /* right button down */

        private readonly ManualResetEvent stoppeing_event_ = new ManualResetEvent(false);
        TimeSpan interval_;

        public MouseCtrl()
        {
            interval_ = TimeSpan.FromMilliseconds(100);
            stoppeing_event_.Reset();
        }

        public void MouseClickCustom(int interval = 100)
        {
            try
            {
                mouse_event(kMouseEventLeftDown, 0, 0, 0, 0);
                mouse_event(kMouseEventLeftUp, 0, 0, 0, 0);
                stoppeing_event_.WaitOne(interval);
            }
            catch (Exception e)
            {
                MessageBox.Show("MouseClickCustom\r\n" + e.Message);
            }
        }

        public void MouseSetPosCustom(int x, int y, int interval = 100)
        {
            try
            {
                SetCursorPos(x, y);
                stoppeing_event_.WaitOne(interval_);

                MouseClickCustom(interval);
            }
            catch (Exception e)
            {
                MessageBox.Show("MouseSetPosCustom\r\n" + e.Message);
            }
        }
    }
}
