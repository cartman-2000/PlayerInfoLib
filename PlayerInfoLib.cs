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
using Rocket.API.Collections;

namespace PlayerInfoLibrary
{
    public class PlayerInfoLib : RocketPlugin<PlayerInfoLibConfig>
    {
        public static PlayerInfoLib Instance;
        public static DatabaseManager Database;
        internal static Dictionary<CSteamID, DateTime> LoginTime = new Dictionary<CSteamID, DateTime>();

        protected override void Load()
        {
            Instance = this;
            Database = new DatabaseManager();
            U.Events.OnPlayerConnected += Events_OnPlayerConnected;
            U.Events.OnPlayerDisconnected += Events_OnPlayerDisconnected;
            if (Instance.Configuration.Instance.KeepaliveInterval <= 0)
            {
                Logger.LogWarning("Error: Keep alive config option must be above 0.");
                Instance.Configuration.Instance.KeepaliveInterval = 10;
            }
            Instance.Configuration.Save();
        }

        protected override void Unload()
        {
            U.Events.OnPlayerConnected -= Events_OnPlayerConnected;
            U.Events.OnPlayerDisconnected -= Events_OnPlayerDisconnected;

            Database.Unload();
            Database = null;
        }

        private void Events_OnPlayerConnected(UnturnedPlayer player)
        {
            if (LoginTime.ContainsKey(player.CSteamID))
                LoginTime.Remove(player.CSteamID);
            LoginTime.Add(player.CSteamID, DateTime.Now);
            PlayerData pData = Database.QueryById(player.CSteamID, false);
            int totalTime = pData.TotalPlayime;
            DateTime loginTime = PlayerInfoLib.LoginTime[player.CSteamID];
            pData = new PlayerData(player.CSteamID, player.SteamName, player.CharacterName, player.CSteamID.GetIP(), loginTime, Database.InstanceID, Provider.serverName, Database.InstanceID, loginTime, false, false, totalTime);
            Database.SaveToDB(pData);
            // Recheck the ip address in the component, the ip isn't always fully set by the time this event is called.
            PlayerInfoLibPComponent pc = player.GetComponent<PlayerInfoLibPComponent>();
            pc.Start(pData);
        }

        private void Events_OnPlayerDisconnected(UnturnedPlayer player)
        {
            if (player != null)
            {
                if (LoginTime.ContainsKey(player.CSteamID))
                {
                    PlayerData pData = Database.QueryById(player.CSteamID, false);
                    if (pData.IsValid() && pData.IsLocal())
                    {
                        int totalSessionTime = (int)(DateTime.Now - LoginTime[player.CSteamID]).TotalSeconds;
                        pData.TotalPlayime += totalSessionTime;
                        Database.SaveToDB(pData);
                    }
                    LoginTime.Remove(player.CSteamID);
                }
            }
        }


        public override TranslationList DefaultTranslations
        {
            get
            {
                return new TranslationList
                {
                    { "investigate_help", CommandInvestigate.syntax + " - " + CommandInvestigate.help },
                    { "number_of_records_found", "{0} Records found for: {1}, Page: {2} of {3}" },
                };
            }
        }
    }
}
