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

    #endregion

    #region event

    public event Action? OnChanged;

    private void NotifyChanged() => OnChanged?.Invoke();

    #endregion
}