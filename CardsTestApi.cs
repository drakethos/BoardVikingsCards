using System.Linq;
using Cards.Deck;
using Cards.Target;
using Cards.Util;
using UnityEngine;

namespace Cards;

/// <summary>
/// Entry points for TestLab "invoke" steps: give a card by its code, measure a throw, check icon proportions. Public static,
/// strings and numbers only.
/// </summary>
public static class CardsTestApi
{
    private static ItemDrop.ItemData? Make(string prefab, string code)
    {
        var go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
        var drop = go != null ? go.GetComponent<ItemDrop>() : null;
        if (drop == null)
            return null;
        var data = drop.m_itemData.Clone();
        data.m_dropPrefab = go;
        data.m_stack = 1;
        data.m_quality = 1;
        data.m_variant = CardLib.IndexOf(code);
        return data;
    }

    /// <summary>kind: "throw" or "play". True when the card landed in the inventory with the right face.</summary>
    public static bool TestGive(string code, string kind)
    {
        var player = Player.m_localPlayer;
        var data = Make(kind == "play" ? CardLib.PlayingPrefab : CardLib.ThrowingPrefab, code);
        if (player == null || data == null || !player.GetInventory().AddItem(data))
            return false;
        return data.m_variant == CardLib.IndexOf(code);
    }

    /// <summary>How many cards have hit the nearest archery target (-1: none there).</summary>
    public static int TestTargetHits() => CardTargets.Nearest() is { } t ? CardTargets.Hits(t) : -1;

    /// <summary>The nearest archery target's total score.</summary>
    public static int TestTargetScore() => CardTargets.Nearest() is { } t ? CardTargets.Score(t) : -1;

    /// <summary>Faces stuck in the nearest target, as card codes, in the order they hit.</summary>
    public static string TestTargetFaces()
    {
        var nview = CardTargets.Nearest() is { } t ? CardTargets.NviewOf(t) : null;
        if (nview == null || !nview.IsValid())
            return "";
        return string.Join(",", CardTargets.Faces(nview.GetZDO()).Select(CardLib.CodeOf));
    }

    /// <summary>Look straight at the nearest target's bullseye (like aiming with the mouse). Returns the distance in meters, or -1.</summary>
    public static float TestAimAtTarget()
    {
        var player = Player.m_localPlayer;
        var target = CardTargets.Nearest();
        var center = target != null ? CardTargets.CenterOf(target) : null;
        if (player == null || center == null)
            return -1f;
        var eye = player.GetEyePoint();
        var direction = (center.Value - eye).normalized;
        player.SetLookDir(direction, 0f);
        if (ProjectileCardDriver.Probe)
            Jotunn.Logger.LogInfo($"[probe] aim from {eye} at {center.Value}, looking {direction}, player faces {player.transform.forward}");
        return Mathf.Round(Vector3.Distance(eye, center.Value) * 10f) / 10f;
    }

    /// <summary>How far the nearest target's bullseye is above the ground (-99 when there is none).</summary>
    public static float TestTargetHeight()
    {
        var target = CardTargets.Nearest();
        var center = target != null ? CardTargets.CenterOf(target) : null;
        if (center == null || ZoneSystem.instance == null)
            return -99f;
        return Mathf.Round((center.Value.y - ZoneSystem.instance.GetGroundHeight(center.Value)) * 100f) / 100f;
    }

    /// <summary>Removes every throwing card lying around (so a test counts only its own).</summary>
    public static int TestClearThrown()
    {
        int n = 0;
        foreach (var drop in Object.FindObjectsOfType<ItemDrop>().Where(d => d.m_itemData?.m_dropPrefab != null && d.m_itemData.m_dropPrefab.name == CardLib.ThrowingPrefab).ToList())
        {
            ZNetScene.instance.Destroy(drop.gameObject);
            n++;
        }

        return n;
    }

    /// <summary>Faces (card codes, sorted, comma separated) of throwing cards lying within a radius of the nearest target.</summary>
    public static string TestNearbyFaces(float radius)
    {
        var target = CardTargets.Nearest();
        if (target == null)
            return "";
        return string.Join(",", Object.FindObjectsOfType<ItemDrop>()
            .Where(d => d.m_itemData?.m_dropPrefab != null && d.m_itemData.m_dropPrefab.name == CardLib.ThrowingPrefab)
            .Where(d => (d.transform.position - target.transform.position).magnitude <= radius)
            .Select(d => CardLib.CodeOf(d.m_itemData.m_variant))
            .OrderBy(c => c, System.StringComparer.Ordinal));
    }

    /// <summary>Does the archery target in the game list the throwing card as ammo?</summary>
    public static bool TestTargetAcceptsCards() => CardTargets.Nearest() is { } t && CardTargets.AcceptsCards(t);

    /// <summary>Gives a deck in a back design (0-14).</summary>
    public static bool TestGiveDeck(int back)
    {
        var player = Player.m_localPlayer;
        var data = Make(DeckData.DeckPrefab, "CA");
        if (player == null || data == null)
            return false;
        data.m_variant = back;
        return player.GetInventory().AddItem(data);
    }

    /// <summary>The back design of the first card of that kind in the inventory (-1 none).</summary>
    public static int TestCarriedBack(string kind)
    {
        var player = Player.m_localPlayer;
        if (player == null)
            return -1;
        var prefab = kind == "play" ? CardLib.PlayingPrefab : CardLib.ThrowingPrefab;
        var item = player.GetInventory().GetAllItems().FirstOrDefault(i => i.m_dropPrefab != null && i.m_dropPrefab.name == prefab);
        return item == null ? -1 : CardBacks.BackOf(item);
    }

