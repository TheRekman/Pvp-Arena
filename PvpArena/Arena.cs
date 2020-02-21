using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace PvpArena
{
    public class Arena
    {
        public int Id;
        public Map Map;
        public string Name;
        public Point Position;
        public Point Size;
        public string Align;

        public Arena(int id, string name, Map map, Point position, Point size, string align)
        {
            Id = id;
            Map = map;
            Name = name;
            Position = position;
            Size = size;
            Align = align;
        }
    }
}
