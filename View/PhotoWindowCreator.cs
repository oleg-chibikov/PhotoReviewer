using System;
using System.Threading;
using System.Threading.Tasks;
using PhotoReviewer.Contracts.View;
using Scar.Common.View.WindowCreation;

namespace PhotoReviewer.View
{
    public sealed class PhotoWindowCreator : IWindowCreator<IPhotoWindow>
    {
        readonly IWindowFactory<IMainWindow> _mainWindowFactory;

        readonly Func<IMainWindow, IPhotoWindow> _photoWindowFactory;

        public PhotoWindowCreator(IWindowFactory<IMainWindow> mainWindowFactory, Func<IMainWindow, IPhotoWindow> photoWindowFactory)
        {
            _mainWindowFactory = mainWindowFactory ?? throw new ArgumentNullException(nameof(mainWindowFactory));
            _photoWindowFactory = photoWindowFactory ?? throw new ArgumentNullException(nameof(photoWindowFactory));
        }

        public async Task<IPhotoWindow> CreateWindowAsync(CancellationToken cancellationToken)
        {
            var mainWindow = await _mainWindowFactory.GetWindowAsync(cancellationToken).ConfigureAwait(false);
            return _photoWindowFactory(mainWindow);
        }
    }
}
