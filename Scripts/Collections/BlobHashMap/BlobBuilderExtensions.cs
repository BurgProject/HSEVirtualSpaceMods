namespace HSE.Collections
{
    using System;
    using System.Collections.Generic;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Entities;
    using UnityEngine;

    /// <summary> Extension methods for BlobBuilder to allocate BlobHashMaps with. </summary>
    public static class BlobBuilderExtensions
    {
        // 16384 is somewhat arbitrary but tests have shown that for small enough capacities this will
        // be a bit faster while still not allocating loads of memory
        private const int UseBucketCapacityRatioOfThreeUpTo = 16384;

        /// <summary> Allocates a BlobHashMap and copies all key value pairs from the source NativeHashMap. </summary>
        /// <param name="builder">Reference to the struct BlobBuilder used to construct the hashmap</param>
        /// <param name="blobHashMap">Reference to the struct BlobHashMap field</param>
        /// <param name="source">Source hashmap to copy keys and values from</param>
        public static void ConstructHashMap<TKey, TValue>(
            this ref BlobBuilder builder, ref BlobHashMap<TKey, TValue> blobHashMap, ref NativeParallelHashMap<TKey, TValue> source)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            int count = source.Count();
            var hashMapBuilder = builder.AllocateHashMap(ref blobHashMap, count);

            using var e = source.GetEnumerator();
            while (e.MoveNext())
            {
                hashMapBuilder.Add(e.Current.Key, e.Current.Value);
            }
        }

        /// <summary> Allocates a BlobHashMap and copies all key value pairs from the source dictionary. </summary>
        /// <param name="builder">Reference to the struct BlobBuilder used to construct the hashmap</param>
        /// <param name="blobHashMap">Reference to the struct BlobHashMap field</param>
        /// <param name="source">Source hashmap to copy keys and values from</param>
        public static void ConstructHashMap<TKey, TValue>(
            this ref BlobBuilder builder, ref BlobHashMap<TKey, TValue> blobHashMap, Dictionary<TKey, TValue> source)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            int count = source.Count;
            int ratio = count <= UseBucketCapacityRatioOfThreeUpTo ? 3 : 2;

            var hashMapBuilder = builder.AllocateHashMap(ref blobHashMap, source.Count, ratio);
            foreach (var kv in source)
                hashMapBuilder.Add(kv.Key, kv.Value);
        }

        /// <summary> Allocates a BlobHashMap and returns a builder than can be used to add values manually. </summary>
        /// <param name="builder">Reference to the struct BlobBuilder used to construct the hashmap</param>
        /// <param name="blobHashMap">Reference to the struct BlobHashMap field</param>
        /// <param name="capacity">Capacity of the allocated hashmap. This value cannot be changed after allocation</param>
        /// <returns>Builder that can be ued to add values to the hashmap</returns>
        public static BlobBuilderHashMap<TKey, TValue> AllocateHashMap<TKey, TValue>(
            this ref BlobBuilder builder, ref BlobHashMap<TKey, TValue> blobHashMap, int capacity)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            return AllocateHashMap(ref builder, ref blobHashMap, capacity, capacity <= UseBucketCapacityRatioOfThreeUpTo ? 3 : 2);
        }

        /// <summary> Allocates a BlobHashMap and returns a builder than can be used to add values manually. </summary>
        /// <param name="builder">Reference to the struct BlobBuilder used to construct the hashmap</param>
        /// <param name="blobHashMap">Reference to the struct BlobHashMap field</param>
        /// <param name="capacity">Capacity of the allocated hashmap. This value cannot be changed after allocation</param>
        /// <param name="bucketCapacityRatio">
        /// Bucket capacity ratio to use when allocating the hashmap.
        /// A higher value may result in less collisions and slightly better performance, but memory consumption increases exponentially.
        /// </param>
        /// <returns>Builder that can be ued to add values to the hashmap</returns>
        public static BlobBuilderHashMap<TKey, TValue> AllocateHashMap<TKey, TValue>(
            this ref BlobBuilder builder, ref BlobHashMap<TKey, TValue> blobHashMap,
            int capacity, int bucketCapacityRatio)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            var hashmapBuilder = new BlobBuilderHashMap<TKey, TValue>(capacity, bucketCapacityRatio, ref builder, ref blobHashMap.Data);

            return hashmapBuilder;
        }

        /// <summary> Allocates a BlobHashMap and copies all key value pairs from the source NativeHashMap. </summary>
        /// <param name="builder">Reference to the struct BlobBuilder used to construct the hashmap</param>
        /// <param name="blobMultiHashMap">Reference to the struct BlobMultiHashMap field</param>
        /// <param name="source">Source multihashmap to copy keys and values from</param>
        public static void ConstructMultiHashMap<TKey, TValue>(
            this ref BlobBuilder builder, ref BlobMultiHashMap<TKey, TValue> blobMultiHashMap, ref NativeParallelMultiHashMap<TKey, TValue> source)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            var count = source.Count();
            var hashMapBuilder = builder.AllocateMultiHashMap(ref blobMultiHashMap, count);

            using var e = source.GetEnumerator();
            while (e.MoveNext())
            {
                hashMapBuilder.Add(e.Current.Key, e.Current.Value);
            }
        }

        /// <summary> Allocates a BlobMultiHashMap and returns a builder than can be used to add values manually. </summary>
        /// <param name="builder">Reference to the struct BlobBuilder used to construct the hashmap</param>
        /// <param name="blobMultiHashMap">Reference to the struct BlobHashMap field</param>
        /// <param name="capacity">Capacity of the allocated multihashmap. This value cannot be changed after allocation</param>
        /// <returns>Builder that can be ued to add values to the multihashmap</returns>
        public static BlobBuilderMultiHashMap<TKey, TValue> AllocateMultiHashMap<TKey, TValue>(
            this ref BlobBuilder builder, ref BlobMultiHashMap<TKey, TValue> blobMultiHashMap, int capacity)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            return new BlobBuilderMultiHashMap<TKey, TValue>(
                capacity, capacity <= UseBucketCapacityRatioOfThreeUpTo ? 3 : 2, ref builder, ref blobMultiHashMap.Data);
        }

        /// <summary> Serialize structure in BlobArray<byte>. </summary>
        /// <param name="builder">Reference to the struct BlobBuilder used to serialize</param>
        /// <param name="blobArray">Reference to the struct BlobArray field</param>
        /// <param name="data">Structure data</param>
        public static unsafe void SerializeByteData<T>(
            this ref BlobBuilder builder, ref BlobArray<byte> blobArray, T data)
            where T : unmanaged
        {
            int size = UnsafeUtility.SizeOf<T>();
            BlobBuilderArray<byte> arrayBuilder = builder.Allocate(ref blobArray, size);
            UnsafeUtility.CopyStructureToPtr(ref data, arrayBuilder.GetUnsafePtr());
        }

        /// <summary> Serialize structure in BlobArray<byte>. </summary>
        /// <param name="builder">Reference to the struct BlobBuilder used to serialize</param>
        /// <param name="blobArray">Reference to the struct BlobArray field</param>
        /// <param name="data">Structure data</param>
        public static unsafe T DeserializeByteData<T>(ref BlobArray<byte> blobArray)
            where T : unmanaged
        {
            int expectedSize = UnsafeUtility.SizeOf<T>();
            if (blobArray.Length != expectedSize)
                throw new ArgumentException($"desync blob array: {blobArray.Length} and expected {expectedSize}");

            NativeArray<byte> tempBuffer = new NativeArray<byte>(blobArray.Length, Allocator.Temp);
            UnsafeUtility.MemCpy(tempBuffer.GetUnsafePtr(), blobArray.GetUnsafePtr(),blobArray.Length);

            T result;
            UnsafeUtility.CopyPtrToStructure(tempBuffer.GetUnsafePtr(), out result);
            tempBuffer.Dispose();
            return result;
        }

        public static unsafe T DeserializeByteData<T>(this ref BlobBuilderArray<byte> blobArray)
    where T : unmanaged
        {
            int expectedSize = UnsafeUtility.SizeOf<T>();
            if (blobArray.Length != expectedSize)
                throw new ArgumentException($"desync blob array: {blobArray.Length} and expected {expectedSize}");

            NativeArray<byte> tempBuffer = new NativeArray<byte>(blobArray.Length, Allocator.Temp);
            UnsafeUtility.MemCpy(tempBuffer.GetUnsafePtr(), blobArray.GetUnsafePtr(), blobArray.Length);

            T result;
            UnsafeUtility.CopyPtrToStructure(tempBuffer.GetUnsafePtr(), out result);
            tempBuffer.Dispose();
            return result;
        }


        public static unsafe void CopyByteArray(this ref BlobBuilderArray<byte> blobArray, byte[] byteArray)
        {
            if (byteArray.Length == 0)
                return;

            if (blobArray.Length < byteArray.Length)
                throw new ArgumentException("Blob array is to small to copy bytes");

            fixed (byte* ptr = byteArray)
            {
                void* dstPtr = blobArray.GetUnsafePtr();
         
                UnsafeUtility.MemCpy(dstPtr, ptr, byteArray.Length);
            }
   
        }

        public static unsafe void Fill<T>(this ref BlobBuilderArray<T> blobArray, IEnumerable<T> values) where T : unmanaged
        {
            var enumerator = values.GetEnumerator();
            int i = 0;
            while (enumerator.MoveNext())
            {
                blobArray[i] = enumerator.Current;
                i++;
            }
        }

        public static void FillByDictionary<R, T>(this ref BlobBuilderMultiHashMap<R, T> multiHashMap, Dictionary<R, List<T>> dictMap, bool reverseFill = false) 
            where T : unmanaged where R : unmanaged, IEquatable<R>
        {
            foreach (var compKeyPair in dictMap){
                if(reverseFill)
                    for (int i = compKeyPair.Value.Count - 1; i >= 0; i--)
                        multiHashMap.Add(compKeyPair.Key, compKeyPair.Value[i]);
                else for (int i = 0; i < compKeyPair.Value.Count; i++)
                        multiHashMap.Add(compKeyPair.Key, compKeyPair.Value[i]);
            }
        }

        /// <summary> Allocates a BlobMultiHashMap and returns a builder than can be used to add values manually. </summary>
        /// <param name="builder">Reference to the struct BlobBuilder used to construct the hashmap</param>
        /// <param name="blobMultiHashMap">Reference to the struct BlobHashMap field</param>
        /// <param name="capacity">Capacity of the allocated multihashmap. This value cannot be changed after allocation</param>
        /// <param name="bucketCapacityRatio">
        /// Bucket capacity ratio to use when allocating the hashmap.
        /// A higher value may result in less collisions and slightly better performance, but memory consumption increases exponentially.
        /// </param>
        /// <returns>Builder that can be ued to add values to the multihashmap</returns>
        public static BlobBuilderMultiHashMap<TKey, TValue> AllocateMultiHashMap<TKey, TValue>(
            this ref BlobBuilder builder, ref BlobMultiHashMap<TKey, TValue> blobMultiHashMap,
            int capacity,
            int bucketCapacityRatio)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            return new BlobBuilderMultiHashMap<TKey, TValue>(capacity, bucketCapacityRatio, ref builder, ref blobMultiHashMap.Data);
        }
    }
}
