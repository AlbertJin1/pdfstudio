using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace PdfStudio.Services;

public sealed class PdfToWordOptions
{
    /// <summary>Resolution of the picture that carries lines, shapes and photos behind the text.</summary>
    public int GraphicsDpi { get; set; } = 150;
}

public sealed class PdfToWordResult
{
    public int Pages { get; init; }
    public int TextLines { get; init; }
    /// <summary>Number of real Word tables created.</summary>
    public int Tables { get; init; }
    public List<string> Warnings { get; } = new();
}

/// <summary>
/// Built-in PDF → Word (.docx) converter — no Office, no LibreOffice.
/// For every page it
///   1. reads the text with PDFium (position, baseline, font, size, colour),
///   2. deletes exactly that text from PDFium's in-memory copy of the page and renders what is left
///      (lines, table borders, shapes, logos, photos) into one picture,
///   3. writes the picture behind real, editable text boxes placed on the original baselines.
/// Tables are the exception: their ruling lines are read from the page, rebuilt as real Word tables (grid, merged cells,
/// borders) and the text inside them is written into the cells as ordinary paragraphs — never as text boxes.
/// The Word page has the PDF's size and zero margins, so the layout matches the PDF.
/// </summary>
public sealed class PdfToWordService
{
    private const string Lib = "pdfium";

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void FPDF_InitLibrary();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr FPDF_LoadMemDocument(IntPtr data, int size, [MarshalAs(UnmanagedType.LPStr)] string? password);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void FPDF_CloseDocument(IntPtr doc);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int FPDF_GetPageCount(IntPtr doc);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr FPDF_LoadPage(IntPtr doc, int index);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void FPDF_ClosePage(IntPtr page);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern float FPDF_GetPageWidthF(IntPtr page);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern float FPDF_GetPageHeightF(IntPtr page);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int FPDFPage_GetRotation(IntPtr page);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int FPDF_PageToDevice(IntPtr page, int startX, int startY, int sizeX, int sizeY, int rotate, double pageX, double pageY, out int deviceX, out int deviceY);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr FPDFText_LoadPage(IntPtr page);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void FPDFText_ClosePage(IntPtr textPage);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int FPDFText_CountChars(IntPtr textPage);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern uint FPDFText_GetUnicode(IntPtr textPage, int index);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern double FPDFText_GetFontSize(IntPtr textPage, int index);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern uint FPDFText_GetFontInfo(IntPtr textPage, int index, byte[]? buffer, uint length, out int flags);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int FPDFText_GetFontWeight(IntPtr textPage, int index);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int FPDFText_GetFillColor(IntPtr textPage, int index, out uint r, out uint g, out uint b, out uint a);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int FPDFText_GetCharOrigin(IntPtr textPage, int index, out double x, out double y);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int FPDFText_GetCharBox(IntPtr textPage, int index, out double left, out double right, out double bottom, out double top);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern float FPDFText_GetCharAngle(IntPtr textPage, int index);
    [StructLayout(LayoutKind.Sequential)] private struct FsMatrix { public float A, B, C, D, E, F; }
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int FPDFText_GetMatrix(IntPtr textPage, int index, out FsMatrix matrix);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr FPDFText_GetTextObject(IntPtr textPage, int index);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int FPDFTextObj_GetTextRenderMode(IntPtr textObject);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int FPDFPage_CountObjects(IntPtr page);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr FPDFPage_GetObject(IntPtr page, int index);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int FPDFPage_RemoveObject(IntPtr page, IntPtr pageObj);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int FPDFPageObj_GetType(IntPtr pageObj);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void FPDFPageObj_Destroy(IntPtr pageObj);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int FPDFFormObj_CountObjects(IntPtr formObj);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr FPDFFormObj_GetObject(IntPtr formObj, uint index);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int FPDFFormObj_RemoveObject(IntPtr formObj, IntPtr subObj);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr FPDFBitmap_Create(int width, int height, int alpha);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr FPDFBitmap_GetBuffer(IntPtr bitmap);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int FPDFBitmap_GetStride(IntPtr bitmap);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void FPDFBitmap_Destroy(IntPtr bitmap);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void FPDF_RenderPageBitmap(IntPtr bitmap, IntPtr page, int startX, int startY, int sizeX, int sizeY, int rotate, int flags);

