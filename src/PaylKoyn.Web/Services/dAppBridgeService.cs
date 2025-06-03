using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace PaylKoyn.Web.Services;

public record CardanoWallet
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; init; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
}

public class DAppBridgeService(IJSRuntime jsRuntime)
{
    public async Task<IEnumerable<CardanoWallet>> GetWalletsAsync()
    {
        return await jsRuntime.InvokeAsync<IEnumerable<CardanoWallet>>("window.listWallets");
    }

    public async Task ConnectWalletAsync(string walletId)
    {
        await jsRuntime.InvokeVoidAsync("window.connectWalletById", walletId);
    }
}