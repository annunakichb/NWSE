using NWSELib.common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using System.Reflection;
using NWSELib.genome;
using System.Text;

namespace NWSELib
{
    [XmlRoot(ElementName = "configuration")]
    public class Configuration
    {
        [XmlArray(ElementName = "mensurations")]
        [XmlArrayItem(ElementName = "mensuration", Type = typeof(Mensuration))]
        public List<Mensuration> mensurations = new List<Mensuration>();

        [XmlElement(ElementName = "agent")]
        public Agent agent = new Agent();

        [XmlElement(ElementName ="learning")]
        public Learning learning = new Learning();

        [XmlElement(ElementName = "evaluation")]
        public Evaluation evaluation = new Evaluation();

        [XmlElement(ElementName = "evolution")]
        public Evolution evolution = new Evolution();

        [XmlArray(ElementName ="handlers")]
        [XmlArrayItem(ElementName ="handler",Type =typeof(Handler))]
        public List<Handler> handlers = new List<Handler>();

        [XmlElement(ElementName ="view")]
        public View view = new View();

        public Handler Find(String name)
        {
            return handlers.FirstOrDefault<Handler>(h => h.name == name);
        }

        public Handler random_handler(double[] distribution = null)
        {
            if (distribution == null)
            {
                distribution = new double[handlers.Count];
                for (int i = 0; i < distribution.Length; i++) distribution[i] = 1.0 / handlers.Count;
            }
            int index = new Random().Next(0, handlers.Count);
            return handlers[index];
        }

        

        public class Mensuration
        {
            #region 基本信息
            [XmlAttribute]
            public String name;
            [XmlAttribute]
            public String caption;
            [XmlAttribute]
            public int dimension;
            [XmlAttribute]
            public double tolerate;
            [XmlAttribute]
            public String typeName;

            [XmlAttribute]
            public String range = "[0-1]";
            [XmlAttribute]
            public String levels = "";
            [XmlAttribute]
            public String levelNames = "";

            #endregion

            #region 数据范围分级信息
            [XmlIgnore]
            private ValueRange _range;
            [XmlIgnore]
            public ValueRange Range { get => _range == null ? _range = new ValueRange(range) : _range; }

            [XmlIgnore]
            private List<int> _levels;
            [XmlIgnore]
            private List<String[]> _levelNames;
            [XmlIgnore]
            public int Level { get => Levels == null ? 0 : Levels[0]; }
            [XmlIgnore]
            public String[] LevelName { get => LevelNames == null ? null : LevelNames[0]; }
            [XmlIgnore]
            public List<int> Levels
            {
                get
                {
                    if (_levels == null)
                    {
                        if (levels == null || levels.Trim() == "") return null;
                        _levels = this.levels.Split(',').ToList().ConvertAll(l => int.Parse(l));
                    }

                    return _levels;
                }
            }

            [XmlIgnore]
            public List<String[]> LevelNames
            {
                get
                {
                    if (_levelNames == null)
                    {
                        if (levelNames == null || levelNames.Trim() == "") return null;
                        String[] s1 = levelNames.Split(';');
                        _levelNames = new List<string[]>();
                        for (int i = 0; i < s1.Length; i++)
                            _levelNames.Add(s1[i].Split(','));
                    }
                    return _levelNames;
                }
            }
            #endregion
        }


        public class Agent
        {
            [XmlAttribute]
            public int shorttermcapacity;
            [XmlAttribute]
            public int inferencesamples;
            [XmlElement]
            public Receptors receptors = new Receptors();
            [XmlElement]
            public Noise noise;
            
            
        }

        public class Noise
        {
            [XmlAttribute]
            public double sensorNoise;
            [XmlAttribute]
            public double effectorNoise;
            [XmlAttribute]
            public double headingNoise;
        }
        public class Receptors
        {
            [XmlArray(ElementName = "env")]
            [XmlArrayItem(Type = typeof(Sensor), ElementName = "sensor")]
            public List<Sensor> env = new List<Sensor>();

