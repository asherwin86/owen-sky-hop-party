using System.Collections.Generic;
using UnityEngine;

namespace SkyHop
{
    /// <summary>A bean-shaped hopper. Driven by a controller (local input, bot) that fills in the Input* fields.</summary>
    [DefaultExecutionOrder(0)]
    public class Player : MonoBehaviour
    {
        public static readonly List<Player> All = new List<Player>();

        // inputs, set by a controller before this runs
        public Vector3 inWorld;        // desired move direction in world space (length 0..1)
        public bool inJumpDown, inJumpHeld;

        public string pname = "Player";
        public Color color = Color.white;
        public bool controlsEnabled;
        public bool Alive = true;
        public bool Grounded { get; private set; }
        public Vector3 Velocity => _vel;
        public int cpIndex;            // last checkpoint reached
        public bool finished;
        public float finishTime;
        public int netId = -1;
        public bool isRemote;

        public System.Action<Player> OnFell;
        public System.Action<Player> OnJump, OnLand;

        private CharacterController _cc;
        private Vector3 _vel;
        private float _coyote, _jumpBuf, _heading;
        private int _airJumps;
        private bool _cut;
        private Transform _visual, _footL, _footR;
        private Material _bodyMat;
        private float _squash, _runPhase;
        private ParticleSystem _dust;
        private float _padCool;

        public const float Speed = 8f, JumpV = 10.5f, AirJumpV = 9.5f, Gravity = 28f;

        public static Player Create(string name, Color c, Vector3 pos, bool remote = false)
        {
            var go = new GameObject("Player_" + name);
            go.transform.position = pos;
            var p = go.AddComponent<Player>();
            p.pname = name; p.color = c; p.isRemote = remote;
            p.Build();
            return p;
        }

        private void Build()
        {
            _heading = 0f;
            if (!isRemote)
            {
                _cc = gameObject.AddComponent<CharacterController>();
                _cc.height = 1.7f; _cc.radius = 0.42f; _cc.center = new Vector3(0f, 0.85f, 0f);
                _cc.stepOffset = 0.35f; _cc.slopeLimit = 55f; _cc.skinWidth = 0.05f; _cc.minMoveDistance = 0f;
            }
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            _bodyMat = Mats.Lit(color, 0.45f);
            _bodyMat = new Material(_bodyMat);
            var body = Mats.Prim(PrimitiveType.Capsule, _visual, new Vector3(0f, 0.85f, 0f), new Vector3(0.9f, 0.82f, 0.9f), _bodyMat);
            var white = Mats.Unlit(Color.white); var dark = Mats.Unlit(new Color(0.08f, 0.08f, 0.12f));
            foreach (float sx in new[] { -0.17f, 0.17f })
            {
                Mats.Prim(PrimitiveType.Sphere, _visual, new Vector3(sx, 1.18f, 0.34f), new Vector3(0.22f, 0.26f, 0.12f), white);
                Mats.Prim(PrimitiveType.Sphere, _visual, new Vector3(sx, 1.17f, 0.4f), new Vector3(0.1f, 0.12f, 0.06f), dark);
            }
            var foot = Mats.Lit(color * 0.7f + Color.black * 0.3f, 0.2f);
            _footL = Mats.Prim(PrimitiveType.Sphere, _visual, new Vector3(-0.2f, 0.12f, 0.05f), new Vector3(0.3f, 0.2f, 0.42f), foot).transform;
            _footR = Mats.Prim(PrimitiveType.Sphere, _visual, new Vector3(0.2f, 0.12f, 0.05f), new Vector3(0.3f, 0.2f, 0.42f), foot).transform;
            _dust = Fx.Make(transform, Vector3.up * 0.1f, new Color(1f, 1f, 1f, 0.8f), 0.22f, 0.35f, 1.5f, 0f, 24, true);
        }

        public void SetColor(Color c)
        {
            color = c;
            if (_bodyMat != null) _bodyMat.color = c;
        }

        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        public Vector3 Center => transform.position + Vector3.up * 0.9f;

        public void Teleport(Vector3 pos, float yaw)
        {
            if (_cc != null) _cc.enabled = false;
            transform.position = pos;
            if (_cc != null) _cc.enabled = true;
            _vel = Vector3.zero; _heading = yaw;
            _visual.rotation = Quaternion.Euler(0f, yaw, 0f);
            Alive = true;
        }

        public void Knock(Vector3 dir, float power)
        {
            dir.y = 0f;
            _vel = dir.normalized * power + Vector3.up * 7f;
            Grounded = false; _coyote = 0f;
            Sfx.PlayAt("knock", transform.position, 0.8f);
        }

        public void Launch(Vector3 v)
        {
            _vel = v; Grounded = false; _coyote = 0f; _airJumps = 1; _squash = -0.5f; _cut = true;
        }

