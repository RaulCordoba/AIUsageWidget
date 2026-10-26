using System.Xml.Linq;
using AIUsageWidget.Core.Interfaces;

namespace AIUsageWidget.Infrastructure.SystemServices;

/// <summary>Launches once at login; no resident daemon and no elevated privileges.</summary>
public sealed class MacStartupService : IStartupService
{
    private readonly string _file;
    private readonly string _executable;
    public MacStartupService() : this(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.ProcessPath ?? throw new InvalidOperationException("Ruta de la aplicación no disponible.")) { }

    public MacStartupService(string homeDirectory, string executablePath)
    {
        _file = Path.Combine(homeDirectory, "Library", "LaunchAgents", "com.aiusagewidget.desktop.plist");
        _executable = Path.GetFullPath(executablePath);
    }

    public bool IsEnabled() => File.Exists(_file);

    public void Enable()
    {
        // Start through LaunchServices for the correct bundle identity, menu bar and permissions.
        var marker = ".app" + Path.DirectorySeparatorChar;
        var index = _executable.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        string[] args = index >= 0
            ? ["/usr/bin/open", "-a", _executable[..(index + 4)], "--args", "--autostart"]
            : [_executable, "--autostart"];
        var document = new XDocument(new XDeclaration("1.0", "UTF-8", null),
            new XElement("plist", new XAttribute("version", "1.0"),
                new XElement("dict",
                    new XElement("key", "Label"), new XElement("string", "com.aiusagewidget.desktop"),
                    new XElement("key", "ProgramArguments"), new XElement("array", args.Select(x => new XElement("string", x))),
                    new XElement("key", "RunAtLoad"), new XElement("true"))));
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        document.Save(_file);
    }

    public void Disable() => File.Delete(_file);
}
