using System;
using Rocket.API;

namespace PlayerInfoLib
{
    public class PlayerInfoLibConfig : IRocketPluginConfiguration
    {
        public string DatabaseAddress = "localhost";
        public ushort DatabasePort = 3306;
        public string DatabaseUserName = "unturned";
        public string DatabasePassword = "password";
        public string DatabaseName = "unturned";
        public string DatabaseTableName = "playerinfo";
        public float KeepaliveInterval = 10;
        public float CacheTime = 180;

        public void LoadDefaults() { }
    }
}