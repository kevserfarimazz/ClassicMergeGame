using UnityEngine;

// Tahta boyutu ve kilit-açma ekonomisi burada tanımlanır.
[CreateAssetMenu(fileName = "NewBoardConfig", menuName = "CodecoSoft/Board Config")]
public class BoardConfigSO : ScriptableObject
{
    public int width = 6;
    public int height = 8;

    // Oyun başında açık olan hücreler (genelde ortada küçük bir alan).
    public Vector2Int[] initiallyUnlockedCells;

    public int baseUnlockCost = 50;
    public float unlockCostGrowth = 1.6f;

    // Şimdiye kadar açılan hücre sayısına göre bir sonraki hücrenin maliyeti.
    public int GetUnlockCost(int unlockedCellCount)
    {
        return Mathf.RoundToInt(baseUnlockCost * Mathf.Pow(unlockCostGrowth, unlockedCellCount));
    }
}
