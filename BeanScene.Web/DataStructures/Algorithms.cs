namespace BeanScene.Web.DataStructures;

/// <summary>Hand-written sorting algorithms (not List.Sort) to demonstrate the technique.</summary>
public static class SortingAlgorithms
{
    /// <summary>Stable merge sort, O(n log n). Returns a new sorted list; the input is not changed.</summary>
    public static List<T> MergeSort<T>(IReadOnlyList<T> items, Comparison<T> compare)
    {
        if (items.Count <= 1) return new List<T>(items);

        var middle = items.Count / 2;
        var left = MergeSort(items.Take(middle).ToList(), compare);
        var right = MergeSort(items.Skip(middle).ToList(), compare);
        return Merge(left, right, compare);
    }

    private static List<T> Merge<T>(List<T> left, List<T> right, Comparison<T> compare)
    {
        var result = new List<T>(left.Count + right.Count);
        int i = 0, j = 0;
        while (i < left.Count && j < right.Count)
        {
            // <= keeps equal items in their original order (stable)
            if (compare(left[i], right[j]) <= 0) result.Add(left[i++]);
            else result.Add(right[j++]);
        }
        while (i < left.Count) result.Add(left[i++]);
        while (j < right.Count) result.Add(right[j++]);
        return result;
    }

    /// <summary>In-place quick sort (Lomuto partition), average O(n log n).</summary>
    public static void QuickSort<T>(IList<T> items, Comparison<T> compare)
    {
        QuickSort(items, 0, items.Count - 1, compare);
    }

    private static void QuickSort<T>(IList<T> items, int low, int high, Comparison<T> compare)
    {
        if (low >= high) return;
        var pivotIndex = Partition(items, low, high, compare);
        QuickSort(items, low, pivotIndex - 1, compare);
        QuickSort(items, pivotIndex + 1, high, compare);
    }

    private static int Partition<T>(IList<T> items, int low, int high, Comparison<T> compare)
    {
        var pivot = items[high];
        var i = low - 1;
        for (var j = low; j < high; j++)
        {
            if (compare(items[j], pivot) <= 0)
            {
                i++;
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
        (items[i + 1], items[high]) = (items[high], items[i + 1]);
        return i + 1;
    }
}

/// <summary>Hand-written searching algorithms.</summary>
public static class SearchingAlgorithms
{
    /// <summary>Linear search, O(n). Returns the index of the first match or -1.</summary>
    public static int LinearSearch<T>(IReadOnlyList<T> items, Predicate<T> match)
    {
        for (var i = 0; i < items.Count; i++)
            if (match(items[i])) return i;
        return -1;
    }

    /// <summary>
    /// Binary search, O(log n). The list MUST already be sorted with the same comparison.
    /// Returns the index of a matching item or -1. <paramref name="compareToKey"/> returns
    /// less than 0 if the item sorts before the key, 0 if equal, more than 0 if after.
    /// </summary>
    public static int BinarySearch<T>(IReadOnlyList<T> sortedItems, Func<T, int> compareToKey)
    {
        int low = 0, high = sortedItems.Count - 1;
        while (low <= high)
        {
            var mid = low + (high - low) / 2;
            var result = compareToKey(sortedItems[mid]);
            if (result == 0) return mid;
            if (result < 0) low = mid + 1;
            else high = mid - 1;
        }
        return -1;
    }
}
