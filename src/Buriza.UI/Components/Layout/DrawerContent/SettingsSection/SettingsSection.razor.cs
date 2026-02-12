using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using static Buriza.Data.Models.Enums.DrawerContentType;

namespace Buriza.UI.Components.Layout;

public partial class SettingsSection : ComponentBase, IDisposable
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    [Parameter]
    public EventCallback OnAccountSettingsClick { get; set; }

    [Parameter]
    public EventCallback OnNodeClick { get; set; }

    [Parameter]
    public EventCallback OnSeeAllWallets { get; set; }

    [Parameter]
    public EventCallback<string> OnSwitchAccount { get; set; }

    protected bool IsDarkMode
    {
        get => AppStateService.IsDarkMode;
        set => AppStateService.IsDarkMode = value;
    }

    protected override void OnInitialized()
    {
        AppStateService.OnChanged += StateHasChanged;
    }

    public void Dispose()
    {
        AppStateService.OnChanged -= StateHasChanged;
    }

    protected async Task HandleAccountSettingsClick()
    {
        if (OnAccountSettingsClick.HasDelegate)
            await OnAccountSettingsClick.InvokeAsync();
        else
            AppStateService.SetDrawerContent(AccountSettings);
    }

    protected async Task HandleNodeClick()
    {
        if (OnNodeClick.HasDelegate)
            await OnNodeClick.InvokeAsync();
        else
            AppStateService.SetDrawerContent(NodeSettings);
    }

    protected async Task HandleSeeAllWallets()
    {
        if (OnSeeAllWallets.HasDelegate)
            await OnSeeAllWallets.InvokeAsync();
        else
            AppStateService.SetDrawerContent(SwitchWallet);
    }

    protected async Task HandleSwitchAccount(string walletName)
    {
        AppStateService.SelectedWalletName = walletName;
        if (OnSwitchAccount.HasDelegate)
            await OnSwitchAccount.InvokeAsync(walletName);
        else
            AppStateService.SetDrawerContent(SwitchAccount);
    }
}
