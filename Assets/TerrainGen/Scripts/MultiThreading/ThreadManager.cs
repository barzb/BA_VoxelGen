using UnityEngine;
using System.Collections;
using System.Linq;
using System.Collections.Generic;

// SINGLETON CLASS FOR THREAD MANAGEMENT
public sealed class ThreadManager
{
    // ATTRIBUTES

    // singleton
    private static ThreadManager instance;

    // list of active working threads
    private static List<WorkingThread> threads;
    private static uint maxThreads;
    private static uint activeThreads;

    private static List<IThreadedJob> jobList;
    private static List<WorkingThread> joblessThreads;

    // PROPERTIES
    public static uint ActiveThreads { get { return activeThreads; } }
    
    // CONSTRUCTOR (private)
    private ThreadManager(uint _maxThreads)
    {
        maxThreads = _maxThreads;
        threads = new List<WorkingThread>();
        jobList = new List<IThreadedJob>();
        joblessThreads = new List<WorkingThread>();
        activeThreads = 0;
    }


    // METHODS

    // only way to create a thread manager. there can only be one!
    public static ThreadManager Instance(uint _maxThreads)
    {
        if(instance == null) {
            instance = new ThreadManager(_maxThreads);
        } else {
            maxThreads = _maxThreads;
        }

        return instance;        
    }

    // CALLED FROM OUTSIDE, ADD JOBS TO THE JOBLIST
    public static void AddJob(IThreadedJob j)
    {
        if (j != null) {
            jobList.Add(j);
        }
    }
    
    // CALLED FROM WORKING THREADS THAT WANT A JOB
    public static void RequestJob(WorkingThread t)
    {
        // thread will get an available job in Update()
        joblessThreads.Add(t);
    }

    // CALLED EVERY FRAME FROM WORLD CLASS
    public void Update()
    {
        uint jobCount = (uint)jobList.Count;
        uint joblessThreadCount = (uint)joblessThreads.Count;

        // NO JOBS AVAILABLE
        if (jobCount == 0)
        {
            // DESTROY JOBLESS THREADS IF THERE ARE ANY
            if(joblessThreadCount > 0)
            {
                // ABORT THREAD AND REMOVE FROM LIST
                while(joblessThreadCount > 0)
                {
                    joblessThreads[0].Abort();
                    threads.Remove(joblessThreads[0]);
                    joblessThreads.RemoveAt(0);
                    joblessThreadCount--;
                }
                activeThreads = (uint)threads.Count;
            }

            return;
        }
        
        // THERE ARE NOT ENOUGH THREADS AND TOO MUCH JOBS
        if((activeThreads < maxThreads) && (jobCount > joblessThreadCount))
        {
            // ADD THREADS AS LONG AS THERE AREN'T MORE THAN MAX
            // AND JOBS FOR THOSE THREADS ARE AVAILABLE
            uint maxThreadsToAdd = maxThreads - activeThreads;
            uint jobsAvailable = jobCount;
            for(int i = 0; i < maxThreadsToAdd; i++)
            {
                if(jobsAvailable > 0) {
                    AddThread();
                    joblessThreadCount++;
                    jobsAvailable--;
                } else {
                    break;
                }
            }
        }

        // THERE ARE JOBS TO BE DONE
        // GIVE JOBLESS THREADS JOBS
        if(joblessThreadCount > 0)
        {
            while(joblessThreadCount > 0)
            {
                // THERE ARE JOBS LEFT TO DO
                if(jobCount > 0)
                {
                    // CHECK IF JOB IS STILL THERE
                    if(jobList[0] == null) {
                        jobList.RemoveAt(0);
                        continue;
                    }

                    // GIVE THE THREAD A JOB
                    joblessThreads[0].GiveJob(jobList[0]);
                    joblessThreads.RemoveAt(0);
                    jobList.RemoveAt(0);

                    jobCount--;
                    joblessThreadCount--;
                }
                // THERE ARE NO MORE JOBS
                else
                {
                    // JOBLESS THREADS WILL BE DELETED NEXT FRAME
                    Debug.Log("No jobs left.");
                    return;
                }
            }
        }
    }

    // Sort the list of jobs
    public static void SortJobs()
    {
        if (jobList.Count > 0)
        {
            // SORT JOBS BY PRIORITY (i.e. distance from chunk to player)
            // the OrderBy method is provided by the Linq extension
            jobList = jobList.OrderBy(job => job.Priority).ToList();
        }
    }

    // abort all active threads
    public static void AbortThreads()
    {
        foreach (WorkingThread t in threads) {
            t.Abort();
        }
    }

    // add a working thread to the list of active threads
    private static WorkingThread AddThread()
    {
        WorkingThread thread = new WorkingThread();
        threads.Add(thread);
        joblessThreads.Add(thread);
        thread.Initialize();

        activeThreads = (uint)threads.Count;

        return thread;
    }
}
