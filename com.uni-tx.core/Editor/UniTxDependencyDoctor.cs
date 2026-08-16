using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UniTx.Core.EditorTools
{
    /// <summary>
    /// Reports UniTx packages whose dependencies are missing, with the URLs that fix them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This assembly deliberately references <b>nothing</b> — not UniTask, not
    /// <c>com.uni-tx.core</c>'s runtime assembly. Unity's Package Manager cannot resolve git
    /// dependencies declared inside a package, so the realistic failure mode is a consumer
    /// installing one package by git URL and getting a wall of <c>CS0246</c>. If this tool
    /// referenced any of those assemblies it would fail to compile alongside them and could
    /// never report anything.
    /// </para>
    /// <para>
    /// Everything is therefore discovered by probing loaded assemblies by name.
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    internal static class UniTxDependencyDoctor
    {
        private const string SessionKey = "UniTx.DependencyDoctor.Reported";
        private const string Version = "1.1.0";
        private const string RepoUrl = "https://github.com/uni-tx/kit.git";
        private const string UniTaskAssembly = "UniTask";

        private const string UniTaskUrl =
            "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11";

        /// <summary>
        /// Package id (suffix) to the UniTx assemblies it needs.
        /// </summary>
        private static readonly Dictionary<string, string[]> Requirements = new()
        {
            ["ioc"] = Array.Empty<string>(),
            ["core"] = new[] { "com.uni-tx.ioc" },
            ["events"] = new[] { "com.uni-tx.core" },
            ["resources"] = new[] { "com.uni-tx.core" },
            ["pooling"] = new[] { "com.uni-tx.core", "com.uni-tx.ioc" },
            ["audio"] = new[] { "com.uni-tx.core", "com.uni-tx.ioc", "com.uni-tx.pooling" },
            ["content"] = new[] { "com.uni-tx.core", "com.uni-tx.resources" },
            ["serialization"] = new[] { "com.uni-tx.core", "com.uni-tx.ioc" },
            ["widgets"] = new[] { "com.uni-tx.core", "com.uni-tx.ioc", "com.uni-tx.resources" },
            ["entity"] = new[]
            {
                "com.uni-tx.core", "com.uni-tx.ioc", "com.uni-tx.content", "com.uni-tx.serialization",
            },
            ["sprite-loader"] = new[] { "com.uni-tx.core", "com.uni-tx.resources" },
            ["localization"] = new[] { "com.uni-tx.core" },
            ["tweening"] = Array.Empty<string>(),
            ["analytics"] = new[] { "com.uni-tx.core", "com.uni-tx.ioc" },
            ["ads"] = new[] { "com.uni-tx.core", "com.uni-tx.ioc" },
            ["bootstrap"] = new[]
            {
                "com.uni-tx.core", "com.uni-tx.ioc", "com.uni-tx.events", "com.uni-tx.resources",
                "com.uni-tx.pooling", "com.uni-tx.audio", "com.uni-tx.content",
                "com.uni-tx.serialization", "com.uni-tx.widgets", "com.uni-tx.entity",
            },
        };

        /// <summary>
        /// Packages that do not need UniTask.
        /// </summary>
        private static readonly HashSet<string> NoUniTask = new() { "ioc" };

        static UniTxDependencyDoctor()
        {
            // Once per editor session, not once per domain reload — otherwise every script
            // change reprints the same wall of text.
            if (SessionState.GetBool(SessionKey, false)) return;

            SessionState.SetBool(SessionKey, true);

            // Deferred: during a static constructor the assembly list can still be settling,
            // which would produce false "missing" reports on a cold open.
            EditorApplication.delayCall += () => Report(logWhenHealthy: false);
        }

        /// <summary>
        /// Checks the installed UniTx packages and logs anything missing.
        /// </summary>
        [MenuItem("UniTx/Check Dependencies")]
        private static void CheckFromMenu() => Report(logWhenHealthy: true);

        private static void Report(bool logWhenHealthy)
        {
            var loaded = LoadedAssemblyNames();
            var installed = Requirements.Keys
                .Where(id => loaded.Contains($"com.uni-tx.{id}"))
                .ToArray();

            if (installed.Length == 0) return;

            var hasUniTask = loaded.Contains(UniTaskAssembly);
            var problems = new List<string>();

            foreach (var id in installed)
            {
                var missing = Requirements[id].Where(a => !loaded.Contains(a)).ToArray();
                var needsUniTask = !NoUniTask.Contains(id) && !hasUniTask;

                if (missing.Length == 0 && !needsUniTask) continue;

                var builder = new StringBuilder();
                builder.Append($"  com.uni-tx.{id} is missing: ");
                builder.Append(string.Join(", ", missing.Concat(needsUniTask ? new[] { "UniTask" } : Array.Empty<string>())));
                problems.Add(builder.ToString());
            }

            if (problems.Count == 0)
            {
                if (logWhenHealthy)
                {
                    Debug.Log($"[UniTx] All {installed.Length} installed package(s) have their dependencies.");
                }

                return;
            }

            var message = new StringBuilder();
            message.AppendLine("[UniTx] Missing package dependencies.");
            message.AppendLine();
            message.AppendLine("Unity's Package Manager cannot resolve git dependencies declared inside a");
            message.AppendLine("package, so UniTx siblings are not installed automatically. Add the missing");
            message.AppendLine("entries to Packages/manifest.json:");
            message.AppendLine();

            foreach (var problem in problems)
            {
                message.AppendLine(problem);
            }

            message.AppendLine();
            message.AppendLine("\"dependencies\": {");

            if (!hasUniTask) message.AppendLine($"  \"com.cysharp.unitask\": \"{UniTaskUrl}\",");

            foreach (var assembly in RequiredButMissing(installed, loaded))
            {
                var suffix = assembly.Replace("com.uni-tx.", string.Empty);
                message.AppendLine(
                    $"  \"{assembly}\": \"{RepoUrl}?path=/{assembly}#{suffix}@{Version}\",");
            }

            message.AppendLine("  ...your existing entries");
            message.AppendLine("}");

            // A warning, not an error: the compile errors themselves are already errors, and
            // this is the explanation for them rather than another failure.
            Debug.LogWarning(message.ToString());
        }

        private static IEnumerable<string> RequiredButMissing(
            IEnumerable<string> installed, ICollection<string> loaded)
            => installed
                .SelectMany(id => Requirements[id])
                .Where(a => !loaded.Contains(a))
                .Distinct()
                .OrderBy(a => a, StringComparer.Ordinal);

        private static HashSet<string> LoadedAssemblyNames()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    names.Add(assembly.GetName().Name);
                }
                catch (Exception)
                {
                    // A dynamic or unloadable assembly cannot report a name; it is never one
                    // of ours, so skipping it is correct.
                }
            }

            return names;
        }
    }
}
