using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Cards.Util;

/// <summary>
/// One card item shows any of the 53 faces (the item's variant). This sits on the visual root ("attach") of a card prefab and
/// swaps the front sprite to the variant's card, keeping its true proportions inside <see cref="Box"/>.
/// </summary>
internal sealed class CardFaceVisual : MonoBehaviour
{
    /// <summary>Largest the card may be (copied into every spawned clone).</summary>
    public Vector2 Box = new Vector2(0.18f, 0.25f);

    /// <summary>A deck box: the variant is its back design and both sides show the box.</summary>
    public bool Deck;

    private GameObject? _front;
    private GameObject? _back;
    private int _shown = -1;
    private int _shownBack = -1;

    private bool Find()
    {
        if (_front == null)
        {
            var t = transform.Find("front");
            _front = t != null ? t.gameObject : null;
        }

        if (_back == null)
        {
            var t = transform.Find("back");
            _back = t != null ? t.gameObject : null;
        }

        return _front != null && _back != null;
    }

    /// <summary>Show the face of a card variant (or, for a deck box, the box of that back design).</summary>
    internal void Set(int variant)
    {
        if (variant == _shown || !Find())
            return;
        _shown = variant;
        if (Deck)
        {
            var box = CardBacks.DeckIcon(variant);
            CardItemSanitize.ApplySprite(_front!, box, Box);
            CardItemSanitize.ApplySprite(_back!, box, Box);
            return;
        }

        var sprite = SpriteTools.TryGetSprite(CardLib.CodeOf(variant)) ?? CardBacks.Back(CardBacks.DefaultBack);
        CardItemSanitize.ApplySprite(_front!, sprite, Box);
    }

    /// <summary>Show the back design on the other side of the card.</summary>
    internal void SetBack(int back)
    {
        if (Deck || back == _shownBack || !Find())
            return;
        _shownBack = back;
        CardItemSanitize.ApplySprite(_back!, CardBacks.Back(back), Box);
    }
}

/// <summary>Root of a card item prefab: keeps the world visual on the item's variant (it can change after pick-up or load).</summary>
internal sealed class CardItemDriver : MonoBehaviour
{
    private ItemDrop? _drop;
    private CardFaceVisual? _visual;
    private int _last = -1;
    private int _lastBack = -1;

    private void Awake()
    {
        _drop = GetComponent<ItemDrop>();
        _visual = GetComponentInChildren<CardFaceVisual>(true);
        Refresh();
        InvokeRepeating(nameof(Refresh), 0.2f, 0.5f);
    }

    private void Refresh()
    {
        if (_drop == null || _visual == null || _drop.m_itemData == null)
            return;
        int v = _drop.m_itemData.m_variant;
        int back = CardBacks.BackOf(_drop.m_itemData);
        if (v == _last && back == _lastBack)
            return;
        _last = v;
        _lastBack = back;
        _visual.Set(v);
        _visual.SetBack(back);
    }
}

/// <summary>Root of the thrown-card projectile: shows the face of the card that was thrown.</summary>
internal sealed class ProjectileCardDriver : MonoBehaviour
{
    private static readonly FieldInfo? SpawnItem = AccessTools.Field(typeof(Projectile), "m_spawnItem");
    private static readonly FieldInfo? Vel = AccessTools.Field(typeof(Projectile), "m_vel");

    /// <summary>Test hook: log the flight of the next thrown card.</summary>
    internal static bool Probe;

    private Projectile? _projectile;
    private Rigidbody? _body;
    private CardFaceVisual? _visual;
    private bool _done;
    private float _until;
    private float _born;
    private int _logged;

    private void Awake()
    {
        _projectile = GetComponent<Projectile>();
        _body = GetComponent<Rigidbody>();
        _visual = GetComponentInChildren<CardFaceVisual>(true);
        _until = Time.time + 2f;
        _born = Time.time;
        if (Probe)
            Jotunn.Logger.LogInfo($"[probe] projectile born at {transform.position}");
    }

    private void Update()
    {
        if (Probe && _logged < 12 && _projectile != null)
        {
            _logged++;
            var vel = Vel?.GetValue(_projectile);
            Jotunn.Logger.LogInfo($"[probe] t={Time.time - _born:0.00} pos={transform.position} vel={vel} " +
                                  $"kinematic={_body?.isKinematic} bodyVel={_body?.velocity} active={gameObject.activeSelf}");
        }

        if (_done || _projectile == null || _visual == null || Time.time > _until)
        {
            if (!Probe || Time.time > _until + 1f)
                enabled = false;
            return;
        }

        if (SpawnItem?.GetValue(_projectile) is ItemDrop.ItemData item)
        {
            _visual.Set(item.m_variant);
            _visual.SetBack(CardBacks.BackOf(item));
            _done = true;
        }
    }

    private void OnDestroy()
    {
        if (Probe)
            Jotunn.Logger.LogInfo($"[probe] projectile destroyed at {transform.position} after {Time.time - _born:0.00}s");
    }
}

/// <summary>Test probe: what a thrown card hits.</summary>
[HarmonyPatch]
internal static class ProjectileHitProbe
{
    [HarmonyPatch(typeof(Projectile), "OnHit")]
    [HarmonyPrefix]
    private static void OnHit(Projectile __instance, object[] __args)
    {
        if (!ProjectileCardDriver.Probe || __instance.GetComponent<ProjectileCardDriver>() == null)
            return;
        var parts = new System.Collections.Generic.List<string>();
        foreach (var a in __args)
        {
            if (a is Collider c)
                parts.Add($"collider '{c.name}' (root '{c.transform.root.name}', layer {LayerMask.LayerToName(c.gameObject.layer)})");
            else
                parts.Add(a?.ToString() ?? "null");
        }

        Jotunn.Logger.LogInfo("[probe] OnHit " + string.Join(" | ", parts));
    }
}

/// <summary>A card in the hand (equipped) gets its face from the item's variant too.</summary>
[HarmonyPatch]
internal static class CardHeldFacePatch
{
    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.AttachItem))]
    [HarmonyPostfix]
    private static void Attached(int variant, GameObject __result)
    {
        if (__result == null)
            return;
        var visual = __result.GetComponentInChildren<CardFaceVisual>(true);
        if (visual != null)
            visual.Set(variant);
    }
}

/// <summary>Shows "Seven of Spades" for a card item instead of the generic item name.</summary>
internal sealed class CardNameLayer : DrakeModsLibs.API.IItemDisplayNameLayer
{
    public int Priority => 50;

    public bool TryApply(ItemDrop.ItemData? item, ref string displayName)
    {
        if (item?.m_dropPrefab == null)
            return false;
        var prefab = item.m_dropPrefab.name;
        if (prefab == Deck.DeckData.DeckPrefab)
        {
            displayName = Deck.DeckData.DeckName + " (" + CardBacks.Names[CardBacks.Clamp(item.m_variant)] + ")";
            return true;
        }

        if (!CardLib.IsVariantCardPrefab(prefab))
            return false;
        displayName = CardLib.NameOf(item.m_variant);
        return true;
    }
}
