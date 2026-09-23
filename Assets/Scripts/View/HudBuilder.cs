using System;
using UnityEngine;
using UnityEngine.UI;

// Referans tarzı ekran arayüzünü (sahne perdesi, koyu yuvarlak HUD paneli,
// companion portresi, görev kartı şeridi, kremli alt bar) kod içinde kurar.
// Sadece görsel iskele + statik parçalar; dinamik değerleri BoardView.RefreshUI
// döndürülen GameHud üzerinden günceller.
public static class HudBuilder
{
    private static readonly Color Panel = new Color(0.055f, 0.227f, 0.220f, 1f);
    private static readonly Color PanelInset = new Color(0.039f, 0.152f, 0.149f, 1f);
    private static readonly Color CurtainCream = new Color(0.925f, 0.851f, 0.663f, 1f);
    private static readonly Color ValanceTeal = new Color(0.184f, 0.549f, 0.475f, 1f);
    private static readonly Color Cream = new Color(0.984f, 0.957f, 0.886f, 1f);
    private static readonly Color BtnBlue = new Color(0.243f, 0.608f, 0.902f, 1f);
    private static readonly Color Gold = new Color(0.914f, 0.651f, 0.235f, 1f);
    private static readonly Color Green = new Color(0.361f, 0.761f, 0.290f, 1f);
    private static readonly Color DiscEnergy = new Color(0.184f, 0.435f, 0.561f, 1f);
    private static readonly Color DiscCoin = new Color(0.788f, 0.541f, 0.180f, 1f);
    private static readonly Color DiscGem = new Color(0.490f, 0.310f, 0.659f, 1f);
    private static readonly Color InkSoft = new Color(0.353f, 0.420f, 0.478f, 1f);
    private static readonly Color PanelText = new Color(0.93f, 0.97f, 0.95f, 1f);
    private static readonly Color TrackColor = new Color(0.831f, 0.776f, 0.639f, 1f);

    private static Font font;

    // Kart şeridi ve dock barının sabit piksel yerleşimi — BuildCardStrip/BuildDock
    // bunları kullanır, BoardView.FitCameraToBoard da AYNI sabitlerden board için
    // ayrılması gereken ekran payını hesaplar. Tek kaynak: burada tutarsızlık,
    // dar/kısa ekranlarda sipariş kutusunun kartların altında kalması demek.
    public const float CardStripTopOffset = 318f;
    public const float CardStripHeight = 158f;
    public const float DockMargin = 20f;
    public const float DockHeight = 188f;

    // BoardView'ın kamerayı sığdırırken üstte/altta HUD için gerçekten ayırması
    // gereken piksel yüksekliği (+ birazcık nefes payı). Sabit bir yüzde yerine
    // buradan okunur — böylece ekran oranı/notch farkı ne olursa olsun board
    // içeriği HUD'un altında/üstünde kalmaz.
    public static float TopZonePixelHeight(float safeTopInset) =>
        safeTopInset + CardStripTopOffset + CardStripHeight + 30f;

    public static float BottomZonePixelHeight() => DockMargin + DockHeight + 24f;

    public static GameHud Build(Transform canvas, float safeTopInset, HudActions actions)
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var hud = new GameHud();

        BuildCurtain(canvas, safeTopInset);
        BuildHeader(canvas, safeTopInset, actions, hud);
        BuildCornerButtons(canvas, safeTopInset, actions);
        BuildCompanion(canvas, safeTopInset, hud);
        BuildCardStrip(canvas, safeTopInset, actions, hud);
        BuildDock(canvas, actions, hud);

        var toastLayer = Rect("ToastLayer", canvas);
        Stretch(toastLayer, 0, 0, 0, 0);
        hud.toastLayer = toastLayer;

