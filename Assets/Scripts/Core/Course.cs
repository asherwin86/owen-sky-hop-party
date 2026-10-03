using System.Collections.Generic;
using UnityEngine;

namespace SkyHop
{
    /// <summary>Builds the floating-island obstacle course at runtime.</summary>
    public class Course : MonoBehaviour
    {
        public static Course I;
        public const float KillY = -35f;

        public struct Checkpoint { public Vector3 pos; public float radius; public int wpIndex; }

        public readonly List<Transform> Waypoints = new List<Transform>();
        public readonly List<Checkpoint> Checkpoints = new List<Checkpoint>();
        public Vector3[] StartSlots = new Vector3[8];
        public Vector3 FinishPos;
        public float FinishRadius = 8f;
        public Transform FlagCloth;
        public float EndZ;

        private Transform _root;
        private int _col;
        private static Mesh _cone;

        private static readonly Color[] Pal =
        {
            new Color(0.55f, 0.9f, 0.6f), new Color(1f, 0.68f, 0.82f), new Color(1f, 0.9f, 0.5f), new Color(0.76f, 0.66f, 1f),
            new Color(1f, 0.74f, 0.46f), new Color(0.5f, 0.82f, 1f), new Color(0.78f, 0.95f, 0.5f), new Color(1f, 0.62f, 0.58f)
        };

        public static Course Build()
        {
            if (I != null) Destroy(I.gameObject);
            var go = new GameObject("Course");
            var c = go.AddComponent<Course>();
            I = c;
            c._root = go.transform;
            c.Make();
            return c;
        }

        // ---------- geometry helpers ----------
        private Color NextCol() { return Pal[_col++ % Pal.Length]; }
        private static Color Side(Color top) { return Color.Lerp(top, new Color(0.45f, 0.36f, 0.5f), 0.65f); }

        private static Mesh ConeMesh()
        {
            if (_cone != null) return _cone;
            const int n = 10;
            var verts = new List<Vector3>(); var tris = new List<int>();
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                int b = verts.Count;
                verts.Add(Vector3.zero);
                verts.Add(new Vector3(Mathf.Cos(a0), 1f, Mathf.Sin(a0)));
                verts.Add(new Vector3(Mathf.Cos(a1), 1f, Mathf.Sin(a1)));
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            }
            _cone = new Mesh { name = "HopCone" };
            _cone.SetVertices(verts); _cone.SetTriangles(tris, 0);
            _cone.RecalculateNormals(); _cone.RecalculateBounds();
            return _cone;
        }

