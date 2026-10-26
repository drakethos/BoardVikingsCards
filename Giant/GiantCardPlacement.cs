using System.Linq;
using DrakeModsLibs.UI;
using HarmonyLib;
using UnityEngine;

namespace Cards.Giant;

/// <summary>
/// Free tilt for giant cards while building: hold the tilt key (Ctrl by default) and scroll. The card leans around its bottom
/// edge, in the direction it faces, so two cards leaning on each other make a house of cards. The tilt stays until you change it.
/// </summary>
[HarmonyPatch]
internal static class GiantCardPlacement
{
    internal const float MaxTilt = 85f;

    /// <summary>Test hook: use the built-in tilt even though a rotation mod is installed.</summary>
    internal static bool ForceBuiltIn;

    private static bool? _gizmo;

    /// <summary>The Gizmo mod gives free rotation on all axes; when it is installed the built-in tilt stays out of its way.</summary>
    internal static bool GizmoPresent =>
        _gizmo ??= BepInEx.Bootstrap.Chainloader.PluginInfos.Keys
            .Any(k => k.IndexOf("gizmo", System.StringComparison.OrdinalIgnoreCase) >= 0);

    internal static bool BuiltInTilt => ForceBuiltIn || Cards.AlwaysBuiltInTilt || !GizmoPresent;

    /// <summary>Degrees the top leans away from the way the card faces. Applied to every giant card ghost.</summary>
    internal static float Pitch;

    private static readonly System.Reflection.FieldInfo? GhostField = AccessTools.Field(typeof(Player), "m_placementGhost");
    private static readonly System.Reflection.FieldInfo? RotationField = AccessTools.Field(typeof(Player), "m_placeRotation");

    private static int _rotationBefore;
    private static float _lastMessage;

    internal static GameObject? Ghost(Player player)
    {
        var ghost = GhostField?.GetValue(player) as GameObject;
        return ghost != null && ghost.GetComponent<GiantCard>() != null ? ghost : null;
    }

    /// <summary>Card rotation: yaw around the vertical, then lean around the card's own sideways axis.</summary>
    internal static Quaternion Orientation(float yaw, float pitch) =>
        Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(pitch, 0f, 0f);

    internal static void AddTilt(float delta)
    {
        Pitch = Mathf.Clamp(Mathf.Round((Pitch + delta) * 10f) / 10f, -MaxTilt, MaxTilt);
        if (Time.unscaledTime - _lastMessage > 0.15f)
        {
            _lastMessage = Time.unscaledTime;
            DrakeMessage.Show("Tilt " + Pitch.ToString("0.#") + "°", DrakeMessageKind.Info, null, 1.2f);
        }
    }

    [HarmonyPatch(typeof(Player), "UpdatePlacement")]
    [HarmonyPrefix]
    private static void BeforePlacement(Player __instance)
    {
        _rotationBefore = RotationField != null ? (int)RotationField.GetValue(__instance) : 0;
    }

    [HarmonyPatch(typeof(Player), "UpdatePlacement")]
    [HarmonyPostfix]
    private static void AfterPlacement(Player __instance)
    {
        if (!BuiltInTilt || __instance != Player.m_localPlayer || Ghost(__instance) == null)
            return;

        float scroll = Input.mouseScrollDelta.y;
        if (scroll == 0f || !Input.GetKey(Cards.TiltKey))
            return;

        // The scroll belongs to the tilt: give back the turn the game just applied.
        RotationField?.SetValue(__instance, _rotationBefore);
        AddTilt(Mathf.Sign(scroll) * Cards.TiltStep);
    }

    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    [HarmonyPostfix]
    private static void AfterGhost(Player __instance)
    {
        if (!BuiltInTilt)
            return;
        var ghost = Ghost(__instance);
        if (ghost == null)
            return;

        ghost.transform.rotation = Orientation(ghost.transform.eulerAngles.y, Pitch);
    }
}