            [XmlArray(ElementName = "gestures")]
            [XmlArrayItem(Type = typeof(Sensor), ElementName = "sensor")]
            public List<Sensor> gestures = new List<Sensor>();

            [XmlArray(ElementName = "actions")]
            [XmlArrayItem(Type = typeof(Sensor), ElementName = "sensor")]
            public List<Sensor> actions = new List<Sensor>();

            public List<Sensor> GetAllSensor()
            {
                List<Sensor> r = new List<Sensor>();
                r.AddRange(env);
                r.AddRange(gestures);
                r.AddRange(actions);
                return r;
            }
            public Sensor GetEnvSensor(String name)
            {
                return env.FirstOrDefault<Sensor>(s => s.name == name);
            }
            public Sensor GetGestureSensor(String name)
            {
                return gestures.FirstOrDefault<Sensor>(s => s.name == name);
            }
            public Sensor GetActionSensor(String name)
            {
                return actions.FirstOrDefault<Sensor>(s => s.name == name);
            }
            public Sensor GetSensor(String name)
            {
                Sensor r = GetEnvSensor(name);
                if (r == null) r = GetGestureSensor(name);
                if (r == null) r = GetActionSensor(name);
                return r;
            }

        }
        public class Sensor
        {
            [XmlAttribute]
            public String name;
            [XmlAttribute]
            public String desc;
            [XmlAttribute]
            public String cataory;
            [XmlAttribute]
            public String group;
            [XmlAttribute]
            public String range;
            [XmlAttribute]
            public String levels;
            [XmlAttribute]
            public String levelNames = "";
            [XmlArray]
            [XmlArrayItem(Type = typeof(SensorProperty))]
            public List<SensorProperty> properties = new List<SensorProperty>();
            [XmlAttribute]
            public int abstractLevel;

            #region 数据范围分级信息
            [XmlIgnore]
            private ValueRange _range;
            [XmlIgnore]
            public ValueRange Range { get => _range == null ? _range = new ValueRange(range) : _range; }

            [XmlIgnore]
            private List<int> _levels;
            [XmlIgnore]
            private List<String[]> _levelNames;
            [XmlIgnore]
            public int Level { get => Levels==null?0:Levels[0]; }
            [XmlIgnore]
            public String[] LevelName { get => LevelNames==null?null:LevelNames[0]; }
            [XmlIgnore]
            public List<int> Levels
            {
                get
                {
                    if (_levels == null)
                    {
                        if (levels == null || levels.Trim() == "") return null;
                        _levels = this.levels.Split(',').ToList().ConvertAll(l => int.Parse(l));
                    }

                    return _levels;
                }
            }

            [XmlIgnore]
            public List<String[]> LevelNames
            {
                get
                {
                    if (_levelNames == null)
                    {
                        if (levelNames == null || levelNames.Trim() == "") return null;
                        String[] s1 = levelNames.Split(';');
                        _levelNames = new List<string[]>();
                        for (int i = 0; i < s1.Length; i++)
                            _levelNames.Add(s1[i].Split(','));
                    }
                    return _levelNames;
                }
            }
            #endregion
        }

        public class SensorProperty
        {
            [XmlAttribute]
            public String cataory;
            [XmlAttribute]
            public String type;
            [XmlAttribute(AttributeName ="value")]
            public String valueText;

            public Vector Value
            {
                get
                {
                    if (valueText == null || valueText.Trim() == "") return null;
                    String[] s1 = valueText.Split(',');
                    if (s1 == null || s1.Length <= 0) return null;
                    List<double> vs = s1.ToList().ConvertAll(s => double.Parse(s));
                    return new Vector(vs.ToArray());
                }
            }
        }

        public class Learning
        {
            
