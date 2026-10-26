using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cards.Util;

/// <summary>
/// Cards cooked in code: each side of a card is a small rounded-corner mesh with the card art mapped onto it, drawn with a lit
/// matte material and real back-face culling. The face side is seen from behind the piece's forward direction and the back
/// side from the front, so text is never mirrored and the two sides never fight, with no per-frame switching.
/// </summary>
internal static class CardMesh
{
    private const int ArcSteps = 5;
    private const float CornerFraction = 0.06f;

    /// <summary>
    /// How bright the paper is. The paper is lit by the world, and a white card in full sun would go past 1.0 and glow through the
    /// game's bloom, so it is dimmed well down. Set from the config ("Paper brightness") and live while the game runs.
    /// </summary>
    internal static float Brightness { get; private set; } = 0.62f;

    internal static void SetBrightness(float value)
    {
        Brightness = Mathf.Clamp(value, 0.05f, 1f);
        foreach (var material in Materials.Values)
        {
            if (material != null && material.HasProperty("_Color"))
                material.SetColor("_Color", new Color(Level(Brightness), Level(Brightness), Level(Brightness), 1f));
        }

        if (_plain != null && _plain.HasProperty("_Color"))
            _plain.SetColor("_Color", new Color(0.45f * Level(Brightness), 0.08f * Level(Brightness), 0.1f * Level(Brightness), 1f));
    }

    private static readonly Dictionary<(int sprite, int w, int h, bool back), Mesh> Meshes =
        new Dictionary<(int, int, int, bool), Mesh>();

    private static readonly Dictionary<int, Material> Materials = new Dictionary<int, Material>();
    private static Shader? _shader;
    private static Material? _plain;
    private static bool _selfLit;

    /// <summary>
    /// The colour multiplier for a brightness. The self-lit paper shader adds the card's own colour to the lit colour, so cards in
    /// shade stay readable instead of going black; it needs a lower multiplier to match the plain shader's look in the sun.
    /// </summary>
    internal static float Level(float brightness)
    {
        PaperShader();
        return _selfLit ? brightness * 0.56f : brightness;
    }

    /// <summary>A child object that will show one side of a card. Named "front" or "back"; the name decides which side it faces.</summary>
    internal static GameObject MakeFace(string name, Transform parent, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.layer = parent.gameObject.layer;
        go.AddComponent<MeshFilter>();
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        return go;
    }

    /// <summary>
    /// Show a sprite on a card side, as large as fits inside <paramref name="box"/> with its own proportions (never stretched).
    /// </summary>
    internal static void Apply(GameObject face, Sprite? sprite, Vector2 box)
    {
        var filter = face.GetComponent<MeshFilter>();
        var renderer = face.GetComponent<MeshRenderer>();
        if (filter == null || renderer == null)
            return;

        bool back = face.name == "back";
        if (sprite == null)
        {
            filter.sharedMesh = Build(null, box, back);
            renderer.sharedMaterial = Plain();
            return;
        }

        filter.sharedMesh = GetMesh(sprite, box, back);
        renderer.sharedMaterial = GetMaterial(sprite.texture);
    }

    /// <summary>Width and height of the card for this sprite inside the box.</summary>
    internal static Vector2 Fit(Sprite? sprite, Vector2 box)
    {
        if (sprite == null)
            return box;
        var r = sprite.textureRect;
        if (r.width < 1f || r.height < 1f)
            return box;
        float aspect = r.width / r.height;
        float w = box.x;
        float h = w / aspect;
        if (h > box.y)
        {
            h = box.y;
            w = h * aspect;
        }

        return new Vector2(w, h);
    }

    // ---------------------------------------------------------------- mesh

    private static Mesh GetMesh(Sprite sprite, Vector2 box, bool back)
    {
        var key = (sprite.GetInstanceID(), Mathf.RoundToInt(box.x * 1000f), Mathf.RoundToInt(box.y * 1000f), back);
        if (!Meshes.TryGetValue(key, out var mesh) || mesh == null)
        {
            mesh = Build(sprite, box, back);
            Meshes[key] = mesh;
        }

        return mesh;
    }

