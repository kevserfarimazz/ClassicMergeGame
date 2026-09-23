using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Sahne kurulumu gerektirmeyen prototip kontrolcüsü: grid'i kod içinde üretir,
// MergeBoard'u sarmalar, fare ile sürükle-bırak girdisini yönetir.
public class BoardView : MonoBehaviour
{
    public ItemChainSO[] chains;
    public int width = 6;
    public int height = 8;
    public float cellSize = 1.1f;

    [Header("Generator")]
    public float generatorInterval = 0.8f;
    public float generatorIntervalMin = 0.5f;
    public float generatorIntervalDecayPerUnlock = 0.05f;
    public float[] spawnTierWeights = { 5f, 1f };

    [Header("Economy")]
    public int startingCoins = 50;
    public int baseUnlockCost = 15;

    [Header("Orders")]
    public int orderMinCount = 2;
    public int orderMaxCount = 5;
    public int orderRewardMultiplier = 5;

    [Header("Level")]
    public int ordersRequiredForLevelUp = 3;
    public int levelUpCoinBonus = 200;
    public int levelsPerTheme = 3;

    // Level atlamak için board'un tamamının açık olması yerine, level'e göre
    // kademeli artan bir yüzdesi yeterli olur. Erken levellerde daha az
    // hücre açman gerekir, ilerledikçe %100'e doğru yaklaşır.
    [Range(0f, 1f)] public float areaRequiredAtLevel1 = 0.4f;
    public float areaRequiredGrowthPerLevel = 0.1f;

    // Sipariş isteyebileceği en yüksek tier'ı elle her level için yazmak yerine
    // otomatik hesaplar: level 1'de sadece düşük tier'lar istenir, her
    // "levelsPerTierIncrease" level'de bir sipariş bir üst tier'a açılır.
    // Örn. varsayılanlarla level 1-2: tier 0-1, level 3-4: tier 0-2, ...
    public int orderMaxTierAtLevel1 = 1;
    public int levelsPerTierIncrease = 2;

    // LevelDirector: sipariş hedefi ve sipariş adedi level ilerledikçe (yukarıdaki
    // sabitlerden başlayarak) yavaşça büyür, bu tavanlara ulaşınca düzleşir —
    // oyun hiçbir levelde imkansız hale gelmez, ama sonsuza kadar tekrar da etmez.
    [Header("Level İlerlemesi (sonsuz — LevelDirector)")]
    public int ordersRequiredCap = 6;
    public int levelsPerOrderRequirementIncrease = 15;
    public int orderCountCapBonus = 3;
    public int levelsPerOrderCountIncrease = 20;

    [Header("Energy")]
    public int maxEnergy = 100;
    public float energyRegenSeconds = 60f;

    [Header("Speed Bonus")]
    public float speedBonusThresholdSeconds = 300f;
    public int speedBonusCoinReward = 150;

    [Header("Crates")]
    [Range(0f, 1f)] public float crateSpawnChance = 0.4f;
    public int crateOpenCoinReward = 30;

    private static readonly string[] ThemeNames = { "Garaj", "Navigasyon", "Mutfak" };

    // Sahne çerçevesi (kamera arka planı) ve board panelinin tema başına tonu.
    private static readonly Color StageColor = new Color(0.121f, 0.478f, 0.420f);
    private static readonly Color[] ThemeBoardColors =
    {
        new Color(0.725f, 0.839f, 0.922f),
        new Color(0.694f, 0.808f, 0.906f),
        new Color(0.780f, 0.850f, 0.780f),
    };

    private MergeBoard board;
    private CellView[,] cellViews;
    private CellView dragSourceCell;
    private Camera cam;

    private CellView generatorCell;
    private ItemSpawner[] spawners;
    private float generatorTimer;

    private CellView trashCell;
    private CellView orderCell;

    private SpriteRenderer boardFrame;
    private SpriteRenderer boardPanel;

    private int coins;
    private int initialUnlockedCount;
    private int orderChainIndex;
    private int orderTier;
    private int orderTargetCount;
    private int orderProgress;
    private int level = 1;
    private int ordersCompletedThisLevel;

    private float energy;
    private float energyRegenTimer;
    private float levelElapsedSeconds;

    private GameHud hud;
    private Transform hudCanvas;

    // Çentikli telefonlarda ekranın tepesindeki "güvensiz" pay (canvas biriminde).
    // Start()'ta FitCameraToBoard'dan ÖNCE hesaplanır — hem kamera hem HudBuilder
    // aynı değeri kullanır, aksi halde ekran oranına göre HUD ile board'un
    // ayrıldığı yer kayar (bkz. FitCameraToBoard).
    private float safeTopInsetCanvas;

    private RectTransform levelUpBannerRect;
    private CanvasGroup levelUpBannerGroup;
    private Text levelUpBannerText;

    private float displayedCoins;

    private AudioSource audioSource;
    private AudioSource musicSource;
    private bool soundOn = true;
    private bool musicOn = true;

    private bool isPaused;
    private GameObject pausePanel;
    private Text soundToggleText;
    private Text musicToggleText;

    private GameObject dailyRewardPanel;
    private Text dailyRewardAmountText;
    private int pendingDailyRewardAmount;
    private string lastClaimDate = "";

    private string SavePath => Path.Combine(Application.persistentDataPath, "mergesave.json");

