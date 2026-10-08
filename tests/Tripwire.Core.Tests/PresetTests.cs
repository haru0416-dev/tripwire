using System.Linq;
using Tripwire.Core;
using Xunit;

public class PresetTests
{
    [Fact]
    public void EveryPresetCallbackIsAUsableName()
    {
        foreach (var p in AssetPresets.All)
            foreach (var n in p.Notifications)
            {
                Assert.Null(CodeGenerator.CheckCallbackName(n.Callback, null));
                Assert.False(string.IsNullOrEmpty(n.Ja));
                Assert.All(n.RegisterArgs.Keys, k => Assert.InRange(k, 0, n.RegisterParamCount - 1));
            }
        Assert.Equal(AssetPresets.All.Count, AssetPresets.All.Select(p => p.Id).Distinct().Count());
    }

}
