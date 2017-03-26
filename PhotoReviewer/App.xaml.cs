using System;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using Autofac;
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
using Scar.Common.Drawing;
using Scar.Common.Logging;
using Scar.Common.WPF;
using Scar.Common.WPF.Localization;

namespace PhotoReviewer
{
    public partial class App
    {
        private static readonly Guid AppGuid = AssemblyUtility.GetAssemblyGuid();
        private ILifetimeScope container;
        private IMessenger messenger;
        private Mutex mutex;

        protected override void OnStartup([NotNull] StartupEventArgs e)
        {
            if (e == null)
                throw new ArgumentNullException(nameof(e));
            DispatcherHelper.Initialize();
            RegisterDependencies();

            //CultureUtilities.ChangeCulture(container.Resolve<ISettingsRepository>().Get().UiLanguage);

            messenger = container.Resolve<IMessenger>();
            messenger.Register<string>(this, MessengerTokens.UiLanguageToken, CultureUtilities.ChangeCulture);
            messenger.Register<string>(this, MessengerTokens.UserMessageToken, message => MessageBox.Show(message, nameof(PhotoReviewer), MessageBoxButton.OK, MessageBoxImage.Information));
            messenger.Register<string>(this, MessengerTokens.UserWarningToken, message => MessageBox.Show(message, nameof(PhotoReviewer), MessageBoxButton.OK, MessageBoxImage.Warning));
            if (VerifyNotLaunched())
                return;

            container.Resolve<WindowFactory<IMainWindow>>().GetOrCreateWindow().ShowDialog();
        }

        private bool VerifyNotLaunched()
        {
            var sid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            var mutexSecurity = new MutexSecurity();
            mutexSecurity.AddAccessRule(new MutexAccessRule(sid, MutexRights.FullControl, AccessControlType.Allow));
            mutexSecurity.AddAccessRule(new MutexAccessRule(sid, MutexRights.ChangePermissions, AccessControlType.Deny));
            mutexSecurity.AddAccessRule(new MutexAccessRule(sid, MutexRights.Delete, AccessControlType.Deny));
            bool createdNew;
            mutex = new Mutex(false, $"Global\\{nameof(PhotoReviewer)}-{AppGuid}", out createdNew, mutexSecurity);

            if (!mutex.WaitOne(0, false))
            {
                messenger.Send(Errors.AlreadyRunning, MessengerTokens.UserWarningToken);
                return true;
            }
            return false;
        }

        private void RegisterDependencies()
        {
            Trace.CorrelationManager.ActivityId = Guid.NewGuid();

            var builder = new ContainerBuilder();

            builder.RegisterGeneric(typeof(WindowFactory<>)).SingleInstance();
            builder.RegisterType<WinComparer>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<MetadataExtractor>().AsImplementedInterfaces().SingleInstance();
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

            container = builder.Build();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            container.Dispose();
            mutex.Dispose();
        }
    }
}