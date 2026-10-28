using System;
using System.Collections.Generic;
using GraphProcessor;
using UnityEngine;

namespace HSE.CodeCraft
{
    public abstract class ArrayInputNode<T> : BaseHSENode
    {
        [Input, NonSerialized] public T[] Source;

        protected bool ValidateSource() =>
            GetPort(nameof(Source), null)?.GetEdges().Count == 1 && Source != null || Fail("Connect an array to Source.");

        protected bool ValidateIndex(int index) =>
            index >= 0 && index < Source.Length || Fail("Index is outside the array.");

        protected bool Fail(string message)
        {
            AddMessage(message, NodeMessageType.Error);
            return false;
        }
    }

    public abstract class ArrayTransformNode<T> : ArrayInputNode<T>
    {
        [Output(allowMultiple = true), NonSerialized] public T[] Result = Array.Empty<T>();

        protected sealed override bool Process()
        {
            ClearMessages();
            Result = Array.Empty<T>();
            return ValidateSource() && Transform();
        }

        protected abstract bool Transform();
    }

    public abstract class ArrayCopyNode<T> : ArrayTransformNode<T>
    {
        protected override bool Transform()
        {
            Result = (T[])Source.Clone();
            return true;
        }
    }

    public abstract class ArrayConcatNode<T> : ArrayTransformNode<T>
    {
        [Input, NonSerialized] public T[] Other;

        protected override bool Transform()
        {
            if (GetPort(nameof(Other), null)?.GetEdges().Count != 1 || Other == null)
                return Fail("Connect the second array to Other.");

            Result = new T[Source.Length + Other.Length];
            Array.Copy(Source, Result, Source.Length);
            Array.Copy(Other, 0, Result, Source.Length, Other.Length);
            return true;
        }
    }

    public abstract class ArrayAppendNode<T> : ArrayTransformNode<T>
    {
        [Input, ShowAsDrawer] public T Item;

        protected override bool Transform()
        {
            Result = new T[Source.Length + 1];
            Array.Copy(Source, Result, Source.Length);
            Result[Source.Length] = Item;
            return true;
        }
    }

    public abstract class ArrayInsertNode<T> : ArrayTransformNode<T>
    {
        [Input, ShowAsDrawer] public T Item;
        [Input, ShowAsDrawer] public int Index;

        protected override bool Transform()
        {
            if (Index < 0 || Index > Source.Length) return Fail("Insert index must be between zero and array length.");

            Result = new T[Source.Length + 1];
            Array.Copy(Source, Result, Index);
            Result[Index] = Item;
            Array.Copy(Source, Index, Result, Index + 1, Source.Length - Index);
            return true;
        }
    }

    public abstract class ArrayRemoveAtNode<T> : ArrayTransformNode<T>
    {
        [Input, ShowAsDrawer] public int Index;

        protected override bool Transform()
        {
            if (!ValidateIndex(Index)) return false;

            Result = new T[Source.Length - 1];
            Array.Copy(Source, Result, Index);
            Array.Copy(Source, Index + 1, Result, Index, Source.Length - Index - 1);
            return true;
        }
    }

    public abstract class ArrayReplaceAtNode<T> : ArrayTransformNode<T>
    {
        [Input, ShowAsDrawer] public T Item;
        [Input, ShowAsDrawer] public int Index;

        protected override bool Transform()
        {
            if (!ValidateIndex(Index)) return false;

            Result = (T[])Source.Clone();
            Result[Index] = Item;
            return true;
        }
    }

    public abstract class ArraySliceNode<T> : ArrayTransformNode<T>
    {
        [Input, ShowAsDrawer] public int Index;
        [Input, ShowAsDrawer] public int Count;

        protected override bool Transform()
        {
            if (Index < 0 || Index > Source.Length || Count < 0 || Count > Source.Length - Index)
                return Fail("Slice must fit inside the array; Count may be zero.");

            Result = new T[Count];
            Array.Copy(Source, Index, Result, 0, Count);
            return true;
        }
    }

    public abstract class ArrayReverseNode<T> : ArrayTransformNode<T>
    {
        protected override bool Transform()
        {
            Result = (T[])Source.Clone();
            Array.Reverse(Result);
            return true;
        }
    }

