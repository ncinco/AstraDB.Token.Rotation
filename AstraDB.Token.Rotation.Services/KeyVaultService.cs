using AstraDB.Token.Rotation.Configuration;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace AstraDB.Token.Rotation.Services
{
    public class KeyVaultService : IKeyVaultService
    {
        private readonly SecretClient _keyVaultSecretClient;

        public KeyVaultService()
        {
            var _credential = new ClientSecretCredential(KeyVaultConfig.TenantId, KeyVaultConfig.ClientId, KeyVaultConfig.ClientSecret);
            _keyVaultSecretClient = new SecretClient(new Uri(KeyVaultConfig.KeyVaultUrl), _credential);
        }

        public void NewVersion(string secretName, string secretStatus, string clientId, string generatedOn, string value)
        {
            var theSecret = _keyVaultSecretClient.GetSecret(secretName).Value;

            if (theSecret == null)
            {
                Console.WriteLine($"Can't find secret named {secretName}. Potential bug.");
                return;
            }

            // copy tags
            var tags = theSecret.Properties.Tags;

            Console.WriteLine($"Creating new secret version: {theSecret.Name}");
            // update the secret, this will create new version WITHOUT the tags
            _keyVaultSecretClient.SetSecret(secretName, value);
            // get the latest version
            theSecret = _keyVaultSecretClient.GetSecret(secretName);

            Console.WriteLine($"Updating secret tags: {theSecret.Name}");
            tags[KeyVaultTags.ClientId] = clientId;
            tags[KeyVaultTags.Status] = secretStatus;
            tags["generatedOn"] = generatedOn;

            theSecret.Properties.ContentType = "text/plain";
            theSecret.Properties.NotBefore = DateTime.UtcNow;
            theSecret.Properties.ExpiresOn = DateTime.UtcNow.AddHours(24);

            theSecret.Properties.Tags.Clear();
            foreach (var tag in tags)
            {
                theSecret.Properties.Tags.Add(tag);
            }

            _keyVaultSecretClient.UpdateSecretProperties(theSecret.Properties);
        }

        public void SetPerviousVersionToRotating(string secretName)
        {
            var theCurrentSecret = _keyVaultSecretClient.GetSecret(secretName).Value;

            var previousVersion = GetPreviousVersion(theCurrentSecret);

            if (previousVersion == null)
            {
                Console.WriteLine($"Can't find secret named {secretName}. Potential bug.");
                return;
            }

            // update status tag
            Console.WriteLine($"Updating secret tags: {previousVersion.Name}");
            previousVersion.Tags[KeyVaultTags.Status] = KeyVaultStatus.Rotating;
            _keyVaultSecretClient.UpdateSecretProperties(previousVersion);
        }

        public KeyVaultSecret GetSecret(string secretName)
        {
            return _keyVaultSecretClient
                .GetSecret(secretName);
        }

        public List<SecretProperties> GetPropertiesOfSecrets()
        {
            return _keyVaultSecretClient
                .GetPropertiesOfSecrets()
                .ToList();
        }

        public SecretProperties GetPreviousVersion(KeyVaultSecret secret)
        {
            var previousVersion = _keyVaultSecretClient
                .GetPropertiesOfSecretVersions(secret.Name)
                .OrderByDescending(x => x.CreatedOn)
                .FirstOrDefault(x => x.Version != secret.Properties.Version
                    && x.Tags[KeyVaultTags.Status] == KeyVaultStatus.Rotating);

            if (previousVersion == null)
            {
                Console.WriteLine($"Can't find secret named {secret.Name}. Potential bug.");
            }

            return previousVersion;
        }

        public bool ExpirePreviousVersion(SecretProperties previousVersion)
        {
            if (previousVersion == null)
            {
                // no other version, first one
                return false;
            }

            // disable, expire and status to rotated
            previousVersion.Enabled = false;
            previousVersion.ExpiresOn = DateTime.UtcNow;
            previousVersion.Tags[KeyVaultTags.Status] = KeyVaultStatus.Rotated;

            _keyVaultSecretClient.UpdateSecretProperties(previousVersion);

            return true;
        }
    }
}