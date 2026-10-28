using System;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;

namespace HSE.Collections
{
    [BurstCompile]
    public struct BlobHashMapUtility
    {
        [BurstCompile]
        public static NativeList<T> GetValuesFromBlobMultiHashMap<R, T>(R key, ref BlobMultiHashMap<R, T> map) where T : unmanaged where R : unmanaged, IEquatable<R>
        {
            NativeList<T> result = new NativeList<T>(Allocator.Temp);
            map.TryGetFirstValue(key, out var value, out var iterator);
            do
            {
                result.Add(value);
            }
            while (map.TryGetNextValue(out value, ref iterator));
            return result;
        }
    }
}
