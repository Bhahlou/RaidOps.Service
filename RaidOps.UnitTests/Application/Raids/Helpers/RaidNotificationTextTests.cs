using FluentAssertions;
using RaidOps.Application.Implementations.Raids.Helpers;
using RaidOps.Domain.Enums;

namespace RaidOps.UnitTests.Application.Raids.Helpers;

public class RaidNotificationTextTests
{
    // ── GetTitleAndColor ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(GuildNotificationEventType.RaidPublished, "en", "Raid published")]
    [InlineData(GuildNotificationEventType.RaidPublished, "fr", "Raid publié")]
    [InlineData(GuildNotificationEventType.RaidPublished, "de", "Raid veröffentlicht")]
    [InlineData(GuildNotificationEventType.RaidCancelled, "en", "Raid cancelled")]
    [InlineData(GuildNotificationEventType.RaidCancelled, "fr", "Raid annulé")]
    [InlineData(GuildNotificationEventType.RaidCancelled, "de", "Raid abgesagt")]
    [InlineData(GuildNotificationEventType.RaidRescheduled, "en", "Raid rescheduled")]
    [InlineData(GuildNotificationEventType.RaidSlotAssigned, "fr", "Personnage ajouté")]
    [InlineData(GuildNotificationEventType.RaidSlotUnassigned, "de", "Charakter entfernt")]
    [InlineData(GuildNotificationEventType.RaidSlotsSwapped, "en", "Characters swapped")]
    [InlineData(GuildNotificationEventType.RaidSlotSpecChanged, "fr", "Spécialisation changée")]
    public void GetTitleAndColor_SupportedLanguage_ReturnsLocalizedTitle(GuildNotificationEventType eventType, string language, string expectedTitle)
    {
        var (title, _) = RaidNotificationText.GetTitleAndColor(eventType, language);

        title.Should().Be(expectedTitle);
    }

    [Fact]
    public void GetTitleAndColor_UnsupportedLanguage_FallsBackToEnglish()
    {
        var (title, color) = RaidNotificationText.GetTitleAndColor(GuildNotificationEventType.RaidPublished, "es");

        title.Should().Be("Raid published");
        color.Should().Be(0x57F287);
    }

    // ── Descriptions ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("en", "<@1> published **Split 1**.")]
    [InlineData("fr", "<@1> a publié **Split 1**.")]
    [InlineData("de", "<@1> hat **Split 1** veröffentlicht.")]
    public void GetPublishedDescription_LocalizesRequesterMentionAndEventName(string language, string expected)
    {
        RaidNotificationText.GetPublishedDescription("1", "Split 1", language).Should().Be(expected);
    }

    [Theory]
    [InlineData("en", "<@1> cancelled **Split 1**.")]
    [InlineData("fr", "<@1> a annulé **Split 1**.")]
    [InlineData("de", "<@1> hat **Split 1** abgesagt.")]
    public void GetCancelledDescription_LocalizesRequesterMentionAndEventName(string language, string expected)
    {
        RaidNotificationText.GetCancelledDescription("1", "Split 1", language).Should().Be(expected);
    }

    [Theory]
    [InlineData("en", "<@1> rescheduled **Split 1**: old → new.")]
    [InlineData("fr", "<@1> a reprogrammé **Split 1** : old → new.")]
    [InlineData("de", "<@1> hat **Split 1** verschoben: old → new.")]
    public void GetRescheduledDescription_LocalizesOldAndNewTime(string language, string expected)
    {
        RaidNotificationText.GetRescheduledDescription("1", "Split 1", "old", "new", language).Should().Be(expected);
    }

    [Theory]
    [InlineData("en", "<@1> assigned X (group 2, slot 3) in **Split 1**.")]
    [InlineData("fr", "<@1> a assigné X (groupe 2, slot 3) dans **Split 1**.")]
    [InlineData("de", "<@1> hat X (Gruppe 2, Slot 3) in **Split 1** zugewiesen.")]
    public void GetSlotAssignedDescription_LocalizesGroupAndSlotWording(string language, string expected)
    {
        RaidNotificationText.GetSlotAssignedDescription("1", "Split 1", "X", 2, 3, language).Should().Be(expected);
    }

    [Theory]
    [InlineData("en", "<@1> unassigned X (group 2, slot 3) from **Split 1**.")]
    [InlineData("fr", "<@1> a désassigné X (groupe 2, slot 3) de **Split 1**.")]
    [InlineData("de", "<@1> hat X (Gruppe 2, Slot 3) aus **Split 1** entfernt.")]
    public void GetSlotUnassignedDescription_LocalizesGroupAndSlotWording(string language, string expected)
    {
        RaidNotificationText.GetSlotUnassignedDescription("1", "Split 1", "X", 2, 3, language).Should().Be(expected);
    }

    [Theory]
    [InlineData("en", "<@1> swapped A and B in **Split 1**.")]
    [InlineData("fr", "<@1> a échangé A et B dans **Split 1**.")]
    [InlineData("de", "<@1> hat A und B in **Split 1** getauscht.")]
    public void GetSlotsSwappedDescription_LocalizesBothCharacters(string language, string expected)
    {
        RaidNotificationText.GetSlotsSwappedDescription("1", "Split 1", "A", "B", language).Should().Be(expected);
    }

    [Theory]
    [InlineData("en", "<@1> changed X's spec from OLD to NEW in **Split 1**.")]
    [InlineData("fr", "<@1> a changé la spécialisation de X de OLD vers NEW dans **Split 1**.")]
    [InlineData("de", "<@1> hat die Skillung von X in **Split 1** von OLD zu NEW geändert.")]
    public void GetSlotSpecChangedDescription_LocalizesOldAndNewSpec(string language, string expected)
    {
        RaidNotificationText.GetSlotSpecChangedDescription("1", "Split 1", "X", "OLD", "NEW", language).Should().Be(expected);
    }

    [Fact]
    public void GetPublishedDescription_UnsupportedLanguage_FallsBackToEnglish()
    {
        RaidNotificationText.GetPublishedDescription("1", "Split 1", "es").Should().Be("<@1> published **Split 1**.");
    }
}
