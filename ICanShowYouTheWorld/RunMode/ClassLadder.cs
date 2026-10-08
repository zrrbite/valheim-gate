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
        /// Defeated bosses needed for rung 0, 1 and 2: at the choice, after Eikthyr, after the
        /// Elder. Data, so moving a rung is one number.
        /// </summary>
        /// <remarks>
        /// Was { 0, 1, 3 } until 2026-10-05. Rung 3 after Bonemass meant most sessions only ever
        /// held two of a way's three abilities (owner, after play).
        /// </remarks>
        public static readonly int[] Thresholds = { 0, 1, 2 };

        /// <summary>
        /// Which god falls at each boss count, in the saga's order, for saying what a rung waits
        /// on. A table rather than two strings in the HUD so that moving a threshold keeps the line
        /// true; a count past the table falls back to a number.
        /// </summary>
        private static readonly Dictionary<int, string> BossAtCount = new Dictionary<int, string>
        {
            { 1, "Eikthyr" }, { 2, "the Elder" }, { 3, "Bonemass" }, { 4, "Moder" }, { 5, "Yagluth" },
            { 6, "the Queen" },
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
        /// The level the way's skill shows in the Skills window: 100 times the share of rungs whose
        /// every boon is held, floored - 0, 33, 66, 100 on a ladder of three. Passives do not count:
        /// they come with rung 1, so the choice itself already reads 33.
        /// </summary>
        /// <remarks>
        /// A MIRROR of the ladder, not a skill that is trained. The owner wanted to "see your progress
        /// on a skillbar", and the ladder is the boss count by design (see the class spec), so the bar
        /// reads the ladder and nothing ever raises it by use. Floored rather than rounded so the
        /// middle rung says 66 and only the whole way says 100. Here, not in the host, because the
        /// host's arithmetic is the kind the tests cannot see.
        /// </remarks>
        public static int MirrorLevel(ClassDefinition cls, IEnumerable<string> heldIds)
        {
            var rungs = cls?.Rungs;
            if (rungs == null || rungs.Length == 0) return 0;
            var held = new HashSet<string>(heldIds ?? Enumerable.Empty<string>());

            int learned = rungs.Count(r => r != null && r.Length > 0 && r.All(held.Contains));
            return 100 * learned / rungs.Length;
        }

        /// <summary>
        /// Which rung a boon sits on: the 0-based index in the first way of the <see cref="Catalog"/> whose
        /// <see cref="ClassDefinition.Rungs"/> hold the id, or -1 for a null, empty, unknown or passive id.
        /// BoonKeys calls this for every rung boon and turns the index into a key with
        /// <see cref="KeyLayout.RungKey"/> (Keypad 7 / 0 / Insert, or U / I / O), so a way's keys follow its ladder
        /// and reordering a rung cannot leave a boon on the wrong key.
        /// </summary>
        public static int RungIndex(string boonId)
        {
            if (string.IsNullOrEmpty(boonId)) return -1;
            foreach (var cls in Catalog())
            {
                var rungs = cls.Rungs ?? new string[0][];
                for (int i = 0; i < rungs.Length; i++)
                    if (rungs[i] != null && rungs[i].Contains(boonId)) return i;
            }
            return -1;
        }

        /// <summary>
        /// The table: Hunter, Völva, Berserker (v1), then Húskarl, Skald, Sæfari, Smiðr - the seven
        /// graves the thane names. Catalog order is card order and key order (Keypad1-7).
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
                Description = "Beasts answer you. A wolf comes when you call. Later, a beast on loan from the lands you have opened, " +
                              "and the trick of going unseen. Your bow hand knows more than it did, and your tames are stronger." +
                              " You move quietly." +
                              " Later your arrows learn fire and frost.",
                PassiveBoonIds = new[] { "hunter", "shepherd" },
                // Elemental Arrows beside Menagerie (owner, 2026-09-28): the Hunter is the bow's way,
                // and the saga's bow is Thor's - so her second rung teaches it fire and frost.
                Rungs = new[] { new[] { "brother" }, new[] { "menagerie", "elemental" }, new[] { "unseen" } },
            },
            new ClassDefinition
            {
                Id = "volva", Display = "Völva", Title = "Sigrún",
                Description = "A mending warmth follows you and everyone at your side. The dead rise at your word from " +
                              "the start; later you can pour the warmth out where you stand, and after that the sky " +
                              "answers where you point.",
                PassiveBoonIds = new[] { "hearthlight" },
                // Her dead first (class balance, 2026-10-08): the aura needs allies to mend from the start.
                Rungs = new[] { new[] { "bonecaller" }, new[] { "shaman" }, new[] { "wrath" } },
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

            // The four of 2026-09-28, completing the hird of seven the thane buries. Their gifts are
            // vanilla prefabs, checked at run start with the other rewards like Ulfr's axes.
            new ClassDefinition
            {
                Id = "huskarl", Display = "Húskarl", Title = "Halvard",
                Description = "You stand and things break on you. A bash that staggers what is in front; later a wall " +
                              "that shrugs off blows, and after that a last stand nothing can end. His shield and spear " +
                              "are yours the moment you take his name.",
                PassiveBoonIds = new[] { "hirdman" },
                Rungs = new[] { new[] { "bash" }, new[] { "bulwark" }, new[] { "laststand" } },
                GrantItems = new[] { ("ShieldWood", 1), ("SpearFlint", 1) },
            },
            new ClassDefinition
            {
                Id = "skald", Display = "Skald", Title = "Ormr",
                Description = "You sing and the road shortens: a marching song that carries you; later a war song that " +
                              "sharpens your blows, and after that Bragi’s own saga, which rests you where you stand. His " +
                              "flask is yours the moment you take his name.",
                PassiveBoonIds = new[] { "poet" },
                Rungs = new[] { new[] { "march" }, new[] { "warsong" }, new[] { "bragi" } },
                GrantItems = new[] { ("MeadHealthMinor", 3) },
            },
            new ClassDefinition
            {
                Id = "saefari", Display = "Sæfari", Title = "Ragna",
                Description = "Water is yours: a tide that carries you without tiring; later a fair wind at your ship’s " +
                              "back, and after that sea-legs that no cold or wet can touch. Her harpoon is yours the " +
                              "moment you take her name.",
                PassiveBoonIds = new[] { "seafarer" },
                Rungs = new[] { new[] { "tide" }, new[] { "fairwind" }, new[] { "sealegs" } },
                // The abyssal harpoon - a Mistlands weapon handed over in the Meadows, because it is
                // the one spear the game makes for pulling things out of the water.
                GrantItems = new[] { ("SpearChitin", 1) },
            },
            new ClassDefinition
            {
                Id = "smidr", Display = "Smiðr", Title = "Dvalinn",
                Description = "You build where you stand: a bench and a forge raised from nothing for a minute and a " +
                              "half; later the master’s minute, when building costs nothing, and after that walls that " +
                              "no weather wears. His tools are yours the moment you take his name.",
                PassiveBoonIds = new[] { "craftsman" },
                Rungs = new[] { new[] { "fieldforge" }, new[] { "mastersminute" }, new[] { "reinforce" } },
                GrantItems = new[] { ("Hoe", 1), ("Cultivator", 1) },
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

            // A cover that names nothing covers nothing, and the blank card stays on the wheel (2026-10-08).
            foreach (var def in poolList.Where(b => b.CoveredBy != null))
                foreach (var cover in def.CoveredBy.Where(c => !byId.ContainsKey(c ?? "")))
                    yield return $"boon '{def.Id}' is CoveredBy '{cover}', which is not in the pool";
        }
    }
}
