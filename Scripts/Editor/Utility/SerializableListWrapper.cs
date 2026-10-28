using System;
using System.Collections.Generic;

namespace HSE.CodeCraft
{
    [Serializable]
    public struct SerializableListWrapper<T>
    {
        public List<T> Values;
        public SerializableListWrapper(List<T> values) => Values = values;
        public void Add(T value) => Values.Add(value); 
        public void Remove(T value)
        {
            if(Contains(value))
                Values.Remove(value);
        }
        public bool Contains(T value) => Values.Contains(value);
    }
}
