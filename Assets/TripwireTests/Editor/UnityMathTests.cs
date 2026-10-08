using Tripwire.Core;
using NUnit.Framework;
using UnityEngine;

namespace Tripwire.Tests
{
    public class UnityMathTests
    {
        [Test]
        public void EulerMatchesUnityQuaternionEuler()
        {
            var rng = new System.Random(12345);
            float worst = 0f;
            for (int i = 0; i < 1000; i++)
            {
                float x = (float)(rng.NextDouble() * 720 - 360), y = (float)(rng.NextDouble() * 720 - 360), z = (float)(rng.NextDouble() * 720 - 360);
                var q = UnityMath.Euler(x, y, z);
                var ours = new Quaternion(q[0], q[1], q[2], q[3]);
                var unity = Quaternion.Euler(x, y, z);
                // Same rotation, and the same sign convention (the generated constant should match component-wise).
                worst = Mathf.Max(worst, Quaternion.Angle(ours, unity));
                Assert.Greater(Quaternion.Dot(ours, unity), 0.99999f, $"({x},{y},{z})");
            }
            Assert.Less(worst, 0.01f, "max angle difference (deg)");
        }
    }
}
