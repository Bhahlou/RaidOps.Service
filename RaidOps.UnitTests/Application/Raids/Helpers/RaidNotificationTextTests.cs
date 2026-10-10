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

    // ── GetTitleAndColor (composition announcement) ─────────────────────────────

    [Theory]
    [InlineData("en", "Current composition")]
    [InlineData("fr", "Composition actuelle")]
    [InlineData("de", "Aktuelle Zusammensetzung")]
    public void GetTitleAndColor_RaidCompositionAnnouncementPosted_ReturnsLocalizedTitle(string language, string expectedTitle)
    {
        var (title, color) = RaidNotificationText.GetTitleAndColor(GuildNotificationEventType.RaidCompositionAnnouncementPosted, language);

        title.Should().Be(expectedTitle);
        color.Should().Be(0x5865F2);
    }

    // ── GetCompositionAnnouncementDescription ────────────────────────────────────

    [Theory]
    [InlineData("en", "starts · updated automatically.")]
    [InlineData("fr", "starts · mis à jour automatiquement.")]
    [InlineData("de", "starts · wird automatisch aktualisiert.")]
    public void GetCompositionAnnouncementDescription_LocalizesTrailingPhrase(string language, string expected)
    {
        RaidNotificationText.GetCompositionAnnouncementDescription("starts", language).Should().Be(expected);
    }

    // ── GetSignupCallDescription ─────────────────────────────────────────────

    [Theory]
    [InlineData("en", "👤 Organized by <@1>")]
    [InlineData("fr", "👤 Organisé par <@1>")]
    [InlineData("de", "👤 Organisiert von <@1>")]
    public void GetSignupCallDescription_LocalizesOrganizerLine(string language, string expectedOrganizerLine)
    {
        var startsAtUtc = new DateTime(2026, 2, 1, 20, 0, 0, DateTimeKind.Utc);
        var unixSeconds = new DateTimeOffset(startsAtUtc).ToUnixTimeSeconds();

        var description = RaidNotificationText.GetSignupCallDescription(startsAtUtc, "1", acceptedCount: 3, tentativeCount: 1, declinedCount: 2, language);

        description.Should().Contain($"📅 <t:{unixSeconds}:F>");
        description.Should().Contain(expectedOrganizerLine);
        description.Should().Contain("✅ 3 · ❓ 1 · ❌ 2");
    }

    // ── GetSignupCallStatusLabel ──────────────────────────────────────────────

    [Theory]
    [InlineData(SignupStatus.Accepted, "en", "Accepted")]
    [InlineData(SignupStatus.Accepted, "fr", "Présent")]
    [InlineData(SignupStatus.Accepted, "de", "Zugesagt")]
    [InlineData(SignupStatus.Tentative, "en", "Tentative")]
    [InlineData(SignupStatus.Tentative, "fr", "Peut-être")]
    [InlineData(SignupStatus.Tentative, "de", "Vielleicht")]
    [InlineData(SignupStatus.Declined, "en", "Declined")]
    [InlineData(SignupStatus.Declined, "fr", "Absent")]
    [InlineData(SignupStatus.Declined, "de", "Abgesagt")]
    public void GetSignupCallStatusLabel_LocalizesEachStatus(SignupStatus status, string language, string expected)
    {
        RaidNotificationText.GetSignupCallStatusLabel(status, language).Should().Be(expected);
    }

    [Fact]
    public void GetSignupCallStatusLabel_UnknownStatus_FallsBackToStatusToString()
    {
        RaidNotificationText.GetSignupCallStatusLabel((SignupStatus)99, "en").Should().Be("99");
    }

    // ── GetGroupLabel ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("en", "Group 2:")]
    [InlineData("fr", "Groupe 2 :")]
    [InlineData("de", "Gruppe 2:")]
    public void GetGroupLabel_LocalizesGroupWording(string language, string expected)
    {
        RaidNotificationText.GetGroupLabel(2, language).Should().Be(expected);
    }

    // ── GetPlayerAddedDmTitleAndColor ─────────────────────────────────────────────

    [Theory]
    [InlineData("en", "Added to the raid")]
    [InlineData("fr", "Ajouté au raid")]
    [InlineData("de", "Zum Raid hinzugefügt")]
    public void GetPlayerAddedDmTitleAndColor_ReturnsLocalizedTitle(string language, string expectedTitle)
    {
        var (title, color) = RaidNotificationText.GetPlayerAddedDmTitleAndColor(language);

        title.Should().Be(expectedTitle);
        color.Should().Be(0x57F287);
    }

    // ── GetPlayerRemovedDmTitleAndColor ───────────────────────────────────────────

    [Theory]
    [InlineData("en", "Removed from the raid")]
    [InlineData("fr", "Retiré du raid")]
    [InlineData("de", "Vom Raid entfernt")]
    public void GetPlayerRemovedDmTitleAndColor_ReturnsLocalizedTitle(string language, string expectedTitle)
    {
        var (title, color) = RaidNotificationText.GetPlayerRemovedDmTitleAndColor(language);

        title.Should().Be(expectedTitle);
        color.Should().Be(0xED4245);
    }

    // ── GetRaidPublishedDmTitleAndColor ───────────────────────────────────────────

    [Theory]
    [InlineData("en", "Split 1 published")]
    [InlineData("fr", "Split 1 publié")]
    [InlineData("de", "Split 1 veröffentlicht")]
    public void GetRaidPublishedDmTitleAndColor_LocalizesAroundEventName(string language, string expectedTitle)
    {
        var (title, color) = RaidNotificationText.GetRaidPublishedDmTitleAndColor("Split 1", language);

        title.Should().Be(expectedTitle);
        color.Should().Be(0x57F287);
    }

    // ── GetPlayerCompositionDmDescription ─────────────────────────────────────────

    [Theory]
    [InlineData("en", true, "You've been added to **Split 1** (starts) with X.")]
    [InlineData("fr", true, "Tu as été ajouté à **Split 1** (starts) avec X.")]
    [InlineData("de", true, "Du wurdest mit X zu **Split 1** (starts) hinzugefügt.")]
    [InlineData("en", false, "You've been removed from **Split 1** (starts) with X.")]
    [InlineData("fr", false, "Tu as été retiré de **Split 1** (starts) avec X.")]
    [InlineData("de", false, "Du wurdest mit X von **Split 1** (starts) entfernt.")]
    public void GetPlayerCompositionDmDescription_LocalizesAddedOrRemovedVerb(string language, bool added, string expected)
    {
        RaidNotificationText.GetPlayerCompositionDmDescription("Split 1", "starts", "X", added, language).Should().Be(expected);
    }

    // ── GetPlayerSpecChangedDmTitleAndColor ───────────────────────────────────────

    [Theory]
    [InlineData("en", "Spec changed")]
    [InlineData("fr", "Spécialisation changée")]
    [InlineData("de", "Skillung geändert")]
    public void GetPlayerSpecChangedDmTitleAndColor_ReturnsLocalizedTitle(string language, string expectedTitle)
    {
        var (title, color) = RaidNotificationText.GetPlayerSpecChangedDmTitleAndColor(language);

        title.Should().Be(expectedTitle);
        color.Should().Be(0xFEE75C);
    }

    // ── GetPlayerSpecChangedDmDescription ─────────────────────────────────────────

    [Theory]
    [InlineData("en", "Your spec for **Split 1** (starts) changed on X: OLD → NEW.")]
    [InlineData("fr", "Ta spécialisation pour **Split 1** (starts) a changé sur X : OLD → NEW.")]
    [InlineData("de", "Deine Skillung für **Split 1** (starts) wurde auf X geändert: OLD → NEW.")]
    public void GetPlayerSpecChangedDmDescription_LocalizesOldAndNewSpec(string language, string expected)
    {
        RaidNotificationText.GetPlayerSpecChangedDmDescription("Split 1", "starts", "X", "OLD", "NEW", language).Should().Be(expected);
    }

    // ── GetRaidCancelledDmTitleAndColor ───────────────────────────────────────────

    [Theory]
    [InlineData("en", "Raid cancelled")]
    [InlineData("fr", "Raid annulé")]
    [InlineData("de", "Raid abgesagt")]
    public void GetRaidCancelledDmTitleAndColor_ReturnsLocalizedTitle(string language, string expectedTitle)
    {
        var (title, color) = RaidNotificationText.GetRaidCancelledDmTitleAndColor(language);

        title.Should().Be(expectedTitle);
        color.Should().Be(0xED4245);
    }

    // ── GetRaidCancelledDmDescription ─────────────────────────────────────────────

    [Theory]
    [InlineData("en", "The raid **Split 1** (starts) you were in with X has been cancelled.")]
    [InlineData("fr", "Le raid **Split 1** (starts) auquel tu participais avec X a été annulé.")]
    [InlineData("de", "Der Raid **Split 1** (starts), an dem du mit X teilgenommen hast, wurde abgesagt.")]
    public void GetRaidCancelledDmDescription_LocalizesBody(string language, string expected)
    {
        RaidNotificationText.GetRaidCancelledDmDescription("Split 1", "starts", "X", language).Should().Be(expected);
    }

    // ── GetGroupingPingMessage ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("en", "<@1>\n**Split 1** - grouping in progress. /w Arthas inv")]
    [InlineData("fr", "<@1>\n**Split 1** - groupage en cours. /w Arthas inv")]
    [InlineData("de", "<@1>\n**Split 1** - Gruppierung läuft. /w Arthas inv")]
    public void GetGroupingPingMessage_LocalizesGroupingPhraseKeepsWhisperCommandLiteral(string language, string expected)
    {
        RaidNotificationText.GetGroupingPingMessage("<@1>", "Split 1", "Arthas", language).Should().Be(expected);
    }

    [Fact]
    public void GetGroupingPingMessage_UnsupportedLanguage_FallsBackToEnglish()
    {
        RaidNotificationText.GetGroupingPingMessage("<@1>", "Split 1", "Arthas", "es")
            .Should().Be("<@1>\n**Split 1** - grouping in progress. /w Arthas inv");
    }
}
