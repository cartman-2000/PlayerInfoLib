using Rocket.Core.Logging;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using Rocket.API.Collections;
using Rocket.API;

namespace PlayerInfoLibrary
{
    public class PlayerInfoLib : RocketPlugin<PlayerInfoLibConfig>
    {
        public static PlayerInfoLib Instance;
        public static DatabaseManager Database;
        private DateTime lastCheck = DateTime.Now;
        internal static Dictionary<CSteamID, DateTime> LoginTime = new Dictionary<CSteamID, DateTime>();
        private DateTime lastCheckExpiredPInfo = DateTime.Now;

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
            if (Instance.Configuration.Instance.ExpiredCheckInterval < 1)
            {
                Logger.LogWarning("Error: Expired check interval must bu above 1.");
                Instance.Configuration.Instance.ExpiredCheckInterval = 30;
            }
            Instance.Configuration.Save();
            if (Database.Initialized)
                Logger.Log(string.Format("PlayerInfoLib plugin has been loaded, Server Instance ID is: {0}", Database.InstanceID), ConsoleColor.Yellow);
            else
                Logger.Log("There was in issue loading the plugin, please check your config.", ConsoleColor.Red);
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

        public void FixedUpdate()
        {
            if (State == PluginState.Loaded)
            {
                if ((DateTime.Now - lastCheck).TotalMinutes >= Configuration.Instance.KeepaliveInterval)
                {
                    lastCheck = DateTime.Now;
                    Database.CheckConnection();
                }
                if ((DateTime.Now - lastCheckExpiredPInfo).TotalMinutes >= Configuration.Instance.ExpiredCheckInterval)
                {
                    lastCheckExpiredPInfo = DateTime.Now;
                    Database.CheckExpired();
                    if (Configuration.Instance.ExpiresAfter >= 1)
                        Database.PrecessExpiredPInfo();
                }
            }
        }

        public override TranslationList DefaultTranslations
        {
            get
            {
                return new TranslationList
                {
                    { "too_many_parameters", "Too many parameters." },
                    { "investigate_help", CommandInvestigate.syntax + " - " + CommandInvestigate.help },
                    { "delint_help", CommandDelInstance.syntax + " - " + CommandDelInstance.help },
                    { "rnint_help", CommandRnInstance.syntax + " - " + CommandRnInstance.help },
                    { "invalid_page", "Error: Invalid page number." },
                    { "number_of_records_found", "{0} Records found for: {1}, Page: {2} of {3}" },
                    { "delint_invalid", "Error: Invalid Instance ID." },
                    { "delint_not_found", "Error: Failed to find Instance ID in the database." },
                    { "delint_success", "Successfully Removed all data for this Instance ID, if you removed the data for this server, you will need to reload the plugin for it to be operational again." },
                    { "rnint_success", "Successfully changed the instance name for this server in the Database, Server should be restarted now." },
                    { "rnint_not_found", "Error: Failed to set the new instance name to the Database." },
                };
            }
        }
    }
}
