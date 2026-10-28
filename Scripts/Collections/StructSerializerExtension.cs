using System;
using System.ComponentModel;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

public static class StructSerializerExtension
{
    public static unsafe int GetUnsafeSize<T>() where T : unmanaged => UnsafeUtility.SizeOf<T>();
    public static unsafe int GetUnsafeSize<T>(this T component) where T : unmanaged => UnsafeUtility.SizeOf<T>();
    public static unsafe byte[] SerializeComponent<T>(this T component) where T : unmanaged
    {
        int size = UnsafeUtility.SizeOf<T>();
        byte[] bytes = new byte[size];

        fixed (byte* ptr = bytes)
        {
            UnsafeUtility.CopyStructureToPtr(ref component, ptr);
        }
        return bytes;
    }

    public static unsafe T DeserializeComponent<T>(byte[] bytes) where T : unmanaged
    {
        T component = default;

        fixed (byte* ptr = bytes)
        {
            UnsafeUtility.CopyPtrToStructure(ptr, out component);
        }
        
        return component;
    }
    public static unsafe T DeserializeComponent<T>(this ref BlobArray<byte> bytes) where T : unmanaged
    {
        UnsafeUtility.CopyPtrToStructure<T>(bytes.GetUnsafePtr(), out var component);
        return component;
    }
    public static FixedBytes16 SerializeToFixedBytes16<T>(this T component)
        where T : unmanaged
    {
        if (UnsafeUtility.SizeOf<T>() > 16)
            throw new ArgumentException("Component size exceeds FixedBytes16 capacity");
        FixedBytes16 fixedBytes = default;
        unsafe
        {
            void* ptr = UnsafeUtility.AddressOf(ref fixedBytes);
            UnsafeUtility.CopyStructureToPtr(ref component, ptr);
            return fixedBytes;
        }
    }
    public static unsafe void SerializeToBlobArrayFast<T>(
            this T component,
            ref BlobBuilder builder,
            ref BlobArray<byte> blobArray)
            where T : unmanaged
    {
        int size = UnsafeUtility.SizeOf<T>();

        var dataArray = builder.Allocate(ref blobArray, size);

        void* destPtr = dataArray.GetUnsafePtr();
        void* srcPtr = UnsafeUtility.AddressOf(ref component);

        UnsafeUtility.MemCpy(destPtr, srcPtr, size);
    }

    public static unsafe byte[] GetBytes<T>(T value) where T : unmanaged
    {
        byte[] bytes = new byte[UnsafeUtility.SizeOf<T>()];
        fixed (byte* ptr = bytes)
        {
            UnsafeUtility.CopyStructureToPtr(ref value, ptr);
        }
        return bytes;
    }

    public static unsafe byte[] GetBytes<T>(T[] array) where T : unmanaged
    {
        if (array == null || array.Length == 0)
            return new byte[0];

        int elementSize = UnsafeUtility.SizeOf<T>();
        int totalSize = array.Length * elementSize;

        byte[] bytes = new byte[totalSize];

        fixed (byte* destPtr = bytes)
        {
            fixed (T* srcPtr = array)
            {
                UnsafeUtility.MemCpy(destPtr, srcPtr, totalSize);
            }
        }

        return bytes;
    }

    public static FixedBytes30 SerializeToFixedBytes30<T>(this T component) where T : unmanaged
    {
        if (UnsafeUtility.SizeOf<T>() > 30)
            throw new ArgumentException("Component size exceeds FixedBytes30 capacity");
        FixedBytes30 fixedBytes = default;
        unsafe
        {
            void* ptr = UnsafeUtility.AddressOf(ref fixedBytes);
            UnsafeUtility.CopyStructureToPtr(ref component, ptr);
            return fixedBytes;
        }
    }
    public static FixedBytes62 SerializeToFixedBytes62<T>(this T component) where T : unmanaged
    {
        if (UnsafeUtility.SizeOf<T>() > 62)
            throw new ArgumentException("Component size exceeds FixedBytes62 capacity");
        FixedBytes62 fixedBytes = default;
        unsafe
        {
            void* ptr = UnsafeUtility.AddressOf(ref fixedBytes);
            UnsafeUtility.CopyStructureToPtr(ref component, ptr);
            return fixedBytes;
        }
    }
    public static FixedBytes126 SerializeToFixedBytes126<T>(this T component) where T : unmanaged
    {
        if (UnsafeUtility.SizeOf<T>() > 16)
            throw new ArgumentException("Component size exceeds FixedBytes126 capacity");
        FixedBytes126 fixedBytes = default;
        unsafe
        {
            void* ptr = UnsafeUtility.AddressOf(ref fixedBytes);
            UnsafeUtility.CopyStructureToPtr(ref component, ptr);
            return fixedBytes;
        }
    }
    public static T DeserializeFromFixedBytes16<T>(FixedBytes16 fixedBytes) where T : unmanaged
    {
        unsafe
        {
            void* ptr = UnsafeUtility.AddressOf(ref fixedBytes);
            T result = default;
            UnsafeUtility.CopyPtrToStructure(ptr, out result);
            return result;
        }
    }
    public static T DeserializeFromFixedBytes30<T>(FixedBytes30 fixedBytes) where T : unmanaged
    {
        unsafe
        {
            void* ptr = UnsafeUtility.AddressOf(ref fixedBytes);
            T result = default;
            UnsafeUtility.CopyPtrToStructure(ptr, out result);
            return result;
        }
    }
    public static T DeserializeFromFixedBytes62<T>(FixedBytes62 fixedBytes) where T : unmanaged
    {
        unsafe
        {
            void* ptr = UnsafeUtility.AddressOf(ref fixedBytes);
            T result = default;
            UnsafeUtility.CopyPtrToStructure(ptr, out result);
            return result;
        }
    }
    public static T DeserializeFromFixedBytes126<T>(FixedBytes126 fixedBytes) where T : unmanaged
    {
        unsafe
        {
            void* ptr = UnsafeUtility.AddressOf(ref fixedBytes);
            T result = default;
            UnsafeUtility.CopyPtrToStructure(ptr, out result);
            return result;
        }
    }
}