    private const int ObjText = 1, ObjImage = 3, ObjShading = 4, ObjForm = 5;
    private const int RenderAnnot = 0x01;
    private const int MaxBitmapSide = 5000;

    // ───────────────────────── characters ─────────────────────────

    private sealed class Ch
    {
        public string S = "";
        public float X0, Y0, X1, Y1, OX, OY;
        public double Size;
        public PdfFontInfo Font = null!;
        public bool Bold, Italic;
        public int Rgb;
    }

    private sealed class PageText
    {
        public List<Ch> Chars = new();
        public HashSet<IntPtr> Objects = new();
        public bool ObjectMappingAvailable = true;
    }

    /// <summary>Word bullets come out of PDFs as private-use Symbol/Wingdings code points that other fonts draw as an empty box.</summary>
    private static string MapSymbol(uint u) => u switch
    {
        0xF0B7 or 0xF06C or 0xF095 => "\u2022",
        0xF0A7 or 0xF06E or 0xF0A8 => "\u25AA",
        0xF0D8 => "\u27A2",
        0xF0FC => "\u2713",
        0xF0E0 => "\u2192",
        _ => char.ConvertFromUtf32((int)u)
    };

    private static bool IsUpright(float angle, int pageRotation)
    {
        if (float.IsNaN(angle)) return true;
        var expected = pageRotation * Math.PI / 2;
        var d = (angle - expected) % (2 * Math.PI);
        if (d > Math.PI) d -= 2 * Math.PI;
        if (d < -Math.PI) d += 2 * Math.PI;
        return Math.Abs(d) < 0.09;
    }

