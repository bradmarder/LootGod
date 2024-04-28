using Microsoft.AspNetCore.Mvc.Testing;

namespace LootGodIntegration.Tests;

public class LootTest
{
    [Fact]
    public async Task HealthCheck()
    {
        await using var app = new WebApplicationFactory<Program>();
        using var client = app.CreateDefaultClient();

        var response = await client.GetStringAsync("/healthz");

        Assert.Equal("Healthy", response);
    }
}