using System.Collections.Generic;

namespace ClassicUO.Game.Map
{
    public struct ServerStaticEntry
    {
        public ushort Graphic;
        public byte LocalX;   // 0–7
        public byte LocalY;   // 0–7
        public sbyte Z;
        public ushort Hue;
    }

    public class WorldChunkData
    {
        public readonly ushort[] LandGraphics = new ushort[64]; // [y*8+x]
        public readonly sbyte[]  LandZ        = new sbyte[64];
        public readonly List<ServerStaticEntry> Statics = new List<ServerStaticEntry>();
    }
}
