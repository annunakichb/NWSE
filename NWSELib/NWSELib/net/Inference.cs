using System;
using System.Linq;
using NWSELib.common;
using NWSELib.genome;
using System.Collections.Generic;
using Microsoft.ML.Probabilistic.Distributions;
using Microsoft.ML.Probabilistic.Models;
using Microsoft.ML;
using log4net;
using System.Text;

namespace NWSELib.net
{
    
   
    /// <summary>
    /// 推理节点
    /// </summary>
    public class Inference : Node
    {
        #region 成员
        static ILog logger = LogManager.GetLogger(typeof(Inference));
        /// <summary>
        /// 推理节点存储的记录
        /// </summary>
        protected List<InferenceRecord> records = new List<InferenceRecord>();
        /// <summary>
        /// 推理节点存储的记录
        /// </summary>
        public List<InferenceRecord> Records { get => this.records; }
        /// <summary>
        /// 新样本,尚未归属到任何记录中，因为要积累一些才进行记录融合
        /// </summary>
        public List<List<Vector>> unclassified_samples = new List<List<Vector>>();
        /// <summary>
        /// 新样本的密度值，作为密度聚类的依据，其数量应与unclassified_samples始终一致
        /// </summary>
        public List<double> density = new List<double>();

        /// <summary>
        /// 下一级别记录
        /// </summary>
        public Dictionary<int, List<InferenceRecord>> childs = new Dictionary<int, List<InferenceRecord>>();


        /// <summary>
        /// 可靠度
        /// </summary>
        public override double Reability 
        { 
            get
            {
                if(this.records.Count<=0)return double.NaN;
                List<double> ds = this.records.FindAll(r => !double.IsNaN(r.accuracy)).ConvertAll(r => r.accuracy);
                if (ds == null || ds.Count <= 0) return double.NaN;
                return ds.Average();
            }
        }
        #endregion

        #region 初始化
        public readonly List<Receptor> conditionReceptors;
        public readonly List<Receptor> variablesReceptors;
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="gene"></param>
        public Inference(NodeGene gene,Network net) : base(gene,net) 
        {
            this.getGene().sort_dimension();
            conditionReceptors = this.getGene().getConditionIds().ConvertAll(id => (Receptor)net[id]);
            variablesReceptors = this.getGene().getVariableIds().ConvertAll(id => (Receptor)net[id]);
        }
        
        public override string ToString()
        {
            StringBuilder str = new StringBuilder();
            str.Append("推理节点=" + Gene.Text + System.Environment.NewLine);
            str.Append("记录数=" + Records.Count.ToString() + System.Environment.NewLine);
            for (int j = 0; j < Records.Count; j++)
            {
                str.Append(Records[j].toString(this,j));
                
            }
            return str.ToString();
        }
        /// <summary>
        /// 分级显示
        /// </summary>
        /// <returns></returns>
        public string toCaption()
        {
            StringBuilder str = new StringBuilder();
            str.Append("推理节点=" + Gene.Text + System.Environment.NewLine);
            str.Append("记录数=" + Records.Count.ToString() + System.Environment.NewLine);
            for (int j = 0; j < Records.Count; j++)
            {
                str.Append(Records[j].toString(this,j));

            }
            return str.ToString();
        }
        #endregion

        #region 信息查询
        /// <summary>
        /// 推理基因
        /// </summary>
        /// <returns></returns>
        public InferenceGene getGene()
        {
            return (InferenceGene)gene;
        }
        
        /// <summary>
        /// 取得所有维度的节点id
        /// </summary>
        /// <returns></returns>
        public List<int> getIdList()
        {
            return this.getGene().getDimensions().ConvertAll(d => d.Item1);
        }

        

        /// <summary>
        /// 将各维度的id分解为环境类(含姿态)、动作类、后置变量类
        /// </summary>
        /// <returns></returns>
        public (List<int>, List<int>, List<int>) splitIds()
        {
            List<int> e = new List<int>();
            List<int> a = new List<int>();
            List<int> v = this.getGene().variables.ConvertAll(var => var.Item1);

            
            List<(int, int)> ds = this.getGene().conditions;
            for (int i = 0; i < ds.Count; i++)
            {
                NodeGene g = this.getGene().owner[ds[i].Item1];
                if (g.IsActionSensor())
                    a.Add(ds[i].Item1);
                else
                    e.Add(ds[i].Item1);
            }
            return (e, a, v);
        }

