using Chrysalis.Tx.Models;
using Chrysalis.Tx.Providers;
using Chrysalis.Wallet.Models.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PaylKoyn.Data.Extensions;

public static class CardanoProviderExtension
{
    public static IServiceCollection AddCardanoProvider(this IServiceCollection services, IConfiguration configuration)
    {
        string blockfrostUrl = configuration["BlockfrostUrl"] ?? string.Empty;
        string apiKey = configuration["BlockfrostApiKey"] ?? string.Empty;
        ulong networkMagic = configuration.GetValue<ulong>("CardanoNodeConnection:NetworkMagic", 2);

        NetworkType networkType = networkMagic switch
        {
            764824073 => NetworkType.Mainnet,
            2 => NetworkType.Preview,
            _ => NetworkType.Preprod
        };

        services.AddSingleton<ICardanoDataProvider>(provider => new Blockfrost(apiKey, networkType, blockfrostUrl));

        return services;
    }
}