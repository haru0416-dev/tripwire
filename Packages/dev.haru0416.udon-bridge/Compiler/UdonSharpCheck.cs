using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UdonSharp.Compiler;

namespace UdonBridge
{
    public struct CheckProblem
    {
        public int Start, Length, Line, Column;
        public bool Error;
        public string Message;
        /// <summary>Another file the problem is in (null: the checked file); UdonSharp stops on it all the same.</summary>
        public string File;
    }

    /// <summary>
    /// UdonSharp's own errors for unsaved text: its binder, emit and Udon's assembler run on a Roslyn compilation, in
    /// memory (no assembly load, no program asset written).
    /// </summary>
    public static class UdonSharpCheck
    {
        public const string BehaviourTypeName = "UdonSharp.UdonSharpBehaviour";

        // Each may be null on another SDK: Available / EmitAvailable say so.
        const BindingFlags InternalStatic = BindingFlags.NonPublic | BindingFlags.Static;
        static readonly Assembly Us = typeof(UdonSharpCompilerV1).Assembly;
        static readonly Type ContextType = Us.GetType("UdonSharp.Compiler.CompilationContext");
        static readonly Type BindingType = Us.GetType("UdonSharp.Compiler.ModuleBinding");
        static readonly Type TupleType = BindingType != null ? typeof(ValueTuple<,>).MakeGenericType(typeof(INamedTypeSymbol), BindingType) : null;
        static readonly MethodInfo bindAll = typeof(UdonSharpCompilerV1).GetMethod("BindAllPrograms", InternalStatic);
        static readonly MethodInfo inheritance = ContextType?.GetMethod("BuildUdonBehaviourInheritanceLookup");
        static readonly MethodInfo isBehaviour = Us.GetType("UdonSharp.Compiler.Binder.ExternResolverExtensions")?.GetMethod("IsUdonSharpBehaviour", BindingFlags.Public | BindingFlags.Static);
        static readonly Type UdonInterface = Us.GetType("UdonSharp.Compiler.Udon.CompilerUdonInterface");
        static readonly MethodInfo assemblyCacheInit = UdonInterface?.GetMethod("AssemblyCacheInit", InternalStatic);
        static readonly Type ModuleType = Us.GetType("UdonSharp.Compiler.Assembly.AssemblyModule");
        static readonly Type EmitType = Us.GetType("UdonSharp.Compiler.Emit.EmitContext");
        static readonly Type CompilerExceptionType = Us.GetType("UdonSharp.Core.CompilerException");
        static readonly MethodInfo checkSync = typeof(UdonSharpCompilerV1).GetMethod("CheckSyncCompatibility", InternalStatic);
        static readonly MethodInfo assemble = UdonInterface?.GetMethod("Assemble", BindingFlags.Public | BindingFlags.Static);
        static readonly Type SeverityType = Us.GetType("UdonSharp.Compiler.DiagnosticSeverity");
        static readonly MethodInfo addDiagnostic = SeverityType != null ? ContextType?.GetMethod("AddDiagnostic", new[] { SeverityType, typeof(Location), typeof(string) }) : null;

        static volatile bool prepared;

        public static readonly bool Available = ContextType != null && BindingType != null && TupleType != null && bindAll != null && inheritance != null && isBehaviour != null;

        public static readonly bool EmitAvailable = Available && ModuleType != null && EmitType != null && CompilerExceptionType != null && checkSync != null && assemble != null && addDiagnostic != null;

        /// <summary>First line only; other exception types than NotSupported keep their name (UdonSharp's internal errors).</summary>
        public static string Readable(string message)
        {
            var first = message.Split('\n')[0].Trim();
            return first.StartsWith("System.NotSupportedException: ") ? first.Substring("System.NotSupportedException: ".Length) : first;
        }

        /// <summary>Call on the main thread before checks run elsewhere: the binder's first use reads the AssetDatabase.</summary>
        public static void Prepare()
        {
            if (!Available || !UdonNames.Available) return; // an SDK whose internals moved: the checks report it themselves
            if (!prepared) UdonNames.Warm();
            // Every time: UdonSharp drops this cache without a domain reload; it returns at once when cached.
            assemblyCacheInit?.Invoke(null, null);
            prepared = true;
        }

