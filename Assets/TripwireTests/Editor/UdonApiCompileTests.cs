using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Tripwire.Core;
using Tripwire.Editor;
using NUnit.Framework;
using UdonSharp;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tripwire.Tests
{
    /// <summary>
    /// Every member the "Call Udon API" picker offers must compile, both as C# (Unity) and as UdonSharp.
    /// Generates calls with default arguments in chunks, recompiles, then U#-compiles them.
    /// </summary>
    public class UdonApiCompileTests
    {
        [Test]
        public void ConstantConstructorsThatUdonSharpCannotFoldAreReported()
        {
            UdonApi.InstallEditorKnowledge();
            CallSpec Ctor(string udonName) => UdonApi.Get(udonName);
            var dateTime = Ctor("SystemDateTime.__ctor__SystemInt32_SystemInt32_SystemInt32__SystemDateTime");
            var descriptor = Ctor("UnityEngineRenderTextureDescriptor.__ctor__SystemInt32_SystemInt32__UnityEngineRenderTextureDescriptor");
            Assert.IsNotNull(dateTime, "DateTime(int,int,int) offered");
            Assert.IsNotNull(descriptor, "RenderTextureDescriptor(int,int) offered");

            TriggerProgram Program(CallSpec c, params ArgValue[] args)
            {
                var p = new TriggerProgram();
                p.Variables.Add(new VariableDecl { Name = "r", Type = c.Returns });
                var e = new EventBlock { EventId = "Interact" };
                var call = new ActionCall { ActionId = ActionCatalog.CallId, Call = c, ResultVariable = "r" };
                call.Args.AddRange(args);
                e.Actions.Add(call);
                p.Events.Add(e);
                return p;
            }

            Assert.IsTrue(CodeGenerator.Generate(Program(dateTime, ArgValue.Const(0), ArgValue.Const(0), ArgValue.Const(0))).HasErrors, "invalid date");
            Assert.IsFalse(CodeGenerator.Generate(Program(dateTime, ArgValue.Const(2026), ArgValue.Const(10), ArgValue.Const(5))).HasErrors, "valid date");
            Assert.IsTrue(CodeGenerator.Generate(Program(descriptor, ArgValue.Const(256), ArgValue.Const(256))).HasErrors, "main-thread-only constructor");
        }

        const string Dir = "Assets/TripwireTests/Temp/ApiCompile";
        const int ChunkSize = 400;

        static ArgValue DefaultArg(ParamType type)
        {
            switch (type.Kind)
            {
                case ValueKind.Object: return ArgValue.Objs(1);
                case ValueKind.Player: return ArgValue.Local();
                case ValueKind.Bool: return ArgValue.Const(false);
                case ValueKind.Int: return ArgValue.Const(0);
                case ValueKind.Float: return ArgValue.Const(0f);
                case ValueKind.String: return ArgValue.Const("");
                case ValueKind.Vector3: return ArgValue.Const(new float[3]);
                case ValueKind.Vector2: return ArgValue.Const(new float[2]);
                case ValueKind.Color: return ArgValue.Const(new float[4]);
                case ValueKind.Quaternion: return ArgValue.Const(new float[3]);
                case ValueKind.Enum: return ArgValue.Const(UdonApi.EnumMembers(TripwireModel.ResolveType(type.UnityType)).First());
                case ValueKind.Url: return ArgValue.Const("https://example.com/");
                default: throw new ArgumentException(type.ToString());
            }
        }

        static readonly Dictionary<ValueKind, string> ResultVars = new Dictionary<ValueKind, string>
        {
            { ValueKind.Bool, "vb" }, { ValueKind.Int, "vi" }, { ValueKind.Float, "vf" }, { ValueKind.String, "vs" }, { ValueKind.Vector3, "v3" },
        };

        static void WriteChunks()
        {
            if (Directory.Exists(Dir)) Directory.Delete(Dir, true);
            Directory.CreateDirectory(Dir);
            UdonApi.InstallEditorKnowledge(); // also installs the reflection type check the editor uses
            var all = UdonApi.All;
            Debug.Log("[TripwireTest] offered Udon API members: " + all.Count);
            for (int start = 0; start < all.Count; start += ChunkSize)
            {
                var p = new TriggerProgram();
                // One variable per distinct type: values without a literal form are passed through variables, and
                // every return value is stored in a variable of exactly its type.
                var varOf = new Dictionary<string, string>();
                string VarFor(ParamType type)
                {
                    var key = CodeGenerator.FullTypeName(type);
                    string name;
                    if (!varOf.TryGetValue(key, out name))
                    {
                        name = "t" + varOf.Count;
                        varOf.Add(key, name);
                        p.Variables.Add(new VariableDecl { Name = name, Type = type });
                    }
                    return name;
                }
                ArgValue Arg(ParamType type) => type.Kind == ValueKind.Other ? ArgValue.Var(VarFor(type)) : DefaultArg(type);

                var e = new EventBlock { EventId = "Interact" };
                foreach (var c in all.Skip(start).Take(ChunkSize))
                {
                    var call = new ActionCall { ActionId = ActionCatalog.CallId, Call = c };
                    // Constructors: arguments from variables, so U# does not fold them with placeholder zeros
                    // (folding with real values is checked when generating; see UdonApi.CheckConstantConstruction).
                    int k = 0, first = c.Instance != null ? 1 : 0;
                    foreach (var type in TripwireModel.CallArgTypes(c))
                    {
                        int pi = k++ - first;
                        if (pi >= 0 && c.Params[pi].Receives)
                        {
                            // out: a variable of its own (the result may have the same type, and can't share one)
                            var outName = "o" + p.Variables.Count;
                            p.Variables.Add(new VariableDecl { Name = outName, Type = type });
                            call.Args.Add(ArgValue.Var(outName));
                            continue;
                        }
                        // A struct's setter or method works on a variable (stored back after the call).
                        bool structTarget = pi < 0 && c.InstanceIsStruct;
                        call.Args.Add(structTarget || c.Kind == CallKind.Ctor && type.Kind != ValueKind.Object && type.Kind != ValueKind.Player ? ArgValue.Var(VarFor(type)) : Arg(type));
                    }
                    if (c.Returns != null) call.ResultVariable = VarFor(c.Returns);
                    e.Actions.Add(call);
                }
                p.Events.Add(e);
                var g = CodeGenerator.Generate(p);
                Assert.IsFalse(g.HasErrors, string.Join("\n", g.Diagnostics.Where(d => d.Severity == Severity.Error).Take(20)));
                File.WriteAllText(Dir + "/" + g.ClassName + ".cs", g.Source);
            }
            WriteEveryEvent();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// One trigger with every Udon / Unity event of the catalog, each storing its values in variables of their
        /// types, plus a synced variable (its OnDeserialization shares the method with the event block).
        /// </summary>
        static void WriteEveryEvent()
        {
            var p = new TriggerProgram();
            p.Variables.Add(new VariableDecl { Name = "synced", Kind = ValueKind.Int, Initial = 0, Synced = true });
            int n = 0;
            foreach (var spec in EventCatalog.All.Where(x => x.Shape == EventShape.Override || x.Shape == EventShape.UnityMessage))
            {
                var e = new EventBlock { EventId = spec.Id };
                e.Actions.Add(new ActionCall { ActionId = "Debug.Log", Args = { ArgValue.Const(spec.Id) } });
                foreach (var ep in spec.Params)
                {
                    var name = "e" + n++;
                    p.Variables.Add(new VariableDecl { Name = name, Type = ep.Type });
                    e.Actions.Add(new ActionCall { ActionId = "Variable.Set", Args = { ArgValue.Const(name), ArgValue.Param(ep.Name) } });
                }
                p.Events.Add(e);
            }
            var g = CodeGenerator.Generate(p);
            Assert.IsFalse(g.HasErrors, string.Join("\n", g.Diagnostics.Where(d => d.Severity == Severity.Error).Take(20)));
            File.WriteAllText(Dir + "/" + g.ClassName + ".cs", g.Source);
        }

        static List<string> CompileAll(out List<string> report)
        {
            var scripts = Directory.GetFiles(Dir, "*.cs").Select(x => x.Replace('\\', '/')).ToList();
            Assert.IsNotEmpty(scripts);
            foreach (var cs in scripts)
            {
                var assetPath = Path.ChangeExtension(cs, ".asset");
                if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(assetPath) != null) continue;
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(cs);
                Assert.IsNotNull(script, cs);
                Assert.IsNotNull(script.GetClass(), "Unity compiled " + cs);
                var programAsset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                programAsset.sourceCsScript = script;
                programAsset.ScriptVersion = UdonSharpProgramVersion.CurrentVersion;
                AssetDatabase.CreateAsset(programAsset, assetPath);
            }
            AssetDatabase.SaveAssets();

            var errors = new List<string>();
            Application.LogCallback collect = (message, stack, type) =>
            {
                if ((type == LogType.Error || type == LogType.Exception) && message.Contains("ApiCompile")) errors.Add(message);
            };
            Application.logMessageReceived += collect;
            try { UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync(); }
            finally { Application.logMessageReceived -= collect; }

            report = errors.Take(40).Select(Describe).ToList();
            return errors;
        }

        static string Describe(string msg)
        {
            // Report each failing line with the generated statement, so the offending member is visible.
            var m = Regex.Match(msg, @"(Assets/TripwireTests/Temp/ApiCompile/[^\s(]+\.cs)\((\d+),");
            if (!m.Success) return msg;
            var lines = File.ReadAllLines(m.Groups[1].Value);
            int line = int.Parse(m.Groups[2].Value);
            return msg.Split('\n')[0] + "\n    >> " + (line - 1 < lines.Length ? lines[line - 1].Trim() : "?");
        }


        [UnityTest]
        public IEnumerator EveryOfferedMemberCompilesAsUdonSharp()
        {
            LogAssert.ignoreFailingMessages = true;
            if (!Directory.Exists(Dir) || Directory.GetFiles(Dir, "*.cs").Length == 0) WriteChunks();
            yield return new RecompileScripts(false);
            LogAssert.ignoreFailingMessages = true;

            var errors = CompileAll(out var report);
            File.WriteAllLines("Logs/api-compile-errors.txt", errors);
            Assert.IsEmpty(errors, errors.Count + " U# errors (all in Logs/api-compile-errors.txt):\n" + string.Join("\n", report));
            Assert.IsFalse(UdonSharpProgramAsset.AnyUdonSharpScriptHasError());
        }
    }
}
