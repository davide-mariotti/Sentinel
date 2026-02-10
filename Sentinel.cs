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
            // Ensure single instance? (Optional but good for tray apps)
            
            Application.Run(new SentinelContext());
        }
    }

    public class SentinelContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private ContextMenuStrip contextMenu;
        
        private SettingsForm settingsForm;
        
        private bool isAwake = true;
        private const string AppName = "Sentinel";
        private const string RegistryPath = "SOFTWARE\\SentinelApp";

        // Schedule Settings
        private bool isScheduleEnabled = false;
        private TimeSpan scheduleStart;
        private TimeSpan scheduleEnd;

        // P/Invoke signatures
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern uint SetThreadExecutionState(uint esFlags);

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;
        private const uint ES_DISPLAY_REQUIRED = 0x00000002;
        
        private const int KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_F15 = 0x7E; // F15 key

        private Timer inputTimer;

        public SentinelContext()
        {
            // Create Tray Icon
            trayIcon = new NotifyIcon();
            LoadIcon();
            trayIcon.Visible = true;
            trayIcon.MouseDoubleClick += TrayIcon_MouseDoubleClick;

            // Load Settings
            LoadSettings();

            // Initialize Timer for Keep-Alive (every 60 seconds)
            inputTimer = new Timer();
            inputTimer.Interval = 60000; // 60 seconds
            inputTimer.Tick += InputTimer_Tick;

            // Initialize Settings Form
            // We pass the currently loaded settings
            settingsForm = new SettingsForm(isAwake, IsStartupEnabled(), isScheduleEnabled, scheduleStart, scheduleEnd);
            settingsForm.ActiveChanged += SettingsForm_ActiveChanged;
            settingsForm.StartupChanged += SettingsForm_StartupChanged;
            settingsForm.ScheduleChanged += SettingsForm_ScheduleChanged;

            // Create ContextMenuStrip
            contextMenu = new ContextMenuStrip();
            
            // Header (non-clickable)
            ToolStripMenuItem headerItem = new ToolStripMenuItem("Sentinel");
            headerItem.Enabled = false;
            headerItem.Font = new Font(contextMenu.Font, FontStyle.Bold);
            contextMenu.Items.Add(headerItem);
            contextMenu.Items.Add(new ToolStripSeparator());

            // Open Settings
            contextMenu.Items.Add("Impostazioni...", null, new EventHandler((s, e) => ShowSettings()));

            contextMenu.Items.Add(new ToolStripSeparator());

            // Exit
            contextMenu.Items.Add("Esci", null, new EventHandler(Exit));

            trayIcon.ContextMenuStrip = contextMenu;

            // Update Text
            UpdateTrayText();

            // Enable Keep Awake based on default
            SetAwakeState(isAwake);

            // Show settings form on startup as requested
            ShowSettings();
        }

        private void LoadSettings()
        {
            // Defaults
            isAwake = true;
            isScheduleEnabled = false;
            scheduleStart = new TimeSpan(13, 0, 0); // Default 13:00
            scheduleEnd = new TimeSpan(14, 0, 0);   // Default 14:00

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        var awakeVal = key.GetValue("IsAwake");
                        if (awakeVal != null) isAwake = Convert.ToBoolean(awakeVal);

                        var schedEnVal = key.GetValue("ScheduleEnabled");
                        if (schedEnVal != null) isScheduleEnabled = Convert.ToBoolean(schedEnVal);

                        var schedStartVal = key.GetValue("ScheduleStartTicks");
                        if (schedStartVal != null) scheduleStart = TimeSpan.FromTicks(Convert.ToInt64(schedStartVal));

                        var schedEndVal = key.GetValue("ScheduleEndTicks");
                        if (schedEndVal != null) scheduleEnd = TimeSpan.FromTicks(Convert.ToInt64(schedEndVal));
                    }
                }
            }
            catch { /* Ignore errors, use defaults */ }
        }

        private void SaveSettings()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        key.SetValue("IsAwake", isAwake);
                        key.SetValue("ScheduleEnabled", isScheduleEnabled);
                        key.SetValue("ScheduleStartTicks", scheduleStart.Ticks);
                        key.SetValue("ScheduleEndTicks", scheduleEnd.Ticks);
                    }
                }
            }
            catch { /* Ignore */ }
        }

        private void LoadIcon()
        {
            try
            {
                // Try to load sentinel.png and convert to icon
                string pngPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sentinel.png");
                if (File.Exists(pngPath))
                {
                    using (Bitmap bmp = new Bitmap(pngPath))
                    {
                        trayIcon.Icon = Icon.FromHandle(bmp.GetHicon());
                    }
                }
                else
                {
                    // Fallback
                    trayIcon.Icon = SystemIcons.Shield; 
                }
            }
            catch
            {
                 // Try fallback to .ico if png fails or not found
                try 
                {
                     string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sentinel_icon.ico");
                     if (File.Exists(iconPath))
                     {
                         trayIcon.Icon = new Icon(iconPath);
                     }
                     else
                     {
                         trayIcon.Icon = SystemIcons.Shield;
                     }
                }
                catch
                {
                    trayIcon.Icon = SystemIcons.Shield;
                }
            }
        }

        private void InputTimer_Tick(object sender, EventArgs e)
        {
            // Check Schedule
            if (isScheduleEnabled)
            {
                TimeSpan now = DateTime.Now.TimeOfDay;
                bool inPauseRange = false;
                
                if (scheduleStart <= scheduleEnd)
                {
                    // Normal range (e.g. 13:00 to 14:00)
                    if (now >= scheduleStart && now < scheduleEnd)
                        inPauseRange = true;
                }
                else
                {
                    // Overnight range (e.g. 23:00 to 06:00)
                    if (now >= scheduleStart || now < scheduleEnd)
                        inPauseRange = true;
                }

                if (inPauseRange)
                {
                    // We are in the Pause window. 
                    // Ensure the system state allows sleep (Clear ES flags)
                    SetThreadExecutionState(ES_CONTINUOUS);
                    
                    // Update icon text to reflect pause
                    trayIcon.Text = "Sentinel: In Pausa (Programmata)";
                    return; // SKIP keypress
                }
            }

            // If not paused, check if active
            if (isAwake)
            {
                // Ensure keep-awake flags are set (redundancy)
                SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
                
                // Simulate F15 Key Press (invisible to most apps)
                keybd_event(VK_F15, 0, 0, UIntPtr.Zero); // Key Down
                keybd_event(VK_F15, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // Key Up
                
                trayIcon.Text = "Sentinel: Attivo";
            }
            else
            {
                 trayIcon.Text = "Sentinel: Inattivo";
            }
        }

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
            scheduleStart = settingsForm.dtpStartTime.Value.TimeOfDay;
            scheduleEnd = settingsForm.dtpEndTime.Value.TimeOfDay;
            
            // Force an update check immediately?
            // Or just save settings.
            SaveSettings();
            UpdateTrayText();
        }

        private void UpdateTrayText()
        {
            // Logic handled mainly in Timer, but we can set instantaneous state here
             trayIcon.Text = isAwake ? "Sentinel: Attivo" : "Sentinel: Inattivo";
        }

        private void SetAwakeState(bool awake)
        {
            if (awake)
            {
                // Prevent sleep
                SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
                inputTimer.Start();
            }
            else
            {
                // Allow sleep
                SetThreadExecutionState(ES_CONTINUOUS);
                // We keep timer running *if* we want to check for schedule re-activation? 
                // Ah, if "isAwake" is false (globally disabled), we usually stop the timer.
                // But the user might want "Scheduled" to OVERRIDE "Inactive"? 
                // Usually "Active" is the master switch.
                // If "Active" is OFF, the tool does nothing.
                // If "Active" is ON, the tool works UNLESS paused by schedule.
                
                // So if awake is false, we stop timer.
                inputTimer.Stop();
            }
        }

        private void SettingsForm_StartupChanged(object sender, EventArgs e)
        {
            SetStartup(settingsForm.chkStartup.Checked);
        }

        private void ShowSettings()
        {
            if (settingsForm.Visible)
            {
                settingsForm.Activate();
            }
            else
            {
                settingsForm.Show();
            }
        }

        private void TrayIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ShowSettings();
        }

        private bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false))
                {
                    return key.GetValue(AppName) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private void SetStartup(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (enable)
                    {
                        key.SetValue(AppName, Application.ExecutablePath);
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Errore impostazione avvio automatico: " + ex.Message);
            }
        }

        private void Exit(object sender, EventArgs e)
        {
            // Reset state before exit
            SetThreadExecutionState(ES_CONTINUOUS);
            trayIcon.Visible = false;
            settingsForm.Dispose();
            Application.Exit();
        }
    }
}
