using Rocket.Unturned.Player;
using SDG.Unturned;
using System;

namespace PlayerInfoLibrary
{
    public class PlayerInfoLibPComponent : UnturnedPlayerComponent
    {
        private bool start;
        private DateTime startTime;
        private float ping;

        protected override void Load()
        {
            start = false;
        }

        internal void Start()
        {
            startTime = DateTime.Now;
            start = true;
        }

        public void FixedUpdate()
        {
            if (start)
            {
                ping = Player.Ping;
                if (ping == 0)
                    ping = 1;
                if ((DateTime.Now - startTime).TotalSeconds >= 3 + (ping * 10))
                {
                    start = false;
                    PlayerData pData = PlayerInfoLib.Database.QueryById(Player.CSteamID, false);
                    int totalTime = pData.TotalPlayime;
                    DateTime loginTime = PlayerInfoLib.LoginTime[Player.CSteamID];
                    pData = new PlayerData(Player.CSteamID, Player.SteamName, Player.CharacterName, Player.CSteamID.GetIP(), loginTime, PlayerInfoLib.Database.InstanceID, Provider.serverName, PlayerInfoLib.Database.InstanceID, loginTime, false, false, totalTime);
                    PlayerInfoLib.Database.SaveToDB(pData);
                    enabled = false;
                }
            }
        }
    }
}