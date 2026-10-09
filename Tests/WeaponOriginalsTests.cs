using System;
using ICanShowYouTheWorld.RunMode;

/// <summary>
/// Thor's bow and the weapon boons on one damage block (2026-10-08, the final review): the boons keep the block's
/// original and rewrite it as original x their product; the bow's element switch and the Moder tempering write the
/// block's own numbers. The rule: after any interleaving of the three, the bow's damage is (its base with the
/// current element x the current scale) x the current product.
/// </summary>
static class WeaponOriginalsTests
{
    // The game's damage struct, cut down to what the bow carries.
    private struct Dmg
    {
        public float Pierce, Lightning, Fire, Frost;
    }

    // The game's shared item block: a class, so it is the key, and the struct inside is the value.
    private sealed class Block
    {
        public Dmg Damages;
    }

    private enum Element { Lightning, Fire, Frost }

    private const float Pierce = 44f, ElementBase = 22f;

    private static Dmg Scaled(Dmg d, float m) =>
        new Dmg { Pierce = d.Pierce * m, Lightning = d.Lightning * m, Fire = d.Fire * m, Frost = d.Frost * m };

    // SagaItems.ApplyBowElement, cut down: exactly one element, at the base x the scale; the pierce left as it is.
    private static void WriteElement(Block b, Element e, float scale)
    {
        b.Damages.Lightning = e == Element.Lightning ? ElementBase * scale : 0f;
        b.Damages.Fire = e == Element.Fire ? ElementBase * scale : 0f;
        b.Damages.Frost = e == Element.Frost ? ElementBase * scale : 0f;
    }

    private static Dmg Expected(Element e, float scale, float product)
    {
        var b = new Block { Damages = new Dmg { Pierce = Pierce } };
        WriteElement(b, e, scale);
        return Scaled(b.Damages, product);
    }

    private static bool Near(Dmg a, Dmg b) =>
        Math.Abs(a.Pierce - b.Pierce) < 0.001f && Math.Abs(a.Lightning - b.Lightning) < 0.001f &&
        Math.Abs(a.Fire - b.Fire) < 0.001f && Math.Abs(a.Frost - b.Frost) < 0.001f;

    private static string Show(Dmg d) => $"pierce {d.Pierce:0.##} lightning {d.Lightning:0.##} fire {d.Fire:0.##} frost {d.Frost:0.##}";

    private static WeaponOriginals<Block, Dmg> NewStore() =>
        new WeaponOriginals<Block, Dmg>(b => b.Damages, (b, d) => b.Damages = d, Scaled);

    private static Block NewBow()
    {
        var bow = new Block { Damages = new Dmg { Pierce = Pierce } };
        WriteElement(bow, Element.Lightning, 1f);
        return bow;
    }

