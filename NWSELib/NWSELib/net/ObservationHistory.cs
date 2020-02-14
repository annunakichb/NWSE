using NWSELib.common;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace NWSELib.net
{
    /// <summary>
    /// 观察空间
    /// </summary>
    public class ObservationHistory
    {
        public readonly List<Item> items = new List<Item>();
        
        public Item match(Network net,Vector vector,bool create)
        {
            foreach(Item item in items)
            {
                bool match = true;
                for (int i = 0; i < net.Receptors.Count; i++)
                {
                    if (net.Receptors[i].distance(item.obsValues[i], vector[i]) > 1) { match = false;break; }
                }
                if (match) return item;
            }
            if (!create) return null;
            Item newitem = new Item();
            newitem.obsValues = vector.clone();
            items.Add(newitem);
            return newitem;
        }
        

        public class Item
        {
            /// <summary>
            /// 观察值
            /// </summary>
            public Vector obsValues;
            /// <summary>
            /// 评估值
            /// </summary>
            public double evaluation; 

            public double distance(Network net,Vector vector)
            {
                List<double> ds = new List<double>();
                for (int i = 0; i < net.Receptors.Count; i++)
                {
                    ds.Add(net.Receptors[i].distance(obsValues[i], vector[i]));
                }
                return ds.Average();
            }
        }
    }
}
