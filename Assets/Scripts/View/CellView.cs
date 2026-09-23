using System.Collections;
using UnityEngine;

// Tek bir grid hücresinin görsel temsili: yumuşak yuvarlak arka plan
// (kilitli/açık/özel rengi) + üzerindeki ItemView + seçim çerçevesi.
[RequireComponent(typeof(BoxCollider2D))]
public class CellView : MonoBehaviour
{
    // Board paneli gökyüzü mavisi olduğu için açık hücreler yarı saydam beyazla
    // hafifçe aydınlatılır; kilitli hücreler koyu teal bir örtüyle "henüz senin
    // değil" hissi verir.
    private static readonly Color UnlockedColor = new Color(1f, 1f, 1f, 0.34f);
    private static readonly Color LockedColor = new Color(0.40f, 0.52f, 0.64f, 0.55f);
    private static readonly Color GeneratorColor = new Color(0.16f, 0.72f, 0.62f, 1f);
    private static readonly Color TrashColor = new Color(0.33f, 0.75f, 0.45f, 1f);
    private static readonly Color OrderColor = new Color(0.95f, 0.69f, 0.27f, 1f);
    private static readonly Color CrateColor = new Color(0.52f, 0.38f, 0.25f, 1f);
    private static readonly Color OutlineColor = new Color(1f, 1f, 1f, 0.50f);
    private static readonly Color SelectionColor = new Color(0.36f, 0.80f, 0.42f, 1f);

    private SpriteRenderer background;
    private SpriteRenderer outline;
    private TextMesh label;
    private SpriteRenderer icon;
    private GameObject crateVisual;
    private GameObject selectionVisual;
    private Vector3 basePosition;
    private Coroutine shakeRoutine;
    private Coroutine pulseRoutine;
    private Color pulseBaseColor;

    public int GridX { get; private set; }
    public int GridY { get; private set; }
    public ItemView CurrentItemView { get; private set; }

    private void Awake()
    {
        outline = new GameObject("Outline").AddComponent<SpriteRenderer>();
        outline.transform.SetParent(transform);
        outline.transform.localPosition = new Vector3(0f, 0f, 0.05f);
        outline.transform.localScale = Vector3.one * 1.03f;
        outline.sprite = RuntimeSprite.RoundedSquare;
        outline.color = OutlineColor;
        outline.sortingOrder = -1;

        background = gameObject.AddComponent<SpriteRenderer>();
        background.sprite = RuntimeSprite.RoundedSquare;
        background.sortingOrder = 0;

        var col = gameObject.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;

        basePosition = transform.localPosition;
    }

    public void Init(int x, int y)
    {
        GridX = x;
        GridY = y;
    }

    public void SetLockVisual(bool locked)
    {
        background.color = locked ? LockedColor : UnlockedColor;
        outline.enabled = !locked;
    }

    public void SetGeneratorVisual()
    {
        background.color = GeneratorColor;
        outline.color = new Color(1f, 1f, 1f, 0.45f);
    }

    public void SetTrashVisual()
    {
        background.color = TrashColor;
        outline.color = new Color(1f, 1f, 1f, 0.45f);
    }

    public void SetOrderVisual()
    {
        background.color = OrderColor;
        outline.color = new Color(1f, 1f, 1f, 0.45f);
    }

    // Üretici/çöp/sipariş gibi özel hücrelerin ne işe yaradığını, hücrenin ALTINDA
    // duran küçük bir yazıyla etiketler.
    public void SetLabel(string text, bool below = true)
    {
        if (label == null)
        {
            var labelGO = new GameObject("SlotLabel");
            labelGO.transform.SetParent(transform);
            labelGO.transform.localPosition = new Vector3(0f, below ? -0.72f : 0.72f, -0.1f);

            label = labelGO.AddComponent<TextMesh>();
            label.anchor = below ? TextAnchor.UpperCenter : TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 40;
            label.characterSize = 0.055f;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(1f, 1f, 1f, 0.92f);

            var renderer = labelGO.GetComponent<MeshRenderer>();
            renderer.sortingOrder = 4;
        }

        label.text = text;
    }

