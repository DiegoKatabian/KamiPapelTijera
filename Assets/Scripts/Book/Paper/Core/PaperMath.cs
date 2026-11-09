using System;

namespace Kami.Paper
{
    // The paper page core (spec 025, D38) has no UnityEngine on purpose: tools/run-paper-tests.py compiles it into a
    // plain console test and checks it against tools/paper_core.py's goldens, from outside Unity. Keep it that way:
    // every type here is plain C#, and the Unity side converts at the edges (PaperUnity.cs).

    /// <summary>A 2D point or vector in the cross-section of the book (x across the page, y up).</summary>
    public struct V2
    {
        public double x, y;

        public V2(double x, double y)
        {
            this.x = x;
            this.y = y;
        }

        public static V2 operator +(V2 a, V2 b) => new V2(a.x + b.x, a.y + b.y);
        public static V2 operator -(V2 a, V2 b) => new V2(a.x - b.x, a.y - b.y);
        public static V2 operator *(V2 a, double s) => new V2(a.x * s, a.y * s);
        public static V2 Lerp(V2 a, V2 b, double t) => new V2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);

        public override string ToString() => $"({x:F4}, {y:F4})";
    }

    /// <summary>A 3D point in book space (x across, y up, z along the spine).</summary>
    public struct V3
    {
        public double x, y, z;

        public V3(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public override string ToString() => $"({x:F4}, {y:F4}, {z:F4})";
    }

    /// <summary>
    /// A 2D rigid map x' = R x + T (a rotation by Angle, then a translation): how a point stuck to the paper moves when
    /// the paper under it moves from one frame to another.
    /// </summary>
    public struct Rigid2
    {
        public double Angle;
        public V2 T;

        public static Rigid2 Identity => new Rigid2 { Angle = 0, T = new V2(0, 0) };

        /// <summary>The map taking the frame (fromPoint, fromAngle) onto (toPoint, toAngle).</summary>
        public static Rigid2 Between(V2 fromPoint, double fromAngle, V2 toPoint, double toAngle)
        {
            Rigid2 r = new Rigid2 { Angle = toAngle - fromAngle };
            V2 rotated = r.Rotate(fromPoint);
            r.T = toPoint - rotated;
            return r;
        }

        public V2 Rotate(V2 p)
        {
            double c = Math.Cos(Angle), s = Math.Sin(Angle);
            return new V2(c * p.x - s * p.y, s * p.x + c * p.y);
        }

        public V2 Apply(V2 p) => Rotate(p) + T;

        public Rigid2 Inverse()
        {
            Rigid2 inv = new Rigid2 { Angle = -Angle };
            inv.T = inv.Rotate(T) * -1.0;
            return inv;
        }
    }

    public static class PaperMath
    {
        public static double Clamp01(double x) => x < 0 ? 0 : (x > 1 ? 1 : x);

        public static double Smoothstep(double x)
        {
            x = Clamp01(x);
            return x * x * (3 - 2 * x);
        }
    }
}
