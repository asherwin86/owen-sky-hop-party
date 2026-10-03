using System.Collections.Generic;
using UnityEngine;

namespace SkyHop
{
    /// <summary>Course 1-3: built from reusable sections. Course 0 (Sunny Steps) lives in Course.Make().</summary>
    public partial class Course
    {
        public const int Count = 4;
        public static readonly string[] Names = { "Sunny Steps", "Candy Canyon", "Frosty Peaks", "Twilight Spire" };
        public static readonly string[] Blurbs =
        {
            "A friendly warm-up over the clouds.",
            "Long leaps, spinning discs and a sweeper.",
            "Slippery tiles, wobbly bridges, a spinning cross.",
            "The long one. Pads, discs, bridges, everything.",
        };

        public int Id;
        public string Name = "";
        public Color CloudCol = Color.white;
        private float _e, _y;   // z of the end of the last element, and the height we are at

        private static Color[] Palette(int id)
        {
            switch (id)
            {
                case 1: return new[] {
                    new Color(1f, 0.62f, 0.8f), new Color(0.75f, 0.6f, 1f), new Color(0.6f, 0.95f, 0.85f), new Color(1f, 0.85f, 0.5f),
                    new Color(1f, 0.55f, 0.65f), new Color(0.6f, 0.78f, 1f), new Color(0.95f, 0.7f, 1f), new Color(0.7f, 1f, 0.7f) };
                case 2: return new[] {
                    new Color(0.7f, 0.9f, 1f), new Color(0.85f, 0.95f, 1f), new Color(0.55f, 0.8f, 0.95f), new Color(0.8f, 0.85f, 1f),
                    new Color(0.65f, 1f, 0.95f), new Color(0.95f, 0.98f, 1f), new Color(0.6f, 0.75f, 1f), new Color(0.75f, 0.95f, 0.9f) };
                case 3: return new[] {
                    new Color(0.65f, 0.5f, 1f), new Color(0.35f, 0.85f, 0.85f), new Color(1f, 0.6f, 0.4f), new Color(0.85f, 0.45f, 0.9f),
                    new Color(0.45f, 0.65f, 1f), new Color(1f, 0.75f, 0.35f), new Color(0.5f, 0.9f, 0.7f), new Color(1f, 0.5f, 0.6f) };
                default: return DefaultPal;
            }
        }

        private void ApplyTheme()
        {
            Color sky, eq, ground, fog;
            switch (Id)
            {
                case 1: fog = new Color(1f, 0.78f, 0.9f); sky = new Color(1f, 0.85f, 0.95f); eq = new Color(0.95f, 0.8f, 0.9f); ground = new Color(0.6f, 0.45f, 0.6f); CloudCol = new Color(1f, 0.95f, 1f); break;
                case 2: fog = new Color(0.8f, 0.93f, 1f); sky = new Color(0.85f, 0.95f, 1f); eq = new Color(0.8f, 0.88f, 0.95f); ground = new Color(0.55f, 0.6f, 0.7f); CloudCol = Color.white; break;
                case 3: fog = new Color(0.3f, 0.22f, 0.55f); sky = new Color(0.5f, 0.45f, 0.8f); eq = new Color(0.45f, 0.4f, 0.65f); ground = new Color(0.28f, 0.22f, 0.42f); CloudCol = new Color(0.72f, 0.62f, 0.95f); break;
                default: fog = new Color(0.62f, 0.84f, 1f); sky = new Color(0.78f, 0.88f, 1f); eq = new Color(0.8f, 0.82f, 0.9f); ground = new Color(0.55f, 0.5f, 0.62f); CloudCol = Color.white; break;
            }
            RenderSettings.fogColor = fog;
            RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = eq; RenderSettings.ambientGroundColor = ground;
        }

