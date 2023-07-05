namespace AstraDB.Token.Rotation.Configuration
{
    public class KeyVaultTags
    {
        public const string Status = "status";

        public const string SeedClientId = "seed_clientId";

        public const string ClientId = "clientId";

        public const string GeneratedOn = "generatedOn";
    }

    public class KeyVaultStatus
    {
        public const string Active = "active";

        public const string Rotating = "rotating";

        public const string Rotated = "rotated";
    }
}