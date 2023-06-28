namespace AstraDB.Token.Rotation.Models
{
    public class EventStreamTokenRotationMessage
    {
        public string ClientId { get; set; }

        public List<string> Roles { get; set; }
    }
}