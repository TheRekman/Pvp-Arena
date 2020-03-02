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

        public List<string> ArenaInfo(Arena arena) =>
            new List<string>
            {
                $"Name: {arena.Name};",
                $"X: {arena.Position.X}, Y: {arena.Position.Y};",
                $"Width: {arena.Size.X};",
                $"Height: {arena.Size.Y};",
                $"Map: {arena.Map}",
                $"Align: {arena.Align};"
            };

        public ArenaManager(IDbConnection db, MapManager mapManager)
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

                    Map map = MapManager.GetMapByName(mapName);
                    Arena arena = new Arena(id, name, map, new Point(x, y), new Point(width, height), align);
                    Arenas.Add(arena);
                    if (map == null) TShockAPI.TShock.Log.ConsoleError($"Failed find map for {arena.Name} arena.");
                    else LoadArenaMap(arena);
                }
            }
        }
        public void SetArena(string name, Point first, Point second, string align, Map map)
        {
            int x = Math.Min(first.X, second.X);
            int y = Math.Min(first.Y, second.Y);
            int width = Math.Max(first.X, second.X) - x;
            int height = Math.Max(first.Y, second.Y) - y;

            DbConnection.Query(
                "INSERT INTO Arenas (Name, X, Y, Width, Height, MapName, Align, WorldId) VALUES (@0, @1, @2, @3, @4, @5, @6, @7)",
                name, x, y, width, height, map.Name, align, Main.worldID.ToString());
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
            var arena = new Arena(id, name, map, new Point(x, y), new Point(width, height), align);
            Arenas.Add(arena);
            LoadArenaMap(arena);
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
                if (TShock.Players[i].Active && CheckInArea(TShock.Players[i], arena))
                    result.Add(TShock.Players[i]);
            return result;
        }

        public void LoadArenaMap(Arena arena)
        {
            ClearArea(arena.Position, new Point(arena.Position.X + arena.Size.X, arena.Position.Y + arena.Size.Y));
            MapManager.LoadMap(arena.Map, arena.MapPoint);
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
    }
}
