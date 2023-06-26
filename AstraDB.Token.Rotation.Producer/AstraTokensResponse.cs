namespace AstraDB.Token.Rotation.Producer
{
    public class AstraTokensResponse
    {
        public List<Client> Clients { get; set; }
    }

    public class Client
    {
        public string ClientId { get; set; }
        public List<string> Roles { get; set; }
        public DateTime GeneratedOn { get; set; }
    }
}