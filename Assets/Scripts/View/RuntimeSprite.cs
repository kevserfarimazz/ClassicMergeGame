using UnityEngine;

// Prototip aşamasında sprite asset'i olmadan hücre/parça/UI görselleri için
// basit dokular üretir. SpriteRenderer.color / Image.color ile boyanır.
public static class RuntimeSprite
{
    private static Sprite square;
    private static Sprite roundedSquare;
    private static Sprite pill;
    private static Sprite circle;
    private static Sprite chip;
    private static Sprite panel;
    private static Sprite ring;
    private static Sprite curtain;

    public static Sprite Square
    {
        get
        {
            if (square == null)
            {
                var tex = Texture2D.whiteTexture;
                square = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            }
            return square;
        }
    }

    // Yuvarlak köşeli kare doku; hücre/parça kutularına ve UI panellerine
    // modern, "mobil oyun" hissi veren yumuşak bir görünüm katar.
    public static Sprite RoundedSquare
    {
        get
        {
            if (roundedSquare == null)
            {
                roundedSquare = CreateRoundedSquare(256, 40);
            }
            return roundedSquare;
        }
    }

    // Köşe yarıçapı yarım genişliğe eşit — kare kullanıldığında tam daire,
    // dikdörtgen (Sliced) kullanıldığında "hap" (pill) şekli verir. Level
    // rozeti ve coin/enerji göstergeleri için.
    public static Sprite Pill
    {
        get
        {
            if (pill == null)
            {
                pill = CreateRoundedSquare(128, 62);
            }
            return pill;
        }
    }

    // Tam daire doku — kenar payı (border) TAŞIMAZ, bu yüzden hiçbir boyutta
    // "Sliced" 4/9-dilim matematiğine takılıp ezilmez. Sadece Image.Type.Simple
    // ile kare (1:1) bir rect üzerinde kullanılmalı: rozet halkası, coin/enerji
    // ikon daireleri gibi tam yuvarlak öğeler için.
    public static Sprite Circle
    {
        get
        {
            if (circle == null)
            {
                circle = CreateCircle(256);
            }
            return circle;
        }
    }

    // Geniş "hap" (coin/enerji göstergesi gibi dikdörtgen) şekiller için, kısa
    // kenara göre güvenli kalan orta büyüklükte bir köşe yuvarlaklığı. Pill'den
    // (62) daha küçük bir kenar payı kullanır ki dar/kısa dikdörtgenlerde de
    // 9-dilim matematiği ezilip küçük bir yuvarlağa çökmesin.
    public static Sprite Chip
    {
        get
        {
            if (chip == null)
            {
                chip = CreateRoundedSquare(128, 38);
            }
            return chip;
        }
    }

    // Board zemini, HUD paneli, alt bar gibi büyük yüzeyler için geniş köşe
    // yuvarlaklığı olan yumuşak bir kare. Sliced modda her boyutta düzgün kalır.
    public static Sprite Panel
    {
        get
        {
            if (panel == null)
            {
                panel = CreateRoundedSquare(256, 60);
            }
            return panel;
        }
    }

    // Level rozetinin ilerleme halkası için içi boş bir çember (kalın kenar).
    // Image.Type.Filled + Radial360 ile doldurma yüzdesi gösterilir.
    public static Sprite Ring
    {
        get
        {
            if (ring == null)
            {
                ring = CreateRing(256, 30);
            }
            return ring;
        }
    }

    // Ekranın en üstündeki sahne perdesi dekoru: dikey kıvrım bantları olan,
    // alta doğru saydamlaşan bir şerit. Image.color ile perde rengine boyanır.
    public static Sprite Curtain
    {
        get
        {
            if (curtain == null)
            {
                curtain = CreateCurtain(128, 96);
            }
            return curtain;
        }
    }

    private static Sprite CreateRing(int size, int thickness)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float outer = size / 2f;
        float inner = outer - thickness;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - outer;
                float dy = y + 0.5f - outer;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = (byte)(d <= outer && d >= inner ? 255 : 0);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite CreateCurtain(int width, int height)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat
        };

        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            // Doku y=0 en ALT sıra: üstte (v→1) tam opak, alttaki %35'te (festonlu
            // etek) yumuşakça saydamlaşır.
            float v = y / (float)(height - 1);
            float fade = v > 0.35f ? 1f : Mathf.SmoothStep(0f, 1f, v / 0.35f);

            for (int x = 0; x < width; x++)
            {
                // 6 kıvrım bandı: kosinüsle yumuşak koyu/açık gölgeleme.
                float fold = Mathf.Cos(x / (float)width * Mathf.PI * 2f * 6f) * 0.5f + 0.5f;
                float shade = Mathf.Lerp(0.72f, 1f, fold);
                byte c = (byte)(255 * shade);
                byte a = (byte)(255 * fade);
                pixels[y * width + x] = new Color32(c, c, c, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), width);
    }

    private static Sprite CreateCircle(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        int r = size / 2;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inside = IsInsideRoundedRect(x, y, size, r);
                pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        // Bilerek border PARAMETRESİZ (4 argümanlı) overload: r == size/2 olduğu
        // için 9-dilim border'ı (r,r,r,r) texture genişliğine eşit olurdu, bu da
        // geçersiz/dejenere bir slice'a (ve daha önce yaşanan Sprite.Create
        // çökmesine) yol açar. Border'ı hiç tanımlamayıp Simple modda kullanmak
        // bu sınıf sorunlarını tamamen ortadan kaldırıyor.
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite CreateRoundedSquare(int size, int cornerRadius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

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

        // Border tanımlamak, bu sprite'ı UI Image üzerinde "Sliced" modda kullanınca
        // köşelerin orantısız gerilmeden (elips olmadan) kalmasını sağlar.
        var border = new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect, border);
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
}
