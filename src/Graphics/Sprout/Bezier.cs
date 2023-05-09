using System;
using UnityEngine;

namespace applecat.Graphics.Sprout
{
    public static class Bezier
    {
        public static Vector2 Quad(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            Vector2 ab = Vector2.LerpUnclamped(a, b, t);
            Vector2 bc = Vector2.LerpUnclamped(b, c, t);
            return Vector2.LerpUnclamped(ab, bc, t);
        }

        public static Vector2 Cubic(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            Vector2 ab = Vector2.LerpUnclamped(a, b, t);
            Vector2 bc = Vector2.LerpUnclamped(b, c, t);
            Vector2 cd = Vector2.LerpUnclamped(c, d, t);
            Vector2 abbc = Vector2.LerpUnclamped(ab, bc, t);
            Vector2 bccd = Vector2.LerpUnclamped(bc, cd, t);
            return Vector2.LerpUnclamped(abbc, bccd, t);
        }

        public static void CatmullRomToBezier(in Vector2 prev, in Vector2 start, in Vector2 end, in Vector2 next,
            out Vector2 c1, out Vector2 c2)
        {
            c1 = start + (end - prev) / 6f;
            c2 = end - (next - start) / 6f;
        }

        public static Vector2 EvaluatePolyline(Vector2[] points, float t)
        {
            if (points == null || points.Length < 2)
            {
                throw new ArgumentException("至少需要 2 个采样点。", nameof(points));
            }

            int segments = points.Length - 1;
            float scaled = Mathf.Clamp01(t) * segments;
            int index = Mathf.Min((int)scaled, segments - 1);
            float localT = scaled - index;

            int last = points.Length - 1;
            Vector2 prev = points[Mathf.Max(index - 1, 0)];
            Vector2 start = points[index];
            Vector2 end = points[index + 1];
            Vector2 next = points[Mathf.Min(index + 2, last)];

            CatmullRomToBezier(prev, start, end, next, out Vector2 c1, out Vector2 c2);
            return Cubic(start, c1, c2, end, localT);
        }
    }
}