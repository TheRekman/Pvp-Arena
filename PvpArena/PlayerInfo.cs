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
        public State Status;
        public string MapName;
        public string ArenaName;
        public Point Point;
        public void ClearRequest()
        {
            ArenaName = null;
            MapName = null;
            Status = State.None;
        }
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
        MapLoad,
        ArenaSet,
        ArenaSetPoint2,
    }
}
