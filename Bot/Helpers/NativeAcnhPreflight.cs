#if PLATFORM_CANDIDATE
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers.Binary;
using ACNHMobileSpawner;
using SysBot.Base;
using SysBot.Base.Protocol;

namespace SysBot.ACNHOrders
{
    public static class NativeAcnhPreflight
    {
        public const ulong GameTitle=0x01006F8002326000;
        public static void ValidateExpectedBuild(string value)
        {
            if(value==null||value.Length!=64||value.Any(c=>!Uri.IsHexDigit(c))||value.All(c=>c=='0'))
                throw new ArgumentException("Configure the independently verified 32-byte ACNH Build ID.");
        }
        public static void ValidateIdentity(Sbp2DeviceIdentity identity,string expected)
        {
            ValidateExpectedBuild(expected);
            if(identity.TitleId!=GameTitle||identity.ProcessId==0||identity.MainBase==0||identity.HeapBase==0||
                !string.Equals(identity.BuildId,expected,StringComparison.OrdinalIgnoreCase))
                throw new IOException("ACNH process/build identity mismatch; no offset reads permitted.");
        }
        public static async Task RunAsync(CrossBotConfig config,CancellationToken ct)
        {
            ValidateExpectedBuild(config.NativeExpectedBuildId);
            using var guard=LegacyRecoveryGuard.OpenFromEnvironment();
            var profile=config.Protocol==SwitchProtocol.WiFi?BotbaseProfile.PlatformTcp2:BotbaseProfile.PlatformUsb2;
            var transport=new Sbp2SwitchTransport(config,profile);
            try{
                await transport.ConnectAsync(ct).ConfigureAwait(false);
                var required=Sbp2Capability.DeviceIdentity|Sbp2Capability.ReadPointer|Sbp2Capability.Screenshot;
                if(transport.Negotiated is not {} caps||(caps.Capabilities&required)!=required)
                    throw new NotSupportedException("Native preflight requires identity, pointer and screenshot capability.");
                async Task<Sbp2DeviceIdentity> Identity()=>Sbp2MetadataCodec.DecodeIdentity(await transport.MetadataAsync(Sbp2Command.GetDeviceIdentity,ct).ConfigureAwait(false));
                var first=await Identity().ConfigureAwait(false);ValidateIdentity(first,config.NativeExpectedBuildId);
                var jumps=(long[])OffsetHelper.PlayerCoordJumps.Clone();
                jumps[^1]=checked(jumps[^1]+0x20);
                var scene=await transport.ReadAsync(Sbp2Command.ReadPointer,Sbp2PayloadCodec.EncodePointer(new Sbp2Pointer(4,jumps)),ct).ConfigureAwait(false);
                if(scene.Length!=4)throw new InvalidDataException("Invalid scene read.");
                var screenshot=await transport.CaptureScreenAsync(ct).ConfigureAwait(false);
                bool loading=AirportScreenEvidence.IsLoading(screenshot);
                var last=await Identity().ConfigureAwait(false);
                if(first!=last)throw new IOException("Game identity changed during preflight.");
                ct.ThrowIfCancellationRequested();
                Console.WriteLine($"SBP/2 preflight passed: title={first.TitleId:X16}, version={first.TitleVersion}, scene=0x{BinaryPrimitives.ReadUInt32LittleEndian(scene):X8}, loadingPixel={loading}, JPEG={screenshot.Length} bytes. No mutations or orders executed.");
            }finally{transport.Disconnect();}
        }
    }
}
#endif
