var project = "src/FuncCircle/FuncCircle.fsproj";

Task("Publish").Does(() => {
    DotNetCorePublish(project, new DotNetCorePublishSettings {
        OutputDirectory = "publish"
    });
});

Task("Start")
    .IsDependentOn("Publish")
    .Does(() => {
    StartProcess("func", new ProcessSettings {
        Arguments = "start",
        WorkingDirectory = "publish"
    });
});

var target = Argument("target", "default");
RunTarget(target);