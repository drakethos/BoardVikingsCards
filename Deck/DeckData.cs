using System;
using System.Collections.Generic;
using System.Linq;
using Cards.Util;
using UnityEngine;

namespace Cards.Deck;

/// <summary>
/// A deck is ONE item. Its remaining cards live in the item's custom data as an ordered list of card codes (top first),
/// so 52 cards cost one inventory slot and nobody shuffles by hand. A deck with no saved list is a fresh ordered 52.
/// </summary>
internal static class DeckData
{
    internal const string DeckPrefab = "Card_Deck";
    internal const string DeckName = "Deck of Cards";
    private const string Key = "BoardVikings_Deck";
    private const char Sep = ',';

    private static readonly HashSet<string> ValidCodes =
        new HashSet<string>(CardLib.All.Select(c => c.SpriteName), StringComparer.Ordinal);

    internal static bool IsDeck(ItemDrop.ItemData? item) =>
        item?.m_dropPrefab != null && item.m_dropPrefab.name == DeckPrefab;

    /// <summary>The card code of a loose card item (or null).</summary>
    internal static string? LooseCardCode(ItemDrop.ItemData? item)
    {
        var name = item?.m_dropPrefab != null ? item.m_dropPrefab.name : null;
        if (name == null || !name.StartsWith("Card_", StringComparison.Ordinal) || name == DeckPrefab)
            return null;
        var code = name.Substring(5);
        return ValidCodes.Contains(code) ? code : null;
    }

    /// <summary>Fresh deck: 52 cards in suit order (no Joker; Jokers stay separate loose cards).</summary>
    internal static List<string> Fresh() =>
        CardLib.All.Select(c => c.SpriteName).Where(c => c != "Joker").ToList();

    internal static List<string> Read(ItemDrop.ItemData deck)
    {
        if (deck.m_customData == null || !deck.m_customData.TryGetValue(Key, out var raw))
            return Fresh();
        if (string.IsNullOrEmpty(raw))
            return new List<string>();
        return raw.Split(Sep).Where(ValidCodes.Contains).ToList();
    }

    internal static void Write(ItemDrop.ItemData deck, List<string> cards)
    {
        deck.m_customData ??= new Dictionary<string, string>();
        deck.m_customData[Key] = string.Join(Sep.ToString(), cards);
    }

    /// <summary>Fisher-Yates.</summary>
    internal static void Shuffle(List<string> cards)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }

    /// <summary>Gives the player one loose card. False when the inventory has no room.</summary>
    internal static bool GiveCard(Player player, string code)
    {
        var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("Card_" + code) : null;
        var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
        if (drop == null)
        {
            Jotunn.Logger.LogWarning($"Card prefab Card_{code} missing; cannot pull it from the deck.");
            return false;
        }

        var data = drop.m_itemData.Clone();
        data.m_dropPrefab = prefab;
        data.m_stack = 1;
        data.m_quality = 1;
        return player.GetInventory().AddItem(data);
    }
}
