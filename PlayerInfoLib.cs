using Rocket.Core.Logging;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PlayerInfoLibrary
{
    public class PlayerInfoLib : RocketPlugin<PlayerInfoLibConfig>
    {
        public static PlayerInfoLib Instance;
        public static DatabaseManager Database;

        protected override void Load()
        {
            Instance = this;
            Database = new DatabaseManager();
            U.Events.OnPlayerConnected += Events_OnPlayerConnected;
            if (Instance.Configuration.Instance.KeepaliveInterval <= 0)
            {
                Logger.LogWarning("Error: Keel alive config option must be above 0.");
                Instance.Configuration.Instance.KeepaliveInterval = 10;
            }
            Instance.Configuration.Save();
        }

        protected override void Unload()
        {
            U.Events.OnPlayerConnected -= Events_OnPlayerConnected;
            Database.Unload();
            Database = null;
        }

        private void Events_OnPlayerConnected(UnturnedPlayer player)
        {
            PlayerData pData = new PlayerData(player.CSteamID, player.SteamName, player.CharacterName, player.CSteamID.GetIP(), DateTime.Now, DateTime.Now, false, false, Database.InstanceID, Provider.serverName);
            Database.SaveToDB(pData);
        }
    }
}
