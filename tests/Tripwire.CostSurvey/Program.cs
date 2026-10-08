// A static cost survey of the generated UdonSharp: every event × action, with setting variants, generated as the
// Inspector would, and counted. Finds combinations whose output does more than it should, especially on events that
// happen often (every frame, every physics step, while staying in an area), and whether Tripwire warns about them.
//
// Writes <out>/survey.tsv (one row per accepted combination), <out>/excluded.tsv (combinations that didn't generate,
// with the reason) and <out>/summary.txt (counts and the flagged combinations, worst first).
// Usage: dotnet run -- <out dir>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Tripwire.Core;

static class Program
{
    // Variables every program declares (unsynced unless a variant says so), so variable actions find one that fits.
    static readonly (string name, ParamType type)[] Vars =
    {
        ("b", ParamType.Of(ValueKind.Bool)), ("i", ParamType.Of(ValueKind.Int)), ("f", ParamType.Of(ValueKind.Float)),
        ("s", ParamType.Of(ValueKind.String)), ("go", ParamType.Object("UnityEngine.GameObject")),
        ("gos", ParamType.Objects("UnityEngine.GameObject")),
    };

    sealed class Variant
    {
        public Broadcast Broadcast; public float Delay; public int Targets; public bool Synced;
        public string Key => $"{Broadcast}/{(Delay > 0 ? "delay" : "now")}/x{Targets}/{(Synced ? "synced" : "local")}";
    }

    static IEnumerable<Variant> Variants()
    {
        foreach (var b in new[] { Broadcast.Local, Broadcast.All })
            foreach (var d in new[] { 0f, 0.5f })
                foreach (var n in new[] { 1, 3 })
                    foreach (var s in new[] { false, true })
                        yield return new Variant { Broadcast = b, Delay = d, Targets = n, Synced = s };
    }

    static TriggerProgram Program_(EventSpec ev, Variant v, ActionCall action)
    {
        var p = new TriggerProgram();
        foreach (var (name, type) in Vars) p.Variables.Add(new VariableDecl { Name = name, Type = type, Synced = v.Synced && type.Kind != ValueKind.Object });
        var block = new EventBlock { EventId = ev.Id, Broadcast = v.Broadcast, DelaySeconds = v.Delay, Actions = { action } };
        switch (ev.Shape)
        {
            case EventShape.Custom: block.Name = "Ping"; break;
            case EventShape.VariableChanged: block.Name = "i"; break;
            case EventShape.Timer: block.Name = "tick"; block.Timer = new TimerSpec { MinSeconds = 1, MaxSeconds = 1 }; break;
            case EventShape.Ui: block.HasUiSource = true; break;
        }
        p.Events.Add(block);
        // Timer actions need a timer to name.
        if (ev.Shape != EventShape.Timer && ActionCatalog.Get(action.ActionId)?.Params.Any(x => x.TimerRef) == true)
            p.Events.Add(new EventBlock { EventId = EventCatalog.TimerId, Name = "tick", Timer = new TimerSpec(), Actions = { Log("t") } });
        return p;
    }

    static ActionCall Log(string text) => new ActionCall { ActionId = ActionCatalog.LogId, Args = { ArgValue.Const(text) } };

    /// <summary>Arguments for an action as the Inspector would fill them; null when this survey can't (calls to scripts).</summary>
    static List<ActionCall> Fill(ActionSpec a, Variant v)
    {
        if (a.Special == ActionSpecial.Call || a.Id == ActionCatalog.ScriptCallId) return null;
        var results = new List<ActionCall>();
        // Variable references: try each declared variable; the generator says which fit.
        var varParams = a.Params.Select((p, i) => (p, i)).Where(x => x.p.VariableRef || x.p.RemoteVariableRef).ToList();
        IEnumerable<string[]> choices = new[] { new string[a.Params.Length] };
        foreach (var (p, i) in varParams)
            choices = choices.SelectMany(c => (p.Optional ? Vars.Select(x => x.name).Prepend("") : Vars.Select(x => x.name)).Select(n => { var d = (string[])c.Clone(); d[i] = n; return d; }));
        foreach (var names in choices.Take(400))
        {
            var call = new ActionCall { ActionId = a.Id };
            for (int i = 0; i < a.Params.Length; i++)
            {
                var p = a.Params[i];
                if (p.VariableRef || p.RemoteVariableRef) call.Args.Add(ArgValue.Const(names[i] ?? ""));
                else if (p.TimerRef) call.Args.Add(ArgValue.Const("tick"));
                else if (p.Type != null && p.Type.Kind == ValueKind.Object) call.Args.Add(p.Type.IsArray ? ArgValue.Objs(v.Targets) : ArgValue.Objs(1));
                else if (p.Type != null && p.Type.Kind == ValueKind.Player) call.Args.Add(ArgValue.Local());
                else if (p.Template) call.Args.Add(ArgValue.Const("Score {i}"));
                else if (p.Name == "event") call.Args.Add(ArgValue.Const("Ping")); // Send Event: an empty name is an error
                else if (p.Type == null) call.Args.Add(ArgValue.Const(1)); // takes the variable's type (amount, min, max)
                else call.Args.Add(ArgValue.Const(p.Default ?? DefaultFor(p.Type)));
            }
            if (a.HoldsActions) call.Then.Add(Log("inner"));
            if (a.HasConditions) call.Conditions.Add(new Condition { Variable = "b", Op = CompareOp.Equal, Value = ArgValue.Const(true) });
            if (a.Params.Any(x => x.RemoteVariableRef)) call.RemoteType = ParamType.Of(ValueKind.Int);
            // Break and Continue only exist inside a loop.
            if (a.Special == ActionSpecial.Break || a.Special == ActionSpecial.Continue)
                call = new ActionCall { ActionId = ActionCatalog.RepeatId, Args = { ArgValue.Const(3), ArgValue.Const("") }, Then = { call } };
            results.Add(call);
        }
        return results;
    }

