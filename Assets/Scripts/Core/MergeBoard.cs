using System.Collections.Generic;
using UnityEngine;

// Tahtanın tüm durumunu ve merge/unlock kurallarını yöneten, sahneden bağımsız çekirdek sınıf.
// Görsel/drag&drop katmanı bu sınıfı sarmalayacak; burası tamamen test edilebilir kalır.
public class MergeBoard
{
    public enum MoveResult
    {
        Invalid,
        Moved,
        Merged
    }

    private readonly GridCell[,] cells;

    public int Width { get; }
    public int Height { get; }

    public MergeBoard(int width, int height, IEnumerable<Vector2Int> initiallyUnlocked)
    {
        Width = width;
        Height = height;
        cells = new GridCell[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cells[x, y] = new GridCell { x = x, y = y, lockState = CellLockState.Locked };
            }
        }

        if (initiallyUnlocked != null)
        {
            foreach (var pos in initiallyUnlocked)
            {
                if (InBounds(pos.x, pos.y))
                {
                    cells[pos.x, pos.y].lockState = CellLockState.Unlocked;
                }
            }
        }
    }

    public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    public GridCell GetCell(int x, int y) => InBounds(x, y) ? cells[x, y] : null;

    public bool TryPlaceItem(int x, int y, ItemInstance item)
    {
        var cell = GetCell(x, y);
        if (cell == null || !cell.IsEmpty) return false;

        cell.item = item;
        return true;
    }

    // fromX/fromY'deki parçayı toX/toY'ye taşımayı dener.
    // Hedef boşsa taşır, aynı tier'daysa merge eder, aksi halde reddeder.
    public MoveResult TryMove(int fromX, int fromY, int toX, int toY)
    {
        var from = GetCell(fromX, fromY);
        var to = GetCell(toX, toY);

        if (from == null || to == null || from.item == null) return MoveResult.Invalid;
        if (to.lockState == CellLockState.Locked) return MoveResult.Invalid;
        if (to.hasCrate) return MoveResult.Invalid;
        if (from == to) return MoveResult.Invalid;

        if (to.item == null)
        {
            to.item = from.item;
            from.item = null;
            return MoveResult.Moved;
        }

        if (from.item.CanMergeWith(to.item))
        {
            int nextTier = to.item.tierIndex + 1;
            to.item = new ItemInstance(to.item.chain, nextTier);
            from.item = null;
            return MoveResult.Merged;
        }

        return MoveResult.Invalid;
    }

    // Tahtanın kendi hücrelerinden değil, dışarıdan (üretici/spawner gibi) gelen bir parçayı
    // hedef hücreye yerleştirmeyi ya da hedefteki parçayla merge etmeyi dener.
    public MoveResult TryPlaceOrMergeExternal(int x, int y, ItemInstance incoming)
    {
        var cell = GetCell(x, y);
        if (cell == null || cell.lockState == CellLockState.Locked) return MoveResult.Invalid;
        if (cell.hasCrate) return MoveResult.Invalid;

        if (cell.item == null)
        {
            cell.item = incoming;
            return MoveResult.Moved;
        }

        if (incoming.CanMergeWith(cell.item))
        {
            int nextTier = cell.item.tierIndex + 1;
            cell.item = new ItemInstance(cell.item.chain, nextTier);
            return MoveResult.Merged;
        }

        return MoveResult.Invalid;
    }

    public bool TryRemoveItem(int x, int y, out ItemInstance removed)
    {
        removed = null;
        var cell = GetCell(x, y);
        if (cell == null || cell.item == null) return false;

        removed = cell.item;
        cell.item = null;
        return true;
    }

    // Yeni açılan bir hücreye kilitli kasa koyar (hücre boş ve açık olmalı).
    public bool TryMarkCrate(int x, int y)
    {
        var cell = GetCell(x, y);
        if (cell == null || cell.lockState == CellLockState.Locked || cell.item != null || cell.hasCrate) return false;

        cell.hasCrate = true;
        return true;
    }

    public bool TryOpenCrate(int x, int y)
    {
        var cell = GetCell(x, y);
        if (cell == null || !cell.hasCrate) return false;

        cell.hasCrate = false;
        return true;
    }

    public bool TryUnlockCell(int x, int y)
    {
        var cell = GetCell(x, y);
        if (cell == null || cell.lockState == CellLockState.Unlocked) return false;

        cell.lockState = CellLockState.Unlocked;
        return true;
    }

    public int CountUnlockedCells()
    {
        int count = 0;
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (cells[x, y].lockState == CellLockState.Unlocked) count++;
            }
        }
        return count;
    }

    public bool HasAnyEmptyUnlockedCell()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (cells[x, y].IsEmpty) return true;
            }
        }
        return false;
    }
}
