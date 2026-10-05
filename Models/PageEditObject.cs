namespace PdfStudio.Models;

public enum EditKind { Text, Image, Rect, Ellipse, Line }

/// <summary>
/// One object placed with the Edit PDF window. Unlike the anchor-based text/image overlays, edit objects use absolute
/// coordinates: points, top-left origin, in the page as first displayed (the PDF's own /Rotate applied, user rotation not).
/// Text: X/Y is the top-left corner; width and height follow the text. Line: runs from (X,Y) to (X+W, Y+H).
/// </summary>
public sealed class PageEditObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public EditKind Kind { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }

    // text
    public string Text { get; set; } = string.Empty;
    public string FontName { get; set; } = "Arial";
    public double FontSize { get; set; } = 14;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public string ColorHex { get; set; } = "#000000";

    /// <summary>
    /// Horizontal fit (1 = natural). Set when an existing PDF line is converted so the replacement font, which can have
    /// slightly different advance widths than the PDF's own font, spans exactly the width of the original line.
    /// </summary>
    public double HScale { get; set; } = 1.0;

    // image
    public string ImagePath { get; set; } = string.Empty;

    // shapes (empty hex = none). Alpha 0-255.
    public string FillHex { get; set; } = string.Empty;
    public int FillAlpha { get; set; } = 255;
    public string StrokeHex { get; set; } = "#000000";
    public double StrokeWidth { get; set; } = 1.5;

    /// <summary>Set on the cover + text pair created when an existing PDF text line is edited.</summary>
    public string Tag { get; set; } = string.Empty;

    public PageEditObject Clone() => (PageEditObject)MemberwiseClone();

    public static string Hex(System.Drawing.Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
