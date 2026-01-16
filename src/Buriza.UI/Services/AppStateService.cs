using Buriza.Data.Models.Enums;

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

    private AssetType _selectedAssetType = AssetType.Token;
    public AssetType SelectedAssetType
    {
        get => _selectedAssetType;
        set
        {
            if (_selectedAssetType != value)
            {
                _selectedAssetType = value;
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

    public int SelectedAssetTypeIndex
    {
        get => (int)_selectedAssetType;
        set
        {
            if ((int)_selectedAssetType != value)
            {
                _selectedAssetType = (AssetType)value;
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

    private bool _isSendConfirmed = false;
    public bool IsSendConfirmed
    {
        get => _isSendConfirmed;
        set
        {
            if (_isSendConfirmed != value)
            {
                _isSendConfirmed = value;
                NotifyChanged();
            }
        }
    }

    private bool _isReceiveAdvancedMode = false;
    public bool IsReceiveAdvancedMode
    {
        get => _isReceiveAdvancedMode;
        set
        {
            if (_isReceiveAdvancedMode != value)
            {
                _isReceiveAdvancedMode = value;
                NotifyChanged();
            }
        }
    }

    private bool _isManageAccountFormVisible = false;
    public bool IsManageAccountFormVisible
    {
        get => _isManageAccountFormVisible;
        set
        {
            if (_isManageAccountFormVisible != value)
            {
                _isManageAccountFormVisible = value;
                NotifyChanged();
            }
        }
    }

    private bool _isManageEditMode = false;
    public bool IsManageEditMode
    {
        get => _isManageEditMode;
        set
        {
            if (_isManageEditMode != value)
            {
                _isManageEditMode = value;
                NotifyChanged();
            }
        }
    }

    private SidebarContentType _currentSidebarContent = SidebarContentType.None;
    public SidebarContentType CurrentSidebarContent
    {
        get => _currentSidebarContent;
        set
        {
            if (_currentSidebarContent != value)
            {
                _currentSidebarContent = value;
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

    public void RequestAddRecipient()
    {
        OnAddRecipientRequested?.Invoke();
    }

    public void RequestResetSendConfirmation()
    {
        OnResetSendConfirmationRequested?.Invoke();
    }

    public void HideManageAccountForm()
    {
        IsManageAccountFormVisible = false;
        IsManageEditMode = false;
    }

    #endregion

    #region callback events

    public event Action? OnAddRecipientRequested;
    public event Action? OnResetSendConfirmationRequested;

    #endregion

    #region event

    public event Action? OnChanged;

    private void NotifyChanged() => OnChanged?.Invoke();

    #endregion
}