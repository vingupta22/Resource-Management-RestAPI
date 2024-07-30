using System;
using System.IO;
using System.Xml;
using System.Threading;
using System.Collections;
using System.Diagnostics;
// using XactiMed;
using XactiMed.XApps;
using XactiMed.XConnect;
using Azure.Messaging.ServiceBus;
using NuGet.Protocol.Plugins;
using static System.Net.Mime.MediaTypeNames;

namespace ReportPickupService
{
    /// <summary>
    /// Defines the Pickup result status
    /// </summary>
    /// 


    // the client that owns the connection and can be used to create senders and receivers
    ServiceBusClient client;

    // the sender used to publish messages to the queue
    ServiceBusSender sender;

    // number of messages to be sent to the queue
    const int numOfMessages = 3;

    // The Service Bus client types are safe to cache and use as a singleton for the lifetime
    // of the application, which is best practice when messages are being published or read
    // regularly.
    //
    // set the transport type to AmqpWebSockets so that the ServiceBusClient uses the port 443. 
    // If you use the default AmqpTcp, you will need to make sure that the ports 5671 and 5672 are open

    // TODO: Replace the <NAMESPACE-CONNECTION-STRING> and <QUEUE-NAME> placeholders
    var clientOptions = new ServiceBusClientOptions()
    {
        TransportType = ServiceBusTransportType.AmqpWebSockets
    };
    client = new ServiceBusClient("<NAMESPACE-CONNECTION-STRING>", clientOptions);
    sender = client.CreateSender("<QUEUE-NAME>");

    // create a batch 
    using ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync();

    for (int i = 1; i <= numOfMessages; i++)
    {
        // try adding a message to the batch
        if (!messageBatch.TryAddMessage(new ServiceBusMessage($"Message {i}")))
        {
            // if it is too large for the batch
            throw new Exception($"The message {i} is too large to fit in the batch.");
}
    }

    try
{
    // Use the producer client to send the batch of messages to the Service Bus queue
    await sender.SendMessagesAsync(messageBatch);
    Console.WriteLine($"A batch of {numOfMessages} messages has been published to the queue.");
}
finally
{
    // Calling DisposeAsync on client types is required to ensure that network
    // resources and other unmanaged objects are properly cleaned up.
    await sender.DisposeAsync();
    await client.DisposeAsync();
}



public enum PickupResult
{
    Success,
    Failure,
    Ignored,
}

/// <summary>
/// Summary description for ReportPickupService.
/// </summary>
public class ReportPickupService : BaseTaskService
{

    FileSystemWatcher m_watcher = null;
    AutoResetEvent m_reportEvent = null;
    RegisteredWaitHandle m_waitHandle = null;
    Queue m_reportQueue = null;
    Hashtable m_mappings = null;
    UncShareHelper m_shareHelper = null;
    object m_lock = new object();

    int m_nInitWatcherCount = 0;
    string m_sLockName = null;
    string m_sLockUser = null;

    const int RetryTimeout = 15 * 1000;
    const int MaxInitWatcherCount = 20;

    #region Standard BaseTaskService Required Overrides

    public ReportPickupService(System.ServiceProcess.ServiceBase svc) :
        base(svc)
    {
        // Nothing else to do, yet...
    }

    public ReportPickupConfigFile Cfg
    {
        get
        {
            return m_cfgFile as ReportPickupConfigFile;
        }
    }

    /// <summary>
    /// Creates new OamDB monitor log object
    /// </summary>
    /// <returns>OamDB log object</returns>
    public override OamDB.MonitorLog NewMonitorLog()
    {
        OamDB.MonitorLog monlog = new OamDB.MonitorLog();
        monlog.AppID = ApplicationID.XConnect;
        monlog.ServiceName = ServiceName;
        monlog.SetMachine(m_nMachineID, HostName);
        if (Log.Filename != null) monlog.AddLogFilename(Log.Filename);
        return monlog;
    }

    /// <summary>
    /// Creates new ReportPickup Configuration file
    /// </summary>
    /// <param name="sFilename">Filename for configuration</param>
    /// <returns>ReportPickupConfigFile object </returns>
    protected override BaseTaskConfig NewConfigFile(string sFilename)
    {
        return new ReportPickupConfigFile(sFilename);
    }

    #endregion // Standard BaseTaskService Required Overrides