        /// <summary>For text without C# errors. Any thread after <see cref="Prepare"/>.</summary>
        public static List<CheckProblem> Check(CSharpCompilation compilation, SyntaxTree tree, string path, bool hasProgramAsset)
        {
            var problems = new List<CheckProblem>();
            if (!Available) return problems;
            if (!prepared)
            {
                if (!UnityEditorInternal.InternalEditorUtility.CurrentThreadIsMainThread())
                {
                    problems.Add(new CheckProblem { Line = 0, Column = 0, Length = 1, Error = false, Message = "UdonSharp's check is not ready yet (UdonSharpCheck.Prepare runs on the main thread first)" });
                    return problems;
                }
                Prepare();
            }
            var model = compilation.GetSemanticModel(tree);
            INamedTypeSymbol root = null;
            foreach (var decl in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                if (model.GetDeclaredSymbol(decl) is INamedTypeSymbol t && !t.IsAbstract && !t.IsGenericType && (bool)isBehaviour.Invoke(null, new object[] { t }))
                { root = t; break; }
            if (root == null)
            {
                Uncompiled(tree, model, path, problems);
                // UdonSharp's own rule: a script with a program asset must declare a behaviour.
                if (problems.Count == 0 && hasProgramAsset)
                    problems.Add(new CheckProblem { Line = 0, Column = 0, Length = 1, Error = true,
                        Message = "Script with U# program asset referencing it must have an UdonSharpBehaviour definition; scripts without UdonSharpBehaviour definitions should not have an associated UdonSharpProgramAsset" });
                return problems;
            } // a plain class or an abstract base: nothing of its own to bind

            var context = Activator.CreateInstance(ContextType, new UdonSharpCompileOptions());
            ContextType.GetProperty("RoslynCompilation").SetValue(context, compilation);
            var binding = Activator.CreateInstance(BindingType);
            BindingType.GetField("tree").SetValue(binding, tree);
            BindingType.GetField("filePath").SetValue(binding, path);
            BindingType.GetField("semanticModel").SetValue(binding, model);
            var bindings = Array.CreateInstance(BindingType, 1);
            bindings.SetValue(binding, 0);
            ContextType.GetProperty("ModuleBindings").SetValue(context, bindings);

            var roots = Array.CreateInstance(TupleType, 1);
            roots.SetValue(Activator.CreateInstance(TupleType, root, binding), 0);
            try
            {
                inheritance.Invoke(context, new object[] { new[] { root } });
                bindAll.Invoke(null, new object[] { roots, context });
            }
            catch (TargetInvocationException e)
            {
                problems.Add(new CheckProblem { Line = 0, Column = 0, Length = 1, Error = true, Message = "UdonSharp's binder failed: " + e.InnerException?.Message });
                return problems;
            }

            Collect(context, tree, problems);
            if (!problems.Any(p => p.Error) && EmitAvailable) { Emit(context, root, binding, tree); Collect(context, tree, problems); }

            // Generic methods get past the binder and crash UdonSharp's emit with a NullReferenceException.
            if (!problems.Any(p => p.Error))
                foreach (var m in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => m.TypeParameterList != null))
                {
                    var pos = m.Identifier.GetLocation().GetLineSpan().StartLinePosition;
                    problems.Add(new CheckProblem { Start = m.Identifier.SpanStart, Length = m.Identifier.Span.Length, Line = pos.Line, Column = pos.Character, Error = true,
                        Message = "Generic methods are not supported by UdonSharp (its compile fails with an internal error)" });
                }
            return problems.GroupBy(p => (p.File, p.Start, p.Message)).Select(g => g.First()).OrderBy(p => p.File != null).ThenBy(p => p.Start).ToList();
        }

