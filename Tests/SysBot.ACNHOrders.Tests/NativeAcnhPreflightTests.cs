#if PLATFORM_CANDIDATE
using System;
using System.IO;
using SysBot.ACNHOrders;
using SysBot.Base.Protocol;
using Xunit;
public sealed class NativeAcnhPreflightTests
{
    [Fact] public void ExactBuildAndTitleRequired()
    {
        var build=new string('A',64);
        var identity=new Sbp2DeviceIdentity(1,NativeAcnhPreflight.GameTitle,2,4096,8192,build);
        NativeAcnhPreflight.ValidateIdentity(identity,build.ToLowerInvariant());
        Assert.Throws<ArgumentException>(()=>NativeAcnhPreflight.ValidateExpectedBuild(new string('0',64)));
        Assert.Throws<IOException>(()=>NativeAcnhPreflight.ValidateIdentity(identity with{TitleId=1},build));
        Assert.Throws<IOException>(()=>NativeAcnhPreflight.ValidateIdentity(identity,new string('B',64)));
    }
}
#endif
