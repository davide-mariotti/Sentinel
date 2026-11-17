using System;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
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
        static readonly bool It = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "it";
        public static string T(string it, string en) { return It ? it : en; }

        public static Icon AppIcon(string name, Size size)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
                return s == null ? SystemIcons.Shield : new Icon(s, size);
        }
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
        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

        private const uint ES_CONTINUOUS       = 0x80000000;
        private const uint ES_SYSTEM_REQUIRED  = 0x00000001;
        private const uint ES_DISPLAY_REQUIRED = 0x00000002;
        private const int  KEYEVENTF_KEYUP     = 0x0002;
        private const byte VK_F15              = 0x7E;   // a key no keyboard has: nobody notices it

        private Timer inputTimer;

        public SentinelContext(bool fromStartup)
        {
            iconOn  = L.AppIcon("Sentinel.ico",    SystemInformation.SmallIconSize);
            iconOff = L.AppIcon("SentinelOff.ico", SystemInformation.SmallIconSize);

            trayIcon = new NotifyIcon();
            trayIcon.Icon    = iconOn;
            trayIcon.Visible = true;
            trayIcon.MouseDoubleClick += (s, e) => ShowSettings();

            LoadSettings();

            // Keep-alive timer (60 s)
            inputTimer          = new Timer();
            inputTimer.Interval = 60000;
            inputTimer.Tick    += InputTimer_Tick;

            // Battery main check (60 s)
            batteryTimer          = new Timer();
            batteryTimer.Interval = 60000;
            batteryTimer.Tick    += BatteryTimer_Tick;

            // Shared balloon refresh (28 s) – re-shows both high/low balloons before OS auto-dismisses
            batteryRefreshTimer          = new Timer();
            batteryRefreshTimer.Interval = 28000;
            batteryRefreshTimer.Tick    += BatteryRefreshTimer_Tick;

            bool startup = IsStartupEnabled();
            if (startup) SetStartup(true);   // refresh the Run entry: exe moved, or written by an older version

            settingsForm = new SettingsForm(
                isAwake, startup, isScheduleEnabled, scheduleStart, scheduleEnd,
                batteryMonitorEnabled, batteryHighThreshold, batteryLowThreshold, highAlertMaxMinutes);

            settingsForm.ActiveChanged          += SettingsForm_ActiveChanged;
            settingsForm.StartupChanged         += SettingsForm_StartupChanged;
            settingsForm.ScheduleChanged        += SettingsForm_ScheduleChanged;
            settingsForm.BatterySettingsChanged += SettingsForm_BatterySettingsChanged;

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
            if (isAwake && !isLocked && !IsInScheduledPause() && GetIdleTimeMs() > 55000)
            {
                keybd_event(VK_F15, 0, 0, UIntPtr.Zero);
                keybd_event(VK_F15, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            UpdateTray();
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
                string.Format(L.T("\U0001F50B Batteria al {0}%\nStacca il caricatore, la batteria ringrazia.",
                                  "\U0001F50B Battery at {0}%\nUnplug the charger, your battery will thank you."), pct),
                ToolTipIcon.Warning);
        }

        private void ShowLowBalloon(int pct)
        {
            trayIcon.ShowBalloonTip(30000,
                L.T("Sentinel – Batteria bassa", "Sentinel – Battery low"),
                string.Format(L.T("\U0001F50B Batteria al {0}%\nCollega il caricatore, adesso.",
                                  "\U0001F50B Battery at {0}%\nPlug in the charger, now."), pct),
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
                    int pct    = (int)Math.Round(f * 100);
                    string plug = (ps.PowerLineStatus == PowerLineStatus.Online) ? "⚡" : "\U0001f50b";
                    batt = string.Format(" | {0}{1}%", plug, pct);
                }
            }

            string full = string.Format("Sentinel: {0}{1}", status, batt);
            trayIcon.Text = full.Length > 63 ? full.Substring(0, 63) : full;
        }

        // ── Settings sync ─────────────────────────────────────────────────────

        private void SettingsForm_ActiveChanged(object sender, EventArgs e)
        {
            isAwake = settingsForm.chkActive.Checked;
            SetAwakeState(isAwake);
            UpdateTray();
            SaveSettings();
        }

        private void SettingsForm_ScheduleChanged(object sender, EventArgs e)
        {
            isScheduleEnabled = settingsForm.chkScheduleEnabled.Checked;
            scheduleStart     = settingsForm.dtpStartTime.Value.TimeOfDay;
            scheduleEnd       = settingsForm.dtpEndTime.Value.TimeOfDay;
            SaveSettings();
            ApplyExecutionState();
            UpdateTray();
        }

        private void SettingsForm_BatterySettingsChanged(object sender, EventArgs e)
        {
            batteryMonitorEnabled = settingsForm.chkBatteryMonitor.Checked;
            batteryHighThreshold  = (int)settingsForm.nudHighThreshold.Value;
            batteryLowThreshold   = (int)settingsForm.nudLowThreshold.Value;
            highAlertMaxMinutes   = (int)settingsForm.nudHighMaxMinutes.Value;

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

            SaveSettings();
            UpdateTray();
        }

        // ── Registry ──────────────────────────────────────────────────────────

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
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key == null) return;
                    object v;

                    v = key.GetValue("IsAwake");              if (v != null) isAwake              = Convert.ToBoolean(v);
                    v = key.GetValue("ScheduleEnabled");      if (v != null) isScheduleEnabled    = Convert.ToBoolean(v);
                    v = key.GetValue("ScheduleStartTicks");   if (v != null) scheduleStart        = TimeSpan.FromTicks(Convert.ToInt64(v));
                    v = key.GetValue("ScheduleEndTicks");     if (v != null) scheduleEnd          = TimeSpan.FromTicks(Convert.ToInt64(v));
                    v = key.GetValue("BatteryMonitorEnabled");if (v != null) batteryMonitorEnabled = Convert.ToBoolean(v);
                    v = key.GetValue("BatteryHighThreshold"); if (v != null) batteryHighThreshold  = Convert.ToInt32(v);
                    v = key.GetValue("BatteryLowThreshold");  if (v != null) batteryLowThreshold   = Convert.ToInt32(v);
                    v = key.GetValue("HighAlertMaxMinutes");  if (v != null) highAlertMaxMinutes   = Convert.ToInt32(v);
                }
            }
            catch { }   // broken registry values: defaults win, no drama
        }

        private void SaveSettings()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key == null) return;
                    key.SetValue("IsAwake",               isAwake);
                    key.SetValue("ScheduleEnabled",       isScheduleEnabled);
                    key.SetValue("ScheduleStartTicks",    scheduleStart.Ticks);
                    key.SetValue("ScheduleEndTicks",      scheduleEnd.Ticks);
                    key.SetValue("BatteryMonitorEnabled", batteryMonitorEnabled);
                    key.SetValue("BatteryHighThreshold",  batteryHighThreshold);
                    key.SetValue("BatteryLowThreshold",   batteryLowThreshold);
                    key.SetValue("HighAlertMaxMinutes",   highAlertMaxMinutes);
                }
            }
            catch { }
        }

        // ── Startup / Exit ────────────────────────────────────────────────────

        private void SetAwakeState(bool awake)
        {
            ApplyExecutionState();
            if (awake) inputTimer.Start();
            else       inputTimer.Stop();
        }

        private void SettingsForm_StartupChanged(object sender, EventArgs e)
        {
            SetStartup(settingsForm.chkStartup.Checked);
        }

        private void ShowSettings()
        {
            if (settingsForm.Visible) settingsForm.Activate();
            else settingsForm.Show();
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
            batteryTimer.Stop();
            batteryRefreshTimer.Stop();
            trayIcon.Visible = false;
            settingsForm.Dispose();
            Application.Exit();
        }
    }
}