        public override List<Node> getInputNodes(Network net)
        {
            return this.getGene().getDimensions().ConvertAll(d=>net.getNode(d.Item1));
        }
        #endregion

        #region 值管理
        /// <summary>
        /// 根据推理节点的维定义（依据时间要求）取得所有输入值
        /// </summary>
        /// <param name="net"></param>
        /// <returns></returns>
        public List<Vector> getInputValues(Network net,int time)
        {
            return this.getGene().getDimensions().ConvertAll(d => net.getNode(d.Item1).GetValue(time - d.Item2));
        }

        /// <summary>
        /// 取得给定值的文本显示信息
        /// </summary>
        /// <param name="value">为空，则取当前最新值</param>
        /// <returns></returns>
        public override String getValueText(Vector value = null)
        {
            if (value == null) value = Value;
            if (value == null) return "";
            List<Receptor> receptors = this.getGene().getLeafGenes().ConvertAll(g=>(Receptor)net[g.Id]);
            int condCount = this.getGene().ConditionCount;
            int varCount = this.getGene().VariableCount;

            StringBuilder str = new StringBuilder();
            for(int i=0;i<receptors.Count;i++)
            {
                if(str.ToString() != "")
                {
                    if(i == condCount && varCount > 0)
                        str.Append("=>");
                    else str.Append(",");
                }
                str.Append(receptors[i].getValueText(new Vector(value[i])));
            }
            return str.ToString();
        }

        /// <summary>
        /// 取得前置条件和后置变量值
        /// </summary>
        /// <param name="net"></param>
        /// <param name="time"></param>
        /// <returns></returns>
        public (List<Vector>,List<Vector>) getValues2(Network net, int time)
        {
            List<Vector> c = this.getGene().conditions
                .ConvertAll(item=>(net[item.Item1],item.Item2))
                .ConvertAll(item=>item.Item1.GetValue(time-item.Item2));

            List<Vector> v = this.getGene().variables
                .ConvertAll(item => (net[item.Item1], item.Item2))
                .ConvertAll(item => item.Item1.GetValue(time - item.Item2));
            

            return (c, v);
        }

        public (List<Vector>, List<Vector>, List<Vector>) getValues3(Network net, int time)
        {
            List<Vector> ce = new List<Vector>();
            List<Vector> ca = new List<Vector>();
            List<Vector> v = new List<Vector>();

            List<(int, int)> conditions = this.getGene().conditions;
            for (int i = 0; i < conditions.Count; i++)
            {
                Node node = net.getNode(conditions[i].Item1);
                if (node.Gene.IsActionSensor())
                    ca.Add(node.GetValue(time - conditions[i].Item2));
                else
                    ce.Add(node.GetValue(time - conditions[i].Item2));
            }
            return (ce,ca,v);
        }



        /// <summary>
        /// 将所有值中的环境部分（非动作部分）用envValues替换
        /// 其中allValue和envValues维度相同，且只包含条件部分
        /// </summary>
        /// <param name="allValue"></param>
        /// <param name="envValues"></param>
        /// <returns></returns>
        internal List<Vector> replaceEnvValue(List<Vector> allValue, List<Vector> envValues)
        {

            List<Vector> r = new List<Vector>(allValue);
            for (int i = 0; i < this.getGene().conditions.Count; i++)
            {
                NodeGene gene = this.getGene().owner[this.getGene().conditions[i].Item1];
                if (gene.IsActionSensor()) continue;
                r[i] = envValues[i];

            }
            return r;
        }

