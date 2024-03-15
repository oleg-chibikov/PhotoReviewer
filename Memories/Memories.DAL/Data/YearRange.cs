namespace PhotoReviewer.Memories.DAL.Data;

public class YearRange(int minYear, int maxYear)
{
    public int MinYear { get; } = minYear;

    public int MaxYear { get; } = maxYear;
}