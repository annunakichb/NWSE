using NWSELib.common;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace NWSELib.net
{
    /// <summary>
    /// 行为记忆
    /// </summary>
    public class ObservationHistory
    {

        #region 基本信息
        public Network net;
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="net"></param>
        public ObservationHistory(Network net)
        {
            this.net = net;
        }
        /// <summary>
        /// 行为记忆
        /// </summary>
        internal readonly List<Scene> scenes = new List<Scene>();
        #endregion

        #region 查询

        /// <summary>
        /// 寻找最优行动(场景匹配的评估值最大的那个)
        /// </summary>
        /// <returns></returns>
        public ActionRecord FindOptimaAction(bool positiveOnly=true, List<Vector> observation = null)
        {
            var scene = this.FindMatchedScene(observation);
            if (scene.Item1 == null) return null;

            double e = double.MinValue;
            ActionRecord r = null;

            List<ActionRecord> actions = scene.Item1.records;
            foreach (ActionRecord action in actions)
            {
                if(action.evaluation > e)
                {
                    e = action.evaluation;
                    r = action;
                }
            }
            if (positiveOnly && e < 0) return null;
            return r;
        }

        public List<ActionRecord> FindMatchActionPlans(List<Vector> observation = null)
        {
            var scene = this.FindMatchedScene(observation);
            return scene.Item1 == null ? new List<ActionRecord>() : scene.Item1.records;
        }

        private (Scene,List<double>) FindMatchedScene(List<Vector> observation = null)
        {
            if(observation == null)
                observation = net.GetSplitReceptorValues().scene;
            
            foreach (Scene s in scenes)
            {
                var r = s.Match(net, observation);
                if (r.Item1) return (s, r.Item2);
            }
            return (null,null);
        }

        public (bool, List<double>) Match(net.ActionPlan plan,List<Vector> obs)
        {
            List<double> dis = net.GetReceptorDistance(plan.inputObs,obs);
            for(int i=0;i<dis.Count;i++)
            {
                if (!net.Receptors[i].IsTolerateDistance(dis[i])) return (false,dis);
            }
            return (true,dis);
        }

        #endregion

        #region 添加
        public void Merge(Network net,ActionPlanChain chain,bool evaulation=true)
        {
            if (chain == null) return;
            double reward = chain.Last.reward;
            int length = chain.Length;
            List<net.ActionPlan> plans = chain.ToList();

            if(evaulation)
                plans[0].evaulation = reward >= 0 ? length : -length;
            Scene scene = FindMatchedScene(plans[0].inputObs).Item1;
            if (scene == null)
            {
                scene = new Scene(plans[0].inputObs);
                this.scenes.Add(scene);
            }
            scene.PutActionPlan(plans[0]);


            for (int i=1;i< plans.Count;i++)
            {
                if (evaulation)
                    plans[i].evaulation = reward >= 0 ? length - i : i - length;

                scene = FindMatchedScene(plans[i].inputObs).Item1;
                if(scene == null)
                {
                    scene = new Scene(plans[i].inputObs);
                    this.scenes.Add(scene);
                }
                scene.PutActionPlan(plans[i]);

            }
        }
        public void Merge(Network net, net.ActionPlan plan,bool evaulation=true)
        {
            if (plan == null) return;
            if(evaulation)
                plan.evaulation = plan.reward >= 0 ? 1 : -1;
            Scene scene = FindMatchedScene(plan.inputObs).Item1;
            if (scene == null)
            {
                scene = new Scene(plan.inputObs);
                this.scenes.Add(scene);
            }
                
            scene.PutActionPlan(plan);
        }
        #endregion

        public class ActionRecord
        {
            public List<double> actions;
            public double evaluation;

            public ActionRecord(List<double> actions, double evaluation)
            {
                this.actions = new List<double>(actions);
                this.evaluation = evaluation;
            }
        }
        public class Scene
        {
            public List<Vector> scene;
            public readonly List<ActionRecord> records = new List<ActionRecord>();
            public Scene(List<Vector> scene) { this.scene = scene; }
            public Scene(List<Vector> scene, List<net.ActionPlan> plans)
            {
                this.scene = scene;
                if (plans == null || plans.Count <= 0) return;
                this.records = plans.ConvertAll(p => new ActionRecord(p.actions, p.evaulation));
            }
            public Scene(List<Vector> scene, params net.ActionPlan[] plans)
            {
                this.scene = scene;
                if (plans == null || plans.Length <= 0) return;
                this.records = plans.ToList().ConvertAll(p => new ActionRecord(p.actions, p.evaulation));
            }
            public void Clear() { this.records.Clear(); }

            public (bool,List<double>) Match(Network net,List<Vector> observation)
            {
                List<double> dis = new List<double>();
                
                for (int i = 0; i < net.Receptors.Count; i++)
                {
                    if (net.Receptors[i].getGene().IsActionSensor()) continue;
                    double td = net.Receptors[i].distance(scene[i][0], observation[i][0]);
                    if (!net.Receptors[i].IsTolerateDistance(td)) return (false, null);
                    dis.Add(td);
                }
                return (true, dis);
            }

            public ActionRecord GetAction(List<double> actions)
            {
                if (actions.Count <= 0) return null;
                return this.records.FirstOrDefault(p => Utility.equals<double>(p.actions,actions));
            }
            public int IndexOfActionPlan(List<double> actions)
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    if (Utility.equals<double>(this.records[i].actions, actions))
                        return i;
                }
                return -1;
            }
            public void PutActionPlan(net.ActionPlan plan)
            {
                int index = IndexOfActionPlan(plan.actions);
                if (index >= 0) this.records[index] = new ActionRecord(plan.actions,plan.evaulation);
                else this.records.Add(new ActionRecord(plan.actions, plan.evaulation));
            }
            public void Remove(List<double> actions)
            {
                int index = IndexOfActionPlan(actions);
                if (index >= 0) this.records.RemoveAt(index);
            }

            public override string ToString()
            {
                return this.scene.flatten().Item1.ToString() + ";" +
                       records.Count.ToString() + "(" +
                       records.ConvertAll(p => p.actions[0].ToString("F3"))
                       .Aggregate((x, y) => x + "," + y) + ")";
            }
        }


    }
}
