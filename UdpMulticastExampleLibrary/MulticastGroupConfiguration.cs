using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace UdpMulticastExampleLibrary
{
  public class MulticastGroupConfiguration
  {
    #region Constants

    private const string CONFIGURATION_ROOT_ELEMENT_NAME = "multicastconfigurations";
    private const string CONFIGURATION_ELEMENT_NAME = "configuration";
    private const string CONFIGURATION_ATTRIBUTE_NAME = "name";

    private const string RANGE_ELEMENT_NAME = "range";
    private const string RANGE_ATTRIBUTE_MIN = "min";
    private const string RANGE_ATTRIBUTE_MAX = "max";

    private const string GROUP_ELEMENT_NAME = "group";
    private const string GROUP_ATTRIBUTE_SERVER_PORT = "serverport";
    private const string GROUP_ATTRIBUTE_CLIENT_PORT = "clientport";
    private const string GROUP_ATTRIBUTE_ADDRESS = "address";
    private const string GROUP_ATTRIBUTE_ID = "id";

    private const string DELAY_ELEMENT_NAME = "delay";
    private const string DELAY_ATTRIBUTE_VALUE = "milliseconds";

    private const int PORT_NUMBER_MIN = 4001;
    private const int PORT_NUMBER_MAX = 65000;
    private const string GROUP_ADDRESS_V4_DEFAULT = "239.0.0.215";
    private const string GROUP_ADDRESS_V6_DEFAULT = "ff01::";
    private const string GROUP_ID_V6_DEFAULT = "1";

    private const int DELAY_MAX = 1200000;

    #endregion

    #region Variables



    #endregion

    #region Name


    public string Name { get; set; }

    #endregion

    #region ServerPort

    private int mServerPort = PORT_NUMBER_MIN;

    public int ServerPort
    {
      get
      {
        return mServerPort;
      }
      set
      {
        if (value < PORT_NUMBER_MIN)
          value = PORT_NUMBER_MIN;
        if (value > PORT_NUMBER_MAX)
          value = PORT_NUMBER_MAX;
        if (value == mClientPort)
          value++;

        mServerPort = value;
      }
    }

    #endregion

    #region ClientPort

    private int mClientPort = PORT_NUMBER_MIN + 1000;

    public int ClientPort
    {
      get
      {
        return mClientPort;
      }
      set
      {
        if (value < PORT_NUMBER_MIN)
          value = PORT_NUMBER_MIN;
        if (value > PORT_NUMBER_MAX)
          value = PORT_NUMBER_MAX;
        if (value == mServerPort)
          value++;

        mClientPort = value;
      }
    }

    #endregion

    #region GroupAddress

    public string GroupAddress
    {
      get
      {
        string v = GroupAddressPrefix;
        if (UseInternetV6)
        {
          string aGroupId = GroupId;
          if (string.IsNullOrEmpty(aGroupId))
            aGroupId = GROUP_ID_V6_DEFAULT;
          v += aGroupId;
        }
        return v;
      }
      set
      {
        GroupAddressPrefix = value;
      }
    }

    #endregion

    #region GroupAddressPrefix

    private string mGroupAddressPrefix = GROUP_ADDRESS_V6_DEFAULT;

    public string GroupAddressPrefix
    {
      get
      {
        return mGroupAddressPrefix;
      }
      set
      {
        mGroupAddressPrefix = value?.Trim();
        if (UseInternetV6)
        {
          var aHexs = mGroupAddressPrefix.Split(':');
          int aGroupIdStart = aHexs.Length - 3;
          if (aGroupIdStart >= 0)
          {
            string aGrpId = null;
            string aPrefix = mGroupAddressPrefix;
            if (string.IsNullOrEmpty(aHexs[aGroupIdStart]))
            {
              aGrpId = aHexs[aGroupIdStart + 1] + ":" + aHexs[aGroupIdStart + 2];
              aPrefix = string.Join(':', aHexs, 0, aGroupIdStart + 1);
            }
            else if (string.IsNullOrEmpty(aHexs[aGroupIdStart + 1]))
            {
              aGrpId = aHexs[aGroupIdStart + 2];
              aPrefix = string.Join(':', aHexs, 0, aGroupIdStart + 2);
            }
            mGroupAddressPrefix = aPrefix + ":";
            GroupId = aGrpId;
          }
        }
      }
    }

    #endregion

    #region GroupId

    public string GroupId { get; set; } = GROUP_ID_V6_DEFAULT;

    #endregion

    #region UseInternetV6

    public bool UseInternetV6
    {
      get
      {
        return mGroupAddressPrefix?.StartsWith("ff", StringComparison.OrdinalIgnoreCase) ?? false;
      }
    }

    #endregion

    #region NumberRangeMin


    public double NumberRangeMin { get; private set; } = int.MinValue;

    #endregion

    #region NumberRangeMax


    public double NumberRangeMax { get; private set; } = int.MaxValue;

    #endregion

    #region ClientReceiveDelay

    private int mClientReceiveDelay;

    public int ClientReceiveDelay
    {
      get
      {
        return mClientReceiveDelay;
      }
      set
      {
        if (value < 0)
          value = 0;
        if (value > DELAY_MAX)
          value = DELAY_MAX;

        mClientReceiveDelay = value;
      }
    }

    #endregion

    #region ToString

    public override string ToString()
    {
      StringBuilder aSb = new StringBuilder();
      aSb.Append("Configuration: ");
      aSb.AppendLine(Name);
      aSb.Append("Range: [");
      aSb.Append(NumberRangeMin);
      aSb.Append("; ");
      aSb.Append(NumberRangeMax);
      aSb.AppendLine("]");
      aSb.Append("Multicast Address: ");
      aSb.AppendLine(GroupAddress);
      aSb.Append("Server port: ");
      aSb.Append(mServerPort);
      aSb.AppendLine();
      aSb.Append("Client port: ");
      aSb.Append(mClientPort);
      aSb.AppendLine();
      aSb.Append("Client receive delay (milliseconds): ");
      aSb.Append(mClientReceiveDelay);
      aSb.AppendLine();

      return aSb.ToString();
    }

    #endregion

    #region Methods

    public void SetNumberRange(double aMin, double aMax)
    {
      if (aMax < aMin)
      {
        double tmp = aMax;
        aMax = aMin;
        aMin = tmp;
      }
      if (aMin == aMax)
        aMax += 0.0001;

      NumberRangeMin = aMin;
      NumberRangeMax = aMax;
    }

    public void SaveConfiguration(string aFileName, bool aUpdate = true)
    {
      XDocument aXdoc = null;
      XElement aRoot = null;
      if (aUpdate)
        try
        {
          aXdoc = XDocument.Load(aFileName);
          aRoot = aXdoc.Root;
        }
        catch
        {
        }
      if (aRoot == null)
      {
        aRoot = new XElement(CONFIGURATION_ROOT_ELEMENT_NAME);
        aXdoc = new XDocument(aRoot);
      }
      XElement aConfiguration = new XElement(CONFIGURATION_ELEMENT_NAME,
          new XElement(RANGE_ELEMENT_NAME,
            new XAttribute(RANGE_ATTRIBUTE_MIN, NumberRangeMin),
            new XAttribute(RANGE_ATTRIBUTE_MAX, NumberRangeMax)
          ),
          new XElement(GROUP_ELEMENT_NAME,
            new XAttribute(GROUP_ATTRIBUTE_SERVER_PORT, mServerPort),
            new XAttribute(GROUP_ATTRIBUTE_CLIENT_PORT, mClientPort),
            new XAttribute(GROUP_ATTRIBUTE_ADDRESS, mGroupAddressPrefix),
            new XAttribute(GROUP_ATTRIBUTE_ID, GroupId)
          ),
          new XElement(DELAY_ELEMENT_NAME,
            new XAttribute(DELAY_ATTRIBUTE_VALUE, mClientReceiveDelay)
          )
        );
      if (!string.IsNullOrWhiteSpace(Name))
        aConfiguration.Add(new XAttribute(CONFIGURATION_ATTRIBUTE_NAME, Name));
      aRoot.Add(aConfiguration);
      aXdoc.Save(aFileName);
    }

    public string LoadConfiguration(string aFileName, string aConfigurationName = null, bool aFirstIfMissing = true)
    {
      string res = null;
      if (File.Exists(aFileName))
      {
        XElement aRoot = null;
        try
        {
          aRoot = XElement.Load(aFileName);
        }
        catch
        {
        }
        if (aRoot != null)
        {
          IEnumerable<XElement> aConfigurations = aRoot.Elements(CONFIGURATION_ELEMENT_NAME);
          if (!string.IsNullOrWhiteSpace(aConfigurationName))
          {
            IEnumerable<XElement> aNamedConfigurations =
              from el in aConfigurations
              where aConfigurationName.Equals((string)el.Attribute(CONFIGURATION_ATTRIBUTE_NAME), StringComparison.CurrentCultureIgnoreCase)
              select el;
            if ((aNamedConfigurations.Count() > 0) || !aFirstIfMissing)
              aConfigurations = aNamedConfigurations;
          }

          XElement aConfiguration = aConfigurations.FirstOrDefault();
          if (aConfiguration != null)
          {
            res = aConfiguration.Attribute(CONFIGURATION_ATTRIBUTE_NAME)?.Value;
            Name = res;

            XElement aRange = aConfiguration.Element(RANGE_ELEMENT_NAME);
            if (aRange != null)
            {
              double aMin = int.MinValue;
              double aMax = int.MaxValue;

              string aVal = aRange.Attribute(RANGE_ATTRIBUTE_MIN)?.Value;
              if (string.IsNullOrWhiteSpace(aVal) || !double.TryParse(aVal, out aMin) || !double.TryParse(aVal, NumberStyles.Float, CultureInfo.InvariantCulture, out aMin))
                aMin = int.MinValue;

              aVal = aRange.Attribute(RANGE_ATTRIBUTE_MAX)?.Value;
              if (string.IsNullOrWhiteSpace(aVal) || !double.TryParse(aVal, out aMax) || !double.TryParse(aVal, NumberStyles.Float, CultureInfo.InvariantCulture, out aMax))
                aMax = int.MaxValue;

              SetNumberRange(aMin, aMax);
            }

            aRange = aConfiguration.Element(GROUP_ELEMENT_NAME);
            if (aRange != null)
            {
              string aVal = aRange.Attribute(GROUP_ATTRIBUTE_ADDRESS)?.Value;
              if (string.IsNullOrWhiteSpace(aVal))
                aVal = GROUP_ADDRESS_V6_DEFAULT;
              GroupAddressPrefix = aVal;

              GroupId = aRange.Attribute(GROUP_ATTRIBUTE_ID)?.Value;

              aVal = aRange.Attribute(GROUP_ATTRIBUTE_SERVER_PORT)?.Value;
              if (!int.TryParse(aVal, out mServerPort))
                mServerPort = PORT_NUMBER_MIN;

              aVal = aRange.Attribute(GROUP_ATTRIBUTE_CLIENT_PORT)?.Value;
              if (!int.TryParse(aVal, out mClientPort))
                mClientPort = PORT_NUMBER_MIN + 1000;
            }

            aRange = aConfiguration.Element(DELAY_ELEMENT_NAME);
            if (aRange != null)
            {
              string aVal = aRange.Attribute(DELAY_ATTRIBUTE_VALUE)?.Value;
              int aDelay;
              if (!int.TryParse(aVal, out aDelay))
                aDelay = 0;
              ClientReceiveDelay = aDelay;
            }
          }
        }
      }
      return res;
    }

    #endregion
  }
}
