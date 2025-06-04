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
        string provider = configuration["CardanoProvider"] ?? "Blockfrost";
        ulong networkMagic = configuration.GetValue<ulong>("CardanoNodeConnection:NetworkMagic", 2);
        NetworkType networkType = networkMagic switch
        {
            764824073 => NetworkType.Mainnet,
            2 => NetworkType.Preview,
            _ => NetworkType.Preprod
        };

        switch (provider)
        {
            case "Ouroboros":
                string nodeSocketPath = configuration["CardanoNodeConnection:SocketPath"]
                    ?? throw new ArgumentException("CardanoNodeSocketPath is not configured in the application settings.");
                services.AddSingleton<ICardanoDataProvider>(provider => new Ouroboros(nodeSocketPath, networkMagic));
                break;
            default:
                string apiKey = configuration["BlockfrostApiKey"] ??
                    throw new ArgumentException("BlockfrostApiKey is not configured in the application settings.");

                services.AddSingleton<ICardanoDataProvider>(provider => new Blockfrost(apiKey, networkType));
                break;
        }

        return services;
    }
}