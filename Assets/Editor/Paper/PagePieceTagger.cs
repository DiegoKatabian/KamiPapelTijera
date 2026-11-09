using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Kami > Pages > Tag Page Pieces (spec 025 D29). Select a page folder (or several) in the scene:
/// "Report" lists how the paper page turn will group and classify everything under it, without changing anything;
/// "Apply" adds a PagePiece with that class to every piece that has none (Undo works), so the class can be changed by
/// hand in the Inspector. Pieces that already have a PagePiece keep theirs.
/// </summary>
public static class PagePieceTagger
{
    [MenuItem("Kami/Pages/Tag Page Pieces - Report (selected folders)")]
    static void Report() => Run(false);

    [MenuItem("Kami/Pages/Tag Page Pieces - Apply (selected folders)")]
    static void Apply() => Run(true);

    static void Run(bool apply)
    {
        PaperTurn turn = Object.FindObjectOfType<PaperTurn>(true);
        if (turn == null || turn.Book == null)
        {
            Debug.LogWarning("[PagePieceTagger] no PaperTurn with book data in the open scene");
            return;
        }
        PaperBookData book = turn.Book;
        PaperFrame frame = new PaperFrame { SpineX = book.spineX, Dir = 1 };
        if (Selection.transforms.Length == 0)
        {
            Debug.LogWarning("[PagePieceTagger] select one or more page folders first");
            return;
        }
        foreach (Transform folder in Selection.transforms)
        {
            List<KeyValuePair<Transform, List<Renderer>>> groups = new List<KeyValuePair<Transform, List<Renderer>>>();
            List<Renderer> untouched = new List<Renderer>();
            PaperPieces.Group(folder, book, groups, untouched);
            StringBuilder sb = new StringBuilder($"[PagePieceTagger] {folder.name}: {groups.Count} pieces{(apply ? " (applying)" : " (report only)")}\n");
            int added = 0;
            foreach (KeyValuePair<Transform, List<Renderer>> g in groups)
            {
                PagePiece existing = g.Key.GetComponent<PagePiece>();
                Bounds world = PaperPieces.WorldBounds(g.Value);
                PagePiece.PieceClass auto = PaperPieces.Classify(g.Value, world, book);
                Bounds canon = PaperPieces.CanonBounds(g.Value, frame);
                string side = canon.center.x >= 0 ? "right" : "left";
                sb.AppendLine($"  {g.Key.name}: {(existing != null ? existing.pieceClass + " (tagged)" : auto + " (auto)")}, " +
                              $"{side} half, {g.Value.Count} renderers, size {world.size:F1}");
                if (apply && existing == null && g.Key != folder)
                {
                    PagePiece added1 = Undo.AddComponent<PagePiece>(g.Key.gameObject);
                    added1.pieceClass = auto;
                    added++;
                }
            }
            foreach (Renderer r in untouched)
            {
                sb.AppendLine($"  left alone: {r.name}");
            }
            if (apply)
            {
                sb.AppendLine($"  added {added} PagePiece components");
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(folder.gameObject.scene);
            }
            Debug.Log(sb.ToString());
        }
    }
}
