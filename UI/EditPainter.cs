using PdfStudio.Models;
using PdfStudio.Services;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace PdfStudio.UI;

/// <summary>Draws Edit PDF objects with GDI+ (editor canvas and main preview share this) and measures text.</summary>
public static class EditPainter
{
    private static readonly Dictionary<string, Font> FontCache = new();
    private static readonly Bitmap MeasureBmp = new(1, 1);
    private static readonly Dictionary<string, Image?> ImageCache = new();

    public static Font GetFont(string name, double sizePt, bool bold, bool italic, bool underline)
    {
        var size = (float)Math.Max(4, sizePt);
        var key = $"{name}|{size:0.##}|{bold}|{italic}|{underline}";
        if (FontCache.TryGetValue(key, out var f)) return f;
        try
        {
            var fam = TextMetrics.Family(name);
            var style = TextMetrics.StyleFor(fam, bold, italic) | (underline ? FontStyle.Underline : 0);
            f = new Font(fam, size, style, GraphicsUnit.Pixel);
        }
        catch { f = new Font("Arial", size, FontStyle.Regular, GraphicsUnit.Pixel); }
        if (FontCache.Count > 600) FontCache.Clear();   // not disposed: a font may still be assigned to a control
        FontCache[key] = f;
        return f;
    }

    public static SizeF MeasureText(PageEditObject o)
    {
        var lines = (o.Text ?? "").Replace("\r", "").Split('\n');
        var size = Math.Max(4, o.FontSize);
        double w = 0;
        foreach (var l in lines)
            w = Math.Max(w, TextMetrics.Advance(l.Length == 0 ? " " : l, o.FontName, size, o.Bold, o.Italic));
        w *= HScaleOf(o);
        return new SizeF((float)Math.Max(w, size * 0.5), (float)(lines.Length * size * 1.25));
    }

    public static double HScaleOf(PageEditObject o) => o.HScale is > 0.2 and < 5 ? o.HScale : 1.0;

    /// <summary>Normalised bounding box (points) of any object.</summary>
    public static RectangleF Bounds(PageEditObject o)
    {
        if (o.Kind == EditKind.Text) { var s = MeasureText(o); return new RectangleF((float)o.X, (float)o.Y, s.Width, s.Height); }
        var x = (float)Math.Min(o.X, o.X + o.W); var y = (float)Math.Min(o.Y, o.Y + o.H);
        return new RectangleF(x, y, (float)Math.Abs(o.W), (float)Math.Abs(o.H));
    }

    public static Color Parse(string hex, Color fallback)
    {
        try { return string.IsNullOrEmpty(hex) ? fallback : ColorTranslator.FromHtml(hex); } catch { return fallback; }
    }

    private static Image? LoadImage(string path)
    {
        if (ImageCache.TryGetValue(path, out var img)) return img;
        try
        {
            using var fs = new MemoryStream(File.ReadAllBytes(path));
            using var src = Image.FromStream(fs);
            img = new Bitmap(src);
        }
        catch { img = null; }
        ImageCache[path] = img;
        return img;
    }

    /// <summary>
    /// Text is filled from its glyph outlines (no hinting, no grid-fitting, sub-pixel positioned) – that is how PDFium draws
    /// the page, so edited text has the same weight and sharpness as the text around it, at any zoom.
    /// The line box top is o.Y; the baseline sits one font-ascent below it (identical rule in the PDF export).
    /// </summary>
    private static void DrawText(Graphics g, PageEditObject o)
    {
        if (string.IsNullOrEmpty(o.Text)) return;
        var fam = TextMetrics.Family(o.FontName);
        var style = TextMetrics.StyleFor(fam, o.Bold, o.Italic);
        var size = (float)Math.Max(4, o.FontSize);
        var lh = size * 1.25f;
        var asc = (float)TextMetrics.Ascent(o.FontName, size, o.Bold, o.Italic);
        var sx = (float)HScaleOf(o);
        var lines = o.Text.Replace("\r", "").Split('\n');
        using var brush = new SolidBrush(Parse(o.ColorHex, Color.Black));
        using var fmt = (StringFormat)StringFormat.GenericTypographic.Clone();
        fmt.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;

        var state = g.Save();
        g.TranslateTransform((float)o.X, (float)o.Y);
        if (Math.Abs(sx - 1f) > 0.0005f) g.ScaleTransform(sx, 1f);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length == 0) continue;
            using var path = new GraphicsPath(FillMode.Winding);
            path.AddString(line, fam, (int)style, size, new PointF(0, i * lh), fmt);
            g.FillPath(brush, path);
            if (o.Underline)
            {
                var w = (float)TextMetrics.Advance(line, o.FontName, size, o.Bold, o.Italic);
                var th = Math.Max(0.5f, size / 16f);
                g.FillRectangle(brush, 0, i * lh + asc + size * 0.11f, w, th);
            }
        }
        g.Restore(state);
    }

    /// <summary>Draws objects in point coordinates; the caller sets the Graphics transform (points → pixels).</summary>
    public static void Draw(Graphics g, IEnumerable<PageEditObject> objs, Guid? skip = null)
    {
        var oldSmooth = g.SmoothingMode; var oldText = g.TextRenderingHint;
        g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        foreach (var o in objs)
        {
            if (skip == o.Id) continue;
            switch (o.Kind)
            {
                case EditKind.Text:
                    DrawText(g, o);
                    break;
                case EditKind.Image:
                {
                    var img = LoadImage(o.ImagePath);
                    if (img != null && o.W > 0 && o.H > 0) g.DrawImage(img, (float)o.X, (float)o.Y, (float)o.W, (float)o.H);
                    break;
                }
                case EditKind.Rect:
                case EditKind.Ellipse:
                {
                    var b = Bounds(o);
                    if (b.Width <= 0 || b.Height <= 0) break;
                    if (!string.IsNullOrEmpty(o.FillHex))
                    {
                        using var br = new SolidBrush(Color.FromArgb(Math.Clamp(o.FillAlpha, 0, 255), Parse(o.FillHex, Color.White)));
                        // the cover behind edited text is pixel-snapped so it leaves no soft, visible edge
                        var crisp = o.Kind == EditKind.Rect && o.Tag.Length > 0 && o.FillAlpha >= 255;
                        var prevSm = g.SmoothingMode; var prevPo = g.PixelOffsetMode;
                        if (crisp) { g.SmoothingMode = SmoothingMode.None; g.PixelOffsetMode = PixelOffsetMode.Half; }
                        if (o.Kind == EditKind.Rect) g.FillRectangle(br, b); else g.FillEllipse(br, b);
                        g.SmoothingMode = prevSm; g.PixelOffsetMode = prevPo;
                    }
                    if (!string.IsNullOrEmpty(o.StrokeHex) && o.StrokeWidth > 0)
                    {
                        using var pen = new Pen(Parse(o.StrokeHex, Color.Black), (float)o.StrokeWidth);
                        if (o.Kind == EditKind.Rect) g.DrawRectangle(pen, b.X, b.Y, b.Width, b.Height); else g.DrawEllipse(pen, b);
                    }
                    break;
                }
                case EditKind.Line:
                {
                    using var pen = new Pen(Parse(string.IsNullOrEmpty(o.StrokeHex) ? "#000000" : o.StrokeHex, Color.Black), (float)Math.Max(0.25, o.StrokeWidth)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                    g.DrawLine(pen, (float)o.X, (float)o.Y, (float)(o.X + o.W), (float)(o.Y + o.H));
                    break;
                }
            }
        }
        g.SmoothingMode = oldSmooth; g.TextRenderingHint = oldText;
    }
}
