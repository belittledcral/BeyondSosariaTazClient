using System;
using System.Collections.Generic;
using System.IO;
using ClassicUO.Game.Map;

namespace ClassicUO.Game.Managers
{
    public sealed class WorldDataManager
    {
        private readonly World _world;
        private readonly int _mapHeightInBlocks;

        public WorldDataStore Store { get; }
        public WorldDataPersistence Persistence { get; }
        public WorldPersistenceMode Mode => Persistence.Mode;

        public (int minBX, int minBY, int maxBX, int maxBY) CurrentViewInBlocks { get; private set; }

        /// <summary>
        /// Raised when the visible block range changes. Args: minBX, minBY, maxBX, maxBY.
        /// </summary>
        public event Action<int, int, int, int> ViewBoundsChanged;

        public WorldDataManager(
            World world,
            int mapHeightInBlocks,
            WorldPersistenceMode mode,
            string worldId = null)
        {
            _world = world;
            _mapHeightInBlocks = mapHeightInBlocks;
            Store = new WorldDataStore(mapHeightInBlocks);

            string saveDir = null;
            if (mode == WorldPersistenceMode.FileBacked && worldId != null)
            {
                saveDir = Path.Combine(CUOEnviroment.ExecutablePath, "procgen", worldId);
            }

            Persistence = new WorldDataPersistence(mode, saveDir, mapHeightInBlocks);
        }

        // ------------------------------------------------------------------ //
        // Server API

        /// <summary>Receive and store 64 land tiles for one 8×8 block.</summary>
        public void ReceiveTerrainBlock(
            int blockX, int blockY,
            ReadOnlySpan<ushort> graphics,
            ReadOnlySpan<sbyte>  zValues)
        {
            WorldChunkData data = Store.GetOrCreate(blockX, blockY);
            graphics.CopyTo(data.LandGraphics);
            zValues.CopyTo(data.LandZ);

            if (Mode == WorldPersistenceMode.FileBacked)
                Persistence.FlushChunk(blockX, blockY, data);

            InvalidateChunk(blockX, blockY);
        }

        /// <summary>Receive and store statics for one block (replaces existing).</summary>
        public void ReceiveStaticsBlock(
            int blockX, int blockY,
            IEnumerable<ServerStaticEntry> statics)
        {
            WorldChunkData data = Store.GetOrCreate(blockX, blockY);
            data.Statics.Clear();
            data.Statics.AddRange(statics);

            if (Mode == WorldPersistenceMode.FileBacked)
                Persistence.FlushChunk(blockX, blockY, data);

            InvalidateChunk(blockX, blockY);
        }

        /// <summary>Drop server data for a block so file-based loading takes over.</summary>
        public void ReleaseBlock(int blockX, int blockY)
        {
            Store.Clear(blockX, blockY);
            InvalidateChunk(blockX, blockY);
        }

        /// <summary>Drop all server data and clear save files (if FileBacked).</summary>
        public void ReleaseAll()
        {
            Store.ClearAll();
            if (Mode == WorldPersistenceMode.FileBacked)
                Persistence.DeleteSavedWorld();
        }

        // ------------------------------------------------------------------ //
        // View tracking — called by GameScene each frame after GetViewPort()

        internal void UpdateViewBounds(int minTileX, int minTileY, int maxTileX, int maxTileY)
        {
            int minBX = minTileX >> 3;
            int minBY = minTileY >> 3;
            int maxBX = maxTileX >> 3;
            int maxBY = maxTileY >> 3;

            var prev = CurrentViewInBlocks;
            if (prev.minBX == minBX && prev.minBY == minBY &&
                prev.maxBX == maxBX && prev.maxBY == maxBY)
            {
                return;
            }

            CurrentViewInBlocks = (minBX, minBY, maxBX, maxBY);
            ViewBoundsChanged?.Invoke(minBX, minBY, maxBX, maxBY);
        }

        // ------------------------------------------------------------------ //
        // Helpers

        private void InvalidateChunk(int blockX, int blockY)
        {
            Map.Map map = _world?.Map;
            if (map == null) return;

            // Mark the live chunk as destroyed so GetChunk2 reloads it next frame
            int block = blockX * _mapHeightInBlocks + blockY;
            Chunk existing = map.GetChunk(block);
            if (existing != null && !existing.IsDestroyed)
            {
                existing.Destroy();
            }
        }
    }
}
