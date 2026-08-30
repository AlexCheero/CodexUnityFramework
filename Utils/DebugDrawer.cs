#if UNITY_EDITOR
using UnityEngine;

namespace CodexFramework.Utils
{
    public static class DebugDrawer
    {
        // Sphere with radius of 1
        private static readonly Vector4[] s_UnitSphere = MakeUnitSphere(16);

        private static Vector4[] MakeUnitSphere(int len)
        {
            Debug.Assert(len > 2);
            var v = new Vector4[len * 3];
            for (int i = 0; i < len; i++)
            {
                var f = i / (float)len;
                float c = Mathf.Cos(f * (float)(Mathf.PI * 2.0));
                float s = Mathf.Sin(f * (float)(Mathf.PI * 2.0));
                v[0 * len + i] = new Vector4(c, s, 0, 1);
                v[1 * len + i] = new Vector4(0, c, s, 1);
                v[2 * len + i] = new Vector4(s, 0, c, 1);
            }
            return v;
        }

        public static void DrawRayWithOrigin(Vector3 pos, Vector3 dir, Color color, float duration = 0.0f, float originRadius = 0.3f)
        {
            DrawDebugSphere(pos, originRadius, color, duration);
            Debug.DrawRay(pos, dir, color, duration);
        }
        
        public static void DrawDebugSphere(Vector3 pos, float radius, Color color, float duration = 0.0f)
            => DrawDebugSphere(new Vector4(pos.x, pos.y, pos.z), radius, color, duration);
        public static void DrawDebugSphere(Vector4 pos, float radius, Color color, float duration = 0.0f)
        {
            Vector4[] v = s_UnitSphere;
            int len = s_UnitSphere.Length / 3;
            for (int i = 0; i < len; i++)
            {
                var sX = pos + radius * v[0 * len + i];
                var eX = pos + radius * v[0 * len + (i + 1) % len];
                var sY = pos + radius * v[1 * len + i];
                var eY = pos + radius * v[1 * len + (i + 1) % len];
                var sZ = pos + radius * v[2 * len + i];
                var eZ = pos + radius * v[2 * len + (i + 1) % len];
                Debug.DrawLine(sX, eX, color, duration);
                Debug.DrawLine(sY, eY, color, duration);
                Debug.DrawLine(sZ, eZ, color, duration);
            }
        }

        public static void DrawDebugCube(Vector3 position, float dimension, float duration = 0.0f) => DrawDebugCube(position, dimension, Color.green, duration);
        public static void DrawDebugCube(Vector3 position, float dimension, Color color, float duration = 0.0f)
            => DrawDebugCuboid(position, new Vector3(dimension, dimension, dimension), color, duration);
        
        public static void DrawDebugCuboid(Vector3 position, Vector3 dimensions, float duration = 0.0f)
            => DrawDebugCuboid(position, dimensions, Color.green, duration);
        public static void DrawDebugCuboid(Vector3 position, Vector3 dimensions, Color color, float duration = 0.0f)
        {
            Debug.DrawLine(position, position + Vector3.up*dimensions.y, color, duration);
            Debug.DrawLine(position, position + Vector3.right*dimensions.x, color, duration);
            Debug.DrawLine(position, position + Vector3.forward*dimensions.z, color, duration);

            var nextPos = position + Vector3.up * dimensions.y;
            Debug.DrawLine(nextPos, nextPos + Vector3.right*dimensions.x, color, duration);
            Debug.DrawLine(nextPos, nextPos + Vector3.forward*dimensions.z, color, duration);

            nextPos = position + Vector3.right * dimensions.x;
            Debug.DrawLine(nextPos, nextPos + Vector3.up*dimensions.y, color, duration);
            Debug.DrawLine(nextPos, nextPos + Vector3.forward*dimensions.z, color, duration);
                
            nextPos = position + Vector3.forward*dimensions.z;
            Debug.DrawLine(nextPos, nextPos + Vector3.up*dimensions.y, color, duration);
            Debug.DrawLine(nextPos, nextPos + Vector3.right*dimensions.x, color, duration);
                
            nextPos = position + dimensions;
            Debug.DrawLine(nextPos, nextPos - Vector3.up*dimensions.y, color, duration);
            Debug.DrawLine(nextPos, nextPos - Vector3.right*dimensions.x, color, duration);
            Debug.DrawLine(nextPos, nextPos - Vector3.forward*dimensions.z, color, duration);
        }

