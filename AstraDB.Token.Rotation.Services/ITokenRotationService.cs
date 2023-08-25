namespace AstraDB.Token.Rotation.Services
{
    public interface ITokenRotationService
    {
        Task TaskProduceDummyMessagesAsync();

        Task TaskConsumeDummyMessagesAsync();

        Task ProduceMessagesAsync();

        Task ConsumeMessagesAsync();

        Task ExpireTokensAsync();
    }
}