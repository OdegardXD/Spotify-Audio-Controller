namespace Spotify_Audio_Controller
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            notifyIcon1 = new NotifyIcon(components);
            contextMenuStrip1 = new ContextMenuStrip(components);
            exitToolStripMenuItem = new ToolStripMenuItem();
            changeVolumeUpKeybindToolStripMenuItem = new ToolStripMenuItem();
            changeVolumeDownKeybindToolStripMenuItem = new ToolStripMenuItem();
            timer1 = new System.Windows.Forms.Timer(components);
            changeVolumeChangeIncrementalToolStripMenuItem = new ToolStripMenuItem();
            changeSkipNextKeybindToolStripMenuItem = new ToolStripMenuItem();
            changeSkipPrevKeybindToolStripMenuItem = new ToolStripMenuItem();
            contextMenuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // notifyIcon1
            // 
            notifyIcon1.BalloonTipText = "Spotify Controller";
            notifyIcon1.BalloonTipTitle = "Spotify Controller";
            notifyIcon1.ContextMenuStrip = contextMenuStrip1;
            notifyIcon1.Icon = (Icon)resources.GetObject("notifyIcon1.Icon");
            notifyIcon1.Text = "Ode's Spotify Audio Controller";
            notifyIcon1.Visible = true;
            // 
            // contextMenuStrip1
            // 
            contextMenuStrip1.Items.AddRange(new ToolStripItem[] { exitToolStripMenuItem, changeVolumeUpKeybindToolStripMenuItem, changeVolumeDownKeybindToolStripMenuItem, changeSkipNextKeybindToolStripMenuItem, changeSkipPrevKeybindToolStripMenuItem, changeVolumeChangeIncrementalToolStripMenuItem });
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new Size(239, 114);
            // 
            // exitToolStripMenuItem
            // 
            exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            exitToolStripMenuItem.Size = new Size(238, 22);
            exitToolStripMenuItem.Text = "Exit";
            exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;
            // 
            // changeVolumeUpKeybindToolStripMenuItem
            // 
            changeVolumeUpKeybindToolStripMenuItem.Name = "changeVolumeUpKeybindToolStripMenuItem";
            changeVolumeUpKeybindToolStripMenuItem.Size = new Size(238, 22);
            changeVolumeUpKeybindToolStripMenuItem.Text = "Change Volume Up Keybind";
            changeVolumeUpKeybindToolStripMenuItem.Click += changeVolumeUpKeybindToolStripMenuItem_Click;
            // 
            // changeVolumeDownKeybindToolStripMenuItem
            // 
            changeVolumeDownKeybindToolStripMenuItem.Name = "changeVolumeDownKeybindToolStripMenuItem";
            changeVolumeDownKeybindToolStripMenuItem.Size = new Size(238, 22);
            changeVolumeDownKeybindToolStripMenuItem.Text = "Change Volume Down Keybind";
            changeVolumeDownKeybindToolStripMenuItem.Click += changeVolumeDownKeybindToolStripMenuItem_Click;
            // 
            // changeSkipNextKeybindToolStripMenuItem
            // 
            changeSkipNextKeybindToolStripMenuItem.Name = "changeSkipNextKeybindToolStripMenuItem";
            changeSkipNextKeybindToolStripMenuItem.Size = new Size(238, 22);
            changeSkipNextKeybindToolStripMenuItem.Text = "Change Next Song Keybind";
            changeSkipNextKeybindToolStripMenuItem.Click += changeSkipNextKeybindToolStripMenuItem_Click;
            // 
            // changeSkipPrevKeybindToolStripMenuItem
            // 
            changeSkipPrevKeybindToolStripMenuItem.Name = "changeSkipPrevKeybindToolStripMenuItem";
            changeSkipPrevKeybindToolStripMenuItem.Size = new Size(238, 22);
            changeSkipPrevKeybindToolStripMenuItem.Text = "Change Previous Song Keybind";
            changeSkipPrevKeybindToolStripMenuItem.Click += changeSkipPrevKeybindToolStripMenuItem_Click;
            // 
            // timer1
            // 
            timer1.Enabled = true;
            timer1.Interval = 10000;
            timer1.Tick += timer1_Tick;
            // 
            // changeVolumeChangeIncrementalToolStripMenuItem
            // 
            changeVolumeChangeIncrementalToolStripMenuItem.Name = "changeVolumeChangeIncrementalToolStripMenuItem";
            changeVolumeChangeIncrementalToolStripMenuItem.Size = new Size(238, 22);
            changeVolumeChangeIncrementalToolStripMenuItem.Text = "Set Volume Step Size";
            changeVolumeChangeIncrementalToolStripMenuItem.Click += changeVolumeChangeIncrementalToolStripMenuItem_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Name = "Form1";
            Text = "Form1";
            contextMenuStrip1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private NotifyIcon notifyIcon1;
        private ContextMenuStrip contextMenuStrip1;
        private ToolStripMenuItem exitToolStripMenuItem;
        private ToolStripMenuItem changeVolumeUpKeybindToolStripMenuItem;
        private ToolStripMenuItem changeVolumeDownKeybindToolStripMenuItem;
        private System.Windows.Forms.Timer timer1;
        private ToolStripMenuItem changeVolumeChangeIncrementalToolStripMenuItem;
        private ToolStripMenuItem changeSkipNextKeybindToolStripMenuItem;
        private ToolStripMenuItem changeSkipPrevKeybindToolStripMenuItem;
    }
}
