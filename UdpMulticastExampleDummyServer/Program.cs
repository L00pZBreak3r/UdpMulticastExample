using System.Threading;

namespace UdpMulticastExampleDummyServer
{
  class Program
  {
    static void Main(string[] args)
    {
      MulticastServer aServer = new MulticastServer("configuration.xml");
      if (args.Length > 0)
        aServer.SaveConfiguration("configuration.template.xml");
      aServer.PrintConfiguration("Server configuration:");
      aServer.StartDataExchange();
      aServer.DoUiInteraction();
    }
  }
}
