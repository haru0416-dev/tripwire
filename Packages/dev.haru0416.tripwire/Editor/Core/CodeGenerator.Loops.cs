using System.Collections.Generic;
using System.Linq;

namespace Tripwire.Core
{
    /// <summary>How one event block runs another: in the same call (and so the same frame), over the network, or later.</summary>
    public enum LinkKind { Immediate, Network, Delayed }

    /// <summary>Block <see cref="From"/>'s action number <see cref="Act"/> runs block <see cref="To"/> (of the same trigger).</summary>
    public sealed class EventLink
    {
        public int From, Act, To;
        public LinkKind Kind;
    }

    public static partial class CodeGenerator
    {
        /// <summary>
        /// How the trigger's event blocks run each other: Send Event to this trigger, and changes to watched variables
        /// (Toggle / Add / Random: they change the value every time; Set may put back the same value, which stops).
        /// Blocks that delay themselves or broadcast turn the link into Delayed / Network.
        /// </summary>
        public static List<EventLink> EventLinks(TriggerProgram p)
        {
            var links = new List<EventLink>();
            LinkKind Into(EventBlock target, LinkKind how) =>
                how == LinkKind.Network || target.Broadcast != Broadcast.Local ? LinkKind.Network
                : how == LinkKind.Delayed || target.DelaySeconds > 0f ? LinkKind.Delayed
                : LinkKind.Immediate;
            for (int i = 0; i < p.Events.Count; i++)
            {
                var flat = ActionCall.Flatten(p.Events[i].Actions);
                for (int k = 0; k < flat.Count; k++)
                {
                    var a = flat[k];
                    void To(System.Func<EventBlock, bool> which, LinkKind how)
                    {
                        for (int j = 0; j < p.Events.Count; j++)
                            if (which(p.Events[j])) links.Add(new EventLink { From = i, Act = k, To = j, Kind = Into(p.Events[j], how) });
                    }
                    bool ToSelf = a.Args.Count > 0 && a.Args[0]?.Source == ArgSource.Self;
                    if (a.ActionId == ActionCatalog.SendEventId && ToSelf && a.Args.Count > 1 && a.Args[1]?.Constant is string name)
                    {
                        var broadcast = a.Args.Count > 2 && a.Args[2]?.Constant is int b ? (Broadcast)b : Broadcast.Local;
                        To(e => e.EventId == EventCatalog.CustomId && e.Name == name, broadcast == Broadcast.Local ? LinkKind.Immediate : LinkKind.Network);
                    }
                    if (a.ActionId == ActionCatalog.SendRandomEventId && ToSelf && a.Args.Count > 1 && a.Args[1]?.Constant is string names)
                    {
                        var broadcast = a.Args.Count > 2 && a.Args[2]?.Constant is int b ? (Broadcast)b : Broadcast.Local;
                        foreach (var picked in RandomEventNames(names))
                            To(e => e.EventId == EventCatalog.CustomId && e.Name == picked, broadcast == Broadcast.Local ? LinkKind.Immediate : LinkKind.Network);
                    }
                    if (a.ActionId == ActionCatalog.SendEventDelayedId && ToSelf && a.Args.Count > 1 && a.Args[1]?.Constant is string later)
                        To(e => e.EventId == EventCatalog.CustomId && e.Name == later, LinkKind.Delayed);
                    if (ActionCatalog.Get(a.ActionId)?.ChangesValueEachTime == true && a.Args.Count > 0 && a.Args[0]?.Constant is string changed)
                        To(e => e.EventId == EventCatalog.VariableChangedId && e.Name == changed, LinkKind.Immediate);
                }
            }
            return links;
        }

        /// <summary>
        /// The cycles of a graph through the given links (each cycle once, as the node list starting from its smallest
        /// node). Shared by the trigger's own check and the scene-wide one in the editor. Warnings need a few loops, not
        /// all of them: blocks that all call each other have more cycles than can be listed (9 such blocks: over 100,000).
        /// So each start node gets its own share of <paramref name="maxSteps"/> and finds at least its first cycle; more
        /// cycles are listed only up to <paramref name="maxCycles"/> in all. A busy part of the graph can't hide a loop elsewhere.
        /// </summary>
        public static List<List<T>> Cycles<T>(IEnumerable<T> nodes, System.Func<T, IEnumerable<T>> next, IComparer<T> order = null,
            int maxCycles = 20, int maxSteps = 20000)
        {
            order ??= Comparer<T>.Default;
            var all = nodes.ToList();
            var found = new List<List<T>>();
            var seen = new HashSet<string>();
            int stepsPerStart = System.Math.Max(2000, maxSteps / System.Math.Max(1, all.Count));
            // From each start node, paths through nodes not smaller than it, so each cycle is found once, from its smallest node.
            foreach (var start in all)
            {
                int steps = 0, foundHere = 0;
                bool Full() => steps >= stepsPerStart || (foundHere > 0 && found.Count >= maxCycles);
                var path = new List<T> { start };
                var onPath = new HashSet<T> { start };
                void Walk(T at, int depth)
                {
                    if (depth > 12 || Full()) return; // long chains of distinct blocks are rare; keep the search bounded
                    foreach (var n in next(at).Distinct())
                    {
                        steps++;
                        if (Full()) return;
                        if (order.Compare(n, start) < 0) continue;
                        if (EqualityComparer<T>.Default.Equals(n, start))
                        {
                            var key = string.Join(">", path);
                            if (seen.Add(key)) { found.Add(new List<T>(path)); foundHere++; }
                            continue;
                        }
                        if (!onPath.Add(n)) continue;
                        path.Add(n);
                        Walk(n, depth + 1);
                        path.RemoveAt(path.Count - 1);
                        onPath.Remove(n);
                    }
                }
                Walk(start, 0);
            }
            return found;
        }

