using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
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

        public string[] MapList
        {
            get
            {
                List<string> result = new List<string>();
                Maps.ForEach(map =>
                    result.Add(string.Format("Name: {0}; Width: {1}; Height: {2}",
                                              map.Name, map.Size.X, map.Size.Y))
                );
                return result.ToArray();
            }
        }

        public void LoadMapsInfo()
        {
            var files = Directory.GetFiles(MapPath);
            for(int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileName(files[i]).Split('-')[0];
                int width;
                int height;
                using (var reader = new BinaryReader(File.OpenRead(files[i])))
                {
                    width = reader.ReadInt32();
                    height = reader.ReadInt32();
                }
                Maps.Add(new Map(name, files[i], new Point(width, height)));
            }
        }

        public void SaveMap(string name, Point start, Point end)
        {
            string FilePath;
            Map map = Maps.FirstOrDefault(mp => mp.Name == name);
            if (map != null) FilePath = map.Path;
            else FilePath = Path.Combine(MapPath, string.Format("{0}-{1}.map", name, DateTime.Now.ToShortDateString()));
            TShockAPI.TShock.Log.ConsoleInfo(FilePath);

            if (start.X > end.X) Swap(ref start.X, ref end.X);
            if (start.Y > end.Y) Swap(ref start.Y, ref end.Y);
            int width = end.X - start.X;
            int height = end.Y - start.Y;
            using (var writer = new BinaryWriter(File.OpenWrite(FilePath)))
            {
                writer.Write(width);
                writer.Write(height);
                #region Tile Save
                for (int x = start.X; x < end.X; x++)
                    for (int y = start.Y; y < end.Y; y++)
                    {
                        writer.Write(Main.tile[x, y].type); //ushort
                        writer.Write(Main.tile[x, y].wall); //byte
                        writer.Write(Main.tile[x, y].sTileHeader); //short
                        writer.Write(Main.tile[x, y].bTileHeader); //byte
                        writer.Write(Main.tile[x, y].bTileHeader2); //byte 
                        writer.Write(Main.tile[x, y].bTileHeader3); //byte
                        writer.Write(Main.tile[x, y].frameX); //short
                        writer.Write(Main.tile[x, y].frameY); //short
                        writer.Write(Main.tile[x, y].liquid); //byte
                    }
                #endregion
            }
            Maps.Add(new Map(name, FilePath, new Point(width, height)));
        }
        public void LoadMap(Map map, Point start)
        {
            using (var reader = new BinaryReader(File.OpenRead(map.Path)))
            {
                int width = reader.ReadInt32();
                int height = reader.ReadInt32();
                #region Tile Load
                for (int i = 0; i < width; i++)
                    for(int j = 0; j < height; j++)
                    {
                        int x = i + start.X;
                        int y = j + start.Y;
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
                        Main.tile[x, y] = tile;
                    }
                #endregion
            }
            Netplay.ResetSections();
        }
        public Map GetMapByName(string mapName) => Maps.FirstOrDefault(map => map.Name == mapName);

        private void Swap<T>(ref T first, ref T second)
        {
            T temp = first;
            first = second;
            second = first;
        }
    }
}
