using System.Collections.Generic;
using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// Autonomous eVTOL air taxis (Neon Mumbai 2077): they lift off from rooftops and landmarks,
    /// cruise in smooth arcs above the towers with banking turns, hover, and land at the next pad.
    /// Purely visual; part of the "Neon" layer and hidden in the realistic style.
    /// </summary>
    public sealed class AirTraffic : MonoBehaviour
    {
        private sealed class Drone
        {
            public Transform T;
            public Vector3 From, To;
            public float Progress, Duration, Hover, Cruise;
            public Vector3 LastPos;
        }

        private readonly List<Drone> _drones = new List<Drone>();
        private readonly List<Vector3> _pads = new List<Vector3>();
        private Pcg32 _rng;

        public void Init(WorldContext ctx, int count)
        {
            _rng = new Pcg32(4096UL);
            foreach (BuildingRecord b in ctx.Towers.Records)
                if (b.Height > 8f && !b.Cylinder && b.Width > 3f) _pads.Add(new Vector3(b.Center.x, b.Top + 0.1f, b.Center.y));
            // Landmark pads (skip floating labels such as the giant hologram, which is not a landing spot)
            foreach (LandmarkInfo lm in ctx.Landmarks)
                if (lm.Position.y < 18f && lm.NodeId >= 0) _pads.Add(lm.Position + Vector3.up * 2f);
            if (_pads.Count < 2) return;

            Color[] strips = { new Color(0.2f, 1f, 1.2f), new Color(1.2f, 0.2f, 0.9f), new Color(1.2f, 0.7f, 0.15f) };
            for (int i = 0; i < count; i++)
            {
                Color strip = strips[i % strips.Length];
                GameObject go = VehicleFactory.Spawn(ctx.Mats, VehicleKind.AirTaxi, strip, transform, $"AirTaxi_{i + 1:00}");
                go.transform.localScale = Vector3.one * 1.6f;
                var d = new Drone { T = go.transform, From = Pad(), To = Pad() };
                StartLeg(d);
                d.Progress = _rng.Range(0f, 0.9f);
                _drones.Add(d);
            }
        }

        private Vector3 Pad() => _pads[_rng.Range(0, _pads.Count)];

        private void StartLeg(Drone d)
        {
            float dist = Vector3.Distance(d.From, d.To);
            d.Duration = Mathf.Max(6f, dist / 14f);
            d.Cruise = Mathf.Max(d.From.y, d.To.y) + _rng.Range(8f, 16f);
            d.Progress = 0f;
            d.Hover = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            foreach (Drone d in _drones)
            {
                if (d.Hover > 0f)
                {
                    d.Hover -= dt;
                    if (d.Hover <= 0f)
                    {
                        d.From = d.To;
                        int guard = 0;
                        do { d.To = Pad(); } while ((d.To - d.From).sqrMagnitude < 400f && ++guard < 12);
                        StartLeg(d);
                    }
                    continue;
                }

                d.Progress += dt / d.Duration;
                if (d.Progress >= 1f)
                {
                    d.Progress = 1f;
                    d.Hover = _rng.Range(2f, 5f);
                }

                float k = Mathf.SmoothStep(0f, 1f, d.Progress);
                Vector3 flat = Vector3.Lerp(d.From, d.To, k);
                // Climb vertically first, cruise, then descend: a smooth "table-top" altitude profile
                float lift = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.2f, d.Progress)) *
                             Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.8f, d.Progress));
                float baseY = Mathf.Lerp(d.From.y, d.To.y, k);
                Vector3 pos = new Vector3(flat.x, Mathf.Lerp(baseY, d.Cruise, lift), flat.z);

                Vector3 vel = (pos - d.LastPos) / Mathf.Max(dt, 1e-4f);
                d.LastPos = pos;
                d.T.position = pos;
                Vector3 horizontal = new Vector3(vel.x, 0f, vel.z);
                if (horizontal.sqrMagnitude > 0.5f)
                {
                    Quaternion look = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
                    float turn = Vector3.SignedAngle(d.T.forward, horizontal, Vector3.up);
                    Quaternion bank = Quaternion.Euler(4f, 0f, Mathf.Clamp(-turn * 0.8f, -18f, 18f));
                    d.T.rotation = Quaternion.Slerp(d.T.rotation, look * bank, 1f - Mathf.Exp(-dt * 2.5f));
                }
            }
        }
    }
}
