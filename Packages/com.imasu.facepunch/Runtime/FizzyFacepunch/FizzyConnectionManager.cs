using Steamworks;
using System;

namespace Mirror.FizzySteam
{
  public class FizzyConnectionManager : ConnectionManager
  {
    public Action<IntPtr, int> ForwardMessage;

    public override void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
      ForwardMessage(data, size);
    }
  }
}
