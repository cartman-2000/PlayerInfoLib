using Rocket.API;
using Rocket.API.Extensions;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Commands;
using SDG.Unturned;
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
            uint start = 0;
            uint perPage = caller is ConsolePlayer ? 10u : 4u;
            List<PlayerData> pInfo = new List<PlayerData>();
            if (command.Length >= 1)
            {
                if (command.Length ==2)
                {
                    page = command.GetUInt32Parameter(1);
                    if (page == null || page == 0)
                    {
                        UnturnedChat.Say(caller, "Invalid page number");
                        return;
                    }
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
                else if(Parser.checkIP(command[0]))
                {
                        pInfo = PlayerInfoLib.Database.QueryByName(command[0], QueryType.IP, out totalRecods, true, (uint)page, perPage);
                }
                else
                {
                        pInfo = PlayerInfoLib.Database.QueryByName(command[0], QueryType.Both, out totalRecods, true, (uint)page, perPage);
                }
                if (pInfo.Count > 0)
                {
                    start = ((uint)page - 1) * perPage;
                    UnturnedChat.Say(caller, string.Format("{0} Records found for: {1}, Page: {2} of {3}", totalRecods, command[0], page, (totalRecods / perPage + 1)));
                    foreach (PlayerData pData in pInfo)
                    {
                        start++;
                        if (pData.IsLocal())
                        {
                            UnturnedChat.Say(caller, string.Format("{0}: {1} [{2}] ({3}), IP: {4}", start, caller is ConsolePlayer ? pData.CharacterName : pData.CharacterName.Truncate(14), caller is ConsolePlayer ? pData.SteamName : pData.SteamName.Truncate(14), pData.SteamID, pData.IP));
                            UnturnedChat.Say(caller, string.Format("Local: {0}, Seen: {1}, Cleaned:{2}:{3}", pData.IsLocal(), pData.LastLoginLocal, pData.CleanedBuildables, pData.CleanedPlayerData));
                        }
                        else
                        {
                            UnturnedChat.Say(caller, string.Format("{0}: {1} [{2}] ({3}), IP: {4}", start, caller is ConsolePlayer ? pData.CharacterName : pData.CharacterName.Truncate(14), caller is ConsolePlayer ? pData.SteamName : pData.SteamName.Truncate(14), pData.SteamID, pData.IP));
                            UnturnedChat.Say(caller, string.Format("Local: {0}, Seen: {1} on: {2}:{3}", pData.IsLocal(), pData.LastLoginLocal, pData.LastServerID, pData.LastServerName));
                        }
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
