using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Tests.ThemeSlots;

public class ReadersRoundTripTests
{
    private static readonly ThemeImageDecision[] LogoAndStamp = [new(ThemeImageRole.Logo, true), new(ThemeImageRole.Signature, false)];

    [Fact]
    public void Fixed_notes_written_are_read_back_unchanged()
    {
        var theme = OverridesBlockText.RemoveAll(ThemeFixtures.Load(ThemeFixtures.TestTheme));
        FixedNoteInput[] notes =
        [
            new(FixedNotePosition.Before, "Bir\n\n  İki", true, true, true, 12, "#1A2B3C", "times", FixedNoteAlign.Center),
            new(FixedNotePosition.After, "Son", false, false, false, 11, "#000000", "theme", FixedNoteAlign.Right),
        ];

        var written = FixedNoteWriter.Apply(theme, notes, DocumentType.Invoice);
        Assert.True(written.Applied, written.Error);

        var read = FixedNoteReader.Read(written.Xslt, DocumentType.Invoice);
        Assert.Equal(notes, read.OrderBy(n => n.Position));
    }

    [Fact]
    public async Task Composed_bank_rows_and_hidden_kinds_are_inspected_back()
    {
        var marked = (await ThemeFixtures.Analyzer().AnalyzeAsync(ThemeFixtures.CompanyImagesTheme, DocumentType.Invoice, [],
            LogoAndStamp, Guid.Empty, XsltRoute.Theme, CancellationToken.None)).Xslt;
        BankInfoInput[] banks = [new("Ziraat Bankası", "TR00 0001 0000 0000 0000 0000 01"), new("A & B <Bank>", "TR11")];

        var composed = new ThemeAssetComposer(new ThemeImageValidator()).Compose(marked,
            new ThemeComposeInput([], banks, HideThemeImages: [ThemeAssetKind.Logo]));
        Assert.True(composed.Success, composed.Error);

        var inspection = new ThemeOverridesProcessor(new ThemeImageValidator()).Inspect(composed.Xslt);
        Assert.True(inspection.Recognized);
        Assert.Equal(banks, inspection.Bank);
        Assert.Equal([ThemeAssetKind.Logo], inspection.Hide);
    }

    [Fact]
    public async Task Prior_image_roles_are_read_from_marked_output()
    {
        var source = ThemeFixtures.CompanyImagesTheme;
        var marked = (await ThemeFixtures.Analyzer().AnalyzeAsync(source, DocumentType.Invoice, [],
            LogoAndStamp, Guid.Empty, XsltRoute.Theme, CancellationToken.None)).Xslt;

        var prior = PriorImageRoleReader.Read(marked, DocumentType.Invoice);

        Assert.Equal(ThemeFixtures.Analyzer().DetectImages(marked, DocumentType.Invoice), prior.Detected);
        Assert.Equal([ThemeImageRole.Logo, ThemeImageRole.Signature], prior.Roles);
        Assert.All(PriorImageRoleReader.Read(source, DocumentType.Invoice).Roles, Assert.Null);
    }
}
