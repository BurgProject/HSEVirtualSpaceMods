using System;
using System.Collections.Generic;
using System.Linq;
using HSE.Mechanics.Components;
using HSE.Serialization.Wire.Generated;
using Unity.Entities;

namespace HSE.CodeCraft
{
    public sealed class CommandComponentType
    {
        public readonly uint Id;
        public readonly Type Type;
        public readonly bool IsBuffer;
        public readonly bool IsEnableable;
        public bool IsUserSelectable => Type.Assembly == typeof(RotateAroundAxis).Assembly &&
            (Type.Namespace == "HSE.Mechanics.Components" ||
             (Type.Namespace?.StartsWith("HSE.Mechanics.Components.", StringComparison.Ordinal) ?? false));

        public CommandComponentType(uint id, Type type)
        {
            Id = id;
            Type = type;
            IsBuffer = typeof(IBufferElementData).IsAssignableFrom(type);
            IsEnableable = typeof(IEnableableComponent).IsAssignableFrom(type);
        }
    }

    /// <summary>Каталог авторинга строится из регистрации кодека, без ручного списка типов и runtime-reflection.</summary>
    public static class CommandComponentTypes
    {
        private static List<CommandComponentType> _types;
        private static bool _reportedUnavailable;

        public static IReadOnlyList<CommandComponentType> All
        {
            get
            {
                if (!ComponentCodecs.IsReady)
                {
                    if (!_reportedUnavailable)
                        UnityEngine.Debug.LogError("Wire codec is unavailable. Install a complete SDK with its exported codec.");
                    _reportedUnavailable = true;
                    return Array.Empty<CommandComponentType>();
                }
                _reportedUnavailable = false;
                if (_types != null) return _types;
                _types = new List<CommandComponentType>();
                var getId = typeof(ComponentCodecs).GetMethod(nameof(ComponentCodecs.GetTypeId));
                var assemblies = new[] { typeof(RotateAroundAxis).Assembly, typeof(GraphProgram).Assembly };
                foreach (var type in assemblies.SelectMany(a => a.GetTypes()).Where(t => t.IsValueType && !t.IsGenericType))
                {
                    if (!Attribute.IsDefined(type, typeof(HSESerializableStructAttribute))) continue;
                    uint id = (uint)getId.MakeGenericMethod(type).Invoke(null, null);
                    if (id != 0) _types.Add(new CommandComponentType(id, type));
                }
                _types.Sort((a, b) => string.CompareOrdinal(a.Type.FullName, b.Type.FullName));
                return _types;
            }
        }

        public static CommandComponentType Find(uint id) => All.FirstOrDefault(t => t.Id == id);
        public static CommandComponentType Find(Type type) => All.FirstOrDefault(t => t.Type == type);
    }
}
