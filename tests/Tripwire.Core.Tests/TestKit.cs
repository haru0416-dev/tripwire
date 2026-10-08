using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Tripwire.Core;
using Xunit;

/// <summary>Helpers the generator tests share (use with <c>using static TestKit;</c>).</summary>
public static class TestKit
{
    /// <summary>The tests check each event's own code; InlineTests turn the final tidy-up back on where they need it.</summary>
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void KeepBodiesSeparate() => CodeGenerator.KeepBodiesSeparate = true;

    /// <summary>The source parses as C# 9 (the language UdonSharp compiles).</summary>
    public static void AssertParses(GeneratedProgram g)
    {
        var errors = CSharpSyntaxTree.ParseText(g.Source, new CSharpParseOptions(LanguageVersion.CSharp9)).GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    /// <summary>No errors reported, and the source parses.</summary>
    public static GeneratedProgram Ok(GeneratedProgram g)
    {
        Assert.False(g.HasErrors, string.Join("\n", g.Diagnostics));
        AssertParses(g);
        return g;
    }

    /// <summary>
    /// A valid program's source with whitespace collapsed: asserts follow statement order, not indentation. Without the
    /// event history's notes (TraceTests check those): the other tests are about the code that does the work.
    /// </summary>
    public static string Flat(GeneratedProgram g) => Regex.Replace(WithoutTrace(Ok(g).Source), @"\s+", " ");

    /// <summary>The source without the history's notes, ring and method (CodeGenerator.Trace).</summary>
    public static string WithoutTrace(string source)
    {
        var s = Regex.Replace(source, @"\{ if \(tw_Trace\) Tw_Trace\(.*?\); return; \}", "return;");
        s = Regex.Replace(s, @"^ *(?:else )?if \(tw_Trace\) Tw_Trace\(.*?\);\n", "", RegexOptions.Multiline);
        s = Regex.Replace(s, @"^ *(?:bool tw_Trace|string\[\] tw_Log|int tw_LogN);\n", "", RegexOptions.Multiline);
        return Regex.Replace(s, @" *void Tw_Trace\(string note\)\n *\{\n.*?\n *\}\n\n", "", RegexOptions.Singleline);
    }

    public static string Flat(TriggerProgram p) => Flat(CodeGenerator.Generate(p));

    public static ActionCall Log(string text) => new ActionCall { ActionId = ActionCatalog.LogId, Args = { ArgValue.Const(text) } };
}
