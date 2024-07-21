using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace BossKey
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        class HotKeyInfo(int id)
        {
            public int Id { get; set; } = id;
            public bool Visible = true;
            public List<IntPtr> WindowList { get; set; } = [];
            //public List<int> PIdList { get; set; } = [];
            public List<string> PNameList { get; set; } = [];
            public void SwitchVisualStates()
            {
                int pid;
                // 隐藏
                if (Visible == true)
                {
                    for (int i = 0; i < WindowList.Count; i++)
                    {
                        ShowWindow(WindowList[i], 0);
                    }
                    //if (PIdList.Count != 0 || PNameList.Count != 0)
                    //{
                        EnumWindows((hwnd, lParam) => 
                        {
                            _ = GetWindowThreadProcessId(hwnd, out pid);
                            //if (PIdList.Contains(pid))
                            //{
                            //    ShowWindow(hwnd, 0);
                            //}
                            if (PNameList.Count != 0)
                            {
                                Process process = Process.GetProcessById(pid);
                                if (PNameList.Contains(process.ProcessName))
                                {
                                    ShowWindow(hwnd, 0);
                                }
                            }
                            return true;
                        }, IntPtr.Zero);
                    //}
                    Visible = false;
                }
                // 显示
                else
                {
                    for (int i = 0; i < WindowList.Count; i++)
                    {
                        ShowWindow(WindowList[i], 2);
                        ShowWindow(WindowList[i], 1);
                    }
                    //if (PIdList.Count != 0 || PNameList.Count != 0)
                    //{
                        EnumWindows((hwnd, lParam) =>
                        {
                            _ = GetWindowThreadProcessId(hwnd, out pid);
                            //if (PIdList.Contains(pid))
                            //{
                            //    ShowWindow(hwnd, 2);
                            //    ShowWindow(hwnd, 1);
                            //}
                            if (PNameList.Count != 0)
                            {
                                Process process = Process.GetProcessById(pid);
                                if (PNameList.Contains(process.ProcessName))
                                {
                                    ShowWindow(hwnd, 2);
                                    ShowWindow(hwnd, 1);
                                }
                            }
                            return true;
                        }, IntPtr.Zero);
                    //}
                    Visible = true;
                }
            }
        }

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
        [DllImport("user32.DLL")]
        private static extern bool ShowWindow(IntPtr hWnd, uint nCmdShow);
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        // 注册全局热键的常量
        private const int WM_HOTKEY = 0x0312;
        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int MOD_WIN = 0x0008;

        private const int WM_GETTRAYWND = 0x0000;
        private const int WM_GETTRAYPOS = 0x0005;
        private const int TB_BUTTONCOUNT = 0x0418;
        private const int TB_GETBUTTON = 0x417;

        private int _hotKeyId = 0;
        private IntPtr Handle= IntPtr.Zero;
        private SortDescription _sortDescriptionByVisible;
        private SortDescription _sortDescriptionByName;
        private readonly List<HotKeyInfo> _hotkeyList = [];
        // 按压检测热键时
        private bool _isTextBoxAltPressed = false;
        private bool _isTextBoxCtrlPressed = false;
        private bool _isTextBoxShiftPressed = false;
        private bool _isTextBoxWinPressed = false;

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

        private void AddHotKeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowListView.SelectedItems.Count == 0 && ProcessListView.SelectedItems.Count == 0) { return; }
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
            HotKeyInfo targetWindowList = new(_hotKeyId);
            StringBuilder textHotKeyListBox = new(HotKeyTextBox.Text);
            textHotKeyListBox.Append("  [");
            foreach (var item in WindowListView.SelectedItems)
            {
                if (item is WindowInfo windowInfo)
                {
                    targetWindowList.WindowList.Add((windowInfo.Hwnd));
                    textHotKeyListBox.Append(windowInfo.Name);
                    textHotKeyListBox.Append('；');
                }
            }
            foreach (var item in ProcessListView.SelectedItems)
            {
                if (item is ProcessInfo processInfo)
                {
                    targetWindowList.PNameList.Add((processInfo.Name));
                    textHotKeyListBox.Append(processInfo.Name);
                    textHotKeyListBox.Append('；');
                }
            }
            if (!RegisterHotKey(Handle, _hotKeyId, fsModifiers, vk))
            {
                MessageBox.Show("注册热键失败！不能添加重复的热键");
                return;
            }
            textHotKeyListBox.Append(']');
            HotKeyListBox.Items.Add(textHotKeyListBox);
            _hotkeyList.Add(targetWindowList);
            _hotKeyId++;
        }
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                if (wParam == 0xBFFF)
                {
                    if (Visibility == Visibility.Visible) { Hide(); }
                    else { Show(); }
                }
                else
                {
                    SwitchTargetWindow(wParam.ToInt32());
                }
            }
            return IntPtr.Zero;
        }

        private void SwitchTargetWindow(int id)
        {
            var item = _hotkeyList.Where(x => x.Id == id).FirstOrDefault();
            item?.SwitchVisualStates();
        }
        private void DeleteHotKey_Click(object sender, EventArgs e)
        {
            int index = HotKeyListBox.SelectedIndex;
            HotKeyListBox.Items.RemoveAt(index);
            UnregisterHotKey(Handle, _hotkeyList[index].Id);
            _hotkeyList.RemoveAt(index);
        }

        private void RefreshWindowListViewButton_Click(object sender, RoutedEventArgs e) => RefreshWindowListView();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Handle = new WindowInteropHelper(this).Handle;
            RefreshWindowListView();
            RefreshProcessListView();
            HwndSource hwndSource = HwndSource.FromHwnd(Handle);
            hwndSource.AddHook(WndProc);
            RegisterHotKey(Handle, 0xBFFF, MOD_ALT, (uint)KeyInterop.VirtualKeyFromKey(Key.P));
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
        class ProcessInfo
        {
            public string Name {  get; set; } = string.Empty;
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

        private void RefreshProcessListViewButton_Click(object sender, RoutedEventArgs e) => RefreshProcessListView();
        private void RefreshProcessListView()
        {
            HashSet<string> processNameList = [];
            Process[] processeList = Process.GetProcesses();

            foreach (Process process in processeList)
            {
                try
                {
                    processNameList.Add(process.ProcessName);
                }
                catch //(Exception ex)
                {
                    ;
                    // 处理可能的权限异常
                    //Console.WriteLine($"无法获取进程名: {ex.Message}");
                }
            }
            foreach (string name in processNameList)
            {
                ProcessListView.Items.Add(new ProcessInfo { Name = name });
            }
            ProcessListView.Items.SortDescriptions.Add(_sortDescriptionByName);
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
/*            TabItem? selectedTab = ((TabControl)sender).SelectedItem as TabItem;
            if (selectedTab != null)
            {
                // 获取选中的 Tab 的标题
                switch (selectedTab.TabIndex)
                {
                    case 0:
                        if (WindowListView.Items.Count == 0)
                            RefreshWindowListView();
                        break;
                    case 1:
                        if (ProcessListView.Items.Count == 0)
                            RefreshProcessListView();
                        break;
                }
            }*/
        }
    }
}