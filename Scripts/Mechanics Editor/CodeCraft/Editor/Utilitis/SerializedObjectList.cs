using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HSE.CodeCraft.Editor
{
    internal class SerializedObjectList : IList
    {
        public SerializedProperty ArraySize { get; private set; }


        public Action OnAdd;
        public Action OnRemove;

        List<SerializedProperty> properties;
        private readonly SerializedObject _serializedObject;
        public SerializedObjectList(SerializedProperty parentProperty, bool includeArraySize)
        {
            RefreshProperties(parentProperty, includeArraySize);
            _serializedObject = parentProperty.serializedObject;
        }

        public void RefreshProperties(SerializedProperty parentProperty, bool includeArraySize)
        {
            var property = parentProperty.Copy();
            var endProperty = property.GetEndProperty();

            property.NextVisible(true); // Expand the first child.

            properties = new List<SerializedProperty>();
            do
            {
                if (SerializedProperty.EqualContents(property, endProperty))
                    break;

                if (property.propertyType == SerializedPropertyType.ArraySize)
                {
                    ArraySize = property.Copy();
                    if (includeArraySize)
                    {
                        properties.Add(ArraySize);
                    }
                }
                else
                {
                    properties.Add(property.Copy());
                }
            }
            while (property.NextVisible(false)); // Never expand children.

            if (ArraySize == null)
            {
                throw new ArgumentException("Can't find array size property!");
            }
        }

        public object this[int index]
        {
            get
            {
                return properties[index];
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public bool IsReadOnly => true;

        public bool IsFixedSize => false;

        public int Count => properties.Count;

        bool ICollection.IsSynchronized
        {
            get { return (properties as ICollection).IsSynchronized; }
        }

        object ICollection.SyncRoot
        {
            get { return (properties as ICollection).SyncRoot; }
        }

        public int Add(object value)
        {
            ArraySize.intValue++;
            _serializedObject.ApplyModifiedProperties();
            return ArraySize.intValue;
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(object value)
        {
            return IndexOf(value) >= 0;
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public IEnumerator GetEnumerator()
        {
            return properties.GetEnumerator();
        }

        public int IndexOf(object value)
        {
            var prop = value as SerializedProperty;

            if (value != null && prop != null)
            {
                return properties.IndexOf(prop);
            }
            return -1;
        }

        public void Insert(int index, object value)
        {
            throw new NotImplementedException();
        }

        public void Remove(object value)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            properties.RemoveAt(index);
           
            ArraySize.intValue = Count;
            _serializedObject.ApplyModifiedProperties();
        }
    }
}
