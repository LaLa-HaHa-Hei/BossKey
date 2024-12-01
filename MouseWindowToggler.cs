using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows;

namespace BossKey
{
    public class MouseWindowToggler
    {
        public enum Mode
        {
            ToggleOnMiddleClick = 0,
            ToggleOnSimultaneousLeftRightClick = 1,
            ToggleOnCursorInWindow = 2,
        }
        [DllImport("user32.DLL")]
        private static extern bool ShowWindow(IntPtr hWnd, uint nCmdShow);
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        const uint SW_HIDE = 0;
        const uint SW_SHOWNORMAL = 1;
        const uint SW_SHOWMINIMIZED = 2;
        const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MOUSEMOVE = 0x0200;
        const int WH_MOUSE_LL = 14;

        // 用来记录按下的时间
        private DateTime leftButtonDownTime = DateTime.MinValue;
        private DateTime rightButtonDownTime = DateTime.MinValue;

        public List<IntPtr> HWNDList { get; set; } = [];
        public List<(IntPtr HWND, bool visible)> HWNDList_CurserInWindow { get; set; } = [];
        public bool IsWindowVisible { get; set; } = true;
        public bool IsHook { get; private set; } = false;

        private IntPtr _hookId = IntPtr.Zero;
        private bool _disposed = false;
        private Mode _mode = 0;

        public MouseWindowToggler(Mode mode)
        {
            _mode = mode;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                //if (disposing)
                //{
                //    // 释放托管资源
                //}

                // 释放非托管资源
                if (IsHook)
                {
                    _ = Unhook();
                    IsHook = false;
                }

                _disposed = true;
            }
        }

        public bool SetHook()
        {
            using Process curProcess = Process.GetCurrentProcess();
            using ProcessModule curModule = curProcess.MainModule;
            _hookId = SetWindowsHookEx(WH_MOUSE_LL, HookCallback, GetModuleHandle(curModule.ModuleName), 0);
            if (_hookId == IntPtr.Zero)
            {
                return false;
            }
            else
            {
                IsHook = true;
                return true;
            }
        }

        public bool Unhook()
        {
            if (UnhookWindowsHookEx(_hookId))
            {
                IsHook = false;
                return true;
            }
            else
            {
                return false;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();

                // 使用 switch 来处理不同的消息类型
                switch (msg)
                {
                    case WM_MBUTTONDOWN:
                        // 处理鼠标中键按下
                        if (_mode == Mode.ToggleOnMiddleClick)
                            ToggleWindowVisibility();
                        break;

                    case WM_LBUTTONDOWN:
                        // 处理鼠标左键按下
                        leftButtonDownTime = DateTime.Now;
                        break;

                    case WM_RBUTTONDOWN:
                        // 处理鼠标右键按下
                        rightButtonDownTime = DateTime.Now;
                        break;

                    case WM_LBUTTONUP:
                        // 处理鼠标左键释放
                        leftButtonDownTime = DateTime.MinValue;
                        break;

                    case WM_RBUTTONUP:
                        // 处理鼠标右键释放
                        rightButtonDownTime = DateTime.MinValue;
                        break;
                    case WM_MOUSEMOVE:
                        if (_mode == Mode.ToggleOnCursorInWindow)
                        {
                            for (int i = 0; i < HWNDList_CurserInWindow.Count; i++)
                            {
                                if (GetWindowRect(HWNDList_CurserInWindow[i].HWND, out RECT rect))
                                {
                                    try
                                    {
                                        var ms = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                                        int cursorX = ms.pt.x;
                                        int cursorY = ms.pt.y;
                                        int width = rect.Right - rect.Left;
                                        int height = rect.Bottom - rect.Top;
                                        int x = rect.Left;
                                        int y = rect.Top;

                                        bool isInWindow = cursorX >= x && cursorX <= x + width && cursorY >= y && cursorY <= y + height;

                                        if (isInWindow && !HWNDList_CurserInWindow[i].visible)
                                        {
                                            ShowWindow(HWNDList_CurserInWindow[i].HWND, SW_SHOWMINIMIZED);
                                            ShowWindow(HWNDList_CurserInWindow[i].HWND, SW_SHOWNORMAL);
                                            HWNDList_CurserInWindow[i] = (HWNDList_CurserInWindow[i].HWND, true);
                                        }
                                        else if (!isInWindow && HWNDList_CurserInWindow[i].visible)
                                        {
                                            ShowWindow(HWNDList_CurserInWindow[i].HWND, SW_HIDE);
                                            HWNDList_CurserInWindow[i] = (HWNDList_CurserInWindow[i].HWND, false);
                                        }
                                    }
                                    catch { }
                                }
                                else
                                {
                                    MessageBox.Show("无法获取窗口信息");
                                }
                            }
                        }
                        break;
                }

                // 判断左右键是否几乎同时按下（比如500毫秒内）
                if (_mode == Mode.ToggleOnSimultaneousLeftRightClick && leftButtonDownTime != DateTime.MinValue && rightButtonDownTime != DateTime.MinValue)
                {
                    var timeDiff = Math.Abs((leftButtonDownTime - rightButtonDownTime).TotalMilliseconds);
                    if (timeDiff < 500) // 500毫秒内视为同时按下
                    {
                        ToggleWindowVisibility();
                    }
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void ToggleWindowVisibility()
        {
            // 隐藏
            if (IsWindowVisible)
            {
                foreach (IntPtr HWND in HWNDList)
                {
                    try
                    {
                        ShowWindow(HWND, SW_HIDE);
                    }
                    catch { }
                }
                IsWindowVisible = false;
            }
            // 显示
            else
            {
                foreach (IntPtr HWND in HWNDList)
                {
                    try
                    {
                        ShowWindow(HWND, SW_SHOWMINIMIZED);
                        ShowWindow(HWND, SW_SHOWNORMAL);
                    }
                    catch { }
                }
                IsWindowVisible = true;
            }
        }
    }
}
