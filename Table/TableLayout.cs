using UnityEngine;

namespace Cards.Table;

/// <summary>Where the seats sit around the edge of a rectangular table, in the table's own space (origin = middle of the table top).</summary>
internal static class TableLayout
{
    /// <summary>How far inside the edge the seats' cards lie.</summary>
    private const float Inset = 0.20f;

    /// <summary>
    /// Seat position and the direction the seat faces (towards the middle). Seats are spread evenly along the edge, starting at
    /// the middle of the south side and going counter-clockwise, so two seats face each other and four make a cross.
    /// </summary>
    internal static void Seat(Vector2 size, int index, int count, out Vector3 position, out Vector3 facing)
    {
        float a = size.x / 2f - Inset;
        float b = size.y / 2f - Inset;
        float perimeter = 4f * (a + b);
        float s = perimeter * index / Mathf.Max(1, count);

        // Segments from the south middle: south right half, east side, north side, west side, south left half.
        float[] length = { a, 2f * b, 2f * a, 2f * b, a };
        Vector2[] start = { new Vector2(0f, -b), new Vector2(a, -b), new Vector2(a, b), new Vector2(-a, b), new Vector2(-a, -b) };
        Vector2[] direction = { Vector2.right, Vector2.up, Vector2.left, Vector2.down, Vector2.right };

        int segment = 0;
        while (segment < 4 && s > length[segment])
        {
            s -= length[segment];
            segment++;
        }

        var p = start[segment] + direction[segment] * s;
        position = new Vector3(p.x, 0f, p.y);
        facing = new Vector3(-p.x, 0f, -p.y);
        if (facing.sqrMagnitude < 0.0001f)
            facing = Vector3.forward;
        facing.Normalize();
    }

    /// <summary>Size of a card lying on the table, in meters.</summary>
    internal static readonly Vector2 CardSize = new Vector2(0.10f, 0.14f);

    /// <summary>The deck's spot (left of the middle) and the discard pile's (right of it).</summary>
    internal static Vector3 DeckSpot(Vector2 size) => new Vector3(-0.13f, 0f, 0f);

    internal static Vector3 DiscardSpot(Vector2 size) => new Vector3(0.13f, 0f, 0f);

    /// <summary>Where the pot's chips sit.</summary>
    internal static Vector3 PotSpot(Vector2 size) => new Vector3(0f, 0f, -0.16f);

    /// <summary>
    /// Where the n-th played card lies: a tidy grid across the middle of the felt, behind the deck and discard (towards the north edge).
    /// </summary>
    internal static Vector3 PlayedSpot(Vector2 size, int index)
    {
        int perRow = Mathf.Max(3, Mathf.FloorToInt((size.x - 0.5f) / 0.115f));
        int row = index / perRow;
        int col = index % perRow;
        float width = (perRow - 1) * 0.115f;
        return new Vector3(-width / 2f + col * 0.115f, 0f, 0.17f + row * 0.15f);
    }

    /// <summary>A seat's hand: cards fanned in front of the seat, towards the middle, centred on the seat.</summary>
    internal static void HandSpot(Vector2 size, int seat, int seatCount, int index, int handCount, out Vector3 position, out Vector3 facing)
    {
        Seat(size, seat, seatCount, out var at, out facing);
        var across = Vector3.Cross(Vector3.up, facing).normalized;
        float step = Mathf.Min(0.075f, 0.5f / Mathf.Max(1, handCount));
        float offset = (index - (handCount - 1) / 2f) * step;
        position = at + facing * 0.12f + across * offset + Vector3.up * (0.002f * index);
    }

    /// <summary>A seat's chips sit just beside the hand.</summary>
    internal static Vector3 ChipSpot(Vector2 size, int seat, int seatCount)
    {
        Seat(size, seat, seatCount, out var at, out var facing);
        var across = Vector3.Cross(Vector3.up, facing).normalized;
        return at + facing * 0.12f + across * 0.30f;
    }
}
