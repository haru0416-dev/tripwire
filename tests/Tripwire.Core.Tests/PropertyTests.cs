using System;
using System.Collections.Generic;
using System.Linq;
using CsCheck;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Tripwire.Core;
using Xunit;
using static TestKit;

/// <summary>
/// Properties that hold for any trigger the Inspector can build, checked on random ones (CsCheck: a failure is shrunk
/// to a small trigger and printed with its seed). The triggers come from the catalogs, valid or not: any event, action,
/// argument source, condition, variable, gate. PBT_ITER=n runs more of them. One thread: the generator has
/// process-wide settings (KeepBodiesSeparate, the UI language) that tests switch.
/// </summary>
public class PropertyTests
{
    static readonly long Iter = long.TryParse(Environment.GetEnvironmentVariable("PBT_ITER"), out var n) ? n : 300;

    // ---------------- random triggers ----------------

    // Names a person might type, plus edge cases: Japanese, keywords, reserved prefixes, quotes, an encoded-looking name.
    static readonly string[] VariableNames = { "a", "count", "isOn", "スコア", "扉_開", "class", "v_x", "tw_Known", "uni_30b9", "say \"hi\"", "a b", "x" };
    static readonly string[] EventNames = { "Open", "Close", "open", "Interact", "Tw_X", "日本語", "", "A1" };
    static readonly ValueKind[] Kinds = { ValueKind.Bool, ValueKind.Int, ValueKind.Float, ValueKind.String, ValueKind.Vector3, ValueKind.Vector2, ValueKind.Color, ValueKind.Quaternion, ValueKind.Object, ValueKind.Player };

    // Any UTF-16 text: control characters, line terminators, lone surrogates, Japanese (CsCheck's Gen.String is printable ASCII only).
    static readonly Gen<string> AnyString = Gen.Frequency((1, Gen.String), (2, Gen.String[Gen.Char['\0', '\uffff'], 0, 30]));

    static readonly Gen<string> Text = Gen.OneOf(Gen.OneOfConst("", "hi", "{count}", "{スコア} 点", "{nope}", "a\nb", "\"q\"", "\\", "{", "}", "{{x}}", "\u2028", "😀"), AnyString);

    static readonly Gen<float> Float = Gen.OneOf(Gen.OneOfConst(0f, -0f, 1f, -1.5f, 0.1f, float.MaxValue, float.Epsilon, float.NaN, float.PositiveInfinity), Gen.Float);

    static Gen<object> Constant(ValueKind kind) => kind switch
    {
        ValueKind.Bool => Gen.Bool.Select(b => (object)b),
        ValueKind.Int => Gen.Int.Select(i => (object)i),
        ValueKind.Float => Float.Select(f => (object)f),
        ValueKind.String => Text.Select(s => (object)s),
        ValueKind.Vector3 or ValueKind.Quaternion => Float.Array[3].Select(a => (object)a),
        ValueKind.Vector2 => Float.Array[2].Select(a => (object)a),
        ValueKind.Color => Float.Array[4].Select(a => (object)a),
        _ => Gen.Const((object)null),
    };

    static readonly Gen<VariableDecl> Variable =
        from name in Gen.OneOfConst(VariableNames)
        from kind in Gen.OneOfConst(Kinds)
        from initial in Constant(kind)
        from how in Gen.Int[0, 5] // plain, synced, temporary, saved, saved and synced (an error), external
        from key in Gen.OneOfConst(null, "k", "スコア")
        select new VariableDecl
        {
            Name = name, Type = kind == ValueKind.Object ? ParamType.Object("UnityEngine.GameObject") : ParamType.Of(kind), Initial = initial,
            Synced = how == 1 || how == 4, Temporary = how == 2, SaveKey = how >= 3 && how <= 4 ? key ?? name : null, External = how == 5,
        };

    // An argument for a parameter: mostly what the Inspector offers for its type, sometimes anything.
    static Gen<ArgValue> Arg(ActionParam param) =>
        Gen.Frequency(
            (5, (param.VariableRef || param.TimerRef ? Gen.OneOfConst(VariableNames.Concat(EventNames).ToArray()).Select(s => (object)s)
                 : param.Choices != null ? Gen.Int[-1, param.Choices.Length].Select(i => (object)i)
                 : Constant(param.Type?.Kind ?? ValueKind.Int)).Select(ArgValue.Const)),
            (3, Gen.OneOfConst(VariableNames).Select(ArgValue.Var)),
            (1, Gen.OneOfConst("player", "value", "nope").Select(ArgValue.Param)),
            (1, Gen.Int[0, 3].Select(ArgValue.Objs)),
            (1, Gen.Const(0).Select(_ => ArgValue.SelfObject())),
            (1, Gen.Const(0).Select(_ => ArgValue.Local())),
            (1, Gen.Const((ArgValue)null))); // an input missing from the saved data (the editor passes null)

    static Gen<ArgValue[]> Args(ActionParam[] ps) =>
        ps.Aggregate(Gen.Const(new ArgValue[0]), (gen, param) => from done in gen from arg in Arg(param) select done.Append(arg).ToArray());

    static readonly Gen<Condition> Condition =
        from variable in Gen.OneOfConst(VariableNames.Append(null).ToArray())
        from op in Gen.Enum<CompareOp>()
        from value in Gen.Frequency((3, Gen.Int[-2, 5].Select(i => ArgValue.Const(i))), (1, Gen.Bool.Select(b => ArgValue.Const(b))), (1, Gen.OneOfConst(VariableNames).Select(ArgValue.Var)))
        from negate in Gen.Bool
        select new Condition { Variable = variable, Op = op, Value = value, Negate = negate };

    // Udon.Call needs the editor's API list: everything else in the catalog.
    static readonly ActionSpec[] Actions = ActionCatalog.All.Where(a => a.Id != ActionCatalog.CallId).ToArray();
    // As offered in the Inspector: no script calls (they need the editor's list of scripts), Break / Continue in loops.
    static readonly ActionSpec[] Offered = Actions.Where(a => a.Id != ActionCatalog.ScriptCallId && a.Special != ActionSpecial.Break && a.Special != ActionSpecial.Continue).ToArray();
    static readonly ActionSpec[] InLoops = Offered.Concat(Actions.Where(a => a.Special == ActionSpecial.Break || a.Special == ActionSpecial.Continue)).ToArray();

