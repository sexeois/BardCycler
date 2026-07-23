using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BardCycler
{
    public class CyclerGui : Form
    {
        private CyclerConfig _config;
        private CyclerEngine _engine;
        private readonly string _profileDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BardCycler", "profiles");

        private ComboBox _cmbHotkey, _cmbProfiles;
        private ListBox _lstSlots;
        private NumericUpDown _nudSlotInput, _nudRepeatDelay, _nudRepeatRate, _nudInputDelay;
        private CheckBox _chkConsume, _chkAutoRepeat, _chkEnabled;
        private Button _btnStartStop, _btnAddSlot, _btnRemoveSlot, _btnMoveUp, _btnMoveDown;
        private Button _btnSaveProfile, _btnLoadProfile, _btnResetDefaults;
        private Label _lblStatus, _lblState, _lblCyclePreview;
        private Panel _pnlStatusDot;
        private TextBox _txtLog;

        public CyclerGui()
        {
            Directory.CreateDirectory(_profileDir);
            _config = CyclerConfig.Load(Path.Combine(_profileDir, "default.json"));
            _engine = new CyclerEngine(_config);
            _engine.LogMessage += OnLog;
            _engine.StateChanged += OnStateChanged;
            BuildUi();
            SyncUiFromConfig();
        }

        private void BuildUi()
        {
            Text = "BardCycler v4080";
            Size = new Size(720, 640);
            MinimumSize = new Size(720, 640);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(18, 18, 24);
            ForeColor = Color.FromArgb(220, 220, 230);

            var left = new Panel { Dock = DockStyle.Left, Width = 340, Padding = new Padding(12) };
            var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
            Controls.Add(right);
            Controls.Add(left);
            int y = 8;

            _pnlStatusDot = new Panel
            {
                Size = new Size(14, 14),
                Location = new Point(12, y + 2),
                BackColor = Color.FromArgb(220, 50, 50)
            };
            left.Controls.Add(_pnlStatusDot);

            _lblStatus = new Label
            {
                Text = "STOPPED",
                Location = new Point(32, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 50, 50)
            };
            left.Controls.Add(_lblStatus);

            _lblState = new Label
            {
                Text = "State: Idle | Slot: 1 | Original: -",
                Location = new Point(160, y + 2),
                AutoSize = true,
                ForeColor = Color.FromArgb(140, 140, 160)
            };
            left.Controls.Add(_lblState);
            y += 36;

            Lbl(left, "Cycle Hotkey:", 12, y);
            _cmbHotkey = new ComboBox
            {
                Location = new Point(120, y - 2),
                Size = new Size(100, 24),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (var k in new[]
            {
                "F","G","H","J","K","L","C","V","B","N","M",
                "X","Z","Q","E","R","T","Y","U","I","O","P",
                "Space","F1","F2","F3","F4","F5","F6",
                "F7","F8","F9","F10","F11","F12"
            })
                _cmbHotkey.Items.Add(k);
            left.Controls.Add(_cmbHotkey);
            y += 34;

            Lbl(left, "Target Slots (order matters):", 12, y);
            y += 22;

            _lstSlots = new ListBox
            {
                Location = new Point(12, y),
                Size = new Size(180, 130),
                BackColor = Color.FromArgb(30, 30, 42),
                ForeColor = Color.FromArgb(200, 200, 215),
                BorderStyle = BorderStyle.FixedSingle
            };
            left.Controls.Add(_lstSlots);

            _nudSlotInput = new NumericUpDown
            {
                Location = new Point(200, y),
                Size = new Size(55, 24),
                Minimum = 1, Maximum = 9, Value = 1,
                BackColor = Color.FromArgb(30, 30, 42),
                ForeColor = Color.White
            };
            left.Controls.Add(_nudSlotInput);

            _btnAddSlot = Btn("Add", 262, y, 60);
            _btnAddSlot.Click += (s, e) =>
            {
                int sl = (int)_nudSlotInput.Value;
                if (!_config.TargetSlots.Contains(sl))
                {
                    _config.TargetSlots.Add(sl);
                    SyncSlotList();
                    UpdatePreview();
                }
            };
            left.Controls.Add(_btnAddSlot);

            _btnRemoveSlot = Btn("Remove", 200, y + 32, 70);
            _btnRemoveSlot.Click += (s, e) =>
            {
                if (_lstSlots.SelectedIndex >= 0)
                {
                    _config.TargetSlots.RemoveAt(_lstSlots.SelectedIndex);
                    SyncSlotList();
                    UpdatePreview();
                }
            };
            left.Controls.Add(_btnRemoveSlot);

            _btnMoveUp = Btn("^", 278, y + 32, 36);
            _btnMoveUp.Click += (s, e) => MoveSlot(-1);
            left.Controls.Add(_btnMoveUp);

            _btnMoveDown = Btn("v", 318, y + 32, 36);
            _btnMoveDown.Click += (s, e) => MoveSlot(1);
            left.Controls.Add(_btnMoveDown);
            y += 142;

            _lblCyclePreview = new Label
            {
                Location = new Point(12, y),
                Size = new Size(310, 22),
                ForeColor = Color.FromArgb(100, 200, 255),
                Font = new Font("Consolas", 9f)
            };
            left.Controls.Add(_lblCyclePreview);
            y += 30;

            Lbl(left, "Repeat Delay (ms):", 12, y);
            _nudRepeatDelay = Nud(160, y, 0, 5000, _config.RepeatDelayMs);
            left.Controls.Add(_nudRepeatDelay);
            y += 30;

            Lbl(left, "Repeat Rate (ms):", 12, y);
            _nudRepeatRate = Nud(160, y, 10, 5000, _config.RepeatRateMs);
            left.Controls.Add(_nudRepeatRate);
            y += 30;

            Lbl(left, "Input Delay (ms):", 12, y);
            _nudInputDelay = Nud(160, y, 0, 500, _config.InputDelayMs);
            left.Controls.Add(_nudInputDelay);
            y += 34;

            _chkConsume = Chk("Consume hotkey", 12, y, _config.ConsumeHotkey);
            left.Controls.Add(_chkConsume);
            y += 26;

            _chkAutoRepeat = Chk("Auto-repeat on hold", 12, y, _config.AutoRepeatOnHold);
            left.Controls.Add(_chkAutoRepeat);
            y += 26;

            _chkEnabled = Chk("Utility enabled", 12, y, _config.Enabled);
            left.Controls.Add(_chkEnabled);
            y += 36;

            _btnStartStop = new Button
            {
                Text = "START",
                Location = new Point(12, y),
                Size = new Size(140, 36),
                BackColor = Color.FromArgb(40, 160, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };
            _btnStartStop.FlatAppearance.BorderSize = 0;
            _btnStartStop.Click += ToggleEngine;
            left.Controls.Add(_btnStartStop);

            _btnResetDefaults = Btn("Reset Defaults", 164, y + 6, 110);
            _btnResetDefaults.Click += (s, e) =>
            {
                _config = CyclerConfig.Defaults();
                _engine.UpdateConfig(_config);
                SyncUiFromConfig();
                OnLog("reset to defaults.");
            };
            left.Controls.Add(_btnResetDefaults);
            y += 50;

            Lbl(left, "Profiles:", 12, y);
            y += 22;

            _cmbProfiles = new ComboBox
            {
                Location = new Point(12, y),
                Size = new Size(180, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(30, 30, 42),
                ForeColor = Color.White
            };
            left.Controls.Add(_cmbProfiles);

            _btnSaveProfile = Btn("Save", 200, y, 55);
            _btnSaveProfile.Click += SaveProfile;
            left.Controls.Add(_btnSaveProfile);

            _btnLoadProfile = Btn("Load", 262, y, 55);
            _btnLoadProfile.Click += LoadProfile;
            left.Controls.Add(_btnLoadProfile);

            RefreshProfiles();

            Lbl(right, "Activity Log:", 4, 8);
            _txtLog = new TextBox
            {
                Location = new Point(4, 28),
                Size = new Size(330, 540),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = Color.FromArgb(12, 12, 18),
                ForeColor = Color.FromArgb(130, 220, 130),
                Font = new Font("Consolas", 8.5f),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                    | AnchorStyles.Left | AnchorStyles.Right
            };
            right.Controls.Add(_txtLog);

            _cmbHotkey.SelectedIndexChanged += (s, e) =>
            {
                if (Enum.TryParse<Keys>(_cmbHotkey.Text, out var k))
                {
                    _config.CycleHotkey = k;
                    _engine.UpdateConfig(_config);
                }
            };
            _nudRepeatDelay.ValueChanged += (s, e) =>
            {
                _config.RepeatDelayMs = (int)_nudRepeatDelay.Value;
                _engine.UpdateConfig(_config);
            };
            _nudRepeatRate.ValueChanged += (s, e) =>
            {
                _config.RepeatRateMs = (int)_nudRepeatRate.Value;
                _engine.UpdateConfig(_config);
            };
            _nudInputDelay.ValueChanged += (s, e) =>
            {
                _config.InputDelayMs = (int)_nudInputDelay.Value;
                _engine.UpdateConfig(_config);
            };
            _chkConsume.CheckedChanged += (s, e) =>
            {
                _config.ConsumeHotkey = _chkConsume.Checked;
                _engine.UpdateConfig(_config);
            };
            _chkAutoRepeat.CheckedChanged += (s, e) =>
            {
                _config.AutoRepeatOnHold = _chkAutoRepeat.Checked;
                _engine.UpdateConfig(_config);
            };
            _chkEnabled.CheckedChanged += (s, e) =>
            {
                _config.Enabled = _chkEnabled.Checked;
                _engine.UpdateConfig(_config);
            };

            FormClosing += (s, e) =>
            {
                _engine.Stop();
                _config.Save(Path.Combine(_profileDir, "default.json"));
            };
        }

        private void SyncUiFromConfig()
        {
            _cmbHotkey.Text = _config.CycleHotkey.ToString();
            _nudRepeatDelay.Value = Clamp(_nudRepeatDelay, _config.RepeatDelayMs);
            _nudRepeatRate.Value = Clamp(_nudRepeatRate, _config.RepeatRateMs);
            _nudInputDelay.Value = Clamp(_nudInputDelay, _config.InputDelayMs);
            _chkConsume.Checked = _config.ConsumeHotkey;
            _chkAutoRepeat.Checked = _config.AutoRepeatOnHold;
            _chkEnabled.Checked = _config.Enabled;
            SyncSlotList();
            UpdatePreview();
        }

        private decimal Clamp(NumericUpDown n, int v)
            => Math.Max(n.Minimum, Math.Min(n.Maximum, v));

        private void SyncSlotList()
        {
            _lstSlots.Items.Clear();
            for (int i = 0; i < _config.TargetSlots.Count; i++)
                _lstSlots.Items.Add($"[{i}] Slot {_config.TargetSlots[i]}");
        }

        private void UpdatePreview()
        {
            _lblCyclePreview.Text = _config.TargetSlots.Count == 0
                ? "Cycle: (empty)"
                : $"Cycle: [?] -> {string.Join(" -> ", _config.TargetSlots)} -> [?]";
        }

        private void MoveSlot(int d)
        {
            int i = _lstSlots.SelectedIndex;
            int j = i + d;
            if (i < 0 || j < 0 || j >= _config.TargetSlots.Count) return;
            var t = _config.TargetSlots[i];
            _config.TargetSlots[i] = _config.TargetSlots[j];
            _config.TargetSlots[j] = t;
            SyncSlotList();
            _lstSlots.SelectedIndex = j;
            UpdatePreview();
        }

        private void ToggleEngine(object s, EventArgs e)
        {
            if (_engine.IsRunning)
            {
                _engine.Stop();
                _btnStartStop.Text = "START";
                _btnStartStop.BackColor = Color.FromArgb(40, 160, 70);
                _pnlStatusDot.BackColor = Color.FromArgb(220, 50, 50);
                _lblStatus.Text = "STOPPED";
                _lblStatus.ForeColor = Color.FromArgb(220, 50, 50);
            }
            else
            {
                _engine.UpdateConfig(_config);
                _engine.Start();
                _btnStartStop.Text = "STOP";
                _btnStartStop.BackColor = Color.FromArgb(200, 60, 50);
                _pnlStatusDot.BackColor = Color.FromArgb(50, 210, 80);
                _lblStatus.Text = "RUNNING";
                _lblStatus.ForeColor = Color.FromArgb(50, 210, 80);
            }
        }

        private void RefreshProfiles()
        {
            _cmbProfiles.Items.Clear();
            if (Directory.Exists(_profileDir))
                foreach (var f in Directory.GetFiles(_profileDir, "*.json"))
                    _cmbProfiles.Items.Add(Path.GetFileNameWithoutExtension(f));
            if (_cmbProfiles.Items.Count > 0)
                _cmbProfiles.SelectedIndex = 0;
        }

        private void SaveProfile(object s, EventArgs e)
        {
            string name = Microsoft.VisualBasic.Interaction.InputBox(
                "Profile name:", "Save Profile", "my_profile");
            if (string.IsNullOrWhiteSpace(name)) return;
            name = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
            _config.ProfileName = name;
            _config.Save(Path.Combine(_profileDir, name + ".json"));
            RefreshProfiles();
            _cmbProfiles.Text = name;
            OnLog($"saved: {name}");
        }

        private void LoadProfile(object s, EventArgs e)
        {
            if (_cmbProfiles.SelectedItem == null) return;
            string name = _cmbProfiles.SelectedItem.ToString();
            string path = Path.Combine(_profileDir, name + ".json");
            if (!File.Exists(path)) return;
            bool was = _engine.IsRunning;
            if (was) _engine.Stop();
            _config = CyclerConfig.Load(path);
            _engine.UpdateConfig(_config);
            SyncUiFromConfig();
            if (was) _engine.Start();
            OnLog($"loaded: {name}");
        }

        private void OnLog(string msg)
        {
            if (InvokeRequired) { Invoke(new Action<string>(OnLog), msg); return; }
            _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");
        }

        private void OnStateChanged(CyclerEngine.CycleState st, int slot, int orig)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<CyclerEngine.CycleState, int, int>(OnStateChanged),
                    st, slot, orig);
                return;
            }
            _lblState.Text = $"State: {st} | Slot: {slot} | Original: " +
                (st == CyclerEngine.CycleState.Cycling ? orig.ToString() : "-");
            if (st == CyclerEngine.CycleState.Cycling)
                _lblCyclePreview.Text =
                    $"Cycle: [{orig}] -> {string.Join(" -> ", _config.TargetSlots)} -> [{orig}]";
            else
                UpdatePreview();
        }

        private Label Lbl(Control p, string t, int x, int y)
        {
            var l = new Label { Text = t, Location = new Point(x, y), AutoSize = true };
            p.Controls.Add(l);
            return l;
        }

        private Button Btn(string t, int x, int y, int w)
        {
            var b = new Button
            {
                Text = t,
                Location = new Point(x, y),
                Size = new Size(w, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 45, 60),
                ForeColor = Color.FromArgb(200, 200, 215)
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 90);
            return b;
        }

        private NumericUpDown Nud(int x, int y, int min, int max, int val)
            => new NumericUpDown
            {
                Location = new Point(x, y),
                Size = new Size(80, 24),
                Minimum = min,
                Maximum = max,
                Value = Math.Max(min, Math.Min(max, val)),
                BackColor = Color.FromArgb(30, 30, 42),
                ForeColor = Color.White
            };

        private CheckBox Chk(string t, int x, int y, bool c)
            => new CheckBox { Text = t, Location = new Point(x, y), AutoSize = true, Checked = c };
    }
}