        private void Update()
        {
            if (isRemote) { Animate(RemoteSpeed); return; }
            Physics.SyncTransforms();

            if (inJumpDown) _jumpBuf = 0.14f; else _jumpBuf -= Time.deltaTime;
            Vector3 want = controlsEnabled ? inWorld : Vector3.zero;
            if (want.sqrMagnitude > 1f) want.Normalize();

            // ground probe
            Vector3 feet = transform.position;
            bool hit = Physics.SphereCast(feet + Vector3.up * 0.6f, 0.3f, Vector3.down, out RaycastHit gh, 0.6f + 0.18f, ~0, QueryTriggerInteraction.Ignore);
            bool wasGrounded = Grounded;
            Grounded = hit && _vel.y <= 0.5f;

            Vector3 carry = Vector3.zero;
            if (Grounded)
            {
                var mover = gh.collider.GetComponentInParent<IMover>();
                if (mover != null) carry = mover.DeltaAt(feet);
                var tile = gh.collider.GetComponentInParent<VanishingTile>();
                if (tile != null) tile.Touch();
            }

            if (Grounded) { _coyote = 0.12f; _airJumps = 1; _cut = false; if (!wasGrounded) Landed(); }
            else _coyote -= Time.deltaTime;

            // horizontal
            Vector3 h = new Vector3(_vel.x, 0f, _vel.z);
            Vector3 target = want * Speed;
            float accel = want.sqrMagnitude > 0.01f ? (Grounded ? 55f : 22f) : (Grounded ? 60f : 6f);
            h = Vector3.MoveTowards(h, target, accel * Time.deltaTime);
            _vel.x = h.x; _vel.z = h.z;

            // jump
            if (_jumpBuf > 0f && controlsEnabled)
            {
                if (_coyote > 0f)
                {
                    DoJump(JumpV, false);
                }
                else if (_airJumps > 0)
                {
                    _airJumps--; DoJump(AirJumpV, true);
                }
            }
            if (!inJumpHeld && _vel.y > 3.5f && !_cut) { _vel.y *= 0.55f; _cut = true; }

            // gravity
            if (Grounded && _vel.y <= 0f) _vel.y = -3f;
            else _vel.y = Mathf.Max(_vel.y - Gravity * Time.deltaTime, -42f);

            // jump pads
            _padCool -= Time.deltaTime;
            if (_padCool <= 0f)
                foreach (var pad in JumpPad.All)
                    if (pad.Overlaps(feet)) { pad.Pulse(); Launch(pad.transform.TransformDirection(pad.launch)); _padCool = 0.5f; Sfx.PlayAt("pad", transform.position); OnJump?.Invoke(this); break; }

            _cc.Move(_vel * Time.deltaTime + carry);
            if (_cc.isGrounded == false && Grounded && _vel.y > 0f) Grounded = false;

            // facing
            if (want.sqrMagnitude > 0.04f)
            {
                float tgt = Mathf.Atan2(want.x, want.z) * Mathf.Rad2Deg;
                _heading = Mathf.LerpAngle(_heading, tgt, 1f - Mathf.Exp(-18f * Time.deltaTime));
            }
            Animate(new Vector2(_vel.x, _vel.z).magnitude);

            if (transform.position.y < Course.KillY && Alive) { Alive = false; OnFell?.Invoke(this); }
        }

        public float RemoteSpeed;

        private void DoJump(float v, bool air)
        {
            _vel.y = v; _jumpBuf = 0f; _coyote = 0f; _cut = false; Grounded = false;
            _squash = -0.35f;
            Sfx.PlayAt(air ? "djump" : "jump", transform.position);
            if (air) _dust.Emit(8);
            OnJump?.Invoke(this);
        }

        private void Landed()
        {
            _squash = 0.4f;
            Sfx.PlayAt("land", transform.position, 0.6f);
            _dust.Emit(6);
            OnLand?.Invoke(this);
        }

        public void RemoteSquashJump() { _squash = -0.3f; }

        private void Animate(float speed)
        {
            _squash = Mathf.MoveTowards(_squash, 0f, Time.deltaTime * 2.8f);
            float sy = 1f + _squash * 0.5f - Mathf.Clamp(_vel.y * 0.006f, -0.1f, 0.1f);
            float sxz = 1f / Mathf.Sqrt(Mathf.Max(0.5f, sy));
            _visual.localScale = new Vector3(sxz, sy, sxz);
            _visual.rotation = Quaternion.Euler(0f, _heading, 0f);
            bool run = Grounded && speed > 0.5f;
            _runPhase += Time.deltaTime * (6f + speed);
            float a = run ? Mathf.Sin(_runPhase) : 0f;
            _footL.localPosition = new Vector3(-0.2f, 0.12f + Mathf.Max(0f, a) * 0.15f, 0.05f + a * 0.2f);
            _footR.localPosition = new Vector3(0.2f, 0.12f + Mathf.Max(0f, -a) * 0.15f, 0.05f - a * 0.2f);
        }

        public void SetHeading(float yaw) { _heading = yaw; }
        public float Heading => _heading;
    }
}
