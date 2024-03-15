using System.Windows.Media.Imaging;
using Scar.Common.ImageProcessing.Metadata;

namespace PhotoReviewer.Memories.Utils;

public static class OrientationExtensions
{
    public static Rotation ToRotation(this Orientation orientation)
    {
        return orientation switch
        {
            Orientation.NotSpecified or Orientation.Straight => Rotation.Rotate0,
            Orientation.Left => Rotation.Rotate90,
            Orientation.Reverse => Rotation.Rotate180,
            Orientation.Right => Rotation.Rotate270,
            _ => throw new ArgumentException("Invalid orientation value.", nameof(orientation)),
        };
    }
}