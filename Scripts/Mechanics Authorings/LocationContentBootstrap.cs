using System;
using System.Collections.Generic;

namespace HSE.Mechanics.Authorings
{
    /// <summary>Точки входа ECS для загрузки локации без встроенной ContentScene. Не содержит версии компонентов или признака наличия Mono-сцены.</summary>
    [Serializable]
    public sealed class LocationContentBootstrap
    {
        public const string FileName = "location-entities.json";

        public int Version = 1;
        public string[] SubScenes;

        public void Validate()
        {
            if (Version != 1 || SubScenes == null || SubScenes.Length == 0 || SubScenes.Length > 128)
                throw new InvalidOperationException("Unsupported or invalid location bootstrap.");

            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var guid in SubScenes)
            {
                if (guid == null || guid.Length != 32 || !unique.Add(guid))
                    throw new InvalidOperationException("Invalid or duplicate location subscene GUID.");

                foreach (char digit in guid)
                    if (!Uri.IsHexDigit(digit))
                        throw new InvalidOperationException("Invalid location subscene GUID.");

                if (guid == new string('0', 32))
                    throw new InvalidOperationException("Empty location subscene GUID.");
            }
        }
    }
}
