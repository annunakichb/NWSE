using NWSELib.common;
using NWSELib.env;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using System.Linq;
using NWSELib.net;
using NWSELib;

namespace NWSEExperiment.maze
{
    public class HardMaze : IEnv
    {
        #region 原有代码
         
        #region Instance variables

        // Legacy naming convention used to maintain compatibility with old experiment/CurrentEnvironment files. Do not modify.

        public List<Wall> walls;
        public String name;
        [XmlIgnore]
        public Random rng = new Random();

        public Rectangle AOIRectangle { get; set; }
        public List<Point> POIPosition { get; set; }
        public float maxDistance; // Distance from corner to corner. Determined by AOI.

        public Point2D start_point; //Start location of the agent
        public Point2D goal_point;

        public int group_orientation;
        public int robot_spacing;
        public int robot_heading;
        public int seed;
        public float view_x;
        public float view_y;
        public float view_scale;

        public float stagger;

        #endregion

        

        #region Constructors

        /// <summary>
        /// Creates a small, empty CurrentEnvironment.
        /// </summary>
        public HardMaze()
        {
            //reset();
            AgentVisible = true;
            

        }

        #endregion

        #region Methods
        /// <summary>
        /// Save the CurrentEnvironment to the specified XML file.
        /// </summary>
        public void save(string name)
        {
            System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(this.GetType());
            TextWriter outfile = new StreamWriter(name);
            x.Serialize(outfile, this);
            outfile.Close();
        }

        /// <summary>
        /// Loads a CurrentEnvironment from the specified XML file and initializes it.
        /// </summary>
        public static HardMaze loadEnvironment(string name)
        {
            System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(typeof(HardMaze));
            TextReader infile = new StreamReader(name);
            HardMaze k = (HardMaze)x.Deserialize(infile);
            k.maxDistance = (float)Math.Sqrt(k.AOIRectangle.Width * k.AOIRectangle.Width + k.AOIRectangle.Height * k.AOIRectangle.Height);

            k.name = name;

            Console.WriteLine("# walls: " + k.walls.Count);
            k.rng = new Random(k.seed);

            k.initOptimaTraces();
            return k;
        }

        /// <summary>
        /// Finds the wall with the specified name. Pretty straightforward.
        /// </summary>
        public Wall findWallByName(string name)
        {
            foreach (Wall wall in walls)
            {
                if (wall.Name == name)
                    return wall;
            }
            return null;
        }

        

        /// <summary>
        /// Tests collision between the specified robot and all walls and other robots in the CurrentEnvironment.
        /// </summary>
		public bool robotCollide(Point2D robotStartLocation,Point2D robotStopLocation)
        {
            foreach (Wall wall in walls)
            {
                Line2D robotLine = new Line2D(robotStartLocation, robotStopLocation);
                Point2D intePt = null;
                int intersectionType = wall.Line.intersection(robotLine, out intePt);
                if (intersectionType == 1) return true; 
            }
            return false;
        }


