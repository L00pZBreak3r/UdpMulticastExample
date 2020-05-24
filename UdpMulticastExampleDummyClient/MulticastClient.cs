using System;
using System.Collections.Generic;
using System.Threading;

using System.Net.Sockets;

using UdpMulticastExampleLibrary;
using System.Linq;

namespace UdpMulticastExampleDummyClient
{
  class MulticastClient
  {

    private class ReceivedValuesComparer : IComparer<KeyValuePair<long, double>>
    {
      public int Compare(KeyValuePair<long, double> x, KeyValuePair<long, double> y)
      {
        return x.Value.CompareTo(y.Value);
      }
    }

    //Ah, heck, let's compute the corrected sample standard deviation too.
    private const int VALUE_BUFFER_LENGTH_MAX = 256 * 1024 * 1024;
    private readonly List<KeyValuePair<long, double>> mReceivedValues;
    private static readonly ReceivedValuesComparer mReceivedValuesComparer = new ReceivedValuesComparer();

    //In this case we compute the uncorrected sample standard deviation.
    private double mSum;
    private double mSum2;

    private readonly string mDeviationName;

    private MulticastEndpointClient mMulticastEndpoint;
    private Thread mWorker;
    private double EndOfCommunicationValue = int.MinValue;
    private long mCount;
    private long mCurrentIndex;
    private long mLost;

    public volatile bool CanWork;
    private volatile bool mIsWorking;

    public MulticastClient(bool aComputeMedian, string aFileName, string aConfigurationName = null, bool aFirstIfMissing = true)
    {
      mDeviationName = (aComputeMedian) ? "corrected deviation" : "uncorrected deviation";
      mReceivedValues = (aComputeMedian) ? new List<KeyValuePair<long, double>>(VALUE_BUFFER_LENGTH_MAX) : null;

      mMulticastEndpoint = new MulticastEndpointClient(false, aFileName, aConfigurationName, aFirstIfMissing);
      mMulticastEndpoint.AlwaysSkip = true;

      var aBytesD = BitConverter.GetBytes(EndOfCommunicationValue);
      var aBytesL = BitConverter.GetBytes(-1L);
      byte[] aPacket = new byte[aBytesL.Length + aBytesD.Length];
      Array.Copy(aBytesL, aPacket, aBytesL.Length);
      Array.Copy(aBytesD, 0, aPacket, aBytesL.Length, aBytesD.Length);
      mMulticastEndpoint.EndOfMessageSequence = aPacket;
      mMulticastEndpoint.ClientReceiveBufferSize = aPacket.Length * 2;

      mMulticastEndpoint.DataReceived += MulticastDataReceived;
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
      if (mReceivedValues != null)
      {
        lock (mReceivedValues)
        {
          mReceivedValues.Clear();
        }
      }
      else
      {
        mSum = 0.0;
        mSum2 = 0.0;
      }
      mCount = 0;
      mCurrentIndex = 0;
      mLost = 0;
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

      Console.WriteLine("Client started.");
      ReceiveData();

      mMulticastEndpoint.CloseEndpoint();
      mIsWorking = false;
      CanWork = false;
      Console.WriteLine("Client stopped.");
    }

    private void PrintStatistics()
    {
      if (mCurrentIndex >= 0)
      {
        long aCurLost = mCurrentIndex - mCount + 1;
        if (aCurLost > mLost)
          mLost = aCurLost;
      }
      Console.WriteLine("Values received = {0}; lost = {1}; delay (ms) = {2}", mCount, mLost, mMulticastEndpoint.ClientReceiveDelay);

      double aMean = 0.0;
      double aDeviation = 0.0;
      double aMedian = double.NaN;
      double aMode = double.NaN;
      long aN = mCount;
      if (aN > 0L)
      {
        if (mReceivedValues != null)
        {
          lock (mReceivedValues)
          {
            aN = mReceivedValues.Count;
            mReceivedValues.Sort(mReceivedValuesComparer);
            aMedian = mReceivedValues[(int)aN / 2].Value;
            if (((int)aN & 1) == 0)
            {
              double aMedian2 = mReceivedValues[(int)aN / 2 - 1].Value;
              aMedian = (aMedian + aMedian2) / 2.0;
            }

            //Mode. Since we have doubles, it's unlikely we have repaeted exact values. So, let's round them and find mode among these rounded values.
            aMode = mReceivedValues[0].Value;
            double aVal20 = aMode;
            double aVal1 = Math.Round(aMode);
            double aVal2 = aVal1;
            int aSequenceLength1 = 1;
            int aSequenceLength2 = aSequenceLength1;
            for (int i = 1; i < aN; i++)
            {
              double aValCur0 = mReceivedValues[i].Value;
              double aValCur = Math.Round(aValCur0);
              if (aValCur == aVal2)
                aSequenceLength2++;
              else
              {
                if (aSequenceLength1 < aSequenceLength2)
                {
                  aSequenceLength1 = aSequenceLength2;
                  aVal1 = aVal2;
                  aMode = aVal20;
                }
                aVal20 = aValCur0;
                aVal2 = aValCur;
                aSequenceLength2 = 1;
              }
            }

            aMean = mReceivedValues.Average(kv => kv.Value);
            aDeviation = 0.0;
            if (aN > 1L)
              aDeviation = Math.Sqrt(mReceivedValues.Sum(kv => Math.Pow(kv.Value - aMean, 2.0)) / (aN - 1));
          }
        }
        else
        {
          aMean = mSum / aN;
          aDeviation = Math.Sqrt(mSum2 / aN - (aMean * aMean));
        }
      }
      Console.WriteLine("Statistics: mean={1}; {0}={2}; median={3}; mode={4}", mDeviationName, aMean, aDeviation, aMedian, aMode);
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

    private void MulticastDataReceived(object sender, MulticastEndpointClient.DataReceivedEventArgs e)
    {
      e.Cancel = !CanWork;
      long ind = -1L;
      double d = EndOfCommunicationValue;
      var aBytes = e.Data;
      if (aBytes.Length >= 16)
        try
        {
          ind = BitConverter.ToInt64(aBytes, 0);
          d = BitConverter.ToDouble(aBytes, 8);
        }
        catch
        {
          ind = -1L;
          d = EndOfCommunicationValue;
        }
      if ((d != EndOfCommunicationValue) && (ind >= 0L))
      {
        if (mReceivedValues != null)
        {
          lock (mReceivedValues)
          {
            int aValuesToDelete = mReceivedValues.Count - VALUE_BUFFER_LENGTH_MAX;
            for (int i = 0; i < aValuesToDelete; i++)
            {
              long aMinKey = long.MaxValue;
              int aMinKeyIndex = -1;
              int aCnt = mReceivedValues.Count;
              for (int k = 0; k < aCnt; k++)
                if (mReceivedValues[k].Key < aMinKey)
                {
                  aMinKey = mReceivedValues[k].Key;
                  aMinKeyIndex = k;
                }
              if (aMinKeyIndex >= 0)
                mReceivedValues.RemoveAt(aMinKeyIndex);
            }
            mReceivedValues.Add(new KeyValuePair<long, double>(ind, d));
          }
        }
        else
        {
          mSum += d;
          mSum2 += d * d;
        }

        mCurrentIndex = ind;
        mCount++;
/*#if DEBUG
        Console.WriteLine("Received[{0,10}]: {1,30}", ind, d);
#endif*/
      }
/*#if DEBUG
      else
        Console.WriteLine("Received[{0,10}]: {1,30}", 0, "END");
#endif*/
    }
  }
}
