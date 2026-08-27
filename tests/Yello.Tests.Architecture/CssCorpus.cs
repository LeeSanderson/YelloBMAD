using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Yello.Tests.Architecture;

/// <summary>
/// Reads every stylesheet and markup file in the repository, parses them, and resolves token
/// references.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the first CSS-aware gate in the repository.</b> <c>RepositoryLayout.SourceFilesOf</c>
/// is <c>*.cs</c> only and no existing gate reads a static asset, so the tree walk is routed
/// through <c>RepositoryLayout.EnumerateSourceFiles</c> deliberately: a gate doing its own
/// <c>EnumerateFiles</c> re-introduces the hazard the exclusion list exists to prevent. The
/// seven <c>:root</c> blocks under <c>_bmad-output/.../mockups/</c> are excluded by that list,
/// which matters more here than anywhere else - they declare 13 abbreviated non-semantic names
/// with no <c>-light</c> siblings, so a scan reaching them would report a token set that fails
/// every assertion in this suite and describes no file the product ships.
/// </para>
/// <para>
/// <b>Every gate reads the whole corpus, never a named file.</b> Eight of story 1.2's thirteen
/// acceptance criteria are conditioned on components that do not exist yet - "Given any
/// interactive component", "When components are inspected", "Given a text link inside a
/// sentence". A gate written as an assertion about a known file passes because that file is
/// empty of violations, and keeps passing while a later story writes the violation it was meant
/// to catch. Story 1.1's own summary of its dominant defect class was <i>"several gates assert
/// something materially weaker than their names, comments and the ACs claim"</i>. Globbing is
/// what makes these gates cover epic 2.
/// </para>
/// <para>
/// <b>Comments are blanked, not removed.</b> Every span replaced keeps its length, so an offset
/// in the blanked text is an offset in the file. That is what makes the theme-boundary
/// containment check possible at all: the boundary markers live in comments, and the prose
/// inside those comments names the very token references the check forbids
/// (<c>"never var(--surface-card-light)"</c>). Stripping comments would move every offset;
/// keeping them would make the documentation fail the gate it documents.
/// </para>
/// <para>
/// <b>Declaration order of the properties below is load-bearing.</b> Static initialisers run in
/// textual order, so <see cref="ThemeBoundaryRange"/> is established before
/// <see cref="TokenValues"/>, which excludes the boundary by offset. Reversed, the range would
/// still be <c>(0,0)</c> while the palette was read, every rule would count as outside the
/// boundary, and each semantic name would end up bound to <c>var(--x-light)</c> rather than to
/// its hex value - collapsing the palette to one theme with every assertion still green.
/// </para>
/// </remarks>
internal static partial class CssCorpus
{
    /// <summary>
    /// The file the token layer lives in. Named here because the AC2 count, the palette and the
    /// theme boundary are statements about <i>this</i> file; every other gate globs.
    /// </summary>
    public const string TokensFileName = "tokens.css";

    public const string ThemeBoundaryBeginMarker = "THEME BOUNDARY BEGIN";
    public const string ThemeBoundaryEndMarker = "THEME BOUNDARY END";

    /// <summary>
    /// Every stylesheet in the source tree, ordered so failure messages are stable between runs.
    /// </summary>
    public static IReadOnlyList<Sheet> StyleSheets { get; } =
    [
        .. RepositoryLayout.EnumerateSourceFiles("*.css")
            .OrderBy(RepositoryLayout.RelativePath, StringComparer.Ordinal)
            .Select(ReadStyleSheet),
    ];

    /// <summary>
    /// Every Razor component and HTML page in the source tree.
    /// </summary>
    /// <remarks>
    /// Both extensions, deliberately. A <c>-light</c> token reference or a hard-coded string can
    /// arrive in either, and <c>index.html</c> is where this repository's declared copy variances
    /// actually live.
    /// </remarks>
    public static IReadOnlyList<Markup> MarkupFiles { get; } =
    [
        .. RepositoryLayout.EnumerateSourceFiles("*.razor")
            .Concat(RepositoryLayout.EnumerateSourceFiles("*.html"))
            .OrderBy(RepositoryLayout.RelativePath, StringComparer.Ordinal)
            .Select(ReadMarkupFile),
    ];

    /// <summary>
    /// The offsets of the theme-boundary markers in the token layer.
    /// </summary>
    public static (int Begin, int End) ThemeBoundaryRange { get; } = FindThemeBoundary();

