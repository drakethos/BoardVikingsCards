using System.Linq;
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

    private static long Me => Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0;

    private static string MyName => Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : "";

    private static T Act<T>(System.Func<CardTable, T> read, string? action, T fallback)
    {
        var table = Nearest();
        if (table == null)
            return fallback;
        if (action != null)
            table.Act(action);
        table.Refresh();
        return read(table);
    }

    /// <summary>How many seats the table uses.</summary>
    public static int TestSeats() => Act(t => t.State.seatCount, null, -1);

    /// <summary>How many of them have someone sitting.</summary>
    public static int TestTaken() => Act(t => t.State.Taken(), null, -1);

    /// <summary>Dealer sets the seat count; returns the count afterwards.</summary>
    public static int TestSetSeats(int seats) => Act(t => t.State.seatCount, TableActions.SetSeats(Me, seats), -1);

    /// <summary>Sit in a seat (1 to 8); returns the seat I am in afterwards (1 to 8), or 0 when not seated.</summary>
    public static int TestSit(int seat) => Act(t => t.State.SeatOf(Me) + 1, TableActions.Sit(Me, MyName, seat - 1), -1);

    public static int TestSeatOf() => Act(t => t.State.SeatOf(Me) + 1, null, -1);

    public static int TestLeave() => Act(t => t.State.SeatOf(Me) + 1, TableActions.Leave(Me), -1);

    /// <summary>Name shown in a seat ("" when empty).</summary>
    public static string TestSeatName(int seat) => Act(t => t.State.seats[seat - 1].name, null, "");

    /// <summary>Runs the actions on a bare table state (no table piece) and reports what happened, for finding faults.</summary>
    public static string TestActionsSelfCheck()
    {
        var state = TableState.Parse("");
        bool sat = TableActions.Apply(state, TableActions.Sit(1234, "A", 1), false);
        var again = TableState.Parse(state.Serialize());
        bool seats = TableActions.Apply(again, TableActions.SetSeats(1234, 6), true);
        return $"sat={sat} seat={state.SeatOf(1234)} roundtrip={again.SeatOf(1234)} seats={seats}/{again.seatCount} json={state.Serialize().Length}";
    }

    /// <summary>What the nearest table is made of and where it is (for finding why it does not show).</summary>
    public static string TestTableInfo()
    {
        var table = Nearest();
        if (table == null)
            return "no table";
        var player = Player.m_localPlayer;
        var renderers = table.GetComponentsInChildren<Renderer>(true);
        var parts = renderers.Select(r =>
            $"{r.name}:{(r.enabled ? "on" : "off")}/{(r.gameObject.activeInHierarchy ? "active" : "inactive")}" +
            $"/{(r.sharedMaterial != null ? r.sharedMaterial.shader.name : "nomat")}/size{r.bounds.size.x:0.0}x{r.bounds.size.y:0.0}x{r.bounds.size.z:0.0}");
        var offset = table.transform.position - player.transform.position;
        return $"pos+{offset.x:0.0},{offset.y:0.0},{offset.z:0.0} layer={table.gameObject.layer} renderers={renderers.Length} " + string.Join(" ; ", parts);
    }

    /// <summary>Height of the table top above the ground under it, in meters.</summary>
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

    /// <summary>The table's size in meters as "x,z" (to check Small and Long differ).</summary>
    public static string TestSize() => Act(t => $"{t.Size.x:0.0},{t.Size.y:0.0}", null, "");
}
