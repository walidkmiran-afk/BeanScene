using BeanScene.Web.Models;

namespace BeanScene.Web.DataStructures;

/// <summary>
/// Holds Pending reservations in the order staff should follow them up (soonest sitting first),
/// using the custom DoublyLinkedList and merge sort. Also supports a fast last-name lookup using binary search.
/// </summary>
public class PendingFollowUpQueue : IEnumerable<Reservation>
{
    private readonly DoublyLinkedList<Reservation> _queue = new();
    private readonly List<Reservation> _byLastName;

    public PendingFollowUpQueue(IEnumerable<Reservation> pendingReservations)
    {
        var all = pendingReservations.ToList();

        // Sort by sitting date, then start time (merge sort), then store in the linked list.
        var bySitting = SortingAlgorithms.MergeSort(all, (a, b) =>
        {
            var byDate = a.Sitting.SittingDate.CompareTo(b.Sitting.SittingDate);
            return byDate != 0 ? byDate : a.StartTime.CompareTo(b.StartTime);
        });
        foreach (var r in bySitting) _queue.AddLast(r);

        // A second ordering (quick sort, in place) used for binary search by last name.
        _byLastName = new List<Reservation>(all);
        SortingAlgorithms.QuickSort(_byLastName,
            (a, b) => string.Compare(a.Customer.LastName, b.Customer.LastName, StringComparison.OrdinalIgnoreCase));
    }

    public int Count => _queue.Count;

    /// <summary>Finds a pending reservation by guest last name (binary search), or null.</summary>
    public Reservation? FindByLastName(string lastName)
    {
        var index = SearchingAlgorithms.BinarySearch(_byLastName,
            r => string.Compare(r.Customer.LastName, lastName, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? _byLastName[index] : null;
    }

    public IEnumerator<Reservation> GetEnumerator() => _queue.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