    /// <summary>Name the game shows for the first deck in the inventory.</summary>
    public static string TestDeckName()
    {
        var player = Player.m_localPlayer;
        var item = player?.GetInventory().GetAllItems().FirstOrDefault(DeckData.IsDeck);
        return item == null ? "" : CardNames.Display(item) ?? "";
    }

    /// <summary>How many deck variant icons are square (one per back design).</summary>
    public static int TestDeckIcons()
    {
        var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(DeckData.DeckPrefab) : null;
        var icons = prefab != null ? prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons : null;
        return icons == null ? -1 : icons.Count(i => i != null && Mathf.Abs(i.rect.width - i.rect.height) < 0.5f);
    }

    /// <summary>Set the paper brightness (as the config does) and return what is in effect.</summary>
    public static float TestSetBrightness(float value)
    {
        CardMesh.SetBrightness(value);
        return Mathf.Round(CardMesh.Brightness * 100f) / 100f;
    }

    /// <summary>Gives a throwing card and holds it.</summary>
    /// <summary>Log the flight of the next thrown card to the BepInEx log.</summary>
    public static bool TestProbeThrow(bool on)
    {
        ProjectileCardDriver.Probe = on;
        return on;
    }

    public static bool TestEquipThrowing(string code)
    {
        var player = Player.m_localPlayer;
        var data = Make(CardLib.ThrowingPrefab, code);
        if (player == null || data == null || !player.GetInventory().AddItem(data))
            return false;
        player.EquipItem(data, true);
        return true;
    }

    /// <summary>The face (code) of the first throwing/playing card in the inventory of that kind, or "".</summary>
    public static string TestCarriedFace(string kind)
    {
        var player = Player.m_localPlayer;
        if (player == null)
            return "";
        var prefab = kind == "play" ? CardLib.PlayingPrefab : CardLib.ThrowingPrefab;
        var item = player.GetInventory().GetAllItems().FirstOrDefault(i => i.m_dropPrefab != null && i.m_dropPrefab.name == prefab);
        return item != null ? CardLib.CodeOf(item.m_variant) : "";
    }

    /// <summary>The name the game shows for the first card of that kind in the inventory.</summary>
    public static string TestCarriedName(string kind)
    {
        var player = Player.m_localPlayer;
        if (player == null)
            return "";
        var prefab = kind == "play" ? CardLib.PlayingPrefab : CardLib.ThrowingPrefab;
        var item = player.GetInventory().GetAllItems().FirstOrDefault(i => i.m_dropPrefab != null && i.m_dropPrefab.name == prefab);
        return item == null ? "" : CardNames.Display(item) ?? "";
    }

    private static ItemDrop? NearestLandedThrowing()
    {
        var player = Player.m_localPlayer;
        if (player == null)
            return null;
        return Object.FindObjectsOfType<ItemDrop>()
            .Where(d => d.m_itemData?.m_dropPrefab != null && d.m_itemData.m_dropPrefab.name == CardLib.ThrowingPrefab)
            .OrderBy(d => (d.transform.position - player.transform.position).sqrMagnitude)
            .FirstOrDefault();
    }

    /// <summary>Flat distance in meters from the player to the nearest landed throwing card, or -1.</summary>
    public static float TestThrowDistance()
    {
        var player = Player.m_localPlayer;
        var landed = NearestLandedThrowing();
        if (player == null || landed == null)
            return -1f;
        var d = landed.transform.position - player.transform.position;
        d.y = 0f;
        return Mathf.Round(d.magnitude * 10f) / 10f;
    }

    /// <summary>The face (code) of the landed card, to prove the thrown card keeps its face.</summary>
    public static string TestLandedFace()
    {
        var landed = NearestLandedThrowing();
        return landed != null ? CardLib.CodeOf(landed.m_itemData.m_variant) : "";
    }

    /// <summary>How many of the item's variant icons are exactly square (a card is centred with padding, never stretched).</summary>
    public static int TestSquareIcons(string kind)
    {
        var prefab = ObjectDB.instance != null
            ? ObjectDB.instance.GetItemPrefab(kind == "play" ? CardLib.PlayingPrefab : CardLib.ThrowingPrefab)
            : null;
        var icons = prefab != null ? prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons : null;
        return icons == null ? -1 : icons.Count(i => i != null && Mathf.Abs(i.rect.width - i.rect.height) < 0.5f);
    }

    private static Vector3? CardSize()
    {
        var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(CardLib.ThrowingPrefab) : null;
        var front = prefab != null ? prefab.transform.Find("attach/front") : null;
        var mesh = front != null ? front.GetComponent<MeshFilter>()?.sharedMesh : null;
        return mesh != null ? mesh.bounds.size : (Vector3?)null;
    }

    /// <summary>Width of the held/ground card relative to its height (a real card is about 0.7).</summary>
    public static float TestCardShape() =>
        CardSize() is { } s && s.y > 0f ? Mathf.Round(s.x / s.y * 100f) / 100f : -1f;

    /// <summary>Tallest side of the card as held, in meters (smaller is easier to hold).</summary>
    public static float TestHeldSize() =>
        CardSize() is { } s ? Mathf.Round(Mathf.Max(s.x, s.y) * 100f) / 100f : -1f;
}
