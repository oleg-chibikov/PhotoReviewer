using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using PhotoReviewer.Contracts.View;
using Scar.Common.WPF.View;

namespace PhotoReviewer.View
{
    [UsedImplicitly]
    internal sealed class PhotoWindowCreator : IWindowCreator<IPhotoWindow>
    {
        [NotNull]
        private readonly IWindowFactory<IMainWindow> _mainWindowFactory;

        [NotNull]
        private readonly Func<IMainWindow, IPhotoWindow> _photoWindowFactory;

        public PhotoWindowCreator([NotNull] IWindowFactory<IMainWindow> mainWindowFactory, [NotNull] Func<IMainWindow, IPhotoWindow> photoWindowFactory)
        {
            _mainWindowFactory = mainWindowFactory ?? throw new ArgumentNullException(nameof(mainWindowFactory));
            _photoWindowFactory = photoWindowFactory ?? throw new ArgumentNullException(nameof(photoWindowFactory));
        }

        public async Task<IPhotoWindow> CreateWindowAsync(CancellationToken cancellationToken)
        {
            var mainWindow = await _mainWindowFactory.GetWindowIfExistsAsync(cancellationToken).ConfigureAwait(false);
            return _photoWindowFactory(mainWindow);
        }
    }
}