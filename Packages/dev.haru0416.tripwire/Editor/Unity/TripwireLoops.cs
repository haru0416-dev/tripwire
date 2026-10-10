using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tripwire.Editor
{
    /// <summary>
    /// Loops that pass through other triggers: Send Event with objects dragged in as targets (other triggers, or this
    /// trigger's own object), which the generator can't see. The trigger's own loops are found by the generator.
    /// </summary>
    internal static class TripwireLoops
    {
        sealed class Link { public int From, To, Act; public LinkKind Kind; public bool Across; }

        /// <summary>Adds a warning to <paramref name="g"/> for each loop through another trigger that this trigger is part of.</summary>
        public static void AddSceneWarnings(TripwireTrigger t, GeneratedProgram g)
        {
            // Cheap way out: no Send Event with dragged-in targets, no loop through other triggers.
            if (!t.events.Any(e => TripwireModel.FlatActions(e.actions).Any(a => SendsToObjects(a) && Targets(a).Any()))) return;

            // Nodes are (trigger, block); only triggers reachable from this one are read.
            var triggers = new List<TripwireTrigger>();
            var programs = new List<TriggerProgram>();
            var nodes = new List<(int trigger, int block)>();
            var links = new List<Link>();
            int Trigger(TripwireTrigger x)
            {
                int i = triggers.IndexOf(x);
                if (i >= 0) return i;
                triggers.Add(x);
                programs.Add(TripwireModel.ToProgram(x));
                return triggers.Count - 1;
            }
            int Node(int trigger, int block)
            {
                int i = nodes.IndexOf((trigger, block));
                if (i >= 0) return i;
                nodes.Add((trigger, block));
                return nodes.Count - 1;
            }

            var queue = new Queue<int>();
            queue.Enqueue(Trigger(t));
            var done = new HashSet<int>();
            while (queue.Count > 0)
            {
                int ti = queue.Dequeue();
                if (!done.Add(ti)) continue;
                var trigger = triggers[ti];
                foreach (var l in CodeGenerator.EventLinks(programs[ti]))
                    links.Add(new Link { From = Node(ti, l.From), To = Node(ti, l.To), Act = l.Act, Kind = l.Kind });
                for (int bi = 0; bi < trigger.events.Count; bi++)
                {
                    var flat = TripwireModel.FlatActions(trigger.events[bi].actions);
                    for (int k = 0; k < flat.Count; k++)
                    {
                        var a = flat[k];
                        if (!SendsToObjects(a)) continue;
                        var names = a.args.Count < 2 ? new List<string>()
                            : a.actionId == ActionCatalog.SendRandomEventId ? CodeGenerator.RandomEventNames(a.args[1].stringValue) : new List<string> { a.args[1].stringValue };
                        names.RemoveAll(string.IsNullOrEmpty);
                        if (names.Count == 0) continue;
                        var how = a.actionId == ActionCatalog.SendEventDelayedId ? LinkKind.Delayed
                            : a.args.Count > 2 && a.args[2].intValue != 0 ? LinkKind.Network : LinkKind.Immediate;
                        foreach (var target in Targets(a))
                        {
                            int ui = Trigger(target);
                            queue.Enqueue(ui);
                            for (int bj = 0; bj < target.events.Count; bj++)
                            {
                                var e = target.events[bj];
                                if (e.eventId != EventCatalog.CustomId || !names.Contains(e.name)) continue;
                                var kind = how == LinkKind.Network || e.broadcast != KBroadcast.Local ? LinkKind.Network
                                    : how == LinkKind.Delayed || e.delaySeconds > 0f ? LinkKind.Delayed : LinkKind.Immediate;
                                links.Add(new Link { From = Node(ti, bi), To = Node(ui, bj), Act = k, Kind = kind, Across = true });
                            }
                        }
                    }
                }
            }

            string Name(int n)
            {
                var (ti, bi) = nodes[n];
                var e = triggers[ti].events[bi];
                var spec = EventCatalog.Get(e.eventId);
                var title = (spec != null ? Texts.EventName(spec) : e.eventId) + (string.IsNullOrEmpty(e.name) ? "" : Texts.T(" (" + e.name + ")", "「" + e.name + "」"));
                return triggers[ti].name + Texts.T(": ", " の ") + title;
            }

            var reported = new HashSet<string>();
            void Report(LinkKind[] kinds, System.Func<string, bool, string> en, System.Func<string, bool, string> ja)
            {
                // Each node's next nodes, listed once (the search asks at every step).
                var next = Enumerable.Range(0, nodes.Count).Select(_ => new List<int>()).ToArray();
                foreach (var l in links)
                    if (kinds.Contains(l.Kind) && !next[l.From].Contains(l.To)) next[l.From].Add(l.To);
                var cycles = CodeGenerator.Cycles(Enumerable.Range(0, nodes.Count), n => next[n]);
                foreach (var c in cycles)
                {
                    // Only loops through another trigger or a dragged-in target, and only those this trigger is in.
                    var steps = c.Select((n, i) => links.First(l => l.From == n && l.To == c[(i + 1) % c.Count] && kinds.Contains(l.Kind))).ToList();
                    if (!steps.Any(s => s.Across)) continue;
                    int at = c.FindIndex(n => nodes[n].trigger == 0);
                    if (at < 0) continue;
                    var key = string.Join(">", c.OrderBy(n => n));
                    if (!reported.Add(key)) continue;
                    var rotated = c.Skip(at).Concat(c.Take(at)).ToList();
                    var path = string.Join(" → ", rotated.Concat(new[] { rotated[0] }).Select(Name));
                    bool onlyThis = c.All(n => nodes[n].trigger == 0);
                    g.Diagnostics.Add(new Diagnostic { Severity = Severity.Warning, Message = Texts.T(en(path, onlyThis), ja(path, onlyThis)), Event = nodes[c[at]].block, Action = steps[at].Act });
                }
            }
            Report(new[] { LinkKind.Immediate },
                (p, one) => "These run each other right away without end: " + p + ". Unless a condition stops it, Udon stops " + (one ? "this trigger" : "these triggers") + ". Delay one of them, or add a condition.",
                (p, one) => p + " と、すぐに呼び合いが続きます。条件で止まらなければ、Udon が" + (one ? "このトリガー" : "これらのトリガー") + "を止め、以後動かなくなります。どれかを遅らせるか、止まる条件を付けてください。");
            Report(new[] { LinkKind.Immediate, LinkKind.Network },
                (p, one) => "These send to each other over the network without end: " + p + ". Every player keeps sending, which delays other sync. Delay one of them, or add a condition.",
                (p, one) => p + " と、ネットワークで送り合いが続きます。全員が送り続けるので、ほかの同期が遅れます。どれかを遅らせるか、止まる条件を付けてください。");
        }

        static bool SendsToObjects(KAction a) =>
            (a.actionId == ActionCatalog.SendEventId || a.actionId == ActionCatalog.SendEventDelayedId || a.actionId == ActionCatalog.SendRandomEventId)
            && a.args.Count > 0 && a.args[0].source == KArgSource.Objects;

        /// <summary>The triggers among an action's dragged-in targets (a trigger's object, or the UdonBehaviour it made).</summary>
        static IEnumerable<TripwireTrigger> Targets(KAction a) =>
            a.args[0].objects
                .Select(o => o is GameObject go ? go : (o as Component)?.gameObject)
                .Where(go => go != null)
                .Select(go => go.GetComponent<TripwireTrigger>())
                .Where(x => x != null)
                .Distinct();
    }
}
