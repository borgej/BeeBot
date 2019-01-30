namespace YTBot.Models
{
    public class KillStat
    {
        public int Kills { get; set; }
        public int SquadKills { get; set; }
        public int Deaths { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public KillStat()
        {
            // Set initial stats
            Kills = 0;
            SquadKills = 0;
            Deaths = 0;
        }

        public int SetKills(int kills)
        {
            Kills = kills;

            return Kills;
        }

        public int SetDeaths(int deaths)
        {
            Deaths = deaths;

            return Deaths;
        }

        public int SetSquadkills(int squadKills)
        {
            SquadKills = squadKills;

            return SquadKills;
        }
    }
}