using PdfStudio.Models;
using PdfStudio.Services;

namespace PdfStudio.UI;

public sealed class InputDialog : ModernDialog
{
    private readonly TextBox box = new();
    public string Value => box.Text.Trim();

    private InputDialog(string title, string prompt, string initial, bool password)
        : base(title, prompt, 460)
    {
        box.Text = initial; box.UseSystemPasswordChar = password;
        AddWide(box);
        Shown += (_, _) => { box.Focus(); box.SelectAll(); };
    }

    public static string? Show(IWin32Window owner, string title, string prompt, string initial = "", bool password = false)
    {
        using var d = new InputDialog(title, prompt, initial, password);
        return d.ShowDialog(owner) == DialogResult.OK ? d.Value : null;
    }
}

public enum OverlayScope { ThisPage, SelectedPages, AllPages }

public sealed class TextOverlayDialog : ModernDialog
{
    private readonly TextBox text = new() { Multiline = true, Height = 74, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true };
    private readonly ComboBox font = new ModernCombo();
    private readonly NumericUpDown size = Num(6, 200, 20);
    private readonly CheckBox bold = new() { Text = "Bold", AutoSize = true };
    private readonly CheckBox italic = new() { Text = "Italic", AutoSize = true };
    private readonly ComboBox hAnchor = MakeCombo(("Left", HAnchor.Left), ("Center", HAnchor.Center), ("Right", HAnchor.Right));
    private readonly ComboBox vAnchor = MakeCombo(("Top", VAnchor.Top), ("Middle", VAnchor.Middle), ("Bottom", VAnchor.Bottom));
    private readonly NumericUpDown offX = Num(-600, 600, 0);
    private readonly NumericUpDown offY = Num(-800, 800, 0);
    private readonly ComboBox scope;
    private readonly Panel swatch = new() { Width = 34, Height = 26, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 2, 10, 2) };
    private string colorHex = "#111111";

    public PageTextAnnotation Annotation => new()
    {
        Text = text.Text, FontName = font.SelectedItem?.ToString() ?? "Arial", FontSize = (double)size.Value,
        Bold = bold.Checked, Italic = italic.Checked, ColorHex = colorHex,
        H = ValueOf<HAnchor>(hAnchor), V = ValueOf<VAnchor>(vAnchor), OffsetX = (double)offX.Value, OffsetY = (double)offY.Value
    };
    public OverlayScope Scope => ValueOf<OverlayScope>(scope);

    public TextOverlayDialog(int selectedCount)
        : base("Add text to pages", "Place a text label, stamp or note. It is saved into the exported PDF.", 560, "Add text")
    {
        font.Items.AddRange(new object[] { "Arial", "Times New Roman", "Courier New", "Verdana", "Segoe UI", "Calibri", "Tahoma", "Georgia" });
        font.SelectedIndex = 0;
        scope = MakeCombo(("This page only", OverlayScope.ThisPage),
                          ($"Selected pages ({selectedCount})", OverlayScope.SelectedPages),
                          ("All pages", OverlayScope.AllPages));

        AddRow("Text", text);
        AddRow("Font", font);
        AddRow("Size (pt)", size);

        var style = new FlowLayoutPanel { AutoSize = true, BackColor = Color.Transparent, WrapContents = false };
        var pick = new FlatButton { Text = "Colour…", Width = 96, Height = 30, Margin = new Padding(0, 0, 14, 0) };
        swatch.BackColor = ColorTranslator.FromHtml(colorHex);
        pick.Click += (_, _) =>
        {
            using var cd = new ColorDialog { Color = swatch.BackColor, FullOpen = true };
            if (cd.ShowDialog(this) == DialogResult.OK) { swatch.BackColor = cd.Color; colorHex = $"#{cd.Color.R:X2}{cd.Color.G:X2}{cd.Color.B:X2}"; }
        };
        bold.Margin = new Padding(0, 4, 14, 0); italic.Margin = new Padding(0, 4, 0, 0);
        style.Controls.AddRange(new Control[] { swatch, pick, bold, italic });
        AddRow("Style", style);

        AddHeading("Position");
        AddRow("Horizontal", hAnchor);
        AddRow("Vertical", vAnchor);
        AddRow("Nudge right (pt)", offX);
        AddRow("Nudge down (pt)", offY);
        AddHeading("Apply to");
        AddRow("Pages", scope);
        AddNote("Positions are anchored to the page edges, so the same setting works on A4, Letter and landscape pages alike.");
        if (selectedCount > 1) SelectValue(scope, OverlayScope.SelectedPages);
    }

    protected override bool ValidateInput(out string error)
    {
        error = string.IsNullOrWhiteSpace(text.Text) ? "Enter the text to add." : "";
        return error.Length == 0;
    }
}

