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
using JetBrains.Annotations;
using PhotoReviewer.Contracts.View;
using PhotoReviewer.Core;
using PhotoReviewer.DAL;
using PhotoReviewer.DAL.Model;
using PhotoReviewer.Resources;
using PhotoReviewer.View;
using PhotoReviewer.ViewModel;
using Scar.Common;
using Scar.Common.Comparers;
using Scar.Common.Drawing.ExifTool;
using Scar.Common.Drawing.ImageRetriever;
using Scar.Common.Drawing.MetadataExtractor;
using Scar.Common.Logging;
using Scar.Common.Processes;
using Scar.Common.WPF.Localization;
using Scar.Common.WPF.View;

namespace PhotoReviewer
{
    internal sealed partial class App
    {
        private static readonly Guid AppGuid = new Guid("9fdf0bfb-637c-4054-b0e1-0249defe1d91");

        [NotNull]
        private readonly ILifetimeScope _container;

        [NotNull]
        private readonly ILog _logger;

        [NotNull]
        private readonly IMessenger _messenger;

        [NotNull]
        private readonly Mutex _mutex;

        public App()
        {
            //TODO:Bindings wModes explicit!
            _container = RegisterDependencies();

            //CultureUtilities.ChangeCulture(container.Resolve<ISettingsRepository>().Get().UiLanguage);

            _messenger = _container.Resolve<IMessenger>();
            _messenger.Register<string>(this, MessengerTokens.UiLanguageToken, CultureUtilities.ChangeCulture);
            _messenger.Register<string>(this, MessengerTokens.UserMessageToken, message => MessageBox.Show(message, nameof(PhotoReviewer), MessageBoxButton.OK, MessageBoxImage.Information));
            _messenger.Register<string>(this, MessengerTokens.UserWarningToken, message => MessageBox.Show(message, nameof(PhotoReviewer), MessageBoxButton.OK, MessageBoxImage.Warning));
            _messenger.Register<string>(this, MessengerTokens.UserErrorToken, message => MessageBox.Show(message, nameof(PhotoReviewer), MessageBoxButton.OK, MessageBoxImage.Error));
            _logger = _container.Resolve<ILog>();
            _mutex = CreateMutex();

            // ReSharper disable once AssignNullToNotNullAttribute
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void App_DispatcherUnhandledException(object sender, [NotNull] DispatcherUnhandledExceptionEventArgs e)
        {
            // Process unhandled exception
            _logger.Fatal("Unhandled exception", e.Exception);
            NotifyError(e.Exception);
            // Prevent default unhandled exception processing
            e.Handled = true;
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

        private void NotifyError(Exception e)
        {
            _messenger.Send($"{Errors.Error}: {e.GetMostInnerException()}", MessengerTokens.UserErrorToken);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _container.Dispose();
            _mutex.Dispose();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            if (!_mutex.WaitOne(0, false))
            {
                _messenger.Send(Errors.AlreadyRunning, MessengerTokens.UserWarningToken);
                return;
            }

            _container.Resolve<WindowFactory<IMainWindow>>().GetOrCreateWindow().ShowDialog();
        }

        [NotNull]
        private ILifetimeScope RegisterDependencies()
        {
            Trace.CorrelationManager.ActivityId = Guid.NewGuid();

            var builder = new ContainerBuilder();

            builder.RegisterGeneric(typeof(WindowFactory<>)).SingleInstance();
            builder.RegisterType<WinComparer>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<MetadataExtractor>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ImageRetriever>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ProcessUtility>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ExifTool>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<Messenger>().AsImplementedInterfaces().SingleInstance();

            builder.RegisterType<PhotoInfoRepository<FavoritedPhoto>>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<PhotoInfoRepository<MarkedForDeletionPhoto>>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<SettingsRepository>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<PhotoUserInfoRepository>().AsImplementedInterfaces().SingleInstance();

            builder.RegisterType<WindowsArranger>().AsSelf().SingleInstance();
            builder.RegisterType<QueueAppendable>().AsImplementedInterfaces().InstancePerDependency();
            builder.RegisterType<CancellationTokenSourceProvider>().AsImplementedInterfaces().InstancePerDependency();
            builder.RegisterModule<LoggingModule>();

            builder.RegisterAssemblyTypes(typeof(MainWindow).Assembly).AsImplementedInterfaces().InstancePerDependency();
            builder.RegisterAssemblyTypes(typeof(MainViewModel).Assembly).AsSelf().InstancePerDependency();

            return builder.Build();
        }

        private void TaskScheduler_UnobservedTaskException(object sender, [NotNull] UnobservedTaskExceptionEventArgs e)
        {
            _logger.Fatal("Unhandled exception", e.Exception);
            NotifyError(e.Exception);
            e.SetObserved();
            e.Exception.Handle(ex => true);
        }
    }
}