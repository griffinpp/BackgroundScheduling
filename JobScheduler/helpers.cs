using System;

namespace BackgroundScheduling
{
    /// <summary>
    /// a public struct to represent a job in the queue.  Jobs are defined by a name, an Action to perform, whether or not they should repeat, how often they should repeat (in minutes), and a field
    /// to store the last time they were run.  For now, to keep things simple, only Actions are allowed to be run as jobs, so no parameters are allowed.
    /// </summary>
    public struct Job
    {
        public string Name { get; set; }
        public Action Work { get; set; }
        public bool Repeat { get; set; }
        public DateTime StartTime { get; set; }
        public int Interval { get; set; }
        public DateTime LastRun { get; set; }
    }

    public class JobSchedulerEventArgs : EventArgs
    {
        public string JobName { get; set; }
        public DateTime TimeStamp { get; set; }
    }
}
