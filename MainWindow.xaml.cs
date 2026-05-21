using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
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
        private UiText text;
        private long pressCount;
        private bool isPressing;
        private bool isArming;
        private bool useChinese;

        public MainWindow()
        {
            InitializeComponent();
            useChinese = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase);
            text = UiText.For(useChinese);
            ApplyText();

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
                MessageBox.Show(this, text.HotkeyFailed, text.Tip, MessageBoxButton.OK, MessageBoxImage.Information);
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
            var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
            var virtualKey = KeyInterop.VirtualKeyFromKey(actualKey);
            var key = virtualKey == 0 ? Forms.Keys.Space : (Forms.Keys)virtualKey;

            if (key == Forms.Keys.ShiftKey || key == Forms.Keys.ControlKey || key == Forms.Keys.Menu)
            {
                return;
            }

            targetKey = key;
            KeyBox.Text = KeyName(targetKey);
        }

        private void IntervalBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        private void IntervalBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ReadInterval();
        }

        private void IntervalBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ReadInterval();
                e.Handled = true;
            }
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            TogglePressing();
        }

        private void TopmostBox_Changed(object sender, RoutedEventArgs e)
        {
            Topmost = TopmostBox.IsChecked == true;
        }

        private void FooterText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            useChinese = !useChinese;
            text = UiText.For(useChinese);
            ApplyText();
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
                if (source is Button)
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
            if (!int.TryParse(IntervalBox.Text, out var interval))
            {
                interval = 100;
            }

            interval = Math.Max(10, Math.Min(600000, interval));
            IntervalBox.Text = interval.ToString(CultureInfo.InvariantCulture);
            return interval;
        }

        private void RefreshStatus()
        {
            if (isArming)
            {
                ToggleButton.Content = text.StopButton;
                StatusText.Text = text.Ready;
            }
            else if (isPressing)
            {
                ToggleButton.Content = text.StopButton;
                StatusText.Text = text.Running;
            }
            else
            {
                ToggleButton.Content = text.StartButton;
                StatusText.Text = text.Stopped;
            }

            CountText.Text = pressCount.ToString(CultureInfo.InvariantCulture);
        }

        private void DoKeyPress()
        {
            try
            {
                KeyboardPresser.Press(targetKey, CtrlBox.IsChecked == true, ShiftBox.IsChecked == true, AltBox.IsChecked == true);
                pressCount++;
                CountText.Text = pressCount.ToString(CultureInfo.InvariantCulture);
            }
            catch (Win32Exception)
            {
                StopPressing();
                MessageBox.Show(this, text.SendFailed, text.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyText()
        {
            SubtitleText.Text = text.Subtitle;
            KeyLabel.Text = text.KeyLabel;
            ModifiersLabel.Text = text.ModifiersLabel;
            IntervalLabel.Text = text.IntervalLabel;
            StatusLabel.Text = text.StatusLabel;
            CountLabel.Text = text.CountLabel;
            CountUnitText.Text = text.CountUnit;
            TopmostBox.Content = text.Topmost;
            FooterText.Text = text.Footer;
            RefreshStatus();
        }

        private static string KeyName(Forms.Keys key)
        {
            if (key == Forms.Keys.Space) return "Space";
            if (key == Forms.Keys.Return) return "Enter";
            if (key == Forms.Keys.Escape) return "Esc";
            if (key == Forms.Keys.Back) return "Backspace";
            if (key == Forms.Keys.Next) return "PageDown";
            if (key == Forms.Keys.Prior) return "PageUp";
            return key.ToString();
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private sealed class UiText
        {
            public string Subtitle { get; private set; }
            public string KeyLabel { get; private set; }
            public string ModifiersLabel { get; private set; }
            public string IntervalLabel { get; private set; }
            public string StatusLabel { get; private set; }
            public string CountLabel { get; private set; }
            public string CountUnit { get; private set; }
            public string Topmost { get; private set; }
            public string Footer { get; private set; }
            public string StartButton { get; private set; }
            public string StopButton { get; private set; }
            public string Stopped { get; private set; }
            public string Ready { get; private set; }
            public string Running { get; private set; }
            public string Tip { get; private set; }
            public string Error { get; private set; }
            public string HotkeyFailed { get; private set; }
            public string SendFailed { get; private set; }

            public static UiText For(bool chinese)
            {
                if (chinese)
                {
                    return new UiText
                    {
                        Subtitle = "小体积、无广告。F7 启动或停止。",
                        KeyLabel = "连点键位",
                        ModifiersLabel = "组合按键",
                        IntervalLabel = "按键间隔",
                        StatusLabel = "状态",
                        CountLabel = "按键次数",
                        CountUnit = "次",
                        Topmost = "窗口置顶",
                        Footer = "♥  Designed by Hongswai · 中文  ♥",
                        StartButton = ">> 开始 F7",
                        StopButton = ">> 停止 F7",
                        Stopped = "已停止",
                        Ready = "准备中",
                        Running = "运行中",
                        Tip = "提示",
                        Error = "错误",
                        HotkeyFailed = "F7 热键注册失败，可能已被其他程序占用。你仍然可以使用窗口里的按钮启动或停止。",
                        SendFailed = "发送按键失败。"
                    };
                }

                return new UiText
                {
                    Subtitle = "Small, ad-free. Press F7 to start or stop.",
                    KeyLabel = "Key",
                    ModifiersLabel = "Modifiers",
                    IntervalLabel = "Interval",
                    StatusLabel = "Status",
                    CountLabel = "Presses",
                    CountUnit = "times",
                    Topmost = "Always on top",
                    Footer = "♥  Designed by Hongswai · English  ♥",
                    StartButton = ">> Start F7",
                    StopButton = ">> Stop F7",
                    Stopped = "Stopped",
                    Ready = "Ready",
                    Running = "Running",
                    Tip = "Tip",
                    Error = "Error",
                    HotkeyFailed = "The F7 hotkey could not be registered. It may already be used by another app. You can still use the button in this window.",
                    SendFailed = "Failed to send the key press."
                };
            }
        }
    }

    internal static class KeyboardPresser
    {
        private const uint InputKeyboard = 1;
        private const uint KeyeventfKeyup = 0x0002;

        public static void Press(Forms.Keys key, bool ctrl, bool shift, bool alt)
        {
            var inputs = new Input[8];
            var index = 0;

            if (ctrl) AddKey(inputs, ref index, Forms.Keys.ControlKey, false);
            if (shift) AddKey(inputs, ref index, Forms.Keys.ShiftKey, false);
            if (alt) AddKey(inputs, ref index, Forms.Keys.Menu, false);

            AddKey(inputs, ref index, key, false);
            AddKey(inputs, ref index, key, true);

            if (alt) AddKey(inputs, ref index, Forms.Keys.Menu, true);
            if (shift) AddKey(inputs, ref index, Forms.Keys.ShiftKey, true);
            if (ctrl) AddKey(inputs, ref index, Forms.Keys.ControlKey, true);

            var actual = new Input[index];
            Array.Copy(inputs, actual, index);
            var sent = SendInput((uint)actual.Length, actual, Marshal.SizeOf(typeof(Input)));
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