public sealed class ImageOverlayDialog : ModernDialog
{
    private readonly TextBox path = new();
    private readonly NumericUpDown widthMm = Num(5, 400, 40);
    private readonly ComboBox hAnchor = MakeCombo(("Left", HAnchor.Left), ("Center", HAnchor.Center), ("Right", HAnchor.Right));
    private readonly ComboBox vAnchor = MakeCombo(("Top", VAnchor.Top), ("Middle", VAnchor.Middle), ("Bottom", VAnchor.Bottom));
    private readonly NumericUpDown offX = Num(-600, 600, 0);
    private readonly NumericUpDown offY = Num(-800, 800, 0);
    private readonly ComboBox scope;

    public PageImageAnnotation Annotation => new()
    {
        Path = path.Text.Trim(), WidthPt = (double)widthMm.Value * 72.0 / 25.4,
        H = ValueOf<HAnchor>(hAnchor), V = ValueOf<VAnchor>(vAnchor), OffsetX = (double)offX.Value, OffsetY = (double)offY.Value
    };
    public OverlayScope Scope => ValueOf<OverlayScope>(scope);

    public ImageOverlayDialog(int selectedCount)
        : base("Add an image to pages", "Place a logo, stamp or signature picture. It is saved into the exported PDF.", 560, "Add image")
    {
        scope = MakeCombo(("This page only", OverlayScope.ThisPage),
                          ($"Selected pages ({selectedCount})", OverlayScope.SelectedPages),
                          ("All pages", OverlayScope.AllPages));

        var host = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, BackColor = Color.Transparent };
        host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        host.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        path.Dock = DockStyle.Fill; path.Margin = new Padding(0, 0, 8, 0);
        var browse = new FlatButton { Text = "Browse…", Width = 92, Height = 30, Margin = new Padding(0) };
        browse.Click += (_, _) =>
        {
            using var d = new OpenFileDialog { Title = "Choose a picture", Filter = "Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files|*.*" };
            if (d.ShowDialog(this) == DialogResult.OK) path.Text = d.FileName;
        };
        host.Controls.Add(path, 0, 0); host.Controls.Add(browse, 1, 0);

        AddRow("Picture", host);
        var savedSigs = SignatureStore.List();
        if (savedSigs.Count > 0)
        {
            var savedBox = new ModernCombo();
            savedBox.Items.Add("Choose one of your saved signatures…");
            foreach (var s in savedSigs) savedBox.Items.Add(new Choice<SavedSignature>(s.Name, s));
            savedBox.SelectedIndex = 0;
            savedBox.SelectedIndexChanged += (_, _) =>
            {
                if (savedBox.SelectedItem is Choice<SavedSignature> c && File.Exists(c.Value.Path)) path.Text = CopyForExport(c.Value.Path);
            };
            AddRow("Saved signature", savedBox);
        }
        AddRow("Width (mm)", widthMm);
        AddHeading("Position");
        AddRow("Horizontal", hAnchor);
        AddRow("Vertical", vAnchor);
        AddRow("Nudge right (pt)", offX);
        AddRow("Nudge down (pt)", offY);
        AddHeading("Apply to");
        AddRow("Pages", scope);
        AddNote("PNG pictures with a transparent background make the best signatures and logos. Positions are anchored to the page edges.");
        SelectValue(hAnchor, HAnchor.Right); SelectValue(vAnchor, VAnchor.Bottom);
        if (selectedCount > 1) SelectValue(scope, OverlayScope.SelectedPages);
    }

    /// <summary>A private copy, so deleting the signature from the library later cannot break the export.</summary>
    private static string CopyForExport(string source)
    {
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "PdfStudio"); Directory.CreateDirectory(dir);
            var copy = Path.Combine(dir, $"sig_{Guid.NewGuid():N}.png");
            File.Copy(source, copy, true);
            return copy;
        }
        catch { return source; }
    }

    protected override bool ValidateInput(out string error)
    {
        error = File.Exists(path.Text.Trim()) ? "" : "Choose a picture file (PNG, JPG, BMP, GIF or TIFF).";
        return error.Length == 0;
    }
}

public enum BlankSize { MatchPage, A4, A3, A5, Letter, Legal }
public enum BlankPosition { AfterActive, AtEnd, AtStart }

