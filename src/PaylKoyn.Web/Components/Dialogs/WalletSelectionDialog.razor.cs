using Microsoft.AspNetCore.Components;
using MudBlazor;
using PaylKoyn.Web.Services;

namespace PaylKoyn.Web.Components.Dialogs;

public partial class WalletSelectionDialog
{
    [Inject]
    public required IconService IconService { get; set; }

    [Parameter]
    public IEnumerable<CardanoWallet> ConnectedWallets { get; set; } = [];

    [CascadingParameter]
    protected IMudDialogInstance? MudDialog { get; set; }

    protected void Cancel() => MudDialog?.Cancel();
    private void SelectWallet(CardanoWallet wallet) => MudDialog?.Close(DialogResult.Ok(wallet));
}