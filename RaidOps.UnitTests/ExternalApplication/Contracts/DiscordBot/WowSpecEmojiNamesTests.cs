using FluentAssertions;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.UnitTests.ExternalApplication.Contracts.DiscordBot;

public class WowSpecEmojiNamesTests
{
    [Fact]
    public void GetName_KnownClass_UsesClassSlugAndSlugifiedSpecName()
    {
        WowSpecEmojiNames.GetName(1, "Arms").Should().Be("spec_warrior_arms");
    }

    [Fact]
    public void GetName_KnownClassWithSpaceInSpecName_StripsTheSpace()
    {
        WowSpecEmojiNames.GetName(3, "Beast Mastery").Should().Be("spec_hunter_beastmastery");
    }

    [Fact]
    public void GetName_SpecNameWithPunctuation_StripsNonAlphanumericCharacters()
    {
        WowSpecEmojiNames.GetName(8, "Fire!!").Should().Be("spec_mage_fire");
    }

    [Fact]
    public void GetName_UnknownClassId_FallsBackToTheRawClassIdInsteadOfASlug()
    {
        WowSpecEmojiNames.GetName(999, "Arms").Should().Be("spec_999_arms");
    }
}
