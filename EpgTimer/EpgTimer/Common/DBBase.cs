using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace EpgTimer.Common
{
    public abstract class DBBase
    {

        protected const string FULLTEXTCATALOG = "epg_catalog";

        protected readonly DateTime minValue_SmallDateTime = new DateTime(1900, 1, 1);

        public enum connectTestResults
        {
            success,
            createDB,
            serverNotFound,
            unKnownError,
        };

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

        protected string getQuery_Select(string where0, string orderBy0, bool ascending0, int amount0)
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

        protected int insert(Dictionary<string, string> keyValueDict0)
        {
            int res1 = -1;
            StringBuilder keys1 = new StringBuilder();
            StringBuilder values1 = new StringBuilder();
            foreach (var item in keyValueDict0)
            {
                if (0 < keys1.Length)
                {
                    keys1.Append(", ");
                }
                keys1.Append("[" + item.Key + "]");
                //
                if (0 < values1.Length)
                {
                    values1.Append(", ");
                }
                string val1 = item.Value;
                if (string.IsNullOrEmpty(val1))
                {
                    val1 = "'" + val1 + "'";
                }
                values1.Append(val1);
            }
            string query1 = "INSERT INTO " + tableName + "(" + keys1.ToString() + ")" + " VALUES(" + values1.ToString() + ")";
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

        protected int update(Dictionary<string, string> keyValueDict0, string where0)
        {
            int res1 = -1;
            StringBuilder keyValue1 = new StringBuilder();
            foreach (var item in keyValueDict0)
            {
                if (0 < keyValue1.Length)
                {
                    keyValue1.Append(", ");
                }
                keyValue1.Append("[" + item.Key + "]=" + item.Value);
            }
            string query1 = "UPDATE " + tableName + " SET " + keyValue1.ToString() + " WHERE " + where0;
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

        protected int delete(StringBuilder where0)
        {
            return delete(where0.ToString());
        }

        protected int delete(string where0)
        {
            int res1 = -1;
            string query1 = "DELETE FROM " + tableName + " WHERE " + where0;
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

        protected bool exists(string where0)
        {
            bool isExist1 = false;
            string query1 = "SELECT TOP 1 * FROM " + tableName + " WHERE " + where0;
            try
            {
                using (SqlConnection sqlConn1 = new SqlConnection(sqlConnStr))
                {
                    sqlConn1.Open();
                    using (SqlCommand cmd1 = new SqlCommand(query1, sqlConn1))
                    {
                        isExist1 = (cmd1.ExecuteNonQuery() == 1);
                    }
                }
            }
            catch (Exception ex0)
            {
                System.Diagnostics.Trace.WriteLine(ex0);
            }

            return isExist1;
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

        #region - Property -
        #endregion

        protected static string machineName { get; set; }

        protected static string instanceName { get; set; }

        string dataSource
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

        protected string sqlConnStr
        {
            get { return "Data Source=" + dataSource + ";Initial Catalog=EDCB;Integrated Security=True"; }
        }

        protected abstract string tableName { get; }

        #region - Event Handler -
        #endregion

    }
}
