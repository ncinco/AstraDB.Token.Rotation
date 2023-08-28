using Azure.Identity;
using Confluent.Kafka;

namespace AstraDB.Token.Rotation.Services
{
    public abstract class AuthenticateCallbackHandlerBase
    {
        protected string PrincipalName { get; set; }

        protected string LogicalCluster { get; set; }

        protected string IdentityPoolId { get; set; }

        public void Handle(IClient client, string configuration)
        {
            try
            {
                var extensions = new Dictionary<string, string>
                {
                    { "logicalCluster", LogicalCluster },
                    { "identityPoolId", IdentityPoolId }
                };

                var credential = new ManagedIdentityCredential();
                var token = credential.GetToken(new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/" }));

                var lifetime = token.ExpiresOn.ToUnixTimeMilliseconds();
                client.OAuthBearerSetToken(token.Token, lifetime, PrincipalName, extensions);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}