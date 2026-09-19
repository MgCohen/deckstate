using System.Text;
using SkiaSharp;

namespace DeckState.StreamDeck;

public static class KeySvg
{
    public static string Draw(Action<KeyCanvas> draw)
    {
        using var stream = new MemoryStream();
        using (var canvas = SKSvgCanvas.Create(new SKRect(0, 0, 144, 144), stream))
            draw(new KeyCanvas(canvas));
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
