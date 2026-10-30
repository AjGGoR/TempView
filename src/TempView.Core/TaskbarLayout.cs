namespace TempView.Core;

public static class TaskbarLayout
{
    /// <summary>Finds a fully free horizontal interval. Coordinates may be negative on secondary displays.</summary>
    public static int? TryFindGap(int left, int right, int width, string side, IEnumerable<(int Left, int Right)> occupied)
    {
        ArgumentNullException.ThrowIfNull(occupied);
        if (side is not ("left" or "right")) throw new ArgumentException("Side must be left or right.", nameof(side));
        if (width <= 0 || (long)right - left < width) return null;

        long cursor = left;
        int? result = null;
        foreach (var interval in occupied.Where(i => i.Right > i.Left && i.Right > left && i.Left < right).OrderBy(i => i.Left).ThenBy(i => i.Right))
        {
            long start = Math.Max((long)left, interval.Left);
            long end = Math.Min((long)right, interval.Right);
            if (start - cursor >= width)
            {
                if (side == "left") return (int)cursor;
                result = (int)(start - width);
            }
            cursor = Math.Max(cursor, end);
        }
        if ((long)right - cursor >= width) return side == "left" ? (int)cursor : right - width;
        return result;
    }
}
