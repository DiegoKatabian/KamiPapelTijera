using System;

namespace Kami.Paper
{
    public enum FoldKind
    {
        /// <summary>Thin side facing the camera (sprites, flat props): hinges on its base line and falls away from the camera.</summary>
        Card = 0,
        /// <summary>A 3D prop: hinges on its outer base edge (parallel to the spine) and falls outward, then squashes.</summary>
        Volume = 1,
    }

    /// <summary>
    /// The lowest height of another leaf over bins of outward distance (spec 025 D28): what a piece standing on its own
    /// leaf must stay under. Mirrors paper_core.height_table / table_lookup.
    /// </summary>
    public class HeightTable
    {
        readonly double[] _min;
        public double X0 { get; private set; }
        public double X1 { get; private set; }
        public int Bins => _min.Length;

        public HeightTable(int bins)
        {
            _min = new double[bins];
        }

        /// <summary>Fills the table from a polyline (canonical x, y); outward = sign * x.</summary>
        public void Build(V2[] polyline, int count, int sign, double x0, double x1)
        {
            X0 = x0;
            X1 = x1;
            int bins = _min.Length;
            for (int i = 0; i < bins; i++)
            {
                _min[i] = double.PositiveInfinity;
            }
            double width = (x1 - x0) / bins;
            for (int k = 0; k < count - 1; k++)
            {
                V2 a = polyline[k], b = polyline[k + 1];
                double xa = sign * a.x, xb = sign * b.x;
                double lo = Math.Min(xa, xb), hi = Math.Max(xa, xb);
                int i0 = (int)Math.Floor((lo - x0) / width);
                int i1 = (int)Math.Floor((hi - x0) / width);
                int from = Math.Max(i0, 0), to = Math.Min(i1, bins - 1);
                for (int i = from; i <= to; i++)
                {
                    double bl = x0 + i * width, br = x0 + (i + 1) * width;
                    double m = double.PositiveInfinity;
                    double ea = Math.Max(bl, lo), eb = Math.Min(br, hi);
                    for (int e = 0; e < 2; e++)
                    {
                        double bx = e == 0 ? ea : eb;
                        double yv;
                        if (hi - lo < 1e-12)
                        {
                            yv = Math.Min(a.y, b.y);
                        }
                        else
                        {
                            double f = (bx - xa) / (xb - xa);
                            yv = a.y + f * (b.y - a.y);
                        }
                        if (yv < m)
                        {
                            m = yv;
                        }
                    }
                    if (m < _min[i])
                    {
                        _min[i] = m;
                    }
                }
            }
        }

        public double Lookup(double xo)
        {
            int bins = _min.Length;
            int i = (int)Math.Floor((xo - X0) / (X1 - X0) * bins);
            if (i < 0 || i >= bins)
            {
                return double.PositiveInfinity;
            }
            return _min[i];
        }
    }

    /// <summary>What the solver decided for one piece this frame.</summary>
    public struct FoldResult
    {
        public double Alpha;
        public double Squash;
        public bool Fitted;
    }

    /// <summary>
    /// Folds a piece just enough to stay under the other leaf of its spread (spec 025 D27, D28). The piece is described
    /// by the 8 corners of its bounds in its own leaf's rest space: (outward distance, height, z). Mirrors
    /// paper_core.fold_corners / fits / max_squash / solve_fold.
    /// </summary>
    public static class PaperFold
    {
        public const double DefaultClearance = 0.15;
        public const double DefaultMinSquash = 0.02;
        public const int DefaultSteps = 8;
        public const int DefaultSamples = 9;

        /// <summary>Corners after folding by alpha and squashing along y toward the pivot (output array reused).</summary>
        public static void FoldCorners(V3[] corners, V3[] result, FoldKind kind, double alpha, double squash, V3 pivot, int fallSign)
        {
            double ca = Math.Cos(alpha), sa = Math.Sin(alpha);
            for (int i = 0; i < corners.Length; i++)
            {
                V3 c = corners[i];
                double x = c.x, y = c.y, z = c.z;
                if (kind == FoldKind.Card)
                {
                    double dy = y - pivot.y, dz = z - pivot.z;
                    y = pivot.y + dy * ca - fallSign * dz * sa;
                    z = pivot.z + fallSign * dy * sa + dz * ca;
                }
                else
                {
                    double dx = x - pivot.x, dy = y - pivot.y;
                    x = pivot.x + dx * ca + dy * sa;
                    y = pivot.y - dx * sa + dy * ca;
                }
                y = pivot.y + (y - pivot.y) * squash;
                result[i] = new V3(x, y, z);
            }
        }

