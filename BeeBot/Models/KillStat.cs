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

        public int IncrementSquad()
        {
            SquadKills++;

            return SquadKills;
        }

        public int IncrementDeaths()
        {
            Deaths++;

            return Deaths;
        }

        public int IncrementKills()
        {
            Kills++;

            return Kills;
        }
    }
}