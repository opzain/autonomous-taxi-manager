using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>Spins an object around the world up axis.</summary>
    public sealed class Spinner : MonoBehaviour
    {
        public float DegreesPerSecond = 10f;

        private void Update() => transform.Rotate(0f, DegreesPerSecond * Time.deltaTime, 0f, Space.World);
    }
}
