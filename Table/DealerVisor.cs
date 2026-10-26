using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Cards.Table;

/// <summary>
/// The dealer's green visor: a helmet. Whoever wears it is the dealer at any card table, and (once the rules are locked) the only
/// one who can change them. The model is built in code (a cream band and a translucent green brim) until a modelled one replaces it.
/// </summary>
internal static class DealerVisor
{
    internal const string Prefab = "Drake_DealerVisor";
    internal const string DisplayName = "Dealer's Visor";

    private static readonly FieldInfo? HelmetField = AccessTools.Field(typeof(Humanoid), "m_helmetItem");

    /// <summary>Is this player wearing the visor?</summary>
    internal static bool IsWorn(Player? player)
    {
        if (player == null || HelmetField == null)
            return false;
        var item = HelmetField.GetValue(player) as ItemDrop.ItemData;
        return item?.m_dropPrefab != null && item.m_dropPrefab.name == Prefab;
    }

    internal static void Create()
    {
        const string description = "A green dealer's visor. Wear it at a card table and you are the dealer: you deal, you set the rules, " +
                                   "and once the rules are locked only the visor can change them.";
        var icon = Util.CardIcons.VisorIcon();
        var config = new ItemConfig
        {
            CraftingStation = CraftingStations.Workbench,
            Name = DisplayName,
            Description = description,
            Icon = icon,
            Amount = 1,
            Requirements =
            [
                new RequirementConfig("LeatherScraps", 4, 0, true),
                new RequirementConfig("Resin", 2, 0, true)
            ]
        };

        var item = new CustomItem(Prefab, "HelmetLeather", config);
        var prefab = item.ItemPrefab;
        var shared = item.ItemDrop?.m_itemData?.m_shared;
        if (prefab == null || shared == null)
        {
            Jotunn.Logger.LogError("Dealer visor: the helmet donor is missing.");
            return;
        }

        shared.m_name = DisplayName;
        shared.m_description = description;
        shared.m_armor = 0f;
        shared.m_armorPerLevel = 0f;
        shared.m_weight = 0.3f;
        shared.m_maxQuality = 1;
        shared.m_variants = 0;
        shared.m_movementModifier = 0f;
        shared.m_equipStatusEffect = null;
        shared.m_setStatusEffect = null;
        shared.m_setName = "";
        shared.m_setSize = 0;
        shared.m_icons = new[] { icon };
        // An open visor: the hair and beard still show (the leather helmet hides them).
        foreach (var field in new[] { "m_helmetHideHair", "m_helmetHideBeard" })
        {
            var info = typeof(ItemDrop.ItemData.SharedData).GetField(field);
            if (info == null)
                continue;
            // Older builds use a bool, the live game an enum whose first value shows everything.
            info.SetValue(shared, info.FieldType == typeof(bool) ? false : System.Enum.ToObject(info.FieldType, 0));
        }

        // The donor's model goes; ours takes its place as the "attach" child the game puts on the head.
        var doomed = new List<GameObject>();
        foreach (Transform child in prefab.transform)
            doomed.Add(child.gameObject);
        foreach (var go in doomed)
            Object.DestroyImmediate(go);
        foreach (var renderer in prefab.GetComponents<Renderer>())
            Object.DestroyImmediate(renderer);

        BuildModel(prefab.transform);
        ItemManager.Instance.AddItem(item);
    }

    // ---------------------------------------------------------------- the model (origin = middle of the head, +Z = forward)
    //
    // Like a real dealer's visor: a white band round the forehead (widest at the front, a thin elastic strap at the back) and a
    // curved green plastic brim at the FRONT ONLY, longest in the middle and tapering to nothing at the sides, with a white edge.

    private const float BandRadius = 0.118f;
    private const float BandY = 0.075f;
    private const float BrimReach = 0.115f;
    private const float BrimHalfAngle = 68f;

    private static void BuildModel(Transform parent)
    {
        var attach = new GameObject("attach").transform;
        attach.SetParent(parent, false);

        if (TryBuildFromModel(attach))
            return;

        var cream = TableMaterials.Flat(new Color(0.95f, 0.93f, 0.86f));
        Part(attach, "band", Band(36), cream, new Vector3(0f, BandY, 0f));
        Part(attach, "brim", Brim(28), TableMaterials.Glass(new Color(0.08f, 0.70f, 0.30f, 0.62f)),
            new Vector3(0f, BandY - 0.018f, 0f));
        Part(attach, "trim", Trim(28), cream, new Vector3(0f, BandY - 0.018f, 0f));
    }

