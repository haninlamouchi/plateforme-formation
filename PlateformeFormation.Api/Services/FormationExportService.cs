using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PlateformeFormation.Api.Helpers;
using PlateformeFormation.Api.Models;
using static PlateformeFormation.Api.Helpers.FormationContentParser;

namespace PlateformeFormation.Api.Services;

public class FormationExportService : IFormationExportService
{
    private const double Margin = 40;
    private static readonly XColor PrimaryColor = XColor.FromArgb(155, 17, 30);
    private static readonly XColor MutedColor = XColor.FromArgb(110, 110, 110);
    private static readonly XColor TintBg = XColor.FromArgb(248, 246, 240);

    private static readonly XColor[] Palette =
    [
        XColor.FromArgb(155, 17, 30),
        XColor.FromArgb(37, 99, 235),
        XColor.FromArgb(124, 58, 237),
        XColor.FromArgb(5, 150, 105),
        XColor.FromArgb(217, 119, 6),
        XColor.FromArgb(219, 39, 119),
        XColor.FromArgb(8, 145, 178),
    ];

    private readonly XFont _titleFont = new("Arial", 22, XFontStyle.Bold);
    private readonly XFont _mutedFont = new("Arial", 9, XFontStyle.Regular);
    private readonly XFont _headingFont = new("Arial", 13, XFontStyle.Bold);
    private readonly XFont _bodyFont = new("Arial", 10.5, XFontStyle.Regular);
    private readonly XFont _smallFont = new("Arial", 9.5, XFontStyle.Regular);
    private readonly XFont _boldBodyFont = new("Arial", 10.5, XFontStyle.Bold);
    private readonly XFont _numberFont = new("Arial", 11, XFontStyle.Bold);

    // Module-card typography — a module title reads as its own heading (bigger than a generic
    // _boldBodyFont label), and every subsection ("Objectif", "Contenu", "Livrable"...) gets a small
    // uppercase label sitting above its content instead of an inline bold prefix, so the eye can jump
    // straight to the section it's looking for instead of reading every line to find it.
    private readonly XFont _moduleTitleFont = new("Arial", 14, XFontStyle.Bold);
    private readonly XFont _labelFont = new("Arial", 8, XFontStyle.Bold);

    private PdfDocument _doc = null!;
    private PdfPage _page = null!;
    private XGraphics _gfx = null!;
    private double _y;
    private double _pageWidth;
    private double _pageHeight;

    private readonly IFormationQualityService _quality;

    public FormationExportService(IFormationQualityService quality)
    {
        _quality = quality;
    }

    public byte[] GeneratePdf(Formation formation)
    {
        _doc = new PdfDocument();
        var objectifs = ParseObject<ObjectifsData>(formation.Objectifs)
            ?? new ObjectifsData(null, null, null, null, null, null, null, null, null, null, null, null);
        var modules = ParseCards<ModuleCard>(formation.Modules,
            () => new ModuleCard(0, formation.Modules, null, null, null, null, null, null, null, null, null, null, null))
            .OrderBy(m => m.Numero).ToList();

        var activites = ParseCards<ActivitePedagogique>(formation.Activites,
            () => new ActivitePedagogique(formation.Activites, null, null));

        DrawCoverPage(formation, objectifs);
        NewPage();

        DrawHeading("Programme de formation");
        DrawSeparator();
        _y += 16;

        DrawQualityBox(_quality.Evaluate(formation));

        // Sections below follow the fixed 13-part order (contexte -> ... -> sources) — the layout is
        // never reinterpreted per generation, only the content changes.
        DrawContexte(objectifs);
        DrawModalitesPratiques(objectifs.ModalitesPratiques);
        DrawAIssueDeLaFormation(modules);
        DrawBeneficesEntreprise(objectifs.BeneficesEntreprise);
        DrawTestPositionnement(objectifs.TestPositionnement, modules);

        if (modules.Count > 0)
        {
            DrawHeading("Vue d'ensemble du programme");
            _y += 4;
            DrawModuleOverviewTable(modules, objectifs.ModuleBonus);
            _y += 6;

            DrawPlanningTable(modules, objectifs.ModuleBonus);

            DrawHeading("Programme détaillé");
            _y += 4;
            foreach (var m in modules) DrawModuleCard(m);
            _y += 6;
        }

        DrawModuleBonus(objectifs.ModuleBonus);

        if (activites.Count > 0)
        {
            DrawHeading("Activités pédagogiques");
            _y += 4;
            foreach (var a in activites) DrawActivite(a);
            _y += 6;
        }

        var evaluation = ParseCards<EvaluationItem>(formation.MethodesEvaluation,
            () => new EvaluationItem(formation.MethodesEvaluation, null, false));
        if (evaluation.Count > 0)
        {
            DrawHeading("Modalités d'évaluation");
            _y += 4;
            for (var i = 0; i < evaluation.Count; i++) DrawEvaluationCard(i, evaluation[i]);
            _y += 6;
        }

        if (objectifs.RessourcesPedagogiques is { Count: > 0 })
        {
            DrawHeading("Ressources pédagogiques");
            _y += 4;
            foreach (var r in objectifs.RessourcesPedagogiques) DrawBullet(r);
            _y += 8;
        }

        DrawSourcesTransparence(objectifs, formation);

        using var stream = new MemoryStream();
        _doc.Save(stream, false);
        return stream.ToArray();
    }

