using FastEndpoints;
using PaylKoyn.API.Defaults;
using PaylKoyn.Data.Models.Api.Response.Data;

namespace PaylKoyn.API.Endpoints.Epoch;

public class GetProtocolParameters(IConfiguration configuration) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/epochs/latest/parameters");
        AllowAnonymous();

        Description(d => d
            .WithTags("Epoch")
            .Produces<ProtocolParametersResponse[]>(StatusCodes.Status200OK)
            .ProducesProblemFE(StatusCodes.Status400BadRequest)
            .ProducesProblemFE(StatusCodes.Status500InternalServerError)
            .WithName("GetProtocolParameters")
        );
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int networkMagic = configuration.GetValue<int>("CardanoNetworkMagic");

        ProtocolParametersResponse response = networkMagic switch
        {
            764824073 => PParamsDefaults.Mainnet(),
            2 => PParamsDefaults.Preview(),
            _ => throw new NotImplementedException()
        };

        await SendOkAsync(response, cancellation: ct);

    }


}
