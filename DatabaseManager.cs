using MySql.Data.MySqlClient;
using Rocket.Core.Logging;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayerInfoLibrary
{
    public class DatabaseManager
    {
        private Dictionary<CSteamID, PlayerData> Cache = new Dictionary<CSteamID, PlayerData>();
        public bool Initialized { get; private set; }
        private MySqlConnection Connection = null;
        private int MaxRetry = 5;
        private string Table;
        private string TableConfig;
        private string TableInstance;
        private string TableServer;
        internal ushort InstanceID { get; private set; }
        public static readonly uint DatabaseSchemaVersion = 4;
        public static readonly uint DatabaseInterfaceVersion = 2;

        // Initialization section.
        internal DatabaseManager()
        {
            new I18N.West.CP1250();
            Initialized = false;
            Table = PlayerInfoLib.Instance.Configuration.Instance.DatabaseTableName;
            TableConfig = Table + "_config";
            TableInstance = Table + "_instance";
            TableServer = Table + "_server";
            CheckSchema();
        }

        internal void Unload()
        {
            Connection.Dispose();
        }

        // Plugin/Database setup section.
        private void CheckSchema()
        {
            try
            {
                if (!CreateConnection())
                    return;
                ushort version = 0;
                MySqlCommand command = Connection.CreateCommand();
                command.CommandText = "show tables like '" + TableConfig + "';";
                object test = command.ExecuteScalar();

                if (test == null)
                {
                    command.CommandText = "CREATE TABLE `"+TableConfig+"` (" +
                        " `key` varchar(40) COLLATE utf8_unicode_ci NOT NULL," +
                        " `value` varchar(40) COLLATE utf8_unicode_ci NOT NULL," +
                        " PRIMARY KEY(`key`)" +
                        ") ENGINE = MyISAM DEFAULT CHARSET = utf8 COLLATE = utf8_unicode_ci;";
                    command.CommandText += "CREATE TABLE `"+Table+"` (" +
                        " `SteamID` bigint(24) unsigned NOT NULL," +
                        " `SteamName` varchar(255) COLLATE utf8_unicode_ci NOT NULL," +
                        " `CharName` varchar(255) COLLATE utf8_unicode_ci NOT NULL," +
                        " `IP` varchar(16) COLLATE utf8_unicode_ci NOT NULL," +
                        " `LastLoginGlobal` bigint(32) NOT NULL," +
                        " `LastServerID` smallint(5) unsigned NOT NULL," +
                        " PRIMARY KEY (`SteamID`)," +
                        " KEY `LastServerID` (`LastServerID`)," +
                        " KEY `IP` (`IP`)" +
                        ") ENGINE = MyISAM DEFAULT CHARSET = utf8 COLLATE = utf8_unicode_ci; ";
                    command.CommandText += "CREATE TABLE `"+TableInstance+"` (" +
                        " `ServerID` smallint(5) unsigned NOT NULL AUTO_INCREMENT," +
                        " `ServerInstance` varchar(128) COLLATE utf8_unicode_ci NOT NULL," +
                        " `ServerName` varchar(60) COLLATE utf8_unicode_ci NOT NULL," +
                        " PRIMARY KEY(`ServerID`)," +
                        " UNIQUE KEY `ServerInstance` (`ServerInstance`)" +
                        ") ENGINE = MyISAM DEFAULT CHARSET = utf8 COLLATE = utf8_unicode_ci; ";
                    command.CommandText += "CREATE TABLE `"+TableServer+"` (" +
                        " `SteamID` bigint(24) unsigned NOT NULL," +
                        " `ServerID` smallint(5) unsigned NOT NULL," +
                        " `LastLoginLocal` bigint(32) NOT NULL," +
                        " `CleanedBuildables` BOOLEAN NOT NULL," +
                        " `CleanedPlayerData` BOOLEAN NOT NULL," +
                        " PRIMARY KEY(`SteamID`,`ServerID`)," +
                        " KEY `CleanedBuildables` (`CleanedBuildables`)," +
                        " KEY `CleanedPlayerData` (`CleanedPlayerData`)" +
                        ") ENGINE = MyISAM DEFAULT CHARSET = utf8 COLLATE = utf8_unicode_ci; ";
                    command.ExecuteNonQuery();
                    CheckVersion(version, command);
                    
                }
                else
                {
                    command.CommandText = "SELECT `value` FROM `" + TableConfig + "` WHERE `key` = 'version'";
                    object result = command.ExecuteScalar();
                    if (result != null)
                    {
                        if (ushort.TryParse(result.ToString(), out version))
                        {
                            if (version < DatabaseSchemaVersion)
                                CheckVersion(version, command);
                        }
                        else
                        {
                            Logger.LogError("Error: Database version number not found.");
                            return;
                        }
                    }
                    else
                    {
                        Logger.LogError("Error: Database version number not found.");
                        return;
                    }
                }
                if (!GetInstanceID())
                {
                    // Retry getting the instance id, if the server instance was just added to the database.
                    if (!GetInstanceID(true))
                    {
                        Logger.LogError("Error: Error getting instance id from database.");
                        return;
                    }
                }
                Initialized = true;
            }
            catch (MySqlException ex)
            {
                Logger.LogException(ex);
            }
        }

        private bool GetInstanceID(bool retrying = false)
        {
            //Load server instance id.
            MySqlDataReader getInstance = null;
            try {
                MySqlCommand command = Connection.CreateCommand();
                command.Parameters.AddWithValue("@instname", Provider.serverID.ToLower());
                command.Parameters.AddWithValue("@servername", Provider.serverName);
                command.CommandText = "SELECT `ServerID`, `ServerName` FROM `" + TableInstance + "` WHERE `ServerInstance` = @instname;";
                getInstance = command.ExecuteReader();
                if (getInstance.Read())
                {
                    InstanceID = getInstance.GetUInt16("ServerID");
                    if (InstanceID == 0)
                        return false;
                    if (getInstance.GetString("ServerName") != Provider.serverName)
                    {
                        getInstance.Close();
                        getInstance.Dispose();
                        command.CommandText = "UPDATE `" + TableInstance + "` SET `ServerName` = @servername WHERE `ServerID` = " + InstanceID + ";";
                        command.ExecuteNonQuery();
                    }
                    return true;
                }
                // Instance record wasn't found, add one to the database.
                else if (!retrying)
                {
                    getInstance.Close();
                    getInstance.Dispose();
                    command.CommandText = "INSERT INTO `" + TableInstance + "` (`ServerInstance`, `ServerName`) VALUES (@instname, @servername);";
                    command.ExecuteNonQuery();
                }
                return false;
            }
            catch (MySqlException ex)
            {
                HandleException(ex);
            }
            finally
            {
                if (getInstance != null)
                {
                    getInstance.Close();
                    getInstance.Dispose();
                }
            }
            return false;
        }

        internal bool SetInstanceName(string newName)
        {
            try
            {
                if (Initialized)
                {
                    MySqlCommand command = Connection.CreateCommand();
                    command.Parameters.AddWithValue("@newname", newName);
                    command.Parameters.AddWithValue("@instance", InstanceID);
                    command.CommandText = "UPDATE `" + TableInstance + "` SET `ServerInstance` = @newname WHERE `ServerID` = @instance;";
                    command.ExecuteNonQuery();
                    return true;
                }
            }
            catch (MySqlException ex)
            {
                HandleException(ex);
            }
            return false;
        }

        private void CheckVersion(ushort version, MySqlCommand command)
        {
            ushort updatingVersion = 0;
            try
            {
                if (version < 1)
                {
                    updatingVersion = 1;
                    command.CommandText = "INSERT INTO `" + TableConfig + "` (`key`, `value`) VALUES ('version', '1');";
                    command.ExecuteNonQuery();
                }
                if (version < 2)
                {
                    updatingVersion = 2;
                    Logger.LogWarning("Updating Playerinfo DB to version: " + updatingVersion);
                    command.CommandText = "ALTER TABLE `" + Table + "` DROP INDEX IP;" +
                        "ALTER TABLE `" + Table + "` CHANGE `IP` `IP_old` VARCHAR(16) CHARACTER SET utf8 COLLATE utf8_unicode_ci NOT NULL;" +
                        "ALTER TABLE `" + Table + "` ADD `IP` INT(10) UNSIGNED NOT NULL AFTER `CharName`;";
                    command.ExecuteNonQuery();
                    Dictionary<CSteamID, uint> New = new Dictionary<CSteamID, uint>();
                    command.CommandText = "SELECT SteamID, IP_old FROM `" + Table + "`";
                    MySqlDataReader result = command.ExecuteReader();
                    if (result.HasRows)
                    {
                        while (result.Read())
                        {
                            if (!result.IsDBNull("IP_old"))
                            {
                                if (Parser.checkIP(result.GetString("IP_old")))
                                {
                                    New.Add((CSteamID)result.GetUInt64("SteamID"), Parser.getUInt32FromIP(result.GetString("IP_old")));
                                }
                            }
                        }
                    }
                    result.Close();
                    result.Dispose();
                    if (New.Count != 0)
                    {
                        foreach (KeyValuePair<CSteamID, uint> record in New)
                        {
                            command.CommandText = "UPDATE `" + Table + "` SET `IP` = " + record.Value + " WHERE `SteamID` = " + record.Key + ";";
                            command.ExecuteNonQuery();
                        }
                    }
                    command.CommandText = "ALTER TABLE `" + Table + "` ADD INDEX(`IP`);" +
                        "ALTER TABLE `" + Table + "` DROP `IP_old`;" +
                        "UPDATE `" + TableConfig + "` SET `value` = '2' WHERE `key` = 'version';";
                    command.ExecuteNonQuery();
                }
                if (version < 3)
                {
                    updatingVersion = 3;
                    Logger.LogWarning("Updating Playerinfo DB to version: " + updatingVersion);
                    command.CommandText = "ALTER TABLE `" + Table + "` ADD `TotalPlayTime` INT NOT NULL AFTER `LastLoginGlobal`;" +
                        "UPDATE `" + TableConfig + "` SET `value` = '3' WHERE `key` = 'version';";
                    command.ExecuteNonQuery();
                    Logger.LogWarning("Finished.");
                }
                if (version < 4)
                {
                    updatingVersion = 4;
                    // Updating tables to handle Special UTF8 characters(like emoji characters.)
                    Logger.LogWarning("Updating Playerinfo DB to version: " + updatingVersion);
                    command.CommandText = "ALTER TABLE `" + Table + "` MODIFY `SteamName` VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL, MODIFY `CharName` VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL;" +
                        "ALTER TABLE `"+ TableInstance + "` MODIFY `ServerInstance` VARCHAR(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL, MODIFY `ServerName` VARCHAR(60) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL;" +
                        "REPAIR TABLE `" + Table + "`, `" + TableInstance + "`;" +
                        "UPDATE `" + TableConfig + "` SET `value` = '4' WHERE `key` = 'version';";
                    command.ExecuteNonQuery();
                }
            }
            catch (MySqlException ex)
            {
                HandleException(ex, "Failed in updating Database schema to version " + updatingVersion + ", you may have to do a manual update to the database schema.");
            }
        }

        // Connection handling section.
        internal void CheckConnection()
        {
            try
            {
                if (Initialized)
                {
                    MySqlCommand command = Connection.CreateCommand();
                    command.CommandText = "SELECT 1";
                    command.ExecuteNonQuery();
                }
            }
            catch (MySqlException ex)
            {
                HandleException(ex);
            }
        }

        private bool CreateConnection(int count = 1)
        {
            try
            {
                Connection = null;
                if (PlayerInfoLib.Instance.Configuration.Instance.DatabasePort == 0)
                    PlayerInfoLib.Instance.Configuration.Instance.DatabasePort = 3306;
                Connection = new MySqlConnection(string.Format("SERVER={0};DATABASE={1};UID={2};PASSWORD={3};PORT={4};CHARSET=utf8mb4", PlayerInfoLib.Instance.Configuration.Instance.DatabaseAddress, PlayerInfoLib.Instance.Configuration.Instance.DatabaseName, PlayerInfoLib.Instance.Configuration.Instance.DatabaseUserName, PlayerInfoLib.Instance.Configuration.Instance.DatabasePassword, PlayerInfoLib.Instance.Configuration.Instance.DatabasePort));
                Connection.Open();
                return true;
            }
            catch(MySqlException ex)
            {
                if (count < MaxRetry)
                {
                    return CreateConnection(count + 1);
                }
                Logger.LogException(ex, "Failed to connect to the database server!");
                return false;
            }
        }

        private bool HandleException(MySqlException ex, string msg = null)
        {
            if (ex.Number == 0)
            {
                Logger.LogException(ex, "Error: Connection lost to database server, attempting to reconnect.");
                if (CreateConnection())
                {
                    Logger.Log("Success.");
                    return true;
                }
                Logger.LogError("Reconnect Failed.");
            }
            else
            {
                Logger.LogWarning(ex.Number.ToString() + ":" + ((MySqlErrorCode)ex.Number).ToString());
                Logger.LogException(ex , msg != null ? msg : null);
            }
            return false;
        }

        // Query section.
        /// <summary>
        /// Queries the stored player info by Steam ID.
        /// </summary>
        /// <param name="steamId">String: SteamID of the player you want to get player data for.</param>
        /// <param name="cached">Bool: Optional param for checking the cached info first before checking the database(faster checks for previously cached data.)</param>
        /// <returns>Returns a PlayerData type object if the player data was found, or an empty PlayerData object if the player wasn't found.</returns>
        public PlayerData QueryById(CSteamID steamId, bool cached = true)
        {
            PlayerData UnsetData = new PlayerData();
            PlayerData playerData = UnsetData;
            MySqlDataReader reader = null;
            if (Cache.ContainsKey(steamId) && cached == true)
            {
                playerData = Cache[steamId];
                if (!playerData.IsCacheExpired())
                {
                    return playerData;
                }
                else
                    playerData = UnsetData;
            }
            try
            {
                if (!Initialized)
                {
                    Logger.LogError("Error: Cant load player info from DB, plugin hasn't initialized properly.");
                    return UnsetData;
                }
                MySqlCommand command = Connection.CreateCommand();
                command.Parameters.AddWithValue("@steamid", steamId);
                command.Parameters.AddWithValue("@instance", InstanceID);
                command.CommandText = "SELECT * FROM (SELECT a.SteamID, a.SteamName, a.CharName, a.IP, a.LastLoginGlobal, a.TotalPlayTime, a.LastServerID, b.ServerID, b.LastLoginLocal, b.CleanedBuildables, b.CleanedPlayerData, c.ServerName AS LastServerName FROM `" + Table + "` AS a LEFT JOIN `" + TableServer + "` AS b ON a.SteamID = b.SteamID LEFT JOIN `" + TableInstance + "` AS c ON a.LastServerID = c.ServerID WHERE (b.ServerID = @instance OR b.ServerID = a.LastServerID OR b.ServerID IS NULL) AND a.SteamID = @steamid ORDER BY b.LastLoginLocal ASC) AS g GROUP BY g.SteamID";
                reader = command.ExecuteReader();
                if (reader.Read())
                {
                    playerData = BuildPlayerData(reader);
                }
                if (Cache.ContainsKey(steamId))
                    Cache.Remove(steamId);
                playerData.CacheTime = DateTime.Now;
                Cache.Add(steamId, playerData);
            }
            catch (MySqlException ex)
            {
                HandleException(ex);
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                    reader.Dispose();
                }
            }
            return playerData;
        }

        /// <summary>
        /// Queries the database by a name.
        /// </summary>
        /// <param name="playerName">Player name to search for in the database.</param>
        /// <param name="queryType">Sets what type of lookup it is: by steam name, by char name, or by both.</param>
        /// <param name="totalRecods">Returns the total number of records found in the database for the search query.</param>
        /// <param name="pagination">Enables or disables pagination prior to the return.</param>
        /// <param name="page">For pagination, set the page to return.</param>
        /// <param name="limit">Limits the number of records to return.</param>
        /// <returns>A list of PlayerData typed data.</returns>
        public List<PlayerData> QueryByName(string playerName, QueryType queryType, out uint totalRecods, bool pagination = true, uint page = 1, uint limit = 4)
        {
            List<PlayerData> playerList = new List<PlayerData>();
            MySqlDataReader reader = null;
            totalRecods = 0;
            uint limitStart = (page - 1) * limit;
            try
            {
                if (!Initialized)
                {
                    Logger.LogError("Error: Cant load player info from DB, plugin hasn't initialized properly.");
                    return playerList;
                }
                if (page == 0 || limit == 0)
                {
                    Logger.LogError("Error: Invalid pagination values, these must be above 0.");
                    return playerList;
                }
                if (playerName.Trim() == string.Empty)
                {
                    Logger.LogWarning("Warning: Need at least one character in the player name.");
                    return playerList;
                }

                MySqlCommand command = Connection.CreateCommand();
                command.Parameters.AddWithValue("@name", "%" + playerName + "%");
                command.Parameters.AddWithValue("@instance", InstanceID);
                string type;
                switch (queryType)
                {
                    case QueryType.Both:
                        type = "AND (a.SteamName LIKE @name OR a.CharName LIKE @name)";
                        break;
                    case QueryType.CharName:
                        type = "AND a.CharName LIKE @name";
                        break;
                    case QueryType.SteamName:
                        type = "AND a.SteamName LIKE @name";
                        break;
                    case QueryType.IP:
                        type = "AND a.IP = " + Parser.getUInt32FromIP(playerName);
                        break;
                    default:
                        type = string.Empty;
                        break;
                }
                if (pagination)
                    command.CommandText = "SELECT COUNT(*) AS count FROM (SELECT * FROM (SELECT a.SteamID FROM `" + Table + "` AS a LEFT JOIN `" + TableServer + "` AS b ON a.SteamID = b.SteamID WHERE (b.ServerID = @instance OR b.ServerID = a.LastServerID OR b.ServerID IS NULL) " + type + " ORDER BY b.LastLoginLocal ASC) AS g GROUP BY g.SteamID) AS c;";
                command.CommandText += "SELECT * FROM (SELECT a.SteamID, a.SteamName, a.CharName, a.IP, a.LastLoginGlobal, a.TotalPlayTime, a.LastServerID, b.ServerID, b.LastLoginLocal, b.CleanedBuildables, b.CleanedPlayerData, c.ServerName AS LastServerName FROM `" + Table + "` AS a LEFT JOIN `" + TableServer + "` AS b ON a.SteamID = b.SteamID LEFT JOIN `" + TableInstance + "` AS c ON a.LastServerID = c.ServerID WHERE (b.ServerID = @instance OR b.ServerID = a.LastServerID OR b.ServerID IS NULL) " + type + " ORDER BY b.LastLoginLocal ASC) AS g GROUP BY g.SteamID ORDER BY g.LastLoginGlobal DESC" + (pagination ? " LIMIT " + limitStart + ", " + limit + ";" : ";");
                reader = command.ExecuteReader();
                if (pagination)
                {
                    if (reader.Read())
                        totalRecods = reader.GetUInt32("count");
                    if (!reader.NextResult())
                    {
                        return playerList;
                    }
                }
                if (!reader.HasRows)
                {
                    return playerList;
                }
                while (reader.Read())
                {
                    PlayerData record = BuildPlayerData(reader);
                    record.CacheTime = DateTime.Now;
                    playerList.Add(record);

                }
                if (!pagination)
                    totalRecods = (uint)playerList.Count;
            }
            catch (MySqlException ex)
            {
                HandleException(ex);
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                    reader.Dispose();
                }
            }
            return playerList;
        }

        public List<object[]> GetCleanupList(OptionType optionType, long beforeTime)
        {
            List<object[]> tmp = new List<object[]>();
            MySqlDataReader reader = null;
            if (!Initialized)
            {
                Logger.LogError("Error: Cant load player info from DB, plugin hasn't initialized properly.");
                return tmp;
            }
            string type = ParseOption(optionType);
            if (type == null)
                return tmp;
            try
            {
                MySqlCommand command = Connection.CreateCommand();
                command.Parameters.AddWithValue("@time", beforeTime);
                command.Parameters.AddWithValue("@instance", InstanceID);
                command.CommandText = "SELECT a.SteamID, b.CharName, b.SteamName  FROM `" + TableServer + "` AS a LEFT JOIN `" + Table + "` AS b ON a.SteamID = b.SteamID WHERE a.ServerID = @instance AND a.LastLoginLocal < @time AND a." + type + " = 0 AND b.SteamID IS NOT NULL ORDER BY a.LastLoginLocal  ASC;";
                reader = command.ExecuteReader();
                if (!reader.HasRows)
                {
                    return tmp;
                }
                while (reader.Read())
                {
                    tmp.Add(new object[]
                    {
                        reader.GetUInt64("SteamID"),
                        reader.GetString("CharName"),
                        reader.GetString("SteamName"),
                    });
                }
                return tmp;
            }
            catch (MySqlException ex)
            {
                HandleException(ex);
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                    reader.Dispose();
                }
            }
            return tmp;
        }

        public void SetOption(CSteamID SteamID, OptionType optionType, bool setValue)
        {
            if (!Initialized)
            {
                Logger.LogError("Error: Cant load player info from DB, plugin hasn't initialized properly.");
                return;
            }
            string type = ParseOption(optionType);
            if (type == null)
                return;
            try
            {
                MySqlCommand command = Connection.CreateCommand();
                command.Parameters.AddWithValue("@steamid", SteamID);
                command.Parameters.AddWithValue("@instance", InstanceID);
                command.Parameters.AddWithValue("@setvalue", setValue);
                command.CommandText = "UPDATE `" + TableServer + "` SET " + type + " = @setvalue WHERE SteamID = @steamid AND ServerID = @instance;";
                command.ExecuteNonQuery();
            }
            catch (MySqlException ex)
            {
                HandleException(ex);
            }
        }

        private string ParseOption(OptionType optionType)
        {
            string type = null;
            switch (optionType)
            {
                case OptionType.Buildables:
                    type = "CleanedBuildables";
                    break;
                case OptionType.PlayerFiles:
                    type = "CleanedPlayerData";
                    break;
                default:
                    return type;
            }
            return type;
        }

        private PlayerData BuildPlayerData(MySqlDataReader reader)
        {
            return new PlayerData((CSteamID)reader.GetUInt64("SteamID"), reader.GetString("SteamName"), reader.GetString("CharName"), Parser.getIPFromUInt32(reader.GetUInt32("IP")), reader.GetInt64("LastLoginGlobal").FromTimeStamp(), reader.GetUInt16("LastServerID"), !reader.IsDBNull("LastServerName") ? reader.GetString("LastServerName") : string.Empty, !reader.IsDBNull("ServerID") ? reader.GetUInt16("ServerID") : (ushort)0, !reader.IsDBNull("LastLoginLocal") ? reader.GetInt64("LastLoginLocal").FromTimeStamp() : (0L).FromTimeStamp(), !reader.IsDBNull("CleanedBuildables") ? reader.GetBoolean("CleanedBuildables") : false, !reader.IsDBNull("CleanedPlayerData") ? reader.GetBoolean("CleanedPlayerData") : false, reader.GetInt32("TotalPlayTime"));
        }

        // Cleanup section.
        internal void CheckExpired()
        {
            List<KeyValuePair<CSteamID, PlayerData>> tmp = Cache.Where(pd => pd.Value.IsCacheExpired()).ToList<KeyValuePair<CSteamID, PlayerData>>();
            foreach (KeyValuePair<CSteamID, PlayerData> pdata in tmp)
            {
                Cache.Remove(pdata.Key);
            }
        }

        internal bool RemoveInstance(ushort InstanceId)
        {
            if (!Initialized)
            {
                return false;
            }
            else
            {
                MySqlDataReader reader = null;
                Dictionary<ulong, object[]> records = new Dictionary<ulong, object[]>();
                try
                {
                    MySqlCommand command = Connection.CreateCommand();
                    command.Parameters.AddWithValue("@forinstance", InstanceId);
                    command.CommandText = "SELECT ServerID FROM `" + TableInstance + "` WHERE ServerID = @forinstance;";
                    object result = command.ExecuteScalar();
                    if (result == null)
                    {
                        return false;
                    }
                    command.CommandText = "SELECT SteamID FROM `" + TableServer + "` WHERE ServerID = @forinstance";
                    reader = command.ExecuteReader();
                    if(reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            records.Add(reader.GetUInt64("SteamID"), new object[] {});
                        }
                        reader.Close();
                        reader.Dispose();
                        ProcessRecordRemoval(command, records, InstanceId);
                        command.CommandText = "DELETE FROM `" + TableInstance + "` WHERE ServerID = " + InstanceId + ";";
                        command.ExecuteNonQuery();
                        if (InstanceId == InstanceID)
                            Initialized = false;
                        return true;
                    }
                }
                catch (MySqlException ex)
                {
                    HandleException(ex);
                    return false;
                }
                finally
                {
                    if (reader != null)
                    {
                        reader.Close();
                        reader.Dispose();
                    }
                }
            }
            return true;
        }

        internal void PrecessExpiredPInfo()
        {
            if (Initialized)
            {
                MySqlDataReader reader = null;
                Dictionary<ulong, object[]> records = new Dictionary<ulong, object[]>();
                try
                {
                    MySqlCommand command = Connection.CreateCommand();
                    command.Parameters.AddWithValue("@forinstance", InstanceID);
                    command.Parameters.AddWithValue("@calcedcutoff", (DateTime.Now.ToTimeStamp() - PlayerInfoLib.Instance.Configuration.Instance.ExpiresAfter * 86400));
                    command.CommandText = "SELECT a.SteamID, b.CharName, b.SteamName FROM `" + TableServer + "` AS a LEFT JOIN `" + Table + "` AS b ON a.SteamID = b.SteamID WHERE ServerID = @forinstance AND LastLoginLocal < @calcedcutoff;";
                    reader = command.ExecuteReader();
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            records.Add(reader.GetUInt64("SteamID"), new object[] { reader.GetString("CharName"), reader.GetString("SteamName") });
                        }
                        reader.Close();
                        reader.Dispose();
                        ProcessRecordRemoval(command, records, InstanceID, false);
                    }
                    else
                    {
                        Logger.Log("No expired player info records found for this batch.", ConsoleColor.Yellow);
                    }
                }
                catch (MySqlException ex)
                {
                    HandleException(ex);
                }
                finally
                {
                    if (reader != null)
                    {
                        reader.Close();
                        reader.Dispose();
                    }
                }
            }
        }

        private void ProcessRecordRemoval(MySqlCommand command, Dictionary<ulong, object[]> records, ushort InstanceId, bool InstanceRemoval = true)
        {
            try
            {
                int totalRemoved = 0;
                int totalRemovedServer = 0;
                int recordNum = 0;
                if (InstanceRemoval)
                    Logger.Log(string.Format("Starting player info removal process For the entered Instance ID, number of records to process: {0}.", records.Count), ConsoleColor.Yellow);
                else
                    Logger.Log(string.Format("Starting expired player info cleanup process, number of records to cleanup in this batch: {0}", records.Count), ConsoleColor.Yellow);

                foreach (KeyValuePair<ulong, object[]> val in records)
                {
                    int count = 0;
                    recordNum++;
                    if (recordNum % 1000 == 0)
                        Logger.Log(string.Format("Processing record: {0} of {1}", recordNum, records.Count));
                    command.CommandText = "SELECT COUNT(*) as count FROM `" + TableServer + "` WHERE SteamID = " + val.Key + ";";
                    object resultc = command.ExecuteScalar();
                    if(resultc != null && resultc != DBNull.Value)
                    {
                        if (int.TryParse(resultc.ToString(), out count))
                        {
                            if (!InstanceRemoval)
                                Logger.Log(string.Format("Removing Player info for: {0} [{1}] ({2})", val.Value[0].ToString(), val.Value[1].ToString(), val.Key));
                            if (count <= 1)
                            {
                                command.CommandText = "DELETE FROM `" + Table + "` WHERE SteamID = " + val.Key + ";";
                                command.ExecuteNonQuery();
                                totalRemoved++;
                            }
                            command.CommandText = "DELETE FROM `" + TableServer + "` WHERE SteamID = " + val.Key + " AND ServerID = " + InstanceId + ";";
                            command.ExecuteNonQuery();
                            totalRemovedServer++;
                        }
                    }

                }
                Logger.Log(string.Format("Finished player info cleanup. Number cleaned: {0}: {1}, {2}: {3}.", TableServer, totalRemovedServer, Table, totalRemoved), ConsoleColor.Yellow);
            }
            catch (MySqlException ex)
            {
                HandleException(ex);
            }
        }

        // Data Saving section.
        internal void SaveToDB(PlayerData pdata, bool retry = false)
        {
            try
            {
                if (!Initialized)
                {
                    Logger.LogError("Error: Cant save player info, plugin hasn't initialized properly.");
                    return;
                }
                if (!pdata.IsValid())
                {
                    Logger.LogError("Error: Invalid player data information.");
                    return;
                }
                MySqlCommand command = Connection.CreateCommand();
                command.Parameters.AddWithValue("@steamid", pdata.SteamID);
                command.Parameters.AddWithValue("@steamname", pdata.SteamName.Truncate(200));
                command.Parameters.AddWithValue("@charname", pdata.CharacterName.Truncate(200));
                command.Parameters.AddWithValue("@ip", Parser.getUInt32FromIP(pdata.IP));
                command.Parameters.AddWithValue("@instanceid", pdata.ServerID);
                command.Parameters.AddWithValue("@lastinstanceid", pdata.LastServerID);
                command.Parameters.AddWithValue("@lastloginglobal", pdata.LastLoginGlobal.ToTimeStamp());
                command.Parameters.AddWithValue("@totalplaytime", pdata.TotalPlayime);
                command.Parameters.AddWithValue("@lastloginlocal", pdata.LastLoginLocal.ToTimeStamp());
                command.Parameters.AddWithValue("@cleanedbuildables", pdata.CleanedBuildables);
                command.Parameters.AddWithValue("@cleanedplayerdata", pdata.CleanedPlayerData);
                command.CommandText = "INSERT INTO `" + Table + "` (`SteamID`, `SteamName`, `CharName`, `IP`, `LastLoginGlobal`, `TotalPlayTime`, `LastServerID`) VALUES (@steamid, @steamname, @charname, @ip, @lastloginglobal, @totalplaytime, @lastinstanceid) ON DUPLICATE KEY UPDATE `SteamName` = VALUES(`SteamName`), `CharName` = VALUES(`CharName`), `IP` = VALUES(`IP`), `LastLoginGlobal` = VALUES(`LastLoginglobal`), `TotalPlayTime` = VALUES(`TotalPlayTime`), `LastServerID` = VALUES(`LastServerID`);" +
                    "INSERT INTO `" + TableServer + "` (`SteamID`, `ServerID`, `LastLoginLocal`, `CleanedBuildables`, `CleanedPlayerData`) VALUES (@steamid, @instanceid, @lastloginlocal, @cleanedplayerdata, @cleanedplayerdata) ON DUPLICATE KEY UPDATE `LastLoginLocal` = VALUES(`LastLoginLocal`), `CleanedBuildables` = VALUES(`CleanedBuildables`), `CleanedPlayerData` = VALUES(`CleanedPlayerData`);";
                command.ExecuteNonQuery();
                if (Cache.ContainsKey(pdata.SteamID))
                    Cache.Remove(pdata.SteamID);
                pdata.CacheTime = DateTime.Now;
                Cache.Add(pdata.SteamID, pdata);
            }
            catch(MySqlException ex)
            {
                if (!retry)
                {
                    if (HandleException(ex))
                        SaveToDB(pdata, true);
                }
            }
        }
    }

    public enum QueryType
    {
        SteamName,
        CharName,
        Both,
        IP,
    }

    public enum OptionType
    {
        Buildables,
        PlayerFiles,
    }
}