﻿using NWSEExperiment.maze;
using NWSELib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using log4net;

using NWSELib.net;

namespace NWSEExperiment
{
    public partial class MainForm : Form
    {
        public HardMaze maze;
        private CoordinateFrame frame;
        public Session evolutionSession;
        private ILog logger = LogManager.GetLogger(typeof(MainForm));

        private Network optima_net;
        private int optima_generation;

        public MainForm()
        {
            InitializeComponent();

            log4net.Config.XmlConfigurator.Configure();
            this.Width = Session.GetConfiguration().view.width;
            this.Height = Session.GetConfiguration().view.height;


            maze = HardMaze.loadEnvironment("QDMaze.xml");
            this.panel2.Width = Session.GetConfiguration().view.width - maze.AOIRectangle.Width - 10;
            //this.Refresh();

            
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            
        }

        public void panel_Paint(object sender, PaintEventArgs e)
        {
            
            if (maze == null) return;
            if(frame == null)
                frame = new CoordinateFrame(0.0f, maze.AOIRectangle.Y, 1.1f, 0.0f);
            
            maze.draw(e.Graphics, frame);
        }

        private void showOptimaInd(Graphics g)
        {
            if (this.optima_net == null) return;
            Network net = optima_net;
            gbInd.Text = "generation=" + this.optima_generation + ",network=" + net.Id;

            int hspace = 5, vspace = 10;
        }

        private void panel_MouseMove(object sender, MouseEventArgs e)
        {
            if (maze == null || frame == null) return;
            float mazeX, mazeY;
            frame.convertFromDisplay(e.X, e.Y, out mazeX, out mazeY);
            this.statusXY.Text = String.Format("X={0:000.00},Y={1:000.00}", mazeX, mazeY);
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            if (evolutionSession != null) return;
            try
            {
                evolutionSession = new Session();
                evolutionSession.run(this.maze, EventHandler);
            }
            catch(Exception ex)
            {
                logger.Error(ex.StackTrace);
            }
            finally
            {

            }
        }

        public void EventHandler(String eventName, params Object[] states)
        {
            if(eventName == Session.EVT_NAME_DO_ACTION)
            {
                Network net = (Network)states[0];
                maze.updateAgent(net);
                this.Refresh();
            }else if(eventName == Session.EVT_NAME_END_ACTION)
            {
                Network net = (Network)states[0];
                maze.removeAgent(net);
                this.Refresh();
            }
            else if(eventName == Session.EVT_NAME_CLEAR_AGENT)
            {
                maze.clearAgent();
                this.Refresh();
            }else if(eventName == Session.EVT_NAME_OPTIMA_IND)
            {

            }else if(eventName == Session.EVT_NAME_MESSAGE)
            {
                txtMsg.Text += states[0].ToString() + System.Environment.NewLine;
            }
        }

    }
}
