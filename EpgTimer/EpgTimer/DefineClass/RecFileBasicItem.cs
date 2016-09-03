using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EpgTimer
{
    public class RecFileBasicItem
    {
        private RecFileBasicInfo data;

        public RecFileBasicItem(RecFileBasicInfo data)
        {
            this.data = data;
        }

        public String EventName
        {
            get { return data.Title; }
        }
        public String StartTime
        {
            get
            {
                return data.StartTime.ToString("yyyy/MM/dd(ddd) HH:mm:ss");
            }
        }
        public TimeSpan Duration
        {
            get
            {
                return TimeSpan.FromSeconds(data.DurationSecond);
            }
        }
        public String RecFilePath
        {
            get { return data.RecFilePath; }
        }
    }
}
