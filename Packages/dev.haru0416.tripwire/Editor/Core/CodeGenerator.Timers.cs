using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tripwire.Core
{
    // Timer events: scheduling, stale ticks, starting at load.
    public static partial class CodeGenerator
    {
        sealed partial class Generator
        {
            IEnumerable<int> TimerEvents() => Enumerable.Range(0, p.Events.Count).Where(i => p.Events[i].EventId == EventCatalog.TimerId && p.Events[i].Timer != null);
            /// <summary>Timers that start at load; only those whose event resolved (the others are errors and get no methods).</summary>
            IEnumerable<int> TimersToStart(EventSpec[] specs) => TimerEvents().Where(i => p.Events[i].Timer.AutoStart && specs[i] != null);

            /// <summary>
            /// Timer events: a running flag (Stop Timer clears it) and the time the next run is due. Every (re)start
            /// schedules a tick and moves the due time; a tick that arrives before the due time is an older one, made
            /// stale by a restart, and does nothing. So a restart always waits a full interval, and a tick that never
            /// arrives can't leave the timer stuck.
            /// </summary>
            void EmitTimers(EventSpec[] specs)
            {
                foreach (var i in TimerEvents())
                {
                    if (specs[i] == null) continue;
                    var tm = p.Events[i].Timer;
                    fields.Append("        bool tw_TimerOn").Append(i).Append(";\n        float tw_TimerDue").Append(i).Append(";\n");
                    var delay = tm.MaxSeconds > tm.MinSeconds
                        ? "UnityEngine.Random.Range(" + ConstantExpr(ValueKind.Float, tm.MinSeconds, "Timer" + i + "_min") + ", " + ConstantExpr(ValueKind.Float, tm.MaxSeconds, "Timer" + i + "_max") + ")"
                        : ConstantExpr(ValueKind.Float, tm.MinSeconds, "Timer" + i + "_min");
                    methods.Append("        void Tw_Schedule").Append(i).Append("()\n        {\n")
                        .Append("            float tw_D = ").Append(delay).Append(";\n")
                        .Append("            tw_TimerDue").Append(i).Append(" = UnityEngine.Time.time + tw_D;\n")
                        .Append("            SendCustomEventDelayedSeconds(\"_Tw_T").Append(i).Append("\", tw_D);\n        }\n\n");
                    // A small tolerance: the scheduler may run a tick a hair before Time.time reaches the due time. At most half
                    // the shortest interval, so a stale tick of a very fast timer (under 0.04 s) can't pass for a current one.
                    var tolerance = ConstantExpr(ValueKind.Float, Math.Min(0.02f, tm.MinSeconds * 0.5f), "Timer" + i + "_tolerance");
                    methods.Append("        public void _Tw_T").Append(i).Append("()\n        {\n")
                        .Append("            if (!tw_TimerOn").Append(i).Append(" || UnityEngine.Time.time < tw_TimerDue").Append(i).Append(" - ").Append(tolerance).Append(") return;\n")
                        .Append(tm.Repeat ? "            Tw_Schedule" + i + "();\n" : "            tw_TimerOn" + i + " = false;\n")
                        .Append("            ").Append(Dispatch(i)).Append("\n        }\n\n");
                }
                var start = TimersToStart(specs).ToList();
                if (start.Count == 0) return;
                methods.Append("        void Tw_StartTimers()\n        {\n");
                foreach (var i in start) methods.Append("            tw_TimerOn").Append(i).Append(" = true;\n            Tw_Schedule").Append(i).Append("();\n");
                methods.Append("        }\n\n");
            }
        }
    }
}
