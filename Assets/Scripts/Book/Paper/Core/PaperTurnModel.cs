using System;

namespace Kami.Paper
{
    /// <summary>
    /// What a page piece lies on during a turn (spec 025 D24, D25). Canonical book space: the turn's start side is +x.
    /// The page being left has its start-side half on the sheet's front and its end-side half on the static end half;
    /// the page arriving has its end-side half on the sheet's back and its start-side half on the static start half.
    /// </summary>
    public enum LeafKind
    {
        /// <summary>Arriving page, start half: stays, unfolds as the sheet lifts off it.</summary>
        StartStatic = 0,
        /// <summary>Leaving page, end half: stays, folds as the sheet comes down on it.</summary>
        EndStatic = 1,
        /// <summary>Leaving page, start half: rides the sheet's front (rest = the sheet at t 0), folds as it lands.</summary>
        SheetFront = 2,
        /// <summary>Arriving page, end half: rides the sheet's back (rest = the sheet at t 1), unfolds as it lands.</summary>
        SheetBack = 3,
    }

    /// <summary>A 3D affine map: x' = M x + T (row-major 3x3).</summary>
    public struct Affine3
    {
        public double m00, m01, m02, m10, m11, m12, m20, m21, m22;
        public double t0, t1, t2;

        public static Affine3 Identity => new Affine3 { m00 = 1, m11 = 1, m22 = 1 };

        public static Affine3 operator *(Affine3 a, Affine3 b)
        {
            return new Affine3
            {
                m00 = a.m00 * b.m00 + a.m01 * b.m10 + a.m02 * b.m20,
                m01 = a.m00 * b.m01 + a.m01 * b.m11 + a.m02 * b.m21,
                m02 = a.m00 * b.m02 + a.m01 * b.m12 + a.m02 * b.m22,
                m10 = a.m10 * b.m00 + a.m11 * b.m10 + a.m12 * b.m20,
                m11 = a.m10 * b.m01 + a.m11 * b.m11 + a.m12 * b.m21,
                m12 = a.m10 * b.m02 + a.m11 * b.m12 + a.m12 * b.m22,
                m20 = a.m20 * b.m00 + a.m21 * b.m10 + a.m22 * b.m20,
                m21 = a.m20 * b.m01 + a.m21 * b.m11 + a.m22 * b.m21,
                m22 = a.m20 * b.m02 + a.m21 * b.m12 + a.m22 * b.m22,
                t0 = a.m00 * b.t0 + a.m01 * b.t1 + a.m02 * b.t2 + a.t0,
                t1 = a.m10 * b.t0 + a.m11 * b.t1 + a.m12 * b.t2 + a.t1,
                t2 = a.m20 * b.t0 + a.m21 * b.t1 + a.m22 * b.t2 + a.t2,
            };
        }

        public V3 Apply(V3 p)
        {
            return new V3(m00 * p.x + m01 * p.y + m02 * p.z + t0,
                          m10 * p.x + m11 * p.y + m12 * p.z + t1,
                          m20 * p.x + m21 * p.y + m22 * p.z + t2);
        }

        public static Affine3 Translate(double x, double y, double z) => new Affine3 { m00 = 1, m11 = 1, m22 = 1, t0 = x, t1 = y, t2 = z };
        public static Affine3 Scale(double x, double y, double z) => new Affine3 { m00 = x, m11 = y, m22 = z };

        public static Affine3 RotZ(double a)
        {
            double c = Math.Cos(a), s = Math.Sin(a);
            return new Affine3 { m00 = c, m01 = -s, m10 = s, m11 = c, m22 = 1 };
        }

        public static Affine3 RotX(double a)
        {
            double c = Math.Cos(a), s = Math.Sin(a);
            return new Affine3 { m00 = 1, m11 = c, m12 = -s, m21 = s, m22 = c };
        }
    }

    /// <summary>One piece's rest data, in canonical book space (built once per turn by the Unity side).</summary>
    public class PaperPieceRest
    {
        public FoldKind Kind;
        public LeafKind Leaf;
        /// <summary>The 8 corners of the piece's bounds, canonical (x, y, z).</summary>
        public V3[] Corners = new V3[8];
        /// <summary>False = the piece only rides its leaf, never folds (Flat pieces, Riders use their host's map).</summary>
        public bool Folds = true;

