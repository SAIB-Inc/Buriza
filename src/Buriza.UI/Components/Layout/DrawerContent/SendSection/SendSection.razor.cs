using Buriza.Data.Models.Common;
using Buriza.UI.Resources;
using Buriza.UI.Services;
using Microsoft.AspNetCore.Components;
using static Buriza.Data.Models.Enums.DrawerContentType;

namespace Buriza.UI.Components.Layout;

public partial class SendSection : IDisposable
{
    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required AppStateService AppStateService { get; set; }

    protected bool IsConfirmed { get; set; }

    private List<Recipient> recipients = [new Recipient { Id = 1, TokenEntries = [new TokenEntry { ImageSrc = Tokens.Ada }] }];

    protected override void OnInitialized()
    {
        AppStateService.OnAddRecipientRequested += AddRecipient;
        AppStateService.OnResetSendConfirmationRequested += ResetConfirmation;
        AppStateService.OnChanged += OnAppStateChanged;
    }

    public void Dispose()
    {
        AppStateService.OnAddRecipientRequested -= AddRecipient;
        AppStateService.OnResetSendConfirmationRequested -= ResetConfirmation;
        AppStateService.OnChanged -= OnAppStateChanged;
    }

    private void OnAppStateChanged()
    {
        if (!AppStateService.IsFilterDrawerOpen && IsConfirmed)
        {
            IsConfirmed = false;
            AppStateService.IsSendConfirmed = false;
            StateHasChanged();
        }
    }

    private void AddRecipient()
    {
        recipients.Add(new Recipient { Id = recipients.Count + 1 });
        StateHasChanged();
    }

    private void ResetConfirmation()
    {
        IsConfirmed = false;
        AppStateService.IsSendConfirmed = false;
        StateHasChanged();
    }
    
    private void RemoveRecipient(Recipient recipient)
    {
        if (recipients.Count > 1)
        {
            recipients.Remove(recipient);
        }
    }
    
    private void AddToken(Recipient recipient)
    {
        recipient.TokenEntries.Add(new TokenEntry { ImageSrc = Tokens.Ada });
    }
    
    private void RemoveToken(Recipient recipient, TokenEntry entry)
    {
        if (recipient.TokenEntries.Count > 1)
        {
            recipient.TokenEntries.Remove(entry);
        }
    }
    
    private void HandleSend()
    {
        if (IsConfirmed)
        {
            // Change drawer content to TransactionStatus instead of navigating away
            AppStateService.SetDrawerContent(TransactionStatus);
        }
        else
        {
            IsConfirmed = true;
            AppStateService.IsSendConfirmed = true;
            StateHasChanged();
        }
    }

    private void HandleSelectAsset()
    {
        // Change drawer content to SelectAsset section
        AppStateService.SetDrawerContent(SelectAsset);
    }

     private bool IsValidAddress(string address)
    {
        // Check if address is valid (@TODO: Implement proper Cardano address validation)
        if (string.IsNullOrWhiteSpace(address)) return false;
        if (address.Length < 59) return false;
        return address.StartsWith("addr1") || address.StartsWith("addr_test1");
    }
}