    private static Mesh Build(Sprite? sprite, Vector2 box, bool back)
    {
        var size = Fit(sprite, box);
        float w = size.x, h = size.y;
        float r = Mathf.Min(w, h) * CornerFraction;

        // Rounded rectangle outline, counter-clockwise seen from +Z.
        var outline = new List<Vector2>();
        AddArc(outline, new Vector2(w / 2f - r, h / 2f - r), r, 0f, 90f);
        AddArc(outline, new Vector2(-w / 2f + r, h / 2f - r), r, 90f, 180f);
        AddArc(outline, new Vector2(-w / 2f + r, -h / 2f + r), r, 180f, 270f);
        AddArc(outline, new Vector2(w / 2f - r, -h / 2f + r), r, 270f, 360f);

        var vertices = new List<Vector3> { Vector3.zero };
        var uvs = new List<Vector2>();
        var normals = new List<Vector3>();
        Rect rect = sprite != null ? sprite.textureRect : new Rect(0, 0, 1, 1);
        float texW = sprite != null ? sprite.texture.width : 1f;
        float texH = sprite != null ? sprite.texture.height : 1f;

        Vector2 Uv(Vector2 p)
        {
            float u = p.x / w + 0.5f;
            float v = p.y / h + 0.5f;
            if (back)
                u = 1f - u; // seen from the other side, so the picture is mirrored back
            return new Vector2((rect.x + u * rect.width) / texW, (rect.y + v * rect.height) / texH);
        }

        var normal = back ? new Vector3(0f, 0f, 1f) : new Vector3(0f, 0f, -1f);
        uvs.Add(Uv(Vector2.zero));
        normals.Add(normal);
        foreach (var p in outline)
        {
            vertices.Add(new Vector3(p.x, p.y, 0f));
            uvs.Add(Uv(p));
            normals.Add(normal);
        }

        var triangles = new List<int>();
        for (int i = 1; i <= outline.Count; i++)
        {
            int next = i == outline.Count ? 1 : i + 1;
            // Counter-clockwise from +Z faces +Z (the back); the face side is wound the other way to face -Z.
            if (back)
            {
                triangles.Add(0);
                triangles.Add(i);
                triangles.Add(next);
            }
            else
            {
                triangles.Add(0);
                triangles.Add(next);
                triangles.Add(i);
            }
        }

        var mesh = new Mesh { name = (back ? "CardBack" : "CardFace") + (sprite != null ? "_" + sprite.name : "") };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddArc(List<Vector2> outline, Vector2 center, float radius, float fromDeg, float toDeg)
    {
        for (int i = 0; i <= ArcSteps; i++)
        {
            float a = Mathf.Lerp(fromDeg, toDeg, i / (float)ArcSteps) * Mathf.Deg2Rad;
            outline.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
        }
    }

    // ---------------------------------------------------------------- material

    private static Shader? PaperShader()
    {
        if (_shader != null)
            return _shader;
        // The game's own piece shader lights things the way the rest of the world is lit (shade is dim, never black); it is not
        // findable by name, so it is borrowed from a vanilla piece.
        try
        {
            var donor = Jotunn.Managers.PrefabManager.Instance.GetPrefab("wood_floor");
            var shader = donor != null ? donor.GetComponentInChildren<Renderer>()?.sharedMaterial?.shader : null;
            if (shader != null && shader.name.StartsWith("Custom/"))
            {
                _shader = shader;
                Jotunn.Logger.LogInfo($"Card paper shader: {shader.name} (from wood_floor).");
                return _shader;
            }
        }
        catch (System.Exception ex) { Jotunn.Logger.LogWarning($"Card paper shader donor failed: {ex.Message}"); }

        foreach (var name in new[] { "Legacy Shaders/Diffuse", "Sprites/Default" })
        {
            _shader = Shader.Find(name);
            if (_shader != null)
            {
                _selfLit = name.Contains("Self-Illumin");
                Jotunn.Logger.LogInfo($"Card paper shader: {name}.");
                break;
            }
        }

        return _shader;
    }

    private static Material GetMaterial(Texture texture)
    {
        int id = texture.GetInstanceID();
        if (Materials.TryGetValue(id, out var material) && material != null)
            return material;

        material = new Material(PaperShader()) { name = "CardPaper_" + texture.name };
        material.mainTexture = texture;
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", new Color(Level(Brightness), Level(Brightness), Level(Brightness), 1f));
        Materials[id] = material;
        return material;
    }

    private static Material Plain()
    {
        if (_plain != null)
            return _plain;
        _plain = new Material(PaperShader()) { name = "CardPaperPlain" };
        if (_plain.HasProperty("_Color"))
            _plain.SetColor("_Color", new Color(0.45f * Level(Brightness), 0.08f * Level(Brightness), 0.1f * Level(Brightness), 1f));
        return _plain;
    }
}