        private void MakeCourse(int id)
        {
            SecStart();
            switch (id)
            {
                case 1: // Candy Canyon
                    SecStones(7, 2.4f, 4.2f, 1.4f, 2.4f, 0.2f);
                    SecRest(2.8f, 5f);
                    SecPad();
                    SecDiscs(3, 4f, 2.4f, 2f, 55f);
                    SecRest(2.6f, 5f);
                    SecBridge(24f, 1.8f, 0f, 0f);
                    SecRest(0f, 5f);
                    SecSliders(5, 4.5f, 5.5f, 1.2f, 2.6f, 3.4f);
                    SecRest(3f, 5.5f);
                    SecSweeper(10f, 100f, false);
                    SecClimb(8, 1.6f, 4.5f, 2.5f, 3.0f, new[] { 2, 5 });
                    break;
                case 2: // Frosty Peaks
                    SecTiles(10, 2.0f, 3.3f, 1.5f, 1.8f, 0.35f);
                    SecRest(1.5f, 5f);
                    SecBridge(20f, 2.6f, 14f, 2.0f);
                    SecRest(0f, 4f);
                    SecBridge(20f, 2.6f, 14f, 2.4f);
                    SecRest(0f, 5f);
                    SecPad();
                    SecStones(8, 2.2f, 4.4f, 1.5f, 1.6f, 0.5f);
                    SecRest(2.4f, 5f);
                    SecSweeper(10f, 65f, true);
                    SecTiles(6, 2.0f, 3.3f, 1.5f, 1.8f, 0.6f);
                    SecRest(1.4f, 5f);
                    SecClimb(7, 1.8f, 5f, 2.4f, 3.2f, new[] { 2, 5 });
                    break;
                default: // Twilight Spire
                    SecStones(10, 2.6f, 4.2f, 1.5f, 2.0f, 0.8f);
                    SecRest(2.4f, 5f);
                    SecPad();
                    SecPad();
                    SecDiscs(4, 3.6f, 2.2f, 2.4f, 75f);
                    SecRest(2.4f, 5f);
                    SecSliders(4, 4.4f, 5.0f, 1.3f, 2.2f, 3.4f);
                    SecRest(3f, 5.5f);
                    SecBridge(26f, 2.4f, 16f, 2.2f);
                    SecRest(0f, 5f);
                    SecTiles(8, 1.6f, 3.2f, 1.7f, 1.8f, 0f);
                    SecRest(1.4f, 5f);
                    SecSweeper(11f, 75f, true);
                    SecClimb(10, 1.8f, 4.8f, 2.4f, 3.2f, new[] { 3, 5, 7 });
                    break;
            }
            SecFinish(9.5f, 1.9f);
            Decor();
        }

        // ---------------------------------------------------------------- sections
        private void SecStart()
        {
            Island(Vector3.zero, 11f, NextCol());
            for (int i = 0; i < 8; i++) StartSlots[i] = new Vector3(-4.5f + (i % 4) * 3f, 0.05f, -5f + (i / 4) * 3f);
            AddCheckpoint(Vector3.zero, 12f);
            StartArch(new Vector3(0f, 0f, 8.5f), "START");
            Wp("start", null, new Vector3(0f, 0f, 9f));
            _e = 11f; _y = 0f;
        }

        private void SecStones(int n, float gap0, float dz, float rad, float xoff, float rise)
        {
            float cz = _e + gap0 + rad;
            for (int i = 0; i < n; i++)
            {
                _y += rise;
                float x = (i % 2 == 0 ? -xoff : xoff);
                Pad(new Vector3(x, _y, cz), rad, Pal[(_col + i) % Pal.Length]);
                Wp("stone" + i, null, new Vector3(x, _y, cz));
                _e = cz + rad;
                cz += dz;
            }
            _col += n;
        }

        private void SecRest(float gap, float r)
        {
            float cz = _e + gap + r;
            Island(new Vector3(0f, _y, cz), r, NextCol());
            Wp("rest", null, new Vector3(0f, _y, cz));
            AddCheckpoint(new Vector3(0f, _y, cz), r + 0.2f);
            _e = cz + r;
        }

