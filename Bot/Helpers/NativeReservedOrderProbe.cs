#if PLATFORM_CANDIDATE
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ACNHMobileSpawner;
using SysBot.Base;
using SysBot.Base.Protocol;
using SysBot.ACNH.Adapter;

namespace SysBot.ACNHOrders
{
    /// <summary>Explicit preexisting-order probe or one-shot airport maintenance.
    /// No create/bind, no order services, automatic restart or replay.</summary>
    public static class NativeReservedOrderProbe
    {
        public static async Task RunAsync(CrossBotConfig config,CancellationToken ct)
        {
            if(config.NativePreflightOnly)throw new ArgumentException("Select exactly one diagnostic mode.");
            NativeAcnhPreflight.ValidateExpectedBuild(config.NativeExpectedBuildId);
            byte[]? outside=null,inside=null;
            if(config.NativeAirportEntryOnly){
                outside=LegacyRecoveryGuard.ParseArrivalAnchor(config.NativeAirportOutsideAnchorHex);
                inside=LegacyRecoveryGuard.ParseArrivalAnchor(config.NativeAirportInsideAnchorHex);
                if(outside.AsSpan().SequenceEqual(inside))throw new ArgumentException("Independent source and destination anchors required.");
            }
            var root=Environment.GetEnvironmentVariable("ACNH_RECOVERY_ROOT")??"";
            var slot=Environment.GetEnvironmentVariable("ACNH_DEVICE_SLOT")??"";
            if(!Path.IsPathFullyQualified(root)||!Path.IsPathFullyQualified(config.NativeCheckpointDirectory)||
               !Directory.Exists(root)||!Directory.Exists(config.NativeCheckpointDirectory)||string.IsNullOrWhiteSpace(slot)||
               string.IsNullOrWhiteSpace(config.NativeOrderId))
                throw new ArgumentException("Existing absolute recovery/checkpoint directories and native order/slot required.");
            var expected=new Sbp2DeviceIdentity(config.NativeExpectedProcessId,NativeAcnhPreflight.GameTitle,
                config.NativeExpectedTitleVersion,config.NativeExpectedMainBase,config.NativeExpectedHeapBase,config.NativeExpectedBuildId.ToUpperInvariant());
            NativeAcnhPreflight.ValidateIdentity(expected,config.NativeExpectedBuildId);
            if(expected.ProcessId==0||expected.MainBase==0||expected.HeapBase==0)throw new ArgumentException("Exact native identity required.");
            var store=new OrderCheckpointStore(config.NativeCheckpointDirectory);
            var order=store.Load(config.NativeOrderId)??throw new InvalidOperationException("Existing native checkpoint required.");
            var key=Sbp2FreezeDeviceKey.Parse(order.DeviceIdentity);
            if(key.Pid!=expected.ProcessId||key.Title!=expected.TitleId||order.Checkpoint==DeliveryCheckpoint.DeliveryCommitted)
                throw new InvalidOperationException("Native order identity mismatch or already completed.");
            using var lease=new DeviceLifecycleCoordinator(root).Open(slot,order.OrderId);
            var profile=config.Protocol==SwitchProtocol.WiFi?BotbaseProfile.PlatformTcp2:BotbaseProfile.PlatformUsb2;
            await using var native=await AcnhNativeTransport.OpenAsync(config,profile,lease,store,expected,ct).ConfigureAwait(false);
            if(config.NativeAirportEntryOnly){
                // Original candidate EnterAirport movement: diagonal 400ms then neutral 600ms.
                // No raw configure/anchor writes, no second attempt, no delivery authorization.
                var motion=new[]{new Sbp2ControllerStep(400,new(Sbp2Buttons.None,20000,20000)),new Sbp2ControllerStep(600,default)};
                var result=await native.EnterAirportAsync(order,OffsetHelper.PlayerCoordJumps,new[]{inside!},new[]{outside!},motion,AirportScreenEvidence.IsLoading,ct).ConfigureAwait(false);
                if(!result.Ready)throw new IOException("Airport transition or pause release not verified; preserve reservation and reconcile.");
                Console.WriteLine("Native airport candidate completed movement and owned pause release. Order still requires reconciliation; no delivery or Dodo publication.");
                return;
            }
            var jumps=(long[])OffsetHelper.PlayerCoordJumps.Clone();jumps[^1]=checked(jumps[^1]+0x20);
            var scene=await native.ReadPointerAsync(jumps,4,ct).ConfigureAwait(false);
            bool loading=AirportScreenEvidence.IsLoading(await native.CaptureAsync(ct).ConfigureAwait(false));
            if(store.Load(order.OrderId)!=order)throw new IOException("Checkpoint changed during native probe.");
            Console.WriteLine($"Native reserved-order probe passed: sceneBytes={scene.Length}, loadingPixel={loading}, checkpoint={order.Checkpoint}. No device mutation or reservation release.");
        }
    }
}
#endif