    /// <summary>
    /// Specifies the processing that occurs when SCM(Service Control Manager) receives a Start command.
    /// It specifies the behavior of the service
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    protected override bool OnStart(CommandLineArgs args)
    {
        try
        {
            OpenConfigFile(true);
            OpenLogFile();
        }
        catch (Exception x)
        {
            LogEvent(EventLogEntryType.Error, "{0} aborted:\r\n{1}", ServiceName, x.ToString());
            return false;
        }
        Log.WriteLine("{0} starting up...", ServiceName);
        Log.WriteLine("Configuration: \"{0}\"", Cfg.Filename);

        try
        {
            if (Cfg.SecureLock)
            {
                string sLockName = "ReportPickupService";
                string sLockUser = Guid.NewGuid().ToString();
                if (!m_jobdb.SecureLock(sLockName, sLockUser))
                {
                    throw new Exception(string.Format("Unable to obtain GlobalLock: \"{0}\".", sLockName));
                }
                m_sLockName = sLockName;
                m_sLockUser = sLockUser;
            }

            m_nMachineID = m_jobdb.AssertMachine(System.Net.Dns.GetHostName(), null);
            LoadMappingsTable();

            m_reportQueue = new Queue();
            m_reportEvent = new AutoResetEvent(false);
            m_waitHandle = ThreadPool.RegisterWaitForSingleObject(m_reportEvent, new WaitOrTimerCallback(OnReportEvent), m_reportEvent, RetryTimeout, false);

            InitializeWatcher();
        }
        catch (Exception x)
        {
            LogEvent(EventLogEntryType.Error, "{0} aborted:\r\n{1}", ServiceName, x.ToString());
            Log.WriteLine(x.ToString());
            return false;
        }

        LogEvent(EventLogEntryType.Information, "{0} started.", ServiceName);
        Log.WriteLine("{0} started.", ServiceName);
        PostMonitorLog(OamDB.Severity.Normal, 100);
        return true;
    }

    /// <summary>
    /// Specifies the processing that occurs when SCM(Service Control Manager) receives a Stop command
    /// </summary>
    protected override void OnStop()
    {
        Log.WriteLine("{0} shutting down...", ServiceName);
        if (m_cfgFile != null) m_cfgFile.EnableRaisingEvents = false;

        try
        {
            if (m_watcher != null) m_watcher.EnableRaisingEvents = false;
            if (m_waitHandle != null) m_waitHandle.Unregister(m_reportEvent);
            if (m_reportEvent != null) m_reportEvent.Close();

            ProcessReports();
        }
        catch (Exception x)
        {
            Log.WriteLine(x);
        }
        finally
        {
            if (m_sLockName != null && m_sLockUser != null)
            {
                m_jobdb.ReleaseLock(m_sLockName, m_sLockUser);
            }
        }

        LogEvent(EventLogEntryType.Information, "{0} shutdown.", ServiceName);
        Log.WriteLine("Shutdown complete.");
        PostMonitorLog(OamDB.Severity.Normal, 101);
    }

    /// <summary>
    /// Executes when config file changes
    /// </summary>
    /// <param name="sender">sender of the object</param>
    protected override void OnConfigFileChanged(object sender)
    {
        LoadMappingsTable();
    }

    /// <summary>
    /// Add new Directory to Directory list
    /// </summary>
    private void LoadMappingsTable()
    {
        if (m_mappings == null) m_mappings = new Hashtable();

        lock (m_mappings.SyncRoot)
        {
            m_mappings.Clear();
            XmlElement xmlmap = Cfg.RouteMappings;
            if (xmlmap == null) return;

            EzXml.Filter filter = new EzXml.Filter(XmlNodeType.Element);
            XmlNode node = filter.FirstChild(xmlmap);
            while (node != null)
            {
                if (node.Name == "For")
                {
                    string sDirectory = EzXml.GetStringAttribute(node, "Directory");
                    string sRoute = EzXml.GetStringAttribute(node, "Route");

                    string sCircuit = EzXml.GetStringAttribute(node, "Circuit");

                    if (!string.IsNullOrEmpty(sCircuit))
                    {
                        sRoute += ":" + sCircuit;
                    }

                    if (sDirectory != null && sRoute != null)
                        m_mappings[sDirectory.ToUpper()] = sRoute;
                    else
                        Log.WriteLine("ERR: \"Directory\" and \"Route\" attributes required on \"For\" mapping node.");
                }
                else Log.WriteLine("ERR: Unexpected mapping node \"{0}\" ignored.", node.Name);
                node = filter.NextSibling(node);
            }
        }
    }

    /// <summary>
    /// Maps the Directory
    /// </summary>
    /// <param name="sDirectory"></param>
    /// <returns>Gets the mapping Directory or Route</returns>
    private string GetMapping(string sDirectory)
    {
        if (sDirectory == null || m_mappings == null) return sDirectory;
        lock (m_mappings.SyncRoot)
        {
            string sRoute = m_mappings[sDirectory.ToUpper()] as string;
            return (sRoute == null) ? sDirectory : sRoute;
        }
    }

