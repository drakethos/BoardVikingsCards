using System.Collections.Generic;
using System.Linq;
using Cards.Deck;
using Cards.Util;
using Cards.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cards.Table;

/// <summary>
/// E on the table: the one panel for everyone at it. Seats and your hand and the piles for the players; the deck, the rules and
/// the chips for the dealer (and, in free play, for everybody). The table in the world only shows what the panel does.
/// Built with the shared Libs window.
/// </summary>
internal sealed class TablePanel
{
    private static TablePanel? _instance;

    private readonly UkWindow _window;
    private readonly ScrollView _scroll;
    private CardTable? _table;
    private int _selected = -1;
    private bool _giving;
    private int _dealEach = 5;

    internal static void Open(CardTable table)
    {
        _instance ??= new TablePanel();
        _instance.Show(table);
    }

    private TablePanel()
    {
        _window = new UkWindow("boardvikings_table", 720f);
        _scroll = new ScrollView(ScrollViewMode.Vertical);
        _scroll.style.maxHeight = 700;
        _scroll.style.minHeight = 260;
        _window.Body.Add(_scroll);
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
        _selected = -1;
        _giving = false;
        Rebuild();
        _window.Open();
    }

    // ---------------------------------------------------------------- small helpers

    private static long Me => CardTable.MyId;

    private static bool Visor => CardTable.MyVisor;

    private void Do(string action) => _table?.Act(action);

    private static VisualElement Section(string title)
    {
        var box = new VisualElement().Column().Fill(UkTheme.PanelDeep).Round(UkTheme.Radius + 2).Border(UkTheme.LineSoft).Pad(10, 14);
        box.style.marginBottom = 10;
        box.Add(UkControls.Text(title, 15, UkTheme.Gold, heading: true, wrap: false));
        return box;
    }

