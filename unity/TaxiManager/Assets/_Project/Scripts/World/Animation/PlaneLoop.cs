using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>Aircraft approaching Mumbai airport over Kurla: glide slope, touchdown, roll-out, repeat.</summary>
    public sealed class PlaneLoop : MonoBehaviour
    {
        public Vector3 Threshold;
        public Vector3 RunwayDirection = new Vector3(-0.8f, 0f, 0.6f);
        public float ApproachDistance = 700f;
        public float ApproachHeight = 36f;
        public float ApproachSeconds = 42f;
        public float RolloutDistance = 140f;
        public float RolloutSeconds = 12f;
        public float PauseSeconds = 14f;
        public float TimeScale = 1f;

        private float _t;
        private Renderer[] _renderers;
        private Light _landingLight;

        private void Start()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _landingLight = GetComponentInChildren<Light>();
            RunwayDirection.y = 0f;
            RunwayDirection.Normalize();
        }

        private void Update()
        {
            _t += Time.deltaTime * TimeScale;
            float total = ApproachSeconds + RolloutSeconds + PauseSeconds;
            if (_t > total) _t -= total;

            bool visible = _t < ApproachSeconds + RolloutSeconds;
            foreach (Renderer r in _renderers) r.enabled = visible;
            if (_landingLight != null) _landingLight.enabled = visible && _t < ApproachSeconds + 2f;
            if (!visible) return;

            Vector3 pos;
            float pitch;
            if (_t < ApproachSeconds)
            {
                float k = _t / ApproachSeconds;
                float along = Mathf.Lerp(-ApproachDistance, 0f, k);
                float flare = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.94f, 1f, k));
                float h = Mathf.Lerp(ApproachHeight, 0.25f, k) * (1f - flare * 0.6f) + 0.25f * flare;
                pos = Threshold + RunwayDirection * along + Vector3.up * Mathf.Max(h, 0.25f);
                pitch = Mathf.Lerp(2f, 6f, flare);
            }
            else
            {
                float k = (_t - ApproachSeconds) / RolloutSeconds;
                float along = RolloutDistance * (1f - (1f - k) * (1f - k));
                pos = Threshold + RunwayDirection * along + Vector3.up * 0.25f;
                pitch = 0f;
            }
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(RunwayDirection) * Quaternion.Euler(-pitch, 0f, 0f));
        }
    }
}
