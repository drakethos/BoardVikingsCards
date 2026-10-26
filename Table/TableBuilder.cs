using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cards.Table;

/// <summary>Materials for the table: Valheim's own wood, and green felt made in code.</summary>
internal static class TableMaterials
{
    private static Material? _wood;
    private static Material? _felt;
    private static Shader? _diffuse;

    private static Shader Diffuse() => _diffuse ??= Shader.Find("Legacy Shaders/Diffuse") ?? Shader.Find("Sprites/Default");

    internal static Material Flat(Color color)
    {
        var material = new Material(Diffuse()) { name = "CardTableFlat" };
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        return material;
    }

    /// <summary>Wood: the material of a vanilla wooden piece, shared (never copied, which keeps graphics memory low).</summary>
    internal static Material Wood()
    {
        if (_wood != null)
            return _wood;

        foreach (var prefabName in new[] { "wood_window", "wood_floor", "woodwall" })
        {
            var prefab = PrefabManager.Instance.GetPrefab(prefabName);
            var material = prefab != null
                ? WoodOf(prefab)
                : null;
            if (material != null)
            {
                _wood = material;
                Jotunn.Logger.LogInfo($"Card table wood: material '{material.name}' from '{prefabName}'.");
                return _wood;
            }
        }

        _wood = Flat(new Color(0.40f, 0.26f, 0.14f));
        return _wood;
    }

    /// <summary>The wood material on a prefab (by name), or its first material when none is named wood.</summary>
    private static Material? WoodOf(GameObject prefab)
    {
        var all = prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).ToList();
        return all.FirstOrDefault(m => m.name.IndexOf("wood", System.StringComparison.OrdinalIgnoreCase) >= 0) ?? all.FirstOrDefault();
    }

    /// <summary>Green felt: a soft speckled green, tiled across the table.</summary>
    internal static Material Felt(Vector2 size)
    {
        if (_felt == null)
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true)
            {
                name = "CardTableFelt",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var rng = new System.Random(7);
            var pixels = new Color[n * n];
            for (int i = 0; i < pixels.Length; i++)
            {
                float v = 0.92f + (float)rng.NextDouble() * 0.16f;
                pixels[i] = new Color(0.10f * v, 0.36f * v, 0.19f * v, 1f);
            }

            tex.SetPixels(pixels);
            tex.Apply(true, false);
            _felt = new Material(Diffuse()) { name = "CardTableFelt", mainTexture = tex };
        }

        // One repeat per 10 cm, so the weave looks the same on a small and a long table.
        _felt.mainTextureScale = new Vector2(size.x * 10f, size.y * 10f);
        return _felt;
    }
}

/// <summary>Builds the table piece from simple shapes (a model from Blender replaces this later; the behaviour stays the same).</summary>
internal static class TableBuilder
{
    internal static void Build(GameObject root, Vector2 size, string title)
    {
        root.SetActive(true);
        StripDonor(root);

        var table = root.AddComponent<CardTable>();
        table.Size = size;
        table.Title = title;
        root.AddComponent<TableView>();

        float top = table.TopHeight;
        const float slab = 0.06f;
        var wood = TableMaterials.Wood();

        Part(root, "top", new Vector3(0f, top - slab / 2f, 0f), new Vector3(size.x, slab, size.y), wood);
        var felt = Part(root, "felt", new Vector3(0f, top + 0.006f, 0f), new Vector3(size.x - 0.2f, 0.012f, size.y - 0.2f),
            TableMaterials.Felt(size - new Vector2(0.2f, 0.2f)));
        felt.GetComponent<Collider>().enabled = false;

        float lx = size.x / 2f - 0.14f, lz = size.y / 2f - 0.14f;
        foreach (var sx in new[] { -1f, 1f })
        {
            foreach (var sz in new[] { -1f, 1f })
                Part(root, "leg", new Vector3(sx * lx, (top - slab) / 2f, sz * lz), new Vector3(0.09f, top - slab, 0.09f), wood);
        }

        var layer = LayerMask.NameToLayer("piece");
        SetLayer(root, layer < 0 ? 10 : layer);
    }

    private static GameObject Part(GameObject root, string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    private static void SetLayer(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayer(child.gameObject, layer);
    }

    /// <summary>The donor only supplies the networking, piece and wear components; its own visuals and behaviours go.</summary>
    private static void StripDonor(GameObject root)
    {
        var doomed = new List<Object>();
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            switch (component)
            {
                case Collider:
                case Rigidbody:
                case Renderer:
                case ZSyncTransform:
                case TerrainModifier:
                    doomed.Add(component);
                    break;
                case Canvas canvas:
                    doomed.Add(canvas.gameObject);
                    break;
                case TMPro.TMP_Text text:
                    doomed.Add(text.gameObject);
                    break;
                case MonoBehaviour mb when mb.GetType().Name is "Sign" or "Container" or "Smelter" or "Fireplace":
                    doomed.Add(mb);
                    break;
            }
        }

        foreach (var obj in doomed)
        {
            if (obj != null)
                Object.DestroyImmediate(obj);
        }

        var children = new List<GameObject>();
        foreach (Transform child in root.transform)
            children.Add(child.gameObject);
        foreach (var child in children)
            Object.DestroyImmediate(child);

        var piece = root.GetComponent<Piece>();
        if (piece != null)
        {
            piece.m_groundPiece = false;
            piece.m_groundOnly = false;
            piece.m_clipGround = true;
            piece.m_noInWater = true;
            piece.m_canBeRemoved = true;
        }

        var wnt = root.GetComponent<WearNTear>();
        if (wnt != null)
        {
            wnt.m_autoCreateFragments = false;
            wnt.m_fragmentRoots = System.Array.Empty<GameObject>();
            wnt.m_destroyedEffect = new EffectList();
            wnt.m_hitEffect = new EffectList();
            wnt.m_noSupportWear = true;
            wnt.m_noRoofWear = true;
            wnt.m_health = 400f;
        }

        var znet = root.GetComponent<ZNetView>();
        if (znet != null)
            znet.m_persistent = true;
    }
}
