using XsltCraft.Application.DTO;
using XsltCraft.Application.EasyEdit;
using XsltCraft.Application.EasyEdit.Banks;
using XsltCraft.Application.Tests.ThemeSlots;
using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Catalog;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.Validation;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Application.Xslt;

namespace XsltCraft.Application.Tests.EasyEdit;

/// <summary>Analiz → compose → yeniden analiz: belge banka düzenlemesi diğer düzenlemelerle aynı istekte.</summary>
public class DocumentBankComposeTests
{
    private const string Iban1 = "TR66 0006 7010 0000 0020 1934 37";
    private const string Iban2 = "TR330006100519786457841326";
    private const string Iban3 = "TR120001000000000000000001";

    private static readonly IThemeAssetComposer Composer = new ThemeAssetComposer(new ThemeImageValidator());

    // Banka tablosu kaşe görselinden önce: düzenleme sonraki adımların metnini kaydırır.
    private static readonly string BankTheme = ThemeFixtures.CompanyImagesTheme.Replace(
        "<!-- Alıcı - kaşe",
        $"""<table id="banks"><tr><th>Banka</th><th>IBAN</th></tr><tr><td>Ziraat Bankası</td><td>{Iban1}</td></tr><tr><td>Halkbank</td><td>{Iban2}</td></tr></table><!-- Alıcı - kaşe""",
        StringComparison.Ordinal);

    private static Task<EasyEditAnalysis> Analyze(string xslt) =>
        new UploadedXsltAnalyzer(new DefaultXsltSafetyScanner(), new ThemeOverridesProcessor(new ThemeImageValidator()),
                new ThemeSlotAnalyzer(ThemeFixtures.Execution, ThemeFixtures.Samples))
            .AnalyzeAsync(xslt, null, Guid.NewGuid(), CancellationToken.None);

    private static FixedNoteInput Note(string text) =>
        new(FixedNotePosition.Before, text, false, false, false, 11, "#000000", "theme", FixedNoteAlign.Left);

    [Fact]
    public async Task Analyze_ReturnsDocumentBank_AndMapsToResponse()
    {
        var analysis = await Analyze(BankTheme);

        var bank = Assert.IsType<DocumentBankInfo>(analysis.DocumentBank);
        Assert.True(bank.Found);
        Assert.True(bank.CanAdd);
        Assert.Equal(["Ziraat Bankası", "Halkbank"], bank.Rows.Select(r => r.BankName));

        var dto = EasyEditResponseMapper.ToResponse(analysis, new ThemeSlotProjector()).DocumentBank!;
        Assert.True(dto.Found);
        Assert.Equal(new EasyEditDocumentBankRowDto(1, "Halkbank", Iban2, true, true, bank.Rows[1].Line), dto.Rows[1]);
    }

    [Fact]
    public async Task Analyze_UnusableVerdict_NoDocumentBank()
    {
        var analysis = await Analyze(BankTheme.Replace("</body>", "</bdy>", StringComparison.Ordinal));

        Assert.Null(analysis.DocumentBank);
        Assert.Null(EasyEditResponseMapper.ToResponse(analysis, new ThemeSlotProjector()).DocumentBank);
    }

    [Fact]
    public async Task Compose_BankEditsWithStaticImagesAndFixedNotes_IndexesStayAligned()
    {
        var analysis = await Analyze(BankTheme);
        var xslt = analysis.Xslt!;
        Assert.Equal(2, analysis.Images.Count);
        var stamp = analysis.Images[^1];
        Assert.True(analysis.NotesSupported);

        var result = Composer.Compose(xslt, new ThemeComposeInput([], [],
            FixedNotes: [Note("Ödeme 30 gün içinde")],
            DocumentType: analysis.DocumentType,
            StaticImages: [new StaticImageInput(stamp.Index, null, true, null, null, null)],
            DocumentBankEdits:
            [
                new DocumentBankEditInput(0, "", "", true),
                new DocumentBankEditInput(1, "Halk Bankası", Iban2, false),
                new DocumentBankEditInput(null, "Vakıf <&> Katılım", Iban3, false),
            ]));

        Assert.True(result.Success, result.Error);
        Assert.Empty(result.Warnings);
        Assert.Null(XsltSafety.FindThreat(result.Xslt));
        SecureXslt.Compile(result.Xslt);

        var again = await Analyze(result.Xslt);
        Assert.Equal(EasyEditVerdict.Suitable, again.Verdict);
        var bank = again.DocumentBank!;
        Assert.Equal(["Halk Bankası", "Vakıf <&> Katılım"], bank.Rows.Select(r => r.BankName));
        Assert.Equal([Iban2, Iban3], bank.Rows.Select(r => r.Iban));
        // Silinen görsel kaşe (son görsel); logo yerinde.
        Assert.Equal(analysis.Images[0].PreviewDataUrl, Assert.Single(again.Images).PreviewDataUrl);
        Assert.Equal(analysis.Images[0].Line, again.Images[0].Line);
        Assert.Equal("Ödeme 30 gün içinde", Assert.Single(again.Notes).Text);
    }

    [Fact]
    public async Task Compose_AllRowsRemoved_TableRemoved()
    {
        var xslt = (await Analyze(BankTheme)).Xslt!;

        var result = Composer.Compose(xslt, new ThemeComposeInput([], [],
            DocumentBankEdits: [new DocumentBankEditInput(0, "", "", true), new DocumentBankEditInput(1, "", "", true)]));

        Assert.True(result.Success, result.Error);
        Assert.DoesNotContain("id=\"banks\"", result.Xslt, StringComparison.Ordinal);
        Assert.False((await Analyze(result.Xslt)).DocumentBank!.Found);
    }

    [Fact]
    public void Compose_NoDocumentBank_WarnsAndKeepsText()
    {
        var xslt = ThemeFixtures.CompanyImagesTheme;

        var result = Composer.Compose(xslt, new ThemeComposeInput([], [],
            DocumentBankEdits: [new DocumentBankEditInput(null, "Ziraat", Iban1, false)]));

        Assert.True(result.Success, result.Error);
        Assert.Same(xslt, result.Xslt);
        Assert.Equal(DocumentBankEditor.NotFound, Assert.Single(result.Warnings).Code);
    }
}