        /// <summary>
        /// Draws the CurrentEnvironment to the screen.
        /// </summary>
        public void draw(Graphics g, CoordinateFrame frame)
        {
            float sx, sy;
            float gx, gy;

            frame.convertToDisplay((float)start_point.X, (float)start_point.Y, out sx, out sy);
            frame.convertToDisplay((float)goal_point.X, (float)goal_point.Y, out gx, out gy);
            Rectangle startrect = new Rectangle((int)sx - 3, (int)sy - 3, 6, 6);
            Rectangle goalrect = new Rectangle((int)gx - 3, (int)gy - 3, 6, 6);

            float rx, ry, rsx, rsy;
            frame.convertToDisplay((float)AOIRectangle.X, (float)AOIRectangle.Y, out rx, out ry);
            frame.convertToDisplayOffset((float)AOIRectangle.Width, (float)AOIRectangle.Height, out rsx, out rsy);
            Rectangle AOIDisplay = new Rectangle((int)rx, (int)ry, (int)rsx, (int)rsy);

            //Display Area of Interest rectangle
            g.DrawRectangle(EngineUtilities.DashedPen, AOIDisplay);

            g.DrawEllipse(EngineUtilities.BluePen, startrect);
            g.DrawEllipse(EngineUtilities.RedPen, goalrect);

            //Display Point Of Interests
            int index = 0;
            foreach (Point p in POIPosition)
            {
                Point p2 = frame.convertToDisplay(p);
                g.DrawEllipse(EngineUtilities.GreendPen, new Rectangle((int)p2.X - 3, (int)p2.Y - 3, 6, 6));
                g.DrawString(index.ToString(), new Font("Verdana", 8), new SolidBrush(Color.Black), p2.X, p2.Y);
                index++;
            }

            foreach (Wall wall in walls)
            {
                wall.draw(g, frame);
            }

            if (!this.AgentVisible) return;

            List<RobotAgent> agents = this.agents.Values.ToList();
            foreach(RobotAgent agent in agents)
            {
                if (!agent.Visible) continue;
                agent.draw(g, frame,agent==CurrentAgent?ShowTrail:false);
            }
        }
        #endregion
        #endregion

        #region 增加的内容

        #region 机器人信息
        [XmlIgnore]
        private ConcurrentDictionary<int, RobotAgent> agents = new ConcurrentDictionary<int, RobotAgent>();
        [XmlIgnore]
        List<List<Point2D>> optimaTraces = new List<List<Point2D>>();
        public bool AgentVisible { get; set; }

        [XmlIgnore]
        public List<RobotAgent> Agents { get => agents.Values.ToList(); }

        RobotAgent currentAgent = null;
        RobotAgent CurrentAgent
        {
            get
            {
                if (agents.Count == 1)
                    return currentAgent = this.agents.Values.ToList()[0];
                return currentAgent;
            }
        }

        public bool ShowTrail { get; set; }

        #endregion

        #region 交互

        public (List<double>, List<double>) reset(Network net,bool clearAgent=true)
        {
            if (clearAgent) this.agents.Clear();

            RobotAgent agent = null; 
            if (!this.agents.ContainsKey(net.Id))
            {
                agent = new RobotAgent(net, this);
                this.agents.TryAdd(agent.getId(), agent);
            }
            agent = this.agents[net.Id];

            agent.reset(this.start_point);
            List<double> obs =agent.getObserve();
            obs.Add(0); //没有发生碰撞冲突
            var poscode = MeasureTools.Position.poscodeCompute(this.AOIRectangle, agent.Location.X, agent.Location.Y);
            obs.Add(poscode.Item1);

            
            List<double> gesture = new List<double>();
            gesture.Add(agent.Heading / (2 * Math.PI));
            
            return (obs,gesture);
        }

        

        public (List<double>, List<double>, List<double>, double, bool) action(Network net, List<double> actions)
        {
            RobotAgent agent = this.agents[net.Id];
            bool noncollision = agent.doAction(actions.ToArray());

            List<double> obs = agent.getObserve();
            obs.Add(agent.HasCollided?1:0);

            var poscode = MeasureTools.Position.poscodeCompute(this.AOIRectangle,agent.Location.X,agent.Location.Y);
            obs.Add(poscode.Item1);

            List<double> gesture = new List<double>();
            gesture.Add(agent.Heading/(2*Math.PI));


            double reward = this.compute_reward(agent);

            bool end = agent.Location.manhattanDistance(this.goal_point) < Session.GetConfiguration().evaluation.end_distance;
            return (obs, gesture, actions,reward,end);
        }

