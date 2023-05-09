using UnityEngine;

namespace applecat.Graphics.Sprout
{
    public static class MeshUtil
    {
        public static int StripVertexCount(int segments)
        {
            return segments * 2 + 2;
        }

        public static int RingBackIndex(int ring)
        {
            return ring * 2;
        }

        public static int RingForwardIndex(int ring)
        {
            return ring * 2 + 1;
        }

        public static int StripTipIndex(int segments)
        {
            return segments * 2 + 1;
        }

        public static int[] BuildStripTriangles(int segments)
        {
            int[] indices = new int[segments * 6];
            int tip = StripTipIndex(segments);

            for (int seg = 0; seg < segments; seg++)
            {
                int a = RingBackIndex(seg);
                int b = RingForwardIndex(seg);

                int c = seg == segments - 1 ? tip : RingBackIndex(seg + 1);
                int d = seg == segments - 1 ? tip : RingForwardIndex(seg + 1);

                int o = seg * 6;
                indices[o] = a;
                indices[o + 1] = b;
                indices[o + 2] = c;
                indices[o + 3] = b;
                indices[o + 4] = d;
                indices[o + 5] = c;
            }

            return indices;
        }

        public static TriangleMesh BuildStrip(int segments)
        {
            int[] flat = BuildStripTriangles(segments);
            TriangleMesh.Triangle[] triangles = new TriangleMesh.Triangle[segments * 2];
            for (int i = 0; i < triangles.Length; i++)
            {
                triangles[i] = new TriangleMesh.Triangle(flat[i * 3], flat[i * 3 + 1], flat[i * 3 + 2]);
            }

            TriangleMesh mesh = new TriangleMesh("Futile_White", triangles, customColor: true);

            int expected = StripVertexCount(segments);
            if (mesh.vertices.Length != expected)
            {
                System.Array.Resize(ref mesh.vertices, expected);
            }

            if (mesh.verticeColors.Length != expected)
            {
                System.Array.Resize(ref mesh.verticeColors, expected);
            }

            return mesh;
        }

        public static void SetRing(TriangleMesh mesh, int ring, Vector2 back, Vector2 forward)
        {
            int count = mesh.vertices.Length;
            int backIndex = RingBackIndex(ring);
            int forwardIndex = RingForwardIndex(ring);

            if (backIndex >= 0 && backIndex < count)
            {
                mesh.MoveVertice(backIndex, back);
            }

            if (forwardIndex >= 0 && forwardIndex < count)
            {
                mesh.MoveVertice(forwardIndex, forward);
            }
        }
    }
}