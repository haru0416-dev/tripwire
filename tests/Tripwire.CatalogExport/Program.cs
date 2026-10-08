// Writes Tripwire's event / action catalog as JSON for the docs site, in both languages, so the reference pages show
// exactly what the Inspector shows. Usage: dotnet run -- <output.json>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Tripwire.Core;

static class Program
{
    static object Both(Func<string> text)
    {
        var prev = Texts.Language;
        try
        {
            Texts.Language = UiLanguage.Japanese; var ja = text();
            Texts.Language = UiLanguage.English; var en = text();
            return new { ja, en };
        }
        finally { Texts.Language = prev; }
    }

    /// <summary>How the type is written in UdonSharp, for the "in Udon" lines.</summary>
    static string CsType(ParamType t)
    {
        if (t == null) return "";
        string one;
        switch (t.Kind)
        {
            case ValueKind.Bool: one = "bool"; break;
            case ValueKind.Int: one = "int"; break;
            case ValueKind.Float: one = "float"; break;
            case ValueKind.String: one = "string"; break;
            case ValueKind.Vector3: one = "Vector3"; break;
            case ValueKind.Vector2: one = "Vector2"; break;
            case ValueKind.Color: one = "Color"; break;
            case ValueKind.Quaternion: one = "Quaternion"; break;
            case ValueKind.Player: one = "VRCPlayerApi"; break;
            case ValueKind.Url: one = "VRCUrl"; break;
            default:
                var n = t.UnityType ?? "?";
                n = n.Substring(n.LastIndexOf('.') + 1);
                return n;
        }
        return t.IsArray ? one + "[]" : one;
    }

    /// <summary>The event's method as UdonSharp declares it, or null for Tripwire's own kinds (custom, timer...).</summary>
    static string Signature(EventSpec e) =>
        e.Shape == EventShape.Override || e.Shape == EventShape.UnityMessage
            ? (e.Shape == EventShape.Override ? "public override void " : "void ") + e.Method + "(" + string.Join(", ", e.Params.Select(p => CsType(p.Type) + " " + p.Name)) + ")"
            : null;

    static object Default(object d) =>
        d switch { null => null, float[] v => "(" + string.Join(", ", v) + ")", bool b => b ? "true" : "false", string s when s.Length == 0 => null, _ => d.ToString() };

    static int Main(string[] args)
    {
        if (args.Length != 1) { Console.Error.WriteLine("usage: dotnet run -- <output.json>"); return 1; }

        var categories = Texts.EventCategories.Select(c => new { id = c, kind = "event" })
            .Concat(Texts.ActionCategories.Select(c => new { id = c, kind = "action" }))
            .Select(c => new
            {
                c.id, c.kind,
                name = Both(() => Texts.Category(c.id)),
                advanced = c.kind == "event" && Texts.AdvancedCategories.Contains(c.id),
            });

        var events = EventCatalog.InMenuOrder.Select(e => new
        {
            e.Id, e.Category,
            code = Texts.EventCode(e),
            name = Both(() => Texts.EventName(e)),
            description = Both(() => Texts.EventDescription(e)),
            values = e.Params.Select(p => new { name = Both(() => Texts.EventValueName(e, p.Name)), type = Both(() => Texts.TypeName(p.Type)) }),
            frequent = e.Frequent,
            usesName = e.UsesName,
            signature = Signature(e),
            needs = Enum.GetValues(typeof(EventNeed)).Cast<EventNeed>().Where(n => EventCatalog.Needs(e, n)).Select(n => n.ToString()),
        });

        var actions = ActionCatalog.InMenuOrder.Select(a => new
        {
            a.Id, a.Category,
            code = Texts.ActionCode(a.Id),
            name = Both(() => Texts.ActionName(a)),
            description = Both(() => Texts.ActionDescription(a)),
            parameters = a.Params.Select(p => new
            {
                name = Both(() => Texts.Param(p.Name)),
                type = Both(() => p.VariableRef || p.RemoteVariableRef ? Texts.T("Variable", "変数") : p.TimerRef ? Texts.T("Timer", "タイマー")
                    : Texts.TypeName(p.Type) == "?" ? Texts.T("Number", "数") : Texts.TypeName(p.Type)), // "?": takes an integer or a number
                @default = p.Choices != null ? null : Default(p.Default),
            }),
            // The UdonSharp the action becomes, per target ({name} are the settings); null for blocks and calls.
            template = string.IsNullOrEmpty(a.Template) ? null : a.Template,
            holdsActions = a.HoldsActions,
            hasConditions = a.HasConditions,
        });

        var json = JsonSerializer.Serialize(new { categories, events, actions }, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // keep Japanese readable in diffs
        });
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0])));
        File.WriteAllText(args[0], json + "\n");
        Console.WriteLine($"{events.Count()} events, {actions.Count()} actions → {args[0]}");
        return 0;
    }
}
