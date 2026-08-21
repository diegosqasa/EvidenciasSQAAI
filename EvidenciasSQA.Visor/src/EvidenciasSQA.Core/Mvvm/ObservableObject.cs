using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EvidenciasSQA.Core.Mvvm;

/// <summary>
/// Base INotifyPropertyChanged del patrón MVVM, compartida por el Visor y el Editor.
/// (El core ya depende de WPF por DrawingContext; se centraliza aquí para no
/// duplicar código entre los dos ejecutables.)
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
