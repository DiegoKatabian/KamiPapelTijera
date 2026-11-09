using System;

namespace Kami.Paper
{
    /// <summary>How the procedural sheet moves (spec 025 D23). Fitted to the old clip by tools/fit-paper-curl.py.</summary>
    [Serializable]
    public class CurlParams
    {
        /// <summary>How far the part at the spine runs ahead of the edge (0 = the sheet turns as a rigid board).</summary>
        public double lead = 0.25;
        /// <summary>0 = linear time, 1 = smoothstep time.</summary>
        public double ease = 0;
        /// <summary>How the edge's lag grows from the spine: lag ~ (u / L)^shape.</summary>
        public double shape = 1.8;
        /// <summary>Seconds the sheet takes from lifting to landing.</summary>
        public double duration = 3.3333;
        /// <summary>1 = a real sheet. Below 1 squashes the curl's height (the old sheet's look); pieces follow it.</summary>
        public double heightScale = 1;

        public double Eased(double t)
        {
            t = PaperMath.Clamp01(t);
            return (1 - ease) * t + ease * PaperMath.Smoothstep(t);
        }
    }

    /// <summary>
    /// The turning sheet in canonical book space (the turn's start side is +x, the end side -x): an inextensible strip
    /// of Segments pieces from the spine to the edge, lying on the start half's profile at t 0 and on the end half's at
    /// t 1. Each segment's angle blends from the start surface to the end surface, the part at the spine first.
    /// Mirrors paper_core.Curl.
    /// </summary>
    public class PaperCurl
    {
        public const int Segments = 64;

        readonly double[] _phiStart = new double[Segments];
        readonly double[] _phiEnd = new double[Segments];
        readonly CurlParams _p;

        public double Length { get; }
        public double SegmentLength { get; }
        public V2 Origin { get; }
        public PaperProfile Start { get; }
        public PaperProfile End { get; }

        public PaperCurl(PaperProfile start, PaperProfile end, CurlParams p)
        {
            Start = start ?? throw new ArgumentNullException(nameof(start));
            End = end ?? throw new ArgumentNullException(nameof(end));
            _p = p ?? new CurlParams();
            Length = Math.Min(start.Length, end.Length);
            SegmentLength = Length / Segments;
            // each segment follows the chord of the surface it rests on (a midpoint slope overshoots in the gutter)
            for (int i = 0; i < Segments; i++)
            {
                _phiStart[i] = Chord(start, i);
                _phiEnd[i] = Math.PI - Chord(end, i);
            }
            Origin = new V2(0, 0.5 * (start.SpineHeight + end.SpineHeight));
        }

        double Chord(PaperProfile profile, int i)
        {
            V2 a = profile.Point(i * SegmentLength);
            V2 b = profile.Point((i + 1) * SegmentLength);
            return Math.Atan2(b.y - a.y, b.x - a.x);
        }

        /// <summary>The sheet at normalized time t: Segments + 1 points and Segments angles (arrays reused by the caller).</summary>
        public void Evaluate(double t, V2[] points, double[] angles)
        {
            if (points == null || points.Length < Segments + 1 || angles == null || angles.Length < Segments)
            {
                throw new ArgumentException("Evaluate needs Segments + 1 points and Segments angles");
            }
            double e = _p.Eased(t);
            double k = _p.lead;
            points[0] = Origin;
            double x = Origin.x, y = Origin.y;
            for (int i = 0; i < Segments; i++)
            {
                double u = (i + 0.5) / Segments;
                double w = PaperMath.Smoothstep(e * (1 + k) - k * Math.Pow(u, _p.shape));
                double a = _phiStart[i] + w * (_phiEnd[i] - _phiStart[i]);
                angles[i] = a;
                x += Math.Cos(a) * SegmentLength;
                y += Math.Sin(a) * SegmentLength;
                points[i + 1] = new V2(x, y);
            }
            if (_p.heightScale != 1)
            {
                // squash the height above the line joining the two rest surfaces' spine point, then recompute angles
                for (int i = 1; i <= Segments; i++)
                {
                    points[i] = new V2(points[i].x, Origin.y + (points[i].y - Origin.y) * _p.heightScale);
                }
                for (int i = 0; i < Segments; i++)
                {
                    angles[i] = Math.Atan2(points[i + 1].y - points[i].y, points[i + 1].x - points[i].x);
                }
            }
        }

        /// <summary>Position and tangent angle at arc length u on an evaluated sheet (clamped). Mirrors paper_core.frame.</summary>
        public void Frame(V2[] points, double[] angles, double u, out V2 position, out double angle)
        {
            double f = u / SegmentLength;
            if (f < 0)
            {
                f = 0;
            }
            if (f > Segments - 1e-9)
            {
                f = Segments - 1e-9;
            }
            int i = (int)f;
            if (i > Segments - 1)
            {
                i = Segments - 1;
            }
            double r = f - i;
            position = V2.Lerp(points[i], points[i + 1], r);
            angle = angles[i];
        }
    }
}
