using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.ACNHOrders
{
    public static class AirportReadRecovery
    {
        public static ulong CheckedAddress(ulong coordinate)
        {
            if (coordinate == 0 || coordinate > ulong.MaxValue - 0x23)
                throw new InvalidDataException("Invalid coordinate pointer.");
            return coordinate + 0x20;
        }

        public static async Task<OverworldState?> ReadAsync(
            Func<CancellationToken, Task<ulong>> resolve,
            Func<ulong, CancellationToken, Task<byte[]>> read,
            CancellationToken token, int delayMs = 250)
        {
            if (delayMs < 0 || delayMs > 1000) throw new ArgumentOutOfRangeException(nameof(delayMs));
            for (int attempt = 0; attempt < 3; attempt++)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    ulong coordinate = await resolve(token).ConfigureAwait(false);
                    if (coordinate != 0)
                    {
                        byte[] data = await read(CheckedAddress(coordinate), token).ConfigureAwait(false);
                        if (data.Length != 4) throw new InvalidDataException("Unexpected overworld payload size.");
                        return DodoPositionHelper.GetOverworldState(BitConverter.ToUInt32(data, 0));
                    }
                }
                catch (InvalidDataException ex) when (
                    ex.Message.StartsWith("sys-botbase returned a complete but short response frame:", StringComparison.Ordinal) &&
                    ex.Message.EndsWith("The LF boundary was consumed; the TCP stream remains synchronized.", StringComparison.Ordinal))
                {
                    // Only the known complete LF frame diagnostic is retryable.
                }
                if (attempt < 2) await Task.Delay(delayMs, token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
            return null;
        }
    }

    public enum AirportEntryDecision { Wait, Move, Complete }

    public sealed class AirportEntryObservation
    {
        private bool moveSent;
        private bool transitionSeen;
        private bool uncertainAfterMove;
        public void MarkMoveSent() => moveSent = true;
        public AirportEntryDecision Observe(OverworldState? state, bool loadingScreen)
        {
            if (state == OverworldState.UserArriveLeaving)
                throw new IOException("Unexpected visitor transition during airport entry.");
            if (loadingScreen || state == OverworldState.Loading)
            {
                if (!moveSent) throw new IOException("Unexpected loading before airport movement.");
                transitionSeen = true;
                return AirportEntryDecision.Wait;
            }
            if (state != OverworldState.Overworld)
            {
                if (moveSent) uncertainAfterMove = true;
                return AirportEntryDecision.Wait;
            }
            if (transitionSeen) return AirportEntryDecision.Complete;
            return uncertainAfterMove ? AirportEntryDecision.Wait : AirportEntryDecision.Move;
        }
    }
}
