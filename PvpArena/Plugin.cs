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

namespace PvpArena
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {

        public override string Author => "Noname";

        public override string Description => "Add functonal for pvp arena";

        public override string Name => "PvpArena";


        public MapManager MapManager;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {

            }
        }



        public Plugin(Main game) : base(game)
        {
        }

        public override void Initialize()
        {
            MapManager = new MapManager(Path.Combine(TShock.SavePath, "[PvpArena] Maps"));
            Commands.ChatCommands.Add(new Command(MapCmd, "map"));
        }

        private void MapCmd(CommandArgs args)
        {
            if (args.Parameters[0] == "save")
            {
                MapManager.SaveMap("TestMap", new Point(args.Player.TileX - 20, args.Player.TileY - 20), new Point(args.Player.TileX + 20, args.Player.TileY + 20));
                args.Player.SendSuccessMessage("Save success!");
            }
            else if (args.Parameters[0] == "load")
            {
                MapManager.LoadMapByName("TestMap", new Point(args.Player.TileX, args.Player.TileY));
                args.Player.SendSuccessMessage("Load success!");
            }
        }
    }
}
