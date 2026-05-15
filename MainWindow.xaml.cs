using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace WpfKeyAutoClicker
{
    public partial class MainWindow : Window
    {
        private const int HotkeyId = 0x6701;
        private const int WmHotkey = 0x0312;
        private const uint ModNone = 0;
        private const uint VkF7 = 0x76;
        private const int StartDelayMs = 700;

        private readonly DispatcherTimer pressTimer = new DispatcherTimer();
        private readonly DispatcherTimer startDelayTimer = new DispatcherTimer();
        private Forms.Keys targetKey = Forms.Keys.Space;
        private HwndSource hwndSource;
        private long pressCount;
        private bool isPressing;
        private bool isArming;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;

            pressTimer.Interval = TimeSpan.FromMilliseconds(100);
            pressTimer.Tick += delegate { DoKeyPress(); };

            startDelayTimer.Interval = TimeSpan.FromMilliseconds(StartDelayMs);
            startDelayTimer.Tick += delegate
            {
                startDelayTimer.Stop();
                isArming = false;
                isPressing = true;
                pressTimer.Interval = TimeSpan.FromMilliseconds(ReadInterval());
                pressTimer.Start();
                RefreshStatus();
            };
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            hwndSource = HwndSource.FromHwnd(helper.Handle);
            hwndSource?.AddHook(WndProc);

            if (!RegisterHotKey(helper.Handle, HotkeyId, ModNone, VkF7))
            {
                MessageBox.Show(this, "F7 热键注册失败，可能已经被其他程序占用。你仍然可以使用窗口里的按钮启动或停止。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            StopPressing();
            var helper = new WindowInteropHelper(this);
            UnregisterHotKey(helper.Handle, HotkeyId);
            if (hwndSource != null)
            {
                hwndSource.RemoveHook(WndProc);
                hwndSource = null;
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
            {
                TogglePressing();
                handled = true;
            }

            return IntPtr.Zero;
        }

        private void KeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            Forms.Keys key = KeyInterop.VirtualKeyFromKey(e.Key == Key.System ? e.SystemKey : e.Key) == 0
                ? Forms.Keys.Space
                : (Forms.Keys)KeyInterop.VirtualKeyFromKey(e.Key == Key.System ? e.SystemKey : e.Key);

            if (key == Forms.Keys.ShiftKey || key == Forms.Keys.ControlKey || key == Forms.Keys.Menu)
            {
                return;
            }

            targetKey = key;
            KeyBox.Text = KeyName(targetKey);
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            TogglePressing();
        }

        private void TopmostBox_Changed(object sender, RoutedEventArgs e)
        {
            Topmost = TopmostBox.IsChecked == true;
        }

        private void TitleBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && !IsFromButton(e.OriginalSource as DependencyObject))
            {
                DragMove();
            }
        }

        private static bool IsFromButton(DependencyObject source)
        {
            while (source != null)
            {
                if (source is System.Windows.Controls.Button)
                {
                    return true;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TogglePressing()
        {
            if (isPressing || isArming)
            {
                StopPressing();
                return;
            }

            StartPressing();
        }

        private void StartPressing()
        {
            pressTimer.Interval = TimeSpan.FromMilliseconds(ReadInterval());
            isArming = true;
            startDelayTimer.Start();
            RefreshStatus();
        }

        private void StopPressing()
        {
            startDelayTimer.Stop();
            pressTimer.Stop();
            isArming = false;
            isPressing = false;
            RefreshStatus();
        }

        private int ReadInterval()
        {
            int interval;
            if (!int.TryParse(IntervalBox.Text, out interval))
            {
                interval = 100;
            }

            interval = Math.Max(10, Math.Min(600000, interval));
            IntervalBox.Text = interval.ToString();
            return interval;
        }

        private void RefreshStatus()
        {
            if (isArming)
            {
                ToggleButton.Content = "停止 F7";
                StatusText.Text = "准备中";
            }
            else if (isPressing)
            {
                ToggleButton.Content = "停止 F7";
                StatusText.Text = "运行中";
            }
            else
            {
                ToggleButton.Content = "开始 F7";
                StatusText.Text = "已停止";
            }

            CountText.Text = pressCount.ToString();
        }

        private void DoKeyPress()
        {
            try
            {
                KeyboardPresser.Press(targetKey, CtrlBox.IsChecked == true, ShiftBox.IsChecked == true, AltBox.IsChecked == true);
                pressCount++;
                CountText.Text = pressCount.ToString();
            }
            catch (Win32Exception)
            {
                StopPressing();
                MessageBox.Show(this, "发送按键失败。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string KeyName(Forms.Keys key)
        {
            if (key == Forms.Keys.Space) return "Space";
            if (key == Forms.Keys.Return) return "Enter";
            if (key == Forms.Keys.Escape) return "Esc";
            return key.ToString();
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }

    internal static class KeyboardPresser
    {
        private const uint InputKeyboard = 1;
        private const uint KeyeventfKeyup = 0x0002;

        public static void Press(Forms.Keys key, bool ctrl, bool shift, bool alt)
        {
            Input[] inputs = new Input[8];
            int index = 0;

            if (ctrl) AddKey(inputs, ref index, Forms.Keys.ControlKey, false);
            if (shift) AddKey(inputs, ref index, Forms.Keys.ShiftKey, false);
            if (alt) AddKey(inputs, ref index, Forms.Keys.Menu, false);

            AddKey(inputs, ref index, key, false);
            AddKey(inputs, ref index, key, true);

            if (alt) AddKey(inputs, ref index, Forms.Keys.Menu, true);
            if (shift) AddKey(inputs, ref index, Forms.Keys.ShiftKey, true);
            if (ctrl) AddKey(inputs, ref index, Forms.Keys.ControlKey, true);

            Input[] actual = new Input[index];
            Array.Copy(inputs, actual, index);
            uint sent = SendInput((uint)actual.Length, actual, Marshal.SizeOf(typeof(Input)));
            if (sent != actual.Length)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        private static void AddKey(Input[] inputs, ref int index, Forms.Keys key, bool up)
        {
            inputs[index].Type = InputKeyboard;
            inputs[index].Data.Keyboard.VirtualKey = (ushort)key;
            inputs[index].Data.Keyboard.Flags = up ? KeyeventfKeyup : 0;
            index++;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint cInputs, Input[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct Input
        {
            public uint Type;
            public InputUnion Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public KeyboardInput Keyboard;

            [FieldOffset(0)]
            public MouseInput Mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            public ushort VirtualKey;
            public ushort ScanCode;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            public int Dx;
            public int Dy;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }
    }
}
