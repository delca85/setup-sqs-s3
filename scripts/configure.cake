#load "get-hostname.cake"
#load "ensure-janus-queue-exists.cake"
#load "update-docker-compose-env-file.cake"

Task("configure")
    .IsDependentOn("check-all")
    .IsDependentOn("ecr-login")
    .IsDependentOn("hsd")
    .IsDependentOn("get-hostname")
    .IsDependentOn("update-docker-compose-env-file")
    .IsDependentOn("load-env")
    .IsDependentOn("remove-temporary-containers")
    .IsDependentOn("ensure-janus-queue-exists")
    .ReportError(exception =>
    {
        Error("One or more configuration prep checks failed!  Please see above output for more information.");
    });
