MedallionCollections
==============

MedallionCollections is a lightweight library containing common utilities for working with .NET collections and enumerables. While there are countless potential such utility methods, I've intentionally tried to limit this package to a bare minimum set that I have personally found to be useful time and again over numerous projects.

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

```
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

- collection equality
- append
- comparers
- traverse
- getoradd
- partition
- empty