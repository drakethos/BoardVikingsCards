using System.Collections.Generic;
using System.Linq;
using Cards.Util;
using Cards.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cards.Deck;

/// <summary>Right-click a deck: pick any card out of it by suit, put cards back, shuffle. Built with the shared Libs window.</summary>
internal sealed class DeckWindow
{

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

        _grid = CardWidgets.Wrapping();
        _window.Body.Add(_grid);

        var divider = UkControls.Divider();
        divider.style.marginTop = 10;
        divider.style.marginBottom = 10;
        _window.Body.Add(divider);

        _inventoryHint = UkControls.Text("", 14, UkTheme.TextMuted);
        _inventoryHint.style.marginBottom = 8;
        _window.Body.Add(_inventoryHint);
        _inventoryRow = CardWidgets.Wrapping();
        _window.Body.Add(_inventoryRow);

        _window.Footer.Add(UkControls.MakeButton("Shuffle", Shuffle, UkButtonKind.Primary, 140f));
        var all = UkControls.MakeButton("Put all my cards back", ReturnAll, UkButtonKind.Secondary);
        all.style.marginLeft = 8;
        _window.Footer.Add(all);
        _window.ShowFooter(true);
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
        _window.SetHeader(DeckData.DeckName + " (" + CardBacks.Names[CardBacks.Clamp(_deck.m_variant)] + ")",
            cards.Count + (cards.Count == 1 ? " card left" : " cards left"), CardBacks.DeckIcon(_deck.m_variant));

        for (int i = 0; i < _suitButtons.Length; i++)
        {
            bool joker = i == 4;
            int count = joker ? cards.Count(c => c == "Joker") : cards.Count(c => c.StartsWith(CardWidgets.SuitCodes[i]));
            _suitButtons[i].text = (joker ? "★ Joker" : CardWidgets.SuitGlyphs[i] + " " + CardWidgets.SuitNames[i]) + "  " + count;
            UkControls.SetKind(_suitButtons[i], i == _suit ? UkButtonKind.Selected : UkButtonKind.Ghost);
        }

        _grid.Clear();
        var codes = CardWidgets.CodesOfSuit(_suit);
        foreach (var code in codes)
        {
            bool inDeck = cards.Contains(code);
            var captured = code;
            _grid.Add(CardWidgets.CardTile(code, inDeck, () => Pull(captured)));
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
            _inventoryRow.Add(CardWidgets.CardTile(DeckData.LooseCardCode(item)!, true, () => Return(captured)));
        }
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
