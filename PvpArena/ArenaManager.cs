﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using MySql.Data.MySqlClient;
using TShockAPI.DB;
using Terraria;
using Microsoft.Xna.Framework;

namespace PvpArena
{
    public class ArenaManager
    {
        private IDbConnection DbConnection;
        private MapManager MapManager;
        public List<Arena> Arenas = new List<Arena>();

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

        public void Reload()
        {
            Arenas.Clear();
            using (var reader = DbConnection.QueryReader("SELECT * FROM Arenas WHERE WorldId = @0", Main.worldID.ToString()))
            {
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
                name, x, y, width, height, map.Name, align, Main.worldID.ToString()) ;
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

            Arenas.Add(new Arena(id, name, map, new Point(x, y), new Point(width, height), align));
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

        public Arena GetArenaByName(string name) => Arenas.FirstOrDefault(arena => arena.Name == name);

        public void LoadArenaMap(Arena arena)
        {
            Point point;
            switch (arena.Align)
            {
                case "c":
                    point = new Point(arena.Position.X / 2 - arena.Map.Size.X / 2,
                                      arena.Position.Y / 2 - arena.Map.Size.Y / 2);
                    break;
                case "tl":
                    point = arena.Position;
                    break;
                case "tr":
                    point = new Point(arena.Position.X + arena.Size.X - arena.Map.Size.X, arena.Position.Y);
                    break;
                case "bl":
                    point = new Point(arena.Position.X, arena.Position.Y + arena.Size.Y - arena.Map.Size.Y);
                    break;
                case "br":
                    point = new Point(arena.Position.X + arena.Size.X - arena.Map.Size.X,
                                      arena.Position.Y + arena.Size.Y - arena.Map.Size.Y);
                    break;
                case "l":
                    point = new Point(arena.Position.X, arena.Position.Y / 2 - arena.Map.Size.Y / 2);
                    break;
                case "r":
                    point = new Point(arena.Position.X + arena.Size.X - arena.Map.Size.X,
                                      arena.Position.Y / 2 - arena.Map.Size.Y / 2);
                    break;
                case "t":
                    point = new Point(arena.Position.X / 2 - arena.Map.Size.X / 2, arena.Position.Y);
                    break;
                case "b":
                    point = new Point(arena.Position.X / 2 - arena.Map.Size.X / 2,
                                      arena.Position.Y + arena.Size.Y - arena.Map.Size.Y);
                    break;
                default:
                    point = arena.Position;
                    break;
            }
            
            MapManager.LoadMap(arena.Map, point);
        }
    }
}
