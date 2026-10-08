using System;
using System.Linq;
using Tripwire.Core;
using Tripwire.Editor;
using NUnit.Framework;

namespace Tripwire.Tests
{
    /// <summary>What a trigger is offered on other U# scripts (read by reflection; the scenarios run them).</summary>
    public class UdonSharpApiTests
    {
        static Type CounterType => TripwireModel.ResolveType("TripwireTestCounter");
        static Type NotifierType => TripwireModel.ResolveType("TripwireTestNotifier");

        [Test]
        public void MembersOfAScriptAreOfferedWithoutInheritedOnes()
        {
            var members = UdonSharpApi.Members(CounterType);
            Assert.IsTrue(members.Any(c => c.Kind == CallKind.Method && c.Member == "Add" && c.Params.Single().Type.Kind == ValueKind.Int));
            Assert.IsTrue(members.Any(c => c.Kind == CallKind.Method && c.Member == "Doubled" && c.Returns.Kind == ValueKind.Int));
            Assert.IsTrue(members.Any(c => c.Kind == CallKind.Get && c.Member == "count"));
            Assert.IsTrue(members.Any(c => c.Kind == CallKind.Set && c.Member == "lamp"));
            Assert.IsFalse(members.Any(c => c.Member == "SendCustomEvent" || c.Member == "GetComponent" || c.Member == "enabled"));
        }

        [Test]
        public void RegistrationMethodsAreFound()
        {
            var regs = UdonSharpApi.Registrations(NotifierType);
            var protv = regs.Single(r => r.Method == "_RegisterListener");
            Assert.AreEqual(0, protv.SelfIndex);
            Assert.AreEqual(1, protv.PassCount, "the optional priority is left out");
            var txl = regs.Single(r => r.Method == "_RegisterNamed");
            Assert.AreEqual(1, txl.SelfIndex);
            Assert.AreEqual(3, txl.PassCount);
            Assert.IsFalse(regs.Any(r => r.Method == "_Notify"), "methods without a behaviour parameter are not registrations");
        }
    }
}
