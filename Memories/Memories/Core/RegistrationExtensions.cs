using Autofac;
using Autofac.Extras.Quartz;
using Microsoft.Extensions.Configuration;
using PhotoReviewer.Memories.DAL;
using PhotoReviewer.Memories.Data;
using PhotoReviewer.Memories.View;
using PhotoReviewer.Memories.ViewModel;
using Scar.Common.ImageProcessing.MetadataExtraction;
using Scar.Common.MVVM.Commands;
using Scar.Common.View.WindowCreation;
using Scar.Common.WPF.ImageRetrieval;
using Scar.Common.WPF.WindowCreation;

namespace PhotoReviewer.Memories.Core;

public static class RegistrationExtensions
{
    public static async Task LaunchMemoriesAsync(ILifetimeScope container)
    {
        container.Resolve<LibraryWatcher>().Watch();
        await Task.WhenAll(
            Task.Run(
                () =>
                    _ = container.Resolve<GalleryViewModel>()),
            Task.Run(
                () =>
                {
                    var jobScheduler = container.Resolve<JobScheduler>();
                    jobScheduler.ScheduleJobsAsync<MemoriesJob>().ConfigureAwait(false);
                    jobScheduler.ScheduleJobsAsync<SynchronizerJob>(JobScheduleOptions.Daily).ConfigureAwait(false);
                })).ConfigureAwait(false);
    }

    public static Settings CreateSettings(IConfigurationSection appSettings)
    {
        _ = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        return new Settings(
            appSettings[nameof(Settings.Environment)] ?? "Development",
            TimeSpan.TryParse(
                appSettings[nameof(Settings.JobRunInterval)],
                out var interval)
                ? interval
                : TimeSpan.FromMinutes(10),
            appSettings[nameof(Settings.DataFolder)] ?? "./",
            appSettings[nameof(Settings.LibraryFolder)] ?? "Z:/");
    }

    public static void Register(this ContainerBuilder builder)
    {
        builder.RegisterType<NotificationManager>().AsSelf().SingleInstance();
        builder.RegisterType<GalleryViewModel>().AsSelf().InstancePerDependency();
        builder.RegisterType<GalleryWindow>().AsSelf().InstancePerDependency();
        builder.RegisterType<GenericWindowCreator<GalleryWindow>>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<WindowFactory<GalleryWindow>>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<JobScheduler>().AsSelf().SingleInstance();
        builder.RegisterType<LibrarySynchronizer>().AsSelf().SingleInstance();
        builder.RegisterType<StaThreadWindowDisplayer>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<ApplicationCommandManager>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<FileRecordRepositoryFactory>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<DirectorySyncStatusRepository>().AsImplementedInterfaces().InstancePerDependency();
        builder.RegisterType<LibraryWatcher>().AsSelf().SingleInstance();
        builder.RegisterModule(new QuartzAutofacFactoryModule());
        builder.RegisterModule(new QuartzAutofacJobsModule(typeof(MemoriesJob).Assembly));
    }

    public static void RegisterAll(this ContainerBuilder builder)
    {
        builder.Register();
        builder.RegisterAdditional();
    }

    static void RegisterAdditional(this ContainerBuilder builder)
    {
        builder.RegisterType<ImageRetriever>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<MetadataExtractor>().AsImplementedInterfaces().SingleInstance();
    }
}
