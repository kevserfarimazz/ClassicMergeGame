using System.IO;
using UnityEditor;
using UnityEngine;

// Bilgi paneli (Header) için gereken temel UI dokularını (tam daire, yuvarlak
// köşeli kare) GERÇEK bir .png asset'i olarak Assets/Resources/UI/ altına
// üretir. RuntimeSprite.cs'deki piksel mantığının birebir aynısını kullanır;
// farkı, bunun oyunu her Play'e basışta değil, Editor'da BİR KERE çalışıp
// kalıcı bir dosya bırakması — böylece Header sahneye gerçek, sürükle-bırak
// düzenlenebilir bir obje olarak kurulabiliyor.
public static class UISpriteGenerator
{
    private const string OutputFolder = "Assets/Resources/UI";

    [MenuItem("Codeco Soft/1) UI Dokularını Oluştur")]
    public static void GenerateAll()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "UI");
        }

        WriteTexture("circle", 128, 64);
        WriteTexture("roundedsquare", 128, 24);

        AssetDatabase.Refresh();

        ConfigureSprite($"{OutputFolder}/circle.png", Vector4.zero);
        ConfigureSprite($"{OutputFolder}/roundedsquare.png", new Vector4(24, 24, 24, 24));

        AssetDatabase.SaveAssets();
        Debug.Log("UI dokuları oluşturuldu: " + OutputFolder + " — şimdi 'Codeco Soft > 2) Bilgi Panelini Sahneye Kur' menüsünü çalıştırabilirsin.");
    }

    private static void WriteTexture(string name, int size, int cornerRadius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inside = IsInsideRoundedRect(x, y, size, cornerRadius);
                pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        var path = $"{OutputFolder}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    private static bool IsInsideRoundedRect(int x, int y, int size, int r)
    {
        int maxX = size - 1;
        int maxY = size - 1;

        bool nearLeft = x < r;
        bool nearRight = x > maxX - r;
        bool nearBottom = y < r;
        bool nearTop = y > maxY - r;

        if ((nearLeft || nearRight) && (nearBottom || nearTop))
        {
            int cx = nearLeft ? r : maxX - r;
            int cy = nearBottom ? r : maxY - r;
            int dx = x - cx;
            int dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        return true;
    }

    private static void ConfigureSprite(string path, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = border;
        importer.spritePixelsPerUnit = 128f;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }
}
