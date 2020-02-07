using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.IO;
using System.Linq;
using System.Threading;
  

using log4net;

using NWSELib.common;
using NWSELib.evolution;
using NWSELib.genome;
using NWSELib.net;
using NWSELib.env;

namespace NWSELib
{
    /// <summary>
    /// 推送事件消息
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="generation"></param>
    /// <param name="states"></param>
    public delegate void EventHandler(String eventName, int generation,params Object[] states);
    /// <summary>
    /// 生成本能动作（总是朝向目标方向走）
    /// </summary>
    /// <param name="net"></param>
    /// <param name="time"></param>
    /// <returns></returns>
    public delegate List<double> InstinctActionHandler(Network net, int time);
    /// <summary>
    /// 计算适应度方法
    /// </summary>
    /// <param name="net"></param>
    /// <param name="session"></param>
    /// <returns></returns>
    public delegate double FitnessHandler(Network net, Session session);
    /// <summary>
    /// 执行任务
    /// </summary>
    public class Session
    {
        #region 基本信息
        /// <summary>
        /// 配置
        /// </summary>
        public static Configuration config;
        /// <summary>
        /// 进化树
        /// </summary>
        public EvolutionTreeNode root;
        /// <summary>
        /// ID生成器
        /// </summary>
        public static IdGenerator idGenerator = new IdGenerator();
        /// <summary>
        /// 本能行为处理器
        /// </summary>
        public static InstinctActionHandler instinctActionHandler;
        /// <summary>
        /// 适应度函数
        /// </summary>
        public static FitnessHandler fitnessHandler;

        /// <summary>
        /// 配置
        /// </summary>
        public static Configuration GetConfiguration()
        {
            if(config == null)
            {
                config = (Configuration)new XmlSerializer(typeof(Configuration)).Deserialize(new FileStream("config.xml",FileMode.Open));
                MeasureTools.init();
            }
            return config;
        }
        /// <summary>
        /// 进化树
        /// </summary>
        public EvolutionTreeNode getEvolutionRootNode()
        {
            return root;
        }
        /// <summary>
        /// ID生成器
        /// </summary>
        public IdGenerator GetIdGenerator()
        {
            return idGenerator;
        }

        
        #endregion

        #region 进化状态
        public bool paused;
        /// <summary>
        /// 进化年代
        /// </summary>
        protected int generation = 0;
        /// <summary>
        /// 进化年代
        /// </summary>
        public int Generation { get => generation; set => generation = value; }

        /// <summary>
        /// 日志
        /// </summary>
        public ILog logger;
        /// <summary>
        /// 创世染色体
        /// </summary>
        NWSEGenome orginGenome;
        /// <summary>
        /// 创世个体
        /// </summary>
        Network orginNet;
        /// <summary>
        /// 个体集
        /// </summary>
        public List<Network> inds = new List<Network>();
        /// <summary>
        /// 可以完成任务的个体集
        /// </summary>
        public List<Network> taskCompletedNets = new List<Network>();
        /// <summary> 
        /// 事件处理
        /// </summary>
        private EventHandler handler;
        /// <summary>
        /// 交互环境
        /// </summary>
        private IEnv env;
        /// <summary>
        /// 交互环境
        /// </summary>
        public IEnv Env { get=> env; }
        /// <summary>
        /// 运行中
        /// </summary>
        private bool running;
        /// <summary>
        /// 运行中
        /// </summary>
        public bool Running { get => running; }
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="env"></param>
        /// <param name="fitness"></param>
        /// <param name="handler"></param>
        /// <param name="instinctHandler"></param>
        public Session(IEnv env, FitnessHandler fitness,EventHandler handler,InstinctActionHandler instinctHandler)
        {
            this.env = env;
            this.handler = handler;
            Session.instinctActionHandler = instinctHandler;
            Session.fitnessHandler = fitness;
        }

        
        public const String EVT_EVAULATION_BEGIN = "net evaulation begin";
        public const String EVT_LOG = "log";
        public const String EVT_STEP = "step";
        public const String EVT_EVAULATION_END = "net evaulation end";
        public const String EVT_EVAULATION_SUMMARY = "evaulation summary";

        public const String EVT_MSG = "msg";
        public const String EVT_INVAILD_GENE = "invaild_gene";
        public const String EVT_VAILD_GENE = "vaild_gene";
        public const String EVT_IND_COUNT = "ind_count";
        public const String EVT_REABILITY = "reability";
        public const String EVT_GENERATION_END = "generation_end";
        public const String EVT_EVOLUTION_END = "evolution_end";