    static Gen<ActionCall> Action(int depth) =>
        from spec in Gen.OneOfConst(depth < 2 ? Actions : Actions.Where(a => !a.HoldsActions).ToArray())
        from args in Args(spec.Params)
        from extra in Gen.Int[0, 4] // a missing or extra argument, now and then
        from conditions in spec.HoldsActions ? Condition.List[0, 2] : Gen.Const(new List<Condition>())
        from then in spec.HoldsActions ? Action(depth + 1).List[0, 3] : Gen.Const(new List<ActionCall>())
        from otherwise in spec.HoldsActions ? Action(depth + 1).List[0, 2] : Gen.Const(new List<ActionCall>())
        from result in Gen.OneOfConst(null, "count", "スコア")
        select new ActionCall
        {
            ActionId = spec.Id,
            Args = extra == 0 ? args.Take(Math.Max(0, args.Length - 1)).ToList() : extra == 1 ? args.Append(ArgValue.Const(1)).ToList() : args.ToList(),
            Conditions = conditions, Then = then, Else = otherwise, ResultVariable = result,
        };

    static readonly Gen<EventBlock> Event =
        from spec in Gen.OneOfConst(EventCatalog.All.ToArray())
        from name in Gen.OneOfConst(EventNames.Concat(VariableNames).ToArray())
        from broadcast in Gen.Enum<Broadcast>()
        from delay in Gen.OneOfConst(0f, 0f, 0.5f, -1f)
        from filter in Gen.Enum<PlayerFilter>()
        from gate in Gen.Enum<Gate>()
        from gateNames in Gen.Frequency((1, Gen.Const((List<string>)null)), (3, Text.List[0, 3]))
        from conditions in Condition.List[0, 2]
        from matchAny in Gen.Bool
        from actions in Action(0).List[0, 4]
        select new EventBlock
        {
            EventId = spec.Id, Name = name, Broadcast = broadcast, DelaySeconds = delay, PlayerFilter = filter, Gate = gate, GateNames = gateNames,
            Conditions = conditions, MatchAny = matchAny, Actions = actions,
            // As the editor fills them in for these kinds of events.
            Timer = spec.Shape == EventShape.Timer ? new TimerSpec() : null,
            Listen = spec.Shape == EventShape.Listen ? new ListenSpec() : null,
        };

    static readonly Gen<TriggerProgram> Wild =
        from variables in Variable.List[0, 5]
        from events in Event.List[0, 6]
        from continuous in Gen.Bool
        select new TriggerProgram { Variables = variables, Events = events, ContinuousSync = continuous };

    // ---- triggers made the way the Inspector offers: arguments of fitting types, variables that exist ----
    // (Wild ones are mostly refused, so what the generator accepts would hardly be checked without these.)

    static readonly string[] CustomNames = { "Open", "Close", "Ping" };
    static readonly ParamType GameObjectType = ParamType.Object("UnityEngine.GameObject");

    static Gen<VariableDecl> PlausibleVariable(string[] names) =>
        from name in Gen.OneOfConst(names.Where(v => CodeGenerator.CheckVariableName(v) == null).ToArray())
        from kind in Gen.OneOfConst(Kinds.Where(k => k != ValueKind.Player).ToArray())
        from initial in Constant(kind)
        from how in Gen.Frequency((4, Gen.Const(0)), (2, Gen.Const(1)), (1, Gen.Const(2)), (1, Gen.Const(3)))
        let type = kind == ValueKind.Object ? GameObjectType : ParamType.Of(kind)
        select new VariableDecl
        {
            Name = name, Type = type, Initial = initial,
            Synced = how == 1 && kind != ValueKind.Object, Temporary = how == 2 && CodeGenerator.CanBeTemporary(type), SaveKey = how == 3 && CodeGenerator.SavedAs(type) != null ? name : null,
        };

    static Gen<ArgValue> PlausibleArg(ActionSpec spec, ActionParam prm, List<VariableDecl> vars, EventSpec ev)
    {
        Gen<ArgValue> VarOf(Func<VariableDecl, bool> fits, Gen<ArgValue> otherwise)
        {
            var names = vars.Where(fits).Select(v => v.Name).ToArray();
            return names.Length == 0 ? otherwise : Gen.Frequency((2, Gen.OneOfConst(names).Select(ArgValue.Var)), (1, otherwise));
        }
        if (prm.Choices != null) return Gen.Int[0, prm.Choices.Length - 1].Select(i => ArgValue.Const(i));
        if (prm.VariableRef)
        {
            var names = vars.Where(v => ActionRules.VariableFits(spec, prm, v.Type, v.Synced)).Select(v => v.Name).ToArray();
            return names.Length == 0 ? Gen.Const(ArgValue.Const(prm.Optional ? "" : "count")) : Gen.OneOfConst(names).Select(n => ArgValue.Const(n));
        }
        if (prm.TimerRef) return Gen.Const(ArgValue.Const("Tick"));
        if (prm.Name == "event") return Gen.OneOfConst(CustomNames).Select(n => ArgValue.Const(n));
        if (prm.Name == "events") return Gen.Const(ArgValue.Const(string.Join("\n", CustomNames)));
        if (prm.RemoteVariableRef) return Gen.Const(ArgValue.Const("n"));
        if (prm.Type == null) return Gen.Int[0, 9].Select(i => ArgValue.Const(i));
        var type = prm.Type;
        var players = ev?.Params.Where(x => x.Type.Kind == ValueKind.Player).Select(x => x.Name).ToArray() ?? new string[0];
        switch (type.Kind)
        {
            case ValueKind.Object:
            case ValueKind.Other:
                if (type.IsArray) return Gen.Frequency((3, Gen.Int[1, 3].Select(ArgValue.Objs)), (1, Gen.Const(0).Select(_ => ArgValue.SelfObject())));
                return VarOf(v => CodeGenerator.IsAssignable(type, v.Type), Gen.Frequency((3, Gen.Const(0).Select(_ => ArgValue.Objs(1))), (1, Gen.Const(0).Select(_ => ArgValue.SelfObject()))));
            case ValueKind.Player:
                return players.Length == 0 ? Gen.Const(0).Select(_ => ArgValue.Local()) : Gen.OneOf(Gen.Const(0).Select(_ => ArgValue.Local()), Gen.OneOfConst(players).Select(ArgValue.Param));
            case ValueKind.Url:
                return Gen.Const(ArgValue.Const("https://example.com/a.mp4"));
            case ValueKind.Enum:
                return Gen.Const(ArgValue.Const(prm.Default));
            default:
                return VarOf(v => CodeGenerator.IsAssignable(type, v.Type), Constant(type.Kind).Select(ArgValue.Const));
        }
    }

