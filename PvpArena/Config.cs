using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using TShockAPI;
namespace PvpArena
{
    public class Config
    {

        public bool PrivateAutoCreate = true;
        public int VoteTime = 600;
        public int RepeatVoteTime = 1300;

        public string MySqlHost = "";
        public string MySqlDbName = "";
        public string MySqlUsername = "";
        public string MySqlPassword = "";

        public static Config Read(string path)
        {
            if (!File.Exists(path))
                return Create(path);
            try
            {
                string jsonString = File.ReadAllText(path);
                var conf = JsonConvert.DeserializeObject<Config>(jsonString);
                return conf;
            }
            catch (Exception e)
            {
                TShock.Log.ConsoleError("[PvpArena] Failed read config file.");
                TShock.Log.ConsoleError(e.Message);
                return new Config();
            }
        }

        public static Config Create(string path)
        {
            try
            {
                var conf = new Config();
                File.WriteAllText(path, JsonConvert.SerializeObject(conf, Formatting.Indented));
                TShock.Log.Info("[PvpArena] New config file created.");
                return conf;
            }
            catch
            {
                TShock.Log.ConsoleError("[PvpArena] Failed create config file.");
                throw;
            }

        }
    }
}