        private GameObject Cyl(Transform parent, Vector3 localPos, float diameter, float height, Material m, bool collider)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var old = g.GetComponent<Collider>(); Destroy(old);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (collider) { var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = g.GetComponent<MeshFilter>().sharedMesh; mc.convex = true; }
            return g;
        }

        private void RockCone(Transform parent, float topLocalY, float radius, float depth, Color side)
        {
            var g = new GameObject("Rock");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = new Vector3(0f, topLocalY - depth, 0f);
            g.transform.localScale = new Vector3(radius, depth, radius);
            g.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = Mats.Lit(side * 0.85f, 0.1f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private GameObject Island(Vector3 top, float radius, Color col, bool collider = true, float depthMul = 1f)
        {
            var go = new GameObject("Island"); go.transform.SetParent(_root, false); go.transform.position = top;
            const float th = 1.1f;
            Cyl(go.transform, new Vector3(0f, -th * 0.5f, 0f), radius * 2f, th, Mats.Lit(Side(col), 0.1f), collider);
            Cyl(go.transform, new Vector3(0f, -0.05f, 0f), radius * 2f + 0.06f, 0.12f, Mats.Lit(col, 0.2f), false);
            RockCone(go.transform, -th, radius * 0.97f, Mathf.Max(3f, radius * 1.15f * depthMul), Side(col));
            return go;
        }

        private GameObject Pad(Vector3 top, float radius, Color col)
        {
            var go = new GameObject("Stone"); go.transform.SetParent(_root, false); go.transform.position = top;
            const float th = 0.7f;
            Cyl(go.transform, new Vector3(0f, -th * 0.5f, 0f), radius * 2f, th, Mats.Lit(Side(col), 0.1f), true);
            Cyl(go.transform, new Vector3(0f, -0.05f, 0f), radius * 2f + 0.05f, 0.1f, Mats.Lit(col, 0.2f), false);
            RockCone(go.transform, -th, radius * 0.9f, radius * 1.1f, Side(col));
            return go;
        }

        private GameObject Slab(Vector3 top, Vector3 size, Color col)
        {
            var go = new GameObject("Slab"); go.transform.SetParent(_root, false);
            go.transform.position = top - Vector3.up * size.y * 0.5f;
            go.AddComponent<BoxCollider>().size = size;
            Mats.Prim(PrimitiveType.Cube, go.transform, Vector3.zero, size, Mats.Lit(Side(col), 0.1f));
            Mats.Prim(PrimitiveType.Cube, go.transform, new Vector3(0f, size.y * 0.5f - 0.05f, 0f), new Vector3(size.x + 0.04f, 0.12f, size.z + 0.04f), Mats.Lit(col, 0.2f));
            return go;
        }

        private Transform Wp(string name, Transform parent, Vector3 worldPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent != null ? parent : _root, false);
            t.position = worldPos;
            Waypoints.Add(t);
            return t;
        }

        private void AddCheckpoint(Vector3 pos, float radius)
        {
            Checkpoints.Add(new Checkpoint { pos = pos, radius = radius, wpIndex = Waypoints.Count });
            // little flag-less marker ring on the ground
            var ring = Cyl(_root, pos + Vector3.up * 0.04f, 2.2f, 0.06f, Mats.Unlit(new Color(1f, 1f, 1f, 1f)), false);
            ring.name = "CheckpointMarker";
        }

        public int WaypointAfterCheckpoint(int cp) { return Checkpoints[Mathf.Clamp(cp, 0, Checkpoints.Count - 1)].wpIndex; }

        // ---------- the course ----------
        private void Make()
        {
            Color c;
            // --- S0: start island
            c = NextCol();
            var start = Island(Vector3.zero, 11f, c);
            for (int i = 0; i < 8; i++) StartSlots[i] = new Vector3(-4.5f + (i % 4) * 3f, 0.05f, -5f + (i / 4) * 3f);
            AddCheckpoint(new Vector3(0f, 0f, 0f), 12f);
            StartArch(new Vector3(0f, 0f, 8.5f), "START");
            Wp("start", null, new Vector3(0f, 0f, 9f));
            float z = 11f, y = 0f;

            // --- S1: stepping stones
            c = NextCol();
            for (int i = 0; i < 9; i++)
            {
                z += 3.9f; y += 0.35f;
                float x = (i % 2 == 0 ? -2.3f : 2.3f);
                Pad(new Vector3(x, y, z), 1.5f, Pal[(_col + i) % Pal.Length]);
                Wp("stone" + i, null, new Vector3(x, y, z));
            }
            z += 7.2f;
            c = NextCol();
            Island(new Vector3(0f, y, z), 5f, c);
            AddCheckpoint(new Vector3(0f, y, z), 5.2f);
            Wp("rest1", null, new Vector3(0f, y, z));
            float edge = z + 5f;

            // --- S2: jump pad
            var padTop = new Vector3(0f, y, edge - 1.2f);
            var padGo = new GameObject("JumpPad"); padGo.transform.SetParent(_root, false); padGo.transform.position = padTop;
            Cyl(padGo.transform, new Vector3(0f, 0.1f, 0f), 2.6f, 0.2f, Mats.Unlit(new Color(1f, 0.45f, 0.15f)), false);
            Cyl(padGo.transform, new Vector3(0f, 0.22f, 0f), 1.7f, 0.1f, Mats.Unlit(new Color(1f, 0.9f, 0.2f)), false);
            var jp = padGo.AddComponent<JumpPad>(); jp.launch = new Vector3(0f, 20.5f, 11f); jp.radius = 1.3f;
            Wp("pad", padGo.transform, padTop);
            y += 5.5f; z = edge + 15.5f;
            c = NextCol();
            Island(new Vector3(0f, y, z), 6f, c);
            Wp("land2", null, new Vector3(0f, y, z));
            AddCheckpoint(new Vector3(0f, y, z), 6.2f);
            edge = z + 6f;

            // --- S3: wobbling bridge
            const float bridgeLen = 26f;
            var bridge = Slab(new Vector3(0f, y, edge + bridgeLen * 0.5f), new Vector3(3.2f, 0.5f, bridgeLen), Pal[(_col++) % Pal.Length]);
            bridge.AddComponent<RotatingThing>().wobbleDeg = 11f;
            bridge.GetComponent<RotatingThing>().localAxis = Vector3.forward;
            bridge.GetComponent<RotatingThing>().wobbleSpeed = 1.5f;
            Wp("bridge1", bridge.transform, new Vector3(0f, y, edge + 4f));
            Wp("bridge2", bridge.transform, new Vector3(0f, y, edge + bridgeLen - 4f));
            z = edge + bridgeLen + 6f;
            c = NextCol();
            Island(new Vector3(0f, y, z), 6f, c);
            Wp("rest3", null, new Vector3(0f, y, z));
            AddCheckpoint(new Vector3(0f, y, z), 6.2f);
            edge = z + 6f;

            // --- S4: sliding platforms
            float sz = edge + 2.6f + 2.25f;
            for (int i = 0; i < 4; i++)
            {
                var s = Slab(new Vector3(0f, y, sz), new Vector3(4.5f, 0.6f, 4.5f), Pal[(_col++) % Pal.Length]);
                var sp = s.AddComponent<SlidingPlatform>(); sp.axis = Vector3.right; sp.amplitude = 5f; sp.speed = 1.05f; sp.phase = i * 1.4f;
                Wp("slide" + i, s.transform, new Vector3(0f, y, sz));
                sz += 8.2f;
            }
            z = sz - 8.2f + 2.25f + 3f + 5.5f;
            c = NextCol();
            Island(new Vector3(0f, y, z), 5.5f, c);
            Wp("rest4", null, new Vector3(0f, y, z));
            AddCheckpoint(new Vector3(0f, y, z), 5.7f);
            edge = z + 5.5f;

            // --- S5: sweeper island
            z = edge + 3f + 9f;
            c = NextCol();
            Island(new Vector3(0f, y, z - 0.01f), 9f, c);
            AddCheckpoint(new Vector3(0f, y, z - 7f), 2.6f);   // respawn point sits on the entry side
            var barRoot = new GameObject("Sweeper"); barRoot.transform.SetParent(_root, false);
            barRoot.transform.position = new Vector3(0f, y + 0.35f, z);
            Mats.Prim(PrimitiveType.Cube, barRoot.transform, Vector3.zero, new Vector3(12f, 0.55f, 0.7f), Mats.Lit(new Color(0.95f, 0.3f, 0.3f), 0.3f));
            Mats.Prim(PrimitiveType.Cube, barRoot.transform, new Vector3(5.2f, 0f, 0f), new Vector3(1.5f, 0.6f, 0.75f), Mats.Lit(Color.white));
            Mats.Prim(PrimitiveType.Cube, barRoot.transform, new Vector3(-5.2f, 0f, 0f), new Vector3(1.5f, 0.6f, 0.75f), Mats.Lit(Color.white));
            Cyl(_root, new Vector3(0f, y + 0.2f, z), 1.4f, 0.4f, Mats.Lit(new Color(0.35f, 0.3f, 0.45f)), false);
            var rt = barRoot.AddComponent<RotatingThing>(); rt.localAxis = Vector3.up; rt.degPerSec = 85f;
            var sw = barRoot.AddComponent<SweeperBar>(); sw.halfLength = 6f; sw.halfThick = 0.35f; sw.height = 0.55f; sw.power = 13f;
            Wp("sweepIn", null, new Vector3(0f, y, z - 7f));
            Wp("sweepOut", null, new Vector3(0f, y, z + 7.5f));
            edge = z + 9f;
            Wp("sweepEdge", null, new Vector3(0f, y, edge - 0.8f));

            // --- S6: vanishing tiles
            float tz = edge + 1.6f + 1.6f;
            for (int i = 0; i < 8; i++)
            {
                float x = (i % 2 == 0 ? -1.6f : 1.6f) * (i == 0 ? 0f : 1f);
                var t = Slab(new Vector3(x, y, tz), new Vector3(3.3f, 0.5f, 3.3f), Pal[(_col + i) % Pal.Length]);
                t.AddComponent<VanishingTile>();
                Wp("tile" + i, t.transform, new Vector3(x, y, tz));
                tz += 4.5f;
            }
            _col += 8;
            z = tz - 4.5f + 1.65f + 1.2f + 5f;
            c = NextCol();
            Island(new Vector3(0f, y, z), 5f, c);
            Wp("rest6", null, new Vector3(0f, y, z));
            AddCheckpoint(new Vector3(0f, y, z), 5.2f);
            edge = z + 5f;

            // --- S7: the final climb
            float cz = edge + 2.4f + 2.5f;
            for (int i = 0; i < 9; i++)
            {
                y += 1.7f;
                float x = (i % 2 == 0 ? -3.2f : 3.2f) * (i == 8 ? 0f : 1f);
                var s = Slab(new Vector3(x, y, cz), new Vector3(5f, 0.7f, 5f), Pal[(_col + i) % Pal.Length]);
                if (i == 3 || i == 6) { var sp = s.AddComponent<SlidingPlatform>(); sp.axis = Vector3.right; sp.amplitude = 3f; sp.speed = 1.3f; sp.phase = i; }
                Wp("climb" + i, s.transform, new Vector3(x, y, cz));
                if (i == 4) AddCheckpoint(new Vector3(x, y, cz), 3.2f);
                cz += 7.4f;
            }
            _col += 9;
            z = cz - 7.4f + 2.5f + 2.4f + 9f;
            c = NextCol();
            Island(new Vector3(0f, y, z), 9.5f, Pal[1]);
            FinishPos = new Vector3(0f, y, z);
            Wp("finish", null, FinishPos);
            StartArch(new Vector3(0f, y, z - 6f), "FINISH");
            // flag
            Cyl(_root, FinishPos + new Vector3(0f, 3f, 2f), 0.18f, 6f, Mats.Lit(new Color(0.95f, 0.95f, 1f), 0.5f), false);
            var cloth = Mats.Prim(PrimitiveType.Cube, _root, FinishPos + new Vector3(0.9f, 5.2f, 2f), new Vector3(1.8f, 1.1f, 0.05f), Mats.LitTex(Checker(), new Vector2(3f, 2f), Color.white));
            FlagCloth = cloth.transform;
            EndZ = z;
            Decor();
        }

        private void StartArch(Vector3 pos, string label)
        {
            var post = Mats.Lit(new Color(0.95f, 0.95f, 1f), 0.5f);
            Cyl(_root, pos + new Vector3(-6.5f, 2.2f, 0f), 0.5f, 4.4f, post, false);
            Cyl(_root, pos + new Vector3(6.5f, 2.2f, 0f), 0.5f, 4.4f, post, false);
            Mats.Prim(PrimitiveType.Cube, _root, pos + new Vector3(0f, 4.4f, 0f), new Vector3(13.5f, 1.1f, 0.3f), Mats.LitTex(Checker(), new Vector2(14f, 1.2f), Color.white));
        }

        private static Texture2D _checker;
        private static Texture2D Checker()
        {
            if (_checker != null) return _checker;
            _checker = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            _checker.SetPixels(new[] { Color.white, Color.black, Color.black, Color.white });
            _checker.Apply();
            return _checker;
        }

        private void Decor()
        {
            var rnd = new System.Random(7);
            var white = Mats.Unlit(new Color(1f, 1f, 1f));
            for (int i = 0; i < 46; i++)
            {
                float zz = Mathf.Lerp(-40f, EndZ + 90f, (float)rnd.NextDouble());
                float side = rnd.NextDouble() < 0.5 ? -1f : 1f;
                float xx = side * (28f + (float)rnd.NextDouble() * 80f);
                float yy = Mathf.Lerp(-26f, 36f, (float)rnd.NextDouble());
                var cl = new GameObject("Cloud"); cl.transform.SetParent(_root, false);
                cl.transform.position = new Vector3(xx, yy, zz);
                int blobs = 3 + rnd.Next(3);
                for (int b = 0; b < blobs; b++)
                {
                    float s = 7f + (float)rnd.NextDouble() * 7f;
                    Mats.Prim(PrimitiveType.Sphere, cl.transform, new Vector3((b - blobs * 0.5f) * 5f, (float)rnd.NextDouble() * 2f, (float)rnd.NextDouble() * 3f), new Vector3(s, s * 0.55f, s * 0.8f), white);
                }
                cl.AddComponent<Drift>().v = new Vector3(0.4f * side, 0f, 0f);
            }
            // a few faraway floating rocks
            for (int i = 0; i < 9; i++)
            {
                float zz = Mathf.Lerp(0f, EndZ, (float)rnd.NextDouble());
                float side = i % 2 == 0 ? -1f : 1f;
                var top = new Vector3(side * (45f + (float)rnd.NextDouble() * 40f), Mathf.Lerp(-8f, 24f, (float)rnd.NextDouble()), zz);
                Island(top, 5f + (float)rnd.NextDouble() * 6f, Pal[rnd.Next(Pal.Length)], false, 1.6f);
            }
        }

        // ---------- queries ----------
        public static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>Advance a player's checkpoint if they reached the next one.</summary>
        public bool TryCheckpoint(Player p)
        {
            int n = p.cpIndex + 1;
            if (n >= Checkpoints.Count) return false;
            var cp = Checkpoints[n];
            Vector3 d = p.transform.position - cp.pos;
            if (new Vector2(d.x, d.z).magnitude < cp.radius && Mathf.Abs(d.y) < 2.5f) { p.cpIndex = n; return true; }
            return false;
        }

        public bool InFinish(Player p)
        {
            Vector3 d = p.transform.position - FinishPos;
            return new Vector2(d.x, d.z).magnitude < FinishRadius && d.y > -2f && d.y < 3f;
        }

        public Vector3 RespawnPos(int cp)
        {
            var c = Checkpoints[Mathf.Clamp(cp, 0, Checkpoints.Count - 1)];
            return c.pos + Vector3.up * 0.4f;
        }

        public float Progress(Vector3 pos, int cp)
        {
            Vector3 next = cp + 1 < Checkpoints.Count ? Checkpoints[cp + 1].pos : FinishPos;
            return cp * 1000f - Vector3.Distance(Flat(pos), Flat(next));
        }

        private void Update()
        {
            if (FlagCloth != null) FlagCloth.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 3f) * 8f, 0f);
        }
    }
}
