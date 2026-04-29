using System;
using System.Drawing;
using System.Windows.Forms;

namespace SentinelApp
{
    public class SettingsForm : Form
    {
        // ── Keep-Alive ────────────────────────────────────────────────────────
        public CheckBox chkActive;
        public CheckBox chkStartup;

        // ── Schedule ──────────────────────────────────────────────────────────
        public CheckBox       chkScheduleEnabled;
        public DateTimePicker dtpStartTime;
        public DateTimePicker dtpEndTime;

        // ── Battery ────────────────────────────────────────────────────────────
        public CheckBox      chkBatteryMonitor;
        public NumericUpDown nudHighThreshold;
        public NumericUpDown nudLowThreshold;
        public NumericUpDown nudHighMaxMinutes;

        // ── Private layout ────────────────────────────────────────────────────
        private Label    lblTitle;
        private Button   btnClose;
        private GroupBox grpSchedule;
        private Label    lblStart, lblEnd;
        private GroupBox grpBattery;
        private Label    lblHighThreshold, lblHighPct;
        private Label    lblLowThreshold,  lblLowPct;
        private Label    lblHighMax,        lblHighMaxMin;

        // ── Events ────────────────────────────────────────────────────────────
        public event EventHandler ActiveChanged;
        public event EventHandler StartupChanged;
        public event EventHandler ScheduleChanged;
        public event EventHandler BatterySettingsChanged;

        public SettingsForm(
            bool initialActive, bool initialStartup,
            bool initialSchedule, TimeSpan start, TimeSpan end,
            bool batteryEnabled, int highThreshold, int lowThreshold, int highMaxMinutes)
        {
            InitializeComponent();

            chkActive.Checked          = initialActive;
            chkStartup.Checked         = initialStartup;
            chkScheduleEnabled.Checked = initialSchedule;

            DateTime today = DateTime.Today;
            dtpStartTime.Value   = today.Add(start);
            dtpEndTime.Value     = today.Add(end);
            dtpStartTime.Enabled = initialSchedule;
            dtpEndTime.Enabled   = initialSchedule;

            chkBatteryMonitor.Checked = batteryEnabled;
            nudHighThreshold.Value    = highThreshold;
            nudLowThreshold.Value     = lowThreshold;
            nudHighMaxMinutes.Value   = highMaxMinutes;
            SetBatteryControlsEnabled(batteryEnabled);

            // Events
            chkActive.CheckedChanged  += (s, e) => Fire(ActiveChanged);
            chkStartup.CheckedChanged += (s, e) => Fire(StartupChanged);

            chkScheduleEnabled.CheckedChanged += (s, e) =>
            {
                dtpStartTime.Enabled = chkScheduleEnabled.Checked;
                dtpEndTime.Enabled   = chkScheduleEnabled.Checked;
                Fire(ScheduleChanged);
            };
            dtpStartTime.ValueChanged += (s, e) => Fire(ScheduleChanged);
            dtpEndTime.ValueChanged   += (s, e) => Fire(ScheduleChanged);

            chkBatteryMonitor.CheckedChanged += (s, e) =>
            {
                SetBatteryControlsEnabled(chkBatteryMonitor.Checked);
                Fire(BatterySettingsChanged);
            };
            nudHighThreshold.ValueChanged  += (s, e) => Fire(BatterySettingsChanged);
            nudLowThreshold.ValueChanged   += (s, e) => Fire(BatterySettingsChanged);
            nudHighMaxMinutes.ValueChanged  += (s, e) => Fire(BatterySettingsChanged);
        }

        private void Fire(EventHandler ev) { if (ev != null) ev(this, EventArgs.Empty); }

        private void SetBatteryControlsEnabled(bool on)
        {
            nudHighThreshold.Enabled  = on;
            nudLowThreshold.Enabled   = on;
            nudHighMaxMinutes.Enabled = on;
        }

        private void InitializeComponent()
        {
            this.chkActive         = new CheckBox();
            this.chkStartup        = new CheckBox();
            this.lblTitle          = new Label();
            this.btnClose          = new Button();

            this.grpSchedule        = new GroupBox();
            this.chkScheduleEnabled = new CheckBox();
            this.dtpStartTime       = new DateTimePicker();
            this.dtpEndTime         = new DateTimePicker();
            this.lblStart           = new Label();
            this.lblEnd             = new Label();

            this.grpBattery        = new GroupBox();
            this.chkBatteryMonitor = new CheckBox();
            this.nudHighThreshold  = new NumericUpDown();
            this.nudLowThreshold   = new NumericUpDown();
            this.nudHighMaxMinutes = new NumericUpDown();
            this.lblHighThreshold  = new Label();
            this.lblHighPct        = new Label();
            this.lblLowThreshold   = new Label();
            this.lblLowPct         = new Label();
            this.lblHighMax        = new Label();
            this.lblHighMaxMin     = new Label();

            this.grpSchedule.SuspendLayout();
            this.grpBattery.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)this.nudHighThreshold).BeginInit();
            ((System.ComponentModel.ISupportInitialize)this.nudLowThreshold).BeginInit();
            ((System.ComponentModel.ISupportInitialize)this.nudHighMaxMinutes).BeginInit();
            this.SuspendLayout();

            // lblTitle
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font     = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
            this.lblTitle.Location = new Point(12, 9);
            this.lblTitle.Text     = "Settings";

            // chkActive
            this.chkActive.AutoSize = true;
            this.chkActive.Font     = new Font("Segoe UI", 10F);
            this.chkActive.Location = new Point(20, 40);
            this.chkActive.Text     = "Attivo (Previene Standby)";

            // chkStartup
            this.chkStartup.AutoSize = true;
            this.chkStartup.Font     = new Font("Segoe UI", 10F);
            this.chkStartup.Location = new Point(20, 68);
            this.chkStartup.Text     = "Esegui all'avvio";

