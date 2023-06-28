namespace AstraDB.Token.Rotation.Models
{
    public class AstraRevokeTokenResponse
    {
        public List<Error> Errors { get; set; }
    }

    public class Error
    {
        public int ID { get; set; }
        public string Message { get; set; }
    }
}