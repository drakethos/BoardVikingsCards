using System.Collections.Generic;
using UnityEngine;

namespace Cards.Util;

public static class SpriteTools
{
    private static Dictionary<string, Sprite>? _cache;

    /// <summary>Warm the sprite cache from the loaded card bundle. Safe to call more than once.</summary>
    public static void EnsureLoaded()
    {
        if (_cache != null)
            return;

        _cache = new Dictionary<string, Sprite>();

        if (Loader.CardBundle == null)
        {
            Jotunn.Logger.LogError("Cannot load card sprites — AssetBundle is null.");
            return;
        }

        foreach (var sprite in Loader.CardBundle.LoadAllAssets<Sprite>())
        {
            if (sprite != null && !_cache.ContainsKey(sprite.name))
                _cache[sprite.name] = sprite;
        }

        Jotunn.Logger.LogInfo($"Loaded {_cache.Count} card sprites from asset bundle.");
    }

    public static Sprite? GetSprite(string spriteName)
    {
        EnsureLoaded();

        if (_cache != null && _cache.TryGetValue(spriteName, out var sprite))
            return sprite;

        Jotunn.Logger.LogWarning($"Sprite '{spriteName}' not found in cache.");
        return null;
    }

    /// <summary>Lookup without warning (optional backs, aliases).</summary>
    public static Sprite? TryGetSprite(string spriteName)
    {
        EnsureLoaded();
        if (_cache != null && _cache.TryGetValue(spriteName, out var sprite))
            return sprite;
        return null;
    }
}
