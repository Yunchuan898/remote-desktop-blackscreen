using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace BlackScreenHelper
{
    // ------------------------------------------------------------------
    // Win32 常量与 P/Invoke 声明
    // ------------------------------------------------------------------
    internal static class Native
    {
        public const int WM_HOTKEY = 0x0312;
        public const int WM_SYSCOMMAND = 0x0112;
        public const int SC_MONITORPOWER = 0xF170;

        public const int MOD_ALT = 0x1;
        public const int MOD_CONTROL = 0x2;
        public const int MOD_SHIFT = 0x4;
        public const int MOD_WIN = 0x8;

        public const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

        public const int WS_EX_TOPMOST = 0x8;
        public const int WS_EX_TRANSPARENT = 0x20;
        public const int WS_EX_TOOLWINDOW = 0x80;
        public const int WS_EX_LAYERED = 0x80000;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        public const uint LWA_ALPHA = 0x2;

        public const uint SWP_NOSIZE = 0x1;
        public const uint SWP_NOMOVE = 0x2;
        public const uint SWP_NOACTIVATE = 0x10;

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);

        [DllImport("user32.dll")]
        public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte alpha, uint flags);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder text, int max);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    }

    // ------------------------------------------------------------------
    // 检测模式 / 黑屏方式
    // ------------------------------------------------------------------
    internal enum DetectMode
    {
        Manual,
        Process,
        Window
    }

    internal enum BlankMethod
    {
        Overlay,
        MonitorOff
    }

    // ------------------------------------------------------------------
    // 配置
    // ------------------------------------------------------------------
    internal class Config
    {
        public DetectMode Mode = DetectMode.Manual;
        public BlankMethod Method = BlankMethod.Overlay;
        public List<string> Processes = new List<string>();
        public List<string> WindowKeywords = new List<string>();
        public List<string> WindowClasses = new List<string>();
        public bool AutoStart = false;
        public bool LearnMode = false;
        public uint HotkeyModifiers = Native.MOD_CONTROL | Native.MOD_ALT;
        public uint HotkeyKey = (uint)Keys.B;

        private static string NormalizeProc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Trim().ToLowerInvariant();
            if (s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 4);
            return s;
        }

        public static bool ProcessMatches(string processName, string target)
        {
            return NormalizeProc(processName) == NormalizeProc(target);
        }

        public static Config Load(string path)
        {
            Config c = new Config();
            if (!File.Exists(path))
            {
                c.Processes.Add("SunloginClient.exe");
                c.Processes.Add("ToDesk.exe");
                return c;
            }

            foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                int idx = line.IndexOf('=');
                if (idx < 0) continue;
                string key = line.Substring(0, idx).Trim();
                string val = line.Substring(idx + 1).Trim();
                if (val.Length == 0) continue;

                switch (key.ToLowerInvariant())
                {
                    case "mode":
                        if (val.Equals("process", StringComparison.OrdinalIgnoreCase)) c.Mode = DetectMode.Process;
                        else if (val.Equals("window", StringComparison.OrdinalIgnoreCase)) c.Mode = DetectMode.Window;
                        else c.Mode = DetectMode.Manual;
                        break;
                    case "blackscreenmethod":
                        if (val.Equals("monitoroff", StringComparison.OrdinalIgnoreCase)) c.Method = BlankMethod.MonitorOff;
                        else c.Method = BlankMethod.Overlay;
                        break;
                    case "process":
                        c.Processes.Add(val);
                        break;
                    case "windowkeyword":
                        c.WindowKeywords.Add(val);
                        break;
                    case "windowclass":
                        c.WindowClasses.Add(val);
                        break;
                    case "autostart":
                        c.AutoStart = val.Equals("1") || val.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "learnmode":
                        c.LearnMode = val.Equals("1") || val.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "hotkey":
                        ParseHotkey(val, c);
                        break;
                }
            }
            return c;
        }

        private static void ParseHotkey(string val, Config c)
        {
            string[] parts = val.Split('+');
            uint mods = 0;
            uint key = 0;
            foreach (string rawPart in parts)
            {
                string t = rawPart.Trim();
                if (t.Length == 0) continue;
                if (t.Equals("control", StringComparison.OrdinalIgnoreCase) || t.Equals("ctrl", StringComparison.OrdinalIgnoreCase))
                    mods |= Native.MOD_CONTROL;
                else if (t.Equals("alt", StringComparison.OrdinalIgnoreCase))
                    mods |= Native.MOD_ALT;
                else if (t.Equals("shift", StringComparison.OrdinalIgnoreCase))
                    mods |= Native.MOD_SHIFT;
                else if (t.Equals("win", StringComparison.OrdinalIgnoreCase))
                    mods |= Native.MOD_WIN;
                else
                    key = KeyFromString(t);
            }
            if (key != 0)
            {
                c.HotkeyModifiers = mods;
                c.HotkeyKey = key;
            }
        }

        private static uint KeyFromString(string t)
        {
            if (t.Length == 1)
            {
                char ch = char.ToUpperInvariant(t[0]);
                if (ch >= 'A' && ch <= 'Z') return (uint)ch;
                if (ch >= '0' && ch <= '9') return (uint)ch;
            }
            if (t.StartsWith("F", StringComparison.OrdinalIgnoreCase) && t.Length > 1)
            {
                int n;
                if (int.TryParse(t.Substring(1), out n) && n >= 1 && n <= 24)
                    return (uint)((int)Keys.F1 + n - 1);
            }
            return 0;
        }

        public void Save(string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# =======================================================");
            sb.AppendLine("# 远程黑屏助手 配置文件（改完后重启程序生效）");
            sb.AppendLine("# =======================================================");
            sb.AppendLine("# 检测模式：Manual(手动) / Process(进程) / Window(窗口)");
            sb.AppendLine("Mode=" + (Mode == DetectMode.Process ? "Process" : Mode == DetectMode.Window ? "Window" : "Manual"));
            sb.AppendLine();
            sb.AppendLine("# 黑屏方式：Overlay(覆盖层,默认) / MonitorOff(关闭显示器)");
            sb.AppendLine("BlackScreenMethod=" + (Method == BlankMethod.MonitorOff ? "MonitorOff" : "Overlay"));
            sb.AppendLine();
            sb.AppendLine("# 需要监视的进程名（Process 模式用，可多行）");
            foreach (string p in Processes) sb.AppendLine("Process=" + p);
            sb.AppendLine();
            sb.AppendLine("# 需要匹配的窗口标题关键字（Window 模式用，可多行）");
            foreach (string k in WindowKeywords) sb.AppendLine("WindowKeyword=" + k);
            sb.AppendLine();
            sb.AppendLine("# 需要匹配的窗口类名关键字（Window 模式用，可留空）");
            foreach (string wc in WindowClasses) sb.AppendLine("WindowClass=" + wc);
            sb.AppendLine();
            sb.AppendLine("# 是否开机自启：1 / 0");
            sb.AppendLine("AutoStart=" + (AutoStart ? "1" : "0"));
            sb.AppendLine("# 学习模式（窗口侦探）：1 / 0");
            sb.AppendLine("LearnMode=" + (LearnMode ? "1" : "0"));
            sb.AppendLine();
            sb.AppendLine("# 全局热键（例如 Control+Alt+B）");
            sb.AppendLine("Hotkey=Control+Alt+B");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
    }

    // ------------------------------------------------------------------
    // 黑屏覆盖层窗口（全屏、无边框、置顶、鼠标穿透、排除屏幕捕获）
    // ------------------------------------------------------------------
    internal class BlackOverlay : Form
    {
        public BlackOverlay()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.BackColor = Color.Black;
            this.Bounds = SystemInformation.VirtualScreen;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TOPMOST
                            | Native.WS_EX_TRANSPARENT
                            | Native.WS_EX_TOOLWINDOW
                            | Native.WS_EX_LAYERED
                            | Native.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // 标记为「排除屏幕捕获」：本机看得见，但远程工具(DDA 抓屏)看不到
            Native.SetWindowDisplayAffinity(this.Handle, Native.WDA_EXCLUDEFROMCAPTURE);
            // 设为不透明黑色
            Native.SetLayeredWindowAttributes(this.Handle, 0, 255, Native.LWA_ALPHA);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 覆盖层不允许被用户直接关闭（如 Alt+F4），只能由程序 Hide
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        public void ForceTopMost()
        {
            Native.SetWindowPos(this.Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }
    }

    // ------------------------------------------------------------------
    // 控制面板（远端可见的状态 + 开关）：普通窗口，会被抓屏，所以远程端能看见
    // ------------------------------------------------------------------
    internal class ControlPanel : Form
    {
        private Label statusLabel;
        private Button toggleButton;
        private AppContext owner;

        public ControlPanel(AppContext owner)
        {
            this.owner = owner;
            this.Text = "远程黑屏助手";
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.ShowInTaskbar = true;
            this.ClientSize = new Size(252, 110);
            this.Location = new Point(
                Screen.PrimaryScreen.WorkingArea.Right - 262,
                Screen.PrimaryScreen.WorkingArea.Top + 16);

            Font f = new Font("Microsoft YaHei UI", 9.5f);

            statusLabel = new Label();
            statusLabel.AutoSize = false;
            statusLabel.Location = new Point(12, 12);
            statusLabel.Size = new Size(228, 22);
            statusLabel.Font = f;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;

            toggleButton = new Button();
            toggleButton.Location = new Point(12, 40);
            toggleButton.Size = new Size(228, 34);
            toggleButton.Font = f;
            toggleButton.Click += delegate { owner.ToggleBlank(); };

            Label hint = new Label();
            hint.AutoSize = false;
            hint.Location = new Point(12, 82);
            hint.Size = new Size(228, 18);
            hint.Font = new Font("Microsoft YaHei UI", 8f);
            hint.ForeColor = Color.Gray;
            hint.TextAlign = ContentAlignment.MiddleLeft;
            hint.Text = "黑屏后仍可在远程端点击按钮恢复";

            this.Controls.Add(statusLabel);
            this.Controls.Add(toggleButton);
            this.Controls.Add(hint);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // 不抢焦点：点击按钮时不会被激活并盖到黑屏覆盖层之上
                cp.ExStyle |= Native.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        public void UpdateState(bool blanked)
        {
            if (blanked)
            {
                statusLabel.Text = "本地屏幕：黑屏中";
                statusLabel.ForeColor = Color.FromArgb(205, 40, 40);
                toggleButton.Text = "立即恢复";
            }
            else
            {
                statusLabel.Text = "本地屏幕：正常";
                statusLabel.ForeColor = Color.FromArgb(0, 130, 60);
                toggleButton.Text = "立即黑屏";
            }
        }
    }

    // ------------------------------------------------------------------
    // 隐藏消息窗口：接收全局热键消息
    // ------------------------------------------------------------------
    internal class MessageForm : Form
    {
        private AppContext owner;

        public MessageForm(AppContext owner)
        {
            this.owner = owner;
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Opacity = 0.0;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY)
            {
                owner.OnHotkey();
                return;
            }
            base.WndProc(ref m);
        }
    }

    // ------------------------------------------------------------------
    // 应用上下文：托盘、热键、检测轮询、黑屏控制
    // ------------------------------------------------------------------
    internal class AppContext : ApplicationContext
    {
        private const int HOTKEY_ID = 1;

        private string baseDir;
        private string configPath;
        private string logPath;

        private Config config;
        private NotifyIcon tray;
        private MessageForm messageForm;
        private BlackOverlay overlay;
        private ControlPanel panel;

        private Timer pollTimer;
        private Timer keepAliveTimer;

        private bool blanked = false;
        private bool manualOverride = false;
        private bool hotkeyRegistered = false;

        private HashSet<string> loggedLearn = new HashSet<string>();

        // 菜单项引用，用于勾选状态维护
        private ToolStripMenuItem modeManualItem;
        private ToolStripMenuItem modeProcessItem;
        private ToolStripMenuItem modeWindowItem;
        private ToolStripMenuItem methodOverlayItem;
        private ToolStripMenuItem methodMonitorOffItem;
        private ToolStripMenuItem learnModeItem;
        private ToolStripMenuItem autoStartItem;

        public AppContext()
        {
            baseDir = AppDomain.CurrentDomain.BaseDirectory;
            configPath = Path.Combine(baseDir, "BlackScreenHelper.config");
            logPath = Path.Combine(baseDir, "BlackScreenHelper.log");

            if (!File.Exists(configPath))
            {
                Config def = new Config();
                def.Processes.Add("SunloginClient.exe");
                def.Processes.Add("ToDesk.exe");
                def.Save(configPath);
            }
            config = Config.Load(configPath);

            overlay = new BlackOverlay();

            // 隐藏消息窗口（承载热键）
            messageForm = new MessageForm(this);
            IntPtr h = messageForm.Handle; // 强制创建句柄

            BuildTray();

            RegisterHotkey();

            pollTimer = new Timer();
            pollTimer.Interval = 1500;
            pollTimer.Tick += delegate { PollDetection(); };
            pollTimer.Start();

            keepAliveTimer = new Timer();
            keepAliveTimer.Interval = 1000;
            keepAliveTimer.Tick += delegate { KeepAlive(); };
            keepAliveTimer.Start();

            ApplyAutoStart(); // 使注册表与当前配置保持一致

            panel = new ControlPanel(this);
            panel.UpdateState(false);
            panel.Show();

            UpdateTrayText();
        }

        // ---------------- 托盘与菜单 ----------------
        private void BuildTray()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            ToolStripMenuItem toggle = new ToolStripMenuItem("黑屏 / 恢复\t(Ctrl+Alt+B)");
            toggle.Click += delegate { ToggleBlank(); };

            ToolStripMenuItem modeMenu = new ToolStripMenuItem("检测模式");
            modeManualItem = new ToolStripMenuItem("手动");
            modeProcessItem = new ToolStripMenuItem("进程");
            modeWindowItem = new ToolStripMenuItem("窗口");
            modeManualItem.Click += delegate { SetMode(DetectMode.Manual); };
            modeProcessItem.Click += delegate { SetMode(DetectMode.Process); };
            modeWindowItem.Click += delegate { SetMode(DetectMode.Window); };
            modeMenu.DropDownItems.Add(modeManualItem);
            modeMenu.DropDownItems.Add(modeProcessItem);
            modeMenu.DropDownItems.Add(modeWindowItem);

            ToolStripMenuItem methodMenu = new ToolStripMenuItem("黑屏方式");
            methodOverlayItem = new ToolStripMenuItem("覆盖层(推荐)");
            methodMonitorOffItem = new ToolStripMenuItem("关闭显示器");
            methodOverlayItem.Click += delegate { SetMethod(BlankMethod.Overlay); };
            methodMonitorOffItem.Click += delegate { SetMethod(BlankMethod.MonitorOff); };
            methodMenu.DropDownItems.Add(methodOverlayItem);
            methodMenu.DropDownItems.Add(methodMonitorOffItem);

            learnModeItem = new ToolStripMenuItem("学习模式(窗口侦探)");
            learnModeItem.Click += delegate { ToggleLearnMode(); };

            autoStartItem = new ToolStripMenuItem("开机自启");
            autoStartItem.Click += delegate { ToggleAutoStart(); };

            ToolStripMenuItem showPanel = new ToolStripMenuItem("显示控制面板");
            showPanel.Click += delegate { ShowPanel(); };

            ToolStripMenuItem openConfig = new ToolStripMenuItem("打开配置文件");
            openConfig.Click += delegate { OpenConfig(); };

            ToolStripMenuItem exit = new ToolStripMenuItem("退出");
            exit.Click += delegate { ExitThread(); };

            menu.Items.Add(toggle);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(modeMenu);
            menu.Items.Add(methodMenu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(learnModeItem);
            menu.Items.Add(autoStartItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(showPanel);
            menu.Items.Add(openConfig);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exit);

            tray = new NotifyIcon();
            tray.Icon = SystemIcons.Application;
            tray.Text = "远程黑屏助手";
            tray.Visible = true;
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ToggleBlank(); };

            RefreshMenuChecks();
        }

        private void RefreshMenuChecks()
        {
            modeManualItem.Checked = config.Mode == DetectMode.Manual;
            modeProcessItem.Checked = config.Mode == DetectMode.Process;
            modeWindowItem.Checked = config.Mode == DetectMode.Window;
            methodOverlayItem.Checked = config.Method == BlankMethod.Overlay;
            methodMonitorOffItem.Checked = config.Method == BlankMethod.MonitorOff;
            learnModeItem.Checked = config.LearnMode;
            autoStartItem.Checked = config.AutoStart;
        }

        // ---------------- 热键 ----------------
        private void RegisterHotkey()
        {
            if (Native.RegisterHotKey(messageForm.Handle, HOTKEY_ID, config.HotkeyModifiers, config.HotkeyKey))
            {
                hotkeyRegistered = true;
            }
        }

        private void UnregisterHotkey()
        {
            if (hotkeyRegistered)
            {
                Native.UnregisterHotKey(messageForm.Handle, HOTKEY_ID);
                hotkeyRegistered = false;
            }
        }

        public void OnHotkey()
        {
            ToggleBlank();
        }

        // ---------------- 黑屏 / 恢复 ----------------
        internal void ToggleBlank()
        {
            manualOverride = true;
            SetBlank(!blanked);
        }

        private void SetBlank(bool blank)
        {
            if (blank == blanked) return;
            blanked = blank;
            if (blank)
            {
                ApplyBlank();
            }
            else
            {
                Restore();
            }
            UpdateTrayText();
            if (panel != null) panel.UpdateState(blanked);
        }

        private void ApplyBlank()
        {
            if (config.Method == BlankMethod.Overlay)
            {
                if (!overlay.Visible)
                {
                    overlay.Bounds = SystemInformation.VirtualScreen;
                    overlay.Show();
                }
                overlay.ForceTopMost();
            }
            else
            {
                KeepMonitorOff();
            }
        }

        private void Restore()
        {
            if (config.Method == BlankMethod.Overlay)
            {
                if (overlay.Visible) overlay.Hide();
            }
            else
            {
                Native.SendMessage(Native.HWND_BROADCAST, Native.WM_SYSCOMMAND,
                    (IntPtr)Native.SC_MONITORPOWER, new IntPtr(-1));
            }
        }

        private void KeepAlive()
        {
            if (!blanked) return;
            if (config.Method == BlankMethod.Overlay)
            {
                if (overlay.Visible) overlay.ForceTopMost();
            }
            else
            {
                KeepMonitorOff();
            }
        }

        private void KeepMonitorOff()
        {
            Native.SendMessage(Native.HWND_BROADCAST, Native.WM_SYSCOMMAND,
                (IntPtr)Native.SC_MONITORPOWER, (IntPtr)2);
        }

        private void UpdateTrayText()
        {
            if (tray == null) return;
            string mode = config.Mode == DetectMode.Process ? "进程" : config.Mode == DetectMode.Window ? "窗口" : "手动";
            string state = blanked ? "黑屏中" : "正常";
            if (manualOverride && config.Mode != DetectMode.Manual) state += "(手动接管)";
            tray.Text = "远程黑屏助手 [" + mode + "] " + state;
        }

        // ---------------- 自动检测 ----------------
        private void PollDetection()
        {
            if (config.Mode == DetectMode.Manual) return;
            if (manualOverride) return;
            bool connected = DetectConnected();
            SetBlank(connected);
            UpdateTrayText();
        }

        private bool DetectConnected()
        {
            if (config.Mode == DetectMode.Process)
            {
                Process[] procs = Process.GetProcesses();
                foreach (Process p in procs)
                {
                    try
                    {
                        foreach (string target in config.Processes)
                        {
                            if (Config.ProcessMatches(p.ProcessName, target)) return true;
                        }
                    }
                    catch { }
                }
                return false;
            }

            // Window 模式
            bool found = false;
            List<string> seen = new List<string>();
            Native.EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                if (!Native.IsWindowVisible(hWnd)) return true;

                StringBuilder title = new StringBuilder(512);
                StringBuilder cls = new StringBuilder(256);
                Native.GetWindowText(hWnd, title, 512);
                Native.GetClassName(hWnd, cls, 256);
                string t = title.ToString();
                string c = cls.ToString();

                if (config.LearnMode && BelongsToWatchedProcess(hWnd))
                {
                    seen.Add(string.Format("HWND=0x{0:X} Title=\"{1}\" Class=\"{2}\"",
                        hWnd.ToInt64(), t, c));
                }

                foreach (string kw in config.WindowKeywords)
                {
                    if (kw.Length > 0 && t.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        found = true;
                        return false;
                    }
                }
                foreach (string kw in config.WindowClasses)
                {
                    if (kw.Length > 0 && c.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        found = true;
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);

            if (config.LearnMode && seen.Count > 0) LogLearn(seen);
            return found;
        }

        private bool BelongsToWatchedProcess(IntPtr hWnd)
        {
            uint pid;
            Native.GetWindowThreadProcessId(hWnd, out pid);
            if (pid == 0) return false;
            try
            {
                Process p = Process.GetProcessById((int)pid);
                foreach (string target in config.Processes)
                {
                    if (Config.ProcessMatches(p.ProcessName, target)) return true;
                }
            }
            catch { }
            return false;
        }

        private void LogLearn(List<string> current)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string s in current)
            {
                if (loggedLearn.Add(s))
                {
                    sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + s);
                }
            }
            if (sb.Length > 0)
            {
                try { File.AppendAllText(logPath, sb.ToString(), Encoding.UTF8); }
                catch { }
            }
        }

        // ---------------- 菜单动作 ----------------
        private void SetMode(DetectMode m)
        {
            config.Mode = m;
            manualOverride = false;
            config.Save(configPath);
            RefreshMenuChecks();
            UpdateTrayText();
        }

        private void SetMethod(BlankMethod m)
        {
            config.Method = m;
            config.Save(configPath);
            RefreshMenuChecks();
        }

        private void ToggleLearnMode()
        {
            config.LearnMode = !config.LearnMode;
            config.Save(configPath);
            RefreshMenuChecks();
        }

        private void ToggleAutoStart()
        {
            config.AutoStart = !config.AutoStart;
            config.Save(configPath);
            ApplyAutoStart();
            RefreshMenuChecks();
        }

        private void ApplyAutoStart()
        {
            try
            {
                string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
                using (Microsoft.Win32.RegistryKey key =
                    Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runKey, true))
                {
                    if (key == null) return;
                    string exePath = "\"" + Application.ExecutablePath + "\"";
                    if (config.AutoStart)
                    {
                        key.SetValue("BlackScreenHelper", exePath);
                    }
                    else
                    {
                        key.DeleteValue("BlackScreenHelper", false);
                    }
                }
            }
            catch { }
        }

        private void ShowPanel()
        {
            if (panel == null || panel.IsDisposed) return;
            panel.Show();
            Native.SetWindowPos(panel.Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        private void OpenConfig()
        {
            try
            {
                Process.Start("notepad.exe", "\"" + configPath + "\"");
            }
            catch { }
        }

        // ---------------- 退出 ----------------
        protected override void ExitThreadCore()
        {
            Restore();
            UnregisterHotkey();
            pollTimer.Stop();
            keepAliveTimer.Stop();
            if (tray != null)
            {
                tray.Visible = false;
                tray.Dispose();
            }
            if (overlay != null && !overlay.IsDisposed)
            {
                overlay.Dispose();
            }
            if (panel != null && !panel.IsDisposed)
            {
                panel.Dispose();
            }
            if (messageForm != null && !messageForm.IsDisposed)
            {
                messageForm.Dispose();
            }
            base.ExitThreadCore();
        }
    }

    // ------------------------------------------------------------------
    // 入口
    // ------------------------------------------------------------------
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            System.Threading.Mutex mutex = new System.Threading.Mutex(true, "BlackScreenHelper_SingleInstance", out createdNew);
            if (!createdNew)
            {
                MessageBox.Show("远程黑屏助手已经在运行。", "远程黑屏助手",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new AppContext());
            GC.KeepAlive(mutex);
        }
    }
}
