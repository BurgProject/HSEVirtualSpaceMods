using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace HSE.CodeCraft.Editor
{

    public class ListViewBinding
    {
        public readonly SerializedProperty ArraySizeProp;

        private readonly ListView _listView;

        private readonly SerializedProperty _boundProperty;

        public readonly Action<int, object> ItemChangeEvent;
  

        public ListViewBinding(ListView listView, SerializedProperty prop)
        {
            _boundProperty = prop;
            ArraySizeProp = GetArraySize(_boundProperty);
            this._listView = listView;
            listView.makeItem = () => MakeListViewItem();
            listView.BindProperty(_boundProperty);
        }

       private VisualElement MakeListViewItem(){ 
            var propField = new PropertyField();
            return propField;
        }

        private void OnItemChange(SerializedPropertyChangeEvent evt, int index)
        {
            ItemChangeEvent.Invoke(index, evt.changedProperty.boxedValue);
        }

        void BindListViewItem(VisualElement ve, int index)
        {
            var propertyField = (PropertyField)ve;
            var property = _boundProperty.GetArrayElementAtIndex(index);
            propertyField.BindProperty(property);
            //propertyField.RegisterValueChangeCallback((e) => { Debug.Log(e); });
        }
        private SerializedProperty GetArraySize(SerializedProperty parentProperty)
        {
            var property = parentProperty.Copy();
            var endProperty = property.GetEndProperty();
            SerializedProperty arraySize = null;
            property.NextVisible(true); // Expand the first child.
            do
            {
                if (SerializedProperty.EqualContents(property, endProperty))
                    break;

                if (property.propertyType == SerializedPropertyType.ArraySize)
                {
                    arraySize = property.Copy();
                    break;
                }
            }
            while (property.NextVisible(false)); // Never expand children.
            if (arraySize == null)
            {
                throw new ArgumentException("Can't find array size property!");
            }
            return arraySize;
        }
    }
    public class ListViewBindingWhileWaitingForRealBindings
    {
        private readonly SerializedObjectList _dataList;
        private readonly ListView _listView;

        private readonly SerializedObject boundObject;
        private readonly SerializedProperty boundProperty;

        private readonly bool _displayArraySize;
        private readonly string _displayLabelName;

        private int _listViewArraySize;

        public SerializedProperty arraySize { get; private set; }

        public ListViewBindingWhileWaitingForRealBindings(ListView listView, SerializedProperty prop, bool displayArraySize, string displayLabelName = null)
        {
            boundProperty = prop;
            boundObject = prop.serializedObject;
            boundObject.SetIsDifferentCacheDirty();

            _dataList = new SerializedObjectList(prop, displayArraySize);
            arraySize = _dataList.ArraySize;
            _listViewArraySize = _dataList.ArraySize.intValue;

            _displayArraySize = displayArraySize;
            _displayLabelName = displayLabelName;
    
            this._listView = listView;
            if (listView.makeItem == null)
            {
                listView.makeItem = () => MakeListViewItem();
            }
    
            
            if (listView.bindItem == null)
            {
                listView.bindItem = (v, i) => BindListViewItem(v, i);
            }
            listView.itemsSource = _dataList;
           // listView.schedule.Execute(() => Update()).Every(100); //we poll a few times a second
        }
        VisualElement MakeListViewItem() => new PropertyField();

        void BindListViewItem(VisualElement ve, int index)
        {
            var field = ve as IBindable;
            
            if (field == null)
            {
                //we find the first Bindable
                field = ve.Query().Where(x => x is IBindable).First() as IBindable;
            }

            if (field == null)
            {
                //can't default bind to anything!
                throw new InvalidOperationException("Can't find BindableElement: please provide BindableVisualElements or provide your own Listview.bindItem callback");
            }
         
            object item = _listView.itemsSource[index];
            var itemProp = item as SerializedProperty;
            field.bindingPath = itemProp.propertyPath;
            ve.Bind(itemProp.serializedObject);
        }

        void UpdateArraySize()
        {
            _dataList.RefreshProperties(boundProperty, _displayArraySize);

            arraySize = _dataList.ArraySize;
            _listViewArraySize = arraySize.intValue;
            _listView.Rebuild();
        }


        public void Update()
        {
            try
            {
                if (boundObject != null && boundProperty != null)
                {
                    boundObject.UpdateIfRequiredOrScript();

                    int currentArraySize = arraySize.intValue;

                    if (currentArraySize != _listViewArraySize)
                    {
                        UpdateArraySize();
                    }
                }
            }
            catch (ArgumentNullException)
            {
                //this can happen when serializedObject has been disposed of
            }
        }
    }
}
