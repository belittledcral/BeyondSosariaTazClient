using System;
using ClassicUO.Game;
using ClassicUO.Game.GameObjects;
using ClassicUO.IO;
using ClassicUO.Network.PacketHandlers.Helpers;

namespace ClassicUO.Network.PacketHandlers;

internal static class UpdateContainedItems
{
    public static void Receive(World world, ref StackDataReader p)
    {
        if (!world.InGame)
            return;

        ushort count = p.ReadUInt16BE();

        if (count == 0)
            return;

        Span<ContainerItemData> items = count <= 128
            ? stackalloc ContainerItemData[count]
            : new ContainerItemData[count];

        for (int i = 0; i < count; i++)
        {
            items[i].Serial = p.ReadUInt32BE();
            items[i].Graphic = (ushort)(p.ReadUInt16BE() + p.ReadUInt8());
            items[i].Amount = Math.Max(p.ReadUInt16BE(), (ushort)1);
            items[i].X = p.ReadUInt16BE();
            items[i].Y = p.ReadUInt16BE();

            if (Client.Game.UO.Version >= Utility.ClientVersion.CV_6017)
                p.Skip(1);

            items[i].ContainerSerial = p.ReadUInt32BE();
            items[i].Hue = p.ReadUInt16BE();

            if (i == 0)
            {
                Entity container = world.Get(items[i].ContainerSerial);

                if (container != null)
                    ItemHelpers.ClearContainerAndRemoveItems(world, container, container.Graphic == 0x2006);
            }
        }

        ItemHelpers.AddItemsToContainerBatch(world, items);
    }
}
