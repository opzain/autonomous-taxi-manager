using System.Collections.Generic;
using UnityEngine;

namespace TaxiSim.World
{
    public enum VehicleKind
    {
        Sedan,
        KaaliPeeli,     // Mumbai's black-and-yellow taxi
        AutoRickshaw,
        BestBus,        // red BEST bus
        FleetEv,        // our autonomous EV taxi
        RivalCab,       // rival fleet "RapidCab"
        LocalTrainCar,
        MetroCar,
        Airliner,
        AirTaxi,        // autonomous eVTOL air taxi (Neon Mumbai 2077)
        Scooter,        // two-wheeler with rider
        Truck           // colourful Tata goods truck
    }

    /// <summary>
    /// Low-poly procedural vehicles (sizes ~1.6x real for readability at city scale).
    /// Each mesh has three sub-meshes: paint (vertex colour), glass, lights (emissive).
    /// Local space: +z forward, pivot on the ground at the centre.
    /// </summary>
    public static class VehicleFactory
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        public static readonly Color[] CarPaints =
        {
            new Color(0.92f, 0.92f, 0.9f), new Color(0.65f, 0.66f, 0.68f), new Color(0.12f, 0.12f, 0.13f),
            new Color(0.6f, 0.08f, 0.08f), new Color(0.1f, 0.22f, 0.5f), new Color(0.35f, 0.36f, 0.38f),
            new Color(0.75f, 0.7f, 0.6f), new Color(0.2f, 0.35f, 0.3f)
        };

        public static Material[] Materials(MaterialLibrary m) => new[] { m.VehiclePaint, m.Glass, m.VehicleLights };