    /// <summary>The modelled visor (Assets/dealer_visor.glb, made by Models/dealer_visor.py). The code-built one below is the fallback.</summary>
    private static bool TryBuildFromModel(Transform attach)
    {
        try
        {
            var path = Util.Loader.FindAsset("dealer_visor.glb");
            if (path == null)
                return false;
            var model = Gltf.GlbReader.Read(System.IO.File.ReadAllBytes(path));
            // The reader's space has the model's front at -Z; the game's head faces +Z, so the model is turned half way round.
            var root = new GameObject("model").transform;
            root.SetParent(attach, false);
            root.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var materials = new Dictionary<int, Material>();
            int count = 0;
            foreach (var sub in model.Submeshes)
            {
                var mesh = new Mesh { name = "Visor_" + (sub.Name ?? count.ToString()) };
                var vertices = new List<Vector3>(sub.Positions.Length / 3);
                for (int i = 0; i + 2 < sub.Positions.Length; i += 3)
                    vertices.Add(new Vector3(sub.Positions[i], sub.Positions[i + 1], sub.Positions[i + 2]));
                mesh.SetVertices(vertices);
                mesh.SetTriangles(sub.Indices, 0);
                mesh.RecalculateNormals();

                if (!materials.TryGetValue(sub.Material, out var material))
                {
                    var gm = sub.Material >= 0 && sub.Material < model.Materials.Count ? model.Materials[sub.Material] : null;
                    var c = gm?.BaseColor ?? new[] { 1f, 1f, 1f, 1f };
                    var color = new Color(c[0], c[1], c[2], c.Length > 3 ? c[3] : 1f);
                    material = gm != null && gm.Blend ? TableMaterials.Glass(color) : TableMaterials.Flat(color);
                    materials[sub.Material] = material;
                }

                Part(root, mesh.name, mesh, material, Vector3.zero);
                count++;
            }

            Jotunn.Logger.LogInfo($"Dealer visor: built from dealer_visor.glb ({count} parts).");
            return count > 0;
        }
        catch (System.Exception ex)
        {
            Jotunn.Logger.LogWarning($"Dealer visor model could not be read ({ex.Message}); using the built-in shape.");
            foreach (Transform child in attach)
                Object.Destroy(child.gameObject);
            return false;
        }
    }

    private static void Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>How far the brim reaches at an angle from the front (degrees): full in the middle, nothing at the sides.</summary>
    private static float Reach(float degrees)
    {
        float t = Mathf.Clamp01(1f - Mathf.Abs(degrees) / BrimHalfAngle);
        return BrimReach * Mathf.Sqrt(Mathf.Sin(t * Mathf.PI / 2f));
    }

    /// <summary>The brim's outer edge point at an angle: out from the band, drooping a little, more at the middle.</summary>
    private static Vector3 BrimEdge(float degrees)
    {
        float a = degrees * Mathf.Deg2Rad;
        var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
        float reach = Reach(degrees);
        float droop = -0.012f - 0.07f * reach;
        return dir * (BandRadius + reach) + Vector3.up * droop;
    }

    /// <summary>The band: a ring whose height changes with the angle (tall in front, a strap behind), open to both sides.</summary>
    private static Mesh Band(int segments)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        const float thickness = 0.007f;
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            float front = (Mathf.Cos(a) + 1f) / 2f;                 // 1 at the front, 0 at the back
            float height = Mathf.Lerp(0.012f, 0.042f, front * front);
            var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            vertices.Add(dir * (BandRadius + thickness) + Vector3.up * (height / 2f));
            vertices.Add(dir * (BandRadius + thickness) - Vector3.up * (height / 2f));
            vertices.Add(dir * BandRadius + Vector3.up * (height / 2f));
            vertices.Add(dir * BandRadius - Vector3.up * (height / 2f));
        }

        for (int i = 0; i < segments; i++)
        {
            int b = i * 4;
            Quad(triangles, b, b + 4, b + 5, b + 1);          // outer wall
            Quad(triangles, b + 2, b + 3, b + 7, b + 6);      // inner wall
            Quad(triangles, b, b + 2, b + 6, b + 4);          // top
            Quad(triangles, b + 1, b + 5, b + 7, b + 3);      // bottom
        }

        var mesh = new Mesh { name = "VisorBand" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        return mesh;
    }

    /// <summary>The brim: a curved tongue of plastic on the front of the band, seen from above and below.</summary>
    private static Mesh Brim(int segments)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int i = 0; i <= segments; i++)
        {
            float degrees = Mathf.Lerp(-BrimHalfAngle, BrimHalfAngle, i / (float)segments);
            float a = degrees * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            vertices.Add(dir * BandRadius);
            vertices.Add(BrimEdge(degrees));
        }

        for (int i = 0; i < segments; i++)
        {
            int b = i * 2;
            Quad(triangles, b, b + 2, b + 3, b + 1);
            Quad(triangles, b, b + 1, b + 3, b + 2); // the same sheet from the other side
        }

        var mesh = new Mesh { name = "VisorBrim" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        return mesh;
    }

    /// <summary>The white edge along the brim's outer rim.</summary>
    private static Mesh Trim(int segments)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        const float width = 0.007f;
        for (int i = 0; i <= segments; i++)
        {
            float degrees = Mathf.Lerp(-BrimHalfAngle, BrimHalfAngle, i / (float)segments);
            var edge = BrimEdge(degrees);
            var inward = new Vector3(edge.x, 0f, edge.z).normalized * -width;
            vertices.Add(edge);
            vertices.Add(edge + inward + Vector3.up * (width * 0.4f));
        }

        for (int i = 0; i < segments; i++)
        {
            int b = i * 2;
            Quad(triangles, b, b + 2, b + 3, b + 1);
            Quad(triangles, b, b + 1, b + 3, b + 2);
        }

        var mesh = new Mesh { name = "VisorTrim" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        return mesh;
    }

    private static void Quad(List<int> t, int a, int b, int c, int d)
    {
        t.Add(a);
        t.Add(b);
        t.Add(c);
        t.Add(a);
        t.Add(c);
        t.Add(d);
    }
}
