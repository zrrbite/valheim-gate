using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// Serializable snapshot of an in-progress Run Mode run. JsonUtility can't
    /// serialize dictionaries, so anything key/value-shaped is stored as
    /// parallel lists instead.
    /// </summary>
    [Serializable]
    public class RunSaveState
    {
        public float elapsedSeconds;
        public float heat;
        public List<string> defeatedBossKeys;
        public List<string> splitLabels;
        public List<float> splitTimes;
        public List<string> activeChallengeIds;
        public List<float> activeChallengeProgress;

        /// <summary>
        /// Deal-time zero point for each active StatDelta challenge, same index pairing as
        /// <see cref="activeChallengeIds"/>. -1 means "none taken yet" — a stand-in for NaN, which
        /// JsonUtility writes as a bare <c>NaN</c> token that no other JSON reader accepts. Absent
        /// in saves written before alpha4; the run then re-baselines on resume.
        /// </summary>
        public List<float> activeChallengeBaselines;

        /// <summary>
        /// Per-sub progress for each active challenge, same index pairing as
        /// <see cref="activeChallengeIds"/>. JsonUtility can't nest a list of lists, so each
        /// entry is that slot's <see cref="ChallengeEngine.SubProgress"/> values joined with
        /// ';' ("1;0;3"); the empty string means "not a composite challenge" (SubProgress null).
        /// Absent in saves written before alpha8, which restores every composite sub at zero —
        /// see ChallengeEngine.RestoreActive's malformed-input tolerance.
        /// </summary>
        public List<string> activeChallengeSubProgress;

        /// <summary>
        /// Position in the main-quest chain (see <see cref="ChallengeEngine.SetMainChain"/>),
        /// stored independently of the actives lists because the questline occupies a reserved
        /// slot of its own rather than one of the three random ones.
        ///
        /// Absent in saves written before the chain existed, where JsonUtility leaves it 0 — which
        /// is exactly the right answer: such a run resumes with the questline at step 0, fresh.
        /// </summary>
        public int mainQuestIndex;

        /// <summary>Progress on the step named by <see cref="mainQuestIndex"/>; 0 on a legacy save.</summary>
        public float mainQuestProgress;

        /// <summary>
        /// Id of the questline step in progress. Authoritative over <see cref="mainQuestIndex"/>,
        /// which only means anything against the exact chain that wrote it — and the chain is
        /// content that changes between builds. Null/empty on a save from before this field, where
        /// the restore falls back to the index and drops the progress rather than risk crediting a
        /// step the player never worked on.
        /// </summary>
        public string mainQuestId;

        /// <summary>
        /// Per-track questline positions, as parallel lists — one entry per track, in the act's
        /// track order. Written since alpha32, when one questline became two.
        ///
        /// Null on an older save, which is what <see cref="mainQuestId"/> above is still here for:
        /// that single id is resolved across EVERY track's chain and seats whichever track owns it,
        /// leaving the other at its start. Same id-over-index principle the single chain already
        /// used, one level up.
        /// </summary>
        public List<string> trackIds;
        public List<int> trackIndices;
        public List<float> trackProgress;
        public List<string> trackStepIds;

        /// <summary>
        /// The player's WoodCutting skill level as it stood before the run LOANED them 100 (see
        /// RunService's skill boost — "max woodcutting from the start", with no grinding for it).
        /// Restored when the run finishes or is abandoned, so the loan is never permanent.
        ///
        /// -1 means "no snapshot in this save": either a save written before the loan existed, or
        /// one taken before the snapshot could be read. A resume takes a fresh snapshot in that
        /// case rather than guessing a level to give back.
        ///
        /// LEGACY: superseded by <see cref="skillLoanTypes"/>, which carries the questline's skill
        /// grants as well. Still READ, so a run saved before that change resumes with its
        /// WoodCutting loan intact; no longer written.
        /// </summary>
        public float woodcuttingOriginal = -1f;

        /// <summary>
        /// Skills this run has loaned, as three index-paired lists: the skill (a
        /// <c>Skills.SkillType</c> cast to int), what it was worth before the run touched it, and
        /// what the run raised it to. Empty on a save from before the questline paid in skill.
        ///
        /// Stored as int rather than the enum because JsonUtility serialises enums by ordinal
        /// anyway, and a raw int makes the file readable — and repairable — by hand.
        /// </summary>
        public List<int> skillLoanTypes;
        public List<float> skillLoanOriginals;
        public List<float> skillLoanLevels;

        public List<string> heldBoonIds;
        public List<float> heldBoonCooldowns;

        /// <summary>Charges per held boon, same index pairing as <see cref="heldBoonIds"/> (way's single-charge active; 0 for everything else).</summary>
        public List<int> heldBoonCharges;
        public int rngSeed;

        /// <summary>Bitmask of Heightmap.Biome values visited this run (biome-gated quest dealing).
        /// 0 on old saves — RunService re-seeds Meadows + current biome on resume.</summary>
        public int visitedBiomes;

        /// <summary>
        /// Categories of building piece the player has been seen to have built this run — the
        /// vocabulary of ChallengeKind.BuildPiece ("Fire", "Bed", "Chest", "Door").
        ///
        /// Persisted because the live scan only sees what is near the player: resume a run out in
        /// the field and an unpersisted set would be empty, un-finishing a completed build step and
        /// pulling the door task back out of the draw pool until the player next walked home.
        /// Null on old saves, which reads as "nothing yet" and re-latches on the next scan.
        /// </summary>
        public List<string> builtCategories;

        /// <summary>
        /// The run's stash, as four parallel lists — prefab, count, quality, variant — in the same
        /// style as every other collection here, because Unity's JsonUtility cannot serialise a
        /// list of objects nested in a plain class without a wrapper per element.
        ///
        /// Quality and variant are stored because they are part of an item's IDENTITY: a level-3
        /// axe and a level-1 axe are different objects, and collapsing them would hand back two of
        /// whichever was written last. Durability is deliberately NOT stored — a withdrawn tool
        /// returns at full, which is a small gift rather than a loss.
        ///
        /// Null on a pre-alpha30 save; RunStash.Restore tolerates any short or absent list.
        /// </summary>
        /// <summary>
        /// Max health lent by completions so far (alpha35). Re-applied on resume; 0 on an older
        /// save, which reads as "nothing lent yet" and simply starts the accumulation from there.
        /// </summary>
        public float taskHealthReward;

        /// <summary>
        /// Unspent Homeward charges (alpha35) — one granted per boss felled, each teleporting the
        /// player to their claimed bed. Persisted so a suspend cannot quietly spend them.
        /// </summary>
        public int homewardCharges;

        public List<string> stashPrefabs;
        public List<int> stashCounts;
        public List<int> stashQualities;
        public List<int> stashVariants;

        /// <summary>
        /// The world this run belongs to. A saved run must never be resumed against a
        /// different world — its boss keys and world modifiers would be meaningless there.
        /// </summary>
        public string worldId;

        /// <summary>
        /// Pre-run values of the world-modifier global keys, as GlobalKeys enum values.
        /// Carried across a reload so a resumed run restores the world's ORIGINAL rates
        /// rather than baking its own inflated ones in permanently.
        /// </summary>
        public List<int> modifierKeys;

        /// <summary>Pre-run values, in the same order as <see cref="modifierKeys"/>.</summary>
        public List<float> modifierValues;
    }

    /// <summary>
    /// Reads/writes per-character Run Mode save state to JSON on disk.
    /// Mirrors the read/write/try-catch style of Core/Configuration.cs.
    /// </summary>
    public static class RunStorage
    {
        /// <summary>
        /// Full path to the run-state file for a given character name.
        /// </summary>
        public static string PathForCharacter(string characterName)
        {
            string sanitized = Sanitize(characterName);
            return Application.persistentDataPath + "/ICSYTW_run_" + sanitized + ".json";
        }

        /// <summary>
        /// Write the given run state to disk for the named character, overwriting any existing file.
        /// </summary>
        public static void Save(string characterName, RunSaveState s)
        {
            if (string.IsNullOrEmpty(characterName) || s == null)
            {
                return;
            }

            try
            {
                string path = PathForCharacter(characterName);
                string json = JsonUtility.ToJson(s, prettyPrint: true);

                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ICanShowYouTheWorld] Failed to save run state for '{characterName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Load the run state for the named character. Returns null if the file is missing,
        /// unreadable, or fails to parse.
        /// </summary>
        public static RunSaveState TryLoad(string characterName)
        {
            bool ignored;
            return TryLoad(characterName, out ignored);
        }

        /// <summary>
        /// Load the run state for the named character, distinguishing "no run saved" from
        /// "a run was saved but the file can't be read".
        ///
        /// The two cases could not be less alike: the first is the normal state of a character
        /// that isn't mid-run, the second means an in-progress run — and, more importantly, the
        /// only surviving copy of that world's ORIGINAL modifier values — just became
        /// unreadable. Returning a bare null for both let that loss pass in silence.
        ///
        /// An unreadable file is renamed to "&lt;name&gt;.json.corrupt" rather than deleted,
        /// precisely because of those originals: a human can still salvage the numbers out of it.
        /// </summary>
        /// <param name="existedButCorrupt">
        /// True when a file was present but could not be read or parsed (and has been
        /// quarantined). False when there simply was no run saved.
        /// </param>
        public static RunSaveState TryLoad(string characterName, out bool existedButCorrupt)
        {
            existedButCorrupt = false;

            if (string.IsNullOrEmpty(characterName))
            {
                return null;
            }

            string path = null;
            try
            {
                path = PathForCharacter(characterName);
                if (!File.Exists(path))
                {
                    return null;
                }

                string json = File.ReadAllText(path);
                var state = JsonUtility.FromJson<RunSaveState>(json);

                // JsonUtility answers empty/"null" JSON with a null object rather than throwing.
                if (state == null)
                {
                    Debug.LogError($"[ICanShowYouTheWorld] Run state for '{characterName}' is empty or not an object.");
                    existedButCorrupt = true;
                    QuarantineCorrupt(path, characterName);
                    return null;
                }

                return state;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ICanShowYouTheWorld] Failed to load run state for '{characterName}': {ex.Message}");
                existedButCorrupt = true;
                QuarantineCorrupt(path, characterName);
                return null;
            }
        }

        /// <summary>
        /// Moves an unreadable run-state file aside so the next load reads as "no run" instead of
        /// failing forever, while keeping the file itself — it is the only record of the world's
        /// pre-run modifier values. An existing quarantine file is never overwritten; a colliding
        /// rename gets a timestamp instead.
        /// </summary>
        private static void QuarantineCorrupt(string path, string characterName)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                if (!File.Exists(path))
                {
                    return;
                }

                string target = path + ".corrupt";
                if (File.Exists(target))
                {
                    target = path + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) +
                             ".corrupt";
                }

                File.Move(path, target);
                Debug.LogWarning($"[ICanShowYouTheWorld] Unreadable run state for '{characterName}' kept as {target}.");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[ICanShowYouTheWorld] Failed to quarantine run state for '{characterName}': {ex.Message}");
            }
        }

        /// <summary>
        /// The skill loans in a save, keyed by type, with the legacy single-WoodCutting field
        /// folded in. A save written before <see cref="RunSaveState.skillLoanTypes"/> existed
        /// carries only <see cref="RunSaveState.woodcuttingOriginal"/>, and resuming it must still
        /// give that level back — so it is migrated here rather than dropped, using the loan level
        /// the build that wrote it always used (100, the game's skill cap).
        ///
        /// Entries whose lists are short or whose type is not a real SkillType are skipped: a
        /// hand-edited or truncated file should cost the player one loan, not the whole run.
        /// </summary>
        public static IEnumerable<KeyValuePair<Skills.SkillType, (float original, float level)>>
            ImportSkillLoans(RunSaveState s)
        {
            if (s == null) yield break;

            var types = s.skillLoanTypes;
            if (types != null && types.Count > 0)
            {
                for (int i = 0; i < types.Count; i++)
                {
                    if (s.skillLoanOriginals == null || i >= s.skillLoanOriginals.Count) continue;
                    if (s.skillLoanLevels == null || i >= s.skillLoanLevels.Count) continue;
                    if (!Enum.IsDefined(typeof(Skills.SkillType), types[i])) continue;

                    yield return new KeyValuePair<Skills.SkillType, (float, float)>(
                        (Skills.SkillType)types[i], (s.skillLoanOriginals[i], s.skillLoanLevels[i]));
                }
                yield break;
            }

            if (s.woodcuttingOriginal >= 0f)
            {
                yield return new KeyValuePair<Skills.SkillType, (float, float)>(
                    Skills.SkillType.WoodCutting, (s.woodcuttingOriginal, 100f));
            }
        }

        /// <summary>
        /// Delete the run-state file for the named character, if it exists.
        /// </summary>
        public static void Delete(string characterName)
        {
            if (string.IsNullOrEmpty(characterName))
            {
                return;
            }

            try
            {
                string path = PathForCharacter(characterName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ICanShowYouTheWorld] Failed to delete run state for '{characterName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Keeps only letters/digits from the character name, replacing everything else
        /// with '_', so the result is always a safe filename component.
        /// </summary>
        private static string Sanitize(string characterName)
        {
            if (string.IsNullOrEmpty(characterName))
            {
                return "_";
            }

            var sb = new StringBuilder(characterName.Length);
            foreach (char c in characterName)
            {
                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                sb.Append(ok ? c : '_');
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Cross-run progress stored directly on the character save (Player.m_customData),
    /// so it persists and travels with the character file rather than a side JSON file.
    /// </summary>
    public static class PermanentRecord
    {
        private const string BossesKey = "ICSYTW_saga_bosses";
        private const string BestScoreKey = "ICSYTW_saga_best";
        private const string RunsKey = "ICSYTW_saga_runs";
        private const int MainlandBossCount = 5;

        /// <summary>
        /// Records a boss defeat into the character's saga record. No-op if already recorded.
        /// </summary>
        public static void RecordBossKill(Player p, string bossKey)
        {
            if (p == null || p.m_customData == null || string.IsNullOrEmpty(bossKey))
            {
                return;
            }

            try
            {
                List<string> bosses = ParseBossList(p);
                if (!bosses.Contains(bossKey))
                {
                    bosses.Add(bossKey);
                }

                p.m_customData[BossesKey] = string.Join(",", bosses);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ICanShowYouTheWorld] Failed to record boss kill '{bossKey}': {ex.Message}");
            }
        }

        /// <summary>
        /// Records the result of a completed run: bumps the best score if this one is higher,
        /// and increments the total run count.
        /// </summary>
        public static void RecordScore(Player p, float score)
        {
            if (p == null || p.m_customData == null)
            {
                return;
            }

            try
            {
                float best = ParseBestScore(p);
                if (score > best)
                {
                    p.m_customData[BestScoreKey] = score.ToString("0.###", CultureInfo.InvariantCulture);
                }

                int runs = ParseRunCount(p);
                p.m_customData[RunsKey] = (runs + 1).ToString(CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ICanShowYouTheWorld] Failed to record score '{score}': {ex.Message}");
            }
        }

        /// <summary>
        /// Human-readable one-liner for the lobby UI, e.g. "Bosses: 3/5 · Best: 2.41 · Runs: 4".
        /// Safe to call with a null player or a character with no saga data yet.
        /// </summary>
        public static string GetSummary(Player p)
        {
            int bossCount = 0;
            string bestText = "—";
            int runs = 0;

            if (p != null && p.m_customData != null)
            {
                try
                {
                    bossCount = ParseBossList(p).Count;

                    float best = ParseBestScore(p);
                    if (best > 0f)
                    {
                        bestText = best.ToString("0.###", CultureInfo.InvariantCulture);
                    }

                    runs = ParseRunCount(p);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ICanShowYouTheWorld] Failed to build saga summary: {ex.Message}");
                }
            }

            return $"Bosses: {bossCount}/{MainlandBossCount} · Best: {bestText} · Runs: {runs}";
        }

        private static List<string> ParseBossList(Player p)
        {
            if (!p.m_customData.TryGetValue(BossesKey, out string raw) || string.IsNullOrEmpty(raw))
            {
                return new List<string>();
            }

            return raw
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .Distinct()
                .ToList();
        }

        private static float ParseBestScore(Player p)
        {
            if (p.m_customData.TryGetValue(BestScoreKey, out string raw) &&
                float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float best))
            {
                return best;
            }
            return 0f;
        }

        private static int ParseRunCount(Player p)
        {
            if (p.m_customData.TryGetValue(RunsKey, out string raw) &&
                int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int runs))
            {
                return runs;
            }
            return 0;
        }
    }
}
