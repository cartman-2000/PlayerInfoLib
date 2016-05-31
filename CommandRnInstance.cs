using Rocket.API;
using Rocket.Unturned.Chat;
using System.Collections.Generic;

namespace PlayerInfoLibrary
{
    public class CommandRnInstance : IRocketCommand
    {
        internal static readonly string syntax = "<\"New instance name\">";
        internal static readonly string help = "Renames the instance name, in the record for this server, in the database";
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
            get { return "rnint"; }
        }

        public List<string> Permissions
        {
            get { return new List<string> { "PlayerInfoLib.rnint" }; }
        }

        public string Syntax
        {
            get { return syntax; }
        }

        public void Execute(IRocketPlayer caller, string[] command)
        {
            if (command.Length == 0)
            {
                UnturnedChat.Say(caller, PlayerInfoLib.Instance.Translate("rnint_help"));
            }
            if (command.Length > 1)
            {
                UnturnedChat.Say(caller, PlayerInfoLib.Instance.Translate("too_many_parameters"));
                return;
            }
            if (command.Length == 1)
            {
                string newName = command[0].ToLower();
                if (PlayerInfoLib.Database.SetInstanceName(newName))
                {
                    UnturnedChat.Say(caller, PlayerInfoLib.Instance.Translate("rnint_success"));
                    return;
                }
                else
                {
                    UnturnedChat.Say(caller, PlayerInfoLib.Instance.Translate("rnint_not_found"));
                }
            }
        }
    }
}
