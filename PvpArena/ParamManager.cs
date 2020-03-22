using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;
using Terraria;

namespace PvpArena
{
    public class ParamManager
    {

        ArenaManager ArenaManager;
        MapManager MapManager;
        public List<Voting> Votings;
        public static readonly List<string> Params = new List<string>
        {
                "vt", "vote",
                "ac", "autochange",

                "ap", "autopvp",
                "at", "autotp",
                "ai", "autoinvise",

                "as", "autospawn"
        };

        public ParamManager(ref ArenaManager arenaManager, ref MapManager mapManager)
        {
            ArenaManager = arenaManager;
            MapManager = mapManager;
            Votings = new List<Voting>();
        }

        public void OnPlayerUpdate(object sender, GetDataHandlers.PlayerUpdateEventArgs args)
        {
            var player = TShock.Players[args.PlayerId];
            var arena = ArenaManager.InArea(player);

            if (arena == null || player.HasPermission(Permissions.Ignore)) return;


            if (ParamContains(arena, "autopvp"))
            {
                if (!player.TPlayer.hostile)
                    ChangePvpStatus(player);
                return;
            }
            if (ParamContains(arena, "autoinvise"))
            {
                if (!player.TPlayer.hostile)
                    BuffInvise(player);
                return;
            }
            if (ParamContains(arena, "autotp"))
            {
                if (!player.TPlayer.hostile)
                    player.Teleport(Terraria.Main.spawnTileX * 16, Terraria.Main.spawnTileY * 16);
                return;
            }
        }

        public void ChangePvpStatus(TSPlayer player)
        {
            player.TPlayer.hostile = !player.TPlayer.hostile;
            TSPlayer.All.SendData(PacketTypes.TogglePvp, "", player.Index);
            player.SendInfoMessage("Your PvP status is forced enabled in this arena!");
        }

        public void BuffInvise(TSPlayer player)
        {
            player.SetBuff(10, 500);
        }

        public bool ParamContains(Arena arena, string param)
        {
            if (arena.Parameters == null) return false;
            for (int i = 0; i < arena.Parameters.Count; i++)
                if (arena.Parameters[i].StartsWith(param.Split(':')[0]))
                    return true;
            return false;
        }
        public void OnTogglePvp(object sender, GetDataHandlers.TogglePvpEventArgs args)
        {
            var player = TShock.Players[args.PlayerId];
            var arena = ArenaManager.InArea(player);

            if (arena == null || args.Pvp == false) return;

            if (ParamContains(arena, "autospawn"))
            {
                int id = new Random().Next() % arena.Map.Spawns.Count();
                player.Teleport((arena.Position.X + arena.Map.Spawns[id].X) * 16, (arena.Position.X + arena.Map.Spawns[id].X) * 16);
            }
        }

        public void OnUpdate()
        {
            for (int i = 0; i < ArenaManager.Arenas.Count; i++)
            {
                var arena = ArenaManager.Arenas[i];
                if(ParamContains(arena, "autochange"))
                {
                    if(arena.NextChange < DateTime.Now)
                    {
                        string[] param = arena.Parameters.Find(s => s.StartsWith("autochange")).Split(':');
                        int nextVoteSec = int.Parse(param[1]);
                        string tag = param[2];
                        var maps = MapManager.GetMapsByTag(tag);
                        maps.RemoveAll(mp => mp.Size.X > arena.Size.X || mp.Size.Y > arena.Size.Y);
                        maps.Remove(arena.Map);
                        if (maps.Count > 0)
                        {
                            int id = new Random().Next() % maps.Count;
                            ArenaManager.SetMap(arena, maps[id]);
                        }
                        arena.NextChange = DateTime.Now.AddSeconds(nextVoteSec);
                    }
                }
            }
            for (int i = 0; i < Votings.Count; i++)
                if (Votings[i].CheckEnd())
                {
                    var arena = Votings[i].Arena;
                    var map = Votings[i].GetWinner();
                    string[] param = arena.Parameters.Find(s => s.StartsWith("vote")).Split(':');
                    ArenaManager.SetMap(arena, map);
                    arena.NextChange = DateTime.Now;
                    var nextVote = DateTime.Now.AddSeconds(int.Parse(param[2]));
                    arena.NextChange = nextVote;
                    ArenaManager.GetPlayersInArena(arena).ForEach(plr =>
                        plr.SendInfoMessage($"Next vote will be available in {GenerateTimeString(TimeSpan.FromSeconds(int.Parse(param[1])))}"));
                    Votings.RemoveAt(i);
                }
        }
        public bool AddParam(Arena arena, string param)
        {
            string shortParam = param.Split(':')[0];
            if (!Params.Contains(shortParam)) return false;
            string fullParam = GetFullParamName(shortParam);
            param = param.Replace(shortParam, fullParam);
            RemoveSame(arena, fullParam);
            ArenaManager.AddParam(arena, param);
            return true;
        }
        public bool RemoveParam(Arena arena, string param)
        {
            string shortParam = param.Split(':')[0];
            if (!Params.Contains(shortParam)) return false;
            string fullParam = GetFullParamName(shortParam);
            param = param.Replace(shortParam, fullParam);
            RemoveSame(arena, fullParam);
            ArenaManager.RemoveParam(arena, param);
            return true;
        }
        private void RemoveSame(Arena arena, string param)
        {
            for(int i = 0; i < arena.Parameters.Count; i++)
                if (arena.Parameters[i].StartsWith(param))
                    arena.Parameters.RemoveAt(i);
        }
        private string GetFullParamName(string param)
        {
            int id = Params.IndexOf(param);
            if (id % 2 == 0)
                id++;
            return Params[id];
        }
        private string GenerateTimeString(TimeSpan time)
        {
            string result = null;
            if (time.Hours > 0) result += $"{time.Hours}h:";
            if (time.Minutes > 0 || time.Hours > 0) result += $"{time.Minutes}m:";
            if (time.Seconds > 0 || time.Minutes > 0 || time.Hours > 0) result += $"{time.Seconds}s";
            return result;
        }
    }
}