        public static bool Fits(V3[] corners, HeightTable table, double clearance)
        {
            for (int i = 0; i < corners.Length; i++)
            {
                if (corners[i].y > table.Lookup(corners[i].x) - clearance)
                {
                    return false;
                }
            }
            return true;
        }

        static bool FitsAt(V3[] corners, V3[] scratch, FoldKind kind, double alpha, double squash, V3 pivot, int fallSign,
                           HeightTable table, double clearance)
        {
            FoldCorners(corners, scratch, kind, alpha, squash, pivot, fallSign);
            return Fits(scratch, table, clearance);
        }

        /// <summary>Largest squash that fits at a fixed fold, or -1 when even minSquash does not.</summary>
        public static double MaxSquash(V3[] corners, V3[] scratch, FoldKind kind, double alpha, V3 pivot, int fallSign,
                                       HeightTable table, double clearance, double minSquash, int steps)
        {
            if (FitsAt(corners, scratch, kind, alpha, 1.0, pivot, fallSign, table, clearance))
            {
                return 1.0;
            }
            if (!FitsAt(corners, scratch, kind, alpha, minSquash, pivot, fallSign, table, clearance))
            {
                return -1;
            }
            double lo = minSquash, hi = 1.0;
            for (int s = 0; s < steps; s++)
            {
                double mid = 0.5 * (lo + hi);
                if (FitsAt(corners, scratch, kind, alpha, mid, pivot, fallSign, table, clearance))
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }
            return lo;
        }

        /// <summary>
        /// The smallest fold that keeps every corner under the table; if no fold does, the fold allowing the largest
        /// squash. Folding is scanned at `samples` angles then refined, because a wide volume gets taller as it tips.
        /// `scratch` must be as long as `corners`.
        /// </summary>
        public static FoldResult Solve(V3[] corners, V3[] scratch, FoldKind kind, V3 pivot, int fallSign, HeightTable table,
                                       double clearance = DefaultClearance, double maxAlpha = Math.PI / 2,
                                       double minSquash = DefaultMinSquash, int steps = DefaultSteps, int samples = DefaultSamples)
        {
            double prev = double.NaN;
            for (int i = 0; i < samples; i++)
            {
                double a = maxAlpha * i / (samples - 1);
                if (FitsAt(corners, scratch, kind, a, 1.0, pivot, fallSign, table, clearance))
                {
                    if (double.IsNaN(prev))
                    {
                        return new FoldResult { Alpha = a, Squash = 1, Fitted = true };
                    }
                    double lo = prev, hi = a;
                    for (int s = 0; s < steps; s++)
                    {
                        double mid = 0.5 * (lo + hi);
                        if (FitsAt(corners, scratch, kind, mid, 1.0, pivot, fallSign, table, clearance))
                        {
                            hi = mid;
                        }
                        else
                        {
                            lo = mid;
                        }
                    }
                    return new FoldResult { Alpha = hi, Squash = 1, Fitted = true };
                }
                prev = a;
            }
            double bestAlpha = 0, bestSquash = -1;
            for (int i = 0; i < samples; i++)
            {
                double a = maxAlpha * i / (samples - 1);
                double sq = MaxSquash(corners, scratch, kind, a, pivot, fallSign, table, clearance, minSquash, steps);
                if (sq >= 0 && (bestSquash < 0 || sq > bestSquash + 1e-9))
                {
                    bestAlpha = a;
                    bestSquash = sq;
                }
            }
            if (bestSquash < 0)
            {
                return new FoldResult { Alpha = maxAlpha, Squash = minSquash, Fitted = false };
            }
            return new FoldResult { Alpha = bestAlpha, Squash = bestSquash, Fitted = true };
        }
    }
}
