using Medallion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Playground.IndexedCollection.Tests
{
    public class RandomizedBinaryTreeTest
    {
        private readonly ITestOutputHelper _output;

        public RandomizedBinaryTreeTest(ITestOutputHelper output)
        {
            this._output = output;
        }

        [Fact]
        public void SimpleTest()
        {
            var sw = new System.Diagnostics.Stopwatch();

            var dict = new SortedDictionary<int, string>();
            sw.Restart();
            foreach (var i in Enumerable.Range(0, 1000))
            {
                dict.Add(i, i.ToString());
            }
            this._output.WriteLine("Dict: " + sw.Elapsed.ToString());
            
            var rbt = new RandomizedBinaryTree<int, string>();
            foreach (var i in Enumerable.Range(0, 1000))
            {
                rbt.Add(i, i.ToString());
            }
            rbt.Clear();
            rbt.Count.ShouldEqual(0);

            sw.Restart();
            foreach (var i in Enumerable.Range(0, 1000))
            {
                rbt.Add(i, i.ToString());
            }
            this._output.WriteLine("RBT: " + sw.Elapsed.ToString());
            rbt.CheckInvariants();

            this._output.WriteLine(rbt.MaxDepth() + " vs. " + Math.Log(rbt.Count, 2));

            rbt.SequenceShouldEqual(Enumerable.Range(0, 1000).Select(i => new KeyValuePair<int, string>(i, i.ToString())));

            rbt.TryGetValue(51, out var value51).ShouldEqual(true);
            value51.ShouldEqual("51");
            rbt.TryGetValue(2000, out var value2000).ShouldEqual(false);
            value2000.ShouldEqual(null);

            rbt.TryGetValue(501, out var value501).ShouldEqual(true);
            rbt.Remove(501).ShouldEqual(true);
            rbt.CheckInvariants();
            rbt.TryGetValue(501, out var value510).ShouldEqual(false);
            rbt.Remove(501).ShouldEqual(false);

            for (var i = 0; i < 1000; i += 2)
            {
                rbt.Remove(i).ShouldEqual(true);
            }

            rbt.SequenceShouldEqual(Enumerable.Range(0, 1000).Where(i => i % 2 == 1 && i != 501).Select(i => new KeyValuePair<int, string>(i, i.ToString())));
            this._output.WriteLine(rbt.MaxDepth() + " vs. " + Math.Log(rbt.Count, 2));
        }
    }
}