    /// <summary>
    /// Iniializes the file system watcher
    /// </summary>
    private void InitializeWatcher()
    {
        Log.WriteLine("Initializing watcher...");
        m_nInitWatcherCount++;
        lock (m_lock)
        {
            m_shareHelper = new UncShareHelper();
        }
        string sDirectory = Cfg.PickupDirectory;
        if (sDirectory == null)
        {
            sDirectory = m_resdb.GetResourceString(
                Globals.SYSTEM,
                ApplicationID.XConnect,
                (short)XConnectResource.ReportPickupDirectory);
        }
        if (sDirectory == null || !Directory.Exists(sDirectory))
        {
            throw new DirectoryNotFoundException(string.Format("Directory \"{0}\" does not exist.", sDirectory));
        }

        // Must be before PreloadQueue
        // Do this here to avoid another watcher being created while in PreloadQueue
        m_watcher = new FileSystemWatcher();

        if (m_nInitWatcherCount == 1)
        {
            Log.WriteLine("PreloadQueue start...");
            PreloadQueue(sDirectory);
            Log.WriteLine("PreloadQueue finish...");
            if (m_reportQueue.Count > 0) m_reportEvent.Set();
        }

        // Attempt to watch as local path (if possible)
        lock (m_lock)
        {
            sDirectory = m_shareHelper.ToLocalPath(sDirectory);
        }


        m_watcher.Path = sDirectory;
        m_watcher.InternalBufferSize = 16 * 4096;
        m_watcher.IncludeSubdirectories = true;
        m_watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;
        m_watcher.Created += new FileSystemEventHandler(OnFileCreated);
        m_watcher.Error += new ErrorEventHandler(OnFileError);
        m_watcher.EnableRaisingEvents = true;

        Log.WriteLine("Watching \"{0}\"...", sDirectory);
    }

    /// <summary>
    /// Inserts the files in the specified directory to Queue
    /// </summary>
    /// <param name="sDirectory">Directory path</param>
    void PreloadQueue(string sDirectory)
    {
        DirectoryInfo dirInfo = new DirectoryInfo(sDirectory);
        foreach (FileInfo fileInfo in dirInfo.GetFiles())
        {
            lock (m_reportQueue.SyncRoot)
            {
                m_reportQueue.Enqueue(fileInfo.FullName);
            }
        }
        foreach (DirectoryInfo subInfo in dirInfo.GetDirectories())
        {
            PreloadQueue(subInfo.FullName);
        }
    }

    /// <summary>
    /// Re-initializes the file system watcher
    /// </summary>
    private void ReinitializeWatcher()
    {
        Log.WriteLine("Re-initializing watcher...");
        if (m_watcher != null)
        {
            m_watcher.EnableRaisingEvents = false;
            m_watcher.Dispose();
            m_watcher = null;
        }
        if (m_resdb.GetType() == typeof(ResourceDBCache))
        {
            // Refresh the resource DB cache in case the Pickup directory has changed.
            ((ResourceDBCache)m_resdb).LoadSite(Globals.SYSTEM);
        }
        InitializeWatcher();
    }

    /// <summary>
    /// This method executes when an error occurs in the file 
    /// </summary>
    /// <param name="sender">sender object</param>
    /// <param name="e">ErrorEventArgs</param>
    private void OnFileError(object sender, ErrorEventArgs e)
    {
        Log.WriteLine("OnFileError...");
        try
        {
            lock (this)
            {
                ReinitializeWatcher();
            }
        }
        catch (Exception x)
        {
            Log.WriteLine(x);
            if (m_nInitWatcherCount >= MaxInitWatcherCount) AbortService();
        }
    }

    /// <summary>
    /// This method executes when a file is created
    /// </summary>
    /// <param name="sender">sender object</param>
    /// <param name="e">FileSystemEventArgs</param>
    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        try
        {
            lock (m_reportQueue.SyncRoot)
            {
                m_reportQueue.Enqueue(e.FullPath);
            }
            m_reportEvent.Set();
        }
        catch (Exception x)
        {
            Log.WriteLine(x);
        }
    }

    /// <summary>
    /// This method executes when Report event triggers
    /// </summary>
    /// <param name="sender">sender object</param>
    /// <param name="bTimedOut">true if timed out</param>
    private void OnReportEvent(object sender, bool bTimedOut)
    {
        Log.WriteLine("OnReportEvent...");
        try
        {
            Log.WriteLine("bTimedOut = {0};   m_watcher = {1}", bTimedOut.ToString(), m_watcher == null ? "NULL" : "NOT NULL");
            if (bTimedOut && m_watcher == null)
            {
                lock (this)
                {
                    if (m_watcher == null)
                    {
                        ReinitializeWatcher();
                        return;
                    }
                }
            }
            ProcessReports();
        }
        catch (Exception x)
        {
            Log.WriteLine(x);
        }
    }

