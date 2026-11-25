using System.Collections.Generic;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// Real point lights under a subset of street lamps (the emissive lamp heads glow everywhere;
    /// these add actual light pools on the asphalt). The count depends on the graphics tier.
    /// URP Forward+ handles many lights efficiently.
    /// </summary>
    public sealed class StreetLights : MonoBehaviour
    {
        private readonly List<Light> _lights = new List<Light>();
        private float _level = -1f;

        public void Build(RoadNetwork roads, int maxLights)
        {
            foreach (Light l in _lights) if (l != null) Destroy(l.gameObject);
            _lights.Clear();
            int total = roads.LampHeads.Count;
            if (maxLights <= 0 || total == 0) return;
            float step = Mathf.Max(1f, (float)total / maxLights);
            for (float f = 0; f < total && _lights.Count < maxLights; f += step)
            {
                int i = Mathf.FloorToInt(f);
                var go = new GameObject($"StreetLight_{i}");
                go.transform.SetParent(transform, false);
                go.transform.position = roads.LampHeads[i];
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 3.2f;
                l.color = roads.LampColors[i];
                l.intensity = 0f;
                l.shadows = LightShadows.None;
                l.enabled = false;
                _lights.Add(l);
            }
            _level = -1f;
        }

        /// <summary>0 = off (day) .. 1 = full (night).</summary>
        public void SetLevel(float level)
        {
            if (Mathf.Abs(level - _level) < 0.01f) return;
            _level = level;
            foreach (Light l in _lights)
            {
                l.enabled = level > 0.02f;
                l.intensity = 2.4f * level;
            }
        }
    }
}
