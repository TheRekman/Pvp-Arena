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
        public int PlayerID;
        public bool MapSave;
        public string MapName;
        public string ArenaName;
        public Point Point;
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
}
