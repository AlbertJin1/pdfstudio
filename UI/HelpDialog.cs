using PdfStudio.Services;
using System.Drawing.Drawing2D;

namespace PdfStudio.UI;

/// <summary>Searchable, detailed user guide with a topic list on the left and formatted content on the right.</summary>
public sealed class HelpDialog : Form
{
    private sealed record Topic(string Glyph, string Title, string Body);

    // Mini markup: "## " sub-heading, "### " small label, "- " bullet, "> " tip, "|Key|Meaning" shortcut row, blank line = small gap.
    private static readonly Topic[] Topics =
    {
        new("\uE80F", "Welcome & quick start",
@"PDF Studio lets you build a PDF visually. Every page of every file you add becomes a thumbnail tile; you arrange the tiles, edit them, and export the result.

## The 5-step workflow
- Add files: click Add files, press Ctrl+O, or simply drag PDFs and pictures onto the window.
- Arrange: drag thumbnails to reorder, or use Move earlier / Move later.
- Edit: rotate, duplicate, delete, insert blank pages, add text or images.
- Check: select a page to see it large in the preview pane on the right.
- Export: click Export PDF (Ctrl+S) and choose where to save.

> Nothing is changed in your original files. PDF Studio only writes the new file you export.
> Made a mistake? Press Ctrl+Z. Every change can be undone and redone."),

        new("\uE710", "Adding files",
@"## Supported files
- PDF documents (any number of files; every page is added).
- Pictures: PNG, JPG, BMP, GIF, TIFF (multi-page TIFFs too) and WebP. A small dialog lets you choose the page size and fit.

## Ways to add
- Add files button: Add PDFs, Add images.
- Drag files from Windows Explorer onto the window to add them at the end.
- Drag files between two thumbnails to insert them exactly there (an insertion line shows where).
- More tools > Insert PDF after selection puts a whole PDF right after the selected page.

## Password-protected PDFs
If a PDF is locked, PDF Studio asks for its password. After you unlock it, the password is remembered until you close the app (it is never saved to disk).

> Adding the same file twice is allowed. It is handy for repeating a page.
> Large files are read quickly because only page sizes are read up front. Thumbnails load as you scroll."),

        new("\uE8A9", "Page grid & thumbnails",
@"The big area in the middle shows every page as a thumbnail with its page number and source file name underneath.

## Thumbnails
- Thumbnails are drawn only for the pages you can see and fade in smoothly as they finish.
- A tile that says Loading… is waiting its turn. One that says Preview unavailable could not be rendered (damaged page or locked file).
- Changing order or rotation never re-renders thumbnails, so it stays instant even with hundreds of pages.

## Size & layout
- Use the thumbnail slider, the + / − buttons in the status bar, or Ctrl + mouse wheel to resize the tiles.
- Fit columns (status bar) stretches tiles slightly so each row fills the whole width with no wasted space. Turn it off for exact sizes.
- Hide the preview pane (button at the far right of the status bar) to give the grid the whole window. The preview hides itself automatically on narrow windows.
- The mouse wheel scrolls smoothly; dragging near the top or bottom edge auto-scrolls.

## Markers on a tile
- ↻90° means you rotated the page.
- ✎ means the page has text added. ▣ means it has a picture added.
- A check circle in the corner means the page is selected."),

        new("\uE762", "Selecting pages",
@"Most commands act on the selected pages. If nothing is selected they act on the page you last clicked.

## With the mouse
- Click a page to select only that page.
- Ctrl+click adds or removes a page. Shift+click selects a range.
- Click the check circle in a tile's corner to toggle it.
- Drag a rubber-band rectangle on the empty background to select many pages. Hold Ctrl to add to the selection.

## With the keyboard
- Arrow keys, Home, End, Page Up and Page Down move the selection. Add Shift to extend it.
- Space toggles the active page. Ctrl+A selects all. Ctrl+I inverts. Esc clears the selection.

## Select menu & tools
- Odd pages / Even pages: ideal for double-sided scans.
- By page range: type something like 1-3, 5, 8-  (8- means from page 8 to the end). Words odd and even also work.
- More tools > Select blank pages finds empty or nearly empty pages (scanner separators) so you can delete them.
- More tools > Select pages from the same file picks every page that came from the same source."),

        new("\uE8CB", "Reordering & arranging",
@"## Drag and drop
- Grab any tile and drop it between two others. A purple insertion line shows where it will land.
- Select several pages first, then drag one of them: the whole group moves together (a badge shows how many).
- Press Esc while dragging to cancel. Dropping a page back where it was does nothing.
- Drag to the top or bottom edge of the grid to scroll while dragging.

## Buttons
- Move earlier / Move later nudge the selection one place (Ctrl+Shift+← / →).
- Move to > To the start, To the end, or To page number.
- Reverse flips the order of the selected pages, or of all pages when fewer than two are selected.

> Every move is a single undo step, even when you move many pages at once."),

        new("\uE70F", "Editing pages",
@"## Rotate
- Rotate left / right (Ctrl+← / Ctrl+→) turn the selected pages 90 degrees. Use it twice for upside-down pages.
- Hover over a tile to get quick Rotate and Delete buttons.
- More tools also offers Rotate 180°, Reset rotation, and Rotate ALL pages.

## Duplicate & delete
- Duplicate (Ctrl+D) places a copy right after each selected page.
- Delete (Del) removes the selected pages from your document only, never from the original file.
- More tools > Delete pages by range removes pages such as 2-4, 9.

## Blank pages
Blank page inserts empty pages: choose size (same as current page, A4, A3, A5, Letter, Legal), orientation, how many, and where.

## Undo & redo
Ctrl+Z undoes, Ctrl+Y redoes. Rotation, order, text, deletions and insertions are all covered."),

        new("\uE8D2", "Text & image overlays",
@"Add words or pictures on top of pages: titles, notes, stamps like APPROVED, company logos or a signature.

## Add text
- Click Add text (or the Text button in the preview pane).
- Type the text, choose font, size, bold/italic and colour.
- Position is chosen by anchor (left, centre, right and top, middle, bottom) plus a nudge in points. Because it is anchored to the page edges, one setting works on A4, Letter and landscape pages alike.
- Choose whether it goes on this page, the selected pages, or all pages.

## Add an image
- Click Add image and pick a PNG, JPG, BMP, GIF or TIFF.
- Set the width in millimetres; the height follows automatically.
- Use a PNG with a transparent background for signatures and logos.

## Your saved signatures
- In Edit PDF, click Signature. Your saved signatures are listed with a preview; click one to place it on the page, then drag and resize it.
- First time: choose Add signature picture and pick a PNG, JPG or scan of your signature. A PNG with a transparent background is kept as it is. For a photo or scan on white paper, tick Remove the paper background and adjust Cleanup until the checkerboard shows only your ink.
- Or choose Draw a new signature and keep Keep it in My signatures ticked.
- Saved signatures stay on this computer and are there every time you open PDF Studio. Manage saved signatures lets you rename (F2) or delete them.
- The Add image window also has a Saved signature list, for stamping a signature on many pages at once.

## Remove
More tools > Remove text and images from selected pages clears all overlays on those pages. The trash button in the preview pane does the same for the current page.

> Overlays are written into the PDF when you export. The preview pane shows them live.
> Overlays are not true redaction. They sit on top of the page and do not remove the content underneath."),

        new("\uE8C6", "Splitting & extracting",
@"## Extract
Extract saves the selected pages as a new PDF, leaving your workspace untouched.

## Split
Split cuts your current page sequence into several PDF files in a folder you choose.
- Every N pages: for example every 10 pages.
- Each page as its own PDF.
- Two parts: split after the page you have selected.
- Custom ranges: 1-3, 4-6, 9 creates three files.
Files are named Part_01.pdf, Part_02.pdf and so on (you can change the base name).

> Split and Extract use the pages as they currently appear in the grid, including your rotations, overlays and ordering."),

        new("\uE896", "Exporting",
@"Click Export PDF (Ctrl+S) to write the final document. Pages stay vector (text stays sharp and searchable); nothing is flattened to pictures.

## Export options (tabs)
- Details: document title, author, subject and keywords.
- Page numbers: position (six places), format (1, Page 1, Page 1 of 10, 1 / 10), start number, font size, and skip the cover page.
- Watermark: text, opacity, size and angle (e.g. CONFIDENTIAL, DRAFT).
- Compress & protect: compression level, grayscale, passwords and permissions.

## Compression and grayscale (built in)
- Pictures are re-encoded as JPEG and scaled down if they are larger than needed; everything else is packed tighter. Text and vector graphics are never turned into pictures.
- Light keeps print quality (about 300 dpi), Balanced suits email (about 150 dpi), Strong gives the smallest file (about 100 dpi). The result only replaces the original if it is smaller.
- Grayscale converts pictures and colours in the pages to shades of grey.

## Password protection
- Open password: required to open the file.
- Owner password: needed to change permissions. If you leave it blank, a random one is used so the permissions are still enforced.
- Allow printing, copying and editing can be switched off individually.
- Passwords are never stored; you enter them each time.

## Other export commands
- Export selected pages (More tools) writes only the selection.
- Open in viewer builds a temporary copy and opens it in your default PDF app to check the result before saving."),

        new("\uE8AB", "Converting",
@"Open the Convert menu in the top bar.

## PDF to Word
Converts the whole workspace, only the selected pages, or any PDF file to a Word (.docx) document. It is built into PDF Studio — no Word or any other program is needed to convert.

Each PDF page becomes one Word page of the same size with no margins. Text is real, editable Word text placed on its original position, font, size and colour. Lines, table borders, shapes, logos and photos are kept as a picture behind the text, so the page looks the same as the PDF.

- Graphics quality: Standard (150 dpi), High (220 dpi) or Draft (96 dpi). It only affects the picture behind the text.
- Text is placed line by line, so editing a line does not re-flow the paragraph around it. Type over a line or delete it and retype it.
- Text that is rotated or slanted stays in the picture (it looks right but is not editable).
- Scanned PDFs (pictures of text) have no real text and cannot be turned into editable text; they come out as page images.

## PDF to images
- Formats: PNG, JPEG, WebP, BMP, TIFF, GIF.
- Resolution 36 to 1200 dpi (150 is good for screens, 300 for print).
- JPEG and WebP quality slider; choose all pages, selected pages, or a range.
- Files are named with your prefix and page number.

## Images to PDF
- Page size: same as the image, A4, A3, A5, B5, Letter, Legal, Tabloid or custom.
- Orientation: automatic, portrait or landscape.
- Fit: fit inside, fill, stretch or actual size, with a margin.
- A live preview shows exactly how the picture will sit on the page. Phone photos are rotated correctly using their EXIF data."),

        new("\uE7B3", "Preview pane",
@"Select a page to see it large on the right.

- Zoom with the − and + buttons or Ctrl + mouse wheel. Fit shows the whole page.
- When zoomed in, drag the page with the mouse to pan.
- The arrow buttons step to the previous or next page.
- Rotate buttons, Text, Image and the trash button work on the current page.
- The preview includes your rotation, text and images exactly as they will be exported.

> Page numbers and watermarks are applied at export time, so they are not drawn in the preview."),

        new("\uE765", "Keyboard & mouse shortcuts",
@"## Files & export
|Ctrl+O|Add PDF files
|Ctrl+S|Export PDF
|Ctrl+Shift+S|Export selected pages
|Ctrl+P|Open the result in your PDF viewer
|F1|Open this help guide

## Selecting
|Ctrl+A|Select all pages
|Ctrl+I|Invert selection
|Esc|Clear selection / cancel a drag
|Arrow keys, Home, End|Move the selection
|Shift + arrow keys|Extend the selection
|Space|Toggle the active page

## Editing
|Ctrl+Z / Ctrl+Y|Undo / Redo
|Ctrl+← / Ctrl+→|Rotate left / right
|Ctrl+D|Duplicate
|Del|Delete
|Ctrl+Shift+← / →|Move earlier / later
|Ctrl+T|Add text

## View
|Ctrl + mouse wheel|Resize thumbnails (in the grid) or zoom (in the preview)
|Ctrl + plus / minus|Make thumbnails larger / smaller
|Double-click a tile|Select it and show it in the preview
|Right-click|Context menu with the most-used commands"),

        new("\uE713", "Optional components",
@"PDF Studio works on its own for merging, reordering, editing, images, splitting, compression, PDF to image and PDF to Word. Nothing else needs installing.

## PDF to Word
PDF to Word is built in too. No Word or any other program is needed to convert."),

        new("\uE946", "Troubleshooting & FAQ",
@"## Thumbnails stay blank or say Preview unavailable
- The PDF page may be damaged or use an unsupported feature. Try exporting it again from the original program.
- If the file is password protected, remove the page and re-add the file, entering the password when asked.

## 'This PDF is password-protected'
Add the file again and enter its password when prompted. Wrong password? You get three tries per file.

## PDF to Word text looks slightly different from the PDF
Word uses the fonts installed on your PC. If the PDF used a font you do not have, a similar one is used and the spacing is adjusted so every line keeps its width. Install the original font to get an exact match.

## Compression made no difference
The file is only replaced when the compressed version is smaller. PDFs that are mostly text, or already optimised, often cannot shrink much. Pick Strong for scans and photo-heavy files.

## The text I added is in the wrong place
Positions are anchored to the page edges after the page's own rotation. Use the Nudge values to fine-tune, and check the preview pane.

## Does PDF Studio change my original files?
Never. It only ever writes the new files you ask for.

## Where are temporary files kept?
In a session folder in your Windows temp directory. It is deleted automatically when you close the app.

## Settings
Your theme, thumbnail size and preview visibility are remembered between sessions."),

        new("\uE81C", "Version history",
            ChangeLog.ToHelpMarkup(AppInfo.Version)),

        new("\uE77B", "About & credits",
@"PDF Studio is built and maintained by:

## Jessie Albert Regualos
IT Specialist - Mindanao

## Built with
- PDFsharp for assembling pages, overlays, stamps and security.
- PDFium (through PDFtoImage) for sharp page rendering.
- Windows Forms on .NET 8 for the user interface.

Open the About window from the (i) button in the top bar for version and system information.")
    };

    // ───────────────────────── window ─────────────────────────

    private static int S(int v) => Theme.S(v);
    private static Padding P(int l, int t, int r, int b) => new(S(l), S(t), S(r), S(b));

    private readonly SearchBox search = new();
    private readonly ViewPanel navList = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly Txt navCaption = new("TOPICS", 8f, FontStyle.Bold, true) { Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft };
    private readonly RichTextBox rtb = new NoCaretRichTextBox();
    private readonly Surface contentHost = new();
    private readonly Surface nav = new();
    private readonly List<Topic> shown = new();
    private readonly List<NavItem> items = new();
    private readonly FlatButton prevBtn = new(), nextBtn = new();
    private Topic? current;

    public HelpDialog(string? startTopic = null)
    {
        // Sizes are written in design pixels and scaled once with Theme.S, so no second automatic scaling happens.
        AutoScaleMode = AutoScaleMode.None;
        Text = "PDF Studio — Help & user guide";
        Font = Theme.Ui();
        StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false; KeyPreview = true;
        var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
        ClientSize = new Size(Math.Min(S(1080), wa.Width - S(40)), Math.Min(S(740), wa.Height - S(60)));
        MinimumSize = new Size(Math.Min(S(800), wa.Width), Math.Min(S(560), wa.Height));
        BackColor = Theme.Surface; Opacity = 0;

        // ── header: title block on the left, search on the right ──
        var header = new Surface { Dock = DockStyle.Top, Height = S(80), Padding = P(28, 0, 28, 0), BorderBottom = true };
        header.UseRole(() => Theme.Surface);
        var badge = new Txt("\uE897", 14f) { Font = new Font("Segoe MDL2 Assets", 15f, FontStyle.Regular, GraphicsUnit.Point), ForeColor = Theme.Accent, AutoSize = true, Margin = P(0, 0, 14, 0), Anchor = AnchorStyles.None };
        Theme.Changed += () => { if (!IsDisposed) badge.ForeColor = Theme.Accent; };
        var title = new Txt("Help & user guide", 15f, FontStyle.Bold) { AutoSize = true, Margin = new Padding(0), Anchor = AnchorStyles.Left };
        var sub = new Txt("Answers, shortcuts and tips for every feature", 9f, FontStyle.Regular, true) { AutoSize = true, Margin = P(0, 2, 0, 0), Anchor = AnchorStyles.Left };
        var titleBlock = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = new Padding(0), Anchor = AnchorStyles.Left };
        titleBlock.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        titleBlock.RowStyles.Add(new RowStyle(SizeType.AutoSize)); titleBlock.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        titleBlock.Controls.Add(title, 0, 0); titleBlock.Controls.Add(sub, 0, 1);
        search.Width = S(340); search.Height = S(42); search.Anchor = AnchorStyles.None; search.Margin = new Padding(0);
        var headRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
        headRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(340)));
        headRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        headRow.Controls.Add(badge, 0, 0); headRow.Controls.Add(titleBlock, 1, 0); headRow.Controls.Add(search, 3, 0);
        header.Controls.Add(headRow);

        // ── topic list (left) ──
        nav.Dock = DockStyle.Left; nav.Width = S(290); nav.Padding = P(12, 12, 8, 12); nav.BorderRight = true;
        nav.UseRole(() => Theme.SurfaceAlt);
        navCaption.Height = S(28); navCaption.Padding = P(10, 0, 0, 0);
        navList.BackColor = Theme.SurfaceAlt;
        Theme.Changed += () => { if (!IsDisposed) navList.BackColor = Theme.SurfaceAlt; };
        nav.Controls.Add(navList); nav.Controls.Add(navCaption);

        // ── content (right) ──
        contentHost.Dock = DockStyle.Fill; contentHost.Padding = P(40, 26, 24, 12);
        contentHost.UseRole(() => Theme.Surface);
        rtb.Dock = DockStyle.Fill; rtb.ReadOnly = true; rtb.BorderStyle = BorderStyle.None; rtb.DetectUrls = false;
        rtb.ScrollBars = RichTextBoxScrollBars.Vertical; rtb.TabStop = false; rtb.Cursor = Cursors.Arrow;
        contentHost.Controls.Add(rtb);

        // ── footer: credit on the left, Previous / Next on the right (laid out by hand so nothing can collapse) ──
        var footer = new Surface { Dock = DockStyle.Bottom, Height = S(72), Padding = P(28, 0, 28, 0), BorderTop = true };
        footer.UseRole(() => Theme.Surface);
        var credit = new Txt($"Developed by {AppInfo.Developer}  ·  {AppInfo.DeveloperRole}", 9f, FontStyle.Regular, true) { TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Margin = new Padding(0) };
        void Setup(FlatButton b, string text, string glyph, int minW)
        {
            b.Text = text; b.Glyph = glyph; b.Kind = BtnKind.Secondary; b.Height = S(40); b.AutoWidth = true; b.MinWidth = minW;
            b.Anchor = AnchorStyles.None; b.TabStop = true;
        }
        Setup(prevBtn, "Previous", "\uE76B", 108);
        Setup(nextBtn, "Next", "\uE76C", 96);
        prevBtn.Click += (_, _) => Step(-1);
        nextBtn.Click += (_, _) => Step(+1);
        footer.Controls.Add(credit); footer.Controls.Add(prevBtn); footer.Controls.Add(nextBtn);
        void LayoutFooter()
        {
            prevBtn.FitNow(); nextBtn.FitNow();
            var y = (footer.ClientSize.Height - prevBtn.Height) / 2;
            var right = footer.ClientSize.Width - footer.Padding.Right;
            nextBtn.Location = new Point(right - nextBtn.Width, y);
            prevBtn.Location = new Point(nextBtn.Left - S(10) - prevBtn.Width, y);
            credit.SetBounds(footer.Padding.Left, 0, Math.Max(40, prevBtn.Left - S(16) - footer.Padding.Left), footer.ClientSize.Height);
        }
        footer.Resize += (_, _) => LayoutFooter();
        footer.Layout += (_, _) => LayoutFooter();
        prevBtn.SizeChanged += (_, _) => LayoutFooter();
        nextBtn.SizeChanged += (_, _) => LayoutFooter();

        Controls.Add(contentHost); Controls.Add(nav); Controls.Add(footer); Controls.Add(header);

        search.TextChangedX += () => Filter();
        Theme.Changed += OnTheme;
        Load += (_, _) => { ApplyTheme(); Filter(startTopic); };
        Shown += (_, _) => { FlatButton.FitTree(this); ModernDialog.FadeIn(this); };
        Resize += (_, _) => { nav.Width = ClientSize.Width < S(920) ? S(236) : S(290); };
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.F) { search.Focus(); search.SelectAll(); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.PageDown) { Step(+1); e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.PageUp) { Step(-1); e.SuppressKeyPress = true; }
            else if (e.Alt && e.KeyCode == Keys.Right) { Step(+1); e.SuppressKeyPress = true; }
            else if (e.Alt && e.KeyCode == Keys.Left) { Step(-1); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) { Close(); e.SuppressKeyPress = true; }
        };
    }

    private void OnTheme() { if (IsDisposed) return; BackColor = Theme.Surface; ApplyTheme(); foreach (var it in items) it.Invalidate(); if (current is not null) ShowTopic(current); }
    protected override void Dispose(bool disposing) { if (disposing) Theme.Changed -= OnTheme; base.Dispose(disposing); }

    private void ApplyTheme()
    {
        rtb.BackColor = Theme.Surface; rtb.ForeColor = Theme.Text;
        search.ApplyTheme();
    }

    /// <summary>Jumps to a topic by (part of) its title, clearing any active search so the topic is always listed.</summary>
    public void OpenTopic(string title)
    {
        search.Clear();   // re-filters by itself when a search was active
        var t = shown.FirstOrDefault(x => x.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
        if (t is not null) Select(t);
    }

    private void Step(int dir)
    {
        if (shown.Count == 0 || current is null) return;
        var i = shown.IndexOf(current) + dir;
        if (i >= 0 && i < shown.Count) Select(shown[i]);
    }

    private void Filter(string? preferTitle = null)
    {
        var q = search.Value.Trim();
        shown.Clear();
        foreach (var t in Topics)
            if (q.Length == 0 || t.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || t.Body.Contains(q, StringComparison.OrdinalIgnoreCase))
                shown.Add(t);

        navList.SuspendLayout();
        foreach (var it in items) { navList.Controls.Remove(it); it.Dispose(); }
        items.Clear();
        foreach (var t in shown)
        {
            var it = new NavItem(t.Glyph, t.Title) { Dock = DockStyle.Top, Height = S(44), Tag = t };
            it.Clicked += () => Select(t);
            items.Add(it);
        }
        for (var i = items.Count - 1; i >= 0; i--) navList.Controls.Add(items[i]);   // Dock.Top stacks last-added first
        navList.ResumeLayout(true);
        navCaption.Text = q.Length == 0 ? $"TOPICS  ·  {shown.Count}" : $"{shown.Count} MATCHING TOPIC{(shown.Count == 1 ? "" : "S")}";

        if (shown.Count == 0)
        {
            current = null; rtb.Clear();
            rtb.SelectionFont = Theme.Ui(11f); rtb.SelectionColor = Theme.Muted;
            rtb.SelectedText = $"No topics match “{q}”.\nTry a shorter word such as rotate, export or password.";
            UpdateStepButtons();
            return;
        }
        var idx = preferTitle is null ? 0 : Math.Max(0, shown.FindIndex(t => t.Title.Contains(preferTitle, StringComparison.OrdinalIgnoreCase)));
        Select(shown[idx]);
    }

    private void Select(Topic t)
    {
        foreach (var it in items) it.Selected = ReferenceEquals(it.Tag, t);
        var sel = items.FirstOrDefault(i => i.Selected);
        if (sel is not null) navList.ScrollControlIntoView(sel);
        ShowTopic(t);
        UpdateStepButtons();
    }

    private void UpdateStepButtons()
    {
        var i = current is null ? -1 : shown.IndexOf(current);
        prevBtn.Enabled = i > 0;
        nextBtn.Enabled = i >= 0 && i < shown.Count - 1;
        prevBtn.Invalidate(); nextBtn.Invalidate();
    }

    // ───────────────────────── custom controls ─────────────────────────

    /// <summary>Read-only RichTextBox that never shows the blinking text cursor (selection and copy still work).</summary>
    private sealed class NoCaretRichTextBox : RichTextBox
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool HideCaret(IntPtr hWnd);
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg is 0x0007 or 0x0201 or 0x0202 or 0x0203 or 0x0200 or 0x0100 or 0x0101 or 0x000F or 0x00B6 or 0x0115 or 0x020A) HideCaret(Handle);
        }
    }

    /// <summary>One row of the topic list: icon + title, hover and selected states, no native ListBox.</summary>
    private sealed class NavItem : Control
    {
        private readonly string glyph, title;
        private bool hover;
        private bool selected;
        public event Action? Clicked;
        public bool Selected { get => selected; set { if (selected != value) { selected = value; Invalidate(); } } }

        public NavItem(string glyph, string title)
        {
            this.glyph = glyph; this.title = title;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand; TabStop = false;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; base.OnMouseLeave(e); Invalidate(); }
        protected override void OnClick(EventArgs e) { base.OnClick(e); Clicked?.Invoke(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.SurfaceAlt);
            var r = new RectangleF(S(2), S(2), Width - S(6), Height - S(4));
            if (selected) Gfx.FillRound(g, Theme.AccentSoft, r, S(9));
            else if (hover) Gfx.FillRound(g, Gfx.Mix(Theme.SurfaceAlt, Theme.Border, 0.55f), r, S(9));
            if (selected)
                Gfx.FillRound(g, Theme.Accent, new RectangleF(r.X + S(1), r.Y + S(9), S(3), r.Height - S(18)), 1.5f);

            var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            var fg = selected ? Theme.Accent : Theme.Text;
            TextRenderer.DrawText(g, glyph, Theme.Icons(11f), new Rectangle(S(16), 0, S(26), Height), selected ? Theme.Accent : Theme.Muted, flags);
            TextRenderer.DrawText(g, title, Theme.Ui(9.75f, selected ? FontStyle.Bold : FontStyle.Regular), new Rectangle(S(50), 0, Math.Max(10, Width - S(60)), Height), fg, flags);
        }
    }

    /// <summary>Rounded search field with a magnifier and a clear (×) button.</summary>
    private sealed class SearchBox : Control
    {
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Theme.Surface); }
        private readonly TextBox box = new() { BorderStyle = BorderStyle.None };
        private bool focused, hoverClear;
        public event Action? TextChangedX;
        public string Value => box.Text;

        public SearchBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            box.Font = Theme.Ui(10f); box.PlaceholderText = "Search the guide…  (Ctrl+F)";
            box.GotFocus += (_, _) => { focused = true; Invalidate(); };
            box.LostFocus += (_, _) => { focused = false; Invalidate(); };
            box.TextChanged += (_, _) => { Invalidate(); TextChangedX?.Invoke(); };
            box.KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape && box.Text.Length > 0) { box.Clear(); e.SuppressKeyPress = true; e.Handled = true; } };
            Controls.Add(box);
            ApplyTheme();
        }

        public void ApplyTheme() { box.BackColor = Theme.Input; box.ForeColor = Theme.Text; Invalidate(); }
        public new void Focus() => box.Focus();
        public void SelectAll() => box.SelectAll();
        public void Clear() => box.Clear();

        private Rectangle ClearRect => new(Width - S(38), (Height - S(28)) / 2, S(28), S(28));

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            var h = box.PreferredHeight;
            box.SetBounds(S(42), Math.Max(0, (Height - h) / 2), Math.Max(20, Width - S(42) - S(44)), h);
        }

        protected override void OnMouseMove(MouseEventArgs e) { var h = box.Text.Length > 0 && ClearRect.Contains(e.Location); if (h != hoverClear) { hoverClear = h; Cursor = h ? Cursors.Hand : Cursors.IBeam; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { hoverClear = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (box.Text.Length > 0 && ClearRect.Contains(e.Location)) box.Clear();
            box.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Surface);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Gfx.FillRound(g, Theme.Input, r, S(10));
            Gfx.DrawRound(g, focused ? Theme.Accent : Theme.Border, r, S(10), focused ? 1.6f : 1f);
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine;
            TextRenderer.DrawText(g, "\uE721", Theme.Icons(11f), new Rectangle(S(8), 0, S(30), Height), focused ? Theme.Accent : Theme.Muted, flags);
            if (box.Text.Length > 0)
            {
                var cr = ClearRect;
                if (hoverClear) Gfx.FillRound(g, Theme.SurfaceAlt, cr, S(8));
                TextRenderer.DrawText(g, "\uE711", Theme.Icons(9f), cr, hoverClear ? Theme.Text : Theme.Muted, flags);
            }
        }
    }

    // ───────────────────────── rendering ─────────────────────────

    private void Para(int indent = 0, int hanging = 0, int tab = 60)
    {
        rtb.SelectionStart = rtb.TextLength; rtb.SelectionLength = 0;
        rtb.SelectionIndent = Theme.S(indent); rtb.SelectionHangingIndent = Theme.S(hanging);
        rtb.SelectionTabs = new[] { Theme.S(tab) };
    }

    private void Run(string text, Font font, Color color)
    {
        rtb.SelectionStart = rtb.TextLength; rtb.SelectionLength = 0;
        rtb.SelectionFont = font; rtb.SelectionColor = color;
        rtb.SelectedText = text;
    }

    private void ShowTopic(Topic t)
    {
        current = t;
        rtb.SuspendLayout();
        rtb.Clear();
        rtb.BackColor = Theme.Surface;

        Para(); Run(t.Title + "\n", Theme.Ui(21f, FontStyle.Bold), Theme.Accent);
        Para(); Run("\n", Theme.Ui(4f), Theme.Text);

        foreach (var raw in t.Body.Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0) { Para(); Run("\n", Theme.Ui(5f), Theme.Text); continue; }
            if (line.StartsWith("### "))
            {
                Para(0, 0); Run(line[4..].ToUpperInvariant() + "\n", Theme.Ui(8.75f, FontStyle.Bold), Theme.Accent);
            }
            else if (line.StartsWith("## "))
            {
                Para(); Run("\n", Theme.Ui(3f), Theme.Text);
                Para(); Run(line[3..] + "\n", Theme.Ui(13f, FontStyle.Bold), Theme.Text);
            }
            else if (line.StartsWith("- "))
            {
                Para(14, 20, 34); Run("•\t", Theme.Ui(10.5f, FontStyle.Bold), Theme.Accent); Run(line[2..] + "\n", Theme.Ui(10.5f), Theme.Text);
            }
            else if (line.StartsWith("> "))
            {
                Para(14, 0, 60); Run("Tip   ", Theme.Ui(9.5f, FontStyle.Bold), Theme.Accent); Run(line[2..] + "\n", Theme.Ui(10f, FontStyle.Italic), Theme.Muted);
            }
            else if (line.StartsWith("|"))
            {
                var parts = line.Split('|');
                Para(14, 0, 230);
                Run(parts.Length > 1 ? parts[1] : "", Theme.Ui(10f, FontStyle.Bold), Theme.Accent);
                Run("\t" + (parts.Length > 2 ? parts[2] : "") + "\n", Theme.Ui(10f), Theme.Text);
            }
            else { Para(); Run(line + "\n", Theme.Ui(10.5f), Theme.Text); }
        }

        // highlight what the person searched for
        var q = search.Value.Trim();
        if (q.Length >= 2)
        {
            var at = 0; var n = 0;
            while (n++ < 300 && (at = rtb.Text.IndexOf(q, at, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                rtb.Select(at, q.Length);
                rtb.SelectionBackColor = Theme.AccentSoft;
                at += q.Length;
            }
        }

        rtb.SelectionStart = 0; rtb.SelectionLength = 0;
        rtb.ResumeLayout();
        rtb.ScrollToCaret();
    }
}
