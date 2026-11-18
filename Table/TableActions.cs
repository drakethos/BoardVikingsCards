using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cards.Table;

/// <summary>
/// Everything a player or the dealer can do at the table, as short text commands ("play|id|vis|card|up"). The table's owner runs
/// them against the saved state, so every client sees the same table. Free play checks nothing except who may change the rules;
/// turn order only holds players to their turn. Who is the dealer: the first player to use the table (press E, sit, or deal), until
/// they leave. The dealer visor (the "vis" flag) is also the dealer, and once the rules are locked only the visor changes them.
/// </summary>
internal static class TableActions
{
    // ---- command builders (what the panel and the tests send)

    private static string Head(string cmd, long player, bool visor) => $"{cmd}|{player}|{(visor ? 1 : 0)}";

    internal static string Sit(long player, bool visor, string name, int seat) =>
        $"{Head("sit", player, visor)}|{Clean(name)}|{seat}";

    /// <summary>Press E on the table. The first player to do this, while nobody is sitting as dealer, becomes the dealer.</summary>
    internal static string Touch(long player, bool visor, string name) => $"{Head("touch", player, visor)}|{Clean(name)}";

    internal static string Leave(long player, bool visor) => Head("leave", player, visor);

    internal static string SetSeats(long player, bool visor, int seats) => $"{Head("seats", player, visor)}|{seats}";

    internal static string PlaceDeck(long player, bool visor, int back, IEnumerable<int> cards) =>
        $"{Head("placedeck", player, visor)}|{back}|{string.Join(",", cards)}";

    internal static string TakeDeck(long player, bool visor) => Head("takedeck", player, visor);

    internal static string Shuffle(long player, bool visor) => Head("shuffle", player, visor);

    internal static string Deal(long player, bool visor, int each) => $"{Head("deal", player, visor)}|{each}";

    internal static string Draw(long player, bool visor) => Head("draw", player, visor);

    /// <summary>Play a card from my hand onto the first spread (face up or down).</summary>
    internal static string Play(long player, bool visor, int handIndex, bool faceUp) =>
        $"{Head("play", player, visor)}|{handIndex}|{(faceUp ? 1 : 0)}";

    /// <summary>
    /// Lay a hand card on a spread. Spread -1 starts a new row; -2 adds to the latest row; 0, 1, 2... are specific rows.
    /// </summary>
    internal static string Lay(long player, bool visor, int handIndex, int spread, bool faceUp) =>
        $"{Head("lay", player, visor)}|{handIndex}|{spread}|{(faceUp ? 1 : 0)}";

    /// <summary>Lay a card I was carrying (it is no longer an item) onto a spread. Same spread numbers as <see cref="Lay"/>.</summary>
    internal static string PlaceCard(long player, bool visor, int card, int spread, bool faceUp) =>
        $"{Head("place", player, visor)}|{card}|{spread}|{(faceUp ? 1 : 0)}";

    /// <summary>Deal cards from the deck onto a spread, face up. Spread -1 starts a new row; -2 adds to the latest.</summary>
    internal static string DealSpread(long player, bool visor, int count, int spread) =>
        $"{Head("dealspread", player, visor)}|{count}|{spread}";

    internal static string Discard(long player, bool visor, int handIndex) => $"{Head("discard", player, visor)}|{handIndex}";

    /// <summary>Hand one of my cards to another seat.</summary>
    internal static string Give(long player, bool visor, int handIndex, int toSeat) =>
        $"{Head("give", player, visor)}|{handIndex}|{toSeat}";

    /// <summary>Turn a hand card over. Players turn their own; the dealer turns anyone's.</summary>
    internal static string Flip(long player, bool visor, int seat, int handIndex) =>
        $"{Head("flip", player, visor)}|{seat}|{handIndex}";

    /// <summary>Pick a card up off the felt into my hand (flat index across every spread).</summary>
    internal static string Grab(long player, bool visor, int playedIndex) => $"{Head("grab", player, visor)}|{playedIndex}";

