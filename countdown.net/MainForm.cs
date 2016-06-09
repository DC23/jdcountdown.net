﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms;

namespace CountdownTimer
{
    public partial class MainForm : Form
    {
        Stopwatch timer = new Stopwatch();
        TimeSpan setTime = new TimeSpan();
        Timer updateTick = new Timer();
        SoundPlayer soundPlayer = new SoundPlayer();
        bool pomodoroBreak = false;
        DateTime start = DateTime.Now;
        string originalResetButtonText;
        List<Button> presetButtons = new List<Button>();
        UserProperties userProperties = null;

        public MainForm()
        {
            InitializeComponent();

            originalResetButtonText = buttonReset.Text;

            // preset buttons
            presetButtons.Add(buttonPreset1);
            presetButtons.Add(buttonPreset2);
            presetButtons.Add(buttonPreset3);
            presetButtons.Add(buttonPreset4);
            presetButtons.Add(buttonPreset5);
            presetButtons.Add(buttonPreset6);
            presetButtons.Add(buttonPreset7);
            presetButtons.Add(buttonPreset8);

            UserProperties = UserProperties.Load();

            // other setup
            updateTick.Tick += updateTick_Tick;
            updateTick.Interval = 200;

            soundPlayer.SoundLocation = "TimesUp.wav";
            soundPlayer.Load();

            UpdateStatusText();
        }

        private void UpdatePresets()
        {
            for (int i = 0; i < UserProperties.Presets.Length; i++)
            {
                if (i < presetButtons.Count)
                    SetPresetButtonTime(presetButtons[i], UserProperties.Presets[i]);
            }

            UpdateButtonStates();
        }

        void SetPresetButtonTime(Button button, TimeSpan timeSpan)
        {
            if (timeSpan.Hours > 0)
                button.Text = String.Format("{0:D2}:{1:D2}:{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
            else
                button.Text = String.Format("{0:D2}:{1:D2}", timeSpan.Minutes, timeSpan.Seconds);

            button.Tag = timeSpan;
        }

        void UpdateButtonStates()
        {
            if (PomodoroMode)
            {
                foreach (var preset in presetButtons)
                    preset.Visible = false;

                buttonSet.Visible = false;
                buttonReset.Visible = PomodoroBreak;
            }
            else
            {
                foreach (var preset in presetButtons)
                {
                    preset.Visible = true;
                    preset.Enabled = IsStopped;
                }

                buttonSet.Visible = true;
                buttonSet.Enabled = IsStopped;
                buttonReset.Visible = true;
                buttonReset.Enabled = IsStopped;
            }

            radioButtonStopwatch.Enabled = false;// IsStopped;
            radioButtonTimer.Enabled = IsStopped;
        }

        private void UpdateProperties()
        {
            PomodoroMode = UserProperties.PomodoroMode;
            labelTimer.ForeColor = UserProperties.FontColor;
            labelTimer.Font = UserProperties.TimerFont;
            Opacity = UserProperties.Opacity;

            if (PomodoroMode)
            {
                if (pomodoroBreak)
                    SetFormColor(UserProperties.PomodoroBreakColor);
                else
                    SetFormColor(UserProperties.PomodoroColor);
            }
            else
            {
                SetFormColor(UserProperties.TimerColor);
            }

            FormBorderStyle = UserProperties.Border;
            TopMost = UserProperties.TopMost;
            UpdatePresets();
        }

        void UpdateStatusText()
        {
            TimeSpan up = Uptime;
            if (PomodoroMode)
            {
                toolStripStatusLabel1.Text = string.Format("Completed: {0}    Aborted: {1}",
                    CompletedPomodoroCount,
                    AbortedPomodoroCount);
            }
            else
            {
                toolStripStatusLabel1.Text = string.Format("Uptime: {0:D2}:{1:D2}",
                    up.Hours,
                    up.Minutes);
            }
        }

        private void Reset()
        {
            timer.Reset();
            SetTime = SetTime; // set the current set time back into itself. Hacky way to refresh the display

            // skip the break and change state back to the next pomodoro
            if (PomodoroMode && PomodoroBreak)
                PomodoroBreak = !PomodoroBreak;
        }

        void Stop()
        {
            if (IsRunning)
                ToggleTimerState(false);
        }

        private void ToggleTimerState(bool canceled)
        {
            if (IsRunning)
            {
                // pause/abort
                timer.Stop();
                buttonStartPause.Text = "&Start";
                updateTick.Enabled = false;

                // special-case handling for when this method is called from the abort button
                if (PomodoroMode && canceled)
                {
                    // If on a break, cancel it.
                    if (PomodoroBreak)
                    {
                        PomodoroBreak = false;
                    }
                    // If running a pomodoro, cancel it but do not enter a break
                    else
                    {
                        SetTime = PomodoroTime;
                        AbortedPomodoroCount++;
                    }
                }
            }
            else
            {
                // start
                updateTick.Enabled = true;
                timer.Start();
                if (PomodoroMode)
                    buttonStartPause.Text = "&Abort";
                else
                    buttonStartPause.Text = "&Pause";
            }

            UpdateStatusText();
            UpdateButtonStates();
        }

        private void UpdateTimeDisplay(TimeSpan time)
        {
            labelTimer.Text = String.Format("{0:D2}:{1:D2}:{2:D2}", time.Hours, time.Minutes, time.Seconds);
        }

        private void SetFormColor(Color color)
        {
            BackColor = color;
            statusStrip1.BackColor = color;
        }

        #region DragMove implementation for borderless mode
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImportAttribute("user32.dll")]
        public static extern bool ReleaseCapture();

        public static bool IsLinux
        {
            get
            {
                int p = (int)Environment.OSVersion.Platform;
                return (p == 4) || (p == 6) || (p == 128);
            }
        }

