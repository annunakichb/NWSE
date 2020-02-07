using NWSELib;
using NWSELib.common;
using NWSELib.env;
using NWSELib.net;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NWSEExperiment.maze
{
    #region 感知设备
    public interface IAgentSensor
    {
        void update();
    }
    public static class AgentSensorHelper
    {
        /// <summary>
        /// Casts a ray from the robot's center point according to the specified Angle and returns the Distance to the closest object.
        /// </summary>
        /// <param name="Angle">Angle of sensor, in radians. Also see the "absolute" parameter.</param>
        /// <param name="maxRange">Max Distance that the robot can see in front of itself.</param>
        /// <param name="point">2D location of the robot's center.</param>
        /// <param name="Owner">The currently active robot.</param>
        /// <param name="hit">** Output variable ** - true if another object intersects the cast ray, false otherwise.</param>
        /// <param name="absolute">If false, the Angle parameter is relative to the agent's Heading. Otherwise it follows the standard unit AreaOfImpact.</param>
        /// <returns></returns>
		public static double raycast(this IAgentSensor sensor,HardMaze maze,double angle, double maxRange, Point2D point, RobotAgent owner, out Wall hit, bool absolute = false)
        {
            hit = null;
            Point2D casted = new Point2D(point);
            double distance = maxRange;

            // Cast point casted out from the robot's center point along the sensor direction
            if (!absolute)
            {
                casted.X += Math.Cos(angle + owner.Heading) * distance;
                casted.Y += Math.Sin(angle + owner.Heading) * distance;
            }
            else
            {
                casted.X += Math.Cos(angle) * distance;
                casted.Y += Math.Sin(angle) * distance;
            }

            // Create line segment from robot's center to casted point
            Line2D cast = new Line2D(point, casted);

            // Now do naive detection of collision of casted rays with objects
            // First for all walls
            foreach (Wall wall in maze.walls)
            {
                if (!wall.Visible)
                    continue;
                bool found = false;
                Point2D intersection = wall.Line.intersection(cast, out found);
                if (found)
                {
                    double new_distance = intersection.distance(point);
                    if (new_distance < distance)
                    {
                        distance = new_distance;
                        hit = wall;
                    }
                }
            }
            return distance;

           
        }
    }
    /// <summary>
    /// Rangefinder sensor - returns the DistanceToClosestObject to the closest sensed object within its range.
    /// </summary>
    public class RangeFinder : IAgentSensor
    {
        #region Instance variables

        public RobotAgent Owner;
        public double Angle;
        public double DistanceToClosestObject;
        public double MaxRange;

        #endregion

        #region Constructors

        public RangeFinder(double a, RobotAgent o, double _max_range, double _noise)
        {
            Owner = o;
            Angle = a;
            MaxRange = _max_range;
            DistanceToClosestObject = (-1);
        }

        #endregion

        #region Methods

        public double getActivation()
        {
            return DistanceToClosestObject / MaxRange;
        }
        public double getRawActivation()
        {
            return DistanceToClosestObject;
        }


        public virtual void update()
        {
            HardMaze env = this.Owner.maze;
            Point2D location = new Point2D(Owner.Location.X, Owner.Location.Y);
            Wall hit;
            DistanceToClosestObject = this.raycast(env,Angle, MaxRange, location, Owner, out hit);
           
        }

        

        #endregion
    }

    /// <summary>
	/// Basic pie slice sensor.
	/// </summary>
	public class Radar : IAgentSensor
    {
        #region Instance variables

        public RobotAgent Owner;
        string Type = "goal";
        /// <summary>
        /// 扫描范围：角度1
        /// </summary>
        public double StartAngle;  // Both angles are in radians
        /// <summary>
        /// 扫描范围：角度2；
        /// </summary>
        public double EndAngle;
        
        /// <summary>
        /// 扫描有效距离
        /// </summary>
        public double MaxRange;
        public double Noise;

        public double Distance;
        public double Activation = 0;
        

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor.
        /// </summary>
        public Radar(double startAngle, double endAngle, RobotAgent owner, string type = "goal", double maxRange = 150)
        {
            Owner = owner;
            StartAngle = startAngle;
            EndAngle = endAngle;
            MaxRange = maxRange;
            Distance = (-1);
            Noise = 0.0;
            Type = type;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Returns the current sensor activation.
        /// </summary>
        public double getActivation()
        {
            return Activation;
        }

        /// <summary>
        /// Returns the raw sensor activation.
        /// </summary>
		public double getRawActivation()
        {
            return Activation;
        }

        /// <summary>
        /// Updates the sensor based on the current state of the environment.
        /// </summary>
        public virtual void update()
        {
            HardMaze env = this.Owner.maze;
            Activation = 0;
            Point2D temp;
            double dist;
            double angle;
            
            if (Type == "goal")
                temp = new Point2D(env.goal_point.X, env.goal_point.Y);
            else
                temp = new Point2D(env.start_point.X, env.start_point.Y);

            dist = EngineUtilities.euclideanDistance(temp, new Point2D(Owner.AreaOfImpact.Position.X, Owner.AreaOfImpact.Position.Y));

            //translate with respect to location of navigator
            temp.X -= (float)Owner.AreaOfImpact.Position.X;
            temp.Y -= (float)Owner.AreaOfImpact.Position.Y;

            angle = (float)temp.angle();

            angle *= 57.297f;
            angle -= (float)Owner.Heading * 57.297f;
            

            while (angle > 360)
                angle -= 360;
            while (angle < 0)
                angle += 360;

            if (angle >= StartAngle && angle < EndAngle)
            {
                if (Type == "goal")
                    Activation = 1.0 - (dist > MaxRange ? 1.0 : dist / MaxRange);
                else
                    Activation = 1.0;
            }

            else if (angle + 360.0 >= StartAngle && angle + 360.0 < EndAngle)
            {
                if (Type == "goal")
                    Activation = 1.0 - (dist > MaxRange ? 1.0 : dist / MaxRange);
                else
                    Activation = 1.0;
            }
        }

        

        #endregion
    }

    #endregion

    #region 机器人
    public class RobotAgent : Agent
    {
        #region 内部状态
        public const double RADIUS = 10;
        protected double velocity;
        protected double heading;
        List<Point2D> traces = new List<Point2D>();
        protected Circle2D areaOfImpact;
        public double Velocity { get => velocity; set => velocity = value; }
        public double Heading { get => heading; set => heading = value; }
        public Point2D Location { get { return traces==null|| traces.Count<=0?null: traces[traces.Count-1]; } set { traces.Add(value);  } }

        public override bool Visible { get; set; }
        public Point2D OldLocation 
        { 
            get 
            {
                if (traces == null || traces.Count <= 0) return null;
                else if (traces.Count == 1) return this.traces[0];
                else return this.traces[this.traces.Count-2];
            }  
        }
        public List<Point2D> Traces { get => this.traces; }

        public Circle2D AreaOfImpact { get => areaOfImpact; set => areaOfImpact = value; }
        
        public bool Stopped;
        public bool HasCollided;
        public bool PrevCollided;

        
        #endregion

        #region 内部组成
        public Network net;
        public override int getId() { return net.Id; }
        public HardMaze maze;
        public List<IAgentSensor> WallSensors;
        public List<IAgentSensor> GoalSensors;
        public List<IAgentSensor> CompassSensors;


        // Sensor and effector Noise
        protected double HeadingNoise;
        protected double EffectorNoise;
        protected double SensorNoise;

        int RangefinderRange = 100;
        int PiesliceRange = 2000;

        #endregion

        public double Timestep;
        float[] Inputs;
        public Random rng = new Random();


        

        public List<float> InputValues = new List<float>();

        /// <summary>
        /// Applies Noise to the Heading per the HeadingNoise property.
        /// </summary>
        public double noisyHeading()
        {
            return Heading + 0.1 * (rng.NextDouble()<=0.5 ? 1 : -1) * rng.Next(0, (int)HeadingNoise) / 100.0;
        }

        

        public void reset(Point2D initpos)
        {
            this.traces.Clear();
            Location = new Point2D(initpos.X,initpos.Y);
        }

        static Pen dash_pen = null;
        /// <summary>
        /// Initialize the robot.
        /// </summary>
        public RobotAgent(Network net, HardMaze maze)
        {
            if(dash_pen == null)
            {
                dash_pen = new Pen(Color.Green, 1);
                dash_pen.DashStyle = DashStyle.Dot;
               
            }
            this.net = net;
            double locationX = maze.start_point.X;
            double locationY = maze.start_point.Y;
            double heading = maze.robot_heading;

            double sensorNoise = Session.GetConfiguration().agent.noise.sensorNoise;
            double effectorNoise = Session.GetConfiguration().agent.noise.effectorNoise;
            double headingNoise =Session.GetConfiguration().agent.noise.headingNoise;

            double timeStep = Session.GetConfiguration().evaluation.timeStep;

            Location = new Point2D(locationX, locationY);
            AreaOfImpact = new Circle2D(Location, RADIUS);


            //Heading =EngineUtilities.angletoradian(heading);
            Heading = heading/ MeasureTools.DRScale;
            Velocity = 0.0;
            HasCollided = false;
            this.Timestep = timeStep;
            this.maze = maze;

            this.SensorNoise = sensorNoise;
            this.EffectorNoise = effectorNoise;
            this.HeadingNoise = headingNoise;

            this.initSensors();
 
        }
        

        /// <summary>
        /// Initializes the robot's GoalSensors and positions them on the robot's body.
        /// </summary>
        public void initSensors(int numWallSensors=5)
        {
            // Set up the 5 front-facing rangefinders
            WallSensors = new List<IAgentSensor>();
            double delta = 180.0 / 4; // in degrees
            delta /= 57.29578f; //45 /=(180/pi) convert degrees to radians because that is what RangeFinders take
            double startAngle = 4.71239f; //270 start the first rangefinder facing due left
            for (int j = 0; j < numWallSensors; j++)
            {
                WallSensors.Add(new RangeFinder(startAngle, this, RangefinderRange, 0.0));
                startAngle += delta;
            }

            // Set up the single rear-facing rangefinder
            startAngle -= delta; // Set the StartAngle to facing due right
            startAngle += (90 / 57.29578f); //1.57 (convert 90 degrees to radians)
            WallSensors.Add(new RangeFinder(startAngle, this, RangefinderRange, 0.0));

            // Set up the POI radars
            GoalSensors = new List<IAgentSensor>();
            GoalSensors.Add(new Radar(315, 45, this, "goal", PiesliceRange)); // front
            GoalSensors.Add(new Radar(45, 135, this, "goal", PiesliceRange)); // right
            GoalSensors.Add(new Radar(135, 225, this, "goal", PiesliceRange)); // rear
            GoalSensors.Add(new Radar(225, 315, this, "goal", PiesliceRange)); // left
            

            // Set up the Northstar GoalSensors
            CompassSensors = new List<IAgentSensor>();
            CompassSensors.Add(new Radar(45, 135, this, "northstar", PiesliceRange * 2)); // front   // Note: maxRange extended 2x purely for visual purposes.
            CompassSensors.Add(new Radar(135, 225, this, "northstar", PiesliceRange * 2)); // right
            CompassSensors.Add(new Radar(225, 315, this, "northstar", PiesliceRange * 2)); // rear
            CompassSensors.Add(new Radar(315, 45, this, "northstar", PiesliceRange * 2)); // left

            // Initialize a persistent inputs array so we don'type have to keep re-allocating memory
            Inputs = new float[WallSensors.Count + GoalSensors.Count];

            updateSensors();
        }

        /// <summary>
        /// Updates all of the robot's GoalSensors. Called on each simulator tick.
        /// </summary>
        /// <param name="env">The simulator CurrentEnvironment.</param>
        /// <param name="robots">List of other robots in the CurrentEnvironment. Not actually used in this function, only included for inheritance reasons.</param>
        /// <param name="cm">The CurrentEnvironment's collision manager.</param>
        public void updateSensors()
        {

            // Clear out GoalSensors from last time
            InputValues.Clear();
            foreach (Radar r in GoalSensors)
            {
                r.Activation = 0;
            }
            foreach (Radar r in CompassSensors)
            {
                r.Activation = 0;
            }

            // Update regular (target) GoalSensors
            double angle = 0;
            Point2D temp;
            temp = new Point2D(maze.goal_point.X, maze.goal_point.Y);
            temp.X -= (float)Location.X;
            temp.Y -= (float)Location.Y;
            //temp.X -= (float)AreaOfImpact.Position.X;
            //temp.Y -= (float)AreaOfImpact.Position.Y;

            angle = (float)temp.angle();
            angle -= Heading;
            angle *= 57.297f; // convert radians to degrees

            while (angle > 360)
                angle -= 360;
            while (angle < 0)
                angle += 360;

            foreach (Radar r in GoalSensors)
            {
                if ((angle < 45 || angle >= 315) && r.StartAngle == 315)
                {
                    r.Activation = angle / 360;
                    break;
                }
                else if (angle >= r.StartAngle && angle < r.EndAngle)
                {
                    r.Activation = angle / 360;
                    break;
                }
            }

            

            // Update the compass/northstar GoalSensors
            // Note: This is trivial compared to rangefinder updates, which check against all walls for collision. No need to gate it to save CPU.
            double northstarangle = Heading / 57.297;
            northstarangle *= 57.297f; // convert radians to degrees

            while (northstarangle > 360)
                northstarangle -= 360;
            while (northstarangle < 0)
                northstarangle += 360;

            foreach (Radar r in CompassSensors)
            {
                // First, check if the Angle is in the wonky pie slice
                if ((northstarangle < 45 || northstarangle >= 315) && r.StartAngle == 315)
                {
                    r.Activation = 1;
                    break;
                }

                // Then check the other pie slices
                else if (northstarangle >= r.StartAngle && northstarangle < r.EndAngle)
                {
                    r.Activation = 1;
                    break;
                }
            }

            // Update the rangefinders
            foreach (RangeFinder r in WallSensors)
            {
                r.update();
            }

        }
        /*public void updateSensors()
        {

            // Clear out GoalSensors from last time
            InputValues.Clear();
            foreach (Radar r in GoalSensors)
            {
                r.Activation = 0;
            }
            foreach (Radar r in CompassSensors)
            {
                r.Activation = 0;
            }

            // Update regular (target) GoalSensors
            double angle = 0;
            Point2D temp;
            temp = new Point2D(maze.goal_point.X, maze.goal_point.Y);
            temp.X -= (float)Location.X;
            temp.Y -= (float)Location.Y;
            //temp.X -= (float)AreaOfImpact.Position.X;
            //temp.Y -= (float)AreaOfImpact.Position.Y;

            angle = (float)temp.angle();
            angle -= Heading;
            angle *= 57.297f; // convert radians to degrees

            while (angle > 360)
                angle -= 360;
            while (angle < 0)
                angle += 360;

            foreach (Radar r in GoalSensors)
            {
                if ((angle < 45 || angle >= 315) && r.StartAngle == 315)
                {
                    r.Activation = Math.Cos(angle);
                    //if (angle >= 315) r.Activation = -1 * r.Activation;
                    //r.Activation = r.Activation / (Math.PI * 2) + 0.5;
                    r.Activation = r.Activation / 2 + 0.5;
                }
                else if (angle >= r.StartAngle && angle < r.EndAngle)
                {
                    double c = (r.EndAngle - r.StartAngle) / 2;
                    r.Activation = Math.Cos(Math.Abs(angle - c));
                    if (angle >= c) r.Activation = -1 * r.Activation;
                    r.Activation = r.Activation / 2 + 0.5;
                }
            }


            foreach (Radar r in GoalSensors)
                // First, check if the Angle is in the wonky pie slice
                if ((angle < 45 || angle >= 315) && r.StartAngle == 315)
                {
                    //r.Activation = 1;
                    r.Activation = Math.Cos(angle);
                    break;
                }

                // Then check the other pie slices
                else if (angle >= r.StartAngle && angle < r.EndAngle)
                {
                    //r.Activation = 1;
                    r.Activation = Math.Cos(Math.Abs(angle - (r.EndAngle - r.StartAngle) / 2));
                    break;
                }
        }

        // Update the compass/northstar GoalSensors
        // Note: This is trivial compared to rangefinder updates, which check against all walls for collision. No need to gate it to save CPU.
        double northstarangle = Heading / 57.297;
        northstarangle *= 57.297f; // convert radians to degrees

            while (northstarangle > 360)
                northstarangle -= 360;
            while (northstarangle< 0)
                northstarangle += 360;

            foreach (Radar r in CompassSensors)
            {
                // First, check if the Angle is in the wonky pie slice
                if ((northstarangle< 45 || northstarangle >= 315) && r.StartAngle == 315)
                {
                    r.Activation = 1;
                    break;
                }

                // Then check the other pie slices
                else if (northstarangle >= r.StartAngle && northstarangle<r.EndAngle)
                {
                    r.Activation = 1;
                    break;
                }
            }

            // Update the rangefinders
            foreach (RangeFinder r in WallSensors)
            {
                r.update();
            }

        }*/

        public override List<double> getObserve()
        {

            double[] obs = new double[this.WallSensors.Count + this.GoalSensors.Count];
            // Update wall sensor inputs
            for (int j = 0; j < WallSensors.Count; j++)
            {
                obs[j] = ((RangeFinder)WallSensors[j]).DistanceToClosestObject;
                obs[j] = obs[j] / RangefinderRange;
            }

            // Update pie slice sensor inputs
            for (int j = WallSensors.Count; j < WallSensors.Count + GoalSensors.Count; j++)
            {
                obs[j] = (float)((Radar)GoalSensors[j - WallSensors.Count]).getActivation();
                if (obs[j] > 1.0) obs[j] = 1.0f;
            }

            return obs.ToList();
        }


        
        /// <summary>
        /// Enacts agent behavior based on neural network outputs. Movement uses instant/reactive turning and acceleration-based movement (hybrid approach) to encourage robots to move at the same (maximum) Velocity.
        /// </summary>
        /// <param name="outputs">Neural network outputs.</param>
        /// <param name="Timestep">The current timestep.</param>
        public override bool doAction(double[] actions)
        {
            double timeStep = this.Timestep;

            Velocity = Max_Speed_Action;
            /*
            if (actions[0] > 0.5) Velocity = Max_Speed_Action;
            else if (actions[0] < 0.5) Velocity = -Max_Speed_Action;
            else Velocity = 0;*/

             //Velocity = (actions[0] - 0.5) * Max_Speed_Action;
             //if (Velocity > 6.0) Velocity = 6.0;
             //if (Velocity < -6.0) Velocity = (-6.0);
            Heading += (actions[0] - 0.5) * Max_Rotate_Action * 2;
            if (Heading < 0) Heading += 2 * Math.PI;
            if (Heading > 2 * Math.PI) Heading -= 2 * Math.PI;

            //double tempHeading = noisyHeading();
            //Heading = tempHeading;
            double dx = Math.Cos(Heading) * Velocity * Timestep;
            double dy = Math.Sin(Heading) * Velocity * Timestep;
            Point2D tempLocation = new Point2D(Location.X + dx, Location.Y + dy);

            PrevCollided = HasCollided;
            HasCollided = this.maze.robotCollide(Location, tempLocation);
            if (HasCollided)
            {
                updateSensors();
                return false;
            }
            updatePosition(timeStep);
            updateSensors();

            return true;
        }

        /// <summary>
        /// 计算从起点到终点移动或者反方向移动后的位置
        /// </summary>
        /// <param name="begin"></param>
        /// <param name="end"></param>
        /// <param name="v">1为正方向</param>
        /// <returns></returns>
        public Point2D compute_move_location(Point2D begin, Point2D end, int v)
        {
            double angle = 0;
            Point2D temp = null;
            if (v == 1)
            {
                temp = new Point2D(end.X, end.Y);
                temp.X -= (float)begin.X;
                temp.Y -= (float)begin.Y;
            }
            else
            {
                temp = new Point2D(begin.X, begin.Y);
                temp.X -= (float)end.X;
                temp.Y -= (float)end.Y;
            }
            angle = (float)temp.angle();

            double dx = Math.Cos(angle) *(RobotAgent.Max_Speed_Action/2) * Timestep;
            double dy = Math.Sin(angle) * (RobotAgent.Max_Speed_Action/2) * Timestep;
            return new Point2D(begin.X + dx, begin.Y + dy);
        }
        public void updatePosition(double Timestep)
        {

            OldLocation.X = Location.X;
            OldLocation.Y = Location.Y;

            //update current coordinates (may be revoked if new position forces collision)
            if (!Stopped)
            {
                double tempHeading = noisyHeading();
                Heading = tempHeading;
                double dx = Math.Cos(tempHeading) * Velocity * Timestep;
                double dy = Math.Sin(tempHeading) * Velocity * Timestep;
                Location = new Point2D(Location.X + dx, Location.Y + dy);
            }
        }

        

        internal void draw(Graphics g, CoordinateFrame frame,bool showtrail=false)
        {
            //画位置
            Point2D p2 = frame.convertToDisplay(this.Location);
            g.FillEllipse(System.Drawing.Brushes.Red, new Rectangle((int)p2.X - 3, (int)p2.Y - 3, 6, 6));

            //画方向
            double dx = Math.Cos(Heading) * 30;
            double dy = Math.Sin(Heading) * 30;
            Point2D dLocation = new Point2D(Location.X + dx, Location.Y + dy);
            dLocation = frame.convertToDisplay(dLocation);
            g.DrawLine(System.Drawing.Pens.Red, (float)p2.X, (float)p2.Y, (float)dLocation.X, (float)dLocation.Y);

            //画轨迹
            if (showtrail)
            {
                for(int i=0;i<this.traces.Count-1;i++)
                {
                    Point2D l1 = frame.convertToDisplay(this.traces[i]);
                    Point2D l2 = frame.convertToDisplay(this.traces[i+1]);
                    g.DrawLine(System.Drawing.Pens.Black, (float)l1.X, (float)l1.Y, (float)l2.X, (float)l2.Y);
                }
            }
        }

        Font eva_font = new Font(FontFamily.GenericSerif, 9);
        Brush eva_brush = new SolidBrush(Color.Black);
        public void drawEvaulation(Graphics g, CoordinateFrame frame)
        {
            if (this.net.lastActionPlan == null) return;
            List<(List<double>,double,List<Vector>)> records = this.net.lastActionPlan.actionEvaulationRecords;
            if (records == null || records.Count <= 0) return;
            //records = records.FindAll(r => !double.IsNaN(r.Item2));
            //if (records == null || records.Count <= 0) return;

            List<double> evas = records.ConvertAll(e => e.Item2);
            double min = evas.Min();
            double max = evas.Max();
            evas = evas.ConvertAll(e => double.IsNaN(e)?e:(e - min) / (max - min+0.00001));
            List<int> length = evas.ConvertAll(e => double.IsNaN(e)?100:(int)(e / 200 + 100));
            
            for(int i=0;i< records.Count;i++)
            {
                (List<double>, double,List<Vector>) r = records[i];
                double action = r.Item1[0];
                double futureHeading  = Heading + (action - 0.5) * Max_Rotate_Action * 2;
                if (futureHeading < 0) futureHeading += 2 * Math.PI;
                
                double dx = Math.Cos(futureHeading) * length[i];
                double dy = Math.Sin(futureHeading) * length[i];

                Point2D p2 = new Point2D(this.Location.X+dx,this.Location.Y+dy);

                Point2D l1 = frame.convertToDisplay(this.Location);
                Point2D l2 = frame.convertToDisplay(p2);
                g.DrawLine(dash_pen, (float)l1.X, (float)l1.Y, (float)l2.X, (float)l2.Y);
                //g.DrawLine(System.Drawing.Pens.Red, (float)l1.X, (float)l1.Y, (float)l2.X, (float)l2.Y);
                g.DrawString(double.IsNaN(records[i].Item2)?"Nan":records[i].Item2.ToString("F2"), eva_font, eva_brush, (float)l2.X, (float)l2.Y);
            }
        }
        #endregion
    }
}