    private static PageText ReadChars(IntPtr page, IntPtr tp, float pw, float ph, PdfFontResolver fonts)
    {
        var res = new PageText();
        int sx = (int)Math.Round(pw * 10), sy = (int)Math.Round(ph * 10);
        var rotation = FPDFPage_GetRotation(page);
        var n = FPDFText_CountChars(tp);
        var nameBuf = new byte[256];
        var modeCache = new Dictionary<IntPtr, bool>();   // text object -> is it invisible (OCR layer)?
        var angleOk = true;

        for (var i = 0; i < n; i++)
        {
            var u = FPDFText_GetUnicode(tp, i);
            if (u == 0 || u == 0xAD || u == 0xFFFE || u == 0xFFFF) continue;
            if (u < ' ') continue;
            if (u > 0x10FFFF || (u >= 0xD800 && u <= 0xDFFF)) continue;
            if (FPDFText_GetCharBox(tp, i, out var l, out var r, out var b, out var t) == 0) continue;
            // spaces often have an empty box (justified text, word-spacing) but still separate the words, so they are kept
            var isSpace = u == 0xA0 || (u < 0x10000 && char.IsWhiteSpace((char)u));
            if (!isSpace && r - l <= 0.01 && t - b <= 0.01) continue;

            // text object this character belongs to (lets us delete exactly the text we emit, and skip invisible OCR text)
            IntPtr obj = IntPtr.Zero;
            if (res.ObjectMappingAvailable)
            {
                try { obj = FPDFText_GetTextObject(tp, i); }
                catch (EntryPointNotFoundException) { res.ObjectMappingAvailable = false; }
            }
            if (obj != IntPtr.Zero)
            {
                if (!modeCache.TryGetValue(obj, out var invisible))
                {
                    try { var mode = FPDFTextObj_GetTextRenderMode(obj); invisible = mode == 3 || mode == 7; }
                    catch (EntryPointNotFoundException) { invisible = false; }
                    modeCache[obj] = invisible;
                }
                if (invisible) continue;   // scanned page's hidden OCR text: the picture already shows it
            }

            // rotated / vertical text can't be a plain Word line: leave it in the picture instead
            if (res.ObjectMappingAvailable && angleOk)
            {
                try
                {
                    if (!IsUpright(FPDFText_GetCharAngle(tp, i), rotation)) continue;
                }
                catch (EntryPointNotFoundException) { angleOk = false; }
            }

            FPDF_PageToDevice(page, 0, 0, sx, sy, 0, l, t, out var ax, out var ay);
            FPDF_PageToDevice(page, 0, 0, sx, sy, 0, r, b, out var bx, out var by);
            float x0 = Math.Min(ax, bx) / 10f, x1 = Math.Max(ax, bx) / 10f, y0 = Math.Min(ay, by) / 10f, y1 = Math.Max(ay, by) / 10f;

            float ox = x0, oy = y1 - (y1 - y0) * 0.2f;
            try
            {
                if (FPDFText_GetCharOrigin(tp, i, out var cx0, out var cy0) != 0)
                {
                    FPDF_PageToDevice(page, 0, 0, sx, sy, 0, cx0, cy0, out var dx, out var dy);
                    ox = dx / 10f; oy = dy / 10f;
                }
            }
            catch (EntryPointNotFoundException) { }

            var len = FPDFText_GetFontInfo(tp, i, nameBuf, (uint)nameBuf.Length, out var flags);
            var rawFont = len > 1 ? Encoding.UTF8.GetString(nameBuf, 0, (int)Math.Min(len, (uint)nameBuf.Length) - 1) : "";
            var weight = FPDFText_GetFontWeight(tp, i);
            // real family from the embedded font program (PDF font names like "CIDFont+F1" say nothing)
            var face = fonts.For(obj, rawFont, flags, weight);
            var bold = face.Bold;
            var italic = face.Italic;

            var rgb = 0;
            if (FPDFText_GetFillColor(tp, i, out var cr, out var cg, out var cb, out var ca) != 0)
                rgb = ((int)Math.Min(cr, 255u) << 16) | ((int)Math.Min(cg, 255u) << 8) | (int)Math.Min(cb, 255u);

            // PDFium's font size is the size before the text matrix / page transform. Many producers draw text with a
            // scaled matrix (e.g. 13.3 pt x 0.75 = 10 pt), so the real size is font size x matrix scale.
            var size = FPDFText_GetFontSize(tp, i);
            if (!isSpace)
            {
                try
                {
                    if (FPDFText_GetMatrix(tp, i, out var tm) != 0)
                    {
                        var k = Math.Sqrt(Math.Abs((double)tm.A * tm.D - (double)tm.B * tm.C));
                        if (k > 0.01 && k < 100) size *= k;
                    }
                }
                catch (EntryPointNotFoundException) { }
            }
            else if (res.Chars.Count > 0 && Math.Abs(res.Chars[^1].OY - oy) < res.Chars[^1].Size * 0.3)
            {
                size = res.Chars[^1].Size;   // spaces PDFium makes up carry no matrix: use the neighbouring letter's size
            }
            if (double.IsNaN(size) || size < 2 || size > 400) size = Math.Max(4, (y1 - y0) / 1.15);

            res.Chars.Add(new Ch
            {
                S = isSpace ? " " : MapSymbol(u), X0 = x0, X1 = x1, Y0 = y0, Y1 = y1, OX = ox, OY = oy,
                Size = size, Font = face.Font, Bold = bold, Italic = italic, Rgb = rgb
            });
            if (obj != IntPtr.Zero) res.Objects.Add(obj);
        }
        return res;
    }

    // ───────────────────────── lines ─────────────────────────

    private static List<List<Ch>> SplitLines(List<Ch> chars)
    {
        var lines = new List<List<Ch>>();
        var cur = new List<Ch>();
        foreach (var ch in chars)
        {
            if (cur.Count > 0)
            {
                var last = cur[^1];
                var size = (float)Math.Max(ch.Size, 4);
                // same baseline = same line (spaces have zero-height boxes in PDFium, so boxes can't be compared)
                var sameLine = Math.Abs(last.OY - ch.OY) < Math.Max(size, (float)last.Size) * 0.3f;
                var gap = ch.X0 - last.X1;
                var jump = gap > size * 1.6f || gap < -size * 0.5f;
                if (!sameLine || jump) { lines.Add(cur); cur = new List<Ch>(); }
            }
            cur.Add(ch);
        }
        if (cur.Count > 0) lines.Add(cur);
        return lines;
    }

