namespace Imasu.Steamworks
{
    /// <summary>
    /// Voice-state surface the CORE can use without depending on the optional
    /// Voice addon (com.imasu.steamworks.voice). VoicePlayer implements it; core
    /// UI (scoreboard, waiting-room rows) queries it via
    /// FindObjectsByType&lt;IVoiceMember&gt; — when the addon isn't installed the
    /// interface simply never matches anything and rows render neutral.
    /// </summary>
    public interface IVoiceMember
    {
        bool IsLocalPlayer { get; }
        bool MicLive { get; }
        bool Speaking { get; }
        bool MicFailed { get; }
        ulong OwnerSteamId { get; }
        string DisplayName { get; }
        long BytesSent { get; }
        long BytesReceived { get; }

        void SetPushToTalk(bool held);
    }
}