    /// <summary>Pick one card up out of a spread into my hand (the card's place in that row, not across all rows).</summary>
    internal static string Pickup(long player, bool visor, int spread, int indexInSpread) =>
        $"{Head("pickup", player, visor)}|{spread}|{indexInSpread}";

    internal static string TakeDiscard(long player, bool visor) => Head("takediscard", player, visor);

    internal static string Collect(long player, bool visor) => Head("collect", player, visor);

    internal static string Reveal(long player, bool visor) => Head("reveal", player, visor);

    internal static string SetMode(long player, bool visor, string mode) => $"{Head("mode", player, visor)}|{mode}";

    internal static string Lock(long player, bool visor, bool locked) => $"{Head("lock", player, visor)}|{(locked ? 1 : 0)}";

    internal static string NextTurn(long player, bool visor) => Head("next", player, visor);

    /// <summary>Put chips from my stack into the pot.</summary>
    internal static string Bet(long player, bool visor, int kind, int count) => $"{Head("bet", player, visor)}|{kind}|{count}";

    /// <summary>Pay the whole pot to a seat.</summary>
    internal static string Win(long player, bool visor, int seat) => $"{Head("win", player, visor)}|{seat}";

    /// <summary>Chips from my pocket onto the table (the panel takes them out of my inventory first).</summary>
    internal static string BuyIn(long player, bool visor, int kind, int count) => $"{Head("buyin", player, visor)}|{kind}|{count}";

    /// <summary>Chips from my stack back into my pocket (the panel gives me the items).</summary>
    internal static string CashOut(long player, bool visor, int kind, int count) => $"{Head("cashout", player, visor)}|{kind}|{count}";

    /// <summary>The dealer gives every seated player a starting stack of chips.</summary>
    internal static string Stack(long player, bool visor) => Head("stack", player, visor);

    private static string Clean(string text) => (text ?? "").Replace("|", " ");

    // ---- running a command

