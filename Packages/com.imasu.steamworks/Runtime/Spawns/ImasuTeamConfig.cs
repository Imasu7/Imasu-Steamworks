using UnityEngine;

namespace Imasu.Steamworks
{
    /// <summary>
    /// Team configuration for spawns &amp; scoreboards. Default = 1 team (everyone
    /// against the map). Add more teams (PvP / team battles) from the Control
    /// Center → Spawns &amp; Teams section; each team can then own its spawn points.
    /// </summary>
    [CreateAssetMenu(fileName = "ImasuTeamConfig", menuName = "Imasu/Team Config")]
    public class ImasuTeamConfig : ScriptableObject
    {
        [Tooltip("How far apart two players must spawn (anti-clump), in meters. 0 = no check.")]
        public float minSeparation = 2f;

        public TeamEntry[] teams = { new TeamEntry() };

        [System.Serializable]
        public class TeamEntry
        {
            [Tooltip("Team name shown in the scoreboard / lobby.")]
            public string name = "Team 1";

            [Tooltip("Team color.")]
            public Color color = new Color(0.4f, 1f, 0.45f);

            [Tooltip("teamId of the spawns this team uses. 0 = any.")]
            public int spawnTeamId = 1;
        }

        public string TeamName(int teamId)
        {
            foreach (TeamEntry t in teams)
                if (t.spawnTeamId == teamId) return t.name;
            return "Team " + teamId;
        }
    }
}