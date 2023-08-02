using System;
using System.Net;
using System.Net.Sockets;
using System.Linq;

namespace Sst.SpeedrunTimer {
public static class Network {
  public static string[] GetAllAddresses() {
    return new[] { "127.0.0.1" }
        .Concat(Dns.GetHostEntry(Dns.GetHostName())
                    .AddressList
                    .Where(address => address.AddressFamily ==
                                      AddressFamily.InterNetwork)
                    .Select(address => address.ToString()))
        .ToArray();
  }
}
}
