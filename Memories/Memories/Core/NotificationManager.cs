using Microsoft.Toolkit.Uwp.Notifications;
using PhotoReviewer.Memories.View;
using Scar.Common.MVVM.Commands;
using Scar.Common.View.WindowCreation;

namespace PhotoReviewer.Memories.Core;

public class NotificationManager(IUiThreadRunner uiThreadRunner, IWindowFactory<GalleryWindow> galleryWindowFactory)
{
    public void ShowNotification(string title, string buttonText)
    {
        new ToastContentBuilder()
            .AddText(title)
            .AddButton(new ToastButton()
                .SetContent(buttonText)
                .SetBackgroundActivation())
            .Show();
        ToastNotificationManagerCompat.OnActivated += _ =>
        {
            uiThreadRunner.Run(
                 () =>
                {
                    galleryWindowFactory.ShowWindowAsync(default);
                });
        };
    }
}