        public (InferenceRecord,double) getNearestMatchRecord(Network net, List<Vector> values, int time)
        {
            List<(InferenceRecord, double)> result = this.getMatchRecords(net, values, time);
            if (result == null || result.Count <= 0) return (null, 0.0);
            double d = double.MaxValue;
            InferenceRecord r = null;
            foreach(var temp in result)
            {
                if(temp.Item2<d)
                {
                    d = temp.Item2;
                    r = temp.Item1;
                }
            }
            return (r, d);
        }
        public List<(InferenceRecord,double)> getMatchRecords(Network net,List<Vector> values,int time)
        {
            List<(InferenceRecord, double)> result = new List<(InferenceRecord, double)>();

            foreach (InferenceRecord r in this.records)
            {
                double dis = 0.0;
                if(r.isConditionValueMatch(values, out dis))
                {
                    result.Add((r,dis));
                }
            }
            return result;
        }
        public List<(InferenceRecord,double)> getMatchRecordsInThink(Network net,int time)
        {
            List<(InferenceRecord, double)> result = new List<(InferenceRecord, double)>();

            List<(int, int)> conds = this.getGene().getConditions();
            List<Vector> values = conds.ConvertAll(cond =>
                net.getNode(cond.Item1).getThinkValues(time)
            );
            foreach(InferenceRecord record in this.records)
            {
                double distance = 0.0;
                if (record.isConditionValueMatch(values, out distance))
                {
                    result.Add((record,distance));
                }
            }

            return result;
        }

        /// <summary>
        /// 取得部分条件啊匹配的记录，用于推理合成
        /// </summary>
        /// <param name="condIds"></param>
        /// <param name="condValues"></param>
        /// <returns></returns>
        public List<InferenceRecord> getPartialMatchRecords(Network net,List<int> condIds, List<Vector> condValues)
        {
            List<InferenceRecord> r = new List<InferenceRecord>();
            foreach(InferenceRecord record in this.records)
            {
                List<Vector> meanValues = record.getConditionValueByIds(condIds);
                if (meanValues == null || meanValues.size() != condValues.size()) continue;
                (Vector meanValue,List<Node> meanNode) = net.flattenValues(condIds, meanValues);
                (Vector condValue, List<Node> condNode) = net.flattenValues(condIds, condValues);
                if (meanValue.Size != condValue.Size) continue;

                bool match = true;
                for(int i=0;i<meanNode.Count;i++)
                {
                    if (!MeasureTools.GetMeasure(meanNode[i].Gene.Cataory).match(meanValue[i], condValue[i]))
                    {
                        match = false;break;
                    }
                
                }
                if (match) r.Add(record);

            }
            return r;
        }

        public List<InferenceRecord> getVariableMatchRecords(Network net, int time,List<Vector> varValues=null)
        {
            //变量值无效，则从环境取
            if(varValues == null || varValues.Count<=0)
            {
                varValues = this.getValues2(net,time).Item2;
            }
            //变量值无效
            if (varValues.Contains(null)) return null;

            double tempDis = 0;
            return this.records.FindAll(r => r.isVariableMatch(varValues,out tempDis));
            
        }
        #endregion

