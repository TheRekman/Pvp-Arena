﻿using System;
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
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
        }

        private void OnInitialize(EventArgs args)
        {
            MapManager = new MapManager(Path.Combine(TShock.SavePath, "[PvpArena] Maps"));
            Commands.ChatCommands.Add(new Command(Permissions.MapUse, MapCmd, "map"));
        }

        private void OnGetData(GetDataEventArgs args)
        {
            var playerInfo = TShock.Players[args.Msg.whoAmI].GetPlayerInfo();
            if (playerInfo.Status == State.None)
                return;
            switch (args.MsgID)
            {
                #region OnTileChange
                case PacketTypes.Tile:
                    using(var reader = new BinaryReader(new MemoryStream(args.Msg.readBuffer)))
                    {
                        reader.ReadByte();
                        short x = reader.ReadInt16();
                        short y = reader.ReadInt16();
                        if ((x >= 0 && y >= 0) && (x < Main.maxTilesX && y < Main.maxTilesY))
                        {
                            SetPoints(new Point(x, y), playerInfo, TShock.Players[args.Msg.whoAmI]);
                        }
                    }
                    args.Handled = true;
                    break;
                #endregion
                #region OnMassWire
                case PacketTypes.MassWireOperation:
                    using (var reader = new BinaryReader(new MemoryStream(args.Msg.readBuffer)))
                    {
                        short x1 = reader.ReadInt16();
                        short y1 = reader.ReadInt16();
                        short x2 = reader.ReadInt16();
                        short y2 = reader.ReadInt16();
                        if (x1 > 0 && y1 > 0 && x2 > 0 && y2 > 0 &&
                            x1 < Main.maxTilesX && y1 < Main.maxTilesY &&
                            x2 < Main.maxTilesX && y2 < Main.maxTilesY)
                        {
                            SetPoints(new Point(x1, y1), playerInfo, TShock.Players[args.Msg.whoAmI]);
                            if (x1 != x2 || y1 != y2)
                                SetPoints(new Point(x2, y2), playerInfo, TShock.Players[args.Msg.whoAmI]);
                        }
                    }
                    args.Handled = true;
                    break;
                #endregion
            }
        }
        private void SetPoints(Point point, PlayerInfo playerInfo, TSPlayer player)
        {
            switch (playerInfo.Status)
            {
                case State.MapSave:
                    playerInfo.Point = point;
                    player.SendSuccessMessage("First point set.");
                    playerInfo.Status = State.MapSavePoint2;
                    break;
                case State.MapSavePoint2:
                    MapManager.SaveMap(playerInfo.MapName, playerInfo.Point, point);
                    player.SendSuccessMessage("Map saved successfully!");
                    playerInfo.MapName = null;
                    playerInfo.Status = State.None;
                    break;
                case State.MapLoad:
                    MapManager.LoadMap(MapManager.GetMapByName(playerInfo.MapName), point);
                    player.SendSuccessMessage("Map loaded successfully!");
                    playerInfo.MapName = null;
                    playerInfo.Status = State.None;
                    break;
                case State.ArenaSet:
                case State.ArenaSetPoint2:
                    TShock.Log.ConsoleError("ImpossibleCode");
                    playerInfo.Status = State.None;
                    break;
            }
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
