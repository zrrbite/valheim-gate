# The saga's reward — "The Saga of <character>"

Written 2026-10-05. Owner: *"Wouldn't it be awesome if we rewarded the player with a link to the saga
story you're writing, as a reward for completing the game?"* — and then chose **their own saga, written
locally** over a public link or reading it in-game.

## What the player gets

When a run is **won** — the final boss falls (`FinishRun`, Yagluth by default) — the mod writes an HTML
page, **The Saga of <character>**, next to the saga's own files (`Application.persistentDataPath`, the
same folder as the config), and opens it in the browser. The run's last message says the skalds have
finished it. An abandoned run gets nothing: the reward is for finishing.

The page is the myth (`docs/THE-SAGA.md`, embedded in the DLL at build time) **cut at the act the run
ended in**, woven with the run's own record:

1. A title page — *The Saga of <character>* — with the date and the way taken.
2. The myth's prologue.
3. Each act's tale, in order, **up to and including the run's final act** — never past it. A run that
   ends at Yagluth never learns of the lantern-keeper.
4. Under each tale, **"What was told of you"**: that act's chronicle lines (the BOOK's record,
   `numeral|step|line`), in the order they happened.
5. A tally: the gods felled, time, heat, the saga score.
6. A close: the myth's epilogue **only** when the run reached the story's last act; otherwise a short
   coda that says more remains untold, without saying what.

The lobby gains **"Read your saga"** once this character has one; it reopens the newest page.

## Why local, why cut

A link would point at a private repo or a private page, would be the same for everyone, and would spoil
acts the player never reached. A local page is offline, private, theirs to keep or share, and no two
are alike.

## The code

- **`RunMode/SagaPage.cs` — pure, tested.**
  - `SagaTale.Split(markdown, actTitles)` → prologue, the tales matched to act titles (the myth's `##`
    headings ARE the act titles), epilogue. The myth's own italic note is dropped (it addresses the
    repo's reader, not the player).
  - `SagaPage.MarkdownToHtml(md)` — the subset the myth uses: `###` headings, paragraphs, `>`
    quotes, `*italic*`, `**bold**`, `---`; everything HTML-escaped first.
  - `SagaPage.Compose(input)` → the whole page (inline CSS: parchment, serif, readable on a phone;
    dark-mode aware).
- **`RunMode/Unity/SagaReward.cs`** — reads the embedded resource, gathers the run's facts, writes the
  file (`Saga of <character> - <yyyy-MM-dd HHmm>.html`, name sanitised), opens it with
  `Application.OpenURL` on a `file://` URI; `Latest(character)` for the lobby button.
- **`RunService.FinishRun`** — composes the page BEFORE `EndRun` (which clears the chronicle).
- **csproj** — `<EmbeddedResource Include="..\docs\THE-SAGA.md" LogicalName="ICanShowYouTheWorld.TheSaga.md">`,
  so the page is always the current story; no copy to drift.
- **Run-start check** — every act title has a matching tale in the embedded myth; logged if not.

## Testing

`SagaPageTests`: the split finds prologue/tales/epilogue; a run ending at act index 4 contains Act V's
tale and **not** Act VI's or the epilogue; a run ending at the last act gets the epilogue; chronicle
lines land under their own act; HTML is escaped; the markdown subset converts.

## Not in this work

Publishing the story online; a per-boon or per-death account in the page (the chronicle is the record).

## Addendum, same day: the saga so far, from the BOOK

The owner asked whether the BOOK (the Run window's chronicle) and the saga page should be one thing,
and left it to judgement ("Do what you think is best. Keep them seperate?"). Ruling: **two texts, linked.**

- The BOOK stays the short record, read mid-fight: each act's opening passage and closing line, the
  deeds, the live quests. The myth's ~2,000 words an act would bury it, and reads better in a browser.
- Each god felled writes that act's chapter of the myth. The BOOK's title page then says "The skalds
  have written your saga as far as “<title>”" with a **Read your saga so far** button
  (`RunService.ReadSagaSoFar`), and the god's fall says so in the chat log (only there: the card, the
  raven and the rungs already fill the screen at that moment).
- The page so far (`SagaPageInput.SoFar`) is cut at the last god felled. **The act being played is never
  told**, because its tale names the speakers and how each one ends. It has no epilogue and no reckoning,
  and closes on "the rest is not written yet". It is one file per character (`Saga of <name> - so far.html`),
  overwritten each time; the lobby's "Read your saga" ignores it and opens only finished sagas.
- One button, not one per chapter: a chapter link would need a `#fragment` on a `file://` URL, which
  browsers opened this way often drop.
