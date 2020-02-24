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

        public override string Author => "Rekman";

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
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInitialize);
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
            ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);
        }

        private void OnPostInitialize(EventArgs args) => ArenaManager.Reload();

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
                    playerInfo.Status = State.None;
                    break;
                case State.MapLoad:
                    MapManager.LoadMap(MapManager.GetMapByName(playerInfo.Name), point);
                    player.SendSuccessMessage("Map loaded successfully!");
                    playerInfo.Status = State.None;
                    break;
                case State.ArenaSet:
                    playerInfo.Point = point;
                    player.SendSuccessMessage("First point set.");
                    playerInfo.Status = State.ArenaSetPoint2;
                    break;
                case State.ArenaSetPoint2:
                    ArenaManager.SetArena(playerInfo.Name, playerInfo.Point, point, playerInfo.Align, playerInfo.Map);
                    if (playerInfo.Map.Size.X > point.X - playerInfo.Point.X || playerInfo.Map.Size.Y > point.Y - playerInfo.Point.Y)
                    {
                        playerInfo.Status = State.None;
                        player.SendErrorMessage($"Map size must be smaller than arena size! Request denied.");
                        return;
                    }
                    player.SendSuccessMessage("Arena setted successfully!");
                    playerInfo.Status = State.None;
                    if (Config.PrivateAutoCreate)
                        CreateRegionForArena(ArenaManager.GetArenaByName(playerInfo.Name), player);
                    break;
                case State.ArenaSetWithSize:
                    var size = new Point(point.X + playerInfo.Point.X, point.Y + playerInfo.Point.Y);
                    ArenaManager.SetArena(playerInfo.Name, point, size, playerInfo.Align, playerInfo.Map);
                    player.SendSuccessMessage("Arena setted successfully!");
                    playerInfo.Status = State.None;
                    if(Config.PrivateAutoCreate)
                        CreateRegionForArena(ArenaManager.GetArenaByName(playerInfo.Name), player);
                    break;
            }
        }

        private void CreateRegionForArena(Arena arena, TSPlayer player)
        {
            string regionName = string.Format("PvpArena-{0}", arena.Name);
            var region = TShock.Regions.GetRegionByName(regionName);
            int i = 1;
            while (region != null)
            {
                regionName = string.Format("PvpArena-{0}:{1}", arena.Name, i);
                region = TShock.Regions.GetRegionByName(regionName);
                i++;
            }
            TShock.Regions.AddRegion(arena.Position.X, arena.Position.Y, arena.Size.X, arena.Size.Y, regionName, player.User.Name, Main.worldID.ToString());
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
                        "save <mapname> - map save in file;",
                        "load <mapname> - map load from file;",
                        "del <mapname> - delete map file;",
                        "list [page] - map list;"
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
        private bool CheckAlign(TSPlayer player, string align)
        {
            var aligns = new string[]
            {
                "c",
                "t",
                "b",
                "l",
                "r",
                "tl",
                "tr",
                "bl",
                "br"
            };
            foreach (string a in aligns)
                if (a == align) return true;
            player.SendErrorMessage("Invalid align. Available aligns: c, t, b, l, r, tl, tr, bl, br");
            return false;
        }
        private void ArenaCmd(CommandArgs args)
        {
            string subCmd = args.Parameters.Count == 0 ? "help" : args.Parameters[0];


            switch (subCmd)
            {
                #region Define
                case "define":
                    if (args.Parameters.Count < 3)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /arena define <name> <mapname> [align] [width] [height]");
                        return;
                    }

                    var arena = ArenaManager.GetArenaByName(args.Parameters[1]);
                    if (arena != null)
                    {
                        args.Player.SendErrorMessage($"Arena {arena.Name} already exist!");
                        return;
                    }

                    var map = MapManager.GetMapByName(args.Parameters[2]);
                    if (map == null)
                    {
                        args.Player.SendErrorMessage($"Map {args.Parameters[2]} does not exist!");
                        return;
                    }
                    
                    string align;
                    if (args.Parameters.Count > 3)
                    {
                        if (!CheckAlign(args.Player, args.Parameters[3].ToLower()))
                            return;
                        align = args.Parameters[3].ToLower();
                    }
                    else align = "c";

                    var playerInfo = args.Player.GetPlayerInfo();
                    if (args.Parameters.Count == 6)
                    {
                        Point size;
                        if (!int.TryParse(args.Parameters[4], out size.X))
                        {
                            args.Player.SendErrorMessage($"Invalid width {args.Parameters[4]}");
                            return;
                        }
                        if (!int.TryParse(args.Parameters[5], out size.Y))
                        {
                            args.Player.SendErrorMessage($"Invalid height {args.Parameters[5]}");
                            return;
                        }
                        size.X--;
                        size.Y--;
                        if (map.Size.X > size.X || map.Size.Y > size.Y)
                        {
                            args.Player.SendErrorMessage($"Map size must be smaller than arena size!");
                            return;
                        }
                        playerInfo.Point = size;
                        playerInfo.Status = State.ArenaSetWithSize;
                        args.Player.SendInfoMessage("Set point for create new arena.");
                    }
                    else
                    {
                        playerInfo.Status = State.ArenaSet;
                        args.Player.SendInfoMessage("Set 2 point or use The Grand Design for create new arena.");
                    }
                    playerInfo.Name = args.Parameters[1];
                    playerInfo.Map = map;
                    playerInfo.Align = align;


                    break;
                #endregion

                #region Delete
                case "delete":
                    if (args.Parameters.Count < 2)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /arena delete <name> ");
                        return;
                    }

                    arena = ArenaManager.GetArenaByName(args.Parameters[1]);
                    if (arena == null)
                    {
                        args.Player.SendErrorMessage($"Arena {args.Parameters[1]} does not exist!");
                        return;
                    }

                    ArenaManager.RemoveArena(arena);
                    args.Player.SendSuccessMessage("Arena deleted successfully.");
                    break;
                #endregion

                #region Setmap
                case "setmap":
                    if (args.Parameters.Count < 3)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /arena setmap <name> <mapname> ");
                        return;
                    }

                    arena = ArenaManager.GetArenaByName(args.Parameters[1]);
                    if (arena == null)
                    {
                        args.Player.SendErrorMessage($"Arena {args.Parameters[1]} does not exist!");
                        return;
                    }

                    map = MapManager.GetMapByName(args.Parameters[2]);
                    if (map == null)
                    {
                        args.Player.SendErrorMessage($"Map {args.Parameters[2]} does not exist!");
                        return;
                    }

                    if (map.Size.X > arena.Size.X || map.Size.Y > arena.Size.Y)
                    {
                        args.Player.SendErrorMessage($"Map size must be smaller than arena size!");
                        return;
                    }
                    ArenaManager.SetMap(arena, map);
                    args.Player.SendSuccessMessage("New map setted.");
                    break;
                #endregion

                #region Align
                case "align":
                    if (args.Parameters.Count < 3)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /arena info <name> <align>");
                        return;
                    }

                    arena = ArenaManager.GetArenaByName(args.Parameters[1]);
                    if (arena == null)
                    {
                        args.Player.SendErrorMessage($"Arena {args.Parameters[1]} does not exist!");
                        return;
                    }


                    if (!CheckAlign(args.Player, args.Parameters[2].ToLower()))
                        return;
                    align = args.Parameters[2].ToLower();

                    ArenaManager.SetAlign(arena, align);
                    args.Player.SendSuccessMessage("New align setted.");
                    break;

                #endregion

                #region Info
                case "info":
                    if(args.Parameters.Count < 2)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /arena info <name> [page number]");
                        return;
                    }
                    arena = ArenaManager.GetArenaByName(args.Parameters[1]);
                    if(arena == null)
                    {
                        args.Player.SendErrorMessage($"Arena {args.Parameters[1]} does not exist!");
                        return;
                    }
                    int page = 1;
                    if (args.Parameters.Count > 2)
                        if (!int.TryParse(args.Parameters[2], out page))
                        {
                            args.Player.SendErrorMessage($"Invalid number {args.Parameters[2]}.");
                            return;
                        }
                    PaginationTools.SendPage(args.Player, page, ArenaManager.ArenaInfo(arena),
                        new PaginationTools.Settings()
                        {
                            NothingToDisplayString = "Arena not found. Use /arena define <name> <mapname> for create arena.",
                            HeaderFormat = "Arena info ({0}/{1}):",
                            FooterFormat = "Type /arena info " + arena.Name + " {0} for more.",
                        });
                    break;
                #endregion

                #region List
                case "list":
                    page = 1;
                    if (args.Parameters.Count > 1)
                        if (!int.TryParse(args.Parameters[1], out page))
                        {
                            args.Player.SendErrorMessage($"Invalid number {args.Parameters[1]}.");
                            return;
                        }
                    PaginationTools.SendPage(args.Player, page, PaginationTools.BuildLinesFromTerms(ArenaManager.ArenaList),
                        new PaginationTools.Settings()
                        {
                            HeaderFormat = "Arena list ({0}/{1}):",
                            FooterFormat = "Type /arena list {0} for more.",
                        });
                    break;
                #endregion

                #region Help
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
                        "define <name> <mapname> [align] [width] [height] - create arena.",
                        "delete <name> - remove arena.",
                        "setmap <name> <mapname> - set new map for arena",
                        "align <name> <align> - set new align for arena",
                        "info <name> - return info about arena",
                        "list [page] - return arena list"
                    };
                    PaginationTools.SendPage(args.Player, page, helpList,
                        new PaginationTools.Settings()
                        {
                            HeaderFormat = "Arena sub command list ({0}/{1}):",
                            FooterFormat = "Type /arena help {0} for more.",
                        });
                    break;
                #endregion

                default:
                    args.Player.SendErrorMessage("Invalid sub command! Check /arena help for more details.");
                    break;
            }
        }
    }
}