        public static List<double> createInstinctAction(Network net, int time)
        {
            String[] g = { "g1", "g2", "g3", "g4" };
            double[] cAngle = { 0, Math.PI / 2, Math.PI, -Math.PI/ 2 };
            for(int i =0;i<g.Length;i++)
            {
                double v = net.getNode(g[i]).GetValue(time)[0];
                if (v <= 0) continue;
                double angle = v;
                if (v < 0.5) angle += 0.5;
                else if (v > 0.5) angle -= 0.5;
                else angle = 1.0;
                
                return new double[] { angle }.ToList();
            }
            return new double[] { 0.5 }.ToList(); ;
            /*
            //如果面朝目标，则直接向前走
            if (net.getNode("g1").GetValue(time)[0] == 1)
            {
                return new List<double>()
                {
                    //EngineUtilities.RNG.NextDouble()/2.0+0.5,
                    0.5
                };
            }//左转90度
            else if (net.getNode("g2").GetValue(time)[0] == 1)
            {
                return new List<double>()
                {
                    //EngineUtilities.RNG.NextDouble()/2.0+0.5,
                    0.25
                };
            }//转180度
            else if (net.getNode("g3").GetValue(time)[0] == 1)
            {
                return new List<double>()
                {
                    //EngineUtilities.RNG.NextDouble()/2.0,
                    1.0
                };
            }//
            else if (net.getNode("g4").GetValue(time)[0] == 1)
            {
                return new List<double>()
                {
                    //EngineUtilities.RNG.NextDouble()/2.0+0.5,
                    0.75
                };
            }
            else
            {
                return null;
            }
            */
        }


        #endregion

        #region 计算奖励

        private void initOptimaTraces()
        {
            this.optimaTraces = new List<List<Point2D>>();
            List<Point2D> t1 = new List<Point2D>();
            t1.Add(new Point2D(this.start_point.X, this.start_point.Y));
            t1.Add(new Point2D(469,738));
            t1.Add(new Point2D(254, 738));
            t1.Add(new Point2D(244, 1022));
            t1.Add(new Point2D(this.goal_point.X,this.goal_point.Y+150));
            optimaTraces.Add(t1);

            List<Point2D> t2 = new List<Point2D>();
            t2.Add(new Point2D(this.start_point.X, this.start_point.Y));
            t2.Add(new Point2D(469, 738));
            t2.Add(new Point2D(683, 738));
            t2.Add(new Point2D(693, 1022));
            t2.Add(new Point2D(this.goal_point.X, this.goal_point.Y+150));
            optimaTraces.Add(t2);
        }
        public double compute_fitness(Network net,Session session)
        {
            RobotAgent robot = (RobotAgent)this.GetAgent(net.Id);
            List<Point2D> traces = new List<Point2D>(robot.Traces);
            if (traces.Count <= 0) return 0;
            //计算所有轨迹点与目标点的距离是否小于阈值
            double goal_reward = 0, max_goal_reward = 10;
            List<double> goaldis = traces.ConvertAll(t=>t.manhattanDistance(this.goal_point));
            if (goaldis.Min() <= Session.GetConfiguration().evaluation.end_distance)
                goal_reward = max_goal_reward;
            //去掉冗余轨迹点
            for(int i=1;i<traces.Count;i++)
            {
                if(traces[i].X == traces[i-1].X && traces[i].Y == traces[i-1].Y)
                {
                    traces.RemoveAt(i--);
                }
            }

            //与每一个最优轨迹比较,取适应度最高的
            double maxFitness = double.MinValue;
            for (int i = 0; i < optimaTraces.Count; i++)
            {
                List<Point2D> optima = optimaTraces[i];
                int lastOptimaIndex = 0;
                List<double> dis = new List<double>();
                //对每一个轨迹点，计算与其最近的最优轨迹点
                for(int j=1;j<traces.Count;j++)
                {
                    List<double> td = optima.ConvertAll(op => op.distance(traces[j]));
                    int tdminIndex = td.argmin();
                    if (tdminIndex <= lastOptimaIndex) continue;
                    for (int k = lastOptimaIndex + 1; k <= tdminIndex - 1; k++)
                        dis.Add(td[k]);
                    lastOptimaIndex = tdminIndex;
                    dis.Add(td[tdminIndex]);
                    if (lastOptimaIndex == optima.Count - 1) break;
                }
                
                double fitness = lastOptimaIndex + (dis.Count <= 0 ? 0 : Math.Exp(-1 * dis.Average()/100));
                if (fitness > maxFitness)
                    maxFitness = fitness;
            }
            return maxFitness + goal_reward;
        }

        
        public double compute_reward(Agent agent)
        {
            if (Session.GetConfiguration().evaluation.reward_method == "collision")
                return compute_reward_collision(agent);
            //else if (Session.GetConfiguration().evaluation.reward_method == "optiomatraces")
            return compute_reward_optiomatraces((RobotAgent)agent);
        }
        /// <summary>
        /// 如果是
        /// </summary>
        /// <param name="agent"></param>
        /// <returns></returns>
        public double compute_reward_collision(Agent agent)
        {
            RobotAgent robot = (RobotAgent)agent;
            if (robot.PrevCollided && !robot.HasCollided)
                return 1.0;
            else if (!robot.PrevCollided && robot.HasCollided)
                return -50.0;
            else if (robot.PrevCollided && robot.HasCollided)
                return -50.0;
            else return 0.1;
        }
        public double compute_reward_optiomatraces(RobotAgent agent)
        {
            if (agent.PrevCollided && !agent.HasCollided)
                return 1.0;
            else if (!agent.PrevCollided && agent.HasCollided)
                return -50.0;
            else if (agent.PrevCollided && agent.HasCollided)
                return -50.0;

            //计算离Agent的前一个点最近的最优点
            int optima_index=-1,posindex=-1;
            double min = double.MaxValue ;
            for(int i=0;i<optimaTraces.Count;i++)
            {
                for(int j=0;j<optimaTraces[i].Count;j++)
                {
                    double dis = EngineUtilities.euclideanDistance(agent.OldLocation, optimaTraces[i][j]);
                    if (dis < min)
                    {
                        optima_index = i;
                        posindex = j;
                        min = dis;
                    }
                }
            }
            if (posindex >= optimaTraces[optima_index].Count - 1) return 1.0;
            Point2D correctPoint = optimaTraces[optima_index][posindex + 1];

            //计算前一个点行动后的最理想距离（即前一个点向正确点直接走后与正确点的距离）
            Point2D p1 = ((RobotAgent)agent).compute_move_location(agent.OldLocation, correctPoint,1);
            Point2D p2 = ((RobotAgent)agent).compute_move_location(agent.OldLocation, correctPoint, -1);
            double d1 = EngineUtilities.euclideanDistance(p1, correctPoint);
            double d2 = EngineUtilities.euclideanDistance(p2, correctPoint);
            //计算实际点到正确点的距离
            double d3 = EngineUtilities.euclideanDistance(agent.Location, correctPoint);

            return ((d2 - d3) / (d2 - d1))*2.0 + -1.0;
        }