    static Gen<ArgValue[]> PlausibleArgs(ActionSpec spec, List<VariableDecl> vars, EventSpec ev) =>
        spec.Params.Aggregate(Gen.Const(new ArgValue[0]), (gen, prm) => from done in gen from arg in PlausibleArg(spec, prm, vars, ev) select done.Append(arg).ToArray());

    static Gen<Condition> PlausibleCondition(List<VariableDecl> vars) =>
        vars.Count == 0 ? Gen.Const((Condition)null) :
        from v in Gen.OneOfConst(vars.ToArray())
        from op in Gen.OneOfConst(CompareOp.Equal, CompareOp.NotEqual)
        from value in Constant(v.Type.Kind)
        from negate in Gen.Bool
        select new Condition { Variable = v.Name, Op = op, Value = value == null ? ArgValue.Var(v.Name) : ArgValue.Const(value), Negate = negate };

    static Gen<ActionCall> PlausibleAction(List<VariableDecl> vars, EventSpec ev, int depth, int maxDepth, bool inLoop = false) =>
        from spec in Gen.OneOfConst((inLoop ? InLoops : Offered).Where(a => depth < maxDepth || !a.HoldsActions).ToArray())
        from args in PlausibleArgs(spec, vars, ev)
        from conditions in spec.HasConditions ? PlausibleCondition(vars).List[1, 2] : Gen.Const(new List<Condition>())
        from then in spec.HoldsActions ? PlausibleAction(vars, ev, depth + 1, maxDepth, inLoop || spec.IsLoop).List[1, 3] : Gen.Const(new List<ActionCall>())
        from otherwise in spec.HasElse ? PlausibleAction(vars, ev, depth + 1, maxDepth, inLoop).List[0, 2] : Gen.Const(new List<ActionCall>())
        select new ActionCall
        {
            ActionId = spec.Id, Args = args.ToList(), Conditions = conditions.Where(c => c != null).ToList(), Then = then, Else = otherwise,
            RemoteType = spec.Id == ActionCatalog.SetRemoteId || spec.Id == ActionCatalog.GetRemoteId ? ParamType.Of(ValueKind.Int) : null,
        };

    static Gen<EventBlock> PlausibleEvent(List<VariableDecl> vars, int minActions, int maxActions, int maxDepth) =>
        from spec in Gen.OneOfConst(EventCatalog.All.Where(e => e.Shape != EventShape.Listen).ToArray())
        from name in spec.Id == EventCatalog.VariableChangedId ? (vars.Count == 0 ? Gen.Const("count") : Gen.OneOfConst(vars.Select(v => v.Name).ToArray()))
                   : spec.Shape == EventShape.Timer ? Gen.Const("Tick") : Gen.OneOfConst(CustomNames)
        from broadcast in Gen.Frequency((4, Gen.Const(Broadcast.Local)), (1, Gen.Enum<Broadcast>()))
        from delay in Gen.OneOfConst(0f, 0f, 0f, 0.5f)
        from gate in Gen.Frequency((3, Gen.Const(Gate.Anyone)), (1, Gen.Enum<Gate>()))
        from gateNames in Text.List[0, 3]
        from conditions in PlausibleCondition(vars).List[0, 1]
        from actions in PlausibleAction(vars, spec, 0, maxDepth).List[minActions, maxActions]
        select new EventBlock
        {
            EventId = spec.Id, Name = name, Broadcast = broadcast, DelaySeconds = delay, Gate = gate, GateNames = gateNames,
            Conditions = conditions.Where(c => c != null).ToList(), Actions = actions,
            Timer = spec.Shape == EventShape.Timer ? new TimerSpec() : null,
        };

    /// <summary>Variables most actions can use: a counter, an on/off, a list of objects, a component.</summary>
    static List<VariableDecl> BaseVariables()
    {
        var body = ParamType.Object("UnityEngine.Rigidbody");
        body.IsComponent = true;
        return new List<VariableDecl>
        {
            new VariableDecl { Name = "count", Kind = ValueKind.Int, Initial = 0 },
            new VariableDecl { Name = "flag", Kind = ValueKind.Bool, Initial = false },
            new VariableDecl { Name = "items", Type = new ParamType(ValueKind.Object, "UnityEngine.GameObject", true) },
            new VariableDecl { Name = "body", Type = body },
        };
    }

    static Gen<TriggerProgram> PlausibleOf(string[] names, int maxExtraVariables, int minEvents, int maxEvents, int minActions, int maxActions, int maxDepth) =>
        from extra in PlausibleVariable(names).List[0, maxExtraVariables]
        let variables = BaseVariables().Concat(extra).GroupBy(v => CodeGenerator.Ident(v.Name)).Select(g => g.First()).ToList()
        from events in PlausibleEvent(variables, minActions, maxActions, maxDepth).List[minEvents, maxEvents]
        from timer in Gen.Bool // the timer Timer actions name
        from continuous in Gen.Bool
        select new TriggerProgram
        {
            Variables = variables, ContinuousSync = continuous,
            Events = timer ? events.Append(new EventBlock { EventId = EventCatalog.TimerId, Name = "Tick", Timer = new TimerSpec(), Actions = { Log("tick") } }).ToList() : events,
        };

    static readonly Gen<TriggerProgram> Plausible = PlausibleOf(VariableNames, 4, 1, 5, 1, 4, 2);

    // ---- big triggers: 20 to 60 cards, actions nested four deep, up to 20 variables ----

    static readonly string[] ManyNames = VariableNames.Concat(Enumerable.Range(0, 30).Select(i => "n" + i)).Concat(Enumerable.Range(0, 10).Select(i => "数" + i)).ToArray();

