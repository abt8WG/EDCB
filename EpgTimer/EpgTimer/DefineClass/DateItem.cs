﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EpgTimer
{
    public class DateItem
    {
        public DateItem()
        {
        }
        public EpgSearchDateInfo DateInfo
        {
            get;
            set;
        }
        public String ViewText
        {
            get;
            set;
        }
        public override string ToString()
        {
            return ViewText;
        }
        public override bool Equals(object obj)
        {
            return DateInfo.Equals(((DateItem)obj).DateInfo);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
