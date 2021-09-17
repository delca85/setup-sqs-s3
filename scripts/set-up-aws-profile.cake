using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;

var AWS_PROFILE_LOCK_NAME = "set-up-aws-profile.lock";
var AWS_PROFILE_LOCK_TIME = 1440;

const string MARVEL_AWS_CREDENTIALS_PROFILE_NAME = "marvel";

// Class to be used elsewhere for providing Marvel AWS credentials. Storing here to help keep access consistent, even though logic is technically duplicated within below task.
public static class AwsCredentialsProvider
{
    // Cache credentials to reduce file access
    private static Lazy<AWSCredentials> Credentials = new Lazy<AWSCredentials>(()=>
    {
        var profileStore = new SharedCredentialsFile();
        if (profileStore.TryGetProfile(MARVEL_AWS_CREDENTIALS_PROFILE_NAME, out CredentialProfile marvelProfile))
        {
            return marvelProfile.GetAWSCredentials(profileStore);
        }
        throw new Exception($"Unable to retrieve AWS credentials for [{MARVEL_AWS_CREDENTIALS_PROFILE_NAME}] profile. You may need to run `check` dev script command.");
    });

    public static AWSCredentials GetMarvelCredentials() => Credentials.Value;
}

Task("set-up-aws-profile")
    .Description("Ensures AWS credentials file is set up with a suitable Marvel profile.")
    .WithCriteria(() => LastRunOlderThan(AWS_PROFILE_LOCK_NAME, AWS_PROFILE_LOCK_TIME), LockExpiryMessage(AWS_PROFILE_LOCK_NAME, AWS_PROFILE_LOCK_TIME))
    .Does(async () =>
{
    var defaultRegion = RegionEndpoint.USEast1;

    // null => default location
    string profilesLocation = null;
    var skipCredentialsValidation = false;
    var skipHudlAccountValidation = false;
    var echoInput = false;
    var isUserInteractive = Environment.UserInteractive;
    Action pauseIoForTests = () => {};

    if (HasArgument("test"))
    {
        // Set up test values
        profilesLocation = Argument<string>("test-credentials-path");
        skipCredentialsValidation = Argument<bool>("test-skip-validation", true);
        skipHudlAccountValidation = Argument<bool>("test-skip-hudl-validation", false);
        if (HasArgument("test-is-user-interactive"))
        {
            isUserInteractive = Argument<bool>("test-is-user-interactive");
        }
        echoInput = true;
        pauseIoForTests = () => { System.Threading.Thread.Sleep(TimeSpan.FromSeconds(.1)); };
    }

    await SetUpProfile();
    Information("Marvel profile is set-up.");
    UpdateLastRun(AWS_PROFILE_LOCK_NAME);

    async Task SetUpProfile()
    {
        var profileStore = new SharedCredentialsFile(profilesLocation);
        if (profileStore.TryGetProfile(MARVEL_AWS_CREDENTIALS_PROFILE_NAME, out CredentialProfile marvelProfile))
        {
            await VerifyProfile(marvelProfile, profileStore);
            EnsureProfileHasRegion(marvelProfile, profileStore);
            return;
        }

        // No Marvel profile set
        var profiles = profileStore.ListProfiles();
        if (!profiles.Any()) {
            var credentialsFilePath = new FilePath(new SharedCredentialsFile(profilesLocation).FilePath);

            if (!FileExists(credentialsFilePath))
            {
                Error($"Credentials file {credentialsFilePath} does not exist. To fix this:");
                Error("1. Follow the instructions here: https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-files.html");
                Error("2. Re-run this command.");
                Error("");
                throw new FileNotFoundException("AWS credentials file missing.");
            }

            // Credentials file exists but has no profiles.
            Error($"Your AWS credentials file ({credentialsFilePath}) does not contain any profiles.");
            Error("Add credentials by following the instructions here: https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-files.html");
            throw new Exception("AWS credentials file does not contain any profiles.");
        }

        Warning($"Your AWS credentials file does not contain a [{MARVEL_AWS_CREDENTIALS_PROFILE_NAME}] profile.");
        if (!isUserInteractive) {
            Warning($"To set up your profile, you can run the prereq checks in an interactive terminal.");
            throw new Exception($"AWS credentials are missing a [{MARVEL_AWS_CREDENTIALS_PROFILE_NAME}] profile.");
        }

        while (true) {
            CredentialProfile chosenProfile = null;
            while (chosenProfile == null)
            {
                chosenProfile = ChooseProfile(profiles);
            }
            marvelProfile = new CredentialProfile(MARVEL_AWS_CREDENTIALS_PROFILE_NAME, chosenProfile.Options);
            marvelProfile.Region = defaultRegion;
            try
            {
                await VerifyProfile(marvelProfile, profileStore);
            }
            catch (Exception e)
            {
                Error($"Failed to verify chosen profile [{chosenProfile.Name}]");
                Error(e);
                continue;
            }

            profileStore.RegisterProfile(marvelProfile);
            break;
        }
    }

    CredentialProfile ChooseProfile(List<CredentialProfile> profiles)
    {
        Warning($"These profiles were found:");
        foreach (var profile in profiles.Select((p, i) => $"{i + 1}) {p.Name}"))
        {
            Information(profile);
        }

        Warning("Enter the name or number of the profile you would like to use for Marvel:");
        var response = Console.ReadLine();
        if (echoInput)
        {
            Console.WriteLine($">{response}");
            pauseIoForTests();
        }
        if (int.TryParse(response, out int number)) {
            try
            {
                return profiles[number - 1];
            }
            catch (ArgumentOutOfRangeException)
            {
                Error("Invalid number.");
                pauseIoForTests();
                return null;
            }
        }

        var chosenProfile = profiles.FirstOrDefault(p => p.Name == response);
        if (chosenProfile == null) {
            Error("Invalid profile name.");
            pauseIoForTests();
            return null;
        }
        return chosenProfile;
    }

    void EnsureProfileHasRegion(CredentialProfile profile, SharedCredentialsFile store)
    {
        if (profile.Region != null)
        {
            return;
        }
        profile.Region = defaultRegion;
        store.RegisterProfile(profile);
    }

    async Task VerifyProfile(CredentialProfile profile, SharedCredentialsFile store)
    {
        if (skipCredentialsValidation)
        {
            return;
        }
        var credentials = profile.GetAWSCredentials(store);
        var client = new AmazonSecurityTokenServiceClient(credentials, defaultRegion);
        var iamClient = new AmazonIdentityManagementServiceClient(credentials, defaultRegion);

        await VerifyHudlCredentials(client);
        await VerifyUserInGroup(iamClient);
    }

    async Task VerifyHudlCredentials(IAmazonSecurityTokenService client)
    {
        if (skipHudlAccountValidation)
        {
            return;
        }

        var response = await client.GetCallerIdentityAsync(new GetCallerIdentityRequest());

        if (response.Account != "761584570493")
        {
            Error($"The credentials for the '{MARVEL_AWS_CREDENTIALS_PROFILE_NAME}' profile do not belong to the 'hudl' AWS account.");
            throw new Exception("Invalid AWS credentials.");
        }
    }

    async Task VerifyUserInGroup(IAmazonIdentityManagementService client)
    {
        var marvelDeveloperGroup = "Developer";
        var groupUrl = $"https://console.aws.amazon.com/iam/home?region=us-east-1#/groups/{marvelDeveloperGroup}";
        var userResponse = await client.GetUserAsync();
        try
        {
            Information($"Checking user account {userResponse.User.UserName} for group membership of {marvelDeveloperGroup}.");
            var responseGroups = await client.ListGroupsForUserAsync(new ListGroupsForUserRequest
            {
                MaxItems = 100,
                UserName = userResponse.User.UserName
            });
            var allGroups = responseGroups.Groups;

            while (responseGroups.IsTruncated)
            {
                responseGroups = await client.ListGroupsForUserAsync(new ListGroupsForUserRequest
                {
                    UserName = userResponse.User.UserName,
                    Marker = responseGroups.Marker
                });
                allGroups.AddRange(responseGroups.Groups);
            }

            if (allGroups.Where(g => g.GroupName == marvelDeveloperGroup).Count() != 1)
            {
                throw new Exception($"Your user is not in the {marvelDeveloperGroup} group.");
            }
        }
        catch (Exception e)
        {
            Error($"Ensure you are a part of the {marvelDeveloperGroup} group (or let #bet-marvel or #sre-support know you need to be added to {groupUrl} ).");
            throw e;
        }
    }
});
