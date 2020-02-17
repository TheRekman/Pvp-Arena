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

        public Map(string name, string path, Point size)
        {
            Name = name;
            Path = path;
            Size = size;
        }
    }
}
