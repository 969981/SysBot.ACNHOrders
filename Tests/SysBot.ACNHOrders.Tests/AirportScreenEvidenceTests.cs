using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SysBot.ACNHOrders;
using Xunit;

public sealed class AirportScreenEvidenceTests
{
    [Theory]
    [InlineData(2,true)]
    [InlineData(40,false)]
    public void LoadingRuleUsesActualPixel(int color,bool expected)
    {
        using var image=new Image<Rgb24>(300,50,new Rgb24((byte)color,(byte)color,(byte)color));
        using var ms=new MemoryStream();image.SaveAsJpeg(ms);
        Assert.Equal(expected,AirportScreenEvidence.IsLoading(ms.ToArray()));
    }
    [Fact] public void InvalidFrameIsNotNonLoadingEvidence() =>
        Assert.Throws<InvalidDataException>(()=>AirportScreenEvidence.IsLoading(new byte[]{1,2,3,4}));
}
