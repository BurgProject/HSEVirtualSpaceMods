using System;
using System.Runtime.InteropServices;

namespace HSE.Serialization.Wire
{
    public enum CodecResult : byte
    {
        Success, UnknownType, UnknownVersion, InvalidLength, InvalidValue, OverlappingMemory,
        RegenerationRequired, MissingComponent, IncompatibleComponent, StateOnlyType,
        UnsupportedProgramVersion, UnknownCommand, UnsupportedCommandVersion, InvalidProgram, InvalidCommand
    }

    // Persist this metadata alongside the bytes, never persist a runtime TypeIndex.
    [Serializable]
    public struct WireRecord
    {
        public uint TypeId;
        public uint Version;
        public int Offset;
        public int ByteLength;
        public int ElementCount;

        public bool IsInside(int storageLength) => Offset >= 0 && ByteLength >= 0 &&
            ElementCount >= 0 && (long)Offset + ByteLength <= storageLength;
    }

    public static unsafe class WirePrimitives
    {
        /// <summary>Проверяет границы длины и терминатор атомарного FixedString на little-endian платформе.</summary>
        public static bool IsValidFixedString(byte* source, int size)
        {
            if (!BitConverter.IsLittleEndian || source == null || (size != 32 && size != 64 && size != 128)) return false;
            int length = (int)Read(source, 2);
            return length <= size - 3 && source[2 + length] == 0;
        }

        /// <summary>Копирует заранее проверенный фиксированный блок без требований к выравниванию wire-адреса.</summary>
        public static T ReadFixedString<T>(byte* source) where T : unmanaged
        {
            T value = default;
            byte* destination = (byte*)&value;
            for (int i = 0; i < sizeof(T); i++) destination[i] = source[i];
            return value;
        }

        /// <summary>Копирует атомарное значение, включая фиксированную ёмкость и терминатор.</summary>
        public static void WriteFixedString<T>(byte* destination, T value) where T : unmanaged
        {
            byte* source = (byte*)&value;
            for (int i = 0; i < sizeof(T); i++) destination[i] = source[i];
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct Bits
        {
            [FieldOffset(0)] public ulong  U64;
            [FieldOffset(0)] public double F64;
            [FieldOffset(0)] public uint   U32;
            [FieldOffset(0)] public float  F32;
        }

        public static ulong Read(byte* source, int size)
        {
            ulong value = 0;

            for (int i = 0; i < size; i++)
                value |= (ulong)source[i] << (8 * i);

            return value;
        }

        public static void Write(byte* destination, ulong value, int size)
        {
            for (int i = 0; i < size; i++)
                destination[i] = (byte)(value >> (8 * i));
        }

        public static float  ReadFloat(byte* p)  => new Bits { U32 = (uint)Read(p, 4) }.F32;
        public static double ReadDouble(byte* p) => new Bits { U64 = Read(p, 8) }.F64;

        public static void WriteFloat(byte* p, float value) => Write(p, new Bits { F32 = value }.U32, 4);
        public static void WriteDouble(byte* p, double value) => Write(p, new Bits { F64 = value }.U64, 8);

        // Lengths are exact, not capacities. A zero-byte range may have a null pointer.
        public static CodecResult Validate(void* source, int sourceLength, void* destination,
            int destinationLength, int count, int sourceStride, int destinationStride, bool buffer)
        {
            if (count < 0 || (!buffer && count != 1) || sourceLength < 0 || destinationLength < 0 ||
                (long)count * sourceStride != sourceLength ||
                (long)count * destinationStride != destinationLength ||
                (sourceLength != 0 && source == null) || (destinationLength != 0 && destination == null))
                return CodecResult.InvalidLength;

            ulong src = (ulong)source, dst = (ulong)destination;
            if ((src <= dst && dst - src < (ulong)sourceLength && destinationLength != 0) ||
                (dst <= src && src - dst < (ulong)destinationLength && sourceLength != 0))
                return CodecResult.OverlappingMemory;

            return CodecResult.Success;
        }
    }
}
