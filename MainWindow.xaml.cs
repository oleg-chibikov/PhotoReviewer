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
            var pvWindow = new PhotoView((Photo)PhotosListBox.SelectedItem) { Owner = this };
            pvWindow.Show();
        }

        private void MarkAsDeleted(object sender, RoutedEventArgs e)
        {
            MarkAsDeleted();
        }

        private void Favorite(object sender, RoutedEventArgs e)
        {
            Favorite();
        }

        private void MarkAsDeleted()
        {
            var photos = PhotosListBox.SelectedItems;
            foreach (Photo photo in photos)
            {
                if (photo.MarkedForDeletion)
                {
                    photo.MarkedForDeletion = false;
                    DbProvider.Delete(photo.Source, DbProvider.OperationType.MarkForDeletion);
                }
                else
                {
                    photo.MarkedForDeletion = true;
                    DbProvider.Save(photo.Source, DbProvider.OperationType.MarkForDeletion);
                }
            }
        }

        private void Favorite()
        {
            var photos = PhotosListBox.SelectedItems;
            foreach (Photo photo in photos)
            {
                if (photo.Favorited)
                {
                    photo.Favorited = false;
                    DbProvider.Delete(photo.Source, DbProvider.OperationType.Favorite);
                }
                else
                {
                    photo.Favorited = true;
                    DbProvider.Save(photo.Source, DbProvider.OperationType.Favorite);
                }
            }
        }

        private void OpenInExplorer(object sender, RoutedEventArgs e)
        {
            OpenInExplorer(((Photo)PhotosListBox.SelectedItem).Source);
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
            if (PhotosListBox.HasItems)
                PhotosListBox.SelectedIndex = 0;
        }

        private void PhotosListBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Delete:
                case Key.Back:
                    MarkAsDeleted();
                    break;
                case Key.Enter:
                    Favorite();
                    break;
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var photos = Photos.Where(x => x.MarkedForDeletion).ToArray();
            if (!photos.Any())
            {
                MessageBox.Show("Nothing to delete");
                return;
            }
            foreach (var photo in photos)
            {
                Photos.Remove(photo);
                if (File.Exists(photo.Source))
                {
                    FileSystem.DeleteFile(photo.Source,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin);
                }
                DbProvider.Delete(photo.Source, DbProvider.OperationType.MarkForDeletion);
            }
        }

        private void MoveFavoritedButton_Click(object sender, RoutedEventArgs e)
        {
            var photos = Photos.Where(x => x.Favorited).ToArray();
            if (!photos.Any())
            {
                MessageBox.Show("Nothing to move");
                return;
            }
            var dir = Path.GetDirectoryName(photos.First().Source) + "\\Favorite\\";
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            foreach (var photo in photos)
            {
                if (!File.Exists(photo.Source))
                    continue;
                var newName = dir + Path.GetFileName(photo.Source);
                if (!File.Exists(newName))
                    File.Copy(photo.Source, newName);
            }
            Process.Start(dir);
        }

        private void ImagesDir_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;
            Photos.Path = Settings.Default.LastFolder = ImagesDir.Text;
            Settings.Default.Save();
            if (PhotosListBox.HasItems)
                PhotosListBox.SelectedIndex = 0;
        }

        private void RenameToDate(object sender, RoutedEventArgs e)
        {
            var photos = PhotosListBox.SelectedItems.Cast<Photo>().ToArray();
            if (!photos.Any())
            {
                MessageBox.Show("Nothing to rename");
                return;
            }
            var dir = Path.GetDirectoryName(photos.First().Source);
            foreach (var photo in photos)
            {
                var oldPath = photo.Source;
                if (!File.Exists(photo.Source) || !photo.Metadata.DateImageTaken.HasValue)
                    continue;
                var newName = photo.Metadata.DateImageTaken.Value.ToString("yyyy-MM-dd hh-mm-ss");
                var newPath = $"{dir}\\{newName}.jpg";
                if (!File.Exists(newPath))
                    File.Move(oldPath, newPath);
                photo.Name = newName;
                photo.Source = newPath;
                DbProvider.Delete(oldPath, DbProvider.OperationType.MarkForDeletion);
                DbProvider.Delete(oldPath, DbProvider.OperationType.Favorite);
                DbProvider.Save(oldPath, DbProvider.OperationType.MarkForDeletion);
                DbProvider.Save(oldPath, DbProvider.OperationType.Favorite);
            }
            OpenInExplorer(((Photo)PhotosListBox.SelectedItem).Source);
        }

        private static void OpenInExplorer(string filePath)
        {
            new Process
            {
                StartInfo =
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{filePath}\""
                }
            }.Start();
        }
    }
}