    private static DocxTextLine? BuildLine(List<Ch> chars)
    {
        while (chars.Count > 0 && chars[0].S == " ") chars.RemoveAt(0);
        while (chars.Count > 0 && chars[^1].S == " ") chars.RemoveAt(chars.Count - 1);
        if (chars.Count == 0) return null;

        // runs: consecutive characters with the same look (spaces never start a new run)
        var runs = new List<(StringBuilder Text, PdfFontInfo Font, double Size, bool Bold, bool Italic, int Rgb)>();
        var starts = new List<int>();   // index (in chars) of the first character of each run
        for (var ci = 0; ci < chars.Count; ci++)
        {
            var c = chars[ci];
            var size = Math.Round(c.Size * 2) / 2;
            if (size < 1) size = 1;
            if (runs.Count > 0)
            {
                var r = runs[^1];
                var same = ReferenceEquals(r.Font, c.Font) && Math.Abs(r.Size - size) < 0.01 && r.Bold == c.Bold && r.Italic == c.Italic && r.Rgb == c.Rgb;
                if (same || c.S == " ") { r.Text.Append(c.S); continue; }
            }
            runs.Add((new StringBuilder(c.S), c.Font, size, c.Bold, c.Italic, c.Rgb));
            starts.Add(ci);
        }

        var line = new DocxTextLine();
        double ascent = 0, descent = 0, measured = 0, maxSize = 0;
        foreach (var r in runs)
        {
            ascent = Math.Max(ascent, TextMetrics.Ascent(r.Font.MeasureFamily, r.Size, r.Bold, r.Italic));
            descent = Math.Max(descent, TextMetrics.Descent(r.Font.MeasureFamily, r.Size, r.Bold, r.Italic));
            measured += TextMetrics.Advance(r.Text.ToString(), r.Font.MeasureFamily, r.Size, r.Bold, r.Italic);
            maxSize = Math.Max(maxSize, r.Size);
        }

        // baseline = median pen-origin y of the real glyphs (ignores a stray superscript)
        var ys = chars.Where(c => c.S != " ").Select(c => c.OY).OrderBy(v => v).ToList();
        var baseline = ys.Count > 0 ? ys[ys.Count / 2] : chars[^1].Y1;

        var first = chars[0]; var lastCh = chars[^1];
        var target = (lastCh.OX - first.OX) + TextMetrics.Advance(lastCh.S, lastCh.Font.MeasureFamily, Math.Round(lastCh.Size * 2) / 2, lastCh.Bold, lastCh.Italic);
        if (target < 1) target = Math.Max(1, lastCh.X1 - first.OX);

        // Letter-spacing is worked out so that everything starts exactly where the PDF puts it, even when the fallback
        // font is a little wider/narrower than the original or the line mixes fonts. A run that matches the PDF closely
        // stays one run; a run that drifts is cut into words and spaces, each with its own spacing, so that the
        // correction lands on the gaps between words instead of stretching every letter.
        // Positions of real glyphs are reliable; the spaces PDFium makes up between words are not, so only glyph origins
        // are used as anchors and a space simply fills the gap up to the next glyph.
        var segs = new List<(string Text, int Run, int First, int Last)>();   // First/Last = indexes in chars (inclusive)
        for (var j = 0; j < runs.Count; j++)
        {
            var r = runs[j];
            var end = j + 1 < runs.Count ? starts[j + 1] : chars.Count;   // exclusive
            var text = r.Text.ToString();
            var m = TextMetrics.Advance(text, r.Font.MeasureFamily, r.Size, r.Bold, r.Italic);
            var runTarget = j + 1 < runs.Count ? chars[starts[j + 1]].OX - chars[starts[j]].OX : target - (chars[starts[j]].OX - first.OX);
            var n = text.Length;
            var uniform = n > 1 && m > 0 && runTarget > 0 ? (runTarget - m) / (j + 1 < runs.Count ? n : n - 1) : 0;
            var split = text.Contains(' ') && Math.Abs(uniform) > 0.04 * r.Size && text.Length == end - starts[j];
            if (!split) { segs.Add((text, j, starts[j], end - 1)); continue; }

            var sb = new StringBuilder(); var segFirst = starts[j]; var wasSpace = chars[starts[j]].S == " ";
            for (var ci = starts[j]; ci < end; ci++)
            {
                var sp = chars[ci].S == " ";
                if (sp != wasSpace) { segs.Add((sb.ToString(), j, segFirst, ci - 1)); sb.Clear(); segFirst = ci; wasSpace = sp; }
                sb.Append(chars[ci].S);
            }
            if (sb.Length > 0) segs.Add((sb.ToString(), j, segFirst, end - 1));
        }

        // where the last real glyph of a segment ends in the PDF (its pen origin + its own width)
        double InkEnd(int k)
        {
            var li = segs[k].Last;
            while (li > segs[k].First && chars[li].S == " ") li--;
            var c = chars[li];
            return c.OX + TextMetrics.Advance(c.S, c.Font.MeasureFamily, Math.Round(c.Size * 2) / 2, c.Bold, c.Italic);
        }
        double NextGlyphX(int k)   // x of the first real glyph at or after segment k+1
        {
            for (var q = k + 1; q < segs.Count; q++)
            {
                var ci = segs[q].First;
                while (ci <= segs[q].Last && chars[ci].S == " ") ci++;
                if (ci <= segs[q].Last) return chars[ci].OX;
            }
            return double.NaN;
        }

        var spacings = new double[segs.Count];
        double total = 0;
        double pen = first.OX;   // where the previous segments really end up in Word
        for (var k = 0; k < segs.Count; k++)
        {
            var r = runs[segs[k].Run];
            var m = TextMetrics.Advance(segs[k].Text, r.Font.MeasureFamily, r.Size, r.Bold, r.Italic);
            var n = segs[k].Text.Length;
            var isGap = segs[k].Text.Trim().Length == 0;
            var isLast = k == segs.Count - 1;
            var nextX = isLast ? double.NaN : NextGlyphX(k);
            var nextIsSpace = !isLast && segs[k + 1].Text.Trim().Length == 0;

            double desiredEnd; int denom;
            if (isLast) { desiredEnd = first.OX + target; denom = n - 1; }
            else if (double.IsNaN(nextX)) { desiredEnd = InkEnd(k); denom = n - 1; }
            else if (isGap || !nextIsSpace) { desiredEnd = nextX; denom = n; }   // a gap or a word that runs straight into the next one: end exactly at the next glyph
            else { desiredEnd = InkEnd(k); denom = n - 1; }                       // a word followed by a space: the space takes up the slack

            double sp = 0;
            if (denom > 0 && m > 0) sp = (desiredEnd - pen - m) / denom;
            sp = isGap ? Math.Clamp(sp, -0.5 * r.Size, 40) : Math.Clamp(sp, -0.15 * r.Size, 0.5 * r.Size);
            if (Math.Abs(sp) < 0.01) sp = 0;
            spacings[k] = sp;
            pen += m + sp * n;
            total += m + sp * n;
        }

        var boxW = Math.Max(target, total) * 1.12 + 6;
        var result = new DocxTextLine
        {
            X = first.OX,
            Top = baseline - ascent,
            Width = boxW,
            Height = ascent + descent + 2,
            SpacingPt = 0,
            Baseline = baseline,
            Ascent = ascent,
            Descent = descent,
            TextWidth = Math.Max(target, total),
            MaxSize = maxSize
        };
        for (var k = 0; k < segs.Count; k++)
        {
            var r = runs[segs[k].Run];
            result.Runs.Add(new DocxRun { Text = segs[k].Text, Font = r.Font.Family, Info = r.Font, SizePt = r.Size, Bold = r.Bold, Italic = r.Italic, Rgb = r.Rgb, SpacingPt = spacings[k] });
        }
        return result;
    }

