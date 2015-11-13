﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EpgTimer
{
    public class CustomEpgTabInfo : IEquatable<CustomEpgTabInfo>
    {
        private static int count = 0;
        private int uniqId;

        public CustomEpgTabInfo()
        {
            ViewServiceList = new List<UInt64>();
            ViewContentKindList = new List<UInt16>();
            ViewMode = 0;
            NeedTimeOnlyBasic = false;
            NeedTimeOnlyWeek = false;
            StartTimeWeek = 4;
            SearchMode = false;
            SearchKey = new EpgSearchKeyInfo();
            FilterEnded = false;
            uniqId = ++count;
        }
        public String TabName
        {
            get;
            set;
        }
        public int ViewMode
        {
            get;
            set;
        }
        public bool NeedTimeOnlyBasic
        {
            get;
            set;
        }
        public bool NeedTimeOnlyWeek
        {
            get;
            set;
        }
        public int StartTimeWeek
        {
            get;
            set;
        }
        public List<UInt64> ViewServiceList
        {
            get;
            set;
        }
        public List<UInt16> ViewContentKindList
        {
            get;
            set;
        }
        public bool SearchMode
        {
            get;
            set;
        }
        public EpgSearchKeyInfo SearchKey
        {
            get;
            set;
        }
        public bool FilterEnded
        {
            get;
            set;
        }

        public void CopyTo(ref CustomEpgTabInfo dest)
        {
            dest.TabName = TabName;
            dest.ViewMode = ViewMode;
            dest.NeedTimeOnlyBasic = NeedTimeOnlyBasic;
            dest.NeedTimeOnlyWeek = NeedTimeOnlyWeek;
            dest.StartTimeWeek = StartTimeWeek;
            dest.ViewServiceList = ViewServiceList.ToList();
            dest.ViewContentKindList = ViewContentKindList.ToList();
            dest.SearchMode = SearchMode;
            dest.FilterEnded = FilterEnded;

            SearchKey.CopyTo(dest.SearchKey);

            dest.uniqId = uniqId;
        }
        public override string ToString()
        {
            return TabName;
        }
        public bool Equals(CustomEpgTabInfo other)
        {
            return (uniqId == other.uniqId);
        }
    }
}
