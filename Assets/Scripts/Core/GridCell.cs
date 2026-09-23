public enum CellLockState
{
    Locked,
    Unlocked
}

public class GridCell
{
    public int x;
    public int y;
    public CellLockState lockState;
    public ItemInstance item;

    // Yeni açılan bir hücrede bazen normal boş yerine kilitli bir kasa çıkar;
    // oyuncu üzerine bir parça sürükleyip tüketerek kasayı açar.
    public bool hasCrate;

    public bool IsEmpty => lockState == CellLockState.Unlocked && item == null && !hasCrate;
}