public sealed class BlankPageDialog : ModernDialog
{
    private readonly ComboBox size = MakeCombo(("Same as current page", BlankSize.MatchPage), ("A4", BlankSize.A4), ("A3", BlankSize.A3),
        ("A5", BlankSize.A5), ("Letter", BlankSize.Letter), ("Legal", BlankSize.Legal));
    private readonly ComboBox orient = MakeCombo(("Portrait", false), ("Landscape", true));
    private readonly NumericUpDown count = Num(1, 200, 1);
    private readonly ComboBox placement = MakeCombo(("After the active page", BlankPosition.AfterActive), ("At the end", BlankPosition.AtEnd), ("At the start", BlankPosition.AtStart));

    public BlankSize PageSizeChoice => ValueOf<BlankSize>(size);
    public bool Landscape => ValueOf<bool>(orient);
    public int Count => (int)count.Value;
    public BlankPosition Position => ValueOf<BlankPosition>(placement);

    public BlankPageDialog() : base("Insert blank pages", "Add empty pages for notes, separators or double-sided printing.", 480, "Insert")
    {
        AddRow("Page size", size); AddRow("Orientation", orient); AddRow("How many", count); AddRow("Insert", placement);
    }
}

public enum SplitMode { EveryN, EachPage, AtActive, Ranges }

public sealed class SplitDialog : ModernDialog
{
    private readonly ComboBox mode;
    private readonly NumericUpDown every = Num(1, 5000, 1);
    private readonly TextBox ranges = new() { Text = "1-3, 4-6" };
    private readonly TextBox baseName = new();
    private readonly TextBox folder;
    private readonly int pageCount, active;

    public SplitMode Mode => ValueOf<SplitMode>(mode);
    public int EveryN => (int)every.Value;
    public string Ranges => ranges.Text;
    public string BaseName => string.IsNullOrWhiteSpace(baseName.Text) ? "Part" : baseName.Text.Trim();
    public string Folder => folder.Text.Trim();

    public SplitDialog(int pageCount, int activeIndex, string defaultFolder)
        : base("Split document", $"Cut the current {pageCount}-page sequence into separate PDF files.", 540, "Split")
    {
        this.pageCount = pageCount; active = activeIndex;
        mode = MakeCombo(("Every N pages", SplitMode.EveryN), ("Each page as its own PDF", SplitMode.EachPage),
                         (activeIndex >= 0 ? $"Two parts — split after page {activeIndex + 1}" : "Two parts — split after the active page", SplitMode.AtActive),
                         ("Custom page ranges", SplitMode.Ranges));
        every.Value = Math.Min(every.Maximum, Math.Max(1, pageCount / 2));
        baseName.Text = "Part";
        var f = FolderPicker(defaultFolder, "Choose where the split files are saved");
        folder = f.Box;
        AddRow("Split", mode);
        AddRow("Pages per file", every);
        AddRow("Ranges", ranges);
        AddNote("Ranges: one file per comma-separated item, e.g. \"1-3, 4-6, 9\".", 1);
        AddRow("File name", baseName);
        AddRow("Save to", f.Host);
        mode.SelectedIndexChanged += (_, _) => UpdateEnabled();
        UpdateEnabled();
    }

    private void UpdateEnabled()
    {
        every.Enabled = Mode == SplitMode.EveryN;
        ranges.Enabled = Mode == SplitMode.Ranges;
    }

    protected override bool ValidateInput(out string error)
    {
        error = "";
        if (!Directory.Exists(Folder) && string.IsNullOrWhiteSpace(Folder)) error = "Choose a folder to save the split files.";
        else if (Mode == SplitMode.AtActive && (active < 0 || active >= pageCount - 1)) error = "Select a page that is not the last page, then try again.";
        else if (Mode == SplitMode.Ranges)
        {
            try { PageRange.ParseGroups(Ranges, pageCount); } catch (FormatException ex) { error = ex.Message; }
        }
        return error.Length == 0;
    }
}

/// <summary>Export options: details, page numbers, watermark, compression and password protection.</summary>
public sealed class ExportDialog : ModernDialog
{
    private readonly ExportOptions o;
    private readonly List<(FlatButton Tab, TableLayoutPanel Page)> tabs = new();

