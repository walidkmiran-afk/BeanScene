using System.Collections;

namespace BeanScene.Web.DataStructures;

/// <summary>
/// A hand-written generic doubly linked list. It implements IEnumerable&lt;T&gt; so it works with foreach and LINQ.
/// Used by <see cref="PendingFollowUpQueue"/> to hold the Pending reservations staff must follow up.
/// Demonstrates: nested class, encapsulation, generics, collections and IEnumerable.
/// </summary>
public class DoublyLinkedList<T> : IEnumerable<T>
{
    // Nested class: only the list needs to know about nodes (encapsulation).
    private sealed class Node
    {
        public T Value { get; }
        public Node? Next { get; set; }
        public Node? Previous { get; set; }

        public Node(T value) => Value = value;
    }

    private Node? _head;
    private Node? _tail;

    public int Count { get; private set; }

    public void AddFirst(T value)
    {
        var node = new Node(value);
        if (_head == null)
        {
            _head = _tail = node;
        }
        else
        {
            node.Next = _head;
            _head.Previous = node;
            _head = node;
        }
        Count++;
    }

    public void AddLast(T value)
    {
        var node = new Node(value);
        if (_tail == null)
        {
            _head = _tail = node;
        }
        else
        {
            node.Previous = _tail;
            _tail.Next = node;
            _tail = node;
        }
        Count++;
    }

    /// <summary>Removes the first node holding the value. Returns false if it is not in the list.</summary>
    public bool Remove(T value)
    {
        var comparer = EqualityComparer<T>.Default;
        for (var current = _head; current != null; current = current.Next)
        {
            if (!comparer.Equals(current.Value, value)) continue;

            if (current.Previous != null) current.Previous.Next = current.Next; else _head = current.Next;
            if (current.Next != null) current.Next.Previous = current.Previous; else _tail = current.Previous;
            Count--;
            return true;
        }
        return false;
    }

    public bool Contains(T value)
    {
        var comparer = EqualityComparer<T>.Default;
        for (var current = _head; current != null; current = current.Next)
            if (comparer.Equals(current.Value, value)) return true;
        return false;
    }

    public T First => _head != null ? _head.Value : throw new InvalidOperationException("The list is empty.");
    public T Last => _tail != null ? _tail.Value : throw new InvalidOperationException("The list is empty.");

    public void Clear()
    {
        _head = _tail = null;
        Count = 0;
    }

    /// <summary>Walks head to tail.</summary>
    public IEnumerator<T> GetEnumerator()
    {
        for (var current = _head; current != null; current = current.Next)
            yield return current.Value;
    }

    /// <summary>Walks tail to head (this is what the previous pointers are for).</summary>
    public IEnumerable<T> Reverse()
    {
        for (var current = _tail; current != null; current = current.Previous)
            yield return current.Value;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
