using System.Collections.Generic;
using System.Linq;
using Cards.Deck;
using Cards.Util;
using UnityEngine;

namespace Cards.Table;

/// <summary>Entry points for TestLab "invoke" steps, acting on the card table nearest the player as the local player.</summary>
public static class TableTestApi
{
    private static CardTable? Nearest()
    {
        var player = Player.m_localPlayer;
        if (player == null)
            return null;
        return Object.FindObjectsOfType<CardTable>()
            .OrderBy(t => (t.transform.position - player.transform.position).sqrMagnitude)
            .FirstOrDefault();
    }

    private static long Me => CardTable.MyId;

    private static bool Visor => CardTable.MyVisor;

    private static T Read<T>(System.Func<CardTable, T> read, string? action, T fallback)
    {
        var table = Nearest();
        if (table == null)
            return fallback;
        if (action != null)
            table.Act(action);
        table.Refresh();
        return read(table);
    }

    private static SeatState? MySeat(CardTable t)
    {
        int seat = t.State.SeatOf(Me);
        return seat >= 0 ? t.State.seats[seat] : null;
    }

    // ---- seats

    public static int TestSeats() => Read(t => t.State.seatCount, null, -1);

    public static int TestTaken() => Read(t => t.State.Taken(), null, -1);

    public static int TestSetSeats(int seats) => Read(t => t.State.seatCount, TableActions.SetSeats(Me, Visor, seats), -1);

    /// <summary>Sit in a seat (1 to 8); returns the seat I am in afterwards (1 to 8), or 0 when not seated.</summary>
    public static int TestSit(int seat) => Read(t => t.State.SeatOf(Me) + 1, TableActions.Sit(Me, Visor, CardTable.MyName, seat - 1), -1);

    public static int TestSeatOf() => Read(t => t.State.SeatOf(Me) + 1, null, -1);

    public static int TestLeave() => Read(t => t.State.SeatOf(Me) + 1, TableActions.Leave(Me, Visor), -1);

    public static string TestSeatName(int seat) => Read(t => t.State.seats[seat - 1].name, null, "");

    public static string TestSize() => Read(t => $"{t.Size.x:0.0},{t.Size.y:0.0}", null, "");

    // ---- the deck and the cards (as the local player, through the real actions)

    /// <summary>Put a deck from my inventory on the table; returns the cards on the table (0 when it did not work).</summary>
    public static int TestPlaceDeck()
    {
        var table = Nearest();
        if (table == null)
            return -1;
        table.PlaceDeckFromInventory();
        table.Refresh();
        return table.State.deck.Count;
    }

    /// <summary>Pick the deck up into my inventory; returns the cards in the deck item I now hold (-1: none).</summary>
    public static int TestTakeDeck()
    {
        var table = Nearest();
        if (table == null)
            return -1;
        table.TakeDeckToInventory();
        table.Refresh();
        return DeckApi.TestCount();
    }

    public static int TestDeckCount() => Read(t => t.State.deck.Count, null, -1);

    public static int TestDiscardCount() => Read(t => t.State.discard.Count, null, -1);

    public static int TestPlayedCount() => Read(t => t.State.played.Count, null, -1);

    public static int TestShuffle() => Read(t => t.State.deck.Count, TableActions.Shuffle(Me, Visor), -1);

    public static int TestDeal(int each) => Read(t => t.State.deck.Count, TableActions.Deal(Me, Visor, each), -1);

    public static int TestHand() => Read(t => MySeat(t)?.hand.Count ?? -1, null, -1);

    /// <summary>My hand as card codes.</summary>
    public static string TestHandFaces() => Read(t => string.Join(",", (MySeat(t)?.hand ?? new List<int>()).Select(CardLib.CodeOf)), null, "");

    public static int TestDraw() => Read(t => MySeat(t)?.hand.Count ?? -1, TableActions.Draw(Me, Visor), -1);

    public static int TestPlay(int handIndex, bool faceUp) => Read(t => t.State.played.Count, TableActions.Play(Me, Visor, handIndex, faceUp), -1);

