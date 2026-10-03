using System.Collections.Generic;
using UnityEngine;

namespace SkyHop
{
    /// <summary>Anything that moves and can carry a player standing on it.</summary>
    public interface IMover { Vector3 DeltaAt(Vector3 worldPoint); }

    [DefaultExecutionOrder(-50)]
    public class SlidingPlatform : MonoBehaviour, IMover
    {
        public Vector3 axis = Vector3.right;
        public float amplitude = 5f, speed = 1f, phase;
        private Vector3 _base, _prev;
        private void Awake() { _base = transform.position; _prev = _base; }
        private void Update()
        {
            _prev = transform.position;
            transform.position = _base + axis * (Mathf.Sin(Time.time * speed + phase) * amplitude);
        }
        public Vector3 DeltaAt(Vector3 p) => transform.position - _prev;
    }

    [DefaultExecutionOrder(-50)]
    public class RotatingThing : MonoBehaviour, IMover
    {
        public Vector3 localAxis = Vector3.forward;
        public float degPerSec;      // constant spin when > 0 and wobbleDeg == 0
        public float wobbleDeg, wobbleSpeed = 1.2f;
        private Quaternion _base, _prevRot;
        private void Awake() { _base = transform.rotation; _prevRot = _base; }
        private void Update()
        {
            _prevRot = transform.rotation;
            if (wobbleDeg > 0f) transform.rotation = _base * Quaternion.AngleAxis(Mathf.Sin(Time.time * wobbleSpeed) * wobbleDeg, localAxis);
            else transform.rotation = _prevRot * Quaternion.AngleAxis(degPerSec * Time.deltaTime, localAxis);
        }
        public Vector3 DeltaAt(Vector3 p)
        {
            Quaternion d = transform.rotation * Quaternion.Inverse(_prevRot);
            Vector3 pivot = transform.position;
            return d * (p - pivot) + pivot - p;
        }
    }

    /// <summary>Tile that wobbles then drops when stepped on, and comes back later.</summary>
    public class VanishingTile : MonoBehaviour
    {
        private float _t = -1f;
        private Vector3 _home;
        private Collider[] _cols;
        private Renderer[] _rends;
        private void Awake() { _home = transform.position; _cols = GetComponentsInChildren<Collider>(); _rends = GetComponentsInChildren<Renderer>(); }
        public void Touch() { if (_t < 0f) _t = 0f; }
        private void Update()
        {
            if (_t < 0f) return;
            _t += Time.deltaTime;
            if (_t < 0.7f) transform.position = _home + new Vector3(Mathf.Sin(_t * 70f) * 0.05f, 0f, 0f);
            else if (_t < 3.4f)
            {
                transform.position = _home;
                SetOn(false);
            }
            else { _t = -1f; transform.position = _home; SetOn(true); }
        }
        private void SetOn(bool on)
        {
            foreach (var c in _cols) if (c != null) c.enabled = on;
            foreach (var r in _rends) if (r != null) r.enabled = on;
        }
    }

    public class JumpPad : MonoBehaviour
    {
        public static readonly List<JumpPad> All = new List<JumpPad>();
        public Vector3 launch = new Vector3(0f, 20f, 8f);
        public float radius = 1.4f;
        private float _squash;
        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }
        public bool Overlaps(Vector3 feet)
        {
            Vector3 d = feet - transform.position;
            return new Vector2(d.x, d.z).magnitude < radius && d.y > -0.4f && d.y < 0.7f;
        }
        public void Pulse() { _squash = 1f; }
        private void Update()
        {
            _squash = Mathf.MoveTowards(_squash, 0f, Time.deltaTime * 4f);
            transform.localScale = new Vector3(1f, 1f - _squash * 0.5f, 1f);
        }
    }

    /// <summary>Spinning sweeper bar that bats players away.</summary>
    public class SweeperBar : MonoBehaviour
    {
        public float halfLength = 5.5f, halfThick = 0.35f, height = 0.9f, power = 13f;
        private readonly Dictionary<Player, float> _cool = new Dictionary<Player, float>();
        private void Update()
        {
            foreach (var p in Player.All)
            {
                if (p == null || !p.Alive) continue;
                Vector3 lp = transform.InverseTransformPoint(p.transform.position);
                if (Mathf.Abs(lp.x) < halfLength + 0.4f && Mathf.Abs(lp.z) < halfThick + 0.45f && lp.y > -0.5f && lp.y < height + 0.2f)
                {
                    if (_cool.TryGetValue(p, out float until) && Time.time < until) continue;
                    _cool[p] = Time.time + 0.6f;
                    Vector3 off = p.transform.position - transform.position; off.y = 0f;
                    Vector3 tang = Vector3.Cross(Vector3.up, off).normalized;
                    p.Knock(tang, power);
                }
            }
        }
    }

    public class Drift : MonoBehaviour
    {
        public Vector3 v;
        private void Update() { transform.position += v * Time.deltaTime; }
    }
}
