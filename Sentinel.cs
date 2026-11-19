using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

namespace SentinelApp
{
    public class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // WebView2 (the settings window) is packed inside the exe: still one file to download.
            AppDomain.CurrentDomain.AssemblyResolve += Libs.Resolve;
            Run(args);
        }

        // Kept apart from Main: the JIT must not meet a library type before the resolver is in place.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Run(string[] args)
        {
            bool first;
            using (var single = new Mutex(true, "Local\\SentinelApp", out first))
            {
                if (!first)
                {
                    MessageBox.Show(L.T("Sentinel è già acceso: lo trovi nell'area di notifica, vicino all'orologio.",
                                        "Sentinel is already running: look for it in the notification area, next to the clock."),
                                    "Sentinel", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool fromStartup = Array.IndexOf(args, "--startup") >= 0;
                Application.Run(new SentinelContext(fromStartup));
            }
        }
    }

    // Italian if Windows speaks Italian, English otherwise. That's the whole i18n framework.
    public static class L
    {
        public static readonly bool It = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "it";
        public static string T(string it, string en) { return It ? it : en; }

        public static Icon AppIcon(string name, Size size)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
                return s == null ? SystemIcons.Shield : new Icon(s, size);
        }
    }

    public static class Libs
    {
        static readonly Dictionary<string, Assembly> Loaded = new Dictionary<string, Assembly>();

        public static Stream Open(string name) { return Assembly.GetExecutingAssembly().GetManifestResourceStream(name); }

        static byte[] Resource(string name)
        {
            using (Stream s = Open(name))
            {
                if (s == null) return null;
                var m = new MemoryStream();
                s.CopyTo(m);
                return m.ToArray();
            }
        }

        public static Assembly Resolve(object sender, ResolveEventArgs e)
        {
            string file = new AssemblyName(e.Name).Name + ".dll";
            lock (Loaded)
            {
                Assembly a;
                if (Loaded.TryGetValue(file, out a)) return a;
                byte[] bytes = Resource(file);
                if (bytes == null) return null;
                a = Assembly.Load(bytes);
                Loaded[file] = a;
                return a;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibrary(string path);

        // WebView2's loader is native: it can't be loaded from memory, so it goes to disk once, the one for this
        // processor (Sentinel runs as x64, ARM64 or x86, whatever Windows is), then into the process by full path.
        public static void LoadWebViewLoader(string folder)
        {
            string arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
            string name = arch == "ARM64" ? "WebView2Loader.arm64.dll" : IntPtr.Size == 4 ? "WebView2Loader.x86.dll" : "WebView2Loader.x64.dll";
            byte[] bytes = Resource(name);
            string path = Path.Combine(folder, "WebView2Loader.dll");
            Directory.CreateDirectory(folder);
            if (!File.Exists(path) || new FileInfo(path).Length != bytes.Length) File.WriteAllBytes(path, bytes);
            if (LoadLibrary(path) == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public static class Json
    {
        static readonly JavaScriptSerializer S = new JavaScriptSerializer();
        public static string Write(object o) { return S.Serialize(o); }
        public static T Read<T>(string s) { return S.Deserialize<T>(s); }
    }

    public class SentinelContext : ApplicationContext
    {
        private NotifyIcon       trayIcon;
        private ContextMenuStrip contextMenu;
        private SettingsForm     settingsForm;
        private Icon             iconOn, iconOff;

        private bool     isAwake           = true;
        private bool     isScheduleEnabled = false;
        private bool     isLocked          = false;   // Win+L: PC stays awake, screen may turn off
        private TimeSpan scheduleStart;
        private TimeSpan scheduleEnd;

        private const string AppName      = "Sentinel";
        private const string RegistryPath = "SOFTWARE\\SentinelApp";
        private const string RunPath      = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

        // Portable version: a Sentinel.ini next to the exe. Settings live in that file and the registry
        // is never touched, autostart included. Same exe: the file alone makes the difference.
        private static readonly string IniPath  = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "Sentinel.ini");
        public  static readonly bool   Portable = File.Exists(IniPath);
        private const string IniHeader = "; Sentinel settings. While this file sits next to Sentinel.exe, nothing goes to the registry.";

        // ── Battery Monitor ───────────────────────────────────────────────────
        private bool batteryMonitorEnabled = true;
        private int  batteryHighThreshold  = 97;   // % – disconnect / stop charging
        private int  batteryLowThreshold   = 15;   // % – connect / resume charging
        private int  highAlertMaxMinutes   = 5;    // balloon mode: max duration of high alert
        // Runtime state (not persisted)
        private bool     _highAlertActive              = false;
        private bool     _highAlertPermanentlyDismissed = false;
        private DateTime _highAlertShownAt             = DateTime.MinValue;
        private bool     _lowAlertActive               = false;

        private Timer batteryTimer;        // 60 s – main check
        private Timer batteryRefreshTimer;  // 28 s – balloon refresh
        // ─────────────────────────────────────────────────────────────────────

        // P/Invoke
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern uint SetThreadExecutionState(uint esFlags);
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint type; public MOUSEINPUT mi; }   // the mouse is the biggest member of the union: same size

        private const uint ES_CONTINUOUS       = 0x80000000;
        private const uint ES_SYSTEM_REQUIRED  = 0x00000001;
        private const uint ES_DISPLAY_REQUIRED = 0x00000002;
        private const uint INPUT_MOUSE         = 0;
        private const uint MOUSEEVENTF_MOVE    = 0x0001;

        // Checked every 20 s, nudged after 30 s still: Windows never sees more than ~50 s of idle,
        // under the shortest screensaver (1 minute). With 60/55 it could reach almost 2 minutes.
        private const int CheckEveryMs = 20000;
        private const int NudgeAfterMs = 30000;

        private Timer inputTimer;
        private Timer statusTimer;

        public SentinelContext(bool fromStartup)
        {
            iconOn  = L.AppIcon("Sentinel.ico",    SystemInformation.SmallIconSize);
            iconOff = L.AppIcon("SentinelOff.ico", SystemInformation.SmallIconSize);

            trayIcon = new NotifyIcon();
            trayIcon.Icon    = iconOn;
            trayIcon.Visible = true;
            trayIcon.MouseDoubleClick += (s, e) => ShowSettings();

            LoadSettings();

            // Keep-alive timer
            inputTimer          = new Timer();
            inputTimer.Interval = CheckEveryMs;
            inputTimer.Tick    += InputTimer_Tick;

            // Battery main check (60 s)
            batteryTimer          = new Timer();
            batteryTimer.Interval = 60000;
            batteryTimer.Tick    += BatteryTimer_Tick;

            // Shared balloon refresh (28 s) – re-shows both high/low balloons before OS auto-dismisses
            batteryRefreshTimer          = new Timer();
            batteryRefreshTimer.Interval = 28000;
            batteryRefreshTimer.Tick    += BatteryRefreshTimer_Tick;

            bool startup = !Portable && IsStartupEnabled();
            if (startup) SetStartup(true);   // refresh the Run entry: exe moved, or written by an older version

            settingsForm = new SettingsForm(Path.Combine(Portable ? Path.GetTempPath() : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sentinel", "web"));
            settingsForm.Message += OnMessage;
            var handle = settingsForm.Handle;   // the window must exist, even hidden

            // while the window is open, it gets the live state once a second
            statusTimer          = new Timer();
            statusTimer.Interval = 1000;
            statusTimer.Tick    += (s, e) => { if (settingsForm.PageReady) settingsForm.Post(Status()); };
            statusTimer.Start();

            contextMenu = new ContextMenuStrip();
            var header = new ToolStripMenuItem("Sentinel");
            header.Enabled = false;
            header.Font    = new Font(contextMenu.Font, FontStyle.Bold);
            contextMenu.Items.Add(header);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(L.T("Impostazioni...", "Settings..."), null, (s, e) => ShowSettings());
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(L.T("Esci", "Exit"), null, Exit);
            trayIcon.ContextMenuStrip = contextMenu;

            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;

            SetAwakeState(isAwake);
            UpdateTray();
            if (batteryMonitorEnabled) batteryTimer.Start();

            if (!fromStartup) ShowSettings();   // at login it stays quiet in the tray
        }

        // ── Keep-Alive ────────────────────────────────────────────────────────

        private bool IsInScheduledPause()
        {
            if (!isScheduleEnabled) return false;
            TimeSpan now = DateTime.Now.TimeOfDay;
            return (scheduleStart <= scheduleEnd)
                ? (now >= scheduleStart && now < scheduleEnd)
                : (now >= scheduleStart || now < scheduleEnd);   // pause across midnight
        }

        private void InputTimer_Tick(object sender, EventArgs e)
        {
            ApplyExecutionState();
            if (isAwake && !isLocked && !IsInScheduledPause() && GetIdleTimeMs() > NudgeAfterMs) Nudge();
            UpdateTray();
        }

        // A mouse "move" of zero pixels. Windows counts it as activity (screensaver, Teams "away",
        // idle lock), the pointer doesn't budge and no app gets a key. The F15 key used before did
        // reach the app in front: in Excel it emptied the selected cell, formula included.
        private static void Nudge()
        {
            INPUT[] input = new INPUT[1];
            input[0].type        = INPUT_MOUSE;
            input[0].mi.dwFlags  = MOUSEEVENTF_MOVE;
            SendInput(1, input, Marshal.SizeOf(typeof(INPUT)));
        }

        // What we ask Windows: nothing when off or paused, PC only when locked, PC + screen otherwise.
        private void ApplyExecutionState()
        {
            if (!isAwake || IsInScheduledPause()) SetThreadExecutionState(ES_CONTINUOUS);
            else if (isLocked)                    SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED);
            else                                  SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
        }

        private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionLock)   isLocked = true;
            if (e.Reason == SessionSwitchReason.SessionUnlock) isLocked = false;
            ApplyExecutionState();
        }

        private uint GetIdleTimeMs()
        {
            LASTINPUTINFO li = new LASTINPUTINFO();
            li.cbSize = (uint)Marshal.SizeOf(li);
            if (!GetLastInputInfo(ref li)) return 0;
            return (uint)Environment.TickCount - li.dwTime;
        }

        // ── Battery Monitor ───────────────────────────────────────────────────

        private void BatteryTimer_Tick(object sender, EventArgs e)
        {
            if (!batteryMonitorEnabled) return;
            CheckBatteryState();
        }

        private void BatteryRefreshTimer_Tick(object sender, EventArgs e)
        {
            PowerStatus ps      = SystemInformation.PowerStatus;
            bool        charger = ps.PowerLineStatus == PowerLineStatus.Online;
            float       f       = ps.BatteryLifePercent;
            if (f < 0 || f > 1) return;
            int pct = (int)Math.Round(f * 100);

            // ── HIGH: persistent until charger unplugged (or max time) ─────────
            if (_highAlertActive)
            {
                bool timeUp = (DateTime.Now - _highAlertShownAt).TotalMinutes >= highAlertMaxMinutes;
                if (!charger || pct < batteryHighThreshold)
                {
                    // Charger unplugged or battery dropped → dismiss permanently this cycle
                    DismissBalloon();
                    _highAlertActive              = false;
                    _highAlertPermanentlyDismissed = false; // condition no longer met, reset
                }
                else if (timeUp)
                {
                    // Max duration reached → dismiss and don't re-show until condition resets
                    DismissBalloon();
                    _highAlertActive              = false;
                    _highAlertPermanentlyDismissed = true;
                }
                else
                {
                    ShowHighBalloon(pct); // re-show while still needed
                }
            }

            // ── LOW: persistent until charger plugged in ───────────────────────
            if (_lowAlertActive)
            {
                if (charger)
                {
                    DismissBalloon();
                    _lowAlertActive = false;
                }
                else
                {
                    ShowLowBalloon(pct); // re-show while still needed
                }
            }

            // Stop shared timer when both alerts are inactive
            if (!_highAlertActive && !_lowAlertActive)
                batteryRefreshTimer.Stop();

            UpdateTray();
        }

        private void CheckBatteryState()
        {
            PowerStatus ps      = SystemInformation.PowerStatus;
            bool        charger = ps.PowerLineStatus == PowerLineStatus.Online;
            float       f       = ps.BatteryLifePercent;

            if (f < 0 || f > 1) return;   // desktop PC, no battery: nothing to watch

            int pct = (int)Math.Round(f * 100);

            // ── HIGH: charger connected + battery at/above threshold ─────────
            if (charger && pct >= batteryHighThreshold)
            {
                if (!_highAlertActive && !_highAlertPermanentlyDismissed)
                {
                    ShowHighBalloon(pct);
                    _highAlertActive  = true;
                    _highAlertShownAt = DateTime.Now;
                    batteryRefreshTimer.Start();
                }
            }
            else
            {
                if (_highAlertActive) DismissBalloon();
                _highAlertActive              = false;
                _highAlertPermanentlyDismissed = false;
                if (!_lowAlertActive) batteryRefreshTimer.Stop();
            }

            // ── LOW: no charger + battery at/below threshold ─────────────────
            if (!charger && pct <= batteryLowThreshold)
            {
                if (!_lowAlertActive)
                {
                    ShowLowBalloon(pct);
                    _lowAlertActive = true;
                    batteryRefreshTimer.Start();
                }
            }
            else
            {
                if (_lowAlertActive)
                {
                    DismissBalloon();
                    _lowAlertActive = false;
                    if (!_highAlertActive) batteryRefreshTimer.Stop();
                }
            }

            UpdateTray();
        }

        private void ShowHighBalloon(int pct)
        {
            trayIcon.ShowBalloonTip(30000,
                L.T("Sentinel – Batteria alta", "Sentinel – Battery high"),
                string.Format(L.T("Batteria al {0}%\nStacca il caricatore, la batteria ringrazia.",
                                  "Battery at {0}%\nUnplug the charger, your battery will thank you."), pct),
                ToolTipIcon.Warning);
        }

        private void ShowLowBalloon(int pct)
        {
            trayIcon.ShowBalloonTip(30000,
                L.T("Sentinel – Batteria bassa", "Sentinel – Battery low"),
                string.Format(L.T("Batteria al {0}%\nCollega il caricatore, adesso.",
                                  "Battery at {0}%\nPlug in the charger, now."), pct),
                ToolTipIcon.Error);
        }

        private void DismissBalloon()
        {
            trayIcon.Visible = false;
            trayIcon.Visible = true;
        }

        // ── Tray icon + text ──────────────────────────────────────────────────

        private void UpdateTray()
        {
            bool   paused = isAwake && IsInScheduledPause();
            string status = !isAwake ? L.T("Spento", "Off")
                          : paused   ? L.T("In pausa (programmata)", "Paused (scheduled)")
                          :            L.T("Attivo", "Active");
            trayIcon.Icon = (isAwake && !paused) ? iconOn : iconOff;

            string batt = "";
            if (batteryMonitorEnabled)
            {
                PowerStatus ps = SystemInformation.PowerStatus;
                float f = ps.BatteryLifePercent;
                if (f >= 0 && f <= 1)
                {
                    int pct     = (int)Math.Round(f * 100);
                    string plug = (ps.PowerLineStatus == PowerLineStatus.Online) ? L.T(" in carica", " charging") : "";
                    batt = string.Format(" | {0}%{1}", pct, plug);
                }
            }

            string full = string.Format(Portable ? "Sentinel portable: {0}{1}" : "Sentinel: {0}{1}", status, batt);
            trayIcon.Text = full.Length > 63 ? full.Substring(0, 63) : full;
        }

        // ── The settings window (a page in WebView2, ui\) ──────────────────────

        private Dictionary<string, object> Settings()
        {
            return new Dictionary<string, object> {
                { "t", "settings" }, { "awake", isAwake }, { "startup", !Portable && IsStartupEnabled() },
                { "schedule", isScheduleEnabled }, { "from", scheduleStart.ToString("hh\\:mm") }, { "to", scheduleEnd.ToString("hh\\:mm") },
                { "battery", batteryMonitorEnabled }, { "high", batteryHighThreshold }, { "low", batteryLowThreshold }, { "highMinutes", highAlertMaxMinutes } };
        }

        private Dictionary<string, object> Status()
        {
            PowerStatus ps = SystemInformation.PowerStatus;
            float f = ps.BatteryLifePercent;
            bool hasBattery = f >= 0 && f <= 1 && ps.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery;
            return new Dictionary<string, object> {
                { "t", "status" }, { "awake", isAwake }, { "paused", isAwake && IsInScheduledPause() }, { "locked", isLocked },
                { "idle", (int)(GetIdleTimeMs() / 1000) }, { "nudgeAfter", NudgeAfterMs / 1000 },
                { "battery", hasBattery ? (object)(int)Math.Round(f * 100) : null }, { "charging", ps.PowerLineStatus == PowerLineStatus.Online } };
        }

        // Everything the window asks for. The page is ours, but what it sends is still checked here.
        private void OnMessage(Dictionary<string, object> m)
        {
            object v;
            switch (Convert.ToString(m.TryGetValue("t", out v) ? v : ""))
            {
                case "ready":
                    settingsForm.Post(new Dictionary<string, object> {
                        { "t", "init" }, { "lang", L.It ? "it" : "en" }, { "portable", Portable },
                        { "version", Assembly.GetExecutingAssembly().GetName().Version.ToString(3) } });
                    settingsForm.Post(Settings());
                    settingsForm.Post(Status());
                    return;
                case "open":
                    string url = m.TryGetValue("url", out v) ? Convert.ToString(v) : "";
                    if (url == "https://github.com/davide-mariotti/Sentinel" || url == "https://ko-fi.com/Z0X32ATNHL")
                        try { Process.Start("explorer.exe", "\"" + url + "\""); } catch { }
                    return;
                case "set":
                    break;
                default:
                    return;
            }

            if (m.TryGetValue("awake", out v))
            {
                isAwake = Convert.ToBoolean(v);
                SetAwakeState(isAwake);
            }
            if (m.TryGetValue("startup", out v) && !Portable) SetStartup(Convert.ToBoolean(v));

            bool schedule = false;
            TimeSpan t;
            if (m.TryGetValue("schedule", out v)) { isScheduleEnabled = Convert.ToBoolean(v); schedule = true; }
            if (m.TryGetValue("from", out v) && TimeSpan.TryParseExact(Convert.ToString(v), "hh\\:mm", CultureInfo.InvariantCulture, out t)) { scheduleStart = t; schedule = true; }
            if (m.TryGetValue("to", out v) && TimeSpan.TryParseExact(Convert.ToString(v), "hh\\:mm", CultureInfo.InvariantCulture, out t)) { scheduleEnd = t; schedule = true; }
            if (schedule) ApplyExecutionState();

            bool battery = false;
            if (m.TryGetValue("battery", out v))     { batteryMonitorEnabled = Convert.ToBoolean(v); battery = true; }
            if (m.TryGetValue("high", out v))        { batteryHighThreshold = Math.Max(1, Math.Min(100, Convert.ToInt32(v))); battery = true; }
            if (m.TryGetValue("low", out v))         { batteryLowThreshold  = Math.Max(1, Math.Min(100, Convert.ToInt32(v))); battery = true; }
            if (m.TryGetValue("highMinutes", out v)) { highAlertMaxMinutes  = Math.Max(1, Math.Min(60, Convert.ToInt32(v))); battery = true; }
            if (battery) ApplyBatterySettings();

            SaveSettings();
            UpdateTray();
            settingsForm.Post(Settings());
            settingsForm.Post(Status());
        }

        private void ApplyBatterySettings()
        {
            // Reset all alert state on settings change
            _highAlertActive              = false;
            _highAlertPermanentlyDismissed = false;
            _lowAlertActive               = false;
            batteryRefreshTimer.Stop();
            DismissBalloon();

            if (batteryMonitorEnabled)
                batteryTimer.Start();
            else
                batteryTimer.Stop();
        }

        // ── Settings: registry, or Sentinel.ini in the portable version ───────

        private void LoadSettings()
        {
            isAwake              = true;
            isScheduleEnabled    = false;
            scheduleStart        = new TimeSpan(13, 0, 0);
            scheduleEnd          = new TimeSpan(14, 0, 0);
            batteryMonitorEnabled = true;
            batteryHighThreshold  = 97;
            batteryLowThreshold   = 15;
            highAlertMaxMinutes   = 5;

            try
            {
                Dictionary<string, string> v = ReadValues();
                string s;
                if (v.TryGetValue("IsAwake",               out s)) isAwake              = Convert.ToBoolean(s);
                if (v.TryGetValue("ScheduleEnabled",       out s)) isScheduleEnabled    = Convert.ToBoolean(s);
                if (v.TryGetValue("ScheduleStartTicks",    out s)) scheduleStart        = TimeSpan.FromTicks(Convert.ToInt64(s));
                if (v.TryGetValue("ScheduleEndTicks",      out s)) scheduleEnd          = TimeSpan.FromTicks(Convert.ToInt64(s));
                if (v.TryGetValue("BatteryMonitorEnabled", out s)) batteryMonitorEnabled = Convert.ToBoolean(s);
                if (v.TryGetValue("BatteryHighThreshold",  out s)) batteryHighThreshold  = Convert.ToInt32(s);
                if (v.TryGetValue("BatteryLowThreshold",   out s)) batteryLowThreshold   = Convert.ToInt32(s);
                if (v.TryGetValue("HighAlertMaxMinutes",   out s)) highAlertMaxMinutes   = Convert.ToInt32(s);
            }
            catch { }   // broken values: defaults win, no drama
        }

        // Every saved value as text: registry values, or "name=value" lines of Sentinel.ini
        private Dictionary<string, string> ReadValues()
        {
            var d = new Dictionary<string, string>();
            if (Portable)
            {
                foreach (string line in File.ReadAllLines(IniPath))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0 && !line.StartsWith(";")) d[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
                return d;
            }
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                if (key != null)
                    foreach (string name in key.GetValueNames())
                        d[name] = Convert.ToString(key.GetValue(name), CultureInfo.InvariantCulture);
            return d;
        }

        private void SaveSettings()
        {
            var values = new Dictionary<string, object>
            {
                { "IsAwake",               isAwake },
                { "ScheduleEnabled",       isScheduleEnabled },
                { "ScheduleStartTicks",    scheduleStart.Ticks },
                { "ScheduleEndTicks",      scheduleEnd.Ticks },
                { "BatteryMonitorEnabled", batteryMonitorEnabled },
                { "BatteryHighThreshold",  batteryHighThreshold },
                { "BatteryLowThreshold",   batteryLowThreshold },
                { "HighAlertMaxMinutes",   highAlertMaxMinutes },
            };
            try
            {
                if (Portable)
                {
                    var lines = new List<string> { IniHeader };
                    foreach (var kv in values) lines.Add(kv.Key + "=" + Convert.ToString(kv.Value, CultureInfo.InvariantCulture));
                    File.WriteAllLines(IniPath, lines.ToArray());
                    return;
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                    if (key != null)
                        foreach (var kv in values) key.SetValue(kv.Key, kv.Value);   // same types as before: bool and long as text, int as DWORD
            }
            catch { }   // read-only stick or locked key: the change doesn't stick, Sentinel keeps working
        }

        // ── Startup / Exit ────────────────────────────────────────────────────

        private void SetAwakeState(bool awake)
        {
            ApplyExecutionState();
            if (awake) inputTimer.Start();
            else       inputTimer.Stop();
        }

        private void ShowSettings()
        {
            if (!settingsForm.Visible) { settingsForm.Show(); return; }
            if (settingsForm.WindowState == FormWindowState.Minimized) settingsForm.WindowState = FormWindowState.Normal;
            settingsForm.Activate();
        }

        private bool IsStartupEnabled()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunPath, false))
                    return k != null && k.GetValue(AppName) != null;
            }
            catch { return false; }
        }

        private void SetStartup(bool enable)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunPath, true))
                {
                    // quoted path: a folder with spaces in the name must not break the autostart
                    if (enable) k.SetValue(AppName, "\"" + Application.ExecutablePath + "\" --startup");
                    else k.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex) { MessageBox.Show(L.T("Avvio automatico non impostato: ", "Couldn't set autostart: ") + ex.Message); }
        }

        private void Exit(object sender, EventArgs e)
        {
            SetThreadExecutionState(ES_CONTINUOUS);
            SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
            inputTimer.Stop();
            statusTimer.Stop();
            batteryTimer.Stop();
            batteryRefreshTimer.Stop();
            trayIcon.Visible = false;
            settingsForm.Dispose();
            Application.Exit();
        }
    }
}
