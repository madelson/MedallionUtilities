using Medallion.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Medallion.PriorityQueue
{
    public class PriorityQueueTest
    {
        private readonly ITestOutputHelper output;

        public PriorityQueueTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void TestBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new PriorityQueue<int>(items: null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PriorityQueue<int>(initialCapacity: -1));

            var pq = new PriorityQueue<int>();
            Assert.Throws<ArgumentNullException>(() => pq.EnqueueRange(null));
            
            pq.Enqueue(1);
            Assert.Throws<ArgumentException>(() => ((ICollection<int>)pq).CopyTo(new int[0], 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ((ICollection<int>)pq).CopyTo(new int[0], -1));
            Assert.Throws<ArgumentException>(() => ((ICollection<int>)pq).CopyTo(new int[2], 2));
        }

        [Fact]
        public void TestInitialCapacity()
        {
            var pq = new PriorityQueue<int>(0);
            pq.Enqueue(2);
            pq.Dequeue().ShouldEqual(2);
        }

        [Fact]
        public void TestSort()
        {
            var random = new Random(12345);
            var numbers = random.NextDoubles().Take(1000).ToArray();

            var pq = new PriorityQueue<double>(numbers);
            var results = new List<double>();
            while (pq.Count > 0)
            {
                results.Add(pq.Dequeue());
            }

            results.SequenceShouldEqual(numbers.OrderBy(i => i));
        }

        [Fact]
        public void TestMaintainTopN()
        {
            const int N = 10;
            var numbers = Enumerable.Range(0, 1000).Shuffled(Rand.CreateJavaRandom(1));

            var pq = new PriorityQueue<int>();
            foreach (var n in numbers)
            {
                pq.Enqueue(n);
                if (pq.Count > N) { pq.Dequeue(); }
            }

            pq.CollectionEquals(Enumerable.Range(1000 - N, N)).ShouldEqual(true);
        }

        [Fact]
        public void TestMaintainTopNBatchAdds()
        {
            const int N = 25;

            var numbers = new Queue<int>(Enumerable.Range(0, 1000));
            var rand = Rand.CreateJavaRandom(123);

            var pq = new PriorityQueue<int>();
            while (numbers.Count > 0)
            {
                var amountToAdd = Math.Min(numbers.Count, (int)Math.Round(50 * Math.Abs(rand.NextGaussian())));
                pq.EnqueueRange(Enumerable.Range(0, amountToAdd).Select(_ => numbers.Dequeue()));
                while (pq.Count > N) { pq.Dequeue(); }
            }

            pq.CollectionEquals(Enumerable.Range(1000 - N, N)).ShouldEqual(true);
        }

        [Fact]
        public void TestMaintainBottomN()
        {
            const int N = 10;
            var numbers = Enumerable.Range(0, 1000).Shuffled(Rand.CreateJavaRandom(1));

            var pq = new PriorityQueue<int>(Comparers.Reverse<int>());
            foreach (var n in numbers)
            {
                pq.Enqueue(n);
                if (pq.Count > N) { pq.Dequeue(); }
            }

            pq.CollectionEquals(Enumerable.Range(0, N)).ShouldEqual(true);
        }

        [Fact]
        public void TestEnqueuePeekAndDequeue()
        {
            var pq = new PriorityQueue<int>();
            pq.Count.ShouldEqual(0);
            Assert.Throws<InvalidOperationException>(() => pq.Peek());
            Assert.Throws<InvalidOperationException>(() => pq.Dequeue());

            pq.Enqueue(7);
            pq.Peek().ShouldEqual(7);
            pq.Enqueue(4);
            pq.Peek().ShouldEqual(4);
            pq.Count.ShouldEqual(2);
            pq.Dequeue().ShouldEqual(4);
            pq.Count.ShouldEqual(1);
        }

        [Fact]
        public void TestEnumerator()
        {
            var pq = new PriorityQueue<string> { "a", "b", "c" };
            var array = pq.ToArray();
            array.OrderBy(s => s).SequenceShouldEqual(new[] { "a", "b", "c" });

            using (var enumerator = pq.GetEnumerator())
            {
                enumerator.MoveNext().ShouldEqual(true);
                pq.Enqueue("d");
                Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
            }
        }

        [Fact]
        public void TestRemove()
        {
            var pq = new PriorityQueue<int> { 1, 2, 3, 4, 5, 6 };
            pq.Remove(3).ShouldEqual(true);
            pq.Count.ShouldEqual(5);
            pq.Remove(3).ShouldEqual(false);
            pq.Count.ShouldEqual(5);
            pq.Remove(0).ShouldEqual(false);
            pq.Dequeue().ShouldEqual(1);
            pq.Remove(2).ShouldEqual(true);
            pq.Dequeue().ShouldEqual(4);
            pq.Remove(6).ShouldEqual(true);
            pq.Dequeue().ShouldEqual(5);
            pq.Count.ShouldEqual(0);

            var absoluteQueue = new PriorityQueue<int>(Comparers.Create<int, int>(Math.Abs));
            absoluteQueue.Remove(2).ShouldEqual(false);
            absoluteQueue.Enqueue(2);
            absoluteQueue.Remove(-2).ShouldEqual(true);
        } 

        [Fact]
        public void TestContains()
        {
            var pq = new PriorityQueue<int>(Enumerable.Range(0, 1000));
            pq.Contains(-1).ShouldEqual(false);
            pq.Contains(999).ShouldEqual(true);
            pq.Contains(500).ShouldEqual(true);
            pq.Contains(1000).ShouldEqual(false);

            var absoluteQueue = new PriorityQueue<int>(Comparers.Create<int, int>(Math.Abs));
            absoluteQueue.Contains(9000).ShouldEqual(false);
            absoluteQueue.Add(9000);
            absoluteQueue.Contains(9000).ShouldEqual(true);
            absoluteQueue.Contains(-9000).ShouldEqual(true);
        }

        [Fact]
        public void TestClear()
        {
            var pq = new PriorityQueue<int>();
            pq.Enqueue(1);
            pq.Clear();
            pq.Count.ShouldEqual(0);
            pq.EnqueueRange(Enumerable.Range(0, 1000));
            pq.EnqueueRange(Enumerable.Range(100, 2000));
            pq.Clear();
            pq.Count.ShouldEqual(0);
            pq.Enqueue(900);
            pq.Dequeue().ShouldEqual(900);
            pq.Clear();
            pq.Count.ShouldEqual(0);
        }

        [Fact]
        public void TestCopyTo()
        {
            ICollection<string> pq = new PriorityQueue<string>();

            pq.CopyTo(new string[0], 0);

            pq.Add("a");
            pq.Add("b");

            Assert.Throws<ArgumentNullException>(() => pq.CopyTo(null, 0));
            Assert.Throws<ArgumentException>(() => pq.CopyTo(new string[1], 0));
            Assert.Throws<ArgumentException>(() => pq.CopyTo(new string[4], 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => pq.CopyTo(new string[4], int.MinValue));

            var strings = new string[4];
            pq.CopyTo(strings, 1);
            strings.SequenceShouldEqual(new[] { null, "a", "b", null });
        }

        [Fact]
        public void TestGarbageCollection()
        {
            var pq = new PriorityQueue<string>();
            var reference = AddWeak(pq, () => Guid.NewGuid().ToString());
            CollectGarbage();
            reference.IsAlive.ShouldEqual(true);

            new Action(() => pq.Dequeue())();
            CollectGarbage();
            reference.IsAlive.ShouldEqual(false);

            reference = AddWeak(pq, () => Guid.NewGuid().ToString());
            new Func<WeakReference, bool>(r => pq.Remove((string)r.Target))(reference).ShouldEqual(true);
            CollectGarbage();
            reference.IsAlive.ShouldEqual(false);

            reference = AddWeak(pq, () => Guid.NewGuid().ToString());
            pq.Clear();
            CollectGarbage();
            reference.IsAlive.ShouldEqual(false);

            // make sure pq being GC'd isn't the cause of success
            GC.KeepAlive(pq);
        }

        private static void CollectGarbage()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference AddWeak<T>(PriorityQueue<T> queue, Func<T> valueFactory)
            where T : class
        {
            var value = valueFactory();
            queue.Add(value);
            return new WeakReference(value);
        }        
    }
}
