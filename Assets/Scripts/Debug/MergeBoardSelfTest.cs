using UnityEngine;

// Görsel/sahne kurmadan MergeBoard mantığını hızlıca doğrulamak için.
// Boş bir GameObject'e ekleyip Play'e basmak yeterli; sonuçlar Console'a düşer.
// Chain alanını Inspector'dan atamayı unutma (en az 3 tier'lı bir ItemChainSO gerekir).
public class MergeBoardSelfTest : MonoBehaviour
{
    public ItemChainSO chain;

    private void Start()
    {
        if (chain == null || chain.tiers == null || chain.tiers.Length < 3)
        {
            Debug.LogError("MergeBoardSelfTest: en az 3 tier'lı bir ItemChainSO ata.");
            return;
        }

        var board = new MergeBoard(3, 3, new[]
        {
            new Vector2Int(0, 0),
            new Vector2Int(1, 0),
            new Vector2Int(2, 0),
        });

        Assert("Kilitli hücreye yerleştirme reddedilmeli", !board.TryPlaceItem(0, 1, new ItemInstance(chain, 0)));

        Assert("Açık hücreye yerleştirme başarılı olmalı", board.TryPlaceItem(0, 0, new ItemInstance(chain, 0)));
        Assert("Aynı tier ikinci parça yerleşmeli", board.TryPlaceItem(1, 0, new ItemInstance(chain, 0)));

        var moveResult = board.TryMove(0, 0, 1, 0);
        Assert($"Aynı tier üst üste gelince merge olmalı (sonuç: {moveResult})", moveResult == MergeBoard.MoveResult.Merged);
        Assert("Merge sonrası hedef tier +1 olmalı", board.GetCell(1, 0).item.tierIndex == 1);
        Assert("Merge sonrası kaynak hücre boşalmalı", board.GetCell(0, 0).item == null);

        Assert("Kilitli hücre açılabilmeli", board.TryUnlockCell(0, 1));
        Assert("Açılan hücre sayaca yansımalı", board.CountUnlockedCells() == 4);

        var spawner = new ItemSpawner(chain, new float[] { 3f, 1f }, seed: 42);
        var spawned = spawner.SpawnNext();
        Assert("Spawner geçerli bir tier üretmeli", spawned.tierIndex >= 0 && spawned.tierIndex <= chain.MaxTierIndex);

        TestLevelDirector();

        Debug.Log("MergeBoardSelfTest tamamlandı.");
    }

    // LevelDirector 1'den 2000'e kadar her level için çökmeden, mantıklı
    // (negatif olmayan, sipariş adedi min<=max, tavanları aşmayan) bir plan
    // üretiyor mu diye kontrol eder — "level 1000'de ne olacak?" sorusunun
    // otomatik testi.
    private void TestLevelDirector()
    {
        var cfg = new LevelDirector.Config
        {
            ThemeCount = 2,
            ChainCount = 2,
            LevelsPerTheme = 3,
            AreaRequiredAtLevel1 = 0.4f,
            AreaRequiredGrowthPerLevel = 0.1f,
            OrdersRequiredAtLevel1 = 3,
            OrdersRequiredCap = 6,
            LevelsPerOrderRequirementIncrease = 15,
            OrderMinCountAtLevel1 = 2,
            OrderMaxCountAtLevel1 = 5,
            OrderCountCapBonus = 3,
            LevelsPerOrderCountIncrease = 20,
            OrderMaxTierAtLevel1 = 1,
            LevelsPerTierIncrease = 2,
            LevelUpCoinBonusBase = 200,
        };

        bool allSane = true;
        for (int lvl = 1; lvl <= 2000; lvl++)
        {
            var plan = LevelDirector.For(lvl, cfg);
            bool sane = plan.ThemeIndex >= 0 && plan.ThemeIndex < cfg.ThemeCount
                && plan.ActiveChainCount >= 1 && plan.ActiveChainCount <= cfg.ChainCount
                && plan.OrdersRequiredForLevelUp >= 1 && plan.OrdersRequiredForLevelUp <= cfg.OrdersRequiredCap
                && plan.RequiredAreaFraction >= 0f && plan.RequiredAreaFraction <= 1f
                && plan.OrderMinCount >= 1 && plan.OrderMaxCount >= plan.OrderMinCount
                && plan.LevelUpCoinReward > 0;

            if (!sane)
            {
                allSane = false;
                Debug.LogError($"FAIL: LevelDirector.For({lvl}) mantıksız değer üretti — " +
                    $"theme={plan.ThemeIndex} chains={plan.ActiveChainCount} " +
                    $"orders={plan.OrdersRequiredForLevelUp} area={plan.RequiredAreaFraction} " +
                    $"count={plan.OrderMinCount}-{plan.OrderMaxCount} coin={plan.LevelUpCoinReward}");
                break;
            }
        }

        if (allSane)
        {
            var l1 = LevelDirector.For(1, cfg);
            var l1000 = LevelDirector.For(1000, cfg);
            Debug.Log($"OK: LevelDirector 1..2000 arası çökmeden mantıklı planlar üretti. " +
                $"(level 1: {l1.OrdersRequiredForLevelUp} sipariş/{l1.OrderMinCount}-{l1.OrderMaxCount} adet — " +
                $"level 1000: {l1000.OrdersRequiredForLevelUp} sipariş/{l1000.OrderMinCount}-{l1000.OrderMaxCount} adet)");
        }
    }

    private void Assert(string message, bool condition)
    {
        if (condition) Debug.Log($"OK: {message}");
        else Debug.LogError($"FAIL: {message}");
    }
}
