using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using SysBot.Base;
using Xunit;

namespace SysBot.ACNHOrders.Tests
{
    public class SdkRuntimeIdentityTests
    {
        [Fact]
        public void LoadedSdkMatchesExpectedCandidate()
        {
            var assembly = typeof(ISwitchConnectionAsync).Assembly;
            var expected = Environment.GetEnvironmentVariable("ACNH_EXPECTED_SDK_SHA256");
            Assert.Equal("SysBot.Base", assembly.GetName().Name);
            var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location)));
            if (string.IsNullOrEmpty(expected)) return; // local development without CI pin
            Assert.Equal(expected, actual);
            var nlog = Assembly.Load(new AssemblyName("NLog"));
            bool platform = Environment.GetEnvironmentVariable("ACNH_EXPECTED_PLATFORM_SDK") == "true";
            Assert.Equal(platform ? 5 : 4, nlog.GetName().Version!.Major);
            if (platform)
                Assert.Equal("LibUsbDotNet", Assembly.Load(new AssemblyName("LibUsbDotNet")).GetName().Name);
        }
    }
}
