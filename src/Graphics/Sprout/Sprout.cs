using System;
using UnityEngine;

namespace applecat.Graphics.Sprout
{
    public sealed class Sprout : GraphicsModule
    {
        private const int StalkNodes = 10;

        private const float StalkLength = 10.5f;

        private const int StalkDrawSegments = 12;

        private const int LeafRibNodes = 5;

        private const int LeafSegmentsPerRib = 4;

        private const int LeafDrawSegments = (LeafRibNodes - 1) * LeafSegmentsPerRib;

        private const float StalkWidthRoot = 1.15f;

        private const float StalkWidthTip = 0.28f;

        private const float StalkEmbedDepth = 3.2f;

        private const float StalkGravity = 0.010f;

        private const float StalkDroopDegrees = 22f;

        private const float StalkRestStiffness = 0.5f;

        private const float LeafRestStiffness = 0.3f;

        private const float LeafTipFactor = 0.06f;

        private const float LeafMinFactor = 0.12f;

        private const float StalkRestTilt = 24f;

        private const float LeafAttachPosition = 0.46f;

        private const float LeafTiltDegrees = 55f;

        private const float LeafLength = 7.6f;

        private const float LeafHalfWidth = 2.2f;

        private static readonly LeafAnchor[] Anchors =
        {
            new LeafAnchor(LeafAttachPosition, -LeafTiltDegrees, LeafLength, LeafHalfWidth),
            new LeafAnchor(LeafAttachPosition, LeafTiltDegrees, LeafLength, LeafHalfWidth)
        };

        private const int LightSampleInterval = 4;

        private const float PupScale = 0.78f;

        private const float VisibilityLerp = 0.12f;

        private static readonly Color StemRootColor = new Color(0.36f, 0.48f, 0.26f);

        private static readonly Color StemTipColor = new Color(0.55f, 0.68f, 0.38f);

        private static readonly Color LeafColor = new Color(0.48f, 0.63f, 0.33f);

        private static readonly Color LeafVeinColor = new Color(0.61f, 0.73f, 0.45f);

        private readonly struct LeafAnchor
        {
            public readonly float Position;

            public readonly float Angle;

            public readonly float Length;

            public readonly float HalfWidth;

            public LeafAnchor(float position, float angle, float length, float halfWidth)
            {
                Position = position;
                Angle = angle;
                Length = length;
                HalfWidth = halfWidth;
            }
        }

        private sealed class Leaf
        {
            public LeafAnchor Anchor;

            public VerletChain Rib;

            public TriangleMesh Mesh;

            public Vector2[] Chain;

            public Vector2[] Points;

            public Vector2[] Normals;

            public float[] RingWidths;

            public Vector2 AnchorWorld;

            public Vector2 AnchorPrev;
        }

        private readonly Player _player;

        private VerletChain _stalk;

        private readonly Leaf[] _leaves;

        private readonly Vector2[] _stalkChain;

        private readonly Vector2[] _stalkPoints;

        private readonly Vector2[] _stalkNormals;

        private readonly float[] _ringWidths;

        private TriangleMesh _stalkMesh;

        private int _startSprite = -1;

        private bool _spritesReady;

        public int FirstSpriteIndex => _spritesReady ? _startSprite : int.MaxValue;

        private int SpriteCount => 1 + _leaves.Length;

        private bool SpritesAvailable(RoomCamera.SpriteLeaser sLeaser)
        {
            return _spritesReady
                && sLeaser?.sprites != null
                && sLeaser.sprites.Length >= _startSprite + SpriteCount;
        }

        private Vector2 _rootWorld;

        private Vector2 _rootPrev;

        private Vector2 _rootVelocity;

        private Vector2 _up = Vector2.up;

        private Vector2 _upPrev = Vector2.up;

        private float _scale = 1f;

        private float _targetScale = 1f;

        private float _phase;

        private float _darkness;

        private int _lightTimer;

        private Vector2 _lastBodyPos;

        private const int HeadSpriteIndex = 3;

