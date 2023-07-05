namespace AstraDB.Token.Rotation.Services
{
    public interface ITokenRotationService
    {
        void ProduceMessages();

        void ConsumeMessages();

        void ExpireTokens();
    }
}