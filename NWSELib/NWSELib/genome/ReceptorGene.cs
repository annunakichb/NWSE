using NWSELib.common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NWSELib.genome
{
    /// <summary>
    /// 感知层基因
    /// </summary>
    public class ReceptorGene : NodeGene
    {
        #region 数据范围和分级
        
        /// <summary>
        /// 数据抽象层级
        /// </summary>
        public int AbstractLevel
        {
            get
            {
                return Session.GetConfiguration().agent.receptors.GetSensor(this.name).abstractLevel;
            }
        }

        private int getAbstractSectionCount(int abstraceLevel)
        {
            Configuration.Sensor s = Session.GetConfiguration().agent.receptors.GetSensor(this.name);
            if (s != null && s.Levels != null && abstraceLevel <= s.Levels.Count)
            {
                return abstraceLevel==0?s.Levels[abstraceLevel]: s.Levels[abstraceLevel-1];
            }
            MeasureTools mt = MeasureTools.GetMeasure(this.cataory);
            if (mt == null) throw new Exception(this.Name + "无法完成分级：" + abstraceLevel.ToString() + ",");
            if (mt.Levels != null && abstraceLevel <= mt.Levels.Count)
            {
                return abstraceLevel == 0 ? mt.Levels[abstraceLevel] : mt.Levels[abstraceLevel - 1];
            }
            throw new Exception(this.Name + "无法完成有效分级：" + abstraceLevel.ToString() + ",");
        }

        public int AbstractSectionCount { get => getAbstractSectionCount(this.AbstractLevel); }

        public List<String> AbstractLevelNames 
        {
            get
            {
                List<String> names = null;
                Configuration.Sensor s = Session.GetConfiguration().agent.receptors.GetSensor(this.name);
                if(s != null && s.LevelNames != null)
                {
                    names = s.LevelNames[this.AbstractLevel==0?0: AbstractLevel-1].ToList();
                }
                if (names != null) return names;

                MeasureTools mt = MeasureTools.GetMeasure(this.Cataory);
                if(mt != null && mt.LevelNames != null)
                {
                    names = mt.LevelNames[this.AbstractLevel == 0 ? 0 : AbstractLevel - 1].ToList();
                }
                return names;
            }
        }
        /// <summary>
        /// 取值范围
        /// </summary>
        public ValueRange Range
        {
            get
            {
                return MeasureTools.GetMeasure(this.Cataory).Range;
            }
        }
        /// <summary>
        /// 当前分级的单位距离
        /// </summary>
        public double LevelUnitDistance
        {
            get
            {
                if (AbstractSectionCount == 0) return 0;
                return this.Range.Distance / this.AbstractSectionCount;
            }
        }

        #endregion

        #region 维度管理
        /// <summary>
        /// 输入基因的维度列表
        /// </summary>
        public override List<int> Dimensions { get => new List<int>(); }
        /// <summary>
        /// 自身数据的维度
        /// </summary>
        public override int Dimension { get => 1; }

        /// <summary>
        /// 取得输入基因
        /// </summary>
        /// <returns></returns>
        public override List<NodeGene> getInputGenes()
        {
            return null;
        }
        #endregion


        #region 初始化、读写和转换

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="genome"></param>
        public ReceptorGene(NWSEGenome genome):base(genome)
        {
            
        }
        /// <summary>
        /// 转字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "ReceptorGene:" + Text + ";info:" + base.ToString() + ";param:abstractLevel="+this.AbstractLevel.ToString();
        }
        /// <summary>
        /// 解析字符串
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static ReceptorGene parse(NWSEGenome genome,String s)
        {
            String[] ss = s.Split(';');
            ReceptorGene gene = new ReceptorGene(genome);
            gene.name = ss[0].Substring(ss[0].IndexOf(":")+1).Trim();

            gene.parseInfo(ss[1].Substring(ss[1].IndexOf("info:")+5));

            int index = ss[2].IndexOf("abstractLevel");
            index = ss[2].IndexOf("=", index + 1);
            
            return gene;

        }

        public override T clone<T>()
        {
            return (T)(Object)new ReceptorGene(this.owner).copy<ReceptorGene>(this);
        }

        /// <summary>
        /// 将动作感知基因转为动作基因
        /// </summary>
        public ReceptorGene toActionGene()
        {
            return new ReceptorGene(this.owner)
            {
                Id = this.Id,
                name = this.name.Substring(1),
                generation = this.generation,
                cataory = this.cataory
            };
        }
        #endregion
    }
}
