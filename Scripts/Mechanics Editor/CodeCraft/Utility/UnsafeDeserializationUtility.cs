
using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
public struct UnsafeDeserializationUtility
{

    public static unsafe void WriteToDstImplicit(void* dst,
                                       void* src,
                                       int elementSize,
                                       int srcFixedElementSize)
    {
        byte* dstPtr = (byte*)dst;
        byte* srcPtr = (byte*)src;

        WriteToDstBytesImplicit(dstPtr, srcPtr, elementSize, srcFixedElementSize);
    }
    public static unsafe void WriteToDstBytesImplicit(byte* dstBytes,
                                     byte* sourceFixedBytes,
                                     int elementSize,
                                     int srcFixedElementSize)
    {
        if (elementSize == srcFixedElementSize)
        {
            UnsafeUtility.MemCpy(
                dstBytes,
                sourceFixedBytes,
                elementSize
            );
        }
        else if (elementSize < srcFixedElementSize)
        {
            int bytesToCopy = math.min(elementSize, srcFixedElementSize);
            UnsafeUtility.MemCpy(dstBytes, sourceFixedBytes, bytesToCopy);
        }
    }
    public static unsafe void WriteToDstBytesWithOffset(byte* dstPtrBytes,
                                  byte* sourceBytes,
                                  int offset, int length)
    {
        byte* ptrOffset = sourceBytes + offset;
        UnsafeUtility.MemCpy(dstPtrBytes, ptrOffset, length);
    }

    public static unsafe void WriteToBufferImplicit(void* dstBytesArray,
                                   void* sourceFixedBytesArray,
                                   int elementCountToWrite,
                                   int bufferElementSize,
                                   int srcFixedElementSize = 0)
    {
        byte* dstPtr = (byte*)dstBytesArray;
        byte* srcPtr = (byte*)sourceFixedBytesArray;

        if (bufferElementSize == srcFixedElementSize || srcFixedElementSize == 0)
        {
            UnsafeUtility.MemCpy(
                dstPtr,
                srcPtr,
                elementCountToWrite * srcFixedElementSize
            );
        }
        else if (bufferElementSize < srcFixedElementSize)
        {

            for (int i = 0; i < elementCountToWrite; i++)
            {
                byte* dstElement = dstPtr + (i * bufferElementSize);
                byte* srcElement = srcPtr + (i * srcFixedElementSize);

                UnsafeUtility.MemCpy(dstElement, srcElement, bufferElementSize);
            }
        }
        else
        {
            int fixedBytesPerElement = (bufferElementSize + srcFixedElementSize - 1) / srcFixedElementSize;

            for (int i = 0; i < elementCountToWrite; i++)
            {
                byte* dstElement = dstPtr + (i * bufferElementSize);
                byte* srcElement = srcPtr + (i * fixedBytesPerElement * srcFixedElementSize);

                int bytesToCopy = Math.Min(bufferElementSize, fixedBytesPerElement * srcFixedElementSize);
                UnsafeUtility.MemCpy(dstElement, srcElement, bytesToCopy);

                if (bufferElementSize > bytesToCopy)
                {
                    UnsafeUtility.MemClear(dstElement + bytesToCopy, bufferElementSize - bytesToCopy);
                }
            }
        }
    }
}
