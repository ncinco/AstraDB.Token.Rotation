namespace AstraDB.Token.Rotation.Models
{
    public class EventStreamTokenRotationMessage
    {
        public string SeedClientId { get; set; }

        public string ClientId { get; set; }

        public List<string> Roles { get; set; }
    }
}