        public Sprout(PlayerGraphics graphics)
            : base(graphics.owner, internalContainers: false)
        {
            _player = (Player)graphics.owner;

            if (graphics.RenderAsPup)
            {
                _scale = PupScale;
                _targetScale = PupScale;
            }

            _stalk = new VerletChain(
                StalkNodes,
                StalkLength,
                StalkDroopDegrees,
                damping: 0.9f,
                stiffness: 1f,
                extraMassAtRoot: 0.85f,
                restStiffness: StalkRestStiffness);
            _stalkChain = new Vector2[StalkNodes];
            _stalkPoints = new Vector2[StalkDrawSegments + 1];
            _stalkNormals = new Vector2[StalkDrawSegments + 1];
            _ringWidths = new float[StalkDrawSegments + 1];

            _leaves = new Leaf[Anchors.Length];
            for (int i = 0; i < Anchors.Length; i++)
            {
                _leaves[i] = new Leaf
                {
                    Anchor = Anchors[i],
                    Rib = new VerletChain(LeafRibNodes, Anchors[i].Length, 0f, damping: 0.88f, stiffness: 0.9f, extraMassAtRoot: 1.2f, restStiffness: LeafRestStiffness),
                    Chain = new Vector2[LeafRibNodes],

                    Points = new Vector2[LeafDrawSegments + 1],
                    Normals = new Vector2[LeafDrawSegments + 1],
                    RingWidths = new float[LeafDrawSegments + 1]
                };
            }

            _lastBodyPos = _player.mainBodyChunk.pos;
            _up = Vector2.up;
            _rootWorld = _player.mainBodyChunk.pos;
            Reset();
        }

        public bool Active => _player != null && _player.room != null && !_player.dead && _player.graphicsModule != null;

        public override void Reset()
        {
            base.Reset();

            if (_player == null)
            {
                return;
            }

            _rootVelocity = Vector2.zero;
            _rootWorld = _player.mainBodyChunk.pos;
            _lastBodyPos = _player.mainBodyChunk.pos;

            _up = BuildHeadFrame().Up;
            _stalk.RestDirection = RestAxisFor(StalkRestTilt, _up);
            _stalk.Reset(_rootWorld, _up);

            for (int i = 0; i < _leaves.Length; i++)
            {
                Leaf leaf = _leaves[i];
                Vector2 axis = RestAxisFor(leaf.Anchor.Angle, _up);
                leaf.Rib.Reset(_rootWorld + axis * 0.4f, axis);
            }

            _scale = _targetScale;
        }

        public override void SuckedIntoShortCut(Vector2 shortCutPosition)
        {
            _targetScale = 0f;
            _scale = 0f;

            Vector2 delta = shortCutPosition - _rootWorld;
            _stalk.Teleport(delta);
            for (int i = 0; i < _leaves.Length; i++)
            {
                _leaves[i].Rib.Teleport(delta);
            }

            _rootWorld = shortCutPosition;
            _lastBodyPos = shortCutPosition;
        }

        public override void Update()
        {
            base.Update();

            if (_player == null)
            {
                return;
            }

            _targetScale = Active ? 1f : 0f;
            if (Active)
            {
                Simulate();
            }
            else
            {
                _rootWorld = _player.mainBodyChunk.pos;
                Vector2 rest = RestAxisFor(StalkRestTilt, _up);
                _stalk.RestDirection = rest;
                _stalk.Reset(_rootWorld, rest);
                _stalk.CopyPositionsTo(_stalkChain);
            }

            _scale = Mathf.Lerp(_scale, _targetScale, VisibilityLerp);
            _phase += 0.11f;
            _lightTimer++;
        }

        public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            _startSprite = sLeaser.sprites.Length;

            Array.Resize(ref sLeaser.sprites, sLeaser.sprites.Length + SpriteCount);

            _stalkMesh = MeshUtil.BuildStrip(StalkDrawSegments);
            _stalkMesh.color = Color.white;
            _stalkMesh.isVisible = true;
            sLeaser.sprites[_startSprite] = _stalkMesh;

            for (int i = 0; i < _leaves.Length; i++)
            {
                TriangleMesh mesh = MeshUtil.BuildStrip(LeafDrawSegments);
                ApplyLeafAtlasSlice(mesh);
                mesh.color = Color.white;
                mesh.isVisible = true;
                _leaves[i].Mesh = mesh;
                sLeaser.sprites[_startSprite + 1 + i] = mesh;
            }

            int expectedStalk = MeshUtil.StripVertexCount(StalkDrawSegments);
            int expectedLeaf = MeshUtil.StripVertexCount(LeafDrawSegments);
            if (_stalkMesh.vertices.Length != expectedStalk || _leaves[0].Mesh.vertices.Length != expectedLeaf)
            {
                throw new InvalidOperationException(
                    $"豆芽网格顶点数不符：茎 {_stalkMesh.vertices.Length}/{expectedStalk}，"
                    + $"叶 {_leaves[0].Mesh.vertices.Length}/{expectedLeaf}。");
            }

            _spritesReady = true;
            RefreshColors();
            base.InitiateSprites(sLeaser, rCam);

