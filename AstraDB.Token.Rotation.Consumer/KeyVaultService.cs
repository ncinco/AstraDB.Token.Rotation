using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace AstraDB.Token.Rotation.Consumer
{
    public interface IKeyVaultService
    {
        void NewVersion(string secretName, string secretStatus, string clientId, string generatedOn, string value);

        void UpdateSecret(string secretName, string secretStatus);

        bool ExpirePreviousVersion(string secretName);
    }

    public class KeyVaultService : IKeyVaultService 
    {
        private const string KeyVaultUrl = "https://kv-astradb-astra.vault.azure.net/";
        private const string TenantId = "a5e8ce79-b0ec-41a2-a51c-aee927f1d808";
        private const string ClientId = "8b281262-415c-4a6c-91b9-246de71c17a9";
        private const string ClientSecret = "3tt8Q~xnvgt~kDmPdGlMoLxzmo8oC7Nf9OSlAcWy";

        private ClientSecretCredential _credential;
        private SecretClient _keyVaultSecretClient;

        public KeyVaultService()
        {
            _credential = new ClientSecretCredential(TenantId, ClientId, ClientSecret);
            _keyVaultSecretClient = new SecretClient(new Uri(KeyVaultUrl), _credential);
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

            Console.WriteLine($"Creating new secret version: {theSecret.Name} {tags}");
            // update the secret, this will create new version WITHOUT the tags
            _keyVaultSecretClient.SetSecret(secretName, value);
            // get the latest version
            theSecret = _keyVaultSecretClient.GetSecret(secretName);

            Console.WriteLine($"Updating secret tags: {theSecret.Name} {tags}");
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
            var theSecret = _keyVaultSecretClient.GetSecret(secretName).Value;

            if (theSecret == null)
            {
                Console.WriteLine($"Can't find secret named {secretName}. Potential bug.");
                return;
            }

            // copy tags
            var tags = theSecret.Properties.Tags;

            Console.WriteLine($"Updating secret tags: {theSecret.Name} {tags}");
            tags["status"] = secretStatus;

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

        public bool ExpirePreviousVersion(string secretName)
        {
            var theSecret = _keyVaultSecretClient.GetSecret(secretName).Value;

            if (theSecret == null)
            {
                Console.WriteLine($"Can't find secret named {secretName}. Potential bug.");
                return false;
            }

            var versions = _keyVaultSecretClient.GetPropertiesOfSecretVersions(secretName).ToList();
            var version = versions
                .OrderByDescending(x => x.CreatedOn)
                .FirstOrDefault(x => x.Version != theSecret.Properties.Version);

            if(version == null)
            {
                // no other version, first one
                return false;
            }

            // disable, expire and status to rotated
            version.Enabled = false;
            version.ExpiresOn= DateTime.UtcNow;
            version.Tags["status"] = "rotated";

            _keyVaultSecretClient.UpdateSecretProperties(version);

            return true;
        }
    }
}