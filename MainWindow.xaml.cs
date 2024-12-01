using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace BossKey
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private delegate bool WndEnumProc(IntPtr hwnd, IntPtr lParam);
        [LibraryImport("user32.dll")]
        private static partial int EnumWindows(WndEnumProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder title, int size);
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool IsWindowVisible(IntPtr hWnd);
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool IsWindow(IntPtr hWnd);
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool IsWindowEnabled(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);
        [LibraryImport("user32.dll")]
        private static partial int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        // 注册全局热键的常量
        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int MOD_WIN = 0x0008;

        //private const int WM_GETTRAYWND = 0x0000;
        //private const int WM_GETTRAYPOS = 0x0005;
        //private const int TB_BUTTONCOUNT = 0x0418;
        //private const int TB_GETBUTTON = 0x417;

        private int _hotKeyId = 0;
        private IntPtr Handle= IntPtr.Zero;
        private SortDescription _sortDescriptionByVisible;
        private SortDescription _sortDescriptionByName;
        // 按压检测热键时
        private bool _isTextBoxAltPressed = false;
        private bool _isTextBoxCtrlPressed = false;
        private bool _isTextBoxShiftPressed = false;
        private bool _isTextBoxWinPressed = false;

        private HotkeyWindowToggler? _hotkeyWindowToggler = null;
        private MouseWindowToggler? _mouseWindowToggler = null;

        public MainWindow()
        {
            InitializeComponent();
            _sortDescriptionByVisible = new SortDescription("Visible", ListSortDirection.Descending);
            _sortDescriptionByName = new SortDescription("Name", ListSortDirection.Ascending);
        }
        private bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam)
        {
            if (!IsWindow(hwnd) || !IsWindowEnabled(hwnd)) { return true;}

            int cTextLen = GetWindowTextLength(hwnd);
            string Title;
            if (cTextLen != 0)
            {
                StringBuilder text = new(cTextLen + 1);
                _ = GetWindowText(hwnd, text, cTextLen + 1);
                Title = text.ToString();
            }
            else return true;
            bool visible;
            string visibleText;
            if (IsWindowVisible(hwnd)) 
            {
                visible = true;
                visibleText= "✔"; //✓✔
            }
            else
            {
                visible = false;
                visibleText = "✘"; //✗✘
            }
            _ = GetWindowThreadProcessId(hwnd, out int pid);
            string filePath;
            try
            {
                filePath = Process.GetProcessById(pid).MainModule?.FileName ?? "无法获取";
            }
            catch (System.ComponentModel.Win32Exception)
            {
                filePath = "没有权限";
            }
            WindowListView.Items.Add(new WindowInfo { Hwnd = hwnd, Name = Title, Visible = visible, FilePath = filePath, Pid = pid, VisibleText=visibleText });
            return true;
        }

        private void RefreshWindowListView()
        {
            WindowListView.Items.Clear();
            _ = EnumWindows(EnumWindowsProc, IntPtr.Zero);
            WindowListView.Items.SortDescriptions.Add(_sortDescriptionByVisible);
            //WindowsListView.Items.SortDescriptions.Add(_sortDescriptionByName);
        }
        private void RefreshWindowListViewButton_Click(object sender, RoutedEventArgs e) => RefreshWindowListView();

        private void AddHotKeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowListView.SelectedItems.Count == 0) { return; }
            string[] keys = HotKeyTextBox.Text.Split('+');
            uint fsModifiers = 0;
            uint vk = 0;
            foreach (string key in keys)
            {
                switch (key)
                {
                    case "Alt":
                        fsModifiers |= MOD_ALT;
                        break;
                    case "Ctrl":
                        fsModifiers |= MOD_CONTROL;
                        break;
                    case "Shift":
                        fsModifiers |= MOD_SHIFT;
                        break;
                    case "Win":
                        fsModifiers |= MOD_WIN;
                        break;
                    default:
                        uint scanCode = MapVirtualKey(Convert.ToUInt32(key[0]), 0); // 将字符串的第一个字符转换为对应的扫描码
                        vk = MapVirtualKey(scanCode, 1); // 将扫描码转换为虚拟键值
                        break;
                }
            }
            StringBuilder text = new();
            if (_hotkeyWindowToggler != null)
            {
                _hotkeyWindowToggler.Dispose();
                _hotkeyWindowToggler = null;
            }
            _hotkeyWindowToggler = new(Handle, _hotKeyId, fsModifiers, vk);
            foreach (var item in WindowListView.SelectedItems)
            {
                if (item is WindowInfo windowInfo)
                {
                    _hotkeyWindowToggler.HWNDList.Add(windowInfo.Hwnd);
                    text.Append(windowInfo.Hwnd);
                    text.Append(':');
                    text.Append(windowInfo.Name);
                    text.Append("; ");
                }
            }
            if (!_hotkeyWindowToggler.Register())
            {
                MessageBox.Show("注册热键失败！不能添加重复的热键");
                _hotkeyWindowToggler?.Dispose();
                _hotkeyWindowToggler = null;
                HotKekWindowListTextBlock.Text = "";
                return;
            }
            HotKekWindowListTextBlock.Text = text.ToString();
        }
        private void DelHotKeyButton_Click(object sender, RoutedEventArgs e)
        {
            _hotkeyWindowToggler?.Dispose();
            _hotkeyWindowToggler = null;
            HotKekWindowListTextBlock.Text = "";
        }

        private void AddMouseButton_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = MouseComboBox.SelectedIndex;
            StringBuilder text = new();
            if (_mouseWindowToggler != null)
            {
                _mouseWindowToggler.Dispose();
                _mouseWindowToggler = null;
            }
            if (selectedIndex == 0)
            {
                _mouseWindowToggler = new(MouseWindowToggler.Mode.ToggleOnMiddleClick);
            }
            else if(selectedIndex == 1)
            {
                _mouseWindowToggler = new(MouseWindowToggler.Mode.ToggleOnSimultaneousLeftRightClick);
            }
            else if (selectedIndex == 2)
            {
                _mouseWindowToggler = new(MouseWindowToggler.Mode.ToggleOnCursorInWindow);
            }
            else
            {
                MessageBox.Show("错误，没有对应的模式");
                return;
            }
            foreach (var item in WindowListView.SelectedItems)
            {
                if (item is WindowInfo windowInfo)
                {
                    if (selectedIndex == 2)
                        _mouseWindowToggler.HWNDList_CurserInWindow.Add((windowInfo.Hwnd, false));
                    else
                        _mouseWindowToggler.HWNDList.Add(windowInfo.Hwnd);
                    text.Append(windowInfo.Hwnd);
                    text.Append(':');
                    text.Append(windowInfo.Name);
                    text.Append("; ");
                }
            }
            if (!_mouseWindowToggler.SetHook())
            {
                MessageBox.Show("注册鼠标钩子失败！");
                _mouseWindowToggler?.Dispose();
                _mouseWindowToggler = null;
                MouseWindowListTextBlock.Text = "";
                return;
            }
            MouseWindowListTextBlock.Text = text.ToString();
        }
        private void DelMouseButton_Click(object sender, RoutedEventArgs e)
        {
            _mouseWindowToggler?.Dispose();
            _mouseWindowToggler = null;
            MouseWindowListTextBlock.Text = "";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Handle = new WindowInteropHelper(this).Handle;
            RefreshWindowListView();
            HotkeyWindowToggler mainWindowHotkey = new(Handle, 0xBFFF, MOD_ALT, (uint)KeyInterop.VirtualKeyFromKey(Key.P));
            mainWindowHotkey.HWNDList.Add(Handle);
            mainWindowHotkey.Register();
        }
        class WindowInfo
        {
            public bool Visible { set; get; }
            public string VisibleText { set; get; } = string.Empty;
            public string Name { set; get; } = string.Empty;
            public IntPtr Hwnd { set; get; }
            public IntPtr Pid { set; get; }
            public string FilePath { set; get; } = string.Empty;
        }

        private void HotKeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Key pressedKey;
            // 按住alt时会变成Key.System
            if (e.Key == Key.System) { pressedKey = e.SystemKey; }
            else { pressedKey = e.Key; }

            if ((pressedKey >= Key.A && pressedKey <= Key.Z) ||
                (pressedKey >= Key.NumPad0 && pressedKey <= Key.NumPad9))
            {
                if (HotKeyTextBox.Text.Length != 0)
                {
                    string convertedKey = pressedKey.ToString().ToUpper();
                    HotKeyTextBox.Text += "+";
                    HotKeyTextBox.Text += convertedKey;
                    AddHotKeyButton.Focus();
                }
            }
            else
            {
                switch (pressedKey)
                {
                    case Key.LeftAlt:
                    case Key.RightAlt:
                        if (_isTextBoxAltPressed)
                            break;
                        _isTextBoxAltPressed = true;
                        if (HotKeyTextBox.Text.Length != 0)
                            HotKeyTextBox.Text += "+";
                        HotKeyTextBox.Text += "Alt";
                        break;
                    case Key.LeftShift:
                    case Key.RightShift:
                        if (_isTextBoxShiftPressed)
                            break;
                        _isTextBoxShiftPressed = true;
                        if (HotKeyTextBox.Text.Length != 0)
                            HotKeyTextBox.Text += "+";
                        HotKeyTextBox.Text += "Shift";
                        break;
                    case Key.LeftCtrl:
                    case Key.RightCtrl:
                        if (_isTextBoxCtrlPressed)
                            break;
                        _isTextBoxCtrlPressed = true;
                        if (HotKeyTextBox.Text.Length != 0)
                            HotKeyTextBox.Text += "+";
                        HotKeyTextBox.Text += "Ctrl";
                        break;
                    case Key.LWin:
                    case Key.RWin:
                        if (_isTextBoxWinPressed)
                            break;
                        _isTextBoxWinPressed = true;
                        if (HotKeyTextBox.Text.Length != 0)
                            HotKeyTextBox.Text += "+";
                        HotKeyTextBox.Text += "Win";
                        break;
                }
            }
            e.Handled = true; //阻止了原始按键事件的继续传播
        }

        private void HotKeyTextBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 切换为英文输入法
            InputMethod.SetPreferredImeState(HotKeyTextBox, InputMethodState.Off);
            HotKeyTextBox.Clear();
            _isTextBoxAltPressed = false;
            _isTextBoxCtrlPressed = false;
            _isTextBoxShiftPressed = false;
            _isTextBoxWinPressed = false;
        }

        private void OpenAboutWindow_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow w = new()
            {
                Owner = this
            };
            w.Show();
        }
    }
}