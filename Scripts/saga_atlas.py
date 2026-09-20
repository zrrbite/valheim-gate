# -*- coding: utf-8 -*-
"""Generates the Saga Atlas - a one-page overview of every questline in the saga.

    python Scripts/saga_atlas.py          # writes saga-atlas.html beside the repo

Run it from the repo root. Publish the result as an Artifact and REPLACE the existing
page rather than making a new one, so the link the owner has keeps working:
    https://claude.ai/artifact/8EvSbu7GH5SQ1Fq9Md83ca

Why a generator and not a hand-written page. Every lane, step, target and track is read
out of RunService.cs and the track is resolved the same way the game resolves it - an
explicit Track wins, else KillPrefab means HUNT and everything else means CRAFT. So the
page cannot drift from the code, and an act that gains a step gains it here too without
anybody remembering to. The only hand-written parts are the prose (epigraphs, chapters,
the log, what-is-next), which are judgement rather than data.

The one thing to keep honest by hand is the STATUS of each act in the ACTS table below
and the version in the page's meta line. Both are claims about the world, not facts in
the source, and a stale "played through" is exactly the kind of lie this project treats
as worse than a missing feature.
"""
import io, re, os, html

SRC = 'ICanShowYouTheWorld/RunMode/Unity/RunService.cs'
OUT = os.environ.get('SAGA_ATLAS_OUT', 'saga-atlas.html')

s = io.open(SRC, encoding='utf-8').read()
names = io.open('ICanShowYouTheWorld/RunMode/SagaNames.cs', encoding='utf-8').read()
consts = dict(re.findall(r'public const string (\w+StepId) = "([^"]+)";', names))

i = s.index('TrackTable =')
labels = dict(re.findall(r'\((\w+),\s*"([^"]+)"\)', s[i:s.index('};', i)]))


def unesc(t):
    return (t.replace('\\u2019', u'’').replace('\\u2014', u'—')
             .replace('\\u2013', u'–').replace("\\'", "'"))


def chain(method):
    a = s.index('List<ChallengeDefinition> %s()' % method)
    body = s[a:s.index('\n        };', a)]
    steps = []
    for b in body.split('new ChallengeDefinition')[1:]:
        def f(pat, d=''):
            m = re.search(pat, b)
            return m.group(1).strip() if m else d
        ident = f(r'Id = ([^,]+),')
        ident = ident.strip('"') if ident.startswith('"') else consts.get(ident.split('.')[-1], ident)
        kind = f(r'Kind = ChallengeKind\.(\w+)')
        tr = f(r'Track = (\w+)')
        track = labels.get(tr, tr) if tr else (labels['HuntTrackId'] if kind == 'KillPrefab' else labels['CraftTrackId'])
        steps.append(dict(id=ident, kind=kind, track=track,
                          target=int(f(r'Target = (\d+)', '1')),
                          display=unesc(f(r'Display = "([^"]*)"')),
                          reward=unesc(f(r'RewardText = "([^"]*)"')),
                          losable=bool(re.search(r'FailAfterMinutes|FailAfter\b', b))))
    return steps


