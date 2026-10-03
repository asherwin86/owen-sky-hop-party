#if UNITY_EDITOR
using System.Text;
using UnityEngine;

namespace SkyHop
{
    /// <summary>Editor-only: lets a bot play the whole course and reports timings.</summary>
    public class AutoTest : MonoBehaviour
    {
        private float _t, _lastCp;
        private readonly StringBuilder _log = new StringBuilder();
        private BotController _bot;
        private bool _started, _done;
        private int _falls;
        private float _logT;

        private void Update()
        {
            var gf = GameFlow.I;
            if (gf == null) return;
            if (!_started)
            {
                _started = true;
                var typeStart = typeof(GameFlow).GetMethod("StartSolo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                typeStart.Invoke(gf, null);
                // replace local control with a bot for the player
                Destroy(gf.lc);
                _bot = gf.me.gameObject.AddComponent<BotController>(); _bot.player = gf.me; _bot.skill = 1f;
                gf.me.OnFell += p => { _falls++; _log.AppendLine("  fell at cp" + p.cpIndex + " t=" + _t.ToString("0.0")); _bot.ResetTo(p.cpIndex); };
                Time.timeScale = 1.5f;
                return;
            }
            if (_done) return;
            _logT += Time.unscaledDeltaTime;
            if (_logT > 1.5f) { _logT = 0f; var pp = gf.me.transform.position; var w = Course.I.Waypoints; Debug.Log("[bot] wp=" + _bot.Wp + " " + (_bot.Wp < w.Count ? w[_bot.Wp].name : "-") + " pos=" + pp.ToString("0.0") + " gnd=" + gf.me.Grounded + " vy=" + gf.me.Velocity.y.ToString("0.0") + " cp=" + gf.me.cpIndex); }
            _t += Time.deltaTime / Time.timeScale * Time.timeScale;
            if (gf.me.cpIndex != (int)_lastCp) { _lastCp = gf.me.cpIndex; _log.AppendLine("cp" + gf.me.cpIndex + " at " + _t.ToString("0.0")); }
            if (gf.me.finished || _t > 600f)
            {
                _done = true;
                _log.AppendLine(gf.me.finished ? "FINISHED in " + gf.me.finishTime.ToString("0.0") + "s, falls=" + _falls : "TIMEOUT cp=" + gf.me.cpIndex + " falls=" + _falls);
                System.IO.File.WriteAllText("autotest_report.txt", _log.ToString());
                Debug.Log("[AutoTest]\n" + _log);
                Time.timeScale = 1f;
            }
        }
    }
}
#endif
