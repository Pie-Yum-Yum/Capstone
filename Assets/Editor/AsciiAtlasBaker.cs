// Put this file in a folder named "Editor" (e.g. Assets/Editor/AsciiAtlasBaker.cs)
// Opens from the menu: Tools > ASCII > Atlas Baker
//
// Bakes a horizontal strip of square tiles: the density ramp first, then 4 edge glyphs.
// Output is white glyphs on black, which is what the shader expects.

using System.IO;
using UnityEditor;
using UnityEngine;

public class AsciiAtlasBaker : EditorWindow
{
    [SerializeField] private Font font;                      // leave null to use an OS monospace font
    [SerializeField] private string osFontName = "Courier New";
    [SerializeField] private string ramp = " .:-=+*#%@";      // darkest -> brightest
    [SerializeField] private string edgeChars = "-/|\\";      // MUST stay in this order: - / | \
    [SerializeField] private int tileSize = 16;              // pixels per tile
    [SerializeField] private int fontSize = 14;
    [SerializeField] private float baselineFraction = 0.25f; // where the text baseline sits in the tile
    [SerializeField] private string outputPath = "Assets/Art/AsciiAtlas.png";

    [MenuItem("Tools/ASCII/Atlas Baker")]
    private static void Open()
    {
        GetWindow<AsciiAtlasBaker>("ASCII Atlas");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Font", EditorStyles.boldLabel);
        font = (Font)EditorGUILayout.ObjectField("Font Asset", font, typeof(Font), false);
        using (new EditorGUI.DisabledScope(font != null))
        {
            osFontName = EditorGUILayout.TextField("OS Font Name", osFontName);
        }
        fontSize = EditorGUILayout.IntSlider("Font Size", fontSize, 6, 64);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Characters", EditorStyles.boldLabel);
        ramp = EditorGUILayout.TextField("Density Ramp", ramp);
        EditorGUILayout.LabelField("Edge Glyphs", edgeChars + "  (fixed order)");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        tileSize = EditorGUILayout.IntSlider("Tile Size (px)", tileSize, 4, 64);
        baselineFraction = EditorGUILayout.Slider("Baseline", baselineFraction, 0f, 0.6f);
        outputPath = EditorGUILayout.TextField("Path", outputPath);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Set _CharCount on the material to " + Mathf.Max(1, ramp.Length) +
            " (ramp length only, not counting the 4 edge glyphs).",
            MessageType.Info);

        if (GUILayout.Button("Bake Atlas", GUILayout.Height(30)))
        {
            Bake();
        }
    }

    private void Bake()
    {
        string all = ramp + edgeChars;
        if (all.Length == 0)
        {
            Debug.LogError("[AsciiAtlasBaker] Nothing to bake.");
            return;
        }

        Font f = font != null ? font : Font.CreateDynamicFontFromOSFont(osFontName, fontSize);
        if (f == null)
        {
            Debug.LogError("[AsciiAtlasBaker] Could not create font '" + osFontName + "'.");
            return;
        }

        // Force the dynamic font atlas to contain every glyph we need before reading metrics.
        f.RequestCharactersInTexture(all, fontSize, FontStyle.Normal);

        int w = tileSize * all.Length;
        int h = tileSize;

        RenderTexture rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        rt.filterMode = FilterMode.Point;

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        GL.Clear(true, true, Color.black);
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, w, 0, h); // origin bottom-left, y up

        f.material.SetPass(0);
        GL.Begin(GL.QUADS);
        GL.Color(Color.white);

        float baseline = tileSize * baselineFraction;

        for (int i = 0; i < all.Length; i++)
        {
            CharacterInfo ci;
            if (!f.GetCharacterInfo(all[i], out ci, fontSize, FontStyle.Normal))
            {
                continue; // leaves a blank tile
            }

            float glyphWidth = ci.maxX - ci.minX;
            if (glyphWidth <= 0f)
            {
                continue; // space, or a glyph with no ink
            }

            float x0 = i * tileSize + (tileSize - glyphWidth) * 0.5f;
            float x1 = x0 + glyphWidth;
            float y0 = baseline + ci.minY;
            float y1 = baseline + ci.maxY;

            GL.TexCoord(ci.uvBottomLeft);  GL.Vertex3(x0, y0, 0f);
            GL.TexCoord(ci.uvBottomRight); GL.Vertex3(x1, y0, 0f);
            GL.TexCoord(ci.uvTopRight);    GL.Vertex3(x1, y1, 0f);
            GL.TexCoord(ci.uvTopLeft);     GL.Vertex3(x0, y1, 0f);
        }

        GL.End();
        GL.PopMatrix();

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();

        RenderTexture.active = previous;
        rt.Release();
        DestroyImmediate(rt);

        string dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllBytes(outputPath, tex.EncodeToPNG());
        DestroyImmediate(tex);

        AssetDatabase.Refresh();
        ApplyImportSettings(outputPath);

        Debug.Log("[AsciiAtlasBaker] Baked " + all.Length + " tiles to " + outputPath +
                  ". Set _CharCount = " + ramp.Length + " on the material.");
    }

    private static void ApplyImportSettings(string path)
    {
        TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null)
        {
            return;
        }

        imp.textureType = TextureImporterType.Default;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.filterMode = FilterMode.Bilinear;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = false;
        imp.sRGBTexture = false; // this is a mask, not color
        imp.SaveAndReimport();
    }
}
