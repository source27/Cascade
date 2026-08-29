using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cascade.Service;
using Cascade.Core;
using HybridCLR;

namespace Cascade.Launcher
{
    internal sealed class CodeLoader
    {
        private readonly ILogService _log;
        private readonly BootstrapAssemblyLoadMode _assemblyLoadMode;
        private readonly string _entryTypeName;
        private readonly string _hotUpdateAssemblyName;

        public CodeLoader(
            ILogService log,
            BootstrapAssemblyLoadMode assemblyLoadMode,
            string entryTypeName,
            string hotUpdateAssemblyName)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _assemblyLoadMode = assemblyLoadMode;
            _entryTypeName = string.IsNullOrWhiteSpace(entryTypeName) ? BootstrapConfiguration.DefaultGameLogicEntryType : entryTypeName;
            _hotUpdateAssemblyName = string.IsNullOrWhiteSpace(hotUpdateAssemblyName) ? BootstrapConfiguration.DefaultHotUpdateAssemblyName : hotUpdateAssemblyName;
        }

        public void LoadMetadata(byte[] bytes, string location)
        {
            if (bytes == null || bytes.Length == 0)
                throw new InvalidOperationException($"AOT metadata is empty: {location}");

#if !UNITY_EDITOR
            var result = RuntimeApi.LoadMetadataForAOTAssembly(bytes, HomologousImageMode.SuperSet);
            if (result != LoadImageErrorCode.OK)
                throw new InvalidOperationException($"HybridCLR returned {result} for {location}.");
#endif
            _log.Info("CodeLoader", $"AOT metadata ready: {location}, {bytes.Length} bytes, {ComputeHash(bytes)}.");
        }

        public Assembly LoadGameLogicAssembly(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                throw new InvalidOperationException("GameLogic DLL is empty.");

            _log.Info("CodeLoader", $"GameLogic DLL ready: {bytes.Length} bytes, {ComputeHash(bytes)}.");

#if UNITY_EDITOR
            if (_assemblyLoadMode == BootstrapAssemblyLoadMode.EditorLoaded)
            {
                var loaded = FindLoadedGameLogicAssembly();
                if (loaded == null)
                    throw new InvalidOperationException("GameLogic.HotUpdate assembly is not loaded.");
                return loaded;
            }
#endif
            return Assembly.Load(bytes);
        }

        public async UniTask<string> InvokeEntryAsync(
            Assembly assembly,
            IGameHost host,
            CancellationToken cancellationToken = default)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));
            if (host == null)
                throw new ArgumentNullException(nameof(host));

            var entryType = assembly.GetType(_entryTypeName, true);
            var entryMethod = entryType.GetMethod("Start", BindingFlags.Public | BindingFlags.Static);
            if (entryMethod == null)
                throw new MissingMethodException("Expected public static Start on GameLogicEntry.");

            var parameters = entryMethod.GetParameters();
            object result;
            if (parameters.Length == 2 &&
                typeof(IGameHost).IsAssignableFrom(parameters[0].ParameterType) &&
                parameters[1].ParameterType == typeof(CancellationToken))
            {
                result = entryMethod.Invoke(null, new object[] { host, cancellationToken });
            }
            else
            {
                throw new MissingMethodException("Expected Start(IGameHost, CancellationToken).");
            }

            if (!(result is UniTask<string> startTask))
                throw new InvalidOperationException($"{_entryTypeName}.Start must return UniTask<string>.");
            return await startTask;
        }

        public void InvokeStop(Assembly assembly)
        {
            if (assembly == null)
            {
#if UNITY_EDITOR
                assembly = FindLoadedGameLogicAssembly();
#endif
                if (assembly == null)
                    return;
            }

            var entryType = assembly.GetType(_entryTypeName, false);
            var stopMethod = entryType?.GetMethod("Stop", BindingFlags.Public | BindingFlags.Static);
            if (stopMethod == null || stopMethod.GetParameters().Length != 0)
                return;

            stopMethod.Invoke(null, null);
            _log.Info("CodeLoader", "GameLogicEntry.Stop invoked.");
        }

        private Assembly FindLoadedGameLogicAssembly()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, _hotUpdateAssemblyName, StringComparison.Ordinal));
        }

        private static string ComputeHash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
