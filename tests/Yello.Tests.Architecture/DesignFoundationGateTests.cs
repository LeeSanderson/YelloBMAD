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

    private static readonly double[] AllowedRadiiPx = [0, 2, 3, 6, 9999];

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
        };

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
        };

    /// <summary>
    /// The only words permitted as a literal in a component: proper nouns PRD section 2's Glossary
    /// fixes, which are not translated in any locale.
    /// </summary>
    /// <remarks>
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
            if (!TypographicProperties.Contains(declaration.Property))
            {
                continue;
            }

            problems.AddRange(CssCorpus.PixelLengths(declaration.Value)
                .Select(length =>
                    $"{Where(sheet, declaration)} sets '{declaration.Property}' to a px length " +
                    $"({length}px) in '{rule.Selector}'."));

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
            "Type is sized in absolute pixels.",
            "Sizes are in `rem` against a 16px root and line-heights are >= 1.5. px survives only " +
            "on hairlines, radii and outline offsets - values that should not scale with text.");
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

            problems.AddRange(CssCorpus.PixelLengths(declaration.Value)
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
        var focusRules = CssCorpus.AllRules()
            .Where(p => p.Rule.Selector.Contains(":focus-visible", StringComparison.OrdinalIgnoreCase))
            .Where(p => p.Rule.Declarations.Any(IsRingDeclaration))
            .ToList();

        if (focusRules.Count == 0)
        {
            problems.Add(
                "No rule in the repository draws an outline on ':focus-visible'. Keyboard parity " +
                "on the Board is a PRD requirement, which makes one visible focus treatment " +
                "load-bearing rather than decorative.");
        }

        foreach (var (sheet, rule) in focusRules)
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
            if (!IsBorderWidthProperty(declaration.Property))
            {
                continue;
            }

            problems.AddRange(CssCorpus.PixelLengths(declaration.Value)
                .Where(width => width > LengthTolerance && width < HairlineFloorPx - LengthTolerance)
                .Select(width =>
                    $"{Where(sheet, declaration)} sets '{declaration.Property}' to {width}px on " +
                    $"'{rule.Selector}'."));
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
            if (!IsMinimumHeightProperty(declaration.Property))
            {
                continue;
            }

            problems.AddRange(CssCorpus.PixelLengths(declaration.Value)
                .Where(px => px > LengthTolerance && px < TargetFloorPx - LengthTolerance)
                .Select(px =>
                    $"{Where(sheet, declaration)} sets '{declaration.Property}' to {px}px on " +
                    $"'{rule.Selector}', below the {TargetFloorPx}px floor."));
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

        var underlined = CssCorpus.AllRules()
            .Where(p => p.Rule.Selector.Contains("text-link", StringComparison.OrdinalIgnoreCase))
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

            if (rule.Selector.Contains(":lang(", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            problems.Add(
                $"{Where(sheet, declaration)} declares '{declaration.Property}: " +
                $"{declaration.Value}' on '{rule.Selector}', which is not locale-scoped.");
        }

        AssertNoProblems(problems,
            "A casing transform is applied without a locale scope.",
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
            .Where(p => IsReducedMotionContext(p.Rule.AtRulePath))
            .Where(p => p.Rule.Selector.Contains('*', StringComparison.Ordinal))
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
            if (!declaration.IsImportant
                || !IsMotionProperty(declaration.Property)
                || IsReducedMotionContext(rule.AtRulePath))
            {
                continue;
            }

            problems.Add(
                $"{Where(sheet, declaration)} declares '{declaration.Property}' !important " +
                $"outside the reduced-motion block, on '{rule.Selector}'. It would outrank the " +
                "reset on source order.");
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
        var outline = rule.Declarations.FirstOrDefault(IsRingDeclaration);

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

        var widths = CssCorpus.PixelLengths(outline.Value).ToList();

        if (widths.Count == 0 || widths.TrueForAll(w => w < FocusRingFloorPx - LengthTolerance))
        {
            yield return
                $"{where} ('{rule.Selector}') draws the ring at {DescribeWidths(widths)} rather " +
                $"than at least {FocusRingFloorPx}px.";
        }

        if (!rule.Declarations.Any(d =>
                d.Property.Equals("outline-offset", StringComparison.OrdinalIgnoreCase)))
        {
            yield return
                $"{where} ('{rule.Selector}') sets no 'outline-offset'. The default is 0, which " +
                "draws the ring on the control - and at 1.45 against the accent it vanishes " +
                "there. The offset is what makes the ring visible.";
        }
    }

    private static string DescribeWidths(IReadOnlyList<double> widths) =>
        widths.Count == 0 ? "no stated width" : $"{widths[0]}px";

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
    /// True when a declaration actually draws the ring, rather than merely positioning it.
    /// </summary>
    private static bool IsRingDeclaration(CssCorpus.Declaration declaration) =>
        declaration.Property.Equals("outline", StringComparison.OrdinalIgnoreCase)
        || declaration.Property.Equals("outline-width", StringComparison.OrdinalIgnoreCase);

    private static bool IsMinimumHeightProperty(string property) =>
        property.Equals("min-height", StringComparison.OrdinalIgnoreCase)
        || property.Equals("min-block-size", StringComparison.OrdinalIgnoreCase);

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
    /// True when a value is the absence of the thing rather than a setting of it.
    /// </summary>
    private static bool IsNoneValue(string value) => AbsenceValues.Contains(value.Trim());

    /// <summary>
    /// True when an outline declaration takes the ring away.
    /// </summary>
    private static bool RemovesOutline(CssCorpus.Declaration declaration)
    {
        if (declaration.Property.Equals("outline-offset", StringComparison.OrdinalIgnoreCase)
            || declaration.Property.Equals("outline-color", StringComparison.OrdinalIgnoreCase))
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
    private static bool SelectorTargetsLink(string selector) =>
        selector.Contains("link", StringComparison.OrdinalIgnoreCase)
        || AnchorElementSelectorPattern.IsMatch(selector);

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

        foreach (var opener in new[] { "@code", "@functions", "@{" })
        {
            blanked = BlankBracedBlocks(blanked, opener);
        }

        return blanked;
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

    private static int MatchingBrace(string source, int open)
    {
        var depth = 0;

        for (var index = open; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
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
        }

        return source.Length - 1;
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
        let value = match.Groups[2].Value
        where !value.TrimStart().StartsWith('@')
        select (match.Index, match.Groups[1].Value, value);

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

    // A hex colour, or any of the colour functions that produce one.
    [GeneratedRegex(
        @"#[0-9A-Fa-f]{3,8}\b|\b(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch|color|color-mix)\s*\(",
        RegexOptions.None,
        matchTimeoutMilliseconds: 5000)]
    private static partial Regex ColourLiteralPattern { get; }

    [GeneratedRegex(@"(?<![\w.])(-?\d+(?:\.\d+)?)px\b", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex RawPixelPattern { get; }

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

    // An `a` element selector: at the start or after a combinator, and not part of a longer
    // identifier. `.accent` and `[data-a]` must not match.
    [GeneratedRegex(@"(^|[\s,>+~])a(?=[\s,>+~:.\[#]|$)", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex AnchorElementSelectorPattern { get; }

    // A Razor directive line. These are compiler instructions, not markup.
    [GeneratedRegex(
        @"^[ \t]*@(using|page|namespace|inherits|implements|inject|layout|attribute|typeparam|rendermode|preservewhitespace|addTagHelper|removeTagHelper|tagHelperPrefix|model)\b[^\r\n]*",
        RegexOptions.Multiline,
        matchTimeoutMilliseconds: 5000)]
    private static partial Regex RazorDirectivePattern { get; }

    // A Razor expression in markup: an explicit @(...), or an implicit @Member.Chain(args).
    [GeneratedRegex(
        @"@\([^()]*(?:\([^()]*\)[^()]*)*\)|@[A-Za-z_][A-Za-z0-9_.]*(?:\([^()]*\))?",
        RegexOptions.None,
        matchTimeoutMilliseconds: 5000)]
    private static partial Regex RazorExpressionPattern { get; }

    [GeneratedRegex(@"&(?:[A-Za-z][A-Za-z0-9]*|#\d+|#[xX][0-9A-Fa-f]+);", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex HtmlEntityPattern { get; }

    // A word a person would read. Two letters or more, so a stray initial or a units suffix in an
    // expression remnant is not reported as copy.
    [GeneratedRegex(@"\p{L}{2,}", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex WordPattern { get; }

    [GeneratedRegex(@"([A-Za-z-]+)\s*=\s*""([^""]*)""", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex AttributePattern { get; }
}
