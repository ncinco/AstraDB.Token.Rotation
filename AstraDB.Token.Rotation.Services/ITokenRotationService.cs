namespace AstraDB.Token.Rotation.Services
{
    public interface ITokenRotationService
    {
        Task ProduceMessagesAsync();

        Task ConsumeMessagesAsync();

        Task ExpireTokensAsync();
    }
}