    /// <summary>Apply one command to the state. Returns true when the state changed.</summary>
    /// <param name="creator">Kept so older callers still compile. The builder is not the dealer.</param>
    internal static bool Apply(TableState state, string action, long creator)
    {
        _ = creator;
        if (string.IsNullOrEmpty(action))
            return false;
        var p = action.Split('|');
        if (p.Length < 3 || !long.TryParse(p[1], out var player))
            return false;
        bool visor = p[2] == "1";
        var args = p.Skip(3).ToArray();

        int me = state.SeatOf(player);
        bool seated = me >= 0 && me < state.seatCount;
        // Nobody holds the deal yet: the first setup action (deal, seats, shuffle, ...) claims it.
        bool claimOnSuccess = false;
        bool dealer = state.PlayerIsDealer(player, visor);
        if (!dealer && state.dealer == 0 && !state.locked && player != 0 && CanClaim(p[0]))
        {
            dealer = true;
            claimOnSuccess = true;
        }

        bool open = !state.locked && state.mode == TableState.Free;
        bool onTable = dealer || (open && seated);
        bool myTurn = seated && (state.mode == TableState.Free || state.turn == me || dealer);

        bool changed = p[0] switch
        {
            "touch" => Touch(state, player, args.Length > 0 ? args[0] : ""),
            "sit" when args.Length >= 2 && int.TryParse(args[1], out var seat) => Sit(state, player, args[0], seat),
            "leave" => Leave(state, player),
            "seats" when dealer && args.Length >= 1 && int.TryParse(args[0], out var count) => SetSeats(state, count),

            "placedeck" when onTable && args.Length >= 2 && int.TryParse(args[0], out var back) => PlaceDeck(state, back, args[1]),
            "takedeck" when onTable => TakeDeck(state),
            "shuffle" when onTable => Shuffle(state),
            "deal" when onTable && args.Length >= 1 && int.TryParse(args[0], out var each) => Deal(state, each),
            "dealspread" when onTable && Ints(args, 2, out var ds) => DealOntoSpread(state, ds[0], ds[1]),
            "collect" when onTable => Collect(state),
            "reveal" when onTable => Toggle(state),
            "win" when onTable && args.Length >= 1 && int.TryParse(args[0], out var winner) => Win(state, winner),

            "draw" when myTurn => Draw(state, me),
            "play" when myTurn && Ints(args, 2, out var a) => Play(state, me, a[0], a[1] == 1),
            "lay" when myTurn && Ints(args, 3, out var lay) => LayCard(state, me, lay[0], lay[1], lay[2] == 1),
            "place" when myTurn && Ints(args, 3, out var pc) => PlaceOntoSpread(state, me, pc[0], pc[1], pc[2] == 1),
            "discard" when myTurn && Ints(args, 1, out var d) => Discard(state, me, d[0]),
            "give" when myTurn && Ints(args, 2, out var g) => Give(state, me, g[0], g[1]),
            "grab" when myTurn && Ints(args, 1, out var gr) => Grab(state, me, gr[0]),
            "pickup" when myTurn && Ints(args, 2, out var pu) => Pickup(state, me, pu[0], pu[1]),
            "takediscard" when myTurn => TakeDiscard(state, me),
            "flip" when seated && Ints(args, 2, out var f) && (f[0] == me || dealer) => Flip(state, f[0], f[1]),
            "bet" when myTurn && Ints(args, 2, out var b) => Bet(state, me, b[0], b[1]),
            "buyin" when seated && Ints(args, 2, out var bi) => Chip(state.seats[me].chips, bi[0], bi[1]),
            "cashout" when seated && Ints(args, 2, out var co) => Chip(state.seats[me].chips, co[0], -co[1]),

            "stack" when dealer => StartingStacks(state),
            "mode" when dealer && args.Length >= 1 && (args[0] == TableState.Free || args[0] == TableState.Turns) => SetMode(state, args[0]),
            "lock" when args.Length >= 1 && (args[0] == "1" ? dealer : visor) => SetLock(state, args[0] == "1"),
            "next" when seated && (dealer || state.turn == me) => NextTurn(state),
            _ => false
        };

        if (!changed)
            return false;
        // A visor wearer who sets the table up, while nobody else holds the deal, becomes the dealer too.
        if (claimOnSuccess || (visor && state.dealer == 0 && CanClaim(p[0])))
            Claim(state, player, SeatName(state, player));
        return true;
    }

    /// <summary>Commands that count as using the table, so the first one of them claims the deal when it is free.</summary>
    private static bool CanClaim(string command) => command is "seats" or "placedeck" or "takedeck" or "shuffle" or "deal"
        or "dealspread" or "collect" or "reveal" or "win" or "stack" or "mode" or "lock";

    private static bool Toggle(TableState state)
    {
        state.reveal = !state.reveal;
        return true;
    }

    private static bool SetMode(TableState state, string mode)
    {
        if (state.mode == mode)
            return false;
        state.mode = mode;
        return true;
    }

    private static bool SetLock(TableState state, bool want)
    {
        if (state.locked == want)
            return false;
        state.locked = want;
        return true;
    }

    private static string SeatName(TableState state, long player)
    {
        int seat = state.SeatOf(player);
        return seat >= 0 ? state.seats[seat].name : "";
    }

    private static bool Ints(string[] args, int count, out int[] values)
    {
        values = new int[count];
        if (args.Length < count)
            return false;
        for (int i = 0; i < count; i++)
        {
            if (!int.TryParse(args[i], out values[i]))
                return false;
        }

        return true;
    }

    // ---- seats

    private static bool Sit(TableState state, long player, string name, int seat)
    {
        if (player == 0 || seat < 0 || seat >= state.seatCount)
            return false;
        var target = state.seats[seat];
        if (target.Taken && target.player != player)
            return false;

        int current = state.SeatOf(player);
        if (current == seat)
            return false;
        if (current >= 0)
            MoveSeat(state.seats[current], target);

        state.seats[seat].player = player;
        state.seats[seat].name = name;
        Claim(state, player, name);
        return true;
    }