        #region 激活和自适应调整
        /// <summary>
        /// 设置当前值
        /// </summary>
        /// <param name="value"></param>
        /// <param name="time"></param>
        /// <returns></returns>
        public override Object activate(Network net, int time, Object value = null)
        {
            //所有的输入节点都已经被激活
            //Wether all input nodes have already been activated
            List<(int,int)> conds = ((InferenceGene)this.Gene).getConditions();
            List<(Node, int)> condNodes = conds.ConvertAll(c => (net.getNode(c.Item1), c.Item2));
            if (!condNodes.All(n => n.Item1.IsActivate(time - n.Item2)))
                return null;

            List<(int, int)> vars = this.getGene().getVariables();
            List<(Node, int)> varNodes = vars.ConvertAll(v => (net.getNode(v.Item1), v.Item2));
            if (!varNodes.All(n => n.Item1.IsActivate(time - n.Item2)))
                return null;

            Vector activeValue = null;
            //确保推理基因的各维度的顺序正确（前置条件在前，后置变量在后，且前置条件id按从小到大排列）
            //Make sure that the dimensions of the inference gene are in the correct order
            ((InferenceGene)this.Gene).sort_dimension();

            List<Node> inputs = net.getInputNodes(this.Id);

            //根据基因定义的顺序，将输入值组成List<Vector>
            //Put the input values into the List according to the order of the input dimensions
            List<Vector> values = this.getInputValues(net,time);
            if(values == null)
            {
                base.activate(net, time, null);
                return null;
            }
            int totaldimesion = values.flatten().Item1.Size;


            //如果没有任何节点记录，则生成第一个
            //Create a new record if there are no nodes in current inference node, 
            if (this.records.Count <= 0)
            {
                InferenceRecord record = new InferenceRecord(this);
                record.means = values;
                record.covariance = new double[totaldimesion, totaldimesion];
                for (int i = 0; i < totaldimesion; i++) //缺省协方差矩阵为单位阵
                    record.covariance[i, i] = 1.0;
                record.weight = 1.0;
                record.acceptCount = 1;
                var nearestRecord = this.getNearestMatchRecord(net, record.getMeanValues().condValues, time);
                if (nearestRecord.Item1 != null) record.evulation = nearestRecord.Item1.evulation;
                this.records.Add(record);
                activeValue = values.flatten().Item1;
                base.activate(net, time, activeValue);
               
                return activeValue;
            }

            //计算输入值的归属
            //Calculate which record the input value belongs to
            List<double> probs = this.records.ConvertAll(r => r.prob(values)/r.prob(r.means));
            //double sumprobs = probs.Sum();
            //probs = probs.ConvertAll(p => p / sumprobs);
            int pindex = probs.argmax();
            if(probs.Max()>=Session.GetConfiguration().learning.inference.accept_prob)
            {
                this.records[pindex].acceptCount += 1;
                this.records[pindex].acceptRecords.Add(values);
                if(this.records[pindex].acceptCount >= Session.GetConfiguration().learning.inference.accept_max_count)
                {
                    this.records[pindex].do_adjust(net);
                }
                activeValue = this.records[pindex].means.flatten().Item1;
                base.activate(net, time, activeValue);
                return activeValue;

            }
            //判断是否需要加入到未归类样本中:如果节点中记录非常少，则尽量增加记录
            if(this.records.Count<=30)
            {
                InferenceRecord record = new InferenceRecord(this);
                record.means = values;
                record.covariance = new double[totaldimesion, totaldimesion];
                for (int i = 0; i < totaldimesion; i++) //缺省协方差矩阵为单位阵
                    record.covariance[i, i] = 1.0;
                record.acceptCount = 1;
                var nearestRecord = this.getNearestMatchRecord(net, record.getMeanValues().condValues, time);
                if (nearestRecord.Item1 != null) record.evulation = nearestRecord.Item1.evulation;
                this.records.Add(record);
                activeValue = values.flatten().Item1;
                base.activate(net, time, activeValue);

                adjust_weights();
                return activeValue;
            }

            //计算每个记录的密度值，以及样本的密度值
            //If the new sample is not classified into any records, calculate the density values for each record and for all unclassified samples
            //1.Calculate the Euclidean distance from each record to the new sample and all unclassified samples to the new sample
            //2.Take the ratio of the above distances as the density increment of each record and unclassified sample
            //3.The density of the new sample is equal to that of the nearest record or unclassified sample
            List<List<Vector>> allValues = new List<List<Vector>>();
            this.records.ForEach(r => allValues.Add(r.means));
            allValues.AddRange(this.unclassified_samples);
            List<double> distances = allValues.ConvertAll(v => v.distance(values));
            double dissum = distances.Sum();
            List<double> delta_diensity = distances.ConvertAll(d => (dissum - d) / dissum);
            for(int i=0;i<this.records.Count;i++)
            {
                this.records[i].density += delta_diensity[i];
            }
            for(int i=this.records.Count;i<delta_diensity.Count;i++)
            {
                this.density[i - this.records.Count] += delta_diensity[i];
            }
            this.unclassified_samples.Add(values);
            int ti = distances.argmin();
            double td = ti < this.records.Count ? this.records[ti].density : this.density[ti - this.records.Count];
            this.density.Add(td);

            //未归类样本很少，暂不进行聚类
            if (unclassified_samples.Count <= 10)
            {
                base.activate(net, time, null);
                return values;
            }

            //如果新样本的最大密度接近原有高斯分量中心的最小密度，则启动新样本聚类过程
            //If the maximum density of the unclassified samples is close to the minimum density of the original Gaussian records, the clustering process of the the unclassified samples will be started
            double max_newsample_density = this.density.Max();
            double min_record_density = this.records.ConvertAll(r => r.density).Min();
            int newCount = 0;
            if(max_newsample_density >= min_record_density*2/3)
            {
                (List<List<List<Vector>>> clusters,List<List<double>> des) = do_unclassfied_cluster();
                for(int i=0;i<clusters.Count;i++)
                {
                    
                    InferenceRecord newRecord = create_newrecord_bysamples(clusters[i], des[i]);
                    newRecord.density = des[i].Average();
                    var nearestRecord = this.getNearestMatchRecord(net, newRecord.getMeanValues().condValues, time);
                    if (nearestRecord.Item1 != null) newRecord.evulation = nearestRecord.Item1.evulation;
                    this.records.Add(newRecord);
                }
                newCount = clusters.Count;
            }
            //如果两个节点的距离太近，则合并节点
            //If two records are too close, merge nodes
            int mergeCount = 0;// try_merge_records();
            //重新调整权重
            adjust_weights();

            //输出
            logger.Debug(net.Id.ToString()+".inference" + this.Id.ToString() +"'records are adjusted:count of new="
                + newCount.ToString()
                + ",count of merge="
                + mergeCount.ToString()
                +",count of record="+this.records.Count
                +",accept counts =" + this.records.ConvertAll(r=>r.acceptCount.ToString()).Aggregate((x,y)=>x+","+y));

            activeValue = this.records[this.records.ConvertAll(r => r.prob(values) / r.prob(r.means)).argmax()].means.flatten().Item1;
            base.activate(net, time, activeValue);
            return activeValue;

        }
        /// <summary>
        /// 对未归类样本进行聚类操作
        /// Cluster unclassified samples
        /// </summary>
        /// <returns></returns>
        private (List<List<List<Vector>>>,List<List<double>>) do_unclassfied_cluster()
        {
            logger.Debug("do_unclassfied_cluster....");
            int count = this.unclassified_samples.Count;
            List<List<List<Vector>>> r = new List<List<List<Vector>>>();
            List<List<double>> dens = new List<List<double>>();
            while (this.unclassified_samples.Count > 0)
            {
                //取最大密度点，作为一个新分类
                //Take the maximum density point as a new classification
                int maxindex = this.density.argmax();
                List<Vector> sample = this.unclassified_samples[maxindex];
                List<List<Vector>> classes = new List<List<Vector>>();
                List<double> den = new List<double>();
                classes.Add(sample);
                den.Add(this.density[maxindex]);
                r.Add(classes);
                dens.Add(den);

                //从样本集中移除该点
                //Remove the sample from the unclassified samples
                this.unclassified_samples.RemoveAt(maxindex);
                this.density.RemoveAt(maxindex);

                //如果只剩下最后一个样本，将它独立作为一类
                if (this.unclassified_samples.Count == 1)
                {
                    List<List<Vector>> lastclasses = new List<List<Vector>>();
                    List<double> lastden = new List<double>();
                    lastclasses.Add(this.unclassified_samples[0]);
                    lastden.Add(this.density[0]);
                    r.Add(lastclasses);
                    dens.Add(lastden);
                    break;
                }

                //if (this.unclassified_samples.Count <= 0) break;
                

                //计算未归类样本集中所有样本与该样本的距离
                //Calculate the distance between all samples in the unclassified sample set and the sample
                List<double> ds = this.unclassified_samples.ConvertAll(s => s.distance(sample));
                
                //对距离从小到大排序
                //sort the distances in ascending order
                List<int> sortedindex = ds.argsort();


                //对排列后的距离寻找最小方差分裂点
                //Finding the minimum variance split point
                List<double> disvar = new List<double>();
                for(int i=2;i<ds.Count;i++)
                {
                    List<double> temp = new List<double>();
                    for (int j = 0; j < i; j++)
                        temp.Add(ds[sortedindex[j]]);

                    (double t1, double t2) = new Vector(temp.ToArray()).avg_variance();
                    disvar.Add(t2);
                }
                int argminvar = disvar.argmin()+2;

                //将能够使方差最小的样本归属同一类
                List<List<Vector>> temps = new List<List<Vector>>();
                List<double> dtemps = new List<double>();
                for(int i=0;i<argminvar;i++)
                {
                    classes.Add(this.unclassified_samples[sortedindex[i]]);
                    den.Add(this.density[sortedindex[i]]);
                }
                for (int i = argminvar; i < sortedindex.Count; i++)
                {
                    temps.Add(this.unclassified_samples[sortedindex[i]]);
                    dtemps.Add(this.density[sortedindex[i]]);
                }
                unclassified_samples = temps;
                density = dtemps;
                if (this.unclassified_samples.Count <= 0) break;

            }

            logger.Debug("do_unclassfied_cluster:count = "+count.ToString()
                +",cluster="+r.Count.ToString()+",size of per cluster="+
                r.ConvertAll(e=>e.Count.ToString()).Aggregate((a,b)=>a+","+b));
            return (r,dens);
        }
        
