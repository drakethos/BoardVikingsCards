using Cards.Util;
using System.Linq;
using UnityEngine;

namespace Cards.Deck;

/// <summary>
/// The deck's actions in one place. The window calls these, and so do the TestLab tests (via invoke), so what the tests prove
/// is what a click does.
/// </summary>
public static class DeckApi
{
    /// <summary>The local player's first deck item, or null.</summary>
    internal static ItemDrop.ItemData? FindDeck()
    {
        var player = Player.m_localPlayer;
        return player == null
            ? null
            : player.GetInventory().GetAllItems().FirstOrDefault(DeckData.IsDeck);
    }

    /// <summary>Pull one card out of the deck into the inventory. False: no deck, card not in it, or inventory full.</summary>
    internal static bool Pull(ItemDrop.ItemData deck, string code)
    {
        var player = Player.m_localPlayer;
        if (player == null)
            return false;
        var cards = DeckData.Read(deck);
        if (!cards.Contains(code) || !DeckData.GiveCard(player, code, deck.m_variant))
            return false;
        cards.Remove(code);
        DeckData.Write(deck, cards);
        return true;
    }

    /// <summary>Put a loose card on top of the deck. A deck never holds the same card twice (Jokers excepted).</summary>
    internal static bool Return(ItemDrop.ItemData deck, ItemDrop.ItemData item)
    {
        var player = Player.m_localPlayer;
        var code = DeckData.LooseCardCode(item);
        if (player == null || code == null)
            return false;
        var cards = DeckData.Read(deck);
        if (cards.Contains(code) && code != "Joker")
            return false;
        player.GetInventory().RemoveItem(item);
        cards.Insert(0, code);
        DeckData.Write(deck, cards);
        return true;
    }

    /// <summary>Put every loose card the player carries back. Returns how many went.</summary>
    internal static int ReturnAll(ItemDrop.ItemData deck)
    {
        var player = Player.m_localPlayer;
        if (player == null)
            return 0;
        int moved = 0;
        foreach (var item in player.GetInventory().GetAllItems().ToList())
        {
            var code = DeckData.LooseCardCode(item);
            if (code == null || !Return(deck, item))
                continue;
            moved++;
        }

        return moved;
    }

    internal static void Shuffle(ItemDrop.ItemData deck)
    {
        var cards = DeckData.Read(deck);
        DeckData.Shuffle(cards);
        DeckData.Write(deck, cards);
    }

    // ---- string/primitive entry points for TestLab "invoke" steps (they act on the player's first deck)

    public static int TestCount() => FindDeck() is { } d ? DeckData.Read(d).Count : -1;

    public static bool TestPull(string code) => FindDeck() is { } d && Pull(d, code);

    public static int TestReturnAll() => FindDeck() is { } d ? ReturnAll(d) : -1;

    /// <summary>Shuffles and returns the top card; running it twice makes a repeat very unlikely.</summary>
    public static string TestShuffleTop()
    {
        var d = FindDeck();
        if (d == null)
            return "";
        Shuffle(d);
        var cards = DeckData.Read(d);
        return cards.Count > 0 ? string.Join(",", cards.Take(8)) : "";
    }

    public static bool TestOpenWindow()
    {
        var d = FindDeck();
        if (d == null)
            return false;
        DeckWindow.Open(d);
        return true;
    }

    // ---- the deck on a card table

    /// <summary>The cards left in a deck item as card numbers (see CardLib), top first.</summary>
    internal static System.Collections.Generic.List<int> ToTableCards(ItemDrop.ItemData deck) =>
        DeckData.Read(deck).Select(CardLib.IndexOf).Where(i => i >= 0).ToList();

    /// <summary>
    /// Gives the player a deck item holding these cards in this back design (it drops at their feet when the inventory is full).
    /// </summary>
    internal static bool MakeDeckItem(Player player, System.Collections.Generic.IList<int> cards, int back)
    {
        var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(DeckData.DeckPrefab) : null;
        var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
        if (drop == null)
            return false;
        var data = drop.m_itemData.Clone();
        data.m_dropPrefab = prefab;
        data.m_stack = 1;
        data.m_quality = 1;
        data.m_variant = CardBacks.Clamp(back);
        DeckData.Write(data, cards.Select(CardLib.CodeOf).ToList());
        if (!player.GetInventory().AddItem(data))
            ItemDrop.DropItem(data, 1, player.transform.position + player.transform.forward + Vector3.up, player.transform.rotation);
        return true;
    }
}
