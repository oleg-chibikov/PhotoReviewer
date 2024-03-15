using System.Globalization;
using System.IO;
using System.Windows;
using Autofac;
using Microsoft.Extensions.Logging;
using PhotoReviewer.Contracts.View;
using PhotoReviewer.Core;
using PhotoReviewer.DAL;
using PhotoReviewer.DAL.Model;
using PhotoReviewer.Resources;
using PhotoReviewer.View;
using PhotoReviewer.ViewModel;
using Scar.Common.Async;
using Scar.Common.Comparers;
using Scar.Common.ImageProcessing.ExifExtraction;
using Scar.Common.ImageProcessing.MetadataExtraction;
using Scar.Common.Messages;
using Scar.Common.MVVM.Commands;
using Scar.Common.Processes;
using Scar.Common.RateLimiting;
using Scar.Common.View.AutofacWindowProvision;
using Scar.Common.View.WindowCreation;
using Scar.Common.WPF.ImageRetrieval;
using Scar.Memories.DAL;
using Scar.Memories.View;
using Scar.Memories.ViewModel;
using Serilog;

namespace PhotoReviewer.Launcher;

public sealed partial class App
{
    static Scar.Memories.Data.Settings? _memoriesSettings;

    public App() : base(alreadyRunningMessage: Errors.AlreadyRunning, configureLogging: (hostBuilderContext, loggingBuilder) =>
    {
        if (File.Exists(PathsProvider.LogsPath))
        {
            File.Delete(PathsProvider.LogsPath);
        }

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.File(PathsProvider.LogsPath, formatProvider: CultureInfo.InvariantCulture)
            .MinimumLevel.Verbose()
            .CreateLogger();

        loggingBuilder.ClearProviders().AddSerilog();
        loggingBuilder.SetMinimumLevel(LogLevel.Trace);

        var configuration = hostBuilderContext.Configuration;
        var appSettings = configuration.GetSection("AppSettings");
        _memoriesSettings = new Scar.Memories.Data.Settings(
            appSettings[nameof(Scar.Memories.Data.Settings.DataFolder)] ?? "./");
    })
    {
        InitializeComponent();
    }

    protected override async Task OnStartupAsync()
    {
        var logger = Container.Resolve<ILogger<App>>();
        logger.LogTrace("Starting...");
        await Container.Resolve<WindowFactory<IMainWindow>>().ShowWindowAsync(CancellationToken.None).ConfigureAwait(false);
        logger.LogInformation("Started");
    }

    protected override void RegisterDependencies(ContainerBuilder builder)
    {
        if (_memoriesSettings != null)
        {
            builder.RegisterInstance(_memoriesSettings).AsSelf().AsImplementedInterfaces().SingleInstance();
        }

        builder.RegisterGeneric(typeof(WindowFactory<>)).SingleInstance();
        builder.RegisterType<WinComparer>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<MetadataExtractor>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<ImageRetriever>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<ProcessUtility>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<ExifTool>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<ImagesDirectoryWatcher>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<AutofacScopedWindowProvider>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<GenericWindowCreator<IMainWindow>>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<WindowDisplayer>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<RateLimiter>().AsImplementedInterfaces().InstancePerDependency();
        builder.RegisterType<ApplicationCommandManager>().AsImplementedInterfaces().SingleInstance();

        builder.RegisterType<PhotoInfoRepository<FavoritedPhoto>>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<PhotoInfoRepository<MarkedForDeletionPhoto>>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<SettingsRepository>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<PhotoUserInfoRepository>().AsImplementedInterfaces().SingleInstance();

        builder.RegisterType<WindowsArranger>().AsSelf().SingleInstance();
        builder.RegisterType<CancellationTokenSourceProvider>().AsImplementedInterfaces().InstancePerDependency();

        builder.RegisterAssemblyTypes(typeof(MainWindow).Assembly).AsImplementedInterfaces().InstancePerDependency();
        builder.RegisterAssemblyTypes(typeof(MainViewModel).Assembly).Where(x => !x.Name.Contains("ProcessedByFody", StringComparison.CurrentCultureIgnoreCase)).AsSelf().InstancePerDependency();
        builder.RegisterType<UiThreadRunner>().AsSelf().SingleInstance();
        builder.RegisterType<GalleryViewModel>().AsSelf().InstancePerDependency();
        builder.RegisterType<GalleryWindow>().AsSelf().InstancePerDependency();
        builder.RegisterType<GenericWindowCreator<GalleryWindow>>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<WindowFactory<GalleryWindow>>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<FileRecordRepositoryFactory>().AsImplementedInterfaces().SingleInstance();
    }

    protected override void ShowMessage(Message message)
    {
        _ = message ?? throw new ArgumentNullException(nameof(message));
        var image = message.Type switch
        {
            MessageType.Message => MessageBoxImage.Information,
            MessageType.Warning => MessageBoxImage.Warning,
            MessageType.Error => MessageBoxImage.Error,
            _ => throw new ArgumentOutOfRangeException(nameof(message))
        };

        MessageBox.Show(message.Text, nameof(PhotoReviewer), MessageBoxButton.OK, image);
    }
}