    private static VisualElement Buttons()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.marginTop = 8;
        return row;
    }

    private static Button Action(string text, System.Action onClick, UkButtonKind kind = UkButtonKind.Secondary, bool on = true)
    {
        var button = UkControls.MakeButton(text, onClick, kind);
        button.style.height = 36;
        button.style.marginRight = 6;
        button.style.marginBottom = 6;
        if (!on)
            UkControls.SetButtonEnabled(button, false);
        return button;
    }

    private static VisualElement Tiles()
    {
        var row = CardWidgets.Wrapping();
        row.style.marginTop = 8;
        return row;
    }

    private static void Mark(Button tile, bool selected)
    {
        float width = selected ? 3f : 0f;
        tile.style.borderTopWidth = tile.style.borderBottomWidth = tile.style.borderLeftWidth = tile.style.borderRightWidth = width;
        tile.style.borderTopColor = tile.style.borderBottomColor = tile.style.borderLeftColor = tile.style.borderRightColor = UkTheme.Accent;
    }

    // ---------------------------------------------------------------- the panel

    private void Rebuild()
    {
        if (_table == null)
            return;
        var state = _table.State;
        int mine = state.SeatOf(Me);
        bool seated = mine >= 0 && mine < state.seatCount;
        bool dealer = _table.IsDealerMe;
        bool open = !state.locked && state.mode == TableState.Free;
        bool canTable = dealer || (open && seated);
        if (!seated || _selected >= state.seats[mine].hand.Count)
            _selected = -1;

        string role = dealer ? "You are the dealer" : (seated ? "Seat " + (mine + 1) : "Not seated");
        _window.SetHeader(_table.Title, $"{state.Taken()} of {state.seatCount} seated · {(state.mode == TableState.Free ? "free play" : "turn order")}" +
                                         (state.locked ? " · rules locked" : "") + " · " + role, CardBacks.DeckIcon(state.deckBack));

        _scroll.Clear();
        if (seated)
            AddHand(state, mine);
        AddPiles(state, mine, seated, canTable);
        AddChips(state, mine, seated, canTable);
        AddSeats(state, mine);
        if (canTable)
            AddTableTools(state, dealer, mine);
        if (dealer)
            AddRules(state);

        // Sections keep their natural height and the panel scrolls; without this the window squeezes them to fit and they overlap.
        foreach (var element in _scroll.contentContainer.Query<VisualElement>().ToList())
            element.style.flexShrink = 0;
    }

    private void AddHand(TableState state, int mine)
    {
        var seat = state.seats[mine];
        bool myTurn = state.mode == TableState.Free || state.turn == mine || _table!.IsDealerMe;
        var box = Section($"Your hand ({seat.hand.Count})" + (state.mode == TableState.Turns ? (state.turn == mine ? " · your turn" : " · wait for your turn") : ""));

        var tiles = Tiles();
        if (seat.hand.Count == 0)
            tiles.Add(UkControls.Text("No cards yet. Draw one from the deck, or ask the dealer to deal.", 14, UkTheme.TextMuted));
        for (int i = 0; i < seat.hand.Count; i++)
        {
            int index = i;
            var tile = CardWidgets.CardTile(CardLib.CodeOf(seat.hand[i]), true, () =>
            {
                _selected = _selected == index ? -1 : index;
                _giving = false;
                Rebuild();
            });
            Mark(tile, i == _selected);
            if (seat.up[i])
                tile.tooltip = "Face up on the table";
            tiles.Add(tile);
        }

        box.Add(tiles);

        var buttons = Buttons();
        bool has = _selected >= 0;
        buttons.Add(Action("Play face up", () => Pick(i => TableActions.Play(Me, Visor, i, true)), UkButtonKind.Primary, has && myTurn));
        buttons.Add(Action("Play face down", () => Pick(i => TableActions.Play(Me, Visor, i, false)), UkButtonKind.Secondary, has && myTurn));
        buttons.Add(Action("Discard", () => Pick(i => TableActions.Discard(Me, Visor, i)), UkButtonKind.Secondary, has && myTurn));
        buttons.Add(Action("Turn over", () => Pick(i => TableActions.Flip(Me, Visor, mine, i)), UkButtonKind.Secondary, has));
        buttons.Add(Action(_giving ? "Give to…" : "Give to…", () =>
        {
            _giving = !_giving;
            Rebuild();
        }, _giving ? UkButtonKind.Selected : UkButtonKind.Secondary, has && myTurn));
        box.Add(buttons);

        if (_giving && has)
        {
            var seats = Buttons();
            for (int i = 0; i < state.seatCount; i++)
            {
                if (i == mine || !state.seats[i].Taken)
                    continue;
                int to = i;
                seats.Add(Action(state.seats[i].name, () =>
                {
                    Pick(h => TableActions.Give(Me, Visor, h, to));
                    _giving = false;
                }));
            }

            box.Add(seats);
        }

        _scroll.Add(box);
    }

    /// <summary>Run an action on the selected hand card, then clear the selection.</summary>
    private void Pick(System.Func<int, string> action)
    {
        if (_selected < 0)
            return;
        Do(action(_selected));
        _selected = -1;
    }

    private void AddPiles(TableState state, int mine, bool seated, bool canTable)
    {
        bool myTurn = seated && (state.mode == TableState.Free || state.turn == mine || _table!.IsDealerMe);
        var box = Section("On the table");

        var row = new VisualElement().Row();
        row.style.marginTop = 6;
        row.Add(UkControls.Text($"Deck: {state.deck.Count} card{(state.deck.Count == 1 ? "" : "s")}", 15, UkTheme.Text, wrap: false));
        row.Add(UkControls.Spacer());
        row.Add(UkControls.Text($"Discard: {state.discard.Count}", 15, UkTheme.Text, wrap: false));
        box.Add(row);

        var buttons = Buttons();
        buttons.Add(Action("Draw a card", () => Do(TableActions.Draw(Me, Visor)), UkButtonKind.Primary, myTurn && state.deck.Count > 0));
        buttons.Add(Action("Take the top discard", () => Do(TableActions.TakeDiscard(Me, Visor)), UkButtonKind.Secondary, myTurn && state.discard.Count > 0));
        if (state.mode == TableState.Turns)
            buttons.Add(Action("Pass the turn", () => Do(TableActions.NextTurn(Me, Visor)), UkButtonKind.Secondary,
                seated && (state.turn == mine || _table!.IsDealerMe)));
        box.Add(buttons);

        if (state.discard.Count > 0)
        {
            var top = Tiles();
            top.Add(CardWidgets.CardTile(CardLib.CodeOf(state.discard[0]), false, () => { }));
            box.Add(UkControls.Text("Top of the discard pile", 13, UkTheme.TextMuted));
            box.Add(top);
        }

        box.Add(UkControls.Text(state.played.Count == 0 ? "No cards on the felt." : "On the felt (click to pick one up):", 13, UkTheme.TextMuted));
        var felt = Tiles();
        for (int i = 0; i < state.played.Count; i++)
        {
            int index = i;
            var tile = CardWidgets.CardTile(CardLib.CodeOf(state.played[i]), seated && myTurn, () => Do(TableActions.Grab(Me, Visor, index)));
            if (!state.playedUp[i] && !(seated && state.playedBy[i] == mine))
            {
                // A card played face down: only its player (and nobody else) sees what it is.
                tile = CardWidgets.SpriteTile(CardBacks.Back(state.deckBack), "?", seated && myTurn, () => Do(TableActions.Grab(Me, Visor, index)));
            }

            felt.Add(tile);
        }

        box.Add(felt);
        _scroll.Add(box);
    }

    private void AddChips(TableState state, int mine, bool seated, bool canTable)
    {
        bool any = state.pot.Any(c => c > 0) || state.seats.Any(s => s.chips.Any(c => c > 0));
        if (!any && !canTable)
            return;
        bool myTurn = seated && (state.mode == TableState.Free || state.turn == mine || _table!.IsDealerMe);
        var box = Section("Chips");
        box.Add(UkControls.Text($"The pot: {Describe(state.pot)}", 15, UkTheme.Text));
        if (seated)
            box.Add(UkControls.Text($"Your stack: {Describe(state.seats[mine].chips)}", 15, UkTheme.Text));

        if (seated)
        {
            var bet = Buttons();
            for (int k = 0; k < Chips.Kinds; k++)
            {
                int kind = k;
                bet.Add(Action($"Bet a {Chips.Names[k].ToLowerInvariant()} chip", () => Do(TableActions.Bet(Me, Visor, kind, 1)),
                    UkButtonKind.Secondary, myTurn && state.seats[mine].chips[k] > 0));
            }

            box.Add(bet);
        }

        if (canTable && state.pot.Any(c => c > 0))
        {
            var pay = Buttons();
            pay.Add(UkControls.Text("Pay the pot to:", 14, UkTheme.TextMuted));
            for (int i = 0; i < state.seatCount; i++)
            {
                if (!state.seats[i].Taken)
                    continue;
                int seat = i;
                var button = Action(state.seats[i].name, () => Do(TableActions.Win(Me, Visor, seat)));
                button.style.marginLeft = 6;
                pay.Add(button);
            }

            box.Add(pay);
        }

        _scroll.Add(box);
    }

    private static string Describe(int[] counts)
    {
        var parts = new List<string>();
        for (int k = 0; k < Chips.Kinds; k++)
        {
            if (counts[k] > 0)
                parts.Add($"{counts[k]} {Chips.Names[k].ToLowerInvariant()}");
        }

        return parts.Count == 0 ? "empty" : string.Join(", ", parts) + $" (worth {Chips.Value(counts)})";
    }

    private void AddSeats(TableState state, int mine)
    {
        var box = Section("Seats");
        for (int i = 0; i < state.seatCount; i++)
        {
            int seat = i;
            var s = state.seats[i];
            var row = new VisualElement().Row().Pad(6, 10).Fill(i == mine ? UkTheme.Active : UkTheme.Panel).Round(UkTheme.Radius)
                .Border(UkTheme.LineSoft);
            row.style.marginTop = 6;
            string turn = state.mode == TableState.Turns && state.turn == i && s.Taken ? "  ◄ turn" : "";
            row.Add(UkControls.Text("Seat " + (i + 1), 15, UkTheme.Text, heading: true, wrap: false));
            var who = UkControls.Text(s.Taken ? "  " + s.name + turn : "  empty", 14, s.Taken ? UkTheme.Gold : UkTheme.TextMuted, wrap: false);
            who.style.marginLeft = 10;
            row.Add(who);
            row.Add(UkControls.Spacer());
            if (s.Taken)
                row.Add(UkControls.Text($"{s.hand.Count} card{(s.hand.Count == 1 ? "" : "s")}", 13, UkTheme.TextMuted, wrap: false));

            if (i == mine)
            {
                var leave = UkControls.MakeButton("Leave", () => Do(TableActions.Leave(Me, Visor)), UkButtonKind.Secondary, 90f);
                leave.style.marginLeft = 10;
                row.Add(leave);
            }
            else if (!s.Taken)
            {
                var sit = UkControls.MakeButton("Sit here", () => Do(TableActions.Sit(Me, Visor, CardTable.MyName, seat)), UkButtonKind.Primary, 90f);
                sit.style.marginLeft = 10;
                row.Add(sit);
            }

            box.Add(row);
        }

        _scroll.Add(box);
    }

    /// <summary>The deck and the cards: for the dealer, and for everybody in free play.</summary>
    private void AddTableTools(TableState state, bool dealer, int mine)
    {
        var box = Section(dealer ? "Dealer: the deck" : "The deck (free play)");
        var player = Player.m_localPlayer;
        var deckItem = DeckApi.FindDeck();

        var deck = Buttons();
        deck.Add(Action("Put my deck on the table", () => _table?.PlaceDeckFromInventory(), UkButtonKind.Primary, deckItem != null && state.deck.Count == 0));
        deck.Add(Action("Pick the deck up", () => _table?.TakeDeckToInventory(), UkButtonKind.Secondary, state.deck.Count > 0));
        deck.Add(Action("Shuffle", () => Do(TableActions.Shuffle(Me, Visor)), UkButtonKind.Secondary, state.deck.Count > 1));
        box.Add(deck);

        var deal = new VisualElement().Row();
        deal.style.marginTop = 6;
        deal.Add(UkControls.Text("Deal", 15, UkTheme.TextBody, wrap: false));
        var minus = UkControls.MakeIconButton("−", () => ChangeDeal(-1), "Fewer cards");
        minus.style.marginLeft = 10;
        deal.Add(minus);
        var each = UkControls.Text(_dealEach.ToString(), 18, UkTheme.Text, heading: true, wrap: false);
        each.style.marginLeft = each.style.marginRight = 10;
        deal.Add(each);
        deal.Add(UkControls.MakeIconButton("+", () => ChangeDeal(1), "More cards"));
        var go = UkControls.MakeButton("to everyone", () => Do(TableActions.Deal(Me, Visor, _dealEach)), UkButtonKind.Primary);
        go.style.marginLeft = 10;
        deal.Add(go);
        box.Add(deal);

        var more = Buttons();
        more.Add(Action("Collect every card", () => Do(TableActions.Collect(Me, Visor))));
        more.Add(Action(state.reveal ? "Hide the hands" : "Show every hand", () => Do(TableActions.Reveal(Me, Visor))));
        box.Add(more);
        _scroll.Add(box);
    }

    private void ChangeDeal(int delta)
    {
        _dealEach = Mathf.Clamp(_dealEach + delta, 1, 12);
        Rebuild();
    }

    /// <summary>Mode, the lock, the seat count and the starting chips: the dealer's own controls.</summary>
    private void AddRules(TableState state)
    {
        var box = Section("Dealer: the rules");
        var mode = Buttons();
        mode.Add(Action("Free play", () => Do(TableActions.SetMode(Me, Visor, TableState.Free)),
            state.mode == TableState.Free ? UkButtonKind.Selected : UkButtonKind.Secondary));
        mode.Add(Action("Turn order", () => Do(TableActions.SetMode(Me, Visor, TableState.Turns)),
            state.mode == TableState.Turns ? UkButtonKind.Selected : UkButtonKind.Secondary));
        mode.Add(Action(state.locked ? "Unlock the rules" : "Lock the rules", () => Do(TableActions.Lock(Me, Visor, !state.locked)),
            state.locked ? UkButtonKind.Primary : UkButtonKind.Secondary, !state.locked || Visor));
        box.Add(mode);
        box.Add(UkControls.Text(state.locked
            ? "Locked: only the dealer's visor changes the rules."
            : "Unlocked: free play lets anyone at the table deal and shuffle.", 13, UkTheme.TextMuted));

        var seats = new VisualElement().Row();
        seats.style.marginTop = 8;
        seats.Add(UkControls.Text("Seats at this table", 15, UkTheme.TextBody, wrap: false));
        seats.Add(UkControls.Spacer());
        seats.Add(UkControls.MakeIconButton("−", () => Do(TableActions.SetSeats(Me, Visor, state.seatCount - 1)), "Fewer seats"));
        var count = UkControls.Text(state.seatCount.ToString(), 18, UkTheme.Text, heading: true, wrap: false);
        count.style.marginLeft = count.style.marginRight = 12;
        seats.Add(count);
        seats.Add(UkControls.MakeIconButton("+", () => Do(TableActions.SetSeats(Me, Visor, state.seatCount + 1)), "More seats"));
        box.Add(seats);

        var chips = Buttons();
        chips.Add(Action("Give everyone a starting stack of chips", () => Do(TableActions.Stack(Me, Visor))));
        box.Add(chips);
        _scroll.Add(box);
    }
}
