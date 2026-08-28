using System.Drawing;

namespace LogicArrowsLauncher;

public sealed record OptimizationStats(
    int OriginalCells,
    int OptimizedCells,
    int OriginalChunks,
    int OptimizedChunks,
    int OriginalWidth,
    int OriginalHeight,
    int OptimizedWidth,
    int OptimizedHeight,
    double AreaReductionPercent,
    int RemovedDead,
    int DuplicateCells,
    int DeletedCols,
    int DeletedRows,
    int LongLinks,
    bool PruneCancelled,
    IReadOnlyList<string> Warnings
);

public sealed record OptimizationResult(
    MapBlueprint OptimizedBlueprint,
    string OptimizedBase64,
    OptimizationStats Stats
);

/// <summary>
/// Умный оптимизатор схем v2 — порт JS-версии (MapBridgeScript).
/// Основан на точных правилах движка игры (bundle.js v1.4, module ChunkUpdates):
/// удаляет только механизмы, которые никогда не сработают, и сжимает пустоты
/// с точным сохранением всех сигнальных связей (включая прыжки на 2 клетки).
/// </summary>
public static class MapOptimizer
{
    // Офсеты передачи сигнала (h-система бандла).
    private static readonly IReadOnlyDictionary<int, int[][]> Offsets = new Dictionary<int, int[][]>
    {
        [1] = new[] { new[] { -1, 0 } },
        [2] = new[] { new[] { -1, 0 }, new[] { 0, 1 }, new[] { 1, 0 }, new[] { 0, -1 } },
        [4] = new[] { new[] { -1, 0 } },
        [5] = new[] { new[] { -1, 0 } },
        [6] = new[] { new[] { -1, 0 }, new[] { 1, 0 } },
        [7] = new[] { new[] { -1, 0 }, new[] { 0, 1 } },
        [8] = new[] { new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } },
        [9] = new[] { new[] { -1, 0 }, new[] { 0, 1 }, new[] { 1, 0 }, new[] { 0, -1 } },
        [10] = new[] { new[] { -2, 0 } },
        [11] = new[] { new[] { -1, 1 } },
        [12] = new[] { new[] { -1, 0 }, new[] { -2, 0 } },
        [13] = new[] { new[] { -2, 0 }, new[] { 0, 1 } },
        [14] = new[] { new[] { -1, 0 }, new[] { -1, 1 } },
        [15] = new[] { new[] { -1, 0 } },
        [16] = new[] { new[] { -1, 0 } },
        [17] = new[] { new[] { -1, 0 } },
        [18] = new[] { new[] { -1, 0 } },
        [19] = new[] { new[] { -1, 0 } },
        [20] = new[] { new[] { -1, 0 } },
        [21] = new[] { new[] { -1, 0 }, new[] { 0, 1 }, new[] { 1, 0 }, new[] { 0, -1 } },
        [22] = new[] { new[] { -1, 0 } },
        [24] = new[] { new[] { -1, 0 } },
    };

    // Источники: сигнал есть без внешнего входа (источник, генератор, кнопки).
    private static readonly HashSet<int> Sources = new() { 2, 9, 21, 24 };

    // Защищённые типы: 23 — цель уровня, 25 — декоративная стрелка («Does
    // nothing» в бандле, голубая — из неё рисуют пиксель-арт). Любой
    // НЕИЗВЕСТНЫЙ тип тоже декор: лучше сохранить лишнее, чем удалить схему.
    private static readonly HashSet<int> KnownTypes = new(Offsets.Keys.Concat(new[] { 3, 25 }));

    private static bool IsDecor(int type) => type == 25 || type == 23 || !KnownTypes.Contains(type);

    private static int MinInputs(int type) => type is 16 or 18 ? 2 : 1;

    private static (int X, int Y) RelTarget(int x, int y, int rotation, bool flipped, int dx, int dy)
    {
        int c = flipped ? -dy : dy;
        return (rotation & 3) switch
        {
            0 => (x + c, y + dx),
            1 => (x - dx, y + c),
            2 => (x - c, y - dx),
            _ => (x + dx, y - c),
        };
    }

    private static IEnumerable<(int Dx, int Dy)> OutOffsets(int type)
    {
        if (Offsets.TryGetValue(type, out var list))
            foreach (var o in list) yield return (o[0], o[1]);
        if (type == 3) yield return (-1, 0); // блокер гасит стрелку перед собой
    }

    private static IEnumerable<(int Dx, int Dy)> MechOffsets(int type)
    {
        foreach (var o in OutOffsets(type)) yield return o;
        if (type == 5) yield return (1, 0); // детектор читает клетку сзади
    }

    private readonly record struct CellRef(int X, int Y, int Type, int Rotation, bool Flipped);

    private static Dictionary<(int X, int Y), CellRef> BuildIndex(IEnumerable<CellRef> cells) =>
        cells.ToDictionary(c => (c.X, c.Y));

    public static OptimizationResult Optimize(MapBlueprint original)
    {
        var warnings = new List<string>();

        if (original.Cells.Count == 0)
        {
            return BuildResult(original.Clone(), original, new OptimizationStats(
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, warnings));
        }

        // 0. Дедупликация координат (последняя побеждает).
        var byCoord = new Dictionary<(int X, int Y), CellRef>();
        foreach (var c in original.Cells)
            byCoord[(c.X, c.Y)] = new CellRef(c.X, c.Y, c.Type, c.Rotation, c.Flipped);
        var work = byCoord.Values.ToList();
        int duplicates = original.Cells.Count - work.Count;

        int origMinX = work.Min(c => c.X), origMaxX = work.Max(c => c.X);
        int origMinY = work.Min(c => c.Y), origMaxY = work.Max(c => c.Y);
        int origW = origMaxX - origMinX + 1, origH = origMaxY - origMinY + 1;
        int origArea = Math.Max(1, origW * origH);

        // 1. Безопасная чистка: клетки, которые никогда не сработают.
        var (kept, removedDead, pruneCancelled) = SafePrune(work);
        if (pruneCancelled)
        {
            warnings.Add("В схеме не найдено ни одного работающего источника сигнала. Чистка отменена — удалён 0 блоков.");
        }

        // 2. Сжатие пустот с сохранением всех связей.
        var (finalCells, deletedCols, deletedRows) = Compact(kept, warnings);

        // 3. Нормализация: сдвиг min(X,Y) в (1,1).
        if (finalCells.Count > 0)
        {
            int minX = finalCells.Min(c => c.X), minY = finalCells.Min(c => c.Y);
            finalCells = finalCells
                .Select(c => new CellRef(c.X - minX + 1, c.Y - minY + 1, c.Type, c.Rotation, c.Flipped))
                .ToList();
        }

        var optBlueprint = new MapBlueprint();
        foreach (var c in finalCells)
            optBlueprint.Cells.Add(new MapCell(c.X, c.Y, c.Type, c.Rotation, c.Flipped));

        int optW = 0, optH = 0;
        if (finalCells.Count > 0)
        {
            int minX = finalCells.Min(c => c.X), maxX = finalCells.Max(c => c.X);
            int minY = finalCells.Min(c => c.Y), maxY = finalCells.Max(c => c.Y);
            optW = maxX - minX + 1;
            optH = maxY - minY + 1;
        }
        int optArea = Math.Max(1, Math.Max(1, optW) * Math.Max(1, optH));
        double reduction = Math.Max(0, Math.Round((1.0 - optArea / (double)origArea) * 1000.0) / 10.0);
        int longLinks = CountLongLinks(finalCells);

        var stats = new OptimizationStats(
            OriginalCells: original.Cells.Count,
            OptimizedCells: optBlueprint.CellCount,
            OriginalChunks: original.ChunkCount,
            OptimizedChunks: optBlueprint.ChunkCount,
            OriginalWidth: origW,
            OriginalHeight: origH,
            OptimizedWidth: optW,
            OptimizedHeight: optH,
            AreaReductionPercent: reduction,
            RemovedDead: removedDead,
            DuplicateCells: duplicates,
            DeletedCols: deletedCols,
            DeletedRows: deletedRows,
            LongLinks: longLinks,
            PruneCancelled: pruneCancelled,
            Warnings: warnings
        );

        return BuildResult(optBlueprint, original, stats);
    }

    private static OptimizationResult BuildResult(MapBlueprint optimized, MapBlueprint original, OptimizationStats stats)
    {
        string base64 = optimized.CellCount > 0 ? MapCodec.Encode(optimized) : string.Empty;
        if (optimized.CellCount == 0 && original.CellCount > 0)
        {
            // Не отдаём пустую схему: чистка отключается на уровне SafePrune,
            // эта ветка — страховка.
            base64 = MapCodec.Encode(original.Clone());
        }
        return new OptimizationResult(optimized, base64, stats);
    }

    private static (List<CellRef> Kept, int RemovedDead, bool Cancelled) SafePrune(List<CellRef> cells)
    {
        var alive = new List<CellRef>(cells);
        int removed = 0;
        while (true)
        {
            var live = ComputeFiring(alive);
            // Декор (тип 25, цель уровня, неизвестные типы) никогда не удаляем.
            var next = alive.Where(c => live.Contains(c) || IsDecor(c.Type)).ToList();
            if (next.Count == alive.Count) return (alive, removed, false);
            if (next.Count == 0)
            {
                // Нет ни одного источника — отменяем чистку целиком.
                return (new List<CellRef>(cells), 0, true);
            }
            removed += alive.Count - next.Count;
            alive = next;
        }
    }

    private static HashSet<CellRef> ComputeFiring(List<CellRef> cells)
    {
        var byKey = BuildIndex(cells);
        var inEdges = cells.ToDictionary(c => c, _ => new List<CellRef>());
        foreach (var c in cells)
        {
            foreach (var t in OutTargets(c, byKey)) inEdges[t].Add(c);
        }

        var live = new HashSet<CellRef>();
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var c in cells)
            {
                if (live.Contains(c)) continue;
                bool ok;
                if (Sources.Contains(c.Type) || c.Type is 15 or 23) ok = true;
                else if (c.Type == 5)
                {
                    var (bx, by) = RelTarget(c.X, c.Y, c.Rotation, c.Flipped, 1, 0);
                    ok = byKey.TryGetValue((bx, by), out var behind) && live.Contains(behind);
                }
                else
                {
                    int active = inEdges[c].Count(live.Contains);
                    ok = active >= MinInputs(c.Type);
                }
                if (ok) { live.Add(c); changed = true; }
            }
        }
        return live;
    }

    private static List<CellRef> OutTargets(CellRef c, Dictionary<(int X, int Y), CellRef> byKey)
    {
        var result = new List<CellRef>();
        foreach (var (dx, dy) in OutOffsets(c.Type))
        {
            var (tx, ty) = RelTarget(c.X, c.Y, c.Rotation, c.Flipped, dx, dy);
            if (byKey.TryGetValue((tx, ty), out var t)) result.Add(t);
        }
        return result;
    }

    private static int CountLongLinks(List<CellRef> cells)
    {
        var byKey = BuildIndex(cells);
        int n = 0;
        foreach (var c in cells)
        {
            foreach (var (dx, dy) in MechOffsets(c.Type))
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) < 2) continue;
                var (tx, ty) = RelTarget(c.X, c.Y, c.Rotation, c.Flipped, dx, dy);
                if (byKey.ContainsKey((tx, ty))) n++;
            }
        }
        return n;
    }

    /// <summary>
    /// Столбцы и ряды, занимаемые 8-связными компонентами декоративных стрелок:
    /// сжатие внутри них исказило бы пиксель-арт (цифры индикатора и т.п.).
    /// </summary>
    private static (HashSet<int> Cols, HashSet<int> Rows) DecorProtection(List<CellRef> cells)
    {
        var decor = cells.Where(c => IsDecor(c.Type) && c.Type != 23).ToList();
        var pos = decor.Select(c => (c.X, c.Y)).ToHashSet();
        var seen = new HashSet<(int X, int Y)>();
        var cols = new HashSet<int>();
        var rows = new HashSet<int>();
        foreach (var d in decor)
        {
            if (!seen.Add((d.X, d.Y))) continue;
            var stack = new Stack<(int X, int Y)>();
            stack.Push((d.X, d.Y));
            int minX = d.X, maxX = d.X, minY = d.Y, maxY = d.Y;
            while (stack.Count > 0)
            {
                var (cx, cy) = stack.Pop();
                if (cx < minX) minX = cx;
                if (cx > maxX) maxX = cx;
                if (cy < minY) minY = cy;
                if (cy > maxY) maxY = cy;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var key = (cx + dx, cy + dy);
                        if (pos.Contains(key) && seen.Add(key)) stack.Push(key);
                    }
                }
            }
            for (int x = minX; x <= maxX; x++) cols.Add(x);
            for (int y = minY; y <= maxY; y++) rows.Add(y);
        }
        return (cols, rows);
    }

    /// <summary>Сжатие пустых столбцов/рядов с гарантией сохранения всех связей.</summary>
    private static (List<CellRef> Cells, int DeletedCols, int DeletedRows) Compact(
        List<CellRef> cells, List<string> warnings)
    {
        if (cells.Count == 0) return (cells, 0, 0);

        var xs = cells.Select(c => c.X).ToHashSet();
        var ys = cells.Select(c => c.Y).ToHashSet();
        int minX = xs.Min(), maxX = xs.Max();
        int minY = ys.Min(), maxY = ys.Max();

        // Защита пиксель-арта: bbox каждой 8-связной компоненты декора.
        var (pCols, pRows) = DecorProtection(cells);
        var sx = new HashSet<int>();
        var sy = new HashSet<int>();
        for (int x = minX; x <= maxX; x++) if (!xs.Contains(x) && !pCols.Contains(x)) sx.Add(x);
        for (int y = minY; y <= maxY; y++) if (!ys.Contains(y) && !pRows.Contains(y)) sy.Add(y);

        var byKey = BuildIndex(cells);
        var consOcc = new List<(CellRef A, CellRef B, int Ox, int Oy)>();
        var consVoid = new List<(CellRef A, int Ox, int Oy)>();
        foreach (var c in cells)
        {
            foreach (var (dx, dy) in MechOffsets(c.Type))
            {
                var (px, py) = RelTarget(c.X, c.Y, c.Rotation, c.Flipped, dx, dy);
                if (byKey.TryGetValue((px, py), out var k))
                    consOcc.Add((c, k, px - c.X, py - c.Y));
                else
                    consVoid.Add((c, px - c.X, py - c.Y));
            }
        }

        var arrX = new List<int>(sx);
        var arrY = new List<int>(sy);
        int Less(List<int> arr, int v) { int n = 0; foreach (var s in arr) if (s < v) n++; return n; }
        int Mx(int v) => v - Less(arrX, v);
        int My(int v) => v - Less(arrY, v);

        bool RepairBetween(CellRef a, CellRef b)
        {
            int loX = Math.Min(a.X, b.X), hiX = Math.Max(a.X, b.X);
            for (int g = loX; g < hiX; g++)
                if (sx.Remove(g)) { arrX.Remove(g); return true; }
            int loY = Math.Min(a.Y, b.Y), hiY = Math.Max(a.Y, b.Y);
            for (int g = loY; g < hiY; g++)
                if (sy.Remove(g)) { arrY.Remove(g); return true; }
            return false;
        }

        for (long iter = 0; iter < 200000; iter++)
        {
            (CellRef, CellRef)? fail = null;
            foreach (var (a, b, ox, oy) in consOcc)
            {
                if (Mx(b.X) - Mx(a.X) != ox || My(b.Y) - My(a.Y) != oy) { fail = (a, b); break; }
            }
            if (fail == null)
            {
                var img = cells.ToDictionary(k => (Mx(k.X), My(k.Y)), k => k);
                foreach (var (a, ox, oy) in consVoid)
                {
                    if (img.TryGetValue((Mx(a.X) + ox, My(a.Y) + oy), out var hit)) { fail = (a, hit); break; }
                }
            }
            if (fail == null) break;
            if (!RepairBetween(fail.Value.Item1, fail.Value.Item2))
            {
                warnings.Add("Сжатие пропущено: не удалось гарантировать сохранность всех связей.");
                return (cells, 0, 0);
            }
        }

        var result = cells
            .Select(c => new CellRef(Mx(c.X), My(c.Y), c.Type, c.Rotation, c.Flipped))
            .ToList();
        return (result, arrX.Count, arrY.Count);
    }
}
