/*
 * EvidenciasSQA - a free and open source screenshot tool
 * Copyright (C) 2004-2026 Thomas Braun, Jens Klingen, Robin Krom
 *
 * For more information see: https://evidenciassqa.com/
 * The EvidenciasSQA project is hosted on GitHub https://github.com/evidenciassqa/evidenciassqa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 1 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Dapplo.Ini;
using Dapplo.Ini.Parsing;
using EvidenciasSQA.Base.Core;
using EvidenciasSQA.Configuration;
using EvidenciasSQA.Editor.Configuration;
using EvidenciasSQA.Forms;
using EvidenciasSQA.Helpers;
using log4net;

namespace EvidenciasSQA;

/// <summary>
/// Description of Main.
/// </summary>
public class EvidenciasSQAMain
{
    private static ILog LOG;
    public static string LogFileLocation;
    static EvidenciasSQAMain()
    {
        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
    }

    private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
    {
        Assembly ayResult = null;
        string sShortAssemblyName = args.Name.Split(',')[0];
        Assembly[] ayAssemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (Assembly ayAssembly in ayAssemblies)
        {
            if (sShortAssemblyName != ayAssembly.FullName.Split(',')[0])
            {
                continue;
            }

            ayResult = ayAssembly;
            break;
        }

        return ayResult;
    }

    [STAThread]
    public static void Main(string[] args)
    {
        // Enable TLS 1.2 and 1.3 support only (TLS 1.0/1.1 deprecated per RFC 8996)
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        // Set the Thread name, is better than "1"
        Thread.CurrentThread.Name = Application.ProductName;

        // Init Log4NET
        LogFileLocation = LogHelper.InitializeLog4Net();
        // Get logger
        LOG = LogManager.GetLogger(typeof(MainForm));

        Application.ThreadException += Application_ThreadException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        TaskScheduler.UnobservedTaskException += Task_UnhandledException;

        // Parse command-line arguments early so the optional --ini-directory override
        // can be incorporated into the IniConfigRegistry search paths before Create().
        // Returns null when --help was shown or a parse error occurred (exit immediately).
        var options = EvidenciasSQACommandLine.Parse(args);
        if (options == null)
        {
            return;
        }

        // Register custom value converters (NativeRect, Color, etc.) before building the registry.
        IniValueConverters.Register();

        // Detect PortableApp (PAF) mode: the App\EvidenciasSQA directory lives next to the executable.
        var startupPath = AppContext.BaseDirectory;
        var pafAppPath = Path.Combine(startupPath, @"App\EvidenciasSQA");
        EvidenciasSQAEnvironment.IsPortable = Directory.Exists(pafAppPath);

        // Build the IniConfigRegistry:
        //   AddAppDataPath  → %APPDATA%\EvidenciasSQA
        //   AddSearchPath   → installation / startup directory
        //   --ini-directory → optional command-line override (highest priority)
        var builder = IniConfigRegistry.ForFile("evidenciassqa.ini")
            .AddAppDataPath("EvidenciasSQA")
            .AddSearchPath(startupPath);

        if (!string.IsNullOrEmpty(options.IniDirectory) && Directory.Exists(options.IniDirectory))
        {
            builder.AddSearchPath(options.IniDirectory);
        }

        builder.AddDefaultsFile("evidenciassqa-defaults.ini")
               .AddConstantsFile("evidenciassqa-fixed.ini")
               .WithWriterOptions(new IniWriterOptions
               {
                   AssignmentSeparator = "=",
                   QuoteStyle = IniValueQuoteStyle.Never,
                   EscapeSequences = false,
                   WriteComments = true
               })
               .WithParserOptions(new IniParserOptions
               {
                   CaseSensitiveKeys = false,
                   EscapeSequences = false,
                   LineContinuation = true,
                   QuotedValues = false
               })
               .RegisterSection<ICoreConfiguration>(new CoreConfigurationImpl())
               .RegisterSection<IEditorConfiguration>(new EditorConfigurationImpl())
               .RegisterSection<IWin10Configuration>(new Win10ConfigurationImpl())
               .AutoSaveInterval(TimeSpan.FromSeconds(2))
               .EmptyWhenNull()
               .LockFile()
               .EnableMetadata(applicationName: "EvidenciasSQA");

#if DEBUG
        builder.AddListener(new Helpers.IniListener());
#endif

        var iniConfig = builder.Create();

        // Log the startup
        LOG.Info("Starting: " + EnvironmentInfo.EnvironmentToString(false));

        // Consolidacion .NET 9: un solo proceso gestiona ambos modulos (tray + visor).
        // MainForm.Start valida el arranque (mutex, copydata, restart) y entrega el
        // MainForm oculto; la Application WPF unificada (App.xaml.cs) lo muestra junto
        // a la ventana del visor. Si Start devuelve null, la App se cierra en OnStartup.
        App app = new App { StartupForm = MainForm.Start(options) };
        app.Run();
    }

    internal static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
    {
        Exception exceptionToLog = e.Exception;
        string exceptionText = EnvironmentInfo.BuildReport(exceptionToLog);
        LOG.Error("Exception caught in the ThreadException handler.");
        LOG.Error(exceptionText);
        if (exceptionText != null && exceptionText.Contains("InputLanguageChangedEventArgs"))
        {
            // Ignore for BUG-1809
            return;
        }

        new BugReportForm(exceptionText).ShowDialog();
    }

    internal static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Exception exceptionToLog = e.ExceptionObject as Exception;
        string exceptionText = EnvironmentInfo.BuildReport(exceptionToLog);
        LOG.Error("Exception caught in the UnhandledException handler.");
        LOG.Error(exceptionText);
        if (exceptionText != null && exceptionText.Contains("InputLanguageChangedEventArgs"))
        {
            // Ignore for BUG-1809
            return;
        }

        new BugReportForm(exceptionText).ShowDialog();
    }

    internal static void Task_UnhandledException(object sender, UnobservedTaskExceptionEventArgs args)
    {
        try
        {
            Exception exceptionToLog = args.Exception;
            string exceptionText = EnvironmentInfo.BuildReport(exceptionToLog);
            LOG.Error("Exception caught in the UnobservedTaskException handler.");
            LOG.Error(exceptionText);
            new BugReportForm(exceptionText).ShowDialog();
        }
        finally
        {
            args.SetObserved();
        }
    }
}