#tool nuget:?package=vswhere

DirectoryPath vsLatest  = VSWhereLatest();
FilePath msBuildPathX64 = (vsLatest==null)
                            ? null
                            : vsLatest.CombineWithFilePath("./MSBuild/Current/Bin/amd64/MSBuild.exe");

///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var CURRENT_PACKAGE_VERSION = "99.1.3";

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var packageVersion = Argument("packageVersion", CURRENT_PACKAGE_VERSION);
var majorVersion = $"{packageVersion.Substring(0, packageVersion.IndexOf("."))}.0.0.0";
var buildVersion = Argument("buildVersion", EnvironmentVariable("BUILD_NUMBER") ?? "");
if (!string.IsNullOrEmpty(buildVersion)) {
    buildVersion = $"-{buildVersion}";
}

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("libs")
    .Does(() =>
{
    var sln = IsRunningOnWindows() ? "./src/SignaturePad.sln" : "./src/SignaturePad.Mac.sln";

    MSBuild(sln, new MSBuildSettings {
	    ToolPath = msBuildPathX64,
        Verbosity = Verbosity.Minimal,
        Configuration = configuration,
        PlatformTarget = PlatformTarget.MSIL,
        MSBuildPlatform = MSBuildPlatform.x86,
        ArgumentCustomization = args => args.Append("/restore"),
        Properties = {
            { "AssemblyVersion", new [] { majorVersion } },
            { "Version", new [] { packageVersion } },
        },
    });

    EnsureDirectoryExists("./output/android/");
    EnsureDirectoryExists("./output/ios/");
    EnsureDirectoryExists("./output/uwp/");
    EnsureDirectoryExists("./output/uwp/Themes");
    EnsureDirectoryExists("./output/netstandard/");

    CopyFiles($"./src/SignaturePad.Android/bin/{configuration}/SignaturePad.*", "./output/android/");
    CopyFiles($"./src/SignaturePad.iOS/bin/{configuration}/SignaturePad.*", "./output/ios/");
    CopyFiles($"./src/SignaturePad.UWP/bin/{configuration}/SignaturePad.*", "./output/uwp/");
    CopyFiles($"./src/SignaturePad.UWP/obj/{configuration}/*.xml", "./output/uwp/");
    CopyFiles($"./src/SignaturePad.UWP/obj/{configuration}/Themes/*", "./output/uwp/Themes");

    CopyFiles($"./src/SignaturePad.Forms.Droid/bin/{configuration}/SignaturePad.Forms.*", "./output/android/");
    CopyFiles($"./src/SignaturePad.Forms.iOS/bin/{configuration}/SignaturePad.Forms.*", "./output/ios/");
    CopyFiles($"./src/SignaturePad.Forms.UWP/bin/{configuration}/SignaturePad.Forms.*", "./output/uwp/");
    CopyFiles($"./src/SignaturePad.Forms.UWP/obj/{configuration}/*.xml", "./output/uwp/");
    CopyFiles($"./src/SignaturePad.Forms.UWP/obj/{configuration}/Themes/*", "./output/uwp/Themes");
    CopyFiles($"./src/SignaturePad.Forms/bin/{configuration}/SignaturePad.Forms.*", "./output/netstandard/");
});

Task("nuget")
    .IsDependentOn("libs")
    .WithCriteria(IsRunningOnWindows())
    .Does(() =>
{
    var nuget = Context.Tools.Resolve("nuget.exe");
    var nuspecs = GetFiles("./nuget/*.nuspec");
    var settings = new NuGetPackSettings {
        BasePath = ".",
        OutputDirectory = "./output",
        Properties = new Dictionary<string, string> {
            { "configuration", configuration },
            { "version", packageVersion },
        },
    };

    EnsureDirectoryExists("./output");

    NuGetPack(nuspecs, settings);

    settings.Properties["version"] = $"{packageVersion}-preview{buildVersion}";
    NuGetPack(nuspecs, settings);
});

Task("samples")
    .IsDependentOn("libs")
    .Does(() =>
{
    var settings = new MSBuildSettings {
		ToolPath = msBuildPathX64,
        Verbosity = Verbosity.Minimal,
        Configuration = configuration,
        PlatformTarget = PlatformTarget.MSIL,
        MSBuildPlatform = MSBuildPlatform.x86,
        ArgumentCustomization = args => args.Append("/restore"),
    };

	MSBuild("./samples/Sample.Android/Sample.Android.sln", settings);
	//MSBuild("./samples/Sample.iOS/Sample.iOS.sln", settings);
	//MSBuild("./samples/Sample.Forms/Sample.Forms.Mac.sln", settings);

});

Task("Default")
    .IsDependentOn("libs")
    .IsDependentOn("nuget")
    .IsDependentOn("samples");

Task("CI")
    .IsDependentOn("Default");

RunTarget(target);
