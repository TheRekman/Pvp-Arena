using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using TShockAPI;
namespace PvpArena
{
    public class PlayerInfo
    {
        public static string Key = "PvpArena_Info";
        public State State;
        public string Name;
        public Map Map;
        public Point Point;
        public Point Point2;
        public string Align;
        public List<Point> Spawns;
        public List<string> Tags;
        public Point SpawnPoint;
    }
    public static class PlayerExtension
    {
        public static PlayerInfo GetPlayerInfo(this TSPlayer player)
        {
            if (!player.ContainsData(PlayerInfo.Key))
                player.SetData<PlayerInfo>(PlayerInfo.Key, new PlayerInfo());
            return player.GetData<PlayerInfo>(PlayerInfo.Key);
        }
    }
    public enum State
    {
        None,
        MapSave,
        MapSavePoint2,
        MapSaveSetSpawns,
        MapLoad,
        ArenaSet,
        ArenaSetWithSize,
        ArenaSetPoint2
    }
}
