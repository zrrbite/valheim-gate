using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>One thing the run did, as the BOOK recorded it: act numeral, step, the line said.</summary>
    public sealed class SagaDeed
    {
        public string ActNumeral;
        public string Step;
        public string Line;
    }

    /// <summary>
    /// The myth (docs/THE-SAGA.md) cut into its parts: the prologue, one tale per act, the epilogue.
    /// The myth's `##` headings ARE the act titles, which is the whole contract between the story
    /// file and this code; a title with no tale is reported in <see cref="Missing"/>, never guessed.
    /// </summary>
    public sealed class SagaTale
    {
        public string Prologue = string.Empty;
        public List<string> Tales = new List<string>();
        public string Epilogue = string.Empty;
        public List<string> Missing = new List<string>();

        public static SagaTale Split(string markdown, IList<string> actTitles)
        {
            var result = new SagaTale();
            var titles = actTitles ?? new string[0];
            var sections = new List<(string heading, string body)>();

            string heading = null;
            var body = new StringBuilder();
            foreach (var raw in (markdown ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
            {
                if (raw.StartsWith("## "))
                {
                    if (heading != null) sections.Add((heading, body.ToString().Trim()));
                    heading = raw.Substring(3).Trim();
                    body.Clear();
                    continue;
                }
                // Anything before the first `##` is the file's title and its note for the repo's
                // reader - not the player's.
                if (heading != null) body.AppendLine(raw);
            }
            if (heading != null) sections.Add((heading, body.ToString().Trim()));

            int first = -1, last = -1;
            foreach (var title in titles)
            {
                int at = sections.FindIndex(s => s.heading == title);
                if (at < 0)
                {
                    result.Missing.Add(title);
                    result.Tales.Add(string.Empty);
                    continue;
                }
                result.Tales.Add(sections[at].body);
                if (first < 0 || at < first) first = at;
                if (at > last) last = at;
            }

            if (first < 0) return result;

            result.Prologue = string.Join("\n\n", sections.Take(first).Select(s => "## " + s.heading + "\n\n" + s.body));
            result.Epilogue = string.Join("\n\n", sections.Skip(last + 1).Select(s => "## " + s.heading + "\n\n" + s.body));
            return result;
        }
    }

    /// <summary>Everything the page needs from the run. Plain values, so the page is pure.</summary>
    public sealed class SagaPageInput
    {
        public string Character;
        public string Way;
        public SagaTale Tale;
        public IList<string> ActTitles;
        public IList<string> ActNumerals;

        /// <summary>The act the run ended in (0-based). Nothing past it is told.</summary>
        public int LastActIndex;

        public IList<SagaDeed> Deeds;
        public string Date;
        public int Gods;
        public string Time;
        public float Heat;
        public float Score;
    }

    /// <summary>
    /// The saga's reward page: "The Saga of &lt;character&gt;" - the myth cut at the run's last act,
    /// with "What was told of you" under each tale. Pure, so the spoiler rule is tested: a run that
    /// ended at Yagluth is never told what came after.
    /// </summary>
    public static class SagaPage
    {
        public static string Compose(SagaPageInput input)
        {
            var titles = input.ActTitles ?? new string[0];
            var numerals = input.ActNumerals ?? new string[0];
            var tale = input.Tale ?? new SagaTale();
            int lastAct = System.Math.Min(input.LastActIndex, titles.Count - 1);
            // The whole saga is told when no LATER act has a tale - an act the myth has not reached yet
            // (the Deep North) must not keep a Fader run from its epilogue.
            int lastTold = -1;
            for (int i = 0; i < tale.Tales.Count; i++) if (!string.IsNullOrEmpty(tale.Tales[i])) lastTold = i;
            bool wholeSaga = lastTold >= 0 && lastAct >= lastTold;

            var html = new StringBuilder();
            html.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">");
            html.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
            html.Append("<title>The Saga of ").Append(Escape(input.Character)).Append("</title>");
            html.Append("<style>").Append(Css).Append("</style></head><body><main>");

            html.Append("<header><p class=\"eyebrow\">A saga of the tenth world</p>");
            html.Append("<h1>The Saga of ").Append(Escape(input.Character)).Append("</h1>");
            var sub = new List<string>();
            if (!string.IsNullOrEmpty(input.Date)) sub.Add(Escape(input.Date));
            if (!string.IsNullOrEmpty(input.Way)) sub.Add("who took up the way of " + Escape(input.Way));
            if (sub.Count > 0) html.Append("<p class=\"sub\">").Append(string.Join(" &middot; ", sub)).Append("</p>");
            html.Append("</header>");

            if (!string.IsNullOrEmpty(tale.Prologue))
                html.Append("<section class=\"prologue\">").Append(MarkdownToHtml(tale.Prologue)).Append("</section>");

            for (int i = 0; i <= lastAct && i < titles.Count; i++)
            {
                string numeral = i < numerals.Count ? numerals[i] : string.Empty;
                html.Append("<section class=\"act\"><h2><span class=\"numeral\">").Append(Escape(numeral))
                    .Append("</span>").Append(Escape(titles[i])).Append("</h2>");
                if (i < tale.Tales.Count && !string.IsNullOrEmpty(tale.Tales[i]))
                    html.Append(MarkdownToHtml(tale.Tales[i]));

                var deeds = (input.Deeds ?? new SagaDeed[0]).Where(d => d != null && d.ActNumeral == numeral).ToList();
                if (deeds.Count > 0)
                {
                    html.Append("<aside class=\"deeds\"><h4>What was told of you</h4><ul>");
                    foreach (var d in deeds)
                    {
                        html.Append("<li><strong>").Append(Escape(d.Step)).Append("</strong>");
                        if (!string.IsNullOrEmpty(d.Line)) html.Append(" &mdash; <em>").Append(Escape(d.Line)).Append("</em>");
                        html.Append("</li>");
                    }
                    html.Append("</ul></aside>");
                }
                html.Append("</section>");
            }

            if (wholeSaga && !string.IsNullOrEmpty(tale.Epilogue))
                html.Append("<section class=\"epilogue\">").Append(MarkdownToHtml(tale.Epilogue)).Append("</section>");
            else
                html.Append("<section class=\"coda\"><p>That is as far as the skalds can tell it of you. There is more of " +
                            "the saga &mdash; the world is wider than the gods you felled &mdash; and it waits for " +
                            "whoever goes further.</p></section>");

            html.Append("<footer><h4>The reckoning</h4><dl>");
            html.Append("<dt>Gods felled</dt><dd>").Append(input.Gods.ToString(CultureInfo.InvariantCulture)).Append("</dd>");
            if (!string.IsNullOrEmpty(input.Time)) html.Append("<dt>Time</dt><dd>").Append(Escape(input.Time)).Append("</dd>");
            html.Append("<dt>Heat</dt><dd>").Append(input.Heat.ToString("0", CultureInfo.InvariantCulture)).Append("</dd>");
            html.Append("<dt>Saga score</dt><dd>").Append(input.Score.ToString("0.#", CultureInfo.InvariantCulture)).Append("</dd>");
            html.Append("</dl></footer></main></body></html>");
            return html.ToString();
        }

        /// <summary>"Saga of &lt;character&gt; - &lt;stamp&gt;.html", with anything a file system refuses removed.</summary>
        public static string FileName(string character, string stamp) => FilePrefix(character) + stamp + ".html";

        /// <summary>"Saga of &lt;character&gt; - " - what every page for this character starts with.</summary>
        public static string FilePrefix(string character)
        {
            var bad = new HashSet<char>(Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }));
            string clean = new string((character ?? "the living one").Where(c => !bad.Contains(c)).ToArray()).Trim();
            if (clean.Length == 0) clean = "the living one";
            return $"Saga of {clean} - ";
        }

        /// <summary>
        /// The markdown subset the myth uses: `##`/`###` headings, paragraphs, `&gt;` quotes, `---`,
        /// `**bold**`, `*italic*`. Escaped FIRST, so nothing in the text can become markup.
        /// </summary>
        public static string MarkdownToHtml(string md)
        {
            var html = new StringBuilder();
            var blocks = Regex.Split((md ?? string.Empty).Replace("\r\n", "\n").Trim(), @"\n\s*\n");
            var quote = new List<string>();

            void FlushQuote()
            {
                if (quote.Count == 0) return;
                html.Append("<blockquote>");
                foreach (var para in quote) html.Append("<p>").Append(para).Append("</p>");
                html.Append("</blockquote>");
                quote.Clear();
            }

            foreach (var rawBlock in blocks)
            {
                string block = rawBlock.Trim();
                if (block.Length == 0) continue;

                var lines = block.Split('\n').Select(l => l.TrimEnd()).ToList();
                if (lines.All(l => l.StartsWith(">")))
                {
                    // A quote may run across blank '>' lines; each run of text is one paragraph.
                    var para = new List<string>();
                    foreach (var l in lines)
                    {
                        string t = l.TrimStart('>').Trim();
                        if (t.Length == 0) { if (para.Count > 0) { quote.Add(Inline(string.Join(" ", para))); para.Clear(); } }
                        else para.Add(t);
                    }
                    if (para.Count > 0) quote.Add(Inline(string.Join(" ", para)));
                    continue;
                }

                FlushQuote();
                if (block == "---") { html.Append("<hr>"); continue; }
                if (block.StartsWith("### ")) { html.Append("<h3>").Append(Inline(block.Substring(4).Trim())).Append("</h3>"); continue; }
                if (block.StartsWith("## ")) { html.Append("<h2>").Append(Inline(block.Substring(3).Split('\n')[0].Trim())).Append("</h2>");
                    string rest = string.Join(" ", block.Split('\n').Skip(1)).Trim();
                    if (rest.Length > 0) html.Append("<p>").Append(Inline(rest)).Append("</p>");
                    continue; }
                html.Append("<p>").Append(Inline(string.Join(" ", lines.Select(l => l.Trim())))).Append("</p>");
            }
            FlushQuote();
            return html.ToString();
        }

        private static string Inline(string text)
        {
            string s = Escape(text);
            s = Regex.Replace(s, @"\*\*(?=\S)(.+?)(?<=\S)\*\*", "<strong>$1</strong>");
            s = Regex.Replace(s, @"\*(?=\S)(.+?)(?<=\S)\*", "<em>$1</em>");
            return s;
        }

        private static string Escape(string text) =>
            (text ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        private const string Css =
            ":root{--bg:#f4ecd8;--ink:#2a2118;--soft:#6b5a45;--rule:#d8c7a3;--gold:#8a6414;--panel:#efe3c6}" +
            "@media (prefers-color-scheme:dark){:root{--bg:#16130f;--ink:#e9dfcb;--soft:#a8987c;--rule:#3a3125;--gold:#d8a84a;--panel:#1f1a14}}" +
            "body{margin:0;padding:0 16px;background:var(--bg);color:var(--ink);font:18px/1.7 Georgia,'Palatino Linotype',Palatino,serif}" +
            "main{max-width:40rem;margin:0 auto;padding:48px 0 96px}" +
            "header{text-align:center;margin-bottom:3rem}.eyebrow{letter-spacing:.25em;text-transform:uppercase;font-size:.7rem;color:var(--gold)}" +
            "h1{font-size:2.4rem;line-height:1.1;margin:.4rem 0}.sub{color:var(--soft);font-style:italic}" +
            "h2{font-size:1.6rem;margin:3.5rem 0 1rem;border-bottom:1px solid var(--rule);padding-bottom:.4rem}" +
            ".numeral{color:var(--gold);font-size:.9rem;letter-spacing:.15em;margin-right:.7rem}" +
            "h3{font-size:1.15rem;margin:2rem 0 .5rem;color:var(--soft)}" +
            "blockquote{margin:1.2rem 0;padding:.2rem 1.2rem;border-left:3px solid var(--gold);background:var(--panel)}" +
            "hr{border:0;border-top:1px solid var(--rule);margin:2.5rem 0}" +
            ".deeds{margin:2rem 0;padding:1rem 1.3rem;background:var(--panel);border:1px solid var(--rule);font-size:.92rem}" +
            ".deeds h4,footer h4{margin:0 0 .6rem;letter-spacing:.2em;text-transform:uppercase;font-size:.7rem;color:var(--gold)}" +
            ".deeds ul{margin:0;padding-left:1.1rem}.coda{font-style:italic;color:var(--soft);margin-top:3rem}" +
            "footer{margin-top:4rem;padding-top:1.5rem;border-top:1px solid var(--rule)}" +
            "dl{display:grid;grid-template-columns:auto 1fr;gap:.3rem 1.2rem;margin:0}dt{color:var(--soft)}dd{margin:0}";
    }
}
