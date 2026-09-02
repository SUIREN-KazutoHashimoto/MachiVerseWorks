using System.Globalization;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal static class ObservationCacheIdentity
{
    public static string ForVolume(WorldVolume volume) => string.Join(
        ':',
        BitConverter.DoubleToInt64Bits(volume.MinX).ToString("X16", CultureInfo.InvariantCulture),
        BitConverter.DoubleToInt64Bits(volume.MinY).ToString("X16", CultureInfo.InvariantCulture),
        BitConverter.DoubleToInt64Bits(volume.MinZ).ToString("X16", CultureInfo.InvariantCulture),
        BitConverter.DoubleToInt64Bits(volume.MaxX).ToString("X16", CultureInfo.InvariantCulture),
        BitConverter.DoubleToInt64Bits(volume.MaxY).ToString("X16", CultureInfo.InvariantCulture),
        BitConverter.DoubleToInt64Bits(volume.MaxZ).ToString("X16", CultureInfo.InvariantCulture));

    public static string ForEntity(ulong entityId) => entityId.ToString(CultureInfo.InvariantCulture);

    public static string ForChunk(WorldVolume volume, int chunkIndex) =>
        string.Concat(ForVolume(volume), ":", chunkIndex.ToString(CultureInfo.InvariantCulture));
}
