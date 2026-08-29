using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal static class SubscriptionVolumePolicy
{
    public const string OutsideSpatialGridDetailCode = "subscriptionVolumeOutOfRange";
    public const string TooManyCellsDetailCode = "subscriptionVolumeTooLarge";

    public static bool TryValidate(WorldVolume volume, double cellSize, int maximumCellCount, out string? detailCode)
    {
        if (maximumCellCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCellCount), maximumCellCount, "Maximum subscription cell count must be greater than zero.");
        }

        SpatialCell minCell;
        SpatialCell maxCell;
        try
        {
            minCell = SpatialGrid.ToCell(new WorldPoint(volume.MinX, volume.MinY, volume.MinZ), cellSize);
            maxCell = SpatialGrid.ToCell(new WorldPoint(volume.MaxX, volume.MaxY, volume.MaxZ), cellSize);
        }
        catch (ArgumentOutOfRangeException)
        {
            detailCode = OutsideSpatialGridDetailCode;
            return false;
        }

        var widthInCells = (long)maxCell.X - minCell.X + 1L;
        var depthInCells = (long)maxCell.Y - minCell.Y + 1L;
        var heightInCells = (long)maxCell.Z - minCell.Z + 1L;
        if (widthInCells > maximumCellCount || depthInCells > maximumCellCount || heightInCells > maximumCellCount)
        {
            detailCode = TooManyCellsDetailCode;
            return false;
        }

        var horizontalCellCount = widthInCells * depthInCells;
        if (horizontalCellCount > maximumCellCount || heightInCells > maximumCellCount / horizontalCellCount)
        {
            detailCode = TooManyCellsDetailCode;
            return false;
        }

        detailCode = null;
        return true;
    }
}
