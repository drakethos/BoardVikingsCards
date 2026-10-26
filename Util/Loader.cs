using System.IO;
using Jotunn.Utils;
using UnityEngine;

namespace Cards.Util;

public static class Loader
{
    public static AssetBundle? CardBundle { get; private set; }

    /// <summary>
    /// Deploy folder must match Thunderstore/r2modman style: DrakeMods-BoardVikings-CardGames.
    /// Falls back to older folder names if present.
    /// </summary>
    public static void AddBundles()
    {
        string[] candidateFolders =
        {
            "DrakeMods-BoardVikings-CardGames",
            "DrakeMod-BoardVikings-CardGames",
            "BoardVikings-CardGames"
        };

        foreach (string folder in candidateFolders)
        {
            string relative = Path.Combine(folder, "Assets", "card");
            CardBundle = AssetUtils.LoadAssetBundle(relative);
            if (CardBundle != null)
            {
                Jotunn.Logger.LogInfo($"Loaded card AssetBundle from plugins/{relative}");
                return;
            }
        }

        string assemblyDir = Path.GetDirectoryName(typeof(Loader).Assembly.Location) ?? string.Empty;
        string besideDll = Path.Combine(assemblyDir, "Assets", "card");
        if (File.Exists(besideDll))
        {
            CardBundle = AssetBundle.LoadFromFile(besideDll);
            if (CardBundle != null)
            {
                Jotunn.Logger.LogInfo($"Loaded card AssetBundle from {besideDll}");
                return;
            }
        }

        Jotunn.Logger.LogError(
            "Failed to load card AssetBundle. Expected Assets/card under DrakeMods-BoardVikings-CardGames.");
    }

    /// <summary>Path of a loose asset file shipped with the mod (plugin folder first, then beside the DLL), or null.</summary>
    public static string? FindAsset(string fileName)
    {
        string[] candidateFolders =
        {
            "DrakeMods-BoardVikings-CardGames",
            "DrakeMod-BoardVikings-CardGames",
            "BoardVikings-CardGames"
        };

        foreach (string folder in candidateFolders)
        {
            string path = Path.Combine(BepInEx.Paths.PluginPath, folder, "Assets", fileName);
            if (File.Exists(path))
                return path;
        }

        string assemblyDir = Path.GetDirectoryName(typeof(Loader).Assembly.Location) ?? string.Empty;
        string beside = Path.Combine(assemblyDir, "Assets", fileName);
        return File.Exists(beside) ? beside : null;
    }

    public static GameObject? LoadPrefab(string prefabName, AssetBundle? bundle)
    {
        if (bundle == null)
            return null;

        var prefab = bundle.LoadAsset<GameObject>(prefabName);
        if (prefab == null)
            Jotunn.Logger.LogError($"Failed to load prefab: {prefabName}");
        else
            Jotunn.Logger.LogInfo($"Loaded prefab: {prefabName}");

        return prefab;
    }
}
