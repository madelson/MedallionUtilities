MedallionRandom
==============

MedallionRandom is a lightweight library containing common utilities for working with random numbers. While there are countless potential such utility methods, I've intentionally tried to limit this package to a bare minimum set that I have personally found to be useful time and again over numerous projects.

Download the [NuGet package](https://www.nuget.org/packages/MedallionRandom) [![NuGet Status](http://img.shields.io/nuget/v/MedallionRandom.svg?style=flat)](https://www.nuget.org/packages/MedallionRandom/)

Want to use these functions but don't want an external dependency? Download the [inline NuGet package](https://www.nuget.org/packages/MedallionRandom.Inline/) [![NuGet Status](http://img.shields.io/nuget/v/MedallionRandom.Inline.svg?style=flat)](https://www.nuget.org/packages/MedallionRandom.Inline/). For more about inline NuGet packages, check out [this post](http://www.codeducky.org/inline-nuget-packages-a-solution-to-the-problem-of-utility-libraries/).

## Static random APIs
Languages like Java and Javascript provide easy access to one-off random numbers via `Math.random()`. .NET, on the other hand, has on the <a href="https://msdn.microsoft.com/en-us/library/system.random.aspx">System.Random</a> class which is not thread-safe and somewhat expensive to initialize. Furthermore, new `Random` objects are initialized with the current time by default, which means that they can easily end up with the same internal seed if they are created close together in time. 

MedallionRandom addresses these issues through several static helper methods:

```C#
// provides static access to random doubles in [0, 1) much like Java's Math.random()
double value = Rand.NextDouble();

// provides static access to random integers in a range
int value = Rand.Next(minValue, maxValue);

// provides static access to a thread-safe Random instance. The underlying state is ThreadStatic
// so there is no locking overhead to using this property concurrently on many threads
Random random = Rand.Current;

// creates a new Random whose seed is a mix of the current time and another random value such that
// any two Random objects created by this method are highly unlikely to have the same seed
Random random = Rand.Create();
```

## Extensions on Random

The `Random` class comes with a variety of methods to generate different types of random numbers. MedallionRandom fleshes out this suite with a number of extension methods on `Random` that pull different types of random numbers from the generator. Note that these extensions still use the native `Random` APIs as the underlying source of randomness, meaning that they will work for any implementation of `Random`:

```C#
var random = new Random();

// random true or false value
bool value = random.NextBoolean();

// returns true with the given probability or false otherwise
bool value = random.NextBoolean(probability);

// returns a random integer in the full range of Int32
// (unlike Next() which returns an integer in [0, int.MaxValue - 1)
int value = random.NextInt32();

// returns a random integer in the full range of Int64
long value = random.NextInt64();

// returns a random float value in [0, 1)
float value = random.NextSingle();

// returns a random double in [0, max)
double value = random.NextDouble(max);

// returns a random double in [min, max)
double value = random.NextDouble(min, max);

// samples a value from a normal distribution with mean 0
// and standard deviation 1
double value = random.NextGaussian();

// returns an infinite stream of random values which would be
// generated via repeated calls to NextDouble()
IEnumerable<double> values = random.NextDoubles();

// returns an infinite stream of random bytes which would be
// generated via repeated calls to NextBytes()
IEnumerable<byte> values = random.NextBytes();
```

## Cryptographic random number generator interop

As in many frameworks, the `Random` class in .NET is fast but unsuitable for applications like cryptography or games of chance where money is on the line. 

For such applications, a class like <a href="https://msdn.microsoft.com/en-us/library/system.security.cryptography.rngcryptoserviceprovider.aspx">System.Security.Cryptography.RNGCryptoServiceProvider</a> is preferred instead. Unfortunately, this extends <a href="https://msdn.microsoft.com/en-us/library/system.security.cryptography.randomnumbergenerator.aspx">System.Security.RandomNumberGenerator</a> rather than `Random`. `RandomNumberGenerator` only exposes a few public methods, all of which are limited to filling byte arrays with random bytes. 

To make these high-quality random number generators easier to use, MedallionRandom provides an adapter implementation of `Random` which gives you access to the wide variety of `Random` APIs while pulling from the high-quality output of the underlying `RandomNumberGenerator`:

```C#
using (RandomNumberGenerator randomNumberGenerator = RNGCryptoServiceProvider.Create())
{
	Random cryptoRandom = randomNumberGenerator.AsRandom();
	double value = cryptoRandom.NextDouble();
}
```

## Shuffling

A common use-case for randomness is shuffling a sequence of elements. MedallionRandom provides an implementation of the <a href="https://en.wikipedia.org/wiki/Fisher%E2%80%93Yates_shuffle">Fisher-Yates Shuffle</a> algorithm:

```C#
var random = new Random();

// the Shuffled() method performs a "streaming" shuffle on any IEnumerable<T>
IEnumerable<int> sequence = Enumerable.Range(0, 100);
// shuffles using the provided random instance
IEnumerable<int> shuffled = sequence.Shuffled(random);
// shuffles using Rand.Current
IEnumerable<int> shuffled = sequence.Shuffled(random);

// the Shuffle() method performs an in-place shuffle on any IList<T>
List<int> list = Enumerable.Range(0, 100).ToList();
// shuffles using the provided random instance
list.Shuffle(random);
// shuffles using Rand.Current
list.Shuffle();
```

## JRE-compliant randomness
The <a href="https://docs.oracle.com/javase/8/docs/api/java/util/Random.html">Java platform's java.util.Random</a> class specifies an exact algorithm and exact output sequence that can be expected to be the same on across versions and implementations. 

For cases where consistency is especially important, MedallionRandom contains an implementation of `Random` which replicates the JRE's implementation byte for byte, producing the exact same sequence of numbers given the same seed and API calls:

```C#
Random random = Rand.CreateJavaRandom(seed: 0xDeadBeef);
```