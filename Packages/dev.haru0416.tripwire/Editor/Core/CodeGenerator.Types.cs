using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Tripwire.Core
{
    // Types and literals: C# names of value types, assignability, constant literals.
    public static partial class CodeGenerator
    {
        internal static string TypeName(ParamType t)
        {
            switch (t.Kind)
            {
                case ValueKind.Bool: return "bool";
                case ValueKind.Int: return "int";
                case ValueKind.Float: return "float";
                case ValueKind.String: return "string";
                case ValueKind.Vector3: return "Vector3";
                case ValueKind.Vector2: return "Vector2";
                case ValueKind.Color: return "Color";
                case ValueKind.Quaternion: return "Quaternion";
                case ValueKind.Player: return t.IsArray ? "VRCPlayerApi[]" : "VRCPlayerApi";
                case ValueKind.Url: return t.IsArray ? "VRCUrl[]" : "VRCUrl";
                case ValueKind.Other: return t.UnityType; // C# name already includes any []
                default: return t.UnityType + (t.IsArray ? "[]" : "");
            }
        }

        /// <summary>Fully qualified C# name, for type checks against the real types (see <see cref="TypeAssignable"/>).</summary>
        public static string FullTypeName(ParamType t)
        {
            string name;
            switch (t.Kind)
            {
                case ValueKind.Bool: name = "System.Boolean"; break;
                case ValueKind.Int: name = "System.Int32"; break;
                case ValueKind.Float: name = "System.Single"; break;
                case ValueKind.String: name = "System.String"; break;
                case ValueKind.Vector3: name = "UnityEngine.Vector3"; break;
                case ValueKind.Vector2: name = "UnityEngine.Vector2"; break;
                case ValueKind.Color: name = "UnityEngine.Color"; break;
                case ValueKind.Quaternion: name = "UnityEngine.Quaternion"; break;
                case ValueKind.Player: name = "VRC.SDKBase.VRCPlayerApi"; break;
                case ValueKind.Url: name = "VRC.SDKBase.VRCUrl"; break;
                case ValueKind.Other: return t.UnityType;
                default: name = t.UnityType; break;
            }
            return name + (t.IsArray ? "[]" : "");
        }

        /// <summary>
        /// Whether a value of type <c>have</c> can be passed where <c>want</c> is expected, by full C# names
        /// (subclasses, boxing). The editor installs a reflection-based check; without it only exact matches pass.
        /// </summary>
        public static Func<string, string, bool> TypeAssignable = (want, have) => want == have;

        /// <summary>
        /// The item type of a list type, or null when it isn't one. Object and player lists are marked IsArray; other
        /// arrays (int[], string[], Vector3[] ...) are Other types named "X[]". The editor installs a lookup of the real
        /// item type; this fallback knows the basic ones.
        /// </summary>
        public static Func<ParamType, ParamType> ElementTypeOf = t =>
        {
            if (t == null) return null;
            if (t.IsArray) return t.Element();
            if (t.Kind != ValueKind.Other || t.UnityType == null || !t.UnityType.EndsWith("[]", StringComparison.Ordinal)) return null;
            var name = t.UnityType.Substring(0, t.UnityType.Length - 2);
            switch (name)
            {
                case "System.Boolean": case "bool": return ParamType.Of(ValueKind.Bool);
                case "System.Int32": case "int": return ParamType.Of(ValueKind.Int);
                case "System.Single": case "float": return ParamType.Of(ValueKind.Float);
                case "System.String": case "string": return ParamType.Of(ValueKind.String);
                case "UnityEngine.Vector3": return ParamType.Of(ValueKind.Vector3);
                case "UnityEngine.Vector2": return ParamType.Of(ValueKind.Vector2);
                case "UnityEngine.Color": return ParamType.Of(ValueKind.Color);
                case "UnityEngine.Quaternion": return ParamType.Of(ValueKind.Quaternion);
                case "VRC.SDKBase.VRCUrl": return ParamType.Of(ValueKind.Url);
                default: return ParamType.OtherType(name);
            }
        };

        /// <summary>
        /// UdonSharp folds `new Struct(constants...)` at compile time by running the real constructor (off the main
        /// thread), so a constructor that throws for those values breaks the whole U# compile. The editor installs a
        /// check that tries the same construction and returns an error message, or null when fine / not applicable.
        /// </summary>
        public static Func<CallSpec, List<ArgValue>, string> CheckConstantConstruction;

        /// <summary>Value range of an integer type narrower than int, or null.</summary>
        public static (long min, long max)? NarrowIntRange(string type)
        {
            switch (type)
            {
                case "System.Byte": return (byte.MinValue, byte.MaxValue);
                case "System.SByte": return (sbyte.MinValue, sbyte.MaxValue);
                case "System.Int16": return (short.MinValue, short.MaxValue);
                case "System.UInt16": return (ushort.MinValue, ushort.MaxValue);
                default: return null;
            }
        }

        /// <summary>
        /// Text for a value of this type, or null when it has none: numbers, bools, vectors, colors and choices by their
        /// usual text; a player by display name; an object by name; a URL by its address. Null objects give "".
        /// </summary>
        public static string TextOf(ParamType have, string expr)
        {
            if (have == null || have.IsArray) return null;
            switch (have.Kind)
            {
                case ValueKind.String: return expr;
                case ValueKind.Int: case ValueKind.Float: case ValueKind.Bool: case ValueKind.Vector2: case ValueKind.Vector3:
                case ValueKind.Color: case ValueKind.Quaternion: case ValueKind.Enum:
                    return expr + ".ToString()";
                case ValueKind.Player: return "(Utilities.IsValid(" + expr + ") ? " + expr + ".displayName : \"\")";
                case ValueKind.Object: return "(Utilities.IsValid(" + expr + ") ? " + expr + ".name : \"\")";
                case ValueKind.Url: return "(" + expr + " != null ? " + expr + ".Get() : \"\")";
                default: return null;
            }
        }

        /// <summary>Whether a value of this type can be used where text is expected (see <see cref="TextOf"/>).</summary>
        public static bool IsTextConvertible(ParamType have) => TextOf(have, "x") != null;

        public static bool IsAssignable(ParamType want, ParamType have)
        {
            if (want == null || have == null) return false;
            // byte etc. parameters take int values (cast) and variables of their own type.
            if (want.Kind == ValueKind.Int && !want.IsArray && NarrowIntRange(want.UnityType) != null && have.Kind == ValueKind.Other && have.UnityType == want.UnityType && !have.IsArray)
                return true;
            if (!want.IsArray && !have.IsArray && want.Kind == ValueKind.Float && have.Kind == ValueKind.Int) return true;
            var w = FullTypeName(want);
            var h = FullTypeName(have);
            return w == h || TypeAssignable(w, h);
        }

        /// <summary>Temporary variables are reset from a constant: single values with a literal form only.</summary>
        public static bool CanBeTemporary(ParamType t) => t != null && !t.IsArray && HasLiteral(t.Kind);

        /// <summary>Kinds that have a C# literal form (and so a constant field initializer / Inspector value).</summary>
        static bool HasLiteral(ValueKind k) =>
            !(k == ValueKind.Object || k == ValueKind.Player || k == ValueKind.Other || k == ValueKind.Enum || k == ValueKind.Url);

        internal static string Literal(ValueKind kind, object value)
        {
            switch (kind)
            {
                case ValueKind.Bool:
                    return value is bool b && b ? "true" : "false";
                case ValueKind.Int:
                    return Convert.ToInt32(value ?? 0, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                case ValueKind.Float:
                    return FloatLiteral(Convert.ToSingle(value ?? 0f, CultureInfo.InvariantCulture));
                case ValueKind.String:
                    return StringLiteral(value as string ?? "");
                case ValueKind.Vector3:
                    return "new Vector3(" + Floats(value, 3) + ")";
                case ValueKind.Vector2:
                    return "new Vector2(" + Floats(value, 2) + ")";
                case ValueKind.Color:
                    return "new Color(" + Floats(value, 4) + ")";
                case ValueKind.Quaternion:
                    // Quaternion.Euler(const) is an extern on every call; a constant struct ctor is folded by U#
                    // (measured: 50 rotation sets 34.98 -> 28.14 us per call in the editor VM).
                    var e = value as float[] ?? new float[3];
                    var q = UnityMath.Euler(e.Length > 0 ? e[0] : 0f, e.Length > 1 ? e[1] : 0f, e.Length > 2 ? e[2] : 0f);
                    return "new Quaternion(" + Floats(q, 4) + ")";
                default:
                    throw new ArgumentException("No literal for " + kind);
            }
        }

        internal static string Literal(ParamType type, object value) =>
            type.Kind == ValueKind.Enum ? type.UnityType + "." + (string)value : Literal(type.Kind, value);

        static string Floats(object value, int n)
        {
            var v = value as float[] ?? new float[n];
            var parts = new string[n];
            for (int i = 0; i < n; i++) parts[i] = FloatLiteral(i < v.Length ? v[i] : 0f);
            return string.Join(", ", parts);
        }

        static string FloatLiteral(float f)
        {
            if (float.IsNaN(f) || float.IsInfinity(f)) f = 0f;
            return f.ToString("R", CultureInfo.InvariantCulture) + "f";
        }

        internal static string StringLiteral(string s)
        {
            var sb = new StringBuilder("\"");
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                // A lone half of a surrogate pair can't be written to the UTF-8 file as is (it would become U+FFFD, and the
                // file would never match what was generated): escaped. Whole pairs stay as they are.
                if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { sb.Append(c).Append(s[i + 1]); i++; continue; }
                if (char.IsSurrogate(c)) { sb.Append("\\u").Append(((int)c).ToString("x4")); continue; }
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        // C# line terminators include U+0085, U+2028, U+2029; escape them with the other control chars.
                        if (c < 0x20 || (c >= 0x7f && c <= 0x9f) || c == (char)0x2028 || c == (char)0x2029) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        static bool IsConstantKindCompatible(ValueKind want, object value)
        {
            switch (want)
            {
                case ValueKind.Bool: return value is bool;
                case ValueKind.Int: return value is int;
                case ValueKind.Float: return value is float || value is int || value is double;
                case ValueKind.String: return value is string;
                case ValueKind.Vector3: return value is float[] a && a.Length == 3;
                case ValueKind.Quaternion: return value is float[] q && q.Length == 3;
                case ValueKind.Vector2: return value is float[] v2 && v2.Length == 2;
                case ValueKind.Color: return value is float[] c && c.Length == 4;
                case ValueKind.Enum: return value is string member && IsEnumMemberName(member);
                case ValueKind.Url: return value is string;
                default: return false;
            }
        }
    }
}
