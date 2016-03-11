using UnityEngine;
using System.Collections;
using System.Threading;

// A WORKING THREAD CAN DO JOBS
public sealed class WorkingThread
{
    // systen thread
    private System.Threading.Thread thread = null;
    // true: abort thread in next frame
    private bool abort;
    // handle for locking
    private object handle = new object();

    private IThreadedJob job;
    
    // PROPERTIES
    // returns if the thread is alive or dead
    public bool isAlive { get { return (!abort && thread.IsAlive); } }


    // init this thread
    public void Initialize()
    {
        abort = false;
        job = null;
        // create system thread with the name of the function to execute
        thread = new System.Threading.Thread(ThreadFunction);
        thread.IsBackground = true;
        thread.Start();
    }
    
    // called from ThreadManager, gives the Thread a job to do
    public void GiveJob(IThreadedJob _job)
    {
        lock(handle)
        {
            if(job == null) { 
                job = _job;
            } else {
                Debug.LogError("Giving WorkingThread a job failed! Thread already has a job!");
            }
        }
    }
    
    // will be executed by SystemThread
    private void ThreadFunction()
    {
        // as long as the thread isn't aborted
        while (thread.IsAlive && !abort)
        {
            // wait for job, if you have one -> run the job
            if (job != null)
            {
                job.Run();
                // set reference to null when the job is done
                job = null;
                // request a new job from ThreadManager
                ThreadManager.RequestJob(this);
            }
        }
    }

    // can be called from outside this thread to abort this thread
    public void Abort()
    {
        // thread is marked "aborted" and system thread will be stopped after 
        // the currently executed job is done
        abort = true;
    }
}
