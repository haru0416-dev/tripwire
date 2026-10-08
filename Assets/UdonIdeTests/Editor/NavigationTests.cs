using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace UdonIde.Tests
{
    /// <summary>Other U# scripts and third-party U#: definitions, documentation, availability, completion, references.</summary>
    public class NavigationTests
    {
        const string Door = "Assets/UdonIdeTests/Fixtures/Door.cs";
        const string HelperPath = "Assets/UdonIdeTests/Fixtures/StageB/Helper.cs";
        const string ToyPath = "Assets/UdonIdeTests/Fixtures/ThirdParty/ToyPlayer/ToyPlayer.cs";

        static readonly string Text = @"using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using ToyMedia;

public class Door : UdonSharpBehaviour
{
    public Helper helper;
    public ToyPlayer toy;
    public VRCUrl link;
    public VRC.Udon.UdonBehaviour plain;
    bool open;

    public override void Interact()
    {
        helper.Ping();
        helper.SendCustomEvent(""Ping"");
        helper.SendCustomEvent(nameof(Helper.Ping));
        toy.Play(link);
        var p = transform.position;
        System.IO.File.Exists(""x"");
        SendCustomEvent(""Interact"");
        plain.SendCustomEvent(""Anything"");
        // here
    }

    void Secret() { }
    public void _Local() { }
}
";

        [SetUp]
        public void Fresh() { UdonCheck.SetUnsaved(new Dictionary<string, string>()); UdonCheck.Rescan(); }

        static (SemanticModel model, Microsoft.CodeAnalysis.CSharp.CSharpCompilation compilation) Model(string text)
        {
            var (c, t) = UdonCheck.Compile(Door, text);
            return (c.GetSemanticModel(t), c);
        }

        static ISymbol At(string text, string marker, int into = 1)
        {
            var (model, _) = Model(text);
            return SymbolNav.At(model, text.IndexOf(marker) + into);
        }

        static int LineOf(string path, string contains) => File.ReadAllLines(path).ToList().FindIndex(l => l.Contains(contains));

        [Test]
        public void DefinitionsInOtherScriptsAndThirdParty()
        {
            var ping = SymbolNav.Definition(At(Text, "Ping();"));
            Assert.AreEqual(HelperPath, ping?.Path);
            Assert.AreEqual(LineOf(HelperPath, "public void Ping()"), ping?.Line);
            var play = SymbolNav.Definition(At(Text, "Play(link)"));
            Assert.AreEqual(ToyPath, play?.Path);
            Assert.AreEqual(LineOf(ToyPath, "public void Play("), play?.Line);
            var fromString = SymbolNav.Definition(At(Text, "\"Ping\"", 2));
            Assert.AreEqual(HelperPath, fromString?.Path, "the SendCustomEvent name leads to the method");
            Assert.IsNull(SymbolNav.Definition(At(Text, "position")), "Unity's API has no source here");
        }

        [Test]
        public void DocumentationFromCommentsAndUnityXml()
        {
            var (_, c) = Model(Text);
            var play = SymbolNav.Documentation(At(Text, "Play(link)"), c);
            StringAssert.Contains("Starts playing link for everyone", play);
            StringAssert.Contains("link: The video URL.", play);
            StringAssert.Contains("The world space position of the Transform.", SymbolNav.Documentation(At(Text, "position"), c));
            var describe = SymbolNav.Describe(At(Text, "toy;"), c);
            StringAssert.Contains("(フィールド)", describe);
            StringAssert.Contains("U# スクリプト", describe);
        }

        [Test]
        public void WhatUdonSharpCanUse()
        {
            Assert.AreEqual(Availability.ProjectUdonSharp, SymbolNav.Udon(At(Text, "Ping();")));
            Assert.AreEqual(Availability.ProjectUdonSharp, SymbolNav.Udon(At(Text, "ToyPlayer toy")));
            Assert.AreEqual(Availability.Udon, SymbolNav.Udon(At(Text, "position")));
            Assert.AreEqual(Availability.NotExposed, SymbolNav.Udon(At(Text, "Exists")));
            Assert.AreEqual(Availability.Udon, SymbolNav.Udon(At(Text, "SendCustomEvent(\"Interact\")")));
        }

        static List<Completion> Complete(string text, string after)
        {
            var (c, t) = UdonCheck.Compile(Door, text);
            return UdonCompletion.At(c, t, text.IndexOf(after) + after.Length).Items;
        }

        [Test]
        public void CompletionListsOtherScriptsAsUsable()
        {
            var text = Text.Replace("helper.Ping();", "helper.");
            var items = Complete(text, "helper.");
            foreach (var n in new[] { "Ping", "Twice", "value" })
                Assert.IsTrue(items.Any(i => i.Name == n && i.Exposed), n + " is listed as usable");
            Assert.IsTrue(items.TakeWhile(i => i.Exposed).Any(i => i.Name == "Ping"), "usable ones come first");

            var toy = Complete(Text.Replace("toy.Play(link);", "toy."), "toy.");
            foreach (var n in new[] { "Play", "Stop", "IsPlaying", "volume" }) Assert.IsTrue(toy.Any(i => i.Name == n && i.Exposed), n);
            Assert.IsFalse(toy.Any(i => i.Name == "Notify"), "private members of another class are not offered");
            CollectionAssert.AreEqual(new[] { "IsPlaying", "listeners", "Play", "Stop", "volume" }, toy.Take(5).Select(i => i.Name).ToArray(), "the class's own members come first");

            var go = Complete(Text.Replace("var p = transform.position;", "gameObject."), "gameObject.");
            Assert.IsTrue(go.Any(i => i.Name == "AddComponent" && !i.Exposed), "AddComponent is listed, dimmed");
            Assert.IsTrue(go.Any(i => i.Name == "SetActive" && i.Exposed));

            var scope = Complete(Text.Replace("// here", "op"), "        op");
            Assert.IsTrue(scope.Any(i => i.Name == "open"), "fields in scope");
            Assert.IsTrue(scope.Any(i => i.Name == "helper"));
            Assert.IsTrue(scope.Any(i => i.Name == "foreach" && i.Kind == CompletionKind.Keyword));
        }

        [Test]
        public void ReferencesIncludeEventNames()
        {
            var (_, c) = Model(Text);
            var refs = SymbolNav.References(c, At(Text, "Ping();"));
            var lines = refs.Select(r => r.Path + ":" + r.LineText).ToList();
            Assert.IsTrue(lines.Any(l => l.StartsWith(HelperPath) && l.Contains("public void Ping()")), "the definition: " + string.Join(" | ", lines));
            Assert.IsTrue(lines.Any(l => l.StartsWith(Door) && l.Contains("helper.Ping();")), "the call");
            Assert.IsTrue(lines.Any(l => l.StartsWith(Door) && l.Contains("SendCustomEvent(\"Ping\")")), "the event name string");
            Assert.IsTrue(lines.Any(l => l.StartsWith(Door) && l.Contains("nameof(Helper.Ping)")), "nameof");
        }

        [Test]
        public void EventNamesThatWontReachAMethod()
        {
            var text = Text.Replace("        // here\n    }", @"        helper.SendCustomEvent(""Pnig"");
        SendCustomEvent(""Secret"");
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, ""_Local"");
        SendCustomEventDelayedSeconds(nameof(Interact), 1f);
    }");
            var (model, _) = Model(text);
            var problems = SymbolNav.EventNameProblems(model).Select(p => p.Message).ToList();
            Assert.AreEqual(3, problems.Count, string.Join("\n", problems));
            StringAssert.Contains("「Pnig」", problems[0]); StringAssert.Contains("「Ping」の打ち間違い", problems[0]);
            StringAssert.Contains("public ではない", problems[1]);
            StringAssert.Contains("_ で始まる", problems[2]);
        }

        [Test]
        public void UnsavedTabsCount()
        {
            var helper = File.ReadAllText(HelperPath).Replace("public void Ping()", "public void Extra() { }\n    public void Ping()");
            UdonCheck.SetUnsaved(new Dictionary<string, string> { { HelperPath, helper } });
            var text = Text.Replace("helper.Ping();", "helper.");
            Assert.IsTrue(Complete(text, "helper.").Any(i => i.Name == "Extra"), "the unsaved method is offered");
            UdonCheck.SetUnsaved(new Dictionary<string, string>());
            Assert.IsFalse(Complete(text, "helper.").Any(i => i.Name == "Extra"), "gone once the tab is closed unsaved");
        }

        [Test]
        public void PackagesOpenReadOnly()
        {
            Assert.IsTrue(SymbolNav.IsReadOnlyPath("Packages/com.vrchat.worlds/Integrations/UdonSharp/Runtime/UdonSharpBehaviour.cs"));
            Assert.IsFalse(SymbolNav.IsReadOnlyPath(ToyPath));
        }
    }
}
