using System.Windows;
using System.Windows.Data;

namespace PhotoReviewer
{
    public partial class App
    {
        private void OnApplicationStartup(object sender, StartupEventArgs args)
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
            var objectDataProvider = Resources["Photos"] as ObjectDataProvider;
            mainWindow.Photos = objectDataProvider != null ? (PhotoCollection)objectDataProvider.Data : new PhotoCollection();
            if (!string.IsNullOrEmpty(Settings.Default.LastFolder))
                mainWindow.Photos.Path = mainWindow.ImagesDir.Text = Settings.Default.LastFolder;
        }
    }
}