        private void MainForm_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!IsLinux && e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }
        #endregion

        #region Event Handlers
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            UserProperties.Save();
        }

        private void propertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var properties = new UserPropertiesForm();
            var tempProperties = (UserProperties)UserProperties.Clone();
            properties.SelectedObject = tempProperties;
            bool currentTopMost = TopMost;
            TopMost = false;
            var result = properties.ShowDialog();
            TopMost = currentTopMost;

            if (result == DialogResult.OK)
                UserProperties = (UserProperties)properties.SelectedObject;
        }

        private void pomodoroModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PomodoroMode = !PomodoroMode;
        }

        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            pomodoroModeToolStripMenuItem.Checked = UserProperties.PomodoroMode;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        void updateTick_Tick(object sender, EventArgs e)
        {
            if (IsRunning && IsTimer)
            {
                TimeSpan remaining = setTime - timer.Elapsed;
                if (remaining.TotalMilliseconds > 0)
                {
                    // not done yet
                    UpdateTimeDisplay(remaining);
                }
                else
                {
                    // time's up
                    ToggleTimerState(false);
                    UpdateTimeDisplay(TimeSpan.Zero);

                    if (UserProperties.AudioDing)
                        soundPlayer.Play();

                    if (UserProperties.PopupDing)
                    {
                        // preserve, clear, and restore the TopMost setting, otherwise the modal dialog can be blocked by the top most main window.
                        bool currentTopMost = TopMost;
                        TopMost = false;
                        new TimesUp().ShowDialog();
                        TopMost = currentTopMost;
                    }

                    // If running in pomodoro mode, toggle between the pomodoro and the break
                    if (PomodoroMode)
                    {
                        if (!PomodoroBreak)
                            CompletedPomodoroCount++;

                        PomodoroBreak = !PomodoroBreak;
                    }
                }
            }

            UpdateStatusText();
        }

        private void radioButtonStopwatch_CheckedChanged(object sender, EventArgs e)
        {
        }

        private void radioButtonTimer_CheckedChanged(object sender, EventArgs e)
        {
        }

        private void buttonPreset_Click(object sender, EventArgs e)
        {
            Debug.Assert(sender is Button);
            SetTime = (TimeSpan)((Button)sender).Tag;
        }

        private void buttonStartPause_Click(object sender, EventArgs e)
        {
            // if pomodoro mode, then pass true to the cancelled argument
            ToggleTimerState(PomodoroMode);
        }

        private void buttonReset_Click(object sender, EventArgs e)
        {
            Reset();
        }

        private void buttonSet_Click(object sender, EventArgs e)
        {
            SetTimeForm stf = new SetTimeForm(SetTime);
            // fix bug where the set window can be inaccessible if the main window is set to top-most
            bool topMostSetting = TopMost;
            TopMost = false;
            if (stf.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                SetTime = stf.Time;

            TopMost = topMostSetting;
        }
        #endregion

        #region Properties

        string TimesUpMessage
        {
            get
            {
                if (!PomodoroMode)
                {
                    return "Time's Up!!";
                }
                else
                {
                    if (PomodoroBreak)
                        return "Break's Over";
                    else
                        return "Time for a break!";
                }
            }
        }

        bool IsStopwatch
        {
            get { return this.radioButtonStopwatch.Checked; }
        }

        bool IsTimer
        {
            get { return this.radioButtonTimer.Checked; }
        }

        bool IsRunning
        {
            get { return timer.IsRunning; }
        }

        bool IsStopped
        {
            get { return !IsRunning; }
        }

        bool PomodoroMode
        {
            get { return UserProperties.PomodoroMode; }
            set
            {
                if (value)
                {
                    SetFormColor(UserProperties.PomodoroColor);

                    if (!IsRunning)
                    {
                        Reset();
                        PomodoroBreak = false;
                    }
                }
                else
                {
                    SetFormColor(UserProperties.TimerColor);
                }

                UserProperties.PomodoroMode = value;
                UpdateButtonStates();
                UpdateStatusText();
            }
        }

        TimeSpan SetTime
        {
            get
            {
                return setTime;
            }

            set
            {
                // Only allow sets when stopped 
                if (IsStopped)
                {
                    setTime = value;
                    timer.Reset();
                    UpdateTimeDisplay(setTime);
                }
            }
        }

        TimeSpan PomodoroTime { get { return userProperties.PomodoroTime; } }
        TimeSpan PomodoroBreakTime
        {
            get
            {
                return (CompletedPomodoroCount % 4 == 0) ? userProperties.PomodoroLongBreakTime : userProperties.PomodoroShortBreakTime;
            }
        }

        bool PomodoroBreak
        {
            get
            {
                return pomodoroBreak;
            }
            set
            {
                pomodoroBreak = value;
                if (pomodoroBreak)
                {
                    SetTime = PomodoroBreakTime;
                    SetFormColor(UserProperties.PomodoroBreakColor);
                    originalResetButtonText = buttonReset.Text;
                    buttonReset.Text = "&Skip";
                }
                else
                {
                    SetTime = PomodoroTime;
                    SetFormColor(UserProperties.PomodoroColor);
                    buttonReset.Text = originalResetButtonText;
                }

                UpdateButtonStates();
            }
        }

        TimeSpan Uptime
        {
            get
            {
                return DateTime.Now - start;
            }
        }

        UserProperties UserProperties
        {
            get
            {
                return userProperties;
            }
            set
            {
                userProperties = value;
                UpdateProperties();
            }
        }

        int CompletedPomodoroCount { get; set; } = 0;
        int AbortedPomodoroCount { get; set; } = 0;

        #endregion
    }
}
