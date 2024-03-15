using Scar.Common.DAL.Contracts.Model;
using Scar.Common.ImageProcessing.Metadata;

namespace PhotoReviewer.Memories.DAL.Data;

public class FileRecord : Entity<string>
{
    public DateTime DateTaken { get; set; }

    public static FileRecord Create(string filePath, ExifMetadata metadata)
    {
        _ = metadata ?? throw new ArgumentNullException(nameof(metadata));

        return new FileRecord { Id = filePath, DateTaken = metadata.DateImageTaken ?? new FileInfo(filePath).LastWriteTime };
    }
}