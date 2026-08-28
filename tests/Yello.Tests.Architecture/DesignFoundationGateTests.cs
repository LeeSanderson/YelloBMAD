using System.Text.RegularExpressions;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// The gates that keep story 1.2's design foundations true as components arrive.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the class that decides whether story 1.2 is real.</b> Eight of its thirteen
/// acceptance criteria are conditioned on components that do not exist yet - "Given any
/// interactive component", "When components are inspected", "Given a text link inside a
/// sentence", "Given any user-visible string". A gate written as an assertion about today's tree
/// passes because the tree is empty, and keeps passing while story 2.2 writes the violation it
/// was meant to catch.
/// </para>
/// <para>
/// Story 1.1 accumulated three review passes and 40+ findings, and its own summary of the
/// dominant theme is the most useful thing it handed this story: <i>"several gates assert
/// something materially weaker than their names, comments and the ACs claim"</i>. Two rules
/// follow, and every gate below obeys both:
/// </para>
/// <list type="number">
///   <item><description>
///     <b>Every gate scans the repository, not a known file.</b> All <c>*.css</c>, all
///     <c>*.razor</c>, all <c>*.html</c>, through <c>RepositoryLayout.EnumerateSourceFiles</c>.
///     A gate that names the files it checks stops covering the files added after it.
///   </description></item>
///   <item><description>
///     <b>Every gate was failed on purpose first, against a violation a LATER story would
///     plausibly write.</b> Not a synthetic one: a <c>.razor</c> using
///     <c>var(--surface-card-light)</c>, a component with a hard-coded English string, a
///     <c>box-shadow</c> on a card. Today's empty tree cannot exercise those, which is precisely
///     why they were planted deliberately. <c>tests/TESTING-CONVENTIONS.md:93-96</c> makes this
///     a rule, not a courtesy: <i>"An absence assertion must be validated against a planted
///     signal, or it is not a test."</i> The results are in the story's Dev Agent Record.
///   </description></item>
/// </list>
/// <para>
/// <b>Where a gate reads effective state, it does.</b> This is the CSS analogue of what commit
/// <c>3352676</c> established for MSBuild - "ask MSBuild what it evaluates, instead of reading
/// what the files declare". A border stated as <c>var(--border-hairline-width) solid
/// var(--border-hairline)</c> carries no number at all in its text, so the width gates resolve
/// tokens before measuring. The one gate that deliberately reads DECLARED text is the px
/// confinement gate: its whole point is that an author should write a token rather than a
/// literal, and resolving first would turn every correct use of a token into a violation.
/// </para>
/// <para>
/// <b>Two clauses of AC11 are NOT gated here, and are not claimed to be.</b> "No label sized to
/// its English string" and "metadata never aligned by character count" are not statically
/// detectable and have no component to measure. They are discharged constructively in
/// <c>base.css</c> - content-sized boxes, no fixed heights, no character-cell alignment, and
/// <c>rem</c> internal padding - and stated as such in the Dev Agent Record. Writing a gate that
/// appeared to cover them would be the exact defect class this class exists to avoid.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "UX-DR7")]
[Trait("Requirement", "UX-DR42")]
[Trait("Requirement", "NFR-9")]
public sealed partial class DesignFoundationGateTests
{
    /// <summary>
    /// The hairline floor in px. 1.5, not 1: a 1px border antialiases whenever its edge lands off
    /// a device-pixel boundary, which a 3px spacing grid does at the 1.25x, 1.5x and 1.75x display
    /// scales, and at 80% coverage every border pair in both themes drops below the 3:1 gate.
    /// </summary>
    private const double HairlineFloorPx = 1.5;

    /// <summary>
    /// The interactive target floor in px. WCAG 2.2 AA's 2.5.8; WCAG 2.1 AA has no target-size
    /// criterion at all, so this is a deliberate commitment above the version NFR-9 names.
    /// </summary>
    private const double TargetFloorPx = 24;

    /// <summary>
    /// The focus ring's width and offset floor in px. The offset is the load-bearing half: the
    /// ring sits at 1.45 against <c>accent</c> and would vanish on an accented control, and what
    /// saves it is being drawn on the ground behind.
    /// </summary>
    private const double FocusRingFloorPx = 2;

    private const double LengthTolerance = 1e-9;

    /// <summary>
    /// The line-height floor. AC3, and the figure WCAG 1.4.12's 1.5x text-spacing override needs a
    /// line box to be able to absorb.
    /// </summary>
    private const double MinimumLineHeight = 1.5;

    /// <summary>
    /// Custom-property name endings that carry a typographic value, so the type ban reaches the
    /// token layer where the scale is actually declared.
    /// </summary>
    private static readonly string[] TypographicTokenSuffixes =
        ["-size", "-line-height", "-letter-spacing", "-word-spacing"];

    private static readonly double[] AllowedRadiiPx = [0, 2, 3, 6, 9999];

    /// <summary>
    /// The number of semantic colour names AC2 fixes. Asserted rather than assumed, because every
    /// completeness check in this class derives its expectation from the token layer.
    /// </summary>
    private const int ExpectedSemanticNameCount = 15;

    /// <summary>
    /// How far off a whole device pixel a snapped width may land. Loose enough for the repeating
    /// decimals exact snapping produces - 3/1.75 is 1.7142857px - and far tighter than the half
    /// pixel that causes the antialiasing in the first place.
    /// </summary>
    private const double SnapTolerance = 1e-3;

