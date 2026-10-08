using System;

namespace UdonBridge
{
    public static class ProjectPaths
    {
        /// <summary>The "Assets/..." or "Packages/..." form of a path; the roots count only at a folder boundary.</summary>
        public static string Relative(string path)
        {
            path = path.Replace('\\', '/');
            foreach (var root in new[] { "Assets/", "Packages/" })
            {
                if (path.StartsWith(root, StringComparison.Ordinal)) return path;
                int at = path.IndexOf("/" + root, StringComparison.Ordinal);
                if (at >= 0) return path.Substring(at + 1);
            }
            return path;
        }
    }
}
