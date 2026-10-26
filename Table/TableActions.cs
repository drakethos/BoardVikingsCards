using System;
using UnityEngine;

namespace Cards.Table;

/// <summary>
/// Everything a player or the dealer can do at the table, as short text commands ("sit|id|name|seat"). The table's owner runs
/// them against the saved state, so every client sees the same table. Free play checks nothing except who may change the rules.
/// </summary>
internal static class TableActions
{
    internal static string Sit(long player, string name, int seat) => $"sit|{player}|{Clean(name)}|{seat}";

    internal static string Leave(long player) => $"leave|{player}";

    internal static string SetSeats(long player, int seats) => $"seats|{player}|{seats}";

    private static string Clean(string text) => (text ?? "").Replace("|", " ");

    /// <summary>Apply one command to the state. Returns true when the state changed.</summary>
    internal static bool Apply(TableState state, string action, bool senderIsDealer)
    {
        if (string.IsNullOrEmpty(action))
            return false;
        var p = action.Split('|');
        switch (p[0])
        {
            case "sit" when p.Length >= 4 && long.TryParse(p[1], out var who) && int.TryParse(p[3], out var seat):
                return Sit(state, who, p[2], seat);

            case "leave" when p.Length >= 2 && long.TryParse(p[1], out var leaver):
                return Leave(state, leaver);

            case "seats" when p.Length >= 3 && int.TryParse(p[2], out var count):
                if (state.locked && !senderIsDealer)
                    return false;
                if (!senderIsDealer)
                    return false;
                return SetSeats(state, count);
        }

        return false;
    }

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
            MoveHand(state.seats[current], target);

        state.seats[seat].player = player;
        state.seats[seat].name = name;
        if (current >= 0)
        {
            state.seats[current].player = 0;
            state.seats[current].name = "";
        }

        return true;
    }

    /// <summary>Moving to another seat takes your cards with you.</summary>
    private static void MoveHand(SeatState from, SeatState to)
    {
        to.hand.AddRange(from.hand);
        to.up.AddRange(from.up);
        from.hand.Clear();
        from.up.Clear();
    }

    private static bool Leave(TableState state, long player)
    {
        int seat = state.SeatOf(player);
        if (seat < 0)
            return false;

        // Cards in hand go back under the deck so nothing is lost when someone walks away.
        var s = state.seats[seat];
        state.deck.AddRange(s.hand);
        s.hand.Clear();
        s.up.Clear();
        s.player = 0;
        s.name = "";
        return true;
    }

    private static bool SetSeats(TableState state, int count)
    {
        count = Mathf.Clamp(count, TableState.MinSeats, TableState.MaxSeats);
        if (count == state.seatCount)
            return false;

        // Seats that switch off send their player away (and their cards back to the deck).
        for (int i = count; i < TableState.MaxSeats; i++)
        {
            var s = state.seats[i];
            if (!s.Taken && s.hand.Count == 0)
                continue;
            state.deck.AddRange(s.hand);
            s.hand.Clear();
            s.up.Clear();
            s.player = 0;
            s.name = "";
        }

        state.seatCount = count;
        return true;
    }
}
