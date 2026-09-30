using System;
using UnityEngine;

namespace Imasu.Steamworks
{
    /// <summary>Who may join the lobby — maps onto Steam lobby visibility.</summary>
    public enum LobbySecurity
    {
        /// <summary>SetPrivate: invisible to browsing/friends lists; joining requires an accepted invite.</summary>
        InviteOnly = 0,

        /// <summary>SetFriendsOnly: any Steam friend of ANY member can join freely.</summary>
        FriendsOnly = 1,

        /// <summary>SetPublic: anyone on Steam can find and join.</summary>
        Open = 2
    }

    /// <summary>
    /// Host-configured match settings, chosen on the Game Setup screen and stored
    /// as Steam lobby data so joiners can read them in the waiting room.
    /// </summary>
    [Serializable]
    public struct LobbySettings
    {
        public const string DataName = "name";
        public const string DataMaxPlayers = "maxPlayers";
        public const string DataScene = "scene";
        public const string DataGameMode = "gamemode";
        public const string DataVoiceRange = "voiceRange";
        public const string DataPublic = "public";
        public const string DataSecurity = "security";
        public const string DataCode = "code";

        /// <summary>Game marker key — value "1". The browser filters on this so
        /// other games testing under the shared Spacewar appid never show up.</summary>
        public const string DataMarker = "hds";
        public const string MarkerValue = "1";

        public string lobbyName;
        public int maxPlayers;
        public string sceneName;
        public string gamemode;
        public float voiceRange;

        /// <summary>Transient indices of the host's chosen gamemode/map (not lobby data).</summary>
        public int modeIndex;
        public int mapIndex;

        /// <summary>Legacy pre-security flag; derived from <see cref="security"/> when set.</summary>
        public bool isPublic;

        public LobbySecurity security;

        /// <summary>Short shareable join code (see <see cref="LobbyCode"/>); empty until the host creates the lobby.</summary>
        public string code;

        public static LobbySettings Defaults(int modeIndex = -1, int mapIndex = -1)
        {
            ImasuSteamworksConfig cfg = ImasuSteamworksMaster.Config;
            string firstScene = cfg.SceneFor(modeIndex < 0 ? 0 : modeIndex, mapIndex < 0 ? 0 : mapIndex)
                ?? cfg.FirstMapScene();
            return new LobbySettings
            {
                lobbyName = cfg.EffectiveLobbyName(modeIndex),
                maxPlayers = cfg.EffectiveMaxPlayers(modeIndex, mapIndex),
                sceneName = string.IsNullOrEmpty(firstScene) ? "Game" : firstScene,
                voiceRange = cfg.EffectiveVoipMaxDistance(modeIndex),
                isPublic = true,
                security = cfg.EffectiveSecurity(modeIndex),
                modeIndex = modeIndex,
                mapIndex = mapIndex
            };
        }

        public string ToLobbyData()
        {
            return string.Join("|",
                lobbyName,
                maxPlayers,
                sceneName,
                gamemode ?? "",
                voiceRange.ToString("0.##"),
                isPublic ? "1" : "0",
                ((int)security).ToString(),
                code ?? "");
        }

        public static LobbySettings FromLobbyData(string data)
        {
            LobbySettings s = Defaults();
            if (string.IsNullOrEmpty(data)) return s;

            string[] parts = data.Split('|');
            if (parts.Length >= 1 && !string.IsNullOrEmpty(parts[0])) s.lobbyName = parts[0];
            if (parts.Length >= 2 && int.TryParse(parts[1], out int mp)) s.maxPlayers = mp;
            if (parts.Length >= 3 && !string.IsNullOrEmpty(parts[2])) s.sceneName = parts[2];
            if (parts.Length >= 4 && !string.IsNullOrEmpty(parts[3])) s.gamemode = parts[3];
            if (parts.Length >= 5 && float.TryParse(parts[4], out float vr)) s.voiceRange = vr;
            if (parts.Length >= 7 && byte.TryParse(parts[6], out byte sec) && sec <= (byte)LobbySecurity.Open)
            {
                s.security = (LobbySecurity)sec;
                s.isPublic = s.security == LobbySecurity.Open;
            }
            else if (parts.Length >= 6)
            {
                // Legacy lobbies created before security existed.
                s.isPublic = parts[5] == "1";
                s.security = s.isPublic ? LobbySecurity.Open : LobbySecurity.FriendsOnly;
            }
            if (parts.Length >= 8) s.code = parts[7];
            return s;
        }
    }
}