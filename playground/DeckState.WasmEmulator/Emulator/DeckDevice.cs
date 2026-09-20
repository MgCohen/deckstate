namespace DeckState.WasmEmulator.Emulator;

/// <summary>A real Stream Deck hardware layout: a fixed grid of keys.</summary>
public sealed record DeckDevice(string Name, int Rows, int Columns)
{
    public int KeyCount => Rows * Columns;

    public static readonly DeckDevice Mini = new("Stream Deck Mini", 2, 3);
    public static readonly DeckDevice Standard = new("Stream Deck", 3, 5);
    public static readonly DeckDevice Xl = new("Stream Deck XL", 4, 8);

    public static readonly IReadOnlyList<DeckDevice> All = [Mini, Standard, Xl];
}
