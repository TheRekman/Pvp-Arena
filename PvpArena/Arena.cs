using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using TShockAPI.DB;
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
        public DateTime NextChange;
        public Region Region { get; set; }
        public List<string> Parameters { get; set; }
        public Point MapPoint
        {
            get
            {
                Point point;
                switch (Align)
                {
                    case "c": //center
                        point = new Point(Position.X + Size.X / 2 - Map.Size.X / 2,
                                          Position.Y + Size.Y / 2 - Map.Size.Y / 2);
                        break;
                    case "tl": //topleft
                        point = Position;
                        break;
                    case "tr": //topright
                        point = new Point(Position.X + Size.X - Map.Size.X + 1, Position.Y);
                        break;
                    case "bl": //bottomleft
                        point = new Point(Position.X, Position.Y + Size.Y - Map.Size.Y + 1);
                        break;
                    case "br": //bottomright
                        point = new Point(Position.X + Size.X - Map.Size.X + 1,
                                          Position.Y + Size.Y - Map.Size.Y + 1);
                        break;
                    case "l": //left
                        point = new Point(Position.X, Position.Y + Size.Y / 2 - Map.Size.Y / 2);
                        break;
                    case "r": //right
                        point = new Point(Position.X + Size.X - Map.Size.X + 1,
                                          Position.Y + Size.Y / 2 - Map.Size.Y / 2);
                        break;
                    case "t": //top
                        point = new Point(Position.X + Size.X / 2 - Map.Size.X / 2, Position.Y);
                        break;
                    case "b": //bottom
                        point = new Point(Position.X + Size.X / 2 - Map.Size.X / 2,
                                          Position.Y + Size.Y - Map.Size.Y + 1);
                        break;
                    default:
                        point = Position;
                        break;
                }
                return point;
            }
        }
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
