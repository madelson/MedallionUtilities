using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground.IndexedCollection
{
    public sealed class RandomizedBinaryTree<TKey, TValue> : ICollection<KeyValuePair<TKey, TValue>>, IReadOnlyCollection<KeyValuePair<TKey, TValue>>
    {
        private Node _root;

        public RandomizedBinaryTree(IComparer<TKey> comparer = null)
        {
            this.Comparer = comparer ?? Comparer<TKey>.Default;
        }

        public IComparer<TKey> Comparer { get; }
        public int Count => this._root?.Count ?? 0;
        
        public TValue this[int index]
        {
            get => this.GetNodeAtIndex(index).Value;
            set => this.GetNodeAtIndex(index).Value = value;
        }

        public KeyValuePair<TKey, TValue> Min
        {
            get
            {
                if (this._root == null) { throw new InvalidOperationException("the collection is empty"); }

                var node = this._root;
                while (node.Left != null) { node = node.Left; };
                return new KeyValuePair<TKey, TValue>(node.Key, node.Value);
            }
        }

        public KeyValuePair<TKey, TValue> Max
        {
            get
            {
                if (this._root == null) { throw new InvalidOperationException("the collection is empty"); }

                var node = this._root;
                while (node.Left != null) { node = node.Left; };
                return new KeyValuePair<TKey, TValue>(node.Key, node.Value);
            }
        }

        private Node GetNodeAtIndex(int index)
        {
            if (index < 0 || index >= this.Count) { throw new ArgumentOutOfRangeException(nameof(index), index, "must be non-negative and less than Count"); }

            var node = this._root;
            var adjustedIndex = index;
            while (true)
            {
                var leftCount = node.Left?.Count ?? 0;
                if (adjustedIndex < leftCount) { node = node.Left; }
                else if (adjustedIndex == leftCount) { return node; }
                else
                {
                    adjustedIndex -= (leftCount + 1);
                    node = node.Right;
                }
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            var node = this._root;
            while (node != null)
            {
                var cmp = this.Comparer.Compare(key, node.Key);
                if (cmp < 0) { node = node.Left; }
                else if (cmp > 0) { node = node.Right; }
                else
                {
                    value = node.Value;
                    return true;
                }
            }

            value = default(TValue);
            return false;
        }

        public void Add(TKey key, TValue value) => this.Add(key, value, ref this._root);

        private void Add(TKey key, TValue value, ref Node nodeRef)
        {
            var node = nodeRef;

            if (node == null)
            {
                nodeRef = new Node { Key = key, Value = value, Count = 1 };
                return;
            }

            if (this.Choose(node.Count))
            {
                // split and insert at root
                this.Split(node, key, out var left, out var right);
                nodeRef = new Node { Key = key, Value = value, Left = left, Right = right, Count = Node.ComputeCount(left, right) };
                return;
            }

            ++node.Count;
            var cmp = this.Comparer.Compare(key, node.Key);
            if (cmp < 0) { this.Add(key, value, ref node.Left); }
            else { this.Add(key, value, ref node.Right); }
        }

        /// <summary>
        /// Splits the subtree represented by <paramref name="node"/> into two
        /// trees based on <paramref name="key"/>
        /// </summary>
        private void Split(Node node, TKey key, out Node left, out Node right)
        {
            var cmp = this.Comparer.Compare(key, node.Key);
            if (cmp < 0)
            {
                right = node;
                if (node.Left == null)
                {
                    left = null;
                }
                else
                {
                    node.Count -= node.Left.Count;
                    this.Split(node.Left, key, out left, out node.Left);
                    node.Count += node.Left?.Count ?? 0;
                }
            }
            else
            {
                left = node;
                if (node.Right == null)
                {
                    right = null;
                }
                else
                {
                    node.Count -= node.Right.Count;
                    this.Split(node.Right, key, out node.Right, out right);
                    node.Count += node.Right?.Count ?? 0;
                }
            }
        }

        public bool Remove(TKey key)
        {
            ref var nodeRef = ref this.FindNodeRef(key, ref this._root);
            if (nodeRef == null) { return false; }

            nodeRef = this.Join(nodeRef.Left, nodeRef.Right);
            return true;
        }

        private ref Node FindNodeRef(TKey key, ref Node node)
        {
            if (node == null) { return ref node; }

            var cmp = this.Comparer.Compare(key, node.Key);
            if (cmp < 0)
            {
                ref var result = ref this.FindNodeRef(key, ref node.Left);
                if (result != null) { --node.Count; }
                return ref result;
            }

            if (cmp > 0)
            {
                ref var result = ref this.FindNodeRef(key, ref node.Right);
                if (result != null) { --node.Count; }
                return ref result;
            }

            return ref node;
        }

        private Node Join(Node left, Node right)
        {
            if (left == null) { return right; }
            if (right == null) { return left; }

            if (this.Choose(left.Count, right.Count))
            {
                // left is the new root
                left.Count += right.Count;
                left.Right = this.Join(left.Right, right);
                return left;
            }

            // right is the new root
            right.Count += left.Count;
            right.Left = this.Join(left, right.Left);
            return right;
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

        public void Add(KeyValuePair<TKey, TValue> item) => this.Add(item.Key, item.Value);

        public void Clear() => this._root = null;

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
        {
            return this.TryGetValue(item.Key, out var value) && EqualityComparer<TValue>.Default.Equals(item.Value, value);
        }

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            if (arrayIndex < 0) { throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, "must be non-negative"); }
            if (array.Length - arrayIndex < this.Count) { throw new ArgumentException("insufficient space", nameof(array) + ", " + nameof(arrayIndex)); }

            // todo could do something cleverer here where we simply assign to the right index based on counts

            var i = arrayIndex;
            foreach (var kvp in this)
            {
                array[i++] = kvp;
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            var stack = new Stack<Node>();
            
            void PushLefts(Node node)
            {
                for (var n = node; n != null; n = n.Left)
                {
                    stack.Push(n);
                }
            }

            PushLefts(this._root);

            while (stack.Count > 0)
            {
                var next = stack.Pop();
                yield return new KeyValuePair<TKey, TValue>(next.Key, next.Value);
                PushLefts(next.Right);
            }
        }


        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
        {
            throw new NotImplementedException();
        }

        private sealed class Node
        {
            internal Node Left, Right;
            /// <summary>
            /// # of nodes in the subtree (including this)
            /// </summary>
            internal int Count;
            internal TKey Key;
            internal TValue Value;

            public override string ToString() => $"({this.Key}, {this.Value}) Count = {this.Count}";

            public static int ComputeCount(Node left, Node right) => (left?.Count ?? 0) + (right?.Count ?? 0) + 1;
        }

        internal void CheckInvariants()
        {
            var nodes = new HashSet<Node>();

            void CheckInvariants(Node node, Node parent, bool isNodeLeftChild)
            {
                if (node == null) { return; }
                if (!nodes.Add(node)) { throw new InvalidOperationException($"node {node} encountered multiple times"); }
                
                if (node.Left != null)
                {
                    if (this.Comparer.Compare(node.Key, node.Left.Key) < 0)
                    {
                        throw new InvalidOperationException($"node {node} should be >= left child {node.Left}");
                    }
                    if (parent != null 
                        && !isNodeLeftChild
                        && this.Comparer.Compare(parent.Key, node.Left.Key) > 0)
                    {
                        throw new InvalidOperationException($"left child {node.Left} should be between {parent} and {node}");
                    }
                }
                if (node.Right != null)
                {
                    if (this.Comparer.Compare(node.Key, node.Right.Key) > 0)
                    {
                        throw new InvalidOperationException($"node {node} should be <= right child {node.Right}");
                    }
                    if (parent != null
                        && isNodeLeftChild
                        && this.Comparer.Compare(parent.Key, node.Right.Key) < 0)
                    {
                        throw new InvalidOperationException($"right child {node.Right} should be between {node} and {parent}");
                    }
                }

                CheckInvariants(node.Left, parent: node, isNodeLeftChild: true);
                CheckInvariants(node.Right, parent: node, isNodeLeftChild: false);
                
                if (node.Count != Node.ComputeCount(node.Left, node.Right))
                {
                    throw new InvalidOperationException($"Bad count for {node} based on children {node.Left?.ToString() ?? "null"}, {node.Right?.ToString() ?? "null"}");
                }
            }

            CheckInvariants(this._root, parent: null, isNodeLeftChild: false);
        }

        internal int MaxDepth()
        {
            int MaxDepth(Node node) => node == null
                ? 0
                : 1 + Math.Max(MaxDepth(node.Left), MaxDepth(node.Right));

            return MaxDepth(this._root);
        }

        #region ---- Randomization ----
        private Random rand = new Random(1234); // temporary!

        private bool Choose(int count)
        {
            return rand.NextDouble() < 1.0 / (count + 1);
        }

        private bool Choose(int leftCount, int rightCount)
        {
            return rand.NextDouble() < leftCount / (double)(leftCount + rightCount);
        }
        #endregion
    }
}
