using System.Buffers.Binary;
using System.Security.Cryptography;

namespace FlowOps.Domain.Common;

/// <summary>
/// UUIDv7 ids that are strictly increasing within this process, even for the same millisecond (RFC 9562 §6.2,
/// method 1: a 12-bit counter in <c>rand_a</c>). <see cref="Guid.CreateVersion7(DateTimeOffset)"/> randomises everything
/// after the timestamp, so a request's "Submitted" and "TaskAssigned" activities, written in the same instant, could
/// sort either way. Ordering by id (PostgreSQL compares uuids bytewise) is therefore creation order.
/// </summary>
public static class SequentialGuid
{
    private const int CounterBits = 12;
    private const int MaxCounter = (1 << CounterBits) - 1;
    private static readonly Lock Gate = new();
    private static long _lastMilliseconds = long.MinValue;
    private static int _counter;

    public static Guid NewV7(DateTimeOffset now)
    {
        long milliseconds;
        int counter;
        lock (Gate)
        {
            milliseconds = Math.Max(now.ToUnixTimeMilliseconds(), _lastMilliseconds);
            if (milliseconds == _lastMilliseconds && _counter < MaxCounter)
            {
                _counter++;
            }
            else
            {
                // A new millisecond, or the counter ran out: move on (never back, even if the clock does).
                milliseconds = milliseconds == _lastMilliseconds ? milliseconds + 1 : milliseconds;
                // Start low in the counter range so many ids still fit in this millisecond.
                _counter = RandomNumberGenerator.GetInt32(0, 1 << (CounterBits - 2));
            }

            _lastMilliseconds = milliseconds;
            counter = _counter;
        }

        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes[8..]);
        BinaryPrimitives.WriteInt64BigEndian(bytes, milliseconds << 16);           // 48-bit timestamp, then 16 bits we set next
        BinaryPrimitives.WriteUInt16BigEndian(bytes[6..], (ushort)(0x7000 | counter)); // version 7 + counter
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);                                 // RFC 9562 variant
        return new Guid(bytes, bigEndian: true);
    }
}
