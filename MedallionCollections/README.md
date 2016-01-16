MedallionCollections
==============

MedallionCollections is a lightweight library containing common utilities for working with .NET collections and enumerables. While there are countless potential such utility methods, I've intentionally tried to limit this package to a bare minimum set that I have personally found to be useful time and again over numerous projects.

Download the [NuGet package](https://www.nuget.org/packages/MedallionCollections)

Want to use these functions but don't want an external dependency? Download the inline [NuGet package](https://www.nuget.org/packages/MedallionCollections.Inline/)

## Collection Equality

The `CollectionEquals` extension provides a tuned implementation of [this method](http://www.codeducky.org/engineering-a-collection-equality-function/) for comparing two IEnumerables for equality without regard for order.

```C#
new[] { 1, 2, 3 }.CollectionEquals(new[] { 3, 2, 1 }) // true
```

## Append/Prepend

`Enumerable.Concat` is very handy, but it can be a poor choice for constructing lists due to [quadratic runtime performance](http://blogs.msdn.com/b/wesdyer/archive/2007/03/23/all-about-iterators.aspx). The `Append` and `Prepend` methods allow for construction of lazy sequences which will always iterate in linear time. Furthermore, these can be used to build sequences one element at a time without the cost of wrapping each element in its own tiny collection.

```C#
new[] { 1, 2, 3 }
	.Append(4)
	.Append(new[] { 5, 6, 7 })
	.Prepend(0) // [0, 1, 2, 3, 4, 5, 6, 7]
	
// runs in 2.1s on my machine
Enumerable.Range(0, 10000)
	.Aggregate(Enumerable.Empty<int>(), (e, i) => e.Concat(new[] { i })
	.Count();
	
// runs in 0.01s on my machine
Enumerable.Range(0, 10000)
	.Aggregate(Enumerable.Empty<int>(), (e, i) => e.Append(i))
	.Count();
```

## (Equality)Comparers

The `Comparers` and `EqualityComparers` clases contains static factory methods which make it easy to spin up custom `IComparer<T>` and `IEqualityComparer<T>` implementations.

```C#
// create an EqualityComparer based on equals and hash functions
EqualityComparers.Create<string>((a, b) => a.Length == b.Length, hash: s => s.Length);

// get a comparer that performs object identity comparisons and hashing
// even for types that override Equals() and GetHashCode()
EqualityComparers.GetReferenceComparer<string>();

// get a comparer that compares collections as if with CollectionEquals()
// or sequences as if with SequenceEqual
EqualityComparers.GetCollectionComparer<int>();
EqualityComparers.GetSequenceComparer<int>();

// create a (equality)comparer that compares elements based on
// a selected value
EqualityComparers.Create((string s) => s.Length)
Comparers.Create((string s) => s.Length);

// create a reverse-ordered comparer
StringComparer.OrdinalIgnoreCase.Reverse();

// create a comparer which breaks ties
Comparer.Create((Customer c) => c.Name)
	.ThenBy((Customer c) => c.Id);
	
// create a comparer which compares sequences lexographically
Comparer.GetSequenceComparer<int>()
```

## Traverse

The `Traverse` class contains utility methods for iterating over implicit DAGs. These methods often allow recursive algorithms to be written concisely inline. For example:

```C#
Exception ex = ...
// get the innermost exception for an exception
Traverse.Along(ex, e => e.InnerException).Last();

// find any OperationCanceledExceptions in an AggregateException
// note that you can also specify breadth-first search
AggregateException ex = ...
Traverse.DepthFirst(
	   (Exception)ex, 
		e => (e as AggregateException)?.InnerExceptions 
			?? Enumerable.Empty<Exception>()
	)
	.OfType<OperationCanceledException>()
```

## GetOrAdd

The simple `GetOrAdd` extension of `IDictionary<TKey, TValue>` makes it easy to use any dictionary like a cache (much like the [method](https://msdn.microsoft.com/en-us/library/ee378677(v=vs.110).aspx) in `ConcurrentDictionary<TKey, TValue>`).

```C#
// memoize a function
Func<int, int> toBeMemoized = ...
var cache = new Dictionary<int, int>();
Func<int, int> memoized = i => dictionary.GetOrAdd(i, toBeMemoized);
```

## Partition

`Partition` lazily splits a sequence into equal-sized chunks.

```C#
// returns [[0, 1, 2], [3, 4, 5], [6, 7, 8], [9]]
Enumerable.Range(0, 10).Partition(3)
```

## Empty

The `Empty` class contains static, cached, immutable implementations of all generic and non-generic collection interfaces. Under the hood these are implemented by a specialized implementation with minimal overhead. For example, GetEnumerator() does not require allocation.

```C#
Empty.Dictionary<string, object>().ContainsKey("foo") // false
```

Something else you'd like to see here? Let me know!