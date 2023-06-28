namespace AstraDB.Token.Rotation.Models
{
    public class AstraNewTokenResponse
    {
        public string ClientId { get; set; }

        public string Secret { get; set; }

        public string OrgId { get; set; }

        public List<string> Roles { get; set; }

        public string Token { get; set; }
    }
}