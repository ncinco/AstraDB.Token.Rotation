using Azure.Identity;
using Confluent.Kafka;
using System.Text;

namespace AstraDB.Token.Rotation.Services
{
    public class AuthenticateCallbackHandler
    {
        private readonly string _principalName;
        private readonly string _logicalCluster;
        private readonly string _identityPoolId;

        public AuthenticateCallbackHandler(string principalName, string localCluster, string identityPoolId)
        {
            _principalName = principalName;
            _logicalCluster = localCluster;
            _identityPoolId = identityPoolId;
        }

        public void Handle(IClient client, string configuration)
        {
            try
            {
                var extensions = new Dictionary<string, string>
                {
                    { "logicalCluster", _logicalCluster },
                    { "identityPoolId", _identityPoolId }
                };

                var credential = new ManagedIdentityCredential();
                var token = credential.GetToken(new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/" }));

                var lifetime = token.ExpiresOn.ToUnixTimeMilliseconds();
                client.OAuthBearerSetToken(token.Token, lifetime, _principalName, extensions);
            }
            catch (Exception ex)
            {
                var error = new StringBuilder();
                error.AppendLine(ex.Message);
                error.AppendLine(ex.ToString());

                Console.WriteLine(error.ToString());
            }
        }
    }
}