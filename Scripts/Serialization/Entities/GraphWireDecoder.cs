using HSE.Mechanics.Components;
using HSE.Serialization.Wire.Generated;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace HSE.Serialization.Wire.Entities
{
    public static unsafe class GraphWireDecoder
    {
        public static WireRecord GetRecord(in OverrideKey key) => new WireRecord
        {
            TypeId       = key.TypeId,
            Version      = key.Version,
            Offset       = key.DataOffset,
            ByteLength   = key.DataSize,
            ElementCount = key.ElementCount
        };

        public static CodecResult Validate(in OverrideKey key, ref BlobArray<byte> data,
            out ComponentType type, out int nativeSize)
        {
            type       = default;
            nativeSize = 0;

            var record = GetRecord(key);
            var result = ComponentCodecs.ValidateRecord(record, (byte*)data.GetUnsafePtr(), data.Length);
            if (result != CodecResult.Success) return result;

            ComponentCodecs.GetLayout(key.TypeId, key.Version, out _, out nativeSize, out bool buffer);
            type = ComponentCodecs.GetComponentType(key.TypeId);
            if (type.TypeIndex == TypeIndex.Null) return CodecResult.UnknownType;
            if (type.IsZeroSized) return CodecResult.Success;

            var info = TypeManager.GetTypeInfo(type.TypeIndex);
            if (type.IsBuffer != buffer || (!type.IsComponent && !type.IsBuffer) ||
                nativeSize != (buffer ? info.ElementSize : info.SizeInChunk))
                return CodecResult.IncompatibleComponent;

            return CodecResult.Success;
        }

        // Main-thread adapter. Validation precedes any buffer resize or destination write.
        public static CodecResult Apply(ref SystemState state, Entity entity, in OverrideKey key,
            ref BlobArray<byte> data)
        {
            var result = Validate(key, ref data, out var type, out int nativeSize);
            if (result != CodecResult.Success) return result;
            if (!state.EntityManager.HasComponent(entity, type)) return CodecResult.MissingComponent;
            if (type.IsZeroSized) return CodecResult.Success;

            // Dynamic type access must also wait for jobs owned by other systems.
            ComponentCodecs.CompleteWriteDependency(key.TypeId, state.EntityManager);
            var storage = state.GetEntityStorageInfoLookup()[entity];
            var handle  = state.EntityManager.GetDynamicComponentTypeHandle(type);
            var record  = GetRecord(key);
            byte* source = (byte*)data.GetUnsafePtr();

            if (type.IsBuffer)
            {
                var buffer = storage.Chunk.GetUntypedBufferAccessor(ref handle);
                buffer.ResizeUninitialized(storage.IndexInChunk, key.ElementCount);

                return ComponentCodecs.DeserializeRecord(record, source, data.Length,
                    buffer.GetUnsafePtr(storage.IndexInChunk), nativeSize * key.ElementCount);
            }

            var array = storage.Chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref handle, nativeSize);
            byte* destination = (byte*)array.GetUnsafePtr() + storage.IndexInChunk * nativeSize;

            return ComponentCodecs.DeserializeRecord(record, source, data.Length, destination, nativeSize);
        }
    }
}