        #endregion

        #region 管理Agent
        /// <summary>
        /// 添加新的Agent
        /// </summary>
        /// <param name="net"></param>
        public void updateAgent(Network net)
        {
            if(!this.agents.ContainsKey(net.Id))
            {
                RobotAgent agent = new RobotAgent(net,this);
                this.agents.TryAdd(net.Id, agent);
            }
        }
        /// <summary>
        /// 删除Agent
        /// </summary>
        /// <param name="net"></param>
        /// <returns></returns>
        public RobotAgent removeAgent(Network net)
        {
            RobotAgent agent = null;
            if (this.agents.ContainsKey(net.Id))
                this.agents.TryRemove(net.Id, out agent);
            return agent;
        }
        /// <summary>
        /// 清空agent
        /// </summary>
        public void clearAgent()
        {
            this.agents.Clear();
        }

        /// <summary>
        /// 查询网络对应的Agent
        /// </summary>
        /// <param name="net"></param>
        /// <returns></returns>
        public Agent GetAgent(int id = 0)
        {
            if(id == 0)
            {
                return this.agents.Count <= 0 ? null : (Agent)this.agents.Values.ToList()[0];
            }
            else
            {
                return this.agents.ContainsKey(id) ? this.agents[id] : null;
            }
        }
        #endregion

        #endregion
    }
}