    private void NewPage()
    {
        _page = _doc.AddPage();
        _page.Size = PdfSharpCore.PageSize.A4;
        _gfx = XGraphics.FromPdfPage(_page);
        _pageWidth = _page.Width.Point;
        _pageHeight = _page.Height.Point;
        _y = Margin;
    }

    private double ContentWidth => _pageWidth - 2 * Margin;

    private void EnsureSpace(double needed)
    {
        if (_y + needed > _pageHeight - Margin)
            NewPage();
    }

    private void DrawSeparator()
    {
        EnsureSpace(4);
        _gfx.DrawLine(new XPen(XColor.FromArgb(225, 225, 225), 1), Margin, _y, _pageWidth - Margin, _y);
    }

    // Standalone title page — titre/sous-titre/durée/public/prérequis at a glance (spec part 1),
    // instead of making the reader wait for page 2 to know what they're looking at.
    private void DrawCoverPage(Formation formation, ObjectifsData objectifs)
    {
        NewPage();
        var centerY = _pageHeight / 2 - 60;

        _gfx.DrawLine(new XPen(PrimaryColor, 2), Margin, centerY - 30, Margin + 60, centerY - 30);

        var titleLines = WrapText(formation.Titre, _titleFont, ContentWidth);
        var ty = centerY;
        foreach (var line in titleLines)
        {
            _gfx.DrawString(line, _titleFont, new XSolidBrush(PrimaryColor), Margin, ty);
            ty += 28;
        }

        if (!string.IsNullOrWhiteSpace(objectifs.SousTitre))
        {
            ty += 6;
            foreach (var line in WrapText(objectifs.SousTitre, _headingFont, ContentWidth))
            {
                _gfx.DrawString(line, _headingFont, new XSolidBrush(MutedColor), Margin, ty);
                ty += 18;
            }
        }

        var facts = new List<(string Label, string Value)>();
        if (formation.DureeEstimee is { } duree) facts.Add(("Durée", $"{duree:0.#} heures"));
        if (!string.IsNullOrWhiteSpace(objectifs.PublicCible)) facts.Add(("Public cible", objectifs.PublicCible));
        if (!string.IsNullOrWhiteSpace(objectifs.PrerequisDetailles)) facts.Add(("Prérequis", objectifs.PrerequisDetailles));

        if (facts.Count > 0)
        {
            ty += 20;
            foreach (var (label, value) in facts)
            {
                _gfx.DrawString($"{label.ToUpperInvariant()}", _mutedFont, new XSolidBrush(MutedColor), Margin, ty);
                ty += _smallFont.Height + 2;
                foreach (var line in WrapText(value, _bodyFont, ContentWidth))
                {
                    _gfx.DrawString(line, _bodyFont, XBrushes.Black, Margin, ty);
                    ty += _bodyFont.Height + 2;
                }
                ty += 8;
            }
        }

        var footer = $"Programme de formation professionnelle  ·  {DateTime.Now:dd/MM/yyyy}";
        _gfx.DrawString(footer, _mutedFont, new XSolidBrush(MutedColor), Margin, _pageHeight - Margin);
    }

    private void DrawHeading(string text)
    {
        EnsureSpace(22);
        _gfx.DrawString(text, _headingFont, new XSolidBrush(PrimaryColor), Margin, _y + 14);
        _y += 22;
    }

    private void DrawKeyValue(string label, string value)
    {
        EnsureSpace(_bodyFont.Height + 4);
        var labelText = $"{label} : ";
        _gfx.DrawString(labelText, _boldBodyFont, XBrushes.Black, Margin, _y + _bodyFont.Height);
        var labelWidth = _gfx.MeasureString(labelText, _boldBodyFont).Width;
        foreach (var line in WrapText(value, _bodyFont, ContentWidth - labelWidth))
        {
            _gfx.DrawString(line, _bodyFont, XBrushes.Black, Margin + labelWidth, _y + _bodyFont.Height);
            _y += _bodyFont.Height + 2;
        }
    }

