// Tahtaya yerleştirilmiş tek bir parçanın runtime hali (MonoBehaviour değil, düz veri).
[System.Serializable]
public class ItemInstance
{
    public ItemChainSO chain;
    public int tierIndex;

    public ItemInstance(ItemChainSO chain, int tierIndex)
    {
        this.chain = chain;
        this.tierIndex = tierIndex;
    }

    public bool CanMergeWith(ItemInstance other)
    {
        if (other == null) return false;
        return chain == other.chain
            && tierIndex == other.tierIndex
            && chain.HasNextTier(tierIndex);
    }
}
