using System.Drawing;

namespace LogicArrowsLauncher;

public sealed record MapCell(int X, int Y, int Type, int Rotation, bool Flipped)
{
    public int X { get; set; } = X;
    public int Y { get; set; } = Y;
    public int Type { get; set; } = Type;
    public int Rotation { get; set; } = Rotation;
    public bool Flipped { get; set; } = Flipped;
}

public sealed class MapBlueprint
{
    public List<MapCell> Cells { get; } = new();

    public Rectangle BoundingBox
    {
        get
        {
            if (Cells.Count == 0) return Rectangle.Empty;
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            foreach (var cell in Cells)
            {
                if (cell.X < minX) minX = cell.X;
                if (cell.X > maxX) maxX = cell.X;
                if (cell.Y < minY) minY = cell.Y;
                if (cell.Y > maxY) maxY = cell.Y;
            }
            return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }

    public int Width => BoundingBox.Width;
    public int Height => BoundingBox.Height;
    public int CellCount => Cells.Count;

    public int ChunkCount
    {
        get
        {
            var chunks = new HashSet<(int cx, int cy)>();
            foreach (var cell in Cells)
            {
                int cx = cell.X >= 0 ? cell.X / 16 : (cell.X - 15) / 16;
                int cy = cell.Y >= 0 ? cell.Y / 16 : (cell.Y - 15) / 16;
                chunks.Add((cx, cy));
            }
            return chunks.Count;
        }
    }

    public MapBlueprint Clone()
    {
        var copy = new MapBlueprint();
        foreach (var c in Cells)
        {
            copy.Cells.Add(new MapCell(c.X, c.Y, c.Type, c.Rotation, c.Flipped));
        }
        return copy;
    }
}

public static class MapCodec
{
    public static MapBlueprint Decode(string input)
    {
        var trimmed = input.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ArgumentException("Входная строка пустая.");

        string base64Data = trimmed;

        // If JSON envelope, extract .data
        if (trimmed.StartsWith("{"))
        {
            try
            {
                var envelope = MapFileService.ReadText(trimmed);
                base64Data = envelope.Data;
            }
            catch
            {
                // Try reading json directly
                using var doc = System.Text.Json.JsonDocument.Parse(trimmed);
                if (doc.RootElement.TryGetProperty("data", out var d))
                {
                    base64Data = d.GetString() ?? trimmed;
                }
            }
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64Data);
        }
        catch (Exception ex)
        {
            throw new FormatException("Некорректный Base64 код карты.", ex);
        }

        return DecodeBinary(bytes);
    }

    public static MapBlueprint DecodeBinary(byte[] bytes)
    {
        if (bytes.Length < 4)
            throw new InvalidDataException("Данные карты слишком короткие (< 4 байт).");

        int offset = 0;
        int ReadU8()
        {
            if (offset >= bytes.Length) throw new InvalidDataException("Неожиданный конец потока данных.");
            return bytes[offset++];
        }

        int ReadU16()
        {
            int low = ReadU8();
            int high = ReadU8();
            return (high << 8) | low;
        }

        int ReadS16()
        {
            int val = ReadU16();
            return (val & 0x8000) != 0 ? -(val & 0x7FFF) : val;
        }

        int version = ReadU16();
        if (version != 0)
            throw new InvalidDataException($"Неподдерживаемая версия сохранения карты: {version}");

        int chunkCount = ReadU16();
        var blueprint = new MapBlueprint();

        for (int c = 0; c < chunkCount; c++)
        {
            int chunkX = ReadS16();
            int chunkY = ReadS16();

            int typeCount = ReadU8() + 1;
            for (int t = 0; t < typeCount; t++)
            {
                int type = ReadU8();
                int arrowCount = ReadU8() + 1;
                for (int a = 0; a < arrowCount; a++)
                {
                    int pos = ReadU8();
                    int rot = ReadU8();
                    int lx = pos & 0xF;
                    int ly = pos >> 4;
                    int rotation = rot & 0x3;
                    bool flipped = (rot & 0x4) != 0 || (rot & 0x8) != 0;

                    int gx = chunkX * 16 + lx;
                    int gy = chunkY * 16 + ly;
                    blueprint.Cells.Add(new MapCell(gx, gy, type, rotation, flipped));
                }
            }
        }

        return blueprint;
    }

    public static string Encode(MapBlueprint blueprint)
    {
        var bytes = EncodeBinary(blueprint);
        return Convert.ToBase64String(bytes);
    }

    public static byte[] EncodeBinary(MapBlueprint blueprint)
    {
        var stream = new MemoryStream();
        void WriteU8(int val) => stream.WriteByte((byte)(val & 0xFF));
        void WriteU16(int val)
        {
            WriteU8(val & 0xFF);
            WriteU8((val >> 8) & 0xFF);
        }
        void WriteS16(int val)
        {
            int encoded = val < 0 ? (-val | 0x8000) : val;
            WriteU16(encoded);
        }

        // Header: Version 0
        WriteU16(0);

        // Group cells by chunk (cx, cy)
        var chunkMap = new Dictionary<(int cx, int cy), List<MapCell>>();
        foreach (var cell in blueprint.Cells)
        {
            int cx = cell.X >= 0 ? cell.X / 16 : (cell.X - 15) / 16;
            int cy = cell.Y >= 0 ? cell.Y / 16 : (cell.Y - 15) / 16;
            var key = (cx, cy);
            if (!chunkMap.TryGetValue(key, out var list))
            {
                list = new List<MapCell>();
                chunkMap[key] = list;
            }
            list.Add(cell);
        }

        // Chunk count
        WriteU16(chunkMap.Count);

        foreach (var (coords, chunkCells) in chunkMap)
        {
            WriteS16(coords.cx);
            WriteS16(coords.cy);

            // Group by arrow type
            var typeMap = new Dictionary<int, List<MapCell>>();
            foreach (var cell in chunkCells)
            {
                if (!typeMap.TryGetValue(cell.Type, out var list))
                {
                    list = new List<MapCell>();
                    typeMap[cell.Type] = list;
                }
                list.Add(cell);
            }

            WriteU8(typeMap.Count - 1);

            foreach (var (type, arrows) in typeMap)
            {
                WriteU8(type);
                WriteU8(arrows.Count - 1);

                foreach (var arrow in arrows)
                {
                    int lx = arrow.X - (coords.cx * 16);
                    int ly = arrow.Y - (coords.cy * 16);
                    int pos = (ly << 4) | (lx & 0xF);
                    int rot = (arrow.Flipped ? 0x4 : 0x0) | (arrow.Rotation & 0x3);

                    WriteU8(pos);
                    WriteU8(rot);
                }
            }
        }

        return stream.ToArray();
    }
}
