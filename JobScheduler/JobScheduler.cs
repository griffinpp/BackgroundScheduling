using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Caching;
using System.Net;

namespace BackgroundScheduling
{
    /// <summary>
    /// This singleton class provides job scheduling capabilities in the background, outside of a page request.  It will check the job queue every RECYCLE_SECONDS and run any 
    /// jobs whose interval is longer than the time between now and their last run.
    /// </summary>
    public sealed class JobScheduler
    {
        #region Singleton Implementation

        private static readonly JobScheduler instance = new JobScheduler();

        static JobScheduler() { }

        //Code here gets executed once, when the singleton is instantiated
        private JobScheduler()
        {
            Start();//start the process on first instantiation
        }

        public static JobScheduler Instance
        {
            get { return instance; }
        }

        #endregion

        #region Properties

        /// <summary>
        /// This is the key that will be used in the cache.  We don't actually care what data we store in the cache, we are only interested in the callback function that executes
        /// when the cached item expires.
        /// </summary>
        private const string CACHE_JOB_KEY = "JobsCacheKey";

        /// <summary>
        /// How often the JobHandler should check the job queue.  Practically, this probably cannot be any less than 30 seconds or so, but may depend on the server
        /// </summary>
        private const int RECYCLE_SECONDS = 60;

        /// <summary>
        /// An internal switch to control whether the JobHandler should run.  This is used to make pausing the JobHandler possible
        /// </summary>
        private bool run = true;

        /// <summary>
        /// A public property that returns whether or not the JobHandler is currently running
        /// </summary>
        public bool IsRunning
        {
            get { return HttpRuntime.Cache[CACHE_JOB_KEY] != null; }
        }

        /// <summary>
        /// The job queue
        /// </summary>
        private List<Job> jobs = new List<Job>();

        /// <summary>
        /// A public property that returns a copy of the job queue, mainly for the ability to check the status of the job queue externally without the ability to alter the queue.
        /// </summary>
        public List<Job> JobQueue
        {
            get
            {
                var queue = new List<Job>();
                foreach (var job in jobs)
                    queue.Add(job);

                return queue;
            }
        }

        #endregion        
        
        #region Events

        public event EventHandler<JobSchedulerEventArgs> Started;
        public event EventHandler<JobSchedulerEventArgs> Stopped;
        public event EventHandler<JobSchedulerEventArgs> Paused;
        public event EventHandler<JobSchedulerEventArgs> JobAdded;
        public event EventHandler<JobSchedulerEventArgs> JobRemoved;
        public event EventHandler<JobSchedulerEventArgs> JobQueueCleared;
        public event EventHandler<JobSchedulerEventArgs> JobQueueStart;
        public event EventHandler<JobSchedulerEventArgs> JobStart;
        public event EventHandler<JobSchedulerEventArgs> JobEnd;
        public event EventHandler<JobSchedulerEventArgs> JobFailed;
        public event EventHandler<JobSchedulerEventArgs> JobQueueEnd;

        private void OnStarted(JobSchedulerEventArgs e)
        {
            if(Started != null)
                Started(this, e);
        }

        private void OnStopped(JobSchedulerEventArgs e)
        {
            if (Stopped != null)
                Stopped(this, e);
        }

        private void OnPaused(JobSchedulerEventArgs e)
        {
            if (Paused != null)
                Paused(this, e);
        }

        private void OnJobAdded(JobSchedulerEventArgs e)
        {
            if (JobAdded != null)
                JobAdded(this, e);
        }

        private void OnJobRemoved(JobSchedulerEventArgs e)
        {
            if (JobRemoved != null)
                JobRemoved(this, e);
        }

        private void OnJobQueueCleared(JobSchedulerEventArgs e)
        {
            if (JobQueueCleared != null)
                JobQueueCleared(this, e);
        }

