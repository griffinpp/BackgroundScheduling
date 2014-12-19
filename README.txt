BackgroundScheduling solution with JobScheduler project and JobSchedulerTests project

TL;DR: The JobScheduler class in this solution provides a simulated background service for web applications that can run jobs independantly of page requests.  It cannot operate across multiple servers,
       and so is best suited to small to medium sized projects.  It is capable of file and database interaction, and has generally been a godsend in situations where I do not have access to the server
       itself and have no ability to set up an operating system service or other solution to the need for background execution of jobs.



