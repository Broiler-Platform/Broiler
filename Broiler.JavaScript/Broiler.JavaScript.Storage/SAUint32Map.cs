using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Storage;


public struct SAUint32Map<T>
{
    public const int NodeBlock = 4;

    [DebuggerDisplay("{Key}: {Value}")]
    public struct KeyValue
    {
        public uint Key;
        public T Value;
    }

    enum NodeState : byte
    {
        Empty = 0,
        Filled = 1,
        HasValue = 4
    }

    static Node Empty = new();

    [DebuggerDisplay("{Key}={Value}")]
    struct Node
    {
        public readonly bool HasValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (State & NodeState.HasValue) > 0;
        }

        /// <summary>
        /// Current key
        /// </summary>
        public uint Key;
        public NodeState State;
        /// <summary>
        /// Current value
        /// </summary>
        public T Value;
        /// <summary>
        /// Index of First Child.
        /// All children must be allocated
        /// in advance.
        /// </summary>
        public VirtualArray Children;
    }

    private VirtualMemory<Node> nodes;

    // first set of roots
    private VirtualArray roots;

    public T this[uint index]
    {
        get
        {
            ref var node = ref GetNode(index);
            return node.HasValue ? node.Value : default;
        }
    }

    public readonly bool IsNull => nodes.IsEmpty;


    public IEnumerable<KeyValue> All
    {
        get
        {
            foreach (var (k, v) in AllValues())
                yield return new KeyValue { Key = k, Value = v };
        }
    }

    public readonly IEnumerable<(uint Key, T Value)> AllValues()
    {
        if (nodes.IsEmpty)
            yield break;

        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes.GetAt(i);
            if (node.HasValue)
                yield return (node.Key, node.Value);
        }
    }

    public bool HasKey(uint key)
    {
        ref var node = ref GetNode(key);
        return node.HasValue;
    }

    public bool TryGetValue(uint key, out T value)
    {
        ref var node = ref GetNode(key);
        if (node.HasValue)
        {
            value = node.Value;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryRemove(uint key, out T value)
    {
        ref var node = ref GetNode(key);
        if (node.HasValue)
        {
            value = node.Value;
            node.Value = default;
            node.State = NodeState.Filled;
            return true;
        }

        value = default;
        return false;
    }

    public void Save(uint key, T value)
    {
        ref var node = ref GetNode(key, true);
        node.Value = value;
        node.State |= NodeState.HasValue;
    }

    public ref T Put(uint key)
    {
        ref var node = ref GetNode(key, true);
        node.State |= NodeState.HasValue;
        return ref node.Value;
    }

    public ref T GetRefOrDefault(uint key, ref T def) 
    {
        ref var node = ref GetNode(key);
        if (node.HasValue)
            return ref node.Value;

        return ref def;
    }

    public bool RemoveAt(uint key)
    {
        ref var node = ref GetNode(key);
        if (node.HasValue)
        {
            node.State = NodeState.Filled;
            node.Value = default;
            return true;
        }

        return false;
    }


    private ref Node GetNode(uint originalKey, bool create = false)
    {
        ref var node = ref Empty;

        if (roots.IsEmpty) 
        { 
            if (!create) 
                return ref Empty;

            // extend...
            roots = nodes.Allocate(4);
            nodes[roots, 0].State = NodeState.Filled;
        }

        if (originalKey == 0)
        {
            node = ref nodes[roots, 0];
            return ref node;
        }

        var leaves = roots;

        // let us walk the nodes...
        for (long key = originalKey; key > 0; key >>= 2)
        {
            var index = (int)(key & 0x3);
            node = ref nodes[leaves, index];
            if (node.Key == originalKey) 
            {
                if (create)
                {
                    if (node.State == NodeState.Empty)
                        node.State = NodeState.Filled;
                }

                return ref node;
            }

            if (create)
            {
                if (node.State == NodeState.Empty)
                {
                    // lets occupy current node.
                    node.State = NodeState.Filled;
                    node.Key = originalKey;
                    return ref node;
                }

                if (node.Key > originalKey)
                {
                    // need to make this non recursive...
                    var oldKey = node.Key;
                    var oldValue = node.Value;
                    // var oldChild = node.Children;
                    node.Key = originalKey;
                    node.State = NodeState.Filled;
                    node.Value = default;
                    ref var newChild = ref GetNode(oldKey, true);
                    newChild.Key = oldKey;
                    newChild.Value = oldValue;
                    // var newChildren = newChild.Children;
                    // newChild.Children = oldChild;
                    newChild.State |= NodeState.HasValue;
                    // this is case when array is resized
                    // and we still might have reference to old node
                    node = ref nodes[leaves, index];
                    // node.Children = newChildren;
                    return ref node;
                }

                node.State |= NodeState.Filled;
                if (node.Children.IsEmpty)
                {
                    var c = nodes.Allocate(4);
                    // allocation may have moved node
                    node = ref nodes[leaves, index];
                    node.Children = c;
                }
            }

            var next = node.Children;
            if (next.IsEmpty)
                return ref Empty;

            leaves = next;
        }

        if (node.Key == originalKey)
            return ref node;

        return ref Empty;
    }

    public void Resize(int size)
    {
        if (size < 0)
            return;

        // right align to 4 bits..
        size = ((size / 4)+1)*4;
        nodes.SetCapacity(size);
    }
}
