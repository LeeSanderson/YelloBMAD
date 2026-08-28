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
    /// CSS written inside markup - <c>style</c> attributes and <c>&lt;style&gt;</c> element bodies
    /// - presented as a stylesheet per markup file so every gate sees it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Without this, every CSS gate in the suite was bypassable by one attribute.</b> A single
    /// <c>style="box-shadow: 0 2px 4px #000; height: 32px; border-radius: 50%; margin-left: 9px;
    /// text-transform: uppercase; font-size: 13px; outline: none"</c> on one element defeated the
    /// shadow, fixed-height, radius, physical-property, type-px and outline gates simultaneously
    /// and the suite reported every assertion green. Markup was read by exactly one gate, and only
    /// for the two <c>-light</c> regexes, while the class docblock claimed the gates covered
    /// "all <c>*.css</c>, all <c>*.razor</c>, all <c>*.html</c>". Blazor components routinely
    /// carry <c>style</c> attributes, so this was the most probable regression path in epic 2.
    /// </para>
    /// <para>
    /// Offsets are REAL offsets into the markup file, not into a synthesised stylesheet, so a
    /// failure message names a line a human can open. For <c>&lt;style&gt;</c> bodies that is
    /// achieved by blanking everything outside the body and parsing the result - the same
    /// length-preserving trick the comment blanker uses.
    /// </para>
    /// <para>
    /// A value that begins with <c>@</c> is a Razor expression rather than CSS and is skipped:
    /// <c>style="@BarWidth"</c> is a binding whose text this parser cannot evaluate, and reporting
    /// its identifier as a declaration would fail correct code.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<Sheet> MarkupStyleSheets { get; } =
    [
        .. MarkupFiles.Select(ReadMarkupStyles),
    ];

    /// <summary>
    /// Every stylesheet and every markup file's inline CSS, which is what the gates iterate.
    /// </summary>
    public static IReadOnlyList<Sheet> AllSheets { get; } =
    [
        .. StyleSheets.Concat(MarkupStyleSheets),
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
    /// Every custom property declared anywhere in the corpus outside the theme boundary, which is
    /// what <see cref="Resolve"/> substitutes from.
    /// </summary>
    /// <remarks>
    /// Wider than <see cref="TokenValues"/> on purpose. Resolution has to see every declaration
    /// the browser sees, or a <c>var()</c> naming a property declared in <c>base.css</c> - or in
    /// any stylesheet a later story adds - resolves to nothing, and then every length gate
    /// downstream measures no number and reports green. <c>TokenValues</c> stays confined to the
    /// token layer because the AC2 count and the palette are statements about THAT file.
    /// </remarks>
    public static IReadOnlyDictionary<string, string> AllCustomProperties { get; } =
        ReadAllCustomProperties();

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
    /// <remarks>
    /// Counted before it is taken, not via <c>SingleOrDefault</c>. That threw
    /// <c>"Sequence contains more than one matching element"</c> on the duplicate case - before the
    /// <c>?? throw</c> could run - so the crafted message below was reachable only for ZERO
    /// matches and its count interpolation was dead code for the branch it was written for. An
    /// unnamed framework exception inside a release gate is the outcome
    /// <c>RepositoryLayout.LoadXml</c>/<c>LoadJson</c> exist to avoid.
    /// </remarks>
    public static Sheet Tokens
    {
        get
        {
            var found = StyleSheets.Where(IsTokensFile).ToList();

            return found.Count == 1
                ? found[0]
                : throw new InvalidOperationException(
                    $"Expected exactly one '{TokensFileName}' in the source tree; found " +
                    $"{found.Count}. Every design gate in this suite reads the token layer, so it " +
                    "cannot run without knowing which file that is - and two of them would mean " +
                    "two competing token sets.");
        }
    }

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
        from sheet in AllSheets
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
    /// The root font size the <c>rem</c> scale is measured against. <c>base.css</c> pins
    /// <c>html { font-size: 100% }</c> and a gate refuses any px override, so 16 is the browser
    /// default this resolves to.
    /// </summary>
    public const double RootFontSizePx = 16;

    /// <summary>
    /// Every length in a value that can be converted to px, in px, after token substitution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why the px-only reading was not enough.</b> A floor stated in px and measured in px is
    /// defeated by writing the same length in another unit. <c>border-block-start: 0.0625rem</c>
    /// is 1px and passed the 1.5px hairline floor; <c>min-height: 1rem</c> is 16px and passed the
    /// 24px target floor; <c>font-size: 13pt</c> is absolute type and passed the px type ban.
    /// Every one of those is the requirement defeated by a unit change rather than by a value
    /// change.
    /// </para>
    /// <para>
    /// <c>em</c> is converted against the root as an approximation - its real basis is the
    /// element's own font size, which static analysis cannot know. The approximation is
    /// deliberately on the permissive side for the *unit* and still catches every case where the
    /// NUMBER is small enough to breach a floor, which is the shape these gates check.
    /// <c>ex</c>, <c>ch</c>, percentages and the viewport units are NOT converted: they are
    /// font- or container-relative in ways no fixed factor represents, and a wrong number in a
    /// failure message is worse than an honest omission.
    /// </para>
    /// </remarks>
    public static IEnumerable<double> AbsoluteLengthsPx(string value)
    {
        var resolved = Resolve(value);

        foreach (var match in AbsoluteLengthPattern.Matches(resolved).Cast<Match>())
        {
            var number = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var unit = match.Groups[2].Value.ToUpperInvariant();

            yield return number * UnitFactorPx(unit);
        }
    }

    /// <summary>
    /// The px widths of any border-width keywords in a value.
    /// </summary>
    /// <remarks>
    /// Kept separate from <see cref="AbsoluteLengthsPx"/> deliberately. <c>medium</c> and
    /// <c>thin</c> are lengths only in a border-width context; folding them into the general
    /// length reader would make them into lengths everywhere, and a gate would then report a
    /// font stack or a keyword value as a 3px length.
    /// </remarks>
    public static IEnumerable<double> BorderWidthKeywordsPx(string value)
    {
        foreach (var match in BorderWidthKeywordPattern.Matches(Resolve(value)).Cast<Match>())
        {
            yield return KeywordWidthPx(match.Value.ToUpperInvariant());
        }
    }

    /// <summary>
    /// px per unit, for the units a fixed factor genuinely represents.
    /// </summary>
    private static double UnitFactorPx(string unit) => unit switch
    {
        "PX" => 1,
        "REM" or "EM" => RootFontSizePx,
        "PT" => 96.0 / 72.0,
        "PC" => 16,
        "IN" => 96,
        "CM" => 96.0 / 2.54,
        "MM" => 96.0 / 25.4,
        "Q" => 96.0 / 101.6,
        _ => 1,
    };

    /// <summary>
    /// The px width of a border-width keyword, per CSS 2.1's usual UA values.
    /// </summary>
    private static double KeywordWidthPx(string keyword) => keyword switch
    {
        "THIN" => 1,
        "MEDIUM" => 3,
        "THICK" => 5,
        _ => 0,
    };

    /// <summary>
    /// Text with every CSS comment replaced by spaces of the same length, so offsets survive.
    /// </summary>
    /// <remarks>
    /// Walked rather than regex-replaced, because a comment marker can appear inside a quoted
    /// value - <c>content: "/*"</c> is legal CSS - and a regex has no way to know it is inside a
    /// string. Blanking from that <c>/*</c> to the next <c>*&#47;</c> anywhere later in the file
    /// would blank REAL declarations, and the gates would then never see the code they were
    /// written to check. Quoted runs are therefore skipped before comments are recognised.
    /// </remarks>
    public static string BlankCssComments(string text)
    {
        var characters = text.ToCharArray();
        var index = 0;

        while (index < text.Length)
        {
            var current = text[index];

            if (current is '"' or '\'')
            {
                index = SkipQuoted(text, index);
                continue;
            }

            if (current == '/' && index + 1 < text.Length && text[index + 1] == '*')
            {
                var close = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
                var end = close < 0 ? text.Length - 1 : close + 1;

                BlankSpan(characters, index, end);
                index = end + 1;
                continue;
            }

            index++;
        }

        return new string(characters);
    }

    /// <summary>
    /// The index just past a quoted run starting at <paramref name="start"/>.
    /// </summary>
    private static int SkipQuoted(string text, int start)
    {
        var quote = text[start];
        var index = start + 1;

        while (index < text.Length && text[index] != quote)
        {
            index += text[index] == '\\' ? 2 : 1;
        }

        return index + 1;
    }

    private static void BlankSpan(char[] characters, int from, int to)
    {
        for (var index = Math.Max(from, 0); index <= to && index < characters.Length; index++)
        {
            if (characters[index] is not ('\r' or '\n'))
            {
                characters[index] = ' ';
            }
        }
    }

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

    /// <summary>
    /// True when a value still names a <c>var()</c> reference after resolution, so a gate can
    /// report the unresolved reference rather than silently measuring nothing.
    /// </summary>
    public static bool HasUnresolvedReference(string value) =>
        VarReferencePattern.IsMatch(Resolve(value));

    /// <summary>
    /// Every custom-property name referenced in a value that resolution could not substitute.
    /// </summary>
    public static IEnumerable<string> UnresolvedReferences(string value) =>
        ReferencedTokens(Resolve(value));

    private static string Substitute(Match match) =>
        AllCustomProperties.TryGetValue(match.Groups[1].Value, out var resolved)
            ? resolved
            : match.Value;

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

        var path = RepositoryLayout.RelativePath(file);

        return new Sheet(file, path, raw, blanked, Parse(blanked, path));
    }

    private static Markup ReadMarkupFile(FileInfo file)
    {
        var raw = File.ReadAllText(file.FullName);

        return new Markup(file, RepositoryLayout.RelativePath(file), raw, BlankMarkupComments(raw));
    }

    /// <summary>
    /// Turns one markup file's inline CSS into a stylesheet: its <c>&lt;style&gt;</c> bodies and
    /// one rule per <c>style</c> attribute.
    /// </summary>
    private static Sheet ReadMarkupStyles(Markup markup)
    {
        var rules = new List<Rule>();

        rules.AddRange(Parse(StyleElementBodiesOnly(markup.Blanked), markup.Path));

        foreach (var match in StyleAttributePattern.Matches(markup.Blanked).Cast<Match>())
        {
            var value = match.Groups[2].Value;

            if (value.TrimStart().StartsWith('@'))
            {
                continue;
            }

            var declarations = new List<Declaration>();

            foreach (var (statement, offset) in SplitStatements(value, match.Groups[2].Index))
            {
                AddDeclaration(statement, offset, declarations);
            }

            if (declarations.Count > 0)
            {
                rules.Add(new Rule("[style]", string.Empty, match.Index, declarations));
            }
        }

        return new Sheet(
            markup.File,
            markup.Path,
            markup.Raw,
            markup.Blanked,
            [.. rules.OrderBy(r => r.Offset)]);
    }

    /// <summary>
    /// The text with everything outside a <c>&lt;style&gt;</c> element's body blanked, so parsing
    /// it yields rules at their real offsets in the file.
    /// </summary>
    private static string StyleElementBodiesOnly(string text)
    {
        var characters = new char[text.Length];

        for (var index = 0; index < text.Length; index++)
        {
            characters[index] = text[index] is '\r' or '\n' ? text[index] : ' ';
        }

        foreach (var match in StyleElementPattern.Matches(text).Cast<Match>())
        {
            var body = match.Groups[1];

            for (var index = body.Index; index < body.Index + body.Length; index++)
            {
                characters[index] = text[index];
            }
        }

        return new string(characters);
    }

    /// <summary>
    /// Splits a declaration list on semicolons that are not inside parentheses or quotes, keeping
    /// each statement's real offset.
    /// </summary>
    private static IEnumerable<(string Statement, int Offset)> SplitStatements(string value, int start)
    {
        var depth = 0;
        var from = 0;
        var index = 0;

        // A while loop rather than a for: a quoted run advances the position by more than one, and
        // the coding standard forbids writing the stop-condition variable inside a for body.
        while (index < value.Length)
        {
            var current = value[index];

            if (current is '"' or '\'')
            {
                index = SkipQuoted(value, index);
                continue;
            }

            if (current == '(')
            {
                depth++;
            }
            else if (current == ')')
            {
                depth = Math.Max(depth - 1, 0);
            }
            else if (current == ';' && depth == 0)
            {
                yield return (value[from..index], start + from);
                from = index + 1;
            }
            else
            {
                // Any other character neither terminates a statement nor changes depth.
            }

            index++;
        }

        if (from < value.Length)
        {
            yield return (value[from..], start + from);
        }
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

    /// <summary>
    /// Reads every custom property from every stylesheet, excluding the token layer's theme
    /// boundary.
    /// </summary>
    /// <remarks>
    /// The boundary exclusion is applied to the TOKEN LAYER ONLY. Offsets are per-file, so
    /// testing another sheet's offset against the token layer's boundary range would exclude
    /// whichever of its rules happened to sit between the same two byte positions.
    /// </remarks>
    private static IReadOnlyDictionary<string, string> ReadAllCustomProperties()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var sheet in StyleSheets)
        {
            var isTokenLayer = IsTokensFile(sheet);

            foreach (var rule in sheet.Rules)
            {
                if (isTokenLayer && IsInsideThemeBoundary(rule.Offset))
                {
                    continue;
                }

                foreach (var declaration in rule.Declarations.Where(IsCustomProperty))
                {
                    // Last wins, as the cascade does within one origin.
                    values[declaration.Property[2..]] = declaration.Value.Trim();
                }
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
    private static IReadOnlyList<Rule> Parse(string blanked, string path)
    {
        var rules = new List<Rule>();
        var cursor = new Cursor(blanked, path);

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

            // Inside a parenthesised run, `;`, `{` and `}` are ordinary characters. A data URI -
            // `url(data:image/svg+xml;base64,AAA)` - carries a semicolon that is NOT a statement
            // terminator, and splitting there turned one declaration into a property no gate
            // matches plus a dropped fragment: a silent pass rather than a parse error.
            if (cursor.InParens && current is ';' or '{' or '}')
            {
                cursor.Append();
                continue;
            }

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
                AppendTrackingParens(cursor, current);
            }
        }

        Flush(cursor, declarations);

        return declarations;
    }

    /// <summary>
    /// Appends one ordinary character, keeping the cursor's parenthesis depth current.
    /// </summary>
    private static void AppendTrackingParens(Cursor cursor, char current)
    {
        if (current == '(')
        {
            cursor.EnterParen();
        }
        else if (current == ')')
        {
            cursor.ExitParen();
        }
        else
        {
            // Any other character leaves the parenthesis depth unchanged.
        }

        cursor.Append();
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
    //
    // `IgnoreCase` and the leading-dot alternative are both load-bearing. CSS units are
    // case-insensitive, so `13PX` and `1Px` are lengths the browser honours and the original
    // case-sensitive pattern saw no number at all - every length gate passed. And `.5px` is a
    // legal length written without a leading zero, which the original `-?\d+` could not capture,
    // so a sub-hairline border written that way was invisible. The lookbehind still blocks the
    // `.5px` inside `1.5px` from matching on its own, because the character before the dot is a
    // word character.
    [GeneratedRegex(
        @"(?<![\w.])(-?(?:\d+(?:\.\d+)?|\.\d+))px\b",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 5000)]
    private static partial Regex PixelLengthPattern { get; }

    [GeneratedRegex(@"\s+", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex WhitespaceRunPattern { get; }

    // A length in any unit a fixed factor converts to px. `ex`, `ch`, `%` and the viewport units
    // are deliberately absent - see AbsoluteLengthsPx.
    [GeneratedRegex(
        @"(?<![\w.])(-?(?:\d+(?:\.\d+)?|\.\d+))(px|rem|em|pt|pc|in|cm|mm|q)\b",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 5000)]
    private static partial Regex AbsoluteLengthPattern { get; }

    // The three border-width keywords, which are lengths without being numbers.
    [GeneratedRegex(@"\b(?:thin|medium|thick)\b", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 5000)]
    private static partial Regex BorderWidthKeywordPattern { get; }

    // A `style` attribute and its value. The quote character is captured and back-referenced so
    // both quoting styles are read with one pattern and the OTHER quote may appear inside the
    // value. The lookbehind stops `data-style=` and any other `-style` suffix from matching.
    [GeneratedRegex(
        @"(?<![\w-])style\s*=\s*([""'])((?:(?!\1)[\s\S])*)\1",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 5000)]
    private static partial Regex StyleAttributePattern { get; }

    // A `<style>` element and its body.
    [GeneratedRegex(
        @"<style\b[^>]*>([\s\S]*?)</style\s*>",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 5000)]
    private static partial Regex StyleElementPattern { get; }

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
    private sealed class Cursor(string text, string path)
    {
        public StringBuilder Buffer { get; } = new();

        public int Index { get; private set; }

        public int ContentStart { get; private set; } = -1;

        public bool AtEnd => Index >= text.Length;

        public char Current => text[Index];

        /// <summary>
        /// How many unclosed <c>(</c> the cursor sits inside.
        /// </summary>
        public int ParenDepth { get; private set; }

        public bool InParens => ParenDepth > 0;

        public void Advance() => Index++;

        public void EnterParen() => ParenDepth++;

        public void ExitParen() => ParenDepth = Math.Max(ParenDepth - 1, 0);

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
        /// <remarks>
        /// An unterminated quote THROWS rather than running to end of file. Consuming to the end
        /// silently meant every rule after the quote was never parsed at all, and every gate in
        /// this suite then reported green over a stylesheet it had not read - the worst available
        /// outcome, and indistinguishable from a clean run.
        /// </remarks>
        public void ConsumeString()
        {
            MarkContent();

            var quote = Current;
            var start = Index;
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

            if (AtEnd)
            {
                throw new InvalidOperationException(
                    $"'{path}' has an unterminated {quote} string starting at line " +
                    $"{LineAt(text, start)}. Every rule after it would be unparsed, so every " +
                    "design gate would report green over a stylesheet it never read.");
            }

            Buffer.Append(Current);
            Index++;
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
