using Cards.Util;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine.UIElements;

namespace Cards.Table;

/// <summary>
/// E on the table. Choose a seat (or leave yours); the dealer also sets how many seats the table uses. Hands, the deck and the
/// rules join this panel as the table learns to deal. Built with the shared Libs window.
/// </summary>
internal sealed class TablePanel
{
    private static TablePanel? _instance;

    private readonly UkWindow _window;
    private readonly VisualElement _seats;
    private readonly VisualElement _dealer;
    private readonly Label _seatCount;
    private CardTable? _table;

    internal static void Open(CardTable table)
    {
        _instance ??= new TablePanel();
        _instance.Show(table);
    }

    private TablePanel()
    {
        _window = new UkWindow("boardvikings_table", 520f);

        _seats = new VisualElement().Column();
        _window.Body.Add(_seats);

        var divider = UkControls.Divider();
        divider.style.marginTop = 12;
        divider.style.marginBottom = 12;
        _window.Body.Add(divider);

        _dealer = new VisualElement().Row();
        _dealer.Add(UkControls.Text("Seats at this table", 15, UkTheme.TextBody, wrap: false));
        _dealer.Add(UkControls.Spacer());
        _dealer.Add(UkControls.MakeIconButton("−", () => ChangeSeats(-1), "Fewer seats"));
        _seatCount = UkControls.Text("4", 18, UkTheme.Text, heading: true, wrap: false);
        _seatCount.style.marginLeft = _seatCount.style.marginRight = 12;
        _dealer.Add(_seatCount);
        _dealer.Add(UkControls.MakeIconButton("+", () => ChangeSeats(1), "More seats"));
        _window.Body.Add(_dealer);
    }

    private void Show(CardTable table)
    {
        if (_table != null)
            _table.Changed -= Rebuild;
        _table = table;
        _table.Changed += Rebuild;
        _window.Closed = () =>
        {
            if (_table != null)
                _table.Changed -= Rebuild;
        };
        Rebuild();
        _window.Open();
    }

    private static long Me => Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0;

    private static string MyName => Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : "";

    private void Rebuild()
    {
        if (_table == null)
            return;
        var state = _table.State;
        int mine = state.SeatOf(Me);
        _window.SetHeader(_table.Title, $"{state.Taken()} of {state.seatCount} seats taken", CardBacks.DeckIcon(state.deckBack));

        _seats.Clear();
        for (int i = 0; i < state.seatCount; i++)
        {
            int seat = i;
            var s = state.seats[i];
            var row = new VisualElement().Row().Pad(8, 12).Fill(i == mine ? UkTheme.Active : UkTheme.PanelDeep).Round(UkTheme.Radius)
                .Border(UkTheme.LineSoft);
            row.style.marginBottom = 6;
            row.Add(UkControls.Text("Seat " + (i + 1), 16, UkTheme.Text, heading: true, wrap: false));
            var who = UkControls.Text(s.Taken ? "  " + s.name : "  empty", 15, s.Taken ? UkTheme.Gold : UkTheme.TextMuted, wrap: false);
            who.style.marginLeft = 12;
            row.Add(who);
            row.Add(UkControls.Spacer());

            if (i == mine)
                row.Add(UkControls.MakeButton("Leave", () => _table!.Act(TableActions.Leave(Me)), UkButtonKind.Secondary, 110f));
            else if (!s.Taken)
                row.Add(UkControls.MakeButton("Sit here", () => _table!.Act(TableActions.Sit(Me, MyName, seat)), UkButtonKind.Primary, 110f));
            _seats.Add(row);
        }

        _dealer.Show(_table.IsDealer(Me));
        _seatCount.text = state.seatCount.ToString();
    }

    private void ChangeSeats(int delta)
    {
        if (_table == null)
            return;
        _table.Act(TableActions.SetSeats(Me, _table.State.seatCount + delta));
    }
}
