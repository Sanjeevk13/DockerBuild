#addin "nuget:?package=Cake.Docker&version=0.9.9"
#tool "nuget:?package=OctopusTools&version=6.1.1"
//#tool "nuget:?package=GitVersion.CommandLine"
#addin "nuget:?package=Cake.Powershell&version=0.4.7"
#addin "nuget:?package=Cake.FileHelpers&version=3.1.0"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var buildNumber = Argument("buildNumber", "0");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////


// Define directories.
var artifacts ="./.artifacts";
var artifactsdir = Directory (artifacts);

// Define config variables
var appName = "DemoApp";
//var projectName = "DemoApp";
//var projectPath = "GoRewardsBookingService";
var registryUsername = EnvironmentVariable("REG_USER") ?? "sanjeevk13";
var registryPassword = EnvironmentVariable("REG_PASSWORD") ?? "Vicky@di1331";
var registryUrl = EnvironmentVariable("REG_URL") ?? "https://hub.docker.com/repository/docker/sanjeevk13/my-first-repo";
var buildVersion = EnvironmentVariable("BUILD_BUILDID") ??  EnvironmentVariable("BUILD_NUMBER") ?? buildNumber;
//var octoApiKey = EnvironmentVariable("OCTOPUS_API_KEY") ?? "API-JMONTCBFJP5GY8ETET85WSVOXZC";
//var octoAddress = EnvironmentVariable("OCTOPUS_URL") ?? "https://davideploy.apps.bcstechnology.com.au/";

var dockerImageUrlVersion = "";
var dockerImageUrlVersionLatest = registryUrl + "/" + appName + ":latest";
//var dockerNSQImageName = registryUrl + "/" + "nsq:1.1.0";
//var dockerNSQImageUrlVersion = "";
//var dockerNSQImageUrlVersionLatest = registryUrl + "/" + "nsq:latest";
var versionBuildString ="";
var buildTag ="";
var buildTagExt ="";

Information(logAction => logAction ("Target : {0}", target));    

FilePath        kubePath      = Context.Tools.Resolve("kubectl.exe");

Action<FilePath, ProcessArgumentBuilder> Kubectl => (path, args) => {
    var result = StartProcess(
        path,
        new ProcessSettings {
            Arguments = args
        });

    if(0 != result)
    {
        throw new Exception($"Failed to execute tool {path.GetFilename()} ({result})");
    }
};

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(_ =>
{
    Information("");
    Information(" ____   _____  _____ ");
    Information("|  _ \\ / ____|/ ____|");
    Information("| |_) | |    | (___  ");
    Information("|  _ <| |     \\___ \\ ");
    Information("| |_) | |____ ____) |");
    Information("|____/ \\_____|_____/ ");
    Information("");
});

Teardown(_ =>
{
    Information("Finished running tasks.");
});


//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectory(artifactsdir);
    CreateDirectory(artifactsdir);
});

Task("GetVersionInfo")
    .WithCriteria(target != "Jenkins" && target != "JenkinsBuild")
    .Does(() =>
    {
        var versioninfo = GitVersion(new GitVersionSettings {
            UpdateAssemblyInfo = false,
            NoFetch = true
        });

        var versionSuffixPadded = "";
        if(versioninfo.BuildMetaDataPadded != "") {
            Information(logAction => logAction ("MajorMinorPatch : {0}", versioninfo.MajorMinorPatch));    
            versionSuffixPadded = versioninfo.BuildMetaDataPadded;
        }
        
        versionBuildString = versioninfo.MajorMinorPatch + versionSuffixPadded;
        Information(logAction => logAction ("Build String : {0}", versionBuildString));    
        buildTag = versionBuildString + buildVersion;
        dockerImageUrlVersion = registryUrl + "/" + appName + ":" + buildTag ;
        dockerImageUrlVersionLatest = registryUrl + "/" + appName + ":latest";
    //  dockerNSQImageUrlVersion = registryUrl + "/nsq:" + buildTag ;
        Information(logAction => logAction ("Docker name : {0}", dockerImageUrlVersion));    
    });

