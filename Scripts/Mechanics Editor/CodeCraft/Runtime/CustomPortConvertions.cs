using GraphProcessor;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HSE.CodeCraft
{
    public class CustomPortConvertions : ITypeAdapter
    {
        public static Vector4 ConvertFloatToVector4(float from) => new Vector4(from, from, from, from);
        public static float ConvertVector4ToFloat(Vector4 from) => 0;
        public static float ConvertIntToFloat(int from) => from;
        public static int ConvertFloatToInt(float from) => (int)from;

        public override IEnumerable<(Type, Type)> GetIncompatibleTypes()
        {
            yield return (typeof(ConditionalLink), typeof(object));
            yield return (typeof(RelayNode.PackedRelayData), typeof(object));
        }
    }
}