    // ───────────────────────── table cells ─────────────────────────

    /// <summary>
    /// Lays the text lines of one table cell out as Word paragraphs. Lines on the same baseline become one paragraph with tab
    /// stops (columns inside a cell); the paragraph's space-before and exact line height put every baseline where the PDF has it,
    /// and left / centre / right alignment is recognised from the gaps to the cell edges so edited text behaves naturally.
    /// </summary>
    private static void FillCell(DocxCell cell, List<DocxTextLine> lines)
    {
        if (lines.Count == 0) return;
        var rows = new List<List<DocxTextLine>>();
        foreach (var l in lines.OrderBy(l => l.Baseline).ThenBy(l => l.X))
        {
            if (rows.Count > 0)
            {
                var cur = rows[^1];
                var bl = cur.Average(c => c.Baseline);
                var size = Math.Max(cur.Max(c => c.MaxSize), l.MaxSize);
                if (Math.Abs(l.Baseline - bl) < 0.3 * size) { cur.Add(l); continue; }
            }
            rows.Add(new List<DocxTextLine> { l });
        }

        var y = cell.Y0;   // bottom of the previous paragraph
        foreach (var row in rows)
        {
            row.Sort((a, b) => a.X.CompareTo(b.X));
            var baseline = row.Average(s => s.Baseline);
            var asc = row.Max(s => s.Ascent); var desc = row.Max(s => s.Descent);
            var height = Math.Max(2, asc + desc);
            var top = baseline - asc;
            var start = Math.Max(y, top);
            var p = new DocxCellPara { Before = start - y, LineHeight = height };
            y = start + height;

            var first = row[0]; var last = row[^1];
            var left = first.X - cell.X0;
            var right = cell.X1 - (last.X + last.TextWidth);
            var slack = Math.Max(6, 0.1 * last.TextWidth);   // room so a slightly wider substitute font never wraps the line
            if (row.Count == 1 && left >= 6 && Math.Abs(left - right) <= 2.5)
            {
                p.Align = 1; p.IndentLeft = -slack; p.IndentRight = -slack;
            }
            else if (row.Count == 1 && left >= 15 && right >= -1 && right <= 8 && right < left * 0.5)
            {
                p.Align = 2; p.IndentLeft = -slack; p.IndentRight = Math.Max(0, right);
            }
            else
            {
                p.IndentLeft = Math.Max(0, left); p.IndentRight = -slack;
            }
            p.Segs.AddRange(row);
            cell.Paras.Add(p);
        }
    }

