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
}
