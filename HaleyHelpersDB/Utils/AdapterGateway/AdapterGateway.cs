using Haley.Abstractions;
using Haley.Enums;
using Haley.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;

namespace Haley.Utils {

    public delegate void DictionaryUpdatedEvent();

    //DB ADAPTER SERVICE
    public partial class AdapterGateway : ConcurrentDictionary<string, IDBAdapter>, IAdapterGateway {

        const string DBASTRING = "AdapterStrings";
        const string CONFIG_DEFADAPTER = "defadapter";
        public const string DBNAME_KEY = "database";
        const string DBTYPE_KEY = "dbtype";
        const string SEARCHPATH_KEY = "searchpath";

        IConfigurationRoot _cfgRoot;
        IGatewayUtil _util;

        ConcurrentDictionary<string, (string conStr, TargetDB dbtype)> connectionstrings = new ConcurrentDictionary<string, (string cstr, TargetDB dbtype)>();

        protected string _defaultAdapterKey = string.Empty;
        public bool IsDevelopment { get; protected set; }
        public Guid Id { get; } = Guid.NewGuid();
        public bool LogQueryInConsole { get; set; }
        public bool ThrowCRUDExceptions { get; set; }
        public event DictionaryUpdatedEvent Updated;
        protected virtual IAdapterGateway GetDBService() { return this; }

        public AdapterGateway(bool autoConfigure = true) {
            //Id = Guid.NewGuid();
            if (autoConfigure) Configure();
        }
    }
}