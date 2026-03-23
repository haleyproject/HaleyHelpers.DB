using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;

namespace Haley.Utils {

    public partial class AdapterGateway  {
        public IAdapterGateway Configure() {
            return Configure(false);
        }

        IAdapterGateway Configure(bool reload) {
            ParseConnectionStrings(reload); //Load all latest connection string information into memory.
            if (connectionstrings == null) throw new ArgumentNullException(nameof(connectionstrings));
            //Supposed to read the json files and then generate all the adapters.
            try {
                var root = GetConfigurationRoot(reload);
                var dbastrings = root.GetSection(DBASTRING);
                if (dbastrings == null || dbastrings.GetChildren() == null || dbastrings.GetChildren().Count() == 0) return this; //Dont' process if we dont' have any children.
                List<AdapterConfig> adapters = new List<AdapterConfig>();
                foreach (var kvp in dbastrings.GetChildren()) {
                    try {
                        if (string.IsNullOrWhiteSpace(kvp.Value)) continue;
                        var aconfig = kvp.Value.ToDictionarySplit().Map<AdapterConfig>();
                        aconfig.DBAString = kvp.Value;
                        aconfig.AdapterKey = kvp.Key;
                        adapters.Add(aconfig);
                    } catch (Exception ex) {
                        Debug.WriteLine(ex.Message);
                    }
                }
                foreach (var entry in adapters) {

                    if (string.IsNullOrWhiteSpace(entry.AdapterKey) || string.IsNullOrWhiteSpace(entry.ConnectionKey)) continue;
                    //based upon the connection string key in the entry, fetch the corresponding Connection string and it's dbtype from the already parsed connection strings.
                    if (connectionstrings.TryGetValue(entry.ConnectionKey, out var connectionData)) {
                        entry.DBType = connectionData.dbtype;
                        //If user didn't specify dbname , then take it from the connectionstring itself.
                        var constr = connectionData.conStr;
                        if (!string.IsNullOrWhiteSpace(entry.DBName)) {
                            //replace this name in the connection string.
                            constr = constr.ReplaceValue(';', DBNAME_KEY, entry.DBName);
                        } else {
                            //Let us try to fetch the dbname from the input.
                            entry.DBName = Convert.ToString(constr.GetValue(DBNAME_KEY, ';'));
                        }
                        entry.ConnectionString = constr;

                        //For postgres, add schema as well
                        if (entry.DBType == TargetDB.pgsql && !string.IsNullOrWhiteSpace(entry.SchemaName)) {
                            entry.ConnectionString = entry.ConnectionString.ReplaceValue(';',SEARCHPATH_KEY, entry.SchemaName);
                        }

                        // In case dbtype is unknown then it should not register as we don't know which database handler to use.
                        if (entry.DBType == TargetDB.unknown) {
                            throw new ArgumentException($@"Missing: Value for DBTYPE which is needed to decide the type of database to connect to. Entry {entry.ConnectionKey} - {entry.AdapterKey}");
                        }

                        if (this.ContainsKey(entry.AdapterKey) && reload) {
                            //Now this will be an update.
                            this[entry.AdapterKey].UpdateDBEntry(entry);
                        } else {
                            Add(entry);
                        }
                    }
                }

                //After we finish loading everything, check if we have any default adapter or not.
                var defAdapter = ResourceUtils.FetchVariableWith(root, CONFIG_DEFADAPTER);
                if (!string.IsNullOrWhiteSpace(defAdapter?.Result?.ToString())) _defaultAdapterKey = defAdapter?.Result?.ToString();
            } catch (Exception) {
                throw;
            }
            return this;
        }

        void ParseConnectionStrings(bool reload = false) {
            var root = GetConfigurationRoot(reload);
            var allconnection = root.GetSection("ConnectionStrings");
            connectionstrings = new ConcurrentDictionary<string, (string cstr, TargetDB dbtype)>(); //reset.
            foreach (var item in allconnection.GetChildren()) {
                connectionstrings.TryAdd(item.Key, SplitConnectionString(item.Value));
            }
        }

        public IConfigurationRoot GetConfigurationRoot(bool reload = false, bool force_reload = false) {
            if (_cfgRoot == null || force_reload) {
                //Set default configuration root.
                SetConfigurationRoot(null, null);
            } else {
                if (reload) _cfgRoot.Reload();
            }
            return _cfgRoot;
        }

        public IAdapterGateway SetConfigurationRoot(string[] jsonPaths, string basePath = null) {
            SetConfigurationRoot(ResourceUtils.GenerateConfigurationRoot(jsonPaths, basePath));
            return this;
        }

        public IAdapterGateway SetConfigurationRoot(IConfigurationRoot cfgRoot) {
            if (cfgRoot == null) throw new ArgumentNullException(nameof(cfgRoot));
            _cfgRoot = cfgRoot;
            return this;
        }
    }
}