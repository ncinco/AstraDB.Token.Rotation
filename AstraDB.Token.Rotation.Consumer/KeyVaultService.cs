using AstraDB.Token.Rotation.Configuration;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System;

namespace AstraDB.Token.Rotation.Consumer
{
    public interface IKeyVaultService
    {
        void NewVersion(string secretName, string secretStatus, string clientId, string generatedOn, string value);

        void UpdateSecret(string secretName, string secretStatus);

        bool ExpirePreviousVersion(string secretName, string clientId);
    }

    public class KeyVaultService : IKeyVaultService
    {
        private ClientSecretCredential _credential;
        private SecretClient _keyVaultSecretClient;

        public KeyVaultService()
        {
            _credential = new ClientSecretCredential(KeyVault.TenantId, KeyVault.ClientId, KeyVault.ClientSecret);
            _keyVaultSecretClient = new SecretClient(new Uri(KeyVault.KeyVaultUrl), _credential);
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
            tags["clientId"] = clientId;
            tags["status"] = secretStatus;
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

        public void UpdateSecret(string secretName, string secretStatus)
        {
            var theCurrentSecret = _keyVaultSecretClient.GetSecret(secretName).Value;

            var previousVersion = _keyVaultSecretClient
                .GetPropertiesOfSecretVersions(secretName)
                .FirstOrDefault(x => x.Version != theCurrentSecret.Properties.Version);

            if (previousVersion == null)
            {
                Console.WriteLine($"Can't find secret named {secretName}. Potential bug.");
                return;
            }

            // copy tags
            Console.WriteLine($"Updating secret tags: {previousVersion.Name}");
            previousVersion.Tags["status"] = secretStatus;
            _keyVaultSecretClient.UpdateSecretProperties(previousVersion);
        }

        public bool ExpirePreviousVersion(string secretName, string clientId)
        {
            var theSecret = _keyVaultSecretClient.GetSecret(secretName).Value;

            if (theSecret == null)
            {
                Console.WriteLine($"Can't find secret named {secretName}. Potential bug.");
                return false;
            }

            var version = _keyVaultSecretClient
                .GetPropertiesOfSecretVersions(secretName)
                .FirstOrDefault(x => x.Version != theSecret.Properties.Version);

            if (version == null)
            {
                // no other version, first one
                return false;
            }

            // disable, expire and status to rotated
            version.Enabled = false;
            version.ExpiresOn = DateTime.UtcNow;
            version.Tags["status"] = "rotated";

            _keyVaultSecretClient.UpdateSecretProperties(version);

            return true;
        }
    }
}