using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TShockAPI;
using TShockAPI.DB;

namespace PvpArena
{
    public class Voting
    {
        public DateTime TimeStart { get; private set; }
        public DateTime EndDateTime
        {
            get
            {
                return TimeStart.AddSeconds(EndTime);
            }
        }
        int EndTime;
        public Arena Arena;
        List<Map> Maps;
        ArenaManager ArenaManager;
        List<Vote> Votes;

        public Voting(Arena arena, int time, ref ArenaManager arenaManager)
        {
            Arena = arena;
            EndTime = time;
            ArenaManager = arenaManager;
            TimeStart = DateTime.Now;
            Maps = new List<Map> { Arena.Map };
            Votes = new List<Vote>();
            arenaManager.GetPlayersInArena(arena).ForEach(plr =>
                plr.SendInfoMessage("Map vote started! Use /vote for map vote or add new in vote."));
        }
        public void RegistVote(TSPlayer tSPlayer, Map map)
        {
            var players = ArenaManager.GetPlayersInArena(Arena);
            var vote = Votes.Find(v => v.Player == tSPlayer);
            if (vote != null)
                Votes.Remove(vote);
            if (!Maps.Contains(map))
            {
                Maps.Add(map);
                players.ForEach(plr => plr.SendInfoMessage($"{tSPlayer.Name} add map {map.Name} in vote!"));
            }
            Votes.Add(new Vote(tSPlayer, Maps.IndexOf(map)));
        }

        public Map GetMapById(int id)
        {
            if (id < Maps.Count)
                return Maps[id];
            return null;
        }

        public TimeSpan VoteTime
        {
            get
            {
                return EndDateTime - DateTime.Now;
            }
        }
        public List<string> GetInfo()
        {
            var result = new List<string>();

            for (int i = 0; i < Maps.Count; i++)
                result.Add($"{i}. {Maps[i].Name} - {VoteCount(Maps[i])}");
            
            return result;
        }
        public int VoteCount(Map map)
        {
            int result = 0;
            int id = Maps.IndexOf(map);
            for (int i = 0; i < Votes.Count; i++)
                if (Votes[i].MapVote == id)
                    result++;
            return result;
        }
        public Map GetWinner()
        {
            var result = Maps[0];
            for (int i = 1; i < Maps.Count; i++)
                if (VoteCount(result) < VoteCount(Maps[i]))
                    result = Maps[i];
            return result;
        }
        public bool CheckEnd() => DateTime.Now > EndDateTime;
    }
}
