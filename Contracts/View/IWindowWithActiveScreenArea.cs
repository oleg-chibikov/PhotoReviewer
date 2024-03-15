using System.Drawing;
using Scar.Common.WPF.Contracts;

namespace PhotoReviewer.Contracts.View;

public interface IWindowWithActiveScreenArea : IWindow
{
    Rectangle ActiveScreenArea { get; }
}