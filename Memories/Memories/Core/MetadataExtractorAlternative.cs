// using System.IO;
// using System.Windows.Media.Imaging;
//
// namespace PhotoReviewer.Memories.Core
// {
//     public class MetadataExtractor
//     {
//         const string OrientationQuery = "System.Photo.Orientation";
//         const string DateTakenQuery = "System.Photo.DateTaken";
//
//         public enum MetadataType
//         {
//             Rotation,
//             DateTaken,
//             Both
//         }
//
//         public static (Rotation rotation, DateTime? dateTaken) GetMetadata(string path, MetadataType metadataType = MetadataType.Rotation)
//         {
//             var rotation = Rotation.Rotate0;
//             DateTime? dateTaken = null;
//
//             using var fileStream = new FileStream(
//                 path,
//                 FileMode.Open,
//                 FileAccess.Read);
//
//             var bitmapFrame = BitmapFrame.Create(
//                 fileStream,
//                 BitmapCreateOptions.DelayCreation,
//                 BitmapCacheOption.None);
//
//             if (bitmapFrame.Metadata is not BitmapMetadata bitmapMetadata)
//             {
//                 return (rotation, dateTaken);
//             }
//
//             if (metadataType is MetadataType.Rotation or MetadataType.Both)
//             {
//                 if (bitmapMetadata.ContainsQuery(OrientationQuery))
//                 {
//                     var o = bitmapMetadata.GetQuery(OrientationQuery);
//
//                     if (o != null)
//                     {
//                         rotation = (ushort)o switch
//                         {
//                             6 => Rotation.Rotate90,
//                             3 => Rotation.Rotate180,
//                             8 => Rotation.Rotate270,
//                             _ => rotation
//                         };
//                     }
//                 }
//             }
//
//             if (metadataType is MetadataType.DateTaken or MetadataType.Both)
//             {
//                 // Try to retrieve date taken from various metadata standards
//                 dateTaken = GetDateTakenFromMetadata(bitmapMetadata);
//             }
//
//             return (rotation, dateTaken);
//         }
//
//         static DateTime? GetDateTakenFromMetadata(BitmapMetadata bitmapMetadata)
//         {
//             // Try Exif standard for JPEG files
//             if (bitmapMetadata.ContainsQuery(DateTakenQuery))
//             {
//                 var dateTakenString = bitmapMetadata.GetQuery(DateTakenQuery) as string;
//                 if (DateTime.TryParse(dateTakenString, out var parsedDateTaken))
//                 {
//                     return parsedDateTaken;
//                 }
//             }
//
//             // Try XMP standard for various file types
//             if (bitmapMetadata.ContainsQuery("http://purl.org/dc/elements/1.1/date"))
//             {
//                 var dateTakenString = bitmapMetadata.GetQuery("http://purl.org/dc/elements/1.1/date") as string;
//                 if (DateTime.TryParse(dateTakenString, out var parsedDateTaken))
//                 {
//                     return parsedDateTaken;
//                 }
//             }
//
//             return null;
//         }
//     }
// }
