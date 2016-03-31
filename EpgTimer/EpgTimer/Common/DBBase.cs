using EpgTimer.DefineClass;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace EpgTimer.Common
{

    public abstract class DB
    {
        public enum connectTestResults
        {
            success,
            createDB,
            serverNotFound,
            unKnownError,
        };

        public static readonly DateTime minValue_DateTime = new DateTime(1900, 1, 1);

        #region - Constructor -
        #endregion

        #region - Method -
        #endregion

        public abstract bool exists(long id0);

        #region - Property -
        #endregion

        public abstract string tableName { get; }

        /// <summary>
        /// 
        /// </summary>
        public static string machineName
        {
            get { return _machineName; }
            set { _machineName = value; }
        }
        static string _machineName = null;

        /// <summary>
        /// 
        /// </summary>
        public static string instanceName
        {
            get { return _instanceName; }
            set { _instanceName = value; }
        }
        static string _instanceName = null;

        protected string dataSource
        {
            get
            {
                string dataSource1 = machineName;
                if (!string.IsNullOrWhiteSpace(instanceName))
                {
                    dataSource1 += "\\" + instanceName;
                }
                return dataSource1;
            }
        }

        #region - Event Handler -
        #endregion

    }

    public abstract class DBBase<T> : DB
        where T : class, IDBRecord
    {

        protected const string COLUMN_ID = "ID";
        protected const string FULLTEXTCATALOG = "epg_catalog";

        protected static readonly string startTimeStrFormat = "yyyy-MM-dd HH:mm";
        protected static readonly string timeStampStrFormat = "yyyy-MM-dd HH:mm:ss.fff";

        long _id = -1;

        #region - Constructor -
        #endregion

        protected DBBase() { }

        #region - Method -
        #endregion

        /// <summary>
        /// SQLServerへの接続テスト
        /// 
        /// </summary>
        /// <param name="isTestOnly"></param>
        /// <returns></returns>
        public connectTestResults connectionTest(bool isTestOnly)
        {
            try
            {
                using (SqlConnection sqlConn1 = new SqlConnection(sqlConnStr))
                {
                    sqlConn1.Open();
                    return connectTestResults.success;
                }
            }
            catch (SqlException sqlEx0)
            {
                switch (sqlEx0.Number)
                {
                    case -1:
                        // SQLSERVERが見つからない
                        // "SQL Server への接続を確立しているときにネットワーク関連またはインスタンス固有のエラーが発生しました。サーバーが見つからないかアクセスできません。インスタンス名が正しいこと、および SQL Server がリモート接続を許可するように構成されていることを確認してください。 (provider: SQL Network Interfaces, error: 26 - 指定されたサーバーまたはインスタンスの位置を特定しているときにエラーが発生しました)"
                        return connectTestResults.serverNotFound;
                    case 4060:
                        // DBが無い
                        // 「このログインで要求されたデータベース "%1!" を開けません。ログインに失敗しました。」
                        if (!isTestOnly)
                        {
                            createDB_EDCB();
                        }
                        return connectTestResults.createDB;
                    default:
                        System.Diagnostics.Trace.WriteLine(sqlEx0);
                        return connectTestResults.unKnownError;
                }
            }
            catch (Exception ex0)
            {
                System.Diagnostics.Trace.WriteLine(ex0);
            }

            return connectTestResults.unKnownError;
        }

        protected string getQuery_Select(string where0 = "", string orderBy0 = "", bool ascending0 = true, int amount0 = 0)
        {
            StringBuilder query1 = new StringBuilder("SELECT");
            if (0 < amount0)
            {
                query1.Append(" TOP " + amount0);
            }
            query1.Append(" * FROM [" + tableName + "]");
            if (!string.IsNullOrEmpty(where0))
            {
                query1.Append(" WHERE " + where0);
            }
            if (!string.IsNullOrEmpty(orderBy0))
            {
                query1.Append(" ORDER BY [" + orderBy0 + "]");
                if (!ascending0)
                {
                    query1.Append(" DESC");
                }
            }

            return query1.ToString();
        }

        public virtual int insert(T item0)
        {
            long id1;
            return insert(out id1, item0);
        }

        public virtual int insert(out long id0, T item0)
        {
            if (isSetIdByManual)
            {
                if (item0.ID < 0)
                {
                    item0.ID = getId();
                }
            }
            id0 = item0.ID;
            //
            StringBuilder keys1 = new StringBuilder();
            StringBuilder values1 = new StringBuilder();
            foreach (var kvp1 in getFieldNameValues(item0, true))
            {
                if (0 < keys1.Length)
                {
                    keys1.Append(", ");
                }
                keys1.Append("[" + kvp1.Key + "]");
                //
                if (0 < values1.Length)
                {
                    values1.Append(", ");
                }
                string val1 = kvp1.Value;
                if (string.IsNullOrEmpty(val1))
                {
                    val1 = "'" + val1 + "'";
                }
                values1.Append(val1);
            }
            string query1 = "INSERT INTO " + tableName + "(" + keys1.ToString() + ")" + " VALUES(" + values1.ToString() + ")";
            int res1 = -1;
            try
            {
                using (SqlConnection sqlConn1 = new SqlConnection(sqlConnStr))
                {
                    sqlConn1.Open();
                    using (SqlCommand cmd1 = new SqlCommand(query1, sqlConn1))
                    {
                        res1 = cmd1.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex0)
            {
                System.Diagnostics.Trace.WriteLine(ex0);
            }

            return res1;
        }

        public int update(T item0)
        {
            return update(
                new List<T>() { item0 });
        }

        public int update(List<T> items0)
        {
            int res1 = -1;

            StringBuilder query1 = new StringBuilder();
            foreach (var item1 in items0)
            {
            StringBuilder keyValue1 = new StringBuilder();
                foreach (var kvp1 in getFieldNameValues(item1, false))
            {
                if (0 < keyValue1.Length)
                {
                    keyValue1.Append(", ");
                }
                    keyValue1.Append("[" + kvp1.Key + "]=" + kvp1.Value);
                }
                query1.Append("UPDATE " + tableName + " SET " + keyValue1.ToString() + " WHERE " + COLUMN_ID + "=" + item1.ID + "\n");
            }
            try
            {
                using (SqlConnection sqlConn1 = new SqlConnection(sqlConnStr))
                {
                    sqlConn1.Open();
                    using (SqlCommand cmd1 = new SqlCommand(query1.ToString(), sqlConn1))
                    {
                        res1 = cmd1.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex0)
            {
                System.Diagnostics.Trace.WriteLine(ex0);
            }

            return res1;
        }

        public virtual int delete(T item0)
        {
            return delete(new long[] { item0.ID });
        }

        public virtual int delete(long id0)
        {
            return delete(new long[] { id0 });
        }

        public virtual int delete(IEnumerable<long> ids0)
        {
            if (ids0.Count() == 0) { return 0; }
            //
            StringBuilder sb1 = new StringBuilder();
            foreach (long id1 in ids0)
            {
                if (0 < sb1.Length)
                {
                    sb1.Append(" OR ");
                }
                sb1.Append(COLUMN_ID + "=" + id1);
            }

            int res1 = -1;
            string query1 = "DELETE FROM " + tableName + " WHERE " + sb1.ToString();
            try
            {
                using (SqlConnection sqlConn1 = new SqlConnection(sqlConnStr))
                {
                    sqlConn1.Open();
                    using (SqlCommand cmd1 = new SqlCommand(query1, sqlConn1))
                    {
                        res1 = cmd1.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex0)
            {
                System.Diagnostics.Trace.WriteLine(ex0);
            }

            return res1;
        }

        protected abstract Dictionary<string, string> getFieldNameValues(T item0, bool withID0);

        public abstract T getItem(SqlDataReader reader0);

        //public T exists<T>(long id0)
        //{
        //    return exists(id0, COLUMN_ID);
        //}

        public override bool exists(long id0)
        {
            return exists(id0, COLUMN_ID);
        }

        public bool exists(long id0, string columnName0)
        {
            if (id0 < 0) { return false; }
            //
            long? res1 = null;
            string query1 = "SELECT " + columnName0 + " FROM " + tableName + " WHERE " + columnName0 + "=" + id0;
            try
            {
                using (SqlConnection sqlConn1 = new SqlConnection(sqlConnStr))
                {
                    sqlConn1.Open();
                    using (SqlCommand cmd1 = new SqlCommand(query1, sqlConn1))
                    {
                        res1 = cmd1.ExecuteScalar() as long?;
                    }
                }
            }
            catch (Exception ex0)
            {
                System.Diagnostics.Trace.WriteLine(ex0);
            }

            return (res1 != null);
        }

        /// <summary>
        /// シングル・クォートで囲む
        /// </summary>
        /// <param name="str1"></param>
        /// <returns></returns>
        protected string q(string str1)
        {
            return "'" + str1 + "'";
        }

        /// <summary>
        /// シングルクォートをエスケープ
        /// シングルクォートで囲む
        /// </summary>
        /// <param name="text0"></param>
        /// <returns></returns>
        protected string createTextValue(string text0)
        {
            return q(text0.Replace("'", "''"));
        }

        public void createDB_EDCB()
        {
            string query1 = "USE master;" +
                "CREATE DATABASE EDCB;";
            string query2 = "USE EDCB;" +
                  "CREATE FULLTEXT CATALOG " + FULLTEXTCATALOG + "; ";
            string connStr1 = "Data Source=" + machineName + "\\" + instanceName + ";Initial Catalog=master;Integrated Security=True";
            try
            {
                using (SqlConnection sqlConn1 = new SqlConnection(connStr1))
                {
                    sqlConn1.Open();
                    using (SqlCommand cmd1 = new SqlCommand(query1, sqlConn1))
                    {
                        cmd1.ExecuteNonQuery();
                    }
                    using (SqlCommand cmd1 = new SqlCommand(query2, sqlConn1))
                    {
                        cmd1.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex0)
            {
                System.Diagnostics.Trace.WriteLine(ex0);
            }
        }

        protected string convert2BitString(bool val0)
        {
            return (val0 ? 1 : 0).ToString();
        }

        protected void createIndex(string indexName0, string[] columns0)
        {
            StringBuilder sb1 = new StringBuilder();
            foreach (var item1 in columns0)
            {
                if (0 < sb1.Length)
                {
                    sb1.Append(", ");
                }
                sb1.Append(item1);
            }
            string query1 = "IF NOT EXISTS (SELECT name FROM sys.indexes WHERE name = N'" + indexName0 + "')" +
                " CREATE INDEX " + indexName0 + " ON [dbo].[" + tableName + "](" + sb1.ToString() + ")";
            try
            {
                using (SqlConnection sqlConn1 = new SqlConnection(sqlConnStr))
                {
                    sqlConn1.Open();
                    using (SqlCommand cmd1 = new SqlCommand(query1, sqlConn1))
                    {
                        cmd1.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex0)
            {
                System.Diagnostics.Trace.WriteLine(ex0);
            }
        }

        public T select(long id0)
        {
            string where1 = COLUMN_ID + "=" + id0;
            List<T> itemList1 = select(where0: where1);
            if (0 == itemList1.Count)
            {
                return null;
            }
            else
            {
                return itemList1[0];
            }
        }

        public List<T> select(string where0 = "", string orderBy0 = "", bool ascending0 = true, int amount0 = 0)
        {
            string query1 = getQuery_Select(where0, orderBy0, ascending0, amount0);
            List<T> itemList1 = new List<T>();
            try
            {
                using (SqlConnection sqlConn1 = new SqlConnection(sqlConnStr))
                {
                    sqlConn1.Open();
                    using (SqlCommand cmd1 = new SqlCommand(query1, sqlConn1))
                    using (SqlDataReader reader1 = cmd1.ExecuteReader())
                    {
                        while (reader1.Read())
                        {
                            itemList1.Add(
                                getItem(reader1));
                        }
                    }
                }
            }
            catch (Exception ex0)
            {
                System.Diagnostics.Trace.WriteLine(ex0);
            }

            return itemList1;
        }

        protected abstract long getId();

        protected long _getId(ref long id0)
        {
            if (id0 == 0)
            {
                List<T> list1 = select(orderBy0: COLUMN_ID, ascending0: false, amount0: 1);
                if (0 < list1.Count)
                {
                    id0 = list1[0].ID;
                }
            }

            return System.Threading.Interlocked.Increment(ref id0);
        }

        #region - Property -
        #endregion


        protected string sqlConnStr
        {
            get { return "Data Source=" + dataSource + ";Initial Catalog=EDCB;Integrated Security=True"; }
        }

        /// <summary>
        /// IDBRecord.ID(Primary Key)を手動でセット
        /// </summary>
        protected abstract bool isSetIdByManual { get; }

        #region - Event Handler -
        #endregion

    }
}
