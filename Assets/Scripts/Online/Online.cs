using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SkyHop
{
    /// <summary>Online free-for-all: menu, lobby, state streaming and results. The server just relays.</summary>
    public class Online : MonoBehaviour
    {
        public static Online I;
        public const string OfficialServerUrl = "wss://sky-hop-server.onrender.com";

        public enum Mode { Off, Menu, Connecting, Lobby, Race, Results }
        public Mode mode = Mode.Off;

        // ---- wire messages (JsonUtility)
        [System.Serializable] private class PInfo { public int id; public string name; public int color; public int wins; }
        [System.Serializable] private class Slot { public int id; public int slot; }
        [System.Serializable] private class Snap { public int id; public float x, y, z, h, v, pr; public int g, c, j; }
        [System.Serializable] private class Res { public int id; public string name; public float time; }
        [System.Serializable] private class Msg
        {
            public string t, code, phase, msg, name;
            public int id, hostId, winner, course;
            public bool isPublic, first;
            public float countdown, time;
            public float next = -1f;
            public PInfo[] players; public Slot[] slots; public Snap[] p; public Res[] list;
        }
        [System.Serializable] private class StateOut { public string t = "state"; public float x, y, z, h, v, pr; public int g, c, j; }
        [System.Serializable] private class FinishOut { public string t = "finish"; public float time; }
        [System.Serializable] private class NameOut { public string t; public string name; public string code; }
        [System.Serializable] private class IdOut { public string t; public int id; }
        [System.Serializable] private class TOut { public string t; }
        [System.Serializable] private class StartOut { public string t = "start"; public int course; }

        private class Remote
        {
            public Player player; public Vector3 target, vel; public float tTime, h; public int lastJ;
        }

        private GameFlow _gf;
        private Canvas _canvas;
        private GameObject _menuP, _lobbyP, _resP, _leaveP;
        private TMP_Text _status, _lobbyHead, _lobbyStatus, _resTitle, _resBody;
        private TMP_InputField _codeIn;
        private Button _startBtn;
        private readonly Image[] _rowColor = new Image[8];
        private readonly TMP_Text[] _rowName = new TMP_Text[8];
        private readonly TMP_Text[] _rowWins = new TMP_Text[8];
        private readonly Button[] _rowKick = new Button[8];

        private string _pendingAction;       // "public" | "create" | "join"
        private float _connectT, _sendT, _nextLabel = -1f;
        private int _myId, _hostId, _myJump;
        private bool _isPublic, _joined, _raceLive;
        private string _roomCode = "";
        private List<PInfo> _players = new List<PInfo>();
        private readonly Dictionary<int, Remote> _remotes = new Dictionary<int, Remote>();
        private readonly List<string> _finishLines = new List<string>();
        private string _err;

        public static void Create(GameFlow gf)
        {
            var go = new GameObject("Online");
            var o = go.AddComponent<Online>();
            o._gf = gf;
            I = o;
            gf.OnlineHook = o.Open;
            gf.OnlineFinish = o.OnMeFinished;
            gf.OnlineEsc = o.OnEsc;
            o.BuildUI();
        }

        // ------------------------------------------------------------ UI
        private void BuildUI()
        {
            _canvas = UIKit.MakeCanvas("OnlineUI", 20, transform);
            Transform ui = _canvas.transform;
            Color card = new Color(0.12f, 0.16f, 0.35f, 0.92f);

            _menuP = UIKit.Group(ui, "OnlineMenu");
            UIKit.Panel(_menuP.transform, "Dim", new Color(0f, 0.05f, 0.2f, 0.55f));
            UIKit.Box(_menuP.transform, "Card", UIKit.C, Vector2.zero, new Vector2(640f, 580f), card);
            var t = UIKit.Label(_menuP.transform, "ONLINE", 72f, UIKit.C, new Vector2(0f, 230f), new Vector2(600f, 90f)); t.fontStyle = FontStyles.Bold;
            UIKit.Btn(_menuP.transform, "QUICK RACE", UIKit.C, new Vector2(0f, 130f), new Vector2(440f, 72f), new Color(0.3f, 0.75f, 0.35f), () => { Sfx.Play("click"); Connect("public"); }, 36f);
            UIKit.Btn(_menuP.transform, "CREATE PRIVATE ROOM", UIKit.C, new Vector2(0f, 40f), new Vector2(440f, 72f), new Color(0.35f, 0.5f, 0.95f), () => { Sfx.Play("click"); Connect("create"); }, 32f);
            _codeIn = UIKit.InputBox(_menuP.transform, "ROOM CODE", UIKit.C, new Vector2(-115f, -55f), new Vector2(210f, 58f), 4);
            _codeIn.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
            _codeIn.onValueChanged.AddListener(s => { if (s != s.ToUpperInvariant()) _codeIn.text = s.ToUpperInvariant(); });
            UIKit.Btn(_menuP.transform, "JOIN", UIKit.C, new Vector2(115f, -55f), new Vector2(210f, 58f), new Color(0.95f, 0.6f, 0.2f), () => { Sfx.Play("click"); Connect("join"); }, 30f);
            _status = UIKit.Label(_menuP.transform, "Race friends or strangers. First to the flag wins!", 24f, UIKit.C, new Vector2(0f, -135f), new Vector2(580f, 70f), TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.9f));
            _status.textWrappingMode = TextWrappingModes.Normal;
            UIKit.Btn(_menuP.transform, "BACK", UIKit.C, new Vector2(0f, -230f), new Vector2(220f, 58f), new Color(0.85f, 0.4f, 0.4f), () => { Sfx.Play("click"); CloseAll(); }, 28f);

            _lobbyP = UIKit.Group(ui, "Lobby");
            UIKit.Panel(_lobbyP.transform, "Dim", new Color(0f, 0.05f, 0.2f, 0.55f));
            UIKit.Box(_lobbyP.transform, "Card", UIKit.C, Vector2.zero, new Vector2(760f, 650f), card);
            _lobbyHead = UIKit.Label(_lobbyP.transform, "", 54f, UIKit.C, new Vector2(0f, 270f), new Vector2(700f, 80f)); _lobbyHead.fontStyle = FontStyles.Bold;
            for (int i = 0; i < 8; i++)
            {
                float y = 190f - i * 46f;
                var row = UIKit.Group(_lobbyP.transform, "Row" + i, false);
                _rowColor[i] = UIKit.Box(row.transform, "C", UIKit.C, new Vector2(-300f, y), new Vector2(30f, 30f), Color.white);
                _rowName[i] = UIKit.Label(row.transform, "", 30f, UIKit.C, new Vector2(-40f, y), new Vector2(480f, 40f), TextAlignmentOptions.Left);
                _rowWins[i] = UIKit.Label(row.transform, "", 26f, UIKit.C, new Vector2(170f, y), new Vector2(160f, 40f), TextAlignmentOptions.Right, new Color(1f, 0.9f, 0.4f));
                int idx = i;
                _rowKick[i] = UIKit.Btn(row.transform, "X", UIKit.C, new Vector2(300f, y), new Vector2(44f, 36f), new Color(0.85f, 0.35f, 0.35f), () => KickRow(idx), 22f);
            }
            _lobbyStatus = UIKit.Label(_lobbyP.transform, "", 28f, UIKit.C, new Vector2(0f, -195f), new Vector2(700f, 44f), TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.92f));
            _startBtn = UIKit.Btn(_lobbyP.transform, "START RACE", UIKit.C, new Vector2(-150f, -265f), new Vector2(300f, 64f), new Color(0.3f, 0.75f, 0.35f), () => { Sfx.Play("click"); Send(new StartOut { course = _gf.CourseId }); }, 30f);
            UIKit.Btn(_lobbyP.transform, "LEAVE", UIKit.C, new Vector2(170f, -265f), new Vector2(260f, 64f), new Color(0.85f, 0.4f, 0.4f), () => { Sfx.Play("click"); Leave(); }, 30f);

            _resP = UIKit.Group(ui, "OnlineResults");
            UIKit.Panel(_resP.transform, "Dim", new Color(0f, 0f, 0f, 0.4f));
            UIKit.Box(_resP.transform, "Card", UIKit.C, Vector2.zero, new Vector2(700f, 580f), card);
            _resTitle = UIKit.Label(_resP.transform, "", 74f, UIKit.C, new Vector2(0f, 225f), new Vector2(660f, 100f)); _resTitle.fontStyle = FontStyles.Bold;
            _resBody = UIKit.Label(_resP.transform, "", 30f, UIKit.C, new Vector2(0f, 30f), new Vector2(640f, 340f), TextAlignmentOptions.Center);
            UIKit.Btn(_resP.transform, "BACK TO LOBBY", UIKit.C, new Vector2(-170f, -235f), new Vector2(300f, 66f), new Color(0.3f, 0.75f, 0.35f), () => { Sfx.Play("click"); BackToLobby(); }, 28f);
            UIKit.Btn(_resP.transform, "LEAVE", UIKit.C, new Vector2(170f, -235f), new Vector2(260f, 66f), new Color(0.85f, 0.4f, 0.4f), () => { Sfx.Play("click"); Leave(); }, 30f);

            _leaveP = UIKit.Group(ui, "LeaveConfirm");
            UIKit.Panel(_leaveP.transform, "Dim", new Color(0f, 0f, 0f, 0.5f));
            UIKit.Box(_leaveP.transform, "Card", UIKit.C, Vector2.zero, new Vector2(560f, 300f), card);
            UIKit.Label(_leaveP.transform, "Leave the race?", 52f, UIKit.C, new Vector2(0f, 80f), new Vector2(520f, 80f));
            UIKit.Btn(_leaveP.transform, "KEEP RACING", UIKit.C, new Vector2(-135f, -50f), new Vector2(240f, 64f), new Color(0.3f, 0.75f, 0.35f), () => { Sfx.Play("click"); _leaveP.SetActive(false); }, 26f);
            UIKit.Btn(_leaveP.transform, "LEAVE", UIKit.C, new Vector2(135f, -50f), new Vector2(240f, 64f), new Color(0.85f, 0.4f, 0.4f), () => { Sfx.Play("click"); Leave(); }, 28f);
            HideAll();
        }

        private void HideAll() { _menuP.SetActive(false); _lobbyP.SetActive(false); _resP.SetActive(false); _leaveP.SetActive(false); }

        // ------------------------------------------------------------ flow
        public void Open()
        {
            _gf.HideMenu();
            mode = Mode.Menu;
            HideAll(); _menuP.SetActive(true);
            _status.text = "Race friends or strangers. First to the flag wins!";
            _status.color = new Color(1f, 1f, 1f, 0.9f);
        }

        private void CloseAll()
        {
            HideAll();
            GameSocket.Close();
            mode = Mode.Off; _joined = false; _raceLive = false;
            ClearRemotes();
            _gf.OnlineMode = false;
            _gf.ReturnToMenu();
        }

        private void Connect(string action)
        {
            _gf.CommitNameFromUI();
            if (action == "join" && _codeIn.text.Trim().Length != 4) { Status("Type the 4-letter room code first.", true); return; }
            _pendingAction = action; _joined = false; _err = null;
            mode = Mode.Connecting; _connectT = 0f;
            Status("Connecting to the server... (the first time can take up to a minute while it wakes up)", false);
            string url = PlayerPrefs.GetString("hop_server", OfficialServerUrl);
            GameSocket.Connect(url);
        }

        private void Status(string s, bool err)
        {
            _status.text = s; _status.color = err ? new Color(1f, 0.6f, 0.55f) : new Color(1f, 1f, 1f, 0.9f);
        }

        private void Send(object o) { GameSocket.Send(JsonUtility.ToJson(o)); }

        private void Leave()
        {
            Send(new TOut { t = "leave" });
            CloseAll();
        }

        private void BackToLobby()
        {
            _resP.SetActive(false);
            _gf.EndOnlineRace();
            ClearRemotes();
            mode = Mode.Lobby; _lobbyP.SetActive(true); RefreshLobby();
        }

        private void OnEsc()
        {
            if (mode == Mode.Race) _leaveP.SetActive(!_leaveP.activeSelf);
        }

        private void ClearRemotes()
        {
            foreach (var r in _remotes.Values) if (r.player != null) Destroy(r.player.gameObject);
            _remotes.Clear();
        }

        // ------------------------------------------------------------ update
        private void Update()
        {
            if (mode == Mode.Off) return;

            if (mode == Mode.Connecting)
            {
                _connectT += Time.unscaledDeltaTime;
                if (GameSocket.State == GameSocket.Open && !_joined && _pendingAction != null)
                {
                    string nm = _gf.PlayerName;
                    if (_pendingAction == "public") Send(new NameOut { t = "joinpublic", name = nm });
                    else if (_pendingAction == "create") Send(new NameOut { t = "create", name = nm });
                    else Send(new NameOut { t = "join", name = nm, code = _codeIn.text.Trim() });
                    _pendingAction = null;
                }
                else if (GameSocket.State == GameSocket.Closed && _connectT > 0.5f) { FailBack("Couldn't reach the server. Check your internet and try again."); return; }
                else if (_connectT > 90f) { GameSocket.Close(); FailBack("The server took too long to answer. Try again in a moment."); return; }
            }

            while (GameSocket.TryReceive(out string raw))
            {
                Msg m;
                try { m = JsonUtility.FromJson<Msg>(raw); } catch { continue; }
                if (m == null || string.IsNullOrEmpty(m.t)) continue;
                Handle(m);
                if (mode == Mode.Off) return;
            }

            if ((mode == Mode.Lobby || mode == Mode.Race || mode == Mode.Results) && _joined && GameSocket.State == GameSocket.Closed)
            {
                HideAll(); GameSocket.Close(); mode = Mode.Off; _joined = false; ClearRemotes(); _gf.OnlineMode = false;
                _gf.ReturnToMenu();
                _gf.ShowMessage("Lost connection to the server.");
                return;
            }

            if (mode == Mode.Race) RaceTick();
            if (mode == Mode.Lobby && _isPublic) UpdateLobbyTimer();
        }

        private void FailBack(string msg)
        {
            mode = Mode.Menu; _pendingAction = null;
            Status(msg, true);
        }

        private void Handle(Msg m)
        {
            switch (m.t)
            {
                case "joined": _myId = m.id; _joined = true; break;
                case "err":
                    if (mode == Mode.Connecting || mode == Mode.Menu) { GameSocket.Close(); _joined = false; FailBack(m.msg); }
                    else _lobbyStatus.text = m.msg;
                    break;
                case "room":
                    _roomCode = m.code; _hostId = m.hostId; _isPublic = m.isPublic;
                    _players = new List<PInfo>(m.players ?? new PInfo[0]);
                    if (mode == Mode.Connecting) { mode = Mode.Lobby; HideAll(); _lobbyP.SetActive(true); }
                    if (mode == Mode.Lobby) RefreshLobby();
                    break;
                case "start": OnStart(m); break;
                case "snap": OnSnap(m); break;
                case "finished": OnFinished(m); break;
                case "results": OnResults(m); break;
                case "kicked":
                    HideAll(); GameSocket.Close(); mode = Mode.Off; _joined = false; ClearRemotes(); _gf.OnlineMode = false;
                    _gf.ReturnToMenu(); _gf.ShowMessage("You were removed from the room.");
                    break;
            }
        }

        // ------------------------------------------------------------ lobby
        private void RefreshLobby()
        {
            _lobbyHead.text = _isPublic ? "QUICK RACE" : "ROOM  <color=#FFE066>" + _roomCode + "</color>";
            bool iAmHost = !_isPublic && _hostId == _myId;
            for (int i = 0; i < 8; i++)
            {
                bool has = i < _players.Count;
                _rowColor[i].gameObject.SetActive(has); _rowName[i].gameObject.SetActive(has);
                _rowWins[i].gameObject.SetActive(has); _rowKick[i].gameObject.SetActive(has && iAmHost && _players[i].id != _myId);
                if (!has) continue;
                var p = _players[i];
                _rowColor[i].color = GameFlow.Colors[p.color % GameFlow.Colors.Length];
                _rowName[i].text = p.name + (p.id == _myId ? "  <size=70%>(you)</size>" : "") + (p.id == _hostId && !_isPublic ? "  <size=70%>HOST</size>" : "");
                _rowWins[i].text = p.wins > 0 ? p.wins + (p.wins == 1 ? " win" : " wins") : "";
            }
            _startBtn.gameObject.SetActive(iAmHost);
            if (_isPublic) UpdateLobbyTimer();
            else _lobbyStatus.text = iAmHost ? (_players.Count < 2 ? "Share the room code with a friend, then press START." : "Ready when you are!") : "Waiting for the host to start...";
        }

        private void UpdateLobbyTimer()
        {
            if (!_isPublic) return;
            if (_players.Count < 2) _lobbyStatus.text = "Waiting for more players to join...";
            else if (_nextLabel >= 0f) _lobbyStatus.text = "Next race in " + Mathf.CeilToInt(_nextLabel) + " s";
            else _lobbyStatus.text = "Get ready...";
        }

        private void KickRow(int i)
        {
            if (i < _players.Count) Send(new IdOut { t = "kick", id = _players[i].id });
        }

        // ------------------------------------------------------------ race
        private void OnStart(Msg m)
        {
            _gf.LoadCourse(m.course);
            HideAll();
            ClearRemotes();
            _finishLines.Clear();
            int mySlot = 0;
            var remotes = new List<Player>();
            var course = _gf.course;
            foreach (var s in m.slots)
            {
                if (s.id == _myId) { mySlot = s.slot; continue; }
                var info = _players.Find(p => p.id == s.id);
                if (info == null) continue;
                var pl = Player.Create(info.name, GameFlow.Colors[info.color % GameFlow.Colors.Length], course.StartSlots[Mathf.Clamp(s.slot, 0, 7)], true);
                pl.netId = s.id;
                _remotes[s.id] = new Remote { player = pl, target = pl.transform.position, tTime = Time.time };
                remotes.Add(pl);
            }
            var mine = _players.Find(p => p.id == _myId);
            if (mine != null) _gf.me.SetColor(GameFlow.Colors[mine.color % GameFlow.Colors.Length]);
            _myJump = 0; _raceLive = false; _sendT = 0f;
            _gf.me.OnJump = p => _myJump++;
            _gf.BeginOnline(remotes, Mathf.Clamp(mySlot, 0, 7), m.countdown, m.course);
            mode = Mode.Race;
        }

        private void RaceTick()
        {
            // stream my state ~15 times a second once the race is live
            if (_gf.state == GameFlow.St.Racing || _gf.state == GameFlow.St.Finished)
            {
                _sendT -= Time.unscaledDeltaTime;
                if (_sendT <= 0f)
                {
                    _sendT = 1f / 15f;
                    var me = _gf.me;
                    var vel = me.Velocity;
                    Send(new StateOut
                    {
                        x = me.transform.position.x, y = me.transform.position.y, z = me.transform.position.z, h = me.Heading,
                        v = new Vector2(vel.x, vel.z).magnitude, g = me.Grounded ? 1 : 0, c = me.cpIndex, j = _myJump,
                        pr = _gf.course.Progress(me.transform.position, me.cpIndex)
                    });
                }
            }
            // smooth remote players
            foreach (var kv in _remotes)
            {
                var r = kv.Value; if (r.player == null) continue;
                Vector3 predicted = r.target + r.vel * Mathf.Min(0.3f, Time.time - r.tTime);
                Vector3 cur = r.player.transform.position;
                if ((predicted - cur).sqrMagnitude > 14f * 14f) cur = predicted;
                else cur = Vector3.Lerp(cur, predicted, 1f - Mathf.Exp(-14f * Time.deltaTime));
                r.player.transform.position = cur;
                r.player.SetHeading(Mathf.LerpAngle(r.player.Heading, r.h, 1f - Mathf.Exp(-14f * Time.deltaTime)));
            }
        }

        private void OnSnap(Msg m)
        {
            if (m.next >= 0f) _nextLabel = m.next; else _nextLabel = -1f;
            if (mode != Mode.Race || m.p == null) return;
            foreach (var s in m.p)
            {
                if (s.id == _myId || !_remotes.TryGetValue(s.id, out var r) || r.player == null) continue;
                Vector3 np = new Vector3(s.x, s.y, s.z);
                float dt = Mathf.Max(0.02f, Time.time - r.tTime);
                if (r.tTime > 0f && Time.time - r.tTime < 0.5f) r.vel = Vector3.ClampMagnitude((np - r.target) / dt, 30f); else r.vel = Vector3.zero;
                r.target = np; r.tTime = Time.time; r.h = s.h;
                r.player.RemoteSpeed = s.v; r.player.SetRemoteGrounded(s.g == 1);
                r.player.cpIndex = s.c; r.player.netProgress = s.pr;
                if (s.j != r.lastJ) { r.lastJ = s.j; r.player.RemoteSquashJump(); Sfx.PlayAt("jump", np, 0.5f); }
            }
        }

        private void OnFinished(Msg m)
        {
            if (m.id != _myId && _remotes.TryGetValue(m.id, out var r) && r.player != null) { r.player.finished = true; r.player.finishTime = m.time; }
            _finishLines.Add(m.name + "  " + UIKit.TimeString(m.time));
            if (m.first)
            {
                Sfx.Play("finish", 0.7f);
                if (m.id != _myId) _gf.Toast(m.name.ToUpperInvariant() + " WINS!", new Color(1f, 0.9f, 0.3f), 3f);
            }
            else if (m.id != _myId) _gf.Toast(m.name + " finished", Color.white, 1.8f);
            if (mode == Mode.Race && _gf.state == GameFlow.St.Finished) ShowProvisional();
        }

        private void OnMeFinished(float time)
        {
            Send(new FinishOut { time = time });
            Sfx.Play("win");
            Fx.Confetti(_gf.course.FinishPos);
            _gf.Toast("YOU FINISHED!", new Color(0.55f, 1f, 0.6f), 3f);
            _finishLines.Add(_gf.me.pname + "  " + UIKit.TimeString(time));
            StartCoroutine(ShowProvisionalLater());
        }

        private System.Collections.IEnumerator ShowProvisionalLater() { yield return new WaitForSeconds(1.6f); if (mode == Mode.Race) ShowProvisional(); }

        private void ShowProvisional()
        {
            _resP.SetActive(true);
            _resTitle.text = "FINISHED!";
            _resTitle.color = new Color(0.6f, 1f, 0.65f);
            _resBody.text = "Waiting for the others...\n\n" + string.Join("\n", _finishLines);
        }

        private void OnResults(Msg m)
        {
            if (mode != Mode.Race && mode != Mode.Lobby) return;
            mode = Mode.Results;
            _leaveP.SetActive(false);
            bool iWon = m.winner == _myId && m.winner != 0;
            string winnerName = "";
            var sb = new StringBuilder();
            int pos = 1;
            foreach (var r in m.list)
            {
                if (r.id == m.winner) winnerName = r.name;
                sb.Append(UIKit.Ordinal(pos++)).Append("  ").Append(r.id == _myId ? "<b>" + r.name + "</b>" : r.name).Append("   ")
                  .Append(r.time >= 0f ? UIKit.TimeString(r.time) : "did not finish").Append("\n");
            }
            _resTitle.text = iWon ? "YOU WIN!" : (string.IsNullOrEmpty(winnerName) ? "NO WINNER" : winnerName.ToUpperInvariant() + " WINS!");
            _resTitle.color = iWon ? new Color(1f, 0.9f, 0.3f) : Color.white;
            _resBody.text = sb.ToString();
            _resP.SetActive(true);
            if (iWon) Sfx.Play("win");
        }
    }
}
