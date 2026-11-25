using System.Collections.Generic;
using TaxiSim.Core;
using UnityEngine;

namespace TaxiSim.World
{
    /// <summary>
    /// Smooth 3D city camera with full x / y / z movement:
    ///  * Pan across the map (x and z)      – WASD / arrows / middle-drag   (ESP32: arrow buttons)
    ///  * Zoom / height (y)                 – mouse wheel, Z / X            (ESP32: encoder)
    ///  * Orbit (yaw) and tilt (pitch)      – Q / E, R / F, right-drag      (ESP32: Accept / Reject in camera mode)
    ///  * Follow a fleet taxi (Space), fly between landmarks (Tab), home view (H)
    ///  * Cinematic mode (C) is the separate CinematicDirector, which takes over the camera (ExternalControl)
    /// Public methods let the ESP32 console drive the same camera later.
    /// </summary>
    public sealed class CityCameraRig : MonoBehaviour
    {
        public Camera Cam;
        [System.NonSerialized] public CityGeography Geography;
        public PostFxRig Post;
        [System.NonSerialized] public List<LandmarkInfo> Landmarks = new List<LandmarkInfo>();
        public TrafficAmbient Traffic;

        public Vector3 Pivot = new Vector3(175f, 0f, 120f);
        public float Yaw = 205f;
        public float Pitch = 34f;
        public float Distance = 150f;
        public float MinDistance = 6f;
        public float MaxDistance = 650f;
        /// <summary>While true another component (the cinematic director) drives the camera.</summary>
        [System.NonSerialized] public bool ExternalControl;
        /// <summary>While a dispatch shift runs the arrow keys drive the console; the camera keeps WASD.</summary>
        [System.NonSerialized] public bool ArrowsReserved;
        /// <summary>Street-level chase camera behind a fleet taxi (G).</summary>
        public bool StreetMode { get; private set; }

        public Transform FollowTarget { get; private set; }
        public string FollowLabel { get; private set; }
        public string FocusName { get; private set; } = "G-Block, BKC";

        private Vector3 _pivot;
        private float _yaw, _pitch, _distance;
        private int _landmarkIndex = -1;
        private int _followIndex = -1;

        private void Start()
        {
            _pivot = Pivot;
            _yaw = Yaw;
            _pitch = Pitch;
            _distance = Distance;
            ApplyTransform();
        }

        // ------------------------------------------------------------------ public API (console / UI)

        public void Pan(Vector2 axis, float dt)
        {
            float speed = Mathf.Lerp(12f, 220f, Mathf.InverseLerp(MinDistance, MaxDistance, Distance));
            Quaternion yawRot = Quaternion.Euler(0f, Yaw, 0f);
            Vector3 move = yawRot * new Vector3(axis.x, 0f, axis.y) * speed * dt;
            Pivot += move;
            StopFollow();
        }

        public void Orbit(float yawDelta, float pitchDelta)
        {
            Yaw = Mathf.Repeat(Yaw + yawDelta, 360f);
            Pitch = Mathf.Clamp(Pitch + pitchDelta, 8f, 85f);
        }

        public void Zoom(float steps)
        {
            Distance = Mathf.Clamp(Distance * Mathf.Pow(0.85f, steps), MinDistance, MaxDistance);
        }

        public void FlyTo(Vector3 position, float distance, string name)
        {
            StopFollow();
            Pivot = new Vector3(position.x, 0f, position.z);
            Distance = Mathf.Clamp(distance, MinDistance, MaxDistance);
            FocusName = name;
        }

        public void NextLandmark(int direction)
        {
            if (Landmarks.Count == 0) return;
            _landmarkIndex = (int)Mathf.Repeat(_landmarkIndex + direction, Landmarks.Count);
            LandmarkInfo lm = Landmarks[_landmarkIndex];
            FlyTo(lm.Position, 45f, lm.Name);
            Pitch = 30f;
        }

        public void FollowNextTaxi()
        {
            if (Traffic == null || Traffic.Fleet.Count == 0) return;
            _followIndex = (_followIndex + 1) % Traffic.Fleet.Count;
            FollowTarget = Traffic.Fleet[_followIndex].Transform;
            FollowLabel = Traffic.Fleet[_followIndex].Label;
            Distance = StreetMode ? 2.6f : 14f;
            Pitch = StreetMode ? 9f : 26f;
            FocusName = $"Following {FollowLabel}";
        }

        /// <summary>Follows fleet taxi number <paramref name="index"/> (AT-01 = 0).</summary>
        public void FollowFleet(int index)
        {
            if (Traffic == null || index < 0 || index >= Traffic.Fleet.Count) return;
            _followIndex = index;
            FollowTarget = Traffic.Fleet[index].Transform;
            FollowLabel = Traffic.Fleet[index].Label;
            Distance = StreetMode ? 2.6f : 16f;
            Pitch = StreetMode ? 9f : 28f;
            FocusName = $"Following {FollowLabel}";
        }

        /// <summary>G: low chase camera behind a fleet taxi, like a driving game; G again returns to the city view.</summary>
        public void ToggleStreetCam()
        {
            if (StreetMode)
            {
                StreetMode = false;
                SetLens(40f, 0.3f);
                MinDistance = 6f;
                Distance = 45f;
                Pitch = 30f;
                return;
            }
            if (FollowTarget == null) FollowNextTaxi();
            if (FollowTarget == null) return;
            StreetMode = true;
            MinDistance = 1.2f;
            Distance = 2.6f;
            Pitch = 9f;
            SetLens(58f, 0.03f);
            FocusName = $"Street camera · {FollowLabel}";
        }