        /// <summary>Whether a program asset uses the script (main thread: asks the AssetDatabase).</summary>
        public static bool HasProgramAsset(string path) =>
            UdonSharp.UdonSharpProgramAsset.GetAllUdonSharpPrograms().Any(p => p != null && p.sourceCsScript != null && UnityEditor.AssetDatabase.GetAssetPath(p.sourceCsScript) == path);

        static void Collect(object context, SyntaxTree tree, List<CheckProblem> problems)
        {
            foreach (var d in (IEnumerable)ContextType.GetProperty("Diagnostics").GetValue(context))
            {
                var dt = d.GetType();
                var location = (Location)dt.GetProperty("Location").GetValue(d);
                var message = (string)dt.GetProperty("Message").GetValue(d);
                bool error = dt.GetProperty("Severity").GetValue(d).ToString() == "Error";
                var span = location?.SourceSpan ?? default;
                var pos = location != null ? location.GetLineSpan().StartLinePosition : default;
                var other = location?.SourceTree != null && location.SourceTree != tree ? ProjectPaths.Relative(location.SourceTree.FilePath) : null;
                problems.Add(new CheckProblem { Start = span.Start, Length = Math.Max(1, span.Length), Line = pos.Line, Column = pos.Character, Error = error, Message = Readable(message), File = other });
            }
        }

        /// <summary>Emit and assemble: sync checks, and what only the assembler sees (a field type Udon doesn't have).</summary>
        static void Emit(object context, INamedTypeSymbol root, object binding, SyntaxTree tree)
        {
            object emit = null;
            void Add(Location at, string message) =>
                addDiagnostic.Invoke(context, new object[] { Enum.Parse(SeverityType, "Error"), at, message });
            Location Current() => (EmitType.GetProperty("CurrentNode").GetValue(emit) as SyntaxNode)?.GetLocation();
            try
            {
                var module = Activator.CreateInstance(ModuleType, context);
                BindingType.GetField("assemblyModule").SetValue(binding, module);
                emit = Activator.CreateInstance(EmitType, module, root);
                EmitType.GetMethod("Emit", Type.EmptyTypes).Invoke(emit, null);
                foreach (var field in (IEnumerable)EmitType.GetProperty("DeclaredFields").GetValue(emit))
                    checkSync.Invoke(null, new[] { field, context, emit });
                if ((int)ContextType.GetProperty("ErrorCount").GetValue(context) > 0) return;
                var uasm = (string)ModuleType.GetMethod("BuildUasmStr").Invoke(module, null);
                var heap = (uint)ModuleType.GetMethod("GetHeapSize").Invoke(module, null);
                assemble.Invoke(null, new object[] { uasm, heap });
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                var inner = e.InnerException;
                var at = CompilerExceptionType.IsInstanceOfType(inner) ? (Location)CompilerExceptionType.GetProperty("Location").GetValue(inner) : null;
                Add(at ?? (emit != null ? Current() : null), CompilerExceptionType.IsInstanceOfType(inner) ? inner.Message : inner.ToString());
            }
        }

        /// <summary>A behaviour without a compiled C# type yet (new or renamed), which UdonSharp needs to bind.</summary>
        static void Uncompiled(SyntaxTree tree, SemanticModel model, string path, List<CheckProblem> problems)
        {
            foreach (var decl in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!(model.GetDeclaredSymbol(decl) is INamedTypeSymbol t) || t.IsAbstract) continue;
                bool behaviour = false;
                for (var b = t.BaseType; b != null; b = b.BaseType) if (b.ToDisplayString() == BehaviourTypeName) { behaviour = true; break; }
                if (!behaviour) continue;
                var pos = decl.Identifier.GetLocation().GetLineSpan().StartLinePosition;
                bool sameName = t.Name == System.IO.Path.GetFileNameWithoutExtension(path);
                problems.Add(new CheckProblem { Start = decl.Identifier.SpanStart, Length = decl.Identifier.Span.Length, Line = pos.Line, Column = pos.Character,
                    Error = !sameName,
                    Message = sameName ? "Unity hasn't compiled this class yet; UdonSharp's checks start once it is saved and compiled"
                                       : "UdonSharpBehaviour classes must have the same name as their containing .cs file" });
            }
        }
    }
}
