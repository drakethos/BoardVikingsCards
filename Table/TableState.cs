using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cards.Table;

/// <summary>One seat at the table.</summary>
[Serializable]
internal sealed class SeatState
{
    /// <summary>Id of the player sitting here (0 = empty).</summary>
    public long player;

    public string name = "";

    /// <summary>The player's hand: card face indexes (see CardLib), in the order held.</summary>
    public List<int> hand = new List<int>();

    /// <summary>One flag per hand card: shown face up to everyone.</summary>
    public List<bool> up = new List<bool>();

    /// <summary>Chips in front of the player, per kind (see Chips).</summary>
    public int[] chips = new int[Chips.Kinds];

    public bool Taken => player != 0;
}

/// <summary>
/// Everything about one card table, saved as text in the table's object data. The table piece draws itself from this and every
/// action changes it, so what you see on the felt is always what is saved.
/// </summary>
[Serializable]
internal sealed class TableState
{
    internal const int MaxSeats = 8;
    internal const int MinSeats = 2;

    internal const string Free = "free";
    internal const string Turns = "turns";

    /// <summary>How many seats are in use (the rest are switched off by the dealer).</summary>
    public int seatCount = 4;

    public List<SeatState> seats = new List<SeatState>();

    /// <summary>Cards left in the deck on the table, top first.</summary>
    public List<int> deck = new List<int>();

    /// <summary>The discard pile, newest first.</summary>
    public List<int> discard = new List<int>();

    /// <summary>Cards laid in spreads: which card, face up or down, who laid it (seat number, -1 = the deck), and which spread.</summary>
    public List<int> played = new List<int>();

    public List<bool> playedUp = new List<bool>();
    public List<int> playedBy = new List<int>();

    /// <summary>Which spread each laid card belongs to. A spread is a row you can add to and pick up from (a poker board, a rummy meld).</summary>
    public List<int> playedSpread = new List<int>();

    /// <summary>Chips in the middle of the table, per kind.</summary>
    public int[] pot = new int[Chips.Kinds];

    /// <summary>Back design of the deck on the table (see CardBacks).</summary>
    public int deckBack = Util.CardBacks.DefaultBack;

    /// <summary>"free" (no limits) or "turns" (play in seat order).</summary>
    public string mode = Free;

    /// <summary>After this, only the dealer (the visor wearer) can change the rules.</summary>
    public bool locked;

    /// <summary>Seat whose turn it is (turn mode).</summary>
    public int turn;

    /// <summary>Every hand shown face up (the dealer's "show your cards").</summary>
    public bool reveal;

    /// <summary>Player id of the dealer (0 = nobody yet). The first player to use the table takes it, until they leave.</summary>
    public long dealer;

    /// <summary>The dealer's name, shown on the panel and the hover text.</summary>
    public string dealerName = "";

    private const string Header = "BV3";
    private const string HeaderOld = "BV2";

    // The saved text: BV3|seats|mode|locked|turn|deckBack|reveal|dealer|dealerName|pot|deck|discard|played|seat0|...|seat7
    //   BV2 is the older text (no dealer); it still loads, and the next save writes BV3.
    //   lists are comma separated card numbers; a played card is  u12@3s0  (u/d = face up/down, @seat who played it, s = spread);
    //   chips are comma separated counts; a seat is  player ~ name ~ hand ~ chips  with a hand like  u12,d40  (u = face up).
    // Written by hand because Unity's JSON helper silently drops lists of non-public types.

    internal static TableState Parse(string raw)
    {
        var state = new TableState();
        try
        {
            if (!string.IsNullOrEmpty(raw))
                state.Read(raw);
        }
        catch (Exception)
        {
            state = new TableState();
        }

        state.Normalize();
        return state;
    }

    private void Read(string raw)
    {
        var p = raw.Split('|');
        bool current = p.Length >= 13 && p[0] == Header;
        bool older = p.Length >= 11 && p[0] == HeaderOld;
        if (!current && !older)
            return;
        seatCount = int.Parse(p[1]);
        mode = Uri.UnescapeDataString(p[2]);
        locked = p[3] == "1";
        turn = int.Parse(p[4]);
        deckBack = int.Parse(p[5]);
        reveal = p[6] == "1";
        int potAt = 7;
        if (current)
        {
            dealer = long.Parse(p[7]);
            dealerName = Uri.UnescapeDataString(p[8]);
            potAt = 9;
        }

        pot = ReadChips(p[potAt]);
        deck = ReadList(p[potAt + 1]);
        discard = ReadList(p[potAt + 2]);
        ReadPlayed(p[potAt + 3]);

        seats = new List<SeatState>();
        for (int i = potAt + 4; i < p.Length && seats.Count < MaxSeats; i++)
        {
            var f = p[i].Split('~');
            var seat = new SeatState();
            if (f.Length >= 4)
            {
                seat.player = long.Parse(f[0]);
                seat.name = Uri.UnescapeDataString(f[1]);
                foreach (var card in f[2].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    seat.up.Add(card[0] == 'u');
                    seat.hand.Add(int.Parse(card.Substring(1)));
                }

                seat.chips = ReadChips(f[3]);
            }

            seats.Add(seat);
        }
    }

    private void ReadPlayed(string text)
    {
        foreach (var token in text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int spread = 0;
            var card = token;
            int at = card.IndexOf('@');
            if (at <= 0)
                continue;
            int mark = card.IndexOf('s', at + 1);
            if (mark > at)
            {
                int.TryParse(card.Substring(mark + 1), out spread);
                card = card.Substring(0, mark);
            }

            played.Add(int.Parse(card.Substring(1, at - 1)));
            playedUp.Add(card[0] == 'u');
            playedBy.Add(int.Parse(card.Substring(at + 1)));
            playedSpread.Add(spread);
        }
    }

