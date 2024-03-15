using PhotoReviewer.Memories.Core;
using Scar.Common.Console.Startup;

namespace PhotoReviewer.Memories.Launcher;

sealed class Program
{
    static async Task Main()
    {
        if (!UriParser.IsKnownScheme("pack"))
        {
            // pack:// scheme is not yet registered. This scheme is registered when you create the Application object (for example when running from console)
            _ = new System.Windows.Application();
        }

        await new ConsoleLauncher().SetupAsync(
            RegistrationExtensions.RegisterAll,
            RegistrationExtensions.LaunchMemoriesAsync,
            RegistrationExtensions.CreateSettings).ConfigureAwait(false);
    }
}