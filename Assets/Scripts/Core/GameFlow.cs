using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SkyHop
{
    public class GameFlow : MonoBehaviour
    {
        public enum St { Menu, Countdown, Racing, Finished }
        public static GameFlow I;

        public static readonly Color[] Colors =
        {
            new Color(1f, 0.35f, 0.4f), new Color(1f, 0.62f, 0.2f), new Color(1f, 0.88f, 0.25f), new Color(0.45f, 0.85f, 0.4f),
            new Color(0.3f, 0.8f, 0.95f), new Color(0.4f, 0.5f, 1f), new Color(0.75f, 0.45f, 1f), new Color(1f, 0.5f, 0.8f)
        };
        private static readonly string[] BotNames = { "Pip", "Zoey", "Bumble", "Waffle", "Nimbus", "Taffy", "Sprout" };

        public St state = St.Menu;
        public CameraRig cam;
        public Course course;
        public Player me;
        public LocalController lc;
        public readonly List<Player> racers = new List<Player>();
        private readonly List<Player> _finishOrder = new List<Player>();

        private Canvas _canvas;
        private GameObject _menuG, _hudG, _resG, _pauseG, _touchG;
        private TMP_Text _timerT, _cpT, _rankT, _toastT, _countT, _resTitle, _resBody, _bestT, _soundT;
        private TMP_InputField _nameIn;
        private Image[] _swatches;
        private Image _fade;
        private float _raceT, _toast, _countdown;
        private int _myColor;
        private string _myName;
        private bool _paused;

        public static void Create()
        {
            var go = new GameObject("GameFlow");
            go.AddComponent<GameFlow>();
        }

        private void Awake()
        {
            I = this;
            Setup();
        }

        // ---------------------------------------------------------------- world
        private void Setup()
        {
            _myName = PlayerPrefs.GetString("hop_name", "");
            _myColor = Mathf.Clamp(PlayerPrefs.GetInt("hop_color", 4), 0, Colors.Length - 1);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.88f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.8f, 0.82f, 0.9f);
            RenderSettings.ambientGroundColor = new Color(0.55f, 0.5f, 0.62f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.62f, 0.84f, 1f);
            RenderSettings.fogStartDistance = 90f; RenderSettings.fogEndDistance = 320f;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.shadows = LightShadows.None;
            sun.color = new Color(1f, 0.96f, 0.88f); sun.intensity = 1.15f;
            sun.transform.rotation = Quaternion.Euler(52f, -32f, 0f);

            course = Course.Build();
            cam = CameraRig.Create();
            cam.cam.backgroundColor = RenderSettings.fogColor;

            me = Player.Create(string.IsNullOrEmpty(_myName) ? "You" : _myName, Colors[_myColor], course.StartSlots[2]);
            me.OnFell += OnFell;
            lc = gameObject.AddComponent<LocalController>(); lc.player = me;
            racers.Add(me);
            cam.Snap(me); cam.MenuMode = true;

            BuildUI();
            ShowMenu();
        }

        // ---------------------------------------------------------------- flow
        private void ShowMenu()
        {
            ClearBots();
            state = St.Menu; _paused = false; Time.timeScale = 1f;
            _menuG.SetActive(true); _hudG.SetActive(false); _resG.SetActive(false); _pauseG.SetActive(false); _touchG.SetActive(false);
            me.finished = false; me.cpIndex = 0; me.controlsEnabled = false;
            me.Teleport(course.StartSlots[2], 0f);
            cam.target = me; cam.MenuMode = true;
            float best = PlayerPrefs.GetFloat("hop_best", 0f);
            _bestT.text = best > 0f ? "Best time: " + UIKit.TimeString(best) : "Race 3 bots to the flag!";
            Sfx.Music(true);
        }

        private void ClearBots()
        {
            for (int i = racers.Count - 1; i >= 0; i--)
                if (racers[i] != me) { if (racers[i] != null) Destroy(racers[i].gameObject); racers.RemoveAt(i); }
        }

        private void StartSolo()
        {
            CommitName();
            ClearBots();
            _finishOrder.Clear();
            var slots = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
            int mySlot = Random.Range(0, 8);
            slots.Remove(mySlot);
            me.finished = false; me.cpIndex = 0;
            me.Teleport(course.StartSlots[mySlot], 0f);
            var names = new List<string>(BotNames);
            for (int i = 0; i < 3; i++)
            {
                int sl = slots[Random.Range(0, slots.Count)]; slots.Remove(sl);
                string nm = names[Random.Range(0, names.Count)]; names.Remove(nm);
                var bot = Player.Create(nm, Colors[(_myColor + 2 + i * 2) % Colors.Length], course.StartSlots[sl]);
                bot.OnFell += OnFell;
                var bc = bot.gameObject.AddComponent<BotController>(); bc.player = bot; bc.skill = Random.Range(0.8f, 1f);
                racers.Add(bot);
            }
            BeginCountdown();
        }

        private void BeginCountdown()
        {
            state = St.Countdown; _countdown = 3.99f; _raceT = 0f;
            foreach (var r in racers) { r.controlsEnabled = false; r.finished = false; r.cpIndex = 0; }
            _menuG.SetActive(false); _resG.SetActive(false); _hudG.SetActive(true); _touchG.SetActive(TouchWanted());
            cam.MenuMode = false; cam.Snap(me);
            _timerT.text = UIKit.TimeString(0f);
            _lastBeep = 4;
        }

        private int _lastBeep;
        private void StartRace()
        {
            state = St.Racing;
            foreach (var r in racers) r.controlsEnabled = true;
            Sfx.Play("go");
            _countT.text = "GO!"; _countT.color = new Color(1f, 0.95f, 0.3f);
            StartCoroutine(HideCount());
        }

        private IEnumerator HideCount() { yield return new WaitForSeconds(0.9f); if (state == St.Racing) _countT.text = ""; }

        private void Finish(Player p)
        {
            p.finished = true; p.finishTime = _raceT; _finishOrder.Add(p);
            p.controlsEnabled = false;
            Sfx.PlayAt("finish", p.transform.position, 0.6f);
            if (p == me) OnMeFinished();
        }

        private void OnMeFinished()
        {
            state = St.Finished;
            int place = _finishOrder.IndexOf(me) + 1;
            Sfx.Play("win");
            Fx.Confetti(course.FinishPos);
            float best = PlayerPrefs.GetFloat("hop_best", 0f);
            bool newBest = best <= 0f || me.finishTime < best;
            if (newBest) { PlayerPrefs.SetFloat("hop_best", me.finishTime); PlayerPrefs.Save(); }
            _resTitle.text = place == 1 ? "YOU WIN!" : UIKit.Ordinal(place) + " PLACE";
            _resTitle.color = place == 1 ? new Color(1f, 0.9f, 0.3f) : Color.white;
            var sb = new StringBuilder();
            sb.Append("Your time  <b>").Append(UIKit.TimeString(me.finishTime)).Append("</b>");
            if (newBest) sb.Append("   <color=#7CFF8A>NEW BEST!</color>");
            sb.Append("\n\n");
            var order = new List<Player>(racers);
            order.Sort((a, b) => Rank(b).CompareTo(Rank(a)));
            int pos = 1;
            foreach (var r in order)
            {
                sb.Append(UIKit.Ordinal(pos++)).Append("  ").Append(r.pname == me.pname && r == me ? "<b>" + r.pname + "</b>" : r.pname);
                sb.Append("   ").Append(r.finished ? UIKit.TimeString(r.finishTime) : "racing...").Append("\n");
            }
            _resBody.text = sb.ToString();
            _resG.SetActive(true);
        }

        private float Rank(Player p)
        {
            if (p.finished) return 1e6f - p.finishTime;
            return course.Progress(p.transform.position, p.cpIndex);
        }

        private void OnFell(Player p) { StartCoroutine(Respawn(p)); }

        private IEnumerator Respawn(Player p)
        {
            if (p == me) { Sfx.Play("fall"); _toast = 1.2f; _toastT.text = "OOPS!"; _toastT.color = new Color(1f, 0.55f, 0.5f); }
            yield return new WaitForSeconds(0.55f);
            if (p == null) yield break;
            p.Teleport(course.RespawnPos(p.cpIndex), 0f);
            var bc = p.GetComponent<BotController>();
            if (bc != null) bc.ResetTo(p.cpIndex);
        }

        // ---------------------------------------------------------------- update
        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && (state == St.Racing || state == St.Countdown)) TogglePause();
            if (kb != null && kb.mKey.wasPressedThisFrame) ToggleSound();

            if (_toast > 0f) { _toast -= Time.unscaledDeltaTime; if (_toast <= 0f) _toastT.text = ""; }

            switch (state)
            {
                case St.Countdown:
                    _countdown -= Time.deltaTime;
                    int n = Mathf.CeilToInt(_countdown);
                    if (n != _lastBeep && n >= 1) { _lastBeep = n; Sfx.Play("beep"); _countT.text = n.ToString(); _countT.color = Color.white; }
                    if (_countdown <= 0f) StartRace();
                    break;
                case St.Racing:
                case St.Finished:
                    if (state == St.Racing) { _raceT += Time.deltaTime; _timerT.text = UIKit.TimeString(_raceT); }
                    foreach (var r in racers)
                    {
                        if (r == null || r.finished || !r.Alive) continue;
                        if (course.TryCheckpoint(r) && r == me)
                        {
                            Sfx.Play("checkpoint");
                            _toast = 1.6f; _toastT.text = "CHECKPOINT " + r.cpIndex + "/" + (course.Checkpoints.Count - 1); _toastT.color = new Color(0.55f, 1f, 0.6f);
                        }
                        if (course.InFinish(r)) Finish(r);
                    }
                    UpdateRankLabel();
                    break;
            }
            if (state == St.Menu && _nameIn != null) { /* nothing */ }
        }

        private void UpdateRankLabel()
        {
            int rank = 1;
            float mine = Rank(me);
            foreach (var r in racers) if (r != me && Rank(r) > mine) rank++;
            _rankT.text = UIKit.Ordinal(rank) + " <size=60%>/ " + racers.Count + "</size>";
            _cpT.text = "CHECKPOINT " + me.cpIndex + "/" + (course.Checkpoints.Count - 1);
        }

        // ---------------------------------------------------------------- UI
        private bool TouchWanted() { return Application.isMobilePlatform || (Touchscreen.current != null && Keyboard.current == null); }

        private void CommitName()
        {
            string n = _nameIn != null ? _nameIn.text.Trim() : "";
            if (n.Length > 0) { _myName = n; PlayerPrefs.SetString("hop_name", n); me.pname = n; }
            PlayerPrefs.Save();
        }

        private void ToggleSound()
        {
            Sfx.Muted = !Sfx.Muted;
            if (!Sfx.Muted) Sfx.Music(state == St.Menu || true);
            _soundT.text = Sfx.Muted ? "SOUND: OFF" : "SOUND: ON";
        }

        private void TogglePause()
        {
            _paused = !_paused;
            _pauseG.SetActive(_paused);
            Time.timeScale = _paused ? 0f : 1f;
        }

        private void BuildUI()
        {
            _canvas = UIKit.MakeCanvas("UI", 10, transform);
            Transform ui = _canvas.transform;
            Color panel = new Color(0.12f, 0.16f, 0.35f, 0.82f);

            // ---- menu
            _menuG = UIKit.Group(ui, "Menu");
            var title = UIKit.Label(_menuG.transform, "SKY HOP PARTY", 92f, UIKit.T, new Vector2(0f, -40f), new Vector2(1100f, 120f), TextAlignmentOptions.Center, Color.white);
            title.fontStyle = FontStyles.Bold; title.outlineWidth = 0.22f; title.outlineColor = new Color32(35, 60, 140, 255);
            var sub = UIKit.Label(_menuG.transform, "Race over the floating islands. First to the flag wins!", 32f, UIKit.T, new Vector2(0f, -150f), new Vector2(1000f, 50f), TextAlignmentOptions.Center, Color.white); sub.outlineWidth = 0.2f; sub.outlineColor = new Color32(35, 60, 140, 255);
            _nameIn = UIKit.InputBox(_menuG.transform, "Your name", UIKit.B, new Vector2(0f, 255f), new Vector2(330f, 54f), 12);
            _nameIn.text = _myName;
            // swatches
            _swatches = new Image[Colors.Length];
            for (int i = 0; i < Colors.Length; i++)
            {
                int idx = i;
                var sw = UIKit.Box(_menuG.transform, "Color" + i, UIKit.B, new Vector2((i - 3.5f) * 56f, 190f), new Vector2(46f, 46f), Colors[i]);
                var b = sw.gameObject.AddComponent<Button>(); b.targetGraphic = sw;
                b.onClick.AddListener(() => PickColor(idx));
                _swatches[i] = sw;
            }
            PickColor(_myColor, false);
            UIKit.Btn(_menuG.transform, "PLAY", UIKit.B, new Vector2(-140f, 105f), new Vector2(250f, 70f), new Color(0.3f, 0.75f, 0.35f), () => { Sfx.Play("click"); StartSolo(); }, 38f);
            var online = UIKit.Btn(_menuG.transform, "ONLINE", UIKit.B, new Vector2(140f, 105f), new Vector2(250f, 70f), new Color(0.35f, 0.5f, 0.95f), () => { Sfx.Play("click"); OnlineClicked(); }, 38f);
            _bestT = UIKit.Label(_menuG.transform, "", 28f, UIKit.B, new Vector2(0f, 48f), new Vector2(700f, 40f), TextAlignmentOptions.Center, new Color(1f, 0.95f, 0.6f));
            UIKit.Label(_menuG.transform, "Move: WASD / arrows    Jump: Space (twice for a double jump)    Look: right-drag or Q / E", 22f, UIKit.B, new Vector2(0f, 12f), new Vector2(1200f, 34f), TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.8f));
            var snd = UIKit.Btn(_menuG.transform, Sfx.Muted ? "SOUND: OFF" : "SOUND: ON", UIKit.TR, new Vector2(-20f, -20f), new Vector2(210f, 46f), new Color(0.2f, 0.25f, 0.5f, 0.85f), ToggleSound, 22f);
            _soundT = snd.GetComponentInChildren<TMP_Text>();

            // ---- hud
            _hudG = UIKit.Group(ui, "HUD");
            _timerT = UIKit.Label(_hudG.transform, "0:00.00", 56f, UIKit.T, new Vector2(0f, -20f), new Vector2(400f, 70f), TextAlignmentOptions.Center, Color.white);
            _timerT.fontStyle = FontStyles.Bold;
            _cpT = UIKit.Label(_hudG.transform, "", 26f, UIKit.TL, new Vector2(24f, -26f), new Vector2(380f, 40f), TextAlignmentOptions.Left, new Color(1f, 1f, 1f, 0.9f));
            _rankT = UIKit.Label(_hudG.transform, "", 64f, UIKit.TR, new Vector2(-28f, -14f), new Vector2(300f, 80f), TextAlignmentOptions.Right, new Color(1f, 0.92f, 0.4f));
            _rankT.fontStyle = FontStyles.Bold;
            _toastT = UIKit.Label(_hudG.transform, "", 46f, UIKit.C, new Vector2(0f, 150f), new Vector2(900f, 70f), TextAlignmentOptions.Center, Color.white);
            _countT = UIKit.Label(_hudG.transform, "", 150f, UIKit.C, new Vector2(0f, 40f), new Vector2(600f, 200f), TextAlignmentOptions.Center, Color.white);
            _countT.fontStyle = FontStyles.Bold;
            UIKit.Btn(_hudG.transform, "II", UIKit.TL, new Vector2(24f, -76f), new Vector2(60f, 46f), new Color(0.2f, 0.25f, 0.5f, 0.7f), () => { Sfx.Play("click"); TogglePause(); }, 26f);

            // ---- touch controls
            _touchG = UIKit.Group(ui, "Touch");
            var stickBg = UIKit.Box(_touchG.transform, "Stick", UIKit.BL, new Vector2(170f, 170f), new Vector2(240f, 240f), new Color(1f, 1f, 1f, 0.18f), true);
            stickBg.sprite = UIKit.CircleSprite(); stickBg.type = Image.Type.Simple;
            var knob = UIKit.Box(stickBg.transform, "Knob", UIKit.C, Vector2.zero, new Vector2(100f, 100f), new Color(1f, 1f, 1f, 0.45f), true);
            knob.sprite = UIKit.CircleSprite(); knob.type = Image.Type.Simple; knob.raycastTarget = false;
            var ts = stickBg.gameObject.AddComponent<TouchStick>(); ts.knob = (RectTransform)knob.transform; ts.radius = 90f;
            var jb = UIKit.Box(_touchG.transform, "JumpBtn", UIKit.BR, new Vector2(-150f, 150f), new Vector2(170f, 170f), new Color(1f, 1f, 1f, 0.28f), true);
            jb.sprite = UIKit.CircleSprite(); jb.type = Image.Type.Simple;
            UIKit.Label(jb.transform, "JUMP", 34f, UIKit.C, Vector2.zero, new Vector2(170f, 60f));
            jb.gameObject.AddComponent<TouchJump>();

            // ---- results
            _resG = UIKit.Group(ui, "Results");
            UIKit.Panel(_resG.transform, "Dim", new Color(0f, 0f, 0f, 0.45f));
            UIKit.Box(_resG.transform, "Card", UIKit.C, Vector2.zero, new Vector2(700f, 560f), panel);
            _resTitle = UIKit.Label(_resG.transform, "", 78f, UIKit.C, new Vector2(0f, 205f), new Vector2(660f, 100f));
            _resTitle.fontStyle = FontStyles.Bold;
            _resBody = UIKit.Label(_resG.transform, "", 30f, UIKit.C, new Vector2(0f, 20f), new Vector2(620f, 300f), TextAlignmentOptions.Center);
            UIKit.Btn(_resG.transform, "PLAY AGAIN", UIKit.C, new Vector2(-160f, -225f), new Vector2(290f, 66f), new Color(0.3f, 0.75f, 0.35f), () => { Sfx.Play("click"); StartSolo(); }, 30f);
            UIKit.Btn(_resG.transform, "MENU", UIKit.C, new Vector2(160f, -225f), new Vector2(290f, 66f), new Color(0.35f, 0.5f, 0.95f), () => { Sfx.Play("click"); ShowMenu(); }, 30f);

            // ---- pause
            _pauseG = UIKit.Group(ui, "Pause");
            UIKit.Panel(_pauseG.transform, "Dim", new Color(0f, 0f, 0f, 0.5f));
            UIKit.Label(_pauseG.transform, "PAUSED", 80f, UIKit.C, new Vector2(0f, 130f), new Vector2(600f, 100f));
            UIKit.Btn(_pauseG.transform, "RESUME", UIKit.C, new Vector2(0f, 20f), new Vector2(320f, 66f), new Color(0.3f, 0.75f, 0.35f), () => { Sfx.Play("click"); TogglePause(); }, 32f);
            UIKit.Btn(_pauseG.transform, "QUIT TO MENU", UIKit.C, new Vector2(0f, -70f), new Vector2(320f, 66f), new Color(0.85f, 0.4f, 0.4f), () => { Sfx.Play("click"); ShowMenu(); }, 30f);

            _fade = UIKit.Panel(ui, "Fade", new Color(0f, 0f, 0f, 0f)); _fade.raycastTarget = false;
        }

        private void PickColor(int i, bool play = true)
        {
            _myColor = i;
            PlayerPrefs.SetInt("hop_color", i);
            for (int k = 0; k < _swatches.Length; k++) _swatches[k].transform.localScale = Vector3.one * (k == i ? 1.25f : 1f);
            if (me != null) me.SetColor(Colors[i]);
            if (play) Sfx.Play("click");
        }

        // hook for the online mode (added in the next step)
        public System.Action OnlineHook;
        private void OnlineClicked()
        {
            if (OnlineHook != null) OnlineHook();
            else { _bestT.text = "Online races are coming soon!"; }
        }
    }

    public class TouchStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform knob;
        public float radius = 90f;
        public void OnPointerDown(PointerEventData e) { OnDrag(e); }
        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out Vector2 lp);
            Vector2 v = Vector2.ClampMagnitude(lp, radius);
            knob.anchoredPosition = v;
            HopInput.TouchMove = v / radius;
        }
        public void OnPointerUp(PointerEventData e) { knob.anchoredPosition = Vector2.zero; HopInput.TouchMove = Vector2.zero; }
    }

    public class TouchJump : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public void OnPointerDown(PointerEventData e) { HopInput.PressTouchJump(); }
        public void OnPointerUp(PointerEventData e) { HopInput.ReleaseTouchJump(); }
    }
}
