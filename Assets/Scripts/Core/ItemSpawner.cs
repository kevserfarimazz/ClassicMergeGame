using System;

// Yeni parça üretimini yönetir. Ağırlıklandırılmış tier dağılımı ile
// erken oyunda hep düşük tier, ilerledikçe biraz daha yüksek tier düşürülebilir.
public class ItemSpawner
{
    private readonly ItemChainSO chain;
    private readonly float[] tierWeights;
    private readonly Random random;

    public ItemSpawner(ItemChainSO chain, float[] tierWeights = null, int seed = 0)
    {
        this.chain = chain;
        this.tierWeights = (tierWeights != null && tierWeights.Length > 0) ? tierWeights : new float[] { 1f };
        random = seed == 0 ? new Random() : new Random(seed);
    }

    public ItemInstance SpawnNext()
    {
        int tier = WeightedRandomTier();
        return new ItemInstance(chain, tier);
    }

    private int WeightedRandomTier()
    {
        float total = 0f;
        foreach (var w in tierWeights) total += w;

        float roll = (float)random.NextDouble() * total;
        float cumulative = 0f;

        for (int i = 0; i < tierWeights.Length; i++)
        {
            cumulative += tierWeights[i];
            if (roll <= cumulative) return i;
        }

        return 0;
    }
}
