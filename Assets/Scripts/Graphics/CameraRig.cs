using UnityEngine;

namespace Elementa
{
    /// <summary>
    /// Камера «2D сбоку» в духе Melon Sandbox: ортографическая, всегда смотрит по оси Z,
    /// двигается только в плоскости X/Y. Колесо — зум, ПКМ/СКМ или пробел+ЛКМ — панорама,
    /// два пальца — панч-зум. Вращения камеры нет: это принципиально для 2D-вида.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public Vector3 Focus = new Vector3(0f, 4f, -20f);
        public float OrthoSize = 9f;

        public float MinOrtho = 2.5f;
        public float MaxOrtho = 34f;
        public float PanSpeed = 1f;
        public bool InputEnabled = true;

        /// <summary>Глубина, на которой стоит камера (плоскость симуляции z = 0).</summary>
        public const float PlaneZ = 0f;

        Vector3 _targetFocus;
        float _targetOrtho;
        Vector3 _focusVel;
        bool _dragging;
        Vector3 _lastPointer;
        float _lastPinch = -1f;

        public static CameraRig Create()
        {
            var go = new GameObject("CameraRig");
            var rig = go.AddComponent<CameraRig>();
            var camGo = new GameObject("MainCamera");
            camGo.transform.SetParent(go.transform, false);
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            // ортографическая проекция = настоящий 2D-вид без перспективных искажений
            cam.orthographic = true;
            cam.orthographicSize = rig.OrthoSize;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.backgroundColor = new Color(0.145f, 0.16f, 0.205f); // не чёрный: даже без мира видно, что игра жива
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.AddComponent<AudioListener>();
            rig.Cam = cam;
            rig._targetFocus = rig.Focus;
            rig._targetOrtho = rig.OrthoSize;
            rig.ApplyNow();
            return rig;
        }

        void ApplyNow()
        {
            Cam.orthographicSize = OrthoSize;
            Cam.transform.position = new Vector3(Focus.x, Focus.y, PlaneZ - 20f);
            Cam.transform.rotation = Quaternion.identity;
        }

        public void ResetView()
        {
            _targetFocus = new Vector3(0f, 4f, PlaneZ);
            _targetOrtho = 9f;
        }

        public void Frame(Vector3 position, float orthoSize = 9f)
        {
            _targetFocus = new Vector3(position.x, position.y, PlaneZ);
            _targetOrtho = Mathf.Clamp(orthoSize, MinOrtho, MaxOrtho);
        }

        public void ZoomTo(Vector3 worldPoint) => Frame(worldPoint, Mathf.Min(OrthoSize, 5f));

        /// <summary>Пересчитывает ortho size так, чтобы объект шириной width метров поместился.</summary>
        public float OrthoSizeForWidth(float meters) => Mathf.Clamp(meters * 9f / 16f, MinOrtho, MaxOrtho);

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (InputEnabled) HandleInput(dt);

            _targetOrtho = Mathf.Clamp(_targetOrtho, MinOrtho, MaxOrtho);
            OrthoSize = Mathf.Lerp(OrthoSize, _targetOrtho, 1f - Mathf.Exp(-14f * dt));
            // держим камеру в пределах арены, чтобы не улететь в пустоту
            float limit = WorldEnvironment.ArenaHalfSize + 8f;
            _targetFocus.x = Mathf.Clamp(_targetFocus.x, -limit, limit);
            _targetFocus.y = Mathf.Clamp(_targetFocus.y, -6f, limit);
            _targetFocus.z = PlaneZ;
            Focus = Vector3.SmoothDamp(Focus, _targetFocus, ref _focusVel, 0.09f);

            var shake = ScreenShake.I != null ? ScreenShake.I.Offset : Vector3.zero;
            Cam.orthographicSize = OrthoSize;
            Cam.transform.position = new Vector3(Focus.x + shake.x, Focus.y + shake.y, PlaneZ - 20f);
            Cam.transform.rotation = Quaternion.identity;
        }

        void HandleInput(float dt)
        {
            bool overUI = Game.I != null && Game.I.UI != null && Game.I.UI.BlocksCameraInput;

            if (Input.touchCount > 0 && (Game.I == null || Game.I.IsMobile))
            {
                HandleTouch(overUI);
                return;
            }

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                _targetOrtho -= scroll * (_targetOrtho * 0.11f + 0.25f);

            // панорама: ПКМ, СКМ или Alt+ЛКМ (ЛКМ занята инструментами)
            if (!overUI && (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2) || (Input.GetKey(KeyCode.LeftAlt) && Input.GetMouseButtonDown(0))))
            {
                _dragging = true;
                _lastPointer = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2) || Input.GetKeyUp(KeyCode.LeftAlt))
                _dragging = false;

            if (_dragging)
            {
                var delta = Input.mousePosition - _lastPointer;
                _lastPointer = Input.mousePosition;
                float worldPerPixel = (OrthoSize * 2f) / Mathf.Max(1f, Screen.height);
                _targetFocus -= new Vector3(delta.x, delta.y, 0f) * worldPerPixel;
            }

            // WASD / стрелки — панорама, Q/E — зум, F — сброс вида
            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.y -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1f;
            if (move.sqrMagnitude > 0.01f)
                _targetFocus += new Vector3(move.x, move.y, 0f).normalized * (OrthoSize * 1.5f) * dt * PanSpeed;
            if (Input.GetKey(KeyCode.Q)) _targetOrtho += 12f * dt * PanSpeed * Mathf.Max(1f, OrthoSize * 0.25f);
            if (Input.GetKey(KeyCode.E)) _targetOrtho -= 12f * dt * PanSpeed * Mathf.Max(1f, OrthoSize * 0.25f);
            if (Input.GetKeyDown(KeyCode.F)) ResetView();
        }

        void HandleTouch(bool overUI)
        {
            if (Input.touchCount == 1)
            {
                var t = Input.GetTouch(0);
                if (!overUI && t.phase == TouchPhase.Moved)
                {
                    float worldPerPixel = (OrthoSize * 2f) / Mathf.Max(1f, Screen.height);
                    _targetFocus -= new Vector3(t.deltaPosition.x, t.deltaPosition.y, 0f) * worldPerPixel;
                }
            }
            else if (Input.touchCount >= 2)
            {
                var a = Input.GetTouch(0);
                var b = Input.GetTouch(1);
                float pinch = Vector2.Distance(a.position, b.position);
                if (_lastPinch > 0f)
                    _targetOrtho -= (pinch - _lastPinch) * 0.02f * (OrthoSize * 0.12f + 0.3f);
                _lastPinch = pinch;
                if (a.phase == TouchPhase.Ended || b.phase == TouchPhase.Ended) _lastPinch = -1f;
            }
        }

        /// <summary>Переводит экранную точку в плоскость симуляции (z = 0) — основа всех инструментов.</summary>
        public bool ScreenToWorld(Vector3 screenPos, out Vector3 world)
        {
            var ray = Cam.ScreenPointToRay(screenPos);
            var plane = new Plane(Vector3.forward, new Vector3(0f, 0f, PlaneZ));
            if (plane.Raycast(ray, out float enter))
            {
                world = ray.GetPoint(enter);
                return true;
            }
            world = Vector3.zero;
            return false;
        }

        /// <summary>Совместимость со старым кодом: «земля» теперь просто плоскость симуляции.</summary>
        public bool ScreenToGround(Vector3 screenPos, out Vector3 world) => ScreenToWorld(screenPos, out world);
    }
}
