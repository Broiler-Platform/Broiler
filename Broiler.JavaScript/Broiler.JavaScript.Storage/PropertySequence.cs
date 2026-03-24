using System;
using System.Runtime.CompilerServices;
using Broiler.JavaScript.Ast.Misc;

namespace Broiler.JavaScript.Storage;

public struct JSObjectProperty
{
    public JSProperty Property;
    public uint Next;

    public static JSObjectProperty Empty;
}

public delegate void Updater<TKey, TValue>(TKey key, ref TValue value);

public struct PropertySequence
{
    public readonly PropertyEnumerator GetEnumerator(bool showEnumerableOnly = true) => new(this, showEnumerableOnly);

    public struct PropertyEnumerator(PropertySequence sequence, bool showEnumerableOnly)
    {
        private SAUint32Map<JSObjectProperty> map = sequence.map;
        private readonly uint tail = sequence.tail;
        private uint start = sequence.head;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext(out JSProperty property)
        {
            while (start > 0)
            {
                ref var objP = ref map.GetRefOrDefault(start, ref JSObjectProperty.Empty);
                ref var p = ref objP.Property;

                if (p.IsEmpty)
                {
                    start = objP.Next;
                    continue;
                }

                if (showEnumerableOnly)
                {
                    if (!p.IsEnumerable)
                    {
                        start = objP.Next;
                        continue;
                    }
                }

                property = p;
                start = objP.Next;
                return true;
            }

            property = JSProperty.Empty;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext(out KeyString key, out JSProperty property)
        {
            while (start > 0)
            {
                ref var objP = ref map.GetRefOrDefault(start, ref JSObjectProperty.Empty);
                ref var p = ref objP.Property;
                if (p.IsEmpty)
                {
                    start = objP.Next;
                    continue;
                }
                if (showEnumerableOnly)
                {
                    if (!p.IsEnumerable)
                    {
                        start = objP.Next;
                        continue;
                    }
                }
                property = p;
                key = KeyStrings.GetName(start);
                start = objP.Next;
                return true;
            }

            property = JSProperty.Empty;
            key = KeyString.Empty;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNextKey(out KeyString key)
        {
            while (start > 0)
            {
                ref var objP = ref map.GetRefOrDefault(start, ref JSObjectProperty.Empty);
                ref var p = ref objP.Property;
                if (p.IsEmpty)
                {
                    start = objP.Next;
                    continue;
                }
                if (showEnumerableOnly)
                {
                    if (!p.IsEnumerable)
                    {
                        start = objP.Next;
                        continue;
                    }
                }
                key = KeyStrings.GetName(start);
                start = objP.Next;
                return true;
            }
            key = KeyString.Empty;
            return false;
        }
    }

    /// <summary>
    /// Static delegate factory for creating type errors when property deletion
    /// is attempted on read-only or non-configurable properties. Set by the
    /// Core assembly during initialization to produce the correct JavaScript
    /// TypeError exception. If not set, falls back to InvalidOperationException.
    /// </summary>
    public static Func<string, Exception>? TypeErrorFactory { get; set; }

    private SAUint32Map<JSObjectProperty> map;
    private uint head;
    private uint tail;

    public readonly bool IsEmpty => head == 0;

    /// <summary>
    /// Returns a reference to the internal map for use by enumerators
    /// that need direct access to the property storage.
    /// </summary>
    public readonly ref SAUint32Map<JSObjectProperty> GetMap() => ref Unsafe.AsRef(in map);

    /// <summary>
    /// Returns the head index for use by enumerators.
    /// </summary>
    public readonly uint Head => head;

    public void Update(Updater<uint, JSProperty> func)
    {
        var start = head;

        while (start > 0)
        {
            ref var objP = ref map.GetRefOrDefault(start, ref JSObjectProperty.Empty);
            ref var p = ref objP.Property;

            if (p.IsEmpty)
            {
                start = objP.Next;
                continue;
            }

            func(start, ref p);
            start = objP.Next;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasKey(uint key) => map.HasKey(key);

    public bool RemoveAt(uint key)
    {
        ref var objectProperty = ref map.GetRefOrDefault(key, ref JSObjectProperty.Empty);
        ref var property = ref objectProperty.Property;

        if (property.IsEmpty)
            return false;

        if (property.IsReadOnly || !property.IsConfigurable)
        {
            var factory = TypeErrorFactory;
            if (factory != null)
                throw factory($"Cannot delete property {KeyStrings.GetNameString(key)} of {this}");
            throw new InvalidOperationException($"Cannot delete property {KeyStrings.GetNameString(key)} of {this}");
        }

        property = JSProperty.Empty;

        return true;
    }

    public ref JSProperty GetValue(uint key)
    {
        ref var objectProperty = ref map.GetRefOrDefault(key, ref JSObjectProperty.Empty);
        ref var property = ref objectProperty.Property;

        if (property.IsEmpty)
            return ref JSProperty.Empty;

        return ref property;
    }

    public bool TryGetValue(uint key, out JSProperty obj)
    {
        ref var objectProperty = ref map.GetRefOrDefault(key, ref JSObjectProperty.Empty);
        ref var property = ref objectProperty.Property;

        if (property.IsEmpty)
        {
            obj = JSProperty.Empty;
            return false;
        }

        obj = property;
        return true;
    }

    public void Put(uint key, IPropertyValue value, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableValue) => Put(key) = JSProperty.Property(key, value, attributes);

    public void Put(in KeyString key, IPropertyValue value, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableValue) => Put(key.Key) = JSProperty.Property(key, value, attributes);

    public void Put(in KeyString key, IPropertyAccessor getter, IPropertyAccessor setter, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableProperty) => Put(key.Key) = JSProperty.Property(key, getter, setter, attributes);

    public ref JSProperty Put(uint key)
    {
        if (head == 0)
        {
            tail = head = key;
            ref var objP = ref map.Put(key);
            return ref objP.Property;
        }

        ref var @new = ref map.Put(key);

        // when tail is same as key, it means last key was added twice..
        // it should not create a loop
        if (@new.Next == 0 && tail != key)
        {
            ref var last = ref map.GetRefOrDefault(tail, ref JSObjectProperty.Empty);
            last.Next = key;
            tail = key;
        }

        return ref @new.Property;
    }
}
