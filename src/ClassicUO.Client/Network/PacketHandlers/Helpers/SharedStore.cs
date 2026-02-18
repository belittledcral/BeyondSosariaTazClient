using System.Collections.Generic;
using ClassicUO.Game;

namespace ClassicUO.Network.PacketHandlers.Helpers;

internal static class SharedStore
{
    private static readonly HashSet<uint> _cliLocRequests = [];
    private static readonly HashSet<uint> _customHouseRequests = [];
    private static readonly List<uint> _cliLocBatch = new(15);

    public static uint RequestedGridLoot { get; set; }

    public static void AddMegaCliLocRequest(uint serial)
    {
        _cliLocRequests.Add(serial);
    }

    public static void AddCustomHouseRequest(uint serial)
    {
        _customHouseRequests.Add(serial);
    }

    public static void SendMegaCliLocRequests(World world)
    {
        if (world.ClientFeatures.TooltipsEnabled && _cliLocRequests.Count != 0)
        {
            if (Client.Game.UO.Version >= Utility.ClientVersion.CV_5090)
            {
                _cliLocBatch.Clear();
                foreach (uint serial in _cliLocRequests)
                {
                    _cliLocBatch.Add(serial);
                    if (_cliLocBatch.Count >= 15)
                    {
                        AsyncNetClient.Socket.Send_MegaClilocRequestBatch(_cliLocBatch);
                        _cliLocBatch.Clear();
                    }
                }
                if (_cliLocBatch.Count > 0)
                    AsyncNetClient.Socket.Send_MegaClilocRequestBatch(_cliLocBatch);

                _cliLocRequests.Clear();
            }
            else
            {
                foreach (uint serial in _cliLocRequests)
                    AsyncNetClient.Socket.Send_MegaClilocRequest_Old(serial);

                _cliLocRequests.Clear();
            }
        }

        if (_customHouseRequests.Count > 0)
        {
            foreach (uint serial in _customHouseRequests)
                AsyncNetClient.Socket.Send_CustomHouseDataRequest(serial);

            _customHouseRequests.Clear();
        }
    }
}
