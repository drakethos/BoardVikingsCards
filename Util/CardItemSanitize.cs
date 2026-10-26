using System;
using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cards.Util;

/// <summary>
/// Cards use a Material donor (LeatherScraps) for stable drop/pickup physics,
/// then gain spear-style throw via a shared projectile that respawns the card on hit.
/// Décor pieces clone vanilla "sign" and stay upright/wall-placed.
/// </summary>
internal static class CardItemSanitize
{
    /// <summary>Giant décor card size in meters (matches original card_piece collider).</summary>
    internal static readonly Vector2 PieceSize = new Vector2(1.39f, 1.9f);

    /// <summary>World-drop / in-hand card size.</summary>
    internal static readonly Vector2 ItemSize = new Vector2(0.55f, 0.75f);

    /// <summary>In-flight projectile card size.</summary>
    internal static readonly Vector2 ProjectileSize = new Vector2(0.4f, 0.55f);

    private const float ItemClearance = 0.04f;
    private const int ItemLayer = 10;
    private const string SharedProjectileName = "Card_SharedProjectile";

    private static readonly string[] SpearItems = { "SpearFlint", "SpearBronze", "SpearCarapace", "SpearChitin" };

    /// <summary>Projectile prefab names read from the real spear items, so a renamed prefab can't break throwing.</summary>
    private static IEnumerable<string> ProjectileDonorNames()
    {
        foreach (var item in SpearItems)
        {
            var go = PrefabManager.Instance.GetPrefab(item);
            var attack = go != null ? go.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_attack : null;
            var secondary = go != null ? go.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_secondaryAttack : null;
            var proj = secondary?.m_attackProjectile != null ? secondary.m_attackProjectile : attack?.m_attackProjectile;
            if (proj != null && proj.GetComponent<Projectile>() != null)
                yield return proj.name;
        }
    }

    private static Sprite? _fallbackBack;
    private static GameObject? _sharedProjectile;

    private static readonly HashSet<string> StripBehaviourNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "Sign", "Container", "ItemDrop", "Smelter", "Fireplace", "TerrainModifier"
    };

    /// <summary>Build one networked projectile used by every throwable card.</summary>
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

        // Land → ItemDrop.DropItem(thrown card) so it can be picked up again.
        projectile.m_respawnItemOnHit = true;
        projectile.m_ttl = 10f;
        projectile.m_gravity = 6f;
        projectile.m_spawnOnHit = null;
        SetField(projectile, "m_spawnOnTtl", false);
        projectile.m_damage = new HitData.DamageTypes();

        StripDonorChildren(clone);
        StripVfx(clone);

        var face = SpriteTools.TryGetSprite("Joker")
                   ?? SpriteTools.TryGetSprite("CA")
                   ?? SpriteTools.TryGetSprite("back");
        BuildCardVisual(clone, face, ProjectileSize, flatOnGround: false, lift: 0f);

        foreach (var col in clone.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(col);
        var box = clone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(ProjectileSize.x, ProjectileSize.y, 0.05f);
        box.center = Vector3.zero;

        var rb = clone.GetComponent<Rigidbody>();
        if (rb == null)
            rb = clone.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

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

    internal static void SanitizeAndBuildItem(CustomItem customItem, CardInfo info, Sprite? face)
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
        shared.m_name = info.FullName;
        shared.m_description = "A throwable playing card. The " + info.FullName;
        shared.m_itemType = ItemDrop.ItemData.ItemType.OneHandedWeapon;
        shared.m_maxStackSize = 1;
        shared.m_autoStack = false;
        shared.m_weight = 0.1f;
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
        shared.m_variants = 0;
        shared.m_attachOverride = ItemDrop.ItemData.ItemType.None;
        shared.m_skillType = Skills.SkillType.Spears;
        shared.m_animationState = ItemDrop.ItemData.AnimationState.OneHanded;
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

        if (face != null)
            shared.m_icons = new[] { face };

        var projectile = EnsureSharedProjectile();
        shared.m_attack = BuildThrowAttack(projectile, stamina: 8f, vel: 25f);
        shared.m_secondaryAttack = BuildThrowAttack(projectile, stamina: 10f, vel: 20f);

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
        BuildCardVisual(prefab, face, ItemSize, flatOnGround: true, ItemClearance, visualRootName: "attach");
        SetLayerRecursive(prefab, ItemLayer);
    }

    private static Attack BuildThrowAttack(GameObject? projectile, float stamina, float vel)
    {
        return new Attack
        {
            m_attackType = Attack.AttackType.Projectile,
            m_attackAnimation = "spear_throw",
            m_attackProjectile = projectile,
            m_consumeItem = true,
            m_attackStamina = stamina,
            m_projectileVel = vel,
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

        BuildCardVisual(pieceRoot, face, PieceSize, flatOnGround: false, lift: 0f);

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

    private static void BuildCardVisual(
        GameObject root,
        Sprite? face,
        Vector2 size,
        bool flatOnGround,
        float lift,
        string visualRootName = "card_visual")
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
            box.center = Vector3.zero;
            box.size = new Vector3(size.x, size.y, 0.04f);
        }

        var attachGo = new GameObject(visualRootName);
        var attach = attachGo.transform;
        attach.SetParent(root.transform, false);
        attach.localPosition = flatOnGround ? new Vector3(0f, lift, 0f) : Vector3.zero;
        attach.localRotation = flatOnGround ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
        attach.localScale = Vector3.one;

        var back = SpriteTools.TryGetSprite("back")
                   ?? SpriteTools.TryGetSprite("Back")
                   ?? SpriteTools.TryGetSprite("card_back")
                   ?? GetFallbackBackSprite();

        AddSpriteFace(attach, "front", face, Quaternion.identity, new Vector3(0f, 0f, 0.001f), size);
        AddSpriteFace(attach, "back", back, Quaternion.Euler(0f, 180f, 0f), new Vector3(0f, 0f, -0.001f), size);
    }

    private static void AddSpriteFace(
        Transform parent,
        string name,
        Sprite? sprite,
        Quaternion localRot,
        Vector3 localPos,
        Vector2 targetSize)
    {
        var go = new GameObject(name);
        var t = go.transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        t.localRotation = localRot;

        var sr = go.AddComponent<SpriteRenderer>();
        if (sprite != null)
        {
            sr.sprite = sprite;
            var bounds = sprite.bounds.size;
            float sx = bounds.x > 0.0001f ? targetSize.x / bounds.x : 1f;
            float sy = bounds.y > 0.0001f ? targetSize.y / bounds.y : 1f;
            t.localScale = new Vector3(sx, sy, 1f);
        }
        else
        {
            t.localScale = new Vector3(targetSize.x, targetSize.y, 1f);
        }

        sr.sortingOrder = 10;
        go.layer = parent.gameObject.layer;
    }

    /// <summary>Fields the store (pre-1.0) stubs lack but the live game has; reflection compiles against both.</summary>
    private static void SetField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);
        if (field == null)
        {
            Jotunn.Logger.LogWarning($"{target.GetType().Name}.{name} not found in this Valheim version.");
            return;
        }

        field.SetValue(target, value);
    }

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

    private static void StripVfx(GameObject prefab)
    {
        // Never destroy a GameObject here: a particle system on the prefab root would take the whole prefab with it
        // (and leave a destroyed prefab registered in ObjectDB, which breaks the game's own startup).
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