        private void SetLens(float fov, float near)
        {
            if (Cam == null) return;
            Cam.fieldOfView = fov;
            Cam.nearClipPlane = near;
        }

        public void StopFollow()
        {
            if (StreetMode) { StreetMode = false; SetLens(40f, 0.3f); MinDistance = 6f; }
            if (FollowTarget == null) return;
            FollowTarget = null;
            FollowLabel = null;
            FocusName = "Free camera";
        }

        public void Home()
        {
            StopFollow();
            Pivot = new Vector3(175f, 0f, 120f);
            Yaw = 205f;
            Pitch = 34f;
            Distance = 150f;
            FocusName = "G-Block, BKC";
        }

        /// <summary>Takes over wherever the camera currently is (after the cinematic director hands it back).</summary>
        public void SyncFromCamera()
        {
            if (Cam == null) return;
            StopFollow();
            SetLens(40f, 0.3f);
            MinDistance = 6f;
            Transform t = Cam.transform;
            Vector3 pos = t.position, fwd = t.forward;
            Vector3 pivot = fwd.y < -0.05f ? pos + fwd * Mathf.Min(pos.y / -fwd.y, 400f) : pos + fwd * 60f;
            pivot.y = 0f;
            Vector3 toPivot = pivot - pos;
            if (toPivot.sqrMagnitude < 1f) { pivot = pos + new Vector3(fwd.x, 0f, fwd.z).normalized * 20f; pivot.y = 0f; toPivot = pivot - pos; }
            Quaternion look = Quaternion.LookRotation(toPivot);
            float pitch = look.eulerAngles.x > 180f ? look.eulerAngles.x - 360f : look.eulerAngles.x;
            Pivot = _pivot = pivot;
            Yaw = _yaw = look.eulerAngles.y;
            Pitch = _pitch = Mathf.Clamp(pitch, 8f, 85f);
            Distance = _distance = Mathf.Clamp(toPivot.magnitude, MinDistance, MaxDistance);
            FocusName = "Free camera";
        }

        // ------------------------------------------------------------------ update

        private void LateUpdate()
        {
            if (ExternalControl) return;
            float dt = Time.unscaledDeltaTime;
            ReadInput(dt);

            if (FollowTarget != null)
            {
                Pivot = FollowTarget.position;
                Yaw = Mathf.LerpAngle(Yaw, FollowTarget.eulerAngles.y, 1f - Mathf.Exp(-dt * 1.5f));
            }

            float k = 1f - Mathf.Exp(-dt * 6f);
            _pivot = Vector3.Lerp(_pivot, Pivot, k);
            _yaw = Mathf.LerpAngle(_yaw, Yaw, k);
            _pitch = Mathf.Lerp(_pitch, Pitch, k);
            _distance = Mathf.Lerp(_distance, Distance, k);
            ApplyTransform();
            if (Post != null) Post.SetFocus(_distance);
        }

        private void ApplyTransform()
        {
            if (Cam == null) return;
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pos = _pivot + rot * new Vector3(0f, 0f, -_distance);
            if (Geography != null)
            {
                float ground = Mathf.Max((float)Geography.GroundHeight(pos.x, pos.z), (float)CityGeography.WaterLevel);
                pos.y = Mathf.Max(pos.y, ground + (StreetMode ? 0.12f : 1.2f));
            }
            Cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(_pivot + Vector3.up * (StreetMode ? 0.25f : 0.5f) - pos));
        }

        private void ReadInput(float dt)
        {
            var axis = Vector2.zero;
            bool arrows = !ArrowsReserved;
            if (InputCompat.Held(TsKey.W) || (arrows && InputCompat.Held(TsKey.Up))) axis.y += 1f;
            if (InputCompat.Held(TsKey.S) || (arrows && InputCompat.Held(TsKey.Down))) axis.y -= 1f;
            if (InputCompat.Held(TsKey.D) || (arrows && InputCompat.Held(TsKey.Right))) axis.x += 1f;
            if (InputCompat.Held(TsKey.A) || (arrows && InputCompat.Held(TsKey.Left))) axis.x -= 1f;
            if (axis != Vector2.zero) Pan(axis.normalized, dt);

            if (InputCompat.Held(TsKey.Q)) Orbit(-70f * dt, 0f);
            if (InputCompat.Held(TsKey.E)) Orbit(70f * dt, 0f);
            if (InputCompat.Held(TsKey.R)) Orbit(0f, 40f * dt);
            if (InputCompat.Held(TsKey.F)) Orbit(0f, -40f * dt);
            if (InputCompat.Held(TsKey.Z)) Zoom(4f * dt);
            if (InputCompat.Held(TsKey.X)) Zoom(-4f * dt);

            float scroll = InputCompat.Scroll();
            if (Mathf.Abs(scroll) > 0.01f) Zoom(scroll);

            Vector2 md = InputCompat.MouseDelta();
            if (InputCompat.RightMouse()) Orbit(md.x * 0.15f, -md.y * 0.12f);
            if (InputCompat.MiddleMouse())
            {
                float scale = Distance * 0.0016f;
                Quaternion yawRot = Quaternion.Euler(0f, Yaw, 0f);
                Pivot -= yawRot * new Vector3(md.x, 0f, md.y) * scale;
                StopFollow();
            }

            if (InputCompat.Pressed(TsKey.Tab)) NextLandmark(InputCompat.Held(TsKey.LeftShift) ? -1 : 1);
            if (InputCompat.Pressed(TsKey.Space)) FollowNextTaxi();
            if (InputCompat.Pressed(TsKey.Escape)) StopFollow();
            if (InputCompat.Pressed(TsKey.H)) Home();
        }
    }
}
