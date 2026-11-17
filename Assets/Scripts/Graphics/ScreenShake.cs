using UnityEngine;

namespace Elementa
{
    /// <summary>Camera shake driven by explosions and heavy impacts.</summary>
    public class ScreenShake : MonoBehaviour
    {
        public static ScreenShake I { get; private set; }
        float _trauma;
        public float Decay = 1.6f;
        public float MaxOffset = 0.6f;

        void Awake()
        {
            I = this;
        }

        public static void Blast(Vector3 worldPosition, float power)
        {
            if (I == null) return;
            var cam = Camera.main;
            float dist = cam != null ? Vector3.Distance(cam.transform.position, worldPosition) : 10f;
            float falloff = Mathf.Clamp01(1f - dist / 40f);
            I._trauma = Mathf.Clamp01(I._trauma + power * 0.12f * falloff);
        }

        public static void Impact(float strength) { if (I != null) I._trauma = Mathf.Clamp01(I._trauma + strength * 0.05f); }

        public Vector3 Offset
        {
            get
            {
                if (_trauma <= 0.001f) return Vector3.zero;
                float shake = _trauma * _trauma;
                float t = Time.time * 34f;
                return new Vector3(
                    Mathf.PerlinNoise(t, 0.3f) * 2f - 1f,
                    Mathf.PerlinNoise(0.7f, t) * 2f - 1f,
                    0f) * (MaxOffset * shake);
            }
        }

        public float Roll => _trauma > 0.001f
            ? (Mathf.PerlinNoise(Time.time * 21f, 5.1f) * 2f - 1f) * _trauma * 1.8f
            : 0f;

        void Update()
        {
            _trauma = Mathf.Max(0f, _trauma - Decay * Time.deltaTime * (0.4f + _trauma));
        }
    }

    /// <summary>Simple third-person orbit camera: mouse/keyboard on PC, two fingers on touch.</summary>
    public class CameraRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public Vector3 Focus = new Vector3(0f, 1f, 0f);
        public float Distance = 16f;
        public float Yaw = 35f;
        public float Pitch = 38f;
        public float MinDistance = 3.5f, MaxDistance = 45f;
        public float MinPitch = 5f, MaxPitch = 85f;
        public float RotateSpeed = 220f;
        public float PanSpeed = 1f;
        public bool InputEnabled = true;

        Vector3 _targetFocus;
        float _targetDistance, _targetYaw, _targetPitch;
        Vector3 _focusVel;
        bool _dragging;
        Vector3 _lastMouse;
        float _lastPinch = -1f;

        public static CameraRig Create()
        {
            var go = new GameObject("CameraRig");
            var rig = go.AddComponent<CameraRig>();
            var camGo = new GameObject("MainCamera");
            camGo.transform.SetParent(go.transform, false);
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 400f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.backgroundColor = new Color(0.055f, 0.065f, 0.085f);
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            rig.Cam = cam;
            rig._targetFocus = rig.Focus;
            rig._targetDistance = rig.Distance;
            rig._targetYaw = rig.Yaw;
            rig._targetPitch = rig.Pitch;
            rig.ResetView();
            return rig;
        }

        public void ResetView()
        {
            _targetFocus = new Vector3(0f, 1.4f, 0f);
            _targetYaw = 35f; _targetPitch = 38f; _targetDistance = 16f;
        }

        public void Frame(Vector3 position, float distance = 12f)
        {
            _targetFocus = position;
            _targetDistance = Mathf.Clamp(distance, MinDistance, MaxDistance);
        }