        public static void DrawCapsule(Vector3 p0, Vector3 p1, float radius, Color color, Vector3 up, float duration = 0f)
        {
            Vector3 axis = p1 - p0;
            float axisLen = axis.magnitude;

            // Build two vectors perpendicular to the capsule axis
            Vector3 axisDir = axisLen > 1e-6f ? axis / axisLen : up;
            Vector3 perp1 = Mathf.Abs(Vector3.Dot(axisDir, up)) < 0.99f
                ? Vector3.Cross(axisDir, up).normalized
                : Vector3.Cross(axisDir, Vector3.right).normalized;
            Vector3 perp2 = Vector3.Cross(axisDir, perp1);

            const int segments = 16;
            const float step = Mathf.PI * 2f / segments;

            // -- Cylinder lines (4 vertical lines along the sides) --
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f;
                Vector3 offset = (Mathf.Cos(angle) * perp1 + Mathf.Sin(angle) * perp2) * radius;
                Debug.DrawLine(p0 + offset, p1 + offset, color, duration);
            }

            // -- Circles at p0 and p1 (capsule waist rings) --
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * step;
                float a1 = (i + 1) * step;
                Vector3 off0 = (Mathf.Cos(a0) * perp1 + Mathf.Sin(a0) * perp2) * radius;
                Vector3 off1 = (Mathf.Cos(a1) * perp1 + Mathf.Sin(a1) * perp2) * radius;
                Debug.DrawLine(p0 + off0, p0 + off1, color, duration);
                Debug.DrawLine(p1 + off0, p1 + off1, color, duration);
            }

            // -- Hemisphere arcs at p0 (bottom) and p1 (top) --
            // Two perpendicular arcs per hemisphere give a good 3D impression
            const int hemiSegments = 8; // half circle = 8 segments
            const float hemiStep = Mathf.PI / hemiSegments;

            for (int i = 0; i < hemiSegments; i++)
            {
                float a0 = i * hemiStep;
                float a1 = (i + 1) * hemiStep;

                // Arc in the perp1/axis plane — bottom hemisphere goes opposite to axis
                Vector3 b0_p1 = (Mathf.Cos(a0) * perp1 - Mathf.Sin(a0) * axisDir) * radius;
                Vector3 b1_p1 = (Mathf.Cos(a1) * perp1 - Mathf.Sin(a1) * axisDir) * radius;
                Debug.DrawLine(p0 + b0_p1, p0 + b1_p1, color, duration);

                // Arc in the perp2/axis plane — bottom hemisphere
                Vector3 b0_p2 = (Mathf.Cos(a0) * perp2 - Mathf.Sin(a0) * axisDir) * radius;
                Vector3 b1_p2 = (Mathf.Cos(a1) * perp2 - Mathf.Sin(a1) * axisDir) * radius;
                Debug.DrawLine(p0 + b0_p2, p0 + b1_p2, color, duration);

                // Top hemisphere arcs go along axis direction
                Vector3 t0_p1 = (Mathf.Cos(a0) * perp1 + Mathf.Sin(a0) * axisDir) * radius;
                Vector3 t1_p1 = (Mathf.Cos(a1) * perp1 + Mathf.Sin(a1) * axisDir) * radius;
                Debug.DrawLine(p1 + t0_p1, p1 + t1_p1, color, duration);

                Vector3 t0_p2 = (Mathf.Cos(a0) * perp2 + Mathf.Sin(a0) * axisDir) * radius;
                Vector3 t1_p2 = (Mathf.Cos(a1) * perp2 + Mathf.Sin(a1) * axisDir) * radius;
                Debug.DrawLine(p1 + t0_p2, p1 + t1_p2, color, duration);
            }
        }
    }
}
#endif