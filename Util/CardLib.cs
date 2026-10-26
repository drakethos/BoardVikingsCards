using System;
using System.Linq;

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

/// <summary>Standard 52-card deck plus Joker. A card's index here is its item variant.</summary>
public static class CardLib
{
    /// <summary>A card you can throw or decorate with. Crafted one at a time, the face is the variant.</summary>
    internal const string ThrowingPrefab = "Card_Throwing";

    /// <summary>A card that came out of a deck. Not a weapon; goes back into a deck.</summary>
    internal const string PlayingPrefab = "Card_Playing";

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

    internal static bool IsVariantCardPrefab(string prefabName) =>
        prefabName == ThrowingPrefab || prefabName == PlayingPrefab;

    internal static string CodeOf(int variant) => All[Math.Max(0, Math.Min(All.Length - 1, variant))].SpriteName;

    internal static string NameOf(int variant) => All[Math.Max(0, Math.Min(All.Length - 1, variant))].FullName;

    /// <summary>The variant number of a card code, or -1.</summary>
    internal static int IndexOf(string code)
    {
        for (int i = 0; i < All.Length; i++)
        {
            if (All[i].SpriteName == code)
                return i;
        }

        return -1;
    }
}
