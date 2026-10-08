using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Tripwire.Core;
using Tripwire.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using static Tripwire.Tests.TestSupport;

namespace Tripwire.Tests
{
    /// <summary>
    /// Export for distribution: a prefab and a scene with triggers become a package without Tripwire in it, with the
    /// generated scripts and nothing from Packages/. (Importing it into a project without Tripwire was checked by hand:
    /// no missing script, the prefab saves, the switch works in ClientSim.)
    /// </summary>
    public class ExportTests
    {
        const string Dir = "Assets/TripwireTests/Temp/Export";
        const string Prefab = Dir + "/Lamp Switch.prefab";
        const string Shop = Dir + "/Shop.unity";
        internal const string Output = "Logs/export-test.unitypackage";

        static TripwireTrigger LampSwitch()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "Lamp Switch";
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "Lamp";
            lamp.transform.SetParent(root.transform, false);
            lamp.SetActive(false);
            var t = root.AddComponent<TripwireTrigger>();
            t.dataVersion = TripwireTrigger.CurrentDataVersion;
            t.variables.Add(new KVariable { name = "on", typeName = "System.Boolean", synced = true });
            t.events.Add(new KEvent { eventId = EventCatalog.InteractId, actions = { new KAction { actionId = ActionCatalog.ToggleVariableId, args = { new KArg { stringValue = "on" } } } } });
            t.events.Add(new KEvent { eventId = EventCatalog.VariableChangedId, name = "on", actions = { new KAction { actionId = "GameObject.SetActive", args = {
                new KArg { source = KArgSource.Objects, objects = { lamp } }, new KArg { source = KArgSource.Variable, name = "on" } } } } });
            return t;
        }

        [UnityTest]
        public IEnumerator APrefabAndASceneExportWithoutTripwire()
        {
            LogAssert.ignoreFailingMessages = true;
            if (!File.Exists(Shop))
            {
                Directory.CreateDirectory(Dir);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                TripwireCompiler.ApplyAll(new[] { LampSwitch() });
                EditorSceneManager.SaveScene(scene, Shop);
            }
            yield return new RecompileScripts(false, false); // the script may exist already (an earlier run)
            LogAssert.ignoreFailingMessages = true;
            for (int i = 0; i < 30 && TripwireCompiler.ResumePending; i++) yield return null;

            if (SceneManagerPath() != Shop) EditorSceneManager.OpenScene(Shop);
            ExportAndCheck(Root("Lamp Switch").GetComponent<TripwireTrigger>());
        }

        // Not in the coroutine: its locals that lambdas capture would live in an object lost at the script reload above.
        static void ExportAndCheck(TripwireTrigger t)
        {
            Assert.AreEqual(TripwireCompiler.State.UpToDate, TripwireCompiler.ApplyAll(new[] { t }));
            // The prefab, and in the scene an instance of it whose trigger is overridden.
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(t.gameObject, Prefab, InteractionMode.AutomatedAction);
            t.events[0].interactText = "Push";
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            EditorSceneManager.SaveScene(t.gameObject.scene);

            var plan = TripwireExport.MakePlan(new[] { Dir });
            CollectionAssert.IsEmpty(plan.Problems);
            CollectionAssert.AreEquivalent(new[] { Prefab, Shop }, plan.Stripped);
            Assert.IsTrue(plan.Packages.Any(p => p.Contains("com.vrchat.worlds")), string.Join(", ", plan.Packages));
            Assert.IsFalse(plan.Packages.Any(p => p.Contains("dev.haru0416.tripwire")), "Tripwire isn't needed");

            TripwireExport.Write(plan, Output);
            var files = Read(Output);
            var paths = files.Where(kv => kv.Key.EndsWith("/pathname")).Select(kv => Encoding.UTF8.GetString(kv.Value)).ToList();
            Assert.IsTrue(paths.All(p => p.StartsWith("Assets/")), "nothing from Packages/: " + string.Join(", ", paths.Where(p => !p.StartsWith("Assets/"))));
            Assert.Contains(Prefab, paths);
            Assert.Contains(Shop, paths);
            Assert.Contains(Dir, paths); // the folder, with its guid
            var generated = paths.Where(p => p.StartsWith(TripwireCompiler.OutputDir + "/Tripwire_")).ToList();
            Assert.IsTrue(generated.Any(p => p.EndsWith(".cs")) && generated.Any(p => p.EndsWith(".asset")), "the generated script and its program: " + string.Join(", ", paths));
            var guid = TripwireExport.ScriptGuid;
            foreach (var kv in files.Where(kv => kv.Key.EndsWith("/asset") && (Path.GetExtension(Encoding.UTF8.GetString(files[kv.Key.Replace("/asset", "/pathname")])) is ".prefab" or ".unity")))
            {
                var text = Encoding.UTF8.GetString(kv.Value);
                Assert.IsFalse(text.Contains(guid), "no Tripwire component left");
                Assert.IsFalse(text.Contains("propertyPath: events."), "no override of a trigger left");
            }
            Assert.IsTrue(File.ReadAllText(Prefab).Contains(guid), "the project's own prefab keeps its trigger");

            // The editable version keeps the cards, and says Tripwire is needed.
            var editable = TripwireExport.MakePlan(new[] { Dir }, keepTripwire: true);
            CollectionAssert.IsEmpty(editable.Stripped);
            Assert.IsTrue(editable.Packages.Any(p => p.Contains("dev.haru0416.tripwire")), string.Join(", ", editable.Packages));
            TripwireExport.Write(editable, Output);
            var kept = Read(Output);
            Assert.IsTrue(kept.Where(kv => kv.Key.EndsWith("/asset")).Any(kv => Encoding.UTF8.GetString(kv.Value).Contains(guid)), "the cards are in");
            Assert.IsNotNull(prefab);
        }

        [Test]
        public void ATriggerThatIsNotAppliedStopsTheExport()
        {
            Directory.CreateDirectory(Dir);
            var t = LampSwitch();
            t.variables.Add(new KVariable { name = "unapplied", typeName = "System.Int32" }); // a program no script exists for
            var path = Dir + "/Unapplied.prefab";
            PrefabUtility.SaveAsPrefabAsset(t.gameObject, path);
            Object.DestroyImmediate(t.gameObject);
            try
            {
                var plan = TripwireExport.MakePlan(new[] { path });
                Assert.IsFalse(plan.CanWrite);
                Assert.IsTrue(plan.Problems.Any(p => p.Contains("Unapplied")), string.Join("\n", plan.Problems)); // a prefab's root is named after its file
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }

        static string SceneManagerPath() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;

        /// <summary>The files of a .unitypackage (a gzipped tar), by name.</summary>
        static Dictionary<string, byte[]> Read(string package)
        {
            var tar = new MemoryStream();
            using (var z = new GZipStream(File.OpenRead(package), CompressionMode.Decompress)) z.CopyTo(tar);
            var data = tar.ToArray();
            var files = new Dictionary<string, byte[]>();
            for (int at = 0; at + 512 <= data.Length;)
            {
                var name = Encoding.ASCII.GetString(data, at, 100).TrimEnd('\0');
                if (name.Length == 0) break;
                long size = System.Convert.ToInt64(Encoding.ASCII.GetString(data, at + 124, 11), 8);
                if (data[at + 156] == '0') files[name] = data.Skip(at + 512).Take((int)size).ToArray();
                at += 512 + (int)((size + 511) / 512 * 512);
            }
            return files;
        }
    }
}
