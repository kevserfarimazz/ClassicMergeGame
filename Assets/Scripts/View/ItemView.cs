using System.Collections;
using UnityEngine;

// Tahtada görünen tek bir parçanın görsel temsili: yumuşak beyaz kart + alt
// gölge + ortada ikon. İkon yoksa tier'a göre renklendirilmiş kart + isim yazısı.
// Yeni üretilmiş parçalarda sol-altta mavi "spark" rozeti gösterilir.
public class ItemView : MonoBehaviour
{
    private const float TargetScale = 0.8f;

    private static readonly Color CardColor = new Color(1f, 1f, 1f, 1f);
    private static readonly Color ShadowColor = new Color(0.06f, 0.15f, 0.26f, 0.22f);
    private static readonly Color SparkColor = new Color(0.25f, 0.66f, 0.96f, 1f);

    private SpriteRenderer shadowRenderer;
    private SpriteRenderer cardRenderer;
    private SpriteRenderer iconRenderer;
    private TextMesh label;
    private GameObject sparkVisual;

    public ItemInstance Data { get; private set; }

    private void Awake()
    {
        shadowRenderer = new GameObject("Shadow").AddComponent<SpriteRenderer>();
        shadowRenderer.transform.SetParent(transform);
        shadowRenderer.transform.localPosition = new Vector3(0f, -0.06f, 0.05f);
        shadowRenderer.transform.localScale = new Vector3(0.98f, 0.98f, 1f);
        shadowRenderer.sprite = RuntimeSprite.RoundedSquare;
        shadowRenderer.color = ShadowColor;
        shadowRenderer.sortingOrder = 1;

        cardRenderer = gameObject.AddComponent<SpriteRenderer>();
        cardRenderer.sprite = RuntimeSprite.RoundedSquare;
        cardRenderer.color = CardColor;
        cardRenderer.sortingOrder = 2;

        iconRenderer = new GameObject("Icon").AddComponent<SpriteRenderer>();
        iconRenderer.transform.SetParent(transform);
        iconRenderer.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        iconRenderer.transform.localScale = Vector3.one * 0.9f;
        iconRenderer.sortingOrder = 3;

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(transform);
        labelGO.transform.localPosition = new Vector3(0f, 0f, -0.1f);

        label = labelGO.AddComponent<TextMesh>();
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 32;
        label.characterSize = 0.10f;
        label.color = new Color(0.16f, 0.20f, 0.26f);

        var labelRenderer = labelGO.GetComponent<MeshRenderer>();
        labelRenderer.sortingOrder = 3;

        transform.localScale = Vector3.zero;
        StartCoroutine(PopIn());
    }

    public void SetItem(ItemInstance item)
    {
        Data = item;
        var tier = item.chain.GetTier(item.tierIndex);

        if (tier.icon != null)
        {
            iconRenderer.sprite = tier.icon;
            iconRenderer.enabled = true;
            cardRenderer.color = CardColor;
            label.text = "";
        }
        else
        {
            // İkon yoksa kartı zincir rengine boyar ve parçanın adını iki satıra
            // bölerek üstüne yazar (örn. "Yapay\nZeka").
            iconRenderer.enabled = false;
            cardRenderer.color = GetFallbackColor(item);
            label.text = tier.displayName.Trim().Replace(" ", "\n");
        }
    }

    // Yeni üretilmiş, henüz dokunulmamış parçalarda sol-altta mavi bir spark rozeti.
    public void SetSpark(bool on)
    {
        if (on)
        {
            if (sparkVisual != null) return;

            sparkVisual = new GameObject("Spark");
            sparkVisual.transform.SetParent(transform);
            sparkVisual.transform.localPosition = new Vector3(-0.42f, -0.42f, -0.15f);
            sparkVisual.transform.localScale = Vector3.one * 0.34f;

            var disc = sparkVisual.AddComponent<SpriteRenderer>();
            disc.sprite = RuntimeSprite.Circle;
            disc.color = SparkColor;
            disc.sortingOrder = 4;

            var boltGO = new GameObject("Bolt").AddComponent<SpriteRenderer>();
            boltGO.transform.SetParent(sparkVisual.transform);
            boltGO.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            boltGO.transform.localScale = Vector3.one * 0.42f;
            boltGO.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            boltGO.sprite = RuntimeSprite.RoundedSquare;
            boltGO.color = Color.white;
            boltGO.sortingOrder = 5;
        }
        else
        {
            if (sparkVisual == null) return;
            Destroy(sparkVisual);
            sparkVisual = null;
        }
    }

    // İkon atanmamış zincirler için, chainId'den türetilen sabit bir renk ailesi üretir;
    // aynı zincirin tier'ları birbirine yakın, farklı zincirler belirgin farklı tonlarda olur.
    private static Color GetFallbackColor(ItemInstance item)
    {
        int chainHash = !string.IsNullOrEmpty(item.chain.chainId) ? item.chain.chainId.GetHashCode() : item.chain.GetHashCode();
        float hueBase = Mathf.Repeat(chainHash / 1000f, 1f);
        float hue = Mathf.Repeat(hueBase + item.tierIndex * 0.045f, 1f);
        return Color.HSVToRGB(hue, 0.45f, 0.97f);
    }

    // Her yeni parça (üretimden ya da merge'den) hafif bir "pop" ile büyüyerek belirir.
    private IEnumerator PopIn()
    {
        const float duration = 0.18f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            transform.localScale = Vector3.one * TargetScale * EaseOutBack(p);
            yield return null;
        }

        transform.localScale = Vector3.one * TargetScale;
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