    // ───────────────────────── graphics layer ─────────────────────────

    /// <summary>Deletes the emitted text from the page (or all text when PDFium can't tell us which is which); reports whether photos/gradients remain.</summary>
    private static void StripText(IntPtr container, bool isPage, HashSet<IntPtr> emitted, bool removeAll, ref bool hasPhoto, ref int remaining)
    {
        var n = isPage ? FPDFPage_CountObjects(container) : FPDFFormObj_CountObjects(container);
        for (var i = n - 1; i >= 0; i--)
        {
            var obj = isPage ? FPDFPage_GetObject(container, i) : FPDFFormObj_GetObject(container, (uint)i);
            if (obj == IntPtr.Zero) continue;
            var type = FPDFPageObj_GetType(obj);
            if (type == ObjText && (removeAll || emitted.Contains(obj)))
            {
                var ok = isPage ? FPDFPage_RemoveObject(container, obj) != 0 : FPDFFormObj_RemoveObject(container, obj) != 0;
                if (ok) { FPDFPageObj_Destroy(obj); continue; }
            }
            remaining++;
            if (type == ObjImage || type == ObjShading) hasPhoto = true;
            else if (type == ObjForm) StripText(obj, false, emitted, removeAll, ref hasPhoto, ref remaining);
        }
    }