Task("GetJenkinsInfo")
    .WithCriteria(target == "Jenkins" || target == "JenkinsBuild")
    .Does(() =>
    {
        buildTag = buildVersion;
        buildTagExt = ".0.0";
        dockerImageUrlVersion = registryUrl + "/" + appName + ":" + buildTag ;
        dockerImageUrlVersionLatest = registryUrl + "/" + appName + ":latest";
    //  dockerNSQImageUrlVersion = registryUrl + "/nsq:" + buildTag ;
        Information(logAction => logAction ("Docker name : {0}", dockerImageUrlVersion));
    });

// Docker Login
Task("DockerLogin")
    .Does(()=>
    {
        DockerLogin(registryUsername, registryPassword, registryUrl);
    });

// Docker builds
Task("CreateDockerImage")
    .IsDependentOn("DockerLogin")
    .IsDependentOn("GetVersionInfo")
    .IsDependentOn("GetJenkinsInfo")
    .Does(()=>
    {
        var dockerBuildSettings = new DockerImageBuildSettings()
        {
            File = "./Dockerfile",
            Tag = new string[]{ dockerImageUrlVersion }
        };

        var path =  Directory(".");
        DockerBuild(dockerBuildSettings, path);
        Information(logAction => logAction ("Docker created : {0}", dockerImageUrlVersion));    
    });


// Docker builds
Task("PushDockerImages")
    .IsDependentOn("DockerLogin")
    .IsDependentOn("CreateDockerImage")
    .Does(()=>
    {   
        Information(logAction => logAction ("Docker to push : {0}", dockerImageUrlVersion));    
        DockerPush(dockerImageUrlVersion);
        DockerTag(dockerImageUrlVersion, dockerImageUrlVersionLatest);
        DockerPush(dockerImageUrlVersionLatest);
        Information(logAction => logAction ("Docker pushed : {0}", dockerImageUrlVersion));    
    });




Task("PrepFiles")
  .IsDependentOn("GetVersionInfo")
  .IsDependentOn("GetJenkinsInfo")
    .Does(() =>
    {
        CleanDirectory("./drop");
        CreateDirectory("./drop");
        CreateDirectory("./drop/config");
        CreateDirectory("./drop/secrets");
        CreateDirectory("./drop/nuget");
        CopyFile("./" + projectPath + "/appsettings.json","./drop/config/appsettings.json");
    });

Task("PackFiles")
  .IsDependentOn("PrepFiles")
  .Does(() => {

    var nuGetPackSettings   = new NuGetPackSettings {
        Id                      = appName,
        Version                 = buildTag + buildTagExt,
        Title                   = projectName,
        Authors                 = new[] {"BCS"},
        Description             = "Release package",
        Summary                 = projectName,
        ProjectUrl              = new Uri("https://teams.microsoft.com/l/channel/19%3a1621886ad7cc466d935b66d807eabec5%40thread.skype/General?groupId=a882811e-e016-4a02-a06d-42524e771977&tenantId=f5195494-192f-494d-a38f-ff21b1f9bbae"),
        Files                   = new [] { new NuSpecContent {Source = "**", Target = ""}, },
        BasePath                = "./drop",
        IncludeReferencedProjects = true,
        Properties = new Dictionary<string, string> { {"Configuration", "Release"} },
        OutputDirectory         = "./drop/nuget/"
    };
    NuGetPack(nuGetPackSettings);   
  });
   
Task("Jenkins")
    .IsDependentOn("DeployPackage")
    .Does(() => {
        Information("Done Building...");
    });

Task("JenkinsBuild")
    .IsDependentOn("CreateDockerImage")
    .Does(() => {
        Information("Done build and integration testing...");
    });




//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("DeployToTest");
Task("Build")
    .IsDependentOn("CreateDockerImage");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);



