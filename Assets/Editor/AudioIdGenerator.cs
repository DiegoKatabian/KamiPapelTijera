using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates AudioId.cs from the bank, so call sites say AudioId.QuestCompleted02 instead of a
/// magic string. The payoff is compile-time safety: delete a sound from the bank and every call
/// site stops compiling, instead of throwing at runtime the way the old string dictionary did
/// (TriggerSound.cs used to ask for "4S_MarimbaLoop", which exists nowhere -- see the design doc).
///
/// Re-run after adding or renaming anything in the bank.
/// </summary>
public static class AudioIdGenerator
{
    const string OUTPUT_PATH = "Assets/Scripts/Managers/Audio/AudioId.cs";

    [MenuItem("Kami/Audio/Regenerate AudioId")]
    public static void Generate()
    {
        AudioBank bank = AssetDatabase.LoadAssetAtPath<AudioBank>("Assets/Resources/AudioBank.asset");
        if (bank == null)
        {
            Debug.LogError("[AudioIdGenerator] there is no Resources/AudioBank.asset. " +
                           "Run Kami/Audio/Rebuild Audio Bank first.");
            return;
        }

        var used = new Dictionary<string, string>(); //identifier -> original id
        var rows = new List<KeyValuePair<string, string>>();

        foreach (string id in bank.Ids)
        {
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            string identifier = Sanitize(id);
            if (used.TryGetValue(identifier, out string clash))
            {
                //fail loudly instead of writing a file that will not compile
                Debug.LogError($"[AudioIdGenerator] collision: '{id}' and '{clash}' produce the same " +
                               $"identifier '{identifier}'. Rename one in the bank and run it again.");
                return;
            }
            used[identifier] = id;
            rows.Add(new KeyValuePair<string, string>(identifier, id));
        }

        var sb = new StringBuilder();
        sb.AppendLine("// GENERATED FILE -- do not edit by hand.");
        sb.AppendLine("// Regenerate with Kami/Audio/Regenerate AudioId after changing AudioBank.");
        sb.AppendLine();
        sb.AppendLine("/// <summary>Every sound id in the AudioBank, as compile-time constants.</summary>");
        sb.AppendLine("public static class AudioId");
        sb.AppendLine("{");
        foreach (KeyValuePair<string, string> row in rows)
        {
            sb.AppendLine($"    public const string {row.Key} = \"{row.Value}\";");
        }
        sb.AppendLine("}");

        System.IO.File.WriteAllText(OUTPUT_PATH, sb.ToString());
        AssetDatabase.ImportAsset(OUTPUT_PATH);
        Debug.Log($"[AudioIdGenerator] wrote {rows.Count} constants to {OUTPUT_PATH}.");
    }

    /// <summary>
    /// Bank ids come from GameObject names, which can contain spaces and other characters that
    /// are illegal in a C# identifier. The const VALUE always keeps the original id, so the
    /// lookup still matches the bank.
    /// </summary>
    static string Sanitize(string id)
    {
        string cleaned = Regex.Replace(id, @"[^A-Za-z0-9_]", "_");
        if (cleaned.Length > 0 && char.IsDigit(cleaned[0]))
        {
            cleaned = "_" + cleaned;
        }
        return cleaned;
    }
}
