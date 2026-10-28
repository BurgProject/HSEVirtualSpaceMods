using UnityEngine;

namespace HSE.CodeCraft
{
    public class ArrayExtractorNode<T>
    {
        public T[] Values;

        public T ExtractValue = default(T);

        public int ExtractIndex = 1;


        //public override void OnFieldsChange()
        //{
        //    if (Values == null)
        //        return;
        //    if (ExtractIndex >= Values.Length)
        //        ExtractIndex = Values.Length - 1;
        //    if(ExtractIndex != -1)
        //        ExtractValue = Values[ExtractIndex];
        //}
    }

}
