using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using MudBlazor;
using PaylKoyn.Web.Components.Dialogs;
using PaylKoyn.Web.Services;

namespace PaylKoyn.Web.Components.Common;

public partial class Header
{
    [Inject]
    public required NavigationManager NavigationManager { get; set; }

    [Inject]
    public required DAppBridgeService DAppBridgeService { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    public string CurrentUrl => new Uri(NavigationManager.Uri).AbsolutePath;
    protected CardanoWallet? SelectedWallet { get; set; } = null;
    public IEnumerable<CardanoWallet> ConnectedWallets { get; set; } = [];

    protected override void OnInitialized()
    {
        base.OnInitialized();
        NavigationManager.LocationChanged += OnLocationChanged;
    }

    private async void OnLocationChanged(object? sender, LocationChangedEventArgs e) => await InvokeAsync(StateHasChanged);

    private async void OnWalletConnectClicked()
    {
        try
        {
            ConnectedWallets = await DAppBridgeService.GetWalletsAsync();
            await InvokeAsync(StateHasChanged);
            await OpenWalletSelectionDialogAsync();
        }
        catch (JSException ex)
        {
            Console.WriteLine($"Error connecting to wallet: {ex.Message}");
        }
    }

    protected async Task OpenWalletSelectionDialogAsync()
    {
        DialogParameters parameters = new()
        {
            {"ConnectedWallets", ConnectedWallets}
        };
        
        DialogOptions options = new()
        {
            CloseOnEscapeKey = true,
            CloseButton = true,
            FullWidth = true,
            MaxWidth = MaxWidth.Large 
        };
        
        IDialogReference dialog = await DialogService.ShowAsync<WalletSelectionDialog>("Wallet Selection Dialog", parameters, options);
        DialogResult? result = await dialog.Result;
        
        if (result is not null && !result.Canceled && result.Data is CardanoWallet selectedWallet)
        {
            SelectedWallet = selectedWallet;
            StateHasChanged();
        }
    }

    private async void OnSpecificWalletConnectClicked(string walletId)
    {
        try
        {
            await DAppBridgeService.ConnectWalletAsync(walletId);
        }
        catch
        {

        }
    }
}