using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SentinelApp
{
    public class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SentinelContext());
        }
    }

    public class SentinelContext : ApplicationContext
    {
        private NotifyIcon       trayIcon;
        private ContextMenuStrip contextMenu;
        private SettingsForm     settingsForm;

        private bool     isAwake           = true;
        private bool     isScheduleEnabled = false;
        private TimeSpan scheduleStart;
        private TimeSpan scheduleEnd;

        private const string AppName      = "Sentinel";
        private const string RegistryPath = "SOFTWARE\\SentinelApp";

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
        private const byte VK_F15              = 0x7E;

        private Timer inputTimer;

        public SentinelContext()
        {
            trayIcon = new NotifyIcon();
            LoadIcon();
            trayIcon.Visible = true;
            trayIcon.MouseDoubleClick += TrayIcon_MouseDoubleClick;

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

            settingsForm = new SettingsForm(
                isAwake, IsStartupEnabled(), isScheduleEnabled, scheduleStart, scheduleEnd,
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
            contextMenu.Items.Add("Impostazioni...", null, (s, e) => ShowSettings());
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Esci", null, new EventHandler(Exit));
            trayIcon.ContextMenuStrip = contextMenu;

            UpdateTrayText();
            SetAwakeState(isAwake);
            if (batteryMonitorEnabled) batteryTimer.Start();

            ShowSettings();
        }

        // ── Keep-Alive ────────────────────────────────────────────────────────

        private void InputTimer_Tick(object sender, EventArgs e)
        {
            if (isScheduleEnabled)
            {
                TimeSpan now = DateTime.Now.TimeOfDay;
                bool inPause = (scheduleStart <= scheduleEnd)
                    ? (now >= scheduleStart && now < scheduleEnd)
                    : (now >= scheduleStart || now < scheduleEnd);

                if (inPause)
                {
                    SetThreadExecutionState(ES_CONTINUOUS);
                    trayIcon.Text = "Sentinel: In Pausa (Programmata)";
                    return;
                }
            }

            if (isAwake)
            {
                SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
                if (GetIdleTimeMs() > 55000)
                {
                    keybd_event(VK_F15, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_F15, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
            }
            UpdateTrayText();
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
                    ShowLowBalloon(ps); // re-show while still needed
                }
            }

            // Stop shared timer when both alerts are inactive
            if (!_highAlertActive && !_lowAlertActive)
                batteryRefreshTimer.Stop();

            UpdateTrayText();
        }

        private void CheckBatteryState()
        {
            PowerStatus ps      = SystemInformation.PowerStatus;
            bool        charger = ps.PowerLineStatus == PowerLineStatus.Online;
            float       f       = ps.BatteryLifePercent;

            if (f < 0 || f > 1) return;

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
                    ShowLowBalloon(ps);
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

            UpdateTrayText();
        }

        private void ShowHighBalloon(int pct)
        {
            trayIcon.ShowBalloonTip(30000,
                "Sentinel \u2013 Batteria Alta",
                string.Format("\U0001F50B Batteria al {0}%\nStacca il cavo di alimentazione per proteggere la batteria!", pct),
                ToolTipIcon.Warning);
        }

        private void ShowLowBalloon(PowerStatus ps)
        {
            int pct = (int)Math.Round(ps.BatteryLifePercent * 100);
            trayIcon.ShowBalloonTip(30000,
                "Sentinel \u2013 Batteria Bassa",
                string.Format("\U0001F50B Batteria al {0}%\nCollega il caricatore subito!", pct),
                ToolTipIcon.Error);
        }

        private void DismissBalloon()
        {
            trayIcon.Visible = false;
            trayIcon.Visible = true;
        }


        // ── Tray text ─────────────────────────────────────────────────────────

        private void UpdateTrayText()
        {
            string status = isAwake ? "Attivo" : "Inattivo";
            string batt   = "";

            if (batteryMonitorEnabled)
            {
                PowerStatus ps = SystemInformation.PowerStatus;
                float f = ps.BatteryLifePercent;
                if (f >= 0 && f <= 1)
                {
                    int pct    = (int)Math.Round(f * 100);
                    string plug = (ps.PowerLineStatus == PowerLineStatus.Online) ? "\u26a1" : "\U0001f50b";
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
            UpdateTrayText();
            SaveSettings();
        }

        private void SettingsForm_ScheduleChanged(object sender, EventArgs e)
        {
            isScheduleEnabled = settingsForm.chkScheduleEnabled.Checked;
            scheduleStart     = settingsForm.dtpStartTime.Value.TimeOfDay;
            scheduleEnd       = settingsForm.dtpEndTime.Value.TimeOfDay;
            SaveSettings();
            UpdateTrayText();
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
            UpdateTrayText();
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
            catch { }
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
            if (awake)
            {
                SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
                inputTimer.Start();
            }
            else
            {
                SetThreadExecutionState(ES_CONTINUOUS);
                inputTimer.Stop();
            }
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

        private void TrayIcon_MouseDoubleClick(object sender, MouseEventArgs e) { ShowSettings(); }

        private void LoadIcon()
        {
            try
            {
                string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sentinel.png");
                if (File.Exists(p)) { using (Bitmap b = new Bitmap(p)) trayIcon.Icon = Icon.FromHandle(b.GetHicon()); }
                else trayIcon.Icon = SystemIcons.Shield;
            }
            catch
            {
                try
                {
                    string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sentinel_icon.ico");
                    trayIcon.Icon = File.Exists(p) ? new Icon(p) : SystemIcons.Shield;
                }
                catch { trayIcon.Icon = SystemIcons.Shield; }
            }
        }

        private bool IsStartupEnabled()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false))
                    return k.GetValue(AppName) != null;
            }
            catch { return false; }
        }

        private void SetStartup(bool enable)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (enable) k.SetValue(AppName, Application.ExecutablePath);
                    else k.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex) { MessageBox.Show("Errore avvio automatico: " + ex.Message); }
        }

        private void Exit(object sender, EventArgs e)
        {
            SetThreadExecutionState(ES_CONTINUOUS);
            batteryTimer.Stop();
            batteryRefreshTimer.Stop();
            trayIcon.Visible = false;
            settingsForm.Dispose();
            Application.Exit();
        }
    }
}
