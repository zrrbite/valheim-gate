using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ICanShowYouTheWorld
{
    /// <summary>
    /// A [Random] button on the game's own New Character and New World panels, and a name already
    /// filled in when either opens.
    ///
    /// Testing this mode means throwing away a character and a world every few builds, and the
    /// friction is not the clicking — it is inventing a name you do not care about, sixty times
    /// (owner: "since im testing and creating new chars and worlds, is it possible to augment the
    /// original ui to generate a random char name and random world name?"). The save directory is
    /// the evidence: Aaaabbnn, Qqqjgghkj, Sdasdggg, Dfgdfgdfg. Sixty characters named by a hand
    /// mashing the home row.
    ///
    /// It is also a correctness aid, not only a convenience. Run state is keyed by CHARACTER name,
    /// so two throwaway characters with names one keypress apart is exactly how a resume gets
    /// attributed to the wrong run — and a generated name is distinct and readable.
    ///
    /// GM BUILDS ONLY. Nothing here is a cheat, but a saga build handed to somebody else should
    /// leave the vanilla menu exactly as the game drew it; this is a tester's tool.
    /// </summary>
    /// <remarks>
    /// Three facts out of the game's IL made this small, and all three are worth keeping written
    /// down because each one replaced a harder plan:
    ///
    ///   <c>FejdStartup.Update</c> recomputes both Done buttons' <c>interactable</c> from
    ///   <c>text.Length</c> EVERY FRAME, and <c>OnNewCharacterDone</c>/<c>OnNewWorldDone</c> read
    ///   <c>.text</c> at click time. So setting the field's text is the whole job: no events to
    ///   fire, no validation to satisfy, nothing to keep in sync.
    ///
    ///   The fields are public — <c>m_csNewCharacterName</c>, <c>m_newWorldName</c>,
    ///   <c>m_newWorldSeed</c> — but their type is <c>GUIFramework.GuiInputField</c>, which lives in
    ///   <c>gui_framework.dll</c> and derives from <c>TMPro.TMP_InputField</c>. The mod references
    ///   neither, and deliberately: see MenuBadge for why another game assembly in libraries/ is a
    ///   worse price than reflection. So the field and its <c>text</c> property are both reflective,
    ///   and the buttons are not — <c>UnityEngine.UI</c> IS referenced.
    ///
    ///   <c>World.HaveWorld(name)</c> is public and static, which is what lets a rolled world name
    ///   be checked before it is offered. A collision is not cosmetic: the game refuses the whole
    ///   creation with "$menu_newworldalreadyexists".
    /// </remarks>
    internal static class MenuDice
    {
        /// <summary>Name of the cloned button, so a second clone is never made.</summary>
        private const string CloneName = "ICSYTW_RandomButton";

        private static FejdStartup _seenStartup;
        private static bool _characterPanelWasOpen;
        private static bool _worldPanelWasOpen;
        private static bool _complained;

        private static readonly System.Random Rng = new System.Random();

        /// <summary>
        /// Every frame from CheatController.Update, beside <see cref="MenuBadge.Tick"/>. Returns on
        /// the first line once the game scene has replaced the menu.
        /// </summary>
        public static void Tick()
        {
            if (!ModVersion.GmEnabled) return;

            try
            {
                var fejd = FejdStartup.instance;
                if (fejd == null)
                {
                    // Returning to the menu rebuilds the scene, so everything found here is stale.
                    _seenStartup = null;
                    _characterPanelWasOpen = false;
                    _worldPanelWasOpen = false;
                    return;
                }

                if (!ReferenceEquals(fejd, _seenStartup))
                {
                    _seenStartup = fejd;
                    _characterPanelWasOpen = false;
                    _worldPanelWasOpen = false;
                }

                TickCharacterPanel(fejd);
                TickWorldPanel(fejd);
            }
            catch (Exception e)
            {
                // Once, and then never again. The menu is the one screen where a per-frame
                // exception would be a wall of log and a dead main menu.
                if (!_complained)
                {
                    _complained = true;
                    UnityEngine.Debug.LogWarning(
                        $"[ICanShowYouTheWorld] Could not add the menu's random-name button: {e.Message}");
                }

                _seenStartup = null;
            }
        }

        private static void TickCharacterPanel(FejdStartup fejd)
        {
            var panel = fejd.m_newCharacterPanel;
            bool open = panel != null && panel.activeInHierarchy;

            if (!open)
            {
                _characterPanelWasOpen = false;
                return;
            }

            // Everything below is once per OPENING, not once per frame: the clone survives in the
            // panel's hierarchy, and a name the player has started typing must never be replaced.
            if (_characterPanelWasOpen) return;

            // The latch is set only once the field has been FOUND, so a panel that is active a
            // frame before its references are wired gets looked at again rather than skipped for
            // the life of the scene. Retrying costs two reflection lookups a frame, and only while
            // this panel is open.
            object field = Field(fejd, "m_csNewCharacterName");
            if (field == null) return;

            _characterPanelWasOpen = true;

            // Only when empty. Re-rolling over a half-typed name would make the button useless and
            // the panel hostile.
            if (string.IsNullOrEmpty(ReadText(field))) WriteText(field, CharacterName());

            EnsureButton(fejd.m_csNewCharacterCancel, () => WriteText(field, CharacterName()));
        }

        private static void TickWorldPanel(FejdStartup fejd)
        {
            var panel = fejd.m_createWorldPanel;
            bool open = panel != null && panel.activeInHierarchy;

            if (!open)
            {
                _worldPanelWasOpen = false;
                return;
            }

            if (_worldPanelWasOpen) return;

            object name = Field(fejd, "m_newWorldName");
            object seed = Field(fejd, "m_newWorldSeed");
            if (name == null) return;   // See TickCharacterPanel: latch after the find, not before.

            _worldPanelWasOpen = true;

            if (string.IsNullOrEmpty(ReadText(name))) WriteText(name, WorldName());

            // The seed comes pre-filled by the game with a random one, so it is left alone unless
            // it is somehow empty. Rolling it is still offered by the button, because a fresh world
            // on the SAME seed is the wrong kind of fresh for a placement test - Thjalfi's shore,
            // the Herald's run and every biome bearing are functions of the seed.
            if (seed != null && string.IsNullOrEmpty(ReadText(seed))) WriteText(seed, Seed());

            EnsureButton(fejd.m_newWorldDone, () =>
            {
                WriteText(name, WorldName());
                if (seed != null) WriteText(seed, Seed());
            });
        }

        /// <summary>
        /// Clones one of the panel's own buttons so the new one is styled, scaled and parented
        /// exactly like the game's, and puts it directly above the template.
        /// </summary>
        /// <remarks>
        /// A cloned button rather than a built one, and an IMGUI overlay least of all: the panel's
        /// buttons are already positioned by a layout the mod cannot see, already scaled to the
        /// display, and already themed. Anything hand-placed would have to guess all three and
        /// would guess differently on every resolution — the same argument that keeps the version
        /// badge inside the game's own label.
        ///
        /// Position is taken from the TEMPLATE's own rect, not from a constant, so it follows the
        /// panel wherever the layout puts it. The chosen offset is logged once precisely because it
        /// is the one number here that can only be judged by looking at it.
        /// </remarks>
        private static void EnsureButton(Button template, UnityEngine.Events.UnityAction onClick)
        {
            if (template == null) return;

            var parent = template.transform.parent;
            if (parent == null) return;
            if (parent.Find(CloneName) != null) return;   // already added this scene

            var clone = UnityEngine.Object.Instantiate(template.gameObject, parent);
            clone.name = CloneName;

            var button = clone.GetComponent<Button>();
            if (button == null)
            {
                UnityEngine.Object.Destroy(clone);
                return;
            }

            // A fresh event object, not RemoveAllListeners(): that clears only runtime listeners
            // and leaves the PERSISTENT ones the prefab was serialised with - so the clone would
            // still cancel character creation, on top of rolling a name.
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(onClick);
            button.interactable = true;

            StripLocalization(clone);
            Relabel(clone, "Random");

            var rect = clone.GetComponent<RectTransform>();
            var from = template.GetComponent<RectTransform>();
            if (rect != null && from != null)
            {
                rect.anchoredPosition = from.anchoredPosition + new Vector2(0f, from.rect.height + 8f);
                UnityEngine.Debug.Log($"[ICanShowYouTheWorld] DEV: [Random] button added above " +
                                      $"'{template.name}' at {rect.anchoredPosition}.");
            }
        }

        /// <summary>
        /// Removes the cloned button's Localize component, if it has one.
        /// </summary>
        /// <remarks>
        /// Valheim localises menu text through a component that rewrites the label from a token, and
        /// it does so after Awake. Left in place it would put the template's own word back and the
        /// button would read "Cancel" while doing something else - the worst possible outcome for a
        /// button. Found by type NAME because Localization lives in a game assembly whose types this
        /// file has no reason to reference.
        /// </remarks>
        private static void StripLocalization(GameObject clone)
        {
            foreach (var component in clone.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                if (component.GetType().Name.IndexOf("Localize", StringComparison.OrdinalIgnoreCase) < 0) continue;

                UnityEngine.Object.Destroy(component);
            }
        }

        /// <summary>
        /// Writes the button's caption into whichever text component the prefab uses.
        /// </summary>
        /// <remarks>
        /// By duck typing rather than by type, because the label is a TMPro component and this
        /// assembly does not reference TextMeshPro. "Has a writable string property called text" is
        /// true of TMP_Text and of UnityEngine.UI.Text alike, which also means this keeps working if
        /// the game ever swaps one for the other.
        /// </remarks>
        private static void Relabel(GameObject clone, string caption)
        {
            foreach (var component in clone.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;

                var prop = component.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
                if (prop == null || prop.PropertyType != typeof(string) || !prop.CanWrite) continue;

                prop.SetValue(component, caption, null);
            }
        }

        // --- the input fields, reflectively ---

        private static object Field(FejdStartup fejd, string name)
        {
            var field = typeof(FejdStartup).GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return null;

            var value = field.GetValue(fejd);

            // A UnityEngine.Object, so an unassigned one is a live-looking null.
            if (value == null || (UnityEngine.Object)value == null) return null;

            return value;
        }

        private static string ReadText(object inputField)
        {
            var prop = inputField.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            return prop == null || !prop.CanRead ? null : prop.GetValue(inputField, null) as string;
        }

        private static void WriteText(object inputField, string value)
        {
            var prop = inputField.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            if (prop == null || !prop.CanWrite) return;

            prop.SetValue(inputField, value, null);
        }

        // --- the names ---

        private static readonly string[] Heads =
        {
            "Bjor", "Ulf", "Sig", "Hald", "Ivar", "Rag", "Ast", "Sten", "Hak", "Ein",
            "Grim", "Sval", "Hrol", "Frey", "Bald", "Gunn", "Kjel", "Orm", "Vign", "Thrand",
        };

        private static readonly string[] Tails =
        {
            "nar", "rik", "mund", "vald", "dis", "run", "gar", "sten", "ulf", "bjorn",
            "hild", "thra", "vor", "grim", "laug", "frid", "mar", "ketil",
        };

        private static readonly string[] Lands =
        {
            "Mid", "Jotun", "Nifl", "Vana", "Glad", "Brei", "Svart", "Utg", "Alf", "Nor",
            "Hrim", "Ida", "Vig", "Myrk", "Thrym",
        };

        private static readonly string[] Places =
        {
            "gard", "heim", "holt", "fell", "vik", "dal", "fjord", "mark", "strand", "berg",
            "moor", "hollow", "reach", "watch",
        };

        private static string Pick(string[] from) => from[Rng.Next(from.Length)];

        private static string CharacterName() => Pick(Heads) + Pick(Tails);

        /// <summary>
        /// A world name the game will actually accept.
        /// </summary>
        /// <remarks>
        /// Checked against <c>World.HaveWorld</c>, because a collision is not a cosmetic problem:
        /// <c>OnNewWorldDone</c> refuses the whole creation with "$menu_newworldalreadyexists", and
        /// on a machine with sixty test worlds the two-syllable space collides sooner than it looks.
        /// After a few tries a number is appended, which always terminates.
        /// </remarks>
        private static string WorldName()
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                string candidate = Pick(Lands) + Pick(Places);
                if (!Taken(candidate)) return candidate;
            }

            return Pick(Lands) + Pick(Places) + Rng.Next(100, 1000);
        }

        private static bool Taken(string worldName)
        {
            try { return World.HaveWorld(worldName); }
            catch { return false; }   // Cannot ask, so do not refuse a perfectly good name.
        }

        private const string SeedAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

        /// <summary>Ten characters, which is what the game's own seed field generates.</summary>
        private static string Seed()
        {
            var seed = new char[10];
            for (int i = 0; i < seed.Length; i++) seed[i] = SeedAlphabet[Rng.Next(SeedAlphabet.Length)];
            return new string(seed);
        }
    }
}
