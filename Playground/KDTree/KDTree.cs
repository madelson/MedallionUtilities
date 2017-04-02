using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.KDTree
{
    // idea: self-balancing tree which annotates dimensions on nodes and preserves them during rotations
    // doesn't quite work => naive rotation can break invariant

    public sealed class KDTree<TPoint, TValue>
    {
        private readonly IKDPointComparer<TPoint> comparer;
        private IReadOnlyList<PointAndValueComparer> singleDimensionComparers;
        private Node root;

        public KDTree(IKDPointComparer<TPoint> comparer = null)
        {
            this.comparer = comparer; // todo null
        }

        public KDTree(IEnumerable<KeyValuePair<TPoint, TValue>> points, IKDPointComparer<TPoint> comparer)
            : this(comparer)
        {
            if (points == null) { throw new ArgumentNullException(nameof(points)); }

            var pointsArray = points.ToArray();
            if (pointsArray.Any(kvp => kvp.Key == null)) { throw new ArgumentNullException(nameof(points), "keys must be non-null"); }

            if (pointsArray.Length > 0)
            {
                this.EnsureInitialized(pointsArray[0].Key);
                this.root = this.BuildTree(pointsArray, 0, pointsArray.Length - 1, dimension: 0);
                this.Count = pointsArray.Length;
            }
        }

        public int Count { get; private set; }

        #region ---- Initialization ----
        private void EnsureInitialized(TPoint point)
        {
            if (this.singleDimensionComparers == null)
            {
                (this.comparer as INeedsInitializationKDPointComparer<TPoint>)?.InitializeFrom(point);
                this.singleDimensionComparers = Enumerable.Range(0, this.comparer.Dimensions)
                    .Select(i => new PointAndValueComparer(this.comparer, i))
                    .ToArray();
            }
        }

        private Node BuildTree(KeyValuePair<TPoint, TValue>[] points, int left, int right, int dimension)
        {
            if (right - left < 0) { return null; }

            // short-circuit on the leaf node case
            if (left == right) { return new Node { point = points[left].Key, value = points[left].Value }; }

            // partition the list to find the median point
            var median = Selection.Select(points, left: left, right: right, k: left + ((right - left) / 2), comparer: this.singleDimensionComparers[dimension]);

            var nextDimension = this.NextDimension(dimension);
            return new Node
            {
                point = points[median].Key,
                value = points[median].Value,
                left = this.BuildTree(points, left, median - 1, nextDimension),
                right = this.BuildTree(points, median + 1, right, nextDimension),
            };
        }

        private int NextDimension(int dimension) => dimension == this.comparer.Dimensions ? 0 : dimension + 1;
        #endregion

        #region ---- Add ----
        public void Add(TPoint point, TValue value)
        {
            if (point == null) { throw new ArgumentNullException(nameof(point)); }
            this.EnsureInitialized(point);

            this.AddToTree(ref this.root, point, value, dimension: 0);
            ++this.Count;
        }

        private void AddToTree(ref Node node, TPoint point, TValue value, int dimension)
        {
            if (node == null)
            {
                node = new Node { point = point, value = value };
            }
            else if (this.comparer.Compare(point, node.point, dimension) <= 0)
            {
                this.AddToTree(ref node.left, point, value, this.NextDimension(dimension));
            }
            else
            {
                this.AddToTree(ref node.right, point, value, this.NextDimension(dimension));
            }
        }
        #endregion

        #region ---- Remove ----
        public bool Remove(TPoint point, TValue value)
        {
            if (point == null) { throw new ArgumentNullException(nameof(point)); }

            if (this.Remove(ref this.root, point, value, dimension: 0))
            {
                --this.Count;
                return true;
            }

            return false;
        }

        private bool Remove(ref Node node, TPoint point, TValue value, int dimension)
        {
            if (node == null) { return false; }

            var cmp = this.comparer.Compare(node.point, point, dimension);

            if (cmp < 0)
            {
                return this.Remove(ref node.left, point, value, this.NextDimension(dimension));
            }
            
            if (cmp > 0)
            {
                return this.Remove(ref node.right, point, value, this.NextDimension(dimension));
            }
            
            if (this.PointEquals(node.point, point) && EqualityComparer<TValue>.Default.Equals(node.value, value))
            {
                // found a match!

                if (node.left == null && node.right == null)
                {
                    // for a leaf node, just remove it
                    node = null;
                }
                else
                {
                    // otherwise, we replace with the closest neighbor in the subtree
                    int replacementDimension;
                    var replacementNode = this.FindReplacementChildForRemoval(node, dimension, out replacementDimension);
                    node.point = replacementNode.point;
                    node.value = replacementNode.value;

                    // have to remove the replacement. If it's a leaf we have to re-search for it from here (grr), otherwise
                    // we can just do this operation again since it can only modify the node
                    throw new NotImplementedException();
                }

                return true;
            }

            // in this case, we matched the current dimension exactly but weren't a full match across the other
            // dimensions and/or value. Thus, we recurse on both sides
            var nextDimension = this.NextDimension(dimension);
            return this.Remove(ref node.left, point, value, nextDimension)
                || this.Remove(ref node.right, point, value, nextDimension);
        }

        private Node FindReplacementChildForRemoval(Node toReplace, int toReplaceDimension, out int replacementDimension)
        {
            // todo this currently double-compares. We should instead start best = null and handle that case in left and right
            // find (basically skip the comparison)

            Node best;
            var bestDimension = this.NextDimension(toReplaceDimension);

            if (toReplace.left != null)
            {
                best = toReplace.left;
                this.LeftFindReplacementChildForRemoval(toReplaceDimension, best, bestDimension, ref best, ref bestDimension);
            }
            else
            {
                best = toReplace.right;
                this.RightFindReplacementChildForRemoval(toReplaceDimension, best, bestDimension, ref best, ref bestDimension);
            }

            replacementDimension = bestDimension;
            return best;
        }

        private void LeftFindReplacementChildForRemoval(
            int toReplaceDimension,
            Node current, 
            int currentDimension, 
            ref Node best, 
            ref int bestDimension)
        {
            if (current == null) { return; }

            var cmp = this.comparer.Compare(current.point, best.point, toReplaceDimension);

            if (cmp >= 0)
            {
                // note that we take a new best even in the case where current and best are
                // equal. This is because nodes deeper down the tree are cheaper to recursively replace
                best = current;
                bestDimension = currentDimension;
            }

            // always recurse right
            var nextDimension = this.NextDimension(currentDimension);
            this.LeftFindReplacementChildForRemoval(toReplaceDimension, current.right, nextDimension, ref best, ref bestDimension);

            // recurse left only if we're not at a node on the replacement dimension. If we are at such a node, then
            // anything to the left will be smaller in that dimension and therefore a worse candidate
            if (currentDimension != toReplaceDimension)
            {
                this.LeftFindReplacementChildForRemoval(toReplaceDimension, current.left, nextDimension, ref best, ref bestDimension);
            }
        }

        private void RightFindReplacementChildForRemoval(
            int toReplaceDimension,
            Node current,
            int currentDimension,
            ref Node best,
            ref int bestDimension)
        {
            if (current == null) { return; }

            var cmp = this.comparer.Compare(current.point, best.point, toReplaceDimension);

            if (cmp <= 0)
            {
                // note that we take a new best even in the case where current and best are
                // equal. This is because nodes deeper down the tree are cheaper to recursively replace
                best = current;
                bestDimension = currentDimension;
            }

            // always recurse left
            var nextDimension = this.NextDimension(currentDimension);
            this.RightFindReplacementChildForRemoval(toReplaceDimension, current.left, nextDimension, ref best, ref bestDimension);

            // recurse right only if we're not at a node on the replacement dimension. If we are at such a node, then
            // anything to the right will be smaller in that dimension and therefore a worse candidate
            if (currentDimension != toReplaceDimension)
            {
                this.RightFindReplacementChildForRemoval(toReplaceDimension, current.right, nextDimension, ref best, ref bestDimension);
            }
        }
        #endregion

        #region ---- Range Search ----
        public TAccumulate AggregateRange<TAccumulate>(TPoint lowerBounds, TPoint upperBounds, TAccumulate seed, Func<TAccumulate, KeyValuePair<TPoint, TValue>, TAccumulate> accumulator)
        {
            if (lowerBounds == null) { throw new ArgumentNullException(nameof(lowerBounds)); }
            if (upperBounds == null) { throw new ArgumentNullException(nameof(upperBounds)); }
            if (accumulator == null) { throw new ArgumentNullException(nameof(accumulator)); }

            this.EnsureInitialized(lowerBounds);

            return this.AggregateRange(this.root, lowerBounds, upperBounds, seed, accumulator, dimension: 0);
        }

        private TAccumulate AggregateRange<TAccumulate>(Node node, TPoint lowerBounds, TPoint upperBounds, TAccumulate seed, Func<TAccumulate, KeyValuePair<TPoint, TValue>, TAccumulate> accumulator, int dimension)
        {
            if (node == null) { return seed; }
            
            if (this.comparer.Compare(node.point, lowerBounds, dimension) < 0)
            {
                return this.AggregateRange(node.left, lowerBounds, upperBounds, seed, accumulator, this.NextDimension(dimension));
            }
            
            if (this.comparer.Compare(node.point, upperBounds, dimension) > 0)
            {
                return this.AggregateRange(node.right, lowerBounds, upperBounds, seed, accumulator, this.NextDimension(dimension));
            }

            // at this point, node is within the range. We should aggregate the node and recurse both left and right
            var withNode = accumulator(seed, new KeyValuePair<TPoint, TValue>(node.point, node.value));
            var nextDimension = this.NextDimension(dimension);
            var withNodeAndLeft = this.AggregateRange(node.left, lowerBounds, upperBounds, withNode, accumulator, nextDimension);
            return this.AggregateRange(node.right, lowerBounds, upperBounds, withNodeAndLeft, accumulator, nextDimension);
        }
        #endregion

        // todo rebalance(force)

        private bool PointEquals(TPoint a, TPoint b)
        {
            var dimensions = this.comparer.Dimensions;
            for (var dimension = 0; dimension < dimensions; ++dimension)
            {
                if (this.comparer.Compare(a, b, dimension) != 0) { return false; }
            }
            return true;
        }

        private sealed class Node
        {
            internal TPoint point;
            internal TValue value;
            internal Node left, right;
        }

        private sealed class PointAndValueComparer : IComparer<KeyValuePair<TPoint, TValue>>
        {
            private readonly IKDPointComparer<TPoint> comparer;
            private readonly int dimension;

            public PointAndValueComparer(IKDPointComparer<TPoint> comparer, int dimension)
            {
                this.comparer = comparer;
                this.dimension = dimension;
            }

            int IComparer<KeyValuePair<TPoint, TValue>>.Compare(KeyValuePair<TPoint, TValue> x, KeyValuePair<TPoint, TValue> y)
            {
                return this.comparer.Compare(x.Key, y.Key, dimension);
            }
        }
    }
}
