using System;

namespace Tripwire.Core
{
    /// <summary>Unity math reproduced without UnityEngine, for computing constants at generation time.</summary>
    public static class UnityMath
    {
        /// <summary>
        /// Same as UnityEngine.Quaternion.Euler(x, y, z) in degrees: rotate z around Z, then x around X, then y around Y
        /// (q = qy * qx * qz). Returns {x, y, z, w}.
        /// </summary>
        public static float[] Euler(float x, float y, float z)
        {
            double hx = x * Math.PI / 360.0, hy = y * Math.PI / 360.0, hz = z * Math.PI / 360.0;
            double sx = Math.Sin(hx), cx = Math.Cos(hx);
            double sy = Math.Sin(hy), cy = Math.Cos(hy);
            double sz = Math.Sin(hz), cz = Math.Cos(hz);

            // qx * qz
            double ax = sx * cz, ay = -sx * sz, az = cx * sz, aw = cx * cz;
            // qy * (qx * qz)
            double rx = cy * ax + sy * az;
            double ry = cy * ay + sy * aw;
            double rz = cy * az - sy * ax;
            double rw = cy * aw - sy * ay;
            return new[] { (float)rx, (float)ry, (float)rz, (float)rw };
        }
    }
}
