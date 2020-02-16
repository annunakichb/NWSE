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
    public class BehaviourMemory
    {

        #region 基本信息
        public Network net;
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="net"></param>
        public BehaviourMemory(Network net)
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
        public ActionPlan FindOptimaActionPlan(bool positiveOnly=true, List<Vector> observation = null)
        {
            var scene = this.FindMatchedScene(observation);
            if (scene.Item1 == null) return null;

            double e = double.MinValue;
            ActionPlan r = null;
            foreach(ActionPlan plan in scene.Item1.plans)
            {
                if(plan.evaulation > e)
                {
                    e = plan.evaulation;
                    r = plan;
                }
            }
            if (positiveOnly && e < 0) return null;
            return r;
        }

        public List<ActionPlan> FindMatchActionPlans(List<Vector> observation = null)
        {
            var scene = this.FindMatchedScene(observation);
            return scene.Item1 == null ? new List<ActionPlan>() : scene.Item1.plans;
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

        public (bool, List<double>) Match(ActionPlan plan,List<Vector> obs)
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
        public void Merge(Network net,ActionPlanChain chain)
        {
            if (chain == null) return;
            double reward = chain.Last.reward;
            int length = chain.Length;
            List<ActionPlan> plans = chain.ToList();
            for(int i=0;i< plans.Count;i++)
            {
                plans[i].evaulation = reward>=0? length-i:i-length;
                Scene scene = FindMatchedScene(plans[i].inputObs).Item1;
                if(scene == null)
                    scene = new Scene(plans[i].inputObs);
                scene.PutActionPlan(plans[i]);
            }
        }
        public void Merge(Network net, ActionPlan plan)
        {
            if (plan == null) return;
            plan.evaulation = plan.reward >= 0 ? 1 : -1;
            Scene scene = FindMatchedScene(plan.inputObs).Item1;
            if (scene == null)
                scene = new Scene(plan.inputObs);
            scene.PutActionPlan(plan);
        }
        #endregion

        internal class Scene
        {
            public List<Vector> scene;
            public readonly List<ActionPlan> plans = new List<ActionPlan>();
            public Scene(List<Vector> scene) { this.scene = scene; }
            public Scene(List<Vector> scene, List<ActionPlan> plans)
            {
                this.scene = scene;
                this.plans.AddRange(plans);
            }
            public Scene(List<Vector> scene, params ActionPlan[] plans)
            {
                this.scene = scene;
                this.plans.AddRange(plans);
            }
            public void Clear() { this.plans.Clear(); }

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

            public ActionPlan GetActionPlan(List<double> actions)
            {
                if (plans.Count <= 0) return null;
                return this.plans.FirstOrDefault(p => p.Equals(actions));
            }
            public int IndexOfActionPlan(List<double> actions)
            {
                for (int i = 0; i < plans.Count; i++)
                {
                    if (plans[i].Equals(actions)) return i;
                }
                return -1;
            }
            public void PutActionPlan(ActionPlan plan)
            {
                int index = IndexOfActionPlan(plan.actions);
                if (index >= 0) this.plans[index] = plan;
                else this.plans.Add(plan);
            }
            public void Remove(List<double> actions)
            {
                int index = IndexOfActionPlan(actions);
                if (index >= 0) this.plans.RemoveAt(index);
            }
        }


    }
}
