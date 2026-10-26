using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cards.Util;

/// <summary>
/// Card items use a Material donor (LeatherScraps) for stable drop/pickup physics. Throwing cards then gain the vanilla spear
/// throw (same attack and flight as a spear, so aiming works the way players expect) via a shared projectile that respawns the
/// card on hit. The giant card piece clones vanilla "sign" and stays upright.
/// </summary>
internal static class CardItemSanitize
{
    internal enum Kind
    {
        /// <summary>Throwable card, face = variant.</summary>
        Throwing,
        /// <summary>Card out of a deck, face = variant. Not a weapon.</summary>
        Playing,
        /// <summary>The deck box.</summary>
        Deck
    }

    /// <summary>Giant card size in meters.</summary>
    internal static readonly Vector2 PieceSize = new Vector2(1.39f, 1.9f);

    /// <summary>A card in the hand or on the ground. Real playing cards are small.</summary>
    internal static readonly Vector2 ItemSize = new Vector2(0.18f, 0.25f);

    /// <summary>In flight, a little larger so the throw reads.</summary>
    internal static readonly Vector2 ProjectileSize = new Vector2(0.22f, 0.30f);

    /// <summary>The deck box lying on the ground or in a hand.</summary>
    internal static readonly Vector2 DeckSize = new Vector2(0.24f, 0.24f);

    private const float ItemClearance = 0.02f;
    private const int ItemLayer = 10;
    private const string SharedProjectileName = "Card_SharedProjectile";

    private static readonly string[] SpearItems = { "SpearFlint", "SpearBronze", "SpearCarapace", "SpearChitin" };

    private static Sprite? _fallbackBack;
    private static GameObject? _sharedProjectile;

