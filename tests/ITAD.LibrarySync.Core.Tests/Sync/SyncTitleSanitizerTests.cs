using FluentAssertions;
using ITAD.LibrarySync.Core.Sync;

namespace ITAD.LibrarySync.Core.Tests.Sync;

public class SyncTitleSanitizerTests
{
    [Theory]
    [InlineData("HUMANKIND™", "HUMANKIND")]
    [InlineData("Injustice™ 2 - Standard Edition", "Injustice 2 - Standard Edition")]
    [InlineData("No Man\u2019s Sky", "No Man's Sky")]
    public void Sanitize_normalizes_special_characters(string input, string expected) =>
        SyncTitleSanitizer.Sanitize(input).Should().Be(expected);
}
