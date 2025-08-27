namespace Buriza.UI.Components.Pages;

public partial class Dapp
{
    protected int ExpandedCard = 1;
    protected bool ShowAuthorization = false;
    protected string SelectedCategory = "All";
    
    protected void ExpandCard(int cardIndex)
    {
        ExpandedCard = cardIndex;
    }
    
    protected void OnDappCardClicked()
    {
        ShowAuthorization = true;
    }
}