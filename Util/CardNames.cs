using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Cards.Util;

/// <summary>
/// One item holds all 53 faces (and the deck holds 15 backs), so the item's own name cannot say which card it is. These patches show
/// "7 of Spades" (or "Deck of Cards (Sea)") where the game shows an item's name: the inventory tooltip and the hover text of
/// an item on the ground.
/// </summary>
internal static class CardNames
{
    /// <summary>The name to show for a card or deck item, or null for any other item.</summary>
    internal static string? Display(ItemDrop.ItemData? item)
    {
        if (item?.m_dropPrefab == null)
            return null;
        var prefab = item.m_dropPrefab.name;
        if (prefab == Deck.DeckData.DeckPrefab)
            return Deck.DeckData.DeckName + " (" + CardBacks.Names[CardBacks.Clamp(item.m_variant)] + ")";
        return CardLib.IsVariantCardPrefab(prefab) ? CardLib.NameOf(item.m_variant) : null;
    }
}

[HarmonyPatch]
internal static class CardNamePatches
{
    /// <summary>The item on the ground: its hover name.</summary>
    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.GetHoverName))]
    [HarmonyPostfix]
    private static void HoverName(ItemDrop __instance, ref string __result)
    {
        var name = CardNames.Display(__instance.m_itemData);
        if (name != null)
            __result = name;
    }

    /// <summary>The item on the ground: the hover text, where the generic name shows.</summary>
    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.GetHoverText))]
    [HarmonyPostfix]
    private static void HoverText(ItemDrop __instance, ref string __result)
    {
        var name = CardNames.Display(__instance.m_itemData);
        var shared = __instance.m_itemData?.m_shared;
        if (name == null || shared == null || string.IsNullOrEmpty(__result))
            return;
        var generic = Localization.instance != null ? Localization.instance.Localize(shared.m_name) : shared.m_name;
        if (!string.IsNullOrEmpty(generic) && __result.Contains(generic))
            __result = __result.Replace(generic, name);
    }

    /// <summary>The inventory tooltip: the card's own name on top.</summary>
    [HarmonyPatch]
    private static class Tooltip
    {
        private static System.Collections.Generic.IEnumerable<MethodBase> TargetMethods() =>
            typeof(ItemDrop.ItemData).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                .Where(m => m.Name == "GetTooltip" && m.IsStatic && m.GetParameters().Length > 0
                            && m.GetParameters()[0].ParameterType == typeof(ItemDrop.ItemData));

        [HarmonyPostfix]
        private static void Postfix(ItemDrop.ItemData item, ref string __result)
        {
            var name = CardNames.Display(item);
            if (name != null)
                __result = "<b>" + name + "</b>\n" + __result;
        }
    }
}
