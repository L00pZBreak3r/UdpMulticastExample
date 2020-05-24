using System;

namespace UdpMulticastExampleDummyClient
{
  class Program
  {
    static void Main(string[] args)
    {
      MulticastClient aClient = new MulticastClient(args.Length > 0, "configuration.xml");
      aClient.StartDataExchange();
      aClient.DoUiInteraction();
    }
  }
}