    // Same checks FormationQualityService verifies in code — surfaced here so the score isn't only
    // visible on-screen. Only lists what actually FAILED.
    private void DrawQualityBox(FormationQualityReport report)
    {
        var failed = report.Checks.Where(c => c.Statut != "OK").ToList();
        var headerText = $"Contrôle qualité pédagogique — score {report.Score}/100";
        var boxHeight = 10 + _bodyFont.Height + failed.Count * (_smallFont.Height + 3) + 6;

        EnsureSpace(boxHeight);
        var top = _y;

        _gfx.DrawRectangle(new XSolidBrush(TintBg), Margin, top, ContentWidth, boxHeight);
        _gfx.DrawString(headerText, _boldBodyFont, new XSolidBrush(PrimaryColor), Margin + 10, top + _bodyFont.Height + 4);

        var ly = top + _bodyFont.Height + 8;
        foreach (var c in failed)
        {
            ly += _smallFont.Height + 3;
            // Plain ASCII — the PDF's embedded Arial subset doesn't include the ✗ glyph, which
            // rendered as an empty box.
            var marker = c.Statut == "ECHEC" ? "X" : "!";
            _gfx.DrawString($"{marker} {c.Libelle}", _smallFont, new XSolidBrush(MutedColor), Margin + 14, ly);
        }

        _y = top + boxHeight + 10;
    }

    private void DrawContexte(ObjectifsData objectifs)
    {
        if (string.IsNullOrWhiteSpace(objectifs.Contexte)) return;
        DrawHeading("Contexte");
        _y += 2;
        foreach (var line in WrapText(objectifs.Contexte, _bodyFont, ContentWidth))
        {
            EnsureSpace(_bodyFont.Height + 2);
            _gfx.DrawString(line, _bodyFont, XBrushes.Black, Margin, _y + _bodyFont.Height);
            _y += _bodyFont.Height + 2;
        }
        _y += 8;
    }

    private void DrawModalitesPratiques(ModalitesPratiques? modalites)
    {
        if (modalites is null) return;
        var hasAny = !string.IsNullOrWhiteSpace(modalites.Format) || !string.IsNullOrWhiteSpace(modalites.Intervenant)
            || modalites.MaterielRequis is { Count: > 0 };
        if (!hasAny) return;

        DrawHeading("Modalités pratiques");
        _y += 2;
        if (!string.IsNullOrWhiteSpace(modalites.Format)) DrawKeyValue("Format", modalites.Format);
        if (!string.IsNullOrWhiteSpace(modalites.Intervenant)) DrawKeyValue("Intervenant", modalites.Intervenant);
        if (modalites.MaterielRequis is { Count: > 0 }) DrawKeyValue("Matériel requis", string.Join(", ", modalites.MaterielRequis));
        _y += 8;
    }

    // Derived from modules[].objectif rather than a separate LLM-generated field — one source of
    // truth for what's taught, never able to drift from the actual module content.
    private void DrawAIssueDeLaFormation(List<ModuleCard> modules)
    {
        var objectifs = modules.Where(m => !string.IsNullOrWhiteSpace(m.Objectif)).Select(m => m.Objectif!).ToList();
        if (objectifs.Count == 0) return;

        DrawHeading("À l'issue de la formation, le participant sera capable de");
        _y += 2;
        foreach (var o in objectifs) DrawBullet(o);
        _y += 8;
    }

    private void DrawBeneficesEntreprise(List<BeneficeEntreprise>? benefices)
    {
        if (benefices is not { Count: > 0 }) return;
        DrawHeading("Bénéfices pour l'entreprise");
        _y += 2;
        foreach (var b in benefices)
        {
            if (string.IsNullOrWhiteSpace(b.Titre)) continue;
            var text = string.IsNullOrWhiteSpace(b.Justification) ? b.Titre : $"{b.Titre} — {b.Justification}";
            DrawBullet(text);
        }
        _y += 8;
    }

    private void DrawActivite(ActivitePedagogique activite)
    {
        if (string.IsNullOrWhiteSpace(activite.Nom)) return;
        var text = string.IsNullOrWhiteSpace(activite.Format) ? activite.Nom : $"{activite.Nom} ({activite.Format})";
        DrawBullet(text);
        if (!string.IsNullOrWhiteSpace(activite.Description))
        {
            var lines = WrapText(activite.Description, _smallFont, ContentWidth - 14);
            foreach (var line in lines)
            {
                EnsureSpace(_smallFont.Height + 1);
                _gfx.DrawString(line, _smallFont, new XSolidBrush(MutedColor), Margin + 14, _y + _smallFont.Height);
                _y += _smallFont.Height + 1;
            }
            _y += 2;
        }
    }

