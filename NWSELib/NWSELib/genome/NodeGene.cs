using System;
using System.Collections.Generic;
using System.Linq;

namespace NWSELib.genome
{
    public abstract class NodeGene : IGene,IComparable<NodeGene>
    {
        
        #region 基本信息
        public NWSEGenome owner;
        /// <summary>
        /// id
        /// </summary>
        protected int id;

        /// <summary>
        /// 名称
        /// </summary>
        protected String name;

        /// <summary>生成的进化年代</summary>
        protected int generation;


        /// <summary>
        /// 所属类别
        /// </summary>
        protected string cataory;

        /// <summary>
        /// 所属分组
        /// </summary>
        protected string group;

        /// <summary>
        /// 深度
        /// </summary>
        protected int depth;

        /// <summary>
        /// 随机数生成器
        /// </summary>
        public static Random rng = new Random();

        /// <summary>
        /// id值
        /// </summary>
        public int Id { get => id; set => id = value; }
        /// <summary>
        /// 对应感受器名称
        /// </summary>
        public string Name { get => name; set => name = value; }
        /// <summary>
        /// 生成的进化年代
        /// </summary>
        public int Generation { get => generation; set => generation = value; }

        /// <summary>
        /// 对应感受器名称
        /// </summary>
        public string Cataory { get => cataory; set => cataory = value; }

        /// <summary>
        /// 所属分组
        /// </summary>
        public String Group
        {
            get => this.group;
            set => this.group = value;
        }
        /// <summary>
        /// 深度
        /// </summary>
        public int Depth { get => depth; set => depth = value; }
        /// <summary>
        /// 显示文本
        /// </summary>
        public virtual String Text { get => Name; } 
        

        /// <summary>
        /// 其各个子节点的维度，没有子节点返回空列表
        /// </summary>
        public abstract List<int> Dimensions { get; }
        /// <summary>
        /// 当前节点数据的总维度
        /// </summary>
        public virtual int Dimension { get => Dimensions.Sum()<=0?1: Dimensions.Sum(); }
        /// <summary>
        /// 取得输入基因
        /// </summary>
        /// <returns></returns>
        public abstract List<NodeGene> getInputGenes();
        /// <summary>
        /// 取得输入基因树上的所有基因
        /// </summary>
        /// <returns></returns>
        public List<NodeGene> getUpstreamGenes()
        {
            return getUpstreamGenes(this, null);
        }
        private List<NodeGene> getUpstreamGenes(NodeGene gene, List<NodeGene> upstreams)
        {
            if (upstreams == null) upstreams = new List<NodeGene>();
            if (gene == null) return upstreams;
            if(!upstreams.Contains(gene))
                upstreams.Add(gene);

            List<NodeGene> inputs = gene.getInputGenes();
            if (inputs == null || inputs.Count <= 0) return upstreams;

            foreach(NodeGene g in inputs)
            {
                upstreams = getUpstreamGenes(g,upstreams);
            }
            return upstreams;
        }

        private List<ReceptorGene> _cachedLeafGenes = null;
        /// <summary>
        /// 取得基因树上的叶子基因
        /// </summary>
        /// <returns></returns>
        public virtual List<ReceptorGene> getLeafGenes()
        {
            if (_cachedLeafGenes != null) return _cachedLeafGenes;
            List<ReceptorGene> r = new List<ReceptorGene>();
            if (this is ReceptorGene)
            {
                r.Add((ReceptorGene)this);return r;
            }
            return _cachedLeafGenes = this.getLeafGenes(this, r);
        }
        private List<ReceptorGene> getLeafGenes(NodeGene g,List<ReceptorGene> r)
        {
            if (g == null) return r;
            if (g is ReceptorGene) { r.Add((ReceptorGene)g);return r; }
            List<NodeGene> inputs = this.getInputGenes();
            if (inputs == null || inputs.Count <= 0) return r;
            for(int i=0;i<inputs.Count;i++)
            {
                r = getLeafGenes(inputs[i], r);
            }
            return r;
        }
        #endregion

        #region 性质

