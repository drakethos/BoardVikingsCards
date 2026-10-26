using System.Collections.Generic;
using System.Linq;
using Cards.Util;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cards.Deck;

/// <summary>Right-click a deck: pick any card out of it by suit, put cards back, shuffle. Built with the shared Libs window.</summary>
internal sealed class DeckWindow
{
    private static readonly string[] SuitCodes = { "C", "H", "S", "D" };
    private static readonly string[] SuitNames = { "Clubs", "Hearts", "Spades", "Diamonds" };
    private static readonly string[] SuitGlyphs = { "♣", "♥", "♠", "♦" };
    private static readonly string[] RankCodes = { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };
    private const float CardW = 76f;
    private const float CardH = 104f;

    private static DeckWindow? _instance;

    private readonly UkWindow _window;
    private readonly VisualElement _suitRow;
    private readonly VisualElement _grid;
    private readonly VisualElement _inventoryRow;
    private readonly Label _inventoryHint;
    private readonly Button[] _suitButtons = new Button[5];
    private ItemDrop.ItemData? _deck;
    private int _suit;

    internal static void Open(ItemDrop.ItemData deck)
    {
        _instance ??= new DeckWindow();
        _instance.Show(deck);
    }

    private DeckWindow()
    {
        _window = new UkWindow("boardvikings_deck", 760f);

        _suitRow = new VisualElement().Row();
        _suitRow.style.marginBottom = 12;
        for (int i = 0; i < _suitButtons.Length; i++)
        {
            int suit = i;
            var button = UkControls.MakeButton("", () => SelectSuit(suit), UkButtonKind.Ghost);
            button.style.marginRight = 6;
            _suitButtons[i] = button;
            _suitRow.Add(button);
        }

        _window.Body.Add(_suitRow);

        _grid = Wrapping();
        _window.Body.Add(_grid);

        var divider = UkControls.Divider();
        divider.style.marginTop = 10;
        divider.style.marginBottom = 10;
        _window.Body.Add(divider);

        _inventoryHint = UkControls.Text("", 14, UkTheme.TextMuted);
        _inventoryHint.style.marginBottom = 8;
        _window.Body.Add(_inventoryHint);
        _inventoryRow = Wrapping();
        _window.Body.Add(_inventoryRow);

        _window.Footer.Add(UkControls.MakeButton("Shuffle", Shuffle, UkButtonKind.Primary, 140f));
        var all = UkControls.MakeButton("Put all my cards back", ReturnAll, UkButtonKind.Secondary);
        all.style.marginLeft = 8;
        _window.Footer.Add(all);
        _window.ShowFooter(true);
    }

    private static VisualElement Wrapping()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        return row;
    }

    private void Show(ItemDrop.ItemData deck)
    {
        _deck = deck;
        Refresh();
        _window.Open();
    }

    private void SelectSuit(int suit)
    {
        _suit = suit;
        Refresh();
    }

    private void Refresh()
    {
        if (_deck == null)
            return;

        var cards = DeckData.Read(_deck);
        _window.SetHeader(DeckData.DeckName, cards.Count + (cards.Count == 1 ? " card left" : " cards left"),
            SpriteTools.TryGetSprite("back"));

        for (int i = 0; i < _suitButtons.Length; i++)
        {
            bool joker = i == 4;
            int count = joker ? cards.Count(c => c == "Joker") : cards.Count(c => c.StartsWith(SuitCodes[i]));
            _suitButtons[i].text = (joker ? "★ Joker" : SuitGlyphs[i] + " " + SuitNames[i]) + "  " + count;
            UkControls.SetKind(_suitButtons[i], i == _suit ? UkButtonKind.Selected : UkButtonKind.Ghost);
        }

        _grid.Clear();
        var codes = _suit == 4 ? new[] { "Joker" } : RankCodes.Select(r => SuitCodes[_suit] + r).ToArray();
        foreach (var code in codes)
        {
            bool inDeck = cards.Contains(code);
            var captured = code;
            _grid.Add(CardTile(code, inDeck, () => Pull(captured)));
        }

        _inventoryRow.Clear();
        var player = Player.m_localPlayer;
        var loose = player == null
            ? new List<ItemDrop.ItemData>()
            : player.GetInventory().GetAllItems().Where(i => DeckData.LooseCardCode(i) != null).ToList();
        _inventoryHint.text = loose.Count == 0
            ? "Cards you carry will show here. Click one to put it back in the deck."
            : "Your cards. Click one to put it back on top of the deck.";
        foreach (var item in loose)
        {
            var captured = item;
            _inventoryRow.Add(CardTile(DeckData.LooseCardCode(item)!, true, () => Return(captured)));
        }
    }

    private static Button CardTile(string code, bool enabled, System.Action onClick)
    {
        var button = new Button(onClick);
        button.style.width = CardW;
        button.style.height = CardH;
        button.style.marginRight = button.style.marginBottom = 6;
        button.style.paddingLeft = button.style.paddingRight = button.style.paddingTop = button.style.paddingBottom = 0;
        button.style.borderTopLeftRadius = button.style.borderTopRightRadius =
            button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 6;
        button.style.backgroundColor = UkTheme.Raised;
        var sprite = SpriteTools.TryGetSprite(code);
        if (sprite != null)
        {
            button.style.backgroundImage = new StyleBackground(sprite);
            button.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            button.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            button.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
        }
        else
        {
            button.text = code;
        }

        button.SetEnabled(enabled);
        button.style.opacity = enabled ? 1f : 0.18f;
        return button;
    }

    private void Pull(string code)
    {
        if (_deck == null)
            return;
        if (!DeckApi.Pull(_deck, code))
        {
            _window.Toast.Show("Your inventory is full.", null, 3f);
            return;
        }

        Refresh();
    }

    private void Return(ItemDrop.ItemData item)
    {
        if (_deck != null && DeckApi.Return(_deck, item))
            Refresh();
    }

    private void ReturnAll()
    {
        if (_deck == null)
            return;
        int moved = DeckApi.ReturnAll(_deck);
        Refresh();
        _window.Toast.Show(moved == 0 ? "No cards to put back." : moved + " cards back in the deck.", null, 3f);
    }

    private void Shuffle()
    {
        if (_deck == null)
            return;
        DeckApi.Shuffle(_deck);
        Refresh();
        _window.Toast.Show("Shuffled.", null, 2f);
    }
}