        /// <summary>
        /// 将vs中的所有样本作为新的高斯分量记录
        /// All samples in vs are recorded as new Gaussian components
        /// </summary>
        /// <param name="vs"></param>
        /// <returns></returns>
        public InferenceRecord create_newrecord_bysamples(List<List<Vector>> vs,List<double> densitys=null)
        {
            InferenceRecord r = new InferenceRecord(this);
            r.acceptCount = vs.Count;

            List<int> dimensions = vs[0].ConvertAll(v => v.Size);
            int totaldimension = dimensions.Sum();
            List<Vector> flatten = vs.ConvertAll(v => v.flatten()).ConvertAll(v => v.Item1);
            r.means = flatten.average().split(dimensions);
            r.covariance = new double[totaldimension, totaldimension];
            r.covariance = Vector.covariance(flatten.ToArray());
            r.acceptCount = vs.Count;
            if(densitys != null || densitys.Count>0) r.density = densitys.Average();
            r.initGaussian();
            return r;

        }
        /// <summary>
        /// 合并靠的太近的记录
        /// Merge too close records
        /// </summary>
        protected int try_merge_records()
        {
            List<(InferenceRecord, InferenceRecord)> needMergeRecordPair = new List<(InferenceRecord, InferenceRecord)>();
            for(int i=0;i<this.records.Count;i++)
            {
                bool merged = false;
                for(int j=i+1;j<this.records.Count;j++)
                {
                    double d1 = this.records[i].prob(this.records[j].means) / this.records[i].prob(this.records[i].means);
                    double d2 = this.records[j].prob(this.records[i].means) / this.records[j].prob(this.records[j].means);
                    if (d1 >= Session.GetConfiguration().learning.inference.accept_prob || d2 >= Session.GetConfiguration().learning.inference.accept_prob)
                    {
                        needMergeRecordPair.Add((this.records[i], this.records[j]));
                        merged = true;
                        this.records.RemoveAt(j);
                        break;
                    }
                }
                if(merged)
                {
                    this.records.RemoveAt(i);i--;
                }
            }
            if (needMergeRecordPair.Count <= 0) return 0;
            for(int i=0;i< needMergeRecordPair.Count;i++)
            {
                InferenceRecord r1 = needMergeRecordPair[i].Item1;
                InferenceRecord r2 = needMergeRecordPair[i].Item2;
                List<List<Vector>> samples = new List<List<Vector>>();
                for (int j = 0; j < r1.acceptCount; j++)
                    samples.Add(r1.means);
                samples.AddRange(r1.acceptRecords);
                for (int j = 0; j < r2.acceptCount; j++)
                    samples.Add(r2.means);
                samples.AddRange(r2.acceptRecords);

                InferenceRecord newRecord = this.create_newrecord_bysamples(samples);
                newRecord.density = (r1.density + r2.density) / 2;
                this.records.Add(newRecord);
            }
            return needMergeRecordPair.Count;
        }
       
        
        

