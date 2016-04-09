using Rocket.API;
using Rocket.API.Extensions;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Commands;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PlayerInfoLibrary
{
    public class CommandInvestigate : IRocketCommand
    {
        internal static readonly string syntax = "<\"Player name\" | SteamID> [page]";
        internal static readonly string help = "Returns info for players matching the search quarry.";
        public List<string> Aliases
        {
            get { return new List<string>(); }
        }

        public AllowedCaller AllowedCaller
        {
            get { return AllowedCaller.Both; }
        }

        public string Help
        {
            get { return help; }
        }

        public string Name
        {
            get { return "investigate2"; }
        }

        public List<string> Permissions
        {
            get { return new List<string> { "PlayerInfoLib.Ivestigate" }; }
        }

        public string Syntax
        {
            get { return syntax; }
        }

        public void Execute(IRocketPlayer caller, string[] command)
        {
            if (command.Length == 0)
            {
                UnturnedChat.Say(caller, Syntax + " - " + Help);
            }
            CSteamID cSteamID;
            uint totalRecods = 1;
            uint? page = 1;
            List<PlayerData> pInfo = new List<PlayerData>();
            if (command.Length >= 1)
            {
                if (command.Length ==2)
                {
                    page = command.GetUInt32Parameter(1);
                }
                if (command.Length > 2)
                {
                    UnturnedChat.Say(caller, "Too many parameters.");
                    return;
                }
                // Is what is entered in the command a SteamID64 number?
                if (command[0].isCSteamID(out cSteamID))
                {
                    PlayerData pData = PlayerInfoLib.Database.QueryById(cSteamID);
                    if(pData.IsValid())
                        pInfo.Add(pData);
                }
                else
                {
                    if (caller is ConsolePlayer)
                        pInfo = PlayerInfoLib.Database.QueryByName(command[0], QueryType.Both, out totalRecods, true, (uint)page, 10);
                    else
                        pInfo = PlayerInfoLib.Database.QueryByName(command[0], QueryType.Both, out totalRecods, true, (uint)page);
                }
                if (pInfo.Count > 0)
                {
                    foreach (PlayerData pData in pInfo)
                    {
                        if (pData.IsLocal())
                            UnturnedChat.Say(caller, string.Format("Info: {0} [{1}] ({2}), IP: {3}, Local: {4}, Seen: {5}, Cleaned:{6}:{7}", caller is ConsolePlayer ? pData.CharacterName : pData.CharacterName.Truncate(14), caller is ConsolePlayer ? pData.SteamName : pData.SteamName.Truncate(14), pData.SteamID, pData.IP, pData.IsLocal(), pData.LastLoginLocal, pData.CleanedBuildables, pData.CleanedPlayerData));
                        else
                            UnturnedChat.Say(caller, string.Format("Info: {0} [{1}] ({2}), IP: {3}, Local: {4}, Seen: {5} on: {6}:{7}", caller is ConsolePlayer ? pData.CharacterName : pData.CharacterName.Truncate(14), caller is ConsolePlayer ? pData.SteamName : pData.SteamName.Truncate(14), pData.SteamID, pData.IP, pData.IsLocal(), pData.LastLoginLocal, pData.LastServerID, pData.LastServerName));
                    }
                }
                else
                {
                    UnturnedChat.Say(caller, "No players found by that name.");
                    return;
                }
            }
        }
    }
}