    /// <summary>
    /// Take the deal. Does nothing while a different player is already sitting as dealer.
    /// A dealer who is not seated (they only pressed E) loses it to the next person who uses the table.
    /// </summary>
    private static void Claim(TableState state, long player, string name)
    {
        if (player == 0)
            return;
        if (state.dealer != 0 && state.dealer != player && state.DealerIsSeated())
            return;
        if (state.dealer == player && state.DealerIsSeated())
            return;
        state.dealer = player;
        if (!string.IsNullOrEmpty(name))
            state.dealerName = name;
    }

    private static bool Touch(TableState state, long player, string name)
    {
        if (player == 0)
            return false;
        if (state.dealer == player)
            return false;
        if (state.DealerIsSeated())
            return false;
        Claim(state, player, name);
        return state.dealer == player;
    }
    /// <summary>Moving to another seat takes your cards and chips with you. The deal stays with the player.</summary>
    private static void MoveSeat(SeatState from, SeatState to)
    {
        to.hand.AddRange(from.hand);
        to.up.AddRange(from.up);
        for (int i = 0; i < Chips.Kinds; i++)
            to.chips[i] += from.chips[i];
        from.hand.Clear();
        from.up.Clear();
        from.chips = new int[Chips.Kinds];
        from.player = 0;
        from.name = "";
    }

    private static bool Leave(TableState state, long player)
    {
        int seat = state.SeatOf(player);
        if (seat < 0)
            return false;
        Vacate(state, state.seats[seat]);
        return true;
    }

    /// <summary>A seat empties: its cards go under the deck and its chips into the pot, so nothing is lost. If this was the dealer, the deal is open again.</summary>
    private static void Vacate(TableState state, SeatState seat)
    {
        if (state.dealer != 0 && state.dealer == seat.player)
        {
            state.dealer = 0;
            state.dealerName = "";
        }

        state.deck.AddRange(seat.hand);
        for (int i = 0; i < Chips.Kinds; i++)
            state.pot[i] += seat.chips[i];
        seat.hand.Clear();
        seat.up.Clear();
        seat.chips = new int[Chips.Kinds];
        seat.player = 0;
        seat.name = "";
    }

    private static bool SetSeats(TableState state, int count)
    {
        count = Mathf.Clamp(count, TableState.MinSeats, TableState.MaxSeats);
        if (count == state.seatCount)
            return false;
        for (int i = count; i < TableState.MaxSeats; i++)
        {
            var s = state.seats[i];
            if (s.Taken || s.hand.Count > 0 || s.chips.Any(c => c > 0))
                Vacate(state, s);
        }

        state.seatCount = count;
        return true;
    }

    // ---- the deck

    private static bool PlaceDeck(TableState state, int back, string cards)
    {
        if (state.deck.Count > 0)
            return false;
        var list = cards.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        if (list.Count == 0)
            return false;
        state.deck = list;
        state.deckBack = Util.CardBacks.Clamp(back);
        return true;
    }

    private static bool TakeDeck(TableState state)
    {
        if (state.deck.Count == 0)
            return false;
        state.deck.Clear();
        return true;
    }

    private static bool Shuffle(TableState state)
    {
        if (state.deck.Count < 2)
            return false;
        for (int i = state.deck.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (state.deck[i], state.deck[j]) = (state.deck[j], state.deck[i]);
        }

        return true;
    }

    private static bool Deal(TableState state, int each)
    {
        each = Mathf.Clamp(each, 1, 12);
        bool any = false;
        for (int round = 0; round < each; round++)
        {
            for (int i = 0; i < state.seatCount; i++)
            {
                if (!state.seats[i].Taken || state.deck.Count == 0)
                    continue;
                AddToHand(state.seats[i], TakeTop(state.deck), false);
                any = true;
            }
        }

        return any;
    }

