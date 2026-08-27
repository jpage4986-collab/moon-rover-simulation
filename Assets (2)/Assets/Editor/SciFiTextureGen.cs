using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// 程序化生成 sci-fi UI 纹理（渐变、网格、扫描线）
/// 生成后保存到 Assets/Kenney/Generated/ 目录
/// </summary>
public static class SciFiTextureGen
{
    const string SAVE_DIR = "Assets/Kenney/Generated";

    [MenuItem("Tools/Kenney/生成 Sci-Fi 纹理")]
    public static void GenerateAll()
    {
        Directory.CreateDirectory(SAVE_DIR);

        GenPanelGradient();
        GenGridTexture();
        GenScanlineTexture();
        GenGlowBorder();

        AssetDatabase.Refresh();
        Debug.Log("[SciFiTextureGen] 全部纹理已生成");
    }

    // ── 面板渐变背景（深蓝→深青） ──
    static void GenPanelGradient()
    {
        string path = SAVE_DIR + "/PanelGradient.png";
        int w = 2, h = 256;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            float t = (float)y / h;
            // 顶部深蓝 (#0a0e1a) → 底部稍浅 (#0d1525)
            Color c = new Color(
                Mathf.Lerp(0.04f, 0.06f, t),
                Mathf.Lerp(0.06f, 0.09f, t),
                Mathf.Lerp(0.12f, 0.16f, t),
                0.92f
            );
            for (int x = 0; x < w; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        SaveTexture(tex, path);
    }

    // ── 网格背景 ──
    static void GenGridTexture()
    {
        string path = SAVE_DIR + "/GridPattern.png";
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color bg = new Color(0, 0, 0, 0);
        Color line = new Color(0.15f, 0.35f, 0.6f, 0.12f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, (x == 0 || y == 0) ? line : bg);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;
        SaveTexture(tex, path);
    }

    // ── 扫描线 ──
    static void GenScanlineTexture()
    {
        string path = SAVE_DIR + "/Scanlines.png";
        int w = 4, h = 4;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color dark = new Color(0, 0, 0, 0.08f);
        Color light = Color.clear;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, (y % 2 == 0) ? dark : light);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;
        SaveTexture(tex, path);
    }

    // ── 发光边框（用于面板边框） ──
    static void GenGlowBorder()
    {
        string path = SAVE_DIR + "/GlowBorder.png";
        int w = 64, h = 4;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        // 中间亮两边暗的渐变
        for (int x = 0; x < w; x++)
        {
            float t = (float)x / w;
            float glow = Mathf.Sin(t * Mathf.PI); // 0→1→0
            Color c = new Color(0.2f, 0.7f, 1f, glow * 0.8f);
            for (int y = 0; y < h; y++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        SaveTexture(tex, path);
    }

    static void SaveTexture(Texture2D tex, string path)
    {
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }
}
