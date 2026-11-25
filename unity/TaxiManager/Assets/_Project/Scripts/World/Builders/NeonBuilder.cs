using System.Collections.Generic;
using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// "Neon Mumbai 2077" dressing layer:
    ///  * rooftop holographic billboards and vertical blade signs (animated HoloAd shader)
    ///  * neon shop strips on Kurla / Dharavi low-rise
    ///  * volumetric light cones under every street lamp
    ///  * sweeping rooftop searchlights at key landmarks
    ///  * a giant rotating hologram of our autonomous taxi above BKC
    /// Everything except the lamp cones is parented under "Neon" so the style can be switched off (V).
    /// </summary>
    public static class NeonBuilder
    {
        public static Transform Build(WorldContext ctx)
        {
            var root = new GameObject("Neon").transform;
            root.SetParent(ctx.Root, false);
            var rng = new Pcg32(2077UL);

            var ads = new MeshBuilder();
            var tubes = new MeshBuilder();
            var frames = new MeshBuilder();

            foreach (BuildingRecord b in ctx.Towers.Records)
            {
                TowerStyle st = b.Style;
                bool commercial = st.Style < 0.75f;

                // 1) Rooftop billboard on taller commercial buildings
                if (!b.Cylinder && commercial && b.Height > 5.5f && b.Width > 2.5f && rng.Chance(0.09 * ctx.Density))
                {
                    float w = Mathf.Min(b.Width, b.Depth) * rng.Range(0.6f, 0.9f);
                    float h = w * rng.Range(0.45f, 0.65f);
                    Quaternion rot = Quaternion.Euler(0f, b.RotationY + (rng.Chance(0.5) ? 0f : 90f), 0f);
                    Vector3 c = new Vector3(b.Center.x, b.Top + 0.45f + h * 0.5f, b.Center.y);
                    AddSign(ads, c, rot, w, h, rng.NextFloat());
                    Vector3 right = rot * Vector3.right;
                    frames.AddBeam(new Vector3(c.x, b.Top, c.z) - right * w * 0.4f, c - right * w * 0.4f, new Vector2(0.08f, 0.08f), new Color(0.2f, 0.2f, 0.22f));
                    frames.AddBeam(new Vector3(c.x, b.Top, c.z) + right * w * 0.4f, c + right * w * 0.4f, new Vector2(0.08f, 0.08f), new Color(0.2f, 0.2f, 0.22f));
                }

                // 2) Vertical blade signs sticking out of the facade (mid-rise, mixed and residential)
                if (!b.Cylinder && b.Height > 2.5f && b.Height < 12f && rng.Chance(0.07 * ctx.Density))
                {
                    Quaternion rot = Quaternion.Euler(0f, b.RotationY, 0f);
                    float side = rng.Chance(0.5) ? 1f : -1f;
                    Vector3 corner = new Vector3(b.Center.x, b.BaseY, b.Center.y) + rot * new Vector3(side * b.Width * 0.5f, 0f, b.Depth * 0.5f);
                    float bh = Mathf.Min(2.2f, b.Height * 0.5f);
                    Vector3 c = corner + rot * new Vector3(0f, 0f, 0.3f) + Vector3.up * (Mathf.Min(b.Height - bh * 0.5f - 0.2f, 1.2f + bh * 0.5f));
                    Quaternion bladeRot = rot * Quaternion.Euler(0f, 90f, 0f);
                    AddSign(ads, c, bladeRot, 0.55f, bh, rng.NextFloat());
                    Color neon = NeonColor(rng.Range(1, 6));
                    tubes.AddBox(c + Vector3.up * (bh * 0.5f + 0.03f), bladeRot, new Vector3(0.6f, 0.03f, 0.03f), neon);
                    tubes.AddBox(c - Vector3.up * (bh * 0.5f + 0.03f), bladeRot, new Vector3(0.6f, 0.03f, 0.03f), neon);
                }

                // 3) Neon shop strips at street level (Kurla market, Dharavi, residential ground floors)
                if (!b.Cylinder && b.Height < 8f && st.Style > 0.8f && rng.Chance(0.18 * ctx.Density))
                {
                    Quaternion rot = Quaternion.Euler(0f, b.RotationY, 0f);
                    Vector3 front = new Vector3(b.Center.x, b.BaseY + Mathf.Min(0.32f, b.Height * 0.4f), b.Center.y) + rot * new Vector3(0f, 0f, b.Depth * 0.5f + 0.02f);
                    tubes.AddBox(front, rot, new Vector3(b.Width * rng.Range(0.4f, 0.85f), 0.05f, 0.03f), NeonColor(rng.Range(1, 6)));
                }
            }

            ads.Emit("HoloBillboards", root, ctx.Mats.HoloAd, false);
            tubes.Emit("NeonTubes", root, ctx.Mats.NeonTubes, false);
            frames.Emit("BillboardFrames", root, ctx.Mats.Metal);

            LampCones(ctx);
            Searchlights(ctx, root);
            GiantHologram(ctx, root);
            return root;
        }

        private static void AddSign(MeshBuilder mb, Vector3 center, Quaternion rot, float width, float height, float seed)
        {
            Vector3 r = rot * Vector3.right * width * 0.5f, u = Vector3.up * height * 0.5f;
            Vector3 n = rot * Vector3.forward;
            var seedUv = new Vector4(seed, 0, 0, 0);
            mb.AddQuad(center - r - u, center + r - u, center + r + u, center - r + u, n, Color.white,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), default, seedUv);
        }

        private static Color NeonColor(int idx)
        {
            switch ((idx - 1) % 5)
            {
                case 0: return new Color(1f, 0.12f, 0.75f);
                case 1: return new Color(0.1f, 0.9f, 1f);
                case 2: return new Color(1f, 0.55f, 0.1f);
                case 3: return new Color(0.62f, 0.25f, 1f);
                default: return new Color(0.55f, 1f, 0.25f);
            }
        }

        /// <summary>Soft volumetric cones under the street lamps (visible at night, stronger in rain).</summary>
        private static void LampCones(WorldContext ctx)
        {
            var cones = new ChunkedMeshBuilder(60000);
            int step = ctx.Density >= 1f ? 1 : ctx.Density >= 0.7f ? 2 : 4;
            for (int i = 0; i < ctx.Roads.LampHeads.Count; i += step)
            {
                Vector3 head = ctx.Roads.LampHeads[i];
                float groundY = head.y - 0.82f;
                cones.Next().AddCylinder(new Vector3(head.x, groundY, head.z), 0.75f, 0.05f, head.y - groundY, 10, ctx.Roads.LampColors[i], capTop: false);
            }
            var root = new GameObject("LampLightCones").transform;
            root.SetParent(ctx.Root, false);
            cones.Emit("LampCones", root, ctx.Mats.LampCones, false);
        }

        private static void Searchlights(WorldContext ctx, Transform root)
        {
            string[] keys = { "jio_world", "nse", "equinox", "phoenix", "one_bkc" };
            int i = 0;
            foreach (string key in keys)
            {
                LandmarkInfo lm = ctx.Landmarks.Find(l => l.NodeId == ctx.Map.Graph.Node(key).Id);
                if (lm == null) continue;
                var beam = new MeshBuilder();
                beam.AddCylinder(Vector3.zero, 0.25f, 3.5f, 90f, 16, Color.white, capTop: false);
                var go = ctx.Spawn($"Searchlight_{key}", beam.ToMesh("SearchlightBeam"), ctx.Mats.Searchlight,
                    new Vector3(lm.Position.x, lm.Position.y - 1.5f, lm.Position.z), Quaternion.identity, root, false);
                var sweep = go.AddComponent<SearchlightSweep>();
                sweep.Phase = i * 1.3f;
                sweep.Speed = 0.25f + 0.07f * i;
                i++;
            }
        }

        private static void GiantHologram(WorldContext ctx, Transform root)
        {
            LandmarkInfo jio = ctx.Landmarks.Find(l => l.NodeId == ctx.Map.Graph.Node("jio_world").Id);
            Vector3 basePos = jio != null ? new Vector3(jio.Position.x, 0f, jio.Position.z) : new Vector3(182f, 0f, 128f);
            basePos.y = ctx.GroundY(basePos.x, basePos.z);
            // Projector beam from the plaza up to the hologram
            var beam = new MeshBuilder();
            beam.AddCylinder(Vector3.zero, 0.4f, 6f, 24f, 24, Color.white, capTop: false);
            ctx.Spawn("Hologram_ProjectorBeam", beam.ToMesh("ProjectorBeam"), ctx.Mats.ProjectorBeam, basePos + Vector3.up * 1.7f, Quaternion.identity, root, false);

            // Rotating 25x hologram of our autonomous EV taxi + data rings
            var holo = new GameObject("Hologram_AutonomousTaxi");
            holo.transform.SetParent(root, false);
            holo.transform.position = basePos + Vector3.up * 24f;
            var model = new GameObject("Model");
            model.transform.SetParent(holo.transform, false);
            model.transform.localScale = Vector3.one * 25f;
            model.transform.localPosition = Vector3.down * 3f;
            model.AddComponent<MeshFilter>().sharedMesh = VehicleFactory.Get(VehicleKind.FleetEv, Color.white);
            var mr = model.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { ctx.Mats.HoloCyan, ctx.Mats.HoloCyan, ctx.Mats.HoloCyan };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int r = 0; r < 3; r++)
            {
                var ring = new MeshBuilder();
                ring.AddCylinder(Vector3.zero, 9f + r * 2.5f, 9f + r * 2.5f, 0.25f, 64, Color.white, capTop: false);
                var go = ctx.Spawn($"HoloRing_{r}", ring.ToMesh("HoloRing"), r == 1 ? ctx.Mats.HoloGold : ctx.Mats.HoloCyan,
                    holo.transform.position + Vector3.down * (4f - r * 1.5f), Quaternion.Euler(r * 8f, 0f, r * 5f), holo.transform, false);
                go.AddComponent<Spinner>().DegreesPerSecond = 12f * (r % 2 == 0 ? 1f : -1.4f);
            }
            holo.AddComponent<Spinner>().DegreesPerSecond = 9f;
            ctx.Landmarks.Add(new LandmarkInfo { Name = "AT Fleet Hologram", Subtitle = "Autonomous Taxi Manager · BKC", Position = holo.transform.position + Vector3.up * 8f, Accent = new Color(0.3f, 1f, 1f) });
        }
    }
}
