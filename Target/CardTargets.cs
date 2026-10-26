using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cards.Util;
using HarmonyLib;
using UnityEngine;

namespace Cards.Target;

/// <summary>
/// The vanilla archery target, taught to take throwing cards. A card that hits scores like an arrow (centre = most points),
/// stays stuck in the target for a while, and pressing E on the target hands every card back with its own face. All of the
/// scoring, hover text and bull's-eye effects are the game's own; this only adds the card as one more kind of ammo.
///
/// The store's compile-time references do not include ArcheryTarget, so everything here finds it by name.
/// </summary>
internal static class CardTargets
{
    internal const string TargetPrefab = "piece_ArcheryTarget";
    internal const string CardsKey = "BoardVikings_TargetCards";
    internal const string HitRpc = "BoardVikings_CardHit";

    internal static readonly Type? TargetType = AccessTools.TypeByName("ArcheryTarget");

    private static readonly FieldInfo? NviewField = AccessTools.Field(TargetType, "m_nview");
    private static readonly FieldInfo? ReturnAmmoField = AccessTools.Field(TargetType, "m_returnAmmo");
    private static readonly FieldInfo? ReturnPointField = AccessTools.Field(TargetType, "m_returnPoint");
    private static readonly FieldInfo? SpawnItemField = AccessTools.Field(typeof(Projectile), "m_spawnItem");
    private static readonly Type? Vars = AccessTools.TypeByName("ZDOVars");
    private static readonly FieldInfo? AmmoTypeKey = AccessTools.Field(Vars, "s_ammoType");
    private static readonly FieldInfo? HitCountKey = AccessTools.Field(Vars, "s_hitPoint");
    private static readonly FieldInfo? ScoreKey = AccessTools.Field(Vars, "s_dataCount");

    internal static bool Available => TargetType != null && ReturnAmmoField != null;

    /// <summary>Patch the target's start, hit and drop methods (only when the game has an archery target).</summary>
    internal static void Apply(Harmony harmony)
    {
        if (!Available)
        {
            Jotunn.Logger.LogInfo("No archery target in this game version; skipping target support.");
            return;
        }

        harmony.Patch(AccessTools.Method(TargetType, "Start"),
            postfix: new HarmonyMethod(typeof(CardTargets), nameof(Started)));
        harmony.Patch(AccessTools.Method(TargetType, "OnProjectileHit"),
            prefix: new HarmonyMethod(typeof(CardTargets), nameof(Hit)));
        harmony.Patch(AccessTools.Method(TargetType, "DropArrows"),
            prefix: new HarmonyMethod(typeof(CardTargets), nameof(Drop)));
    }

    private static IList<ItemDrop>? Ammo(Component target) => ReturnAmmoField?.GetValue(target) as IList<ItemDrop>;

    /// <summary>
    /// Make every archery target prefab count a throwing card as ammo. Finds them by their component, so the prefab's own name
    /// does not matter (and is logged, to use in tests).
    /// </summary>
    internal static void Register(ItemDrop? card)
    {
        if (card == null || !Available)
        {
            Jotunn.Logger.LogWarning($"Archery target support skipped (card: {(card != null ? "ok" : "missing")}, " +
                                     $"target type: {(TargetType != null ? "found" : "missing")}, ammo list: {(ReturnAmmoField != null ? "found" : "missing")}).");
            return;
        }

        var found = Jotunn.Managers.PrefabManager.Cache.GetPrefabs(TargetType);
        if (found == null || found.Count == 0)
        {
            Jotunn.Logger.LogWarning("No archery target prefab found; cards cannot be thrown at targets.");
            return;
        }

        Jotunn.Logger.LogInfo($"Archery target components found on: {string.Join(", ", found.Values.OfType<Component>().Select(c => c.transform.root.name))}.");
        foreach (var entry in found)
        {
            // The cache hands back the components themselves (or their objects); take whichever it gives.
            var target = entry.Value as Component
                         ?? (entry.Value as GameObject)?.GetComponentInChildren(TargetType, true);
            var ammo = target != null ? Ammo(target) : null;
            if (ammo == null)
                continue;
            if (ammo.All(a => a == null || a.gameObject.name != card.gameObject.name))
                ammo.Add(card);
            Jotunn.Logger.LogInfo($"Archery target '{target!.transform.root.name}' accepts throwing cards ({ammo.Count} kinds of ammo).");
        }
    }

    private static readonly FieldInfo? CenterField = AccessTools.Field(TargetType, "m_center");

    /// <summary>The bullseye's position.</summary>
    internal static Vector3? CenterOf(Component target) =>
        (CenterField?.GetValue(target) as GameObject) is { } c ? c.transform.position : (Vector3?)null;

    internal static ZNetView? NviewOf(Component target) => NviewField?.GetValue(target) as ZNetView;

    private static readonly MethodInfo? SetIntByHash = typeof(ZDO).GetMethod("Set", new[] { typeof(int), typeof(int), typeof(bool) });

