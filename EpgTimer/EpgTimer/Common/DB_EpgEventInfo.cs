using EpgTimer.DefineClass;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace EpgTimer.Common
{
    public class DB_EpgEventInfo : DBBase<EpgEventInfoR>
    {

        public static Dictionary<string, DB> linkedTables = new Dictionary<string, DB>();

        public const string TABLE_NAME = "EpgEventInfo";
        /// <summary>
        /// 略記
        /// </summary>
        public const string TABLE_NAME_ABBR = "eei";
        public const string COLUMN_lastUpdate = "lastUpdate",
            COLUMN_original_network_id = "original_network_id", COLUMN_transport_stream_id = "transport_stream_id",
            COLUMN_service_id = "service_id", COLUMN_event_id = "event_id",
            COLUMN_StartTimeFlag = "StartTimeFlag", COLUMN_start_time = "start_time",
            COLUMN_DurationFlag = "DurationFlag", COLUMN_durationSec = "durationSec",
            COLUMN_ShortInfo_event_name = "ShortInfo_event_name", COLUMN_ShortInfo_text_char = "ShortInfo_text_char",
            COLUMN_ExtInfo_text_char = "ExtInfo_text_char",
            COLUMN_ContentInfo = "ContentInfo";
        const string INDEX_NAME = "UX_Epg";
        static long _id = 0;

        #region - Constructor -
        #endregion

        public DB_EpgEventInfo(DB linkedTable0)
        {
            if (!linkedTables.ContainsKey(linkedTable0.tableName))
            {
                linkedTables.Add(linkedTable0.tableName, linkedTable0);
            }
        }

        #region - Method -
        #endregion

        public static List<EpgContentData> getEpgContentData(SqlDataReader reader0, string column0)
        {
            List<EpgContentData> nibbleList1 = new List<EpgContentData>();
            byte[] bytes1 = (byte[])reader0[column0];
            if (4 <= bytes1.Length)
            {
                int i1 = 0;
                while (i1 < bytes1.Length)
                {
                    EpgContentData ecd1 = new EpgContentData()
                    {
                        content_nibble_level_1 = bytes1[i1++],
                        content_nibble_level_2 = bytes1[i1++],
                        user_nibble_1 = bytes1[i1++],
                        user_nibble_2 = bytes1[i1++]
                    };
                    if (ecd1.content_nibble_level_1 == 0 && ecd1.content_nibble_level_2 == 0 && ecd1.user_nibble_1 == 0 && ecd1.user_nibble_2 == 0)
                    {
                        continue;
                    }
                    else
                    {
                        nibbleList1.Add(ecd1);
                    }
                }
            }

            return nibbleList1;
        }

        public override EpgEventInfoR getItem(SqlDataReader reader0)
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

        protected override Dictionary<string, string> getFieldNameValues(EpgEventInfoR item0, bool withID0)
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
            Dictionary<string, string> dict1 = new Dictionary<string, string>();
            if (withID0)
            {
                dict1.Add(COLUMN_ID, item0.ID.ToString());
            }
            dict1.Add(COLUMN_lastUpdate, q(item0.lastUpdate.ToString(timeStampStrFormat)));
            dict1.Add(COLUMN_original_network_id, item0.original_network_id.ToString());
            dict1.Add(COLUMN_transport_stream_id, item0.transport_stream_id.ToString());
            dict1.Add(COLUMN_service_id, item0.service_id.ToString());
            dict1.Add(COLUMN_event_id, item0.event_id.ToString());
            dict1.Add(COLUMN_StartTimeFlag, "0x" + item0.StartTimeFlag.ToString("X"));
            dict1.Add(COLUMN_DurationFlag, "0x" + item0.DurationFlag.ToString("X"));
            dict1.Add(COLUMN_durationSec, item0.durationSec.ToString());
            dict1.Add(COLUMN_ShortInfo_event_name, base.createTextValue(shortInfo_event_name1));
            dict1.Add(COLUMN_ShortInfo_text_char, base.createTextValue(shortInfo_text_char1));
            dict1.Add(COLUMN_ExtInfo_text_char, base.createTextValue(extInfo_text_char1));
            if (item0.start_time < minValue_DateTime)
            {
                dict1.Add(COLUMN_start_time, q(minValue_DateTime.ToString(startTimeStrFormat)));
            }
            else
            {
                dict1.Add(COLUMN_start_time, q(item0.start_time.ToString(startTimeStrFormat)));
            }
            {
                StringBuilder sb1 = new StringBuilder();
                if (item0.ContentInfo != null)
                {
                    foreach (EpgContentData epgContentData1 in item0.ContentInfo.nibbleList)
                    {
                        if (sb1.Length == 0)
                        {
                            sb1.Append("0x");
                        }
                        sb1.Append(epgContentData1.content_nibble_level_1.ToString("X2"));
                        sb1.Append(epgContentData1.content_nibble_level_2.ToString("X2"));
                        sb1.Append(epgContentData1.user_nibble_1.ToString("X2"));
                        sb1.Append(epgContentData1.user_nibble_2.ToString("X2"));
                    }
                }
                if (sb1.Length == 0)
                {
                    sb1.Append(0);
                }
                dict1.Add(COLUMN_ContentInfo, sb1.ToString());
            }

            return dict1;
        }

        protected override long getId()
        {
            return base._getId(ref _id);
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
            base.createIndex(
                INDEX_NAME,
                new string[] {
                    COLUMN_start_time,
                    COLUMN_original_network_id,
                    COLUMN_transport_stream_id,
                    COLUMN_service_id,
                    COLUMN_event_id });
        }

        public override int delete(IEnumerable<long> ids0)
        {
            List<long> ids1 = new List<long>();
            foreach (long id1 in ids0)
            {
                bool isExist1 = false;
                foreach (DB db1 in linkedTables.Values)
                {
                    if (db1.exists(id1))
                    {
                        isExist1 = true;
                        break;
                    }
                }
                if (!isExist1)
                {
                    ids1.Add(id1);
                }
            }

            return base.delete(ids1.ToArray());
        }

        public long exists(EpgEventInfoR epgInfo0)
        {
            long? id1 = null;
            string query1 = "SELECT " + COLUMN_ID + " FROM " + tableName
                + " WHERE " + COLUMN_original_network_id + "=" + epgInfo0.original_network_id
                + " AND " + COLUMN_transport_stream_id + "=" + epgInfo0.transport_stream_id
                + " AND " + COLUMN_service_id + "=" + epgInfo0.service_id
                + " AND " + COLUMN_event_id + "=" + epgInfo0.event_id
                + " AND " + COLUMN_start_time + "= " + q(epgInfo0.start_time.ToString(startTimeStrFormat));
            try
            {
                using (SqlConnection sqlConn1 = new SqlConnection(sqlConnStr))
                {
                    sqlConn1.Open();
                    using (SqlCommand cmd1 = new SqlCommand(query1, sqlConn1))
                    {
                        id1 = cmd1.ExecuteScalar() as long?;
                    }
                }
            }
            catch (Exception ex0)
            {
                System.Diagnostics.Trace.WriteLine(ex0);
            }

            if (id1.HasValue)
            {
                return (long)id1;
            }
            else
            {
                return -1;
            }
        }

        #region - Property -
        #endregion

        public override string tableName
        {
            get { return TABLE_NAME; }
        }

        protected override bool isSetIdByManual
        {
            get { return true; }
        }

        #region - Event Handler -
        #endregion

    }

    /// <summary>
    /// DBレコード
    /// </summary>
    public class EpgEventInfoR : EpgEventInfo, IDBRecord
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

        public override bool Equals(object obj)
        {
            EpgEventInfo info0 = (EpgEventInfo)obj;
            return (original_network_id == info0.original_network_id
                && transport_stream_id == info0.transport_stream_id
                && service_id == info0.service_id
                && event_id == info0.event_id);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

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
