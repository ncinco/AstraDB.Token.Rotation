using Azure.Security.KeyVault.Secrets;

namespace AstraDB.Token.Rotation.Services
{
    public interface IKeyVaultService
    {
        Task NewVersionAsync(string secretName, string secretStatus, string clientId, string generatedOn, string value);

        Task SetPerviousVersionToRotatingAsync(string secretName);

        Task<KeyVaultSecret> GetSecretAsync(string secretName);

        Task<List<SecretProperties>> GetPropertiesOfSecretsAsync();

        SecretProperties GetPreviousVersion(KeyVaultSecret secret, string keyVaultStatus);

        Task<bool> ExpirePreviousVersionsAsyc(string secretName);
    }
}