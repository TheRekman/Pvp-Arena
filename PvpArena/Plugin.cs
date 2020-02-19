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
            Commands.ChatCommands.Add(new Command(Permissions.MapUse, MapCmd, "map"));
        }

        private void MapCmd(CommandArgs args)
        {
            string subCmd = args.Parameters.Count == 0 ? "help" : args.Parameters[0];
            
            switch (subCmd)
            {
                case "save":
                    break;
                case "load":
                    break;
                case "list":
                    break;
                case "help":
                    break;
                default:
                    args.Player.SendErrorMessage("Invalid sub command! Check /map help for more details.");
                    break;
                    
            }
        }
    }
}