    private readonly TextBox title = new(), author = new(), subject = new(), keywords = new();
    private readonly CheckBox grayscale = new() { Text = "Convert to grayscale (saves ink, smaller scans)", AutoSize = true };
    private readonly CheckBox numbers = new() { Text = "Add page numbers", AutoSize = true };
    private readonly ComboBox numPos = MakeCombo(("Bottom centre", PageNumberPosition.BottomCenter), ("Bottom right", PageNumberPosition.BottomRight), ("Bottom left", PageNumberPosition.BottomLeft),
        ("Top centre", PageNumberPosition.TopCenter), ("Top right", PageNumberPosition.TopRight), ("Top left", PageNumberPosition.TopLeft));
    private readonly ComboBox numFmt = MakeCombo(("1", PageNumberFormat.Number), ("Page 1", PageNumberFormat.PageN), ("Page 1 of 10", PageNumberFormat.PageNOfTotal), ("1 / 10", PageNumberFormat.NSlashTotal));
    private readonly NumericUpDown numStart = Num(0, 9999, 1);
    private readonly NumericUpDown numSize = Num(6, 40, 10);
    private readonly CheckBox skipFirst = new() { Text = "Skip the first page (cover)", AutoSize = true };
    private readonly CheckBox watermark = new() { Text = "Add a text watermark", AutoSize = true };
    private readonly TextBox wmText = new();
    private readonly NumericUpDown wmOpacity = Num(3, 100, 20);
    private readonly NumericUpDown wmSize = Num(10, 300, 64);
    private readonly NumericUpDown wmAngle = Num(-90, 90, -35);
    private readonly ComboBox compression = MakeCombo(("None (keep original quality)", PdfCompression.None), ("Light — print quality", PdfCompression.Light),
        ("Balanced — good for email", PdfCompression.Balanced), ("Strong — smallest file", PdfCompression.Strong));
    private readonly ComboBox compMode = MakeCombo(("Preset level", CompressionTargetKind.Preset), ("Reduce by a percentage", CompressionTargetKind.ReducePercent),
        ("Reduce to a target size (MB)", CompressionTargetKind.TargetSize));
    private readonly NumericUpDown reducePct = Num(1, 95, 50);
    private readonly NumericUpDown targetMb = Num(0.05m, 4096m, 2m, 2, 0.5m);
    private readonly TextBox userPw = new() { UseSystemPasswordChar = true };
    private readonly TextBox ownerPw = new() { UseSystemPasswordChar = true };
    private readonly CheckBox allowPrint = new() { Text = "Allow printing", AutoSize = true, Checked = true };
    private readonly CheckBox allowCopy = new() { Text = "Allow copying text and images", AutoSize = true, Checked = true };
    private readonly CheckBox allowEdit = new() { Text = "Allow editing", AutoSize = true, Checked = true };
    private readonly Surface tabBar = new() { Dock = DockStyle.Fill, Height = 44 };
    private readonly Panel host = new() { Dock = DockStyle.Fill, Height = 392 };

    public ExportOptions Result { get; private set; }

    private PageLayoutOptions layout;
    private readonly Txt layoutSummary = new("", 9.5f) { Height = 48 };

