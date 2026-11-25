using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    public enum WeatherMode
    {
        Clear,
        Haze,
        Monsoon
    }

    /// <summary>
    /// Mumbai weather: clear, coastal haze, and monsoon (heavy rain particles around the camera,
    /// roads/buildings slowly getting wet, puddle ripples, dark clouds and lightning flashes).
    /// The rain event in the dispatch simulation will switch this to Monsoon.
    /// </summary>
    public sealed class WeatherSystem : MonoBehaviour
    {
        public WeatherMode Mode = WeatherMode.Clear;
        public Transform FollowCamera;
        public int MaxRainParticlesPerSecond = 9000;

        public float Wetness { get; private set; }
        /// <summary>Minimum street wetness (the neon style keeps night streets glossy, rain or not).</summary>
        public float BaselineWetness;
        public float Rain { get; private set; }
        public float Lightning { get; private set; }

        public float CloudCover => Mode == WeatherMode.Clear ? 0.38f : Mode == WeatherMode.Haze ? 0.55f : 0.93f;
        public float FogMultiplier => Mode == WeatherMode.Clear ? 1f : Mode == WeatherMode.Haze ? 1.9f : 2.3f;
        public float LightMultiplier => Mode == WeatherMode.Clear ? 1f : Mode == WeatherMode.Haze ? 0.82f : 0.42f;

        private ParticleSystem _rain;
        private float _nextFlash = 6f;
        private float _flashTime;
        private readonly Pcg32 _rng = new Pcg32(99UL);

        private static readonly int WetId = Shader.PropertyToID("_TS_Wetness");
        private static readonly int RainId = Shader.PropertyToID("_TS_Rain");

        public void Init(Material rainMaterial)
        {
            _rain = Effects.Create("MonsoonRain", transform, Vector3.zero, Quaternion.identity, rainMaterial);
            var main = _rain.main;
            main.loop = true;
            main.startLifetime = 1.1f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.045f);
            main.maxParticles = 14000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.85f, 0.95f, 0.45f));
            var shape = _rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(70f, 1f, 70f);
            var vel = _rain.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-2.5f);
            vel.y = new ParticleSystem.MinMaxCurve(-30f);
            vel.z = new ParticleSystem.MinMaxCurve(-1f);
            var emission = _rain.emission;
            emission.rateOverTime = 0f;
            var renderer = _rain.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.025f;
            renderer.lengthScale = 1f;
            _rain.Play();
        }

        public void Cycle()
        {
            Mode = (WeatherMode)(((int)Mode + 1) % 3);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float rainTarget = Mode == WeatherMode.Monsoon ? 1f : 0f;
            Rain = Mathf.MoveTowards(Rain, rainTarget, dt / 4f);
            Wetness = Mathf.MoveTowards(Wetness, rainTarget, dt / (Mode == WeatherMode.Monsoon ? 20f : 45f));
            Shader.SetGlobalFloat(WetId, Mathf.Max(Wetness, BaselineWetness));
            Shader.SetGlobalFloat(RainId, Rain);

            if (_rain != null)
            {
                if (FollowCamera != null)
                {
                    Vector3 c = FollowCamera.position;
                    _rain.transform.position = new Vector3(c.x, c.y + 18f, c.z) + FollowCamera.forward * 20f;
                }
                var emission = _rain.emission;
                emission.rateOverTime = Rain * MaxRainParticlesPerSecond;
            }

            // Lightning during the monsoon
            if (Mode == WeatherMode.Monsoon && Rain > 0.6f)
            {
                _nextFlash -= dt;
                if (_nextFlash <= 0f)
                {
                    _flashTime = 0.35f;
                    _nextFlash = _rng.Range(7f, 18f);
                }
            }
            if (_flashTime > 0f)
            {
                _flashTime -= dt;
                float t = _flashTime / 0.35f;
                Lightning = Mathf.Clamp01(Mathf.Sin(t * Mathf.PI * 3f)) * t;
            }
            else
            {
                Lightning = 0f;
            }
        }
    }
}
