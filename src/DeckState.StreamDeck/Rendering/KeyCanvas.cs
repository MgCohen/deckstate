using System.Globalization;
using System.Text;

namespace DeckState.StreamDeck;

/// <summary>
/// Fluent 144x144 key painter that emits SVG directly. Each call appends one element;
/// element order is paint order (later draws sit on top), matching a canvas painter model.
/// No native graphics dependency: the emitted SVG is rasterized by the consumer (Stream Deck,
/// a browser, the protocol harness).
/// </summary>
public sealed class KeyCanvas(StringBuilder svg)
{
    public KeyCanvas Fill(string color, float cornerRadius = 12)
    {
        var (hex, opacity) = ToSvgColor(color);
        svg.Append(CultureInfo.InvariantCulture,
            $"<rect x=\"0\" y=\"0\" width=\"144\" height=\"144\" rx=\"{N(cornerRadius)}\" ry=\"{N(cornerRadius)}\" fill=\"{hex}\"{opacity}/>");
        return this;
    }

    public KeyCanvas Circle(float x, float y, float radius, string color)
    {
        var (hex, opacity) = ToSvgColor(color);
        svg.Append(CultureInfo.InvariantCulture,
            $"<circle cx=\"{N(x)}\" cy=\"{N(y)}\" r=\"{N(radius)}\" fill=\"{hex}\"{opacity}/>");
        return this;
    }

    public KeyCanvas Text(string text, float x, float y, float size, string color = "#FFFFFF")
    {
        var (hex, opacity) = ToSvgColor(color);
        svg.Append(CultureInfo.InvariantCulture,
            $"<text x=\"{N(x)}\" y=\"{N(y)}\" font-family=\"Arial, Helvetica, sans-serif\" font-size=\"{N(size)}\" font-weight=\"600\" text-anchor=\"middle\" fill=\"{hex}\"{opacity}>{Escape(text)}</text>");
        return this;
    }

    public KeyCanvas Triangle(float x, float y, float size, string color, float rotationDegrees = 0)
    {
        var (hex, opacity) = ToSvgColor(color);
        var points = string.Create(CultureInfo.InvariantCulture,
            $"{N(x)},{N(y - size)} {N(x + size * .7f)},{N(y + size)} {N(x - size * .7f)},{N(y + size)}");
        var transform = rotationDegrees == 0
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $" transform=\"rotate({N(rotationDegrees)} {N(x)} {N(y)})\"");
        svg.Append(CultureInfo.InvariantCulture,
            $"<polygon points=\"{points}\" fill=\"{hex}\"{opacity}{transform}/>");
        return this;
    }

    private static string N(float value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    // Accepts #RRGGBB or Skia-style #AARRGGBB (alpha first). Returns an SVG fill hex plus an
    // optional fill-opacity attribute so translucent overlays match the original rendering.
    private static (string Hex, string Opacity) ToSvgColor(string color)
    {
        var c = color.StartsWith('#') ? color[1..] : color;
        byte a = 255, r, g, b;
        if (c.Length == 8)
        {
            a = Convert.ToByte(c[..2], 16);
            r = Convert.ToByte(c[2..4], 16);
            g = Convert.ToByte(c[4..6], 16);
            b = Convert.ToByte(c[6..8], 16);
        }
        else if (c.Length == 6)
        {
            r = Convert.ToByte(c[..2], 16);
            g = Convert.ToByte(c[2..4], 16);
            b = Convert.ToByte(c[4..6], 16);
        }
        else
        {
            return (color, string.Empty); // named colour or already valid: pass through
        }

        var hex = $"#{r:X2}{g:X2}{b:X2}";
        var opacity = a == 255
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $" fill-opacity=\"{a / 255.0:0.###}\"");
        return (hex, opacity);
    }

    private static string Escape(string text) => text
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");
}
