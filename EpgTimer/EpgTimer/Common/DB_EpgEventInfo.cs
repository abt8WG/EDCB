using EpgTimer.DefineClass;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace EpgTimer.Common
{
    /// <summary>
    /// DB_EpgEventInfoへの参照を持つテーブルを管理
    /// </summary>
    public interface IDB_EpgEventInfo
    {
        List<long> exist(IEnumerable<long> ids0, string columnName0);
        string tableName { get; }
        string columnName_epgEventInfoID { get; }
    }

    public class DB_EpgEventInfo : DBBase<EpgEventInfoR>
    {
        /// <summary>
        /// key: table name
        /// </summary>
        public static Dictionary<string, IDB_EpgEventInfo> linkedTables = new Dictionary<string, IDB_EpgEventInfo>();

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

        #region - Constructor -
        #endregion

        public DB_EpgEventInfo() { }

        public DB_EpgEventInfo(IDB_EpgEventInfo linkedTable0)
        {
            if (!linkedTables.ContainsKey(linkedTable0.tableName))
            {
                linkedTables.Add(linkedTable0.tableName, linkedTable0);
            }
        }

        #region - Method -
        #endregion

        public override long insert(EpgEventInfoR item0, SqlCommand cmd1)
        {
            StringBuilder query1 = new StringBuilder();
            query1.AppendLine("DECLARE @id BIGINT");
            query1.AppendLine("SELECT @id = " +
                "(" +
                "SELECT " + COLUMN_ID + " FROM " + TABLE_NAME +
                " WHERE " + COLUMN_original_network_id + "=" + item0.original_network_id +
                 " AND " + COLUMN_transport_stream_id + "=" + item0.transport_stream_id +
                 " AND " + COLUMN_service_id + "=" + item0.service_id +
                 " AND " + COLUMN_event_id + "=" + item0.event_id +
                 " AND " + COLUMN_start_time + "= " + q(item0.start_time.ToString(startTimeStrFormat)) +
                 ")");
            query1.AppendLine("IF @id IS NULL");
            query1.AppendLine(getQuery_Insert(item0));
            query1.AppendLine("ELSE");
            query1.AppendLine("SELECT @id");
            cmd1.CommandText = query1.ToString();
            long id1 = (long)cmd1.ExecuteScalar();

            return id1;
        }

        /// <summary>
        /// epgが登録済みであれば、既存データのIDを返す
        /// </summary>
        /// <param name="item0"></param>
        /// <returns></returns>
        public override long insert(EpgEventInfoR item0)
        {
            long id1 = -1;

            StringBuilder query1 = new StringBuilder();
            query1.AppendLine("DECLARE @id BIGINT");
            query1.AppendLine("SELECT @id = " +
                "(" +
                "SELECT " + COLUMN_ID + " FROM " + TABLE_NAME +
                " WHERE " + COLUMN_original_network_id + "=" + item0.original_network_id +
                 " AND " + COLUMN_transport_stream_id + "=" + item0.transport_stream_id +
                 " AND " + COLUMN_service_id + "=" + item0.service_id +
                 " AND " + COLUMN_event_id + "=" + item0.event_id +
                 " AND " + COLUMN_start_time + "= " + q(item0.start_time.ToString(startTimeStrFormat)) +
                 ")");
            query1.AppendLine("IF @id IS NULL");
            query1.AppendLine(getQuery_Insert(item0));
            query1.AppendLine("ELSE");
            query1.AppendLine("SELECT @id");

            try
            {
                using (SqlConnection sqlConn1 = new SqlConnection(sqlConnStr))
        {
                    sqlConn1.Open();
                    using (SqlCommand cmd1 = new SqlCommand(query1.ToString(), sqlConn1))
            {
                        id1 = (long)cmd1.ExecuteScalar();
            }
                }
            }
            catch (Exception ex0)
            {
                System.Diagnostics.Trace.WriteLine(ex0);
            }

            return id1;
        }

        public static List<EpgContentData> getEpgContentData(SqlDataReader reader0, ref int i0)
        {
            List<EpgContentData> nibbleList1 = new List<EpgContentData>();
            byte[] bytes1 = (byte[])reader0[i0++];
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

        public override EpgEventInfoR getItem(SqlDataReader reader0, ref int i0)
            {
            EpgEventInfoR epgEventInfoR1 = new EpgEventInfoR()
            {
                ID = (long)reader0[i0++],
                lastUpdate = (DateTime)reader0[i0++],
                original_network_id = (ushort)(int)reader0[i0++],
                transport_stream_id = (ushort)(int)reader0[i0++],
                service_id = (ushort)(int)reader0[i0++],
                event_id = (ushort)(int)reader0[i0++],
                StartTimeFlag = Convert.ToByte(reader0[i0++]),
                start_time = (DateTime)reader0[i0++],
                DurationFlag = Convert.ToByte(reader0[i0++]),
                durationSec = (uint)(long)reader0[i0++],
                ShortInfo = new EpgShortEventInfo()
                {
                    event_name = (string)reader0[i0++],
                    text_char = (string)reader0[i0++]
                },
                ExtInfo = new EpgExtendedEventInfo()
                {
                    text_char = (string)reader0[i0++]
                },
                ContentInfo = new EpgContentInfo()
                {
                    nibbleList = getEpgContentData(reader0, ref i0)
                }
            };

            return epgEventInfoR1;
        }

        protected override Dictionary<string, string> getFieldNameValues(EpgEventInfoR item0)
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
            dict1.Add(COLUMN_lastUpdate, q(item0.lastUpdate.ToString(timeStampStrFormat)));
            dict1.Add(COLUMN_original_network_id, item0.original_network_id.ToString());
            dict1.Add(COLUMN_transport_stream_id, item0.transport_stream_id.ToString());
            dict1.Add(COLUMN_service_id, item0.service_id.ToString());
            dict1.Add(COLUMN_event_id, item0.event_id.ToString());
            dict1.Add(COLUMN_StartTimeFlag, "0x" + item0.StartTimeFlag.ToString("X"));
            dict1.Add(COLUMN_DurationFlag, "0x" + item0.DurationFlag.ToString("X"));
            dict1.Add(COLUMN_durationSec, item0.durationSec.ToString());
            dict1.Add(COLUMN_ShortInfo_event_name, createTextValue(shortInfo_event_name1));
            dict1.Add(COLUMN_ShortInfo_text_char, createTextValue(shortInfo_text_char1));
            dict1.Add(COLUMN_ExtInfo_text_char, createTextValue(extInfo_text_char1));
            if (item0.start_time < minValue_DateTime)
            {
                dict1.Add(COLUMN_start_time, q(minValue_DateTime.ToString(startTimeStrFormat)));
            }
            else
            {
                dict1.Add(COLUMN_start_time, q(item0.start_time.ToString(startTimeStrFormat)));
            }
            if (item0.ContentInfo != null)
            {
                addEpgContentData(ref dict1, COLUMN_ContentInfo, item0.ContentInfo.nibbleList);
            }
            else
            {
                dict1.Add(COLUMN_ContentInfo, "0");
            }

            return dict1;
        }

        public static void addEpgContentData(ref Dictionary<string, string> dict0, string column0, List<EpgContentData> ecdList0)
        {
            StringBuilder sb1 = new StringBuilder();
            foreach (EpgContentData epgContentData1 in ecdList0)
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
            if (sb1.Length == 0)
            {
                sb1.Append(0);
            }
            dict0.Add(column0, sb1.ToString());
        }

        public void alterTable_COLUMN_ExtInfo_text_char()
        {
            string query1 = "DROP FULLTEXT INDEX ON [dbo].[" + TABLE_NAME + "]";
            string query2 = "ALTER TABLE [dbo].[" + TABLE_NAME + "] ALTER COLUMN " + COLUMN_ExtInfo_text_char + " nvarchar(3000) NOT NULL";
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

            createFulltext();
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
            //Max_ExtInfo_text_char.Length: 2503
            //Max_ContentInfo_nibbleList_Count1: 4

            string query1 = "CREATE TABLE [dbo].[" + TABLE_NAME + "](" +
                    //"[" + COLUMN_ID + "] [bigint] NOT NULL," +
                    "[" + COLUMN_ID + "] [bigint] IDENTITY(1,1) NOT NULL," +
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
                    "[" + COLUMN_ExtInfo_text_char + "] [nvarchar](3000) NOT NULL," +
                    "[" + COLUMN_ContentInfo + "] [varbinary](20) NOT NULL," +
                    "CONSTRAINT [PK_EpgEventInfo] PRIMARY KEY CLUSTERED ([" + COLUMN_ID + "] ASC))";

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

            createFulltext();
        }

        void createFulltext()
        {
            string query1 = "CREATE FULLTEXT INDEX ON " + TABLE_NAME + "(" +
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

        /// <summary>
        /// 参照されていなければ削除
        /// </summary>
        /// <param name="ids0"></param>
        /// <returns></returns>
        public override int delete(IEnumerable<long> ids0)
        {
            List<long> idList1 = new List<long>();
                foreach (IDB_EpgEventInfo db1 in linkedTables.Values)
                {
                idList1.AddRange(
                    db1.exist(ids0, db1.columnName_epgEventInfoID));
                    }
            List<long> ids2del1 = new List<long>();
            foreach (var id1 in ids0)
            {
                if (!idList1.Contains(id1))
                {
                    ids2del1.Add(id1);
                }
            }

            return base.delete(ids2del1.ToArray());
        }

        public bool alterTalbe_SetIdentity()
        {
            try
            {
                using (SqlConnection connection1 = new SqlConnection(sqlConnStr))
                    {
                    connection1.Open();

                    bool isSetIdentity1 = false;
                    string query1 = "SELECT COLUMNPROPERTY(OBJECT_ID('" + TABLE_NAME + "'),'" + COLUMN_ID + "','IsIdentity')";
                    using (SqlCommand cmd1 = new SqlCommand(query1, connection1))
                    {
                        isSetIdentity1 = ((int)cmd1.ExecuteScalar() == 0);
                    }
                    if (!isSetIdentity1)
                    {
                        return false;   // 設定済み
                    }
                    //
                    //
                    //
                    using (SqlCommand cmd2 = connection1.CreateCommand())
                    {
                        cmd2.CommandText = @"
BEGIN TRANSACTION
SET QUOTED_IDENTIFIER ON
SET ARITHABORT ON
SET NUMERIC_ROUNDABORT OFF
SET CONCAT_NULL_YIELDS_NULL ON
SET ANSI_NULLS ON
SET ANSI_PADDING ON
SET ANSI_WARNINGS ON
COMMIT
BEGIN TRANSACTION";
                        cmd2.ExecuteNonQuery();

                        cmd2.CommandText = @"
CREATE TABLE dbo.Tmp_EpgEventInfo
    (
    ID bigint NOT NULL IDENTITY (1, 1),
    lastUpdate datetime NOT NULL,
    original_network_id int NOT NULL,
    transport_stream_id int NOT NULL,
    service_id int NOT NULL,
    event_id int NOT NULL,
    StartTimeFlag bit NOT NULL,
    start_time smalldatetime NOT NULL,
    DurationFlag bit NOT NULL,
    durationSec bigint NOT NULL,
    ShortInfo_event_name nvarchar(100) NOT NULL,
    ShortInfo_text_char nvarchar(200) NOT NULL,
    ExtInfo_text_char nvarchar(3000) NOT NULL,
    ContentInfo varbinary(20) NOT NULL
    )  ON [PRIMARY]";
                        cmd2.ExecuteNonQuery();

                        cmd2.CommandText = @"
ALTER TABLE dbo.Tmp_EpgEventInfo SET (LOCK_ESCALATION = TABLE)";
                        cmd2.ExecuteNonQuery();

                        cmd2.CommandText = @"
SET IDENTITY_INSERT dbo.Tmp_EpgEventInfo ON";
                        cmd2.ExecuteNonQuery();

                        cmd2.CommandText = @"
IF EXISTS(SELECT * FROM dbo.EpgEventInfo)
     EXEC('INSERT INTO dbo.Tmp_EpgEventInfo (ID, lastUpdate, original_network_id, transport_stream_id, service_id, event_id, StartTimeFlag, start_time, DurationFlag, durationSec, ShortInfo_event_name, ShortInfo_text_char, ExtInfo_text_char, ContentInfo)
        SELECT ID, lastUpdate, original_network_id, transport_stream_id, service_id, event_id, StartTimeFlag, start_time, DurationFlag, durationSec, ShortInfo_event_name, ShortInfo_text_char, ExtInfo_text_char, ContentInfo FROM dbo.EpgEventInfo WITH (HOLDLOCK TABLOCKX)')
";
                        cmd2.ExecuteNonQuery();

                        cmd2.CommandText = @"
SET IDENTITY_INSERT dbo.Tmp_EpgEventInfo OFF";
                        cmd2.ExecuteNonQuery();

                        cmd2.CommandText = @"
DROP TABLE dbo.EpgEventInfo";
                        cmd2.ExecuteNonQuery();

                        cmd2.CommandText = @"
EXECUTE sp_rename N'dbo.Tmp_EpgEventInfo', N'EpgEventInfo', 'OBJECT' ";
                        cmd2.ExecuteNonQuery();

                        cmd2.CommandText = @"
ALTER TABLE dbo.EpgEventInfo ADD CONSTRAINT
    PK_EpgEventInfo PRIMARY KEY CLUSTERED 
    (
    ID
    ) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]";
                        cmd2.ExecuteNonQuery();

                        cmd2.CommandText = @"
COMMIT
CREATE FULLTEXT INDEX ON dbo.EpgEventInfo
( 
    ShortInfo_event_name LANGUAGE 1041, 
    ShortInfo_text_char LANGUAGE 1041, 
    ExtInfo_text_char LANGUAGE 1041
 )
KEY INDEX PK_EpgEventInfo
ON epg_catalog
 WITH  CHANGE_TRACKING  AUTO ";
                        cmd2.ExecuteNonQuery();

                        cmd2.CommandText = @"
ALTER FULLTEXT INDEX ON dbo.EpgEventInfo
ENABLE";
                        cmd2.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex);
            }

            return true;
        }

        #region - Property -
        #endregion

        public override string tableName
        {
            get { return TABLE_NAME; }
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

        public void reserveAdd()
        {
            CommonManager.Instance.MUtil.ReserveAdd(
                new List<EpgEventInfo>() { this },
                null);
        }

        public void reserveChangeOnOff()
        {
            ReserveData reserveData1 = getReserveData();
            CommonManager.Instance.MUtil.ReserveChangeOnOff(
                new List<ReserveData>() { reserveData1 });
        }

        public void openEpgReserveDialog(Control owner0)
        {
            CommonManager.Instance.MUtil.OpenEpgReserveDialog(this, owner0, 1);
        }

        public void openChgReserveDialog(Control owner0)
        {
            ReserveData reserveData1 = getReserveData();
            CommonManager.Instance.MUtil.OpenChgReserveDialog(reserveData1, owner0, 1);
        }

        ReserveData getReserveData()
        {
            var query1 = CommonManager.Instance.DB.ReserveList.Values.Where(
                x1 =>
                {
                    return (transport_stream_id == x1.TransportStreamID
                    && original_network_id == x1.OriginalNetworkID
                    && service_id == x1.ServiceID
                    && event_id == x1.EventID);
                });
            foreach (ReserveData reserveData1 in query1)
            {
                return reserveData1;
            }

            return null;
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