        private void OnJobQueueStart(JobSchedulerEventArgs e)
        {
            if (JobQueueStart != null)
                JobQueueStart(this, e);
        }

        private void OnJobStart(JobSchedulerEventArgs e)
        {
            if (JobStart != null)
                JobStart(this, e);
        }

        private void OnJobEnd(JobSchedulerEventArgs e)
        {
            if (JobEnd != null)
                JobEnd(this, e);
        }

        private void OnJobFailed(JobSchedulerEventArgs e)
        {
            if (JobFailed != null)
                JobFailed(this, e);
        }

        private void OnJobQueueEnd(JobSchedulerEventArgs e)
        {
            if (JobQueueEnd != null)
                JobQueueEnd(this, e);
        }

        #endregion
        
        #region Private Methods

        /// <summary>
        /// The main process that puts an item into the cache and defines what method to call when that item expires.
        /// </summary>
        private void Recycle()
        {
            if (HttpRuntime.Cache[CACHE_JOB_KEY] != null) return; //don't add the cache item if it already exists

            HttpRuntime.Cache.Insert(CACHE_JOB_KEY, "Jobs", null,
                DateTime.MaxValue, TimeSpan.FromSeconds(RECYCLE_SECONDS),
                CacheItemPriority.Normal,
                new CacheItemRemovedCallback(RunJobs));
        }

        /// <summary>
        /// The actual method that runs every RECYCLE_SECONDS when the cached item expires.  The first thing it does is re-add the item back into the cache, so
        /// that it can self-perpetuate.  The parameters are necessary to match the signature of a CacheItemRemovedCallback delegate, but are not otherwise used.
        /// </summary>
        /// <param name="key">not used</param>
        /// <param name="value">not used</param>
        /// <param name="reason">not used</param>
        private void RunJobs(string key, object value, CacheItemRemovedReason reason)
        {
            if (!run) return; //stop signal has been sent;
            OnJobQueueStart(new JobSchedulerEventArgs() { TimeStamp = DateTime.UtcNow });
            Recycle(); //add the item back into the cache

            //work through the job queue
            lock (jobs)
            {
                for (int i = 0; i < jobs.Count(); i++)
                {
                    var job = jobs[i];

                    //if the interval between now and the last time the job was run is less than the job's specified interval, or we have not reached the
                    //job's specified start time, move to the next job.
                    //Note that job intervals are defined in minutes
                    if ((DateTime.UtcNow - job.LastRun).Minutes < job.Interval || DateTime.UtcNow < job.StartTime) continue;

                    //Otherwise, the job is overdue to run, so invoke its action
                    try
                    {
                        OnJobStart(new JobSchedulerEventArgs() { JobName = job.Name, TimeStamp = DateTime.UtcNow });
                        job.Work.Invoke();
                        OnJobEnd(new JobSchedulerEventArgs() { JobName = job.Name, TimeStamp = DateTime.UtcNow });
                    }
                    catch //avoid a big to-do if the actual Action generates an exception
                    {
                        jobs.Remove(job);
                        OnJobFailed(new JobSchedulerEventArgs() { JobName = job.Name, TimeStamp = DateTime.UtcNow });
                        continue;
                    }

                    //if it's a one-time job, remove it from the queue
                    if (!job.Repeat)
                        jobs.Remove(job);
                    else // otherwise, note the time that it ran and leave it in the queue to be checked next go-around
                    {
                        job.LastRun = DateTime.UtcNow;
                        jobs[i] = job;
                    }
                }
            }
            OnJobQueueEnd(new JobSchedulerEventArgs() { TimeStamp = DateTime.UtcNow });
        }