        /// <summary>
        /// 事件触发函数
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="values"></param>
        public void triggerEvent(String eventName,params Object[] values)
        {
            if (handler == null) return;
            handler(eventName,this.generation,values);
        }

        public String currentSessionPath;
        private void prepareStorePath()
        {
            String path = System.AppDomain.CurrentDomain.BaseDirectory;
            String sessionPath = "session_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            sessionPath = path + "\\" + sessionPath;
            Directory.CreateDirectory(sessionPath);

            currentSessionPath = sessionPath;
        }

        #endregion

        #region 执行
        private Thread thread;
        public void run()
        {
            if (thread != null) return;
            thread = new Thread(new ThreadStart(do_run));
            thread.Start();
        }

        public void stop()
        {
            if (thread == null) return;
            try
            {
                thread.Abort();
            }
            finally
            {
                thread = null;
            }
        }
        public void do_run()
        {
            //初始化
            this.running = true;
            MeasureTools.init();
            logger = LogManager.GetLogger(typeof(Session));
            this.generation = 1;
            prepareStorePath();

            //初始化初代个体
            this.triggerEvent(Session.EVT_LOG, "population init...");
            orginGenome = new NWSEGenomeFactory().createOriginGenome(this);
            orginNet = new Network(orginGenome);
            inds.Clear();
            inds.Add(orginNet);
            this.root = new EvolutionTreeNode(orginNet);

            //反复迭代
            while(true)
            {

                this.triggerEvent(Session.EVT_LOG, "evaulating population...");
                //环境交互过程
                foreach (Network net in inds)
                {
                    if (!double.IsNaN(net.Fitness)) continue;
                    (List<double> obs,List<double> gesture) = env.reset(net);
                    net.Reset();
                    this.triggerEvent(Session.EVT_EVAULATION_BEGIN, net);
                    double reward = 0.0;
                    bool end = false;
                    for (int time = 0; time <= Session.GetConfiguration().evaluation.run_count; time++) 
                    {
                        List<double> inputs = new List<double>(obs);
                        inputs.AddRange(gesture);
                        List<double> actions = net.activate(inputs, time,this, reward);
                        (obs, gesture, actions,reward,end) = env.action(net,actions);
                        this.triggerEvent(Session.EVT_STEP,net,time, inputs,actions, obs, gesture, reward, end);

                        if (end) break;
                        judgePaused();
                    }

                    net.Fitness = Session.fitnessHandler == null ? 0 : Session.fitnessHandler(net, this);

                    this.triggerEvent(Session.EVT_EVAULATION_END, net);
                    judgePaused();
                }

                //最优个体
                int maxFitnessIndex = -1;
                double maxFitness = double.MinValue;
                Network maxFitnessNet = null; 
                for(int i=0;i<inds.Count;i++)
                {
                    if (double.IsNaN(inds[i].Fitness))
                        continue;
                    if(inds[i].Fitness > maxFitness)
                    {
                        maxFitnessIndex = i;
                        maxFitness = inds[i].Fitness;
                        maxFitnessNet = inds[i];
                    }

                }
                 
                this.triggerEvent(Session.EVT_EVAULATION_SUMMARY, maxFitnessNet, maxFitnessIndex, maxFitness);

                maxFitnessNet.save(this.currentSessionPath,this.generation);

                //如果有个体完成了任务，则从种群中移除
                for(int i=0;i<inds.Count;i++)
                {
                    if(inds[i].Fitness >= 14)
                    {
                        inds[i].save(this.currentSessionPath, this.generation);
                        this.taskCompletedNets.Add(inds[i]);
                        inds.RemoveAt(i--);
                    }
                }

                //是否达到最大迭代次数
                this.generation += 1;
                if (this.generation >= Session.GetConfiguration().evolution.iter_count)
                {
                    triggerEvent(EVT_EVOLUTION_END, this);
                    running = false;
                    return;
                }

                //进化过程
                Evolution evolution = new Evolution();
                evolution.execute(inds, this);

                //triggerEvent(EVT_GENERATION_END, this);
                judgePaused();

                running = false;

            }

        }

        
        /// <summary>
        /// 判断是否暂停
        /// </summary>
        /// <returns></returns>
        public bool judgePaused()
        {
            while(paused)
            {
                try
                {
                    Thread.Sleep(1000);

                }catch(ThreadInterruptedException e1)
                {
                    return false;
                }catch(Exception e2)
                {
                    return true;
                }
            }
            return true;
        }

        
    }
    #endregion
}