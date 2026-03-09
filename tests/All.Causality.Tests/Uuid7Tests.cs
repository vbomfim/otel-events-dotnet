using All.Causality;

namespace All.Causality.Tests;

/// <summary>
/// Tests for UUID v7 generation (RFC 9562).
/// Validates format, time-sortability, uniqueness, and monotonicity.
/// </summary>
public class Uuid7Tests
{
    [Fact]
    public void NewUuid7_ReturnsValidGuid()
    {
        // Act
        var uuid = Uuid7.NewUuid7();

        // Assert — must be a valid Guid (not empty)
        Assert.NotEqual(Guid.Empty, uuid);
    }

    [Fact]
    public void NewUuid7_HasVersion7Marker()
    {
        // Act
        var uuid = Uuid7.NewUuid7();
        var bytes = uuid.ToByteArray(bigEndian: true);

        // Assert — version nibble (bits 48-51) must be 0b0111 = 7
        var versionNibble = (bytes[6] >> 4) & 0x0F;
        Assert.Equal(7, versionNibble);
    }

    [Fact]
    public void NewUuid7_HasVariant10xxBits()
    {
        // Act
        var uuid = Uuid7.NewUuid7();
        var bytes = uuid.ToByteArray(bigEndian: true);

        // Assert — variant bits (bits 64-65) must be 0b10
        var variantBits = (bytes[8] >> 6) & 0x03;
        Assert.Equal(0b10, variantBits);
    }

    [Fact]
    public void NewUuid7_EncodesCurrentTimestamp()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Act
        var uuid = Uuid7.NewUuid7();

        // Assert
        var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var embedded = ExtractTimestamp(uuid);

        Assert.InRange(embedded, before, after);
    }

    [Fact]
    public void NewUuid7_IsTimeSortable_AcrossMilliseconds()
    {
        // Arrange — generate UUIDs with a small delay
        var first = Uuid7.NewUuid7();
        Thread.Sleep(2); // ensure different millisecond
        var second = Uuid7.NewUuid7();

        // Assert — second UUID must sort after first (string comparison works for UUID v7)
        var firstStr = first.ToString();
        var secondStr = second.ToString();
        Assert.True(
            string.Compare(firstStr, secondStr, StringComparison.Ordinal) < 0,
            $"Expected {firstStr} < {secondStr} for time-sortability");
    }

    [Fact]
    public void NewUuid7_IsMonotonicallyIncreasing_WithinSameMillisecond()
    {
        // Arrange — generate many UUIDs quickly (likely same millisecond)
        const int count = 100;
        var uuids = new Guid[count];
        for (int i = 0; i < count; i++)
        {
            uuids[i] = Uuid7.NewUuid7();
        }

        // Assert — each UUID sorts after the previous one
        for (int i = 1; i < count; i++)
        {
            var prev = uuids[i - 1].ToString();
            var curr = uuids[i].ToString();
            Assert.True(
                string.Compare(prev, curr, StringComparison.Ordinal) < 0,
                $"UUID at index {i} ({curr}) is not greater than index {i - 1} ({prev})");
        }
    }

    [Fact]
    public void NewUuid7_ProducesUniqueValues()
    {
        // Arrange
        const int count = 10_000;
        var set = new HashSet<Guid>(count);

        // Act
        for (int i = 0; i < count; i++)
        {
            set.Add(Uuid7.NewUuid7());
        }

        // Assert — all unique
        Assert.Equal(count, set.Count);
    }

    [Fact]
    public async Task NewUuid7_ProducesUniqueValues_AcrossThreads()
    {
        // Arrange
        const int threadsCount = 8;
        const int perThread = 1_000;
        var allIds = new Guid[threadsCount * perThread];

        // Act — generate concurrently
        var tasks = Enumerable.Range(0, threadsCount).Select(t => Task.Run(() =>
        {
            for (int i = 0; i < perThread; i++)
            {
                allIds[t * perThread + i] = Uuid7.NewUuid7();
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert — all globally unique
        var set = new HashSet<Guid>(allIds);
        Assert.Equal(threadsCount * perThread, set.Count);
    }

    [Fact]
    public void FormatEventId_HasEvtPrefix()
    {
        // Act
        var eventId = Uuid7.FormatEventId();

        // Assert
        Assert.StartsWith("evt_", eventId);
    }

    [Fact]
    public void FormatEventId_ContainsValidUuidAfterPrefix()
    {
        // Act
        var eventId = Uuid7.FormatEventId();

        // Assert
        var uuidPart = eventId.Substring(4); // after "evt_"
        Assert.True(
            Guid.TryParse(uuidPart, out _),
            $"UUID part '{uuidPart}' is not a valid GUID");
    }

    [Fact]
    public void FormatEventId_MatchesExpectedFormat()
    {
        // Act
        var eventId = Uuid7.FormatEventId();

        // Assert — evt_ + 8-4-4-4-12 format = 4 + 36 = 40 chars
        Assert.Equal(40, eventId.Length);
        Assert.Matches(@"^evt_[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", eventId);
    }

    [Fact]
    public void FormatEventId_IsTimeSortable()
    {
        // Arrange
        var first = Uuid7.FormatEventId();
        Thread.Sleep(2);
        var second = Uuid7.FormatEventId();

        // Assert — string comparison preserves time order
        Assert.True(
            string.Compare(first, second, StringComparison.Ordinal) < 0,
            $"Expected {first} < {second}");
    }

    /// <summary>
    /// Extracts the 48-bit Unix timestamp (milliseconds) from a UUID v7.
    /// </summary>
    private static long ExtractTimestamp(Guid uuid)
    {
        var bytes = uuid.ToByteArray(bigEndian: true);
        long timestamp = 0;
        for (int i = 0; i < 6; i++)
        {
            timestamp = (timestamp << 8) | bytes[i];
        }
        return timestamp;
    }
}