    // Final section: RAG transparency (what actually grounded the content, what the context couldn't
    // cover), plus documents_sources and the real traceability list — kept last per the fixed layout.
    private void DrawSourcesTransparence(ObjectifsData objectifs, Formation formation)
    {
        var hasSources = objectifs.SourcesUtilisees is { Count: > 0 };
        var hasLacunes = objectifs.LacunesContexte is { Count: > 0 };
        var hasDocSources = objectifs.DocumentsSources is { Count: > 0 };
        var hasTraceability = formation.FormationDocuments.Count > 0;
        if (!hasSources && !hasLacunes && !hasDocSources && !hasTraceability) return;

        DrawHeading("Sources et transparence documentaire");
        _y += 2;
        if (hasSources)
        {
            DrawKeyValue("Sources utilisées", string.Join(", ", objectifs.SourcesUtilisees!));
            _y += 2;
        }
        if (hasDocSources)
        {
            DrawKeyValue("Documents sources", string.Join(", ", objectifs.DocumentsSources!));
            _y += 2;
        }
        if (hasLacunes)
        {
            foreach (var lacune in objectifs.LacunesContexte!)
                DrawBullet($"Lacune du contexte : {lacune}");
        }
        if (hasTraceability)
        {
            foreach (var fd in formation.FormationDocuments.OrderByDescending(x => x.ScorePertinence))
                DrawBullet(fd.Document?.Titre ?? "");
        }
        _y += 8;
    }

    // Quick-glance table (# / titre / durée) before the detailed module cards. The optional bonus
    // module only appears here when it's part of the core track — an optional bonus stays out of the
    // overview entirely, consistent with it never counting toward duree_totale_heures (R1/R9).
    private void DrawModuleOverviewTable(List<ModuleCard> modules, ModuleBonus? bonus)
    {
        const double numWidth = 24;
        const double dureeWidth = 50;
        var titreWidth = ContentWidth - numWidth - dureeWidth;

        void DrawRow(string numero, string? titre, double? duree, bool shaded)
        {
            var titreLines = WrapText(titre ?? "", _bodyFont, titreWidth - 8);
            var rowHeight = Math.Max(1, titreLines.Count) * (_bodyFont.Height + 1) + 6;
            EnsureSpace(rowHeight);

            var top = _y;
            if (shaded) _gfx.DrawRectangle(new XSolidBrush(TintBg), Margin, top, ContentWidth, rowHeight);
            _gfx.DrawString(numero, _boldBodyFont, new XSolidBrush(MutedColor), Margin + 6, top + _bodyFont.Height);

            var ly = top;
            foreach (var line in titreLines)
            {
                _gfx.DrawString(line, _bodyFont, XBrushes.Black, Margin + numWidth, ly + _bodyFont.Height);
                ly += _bodyFont.Height + 1;
            }

            if (duree is { } d)
                _gfx.DrawString($"{d:0.#}h", _mutedFont, new XSolidBrush(MutedColor),
                    Margin + ContentWidth - dureeWidth + 6, top + _bodyFont.Height);

            _y = top + rowHeight;
        }

        foreach (var m in modules) DrawRow(m.Numero.ToString(), m.Titre, m.DureeHeures, m.Numero % 2 == 0);

        if (bonus is { InclusDansTroncCommun: true } && !string.IsNullOrWhiteSpace(bonus.Titre))
            DrawRow("B", bonus.Titre, bonus.DureeHeures, modules.Count % 2 == 0);
    }

