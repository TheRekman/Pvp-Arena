using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace PvpArena
{
    public class Map
    {
        public string Name { get; }
        public string Path { get; }
        public Point Size { get; }
        public Point[] Spawns { get; }
        public List<string> Tags { get; set; }

        public Map(string name, string path, Point size, Point[] spawns)
        {
            Name = name;
            Path = path;
            Size = size;
            Spawns = spawns;
        }
    }
}
