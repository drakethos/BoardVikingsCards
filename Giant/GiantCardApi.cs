using System.Linq;
using UnityEngine;

namespace Cards.Giant;

/// <summary>String/number entry points for TestLab "invoke" steps, acting on the giant card nearest the player.</summary>
public static class GiantCardApi
{
    private static GiantCard? Nearest()
    {
        var player = Player.m_localPlayer;
        if (player == null)
            return null;
        return Object.FindObjectsOfType<GiantCard>()
            .Where(c => c.Face != null && c.GetComponent<ZNetView>() is { } z && z.IsValid())
            .OrderBy(c => (c.transform.position - player.transform.position).sqrMagnitude)
            .FirstOrDefault();
    }

    public static string TestFace() => Nearest()?.Face ?? "";

    public static bool TestSetFace(string code)
    {
        var card = Nearest();
        if (card == null)
            return false;
        card.SetFace(code);
        return true;
    }

    public static int TestBack() => Nearest()?.BackDesign ?? -1;

    public static bool TestSetBack(int index)
    {
        var card = Nearest();
        if (card == null)
            return false;
        card.SetBackDesign(index);
        return true;
    }

    /// <summary>The back a card placed next would get.</summary>
    public static int TestNextBack() => GiantCard.NextBack;

    /// <summary>Tilt applied to the next placement, in degrees.</summary>
    public static float TestSetTilt(float degrees)
    {
        GiantCardPlacement.ForceBuiltIn = degrees != 0f;
        GiantCardPlacement.Pitch = 0f;
        GiantCardPlacement.AddTilt(degrees);
        return GiantCardPlacement.Pitch;
    }

    /// <summary>How far the nearest card leans from upright, in whole degrees.</summary>
    public static int TestLean()
    {
        var card = Nearest();
        return card == null ? -1 : Mathf.RoundToInt(Vector3.Angle(card.transform.up, Vector3.up));
    }

    /// <summary>How far an orientation made from this yaw and tilt leans from upright (the math the placement uses).</summary>
    public static int TestOrientationLean(float yaw, float tilt) =>
        Mathf.RoundToInt(Vector3.Angle(GiantCardPlacement.Orientation(yaw, tilt) * Vector3.up, Vector3.up));

    /// <summary>The yaw the card still faces after tilting (so tilting never turns it).</summary>
    public static int TestOrientationYaw(float yaw, float tilt)
    {
        var forward = GiantCardPlacement.Orientation(yaw, tilt) * Vector3.forward;
        forward.y = 0f;
        return Mathf.RoundToInt(Quaternion.LookRotation(forward).eulerAngles.y);
    }

    /// <summary>True when the build-ghost patches that apply the tilt are active.</summary>
    public static bool TestTiltPatched()
    {
        var info = HarmonyLib.Harmony.GetPatchInfo(HarmonyLib.AccessTools.Method(typeof(Player), "UpdatePlacementGhost"));
        return info != null && info.Postfixes.Any(p => p.owner == Cards.GUID);
    }

    public static bool TestOpenPicker()
    {
        var card = Nearest();
        if (card == null)
            return false;
        GiantCardPicker.Open(card);
        return true;
    }
}
