using System;
using System.Collections.Generic;
using System.Reflection;

// Tests of the paper page core (Assets/Scripts/Book/Paper/Core/) against the Python reference's goldens (spec 025 D38).
// Compiled and run outside Unity by tools/run-paper-tests.py. Every public static void method whose name starts with
// "Test" runs; a failure throws.
namespace Kami.Paper.Tests
{
    public static class PaperCoreTests
    {
        const double Tol = 1e-6;

        static PaperProfile Start() => new PaperProfile(Goldens.StartX, Goldens.StartY);
        static PaperProfile End() => new PaperProfile(Goldens.EndX, Goldens.EndY);
        static CurlParams Params() => new CurlParams { lead = Goldens.Lead, ease = Goldens.Ease, shape = Goldens.Shape, duration = Goldens.Duration };

        static void Near(double expected, double actual, string what, double tol = Tol)
        {
            if (double.IsInfinity(expected) && double.IsInfinity(actual))
            {
                return;
            }
            if (Math.Abs(expected - actual) > tol * Math.Max(1, Math.Abs(expected)))
            {
                throw new Exception($"{what}: expected {expected:R}, got {actual:R}");
            }
        }

        public static void TestProfile()
        {
            PaperProfile p = Start();
            for (int i = 0; i < Goldens.ProfileProbes.Length; i++)
            {
                double u = Goldens.ProfileProbes[i];
                Near(Goldens.ProfileAngle[i], p.Angle(u), $"Angle({u})");
                V2 q = p.Point(u);
                Near(Goldens.ProfilePointX[i], q.x, $"Point({u}).x");
                Near(Goldens.ProfilePointY[i], q.y, $"Point({u}).y");
            }
            for (int i = 0; i < Goldens.XoProbes.Length; i++)
            {
                double x = Goldens.XoProbes[i];
                Near(Goldens.ProfileU[i], p.UAt(x), $"UAt({x})");
                Near(Goldens.ProfileHeight[i], p.Height(x), $"Height({x})");
            }
        }

        public static void TestCurlMatchesReference()
        {
            PaperCurl curl = new PaperCurl(Start(), End(), Params());
            V2[] pts = new V2[PaperCurl.Segments + 1];
            double[] ang = new double[PaperCurl.Segments];
            int n = PaperCurl.Segments + 1;
            for (int k = 0; k < Goldens.Times.Length; k++)
            {
                curl.Evaluate(Goldens.Times[k], pts, ang);
                for (int i = 0; i < n; i++)
                {
                    Near(Goldens.CurlX[k * n + i], pts[i].x, $"curl t {Goldens.Times[k]} point {i} x");
                    Near(Goldens.CurlY[k * n + i], pts[i].y, $"curl t {Goldens.Times[k]} point {i} y");
                }
                for (int i = 0; i < PaperCurl.Segments; i++)
                {
                    Near(Goldens.CurlAngle[k * PaperCurl.Segments + i], ang[i], $"curl t {Goldens.Times[k]} angle {i}");
                }
            }
        }

        public static void TestCurlNeverStretches()
        {
            PaperCurl curl = new PaperCurl(Start(), End(), Params());
            V2[] pts = new V2[PaperCurl.Segments + 1];
            double[] ang = new double[PaperCurl.Segments];
            for (int k = 0; k <= 20; k++)
            {
                curl.Evaluate(k / 20.0, pts, ang);
                double len = 0;
                for (int i = 0; i < PaperCurl.Segments; i++)
                {
                    double dx = pts[i + 1].x - pts[i].x, dy = pts[i + 1].y - pts[i].y;
                    len += Math.Sqrt(dx * dx + dy * dy);
                }
                Near(curl.Length, len, $"sheet length at t {k / 20.0}", 1e-9);
            }
        }

        public static void TestCurlRestsOnTheBook()
        {
            // at t 0 the sheet lies on the start half, at t 1 on the end half (within the surface sampling)
            PaperProfile s = Start(), e = End();
            PaperCurl curl = new PaperCurl(s, e, Params());
            V2[] pts = new V2[PaperCurl.Segments + 1];
            double[] ang = new double[PaperCurl.Segments];
            curl.Evaluate(0, pts, ang);
            foreach (V2 p in pts)
            {
                Near(s.Height(p.x), p.y, $"start rest height at x {p.x:F2}", 0.06);
            }
            curl.Evaluate(1, pts, ang);
            foreach (V2 p in pts)
            {
                Near(e.Height(-p.x), p.y, $"end rest height at x {p.x:F2}", 0.06);
            }
        }

        public static void TestRigidInverse()
        {
            Rigid2 r = Rigid2.Between(new V2(3, 1), 0.2, new V2(-4, 7), 2.1);
            V2 p = new V2(5.5, -2);
            V2 back = r.Inverse().Apply(r.Apply(p));
            Near(p.x, back.x, "inverse x");
            Near(p.y, back.y, "inverse y");
        }

