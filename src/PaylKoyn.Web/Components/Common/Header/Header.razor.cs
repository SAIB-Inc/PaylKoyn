using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using PaylKoyn.Web.Services;

namespace PaylKoyn.Web.Components.Common;

public partial class Header
{
    [Inject]
    public required NavigationManager NavigationManager { get; set; }

    [Inject]
    public required DAppBridgeService DAppBridgeService { get; set; }

    public string CurrentUrl => new Uri(NavigationManager.Uri).AbsolutePath;

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
        }
        catch (JSException ex)
        {
            Console.WriteLine($"Error connecting to wallet: {ex.Message}");
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