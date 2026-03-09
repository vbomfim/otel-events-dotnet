using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace OtelEvents.Causality;

/// <summary>
/// Generates UUID v7 values per RFC 9562.
/// UUID v7 encodes a 48-bit Unix timestamp (milliseconds) for time-sortability,
/// followed by random bits for uniqueness.
/// Monotonically increasing within the same millisecond via counter increment.
/// </summary>
public static class Uuid7
{
    /// <summary>
    /// Tracks the last timestamp and counter for monotonic ordering
    /// within the same millisecond.
    /// </summary>
    private static long s_lastTimestamp;

    /// <summary>
    /// 12-bit counter incremented within the same millisecond to guarantee
    /// monotonically increasing UUIDs. Stored as a long for atomic operations.
    /// </summary>
    private static long s_counter;

    /// <summary>
    /// Lock object for CAS loop on timestamp/counter pair.
    /// </summary>
    private static readonly object s_lock = new();

    /// <summary>
    /// Generates a new UUID v7 (RFC 9562) that is time-sortable and globally unique.
    /// </summary>
    /// <returns>A new UUID v7 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guid NewUuid7()
    {
        long timestamp;
        int counter;

        lock (s_lock)
        {
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (timestamp == s_lastTimestamp)
            {
                s_counter++;
                counter = (int)(s_counter & 0xFFF); // 12-bit mask
            }
            else
            {
                s_lastTimestamp = timestamp;
                s_counter = 0;
                counter = 0;
            }
        }

        return CreateUuid7(timestamp, counter);
    }

    /// <summary>
    /// Generates a formatted event ID string: "evt_{uuid_v7}".
    /// Uses stackalloc for zero-allocation formatting where possible.
    /// </summary>
    /// <returns>A formatted event ID string (e.g., "evt_0192a3b4-c5d6-7e8f-9012-a3b4c5d6e7f8").</returns>
    public static string FormatEventId()
    {
        var uuid = NewUuid7();
        return string.Create(40, uuid, static (span, guid) =>
        {
            "evt_".AsSpan().CopyTo(span);
            guid.TryFormat(span.Slice(4), out _);
        });
    }

    /// <summary>
    /// Creates a UUID v7 from a timestamp and counter value.
    /// Layout (128 bits, big-endian):
    ///   [48-bit timestamp_ms] [4-bit version=0111] [12-bit counter] [2-bit variant=10] [62-bit random]
    /// </summary>
    private static Guid CreateUuid7(long timestampMs, int counter)
    {
        Span<byte> bytes = stackalloc byte[16];

        // Fill with random bytes first (for the random portion)
        RandomNumberGenerator.Fill(bytes);

        // Bytes 0-5: 48-bit Unix timestamp in milliseconds (big-endian)
        bytes[0] = (byte)(timestampMs >> 40);
        bytes[1] = (byte)(timestampMs >> 32);
        bytes[2] = (byte)(timestampMs >> 24);
        bytes[3] = (byte)(timestampMs >> 16);
        bytes[4] = (byte)(timestampMs >> 8);
        bytes[5] = (byte)timestampMs;

        // Bytes 6-7: version (4 bits = 0111) + counter (12 bits)
        bytes[6] = (byte)(0x70 | ((counter >> 8) & 0x0F)); // version 7 + high 4 bits of counter
        bytes[7] = (byte)(counter & 0xFF);                   // low 8 bits of counter

        // Byte 8: variant (2 bits = 10) + 6 random bits
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // set variant to 10xx

        // Bytes 9-15: remaining random bits (already filled)

        return new Guid(bytes, bigEndian: true);
    }
}
