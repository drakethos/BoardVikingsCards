using System;
using Cards.Util;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cards.Deck;

/// <summary>Card faces as clickable tiles, shared by the deck window and the giant card picker.</summary>
internal static class CardWidgets
{
    internal static readonly string[] SuitCodes = { "C", "H", "S", "D" };
    internal static readonly string[] SuitNames = { "Clubs", "Hearts", "Spades", "Diamonds" };
    internal static readonly string[] SuitGlyphs = { "♣", "♥", "♠", "♦" };
    internal static readonly string[] RankCodes = { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };

    internal const float CardW = 76f;
    internal const float CardH = 104f;

    /// <summary>The 13 card codes of suit 0-3, or just the Joker for 4.</summary>
    internal static string[] CodesOfSuit(int suit)
    {
        if (suit == 4)
            return new[] { "Joker" };
        var codes = new string[RankCodes.Length];
        for (int i = 0; i < codes.Length; i++)
            codes[i] = SuitCodes[suit] + RankCodes[i];
        return codes;
    }

    internal static string SuitLabel(int suit) =>
        suit == 4 ? "★ Joker" : SuitGlyphs[suit] + " " + SuitNames[suit];

    internal static VisualElement Wrapping()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        return row;
    }

    internal static Button CardTile(string code, bool enabled, Action onClick) =>
        SpriteTile(SpriteTools.TryGetSprite(code), code, enabled, onClick);

    internal static Button SpriteTile(Sprite? sprite, string fallbackText, bool enabled, Action onClick)
    {
        var button = new Button(onClick);
        button.style.width = CardW;
        button.style.height = CardH;
        button.style.marginRight = button.style.marginBottom = 6;
        button.style.paddingLeft = button.style.paddingRight = button.style.paddingTop = button.style.paddingBottom = 0;
        button.style.borderTopLeftRadius = button.style.borderTopRightRadius =
            button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 6;
        button.style.backgroundColor = DrakeModsLibs.UI.Toolkit.UkTheme.Raised;
        if (sprite != null)
        {
            button.style.backgroundImage = new StyleBackground(sprite);
            button.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            button.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            button.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
        }
        else
        {
            button.text = fallbackText;
        }

        button.SetEnabled(enabled);
        button.style.opacity = enabled ? 1f : 0.18f;
        return button;
    }
}
