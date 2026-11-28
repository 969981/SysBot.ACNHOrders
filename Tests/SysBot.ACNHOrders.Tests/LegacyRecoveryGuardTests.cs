using System;
using System.IO;
using System.Text;
using Xunit;
using SysBot.ACNHOrders;

public sealed class LegacyRecoveryGuardTests
{
    [Fact] public void PersistentIntentSurvivesDisposeAndBlocksNewRun()
    {
        var root=Path.Combine(Path.GetTempPath(),"acnh-"+Guid.NewGuid().ToString("N"));
        try {
            using(var first=LegacyRecoveryGuard.Open(root,"switch")) {
                Assert.Throws<IOException>(()=>LegacyRecoveryGuard.Open(root,"switch"));
                first.Begin("order-a");
            }
            Assert.Throws<InvalidOperationException>(()=>LegacyRecoveryGuard.Open(root,"switch"));
        } finally {Directory.Delete(root,true);}
    }
    [Fact] public void ExplicitCompletionAllowsNextRun()
    {
        var root=Path.Combine(Path.GetTempPath(),"acnh-"+Guid.NewGuid().ToString("N"));
        try {
            using(var guard=LegacyRecoveryGuard.Open(root,"switch")) {guard.Begin("order-a");guard.Complete();}
            using var next=LegacyRecoveryGuard.Open(root,"switch");
            Assert.False(next.HasActiveIntent);
        } finally {Directory.Delete(root,true);}
    }
    [Theory]
    [InlineData("AB123","AB123",true)]
    [InlineData("","",false)]
    [InlineData("AB123","AB124",false)]
    [InlineData("AB12\n","AB12\n",false)]
    public void StableExactCode(string a,string b,bool valid)=>
        Assert.Equal(valid,LegacyRecoveryGuard.VerifiedCode(Encoding.ASCII.GetBytes(a),Encoding.ASCII.GetBytes(b))!=null);
    [Fact] public void EmptyFreezeReplyMustBeExact(){
        Assert.True(LegacyRecoveryGuard.IsEmptyFreezeReply(new byte[]{48,48,10}));
        Assert.False(LegacyRecoveryGuard.IsEmptyFreezeReply(new byte[]{48,49,10}));
        Assert.False(LegacyRecoveryGuard.IsEmptyFreezeReply(new byte[]{10}));
    }
}
