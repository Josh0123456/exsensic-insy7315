using System.Text.RegularExpressions;
using Exsensic.Core.Bookings;

namespace Exsensic.UnitTests.Bookings;

/// <summary>
/// Checks the small helpers booking creation relies on: SAST to UTC conversion and booking references.
/// </summary>
public partial class BookingHelpersTests
{
    /// <summary>09:00 SAST is 07:00 UTC, all year, because South Africa has no daylight saving.</summary>
    [Theory]
    [InlineData(2026, 1, 15)]
    [InlineData(2026, 7, 15)]
    public void ToUtc_NineAmSast_IsSevenAmUtc(int year, int month, int day)
    {
        var utc = SastTime.ToUtc(new DateOnly(year, month, day), new TimeOnly(9, 0));

        Assert.Equal(new DateTimeOffset(year, month, day, 7, 0, 0, TimeSpan.Zero), utc);
        Assert.Equal(TimeSpan.Zero, utc.Offset);
    }

    /// <summary>A slot just after midnight SAST is still the previous day in UTC.</summary>
    [Fact]
    public void ToUtc_JustAfterMidnightSast_IsPreviousDayUtc()
    {
        var utc = SastTime.ToUtc(new DateOnly(2026, 10, 5), new TimeOnly(1, 0));

        Assert.Equal(new DateTimeOffset(2026, 10, 4, 23, 0, 0, TimeSpan.Zero), utc);
    }

    /// <summary>References are "EXS-" plus 8 easy-to-read characters, short enough for the 20-character column.</summary>
    [Fact]
    public void Next_Always_MatchesReferenceFormat()
    {
        for (var i = 0; i < 200; i++)
        {
            var reference = BookingReferenceGenerator.Next();

            Assert.Matches(ReferencePattern(), reference);
            Assert.True(reference.Length <= 20);
        }
    }

    /// <summary>References are random, so a batch of them doesn't repeat.</summary>
    [Fact]
    public void Next_ManyCalls_ReturnsDistinctReferences()
    {
        var references = Enumerable.Range(0, 1000).Select(_ => BookingReferenceGenerator.Next()).ToList();

        Assert.Equal(references.Count, references.Distinct().Count());
    }

    /// <summary>No 0, O, 1, I or L, which are easy to confuse when read aloud.</summary>
    [GeneratedRegex("^EXS-[A-HJKMNP-Z2-9]{8}$")]
    private static partial Regex ReferencePattern();
}
