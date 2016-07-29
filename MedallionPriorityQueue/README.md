MedallionPriorityQueue
==============

MedallionPriorityQueue contains a fast implementation of a [Priority Queue](https://docs.oracle.com/javase/7/docs/api/java/util/PriorityQueue.html) data structure based on a [binary heap](https://en.wikipedia.org/wiki/Binary_heap).

Download the [NuGet package](https://www.nuget.org/packages/MedallionPriorityQueue) [![NuGet Status](http://img.shields.io/nuget/v/MedallionPriorityQueue.svg?style=flat)](https://www.nuget.org/packages/MedallionPriorityQueue/)

Want to use this data structure but don't want an external dependency? Download the [inline NuGet package](https://www.nuget.org/packages/MedallionPriorityQueue.Inline/) [![NuGet Status](http://img.shields.io/nuget/v/MedallionPriorityQueue.Inline.svg?style=flat)](https://www.nuget.org/packages/MedallionPriorityQueue.Inline/). For more about inline NuGet packages, check out [this post](http://www.codeducky.org/inline-nuget-packages-a-solution-to-the-problem-of-utility-libraries/).

## API

The PriorityQueue\<T> class implements the [ICollection\<T>](https://msdn.microsoft.com/en-us/library/92t2ye13.aspx) and [IReadOnlyCollection\<T>](https://msdn.microsoft.com/en-us/library/hh881542.aspx) interfaces. It also provides additional methods specific to a priority queue:

Queues can be initialized with a custom instance of [IComparer\<T>](https://msdn.microsoft.com/en-us/library/8ehhxeaf.aspx) upon construction to determine how elements are sorted (e. g. a reverse comparer could be used to create a max queue instead of a min queue).

| Method | Description |
| ------ | --------- |
| Enqueue(T) | Adds an item to the queue (equivalent to [ICollection\<T>.Add(T)](https://msdn.microsoft.com/en-us/library/63ywd54z.aspx). Runs in O(log(N)) time. |
| EnqueueRange(IEnumerable\<T>) | Adds a collection of items to the queue. Runs in O(N) time if the input collection size k is larger than the queue or O(k * log(N)) time otherwise |
| Peek() | Returns the minimum item in the queue without removing it. Runs in O(1) time. |
| Dequeue() | Removes and returns the minimum item in the queue. Runs in O(log(N)) time. |
| Comparer | Exposes the IComparer\<T> |

## Usage

Priority queues have many uses. Here's an example showing how to find the top N elements of a sequence in a streaming fashion:

```C#
public static List<T> TopN(IEnumerable<T> sequence, int n) 
{
	var pq = new PriorityQueue<T>();
	foreach (var element in sequence) 
	{
		pq.Enqueue(element);
		if (pq.Count > n) { pq.Dequeue(); }
	}

	return pq.ToList();
}
```