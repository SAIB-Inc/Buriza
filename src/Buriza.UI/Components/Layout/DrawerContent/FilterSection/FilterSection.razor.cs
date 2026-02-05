namespace Buriza.UI.Components.Layout;

public partial class FilterSection
{
    private bool SentSelected { get; set; }
    private bool ReceiveSelected { get; set; }
    private bool SwapsSelected { get; set; }
    private bool StakingSelected { get; set; }
    private DateTime? FromDate { get; set; }
    private DateTime? ToDate { get; set; }
    private string SelectedDateRange { get; set; } = string.Empty;
    private string SelectedStatus { get; set; } = string.Empty;
    private decimal? MinAmount { get; set; }
    private decimal? MaxAmount { get; set; }

    private void HandleReset()
    {
        ResetTransactionType();
        ResetDateRange();
        ResetStatus();
        MinAmount = null;
        MaxAmount = null;
    }

    private void ResetTransactionType()
    {
        SentSelected = false;
        ReceiveSelected = false;
        SwapsSelected = false;
        StakingSelected = false;
    }

    private void ResetDateRange()
    {
        FromDate = null;
        ToDate = null;
        SelectedDateRange = string.Empty;
    }

    private void ResetStatus()
    {
        SelectedStatus = string.Empty;
    }

    private void HandleApply()
    {
        // TODO: Apply filters and close drawer
    }
}
