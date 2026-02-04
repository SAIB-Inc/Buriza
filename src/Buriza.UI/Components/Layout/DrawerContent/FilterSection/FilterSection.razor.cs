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
        SentSelected = false;
        ReceiveSelected = false;
        SwapsSelected = false;
        StakingSelected = false;
        FromDate = null;
        ToDate = null;
        SelectedDateRange = string.Empty;
        SelectedStatus = string.Empty;
        MinAmount = null;
        MaxAmount = null;
    }

    private void HandleApply()
    {
        // TODO: Apply filters and close drawer
    }
}
