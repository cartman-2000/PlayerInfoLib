using MySql.Data.MySqlClient;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PlayerInfoLibrary
{
    public static class Extensions
    {
        public static DateTime FromTimeStamp(this long timestamp)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timestamp).ToLocalTime();
        }

        public static long ToTimeStamp(this DateTime datetime)
        {
            return (long)(datetime.ToUniversalTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
        }

        public static bool IsDBNull (this MySqlDataReader reader, string fieldname)
        {
            return reader.IsDBNull(reader.GetOrdinal(fieldname));
        }

        public static string GetIP(this CSteamID cSteamID)
        {
            // Grab an active players ip address from CSteamID.
            P2PSessionState_t sessionState;
            SteamGameServerNetworking.GetP2PSessionState(cSteamID, out sessionState);
            return Parser.getIPFromUInt32(sessionState.m_nRemoteIP);
        }

        // Returns a Steamworks.CSteamID on out from a string, and returns true if it is a CSteamID.
        public static bool isCSteamID(this string sCSteamID, out CSteamID cSteamID)
        {
            ulong ulCSteamID;
            cSteamID = (CSteamID)0;
            if (ulong.TryParse(sCSteamID, out ulCSteamID))
            {
                if ((ulCSteamID >= 0x0110000100000000 && ulCSteamID <= 0x0170000000000000) || ulCSteamID == 0)
                {
                    cSteamID = (CSteamID)ulCSteamID;
                    return true;
                }
            }
            return false;
        }

        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