        public static Mesh Get(VehicleKind kind, Color paint)
        {
            string key = kind + "_" + ColorUtility.ToHtmlStringRGB(paint);
            if (Cache.TryGetValue(key, out Mesh mesh) && mesh != null) return mesh;
            var p = new MeshBuilder();
            var g = new MeshBuilder();
            var l = new MeshBuilder();
            Fill(kind, paint, p, g, l);
            mesh = MeshBuilder.ToMultiMaterialMesh(key, p, g, l);
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// Builds a vehicle into separate body / glass / lights builders (for merging many static, parked
        /// vehicles into a few meshes). Parked scooters have no rider.
        /// </summary>
        public static void Fill(VehicleKind kind, Color paint, MeshBuilder p, MeshBuilder g, MeshBuilder l, bool parked = false)
        {
            switch (kind)
            {
                case VehicleKind.Sedan: Sedan(p, g, l, paint); break;
                case VehicleKind.KaaliPeeli: KaaliPeeli(p, g, l); break;
                case VehicleKind.AutoRickshaw: Auto(p, g, l); break;
                case VehicleKind.BestBus: Bus(p, g, l); break;
                case VehicleKind.FleetEv: Pod(p, g, l, new Color(0.93f, 0.95f, 0.97f), new Color(0.2f, 1.0f, 1.2f)); break;
                case VehicleKind.RivalCab: Pod(p, g, l, new Color(1f, 0.45f, 0.08f), new Color(1.2f, 0.6f, 0.1f)); break;
                case VehicleKind.LocalTrainCar: TrainCar(p, g, l, 2.0f, new Color(0.82f, 0.82f, 0.84f), new Color(0.5f, 0.1f, 0.16f)); break;
                case VehicleKind.MetroCar: TrainCar(p, g, l, 1.9f, new Color(0.95f, 0.96f, 0.97f), new Color(0.05f, 0.55f, 0.6f)); break;
                case VehicleKind.Airliner: Airliner(p, g, l); break;
                case VehicleKind.AirTaxi: AirTaxi(p, g, l, paint); break;
                case VehicleKind.Scooter: Scooter(p, g, l, paint, !parked); break;
                case VehicleKind.Truck: Truck(p, g, l, paint); break;
            }
        }

        public static GameObject Spawn(MaterialLibrary mats, VehicleKind kind, Color paint, Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Get(kind, paint);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = Materials(mats);
            return go;
        }

        private static readonly Color Tyre = new Color(0.05f, 0.05f, 0.05f);
        private static readonly Color Head = new Color(1f, 0.97f, 0.9f);
        private static readonly Color Tail = new Color(1f, 0.08f, 0.05f);

        private static void Wheels(MeshBuilder p, float w, float l, float r)
        {
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    p.AddBox(new Vector3(sx * w * 0.46f, r, sz * l * 0.32f), new Vector3(0.05f, r * 2f, r * 2f), Tyre);
        }

        private static void Lamps(MeshBuilder l, float w, float l0, float y)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                l.AddBox(new Vector3(s * w * 0.33f, y, l0 * 0.5f + 0.004f), new Vector3(w * 0.2f, 0.03f, 0.01f), Head);
                l.AddBox(new Vector3(s * w * 0.35f, y, -l0 * 0.5f - 0.004f), new Vector3(w * 0.18f, 0.03f, 0.01f), Tail);
            }
        }

        private static void Sedan(MeshBuilder p, MeshBuilder g, MeshBuilder l, Color paint)
        {
            const float L = 0.72f, W = 0.3f;
            p.AddBox(new Vector3(0, 0.1f, 0), new Vector3(W, 0.11f, L), paint);
            g.AddBox(new Vector3(0, 0.19f, -0.03f), new Vector3(W * 0.84f, 0.08f, L * 0.47f), Color.white);
            p.AddBox(new Vector3(0, 0.235f, -0.03f), new Vector3(W * 0.8f, 0.015f, L * 0.44f), paint);
            Wheels(p, W, L, 0.05f);
            Lamps(l, W, L, 0.11f);
        }

        private static void KaaliPeeli(MeshBuilder p, MeshBuilder g, MeshBuilder l)
        {
            const float L = 0.62f, W = 0.27f;
            var black = new Color(0.06f, 0.06f, 0.07f);
            var yellow = new Color(0.98f, 0.78f, 0.05f);
            p.AddBox(new Vector3(0, 0.1f, 0), new Vector3(W, 0.12f, L), black);
            g.AddBox(new Vector3(0, 0.2f, -0.02f), new Vector3(W * 0.88f, 0.08f, L * 0.5f), Color.white);
            p.AddBox(new Vector3(0, 0.245f, -0.02f), new Vector3(W * 0.9f, 0.02f, L * 0.52f), yellow);
            p.AddBox(new Vector3(0, 0.12f, L * 0.36f), new Vector3(W * 1.01f, 0.04f, L * 0.26f), yellow);
            l.AddBox(new Vector3(0, 0.27f, -0.02f), new Vector3(0.08f, 0.035f, 0.04f), new Color(1f, 0.85f, 0.3f));
            Wheels(p, W, L, 0.045f);
            Lamps(l, W, L, 0.11f);
        }

        private static void Auto(MeshBuilder p, MeshBuilder g, MeshBuilder l)
        {
            const float L = 0.42f, W = 0.2f;
            var green = new Color(0.08f, 0.42f, 0.18f);
            var yellow = new Color(0.98f, 0.8f, 0.08f);
            p.AddBox(new Vector3(0, 0.1f, -0.02f), new Vector3(W, 0.1f, L * 0.75f), green);
            p.AddBox(new Vector3(0, 0.1f, L * 0.36f), new Vector3(W * 0.55f, 0.12f, L * 0.18f), green);
            g.AddBox(new Vector3(0, 0.2f, -0.03f), new Vector3(W * 0.95f, 0.1f, L * 0.55f), Color.white);
            p.AddBox(new Vector3(0, 0.27f, -0.03f), new Vector3(W * 1.04f, 0.05f, L * 0.68f), yellow);
            p.AddBox(new Vector3(0, 0.04f, L * 0.36f), new Vector3(0.04f, 0.08f, 0.08f), Tyre);
            p.AddBox(new Vector3(-W * 0.45f, 0.04f, -L * 0.25f), new Vector3(0.04f, 0.08f, 0.08f), Tyre);
            p.AddBox(new Vector3(W * 0.45f, 0.04f, -L * 0.25f), new Vector3(0.04f, 0.08f, 0.08f), Tyre);
            l.AddBox(new Vector3(0, 0.15f, L * 0.46f), new Vector3(0.05f, 0.04f, 0.01f), Head);
            l.AddBox(new Vector3(0, 0.12f, -L * 0.4f), new Vector3(W * 0.6f, 0.025f, 0.01f), Tail);
        }

        private static void Bus(MeshBuilder p, MeshBuilder g, MeshBuilder l)
        {
            const float L = 1.9f, W = 0.42f;
            var red = new Color(0.72f, 0.07f, 0.05f);
            p.AddBox(new Vector3(0, 0.16f, 0), new Vector3(W, 0.22f, L), red);
            g.AddBox(new Vector3(0, 0.33f, 0), new Vector3(W * 1.005f, 0.13f, L * 0.97f), Color.white);
            p.AddBox(new Vector3(0, 0.42f, 0), new Vector3(W, 0.05f, L), new Color(0.82f, 0.1f, 0.07f));
            l.AddBox(new Vector3(0, 0.37f, L * 0.5f + 0.006f), new Vector3(W * 0.7f, 0.05f, 0.01f), new Color(1f, 0.55f, 0.05f));
            for (int s = -1; s <= 1; s += 2)
                l.AddBox(new Vector3(s * (W * 0.5f + 0.006f), 0.335f, 0), new Vector3(0.005f, 0.07f, L * 0.9f), new Color(0.55f, 0.48f, 0.35f));
            Wheels(p, W, L, 0.07f);
            Lamps(l, W, L, 0.1f);
        }

        private static void Pod(MeshBuilder p, MeshBuilder g, MeshBuilder l, Color body, Color strip)
        {
            const float L = 0.62f, W = 0.3f;
            p.AddBox(new Vector3(0, 0.1f, 0), new Vector3(W, 0.12f, L), body);
            g.AddBox(new Vector3(0, 0.2f, -0.01f), new Vector3(W * 0.88f, 0.09f, L * 0.72f), Color.white);
            p.AddBox(new Vector3(0, 0.26f, -0.01f), new Vector3(W * 0.6f, 0.025f, L * 0.4f), body);
            p.AddCylinder(new Vector3(0, 0.27f, 0.02f), 0.035f, 0.035f, 0.035f, 10, new Color(0.05f, 0.05f, 0.06f)); // lidar
            l.AddBox(new Vector3(0, 0.155f, L * 0.5f + 0.004f), new Vector3(W * 0.9f, 0.018f, 0.01f), strip);
            l.AddBox(new Vector3(0, 0.155f, -L * 0.5f - 0.004f), new Vector3(W * 0.9f, 0.018f, 0.01f), Tail);
            for (int s = -1; s <= 1; s += 2)
                l.AddBox(new Vector3(s * (W * 0.5f + 0.004f), 0.155f, 0), new Vector3(0.01f, 0.014f, L * 0.9f), strip);
            Wheels(p, W, L, 0.05f);
        }

        private static void TrainCar(MeshBuilder p, MeshBuilder g, MeshBuilder l, float L, Color body, Color stripe)
        {
            const float W = 0.37f;
            p.AddBox(new Vector3(0, 0.26f, 0), new Vector3(W, 0.32f, L), body);
            p.AddBox(new Vector3(0, 0.17f, 0), new Vector3(W * 1.01f, 0.05f, L * 0.995f), stripe);
            g.AddBox(new Vector3(0, 0.33f, 0), new Vector3(W * 1.012f, 0.09f, L * 0.9f), Color.white);
            p.AddBox(new Vector3(0, 0.44f, 0), new Vector3(W * 0.9f, 0.04f, L * 0.98f), new Color(0.55f, 0.56f, 0.58f));
            for (int s = -1; s <= 1; s += 2)
                l.AddBox(new Vector3(s * (W * 0.5f + 0.008f), 0.33f, 0), new Vector3(0.004f, 0.05f, L * 0.85f), new Color(0.6f, 0.58f, 0.5f));
            l.AddBox(new Vector3(0, 0.32f, L * 0.5f + 0.004f), new Vector3(W * 0.5f, 0.03f, 0.01f), Head);
        }

        /// <summary>Approximate vehicle length in world units (used for car-following gaps).</summary>
        public static float Length(VehicleKind k)
        {
            switch (k)
            {
                case VehicleKind.BestBus: return 1.9f;
                case VehicleKind.Truck: return 1.2f;
                case VehicleKind.Sedan: return 0.72f;
                case VehicleKind.AutoRickshaw: return 0.42f;
                case VehicleKind.Scooter: return 0.3f;
                default: return 0.62f;
            }
        }

        private static void Scooter(MeshBuilder p, MeshBuilder g, MeshBuilder l, Color paint, bool rider = true)
        {
            var skin = new Color(0.55f, 0.38f, 0.28f);
            var shirt = Color.Lerp(paint, Color.white, 0.3f);
            p.AddBox(new Vector3(0, 0.07f, 0), new Vector3(0.06f, 0.07f, 0.28f), paint);
            p.AddBox(new Vector3(0, 0.11f, 0.11f), new Vector3(0.08f, 0.1f, 0.04f), paint);
            p.AddBox(new Vector3(0, 0.035f, 0.12f), new Vector3(0.03f, 0.07f, 0.07f), Tyre);
            p.AddBox(new Vector3(0, 0.035f, -0.11f), new Vector3(0.03f, 0.07f, 0.07f), Tyre);
            if (!rider)
            {
                p.AddBox(new Vector3(0, 0.15f, -0.03f), new Vector3(0.07f, 0.02f, 0.12f), new Color(0.1f, 0.1f, 0.1f)); // seat
                p.AddBox(new Vector3(0, 0.19f, 0.12f), new Vector3(0.12f, 0.012f, 0.012f), new Color(0.2f, 0.2f, 0.2f)); // handlebar
                return;
            }
            p.AddBox(new Vector3(0, 0.19f, -0.02f), new Vector3(0.09f, 0.12f, 0.06f), shirt);      // rider torso
            p.AddBox(new Vector3(0, 0.12f, 0.02f), new Vector3(0.08f, 0.05f, 0.12f), new Color(0.15f, 0.17f, 0.25f)); // legs
            p.AddEllipsoid(new Vector3(0, 0.28f, -0.01f), new Vector3(0.035f, 0.04f, 0.035f), 8, 5, rngHelmet(paint), 1f); // helmet
            p.AddBox(new Vector3(0, 0.2f, 0.07f), new Vector3(0.12f, 0.02f, 0.02f), skin);       // arms on handlebar
            l.AddBox(new Vector3(0, 0.12f, 0.14f), new Vector3(0.03f, 0.02f, 0.01f), Head);
            l.AddBox(new Vector3(0, 0.1f, -0.145f), new Vector3(0.03f, 0.015f, 0.01f), Tail);
        }

        private static Color rngHelmet(Color paint) => paint.grayscale > 0.5f ? new Color(0.1f, 0.1f, 0.12f) : new Color(0.9f, 0.9f, 0.9f);

        private static void Truck(MeshBuilder p, MeshBuilder g, MeshBuilder l, Color paint)
        {
            const float L = 1.2f, W = 0.36f;
            var cargo = new Color(0.95f, 0.75f, 0.2f);
            p.AddBox(new Vector3(0, 0.17f, L * 0.36f), new Vector3(W, 0.24f, L * 0.26f), paint);           // cab
            g.AddBox(new Vector3(0, 0.24f, L * 0.49f), new Vector3(W * 0.9f, 0.1f, 0.01f), Color.white);    // windscreen
            p.AddBox(new Vector3(0, 0.2f, -L * 0.13f), new Vector3(W * 1.02f, 0.3f, L * 0.7f), cargo);     // body
            p.AddBox(new Vector3(0, 0.37f, -L * 0.13f), new Vector3(W * 1.04f, 0.05f, L * 0.72f), new Color(0.15f, 0.45f, 0.25f)); // tarp
            p.AddBox(new Vector3(0, 0.07f, 0), new Vector3(W * 0.9f, 0.04f, L * 0.98f), new Color(0.15f, 0.15f, 0.16f));
            Wheels(p, W, L, 0.07f);
            Lamps(l, W, L, 0.1f);
        }

        private static void AirTaxi(MeshBuilder p, MeshBuilder g, MeshBuilder l, Color strip)
        {
            var body = new Color(0.9f, 0.92f, 0.95f);
            var dark = new Color(0.08f, 0.08f, 0.1f);
            p.AddEllipsoid(new Vector3(0, 0.25f, 0), new Vector3(0.35f, 0.22f, 0.7f), 12, 8, body, 1f);
            g.AddEllipsoid(new Vector3(0, 0.36f, 0.22f), new Vector3(0.26f, 0.14f, 0.32f), 10, 6, Color.white, 1f);
            // Four rotor arms with ducted rotors
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 hub = new Vector3(sx * 0.75f, 0.3f, sz * 0.55f);
                    p.AddBeam(new Vector3(0, 0.28f, sz * 0.3f), hub, new Vector2(0.06f, 0.04f), dark);
                    p.AddCylinder(hub + Vector3.down * 0.03f, 0.3f, 0.3f, 0.06f, 16, dark, capTop: false);
                    g.AddDisc(hub + Vector3.up * 0.01f, 0.28f, 16, Vector3.up, Color.white);
                    l.AddCylinder(hub + Vector3.down * 0.035f, 0.31f, 0.31f, 0.015f, 16, strip, capTop: false);
                }
            }
            l.AddBox(new Vector3(0, 0.08f, 0), new Vector3(0.4f, 0.02f, 0.9f), strip);           // underglow strip
            l.AddBox(new Vector3(-0.38f, 0.25f, 0), new Vector3(0.03f, 0.03f, 0.03f), new Color(3f, 0.1f, 0.1f));
            l.AddBox(new Vector3(0.38f, 0.25f, 0), new Vector3(0.03f, 0.03f, 0.03f), new Color(0.1f, 3f, 0.3f));
            l.AddBox(new Vector3(0, 0.27f, 0.69f), new Vector3(0.18f, 0.04f, 0.02f), Head);
        }

        private static void Airliner(MeshBuilder p, MeshBuilder g, MeshBuilder l)
        {
            var white = new Color(0.94f, 0.95f, 0.96f);
            var grey = new Color(0.6f, 0.62f, 0.66f);
            p.AddEllipsoid(new Vector3(0, 0.32f, 0), new Vector3(0.22f, 0.24f, 1.85f), 12, 8, white, 1f);
            p.AddBox(new Vector3(0, 0.25f, 0.1f), Quaternion.identity, new Vector3(3.4f, 0.04f, 0.55f), white);
            p.AddBox(new Vector3(0, 0.62f, -1.55f), Quaternion.Euler(-20f, 0, 0), new Vector3(0.04f, 0.6f, 0.45f), new Color(0.1f, 0.25f, 0.55f));
            p.AddBox(new Vector3(0, 0.36f, -1.65f), Quaternion.identity, new Vector3(1.2f, 0.03f, 0.3f), white);
            p.AddEllipsoid(new Vector3(-0.75f, 0.16f, 0.32f), new Vector3(0.1f, 0.1f, 0.3f), 8, 5, grey, 1f);
            p.AddEllipsoid(new Vector3(0.75f, 0.16f, 0.32f), new Vector3(0.1f, 0.1f, 0.3f), 8, 5, grey, 1f);
            g.AddBox(new Vector3(0, 0.4f, 1.55f), Quaternion.Euler(20f, 0, 0), new Vector3(0.24f, 0.06f, 0.18f), Color.white);
            l.AddBox(new Vector3(-1.7f, 0.25f, 0.1f), new Vector3(0.06f, 0.04f, 0.06f), new Color(3f, 0.1f, 0.1f));
            l.AddBox(new Vector3(1.7f, 0.25f, 0.1f), new Vector3(0.06f, 0.04f, 0.06f), new Color(0.1f, 3f, 0.3f));
            l.AddBox(new Vector3(0, 0.93f, -1.7f), new Vector3(0.05f, 0.05f, 0.05f), Color.white * 3f);
            l.AddBox(new Vector3(0, 0.14f, 0.9f), new Vector3(0.12f, 0.05f, 0.05f), Color.white * 4f);
        }
    }
}