    public static void Run()
    {
        // The defect as the review found it: the boons see the bow at lightning, the switch writes fire, the next
        // refresh writes the lightning back.
        var store = NewStore();
        var bow = NewBow();
        store.Remember(bow);
        store.ApplyAll(1.2f);
        Check.That(Near(bow.Damages, Expected(Element.Lightning, 1f, 1.2f)), "a weapon boon multiplies the bow as it was first seen");

        Check.That(store.Rebase(bow, () => WriteElement(bow, Element.Fire, 1f), 1.2f), "a held original is rebased");
        Check.That(Near(bow.Damages, Expected(Element.Fire, 1f, 1.2f)), "the switch to fire lands at once, under the boons' product");
        store.ApplyAll(1.5f);
        Check.That(Near(bow.Damages, Expected(Element.Fire, 1f, 1.5f)),
                   "and the next refresh keeps the fire - not the lightning it first saw: " + Show(bow.Damages));

        // The Moder tempering: the element x1.5, kept through refreshes.
        store.Rebase(bow, () => WriteElement(bow, Element.Fire, 1.5f), 1.5f);
        Check.That(Near(bow.Damages, Expected(Element.Fire, 1.5f, 1.5f)), "the tempering scales the element under the product");
        store.ApplyAll(1.2f);
        Check.That(Near(bow.Damages, Expected(Element.Fire, 1.5f, 1.2f)),
                   "and a refresh keeps the tempering: " + Show(bow.Damages));

        // A respec: the scale goes back to 1, and nothing brings the x1.5 back.
        store.Rebase(bow, () => WriteElement(bow, Element.Fire, 1f), 1.2f);
        store.ApplyAll(1.2f);
        Check.That(Near(bow.Damages, Expected(Element.Fire, 1f, 1.2f)),
                   "a Hunter who lays down the way keeps no x1.5 from an old original: " + Show(bow.Damages));

        // Rebasing never compounds the boons into the original: the pierce stays the base x the product.
        for (int i = 0; i < 10; i++) store.Rebase(bow, () => WriteElement(bow, Element.Frost, 1f), 2f);
        Check.That(Near(bow.Damages, Expected(Element.Frost, 1f, 2f)), "ten rebases under x2 leave the pierce at 88, not 44 x 2^10");

        // The full unwind puts back the CURRENT element, unmultiplied, and forgets the original.
        store.RestoreAll();
        Check.That(Near(bow.Damages, Expected(Element.Frost, 1f, 1f)), "the last weapon boon gone: the bow as it now is, frost, x1");
        Check.That(store.Count == 0 && !store.Holds(bow), "and nothing is held after the unwind");

        // With nothing held, the write is the write, as it was before the boons ever saw the bow.
        Check.That(!store.Rebase(bow, () => WriteElement(bow, Element.Lightning, 1f), 2f), "nothing held: no rebase");
        Check.That(Near(bow.Damages, Expected(Element.Lightning, 1f, 1f)), "and the change is written straight, without a product");
        Check.That(store.Count == 0, "and no original is taken by a write");

        // A write that throws leaves the bow as it was: its original, under the product.
        var fragile = NewStore();
        var bow2 = NewBow();
        fragile.Remember(bow2);
        fragile.ApplyAll(1.5f);
        bool threw = false;
        try
        {
            fragile.Rebase(bow2, () => { bow2.Damages.Fire = 999f; throw new InvalidOperationException("half a write"); }, 1.5f);
        }
        catch (InvalidOperationException) { threw = true; }
        Check.That(threw, "a throwing write still throws");
        Check.That(Near(bow2.Damages, Expected(Element.Lightning, 1f, 1.5f)), "and the bow keeps its original under the product: " + Show(bow2.Damages));
        fragile.ApplyAll(1.5f);
        Check.That(Near(bow2.Damages, Expected(Element.Lightning, 1f, 1.5f)), "nor did the half-write become the original");

        // The rule under any interleaving: switch, temper, refresh, unwind, in a fixed pseudo-random order.
        var rng = new Random(20261008);
        var s = NewStore();
        var b = NewBow();
        Element element = Element.Lightning;
        float scale = 1f, product = 1f;
        float[] products = { 1f, 1.2f, 1.5f, 1.8f, 2.5f };
        int broken = 0;
        string firstBreak = null;
        for (int step = 0; step < 2000; step++)
        {
            int op = rng.Next(10);
            string what;
            if (op < 3)
            {
                element = (Element)rng.Next(3);
                var e = element; var sc = scale;
                s.Rebase(b, () => WriteElement(b, e, sc), product);
                what = "switch to " + element;
            }
            else if (op < 5)
            {
                scale = scale == 1f ? 1.5f : 1f;
                var e = element; var sc = scale;
                s.Rebase(b, () => WriteElement(b, e, sc), product);
                what = "scale " + scale;
            }
            else if (op < 9)
            {
                product = products[rng.Next(products.Length)];
                s.Remember(b);
                s.ApplyAll(product);
                what = "refresh x" + product;
            }
            else
            {
                s.RestoreAll();
                product = 1f;
                what = "unwind";
            }

            if (!Near(b.Damages, Expected(element, scale, product)))
            {
                broken++;
                if (firstBreak == null)
                    firstBreak = $"step {step} ({what}): {Show(b.Damages)}, wanted {Show(Expected(element, scale, product))}";
            }
        }
        Check.That(broken == 0, "2000 interleaved switches, temperings, refreshes and unwinds: the bow is always " +
                                "(base, element, scale) x product" + (firstBreak == null ? "" : " - first broken at " + firstBreak));
    }
}
