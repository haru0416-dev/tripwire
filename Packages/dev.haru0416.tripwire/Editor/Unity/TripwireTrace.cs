using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Tripwire.Core;
using UnityEditor;
using UnityEngine;
using VRC.Udon;

namespace Tripwire.Editor
{
    /// <summary>
    /// The event history during Play: turns on each trigger's notes (CodeGenerator.Trace), reads them every editor frame
    /// and keeps them, worded, for the history window and the cards in the Inspector. Cleared when Play starts again.
    /// </summary>
    [InitializeOnLoad]
    internal static class TripwireTrace
    {
        /// <summary>One event that ran, or that its conditions stopped.</summary>
        internal sealed class Record
        {
            public float Time;
            public TripwireTrigger Trigger;
            /// <summary>Where the trigger is (its hierarchy path): it stays readable after Play, when the object is gone.</summary>
            public string Path;
            public int Event;
            public bool Ran;
            public string EventName;
            /// <summary>What stopped it, or the new value of a change event; empty when there is nothing to add.</summary>
            public string Detail;
        }

        const int Ring = 64, Kept = 1000;
        static readonly List<Record> records = new List<Record>();
        static readonly Dictionary<UdonBehaviour, int> read = new Dictionary<UdonBehaviour, int>();
        static readonly Dictionary<(int trigger, int ev), (Record last, int runs)> perEvent = new Dictionary<(int, int), (Record, int)>();
        static TripwireTrigger[] triggers = new TripwireTrigger[0];
        static double nextScan;

        /// <summary>Notes that came faster than the editor read them (more than the ring holds between two frames).</summary>
        internal static int Missed { get; private set; }

        internal static IReadOnlyList<Record> Records => records;
        internal static event Action Changed;