        public static void TestPiecesMatchReference()
        {
            PaperCurl curl = new PaperCurl(Start(), End(), Params());
            PaperTurnModel model = new PaperTurnModel(curl, 1);
            for (int i = 0; i < 4; i++)
            {
                model.LeafThickness[i] = Goldens.Thickness[i];
            }
            int count = Goldens.PieceNames.Length;
            List<PaperPieceRest> pieces = new List<PaperPieceRest>();
            for (int i = 0; i < count; i++)
            {
                PaperPieceRest p = new PaperPieceRest { Kind = (FoldKind)Goldens.PieceKinds[i], Leaf = (LeafKind)Goldens.PieceLeaves[i] };
                for (int c = 0; c < 8; c++)
                {
                    int b = (i * 8 + c) * 3;
                    p.Corners[c] = new V3(Goldens.PieceCorners[b], Goldens.PieceCorners[b + 1], Goldens.PieceCorners[b + 2]);
                }
                model.Prepare(p);
                pieces.Add(p);
            }
            for (int k = 0; k < Goldens.Times.Length; k++)
            {
                model.SetTime(Goldens.Times[k]);
                for (int i = 0; i < count; i++)
                {
                    Affine3 m = model.Solve(pieces[i]);
                    double[] got = { m.m00, m.m01, m.m02, m.m10, m.m11, m.m12, m.m20, m.m21, m.m22, m.t0, m.t1, m.t2 };
                    int b = (k * count + i) * 12;
                    string who = $"{Goldens.PieceNames[i]} at t {Goldens.Times[k]}";
                    for (int j = 0; j < 12; j++)
                    {
                        Near(Goldens.PieceMatrices[b + j], got[j], $"{who} matrix[{j}]", 1e-5);
                    }
                    int f = (k * count + i) * 3;
                    Near(Goldens.PieceFolds[f], pieces[i].Last.Alpha, $"{who} alpha", 1e-5);
                    Near(Goldens.PieceFolds[f + 1], pieces[i].Last.Squash, $"{who} squash", 1e-5);
                }
            }
        }

        public static void TestNoPieceEndsAboveTheSheet()
        {
            // the property the whole fold rule exists for: through the turn, no corner of a static piece rises above the
            // sheet over it by more than the clearance
            PaperCurl curl = new PaperCurl(Start(), End(), Params());
            PaperTurnModel model = new PaperTurnModel(curl, 1);
            int count = Goldens.PieceNames.Length;
            for (int i = 0; i < count; i++)
            {
                LeafKind leaf = (LeafKind)Goldens.PieceLeaves[i];
                if (leaf != LeafKind.StartStatic && leaf != LeafKind.EndStatic)
                {
                    continue;
                }
                PaperPieceRest p = new PaperPieceRest { Kind = (FoldKind)Goldens.PieceKinds[i], Leaf = leaf };
                for (int c = 0; c < 8; c++)
                {
                    int b = (i * 8 + c) * 3;
                    p.Corners[c] = new V3(Goldens.PieceCorners[b], Goldens.PieceCorners[b + 1], Goldens.PieceCorners[b + 2]);
                }
                model.Prepare(p);
                HeightTable sheet = new HeightTable(PaperTurnModel.TableBins * 4);
                for (int k = 0; k <= 40; k++)
                {
                    model.SetTime(k / 40.0);
                    Affine3 m = model.Solve(p);
                    if (!p.Last.Fitted)
                    {
                        continue; // logged by the Unity side; the test only checks fitted pieces
                    }
                    int sign = leaf == LeafKind.StartStatic ? 1 : -1;
                    sheet.Build(model.SheetPoints, model.SheetPoints.Length, sign, -4, 80);
                    foreach (V3 c in p.Corners)
                    {
                        V3 q = m.Apply(c);
                        double limit = sheet.Lookup(sign * q.x);
                        if (q.y > limit + 0.05)
                        {
                            throw new Exception($"{Goldens.PieceNames[i]} pokes through the sheet at t {k / 40.0}: corner y {q.y:F3} > sheet {limit:F3}");
                        }
                    }
                }
            }
        }
    }

    public static class TestMain
    {
        public static int Main(string[] args)
        {
            int failed = 0, passed = 0;
            foreach (Type type in typeof(TestMain).Assembly.GetTypes())
            {
                foreach (MethodInfo m in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (!m.Name.StartsWith("Test") || m.GetParameters().Length != 0)
                    {
                        continue;
                    }
                    try
                    {
                        m.Invoke(null, null);
                        passed++;
                        Console.WriteLine($"[PaperTests] PASS {type.Name}.{m.Name}");
                    }
                    catch (TargetInvocationException e)
                    {
                        failed++;
                        Console.WriteLine($"[PaperTests] FAIL {type.Name}.{m.Name}: {e.InnerException?.Message}");
                    }
                }
            }
            Console.WriteLine($"[PaperTests] {passed} passed, {failed} failed");
            return failed == 0 ? 0 : 1;
        }
    }
}
