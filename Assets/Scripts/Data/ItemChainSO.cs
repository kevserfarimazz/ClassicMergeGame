using UnityEngine;

// 🔩 -> ⚙️ -> 🔋 -> 📱 -> 💻 -> 🤖 -> 🚀 gibi tek bir merge zincirini tanımlar.
// Birden fazla hat (örn. "Elektronik" ve "Yazılım") için bu SO'dan birden fazla asset oluşturulabilir.
[CreateAssetMenu(fileName = "NewItemChain", menuName = "CodecoSoft/Item Chain")]
public class ItemChainSO : ScriptableObject
{
    [System.Serializable]
    public class Tier
    {
        public string displayName;
        public Sprite icon;
        public int sellValue;
    }

    public string chainId;
    public Tier[] tiers;

    public int MaxTierIndex => tiers.Length - 1;

    public Tier GetTier(int index) => tiers[index];

    public bool HasNextTier(int index) => index < MaxTierIndex;
}
