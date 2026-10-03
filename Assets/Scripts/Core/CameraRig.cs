using UnityEngine;

namespace SkyHop
{
    /// <summary>Third-person orbit camera. Drag (right mouse / touch) or Q/E to look around.</summary>
    public class CameraRig : MonoBehaviour
    {
        public static float Yaw;
        public Player target;
        public Camera cam;
        public Vector3 menuCenter = new Vector3(0f, 3f, 6f);
        private float _yaw, _pitch = 22f, _dist = 9f, _fov = 62f;
        private Vector3 _focus;
        private bool _init;

        public static CameraRig Create()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var c = go.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.55f, 0.82f, 1f);
            c.nearClipPlane = 0.3f; c.farClipPlane = 700f; c.fieldOfView = 62f;
            go.AddComponent<AudioListener>();
            var rig = go.AddComponent<CameraRig>();
            rig.cam = c;
            return rig;
        }

        public void Snap(Player p)
        {
            target = p;
            if (p != null) { _yaw = 0f; Yaw = 0f; _focus = p.transform.position + Vector3.up * 1.4f; _init = true; Place(1f); }
        }

        private bool _menuMode;
        public bool MenuMode { get => _menuMode; set { _menuMode = value; } }

        private void LateUpdate()
        {
            if (target == null) return;
            if (_menuMode)
            {
                _yaw += 12f * Time.deltaTime;
                _pitch = Mathf.Lerp(_pitch, 24f, Time.deltaTime * 2f);
            }
            else
            {
                Vector2 look = HopInput.Look();
                _yaw += look.x; _pitch = Mathf.Clamp(_pitch + look.y, 6f, 65f);
            }
            Yaw = _yaw;
            Place(Time.deltaTime);
        }

        private void Place(float dt)
        {
            Vector3 focusTarget = target.transform.position + Vector3.up * 1.5f;
            if (!_init) { _focus = focusTarget; _init = true; }
            _focus = Vector3.Lerp(_focus, focusTarget, Mathf.Clamp01(dt * 9f));
            // y follows slightly slower so jumps feel floaty
            _focus.y = Mathf.Lerp(_focus.y, focusTarget.y, Mathf.Clamp01(dt * 4f));
            float dist = _menuMode ? 8f : _dist;
            Quaternion q = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desired = _focus - q * Vector3.forward * dist;
            if (Physics.SphereCast(_focus, 0.25f, desired - _focus, out RaycastHit h, dist, ~0, QueryTriggerInteraction.Ignore) && !h.collider.GetComponentInParent<Player>())
                desired = _focus + (desired - _focus).normalized * Mathf.Max(1.2f, h.distance - 0.1f);
            transform.position = desired;
            transform.rotation = Quaternion.LookRotation(_focus - desired, Vector3.up);
            if (_menuMode) transform.position += transform.right * 3.4f;
            float spd = new Vector2(target.Velocity.x, target.Velocity.z).magnitude;
            _fov = Mathf.Lerp(_fov, 62f + Mathf.Clamp01(spd / 14f) * 6f, Mathf.Clamp01(dt * 4f));
            cam.fieldOfView = _fov;
        }
    }
}
