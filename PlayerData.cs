using Steamworks;
using System;

namespace PlayerInfoLibrary
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
        internal ushort LastServerID { get; set; }
        public string LastServerName { get; internal set; }
        public DateTime CacheTime { get; internal set; }

        public PlayerData() { SteamID = CSteamID.Nil; }
        internal PlayerData(CSteamID steamID, string steamName, string characterName, string ip, DateTime lastLoginGlobal, DateTime lastLoginLocal, bool cleanedBuildables, bool cleanedPlayerData, ushort lastServerID, string lastServerName)
        {
            SteamID = steamID;
            SteamName = steamName;
            CharacterName = characterName;
            IP = ip;
            LastLoginGlobal = lastLoginGlobal;
            LastLoginLocal = lastLoginLocal;
            CleanedBuildables = cleanedBuildables;
            CleanedPlayerData = cleanedPlayerData;
            LastServerID = lastServerID;
            LastServerName = lastServerName;
        }
    }
}