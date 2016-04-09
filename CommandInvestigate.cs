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
    public class CommandInvestigate2 : IRocketCommand
    {
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
            get { return "Returns info for players matching the search quarry."; }
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
            get { return "<\"Player name\" | SteamID> [page]"; }
        }

        public void Execute(IRocketPlayer caller, string[] command)
        {
            if (command.Length == 0)
            {
                UnturnedChat.Say(caller, Syntax + " - " + Help);
            }
            bool isCSteamID;
            CSteamID cSteamID;
            uint totalRecods = 0;
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
                    if(pData != null)
                        pInfo.Add(pData);
                    isCSteamID = true;
                }
                else
                {
                    if (caller is ConsolePlayer)
                        pInfo = PlayerInfoLib.Database.QueryByName(command[0], QueryType.Both, out totalRecods, true, (uint)page, 10);
                    else
                        pInfo = PlayerInfoLib.Database.QueryByName(command[0], QueryType.Both, out totalRecods, true, (uint)page);
                    isCSteamID = false;
                }
                if (pInfo.Count > 0)
                {
                    foreach (PlayerData pData in pInfo)
                    {
                        UnturnedChat.Say(caller, string.Format("{0} : {1} : {2} : {3} : {4} : {5} : {6} : {7}", pData.SteamID, pData.SteamName, pData.CharacterName, pData.IP, pData.LastLoginGlobal, pData.LastServerID, pData.LastLoginLocal, pData.LastServerName));
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
