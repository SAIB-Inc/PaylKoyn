using FastEndpoints;

namespace PaylKoyn.Node.Endpoints;

public record TestRequest
{
    public string? Name { get; init; }
}

public class Test : Endpoint<TestRequest>
{
    public override void Configure()
    {
        Post("/test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(TestRequest req, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(req.Name))
        {
            return;
        }

        await SendAsync($"Hello, {req.Name}!", cancellation: ct);
    }
}