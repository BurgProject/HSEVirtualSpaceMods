

using System.Collections.Generic;

namespace HSE.Mono.Extensions
{
    public static class DictionaryExtensions
    {
        public static void Fill<L, R>(this Dictionary<L, List<R>> map, L key, R value, bool withCheck = false)
        {
            if (!map.ContainsKey(key))
                map.Add(key, new List<R> { value });
            else if (!withCheck) map[key].Add(value);
            else if (!map[key].Contains(value)) map[key].Add(value);
        }
        public static void FillSingle<L, R>(this Dictionary<L, List<R>> map, L key, R value)
        {
            if (!map.ContainsKey(key))
                map.Add(key, new List<R> { value });
        }
        public static void FillSingle<L, R>(this Dictionary<L, List<R>> map, L key, List<R> values)
        {
            if (!map.ContainsKey(key))
                map.Add(key, values);
        }
        public static void Fill<L, R>(this Dictionary<L, List<R>> map, L key, List<R> values, bool withCheck = false)
        {
            if (!map.ContainsKey(key))
                map.Add(key, values);
            else if (!withCheck) map[key].AddRange(values);
            else
            {
                foreach (var item in values)
                    if (!map[key].Contains(item)) map[key].Add(item);
            }
        }

        public static int GetMultiMapsCapacity<L, R>(this Dictionary<L, List<R>> keyValuePairs)
        {
            int capacity = 0;
            if (keyValuePairs == null) return 0;
            else foreach (var keyPair in keyValuePairs)
                    capacity += keyPair.Value.Count;
            return capacity;
        }
    }
}
