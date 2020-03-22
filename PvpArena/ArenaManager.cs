using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using MySql.Data.MySqlClient;
using TShockAPI.DB;
using Terraria;
using Microsoft.Xna.Framework;
using TShockAPI;

namespace PvpArena
{
    public class ArenaManager
    {
        private IDbConnection DbConnection;
        private MapManager MapManager;
        public List<Arena> Arenas = new List<Arena>();

        public List<string> ArenaList
        {
            get
            {
                var result = new List<string>();
                Arenas.ForEach(arena => result.Add(arena.Name));
                return result;
            }
        }

        public List<string> ArenaInfo(Arena arena)
        {
            var result = new List<string>
            {
                $"Name: {arena.Name};",
                $"X: {arena.Position.X}, Y: {arena.Position.Y}, W: {arena.Size.X}, H: {arena.Size.Y};",
                $"Map: {arena.Map.Name};",
                $"Align: {arena.Align};"
            };
            var param = PaginationTools.BuildLinesFromTerms(arena.Parameters);
            
            if(param != null && param.Count != 0)
                param[0] = "Params: " + param[0];
            result.AddRange(param);
            return result;
        }

        public ArenaManager(IDbConnection db, ref MapManager mapManager)
        {
            DbConnection = db;
            MapManager = mapManager;

            var table = new SqlTable("Arenas",
                new SqlColumn("Id", MySqlDbType.Int32) { AutoIncrement = true, Primary = true },
                new SqlColumn("Name", MySqlDbType.Text),
                new SqlColumn("X", MySqlDbType.Int32),
                new SqlColumn("Y", MySqlDbType.Int32),
                new SqlColumn("Width", MySqlDbType.Int32),
                new SqlColumn("Height", MySqlDbType.Int32),
                new SqlColumn("MapName", MySqlDbType.Text),
                new SqlColumn("Align", MySqlDbType.Text),
                new SqlColumn("Parameters", MySqlDbType.Text),
                new SqlColumn("Region", MySqlDbType.Text),
                new SqlColumn("WorldId", MySqlDbType.Text)
                );

            var creator = new SqlTableCreator(db,
                db.GetSqlType() == SqlType.Sqlite
                ? (IQueryBuilder)new SqliteQueryCreator()
                : new MysqlQueryCreator());
            creator.EnsureTableStructure(table);
        }

        public Arena InArea(TSPlayer player) =>
            Arenas.FirstOrDefault(arena => CheckInArea(player, arena));
        
        public bool CheckInArea(TSPlayer player, Arena arena) =>
            player.TileX > arena.Position.X && player.TileX < arena.Position.X + arena.Size.X &&
            player.TileY > arena.Position.Y && player.TileY < arena.Position.Y + arena.Size.Y;

        public void Reload()
        {
            using (var reader = DbConnection.QueryReader("SELECT * FROM Arenas WHERE WorldId = @0", Main.worldID.ToString()))
            {
                Arenas.Clear();
                while (reader.Read())
                {
                    int id = reader.Get<int>("Id");
                    string name = reader.Get<string>("Name");
                    int x = reader.Get<int>("X");
                    int y = reader.Get<int>("Y");
                    int width = reader.Get<int>("Width");
                    int height = reader.Get<int>("Height");
                    string mapName = reader.Get<string>("MapName");
                    string align = reader.Get<string>("Align");
                    string paramString = reader.Get<string>("Parameters");
                    List<string> paramList = GetParamsFromString(paramString);

                    Map map = MapManager.GetMapByName(mapName);
                    Arena arena = new Arena(id, name, map, new Point(x, y), new Point(width, height), align) { Parameters = paramList };
                    
                    string regionName = reader.Get<string>("Region");
                    var region = TShock.Regions.GetRegionByName(regionName);
                    arena.Region = region;
                    Arenas.Add(arena);
                    if (map == null) TShock.Log.ConsoleError($"Failed find map for {arena.Name} arena.");
                    else LoadArenaMap(arena);
                }
            }
        }

        public void AddParam(Arena arena, string param)
        {
            arena.Parameters.Add(param);
            DbConnection.Query("UPDATE Arenas SET Parameters = @0 WHERE Id = @1", GenerateParamString(arena.Parameters), arena.Id);
        }

        public void RemoveParam(Arena arena, string param)
        {
            arena.Parameters.Remove(param);
            DbConnection.Query("UPDATE Arenas SET Parameters = @0 WHERE Id = @1", GenerateParamString(arena.Parameters), arena.Id);
        }


