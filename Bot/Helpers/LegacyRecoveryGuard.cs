using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SysBot.ACNHOrders
{
    // Candidate local cooperative lifecycle guard. No expiry or automatic clearing on Dispose.
    // Existing intent after a crash must be independently reconciled by an operator.
    public sealed class LegacyRecoveryGuard : IDisposable
    {
        private readonly FileStream handle;
        private readonly string intent;
        private bool active, disposed;
        private LegacyRecoveryGuard(FileStream handle, string intent) { this.handle=handle; this.intent=intent; }
        public static LegacyRecoveryGuard OpenFromEnvironment() =>
            Open(Environment.GetEnvironmentVariable("ACNH_RECOVERY_ROOT") ?? "", Environment.GetEnvironmentVariable("ACNH_DEVICE_SLOT") ?? "");
        public static LegacyRecoveryGuard Open(string root, string slot)
        {
            if (!Path.IsPathFullyQualified(root) || string.IsNullOrWhiteSpace(slot) || slot.Length>128 || slot.Any(char.IsControl))
                throw new ArgumentException("Configure absolute ACNH_RECOVERY_ROOT and stable ACNH_DEVICE_SLOT.");
            Directory.CreateDirectory(root);
            var key=Path.Combine(root, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(slot))));
            var handle=new FileStream(key+".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            try {
                var intent=key+".legacy-intent";
                try { using var prior=File.OpenRead(intent); throw new InvalidOperationException("Unresolved legacy operation; operator reconciliation required."); }
                catch(FileNotFoundException) { }
                // Also reject unresolved Platform reservation for the same configured slot.
                try { using var prior=File.OpenRead(key+".reservation"); throw new InvalidOperationException("Unresolved Platform device reservation."); }
                catch(FileNotFoundException) { }
                return new LegacyRecoveryGuard(handle,intent);
            } catch { handle.Dispose(); throw; }
        }
        public bool HasActiveIntent=>active&&!disposed;
        public void Begin(string label)
        {
            if(disposed||active||string.IsNullOrWhiteSpace(label)||label.Length>256||label.Any(char.IsControl))
                throw new InvalidOperationException("Invalid or overlapping recovery intent.");
            using var file=new FileStream(intent,FileMode.CreateNew,FileAccess.Write,FileShare.None);
            var bytes=Encoding.UTF8.GetBytes(label);file.Write(bytes,0,bytes.Length);file.Flush(true);active=true;
        }
        public void RequireIntent(){if(!HasActiveIntent)throw new InvalidOperationException("Persisted mutation intent required.");}
        public void Complete(){RequireIntent();File.Delete(intent);active=false;}
        public void Dispose(){if(disposed)return;disposed=true;handle.Dispose();}
        public static bool IsEmptyFreezeReply(byte[]? bytes)=>bytes!=null&&bytes.SequenceEqual(new byte[]{48,48,10});
        public static string? VerifiedCode(byte[]? first,byte[]? second)
        {
            if(first==null||second==null||first.Length!=5||!first.SequenceEqual(second)||
                first.Any(c=>!(c>=65&&c<=90)&&!(c>=48&&c<=57)))return null;
            return Encoding.ASCII.GetString(first);
        }
        public static byte[] ParseArrivalAnchor(string? value)
        {
            if(value==null||value.Length!=32)throw new ArgumentException("Capture independent airport arrival anchor (16 bytes) in ACNH_AIRPORT_ARRIVAL_ANCHOR_HEX.");
            var b=Convert.FromHexString(value);
            if(b.Take(12).All(x=>x==0))throw new ArgumentException("Invalid airport arrival anchor.");
            return b;
        }
    }
}