ACTS = [
    dict(n='I', t='The Stolen Light', m='MainQuestChain', st='played',
         ep='Something is taking the light from the meadows. Take it back.',
         ch="You came ashore alive, which nothing in this world had managed in an age, and the dark "
            "noticed you before anything else did. So you built: an axe, a fire, a bed you called "
            "yours. Every night the meadows whispered over it, counting. Then a raven landed and "
            "told you why you had been sent, and it was not for the antlered one.",
         cl="Eikthyr came down onto stones his own herd had paid for, and went out. The forest went "
            "on being hungry. Nobody had yet asked where it had been carrying everything it took."),
    dict(n='II', t='Where the Light Goes', m='BlackForestChain', st='written',
         ep='The forest has been fed for years. Meet what did the feeding.',
         ch="You went after the little ones instead of waiting for them. They had been carrying the "
            "meadows away for years, down under the roots to something that had never once come up "
            "to collect in person, and nothing had ever followed them home.",
         cl="The Elder burned, and everything it had been fed went out with it. So that was where "
            "the light had gone: nowhere. The world was darker than the day you landed."),
    dict(n='III', t='Nothing Stays Buried', m='SwampChain', st='written',
         ep='What the marsh takes, it keeps.',
         ch="The marsh had taken for longer than the forest and had never spent a thing. You waded "
            "in after what it was holding and found out what happens to the ones who stay.",
         cl="Nothing in the marsh had been carrying light anywhere. It had simply been kept — the "
            "first hoard you had found that nobody was using."),
    dict(n='IV', t='The White Silence', m='MountainChain', st='written',
         ep='Above the treeline, even light freezes.',
         ch="Above the treeline nothing moved and nothing rotted and nothing was hungry. The cold "
            "had been holding what it took for so long that it had stopped behaving like a thief.",
         cl="Moder fell out of her own sky and the mountain gave up what it had been keeping. It was "
            "still warm, which was worse than any answer you had had so far."),
    dict(n='V', t='The Golden Ruin', m='PlainsChain', st='written',
         ep='They harvested a god’s herd before you. See how it ended.',
         ch="Somebody had done all of this before you. The plains were built out of their leavings, "
            "and the fuling had put their huts on top without asking what any of it had been for.",
         cl="The first harvesters were dust in the fields they had cleared, and you were standing in "
            "their answer. It had not worked for them either."),
    dict(n='VI', t='A Light to Carry', m='MistlandsChain', st='thin',
         ep='The dvergr borrow light and give it back. Learn how.',
         ch="In the mist there were lamps, and the lamps were not stolen. The dvergr had worked out "
            "how to borrow light and hand it back, and had no intention of explaining it.",
         cl=''),
    dict(n='VII', t='The Last Light', m='AshlandsChain', st='thin',
         ep='Where light goes to end. Follow it in.',
         ch="Every thread you had pulled ran the same direction, and it ran here, where everything "
            "has already burned once. You followed it in.",
         cl=''),
    dict(n='VIII', t='What the Cold Keeps', m='DeepNorthChain', st='placeholder',
         ep='Ice does not take light. It keeps it. Find out from whom.',
         ch="Nothing in the far north has thawed since before the herd, and the ice is not hungry "
            "and never was. It has only been keeping something.",
         cl=''),
]

STATUS = {
    'played':      ('Written &amp; played', 'ok'),
    'written':     ('Written, not yet played', 'mid'),
    'thin':        ('Thin stand-in', 'thin'),
    'placeholder': ('Placeholder', 'no'),
}
SLUG = {'HUNT': 'hunt', 'CRAFT': 'craft', 'HEARTH': 'hearth', 'FORGE': 'forge', 'MARSH': 'marsh'}

total = 0
acts_html = []
for a in ACTS:
    steps = chain(a['m'])
    total += len(steps)
    lanes = {}
    for st in steps:
        lanes.setdefault(st['track'], []).append(st)

    lane_html = []
    for track, lst in lanes.items():
        rows = []
        for k, st in enumerate(lst):
            boss = st['kind'] == 'KillPrefab' and st is lst[-1] and track == 'HUNT'
            cls = ' class="boss"' if boss else (' class="lose"' if st['losable'] else '')
            tgt = ('<span class="tgt">%d</span>' % st['target']) if st['target'] > 1 else ''
            rows.append('<li%s><span class="sn">%d</span><span class="sd">%s</span>%s</li>'
                        % (cls, k + 1, html.escape(st['display']), tgt))
        lane_html.append(
            '<div class="lane %s"><p class="lbl">%s <span class="ct">%d</span></p><ol>%s</ol></div>'
            % (SLUG.get(track, 'craft'), track, len(lst), ''.join(rows)))

    label, tone = STATUS[a['st']]
    acts_html.append("""
  <article class="act" id="act%s">
    <header>
      <p class="num">Act %s</p>
      <h2>%s</h2>
      <p class="badges"><span class="badge %s">%s</span><span class="badge n">%d steps</span><span class="badge n">%d tracks</span></p>
      <p class="ep">&ldquo;%s&rdquo;</p>
    </header>
    <p class="chap">%s</p>
    <div class="lanes">%s</div>
    %s
  </article>""" % (
        a['n'], a['n'], html.escape(a['t']), tone, label, len(steps), len(lanes),
        html.escape(a['ep']), html.escape(a['ch']), ''.join(lane_html),
        ('<p class="close"><span>Chapter ends</span>%s</p>' % html.escape(a['cl'])) if a['cl'] else ''))

