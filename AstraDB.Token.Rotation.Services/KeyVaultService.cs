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

        public async Task NewVersionAsync(string secretName, string secretStatus, string clientId, string generatedOn, string value)
        {
            var theSecret = await GetSecretAsync(secretName);

            if (theSecret == null)
            {
                Console.WriteLine($"Can't find secret named {secretName}. Potential bug.");
                return;
            }

            // copy tags
            var tags = theSecret.Properties.Tags;

            Console.WriteLine($"Creating new secret version: {theSecret.Name}");
            // update the secret, this will create new version WITHOUT the tags
            await _keyVaultSecretClient.SetSecretAsync(secretName, value);
            // get the latest version
            theSecret = await GetSecretAsync(secretName);

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

            await _keyVaultSecretClient.UpdateSecretPropertiesAsync(theSecret.Properties);
        }

        public async Task SetPerviousVersionToRotatingAsync(string secretName)
        {
            var theCurrentSecret = await GetSecretAsync(secretName);

            var previousVersion = GetPreviousVersion(theCurrentSecret);

            if (previousVersion == null)
            {
                Console.WriteLine($"Can't find secret named {secretName}. Potential bug.");
                return;
            }

            // update status tag
            Console.WriteLine($"Updating secret tags: {previousVersion.Name}");
            previousVersion.Tags[KeyVaultTags.Status] = KeyVaultStatus.Rotating;
            await _keyVaultSecretClient.UpdateSecretPropertiesAsync(previousVersion);
        }

        public async Task<KeyVaultSecret> GetSecretAsync(string secretName)
        {
            return await _keyVaultSecretClient
                .GetSecretAsync(secretName);
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

        public async Task<bool> ExpirePreviousVersionAsyc(SecretProperties previousVersion)
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

            await _keyVaultSecretClient.UpdateSecretPropertiesAsync(previousVersion);

            return true;
        }
    }
}