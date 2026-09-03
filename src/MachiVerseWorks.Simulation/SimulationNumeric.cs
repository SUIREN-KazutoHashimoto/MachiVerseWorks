namespace MachiVerseWorks.Simulation;

internal static class SimulationNumeric
{
    public static double SaturatingAddNonNegative(double left, double right)
    {
        ValidateNonNegativeFinite(left, nameof(left));
        ValidateNonNegativeFinite(right, nameof(right));
        return left > double.MaxValue - right ? double.MaxValue : left + right;
    }

    public static double SaturatingMultiplyNonNegative(double left, double right)
    {
        ValidateNonNegativeFinite(left, nameof(left));
        ValidateNonNegativeFinite(right, nameof(right));
        if (left == 0d || right == 0d) return 0d;
        return left > double.MaxValue / right ? double.MaxValue : left * right;
    }

    public static double SaturatingMultiplyNonNegative(double first, double second, double third) =>
        SaturatingMultiplyNonNegative(SaturatingMultiplyNonNegative(first, second), third);

    public static double SaturatingMultiplyNonNegative(double first, double second, double third, double fourth) =>
        SaturatingMultiplyNonNegative(SaturatingMultiplyNonNegative(first, second, third), fourth);

    public static double SaturatingDoubleSum<T>(IEnumerable<T> source, Func<T, double> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        var total = 0d;
        foreach (var item in source) total = SaturatingAddNonNegative(total, selector(item));
        return total;
    }

    public static long SaturatingLongSum<T>(IEnumerable<T> source, Func<T, long> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        var total = 0L;
        foreach (var item in source)
        {
            var value = selector(item);
            if (value < 0) throw new InvalidOperationException("Saturating monetary aggregation requires non-negative values.");
            if (total > long.MaxValue - value) return long.MaxValue;
            total += value;
        }
        return total;
    }

    public static int SaturatingToInt32NonNegative(long value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        return value >= int.MaxValue ? int.MaxValue : (int)value;
    }

    private static void ValidateNonNegativeFinite(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0d)
            throw new ArgumentOutOfRangeException(name, value, "Value must be finite and non-negative.");
    }
}
