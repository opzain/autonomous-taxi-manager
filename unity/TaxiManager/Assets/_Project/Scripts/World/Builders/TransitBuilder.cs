using System.Collections.Generic;
using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// Mumbai transit layer: Western & Central Railway lines with animated local trains, the elevated
    /// Metro Line 2B viaduct with a metro train, the Bandra–Worli Sea Link (cable-stayed, lit at night)
    /// and the airport runway with aircraft landing over Kurla.
    /// </summary>
    public static class TransitBuilder
    {
        public const float MetroDeck = 3.4f;

        public static void Build(WorldContext ctx)
        {
            var root = new GameObject("Transit").transform;
            root.SetParent(ctx.Root, false);
            CityGeography geo = ctx.Map.Geography;

            Railway(ctx, root, "WesternRailway", geo.WesternRailway, new Color(0.82f, 0.82f, 0.84f), 3);
            Railway(ctx, root, "CentralRailway", geo.CentralRailway, new Color(0.9f, 0.9f, 0.9f), 2);
            Metro(ctx, root);
            SeaLink(ctx, root, geo.SeaLink);
            Airport(ctx, root);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Resamples a 2D polyline every 'step' units and lifts it onto ground/bridges.</summary>
        private static Vector3[] Profile(CityGeography geo, IList<Vec2> line, float step, float baseY, float bridgeY)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector2 a = line[i].ToVector2(), b = line[i + 1].ToVector2();
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / step));
                for (int k = 0; k < n; k++) pts.Add(Vector2.Lerp(a, b, (float)k / n));
            }
            pts.Add(line[line.Count - 1].ToVector2());

            var target = new float[pts.Count];
            for (int i = 0; i < pts.Count; i++)
                target[i] = geo.GroundY(pts[i].x, pts[i].y) < -0.12f ? bridgeY : Mathf.Max(geo.GroundY(pts[i].x, pts[i].y), 0f) + baseY;

            const int r = 6;
            var result = new Vector3[pts.Count];
            for (int i = 0; i < pts.Count; i++)
            {
                float sum = 0f;
                int cnt = 0;
                for (int j = Mathf.Max(0, i - r); j <= Mathf.Min(pts.Count - 1, i + r); j++)
                {
                    float m = 0f;
                    for (int q = Mathf.Max(0, j - r); q <= Mathf.Min(pts.Count - 1, j + r); q++) m = Mathf.Max(m, target[q]);
                    sum += m;
                    cnt++;
                }
                result[i] = new Vector3(pts[i].x, Mathf.Max(sum / cnt, target[i]), pts[i].y);
            }
            return result;
        }

        private static Vector3[] Offset(Vector3[] pts, float offset)
        {
            var o = new Vector3[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 d = (i < pts.Length - 1 ? pts[i + 1] : pts[i]) - (i > 0 ? pts[i - 1] : pts[i]);
                d.y = 0;
                d.Normalize();
                o[i] = pts[i] + new Vector3(d.z, 0, -d.x) * offset;
            }
            return o;
        }

        /// <summary>2D copy of a polyline, keeping every 8th point (enough for the space registry, much faster).</summary>
        private static List<Vector2> Flat(Vector3[] pts)
        {
            var l = new List<Vector2>(pts.Length / 8 + 2);
            for (int i = 0; i < pts.Length; i += 8) l.Add(new Vector2(pts[i].x, pts[i].z));
            Vector3 last = pts[pts.Length - 1];
            l.Add(new Vector2(last.x, last.z));
            return l;
        }

        private static void ElevatedStructure(WorldContext ctx, Vector3[] p, float width, int pillarEvery, float minHeight)
        {
            CityGeography geo = ctx.Map.Geography;
            var concrete = new Color(0.68f, 0.67f, 0.64f);
            for (int k = 0; k < p.Length - 1; k++)
            {
                if (Mathf.Min(p[k].y, p[k + 1].y) < minHeight) continue;
                Vector3 seg = p[k + 1] - p[k];
                float len = seg.magnitude;
                if (len < 1e-3f) continue;
                Quaternion rot = Quaternion.LookRotation(seg / len, Vector3.up);
                Vector3 mid = (p[k] + p[k + 1]) * 0.5f;
                ctx.Concrete.AddBox(mid + Vector3.down * 0.16f, rot, new Vector3(width, 0.3f, len + 0.05f), concrete);
                Vector3 right = rot * Vector3.right;
                ctx.Concrete.AddBox(mid + right * (width * 0.5f - 0.04f) + Vector3.up * 0.08f, rot, new Vector3(0.07f, 0.18f, len + 0.02f), concrete);
                ctx.Concrete.AddBox(mid - right * (width * 0.5f - 0.04f) + Vector3.up * 0.08f, rot, new Vector3(0.07f, 0.18f, len + 0.02f), concrete);
                if (k % pillarEvery == 0 && p[k].y > 0.6f)
                {
                    float g = geo.GroundY(p[k].x, p[k].z);
                    float bottom = g < (float)CityGeography.WaterLevel ? g - 0.3f : g - 0.1f;
                    float top = p[k].y - 0.3f;
                    ctx.Concrete.AddBox(new Vector3(p[k].x, (bottom + top) * 0.5f, p[k].z), rot, new Vector3(0.42f, top - bottom, 0.42f), concrete * 0.95f);
                    ctx.Concrete.AddBox(new Vector3(p[k].x, top - 0.1f, p[k].z), rot, new Vector3(width * 0.9f, 0.2f, 0.5f), concrete);
                }
            }
        }

        private static Transform[] SpawnCars(WorldContext ctx, Transform parent, VehicleKind kind, Color paint, int count, string name)
        {
            var cars = new Transform[count];
            for (int i = 0; i < count; i++) cars[i] = VehicleFactory.Spawn(ctx.Mats, kind, paint, parent, $"{name}_{i}").transform;
            return cars;
        }

        // ------------------------------------------------------------------ railways

        private static void Railway(WorldContext ctx, Transform root, string name, IList<Vec2> line, Color carColor, int trains)
        {
            CityGeography geo = ctx.Map.Geography;
            Vector3[] center = Profile(geo, line, 1f, 0.04f, 0.9f);
            ctx.TrackBed.AddRibbon(center, 3.6f, Color.white, 0.0f);
            ElevatedStructure(ctx, center, 3.8f, 5, 0.3f);

            var tracks = new List<Vector3[]>();
            foreach (float off in new[] { -0.85f, 0.85f })
            {
                Vector3[] track = Offset(center, off);
                tracks.Add(track);
                foreach (float rail in new[] { -0.17f, 0.17f }) ctx.Metal.AddRibbon(Offset(track, rail), 0.05f, new Color(0.7f, 0.7f, 0.72f), 0.06f);
            }

            // Overhead line masts (25 kV catenary)
            var mast = new Color(0.45f, 0.47f, 0.5f);
            for (int k = 3; k < center.Length - 3; k += 6)
            {
                Vector3 d = center[k + 1] - center[k];
                d.y = 0;
                d.Normalize();
                Vector3 right = new Vector3(d.z, 0, -d.x);
                Vector3 b = center[k] + right * 1.9f;
                ctx.Metal.AddBox(b + Vector3.up * 0.6f, Quaternion.LookRotation(d), new Vector3(0.08f, 1.2f, 0.08f), mast);
                ctx.Metal.AddBeam(b + Vector3.up * 1.15f, center[k] - right * 1.2f + Vector3.up * 1.15f, new Vector2(0.05f, 0.05f), mast);
            }

            ctx.Space.AddPolyline(Flat(center), 2.3f);

            // Mumbai local trains (9 cars) running in both directions
            for (int t = 0; t < trains; t++)
            {
                var trainRoot = new GameObject($"{name}_Local_{t}").transform;
                trainRoot.SetParent(root, false);
                Transform[] cars = SpawnCars(ctx, trainRoot, VehicleKind.LocalTrainCar, carColor, 9, "Car");
                var mover = trainRoot.gameObject.AddComponent<TrainMover>();
                mover.Path = new Polyline3(tracks[t % 2]);
                mover.Cars = cars;
                mover.Speed = 7f;
                mover.CarSpacing = 2.08f;
                mover.Dwell = 5f;
                mover.Distance = mover.Path.Length * (0.2f + 0.3f * t);
            }
        }

        // ------------------------------------------------------------------ metro

        private static void Metro(WorldContext ctx, Transform root)
        {
            CityGraph g = ctx.Map.Graph;
            var raw = new List<Vector3>();
            foreach (string key in ctx.Map.Geography.MetroNodeKeys) raw.Add(g.Node(key).Position.XZ.ToVector3(MetroDeck));
            // Offset the viaduct to run alongside (not on top of) the roads
            Vector3[] offset = Offset(raw.ToArray(), 3.2f);
            var pts = new List<Vector3>();
            for (int i = 0; i + 1 < offset.Length; i++)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(offset[i], offset[i + 1]) / 1f));
                for (int k = 0; k < n; k++) pts.Add(Vector3.Lerp(offset[i], offset[i + 1], (float)k / n));
            }
            pts.Add(offset[offset.Length - 1]);
            Vector3[] p = pts.ToArray();

            ElevatedStructure(ctx, p, 1.2f, 4, 1f);
            foreach (float rail in new[] { -0.17f, 0.17f }) ctx.Metal.AddRibbon(Offset(p, rail), 0.04f, new Color(0.7f, 0.7f, 0.72f), 0.02f);
            ctx.NightLights.AddRibbon(Offset(p, 0.62f), 0.03f, new Color(0.3f, 0.9f, 1f), 0.2f);
            ctx.Space.AddPolyline(Flat(p), 1.2f);

            // Stations next to NSE, Jio World and Kurla
            foreach (string key in new[] { "nse", "jio_world", "kurla_stn" })
            {
                Vector3 node = g.Node(key).Position.XZ.ToVector3(MetroDeck);
                int best = 0;
                for (int i = 1; i < p.Length; i++) if ((p[i] - node).sqrMagnitude < (p[best] - node).sqrMagnitude) best = i;
                Vector3 dir = (p[Mathf.Min(best + 1, p.Length - 1)] - p[Mathf.Max(best - 1, 0)]).normalized;
                Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
                // Platforms + glass side walls + roof (open in the middle so the train stays visible)
                foreach (float s in new[] { -1f, 1f })
                {
                    ctx.Concrete.AddBox(p[best] + rot * new Vector3(s * 0.85f, 0.02f, 0), rot, new Vector3(0.5f, 0.08f, 8f), new Color(0.75f, 0.75f, 0.76f));
                    ctx.Glass.AddBox(p[best] + rot * new Vector3(s * 1.08f, 0.42f, 0), rot, new Vector3(0.05f, 0.75f, 8f), Color.white);
                }
                ctx.Concrete.AddBox(p[best] + Vector3.up * 0.86f, rot, new Vector3(2.3f, 0.12f, 8.4f), new Color(0.85f, 0.86f, 0.88f));
                ctx.Lights.AddBox(p[best] + Vector3.up * 0.78f + rot * new Vector3(1.0f, 0, 0), rot, new Vector3(0.03f, 0.05f, 7.8f), new Color(0.3f, 0.9f, 1f));
            }

            var trainRoot = new GameObject("Metro2B_Train").transform;
            trainRoot.SetParent(root, false);
            Transform[] cars = SpawnCars(ctx, trainRoot, VehicleKind.MetroCar, Color.white, 6, "MetroCar");
            var mover = trainRoot.gameObject.AddComponent<TrainMover>();
            mover.Path = new Polyline3(p);
            mover.Cars = cars;
            mover.Speed = 6f;
            mover.CarSpacing = 1.95f;
            mover.Dwell = 4f;
            mover.Distance = mover.Path.Length * 0.4f;
        }

        // ------------------------------------------------------------------ sea link

        private static void SeaLink(WorldContext ctx, Transform root, IList<Vec2> line)
        {
            var pts = new List<Vector3>();
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector3 a = line[i].ToVector3(1.0f), b = line[i + 1].ToVector3(1.0f);
                int n = Mathf.CeilToInt(Vector3.Distance(a, b) / 2f);
                for (int k = 0; k < n; k++) pts.Add(Vector3.Lerp(a, b, (float)k / n));
            }
            pts.Add(line[line.Count - 1].ToVector3(1.0f));
            Vector3[] p = pts.ToArray();
            ctx.Concrete.AddRibbon(p, 2.8f, new Color(0.2f, 0.2f, 0.21f), 0.01f);
            ElevatedStructure(ctx, p, 2.9f, 3, 0.5f);

            var cables = new MeshBuilder();
            var concrete = new Color(0.78f, 0.77f, 0.74f);
            foreach (float pylonZ in new[] { 150f, 40f })
            {
                int idx = 0;
                for (int i = 1; i < p.Length; i++) if (Mathf.Abs(p[i].z - pylonZ) < Mathf.Abs(p[idx].z - pylonZ)) idx = i;
                Vector3 c = p[idx];
                Vector3 dir = (p[Mathf.Min(idx + 1, p.Length - 1)] - p[Mathf.Max(idx - 1, 0)]).normalized;
                Vector3 right = new Vector3(dir.z, 0, -dir.x);
                Vector3 apex = c + Vector3.up * 8.5f, top = c + Vector3.up * 12.6f;
                ctx.Concrete.AddBeam(c + right * 1.9f + Vector3.down * 2.5f, apex, new Vector2(0.45f, 0.45f), concrete);
                ctx.Concrete.AddBeam(c - right * 1.9f + Vector3.down * 2.5f, apex, new Vector2(0.45f, 0.45f), concrete);
                ctx.Concrete.AddBeam(apex, top, new Vector2(0.5f, 0.5f), concrete);
                ctx.NightLights.AddBox(top + Vector3.up * 0.05f, Quaternion.identity, new Vector3(0.25f, 0.1f, 0.25f), new Color(1f, 0.3f, 0.2f));
                for (int k = 0; k < 12; k++)
                {
                    float along = 4f + k * 2.1f;
                    float anchor = Mathf.Lerp(9.2f, 12.3f, k / 11f);
                    foreach (float s in new[] { -1f, 1f })
                        cables.AddBeam(c + Vector3.up * anchor, c + dir * along * s + Vector3.up * 0.1f, new Vector2(0.035f, 0.035f), Color.white);
                }
            }
            cables.Emit("SeaLink_Cables", root, ctx.Mats.NeonCables, true);
            for (int k = 0; k < p.Length; k += 4) ctx.NightLights.AddBox(p[k] + Vector3.up * 0.45f, Quaternion.identity, new Vector3(0.08f, 0.06f, 0.08f), new Color(1f, 0.8f, 0.5f));

            // Light traffic on the sea link (both directions)
            var traffic = new GameObject("SeaLink_Traffic").transform;
            traffic.SetParent(root, false);
            var rng = new Pcg32(777UL);
            for (int lane = -1; lane <= 1; lane += 2)
            {
                Vector3[] lanePts = Offset(p, lane * 0.7f);
                if (lane > 0) System.Array.Reverse(lanePts);
                var path = new Polyline3(lanePts);
                for (int i = 0; i < 6; i++)
                {
                    var carRoot = new GameObject($"SeaLinkCar_{lane}_{i}").transform;
                    carRoot.SetParent(traffic, false);
                    Transform car = VehicleFactory.Spawn(ctx.Mats, rng.Chance(0.3) ? VehicleKind.KaaliPeeli : VehicleKind.Sedan,
                        VehicleFactory.CarPaints[rng.Range(0, VehicleFactory.CarPaints.Length)], carRoot, "Car").transform;
                    var mover = carRoot.gameObject.AddComponent<TrainMover>();
                    mover.Path = path;
                    mover.Cars = new[] { car };
                    mover.PingPong = false;
                    mover.Speed = rng.Range(4f, 6f);
                    mover.Distance = rng.Range(0f, path.Length);
                }
            }
        }

        // ------------------------------------------------------------------ airport

        private static void Airport(WorldContext ctx, Transform root)
        {
            var threshold = new Vector3(340f, 0.06f, 560f);
            Vector3 dir = new Vector3(-0.8f, 0f, 0.6f).normalized;
            Vector3 right = new Vector3(dir.z, 0, -dir.x);
            const float length = 280f, width = 6f;
            Vector3 end = threshold + dir * length;

            // Keep the runway, terminal and the final-approach corridor free of buildings
            ctx.Space.AddSegment(threshold.XZ(), end.XZ(), width * 0.5f + 22f);
            ctx.Space.AddSegment((threshold - dir * 700f).XZ(), threshold.XZ(), 28f);

            var runway = new MeshBuilder();
            runway.AddRibbon(new[] { threshold - dir * 6f, end }, width, Color.white, 0f, new Vector4(1, width, 0, length));
            runway.Emit("Runway", root, ctx.Mats.Road, false);

            for (float d = 0f; d < length; d += 5f)
            {
                Vector3 c = threshold + dir * d + Vector3.up * 0.02f;
                ctx.Lights.AddBox(c, Quaternion.LookRotation(dir), new Vector3(0.12f, 0.01f, 1.8f), Color.white * 0.8f);
                ctx.NightLights.AddBox(c + right * (width * 0.5f + 0.1f), Quaternion.identity, Vector3.one * 0.08f, new Color(0.9f, 0.9f, 1f));
                ctx.NightLights.AddBox(c - right * (width * 0.5f + 0.1f), Quaternion.identity, Vector3.one * 0.08f, new Color(0.9f, 0.9f, 1f));
            }
            for (int i = 1; i <= 12; i++)
            {
                Vector3 c = threshold - dir * (i * 4f) + Vector3.up * 0.15f;
                for (int s = -2; s <= 2; s++)
                    ctx.NightLights.AddBox(c + right * s * 0.6f, Quaternion.identity, Vector3.one * 0.1f, i % 3 == 0 ? new Color(1f, 0.2f, 0.15f) : Color.white);
            }

            // Terminal and control tower beside the runway
            Vector2 term = (threshold + dir * 140f + right * 14f).XZ();
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            var style = new TowerStyle { Style = 0.1f, Frame = new Color(0.85f, 0.86f, 0.88f), Glass = new Color(0.1f, 0.2f, 0.28f), TotalHeight = 2.2f, Accent = true, Seed = 0.77f };
            ctx.Towers.AddBox(term, 0f, 40f, 9f, 2.2f, yaw, style);
            Vector2 tower = (threshold + dir * 90f + right * 12f).XZ();
            style.TotalHeight = 7f;
            ctx.Towers.AddCylinder(tower, 0f, 0.9f, 6f, 12, style);
            ctx.Glass.AddCylinder(new Vector3(tower.x, 6f, tower.y), 1.5f, 1.7f, 0.8f, 12, Color.white);
            ctx.Concrete.AddCylinder(new Vector3(tower.x, 6.8f, tower.y), 1.8f, 1.2f, 0.2f, 12, new Color(0.8f, 0.8f, 0.82f));

            var plane = VehicleFactory.Spawn(ctx.Mats, VehicleKind.Airliner, Color.white, root, "Airliner_Approach");
            var landing = new GameObject("LandingLight").AddComponent<Light>();
            landing.transform.SetParent(plane.transform, false);
            landing.transform.localPosition = new Vector3(0, 0.1f, 1.2f);
            landing.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            landing.type = LightType.Spot;
            landing.spotAngle = 30f;
            landing.range = 40f;
            landing.intensity = 6f;
            landing.color = new Color(1f, 0.97f, 0.9f);
            landing.shadows = LightShadows.None;
            var loop = plane.AddComponent<PlaneLoop>();
            loop.Threshold = threshold;
            loop.RunwayDirection = dir;
            ctx.Landmarks.Add(new LandmarkInfo { Name = "CSMIA Airport (Runway 27)", Subtitle = "Approach over Kurla", Position = term.X0Z(5f), Accent = new Color(0.7f, 0.8f, 1f) });
        }
    }
}
