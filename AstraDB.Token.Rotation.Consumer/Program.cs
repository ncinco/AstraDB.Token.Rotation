namespace AstraDB.Token.Rotation.Consumer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            new TokenRotationService().Start();
        }
    }
}