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
using TShockAPI.Hooks;

namespace PvpArena
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {

        public override string Author => "Rekman";
        public override string Description => "Add functonal for pvp arena";
        public override string Name => "PvpArena";
        public override Version Version => new Version("0.1");

        public MapManager MapManager;
        public ArenaManager ArenaManager;
        public ParamManager ParamManager;
        public Config Config;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInitialize);
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);

                GetDataHandlers.PlayerUpdate -= ParamManager.OnPlayerUpdate;
                GetDataHandlers.TogglePvp -= ParamManager.OnTogglePvp;
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
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
        }

        private void OnPostInitialize(EventArgs args)
        {
            ArenaManager.Reload();
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
            ArenaManager = new ArenaManager(db, ref MapManager);
            ParamManager = new ParamManager(ref ArenaManager, ref MapManager);
            GetDataHandlers.PlayerUpdate += ParamManager.OnPlayerUpdate;
            GetDataHandlers.TogglePvp += ParamManager.OnTogglePvp;
            Commands.ChatCommands.Add(new Command(Permissions.MapUse, MapCmd, "map"));
            Commands.ChatCommands.Add(new Command(Permissions.ArenaUse, ArenaCmd, "arena"));
            Commands.ChatCommands.Add(new Command(Permissions.VoteUse, VoteCmd, "vote"));
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
            
            switch (args.MsgID)
            {
                #region OnTileChange
                case PacketTypes.Tile:
                    var playerInfo = TShock.Players[args.Msg.whoAmI].GetPlayerInfo();
                    if (playerInfo.State == State.None)
                        return;
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
                    playerInfo = TShock.Players[args.Msg.whoAmI].GetPlayerInfo();
                    if (playerInfo.State == State.None)
                        return;
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

                            if ((x1 != x2 || y1 != y2) && (playerInfo.State == State.MapSave || playerInfo.State == State.ArenaSet))
                                SetPoints(new Point(x2, y2), playerInfo, TShock.Players[args.Msg.whoAmI]);
                            SetPoints(new Point(x1, y1), playerInfo, TShock.Players[args.Msg.whoAmI]);
                        }
                    }
                    args.Handled = true;
                    break;
                #endregion

                #region OnDeath
                case PacketTypes.PlayerDeathV2:
                    var arena = ArenaManager.InArea(TShock.Players[args.Msg.whoAmI]);
                    if (arena == null) return;
                    if (TShock.Players[args.Msg.whoAmI].TPlayer.hostile)
                    {
                        playerInfo = TShock.Players[args.Msg.whoAmI].GetPlayerInfo();
                        Random rnd = new Random();
                        Point spawnPoint = arena.Map.Spawns[new Random().Next() % arena.Map.Spawns.Count()];
                        playerInfo.SpawnPoint = new Point((spawnPoint.X + arena.MapPoint.X) * 16,
                                                          (spawnPoint.Y + arena.MapPoint.Y) * 16);
                    }
                    break;
                #endregion

                #region OnSpawn
                case PacketTypes.PlayerSpawn:
                    playerInfo = TShock.Players[args.Msg.whoAmI].GetPlayerInfo();
                    if (playerInfo.SpawnPoint.X < 1) return;
                    TShock.Players[args.Msg.whoAmI].Teleport(playerInfo.SpawnPoint.X, playerInfo.SpawnPoint.Y);
                    playerInfo.SpawnPoint.X = -1;
                    TShock.Players[args.Msg.whoAmI].SendInfoMessage("You automatically teleported to the arena.");
                    break;
               #endregion

            }
        }

        private void OnUpdate(EventArgs args)
        {
            ParamManager.OnUpdate();
        }

        private void AreaFromPoints(ref Point min, ref Point max)
        {
            if(min.X > max.X)
            {
                int temp = min.X;
                min.X = max.X;
                max.X = temp;
            }
            if (min.Y > max.Y)
            {
                int temp = min.Y;
                min.Y = max.Y;
                max.Y = temp;
            }
        }

        private void SetPoints(Point point, PlayerInfo playerInfo, TSPlayer player)
        {
            switch (playerInfo.State)
            {
                case State.MapSave:
                    playerInfo.Point = point;
                    player.SendSuccessMessage("First point set.");
                    playerInfo.State = State.MapSavePoint2;
                    break;
                case State.MapSavePoint2:
                    playerInfo.Point2 = point;
                    player.SendSuccessMessage("Second point set.");
                    player.SendSuccessMessage("Select points for spawns.");
                    AreaFromPoints(ref playerInfo.Point, ref playerInfo.Point2);
                    playerInfo.State = State.MapSaveSetSpawns;
                    playerInfo.Spawns = new List<Point>();
                    break;
                case State.MapSaveSetSpawns:
                    if (point.X < playerInfo.Point.X || point.X > playerInfo.Point2.X ||
                        point.Y < playerInfo.Point.Y || point.Y > playerInfo.Point2.Y )
                    {
                        player.SendErrorMessage("Spawn points must be in save area!");
                        return;
                    }
                    playerInfo.Spawns.Add(new Point(point.X - playerInfo.Point.X, point.Y - playerInfo.Point.Y));
                    player.SendSuccessMessage($"Spawn {playerInfo.Spawns.Count} setted. Use /map end for finnaly save.");
                    break;
                case State.MapLoad:
                    MapManager.LoadMap(MapManager.GetMapByName(playerInfo.Name), point);
                    player.SendSuccessMessage("Map loaded successfully!");
                    playerInfo.State = State.None;
                    break;
                case State.ArenaSet:
                    playerInfo.Point = point;
                    player.SendSuccessMessage("First point set.");
                    playerInfo.State = State.ArenaSetPoint2;
                    break;
                case State.ArenaSetPoint2:
                    if (playerInfo.Map.Size.X > point.X - playerInfo.Point.X || playerInfo.Map.Size.Y > point.Y - playerInfo.Point.Y)
                    {
                        playerInfo.State = State.None;
                        player.SendErrorMessage($"Map size must be smaller than arena size! Request denied.");
                        return;
                    }
                    ArenaManager.SetArena(playerInfo.Name, playerInfo.Point, point, playerInfo.Align, playerInfo.Map);
                    player.SendSuccessMessage("Arena setted successfully!");
                    playerInfo.State = State.None;
                    break;
                case State.ArenaSetWithSize:
                    var size = new Point(point.X + playerInfo.Point.X, point.Y + playerInfo.Point.Y);
                    ArenaManager.SetArena(playerInfo.Name, point, size, playerInfo.Align, playerInfo.Map);
                    player.SendSuccessMessage("Arena setted successfully!");
                    playerInfo.State = State.None;
                    break;
            }
        }

        private void MapCmd(CommandArgs args)
        {
            string subCmd = args.Parameters.Count == 0 ? "help" : args.Parameters[0];

            switch (subCmd)
            {
                #region Save
                case "save":
                    if (args.Parameters.Count < 2)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /map save <name> [tags...]");
                        return;
                    }
                    string name = args.Parameters[1];
                    var playerInfo = args.Player.GetPlayerInfo();
                    playerInfo.State = State.MapSave;
                    playerInfo.Name = name;
                    playerInfo.Tags = new List<string>();
                    for (int i = 2; i < args.Parameters.Count; i++)
                        playerInfo.Tags.Add(args.Parameters[i]);
                    args.Player.SendInfoMessage("Set 2 points or use the grand design.");
                    break;
                #endregion

                #region Load
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
                    playerInfo.State = State.MapLoad;
                    playerInfo.Name = name;
                    args.Player.SendInfoMessage("Set point for map load.");
                    break;
                #endregion
                
                #region Del
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
                #endregion

                #region End
                case "end":
                    playerInfo = args.Player.GetPlayerInfo();
                    if(playerInfo.State != State.MapSaveSetSpawns)
                    {
                        args.Player.SendErrorMessage("You dont have active spawn define request. Use /region define and set map area.");
                        return;
                    }
                    if (playerInfo.Spawns.Count < 1)
                    {
                        args.Player.SendErrorMessage("Set at least 1 spawn for end save!");
                        return;
                    }
                    MapManager.SaveMap(playerInfo.Name, playerInfo.Point, playerInfo.Point2, playerInfo.Spawns.ToArray(), playerInfo.Tags);
                    args.Player.SendSuccessMessage("Map successfully saved.");
                    playerInfo.Spawns = null;
                    playerInfo.Tags = null;
                    playerInfo.State = State.None;
                    break;
                #endregion

                #region Info
                case "info":
                    if(args.Parameters.Count < 2)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /map info <name> [page]");
                        return;
                    }

                    map = MapManager.GetMapByName(args.Parameters[1]);
                    if (map == null)
                    {
                        args.Player.SendErrorMessage($"Map with name {args.Parameters[1]} has not defined.");
                        return;
                    }

                    int page = 1;
                    if (args.Parameters.Count > 2)
                        if (!int.TryParse(args.Parameters[2], out page))
                        {
                            args.Player.SendErrorMessage($"Invalid number {args.Parameters[2]}.");
                            return;
                        }
                    
                    PaginationTools.SendPage(args.Player, page, MapManager.GetMapInfo(map),
                        new PaginationTools.Settings()
                        {
                            HeaderFormat = "Map info ({0}/{1}):",
                            FooterFormat = "Type /map info " + args.Parameters[1] + " {0} for more.",
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
                    PaginationTools.SendPage(args.Player, page, PaginationTools.BuildLinesFromTerms(MapManager.MapList),
                        new PaginationTools.Settings()
                        {
                            NothingToDisplayString = "Maps not found. Use /map save <name> for map save.",
                            HeaderFormat = "Map list ({0}/{1}):",
                            FooterFormat = "Type /map list {0} for more.",
                        });
                    break;
                #endregion

                #region Tag
                case "tag":
                    page = 1;
                    if (args.Parameters.Count < 2)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /map tag <tag> [page]");
                        return;
                    }
                    if (args.Parameters.Count > 2)
                        if (!int.TryParse(args.Parameters[2], out page))
                        {
                            args.Player.SendErrorMessage($"Invalid number {args.Parameters[2]}.");
                            return;
                        }
                    PaginationTools.SendPage(args.Player, page, PaginationTools.BuildLinesFromTerms(MapManager.GetMapsNameByTag(args.Parameters[1])),
                        new PaginationTools.Settings()
                        {
                            NothingToDisplayString = "No maps with this tag.",
                            HeaderFormat = "Maps with " + args.Parameters[1] + " tag ({0}/{1}):",
                            FooterFormat = "Type /map tag " + args.Parameters[1] + " {0} for more.",
                        });
                    break;
                #endregion

                #region AddTags
                case "addtags":
                case "at":
                    if(args.Parameters.Count < 3)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /map addtags <name> <tags...>");
                        return;
                    }
                    
                    map = MapManager.GetMapByName(args.Parameters[1]);
                    if (map == null)
                    {
                        args.Player.SendErrorMessage($"Map with name {args.Parameters[1]} has not defined.");
                        return;
                    }

                    
                    var tagList = new List<string>();
                    for (int i = 2; i < args.Parameters.Count; i++)
                        tagList.Add(args.Parameters[i]);
                    MapManager.AddTags(map, tagList);
                    args.Player.SendSuccessMessage($"Successfully added tags for map.");
                    break;
                #endregion

                #region RemoveTag
                case "removetag":
                case "rt":
                    if (args.Parameters.Count < 3)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /map addtags <name> <tags...>");
                        return;
                    }

                    map = MapManager.GetMapByName(args.Parameters[1]);
                    if (map == null)
                    {
                        args.Player.SendErrorMessage($"Map with name {args.Parameters[1]} has not defined.");
                        return;
                    }

                    string tag = args.Parameters[2];
                    if(!map.Tags.Contains(tag))
                    {
                        args.Player.SendErrorMessage($"Map don't have specific tag.");
                        return;
                    }

                    MapManager.RemoveTag(map, tag);
                    args.Player.SendSuccessMessage($"Successfully removed tags for map.");
                    break;
                #endregion

                #region MapTags
                case "maptags":
                case "mt":
                    if (args.Parameters.Count < 2)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /map maptags <name> [page]");
                        return;
                    }

                    map = MapManager.GetMapByName(args.Parameters[1]);
                    if (map == null)
                    {
                        args.Player.SendErrorMessage($"Map with name {args.Parameters[1]} has not defined.");
                        return;
                    }

                    page = 1;
                    if (args.Parameters.Count > 2)
                        if (!int.TryParse(args.Parameters[2], out page))
                        {
                            args.Player.SendErrorMessage($"Invalid number {args.Parameters[2]}.");
                            return;
                        }

                    PaginationTools.SendPage(args.Player, page, PaginationTools.BuildLinesFromTerms(map.Tags),
                        new PaginationTools.Settings()
                        {
                            NothingToDisplayString = "No tags for this map.",
                            HeaderFormat = "Map tags ({0}/{1}):",
                            FooterFormat = "Type /map maptags " + args.Parameters[1] + " {0} for more.",
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
                        "save <mapname> [tags...] - map save in file;",
                        "load <mapname> - map load from file;",
                        "del <mapname> - delete map file;",
                        "list [page] - map list;",
                        "tag <tag> - map list with tag;",
                        "addtags <mapname> <tags...> - add tags for map;",
                        "removetag <mapname> <tag> - remove tag from map;",
                        "maptags <mapname> - map tag list;",
                        "info <mapname> - return map info."
                    };
                    PaginationTools.SendPage(args.Player, page, helpList,
                        new PaginationTools.Settings()
                        {
                            HeaderFormat = "Map sub command list ({0}/{1}):",
                            FooterFormat = "Type /map help {0} for more.",
                        });
                    break;
                #endregion

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
                        playerInfo.State = State.ArenaSetWithSize;
                        args.Player.SendInfoMessage("Set point for create new arena.");
                    }
                    else
                    {
                        playerInfo.State = State.ArenaSet;
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
                        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /arena align <name> <align>");
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
                            NothingToDisplayString = "Arenas not found. Use /arena define for map save",
                            HeaderFormat = "Arena list ({0}/{1}):",
                            FooterFormat = "Type /arena list {0} for more.",
                        });
                    break;
                #endregion

                #region AddParam
                case "addparam":
                case "ap":
                    
                    if (args.Parameters.Count < 3)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /arena ap <arenaname> <param>");
                        return;
                    }
                    string param = args.Parameters[2];
                    if ((param == "vote" || param == "vt"))
                    {
                        if(args.Parameters.Count != 5)
                        {
                            args.Player.SendErrorMessage($"Invalid syntax! Proper syntax: /arena ap <arenaname> vote [votetime] [voterepeat]");
                            return;
                        }
                        int i = 0;
                        if (!int.TryParse(args.Parameters[3], out i))
                        {
                            args.Player.SendErrorMessage($"Invalid number {args.Parameters[3]}!");
                            return;
                        }
                        if (!int.TryParse(args.Parameters[4], out i))
                        {
                            args.Player.SendErrorMessage($"Invalid number {args.Parameters[4]}!");
                            return;
                        }
                        param = string.Format("{0}:{1}:{2}", param, args.Parameters[3], args.Parameters[4]);
                    }

                    if (param == "autochange" || param == "ac")
                    {
                        if (args.Parameters.Count != 5)
                        {
                            args.Player.SendErrorMessage($"Invalid syntax! Proper syntax: /arena ap <arenaname> autochange [maptag] [timer]");
                            return;
                        }
                        int i = 0;
                        if (!int.TryParse(args.Parameters[3], out i))
                        {
                            args.Player.SendErrorMessage($"Invalid number{args.Parameters[3]}!");
                            return;
                        }
                        param = string.Format("{0}:{1}:{2}", param, args.Parameters[3], args.Parameters[4]);
                    }


                    arena = ArenaManager.GetArenaByName(args.Parameters[1]);
                    if (arena == null)
                    {
                        args.Player.SendErrorMessage($"Arena {args.Parameters[1]} does not exist!");
                        return;
                    }

                    if (!ParamManager.AddParam(arena, param))
                    {
                        args.Player.SendErrorMessage($"Failed add param!");
                        return;
                    }
                    args.Player.SendSuccessMessage($"Success add param!");
                    break;
                #endregion

                #region RemoveParam
                case "removeparam":
                case "rp":
                    if (args.Parameters.Count < 3)
                    {
                        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /arena rp <arenaname> <param>");
                        return;
                    }
                    param = args.Parameters[2];
                    

                    arena = ArenaManager.GetArenaByName(args.Parameters[1]);
                    if (arena == null)
                    {
                        args.Player.SendErrorMessage($"Arena {args.Parameters[1]} does not exist!");
                        return;
                    }

                    if (!ParamManager.RemoveParam(arena, param))
                    {
                        args.Player.SendErrorMessage($"Failed remove param!");
                        return;
                    }
                    args.Player.SendSuccessMessage($"Success remove param!");
                    break;
                #endregion

                #region ParamList
                case "paramlist":
                case "pl":
                    page = 1;
                    if (args.Parameters.Count > 1)
                        if (!int.TryParse(args.Parameters[1], out page))
                        {
                            args.Player.SendErrorMessage($"Invalid number {args.Parameters[1]}.");
                            return;
                        }
                    var helpList = new List<string>
                    {
                        "vote (vt) - allow vote for arena, require vote time & repeat vote time in seconds.",
                        "autochange (ac) - set random map with given tag in given time in seconds.",
                        "autopvp (ap) - activate players pvp if they in arena area.",
                        "autotp (tp) - teleport into spawn if player not in pvp.",
                        "autoinvise (ai) - give invisibile buff if player not in pvp",
                        "autospawn (as) - teleport into arena spawn if player activate pvp"
                    };
                    PaginationTools.SendPage(args.Player, page, helpList,
                        new PaginationTools.Settings()
                        {
                            HeaderFormat = "Param list ({0}/{1}):",
                            FooterFormat = "Type /param pl {0} for more.",
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
                    helpList = new List<string>
                    {
                        "define <arenaname> <mapname> [align] [width] [height] - create arena.",
                        "delete <arenaname> - remove arena.",
                        "setmap <arenaname> <mapname> - set new map for arena",
                        "align <arenaname> <align> - set new align for arena",
                        "info <arenaname> - return info about arena",
                        "list [page] - return arena list",
                        "addparam <arenaname> <param> - adds param for arena",
                        "removeparam <arenaname> <param> - remove param for arena",
                        "paramlist [page] - return param list."
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
    
        private void VoteCmd(CommandArgs args)
        {
            string subCmd = args.Parameters.Count == 0 ? "help" : args.Parameters[0];
            var arena = ArenaManager.InArea(args.Player);
            if(arena == null)
            {
                args.Player.SendErrorMessage("You must be in arena to use this command!");
                return;
            }
            if (!ParamManager.ParamContains(arena, "vote") || ParamManager.ParamContains(arena, "autochange"))
            {
                args.Player.SendErrorMessage("Voting not available for this arena!");
                return;
            }
            switch (subCmd)
            {
                #region info
                case "info":
                    int page = 1;
                    if (args.Parameters.Count > 1)
                        if (!int.TryParse(args.Parameters[1], out page))
                        {
                            args.Player.SendErrorMessage($"Invalid number {args.Parameters[1]}.");
                            return;
                        }
                    var voting = ParamManager.Votings.Find(v => v.Arena == arena);
                    if (voting == null)
                    {
                        args.Player.SendErrorMessage("Not have active voting!");
                        return;
                    }
                    var infoList = voting.GetInfo();
                    var nextVote = voting.EndDateTime - DateTime.Now;
                    PaginationTools.SendPage(args.Player, page, infoList,
                        new PaginationTools.Settings()
                        {
                            HeaderFormat = "Vote info ({0}/{1}):" +
                            $"\nVote end in in {GenerateTimeString(nextVote)}" +
                            "\nId | Map | Vote Count",
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
                        "To use this command you must be in arena.",
                        "/vote <mapId> - vote for map by id in /vote info.",
                        "/vote <mapName> - vote or add map by name.",
                        "/vote info - get map list and they votes."
                    };
                    PaginationTools.SendPage(args.Player, page, helpList,
                        new PaginationTools.Settings()
                        {
                            HeaderFormat = "Vote help ({0}/{1}):",
                            FooterFormat = "Type /vote help {0} for more.",
                        });
                    break;
                #endregion

                #region Vote
                default:
                    
                    voting = ParamManager.Votings.Find(v => v.Arena == arena);
                    Map map;
                    if (int.TryParse(subCmd, out int id) && voting != null)
                        map = voting.GetMapById(id);
                    else
                        map = MapManager.GetMapByName(subCmd);

                    if(map == null)
                    {

                        args.Player.SendErrorMessage($"Invalid map {subCmd}");
                        return;
                    }
                    if (map.Size.X > arena.Size.X || map.Size.Y > arena.Size.Y)
                    {
                        args.Player.SendErrorMessage($"Map size must be smaller than arena size!");
                        return;
                    }
                    if (voting == null)
                    {
                        if(arena.NextChange > DateTime.Now)
                        {
                            nextVote = arena.NextChange - DateTime.Now;
                            args.Player.SendErrorMessage($"Next vote will be available in {GenerateTimeString(nextVote)}");
                            return;
                        }
                        string[] param = arena.Parameters.Find(s => s.StartsWith("vote")).Split(':');
                        ParamManager.Votings.Add(new Voting(arena, int.Parse(param[1]), ref ArenaManager));
                        voting = ParamManager.Votings.Last();
                    }

                    voting.RegistVote(args.Player, map);
                    args.Player.SendSuccessMessage("Success vote!");
                    break;
                    #endregion

            }
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
