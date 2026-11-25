namespace MxHaptics.Settings
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    using MxHaptics.Shared;

    /// <summary>
    /// Okno nastavení ve stylu panelu „Modify settings“ v Logi Options+: sekce pod sebou oddělené čarou,
    /// nadpis sekce s přepínačem vpravo, síla jako kulaté volby, „Vyzkoušet“ jako odkaz v barvě akcentu.
    /// </summary>
    internal sealed class SettingsForm : Form
    {
        private const Int32 ContentWidth = 560;

        private static readonly (String Reaction, String Title, String Description)[] Rows =
        {
            (Reactions.WindowBorder, "Hranice okna", "Kurzor přejede z jednoho okna do jiného."),
            (Reactions.ControlHover, "Tlačítka a odkazy", "Najetí na tlačítko, odkaz, záložku nebo položku menu."),
            (Reactions.ScreenEdge, "Okraj obrazovky", "Náraz na vnější okraj plochy."),
            (Reactions.Click, "Kliknutí", "Levé nebo pravé tlačítko myši na tlačítku, odkazu nebo ikoně na liště."),
        };

        private static readonly String[] StrengthLabels = { "Slabá", "Střední", "Silná" };

        private readonly Theme _theme = Theme.Current;
        private readonly HapticsSettings _settings;
        private readonly Boolean _readOnly;
        private readonly ToggleSwitch _masterSwitch;
        private readonly List<RowUi> _rows = new();
        private readonly Label _status;
        private readonly Label _statsSince;
        private ToggleSwitch _taskbarToggle;
        private Boolean _loading = true;
        private Boolean _refreshingStats;

        private sealed class RowUi
        {
            public String Reaction;
            public ToggleSwitch Toggle;
            public List<LogiRadio> Radios = new();
            public LinkButton Test;
            public Label Count;
        }

        /// <param name="readOnly">Pro screenshoty: výchozí nastavení, nic neukládá ani neposílá pluginu.</param>
        public SettingsForm(Boolean readOnly = false)
        {
            this._readOnly = readOnly;
            this._settings = readOnly ? new HapticsSettings() : HapticsSettings.Load();

            this.Text = "MX Haptics – nastavení";
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.BackColor = this._theme.Back;
            this.ForeColor = this._theme.Text;
            this.KeyPreview = true;
            this.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    this.Close();
                }
            };

            using (var icoStream = typeof(SettingsForm).Assembly.GetManifestResourceStream("MxHaptics.Icon.ico"))
            {
                if (icoStream != null)
                {
                    this.Icon = new Icon(icoStream);
                }
            }

            var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            // --- hlavička ---
            var header = this.Section(new Padding(24, 20, 24, 16));
            var iconImage = LoadIconImage();
            if (iconImage != null)
            {
                header.Controls.Add(new PictureBox
                {
                    Image = iconImage,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Size = new Size(44, 44),
                    Margin = new Padding(0, 0, 14, 0),
                }, 0, 0);
                header.SetRowSpan(header.GetControlFromPosition(0, 0), 2);
            }

            header.Controls.Add(new Label
            {
                Text = "MX Haptics",
                AutoSize = true,
                Font = new Font(this.Font.FontFamily, 17f, FontStyle.Bold),
                Margin = Padding.Empty,
            }, 1, 0);
            header.Controls.Add(this.Secondary("Hmatová odezva myši MX Master 4"), 1, 1);
            header.ColumnStyles.Clear();
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            this.AddSection(root, header);

            // --- hlavní vypínač ---
            var master = this.Section();
            master.Controls.Add(this.Title("Haptická odezva", large: true), 0, 0);
            this._masterSwitch = new ToggleSwitch { Checked = this._settings.Enabled, AccessibleName = "Haptická odezva", Anchor = AnchorStyles.Right };
            master.Controls.Add(this._masterSwitch, 1, 0);
            master.Controls.Add(this.Secondary("Hlavní vypínač všech reakcí. Změny se ukládají hned."), 0, 1);
            master.SetColumnSpan(master.GetControlFromPosition(0, 1), 2);
            this._masterSwitch.CheckedChanged += (_, _) =>
            {
                this._settings.Enabled = this._masterSwitch.Checked;
                this.UpdateEnabledState();
                this.SaveAndNotify();
            };
            this.AddSection(root, master);

            // --- reakce ---
            foreach (var (reaction, title, description) in Rows)
            {
                this.AddSection(root, this.BuildReaction(reaction, title, description));
            }

            // --- patička ---
            var footer = this.Section(new Padding(24, 14, 24, 18));
            this._status = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
            this._statsSince = new Label { AutoSize = true, Anchor = AnchorStyles.Right, ForeColor = this._theme.Secondary, Margin = Padding.Empty };
            footer.Controls.Add(this._status, 0, 0);
            footer.Controls.Add(this._statsSince, 1, 0);
            root.Controls.Add(footer);

            this.Controls.Add(root);
            this.UpdateEnabledState();
            this.UpdateStatus(readOnly || PluginClient.Send("reload") != null ? "Plugin běží." : null);
            this._loading = readOnly;

            if (readOnly)
            {
                this.ShowStats(new HapticsStats());
            }
            else
            {
                this.RefreshStats();
                var timer = new Timer { Interval = 2000 };
                timer.Tick += (_, _) => this.RefreshStats();
                timer.Start();
                this.FormClosed += (_, _) => timer.Dispose();
            }
        }

        private TableLayoutPanel BuildReaction(String reaction, String title, String description)
        {
            var rs = this._settings.For(reaction);
            var row = new RowUi { Reaction = reaction };
            var section = this.Section();
            var r = 0;

            section.Controls.Add(this.Title(title), 0, r);
            row.Toggle = new ToggleSwitch { Checked = rs.Enabled, AccessibleName = title, Anchor = AnchorStyles.Right };
            section.Controls.Add(row.Toggle, 1, r++);

            var desc = this.Secondary(description);
            section.Controls.Add(desc, 0, r++);
            section.SetColumnSpan(desc, 2);

            if (reaction == Reactions.ControlHover)
            {
                var taskbarLabel = new Label { Text = "I tlačítka na hlavní liště Windows", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 10, 0, 0) };
                this._taskbarToggle = new ToggleSwitch
                {
                    Checked = this._settings.TaskbarButtons,
                    AccessibleName = taskbarLabel.Text,
                    Anchor = AnchorStyles.Right,
                    Size = new Size(38, 20),
                    Margin = new Padding(16, 10, 0, 0),
                };
                this._taskbarToggle.CheckedChanged += (_, _) =>
                {
                    this._settings.TaskbarButtons = this._taskbarToggle.Checked;
                    this.SaveAndNotify();
                };
                section.Controls.Add(taskbarLabel, 0, r);
                section.Controls.Add(this._taskbarToggle, 1, r++);
            }

            // síla jako „Haptic intensity“ v Options+, vedle odkaz Vyzkoušet
            var strengths = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 12, 0, 0), Anchor = AnchorStyles.Left };
            strengths.Controls.Add(new Label { Text = "Síla", AutoSize = true, ForeColor = this._theme.Secondary, Margin = new Padding(0, 2, 16, 0) });
            for (var i = 0; i < StrengthLabels.Length; i++)
            {
                var strength = (Strength)i;
                var radio = new LogiRadio { Text = StrengthLabels[i], Checked = rs.Strength == strength };
                radio.CheckedChanged += (_, _) =>
                {
                    if (!radio.Checked)
                    {
                        return;
                    }

                    rs.Strength = strength;
                    this.SaveAndNotify();
                    this.Test(reaction, strength);
                };
                strengths.Controls.Add(radio);
                row.Radios.Add(radio);
            }

            row.Test = new LinkButton { Text = "Vyzkoušet", Anchor = AnchorStyles.Right, Margin = new Padding(16, 12, 0, 0) };
            row.Test.Click += (_, _) => this.Test(reaction, rs.Strength);
            section.Controls.Add(strengths, 0, r);
            section.Controls.Add(row.Test, 1, r++);

            row.Count = this.Secondary(String.Empty);
            row.Count.Margin = new Padding(0, 10, 0, 0);
            section.Controls.Add(row.Count, 0, r);
            section.SetColumnSpan(row.Count, 2);

            row.Toggle.CheckedChanged += (_, _) =>
            {
                rs.Enabled = row.Toggle.Checked;
                this.UpdateEnabledState();
                this.SaveAndNotify();
            };

            this._rows.Add(row);
            return section;
        }

        private TableLayoutPanel Section(Padding? padding = null)
        {
            var section = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                Margin = Padding.Empty,
                Padding = padding ?? new Padding(24, 18, 24, 18),
                MinimumSize = new Size(ContentWidth, 0),
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
            };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            return section;
        }

        private void AddSection(TableLayoutPanel root, Control section)
        {
            root.Controls.Add(section);
            root.Controls.Add(new Panel
            {
                Height = 1,
                Margin = Padding.Empty,
                BackColor = this._theme.Divider,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
            });
        }

        private Label Title(String text, Boolean large = false) => new()
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = new Font(this.Font.FontFamily, large ? 13f : 11.5f, FontStyle.Bold),
            Margin = Padding.Empty,
        };

        private Label Secondary(String text) => new()
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = this._theme.Secondary,
            Margin = new Padding(0, 6, 0, 0),
        };

        private static Image LoadIconImage()
        {
            using var stream = typeof(SettingsForm).Assembly.GetManifestResourceStream("MxHaptics.Icon.png");
            return stream == null ? null : new Bitmap(stream);
        }

        private void UpdateEnabledState()
        {
            var on = this._settings.Enabled;
            foreach (var row in this._rows)
            {
                row.Toggle.Enabled = on;
                foreach (var radio in row.Radios)
                {
                    radio.Enabled = on && row.Toggle.Checked;
                }

                row.Test.Enabled = on;
            }

            this._taskbarToggle.Enabled = on && this._settings.ControlHover.Enabled;
        }

        /// <summary>Počty z běžícího pluginu (aktuální), jinak z uloženého souboru (plugin ukládá jednou za minutu).</summary>
        private async void RefreshStats()
        {
            if (this._refreshingStats)
            {
                return;
            }

            this._refreshingStats = true;
            try
            {
                var stats = await Task.Run(() =>
                {
                    var json = PluginClient.Send("stats");
                    return json != null && json.StartsWith('{') ? HapticsStats.FromJson(json) : HapticsStats.Load();
                });
                if (!this.IsDisposed)
                {
                    this.ShowStats(stats);
                }
            }
            finally
            {
                this._refreshingStats = false;
            }
        }

        private void ShowStats(HapticsStats stats)
        {
            var today = DateTime.Now;
            foreach (var row in this._rows)
            {
                row.Count.Text = $"Dnes {stats.DayOf(row.Reaction, today):N0}   ·   celkem {stats.TotalOf(row.Reaction):N0}";
            }

            this._statsSince.Text = DateTime.TryParseExact(stats.Since, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var since)
                ? $"Počty vibrací od {since:d}"
                : "Počty vibrací: zatím žádné";
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
                this._status.Text = "Plugin neběží – nastavení se použije po jeho spuštění.";
                this._status.ForeColor = this._theme.Warning;
                return;
            }

            this._status.Text = text;
            this._status.ForeColor = error ? this._theme.Error : this._theme.Secondary;
        }
    }
}