            AddToContainer(sLeaser, rCam, null);
        }

        private static void ApplyLeafAtlasSlice(TriangleMesh mesh)
        {
            FAtlasElement element = Futile.atlasManager.GetElementWithName("Futile_White");
            Vector2 uvBottomLeft = element.uvBottomLeft;
            Vector2 uvSpan = element.uvTopRight - uvBottomLeft;
            Vector2 sliceSpan = new Vector2(uvSpan.x * 0.55f, uvSpan.y * 0.85f);

            for (int i = 0; i < mesh.UVvertices.Length; i++)
            {
                float along = i / (float)Mathf.Max(mesh.UVvertices.Length - 1, 1);
                mesh.UVvertices[i] = uvBottomLeft + new Vector2(i % 2 == 0 ? 0f : sliceSpan.x, sliceSpan.y * along);
            }
        }

        public override void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
        {
            if (!SpritesAvailable(sLeaser))
            {
                return;
            }

            FContainer target = newContatiner
                ?? sLeaser.sprites[HeadSpriteIndex].container
                ?? rCam.ReturnFContainer("Midground");

            Attach(sLeaser.sprites[_startSprite], target);
            for (int i = 0; i < _leaves.Length; i++)
            {
                Attach(sLeaser.sprites[_startSprite + 1 + i], target);
            }
        }

        private static void Attach(FSprite sprite, FContainer target)
        {
            if (sprite == null || target == null)
            {
                return;
            }

            sprite.RemoveFromContainer();
            target.AddChild(sprite);
        }

