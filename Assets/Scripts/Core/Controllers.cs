using System.Collections.Generic;
using UnityEngine;

namespace SkyHop
{
    /// <summary>Feeds the local player's input into a Player.</summary>
    [DefaultExecutionOrder(-10)]
    public class LocalController : MonoBehaviour
    {
        public Player player;
        private void Update()
        {
            if (player == null) return;
            Vector2 m = HopInput.Move();
            Quaternion q = Quaternion.Euler(0f, CameraRig.Yaw, 0f);
            player.inWorld = q * new Vector3(m.x, 0f, m.y);
            player.inJumpDown = HopInput.JumpDown();
            player.inJumpHeld = HopInput.JumpHeld();
        }
    }

    /// <summary>Simple waypoint-following bot, used for tests and for solo opponents.</summary>
    [DefaultExecutionOrder(-10)]
    public class BotController : MonoBehaviour
    {
        public Player player;
        public float skill = 1f;        // 0.7 sloppy .. 1 tight
        public float startDelay;
        private int _wp;
        public int Wp => _wp;
        private float _stuck, _lastDist, _jumpHold;
        private float _wobble;

        private void Update()
        {
            if (player == null || Course.I == null) return;
            var wps = Course.I.Waypoints;
            player.inJumpDown = false;
            if (!player.controlsEnabled || _wp >= wps.Count || player.finished) { player.inWorld = Vector3.zero; return; }
            // sync with checkpoint after a fall
            if (!player.Alive) return;
            Transform tw = wps[_wp];
            Vector3 to = tw.position - player.transform.position;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            float d = flat.magnitude;
            float reach = tw.name.StartsWith("pad") ? 0.8f : 1.5f;
            if (d < reach && Mathf.Abs(to.y) < 2.5f) { _wp++; _stuck = 0f; return; }
            _wobble += Time.deltaTime;
            Vector3 dir = flat.normalized;
            player.inWorld = dir * Mathf.Clamp01(0.6f + skill * 0.4f);
            player.inJumpHeld = _jumpHold > 0f;
            _jumpHold -= Time.deltaTime;

            // decide to jump
            Vector3 fp = player.transform.position + dir * 1.5f;
            bool groundAhead = Physics.Raycast(fp + Vector3.up * 0.8f, Vector3.down, 2.6f, ~0, QueryTriggerInteraction.Ignore);
            bool wallAhead = Physics.Raycast(player.transform.position + Vector3.up * 0.4f, dir, 1.0f, ~0, QueryTriggerInteraction.Ignore);
            bool targetHigher = to.y > 0.7f && d < 5f;
            if (player.Grounded && (!groundAhead || wallAhead || targetHigher) && !tw.name.StartsWith("pad"))
            {
                player.inJumpDown = true; _jumpHold = 0.35f;
            }
            else if (!player.Grounded && player.Velocity.y < -1f && d > 2.5f && !Physics.Raycast(player.transform.position + Vector3.up * 0.5f, Vector3.down, 3.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                player.inJumpDown = true; _jumpHold = 0.2f;
            }
            // stuck handling
            if (Mathf.Abs(d - _lastDist) < 0.4f * Time.deltaTime) _stuck += Time.deltaTime; else _stuck = 0f;
            _lastDist = d;
            if (_stuck > 3f && player.Grounded) { player.inJumpDown = true; _jumpHold = 0.4f; _stuck = 1.5f; }
        }

        public void ResetTo(int cp)
        {
            // continue from the waypoint nearest after the checkpoint
            _wp = Mathf.Max(0, Course.I.WaypointAfterCheckpoint(cp));
            _stuck = 0f;
        }
    }
}
