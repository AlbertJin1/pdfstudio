namespace PdfStudio.Models;

public enum PageNumberPosition { BottomCenter, BottomRight, BottomLeft, TopCenter, TopRight, TopLeft }
public enum PageNumberFormat { Number, PageN, PageNOfTotal, NSlashTotal }
public enum PdfCompression { None, Light, Balanced, Strong }

public sealed class ExportOptions
{
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Keywords { get; set; } = "";

    public bool PageNumbers { get; set; }
    public PageNumberPosition NumberPosition { get; set; } = PageNumberPosition.BottomCenter;
    public PageNumberFormat NumberFormat { get; set; } = PageNumberFormat.PageNOfTotal;
    public int StartNumber { get; set; } = 1;
    public double NumberFontSize { get; set; } = 10;
    public bool SkipFirstPage { get; set; }

    public bool Watermark { get; set; }
    public string WatermarkText { get; set; } = "CONFIDENTIAL";
    public int WatermarkOpacityPercent { get; set; } = 20;
    public double WatermarkFontSize { get; set; } = 64;
    public double WatermarkAngle { get; set; } = -35;

    public PdfCompression Compression { get; set; } = PdfCompression.None;
    /// <summary>Convert colours to grey.</summary>
    public bool Grayscale { get; set; }

    public string UserPassword { get; set; } = "";
    public string OwnerPassword { get; set; } = "";
    public bool AllowPrint { get; set; } = true;
    public bool AllowCopy { get; set; } = true;
    public bool AllowEdit { get; set; } = true;

    /// <summary>One page size for the whole document (default: keep every page as it is).</summary>
    public PageLayoutOptions Layout { get; set; } = new();

    public bool HasSecurity => !string.IsNullOrEmpty(UserPassword) || !string.IsNullOrEmpty(OwnerPassword);
}
