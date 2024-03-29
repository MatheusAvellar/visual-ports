﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace VisualPorts {

  public partial class MainWindow : Window {
  
    List<VisualProcess> processesList;
    CollectionView listBoxView;
    // TEMP?
    List<string> toBeLookedUp;
    Dictionary<string, string> reverseLookup;

    public MainWindow() {
      InitializeComponent();
      processesList = new List<VisualProcess>();
      processessListBox.ItemsSource = processesList;
      listBoxView = (CollectionView)CollectionViewSource.GetDefaultView(processessListBox.ItemsSource);

      // TEMP?
      reverseLookup = new Dictionary<string, string>();
    }

    public class VisualProcess {
      public int Port { get; set; }
      public string Name { get; set; }
      public int ID  { get; set; }
      public string State { get; set; }
      public string Host { get; set; }
    }

    private void Button_Click(object sender, RoutedEventArgs e) {
      // Clear ListView
      processesList.Clear();
      // Create hash table for ports (so we don't add the same port twice)
      HashSet<int> ports = new HashSet<int>();

      // TEMP?
      toBeLookedUp = new List<string>();

      // For each magic row in the magic table (don't ask me)
      // [Ref] timvw.be/2007/09/09/build-your-own-netstatexe-with-c/
      foreach(TcpRow tcpRow in ManagedIpHelper.GetExtendedTcpTable(true)) {
        // Only look at ports that are listening for requests
        string process_port_state = tcpRow.State.ToString();
        if(process_port_state.StartsWith("Listen")
        || process_port_state.StartsWith("Established")) {
          // Get the executable name from PID, and the port it's using
          int process_id = tcpRow.ProcessId;
          string process_name = Process.GetProcessById(process_id).ProcessName;
          int process_port = tcpRow.LocalEndPoint.Port;
          // If the port is already on the hash table, skip this entry
          if(ports.Contains(process_port))
            continue;
          // Otherwise, add it to the hash table
          ports.Add(process_port);

          // TEMP?
          string process_host = "";
          foreach(string ip in toBeLookedUp) {
            if(reverseLookup.ContainsKey(ip)) {
              if(ip.Equals(""))
                process_host = ip;
              else
                process_host = reverseLookup[ip];
              continue;
            }

            string reversed = DoReverseIpLookup(ip);
            reverseLookup.Add(ip, reversed);
            if(reversed.Equals(""))
              process_host = ip;
            else
              process_host = reversed;
          }

          // And at last, insert this entry in the ListView
          processesList.Add(new VisualProcess {
            Port = process_port,
            Name = process_name,
            ID = process_id,
            State = process_port_state,
            Host = process_host
          });

          // TEMP?
          toBeLookedUp.Add(tcpRow.RemoteEndPoint.Address.ToString());
        }
      }

      // Being honest, I don't fully understand how this works
      // [Ref] wpf-tutorial.com/listview-control/listview-sorting/
      listBoxView.SortDescriptions.Clear();
      listBoxView.SortDescriptions.Add(new SortDescription("Port", ListSortDirection.Ascending));
    }

    string DoReverseIpLookup(string ip) {
      if(ip.StartsWith("0.0.") || ip.StartsWith("::"))
        return "localhost";
      try {
        IPHostEntry ipEntry = Dns.GetHostEntry(ip);
        return ipEntry.HostName;
      } catch(Exception) {
        Console.WriteLine("Something went wrong fetching " + ip);
        return "";
      }
    }

    // This stuff is just copy pasted, I have no clue how it works (but it does!)
    // [Ref] timvw.be/2007/09/09/build-your-own-netstatexe-with-c/
    #region Managed IP Helper API

    public class TcpTable : IEnumerable<TcpRow> {
      #region Private Fields

      private IEnumerable<TcpRow> tcpRows;

      #endregion

      #region Constructors

      public TcpTable(IEnumerable<TcpRow> tcpRows) {
        this.tcpRows = tcpRows;
      }

      #endregion

      #region Public Properties

      public IEnumerable<TcpRow> Rows {
        get { return this.tcpRows; }
      }

      #endregion

      #region IEnumerable<TcpRow> Members

      public IEnumerator<TcpRow> GetEnumerator() {
        return this.tcpRows.GetEnumerator();
      }

      #endregion

      #region IEnumerable Members

      IEnumerator IEnumerable.GetEnumerator() {
        return this.tcpRows.GetEnumerator();
      }

      #endregion
    }

    public class TcpRow {
      #region Private Fields

      private IPEndPoint localEndPoint;
      private IPEndPoint remoteEndPoint;
      private TcpState state;
      private int processId;

      #endregion

      #region Constructors

      public TcpRow(IpHelper.TcpRow tcpRow) {
        this.state = tcpRow.state;
        this.processId = tcpRow.owningPid;

        int localPort = (tcpRow.localPort1 << 8) + (tcpRow.localPort2) + (tcpRow.localPort3 << 24) + (tcpRow.localPort4 << 16);
        long localAddress = tcpRow.localAddr;
        this.localEndPoint = new IPEndPoint(localAddress, localPort);

        int remotePort = (tcpRow.remotePort1 << 8) + (tcpRow.remotePort2) + (tcpRow.remotePort3 << 24) + (tcpRow.remotePort4 << 16);
        long remoteAddress = tcpRow.remoteAddr;
        this.remoteEndPoint = new IPEndPoint(remoteAddress, remotePort);
      }

      #endregion

      #region Public Properties

      public IPEndPoint LocalEndPoint {
        get { return this.localEndPoint; }
      }

      public IPEndPoint RemoteEndPoint {
        get { return this.remoteEndPoint; }
      }

      public TcpState State {
        get { return this.state; }
      }

      public int ProcessId {
        get { return this.processId; }
      }

      #endregion
    }

    public static class ManagedIpHelper {
      #region Public Methods

      public static TcpTable GetExtendedTcpTable(bool sorted) {
        List<TcpRow> tcpRows = new List<TcpRow>();

        IntPtr tcpTable = IntPtr.Zero;
        int tcpTableLength = 0;

        if(IpHelper.GetExtendedTcpTable(tcpTable, ref tcpTableLength, sorted, IpHelper.AfInet, IpHelper.TcpTableType.OwnerPidAll, 0) != 0) {
          try {
            tcpTable = Marshal.AllocHGlobal(tcpTableLength);
            if(IpHelper.GetExtendedTcpTable(tcpTable, ref tcpTableLength, true, IpHelper.AfInet, IpHelper.TcpTableType.OwnerPidAll, 0) == 0) {
              IpHelper.TcpTable table = (IpHelper.TcpTable)Marshal.PtrToStructure(tcpTable, typeof(IpHelper.TcpTable));

              IntPtr rowPtr = (IntPtr)((long)tcpTable + Marshal.SizeOf(table.length));
              for(int i = 0; i < table.length; ++i) {
                tcpRows.Add(new TcpRow((IpHelper.TcpRow)Marshal.PtrToStructure(rowPtr, typeof(IpHelper.TcpRow))));
                rowPtr = (IntPtr)((long)rowPtr + Marshal.SizeOf(typeof(IpHelper.TcpRow)));
              }
            }
          } finally {
            if(tcpTable != IntPtr.Zero) {
              Marshal.FreeHGlobal(tcpTable);
            }
          }
        }

        return new TcpTable(tcpRows);
      }

      #endregion
    }

    #endregion

    #region P/Invoke IP Helper API

    /// <summary>
    /// <see cref="http://msdn2.microsoft.com/en-us/library/aa366073.aspx"/>
    /// </summary>
    public static class IpHelper {
      #region Public Fields

      public const string DllName = "iphlpapi.dll";
      public const int AfInet = 2;

      #endregion

      #region Public Methods

      /// <summary>
      /// <see cref="http://msdn2.microsoft.com/en-us/library/aa365928.aspx"/>
      /// </summary>
      [DllImport(IpHelper.DllName, SetLastError = true)]
      public static extern uint GetExtendedTcpTable(IntPtr tcpTable, ref int tcpTableLength, bool sort, int ipVersion, TcpTableType tcpTableType, int reserved);

      #endregion

      #region Public Enums

      /// <summary>
      /// <see cref="http://msdn2.microsoft.com/en-us/library/aa366386.aspx"/>
      /// </summary>
      public enum TcpTableType {
        BasicListener,
        BasicConnections,
        BasicAll,
        OwnerPidListener,
        OwnerPidConnections,
        OwnerPidAll,
        OwnerModuleListener,
        OwnerModuleConnections,
        OwnerModuleAll,
      }

      #endregion

      #region Public Structs

      /// <summary>
      /// <see cref="http://msdn2.microsoft.com/en-us/library/aa366921.aspx"/>
      /// </summary>
      [StructLayout(LayoutKind.Sequential)]
      public struct TcpTable {
        public uint length;
        public TcpRow row;
      }

      /// <summary>
      /// <see cref="http://msdn2.microsoft.com/en-us/library/aa366913.aspx"/>
      /// </summary>
      [StructLayout(LayoutKind.Sequential)]
      public struct TcpRow {
        public TcpState state;
        public uint localAddr;
        public byte localPort1;
        public byte localPort2;
        public byte localPort3;
        public byte localPort4;
        public uint remoteAddr;
        public byte remotePort1;
        public byte remotePort2;
        public byte remotePort3;
        public byte remotePort4;
        public int owningPid;
      }

      #endregion
    }

    #endregion
  }
}