    public abstract class ArrayDistinctNode<T> : ArrayTransformNode<T>
    {
        protected override bool Transform()
        {
            var seen = new HashSet<T>();
            var values = new List<T>(Source.Length);
            foreach (var item in Source)
                if (seen.Add(item)) values.Add(item);
            Result = values.ToArray();
            return true;
        }
    }

    public abstract class ArrayGetAtNode<T> : ArrayInputNode<T>
    {
        [Input, ShowAsDrawer] public int Index;
        [Output(allowMultiple = true), NonSerialized] public T Element;

        protected override bool Process()
        {
            ClearMessages();
            Element = default;
            if (!ValidateSource() || !ValidateIndex(Index)) return false;
            Element = Source[Index];
            return true;
        }
    }

    public abstract class ArrayLengthNode<T> : ArrayInputNode<T>
    {
        [Output(allowMultiple = true), NonSerialized] public int Length;

        protected override bool Process()
        {
            ClearMessages();
            Length = 0;
            if (!ValidateSource()) return false;
            Length = Source.Length;
            return true;
        }
    }

    [Serializable, NodeMenuItem("Utilities/Arrays/Vector3/Copy")]
    public sealed class Vector3ArrayCopyNode : ArrayCopyNode<Vector3> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Vector3/Concat")]
    public sealed class Vector3ArrayConcatNode : ArrayConcatNode<Vector3> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Vector3/Append")]
    public sealed class Vector3ArrayAppendNode : ArrayAppendNode<Vector3> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Vector3/Insert")]
    public sealed class Vector3ArrayInsertNode : ArrayInsertNode<Vector3> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Vector3/Remove At")]
    public sealed class Vector3ArrayRemoveAtNode : ArrayRemoveAtNode<Vector3> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Vector3/Replace At")]
    public sealed class Vector3ArrayReplaceAtNode : ArrayReplaceAtNode<Vector3> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Vector3/Slice")]
    public sealed class Vector3ArraySliceNode : ArraySliceNode<Vector3> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Vector3/Reverse")]
    public sealed class Vector3ArrayReverseNode : ArrayReverseNode<Vector3> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Vector3/Distinct")]
    public sealed class Vector3ArrayDistinctNode : ArrayDistinctNode<Vector3> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Vector3/Get At")]
    public sealed class Vector3ArrayGetAtNode : ArrayGetAtNode<Vector3> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Vector3/Length")]
    public sealed class Vector3ArrayLengthNode : ArrayLengthNode<Vector3> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Float/Copy")]
    public sealed class FloatArrayCopyNode : ArrayCopyNode<float> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Float/Concat")]
    public sealed class FloatArrayConcatNode : ArrayConcatNode<float> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Float/Append")]
    public sealed class FloatArrayAppendNode : ArrayAppendNode<float> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Float/Insert")]
    public sealed class FloatArrayInsertNode : ArrayInsertNode<float> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Float/Remove At")]
    public sealed class FloatArrayRemoveAtNode : ArrayRemoveAtNode<float> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Float/Replace At")]
    public sealed class FloatArrayReplaceAtNode : ArrayReplaceAtNode<float> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Float/Slice")]
    public sealed class FloatArraySliceNode : ArraySliceNode<float> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Float/Reverse")]
    public sealed class FloatArrayReverseNode : ArrayReverseNode<float> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Float/Distinct")]
    public sealed class FloatArrayDistinctNode : ArrayDistinctNode<float> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Float/Get At")]
    public sealed class FloatArrayGetAtNode : ArrayGetAtNode<float> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Float/Length")]
    public sealed class FloatArrayLengthNode : ArrayLengthNode<float> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Int/Copy")]
    public sealed class IntArrayCopyNode : ArrayCopyNode<int> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Int/Concat")]
    public sealed class IntArrayConcatNode : ArrayConcatNode<int> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Int/Append")]
    public sealed class IntArrayAppendNode : ArrayAppendNode<int> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Int/Insert")]
    public sealed class IntArrayInsertNode : ArrayInsertNode<int> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Int/Remove At")]
    public sealed class IntArrayRemoveAtNode : ArrayRemoveAtNode<int> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Int/Replace At")]
    public sealed class IntArrayReplaceAtNode : ArrayReplaceAtNode<int> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Int/Slice")]
    public sealed class IntArraySliceNode : ArraySliceNode<int> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Int/Reverse")]
    public sealed class IntArrayReverseNode : ArrayReverseNode<int> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Int/Distinct")]
    public sealed class IntArrayDistinctNode : ArrayDistinctNode<int> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Int/Get At")]
    public sealed class IntArrayGetAtNode : ArrayGetAtNode<int> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Int/Length")]
    public sealed class IntArrayLengthNode : ArrayLengthNode<int> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/GameObject/Copy")]
    public sealed class GameObjectArrayCopyNode : ArrayCopyNode<GameObject> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/GameObject/Concat")]
    public sealed class GameObjectArrayConcatNode : ArrayConcatNode<GameObject> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/GameObject/Append")]
    public sealed class GameObjectArrayAppendNode : ArrayAppendNode<GameObject> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/GameObject/Insert")]
    public sealed class GameObjectArrayInsertNode : ArrayInsertNode<GameObject> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/GameObject/Remove At")]
    public sealed class GameObjectArrayRemoveAtNode : ArrayRemoveAtNode<GameObject> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/GameObject/Replace At")]
    public sealed class GameObjectArrayReplaceAtNode : ArrayReplaceAtNode<GameObject> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/GameObject/Slice")]
    public sealed class GameObjectArraySliceNode : ArraySliceNode<GameObject> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/GameObject/Reverse")]
    public sealed class GameObjectArrayReverseNode : ArrayReverseNode<GameObject> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/GameObject/Distinct")]
    public sealed class GameObjectArrayDistinctNode : ArrayDistinctNode<GameObject> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/GameObject/Get At")]
    public sealed class GameObjectArrayGetAtNode : ArrayGetAtNode<GameObject> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/GameObject/Length")]
    public sealed class GameObjectArrayLengthNode : ArrayLengthNode<GameObject> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Texture2D/Copy")]
    public sealed class TextureArrayCopyNode : ArrayCopyNode<Texture2D> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Texture2D/Concat")]
    public sealed class TextureArrayConcatNode : ArrayConcatNode<Texture2D> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Texture2D/Append")]
    public sealed class TextureArrayAppendNode : ArrayAppendNode<Texture2D> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Texture2D/Insert")]
    public sealed class TextureArrayInsertNode : ArrayInsertNode<Texture2D> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Texture2D/Remove At")]
    public sealed class TextureArrayRemoveAtNode : ArrayRemoveAtNode<Texture2D> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Texture2D/Replace At")]
    public sealed class TextureArrayReplaceAtNode : ArrayReplaceAtNode<Texture2D> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Texture2D/Slice")]
    public sealed class TextureArraySliceNode : ArraySliceNode<Texture2D> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Texture2D/Reverse")]
    public sealed class TextureArrayReverseNode : ArrayReverseNode<Texture2D> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Texture2D/Distinct")]
    public sealed class TextureArrayDistinctNode : ArrayDistinctNode<Texture2D> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Texture2D/Get At")]
    public sealed class TextureArrayGetAtNode : ArrayGetAtNode<Texture2D> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Texture2D/Length")]
    public sealed class TextureArrayLengthNode : ArrayLengthNode<Texture2D> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Material/Copy")]
    public sealed class MaterialArrayCopyNode : ArrayCopyNode<Material> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Material/Concat")]
    public sealed class MaterialArrayConcatNode : ArrayConcatNode<Material> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Material/Append")]
    public sealed class MaterialArrayAppendNode : ArrayAppendNode<Material> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Material/Insert")]
    public sealed class MaterialArrayInsertNode : ArrayInsertNode<Material> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Material/Remove At")]
    public sealed class MaterialArrayRemoveAtNode : ArrayRemoveAtNode<Material> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Material/Replace At")]
    public sealed class MaterialArrayReplaceAtNode : ArrayReplaceAtNode<Material> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Material/Slice")]
    public sealed class MaterialArraySliceNode : ArraySliceNode<Material> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Material/Reverse")]
    public sealed class MaterialArrayReverseNode : ArrayReverseNode<Material> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Material/Distinct")]
    public sealed class MaterialArrayDistinctNode : ArrayDistinctNode<Material> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Material/Get At")]
    public sealed class MaterialArrayGetAtNode : ArrayGetAtNode<Material> { }

    [Serializable, NodeMenuItem("Utilities/Arrays/Material/Length")]
    public sealed class MaterialArrayLengthNode : ArrayLengthNode<Material> { }
}

