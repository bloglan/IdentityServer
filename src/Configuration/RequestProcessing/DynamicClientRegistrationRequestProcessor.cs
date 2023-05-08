// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration.Configuration;
using Duende.IdentityServer.Configuration.Models.DynamicClientRegistration;
using Duende.IdentityServer.Configuration.Validation;
using Duende.IdentityServer.Models;
using IdentityModel;

namespace Duende.IdentityServer.Configuration.RequestProcessing;

/// <inheritdoc />
public class DynamicClientRegistrationRequestProcessor : IDynamicClientRegistrationRequestProcessor
{
    private readonly IdentityServerConfigurationOptions _options;
    private readonly IClientConfigurationStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicClientRegistrationRequestProcessor"/> class.
    /// </summary>
    /// <param name="options">The IdentityServer.Configuration options.</param>
    /// <param name="store">The client configuration store.</param>
    public DynamicClientRegistrationRequestProcessor(
        IdentityServerConfigurationOptions options, 
        IClientConfigurationStore store)
    {
        _options = options;
        _store = store;
    }


    /// <inheritdoc />
    public virtual async Task<IDynamicClientRegistrationResponse> ProcessAsync(
        DynamicClientRegistrationContext context)
    {
        var clientIdResult = await AddClientId(context);
        if(clientIdResult is FailedStep clientIdFailure)
        {
            return clientIdFailure;
        }

        Secret? secret = null;
        string? plainText = null;
        var clientSecretResult = await AddClientSecret(context);
        if(clientSecretResult is FailedStep clientSecretFailure)
        {
            return clientSecretFailure;
        }
        else if(clientSecretResult is SuccessfulStep)
        {
            secret = (Secret) context.Items["secret"];
            plainText = (string) context.Items["plainText"];
        }

        await _store.AddAsync(context.Client);

        return new DynamicClientRegistrationResponse(context.Request, context.Client)
        {
            ClientId = context.Client.ClientId,
            ClientSecret = plainText,
            ClientSecretExpiresAt = secret switch
            {
                null => null, 
                { Expiration: null } => 0,
                { Expiration: DateTime e } => new DateTimeOffset(e).ToUnixTimeSeconds()
            }
        };
    }

    /// <summary>
    /// Adds a client secret to a dynamic client registration request.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual async Task<StepResult> AddClientSecret(
        DynamicClientRegistrationContext context)
    {
        if (!context.Client.ClientSecrets.Any())
        {
            var result = await GenerateSecret(context);
            if(result is SuccessfulStep)
            {
                if (context.Items.TryGetValue("secret", out var secret))
                {
                    context.Client.ClientSecrets.Add((Secret)secret);
                }
            }
            return result;
        }
        return new SuccessfulStep();
    }

    /// <summary>
    /// Generates a secret for a dynamic client registration request.
    /// TODO - Document items that are required to be set
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual Task<StepResult> GenerateSecret(
        DynamicClientRegistrationContext context)
    {
        var plainText = CryptoRandom.CreateUniqueId();

        DateTime? lifetime = _options.DynamicClientRegistration.SecretLifetime switch
        {
            null => null,
            TimeSpan t => DateTime.UtcNow.Add(t)
        };

        var secret = new Secret(plainText.ToSha256(), lifetime);

        context.Items["secret"] = secret;
        context.Items["plainText"] = plainText;

        return StepResult.Success();
    }

    /// <summary>
    /// Generates a client ID and adds it to the validatedRequest's client
    /// model.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    protected virtual Task<StepResult> AddClientId(
        DynamicClientRegistrationContext context)
    {
        context.Client.ClientId = CryptoRandom.CreateUniqueId();
        return StepResult.Success();
    }
}