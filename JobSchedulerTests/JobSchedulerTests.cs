using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using NUnit.Framework;
using System.Diagnostics;
using BackgroundScheduling;

namespace JobSchedulerTests
{
    [TestFixture]
    public class JobSchedulerTests
    {
        private const string JOB_NAME_1 = "1st Test Job";
        private const string JOB_NAME_2  = "2nd Test Job";

        #region SetUp and TearDown

        //Connect event handlers to events
        [TestFixtureSetUp]
        public void SetUp()
        {
            JobScheduler.Instance.Started += new JobSchedulerEventHandler(JobScheduler_Started);
            JobScheduler.Instance.Stopped += new JobSchedulerEventHandler(JobScheduler_Stopped);
            JobScheduler.Instance.Paused += new JobSchedulerEventHandler(JobScheduler_Paused);
            JobScheduler.Instance.JobAdded += new JobSchedulerEventHandler(JobScheduler_JobAdded);
            JobScheduler.Instance.JobRemoved += new JobSchedulerEventHandler(JobScheduler_JobRemoved);
            JobScheduler.Instance.JobQueueStart += new JobSchedulerEventHandler(JobScheduler_JobQueueStart);
            JobScheduler.Instance.JobQueueEnd += new JobSchedulerEventHandler(JobScheduler_JobQueueEnd);
            JobScheduler.Instance.JobStart += new JobSchedulerEventHandler(JobScheduler_JobStart);
            JobScheduler.Instance.JobEnd += new JobSchedulerEventHandler(JobScheduler_JobEnd);
            JobScheduler.Instance.JobFailed += new JobSchedulerEventHandler(JobScheduler_JobFailed);
        }

        //Stop the JobScheduler and clear its job queue after every test (Remember to restart it explicitly in tests that require it to be running!)
        [TearDown]
        public void CleanUp()
        {
            JobScheduler.Instance.Stop();
        }

        //Disconnect event handlers (NUnit will keep the singleton instance alive between test runs, and the handlers will be connected multiple times otherwise)
        [TestFixtureTearDown]
        public void TearDown()
        {
            JobScheduler.Instance.Started -= JobScheduler_Started;
            JobScheduler.Instance.Stopped -= JobScheduler_Stopped;
            JobScheduler.Instance.Paused -= JobScheduler_Paused;
            JobScheduler.Instance.JobAdded -= JobScheduler_JobAdded;
            JobScheduler.Instance.JobRemoved -= JobScheduler_JobRemoved;
            JobScheduler.Instance.JobQueueStart -= JobScheduler_JobQueueStart;
            JobScheduler.Instance.JobQueueEnd -= JobScheduler_JobQueueEnd;
            JobScheduler.Instance.JobStart -= JobScheduler_JobStart;
            JobScheduler.Instance.JobEnd -= JobScheduler_JobEnd;
            JobScheduler.Instance.JobFailed -= JobScheduler_JobFailed;
        }

        #endregion

        #region Event Handlers

        private void JobScheduler_Started(object sender, JobSchedulerEventArgs e)
        {
            Console.WriteLine("jobscheduler started at: " + e.TimeStamp);
        }

        private void JobScheduler_Stopped(object sender, JobSchedulerEventArgs e)
        {
            Console.WriteLine("jobscheduler stopped at: " + e.TimeStamp);
        }

        private void JobScheduler_Paused(object sender, JobSchedulerEventArgs e)
        {
            Console.WriteLine("jobscheduler paused at: " + e.TimeStamp);
        }

        private void JobScheduler_JobAdded(object sender, JobSchedulerEventArgs e)
        {
            Console.WriteLine("jobscheduler added job \"" + e.JobName + "\" at: " + e.TimeStamp);
        }

        private void JobScheduler_JobRemoved(object sender, JobSchedulerEventArgs e)
        {
            Console.WriteLine("jobscheduler removed job \"" + e.JobName + "\" at: " + e.TimeStamp);
        }

        private void JobScheduler_JobQueueStart(object sender, JobSchedulerEventArgs e)
        {
            Console.WriteLine("jobscheduler started running job queue at: " + e.TimeStamp);
        }

        private void JobScheduler_JobQueueEnd(object sender, JobSchedulerEventArgs e)
        {
            Console.WriteLine("jobscheduler finished running job queue at: " + e.TimeStamp);
        }

        private void JobScheduler_JobStart(object sender, JobSchedulerEventArgs e)
        {
            Console.WriteLine("jobscheduler started running job \"" + e.JobName + "\" at: " + e.TimeStamp);
        }

        private void JobScheduler_JobEnd(object sender, JobSchedulerEventArgs e)
        {
            Console.WriteLine("jobscheduler finished running job \"" + e.JobName + "\" at: " + e.TimeStamp);
        }