        return hud;
    }

    // ---------------------------------------------------------------- curtain
    private static void BuildCurtain(Transform canvas, float safeTop)
    {
        float drapeH = safeTop + 116f;

        var drape = Rect("Curtain", canvas);
        TopStretch(drape, 0f, drapeH, 0f);
        var drapeImg = drape.gameObject.AddComponent<Image>();
        drapeImg.sprite = RuntimeSprite.Curtain;
        drapeImg.type = Image.Type.Simple;
        drapeImg.color = CurtainCream;
        drapeImg.raycastTarget = false;

        var valance = Rect("Valance", drape);
        TopStretch(valance, 0f, 46f, 0f);
        var valImg = valance.gameObject.AddComponent<Image>();
        valImg.sprite = RuntimeSprite.Square;
        valImg.color = ValanceTeal;
        valImg.raycastTarget = false;
    }

    // ----------------------------------------------------------------- header
    private static void BuildHeader(Transform canvas, float safeTop, HudActions actions, GameHud hud)
    {
        const float margin = 22f;
        const float headerH = 214f;
        float top = safeTop + 76f;

        var header = Rect("Header", canvas);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.offsetMin = new Vector2(margin, -(top + headerH));
        header.offsetMax = new Vector2(-margin, -top);
        var headerImg = header.gameObject.AddComponent<Image>();
        headerImg.sprite = RuntimeSprite.Panel;
        headerImg.type = Image.Type.Sliced;
        headerImg.color = Panel;

        // --- level rozeti + ilerleme halkası ---
        var badge = Rect("Badge", header);
        TopLeft(badge, 16f, -18f, 132f, 132f);

        var ringBg = Rect("RingBg", badge);
        Stretch(ringBg, 0, 0, 0, 0);
        var ringBgImg = ringBg.gameObject.AddComponent<Image>();
        ringBgImg.sprite = RuntimeSprite.Ring;
        ringBgImg.color = new Color(1f, 1f, 1f, 0.16f);

        var ring = Rect("Ring", badge);
        Stretch(ring, 0, 0, 0, 0);
        var ringImg = ring.gameObject.AddComponent<Image>();
        ringImg.sprite = RuntimeSprite.Ring;
        ringImg.color = Green;
        ringImg.type = Image.Type.Filled;
        ringImg.fillMethod = Image.FillMethod.Radial360;
        ringImg.fillOrigin = (int)Image.Origin360.Top;
        ringImg.fillClockwise = true;
        ringImg.fillAmount = 0f;
        hud.levelRing = ringImg;

        var disc = Rect("Disc", badge);
        Stretch(disc, 14, 14, 14, 14);
        var discImg = disc.gameObject.AddComponent<Image>();
        discImg.sprite = RuntimeSprite.Circle;
        discImg.color = PanelInset;

        var badgeText = AddText(FullRect("Level", disc), "1", 44, FontStyle.Bold, PanelText, TextAnchor.MiddleCenter);
        hud.levelBadge = badgeText;

        // --- para sayaçları ---
        const float countersX = 16f + 132f + 14f;

        var counters = Rect("Counters", header);
        counters.anchorMin = new Vector2(0f, 0.5f);
        counters.anchorMax = new Vector2(1f, 0.5f);
        counters.offsetMin = new Vector2(countersX, -46f);
        counters.offsetMax = new Vector2(-18f, 46f);

        var row = counters.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 20f;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = true;

        hud.energyAmount = BuildCounter(counters, "EnergyCounter", DiscEnergy, "energyicon", "0", actions?.onPlusEnergy, out _);
        hud.coinAmount = BuildCounter(counters, "CoinCounter", DiscCoin, "coinicon", "0", actions?.onPlusCoin, out _);
        hud.gemAmount = BuildCounter(counters, "GemCounter", DiscGem, null, "0", null, out var gemGO);
        hud.gemCounter = gemGO;
        gemGO.SetActive(false); // oyunda gem yok — ileride açılabilir
    }

    // Sahne perdesi hizasındaki köşe butonları: sağ üstte mağaza, sol üstte duraklat.
    private static void BuildCornerButtons(Transform canvas, float safeTop, HudActions actions)
    {
        var shop = Rect("ShopButton", canvas);
        shop.anchorMin = shop.anchorMax = new Vector2(1f, 1f);
        shop.pivot = new Vector2(1f, 1f);
        shop.anchoredPosition = new Vector2(-24f, -(safeTop + 8f));
        shop.sizeDelta = new Vector2(96f, 96f);
        var shopImg = shop.gameObject.AddComponent<Image>();
        shopImg.sprite = RuntimeSprite.RoundedSquare;
        shopImg.type = Image.Type.Sliced;
        shopImg.color = Gold;
        WireButton(shop, actions?.onShop);
        var shopGlyph = Rect("Glyph", shop);
        Stretch(shopGlyph, 18, 18, 18, 18);
        var shopGlyphImg = shopGlyph.gameObject.AddComponent<Image>();
        shopGlyphImg.raycastTarget = false;
        var shopSprite = Resources.Load<Sprite>("Icons/coinicon");
        if (shopSprite != null) { shopGlyphImg.sprite = shopSprite; shopGlyphImg.preserveAspect = true; }
        else { shopGlyphImg.sprite = RuntimeSprite.Circle; shopGlyphImg.color = new Color(1f, 1f, 1f, 0.9f); }

        var pause = Rect("PauseButton", canvas);
        pause.anchorMin = pause.anchorMax = new Vector2(0f, 1f);
        pause.pivot = new Vector2(0f, 1f);
        pause.anchoredPosition = new Vector2(24f, -(safeTop + 8f));
        pause.sizeDelta = new Vector2(64f, 64f);
        var pauseImg = pause.gameObject.AddComponent<Image>();
        pauseImg.sprite = RuntimeSprite.Circle;
        pauseImg.color = new Color(0.055f, 0.227f, 0.220f, 0.92f);
        WireButton(pause, actions?.onPause);
        var pt = AddText(FullRect("T", pause), "II", 24, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        pt.raycastTarget = false;
    }

    private static Text BuildCounter(Transform parent, string name, Color discColor, string glyphResource,
        string amount, Action onPlus, out GameObject root)
    {
        var counter = Rect(name, parent);
        root = counter.gameObject;
        var bg = counter.gameObject.AddComponent<Image>();
        bg.sprite = RuntimeSprite.RoundedSquare;
        bg.type = Image.Type.Sliced;
        bg.color = PanelInset;

        var disc = Rect("Disc", counter);
        disc.anchorMin = new Vector2(0f, 0.5f);
        disc.anchorMax = new Vector2(0f, 0.5f);
        disc.pivot = new Vector2(0f, 0.5f);
        disc.anchoredPosition = new Vector2(8f, 0f);
        disc.sizeDelta = new Vector2(60f, 60f);
        var discImg = disc.gameObject.AddComponent<Image>();
        discImg.sprite = RuntimeSprite.Circle;
        discImg.color = discColor;

        if (!string.IsNullOrEmpty(glyphResource))
        {
            var glyphSprite = Resources.Load<Sprite>($"Icons/{glyphResource}");
            if (glyphSprite != null)
            {
                var glyph = Rect("Glyph", disc);
                Stretch(glyph, 8, 8, 8, 8);
                var glyphImg = glyph.gameObject.AddComponent<Image>();
                glyphImg.sprite = glyphSprite;
                glyphImg.preserveAspect = true;
            }
        }

        var text = AddText(Rect("Amount", counter), amount, 32, FontStyle.Bold, PanelText, TextAnchor.MiddleLeft);
        var tr = (RectTransform)text.transform;
        Stretch(tr, 78, 0, 40, 0);

        var plus = Rect("Plus", counter);
        plus.anchorMin = new Vector2(1f, 0.5f);
        plus.anchorMax = new Vector2(1f, 0.5f);
        plus.pivot = new Vector2(0.5f, 0.5f);
        plus.anchoredPosition = new Vector2(-6f, 0f);
        plus.sizeDelta = new Vector2(46f, 46f);
        var plusImg = plus.gameObject.AddComponent<Image>();
        plusImg.sprite = RuntimeSprite.Circle;
        plusImg.color = Green;
        WireButton(plus, onPlus);
        var plusText = AddText(FullRect("T", plus), "+", 30, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        plusText.raycastTarget = false;

        return text;
    }

    // -------------------------------------------------------------- companion
    private static void BuildCompanion(Transform canvas, float safeTop, GameHud hud)
    {
        const float size = 118f;

        var comp = Rect("Companion", canvas);
        comp.anchorMin = new Vector2(1f, 1f);
        comp.anchorMax = new Vector2(1f, 1f);
        comp.pivot = new Vector2(1f, 1f);
        comp.anchoredPosition = new Vector2(-16f, -(safeTop + 292f));
        comp.sizeDelta = new Vector2(size, size + 36f);

        var ring = Rect("Ring", comp);
        TopLeft(ring, 0f, 0f, size, size);
        var ringImg = ring.gameObject.AddComponent<Image>();
        ringImg.sprite = RuntimeSprite.Circle;
        ringImg.color = Gold;

        var inner = Rect("Inner", ring);
        Stretch(inner, 9, 9, 9, 9);
        var innerImg = inner.gameObject.AddComponent<Image>();
        innerImg.sprite = RuntimeSprite.Circle;
        innerImg.color = new Color(0.62f, 0.80f, 0.87f, 1f);

        var face = Rect("Face", inner);
        Stretch(face, 6, 6, 6, 6);
        var faceImg = face.gameObject.AddComponent<Image>();
        var faceSprite = Resources.Load<Sprite>("Icons/arnav");
        if (faceSprite != null) { faceImg.sprite = faceSprite; faceImg.preserveAspect = true; }
        else { faceImg.sprite = RuntimeSprite.Circle; faceImg.color = new Color(0.55f, 0.73f, 0.82f, 1f); }
        hud.companionFace = faceImg;

        var timer = Rect("Timer", comp);
        timer.anchorMin = new Vector2(0.5f, 1f);
        timer.anchorMax = new Vector2(0.5f, 1f);
        timer.pivot = new Vector2(0.5f, 1f);
        timer.anchoredPosition = new Vector2(0f, -(size - 12f));
        timer.sizeDelta = new Vector2(size + 18f, 34f);
        var timerImg = timer.gameObject.AddComponent<Image>();
        timerImg.sprite = RuntimeSprite.RoundedSquare;
        timerImg.type = Image.Type.Sliced;
        timerImg.color = Panel;
        hud.companionTimer = AddText(FullRect("T", timer), "", 19, FontStyle.Bold, new Color(0.82f, 0.92f, 0.88f), TextAnchor.MiddleCenter);
    }

    // ------------------------------------------------------------- card strip
    private static void BuildCardStrip(Transform canvas, float safeTop, HudActions actions, GameHud hud)
    {
        const float margin = 22f;
        const float stripH = CardStripHeight;
        float top = safeTop + CardStripTopOffset;

        var strip = Rect("CardStrip", canvas);
        strip.anchorMin = new Vector2(0f, 1f);
        strip.anchorMax = new Vector2(1f, 1f);
        strip.offsetMin = new Vector2(margin, -(top + stripH));
        strip.offsetMax = new Vector2(-(margin + 140f), -top);

        var row = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 12f;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = true;

        hud.orderCard = BuildTaskCard(strip, "OrderCard", "SİPARİŞ", false, null, actions?.onOrderInfo);
        hud.questCard = BuildTaskCard(strip, "QuestCard", "SEVİYE", false, null, actions?.onQuestInfo);
        hud.areaCard = BuildTaskCard(strip, "AreaCard", "ALAN", false, null, actions?.onAreaInfo);
        hud.dailyCard = BuildTaskCard(strip, "DailyCard", "GÜNLÜK", true, actions?.onDailyClaim, null);

        // günlük ödül kartındaki "bekleyen ödül" rozeti
        var badge = Rect("Badge", hud.dailyCard.root.transform);
        badge.anchorMin = new Vector2(1f, 1f);
        badge.anchorMax = new Vector2(1f, 1f);
        badge.pivot = new Vector2(0.5f, 0.5f);
        badge.anchoredPosition = new Vector2(-2f, -2f);
        badge.sizeDelta = new Vector2(36f, 36f);
        var badgeImg = badge.gameObject.AddComponent<Image>();
        badgeImg.sprite = RuntimeSprite.Circle;
        badgeImg.color = new Color(0.898f, 0.325f, 0.243f, 1f);
        var bt = AddText(FullRect("T", badge), "!", 20, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        bt.raycastTarget = false;
        hud.dailyBadge = badge.gameObject;
    }

    private static TaskCard BuildTaskCard(Transform parent, string name, string title, bool isClaim, Action onClaim, Action onTap)
    {
        var card = new TaskCard();
        var root = Rect(name, parent);
        card.root = root.gameObject;
        var bg = root.gameObject.AddComponent<Image>();
        bg.sprite = RuntimeSprite.RoundedSquare;
        bg.type = Image.Type.Sliced;
        bg.color = Cream;
        if (onTap != null) WireButton(root, onTap);

        var titleText = AddText(Rect("Title", root), title, 16, FontStyle.Bold, new Color(0.52f, 0.41f, 0.22f), TextAnchor.MiddleCenter);
        var titleRt = (RectTransform)titleText.transform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -12f);
        titleRt.sizeDelta = new Vector2(-10f, 22f);
        titleText.raycastTarget = false;

        if (isClaim)
        {
            var btn = Rect("Claim", root);
            btn.anchorMin = new Vector2(0.5f, 0.5f);
            btn.anchorMax = new Vector2(0.5f, 0.5f);
            btn.pivot = new Vector2(0.5f, 0.5f);
            btn.anchoredPosition = new Vector2(0f, -6f);
            btn.sizeDelta = new Vector2(150f, 56f);
            var btnImg = btn.gameObject.AddComponent<Image>();
            btnImg.sprite = RuntimeSprite.RoundedSquare;
            btnImg.type = Image.Type.Sliced;
            btnImg.color = Green;
            WireButton(btn, onClaim);
            var claimText = AddText(FullRect("T", btn), "Al", 26, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            claimText.raycastTarget = false;
        }
        else
        {
            card.valueLabel = AddText(Rect("Value", root), "0/0", 27, FontStyle.Bold, new Color(0.17f, 0.22f, 0.27f), TextAnchor.MiddleCenter);
            var valRt = (RectTransform)card.valueLabel.transform;
            valRt.anchorMin = new Vector2(0f, 0.5f);
            valRt.anchorMax = new Vector2(1f, 0.5f);
            valRt.pivot = new Vector2(0.5f, 0.5f);
            valRt.anchoredPosition = new Vector2(0f, 8f);
            valRt.sizeDelta = new Vector2(-8f, 34f);
            card.valueLabel.raycastTarget = false;

            var track = Rect("Track", root);
            track.anchorMin = new Vector2(0f, 0f);
            track.anchorMax = new Vector2(1f, 0f);
            track.pivot = new Vector2(0.5f, 0f);
            track.anchoredPosition = new Vector2(0f, 34f);
            track.sizeDelta = new Vector2(-28f, 12f);
            var trackImg = track.gameObject.AddComponent<Image>();
            trackImg.sprite = RuntimeSprite.RoundedSquare;
            trackImg.type = Image.Type.Sliced;
            trackImg.color = TrackColor;
            trackImg.raycastTarget = false;

            var fill = Rect("Fill", track);
            Stretch(fill, 0, 0, 0, 0);
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.sprite = RuntimeSprite.RoundedSquare;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 0f;
            fillImg.color = Green;
            fillImg.raycastTarget = false;
            card.fill = fillImg;

            card.subtitle = AddText(Rect("Subtitle", root), "", 15, FontStyle.Bold, new Color(0.42f, 0.37f, 0.29f), TextAnchor.MiddleCenter);
            var subRt = (RectTransform)card.subtitle.transform;
            subRt.anchorMin = new Vector2(0f, 0f);
            subRt.anchorMax = new Vector2(1f, 0f);
            subRt.pivot = new Vector2(0.5f, 0f);
            subRt.anchoredPosition = new Vector2(0f, 9f);
            subRt.sizeDelta = new Vector2(-6f, 20f);
            card.subtitle.raycastTarget = false;
        }

        return card;
    }

    // -------------------------------------------------------------------- dock
    private static void BuildDock(Transform canvas, HudActions actions, GameHud hud)
    {
        const float margin = DockMargin;
        const float dockH = DockHeight;

        var dock = Rect("Dock", canvas);
        dock.anchorMin = new Vector2(0f, 0f);
        dock.anchorMax = new Vector2(1f, 0f);
        dock.offsetMin = new Vector2(margin, margin);
        dock.offsetMax = new Vector2(-margin, margin + dockH);
        var dockImg = dock.gameObject.AddComponent<Image>();
        dockImg.sprite = RuntimeSprite.Panel;
        dockImg.type = Image.Type.Sliced;
        dockImg.color = Cream;

        BuildRoundButton(dock, "Briefcase", new Vector2(0f, 0.5f), new Vector2(18f, 0f), "package", "", actions?.onBriefcase);
        BuildRoundButton(dock, "MapButton", new Vector2(1f, 0.5f), new Vector2(-18f, 0f), "harita", "", actions?.onMap);
        BuildRoundButton(dock, "SellButton", new Vector2(1f, 0.5f), new Vector2(-152f, 0f), null, "$", actions?.onSell);

        var info = Rect("Info", dock);
        info.anchorMin = new Vector2(0f, 0f);
        info.anchorMax = new Vector2(1f, 1f);
        info.offsetMin = new Vector2(150f, 12f);
        info.offsetMax = new Vector2(-286f, -12f);

        var title = AddText(Rect("Title", info), "", 30, FontStyle.Bold, Gold, TextAnchor.LowerLeft);
        var titleRt = (RectTransform)title.transform;
        titleRt.anchorMin = new Vector2(0f, 0.5f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;
        hud.dockTitle = title;

        var hint = AddText(Rect("Hint", info), "", 24, FontStyle.Bold, InkSoft, TextAnchor.UpperLeft);
        var hintRt = (RectTransform)hint.transform;
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(1f, 0.5f);
        hintRt.offsetMin = Vector2.zero;
        hintRt.offsetMax = Vector2.zero;
        hud.dockHint = hint;
    }

    private static void BuildRoundButton(Transform parent, string name, Vector2 anchor, Vector2 pos,
        string iconResource, string glyph, Action onClick)
    {
        var btn = Rect(name, parent);
        btn.anchorMin = anchor;
        btn.anchorMax = anchor;
        btn.pivot = new Vector2(anchor.x, 0.5f);
        btn.anchoredPosition = pos;
        btn.sizeDelta = new Vector2(120f, 120f);
        var img = btn.gameObject.AddComponent<Image>();
        img.sprite = RuntimeSprite.Circle;
        img.color = BtnBlue;
        WireButton(btn, onClick);

        var sprite = string.IsNullOrEmpty(iconResource) ? null : Resources.Load<Sprite>($"Icons/{iconResource}");
        if (sprite != null)
        {
            var ic = Rect("Icon", btn);
            Stretch(ic, 24, 24, 24, 24);
            var icImg = ic.gameObject.AddComponent<Image>();
            icImg.sprite = sprite;
            icImg.preserveAspect = true;
            icImg.raycastTarget = false;
        }
        else
        {
            var t = AddText(FullRect("T", btn), glyph, 44, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            t.raycastTarget = false;
        }
    }

    // --------------------------------------------------------------- helpers
    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static RectTransform FullRect(string name, Transform parent)
    {
        var rt = Rect(name, parent);
        Stretch(rt, 0, 0, 0, 0);
        return rt;
    }

    private static void Stretch(RectTransform rt, float l, float t, float r, float b)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(l, b);
        rt.offsetMax = new Vector2(-r, -t);
    }

    private static void TopStretch(RectTransform rt, float xInset, float height, float yOffset)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(xInset, -height - yOffset);
        rt.offsetMax = new Vector2(-xInset, -yOffset);
    }

    private static void TopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static Text AddText(RectTransform rt, string content, int size, FontStyle style, Color color, TextAnchor anchor)
    {
        var text = rt.gameObject.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = true;
        return text;
    }

    private static void WireButton(RectTransform rt, Action onClick)
    {
        var btn = rt.gameObject.AddComponent<Button>();
        var img = rt.GetComponent<Image>();
        if (img != null) btn.targetGraphic = img;
        if (onClick != null) btn.onClick.AddListener(() => onClick());
    }
}