        public void ZoomTo(Vector3 worldPoint)
        {
            _targetFocus = worldPoint;
            _targetPitch = Mathf.Lerp(_targetPitch, 55f, 0.5f);
            _targetDistance = 8f;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (InputEnabled) HandleInput(dt);

            _targetDistance = Mathf.Clamp(_targetDistance, MinDistance, MaxDistance);
            _targetPitch = Mathf.Clamp(_targetPitch, MinPitch, MaxPitch);

            Distance = Mathf.Lerp(Distance, _targetDistance, 1f - Mathf.Exp(-12f * dt));
            Yaw = Mathf.LerpAngle(Yaw, _targetYaw, 1f - Mathf.Exp(-14f * dt));
            Pitch = Mathf.Lerp(Pitch, _targetPitch, 1f - Mathf.Exp(-14f * dt));
            Focus = Vector3.SmoothDamp(Focus, _targetFocus, ref _focusVel, 0.12f);

            var rot = Quaternion.Euler(Pitch, Yaw, 0f);
            var pos = Focus - rot * Vector3.forward * Distance;
            var shake = ScreenShake.I != null ? ScreenShake.I.Offset : Vector3.zero;
            Cam.transform.position = pos + rot * shake;
            Cam.transform.rotation = rot * Quaternion.Euler(0f, 0f, ScreenShake.I != null ? ScreenShake.I.Roll : 0f);
        }

        void HandleInput(float dt)
        {
            if (Game.I != null && Game.I.UI != null && Game.I.UI.BlocksCameraInput) return;

            if (Input.touchCount > 0 && (Game.I == null || Game.I.IsMobile))
            {
                HandleTouch();
                return;
            }

            // mouse orbit (right or middle button, or left when no tool is placing)
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f) _targetDistance -= scroll * (Distance * 0.12f + 0.4f);

            if (Input.GetMouseButtonDown(1) || (Input.GetKey(KeyCode.LeftAlt) && Input.GetMouseButtonDown(0)))
            {
                _dragging = true; _lastMouse = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(1) || Input.GetKeyUp(KeyCode.LeftAlt)) _dragging = false;

            if (_dragging)
            {
                var delta = Input.mousePosition - _lastMouse;
                _lastMouse = Input.mousePosition;
                _targetYaw += delta.x * 0.22f;
                _targetPitch -= delta.y * 0.18f;
            }

            // WASD pan on the ground plane
            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += Vector3.forward;
            if (Input.GetKey(KeyCode.S)) move += Vector3.back;
            if (Input.GetKey(KeyCode.A)) move += Vector3.left;
            if (Input.GetKey(KeyCode.D)) move += Vector3.right;
            if (move.sqrMagnitude > 0.01f)
            {
                var flatRot = Quaternion.Euler(0f, Yaw, 0f);
                _targetFocus += flatRot * move.normalized * (Distance * 0.55f) * dt * PanSpeed * 3f;
            }
            if (Input.GetKey(KeyCode.Q)) _targetYaw -= RotateSpeed * dt * 0.4f;
            if (Input.GetKey(KeyCode.E)) _targetYaw += RotateSpeed * dt * 0.4f;
            if (Input.GetKeyDown(KeyCode.F)) Frame(_targetFocus, 12f);
        }

        void HandleTouch()
        {
            if (Input.touchCount == 1)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Moved)
                {
                    _targetYaw += t.deltaPosition.x * 0.35f;
                    _targetPitch -= t.deltaPosition.y * 0.3f;
                }
            }
            else if (Input.touchCount >= 2)
            {
                var a = Input.GetTouch(0);
                var b = Input.GetTouch(1);
                float pinch = Vector2.Distance(a.position, b.position);
                if (_lastPinch > 0f)
                {
                    float diff = pinch - _lastPinch;
                    _targetDistance -= diff * 0.05f * (Distance * 0.15f + 0.5f);
                }
                _lastPinch = pinch;
                if (a.phase == TouchPhase.Ended || b.phase == TouchPhase.Ended) _lastPinch = -1f;
            }
        }

        /// <summary>Converts a screen point to the ground plane (y = 0).</summary>
        public bool ScreenToGround(Vector3 screenPos, out Vector3 world)
        {
            var ray = Cam.ScreenPointToRay(screenPos);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float enter))
            {
                world = ray.GetPoint(enter);
                return true;
            }
            world = Vector3.zero;
            return false;
        }
    }
}