        // filled by PaperTurnModel.Prepare
        internal int Sign;
        /// <summary>The points the fold solver tests: the box's outline sampled along its edges, outward space.</summary>
        internal V3[] Outward = new V3[8];
        internal V3[] Scratch = new V3[8];
        internal V3 Pivot;
        internal double HingeU;
        internal double Alpha = double.NaN;
        internal double Squash = double.NaN;
        internal bool Closing;
        public FoldResult Last;
    }

    /// <summary>
    /// A turn's math (spec 025 D23-D28): the sheet at time t, the leaf map of each piece, the other leaf it must stay
    /// under, and its fold. Mirrors paper_core.TurnModel. No UnityEngine.
    /// </summary>
    public class PaperTurnModel
    {
        public const int TableBins = 128;
        public const double TableMargin = 4;

        readonly PaperCurl _curl;
        readonly V2[] _restFront = new V2[PaperCurl.Segments + 1];
        readonly double[] _restFrontAng = new double[PaperCurl.Segments];
        readonly V2[] _restBack = new V2[PaperCurl.Segments + 1];
        readonly double[] _restBackAng = new double[PaperCurl.Segments];
        readonly V2[] _sheet = new V2[PaperCurl.Segments + 1];
        readonly double[] _sheetAng = new double[PaperCurl.Segments];
        readonly V2[] _startSurface;
        readonly V2[] _endSurface;
        readonly V2[] _mapped;
        readonly HeightTable _table = new HeightTable(TableBins);
        readonly HeightTable _startStaticTable = new HeightTable(TableBins);
        readonly HeightTable _endStaticTable = new HeightTable(TableBins);
        bool _startStaticBuilt, _endStaticBuilt;
        readonly int _fallSign;
        double _tableX1;

        public double T { get; private set; }
        public PaperCurl Curl => _curl;
        public V2[] SheetPoints => _sheet;
        public double[] SheetAngles => _sheetAng;
        /// <summary>The sheet lying on the start half (t 0): where the front face's pieces and picture were.</summary>
        public V2[] RestFrontPoints => _restFront;
        /// <summary>The sheet lying on the end half (t 1): where the back face's pieces and picture end.</summary>
        public V2[] RestBackPoints => _restBack;
        public double Clearance = PaperFold.DefaultClearance;
        /// <summary>
        /// Per LeafKind: how far the pieces standing on that leaf push the facing leaf's pieces away (spec 025 D28). Pages
        /// facing each other close on each other's pop-ups, not just on paper; 0 = only the paper counts.
        /// </summary>
        public readonly double[] LeafThickness = new double[4];
        readonly V2[] _offsetSheet = new V2[PaperCurl.Segments + 1];

        /// <param name="fallSign">+1 = cards fall toward +z (away from a camera looking along +z).</param>
        public PaperTurnModel(PaperCurl curl, int fallSign)
        {
            _curl = curl ?? throw new ArgumentNullException(nameof(curl));
            _fallSign = fallSign >= 0 ? 1 : -1;
            curl.Evaluate(0, _restFront, _restFrontAng);
            curl.Evaluate(1, _restBack, _restBackAng);
            _startSurface = SurfacePolyline(curl.Start, 1);
            _endSurface = SurfacePolyline(curl.End, -1);
            _mapped = new V2[Math.Max(_startSurface.Length, _endSurface.Length)];
            _tableX1 = Math.Max(curl.Start.OuterX, curl.End.OuterX) + TableMargin;
            SetTime(0);
        }

        static V2[] SurfacePolyline(PaperProfile p, int sign)
        {
            const int n = 65;
            V2[] pts = new V2[n];
            for (int i = 0; i < n; i++)
            {
                V2 q = p.Point(p.Length * i / (n - 1));
                pts[i] = new V2(sign * q.x, q.y);
            }
            return pts;
        }

        public void SetTime(double t)
        {
            T = PaperMath.Clamp01(t);
            _curl.Evaluate(T, _sheet, _sheetAng);
            _startStaticBuilt = false;
            _endStaticBuilt = false;
        }

        static int SignOf(LeafKind leaf) => (leaf == LeafKind.StartStatic || leaf == LeafKind.SheetFront) ? 1 : -1;
        static bool IsClosing(LeafKind leaf) => leaf == LeafKind.EndStatic || leaf == LeafKind.SheetFront;

        /// <summary>Spacing of the points tested along a piece's box edges, and the most segments per edge.</summary>
        public const double SampleSpacing = 1.5;
        public const int MaxEdgeSegments = 16;
        /// <summary>Outward distance where a piece's own half starts for the fold test (past the spine's gutter bin).</summary>
        public const double OwnHalfFrom = 0.5;

