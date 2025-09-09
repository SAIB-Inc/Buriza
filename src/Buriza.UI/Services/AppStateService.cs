namespace Buriza.UI.Services;

public class AppStateService
{
    #region properties

    private bool _isDarkMode = true;
    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (_isDarkMode != value)
            {
                _isDarkMode = value;
                NotifyChanged();
            }
        }
    }

    private bool _isSidebarOpen = false;
    public bool IsSidebarOpen
    {
        get => _isSidebarOpen;
        set
        {
            if (_isSidebarOpen != value)
            {
                _isSidebarOpen = value;
                NotifyChanged();
            }
        }
    }

    #endregion

    #region event

    public event Action? OnChanged;

    private void NotifyChanged() => OnChanged?.Invoke();

    #endregion
}