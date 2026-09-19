using System.Text;
using SkiaSharp;

namespace DeckState.StreamDeck;

public sealed class KeyCanvas(SKCanvas canvas)
{
    public KeyCanvas Fill(string color, float cornerRadius = 12)
    {
        using var paint = Paint(color);
        canvas.DrawRoundRect(0, 0, 144, 144, cornerRadius, cornerRadius, paint);
        return this;
    }

    public KeyCanvas Circle(float x, float y, float radius, string color)
    {
        using var paint = Paint(color);
        canvas.DrawCircle(x, y, radius, paint);
        return this;
    }

    public KeyCanvas Text(string text, float x, float y, float size, string color = "#FFFFFF")
    {
        using var paint = Paint(color);
        using var font = new SKFont(SKTypeface.FromFamilyName("Arial"), size);
        using var path = font.GetTextPath(Encoding.UTF8.GetBytes(text), SKTextEncoding.Utf8,
            new SKPoint(x - font.MeasureText(text) / 2, y));
        canvas.DrawPath(path, paint);
        return this;
    }

    public KeyCanvas Triangle(float x, float y, float size, string color, float rotationDegrees = 0)
    {
        canvas.Save();
        canvas.RotateDegrees(rotationDegrees, x, y);
        using var path = new SKPath();
        path.MoveTo(x, y - size);
        path.LineTo(x + size * .7f, y + size);
        path.LineTo(x - size * .7f, y + size);
        path.Close();
        using var paint = Paint(color);
        canvas.DrawPath(path, paint);
        canvas.Restore();
        return this;
    }

    private static SKPaint Paint(string color) => new() { Color = SKColor.Parse(color), IsAntialias = true };
}