    /// <summary>
    /// This method process the Reports data
    /// </summary>
    private void ProcessReports()
    {
        ArrayList retryList = null;
        try
        {
            while (true)
            {
                string sFilename = null;
                lock (m_reportQueue.SyncRoot)
                {
                    if (m_reportQueue.Count > 0)
                    {
                        sFilename = m_reportQueue.Dequeue() as string;
                    }
                }
                if (sFilename == null) return;

                if (ProcessReport(sFilename) == PickupResult.Failure)
                {
                    if (retryList == null) retryList = new ArrayList();
                    retryList.Add(sFilename);
                }
            }
        }
        catch (Exception x)
        {
            Log.WriteLine(x);
        }
        finally
        {
            if (retryList != null)
            {
                foreach (string sFilename in retryList)
                {
                    lock (m_reportQueue.SyncRoot)
                    {
                        m_reportQueue.Enqueue(sFilename);
                    }
                }
            }
        }
    }

    /// <summary>
    /// This method process the Reports data for a specific file
    /// </summary>
    /// <param name="sFilename">file name</param>
    /// <returns>PickupResult object</returns>
    private PickupResult ProcessReport(string sFilename)
    {
        try
        {
            // Ignore directory creation
            if (Directory.Exists(sFilename)) return PickupResult.Ignored;

            string sDirectory = Path.GetDirectoryName(sFilename);
            if (sDirectory == null || sDirectory.Length <= 0) return PickupResult.Ignored;

            // The following weird code insures that the case (upper/lower) of RouteName
            // matches the directory name exactly (which is preferred, but not necessary).
            DirectoryInfo dirInfo = new DirectoryInfo(sDirectory);
            string sRouteName = dirInfo.Name;
            if (dirInfo.Parent != null)
            {
                DirectoryInfo[] infos = dirInfo.Parent.GetDirectories(sRouteName);
                if (infos != null && infos.Length == 1) sRouteName = infos[0].Name;
            }
            sRouteName = GetMapping(sRouteName);

            string sCircuits = string.Empty;

            if (sRouteName.Contains(":"))
            {
                string[] attrs = sRouteName.Split(':');

                if (attrs.Length == 2)
                {
                    sRouteName = attrs[0];
                    sCircuits = attrs[1];
                }
            }

            // Make sure it's not readonly
            FileAttributes atts = File.GetAttributes(sFilename);
            if ((atts & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(sFilename, atts & ~FileAttributes.ReadOnly);

            // Make sure it has valid created/modified times since the lack of can cause
            // problems with Xceed and other libraries that expect it to be valid.
            if (File.GetCreationTime(sFilename).Year < 1980)
                File.SetCreationTime(sFilename, DateTime.Now);
            if (File.GetLastWriteTime(sFilename).Year < 1980)
                File.SetLastWriteTime(sFilename, DateTime.Now);

            // Create and start and Report Parser Service job
            int nCircuitID = -1;
            if (string.IsNullOrEmpty(sCircuits))
                nCircuitID = m_jobdb.GetCircuitID("IncomingReports");
            else
                nCircuitID = m_jobdb.GetCircuitID(sCircuits);

            if (nCircuitID < 0)
                nCircuitID = m_jobdb.GetCircuitID("IncomingReports");

            string sJobName = string.Format("Report {0}", Path.GetFileNameWithoutExtension(sFilename));
            XConJob job = XConJob.Create(m_jobdb, sJobName, null, sRouteName, nCircuitID);
            XConParameters parms = new XConParameters();
            parms.Set("Route", sRouteName);
            // This filename must be a global (UNC) path, if local convert it or else fail.
            lock (m_lock)
            {
                if (UncShareHelper.IsLocalPath(sFilename)) sFilename = m_shareHelper.ToGlobalPath(sFilename);
            }
            parms.Set("File", sFilename);

            try
            {
                Log.WriteLine("Starting Job ID#{0} for \"{1}\" file \"{2}\" CircuitID {3}...", job.JobID, sRouteName, sFilename, nCircuitID);
                job.Start(parms);
            }
            catch (Exception x)
            {
                Log.WriteLine(x);
            }
            return PickupResult.Success;
        }
        catch (Exception x)
        {
            Log.WriteLine(x);
            return PickupResult.Failure;
        }
    }

}

}
