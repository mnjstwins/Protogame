#if PLATFORM_WINDOWS || PLATFORM_MACOS || PLATFORM_LINUX || PLATFORM_WEB || PLATFORM_IOS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Protogame;
using Protoinject;

#if PLATFORM_MACOS
#if PLATFORM_MACOS_LEGACY
using MonoMac.AppKit;
using MonoMac.Foundation;
#else
using AppKit;
using Foundation;
#endif
#endif

public static class FastTrace
{
    public static bool EmitStartupTrace = false;

    [Conditional("DEBUG")]
    public static void WriteLine(string message)
    {
        if (EmitStartupTrace)
        {
            Debug.WriteLine(message);
        }
    }

    [Conditional("DEBUG")]
    public static void WriteLineIf(bool condition, string message)
    {
        if (EmitStartupTrace)
        {
            Debug.WriteLineIf(condition, message);
        }
    }
}

#if PLATFORM_MACOS

public static class Program
{
	public static void Main(string[] args)
	{
        if (args.Contains("--debug-startup"))
        {
            FastTrace.EmitStartupTrace = true;
        }

		NSApplication.Init();

		FastTrace.WriteLine("Startup: Finished NSApplication.Init in static Main");

		using (var p = new NSAutoreleasePool())
		{
			NSApplication.SharedApplication.Delegate = new AppDelegate();

			// TODO: Offer a way of setting the application icon.
			//NSImage appIcon = NSImage.ImageNamed("monogameicon.png");
			//NSApplication.SharedApplication.ApplicationIconImage = appIcon;

			NSApplication.Main(args);
		}
	}
}

public class AppDelegate : NSApplicationDelegate
{
    private static IKernel _kernel;
    private static Game _game;
    private static ICoreServer _server;

	public override bool ApplicationShouldTerminateAfterLastWindowClosed(NSApplication sender)
	{
		return true;
	}

#if PLATFORM_MACOS_LEGACY
	public override void FinishedLaunching(NSObject notification)
#else
	public override void DidFinishLaunching(NSNotification notification)
#endif
	{
		var args = new string[0];

		FastTrace.WriteLine("Startup: Reached AppDelegate.DidFinishLaunching / AppDelegate.FinishedLaunching");

		ErrorProtection.RunEarly(() => ProtectedStartup(args));
		ErrorProtection.RunMain(_kernel.TryGet<IErrorReport>(), ProtectedRun);
	}
#elif PLATFORM_IOS
using Foundation;
using UIKit;

[Register("AppDelegate")]
public class Program : UIApplicationDelegate
{
    private static IKernel _kernel;
    private static Game _game;
    private static ICoreServer _server;

    public static void Main(string[] args)
    {
        if (args.Contains("--debug-startup"))
        {
            FastTrace.EmitStartupTrace = true;
        }

        UIApplication.Main(args, null, "AppDelegate");
    }

    public override void FinishedLaunching(UIApplication app)
    {
        var args = new string[0];

		ErrorProtection.RunEarly(() => ProtectedStartup(args));
		ErrorProtection.RunMain(_kernel.TryGet<IErrorReport>(), ProtectedRun);
    }
#else
public static class Program
{
    private static IKernel _kernel;
    private static Game _game;
    private static ICoreServer _server;

    public static void Main(string[] args)
    {
        if (args.Contains("--debug-startup"))
        {
            FastTrace.EmitStartupTrace = true;
        }

        ErrorProtection.RunEarly(() => ProtectedStartup(args));
        ErrorProtection.RunMain(_kernel.TryGet<IErrorReport>(), ProtectedRun);
    }
#endif