    static object DefaultFor(ParamType t) => t?.Kind switch
    {
        ValueKind.Bool => true, ValueKind.Int => 1, ValueKind.Float => 1f, ValueKind.String => "x",
        ValueKind.Vector3 => new float[3], ValueKind.Url => "https://example.com", _ => "x",
    };

    // ---- counting ----

    /// <summary>The class's methods by name (bodies with their braces), from the generator's fixed layout.</summary>
    static Dictionary<string, string> Methods(string src)
    {
        var map = new Dictionary<string, string>();
        foreach (Match m in Regex.Matches(src, @"\n        (?:public |private )?(?:override )?(?:void|bool|int|float|string)\s+(\w+)\([^)]*\)\s*\n        \{\n(.*?)\n        \}\n", RegexOptions.Singleline))
            map[m.Groups[1].Value] = m.Groups[2].Value;
        return map;
    }

    /// <summary>The bodies a call of the event runs: its entry method and every method of the class it reaches.</summary>
    static string Reachable(string src, EventSpec ev, string eventName)
    {
        var methods = Methods(src);
        string entry = ev.Shape == EventShape.Custom ? eventName
            : ev.Shape == EventShape.Timer ? methods.Keys.FirstOrDefault(k => k.StartsWith("_Tw_T"))
            : ev.Shape == EventShape.VariableChanged ? methods.Keys.FirstOrDefault(k => k.StartsWith("_Tw_Changed_"))
            : ev.Shape == EventShape.Ui ? methods.Keys.FirstOrDefault(k => k.StartsWith("Tw_Ui"))
            : ev.Method;
        if (entry == null || !methods.ContainsKey(entry)) return "";
        var seen = new HashSet<string> { entry };
        var queue = new Queue<string>(seen);
        var sb = new StringBuilder();
        while (queue.Count > 0)
        {
            var body = methods[queue.Dequeue()];
            sb.Append(body).Append('\n');
            foreach (var name in methods.Keys)
                if (!seen.Contains(name) && Regex.IsMatch(body, @"\b" + name + @"\(|""" + name + @""""))
                { seen.Add(name); queue.Enqueue(name); }
        }
        return sb.ToString();
    }

    sealed class Counts
    {
        public int Statements, GetComponent, New, StringBuild, Network, Delayed, Loops, IsValid, Calls;
        public static Counts Of(string body)
        {
            int Count(string pattern) => Regex.Matches(body, pattern).Count;
            return new Counts
            {
                Statements = Count(@";"),
                GetComponent = Count(@"GetComponent<"),
                // A struct built from constants only is folded by UdonSharp at compile time (no work at run time).
                New = Count(@"\bnew\s+[A-Za-z]") - Count(@"\bnew\s+(Vector2|Vector3|Vector4|Quaternion|Color)\(\s*-?[0-9][0-9.]*f?(\s*,\s*-?[0-9][0-9.]*f?)*\s*\)"),
                StringBuild = Count(@"\+\s*""|""\s*\+|\.ToString\(\)|string\.(Format|Concat|Join)\("),
                Network = Count(@"SendCustomNetworkEvent|RequestSerialization\(|Networking\.SetOwner"),
                Delayed = Count(@"SendCustomEventDelayed"),
                Loops = Count(@"\bfor\s*\(|\bwhile\s*\("),
                IsValid = Count(@"Utilities\.IsValid"),
                Calls = Count(@"\b\w+\("),
            };
        }
        public static string Header => "statements\tgetComponent\tnew\tstringBuild\tnetwork\tdelayed\tloops\tisValid\tcalls";
        public string Row => $"{Statements}\t{GetComponent}\t{New}\t{StringBuild}\t{Network}\t{Delayed}\t{Loops}\t{IsValid}\t{Calls}";
    }

    static int Main(string[] args)
    {
        var outDir = args.Length > 0 ? args[0] : "survey-out";
        Directory.CreateDirectory(outDir);
        var rows = new StringBuilder("event\tfrequent\taction\tvariant\twarnings\t" + Counts.Header + "\tsize\n");
        var excluded = new StringBuilder("event\taction\tvariant\treason\n");
        var flagged = new List<(int score, string line)>();
        int accepted = 0, rejected = 0, skipped = 0, unread = 0;
        // The generated source and diagnostics of every combination, to compare before / after a generator change.
        using var corpus = new StreamWriter(new System.IO.Compression.GZipStream(File.Create(Path.Combine(outDir, "corpus.tsv.gz")), System.IO.Compression.CompressionLevel.Optimal));

        foreach (var ev in EventCatalog.All)
        {
            if (ev.Shape == EventShape.Listen) { skipped++; excluded.Append($"{ev.Id}\t*\t*\tneeds a script to listen to (not surveyed)\n"); continue; }
            foreach (var v in Variants())
            {
                var eventName = ev.Shape == EventShape.Custom ? "Ping" : null;

                foreach (var a in ActionCatalog.All)
                {
                    var calls = Fill(a, v);
                    if (calls == null) { if (v.Key == Variants().First().Key) { skipped++; excluded.Append($"{ev.Id}\t{a.Id}\t*\tcalls into another script (not surveyed)\n"); } continue; }
                    GeneratedProgram ok = null; string firstError = null;
                    foreach (var call in calls)
                    {
                        var g = CodeGenerator.Generate(Program_(ev, v, call));
                        if (!g.HasErrors) { ok = g; break; }
                        firstError ??= g.Diagnostics.First(d => d.Severity == Severity.Error).Message;
                    }
                    if (ok == null) { rejected++; excluded.Append($"{ev.Id}\t{a.Id}\t{v.Key}\t{firstError}\n"); continue; }
                    accepted++;
                    corpus.Write($"{ev.Id}|{a.Id}|{v.Key}\t{string.Join(" | ", ok.Diagnostics.Select(d => d.Severity + ": " + d.Message)).Replace('\t', ' ').Replace('\n', ' ')}\t{ok.Source.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\t", "\\t")}\n");
                    var reach = Reachable(ok.Source, ev, eventName);
                    if (reach.Length == 0) { excluded.Append($"{ev.Id}\t{a.Id}\t{v.Key}\tentry method not found (survey can't count it)\n"); unread++; continue; }
                    var c = Counts.Of(reach);
                    var warnings = string.Join(" | ", ok.Diagnostics.Where(d => d.Severity == Severity.Warning).Select(d => d.Message.Split('。', '.')[0])).Replace('\t', ' ');
                    rows.Append($"{ev.Id}\t{(ev.Frequent ? 1 : 0)}\t{a.Id}\t{v.Key}\t{warnings}\t{c.Row}\t{ok.Source.Length}\n");

                    // Work that should not happen on an event that fires all the time.
                    if (ev.Frequent)
                    {
                        int score = c.Network * 100 + c.Delayed * 50 + c.New * 10 + c.StringBuild * 5 + c.GetComponent * 5;
                        if (score > 0)
                            flagged.Add((score, $"{score,4}  {ev.Id} × {a.Id}  [{v.Key}]  net {c.Network} delayed {c.Delayed} new {c.New} str {c.StringBuild} getComp {c.GetComponent}  warned: {(warnings.Length > 0 ? warnings : "NO")}"));
                    }
                }
            }
        }

        File.WriteAllText(Path.Combine(outDir, "survey.tsv"), rows.ToString());
        File.WriteAllText(Path.Combine(outDir, "excluded.tsv"), excluded.ToString());
        var summary = new StringBuilder();
        summary.AppendLine($"accepted {accepted} (of which entry not found, so not counted: {unread}), rejected (generator errors) {rejected}, skipped (not surveyed) {skipped}");
        summary.AppendLine($"flagged on frequent events: {flagged.Count} (of which not warned: {flagged.Count(f => f.line.EndsWith("warned: NO"))})");
        summary.AppendLine();
        foreach (var f in flagged.OrderByDescending(f => f.score).ThenBy(f => f.line)) summary.AppendLine(f.line);
        File.WriteAllText(Path.Combine(outDir, "summary.txt"), summary.ToString());
        Console.WriteLine(summary.ToString().Split('\n').Take(3).Aggregate((x, y) => x + "\n" + y));
        return 0;
    }
}
