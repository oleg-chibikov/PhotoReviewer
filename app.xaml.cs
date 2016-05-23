using System.Windows;
using System.Windows.Data;

namespace PhotoReviewer
{
    public partial class App
    {
        private void OnApplicationStartup(object sender, StartupEventArgs args)
        {
            var objectDataProvider = Resources["Photos"] as ObjectDataProvider;
            var photos = objectDataProvider != null ? (PhotoCollection)objectDataProvider.Data : new PhotoCollection();
            new MainWindow(photos).Show();
        }
    }
}