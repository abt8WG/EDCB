using EpgTimer.DefineClass;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace EpgTimer.Common
{
    public class DB_RecLog : DBBase
    {

        public enum searchColumns
        {
            NONE = 0,
            /// <summary>
            /// 番組名
            /// </summary>
            title = 1,
            /// <summary>
            /// 番組情報
            /// </summary>
            content = 2,
            /// <summary>
            /// コメント
            /// </summary>
            comment = 4,
            /// <summary>
            /// 録画ファイル名
            /// </summary>
            recFilePath = 8,
            /// <summary>
            /// 
            /// </summary>
            ALL = 15
        };

        const string TABLE_NAME = "recLog";
        const string TABLE_NAME_ABBR = "rl";

        public const string COLUMN_ID = "ID", COLUMN_recodeStatus = "recodeStatus",
                COLUMN_comment = "comment", COLUMN_RecFilePath = "recFilePath",
                COLUMN_epgEventInfoID = "epgEventInfoID", COLUMN_epgAllowOverWrite = "epgAllowOverWrite",
                COLUMN_lastUpdate = "lastUpdate";
        const string INDEX_NAME = "IX_Epg";

        #region - Constructor -
        #endregion

        public DB_RecLog(string machineName0, string instanceName0)
        {
            setSqlServerMachineName(machineName0);
            setSqlServerInstanceName(instanceName0);
        }

        #region - Method -
        #endregion

        public void setSqlServerMachineName(string machineName0)
        {
            machineName = machineName0;
        }

        public void setSqlServerInstanceName(string instanceName0)
        {
            instanceName = instanceName0;
        }

        List<string> getSearchWordList(string searchWord0)
        {
            char[] ng1 = new char[] { '「', '」', '（', '）', '(', ')' };
            foreach (var item in ng1)
            {
                searchWord0 = searchWord0.Replace(item, ' ');
            }

            List<string> searchWordList1 = new List<string>();
            foreach (var item in Regex.Split(searchWord0, "\\s+"))
            {
                if (!string.IsNullOrEmpty(item) && 1 < item.Length)
                {
                    searchWordList1.Add(item);
                }
            }

            return searchWordList1;
        }

        public List<RecLogItem> search_Like(string searchWord0, RecLogItem.RecodeStatuses recodeStatuse0, searchColumns searchColumn0 = searchColumns.title, int count0 = 50)
        {
            List<string> searchWordList1 = getSearchWordList(searchWord0);
            StringBuilder sb1 = new StringBuilder();
            foreach (var item1 in searchWordList1)
            {
                string likeWord1 = " LIKE " + base.createTextValue("%" + item1 + "%");
                List<string> searchWords1 = new List<string>();
                if (searchColumn0.HasFlag(searchColumns.title))
                {
                    searchWords1.Add(DB_EpgEventInfo.TABLE_NAME_ABBR + "." + DB_EpgEventInfo.COLUMN_ShortInfo_event_name + likeWord1);
                }
                if (searchColumn0.HasFlag(searchColumns.content))
                {
                    searchWords1.Add(DB_EpgEventInfo.TABLE_NAME_ABBR + "." + DB_EpgEventInfo.COLUMN_ShortInfo_text_char + likeWord1);
                }
                if (searchColumn0.HasFlag(searchColumns.comment))
                {
                    searchWords1.Add(TABLE_NAME_ABBR + "." + COLUMN_comment + likeWord1);
                }
                if (searchColumn0.HasFlag(searchColumns.recFilePath))
                {
                    searchWords1.Add(TABLE_NAME_ABBR + "." + COLUMN_RecFilePath + likeWord1);
                }
                StringBuilder sb2 = new StringBuilder();
                foreach (var item2 in searchWords1)
                {
                    if (0 < sb2.Length)
                    {
                        sb2.Append(" OR ");
                    }
                    sb2.Append(item2);
                }
                //
                if (0 < sb1.Length)
                {
                    sb1.Append(" AND ");
                }
                sb1.Append(sb2.ToString());
            }

            return seach(sb1.ToString(), recodeStatuse0, count0);
        }

        /// <summary>
        /// フルテキストのFREETEXTで得た結果をRegexで絞り込む
        /// </summary>
        /// <param name="searchWord0"></param>
        /// <param name="recodeStatuse0"></param>
        /// <param name="searchColumn0"></param>
        /// <param name="count0"></param>
        /// <returns></returns>
        public List<RecLogItem> search_Fulltext_Freetext_Regex(string searchWord0, RecLogItem.RecodeStatuses recodeStatuse0,
            searchColumns searchColumn0 = searchColumns.title, int count0 = 50)
        {
            List<RecLogItem> recLogItemList1 = new List<RecLogItem>();

            List<string> searchWordList1 = new List<string>() { searchWord0 };
            string searchWordQuery_Fulltext1 = getSearchWordQuery_Fulltext(searchColumn0, searchWordList1, true);

            List<RecLogItem> recLogItemList2 = seach(searchWordQuery_Fulltext1, recodeStatuse0, count0);

            List<string> searchWordList2 = getSearchWordList(searchWord0);
            List<Regex> searchRegexList2 = new List<Regex>();
            foreach (var item in searchWordList2)
            {
                searchRegexList2.Add(new Regex(item));
            }
            foreach (var recLogItem1 in recLogItemList2)
            {
                List<string> textList1 = new List<string>();
                if (searchColumn0.HasFlag(searchColumns.title))
                {
                    textList1.Add(recLogItem1.tvProgramTitle);
                }
                if (searchColumn0.HasFlag(searchColumns.content))
                {
                    textList1.Add(recLogItem1.tvProgramSummary);
                }
                if (searchColumn0.HasFlag(searchColumns.comment))
                {
                    textList1.Add(recLogItem1.comment);
                }
                if (searchColumn0.HasFlag(searchColumns.recFilePath))
                {
                    textList1.Add(recLogItem1.recFilePath);
                }
                foreach (var text1 in textList1)
                {
                    int matchCnt1 = 0;
                    foreach (var rgx1 in searchRegexList2)
                    {
                        if (rgx1.IsMatch(text1))
                        {
                            matchCnt1++;
                        }
                    }
                    if (matchCnt1 == searchRegexList2.Count)
                    {
                        recLogItemList1.Add(recLogItem1);
                        break;
                    }
                }
            }

            return recLogItemList1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="searchWord0"></param>
        /// <param name="searchColumn0">複数選択可</param>
        /// <param name="count0"></param>
        /// <returns>新しいもの順</returns>
        public List<RecLogItem> search_Fulltext(string searchWord0, RecLogItem.RecodeStatuses recodeStatuse0,
            searchColumns searchColumn0 = searchColumns.title, int count0 = 50, bool isFreetext0 = false)
        {
            List<string> searchWordList1;
            if (isFreetext0)
            {
                searchWordList1 = new List<string>() { searchWord0 };
            }
            else
            {
                searchWordList1 = getSearchWordList(searchWord0);
            }
            string searchWordQuery_Fulltext1 = getSearchWordQuery_Fulltext(searchColumn0, searchWordList1, isFreetext0);

            return seach(searchWordQuery_Fulltext1, recodeStatuse0, count0);
        }

        string getSearchWordQuery_Fulltext(searchColumns searchColumn0, List<string> searchWordList0, bool isFreetext0)
        {
            if (searchWordList0.Count == 0) { return null; }
            //
            string freetextOrContains1 = "CONTAINS";
            if (isFreetext0)
            {
                freetextOrContains1 = "FREETEXT";
            }
            // フルテキスト・サーチでエラーになる文字を削除（空白文字に置換え）
            char[] removeChars1 = new char[] { '!', '(', ')', ',' };
            StringBuilder sb1 = new StringBuilder();
            foreach (var item in searchWordList0)
            {
                string word1 = item;
                foreach (var c1 in removeChars1)
                {
                    word1 = word1.Replace(c1, ' ');
                }
                if (0 < sb1.Length)
                {
                    sb1.Append(" AND ");
                }
                sb1.Append('"' + word1 + '"');
            }
            string searchWord2 = base.createTextValue(sb1.ToString());
            StringBuilder query1 = new StringBuilder();
            {
                List<string> columList1 = new List<string>();
                if (searchColumn0.HasFlag(searchColumns.title))
                {
                    columList1.Add(DB_EpgEventInfo.TABLE_NAME_ABBR + "." + DB_EpgEventInfo.COLUMN_ShortInfo_event_name);
                }
                if (searchColumn0.HasFlag(searchColumns.content))
                {
                    columList1.Add(DB_EpgEventInfo.TABLE_NAME_ABBR + "." + DB_EpgEventInfo.COLUMN_ShortInfo_text_char);
                }
                if (0 < columList1.Count)
                {
                    StringBuilder sb_Colum1 = new StringBuilder();
                    foreach (var item in columList1)
                    {
                        if (0 < sb_Colum1.Length)
                        {
                            sb_Colum1.Append(", ");
                        }
                        sb_Colum1.Append(item);
                    }
                    query1.Append(freetextOrContains1 + "((" + sb_Colum1.ToString() + "), " + searchWord2 + ")");
                }
            }
            {
                List<string> columList_RecLog1 = new List<string>();
                if (searchColumn0.HasFlag(searchColumns.comment))
                {
                    columList_RecLog1.Add(TABLE_NAME_ABBR + "." + COLUMN_comment);
                }
                if (searchColumn0.HasFlag(searchColumns.recFilePath))
                {
                    columList_RecLog1.Add(TABLE_NAME_ABBR + "." + COLUMN_RecFilePath);
                }
                if (0 < columList_RecLog1.Count)
                {
                    StringBuilder sb_Colum_RecLog1 = new StringBuilder();
                    foreach (var item in columList_RecLog1)
                    {
                        if (0 < sb_Colum_RecLog1.Length)
                        {
                            sb_Colum_RecLog1.Append(", ");
                        }
                        sb_Colum_RecLog1.Append(item);
                    }
                    if (0 < query1.Length)
                    {
                        query1.Append(" OR ");
                    }
                    query1.Append(freetextOrContains1 + "((" + sb_Colum_RecLog1.ToString() + "), " + searchWord2 + ")");
                }
            }

            return query1.ToString();
        }

        List<RecLogItem> seach(string searchWordQuery0, RecLogItem.RecodeStatuses recodeStatuse0, int count0)
        {
            List<RecLogItem> itemList1 = new List<RecLogItem>();
            //
            StringBuilder query1 = new StringBuilder();
            query1.Append("SELECT");
            if (0 < count0)
            {
                query1.Append(" TOP " + count0);
            }
            query1.Append(" * FROM " + tableName + " " + TABLE_NAME_ABBR + " INNER JOIN " + DB_EpgEventInfo.TABLE_NAME + " " + DB_EpgEventInfo.TABLE_NAME_ABBR +
                " ON (" + TABLE_NAME_ABBR + "." + COLUMN_epgEventInfoID + "=" + DB_EpgEventInfo.TABLE_NAME_ABBR + "." + DB_EpgEventInfo.COLUMN_ID + ")");
            query1.Append(" WHERE " + getRecodeStatusQuery(recodeStatuse0));
            if (!string.IsNullOrEmpty(searchWordQuery0))
            {
                query1.Append(" AND " + searchWordQuery0);
            }
            query1.Append(" ORDER BY " + DB_EpgEventInfo.TABLE_NAME_ABBR + "." + DB_EpgEventInfo.COLUMN_start_time + " DESC");
            try
            {
                using (SqlConnection sqlConn1 = new SqlConnection(sqlConnStr))
                {
                    sqlConn1.Open();
                    using (SqlCommand cmd1 = new SqlCommand(query1.ToString(), sqlConn1))
                    using (SqlDataReader reader1 = cmd1.ExecuteReader())
                    {
                        while (reader1.Read())
                        {
                            RecLogItem recLogItem1 = getRecLogItem(reader1);
                            recLogItem1.epgEventInfoR = db_EpgEventInfo.getEpgEventInfo(reader1);
                            itemList1.Add(recLogItem1);
                        }
                    }
                }
            }
            catch (Exception ex0)
            {
                System.Diagnostics.Trace.WriteLine(ex0);
            }

            //if (itemList1.Count == 0) {
            //    Console.WriteLine(query1);
            //}

            return itemList1;
        }

        static string getRecodeStatusQuery(RecLogItem.RecodeStatuses recodeStatuse0)
        {
            StringBuilder sb_RecodeStatus1 = new StringBuilder();
            {
                List<RecLogItem.RecodeStatuses> recodeStatusList1 = new List<RecLogItem.RecodeStatuses>();
                if (recodeStatuse0.HasFlag(RecLogItem.RecodeStatuses.予約済み))
                {
                    recodeStatusList1.Add(RecLogItem.RecodeStatuses.予約済み);
                }
                if (recodeStatuse0.HasFlag(RecLogItem.RecodeStatuses.録画完了))
                {
                    recodeStatusList1.Add(RecLogItem.RecodeStatuses.録画完了);
                }
                if (recodeStatuse0.HasFlag(RecLogItem.RecodeStatuses.録画異常))
                {
                    recodeStatusList1.Add(RecLogItem.RecodeStatuses.録画異常);
                }
                if (recodeStatuse0.HasFlag(RecLogItem.RecodeStatuses.視聴済み))
                {
                    recodeStatusList1.Add(RecLogItem.RecodeStatuses.視聴済み);
                }
                foreach (var item in recodeStatusList1)
                {
                    if (0 < sb_RecodeStatus1.Length)
                    {
                        sb_RecodeStatus1.Append(" OR ");
                    }
                    sb_RecodeStatus1.Append(TABLE_NAME_ABBR + "." + COLUMN_recodeStatus + "=" + (int)item);
                }
            }

            return "(" + sb_RecodeStatus1.ToString() + ")";
        }

        RecLogItem getRecLogItem(SqlDataReader reader0)
        {
            RecLogItem recLogItem1 = new RecLogItem()
            {
                id = (long)reader0[COLUMN_ID],
                recodeStatus = (RecLogItem.RecodeStatuses)reader0[COLUMN_recodeStatus],
                comment = (string)reader0[COLUMN_comment],
                recFilePath = (string)reader0[COLUMN_RecFilePath],
                epgEventInfoID = (long)reader0[COLUMN_epgEventInfoID],
                epgAlllowOverWrite = (bool)reader0[COLUMN_epgAllowOverWrite],
                lastUpdate = (DateTime)reader0[COLUMN_lastUpdate],
            };

            return recLogItem1;
        }

        /// <summary>
        /// 
        /// </summary>
        public RecLogItem exists(ReserveData reserveData0)
        {
            return exists(RecLogItem.RecodeStatuses.ALL, reserveData0.OriginalNetworkID, reserveData0.TransportStreamID, reserveData0.ServiceID, reserveData0.EventID, reserveData0.StartTime);
        }

        public RecLogItem exists(RecFileInfo recFileInfo0)
        {
            return exists(RecLogItem.RecodeStatuses.ALL, recFileInfo0.OriginalNetworkID, recFileInfo0.TransportStreamID, recFileInfo0.ServiceID, recFileInfo0.EventID, recFileInfo0.StartTime);
        }

        public RecLogItem exists(RecLogItem.RecodeStatuses recodeStatuse0, ushort original_network_id0, ushort transport_stream_id0, ushort service_id0, ushort event_id0, DateTime startTime0)
        {
            RecLogItem recLogItem1 = null;
            string query1 = "SELECT TOP 1 * FROM " + tableName + " " + TABLE_NAME_ABBR +
                " INNER JOIN " + DB_EpgEventInfo.TABLE_NAME + " " + DB_EpgEventInfo.TABLE_NAME_ABBR +
                " ON (" + TABLE_NAME_ABBR + "." + COLUMN_epgEventInfoID + "=" + DB_EpgEventInfo.TABLE_NAME_ABBR + "." + DB_EpgEventInfo.COLUMN_ID + ")" +
                " WHERE " + getRecodeStatusQuery(recodeStatuse0) +
                " AND " + DB_EpgEventInfo.TABLE_NAME_ABBR + "." + DB_EpgEventInfo.COLUMN_original_network_id + "=" + original_network_id0 +
                " AND " + DB_EpgEventInfo.TABLE_NAME_ABBR + "." + DB_EpgEventInfo.COLUMN_transport_stream_id + "=" + transport_stream_id0 +
                " AND " + DB_EpgEventInfo.TABLE_NAME_ABBR + "." + DB_EpgEventInfo.COLUMN_service_id + "=" + service_id0 +
                " AND " + DB_EpgEventInfo.TABLE_NAME_ABBR + "." + DB_EpgEventInfo.COLUMN_event_id + "=" + event_id0 +
                " AND " + DB_EpgEventInfo.TABLE_NAME_ABBR + "." + DB_EpgEventInfo.COLUMN_start_time + "=" + q(startTime0.ToString(DB_EpgEventInfo.startTimeStrFormat));
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
                            recLogItem1 = getRecLogItem(reader1);
                            recLogItem1.epgEventInfoR = db_EpgEventInfo.getEpgEventInfo(reader1);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex0)
            {
                System.Diagnostics.Trace.WriteLine(ex0);
            }

            return recLogItem1;
        }

        ///// <summary>
        ///// RecFileInfoから登録済みかチェック
        ///// </summary>
        ///// <param name="recFileInfo0"></param>
        ///// <returns></returns>
        //public bool exists_RecInfo(RecFileInfo recFileInfo0)
        //{
        //    string query1 = "SELECT TOP 1 " + TABLE_NAME_ABBR + "." + COLUMN_epgEventInfoID + " FROM " + tableName + " " + TABLE_NAME_ABBR +
        //        " INNER JOIN " + DB_EpgEventInfo.TABLE_NAME + " " + DB_EpgEventInfo.TABLE_NAME_ABBR +
        //        " ON (" + TABLE_NAME_ABBR + "." + COLUMN_epgEventInfoID + "=" + DB_EpgEventInfo.TABLE_NAME_ABBR + "." + DB_EpgEventInfo.COLUMN_ID + ")" +
        //        " WHERE " + " " + DB_EpgEventInfo.TABLE_NAME_ABBR + "." + DB_EpgEventInfo.COLUMN_start_time + "=" + q(recFileInfo0.StartTime.ToString(DB_EpgEventInfo.startTimeStrFormat)) +
        //        " AND " + DB_EpgEventInfo.TABLE_NAME_ABBR + "." + DB_EpgEventInfo.COLUMN_ShortInfo_event_name + "=" + createTextValue(recFileInfo0.Title);
        //    try
        //    {
        //        using (SqlConnection sqlConn1 = new SqlConnection(sqlConnStr))
        //        {
        //            sqlConn1.Open();
        //            using (SqlCommand cmd1 = new SqlCommand(query1, sqlConn1))
        //            {
        //                if (cmd1.ExecuteScalar() != null)
        //                {
        //                    return true;
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex0)
        //    {
        //        System.Diagnostics.Trace.WriteLine(ex0);
        //    }

        //    return false;
        //}

        public List<RecLogItem> select_Reserved()
        {
            string where1 = COLUMN_recodeStatus + "=" + (int)RecLogItem.RecodeStatuses.予約済み;
            List<RecLogItem> recLogItemList1 = select(where0: where1);

            return recLogItemList1;
        }

        /// <summary>
        /// 更新されなかった予約アイテムを取得
        /// </summary>
        /// <param name="lastUpdate0">予約リスト更新時刻</param>
        /// <returns></returns>
        public List<RecLogItem> select_Reserved_NotUpdated(DateTime lastUpdate0)
        {
            string where1 = COLUMN_recodeStatus + "=" + (int)RecLogItem.RecodeStatuses.予約済み +
                " AND " + COLUMN_lastUpdate + "<" + q(lastUpdate0.ToString(DB_EpgEventInfo.timeStampStrFormat));
            List<RecLogItem> recLogItemList1 = select(where0: where1);

            return recLogItemList1;
        }

        public List<RecLogItem> select(string where0 = "", string orderBy0 = "", bool ascending0 = true, int amount0 = -1)
        {
            string query1 = base.getQuery_Select(where0, orderBy0, ascending0, amount0);
            List<RecLogItem> itemList1 = new List<RecLogItem>();
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
                            RecLogItem recLogItem1 = getRecLogItem(reader1);
                            recLogItem1.epgEventInfoR = _db_EpgEventInfo.select(recLogItem1.epgEventInfoID);
                            itemList1.Add(recLogItem1);
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

        public RecLogItem insert(RecFileInfo recFileInfo0, DateTime lastUpdate0)
        {
            EpgEventInfoR epgEventInfoR1 = new EpgEventInfoR(recFileInfo0, lastUpdate0);
            RecLogItem recLogItem1 = new RecLogItem()
            {
                lastUpdate = lastUpdate0,
                recFilePath = recFileInfo0.RecFilePath,
                epgEventInfoR = epgEventInfoR1
            };
            if ((RecEndStatus)recFileInfo0.RecStatus == RecEndStatus.NORMAL)
            {
                recLogItem1.recodeStatus = RecLogItem.RecodeStatuses.録画完了;
            }
            else
            {
                recLogItem1.recodeStatus = RecLogItem.RecodeStatuses.録画異常;
            }
            insert(recLogItem1);

            return recLogItem1;
        }

        public int insert(RecLogItem item0)
        {
            db_EpgEventInfo.insert(item0.epgEventInfoR);
            item0.epgEventInfoID = item0.epgEventInfoR.ID;

            Dictionary<string, string> keyValueDict1 = getFieldNameValues(item0);
            return base.insert(keyValueDict1);
        }

        public void updateEpg(EpgEventInfoR item0)
        {
            _db_EpgEventInfo.update(item0);
        }

        public int update(RecLogItem item0, bool isUpdateEpg0 = false)
        {
            if (isUpdateEpg0)
            {
                _db_EpgEventInfo.update(item0.epgEventInfoR);
            }
            //
            Dictionary<string, string> keyValueDict1 = getFieldNameValues(item0);
            string where1 = getQuery_Where(item0);
            return base.update(keyValueDict1, where1);
        }

        string getQuery_Where(RecLogItem item0)
        {
            return COLUMN_ID + "=" + item0.id;
        }

        /// <summary>
        /// epgEventInfoも削除する
        /// </summary>
        /// <param name="item0"></param>
        /// <returns></returns>
        public int delete(RecLogItem item0)
        {
            return delete(new RecLogItem[] { item0 });
        }

        public int delete(IList<RecLogItem> items0)
        {
            if (items0.Count == 0) { return 0; }
            //
            StringBuilder where1 = new StringBuilder();
            List<long> epgEventInfoIDList1 = new List<long>();
            foreach (var item in items0)
            {
                if (0 < where1.Length)
                {
                    where1.Append(" OR ");
                }
                where1.Append(COLUMN_ID + "=" + item.id);
                epgEventInfoIDList1.Add(item.epgEventInfoID);
            }

            return base.delete(where1);
        }

        Dictionary<string, string> getFieldNameValues(RecLogItem item0)
        {
            return new Dictionary<string, string>() {
                { COLUMN_lastUpdate, q(item0.lastUpdate.ToString(DB_EpgEventInfo.timeStampStrFormat)) },
                { COLUMN_recodeStatus, ((int)item0.recodeStatus).ToString() },
                { COLUMN_comment, base.createTextValue(item0.comment) },
                { COLUMN_RecFilePath, base.createTextValue(item0.recFilePath) },
                { COLUMN_epgEventInfoID, item0.epgEventInfoID.ToString() },
                { COLUMN_epgAllowOverWrite,(item0.epgAlllowOverWrite?1:0).ToString() },
            };
        }

        /// <summary>
        /// create RecLog Table and EpgEventInfo Table.
        /// </summary>
        public void createTable_RecLog_EpgEventInfo()
        {
            createTable();
            _db_EpgEventInfo.createTable();
            createIndex();
        }

        void createTable()
        {

            string query1 = "CREATE TABLE [dbo].[" + TABLE_NAME + "](" +
                "[" + COLUMN_ID + "] [bigint] IDENTITY(1,1) NOT NULL," +
                "[" + COLUMN_recodeStatus + "] [int] NOT NULL," +
                "[" + COLUMN_RecFilePath + "] [nvarchar](260) NOT NULL," +
                "[" + COLUMN_comment + "] [nvarchar](max) NOT NULL," +
                "[" + COLUMN_epgEventInfoID + "] [bigint] NOT NULL," +
                "[" + COLUMN_epgAllowOverWrite + "] [bit] NOT NULL," +
                "[" + COLUMN_lastUpdate + "] [datetime] NOT NULL," +
                "CONSTRAINT [PK_RecLog] PRIMARY KEY CLUSTERED " +
                "([" + COLUMN_ID + "] ASC))";

            string query2 = "CREATE FULLTEXT INDEX ON " + TABLE_NAME + "(" +
                COLUMN_RecFilePath + " Language 1041," +
                COLUMN_comment + " Language 1041" +
                ") KEY INDEX PK_RecLog ON " + FULLTEXTCATALOG + ";";

            try
            {
                using (SqlConnection sqlConn1 = new SqlConnection(sqlConnStr))
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

        public void createIndex()
        {
            string query1 = "IF NOT EXISTS (SELECT name FROM sys.indexes WHERE name = N'" + INDEX_NAME + "')" +
                " CREATE INDEX " + INDEX_NAME + " ON [dbo].[" + TABLE_NAME + "](" + COLUMN_epgEventInfoID + ")";
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
            this.db_EpgEventInfo.createIndex();
        }

        #region - Property -
        #endregion

        protected override string tableName
        {
            get { return TABLE_NAME; }
        }

        public DB_EpgEventInfo db_EpgEventInfo
        {
            get { return _db_EpgEventInfo; }
            set { _db_EpgEventInfo = value; }
        }
        DB_EpgEventInfo _db_EpgEventInfo = new DB_EpgEventInfo();

        #region - Event Handler -
        #endregion

    }
}
