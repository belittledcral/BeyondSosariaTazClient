using System.Collections.Generic;

namespace ClassicUO.Game.Map
{
    public sealed class WorldDataStore
    {
        private readonly Dictionary<int, WorldChunkData> _chunks = new Dictionary<int, WorldChunkData>();
        private readonly int _mapHeightInBlocks;

        public WorldDataStore(int mapHeightInBlocks)
        {
            _mapHeightInBlocks = mapHeightInBlocks;
        }

        private int Key(int blockX, int blockY) => blockX * _mapHeightInBlocks + blockY;

        public bool HasData(int blockX, int blockY) => _chunks.ContainsKey(Key(blockX, blockY));

        public WorldChunkData GetOrCreate(int blockX, int blockY)
        {
            int key = Key(blockX, blockY);
            if (!_chunks.TryGetValue(key, out WorldChunkData data))
            {
                data = new WorldChunkData();
                _chunks[key] = data;
            }
            return data;
        }

        public bool TryGet(int blockX, int blockY, out WorldChunkData data)
            => _chunks.TryGetValue(Key(blockX, blockY), out data);

        public void Clear(int blockX, int blockY) => _chunks.Remove(Key(blockX, blockY));

        public void ClearAll() => _chunks.Clear();
    }
}
