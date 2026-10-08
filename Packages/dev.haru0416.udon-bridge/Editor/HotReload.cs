using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace UdonBridge
{
    /// <summary>
    /// Hot reload in play mode: recompile U# only and put the new programs into the running UdonBehaviours, keeping
    /// field values. In play mode only the Udon programs run, so the C# side can stay stale until play ends.
    /// </summary>
    public static class HotReload
    {
        public sealed class Result
        {
            public bool Compiled;
            public int Swapped, Kept, Dropped;
            public double CompileMs, SwapMs;
            public List<string> Notes = new List<string>();
        }

        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        static readonly FieldInfo programField = typeof(UdonBehaviour).GetField("_program", Private);
        static readonly FieldInfo vmField = typeof(UdonBehaviour).GetField("_udonVM", Private);
        static readonly FieldInfo initializedField = typeof(UdonBehaviour).GetField("_initialized", Private);
        static readonly FieldInfo changeEventsField = typeof(UdonBehaviour).GetField("_variableToChangeEvent", Private);
        static readonly MethodInfo unregisterUpdate = typeof(UdonBehaviour).GetMethod("UnregisterUpdate", Private);
        static readonly MethodInfo registerUpdate = typeof(UdonBehaviour).GetMethod("RegisterUpdate", Private);
        static readonly FieldInfo hasErrorField = typeof(UdonBehaviour).GetField("_hasError", Private);
        static readonly MethodInfo processEntryPoints = typeof(UdonBehaviour).GetMethod("ProcessEntryPoints", Private);
        // The compiled program as stored (code and heap constants alike: the assembly text alone misses changed numbers).
        static readonly FieldInfo bytesField = typeof(VRC.Udon.ProgramSources.SerializedUdonProgramAsset).GetField("serializedProgramCompressedBytes", Private);

        public static bool Available => programField != null && vmField != null && initializedField != null && changeEventsField != null
                                        && unregisterUpdate != null && registerUpdate != null && hasErrorField != null && processEntryPoints != null && bytesField != null;

        static string Fingerprint(UdonSharpProgramAsset p)
        {
            var bytes = p.SerializedProgramAsset is VRC.Udon.ProgramSources.SerializedUdonProgramAsset s ? (byte[])bytesField.GetValue(s) : null;
            return bytes == null ? "" : Convert.ToBase64String(System.Security.Cryptography.SHA1.Create().ComputeHash(bytes));
        }

        static Dictionary<UdonSharpProgramAsset, string> running;
        static readonly Dictionary<UdonBehaviour, bool> enabledAtStart = new Dictionary<UdonBehaviour, bool>();

        /// <summary>For tests.</summary>
        public static int Attempts;
        public static Result LastResult;

        public static void Remember()
        {
            running = UdonSharpProgramAsset.GetAllUdonSharpPrograms().ToDictionary(p => p, Fingerprint);
            enabledAtStart.Clear();
            foreach (var ub in UnityEngine.Object.FindObjectsOfType<UdonBehaviour>(true)) enabledAtStart[ub] = ub.enabled;
        }

        public static Result Apply()
        {
            Attempts++;
            var result = LastResult = new Result();
            if (!Available) { result.Notes.Add("this SDK's UdonBehaviour has changed: hot reload is off (stop play mode to see changes)"); return result; }
            var programs = UdonSharpProgramAsset.GetAllUdonSharpPrograms();
            if (running == null) Remember();

            var watch = Stopwatch.StartNew();
            UdonSharpCompilerV1.CompileSync();
            result.CompileMs = watch.Elapsed.TotalMilliseconds;
            if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) { result.Notes.Add("U# compile errors: nothing swapped"); return result; }
            result.Compiled = true;

            // Against what the behaviours run, not the last compile: UdonSharp's own watcher may have compiled the change already.
            var changed = new HashSet<UdonSharpProgramAsset>(programs.Where(p => !running.TryGetValue(p, out var h) || h != Fingerprint(p)));
            var failed = new HashSet<UdonSharpProgramAsset>();
            watch.Restart();
            foreach (var ub in UnityEngine.Object.FindObjectsOfType<UdonBehaviour>(true))
            {
                if (!(ub.programSource is UdonSharpProgramAsset p) || !changed.Contains(p)) continue;
                try
                {
                    var outcome = Swap(ub, p, result);
                    if (outcome == Outcome.Swapped) result.Swapped++;
                    else if (outcome == Outcome.Failed) failed.Add(p);
                }
                catch (Exception e)
                {
                    failed.Add(p);
                    result.Notes.Add(ub.name + ": " + e.GetBaseException().Message);
                }
            }
            // A program counts as running only once every behaviour has it: a failed one is tried again next time.
            foreach (var p in changed) if (!failed.Contains(p)) running[p] = Fingerprint(p);
            result.SwapMs = watch.Elapsed.TotalMilliseconds;
            return result;
        }

        enum Outcome { Swapped, Skipped, Failed }

        /// <summary>Puts the program's new version into a running behaviour; field values carry over by name and type.</summary>
        static Outcome Swap(UdonBehaviour ub, UdonSharpProgramAsset asset, Result result)
        {
            var old = (IUdonProgram)programField.GetValue(ub);
            if (old == null || !(bool)initializedField.GetValue(ub)) return Outcome.Skipped; // not started yet: it loads the new program itself

            // UdonManager sorts Update by the program's order: a changed order would register the behaviour twice.
            var next = asset.SerializedProgramAsset?.RetrieveProgram();
            if (next != null && next.UpdateOrder != old.UpdateOrder)
            {
                result.Notes.Add(ub.name + ": its execution order changed; stop play mode to apply that");
                return Outcome.Failed;
            }

            // Field values. U#'s own symbols (temporaries, constants, this) start with "__" and come fresh with the new program.
            var keep = new Dictionary<string, (object value, Type type)>();
            foreach (var s in old.SymbolTable.GetSymbols())
            {
                if (s.StartsWith("__")) continue;
                uint at = old.SymbolTable.GetAddressFromSymbol(s);
                keep[s] = (old.Heap.GetHeapVariable(at), old.Heap.GetHeapVariableType(at));
            }

            // Undo what the old program registered (as OnDestroy does, without running _onDestroy).
            bool halted = (bool)hasErrorField.GetValue(ub);
            bool wasEnabled = ub.enabled;
            var oldVm = vmField.GetValue(ub);
            unregisterUpdate.Invoke(ub, null);
            foreach (var entry in old.EntryPoints.GetExportedSymbols()) UdonManager.Instance.RegisterInput(ub, entry, false);
            ((System.Collections.IDictionary)changeEventsField.GetValue(ub)).Clear();
            programField.SetValue(ub, null);
            vmField.SetValue(ub, null);
            initializedField.SetValue(ub, false);

            IUdonProgram fresh = null;
            try
            {
                ub.InitializeUdonContent();
                fresh = (IUdonProgram)programField.GetValue(ub);
                // A failed start still sets the program, and flags an error that must not pass for the old halt.
                bool failed = fresh == null || !(bool)initializedField.GetValue(ub) || (!halted && (bool)hasErrorField.GetValue(ub));
                if (failed) throw new InvalidOperationException("the new program didn't start");
            }
            catch (Exception e)
            {
                hasErrorField.SetValue(ub, halted);
                programField.SetValue(ub, old);
                vmField.SetValue(ub, oldVm);
                initializedField.SetValue(ub, true);
                ((System.Collections.IDictionary)changeEventsField.GetValue(ub)).Clear();
                processEntryPoints.Invoke(ub, null);
                if (ub.enabled != wasEnabled) ub.enabled = wasEnabled; // a failed start switches it off
                result.Notes.Add(ub.name + ": " + e.GetBaseException().Message + "; it keeps the old code");
                return Outcome.Failed;
            }
            registerUpdate.Invoke(ub, null);

            foreach (var kv in keep)
            {
                if (!fresh.SymbolTable.TryGetAddressFromSymbol(kv.Key, out uint at)) { result.Dropped++; continue; }
                if (fresh.Heap.GetHeapVariableType(at) != kv.Value.type) { result.Dropped++; result.Notes.Add(ub.name + "." + kv.Key + ": type changed, starts fresh"); continue; }
                fresh.Heap.SetHeapVariable(at, kv.Value.value, kv.Value.type);
                result.Kept++;
            }

            // Udon never clears a halt; the fix was just saved. Switched on only if it was on at the start of play
            // (some behaviours stay off on purpose and run by SendCustomEvent), after the values are back.
            if (halted)
            {
                hasErrorField.SetValue(ub, false);
                if (!ub.enabled && enabledAtStart.TryGetValue(ub, out var wasOn) && wasOn) ub.enabled = true;
                result.Notes.Add(ub.name + ": had stopped on an error; running again" + (ub.enabled ? "" : " (it was switched off at the start, so it stays off)"));
            }
            return Outcome.Swapped;
        }
    }
}