        static TripwireTrace()
        {
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode) return;
                read.Clear(); // new behaviours, each with a fresh ring
                Clear();
            };
        }

        /// <summary>Forgets the history (the "Clear" button). What the behaviours already noted stays read, not shown again.</summary>
        internal static void Clear()
        {
            records.Clear();
            perEvent.Clear();
            Missed = 0;
            nextScan = 0;
            Changed?.Invoke();
        }

        /// <summary>The last record of a trigger's event during this Play, and how many times it ran.</summary>
        internal static (Record last, int runs) Of(TripwireTrigger t, int ev) =>
            t != null && perEvent.TryGetValue((t.GetInstanceID(), ev), out var x) ? x : (null, 0);

        /// <summary>
        /// A value changed by hand during Play, as if an action had set it: a synced one is taken over and sent, and a
        /// watched one runs its On Variable Changed events. Nothing happens when the value is the same.
        /// </summary>
        internal static void SetValue(TripwireTrigger t, KVariable v, object value)
        {
            var ub = t != null ? t.generated : null;
            var field = CodeGenerator.FieldOf(v.name);
            if (ub == null || v.temporary || !ub.TryGetProgramVariable(field, out object before) || Equals(before, value)) return;
            if (v.synced && !VRC.SDKBase.Networking.IsOwner(ub.gameObject)) VRC.SDKBase.Networking.SetOwner(VRC.SDKBase.Networking.LocalPlayer, ub.gameObject);
            ub.SetProgramVariable(field, value);
            if (v.synced)
            {
                // Received sync compares with the last value it knew: this one is already known.
                var prev = CodeGenerator.PrevFieldOf(v.name);
                if (ub.TryGetProgramVariable(prev, out object _)) ub.SetProgramVariable(prev, value);
                ub.RequestSerialization();
            }
            if (t.events.Any(e => e.eventId == EventCatalog.VariableChangedId && e.name == v.name))
                ub.SendCustomEvent(CodeGenerator.ChangedMethodOf(v.name));
        }

        /// <summary>Reads the notes now (tests; the editor reads them every frame).</summary>
        internal static void PollNow() { nextScan = 0; Poll(); }

        static void Poll()
        {
            if (!EditorApplication.isPlaying) return;
            // Triggers can appear during Play (a spawned prefab): looked for again every second.
            if (EditorApplication.timeSinceStartup >= nextScan)
            {
                triggers = UnityEngine.Object.FindObjectsOfType<TripwireTrigger>();
                nextScan = EditorApplication.timeSinceStartup + 1;
            }
            bool added = false;
            foreach (var t in triggers)
            {
                var ub = t != null ? t.generated : null;
                if (ub == null || !ub.TryGetProgramVariable(CodeGenerator.TraceFlag, out bool on)) continue; // nothing noted in this program
                if (!on)
                {
                    // Off until now, or reset by the behaviour's own start-up: (again) a fresh ring, then on.
                    ub.SetProgramVariable(CodeGenerator.TraceLog, new string[Ring]);
                    ub.SetProgramVariable(CodeGenerator.TraceCount, 0);
                    ub.SetProgramVariable(CodeGenerator.TraceFlag, true);
                    read[ub] = 0;
                    continue;
                }
                if (!ub.TryGetProgramVariable(CodeGenerator.TraceCount, out int count)) continue;
                // On, but not known here (scripts reloaded during Play): earlier notes count as read, not as new.
                if (!read.TryGetValue(ub, out int done)) { read[ub] = count; continue; }
                if (count <= done || !ub.TryGetProgramVariable(CodeGenerator.TraceLog, out string[] ring) || ring == null) continue;
                if (count - done > ring.Length) { Missed += count - done - ring.Length; done = count - ring.Length; }
                for (int k = done; k < count; k++) Add(t, CodeGenerator.ParseTrace(ring[k % ring.Length]));
                read[ub] = count;
                added = true;
            }
            if (added) Changed?.Invoke();
        }

        static void Add(TripwireTrigger t, CodeGenerator.TraceEntry note)
        {
            if (note == null || note.Event < 0 || note.Event >= t.events.Count) return;
            var e = t.events[note.Event];
            var spec = EventCatalog.Get(e.eventId);
            var record = new Record
            {
                Time = Time.time,
                Trigger = t,
                Path = PathOf(t.transform),
                Event = note.Event,
                Ran = note.Ran,
                EventName = (spec != null ? Texts.EventName(spec) : e.eventId) + (spec?.UsesName == true && !string.IsNullOrEmpty(e.name) ? "  —  " + e.name : ""),
                Detail = note.Ran ? ChangedValue(t, note) : WhyStopped(t, e, note),
            };
            records.Add(record);
            if (records.Count > Kept) records.RemoveRange(0, records.Count - Kept);
            var key = (t.GetInstanceID(), note.Event);
            perEvent.TryGetValue(key, out var before);
            perEvent[key] = (record, before.runs + (note.Ran ? 1 : 0));
        }

        static string PathOf(Transform tr) => tr.parent == null ? tr.name : PathOf(tr.parent) + "/" + tr.name;

        static string ChangedValue(TripwireTrigger t, CodeGenerator.TraceEntry note) =>
            note.Values.Count == 0 ? "" : string.Join(Texts.T(", ", "、"), note.Values.Select(v => v.Key + " = " + Shown(t, v.Key, v.Value)));

        /// <summary>
        /// "回数 が 3 より小さい（回数 = 3）": the conditions that didn't hold, with the values they had. A condition this
        /// can't check (a value from elsewhere) is listed too, as a possible cause.
        /// </summary>
        static string WhyStopped(TripwireTrigger t, KEvent e, CodeGenerator.TraceEntry note)
        {
            var values = note.Values.GroupBy(v => v.Key).ToDictionary(g => g.Key, g => g.First().Value);
            var failed = e.conditions.Where(c => Holds(t, c, values) != true).ToList();
            if (failed.Count == 0) failed = e.conditions;
            var said = string.Join(e.conditionsMatchAny ? Texts.T(" or ", "・") : Texts.T(" and ", "・"), failed.Select(c => Describe(t, c)));
            var shown = note.Values.Count == 0 ? "" : Texts.T(" (", "（") + ChangedValue(t, note) + Texts.T(")", "）");
            return Texts.T("Condition not met: ", "条件を満たさなかった: ") + said + shown;
        }

        /// <summary>Whether a condition held with the noted values; null when it can't be told here.</summary>
        static bool? Holds(TripwireTrigger t, KCondition c, Dictionary<string, string> values)
        {
            var type = TripwireModel.VariableType(t, c.variable);
            if (type == null || type.IsArray || !values.TryGetValue(c.variable ?? "", out var raw)) return null;
            int compare;
            switch (type.Kind)
            {
                case ValueKind.Bool:
                    if (c.value.source != KArgSource.Constant || !bool.TryParse(raw, out bool b)) return null;
                    compare = b == c.value.boolValue ? 0 : 1;
                    break;
                case ValueKind.Int:
                case ValueKind.Float:
                    if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double n)) return null;
                    double other;
                    if (c.value.source == KArgSource.Constant) other = type.Kind == ValueKind.Int ? c.value.intValue : c.value.floatValue;
                    else if (c.value.source == KArgSource.Variable && values.TryGetValue(c.value.name ?? "", out var o) && double.TryParse(o, NumberStyles.Float, CultureInfo.InvariantCulture, out double ov)) other = ov;
                    else return null;
                    compare = n.CompareTo(other);
                    break;
                case ValueKind.String:
                    if (c.value.source != KArgSource.Constant) return null;
                    compare = string.CompareOrdinal(raw, c.value.stringValue ?? "") == 0 ? 0 : 1;
                    break;
                default:
                    return null;
            }
            bool holds;
            switch (c.op)
            {
                case KCompareOp.NotEqual: holds = compare != 0; break;
                case KCompareOp.Less: holds = compare < 0; break;
                case KCompareOp.LessOrEqual: holds = compare <= 0; break;
                case KCompareOp.Greater: holds = compare > 0; break;
                case KCompareOp.GreaterOrEqual: holds = compare >= 0; break;
                default: holds = compare == 0; break;
            }
            return holds != c.negate;
        }

        static readonly string[] OpsJa = { "と同じ", "と違う", "より小さい", "以下", "より大きい", "以上" };
        static readonly string[] OpsEn = { "equals", "differs from", "less than", "at most", "greater than", "at least" };

        /// <summary>A condition as the Inspector reads it: "回数 が 3 より小さい", "鍵 が オン でない".</summary>
        internal static string Describe(TripwireTrigger t, KCondition c)
        {
            var type = TripwireModel.VariableType(t, c.variable);
            string not = c.negate ? Texts.T(" (not)", " でない") : "";
            if (type != null && type.Kind == ValueKind.Bool && !type.IsArray)
                return c.variable + Texts.T(" is ", " が ") + OnOff(c.value.boolValue) + not;
            string value;
            switch (c.value.source)
            {
                case KArgSource.Variable: value = c.value.name; break;
                case KArgSource.Constant:
                    value = type == null ? c.value.stringValue
                          : type.Kind == ValueKind.Int ? c.value.intValue.ToString(CultureInfo.InvariantCulture)
                          : type.Kind == ValueKind.Float ? c.value.floatValue.ToString("0.###", CultureInfo.InvariantCulture)
                          : "\"" + c.value.stringValue + "\"";
                    break;
                default: value = "…"; break;
            }
            int op = Math.Max(0, Math.Min((int)c.op, OpsJa.Length - 1));
            return Texts.Japanese ? c.variable + " が " + value + " " + OpsJa[op] + not : c.variable + " " + OpsEn[op] + " " + value + not;
        }

        static string OnOff(bool b) => b ? Texts.T("On", "オン") : Texts.T("Off", "オフ");

        /// <summary>A noted value as the Inspector shows it (on/off for bools, the name of an object).</summary>
        static string Shown(TripwireTrigger t, string variable, string raw)
        {
            var type = TripwireModel.VariableType(t, variable);
            if (type != null && type.Kind == ValueKind.Bool && !type.IsArray && bool.TryParse(raw, out bool b)) return OnOff(b);
            if (raw.Length == 0) return Texts.T("(none)", "（なし）");
            // Objects note as "Name (UnityEngine.GameObject)": the name is enough.
            int type0 = raw.LastIndexOf(" (", StringComparison.Ordinal);
            return type0 > 0 && raw.EndsWith(")") && raw.IndexOf('.', type0) > 0 ? raw.Substring(0, type0) : raw;
        }
    }
}