    /// <summary>
    /// Values that switch a property off rather than set it.
    /// </summary>
    private static readonly HashSet<string> AbsenceValues =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "none",
            "0",
            "0px",
            "unset",
            "initial",
        };

    /// <summary>
    /// Properties that size or space type. px is banned on all of them, everywhere, including in
    /// the token layer.
    /// </summary>
    private static readonly HashSet<string> TypographicProperties =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "font",
            "font-size",
            "line-height",
            "letter-spacing",
            "word-spacing",
        };

    /// <summary>
    /// The only properties outside the token layer that may carry a px literal: values that
    /// should NOT scale with text. Everything else takes its length from a token.
    /// </summary>
    private static readonly string[] PixelPermittedPropertyFragments =
        ["radius", "outline"];

    /// <summary>
    /// Physical properties with a logical equivalent. UX-DR42 makes RTL tolerance structural, and
    /// the token layer is where that is decided for every later story.
    /// </summary>
    private static readonly HashSet<string> PhysicalProperties =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "left",
            "right",
            "margin-left",
            "margin-right",
            "padding-left",
            "padding-right",
            "border-left",
            "border-right",
            "border-left-width",
            "border-right-width",
            "border-left-style",
            "border-right-style",
            "border-left-color",
            "border-right-color",
            "border-top-left-radius",
            "border-top-right-radius",
            "border-bottom-left-radius",
            "border-bottom-right-radius",
            "scroll-margin-left",
            "scroll-margin-right",
            "scroll-padding-left",
            "scroll-padding-right",
        };

    /// <summary>
    /// Properties whose <i>values</i> carry a physical direction.
    /// </summary>
    private static readonly HashSet<string> PhysicallyValuedProperties =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "float",
            "clear",
            "text-align",
            "text-align-last",
            "background-position",
            "background-position-x",
            "object-position",
            "transform-origin",
            "perspective-origin",
            "caption-side",
            "resize",
            "direction",
        };

    /// <summary>
    /// Shorthands whose multi-value form is physical: the 4-value order is
    /// top-<b>right</b>-bottom-<b>left</b>, which does not flip under RTL.
    /// </summary>
    /// <remarks>
    /// <c>margin: 0 0 0 var(--space-3)</c> is a physical left margin and passed the gate entirely,
    /// because the enumeration above lists only longhands. Two values are the block/inline pair and
    /// are direction-neutral, so only three and four components are refused.
    /// </remarks>
    private static readonly HashSet<string> PhysicalShorthandProperties =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "margin",
            "padding",
            "inset",
            "border-width",
            "border-style",
            "border-color",
            "scroll-margin",
            "scroll-padding",
        };

    /// <summary>
    /// The locales <c>text-transform</c> is lossy in, which a casing rule must exclude.
    /// </summary>
    /// <remarks>
    /// Turkish and Azeri: dotless i uppercases to I and changes the word. Greek: uppercasing
    /// strips accents and alters final sigma. These scripts HAVE case, so tracking is fine and
    /// only the transform is withheld - which is why they are a separate list from the case-less
    /// scripts <c>base.css</c> also excludes.
    /// </remarks>
    private static readonly string[] LossyCasingLocales = ["tr", "az", "el"];

    /// <summary>
    /// The 8 type roles <c>DESIGN.md</c> names. A surface picks a role; it does not pick a size.
    /// </summary>
    private static readonly string[] TypeRoles =
    [
        "task-title",
        "column-head",
        "space-name",
        "body",
        "dialog-title",
        "meta",
        "role-label",
        "presence-count",
    ];

    /// <summary>
    /// The five axes every role binds, so none of them is ever inherited by accident.
    /// </summary>
    private static readonly string[] TypeAxes =
        ["family", "size", "weight", "line-height", "letter-spacing"];

    /// <summary>
    /// Every non-type token the design's vocabulary depends on, by name.
    /// </summary>
    private static readonly string[] RequiredStructuralTokens =
    [
        "font-system-sans",
        "font-system-mono",
        "space-unit",
        "space-1",
        "space-2",
        "space-3",
        "space-4",
        "space-5",
        "space-6",
        "space-7",
        "space-gutter",
        "space-card-stack-gap",
        "space-card-pad-y",
        "space-card-pad-x",
        "space-control-pad-y",
        "space-control-pad-x",
        "target-min",
        "radius-sm",
        "radius-default",
        "radius-md",
        "radius-lg",
        "radius-full",
        "border-hairline-width",
        "border-emphasis-width",
        "motion-instant",
        "motion-quick",
        "motion-lift",
        "motion-settle",
        "motion-easing-standard",
        "motion-easing-exit",
        "motion-long-press-threshold",
        "motion-long-press-slop",
    ];

    /// <summary>
    /// Attributes whose literal value reaches a person's eyes or a screen reader.
    /// </summary>
    private static readonly HashSet<string> LocalisableAttributes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "title",
            "alt",
            "placeholder",
            "label",
            "aria-label",
            "aria-description",
            "aria-placeholder",
            "aria-roledescription",
            "aria-valuetext",
            "abbr",
            "download",
        };

    /// <summary>
    /// Input types whose <c>value</c> attribute is the button's visible label.
    /// </summary>
    /// <remarks>
    /// <c>value</c> is NOT in the list above, deliberately. On
    /// <c>&lt;option value="active"&gt;</c> and on most inputs it is a form value that no one
    /// reads, so a context-free rule would fail correct code. It is user-visible on exactly these
    /// three input types, which is why they get their own element-aware check.
    /// </remarks>
    private static readonly HashSet<string> LabelledInputTypes =
        new(StringComparer.OrdinalIgnoreCase) { "submit", "button", "reset" };

    /// <summary>
    /// The only word permitted as a literal in a component: the product name, which is a brand and
    /// is not translated in any locale.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The stated basis was wrong in both directions, and was corrected at code review on
    /// 2026-08-27.</b> This comment previously said the permitted set was "exactly the PRD section
    /// 2 Glossary proper nouns". It is not: <c>Yello</c> is not a Glossary entry at all, and the
    /// Glossary fixes Account, User, Space, Membership, Role, Owner, Admin, Member, Viewer,
    /// Invitation, Ownership Offer, Project, Task, Status, Assignee, Label, Board, List View,
    /// Presence, API Token and Session. Had the stated rule been the implemented one,
    /// <c>&lt;h2&gt;Board&lt;/h2&gt;</c>, <c>&lt;p&gt;Owner&lt;/p&gt;</c> and
    /// <c>&lt;span&gt;Viewer&lt;/span&gt;</c> would all have been permitted literals - which would
    /// gut this gate, since those are precisely the words the product's own copy is made of. The
    /// implemented rule is the defensible one; only its description was wrong.
    /// </para>
    /// <para>
    /// This is a narrow rule with a stated reason, not an escape hatch. <c>App.razor</c> renders
    /// the product name, and the product name is a brand: localising it would be wrong, and a
    /// resource entry for it would externalise a string that has exactly one value in every
    /// language. Every OTHER literal fails, which is what the planted-violation results in the Dev
    /// Agent Record demonstrate.
    /// </para>
    /// <para>
    /// It is deliberately <b>not</b> a general "short strings are fine" or "no vowels" heuristic.
    /// Those would exempt a category; this exempts one word, by name, for a reason that is
    /// checkable.
    /// </para>
    /// <para>
    /// <b>The declared variance, recorded rather than exempted silently:</b> <c>index.html</c>
    /// carries English text - "Loading Yello", "An unhandled error has occurred.", "Reload" - and
    /// this gate does not cover it. That is not an oversight and not a hole. Those strings are
    /// emitted by a static file before the WebAssembly runtime exists, so they sit outside Blazor
    /// localisation entirely and no resource lookup could serve them. The gate scans
    /// <c>*.razor</c>, where localisation is actually available. Should Yello ever need a
    /// localised pre-boot page, that is a build-time substitution and a new decision.
    /// </para>
    /// </remarks>
    private static readonly HashSet<string> GlossaryProperNouns =
        new(StringComparer.Ordinal) { "Yello" };

    /// <summary>
    /// No component references a <c>-light</c> token. AC1.
    /// </summary>
    /// <remarks>
    /// A component reaching for a <c>-light</c> token pins itself to one theme, which is why this
    /// is gated rather than discouraged. The permitted region is the theme boundary in
    /// <c>tokens.css</c>, located by its marker comments - and
    /// <c>CssCorpus.ThemeBoundaryRange</c> refuses to run at all unless there is exactly one
    /// well-formed pair of markers, so a second "boundary" cannot be planted to widen the
    /// permission.
    /// <para>
    /// Markup is scanned as well as CSS. A <c>style="background: var(--surface-card-light)"</c>
    /// attribute in a <c>.razor</c> is the same defect and would be invisible to a CSS-only scan.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_component_references_a_light_theme_token()
    {
        var problems = new List<string>();

        foreach (var sheet in CssCorpus.StyleSheets)
        {
            var isTokenLayer = sheet.File.Name.Equals(CssCorpus.TokensFileName, StringComparison.OrdinalIgnoreCase);

            problems.AddRange(
                from match in LightTokenReferencePattern.Matches(sheet.Blanked).Cast<Match>()
                where !(isTokenLayer && CssCorpus.IsInsideThemeBoundary(match.Index))
                select $"{sheet.Path}:{CssCorpus.LineAt(sheet.Raw, match.Index)} references " +
                       $"'--{match.Groups[1].Value}'.");
        }

        foreach (var markup in CssCorpus.MarkupFiles)
        {
            problems.AddRange(
                from match in LightTokenNamePattern.Matches(markup.Blanked).Cast<Match>()
                select $"{markup.Path}:{CssCorpus.LineAt(markup.Raw, match.Index)} references " +
                       $"'--{match.Groups[1].Value}'.");
        }

        AssertNoProblems(problems,
            "A `-light` token is referenced outside the theme boundary.",
            "AC1: every semantic name resolves to its unsuffixed value by default and to its " +
            "`-light` sibling under the light theme, resolved ONCE at the theme boundary in " +
            "tokens.css. Consume `var(--surface-card)`, never `var(--surface-card-light)` - the " +
            "latter pins the component to one theme.");
    }

    /// <summary>
    /// Every rule in the theme boundary rebinds all 15 semantic names, and rebinds each to its own
    /// <c>-light</c> sibling. AC1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The boundary answers two triggers - the OS preference and an explicit <c>data-theme</c> -
    /// because no upstream document decides which selects the theme, and building it this way
    /// makes a stored preference a one-line change later. A media condition and an attribute
    /// selector cannot be unioned into a single CSS rule, so the boundary is one region holding
    /// two rules rather than one rule.
    /// </para>
    /// <para>
    /// That shape has a failure mode this gate exists to close: <b>a partial rule</b>. If the
    /// <c>data-theme</c> rule rebound 14 of the 15 names, a user with a stored light preference on
    /// a dark OS would get one dark token on a light ground - and the contrast harness, which
    /// reads the FIRST boundary rule, would compute a passing palette and report green. So every
    /// rule in the region must be complete, and the count of rules is deliberately not pinned:
    /// adding a third trigger is allowed, and it will be held to the same completeness.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_theme_boundary_rule_rebinds_all_fifteen_semantic_names()
    {
        var problems = new List<string>();
        var semanticNames = SemanticNamesFromTokenLayer();

        if (CssCorpus.ThemeBoundaryRules.Count == 0)
        {
            problems.Add(
                "The theme boundary contains no rule that rebinds a custom property, so the light " +
                "theme resolves to nothing.");
        }

        // The fifteen in this test's name is now ASSERTED, not merely implied. The expectation is
        // derived from the token layer - which keeps a second copy of the list out of this class -
        // but a derived expectation of nothing is satisfied by rebinding nothing: if the semantic
        // declarations were emptied or moved, the loop below would iterate zero names, find no
        // problems, and the name would still claim fifteen.
        if (semanticNames.Count != ExpectedSemanticNameCount)
        {
            problems.Add(
                $"{semanticNames.Count} semantic colour names were found in the token layer " +
                $"outside the theme boundary; AC2 states exactly {ExpectedSemanticNameCount}. " +
                "Every assertion below is derived from this list, so a short list is this gate " +
                "checking less than its name claims rather than a palette that is merely wrong.");
        }

        for (var index = 0; index < CssCorpus.ThemeBoundaryRules.Count; index++)
        {
            var rule = CssCorpus.ThemeBoundaryRules[index];
            var bindings = CssCorpus.ThemeBoundaryBindings[index];
            var where = $"{CssCorpus.Tokens.Path}:{CssCorpus.LineAt(CssCorpus.Tokens.Raw, rule.Offset)}";

            problems.AddRange(semanticNames
                .Where(name => !bindings.ContainsKey(name))
                .Select(name =>
                    $"{where} ('{rule.Selector}') does not rebind '--{name}', so that one token " +
                    "stays at its dark value under the light theme."));

            problems.AddRange(semanticNames
                .Where(bindings.ContainsKey)
                .Where(name => !bindings[name].Equals($"var(--{name}-light)", StringComparison.Ordinal))
                .Select(name =>
                    $"{where} ('{rule.Selector}') rebinds '--{name}' to '{bindings[name]}' rather " +
                    $"than to 'var(--{name}-light)'."));

            // Every trigger must also switch the UA's own widgets. Both rules set this today and
            // nothing required it, so a third trigger could rebind all fifteen colours and leave
            // scrollbars, form controls and the caret rendering dark on a light ground.
            if (!rule.Declarations.Any(d =>
                    d.Property.Equals("color-scheme", StringComparison.OrdinalIgnoreCase)
                    && d.Value.Contains("light", StringComparison.OrdinalIgnoreCase)))
            {
                problems.Add(
                    $"{where} ('{rule.Selector}') does not declare 'color-scheme: light', so the " +
                    "UA-drawn widgets stay dark while the palette goes light.");
            }
        }

        AssertNoProblems(problems,
            "A theme-boundary rule does not resolve the whole palette.",
            "Each rule in the boundary must rebind all 15 semantic names to their own `-light` " +
            "siblings. A rule that rebinds only some of them leaves a dark token on a light " +
            "ground for whichever trigger that rule serves - and the contrast harness reads the " +
            "first rule, so it would not notice.");
    }

    /// <summary>
    /// No colour is stated as a literal outside the token layer. AC1, AC2.
    /// </summary>
    /// <remarks>
    /// A component writing <c>#18213C</c> gets the dark card ground in both themes and is invisible
    /// to the theme boundary, to the 30-token count and to the contrast harness all at once - the
    /// same defect as referencing a <c>-light</c> token, arrived at from the other direction.
    /// Colour functions are refused for the same reason and additionally because a computed colour
    /// cannot be verified against a threshold from the CSS text alone.
    /// </remarks>
    [Fact]
    public void No_colour_is_stated_as_a_literal_outside_the_token_layer()
    {
        var problems = new List<string>();

        foreach (var (sheet, _, declaration) in CssCorpus.AllDeclarations())
        {
            if (sheet.File.Name.Equals(CssCorpus.TokensFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            problems.AddRange(
                from match in ColourLiteralPattern.Matches(declaration.Value).Cast<Match>()
                select $"{Where(sheet, declaration)} states '{match.Value}' in " +
                       $"'{declaration.Property}'.");
        }

        AssertNoProblems(problems,
            "A colour is stated as a literal outside tokens.css.",
            "Every colour comes from one of the 30 tokens. A literal bypasses the theme boundary " +
            "(so it renders the same in both themes), the 30-token count, and the contrast " +
            "harness - which computes only what the token layer declares.");
    }

    /// <summary>
    /// Type is never sized or spaced in absolute pixels, and the root font size is never overridden
    /// in px. AC3.
    /// </summary>
    /// <remarks>
    /// Absolute px type ignores a user's browser font-size preference entirely. That is the most
    /// common low-vision accommodation and what WCAG 1.4.4 is really about, so this is the gate
    /// AC3 exists for rather than a stylistic rule.
    /// <para>
    /// The root is checked separately and held to a percentage. <c>html { font-size: 16px }</c>
    /// defeats the whole <c>rem</c> scale in one line while every individual size stays in
    /// <c>rem</c> and passes the scan above it.
    /// </para>
    /// </remarks>
    [Fact]
    public void Type_is_never_sized_in_absolute_pixels()
    {
        var problems = new List<string>();

        foreach (var (sheet, rule, declaration) in CssCorpus.AllDeclarations())
        {
            // Custom properties count too. The type scale's sizes and line-heights are declared as
            // `--type-*-size` and `--type-*-line-height`, so a check written only against the CSS
            // property names never looked at the file where the values actually live.
            if (!IsTypographicDeclaration(declaration))
            {
                continue;
            }

            problems.AddRange(CssCorpus.PixelLengths(declaration.Value)
                .Select(length =>
                    $"{Where(sheet, declaration)} sets '{declaration.Property}' to a px length " +
                    $"({length}px) in '{rule.Selector}'."));

            // Every ABSOLUTE unit, not px alone. `font-size: 13pt` ignores the user's root size
            // exactly as `13px` does - WCAG 1.4.4 is about the user's font-size preference, not
            // about one spelling of one unit - and a px-only ban passed it, along with pc, mm, cm,
            // in and Q.
            problems.AddRange(AbsoluteNonPixelLengths(declaration.Value)
                .Select(length =>
                    $"{Where(sheet, declaration)} sets '{declaration.Property}' to the absolute " +
                    $"length '{length}' in '{rule.Selector}'. Absolute type ignores the user's " +
                    "browser font-size preference in whatever unit it is written."));

            // AC3's line-height floor, which nothing asserted. The remedy message below has always
            // claimed "line-heights are >= 1.5"; until now no gate read a line-height value at all,
            // and `line-height: 1.1` passed. A line box exactly the glyph height cannot absorb the
            // 1.5x override WCAG 1.4.12 lets a user apply.
            if (IsLineHeightDeclaration(declaration.Property)
                && UnitlessRatio(CssCorpus.Resolve(declaration.Value)) is { } ratio
                && ratio < MinimumLineHeight - LengthTolerance)
            {
                problems.Add(
                    $"{Where(sheet, declaration)} sets 'line-height' to {ratio} on " +
                    $"'{rule.Selector}', below the {MinimumLineHeight} floor.");
            }

            if (IsRootSelector(rule.Selector)
                && declaration.Property.Equals("font-size", StringComparison.OrdinalIgnoreCase)
                && !declaration.Value.Contains('%', StringComparison.Ordinal))
            {
                problems.Add(
                    $"{Where(sheet, declaration)} overrides the root font-size with " +
                    $"'{declaration.Value}'. It must be a percentage - 100% - or absent, so the " +
                    "rem scale is measured against the user's own root size.");
            }
        }

        AssertNoProblems(problems,
            "Type is sized in an absolute unit, or a line-height is below the floor.",
            $"Sizes are in `rem` against a 16px root and line-heights are >= {MinimumLineHeight}. " +
            "Absolute units - px, pt, pc, mm, cm, in, Q - are banned on type in every file, the " +
            "token layer included, because they ignore the user's own font-size preference.");
    }

    /// <summary>
    /// Outside the token layer, a px literal appears only on a radius or an outline. AC3.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This gate reads DECLARED text, not resolved values, and that is deliberate.</b> Its
    /// whole point is that an author should reach for a token rather than type a length;
    /// resolving first would make every correct <c>min-height: var(--target-min)</c> report as a
    /// 24px literal, and the gate would fail the code it is meant to protect.
    /// </para>
    /// <para>
    /// <b>The AC's own phrasing is narrowed here, with the reason stated.</b> Task 4 words this
    /// as "px permitted only on border widths, radii and outline-offset". Taken literally that
    /// fails the token layer itself: <c>DESIGN.md:99-113</c> states the 3/6/9/12/18/24/36 spacing
    /// scale, the 24px target floor and the 10px long-press slop in px on purpose, because those
    /// are structural steps that must not scale with text. AC3's own text confines the
    /// restriction to type - <i>"never on type"</i> - so the type ban above is absolute and
    /// applies inside <c>tokens.css</c> too, while this confinement gate exempts the token layer
    /// and requires every other file to take its lengths from a token. Border widths are covered
    /// by the hairline gate rather than by being exempted here, which is stricter, not looser.
    /// </para>
    /// </remarks>
    [Fact]
    public void Pixel_lengths_outside_the_token_layer_come_from_a_token()
    {
        var problems = new List<string>();

        foreach (var (sheet, rule, declaration) in CssCorpus.AllDeclarations())
        {
            if (sheet.File.Name.Equals(CssCorpus.TokensFileName, StringComparison.OrdinalIgnoreCase)
                || IsPixelPermitted(declaration.Property))
            {
                continue;
            }

            problems.AddRange(RawPixelLengths(declaration.Value)
                .Select(length =>
                    $"{Where(sheet, declaration)} writes '{length}px' directly in " +
                    $"'{declaration.Property}' ('{rule.Selector}')."));
        }

        AssertNoProblems(problems,
            "A px length is written directly outside the token layer.",
            "Lengths come from tokens.css - the spacing scale, the target floor, the border " +
            "widths, the radii - so one edit moves the whole product. px literals are permitted " +
            "only on radii and outlines, and border widths are held to the 1.5px hairline floor " +
            "by their own gate.");
    }

    /// <summary>
    /// The focus ring is never removed, and never drawn inset or at offset zero. AC6.
    /// </summary>
    /// <remarks>
    /// The intuitive rationale for a separate <c>focus-ring</c> token - "so it does not vanish
    /// against an accented control" - does not survive arithmetic: the two sit at 1.45 dark and
    /// 1.48 light, and the ring WOULD vanish. What protects it is <c>outline-offset: 2px</c>,
    /// which puts the ring on the ground behind the control where it reads at 7.17. So the real
    /// rule is geometric, and this gate is about geometry.
    /// <para>
    /// Outline removal is banned outright rather than "banned unless a <c>:focus-visible</c>
    /// treatment accompanies it". The common <c>:focus { outline: none }</c> plus
    /// <c>:focus-visible { outline: ... }</c> pairing is unnecessary here - <c>:focus-visible</c>
    /// already scopes the ring to keyboard focus - and permitting removal anywhere makes the gate
    /// depend on rule ORDER and specificity, which it cannot evaluate. An absolute rule cannot be
    /// got wrong.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_focus_ring_is_never_removed_or_drawn_at_offset_zero()
    {
        var problems = new List<string>();

        foreach (var (sheet, rule, declaration) in CssCorpus.AllDeclarations())
        {
            if (!declaration.Property.StartsWith("outline", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (RemovesOutline(declaration))
            {
                problems.Add(
                    $"{Where(sheet, declaration)} removes the outline: " +
                    $"'{declaration.Property}: {declaration.Value}' on '{rule.Selector}'.");
            }

            if (!declaration.Property.Equals("outline-offset", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Measured through AbsoluteLengthsPx, and zero is treated as a stated offset even
            // when it carries no unit. `outline-offset: 0` is legal CSS, is the case AC6 and
            // DESIGN.md:358 name in words, and is the case this test is NAMED after - and the
            // px-only reading saw no number in it at all, so it was never compared. `0em` and
            // `0rem` escaped the same way. Task 5 planted `-2px`, which was caught, so the
            // planting never reached the case the requirement actually calls out.
            var offsets = StatedLengthsPx(declaration.Value).ToList();

            problems.AddRange(offsets
                .Where(offset => offset < FocusRingFloorPx - LengthTolerance)
                .Select(offset =>
                    $"{Where(sheet, declaration)} sets 'outline-offset' to {offset}px on " +
                    $"'{rule.Selector}'. Below {FocusRingFloorPx}px the ring is drawn on the " +
                    "control rather than on the ground behind it; at or below 0 it is inset, " +
                    "which is what makes it vanish against an accented control."));
        }

        AssertNoProblems(problems,
            "The focus ring has been removed, inset, or drawn at too small an offset.",
            "A 2px ring at a 2px outline-offset, never inset, never at offset 0, and never " +
            "replaced by a colour change or a border swap. Compliance depends on the offset, not " +
            "on the token separation.");
    }

    /// <summary>
    /// A visible focus treatment is declared for <c>:focus-visible</c>, from the focus-ring token.
    /// AC6.
    /// </summary>
    /// <remarks>
    /// The gate above is an absence assertion, and on a tree with no components an absence
    /// assertion is indistinguishable from a working one. This is its positive half: the ring has
    /// to actually exist, be at least 2px, sit at least 2px off the control, and take its colour
    /// from <c>--focus-ring</c> rather than from a literal or from <c>--accent</c>.
    /// </remarks>
    [Fact]
    public void A_visible_focus_treatment_is_declared_for_focus_visible()
    {
        var problems = new List<string>();

        // A rule that only sets `outline-offset` does NOT count as a focus treatment. Filtering on
        // `outline*` did, and a planted `.x:focus-visible { outline-offset: -2px }` was enough to
        // satisfy this gate while `base.css` had no focus rule at all - the gate found a
        // "treatment", found no ring to measure, and reported green. Caught by Task 5's planting.
        var visibleFocusRules = CssCorpus.AllRules()
            .Where(p => p.Rule.Selector.Contains(":focus-visible", StringComparison.OrdinalIgnoreCase))
            .Where(p => p.Rule.Declarations.Any(IsRingDeclaration))
            .ToList();

        if (visibleFocusRules.Count == 0)
        {
            problems.Add(
                "No rule in the repository draws an outline on ':focus-visible'. Keyboard parity " +
                "on the Board is a PRD requirement, which makes one visible focus treatment " +
                "load-bearing rather than decorative.");
        }

        // EVERY focus ring is held to the specification, not only the `:focus-visible` ones. The
        // narrower filter meant `.x:focus { outline: 1px solid var(--focus-ring); outline-offset: 0 }`
        // was ungated entirely - a sub-2px ring drawn on the control, which is both halves of AC6
        // broken on a selector the gate never looked at.
        var everyFocusRule = CssCorpus.AllRules()
            .Where(p => p.Rule.Selector.Contains(":focus", StringComparison.OrdinalIgnoreCase))
            .Where(p => p.Rule.Declarations.Any(IsRingDeclaration));

        foreach (var (sheet, rule) in everyFocusRule)
        {
            problems.AddRange(DescribeFocusTreatment(sheet, rule));
        }

        AssertNoProblems(problems,
            "The `:focus-visible` treatment is missing or does not meet the specification.",
            "AC6: a 2px ring in var(--focus-ring) at a 2px outline-offset. The offset is what " +
            "makes it visible - the ring is only 1.45 against the accent it may sit on.");
    }

    /// <summary>
    /// No surface carries a shadow. AC7.
    /// </summary>
    /// <remarks>
    /// There is no elevation in this design, and it follows from the palette rather than from
    /// preference: the grounds are tinted and close in luminance, so a shadow on them reads as a
    /// smudge rather than a lift. Structure comes from the hairline and the three-step tonal
    /// ladder.
    /// <para>
    /// <c>DESIGN.md:421</c> sets <c>shadow: none</c> explicitly on the task card "so nobody adds
    /// one back". The single sanctioned exception is the lifted card under an active drag, which
    /// <b>story 2.7 builds</b> - so today this gate asserts no shadow at all, and the story that
    /// adds the exception has to widen it deliberately. NFR-3's 16 ms render budget at NFR-8's
    /// 5,000-Task bound is the second reason: shadows, filters and backdrop-blur are exactly what
    /// that budget cannot afford.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_surface_carries_a_shadow()
    {
        var problems = new List<string>();

        foreach (var (sheet, rule, declaration) in CssCorpus.AllDeclarations())
        {
            var isShadowProperty = declaration.Property.Contains("shadow", StringComparison.OrdinalIgnoreCase);
            var hasShadowFunction = declaration.Value.Contains("drop-shadow(", StringComparison.OrdinalIgnoreCase);

            if (!isShadowProperty && !hasShadowFunction)
            {
                continue;
            }

            // `box-shadow: none` is a removal, not a shadow, and story 2.7's lifted card will
            // need to reset it. Refusing it would make the gate unsatisfiable for that story
            // without also permitting the shadow itself.
            if (isShadowProperty && IsNoneValue(declaration.Value))
            {
                continue;
            }

            problems.Add(
                $"{Where(sheet, declaration)} declares '{declaration.Property}: " +
                $"{declaration.Value}' on '{rule.Selector}'.");
        }

        AssertNoProblems(problems,
            "A shadow has been added.",
            "Shadow is not a hierarchy device here: on tinted, close-luminance grounds it reads " +
            "as a smudge. The one sanctioned exception is the lifted card under an active drag, " +
            "which story 2.7 builds - widening this gate is that story's job, deliberately.");
    }

    /// <summary>
    /// No structural border is thinner than the 1.5px hairline. AC7.
    /// </summary>
    /// <remarks>
    /// This is an accessibility requirement, not a stylistic one. A 1px border is antialiased
    /// whenever its edge lands off a device-pixel boundary - which a 3px spacing grid does at the
    /// 1.25x, 1.5x and 1.75x display scales common on Windows and Android - and composited at 80%
    /// coverage, every border pair in both themes drops below the 3:1 gate.
    /// <para>
    /// Zero is permitted: that is the absence of a border, not a thin one.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_structural_border_is_thinner_than_the_hairline_width()
    {
        var problems = new List<string>();

        foreach (var (sheet, rule, declaration) in CssCorpus.AllDeclarations())
        {
            if (!IsStructuralEdgeProperty(declaration.Property, rule.Selector))
            {
                continue;
            }

            // Measured in every convertible unit and in the keywords, not px alone.
            // `border-block-start: 0.0625rem` is 1px and passed; so did `border: thin`, and
            // `border-width: 0.1em`. A floor defeated by a unit change is not a floor.
            problems.AddRange(CssCorpus.AbsoluteLengthsPx(declaration.Value)
                .Concat(CssCorpus.BorderWidthKeywordsPx(declaration.Value))
                .Where(width => width > LengthTolerance && width < HairlineFloorPx - LengthTolerance)
                .Select(width =>
                    $"{Where(sheet, declaration)} sets '{declaration.Property}' to " +
                    $"'{declaration.Value}' ({width}px) on '{rule.Selector}'."));
        }

        AssertNoProblems(problems,
            $"A structural border is thinner than the {HairlineFloorPx}px hairline.",
            "Use var(--border-hairline-width). A 1px border antialiases off the device-pixel grid " +
            "at the display scales this product will actually run at, and at 80% coverage every " +
            "border pair in both themes drops below the 3:1 gate.");
    }

    /// <summary>
    /// The interactive target floor is 24px and is taken from the token. AC8.
    /// </summary>
    /// <remarks>
    /// Stated precisely because it is easy to get wrong in both directions. WCAG 2.1 AA - the
    /// version NFR-9 names - has <b>no</b> target-size criterion at all; 2.5.5's 44x44 is AAA.
    /// WCAG 2.2 AA's 2.5.8 sets 24x24. So 24px is the real current AA floor and a deliberate
    /// commitment above the PRD's stated version. It is not an error and must not be "corrected"
    /// down.
    /// </remarks>
    [Fact]
    public void The_interactive_target_floor_is_declared_and_never_lowered()
    {
        var problems = new List<string>();

        if (!CssCorpus.TokenValues.TryGetValue("target-min", out var declared))
        {
            problems.Add("The token layer declares no '--target-min'.");
        }
        else if (!CssCorpus.PixelLengths(declared).Any(px => Math.Abs(px - TargetFloorPx) < LengthTolerance))
        {
            problems.Add(
                $"'--target-min' is '{declared}', not {TargetFloorPx}px. WCAG 2.2 AA's 2.5.8 sets " +
                "24x24, and that figure is the one this design commits to.");
        }
        else
        {
            // Declared, and declared at the floor the design commits to.
        }

        var usesToken = CssCorpus.AllDeclarations().Any(d =>
            IsMinimumHeightProperty(d.Declaration.Property)
            && CssCorpus.ReferencedTokens(d.Declaration.Value).Contains("target-min", StringComparer.Ordinal));

        if (!usesToken)
        {
            problems.Add(
                "No rule applies 'min-height: var(--target-min)'. The floor has to be acquired by " +
                "default from a base rule, not by each component remembering it.");
        }

        foreach (var (sheet, rule, declaration) in CssCorpus.AllDeclarations())
        {
            if (!IsMinimumSizeProperty(declaration.Property))
            {
                continue;
            }

            // Both axes, and every convertible unit. 2.5.8 is 24x24, which every comment in this
            // file and in tokens.css argues from - but only the block axis was checked, so
            // `min-inline-size: 8px` was unpoliced, and `min-height: 1rem` (16px) passed because
            // only px lengths were read.
            problems.AddRange(CssCorpus.AbsoluteLengthsPx(declaration.Value)
                .Where(px => px > LengthTolerance && px < TargetFloorPx - LengthTolerance)
                .Select(px =>
                    $"{Where(sheet, declaration)} sets '{declaration.Property}' to " +
                    $"'{declaration.Value}' ({px}px) on '{rule.Selector}', below the " +
                    $"{TargetFloorPx}px floor."));
        }

        AssertNoProblems(problems,
            "The interactive target floor is missing, wrong, or lowered somewhere.",
            "24px minimum height on every interactive component, from var(--target-min). If " +
            "something genuinely must be smaller than the floor, it should not be declaring a " +
            "minimum height at all.");
    }

    /// <summary>
    /// Only the four radius values are used. AC9.
    /// </summary>
    /// <remarks>
    /// 2px on the Role chip, Label chips and the Offer indicator; 3px on Tasks, columns, the
    /// context bar, buttons and avatars; 6px on dialogs, the Task detail panel and the invitation
    /// view - the only radius that soft, marking those surfaces as out of plane. Corners this
    /// tight read as engineered rather than friendly.
    /// <para>
    /// Percentages are refused rather than measured: <c>border-radius: 50%</c> is how a circular
    /// avatar arrives, and avatars are squared to 3px on purpose - "a deliberate break from
    /// convention, and part of why the surface reads as a tool".
    /// </para>
    /// </remarks>
    [Fact]
    public void Only_the_four_radius_values_are_used()
    {
        var problems = new List<string>();

        foreach (var (sheet, rule, declaration) in CssCorpus.AllDeclarations())
        {
            if (!declaration.Property.Contains("radius", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var resolved = CssCorpus.Resolve(declaration.Value);

            problems.AddRange(NonPixelLengths(resolved)
                .Select(length =>
                    $"{Where(sheet, declaration)} sets '{declaration.Property}' to '{length}' on " +
                    $"'{rule.Selector}', which is not one of the four radius values."));

            problems.AddRange(CssCorpus.PixelLengths(declaration.Value)
                .Where(px => !Array.Exists(AllowedRadiiPx, allowed => Math.Abs(allowed - px) < LengthTolerance))
                .Select(px =>
                    $"{Where(sheet, declaration)} sets '{declaration.Property}' to {px}px on " +
                    $"'{rule.Selector}'."));
        }

        AssertNoProblems(problems,
            "A radius outside the scale is used.",
            "The scale is 2px, 3px, 6px and the fully-round 9999px, and nothing else. Use " +
            "var(--radius-sm), var(--radius-md), var(--radius-lg) or var(--radius-full).");
    }

    /// <summary>
    /// The fully-round radius is declared once and reached by at most one component. AC9.
    /// </summary>
    /// <remarks>
    /// <c>rounded.full</c> exists for exactly one component - the column count chip, the only pill
    /// in the product. The literal is required to appear exactly once, which is its declaration in
    /// the token layer: that is what stops a component spelling out <c>9999px</c> and bypassing
    /// the reference count entirely.
    /// <para>
    /// The reference count is gated at <b>at most one</b>, not exactly one, because the column
    /// count chip is built by epic 2 and does not exist yet - so today the honest assertion is
    /// zero-or-one, and a second pill fails. Tightening it to exactly one is the job of the story
    /// that builds the chip. Stated plainly rather than gated at 1 and quietly disabled.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_fully_round_radius_is_declared_once_and_used_by_at_most_one_component()
    {
        var problems = new List<string>();

        var literalOccurrences = CssCorpus.StyleSheets
            .Sum(sheet => CssCorpus.Occurrences(sheet.Blanked, "9999px").Count());

        if (literalOccurrences != 1)
        {
            problems.Add(
                $"'9999px' appears {literalOccurrences} times in the stylesheets; it must appear " +
                "exactly once, as the --radius-full declaration in the token layer.");
        }

        var consumers = CssCorpus.AllDeclarations()
            .Where(d => !d.Sheet.File.Name.Equals(CssCorpus.TokensFileName, StringComparison.OrdinalIgnoreCase))
            .Where(d => CssCorpus.ReferencedTokens(d.Declaration.Value).Contains("radius-full", StringComparer.Ordinal))
            .Select(d => $"{Where(d.Sheet, d.Declaration)} ('{d.Rule.Selector}')")
            .ToList();

        if (consumers.Count > 1)
        {
            problems.Add(
                $"var(--radius-full) is used by {consumers.Count} rules: " +
                $"{string.Join(", ", consumers)}. It exists for exactly one component - the " +
                "column count chip, the only pill in the product.");
        }

        AssertNoProblems(problems,
            "The fully-round radius is not confined to one component.",
            "Keep var(--radius-full) to the column count chip. No other pills, and no circular " +
            "avatars.");
    }

    /// <summary>
    /// The text link is underlined, and no rule removes an underline from a link. AC10.
    /// </summary>
    /// <remarks>
    /// The accent passes handsomely against the background - 4.96 on the dark card, 6.81 on the
    /// light - and sits at only <b>2.66</b> against the body text beside it. For a link inside a
    /// sentence the text beside it is the pair that matters, and 2.66 is below the 3:1 WCAG 1.4.1
    /// requires when colour alone distinguishes a link from its surrounding text. The underline is
    /// what carries it, which is why removing one is gated and not merely discouraged.
    /// <para>
    /// The removal ban is scoped to selectors that target a link, because standalone accent
    /// controls legitimately do not need an underline - it is the link inside prose that does.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_text_link_is_underlined_and_no_rule_removes_it()
    {
        var problems = new List<string>();

        // The `.text-link` class itself, not any selector whose text happens to contain
        // "text-link". A `.text-link-quiet` keeping its underline while `.text-link` lost one
        // satisfied the substring reading, so the class AC10 is actually about could be left
        // un-underlined with the gate green.
        var underlined = CssCorpus.AllRules()
            .Where(p => TextLinkClassPattern.IsMatch(p.Rule.Selector))
            .Any(p => p.Rule.Declarations.Any(d =>
                IsTextDecorationProperty(d.Property)
                && d.Value.Contains("underline", StringComparison.OrdinalIgnoreCase)));

        if (!underlined)
        {
            problems.Add(
                "No rule whose selector names a text link declares 'text-decoration: underline'. " +
                "The accent is 2.66 against body text, so colour alone does not distinguish the " +
                "link and the underline is the only thing that does.");
        }

        foreach (var (sheet, rule, declaration) in CssCorpus.AllDeclarations())
        {
            if (!IsTextDecorationProperty(declaration.Property)
                || !IsNoneValue(declaration.Value)
                || !SelectorTargetsLink(rule.Selector))
            {
                continue;
            }

            problems.Add(
                $"{Where(sheet, declaration)} removes the underline from a link: " +
                $"'{declaration.Property}: {declaration.Value}' on '{rule.Selector}'.");
        }

        AssertNoProblems(problems,
            "The text link is not underlined, or a rule removes its underline.",
            "A link inside a sentence is underlined, always. Standalone accent controls do not " +
            "need it; a link in prose does, because the accent fails against the text beside it.");
    }

    /// <summary>
    /// No user-visible string literal appears in a component. AC11.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Scans text nodes and localisable attributes in every <c>.razor</c> file. Razor comments,
    /// directives, <c>@code</c> and <c>@{}</c> blocks and Razor expressions are removed first, so
    /// what remains is what a person actually reads.
    /// </para>
    /// <para>
    /// The one permitted literal is the product name, a PRD section 2 Glossary proper noun - see
    /// <see cref="GlossaryProperNouns"/>, which also records why <c>index.html</c>'s English
    /// strings are a declared variance rather than a hole.
    /// </para>
    /// <para>
    /// This gate has no component to run against today, which is exactly why it was proved
    /// against a planted violation a later story would plausibly write: a component with a
    /// hard-coded English label. The result is in the story's Dev Agent Record.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_user_visible_string_literal_appears_in_a_component()
    {
        var problems = new List<string>();

        foreach (var markup in CssCorpus.MarkupFiles)
        {
            if (!markup.File.Extension.Equals(".razor", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var scannable = BlankRazorNonMarkup(markup.Blanked);

            problems.AddRange(
                from node in TextNodes(scannable)
                let literal = TranslatableText(node.Text)
                where literal.Length > 0
                select $"{markup.Path}:{CssCorpus.LineAt(markup.Raw, node.Offset)} renders the " +
                       $"literal text '{literal}'.");

            problems.AddRange(
                from attribute in LocalisableAttributeValues(scannable)
                    .Concat(LabelledInputValues(scannable))
                let literal = TranslatableText(attribute.Value)
                where literal.Length > 0
                select $"{markup.Path}:{CssCorpus.LineAt(markup.Raw, attribute.Offset)} sets " +
                       $"'{attribute.Name}' to the literal '{literal}'.");
        }

        AssertNoProblems(problems,
            "A user-visible string literal appears in a component.",
            "All copy is externalised into resources. German and Finnish run 30-40% longer than " +
            "English, and casing must come from `text-transform` rather than from the string - a " +
            "resource holding `VIEWER` makes the Role's accessible name get spelled out letter by " +
            "letter by JAWS and VoiceOver.");
    }

    /// <summary>
    /// No user-visible copy is written as a C# string literal in a component. AC11.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The markup gate above blanks <c>@code</c> and <c>@functions</c> before scanning, and reads
    /// no <c>.cs</c> file at all - so the most idiomatic place a Blazor developer writes copy was
    /// out of scope entirely. A planted
    /// <c>@code { private const string Label = "Loading your Spaces, please wait"; }</c> rendered
    /// through <c>&lt;span&gt;@Label&lt;/span&gt;</c> reported green, and the story's own planting
    /// table recorded the <c>@code</c> exclusion as correct behaviour.
    /// </para>
    /// <para>
    /// <b>The heuristic is deliberately conservative, and the direction of its error is chosen.</b>
    /// C# strings in a component are mostly not copy - CSS class names, route templates, element
    /// ids, format specifiers - so a rule that flagged every literal would fail correct code on
    /// the first component and be switched off. What is flagged is a literal that looks like a
    /// SENTENCE: two or more words, initial capital, and none of the punctuation that marks a
    /// value up as machine-facing. That catches copy as it is actually written and leaves
    /// <c>"btn btn-primary"</c>, <c>"/board/{id}"</c> and <c>"yyyy-MM-dd"</c> alone. It therefore
    /// under-reports - all-lowercase copy passes - which is the right way round: a false negative
    /// here leaves a string for a human to notice, while a false positive blocks a correct build.
    /// </para>
    /// <para>
    /// Scope is the component surface only: <c>@code</c>/<c>@functions</c> blocks in
    /// <c>*.razor</c>, plus <c>*.razor.cs</c> code-behind. Ordinary <c>.cs</c> files are not
    /// components and carry log and exception messages that are not localised.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_user_visible_copy_is_written_as_a_csharp_string_literal()
    {
        var problems = new List<string>();

        foreach (var markup in CssCorpus.MarkupFiles)
        {
            if (!markup.File.Extension.Equals(".razor", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            problems.AddRange(SentenceLiterals(RazorCodeBlocksOnly(markup.Blanked))
                .Select(literal =>
                    $"{markup.Path}:{CssCorpus.LineAt(markup.Raw, literal.Offset)} declares the " +
                    $"copy literal \"{literal.Text}\" in C#."));
        }

        foreach (var file in RepositoryLayout.EnumerateSourceFiles("*.razor.cs"))
        {
            var text = File.ReadAllText(file.FullName);

            problems.AddRange(SentenceLiterals(text)
                .Select(literal =>
                    $"{RepositoryLayout.RelativePath(file)}:{CssCorpus.LineAt(text, literal.Offset)} " +
                    $"declares the copy literal \"{literal.Text}\" in C#."));
        }

        AssertNoProblems(problems,
            "User-visible copy is written as a C# string literal in a component.",
            "All copy is externalised into resources, wherever it is written - markup, an `@code` " +
            "block, or a `.razor.cs` code-behind. Inject `IStringLocalizer` and use " +
            "`@Localizer[\"Key\"]`, which this suite's markup gate now accepts as the idiom it is.");
    }

    /// <summary>
    /// Uppercase is applied only inside a locale-scoped rule. UX-DR42.
    /// </summary>
    /// <remarks>
    /// <c>text-transform: uppercase</c> is <b>lossy</b> in several cased scripts - Turkish and
    /// Azeri dotless i becomes I and changes the word; Greek strips accents and alters final
    /// sigma - and meaningless in the case-less ones, where letter-spacing additionally severs the
    /// joins of connected scripts. The treatment therefore has to sit under a locale-aware
    /// <c>:lang()</c> scope that excludes them.
    /// <para>
    /// This gate is what makes that exclusion survive contact with epic 2. Every component that
    /// wants an uppercase head will be tempted to write its own <c>text-transform</c>, and one
    /// unscoped rule silently un-does the exclusion for every locale.
    /// </para>
    /// </remarks>
    [Fact]
    public void Uppercase_is_applied_only_inside_a_locale_scoped_rule()
    {
        var problems = new List<string>();

        foreach (var (sheet, rule, declaration) in CssCorpus.AllDeclarations())
        {
            if (!declaration.Property.Equals("text-transform", StringComparison.OrdinalIgnoreCase)
                || IsNoneValue(declaration.Value))
            {
                continue;
            }

            // WHICH locales are excluded, not merely that some `:lang()` appears. The substring
            // test passed `.x:lang(tr), .y { text-transform: uppercase }` - one half uppercasing
            // Turkish, the exact lossy case the exclusion exists for, and the other half scoped to
            // nothing at all. A gate that checks only for the presence of a locale scope does not
            // verify the exclusion it claims to make survive epic 2.
            var missing = LossyCasingLocales
                .Where(locale => !ExcludesLocale(rule.Selector, locale))
                .ToList();

            if (missing.Count == 0)
            {
                continue;
            }

            problems.Add(
                $"{Where(sheet, declaration)} declares '{declaration.Property}: " +
                $"{declaration.Value}' on '{rule.Selector}', which does not exclude " +
                $"{string.Join(", ", missing.Select(l => $":lang({l})"))}.");
        }

        AssertNoProblems(problems,
            "A casing transform is applied without excluding the locales it is lossy in.",
            "Scope it under `:lang()` and exclude Turkish, Azeri and Greek, where uppercasing is " +
            "lossy, along with the case-less scripts. See the casing rules in base.css - the " +
            "exclusion list is written once so it cannot drift between roles.");
    }

    /// <summary>
    /// Reduced motion neutralises every transition and animation, structurally. AC12.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Enforced by construction rather than by enumeration: a universal <c>!important</c> reset
    /// inside the <c>prefers-reduced-motion: reduce</c> block means no transition or animation can
    /// escape by being declared later, more specifically, or in a file written after it. A gate
    /// that instead listed the transitions it knew about would go stale on the first component
    /// that added one.
    /// </para>
    /// <para>
    /// The second half closes the only remaining escape: an <c>!important</c> transition declared
    /// OUTSIDE the block would tie the reset on specificity and win on source order.
    /// </para>
    /// <para>
    /// Honouring the preference costs no information, because nothing in Yello conveys state by
    /// motion alone - motion only ever reports a change that is also conveyed structurally.
    /// </para>
    /// </remarks>
    [Fact]
    public void Reduced_motion_neutralises_every_transition_and_animation()
    {
        var problems = new List<string>();

        var resetRules = CssCorpus.AllRules()
            .Where(p => IsUnconditionalReducedMotionContext(p.Rule.AtRulePath))
            .Where(p => IsUniversalSelector(p.Rule.Selector))
            .ToList();

        foreach (var property in new[] { "transition", "animation" })
        {
            if (!resetRules.Exists(p => p.Rule.Declarations.Any(d =>
                    d.Property.Equals(property, StringComparison.OrdinalIgnoreCase)
                    && IsNoneValue(d.Value)
                    && d.IsImportant)))
            {
                problems.Add(
                    "No universal rule inside a 'prefers-reduced-motion: reduce' block sets " +
                    $"'{property}: none !important'. Without the universal !important reset, a " +
                    "component declared later or more specifically keeps animating.");
            }
        }

        foreach (var (sheet, rule, declaration) in CssCorpus.AllDeclarations())
        {
            if (!declaration.IsImportant || !IsMotionProperty(declaration.Property))
            {
                continue;
            }

            // The exemption is for the universal RESET, not for the whole block. Exempting every
            // rule whose at-rule path mentioned reduced motion let the preference be defeated from
            // INSIDE the block it is declared in: a planted
            // `@media (prefers-reduced-motion: reduce) { .x { transition: … !important } }`
            // reported green, and a class selector with !important beats `*, *::before, *::after`
            // on specificity and then on source order - so the transition genuinely ran for a
            // reduced-motion user while the gate written to prevent exactly that passed.
            if (IsReducedMotionContext(rule.AtRulePath) && IsUniversalSelector(rule.Selector))
            {
                continue;
            }

            var where = IsReducedMotionContext(rule.AtRulePath)
                ? "inside the reduced-motion block but on a selector narrower than the universal " +
                  "reset, so it outranks the reset on specificity"
                : "outside the reduced-motion block, so it outranks the reset on source order";

            problems.Add(
                $"{Where(sheet, declaration)} declares '{declaration.Property}' !important " +
                $"{where}, on '{rule.Selector}'.");
        }

        AssertNoProblems(problems,
            "The reduced-motion contract is not enforced.",
            "A `@media (prefers-reduced-motion: reduce)` block must neutralise transitions and " +
            "animations universally with !important, and nothing outside it may use !important on " +
            "a motion property.");
    }

    /// <summary>
    /// No physical <c>left</c>/<c>right</c> property is used where a logical one exists. UX-DR42.
    /// </summary>
    /// <remarks>
    /// UX-DR42 makes RTL tolerance structural, and the base layer is where that is decided for
    /// every later story. <c>inline-start</c>/<c>inline-end</c>, <c>padding-inline</c>,
    /// <c>margin-block</c>, <c>inset-block-end</c>, <c>border-block-start</c> - never
    /// <c>left</c>/<c>right</c>. Values carrying a direction are covered too:
    /// <c>text-align: left</c> is <c>start</c>, and <c>float: right</c> is <c>inline-end</c>.
    /// </remarks>
    [Fact]
    public void No_physical_left_or_right_property_is_used_where_a_logical_one_exists()
    {
        var problems = new List<string>();

        foreach (var (sheet, rule, declaration) in CssCorpus.AllDeclarations())
        {
            if (PhysicalProperties.Contains(declaration.Property))
            {
                problems.Add(
                    $"{Where(sheet, declaration)} uses the physical property " +
                    $"'{declaration.Property}' on '{rule.Selector}'.");
            }

            if (PhysicallyValuedProperties.Contains(declaration.Property)
                && PhysicalDirectionValuePattern.IsMatch(declaration.Value))
            {
                problems.Add(
                    $"{Where(sheet, declaration)} sets '{declaration.Property}' to the physical " +
                    $"value '{declaration.Value}' on '{rule.Selector}'.");
            }

            if (PhysicalShorthandProperties.Contains(declaration.Property)
                && ValueComponentCount(declaration.Value) >= 3)
            {
                problems.Add(
                    $"{Where(sheet, declaration)} uses the physical 4-value form of " +
                    $"'{declaration.Property}' ('{declaration.Value}') on '{rule.Selector}'. The " +
                    "3- and 4-value order is top-right-bottom-left, which does not flip under " +
                    "RTL.");
            }
        }

        AssertNoProblems(problems,
            "A physical left/right property or value is used.",
            "Use the logical equivalent: padding-inline-start, margin-inline-end, " +
            "inset-inline-start, border-inline-start-width, text-align: start, float: inline-end. " +
            "RTL tolerance is structural here, and this layer decides it for every later story.");
    }

    /// <summary>
    /// No box is given a fixed height. The statically-detectable half of AC13.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Chips and cards size to content with no fixed heights, so a WCAG 1.4.12 text-spacing
    /// override - line-height 1.5x, letter-spacing 0.12x, word-spacing 0.16x, paragraph spacing 2x
    /// - grows the box instead of clipping the glyphs. Combined with <c>rem</c> internal padding,
    /// that is AC13's constructive half.
    /// </para>
    /// <para>
    /// <b>AC13's measurement half is not gated and is not claimed to be.</b> Verifying 1.4.12 and
    /// 200% text-only zoom needs a rendered surface and a browser, and both are blocked on blocker
    /// B5 - the browser-test binding, undecided until the <c>bmad-testarch-framework</c> run. It
    /// is scenario X-11, recorded in <c>deferred-work.md</c> with its owner. This gate covers what
    /// static analysis genuinely can: a fixed height cannot grow, so it is refused.
    /// </para>
    /// <para>
    /// <c>min-height</c> is untouched - it is the target floor, and a minimum lets a box grow.
    /// <c>max-height</c> is deliberately not gated: a scroll container may legitimately need one,
    /// and it should be a viewport unit or a percentage rather than a fixed length. That is a
    /// judgement about a component, and there are none yet.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_box_is_given_a_fixed_height()
    {
        var problems = new List<string>();

        foreach (var (sheet, rule, declaration) in CssCorpus.AllDeclarations())
        {
            if (!declaration.Property.Equals("height", StringComparison.OrdinalIgnoreCase)
                && !declaration.Property.Equals("block-size", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!FixedLengthPattern.IsMatch(CssCorpus.Resolve(declaration.Value)))
            {
                continue;
            }

            problems.Add(
                $"{Where(sheet, declaration)} fixes '{declaration.Property}' to " +
                $"'{declaration.Value}' on '{rule.Selector}'.");
        }

        AssertNoProblems(problems,
            "A box is given a fixed height.",
            "Chips and cards size to content. A fixed height clips the glyphs under a WCAG 1.4.12 " +
            "text-spacing override and under 200% text-only zoom, which is the accessibility " +
            "setting the release gate exercises. Use min-height, or let the content size the box.");
    }

    /// <summary>
    /// The non-colour token families are complete: every name exists, and every type role binds
    /// all five axes. AC2, AC3, AC9.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AC2's exact-count discipline was applied to one family out of six. The 30 colour names were
    /// gated in both directions; the type scale, the spacing scale, the radii, the border widths
    /// and the motion tokens were gated by nothing at all - so deleting
    /// <c>--type-meta-letter-spacing</c>, or dropping <c>--motion-easing-exit</c>, left every
    /// assertion in the suite green. A deleted type axis is worse than a wrong one, because
    /// <c>base.css</c> binds all five on all eight roles: the declaration silently resolves to
    /// nothing and the role inherits whatever an ancestor had, which is exactly the accidental
    /// inheritance the five-axis binding exists to prevent.
    /// </para>
    /// <para>
    /// <b>Names and axes only - values are deliberately NOT asserted.</b> That is Lee's decision of
    /// 2026-08-27, taken at code review: a value gate needs a second source to compare against, and
    /// both candidates were rejected - parsing <c>DESIGN.md</c> would make a release gate depend on
    /// a planning artifact, and restating the values in C# would only move the transcription. The
    /// consequence is accepted and recorded in the story: a MISTYPED value is caught only if it
    /// crosses a contrast threshold, while a MISSING or renamed one is caught here.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_non_colour_token_families_are_complete()
    {
        var problems = new List<string>();

        problems.AddRange(
            from role in TypeRoles
            from axis in TypeAxes
            let name = $"type-{role}-{axis}"
            where !CssCorpus.TokenValues.ContainsKey(name)
            select $"The token layer declares no '--{name}'. Every role binds all five axes so " +
                   "none can be inherited by accident from an ancestor.");

        problems.AddRange(RequiredStructuralTokens
            .Where(name => !CssCorpus.TokenValues.ContainsKey(name))
            .Select(name => $"The token layer declares no '--{name}'."));

        AssertNoProblems(problems,
            "A non-colour token is missing from the token layer.",
            "The token layer declares the design's whole vocabulary: 8 type roles x 5 axes, the " +
            "spacing scale, the target floor, the radii, the border widths and the motion " +
            "timings. A missing token resolves to nothing and the property silently falls back to " +
            "an inherited value.");
    }

    /// <summary>
    /// Every stylesheet in the corpus is actually linked by the host page. AC1, AC2, AC3.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Without this, the entire story is ungated.</b> Every other assertion in this class and in
    /// <see cref="ColorTokenContrastTests"/> reads the CSS files and says nothing about whether a
    /// browser ever receives them. Deleting the two <c>&lt;link&gt;</c> elements from
    /// <c>index.html</c> left all of them green while no token reached a page: no focus ring, no
    /// target floor, no reduced-motion reset, no locale-aware casing, no palette.
    /// </para>
    /// <para>
    /// This is not a hypothetical. Story 1.1 deliberately removed the template's stylesheet link,
    /// and <c>index.html</c>'s own comment warns about restoring one - so a removed link is the
    /// single most likely edit in this file's history. The Debug Log verified the links resolved
    /// once, by hand, in a story whose whole thesis is that hand verification does not survive
    /// later stories.
    /// </para>
    /// <para>
    /// Derived from the corpus rather than from a hardcoded pair of filenames: a stylesheet added
    /// by a later story is held to the same requirement without anyone remembering to extend this
    /// gate. The link is matched on the fingerprint placeholder form the build rewrites, because
    /// that is the form the file must carry for <c>OverrideHtmlAssetPlaceholders</c> to produce a
    /// cache-busting URL.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_stylesheet_is_linked_by_the_host_page()
    {
        var problems = new List<string>();

        var hosts = CssCorpus.MarkupFiles
            .Where(m => m.File.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (hosts.Count == 0)
        {
            problems.Add(
                "No 'index.html' was found in the source tree, so nothing links the stylesheets " +
                "and no gate in this suite describes what a browser actually loads.");
        }

        foreach (var host in hosts)
        {
            foreach (var sheet in CssCorpus.StyleSheets)
            {
                var stem = Path.GetFileNameWithoutExtension(sheet.File.Name);
                var expected = $"css/{stem}#[.{{fingerprint}}].css";

                if (!host.Blanked.Contains(expected, StringComparison.Ordinal))
                {
                    problems.Add(
                        $"{host.Path} does not link '{sheet.Path}'. Expected a stylesheet link to " +
                        $"'{expected}'.");
                }
            }
        }

        AssertNoProblems(problems,
            "A stylesheet in the corpus is not linked by the host page.",
            "Every *.css file this suite asserts on has to reach the browser, or the assertion " +
            "describes a file with no effect. Link it from index.html through the " +
            "`#[.{fingerprint}]` placeholder the build rewrites.");
    }

    /// <summary>
    /// Every <c>var()</c> reference in the corpus resolves to a declared custom property.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the gate that stops every length gate in this class from passing vacuously.
    /// <c>CssCorpus.Resolve</c> substitutes <c>var()</c> references so a width gate measures what
    /// the browser renders rather than the text an author typed - but an unresolvable reference
    /// leaves <c>var(--typo)</c> as literal text, and text carries no number, so the hairline
    /// floor, the target floor, the radius scale and the fixed-height ban all find nothing to
    /// measure and report green.
    /// </para>
    /// <para>
    /// Resolution gave up silently by design - it is bounded at eight passes so a token cycle
    /// cannot hang the suite, and returning partially-substituted text was preferred to never
    /// returning. That reasoning is right and this is the third option it did not take: let
    /// resolution return, and assert separately that it finished.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_token_reference_resolves_to_a_declared_property()
    {
        var problems = new List<string>();

        foreach (var (sheet, rule, declaration) in CssCorpus.AllDeclarations())
        {
            problems.AddRange(CssCorpus.UnresolvedReferences(declaration.Value)
                .Distinct(StringComparer.Ordinal)
                .Select(name =>
                    $"{Where(sheet, declaration)} references '--{name}' in " +
                    $"'{declaration.Property}' on '{rule.Selector}', which no stylesheet " +
                    "declares."));
        }

        AssertNoProblems(problems,
            "A var() reference does not resolve to any declared custom property.",
            "An unresolved reference is worse than a wrong value: it leaves text where a length " +
            "should be, and every gate that measures a length then finds no number and passes. " +
            "Declare the property in tokens.css, or correct the name.");
    }

    /// <summary>
    /// The hairline is snapped to a device pixel at every common display scale. AC7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>DESIGN.md:366</c> requires structural borders "snapped to device pixels where the
    /// platform allows" in the same sentence as the 1.5px floor. The floor was implemented and
    /// gated; the snapping half was neither implemented, gated, nor recorded as deferred, so half
    /// a <c>status: final</c> requirement was simply silent.
    /// </para>
    /// <para>
    /// The snapping is expressed as <c>resolution</c> media queries rebinding
    /// <c>--border-hairline-width</c> to <c>ceil(1.5 x dppx) / dppx</c>, so the rendered edge is
    /// always a whole number of device pixels and never below the 1.5px floor. This gate holds
    /// three things: the block exists, every rebinding clears the floor, and every rebinding
    /// actually lands on an integer device pixel at the scale it serves - the last being the point,
    /// since a rebinding that cleared the floor but sat mid-pixel would antialias exactly as the
    /// unsnapped 1.5px does.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_hairline_is_snapped_to_a_device_pixel_at_every_declared_scale()
    {
        var problems = new List<string>();

        var snapped = CssCorpus.Tokens.Rules
            .Where(r => ResolutionConditionPattern.IsMatch(r.AtRulePath))
            .Select(r => (Rule: r, Scale: DeclaredScale(r.AtRulePath)))
            .ToList();

        if (snapped.Count == 0)
        {
            problems.Add(
                "No 'resolution' media query rebinds '--border-hairline-width'. AC7 requires " +
                "structural borders snapped to device pixels where the platform allows, and at " +
                "1.5px the edge lands mid-pixel at every scale except 2dppx.");
        }

        foreach (var (rule, scale) in snapped)
        {
            var where = $"{CssCorpus.Tokens.Path}:{CssCorpus.LineAt(CssCorpus.Tokens.Raw, rule.Offset)}";
            var width = rule.Declarations
                .Where(d => d.Property.Equals("--border-hairline-width", StringComparison.Ordinal))
                .Select(d => CssCorpus.AbsoluteLengthsPx(d.Value).FirstOrDefault(double.NaN))
                .FirstOrDefault(double.NaN);

            if (double.IsNaN(width))
            {
                problems.Add($"{where} ('{rule.AtRulePath}') declares no '--border-hairline-width'.");
                continue;
            }

            if (width < HairlineFloorPx - LengthTolerance)
            {
                problems.Add(
                    $"{where} ('{rule.AtRulePath}') snaps the hairline to {width}px, below the " +
                    $"{HairlineFloorPx}px floor. Snapping may only ever round UP.");
            }

            if (scale is { } dppx)
            {
                var devicePixels = width * dppx;

                if (Math.Abs(devicePixels - Math.Round(devicePixels, MidpointRounding.AwayFromZero)) > SnapTolerance)
                {
                    problems.Add(
                        $"{where} ('{rule.AtRulePath}') snaps the hairline to {width}px, which is " +
                        $"{devicePixels} device pixels at {dppx}dppx rather than a whole number - " +
                        "so it antialiases exactly as the unsnapped width does.");
                }
            }
        }

        AssertNoProblems(problems,
            "The hairline is not snapped to a device pixel at every declared scale.",
            "Each 'resolution' branch sets --border-hairline-width to ceil(1.5 * dppx) / dppx, " +
            "which is at or above the 1.5px floor AND a whole number of device pixels at that " +
            "scale. Rounding down is never correct: it breaks the accessibility floor to fix " +
            "antialiasing.");
    }

    private static void AssertNoProblems(List<string> problems, string headline, string remedy) =>
        Assert.True(problems.Count == 0,
            $"{headline}{Environment.NewLine}{Environment.NewLine}" +
            string.Join(Environment.NewLine, problems.Select(p => $"  - {p}")) +
            $"{Environment.NewLine}{Environment.NewLine}{remedy}");

    private static string Where(CssCorpus.Sheet sheet, CssCorpus.Declaration declaration) =>
        $"{sheet.Path}:{CssCorpus.LineAt(sheet.Raw, declaration.Offset)}";

    /// <summary>
    /// The 15 semantic names, taken from the token layer rather than restated here.
    /// </summary>
    /// <remarks>
    /// A colour token whose name does not end in <c>-light</c> is a semantic name. Deriving them
    /// keeps this class from carrying a second copy of the list that
    /// <see cref="ColorTokenContrastTests"/> already gates at exactly 30 - two copies is how the
    /// boundary gate comes to check fourteen names while the count gate checks fifteen.
    /// </remarks>
    private static IReadOnlyList<string> SemanticNamesFromTokenLayer() =>
    [
        .. CssCorpus.Tokens.Rules
            .Where(r => !CssCorpus.IsInsideThemeBoundary(r.Offset))
            .SelectMany(r => r.Declarations)
            .Where(CssCorpus.IsCustomProperty)
            .Where(d => WcagContrast.IsHexColour(d.Value))
            .Select(d => d.Property[2..])
            .Where(n => !n.EndsWith("-light", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal),
    ];

    private static IEnumerable<string> DescribeFocusTreatment(CssCorpus.Sheet sheet, CssCorpus.Rule rule)
    {
        var where = $"{sheet.Path}:{CssCorpus.LineAt(sheet.Raw, rule.Offset)}";

        // LAST, not first. Within one rule the later declaration wins, so a rule that draws a
        // compliant ring and then overrides it - `outline: 2px solid var(--focus-ring)` followed by
        // `outline-width: 0` - renders no ring at all while a first-declaration reading reported
        // the compliant one and passed.
        var outline = rule.Declarations.LastOrDefault(IsRingDeclaration);

        if (outline is null)
        {
            // Unreachable while the caller filters on IsRingDeclaration. Kept so a later widening
            // of that filter cannot turn this into a NullReferenceException inside a release gate.
            yield break;
        }

        if (!CssCorpus.ReferencedTokens(outline.Value).Contains("focus-ring", StringComparer.Ordinal)
            && !outline.Property.Equals("outline-width", StringComparison.OrdinalIgnoreCase))
        {
            yield return
                $"{where} ('{rule.Selector}') draws the ring without var(--focus-ring). The ring " +
                "has its own token so it can be tuned independently of the accent.";
        }

        var widths = StatedLengthsPx(outline.Value).ToList();

        if (widths.Count == 0 || widths.TrueForAll(w => w < FocusRingFloorPx - LengthTolerance))
        {
            yield return
                $"{where} ('{rule.Selector}') draws the ring at {DescribeWidths(widths)} rather " +
                $"than at least {FocusRingFloorPx}px.";
        }

        var offset = rule.Declarations.LastOrDefault(d =>
            d.Property.Equals("outline-offset", StringComparison.OrdinalIgnoreCase));

        if (offset is null)
        {
            yield return
                $"{where} ('{rule.Selector}') sets no 'outline-offset'. The default is 0, which " +
                "draws the ring on the control - and at 1.45 against the accent it vanishes " +
                "there. The offset is what makes the ring visible.";

            yield break;
        }

        // The offset is MEASURED here, not merely required to be present. Presence alone was
        // satisfied by `outline-offset: 0`, which is the exact value AC6 forbids.
        var offsets = StatedLengthsPx(offset.Value).ToList();

        if (offsets.Count == 0 || offsets.TrueForAll(o => o < FocusRingFloorPx - LengthTolerance))
        {
            yield return
                $"{where} ('{rule.Selector}') sets 'outline-offset' to " +
                $"'{offset.Value}' rather than at least {FocusRingFloorPx}px. At 0 the ring is " +
                "drawn on the control, where it sits at 1.45 against the accent and vanishes.";
        }
    }

    private static string DescribeWidths(IReadOnlyList<double> widths) =>
        widths.Count == 0 ? "no stated width" : $"{widths[0]}px";

    /// <summary>
    /// True when a declaration sizes or spaces type, whether as a CSS property or as one of the
    /// type scale's own custom properties.
    /// </summary>
    private static bool IsTypographicDeclaration(CssCorpus.Declaration declaration) =>
        TypographicProperties.Contains(declaration.Property)
        || (CssCorpus.IsCustomProperty(declaration)
            && Array.Exists(TypographicTokenSuffixes,
                suffix => declaration.Property.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)));

    private static bool IsLineHeightDeclaration(string property) =>
        property.Equals("line-height", StringComparison.OrdinalIgnoreCase)
        || property.EndsWith("-line-height", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A value that is a bare number, which is how a line-height ratio is written, or null when it
    /// is anything else - <c>normal</c>, a length, a percentage.
    /// </summary>
    private static double? UnitlessRatio(string value) =>
        double.TryParse(
            value.Trim(),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;

    /// <summary>
    /// Lengths in an absolute unit other than px, exactly as written.
    /// </summary>
    private static IEnumerable<string> AbsoluteNonPixelLengths(string value) =>
        from match in AbsoluteNonPixelPattern.Matches(CssCorpus.Resolve(value)).Cast<Match>()
        select match.Value;

    /// <summary>
    /// True when a selector excludes a locale, by naming it inside a negation.
    /// </summary>
    /// <remarks>
    /// Requires both the negation and the locale, so naming a locale INSIDE the scope - which is
    /// the opposite of excluding it - does not satisfy the check.
    /// </remarks>
    private static bool ExcludesLocale(string selector, string locale)
    {
        var negation = selector.IndexOf(":not(", StringComparison.OrdinalIgnoreCase);

        return negation >= 0
            && selector[negation..].Contains($":lang({locale})", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The display scale in dppx a resolution media query serves, taken from its lower bound.
    /// </summary>
    /// <remarks>
    /// A band's lower bound is the scale that has to snap: within
    /// <c>[1.25dppx, 1.5dppx)</c> the worst case for a width chosen at 1.25 is 1.25 itself. A band
    /// with only an upper bound is the 1dppx floor.
    /// </remarks>
    private static double? DeclaredScale(string atRulePath)
    {
        var minimum = MinResolutionPattern.Match(atRulePath);

        if (minimum.Success)
        {
            return double.Parse(
                minimum.Groups[1].Value,
                System.Globalization.CultureInfo.InvariantCulture);
        }

        return MaxResolutionPattern.IsMatch(atRulePath) ? 1 : null;
    }

    private static bool IsRootSelector(string selector) =>
        selector.Equals("html", StringComparison.OrdinalIgnoreCase)
        || selector.Equals(":root", StringComparison.OrdinalIgnoreCase);

    private static bool IsPixelPermitted(string property) =>
        Array.Exists(PixelPermittedPropertyFragments,
            fragment => property.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static bool IsBorderWidthProperty(string property)
    {
        if (!property.StartsWith("border", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var excluded in new[] { "radius", "image", "collapse", "spacing", "color", "style" })
        {
            if (property.Contains(excluded, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// True when a declaration draws a structural edge that the hairline floor governs.
    /// </summary>
    /// <remarks>
    /// An <c>outline</c> on a non-focus selector is a structural edge by another name.
    /// <c>.card.selected { outline: 1px solid var(--border-hairline) }</c> was a 1px edge no gate
    /// measured - <c>IsBorderWidthProperty</c> requires the property to start with <c>border</c>,
    /// and the px-confinement gate exempts everything containing <c>outline</c> - while the
    /// identical <c>border-inline-start: 1px</c> failed. Focus selectors are excluded because the
    /// ring is held to its own, higher floor by the focus gates.
    /// </remarks>
    private static bool IsStructuralEdgeProperty(string property, string selector) =>
        IsBorderWidthProperty(property)
        || (IsRingWidthProperty(property)
            && !selector.Contains(":focus", StringComparison.OrdinalIgnoreCase));

    private static bool IsRingWidthProperty(string property) =>
        property.Equals("outline", StringComparison.OrdinalIgnoreCase)
        || property.Equals("outline-width", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when a declaration actually draws the ring, rather than merely positioning it.
    /// </summary>
    private static bool IsRingDeclaration(CssCorpus.Declaration declaration) =>
        declaration.Property.Equals("outline", StringComparison.OrdinalIgnoreCase)
        || declaration.Property.Equals("outline-width", StringComparison.OrdinalIgnoreCase);

    private static bool IsMinimumHeightProperty(string property) =>
        property.Equals("min-height", StringComparison.OrdinalIgnoreCase)
        || property.Equals("min-block-size", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Minimum-size properties on either axis. WCAG 2.2 AA's 2.5.8 is 24<b>x</b>24.
    /// </summary>
    private static bool IsMinimumSizeProperty(string property) =>
        IsMinimumHeightProperty(property)
        || property.Equals("min-width", StringComparison.OrdinalIgnoreCase)
        || property.Equals("min-inline-size", StringComparison.OrdinalIgnoreCase);

    private static bool IsTextDecorationProperty(string property) =>
        property.Equals("text-decoration", StringComparison.OrdinalIgnoreCase)
        || property.Equals("text-decoration-line", StringComparison.OrdinalIgnoreCase);

    private static bool IsMotionProperty(string property) =>
        property.StartsWith("transition", StringComparison.OrdinalIgnoreCase)
        || property.StartsWith("animation", StringComparison.OrdinalIgnoreCase);

    private static bool IsReducedMotionContext(string atRulePath) =>
        atRulePath.Contains("prefers-reduced-motion", StringComparison.OrdinalIgnoreCase)
        && atRulePath.Contains("reduce", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when the reduced-motion context is unconditional, so the reset inside it always
    /// applies.
    /// </summary>
    /// <remarks>
    /// A block written <c>@media (prefers-reduced-motion: reduce) and (min-width: 40em)</c>
    /// satisfied the substring test above while applying only on wide viewports - so the reset
    /// counted as present and the preference went unhonoured everywhere else.
    /// </remarks>
    private static bool IsUnconditionalReducedMotionContext(string atRulePath) =>
        IsReducedMotionContext(atRulePath)
        && !atRulePath.Contains(" and ", StringComparison.OrdinalIgnoreCase)
        && atRulePath.Count(c => c == '@') == 1;

    /// <summary>
    /// True when a selector is the universal one, which is what the reduced-motion reset must be
    /// written on so nothing can outrank it on specificity.
    /// </summary>
    private static bool IsUniversalSelector(string selector)
    {
        var parts = selector.Split(
            ',',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        // An empty selector is not universal. At-rule contexts carry one, and treating it as `*`
        // would make every declaration inside them answer for the universal reset's rules.
        return parts.Length > 0
            && Array.TrueForAll(parts, part => part is "*" || part.StartsWith("*:", StringComparison.Ordinal));
    }

    /// <summary>
    /// Every length a value states, in px, counting a unitless zero as the zero it is.
    /// </summary>
    /// <remarks>
    /// <c>outline-offset: 0</c>, <c>outline-width: 0</c> and <c>min-height: 0</c> are all lengths
    /// the browser honours, and a unit-suffixed reading saw no number in any of them - so the
    /// floors they breach were never compared. This is the reading a floor check needs: units
    /// converted where a fixed factor exists, and a bare zero treated as zero rather than as
    /// silence.
    /// </remarks>
    private static IEnumerable<double> StatedLengthsPx(string value)
    {
        var resolved = CssCorpus.Resolve(value);
        var lengths = CssCorpus.AbsoluteLengthsPx(value).ToList();

        if (lengths.Count > 0)
        {
            return lengths;
        }

        return UnitlessZeroPattern.IsMatch(resolved) ? [0d] : [];
    }

    /// <summary>
    /// True when a value is the absence of the thing rather than a setting of it.
    /// </summary>
    private static bool IsNoneValue(string value) => AbsenceValues.Contains(value.Trim());

    /// <summary>
    /// True when an outline declaration takes the ring away.
    /// </summary>
    private static bool RemovesOutline(CssCorpus.Declaration declaration)
    {
        if (declaration.Property.Equals("outline-offset", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // `transparent` is a removal by another route: the ring is still drawn, still measured by
        // every width check, and completely invisible. On `outline-color` it was previously exempt
        // outright, so it was the one way to delete the focus ring that no gate objected to.
        if (CssCorpus.Resolve(declaration.Value)
            .Contains("transparent", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (declaration.Property.Equals("outline-color", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var resolved = CssCorpus.Resolve(declaration.Value).Trim();

        if (IsNoneValue(resolved) || resolved.Equals("hidden", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // The shorthand carries a style, so `outline: 0 none red` is a removal even though the
        // whole value is not the word "none".
        var tokens = resolved.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return declaration.Property.Equals("outline", StringComparison.OrdinalIgnoreCase)
            && Array.Exists(tokens, t =>
                t.Equals("none", StringComparison.OrdinalIgnoreCase)
                || t.Equals("hidden", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// True when a selector targets a link, so removing its underline is a defect.
    /// </summary>
    /// <remarks>
    /// <c>Contains("link")</c> was both too loose and too tight. Too loose: <c>.blinking</c>
    /// contains "link" and was treated as a link. Too tight: <c>*</c>, <c>:is(a, button)</c> and
    /// <c>:where(a)</c> all remove an underline from every link in the product and matched none of
    /// the old conditions, so the AC10 removal ban never fired on the selectors most likely to
    /// carry a global reset.
    /// </remarks>
    private static bool SelectorTargetsLink(string selector) =>
        IsUniversalSelector(selector)
        || LinkClassSelectorPattern.IsMatch(selector)
        || AnchorElementSelectorPattern.IsMatch(selector);

    /// <summary>
    /// How many top-level components a shorthand value has, counting a parenthesised run such as
    /// <c>var(--x)</c> or <c>calc(1px + 2px)</c> as one.
    /// </summary>
    private static int ValueComponentCount(string value)
    {
        var components = 0;
        var depth = 0;
        var inComponent = false;

        foreach (var current in value.Trim())
        {
            if (current == '(')
            {
                depth++;
            }
            else if (current == ')')
            {
                depth = Math.Max(depth - 1, 0);
            }
            else
            {
                // Neither bracket; the depth is unchanged and the whitespace test below decides.
            }

            if (depth == 0 && char.IsWhiteSpace(current))
            {
                inComponent = false;
                continue;
            }

            if (!inComponent)
            {
                inComponent = true;
                components++;
            }
        }

        return components;
    }

    /// <summary>
    /// px lengths exactly as written, before token substitution.
    /// </summary>
    private static IEnumerable<double> RawPixelLengths(string value) =>
        from match in RawPixelPattern.Matches(value).Cast<Match>()
        select double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Lengths in a value expressed in something other than px, so a radius given as a percentage
    /// or an em is reported rather than skipped.
    /// </summary>
    private static IEnumerable<string> NonPixelLengths(string value) =>
        from match in NonPixelLengthPattern.Matches(value).Cast<Match>()
        select match.Value;

    /// <summary>
    /// Razor source with everything that is not rendered markup blanked out.
    /// </summary>
    /// <remarks>
    /// Directives first, then <c>@code</c>, <c>@functions</c> and <c>@{}</c> blocks by brace
    /// matching. Without this, <c>_Imports.razor</c> - which is nothing but <c>@using</c> lines
    /// and contains no tag at all - presents as one enormous text node full of letters and fails
    /// the gate on every build.
    /// </remarks>
    private static string BlankRazorNonMarkup(string source)
    {
        var blanked = RazorDirectivePattern.Replace(source, m => new string(' ', m.Length));

        // Control-flow HEADERS only - the keyword and its condition - never the block body. The
        // body of an `@if` or `@foreach` is rendered markup and must stay scannable, so
        // brace-matched blanking would be a false negative for every literal inside a conditional.
        // Leaving the header in place was a false positive instead: `@if (ShowRevoked)` matched
        // `@if` alone, because the expression pattern's optional parenthesis group needs `(`
        // immediately after the identifier and there is a space - so ` (ShowRevoked)` survived and
        // was reported as user-visible copy. The braces themselves are harmless: `{` and `}` carry
        // no letters, so the word scan ignores them.
        blanked = RazorControlFlowPattern.Replace(blanked, m => new string(' ', m.Length));

        // `<script>` and `<style>` bodies are not copy. Left in place, a component carrying either
        // had its identifiers reported as literal text and failed the gate for being written
        // correctly. (`<style>` bodies are still read as CSS - by CssCorpus.MarkupStyleSheets - so
        // blanking them here loses no coverage.)
        blanked = NonCopyElementPattern.Replace(blanked, m => new string(' ', m.Length));

        foreach (var opener in new[] { "@code", "@functions", "@{" })
        {
            blanked = BlankBracedBlocks(blanked, opener);
        }

        return blanked;
    }

    /// <summary>
    /// The inverse of <see cref="BlankRazorNonMarkup"/>: only the C# inside <c>@code</c> and
    /// <c>@functions</c> blocks, everything else blanked, offsets preserved.
    /// </summary>
    private static string RazorCodeBlocksOnly(string source)
    {
        var characters = BlankedCanvas(source);

        foreach (var opener in new[] { "@code", "@functions" })
        {
            CopyBracedBlocks(source, characters, opener);
        }

        return new string(characters);
    }

    /// <summary>
    /// A buffer the same length as the source, all spaces, with newlines kept so reported line
    /// numbers still mean something.
    /// </summary>
    private static char[] BlankedCanvas(string source)
    {
        var characters = new char[source.Length];

        for (var index = 0; index < source.Length; index++)
        {
            characters[index] = source[index] is '\r' or '\n' ? source[index] : ' ';
        }

        return characters;
    }

    /// <summary>
    /// Copies every brace-balanced block introduced by an opener from the source into the target,
    /// at the same offsets.
    /// </summary>
    private static void CopyBracedBlocks(string source, char[] target, string opener)
    {
        var index = source.IndexOf(opener, StringComparison.Ordinal);

        while (index >= 0)
        {
            var brace = source.IndexOf('{', index);
            var end = brace < 0 ? source.Length - 1 : MatchingBrace(source, brace);

            for (var position = index; position <= end && position < source.Length; position++)
            {
                target[position] = source[position];
            }

            index = end + 1 < source.Length
                ? source.IndexOf(opener, end + 1, StringComparison.Ordinal)
                : -1;
        }
    }

    /// <summary>
    /// C# string literals that read as a sentence of copy rather than as a machine-facing value.
    /// </summary>
    private static IEnumerable<(int Offset, string Text)> SentenceLiterals(string source) =>
        from match in CSharpStringLiteralPattern.Matches(source).Cast<Match>()
        let text = match.Groups[1].Value
        where IsSentenceLike(text)
        select (match.Index, text);

    /// <summary>
    /// True when a string literal looks like copy a translator would have to translate.
    /// </summary>
    private static bool IsSentenceLike(string text)
    {
        if (text.IndexOfAny(['<', '>', '{', '}', '=', ';', '/', '\\', '_', '#', '|']) >= 0)
        {
            return false;
        }

        var words = WordPattern.Matches(text).Select(m => m.Value).ToList();

        if (words.Count < 2 || words.TrueForAll(w => w.All(char.IsUpper)))
        {
            return false;
        }

        var firstLetter = text.FirstOrDefault(char.IsLetter);

        return char.IsUpper(firstLetter)
            && !words.TrueForAll(w => GlossaryProperNouns.Contains(w));
    }

    /// <summary>
    /// Blanks every brace-balanced block introduced by a given opener.
    /// </summary>
    private static string BlankBracedBlocks(string source, string opener)
    {
        var characters = source.ToCharArray();
        var index = source.IndexOf(opener, StringComparison.Ordinal);

        while (index >= 0)
        {
            var brace = source.IndexOf('{', index);

            // An opener with no brace after it is malformed Razor. Blanking to the end of the file
            // is the conservative reading: it cannot leave code being scanned as markup, and the
            // compiler will reject the file long before this gate reports on it.
            var end = brace < 0 ? source.Length - 1 : MatchingBrace(source, brace);

            BlankRange(characters, index, end);

            index = end + 1 < source.Length
                ? source.IndexOf(opener, end + 1, StringComparison.Ordinal)
                : -1;
        }

        return new string(characters);
    }

    private static void BlankRange(char[] characters, int from, int to)
    {
        for (var position = Math.Max(from, 0); position <= to && position < characters.Length; position++)
        {
            if (characters[position] is not ('\r' or '\n'))
            {
                characters[position] = ' ';
            }
        }
    }

    /// <summary>
    /// The position of the brace closing the one at <paramref name="open"/>.
    /// </summary>
    /// <remarks>
    /// Braces inside a string literal, a char literal or a comment are skipped. Counting them
    /// meant a single <c>'{'</c> or a <c>"{0}"</c> format string inside an <c>@code</c> block left
    /// the depth permanently unbalanced, the scan ran to end of file, and every piece of markup
    /// after that block was blanked - silently unscanned by the gate that exists to read it.
    /// </remarks>
    private static int MatchingBrace(string source, int open)
    {
        var depth = 0;
        var index = open;

        while (index < source.Length)
        {
            var current = source[index];

            if (current is '"' or '\'')
            {
                index = SkipCSharpLiteral(source, index);
                continue;
            }

            if (current == '/' && index + 1 < source.Length && source[index + 1] is '/' or '*')
            {
                index = SkipCSharpComment(source, index);
                continue;
            }

            if (current == '{')
            {
                depth++;
            }
            else if (current == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return index;
                }
            }
            else
            {
                // Any other character leaves the brace depth unchanged.
            }

            index++;
        }

        return source.Length - 1;
    }

    /// <summary>
    /// The index just past a C# string or char literal, escapes honoured.
    /// </summary>
    private static int SkipCSharpLiteral(string source, int start)
    {
        var quote = source[start];
        var index = start + 1;

        while (index < source.Length && source[index] != quote)
        {
            index += source[index] == '\\' ? 2 : 1;
        }

        return index + 1;
    }

    /// <summary>
    /// The index just past a C# line or block comment.
    /// </summary>
    private static int SkipCSharpComment(string source, int start)
    {
        if (source[start + 1] == '/')
        {
            var newline = source.IndexOf('\n', start);

            return newline < 0 ? source.Length : newline;
        }

        var close = source.IndexOf("*/", start + 2, StringComparison.Ordinal);

        return close < 0 ? source.Length : close + 2;
    }

    /// <summary>
    /// The text between tags, with the offset each run starts at.
    /// </summary>
    /// <remarks>
    /// A malformed tag makes this yield MORE text than there really is, never less. That direction
    /// matters: an over-reading gate fails loudly on a file someone has to look at, while an
    /// under-reading one silently stops covering the string it was written to catch.
    /// </remarks>
    private static IEnumerable<(int Offset, string Text)> TextNodes(string source)
    {
        var index = 0;

        while (index < source.Length)
        {
            var open = source.IndexOf('<', index);
            var textEnd = open < 0 ? source.Length : open;

            if (textEnd > index)
            {
                yield return (index, source[index..textEnd]);
            }

            var close = open < 0 ? -1 : source.IndexOf('>', open);

            // No tag left to close means no further text node: either the file ended, or it ends
            // inside an unterminated tag.
            index = close < 0 ? source.Length : close + 1;
        }
    }

    /// <summary>
    /// Localisable attributes whose value is a literal rather than a Razor expression.
    /// </summary>
    private static IEnumerable<(int Offset, string Name, string Value)> LocalisableAttributeValues(string source) =>
        from match in AttributePattern.Matches(source).Cast<Match>()
        where LocalisableAttributes.Contains(match.Groups[1].Value)
        let value = AttributeValueOf(match)
        where !value.TrimStart().StartsWith('@')
        select (match.Index, match.Groups[1].Value, value);

    /// <summary>
    /// The quoted value of an attribute match, from whichever quoting style matched.
    /// </summary>
    private static string AttributeValueOf(Match match) =>
        match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;

    /// <summary>
    /// The <c>value</c> attributes that are a button's visible label rather than a form value.
    /// </summary>
    private static IEnumerable<(int Offset, string Name, string Value)> LabelledInputValues(string source)
    {
        foreach (var element in InputElementPattern.Matches(source).Cast<Match>())
        {
            var attributes = element.Groups[1].Value;
            var pairs = AttributePattern.Matches(attributes)
                .Cast<Match>()
                .ToDictionary(m => m.Groups[1].Value, AttributeValueOf, StringComparer.OrdinalIgnoreCase);

            if (pairs.TryGetValue("type", out var type)
                && LabelledInputTypes.Contains(type)
                && pairs.TryGetValue("value", out var value)
                && !value.TrimStart().StartsWith('@'))
            {
                yield return (element.Index, "value", value);
            }
        }
    }

    /// <summary>
    /// What is left of a text run once Razor expressions, entities and Glossary proper nouns are
    /// taken out - in other words, the part a translator would have to translate.
    /// </summary>
    private static string TranslatableText(string text)
    {
        var stripped = RazorExpressionPattern.Replace(text, " ");
        stripped = HtmlEntityPattern.Replace(stripped, " ");

        var words = WordPattern.Matches(stripped)
            .Select(m => m.Value)
            .Where(w => !GlossaryProperNouns.Contains(w))
            .ToList();

        return words.Count == 0 ? string.Empty : string.Join(' ', words);
    }

    // A var() reference to a token whose name ends in -light.
    [GeneratedRegex(@"var\(\s*--([A-Za-z0-9_-]+-light)\b", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex LightTokenReferencePattern { get; }

    // Any mention of a -light custom property in markup, declaration or reference alike: a
    // component has no business doing either.
    [GeneratedRegex(@"--([A-Za-z0-9_-]+-light)\b", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex LightTokenNamePattern { get; }

    // A hex colour, any of the colour functions that produce one, or a named colour keyword.
    //
    // The named colours are here because `color: red` bypasses the theme boundary, the 30-token
    // count and the contrast harness identically to `color: #FF0000` - all three of the reasons
    // this gate's own message gives - and the original pattern let every one of them through.
    // `WcagContrast.Channels` already refuses a named colour as unverifiable, so leaving them out
    // had the two files disagreeing about whether they were a problem. The list is the CSS Color 4
    // basic and extended keywords. The cascade keywords are deliberately absent - `inherit`,
    // `initial`, `unset`, `revert` and `currentColor` all take a colour from somewhere else rather
    // than stating one, so they are not literals.
    [GeneratedRegex(
        @"#[0-9A-Fa-f]{3,8}\b|\b(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch|color|color-mix)\s*\(|" +
        @"\b(?:transparent|aliceblue|antiquewhite|aqua|aquamarine|azure|beige|bisque|black|" +
        @"blanchedalmond|blue|blueviolet|brown|burlywood|cadetblue|chartreuse|chocolate|coral|" +
        @"cornflowerblue|cornsilk|crimson|cyan|darkblue|darkcyan|darkgoldenrod|darkgray|" +
        @"darkgreen|darkgrey|darkkhaki|darkmagenta|darkolivegreen|darkorange|darkorchid|darkred|" +
        @"darksalmon|darkseagreen|darkslateblue|darkslategray|darkslategrey|darkturquoise|" +
        @"darkviolet|deeppink|deepskyblue|dimgray|dimgrey|dodgerblue|firebrick|floralwhite|" +
        @"forestgreen|fuchsia|gainsboro|ghostwhite|gold|goldenrod|gray|green|greenyellow|grey|" +
        @"honeydew|hotpink|indianred|indigo|ivory|khaki|lavender|lavenderblush|lawngreen|" +
        @"lemonchiffon|lightblue|lightcoral|lightcyan|lightgoldenrodyellow|lightgray|lightgreen|" +
        @"lightgrey|lightpink|lightsalmon|lightseagreen|lightskyblue|lightslategray|" +
        @"lightslategrey|lightsteelblue|lightyellow|lime|limegreen|linen|magenta|maroon|" +
        @"mediumaquamarine|mediumblue|mediumorchid|mediumpurple|mediumseagreen|mediumslateblue|" +
        @"mediumspringgreen|mediumturquoise|mediumvioletred|midnightblue|mintcream|mistyrose|" +
        @"moccasin|navajowhite|navy|oldlace|olive|olivedrab|orange|orangered|orchid|" +
        @"palegoldenrod|palegreen|paleturquoise|palevioletred|papayawhip|peachpuff|peru|pink|" +
        @"plum|powderblue|purple|rebeccapurple|red|rosybrown|royalblue|saddlebrown|salmon|" +
        @"sandybrown|seagreen|seashell|sienna|silver|skyblue|slateblue|slategray|slategrey|snow|" +
        @"springgreen|steelblue|tan|teal|thistle|tomato|turquoise|violet|wheat|white|whitesmoke|" +
        @"yellow|yellowgreen)\b",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 5000)]
    private static partial Regex ColourLiteralPattern { get; }

    // px exactly as written. Case-insensitive and leading-dot-tolerant for the same reason
    // CssCorpus.PixelLengthPattern is: `13PX` and `.5px` are lengths the browser honours.
    [GeneratedRegex(
        @"(?<![\w.])(-?(?:\d+(?:\.\d+)?|\.\d+))px\b",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 5000)]
    private static partial Regex RawPixelPattern { get; }

    // An absolute length in a unit other than px. rem and em are deliberately absent: they scale
    // with the user's root size, which is the whole point of the type scale.
    [GeneratedRegex(
        @"(?<![\w.])-?(?:\d+(?:\.\d+)?|\.\d+)(?:pt|pc|in|cm|mm|q)\b",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 5000)]
    private static partial Regex AbsoluteNonPixelPattern { get; }

    // A `resolution` media condition in an at-rule path, in either bound.
    [GeneratedRegex(@"(?:min-|max-)?resolution\s*:", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 5000)]
    private static partial Regex ResolutionConditionPattern { get; }

    [GeneratedRegex(@"min-resolution\s*:\s*([\d.]+)dppx", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 5000)]
    private static partial Regex MinResolutionPattern { get; }

    [GeneratedRegex(@"max-resolution\s*:\s*([\d.]+)dppx", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 5000)]
    private static partial Regex MaxResolutionPattern { get; }

    // A bare `0` used as a whole value, with no unit.
    [GeneratedRegex(@"^\s*-?0(?:\.0+)?\s*$", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex UnitlessZeroPattern { get; }

    // A length or percentage in any unit other than px. `0` alone is excluded because it is
    // unitless and means "no radius" rather than a radius in some other unit.
    //
    // The trailing \b applies to the ALPHABETIC units only. Written as `(?:%|em|...)\b` it never
    // matched a percentage at all: `%` is not a word character, so between it and the end of the
    // value there is no word boundary - and `border-radius: 50%`, the exact way a circular avatar
    // arrives, passed this gate silently. Caught by Task 5's planting.
    [GeneratedRegex(
        @"(?<![\w.])\d+(?:\.\d+)?(?:%|(?:rem|em|ex|ch|vw|vh|vmin|vmax|cm|mm|in|pt|pc|q)\b)",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 5000)]
    private static partial Regex NonPixelLengthPattern { get; }

    // A fixed, non-scaling-with-content length: px or a font-relative unit. Percentages, `auto`,
    // `fit-content`, `min-content` and the viewport units are all content- or container-relative
    // and let a box grow.
    [GeneratedRegex(
        @"(?<![\w.])\d+(?:\.\d+)?(?:px|em|rem|ex|ch|cm|mm|in|pt|pc)\b",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 5000)]
    private static partial Regex FixedLengthPattern { get; }

    // `left` or `right` as a whole word in a declared value.
    [GeneratedRegex(@"\b(left|right)\b", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 5000)]
    private static partial Regex PhysicalDirectionValuePattern { get; }

    // An `a` element selector: at the start, after a combinator, or inside a `:is()`/`:where()`
    // argument list, and not part of a longer identifier. `.accent` and `[data-a]` must not match.
    // The `(` and `)` in the character classes are what make `:is(a, button)` match - a global
    // underline reset written that way was previously invisible to the AC10 removal ban.
    [GeneratedRegex(@"(^|[\s,>+~(])a(?=[\s,>+~:.\[#)]|$)", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex AnchorElementSelectorPattern { get; }

    // The `.text-link` class exactly, not `.text-link-quiet` or any other longer identifier.
    [GeneratedRegex(@"\.text-link(?![\w-])", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 5000)]
    private static partial Regex TextLinkClassPattern { get; }

    // A class whose final hyphen-separated segment is `link`, or one of the link pseudo-classes.
    // `.blinking` does not match; `.text-link`, `.reload-link` and `:any-link` do.
    [GeneratedRegex(
        @"\.(?:[\w-]+-)?link(?![\w-])|:(?:any-link|link|visited)\b",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 5000)]
    private static partial Regex LinkClassSelectorPattern { get; }

    // A Razor directive line. These are compiler instructions, not markup.
    [GeneratedRegex(
        @"^[ \t]*@(using|page|namespace|inherits|implements|inject|layout|attribute|typeparam|rendermode|preservewhitespace|addTagHelper|removeTagHelper|tagHelperPrefix|model)\b[^\r\n]*",
        RegexOptions.Multiline,
        matchTimeoutMilliseconds: 5000)]
    private static partial Regex RazorDirectivePattern { get; }

    // A Razor expression in markup: an explicit @(...), or an implicit @Member.Chain with any
    // number of call and INDEXER suffixes.
    //
    // The indexer suffix is what makes the standard localisation idiom pass. `@Localizer["Delete"]`
    // stripped to `["Delete"]` under the old pattern and was reported as the literal text
    // "Delete" - so the `IStringLocalizer` indexer form, which is how AC11 is actually satisfied in
    // Blazor, failed the gate that exists to require it. Only the parenthesised
    // `@(Loc["Delete"])` survived.
    [GeneratedRegex(
        @"@\([^()]*(?:\([^()]*\)[^()]*)*\)|@[A-Za-z_][A-Za-z0-9_.]*(?:\([^()]*\)|\[[^\]]*\])*",
        RegexOptions.None,
        matchTimeoutMilliseconds: 5000)]
    private static partial Regex RazorExpressionPattern { get; }

    // A Razor control-flow header: the keyword plus its parenthesised condition, if any.
    [GeneratedRegex(
        @"@(?:else\s+if|if|foreach|for|while|switch|do|try|catch|finally|lock|else)\b" +
        @"\s*(?:\([^()]*(?:\([^()]*\)[^()]*)*\))?",
        RegexOptions.None,
        matchTimeoutMilliseconds: 5000)]
    private static partial Regex RazorControlFlowPattern { get; }

    // A `<script>` or `<style>` element and its body. Neither carries user-visible copy, and both
    // are full of identifiers the word scan would report as literal text.
    [GeneratedRegex(
        @"<(script|style)\b[^>]*>[\s\S]*?</\1\s*>",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 5000)]
    private static partial Regex NonCopyElementPattern { get; }

    // A C# string literal, verbatim and interpolated forms included.
    [GeneratedRegex(@"""((?:[^""\\\r\n]|\\.)*)""", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex CSharpStringLiteralPattern { get; }

    [GeneratedRegex(@"&(?:[A-Za-z][A-Za-z0-9]*|#\d+|#[xX][0-9A-Fa-f]+);", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex HtmlEntityPattern { get; }

    // A word a person would read. Two letters or more, so a stray initial or a units suffix in an
    // expression remnant is not reported as copy.
    [GeneratedRegex(@"\p{L}{2,}", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex WordPattern { get; }

    // An attribute and its value, in EITHER quoting style. The double-quote-only reading made
    // `title='Delete'` invisible - single quotes are legal in both HTML and Razor, and a gate that
    // sees only one of them is a gate a developer's editor settings can switch off.
    [GeneratedRegex(
        @"([A-Za-z-]+)\s*=\s*(?:""([^""]*)""|'([^']*)')",
        RegexOptions.None,
        matchTimeoutMilliseconds: 5000)]
    private static partial Regex AttributePattern { get; }

    // An `<input>` element, captured so its `type` and `value` can be read together.
    [GeneratedRegex(@"<input\b([^>]*)>", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 5000)]
    private static partial Regex InputElementPattern { get; }
}
