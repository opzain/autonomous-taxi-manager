using System.Collections.Generic;
using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>A named place shown with a floating label in the 3D view.</summary>
    public sealed class LandmarkInfo
    {
        public string Name;
        public string Subtitle;
        public Vector3 Position;
        public Color Accent;
        public int NodeId = -1;
    }

    /// <summary>Shared state passed between the world builders while the city is generated.</summary>
    public sealed class WorldContext
    {
        public CityMap Map;
        public MaterialLibrary Mats;
        public SpaceRegistry Space;
        public RoadNetwork Roads;
        public TowerMeshBuilder Towers;
        public Transform Root;
        public float Density = 1f;

        // Detail geometry batched per material (emitted once at the end)
        public readonly MeshBuilder Concrete = new MeshBuilder();
        public readonly MeshBuilder Metal = new MeshBuilder();
        public readonly MeshBuilder Glass = new MeshBuilder();
        public readonly MeshBuilder Lights = new MeshBuilder();      // emissive, visible day and night
        public readonly MeshBuilder NightLights = new MeshBuilder(); // emissive at night only
        public readonly MeshBuilder Grass = new MeshBuilder();
        public readonly MeshBuilder Paving = new MeshBuilder();
        public readonly MeshBuilder TrackBed = new MeshBuilder();
        public readonly MeshBuilder Sand = new MeshBuilder();

        public readonly List<LandmarkInfo> Landmarks = new List<LandmarkInfo>();
        public readonly List<Transform> ChargingBays = new List<Transform>();

        // v0.7 street life
        public readonly MeshBuilder Posters = new MeshBuilder();     // printed ads (Billboard material, uv1.x = seed)
        public readonly MeshBuilder StallBulbs = new MeshBuilder();  // bulbs strung under stall canopies (night only)
        public readonly MeshBuilder IdlePeople = new MeshBuilder();  // standing pedestrians (vendors, customers, commuters)
        public readonly List<Vector2> FootpathReserved = new List<Vector2>(); // stalls / shelters: no avenue tree here

        public bool FootpathFree(Vector2 p, float radius)
        {
            float r2 = radius * radius;
            foreach (Vector2 q in FootpathReserved) if ((q - p).sqrMagnitude < r2) return false;
            return true;
        }

        public float GroundY(float x, float z) => Map.Geography.GroundY(x, z);

        public void EmitDetails()
        {
            var root = new GameObject("Details").transform;
            root.SetParent(Root, false);
            Concrete.Emit("Concrete", root, Mats.Concrete);
            Metal.Emit("Metal", root, Mats.Metal);
            Glass.Emit("Glass", root, Mats.Glass);
            Lights.Emit("Lights", root, Mats.VehicleLights, false);
            NightLights.Emit("NightLights", root, Mats.LampHeads, false);
            Grass.Emit("Grass", root, Mats.Grass, false);
            Paving.Emit("Paving", root, Mats.Paving, false);
            TrackBed.Emit("TrackBed", root, Mats.TrackBed, false);
            Sand.Emit("Sand", root, Mats.Sand, false);
            Posters.Emit("Posters", root, Mats.Billboard);
            StallBulbs.Emit("StallBulbs", root, Mats.StallBulbs, false);
            IdlePeople.Emit("StandingPeople", root, Mats.CharacterIdle);
        }

        /// <summary>Creates a standalone renderer (for animated / special-material objects).</summary>
        public GameObject Spawn(string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation, Transform parent = null, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : Root, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