            [XmlElement]
            public LearningInfernece inference = new LearningInfernece();
            [XmlElement]
            public LearningImagination imagination = new LearningImagination();
        }
        public class LearningInfernece
        {
            [XmlAttribute]
            public double accept_prob;
            [XmlAttribute]
            public int accept_max_count;
            [XmlAttribute]
            public double inference_distance;
           
        }

        
        public class LearningImagination
        {
            [XmlAttribute(AttributeName = "abstractLevel")]
            public int abstractLevel;
        }
       
        
        public class Evaluation
        {
            [XmlAttribute]
            public String reward_method;

            [XmlAttribute]
            public double timeStep;

            [XmlAttribute(AttributeName = "gene_reability_range")]
            public String gene_reability_range_str;
            [XmlIgnore]
            public ValueRange gene_reability_range
            {
                get => new ValueRange(gene_reability_range_str);
            }
            [XmlAttribute]
            public int run_count;
            [XmlAttribute]
            public double end_distance;

            [XmlElement]
            public EvaluationReward reward = new EvaluationReward();
            [XmlElement]
            public EvaluationPolicy policy = new EvaluationPolicy();

        }

        public class EvaluationPolicy
        {
            [XmlAttribute]
            public int init_plan_depth = 5;
            [XmlAttribute]
            public String plan_reward_range = "[-50,-50]";
            [XmlAttribute]
            public bool exploration;
           
            [XmlIgnore]
            private ValueRange _plan_reward_range;
            [XmlIgnore]
            public ValueRange PlanRewardRange
            {
                get
                {
                    if(_plan_reward_range == null)
                        _plan_reward_range = new ValueRange(plan_reward_range);
                    return _plan_reward_range;
                }
            }
           
        }

        public class EvaluationReward
        {
            [XmlAttribute]
            public double collision = -50;
            [XmlAttribute]
            public double normal = 0.1;
            [XmlAttribute]
            public double away =1.0;
        }
        public class Evolution
        {
            [XmlAttribute]
            public int propagate_base_count;

            [XmlAttribute]
            public int iter_count;

            [XmlElement]
            public EvolutionSelection selection = new EvolutionSelection();

            [XmlElement]
            public EvolutionMutate mutate = new EvolutionMutate();

        }
        public class EvolutionSelection
        {
            [XmlAttribute]
            public int min_population_capacity;
            [XmlAttribute]
            public int max_population_capacity;
            [XmlAttribute]
            public double reability_lowlimit;
        }
     
        public class EvolutionMutate
        {
            [XmlAttribute]
            public String handlerprob;
           
            [XmlIgnore]
            public List<double> Handlerprob 
            {
                get
                {
                    return Utility.parse<double>(handlerprob);
                }
            }
        }

        public class Handler
        {
            [XmlAttribute]
            public String name;
            [XmlAttribute]
            public int mininputcount;
            [XmlAttribute]
            public int maxinputcount;
            [XmlAttribute]
            public int paramcount;
            [XmlAttribute]
            public String paramrange;
            [XmlIgnore]
            private List<ValueRange> _paramRange;
            [XmlIgnore]
            public List<ValueRange> ParamRange
            {
                get
                {
                    if (_paramRange != null) return _paramRange;
                    if (paramrange == null || paramrange.Trim() == "") return _paramRange;
                    String[] s1 = paramrange.Trim().Split(';');
                    if (s1 == null || s1.Length <= 0) return null;
                    _paramRange = new List<ValueRange>();
                    for (int i=0;i<s1.Length;i++)
                    {
                        _paramRange.Add(new ValueRange(s1[i].Trim()));
                    }
                    return _paramRange;
                }
            }
            [XmlAttribute]
            public String typename;
            [XmlIgnore]
            public Type HandlerType { get => Type.GetType(typename); }
            [XmlAttribute]
            public String selection_prob_range;

            public ValueRange Selection_prob_range
            {
                get => new ValueRange(selection_prob_range);
            }
            public double[] randomParam()
            {
                double[] r = new double[this.paramcount];
                for(int i=0;i<r.Length;i++)
                {
                    r[i] = ParamRange[i].random();
                }
                return r;
            }


        }

        public class View
        {
            [XmlAttribute]
            public int width;
            [XmlAttribute]
            public int height;
        }

    }
}