            // grpSchedule
            this.grpSchedule.Location = new Point(12, 100);
            this.grpSchedule.Size     = new Size(276, 100);
            this.grpSchedule.Text     = "Sospensione Programmata";
            this.grpSchedule.Controls.AddRange(new Control[] {
                this.chkScheduleEnabled,
                this.lblStart, this.dtpStartTime,
                this.lblEnd,   this.dtpEndTime
            });

            this.chkScheduleEnabled.AutoSize = true;
            this.chkScheduleEnabled.Location = new Point(10, 20);
            this.chkScheduleEnabled.Text     = "Abilita (Pausa in Mensa)";

            this.lblStart.AutoSize = true;
            this.lblStart.Location = new Point(7, 50);
            this.lblStart.Text     = "Inizio:";

            this.dtpStartTime.Format     = DateTimePickerFormat.Time;
            this.dtpStartTime.ShowUpDown = true;
            this.dtpStartTime.Location   = new Point(55, 47);
            this.dtpStartTime.Size       = new Size(70, 20);

            this.lblEnd.AutoSize = true;
            this.lblEnd.Location = new Point(7, 75);
            this.lblEnd.Text     = "Fine:";

            this.dtpEndTime.Format     = DateTimePickerFormat.Time;
            this.dtpEndTime.ShowUpDown = true;
            this.dtpEndTime.Location   = new Point(55, 72);
            this.dtpEndTime.Size       = new Size(70, 20);

            // grpBattery
            this.grpBattery.Location = new Point(12, 212);
            this.grpBattery.Size     = new Size(276, 145);
            this.grpBattery.Text     = "Monitoraggio Batteria";
            this.grpBattery.Controls.AddRange(new Control[] {
                this.chkBatteryMonitor,
                this.lblHighThreshold, this.nudHighThreshold, this.lblHighPct,
                this.lblLowThreshold,  this.nudLowThreshold,  this.lblLowPct,
                this.lblHighMax,       this.nudHighMaxMinutes, this.lblHighMaxMin
            });

            // chkBatteryMonitor
            this.chkBatteryMonitor.AutoSize = true;
            this.chkBatteryMonitor.Font     = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            this.chkBatteryMonitor.Location = new Point(10, 20);
            this.chkBatteryMonitor.Text     = "Abilita avvisi batteria";

            // Row 1 – High threshold
            this.lblHighThreshold.AutoSize = true;
            this.lblHighThreshold.Location = new Point(10, 50);
            this.lblHighThreshold.Text     = "Soglia Alta (scollega):";

            this.nudHighThreshold.Location = new Point(158, 47);
            this.nudHighThreshold.Size     = new Size(52, 20);
            this.nudHighThreshold.Minimum  = 1;
            this.nudHighThreshold.Maximum  = 100;
            this.nudHighThreshold.Value    = 97;

            this.lblHighPct.AutoSize = true;
            this.lblHighPct.Location = new Point(215, 50);
            this.lblHighPct.Text     = "%";

            // Row 2 – Low threshold
            this.lblLowThreshold.AutoSize = true;
            this.lblLowThreshold.Location = new Point(10, 80);
            this.lblLowThreshold.Text     = "Soglia Bassa (attacca):";

            this.nudLowThreshold.Location = new Point(158, 77);
            this.nudLowThreshold.Size     = new Size(52, 20);
            this.nudLowThreshold.Minimum  = 1;
            this.nudLowThreshold.Maximum  = 100;
            this.nudLowThreshold.Value    = 15;

            this.lblLowPct.AutoSize = true;
            this.lblLowPct.Location = new Point(215, 80);
            this.lblLowPct.Text     = "%";

            // Row 3 – High alert max duration
            this.lblHighMax.AutoSize = true;
            this.lblHighMax.Location = new Point(10, 110);
            this.lblHighMax.Text     = "Durata max avviso alta:";

            this.nudHighMaxMinutes.Location = new Point(158, 107);
            this.nudHighMaxMinutes.Size     = new Size(52, 20);
            this.nudHighMaxMinutes.Minimum  = 1;
            this.nudHighMaxMinutes.Maximum  = 60;
            this.nudHighMaxMinutes.Value    = 5;

            this.lblHighMaxMin.AutoSize = true;
            this.lblHighMaxMin.Location = new Point(215, 110);
            this.lblHighMaxMin.Text     = "min";

            // btnClose
            this.btnClose.Location = new Point(100, 370);
            this.btnClose.Size     = new Size(80, 30);
            this.btnClose.Text     = "Chiudi";
            this.btnClose.Click   += (s, e) => this.Hide();

            // Form
            this.ClientSize = new Size(304, 415);
            this.Controls.AddRange(new Control[] {
                this.lblTitle,
                this.chkActive,
                this.chkStartup,
                this.grpSchedule,
                this.grpBattery,
                this.btnClose
            });

            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox     = false;
            this.MinimizeBox     = true;
            this.ShowInTaskbar   = true;
            this.StartPosition   = FormStartPosition.CenterScreen;

            string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            this.Text = "Settings - v" + version;

            try { if (System.IO.File.Exists("sentinel.ico")) this.Icon = new Icon("sentinel.ico"); }
            catch { }

            ((System.ComponentModel.ISupportInitialize)this.nudHighThreshold).EndInit();
            ((System.ComponentModel.ISupportInitialize)this.nudLowThreshold).EndInit();
            ((System.ComponentModel.ISupportInitialize)this.nudHighMaxMinutes).EndInit();
            this.grpSchedule.ResumeLayout(false);
            this.grpBattery.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; this.Hide(); }
            base.OnFormClosing(e);
        }
    }
}
