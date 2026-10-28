using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine;

public static class FixedBytesFactory
{
    public static object CreateAndCopyFixedBytes(this byte[] source)
    {
        return source.Length switch
        {
            <= 16 => CreateFixedBytes16(source),
            <= 30 => CreateFixedBytes30(source),
            <= 64 => CreateFixedBytes62(source),
            <= 128 => CreateFixedBytes126(source),
            _ => throw new ArgumentException($"Source array too large: {source.Length} bytes")
        };
    }

    public static unsafe FixedBytes16 CreateFixedBytes16(byte[] source)
    {
        FixedBytes16 result = default;
        fixed (byte* sourcePtr = source)
        {
            void* destPtr = UnsafeUtility.AddressOf(ref result);
            UnsafeUtility.MemCpy(destPtr, sourcePtr, source.Length);
        }
        return result;
    }

    public static unsafe FixedBytes30 CreateFixedBytes30(byte[] source)
    {
        FixedBytes30 result = default;
        fixed (byte* sourcePtr = source)
        {
            void* destPtr = UnsafeUtility.AddressOf(ref result);
            UnsafeUtility.MemCpy(destPtr, sourcePtr, source.Length);
        }
        return result;
    }

    public static unsafe FixedBytes62 CreateFixedBytes62(byte[] source)
    {
        FixedBytes62 result = default;
        fixed (byte* sourcePtr = source)
        {
            void* destPtr = UnsafeUtility.AddressOf(ref result);
            UnsafeUtility.MemCpy(destPtr, sourcePtr, source.Length);
        }
        return result;
    }

    public static unsafe FixedBytes126 CreateFixedBytes126(byte[] source)
    {
        FixedBytes126 result = default;
        fixed (byte* sourcePtr = source)
        {
            void* destPtr = UnsafeUtility.AddressOf(ref result);
            UnsafeUtility.MemCpy(destPtr, sourcePtr, source.Length);
        }
        return result;
    }

    public static unsafe FixedBytes510 CreateFixedBytes510(byte[] source)
    {
        FixedBytes510 result = default;
        fixed (byte* sourcePtr = source)
        {
            void* destPtr = UnsafeUtility.AddressOf(ref result);
            UnsafeUtility.MemCpy(destPtr, sourcePtr, source.Length);
        }
        return result;
    }
}
