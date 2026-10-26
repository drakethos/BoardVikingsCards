using System.Collections.Generic;
using Cards.Util;
using UnityEngine;

namespace Cards.Table;

/// <summary>
/// Draws the table from its saved state: seat markers, the deck, the discard pile, cards played onto the felt, every hand (only
/// the faces the player turned up are shown; the panel shows you your own) and the chips. Objects are pooled and reused.
/// </summary>
internal sealed class TableView : MonoBehaviour
{
    private readonly GameObject?[] _markers = new GameObject?[TableState.MaxSeats];
    private readonly List<GameObject> _cards = new List<GameObject>();
    private readonly List<GameObject> _chips = new List<GameObject>();
    private int _cardsUsed;
    private int _chipsUsed;
    private Material? _empty;
    private Material? _taken;
    private static Material[]? _chipMaterials;

    private float Top => GetComponent<CardTable>().TopHeight + 0.014f;

    internal void Redraw(TableState state)
    {
        var table = GetComponent<CardTable>();
        if (table == null)
            return;

        DrawSeats(table, state);
        _cardsUsed = 0;
        _chipsUsed = 0;

        DrawDeck(table.Size, state);
        DrawDiscard(table.Size, state);
        DrawPlayed(table.Size, state);
        DrawHands(table.Size, state);
        DrawChips(table.Size, state);

        for (int i = _cardsUsed; i < _cards.Count; i++)
            _cards[i].SetActive(false);
        for (int i = _chipsUsed; i < _chips.Count; i++)
            _chips[i].SetActive(false);
    }

    // ---------------------------------------------------------------- seats

