using Steamworks;
using System;

namespace PlayerInfoLib
{
    public class PlayerData
    {
        public CSteamID SteamID { get; private set; }
        public string SteamName { get; internal set; }
        public string CharacterName { get; internal set; }
        public string IP { get; internal set; }
        public DateTime LastLoginGlobal { get; internal set; }
        public DateTime LastLoginLocal { get; internal set; }
        public bool CleanedBuildables { get; internal set; }
        public bool CleanedPlayerData { get; internal set; }
        public DateTime CacheTime { get; internal set; }

        public PlayerData() { }
        internal PlayerData(CSteamID steamID, string steamName, string characterName, string ip, DateTime lastLoginGlobal, DateTime lastLoginLocal, bool cleanedBuildables, bool cleanedPlayerData)
        {
            SteamID = steamID;
            SteamName = steamName;
            CharacterName = characterName;
            IP = ip;
            LastLoginGlobal = lastLoginGlobal;
            LastLoginLocal = lastLoginLocal;
            CleanedBuildables = cleanedBuildables;
            CleanedPlayerData = cleanedPlayerData;
        }
    }
}