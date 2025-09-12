namespace Buriza.UI.Services;

//transfer to Buriza.Data

public enum DrawerContentType
{
    None,
    Summary,        // for history page
    AuthorizeDapp,  // for dapp page  
    Receive,        // button click
    Send,           // button click
    SelectAsset,    // select asset from send section
    TransactionStatus, // transaction success/status
    Settings,       // button click
    Manage          // button click
}

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

    private bool _isFilterDrawerOpen = false;
    public bool IsFilterDrawerOpen
    {
        get => _isFilterDrawerOpen;
        set
        {
            if (_isFilterDrawerOpen != value)
            {
                _isFilterDrawerOpen = value;
                NotifyChanged();
            }
        }
    }

    private DrawerContentType _currentDrawerContent = DrawerContentType.None;
    public DrawerContentType CurrentDrawerContent
    {
        get => _currentDrawerContent;
        set
        {
            if (_currentDrawerContent != value)
            {
                _currentDrawerContent = value;
                NotifyChanged();
            }
        }
    }

    #endregion

    #region methods

    public void SetDrawerContent(DrawerContentType contentType)
    {
        CurrentDrawerContent = contentType;
        IsFilterDrawerOpen = true;
    }

    #endregion

    #region event

    public event Action? OnChanged;

    private void NotifyChanged() => OnChanged?.Invoke();

    #endregion
}