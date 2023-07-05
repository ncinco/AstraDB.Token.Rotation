using Azure.Security.KeyVault.Secrets;

namespace AstraDB.Token.Rotation.Services
{
    public interface IKeyVaultService
    {
        void NewVersion(string secretName, string secretStatus, string clientId, string generatedOn, string value);

        void SetPerviousVersionToRotating(string secretName);

        KeyVaultSecret GetSecret(string secretName);

        List<SecretProperties> GetPropertiesOfSecrets();

        SecretProperties GetPreviousVersion(KeyVaultSecret secret);

        bool ExpirePreviousVersion(SecretProperties previousVersion);
    }
}