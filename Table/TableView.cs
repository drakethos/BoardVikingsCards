using UnityEngine;

namespace Cards.Table;

/// <summary>
/// Draws the table from its saved state: a marker on the felt for each switched-on seat (gold when someone sits there, dim when empty).
/// Cards, chips and piles join this as the table gets more to show.
/// </summary>
internal sealed class TableView : MonoBehaviour
{
    private readonly GameObject?[] _markers = new GameObject?[TableState.MaxSeats];
    private Material? _empty;
    private Material? _taken;

    internal void Redraw(TableState state)
    {
        var table = GetComponent<CardTable>();
        if (table == null)
            return;

        for (int i = 0; i < TableState.MaxSeats; i++)
        {
            bool on = i < state.seatCount;
            var marker = _markers[i];
            if (!on)
            {
                if (marker != null)
                    marker.SetActive(false);
                continue;
            }

            if (marker == null)
                marker = _markers[i] = MakeMarker(i);

            TableLayout.Seat(table.Size, i, state.seatCount, out var pos, out var facing);
            marker.transform.localPosition = new Vector3(pos.x, table.TopHeight + 0.018f, pos.z);
            marker.transform.localRotation = Quaternion.LookRotation(facing, Vector3.up);
            marker.GetComponent<MeshRenderer>().sharedMaterial = state.seats[i].Taken ? Taken() : Empty();
            marker.SetActive(true);
        }
    }

    private GameObject MakeMarker(int index)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "seat_" + (index + 1);
        var collider = go.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
        go.transform.SetParent(transform, false);
        go.transform.localScale = new Vector3(0.16f, 0.003f, 0.16f);
        go.layer = gameObject.layer;
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    private Material Empty() => _empty ??= TableMaterials.Flat(new Color(0.20f, 0.36f, 0.22f));

    private Material Taken() => _taken ??= TableMaterials.Flat(new Color(0.85f, 0.68f, 0.28f));
}
