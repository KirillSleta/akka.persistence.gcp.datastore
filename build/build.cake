#tool "nuget:?package=GitVersion.CommandLine"
#addin "Cake.Docker"
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Debug");
var srcDir = "./artifacts/";
var solutionPath = "../akka.persistence.gcp.datastore.sln";
var unitTestsPath = "../tests/akka.persistence.gcp.datastore.tests.csproj";
var nugetVersion = "0.0.9";
var ReleaseConfig = "Release";
var OutPath =  "../src/bin/"+ReleaseConfig+"/";

Task("Default")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .IsDependentOn("Build")
    .IsDependentOn("Test");

Task("Clean")
    .Does(() => {
        CleanDirectories("../src/**/bin");
        CleanDirectories("../src/**/obj");
        CleanDirectories("../tests/**/bin");
        CleanDirectories("../tests/**/obj");
    });


Task("Restore")
    .Does(() => {
        DotNetCoreRestore("../");
    });

Task("Build")
    .Does(() => {
        DotNetCoreBuild(solutionPath);
    });

Task("BuildRelease")
    .Does(() => {
        var settings = new DotNetCoreBuildSettings
        {
            Configuration = ReleaseConfig,
        };
        DotNetCoreBuild(solutionPath, settings);
    });

Task("CleanBuild")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .IsDependentOn("Build")
    .Does(() => {

    });

Task("Test")
    .IsDependentOn("Build")
    .Does(()=>{
        DotNetCoreTest(unitTestsPath);
    });


	
Task("BuildNugetPackage")
    .Does(() =>
{
    var nuGetPackSettings = new NuGetPackSettings
	{
        Id = "akka.persistence.gcp.datastore",
        Version                 = nugetVersion,
        Title                   = "akka.persistence.gcp.datastore",
        Authors                 = new[] {"Kirill Sleta"},
        Owners                  = new[] {""},
        Description             = "akka.persistence.gcp.datastore",
        Summary                 = "Google Cloud Platform Datastore akka.net prsistence provider",
        ProjectUrl              = new Uri("https://github.com/KirillSleta/akka.persistence.gcp.datastore"),
        Copyright               = "Kirill Sleta 2017",
        NoPackageAnalysis       = true,
        Files                   = new [] {
                                            new NuSpecContent {Source = "akka.persistence.gcp.datastore.dll", Target = "bin"},
                                        },
        BasePath                = "../src/bin/Release/netstandard1.6",
		OutputDirectory = srcDir,
		IncludeReferencedProjects = true,
		Properties = new Dictionary<string, string>
		{
			{ "Configuration", ReleaseConfig }
		}
	};
    var buildSettings = new DotNetCoreBuildSettings 
    {
        Configuration = ReleaseConfig
    };
    DotNetCoreBuild(solutionPath, buildSettings);
    NuGetPack(nuGetPackSettings);
});

Task("PublishNugetPackage")
    //.IsDependentOn("BuildNugetPackage")
    .IsDependentOn("BuildRelease")
    .Does(()=>
    {
        //var package = srcDir +"akka.persistence.gcp.datastore."+ nugetVersion +".nupkg";
        var package = OutPath +"akka.persistence.gcp.datastore."+ nugetVersion +".nupkg";

        // Push the package.
        NuGetPush(package, new NuGetPushSettings {
            Source = "https://www.nuget.org/api/v2/package",
            ApiKey = "{your nuget key}}"
        });
    });


RunTarget(target);
