using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>One photographed PBR material (albedo + optional normal / roughness).</summary>
    public sealed class PbrSet
    {
        public string Name;
        public Texture2D Albedo;
        public Texture2D Normal;
        public Texture2D Roughness;
        public bool Valid => Albedo != null;
    }

    /// <summary>
    /// Finds optional real textures that the team downloads (free CC0 from Poly Haven / ambientCG)
    /// and drops into  Assets/_Project/Resources/TaxiSimTextures/  (HDRI skies into  .../Sky/).
    /// Files are matched by keywords in their names, so the original download names work as-is,
    /// e.g. "Asphalt026C_2K-JPG_Color.jpg" or "asphalt_02_diff_2k.jpg". Anything missing simply
    /// falls back to the procedural look.
    /// </summary>
    public static class TextureLibrary
    {
        public const string Folder = "TaxiSimTextures";

        private static readonly string[] AlbedoTokens = { "basecolor", "albedo", "diff", "color", "col" };
        private static readonly string[] NormalTokens = { "normalgl", "nor_gl", "normal", "nor", "nrm" };
        private static readonly string[] RoughTokens = { "roughness", "rough" };

        private static Texture2D[] _all;

        private static Texture2D[] All
        {
            get
            {
                if (_all == null) _all = Resources.LoadAll<Texture2D>(Folder).OrderBy(t => t.name).ToArray();
                return _all;
            }
        }

        public static int Count => All.Length;

        /// <summary>First texture set whose name contains one of the keywords (in priority order).</summary>
        public static PbrSet Find(params string[] keywords)
        {
            foreach (string kw in keywords)
            {
                string k = kw.ToLowerInvariant();
                List<Texture2D> group = All.Where(t => t.name.ToLowerInvariant().Contains(k)).ToList();
                if (group.Count == 0) continue;
                var set = new PbrSet { Name = kw };
                set.Normal = Pick(group, NormalTokens, avoid: "dx");
                set.Roughness = Pick(group, RoughTokens, avoid: null);
                set.Albedo = group.FirstOrDefault(t => HasToken(t.name, AlbedoTokens) && t != set.Normal && t != set.Roughness)
                             ?? group.FirstOrDefault(t => t != set.Normal && t != set.Roughness && !HasToken(t.name, new[] { "ao", "disp", "height", "metal", "opacity" }));
                if (set.Valid) return set;
            }
            return new PbrSet();
        }

        /// <summary>HDRI sky (imported as a cubemap by TaxiSimTextureImporter) from the Sky sub-folder.</summary>
        public static Cubemap FindSky()
        {
            Cubemap[] skies = Resources.LoadAll<Cubemap>(Folder + "/Sky");
            return skies.Length > 0 ? skies.OrderBy(c => c.name).First() : null;
        }

        private static Texture2D Pick(List<Texture2D> group, string[] tokens, string avoid)
        {
            foreach (string token in tokens)
            {
                Texture2D t = group.FirstOrDefault(x =>
                {
                    string n = x.name.ToLowerInvariant();
                    return n.Contains(token) && (avoid == null || !n.Contains(avoid));
                });
                if (t != null) return t;
            }
            return null;
        }

        private static bool HasToken(string name, string[] tokens)
        {
            string n = name.ToLowerInvariant();
            return tokens.Any(t => n.Contains(t));
        }
    }
}
