using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;

namespace PvpArena
{
    public class Vote
    {
        public TSPlayer Player { get; private set; }
        public int MapVote { get; set; }

        public Vote(TSPlayer player, int vote)
        {
            Player = player;
            MapVote = vote;
        }
    }
}
