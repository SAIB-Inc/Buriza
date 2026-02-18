using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Buriza.UI.Components.Dialogs;

public partial class TransactionConfirmationDialog
{
    private string _password = String.Empty;
    private bool _showPassword = false;
    private InputType _passwordInputType = InputType.Password;

    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = default!;

    private void TogglePasswordVisibility()
    {
        _showPassword = !_showPassword;
        _passwordInputType = _showPassword ? InputType.Text : InputType.Password;
    }

    private void Cancel() => MudDialog.Cancel();

    private void Confirm() => MudDialog.Close(DialogResult.Ok(true));
}