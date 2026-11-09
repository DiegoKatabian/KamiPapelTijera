using Kami.Paper;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// EditMode checks of spec 025's paper page turn assets (D39): Test Runner > EditMode > Run All. Each test also logs a
/// "[GenCheck] PASS/FAIL" line, which is read back from Editor.log. The math itself is tested outside Unity by
/// tools/run-paper-tests.py.
/// </summary>
public class PaperBookTests
{
    static void Check(bool ok, string what)
    {
        Debug.Log($"[GenCheck] {(ok ? "PASS" : "FAIL")} {what}");
        Assert.IsTrue(ok, what);
    }

    [TestCase("Assets/Data/PaperBook/PaperBook_Level1.asset")]
    [TestCase("Assets/Data/PaperBook/PaperBook_Level2.asset")]
    public void BookDataLoadsAndIsSane(string path)
    {
        PaperBookData book = AssetDatabase.LoadAssetAtPath<PaperBookData>(path);
        Check(book != null, $"{path} loads as PaperBookData");
        Check(book.rightOutward != null && book.rightOutward.Length > 10, $"{path} right profile has points");
        Check(book.leftOutward != null && book.leftOutward.Length > 10, $"{path} left profile has points");
        PaperProfile right = book.Profile(true), left = book.Profile(false);
        Check(right.Length > 20 && left.Length > 20, $"{path} halves are longer than 20 units ({right.Length:F1}, {left.Length:F1})");
        Check(book.pageZ.y - book.pageZ.x > 20, $"{path} pages are deeper than 20 units");
        Check(book.curl != null && book.curl.duration > 0.5, $"{path} curl duration {book.curl?.duration}");
        Vector2 uv = book.UvAt(true, book.spineX + 10, 0.5f * (book.pageZ.x + book.pageZ.y));
        Check(uv.x > -0.5f && uv.x < 1.5f && uv.y > -0.5f && uv.y < 1.5f, $"{path} uv map lands near 0..1 ({uv})");
    }

    [Test]
    public void SheetPrefabIsWired()
    {
        PaperSheet sheet = AssetDatabase.LoadAssetAtPath<PaperSheet>("Assets/Prefabs/Book/PaperSheet.prefab");
        Check(sheet != null, "PaperSheet.prefab has a PaperSheet");
        Check(sheet.Edge != null, "PaperSheet.prefab has its Edge");
        Check(sheet.FrontRenderer != null && sheet.BackRenderer != null, "PaperSheet.prefab has both faces");
    }

    [Test]
    public void OverlayShaderCompiles()
    {
        Shader s = Shader.Find("Kami/Paper Overlay");
        Check(s != null && s.isSupported, "Kami/Paper Overlay shader found and supported");
    }

    [Test]
    public void WorldMapRoundTrips()
    {
        // a leaf that did not move must map every point to itself, in both turn directions
        foreach (int dir in new[] { 1, -1 })
        {
            PaperFrame f = new PaperFrame { SpineX = -0.43f, Dir = dir };
            Matrix4x4 m = f.WorldMap(Affine3.Identity);
            Vector3 p = new Vector3(12.3f, 4.5f, -6.7f);
            Check((m.MultiplyPoint3x4(p) - p).sqrMagnitude < 1e-8f, $"identity map is identity (dir {dir})");
            Vector3 c = f.ToCanon(p);
            Check((f.ToWorld(c) - p).sqrMagnitude < 1e-8f, $"canonical round trip (dir {dir})");
        }
    }

    [Test]
    public void ModelRunsOnBothBooks()
    {
        foreach (string path in new[] { "Assets/Data/PaperBook/PaperBook_Level1.asset", "Assets/Data/PaperBook/PaperBook_Level2.asset" })
        {
            PaperBookData book = AssetDatabase.LoadAssetAtPath<PaperBookData>(path);
            PaperCurl curl = new PaperCurl(book.Profile(true), book.Profile(false), book.curl);
            PaperTurnModel model = new PaperTurnModel(curl, book.cardFallSign);
            PaperPieceRest volume = new PaperPieceRest { Kind = FoldKind.Volume, Leaf = LeafKind.EndStatic };
            int k = 0;
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        volume.Corners[k++] = new V3(x == 0 ? -20 : -14, y == 0 ? 1.5 : 12, z == 0 ? -2 : 2);
                    }
                }
            }
            model.Prepare(volume);
            for (int i = 0; i <= 10; i++)
            {
                model.SetTime(i / 10.0);
                model.Solve(volume);
            }
            // Not Fitted: a house standing on the paper is closer to the landing sheet than the clearance (Level 1's page
            // surface is at 1.44 under the base at 1.5), so the solver folds it fully and squashes it, which is the
            // intended end state, and PaperTurn logs it under "could not fit".
            Check(volume.Last.Alpha > 1.0, $"{path}: a house on the end half is folded flat when the book closes on it (alpha {volume.Last.Alpha:F2}, squash {volume.Last.Squash:F2}, fitted {volume.Last.Fitted})");
        }
    }
}
