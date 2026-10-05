# Act VII gets a voice — the charred one, and Last Light

Written 2026-10-05, after Acts II–VI. Act VII, *The Last Light*, was a five-step stand-in, reached
when `runFinalBossKey` is moved to Fader — and then it is the saga's ending, which had no closing
chapter at all (nor had Act VI's).

## Decisions taken (owner, 2026-10-05)

| Question | Answer |
|---|---|
| Who carries the act | **A charred one who remembers** |
| "Let it burn" | **Reforge Thor's bow** — the saga's last item quest closes its first |

## The charred one

One of the Ashlands' burned dead who kept their wits — the drowned one's rhyme. The game's
`Charred_Melee` body (fallbacks: the other charred, then the Ghost — `SagaSpeaker.BodyFallbacks`),
dark with an ember glow. Tame, and immune to non-player damage like every speaker.

- **Where:** where the player comes ashore — dry Ashlands ground near them the first time he is
  wanted (`as-arrive` done).
- **First words (meaning):** this is where light goes to end; everything here burned once; the drowned
  could not let go and the frozen still wait — burning is the only end that lets go. *Let it burn.*
- **The pyre:** a burning fire within 5 m of him (`FrozenOne.FireNear`), then Use with: **the bow**
  (Thor's bow if carried, else any bow — never a stalled saga), **10 flametal** (resolved at run start
  from `FlametalNew`/`Flametal`, logged), **3 lights** (rescued lights first, then wisps). Out comes
  **Last Light**.
- **After Fader:** "You can go home now. Whatever you carry back, carry it the way the dvergr do."

## Last Light

Thor's bow reforged. Thor's bow's Tune first, then pierce 80 (+6/level); the run's element at 44
(+6), plus a standing fire of 20 (+3) on top of whichever element it carries. `SagaItems.IsThorsBow`
makes every Thor's-bow hook — strike on impact, arrow flash, the Hunter's element cycle, the kill
count — apply to both. The bow pool tasks accept either bow (`RequiresItem` "A|B").

## The chain

CRAFT: the Queen's power → reach the Ashlands → **speak with the charred one** (`as-charred`) →
**carry Last Light** (`as-lastlight`, CollectItem). HUNT unchanged. **5 → 7 steps.** No third track.

## The closing chapters

Act VI's close (the lanterns, honest borrowing, and the question of where light goes in the end) and
Act VII's (it went here, to the end of it; Odin's question answered; the last of yours you let go of
yourself) — written into `ActDefinition.ChapterClose`.
