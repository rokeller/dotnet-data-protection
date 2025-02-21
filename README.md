# .Net Data Protection APIs Example

.Net has shipped the [Data Protection APIs][https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/introduction]
for a while now. It's a pretty good system for easily keeping data secure and it
does include key management and rotation.

The example code in this repository shows how to use the APIs and use a single
code base both for local development as well as for production. In production,
keys should be _protected_ through secure key storage services such as Azure Key
Vault and they can be _persisted_ in storage services such as Azure Blob storage.

For local development you'll often want to use the same logical services, or
rather emulators of the same. With [Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite)
Microsoft provides an emulator for Azure Storage accounts, but Microsoft does
not yet offer an emulator for Azure Key Vault.

After looking into this I've found a few emulators for Azure Key Vault, but
most of them were old/outdated or abandoned/deprecated. I ended up forking one
that was apparently maintained until just recently:
[Azure Key Vault Emulator](https://github.com/rokeller/azure-keyvault-emulator).

I quickly realized that this emulator was missing a key API that Azure Key Vault
offers for Data Protection in .Net: the [`unwrap` API](https://learn.microsoft.com/en-us/rest/api/keyvault/keys/unwrap-key/unwrap-key).
This API is used to decrypt the master key that is used to encrypted the derived
keys used for data protection and is therefore essential for using the emulator
in local development.

Luckily the API is easy to implement, and so my fork of the emulator now
implements this as needed. Here's how you can use it in your local development
too.

## Setup emulators

As of the writing, [Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite)
is somewhat easy to use especially in combination with IDEs like Visual Studio
or VSCode.

As for the Azure Key Vault Emulator, it's easiest to just clone the repo and
run the emulator with minor changes to the default settings.

> Note: It's best to ensure that the self-signed certificate from the git repo
  is trusted. Use the instructions from the repo and your OS to get the
  certificate trusted. Naturally, you can use your own self-signed certificates
  too.

Because you'll want to use the [`DefaultAzureCredential` class](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential)
from the Azure SDKs for .Net, you'll need to let the emulator know which tenant
it should authenticate with. This is necessary to ensure you can use the same
code for local development and production environments. The tokens generated
this way and sent to the emulator never leave the emulator.

Next, you can run the Key Vault emulator from the root directory of its
repository:

```bash
AUTH__TENANTID=<ID-of-Azure-Tenant> STORE__BASEDIR=~/path/to/emulator/.vault \
  dotnet run --project AzureKeyVaultEmulator
```

### Use an entry in the `hosts` file for the emulator

You'll want to add a new entry in your `hosts` file to make sure that traffic
to the `emulator.vault.azure.net` FQDN is routed to the host that runs the
Key Vault emulator. For `localhost`, this'll be:

```bash
$ cat /etc/hosts
127.0.0.1       emulator.vault.azure.net
```

### Create a key for key protection

This is a one-time step, provided that you keep the keys persisted by the Key
Vault emulator.

```bash
curl -sX 'POST' \
  'https://emulator.vault.azure.net:11001/keys/my-data-protection-key/create' \
  -H'Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyLCJleHAiOjIwOTAyMzkwMjIsImlzcyI6Imh0dHBzOi8vbG9jYWxob3N0OjUwMDEvIn0.4Uq1vEBCqwxAggZg-IwCE9tfuAUtyrte5IiVRxN5JuI' \
  -H'Content-Type: application/json' \
  -d '{"kty": "RSA"}' | jq -r '.key.kid'
```

Note that the emulator does not verify the token, it just requires any
well-formed token. Once the key is created and persisted, you're ready to start
using it for data protection in local development.

Make sure you take note of the key identifier that's returned by the emulator.
You'll need to set it in the `appsettings.Development.json` file for use in
local development.

## Use the APIs provided by this example

Once all is set up, you can run this example to encrypt data as you like. The
encrypted data is safe to be sent to insecure clients. The `protect` API shows
how a secret can safely be encrypted. The `unprotect` API shows how such an
encrypted secret can be decrypted again. Of course in a real product you
probably won't offer such an API without at least AuthN/AuthZ in place.

```bash
SECRET=$(curl -s -XPOST 'http://localhost:5235/dataprotectiondemo/protect' \
  -d 'plaintext=Hello, World!')

echo "The protected secret is: $SECRET"
echo 'Unprotecting the secret ...'
curl -s -XPOST 'http://localhost:5235/dataprotectiondemo/unprotect' \
  -d "ciphertext=$SECRET"
```

## How it works / why it works

This approach works because the emulators are used to emulate the servers
offering the necessary services (Azure Blobs and Azure Key Vault). Beyond that
The dependency injection calls used allow for some flexibility, specifically
when it comes down to configuring endpoints. These secions here describes the
key points in more detail.

### Configuration for local development vs production

The [`appsettings.Development.json` file](./appsettings.Development.json) has
the relevant settings for local development. For data protection, the only
things you need are the URI of the master key for key protection (in the
`DataProtection:KeyId` setting) as well as the connection string for Azurite
(in the `BlobStorage:ConnectionString` setting). The latter uses the default
connection string of `UseDevelopmentStorage=true`.

The settings for production are in [`appsettings.Production.json`](./appsettings.Production.json)
in this example, but could of course also be injected in production through
corresponding environment variables. It is important to point out here that for
the blob service client, I just set the `BlobStorage:ServiceUri` setting and
assign a managed identity to the workload, such that the `DefaultAzureCredential`
can do its magic and use the managed identity to get an authentication token.
This way, there's no need to store a connection string. With additional firewall
setups on the Key Vault and the Storage account this gives me a pretty solid
system.

### Dependency injection configuration

Dependency injection is set up in [`Program.cs`](./Program.cs) as usual. The
important thing for blob clients is the use of the `AddAzureClients` extension
method to configure the blob service client. Its implementation uses some magic
to detect the best way to configure the clients using the settings that are
available.

For the blob service client specifically this means that if the configuration
section we're passing (`BlobStorage`) has a `connectionString`, it will use
that to connect. If it finds only a `ServiceUri` property in the section, it
will use the credentials passed to the builder. This allows the use of the exact
same code but with different configuration. Just like it needs to be.

To configure the data protection APIs, I'm using a dedicated factory method to
configure key persistence. This is in the [`DataProtection.cs` file](./DataProtection.cs),
where I take advantage of the previously configured Azure clients. Specifically,
I'm using the service provider to fetch the configured instance of
`BlobServiceClient`. This way I do not need to worry how it is instantiated or
configured.
