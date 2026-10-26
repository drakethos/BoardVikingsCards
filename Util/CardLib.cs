namespace Cards.Util;

public readonly struct CardInfo
{
    public string SpriteName { get; }
    public string FullName { get; }

    public CardInfo(string spriteName, string fullName)
    {
        SpriteName = spriteName;
        FullName = fullName;
    }
}

/// <summary>Standard 52-card deck plus Joker.</summary>
public static class CardLib
{
    private static readonly string[] SuitCodes = { "C", "H", "S", "D" };
    private static readonly string[] SuitNames = { "Clubs", "Hearts", "Spades", "Diamonds" };
    private static readonly string[] RankCodes =
        { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };
    private static readonly string[] RankNames =
        { "Ace", "2", "3", "4", "5", "6", "7", "8", "9", "10", "Jack", "Queen", "King" };

    /// <summary>All cards (52 suits + Joker). Built once.</summary>
    public static readonly CardInfo[] All = BuildAll();

    private static CardInfo[] BuildAll()
    {
        var cards = new CardInfo[SuitCodes.Length * RankCodes.Length + 1];
        int i = 0;

        for (int s = 0; s < SuitCodes.Length; s++)
        {
            for (int r = 0; r < RankCodes.Length; r++)
            {
                cards[i++] = new CardInfo(
                    SuitCodes[s] + RankCodes[r],
                    RankNames[r] + " of " + SuitNames[s]);
            }
        }

        cards[i] = new CardInfo("Joker", "Joker");
        return cards;
    }
}
