using System.Text;

namespace DeckState.StreamDeck;

public static class KeySvg
{
    public static string Draw(Action<KeyCanvas> draw)
    {
        var svg = new StringBuilder(256);
        svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"144\" height=\"144\" viewBox=\"0 0 144 144\">");
        draw(new KeyCanvas(svg));
        svg.Append("</svg>");
        return svg.ToString();
    }
}