    // Üretici/çöp/sipariş gibi özel hücrelere ortada, beyaz yuvarlak zemin üstünde
    // sınırlandırılmış bir ikon koyar (ham PNG hücreyi taşmasın diye).
    public void SetIcon(Sprite sprite)
    {
        if (sprite == null) return;

        if (icon == null)
        {
            var discGO = new GameObject("SlotIconDisc");
            discGO.transform.SetParent(transform);
            discGO.transform.localPosition = new Vector3(0f, 0f, -0.08f);
            discGO.transform.localScale = Vector3.one * 0.78f;
            var disc = discGO.AddComponent<SpriteRenderer>();
            disc.sprite = RuntimeSprite.Circle;
            disc.color = new Color(1f, 1f, 1f, 0.95f);
            disc.sortingOrder = 1;

            var iconGO = new GameObject("SlotIcon");
            iconGO.transform.SetParent(transform);
            iconGO.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            iconGO.transform.localScale = Vector3.one * 0.56f;

            icon = iconGO.AddComponent<SpriteRenderer>();
            icon.sortingOrder = 2;
        }

        icon.sprite = sprite;
    }

    public bool HasCrateVisual => crateVisual != null;

    // Yeni açılan hücrede bazen çıkan kilitli kasanın görseli: koyu kutu + "?" işareti.
    public void ShowCrate()
    {
        if (crateVisual != null) return;

        crateVisual = new GameObject("Crate");
        crateVisual.transform.SetParent(transform);
        crateVisual.transform.localPosition = Vector3.zero;
        crateVisual.transform.localScale = Vector3.one * 0.8f;

        var sr = crateVisual.AddComponent<SpriteRenderer>();
        sr.sprite = RuntimeSprite.RoundedSquare;
        sr.color = CrateColor;
        sr.sortingOrder = 2;

        var markGO = new GameObject("Mark");
        markGO.transform.SetParent(crateVisual.transform);
        markGO.transform.localPosition = new Vector3(0f, 0f, -0.1f);

        var mark = markGO.AddComponent<TextMesh>();
        mark.anchor = TextAnchor.MiddleCenter;
        mark.alignment = TextAlignment.Center;
        mark.text = "?";
        mark.fontSize = 40;
        mark.characterSize = 0.13f;
        mark.color = Color.white;

        markGO.GetComponent<MeshRenderer>().sortingOrder = 3;
    }

    public void HideCrate()
    {
        if (crateVisual == null) return;

        Destroy(crateVisual);
        crateVisual = null;
    }

    // Sürüklenen/seçili hücrenin 4 köşesinde beliren yeşil ayraçlar (referanstaki gibi).
    public void SetSelected(bool selected)
    {
        if (selected)
        {
            if (selectionVisual != null) return;

            selectionVisual = new GameObject("Selection");
            selectionVisual.transform.SetParent(transform);
            selectionVisual.transform.localPosition = new Vector3(0f, 0f, -0.2f);

            var corners = new[]
            {
                new Vector2(-1f, 1f), new Vector2(1f, 1f),
                new Vector2(-1f, -1f), new Vector2(1f, -1f)
            };
            foreach (var c in corners)
            {
                var mark = new GameObject("Corner").AddComponent<SpriteRenderer>();
                mark.transform.SetParent(selectionVisual.transform);
                mark.transform.localPosition = new Vector3(c.x * 0.40f, c.y * 0.40f, 0f);
                mark.transform.localScale = Vector3.one * 0.24f;
                mark.sprite = RuntimeSprite.RoundedSquare;
                mark.color = SelectionColor;
                mark.sortingOrder = 6;
            }
        }
        else
        {
            if (selectionVisual == null) return;
            Destroy(selectionVisual);
            selectionVisual = null;
        }
    }

    public void AttachItemView(ItemView view)
    {
        CurrentItemView = view;
        if (view == null) return;

        view.transform.SetParent(transform);
        view.transform.localPosition = Vector3.zero;
    }

    public void ClearItemView()
    {
        CurrentItemView = null;
    }

    // Board tamamen dolduğunda çöp kutusunu dikkat çekmesi için hafifçe parlatıp/söndürür.
    public void SetPulsing(bool active)
    {
        if (active)
        {
            if (pulseRoutine != null) return;
            pulseBaseColor = background.color;
            pulseRoutine = StartCoroutine(PulseRoutine());
        }
        else
        {
            if (pulseRoutine == null) return;
            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
            background.color = pulseBaseColor;
        }
    }

    private IEnumerator PulseRoutine()
    {
        while (true)
        {
            float t = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f;
            background.color = Color.Lerp(pulseBaseColor, Color.white, t * 0.6f);
            yield return null;
        }
    }

    // Yetersiz coin/enerji gibi reddedilen bir aksiyon için kısa bir sallanma geri bildirimi.
    public void Shake()
    {
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        const float duration = 0.3f;
        const float magnitude = 0.08f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float damper = 1f - t / duration;
            float offsetX = Mathf.Sin(t * 60f) * magnitude * damper;
            transform.localPosition = basePosition + new Vector3(offsetX, 0f, 0f);
            yield return null;
        }

        transform.localPosition = basePosition;
        shakeRoutine = null;
    }
}