        public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            RefreshColors();
        }

        private void Simulate()
        {
            HeadFrame frame = BuildHeadFrame();

            _rootVelocity = _player.mainBodyChunk.pos - _lastBodyPos;
            _lastBodyPos = _player.mainBodyChunk.pos;

            _up = frame.Up;
            Vector2 inward = new Vector2(-_up.y, _up.x) * _player.flipDirection;
            Vector2 root = frame.Position + _up * (frame.Radius * 0.85f) - _up * StalkEmbedDepth;

            float gravityScale = Mathf.Clamp(_player.EffectiveRoomGravity, 0f, 1f);
            float submersion = Mathf.Clamp01(_player.mainBodyChunk.submersion);
            Vector2 gravity = new Vector2(0f, -StalkGravity * gravityScale * (1f - 0.75f * submersion));

            Vector2 wind = BuildWind() * (0.055f * gravityScale + 0.02f);

            float timeScale = SimulationTimeScale();
            _stalk.TimeScale = timeScale;

            _rootPrev = _rootWorld;
            _upPrev = _up;

            _stalk.RestDirection = RestAxisFor(StalkRestTilt, _up);
            _stalk.Step(root, _rootVelocity, gravity, wind, subSteps: 6);
            _stalk.CopyPositionsTo(_stalkChain);

            _rootWorld = root;

            for (int i = 0; i < _leaves.Length; i++)
            {
                SimulateLeaf(_leaves[i], gravity, wind, timeScale);
            }
        }

        private float SimulationTimeScale()
        {
            RainWorldGame game = _player?.room?.game;
            if (game == null || game.framesPerSecond <= 0)
            {
                return 1f;
            }

            return Mathf.Clamp(game.framesPerSecond / 40f, 0.1f, 8f);
        }

        private void SimulateLeaf(Leaf leaf, Vector2 gravity, Vector2 wind, float timeScale)
        {
            Vector2 basePos = PointOnStalk(leaf.Anchor.Position);
            Vector2 tangent = TangentOnStalk(leaf.Anchor.Position);
            Vector2 normal = new Vector2(-tangent.y, tangent.x);

            Vector2 anchorPos = basePos + normal * (leaf.Anchor.HalfWidth * 0.12f) * Mathf.Sign(leaf.Anchor.Angle);

            leaf.AnchorPrev = leaf.AnchorWorld;
            leaf.AnchorWorld = anchorPos - basePos;

            leaf.Rib.RestDirection = RestAxisFor(leaf.Anchor.Angle, _up);
            leaf.Rib.TimeScale = timeScale;

            leaf.Rib.Step(anchorPos, _rootVelocity, gravity * 0.9f, wind, subSteps: 6);
        }

        private Vector2 BuildWind()
        {
            float slow = Mathf.Sin(_phase * 0.9f) * 0.6f + Mathf.Sin(_phase * 0.31f + 1.3f) * 0.4f;
            float fast = Mathf.Sin(_phase * 3.7f + 0.7f) * 0.35f + Mathf.Sin(_phase * 6.1f + 2.2f) * 0.2f;
            return new Vector2(slow + fast, Mathf.Sin(_phase * 2.3f + 0.4f) * 0.25f);
        }

        private readonly struct HeadFrame
        {
            public readonly Vector2 Position;

            public readonly Vector2 Up;

            public readonly float Radius;

            public HeadFrame(Vector2 position, Vector2 up, float radius)
            {
                Position = position;
                Up = up;
                Radius = radius;
            }
        }

        private HeadFrame BuildHeadFrame()
        {
            PlayerGraphics graphics = _player.graphicsModule as PlayerGraphics;
            if (graphics == null)
            {
                Vector2 fallback = _player.bodyChunks[0].pos - _player.bodyChunks[1].pos;
                return new HeadFrame(_player.mainBodyChunk.pos, fallback.sqrMagnitude > 1e-6f ? fallback.normalized : Vector2.up, _player.bodyChunks[0].rad);
            }

            Vector2 up = _up;
            if (graphics.head != null)
            {
                Vector2 body = graphics.drawPositions[1, 0];
                Vector2 head = graphics.head.pos;
                Vector2 fromBody = head - body;
                if (fromBody.sqrMagnitude > 1e-4f)
                {
                    up = Vector2.Lerp(_up, fromBody.normalized, 0.35f).normalized;
                }
            }

            return new HeadFrame(graphics.head != null ? graphics.head.pos : _player.mainBodyChunk.pos, up, _player.bodyChunks[0].rad);
        }

        public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            base.DrawSprites(sLeaser, rCam, timeStacker, camPos);

            if (!SpritesAvailable(sLeaser))
            {
                return;
            }

            bool visible = Active && _scale > 0.02f && !culled;
            TriangleMesh stalkMesh = sLeaser.sprites[_startSprite] as TriangleMesh;
            stalkMesh.isVisible = visible;
            for (int i = 0; i < _leaves.Length; i++)
            {
                _leaves[i].Mesh.isVisible = visible;
            }
            if (!visible)
            {
                return;
            }

            _up = Vector2.Lerp(_upPrev, _up, Mathf.Clamp01(timeStacker)).normalized;
            _rootWorld = Vector2.Lerp(_rootPrev, _rootWorld, Mathf.Clamp01(timeStacker));

            SampleStalk(camPos, timeStacker);
            DrawStem();

            for (int i = 0; i < _leaves.Length; i++)
            {
                SampleLeaf(_leaves[i], camPos, timeStacker);
                DrawLeaf(_leaves[i]);
            }

            if (_lightTimer % LightSampleInterval == 0)
            {
                UpdateShading(rCam);
            }
        }

        private void SampleStalk(Vector2 camPos, float timeStacker)
        {
            _stalk.CopyInterpolatedPositionsTo(_stalkChain, timeStacker);
            SamplePolyline(_stalkChain, _stalkPoints, _stalkNormals, camPos);
        }

        private void SampleLeaf(Leaf leaf, Vector2 camPos, float timeStacker)
        {
            leaf.Rib.CopyInterpolatedPositionsTo(leaf.Chain, timeStacker);

            Vector2 basePos = PointOnStalk(leaf.Anchor.Position, timeStacker);
            Vector2 offset = Vector2.Lerp(leaf.AnchorPrev, leaf.AnchorWorld, Mathf.Clamp01(timeStacker));
            leaf.Chain[0] = basePos + offset;

            SamplePolyline(leaf.Chain, leaf.Points, leaf.Normals, camPos);
        }

        private static void SamplePolyline(Vector2[] chain, Vector2[] points, Vector2[] normals, Vector2 camPos)
        {
            int segments = points.Length - 1;
            int chainSegments = chain.Length - 1;

            for (int p = 0; p <= segments; p++)
            {
                float t = p / (float)segments;
                float scaled = t * chainSegments;
                int seg = Mathf.Min((int)scaled, chainSegments - 1);
                float localT = scaled - seg;

                Bezier.CatmullRomToBezier(
                    chain[Mathf.Max(seg - 1, 0)],
                    chain[seg],
                    chain[seg + 1],
                    chain[Mathf.Min(seg + 2, chainSegments)],
                    out Vector2 c1,
                    out Vector2 c2);

                points[p] = Bezier.Cubic(chain[seg], c1, c2, chain[seg + 1], localT) - camPos;
            }

            points[0] = chain[0] - camPos;
            points[segments] = chain[chainSegments] - camPos;

            ComputeNormals(points, normals);
        }

        private static void ComputeNormals(Vector2[] points, Vector2[] normals)
        {
            int last = points.Length - 1;
            for (int i = 0; i <= last; i++)
            {
                Vector2 tangent = points[Mathf.Min(i + 1, last)] - points[Mathf.Max(i - 1, 0)];
                if (tangent.sqrMagnitude < 1e-8f)
                {
                    tangent = Vector2.up;
                }

                tangent.Normalize();
                normals[i] = new Vector2(-tangent.y, tangent.x);
            }
        }

        private static void StrokeRibbon(TriangleMesh mesh, Vector2[] points, Vector2[] normals, float[] widths)
        {
            int segments = points.Length - 1;

            for (int ring = 0; ring < segments; ring++)
            {
                MeshUtil.SetRing(mesh, ring, points[ring] + normals[ring] * widths[ring], points[ring] - normals[ring] * widths[ring]);
            }

            mesh.MoveVertice(MeshUtil.RingBackIndex(segments), points[segments]);
            mesh.MoveVertice(MeshUtil.StripTipIndex(segments), points[segments]);
            mesh.Refresh();
        }

        private void DrawStem()
        {
            int segments = _stalkPoints.Length - 1;
            for (int ring = 0; ring <= segments; ring++)
            {
                _ringWidths[ring] = Mathf.Lerp(StalkWidthRoot, StalkWidthTip, ring / (float)segments) * _scale;
            }

            StrokeRibbon(_stalkMesh, _stalkPoints, _stalkNormals, _ringWidths);
        }

        private void DrawLeaf(Leaf leaf)
        {
            int segments = leaf.Points.Length - 1;
            float halfWidth = leaf.Anchor.HalfWidth * _scale;

            for (int ring = 0; ring <= segments; ring++)
            {
                float along = ring / (float)segments;

                float profile = Mathf.Pow(Mathf.Sin(Mathf.Pow(along, 0.78f) * Mathf.PI), 0.7f);

                leaf.RingWidths[ring] = Mathf.Max(profile, ring == 0 || ring == segments ? LeafTipFactor : LeafMinFactor) * halfWidth;
            }

            StrokeRibbon(leaf.Mesh, leaf.Points, leaf.Normals, leaf.RingWidths);
        }

        private void RefreshColors()
        {
            if (!_spritesReady)
            {
                return;
            }

            Color stemRoot = Shade(StemRootColor);
            Color stemTip = Shade(StemTipColor);
            Color leaf = Shade(LeafColor);
            Color vein = Shade(LeafVeinColor);

            int stalkVerts = _stalkMesh.vertices.Length;
            for (int i = 0; i < stalkVerts; i++)
            {
                float along = i / (float)Mathf.Max(stalkVerts - 1, 1);
                _stalkMesh.verticeColors[i] = Color.Lerp(stemRoot, stemTip, along);
            }

            for (int i = 0; i < _leaves.Length; i++)
            {
                TriangleMesh mesh = _leaves[i].Mesh;
                int verts = mesh.vertices.Length;
                for (int v = 0; v < verts; v++)
                {
                    mesh.verticeColors[v] = v % 2 == 0 ? vein : leaf;
                }
            }
        }

        private void UpdateShading(RoomCamera rCam)
        {
            if (_player.room == null)
            {
                return;
            }

            _darkness = Mathf.Clamp01(_player.room.DarknessOfPoint(rCam, _rootWorld));
            RefreshColors();
        }

        private Color Shade(Color color)
        {
            return Color.Lerp(color, new Color(color.r * 0.25f, color.g * 0.3f, color.b * 0.3f), _darkness * 0.75f);
        }

        private Vector2 RestAxisFor(float degrees, Vector2 up)
        {
            Vector2 side = new Vector2(up.y, -up.x) * _player.flipDirection;
            float radians = degrees * Mathf.Deg2Rad;
            return (up * Mathf.Cos(radians) + side * Mathf.Sin(radians)).normalized;
        }

        private Vector2 PointOnStalk(float t, float timeStacker = -1f)
        {
            float clamped = Mathf.Clamp01(t);
            return timeStacker < 0f ? _stalk.Sample(clamped) : _stalk.SampleInterpolated(clamped, timeStacker);
        }

        private Vector2 TangentOnStalk(float t, float timeStacker = -1f)
        {
            Vector2 a = PointOnStalk(Mathf.Clamp01(t - 0.08f), timeStacker);
            Vector2 b = PointOnStalk(Mathf.Clamp01(t + 0.08f), timeStacker);
            Vector2 dir = b - a;
            return dir.sqrMagnitude < 1e-8f ? _up : dir.normalized;
        }
    }
}