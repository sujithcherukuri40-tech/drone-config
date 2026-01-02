using CommunityToolkit.Mvvm.ComponentModel;

namespace PavanamDroneConfigurator.UI.ViewModels;

public abstract class ViewModelBase : ObservableObject, IDisposable
{
    protected virtual void Dispose(bool disposing)
    {
        // Override in derived classes to cleanup resources
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
