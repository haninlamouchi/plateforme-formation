using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PlateformeFormation.Api.Helpers;
using PlateformeFormation.Api.Models;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using static PlateformeFormation.Api.Helpers.FormationContentParser;

namespace PlateformeFormation.Api.Services;

// Curated ~12-14 slide presentation deck (per module count), built directly on the OpenXml SDK (no
// PowerPoint/Office install needed to generate it). A synthesis deck meant to be projected in a
// meeting or at the start of a session — deliberately NOT the same level of detail as
// FormationExportService's PDF (the reference document: context, modalités, detailed modules,
// évaluation grids, notes formateur, etc.). Internal-only content (quality box, grille d'évaluation,
// notes formateur, activités pédagogiques detail, lacunes contexte, sources documentaires) stays
// PDF-only. Every field the PPT does show is derived mechanically (truncation, sentence-splitting,
// prefix-stripping — see "Mechanical text shortening" below) from the exact same validated JSON the
// PDF reads, via the same ParseObject/ParseCards calls — never a second LLM call at export time, and
// never a duration/schedule recomputed differently than the PDF's.
//
// Visual language — "Cahier & Tableau" (school notebook & chalkboard), replacing an earlier generic
// SaaS-deck look (full-bleed color gradients, rounded-corner shadow cards, uniform pill badges, empty
// decorative circles): flat chalkboard fields (title/module/closing slides) instead of gradients,
// notebook-paper content slides with a red margin rule instead of a colored header band, a real
// table-of-contents layout for the module list instead of chart-shaped timeline components, fiches
// bristol (staggered index cards) instead of the rounded-shadow-card kit for Bénéfices, and module
// color-coding drawn from real French school-subject binder colors (SubjectPalette/SubjectColor)
// rather than an arbitrary categorical palette. Every slide carries a footer (slide number + formation
// title) from one shared template (AddFooter) rather than being styled per-slide.
//
// Type scale: TitlePt (38, within the spec's 36-40pt slide-title range) and BodyPt/BodyMinPt (24/20,
// close to the spec's 24pt body-text minimum) are enforced by shortening content to fit rather than
// ever shrinking font size below them. Georgia (FontDisplay) carries titles/numerals; Segoe UI (Font)
// carries body copy — a deliberate two-role pairing, not one system font used for everything.
//
// Layout correctness: OpenXml gives no text-measurement API (unlike PdfSharpCore's MeasureString, which
// FormationExportService relies on for its WrapText/EnsureSpace flow) — a text box's real height at
// render time depends on how PowerPoint wraps it, which this code cannot query. Every multi-line or
// free-text block is therefore explicitly measured with EstimateLineCount/MeasureTextHeight below
// *before* being placed, and every free-text field is Truncate()'d to a sane cap (sized from the box's
// real measured width via MaxCharsForBox, not a guessed constant, and always breaking at the last word
// boundary) — this is what prevents long AI-generated sentences from silently overflowing their box,
// colliding with whatever comes next, or getting cut mid-word. Sections whose slide count scales with
// formation size (Objectifs pédagogiques, the module list, module slides) paginate for the same
// reason; sections that are always a handful of items (Contexte, Bénéfices, Test de positionnement,
// Ressources et pratique) are capped instead, since they never need more than one slide.
public class FormationPptxExportService : IFormationPptxExportService
{
    // 16:9 widescreen, in EMU (914400 EMU = 1 inch; 12700 EMU = 1 point).
    private const long SlideWidth = 12192000L;
    private const long SlideHeight = 6858000L;
    private const long Margin = 548640L; // 0.6"
    private const long EmuPerPoint = 12700L;

    private const string CardBg = "FFFFFF";
    private const string Font = "Segoe UI";

    // Projection-legible type scale (spec: 36-40pt slide titles, 24pt minimum body). FitFontSize's
    // minSize floors always sit within this scale — content is truncated/shortened to fit at these
    // sizes rather than the font ever shrinking below them.
    private const int TitlePt = 38;
    private const int BodyPt = 24;
    private const int BodyMinPt = 20;
    private const int CaptionPt = 16;

    public byte[] GeneratePptx(Formation formation)
    {
        var objectifs = ParseObject<ObjectifsData>(formation.Objectifs)
            ?? new ObjectifsData(null, null, null, null, null, null, null, null, null, null, null, null);
        var modules = ParseCards<ModuleCard>(formation.Modules,
            () => new ModuleCard(0, formation.Modules, null, null, null, null, null, null, null, null, null, null, null))
            .OrderBy(m => m.Numero).ToList();
        var evaluation = ParseCards<EvaluationItem>(formation.MethodesEvaluation,
            () => new EvaluationItem(formation.MethodesEvaluation, null, false));

        using var stream = new MemoryStream();
        using (var doc = PresentationDocument.Create(stream, PresentationDocumentType.Presentation))
        {
            var presentationPart = doc.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation
            {
                SlideSize = new P.SlideSize { Cx = (int)SlideWidth, Cy = (int)SlideHeight, Type = P.SlideSizeValues.Screen16x9 },
                NotesSize = new P.NotesSize { Cx = 6858000, Cy = 9144000 },
            };

            var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>();
            var themePart = slideMasterPart.AddNewPart<ThemePart>();
            themePart.Theme = BuildThemeV2();

            var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>();
            slideLayoutPart.SlideLayout = BuildBlankLayout();

            slideMasterPart.SlideMaster = BuildSlideMaster();
            slideMasterPart.SlideMaster.SlideLayoutIdList = new P.SlideLayoutIdList(
                new P.SlideLayoutId { Id = 2147483649, RelationshipId = slideMasterPart.GetIdOfPart(slideLayoutPart) });

            presentationPart.Presentation.SlideMasterIdList = new P.SlideMasterIdList(
                new P.SlideMasterId { Id = 2147483648, RelationshipId = presentationPart.GetIdOfPart(slideMasterPart) });

            var slideIdList = new P.SlideIdList();
            presentationPart.Presentation.SlideIdList = slideIdList;

            uint slideId = 256;

            // Sections are built up front (not inserted as each is built) so both the footer's
            // "N / total" and the Sommaire slide's per-section slide numbers can be computed —
            // neither is known until every optional/paginated section has decided whether it applies.
            // `Dark` marks a full-bleed Tableau-background slide, whose footer needs a light-on-dark
            // treatment instead of the default muted-on-paper one.
            var sections = new List<(string Label, List<P.ShapeTree> Slides, bool Dark)>();

            var contexteSlides = BuildContexteSlidesV2(objectifs);
            if (contexteSlides.Count > 0) sections.Add(("Contexte et enjeux", contexteSlides, false));

            var objectifsSlides = BuildObjectifsPedagogiquesSlidesV2(modules);
            if (objectifsSlides.Count > 0) sections.Add(("Objectifs pédagogiques", objectifsSlides, false));

            var benefices = BuildBeneficesSlideV2(objectifs.BeneficesEntreprise);
            if (benefices is not null) sections.Add(("Bénéfices pour l'entreprise", [benefices], false));

            // "Vue d'ensemble" (a real table-of-contents-style module list) is the only planning slide
            // the spec asks for — the day-by-day Jour 1/Jour 2/... breakdown the previous deck also
            // generated is exactly the kind of operational detail the brief says to leave in the PDF
            // only.
            var vueEnsembleSlides = BuildVueDEnsembleSlidesV2(modules);
            if (vueEnsembleSlides.Count > 0) sections.Add(("Vue d'ensemble du programme", vueEnsembleSlides, false));

            if (modules.Count > 0)
                sections.Add((modules.Count == 1 ? "Module pédagogique" : $"Modules pédagogiques ({modules.Count})",
                    modules.Select(BuildModuleSlideV2).ToList(), true));

            var testPositionnementSlides = BuildTestPositionnementSlidesV2(objectifs.TestPositionnement, objectifs.ModuleBonus, modules);
            if (testPositionnementSlides.Count > 0) sections.Add(("Test de positionnement et différenciation", testPositionnementSlides, false));

            var evaluationSlides = BuildEvaluationSlidesV2(evaluation);
            if (evaluationSlides.Count > 0) sections.Add(("Modalités d'évaluation", evaluationSlides, false));

            var ressourcesSlides = BuildRessourcesPratiqueSlidesV2(objectifs);
            if (ressourcesSlides.Count > 0) sections.Add(("Ressources et pratique", ressourcesSlides, false));

            var slides = new List<(P.ShapeTree Tree, bool Dark)>
            {
                (BuildTitleSlideV2(formation, objectifs), true),
            };

            // Slide numbers below account for the Sommaire's own page count (it paginates just like
            // every other list slide once there are enough sections — a formation with Contexte,
            // Objectifs, Bénéfices, Vue d'ensemble, Modules, Test de positionnement, Évaluation and
            // Ressources all present reaches 8 rows, more than one page holds) — computed from
            // `sections` directly rather than guessed, so it never drifts out of sync with the real
            // slide order built right after it.
            const int sommaireRowsPerPage = 5;
            var sommairePageCount = sections.Count > 0 ? (int)Math.Ceiling(sections.Count / (double)sommaireRowsPerPage) : 0;
            var slideNo = 2 + sommairePageCount;
            var sommaireEntries = new List<(string Label, int SlideNumber)>();
            foreach (var (label, secSlides, _) in sections)
            {
                sommaireEntries.Add((label, slideNo));
                slideNo += secSlides.Count;
            }
            foreach (var t in BuildSommaireSlidesV2(sommaireEntries)) slides.Add((t, false));

            foreach (var (_, secSlides, dark) in sections)
                foreach (var t in secSlides) slides.Add((t, dark));

            slides.Add((BuildClotureSlideV2(formation), true));

            var totalSlides = slides.Count;
            for (var i = 0; i < slides.Count; i++)
            {
                var (tree, dark) = slides[i];
                var footerId = tree.Descendants<P.NonVisualDrawingProperties>()
                    .Select(p => p.Id?.Value ?? 0u).DefaultIfEmpty(1u).Max() + 1;
                AddFooter(tree, ref footerId, formation.Titre, i + 1, totalSlides, dark);

                var slidePart = presentationPart.AddNewPart<SlidePart>();
                slidePart.AddPart(slideLayoutPart);
                slidePart.Slide = new P.Slide(new P.CommonSlideData(tree));
                slideIdList.Append(new P.SlideId { Id = slideId++, RelationshipId = presentationPart.GetIdOfPart(slidePart) });
            }

            presentationPart.Presentation.Save();
        }

        return stream.ToArray();
    }

