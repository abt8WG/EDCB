using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace EpgTimer
{
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IXmlSerializable
    {
        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            bool isEmpty = reader.IsEmptyElement;
            reader.Read();
            if (isEmpty) return;

            XmlSerializer serializer = new XmlSerializer(typeof(Pair));
            while (reader.NodeType != XmlNodeType.EndElement)
            {
                Pair entry = serializer.Deserialize(reader) as Pair;
                if (entry != null)
                {
                    Add(entry.key, entry.value);
                }
            }

            reader.ReadEndElement();
        }

        public void WriteXml(XmlWriter writer)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Pair));
            foreach(var entry in this)
            {
                serializer.Serialize(writer, new Pair(entry.Key, entry.Value));
            }
        }

        public class Pair
        {
            public TKey key;
            public TValue value;

            public Pair() { }

            public Pair(TKey key, TValue value)
            {
                this.key = key;
                this.value = value;
            }
        }
    }
}
