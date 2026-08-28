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
    double AreaReductionPercent
);

public sealed record OptimizationResult(
    MapBlueprint OptimizedBlueprint,
    string OptimizedBase64,
    OptimizationStats Stats
);

public static class MapOptimizer
{
    public static OptimizationResult Optimize(MapBlueprint original)
    {
        if (original.Cells.Count == 0)
        {
            return new OptimizationResult(
                original.Clone(),
                MapCodec.Encode(original),
                new OptimizationStats(0, 0, 0, 0, 0, 0, 0, 0, 0));
        }

        var cells = new List<MapCell>();
        // 1. Remove duplicate coordinates (keep last)
        var mapByCoord = new Dictionary<(int x, int y), MapCell>();
        foreach (var c in original.Cells)
        {
            mapByCoord[(c.X, c.Y)] = new MapCell(c.X, c.Y, c.Type, c.Rotation, c.Flipped);
        }
        cells.AddRange(mapByCoord.Values);

        var bboxOrig = original.BoundingBox;
        int origArea = Math.Max(1, bboxOrig.Width * bboxOrig.Height);

        // 2. Normalization: shift min (X, Y) to (1, 1) to fit in Chunk 0
        int minX = cells.Min(c => c.X);
        int minY = cells.Min(c => c.Y);
        foreach (var c in cells)
        {
            c.X -= minX;
            c.Y -= minY;
        }

        // 3. Row & Column Squeeze:
        // Find which X and Y coordinates actually have cells
        var usedX = new HashSet<int>(cells.Select(c => c.X));
        var usedY = new HashSet<int>(cells.Select(c => c.Y));

        // Remap X
        var sortedX = usedX.OrderBy(x => x).ToList();
        var mapX = new Dictionary<int, int>();
        int newX = 1;
        for (int i = 0; i < sortedX.Count; i++)
        {
            if (i > 0)
            {
                int diff = sortedX[i] - sortedX[i - 1];
                // If there's a gap greater than 1, compress it to min distance required
                newX += Math.Min(diff, 1);
            }
            mapX[sortedX[i]] = newX;
        }

        // Remap Y
        var sortedY = usedY.OrderBy(y => y).ToList();
        var mapY = new Dictionary<int, int>();
        int newY = 1;
        for (int i = 0; i < sortedY.Count; i++)
        {
            if (i > 0)
            {
                int diff = sortedY[i] - sortedY[i - 1];
                newY += Math.Min(diff, 1);
            }
            mapY[sortedY[i]] = newY;
        }

        // Apply remap
        foreach (var c in cells)
        {
            if (mapX.TryGetValue(c.X, out var nx)) c.X = nx;
            if (mapY.TryGetValue(c.Y, out var ny)) c.Y = ny;
        }

        var optBlueprint = new MapBlueprint();
        optBlueprint.Cells.AddRange(cells);

        var bboxOpt = optBlueprint.BoundingBox;
        int optArea = Math.Max(1, bboxOpt.Width * bboxOpt.Height);
        double reduction = Math.Max(0, Math.Round((1.0 - (double)optArea / origArea) * 100.0, 1));

        var stats = new OptimizationStats(
            OriginalCells: original.CellCount,
            OptimizedCells: optBlueprint.CellCount,
            OriginalChunks: original.ChunkCount,
            OptimizedChunks: optBlueprint.ChunkCount,
            OriginalWidth: bboxOrig.Width,
            OriginalHeight: bboxOrig.Height,
            OptimizedWidth: bboxOpt.Width,
            OptimizedHeight: bboxOpt.Height,
            AreaReductionPercent: reduction
        );

        string optBase64 = MapCodec.Encode(optBlueprint);

        return new OptimizationResult(optBlueprint, optBase64, stats);
    }
}