        private void JobScheduler_JobFailed(object sender, JobSchedulerEventArgs e)
        {
            Console.WriteLine("job \"" + e.JobName + "\" threw an exception and was removed from the job queue at: " + e.TimeStamp);
        }

        #endregion

        #region Tests

        [Test]
        public void IsRunning()
        {
            JobScheduler.Instance.Start();
            bool cacheExists = HttpRuntime.Cache["JobsCacheKey"] != null;
            Assert.AreEqual(JobScheduler.Instance.IsRunning, cacheExists);

            JobScheduler.Instance.Pause();
            cacheExists = HttpRuntime.Cache["JobsCacheKey"] != null;
            Assert.AreEqual(JobScheduler.Instance.IsRunning, cacheExists);
        }

        [Test]
        public void Start()
        {
            JobScheduler.Instance.Start();
            Assert.IsTrue(JobScheduler.Instance.IsRunning);
        }

        [Test]
        public void AddJob()
        {
            JobScheduler.Instance.AddJob(JOB_NAME_1, () => Console.WriteLine("Job executed"), false, 0, 1);
            bool jobExists = JobScheduler.Instance.JobQueue.Where(a => a.Name == JOB_NAME_1).Any();
            Assert.IsTrue(jobExists);
        }

        [Test]
        public void ClearJobQueue()
        {
            JobScheduler.Instance.AddJob(JOB_NAME_1, () => Console.WriteLine("Job1 executed"), false, 0, 1);
            JobScheduler.Instance.AddJob(JOB_NAME_2, () => Console.WriteLine("Job2 executed"), false, 0, 1);

            JobScheduler.Instance.ClearJobQueue();

            bool jobsExist = JobScheduler.Instance.JobQueue.Any();
            Assert.IsFalse(jobsExist);
        }

        [Test]
        public void AddJobDuplicateName()
        {
            JobScheduler.Instance.AddJob(JOB_NAME_1, () => Console.WriteLine("Job executed"), false, 0, 1);
            JobScheduler.Instance.AddJob(JOB_NAME_1, () => Console.WriteLine("Job executed"), false, 0, 1);
            Assert.AreEqual(1, JobScheduler.Instance.JobQueue.Count());

            JobScheduler.Instance.ClearJobQueue();
        }

        [Test]
        public void Pause()
        {
            JobScheduler.Instance.Start();
            JobScheduler.Instance.AddJob(JOB_NAME_1, () => Console.WriteLine("Job executed"), false, 0, 1);
            JobScheduler.Instance.Pause();
            Assert.IsFalse(JobScheduler.Instance.IsRunning);
            Assert.IsTrue(JobScheduler.Instance.JobQueue.Any());

            JobScheduler.Instance.ClearJobQueue();
        }

        [Test]
        public void RemoveJob()
        {
            JobScheduler.Instance.AddJob(JOB_NAME_1, () => Console.WriteLine("Job executed"), false, 0, 1);
            JobScheduler.Instance.RemoveJob(JOB_NAME_1);
            bool jobExists = JobScheduler.Instance.JobQueue.Where(a => a.Name == JOB_NAME_1).Any();
            Assert.IsFalse(jobExists);
        }

        [Test]
        public void Stop()
        {
            JobScheduler.Instance.AddJob(JOB_NAME_1, () => Console.WriteLine("Job1 executed"), false, 0, 1);
            JobScheduler.Instance.AddJob(JOB_NAME_2, () => Console.WriteLine("Job2 executed"), false, 0, 1);

            JobScheduler.Instance.Stop();

            Assert.IsFalse(JobScheduler.Instance.IsRunning);
            Assert.IsFalse(JobScheduler.Instance.JobQueue.Any());
        }

        [Test][Category("Long Running")]
        public void JobExecuted()
        {
            
            JobScheduler.Instance.AddJob(JOB_NAME_1, () => Console.WriteLine("Job executed"), true, 0, 1);
            JobScheduler.Instance.Start();

            var createdTime = JobScheduler.Instance.JobQueue.First().LastRun;
            System.Threading.Thread.Sleep(80000);//pause to give the scheduler a chance to run through the job queue once
            var lastTime = JobScheduler.Instance.JobQueue.First().LastRun;

            Assert.IsTrue(lastTime > createdTime); //verify that the last run time has been updated
        }

        [Test]
        [Category("Long Running")]
        public void BadJob()
        {
            JobScheduler.Instance.AddJob(JOB_NAME_1, () =>
            {
                var x = 0;
                var y = 10;
                var z = y / x;
            }, false, 0, 1);
            JobScheduler.Instance.Start();

            System.Threading.Thread.Sleep(80000);//pause to give the scheduler a chance to run through the job queue once

            Assert.IsFalse(JobScheduler.Instance.JobQueue.Any()); //The queue should be empty because any job that causes an exception should be removed from the queue
        }
        
        #endregion
    }
}