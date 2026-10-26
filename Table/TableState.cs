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

    /// <summary>How many seats are in use (the rest are switched off by the dealer).</summary>
    public int seatCount = 4;

    public List<SeatState> seats = new List<SeatState>();

    /// <summary>Cards left in the deck on the table, top first.</summary>
    public List<int> deck = new List<int>();

    /// <summary>The discard pile, newest first.</summary>
    public List<int> discard = new List<int>();

    /// <summary>Back design of the deck on the table (see CardBacks).</summary>
    public int deckBack = Util.CardBacks.DefaultBack;

    /// <summary>"free" (no limits), "turns" (play in seat order) or the name of a preset game.</summary>
    public string mode = "free";

    /// <summary>After this, only the dealer can change the rules.</summary>
    public bool locked;

    /// <summary>Seat whose turn it is (turn mode).</summary>
    public int turn;

    private const string Header = "BV1";

    // The saved text: BV1|seats|mode|locked|turn|deckBack|deck|discard|seat0|...|seat7
    //   lists are comma separated card numbers; a seat is  player ~ name ~ hand  with a hand like  u12,d40  (u = face up).
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
        if (p.Length < 8 + 1 || p[0] != Header)
            return;
        seatCount = int.Parse(p[1]);
        mode = Uri.UnescapeDataString(p[2]);
        locked = p[3] == "1";
        turn = int.Parse(p[4]);
        deckBack = int.Parse(p[5]);
        deck = ReadList(p[6]);
        discard = ReadList(p[7]);
        seats = new List<SeatState>();
        for (int i = 8; i < p.Length && seats.Count < MaxSeats; i++)
        {
            var f = p[i].Split('~');
            var seat = new SeatState();
            if (f.Length >= 3)
            {
                seat.player = long.Parse(f[0]);
                seat.name = Uri.UnescapeDataString(f[1]);
                foreach (var card in f[2].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    seat.up.Add(card[0] == 'u');
                    seat.hand.Add(int.Parse(card.Substring(1)));
                }
            }

            seats.Add(seat);
        }
    }

    private static List<int> ReadList(string text) =>
        new List<int>(Array.ConvertAll(text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), int.Parse));

    internal string Serialize()
    {
        Normalize();
        var sb = new System.Text.StringBuilder(Header);
        sb.Append('|').Append(seatCount)
            .Append('|').Append(Uri.EscapeDataString(mode))
            .Append('|').Append(locked ? '1' : '0')
            .Append('|').Append(turn)
            .Append('|').Append(deckBack)
            .Append('|').Append(string.Join(",", deck))
            .Append('|').Append(string.Join(",", discard));
        foreach (var seat in seats)
        {
            sb.Append('|').Append(seat.player).Append('~').Append(Uri.EscapeDataString(seat.name).Replace("~", "%7E")).Append('~');
            for (int i = 0; i < seat.hand.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(seat.up[i] ? 'u' : 'd').Append(seat.hand[i]);
            }
        }

        return sb.ToString();
    }

    /// <summary>Always eight seat slots, a sensible seat count, and matching hand lists.</summary>
    internal void Normalize()
    {
        seatCount = Mathf.Clamp(seatCount, MinSeats, MaxSeats);
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
        }
    }

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
}
