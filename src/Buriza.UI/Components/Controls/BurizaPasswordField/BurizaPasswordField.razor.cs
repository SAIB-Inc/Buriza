using MudBlazor;

namespace Buriza.UI.Components.Controls;

public partial class BurizaPasswordField
{
    protected string Password {get; set;} = String.Empty;
    protected bool ShowPassword {get; set; } = false;
    protected InputType PasswordInputType {get; set; } = InputType.Password;

    protected void TogglePasswordVisibility()
    {
        ShowPassword = !ShowPassword;
        PasswordInputType = ShowPassword ? InputType.Text : InputType.Password;
    }
}