        /// <summary>Computes the piece's outward test points, pivot and hinge position. Call once per turn per piece.</summary>
        public void Prepare(PaperPieceRest piece)
        {
            piece.Sign = SignOf(piece.Leaf);
            piece.Closing = IsClosing(piece.Leaf);
            double minY = double.PositiveInfinity, maxXo = double.NegativeInfinity, sumXo = 0, sumZ = 0;
            double minXo = double.PositiveInfinity, maxY = double.NegativeInfinity;
            double minZ = double.PositiveInfinity, maxZ = double.NegativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                V3 c = piece.Corners[i];
                V3 o = new V3(piece.Sign * c.x, c.y, c.z);
                minY = Math.Min(minY, o.y);
                maxY = Math.Max(maxY, o.y);
                maxXo = Math.Max(maxXo, o.x);
                minXo = Math.Min(minXo, o.x);
                minZ = Math.Min(minZ, o.z);
                maxZ = Math.Max(maxZ, o.z);
                sumXo += o.x;
                sumZ += o.z;
            }
            // the box's corners alone let the sheet pass through the middle of a wide piece (a building spanning the
            // spine has corners on both sides of the curl): test its outline along every edge too
            // only the part on the piece's own half: across the spine the sheet lands on it at the end of every turn,
            // so a piece spanning the spine could never stand up again (and would jump when the real one comes back)
            double testX0 = maxXo > OwnHalfFrom ? Math.Max(minXo, OwnHalfFrom) : minXo;
            piece.Outward = SampleOutline(testX0, maxXo, minY, maxY, minZ, maxZ);
            piece.Scratch = new V3[piece.Outward.Length];
            piece.Pivot = piece.Kind == FoldKind.Card
                ? new V3(sumXo / 8, minY, sumZ / 8)
                : new V3(maxXo, minY, sumZ / 8);
            PaperProfile rest = piece.Sign > 0 ? _curl.Start : _curl.End;
            piece.HingeU = rest.UAt(Math.Max(piece.Pivot.x, 0));
            piece.Alpha = double.NaN;
            piece.Squash = double.NaN;
        }

        static int Segments(double length) => Math.Max(1, Math.Min(MaxEdgeSegments, (int)Math.Ceiling(length / SampleSpacing)));

        /// <summary>
        /// The outline of a box's x-y face at both z ends: its bottom and top edges at every step across, its two sides
        /// at every step up. Mirrors paper_core.sample_outline (same order).
        /// </summary>
        public static V3[] SampleOutline(double x0, double x1, double y0, double y1, double z0, double z1)
        {
            int nx = Segments(x1 - x0), ny = Segments(y1 - y0);
            V3[] pts = new V3[2 * (2 * (nx + 1) + 2 * (ny - 1))];
            int k = 0;
            for (int zi = 0; zi < 2; zi++)
            {
                double z = zi == 0 ? z0 : z1;
                for (int i = 0; i <= nx; i++)
                {
                    double x = x0 + (x1 - x0) * i / nx;
                    pts[k++] = new V3(x, y0, z);
                    pts[k++] = new V3(x, y1, z);
                }
                for (int j = 1; j < ny; j++)
                {
                    double y = y0 + (y1 - y0) * j / ny;
                    pts[k++] = new V3(x0, y, z);
                    pts[k++] = new V3(x1, y, z);
                }
            }
            return pts;
        }

        /// <summary>The 2D map from the piece's rest leaf to where that leaf is now.</summary>
        public Rigid2 LeafDelta(PaperPieceRest piece)
        {
            if (piece.Leaf == LeafKind.StartStatic || piece.Leaf == LeafKind.EndStatic)
            {
                return Rigid2.Identity;
            }
            bool front = piece.Leaf == LeafKind.SheetFront;
            _curl.Frame(front ? _restFront : _restBack, front ? _restFrontAng : _restBackAng, piece.HingeU, out V2 rp, out double ra);
            _curl.Frame(_sheet, _sheetAng, piece.HingeU, out V2 np, out double na);
            return Rigid2.Between(rp, ra, np, na);
        }

