using Haley.Abstractions;
using Haley.Enums;
using Haley.Utils;
using System.Collections.Concurrent;
using System.Data;
using System.Globalization;

namespace Haley.Models
{
    //Each connecton util is expected to contain one connection string within it.
    public class DBAdapter : IDBAdapter {
        public IAdapterConfig Info { get; }  //Read only.
        internal ISqlHandler SQLHandler { get; }

        public Guid Id { get; }

        //ConcurrentDictionary<TargetDB, ISqlHandler> _handlers = new ConcurrentDictionary<TargetDB, ISqlHandler>();
        #region Public Methods

        ISqlHandler GetHandler(TargetDB target,string constr) {
            switch (target) {
                case TargetDB.maria:
                case TargetDB.mysql:
                return new MysqlHandler(constr);
                case TargetDB.mssql:
                return new MssqlHandler(constr);
                case TargetDB.pgsql:
                return new PgsqlHandler(constr);
                case TargetDB.sqlite:
                return new SqliteHandler(constr);
                case TargetDB.unknown:
                default:
                throw new ArgumentException($@"Unable to find any matching SQL Handler for the given target : {target}");
            }
        }

        public Task<object> Scalar(IAdapterArgs input, params (string key, object value)[] parameters) => SQLHandler.Scalar(input, parameters);

        public async Task<object> Read(IAdapterArgs input, params (string key, object value)[] parameters) {
            var dset = await SQLHandler.Read(input, parameters);
            if (dset == null) return null;
           var result =  ((DataSet)dset)?.Select(true)?.Convert(input.JsonStringAsNode)?.ToList();
            //We can apply filter here itself to reduce the overheads..
            return result?.ApplyFilter(input.Filter) ?? null;
        } 

        public Task<object> NonQuery(IAdapterArgs input, params (string key, object value)[] parameters) => SQLHandler.NonQuery(input, parameters);

        public void UpdateDBEntry(IAdapterConfig newentry) {
            Info.Update(newentry);
        }

        #region Typed Handlers 
        public async Task<IFeedback<DbRows>> ReadAsync(IAdapterArgs input, params (string key, object value)[] parameters) {
            var fb = new Feedback<DbRows>();
            var result = await Read(input, parameters);
            //If result is not dictionary, then we can throw error.. But if result is empty list, then we can return empty DbRows.
            if (result is not List<Dictionary<string, object>> list) return fb.SetMessage("Invalid result returned from database for ReadAsync.");
            return fb.SetStatus(true).SetResult(list.ToDbRows());
        }

        public async Task<IFeedback<DbRow>> ReadSingleAsync(IAdapterArgs input, params (string key, object value)[] parameters) {
            var fb = new Feedback<DbRow>();
            if (input is AdapterArgs ex) ex.Filter = ResultFilter.FirstDictionary; //We are setting result filter here itself.. 
            var result = await Read(input, parameters);
            if (result == null) return fb.SetStatus(true).SetMessage("No records found.");
            if (result is not Dictionary<string, object> dic) return fb.SetMessage("Invalid result returned from database for ReadAsync.");
            return fb.SetStatus(true).SetResult(dic.ToDbRow());
        }

        public async Task<IFeedback<T>> ScalarAsync<T>(IAdapterArgs input, params (string key, object value)[] parameters) {
            var fb = new Feedback<T>();
            var result = await Scalar(input, parameters);
            return result.TryAs<T>();
        }

        public async Task<IFeedback<int>> NonQueryAsync(IAdapterArgs input, params (string key, object value)[] parameters) {
            var fb = new Feedback<int>();
            var result = await NonQuery(input, parameters);
            if (result == null || !(result is int resInt)) return fb.SetStatus(false).SetResult(0);
            return fb.SetStatus(true).SetResult(resInt);
        }

        #endregion

        public async Task<IFeedback<IReadOnlyList<T>>> ListAsync<T>(IAdapterArgs input, params (string key, object value)[] parameters)
        {
            var fb = new Feedback<IReadOnlyList<T>>();
            if (input is AdapterArgs ex) ex.Filter = ResultFilter.FirstColumnValuesList; //We are setting result filter here itself.. 
            var result = await Read(input, parameters);
            if (result == null) return fb.SetStatus(true).SetMessage("No records found.");
            if (result is not List<object> list) return fb.SetMessage("Invalid result returned from database for ListAsync.");

            var finalResult = new List<T>();
            foreach (var item in list) {
                if (item is T typedItem) finalResult.Add(typedItem);
            }

            return fb.SetStatus(true).SetResult(finalResult);
        }

        #endregion

        //If root config key is null, then update during run-time is not possible.
        internal DBAdapter(IAdapterConfig entry) {
            Info = entry;
            SQLHandler = GetHandler(Info.DBType,entry.ConnectionString);
            Id = Guid.NewGuid();
        }
    }
}
