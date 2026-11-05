using FlowOps.Domain.Common;
using static FlowOps.Domain.Tests.TestOrg;

namespace FlowOps.Domain.Tests.Common;

public sealed class SequentialGuidTests
{
    [Fact]
    public void SameInstant_IdsIncreaseInByteOrderAndAreVersion7()
    {
        var ids = Enumerable.Range(0, 10_000).Select(_ => SequentialGuid.NewV7(T0)).ToList();

        var bytes = ids.Select(id => id.ToByteArray(bigEndian: true)).ToList();
        for (var i = 1; i < bytes.Count; i++)
        {
            bytes[i].AsSpan().SequenceCompareTo(bytes[i - 1]).ShouldBePositive($"id {i} must sort after id {i - 1}");
        }

        ids.ShouldAllBe(id => id.Version == 7 && id.Variant >= 8 && id.Variant <= 11);
    }

    [Fact]
    public void ClockGoingBack_StillIncreases()
    {
        var later = SequentialGuid.NewV7(T0.AddDays(1)).ToByteArray(bigEndian: true);
        var earlier = SequentialGuid.NewV7(T0).ToByteArray(bigEndian: true);

        earlier.AsSpan().SequenceCompareTo(later).ShouldBePositive();
    }

    [Fact]
    public void Timestamp_IsTheGivenInstantWhenTheClockMovesForward()
    {
        var instant = DateTimeOffset.UtcNow.AddYears(5);

        var bytes = SequentialGuid.NewV7(instant).ToByteArray(bigEndian: true);

        var milliseconds = ((long)bytes[0] << 40) | ((long)bytes[1] << 32) | ((long)bytes[2] << 24) | ((long)bytes[3] << 16) | ((long)bytes[4] << 8) | bytes[5];
        milliseconds.ShouldBe(instant.ToUnixTimeMilliseconds());
    }
}
