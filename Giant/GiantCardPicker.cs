using Cards.Deck;
using Cards.Util;
using Cards.Ui;
using UnityEngine.UIElements;

namespace Cards.Giant;

/// <summary>Pick what a giant card shows: the face (any card, or the back) and the back design on its other side.</summary>
internal sealed class GiantCardPicker
{
    private const int BacksTab = 5;

    private static GiantCardPicker? _instance;

    private readonly UkWindow _window;
    private readonly VisualElement _grid;
    private readonly Label _hint;
    private readonly Button[] _tabs = new Button[6];
    private GiantCard? _card;
    private int _tab;

    internal static void Open(GiantCard card)
    {
        _instance ??= new GiantCardPicker();
        _instance.Show(card);
    }

    private GiantCardPicker()
    {
        _window = new UkWindow("boardvikings_giantcard", 760f);
        _window.SetHeader("Giant Card", "Choose both sides", CardBacks.Back(CardBacks.DefaultBack));

        var tabRow = new VisualElement().Row();
        tabRow.style.marginBottom = 8;
        for (int i = 0; i < _tabs.Length; i++)
        {
            int tab = i;
            var button = UkControls.MakeButton(i == BacksTab ? "▤ Backs" : CardWidgets.SuitLabel(i), () => SelectTab(tab), UkButtonKind.Ghost);
            button.style.marginRight = 6;
            _tabs[i] = button;
            tabRow.Add(button);
        }

        _window.Body.Add(tabRow);
        _hint = UkControls.Text("", 14, UkTheme.TextMuted);
        _hint.style.marginBottom = 8;
        _window.Body.Add(_hint);
        _grid = CardWidgets.Wrapping();
        _window.Body.Add(_grid);

        _window.Footer.Add(UkControls.MakeButton("Show the back", () => ChooseFace(GiantCard.Back), UkButtonKind.Secondary));
        var done = UkControls.MakeButton("Done", () => _window.Close(), UkButtonKind.Primary, 120f);
        done.style.marginLeft = 8;
        _window.Footer.Add(done);
        _window.ShowFooter(true);
    }

    private void Show(GiantCard card)
    {
        _card = card;
        Refresh();
        _window.Open();
    }

    private void SelectTab(int tab)
    {
        _tab = tab;
        Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < _tabs.Length; i++)
            UkControls.SetKind(_tabs[i], i == _tab ? UkButtonKind.Selected : UkButtonKind.Ghost);

        _grid.Clear();
        if (_tab == BacksTab)
        {
            _hint.text = "The design on the other side of the card. New giant cards use the last one you picked.";
            for (int i = 0; i < CardBacks.Count; i++)
            {
                int index = i;
                var tile = CardWidgets.SpriteTile(CardBacks.Back(i), CardBacks.Names[i], true, () => ChooseBack(index));
                _window.Tips.Attach(tile, () => CardBacks.Names[index]);
                _grid.Add(tile);
            }

            return;
        }

        _hint.text = "The face of the card. New giant cards use the last one you picked.";
        foreach (var code in CardWidgets.CodesOfSuit(_tab))
        {
            var captured = code;
            _grid.Add(CardWidgets.CardTile(code, true, () => ChooseFace(captured)));
        }
    }

    private void ChooseFace(string code)
    {
        if (_card == null)
            return;
        _card.SetFace(code);
        _window.Toast.Show(code == GiantCard.Back ? "Showing the back." : "Face: " + CardLib.NameOf(CardLib.IndexOf(code)), null, 2f);
    }

    private void ChooseBack(int index)
    {
        if (_card == null)
            return;
        _card.SetBackDesign(index);
        _window.Toast.Show("Back: " + CardBacks.Names[index], null, 2f);
    }
}
