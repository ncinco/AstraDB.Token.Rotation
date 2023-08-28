namespace AstraDB.Token.Rotation.Services
{
    public class AuthenticateCallbackHandler : AuthenticateCallbackHandlerBase
    {
        public AuthenticateCallbackHandler()
        {
            PrincipalName = "confluent-managed-identity";
            LogicalCluster = "lkc-3ng110";
            IdentityPoolId = "pool-y6OM";
        }
    }
}