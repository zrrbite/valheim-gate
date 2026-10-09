using System;
using System.Collections.Generic;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The one owner of a weapon's damage while the run multiplies it: each weapon's damage as it was before any
    /// weapon boon touched it, kept once, and the live value always rewritten as that original x the product of the
    /// live factors (BoonEffects.RefreshWeaponDamage).
    /// </summary>
    /// <remarks>
    /// Anything else that changes a weapon's damage while its original is held must change it THROUGH this class
    /// (<see cref="Rebase"/>), or the next refresh writes the old original back over it. Thor's bow does (2026-10-08,
    /// the final review): its element switch and the Hunter's Moder tempering write the bow's own numbers, and
    /// written straight into the live block they lasted only until the next weapon-boon pick, respawn or Forge-fed
    /// heat change. Then the HUD said "fire" while the bow dealt the lightning it had when the boons first saw it,
    /// and a Hunter who laid the way down kept a x1.5 element the original had caught.
    ///
    /// Generic over the key and the damage, so the rule is tested without the game's types: in the game the key is
    /// the item's shared block and the value its damage struct.
    /// </remarks>
    public sealed class WeaponOriginals<TKey, TValue> where TKey : class
    {
        private readonly Dictionary<TKey, TValue> _originals = new Dictionary<TKey, TValue>();
        private readonly Func<TKey, TValue> _read;
        private readonly Action<TKey, TValue> _write;
        private readonly Func<TValue, float, TValue> _scaled;

        /// <param name="read">The live damage, copied.</param>
        /// <param name="write">Writes a damage into the live block.</param>
        /// <param name="scaled">A damage times a factor.</param>
        public WeaponOriginals(Func<TKey, TValue> read, Action<TKey, TValue> write, Func<TValue, float, TValue> scaled)
        {
            _read = read ?? throw new ArgumentNullException(nameof(read));
            _write = write ?? throw new ArgumentNullException(nameof(write));
            _scaled = scaled ?? throw new ArgumentNullException(nameof(scaled));
        }

        /// <summary>How many weapons have an original held.</summary>
        public int Count => _originals.Count;

        /// <summary>True while this weapon's original is held.</summary>
        public bool Holds(TKey key) => key != null && _originals.ContainsKey(key);

        /// <summary>Takes the weapon's original, on first sight only: taken again, it would catch the boons' product.</summary>
        public void Remember(TKey key)
        {
            if (key == null || _originals.ContainsKey(key)) return;
            _originals[key] = _read(key);
        }

        /// <summary>Writes every held weapon as its original x <paramref name="product"/>.</summary>
        public void ApplyAll(float product)
        {
            foreach (var kvp in _originals) _write(kvp.Key, _scaled(kvp.Value, product));
        }

        /// <summary>
        /// Changes a weapon's own numbers under the boons: <paramref name="change"/> runs on the original, the result
        /// becomes the original, and the weapon is written as that x <paramref name="product"/>. With no original
        /// held, <paramref name="change"/> just runs. True when an original was rebased.
        /// </summary>
        /// <remarks>
        /// The change runs on the ORIGINAL, never on the live value: run on the live one, the boons' product would be
        /// taken into the new original and multiplied again by every refresh after. A change that throws leaves the
        /// original as it was and the weapon written from it.
        /// </remarks>
        public bool Rebase(TKey key, Action change, float product)
        {
            if (change == null) return false;
            if (key == null || !_originals.TryGetValue(key, out var original))
            {
                change();
                return false;
            }

            _write(key, original);
            try
            {
                change();
                _originals[key] = _read(key);
            }
            finally
            {
                _write(key, _scaled(_originals[key], product));
            }
            return true;
        }

        /// <summary>Puts every held weapon back to its original and forgets them all. The full unwind.</summary>
        public void RestoreAll()
        {
            foreach (var kvp in _originals) _write(kvp.Key, kvp.Value);
            _originals.Clear();
        }
    }
}
