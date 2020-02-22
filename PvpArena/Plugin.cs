using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using System.IO;
using Microsoft.Xna.Framework;
using System.Data;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;

namespace PvpArena
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {

        public override string Author => "Noname";

        public override string Description => "Add functonal for pvp arena";

        public override string Name => "PvpArena";


        public MapManager MapManager;
        public ArenaManager ArenaManager;
        public Config Config;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
            }
            base.Dispose(disposing);
        }

        public Plugin(Main game) : base(game)
        {
        }

        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
        }

        private void OnInitialize(EventArgs args)
        {
            Config = Config.Read(Path.Combine(TShock.SavePath, "[PvpArena]-Config.json"));
            MapManager = new MapManager(Path.Combine(TShock.SavePath, "[PvpArena]-Maps"));
            IDbConnection db = GetDbConnection();
            if(db == null)
            {
                Dispose(true);
                return;
            }
            ArenaManager = new ArenaManager(db, MapManager);
            Commands.ChatCommands.Add(new Command(Permissions.MapUse, MapCmd, "map"));
            Commands.ChatCommands.Add(new Command(Permissions.ArenaUse, ArenaCmd, "arena"));
        }

        private IDbConnection GetDbConnection()
        {
            IDbConnection db;
            if (TShock.Config.StorageType.Equals("mysql", StringComparison.OrdinalIgnoreCase))
            { 
                if (string.IsNullOrWhiteSpace(Config.MySqlHost) ||
                    string.IsNullOrWhiteSpace(Config.MySqlDbName))
                {
                    TShock.Log.ConsoleError("[PvpArena] MySQL is enabled, but the PvpArena MySQL Configuration has not been set.");
                    TShock.Log.ConsoleError("[PvpArena] Please configure your MySQL server information in [PvpArena]-Config.json, then restart the server.");
                    TShock.Log.ConsoleError("[PvpArena] This plugin will now disable itself...");

                    return null;
                }
                string[] host = Config.MySqlHost.Split(':');
                db = new MySqlConnection
                {
                    ConnectionString = String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                        host[0],
                        host.Length == 1 ? "3306" : host[1],
                        Config.MySqlDbName,
                        Config.MySqlUsername,
                        Config.MySqlPassword)
                };
            }
            else if (TShock.Config.StorageType.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
            {
                db = new SqliteConnection(
                    "uri=file://" + Path.Combine(TShock.SavePath, "PvpArena.sqlite") + ",Version=3");
            }
            else
            {
                throw new InvalidOperationException("Invalid storage type!");
            }
            return db;
        }
        private void OnGetData(GetDataEventArgs args)
        {
            var playerInfo = TShock.Players[args.Msg.whoAmI].GetPlayerInfo();
            if (playerInfo.Status == State.None)
                return;
            switch (args.MsgID)
            {
                #region OnTileChange
                case PacketTypes.Tile:
                    using (var reader = new BinaryReader(new MemoryStream(args.Msg.readBuffer, args.Index, args.Length)))
                    {
                        reader.ReadByte();
                        short x = reader.ReadInt16();
                        short y = reader.ReadInt16();
                        if ((x >= 0 && y >= 0) && (x < Main.maxTilesX && y < Main.maxTilesY))
                        {
                            SetPoints(new Point(x, y), playerInfo, TShock.Players[args.Msg.whoAmI]);
                        }
                    }
                    args.Handled = true;
                    break;
                #endregion
                #region OnMassWire
                case PacketTypes.MassWireOperation:
                    using (var reader = new BinaryReader(new MemoryStream(args.Msg.readBuffer, args.Index, args.Length)))
                    {
                        short x1 = reader.ReadInt16();
                        short y1 = reader.ReadInt16();
                        short x2 = reader.ReadInt16();
                        short y2 = reader.ReadInt16();
                        if (x1 > 0 && y1 > 0 && x2 > 0 && y2 > 0 &&
                            x1 < Main.maxTilesX && y1 < Main.maxTilesY &&
                            x2 < Main.maxTilesX && y2 < Main.maxTilesY)
                        {
                            SetPoints(new Point(x1, y1), playerInfo, TShock.Players[args.Msg.whoAmI]);
                            if (x1 != x2 || y1 != y2)
                                SetPoints(new Point(x2, y2), playerInfo, TShock.Players[args.Msg.whoAmI]);
                        }
                    }
                    args.Handled = true;
                    break;
                    #endregion
            }
        }
        private void SetPoints(Point point, PlayerInfo playerInfo, TSPlayer player)
        {
            switch (playerInfo.Status)
            {
                case State.MapSave:
                    playerInfo.Point = point;
                    player.SendSuccessMessage("First point set.");
                    playerInfo.Status = State.MapSavePoint2;
                    break;
                case State.MapSavePoint2:
                    MapManager.SaveMap(playerInfo.Name, playerInfo.Point, point);
                    player.SendSuccessMessage("Map saved successfully!");
                    playerInfo.Name = null;
                    playerInfo.Status = State.None;
                    break;
                case State.MapLoad:
                    MapManager.LoadMap(MapManager.GetMapByName(playerInfo.Name), point);
                    player.SendSuccessMessage("Map loaded successfully!");
                    playerInfo.Name = null;
                    playerInfo.Status = State.None;
                    break;
                case State.ArenaSet:
                case State.ArenaSetPoint2:
                    TShock.Log.ConsoleError("ImpossibleCode");
                    playerInfo.Status = State.None;
                    break;
            }
        }
        private void MapCmd(CommandArgs args)
        {
            string subCmd = args.Parameters.Count == 0 ? "help" : args.Parameters[0];

            switch (subCmd)
            {
                case "save":
                    if (args.Parameters.Count < 2)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /map save <name>");
                        return;
                    }
                    string name = args.Parameters[1];
                    var playerInfo = args.Player.GetPlayerInfo();
                    playerInfo.Status = State.MapSave;
                    playerInfo.Name = name;
                    args.Player.SendInfoMessage("Set 2 points or use the grand design.");
                    break;
                case "load":
                    if (args.Parameters.Count < 2)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /map load <name>");
                        return;
                    }
                    name = args.Parameters[1];
                    Map map = MapManager.GetMapByName(name);
                    if (map == null)
                    {
                        args.Player.SendErrorMessage($"Map with name {args.Parameters[1]} has not defined.");
                        return;
                    }
                    playerInfo = args.Player.GetPlayerInfo();
                    playerInfo.Status = State.MapLoad;
                    playerInfo.Name = name;
                    args.Player.SendInfoMessage("Set point for map load.");
                    break;
                case "del":
                    if (args.Parameters.Count < 2)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /map del <name>");
                        return;
                    }
                    map = MapManager.GetMapByName(args.Parameters[1]);
                    if (map == null)
                    {
                        args.Player.SendErrorMessage($"Map with name {args.Parameters[1]} has not defined.");
                        return;
                    }
                    MapManager.DeleteMap(map);
                    args.Player.SendSuccessMessage("Map deleted  successfully!");
                    break;
                case "list":
                    int page = 1;
                    if (args.Parameters.Count > 1)
                        if (!int.TryParse(args.Parameters[1], out page))
                        {
                            args.Player.SendErrorMessage($"Invalid number {args.Parameters[1]}.");
                            return;
                        }
                    PaginationTools.SendPage(args.Player, page, MapManager.MapList,
                        new PaginationTools.Settings()
                        {
                            NothingToDisplayString = "Maps not found. Use /map save <name> for map save.",
                            HeaderFormat = "Map list ({0}/{1}):",
                            FooterFormat = "Type /map list {0} for more.",
                        });
                    break;
                case "help":
                    page = 1;
                    if (args.Parameters.Count > 1)
                        if (!int.TryParse(args.Parameters[1], out page))
                        {
                            args.Player.SendErrorMessage($"Invalid number {args.Parameters[1]}.");
                            return;
                        }
                    var helpList = new List<string>
                    {
                        "save - map save in file;",
                        "load - map load in file;",
                        "del - delete map file;",
                        "list - map list;"
                    };
                    PaginationTools.SendPage(args.Player, page, helpList,
                        new PaginationTools.Settings()
                        {
                            HeaderFormat = "Map sub command list ({0}/{1}):",
                            FooterFormat = "Type /map help {0} for more.",
                        });
                    break;
                default:
                    args.Player.SendErrorMessage("Invalid sub command! Check /map help for more details.");
                    break;

            }
        }

        private void ArenaCmd(CommandArgs args)
        {
            throw new NotImplementedException();
        }

    }
}
