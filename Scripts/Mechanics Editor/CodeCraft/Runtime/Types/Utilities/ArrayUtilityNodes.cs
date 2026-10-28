using System;
using System.Collections.Generic;
using GraphProcessor;
using UnityEngine;

namespace HSE.CodeCraft
{
    public enum ArrayOperation { Copy, Append, Insert, RemoveAt, ReplaceAt, Concat, Slice, Reverse, Distinct, GetAt }

    public abstract class ArrayValuesNode<T> : BaseHSENode
    {
        public T[] Values = System.Array.Empty<T>();
        [Output(allowMultiple = true), NonSerialized] public T[] Array = System.Array.Empty<T>();

        protected override bool Process()
        {
            Array = Values == null ? System.Array.Empty<T>() : (T[])Values.Clone();
            return true;
        }
    }

    /// <summary>Вычисляет новый массив при обработке редакторского графа, не изменяя входной контейнер.</summary>
    public abstract class ArrayUtilityNode<T> : BaseHSENode
    {
        public ArrayOperation Operation;
        [Input, NonSerialized] public T[] Source;
        [Input, NonSerialized] public T[] Other;
        [Input, ShowAsDrawer] public T Item;
        [Input, ShowAsDrawer] public int Index;
        [Input, ShowAsDrawer] public int Count;
        [Output(allowMultiple = true), NonSerialized] public T[] Result = Array.Empty<T>();
        [Output(allowMultiple = true), NonSerialized] public T Element;
        [Output(allowMultiple = true), NonSerialized] public int Length;

        protected override bool Process()
        {
            ClearMessages();
            Result = Array.Empty<T>();
            Element = default;
            Length = 0;
            if (GetPort(nameof(Source), null)?.GetEdges().Count != 1 || Source == null)
                return Fail("Connect an array to Source.");

            int length = Source.Length;
            switch (Operation)
            {
                case ArrayOperation.Copy:
                    Result = (T[])Source.Clone();
                    break;
                case ArrayOperation.Append:
                    Insert(length);
                    break;
                case ArrayOperation.Insert:
                    if (Index < 0 || Index > length) return Fail("Insert index must be between zero and array length.");
                    Insert(Index);
                    break;
                case ArrayOperation.RemoveAt:
                    if (!ValidIndex(length)) return false;
                    Result = new T[length - 1];
                    Array.Copy(Source, Result, Index);
                    Array.Copy(Source, Index + 1, Result, Index, length - Index - 1);
                    break;
                case ArrayOperation.ReplaceAt:
                    if (!ValidIndex(length)) return false;
                    Result = (T[])Source.Clone();
                    Result[Index] = Item;
                    break;
                case ArrayOperation.Concat:
                    if (GetPort(nameof(Other), null)?.GetEdges().Count != 1 || Other == null)
                        return Fail("Connect an array to Other.");
                    Result = new T[length + Other.Length];
                    Array.Copy(Source, Result, length);
                    Array.Copy(Other, 0, Result, length, Other.Length);
                    break;
                case ArrayOperation.Slice:
                    if (Index < 0 || Index > length || Count < 0 || Count > length - Index)
                        return Fail("Slice must fit inside the array; Count may be zero.");
                    Result = new T[Count];
                    Array.Copy(Source, Index, Result, 0, Count);
                    break;
                case ArrayOperation.Reverse:
                    Result = (T[])Source.Clone();
                    Array.Reverse(Result);
                    break;
                case ArrayOperation.Distinct:
                    var unique = new HashSet<T>();
                    var values = new List<T>(length);
                    foreach (var value in Source)
                        if (unique.Add(value)) values.Add(value);
                    Result = values.ToArray();
                    break;
                case ArrayOperation.GetAt:
                    if (!ValidIndex(length)) return false;
                    Element = Source[Index];
                    Result = (T[])Source.Clone();
                    break;
                default:
                    return Fail("Unsupported array operation.");
            }
            Length = Result.Length;
            return true;
        }

        private void Insert(int index)
        {
            Result = new T[Source.Length + 1];
            Array.Copy(Source, Result, index);
            Result[index] = Item;
            Array.Copy(Source, index, Result, index + 1, Source.Length - index);
        }

        private bool ValidIndex(int length) => Index >= 0 && Index < length || Fail("Index is outside the array.");

        private bool Fail(string message)
        {
            AddMessage(message, NodeMessageType.Error);
            return false;
        }
    }

    [Serializable, NodeMenuItem("Containers/Arrays/Vector3")]
    public sealed class Vector3ArrayNode : ArrayValuesNode<Vector3> { }
    [Serializable, NodeMenuItem("Containers/Arrays/Float")]
    public sealed class FloatArrayNode : ArrayValuesNode<float> { }
    [Serializable, NodeMenuItem("Containers/Arrays/Int")]
    public sealed class IntArrayNode : ArrayValuesNode<int> { }
    [Serializable, NodeMenuItem("Containers/Arrays/GameObject")]
    public sealed class GameObjectArrayNode : ArrayValuesNode<GameObject> { }
    [Serializable, NodeMenuItem("Containers/Arrays/Texture2D")]
    public sealed class TextureArrayNode : ArrayValuesNode<Texture2D> { }
    [Serializable, NodeMenuItem("Containers/Arrays/Material")]
    public sealed class MaterialArrayNode : ArrayValuesNode<Material> { }

    [Serializable]
    public sealed class Vector3ArrayUtilityNode : ArrayUtilityNode<Vector3> { }
    [Serializable]
    public sealed class FloatArrayUtilityNode : ArrayUtilityNode<float> { }
    [Serializable]
    public sealed class IntArrayUtilityNode : ArrayUtilityNode<int> { }
    [Serializable]
    public sealed class GameObjectArrayUtilityNode : ArrayUtilityNode<GameObject> { }
    [Serializable]
    public sealed class TextureArrayUtilityNode : ArrayUtilityNode<Texture2D> { }
    [Serializable]
    public sealed class MaterialArrayUtilityNode : ArrayUtilityNode<Material> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/GameObject Positions")]
    public sealed class GameObjectPositionsNode : BaseHSENode
    {
        [Input, NonSerialized] public GameObject[] Objects;
        [Input, ShowAsDrawer] public Vector3 Offset;
        [Output(allowMultiple = true), NonSerialized] public Vector3[] Positions = Array.Empty<Vector3>();

        protected override bool Process()
        {
            ClearMessages();
            Positions = Array.Empty<Vector3>();
            if (GetPort(nameof(Objects), null)?.GetEdges().Count != 1 || Objects == null)
            {
                AddMessage("Connect a GameObject array.", NodeMessageType.Error);
                return false;
            }
            var result = new Vector3[Objects.Length];
            for (int i = 0; i < result.Length; i++)
            {
                if (Objects[i] == null)
                {
                    AddMessage("Array contains a missing GameObject at index " + i + ".", NodeMessageType.Error);
                    return false;
                }
                result[i] = Objects[i].transform.position + Offset;
            }
            Positions = result;
            return true;
        }
    }
}
