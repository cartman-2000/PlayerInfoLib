using MySql.Data.MySqlClient;
using Rocket.Core.Logging;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Timers;

namespace PlayerInfoLibrary
{
    public class DatabaseManager
    {
        private Dictionary<CSteamID, PlayerData> Cache = new Dictionary<CSteamID, PlayerData>();
        public bool Initialized { get; private set; }
        private MySqlConnection Connection = null;
        private Timer KeepAlive = null;
        private int MaxRetry = 5;
        private string Table;
        private string TableConfig;
        private string TableInstance;
        private string TableServer;
        internal ushort InstanceID { get; private set; }
        public static readonly uint DatabaseSchemaVersion = 1;
        public static readonly uint DatabaseInterfaceVersion = 1;

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
            if(KeepAlive != null)
            {
                KeepAlive.Stop();
                KeepAlive.Dispose();
            }
            Connection.Dispose();
        }

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
                        " KEY `LastServerID` (`LastServerID`)" +
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
                    Logger.LogError("Error: Error getting instance id from database.");
                    return;
                }

                //set keel alive loop.
                if (KeepAlive == null)
                {
                    KeepAlive = new Timer(PlayerInfoLib.Instance.Configuration.Instance.KeepaliveInterval * 60000);
                    KeepAlive.Elapsed += delegate { CheckConnection(); };
                    KeepAlive.AutoReset = true;
                    KeepAlive.Start();
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
                command.Parameters.AddWithValue("@instname", Dedicator.InstanceName.ToLower());
                command.Parameters.AddWithValue("@servername", Provider.serverName);
                command.CommandText = "SELECT `ServerID`, `ServerName` FROM `" + TableInstance + "` WHERE `ServerInstance` = @instname;";
                getInstance = command.ExecuteReader();
                if (getInstance.Read())
                {
                    InstanceID = getInstance.GetUInt16("ServerID");
                    if (getInstance.GetString("ServerName") != Provider.serverName)
                    {
                        command.CommandText = "UPDATE `" + TableInstance + "` SET `ServerName` = 'test' WHERE `ServerID` = " + InstanceID + ";";
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
                    return GetInstanceID(true);
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
            }
            catch (MySqlException ex)
            {
                HandleException(ex, "Failed in updating Database schema to version " + updatingVersion + ", you may have to do a manual update to the database schema.");
            }
        }

        // Connection handling section.
        private void CheckConnection()
        {
            try
            {
                MySqlCommand command = Connection.CreateCommand();
                command.CommandText = "SELECT 1";
                command.ExecuteNonQuery();
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
                Connection = new MySqlConnection(string.Format("SERVER={0};DATABASE={1};UID={2};PASSWORD={3};PORT={4};", PlayerInfoLib.Instance.Configuration.Instance.DatabaseAddress, PlayerInfoLib.Instance.Configuration.Instance.DatabaseName, PlayerInfoLib.Instance.Configuration.Instance.DatabaseUserName, PlayerInfoLib.Instance.Configuration.Instance.DatabasePassword, PlayerInfoLib.Instance.Configuration.Instance.DatabasePort));
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
            Logger.LogException(ex); 

            if (ex.Number == 0)
            {
                Logger.LogWarning("Connection lost to database server, attempted to reconnect.");
                if (CreateConnection())
                {
                    return true;
                }
                Logger.LogWarning("Reconnect Failed.");
            }
            else
            {
                Logger.LogWarning(ex.Number.ToString() + "" + ((MySqlErrorCode)ex.Number).ToString());
                Logger.LogException(ex , msg != null ? msg : null);
            }
            return false;
        }

        // Query section.
        /// <summary>
        /// Queries the stored player info by Steam ID.
        /// </summary>
        /// <param name="steamId">String: steamid of the player you want to get player data for.</param>
        /// <param name="cached">Bool: Optional param for checking the cached info first before checking the database(faster checks for previously cached data.)</param>
        /// <returns>Returns a PlayerData type object if the player data was found, or null.</returns>
        public PlayerData QueryById(CSteamID steamId, bool cached = true)
        {
            PlayerData UnsetData = new PlayerData();
            PlayerData playerData = UnsetData;
            MySqlDataReader reader = null;
            if (Cache.ContainsKey(steamId) && cached == true)
            {
                playerData = Cache[steamId];
                if ((DateTime.Now - playerData.CacheTime).TotalSeconds <= (PlayerInfoLib.Instance.Configuration.Instance.CacheTime * 60))
                {
                    Logger.Log("Hit Cache");
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
                command.Parameters.AddWithValue("@steamid", steamId.ToString());
                command.Parameters.AddWithValue("@instanceid", InstanceID.ToString());
                command.CommandText = "SELECT * FROM (SELECT a.SteamID, a.SteamName, a.CharName, a.IP, a.LastLoginGlobal, a.LastServerID, b.ServerID, b.LastLoginLocal, b.CleanedBuildables, b.CleanedPlayerData, (SELECT `ServerName` FROM `"+TableInstance+"` WHERE `ServerID` = `a`.`LastServerID`) AS `LastServerName` FROM `"+Table+"` AS `a` LEFT JOIN `"+TableServer+"` AS `b` ON `a`.`SteamID` = `b`.`SteamID` WHERE (IF(b.ServerID = 1, b.ServerID = 1, b.ServerID = a.LastServerID OR b.SteamID IS NULL)) AND a.SteamID = @steamid ORDER BY b.LastLoginLocal ASC) AS c GROUP BY c.SteamID";
                reader = command.ExecuteReader();
                if (reader.Read())
                {
                    playerData = BuildPlayerData(reader);
                }
                Logger.Log("Hit Database");
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
        /// <returns></returns>
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
                if (page == 0)
                {
                    Logger.LogError("Error: Page number must be greater than 0.");
                    return playerList;
                }

                MySqlCommand command = Connection.CreateCommand();
                command.Parameters.AddWithValue("@name", "%" + playerName + "%");
                command.Parameters.AddWithValue("@limitstart", limitStart);
                command.Parameters.AddWithValue("@limit", limit);
                switch (queryType)
                {
                    case QueryType.Both:
                        if (pagination)
                            command.CommandText = "SELECT COUNT(*) AS count FROM (SELECT * FROM (SELECT a.SteamID FROM `" + Table + "` AS a LEFT JOIN `" + TableServer + "` AS b ON a.SteamID = b.SteamID WHERE (IF(b.ServerID = 1, b.ServerID = 1, b.ServerID = a.LastServerID OR b.SteamID IS NULL)) AND (a.SteamName LIKE @name OR a.CharName LIKE @name) ORDER BY b.LastLoginLocal ASC) AS c GROUP BY c.SteamID) AS d;";
                        command.CommandText += "SELECT * FROM (SELECT a.SteamID, a.SteamName, a.CharName, a.IP, a.LastLoginGlobal, a.LastServerID, b.ServerID, b.LastLoginLocal, b.CleanedBuildables, b.CleanedPlayerData, (SELECT ServerName FROM `" + TableInstance + "` WHERE ServerID = a.LastServerID) AS LastServerName FROM `" + Table + "` AS a LEFT JOIN `" + TableServer + "` AS b ON a.SteamID = b.SteamID WHERE (IF(b.ServerID = 1, b.ServerID = 1, b.ServerID = a.LastServerID OR b.SteamID IS NULL)) AND (a.SteamName LIKE @name OR a.CharName LIKE @name) ORDER BY b.LastLoginLocal ASC) AS c GROUP BY c.SteamID" + (pagination ? " LIMIT " + limitStart + ", " + limit + ";" : ";");
                        break;
                    case QueryType.CharName:
                        if (pagination)
                            command.CommandText = "SELECT COUNT(*) AS count FROM (SELECT * FROM (SELECT a.SteamID FROM `" + Table + "` AS `a` LEFT JOIN `" + TableServer + "` AS `b` ON `a`.`SteamID` = `b`.`SteamID` WHERE (IF(b.ServerID = 1, b.ServerID = 1, b.ServerID = a.LastServerID OR b.SteamID IS NULL)) AND a.CharName LIKE @name ORDER BY b.LastLoginLocal ASC) AS c GROUP BY c.SteamID) AS d;";
                        command.CommandText += "SELECT * FROM (SELECT a.SteamID, a.SteamName, a.CharName, a.IP, a.LastLoginGlobal, a.LastServerID, b.ServerID, b.LastLoginLocal, b.CleanedBuildables, b.CleanedPlayerData, (SELECT ServerName FROM `" + TableInstance + "` WHERE ServerID = a.LastServerID) AS LastServerName FROM `" + Table + "` AS a LEFT JOIN `" + TableServer + "` AS b ON a.SteamID = b.SteamID WHERE (IF(b.ServerID = 1, b.ServerID = 1, b.ServerID = a.LastServerID OR b.SteamID IS NULL)) AND a.CharName LIKE @name ORDER BY b.LastLoginLocal ASC) AS c GROUP BY c.SteamID" + (pagination ? " LIMIT " + limitStart + ", " + limit + ";" : ";");
                        break;
                    case QueryType.SteamName:
                        if (pagination)
                            command.CommandText = "SELECT COUNT(*) AS count FROM (SELECT * FROM (SELECT a.SteamID FROM `" + Table + "` AS `a` LEFT JOIN `" + TableServer + "` AS `b` ON `a`.`SteamID` = `b`.`SteamID` WHERE (IF(b.ServerID = 1, b.ServerID = 1, b.ServerID = a.LastServerID OR b.SteamID IS NULL)) AND a.SteamName LIKE @name ORDER BY b.LastLoginLocal ASC) AS c GROUP BY c.SteamID) AS d;";
                        command.CommandText += "SELECT * FROM (SELECT a.SteamID, a.SteamName, a.CharName, a.IP, a.LastLoginGlobal, a.LastServerID, b.ServerID, b.LastLoginLocal, b.CleanedBuildables, b.CleanedPlayerData, (SELECT ServerName FROM `" + TableInstance + "` WHERE ServerID = a.LastServerID) AS LastServerName FROM `" + Table + "` AS a LEFT JOIN `" + TableServer + "` AS b ON a.SteamID = b.SteamID WHERE (IF(b.ServerID = 1, b.ServerID = 1, b.ServerID = a.LastServerID OR b.SteamID IS NULL)) AND a.SteamName LIKE @name ORDER BY b.LastLoginLocal ASC) AS c GROUP BY c.SteamID" + (pagination ? " LIMIT " + limitStart + ", " + limit + ";" : ";");
                        break;
                }

                reader = command.ExecuteReader();
                if (pagination)
                {
                    if (reader.Read())
                        totalRecods = reader.GetUInt32("count");
                    if (!reader.NextResult())
                    {
                        Logger.Log("hit 1");
                        return playerList;
                    }
                }
                if (!reader.HasRows)
                {
                    Logger.Log("hit 2");
                    return playerList;
                }
                while (reader.Read())
                {
                    Logger.Log("hit loop");
                    playerList.Add(BuildPlayerData(reader));
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
            return playerList;
        }

        private PlayerData BuildPlayerData(MySqlDataReader reader)
        {
            return new PlayerData((CSteamID)reader.GetUInt64("SteamID"), reader.GetString("SteamName"), reader.GetString("CharName"), reader.GetString("IP"), reader.GetInt64("LastLoginGlobal").FromTimeStamp(), reader.GetUInt16("LastServerID"), !reader.IsDBNull("LastServerName") ? reader.GetString("LastServerName") : string.Empty, !reader.IsDBNull("ServerID") ? reader.GetUInt16("ServerID") : (ushort)0, !reader.IsDBNull("LastLoginLocal") ? reader.GetInt64("LastLoginLocal").FromTimeStamp() : (0L).FromTimeStamp(), !reader.IsDBNull("CleanedBuildables") ? reader.GetBoolean("CleanedBuildables") : false, !reader.IsDBNull("CleanedPlayerData") ? reader.GetBoolean("CleanedPlayerData") : false);
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
                if (pdata.SteamID == CSteamID.Nil)
                {
                    Logger.LogError("Error: Invalid player data information.");
                    return;
                }
                MySqlCommand command = Connection.CreateCommand();
                command.Parameters.AddWithValue("@steamid", pdata.SteamID);
                command.Parameters.AddWithValue("@steamname", pdata.SteamName.Truncate(200));
                command.Parameters.AddWithValue("@charname", pdata.CharacterName.Truncate(200));
                command.Parameters.AddWithValue("@ip", pdata.IP);
                command.Parameters.AddWithValue("@instanceid", pdata.ServerID);
                command.Parameters.AddWithValue("@lastinstanceid", pdata.LastServerID);
                command.Parameters.AddWithValue("@lastloginglobal", pdata.LastLoginGlobal.ToTimeStamp());
                command.Parameters.AddWithValue("@lastloginlocal", pdata.LastLoginLocal.ToTimeStamp());
                command.Parameters.AddWithValue("@cleanedbuildables", pdata.CleanedBuildables);
                command.Parameters.AddWithValue("@cleanedplayerdata", pdata.CleanedPlayerData);
                command.CommandText = "INSERT INTO `" + Table + "` (`SteamID`, `SteamName`, `CharName`, `IP`, `LastLoginGlobal`, `LastServerID`) VALUES (@steamid, @steamname, @charname, @ip, @lastloginglobal, @lastinstanceid) ON DUPLICATE KEY UPDATE `SteamName` = VALUES(`SteamName`), `CharName` = VALUES(`CharName`), `IP` = VALUES(`IP`), `LastLoginGlobal` = VALUES(`LastLoginglobal`), `LastServerID` = VALUES(`LastServerID`);" +
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
    }
}