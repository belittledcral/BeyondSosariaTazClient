using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ClassicUO.Game.Map
{
    public enum WorldPersistenceMode { Buffer, FileBacked }

    // MapBlock binary layout: 4-byte header + 64 cells of 3 bytes each = 196 bytes per block
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct PersistMapCell
    {
        public ushort TileID;
        public sbyte Z;
    }

    // StaticsBlock binary layout: 7 bytes per entry (same as staidx0.mul entry format)
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct PersistStaticEntry
    {
        public ushort Graphic;
        public byte LocalX;
        public byte LocalY;
        public sbyte Z;
        public ushort Hue;
    }

    // staidx entry: offset (4 bytes) + size (4 bytes) = 8 bytes per block
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct PersistStaIdx
    {
        public int Offset;  // byte offset into statics.bin, -1 = no statics
        public int Count;   // number of PersistStaticEntry records
    }

    public sealed class WorldDataPersistence : IDisposable
    {
        private const int MAP_HEADER_SIZE = 4;
        private const int MAP_CELL_SIZE = 3;  // sizeof(PersistMapCell)
        private const int MAP_BLOCK_SIZE = MAP_HEADER_SIZE + 64 * MAP_CELL_SIZE; // 196
        private const int STATIC_ENTRY_SIZE = 7; // sizeof(PersistStaticEntry)
        private const int STAIDX_ENTRY_SIZE = 8; // sizeof(PersistStaIdx)

        private readonly int _mapHeightInBlocks;
        private readonly string _mapBinPath;
        private readonly string _staticsBinPath;
        private readonly string _staidxBinPath;

        public WorldPersistenceMode Mode { get; }
        public string SaveDirectory { get; }

        public WorldDataPersistence(WorldPersistenceMode mode, string saveDirectory, int mapHeightInBlocks)
        {
            Mode = mode;
            _mapHeightInBlocks = mapHeightInBlocks;

            if (mode == WorldPersistenceMode.FileBacked && saveDirectory != null)
            {
                SaveDirectory = saveDirectory;
                Directory.CreateDirectory(saveDirectory);
                _mapBinPath     = Path.Combine(saveDirectory, "map.bin");
                _staticsBinPath = Path.Combine(saveDirectory, "statics.bin");
                _staidxBinPath  = Path.Combine(saveDirectory, "staidx.bin");
            }
        }

        private int BlockIndex(int blockX, int blockY) => blockX * _mapHeightInBlocks + blockY;

        /// <summary>Flush a chunk to local binary files. No-op in Buffer mode.</summary>
        public void FlushChunk(int blockX, int blockY, WorldChunkData data)
        {
            if (Mode != WorldPersistenceMode.FileBacked || SaveDirectory == null) return;

            int idx = BlockIndex(blockX, blockY);

            // --- map.bin ---
            long mapOffset = (long)idx * MAP_BLOCK_SIZE;
            using (var fs = new FileStream(_mapBinPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
            {
                fs.Seek(mapOffset, SeekOrigin.Begin);
                // 4-byte header (unused, write zeroes for .mul compat)
                fs.Write(new byte[MAP_HEADER_SIZE], 0, MAP_HEADER_SIZE);
                Span<byte> cellBuf = stackalloc byte[MAP_CELL_SIZE];
                for (int i = 0; i < 64; i++)
                {
                    ushort tileID = data.LandGraphics[i];
                    sbyte z = data.LandZ[i];
                    cellBuf[0] = (byte)(tileID & 0xFF);
                    cellBuf[1] = (byte)(tileID >> 8);
                    cellBuf[2] = (byte)z;
                    fs.Write(cellBuf);
                }
            }

            // --- statics.bin + staidx.bin ---
            int staticCount = data.Statics.Count;
            int staticsByteOffset;

            using (var fs = new FileStream(_staticsBinPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
            {
                staticsByteOffset = (int)fs.Seek(0, SeekOrigin.End);
                Span<byte> sBuf = stackalloc byte[STATIC_ENTRY_SIZE];
                foreach (var s in data.Statics)
                {
                    sBuf[0] = (byte)(s.Graphic & 0xFF);
                    sBuf[1] = (byte)(s.Graphic >> 8);
                    sBuf[2] = s.LocalX;
                    sBuf[3] = s.LocalY;
                    sBuf[4] = (byte)s.Z;
                    sBuf[5] = (byte)(s.Hue & 0xFF);
                    sBuf[6] = (byte)(s.Hue >> 8);
                    fs.Write(sBuf);
                }
            }

            // write staidx entry
            long idxOffset = (long)idx * STAIDX_ENTRY_SIZE;
            using (var fs = new FileStream(_staidxBinPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
            {
                fs.Seek(idxOffset, SeekOrigin.Begin);
                Span<byte> idxBuf = stackalloc byte[STAIDX_ENTRY_SIZE];
                int offset = staticCount > 0 ? staticsByteOffset : -1;
                idxBuf[0] = (byte)(offset & 0xFF);
                idxBuf[1] = (byte)((offset >> 8) & 0xFF);
                idxBuf[2] = (byte)((offset >> 16) & 0xFF);
                idxBuf[3] = (byte)((offset >> 24) & 0xFF);
                idxBuf[4] = (byte)(staticCount & 0xFF);
                idxBuf[5] = (byte)((staticCount >> 8) & 0xFF);
                idxBuf[6] = (byte)((staticCount >> 16) & 0xFF);
                idxBuf[7] = (byte)((staticCount >> 24) & 0xFF);
                fs.Write(idxBuf);
            }
        }

        /// <summary>
        /// Try to fill <paramref name="target"/> from local .bin files.
        /// Returns true if data was found on disk.
        /// </summary>
        public bool TryLoadChunk(int blockX, int blockY, WorldChunkData target)
        {
            if (Mode != WorldPersistenceMode.FileBacked || !HasSavedChunk(blockX, blockY)) return false;

            int idx = BlockIndex(blockX, blockY);

            // --- map.bin ---
            long mapOffset = (long)idx * MAP_BLOCK_SIZE + MAP_HEADER_SIZE;
            using (var fs = new FileStream(_mapBinPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Length < mapOffset + 64 * MAP_CELL_SIZE) return false;
                fs.Seek(mapOffset, SeekOrigin.Begin);
                Span<byte> cellBuf = stackalloc byte[MAP_CELL_SIZE];
                for (int i = 0; i < 64; i++)
                {
                    fs.ReadExactly(cellBuf);
                    target.LandGraphics[i] = (ushort)(cellBuf[0] | (cellBuf[1] << 8));
                    target.LandZ[i] = (sbyte)cellBuf[2];
                }
            }

            // --- staidx.bin + statics.bin ---
            long idxOffset = (long)idx * STAIDX_ENTRY_SIZE;
            using (var fs = new FileStream(_staidxBinPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Length < idxOffset + STAIDX_ENTRY_SIZE) return true; // land only, ok

                fs.Seek(idxOffset, SeekOrigin.Begin);
                Span<byte> idxBuf = stackalloc byte[STAIDX_ENTRY_SIZE];
                fs.ReadExactly(idxBuf);

                int offset = idxBuf[0] | (idxBuf[1] << 8) | (idxBuf[2] << 16) | (idxBuf[3] << 24);
                int count  = idxBuf[4] | (idxBuf[5] << 8) | (idxBuf[6] << 16) | (idxBuf[7] << 24);

                if (offset < 0 || count <= 0) return true;

                target.Statics.Clear();
                using var sf = new FileStream(_staticsBinPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                sf.Seek(offset, SeekOrigin.Begin);
                Span<byte> sBuf = stackalloc byte[STATIC_ENTRY_SIZE];
                for (int i = 0; i < count; i++)
                {
                    sf.ReadExactly(sBuf);
                    target.Statics.Add(new ServerStaticEntry
                    {
                        Graphic = (ushort)(sBuf[0] | (sBuf[1] << 8)),
                        LocalX  = sBuf[2],
                        LocalY  = sBuf[3],
                        Z       = (sbyte)sBuf[4],
                        Hue     = (ushort)(sBuf[5] | (sBuf[6] << 8)),
                    });
                }
            }

            return true;
        }

        public bool HasSavedChunk(int blockX, int blockY)
        {
            if (Mode != WorldPersistenceMode.FileBacked || !File.Exists(_mapBinPath)) return false;
            long mapOffset = (long)BlockIndex(blockX, blockY) * MAP_BLOCK_SIZE;
            return new FileInfo(_mapBinPath).Length > mapOffset;
        }

        public void DeleteSavedWorld()
        {
            if (SaveDirectory != null && Directory.Exists(SaveDirectory))
                Directory.Delete(SaveDirectory, recursive: true);
        }

        public void Dispose() { /* streams opened/closed per operation */ }
    }
}
