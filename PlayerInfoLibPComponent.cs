using Rocket.Unturned.Player;
using SDG.Unturned;
using System;

namespace PlayerInfoLibrary
{
    public class PlayerInfoLibPComponent : UnturnedPlayerComponent
    {
        private bool start;
        private DateTime startTime;
        private PlayerData pData;
        private float ping;

        protected override void Load()
        {
            start = false;
        }

        internal void Start(PlayerData pdata)
        {
            startTime = DateTime.Now;
            pData = pdata;
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
                    if (Player.CSteamID.GetIP() != pData.IP)
                    {
                        pData.IP = Player.CSteamID.GetIP();
                        PlayerInfoLib.Database.SaveToDB(pData);
                    }
                    enabled = false;
                }
            }
        }
    }
}