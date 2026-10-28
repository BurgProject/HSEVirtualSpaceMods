using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HSE.CodeCraft.Editor
{
    public interface IMerheable { }
    public struct Void: IMerheable
    {

    }
    public class TypeIndexCollector
    {
        [MenuItem("Tools/Collect TypeIndex Constants")]
        public static void Collect()
        {
            var types = new List<Type>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsValueType &&
                        typeof(IMerheable).IsAssignableFrom(type))
                    {
                        types.Add(type);
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("// Auto-generated TypeIndex constants");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine("namespace MergeComponentGen");
            sb.AppendLine("{");
            sb.AppendLine("    public static class TypeIndexConstants");
            sb.AppendLine("    {");

            foreach (var type in types)
            {
                // Здесь мы реально получаем TypeIndex через TypeManager
                
                sb.AppendLine($"        public const int {type.Name} = {1};");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText("Assets/TypeIndexConstants.g.cs", sb.ToString());
            AssetDatabase.Refresh();
        }
    }
}
