using System;
using System.IO;
using System.Text;
using System.Threading;

using System.Net.Sockets;

using UdpMulticastExampleLibrary;

namespace UdpMulticastExampleDummyServer
{
  class MulticastServer
  {
    private MulticastEndpointClient mMulticastEndpoint;
    private static readonly Random mRandom = new Random();
    private Thread mWorker;
    private double EndOfCommunicationValue = int.MinValue;
    private long mCount;

    private double mMin = int.MinValue;
    private double mMax = int.MaxValue;

    public volatile bool CanWork;
    private volatile bool mIsWorking;

    public MulticastServer(string aFileName, string aConfigurationName = null, bool aFirstIfMissing = true)
    {
      mMulticastEndpoint = new MulticastEndpointClient(true, aFileName, aConfigurationName, aFirstIfMissing);

      var aBytesD = BitConverter.GetBytes(EndOfCommunicationValue);
      var aBytesL = BitConverter.GetBytes(-1L);
      byte[] aPacket = new byte[aBytesL.Length + aBytesD.Length];
      Array.Copy(aBytesL, aPacket, aBytesL.Length);
      Array.Copy(aBytesD, 0, aPacket, aBytesL.Length, aBytesD.Length);
      mMulticastEndpoint.EndOfMessageSequence = aPacket;

      mMin = mMulticastEndpoint.NumberRangeMin;
      mMax = mMulticastEndpoint.NumberRangeMax;
    }

    public void SaveConfiguration(string aFileName, bool aUpdate = true)
    {
      mMulticastEndpoint.SaveConfiguration(aFileName, aUpdate);
    }

    private void SendData(long aIndex, double aData)
    {
      var aBytesD = BitConverter.GetBytes(aData);
      var aBytesL = BitConverter.GetBytes(aIndex);
      byte[] aPacket = new byte[aBytesL.Length + aBytesD.Length];
      Array.Copy(aBytesL, aPacket, aBytesL.Length);
      Array.Copy(aBytesD, 0, aPacket, aBytesL.Length, aBytesD.Length);
      mMulticastEndpoint.SendData(aPacket);
    }

    private (long Index, double Value) ReceiveData()
    {
      var aStream = mMulticastEndpoint.ReceiveData();
      long i = -1L;
      double d = EndOfCommunicationValue;
      if (aStream != null)
      {
        var aBytes = aStream.ToArray();
        try
        {
          i = BitConverter.ToInt64(aBytes, 0);
          d = BitConverter.ToDouble(aBytes, 8);
        }
        catch
        {
          i = -1L;
          d = EndOfCommunicationValue;
        }
      }
      return (i, d);
    }

    public void StartDataExchange()
    {
      CanWork = true;
      mIsWorking = false;
      mCount = 0L;
      mWorker = new Thread(DoDataExchange);
      mWorker.Start();
    }

    private void DoDataExchange()
    {
      mIsWorking = true;
      mMulticastEndpoint.OpenEndpoint();
#if DEBUG
      Console.WriteLine("Multicast Address(" + mMulticastEndpoint.mUdpClient.Client.AddressFamily + "): [" + mMulticastEndpoint.mGroupAddress
        + " = " + mMulticastEndpoint.mUdpClient.Client.LocalEndPoint.ToString() + "]");
      if (mMulticastEndpoint.mConfiguration.UseInternetV6)
      {
        IPv6MulticastOption ipv6MulticastOption = new IPv6MulticastOption(mMulticastEndpoint.mGroupAddress);

        Console.WriteLine("IPv6MulticastOption.Group: [" + ipv6MulticastOption.Group + "]");
        Console.WriteLine("IPv6MulticastOption.InterfaceIndex: [" + ipv6MulticastOption.InterfaceIndex + "]");
      }
      Console.WriteLine("Destination: [" + mMulticastEndpoint.mDestination.ToString() + "]");
      Console.WriteLine("Communication: [" + mMulticastEndpoint.mCommunicationEndpoint.ToString() + "]");
#endif

      Console.WriteLine("Server started.");
      while (CanWork)
      {
        double d = GetRandomNumber();
        if (d != EndOfCommunicationValue)
        {
          SendData(mCount, d);
          //Console.WriteLine("Sent[{0,10}]: {1,30}", mCount, d);
          //Thread.Sleep(1000);
          mCount++;
        }
      }
      SendData(-1L, EndOfCommunicationValue);

      mMulticastEndpoint.CloseEndpoint();
      mIsWorking = false;
      CanWork = false;
      Console.WriteLine("Server stopped.");
    }

    private double GetRandomNumber()
    {
      return mRandom.NextDouble() * (mMax - mMin) + mMin;
    }

    public void PrintConfiguration(string aTitle = null)
    {
      if (!string.IsNullOrWhiteSpace(aTitle))
        Console.WriteLine(aTitle);
      Console.WriteLine(mMulticastEndpoint.ConfigurationInfo);
    }

    private void PrintStatistics()
    {
      Console.WriteLine("Values sent: {0}", mCount);
    }

    public void DoUiInteraction()
    {
      ConsoleKey aKey;
      do
      {
        aKey = ConsoleKey.Escape;
        while (CanWork && mIsWorking && !Console.KeyAvailable)
        {
          Thread.Sleep(100);
        }
        if (CanWork)
        {
          aKey = Console.ReadKey(true).Key;
          if (aKey == ConsoleKey.Enter)
            PrintStatistics();
        }
      }
      while (aKey != ConsoleKey.Escape);
      CanWork = false;
      Thread.Sleep(1000);
      mMulticastEndpoint.CloseEndpoint();
      mWorker.Join();
    }
  }
}
