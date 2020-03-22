using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using OTAPI.Tile;
using TShockAPI;

namespace PvpArena
{
    public class MapManager
    {
        public string MapPath;
        public List<Map> Maps = new List<Map>();

        public MapManager(string mapPath)
        {
            if (!Directory.Exists(mapPath)) Directory.CreateDirectory(mapPath);
            MapPath = Path.GetFullPath(mapPath);
            LoadMapsInfo();
        }

        public List<string> MapList
        {
            get
            {
                List<string> result = new List<string>();
                Maps.ForEach(map =>
                    result.Add(map.Name)
                );
                return result;
            }
        }

        public List<string> GetMapInfo(Map map)
            =>
            new List<string>
            {
                $"Name: {map.Name}",
                $"Width: {map.Size.X}; Height: {map.Size.Y}",
                $"Spawn Count: {map.Spawns.Length}",
                $"Tags Count: {map.Tags.Count}"
            };
        
        public void DeleteMap(Map map)
        {
            Maps.Remove(map);
            File.Delete(map.Path);
        }

        private void LoadMapsInfo()
        {
            var files = Directory.GetFiles(MapPath, "*.dat");
            for(int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileName(files[i]).Split('-')[1];
                int width;
                int height;
                Point[] spawns;
                List<string> tags = new List<string>();
                using (var reader = new BinaryReader(File.OpenRead(files[i])))
                {
                    width = reader.ReadInt32();
                    height = reader.ReadInt32();

                    int spawnCount = reader.ReadInt32();
                    spawns = new Point[spawnCount];
                    for (int j = 0; j < spawnCount; j++)
                    {
                        int x = reader.ReadInt32();
                        int y = reader.ReadInt32();
                        spawns[j] = new Point(x, y);
                    }

                    int tagsCount = reader.ReadInt32();
                    for (int j = 0; j < tagsCount; j++)
                        tags.Add(reader.ReadString());

                }
                Maps.Add(new Map(name, files[i], new Point(width, height), spawns) { Tags = tags });
            }
        }

        public void SaveMap(string name, Point first, Point second, Point[] spawns, List<string> tags)
        {
            string FilePath;
            Map map = Maps.FirstOrDefault(mp => mp.Name == name);
            if (map != null) FilePath = map.Path;
            else FilePath = Path.Combine(MapPath, string.Format("PvpMap-{0}-{1}.dat", name, DateTime.Now.ToShortDateString()));

            int startX = Math.Min(first.X, second.X);
            int startY = Math.Min(first.Y, second.Y);
            int endX = Math.Max(first.X, second.X);
            int endY = Math.Max(first.Y, second.Y);
            int width = endX - startX + 1;
            int height = endY - startY + 1;

            using (var writer = new BinaryWriter(File.OpenWrite(FilePath)))
            {
                writer.Write(width);
                writer.Write(height);
                writer.Write(spawns.Length);
                for(int i = 0; i < spawns.Length; i++)
                {
                    writer.Write(spawns[i].X);
                    writer.Write(spawns[i].Y);
                }

                #region Tags Save
                writer.Write(tags.Count);
                for (int i = 0; i < tags.Count; i++)
                    writer.Write(tags[i]);
                #endregion

                #region Tile Save
                var tiles = new ITile[width, height];
                for (int x = startX; x <= endX; x++)
                    for (int y = startY; y <= endY; y++)
                        tiles[x - startX, y - startY] = Main.tile[x, y];
                WriteTiles(writer, tiles);
                #endregion

            }
            Maps.Add(new Map(name, FilePath, new Point(width, height), spawns) { Tags = tags });
        }

