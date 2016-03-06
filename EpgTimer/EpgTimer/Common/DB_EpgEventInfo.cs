using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace EpgTimer.Common
{
    public class DB_EpgEventInfo : DBBase
    {


        public const string TABLE_NAME = "EpgEventInfo";
        /// <summary>
        /// 略記
        /// </summary>
        public const string TABLE_NAME_ABBR = "eei";
        public const string COLUMN_ID = "ID", COLUMN_lastUpdate = "lastUpdate",
            COLUMN_original_network_id = "original_network_id", COLUMN_transport_stream_id = "transport_stream_id",
            COLUMN_service_id = "service_id", COLUMN_event_id = "event_id",
            COLUMN_StartTimeFlag = "StartTimeFlag", COLUMN_start_time = "start_time",
            COLUMN_DurationFlag = "DurationFlag", COLUMN_durationSec = "durationSec",
            COLUMN_ShortInfo_event_name = "ShortInfo_event_name", COLUMN_ShortInfo_text_char = "ShortInfo_text_char",
            COLUMN_ExtInfo_text_char = "ExtInfo_text_char",
            COLUMN_ContentInfo = "ContentInfo";
        const string INDEX_NAME = "UX_Epg";
        long _id = -1;

        #region - Constructor -
        #endregion

        #region - Method -
        #endregion

        public EpgEventInfoR select(long epgEventInfoID0)
        {
            string where1 = COLUMN_ID + "=" + epgEventInfoID0;
            List<EpgEventInfoR> itemList1 = select(where0: where1);
            if (0 == itemList1.Count)
            {
                return null;
            }
            else
            {
                return itemList1[0];
            }
        }

        public List<EpgEventInfoR> select(string where0 = "", string orderBy0 = "", bool ascending0 = true, int amount0 = 0)
        {
            string query1 = base.getQuery_Select(where0, orderBy0, ascending0, amount0);
            List<EpgEventInfoR> itemList1 = new List<EpgEventInfoR>();
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
                            EpgEventInfoR epgEventInfo1 = getEpgEventInfo(reader1);
                            itemList1.Add(epgEventInfo1);
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

        public static List<EpgContentData> getEpgContentData(SqlDataReader reader0, string column0)
        {
            List<EpgContentData> nibbleList1 = new List<EpgContentData>();
            byte[] nibbles1 = (byte[])reader0[column0];
            if (1 < nibbles1.Length)
            {
                for (int i1 = 0; i1 < nibbles1.Length; i1 += 4)
                {
                    nibbleList1.Add(
                         new EpgContentData()
                         {
                             content_nibble_level_1 = nibbles1[i1],
                             content_nibble_level_2 = nibbles1[i1 + 1],
                             user_nibble_1 = nibbles1[i1 + 2],
                             user_nibble_2 = nibbles1[i1 + 3]
                         });
                }
            }

            return nibbleList1;
        }

        public EpgEventInfoR getEpgEventInfo(SqlDataReader reader0)
        {
            EpgContentInfo epgContentInfo1 = new EpgContentInfo()
            {
                nibbleList = getEpgContentData(reader0, COLUMN_ContentInfo)
            };
            EpgEventInfoR epgEventInfoR1 = new EpgEventInfoR()
            {
                ID = (long)reader0[COLUMN_ID],
                lastUpdate = (DateTime)reader0[COLUMN_lastUpdate],
                original_network_id = (ushort)(int)reader0[COLUMN_original_network_id],
                transport_stream_id = (ushort)(int)reader0[COLUMN_transport_stream_id],
                service_id = (ushort)(int)reader0[COLUMN_service_id],
                event_id = (ushort)(int)reader0[COLUMN_event_id],
                StartTimeFlag = Convert.ToByte(reader0[COLUMN_StartTimeFlag]),
                start_time = (DateTime)reader0[COLUMN_start_time],
                DurationFlag = Convert.ToByte(reader0[COLUMN_DurationFlag]),
                durationSec = (uint)(long)reader0[COLUMN_durationSec],
                ShortInfo = new EpgShortEventInfo()
                {
                    event_name = (string)reader0[COLUMN_ShortInfo_event_name],
                    text_char = (string)reader0[COLUMN_ShortInfo_text_char]
                },
                ExtInfo = new EpgExtendedEventInfo()
                {
                    text_char = (string)reader0[COLUMN_ExtInfo_text_char]
                },
                ContentInfo = epgContentInfo1
            };

            return epgEventInfoR1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item0"></param>
        /// <returns>EpgEventInfoR.ID</returns>
        public long insert(EpgEventInfoR item0)
        {
            List<EpgEventInfoR> infoList1 = select(
               COLUMN_original_network_id + "=" + item0.original_network_id + " AND " + COLUMN_transport_stream_id + "=" + item0.transport_stream_id +
               " AND " + COLUMN_service_id + "=" + item0.service_id + " AND " + COLUMN_event_id + "=" + item0.event_id +
               " AND " + COLUMN_start_time + "=" + q(item0.start_time.ToString(startTimeStrFormat)));
            if (0 < infoList1.Count)
            {
                return infoList1[0].ID;
            }
            else
            {
                if (item0.ID < 0)
                {
                    item0.ID = getId();
                }
                Dictionary<string, string> keyValueDict1 = getFieldNameValues(item0);
                keyValueDict1.Add(COLUMN_ID, item0.ID.ToString());
                base.insert(keyValueDict1);

                return item0.ID;
            }
        }

        public int update(EpgEventInfoR item0)
        {
            Dictionary<string, string> keyValueDict1 = getFieldNameValues(item0);
            string where1 = COLUMN_ID + "=" + item0.ID;

            return base.update(keyValueDict1, where1);
        }

        public int delete(long epgEventInfoID0)
        {
            string where1 = COLUMN_ID + "=" + epgEventInfoID0;

            return base.delete(where1);
        }

        public int delete(long[] epgEventInfoIDs0)
        {
            StringBuilder where1 = new StringBuilder();
            foreach (var item in epgEventInfoIDs0)
            {
                if (0 < where1.Length)
                {
                    where1.Append(" OR ");
                }
                where1.Append(COLUMN_ID + "=" + item);
            }

            return base.delete(where1);
        }

        Dictionary<string, string> getFieldNameValues(EpgEventInfoR item0)
        {
            string shortInfo_event_name1 = string.Empty;
            string shortInfo_text_char1 = string.Empty;
            if (item0.ShortInfo != null)
            {
                shortInfo_event_name1 = item0.ShortInfo.event_name;
                shortInfo_text_char1 = item0.ShortInfo.text_char;
            }
            string extInfo_text_char1 = string.Empty;
            if (item0.ExtInfo != null)
            {
                extInfo_text_char1 = item0.ExtInfo.text_char;
            }
            Dictionary<string, string> dict1 = new Dictionary<string, string>() {
                { COLUMN_lastUpdate, q(item0.lastUpdate.ToString(timeStampStrFormat)) },
                { COLUMN_original_network_id, item0.original_network_id.ToString() },
                { COLUMN_transport_stream_id, item0.transport_stream_id.ToString() },
                { COLUMN_service_id, item0.service_id.ToString() },
                { COLUMN_event_id, item0.event_id.ToString() },
                { COLUMN_StartTimeFlag, "0x" +  item0.StartTimeFlag.ToString("X") },
                { COLUMN_DurationFlag, "0x"+  item0.DurationFlag.ToString("X") },
                { COLUMN_durationSec, item0.durationSec.ToString() },
                { COLUMN_ShortInfo_event_name, base.createTextValue(shortInfo_event_name1) },
                { COLUMN_ShortInfo_text_char, base.createTextValue(shortInfo_text_char1) },
                { COLUMN_ExtInfo_text_char, base.createTextValue(extInfo_text_char1) }
            };
            if (item0.start_time < base.minValue_SmallDateTime)
            {
                dict1.Add(COLUMN_start_time, q(base.minValue_SmallDateTime.ToString(startTimeStrFormat)));
            }
            else
            {
                dict1.Add(COLUMN_start_time, q(item0.start_time.ToString(startTimeStrFormat)));
            }
            if (item0.ContentInfo != null)
            {
                addEpgContentData(ref dict1, item0.ContentInfo.nibbleList);
            }

            return dict1;
        }

        public static void addEpgContentData(ref Dictionary<string, string> dict0, List<EpgContentData> ecdList0)
        {
            if (ecdList0.Count == 0) { return; }
            //
            StringBuilder contentInfo1 = new StringBuilder();
            foreach (EpgContentData epgContentData1 in ecdList0)
            {
                if (contentInfo1.Length == 0)
                {
                    contentInfo1.Append("0x");
                }
                contentInfo1.Append(epgContentData1.content_nibble_level_1.ToString("X2"));
                contentInfo1.Append(epgContentData1.content_nibble_level_2.ToString("X2"));
                contentInfo1.Append(epgContentData1.user_nibble_1.ToString("X2"));
                contentInfo1.Append(epgContentData1.user_nibble_2.ToString("X2"));
            }
            if (contentInfo1.Length == 0)
            {
                contentInfo1.Append(0);
            }
            dict0.Add(COLUMN_ContentInfo, contentInfo1.ToString());
        }

        long getId()
        {
            if (_id < 0)
            {
                List<EpgEventInfoR> epgEventInfoList1 = select(orderBy0: COLUMN_ID, ascending0: false, amount0: 1);
                if (0 < epgEventInfoList1.Count)
                {
                    _id = epgEventInfoList1[0].ID;
                }
            }

            return System.Threading.Interlocked.Increment(ref _id);
        }

        public void createTable()
        {
            //epgサンプル数: 37073
            //MAX_ShortInfo_event_name.Length: 62
            //MAX_ShortInfo_text_char.Length: 130
            //MAX_ExtInfo_text_char.Length: 1449
            // Max_ContentInfo_nibbleList_Count1: 5 * 4 = 20 byte

            //epg_Sample: 32352
            //Max_ShortInfo_event_name.Length: 62
            //Max_ShortInfo_text_char.Length: 123
            //Max_ExtInfo_text_char.Length: 1893
            //Max_ContentInfo_nibbleList_Count1: 4

            string query1 = "CREATE TABLE [dbo].[" + TABLE_NAME + "](" +
                    "[" + COLUMN_ID + "] [bigint] NOT NULL," +
                    "[" + COLUMN_lastUpdate + "] [datetime] NOT NULL," +
                    "[" + COLUMN_original_network_id + "] [int] NOT NULL," +
                    "[" + COLUMN_transport_stream_id + "] [int] NOT NULL," +
                    "[" + COLUMN_service_id + "] [int] NOT NULL," +
                    "[" + COLUMN_event_id + "] [int] NOT NULL," +
                    "[" + COLUMN_StartTimeFlag + "] [bit] NOT NULL," +
                    "[" + COLUMN_start_time + "] [smalldatetime] NOT NULL," +
                    "[" + COLUMN_DurationFlag + "] [bit] NOT NULL," +
                    "[" + COLUMN_durationSec + "] [bigint] NOT NULL," +
                    "[" + COLUMN_ShortInfo_event_name + "] [nvarchar](100) NOT NULL," +
                    "[" + COLUMN_ShortInfo_text_char + "] [nvarchar](200) NOT NULL," +
                    "[" + COLUMN_ExtInfo_text_char + "] [nvarchar](2000) NOT NULL," +
                    "[" + COLUMN_ContentInfo + "] [varbinary](20) NOT NULL," +
                    "CONSTRAINT [PK_EpgEventInfo] PRIMARY KEY CLUSTERED ([" + COLUMN_ID + "] ASC))";

            string query2 = "CREATE FULLTEXT INDEX ON " + TABLE_NAME + "(" +
                COLUMN_ShortInfo_event_name + " Language 1041," +
                COLUMN_ShortInfo_text_char + " Language 1041," +
                COLUMN_ExtInfo_text_char + " Language 1041" +
                ") KEY INDEX PK_EpgEventInfo ON " + FULLTEXTCATALOG + ";";

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
                " CREATE INDEX " + INDEX_NAME + " ON [dbo].[" + TABLE_NAME + "](" +
                COLUMN_start_time + ", " + COLUMN_original_network_id + ", " + COLUMN_transport_stream_id + ", " + COLUMN_service_id + ", " + COLUMN_event_id + ")";
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

        #region - Property -
        #endregion

        protected override string tableName
        {
            get { return TABLE_NAME; }
        }

        #region - Event Handler -
        #endregion

    }

    /// <summary>
    /// DBレコード
    /// </summary>
    public class EpgEventInfoR : EpgEventInfo
    {

        #region - Constructor -
        #endregion

        public EpgEventInfoR()
        {
            ShortInfo = new EpgShortEventInfo();
            ExtInfo = new EpgExtendedEventInfo();
            ContentInfo = new EpgContentInfo();
        }

        public EpgEventInfoR(RecFileInfo recFileInfo0, DateTime lastUpdate0)
        {
            base.original_network_id = recFileInfo0.OriginalNetworkID;
            base.transport_stream_id = recFileInfo0.TransportStreamID;
            base.service_id = recFileInfo0.ServiceID;
            base.event_id = recFileInfo0.EventID;
            base.start_time = recFileInfo0.StartTime;
            base.durationSec = recFileInfo0.DurationSecond;
            base.ShortInfo = new EpgShortEventInfo()
            {
                event_name = recFileInfo0.Title
            };

            lastUpdate = lastUpdate0;
        }

        public EpgEventInfoR(EpgEventInfo epgEventInfo0, DateTime lastUpdate0)
        {
            if (epgEventInfo0 != null)
            {
                base.original_network_id = epgEventInfo0.original_network_id;
                base.transport_stream_id = epgEventInfo0.transport_stream_id;
                base.service_id = epgEventInfo0.service_id;
                base.event_id = epgEventInfo0.event_id;
                base.StartTimeFlag = epgEventInfo0.StartTimeFlag;
                base.start_time = epgEventInfo0.start_time;
                base.DurationFlag = epgEventInfo0.DurationFlag;
                base.durationSec = epgEventInfo0.durationSec;
                base.ShortInfo = epgEventInfo0.ShortInfo;
                base.ExtInfo = epgEventInfo0.ExtInfo;
                base.ContentInfo = epgEventInfo0.ContentInfo;
            }

            lastUpdate = lastUpdate0;
        }

        #region - Method -
        #endregion

        #region - Property -
        #endregion

        public long ID
        {
            get { return _ID; }
            set { _ID = value; }
        }
        long _ID = -1;

        public DateTime lastUpdate { get; set; }

        #region - Event Handler -
        #endregion

    }

}
