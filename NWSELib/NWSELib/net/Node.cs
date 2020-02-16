using NWSELib.common;
using NWSELib.genome;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NWSELib.net
{
    /// <summary>
    /// 网络节点
    /// </summary>
    public abstract class Node
    {
        #region 基本信息
        /// <summary>所属网络</summary>
        public Network net;

        /// <summary>节点基因</summary>
        protected NodeGene gene;

        /// <summary>节点Id</summary>
        public int Id { get => this.gene.Id;}
        /// <summary>
        /// 名称
        /// </summary>
        public String Name
        {
            get => gene.Name;
        }
        /// <summary>
        /// 类别
        /// </summary>
        public string Cataory
        {
            get => gene.Cataory;
            set => gene.Cataory = value;
        }
        /// <summary>
        /// 分组
        /// </summary>
        public String Group
        {
            get => gene.Group;
        }
        /// <summary>
        /// 基因
        /// </summary>
        public NodeGene Gene
        {
            get => this.gene;
        }
        /// <summary>
        /// 显示
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return this.Gene.Text;
        }
        /// <summary>
        /// 取得输入节点
        /// </summary>
        /// <param name="net"></param>
        /// <returns></returns>
        public virtual List<Node> getInputNodes(Network net)
        {
            return new List<Node>();
        }

        /// <summary>
        /// 短时记忆时间容量
        /// </summary>
        public int TimeCapacity
        {
            get => Session.GetConfiguration().agent.shorttermcapacity;
        }
        /// <summary>
        /// 维度
        /// </summary>
        public int Dimension
        {
            get => this.gene.Dimension;
        }

        
        #endregion

        

        

        #region 状态信息
        /// <summary>
        /// 值
        /// </summary>
        protected readonly List<Vector> values = new List<Vector>();
        /// <summary>
        /// 时间
        /// </summary>
        protected readonly List<int> times = new List<int>();
        /// <summary>
        /// 起始时间
        /// </summary>
        public int BeginTime
        {
            get => (times == null || times.Count<=0)?0: times[0];
        }
        /// <summary>
        /// 最新时间
        /// </summary>
        public int CurTime
        {
            get { return times.Count<=0?-1:times[times.Count - 1]; }
        }
        /// <summary>
        /// 取得给定值的文本显示信息
        /// </summary>
        /// <param name="value">为空，则取当前最新值</param>
        /// <returns></returns>
        public virtual String getValueText(Vector value=null) 
        { 
            throw new NotImplementedException(); 
        }
        
        public virtual Vector Value
        {
            get { return values.Count<=0?null:values.ToArray()[values.Count - 1]; }
        }

        public virtual List<Vector> ValueList
        {
            get => new List<Vector>(this.values);
        }

        public virtual List<Vector> GetValues(int new_time, int count)
        {
            List<int> ts = this.times.ToList();
            int tindex = times.IndexOf(new_time);
            if (tindex < 0) return null;

            int tstart = tindex - count + 1;
            if (tstart < 0) tstart = 0;
            if (count > values.Count - tstart)
                count = values.Count - tstart;
            return values.GetRange(tstart, count);
            
        }

        public virtual Vector GetValue(int time, int backIndex)
        {
            int tindex = times.IndexOf(time);
            if (tindex < 0) return null;
            if (tindex - backIndex < 0) return null;
            return this.ValueList[tindex - backIndex];
        }
        public virtual Vector GetValue(int time)
        {
            int tindex = times.IndexOf(time);
            if (tindex < 0) return null;
            return this.values[tindex];
        }

        #endregion

        #region 缓存和初始化

        public readonly List<Receptor> LeafReceptors;

        
        public Node(NodeGene gene,Network net)
        {
            this.gene = gene;
            this.net = net;
            LeafReceptors = this.gene.getLeafGenes().ConvertAll(g => (Receptor)net[g.Id]);
        }

        
        #endregion

        #region 评价信息
        
        /// <summary>
        /// 可靠度
        /// </summary>
        public virtual double Reability { get => 0.0; }
       
        
        #endregion

        #region 激活与重置
        /// <summary>
        /// 设置当前值
        /// </summary>
        /// <param name="value"></param>
        /// <param name="time"></param>
        /// <returns></returns>
        public virtual Object activate(Network net, int time, Object value = null)
        {
            Object oldValue = putTimeAndValue(times, values, time, value);

            while(values.Count>Session.GetConfiguration().agent.shorttermcapacity)
            {
                this.values.RemoveAt(0);
                this.times.RemoveAt(0);
            }

            return oldValue;
        }
        /// <summary>
        /// 重置计算
        /// </summary>
        public void Reset()
        {
            this.times.Clear();
            this.values.Clear();
        }
        /// <summary>
        /// 是否已经完成过计算
        /// </summary>
        /// <returns></returns>
        public bool IsActivate(int time)
        {
            return this.times.Contains(time);
        }
        
        #endregion

        #region 想象信息（用于推理）
        private List<int> vtimes = new List<int>();
        private List<Vector> vvalues = new List<Vector>();

        public void think_reset()
        {
            vtimes.Clear();
            vvalues.Clear();
        }
        public void think(Network net, int time, Vector value)
        {
            putTimeAndValue(vtimes, vvalues, time, value);
        }

        public bool IsThinkCompleted(int time)
        {
            if(this.vtimes.Contains(time))return true;
            return this.times.Contains(time);
        }

        public Vector getThinkValues(int time)
        {
            int index = this.vtimes.IndexOf(time);
            if (index >= 0) return vvalues[index];
            return this.GetValue(time);
        }

        private static Object putTimeAndValue(List<int> times,List<Vector> values,int time,Object value)
        {
            if (value != null && value is double) value = new Vector(new double[] { (double)value });

            if (times.Count <= 0)
            {
                times.Add(time);
                values.Add((Vector)value);
                return null;
            }

            Object prev = null;
            int index = times.IndexOf(time);
            if (index >= 0)
            {
                times[index] = time;
                prev = values[index];
                values[index] = (Vector)value;
                return prev;

            }
            int lastime = times.Last();
            if (time > lastime)
            {
                times.Add(time);
                if (value is List<Vector>) value = ((List<Vector>)value).flatten();
                values.Add((Vector)value);
                return null;
            }
            else
            {
                index = 0;
                while (times[index++] < time) ;
                times.Insert(index - 1, time);
                values.Insert(index - 1, (Vector)value);
            }

            

            return prev;
        }
        #endregion
    }
}
