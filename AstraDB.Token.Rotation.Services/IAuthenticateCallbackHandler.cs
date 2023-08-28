using Confluent.Kafka;

namespace AstraDB.Token.Rotation.Services
{
    public interface IAuthenticateCallbackHandler
    {
        public string PrincipalName { get; }

        void Handle(IClient client, string configuration);
    }
}