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
            minCell = SpatialGrid.ToCell(new WorldPoint(area.MinX, area.MinY), cellSize);
            maxCell = SpatialGrid.ToCell(new WorldPoint(area.MaxX, area.MaxY), cellSize);
        }
        catch (ArgumentOutOfRangeException)
        {
            detailCode = OutsideSpatialGridDetailCode;
            return false;
        }

        var widthInCells = (long)maxCell.X - minCell.X + 1L;
        var heightInCells = (long)maxCell.Y - minCell.Y + 1L;
        if (widthInCells > maximumCellCount ||
            heightInCells > maximumCellCount ||
            widthInCells * heightInCells > maximumCellCount)
        {
            detailCode = TooManyCellsDetailCode;
            return false;
        }

        detailCode = null;
        return true;
    }
}