    private static List<int> ReadList(string text) =>
        new List<int>(Array.ConvertAll(text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), int.Parse));

    private static int[] ReadChips(string text)
    {
        var counts = new int[Chips.Kinds];
        var parts = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length && i < counts.Length; i++)
            counts[i] = int.Parse(parts[i]);
        return counts;
    }

    internal string Serialize()
    {
        Normalize();
        var sb = new System.Text.StringBuilder(Header);
        sb.Append('|').Append(seatCount)
            .Append('|').Append(Uri.EscapeDataString(mode))
            .Append('|').Append(locked ? '1' : '0')
            .Append('|').Append(turn)
            .Append('|').Append(deckBack)
            .Append('|').Append(reveal ? '1' : '0')
            .Append('|').Append(dealer)
            .Append('|').Append(Uri.EscapeDataString(dealerName ?? ""))
            .Append('|').Append(string.Join(",", pot))
            .Append('|').Append(string.Join(",", deck))
            .Append('|').Append(string.Join(",", discard))
            .Append('|');
        for (int i = 0; i < played.Count; i++)
        {
            if (i > 0)
                sb.Append(',');
            int spread = i < playedSpread.Count ? playedSpread[i] : 0;
            sb.Append(playedUp[i] ? 'u' : 'd').Append(played[i]).Append('@').Append(playedBy[i]).Append('s').Append(spread);
        }

        foreach (var seat in seats)
        {
            sb.Append('|').Append(seat.player).Append('~').Append(Uri.EscapeDataString(seat.name).Replace("~", "%7E")).Append('~');
            for (int i = 0; i < seat.hand.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(seat.up[i] ? 'u' : 'd').Append(seat.hand[i]);
            }

            sb.Append('~').Append(string.Join(",", seat.chips));
        }

        return sb.ToString();
    }

    /// <summary>Always eight seat slots, a sensible seat count, and matching lists.</summary>
    internal void Normalize()
    {
        seatCount = Mathf.Clamp(seatCount, MinSeats, MaxSeats);
        if (mode != Free && mode != Turns)
            mode = Free;
        if (dealerName == null)
            dealerName = "";
        deckBack = Util.CardBacks.Clamp(deckBack);
        turn = Mathf.Clamp(turn, 0, MaxSeats - 1);
        if (pot.Length != Chips.Kinds)
            Array.Resize(ref pot, Chips.Kinds);
        while (seats.Count < MaxSeats)
            seats.Add(new SeatState());
        if (seats.Count > MaxSeats)
            seats.RemoveRange(MaxSeats, seats.Count - MaxSeats);
        foreach (var seat in seats)
        {
            while (seat.up.Count < seat.hand.Count)
                seat.up.Add(false);
            if (seat.up.Count > seat.hand.Count)
                seat.up.RemoveRange(seat.hand.Count, seat.up.Count - seat.hand.Count);
            if (seat.chips.Length != Chips.Kinds)
                Array.Resize(ref seat.chips, Chips.Kinds);
        }

        while (playedUp.Count < played.Count)
            playedUp.Add(true);
        while (playedBy.Count < played.Count)
            playedBy.Add(-1);
        if (playedUp.Count > played.Count)
            playedUp.RemoveRange(played.Count, playedUp.Count - played.Count);
        if (playedBy.Count > played.Count)
            playedBy.RemoveRange(played.Count, playedBy.Count - played.Count);
        while (playedSpread.Count < played.Count)
            playedSpread.Add(0);
        if (playedSpread.Count > played.Count)
            playedSpread.RemoveRange(played.Count, playedSpread.Count - played.Count);
    }

    /// <summary>The dealer is sitting in a seat that is switched on.</summary>
    internal bool DealerIsSeated()
    {
        if (dealer == 0)
            return false;
        int seat = SeatOf(dealer);
        return seat >= 0 && seat < seatCount;
    }

    /// <summary>This player may act as the dealer: they wear the visor, or they hold the deal and the rules are unlocked.</summary>
    internal bool PlayerIsDealer(long player, bool visor) =>
        player != 0 && (visor || (!locked && dealer == player));

    internal int SeatOf(long player)
    {
        for (int i = 0; i < MaxSeats; i++)
        {
            if (seats[i].player == player)
                return i;
        }

        return -1;
    }

    internal int Taken()
    {
        int n = 0;
        for (int i = 0; i < seatCount; i++)
        {
            if (seats[i].Taken)
                n++;
        }

        return n;
    }

    /// <summary>Every card on the table (deck, discard, spreads, hands), for checking none is lost or doubled.</summary>
    internal int CardTotal()
    {
        int n = deck.Count + discard.Count + played.Count;
        foreach (var seat in seats)
            n += seat.hand.Count;
        return n;
    }
}

/// <summary>Poker chips: kinds and their worth.</summary>
internal static class Chips
{
    /// <summary>Number of chip kinds.</summary>
    internal const int Kinds = 4;

    internal static readonly string[] Names = { "White", "Red", "Green", "Black" };
    internal static readonly int[] Worth = { 1, 5, 25, 100 };

    internal static int Value(int[] counts)
    {
        int total = 0;
        for (int i = 0; i < Kinds && i < counts.Length; i++)
            total += counts[i] * Worth[i];
        return total;
    }
}
