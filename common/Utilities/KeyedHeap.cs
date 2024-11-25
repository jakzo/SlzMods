using System;
using System.Collections.Generic;

namespace Sst.Utilities;

public class KeyedHeap<K, V> {
  public List<(K Key, V Value)> Heap = new();
  public Dictionary<K, int> KeyToIndexInHeap = new();
  public Func<V, V, bool> Comparator;

  public KeyedHeap(Func<V, V, bool> comparator) { Comparator = comparator; }

  // O(1)
  public int Size() => Heap.Count;

  // O(1)
  public (K Key, V Value) Peek() => Heap[0];

  // O(log n)
  public void Push(K key, V value) {
    Heap.Add((key, value));
    KeyToIndexInHeap.Add(key, Heap.Count - 1);
    BubbleUp(Heap.Count - 1);
  }

  // O(log n)
  public (K Key, V Value) Pop() {
    var first = Heap[0];
    KeyToIndexInHeap.Remove(first.Key);
    var last = Heap[Heap.Count - 1];
    Heap.RemoveAt(Heap.Count - 1);
    Heap[0] = last;
    KeyToIndexInHeap[last.Key] = 0;
    SiftDown(0);
    return first;
  }

  // O(log n)
  public void Delete(K key) {
    var index = KeyToIndexInHeap[key];
    KeyToIndexInHeap.Remove(key);
    var end = Heap[Heap.Count - 1];
    Heap.RemoveAt(Heap.Count - 1);
    if (index == Heap.Count)
      return;
    Heap[index] = end;
    KeyToIndexInHeap[end.Key] = index;
    BubbleUp(index);
    SiftDown(index);
  }

  private void BubbleUp(int index) {
    var parentIndex = (index - 1) / 2;
    if (index <= 0 || Comparator(Heap[parentIndex].Value, Heap[index].Value))
      return;
    Swap(index, parentIndex);
    BubbleUp(parentIndex);
  }

  private void SiftDown(int index) {
    var leftIndex = index * 2 + 1;
    var rightIndex = index * 2 + 2;
    var smallest = index;
    if (leftIndex < Heap.Count &&
        !Comparator(Heap[smallest].Value, Heap[leftIndex].Value)) {
      smallest = leftIndex;
    }
    if (rightIndex < Heap.Count &&
        !Comparator(Heap[smallest].Value, Heap[rightIndex].Value)) {
      smallest = rightIndex;
    }
    if (smallest != index) {
      Swap(index, smallest);
      SiftDown(smallest);
    }
  }

  private void Swap(int i, int j) {
    var a = Heap[i];
    var b = Heap[j];
    Heap[i] = b;
    Heap[j] = a;
    KeyToIndexInHeap[a.Key] = j;
    KeyToIndexInHeap[b.Key] = i;
  }
}
