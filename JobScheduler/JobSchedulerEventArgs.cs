using System;

namespace BackgroundScheduling
{
    public class JobSchedulerEventArgs : EventArgs
    {
        public string JobName { get; set; }
        public DateTime TimeStamp { get; set; }
    }
}
