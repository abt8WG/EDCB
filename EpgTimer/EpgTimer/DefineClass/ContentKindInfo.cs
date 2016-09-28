﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EpgTimer
{
    public class ContentKindInfo
    {
        public ContentKindInfo()
        {
        }
        public ContentKindInfo(String contentName, String subName, Byte nibble1, Byte nibble2)
        {
            this.ContentName = contentName;
            this.SubName = subName;
            this.Nibble1 = nibble1;
            this.Nibble2 = nibble2;
            //「その他」をラストへ。CSジャンル仮対応用
            //this.SortKey = (Int32)(((UInt16)(nibble1 == 0x0F ? 0xF0 : nibble1)) << 8 | ((UInt16)nibble2 + 1) & 0x00FF);
        }
        public UInt16 ID
        {
            get
            {
                return (UInt16)(Nibble1 << 8 | Nibble2);
            }
        }
        //public Int32 SortKey
        //{
        //    get;
        //    set;
        //}
        public String ContentName
        {
            get;
            set;
        }
        public String SubName
        {
            get;
            set;
        }
        public Byte Nibble1
        {
            get;
            set;
        }
        public Byte Nibble2
        {
            get;
            set;
        }
        public String ListBoxView
        {
            get
            {
                return ContentName + (SubName == "" ? "" : " - " + SubName); 
            }
        }
        public override string ToString()
        {
            if (Nibble2 == 0xFF)
            {
                return ContentName;
            }
            else
            {
                return "  " + SubName;
            }
        }
    }
}
