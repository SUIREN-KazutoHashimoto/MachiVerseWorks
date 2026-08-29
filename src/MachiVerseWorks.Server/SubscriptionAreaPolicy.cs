using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal static class SubscriptionAreaPolicy
{
    public const string OutsideSpatialGridDetailCode = "subscriptionAreaOutOfRange";
    public const string TooManyCellsDetailCode = "subscriptionAreaTooLarge";

    public static bool TryValidate(
        WorldRect area,
        double cellSize,
        int maximumCellCount,
        out string? detailCode)
    {
        return TryValidate(area.ToVolume(), cellSize, maximumCellCount, out detailCode);
    }

    public static bool TryValidate(
        WorldVolume volume,
        double cellSize,
        int maximumCellCount,
        out string? detailCode)
    {
        if (maximumCellCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCellCount),
                maximumCellCount,
                "Maximum subscription cell count must be greater than zero.");
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
        if (widthInCells > maximumCellCount ||
            depthInCells > maximumCellCount ||
            heightInCells > maximumCellCount)
        {
            detailCode = TooManyCellsDetailCode;
            return false;
        }

        var horizontalCellCount = widthInCells * depthInCells;
        if (horizontalCellCount > maximumCellCount ||
            heightInCells > maximumCellCount / horizontalCellCount)
        {
            detailCode = TooManyCellsDetailCode;
            return false;
        }

        detailCode = null;
        return true;
    }
}
