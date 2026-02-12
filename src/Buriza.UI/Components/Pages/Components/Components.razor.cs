using Buriza.UI.Services;
using Buriza.UI.Components.Dialogs;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Pages;

public partial class Components
{
    [Inject]
    public required AppStateService AppStateService { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    private string selectedValue = "Item 1";

    private Task OnValueChanged(string value)
    {
        selectedValue = value;
        return Task.CompletedTask;
    }

    private async Task OpenTransactionSummaryDialog()
    {
        DialogOptions options = new()
        {
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true,
            BackdropClick = true
        };

        await DialogService.ShowAsync<TransactionSummaryDialog>(null, options);
    }
}
