namespace MxHaptics.Settings
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Windows.Forms;

    using MxHaptics.Shared;

    internal sealed class SettingsForm : Form
    {
        private static readonly (String Reaction, String Title, String Description)[] Rows =
        {
            (Reactions.WindowBorder, "Hranice okna", "Kurzor přejede z jednoho okna do jiného"),
            (Reactions.ControlHover, "Tlačítka a odkazy", "Najetí na tlačítko, odkaz, záložku nebo položku menu"),
            (Reactions.ScreenEdge, "Okraj obrazovky", "Náraz na vnější okraj plochy"),
        };

        private static readonly String[] StrengthLabels = { "Slabá", "Střední", "Silná" };

        private readonly HapticsSettings _settings;
        private readonly Boolean _readOnly;
        private readonly CheckBox _masterSwitch;
        private readonly CheckBox _taskbarCheck;
        private readonly List<Control> _rowControls = new();
        private readonly Label _status;
        private Boolean _loading = true;

        /// <param name="readOnly">Pro screenshoty: výchozí nastavení, nic neukládá ani neposílá pluginu.</param>
        public SettingsForm(Boolean readOnly = false)
        {
            this._readOnly = readOnly;
            this._settings = readOnly ? new HapticsSettings() : HapticsSettings.Load();

            this.Text = "MX Haptics – nastavení";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Padding = new Padding(16);

            var iconImage = LoadIconImage();
            using (var icoStream = typeof(SettingsForm).Assembly.GetManifestResourceStream("MxHaptics.Icon.ico"))
            {
                if (icoStream != null)
                {
                    this.Icon = new Icon(icoStream);
                }
            }

            var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };

            // --- hlavička ---
            var header = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 12) };
            if (iconImage != null)
            {
                header.Controls.Add(new PictureBox
                {
                    Image = iconImage,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Size = new Size(56, 56),
                    Margin = new Padding(0, 0, 12, 0),
                });
            }

            var titles = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Anchor = AnchorStyles.Left };
            titles.Controls.Add(new Label { Text = "MX Haptics", AutoSize = true, Font = new Font(this.Font.FontFamily, 15f, FontStyle.Bold) });
            titles.Controls.Add(new Label { Text = "Hmatová odezva myši MX Master 4. Změny se ukládají hned.", AutoSize = true, ForeColor = SystemColors.GrayText });
            header.Controls.Add(titles);
            root.Controls.Add(header);

            // --- hlavní vypínač ---
            this._masterSwitch = new CheckBox
            {
                Text = "Haptika zapnutá",
                AutoSize = true,
                Checked = this._settings.Enabled,
                Font = new Font(this.Font, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 8),
            };
            this._masterSwitch.CheckedChanged += (_, _) =>
            {
                this._settings.Enabled = this._masterSwitch.Checked;
                this.UpdateEnabledState();
                this.SaveAndNotify();
            };
            root.Controls.Add(this._masterSwitch);

            // --- reakce ---
            var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 0, 0, 8) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            foreach (var (reaction, title, description) in Rows)
            {
                var rs = this._settings.For(reaction);

                var enabled = new CheckBox { Text = title, AutoSize = true, Checked = rs.Enabled, Anchor = AnchorStyles.Left };
                var desc = new Label { Text = description, AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(20, 0, 16, 10) };
                var cell = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0, 6, 0, 0) };
                cell.Controls.Add(enabled);
                cell.Controls.Add(desc);

                if (reaction == Reactions.ControlHover)
                {
                    this._taskbarCheck = new CheckBox
                    {
                        Text = "I tlačítka na hlavní liště Windows",
                        AutoSize = true,
                        Checked = this._settings.TaskbarButtons,
                        Margin = new Padding(20, 0, 16, 10),
                    };
                    this._taskbarCheck.CheckedChanged += (_, _) =>
                    {
                        this._settings.TaskbarButtons = this._taskbarCheck.Checked;
                        this.SaveAndNotify();
                    };
                    cell.Controls.Add(this._taskbarCheck);
                }

                var strength = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 0) };
                strength.Items.AddRange(StrengthLabels);
                strength.SelectedIndex = (Int32)rs.Strength;

                var test = new Button { Text = "Vyzkoušet", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 0, 0) };

                enabled.CheckedChanged += (_, _) =>
                {
                    rs.Enabled = enabled.Checked;
                    this.UpdateEnabledState();
                    this.SaveAndNotify();
                };
                strength.SelectedIndexChanged += (_, _) =>
                {
                    rs.Strength = (Strength)strength.SelectedIndex;
                    this.SaveAndNotify();
                    this.Test(reaction, rs.Strength);
                };
                test.Click += (_, _) => this.Test(reaction, (Strength)strength.SelectedIndex);

                grid.Controls.Add(cell);
                grid.Controls.Add(strength);
                grid.Controls.Add(test);
                this._rowControls.Add(enabled);
                this._rowControls.Add(strength);
                this._rowControls.Add(test);
            }

            root.Controls.Add(grid);

            // --- patička ---
            var footer = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            this._status = new Label { AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = SystemColors.GrayText };
            var close = new Button { Text = "Zavřít", AutoSize = true, Anchor = AnchorStyles.Right };
            close.Click += (_, _) => this.Close();
            this.CancelButton = close;
            footer.Controls.Add(this._status);
            footer.Controls.Add(close);
            root.Controls.Add(footer);

            this.Controls.Add(root);
            this.UpdateEnabledState();
            this.UpdateStatus(readOnly || PluginClient.Send("reload") != null ? "Plugin běží." : null);
            this._loading = readOnly;
        }

        private static Image LoadIconImage()
        {
            using var stream = typeof(SettingsForm).Assembly.GetManifestResourceStream("MxHaptics.Icon.png");
            return stream == null ? null : new Bitmap(stream);
        }

        private void UpdateEnabledState()
        {
            var on = this._settings.Enabled;
            for (var i = 0; i < this._rowControls.Count; i += 3)
            {
                var enabled = (CheckBox)this._rowControls[i];
                enabled.Enabled = on;
                this._rowControls[i + 1].Enabled = on && enabled.Checked;
                this._rowControls[i + 2].Enabled = on;
            }

            this._taskbarCheck.Enabled = on && this._settings.ControlHover.Enabled;
        }

        private void SaveAndNotify()
        {
            if (this._loading)
            {
                return;
            }

            try
            {
                this._settings.Save();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                this.UpdateStatus($"Nastavení nejde uložit: {ex.Message}", error: true);
                return;
            }

            this.UpdateStatus(PluginClient.Send("reload") != null ? "Uloženo." : null);
        }

        private void Test(String reaction, Strength strength)
        {
            if (this._loading)
            {
                return;
            }

            var response = PluginClient.Send($"test {reaction} {strength.ToString().ToLowerInvariant()}");
            if (response != null && response != "ok")
            {
                this.UpdateStatus($"Plugin odpověděl: {response}", error: true);
            }
            else if (response == null)
            {
                this.UpdateStatus(null);
            }
        }

        /// <param name="text">null = plugin neběží.</param>
        private void UpdateStatus(String text, Boolean error = false)
        {
            if (text == null)
            {
                this._status.Text = "Plugin neběží – nastavení se uloží a použije po jeho spuštění.";
                this._status.ForeColor = Color.DarkOrange;
                return;
            }

            this._status.Text = text;
            this._status.ForeColor = error ? Color.Firebrick : SystemColors.GrayText;
        }
    }
}
