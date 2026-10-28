using System;
using UnityEngine;

namespace HSE.CodeCraft.Editor
{
    public static class TypeExtensions
    {
        public static Type GetBaseLayerType(this Type choosenType)
        {
            if (choosenType.IsArray)
                return choosenType.GetElementType();
            return choosenType;
        }

        public static void CheckFloatTypeName(this Type singleType, bool highRegister = false)=>CheckFloatTypeName(singleType.Name, highRegister);
        public static void CheckFloatTypeName(this string singleTypeName, bool highRegister = false)
        {
            if (singleTypeName == typeof(float).Name){
  ;
                if (highRegister)
                    singleTypeName = "Float";
                else singleTypeName = "float";
            }
        }
    }
}
