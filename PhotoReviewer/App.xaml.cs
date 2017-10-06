using System;
using System.Windows;
using Autofac;
using Common.Multithreading;
using PhotoReviewer.Contracts.View;
using PhotoReviewer.Core;
using PhotoReviewer.DAL;
using PhotoReviewer.DAL.Model;
using PhotoReviewer.Resources;
using PhotoReviewer.View;
using PhotoReviewer.ViewModel;
using Scar.Common.Comparers;
using Scar.Common.Drawing.ExifTool;
using Scar.Common.Drawing.ImageRetriever;
using Scar.Common.Drawing.MetadataExtractor;
using Scar.Common.Messages;
using Scar.Common.Processes;
using Scar.Common.WPF.View;

namespace PhotoReviewer
{
    internal sealed partial class App
    {
        protected override string AppGuid { get; } = new Guid("9fdf0bfb-637c-4054-b0e1-0249defe1d91").ToString();
        protected override string AlreadyRunningMessage { get; } = Errors.AlreadyRunning;

        protected override void RegisterDependencies(ContainerBuilder builder)
        {
            builder.RegisterGeneric(typeof(WindowFactory<>)).SingleInstance();
            builder.RegisterType<WinComparer>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<MetadataExtractor>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ImageRetriever>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ProcessUtility>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ExifTool>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ImagesDirectoryWatcher>().AsImplementedInterfaces().SingleInstance();

            builder.RegisterType<PhotoInfoRepository<FavoritedPhoto>>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<PhotoInfoRepository<MarkedForDeletionPhoto>>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<SettingsRepository>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<PhotoUserInfoRepository>().AsImplementedInterfaces().SingleInstance();

            builder.RegisterType<WindowsArranger>().AsSelf().SingleInstance();
            builder.RegisterType<TaskQueue>().AsImplementedInterfaces().InstancePerDependency();
            builder.RegisterType<CancellationTokenSourceProvider>().AsImplementedInterfaces().InstancePerDependency();

            builder.RegisterAssemblyTypes(typeof(MainWindow).Assembly).AsImplementedInterfaces().InstancePerDependency();
            builder.RegisterAssemblyTypes(typeof(MainViewModel).Assembly).AsSelf().InstancePerDependency();
        }

        protected override void ShowMessage(Message message)
        {
            MessageBoxImage image;
            switch (message.Type)
            {
                case MessageType.Message:
                    image = MessageBoxImage.Information;
                    break;
                case MessageType.Warning:
                    image = MessageBoxImage.Warning;
                    break;
                case MessageType.Error:
                    image = MessageBoxImage.Error;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            MessageBox.Show(message.Text, nameof(PhotoReviewer), MessageBoxButton.OK, image);
        }

        protected override void OnStartup()
        {
            Container.Resolve<WindowFactory<IMainWindow>>().ShowWindow();
        }
    }
}