    private void DrawSeats(CardTable table, TableState state)
    {
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

    // ---------------------------------------------------------------- cards

    private static Quaternion Lay(Vector3 facing, bool faceUp)
    {
        var rotation = Quaternion.LookRotation(facing, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
        return faceUp ? rotation : rotation * Quaternion.Euler(0f, 180f, 0f);
    }

    private void ShowCard(Vector3 position, Vector3 facing, int face, bool faceUp, int back, float lift = 0f)
    {
        GameObject card;
        if (_cardsUsed < _cards.Count)
        {
            card = _cards[_cardsUsed];
        }
        else
        {
            card = new GameObject("table_card");
            card.transform.SetParent(transform, false);
            card.layer = gameObject.layer;
            CardMesh.MakeFace("front", card.transform, Vector3.zero);
            CardMesh.MakeFace("back", card.transform, Vector3.zero);
            _cards.Add(card);
        }

        _cardsUsed++;
        card.SetActive(true);
        card.transform.localPosition = new Vector3(position.x, Top + position.y + lift, position.z);
        card.transform.localRotation = Lay(facing, faceUp);
        CardMesh.Apply(card.transform.Find("front").gameObject, SpriteTools.TryGetSprite(CardLib.CodeOf(face)), TableLayout.CardSize);
        CardMesh.Apply(card.transform.Find("back").gameObject, CardBacks.Back(back), TableLayout.CardSize);
        Dim(card.transform.Find("front").gameObject);
        Dim(card.transform.Find("back").gameObject);
    }

    /// <summary>Cards lying flat catch the full sun and glow, while upright ones stay in shade, so flat ones are dimmed further.</summary>
    private static void Dim(GameObject face)
    {
        var renderer = face.GetComponent<Renderer>();
        if (renderer == null)
            return;
        _block ??= new MaterialPropertyBlock();
        var level = CardMesh.Level(CardMesh.Brightness) * 0.8f;
        renderer.GetPropertyBlock(_block);
        _block.SetColor("_Color", new Color(level, level, level, 1f));
        renderer.SetPropertyBlock(_block);
    }

    private static MaterialPropertyBlock? _block;

    private void DrawDeck(Vector2 size, TableState state)
    {
        int layers = Mathf.Min(8, (state.deck.Count + 7) / 8);
        for (int i = 0; i < layers; i++)
            ShowCard(TableLayout.DeckSpot(size), Vector3.forward, state.deck[Mathf.Min(i, state.deck.Count - 1)], false, state.deckBack, i * 0.003f);
    }

    private void DrawDiscard(Vector2 size, TableState state)
    {
        int shown = Mathf.Min(3, state.discard.Count);
        for (int i = shown - 1; i >= 0; i--)
        {
            var facing = Quaternion.Euler(0f, (i * 17f) - 8f, 0f) * Vector3.forward;
            ShowCard(TableLayout.DiscardSpot(size), facing, state.discard[i], true, state.deckBack, (shown - 1 - i) * 0.003f);
        }
    }

    private void DrawPlayed(Vector2 size, TableState state)
    {
        for (int i = 0; i < state.played.Count; i++)
            ShowCard(TableLayout.PlayedSpot(size, i), Vector3.forward, state.played[i], state.playedUp[i], state.deckBack);
    }

    private void DrawHands(Vector2 size, TableState state)
    {
        for (int seat = 0; seat < state.seatCount; seat++)
        {
            var hand = state.seats[seat];
            for (int i = 0; i < hand.hand.Count; i++)
            {
                TableLayout.HandSpot(size, seat, state.seatCount, i, hand.hand.Count, out var pos, out var facing);
                ShowCard(pos, facing, hand.hand[i], hand.up[i] || state.reveal, state.deckBack);
            }
        }
    }

    // ---------------------------------------------------------------- chips

    private static Material ChipMaterial(int kind)
    {
        _chipMaterials ??= new[]
        {
            TableMaterials.Flat(new Color(0.92f, 0.92f, 0.88f)),
            TableMaterials.Flat(new Color(0.72f, 0.12f, 0.12f)),
            TableMaterials.Flat(new Color(0.15f, 0.55f, 0.25f)),
            TableMaterials.Flat(new Color(0.10f, 0.10f, 0.12f))
        };
        return _chipMaterials[Mathf.Clamp(kind, 0, Chips.Kinds - 1)];
    }

    private void Stack(Vector3 at, int kind, int count)
    {
        int shown = Mathf.Min(count, 12);
        for (int i = 0; i < shown; i++)
        {
            GameObject chip;
            if (_chipsUsed < _chips.Count)
            {
                chip = _chips[_chipsUsed];
            }
            else
            {
                chip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                chip.name = "chip";
                var collider = chip.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);
                chip.transform.SetParent(transform, false);
                chip.transform.localScale = new Vector3(0.044f, 0.0035f, 0.044f);
                chip.layer = gameObject.layer;
                chip.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _chips.Add(chip);
            }

            _chipsUsed++;
            chip.SetActive(true);
            chip.transform.localPosition = new Vector3(at.x, Top + 0.0035f + i * 0.007f, at.z);
            chip.GetComponent<MeshRenderer>().sharedMaterial = ChipMaterial(kind);
        }
    }

    private void DrawChips(Vector2 size, TableState state)
    {
        for (int seat = 0; seat < state.seatCount; seat++)
        {
            TableLayout.Seat(size, seat, state.seatCount, out _, out var facing);
            var across = Vector3.Cross(Vector3.up, facing).normalized;
            var spot = TableLayout.ChipSpot(size, seat, state.seatCount);
            for (int kind = 0; kind < Chips.Kinds; kind++)
            {
                if (state.seats[seat].chips[kind] > 0)
                    Stack(spot + across * (kind * 0.05f), kind, state.seats[seat].chips[kind]);
            }
        }

        var pot = TableLayout.PotSpot(size);
        for (int kind = 0; kind < Chips.Kinds; kind++)
        {
            if (state.pot[kind] > 0)
                Stack(pot + Vector3.right * ((kind - 1.5f) * 0.05f), kind, state.pot[kind]);
        }
    }
}
