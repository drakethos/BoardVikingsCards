using HarmonyLib;
using UnityEngine;

namespace Cards.Table;

/// <summary>
/// When a card table is broken (the hammer, or its health hitting zero) the cards on it are destroyed. Zone unload calls
/// <see cref="Object.OnDestroy"/>, not <see cref="WearNTear.Destroy"/>, so walking away does not eat the game.
/// </summary>
[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Destroy))]
internal static class TableBreak
{
    [HarmonyPrefix]
    private static void Prefix(WearNTear __instance)
    {
        if (__instance == null)
            return;
        try
        {
            var table = __instance.GetComponent<CardTable>();
            if (table)
                table.EatCards();
        }
        catch (System.Exception ex)
        {
            Jotunn.Logger.LogWarning($"Card table break failed: {ex.Message}");
        }
    }
}
