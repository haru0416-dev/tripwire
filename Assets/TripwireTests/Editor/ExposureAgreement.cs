using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tripwire.Editor;
using UdonBridge;
using VRC.Udon.Graph;

namespace Tripwire.Tests
{
    /// <summary>
    /// Tripwire reads Udon's node definitions by parsing their names; udon-bridge names members the way UdonSharp does.
    /// For every node Tripwire offers, the member found by reflection must get the node's own name back from
    /// udon-bridge: the two ways of answering "does Udon have this" agree. Counts go to Logs/exposure-agreement.txt.
    /// </summary>
    public class ExposureAgreement
    {
        [Test]
        public void TripwireAndUdonSharpNameTheSameMembers()
        {
            Assert.IsTrue(UdonNames.Available, "udon-bridge found UdonSharp's naming code");
            const BindingFlags all = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
            int accepted = 0, same = 0;
            var problems = new List<string>();
            foreach (var d in UdonNodes.All)
            {
                if (UdonApi.Get(d.fullName) == null) continue; // not a call Tripwire offers
                accepted++;
                var rest = d.fullName.Substring(d.fullName.IndexOf(".__", StringComparison.Ordinal) + 3);
                int sep = rest.IndexOf("__", StringComparison.Ordinal);
                var name = sep < 0 ? rest : rest.Substring(0, sep);
                // The C# parameter types: inputs as they are; `out` / `ref` ones (Udon's IN_OUT) by reference, as Tripwire
                // resolved them (an array filled in place stays a plain array).
                var call = UdonApi.Get(d.fullName);
                var ps = d.parameters.Where((p, i) => (p.parameterType == UdonNodeParameter.ParameterType.IN || p.parameterType == UdonNodeParameter.ParameterType.IN_OUT)
                                                      && !(i == 0 && p.name == "instance" && name != "ctor")).ToList();
                var ins = ps.Select((p, j) =>
                {
                    var element = p.type.IsByRef ? p.type.GetElementType() : p.type;
                    return j < call.Params.Count && call.Params[j].Receives ? element.MakeByRefType() : element;
                }).ToArray();
                MethodBase member = name == "ctor" ? (MethodBase)d.type.GetConstructor(all, null, ins, null) : d.type.GetMethod(name, all, null, ins, null);
                string udon;
                if (member != null) udon = UdonNames.Of(member, d.type);
                else if ((name.StartsWith("get_") || name.StartsWith("set_")) && d.type.GetField(name.Substring(4), all) is FieldInfo field) udon = UdonNames.Of(field, name.StartsWith("set_"), d.type);
                else { problems.Add("not found: " + d.fullName); continue; }
                if (udon == d.fullName && UdonNodes.IsExposed(udon)) same++;
                else problems.Add(d.fullName + "  vs udon-bridge  " + udon);
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/exposure-agreement.txt", $"accepted by Tripwire {accepted}: same name {same}\n" + string.Join("\n", problems.Take(50)) + "\n");
            Assert.Greater(accepted, 10000, "Tripwire offers the Udon API");
            Assert.AreEqual(accepted, same, string.Join("\n", problems.Take(10)));
        }
    }
}