    private static void ProtectedStartup(string[] args)
    {
        FastTrace.WriteLine("Protected Startup: Execution of protected startup has begun");

        var kernel = new StandardKernel();
        kernel.Bind<IRawLaunchArguments>().ToMethod(x => new DefaultRawLaunchArguments(args)).InSingletonScope();
        
		Func<System.Reflection.Assembly, Type[]> TryGetTypes = assembly =>
		{
			try
			{
				return assembly.GetTypes();
			}
			catch
			{
				return new Type[0];
			}
		};

        FastTrace.WriteLine("Protected Startup: Scanning for implementations of IGameConfiguration");

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var typeSource = new List<Type>();
        foreach (var assembly in assemblies)
        {
            typeSource.AddRange(assembly.GetCustomAttributes<ConfigurationAttribute>().Select(x => x.GameConfigurationOrServerClass));
        }

        if (typeSource.Count == 0)
        {
            // Scan all types to find implementors of IGameConfiguration
            typeSource.AddRange(from assembly in AppDomain.CurrentDomain.GetAssemblies()
                from type in TryGetTypes(assembly)
                select type);
        }

        // Search the application domain for implementations of
        // the IGameConfiguration.
        var gameConfigurations = new List<IGameConfiguration>();
        foreach (var type in typeSource)
        {
            if (typeof(IGameConfiguration).IsAssignableFrom(type) &&
                !type.IsInterface && !type.IsAbstract)
            {
                gameConfigurations.Add(Activator.CreateInstance(type) as IGameConfiguration);
            }
        }

        FastTrace.WriteLine("Protected Startup: Found " + gameConfigurations.Count + " implementations of IGameConfiguration");

#if PLATFORM_WINDOWS || PLATFORM_MACOS || PLATFORM_LINUX

        FastTrace.WriteLine("Protected Startup: Scanning for implementations of IServerConfiguration");

        // Search the application domain for implementations of
        // the IServerConfiguration.
        var serverConfigurations = new List<IServerConfiguration>();
        foreach (var type in typeSource)
        {
            if (typeof(IServerConfiguration).IsAssignableFrom(type) &&
                !type.IsInterface && !type.IsAbstract)
            {
                serverConfigurations.Add(Activator.CreateInstance(type) as IServerConfiguration);
            }
        }

        FastTrace.WriteLine("Protected Startup: Found " + serverConfigurations.Count + " implementations of IServerConfiguration");

        if (gameConfigurations.Count == 0 && serverConfigurations.Count == 0)
        {
            throw new InvalidOperationException(
                "You must have at least one implementation of " +
                "IGameConfiguration or IServerConfiguration in your game.");
        }
#else
        if (gameConfigurations.Count == 0)
        {
            throw new InvalidOperationException(
                "You must have at least one implementation of " +
                "IGameConfiguration in your game.");
        }
#endif

        Game game = null;
#if PLATFORM_WINDOWS || PLATFORM_MACOS || PLATFORM_LINUX
        ICoreServer server = null;
#endif

        FastTrace.WriteLine("Protected Startup: Starting iteration through game configuration implementations");

        foreach (var gameConfiguration in gameConfigurations)
        {
            FastTrace.WriteLine("Protected Startup: Configuring kernel with " + gameConfiguration.GetType().FullName);
            gameConfiguration.ConfigureKernel(kernel);

            // It is expected that the AssetManagerProvider architecture will
            // be refactored in future to just provide IAssetManager directly,
            // and this method call will be dropped.
            FastTrace.WriteLine("Protected Startup: Initializing asset manager provider with " + gameConfiguration.GetType().FullName);
            gameConfiguration.InitializeAssetManagerProvider(new AssetManagerProviderInitializer(kernel, args));

            // We only construct one game.  In the event there are
            // multiple game configurations (such as a third-party library
            // providing additional game tools, it's expected that libraries
            // will return null for ConstructGame).
            if (game == null)
			{
                FastTrace.WriteLine("Protected Startup: Attempted to construct game with " + gameConfiguration.GetType().FullName);
                game = gameConfiguration.ConstructGame(kernel);
                FastTrace.WriteLineIf(game != null, "Protected Startup: Constructed game with " + gameConfiguration.GetType().FullName);
            }
        }

        FastTrace.WriteLine("Protected Startup: Finished iteration through game configuration implementations");

#if PLATFORM_WINDOWS || PLATFORM_MACOS || PLATFORM_LINUX

        FastTrace.WriteLine("Protected Startup: Starting iteration through server configuration implementations");

        foreach (var serverConfiguration in serverConfigurations)
        {
            FastTrace.WriteLine("Protected Startup: Configuring kernel with " + serverConfiguration.GetType().FullName);
			serverConfiguration.ConfigureKernel(kernel);

            // It is expected that the AssetManagerProvider architecture will
            // be refactored in future to just provide IAssetManager directly,
            // and this method call will be dropped.
            FastTrace.WriteLine("Protected Startup: Initializing asset manager provider with " + serverConfiguration.GetType().FullName);
			serverConfiguration.InitializeAssetManagerProvider(new AssetManagerProviderInitializer(kernel, args));

            // We only construct one server.  In the event there are
            // multiple server configurations (such as a third-party library
            // providing additional game tools, it's expected that libraries
            // will return null for ConstructServer).
            if (server == null)
            {
                FastTrace.WriteLine("Protected Startup: Attempted to construct server with " + serverConfiguration.GetType().FullName);
				server = serverConfiguration.ConstructServer(kernel);
				Debug.WriteLineIf(server != null, "Protected Startup: Constructed server with " + serverConfiguration.GetType().FullName);
            }
        }

        FastTrace.WriteLine("Protected Startup: Finished iteration through server configuration implementations");

        _kernel = kernel;
        _game = game;
        _server = server;
    }

    private static void ProtectedRun()
    {
        FastTrace.WriteLine("Protected Run: Execution of protected run has begun");

		if (_game == null && _server == null)
        {
            throw new InvalidOperationException(
                "No implementation of IGameConfiguration provided " +
                "a game instance from ConstructGame, and " +
                "no implementation of IServerConfiguration provided " +
                "a server instance from ConstructServer.");
        }

        if (_game != null && _server != null)
        {
            throw new InvalidOperationException(
                "An implementation of IGameConfiguration provided " +
                "a game instance, and an implementation of " +
                "IServerConfiguration provided a server instance.  " +
                "It's not possible to determine whether the game or " +
                "server should be started with this configuration.  " +
                "Move one of the implementations so that it's not in " +
                "the default loaded application domain.");
        }

#else

        if (_game == null)
        {
            throw new InvalidOperationException(
                "No implementation of IGameConfiguration provided " +
                "a game instance from ConstructGame.");
        }

#endif

#if PLATFORM_WINDOWS || PLATFORM_MACOS || PLATFORM_LINUX
        if (_game != null)
        {
#endif
            FastTrace.WriteLine("Protected Run: Starting game");

#if PLATFORM_MACOS || PLATFORM_IOS
			_game.Run();
#else
        	using (var runningGame = _game)
        	{
            	runningGame.Run();
        	}
#endif

#if PLATFORM_WINDOWS || PLATFORM_MACOS || PLATFORM_LINUX
        }
        else if (_server != null)
        {
            FastTrace.WriteLine("Protected Run: Starting server");

			_server.Run();
        }
#endif
    }
}

#endif
