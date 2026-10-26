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