    private static readonly HashSet<string> StripBehaviourNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "Sign", "Container", "ItemDrop", "Smelter", "Fireplace", "TerrainModifier"
    };

    // ---------------------------------------------------------------- vanilla spear: projectile prefab and throw attack

    private static ItemDrop.ItemData.SharedData? SpearShared(string item)
    {
        var go = PrefabManager.Instance.GetPrefab(item);
        return go != null ? go.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
    }

    /// <summary>Projectile prefab names read from the real spear items, so a renamed prefab can't break throwing.</summary>
    private static IEnumerable<string> ProjectileDonorNames()
    {
        foreach (var item in SpearItems)
        {
            var shared = SpearShared(item);
            var proj = shared?.m_secondaryAttack?.m_attackProjectile;
            if (proj == null)
                proj = shared?.m_attack?.m_attackProjectile;
            if (proj != null && proj.GetComponent<Projectile>() != null)
                yield return proj.name;
        }
    }

    /// <summary>A copy of the first spear's throw, so the flight (speed, gravity, aim) is the spear's own.</summary>
    private static Attack? SpearThrow()
    {
        foreach (var item in SpearItems)
        {
            var shared = SpearShared(item);
            var source = shared?.m_secondaryAttack;
            if (source == null || source.m_attackProjectile == null)
                continue;
            var copy = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(source, null) as Attack;
            if (copy != null)
                return copy;
        }

        return null;
    }

    /// <summary>Build one networked projectile used by every throwing card.</summary>
    internal static GameObject? EnsureSharedProjectile()
    {
        if (_sharedProjectile != null)
            return _sharedProjectile;

        GameObject? clone = null;
        string? used = null;
        foreach (var donor in ProjectileDonorNames())
        {
            clone = PrefabManager.Instance.CreateClonedPrefab(SharedProjectileName, donor);
            if (clone != null)
            {
                used = donor;
                break;
            }
        }

        if (clone == null)
        {
            Jotunn.Logger.LogError("Could not clone a vanilla spear projectile — cards will not be throwable.");
            return null;
        }

        clone.SetActive(true);

        var projectile = clone.GetComponent<Projectile>();
        if (projectile == null)
        {
            Jotunn.Logger.LogError($"Cloned '{used}' has no Projectile component.");
            return null;
        }

        // Keep the spear's own flight (speed, gravity, drag) so a throw goes where you aim. Only the landing changes:
        // the card drops back as an item and does no damage.
        projectile.m_respawnItemOnHit = true;
        projectile.m_spawnOnHit = null;
        SetField(projectile, "m_spawnOnTtl", false);
        projectile.m_damage = new HitData.DamageTypes();

        StripDonorChildren(clone);
        StripVfx(clone);

        var face = SpriteTools.TryGetSprite(CardLib.All[0].SpriteName);
        BuildCardVisual(clone, face, ProjectileSize, flatOnGround: true, lift: 0f, variantFace: true);
        clone.AddComponent<ProjectileCardDriver>();

        // No collider at all: the game's flight check would hit the card's own collider on the first frame and land it at your feet.
        foreach (var col in clone.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(col);

        // The projectile moves itself (position += velocity every physics step). A rigidbody that also falls under gravity would
        // pull the card through the ground, so it stays kinematic with no gravity.
        var rb = clone.GetComponent<Rigidbody>();
        if (rb == null)
            rb = clone.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        var znet = clone.GetComponent<ZNetView>();
        if (znet == null)
        {
            znet = clone.AddComponent<ZNetView>();
            znet.m_persistent = false;
        }

        PrefabManager.Instance.AddPrefab(clone);
        _sharedProjectile = clone;
        Jotunn.Logger.LogInfo($"Card throw projectile ready (donor '{used}').");
        return _sharedProjectile;
    }

    // ---------------------------------------------------------------- items

    internal static void SanitizeAndBuildItem(
        CustomItem customItem,
        Kind kind,
        string displayName,
        string description,
        Sprite? face,
        int variants)
    {
        var prefab = customItem.ItemPrefab;
        var drop = customItem.ItemDrop;
        if (prefab == null || drop?.m_itemData?.m_shared == null)
            return;

        prefab.SetActive(true);
        prefab.transform.localScale = Vector3.one;

        var znet = prefab.GetComponent<ZNetView>();
        if (znet != null)
            znet.m_syncInitialScale = true;

        var shared = drop.m_itemData.m_shared;
        shared.m_name = displayName;
        shared.m_description = description;
        shared.m_itemType = kind == Kind.Throwing
            ? ItemDrop.ItemData.ItemType.OneHandedWeapon
            : ItemDrop.ItemData.ItemType.Misc;
        shared.m_maxStackSize = 1;
        shared.m_autoStack = false;
        shared.m_weight = kind == Kind.Deck ? 0.5f : 0.05f;
        shared.m_maxQuality = 1;
        shared.m_useDurability = false;
        shared.m_teleportable = true;
        shared.m_questItem = false;
        shared.m_value = 0;
        shared.m_food = 0f;
        shared.m_foodStamina = 0f;
        shared.m_foodEitr = 0f;
        shared.m_movementModifier = 0f;
        shared.m_equipDuration = 0.2f;
        shared.m_variants = variants;
        shared.m_attachOverride = ItemDrop.ItemData.ItemType.None;
        shared.m_armor = 0f;
        shared.m_armorPerLevel = 0f;
        shared.m_blockPower = 0f;
        shared.m_deflectionForce = 0f;
        shared.m_attackForce = 5f;
        shared.m_backstabBonus = 1f;
        shared.m_dodgeable = true;
        shared.m_blockable = true;
        shared.m_ammoType = string.Empty;
        shared.m_buildPieces = null;
        shared.m_spawnOnHit = null;
        shared.m_spawnOnHitTerrain = null;
        shared.m_damages = new HitData.DamageTypes();
        shared.m_damagesPerLevel = new HitData.DamageTypes();

        if (kind == Kind.Throwing)
        {
            shared.m_skillType = Skills.SkillType.Spears;
            shared.m_animationState = ItemDrop.ItemData.AnimationState.OneHanded;
            var projectile = EnsureSharedProjectile();
            shared.m_attack = BuildThrow(projectile, stamina: 8f);
            shared.m_secondaryAttack = BuildThrow(projectile, stamina: 8f);
        }
        else
        {
            shared.m_skillType = Skills.SkillType.None;
        }

        drop.m_autoPickup = true;
        drop.m_autoDestroy = false;
        drop.m_itemData.m_stack = 1;
        drop.m_itemData.m_durability = 100f;
        drop.m_itemData.m_quality = 1;
        drop.m_itemData.m_variant = 0;
        drop.m_itemData.m_dropPrefab = prefab;

        var rb = prefab.GetComponent<Rigidbody>();
        if (rb != null)
            rb.mass = 0.15f;

        StripDonorChildren(prefab);
        StripVfx(prefab);

        bool isDeck = kind == Kind.Deck;
        var size = isDeck ? DeckSize : ItemSize;
        BuildCardVisual(prefab, face, size, flatOnGround: true, ItemClearance, visualRootName: "attach",
            variantFace: true, backOverride: isDeck ? face : null, deckBox: isDeck);
        prefab.AddComponent<CardItemDriver>();
        SetLayerRecursive(prefab, ItemLayer);
    }

    /// <summary>The spear's throw with our card projectile; a plain projectile attack if no spear can be found.</summary>
    private static Attack BuildThrow(GameObject? projectile, float stamina)
    {
        var attack = SpearThrow();
        bool fromSpear = attack != null;
        if (attack == null)
        {
            attack = new Attack
            {
                m_attackType = Attack.AttackType.Projectile,
                m_attackAnimation = "spear_throw",
                m_projectileVel = 30f,
                m_projectileVelMin = 2f,
                m_attackRange = 1.9f,
                m_attackHeight = 1.5f,
                m_attackStartNoise = 5f,
                m_attackHitNoise = 10f,
                m_projectileAccuracy = 1f,
                m_projectileAccuracyMin = 10f,
                m_useCharacterFacing = true,
                m_useCharacterFacingYAim = true
            };
        }

        attack.m_attackProjectile = projectile;
        // A tap must throw at full speed: with a different minimum the card is only tossed a few feet.
        attack.m_projectileVelMin = attack.m_projectileVel;
        attack.m_projectileAccuracyMin = attack.m_projectileAccuracy;
        Jotunn.Logger.LogInfo($"Card throw: velocity {attack.m_projectileVel}, accuracy {attack.m_projectileAccuracy}, " +
                              $"animation {attack.m_attackAnimation} ({(fromSpear ? "copied from a spear" : "built from scratch")}).");
        attack.m_consumeItem = true;
        attack.m_attackStamina = stamina;
        return attack;
    }

    // ---------------------------------------------------------------- the giant card piece

    internal static void BuildCardPiece(GameObject pieceRoot, Sprite? face, string displayName)
    {
        if (pieceRoot == null)
            return;

        pieceRoot.SetActive(true);

        var toDestroy = new List<Object>(48);
        foreach (var component in pieceRoot.GetComponentsInChildren<Component>(true))
        {
            if (component == null)
                continue;

            switch (component)
            {
                case SpriteRenderer:
                    continue;
                case Renderer renderer:
                    renderer.enabled = false;
                    break;
                case Collider:
                case Rigidbody:
                case ZSyncTransform:
                case TerrainModifier:
                    toDestroy.Add(component);
                    break;
                case Canvas canvas:
                    toDestroy.Add(canvas.gameObject);
                    break;
                case TMPro.TMP_Text tmp:
                    toDestroy.Add(tmp.gameObject);
                    break;
                case MonoBehaviour mb when StripBehaviourNames.Contains(mb.GetType().Name):
                    toDestroy.Add(mb);
                    break;
            }
        }

        for (int i = 0; i < toDestroy.Count; i++)
        {
            if (toDestroy[i] != null)
                Object.DestroyImmediate(toDestroy[i]);
        }

        var doomed = new List<GameObject>();
        foreach (Transform child in pieceRoot.transform)
        {
            if (child.name.StartsWith("card_", StringComparison.OrdinalIgnoreCase))
                continue;
            if (child.GetComponent<MeshFilter>() != null ||
                child.GetComponentInChildren<MeshFilter>(true) != null)
                doomed.Add(child.gameObject);
        }

        foreach (var go in doomed)
            Object.DestroyImmediate(go);

        var netView = pieceRoot.GetComponent<ZNetView>();
        if (netView == null)
        {
            netView = pieceRoot.AddComponent<ZNetView>();
            netView.m_persistent = true;
            netView.m_type = ZDO.ObjectType.Default;
        }

        netView.enabled = true;
        netView.m_syncInitialScale = true;

        // Origin = middle of the bottom edge: the card stands on the ground and tilts around that edge.
        BuildCardVisual(pieceRoot, face, PieceSize, flatOnGround: false, lift: PieceSize.y * 0.5f);

        var piece = pieceRoot.GetComponent<Piece>();
        if (piece != null)
        {
            piece.m_name = displayName;
            piece.m_groundPiece = false;
            piece.m_groundOnly = false;
            piece.m_waterPiece = false;
            piece.m_noInWater = true;
            piece.m_clipGround = true;
            piece.m_clipEverything = true;
            piece.m_allowAltGroundPlacement = true;
            piece.m_noClipping = false;
            piece.m_notOnTiltingSurface = false;
            SetField(piece, "m_canRotate", true);
            piece.m_canBeRemoved = true;
        }

        var wnt = pieceRoot.GetComponent<WearNTear>();
        if (wnt != null)
        {
            wnt.m_autoCreateFragments = false;
            wnt.m_fragmentRoots = Array.Empty<GameObject>();
            wnt.m_destroyedEffect = new EffectList();
            wnt.m_hitEffect = new EffectList();
            wnt.m_noSupportWear = true;
            wnt.m_noRoofWear = true;
            wnt.m_supports = true;
            wnt.m_health = 200f;
        }

        var layer = LayerMask.NameToLayer("piece");
        if (layer < 0)
            layer = 10;
        SetLayerRecursive(pieceRoot, layer);
        pieceRoot.SetActive(true);
    }

    // ---------------------------------------------------------------- visuals

    private static void BuildCardVisual(
        GameObject root,
        Sprite? face,
        Vector2 size,
        bool flatOnGround,
        float lift,
        string visualRootName = "card_visual",
        bool variantFace = false,
        Sprite? backOverride = null,
        bool deckBox = false)
    {
        foreach (var col in root.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(col);

        var box = root.AddComponent<BoxCollider>();
        if (flatOnGround)
        {
            box.center = new Vector3(0f, lift, 0f);
            box.size = new Vector3(size.x, 0.025f, size.y);
        }
        else
        {
            box.center = new Vector3(0f, lift, 0f);
            box.size = new Vector3(size.x, size.y, 0.04f);
        }

        var attachGo = new GameObject(visualRootName);
        var attach = attachGo.transform;
        attach.SetParent(root.transform, false);
        attach.localPosition = new Vector3(0f, lift, 0f);
        attach.localRotation = flatOnGround ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
        attach.localScale = Vector3.one;

        var back = backOverride
                   ?? SpriteTools.TryGetSprite("back")
                   ?? GetFallbackBackSprite();

        AddSpriteFace(attach, "front", face, size);
        AddSpriteFace(attach, "back", back, size);

        if (variantFace)
        {
            var visual = attachGo.AddComponent<CardFaceVisual>();
            visual.Box = size;
            visual.Deck = deckBox;
        }
    }

    private static void AddSpriteFace(Transform parent, string name, Sprite? sprite, Vector2 targetSize)
    {
        var face = CardMesh.MakeFace(name, parent, Vector3.zero);
        CardMesh.Apply(face, sprite, targetSize);
    }

    /// <summary>Shows a sprite on one side of a card, as large as fits in <paramref name="box"/>, never stretched.</summary>
    internal static void ApplySprite(GameObject face, Sprite? sprite, Vector2 box) => CardMesh.Apply(face, sprite, box);

    private static Sprite GetFallbackBackSprite()
    {
        if (_fallbackBack != null)
            return _fallbackBack;

        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "CardBackFallback",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var color = new Color(0.45f, 0.08f, 0.1f, 1f);
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply(false, false);
        _fallbackBack = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        return _fallbackBack;
    }

    /// <summary>Fields the store (pre-1.0) stubs lack but the live game has; reflection compiles against both.</summary>
    private static void SetField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            Jotunn.Logger.LogWarning($"{target.GetType().Name}.{name} not found in this Valheim version.");
            return;
        }

        field.SetValue(target, value);
    }

    private static void StripDonorChildren(GameObject prefab)
    {
        var doomed = new List<GameObject>();
        foreach (Transform child in prefab.transform)
            doomed.Add(child.gameObject);
        foreach (var go in doomed)
            Object.DestroyImmediate(go);

        foreach (var col in prefab.GetComponents<Collider>())
            Object.DestroyImmediate(col);

        var mf = prefab.GetComponent<MeshFilter>();
        if (mf != null)
            Object.DestroyImmediate(mf);
        var mr = prefab.GetComponent<MeshRenderer>();
        if (mr != null)
            Object.DestroyImmediate(mr);
    }

    /// <summary>Never destroys a GameObject: a particle system on the prefab root would take the whole prefab with it.</summary>
    private static void StripVfx(GameObject prefab)
    {
        foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps == null)
                continue;
            var psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
                Object.DestroyImmediate(psr);
            Object.DestroyImmediate(ps);
        }

        foreach (var trail in prefab.GetComponentsInChildren<TrailRenderer>(true))
        {
            if (trail != null)
                Object.DestroyImmediate(trail);
        }
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        var transform = go.transform;
        for (int i = 0, count = transform.childCount; i < count; i++)
            SetLayerRecursive(transform.GetChild(i).gameObject, layer);
    }
}