    public static int TestDiscard(int handIndex) => Read(t => t.State.discard.Count, TableActions.Discard(Me, Visor, handIndex), -1);

    public static bool TestFlip(int handIndex) =>
        Read(t => MySeat(t) is { } s && handIndex < s.up.Count && s.up[handIndex], TableActions.Flip(Me, Visor, Nearest()?.State.SeatOf(Me) ?? 0, handIndex), false);

    public static int TestCollect() => Read(t => t.State.deck.Count, TableActions.Collect(Me, Visor), -1);

    /// <summary>Total cards anywhere on the table (a check that dealing and collecting never lose or add one).</summary>
    public static int TestCardTotal() => Read(t => t.State.CardTotal(), null, -1);

    public static int TestGrab(int playedIndex) => Read(t => MySeat(t)?.hand.Count ?? -1, TableActions.Grab(Me, Visor, playedIndex), -1);

    // ---- dealer, rules, chips

    /// <summary>Wear the dealer's visor (gives me one first when I have none).</summary>
    public static bool TestWearVisor()
    {
        var player = Player.m_localPlayer;
        if (player == null)
            return false;
        var existing = player.GetInventory().GetAllItems().FirstOrDefault(i => i.m_dropPrefab != null && i.m_dropPrefab.name == DealerVisor.Prefab);
        if (existing == null)
        {
            var prefab = ObjectDB.instance.GetItemPrefab(DealerVisor.Prefab);
            var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null)
                return false;
            existing = drop.m_itemData.Clone();
            existing.m_dropPrefab = prefab;
            existing.m_stack = 1;
            existing.m_quality = 1;
            player.GetInventory().AddItem(existing);
        }