    // Day-by-day breakdown — computed from real module durations (FormationPlanner), never generated
    // by the model, so it can never disconnect from the modules it describes.
    private void DrawPlanningTable(List<ModuleCard> modules, ModuleBonus? bonus)
    {
        var jours = FormationPlanner.ComputeJours(modules, bonus);
        if (jours.Count == 0) return;

        var titresParNumero = modules.ToDictionary(m => m.Numero, m => m.Titre ?? $"Module {m.Numero}");
        if (bonus is { InclusDansTroncCommun: true })
        {
            var bonusNumero = modules.Count > 0 ? modules.Max(m => m.Numero) + 1 : 1;
            titresParNumero[bonusNumero] = bonus.Titre ?? "Module bonus";
        }

        DrawHeading("Planning");
        _y += 2;

        var jourWidth = 70.0;
        var dureeWidth = 60.0;
        var contenuWidth = ContentWidth - jourWidth - dureeWidth;

        foreach (var jour in jours)
        {
            var contenu = string.Join(", ", jour.ModuleNumeros.Select(n =>
            {
                var titre = titresParNumero.GetValueOrDefault(n, $"Module {n}");
                return jour.ModulesEnSuite.Contains(n) ? $"{titre} (suite)" : titre;
            }));
            var contenuLines = WrapText(contenu, _bodyFont, contenuWidth - 8);
            var rowHeight = Math.Max(1, contenuLines.Count) * (_bodyFont.Height + 1) + 8;
            EnsureSpace(rowHeight);

            var top = _y;
            _gfx.DrawRectangle(new XSolidBrush(TintBg), Margin, top, ContentWidth, rowHeight);
            _gfx.DrawString(jour.Jour, _boldBodyFont, new XSolidBrush(PrimaryColor), Margin + 6, top + _bodyFont.Height);

            var cy = top + _bodyFont.Height;
            foreach (var line in contenuLines)
            {
                _gfx.DrawString(line, _bodyFont, XBrushes.Black, Margin + jourWidth, cy);
                cy += _bodyFont.Height + 1;
            }

            _gfx.DrawString($"{jour.DureeHeures:0.#}h", _mutedFont, new XSolidBrush(MutedColor),
                Margin + ContentWidth - dureeWidth + 6, top + _bodyFont.Height);

            _y = top + rowHeight + 2;
        }

        _y += 8;
    }