    /// <summary>
    /// Every custom property the token layer declares on <c>:root</c>, outside the theme boundary.
    /// </summary>
    /// <remarks>
    /// The theme boundary is excluded on purpose: inside it the 15 semantic names are rebound to
    /// <c>var(--x-light)</c>, so a map built over the whole file would overwrite each name's
    /// literal value with a reference. The boundary's bindings are read separately, by
    /// <see cref="ThemeBoundaryBindings"/>.
    /// </remarks>
    public static IReadOnlyDictionary<string, string> TokenValues { get; } = ReadTokenValues();

    /// <summary>
    /// The rules inside the theme boundary that rebind custom properties, in file order.
    /// </summary>
    public static IReadOnlyList<Rule> ThemeBoundaryRules { get; } =
    [
        .. Tokens.Rules
            .Where(r => IsInsideThemeBoundary(r.Offset))
            .Where(r => r.Declarations.Any(IsCustomProperty)),
    ];

    /// <summary>
    /// Per boundary rule, each semantic name it rebinds mapped to the value it rebinds it to.
    /// </summary>
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> ThemeBoundaryBindings { get; } =
        ReadThemeBoundaryBindings();

    /// <summary>
    /// The token layer, or a failure that says which file is missing.
    /// </summary>
    public static Sheet Tokens =>
        StyleSheets.SingleOrDefault(IsTokensFile)
        ?? throw new InvalidOperationException(
            $"Expected exactly one '{TokensFileName}' in the source tree; found " +
            $"{StyleSheets.Count(IsTokensFile)}. Every design gate in this suite reads the token " +
            "layer, so it cannot run without knowing which file that is - and two of them would " +
            "mean two competing token sets.");

    /// <summary>
    /// True when an offset in the token layer falls inside the theme boundary.
    /// </summary>
    public static bool IsInsideThemeBoundary(int offset) =>
        offset > ThemeBoundaryRange.Begin && offset < ThemeBoundaryRange.End;

    /// <summary>
    /// True when a declaration declares a custom property rather than a CSS property.
    /// </summary>
    public static bool IsCustomProperty(Declaration declaration) =>
        declaration.Property.StartsWith("--", StringComparison.Ordinal);

    /// <summary>
    /// Every rule in every stylesheet.
    /// </summary>
    public static IEnumerable<(Sheet Sheet, Rule Rule)> AllRules() =>
        from sheet in StyleSheets
        from rule in sheet.Rules
        select (sheet, rule);

    /// <summary>
    /// Every declaration in every stylesheet, with the rule and file it came from.
    /// </summary>
    public static IEnumerable<(Sheet Sheet, Rule Rule, Declaration Declaration)> AllDeclarations() =>
        from pair in AllRules()
        from declaration in pair.Rule.Declarations
        select (pair.Sheet, pair.Rule, declaration);

    /// <summary>
    /// A declared value with every <c>var()</c> reference substituted for what the token layer
    /// says, so a gate compares the length the browser resolves rather than the text an author
    /// typed.
    /// </summary>
    /// <remarks>
    /// This is the CSS analogue of the pattern commit <c>3352676</c> established for MSBuild -
    /// "ask MSBuild what it evaluates, instead of reading what the files declare". A border
    /// stated as <c>var(--border-hairline-width) solid var(--border-hairline)</c> is 1.5px, and a
    /// width gate reading the raw text sees no number at all and passes vacuously. Substitution
    /// repeats because tokens reference tokens: <c>--space-gutter</c> is <c>var(--space-3)</c> is
    /// <c>9px</c>.
    /// </remarks>
    public static string Resolve(string value)
    {
        var current = value;

        // Bounded rather than while(changed): a token cycle would otherwise hang the suite, and a
        // gate that never returns is worse than one reporting a wrong answer. Eight is far beyond
        // the two levels the token layer actually uses.
        for (var pass = 0; pass < 8; pass++)
        {
            var next = VarReferencePattern.Replace(current, Substitute);

            if (next.Equals(current, StringComparison.Ordinal))
            {
                return next;
            }

            current = next;
        }

        return current;
    }

    /// <summary>
    /// Every custom-property name a value references through <c>var()</c>, without the leading
    /// <c>--</c>, so the names line up with the keys of <see cref="TokenValues"/>.
    /// </summary>
    public static IEnumerable<string> ReferencedTokens(string value) =>
        VarReferencePattern.Matches(value).Select(m => m.Groups[1].Value);