        sealed partial class Generator
        {
            /// <summary>
            /// Blocks that run each other forever: in the same call (Udon stops the trigger), over the network (sends
            /// that never stop), or by delayed calls that multiply. Each cycle is reported once, on the action that starts it.
            /// </summary>
            void WarnLoops(EventSpec[] specs)
            {
                var links = EventLinks(p);
                string Name(int i)
                {
                    var e = p.Events[i];
                    var title = specs[i] != null ? Texts.EventName(specs[i]) : e.EventId;
                    return title + (string.IsNullOrEmpty(e.Name) ? "" : Texts.T(" (" + e.Name + ")", "「" + e.Name + "」"));
                }
                string Path(List<int> cycle) => string.Join(" → ", cycle.Concat(new[] { cycle[0] }).Select(Name));
                EventLink First(List<int> cycle, LinkKind[] kinds) =>
                    links.First(l => l.From == cycle[0] && l.To == cycle[cycle.Count > 1 ? 1 : 0] && kinds.Contains(l.Kind));

                // Each block's next blocks, listed once: the search asks for them at every step (thousands of times).
                System.Func<int, IEnumerable<int>> Next(params LinkKind[] kinds)
                {
                    var next = new List<int>[p.Events.Count];
                    for (int i = 0; i < next.Length; i++) next[i] = new List<int>();
                    foreach (var l in links)
                        if (kinds.Contains(l.Kind) && !next[l.From].Contains(l.To)) next[l.From].Add(l.To);
                    return i => next[i];
                }
                var immediate = Cycles(Enumerable.Range(0, p.Events.Count), Next(LinkKind.Immediate));
                foreach (var c in immediate)
                    Warn(c.Count == 1
                            ? Texts.T("This block runs itself again right away, without end. Unless a condition stops it, Udon stops this trigger for good. Delay it, or add a condition that stops it.",
                                      "このブロックは、すぐにまた自分を動かすので、止まらずに続きます。条件で止まらなければ、Udon がこのトリガーを止め、以後動かなくなります。遅らせるか、止まる条件を付けてください。")
                            : Texts.T("These blocks run each other right away, without end: " + Path(c) + ". Unless a condition stops it, Udon stops this trigger for good. Delay one of them, or add a condition that stops it.",
                                      "次のブロックが、止まらずにすぐ呼び合います: " + Path(c) + "。条件で止まらなければ、Udon がこのトリガーを止め、以後動かなくなります。どれかを遅らせるか、止まる条件を付けてください。"),
                         c[0], First(c, new[] { LinkKind.Immediate }).Act);

                var sends = new[] { LinkKind.Immediate, LinkKind.Network };
                foreach (var c in Cycles(Enumerable.Range(0, p.Events.Count), Next(sends)))
                {
                    if (immediate.Any(x => x.SequenceEqual(c))) continue;
                    Warn(c.Count == 1
                            ? Texts.T("This block sends itself over the network again, without end. Every player keeps sending, which delays other sync. Delay it, or add a condition that stops it.",
                                      "このブロックは、ネットワークでまた自分を送るので、止まらずに続きます。全員が送り続けるので、ほかの同期が遅れます。遅らせるか、止まる条件を付けてください。")
                            : Texts.T("These blocks send to each other over the network, without end: " + Path(c) + ". Every player keeps sending, which delays other sync. Delay one of them, or add a condition that stops it.",
                                      "次のブロックが、ネットワークで止まらずに送り合います: " + Path(c) + "。全員が送り続けるので、ほかの同期が遅れます。どれかを遅らせるか、止まる条件を付けてください。"),
                         c[0], First(c, sends).Act);
                }

                // A block that comes back to itself by two or more calls doubles its runs each time.
                var anyLink = Next(LinkKind.Immediate, LinkKind.Network, LinkKind.Delayed);
                for (int i = 0; i < p.Events.Count; i++)
                {
                    var back = links.Where(l => l.From == i && Reaches(anyLink, l.To, i)).ToList();
                    if (back.Count >= 2 && back.Any(l => l.Kind == LinkKind.Delayed))
                        Warn(Texts.T(Name(i) + " schedules itself more than once each time it runs, so the scheduled runs double every round.",
                                     Name(i) + " は、動くたびに自分をまた 2 回以上呼ぶので、予約が倍々に増えていきます。"), i, back[1].Act);
                }
            }

            static bool Reaches(System.Func<int, IEnumerable<int>> next, int from, int to)
            {
                var seen = new HashSet<int>();
                var stack = new Stack<int>();
                stack.Push(from);
                while (stack.Count > 0)
                {
                    int at = stack.Pop();
                    if (at == to) return true;
                    if (!seen.Add(at)) continue;
                    foreach (var n in next(at)) stack.Push(n);
                }
                return false;
            }
        }
    }
}
