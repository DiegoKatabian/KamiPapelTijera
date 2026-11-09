using System;

namespace Kami.Paper
{
    /// <summary>
    /// One half of the open book's top surface, as points (outward distance from the spine, height), parametrised by arc
    /// length from the spine. Measured by tools/measure-paper-book.py. Mirrors paper_core.Profile.
    /// </summary>
    public class PaperProfile
    {
        readonly double[] _xo;
        readonly double[] _y;
        readonly double[] _s;
        readonly double[] _angle;

        public double Length { get; }
        public double OuterX => _xo[_xo.Length - 1];
        public double SpineHeight => _y[0];

        public PaperProfile(double[] outward, double[] height)
        {
            if (outward == null || height == null || outward.Length < 2 || outward.Length != height.Length)
            {
                throw new ArgumentException("a profile needs at least two (outward, height) points");
            }
            _xo = (double[])outward.Clone();
            _y = (double[])height.Clone();
            int n = _xo.Length;
            _s = new double[n];
            _angle = new double[n - 1];
            for (int i = 1; i < n; i++)
            {
                double dx = _xo[i] - _xo[i - 1], dy = _y[i] - _y[i - 1];
                _s[i] = _s[i - 1] + Math.Sqrt(dx * dx + dy * dy);
                _angle[i - 1] = Math.Atan2(dy, dx);
            }
            Length = _s[n - 1];
        }

        int SegmentAt(double u)
        {
            // last i with s[i] <= u, clamped to a valid segment (same as numpy searchsorted(side='right') - 1)
            int lo = 0, hi = _s.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (_s[mid] <= u)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }
            int i = lo - 1;
            if (i < 0)
            {
                i = 0;
            }
            if (i > _angle.Length - 1)
            {
                i = _angle.Length - 1;
            }
            return i;
        }

        /// <summary>Slope angle of the surface at arc length u, relative to the outward direction.</summary>
        public double Angle(double u) => _angle[SegmentAt(u)];

        /// <summary>(outward distance, height) at arc length u.</summary>
        public V2 Point(double u)
        {
            int i = SegmentAt(u);
            double f = (u - _s[i]) / Math.Max(_s[i + 1] - _s[i], 1e-12);
            return new V2(_xo[i] + f * (_xo[i + 1] - _xo[i]), _y[i] + f * (_y[i + 1] - _y[i]));
        }

        /// <summary>Arc length at outward distance xo, clamped to the profile.</summary>
        public double UAt(double xo)
        {
            xo = Math.Min(Math.Max(xo, _xo[0]), _xo[_xo.Length - 1]);
            int i = LastAtOrBelow(_xo, xo);
            double f = (xo - _xo[i]) / Math.Max(_xo[i + 1] - _xo[i], 1e-12);
            return _s[i] + f * (_s[i + 1] - _s[i]);
        }

        /// <summary>Surface height at outward distance xo, clamped to the profile.</summary>
        public double Height(double xo)
        {
            xo = Math.Min(Math.Max(xo, _xo[0]), _xo[_xo.Length - 1]);
            int i = LastAtOrBelow(_xo, xo);
            double f = (xo - _xo[i]) / Math.Max(_xo[i + 1] - _xo[i], 1e-12);
            return _y[i] + f * (_y[i + 1] - _y[i]);
        }

        static int LastAtOrBelow(double[] a, double v)
        {
            int lo = 0, hi = a.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (a[mid] <= v)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }
            int i = lo - 1;
            if (i < 0)
            {
                i = 0;
            }
            if (i > a.Length - 2)
            {
                i = a.Length - 2;
            }
            return i;
        }
    }
}
