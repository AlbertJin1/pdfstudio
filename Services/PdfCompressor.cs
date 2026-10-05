using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using PdfStudio.Models;

namespace PdfStudio.Services;

/// <summary>
/// Built-in PDF compression and grayscale conversion. Needs nothing installed: it re-encodes the pictures inside the
/// PDF (JPEG at a chosen quality, optionally scaled down), Flate-compresses every stream that is stored uncompressed,
/// and for grayscale also rewrites colour operators in page content. Text and vector art are never rasterised.
/// </summary>
public static class PdfCompressor
{
    private sealed record Profile(int Quality, int MaxDim, long MinBytes);

    private static Profile ProfileFor(PdfCompression level) => level switch
    {
        PdfCompression.Light => new Profile(85, 3500, 8_000),     // ~300 dpi on A4
        PdfCompression.Strong => new Profile(45, 1240, 2_000),    // ~100 dpi on A4
        PdfCompression.Balanced => new Profile(70, 1800, 4_000),  // ~150 dpi on A4
        _ => new Profile(90, int.MaxValue, 8_000)                  // None: only used for grayscale
    };

    /// <summary>Compresses the PDF in place. Returns (before, after) sizes in bytes. The file is only replaced if it got smaller (or when grayscale was requested).</summary>
    public static (long Before, long After) Compress(string path, PdfCompression level, bool grayscale = false)
    {
        var before = new FileInfo(path).Length;
        if (level == PdfCompression.None && !grayscale) return (before, before);
        var temp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, $"~cmp_{Guid.NewGuid():N}.pdf");
        try
        {
            var prof = ProfileFor(level);
            using (var doc = PdfReader.Open(path, PdfDocumentOpenMode.Modify))
            {
                var done = new HashSet<int>();
                foreach (var obj in doc.Internals.GetAllObjects())
                {
                    if (obj is not PdfDictionary d || d.Stream is null) continue;
                    try
                    {
                        if (Name(d, "/Subtype") == "/Image")
                        {
                            if (done.Add(obj.GetHashCode())) RecompressImage(d, prof, grayscale);
                        }
                    }
                    catch { /* leave this picture untouched */ }
                }

                if (grayscale)
                {
                    foreach (var page in doc.Pages) GrayStreamsOf(page);
                    foreach (var obj in doc.Internals.GetAllObjects())
                        if (obj is PdfDictionary d && d.Stream is not null && Name(d, "/Subtype") == "/Form") GrayStream(d);
                }

                // Flate-compress anything still stored raw (fonts, ICC profiles, form content, ...).
                foreach (var obj in doc.Internals.GetAllObjects())
                {
                    if (obj is not PdfDictionary d || d.Stream?.Value is not { Length: > 64 }) continue;
                    if (d.Elements.ContainsKey("/Filter") || Name(d, "/Type") == "/XRef") continue;
                    try { d.Stream.Zip(); } catch { }
                }
                doc.Save(temp);
            }

            var after = new FileInfo(temp).Length;
            if (after > 0 && (grayscale || after < before)) { File.Copy(temp, path, true); return (before, after); }
        }
        catch { }
        finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
        return (before, before);
    }

    // ───────────────────────── pictures ─────────────────────────

    private static void RecompressImage(PdfDictionary d, Profile prof, bool gray)
    {
        if (Bool(d, "/ImageMask") || d.Elements.ContainsKey("/Mask") || d.Elements.ContainsKey("/Decode") || d.Elements.ContainsKey("/SMaskInData")) return;
        var filter = FilterOf(d);
        if (filter != "/DCTDecode" && filter != "/FlateDecode" && filter != "") return;
        var w = d.Elements.GetInteger("/Width"); var h = d.Elements.GetInteger("/Height");
        if (w < 8 || h < 8 || (long)w * h > 120_000_000L) return;
        var comps = Components(d);
        if (comps == 0) return;
        var original = d.Stream!.Value;
        if (original is null || original.Length < prof.MinBytes && !gray) return;

        using Bitmap src = filter == "/DCTDecode" ? DecodeJpeg(original) : DecodeRaw(d, w, h, comps);
        var scale = Math.Min(1.0, (double)prof.MaxDim / Math.Max(src.Width, src.Height));
        var nw = Math.Max(1, (int)Math.Round(src.Width * scale)); var nh = Math.Max(1, (int)Math.Round(src.Height * scale));
        using var dst = new Bitmap(nw, nh, PixelFormat.Format24bppRgb);
        dst.SetResolution(96, 96);
        using (var g = Graphics.FromImage(dst))
        using (var ia = new ImageAttributes())
        {
            g.Clear(Color.White);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.CompositingQuality = CompositingQuality.HighQuality;
            ia.SetWrapMode(WrapMode.TileFlipXY);
            if (gray)
            {
                var m = new ColorMatrix(new[]
                {
                    new[] { .299f, .299f, .299f, 0, 0 }, new[] { .587f, .587f, .587f, 0, 0 }, new[] { .114f, .114f, .114f, 0, 0 },
                    new[] { 0f, 0, 0, 1, 0 }, new[] { 0f, 0, 0, 0, 1 }
                });
                ia.SetColorMatrix(m);
            }
            g.DrawImage(src, new Rectangle(0, 0, nw, nh), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
        }

        var jpg = EncodeJpeg(dst, prof.Quality);
        if (!gray && jpg.Length >= original.Length) return;

        d.Stream.Value = jpg;
        d.Elements.SetName("/Filter", "/DCTDecode");
        d.Elements.Remove("/DecodeParms");
        d.Elements.SetInteger("/Width", nw); d.Elements.SetInteger("/Height", nh);
        d.Elements.SetName("/ColorSpace", "/DeviceRGB");
        d.Elements.SetInteger("/BitsPerComponent", 8);
    }

    private static Bitmap DecodeJpeg(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var img = Image.FromStream(ms, false, false);
        var bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format24bppRgb);
        bmp.SetResolution(96, 96);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        g.DrawImage(img, new Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel);
        return bmp;
    }

    private static Bitmap DecodeRaw(PdfDictionary d, int w, int h, int comps)
    {
        if (d.Elements.GetInteger("/BitsPerComponent") != 8) throw new NotSupportedException();
        if (HasPredictor(d)) throw new NotSupportedException();
        var raw = d.Stream!.UnfilteredValue;
        if (raw is null || raw.Length < (long)w * h * comps) throw new InvalidDataException();
        var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        var bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            var row = new byte[Math.Abs(bd.Stride)];
            for (var y = 0; y < h; y++)
            {
                var si = y * w * comps;
                for (var x = 0; x < w; x++)
                {
                    if (comps == 3) { row[x * 3] = raw[si + x * 3 + 2]; row[x * 3 + 1] = raw[si + x * 3 + 1]; row[x * 3 + 2] = raw[si + x * 3]; }
                    else { var v = raw[si + x]; row[x * 3] = row[x * 3 + 1] = row[x * 3 + 2] = v; }
                }
                System.Runtime.InteropServices.Marshal.Copy(row, 0, bd.Scan0 + y * bd.Stride, w * 3);
            }
        }
        finally { bmp.UnlockBits(bd); }
        return bmp;
    }

    private static byte[] EncodeJpeg(Bitmap bmp, int quality)
    {
        var codec = ImageCodecInfo.GetImageEncoders().First(c => c.MimeType == "image/jpeg");
        using var ep = new EncoderParameters(1);
        ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
        using var ms = new MemoryStream();
        bmp.Save(ms, codec, ep);
        return ms.ToArray();
    }

    // ───────────────────────── PDF object helpers ─────────────────────────

    private static PdfItem? Resolve(PdfItem? i) => i is PdfReference r ? r.Value : i;

    private static string Name(PdfDictionary d, string key) => Resolve(d.Elements[key]) is PdfName n ? n.Value : "";

    private static bool Bool(PdfDictionary d, string key) => Resolve(d.Elements[key]) is PdfBoolean b && b.Value;

    private static string FilterOf(PdfDictionary d)
    {
        var f = Resolve(d.Elements["/Filter"]);
        if (f is null) return "";
        if (f is PdfName n) return n.Value;
        if (f is PdfArray a && a.Elements.Count == 1 && Resolve(a.Elements[0]) is PdfName n0) return n0.Value;
        return "?";
    }

    /// <summary>1 (gray), 3 (RGB) or 0 when the colour space is not one we can safely rewrite.</summary>
    private static int Components(PdfDictionary d)
    {
        var cs = Resolve(d.Elements["/ColorSpace"]);
        if (cs is PdfName n) return n.Value == "/DeviceRGB" ? 3 : n.Value == "/DeviceGray" ? 1 : 0;
        if (cs is PdfArray a && a.Elements.Count >= 2 && Resolve(a.Elements[0]) is PdfName kind)
        {
            if (kind.Value == "/CalRGB") return 3;
            if (kind.Value == "/CalGray") return 1;
            if (kind.Value == "/ICCBased" && Resolve(a.Elements[1]) is PdfDictionary icc)
            {
                var c = icc.Elements.GetInteger("/N");
                return c == 3 ? 3 : c == 1 ? 1 : 0;
            }
        }
        return 0;
    }

    private static bool HasPredictor(PdfDictionary d)
    {
        var p = Resolve(d.Elements["/DecodeParms"]) ?? Resolve(d.Elements["/DP"]);
        if (p is PdfArray a) p = a.Elements.Count > 0 ? Resolve(a.Elements[0]) : null;
        return p is PdfDictionary pd && pd.Elements.GetInteger("/Predictor") > 1;
    }

    // ───────────────────────── grayscale for page content ─────────────────────────

    private static void GrayStreamsOf(PdfPage page)
    {
        try
        {
            var contents = page.Contents.Elements;
            for (var i = 0; i < contents.Count; i++)
                if (contents.GetDictionary(i) is { Stream: not null } cd) GrayStream(cd);
        }
        catch { }
    }

    private static void GrayStream(PdfDictionary d)
    {
        try
        {
            var s = d.Stream!;
            if (!s.TryUnfilter() || s.Value is null) return;
            var converted = GrayContent(s.Value);
            if (converted is not null) s.Value = converted;
            s.Zip();
        }
        catch { }
    }

    private static bool IsWs(byte b) => b is 0 or 9 or 10 or 12 or 13 or 32;
    private static bool IsDelim(byte b) => b is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';

    private static int IndexOfToken(byte[] s, int from, string tok)
    {
        for (var i = from; i + tok.Length <= s.Length; i++)
        {
            if (i > 0 && !IsWs(s[i - 1])) continue;
            var ok = true;
            for (var k = 0; k < tok.Length && ok; k++) ok = s[i + k] == tok[k];
            if (ok && (i + tok.Length == s.Length || IsWs(s[i + tok.Length]))) return i;
        }
        return -1;
    }

    /// <summary>Rewrites rg/RG/k/K colour operators as gray g/G. Everything else is copied through untouched.</summary>
    private static byte[]? GrayContent(byte[] s)
    {
        var o = new MemoryStream(s.Length);
        var nums = new List<(int Start, double Val)>();
        int i = 0, copied = 0, n = s.Length;
        var changed = false;
        var inv = CultureInfo.InvariantCulture;

        void Replace(int start, int end, double gray, bool stroke)
        {
            o.Write(s, copied, start - copied);
            o.Write(Encoding.ASCII.GetBytes(Math.Round(Math.Clamp(gray, 0, 1), 3).ToString("0.###", inv) + (stroke ? " G" : " g")));
            copied = end; changed = true;
        }

        while (i < n)
        {
            var c = s[i];
            if (IsWs(c)) { i++; continue; }
            if (c == '%') { while (i < n && s[i] != 10 && s[i] != 13) i++; continue; }
            if (c == '(')
            {
                var depth = 0;
                for (; i < n; i++)
                {
                    if (s[i] == '\\') { i++; continue; }
                    if (s[i] == '(') depth++;
                    else if (s[i] == ')' && --depth == 0) { i++; break; }
                }
                nums.Clear(); continue;
            }
            if (c == '<')
            {
                if (i + 1 < n && s[i + 1] == '<') i += 2; else { while (i < n && s[i] != '>') i++; i++; }
                nums.Clear(); continue;
            }
            if (c == '/') { i++; while (i < n && !IsWs(s[i]) && !IsDelim(s[i])) i++; nums.Clear(); continue; }
            if (IsDelim(c)) { i++; nums.Clear(); continue; }

            var st = i;
            while (i < n && !IsWs(s[i]) && !IsDelim(s[i])) i++;
            var tok = Encoding.ASCII.GetString(s, st, i - st);
            if (double.TryParse(tok, NumberStyles.Float, inv, out var v)) { nums.Add((st, v)); continue; }

            switch (tok)
            {
                case "BI":
                {
                    var id = IndexOfToken(s, i, "ID");
                    if (id < 0) return null;
                    var ei = IndexOfToken(s, id + 3, "EI");
                    if (ei < 0) return null;
                    i = ei + 2;
                    break;
                }
                case "rg" or "RG" when nums.Count >= 3:
                {
                    var a = nums[^3];
                    Replace(a.Start, i, 0.299 * a.Val + 0.587 * nums[^2].Val + 0.114 * nums[^1].Val, tok == "RG");
                    break;
                }
                case "k" or "K" when nums.Count >= 4:
                {
                    var a = nums[^4];
                    double cy = nums[^4].Val, m = nums[^3].Val, y = nums[^2].Val, k = nums[^1].Val;
                    Replace(a.Start, i, 1 - Math.Min(1, 0.30 * cy + 0.59 * m + 0.11 * y + k), tok == "K");
                    break;
                }
            }
            nums.Clear();
        }
        if (!changed) return null;
        o.Write(s, copied, n - copied);
        return o.ToArray();
    }
}
