using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>Rotating rooftop searchlight: slow azimuth sweep with a gentle tilt oscillation.</summary>
    public sealed class SearchlightSweep : MonoBehaviour
    {
        public float Speed = 0.3f;
        public float Phase;

        private void Update()
        {
            float t = Time.time * Speed + Phase;
            float yaw = t * 57.3f;
            float tilt = 18f + 10f * Mathf.Sin(t * 1.7f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(tilt, 0f, 0f);
        }
    }
}
