using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SysBot.ACNHOrders.Tests
{
    public class AirportReadRecoveryTests
    {
        [Fact] public async Task ZeroPointerResolvesAgainWithoutLowRead()
        {
            int count = 0;
            var result = await AirportReadRecovery.ReadAsync(_ => Task.FromResult(++count == 1 ? 0UL : 0x2000UL),
                (a, _) => { Assert.Equal(0x2020UL, a); return Task.FromResult(new byte[] { 0x66,0x66,0x06,0xC0 }); }, default, 0);
            Assert.Equal(OverworldState.Overworld, result);
            Assert.Equal(2, count);
        }
        [Fact] public async Task KnownShortFrameResolvesFreshPointer()
        {
            int count = 0;
            var result = await AirportReadRecovery.ReadAsync(_ => Task.FromResult((ulong)++count * 0x1000),
                (a, _) => { if (a == 0x1020) throw new InvalidDataException("sys-botbase returned a complete but short response frame: test. The LF boundary was consumed; the TCP stream remains synchronized.");
                    Assert.Equal(0x2020UL, a); return Task.FromResult(new byte[] { 0x66,0x66,0x06,0xC0 }); }, default, 0);
            Assert.Equal(OverworldState.Overworld, result);
        }
        [Fact] public async Task PersistentZeroIsMissingNotLoading()
        {
            int count = 0;
            Assert.Null(await AirportReadRecovery.ReadAsync(_ => { count++; return Task.FromResult(0UL); },
                (_, _) => throw new Exception("must not read"), default, 0));
            Assert.Equal(3, count);
        }
        [Fact] public async Task AmbiguousErrorNotRetried()
        {
            int count = 0;
            await Assert.ThrowsAsync<IOException>(() => AirportReadRecovery.ReadAsync(_ => { count++; return Task.FromResult(0x1000UL); },
                (_, _) => throw new IOException("missing LF"), default, 0));
            Assert.Equal(1, count);
        }
        [Fact] public async Task CancellationPropagates()
        {
            using var cts = new CancellationTokenSource(); cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AirportReadRecovery.ReadAsync(_ => throw new Exception("must not resolve"), (_, _) => throw new Exception("must not read"), cts.Token, 0));
        }
        [Fact] public void InvalidCoordinateRejected()
        {
            Assert.Throws<InvalidDataException>(() => AirportReadRecovery.CheckedAddress(0));
            Assert.Throws<InvalidDataException>(() => AirportReadRecovery.CheckedAddress(ulong.MaxValue));
        }
        [Fact] public void LoadingThenOverworld()
        {
            var entry = new AirportEntryObservation();
            Assert.Equal(AirportEntryDecision.Move, entry.Observe(OverworldState.Overworld, false));
            entry.MarkMoveSent();
            Assert.Equal(AirportEntryDecision.Wait, entry.Observe(null, true));
            Assert.Equal(AirportEntryDecision.Wait, entry.Observe(null, false));
            Assert.Equal(AirportEntryDecision.Complete, entry.Observe(OverworldState.Overworld, false));
        }
        [Fact] public void UnknownDoesNotAuthorizeMoveOrCompletion()
        {
            var entry = new AirportEntryObservation();
            Assert.Equal(AirportEntryDecision.Wait, entry.Observe(OverworldState.Unknown, false));
            Assert.Equal(AirportEntryDecision.Wait, entry.Observe(null, false));
            Assert.Equal(AirportEntryDecision.Wait, entry.Observe(OverworldState.Null, false));
            Assert.Throws<IOException>(() => entry.Observe(OverworldState.UserArriveLeaving, false));
        }
    }
}
