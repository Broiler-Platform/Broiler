using Broiler.JavaScript.Core.Extensions;
using System.Runtime.CompilerServices;
using Broiler.JavaScript.Core.Core.Function;

namespace Broiler.JavaScript.Core.Core.Storage;

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

    #region ValueEnumerator
    public struct ValueEnumerator
    {
        public JSObject target;
        private SAUint32Map<JSObjectProperty> map;
        private uint start;
        readonly bool showEnumerableOnly;

        public ValueEnumerator(JSObject target, bool showEnumerableOnly)
        {
            this.showEnumerableOnly = showEnumerableOnly;
            this.target = target;
            ref var properties = ref target.GetOwnProperties();
            map = properties.map;
            start = properties.head;
        }

        public bool MoveNext(out KeyString key)
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

                if (showEnumerableOnly && !p.IsEnumerable)
                {
                    start = objP.Next;
                    continue;
                }

                key = KeyStrings.GetName(start);
                start = objP.Next;
                return true;
            }

            key = KeyString.Empty;
            return false;
        }

        public bool MoveNext(out JSValue value, out KeyString key)
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

                if (showEnumerableOnly && !p.IsEnumerable)
                {
                    start = objP.Next;
                    continue;
                }

                value = target.GetValue(in p);
                key = KeyStrings.GetName(start);
                start = objP.Next;
                return true;
            }

            value = null;
            key = KeyString.Empty;
            return false;
        }

        public bool MoveNextProperty(out JSProperty value, out KeyString key)
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

                if (showEnumerableOnly && !p.IsEnumerable)
                {
                    start = objP.Next;
                    continue;
                }

                value = p;
                key = KeyStrings.GetName(start);
                start = objP.Next;
                return true;
            }

            value = JSProperty.Empty;
            key = KeyString.Empty;
            return false;
        }

    }
    #endregion


    private SAUint32Map<JSObjectProperty> map;
    private uint head;
    private uint tail;

    public readonly bool IsEmpty => head == 0;

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
            throw JSContext.NewTypeError($"Cannot delete property {KeyStrings.GetNameString(key)} of {this}");

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

    public void Put(uint key, JSValue value, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableValue) => Put(key) = JSProperty.Property(key, value, attributes);

    public void Put(in KeyString key, JSValue value, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableValue) => Put(key.Key) = JSProperty.Property(key, value, attributes);

    public void Put(in KeyString key, JSFunction getter, JSFunction setter, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableProperty) => Put(key.Key) = JSProperty.Property(key, getter, setter, attributes);

    public void Put(in KeyString key, JSFunctionDelegate getter, JSFunctionDelegate setter, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableProperty) => Put(key.Key) = JSPropertyFactory.Property(key, getter, setter, attributes);

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
