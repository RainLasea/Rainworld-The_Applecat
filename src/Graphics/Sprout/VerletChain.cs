using System;
using UnityEngine;

namespace applecat.Graphics.Sprout
{
    public sealed class VerletChain
    {
        public struct Node
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float InvMass;

            public Vector2 LastPos;
        }

        private const float MaxFrameVelocity = 24f;

        private const int MaxSubSteps = 4;

        private readonly Node[] _nodes;
        private readonly Vector2[] _restShape;
        private readonly float _damping;
        private readonly float _stiffness;

        private Vector2 _restDirection;

        public float TimeScale { get; set; } = 1f;

        public int Count => _nodes.Length;

        public float RestLength { get; private set; }

        public Node[] Nodes => _nodes;

        public Vector2[] RestShape => _restShape;

        public float RestStiffness { get; private set; }

        public float RestDamping { get; private set; }

        public Vector2 RestDirection
        {
            get => _restDirection;
            set => _restDirection = value.sqrMagnitude < 1e-8f ? Vector2.up : value.normalized;
        }

        public VerletChain(
            int count,
            float totalLength,
            float droopDegrees = 0f,
            float damping = 0.92f,
            float stiffness = 1f,
            float extraMassAtRoot = 1f,
            float restStiffness = 0.25f)
        {
            if (count < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "质点链至少需要 2 个质点。");
            }

            _nodes = new Node[count];
            _damping = damping;
            _stiffness = stiffness;
            RestStiffness = Mathf.Max(restStiffness, 0f);
            RestDamping = 2f * Mathf.Sqrt(Mathf.Max(RestStiffness, 1e-6f));
            _restDirection = Vector2.up;

            _restShape = new Vector2[count];
            BuildRestShape(totalLength, droopDegrees * Mathf.Deg2Rad);

            for (int i = 0; i < count; i++)
            {
                _nodes[i].InvMass = i == 0 ? 0f : Mathf.Lerp(extraMassAtRoot, 0.6f, i / (float)(count - 1));
            }

