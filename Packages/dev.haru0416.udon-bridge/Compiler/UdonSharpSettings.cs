using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UnityEditor;

namespace UdonBridge
{
    /// <summary>UdonSharp's references, sources (with their defines) and parse options, from its CompilationContext.</summary>
    public static class UdonSharpSettings
    {
        static readonly Type ContextType = typeof(UdonSharp.Compiler.UdonSharpCompilerV1).Assembly.GetType("UdonSharp.Compiler.CompilationContext");
        static List<MetadataReference> references;
        static Dictionary<string, string[]> definesByFile;

        public static bool Available => ContextType?.GetMethod("GetMetadataReferences", BindingFlags.Public | BindingFlags.Static) != null
                                        && ContextType.GetMethod("GetBuildAssemblies", BindingFlags.Public | BindingFlags.Static) != null;

        public static IReadOnlyList<MetadataReference> References =>
            references ??= ((IEnumerable)ContextType.GetMethod("GetMetadataReferences", BindingFlags.Public | BindingFlags.Static).Invoke(null, null)).Cast<MetadataReference>().ToList();

        /// <summary>Every source file UdonSharp compiles, with its assembly's preprocessor symbols (main thread the first time).</summary>
        public static IReadOnlyDictionary<string, string[]> Sources
        {
            get
            {
                if (definesByFile != null) return definesByFile;
                var found = new Dictionary<string, string[]>();
                var assemblies = (IEnumerable)ContextType.GetMethod("GetBuildAssemblies", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { true, EditorUserBuildSettings.activeBuildTarget });
                foreach (var a in assemblies)
                {
                    // ScriptAssembly: SourceFiles and Defines are lists (fields or properties).
                    var t = a.GetType();
                    object Member(string name) => t.GetProperty(name)?.GetValue(a) ?? t.GetField(name)?.GetValue(a);
                    var files = (Member("SourceFiles") as IEnumerable<string>)?.ToArray();
                    var defines = (Member("Defines") as IEnumerable<string>)?.ToArray() ?? Array.Empty<string>();
                    if (files == null) throw new InvalidOperationException("UdonSharp's ScriptAssembly has no SourceFiles (SDK changed?)");
                    foreach (var f in files) found[f] = defines;
                }
                return definesByFile = found;
            }
        }

        // UdonSharp parses C# 9 on Unity 2022.3 (7.3 before).
        public static CSharpParseOptions ParseOptions(string[] defines) =>
            CSharpParseOptions.Default.WithDocumentationMode(DocumentationMode.None).WithPreprocessorSymbols(defines).WithLanguageVersion(LanguageVersion.CSharp9);

        public static CSharpParseOptions ParseOptionsFor(string path) =>
            ParseOptions(Sources.TryGetValue(path, out var d) ? d : Array.Empty<string>());

        /// <summary>Forget the source list after scripts are added or removed (main thread).</summary>
        public static void Refresh() => definesByFile = null;
    }
}
