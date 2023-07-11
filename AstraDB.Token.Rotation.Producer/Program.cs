namespace AstraDB.Token.Rotation.Producer
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            await Startup.ConfigureServices();
        }
    }
}