    /// <summary>Everything goes back into the deck: hands, the felt and the discard pile (the chips stay where they are).</summary>
    private static bool Collect(TableState state)
    {
        int before = state.deck.Count;
        foreach (var seat in state.seats)
        {
            state.deck.AddRange(seat.hand);
            seat.hand.Clear();
            seat.up.Clear();
        }

        state.deck.AddRange(state.played);
        state.played.Clear();
        state.playedUp.Clear();
        state.playedBy.Clear();
        state.playedSpread.Clear();
        state.deck.AddRange(state.discard);
        state.discard.Clear();
        state.reveal = false;
        return state.deck.Count != before;
    }

    private static int TakeTop(List<int> pile)
    {
        int card = pile[0];
        pile.RemoveAt(0);
        return card;
    }

    private static void AddToHand(SeatState seat, int card, bool up)
    {
        seat.hand.Add(card);
        seat.up.Add(up);
    }

    // ---- cards in play

    private static bool Draw(TableState state, int seat)
    {
        if (state.deck.Count == 0)
            return false;
        AddToHand(state.seats[seat], TakeTop(state.deck), false);
        return true;
    }

    private static bool Valid(SeatState seat, int index) => index >= 0 && index < seat.hand.Count;

    private static int RemoveFromHand(SeatState seat, int index, out bool up)
    {
        int card = seat.hand[index];
        up = seat.up[index];
        seat.hand.RemoveAt(index);
        seat.up.RemoveAt(index);
        return card;
    }

    private static bool Play(TableState state, int seat, int index, bool faceUp) =>
        LayCard(state, seat, index, 0, faceUp);

    /// <summary>The next free spread number (0 when the felt is empty).</summary>
    private static int NextSpread(TableState state)
    {
        int max = -1;
        for (int i = 0; i < state.playedSpread.Count; i++)
        {
            if (state.playedSpread[i] > max)
                max = state.playedSpread[i];
        }

        return max + 1;
    }

    /// <summary>-1 starts a new spread, any other negative number means the latest one, and 0, 1, 2... pick a row.</summary>
    private static bool TrySpread(TableState state, int requested, out int spread)
    {
        int next = NextSpread(state);
        if (requested == -1)
            spread = next;
        else if (requested < 0)
            spread = next == 0 ? 0 : next - 1;
        else
            spread = requested;
        return spread >= 0 && spread <= next;
    }

    private static void AddPlayed(TableState state, int card, bool faceUp, int by, int spread)
    {
        state.played.Add(card);
        state.playedUp.Add(faceUp);
        state.playedBy.Add(by);
        state.playedSpread.Add(spread);
    }

    private static void RemovePlayed(TableState state, int index)
    {
        state.played.RemoveAt(index);
        state.playedUp.RemoveAt(index);
        state.playedBy.RemoveAt(index);
        if (index < state.playedSpread.Count)
            state.playedSpread.RemoveAt(index);
    }

    private static bool LayCard(TableState state, int seat, int index, int spread, bool faceUp)
    {
        var s = state.seats[seat];
        if (!Valid(s, index) || !TrySpread(state, spread, out var row))
            return false;
        AddPlayed(state, RemoveFromHand(s, index, out _), faceUp, seat, row);
        return true;
    }

    private static bool PlaceOntoSpread(TableState state, int seat, int card, int spread, bool faceUp)
    {
        if (card < 0 || card >= Util.CardLib.All.Length || !TrySpread(state, spread, out var row))
            return false;
        AddPlayed(state, card, faceUp, seat, row);
        return true;
    }

    private static bool DealOntoSpread(TableState state, int count, int spread)
    {
        count = Mathf.Clamp(count, 1, 5);
        if (state.deck.Count == 0 || !TrySpread(state, spread, out var row))
            return false;
        // A new row stays one row: every card dealt together lands on the spread chosen up front.
        bool any = false;
        for (int i = 0; i < count && state.deck.Count > 0; i++)
        {
            AddPlayed(state, TakeTop(state.deck), true, -1, row);
            any = true;
        }

        return any;
    }

