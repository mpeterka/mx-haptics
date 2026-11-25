namespace MxHaptics.Settings
{
    using System;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Windows.Forms;

    /// <summary>Barvy ve stylu Logi Options+: plochý panel, tenké oddělovače, tyrkysový akcent.</summary>
    internal sealed class Theme
    {
        private static Theme _current;

        public Color Back { get; private init; }

        public Color Text { get; private init; }

        public Color Secondary { get; private init; }

        public Color Divider { get; private init; }

        public Color Accent { get; private init; }

        public Color OnKnob { get; private init; }

        public Color OffTrack { get; private init; }

        public Color OffKnob { get; private init; }

        public Color Warning { get; private init; }

        public Color Error { get; private init; }

        /// <summary>Podle barevného režimu aplikace (nastaví se v Program.Main před prvním použitím).</summary>
        public static Theme Current => _current ??= Application.IsDarkModeEnabled
            ? new Theme
            {
                Back = Color.FromArgb(0, 0, 0),
                Text = Color.White,
                Secondary = Color.FromArgb(168, 168, 168),
                Divider = Color.FromArgb(40, 40, 40),
                Accent = Color.FromArgb(0, 212, 191),
                OnKnob = Color.FromArgb(0, 0, 0),
                OffTrack = Color.FromArgb(70, 70, 70),
                OffKnob = Color.FromArgb(200, 200, 200),
                Warning = Color.FromArgb(245, 166, 35),
                Error = Color.FromArgb(255, 99, 99),
            }
            : new Theme
            {
                Back = Color.White,
                Text = Color.FromArgb(17, 17, 17),
                Secondary = Color.FromArgb(95, 95, 95),
                Divider = Color.FromArgb(226, 226, 226),
                Accent = Color.FromArgb(0, 150, 136),
                OnKnob = Color.White,
                OffTrack = Color.FromArgb(200, 200, 200),
                OffKnob = Color.White,
                Warning = Color.FromArgb(196, 110, 0),
                Error = Color.FromArgb(200, 40, 40),
            };

        /// <summary>Barva vypnutého prvku: zesvětlená/ztmavená směrem k pozadí.</summary>
        public Color Disabled(Color c) => Blend(c, this.Back, 0.4);

        private static Color Blend(Color c, Color back, Double amount) => Color.FromArgb(
            (Int32)(back.R + ((c.R - back.R) * amount)),
            (Int32)(back.G + ((c.G - back.G) * amount)),
            (Int32)(back.B + ((c.B - back.B) * amount)));
    }

    /// <summary>Společný základ vlastních prvků: kreslíme sami, pozadí přebíráme od rodiče.</summary>
    internal static class OwnerDraw
    {
        public static void Prepare(Control control, Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(control.Parent?.BackColor ?? Theme.Current.Back);
        }

        public static GraphicsPath Pill(RectangleF r)
        {
            var d = r.Height;
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 90, 180);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 180);
            path.CloseFigure();
            return path;
        }

        public static void SetOwnerDrawn(Control control, Action<ControlStyles, Boolean> setStyle)
        {
            setStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            control.Cursor = Cursors.Hand;
        }
    }

    /// <summary>Přepínač (pilulka) jako v Options+. Pořád je to CheckBox: mezerník, čtečky obrazovky i Checked fungují.</summary>
    internal sealed class ToggleSwitch : CheckBox
    {
        public ToggleSwitch()
        {
            OwnerDraw.SetOwnerDrawn(this, this.SetStyle);
            this.AutoSize = false;
            this.Size = new Size(44, 24);
            this.Margin = new Padding(16, 0, 0, 0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Theme.Current;
            var g = e.Graphics;
            OwnerDraw.Prepare(this, g);

            var r = new RectangleF(1.5f, 1.5f, this.Width - 3f, this.Height - 3f);
            var track = this.Checked ? t.Accent : t.OffTrack;
            var knob = this.Checked ? t.OnKnob : t.OffKnob;
            if (!this.Enabled)
            {
                track = t.Disabled(track);
                knob = t.Disabled(knob);
            }

            using (var path = OwnerDraw.Pill(r))
            using (var brush = new SolidBrush(track))
            {
                g.FillPath(brush, path);
                if (this.Focused && this.ShowFocusCues)
                {
                    using var pen = new Pen(t.Text, 1.5f);
                    g.DrawPath(pen, path);
                }
            }

            var pad = r.Height * 0.2f;
            var d = r.Height - (2 * pad);
            var x = this.Checked ? r.Right - pad - d : r.Left + pad;
            using var knobBrush = new SolidBrush(knob);
            g.FillEllipse(knobBrush, x, r.Top + pad, d, d);
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            this.Invalidate();
        }
    }

    /// <summary>Kulatá volba jako „Haptic intensity“ v Options+: šedý kruh, vybraný tyrkysový s tečkou; v seznamu pod sebou.</summary>
    internal sealed class LogiRadio : RadioButton
    {
        public LogiRadio()
        {
            OwnerDraw.SetOwnerDrawn(this, this.SetStyle);
            this.AutoSize = true;
            this.Margin = new Padding(0, 4, 0, 4);
        }

        private Int32 CircleSize => (Int32)(this.Font.Height * 1.2);

        private Int32 Gap => (Int32)(this.Font.Height * 0.8);

        public override Size GetPreferredSize(Size proposedSize)
        {
            var text = TextRenderer.MeasureText(this.Text, this.Font);
            return new Size(this.CircleSize + this.Gap + text.Width + 2, Math.Max(this.CircleSize, text.Height) + 4);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Theme.Current;
            var g = e.Graphics;
            OwnerDraw.Prepare(this, g);

            var c = this.CircleSize;
            var circle = new RectangleF(1, (this.Height - c) / 2f, c - 2, c - 2);
            var fill = this.Checked ? t.Accent : t.OffTrack;
            var text = t.Text;
            if (!this.Enabled)
            {
                fill = t.Disabled(fill);
                text = t.Disabled(text);
            }

            using (var brush = new SolidBrush(fill))
            {
                g.FillEllipse(brush, circle);
            }

            if (this.Checked)
            {
                var dot = circle.Width * 0.38f;
                using var dotBrush = new SolidBrush(t.Back);
                g.FillEllipse(dotBrush, circle.X + ((circle.Width - dot) / 2), circle.Y + ((circle.Height - dot) / 2), dot, dot);
            }

            var textRect = new Rectangle(c + this.Gap, 0, this.Width - c - this.Gap, this.Height);
            TextRenderer.DrawText(g, this.Text, this.Font, textRect, text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            if (this.Focused && this.ShowFocusCues)
            {
                ControlPaint.DrawFocusRectangle(g, textRect, text, this.Parent?.BackColor ?? t.Back);
            }
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            this.Invalidate();
        }
    }

    /// <summary>
    /// Textové tlačítko velkými písmeny v barvě akcentu („OPEN ACTIONS RING“ v Options+).
    /// Ne <see cref="Button"/>: ten si v tmavém režimu WinForms kreslí sám. Klávesnice (Enter, mezerník) a role pro čtečky ručně.
    /// </summary>
    internal sealed class LinkButton : Control
    {
        private Boolean _hover;
        private Font _bold;

        public LinkButton()
        {
            OwnerDraw.SetOwnerDrawn(this, this.SetStyle);
            this.SetStyle(ControlStyles.Selectable | ControlStyles.StandardClick, true);
            this.TabStop = true;
            this.AutoSize = true;
            this.AccessibleRole = AccessibleRole.PushButton;
            this.Margin = new Padding(16, 0, 0, 0);
        }

        private Font BoldFont => this._bold ??= new Font(this.Font, FontStyle.Bold);

        public override Size GetPreferredSize(Size proposedSize)
        {
            var text = TextRenderer.MeasureText(this.Text.ToUpperInvariant(), this.BoldFont);
            return new Size(text.Width + 6, text.Height + 6);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            this._bold?.Dispose();
            this._bold = null;
            base.OnFontChanged(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                e.Handled = true;
                this.OnClick(EventArgs.Empty);
            }
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            this.Cursor = this.Enabled ? Cursors.Hand : Cursors.Default;
            this.Invalidate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            this.Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            this.Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            this._hover = true;
            this.Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            this._hover = false;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Theme.Current;
            var g = e.Graphics;
            OwnerDraw.Prepare(this, g);

            var color = this.Enabled ? t.Accent : t.Disabled(t.Accent);
            using var underline = this._hover && this.Enabled ? new Font(this.BoldFont, FontStyle.Bold | FontStyle.Underline) : null;
            TextRenderer.DrawText(g, this.Text.ToUpperInvariant(), underline ?? this.BoldFont, this.ClientRectangle, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            if (this.Focused && this.ShowFocusCues)
            {
                ControlPaint.DrawFocusRectangle(g, this.ClientRectangle, color, this.Parent?.BackColor ?? t.Back);
            }
        }

        protected override void Dispose(Boolean disposing)
        {
            if (disposing)
            {
                this._bold?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