            Reset(Vector2.zero, Vector2.up);
        }

        private void BuildRestShape(float totalLength, float droopRadians)
        {
            Vector2 p0 = Vector2.zero;
            Vector2 p1 = Vector2.up * (totalLength * 0.5f);

            Vector2 p2 = new Vector2(Mathf.Sin(droopRadians), Mathf.Cos(droopRadians)) * totalLength;

            const int tableSize = 512;
            float[] arcLength = new float[tableSize + 1];
            Vector2 previous = Bezier.Quad(p0, p1, p2, 0f);
            for (int i = 1; i <= tableSize; i++)
            {
                Vector2 point = Bezier.Quad(p0, p1, p2, i / (float)tableSize);
                arcLength[i] = arcLength[i - 1] + Vector2.Distance(previous, point);
                previous = point;
            }

            float totalArc = arcLength[tableSize];
            int nodeCount = _restShape.Length;

            _restShape[0] = p0;
            for (int node = 1; node < nodeCount; node++)
            {
                float target = totalArc * node / (nodeCount - 1);
                _restShape[node] = Bezier.Quad(p0, p1, p2, ParameterAtArcLength(arcLength, target));
            }

            _restShape[nodeCount - 1] = p2;

            float segment = Vector2.Distance(_restShape[0], _restShape[1]);
            RestLength = segment > 1e-5f ? segment : totalLength / (nodeCount - 1);
        }

        private static float ParameterAtArcLength(float[] arcLength, float target)
        {
            int lo = 0;
            int hi = arcLength.Length - 1;
            if (target <= arcLength[lo])
            {
                return 0f;
            }

            if (target >= arcLength[hi])
            {
                return 1f;
            }

            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (arcLength[mid] <= target)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            float span = arcLength[hi] - arcLength[lo];
            float fraction = span > 1e-8f ? (target - arcLength[lo]) / span : 0f;
            return (lo + fraction) / (arcLength.Length - 1);
        }

        public void Reset(Vector2 root, Vector2 direction)
        {
            RestDirection = direction;
            for (int i = 0; i < _nodes.Length; i++)
            {
                _nodes[i].Pos = root + WorldRestOffset(i);
                _nodes[i].LastPos = _nodes[i].Pos;
                _nodes[i].Vel = Vector2.zero;
            }
        }

        public void Teleport(Vector2 delta)
        {
            for (int i = 0; i < _nodes.Length; i++)
            {
                _nodes[i].Pos += delta;
                _nodes[i].LastPos += delta;
            }
        }

        public void Step(Vector2 root, Vector2 rootVelocity, Vector2 gravity, Vector2 wind, int subSteps)
        {
            for (int i = 0; i < _nodes.Length; i++)
            {
                _nodes[i].LastPos = _nodes[i].Pos;
            }

            _nodes[0].Pos = root;
            _nodes[0].Vel = Vector2.zero;

            Vector2 clampedRootVelocity = ClampLength(rootVelocity, MaxFrameVelocity);

            int subStepsPerStep = Mathf.Clamp(Mathf.CeilToInt(TimeScale - 0.001f), 1, MaxSubSteps);
            float subScale = TimeScale / subStepsPerStep;

            float subDamping = subStepsPerStep == 1 ? _damping : Mathf.Pow(_damping, 1f / subStepsPerStep);
            Vector2 subRootVelocity = clampedRootVelocity / subStepsPerStep;

            for (int sub = 0; sub < subStepsPerStep; sub++)
            {
                for (int i = 1; i < _nodes.Length; i++)
                {
                    ref Node n = ref _nodes[i];
                    float along = i / (float)(_nodes.Length - 1);

                    Vector2 velocity = ClampLength(n.Vel * subDamping, MaxFrameVelocity);

                    velocity += subRootVelocity * (0.35f * (1f - along));

                    velocity += (gravity + wind * (0.35f + 0.65f * along)) * (n.InvMass * subScale);

                    n.Vel = ClampLength(velocity, MaxFrameVelocity);
                    n.Pos += n.Vel;
                }

                if (RestStiffness > 0f)
                {
                    ApplyRestShape(root, subScale);
                }
            }

            for (int pass = 0; pass < subSteps; pass++)
            {
                if (pass % 2 == 0)
                {
                    for (int i = _nodes.Length - 1; i >= 1; i--)
                    {
                        SolveDistance(i - 1, i);
                    }
                }
                else
                {
                    for (int i = 1; i < _nodes.Length; i++)
                    {
                        SolveDistance(i - 1, i);
                    }
                }

                _nodes[0].Pos = root;
                _nodes[0].Vel = Vector2.zero;
            }
        }

        private static Vector2 ClampLength(Vector2 v, float max)
        {
            float sqr = v.sqrMagnitude;
            if (sqr <= max * max)
            {
                return v;
            }

            return v * (max / Mathf.Sqrt(sqr));
        }

        private Vector2 WorldRestOffset(int index)
        {
            Vector2 local = _restShape[index];
            Vector2 forward = _restDirection;

            Vector2 side = new Vector2(forward.y, -forward.x);
            return side * local.x + forward * local.y;
        }

        private void ApplyRestShape(Vector2 root, float scale)
        {
            for (int i = 1; i < _nodes.Length; i++)
            {
                ref Node n = ref _nodes[i];
                float along = i / (float)(_nodes.Length - 1);

                Vector2 target = root + WorldRestOffset(i);
                Vector2 delta = target - n.Pos;

                float strength = RestStiffness * (0.5f + 0.5f * along) * scale;
                n.Vel += delta * strength - n.Vel * (RestDamping * 0.5f * strength);
            }
        }

        private void SolveDistance(int a, int b)
        {
            Vector2 delta = _nodes[b].Pos - _nodes[a].Pos;
            float distance = delta.magnitude;
            if (distance < 1e-5f)
            {
                delta = Vector2.up;
                distance = 1f;
            }

            float invSum = _nodes[a].InvMass + _nodes[b].InvMass;
            if (invSum <= 0f)
            {
                return;
            }

            float error = (distance - RestLength) / distance * _stiffness;
            Vector2 correction = delta * error;
            Vector2 pushA = correction * (_nodes[a].InvMass / invSum);
            Vector2 pushB = correction * (_nodes[b].InvMass / invSum);

            if (_nodes[a].InvMass > 0f)
            {
                _nodes[a].Pos += pushA;
                _nodes[a].Vel += pushA;
            }

            if (_nodes[b].InvMass > 0f)
            {
                _nodes[b].Pos -= pushB;
                _nodes[b].Vel -= pushB;
            }
        }

        public Vector2 Sample(float t)
        {
            float scaled = Mathf.Clamp01(t) * (_nodes.Length - 1);
            int index = Mathf.Min((int)scaled, _nodes.Length - 2);
            return Vector2.Lerp(_nodes[index].Pos, _nodes[index + 1].Pos, scaled - index);
        }

        public Vector2 SampleInterpolated(float t, float timeStacker)
        {
            float scaled = Mathf.Clamp01(t) * (_nodes.Length - 1);
            int index = Mathf.Min((int)scaled, _nodes.Length - 2);
            float blend = scaled - index;

            Vector2 a = Vector2.Lerp(_nodes[index].LastPos, _nodes[index + 1].LastPos, blend);
            Vector2 b = Vector2.Lerp(_nodes[index].Pos, _nodes[index + 1].Pos, blend);
            return Vector2.Lerp(a, b, Mathf.Clamp01(timeStacker));
        }

        public void CopyInterpolatedPositionsTo(Vector2[] into, float timeStacker)
        {
            if (into == null)
            {
                return;
            }

            float stacker = Mathf.Clamp01(timeStacker);
            int count = Mathf.Min(into.Length, _nodes.Length);
            for (int i = 0; i < count; i++)
            {
                into[i] = Vector2.Lerp(_nodes[i].LastPos, _nodes[i].Pos, stacker);
            }
        }

        public void CopyPositionsTo(Vector2[] into)
        {
            if (into == null)
            {
                return;
            }

            int count = Mathf.Min(into.Length, _nodes.Length);
            for (int i = 0; i < count; i++)
            {
                into[i] = _nodes[i].Pos;
            }
        }
    }
}