        private void SecPad()
        {
            var padTop = new Vector3(0f, _y, _e - 1.2f);
            var padGo = new GameObject("JumpPad"); padGo.transform.SetParent(_root, false); padGo.transform.position = padTop;
            Cyl(padGo.transform, new Vector3(0f, 0.1f, 0f), 2.6f, 0.2f, Mats.Unlit(new Color(1f, 0.45f, 0.15f)), false);
            Cyl(padGo.transform, new Vector3(0f, 0.22f, 0f), 1.7f, 0.1f, Mats.Unlit(new Color(1f, 0.9f, 0.2f)), false);
            var jp = padGo.AddComponent<JumpPad>(); jp.launch = new Vector3(0f, 20.5f, 11f); jp.radius = 1.3f;
            Wp("pad", padGo.transform, padTop);
            _y += 5.5f;
            float cz = _e + 15.5f;
            Island(new Vector3(0f, _y, cz), 6f, NextCol());
            Wp("land", null, new Vector3(0f, _y, cz));
            AddCheckpoint(new Vector3(0f, _y, cz), 6.2f);
            _e = cz + 6f;
        }

        private void SecBridge(float len, float width, float wobbleDeg, float wobbleSpeed)
        {
            var bridge = Slab(new Vector3(0f, _y, _e + len * 0.5f), new Vector3(width, 0.5f, len), Pal[(_col++) % Pal.Length]);
            if (wobbleDeg > 0f)
            {
                var rt = bridge.AddComponent<RotatingThing>();
                rt.wobbleDeg = wobbleDeg; rt.localAxis = Vector3.forward; rt.wobbleSpeed = wobbleSpeed;
            }
            Wp("bridge1", bridge.transform, new Vector3(0f, _y, _e + 4f));
            Wp("bridge2", bridge.transform, new Vector3(0f, _y, _e + len - 4f));
            _e += len;
        }

        private void SecSliders(int n, float size, float amp, float speed, float gap0, float gap)
        {
            float cz = _e + gap0 + size * 0.5f;
            for (int i = 0; i < n; i++)
            {
                var s = Slab(new Vector3(0f, _y, cz), new Vector3(size, 0.6f, size), Pal[(_col++) % Pal.Length]);
                var sp = s.AddComponent<SlidingPlatform>(); sp.axis = Vector3.right; sp.amplitude = amp; sp.speed = speed; sp.phase = i * 1.4f;
                Wp("slide" + i, s.transform, new Vector3(0f, _y, cz));
                _e = cz + size * 0.5f;
                cz += size + gap;
            }
        }

        private void SecDiscs(int n, float r, float gap, float xoff, float deg)
        {
            float cz = _e + gap + r;
            for (int i = 0; i < n; i++)
            {
                float x = (i % 2 == 0 ? -xoff : xoff);
                var g = Island(new Vector3(x, _y, cz), r, Pal[(_col++) % Pal.Length]);
                var rt = g.AddComponent<RotatingThing>(); rt.localAxis = Vector3.up; rt.degPerSec = deg * (i % 2 == 0 ? 1f : -1f);
                Mats.Prim(PrimitiveType.Cube, g.transform, new Vector3(0f, 0.07f, 0f), new Vector3(r * 1.9f, 0.1f, 0.5f), Mats.Lit(Color.white, 0.3f));
                Mats.Prim(PrimitiveType.Cube, g.transform, new Vector3(0f, 0.07f, 0f), new Vector3(0.5f, 0.1f, r * 1.9f), Mats.Lit(Color.white, 0.3f));
                Wp("disc" + i, g.transform, new Vector3(x, _y, cz));
                _e = cz + r;
                cz += 2f * r + gap;
            }
        }

