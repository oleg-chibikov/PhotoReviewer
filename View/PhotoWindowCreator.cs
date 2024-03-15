using PhotoReviewer.Contracts.View;
using Scar.Common.View.WindowCreation;

namespace PhotoReviewer.View;

public sealed class PhotoWindowCreator(
        IWindowFactory<IMainWindow> mainWindowFactory,
        Func<IMainWindow, IPhotoWindow> photoWindowFactory)
    : IWindowCreator<IPhotoWindow>
{
    readonly IWindowFactory<IMainWindow> _mainWindowFactory = mainWindowFactory ?? throw new ArgumentNullException(nameof(mainWindowFactory));

    readonly Func<IMainWindow, IPhotoWindow> _photoWindowFactory = photoWindowFactory ?? throw new ArgumentNullException(nameof(photoWindowFactory));

    public async Task<IPhotoWindow> CreateWindowAsync(CancellationToken cancellationToken)
    {
        var mainWindow = await _mainWindowFactory.GetWindowAsync(cancellationToken).ConfigureAwait(false);
        return _photoWindowFactory(mainWindow);
    }
}
