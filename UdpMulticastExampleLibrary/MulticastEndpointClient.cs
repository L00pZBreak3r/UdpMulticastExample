using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace UdpMulticastExampleLibrary
{
  public class MulticastEndpointClient
  {
    public sealed class DataReceivedEventArgs : EventArgs
    {
      public readonly byte[] Data;
      public bool Cancel;
      public bool Skip;

      public DataReceivedEventArgs(byte[] aData)
      {
        Data = aData;
      }
    }

    private const int COMMUNICATION_ENDPOINT_PORT = 50;
    private const int CLIENT_RECEIVE_BUFFER_SIZE_DEFAULT = 8192;
#if DEBUG
    public
#else
    private
#endif
      readonly MulticastGroupConfiguration mConfiguration = new MulticastGroupConfiguration();
    private volatile bool mCanReceive;
    private volatile bool mIsReceiving;

#if DEBUG
    public
#else
    private
#endif
      UdpClient mUdpClient;
#if DEBUG
    public
#else
    private
#endif
      IPAddress mGroupAddress;
#if DEBUG
    public
#else
    private
#endif
      IPEndPoint mDestination;
#if DEBUG
    public
#else
    private
#endif
      IPEndPoint mCommunicationEndpoint;

    public bool ServerMode;
    public byte[] EndOfMessageSequence;
    public bool AlwaysSkip;

    public event EventHandler<DataReceivedEventArgs> DataReceived;

    public MulticastEndpointClient(bool aServerMode = false, string aFileName = null, string aConfigurationName = null, bool aFirstIfMissing = true)
    {
      ServerMode = aServerMode;
      mConfiguration.LoadConfiguration(aFileName, aConfigurationName, aFirstIfMissing);
    }

    public string LoadConfiguration(string aFileName, string aConfigurationName = null, bool aFirstIfMissing = true)
    {
      return mConfiguration.LoadConfiguration(aFileName, aConfigurationName, aFirstIfMissing);
    }

    public void SaveConfiguration(string aFileName, bool aUpdate = true)
    {
      mConfiguration.SaveConfiguration(aFileName, aUpdate);
    }

    #region IsReceiving

    public bool IsReceiving
    {
      get
      {
        return mIsReceiving;
      }
    }

    #endregion

    #region NumberRangeMin

    public double NumberRangeMin
    {
      get
      {
        return mConfiguration.NumberRangeMin;
      } 
    }

    #endregion

    #region NumberRangeMax

    public double NumberRangeMax
    {
      get
      {
        return mConfiguration.NumberRangeMax;
      }
    }

    #endregion

    #region ClientReceiveDelay

    public int ClientReceiveDelay
    {
      get
      {
        return mConfiguration.ClientReceiveDelay;
      }
    }

    #endregion

    #region ConfigurationInfo

    public string ConfigurationInfo
    {
      get
      {
        return mConfiguration.ToString();
      }
    }

    #endregion

    #region ClientReceiveBufferSize

    private int mClientReceiveBufferSize = CLIENT_RECEIVE_BUFFER_SIZE_DEFAULT;

    public int ClientReceiveBufferSize
    {
      get
      {
        return mClientReceiveBufferSize;
      }
      set
      {
        if (value < 8)
          value = CLIENT_RECEIVE_BUFFER_SIZE_DEFAULT;
        mClientReceiveBufferSize = value;
      }
    }

    #endregion

    public int SendData(byte[] aData)
    {
      return ((mUdpClient != null) && (aData != null) && (aData.Length > 0)) ? mUdpClient.Send(aData, aData.Length, mDestination) : 0;
    }

    public int SendData(MemoryStream aData)
    {
      return ((aData != null) && (aData.Length > 0L)) ? SendData(aData.ToArray()) : 0;
    }

    public MemoryStream ReceiveData()
    {
      MemoryStream res = null;
      if ((mUdpClient != null) && mCanReceive && !mIsReceiving)
      {
        res = new MemoryStream();
        byte[] aReceivedData = null;
        do
        {
          mIsReceiving = true;
          if (!ServerMode)
          {
            int aDelay = ClientReceiveDelay;
            if (aDelay > 0)
              Thread.Sleep(aDelay);
          }
          try
          {
            aReceivedData = mUdpClient.Receive(ref mCommunicationEndpoint);
          }
          catch
          {

          }
          if ((aReceivedData != null) && (aReceivedData.Length > 0))
          {
            bool aSkip = false;
            bool aCancel = false;
            if (DataReceived != null)
            {
              var aEventArgs = new DataReceivedEventArgs(aReceivedData);
              DataReceived(this, aEventArgs);
              aSkip = aEventArgs.Skip;
              aCancel = aEventArgs.Cancel;
            }
            if (!AlwaysSkip && !aSkip)
              res.Append(aReceivedData);
            if (aCancel)
              aReceivedData = null;
          }
        }
        while (mCanReceive && (aReceivedData != null) && (aReceivedData.Length > 0) && (EndOfMessageSequence != null) && !EndOfMessageSequence.SequenceEqual(aReceivedData));
        mIsReceiving = false;
      }
      return res;
    }

    public void OpenEndpoint()
    {
      CloseEndpoint();

      bool aUseIpV6 = mConfiguration.UseInternetV6;
      AddressFamily aNetworkFamily = (aUseIpV6) ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork;
      IPAddress aIpAddressAny = (aUseIpV6) ? IPAddress.IPv6Any : IPAddress.Any;
      mGroupAddress = IPAddress.Parse(mConfiguration.GroupAddress);
      int aReceivePort;
      int aSendPort;
      if (ServerMode)
      {
        aReceivePort = mConfiguration.ServerPort;
        aSendPort = mConfiguration.ClientPort;
        mUdpClient = new UdpClient(aReceivePort, aNetworkFamily);
      }
      else
      {
        aReceivePort = mConfiguration.ClientPort;
        aSendPort = mConfiguration.ServerPort;
        mUdpClient = new UdpClient(aNetworkFamily);
        mUdpClient.ExclusiveAddressUse = false;
        mUdpClient.Client.ReceiveBufferSize = mClientReceiveBufferSize;
        mUdpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        mUdpClient.Client.Bind(new IPEndPoint(aIpAddressAny, aReceivePort));
      }
      
      mUdpClient.JoinMulticastGroup(mGroupAddress);

      mDestination = new IPEndPoint(mGroupAddress, aSendPort);
      mCommunicationEndpoint = new IPEndPoint(aIpAddressAny, COMMUNICATION_ENDPOINT_PORT);
      mCanReceive = true;
    }

    public void CloseEndpoint()
    {
      mCanReceive = false;
      for (int i = 0; (i < 10) && mIsReceiving; i++)
        Thread.Sleep(100);
      mIsReceiving = false;
      mUdpClient?.DropMulticastGroup(mGroupAddress);
      mUdpClient?.Close();
      mUdpClient = null;
    }

  }
}
