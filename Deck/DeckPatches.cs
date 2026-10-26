using HarmonyLib;

namespace Cards.Deck;

/// <summary>Right-click (or hotbar use) on a deck opens the deck window instead of "using" it.</summary>
[HarmonyPatch]
internal static class DeckPatches
{
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
    [HarmonyPrefix]
    private static bool UseDeck(Humanoid __instance, ItemDrop.ItemData item)
    {
        if (!DeckData.IsDeck(item) || __instance != Player.m_localPlayer)
            return true;

        DeckWindow.Open(item);
        return false;
    }
}
