using System;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Autofac;
using Common.Logging;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Threading;
using JetBrains.Annotations;
using PhotoReviewer.Core;
using PhotoReviewer.DAL;
using PhotoReviewer.DAL.Model;
using PhotoReviewer.Resources;
using PhotoReviewer.View;
using PhotoReviewer.View.Contracts;
using PhotoReviewer.ViewModel;
using Scar.Common;
using Scar.Common.Comparers;
using Scar.Common.Drawing.ExifTool;
using Scar.Common.Drawing.MetadataExtractor;
using Scar.Common.Logging;
using Scar.Common.Processes;
using Scar.Common.WPF.Localization;
using Scar.Common.WPF.View;

namespace PhotoReviewer
{
    public partial class App
    {
        private static readonly Guid AppGuid = new Guid("9fdf0bfb-637c-4054-b0e1-0249defe1d91");

        [NotNull]
        private readonly ILifetimeScope container;

        [NotNull]
        private readonly ILog logger;

        [NotNull]
        private readonly IMessenger messenger;

        [NotNull]
        private readonly Mutex mutex;

        public App()
        {
            DispatcherHelper.Initialize();
            container = RegisterDependencies();

            //CultureUtilities.ChangeCulture(container.Resolve<ISettingsRepository>().Get().UiLanguage);

            messenger = container.Resolve<IMessenger>();
            messenger.Register<string>(this, MessengerTokens.UiLanguageToken, CultureUtilities.ChangeCulture);
            messenger.Register<string>(this, MessengerTokens.UserMessageToken, message => MessageBox.Show(message, nameof(PhotoReviewer), MessageBoxButton.OK, MessageBoxImage.Information));
            messenger.Register<string>(this, MessengerTokens.UserWarningToken, message => MessageBox.Show(message, nameof(PhotoReviewer), MessageBoxButton.OK, MessageBoxImage.Warning));
            messenger.Register<string>(this, MessengerTokens.UserErrorToken, message => MessageBox.Show(message, nameof(PhotoReviewer), MessageBoxButton.OK, MessageBoxImage.Error));
            logger = container.Resolve<ILog>();
            mutex = CreateMutex();

            // ReSharper disable once AssignNullToNotNullAttribute
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            logger.Fatal("Unhandled exception", e.Exception);
            NotifyError(e.Exception);
            e.SetObserved();
            e.Exception.Handle(ex => true);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Process unhandled exception
            logger.Fatal("Unhandled exception", e.Exception);
            NotifyError(e.Exception);
            // Prevent default unhandled exception processing
            e.Handled = true;
        }

        private void NotifyError(Exception e)
        {
            messenger.Send($"{Errors.Error}: {e.GetMostInnerException().Message}", MessengerTokens.UserErrorToken);
        }

        [NotNull]
        private Mutex CreateMutex()
        {
            var sid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            var mutexSecurity = new MutexSecurity();
            mutexSecurity.AddAccessRule(new MutexAccessRule(sid, MutexRights.FullControl, AccessControlType.Allow));
            mutexSecurity.AddAccessRule(new MutexAccessRule(sid, MutexRights.ChangePermissions, AccessControlType.Deny));
            mutexSecurity.AddAccessRule(new MutexAccessRule(sid, MutexRights.Delete, AccessControlType.Deny));
            bool createdNew;
            return new Mutex(false, $"Global\\{nameof(PhotoReviewer)}-{AppGuid}", out createdNew, mutexSecurity);
        }

        [NotNull]
        private ILifetimeScope RegisterDependencies()
        {
            Trace.CorrelationManager.ActivityId = Guid.NewGuid();

            var builder = new ContainerBuilder();

            builder.RegisterGeneric(typeof(WindowFactory<>)).SingleInstance();
            builder.RegisterType<WinComparer>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<MetadataExtractor>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ProcessUtility>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ExifTool>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<Messenger>().AsImplementedInterfaces().SingleInstance();

            builder.RegisterType<PhotoInfoRepository<FavoritedPhoto>>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<PhotoInfoRepository<MarkedForDeletionPhoto>>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<SettingsRepository>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<PhotoUserInfoRepository>().AsImplementedInterfaces().SingleInstance();

            builder.RegisterAssemblyTypes(typeof(WindowsArranger).Assembly).AsSelf().SingleInstance();
            builder.RegisterModule<LoggingModule>();

            builder.RegisterAssemblyTypes(typeof(MainWindow).Assembly).AsImplementedInterfaces().InstancePerDependency();
            //TODO: register resolving mainwindow with factory
            builder.RegisterAssemblyTypes(typeof(MainViewModel).Assembly).AsSelf().InstancePerDependency();

            return builder.Build();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            if (!mutex.WaitOne(0, false))
            {
                messenger.Send(Errors.AlreadyRunning, MessengerTokens.UserWarningToken);
                return;
            }
            container.Resolve<WindowFactory<IMainWindow>>().GetOrCreateWindow().ShowDialog();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            container.Dispose();
            mutex.Dispose();
        }
    }
}