    // Colored numbered badge + title/duration, then one labeled section per field (Objectif, Contenu,
    // Méthode, Exercice formatif, Livrable, Grille d'évaluation, ...), each visually separated —
    // replacing the previous version's single dense block where every field ran into the next with
    // just an inline bold prefix and no whitespace between them. A colored spine in the page margin
    // (drawn first, sized to the card's full height) marks where each module starts/ends when flipping
    // through the printed document, extending the same "one color per module" identity the numbered
    // circle already carried alone.
    private void DrawModuleCard(ModuleCard m)
    {
        var color = Palette[(m.Numero - 1 + Palette.Length) % Palette.Length];
        const double circleD = 26;
        const double spineWidth = 4;
        const double sectionGap = 12; // whitespace between sections — the main "respirant" fix
        var spineX = Margin - 14; // into the margin, not overlapping the circle/text column
        var textX = Margin + circleD + 14;
        var textWidth = ContentWidth - circleD - 14;

        var titre = m.Titre ?? "";
        var dureeText = m.DureeHeures is { } d ? $"({d:0.#}h)" : "";
        var dureeWidth = dureeText.Length > 0 ? _gfx.MeasureString(dureeText, _mutedFont).Width : 0;
        var titreLines = WrapText(titre, _moduleTitleFont, textWidth - dureeWidth - 8);
        var headerHeight = Math.Max(circleD, titreLines.Count * (_moduleTitleFont.Height + 2));

        // Every field becomes its own (height, draw-at-y) block — computed once up front (so the total
        // card height is known for EnsureSpace/pagination before anything is drawn), then rendered in
        // sequence with `sectionGap` between each. This is what replaced the old approach of just
        // accumulating a running line count across every field with no distinction between them.
        var blocks = new List<(double Height, Action<double> Draw)>();

        if (!string.IsNullOrWhiteSpace(m.Objectif))
            blocks.Add(BuildLabeledTextBlock("Objectif", m.Objectif!, textX, textWidth, color, _bodyFont));

        if (m.Contenu is { Count: > 0 })
        {
            var items = m.Contenu.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
            if (items.Count > 0) blocks.Add(BuildBulletListBlock("Contenu", items, textX, textWidth, color));
        }

        var methodeText = string.Join("  ·  ", new[]
        {
            m.Methode?.Type,
            m.Methode?.PctTheorie is { } pt && m.Methode?.PctPratique is { } pp ? $"{pt:0}% théorie / {pp:0}% pratique" : null,
        }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (methodeText.Length > 0)
            blocks.Add(BuildLabeledTextBlock("Méthode pédagogique", methodeText, textX, textWidth, color, _smallFont));

        if (m.ExerciceFormatif is { Consigne.Length: > 0 } ex)
        {
            var exerciceText = string.IsNullOrWhiteSpace(ex.Type) ? ex.Consigne! : $"{ex.Type} : {ex.Consigne}";
            blocks.Add(BuildLabeledTextBlock("Exercice formatif", exerciceText, textX, textWidth, color, _smallFont));
        }

        var livrableText = string.IsNullOrWhiteSpace(m.Livrable) ? ""
            : m.ReutiliseLivrableModule is { } reuse
                ? $"{m.Livrable} (poursuit le livrable du module {reuse})"
                : m.Livrable;
        if (livrableText.Length > 0)
            blocks.Add(BuildLabeledTextBlock("Livrable", livrableText, textX, textWidth, color, _bodyFont));

        if (m.GrilleEvaluation is { Count: > 0 } grille)
        {
            var items = grille.Where(g => !string.IsNullOrWhiteSpace(g.Critere)).ToList();
            if (items.Count > 0) blocks.Add(BuildGrilleBlock(items, textX, textWidth, color));
        }

        if (m.CompetencesPrerequises is { Count: > 0 } prereq)
            blocks.Add(BuildLabeledTextBlock("S'appuie sur", $"Modules {string.Join(", ", prereq)}", textX, textWidth, color, _smallFont));

        if (m.Sources is { Count: > 0 } sources)
            blocks.Add(BuildLabeledTextBlock("Source", sources[0].DocumentTitre, textX, textWidth, color, _smallFont));

        var notes = m.NotesFormateur?.Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? [];
        if (notes.Count > 0)
            blocks.Add(BuildNotesBlock(notes, textX, textWidth));

        var blocksHeight = blocks.Sum(b => b.Height) + Math.Max(0, blocks.Count - 1) * sectionGap;
        var contentHeight = Math.Max(headerHeight, circleD) + (blocks.Count > 0 ? 10 + blocksHeight : 0);
        EnsureSpace(contentHeight + 16);

        var top = _y;

        // Spine drawn first (behind nothing else sits here) — sized to the card's real content height,
        // computed the same way the card itself is laid out below, so it never runs short or long.
        _gfx.DrawRectangle(new XSolidBrush(color), spineX, top, spineWidth, contentHeight);

        _gfx.DrawEllipse(new XSolidBrush(color), Margin, top, circleD, circleD);
        var numStr = m.Numero.ToString();
        var numSize = _gfx.MeasureString(numStr, _numberFont);
        _gfx.DrawString(numStr, _numberFont, XBrushes.White,
            Margin + (circleD - numSize.Width) / 2, top + circleD / 2 + numSize.Height / 2 - 2);

        var localY = top;
        for (var i = 0; i < titreLines.Count; i++)
        {
            _gfx.DrawString(titreLines[i], _moduleTitleFont, new XSolidBrush(color), textX, localY + _moduleTitleFont.Height);
            if (i == 0 && dureeText.Length > 0)
                _gfx.DrawString(dureeText, _mutedFont, new XSolidBrush(MutedColor),
                    Margin + ContentWidth - dureeWidth, localY + _moduleTitleFont.Height);
            localY += _moduleTitleFont.Height + 2;
        }
        localY = Math.Max(localY, top + circleD);

        if (blocks.Count > 0)
        {
            localY += 10;
            for (var i = 0; i < blocks.Count; i++)
            {
                blocks[i].Draw(localY);
                localY += blocks[i].Height;
                if (i < blocks.Count - 1) localY += sectionGap;
            }
        }

        _y = top + contentHeight + 14;
    }

    // A small uppercase accent-colored label sitting above its content (never an inline bold prefix)
    // + the body text in plain black — color is the label only, never the paragraph itself, per the
    // "texte coloré sur plusieurs lignes est fatigant à lire" note. Reused by every module field that
    // isn't a bullet list or a boxed aside (Objectif, Méthode, Exercice formatif, Livrable, ...).
    private (double Height, Action<double> Draw) BuildLabeledTextBlock(
        string label, string body, double x, double width, XColor accent, XFont font)
    {
        var lines = WrapText(body, font, width);
        var labelHeight = _labelFont.Height + 4;
        var height = labelHeight + lines.Count * (font.Height + 2);

        void Draw(double top)
        {
            _gfx.DrawString(label.ToUpperInvariant(), _labelFont, new XSolidBrush(accent), x, top + _labelFont.Height);
            var ly = top + labelHeight;
            foreach (var line in lines)
            {
                _gfx.DrawString(line, font, XBrushes.Black, x, ly + font.Height);
                ly += font.Height + 2;
            }
        }

        return (height, Draw);
    }

    // The main fix: "contenu" as a real vertical bullet list (one item per line/wrap, a gap between
    // items) instead of a single "•"-joined paragraph rendered full-width in solid module color. Color
    // now lives only on the small square bullet mark, never on the text itself.
    private (double Height, Action<double> Draw) BuildBulletListBlock(
        string label, List<string> items, double x, double width, XColor accent)
    {
        const double bulletIndent = 14, bulletGap = 5, bulletSize = 5;
        var labelHeight = _labelFont.Height + 4;
        var itemLines = items.Select(i => WrapText(i, _bodyFont, width - bulletIndent)).ToList();
        var bodyHeight = itemLines.Sum(l => l.Count * (_bodyFont.Height + 2)) + Math.Max(0, items.Count - 1) * bulletGap;
        var height = labelHeight + bodyHeight;

        void Draw(double top)
        {
            _gfx.DrawString(label.ToUpperInvariant(), _labelFont, new XSolidBrush(accent), x, top + _labelFont.Height);
            var ly = top + labelHeight;
            for (var idx = 0; idx < itemLines.Count; idx++)
            {
                _gfx.DrawRectangle(new XSolidBrush(accent), x, ly + _bodyFont.Height / 2 - bulletSize / 2, bulletSize, bulletSize);
                foreach (var line in itemLines[idx])
                {
                    _gfx.DrawString(line, _bodyFont, XBrushes.Black, x + bulletIndent, ly + _bodyFont.Height);
                    ly += _bodyFont.Height + 2;
                }
                if (idx < itemLines.Count - 1) ly += bulletGap;
            }
        }

        return (height, Draw);
    }

    // "Grille d'évaluation" as a boxed mini-table (critère left, % right-aligned) instead of a
    // "•"-joined inline list — the same "boxed = visually distinct aside" treatment the "Notes
    // formateur" box already used, extended per the brief rather than staying a one-off exception.
    private (double Height, Action<double> Draw) BuildGrilleBlock(
        List<GrilleEvaluationItem> items, double x, double width, XColor accent)
    {
        const double padX = 10, padTop = 8, padBottom = 10, rowGap = 3;
        var headerHeight = _labelFont.Height + 6;

        var rows = items.Select(g =>
        {
            var pctText = g.Pct is { } p ? $"{p:0.#}%" : "";
            var pctWidth = pctText.Length > 0 ? _gfx.MeasureString(pctText, _boldBodyFont).Width + 10 : 0;
            return (Lines: WrapText(g.Critere!, _smallFont, width - padX * 2 - pctWidth), PctText: pctText);
        }).ToList();
        var rowsHeight = rows.Sum(r => r.Lines.Count * (_smallFont.Height + 2)) + Math.Max(0, rows.Count - 1) * rowGap;
        var boxHeight = padTop + headerHeight + rowsHeight + padBottom;

        void Draw(double top)
        {
            _gfx.DrawRectangle(new XSolidBrush(TintBg), x, top, width, boxHeight);
            _gfx.DrawString("GRILLE D'ÉVALUATION", _labelFont, new XSolidBrush(accent), x + padX, top + padTop + _labelFont.Height);
            var ly = top + padTop + headerHeight;
            foreach (var (lines, pctText) in rows)
            {
                for (var li = 0; li < lines.Count; li++)
                {
                    _gfx.DrawString(lines[li], _smallFont, XBrushes.Black, x + padX, ly + _smallFont.Height);
                    if (li == 0 && pctText.Length > 0)
                        _gfx.DrawString(pctText, _boldBodyFont, new XSolidBrush(accent),
                            x + width - padX - _gfx.MeasureString(pctText, _boldBodyFont).Width, ly + _smallFont.Height);
                    ly += _smallFont.Height + 2;
                }
                ly += rowGap;
            }
        }

        return (boxHeight, Draw);
    }

    // Notes formateur — unchanged bordered-box treatment (spec: keep this pattern), just recast as a
    // block so it composes with the rest of the module card's flow instead of being bolted on after.
    private (double Height, Action<double> Draw) BuildNotesBlock(List<string> notes, double x, double width)
    {
        const double padX = 8, padTop = 8, padBottom = 8;
        var headerHeight = _labelFont.Height + 6;
        var notesLines = notes.Select(n => WrapText($"– {n}", _smallFont, width - padX * 2 - 6)).ToList();
        var bodyHeight = notesLines.Sum(l => l.Count * (_smallFont.Height + 2));
        var boxHeight = padTop + headerHeight + bodyHeight + padBottom;

        void Draw(double top)
        {
            _gfx.DrawRectangle(new XPen(XColor.FromArgb(220, 210, 190), 1), x, top, width, boxHeight);
            _gfx.DrawString("NOTES FORMATEUR", _labelFont, new XSolidBrush(PrimaryColor), x + padX, top + padTop + _labelFont.Height);
            var ly = top + padTop + headerHeight;
            foreach (var lines in notesLines)
                foreach (var line in lines)
                {
                    _gfx.DrawString(line, _smallFont, XBrushes.Black, x + padX + 6, ly + _smallFont.Height);
                    ly += _smallFont.Height + 2;
                }
        }

        return (boxHeight, Draw);
    }

    private void DrawModuleBonus(ModuleBonus? bonus)
    {
        if (bonus is null || string.IsNullOrWhiteSpace(bonus.Titre)) return;

        DrawHeading(bonus.InclusDansTroncCommun ? "Module bonus (inclus au tronc commun)" : "Module bonus (optionnel)");
        _y += 2;
        DrawKeyValue("Titre", bonus.Titre);
        if (bonus.DureeHeures is { } d) DrawKeyValue("Durée", $"{d:0.#}h");
        if (bonus.Contenu is { Count: > 0 }) DrawKeyValue("Contenu", string.Join(", ", bonus.Contenu));
        _y += 8;
    }

    private void DrawTestPositionnement(TestPositionnementData? test, List<ModuleCard> modules)
    {
        if (test is null) return;
        var hasAny = !string.IsNullOrWhiteSpace(test.Objectif) || !string.IsNullOrWhiteSpace(test.Exercice)
            || test.SeuilParcoursStandardPct is not null || test.ModuleRemediation is not null;
        if (!hasAny) return;

        DrawHeading("Test de positionnement");
        _y += 2;
        if (!string.IsNullOrWhiteSpace(test.Objectif)) DrawKeyValue("Objectif", test.Objectif);
        if (test.QcmQuestions is { } q) DrawKeyValue("QCM diagnostique", $"{q} question(s)");
        if (!string.IsNullOrWhiteSpace(test.Exercice)) DrawKeyValue("Exercice", test.Exercice);
        if (test.SeuilParcoursStandardPct is { } s) DrawKeyValue("Seuil parcours standard", $"{s:0.#}%");
        if (test.ModuleRemediation is { } mr)
        {
            var titre = modules.FirstOrDefault(m => m.Numero == mr)?.Titre ?? $"module {mr}";
            DrawKeyValue("Module de remédiation", $"{mr} — {titre}");
        }
        _y += 8;
    }

    // Compact card for évaluation: colored square + nom + pondération/continue tag.
    private void DrawEvaluationCard(int index, EvaluationItem item)
    {
        var color = Palette[index % Palette.Length];
        const double box = 8;
        const double textX = Margin + 16;

        EnsureSpace(_bodyFont.Height + 8);
        var top = _y;
        _gfx.DrawRectangle(new XSolidBrush(color), Margin, top + 3, box, box);

        var titreText = SanitizeText(item.Nom ?? "");
        _gfx.DrawString(titreText, _boldBodyFont, XBrushes.Black, textX, top + _bodyFont.Height);

        var tag = item.Pct is { } p ? $"{p:0.#}%" : null;
        if (item.EstEvaluationContinue) tag = tag is null ? "Continue" : $"{tag} · Continue";
        if (tag is not null)
        {
            var titreWidth = _gfx.MeasureString(titreText, _boldBodyFont).Width;
            _gfx.DrawString($"  ·  {tag}", _mutedFont, new XSolidBrush(color), textX + titreWidth, top + _bodyFont.Height);
        }

        _y = top + _bodyFont.Height + 8;
    }

    private void DrawBullet(string text)
    {
        const double indent = 14;
        var lines = WrapText(text, _bodyFont, ContentWidth - indent);
        var first = true;
        foreach (var line in lines)
        {
            EnsureSpace(_bodyFont.Height + 2);
            if (first)
            {
                _gfx.DrawString("•", _bodyFont, XBrushes.Black, Margin, _y + _bodyFont.Height);
                first = false;
            }
            _gfx.DrawString(line, _bodyFont, XBrushes.Black, Margin + indent, _y + _bodyFont.Height);
            _y += _bodyFont.Height + 2;
        }
    }

    // A few already-generated formations have U+2011 (non-breaking hyphen) in their stored content —
    // FormationContentParser normalizes it at ingestion for anything generated from now on, but this
    // covers content already in the DB: the PDF's embedded Arial subset doesn't cover that code point,
    // rendering it as a blank box ("pré‑production" -> "pré□production"). No visual meaning is lost by
    // normalizing to a plain hyphen in this plain-text context.
    private static string SanitizeText(string text) => text.Replace('‑', '-');

    private List<string> WrapText(string text, XFont font, double maxWidth)
    {
        text = SanitizeText(text);
        var result = new List<string>();
        foreach (var paragraph in text.Split('\n'))
        {
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) { result.Add(""); continue; }

            var line = "";
            foreach (var word in words)
            {
                var candidate = line.Length == 0 ? word : $"{line} {word}";
                if (_gfx.MeasureString(candidate, font).Width > maxWidth && line.Length > 0)
                {
                    result.Add(line);
                    line = word;
                }
                else
                {
                    line = candidate;
                }
            }
            if (line.Length > 0) result.Add(line);
        }
        return result;
    }
}
