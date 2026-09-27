using System.Collections.Generic;
using System.Linq;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The ways a run may take up, and the ladder that paces each one's kit.
    ///
    /// Pure, like <see cref="SagaNames"/>, so the harness can see it: every gate this mode has
    /// shipped wrong was logic stranded where the tests could not reach. And PER RUN, because the
    /// saga's invariant is that power is loaned per run and repaid — a way that outlived the run
    /// would be the one piece of power that did not. "This time as a Völva" is worth more than a
    /// permanent Hunter.
    ///
    /// The ladder is the boss count, not a skill level. Skills gain at x3 in a run and are loans
    /// themselves, and the world already says how many gods are down (it is what
    /// <see cref="BoonDefinition.MinBosses"/> reads), so no new save data is needed to pace it.
    /// </summary>
    public static class ClassLadder
    {
        /// <summary>
        /// Defeated bosses needed for rung 0, 1 and 2: at the choice, after Eikthyr, after
        /// Bonemass. Data, so moving a rung is one number.
        /// </summary>
        public static readonly int[] Thresholds = { 0, 1, 3 };

        /// <summary>
        /// Which god falls at each boss count, in the saga's order, for saying what a rung waits
        /// on. A table rather than two strings in the HUD so that moving a threshold keeps the line
        /// true; a count past the table falls back to a number.
        /// </summary>
        private static readonly Dictionary<int, string> BossAtCount = new Dictionary<int, string>
        {
            { 1, "Eikthyr" }, { 2, "the Elder" }, { 3, "Bonemass" }, { 4, "Moder" }, { 5, "Yagluth" },
        };

        /// <summary>"after Eikthyr" for threshold 1, and so on - the HUD's state for a rung not yet due.</summary>
        public static string AfterLine(int threshold) =>
            threshold <= 0 ? "at the choice"
            : BossAtCount.TryGetValue(threshold, out var name) ? "after " + name
            : "after " + threshold + " gods";

        /// <summary>
        /// The boon ids this way owes the player now: its passives plus every rung whose threshold
        /// has been reached, minus what is already held, in ladder order. Empty for a null class.
        /// </summary>
        public static IEnumerable<string> Due(ClassDefinition cls, int defeatedBosses, IEnumerable<string> heldIds)
        {
            if (cls == null) yield break;
            var held = new HashSet<string>(heldIds ?? Enumerable.Empty<string>());

            foreach (var id in cls.PassiveBoonIds ?? new string[0])
                if (held.Add(id)) yield return id;

            var rungs = cls.Rungs ?? new string[0][];
            for (int i = 0; i < rungs.Length && i < Thresholds.Length; i++)
            {
                if (Thresholds[i] > defeatedBosses) break;
                foreach (var id in rungs[i] ?? new string[0])
                    if (held.Add(id)) yield return id;
            }
        }

        /// <summary>
        /// The next boss count at which something new becomes due — the smallest threshold above
        /// <paramref name="defeatedBosses"/> whose rung still has an id not held — or null when
        /// the ladder has nothing further to give.
        /// </summary>
        public static int? NextThreshold(ClassDefinition cls, int defeatedBosses, IEnumerable<string> heldIds)
        {
            if (cls == null || cls.Rungs == null) return null;
            var held = new HashSet<string>(heldIds ?? Enumerable.Empty<string>());

            for (int i = 0; i < cls.Rungs.Length && i < Thresholds.Length; i++)
            {
                if (Thresholds[i] <= defeatedBosses) continue;
                if ((cls.Rungs[i] ?? new string[0]).Any(id => !held.Contains(id))) return Thresholds[i];
            }
            return null;
        }

        /// <summary>
        /// The v1 table: Hunter, Völva, Berserker.
        ///
        /// The descriptions are what the thane's card (THE WAY) prints, in his voice: second person,
        /// and nothing promised that the kit does not do.
        ///
        /// Note the Hunter WAY and the <c>hunter</c> BOON (Bows to 50) share an id. They are keys
        /// in different tables — this one and the boon pool — and nothing looks one up in the
        /// other, so the collision is only a reading hazard, not a bug.
        /// </summary>
        public static IReadOnlyList<ClassDefinition> Catalog() => new List<ClassDefinition>
        {
            new ClassDefinition
            {
                Id = "hunter", Display = "Hunter", Title = "Eydís",
                Description = "Beasts answer you. A wolf comes when you call. Later, any creature on loan, " +
                              "and the trick of going unseen. Your bow hand knows more than it did, and your tames are stronger.",
                PassiveBoonIds = new[] { "hunter", "shepherd" },
                Rungs = new[] { new[] { "brother" }, new[] { "menagerie" }, new[] { "unseen" } },
            },
            new ClassDefinition
            {
                Id = "volva", Display = "Völva", Title = "Sigrún",
                Description = "A mending warmth follows you, and you can pour it out where you stand. Later " +
                              "the dead rise at your word, and after that the sky answers where you point.",
                PassiveBoonIds = new[] { "hearthlight" },
                Rungs = new[] { new[] { "shaman" }, new[] { "bonecaller" }, new[] { "wrath" } },
            },
            new ClassDefinition
            {
                Id = "berserker", Display = "Berserker", Title = "Ulfr",
                Description = "Axe, sword and club sit better in your hand. You strike all round you. Later " +
                              "you can rage — hit harder, and take more — and after that your cry staggers everything that hears it." +
                              " Ulfr’s axes are yours the moment you take his name.",
                PassiveBoonIds = new[] { "warrior" },
                Rungs = new[] { new[] { "rend" }, new[] { "rage" }, new[] { "warcry" } },
                // The way made visible. Owner, after three milestones as a Berserker: "I wasn't
                // quite sure what the class benefits were" - and could he dual-wield from the
                // start? Two arbitrary one-handers cannot share the hands without hooking the
                // game's equip code, but the game ships dual-wield ITEMS, so the way hands one
                // over. The name is SagaItems.UlfrsAxesPrefab; spelled out here because this file
                // is pure and cannot see that one. The run-start validator checks it resolves.
                GrantItems = new[] { ("Saga_UlfrsAxes", 1) },
            },
        };

        public static ClassDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Catalog().FirstOrDefault(c => c.Id == id);
        }

        /// <summary>
        /// Human-readable problems between a class table and a boon pool; empty when they agree.
        ///
        /// The two tables are edited in different files, and every way they can disagree fails
        /// SILENTLY in play — a rung that grants nothing, a class boon no way teaches and the
        /// wheel will never deal, a boon two ways both claim. So the host logs these at run start,
        /// the same policy as the asset-name validators.
        /// </summary>
        public static IEnumerable<string> Validate(IEnumerable<ClassDefinition> classes, IEnumerable<BoonDefinition> pool)
        {
            var classList = (classes ?? Enumerable.Empty<ClassDefinition>()).ToList();
            var poolList = (pool ?? Enumerable.Empty<BoonDefinition>()).ToList();
            var byId = poolList.Where(b => b.Id != null).GroupBy(b => b.Id).ToDictionary(g => g.Key, g => g.First());
            var owner = new Dictionary<string, string>();

            foreach (var cls in classList)
            {
                var ids = (cls.PassiveBoonIds ?? new string[0])
                    .Concat((cls.Rungs ?? new string[0][]).SelectMany(r => r ?? new string[0]))
                    .ToList();

                foreach (var id in ids)
                {
                    if (owner.TryGetValue(id, out var other) && other != cls.Id)
                        yield return $"boon '{id}' is claimed by both '{other}' and '{cls.Id}'";
                    else
                        owner[id] = cls.Id;

                    if (!byId.TryGetValue(id, out var def))
                        yield return $"class '{cls.Id}' names boon '{id}', which is not in the pool";
                    else if (def.ClassId != cls.Id)
                        yield return $"class '{cls.Id}' names boon '{id}', whose ClassId is '{def.ClassId ?? "(general)"}'";
                }

                foreach (var def in poolList.Where(b => b.ClassId == cls.Id && !ids.Contains(b.Id)))
                    yield return $"boon '{def.Id}' has ClassId '{cls.Id}' but that class never teaches it";
            }

            // A class-tagged boon whose class does not exist at all: never offered, never taught.
            var known = new HashSet<string>(classList.Select(c => c.Id));
            foreach (var def in poolList.Where(b => b.ClassId != null && !known.Contains(b.ClassId)))
                yield return $"boon '{def.Id}' has ClassId '{def.ClassId}', which is no class";
        }
    }
}