    private static bool Discard(TableState state, int seat, int index)
    {
        var s = state.seats[seat];
        if (!Valid(s, index))
            return false;
        state.discard.Insert(0, RemoveFromHand(s, index, out _));
        return true;
    }

    private static bool Give(TableState state, int seat, int index, int toSeat)
    {
        var s = state.seats[seat];
        if (!Valid(s, index) || toSeat < 0 || toSeat >= state.seatCount || toSeat == seat || !state.seats[toSeat].Taken)
            return false;
        AddToHand(state.seats[toSeat], RemoveFromHand(s, index, out _), false);
        return true;
    }

    private static bool Grab(TableState state, int seat, int playedIndex)
    {
        if (playedIndex < 0 || playedIndex >= state.played.Count)
            return false;
        AddToHand(state.seats[seat], state.played[playedIndex], false);
        RemovePlayed(state, playedIndex);
        return true;
    }

    private static bool Pickup(TableState state, int seat, int spread, int indexInSpread)
    {
        if (indexInSpread < 0)
            return false;
        int seen = -1;
        for (int i = 0; i < state.played.Count; i++)
        {
            int row = i < state.playedSpread.Count ? state.playedSpread[i] : 0;
            if (row != spread)
                continue;
            seen++;
            if (seen != indexInSpread)
                continue;
            AddToHand(state.seats[seat], state.played[i], false);
            RemovePlayed(state, i);
            return true;
        }

        return false;
    }

    private static bool TakeDiscard(TableState state, int seat)
    {
        if (state.discard.Count == 0)
            return false;
        AddToHand(state.seats[seat], TakeTop(state.discard), false);
        return true;
    }

    private static bool Flip(TableState state, int seat, int index)
    {
        if (seat < 0 || seat >= TableState.MaxSeats || !Valid(state.seats[seat], index))
            return false;
        state.seats[seat].up[index] = !state.seats[seat].up[index];
        return true;
    }

    private static bool NextTurn(TableState state)
    {
        for (int step = 1; step <= TableState.MaxSeats; step++)
        {
            int next = (state.turn + step) % state.seatCount;
            if (state.seats[next].Taken)
            {
                state.turn = next;
                return true;
            }
        }

        return false;
    }

    // ---- chips

    /// <summary>What each player gets from the dealer: 20 white, 10 red, 4 green, 1 black (worth 20 + 50 + 100 + 100 = 270).</summary>
    internal static readonly int[] StartingStack = { 20, 10, 4, 1 };

    private static bool StartingStacks(TableState state)
    {
        bool any = false;
        for (int i = 0; i < state.seatCount; i++)
        {
            if (!state.seats[i].Taken)
                continue;
            for (int k = 0; k < Chips.Kinds; k++)
                state.seats[i].chips[k] += StartingStack[k];
            any = true;
        }

        return any;
    }

    private static bool Chip(int[] stack, int kind, int delta)
    {
        if (kind < 0 || kind >= Chips.Kinds || delta == 0 || stack[kind] + delta < 0)
            return false;
        stack[kind] += delta;
        return true;
    }

    private static bool Bet(TableState state, int seat, int kind, int count)
    {
        var stack = state.seats[seat].chips;
        if (kind < 0 || kind >= Chips.Kinds || count <= 0 || stack[kind] < count)
            return false;
        stack[kind] -= count;
        state.pot[kind] += count;
        return true;
    }

    private static bool Win(TableState state, int seat)
    {
        if (seat < 0 || seat >= state.seatCount || !state.seats[seat].Taken || state.pot.All(c => c == 0))
            return false;
        for (int i = 0; i < Chips.Kinds; i++)
        {
            state.seats[seat].chips[i] += state.pot[i];
            state.pot[i] = 0;
        }

        return true;
    }
}
