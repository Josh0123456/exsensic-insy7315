namespace Exsensic.IntegrationTests.Bookings;

/// <summary>
/// A clock for tests that follows the real time, plus an offset a test can add. Moving it forward lets a
/// test reach a slot's start or the cancellation cut-off immediately, and the result is the same on every run.
/// </summary>
public sealed class TestClock : TimeProvider
{
    private TimeSpan _offset;

    /// <summary>The real UTC time plus however far the test has moved the clock.</summary>
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow + _offset;

    /// <summary>Moves the clock forward (or back, with a negative value).</summary>
    public void Advance(TimeSpan by) => _offset += by;

    /// <summary>Moves the clock so "now" is the given moment.</summary>
    public void SetUtcNow(DateTimeOffset now) => _offset = now - DateTimeOffset.UtcNow;
}