    /// <summary>
    /// Every px length in a declared value, after token substitution.
    /// </summary>
    public static IEnumerable<double> PixelLengths(string value) =>
        from match in PixelLengthPattern.Matches(Resolve(value))
        select double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);

    /// <summary>
    /// Text with every CSS comment replaced by spaces of the same length, so offsets survive.
    /// </summary>
    public static string BlankCssComments(string text) =>
        CssCommentPattern.Replace(text, m => Blank(m.Value));

    /// <summary>
    /// The same, for markup: Razor comments, HTML comments and the C# block comments that appear
    /// inside an <c>@code</c> block.
    /// </summary>
    /// <remarks>
    /// C# <i>line</i> comments are deliberately NOT blanked. Blanking to end-of-line would eat
    /// the rest of any line carrying a URL - <c>&lt;a href="https://x"&gt;Overdue&lt;/a&gt;</c>
    /// loses its text node from the <c>//</c> onward - and a hard-coded string that vanishes from
    /// the scan is a false negative in the one gate whose whole job is to catch hard-coded
    /// strings.
    /// </remarks>
    public static string BlankMarkupComments(string text) =>
        MarkupCommentPattern.Replace(text, m => Blank(m.Value));

    /// <summary>
    /// The 1-based line number of an offset, for a failure message a human has to act on.
    /// </summary>
    public static int LineAt(string text, int offset)
    {
        var limit = Math.Clamp(offset, 0, text.Length);
        var line = 1;

        for (var index = 0; index < limit; index++)
        {
            if (text[index] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    /// <summary>
    /// Every offset at which a value occurs in some text.
    /// </summary>
    public static IEnumerable<int> Occurrences(string text, string value)
    {
        for (var index = text.IndexOf(value, StringComparison.Ordinal);
             index >= 0;
             index = text.IndexOf(value, index + 1, StringComparison.Ordinal))
        {
            yield return index;
        }
    }

    private static string Substitute(Match match) =>
        TokenValues.TryGetValue(match.Groups[1].Value, out var resolved) ? resolved : match.Value;

    private static bool IsTokensFile(Sheet sheet) =>
        sheet.File.Name.Equals(TokensFileName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A run of spaces as long as the text it replaces, with newlines preserved so a reported
    /// line number still means something.
    /// </summary>
    private static string Blank(string text)
    {
        var characters = text.ToCharArray();

        for (var index = 0; index < characters.Length; index++)
        {
            if (characters[index] is not ('\r' or '\n'))
            {
                characters[index] = ' ';
            }
        }

        return new string(characters);
    }

    private static Sheet ReadStyleSheet(FileInfo file)
    {
        var raw = File.ReadAllText(file.FullName);
        var blanked = BlankCssComments(raw);

        return new Sheet(file, RepositoryLayout.RelativePath(file), raw, blanked, Parse(blanked));
    }

    private static Markup ReadMarkupFile(FileInfo file)
    {
        var raw = File.ReadAllText(file.FullName);

        return new Markup(file, RepositoryLayout.RelativePath(file), raw, BlankMarkupComments(raw));
    }

    private static IReadOnlyDictionary<string, string> ReadTokenValues()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var rule in Tokens.Rules.Where(r => !IsInsideThemeBoundary(r.Offset)))
        {
            foreach (var declaration in rule.Declarations.Where(IsCustomProperty))
            {
                // Last wins, as the cascade does within one origin.
                values[declaration.Property[2..]] = declaration.Value.Trim();
            }
        }

        return values;
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> ReadThemeBoundaryBindings()
    {
        var bindings = new List<IReadOnlyDictionary<string, string>>();

        foreach (var rule in ThemeBoundaryRules)
        {
            var rebindings = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var declaration in rule.Declarations.Where(IsCustomProperty))
            {
                rebindings[declaration.Property[2..]] = declaration.Value.Trim();
            }

            bindings.Add(rebindings);
        }

        return bindings;
    }

    /// <summary>
    /// The offsets of the two boundary markers, validated as a single well-formed region.
    /// </summary>
    /// <remarks>
    /// Exactly one of each, in the right order. Two <c>BEGIN</c> markers would let a second
    /// "boundary" be planted anywhere in the file, and every <c>-light</c> reference inside it
    /// would be accepted - which is the AC1 gate silently switched off. Read from the RAW text
    /// because the markers are comments.
    /// </remarks>
    private static (int Begin, int End) FindThemeBoundary()
    {
        var raw = Tokens.Raw;
        var begins = Occurrences(raw, ThemeBoundaryBeginMarker).ToList();
        var ends = Occurrences(raw, ThemeBoundaryEndMarker).ToList();

        if (begins.Count != 1 || ends.Count != 1 || begins[0] >= ends[0])
        {
            throw new InvalidOperationException(
                $"'{RepositoryLayout.RelativePath(Tokens.File)}' must contain exactly one " +
                $"'{ThemeBoundaryBeginMarker}' comment followed by exactly one " +
                $"'{ThemeBoundaryEndMarker}' comment; found {begins.Count} and {ends.Count}. " +
                "AC1 requires the theme to resolve in ONE place, and the gate enforcing it " +
                "locates that place by these markers - so a missing, duplicated or reversed pair " +
                "is the gate unable to answer rather than the gate passing.");
        }

        return (begins[0], ends[0]);
    }

    /// <summary>
    /// Parses comment-blanked CSS into rules and declarations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hand-written rather than taken from a package, for the reason given on
    /// <see cref="WcagContrast"/>: the corpus is hand-written CSS with no preprocessor, and the
    /// grammar this needs is blocks, declarations, at-rules and strings.
    /// </para>
    /// <para>
    /// It handles nesting even though the corpus does not use it. A gate that silently
    /// mis-parses a construct a later story introduces reports green over code it never read,
    /// and CSS nesting is the single most likely such construct.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<Rule> Parse(string blanked)
    {
        var rules = new List<Rule>();
        var cursor = new Cursor(blanked);

        ParseBlock(cursor, [], string.Empty, rules);

        return [.. rules.OrderBy(r => r.Offset)];
    }

    private static List<Declaration> ParseBlock(
        Cursor cursor,
        List<string> atRules,
        string selector,
        List<Rule> rules)
    {
        var declarations = new List<Declaration>();

        while (!cursor.AtEnd)
        {
            var current = cursor.Current;

            if (current == '}')
            {
                cursor.Advance();
                Flush(cursor, declarations);

                return declarations;
            }

            if (current == '{')
            {
                cursor.Advance();
                OpenBlock(cursor, atRules, selector, rules);
            }
            else if (current == ';')
            {
                cursor.Advance();
                Flush(cursor, declarations);
            }
            else if (current is '"' or '\'')
            {
                cursor.ConsumeString();
            }
            else
            {
                cursor.Append();
            }
        }

        Flush(cursor, declarations);

        return declarations;
    }

    private static void OpenBlock(Cursor cursor, List<string> atRules, string selector, List<Rule> rules)
    {
        var headOffset = cursor.ContentStart < 0 ? cursor.Index : cursor.ContentStart;
        var head = cursor.TakeHead();

        if (head.StartsWith('@'))
        {
            atRules.Add(head);
            var atPath = string.Join(' ', atRules);
            var nested = ParseBlock(cursor, atRules, selector, rules);
            atRules.RemoveAt(atRules.Count - 1);

            // An at-rule carrying declarations of its own - @font-face, @property - is a rule.
            // One carrying only nested rules is a context, and its children already record it in
            // their AtRulePath.
            if (nested.Count > 0)
            {
                rules.Add(new Rule(selector, atPath, headOffset, nested));
            }

            return;
        }

        var nestedSelector = selector.Length == 0 ? head : $"{selector} {head}";
        var declarations = ParseBlock(cursor, atRules, nestedSelector, rules);

        rules.Add(new Rule(nestedSelector, string.Join(' ', atRules), headOffset, declarations));
    }

    private static void Flush(Cursor cursor, List<Declaration> declarations)
    {
        AddDeclaration(cursor.Buffer.ToString(), cursor.ContentStart, declarations);
        cursor.ResetBuffer();
    }

    /// <summary>
    /// Turns one <c>property: value</c> statement into a declaration.
    /// </summary>
    /// <remarks>
    /// Split at the FIRST colon only. A value can legitimately contain one -
    /// <c>url(data:image/png;base64,...)</c> - and splitting on all of them turns such a
    /// declaration into a property no gate matches, which is a silent pass rather than a parse
    /// error.
    /// </remarks>
    private static void AddDeclaration(string statement, int offset, List<Declaration> declarations)
    {
        var text = statement.Trim();

        if (text.Length == 0 || offset < 0)
        {
            return;
        }

        var colon = text.IndexOf(':', StringComparison.Ordinal);

        if (colon <= 0)
        {
            return;
        }

        var property = text[..colon].Trim();
        var value = text[(colon + 1)..].Trim();
        var important = false;
        var bang = value.LastIndexOf('!');

        if (bang >= 0 && value[(bang + 1)..].Trim().Equals("important", StringComparison.OrdinalIgnoreCase))
        {
            important = true;
            value = value[..bang].Trim();
        }

        declarations.Add(new Declaration(property, value, important, offset));
    }

    private static string CollapseWhitespace(string text) =>
        WhitespaceRunPattern.Replace(text, " ").Trim();

    [GeneratedRegex(@"/\*[\s\S]*?\*/", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex CssCommentPattern { get; }

    [GeneratedRegex(@"@\*[\s\S]*?\*@|<!--[\s\S]*?-->|/\*[\s\S]*?\*/", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex MarkupCommentPattern { get; }

    // A var() reference and the custom property it names, captured WITHOUT the leading `--` so
    // the capture is a key of TokenValues. Capturing the dashes was a silent total failure rather
    // than a partial one: every lookup missed, every var() was left as text, and the light palette
    // resolved to nothing - which the harness reported as "18 of 36 ratios were computed" rather
    // than as a contrast failure.
    //
    // The optional fallback is consumed so `var(--a, 4px)` resolves rather than being left as
    // unparsed text.
    [GeneratedRegex(@"var\(\s*--([A-Za-z0-9_-]+)\s*(?:,[^()]*)?\)", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex VarReferencePattern { get; }

    // A px length, signed and optionally fractional, with a boundary in front so the `2px` inside
    // an identifier or a longer number is not read as a length.
    [GeneratedRegex(@"(?<![\w.])(-?\d+(?:\.\d+)?)px\b", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex PixelLengthPattern { get; }

    [GeneratedRegex(@"\s+", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex WhitespaceRunPattern { get; }

    /// <summary>
    /// One declaration inside a rule, with its offset in the file.
    /// </summary>
    internal sealed record Declaration(string Property, string Value, bool IsImportant, int Offset);

    /// <summary>
    /// One style rule: its selector, the at-rules enclosing it, and its declarations.
    /// </summary>
    internal sealed record Rule(
        string Selector,
        string AtRulePath,
        int Offset,
        IReadOnlyList<Declaration> Declarations);

    /// <summary>
    /// A stylesheet, as raw text, as comment-blanked text, and as parsed rules.
    /// </summary>
    internal sealed record Sheet(
        FileInfo File,
        string Path,
        string Raw,
        string Blanked,
        IReadOnlyList<Rule> Rules);

    /// <summary>
    /// A markup file - a Razor component or an HTML page - as raw and comment-blanked text.
    /// </summary>
    internal sealed record Markup(FileInfo File, string Path, string Raw, string Blanked);

    /// <summary>
    /// A position in the text being parsed, plus the prelude accumulated so far.
    /// </summary>
    /// <remarks>
    /// A small mutable cursor rather than <c>ref int index</c> threaded through every helper.
    /// That is not a style preference: the parser is recursive, and the whole point is that a
    /// nested block advances the SAME position its caller will resume from. Copying the index
    /// into a helper and forgetting to write it back would re-parse the block body as if it were
    /// the parent's, silently, and every gate downstream would be reading rules that do not exist
    /// in the file.
    /// </remarks>
    private sealed class Cursor(string text)
    {
        public StringBuilder Buffer { get; } = new();

        public int Index { get; private set; }

        public int ContentStart { get; private set; } = -1;

        public bool AtEnd => Index >= text.Length;

        public char Current => text[Index];

        public void Advance() => Index++;

        public void Append()
        {
            MarkContent();
            Buffer.Append(Current);
            Index++;
        }

        /// <summary>
        /// Consumes a quoted string into the buffer, so a <c>;</c> or <c>{</c> inside it does not
        /// terminate a declaration that has not ended.
        /// </summary>
        public void ConsumeString()
        {
            MarkContent();

            var quote = Current;
            Buffer.Append(quote);
            Index++;

            while (!AtEnd && Current != quote)
            {
                if (Current == '\\' && Index + 1 < text.Length)
                {
                    Buffer.Append(Current);
                    Index++;
                }

                Buffer.Append(Current);
                Index++;
            }

            if (!AtEnd)
            {
                Buffer.Append(Current);
                Index++;
            }
        }

        /// <summary>
        /// The accumulated prelude - a selector or an at-rule - with the buffer reset.
        /// </summary>
        public string TakeHead()
        {
            var head = CollapseWhitespace(Buffer.ToString());
            ResetBuffer();

            return head;
        }

        public void ResetBuffer()
        {
            Buffer.Clear();
            ContentStart = -1;
        }

        /// <summary>
        /// Records where the current statement's first non-whitespace character sits.
        /// </summary>
        /// <remarks>
        /// The offset has to skip leading whitespace, not start where the previous statement
        /// ended. The theme-boundary containment check compares a rule's offset against the
        /// position of an <c>END</c> marker that sits between two rules - and with the offset
        /// taken at the previous closing brace, the rule AFTER the marker would report an offset
        /// BEFORE it and be judged inside the boundary. Every non-colour token block would then
        /// count as part of the theme boundary.
        /// </remarks>
        private void MarkContent()
        {
            if (ContentStart < 0 && !char.IsWhiteSpace(Current))
            {
                ContentStart = Index;
            }
        }
    }
}