    /// <summary>
    /// A big trigger with the parts the generator refuses taken out, round by round (an error points at a card, an
    /// action, a condition or a variable). Made at random, almost every big trigger has some error somewhere, and then
    /// nothing of it would be generated.
    /// </summary>
    static TriggerProgram Repaired(TriggerProgram p)
    {
        for (int round = 0; round < 40; round++)
        {
            var errors = CodeGenerator.Generate(p).Diagnostics.Where(d => d.Severity == Severity.Error).ToList();
            if (errors.Count == 0) return p;
            var events = new SortedSet<int>();
            var actions = new SortedSet<(int ev, int act)>();
            var conditions = new SortedSet<(int ev, int cond)>();
            var variables = new SortedSet<int>();
            foreach (var d in errors)
            {
                if (d.Event >= 0 && d.Event < p.Events.Count)
                {
                    if (d.Action >= 0) actions.Add((d.Event, d.Action));
                    else if (d.Condition >= 0) conditions.Add((d.Event, d.Condition));
                    else events.Add(d.Event);
                }
                else if (d.Variable >= 0 && d.Variable < p.Variables.Count) variables.Add(d.Variable);
                else return p; // nowhere to point at: left as it is
            }
            // From the back, so the numbers still to remove stay right.
            foreach (var (ev, act) in actions.Reverse()) { int n = act; RemoveNumbered(p.Events[ev].Actions, ref n); }
            foreach (var (ev, cond) in conditions.Reverse()) if (cond < p.Events[ev].Conditions.Count) p.Events[ev].Conditions.RemoveAt(cond);
            foreach (var ev in events.Reverse()) p.Events.RemoveAt(ev);
            foreach (var v in variables.Reverse()) p.Variables.RemoveAt(v);
        }
        return p;
    }