        private string GenerateParamString(List<string> paramList)
        {
            string result = "";
            for (int i = 0; i < paramList.Count; i++)
                result += paramList[i] + "|";
            if (result.Length > 0) result = result.Remove(result.Length - 1, 1);
            return result;
        }

        private List<string> GetParamsFromString(string paramString)
        {
            if (string.IsNullOrEmpty(paramString))
                return new List<string>();
            var result = new List<string>(paramString.Split('|'));
            result.RemoveAll(s => string.IsNullOrEmpty(s));
            return result;
        }
            

        public void SetArena(string name, Point first, Point second, string align, Map map)
        {
            int x = Math.Min(first.X, second.X);
            int y = Math.Min(first.Y, second.Y);
            int width = Math.Max(first.X, second.X) - x;
            int height = Math.Max(first.Y, second.Y) - y;

            DbConnection.Query(
                "INSERT INTO Arenas (Name, X, Y, Width, Height, MapName, Align, Parameters, Region, WorldId) VALUES (@0, @1, @2, @3, @4, @5, @6, @7, @8, @9)",
                name, x, y, width, height, map.Name, align, "", "", Main.worldID.ToString());
            int id;
            using (QueryResult res = DbConnection.QueryReader("SELECT Id FROM Arenas WHERE Name = @0 AND WorldId = @1", name, Main.worldID.ToString()))
            {
                if (res.Read())
                {
                    id = res.Get<int>("Id");
                }
                else
                {
                    return;
                }
            }
            var arena = new Arena(id, name, map, new Point(x, y), new Point(width, height), align) { Parameters = new List<string>()};
            var reg = CreateRegionForArena(arena);
            arena.Region = reg;
            Arenas.Add(arena);
            LoadArenaMap(arena);
        }

        private Region CreateRegionForArena(Arena arena)
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
            DbConnection.Query("UPDATE Arenas SET Region = @0 WHERE Id = @1", regionName, arena.Id);
            TShock.Regions.AddRegion(arena.Position.X, arena.Position.Y, arena.Size.X, arena.Size.Y, regionName, "", Main.worldID.ToString());
            return TShock.Regions.GetRegionByName(regionName);
        }

        public void RemoveArena(Arena arena)
        {
            DbConnection.Query("DELETE FROM Arenas WHERE Name = @0 AND WorldId = @1", arena.Name, Main.worldID.ToString());
            Arenas.Remove(arena);
        }

        public void SetMap(Arena arena, Map map)
        {
            arena.Map = map;
            DbConnection.Query("UPDATE Arenas SET MapName = @0 WHERE Id = @1", map.Name, arena.Id);
            LoadArenaMap(arena);
        }

        public void SetAlign(Arena arena, string align)
        {
            arena.Align = align;
            DbConnection.Query("UPDATE Arenas SET Align = @0 WHERE Id = @1", align, arena.Id);
            LoadArenaMap(arena);
        }

        public Arena GetArenaByName(string name) => Arenas.FirstOrDefault(arena => arena.Name == name);

        public List<TSPlayer> GetPlayersInArena(Arena arena)
        {
            List<TSPlayer> result = new List<TSPlayer>();
            for (int i = 0; i < TShock.Players.Count(); i++)
                if (TShock.Players[i] != null && CheckInArea(TShock.Players[i], arena))
                    result.Add(TShock.Players[i]);
            return result;
        }

        public void LoadArenaMap(Arena arena)
        {
            ClearArea(arena.Position, new Point(arena.Position.X + arena.Size.X, arena.Position.Y + arena.Size.Y));
            MapManager.LoadMap(arena.Map, arena.MapPoint);
            ReSpawnPlayers(arena);
        }

        private void ClearArea(Point first, Point second)
        {
            int startX = Math.Min(first.X, second.X);
            int startY = Math.Min(first.Y, second.Y);
            int endX = Math.Max(first.X, second.X);
            int endY = Math.Max(first.Y, second.Y);
            for (int x = startX; x <= endX; x++)
                for (int y = startY; y <= endY; y++)
                    Main.tile[x, y] = new Tile();
        }

        private void ReSpawnPlayers(Arena arena)
        {
            var players = GetPlayersInArena(arena);
            for (int i = 0; i < players.Count; i++)
            {
                players[i].Heal();
                int id = new Random().Next() % arena.Map.Spawns.Count();
                players[i].Teleport((arena.MapPoint.X + arena.Map.Spawns[id].X) * 16, (arena.MapPoint.Y + arena.Map.Spawns[id].Y) * 16);
                players[i].SendInfoMessage($"Arena changed to {arena.Map.Name}");
            }
        }

    }
}
