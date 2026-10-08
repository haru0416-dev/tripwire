using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Tripwire.Core;
using Xunit;

/// <summary>
/// Export for distribution: the Tripwire component taken out of real Unity YAML (Fixtures, written by Unity 2022.3: a
/// prefab with a trigger, a scene with an instance whose trigger is overridden and a trigger added to a child, and a
/// variant), and the .unitypackage it is packed into.
/// </summary>
public class DistributionTests
{
    const string Tripwire = "540375422147df5e3ad867e7064225a9";
    const string LampPrefab = "1b85e0a301fc72782bac70bf86dc0f00";
    const long LampTrigger = 8498761419526208808;

    static string Fixture(string name, [CallerFilePath] string here = "") => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here), "Fixtures", name));

    static Dictionary<string, HashSet<long>> Sources => new Dictionary<string, HashSet<long>> { [LampPrefab] = new HashSet<long> { LampTrigger } };

    [Fact]
    public void APrefabLosesItsTriggerAndKeepsTheRest()
    {
        var yaml = Fixture("LampSwitch.prefab.yaml");
        Assert.Equal(new[] { LampTrigger }, Distribution.ComponentIds(yaml, Tripwire));
        var stripped = Distribution.Strip(yaml, Tripwire, null, out int removed);
        Assert.Equal(1, removed);
        Assert.DoesNotContain(Tripwire, stripped);
        Assert.DoesNotContain(LampTrigger.ToString(), stripped); // its document and its entry in the object's list
        Assert.Contains("--- !u!114 &5157079654039242766", stripped); // the generated behaviour stays
        Assert.Equal(yaml.Split('\n').Count(l => l.StartsWith("--- ")) - 1, stripped.Split('\n').Count(l => l.StartsWith("--- ")));
        var lines = yaml.Split('\n').ToList();
        int start = lines.IndexOf("--- !u!114 &" + LampTrigger), end = lines.FindIndex(start + 1, l => l.StartsWith("--- "));
        Assert.Equal(lines.Count - (end - start) - 1, stripped.Split('\n').Length); // its document and its one list entry
    }

    [Fact]
    public void ASceneLosesWhatItsInstancesChangeOrAddOnTriggers()
    {
        var yaml = Fixture("Samples.unity.yaml");
        var stripped = Distribution.Strip(yaml, Tripwire, Sources, out int removed);
        Assert.Equal(1, removed); // the trigger added to the child
        Assert.DoesNotContain(Tripwire, stripped);
        Assert.DoesNotContain("propertyPath: events.", stripped); // the override of the prefab's trigger
        Assert.DoesNotContain("addedObject:", stripped);
        Assert.Contains("    m_AddedComponents: []", stripped);
        Assert.Contains("propertyPath: m_LocalPosition.x", stripped); // other overrides stay
        Assert.Contains("--- !u!1 &1392586418 stripped", stripped);
    }

    [Fact]
    public void AVariantLosesItsOverrideOfTheTrigger()
    {
        var yaml = Fixture("LampVariant.prefab.yaml");
        var stripped = Distribution.Strip(yaml, Tripwire, Sources, out int removed);
        Assert.Equal(0, removed);
        Assert.DoesNotContain("propertyPath: events.", stripped);
        Assert.Contains("propertyPath: m_Name", stripped);
    }

    [Fact]
    public void EmptyValuesAreNotMistakenForEmptiedLists()
    {
        // "m_Name: " is an empty string; "m_Component:" with entries left stays a list.
        var yaml = "%YAML 1.1\n--- !u!1 &1\nGameObject:\n  m_Name: \n  m_Component:\n  - component: {fileID: 2}\n  - component: {fileID: 3}\n"
                 + "--- !u!114 &3\nMonoBehaviour:\n  m_GameObject: {fileID: 1}\n  m_Script: {fileID: 11500000, guid: " + Tripwire + ", type: 3}\n";
        var stripped = Distribution.Strip(yaml, Tripwire, null, out _);
        Assert.Contains("  m_Name: \n", stripped);
        Assert.Contains("  m_Component:\n  - component: {fileID: 2}\n", stripped);
        Assert.DoesNotContain("fileID: 3", stripped);
    }

    [Fact]
    public void AnOverrideWithLinesInsideItGoesWhole()
    {
        // Unity writes a newline inside a quoted string as an unindented blank line, then the rest deeper again.
        var yaml = "%YAML 1.1\n--- !u!1001 &5\nPrefabInstance:\n  m_Modification:\n    m_Modifications:\n"
                 + "    - target: {fileID: " + LampTrigger + ", guid: " + LampPrefab + ", type: 3}\n      propertyPath: events.Array.data[0].comment\n      value: 'Opens the door\n\n\n        second line'\n      objectReference: {fileID: 0}\n"
                 + "    - target: {fileID: 1, guid: " + LampPrefab + ", type: 3}\n      propertyPath: m_LocalPosition.x\n      value: 2\n      objectReference: {fileID: 0}\n"
                 + "    m_RemovedComponents: []\n";
        var stripped = Distribution.Strip(yaml, Tripwire, Sources, out _);
        Assert.DoesNotContain("second line", stripped);
        Assert.Contains("    m_Modifications:\n    - target: {fileID: 1,", stripped);
        Assert.Contains("      propertyPath: m_LocalPosition.x", stripped);
    }

    [Fact]
    public void WindowsLineEndsStayAndAFileWithoutTriggersIsUnchanged()
    {
        var yaml = Fixture("LampSwitch.prefab.yaml").Replace("\n", "\r\n");
        var untouched = "%YAML 1.1\r\n--- !u!1 &1\r\nGameObject:\r\n  m_Name: A\r\n";
        Assert.Equal(untouched, Distribution.Strip(untouched, Tripwire, Sources, out _));
        var stripped = Distribution.Strip(yaml, Tripwire, null, out int removed);
        Assert.Equal(1, removed);
        Assert.DoesNotContain(Tripwire, stripped);
        Assert.Equal(stripped.Split('\n').Length - 1, stripped.Split("\r\n").Length - 1);
    }

    [Fact]
    public void BinaryFilesAreRecognised()
    {
        Assert.True(Distribution.IsText(Fixture("LampSwitch.prefab.yaml")));
        Assert.False(Distribution.IsText("\0\0\0\u0001binary"));
    }

    [Fact]
    public void ThePackageHoldsEachAssetUnderItsGuid()
    {
        var file = Path.GetTempFileName();
        File.WriteAllText(file, "copied from disk");
        try
        {
            var buffer = new MemoryStream();
            Distribution.WritePackage(buffer, new[]
            {
                new Distribution.Entry { Guid = "aaaa", PathName = "Assets/Shop/Lamp.prefab", Meta = Encoding.UTF8.GetBytes("meta-a"), Content = Encoding.UTF8.GetBytes("stripped") },
                new Distribution.Entry { Guid = "bbbb", PathName = "Assets/Shop/Big.png", Meta = Encoding.UTF8.GetBytes("meta-b"), File = file },
                new Distribution.Entry { Guid = "cccc", PathName = "Assets/Shop", Meta = Encoding.UTF8.GetBytes("folder") },
            });
            var files = ReadTar(new MemoryStream(buffer.ToArray()));
            Assert.Equal("Assets/Shop/Lamp.prefab", files["./aaaa/pathname"]);
            Assert.Equal("stripped", files["./aaaa/asset"]);
            Assert.Equal("meta-a", files["./aaaa/asset.meta"]);
            Assert.Equal("copied from disk", files["./bbbb/asset"]);
            Assert.Equal("folder", files["./cccc/asset.meta"]);
            Assert.False(files.ContainsKey("./cccc/asset")); // a folder has no content
        }
        finally { File.Delete(file); }
    }

    /// <summary>The files of a gzipped tar, read back with a plain reader of the format (names and sizes from the headers).</summary>
    static Dictionary<string, string> ReadTar(Stream gz)
    {
        var files = new Dictionary<string, string>();
        var tar = new MemoryStream();
        using (var z = new GZipStream(gz, CompressionMode.Decompress)) z.CopyTo(tar);
        var data = tar.ToArray();
        for (int at = 0; at + 512 <= data.Length;)
        {
            var name = Encoding.ASCII.GetString(data, at, 100).TrimEnd('\0');
            if (name.Length == 0) break;
            long size = System.Convert.ToInt64(Encoding.ASCII.GetString(data, at + 124, 11), 8);
            int sum = data.Skip(at).Take(512).Select((b, i) => i >= 148 && i < 156 ? 32 : b).Sum();
            Assert.Equal(System.Convert.ToInt32(Encoding.ASCII.GetString(data, at + 148, 6), 8), sum);
            if (data[at + 156] == '0') files[name] = Encoding.UTF8.GetString(data, at + 512, (int)size);
            at += 512 + (int)((size + 511) / 512 * 512);
        }
        return files;
    }
}
