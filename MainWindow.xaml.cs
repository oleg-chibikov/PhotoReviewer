using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Microsoft.VisualBasic.FileIO;
using MessageBox = System.Windows.MessageBox;

namespace PhotoReviewer
{
    public sealed partial class MainWindow
    {
        public PhotoCollection Photos;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ViewPhoto(object sender, RoutedEventArgs e)
        {
            var pvWindow = new PhotoView {MainWindow = this, SelectedPhoto = (Photo) PhotosListBox.SelectedItem};
            pvWindow.Show();
        }

        private void MarkAsDeleted(object sender, RoutedEventArgs e)
        {
            var photos = PhotosListBox.SelectedItems;
            foreach (Photo photo in photos)
            {
                photo.MarkedForDeletion = !photo.MarkedForDeletion;
                DbProvider.Save(photo.Source);
            }
        }

        private void OpenInExplorer(object sender, RoutedEventArgs e)
        {
            var photo = (Photo) PhotosListBox.SelectedItem;
            new Process
            {
                StartInfo =
                {
                    FileName = "explorer.exe",
                    Arguments = "/select,\"" + photo.Source + "\""
                }
            }.Start();
        }

        private void OnImagesDirChangeClick(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();

            if (!string.IsNullOrEmpty(Settings.Default.LastFolder))
                dialog.SelectedPath = Settings.Default.LastFolder;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;
            Photos.Path = Settings.Default.LastFolder = ImagesDir.Text = dialog.SelectedPath;
            Settings.Default.Save();
        }

        private void PhotosListBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Delete || e.Key == Key.Back)
                MarkAsDeleted(sender, e);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var photosToDelete = Photos.Where(x => x.MarkedForDeletion).ToArray();
            if (!photosToDelete.Any())
            {
                MessageBox.Show("Nothing to delete");
                return;
            }
            foreach (var photo in photosToDelete)
            {
                Photos.Remove(photo);
                if (File.Exists(photo.Source))
                {
                    FileSystem.DeleteFile(photo.Source,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin);
                }
                DbProvider.Delete(photo.Source);
            }
            //MessageBox.Show("Deleted");
        }
    }
}