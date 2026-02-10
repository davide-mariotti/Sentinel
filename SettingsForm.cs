using System;
using System.Drawing;
using System.Windows.Forms;

namespace SentinelApp
{
    public class SettingsForm : Form
    {
        public CheckBox chkActive;
        public CheckBox chkStartup;
        
        // Schedule Controls
        public CheckBox chkScheduleEnabled;
        public DateTimePicker dtpStartTime;
        public DateTimePicker dtpEndTime;
        
        private Label lblTitle;
        private Button btnClose;
        private GroupBox grpSchedule;
        private Label lblStart;
        private Label lblEnd;

        public event EventHandler ActiveChanged;
        public event EventHandler StartupChanged;
        public event EventHandler ScheduleChanged;

        public SettingsForm(bool initialActive, bool initialStartup, bool initialSchedule, TimeSpan start, TimeSpan end)
        {
            InitializeComponent();
            
            // Set initial values
            chkActive.Checked = initialActive;
            chkStartup.Checked = initialStartup;
            chkScheduleEnabled.Checked = initialSchedule;
            
            // Set DateTimePickers to today's date + time
            DateTime today = DateTime.Today;
            dtpStartTime.Value = today.Add(start);
            dtpEndTime.Value = today.Add(end);
            
            // Enable/Disable times based on checkbox
            dtpStartTime.Enabled = chkScheduleEnabled.Checked;
            dtpEndTime.Enabled = chkScheduleEnabled.Checked;

            // Wire up events
            chkActive.CheckedChanged += (s, e) => { if (ActiveChanged != null) ActiveChanged(this, EventArgs.Empty); };
            chkStartup.CheckedChanged += (s, e) => { if (StartupChanged != null) StartupChanged(this, EventArgs.Empty); };
            
            chkScheduleEnabled.CheckedChanged += (s, e) => 
            { 
                dtpStartTime.Enabled = chkScheduleEnabled.Checked;
                dtpEndTime.Enabled = chkScheduleEnabled.Checked;
                if (ScheduleChanged != null) ScheduleChanged(this, EventArgs.Empty); 
            };
            
            dtpStartTime.ValueChanged += (s, e) => { if (ScheduleChanged != null) ScheduleChanged(this, EventArgs.Empty); };
            dtpEndTime.ValueChanged += (s, e) => { if (ScheduleChanged != null) ScheduleChanged(this, EventArgs.Empty); };
        }

        private void InitializeComponent()
        {
            this.chkActive = new CheckBox();
            this.chkStartup = new CheckBox();
            this.lblTitle = new Label();
            this.btnClose = new Button();
            this.grpSchedule = new GroupBox();
            this.chkScheduleEnabled = new CheckBox();
            this.dtpStartTime = new DateTimePicker();
            this.dtpEndTime = new DateTimePicker();
            this.lblStart = new Label();
            this.lblEnd = new Label();

            this.SuspendLayout();

            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, ((byte)(0)));
            this.lblTitle.Location = new Point(12, 9);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new Size(100, 21);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Settings"; // Changed from "Sentinel" as requested implied by "Window Title" request, but user said "la finestra chiamala settings"

            // 
            // chkActive
            // 
            this.chkActive.AutoSize = true;
            this.chkActive.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(0)));
            this.chkActive.Location = new Point(20, 40);
            this.chkActive.Name = "chkActive";
            this.chkActive.Size = new Size(100, 23);
            this.chkActive.TabIndex = 1;
            this.chkActive.Text = "Attivo (Previene Standby)";
            this.chkActive.UseVisualStyleBackColor = true;

            // 
            // chkStartup
            // 
            this.chkStartup.AutoSize = true;
            this.chkStartup.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point, ((byte)(0)));
            this.chkStartup.Location = new Point(20, 70);
            this.chkStartup.Name = "chkStartup";
            this.chkStartup.Size = new Size(150, 23);
            this.chkStartup.TabIndex = 2;
            this.chkStartup.Text = "Esegui all'avvio";
            this.chkStartup.UseVisualStyleBackColor = true;

            // 
            // grpSchedule
            // 
            this.grpSchedule.Location = new Point(16, 100);
            this.grpSchedule.Name = "grpSchedule";
            this.grpSchedule.Size = new Size(200, 100);
            this.grpSchedule.TabIndex = 4;
            this.grpSchedule.TabStop = false;
            this.grpSchedule.Text = "Sospensione Programmata"; // "Scheduled Suspension"
            this.grpSchedule.Controls.Add(this.chkScheduleEnabled);
            this.grpSchedule.Controls.Add(this.lblStart);
            this.grpSchedule.Controls.Add(this.dtpStartTime);
            this.grpSchedule.Controls.Add(this.lblEnd);
            this.grpSchedule.Controls.Add(this.dtpEndTime);

            // 
            // chkScheduleEnabled
            // 
            this.chkScheduleEnabled.AutoSize = true;
            this.chkScheduleEnabled.Location = new Point(10, 20);
            this.chkScheduleEnabled.Name = "chkScheduleEnabled";
            this.chkScheduleEnabled.Size = new Size(60, 17);
            this.chkScheduleEnabled.TabIndex = 0;
            this.chkScheduleEnabled.Text = "Abilita (Pausa in Mensa)";
            this.chkScheduleEnabled.UseVisualStyleBackColor = true;

            // 
            // lblStart / dtpStartTime
            // 
            this.lblStart.AutoSize = true;
            this.lblStart.Location = new Point(7, 48);
            this.lblStart.Name = "lblStart";
            this.lblStart.Size = new Size(34, 13);
            this.lblStart.Text = "Inizio:";
            
            this.dtpStartTime.Format = DateTimePickerFormat.Time;
            this.dtpStartTime.ShowUpDown = true;
            this.dtpStartTime.Location = new Point(45, 45);
            this.dtpStartTime.Name = "dtpStartTime";
            this.dtpStartTime.Size = new Size(65, 20);
            this.dtpStartTime.TabIndex = 1;

            // 
            // lblEnd / dtpEndTime
            // 
            this.lblEnd.AutoSize = true;
            this.lblEnd.Location = new Point(7, 73);
            this.lblEnd.Name = "lblEnd";
            this.lblEnd.Size = new Size(30, 13);
            this.lblEnd.Text = "Fine:";
            
            this.dtpEndTime.Format = DateTimePickerFormat.Time;
            this.dtpEndTime.ShowUpDown = true;
            this.dtpEndTime.Location = new Point(45, 70);
            this.dtpEndTime.Name = "dtpEndTime";
            this.dtpEndTime.Size = new Size(65, 20);
            this.dtpEndTime.TabIndex = 2;


            // 
            // btnClose
            // 
            this.btnClose.Location = new Point(70, 210);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new Size(80, 30);
            this.btnClose.TabIndex = 3;
            this.btnClose.Text = "Chiudi";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += (s, e) => this.Hide(); // Hide to tray

            // 
            // SettingsForm
            // 
            this.ClientSize = new Size(240, 250);
            this.Controls.Add(this.grpSchedule);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.chkStartup);
            this.Controls.Add(this.chkActive);
            this.Controls.Add(this.lblTitle);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.ShowInTaskbar = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            // Display version in title
            string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            // Simplify to Major.Minor.Build if needed, or keeping full string
            this.Text = "Settings - v" + version; 
            
            try {
                if (System.IO.File.Exists("sentinel.ico"))
                    this.Icon = new Icon("sentinel.ico");
            } catch {}

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
            base.OnFormClosing(e);
        }
    }
}