        /// <summary>
        /// An internal method to check whether a job name already exists in the queue.  This is important, as the name is essentially the key for the job, and there 
        /// should not be duplicate names in the queue
        /// </summary>
        /// <param name="name">the name to check</param>
        /// <returns>true if there is a job in the queue with the specified name, false otherwise</returns>
        private bool JobExists(string name)
        {
            lock (jobs)
            {
                return jobs.Where(a => a.Name == name).Any();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Explicitly start the JobHandler.  
        /// </summary>
        public void Start()
        {
            
            run = true;
            Recycle();
            //Note startup of the JobHandler
            OnStarted(new JobSchedulerEventArgs() { TimeStamp = DateTime.UtcNow });
        }

        /// <summary>
        /// This method will stop checking the job queue until Start() is called, but preserves the job queue in the meantime.
        /// </summary>
        public void Pause()
        {
            //Note pause of the JobHandler
            run = false;
            HttpRuntime.Cache.Remove(CACHE_JOB_KEY);
            OnPaused(new JobSchedulerEventArgs() { TimeStamp = DateTime.UtcNow });
        }

        /// <summary>
        /// Adds a job to the job queue to be executed
        /// </summary>
        /// <param name="name">The name of the job.  This can be used to remove the job from the queue later, if necessary</param>
        /// <param name="work">The actual action the job performs.  This can be a multi-line action, but cannot take any parameters</param>
        /// <param name="repeat">Whether the job should repeat regularly</param>
        /// <param name="delay">How long, in minutes, the JobHandler should wait before executing the job.</param>
        /// <param name="interval">How often, in minutes, the job should repeat.  Since the job scheduler only checks the queue every two minutes, that is effectively as often as a job can run.</param>
        public void AddJob(string name, Action work, bool repeat, int delay, int interval)
        {
            AddJob(name, work, repeat, DateTime.UtcNow.AddMinutes(delay), interval);
        }

        /// <summary>
        /// Adds a job to the job queue to be executed.
        /// </summary>
        /// <param name="name">The name of the job.  This can be used to remove the job from the queue later, if necessary</param>
        /// <param name="work">The actual action the job performs.  This can be a multi-line action, but cannot take any parameters</param>
        /// <param name="repeat">Whether the job should repeat regularly</param>
        /// <param name="start">The UTC datetime when the job should start</param>
        /// <param name="interval">How often, in minutes, the job should repeat.  Since the job scheduler only checks the queue every two minutes, that is effectively as often as a job can run.</param>
        public void AddJob(string name, Action work, bool repeat, DateTime start, int interval)
        {
            if (JobExists(name))
            {
                return;
            }

            var job = new Job() { Name = name, Work = work, Repeat = repeat, StartTime = start, Interval = interval, LastRun = DateTime.UtcNow };
            lock (jobs)
            {
                jobs.Add(job);
            }
            OnJobAdded(new JobSchedulerEventArgs() { JobName = job.Name, TimeStamp = DateTime.UtcNow });
        }

        /// <summary>
        /// Remove a job from the job queue
        /// </summary>
        /// <param name="jobName">The name of the job to remove</param>
        public void RemoveJob(string jobName)
        {
            lock (jobs)
            {
                var job = jobs.FirstOrDefault(a => a.Name == jobName);
                if (String.IsNullOrWhiteSpace(job.Name)) return;

                jobs.Remove(job);
            }

            //Note the job's removal in the event log
            OnJobRemoved(new JobSchedulerEventArgs() { JobName = jobName, TimeStamp = DateTime.UtcNow });
        }

        /// <summary>
        /// Removes all jobs from the job queue
        /// </summary>
        public void ClearJobQueue()
        {
            lock (jobs)
            {
                jobs = new List<Job>();
            }
            //Note clearing of job queue
            OnJobQueueCleared(new JobSchedulerEventArgs() { TimeStamp = DateTime.UtcNow });
        }

        /// <summary>
        /// Removes all jobs from the job queue and stops the JobHandler
        /// </summary>
        public void Stop()
        {
            Pause();
            ClearJobQueue();
            OnStopped(new JobSchedulerEventArgs() { TimeStamp = DateTime.UtcNow });
        }

        #endregion
    }
}