        player.EquipItem(existing, true);
        return DealerVisor.IsWorn(player);
    }

    public static bool TestTakeVisorOff()
    {
        var player = Player.m_localPlayer;
        var worn = player?.GetInventory().GetAllItems().FirstOrDefault(i => i.m_dropPrefab != null && i.m_dropPrefab.name == DealerVisor.Prefab);
        if (player == null || worn == null)
            return false;
        player.UnequipItem(worn, true);
        return !DealerVisor.IsWorn(player);
    }

    /// <summary>
    /// Swing the camera round to look at the player (offset 180 = from the front, 90 = from the side) so a picture can show the face
    /// and what is worn on it. Returns false when there is no player.
    /// </summary>
    public static bool TestCameraAround(float degrees)
    {
        var player = Player.m_localPlayer;
        if (player == null)
            return false;
        var yaw = HarmonyLib.AccessTools.Field(typeof(Player), "m_lookYaw");
        var pitch = HarmonyLib.AccessTools.Field(typeof(Player), "m_lookPitch");
        yaw?.SetValue(player, Quaternion.Euler(0f, player.transform.eulerAngles.y + degrees, 0f));
        pitch?.SetValue(player, 4f);
        return yaw != null;
    }

    public static bool TestIsDealer() => Read(t => t.IsDealerMe, null, false);

    public static string TestMode() => Read(t => t.State.mode + (t.State.locked ? "+locked" : ""), null, "");

    public static string TestSetMode(string mode) => Read(t => t.State.mode, TableActions.SetMode(Me, Visor, mode), "");

    public static string TestLock(bool locked) => Read(t => t.State.mode + (t.State.locked ? "+locked" : ""), TableActions.Lock(Me, Visor, locked), "");

    public static int TestStack() => Read(t => MySeat(t) is { } s ? Chips.Value(s.chips) : -1, TableActions.Stack(Me, Visor), -1);

    public static int TestChips() => Read(t => MySeat(t) is { } s ? Chips.Value(s.chips) : -1, null, -1);

    public static int TestBet(int kind, int count) => Read(t => Chips.Value(t.State.pot), TableActions.Bet(Me, Visor, kind, count), -1);

    public static int TestWin() => Read(t => Chips.Value(t.State.pot), TableActions.Win(Me, Visor, Nearest()?.State.SeatOf(Me) ?? 0), -1);

    // ---- looking

    public static string TestTableInfo()
    {
        var table = Nearest();
        if (table == null)
            return "no table";
        var renderers = table.GetComponentsInChildren<Renderer>(true);
        return $"renderers={renderers.Length}";
    }

    public static float TestTableLift()
    {
        var table = Nearest();
        if (table == null || ZoneSystem.instance == null)
            return -99f;
        var top = table.transform.position.y + table.TopHeight;
        return Mathf.Round((top - ZoneSystem.instance.GetGroundHeight(table.transform.position)) * 100f) / 100f;
    }

    public static bool TestOpenPanel()
    {
        var table = Nearest();
        if (table == null)
            return false;
        TablePanel.Open(table);
        return true;
    }

    /// <summary>How many card objects the table is showing right now (deck layers, discard, felt, hands).</summary>
    public static int TestCardObjects()
    {
        var table = Nearest();
        return table == null ? -1 : table.GetComponentsInChildren<Transform>().Count(t => t.name == "table_card" && t.gameObject.activeSelf);
    }

    // ---- the rules on a bare state (no table piece): quick, exact checks of the logic. Player 100 built the table.

    private const long Creator = 100;

    private static bool Do(TableState state, string action) => TableActions.Apply(state, action, Creator);

    private static TableState Seated(int players)
    {
        var state = TableState.Parse("");
        for (int i = 0; i < players; i++)
            Do(state, TableActions.Sit(100 + i, false, "P" + (i + 1), i));
        return state;
    }

    private static void FullDeck(TableState state) => Do(state, TableActions.PlaceDeck(100, false, 3, Enumerable.Range(0, 52)));

    private static string Hands(TableState s) => string.Join(",", s.seats.Take(4).Select(x => x.hand.Count));

    /// <summary>Runs a named scenario against a bare table state and returns what happened, for exact comparison.</summary>
    public static string TestScenario(string name)
    {
        var s = Seated(4);
        switch (name)
        {
            case "deal":
                FullDeck(s);
                Do(s, TableActions.Deal(100, false, 5));
                return $"hands={Hands(s)} deck={s.deck.Count} total={s.CardTotal()}";

            case "deal-short":
                Do(s, TableActions.PlaceDeck(100, false, 0, new[] { 0, 1, 2, 3, 4 }));
                Do(s, TableActions.Deal(100, false, 3));
                return $"hands={Hands(s)} deck={s.deck.Count}";

            case "play":
                FullDeck(s);
                Do(s, TableActions.Deal(100, false, 3));
                Do(s, TableActions.Play(100, false, 0, true));
                Do(s, TableActions.Play(101, false, 1, false));
                Do(s, TableActions.Discard(102, false, 0));
                Do(s, TableActions.Draw(103, false));
                Do(s, TableActions.Give(100, false, 0, 1));
                return $"hands={Hands(s)} felt={s.played.Count} discard={s.discard.Count} deck={s.deck.Count} " +
                       $"up={string.Join("", s.playedUp.Select(u => u ? "u" : "d"))} total={s.CardTotal()}";

            case "flip":
            {
                FullDeck(s);
                Do(s, TableActions.Deal(100, false, 1));
                bool own = Do(s, TableActions.Flip(101, false, 1, 0));
                bool other = Do(s, TableActions.Flip(101, false, 2, 0));
                bool creator = Do(s, TableActions.Flip(100, false, 2, 0));
                bool visor = Do(s, TableActions.Flip(103, true, 1, 0));
                return $"own={own} other={other} creator={creator} visor={visor}";
            }

            case "turns":
            {
                FullDeck(s);
                Do(s, TableActions.SetMode(100, false, TableState.Turns));
                bool wrong = Do(s, TableActions.Draw(101, false));
                bool mine = Do(s, TableActions.Draw(100, false));
                Do(s, TableActions.NextTurn(100, false));
                bool next = Do(s, TableActions.Draw(101, false));
                return $"wrongTurn={wrong} myTurn={mine} afterPass={next} turn={s.turn}";
            }

            case "lock":
            {
                bool before = Do(s, TableActions.SetMode(100, false, TableState.Turns));
                Do(s, TableActions.SetMode(100, false, TableState.Free));
                bool lockIt = Do(s, TableActions.Lock(100, false, true));
                bool creator = Do(s, TableActions.SetMode(100, false, TableState.Turns));
                bool player = Do(s, TableActions.SetSeats(102, false, 6));
                bool unlockNoVisor = Do(s, TableActions.Lock(100, false, false));
                bool visor = Do(s, TableActions.SetMode(102, true, TableState.Turns));
                bool unlock = Do(s, TableActions.Lock(102, true, false));
                return $"creatorBefore={before} locked={lockIt} creatorAfterLock={creator} playerSeats={player} " +
                       $"unlockWithoutVisor={unlockNoVisor} visorChanges={visor} visorUnlocks={unlock} mode={s.mode} locked={s.locked}";
            }

            case "locked-play":
            {
                FullDeck(s);
                Do(s, TableActions.Lock(100, true, true));
                bool shuffle = Do(s, TableActions.Shuffle(101, false));
                bool dealerShuffle = Do(s, TableActions.Shuffle(101, true));
                bool draw = Do(s, TableActions.Draw(101, false));
                return $"playerShuffle={shuffle} visorShuffle={dealerShuffle} playerDraws={draw}";
            }

            case "no-creator":
            {
                var bare = TableState.Parse("");
                TableActions.Apply(bare, TableActions.Sit(5, false, "A", 0), 0);
                bool noVisor = TableActions.Apply(bare, TableActions.SetSeats(5, false, 6), 0);
                bool visor = TableActions.Apply(bare, TableActions.SetSeats(5, true, 6), 0);
                return $"withoutVisor={noVisor} withVisor={visor}";
            }

            case "leave":
                FullDeck(s);
                Do(s, TableActions.Deal(100, false, 5));
                Do(s, TableActions.Leave(101, false));
                return $"taken={s.Taken()} deck={s.deck.Count} total={s.CardTotal()}";

            case "collect":
                FullDeck(s);
                Do(s, TableActions.Deal(100, false, 5));
                Do(s, TableActions.Play(100, false, 0, true));
                Do(s, TableActions.Discard(101, false, 0));
                Do(s, TableActions.Collect(100, false));
                return $"deck={s.deck.Count} hands={s.seats.Sum(x => x.hand.Count)} felt={s.played.Count} discard={s.discard.Count}";

            case "shuffle":
            {
                FullDeck(s);
                var before = string.Join(",", s.deck);
                Do(s, TableActions.Shuffle(100, false));
                return $"changed={before != string.Join(",", s.deck)} total={s.deck.Count} distinct={s.deck.Distinct().Count()}";
            }

            case "chips":
            {
                Do(s, TableActions.Stack(100, false));
                int start = Chips.Value(s.seats[0].chips);
                bool bet = Do(s, TableActions.Bet(100, false, 1, 2));
                bool tooMuch = Do(s, TableActions.Bet(100, false, 3, 5));
                Do(s, TableActions.Bet(101, false, 0, 10));
                int pot = Chips.Value(s.pot);
                Do(s, TableActions.Win(100, false, 1));
                return $"start={start} bet={bet} tooMuch={tooMuch} pot={pot} afterWin={Chips.Value(s.pot)} winner={Chips.Value(s.seats[1].chips)}";
            }

            case "saved":
            {
                FullDeck(s);
                Do(s, TableActions.Deal(100, false, 2));
                Do(s, TableActions.Play(100, false, 0, false));
                Do(s, TableActions.Stack(100, false));
                Do(s, TableActions.Bet(100, false, 0, 3));
                Do(s, TableActions.Lock(100, true, true));
                var back = TableState.Parse(s.Serialize());
                return $"same={back.Serialize() == s.Serialize()} total={back.CardTotal()} chips={Chips.Value(back.seats[0].chips)} pot={Chips.Value(back.pot)} locked={back.locked}";
            }
        }

        return "unknown scenario";
    }
}
