using Rocket.API;
using Rocket.Unturned.Chat;
using System.Collections.Generic;

namespace PlayerInfoLibrary
{
    public class CommandDelInstance : IRocketCommand
    {
        internal static readonly string syntax = "<\"Instance ID\">";
        internal static readonly string help = "Uses the numerical Instance ID for a server to remove all player data saved for that server. !!Use with caution, can't be undone without a database backup!!";
        public List<string> Aliases
        {
            get { return new List<string>(); }
        }

        public AllowedCaller AllowedCaller
        {
            get { return AllowedCaller.Console; }
        }

        public string Help
        {
            get { return help; }
        }

        public string Name
        {
            get { return "delint"; }
        }

        public List<string> Permissions
        {
            get { return new List<string> { "PlayerInfoLib.delint" }; }
        }

        public string Syntax
        {
            get { return syntax; }
        }

        public void Execute(IRocketPlayer caller, string[] command)
        {
            if (command.Length == 0)
            {
                UnturnedChat.Say(caller, PlayerInfoLib.Instance.Translate("delint_help"));
            }
            if (command.Length > 1)
            {
                UnturnedChat.Say(caller, PlayerInfoLib.Instance.Translate("too_many_parameters"));
                return;
            }
            if (command.Length == 1)
            {
                ushort ID;
                if (!ushort.TryParse(command[0], out ID))
                {
                    UnturnedChat.Say(caller, PlayerInfoLib.Instance.Translate("delint_invalid"));
                    return;
                }
                if (PlayerInfoLib.Database.RemoveInstance(ID))
                {
                    UnturnedChat.Say(caller, PlayerInfoLib.Instance.Translate("delint_success"));
                    return;
                }
                else
                {
                    UnturnedChat.Say(caller, PlayerInfoLib.Instance.Translate("delint_not_found"));
                }
            }
        }
    }
}
