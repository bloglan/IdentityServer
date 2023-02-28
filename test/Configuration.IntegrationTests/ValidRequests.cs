using System.Net;
using FluentAssertions;
using IntegrationTests.TestHosts;
using Xunit;
using Duende.IdentityServer.Configuration.Models.DynamicClientRegistration;
using System.Net.Http.Json;
using Duende.IdentityServer.Models;

namespace IntegrationTests;

public class Stuff : ConfigurationIntegrationTestBase
{
    [Fact]
    public async Task valid_request_creates_new_client()
    {
        IdentityServerHost.ApiScopes.Add(new ApiScope("api1"));

        var request = new DynamicClientRegistrationRequest
        {
            RedirectUris = new[] { new Uri("https://example.com/callback") },
            GrantTypes = new[] { "authorization_code" },
            ClientName = "test",
            ClientUri = new Uri("https://example.com"),
            DefaultMaxAge = 10000,
            Scope = "api1 openid profile"
        };
        var httpResponse = await ConfigurationHost.HttpClient!.PostAsJsonAsync("/connect/dcr", request);

        var response = await httpResponse.Content.ReadFromJsonAsync<DynamicClientRegistrationResponse>();
        response.Should().NotBeNull();
        var newClient = await IdentityServerHost.GetClientAsync(response!.ClientId);
        newClient.Should().NotBeNull();
        newClient.ClientId.Should().Be(response.ClientId);
        newClient.AllowedGrantTypes.Should().BeEquivalentTo(request.GrantTypes);
        newClient.ClientName.Should().Be(request.ClientName);
        newClient.ClientUri.Should().Be(request.ClientUri.ToString());
        newClient.UserSsoLifetime.Should().Be(request.DefaultMaxAge);
    }
}