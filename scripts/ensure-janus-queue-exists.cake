#load "utility/sqs-local-use-queue-manager.cake"

var JANUS_QUEUE_LOCK_NAME = "ensure-janus-queue-exists.lock";
var JANUS_QUEUE_LOCK_TIME = (int)TimeSpan.FromDays(14).TotalMinutes;

Task("ensure-janus-queue-exists")
    // Checking hostname tracking first so we know the tracking file is created. Otherwise this could short circuit and take two runs before this is skipped.
    .WithCriteria(() => FileNewerThan(Hostname.GetHostnameTrackingFilePath(), JANUS_QUEUE_LOCK_NAME, JANUS_QUEUE_LOCK_TIME, local: true) || 
                        FileNewerThan(RepoMetadata.Current.ServiceDefinitionFilePath, JANUS_QUEUE_LOCK_NAME, JANUS_QUEUE_LOCK_TIME, local: true),
                        LockExpiryMessage(JANUS_QUEUE_LOCK_NAME, JANUS_QUEUE_LOCK_TIME, local: true))
    .Does(async ctx =>
    {
        var serviceDefinition = await ServiceConfigurationProvider.LoadServiceDefinitionFile();
        if (!serviceDefinition.Infrastructure.Web.Preferences.JanusEnabled) 
        {
            Information("Janus not enabled. Skipping queue creation.");
            UpdateLock();
            return;
        }
        
        Information("Ensuring local-use Janus queue exists...");

        var runEnvironment = "thor";

        var repoMetadata = RepoMetadata.Current;
        var serviceName = repoMetadata.Service.AsLowerCase;
        var hostnameBranch = Hostname.LocalBranchName;

        var queueManager = new SqsLocalUseQueueManager();

        var queueExists = await queueManager.DoesQueueExist(runEnvironment, serviceName, hostnameBranch);
        if (queueExists)
        {
            Information("Local-use Janus queue already exists.");
            UpdateLock();
            return;
        }

        var createQueueResult = await queueManager.CreateLocalUseQueue(runEnvironment, serviceName, hostnameBranch);
        if (createQueueResult)
        {
            Information("Created local-use Janus queue.");
            UpdateLock();
        }
        else
        {
            Error("Unable to create local-use Janus queue.");
        }

        void UpdateLock()
        {
            UpdateLastRun(JANUS_QUEUE_LOCK_NAME, local: true);
        }
    });