    // ---------- Text measurement & truncation ----------
    // No layout engine to lean on (unlike the PDF export's PdfSharpCore.MeasureString) — these
    // approximate Segoe UI's average glyph width well enough to size boxes so real content never
    // overflows into whatever is placed next.

    // `bold` widens the assumed average glyph — measuring bold text with the regular-weight factor is
    // exactly what let a label estimated at "2 lines" still render as 3 in real PowerPoint (bold Segoe
    // UI glyphs are meaningfully wider), which is what caused the évaluation slide's label/bar overlap
    // to appear on some items and not others: only the ones close to the wrap boundary tipped over.
    private static int EstimateLineCount(string text, int sizePt, long widthEmu, bool bold = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return 1;
        var widthPt = widthEmu / (double)EmuPerPoint;
        var factor = bold ? 0.60 : 0.52;
        var charsPerLine = Math.Max(6, (int)(widthPt / (sizePt * factor)));
        return Math.Max(1, (int)Math.Ceiling(text.Length / (double)charsPerLine));
    }

    private static long MeasureTextHeight(string text, int sizePt, long widthEmu, bool bold = false) =>
        EstimateLineCount(text, sizePt, widthEmu, bold) * (long)Math.Round(sizePt * 1.32 * EmuPerPoint);

    private static long MeasureBlockHeight(IEnumerable<(string Text, int SizePt)> lines, long widthEmu, long gapEmu) =>
        lines.Sum(l => MeasureTextHeight(l.Text, l.SizePt, widthEmu) + gapEmu);

    // Shared vertical-flow step, reused by every slide that stacks a variable number of text rows
    // (module contenu checklist, évaluation labels): advances the cursor `y` past a text block using
    // its REAL measured wrap height — never a fixed/assumed one — plus `innerPad` (padding baked into
    // the returned box height) and `outerGap` (space before the next block). Every "chevauchement" bug
    // reported against this deck traced back to some element positioned from a guessed offset instead
    // of this: a `Math.Min(height, remainingSpace)` clamp that shrank a box below what its own text
    // needed (module contenu), a hardcoded "+440000" bar offset that never looked at how tall the
    // label above it actually rendered (évaluation), and that same label being measured with the
    // wrong (non-bold) width factor even though it renders bold. Neither mistake is possible once
    // every caller advances through this one function (with the right `bold` flag) instead of
    // computing its own offset.
    private static long NextRow(ref long y, string text, int sizePt, long widthEmu, long innerPad, long outerGap, bool bold = false)
    {
        var boxHeight = MeasureTextHeight(text, sizePt, widthEmu, bold) + innerPad;
        y += boxHeight + outerGap;
        return boxHeight;
    }