    /// <summary>Removes the action numbered <paramref name="n"/> as ActionCall.Flatten numbers them (with what it holds).</summary>
    static bool RemoveNumbered(List<ActionCall> list, ref int n)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var a = list[i];
            if (a == null) continue;
            if (n == 0) { list.RemoveAt(i); return true; }
            n--;
            if (a.IsBlock && (RemoveNumbered(a.Then, ref n) || RemoveNumbered(a.Else, ref n))) return true;
        }
        return false;
    }

    static readonly Gen<TriggerProgram> Big = PlausibleOf(ManyNames, 16, 20, 60, 2, 8, 4).Select(Repaired);

    /// <summary>"small" and "big" as the editor generates them, "literal": small ones with the values written in.</summary>
    static Gen<TriggerProgram> Triggers(string kind) => kind == "big" ? Big : Program;

    /// <summary>Big ones take a few milliseconds each: fewer of them.</summary>
    static long IterOf(string kind) => kind == "big" ? Math.Max(10, Iter / 10) : Iter;

    static readonly Gen<TriggerProgram> Program = Gen.Frequency((1, Wild), (3, Plausible));

    static string Show(TriggerProgram p) => string.Join("\n",
        p.Variables.Select(v => "var " + v.Name + ": " + v.Type?.Kind + (v.Synced ? " synced" : "") + (v.Temporary ? " temp" : "") + (v.SaveKey != null ? " saved" : ""))
        .Concat(p.Events.Select(e => "on " + e.EventId + " '" + e.Name + "' gate=" + e.Gate + ": " + string.Join(", ", ActionCall.Flatten(e.Actions).Select(a => a.ActionId + "(" + string.Join(", ", a.Args.Select(ShowArg)) + ")")))));

    static string ShowArg(ArgValue a) => a == null ? "null" : a.Source switch
    {
        ArgSource.Constant => a.Constant is string s ? "\"" + s + "\"" : a.Constant is float[] f ? "(" + string.Join(" ", f) + ")" : a.Constant?.ToString() ?? "null",
        ArgSource.Objects => a.ObjectCount + " objects",
        ArgSource.Variable => "var " + a.Name,
        ArgSource.EventParam => "param " + a.Name,
        _ => a.Source.ToString(),
    };

    /// <summary>As the editor generates (values in fields), or with the values written in (<paramref name="literals"/>).</summary>
    static GeneratedProgram Generate(TriggerProgram p, bool keepBodiesSeparate, bool literals = false)
    {
        var kept = (CodeGenerator.KeepBodiesSeparate, CodeGenerator.ConstantsInFields);
        CodeGenerator.KeepBodiesSeparate = keepBodiesSeparate;
        CodeGenerator.ConstantsInFields = !literals;
        try { return CodeGenerator.Generate(p); }
        finally { (CodeGenerator.KeepBodiesSeparate, CodeGenerator.ConstantsInFields) = kept; }
    }

    static List<string> SyntaxErrors(string source) => CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp9)).GetDiagnostics()
        .Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToList();

    // ---------------- the generator ----------------

    [Fact]
    public void ThePlausibleTriggersAreMostlyAcceptedSoTheChecksBelowHaveCodeToLookAt()
    {
        int accepted = 0, actions = 0;
        Plausible.Sample(p =>
        {
            if (CodeGenerator.Generate(p).HasErrors) return;
            accepted++;
            actions += p.Events.Sum(e => ActionCall.Flatten(e.Actions).Count);
        }, iter: 1000, threads: 1, seed: "0000000000001");
        Assert.True(accepted > 200 && actions > accepted * 3, accepted + " of 1000 accepted, with " + actions + " actions");
    }

    [Fact]
    public void TheBigTriggersStayBigOnceTheRefusedPartsAreOut()
    {
        int n = 0, events = 0, actions = 0;
        Big.Sample(p =>
        {
            Assert.False(CodeGenerator.Generate(p).HasErrors, Show(p));
            n++;
            events += p.Events.Count;
            actions += p.Events.Sum(e => ActionCall.Flatten(e.Actions).Count);
        }, iter: 20, threads: 1);
        Assert.True(events / n >= 20 && actions / n >= 100, "on average " + events / n + " cards and " + actions / n + " actions");
    }

    [Theory, InlineData("small"), InlineData("big"), InlineData("literal")]
    public void WhatHasNoErrorsParsesAsCSharp9BothWays(string kind)
    {
        Triggers(kind).Sample(p =>
        {
            foreach (var separate in new[] { false, true })
            {
                var g = Generate(p, separate, kind == "literal");
                if (g.HasErrors) continue;
                var errors = SyntaxErrors(g.Source);
                Assert.True(errors.Count == 0, (separate ? "bodies separate" : "inlined") + ":\n" + string.Join("\n", errors.Take(5)) + "\n" + g.Source);
            }
        }, iter: IterOf(kind), threads: 1, print: Show);
    }

    [Theory, InlineData("small"), InlineData("big"), InlineData("literal")]
    public void GeneratingTwiceGivesTheSameProgram(string kind)
    {
        Triggers(kind).Sample(p =>
        {
            var a = Generate(p, false, kind == "literal");
            var b = Generate(p, false, kind == "literal");
            Assert.Equal(a.Source, b.Source);
            Assert.Equal(a.ClassName, b.ClassName);
            Assert.Equal(a.Diagnostics.Select(d => d.ToString()), b.Diagnostics.Select(d => d.ToString()));
        }, iter: IterOf(kind), threads: 1, print: Show);
    }

    [Theory, InlineData("small"), InlineData("big"), InlineData("literal")]
    public void InliningChangesNeitherTheMessagesNorTheEntries(string kind)
    {
        Triggers(kind).Sample(p =>
        {
            var inlined = Generate(p, false, kind == "literal");
            var separate = Generate(p, true, kind == "literal");
            Assert.Equal(separate.Diagnostics.Select(d => d.ToString()), inlined.Diagnostics.Select(d => d.ToString()));
            if (separate.HasErrors) return;
            // Every method other scripts, Udon or the network can reach is still there.
            static IEnumerable<string> Entries(string source) => CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(m => m.Modifiers.Any(SyntaxKind.PublicKeyword)).Select(m => m.Identifier.Text + "/" + m.ParameterList.Parameters.Count).OrderBy(x => x, StringComparer.Ordinal);
            Assert.Equal(Entries(separate.Source), Entries(inlined.Source));
        }, iter: IterOf(kind), threads: 1, print: Show);
    }

    [Theory, InlineData("small"), InlineData("big"), InlineData("literal")]
    public void TheClassIsNamedAfterItsSource(string kind)
    {
        Triggers(kind).Sample(p =>
        {
            var g = Generate(p, false, kind == "literal");
            if (g.HasErrors) return;
            Assert.StartsWith(CodeGenerator.ClassPrefix, g.ClassName);
            Assert.Single(CSharpSyntaxTree.ParseText(g.Source).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>(), c => c.Identifier.Text == g.ClassName);
        }, iter: IterOf(kind), threads: 1, print: Show);
    }

    [Theory, InlineData("small"), InlineData("big"), InlineData("literal")]
    public void MessagesAndBindingsPointAtThingsTheTriggerHas(string kind)
    {
        Triggers(kind).Sample(p =>
        {
            var g = Generate(p, false, kind == "literal");
            int Actions(int e) => ActionCall.Flatten(p.Events[e].Actions).Count;
            foreach (var d in g.Diagnostics)
            {
                Assert.True(d.Event >= -1 && d.Event < p.Events.Count, "event of " + d);
                Assert.True(d.Variable >= -1 && d.Variable < p.Variables.Count, "variable of " + d);
                if (d.Event >= 0 && d.Action >= 0) Assert.True(d.Action < Actions(d.Event), "action of " + d);
                if (d.Condition >= 0)
                    Assert.True(d.Action >= 0 ? d.Condition < ActionCall.Flatten(p.Events[d.Event].Actions)[d.Action].Conditions.Count
                                              : d.Event >= 0 && d.Condition < p.Events[d.Event].Conditions.Count, "condition of " + d);
                Assert.False(string.IsNullOrWhiteSpace(d.Message), "a message for " + d);
            }
            if (g.HasErrors) return;
            var fields = CSharpSyntaxTree.ParseText(g.Source).GetRoot().DescendantNodes().OfType<VariableDeclaratorSyntax>().Select(v => v.Identifier.Text).ToHashSet();
            foreach (var b in g.Bindings)
            {
                Assert.Contains(b.Field, fields);
                Assert.True(b.Event < p.Events.Count && b.Variable < p.Variables.Count, "binding " + b.Field);
                if (b.Event >= 0 && b.Action >= 0) Assert.True(b.Action < Actions(b.Event), "binding " + b.Field);
            }
        }, iter: IterOf(kind), threads: 1, print: Show);
    }

    // ---------------- values in fields ----------------

    /// <summary>
    /// The same trigger with every value typed in the Inspector changed (<paramref name="n"/> picks the new values):
    /// texts, numbers, on/off, positions, colors, rotations in actions and conditions, initial values, delays. What
    /// picks code stays: choices, variable / timer / event names, whether there is a delay at all, on/off in conditions.
    /// </summary>
    static TriggerProgram WithOtherValues(TriggerProgram p, int n)
    {
        object Other(object value)
        {
            n++;
            switch (value)
            {
                case bool b: return n % 2 == 0 ? !b : b;
                case int i: return i + n % 7 + 1;
                case float f: return f + 0.25f * (n % 5 + 1);
                case string t: return t.IndexOfAny(new[] { '{', '}' }) >= 0 ? t : t + " v" + n; // a template's shape stays
                case float[] a: return a.Select((x, k) => x + n + k).ToArray();
                default: return value;
            }
        }
        ArgValue Arg(ActionParam prm, ArgValue a)
        {
            if (a == null || a.Source != ArgSource.Constant) return a;
            bool picksCode = prm == null || prm.Choices != null || prm.VariableRef || prm.TimerRef || prm.RemoteVariableRef || prm.Name == "event" || prm.Name == "events"
                || prm.Type == null || prm.Type.Kind == ValueKind.Enum || prm.Type.Kind == ValueKind.Url;
            return picksCode ? a : ArgValue.Const(Other(a.Constant));
        }
        // A condition on on/off is written as x or !x (no comparison), so its true / false picks code too.
        Condition Cond(Condition c) => c == null ? null : new Condition { Variable = c.Variable, Op = c.Op, Negate = c.Negate,
            Value = c.Value?.Source == ArgSource.Constant && !(c.Value.Constant is bool) ? ArgValue.Const(Other(c.Value.Constant)) : c.Value };
        ActionCall Act(ActionCall a)
        {
            if (a == null) return null;
            var spec = ActionCatalog.Get(a.ActionId);
            return new ActionCall
            {
                ActionId = a.ActionId, Call = a.Call, ResultVariable = a.ResultVariable, RemoteType = a.RemoteType, RemoteTemporary = a.RemoteTemporary, MatchAny = a.MatchAny,
                Args = a.Args.Select((x, k) => Arg(spec != null && k < spec.Params.Length ? spec.Params[k] : null, x)).ToList(),
                Conditions = a.Conditions.Select(Cond).ToList(), Then = a.Then.Select(Act).ToList(), Else = a.Else.Select(Act).ToList(),
            };
        }
        return new TriggerProgram
        {
            ContinuousSync = p.ContinuousSync,
            Variables = p.Variables.Select(v => new VariableDecl { Name = v.Name, Type = v.Type, Synced = v.Synced, Temporary = v.Temporary, External = v.External, SaveKey = v.SaveKey, Interpolate = v.Interpolate,
                Initial = v.Type?.Kind == ValueKind.Enum || v.Type?.Kind == ValueKind.Url ? v.Initial : Other(v.Initial) }).ToList(),
            Events = p.Events.Select(e => new EventBlock
            {
                EventId = e.EventId, Name = e.Name, Broadcast = e.Broadcast, PlayerFilter = e.PlayerFilter, Gate = e.Gate, GateNames = e.GateNames?.Select(x => x + "~").ToList(),
                InteractText = e.InteractText, HasUiSource = e.HasUiSource, Listen = e.Listen, MatchAny = e.MatchAny,
                DelaySeconds = e.DelaySeconds > 0 ? e.DelaySeconds + 1f : e.DelaySeconds,
                Timer = e.Timer == null ? null : new TimerSpec { MinSeconds = e.Timer.MinSeconds + 1f, MaxSeconds = e.Timer.MaxSeconds + 1f, Repeat = e.Timer.Repeat, AutoStart = e.Timer.AutoStart },
                Conditions = e.Conditions.Select(Cond).ToList(), Actions = e.Actions.Select(Act).ToList(),
            }).ToList(),
        };
    }

    [Theory, InlineData("small"), InlineData("big")]
    public void ChangingOnlyValuesChangesNoCode(string kind)
    {
        int compared = 0;
        Gen.Select(Triggers(kind), Gen.Int[0, 1000]).Sample((p, n) =>
        {
            var a = Generate(p, false);
            var b = Generate(WithOtherValues(p, n), false);
            if (a.HasErrors || b.HasErrors) return;
            compared++;
            // The same class (so no script compile): only what the editor fills in differs.
            Assert.True(a.Source == b.Source, "the code changed:\n" + FirstDifference(a.Source.Replace(a.ClassName, "C"), b.Source.Replace(b.ClassName, "C")));
            Assert.Equal(a.Bindings.Select(x => x.Field), b.Bindings.Select(x => x.Field));
        }, iter: IterOf(kind), threads: 1, print: t => Show(t.Item1));
        Assert.True(compared > IterOf(kind) / 5, "only " + compared + " pairs were compared");
    }

    static string FirstDifference(string a, string b)
    {
        var la = a.Split('\n');
        var lb = b.Split('\n');
        for (int i = 0; i < Math.Min(la.Length, lb.Length); i++)
            if (la[i] != lb[i]) return "line " + (i + 1) + ":\n- " + la[i] + "\n+ " + lb[i];
        return "lengths " + la.Length + " / " + lb.Length;
    }

    // ---------------- short triggers working together ----------------

    /// <summary>A trigger of a scene, and for the actions that reach the next trigger: their numbers (event, action).</summary>
    sealed class SceneTrigger
    {
        public TriggerProgram Program;
        public readonly List<(int ev, int act)> Reaching = new List<(int, int)>();
    }

    /// <summary>A value of a kind, from a number (for the values a wiring step puts in).</summary>
    static object ValueOf(ValueKind kind, int n) => kind switch
    {
        ValueKind.Bool => n % 2 == 0,
        ValueKind.Int => n,
        ValueKind.Float => n * 0.5f,
        ValueKind.String => "s" + n,
        ValueKind.Vector2 => new float[] { n, 1 },
        ValueKind.Vector3 or ValueKind.Quaternion => new float[] { n, 1, 2 },
        ValueKind.Color => new float[] { 1, 0, 0, 1 },
        _ => null,
    };

    /// <summary>
    /// Each trigger reaches the next one around the ring (the last reaches the first): sets or reads one of its
    /// variables, or sends it one of its Custom events, as the Inspector sets these up (the receiving variable takes
    /// values from outside, the type is the receiver's).
    /// </summary>
    static List<SceneTrigger> Wire(List<TriggerProgram> programs, List<(int kind, int caller, int pick, int where, int how)> links)
    {
        var scene = programs.Select(p => new SceneTrigger { Program = p }).ToList();
        foreach (var (kind, callerPick, pick, where, how) in links)
        {
            var caller = scene[callerPick % scene.Count];
            var receiver = scene[(callerPick % scene.Count + 1) % scene.Count].Program;
            var events = caller.Program.Events;
            if (events.Count == 0) continue; // every card was refused and taken out
            var block = events[where % events.Count];
            ActionCall call;
            if (kind == 2)
            {
                // Send one of its Custom events (one is added when it has none).
                var names = receiver.Events.Where(e => e.EventId == EventCatalog.CustomId).Select(e => e.Name).Distinct().ToList();
                if (names.Count == 0) { receiver.Events.Add(new EventBlock { EventId = EventCatalog.CustomId, Name = "Open", Actions = { Log("opened") } }); names.Add("Open"); }
                call = new ActionCall { ActionId = ActionCatalog.SendEventId, Args = { ArgValue.Objs(1), ArgValue.Const(names[pick % names.Count]), ArgValue.Const(how % 3) } };
            }
            else
            {
                var usable = receiver.Variables.Where(v => !v.Temporary && ValueOf(v.Type.Kind, 0) != null && !v.Type.IsArray).ToList();
                if (usable.Count == 0) continue;
                var v = usable[pick % usable.Count];
                if (kind == 0)
                {
                    v.External = true; // the editor marks a variable another trigger sets
                    call = new ActionCall { ActionId = ActionCatalog.SetRemoteId, Args = { ArgValue.Objs(1), ArgValue.Const(v.Name), ArgValue.Const(ValueOf(v.Type.Kind, how)) }, RemoteType = v.Type };
                }
                else
                {
                    var into = caller.Program.Variables.FirstOrDefault(x => !x.Synced && CodeGenerator.IsAssignable(x.Type, v.Type));
                    if (into == null) { into = new VariableDecl { Name = "got" + caller.Program.Variables.Count, Type = v.Type, Initial = ValueOf(v.Type.Kind, 0) }; caller.Program.Variables.Add(into); }
                    call = new ActionCall { ActionId = ActionCatalog.GetRemoteId, Args = { ArgValue.Objs(1), ArgValue.Const(v.Name), ArgValue.Const(into.Name) }, RemoteType = v.Type };
                }
            }
            caller.Reaching.Add((events.IndexOf(block), ActionCall.Flatten(block.Actions).Count));
            block.Actions.Add(call);
        }
        return scene;
    }

    static readonly Gen<List<SceneTrigger>> Scene =
        from programs in PlausibleOf(VariableNames, 3, 1, 3, 1, 3, 1).Select(Repaired).List[2, 4]
        from links in Gen.Select(Gen.Int[0, 2], Gen.Int[0, 99], Gen.Int[0, 99], Gen.Int[0, 99], Gen.Int[0, 99]).List[1, 6]
        select Wire(programs, links);

    static string ShowScene(List<SceneTrigger> scene) => string.Join("\n---\n", scene.Select((t, i) => "trigger " + i + " (reaches " + (i + 1) % scene.Count + ")\n" + Show(t.Program)));

    [Fact]
    public void WhatATriggerCallsOnAnotherIsThereInTheOthersCode()
    {
        int checkedCalls = 0;
        Scene.Sample(scene =>
        {
            var generated = scene.Select(t => Generate(t.Program, false)).ToList();
            for (int i = 0; i < scene.Count; i++)
            {
                var g = generated[i];
                var other = generated[(i + 1) % scene.Count];
                if (g.HasErrors || other.HasErrors) continue;
                Assert.Empty(SyntaxErrors(g.Source));
                // The fields that hold the next trigger (filled by the editor), and what that trigger's class has.
                var reaching = g.Bindings.Where(b => b.Arg == 0 && scene[i].Reaching.Contains((b.Event, b.Action))).Select(b => b.Field).ToHashSet();
                var theirs = CSharpSyntaxTree.ParseText(other.Source).GetRoot();
                var fields = theirs.DescendantNodes().OfType<FieldDeclarationSyntax>().Where(f => f.Modifiers.Any(SyntaxKind.PublicKeyword))
                    .SelectMany(f => f.Declaration.Variables.Select(v => (name: v.Identifier.Text, type: f.Declaration.Type.ToString()))).ToList();
                var methods = theirs.DescendantNodes().OfType<MethodDeclarationSyntax>()
                    .Where(m => m.Modifiers.Any(SyntaxKind.PublicKeyword) && m.ParameterList.Parameters.Count == 0).Select(m => m.Identifier.Text).ToHashSet();
                var mine = CSharpSyntaxTree.ParseText(g.Source).GetRoot();
                foreach (var call in mine.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (call.Expression is not MemberAccessExpressionSyntax access || access.Expression is not IdentifierNameSyntax target) continue;
                    if (!reaching.Contains(FieldBehind(mine, target.Identifier.Text))) continue;
                    var args = call.ArgumentList.Arguments;
                    // A name written in, or in a field the editor fills (values in fields).
                    string Name(int k) => args[k].Expression is LiteralExpressionSyntax literal ? literal.Token.ValueText
                        : args[k].Expression is IdentifierNameSyntax id ? g.Bindings.FirstOrDefault(x => x.Field == id.Identifier.Text && x.Kind == BindingKind.Constant)?.Constant as string : null;
                    var what = access.Name.Identifier.Text;
                    checkedCalls++;
                    switch (what)
                    {
                        case "SetProgramVariable":
                            Assert.Contains(Name(0), fields.Select(f => f.name));
                            break;
                        case "GetProgramVariable":
                            // Read as the type the other trigger declares.
                            var field = fields.FirstOrDefault(f => f.name == Name(0));
                            Assert.True(field.name != null, Name(0) + " is not a field of trigger " + (i + 1) % scene.Count);
                            Assert.Equal(field.type, CastOf(call));
                            break;
                        case "SendCustomEvent":
                            Assert.Contains(Name(0), methods);
                            break;
                        case "SendCustomNetworkEvent":
                            Assert.Contains(Name(1), methods);
                            Assert.False(Name(1).StartsWith("_"), "the network can't call " + Name(1));
                            break;
                        default:
                            checkedCalls--;
                            break;
                    }
                }
            }
        }, iter: Iter, threads: 1, print: ShowScene);
        Assert.True(checkedCalls > Iter, "only " + checkedCalls + " calls to another trigger were checked");
    }

    /// <summary>The field a name stands for: itself, or the array a loop variable is read from ("T t = field[i];").</summary>
    static string FieldBehind(SyntaxNode root, string name)
    {
        var local = root.DescendantNodes().OfType<VariableDeclaratorSyntax>().FirstOrDefault(v => v.Identifier.Text == name && v.Initializer?.Value is ElementAccessExpressionSyntax);
        return local != null && ((ElementAccessExpressionSyntax)local.Initializer.Value).Expression is IdentifierNameSyntax array ? array.Identifier.Text : name;
    }

    /// <summary>The type a GetProgramVariable result is cast to: "(T)x.GetProgramVariable(...)".</summary>
    static string CastOf(InvocationExpressionSyntax call) => call.Ancestors().OfType<CastExpressionSyntax>().FirstOrDefault()?.Type.ToString();

    // ---------------- pieces of the code ----------------

    static object Evaluate(string expression)
    {
        var e = SyntaxFactory.ParseExpression(expression);
        if (e is PrefixUnaryExpressionSyntax neg && neg.IsKind(SyntaxKind.UnaryMinusExpression)) return -(float)((LiteralExpressionSyntax)neg.Operand).Token.Value;
        return ((LiteralExpressionSyntax)e).Token.Value;
    }

    [Fact]
    public void AnyTextIsWrittenAsALiteralOfTheSameText()
    {
        AnyString.Sample(s =>
        {
            var literal = CodeGenerator.StringLiteral(s);
            Assert.Empty(SyntaxErrors("class C { string s = " + literal + "; }"));
            Assert.Equal(s, Evaluate(literal));
            // Nothing in it ends a line of the file (a line comment or the line count would change).
            Assert.DoesNotContain(literal, c => c == '\n' || c == '\r' || c == '\u0085' || c == '\u2028' || c == '\u2029');
        }, iter: Iter * 10, threads: 1);
    }

    [Fact]
    public void AnyNumberIsWrittenAsALiteralOfTheSameNumber()
    {
        Float.Sample(f =>
        {
            var literal = CodeGenerator.Literal(ValueKind.Float, f);
            var back = (float)Evaluate(literal);
            // Udon has no NaN or infinity constants: they become 0.
            Assert.Equal(float.IsFinite(f) ? BitConverter.SingleToInt32Bits(f) : 0, BitConverter.SingleToInt32Bits(back));
        }, iter: Iter * 10, threads: 1);
        Gen.Int.Sample(i =>
        {
            var literal = CodeGenerator.Literal(ValueKind.Int, i);
            Assert.Empty(SyntaxErrors("class C { int i = " + literal + "; }"));
            Assert.Equal(i, int.Parse(literal, System.Globalization.CultureInfo.InvariantCulture));
        }, iter: Iter * 10, threads: 1);
    }

    [Fact]
    public void AnyVariableNameBecomesAnIdentifierAndTwoNamesSharingOneAreRefused()
    {
        var name = Gen.OneOf(Gen.OneOfConst(VariableNames), Gen.String[Gen.Char['\0', '\uffff'], 1, 40], Gen.String[Gen.Char.AlphaNumeric, 1, 8])
            .Where(s => CodeGenerator.CheckVariableName(s) == null);
        // The second name is now and then the first one's identifier itself ("あ" and "uni_3042").
        var pair = from a in name from b in name from same in Gen.Int[0, 2] select (a, b: same == 0 ? CodeGenerator.Ident(a) : b);
        int clashes = 0;
        pair.Sample(t =>
        {
            var (a, b) = t;
            var id = CodeGenerator.Ident(a);
            Assert.True(SyntaxFacts.IsValidIdentifier(id) && SyntaxFacts.GetKeywordKind(id) == SyntaxKind.None, a + " → " + id);
            if (a == b || CodeGenerator.Ident(b) != id || CodeGenerator.CheckVariableName(b) != null) return;
            // Different names, one identifier: the trigger says so instead of failing to compile.
            clashes++;
            var p = new TriggerProgram { Variables = { new VariableDecl { Name = a, Kind = ValueKind.Int }, new VariableDecl { Name = b, Kind = ValueKind.Int } } };
            Assert.True(CodeGenerator.Generate(p).HasErrors, a + " / " + b);
        }, iter: Iter * 10, threads: 1);
        Assert.True(clashes > 0, "no two names shared an identifier");
    }

    [Fact]
    public void AHistoryNoteReadsBackAsWritten()
    {
        // A value may hold anything but the separator between parts (the note's own format).
        var part = AnyString.Where(s => !s.Contains('\u001f'));
        var note =
            from ran in Gen.Bool
            from ev in Gen.Int[0, 500]
            from values in Gen.Select(Gen.OneOfConst(VariableNames).Where(v => CodeGenerator.CheckVariableName(v) == null), part).List[0, 4]
            select (ran, ev, values);
        note.Sample(t =>
        {
            var text = (t.ran ? "r" : "s") + t.ev + string.Concat(t.values.Select(v => "\u001f" + v.Item1 + "\u001e" + v.Item2));
            var entry = CodeGenerator.ParseTrace(text);
            Assert.Equal(t.ran, entry.Ran);
            Assert.Equal(t.ev, entry.Event);
            Assert.Equal(t.values.Select(v => v.Item1 + "=" + v.Item2), entry.Values.Select(v => v.Key + "=" + v.Value));
        }, iter: Iter * 10, threads: 1);
    }

    // ---------------- the loop check ----------------

    /// <summary>Every cycle of a small graph, by trying each path (the reference the loop check is held to).</summary>
    static HashSet<string> AllCycles(int nodes, HashSet<(int, int)> links)
    {
        var found = new HashSet<string>();
        void Walk(int start, List<int> path)
        {
            int last = path[path.Count - 1];
            for (int next = start; next < nodes; next++)
            {
                if (!links.Contains((last, next))) continue;
                if (next == start) found.Add(string.Join(",", path));
                else if (!path.Contains(next)) { path.Add(next); Walk(start, path); path.RemoveAt(path.Count - 1); }
            }
        }
        for (int s = 0; s < nodes; s++) Walk(s, new List<int> { s });
        return found;
    }

    static readonly Gen<(int nodes, HashSet<(int, int)> links)> Graph =
        from nodes in Gen.Int[1, 7]
        from links in Gen.Select(Gen.Int[0, nodes - 1], Gen.Int[0, nodes - 1]).List[0, nodes * 3]
        select (nodes, links: links.ToHashSet());

    [Fact]
    public void TheLoopCheckFindsExactlyTheCyclesOfASmallGraph()
    {
        Graph.Sample(g =>
        {
            var cycles = CodeGenerator.Cycles(Enumerable.Range(0, g.nodes), x => g.links.Where(l => l.Item1 == x).Select(l => l.Item2), maxCycles: 100000, maxSteps: 1000000);
            var expected = AllCycles(g.nodes, g.links);
            Assert.Equal(expected.OrderBy(x => x), cycles.Select(c => string.Join(",", c)).OrderBy(x => x));
        }, iter: Iter * 3, threads: 1, print: g => g.nodes + " nodes: " + string.Join(" ", g.links.Select(l => l.Item1 + ">" + l.Item2)));
    }

    [Fact]
    public void WithItsLimitsTheLoopCheckStillReportsOnlyRealCyclesAndMissesNone()
    {
        var dense =
            from nodes in Gen.Int[1, 12]
            from links in Gen.Select(Gen.Int[0, nodes - 1], Gen.Int[0, nodes - 1]).List[0, nodes * nodes]
            select (nodes, links: links.ToHashSet());
        dense.Sample(g =>
        {
            var cycles = CodeGenerator.Cycles(Enumerable.Range(0, g.nodes), x => g.links.Where(l => l.Item1 == x).Select(l => l.Item2), maxCycles: 5, maxSteps: 2000);
            foreach (var c in cycles)
            {
                Assert.Equal(c.Min(), c[0]); // listed from its smallest node
                Assert.Equal(c.Count, c.Distinct().Count());
                for (int i = 0; i < c.Count; i++) Assert.Contains((c[i], c[(i + 1) % c.Count]), g.links);
            }
            Assert.Equal(cycles.Count, cycles.Select(c => string.Join(",", c)).Distinct().Count());
            // A node on some cycle: at least one cycle is reported for the graph.
            bool any = Enumerable.Range(0, g.nodes).Any(s => Reaches(g.links, s, s));
            Assert.Equal(any, cycles.Count > 0);
        }, iter: Iter * 3, threads: 1, print: g => g.nodes + " nodes: " + string.Join(" ", g.links.Select(l => l.Item1 + ">" + l.Item2)));
    }

    static bool Reaches(HashSet<(int, int)> links, int from, int to)
    {
        var seen = new HashSet<int>();
        var stack = new Stack<int>(links.Where(l => l.Item1 == from).Select(l => l.Item2));
        while (stack.Count > 0)
        {
            int x = stack.Pop();
            if (x == to) return true;
            if (!seen.Add(x)) continue;
            foreach (var l in links) if (l.Item1 == x) stack.Push(l.Item2);
        }
        return false;
    }
}
