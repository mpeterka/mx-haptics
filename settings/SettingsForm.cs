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
    /// nadpis sekce s přepínačem vpravo, síla jako svislý seznam kulatých voleb („Haptic intensity“),
    /// „Try it“ jako odkaz v barvě akcentu. Obsah se posouvá, patička zůstává dole.
    /// </summary>
    internal sealed class SettingsForm : Form
    {
        private const Int32 ContentWidth = 520;

        private static readonly (String Reaction, String Title, String Description)[] Rows =
        {
            (Reactions.WindowBorder, "Window borders", "The pointer moves from one window to another."),
            (Reactions.ControlHover, "Buttons and links", "The pointer enters a button, link, tab or menu item."),
            (Reactions.ScreenEdge, "Screen edge", "The pointer hits the outer edge of the desktop."),
            (Reactions.Click, "Click", "Left or right click on a button, link or taskbar icon."),
        };

        // vzory z eventMapping.yaml (subtle_collision, damp_collision, sharp_collision); sílu motoru řídí Options+
        private static readonly String[] StrengthLabels = { "Subtle", "Damp", "Sharp" };

        private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

        private readonly Theme _theme = Theme.Current;
        private HapticsSettings _settings;
        private DateTime _settingsStamp;
        private readonly HapticsSettings _defaults = new();
        private readonly Boolean _readOnly;
        private readonly ToggleSwitch _masterSwitch;
        private readonly List<RowUi> _rows = new();
        private readonly TableLayoutPanel _content;
        private readonly Panel _scroller;
        private readonly TableLayoutPanel _footer;
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

        /// <param name="readOnly">Pro screenshoty: výchozí nastavení, celé okno bez posuvníku, nic neukládá ani neposílá pluginu.</param>
        public SettingsForm(Boolean readOnly = false)
        {
            this._readOnly = readOnly;
            this._settings = readOnly ? new HapticsSettings() : HapticsSettings.Load();
            this._settingsStamp = SettingsStamp();

            this.Text = "MX Haptics – Settings";
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
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

            this._content = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Margin = Padding.Empty, Padding = Padding.Empty, Location = Point.Empty };
            this._content.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            // --- hlavička ---
            var header = this.Section(new Padding(24, 20, 24, 16));
            var iconImage = LoadIconImage();
            if (iconImage != null)
            {
                var icon = new PictureBox { Image = iconImage, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(44, 44), Margin = new Padding(0, 0, 14, 0) };
                header.Controls.Add(icon, 0, 0);
                header.SetRowSpan(icon, 2);
            }

            header.Controls.Add(new Label { Text = "MX Haptics", AutoSize = true, Font = new Font(this.Font.FontFamily, 17f, FontStyle.Bold), Margin = Padding.Empty }, 1, 0);
            header.Controls.Add(this.Secondary("Haptic feedback for the MX Master 4 across Windows"), 1, 1);
            header.ColumnStyles.Clear();
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            this.AddSection(header);

            // --- hlavní vypínač ---
            var master = this.Section();
            master.Controls.Add(this.Title("Haptic feedback", large: true), 0, 0);
            this._masterSwitch = new ToggleSwitch { Checked = this._settings.Enabled, AccessibleName = "Haptic feedback", Anchor = AnchorStyles.Right };
            master.Controls.Add(this._masterSwitch, 1, 0);
            var masterDesc = this.Secondary("Master switch for all reactions. Changes are saved instantly.");
            master.Controls.Add(masterDesc, 0, 1);
            master.SetColumnSpan(masterDesc, 2);
            this._masterSwitch.CheckedChanged += (_, _) =>
            {
                var on = this._masterSwitch.Checked;
                this.Change(s => s.Enabled = on);
            };
            this.AddSection(master);

            // --- reakce ---
            foreach (var (reaction, title, description) in Rows)
            {
                this.AddSection(this.BuildReaction(reaction, title, description));
            }

            this._scroller = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
            this._scroller.Controls.Add(this._content);

            // --- patička (mimo posuvnou část) ---
            this._footer = this.Section(new Padding(24, 14, 24, 18));
            this._footer.Dock = DockStyle.Bottom;
            this._status = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
            this._statsSince = new Label { AutoSize = true, Anchor = AnchorStyles.Right, ForeColor = this._theme.Secondary, Margin = Padding.Empty };
            this._footer.Controls.Add(this._status, 0, 0);
            this._footer.Controls.Add(this._statsSince, 1, 0);
            var footerLine = new Panel { Height = 1, Dock = DockStyle.Bottom, BackColor = this._theme.Divider };

            this.Controls.Add(this._scroller);
            this.Controls.Add(footerLine);
            this.Controls.Add(this._footer);

            this.UpdateEnabledState();
            this.UpdateStatus(readOnly || PluginClient.Send("reload") != null ? "Plugin is running." : null);
            this._loading = readOnly;

            if (readOnly)
            {
                this.ShowStats(new HapticsStats());
            }
            else
            {
                this.RefreshStats();
                var timer = new Timer { Interval = 2000 };
                timer.Tick += (_, _) =>
                {
                    this.ReloadIfChangedOutside();
                    this.RefreshStats();
                };
                timer.Start();
                this.FormClosed += (_, _) => timer.Dispose();
            }
        }

        /// <summary>Velikost až po přepočtu na DPI: šířka podle obsahu, výška nejvýš 85 % obrazovky (pro screenshoty celé).</summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            var content = this._content.GetPreferredSize(Size.Empty);
            var footer = this._footer.GetPreferredSize(new Size(content.Width, 0)).Height + 1;
            var max = (Int32)(Screen.FromControl(this).WorkingArea.Height * 0.85) - (this.Height - this.ClientSize.Height) - footer;
            var scroll = !this._readOnly && content.Height > max;
            this.ClientSize = new Size(
                content.Width + (scroll ? SystemInformation.VerticalScrollBarWidth : 0),
                (scroll ? max : content.Height) + footer);
            this.CenterToScreen();
        }

        private TableLayoutPanel BuildReaction(String reaction, String title, String description)
        {
            var rs = this._settings.For(reaction); // jen pro výchozí stav prvků; změny jdou přes Change()
            var defaultStrength = this._defaults.For(reaction).Strength;
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
                var taskbarLabel = new Label { Text = "Include Windows taskbar buttons", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 12, 0, 0) };
                this._taskbarToggle = new ToggleSwitch
                {
                    Checked = this._settings.TaskbarButtons,
                    AccessibleName = taskbarLabel.Text,
                    Anchor = AnchorStyles.Right,
                    Size = new Size(38, 20),
                    Margin = new Padding(16, 12, 0, 0),
                };
                this._taskbarToggle.CheckedChanged += (_, _) =>
                {
                    var on = this._taskbarToggle.Checked;
                    this.Change(s => s.TaskbarButtons = on);
                };
                section.Controls.Add(taskbarLabel, 0, r);
                section.Controls.Add(this._taskbarToggle, 1, r++);
            }

            // vzor jako „Haptic intensity“ v Options+: tučný nadpis a svislý seznam s „(Default)“
            var intensity = new Label { Text = "Vibration pattern", AutoSize = true, Font = new Font(this.Font, FontStyle.Bold), Margin = new Padding(0, 16, 0, 6) };
            section.Controls.Add(intensity, 0, r++);
            var strengths = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
            for (var i = 0; i < StrengthLabels.Length; i++)
            {
                var strength = (Strength)i;
                var radio = new LogiRadio
                {
                    Text = strength == defaultStrength ? $"{StrengthLabels[i]} (Default)" : StrengthLabels[i],
                    Checked = rs.Strength == strength,
                    AccessibleDescription = $"{title} vibration pattern",
                };
                radio.CheckedChanged += (_, _) =>
                {
                    if (!radio.Checked)
                    {
                        return;
                    }

                    this.Change(s => s.For(reaction).Strength = strength);
                    this.Test(reaction, strength);
                };
                strengths.Controls.Add(radio);
                row.Radios.Add(radio);
            }

            section.Controls.Add(strengths, 0, r++);

            // „Try it“ vlevo jako „OPEN ACTIONS RING“, počty vpravo
            row.Test = new LinkButton { Text = "Try it", Anchor = AnchorStyles.Left, Margin = new Padding(0, 10, 0, 0), AccessibleName = $"Try {title}" };
            row.Test.Click += (_, _) => this.Test(reaction, this._settings.For(reaction).Strength);
            row.Count = this.Secondary(String.Empty);
            row.Count.Anchor = AnchorStyles.Right;
            row.Count.Margin = new Padding(16, 10, 0, 0);
            section.Controls.Add(row.Test, 0, r);
            section.Controls.Add(row.Count, 1, r);

            row.Toggle.CheckedChanged += (_, _) =>
            {
                var on = row.Toggle.Checked;
                this.Change(s => s.For(reaction).Enabled = on);
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

        private void AddSection(Control section)
        {
            this._content.Controls.Add(section);
            this._content.Controls.Add(new Panel
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
                row.Count.Text = String.Format(English, "Today {0:N0}  ·  Total {1:N0}", stats.DayOf(row.Reaction, today), stats.TotalOf(row.Reaction));
            }

            this._statsSince.Text = DateTime.TryParseExact(stats.Since, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var since)
                ? String.Format(English, "Counted since {0:MMM d, yyyy}", since)
                : "No vibrations counted yet";
        }

        /// <summary>
        /// Uloží jednu změnu. Základem je aktuální soubor, ne stav načtený při otevření okna: plugin (akce Toggle)
        /// mezitím mohl změnit hlavní vypínač a celý starý objekt by jeho změnu vrátil.
        /// </summary>
        private void Change(Action<HapticsSettings> apply)
        {
            if (this._loading)
            {
                return;
            }

            var settings = HapticsSettings.TryLoad(out var fresh) ? fresh : this._settings;
            apply(settings);
            try
            {
                settings.Save();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                this.UpdateStatus($"Settings could not be saved: {ex.Message}", error: true);
                return;
            }

            this._settingsStamp = SettingsStamp();
            if (!ReferenceEquals(settings, this._settings))
            {
                this._settings = settings;
                this.ApplyToControls(); // přenese i změny zvenku, třeba hlavní vypínač z Options+
            }

            this.UpdateEnabledState();
            this.UpdateStatus(PluginClient.Send("reload") != null ? "Saved." : null);
        }

        /// <summary>Soubor změnil někdo jiný (akce Toggle v Options+, ruční úprava): ukázat jeho stav.</summary>
        private void ReloadIfChangedOutside()
        {
            if (SettingsStamp() == this._settingsStamp || !HapticsSettings.TryLoad(out var fresh))
            {
                return;
            }

            this._settingsStamp = SettingsStamp();
            this._settings = fresh;
            this.ApplyToControls();
        }

        private static DateTime SettingsStamp()
        {
            try
            {
                return File.GetLastWriteTimeUtc(HapticsSettings.FilePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return DateTime.MinValue;
            }
        }

        /// <summary>Nastaví prvky podle <see cref="_settings"/> bez ukládání a bez zkušební vibrace.</summary>
        private void ApplyToControls()
        {
            var wasLoading = this._loading;
            this._loading = true;
            try
            {
                this._masterSwitch.Checked = this._settings.Enabled;
                this._taskbarToggle.Checked = this._settings.TaskbarButtons;
                foreach (var row in this._rows)
                {
                    var rs = this._settings.For(row.Reaction);
                    row.Toggle.Checked = rs.Enabled;
                    row.Radios[(Int32)rs.Strength].Checked = true;
                }

                this.UpdateEnabledState();
            }
            finally
            {
                this._loading = wasLoading;
            }
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
                this.UpdateStatus($"Plugin replied: {response}", error: true);
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
                this._status.Text = "Plugin is not running – settings apply once it starts.";
                this._status.ForeColor = this._theme.Warning;
                return;
            }

            this._status.Text = text;
            this._status.ForeColor = error ? this._theme.Error : this._theme.Secondary;
        }
    }
}
