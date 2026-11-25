using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>Particle effects built from code (no assets needed): fountain jets, festival sparkles, rain.</summary>
    public static class Effects
    {
        public static ParticleSystem Create(string name, Transform parent, Vector3 position, Quaternion rotation, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        /// <summary>Fountain of Joy: a tall central jet plus a ring of smaller jets.</summary>
        public static void Fountain(WorldContext ctx, Vector3 center, float ringRadius)
        {
            var root = new GameObject("FountainOfJoy_Jets").transform;
            root.SetParent(ctx.Root, false);
            Jet(ctx, root, center, 5.5f, 0.16f, 500);
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                Jet(ctx, root, center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * ringRadius * 0.8f, 3.2f, 0.1f, 120);
            }
        }

        private static void Jet(WorldContext ctx, Transform parent, Vector3 pos, float speed, float size, float rate)
        {
            ParticleSystem ps = Create("Jet", parent, pos, Quaternion.Euler(-90f, 0f, 0f), ctx.Mats.Spray);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.9f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.gravityModifier = 0.45f;
            main.maxParticles = 1200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.95f, 1f, 0.55f));
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 3f;
            shape.radius = 0.05f;
            ps.Play();
        }

        /// <summary>Warm floating sparkles over the MMRDA festival stage.</summary>
        public static void Sparkles(WorldContext ctx, Vector3 center, float size)
        {
            ParticleSystem ps = Create("Festival_Sparkles", ctx.Root, center, Quaternion.identity, ctx.Mats.Sparkle);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.maxParticles = 400;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.3f, 1f), new Color(0.6f, 0.4f, 1f, 1f));
            var emission = ps.emission;
            emission.rateOverTime = 80f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(size * 1.6f, 1f, size);
            ps.Play();
        }
    }
}
