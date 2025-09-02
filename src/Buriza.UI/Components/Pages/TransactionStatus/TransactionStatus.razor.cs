using Microsoft.AspNetCore.Components;

namespace Buriza.UI.Components.Pages;

public partial class TransactionStatus
{
    [Inject]
    public required NavigationManager Navigation { get; set; }
    
    private void NavigateToSend()
    {
        Navigation.NavigateTo("/send");
    }
}