PAGE = u"""<title>Saga Atlas</title>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Alegreya:ital,wght@0,400;0,700;1,400&family=Alegreya+Sans:wght@400;500;700&family=IBM+Plex+Mono:wght@400;600&display=swap">
<style>
  :root {
    --ground:#eceeec; --panel:#fbfcfb; --sunk:#e2e6e3; --ink:#1c2220; --soft:#55605c; --faint:#818b87;
    --rule:#ccd3cf; --gold:#8c6a15; --gold2:#6f5307; --accent-wash:#f3ecd8;
    --hunt:#993a26; --craft:#8c6a15; --hearth:#4f6b38; --forge:#8a5124; --marsh:#2f6b60;
    --ok:#3f6b2c; --mid:#8c6a15; --thin:#6a6f7a; --no:#993a26;
    --plate:#12161a; --plate-edge:#2b333d; --plate-gold:#cf9f37;
    --shadow:0 1px 2px rgba(20,30,26,.06), 0 8px 20px -14px rgba(20,30,26,.22);
  }
  @media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) {
    --ground:#0f1214; --panel:#171b1f; --sunk:#12161a; --ink:#dfe4e2; --soft:#9aa39f; --faint:#6f7874;
    --rule:#282f34; --gold:#dfae44; --gold2:#f0c96f; --accent-wash:#1d1f19;
    --hunt:#d4735a; --craft:#dfae44; --hearth:#93b36f; --forge:#c98c56; --marsh:#5ea99a;
    --ok:#8cb36a; --mid:#dfae44; --thin:#8d949e; --no:#d4735a;
    --shadow:0 1px 2px rgba(0,0,0,.45), 0 10px 26px -14px rgba(0,0,0,.75);
  } }
  :root[data-theme="dark"] {
    --ground:#0f1214; --panel:#171b1f; --sunk:#12161a; --ink:#dfe4e2; --soft:#9aa39f; --faint:#6f7874;
    --rule:#282f34; --gold:#dfae44; --gold2:#f0c96f; --accent-wash:#1d1f19;
    --hunt:#d4735a; --craft:#dfae44; --hearth:#93b36f; --forge:#c98c56; --marsh:#5ea99a;
    --ok:#8cb36a; --mid:#dfae44; --thin:#8d949e; --no:#d4735a;
    --shadow:0 1px 2px rgba(0,0,0,.45), 0 10px 26px -14px rgba(0,0,0,.75);
  }

  body { background:var(--ground); color:var(--ink); margin:0; padding:0 16px;
         font-family:"Alegreya Sans",system-ui,sans-serif; font-size:16px; line-height:1.55; }
  .wrap { max-width:60rem; margin:0 auto; padding-block:40px 80px; }
  h1,h2,h3 { font-family:"Alegreya",Georgia,serif; margin:0; text-wrap:balance; }
  h1 { font-size:clamp(2rem,6.5vw,3rem); font-weight:700; line-height:1.08; letter-spacing:-.01em; }
  h2 { font-size:1.6rem; font-weight:700; line-height:1.15; }
  h3 { font-size:1.15rem; font-weight:700; }
  p { margin:.65rem 0; }
  a { color:var(--gold2); }

  .eyebrow, .num, .lbl, .badge, code, .sn, .tgt, .ct, .meta {
    font-family:"IBM Plex Mono",ui-monospace,monospace; }
  .eyebrow { font-size:.68rem; letter-spacing:.2em; text-transform:uppercase; color:var(--gold); margin:0 0 .8rem; }
  .lede { font-family:"Alegreya",Georgia,serif; font-size:1.2rem; font-style:italic; color:var(--soft); max-width:36rem; }
  .meta { font-size:.72rem; color:var(--faint); margin-top:1.2rem; }
  code { font-size:.82em; background:var(--sunk); padding:.1em .38em; border-radius:3px; }

  /* headline figures */
  .tally { display:grid; grid-template-columns:repeat(auto-fit,minmax(104px,1fr)); gap:10px; margin:1.8rem 0 0; }
  .tally div { background:var(--panel); border:1px solid var(--rule); border-radius:4px; padding:12px 14px; }
  .tally b { font-family:"Alegreya",serif; font-size:1.7rem; font-weight:700; display:block; line-height:1;
             color:var(--gold2); font-variant-numeric:tabular-nums; }
  .tally span { font-family:"IBM Plex Mono",monospace; font-size:.64rem; letter-spacing:.12em;
                text-transform:uppercase; color:var(--faint); }

  hr { border:0; border-top:1px solid var(--rule); margin:3rem 0; }
  section { margin-block:3rem; }

  /* the dark plate for graph figures */
  .plate { background:var(--plate); border:1px solid var(--plate-edge); border-radius:3px;
           padding:18px 10px 12px; margin-block:1.2rem; overflow-x:auto; }
  .plate .cap { font-family:"IBM Plex Mono",monospace; font-size:.62rem; letter-spacing:.16em;
                text-transform:uppercase; color:var(--plate-gold); padding:0 8px .7rem; margin:0; }
  .plate pre.mermaid { background:transparent; margin:0; min-width:290px; }

  /* ---- act cards ---- */
  .act { background:var(--panel); border:1px solid var(--rule); border-radius:5px;
         padding:22px 18px; margin-block:18px; box-shadow:var(--shadow); }
  .act header { border-bottom:1px solid var(--rule); padding-bottom:14px; }
  .num { font-size:.68rem; letter-spacing:.2em; text-transform:uppercase; color:var(--gold); margin:0 0 .15rem; }
  .badges { display:flex; flex-wrap:wrap; gap:6px; margin:.6rem 0 0; }
  .badge { font-size:.63rem; letter-spacing:.09em; text-transform:uppercase; padding:.2em .5em;
           border-radius:3px; border:1px solid currentColor; }
  .badge.ok{color:var(--ok)} .badge.mid{color:var(--mid)} .badge.thin{color:var(--thin)} .badge.no{color:var(--no)}
  .badge.n { color:var(--faint); }
  .ep { font-family:"Alegreya",serif; font-style:italic; color:var(--gold2); margin:.7rem 0 0; }
  .chap { font-family:"Alegreya",Georgia,serif; font-size:1.02rem; color:var(--soft); margin:1rem 0 0; max-width:42rem; }
  .close { font-family:"Alegreya",Georgia,serif; font-size:.97rem; color:var(--soft); font-style:italic;
           border-left:2px solid var(--rule); padding-left:12px; margin:1.2rem 0 0; }
  .close span { display:block; font-family:"IBM Plex Mono",monospace; font-style:normal; font-size:.6rem;
                letter-spacing:.16em; text-transform:uppercase; color:var(--faint); margin-bottom:.25rem; }

  /* ---- the lanes: parallel questline tracks ---- */
  .lanes { display:grid; grid-template-columns:repeat(auto-fit,minmax(240px,1fr)); gap:16px; margin-top:20px; }
  .lane { min-width:0; }
  .lbl { font-size:.66rem; letter-spacing:.16em; margin:0 0 .5rem; padding-bottom:.35rem;
         border-bottom:2px solid currentColor; display:flex; justify-content:space-between; align-items:baseline; }
  .ct { font-size:.66rem; opacity:.65; }
  .lane.hunt .lbl{color:var(--hunt)} .lane.craft .lbl{color:var(--craft)}
  .lane.hearth .lbl{color:var(--hearth)} .lane.forge .lbl{color:var(--forge)} .lane.marsh .lbl{color:var(--marsh)}
  .lane ol { list-style:none; margin:0; padding:0; }
  .lane li { display:flex; gap:8px; align-items:baseline; padding:.3rem 0;
             font-size:.9rem; border-bottom:1px dotted var(--rule); }
  .sn { font-size:.68rem; color:var(--faint); min-width:1.3rem; font-variant-numeric:tabular-nums; }
  .sd { flex:1; min-width:0; }
  .tgt { font-size:.66rem; color:var(--faint); background:var(--sunk); padding:.05em .35em; border-radius:3px;
         font-variant-numeric:tabular-nums; }
  .lane li.boss .sd { font-weight:700; }
  .lane.hunt li.boss .sd { color:var(--hunt); }
  .lane li.lose .sd::after { content:" \\2014 losable"; font-size:.7rem; font-style:italic; color:var(--no); }

  /* ---- log ---- */
  ul.log { list-style:none; padding:0; margin:0; display:grid; gap:2px; }
  ul.log li { display:grid; grid-template-columns:5.6rem 1fr; gap:10px; padding:.42rem 0;
              border-bottom:1px solid var(--rule); font-size:.92rem; align-items:baseline; }
  ul.log .d { font-family:"IBM Plex Mono",monospace; font-size:.7rem; color:var(--faint); }
  ul.log li.today .d { color:var(--gold2); }

  ul.next { list-style:none; padding:0; margin:.8rem 0 0; display:grid; gap:10px; }
  ul.next li { padding-left:1.6rem; position:relative; font-size:.95rem; }
  ul.next li::before { content:"\\25B8"; position:absolute; left:0; color:var(--gold); }
  ul.next li.block::before { content:"\\25CF"; color:var(--no); }

  .note { background:var(--accent-wash); border:1px solid var(--rule); border-radius:4px;
          padding:14px 16px; margin:1.4rem 0; font-size:.94rem; }
  .note b { color:var(--gold2); }
  @media (prefers-reduced-motion:reduce){*{animation:none!important;transition:none!important}}
</style>

<div class="wrap">

  <p class="eyebrow">Valheim &middot; Run Mode &middot; branch feature/run-mode</p>
  <h1>Saga Atlas</h1>
  <p class="lede">Every questline in the saga, lane by lane, with the story each act is telling
  and an honest note on how finished it is.</p>
  <p class="meta">Generated from RunService.cs at 1.0.15-run.2026-09-20y &middot; 20 September 2026</p>

  <div class="tally">
    <div><b>__TOTAL__</b><span>quest steps</span></div>
    <div><b>8</b><span>acts</span></div>
    <div><b>1</b><span>played through</span></div>
    <div><b>5</b><span>tracks in use</span></div>
  </div>

  <div class="note">
    <b>How to read a lane.</b> An act runs two or three questlines <em>in parallel</em> — HUNT is what
    the world makes you do, CRAFT is what you make, and the third lane is that act&rsquo;s own domestic
    thread (HEARTH in the Meadows, FORGE in the forest, MARSH in the fens). Each lane is strictly
    linear with no skips, so a lane can stall without stopping the others. A number in a chip is the
    count the step wants. The bold last step of HUNT is the act&rsquo;s god.
  </div>

  <hr>

  <section>
    <h2>The arc</h2>
    <p>Acts II to VII are each a <em>failed answer</em> to the same shortage, and the question one act
    fails to answer is the next act. That is the whole spine, and it is now told to the player one
    chapter at a time in the BOOK rather than living only in the design notes.</p>
    <div class="plate">
      <p class="cap">Fig. 1 &mdash; what each act fails to answer</p>
<pre class="mermaid">
%%{init: {"theme":"base","themeVariables":{
 "background":"#12161a","primaryColor":"#1c242c","primaryTextColor":"#dfe4e2",
 "primaryBorderColor":"#41505e","lineColor":"#8a7a4e","fontSize":"13px",
 "fontFamily":"Alegreya Sans, system-ui, sans-serif","edgeLabelBackground":"#12161a"}}}%%
flowchart TD
  P["Nothing in this world<br/>can make its own light"]
  A1["I · The Stolen Light"]
  A2["II · Where the Light Goes"]
  A3["III · Nothing Stays Buried"]
  A4["IV · The White Silence"]
  A5["V · The Golden Ruin"]
  A6["VI · A Light to Carry"]
  A7["VII · The Last Light"]
  P --> A1
  A1 -- "who is taking it?" --> A2
  A2 -- "spent on nothing" --> A3
  A3 -- "kept, not used" --> A4
  A4 -- "frozen, still warm" --> A5
  A5 -- "others tried and died" --> A6
  A6 -- "borrowed, from whom?" --> A7
  classDef gold fill:#241f14,stroke:#8a6f2a,color:#f2c85e;
  classDef thin fill:#191d22,stroke:#39414c,color:#98a0a8;
  class P,A1 gold;
  class A6,A7 thin;
</pre>
    </div>
  </section>

  <section>
    <h2>The light economy</h2>
    <p>Rescued lights are the only currency the two lanes share, and they exist so the hunt and the
    crafts owe each other something. Acts I and II only — nothing later asks for one, deliberately,
    because a craft nobody can finish is a stalled act.</p>
    <div class="plate">
      <p class="cap">Fig. 2 &mdash; where a light comes from, and what spends it</p>
<pre class="mermaid">
%%{init: {"theme":"base","themeVariables":{
 "background":"#12161a","primaryColor":"#1c242c","primaryTextColor":"#dfe4e2",
 "primaryBorderColor":"#41505e","lineColor":"#8a7a4e","fontSize":"13px",
 "fontFamily":"Alegreya Sans, system-ui, sans-serif","edgeLabelBackground":"#12161a"}}}%%
flowchart LR
  pale["The pale light<br/>+1"]
  race["Night races<br/>+1 each"]
  gath["The Gatherer's hoard<br/>freed on its death"]
  shade["The shade's kept light<br/>+1"]
  cour["Act II couriers<br/>+4"]
  L(("Rescued<br/>light"))
  bow["Thor's bow<br/>-3"]
  anv["The Storm-Anvil<br/>-1, stays in"]
  shd["The Stormward<br/>-3"]
  pale --> L
  race --> L
  gath --> L
  shade --> L
  cour --> L
  L --> bow
  L --> anv
  L --> shd
  classDef spark fill:#16252b,stroke:#4e8f9f,color:#9fdcea;
  classDef gold fill:#241f14,stroke:#8a6f2a,color:#f2c85e;
  class L spark;
  class bow,anv,shd gold;
</pre>
    </div>
    <p>The shade&rsquo;s single kept light is load-bearing beyond its value: Valheim only lists a recipe or
    a build piece whose every ingredient the player has <em>handled</em>, so that one item is what makes
    both Thor&rsquo;s bow and the Storm-Anvil appear at all.</p>
  </section>

  <hr>

  <section>
    <h2>The questlines</h2>
__ACTS__
  </section>

  <hr>

  <section>
    <h2>The log</h2>
    <p>Two days of building, newest first. Every line shipped as its own installed build.</p>
    <ul class="log">
      <li class="today"><span class="d">20 Sep</span><span>Thjalfi — a named ghost who raises the Storm-Anvil for stone and a light</span></li>
      <li class="today"><span class="d">20 Sep</span><span>Pool tasks for Thor&rsquo;s bow and the Stormward, gated on carrying them</span></li>
      <li class="today"><span class="d">20 Sep</span><span>First play: the anvil was never spawned, and the shield&rsquo;s lightning was invisible — both fixed</span></li>
      <li class="today"><span class="d">20 Sep</span><span>The storm eats the shield — each discharge costs durability</span></li>
      <li class="today"><span class="d">20 Sep</span><span>Review of the anvil work: the re-cost retries, the knockback stops scaling an unknown</span></li>
      <li class="today"><span class="d">20 Sep</span><span>The Storm-Anvil is raised in Act I, for a light</span></li>
      <li class="today"><span class="d">20 Sep</span><span>The Storm-Anvil combine, and two beats so the shield quest is worth doing</span></li>
      <li class="today"><span class="d">20 Sep</span><span>The Stormward is huge, and answers being hit with lightning</span></li>
      <li class="today"><span class="d">20 Sep</span><span>The BOOK becomes a book; the GM tab gets its layout back</span></li>
      <li class="today"><span class="d">20 Sep</span><span>The shade comes back after the Breaker and after Eikthyr</span></li>
      <li class="today"><span class="d">20 Sep</span><span>The shade hands over a light — the missing bow recipe, solved</span></li>
      <li class="today"><span class="d">20 Sep</span><span>The dev clock is +2h per press again, readout split off</span></li>
      <li class="today"><span class="d">20 Sep</span><span>Alphabetical crafting — already in the game, just unused</span></li>
      <li class="today"><span class="d">20 Sep</span><span>Thor&rsquo;s bow forks; centre text wraps</span></li>
      <li class="today"><span class="d">20 Sep</span><span>GM becomes a flavour baked into the DLL; the menu is the launcher</span></li>
      <li class="today"><span class="d">20 Sep</span><span>A QUESTS page, and a saga menu that opens itself</span></li>
      <li class="today"><span class="d">20 Sep</span><span>The Stormsworn — one piece of armour per act, Acts II to V</span></li>
      <li><span class="d">19 Sep</span><span>LevelEffects was eating the creature dressing</span></li>
      <li><span class="d">19 Sep</span><span>The quest hints speak, and Hugin announces the recipes</span></li>
      <li><span class="d">19 Sep</span><span>The Stormward — Act I&rsquo;s last craft, and what the troll&rsquo;s hide is for</span></li>
      <li><span class="d">19 Sep</span><span>The Breaker — Act I&rsquo;s troll, and the one fight the forest joins</span></li>
      <li><span class="d">19 Sep</span><span>Act I opens with an errand, a kill that gives nothing, and a vigil</span></li>
      <li><span class="d">19 Sep</span><span>Keys scoped per mode; the entry point moves to FejdStartup.Start</span></li>
    </ul>
  </section>

  <section>
    <h2>What is next</h2>
    <ul class="next">
      <li><b>Answered:</b> the Storm-Anvil&rsquo;s prefab is <code>incinerator</code>, printed by a running
      game at last. The mod still finds it by component, because a component search cannot go stale.</li>
      <li>Thor&rsquo;s bow moved off the workbench onto the anvil — now that the anvil is a place you walk to,
      this is what makes the walk matter.</li>
      <li>The Stormsworn bound at the anvil, one piece per act.</li>
      <li>The Stormward <em>reforged</em> in Act III with iron and ancient bark — not moved there, so Act I
      keeps the capstone craft it was built to have.</li>
      <li>Act II has never been reached in a play-test. <code>forge</code> is still an unverified station name.</li>
      <li>Search at the crafting bench, parked: alphabetical sorting turned out to be most of what it was for.</li>
      <li>A tint or glow for the saga&rsquo;s own items, to be written from the probe dump rather than guessed.</li>
    </ul>
  </section>

</div>
"""

PAGE = PAGE.replace('__ACTS__', ''.join(acts_html)).replace('__TOTAL__', str(total))
io.open(OUT, 'w', encoding='utf-8', newline='\n').write(PAGE)
print('wrote %s  (%d steps across %d acts)' % (OUT, total, len(ACTS)))