        /// <summary>
        /// 是否动作感知
        /// </summary>
        /// <returns></returns>
        public bool IsActionSensor()
        {
            return this is ReceptorGene && this.Group.StartsWith("action");
        }
        /// <summary>
        /// 是否环境感知
        /// </summary>
        /// <returns></returns>
        public bool IsEnvSensor()
        {
            return this is ReceptorGene && this.Group.StartsWith("env");
        }
        /// <summary>
        /// 是否姿态感知
        /// </summary>
        /// <returns></returns>
        public bool IsGestureSensor()
        {
            return this is ReceptorGene && this.Group.StartsWith("body");
        }
        /// <summary>
        /// 是否根环境相关
        /// </summary>
        /// <returns></returns>
        public bool hasEnvDenpend()
        {
            return hasEnvDenpend(this);
        }
        /// <summary>
        /// 递归判断是否根环境相关，即其子节点树中有一个是环境感知
        /// </summary>
        /// <param name="gene"></param>
        /// <returns></returns>
        private bool hasEnvDenpend(NodeGene gene)
        {
            
            List<NodeGene> inputs = this.owner.getInputs(gene);
            if (inputs == null || inputs.Count <= 0)
            {
                return (gene.group.StartsWith("env"));
            }
            foreach(NodeGene g in inputs)
            {
                if (hasEnvDenpend(g)) return true;
            }
            return false;
        }

        
        
        #endregion

       

        #region 统计信息       
        /// <summary>
        /// 使用次数
        /// </summary>
        protected int usedCount;
        /// <summary>
        /// 使用次数
        /// </summary>
        public int UsedCount
        {
            get => usedCount; set => usedCount = value;
        }
        
        
        #endregion

        #region 初始化
        /// <summary>
        /// 克隆
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public abstract T clone<T>() where T : NodeGene;
        /// <summary>
        /// 克隆
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="gene"></param>
        /// <returns></returns>
        public T copy<T>(NodeGene gene) where T : NodeGene
        {
            this.id = gene.id;
            this.name = gene.name;
            this.cataory = gene.cataory;
            this.generation = gene.generation;
            this.group = gene.group;
            this.usedCount = gene.usedCount;
            this.owner = gene.owner;
            this.depth = gene.depth;
            
            return (T)this;

        }
        /// <summary>
        /// 转字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "id=" + this.id.ToString() + ",name=" + this.name +
                ",generation=" + this.generation + ",cataory=" + this.cataory + ",group=" + this.group;
        }
        public static NodeGene parseGene(NWSEGenome genome,String s)
        {
            if (s == null || s.Trim() == "") return null;
            if (s.StartsWith("ReceptorGene")) return ReceptorGene.parse(genome,s);
            else if (s.StartsWith("HandlerGene")) return HandlerGene.parse(genome,s);
            else if (s.StartsWith("InferenceGene")) return InferenceGene.parse(genome,s);
            return null;
        }
        /// <summary>
        /// 字符串转
        /// </summary>
        /// <param name="str"></param>
        public void parseInfo(String str)
        {
            int i1 = str.IndexOf("id");
            int i2 = str.IndexOf("=", i1 + 1);
            int i3 = str.IndexOf(",", i2 + 1);
            String s = str.Substring(i2 + 1, i3 - i2 - 1);
            id = int.Parse(s);

            i1 = str.IndexOf("name");
            i2 = str.IndexOf("=", i1 + 1);
            i3 = str.IndexOf(",", i2 + 1);
            s = str.Substring(i2 + 1, i3 - i2 - 1);
            name = s;

            i1 = str.IndexOf("generation");
            i2 = str.IndexOf("=", i1 + 1);
            i3 = str.IndexOf(",", i2 + 1);
            s = str.Substring(i2 + 1, i3 - i2 - 1);
            generation = int.Parse(s);

            i1 = str.IndexOf("cataory");
            i2 = str.IndexOf("=", i1 + 1);
            i3 = str.IndexOf(",", i2 + 1);
            s = str.Substring(i2 + 1, i3 - i2 - 1);
            cataory = s;

            i1 = str.IndexOf("group");
            i2 = str.IndexOf("=", i1 + 1);
            i3 = str.Length;
            s = str.Substring(i2 + 1, i3 - i2 - 1);
            group = s;

            
            
        }
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="genome">染色体</param>
        public NodeGene(NWSEGenome genome)
        {
            owner = genome;
        }

        public bool equiv(NodeGene gene)
        {
            return this.Text == gene.Text;
        }

        public int CompareTo(NodeGene other)
        {
            if (other == null) return 1;
            if (this.id > other.id) return 1;
            else if (this.id < other.id) return -1;
            return 0;
        }
        #endregion
    }
}