    private void Start()
    {
        cam = Camera.main;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.orthographic = true;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        // Assets/Resources/Audio/music.mp3 eklendiğinde otomatik çalmaya başlar;
        // dosya yoksa Resources.Load null döner, sessizce hiçbir şey yapılmaz.
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.volume = 0.4f;
        var musicClip = Resources.Load<AudioClip>("Audio/music");
        if (musicClip != null)
        {
            musicSource.clip = musicClip;
            musicSource.Play();
        }

        CreateBackgroundStructure();

        spawners = new ItemSpawner[chains.Length];
        for (int i = 0; i < chains.Length; i++)
        {
            spawners[i] = new ItemSpawner(chains[i], spawnTierWeights);
        }

        // Başlangıçta sadece alt sıra açık; geri kalanı coin ile açılıyor.
        var initiallyUnlocked = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
            initiallyUnlocked.Add(new Vector2Int(x, 0));

        board = new MergeBoard(width, height, initiallyUnlocked);
        initialUnlockedCount = board.CountUnlockedCells();
        cellViews = new CellView[width, height];

        coins = startingCoins;
        energy = maxEnergy;
        GenerateNewOrder();

        if (!LoadIfExists())
        {
            PlaceStarterItems();
        }

        ApplyTheme(GetThemeIndex());

        float offsetX = (width - 1) * cellSize / 2f;
        float offsetY = (height - 1) * cellSize / 2f;

        safeTopInsetCanvas = ComputeSafeTopInsetCanvas();
        FitCameraToBoard(offsetX, offsetY);
        LayoutBoardPanel(offsetX, offsetY);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var go = new GameObject($"Cell_{x}_{y}");
                go.transform.SetParent(transform);
                go.transform.localPosition = new Vector3(x * cellSize - offsetX, y * cellSize - offsetY, 0f);

                var cellView = go.AddComponent<CellView>();
                cellView.Init(x, y);

                var modelCell = board.GetCell(x, y);
                cellView.SetLockVisual(modelCell.lockState == CellLockState.Locked);
                cellViews[x, y] = cellView;

                if (modelCell.item != null)
                {
                    var itemView = new GameObject("Item").AddComponent<ItemView>();
                    itemView.SetItem(modelCell.item);
                    cellView.AttachItemView(itemView);
                }

                if (modelCell.hasCrate)
                {
                    cellView.ShowCrate();
                }
            }
        }

        // Üretici/çöp/sipariş hücreleri board grid'inin bir parçası değil,
        // sadece grid'in etrafında duran özel amaçlı görsel slotlar.
        float slotRowOffset = offsetY + cellSize * 0.86f;
        generatorCell = CreateSpecialSlot("Generator", new Vector3(-cellSize * 0.62f, -slotRowOffset, 0f));
        generatorCell.SetGeneratorVisual();
        generatorCell.SetIcon(Resources.Load<Sprite>("Icons/ordernow"));

        trashCell = CreateSpecialSlot("Trash", new Vector3(cellSize * 0.62f, -slotRowOffset, 0f));
        trashCell.SetTrashVisual();
        trashCell.SetIcon(Resources.Load<Sprite>("Icons/recycle"));

        orderCell = CreateSpecialSlot("Order", new Vector3(0f, slotRowOffset, 0f));
        orderCell.SetOrderVisual();
        orderCell.SetIcon(Resources.Load<Sprite>("Icons/package"));

        SetupUI();
        displayedCoins = coins;
        RefreshUI();

        CheckDailyReward();
    }

    private void CheckDailyReward()
    {
        string today = System.DateTime.Now.ToString("yyyy-MM-dd");
        if (lastClaimDate == today) return;

        pendingDailyRewardAmount = 100 + (level - 1) * 25;
        dailyRewardAmountText.text = $"+{pendingDailyRewardAmount} Coin";
        dailyRewardPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    private void ClaimDailyReward()
    {
        coins += pendingDailyRewardAmount;
        lastClaimDate = System.DateTime.Now.ToString("yyyy-MM-dd");
        dailyRewardPanel.SetActive(false);
        Time.timeScale = isPaused ? 0f : 1f;
        PlaySound("levelup");
        SaveGame();
    }

    private void SetupUI()
    {
        // Tıklanabilir Button'lar için Unity'nin olay sistemi (EventSystem) gerekiyor.
        if (EventSystem.current == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }

        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        // Dikey telefon hedefliyoruz: genişliğe göre ölçekle.
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f;

        canvasGO.AddComponent<GraphicRaycaster>();
        hudCanvas = canvasGO.transform;

        hud = HudBuilder.Build(hudCanvas, safeTopInsetCanvas, new HudActions
        {
            onPause = TogglePause,
            onDailyClaim = OpenDailyReward,
            onShop = () => ShowToast("Mağaza yakında"),
            onBriefcase = () => ShowToast("Envanter yakında"),
            onSell = () => ShowToast("Satmak için parçayı çöp kutusuna sürükle"),
            onMap = () => ShowToast("Harita yakında"),
            onPlusCoin = () => ShowToast("Mağaza yakında"),
            onPlusEnergy = () => ShowToast("Mağaza yakında"),
            onOrderInfo = () =>
            {
                var t = chains[orderChainIndex].GetTier(orderTier);
                ShowToast($"Sipariş: {orderTargetCount}× {t.displayName} — birleştirip sipariş kutusuna (sağ üst) sürükle");
            },
            onQuestInfo = () => ShowToast($"Seviye atlamak için: {CurrentPlan.OrdersRequiredForLevelUp} sipariş tamamla + {GetRequiredUnlockedCells()} kare aç"),
            onAreaInfo = () => ShowToast($"Kilitli kareye dokununca {GetUnlockCost()} coin'e açılır. Seviye için {GetRequiredUnlockedCells()} kare gerekiyor."),
        });

        SetupLevelUpBanner(hudCanvas);
        SetupPauseMenu(hudCanvas);
        SetupDailyRewardPanel(hudCanvas);
    }

    private void SetupPauseMenu(Transform canvasParent)
    {
        pausePanel = new GameObject("PausePanel");
        pausePanel.transform.SetParent(canvasParent, false);
        var overlayRect = pausePanel.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        var overlayImage = pausePanel.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.75f);

        var cardGO = new GameObject("Card");
        cardGO.transform.SetParent(pausePanel.transform, false);
        var cardRect = cardGO.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(320f, 300f);
        var cardImage = cardGO.AddComponent<Image>();
        cardImage.sprite = RuntimeSprite.RoundedSquare;
        cardImage.type = Image.Type.Sliced;
        cardImage.color = new Color(0.09f, 0.11f, 0.28f, 0.97f);

        var titleText = CreateUIText(cardRect, new Vector2(20f, -24f), 24, FontStyle.Bold);
        titleText.text = "Duraklatıldı";

        soundToggleText = CreatePauseMenuButton(cardRect, new Vector2(20f, -80f), new Vector2(280f, 50f), ToggleSound);
        soundToggleText.text = soundOn ? "Ses: Açık" : "Ses: Kapalı";

        musicToggleText = CreatePauseMenuButton(cardRect, new Vector2(20f, -140f), new Vector2(280f, 50f), ToggleMusic);
        musicToggleText.text = musicOn ? "Müzik: Açık" : "Müzik: Kapalı";

        var resumeText = CreatePauseMenuButton(cardRect, new Vector2(20f, -200f), new Vector2(280f, 50f), TogglePause);
        resumeText.text = "Devam Et";

        pausePanel.SetActive(false);
    }

    private Text CreatePauseMenuButton(Transform parent, Vector2 anchoredPos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Button");
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        var image = go.AddComponent<Image>();
        image.sprite = RuntimeSprite.RoundedSquare;
        image.type = Image.Type.Sliced;
        image.color = new Color(0.17f, 0.21f, 0.42f, 1f);

        var button = go.AddComponent<Button>();
        button.onClick.AddListener(onClick);

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 18;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;

        return text;
    }

    private void TogglePause()
    {
        isPaused = !isPaused;
        pausePanel.SetActive(isPaused);
        Time.timeScale = isPaused ? 0f : 1f;
    }

    private void ToggleSound()
    {
        soundOn = !soundOn;
        audioSource.mute = !soundOn;
        soundToggleText.text = soundOn ? "Ses: Açık" : "Ses: Kapalı";
        SaveGame();
    }

    private void ToggleMusic()
    {
        musicOn = !musicOn;
        musicSource.mute = !musicOn;
        musicToggleText.text = musicOn ? "Müzik: Açık" : "Müzik: Kapalı";
        SaveGame();
    }

    private void SetupDailyRewardPanel(Transform canvasParent)
    {
        dailyRewardPanel = new GameObject("DailyRewardPanel");
        dailyRewardPanel.transform.SetParent(canvasParent, false);
        var overlayRect = dailyRewardPanel.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        var overlayImage = dailyRewardPanel.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.75f);

        var cardGO = new GameObject("Card");
        cardGO.transform.SetParent(dailyRewardPanel.transform, false);
        var cardRect = cardGO.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(320f, 280f);
        var cardImage = cardGO.AddComponent<Image>();
        cardImage.sprite = RuntimeSprite.RoundedSquare;
        cardImage.type = Image.Type.Sliced;
        cardImage.color = new Color(0.09f, 0.11f, 0.28f, 0.97f);

        var titleText = CreateUIText(cardRect, new Vector2(20f, -24f), 22, FontStyle.Bold);
        titleText.text = "Günlük Ödül!";

        dailyRewardAmountText = CreateUIText(cardRect, new Vector2(20f, -90f), 26, FontStyle.Bold);
        dailyRewardAmountText.color = new Color(1f, 0.84f, 0.32f);

        var claimText = CreatePauseMenuButton(cardRect, new Vector2(20f, -180f), new Vector2(280f, 50f), ClaimDailyReward);
        claimText.text = "Al!";

        dailyRewardPanel.SetActive(false);
    }

    private void SetupLevelUpBanner(Transform canvasParent)
    {
        var go = new GameObject("LevelUpBanner");
        go.transform.SetParent(canvasParent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(480f, 190f);

        var group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var bg = go.AddComponent<Image>();
        bg.sprite = RuntimeSprite.RoundedSquare;
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.06f, 0.07f, 0.20f, 0.95f);

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.84f, 0.32f);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        levelUpBannerRect = rect;
        levelUpBannerGroup = group;
        levelUpBannerText = text;
    }

    private Text CreateUIText(Transform parent, Vector2 anchoredPos, int fontSize, FontStyle fontStyle)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(328f, 34f);

        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        return text;
    }

    private void RefreshUI()
    {
        int req = Mathf.Max(1, CurrentPlan.OrdersRequiredForLevelUp);
        float questFill = Mathf.Clamp01(ordersCompletedThisLevel / (float)req);

        hud.levelBadge.text = level.ToString();
        hud.levelRing.fillAmount = questFill;

        // Coin sayısı hedefe anında zıplamak yerine hızlıca sayarak ulaşır.
        displayedCoins = Mathf.MoveTowards(displayedCoins, coins, Mathf.Max(20f, Mathf.Abs(coins - displayedCoins)) * 8f * Time.deltaTime);
        hud.coinAmount.text = Mathf.RoundToInt(displayedCoins).ToString();

        int energyDisplay = Mathf.FloorToInt(energy);
        hud.energyAmount.text = $"{energyDisplay}/{maxEnergy}";

        float secondsToNext = energyRegenSeconds - energyRegenTimer;
        hud.companionTimer.text = energyDisplay >= maxEnergy
            ? "Dolu"
            : FormatDuration(secondsToNext);

        var orderChain = chains[orderChainIndex];
        var orderTierData = orderChain.GetTier(orderTier);
        SetTaskCard(hud.orderCard, orderProgress, orderTargetCount, $"{orderTargetCount}× {orderTierData.displayName}");
        SetTaskCard(hud.questCard, ordersCompletedThisLevel, req, "sipariş tamamla");
        SetTaskCard(hud.areaCard, board.CountUnlockedCells(), GetRequiredUnlockedCells(), "kare açık");

        bool dailyPending = lastClaimDate != System.DateTime.Now.ToString("yyyy-MM-dd");
        if (hud.dailyBadge != null && hud.dailyBadge.activeSelf != dailyPending)
        {
            hud.dailyBadge.SetActive(dailyPending);
        }

        // Alt bar: duruma göre değişen bir "ne yapmalıyım" koçu.
        string dockTitle, dockHint;
        if (generatorCell.CurrentItemView != null && !board.HasAnyEmptyUnlockedCell())
        {
            dockTitle = "Tahta doldu!";
            dockHint = "Parçaları birleştir ya da çöp kutusuna sürükle";
        }
        else if (energy < 1f)
        {
            dockTitle = "Enerji bitti";
            dockHint = "Birleştirmek için enerji dolmasını bekle";
        }
        else if (BoardHasItem(orderChain, orderTier))
        {
            dockTitle = "Sipariş hazır!";
            dockHint = $"{orderTierData.displayName} parçasını sipariş kutusuna sürükle";
        }
        else
        {
            dockTitle = "Parçaları birleştir";
            dockHint = $"Sipariş: {orderTargetCount}× {orderTierData.displayName} — aynıları üst üste getir";
        }
        hud.dockTitle.text = dockTitle;
        hud.dockHint.text = dockHint;

        // Board dolup üretici parçayı yerleştiremez hale gelince çöp kutusunu vurgula.
        bool boardStuck = generatorCell.CurrentItemView != null && !board.HasAnyEmptyUnlockedCell();
        trashCell.SetPulsing(boardStuck);
    }

    private static void SetTaskCard(TaskCard card, int have, int need, string subtitle)
    {
        if (card == null) return;
        need = Mathf.Max(1, need);
        if (card.fill != null) card.fill.fillAmount = Mathf.Clamp01(have / (float)need);
        if (card.valueLabel != null) card.valueLabel.text = $"{have}/{need}";
        if (card.subtitle != null) card.subtitle.text = subtitle;
    }

    // Board'da belirli zincir+tier'a sahip bir parça var mı (sipariş teslim edilebilir mi)?
    private bool BoardHasItem(ItemChainSO chain, int tier)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var it = board.GetCell(x, y)?.item;
                if (it != null && it.chain == chain && it.tierIndex == tier) return true;
            }
        }
        return false;
    }

    private static string FormatDuration(float seconds)
    {
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int h = total / 3600;
        int m = (total % 3600) / 60;
        int s = total % 60;
        if (h > 0) return $"{h}s {m}dk";
        if (m > 0) return $"{m}dk {s:00}sn";
        return $"{s} sn";
    }

    // Kart şeridindeki "Al" butonu: bekleyen günlük ödül varsa panelini açar.
    private void OpenDailyReward()
    {
        if (dailyRewardPanel.activeSelf) return;

        if (lastClaimDate == System.DateTime.Now.ToString("yyyy-MM-dd"))
        {
            ShowToast("Bugünkü ödülü zaten aldın");
            return;
        }

        if (pendingDailyRewardAmount <= 0)
        {
            pendingDailyRewardAmount = 100 + (level - 1) * 25;
        }
        dailyRewardAmountText.text = $"+{pendingDailyRewardAmount} Coin";
        dailyRewardPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    // Ekranın altında kısa süre görünüp kaybolan bilgi baloncuğu (henüz hazır
    // olmayan mağaza/harita gibi butonlar ve küçük geri bildirimler için).
    private void ShowToast(string message)
    {
        if (hud == null || hud.toastLayer == null) return;

        var go = new GameObject("Toast", typeof(RectTransform));
        go.transform.SetParent(hud.toastLayer, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 340f);
        rect.sizeDelta = new Vector2(640f, 84f);

        var img = go.AddComponent<Image>();
        img.sprite = RuntimeSprite.RoundedSquare;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.04f, 0.16f, 0.15f, 0.94f);
        img.raycastTarget = false;

        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        var trect = (RectTransform)textGO.transform;
        trect.anchorMin = Vector2.zero;
        trect.anchorMax = Vector2.one;
        trect.offsetMin = new Vector2(20f, 0f);
        trect.offsetMax = new Vector2(-20f, 0f);
        var text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = message;
        text.fontSize = 26;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.95f, 0.98f, 0.96f);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;

        var group = go.AddComponent<CanvasGroup>();
        StartCoroutine(ToastRoutine(rect, group));
    }

    private IEnumerator ToastRoutine(RectTransform rect, CanvasGroup group)
    {
        Vector2 basePos = rect.anchoredPosition;

        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / 0.2f);
            group.alpha = p;
            rect.anchoredPosition = basePos + new Vector2(0f, (1f - p) * -20f);
            yield return null;
        }
        group.alpha = 1f;
        rect.anchoredPosition = basePos;

        yield return new WaitForSecondsRealtime(1.4f);

        t = 0f;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = 1f - Mathf.Clamp01(t / 0.3f);
            yield return null;
        }
        Destroy(rect.gameObject);
    }

    // Board'un arkasındaki gökyüzü mavisi yuvarlak paneli (beyaz çerçeveli) bir
    // kere kurar; boyutu LayoutBoardPanel, rengi ApplyTheme ile ayarlanır.
    private void CreateBackgroundStructure()
    {
        boardFrame = CreateSpriteChild("BoardFrame", new Color(1f, 1f, 1f, 0.9f), -12);
        boardPanel = CreateSpriteChild("BoardPanel", Color.white, -11);
    }

    private SpriteRenderer CreateSpriteChild(string name, Color color, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        go.transform.localPosition = new Vector3(0f, 0f, 0.5f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RuntimeSprite.Panel;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = Vector2.one;
        return sr;
    }

    // Board panelini grid + kenar boşluğu + üstteki sipariş / alttaki üretici-çöp
    // sıralarını içine alacak şekilde boyutlar ve ortalar.
    private void LayoutBoardPanel(float offsetX, float offsetY)
    {
        float w = width * cellSize + 0.5f;
        float h = (height + 1.55f) * cellSize + 0.25f;

        boardPanel.size = new Vector2(w, h);
        boardFrame.size = new Vector2(w + 0.22f, h + 0.22f);
    }

    // Level'e göre hesaplanan temayı uygular: sahne çerçevesi + board paneli tonu.
    private void ApplyTheme(int themeIndex)
    {
        cam.backgroundColor = StageColor;
        boardPanel.color = ThemeBoardColors[themeIndex];
        boardFrame.color = new Color(1f, 1f, 1f, 0.9f);
    }

    // O anki levelin tüm zorluk/ödül parametreleri — TEK kaynak LevelDirector.
    // Level 1000 olsa da aynı formülle, elle yazılmış bir istisnaya gerek kalmadan çalışır.
    private LevelDirector.Plan CurrentPlan => LevelDirector.For(level, new LevelDirector.Config
    {
        ThemeCount = ThemeNames.Length,
        ChainCount = chains.Length,
        LevelsPerTheme = levelsPerTheme,
        AreaRequiredAtLevel1 = areaRequiredAtLevel1,
        AreaRequiredGrowthPerLevel = areaRequiredGrowthPerLevel,
        OrdersRequiredAtLevel1 = ordersRequiredForLevelUp,
        OrdersRequiredCap = ordersRequiredCap,
        LevelsPerOrderRequirementIncrease = levelsPerOrderRequirementIncrease,
        OrderMinCountAtLevel1 = orderMinCount,
        OrderMaxCountAtLevel1 = orderMaxCount,
        OrderCountCapBonus = orderCountCapBonus,
        LevelsPerOrderCountIncrease = levelsPerOrderCountIncrease,
        OrderMaxTierAtLevel1 = orderMaxTierAtLevel1,
        LevelsPerTierIncrease = levelsPerTierIncrease,
        LevelUpCoinBonusBase = levelUpCoinBonus,
    });

    private int GetThemeIndex() => CurrentPlan.ThemeIndex;

    private int ActiveChainCount => Mathf.Min(chains.Length, CurrentPlan.ActiveChainCount);

    // Kamerayı ekran oranına göre ayarlar: board + üretici/çöp/sipariş hücreleri
    // ekranın üst (perde + HUD + kart şeridi) ve alt (dock barı) payları
    // ARASINDAKI banda tam ortalanır. Böylece HUD sabit ekran oranıyla çizilir,
    // eski geri-besleme döngüsüne gerek kalmaz.
    private float ComputeSafeTopInsetCanvas()
    {
        if (Screen.width <= 0) return 0f;

        float unsafeTopPixels = Screen.height - Screen.safeArea.yMax;
        float scaleFactor = Screen.width / 1080f;
        return scaleFactor > 0f ? unsafeTopPixels / scaleFactor : 0f;
    }

    private void FitCameraToBoard(float offsetX, float offsetY)
    {
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);

        float contentHalfWidth = offsetX + cellSize * 0.5f;
        // Grid + üstte sipariş kutusu + altta üretici/çöp sırası (grid kenarına
        // iyice yaklaştırıldı) + küçük nefes payı.
        float contentHalfHeight = offsetY + cellSize * 1.36f;

        // HUD'un üstte (perde+header+kart şeridi) ve altta (dock) GERÇEKTE
        // kapladığı piksel yüksekliğinden pay çıkarılır — sabit bir yüzde
        // yerine HudBuilder'ın kendi sabitlerinden okunur. Böylece ekran oranı
        // (ya da notch payı) ne olursa olsun sipariş kutusu kart şeridinin
        // altında/gerisinde kalmaz: HUD ne kadar yer kaplıyorsa kamera board'a
        // tam o kadarını bırakıyor.
        float canvasHeight = 1080f / Mathf.Max(0.01f, aspect);
        float topFraction = Mathf.Clamp(HudBuilder.TopZonePixelHeight(safeTopInsetCanvas) / canvasHeight, 0.20f, 0.48f);
        float bottomFraction = Mathf.Clamp(HudBuilder.BottomZonePixelHeight() / canvasHeight, 0.08f, 0.26f);

        float band = 1f - topFraction - bottomFraction;
        float orthoForHeight = contentHalfHeight / band;
        float orthoForWidth = contentHalfWidth / aspect;
        float orthoSize = Mathf.Max(orthoForHeight, orthoForWidth);
        cam.orthographicSize = orthoSize;

        // Board bandının merkezinin ekran tabanından oranı; kamerayı buna göre kaydır.
        float bandCenterFromBottom = bottomFraction + band * 0.5f;
        var camPos = cam.transform.position;
        camPos.y = orthoSize * (1f - 2f * bandCenterFromBottom);
        cam.transform.position = camPos;
    }

    // Yeni bir oyunda board tamamen boş başlamasın diye alt sıraya birkaç
    // başlangıç parçası koyuyor. Sadece kayıt dosyası yokken çağrılır.
    private void PlaceStarterItems()
    {
        if (width < 3) return;

        var chain = chains[0];
        board.TryPlaceItem(0, 0, new ItemInstance(chain, 0));
        board.TryPlaceItem(1, 0, new ItemInstance(chain, 0));
        board.TryPlaceItem(2, 0, new ItemInstance(chain, 1));
    }

    private CellView CreateSpecialSlot(string name, Vector3 localPosition)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        go.transform.localPosition = localPosition;
        go.transform.localScale = Vector3.one * 1.06f;

        var cellView = go.AddComponent<CellView>();
        cellView.Init(-1, -1);
        return cellView;
    }

    // Sipariş kutusunun yanında duran, tamamen dekoratif (tıklanamaz) kargocu figürü.
    private void CreateCourierDecoration(Vector3 localPos)
    {
        var sprite = Resources.Load<Sprite>("Icons/courier");
        if (sprite == null) return;

        var go = new GameObject("CourierDecor");
        go.transform.SetParent(transform);
        go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one * 0.85f;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 1;
    }

    private void Update()
    {
        if (isPaused || dailyRewardPanel.activeSelf) return;

        UpdateGenerator();
        UpdateEnergy();
        levelElapsedSeconds += Time.deltaTime;
        RefreshUI();

        if (Input.GetMouseButtonDown(0))
        {
            // HUD'a (kart, buton, alt bar) dokunulduysa board etkileşimi başlatma.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            var cell = RaycastCell();
            if (cell != null && IsBoardCell(cell) && board.GetCell(cell.GridX, cell.GridY).lockState == CellLockState.Locked)
            {
                TryUnlockCell(cell);
                return;
            }

            // Boş bir özel slota dokunma → ne işe yaradığını açıkla.
            if (cell != null && !IsBoardCell(cell) && cell.CurrentItemView == null)
            {
                if (cell == generatorCell) ShowToast("Üretici: buradan çıkan parçaları tahtaya sürükle");
                else if (cell == trashCell) ShowToast("Çöp: istemediğin parçayı buraya sürükle, coin kazan");
                else if (cell == orderCell) ShowToast("Sipariş kutusu: istenen parçayı buraya teslim et");
                return;
            }

            if (cell != null && cell.CurrentItemView != null)
            {
                dragSourceCell = cell;
                dragSourceCell.SetSelected(true);
                dragSourceCell.CurrentItemView.SetSpark(false);
            }
        }
        else if (Input.GetMouseButton(0) && dragSourceCell != null)
        {
            dragSourceCell.CurrentItemView.transform.position = GetWorldPointOnBoardPlane();
        }
        else if (Input.GetMouseButtonUp(0) && dragSourceCell != null)
        {
            dragSourceCell.SetSelected(false);
            var targetCell = RaycastCell();
            HandleDrop(dragSourceCell, targetCell);
            dragSourceCell = null;
        }
    }

    private bool IsBoardCell(CellView cell) => cell != generatorCell && cell != trashCell && cell != orderCell;

    private void UpdateEnergy()
    {
        if (energy >= maxEnergy) return;

        energyRegenTimer += Time.deltaTime;
        if (energyRegenTimer < energyRegenSeconds) return;

        energyRegenTimer -= energyRegenSeconds;
        energy = Mathf.Min(maxEnergy, energy + 1f);
    }

    private void UpdateGenerator()
    {
        if (generatorCell.CurrentItemView != null) return;

        generatorTimer += Time.deltaTime;

        int extraUnlocked = board.CountUnlockedCells() - initialUnlockedCount;
        float effectiveInterval = Mathf.Max(generatorIntervalMin, generatorInterval - extraUnlocked * generatorIntervalDecayPerUnlock);
        if (generatorTimer < effectiveInterval) return;

        generatorTimer = 0f;
        var item = spawners[Random.Range(0, ActiveChainCount)].SpawnNext();
        var itemView = new GameObject("Item").AddComponent<ItemView>();
        itemView.SetItem(item);
        itemView.SetSpark(true);
        generatorCell.AttachItemView(itemView);
    }

    private void HandleDrop(CellView from, CellView to)
    {
        if (to == null || to == from)
        {
            from.AttachItemView(from.CurrentItemView);
            return;
        }

        if (to == trashCell)
        {
            HandleTrash(from);
            return;
        }

        if (to == orderCell)
        {
            HandleOrderDelivery(from);
            return;
        }

        if (to == generatorCell)
        {
            from.AttachItemView(from.CurrentItemView);
            return;
        }

        if (to.HasCrateVisual)
        {
            HandleCrateOpen(from, to);
            return;
        }

        // Bu bir merge olacaksa (hedef dolu ve aynı tier) enerji yeterli mi kontrol et;
        // yetersizse merge'i denemeden reddet, taşıma/yerleştirme işlemleri ücretsiz kalır.
        bool wouldMerge = to.CurrentItemView != null && from.CurrentItemView.Data.CanMergeWith(to.CurrentItemView.Data);
        if (wouldMerge && energy < 1f)
        {
            from.AttachItemView(from.CurrentItemView);
            to.Shake();
            ShowFloatingText(to.transform.position, "Enerji Yetersiz!", new Color(1f, 0.4f, 0.4f));
            return;
        }

        bool fromGenerator = from == generatorCell;

        var result = fromGenerator
            ? board.TryPlaceOrMergeExternal(to.GridX, to.GridY, from.CurrentItemView.Data)
            : board.TryMove(from.GridX, from.GridY, to.GridX, to.GridY);

        switch (result)
        {
            case MergeBoard.MoveResult.Moved:
            {
                var movedView = from.CurrentItemView;
                from.ClearItemView();
                to.AttachItemView(movedView);
                SaveGame();
                break;
            }
            case MergeBoard.MoveResult.Merged:
            {
                Destroy(from.CurrentItemView.gameObject);
                Destroy(to.CurrentItemView.gameObject);
                from.ClearItemView();
                to.ClearItemView();

                var mergedItem = board.GetCell(to.GridX, to.GridY).item;
                var newView = new GameObject("Item").AddComponent<ItemView>();
                newView.SetItem(mergedItem);
                to.AttachItemView(newView);

                coins += mergedItem.chain.GetTier(mergedItem.tierIndex).sellValue;
                energy = Mathf.Max(0f, energy - 1f);
                PlaySound("merge");
                SpawnBurst(to.transform.position, new Color(1f, 0.86f, 0.35f), 10, 1.3f, 0.35f, 0.13f, 0f);
                SaveGame();
                break;
            }
            case MergeBoard.MoveResult.Invalid:
            {
                from.AttachItemView(from.CurrentItemView);
                break;
            }
        }
    }

    private void HandleTrash(CellView from)
    {
        if (from.CurrentItemView == null) return;

        var item = from.CurrentItemView.Data;

        if (from != generatorCell)
        {
            board.TryRemoveItem(from.GridX, from.GridY, out _);
        }

        Destroy(from.CurrentItemView.gameObject);
        from.ClearItemView();

        coins += item.chain.GetTier(item.tierIndex).sellValue;
        PlaySound("trash");
        SaveGame();
    }

    // Kilitli bir kasaya parça sürükleyince o parçayı tüketir, kasayı açar ve
    // yerine rastgele bir tier0 parça + bonus coin bırakır.
    private void HandleCrateOpen(CellView from, CellView to)
    {
        if (from.CurrentItemView == null) return;

        if (from != generatorCell)
        {
            board.TryRemoveItem(from.GridX, from.GridY, out _);
        }

        Destroy(from.CurrentItemView.gameObject);
        from.ClearItemView();

        board.TryOpenCrate(to.GridX, to.GridY);
        to.HideCrate();

        var rewardChain = chains[Random.Range(0, ActiveChainCount)];
        var rewardItem = new ItemInstance(rewardChain, 0);
        board.TryPlaceItem(to.GridX, to.GridY, rewardItem);

        var rewardView = new GameObject("Item").AddComponent<ItemView>();
        rewardView.SetItem(rewardItem);
        to.AttachItemView(rewardView);

        coins += crateOpenCoinReward;
        PlaySound("unlock");
        SpawnBurst(to.transform.position, new Color(0.9f, 0.72f, 0.32f), 12, 1.5f, 0.4f, 0.14f, 0.3f);
        SaveGame();
    }

    private void HandleOrderDelivery(CellView from)
    {
        if (from.CurrentItemView == null) return;

        var item = from.CurrentItemView.Data;

        if (item.tierIndex != orderTier || item.chain != chains[orderChainIndex])
        {
            from.AttachItemView(from.CurrentItemView);
            return;
        }

        if (from != generatorCell)
        {
            board.TryRemoveItem(from.GridX, from.GridY, out _);
        }

        Destroy(from.CurrentItemView.gameObject);
        from.ClearItemView();

        orderProgress++;
        if (orderProgress >= orderTargetCount)
        {
            coins += orderRewardMultiplier * chains[orderChainIndex].GetTier(orderTier).sellValue * orderTargetCount;
            PlaySound("order");
            StartCoroutine(DeliveryTruckRoutine(orderCell.transform.position));
            ordersCompletedThisLevel++;

            if (!CheckLevelUp())
            {
                GenerateNewOrder();
            }
        }

        SaveGame();
    }

    // Sipariş tamamlanınca sipariş kutusunda beliren bir kargo arabası paketle
    // sağa doğru yola çıkar, kısa bir süre sonra geri döner ve kaybolur.
    // Tamamen dekoratif — coin ödülü zaten anında verilmiş oluyor.
    private IEnumerator DeliveryTruckRoutine(Vector3 startPos)
    {
        var truck = CreateTruckVisual(startPos, out var renderer);
        var offscreenRight = startPos + new Vector3(10f, 0f, 0f);

        renderer.flipX = false;
        yield return MoveTransform(truck.transform, startPos, offscreenRight, 0.6f);
        yield return new WaitForSeconds(0.25f);

        renderer.flipX = true;
        yield return MoveTransform(truck.transform, offscreenRight, startPos, 0.6f);

        SpawnBurst(startPos, new Color(1f, 0.9f, 0.5f), 8, 1.1f, 0.3f, 0.12f, 0f);
        Destroy(truck);
    }

    private IEnumerator MoveTransform(Transform t, Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / duration);
            t.position = Vector3.Lerp(from, to, p);
            yield return null;
        }

        t.position = to;
    }

    private GameObject CreateTruckVisual(Vector3 pos, out SpriteRenderer mainRenderer)
    {
        var truck = new GameObject("DeliveryCourier");
        truck.transform.position = pos;

        var courierSprite = Resources.Load<Sprite>("Icons/courier_moto");
        if (courierSprite != null)
        {
            var courierSr = truck.AddComponent<SpriteRenderer>();
            courierSr.sprite = courierSprite;
            courierSr.sortingOrder = 15;
            truck.transform.localScale = Vector3.one * 0.9f;
            mainRenderer = courierSr;
            return truck;
        }

        // Yedek: courier_moto ikonu bulunamazsa basit geometrik bir kamyon çizilir.
        var body = new GameObject("Body");
        body.transform.SetParent(truck.transform, false);
        body.transform.localScale = new Vector3(0.9f, 0.5f, 1f);
        var bodySr = body.AddComponent<SpriteRenderer>();
        bodySr.sprite = RuntimeSprite.RoundedSquare;
        bodySr.color = new Color(0.85f, 0.55f, 0.15f);
        bodySr.sortingOrder = 15;

        CreateTruckWheel(truck.transform, new Vector3(-0.3f, -0.28f, 0f));
        CreateTruckWheel(truck.transform, new Vector3(0.3f, -0.28f, 0f));

        var package = new GameObject("Package");
        package.transform.SetParent(truck.transform, false);
        package.transform.localPosition = new Vector3(0f, 0.15f, -0.1f);
        package.transform.localScale = Vector3.one * 0.28f;
        var packageSr = package.AddComponent<SpriteRenderer>();
        packageSr.sprite = RuntimeSprite.RoundedSquare;
        packageSr.color = new Color(0.95f, 0.85f, 0.6f);
        packageSr.sortingOrder = 16;

        mainRenderer = bodySr;
        return truck;
    }

    private void CreateTruckWheel(Transform parent, Vector3 localPos)
    {
        var wheel = new GameObject("Wheel");
        wheel.transform.SetParent(parent, false);
        wheel.transform.localPosition = localPos;
        wheel.transform.localScale = Vector3.one * 0.18f;

        var sr = wheel.AddComponent<SpriteRenderer>();
        sr.sprite = RuntimeSprite.RoundedSquare;
        sr.color = new Color(0.1f, 0.1f, 0.1f);
        sr.sortingOrder = 15;
    }

    // Level atlama şartı (yeterli görev + level'e göre gereken alan) sağlanmışsa level atlatır.
    // Hem sipariş tamamlandığında hem de son hücre açıldığında çağrılır — hangisi son
    // gerçekleşirse o an tetiklensin diye; iki şart da zaten sağlanmışsa bir sonraki
    // olayı beklemek zorunda kalınmaz.
    private bool CheckLevelUp()
    {
        bool areaRequirementMet = board.CountUnlockedCells() >= GetRequiredUnlockedCells();
        if (ordersCompletedThisLevel >= CurrentPlan.OrdersRequiredForLevelUp && areaRequirementMet)
        {
            LevelUp();
            return true;
        }

        return false;
    }

    // Level'e göre level atlamak için gereken açık hücre sayısını hesaplar
    // (board'un tamamı yerine, level arttıkça büyüyen bir yüzdesi).
    private int GetRequiredUnlockedCells()
    {
        return Mathf.CeilToInt(width * height * CurrentPlan.RequiredAreaFraction);
    }

    // Hem tüm hücreler açılıp hem de yeterli sayıda sipariş tamamlanınca tetiklenir:
    // level artar, board tamamen sıfırlanıp yeniden (baştaki gibi kısmen kilitli) başlar.
    private void LevelUp()
    {
        level++;
        ordersCompletedThisLevel = 0;
        coins += CurrentPlan.LevelUpCoinReward;

        // Ödüllendirici hız bonusu: level'i belli bir sürede bitirirsen ekstra coin,
        // bitiremezsen hiçbir ceza yok — sadece bonusu kaçırırsın.
        bool speedBonusEarned = levelElapsedSeconds <= speedBonusThresholdSeconds;
        if (speedBonusEarned)
        {
            coins += speedBonusCoinReward;
        }
        levelElapsedSeconds = 0f;

        PlaySound("levelup");
        StartCoroutine(LevelUpBannerRoutine(speedBonusEarned));
        SpawnConfetti();
        ApplyTheme(GetThemeIndex());

        if (generatorCell.CurrentItemView != null)
        {
            Destroy(generatorCell.CurrentItemView.gameObject);
            generatorCell.ClearItemView();
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var cellView = cellViews[x, y];
                if (cellView.CurrentItemView != null)
                {
                    Destroy(cellView.CurrentItemView.gameObject);
                    cellView.ClearItemView();
                }

                if (cellView.HasCrateVisual)
                {
                    cellView.HideCrate();
                }

                var cell = board.GetCell(x, y);
                cell.item = null;
                cell.hasCrate = false;

                bool shouldBeUnlocked = y == 0;
                cell.lockState = shouldBeUnlocked ? CellLockState.Unlocked : CellLockState.Locked;
                cellView.SetLockVisual(!shouldBeUnlocked);
            }
        }

        initialUnlockedCount = board.CountUnlockedCells();

        GenerateNewOrder();
        SaveGame();
    }

    private IEnumerator LevelUpBannerRoutine(bool speedBonusEarned)
    {
        levelUpBannerText.text = speedBonusEarned
            ? $"LEVEL UP!\nLevel {level}\n+{speedBonusCoinReward} Hız Bonusu!"
            : $"LEVEL UP!\nLevel {level}";
        levelUpBannerRect.localScale = Vector3.one * 0.4f;
        levelUpBannerGroup.alpha = 0f;

        const float popDuration = 0.25f;
        float t = 0f;
        while (t < popDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / popDuration);
            levelUpBannerRect.localScale = Vector3.one * Mathf.LerpUnclamped(0.4f, 1.05f, EaseOutBack(p));
            levelUpBannerGroup.alpha = p;
            yield return null;
        }

        levelUpBannerRect.localScale = Vector3.one;
        levelUpBannerGroup.alpha = 1f;

        yield return new WaitForSeconds(1.2f);

        const float fadeDuration = 0.3f;
        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            levelUpBannerGroup.alpha = 1f - Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }

        levelUpBannerGroup.alpha = 0f;
    }

    // Belirtilen dünya konumunda kısa süreli, kendi kendini yok eden bir parçacık patlaması oluşturur.
    private void SpawnBurst(Vector3 worldPos, Color color, int count, float speed, float lifetime, float size, float gravity)
    {
        var go = new GameObject("Burst");
        go.transform.position = worldPos;

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = color;
        main.gravityModifier = gravity;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.05f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.mainTexture = RuntimeSprite.RoundedSquare.texture;
        renderer.sortingOrder = 20;

        Destroy(go, lifetime + 0.5f);
    }

    // Level atlayınca birkaç renkli patlamayı aynı anda tetikleyerek konfeti hissi verir.
    private void SpawnConfetti()
    {
        var confettiColors = new[]
        {
            new Color(0.95f, 0.35f, 0.45f),
            new Color(0.98f, 0.78f, 0.30f),
            new Color(0.35f, 0.75f, 0.55f),
            new Color(0.35f, 0.60f, 0.95f),
            new Color(0.75f, 0.45f, 0.90f),
        };

        var origin = transform.position;
        foreach (var color in confettiColors)
        {
            SpawnBurst(origin, color, 14, 3.5f, 1.4f, 0.14f, 1.2f);
        }
    }

    // Bir aksiyon reddedildiğinde (örn. yetersiz enerji) o noktada belirip
    // yukarı süzülerek kaybolan kısa bir uyarı yazısı gösterir.
    private void ShowFloatingText(Vector3 worldPos, string message, Color color)
    {
        var go = new GameObject("FloatingText");
        go.transform.position = worldPos;

        var textMesh = go.AddComponent<TextMesh>();
        textMesh.text = message;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = 40;
        textMesh.characterSize = 0.12f;
        textMesh.color = color;
        textMesh.fontStyle = FontStyle.Bold;

        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sortingOrder = 30;

        StartCoroutine(FloatingTextRoutine(go));
    }

    private IEnumerator FloatingTextRoutine(GameObject go)
    {
        var textMesh = go.GetComponent<TextMesh>();
        var startPos = go.transform.position;
        const float duration = 0.9f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            go.transform.position = startPos + new Vector3(0f, p * 0.6f, 0f);

            var c = textMesh.color;
            c.a = 1f - p;
            textMesh.color = c;
            yield return null;
        }

        Destroy(go);
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    private void GenerateNewOrder()
    {
        orderChainIndex = Random.Range(0, ActiveChainCount);
        int maxTier = Mathf.Min(GetLevelMaxOrderTier(), chains[orderChainIndex].MaxTierIndex);
        orderTier = Random.Range(0, maxTier + 1);
        var plan = CurrentPlan;
        orderTargetCount = Random.Range(plan.OrderMinCount, plan.OrderMaxCount + 1);
        orderProgress = 0;
    }

    // "Level manager" mantığı: her level için elle bir hedef yazmak yerine,
    // level arttıkça sipariş isteyebileceği en yüksek tier'ı otomatik yükseltir.
    private int GetLevelMaxOrderTier()
    {
        return CurrentPlan.OrderMaxTierBonus;
    }

    private void TryUnlockCell(CellView cellView)
    {
        int cost = GetUnlockCost();
        if (coins < cost)
        {
            cellView.Shake();
            ShowToast($"Yetersiz coin — {cost} gerekiyor");
            return;
        }

        if (board.TryUnlockCell(cellView.GridX, cellView.GridY))
        {
            coins -= cost;
            cellView.SetLockVisual(false);
            ShowFloatingText(cellView.transform.position, $"-{cost}", new Color(1f, 0.62f, 0.36f));
            PlaySound("unlock");

            // Yeni açılan hücrede bazen boş yerine kilitli bir kasa çıkar;
            // oyuncu üzerine bir parça sürükleyip açabilir.
            if (Random.value < crateSpawnChance && board.TryMarkCrate(cellView.GridX, cellView.GridY))
            {
                cellView.ShowCrate();
            }

            CheckLevelUp();
            SaveGame();
        }
    }

    private int GetUnlockCost()
    {
        int extraUnlocked = board.CountUnlockedCells() - initialUnlockedCount;
        return baseUnlockCost * (extraUnlocked + 1);
    }

    // Ses dosyaları Assets/Resources/Audio altında olduğu için Inspector'da
    // hiçbir alanı elle atamaya gerek kalmadan isimle çağrılabiliyor.
    private void PlaySound(string clipName)
    {
        var clip = Resources.Load<AudioClip>($"Audio/{clipName}");
        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private CellView RaycastCell()
    {
        var worldPos = GetWorldPointOnBoardPlane();
        var hit = Physics2D.OverlapPoint(worldPos);
        return hit != null ? hit.GetComponent<CellView>() : null;
    }

    // Kameranın orthographic ya da perspective olmasından bağımsız çalışır:
    // fare ışınını doğrudan tahtanın bulunduğu Z=0 düzlemiyle kesiştirir.
    private Vector3 GetWorldPointOnBoardPlane()
    {
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        var plane = new Plane(Vector3.forward, Vector3.zero);

        if (plane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }

        return transform.position;
    }

    [System.Serializable]
    private class CellSaveData
    {
        public int x;
        public int y;
        public bool locked;
        public bool hasItem;
        public int chainIndex;
        public int tierIndex;
        public bool hasCrate;
    }

    [System.Serializable]
    private class SaveData
    {
        public int coins;
        public int level;
        public int ordersCompletedThisLevel;
        public int orderChainIndex;
        public int orderTier;
        public int orderTargetCount;
        public int orderProgress;
        public bool soundOn = true;
        public bool musicOn = true;
        public string lastClaimDate = "";
        public float energy;
        public string energySavedAt = "";
        public float levelElapsedSeconds;
        public List<CellSaveData> cells = new List<CellSaveData>();
    }

    private void SaveGame()
    {
        var data = new SaveData
        {
            coins = coins,
            level = level,
            ordersCompletedThisLevel = ordersCompletedThisLevel,
            orderChainIndex = orderChainIndex,
            orderTier = orderTier,
            orderTargetCount = orderTargetCount,
            orderProgress = orderProgress,
            soundOn = soundOn,
            musicOn = musicOn,
            lastClaimDate = lastClaimDate,
            energy = energy,
            energySavedAt = System.DateTime.Now.ToString("o"),
            levelElapsedSeconds = levelElapsedSeconds
        };

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var cell = board.GetCell(x, y);
                data.cells.Add(new CellSaveData
                {
                    x = x,
                    y = y,
                    locked = cell.lockState == CellLockState.Locked,
                    hasItem = cell.item != null,
                    chainIndex = cell.item != null ? System.Array.IndexOf(chains, cell.item.chain) : -1,
                    tierIndex = cell.item?.tierIndex ?? -1,
                    hasCrate = cell.hasCrate
                });
            }
        }

        File.WriteAllText(SavePath, JsonUtility.ToJson(data));
    }

    private bool LoadIfExists()
    {
        if (!File.Exists(SavePath)) return false;

        var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
        coins = data.coins;
        level = data.level > 0 ? data.level : 1;
        ordersCompletedThisLevel = data.ordersCompletedThisLevel;
        orderChainIndex = data.orderChainIndex;
        orderTier = data.orderTier;
        orderTargetCount = data.orderTargetCount;
        orderProgress = data.orderProgress;
        soundOn = data.soundOn;
        musicOn = data.musicOn;
        lastClaimDate = data.lastClaimDate ?? "";
        audioSource.mute = !soundOn;
        musicSource.mute = !musicOn;
        levelElapsedSeconds = data.levelElapsedSeconds;

        // Enerji, uygulama kapalıyken de gerçek zamana göre dolmaya devam eder.
        // energySavedAt yoksa bu alanlardan önceki eski bir kayıttır, dolu başlat.
        if (string.IsNullOrEmpty(data.energySavedAt))
        {
            energy = maxEnergy;
        }
        else
        {
            energy = data.energy;
            if (System.DateTime.TryParse(data.energySavedAt, out var savedAt))
            {
                double elapsedSeconds = (System.DateTime.Now - savedAt).TotalSeconds;
                if (elapsedSeconds > 0)
                {
                    energy += (float)(elapsedSeconds / energyRegenSeconds);
                }
            }
        }
        energy = Mathf.Clamp(energy, 0f, maxEnergy);

        // Eski kayıt dosyaları artık var olmayan bir zincire (örn. kaldırılan Yazılım hattına)
        // veya eski formatta orderTargetCount=0'a işaret edebilir; böyle durumda siparişi yenile.
        if (orderTargetCount <= 0 || orderChainIndex < 0 || orderChainIndex >= chains.Length)
        {
            GenerateNewOrder();
        }

        foreach (var c in data.cells)
        {
            var cell = board.GetCell(c.x, c.y);
            if (cell == null) continue;

            cell.lockState = c.locked ? CellLockState.Locked : CellLockState.Unlocked;
            cell.item = (c.hasItem && c.chainIndex >= 0 && c.chainIndex < chains.Length)
                ? new ItemInstance(chains[c.chainIndex], c.tierIndex)
                : null;
            cell.hasCrate = c.hasCrate;
        }

        return true;
    }
}