        public void AddTags(Map map, List<string> tags)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                if (map.Tags.Contains(tags[i])) continue;
                map.Tags.Add(tags[i]);
            }
            RewriteMapTags(map);
        }

        public void RemoveTag(Map map, string tag)
        {
            if (map.Tags.Contains(tag))
            {
                map.Tags.Remove(tag);
                RewriteMapTags(map);
            }
        }

        private void RewriteMapTags(Map map)
        {
            var tiles = ReadTiles(map);
            File.Delete(map.Path);
            using (var writer = new BinaryWriter(File.OpenWrite(map.Path)))
            {
                writer.Write(map.Size.X);
                writer.Write(map.Size.Y);
                writer.Write(map.Spawns.Length);
                for (int i = 0; i < map.Spawns.Length; i++)
                {
                    writer.Write(map.Spawns[i].X);
                    writer.Write(map.Spawns[i].Y);
                }
                #region Tags Save
                writer.Write(map.Tags.Count);
                for (int i = 0; i < map.Tags.Count; i++)
                    writer.Write(map.Tags[i]);
                #endregion

                WriteTiles(writer, tiles);

            }

        }

        private void WriteTiles(BinaryWriter writer, ITile[,] tiles)
        {
            for (int x = 0; x < tiles.GetLength(0); x++)
                for (int y = 0; y < tiles.GetLength(1); y++)
                {
                    writer.Write(tiles[x, y].type); //ushort
                    writer.Write(tiles[x, y].wall); //byte
                    writer.Write(tiles[x, y].sTileHeader); //short
                    writer.Write(tiles[x, y].bTileHeader); //byte
                    writer.Write(tiles[x, y].bTileHeader2); //byte 
                    writer.Write(tiles[x, y].bTileHeader3); //byte
                    writer.Write(tiles[x, y].frameX); //short
                    writer.Write(tiles[x, y].frameY); //short
                    writer.Write(tiles[x, y].liquid); //byte
                }
        }

        private Tile[,] ReadTiles(Map map)
        {
            Tile[,] tiles;
            using (var reader = new BinaryReader(File.OpenRead(map.Path)))
            {
                int width = reader.ReadInt32();
                int height = reader.ReadInt32();

                int spawnCount = reader.ReadInt32();
                for (int i = 0; i < spawnCount; i++)
                {
                    reader.ReadInt32();
                    reader.ReadInt32();
                }
                    

                int tagsCount = reader.ReadInt32();
                for (int i = 0; i < tagsCount; i++)
                    reader.ReadString();

                tiles = ReadTiles(reader, width, height);
            }
            return tiles;
        }

        private Tile[,] ReadTiles(BinaryReader reader, int width, int height)
        {
            Tile[,] tiles = new Tile[width, height];
            for (int i = 0; i < width; i++)
                for (int j = 0; j < height; j++)
                {
                    Tile tile = new Tile();
                    tile.type = reader.ReadUInt16();
                    tile.wall = reader.ReadByte();
                    tile.sTileHeader = reader.ReadInt16();
                    tile.bTileHeader = reader.ReadByte();
                    tile.bTileHeader2 = reader.ReadByte();
                    tile.bTileHeader3 = reader.ReadByte();
                    tile.frameX = reader.ReadInt16();
                    tile.frameY = reader.ReadInt16();
                    tile.liquid = reader.ReadByte();
                    tiles[i, j] = tile;
                }
            return tiles;
        }

        public void LoadMap(Map map, Point start)
        {
            using (var reader = new BinaryReader(File.OpenRead(map.Path)))
            {
                int width = reader.ReadInt32();
                int height = reader.ReadInt32();
                int spawnCount = reader.ReadInt32();
                Point[] spawns = new Point[spawnCount];
                for (int i = 0; i < spawnCount; i++)
                {
                    int x = reader.ReadInt32();
                    int y = reader.ReadInt32();
                    spawns[i] = new Point(x, y);
                }

                int tagsCount = reader.ReadInt32();
                for (int i = 0; i < tagsCount; i++)
                    reader.ReadString();

                #region Tile Load
                var tiles = ReadTiles(reader, width, height);
                for (int i = 0; i < width; i++)
                    for(int j = 0; j < height; j++)
                    {
                        int x = i + start.X;
                        int y = j + start.Y;
                        Main.tile[x, y] = tiles[i, j];
                    }
                #endregion

            }
            Netplay.ResetSections();
        }

        public Map GetMapByName(string mapName) => Maps.FirstOrDefault(map => map.Name == mapName);

        public List<string> GetMapsNameByTag(string tag)
        {
            List<Map> maps;
            if (tag.StartsWith("!"))
                maps = Maps.FindAll(m => !m.Tags.Contains(tag.Remove(0, 1)));
            else
                maps = Maps.FindAll(m => m.Tags.Contains(tag));
            var result = new List<string>();
            maps.ForEach(m => result.Add(m.Name));
            return result;
        }
        public List<Map> GetMapsByTag(string tag)
        {
            List<Map> maps;
            if (tag.StartsWith("!"))
                maps = Maps.FindAll(m => !m.Tags.Contains(tag.Remove(0, 1)));
            else
                maps = Maps.FindAll(m => m.Tags.Contains(tag));
            return maps;
        }
    }
}
