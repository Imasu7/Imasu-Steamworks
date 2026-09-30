using System;
using Mirror;
using UnityEngine;

namespace Imasu.Steamworks
{
    /// <summary>
    /// Broadcasts this player's connection RTT (ms) for the scoreboard ping column.
    /// Mirror only exposes the LOCAL connection's round-trip time (NetworkTime.rtt),
    /// so the owner reports its own value every second through a SyncVar, which
    /// replicates to everyone (host rows read ~0ms from the same path).
    /// </summary>
    public class PlayerRtt : NetworkBehaviour
    {
        [SyncVar] public double rttMs;

        const float Interval = 1f;
        float nextReport;

        void Update()
        {
            if (!isLocalPlayer) return;
            if (Time.time < nextReport) return;
            nextReport = Time.time + Interval;

            double rtt = NetworkTime.rtt * 1000.0;
            if (Math.Abs(rtt - rttMs) > 0.5) // throttle SyncVar churn
                rttMs = rtt;
        }
    }
}