namespace PhotoReviewer.Memories.Core;

[Flags]
public enum JobScheduleOptions
{
    None = 0,
    Immediate = 1,
    Daily = 2,
    Both = Immediate | Daily
}