    public ExportDialog(ExportOptions current, int pageCount, string okText = "Export PDF", IReadOnlyList<PageItem>? pages = null)
        : base("Export PDF", $"{pageCount} page(s) will be written in the exact order shown.", 780, okText)
    {
        o = current; Result = current;
        layout = current.Layout.Clone();
        FixedBodyHeight = 52 + 14 + 392 + 6;
        tabBar.UseRole(() => Theme.Surface);
        tabBar.BorderBottom = true;

        var tabFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 6, 0, 0) };
        tabBar.Controls.Add(tabFlow);
        var r0 = NextRow(); Grid.Controls.Add(tabBar, 0, r0); Grid.SetColumnSpan(tabBar, 2); tabBar.Height = 52; tabBar.Margin = new Padding(0, 0, 0, 14);
        var r1 = NextRow(); Grid.Controls.Add(host, 0, r1); Grid.SetColumnSpan(host, 2);
        Grid.RowStyles[r0] = new RowStyle(SizeType.Absolute, 52 + 14);
        Grid.RowStyles[r1] = new RowStyle(SizeType.AutoSize);

        void Tab(string name, TableLayoutPanel page)
        {
            page.Dock = DockStyle.Fill; page.Visible = false; page.Padding = new Padding(2, 0, 2, 0); host.Controls.Add(page);
            var b = new FlatButton { Text = name, Kind = BtnKind.Ghost, Width = TextRenderer.MeasureText(name, Theme.Ui(9.5f)).Width + 30, Height = 36, Margin = new Padding(0, 0, 6, 0) };
            b.Click += (_, _) => ShowTab(page);
            tabFlow.Controls.Add(b); tabs.Add((b, page));
        }

        Tab("Details", FormPage("Document details", "Stored in the file properties. Shown by PDF readers and search tools.",
            ("Title", title), ("Author", author), ("Subject", subject), ("Keywords", keywords)));
        var changeSize = new FlatButton { Text = "Choose page size…", Glyph = "\uE7C3", Kind = BtnKind.Secondary, Width = 220, Height = 36 };
        changeSize.Click += (_, _) =>
        {
            using var d = new PageSizeDialog(layout, pages ?? Array.Empty<PageItem>());
            if (d.ShowDialog(this) == DialogResult.OK) { layout = d.Result; UpdateLayoutSummary(); }
        };
        UpdateLayoutSummary();
        Tab("Page size", FormPage("Page size", "Make every page the same size, or keep each page as it is.",
            ("", layoutSummary), ("", changeSize)));
        Tab("Page numbers", FormPage("Page numbers", "Stamp a number on each page when the file is written.",
            ("", numbers), ("Position", numPos), ("Format", numFmt), ("Start at", numStart), ("Font size", numSize), ("", skipFirst)));
        Tab("Watermark", FormPage("Text watermark", "A faint diagonal label across every page, such as CONFIDENTIAL or DRAFT.",
            ("", watermark), ("Text", wmText), ("Opacity (%)", wmOpacity), ("Font size", wmSize), ("Angle (°)", wmAngle)));
        var compNote = new Txt("Built in, nothing to install. Percentage and target size are measured against the file as it would be saved without compression: pictures are re-encoded as gently as possible to reach the goal, and text stays sharp. If a target is too small to reach (mostly-text files), you are told.", 8.5f, FontStyle.Regular, true) { Height = 72 };
        var secNote = new Txt("Permissions are enforced only when an owner password is set.", 8.5f, FontStyle.Regular, true) { Height = 22 };
        Tab("Compress", FormPage("Compress", "Make the file smaller: pick a preset, a percentage, or an exact size limit.",
            ("Method", compMode), ("Preset level", compression), ("Make it smaller by (%)", reducePct), ("Keep it under (MB)", targetMb),
            ("", grayscale), ("", compNote)));
        Tab("Protect", FormPage("Password protection", "Lock the file with a password and choose what readers may do.",
            ("Open password", userPw), ("Owner password", ownerPw),
            ("", secNote), ("", allowPrint), ("", allowCopy), ("", allowEdit)));

        title.Text = o.Title; author.Text = o.Author; subject.Text = o.Subject; keywords.Text = o.Keywords; grayscale.Checked = o.Grayscale; numbers.Checked = o.PageNumbers; SelectValue(numPos, o.NumberPosition);
        SelectValue(numFmt, o.NumberFormat); numStart.Value = Math.Clamp(o.StartNumber, 0, 9999); numSize.Value = (decimal)Math.Clamp(o.NumberFontSize, 6, 40);
        skipFirst.Checked = o.SkipFirstPage; watermark.Checked = o.Watermark; wmText.Text = o.WatermarkText;
        wmOpacity.Value = Math.Clamp(o.WatermarkOpacityPercent, 3, 100); wmSize.Value = (decimal)Math.Clamp(o.WatermarkFontSize, 10, 300);
        wmAngle.Value = (decimal)Math.Clamp(o.WatermarkAngle, -90, 90); SelectValue(compression, o.Compression);
        SelectValue(compMode, o.CompressionKind); reducePct.Value = Math.Clamp(o.ReducePercent, 1, 95); targetMb.Value = (decimal)Math.Clamp(o.TargetSizeMb, 0.05, 4096);
        allowPrint.Checked = o.AllowPrint; allowCopy.Checked = o.AllowCopy; allowEdit.Checked = o.AllowEdit;
        void SyncEnabled()
        {
            foreach (var c in new Control[] { numPos, numFmt, numStart, numSize, skipFirst }) c.Enabled = numbers.Checked;
            foreach (var c in new Control[] { wmText, wmOpacity, wmSize, wmAngle }) c.Enabled = watermark.Checked;
            var kind = ValueOf<CompressionTargetKind>(compMode);
            compression.Enabled = kind == CompressionTargetKind.Preset;
            reducePct.Enabled = kind == CompressionTargetKind.ReducePercent;
            targetMb.Enabled = kind == CompressionTargetKind.TargetSize;
        }
        compMode.SelectedIndexChanged += (_, _) => SyncEnabled();
        numbers.CheckedChanged += (_, _) => SyncEnabled();
        watermark.CheckedChanged += (_, _) => SyncEnabled();
        SyncEnabled();
        ShowTab(tabs[0].Page);
        StretchTarget = host;
    }

    private void UpdateLayoutSummary()
    {
        if (layout.IsOriginal)
        {
            layoutSummary.Text = "Every page keeps its own size.\nChoose a size below to make all pages the same.";
            return;
        }
        var orient = layout.Orientation == TargetOrientation.Portrait ? "portrait"
                   : layout.Orientation == TargetOrientation.Landscape ? "landscape" : "orientation per page";
        var fitText = layout.Fit switch
        {
            PageFitMode.ShrinkOnly => "shrink only", PageFitMode.Cover => "fill (crop)",
            PageFitMode.Stretch => "stretch", PageFitMode.ActualSize => "actual size", _ => "fit inside"
        };
        layoutSummary.Text = "All pages will be " + PageSizing.Name(layout) + "  ·  " + orient + "\nPages of other sizes are fitted: " + fitText + ".";
    }

    /// <summary>
    /// One settings page: heading, short description, then label/control rows. The final row is a stretchy filler so the
    /// real rows keep their natural height instead of being spread over the whole page (which scattered the labels).
    /// </summary>
    private static TableLayoutPanel FormPage(string heading, string blurb, params (string Label, Control Ctl)[] rows)
    {
        var t = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill, BackColor = Color.Transparent };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));

        void Full(Control c, int top, int bottom)
        {
            var r = t.RowCount++;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            c.Margin = new Padding(0, top, 0, bottom);
            t.Controls.Add(c, 0, r); t.SetColumnSpan(c, 2);
        }

        Full(new Txt(heading, 11.5f, FontStyle.Bold) { AutoSize = true }, 0, 2);
        Full(new Txt(blurb, 9f, FontStyle.Regular, true) { AutoSize = true }, 0, 12);

        foreach (var (label, ctl) in rows)
        {
            if (label.Length == 0)
            {
                if (ctl is Txt tx) { tx.Dock = DockStyle.Top; Full(tx, 2, 4); }
                else { ctl.Anchor = AnchorStyles.Left | AnchorStyles.Top; Full(ctl, 4, 4); }
                continue;
            }
            var r = t.RowCount++;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var lab = new Txt(label) { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 6), ForeColor = Theme.Muted, Muted = true };
            t.Controls.Add(lab, 0, r);
            ctl.Margin = new Padding(0, 5, 0, 5);
            if (ctl is NumericUpDown) { ctl.Anchor = AnchorStyles.Left; ctl.Width = Theme.S(120); }
            else ctl.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            t.Controls.Add(ctl, 1, r);
        }
        t.RowCount++;
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        return t;
    }

    private void ShowTab(TableLayoutPanel page)
    {
        foreach (var (tab, p) in tabs) { p.Visible = p == page; tab.Active = p == page; tab.Invalidate(); }
    }

    protected override bool ValidateInput(out string error)
    {
        error = "";
        if (watermark.Checked && string.IsNullOrWhiteSpace(wmText.Text)) error = "Enter the watermark text, or turn the watermark off.";
        Result = new ExportOptions
        {
            Title = title.Text.Trim(), Author = author.Text.Trim(), Subject = subject.Text.Trim(), Keywords = keywords.Text.Trim(), Grayscale = grayscale.Checked,
            PageNumbers = numbers.Checked, NumberPosition = ValueOf<PageNumberPosition>(numPos), NumberFormat = ValueOf<PageNumberFormat>(numFmt),
            StartNumber = (int)numStart.Value, NumberFontSize = (double)numSize.Value, SkipFirstPage = skipFirst.Checked,
            Watermark = watermark.Checked, WatermarkText = wmText.Text.Trim(), WatermarkOpacityPercent = (int)wmOpacity.Value,
            WatermarkFontSize = (double)wmSize.Value, WatermarkAngle = (double)wmAngle.Value,
            Compression = ValueOf<PdfCompression>(compression), CompressionKind = ValueOf<CompressionTargetKind>(compMode),
            ReducePercent = (int)reducePct.Value, TargetSizeMb = (double)targetMb.Value,
            UserPassword = userPw.Text, OwnerPassword = ownerPw.Text,
            AllowPrint = allowPrint.Checked, AllowCopy = allowCopy.Checked, AllowEdit = allowEdit.Checked,
            Layout = layout.Clone()
        };
        return error.Length == 0;
    }
}
