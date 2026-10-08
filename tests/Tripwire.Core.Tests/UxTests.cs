using System.Linq;
using Tripwire.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using static TestKit;

public class UxTests
{
    [Fact]
    public void MenusListEveryCatalogEntryExactlyOnceInAKnownCategory()
    {
        Assert.Equal(EventCatalog.All.Select(e => e.Id).OrderBy(x => x), EventCatalog.InMenuOrder.Select(e => e.Id).OrderBy(x => x));
        Assert.Equal(ActionCatalog.All.Select(a => a.Id).OrderBy(x => x), ActionCatalog.InMenuOrder.Select(a => a.Id).OrderBy(x => x));
        Assert.All(EventCatalog.InMenuOrder, e => Assert.Contains(e.Category, Texts.EventCategories));
        Assert.All(ActionCatalog.InMenuOrder, a => Assert.Contains(a.Category, Texts.ActionCategories));
        // The Udon API escape hatch is only reachable from the advanced category.
        Assert.Equal("Advanced", ActionCatalog.Get(ActionCatalog.CallId).Category);
    }

    [Fact]
    public void EverythingHasJapaneseText()
    {
        var prev = Texts.Language;
        Texts.Language = UiLanguage.Japanese;
        try
        {
            foreach (var e in EventCatalog.All)
            {
                Assert.NotEqual(e.DisplayName, Texts.EventName(e));
                Assert.NotEqual(e.Description, Texts.EventDescription(e));
                foreach (var v in e.Params) Assert.NotEqual(v.Name, Texts.EventValueName(e, v.Name));
            }
            foreach (var a in ActionCatalog.All)
            {
                // Flow constructs keep their English keywords (If, Repeat, Break...) on purpose; their descriptions are Japanese.
                if (a.Category == "Flow" && a.DisplayName == Texts.ActionName(a)) Assert.NotEqual(a.Description, Texts.ActionDescription(a));
                else Assert.NotEqual(a.DisplayName, Texts.ActionName(a));
                foreach (var p in a.Params) Assert.NotEqual(p.Name, Texts.Param(p.Name));
            }
            foreach (var c in Texts.EventCategories.Concat(Texts.ActionCategories)) Assert.NotEqual(c, Texts.Category(c));
        }
        finally { Texts.Language = prev; }
    }

    [Fact]
    public void EveryTypeHasAPlainDescription()
    {
        // The type popup's hover text: plain words for every kind of value, never the C# type name.
        var prev = Texts.Language;
        Texts.Language = UiLanguage.Japanese;
        try
        {
            foreach (var kind in System.Enum.GetValues(typeof(ValueKind)).Cast<ValueKind>())
            {
                var d = Texts.TypeDescription(ParamType.Of(kind));
                Assert.False(string.IsNullOrEmpty(d), kind.ToString());
                Assert.DoesNotContain("System.", d);
            }
            var list = new ParamType(ValueKind.Object, "UnityEngine.GameObject[]") { IsArray = true };
            Assert.Equal("同じ種類の値を、いくつも順に持ちます。", Texts.TypeDescription(list));
        }
        finally { Texts.Language = prev; }
    }

    [Fact]
    public void JapaneseVariableNamesBecomeSafeIdentifiers()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "扉が開いている", Kind = ValueKind.Bool, Synced = true });
        p.Variables.Add(new VariableDecl { Name = "count", Kind = ValueKind.Int });
        var e = new EventBlock { EventId = "Interact" };
        e.Actions.Add(new ActionCall { ActionId = "Variable.Toggle", Args = { ArgValue.Const("扉が開いている") } });
        e.Conditions.Add(new Condition { Variable = "count", Op = CompareOp.Less, Value = ArgValue.Const(3) });
        p.Events.Add(e);
        var changed = new EventBlock { EventId = "OnVariableChanged", Name = "扉が開いている" };
        changed.Actions.Add(new ActionCall { ActionId = "Debug.Log", Args = { ArgValue.Const("開いた") } });
        p.Events.Add(changed);

        var g = CodeGenerator.Generate(p);
        Assert.False(g.HasErrors, string.Join("\n", g.Diagnostics));
        var id = CodeGenerator.Ident("扉が開いている");
        Assert.Matches("^uni_[0-9a-f]+$", id);
        Assert.Contains("[UdonSynced] public bool v_" + id + " = false;", g.Source);
        AssertParses(g);
    }

    [Fact]
    public void DiagnosticsFollowTheLanguage()
    {
        var p = new TriggerProgram();
        p.Variables.Add(new VariableDecl { Name = "", Kind = ValueKind.Bool });
        var prev = Texts.Language;
        try
        {
            Texts.Language = UiLanguage.Japanese;
            Assert.Contains("名前", CodeGenerator.Generate(p).Diagnostics.Single().Message);
            Texts.Language = UiLanguage.English;
            Assert.Contains("name", CodeGenerator.Generate(p).Diagnostics.Single().Message);
        }
        finally { Texts.Language = prev; }
    }
}
