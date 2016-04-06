using EpgTimer.DefineClass;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace EpgTimer.Common
{
    public class DB_RecLog : DBBase<RecLogItem>, IDB_EpgEventInfo
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

        public const string COLUMN_recodeStatus = "recodeStatus",
                COLUMN_comment = "comment", COLUMN_RecFilePath = "recFilePath",
                COLUMN_epgEventInfoID = "epgEventInfoID", COLUMN_epgAllowOverWrite = "epgAllowOverWrite",
                COLUMN_lastUpdate = "lastUpdate";
        const string INDEX_NAME = "IX_Epg";

        #region - Constructor -
        #endregion

        public DB_RecLog()
        {
            db_EpgEventInfo = new DB_EpgEventInfo(this);
        }

        public DB_RecLog(string machineName0, string instanceName0)
            : this()
        {
            setSqlServerMachineName(machineName0);
            setSqlServerInstanceName(instanceName0);
        }

        #region - Method -
        #endregion

        protected override long getId()
        {
            return -1;
        }

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
            if (recodeStatuse0 == RecLogItem.RecodeStatuses.NONE) { return itemList1; }
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
                query1.Append(" AND (" + searchWordQuery0 + ")");
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
                            RecLogItem recLogItem1 = getItem(reader1);
                            recLogItem1.epgEventInfoR = db_EpgEventInfo.getItem(reader1);
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

        string getRecodeStatusQuery(RecLogItem.RecodeStatuses recodeStatuse0, string tableNameAbbr0 = TABLE_NAME_ABBR)
            {
            StringBuilder sb1 = new StringBuilder();
            string tableNameAbbr1 = null;
            if (!string.IsNullOrEmpty(tableNameAbbr0))
            {
                tableNameAbbr1 = tableNameAbbr0 + ".";
            }
            foreach (var item1 in new RecLogItem.RecodeStatuses[] {
                RecLogItem.RecodeStatuses.予約済み,
                RecLogItem.RecodeStatuses.視聴済み,
                RecLogItem.RecodeStatuses.録画完了,
                RecLogItem.RecodeStatuses.録画異常,
                RecLogItem.RecodeStatuses.無効登録,
                 RecLogItem.RecodeStatuses.不明 })
                {
                if (recodeStatuse0.HasFlag(item1))
                {
                    if (0 < sb1.Length)
                    {
                        sb1.Append(" OR ");
                    }

                    sb1.Append(tableNameAbbr1 + COLUMN_recodeStatus + "=" + (int)item1);
                }
            }

            return "(" + sb1.ToString() + ")";
        }

        public override RecLogItem getItem(SqlDataReader reader0)
        {
            RecLogItem recLogItem1 = new RecLogItem()
            {
                ID = (long)reader0[COLUMN_ID],
                recodeStatus = (RecLogItem.RecodeStatuses)reader0[COLUMN_recodeStatus],
                comment = (string)reader0[COLUMN_comment],
                recFilePath = (string)reader0[COLUMN_RecFilePath],
                epgEventInfoID = (long)reader0[COLUMN_epgEventInfoID],
                epgAlllowOverWrite = (bool)reader0[COLUMN_epgAllowOverWrite],
                lastUpdate = (DateTime)reader0[COLUMN_lastUpdate],
            };
            recLogItem1.epgEventInfoR = db_EpgEventInfo.select(recLogItem1.epgEventInfoID);

            return recLogItem1;
        }

        public RecLogItem exists(EpgEventInfo epg0)
        {
            return exists(RecLogItem.RecodeStatuses.ALL, epg0.original_network_id, epg0.transport_stream_id, epg0.service_id, epg0.event_id, epg0.start_time);
        }

        /// <summary>
        /// 
        /// </summary>
     /// <param name="reserveData0"></param>
     /// <returns></returns>
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
                            recLogItem1 = getItem(reader1);
                            recLogItem1.epgEventInfoR = db_EpgEventInfo.getItem(reader1);
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
            RecLogItem.RecodeStatuses recodeStatuse1 = RecLogItem.RecodeStatuses.予約済み | RecLogItem.RecodeStatuses.無効登録;
            string where1 = getRecodeStatusQuery(recodeStatuse1, string.Empty) +
                " AND " + COLUMN_lastUpdate + "<" + q(lastUpdate0.ToString(DB_EpgEventInfo.timeStampStrFormat));
            List<RecLogItem> recLogItemList1 = select(where0: where1);

            return recLogItemList1;
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

        public override int insert(RecLogItem item0)
        {
            long epgEventInfoID1;
            db_EpgEventInfo.insert(out epgEventInfoID1, item0.epgEventInfoR);
            item0.epgEventInfoID = epgEventInfoID1;

            return base.insert(item0);
        }

        public void updateEpg(EpgEventInfoR item0)
        {
            db_EpgEventInfo.update(item0);
        }

        public int update(RecLogItem item0, bool isUpdateEpg0 = false)
        {
            if (isUpdateEpg0)
            {
                db_EpgEventInfo.update(item0.epgEventInfoR);
            }
            //
            return base.update(item0);
        }

        /// <summary>
        /// epgEventInfoも削除する
        /// </summary>
        /// <param name="item0"></param>
        /// <returns></returns>
        public override int delete(RecLogItem item0)
        {
            return delete(new RecLogItem[] { item0 });
        }

        /// <summary>
        /// epgEventInfoも削除する
        /// </summary>
        /// <param name="items0"></param>
        /// <returns></returns>
        public int delete(IEnumerable<RecLogItem> items0)
                {
            int res1 = 0;
            if (items0.Count() == 0) { return 0; }

            res1 = base.delete(
                items0.Select(x => x.ID));
            db_EpgEventInfo.delete(
                items0.Select(x => x.epgEventInfoID));

            return res1;
        }

        protected override Dictionary<string, string> getFieldNameValues(RecLogItem item0, bool withID0)
        {
            Dictionary<string, string> dict1 = new Dictionary<string, string>();
            dict1.Add(COLUMN_lastUpdate, q(item0.lastUpdate.ToString(DB_EpgEventInfo.timeStampStrFormat)));
            dict1.Add(COLUMN_recodeStatus, ((int)item0.recodeStatus).ToString());
            dict1.Add(COLUMN_comment, createTextValue(item0.comment));
            dict1.Add(COLUMN_RecFilePath, createTextValue(item0.recFilePath));
            dict1.Add(COLUMN_epgEventInfoID, item0.epgEventInfoID.ToString());
            dict1.Add(COLUMN_epgAllowOverWrite, (item0.epgAlllowOverWrite ? 1 : 0).ToString());

            return dict1;
        }

        /// <summary>
        /// create RecLog Table and EpgEventInfo Table.
        /// </summary>
        public void createTable_RecLog_EpgEventInfo()
        {
            createTable();
            db_EpgEventInfo.createTable();
            createIndex();
        }

        public void alterTable_EpgEventInfo()
        {
            db_EpgEventInfo.alterTable_COLUMN_ExtInfo_text_char();
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

        public override string tableName
        {
            get { return TABLE_NAME; }
        }

        protected override bool isSetIdByManual
        {
            get { return false; }
        }

        public DB_EpgEventInfo db_EpgEventInfo { get; private set; }

        public string columnName_epgEventInfoID
        {
            get { return COLUMN_epgEventInfoID; }
        }

        #region - Event Handler -
        #endregion

    }
}