        public void adjust_weights()
        {
            List<double> ws = this.records.ConvertAll(r => (double)r.acceptCount);
            double max = ws.Sum();
            ws = ws.ConvertAll(w => w / max);
            for (int i = 0; i < this.records.Count; i++)
                this.records[i].weight = ws[i]; 

            /*List<double> ws = this.records.ConvertAll(r => r.gaussian.GetLogProb(r.gaussian.GetMean()));
            double max = ws.Max();
            ws = ws.ConvertAll(w => w / max);
            for (int i = 0; i < this.records.Count; i++)
                this.records[i].weight = ws[i];*/
        }
        #endregion


        #region 推理

        /// <summary>
        /// 前向推理：给定条件，计算后置变量值
        /// 前向推理有三种方式
        /// 1.将混合高斯模型看作联合模型，进行前向推理
        /// 2.在混合高斯模型上选择距离中心最近的分量，单独对该高斯分量进行前向推理
        /// 3.在混合高斯模型上选择距离中心最近的分量，直接选择该分量的均值的变量部分作为结果
        /// 4.在混合高斯模型上进行采样，在采样中选择距离条件部分最近的，计算后置变量部分作为结果。
        /// 目前用的是第三种
        /// </summary>
        /// <param name="condvalues"></param>
        /// <returns>最近的记录,,是否在容忍距离内,距离值,评估值</returns>
        public (InferenceRecord, List<Vector>, bool vaildDistance,double distance) forward_inference2(Network net, List<Vector> condvalues)
        {
            double distance = double.MaxValue;
            InferenceRecord record = null;
            bool recordMatched = false;
            foreach (InferenceRecord r in this.records)
            {
                double d = 0;
                bool match = r.isConditionValueMatch(condvalues, out d);
                if(d < distance)
                {
                    record = r;
                    distance = d;
                    recordMatched = match;
                }
                
            }
            
            return (record, (record==null?null:record.getMeanValues().varValues), recordMatched, distance);
        }
        public (InferenceRecord, List<Vector>,double distance) forward_inference(Network net,List<Vector> condvalues)
        {
            double distance = double.MaxValue;
            InferenceRecord matchedRecord = null;
            double evaulation = double.MaxValue;
            foreach(InferenceRecord r in this.records)
            {
                double d = 0;
                if (!r.isConditionValueMatch(condvalues, out d))
                    continue;
                //if(r.evulation < evaulation) //找相近里面评估最差的？
                if (d < distance) //找距离最近的
                {
                    evaulation = r.evulation;
                    matchedRecord = r;
                    distance = d;
                }
            }
            if (matchedRecord == null) return (null, null, distance);
            return (matchedRecord, matchedRecord.forward_inference(this, condvalues),distance);
        }
        
        
        

        /// <summary>
        /// 在混合高斯模型上采样
        /// </summary>
        /// <param name="inferencesamples"></param>
        /// <returns></returns>
        private List<List<Vector>> samples(int inferencesamples)
        {
            this.records.ForEach(r => r.initGaussian());

            double[] ws = this.records.ConvertAll(r => r.weight).ToArray();

            Discrete zt = new Discrete(ws);
            List<List<Vector>> result = new List<List<Vector>>();
            for (int i = 0; i < inferencesamples; i++)
            {
                int index = zt.Sample();
                result.Add(this.records[index].sample(1)[0]);
            }
            
            return result;

            /*
            using (Variable.ForEach(n))
            {
                z[n] = Variable.Discrete(ws);
                //z.SetValueRange(n);
                int index = Variable.Switch(z[n]).ConditionValue;

                using (Variable.Switch(z[n]))
                {
                    data[n] = Variable.VectorGaussianFromMeanAndVariance(
                      means[z[n]], variances[z[n]]);
                }
            }
            List<int> dimension = this.records[0].means.ConvertAll(v => v.Size);
            return data.ObservedValue.ToList().ConvertAll(v => v.fromMathVector(dimension));
            */
        }

        

        #endregion
    }
}
