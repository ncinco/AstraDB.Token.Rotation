using Azure.Security.KeyVault.Secrets;

namespace AstraDB.Token.Rotation.Services
{
    public interface IKeyVaultService
    {
        Task NewVersionAsync(string secretName, string secretStatus, string clientId, string generatedOn, string value);

        Task SetPerviousVersionToRotatingAsync(string secretName);

        Task<KeyVaultSecret> GetSecretAsync(string secretName);

        List<SecretProperties> GetPropertiesOfSecrets();

        SecretProperties GetPreviousVersion(KeyVaultSecret secret);

        Task<bool> ExpirePreviousVersionAsyc(SecretProperties previousVersion);
    }
}