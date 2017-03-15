namespace PhotoReviewer.ViewModel
{
    public class ProgressEventArgs
    {
        public ProgressEventArgs(int percent)
        {
            Percent = percent;
        }

        public int Percent { get; private set; }
    }
}