using BeanScene.Web.DataStructures;
using Xunit;

namespace BeanScene.Tests;

public class DoublyLinkedListTests
{
    [Fact]
    public void AddLast_KeepsInsertionOrder()
    {
        var list = new DoublyLinkedList<int>();
        list.AddLast(1); list.AddLast(2); list.AddLast(3);
        Assert.Equal(new[] { 1, 2, 3 }, list.ToArray());
        Assert.Equal(3, list.Count);
    }

    [Fact]
    public void AddFirst_PutsItemAtTheFront()
    {
        var list = new DoublyLinkedList<string>();
        list.AddLast("b"); list.AddFirst("a");
        Assert.Equal(new[] { "a", "b" }, list.ToArray());
        Assert.Equal("a", list.First);
        Assert.Equal("b", list.Last);
    }

    [Fact]
    public void Remove_MiddleItem_RelinksNeighboursInBothDirections()
    {
        var list = new DoublyLinkedList<int>();
        foreach (var n in new[] { 1, 2, 3, 4 }) list.AddLast(n);

        Assert.True(list.Remove(2));

        Assert.Equal(new[] { 1, 3, 4 }, list.ToArray());
        Assert.Equal(new[] { 4, 3, 1 }, list.Reverse().ToArray());   // previous pointers are correct
        Assert.Equal(3, list.Count);
    }

    [Fact]
    public void Remove_HeadAndTail_UpdatesEnds()
    {
        var list = new DoublyLinkedList<int>();
        foreach (var n in new[] { 1, 2, 3 }) list.AddLast(n);
        list.Remove(1); list.Remove(3);
        Assert.Equal(2, list.First);
        Assert.Equal(2, list.Last);
    }

    [Fact]
    public void Remove_MissingItem_ReturnsFalse()
    {
        var list = new DoublyLinkedList<int>();
        list.AddLast(1);
        Assert.False(list.Remove(99));
        Assert.Equal(1, list.Count);
    }

    [Fact]
    public void Empty_First_Throws()
    {
        var list = new DoublyLinkedList<int>();
        Assert.Throws<InvalidOperationException>(() => list.First);
    }

    [Fact]
    public void Contains_And_Clear_Work()
    {
        var list = new DoublyLinkedList<int>();
        list.AddLast(5);
        Assert.True(list.Contains(5));
        list.Clear();
        Assert.False(list.Contains(5));
        Assert.Equal(0, list.Count);
    }
}

public class AlgorithmTests
{
    [Fact]
    public void MergeSort_SortsAscending_AndDoesNotChangeTheInput()
    {
        var input = new List<int> { 5, 2, 9, 1, 5, 6 };
        var sorted = SortingAlgorithms.MergeSort(input, (a, b) => a.CompareTo(b));
        Assert.Equal(new[] { 1, 2, 5, 5, 6, 9 }, sorted);
        Assert.Equal(new[] { 5, 2, 9, 1, 5, 6 }, input);
    }

    [Fact]
    public void MergeSort_IsStable()
    {
        var input = new List<(int Key, string Label)> { (1, "first"), (0, "x"), (1, "second") };
        var sorted = SortingAlgorithms.MergeSort(input, (a, b) => a.Key.CompareTo(b.Key));
        Assert.Equal("first", sorted[1].Label);
        Assert.Equal("second", sorted[2].Label);
    }

    [Fact]
    public void QuickSort_SortsInPlace()
    {
        var items = new List<string> { "pear", "apple", "fig", "banana" };
        SortingAlgorithms.QuickSort(items, (a, b) => string.Compare(a, b, StringComparison.Ordinal));
        Assert.Equal(new[] { "apple", "banana", "fig", "pear" }, items);
    }

    [Fact]
    public void BinarySearch_FindsExistingItem()
    {
        var sorted = new List<int> { 1, 3, 5, 7, 9, 11 };
        var index = SearchingAlgorithms.BinarySearch(sorted, item => item.CompareTo(7));
        Assert.Equal(3, index);
    }

    [Fact]
    public void BinarySearch_ReturnsMinusOne_WhenMissing()
    {
        var sorted = new List<int> { 1, 3, 5 };
        Assert.Equal(-1, SearchingAlgorithms.BinarySearch(sorted, item => item.CompareTo(4)));
    }

    [Fact]
    public void LinearSearch_ReturnsFirstMatchIndex()
    {
        var items = new List<string> { "a", "b", "c", "b" };
        Assert.Equal(1, SearchingAlgorithms.LinearSearch(items, s => s == "b"));
        Assert.Equal(-1, SearchingAlgorithms.LinearSearch(items, s => s == "z"));
    }
}
