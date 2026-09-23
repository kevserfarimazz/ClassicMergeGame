using UnityEngine;

// Level ilerlemesinin TEK kaynağı. Level sayısından (1, 2, ... sınırsız) o levelin
// bütün zorluk/ödül parametrelerini deterministik olarak hesaplar — hiçbir yerde
// "level 47'de şunu yap" gibi elle yazılmış bir kural yok, bu yüzden level 1000
// ya da 100000'de de aynı formülle, çökmeden çalışır.
//
// Level 1-birkaç arası sayılar BoardView'daki eski (bu sınıftan önceki) davranışla
// BİREBİR aynı kalacak şekilde ayarlandı — sadece artık düzleşip kalmak yerine,
// yavaşça ama SONSUZA KADAR derinleşiyor (sipariş sayısı ve hedefi bir tavana
// kadar yükseliyor). Tavanlar, oyunun asla imkansız hale gelmemesi için var.
public static class LevelDirector
{
    // BoardView'ın Inspector'dan ayarladığı ham eğri parametreleri.
    public struct Config
    {
        public int ThemeCount;
        public int ChainCount;
        public int LevelsPerTheme;

        public float AreaRequiredAtLevel1;
        public float AreaRequiredGrowthPerLevel;

        public int OrdersRequiredAtLevel1;
        public int OrdersRequiredCap;
        public int LevelsPerOrderRequirementIncrease;

        public int OrderMinCountAtLevel1;
        public int OrderMaxCountAtLevel1;
        public int OrderCountCapBonus;
        public int LevelsPerOrderCountIncrease;

        public int OrderMaxTierAtLevel1;
        public int LevelsPerTierIncrease;

        public int LevelUpCoinBonusBase;
    }

    // O levelin tüm oynanış parametreleri — BoardView bunun dışında hiçbir
    // "level'e göre" hesap yapmaz, hepsi buradan okunur.
    public struct Plan
    {
        public int ThemeIndex;
        public int ActiveChainCount;
        public int OrdersRequiredForLevelUp;
        public float RequiredAreaFraction;
        public int OrderMaxTierBonus;
        public int OrderMinCount;
        public int OrderMaxCount;
        public int LevelUpCoinReward;
    }

    public static Plan For(int level, Config cfg)
    {
        level = Mathf.Max(1, level);

        int themeIndex = Mathf.Clamp((level - 1) / Mathf.Max(1, cfg.LevelsPerTheme), 0, Mathf.Max(0, cfg.ThemeCount - 1));
        int activeChains = Mathf.Clamp(themeIndex + 1, 1, Mathf.Max(1, cfg.ChainCount));

        float areaFraction = Mathf.Clamp01(cfg.AreaRequiredAtLevel1 + (level - 1) * cfg.AreaRequiredGrowthPerLevel);

        // Sipariş hedefi: yavaşça artar, tavanda düzleşir (asla oynanamaz olmaz).
        int ordersRequired = cfg.OrdersRequiredAtLevel1
            + (level - 1) / Mathf.Max(1, cfg.LevelsPerOrderRequirementIncrease);
        int ordersCap = Mathf.Max(cfg.OrdersRequiredAtLevel1, cfg.OrdersRequiredCap);
        ordersRequired = Mathf.Min(ordersRequired, ordersCap);

        int countGrowth = (level - 1) / Mathf.Max(1, cfg.LevelsPerOrderCountIncrease);
        countGrowth = Mathf.Min(countGrowth, Mathf.Max(0, cfg.OrderCountCapBonus));
        int orderMin = cfg.OrderMinCountAtLevel1 + countGrowth;
        int orderMax = cfg.OrderMaxCountAtLevel1 + countGrowth;

        int orderMaxTierBonus = cfg.OrderMaxTierAtLevel1 + (level - 1) / Mathf.Max(1, cfg.LevelsPerTierIncrease);

        int coinReward = cfg.LevelUpCoinBonusBase * level;

        return new Plan
        {
            ThemeIndex = themeIndex,
            ActiveChainCount = activeChains,
            OrdersRequiredForLevelUp = ordersRequired,
            RequiredAreaFraction = areaFraction,
            OrderMaxTierBonus = orderMaxTierBonus,
            OrderMinCount = orderMin,
            OrderMaxCount = orderMax,
            LevelUpCoinReward = coinReward,
        };
    }
}
