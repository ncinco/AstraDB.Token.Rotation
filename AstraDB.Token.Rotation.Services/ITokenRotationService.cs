namespace AstraDB.Token.Rotation.Services
{
    public interface ITokenRotationService
    {
        Task TaskProduceDummyMessagesAsync();

        Task ProduceMessagesAsync();

        Task ConsumeMessagesAsync();

        Task ExpireTokensAsync();
    }
}