        private void SecSweeper(float r, float deg, bool cross)
        {
            float cz = _e + 3f + r;
            Island(new Vector3(0f, _y, cz - 0.01f), r, NextCol());
            AddCheckpoint(new Vector3(0f, _y, cz - (r - 2f)), 2.6f);
            var hub = new GameObject("Sweeper"); hub.transform.SetParent(_root, false);
            hub.transform.position = new Vector3(0f, _y + 0.35f, cz);
            float half = r - 3f;
            int arms = cross ? 2 : 1;
            for (int a = 0; a < arms; a++)
            {
                var arm = new GameObject("Arm" + a); arm.transform.SetParent(hub.transform, false);
                arm.transform.localRotation = Quaternion.Euler(0f, a * 90f, 0f);
                Mats.Prim(PrimitiveType.Cube, arm.transform, Vector3.zero, new Vector3(half * 2f, 0.55f, 0.7f), Mats.Lit(new Color(0.95f, 0.3f, 0.3f), 0.3f));
                Mats.Prim(PrimitiveType.Cube, arm.transform, new Vector3(half - 0.8f, 0f, 0f), new Vector3(1.5f, 0.6f, 0.75f), Mats.Lit(Color.white));
                Mats.Prim(PrimitiveType.Cube, arm.transform, new Vector3(-half + 0.8f, 0f, 0f), new Vector3(1.5f, 0.6f, 0.75f), Mats.Lit(Color.white));
                var sw = arm.AddComponent<SweeperBar>(); sw.halfLength = half; sw.halfThick = 0.35f; sw.height = 0.55f; sw.power = 13f;
            }
            Cyl(_root, new Vector3(0f, _y + 0.2f, cz), 1.4f, 0.4f, Mats.Lit(new Color(0.35f, 0.3f, 0.45f)), false);
            var rt = hub.AddComponent<RotatingThing>(); rt.localAxis = Vector3.up; rt.degPerSec = deg;
            Wp("sweepIn", null, new Vector3(0f, _y, cz - (r - 2f)));
            Wp("sweepOut", null, new Vector3(0f, _y, cz + (r - 1.5f)));
            _e = cz + r;
            Wp("sweepEdge", null, new Vector3(0f, _y, _e - 0.8f));
        }

        private void SecTiles(int n, float gap0, float size, float gap, float xoff, float rise)
        {
            float tz = _e + gap0 + size * 0.5f;
            for (int i = 0; i < n; i++)
            {
                _y += rise;
                float x = (i % 2 == 0 ? -xoff : xoff) * (i == 0 ? 0f : 1f);
                var t = Slab(new Vector3(x, _y, tz), new Vector3(size, 0.5f, size), Pal[(_col + i) % Pal.Length]);
                t.AddComponent<VanishingTile>();
                Wp("tile" + i, t.transform, new Vector3(x, _y, tz));
                _e = tz + size * 0.5f;
                tz += size + gap;
            }
            _col += n;
        }

        private void SecClimb(int n, float rise, float size, float gap, float xoff, int[] sliders)
        {
            float cz = _e + gap + size * 0.5f;
            for (int i = 0; i < n; i++)
            {
                _y += rise;
                float x = (i % 2 == 0 ? -xoff : xoff) * (i == n - 1 ? 0f : 1f);
                var s = Slab(new Vector3(x, _y, cz), new Vector3(size, 0.7f, size), Pal[(_col + i) % Pal.Length]);
                if (System.Array.IndexOf(sliders, i) >= 0) { var sp = s.AddComponent<SlidingPlatform>(); sp.axis = Vector3.right; sp.amplitude = 3f; sp.speed = 1.3f; sp.phase = i; }
                Wp("climb" + i, s.transform, new Vector3(x, _y, cz));
                if (i == n / 2) AddCheckpoint(new Vector3(x, _y, cz), size * 0.65f);
                _e = cz + size * 0.5f;
                cz += size + gap;
            }
            _col += n;
        }

        private void SecFinish(float r, float gap)
        {
            float cz = _e + gap + r;
            Island(new Vector3(0f, _y, cz), r, Pal[1]);
            FinishPos = new Vector3(0f, _y, cz);
            Wp("finish", null, FinishPos);
            StartArch(new Vector3(0f, _y, cz - 6f), "FINISH");
            Cyl(_root, FinishPos + new Vector3(0f, 3f, 2f), 0.18f, 6f, Mats.Lit(new Color(0.95f, 0.95f, 1f), 0.5f), false);
            var cloth = Mats.Prim(PrimitiveType.Cube, _root, FinishPos + new Vector3(0.9f, 5.2f, 2f), new Vector3(1.8f, 1.1f, 0.05f), Mats.LitTex(Checker(), new Vector2(3f, 2f), Color.white));
            FlagCloth = cloth.transform;
            EndZ = cz;
        }
    }
}
