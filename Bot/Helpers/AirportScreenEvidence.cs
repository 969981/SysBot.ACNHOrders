using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SysBot.ACNHOrders
{
    // Same 1x1 RGB rule as the historical airport comparer. Malformed or
    // unsupported screenshot is an error, not evidence that loading ended.
    public static class AirportScreenEvidence
    {
        public static bool IsLoading(byte[] jpeg)
        {
            if(jpeg==null||jpeg.Length<4||jpeg.Length>2*1024*1024||jpeg[0]!=255||jpeg[1]!=216||
                jpeg[^2]!=255||jpeg[^1]!=217)throw new InvalidDataException("Invalid screenshot JPEG.");
            using var image=Image.Load<Rgb24>(jpeg);
            if(image.Width<=274||image.Height<=24||image.Width>4096||image.Height>4096)
                throw new InvalidDataException("Screenshot dimensions invalid.");
            var p=image[274,24];
            return Math.Abs(p.R-2)<=2&&Math.Abs(p.G-2)<=2&&Math.Abs(p.B-2)<=2;
        }
    }
}