    private static (byte[]? Data, string Ext) RenderGraphics(IntPtr page, float pw, float ph, int dpi, bool photo)
    {
        var scale = dpi / 72.0;
        var wpx = (int)Math.Round(pw * scale); var hpx = (int)Math.Round(ph * scale);
        var big = Math.Max(wpx, hpx);
        if (big > MaxBitmapSide) { var f = (double)MaxBitmapSide / big; wpx = (int)Math.Round(wpx * f); hpx = (int)Math.Round(hpx * f); }
        wpx = Math.Max(1, wpx); hpx = Math.Max(1, hpx);

        var bmp = FPDFBitmap_Create(wpx, hpx, 0);
        if (bmp == IntPtr.Zero) throw new InvalidOperationException("Not enough memory to render the page graphics.");
        try
        {
            FPDFBitmap_FillRect(bmp, 0, 0, wpx, hpx, 0xFFFFFFFF);
            FPDF_RenderPageBitmap(bmp, page, 0, 0, wpx, hpx, 0, RenderAnnot);
            var scan0 = FPDFBitmap_GetBuffer(bmp);
            var stride = FPDFBitmap_GetStride(bmp);

            // nothing left to draw? then the page is pure text and needs no picture
            var row = new byte[wpx * 4];
            var blank = true;
            for (var y = 0; y < hpx && blank; y++)
            {
                Marshal.Copy(scan0 + y * stride, row, 0, row.Length);
                for (var x = 0; x < row.Length; x += 4)
                    if (row[x] != 255 || row[x + 1] != 255 || row[x + 2] != 255) { blank = false; break; }
            }
            if (blank) return (null, "png");

            using var image = new Bitmap(wpx, hpx, stride, PixelFormat.Format32bppRgb, scan0);
            using var ms = new MemoryStream();
            if (photo)
            {
                var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                using var ep = new EncoderParameters(1);
                ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 88L);
                image.Save(ms, codec, ep);
                return (ms.ToArray(), "jpg");
            }
            image.Save(ms, ImageFormat.Png);
            return (ms.ToArray(), "png");
        }
        finally { FPDFBitmap_Destroy(bmp); }
    }

    // ───────────────────────── conversion ─────────────────────────

    public static string DefaultTitle(string pdfPath) => Path.GetFileNameWithoutExtension(pdfPath);

    /// <summary>Converts every page of <paramref name="pdfPath"/> to <paramref name="docxPath"/>.</summary>
    public PdfToWordResult Convert(string pdfPath, string docxPath, PdfToWordOptions? options = null, Action<int, int>? progress = null)
    {
        options ??= new PdfToWordOptions();
        var dpi = Math.Clamp(options.GraphicsDpi, 72, 400);
        var bytes = File.ReadAllBytes(pdfPath);
        var password = PdfPasswords.Get(pdfPath);
        var tmp = docxPath + ".tmp";
        var mem = Marshal.AllocHGlobal(bytes.Length);
        IntPtr doc = IntPtr.Zero;
        var warnings = new List<string>();
        var pageCount = 0; var lineCount = 0; var tableCount = 0;
        var fonts = new PdfFontResolver();   // font handles are only valid while this document is open

        try
        {
            Marshal.Copy(bytes, 0, mem, bytes.Length);
            lock (ImagePdfService.PdfiumGate)
            {
                FPDF_InitLibrary();
                doc = FPDF_LoadMemDocument(mem, bytes.Length, string.IsNullOrEmpty(password) ? null : password);
                if (doc == IntPtr.Zero) throw new InvalidOperationException("This PDF could not be opened (it may be damaged or password-protected).");
                pageCount = FPDF_GetPageCount(doc);
            }
            if (pageCount <= 0) throw new InvalidOperationException("This PDF has no pages.");

            using (var writer = new DocxWriter(tmp))
            {
                for (var i = 0; i < pageCount; i++)
                {
                    progress?.Invoke(i, pageCount);
                    DocxPage dp;
                    try { lock (ImagePdfService.PdfiumGate) dp = ConvertPage(doc, i, dpi, fonts); }
                    catch (Exception ex)
                    {
                        warnings.Add($"Page {i + 1} could not be converted ({ex.Message}); it was left blank.");
                        dp = new DocxPage { WidthPt = 612, HeightPt = 792 };
                    }
                    lineCount += dp.Lines.Count + dp.Tables.Sum(t => t.LineCount); tableCount += dp.Tables.Count;
                    writer.AddPage(dp);
                }
                writer.Finish(DefaultTitle(pdfPath));
            }
            progress?.Invoke(pageCount, pageCount);
            File.Move(tmp, docxPath, true);
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            throw;
        }
        finally
        {
            lock (ImagePdfService.PdfiumGate) { if (doc != IntPtr.Zero) FPDF_CloseDocument(doc); }
            Marshal.FreeHGlobal(mem);
        }

        var result = new PdfToWordResult { Pages = pageCount, TextLines = lineCount, Tables = tableCount };
        result.Warnings.AddRange(warnings);
        return result;
    }

    private static DocxPage ConvertPage(IntPtr doc, int index, int dpi, PdfFontResolver fonts)
    {
        var page = FPDF_LoadPage(doc, index);
        if (page == IntPtr.Zero) throw new InvalidOperationException("PDFium could not load the page.");
        try
        {
            float pw = FPDF_GetPageWidthF(page), ph = FPDF_GetPageHeightF(page);
            var dp = new DocxPage { WidthPt = pw, HeightPt = ph };

            // 1. text
            PageText text;
            var tp = FPDFText_LoadPage(page);
            if (tp == IntPtr.Zero) text = new PageText();
            else
            {
                try { text = ReadChars(page, tp, pw, ph, fonts); }
                finally { FPDFText_ClosePage(tp); }
            }

            // 1b. tables: ruling lines → grid; every character inside a cell goes to that cell instead of a text box
            var rules = PdfRules.Read(page, pw, ph);
            var centres = text.Chars.Select(c => (X: (c.X0 + c.X1) / 2.0, Y: c.OY - c.Size * 0.3)).ToList();
            var tables = rules.Count >= 4
                ? TableBuilder.Build(rules, pw, ph, (x0, y0, x1, y1) => centres.Any(c => c.X >= x0 && c.X <= x1 && c.Y >= y0 && c.Y <= y1))
                : new List<DocxTable>();

            var cellChars = new Dictionary<DocxCell, List<Ch>>();
            var freeChars = text.Chars;
            if (tables.Count > 0)
            {
                freeChars = new List<Ch>();
                foreach (var ch in text.Chars)
                {
                    var cx = (ch.X0 + ch.X1) / 2.0; var cy = ch.OY - ch.Size * 0.3;
                    DocxCell? best = null; var bestArea = double.MaxValue;
                    foreach (var t in tables)
                        foreach (var c in t.Cells)
                        {
                            if (cx < c.X0 || cx >= c.X1 || cy < c.Y0 || cy >= c.Y1) continue;
                            var area = (c.X1 - c.X0) * (c.Y1 - c.Y0);
                            if (area < bestArea) { best = c; bestArea = area; }
                        }
                    if (best is null) { freeChars.Add(ch); continue; }
                    if (!cellChars.TryGetValue(best, out var list)) cellChars[best] = list = new List<Ch>();
                    list.Add(ch);
                }
            }

            var seen = new HashSet<(string, int, int)>();
            foreach (var kv in cellChars)
            {
                var lines = new List<DocxTextLine>();
                foreach (var chars in SplitLines(kv.Value))
                {
                    var line = BuildLine(chars);
                    if (line is null) continue;
                    var txt = string.Concat(line.Runs.Select(r => r.Text));
                    if (!seen.Add((txt, (int)Math.Round(line.X / 2), (int)Math.Round(line.Top / 2)))) continue;
                    lines.Add(line);
                }
                FillCell(kv.Key, lines);
            }
            dp.Tables.AddRange(tables);

            foreach (var chars in SplitLines(freeChars))
            {
                var line = BuildLine(chars);
                if (line is null) continue;
                var txt = string.Concat(line.Runs.Select(r => r.Text));
                if (!seen.Add((txt, (int)Math.Round(line.X / 2), (int)Math.Round(line.Top / 2)))) continue;   // fake-bold double strikes
                dp.Lines.Add(line);
            }

            // 2. everything that is not that text → one picture
            var hasPhoto = false; var remaining = 0;
            PdfRules.Strip(tables.SelectMany(t => t.Rules));   // the table borders are Word's now: take them out of the picture
            StripText(page, true, text.Objects, removeAll: !text.ObjectMappingAvailable, ref hasPhoto, ref remaining);
            var (data, ext) = RenderGraphics(page, pw, ph, dpi, hasPhoto);
            dp.Background = data; dp.BackgroundExt = ext;
            return dp;
        }
        finally { FPDF_ClosePage(page); }
    }
}