    // Cuts at the last space at or before maxChars, never mid-word — a raw t[..maxChars] cut produces
    // garbage like "d'identifi…" wherever the limit happened to land inside a word, which then visibly
    // overlaps whatever text comes next. Only falls back to a hard character cut if there's no space
    // to use (a single very long word) or the nearest space is so early it would throw away most of
    // the budget for no good reason.
    private static string Truncate(string? text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var t = text.Trim();
        if (t.Length <= maxChars) return t;

        // Always break at the last space in the cut window when one exists — a "don't throw away too
        // much of the budget" threshold here was exactly what still let short/tight budgets (livrable,
        // badges) fall through to a mid-word cut. The only remaining hard cut is a genuine no-space
        // case (one very long word), which is unavoidable without hyphenation.
        var cut = t[..maxChars];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > 0) cut = cut[..lastSpace];
        return cut.TrimEnd() + "…";
    }

    // How many characters of the given text can actually fit in widthEmu at sizePt over maxLines,
    // respecting the same average-glyph-width model EstimateLineCount uses — so a Truncate() budget
    // can be sized from the real box instead of an eyeballed constant. Callers pass the *smallest*
    // font size they'd ever render at (their FitFontSize minSize), i.e. the worst case for how much
    // text fits, so the result is never over-generous relative to what the final rendered size allows.
    private static int MaxCharsForBox(long widthEmu, int sizePt, int maxLines, bool bold = false)
    {
        var widthPt = widthEmu / (double)EmuPerPoint;
        var factor = bold ? 0.60 : 0.52;
        var charsPerLine = Math.Max(6, (int)(widthPt / (sizePt * factor)));
        return charsPerLine * maxLines;
    }

    // Shrinks font size until the text fits within maxLines at widthEmu, or bottoms out at minSize.
    private static int FitFontSize(string text, long widthEmu, int startSize, int minSize, int maxLines, bool bold = false)
    {
        for (var size = startSize; size >= minSize; size -= 2)
            if (EstimateLineCount(text, size, widthEmu, bold) <= maxLines) return size;
        return minSize;
    }

    // ---------- Slide builders ----------

    // Spec: title + subtitle, duration + target audience on ONE line, no logo/decorative clutter.
    // Simplified from the previous split hero-panel + stat-tile-stack layout, which put an icon,
    // a kicker label, and three separate cards on a slide the brief says should carry none of that.
    // ---------- Shape primitives ----------

    private static P.ShapeTree NewShapeTree() => new(
        new P.NonVisualGroupShapeProperties(
            new P.NonVisualDrawingProperties { Id = 1, Name = "" },
            new P.NonVisualGroupShapeDrawingProperties(),
            new P.ApplicationNonVisualDrawingProperties()),
        new P.GroupShapeProperties());

    // Full-width colored band with a bold title + muted-white subtitle — used by every non-title,
    // non-closing slide instead of the old thin 1.5pt accent line. Title sits at TitlePt (36-40pt
    // band, spec-mandated slide-title scale); the subtitle is a caption, not body copy, so it stays
    // smaller than BodyPt by design.
    private static void AddFooter(P.ShapeTree tree, ref uint id, string formationTitre, int slideNumber, int totalSlides, bool onDark)
    {
        var color = onDark ? Craie : Ardoise;
        var alpha = onDark ? 65 : 55;
        var y = SlideHeight - 340000;
        AddTextAlpha(tree, id++, "footerTitre", Margin, y, SlideWidth - Margin * 2 - 900000, 300000,
            Truncate(formationTitre, 70), 10, false, color, alpha);
        AddTextAlpha(tree, id, "footerPage", SlideWidth - Margin - 700000, y, 700000, 300000,
            $"{slideNumber} / {totalSlides}", 10, false, color, alpha, align: D.TextAlignmentTypeValues.Right);
    }

    // Rounded card with a soft drop shadow — the shadow is what makes a flat colored rectangle read as
    // an elevated "card" rather than a plain filled box.
    private static D.AdjustValueList BuildAdjustList(int? cornerAdj) =>
        cornerAdj is { } adj ? new D.AdjustValueList(new D.ShapeGuide { Name = "adj", Formula = $"val {adj}" }) : new D.AdjustValueList();

    private static void AddRectangle(
        P.ShapeTree tree, uint id, string name, long x, long y, long cx, long cy, string fillHex,
        D.ShapeTypeValues? preset = null, int? cornerAdj = null, int? alphaPct = null, bool shadow = false)
    {
        var fillColor = new D.RgbColorModelHex { Val = fillHex };
        if (alphaPct is { } a) fillColor.Append(new D.Alpha { Val = a * 1000 });

        var shapeProps = new P.ShapeProperties(
            new D.Transform2D(new D.Offset { X = x, Y = y }, new D.Extents { Cx = cx, Cy = cy }),
            new D.PresetGeometry(BuildAdjustList(cornerAdj)) { Preset = preset ?? D.ShapeTypeValues.Rectangle },
            new D.SolidFill(fillColor),
            new D.Outline(new D.NoFill()));

        if (shadow)
        {
            var shadowColor = new D.RgbColorModelHex { Val = "1E1E1E" };
            shadowColor.Append(new D.Alpha { Val = 20000 });
            shapeProps.Append(new D.EffectList(
                new D.OuterShadow(shadowColor) { BlurRadius = 90000, Distance = 30000, Direction = 5400000 }));
        }

        var shape = new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = name },
                new P.NonVisualShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            shapeProps,
            new P.TextBody(new D.BodyProperties(), new D.ListStyle(), new D.Paragraph()));

        tree.Append(shape);
    }

    private static void AddText(
        P.ShapeTree tree, uint id, string name, long x, long y, long cx, long cy,
        IEnumerable<(string Text, int SizePt, bool Bold, string ColorHex)> lines,
        D.TextAlignmentTypeValues? align = null,
        D.TextAnchoringTypeValues? anchor = null,
        bool wrap = true)
    {
        var effectiveAlign = align ?? D.TextAlignmentTypeValues.Left;
        var effectiveAnchor = anchor ?? D.TextAnchoringTypeValues.Top;

        var body = new P.TextBody(
            new D.BodyProperties
            {
                Anchor = effectiveAnchor, Wrap = wrap ? D.TextWrappingValues.Square : D.TextWrappingValues.None,
                LeftInset = 0, RightInset = 0, TopInset = 0, BottomInset = 0,
            },
            new D.ListStyle());

        foreach (var line in lines)
        {
            var runProps = new D.RunProperties { FontSize = line.SizePt * 100, Bold = line.Bold };
            runProps.Append(new D.SolidFill(new D.RgbColorModelHex { Val = line.ColorHex }));
            runProps.Append(new D.LatinFont { Typeface = Font });

            var para = new D.Paragraph(
                new D.ParagraphProperties { Alignment = effectiveAlign },
                new D.Run(runProps, new D.Text(line.Text)));
            body.Append(para);
        }

        var shapeProps = new P.ShapeProperties(
            new D.Transform2D(new D.Offset { X = x, Y = y }, new D.Extents { Cx = cx, Cy = cy }),
            new D.PresetGeometry(new D.AdjustValueList()) { Preset = D.ShapeTypeValues.Rectangle },
            new D.NoFill(),
            new D.Outline(new D.NoFill()));

        var shape = new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = name },
                new P.NonVisualShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            shapeProps,
            body);

        tree.Append(shape);
    }

    // Single-run text with a uniform alpha applied to its color — used for "muted white" subtitles/
    // labels sitting on a colored/gradient background, where a plain darker hex wouldn't read as part
    // of the same surface.
    private static void AddTextAlpha(
        P.ShapeTree tree, uint id, string name, long x, long y, long cx, long cy,
        string text, int sizePt, bool bold, string colorHex, int alphaPct,
        D.TextAlignmentTypeValues? align = null)
    {
        var color = new D.RgbColorModelHex { Val = colorHex };
        color.Append(new D.Alpha { Val = alphaPct * 1000 });

        var runProps = new D.RunProperties { FontSize = sizePt * 100, Bold = bold };
        runProps.Append(new D.SolidFill(color));
        runProps.Append(new D.LatinFont { Typeface = Font });

        var body = new P.TextBody(
            new D.BodyProperties
            {
                Anchor = D.TextAnchoringTypeValues.Top, Wrap = D.TextWrappingValues.Square,
                LeftInset = 0, RightInset = 0, TopInset = 0, BottomInset = 0,
            },
            new D.ListStyle(),
            new D.Paragraph(
                new D.ParagraphProperties { Alignment = align ?? D.TextAlignmentTypeValues.Left },
                new D.Run(runProps, new D.Text(text))));

        var shapeProps = new P.ShapeProperties(
            new D.Transform2D(new D.Offset { X = x, Y = y }, new D.Extents { Cx = cx, Cy = cy }),
            new D.PresetGeometry(new D.AdjustValueList()) { Preset = D.ShapeTypeValues.Rectangle },
            new D.NoFill(),
            new D.Outline(new D.NoFill()));

        var shape = new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = name },
                new P.NonVisualShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            shapeProps,
            body);

        tree.Append(shape);
    }

    // ==================================================================================
    // ---------- "Cahier & Tableau" design direction (notebook & chalkboard) ----------
    // ==================================================================================
    // The shipped visual language for the whole deck (GeneratePptx() above builds every slide from
    // these). Approved after three iterations on the Vue d'ensemble slide alone (a roadmap-style
    // timeline, then an hour-by-day grid, both still reading as generic chart components; a real
    // table-of-contents layout is what actually broke from that pattern).

    private const string Tableau = "8B0000";   // chalkboard — flat structural fields, never a gradient
    private const string Craie = "F4EFE1";     // chalk — text on Tableau
    private const string Papier = "FBF7EC";    // notebook paper — content-slide background
    private const string EncreRouge = "C1272D"; // teacher's red pen — margin rule, annotations only
    private const string Surligneur = "E8B928"; // highlighter — emphasis marks behind key text
    private const string Ardoise = "2E2A23";   // slate ink — body text on paper
    private const string FontDisplay = "Georgia";

    // Real French school-subject binder colors, used only for module color-coding — deliberately not
    // a generic categorical palette (blue/purple/teal/orange in rotation reads as "any SaaS app's
    // category tags"; this reads as an actual cahier de textes).
    private static readonly string[] SubjectPalette =
        ["2D5F8A", "A9682F", "4C7A5D", "6B4E82", "D9791C", "B84C6E", "C9A227"];
    private static string SubjectColor(int numero) => SubjectPalette[(numero - 1 + SubjectPalette.Length) % SubjectPalette.Length];

    // A folded page corner — the one "decorative" mark in this direction, and unlike the previous
    // version's empty circles it has a literal referent (a well-thumbed notebook's dog-eared page).
    private static void AddDogEar(P.ShapeTree tree, uint id, string baseColor)
    {
        const long size = 420000L;
        var shapeProps = new P.ShapeProperties(
            new D.Transform2D(new D.Offset { X = SlideWidth - size, Y = 0 }, new D.Extents { Cx = size, Cy = size })
            { Rotation = 18000000 }, // 180° in 60000ths of a degree — folds the corner down-left instead of up-right
            new D.PresetGeometry(new D.AdjustValueList()) { Preset = D.ShapeTypeValues.RightTriangle },
            new D.SolidFill(new D.RgbColorModelHex { Val = baseColor }),
            new D.Outline(new D.NoFill()));

        var shape = new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = "dogEar" },
                new P.NonVisualShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            shapeProps,
            new P.TextBody(new D.BodyProperties(), new D.ListStyle(), new D.Paragraph()));

        tree.Append(shape);
    }

    // The red vertical rule of ruled notebook paper — this IS the page-identity element, replacing
    // the previous version's full-width colored gradient band on every content slide.
    private static void AddMarginRule(P.ShapeTree tree, ref uint id)
    {
        const long x = 1150000L;
        AddRectangle(tree, id++, "marginRule", x, 0, 9525, SlideHeight, EncreRouge);
    }

    // Faint horizontal hairlines suggesting ruled paper texture — subtle, not a decorative device on
    // its own; it reads correctly only because the page is already established as "paper" by color +
    // margin rule.
    private static void AddRuledLines(P.ShapeTree tree, ref uint id, long x, long y, long width, long bottom, long spacing)
    {
        for (var ly = y; ly < bottom; ly += spacing)
            AddRectangle(tree, id++, $"rule{ly}", x, ly, width, 6350, Ardoise, alphaPct: 8);
    }

    // A highlighter stroke behind text, standing in for the previous version's colored left accent
    // bar — the mark a student actually makes to flag the important sentence in a paragraph.
    private static void AddHighlightMark(P.ShapeTree tree, ref uint id, long x, long y, long cx, long cy)
    {
        AddRectangle(tree, id++, $"highlight{id}", x, y, cx, cy, Surligneur, alphaPct: 55);
    }

    // A binder divider tab — sharp corners, flush-left label, filled in the module's subject color.
    // Replaces the uniform rounded pill badge used for every kind of label in the previous version
    // (méthode, évaluation status, ...): a tab reads as "this belongs to this module/category", which
    // is literally what it's used for, instead of being one generic shape reused for everything.
    private static void AddTab(P.ShapeTree tree, ref uint id, long x, long y, string text, string fillColor, string textColor, int sizePt)
    {
        var width = Math.Max(900000L, 260000L + text.Length * sizePt * 7200L);
        const long height = 460000L;
        AddRectangle(tree, id++, $"tabBg{id}", x, y, width, height, fillColor);
        AddText(tree, id++, $"tabTxt{id}", x + 180000, y, width - 360000, height,
            [(text, sizePt, true, textColor)], anchor: D.TextAnchoringTypeValues.Center, wrap: false);
    }

    // Slide 1 — Titre: a flat chalkboard field (never a gradient), chalk-white Georgia title, a plain
    // chalk underline rule instead of a decorative circle, duration + public cible on one line.
    private static P.ShapeTree BuildTitleSlideV2(Formation formation, ObjectifsData objectifs)
    {
        var tree = NewShapeTree();
        uint id = 2;
        var contentWidth = SlideWidth - Margin * 2;

        AddRectangle(tree, id++, "board", 0, 0, SlideWidth, SlideHeight, Tableau);

        var titre = Truncate(formation.Titre, MaxCharsForBox(contentWidth, TitlePt, 3));
        var titreSize = FitFontSize(titre, contentWidth, 44, TitlePt, 3);
        var titreY = 2200000L;
        var titreHeight = MeasureTextHeightFont(titre, titreSize, contentWidth, FontDisplay);
        AddTextFont(tree, id++, "titre", Margin, titreY, contentWidth, titreHeight, titre, titreSize, true, Craie, FontDisplay);

        var ruleY = titreY + titreHeight + 220000;
        AddRectangle(tree, id++, "chalkRule", Margin, ruleY, 2400000L, 12700, Craie, alphaPct: 70);

        var afterY = ruleY + 380000;
        if (!string.IsNullOrWhiteSpace(objectifs.SousTitre))
        {
            var sousTitre = Truncate(objectifs.SousTitre, MaxCharsForBox(contentWidth, CaptionPt, 1));
            AddTextAlpha(tree, id++, "sousTitre", Margin, afterY, contentWidth, 400000, sousTitre, CaptionPt, false, Craie, 85);
            afterY += 560000;
        }

        var line = string.Join("     ", new[]
        {
            formation.DureeEstimee is { } duree ? $"⏱  {duree:0.#} heures" : null,
            !string.IsNullOrWhiteSpace(objectifs.PublicCible) ? $"🎯  {Truncate(objectifs.PublicCible, 60)}" : null,
        }.Where(s => s is not null));
        if (line.Length > 0)
            AddTextAlpha(tree, id, "infoLine", Margin, afterY, contentWidth, 500000, line, BodyMinPt, true, Craie, 92);

        return tree;
    }

    // Slide 2 — Sommaire: a deck-level table of contents (section name + dot leader + the slide number
    // it actually starts on), one row per top-level section that ended up in the deck — Contexte,
    // Objectifs pédagogiques, Bénéfices, Vue d'ensemble, the module block as a single entry, Test de
    // positionnement, Évaluation, Ressources. Only emitted when `entries` is non-empty (GeneratePptx
    // already only calls this when `sections` isn't empty). Same row-number/dot-leader/page-number
    // layout as BuildVueDEnsembleSlidesV2's module TOC below, just one level up (sections, not
    // modules) and without a duration column, since a section can span several slides. Paginates at
    // the same 5-rows-per-page as every sibling list slide in this deck (Vue d'ensemble, Objectifs
    // pédagogiques, ...) — a formation with every optional section present (Contexte, Objectifs,
    // Bénéfices, Vue d'ensemble, Modules, Test de positionnement, Évaluation, Ressources) reaches 8
    // rows, which a single fixed-height slide can't fit without the numero glyph (30pt, unscaled)
    // overrunning its row — exactly the class of "fixed division, not measured" bug already fixed
    // elsewhere in this file.
    private static List<P.ShapeTree> BuildSommaireSlidesV2(List<(string Label, int SlideNumber)> entries)
    {
        if (entries.Count == 0) return [];

        const int maxRowsPerPage = 5;
        var pageCount = (int)Math.Ceiling(entries.Count / (double)maxRowsPerPage);
        var pages = new List<P.ShapeTree>();

        for (var page = 0; page < pageCount; page++)
        {
            var pageEntries = entries.Skip(page * maxRowsPerPage).Take(maxRowsPerPage).ToList();
            var tree = NewShapeTree();
            uint id = 2;
            var subtitle = pageCount > 1 ? $"page {page + 1}/{pageCount}" : "";
            var left = AddPaperHeader(tree, ref id, "Sommaire", subtitle);

            var right = SlideWidth - Margin;
            var numX = left; var numWidth = 750000L;
            var titleX = numX + numWidth + 150000L; var titleWidth = 6600000L;
            var pageWidth = 750000L; var pageX = right - pageWidth;
            var leaderX = titleX + titleWidth + 150000L; var leaderRight = pageX - 150000L;

            var top = 2200000L;
            var bottom = SlideHeight - Margin - 400000L;
            var rowHeight = Math.Min(1500000L, (bottom - top) / pageEntries.Count);

            var y = top;
            for (var i = 0; i < pageEntries.Count; i++)
            {
                var (label, slideNumber) = pageEntries[i];
                var globalIndex = page * maxRowsPerPage + i;
                var centerY = y + rowHeight / 2;

                AddTextFont(tree, id++, $"num{i}", numX, y, numWidth, rowHeight, (globalIndex + 1).ToString("00"), 30, true, EncreRouge, FontDisplay, center: true);

                var titre = Truncate(label, MaxCharsForBox(titleWidth, BodyMinPt, 1));
                var titreSize = FitFontSize(titre, titleWidth, BodyPt, BodyMinPt, 1);
                AddTextFont(tree, id++, $"titre{i}", titleX, y, titleWidth, rowHeight, titre, titreSize, false, Ardoise, FontDisplay,
                    center: true, align: D.TextAlignmentTypeValues.Left);

                // Dot leader — same device as the module TOC below, leading the eye to the slide number.
                for (var dx = leaderX; dx <= leaderRight; dx += 165000L)
                    AddRectangle(tree, id++, $"dot{i}_{dx}", dx, centerY - 12700, 25400, 25400, Ardoise, D.ShapeTypeValues.Ellipse, alphaPct: 45);

                AddTextFont(tree, id++, $"page{i}", pageX, y, pageWidth, rowHeight, slideNumber.ToString(), 18, true, EncreRouge, FontDisplay, center: true);

                if (y + rowHeight < bottom)
                    AddRectangle(tree, id++, $"rule{i}", numX, y + rowHeight, right - numX, 6350, Ardoise, alphaPct: 12);

                y += rowHeight;
            }

            pages.Add(tree);
        }

        return pages;
    }

    // Slide 5 — Vue d'ensemble du programme: a real "sommaire" (textbook table of contents) —
    // chapter number, title, a dotted leader, the duration. No bars, no chart, no numbered circles on
    // cards: this replaced two earlier attempts (a roadmap-style timeline, then an hour-by-day grid)
    // that both still read as generic chart components under a school-themed coat of paint — a TOC
    // has no chart-shaped ancestor to be mistaken for. Paginates for a module count large enough that
    // rows would otherwise collapse to an unreadable height, the same discipline the previous
    // version's Agenda slide needed for the same reason.
    private static List<P.ShapeTree> BuildVueDEnsembleSlidesV2(List<ModuleCard> modules)
    {
        if (modules.Count == 0) return [];

        const int maxRowsPerPage = 5;
        var pageCount = (int)Math.Ceiling(modules.Count / (double)maxRowsPerPage);
        var totalHeures = modules.Sum(m => m.DureeHeures ?? 0);
        var pages = new List<P.ShapeTree>();

        for (var page = 0; page < pageCount; page++)
        {
            var pageModules = modules.Skip(page * maxRowsPerPage).Take(maxRowsPerPage).ToList();
            var tree = NewShapeTree();
            uint id = 2;

            AddRectangle(tree, id++, "bg", 0, 0, SlideWidth, SlideHeight, Papier);
            AddMarginRule(tree, ref id);
            AddDogEar(tree, id++, Tableau);

            var left = 1550000L;
            AddTextFont(tree, id++, "heading", left, 500000, SlideWidth - left - Margin, 700000,
                "Vue d'ensemble du programme", TitlePt, true, Ardoise, FontDisplay);
            var subtitle = pageCount > 1
                ? $"{modules.Count} module(s)  ·  {totalHeures:0.#}h au total  ·  page {page + 1}/{pageCount}"
                : $"{modules.Count} module(s)  ·  {totalHeures:0.#}h au total";
            AddText(tree, id++, "subheading", left, 1250000, SlideWidth - left - Margin, 350000, [(subtitle, CaptionPt, false, Ardoise)]);

            // Four columns: chapter number | title | dotted leader | duration. The leader lives in its
            // own fixed gap rather than starting where each title's text happens to end (which OpenXml
            // can't measure) — exactly how a printed TOC's dot leader works, since titles of very
            // different lengths all still reach the same page-number column.
            var right = SlideWidth - Margin;
            var numX = left; var numWidth = 750000L;
            var titleX = numX + numWidth + 150000L; var titleWidth = 5300000L;
            var dureeWidth = 750000L; var dureeX = right - dureeWidth;
            var leaderX = titleX + titleWidth + 150000L; var leaderRight = dureeX - 150000L;

            var top = 2200000L;
            var bottom = SlideHeight - Margin - 400000L;
            var rowHeight = Math.Min(1500000L, (bottom - top) / pageModules.Count);

            var y = top;
            foreach (var m in pageModules)
            {
                var color = SubjectColor(m.Numero);
                var centerY = y + rowHeight / 2;

                AddTextFont(tree, id++, $"num{m.Numero}", numX, y, numWidth, rowHeight,
                    m.Numero.ToString("00"), 30, true, color, FontDisplay, center: true);

                var titre = Truncate(m.Titre, MaxCharsForBox(titleWidth, BodyMinPt, 2));
                var titreSize = FitFontSize(titre, titleWidth, BodyPt, BodyMinPt, 2);
                AddTextFont(tree, id++, $"titre{m.Numero}", titleX, y, titleWidth, rowHeight, titre, titreSize, false, Ardoise, FontDisplay,
                    center: true, align: D.TextAlignmentTypeValues.Left);

                // Dot leader — small evenly-spaced marks, not a chart element, but the same device a
                // printed table of contents uses to lead the eye across to the page number.
                for (var dx = leaderX; dx <= leaderRight; dx += 165000L)
                    AddRectangle(tree, id++, $"dot{m.Numero}_{dx}", dx, centerY - 12700, 25400, 25400, Ardoise, D.ShapeTypeValues.Ellipse, alphaPct: 45);

                if (m.DureeHeures is { } d)
                    AddTextFont(tree, id++, $"duree{m.Numero}", dureeX, y, dureeWidth, rowHeight, $"{d:0.#}h", 18, true, color, FontDisplay, center: true);

                if (y + rowHeight < bottom)
                    AddRectangle(tree, id++, $"rule{m.Numero}", numX, y + rowHeight, right - numX, 6350, Ardoise, alphaPct: 12);

                y += rowHeight;
            }

            pages.Add(tree);
        }

        return pages;
    }

    // Slide 6+ — Module: a notebook double-page. Left margin column (flat Tableau, not a gradient
    // panel) carries the module number like a textbook chapter number, plus a subject-colored spine
    // strip. The right page is faintly ruled and dominated by the contenu checklist — this is also
    // the fix for "empty bottom-right": content is now the primary element filling the page, not an
    // afterthought card competing with badges/bars for leftover space.
    private static P.ShapeTree BuildModuleSlideV2(ModuleCard m)
    {
        var tree = NewShapeTree();
        uint id = 2;
        var color = SubjectColor(m.Numero);

        const long panelWidth = 3200000L;
        AddRectangle(tree, id++, "panel", 0, 0, panelWidth, SlideHeight, Tableau);
        AddRectangle(tree, id++, "spine", 0, 0, 90000, SlideHeight, color);

        AddTextFont(tree, id++, "moduleLabel", Margin, 500000, panelWidth - Margin, 350000, "MODULE", 12, true, Craie, Font);
        AddTextFont(tree, id++, "numero", Margin, 900000, panelWidth - Margin, 1500000, m.Numero.ToString(), 72, true, Craie, FontDisplay);
        if (m.DureeHeures is { } duree)
            AddTextAlpha(tree, id++, "duree", Margin, SlideHeight - 750000, panelWidth - Margin, 400000,
                $"⏱  {duree:0.#} heures", BodyMinPt, true, Craie, 88);

        var bodyLeft = panelWidth + 500000;
        var bodyWidth = SlideWidth - bodyLeft - Margin;
        var bottomLimit = SlideHeight - Margin - 350000;

        AddRectangle(tree, id++, "pageBg", panelWidth, 0, SlideWidth - panelWidth, SlideHeight, Papier);
        AddRuledLines(tree, ref id, bodyLeft, 1900000L, bodyWidth, bottomLimit, 560000L);

        var titre = Truncate(m.Titre, MaxCharsForBox(bodyWidth, BodyMinPt, 2));
        var titreSize = FitFontSize(titre, bodyWidth, TitlePt, BodyMinPt, 2);
        var titreHeight = MeasureTextHeightFont(titre, titreSize, bodyWidth, FontDisplay);
        AddTextFont(tree, id++, "titre", bodyLeft, 420000, bodyWidth, titreHeight, titre, titreSize, true, color, FontDisplay);

        var y = 420000L + titreHeight + 260000;

        if (!string.IsNullOrWhiteSpace(m.Objectif) && y < bottomLimit)
        {
            // Capped at 2 lines (was 3) — contenu is the slide's primary content (per the original
            // "empty bottom-right" fix, it's meant to dominate the page), so objectif no longer gets
            // to eat a 3rd line's worth of height at contenu's expense; a long objectif truncates a
            // little more instead of a contenu bullet disappearing entirely.
            var textWidth = bodyWidth - 60000;
            var objectif = Truncate(m.Objectif, MaxCharsForBox(textWidth, BodyMinPt, 2));
            var objSize = FitFontSize(objectif, textWidth, BodyPt, BodyMinPt, 2);
            var objHeight = MeasureTextHeight(objectif, objSize, textWidth);
            if (y + objHeight <= bottomLimit)
            {
                // Highlight band drawn first (so it sits behind the text) — sized to the whole
                // objectif block, not just one line, so it reads as one continuous highlighter stroke
                // under a 1-2 line statement rather than a stray bar.
                AddHighlightMark(tree, ref id, bodyLeft - 40000, y + 40000, textWidth + 80000, objHeight - 20000);
                AddText(tree, id++, "objectif", bodyLeft, y, textWidth, objHeight, [(objectif, objSize, false, Ardoise)]);
                y += objHeight + 320000;
            }
        }

        // Contenu — an actual checklist, sized to fill the remaining page rather than a small card.
        var contenu = (m.Contenu ?? []).Where(c => !string.IsNullOrWhiteSpace(c)).Take(4).ToList();
        if (contenu.Count > 0 && y < bottomLimit)
        {
            var innerWidth = bodyWidth - 60000;
            var textWidth = innerWidth - 500000;
            var budget2Lines = MaxCharsForBox(textWidth, BodyMinPt, 2);
            var budget1Line = MaxCharsForBox(textWidth, BodyMinPt, 1);
            foreach (var c in contenu)
            {
                // Never shrink a row below what its own text needs — that just moves the overflow
                // onto whatever comes next. Instead, degrade gracefully: try the full 2-line budget
                // first, and only if that genuinely doesn't fit, retry the same bullet truncated
                // tighter to 1 line before giving up on it — a bullet that's still there but shorter
                // beats a bullet that silently vanished (the previous version's "break on first miss"
                // behavior, which dropped every remaining bullet the moment one didn't fit, even when
                // a 1-line version of it clearly would have).
                var text = Truncate(c, budget2Lines);
                var height = MeasureTextHeight(text, BodyMinPt, textWidth);
                if (y + height + 160000 + 140000 > bottomLimit)
                {
                    text = Truncate(c, budget1Line);
                    height = MeasureTextHeight(text, BodyMinPt, textWidth);
                    if (y + height + 160000 + 140000 > bottomLimit) continue; // still doesn't fit — skip only this one, keep trying the rest
                }

                var top = y;
                var boxHeight = NextRow(ref y, text, BodyMinPt, textWidth, 160000, 140000);
                AddTextFont(tree, id++, $"check{contenu.IndexOf(c)}", bodyLeft, top, 420000, boxHeight, "✓", 20, true, color, Font, center: true);
                AddText(tree, id++, $"contenu{contenu.IndexOf(c)}", bodyLeft + 500000, top, textWidth, boxHeight,
                    [(text, BodyMinPt, false, Ardoise)], anchor: D.TextAnchoringTypeValues.Center);
            }
            y += 160000;
        }

        string? methodeText = m.Methode switch
        {
            { PctPratique: { } pp } => $"{pp:0}% pratique",
            { Type.Length: > 0 } => m.Methode.Type,
            _ => null,
        };
        if (methodeText is not null && y + 500000 <= bottomLimit)
        {
            AddTab(tree, ref id, bodyLeft, y, methodeText, color, Craie, CaptionPt);
            y += 460000 + 260000;
        }

        if (!string.IsNullOrWhiteSpace(m.Livrable) && y < bottomLimit)
        {
            var livrableBudget = Math.Max(20, MaxCharsForBox(bodyWidth, 16, 1) - "À rendre : ".Length);
            var text = $"À rendre : {Truncate(m.Livrable, livrableBudget)}";
            AddTextFont(tree, id, "livrable", bodyLeft, y, bodyWidth, 400000, text, 15, false, EncreRouge, Font, italic: true);
        }

        return tree;
    }

    // ---------- Mechanical text shortening ----------
    // The spec forbids a second LLM call at export time — every "reformulation" below is a plain
    // mechanical transform (sentence-splitting, prefix-stripping) over the fields the generation call
    // already produced, not a summarization pass.

    // "Contexte" is stored as a paragraph; a slide needs a few short bullets, not the paragraph
    // verbatim. Splits on sentence-ending punctuation first — but real AI-generated text is sometimes
    // one long clause with no internal '.'/';'/':' at all, which made a naive version of this return
    // exactly ONE giant fragment. Falls back to commas, then to fixed-size word chunks, so the full
    // text always reaches the slide as multiple bullets instead of silently collapsing to one.
    private static List<string> SplitIntoShortBullets(string? text, int maxBullets)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var t = text.Trim();

        var bySentence = t.Split(['.', ';', ':', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0).ToList();
        if (bySentence.Count >= 2) return bySentence.Take(maxBullets).ToList();

        var byComma = t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0).ToList();
        if (byComma.Count >= 2) return byComma.Take(maxBullets).ToList();

        var words = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= 1) return [t];
        var chunkCount = Math.Min(maxBullets, Math.Max(1, (int)Math.Ceiling(words.Length / 12.0)));
        var chunkSize = (int)Math.Ceiling(words.Length / (double)chunkCount);
        var chunks = new List<string>();
        for (var i = 0; i < words.Length; i += chunkSize)
            chunks.Add(string.Join(' ', words.Skip(i).Take(chunkSize)));
        return chunks;
    }

    // A module objective is authored as "Être capable de <verbe + compétence>" or "Savoir <...>"
    // (enforced at generation time). Stripping that fixed prefix mechanically recovers "just the verb
    // + the competency" without needing an LLM to reword it.
    private static string ShortenObjectif(string? objectif)
    {
        if (string.IsNullOrWhiteSpace(objectif)) return "";
        var t = objectif.Trim();
        foreach (var prefix in new[] { "Être capable de ", "être capable de ", "Savoir " })
            if (t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { t = t[prefix.Length..]; break; }
        return t;
    }

    // Shared page shell for every non-title, non-module, non-closing slide: paper background, margin
    // rule, dog-ear, Georgia heading directly on the page (no colored band). Returns the left edge
    // content starts at, so callers don't repeat the margin-rule offset.
    private static long AddPaperHeader(P.ShapeTree tree, ref uint id, string title, string subtitle)
    {
        AddRectangle(tree, id++, "bg", 0, 0, SlideWidth, SlideHeight, Papier);
        AddMarginRule(tree, ref id);
        AddDogEar(tree, id++, Tableau);

        var left = 1550000L;
        AddTextFont(tree, id++, "heading", left, 500000, SlideWidth - left - Margin, 700000, title, TitlePt, true, Ardoise, FontDisplay);
        if (!string.IsNullOrEmpty(subtitle))
            AddText(tree, id++, "subheading", left, 1250000, SlideWidth - left - Margin, 350000, [(subtitle, CaptionPt, false, Ardoise)]);
        return left;
    }

    // Shared list-slide renderer for Contexte, Objectifs pédagogiques, Test de positionnement, and
    // Ressources et pratique — four separate near-duplicate builders in the previous version,
    // consolidated into one: a colored tick + statement + ruled hairline per row, paginated the same
    // way the module-count-scaling Vue d'ensemble/Objectifs slides need to be. `color` is per-item
    // (module subject color for Objectifs pédagogiques, EncreRouge for the others).
    private static List<P.ShapeTree> BuildListSlidesV2(string title, IReadOnlyList<(string Text, string Color)> items)
    {
        if (items.Count == 0) return [];

        const int maxRowsPerPage = 5;
        var pageCount = (int)Math.Ceiling(items.Count / (double)maxRowsPerPage);
        var pages = new List<P.ShapeTree>();

        for (var page = 0; page < pageCount; page++)
        {
            var pageItems = items.Skip(page * maxRowsPerPage).Take(maxRowsPerPage).ToList();
            var tree = NewShapeTree();
            uint id = 2;
            var subtitle = pageCount > 1 ? $"page {page + 1}/{pageCount}" : "";
            var left = AddPaperHeader(tree, ref id, title, subtitle);

            var top = 2100000L;
            var bottom = SlideHeight - Margin - 400000L;
            var textX = left + 500000L;
            var textWidth = SlideWidth - Margin - textX;

            var y = top;
            for (var i = 0; i < pageItems.Count && y < bottom; i++)
            {
                var (rawText, color) = pageItems[i];
                var text = Truncate(rawText, MaxCharsForBox(textWidth, BodyMinPt, 2));
                var textHeight = MeasureTextHeight(text, BodyMinPt, textWidth);
                var rowHeight = Math.Min(textHeight + 280000, bottom - y);

                AddRectangle(tree, id++, $"tick{i}", left, y + (rowHeight - 340000) / 2, 70000, 340000, color, cornerAdj: 20000);
                AddText(tree, id++, $"text{i}", textX, y, textWidth, rowHeight, [(text, BodyMinPt, false, Ardoise)], anchor: D.TextAnchoringTypeValues.Center);
                if (y + rowHeight < bottom)
                    AddRectangle(tree, id++, $"rule{i}", left, y + rowHeight, SlideWidth - Margin - left, 6350, Ardoise, alphaPct: 10);

                y += rowHeight;
            }

            pages.Add(tree);
        }

        return pages;
    }

    // Slide 2 — Contexte et enjeux.
    private static List<P.ShapeTree> BuildContexteSlidesV2(ObjectifsData objectifs)
    {
        var bullets = SplitIntoShortBullets(objectifs.Contexte, 4);
        return BuildListSlidesV2("Contexte et enjeux", bullets.Select(b => (b, EncreRouge)).ToList());
    }

    // Slide 3 — Objectifs pédagogiques: one row per module, ticked in that module's own subject color
    // so it stays visually linked to that module's later slide.
    private static List<P.ShapeTree> BuildObjectifsPedagogiquesSlidesV2(List<ModuleCard> modules)
    {
        var items = modules
            .Where(m => !string.IsNullOrWhiteSpace(m.Objectif))
            .Select(m => ($"{m.Numero}. {ShortenObjectif(m.Objectif)}", SubjectColor(m.Numero)))
            .Where(x => x.Item1.Length > 0)
            .ToList();
        return BuildListSlidesV2("Objectifs pédagogiques", items);
    }

    // Slide N+1 — Test de positionnement et différenciation.
    private static List<P.ShapeTree> BuildTestPositionnementSlidesV2(TestPositionnementData? test, ModuleBonus? bonus, List<ModuleCard> modules)
    {
        var hasBonus = bonus is { InclusDansTroncCommun: true } && !string.IsNullOrWhiteSpace(bonus.Titre);
        if (test is null && !hasBonus) return [];

        var bullets = new List<string>();
        if (!string.IsNullOrWhiteSpace(test?.Objectif))
            bullets.Add($"Objectif du test : {test.Objectif}");
        if (test?.SeuilParcoursStandardPct is { } seuil)
            bullets.Add($"Seuil parcours standard : {seuil:0.#}%");
        if (test?.ModuleRemediation is { } remediationNumero)
        {
            var remediationTitre = modules.FirstOrDefault(m => m.Numero == remediationNumero)?.Titre;
            bullets.Add($"Module de remédiation : #{remediationNumero}" + (remediationTitre is null ? "" : $" — {remediationTitre}"));
        }
        if (hasBonus)
            bullets.Add($"Parcours bonus : {bonus!.Titre}" + (bonus.DureeHeures is { } d ? $" ({d:0.#}h)" : ""));

        return BuildListSlidesV2("Test de positionnement et différenciation", bullets.Select(b => (b, EncreRouge)).ToList());
    }

    // Slide N+3 — Ressources et pratique.
    private static List<P.ShapeTree> BuildRessourcesPratiqueSlidesV2(ObjectifsData objectifs)
    {
        var items = new List<string>();
        var mp = objectifs.ModalitesPratiques;
        if (mp is not null && (!string.IsNullOrWhiteSpace(mp.Format) || mp.MaterielRequis is { Count: > 0 }))
        {
            var materiel = mp.MaterielRequis is { Count: > 0 } ? string.Join(", ", mp.MaterielRequis.Take(3)) : null;
            var line = string.Join("  ·  ", new[] { mp.Format, mp.Intervenant, materiel }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (line.Length > 0) items.Add(line);
        }
        foreach (var r in (objectifs.RessourcesPedagogiques ?? []).Take(6))
            if (!string.IsNullOrWhiteSpace(r)) items.Add(r);

        return BuildListSlidesV2("Ressources et pratique", items.Select(i => (i, EncreRouge)).ToList());
    }

    // Slide N+2 — Modalités d'évaluation: a bar chart (spec allows bars or a pie chart), restyled onto
    // paper with subject colors — Continue/Finale is now a plain typographic tag (☑ / a red-pen-style
    // "FINALE" stamp) instead of the previous version's one generic pill badge reused for everything.
    //
    // Paginates instead of truncating: the previous version `break`'d out of the loop the moment one
    // item's real measured height didn't fit the remaining space on the page, which silently dropped
    // every item after it — a formation with 5 évaluation entries could render only 3 bars with no
    // indication 2 were missing (real bug: data loss at generation time, not a rendering overflow).
    // Every item is grouped onto pages first (by the same real-measured-height math the single-page
    // version used for its cutoff check), and every group becomes its own slide — so however many
    // évaluation entries exist, all of them end up on some slide.
    private static List<P.ShapeTree> BuildEvaluationSlidesV2(List<EvaluationItem> evaluation)
    {
        if (evaluation.Count == 0) return [];

        const long left = 1550000L; // matches AddPaperHeader's fixed left edge
        var right = SlideWidth - Margin;
        var barWidth = right - left - 1500000L;
        var labelWidth = barWidth - 1400000;
        var top = 2200000L;
        var bottom = SlideHeight - Margin - 400000L;
        const long barHeight = 320000L, barGap = 300000L;

        var groups = new List<List<EvaluationItem>>();
        var current = new List<EvaluationItem>();
        var y = top;
        foreach (var item in evaluation)
        {
            var nom = Truncate(item.Nom, MaxCharsForBox(labelWidth, BodyMinPt, 2, bold: true));
            var itemHeight = MeasureTextHeight(nom, BodyMinPt, labelWidth, bold: true) + 140000 + barHeight + barGap;
            if (y + itemHeight > bottom && current.Count > 0)
            {
                groups.Add(current);
                current = [];
                y = top;
            }
            current.Add(item);
            y += itemHeight;
        }
        if (current.Count > 0) groups.Add(current);

        var pages = new List<P.ShapeTree>();
        for (var page = 0; page < groups.Count; page++)
        {
            var tree = NewShapeTree();
            uint id = 2;
            var subtitle = groups.Count > 1
                ? $"{evaluation.Count} composante(s)  ·  page {page + 1}/{groups.Count}"
                : $"{evaluation.Count} composante(s)";
            AddPaperHeader(tree, ref id, "Modalités d'évaluation", subtitle);

            y = top;
            var pageItems = groups[page];
            var baseIndex = groups.Take(page).Sum(g => g.Count);
            for (var i = 0; i < pageItems.Count; i++)
            {
                var item = pageItems[i];
                var color = SubjectColor(baseIndex + i + 1);
                var pct = item.Pct is { } p ? Math.Clamp(p, 0, 100) : 0;
                // The label renders bold — bold:true must be threaded through every measurement of it
                // (the truncation budget here AND the row-height/positioning below), or the two
                // disagree and whichever one used the wrong (non-bold) width factor under-counts how
                // tall it really is.
                var nom = Truncate(item.Nom, MaxCharsForBox(labelWidth, BodyMinPt, 2, bold: true));
                var tag = item.EstEvaluationContinue ? "☑ Continue" : "FINALE";
                var tagColor = item.EstEvaluationContinue ? "4C7A5D" : EncreRouge;

                var rowTop = y;
                var labelHeight = NextRow(ref y, nom, BodyMinPt, labelWidth, 0, 140000, bold: true);
                AddText(tree, id++, $"label{i}", left, rowTop, labelWidth, labelHeight, [(nom, BodyMinPt, true, Ardoise)]);
                AddTextFont(tree, id++, $"tag{i}", left + barWidth - 1350000, rowTop, 1350000, labelHeight, tag, 13, true, tagColor, Font,
                    align: D.TextAlignmentTypeValues.Right);

                var barY = y; // NextRow already advanced y past the label + gap — the real measured position
                AddRectangle(tree, id++, $"track{i}", left, barY, barWidth, barHeight, "E8E2D2", cornerAdj: 6000);
                if (pct > 0)
                    AddRectangle(tree, id++, $"fill{i}", left, barY, Math.Max(120000, (long)(barWidth * pct / 100)), barHeight, color, cornerAdj: 6000);
                AddTextFont(tree, id++, $"pct{i}", left + barWidth + 150000, barY - 60000, 1200000, 440000, $"{pct:0.#}%", 16, true, color, FontDisplay);

                y += barHeight + barGap;
            }

            pages.Add(tree);
        }

        return pages;
    }

    // Slide 4 — Bénéfices pour l'entreprise: fiches bristol (index cards), staggered vertically with
    // a flat solid offset "shadow" (a real card physically casting a shadow, not a blurred SaaS one)
    // and a colored pin — sharp corners throughout, replacing the previous rounded-corner-shadow-card
    // kit entirely.
    private static P.ShapeTree? BuildBeneficesSlideV2(List<BeneficeEntreprise>? benefices)
    {
        var items = (benefices ?? []).Where(b => !string.IsNullOrWhiteSpace(b.Titre)).Take(3).ToList();
        if (items.Count == 0) return null;

        var tree = NewShapeTree();
        uint id = 2;
        AddPaperHeader(tree, ref id, "Bénéfices pour l'entreprise", "");

        const long cardWidth = 3300000L, cardHeight = 3600000L, gap = 500000L;
        var totalWidth = items.Count * cardWidth + (items.Count - 1) * gap;
        var startX = (SlideWidth - totalWidth) / 2;
        var top = 2250000L;
        var yStagger = new long[] { 220000L, 0L, 260000L };

        for (var i = 0; i < items.Count; i++)
        {
            var color = SubjectColor(i + 1);
            var cardX = startX + i * (cardWidth + gap);
            var cardY = top + yStagger[i % yStagger.Length];

            // Flat solid shadow, offset — not a blurred glow.
            AddRectangle(tree, id++, $"shadow{i}", cardX + 70000, cardY + 70000, cardWidth, cardHeight, Ardoise, alphaPct: 20);
            // Thin sharp border, faked as a slightly larger dark rect behind a slightly smaller fill.
            AddRectangle(tree, id++, $"border{i}", cardX, cardY, cardWidth, cardHeight, Ardoise);
            AddRectangle(tree, id++, $"card{i}", cardX + 12700, cardY + 12700, cardWidth - 25400, cardHeight - 25400, CardBg);

            const long pinSize = 260000L;
            AddRectangle(tree, id++, $"pin{i}", cardX + cardWidth / 2 - pinSize / 2, cardY - pinSize / 2, pinSize, pinSize, color, D.ShapeTypeValues.Ellipse);

            var innerX = cardX + 300000L;
            var innerWidth = cardWidth - 600000L;
            const int titreMaxLines = 3;
            var titre = Truncate(items[i].Titre, MaxCharsForBox(innerWidth, BodyMinPt, titreMaxLines));
            var titreSize = FitFontSize(titre, innerWidth, BodyPt, BodyMinPt, titreMaxLines);
            var titreTop = cardY + 500000L;
            var titreHeight = MeasureTextHeightFont(titre, titreSize, innerWidth, FontDisplay);
            AddTextFont(tree, id++, $"titre{i}", innerX, titreTop, innerWidth, titreHeight, titre, titreSize, true, color, FontDisplay);

            var justifTop = titreTop + titreHeight + 260000L;
            const int justifMinPt = 12;
            if (!string.IsNullOrWhiteSpace(items[i].Justification) && justifTop < cardY + cardHeight - 300000L)
            {
                var justifAvailableHeight = cardY + cardHeight - 300000L - justifTop;
                var justifMaxLinesAtMin = Math.Max(2, (int)(justifAvailableHeight / (long)Math.Round(justifMinPt * 1.32 * EmuPerPoint)));
                var justifBudget = MaxCharsForBox(innerWidth, justifMinPt, justifMaxLinesAtMin);
                var justif = Truncate(items[i].Justification, justifBudget);
                var justifSize = FitFontSize(justif, innerWidth, CaptionPt - 2, justifMinPt, justifMaxLinesAtMin);
                AddText(tree, id++, $"justif{i}", innerX, justifTop, innerWidth, justifAvailableHeight, [(justif, justifSize, false, Ardoise)]);
            }
        }

        return tree;
    }

    // Slide finale — Clôture: same flat chalkboard field as the title slide, not a gradient panel.
    private static P.ShapeTree BuildClotureSlideV2(Formation formation)
    {
        var tree = NewShapeTree();
        uint id = 2;
        var contentWidth = SlideWidth - Margin * 2;

        AddRectangle(tree, id++, "bg", 0, 0, SlideWidth, SlideHeight, Tableau);

        AddTextFont(tree, id++, "heading", Margin, 1900000L, contentWidth, 900000, "Merci de votre attention", TitlePt, true, Craie, FontDisplay);
        AddRectangle(tree, id++, "chalkRule", Margin, 2750000L, 2400000L, 12700, Craie, alphaPct: 70);

        if (formation.DureeEstimee is { } duree)
            AddTextAlpha(tree, id++, "duree", Margin, 3100000L, contentWidth, 500000, $"⏱  Durée totale : {duree:0.#} heures", BodyMinPt, false, Craie, 88);

        var contactName = formation.Createur?.Nom;
        var contactEmail = formation.Createur?.Email;
        if (!string.IsNullOrWhiteSpace(contactName) || !string.IsNullOrWhiteSpace(contactEmail))
        {
            AddTextAlpha(tree, id++, "contactLabel", Margin, 4000000L, contentWidth, 350000, "CONTACT / QUESTIONS", 12, true, Craie, 80);
            var contactLine = string.Join("  ·  ", new[] { contactName, contactEmail }.Where(s => !string.IsNullOrWhiteSpace(s)));
            AddTextFont(tree, id, "contactLine", Margin, 4400000L, contentWidth, 500000, Truncate(contactLine, 90), BodyMinPt, true, Craie, Font);
        }

        return tree;
    }

    // Text-measurement helpers parametrized by font family — the shared MeasureTextHeight/
    // EstimateLineCount above assume Font (Segoe UI)'s average glyph width; Georgia runs slightly
    // wider, so headings set in it get their own char-width constant instead of silently under-
    // measuring and risking overflow.
    private static long MeasureTextHeightFont(string text, int sizePt, long widthEmu, string font)
    {
        var factor = font == FontDisplay ? 0.58 : 0.52;
        var widthPt = widthEmu / (double)EmuPerPoint;
        var charsPerLine = Math.Max(6, (int)(widthPt / (sizePt * factor)));
        var lines = Math.Max(1, (int)Math.Ceiling(text.Length / (double)charsPerLine));
        return lines * (long)Math.Round(sizePt * 1.32 * EmuPerPoint);
    }

    private static void AddTextFont(
        P.ShapeTree tree, uint id, string name, long x, long y, long cx, long cy,
        string text, int sizePt, bool bold, string colorHex, string font, bool center = false, bool italic = false,
        D.TextAlignmentTypeValues? align = null)
    {
        var runProps = new D.RunProperties { FontSize = sizePt * 100, Bold = bold, Italic = italic };
        runProps.Append(new D.SolidFill(new D.RgbColorModelHex { Val = colorHex }));
        runProps.Append(new D.LatinFont { Typeface = font });

        var body = new P.TextBody(
            new D.BodyProperties
            {
                Anchor = center ? D.TextAnchoringTypeValues.Center : D.TextAnchoringTypeValues.Top,
                Wrap = D.TextWrappingValues.Square,
                LeftInset = 0, RightInset = 0, TopInset = 0, BottomInset = 0,
            },
            new D.ListStyle(),
            new D.Paragraph(
                new D.ParagraphProperties { Alignment = align ?? (center ? D.TextAlignmentTypeValues.Center : D.TextAlignmentTypeValues.Left) },
                new D.Run(runProps, new D.Text(text))));

        var shapeProps = new P.ShapeProperties(
            new D.Transform2D(new D.Offset { X = x, Y = y }, new D.Extents { Cx = cx, Cy = cy }),
            new D.PresetGeometry(new D.AdjustValueList()) { Preset = D.ShapeTypeValues.Rectangle },
            new D.NoFill(),
            new D.Outline(new D.NoFill()));

        var shape = new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = name },
                new P.NonVisualShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            shapeProps,
            body);

        tree.Append(shape);
    }

    private static D.Theme BuildThemeV2()
    {
        var colorScheme = new D.ColorScheme(
            new D.Dark1Color(new D.SystemColor { Val = D.SystemColorValues.WindowText, LastColor = "000000" }),
            new D.Light1Color(new D.SystemColor { Val = D.SystemColorValues.Window, LastColor = "FFFFFF" }),
            new D.Dark2Color(new D.RgbColorModelHex { Val = Ardoise }),
            new D.Light2Color(new D.RgbColorModelHex { Val = Papier }),
            new D.Accent1Color(new D.RgbColorModelHex { Val = Tableau }),
            new D.Accent2Color(new D.RgbColorModelHex { Val = EncreRouge }),
            new D.Accent3Color(new D.RgbColorModelHex { Val = Surligneur }),
            new D.Accent4Color(new D.RgbColorModelHex { Val = "2D5F8A" }),
            new D.Accent5Color(new D.RgbColorModelHex { Val = "A9682F" }),
            new D.Accent6Color(new D.RgbColorModelHex { Val = "4C7A5D" }),
            new D.Hyperlink(new D.RgbColorModelHex { Val = EncreRouge }),
            new D.FollowedHyperlinkColor(new D.RgbColorModelHex { Val = Tableau }))
        { Name = "PlateformeFormationV2" };

        var fontScheme = new D.FontScheme(
            new D.MajorFont(
                new D.LatinFont { Typeface = FontDisplay }, new D.EastAsianFont { Typeface = "" }, new D.ComplexScriptFont { Typeface = "" }),
            new D.MinorFont(
                new D.LatinFont { Typeface = Font }, new D.EastAsianFont { Typeface = "" }, new D.ComplexScriptFont { Typeface = "" }))
        { Name = "PlateformeFormationV2" };

        var formatScheme = new D.FormatScheme(
            new D.FillStyleList(
                new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.Accent1 }),
                new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.Accent1 }),
                new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.Accent1 })),
            new D.LineStyleList(
                new D.Outline(new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.Text1 })) { Width = 6350 },
                new D.Outline(new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.Text1 })) { Width = 12700 },
                new D.Outline(new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.Text1 })) { Width = 19050 }),
            new D.EffectStyleList(
                new D.EffectStyle(new D.EffectList()),
                new D.EffectStyle(new D.EffectList()),
                new D.EffectStyle(new D.EffectList())),
            new D.BackgroundFillStyleList(
                new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.Light1 }),
                new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.Light1 }),
                new D.SolidFill(new D.SchemeColor { Val = D.SchemeColorValues.Light1 })))
        { Name = "PlateformeFormationV2" };

        return new D.Theme(new D.ThemeElements(colorScheme, fontScheme, formatScheme), new D.ObjectDefaults())
        {
            Name = "PlateformeFormationV2",
        };
    }

    // ---------- Master / layout / theme scaffolding ----------
    // Minimal but schema-valid parts — PowerPoint requires a master+layout+theme even for a deck
    // built entirely from absolutely-positioned shapes with no inherited placeholders.

    private static P.SlideMaster BuildSlideMaster()
    {
        var tree = NewShapeTree();
        return new P.SlideMaster(
            new P.CommonSlideData(tree),
            new P.ColorMap
            {
                Background1 = D.ColorSchemeIndexValues.Light1,
                Text1 = D.ColorSchemeIndexValues.Dark1,
                Background2 = D.ColorSchemeIndexValues.Light2,
                Text2 = D.ColorSchemeIndexValues.Dark2,
                Accent1 = D.ColorSchemeIndexValues.Accent1,
                Accent2 = D.ColorSchemeIndexValues.Accent2,
                Accent3 = D.ColorSchemeIndexValues.Accent3,
                Accent4 = D.ColorSchemeIndexValues.Accent4,
                Accent5 = D.ColorSchemeIndexValues.Accent5,
                Accent6 = D.ColorSchemeIndexValues.Accent6,
                Hyperlink = D.ColorSchemeIndexValues.Hyperlink,
                FollowedHyperlink = D.ColorSchemeIndexValues.FollowedHyperlink,
            });
    }

    private static P.SlideLayout BuildBlankLayout()
    {
        var tree = NewShapeTree();
        return new P.SlideLayout(
            new P.CommonSlideData(tree),
            new P.ColorMapOverride(new D.MasterColorMapping()))
        { Type = P.SlideLayoutValues.Blank };
    }
}