    /// <summary>
    /// Sets a whole number by hash. The store's reference set has Set(int, int); the live game's takes a third argument, so the
    /// call is made by name to work against both.
    /// </summary>
    private static void SetInt(ZDO zdo, int hash, int value)
    {
        if (SetIntByHash != null)
            SetIntByHash.Invoke(zdo, new object[] { hash, value, false });
        else
            typeof(ZDO).GetMethod("Set", new[] { typeof(int), typeof(int) })?.Invoke(zdo, new object[] { hash, value });
    }

    /// <summary>ItemDrop.Save is private in the real game: writes the item's data (its card face) into the dropped object.</summary>
    private static readonly MethodInfo? SaveItem = AccessTools.Method(typeof(ItemDrop), "Save");

    private static int Key(FieldInfo? field) => field != null ? (int)field.GetValue(null) : 0;

    internal static int Hits(Component target) =>
        NviewOf(target) is { } v && v.IsValid() ? v.GetZDO().GetInt(Key(HitCountKey)) : -1;

    internal static int Score(Component target) =>
        NviewOf(target) is { } v && v.IsValid() ? v.GetZDO().GetInt(Key(ScoreKey)) : -1;

    internal static bool AcceptsCards(Component target) =>
        Ammo(target)?.Any(a => a != null && a.gameObject.name == CardLib.ThrowingPrefab) == true;

    internal static Component? Nearest()
    {
        var player = Player.m_localPlayer;
        if (player == null || TargetType == null)
            return null;
        return UnityEngine.Object.FindObjectsOfType(TargetType)
            .OfType<Component>()
            .OrderBy(t => (t.transform.position - player.transform.position).sqrMagnitude)
            .FirstOrDefault();
    }

    // ---------------------------------------------------------------- the list of faces stuck in a target

    internal static List<int> Faces(ZDO zdo)
    {
        var raw = zdo.GetString(CardsKey, "");
        return string.IsNullOrEmpty(raw)
            ? new List<int>()
            : raw.Split(',').Select(s => int.TryParse(s, out var n) ? n : 0).ToList();
    }

    private static void AddFace(ZNetView nview, int variant)
    {
        var zdo = nview.GetZDO();
        var faces = Faces(zdo);
        faces.Add(variant);
        zdo.Set(CardsKey, string.Join(",", faces));
    }

    // ---------------------------------------------------------------- patches (applied by hand, see Apply)

    /// <summary>Each target also listens for "a card with this face hit me" from whoever threw it.</summary>
    private static void Started(Component __instance)
    {
        var nview = NviewOf(__instance);
        if (nview == null || !nview.IsValid())
            return;
        nview.Register<int>(HitRpc, (sender, variant) =>
        {
            if (nview.IsValid() && nview.IsOwner())
                AddFace(nview, variant);
        });
    }

    /// <summary>A card hit the target: remember which card it was, and do not also drop it on the ground.</summary>
    private static void Hit(Component __instance, Projectile projectile)
    {
        if (projectile == null || projectile.GetComponent<ProjectileCardDriver>() == null)
            return;
        if (SpawnItemField?.GetValue(projectile) is not ItemDrop.ItemData item)
            return;

        var nview = NviewOf(__instance);
        if (nview != null && nview.IsValid())
        {
            if (nview.IsOwner())
                AddFace(nview, item.m_variant);
            else
                nview.InvokeRPC(HitRpc, item.m_variant);
        }

        // The target keeps this card (it comes back with E); the projectile must not also respawn it as an item.
        SpawnItemField.SetValue(projectile, null);
    }

    /// <summary>Hand the stuck cards back with their faces. Runs before the game's own drop, which then has no cards left to give.</summary>
    private static void Drop(Component __instance)
    {
        var nview = NviewOf(__instance);
        var ammo = Ammo(__instance);
        var point = ReturnPointField?.GetValue(__instance) as GameObject;
        if (nview == null || !nview.IsValid() || !nview.IsOwner() || ammo == null || point == null)
            return;

        int index = -1;
        for (int i = 0; i < ammo.Count; i++)
        {
            if (ammo[i] != null && ammo[i].gameObject.name == CardLib.ThrowingPrefab)
                index = i;
        }

        if (index < 0)
            return;

        var zdo = nview.GetZDO();
        int count = zdo.GetInt(Key(AmmoTypeKey) + index);
        if (count <= 0)
            return;

        var faces = Faces(zdo);
        for (int i = 0; i < count; i++)
        {
            var drop = UnityEngine.Object.Instantiate(ammo[index], point.transform.position, UnityEngine.Random.rotation);
            drop.m_itemData.m_variant = i < faces.Count ? faces[i] : 0;
            SaveItem?.Invoke(drop, null);
        }

        SetInt(zdo, Key(AmmoTypeKey) + index, 0);
        zdo.Set(CardsKey, "");
    }
}