        /// <summary>Builds the height table of the other leaf of the piece's spread, in the piece's rest space.</summary>
        HeightTable OtherLeafTable(PaperPieceRest piece, Rigid2 delta)
        {
            switch (piece.Leaf)
            {
                case LeafKind.StartStatic:
                    // every static piece of a side shares one table per frame; the sheet's back faces them
                    if (!_startStaticBuilt)
                    {
                        OffsetSheet(-LeafThickness[(int)LeafKind.SheetBack]);
                        _startStaticTable.Build(_offsetSheet, _offsetSheet.Length, 1, -TableMargin, _tableX1);
                        _startStaticBuilt = true;
                    }
                    return _startStaticTable;
                case LeafKind.EndStatic:
                    // the sheet's front faces them
                    if (!_endStaticBuilt)
                    {
                        OffsetSheet(LeafThickness[(int)LeafKind.SheetFront]);
                        _endStaticTable.Build(_offsetSheet, _offsetSheet.Length, -1, -TableMargin, _tableX1);
                        _endStaticBuilt = true;
                    }
                    return _endStaticTable;
                default:
                    bool front = piece.Leaf == LeafKind.SheetFront;
                    V2[] other = front ? _endSurface : _startSurface;
                    double lift = LeafThickness[(int)(front ? LeafKind.EndStatic : LeafKind.StartStatic)];
                    Rigid2 inv = delta.Inverse();
                    for (int i = 0; i < other.Length; i++)
                    {
                        _mapped[i] = inv.Apply(new V2(other[i].x, other[i].y + lift));
                    }
                    _table.Build(_mapped, other.Length, piece.Sign, -TableMargin, _tableX1);
                    break;
            }
            return _table;
        }

        /// <summary>The sheet moved along its front normal (-sin a, cos a) by `distance` (negative = toward its back).</summary>
        void OffsetSheet(double distance)
        {
            for (int i = 0; i < _sheet.Length; i++)
            {
                double a = _sheetAng[i < _sheetAng.Length ? i : _sheetAng.Length - 1];
                _offsetSheet[i] = new V2(_sheet[i].x - Math.Sin(a) * distance, _sheet[i].y + Math.Cos(a) * distance);
            }
        }

        /// <summary>
        /// The piece's map at the current time, canonical rest -> canonical now: its leaf's move after its fold.
        /// Closing pieces never unfold and opening pieces never fold back within a turn (no jitter).
        /// </summary>
        public Affine3 Solve(PaperPieceRest piece)
        {
            Rigid2 delta = LeafDelta(piece);
            double alpha = 0, squash = 1;
            if (piece.Folds)
            {
                HeightTable table = OtherLeafTable(piece, delta);
                FoldResult r = PaperFold.Solve(piece.Outward, piece.Scratch, piece.Kind, piece.Pivot, _fallSign, table, Clearance);
                alpha = r.Alpha;
                squash = r.Squash;
                if (!double.IsNaN(piece.Alpha))
                {
                    if (piece.Closing)
                    {
                        alpha = Math.Max(alpha, piece.Alpha);
                        squash = Math.Min(squash, piece.Squash);
                    }
                    else
                    {
                        alpha = Math.Min(alpha, piece.Alpha);
                        squash = Math.Max(squash, piece.Squash);
                    }
                }
                piece.Alpha = alpha;
                piece.Squash = squash;
                piece.Last = new FoldResult { Alpha = alpha, Squash = squash, Fitted = r.Fitted };
            }
            return Compose(piece, delta, alpha, squash);
        }

        /// <summary>D * O * Squash * Rot * O, where O mirrors x into the piece's outward space.</summary>
        public Affine3 Compose(PaperPieceRest piece, Rigid2 delta, double alpha, double squash)
        {
            Affine3 o = Affine3.Scale(piece.Sign, 1, 1);
            V3 p = piece.Pivot;
            Affine3 rot;
            if (piece.Kind == FoldKind.Card)
            {
                // y' = py + dy cos - f dz sin ; z' = pz + f dy sin + dz cos  == RotX(f * alpha) about the pivot line
                rot = Affine3.Translate(0, p.y, p.z) * Affine3.RotX(_fallSign * alpha) * Affine3.Translate(0, -p.y, -p.z);
            }
            else
            {
                // x' = px + dx cos + dy sin ; y' = py - dx sin + dy cos == RotZ(-alpha) about the pivot line
                rot = Affine3.Translate(p.x, p.y, 0) * Affine3.RotZ(-alpha) * Affine3.Translate(-p.x, -p.y, 0);
            }
            Affine3 sq = Affine3.Translate(0, p.y, 0) * Affine3.Scale(1, squash, 1) * Affine3.Translate(0, -p.y, 0);
            Affine3 d = Affine3.RotZ(delta.Angle);
            d.t0 = delta.T.x;
            d.t1 = delta.T.y;
            return d * o * sq * rot * o;
        }
    }
}
