using Confluent.Kafka;

namespace AstraDB.Token.Rotation.Services
{
    public interface IAuthenticateCallbackHandler
    {
        void Handle(IClient client, string configuration);
    }
}