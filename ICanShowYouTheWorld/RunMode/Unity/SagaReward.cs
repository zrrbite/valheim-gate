using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The saga's reward: "The Saga of &lt;character&gt;", written next to the saga's own files when a run
    /// is won, and opened in the browser. The composing is pure (<see cref="SagaPage"/>); this is the
    /// part that touches the disk, the embedded myth and the browser.
    /// </summary>
    /// <remarks>
    /// The myth is docs/THE-SAGA.md, EMBEDDED at build time (csproj LogicalName below), so the page is
    /// always the current story and there is no copy to drift.
    /// </remarks>
    internal static class SagaReward
    {
        public const string MythResource = "ICanShowYouTheWorld.TheSaga.md";

        /// <summary>The folder the config and the run state live in: where a player would look.</summary>
        public static string Folder => Application.persistentDataPath;

        public static string LoadMyth()
        {
            try
            {
                using (var stream = typeof(SagaReward).Assembly.GetManifestResourceStream(MythResource))
                {
                    if (stream == null) return null;
                    using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] The saga's myth could not be read: " + ex.Message);
                return null;
            }
        }

        /// <summary>Writes the page and returns its path, or null.</summary>
        public static string Write(string character, string html)
        {
            try
            {
                string name = SagaPage.FileName(character, DateTime.Now.ToString("yyyy-MM-dd HHmm"));
                string path = Path.Combine(Folder, name);
                File.WriteAllText(path, html, new System.Text.UTF8Encoding(false));
                Debug.Log($"[ICanShowYouTheWorld] The saga's page: {path}");
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ICanShowYouTheWorld] The saga's page could not be written: " + ex.Message);
                return null;
            }
        }

        /// <summary>Opens a page in the player's browser.</summary>
        public static void Open(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try { Application.OpenURL(new Uri(path).AbsoluteUri); }
            catch (Exception ex) { Debug.LogWarning("[ICanShowYouTheWorld] The saga's page could not be opened: " + ex.Message); }
        }

        /// <summary>The newest page written for this character, or null.</summary>
        public static string Latest(string character)
        {
            try
            {
                if (!Directory.Exists(Folder)) return null;
                string prefix = SagaPage.FilePrefix(character);
                return Directory.GetFiles(Folder, "*.html")
                    .Where(f => Path.GetFileName(f).StartsWith(prefix, StringComparison.Ordinal))
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch { return null; }
        }
    }
}
