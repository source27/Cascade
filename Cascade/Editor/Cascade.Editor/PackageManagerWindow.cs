using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Cascade.Editor
{
    /// <summary>
    /// Lists Cascade's optional modules and integrations, shows what the project already has, and
    /// installs/removes them through the official UPM client (<see cref="Client.Add"/> /
    /// <see cref="Client.Remove"/>) — no hand-editing of <c>Packages/manifest.json</c>.
    /// <para>
    /// Install reference depends on how this package was obtained: a monorepo checkout installs the
    /// sibling folders with a relative <c>file:</c> path; a git-installed package installs
    /// <c>&lt;repo&gt;?path=…</c> pinned to the same revision as the main package.
    /// </para>
    /// </summary>
    public sealed class PackageManagerWindow : EditorWindow
    {
        private CascadePackageCatalogueFile _catalogue;
        private readonly Dictionary<string, PackageInfo> _installed = new Dictionary<string, PackageInfo>(StringComparer.Ordinal);
        private ListRequest _listRequest;
        private Request _mutationRequest;
        private string _busy;
        private string _lastMessage;
        private Vector2 _scroll;
        private bool _showPeers = true;

        [MenuItem("Cascade/集成与模块", priority = 130)]
        private static void Open()
        {
            var window = GetWindow<PackageManagerWindow>();
            window.titleContent = new GUIContent("Cascade 包");
            window.minSize = new Vector2(520f, 320f);
            window.Show();
        }

        private void OnEnable()
        {
            _catalogue = CascadePackageCatalogue.Load(forceReload: true);
            StartList();
        }

        private void OnDisable()
        {
            EditorApplication.update -= Poll;
        }

        private void StartList()
        {
            if (_listRequest != null)
                return;

            _listRequest = Client.List(offlineMode: true, includeIndirectDependencies: true);
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        private void Poll()
        {
            if (_listRequest != null && _listRequest.IsCompleted)
            {
                var request = _listRequest;
                _listRequest = null;
                if (request.Status == StatusCode.Success)
                {
                    _installed.Clear();
                    foreach (var package in request.Result)
                        _installed[package.name] = package;
                }
                else
                {
                    _lastMessage = $"列包失败：{request.Error?.message}";
                }

                Repaint();
            }

            if (_mutationRequest != null && _mutationRequest.IsCompleted)
            {
                var request = _mutationRequest;
                _mutationRequest = null;
                _busy = null;
                _lastMessage = request.Status == StatusCode.Success
                    ? request is AddRequest add && add.Result != null
                        ? $"已安装 {add.Result.name} {add.Result.version}"
                        : "已移除（等待解析）"
                    : $"失败：{request.Error?.message}";
                AssetDatabase.Refresh();
                StartList();
                Repaint();
            }

            if (_listRequest == null && _mutationRequest == null)
                EditorApplication.update -= Poll;
        }

        private void OnGUI()
        {
            if (_catalogue?.packages == null)
            {
                EditorGUILayout.HelpBox(
                    $"读不到 {CascadePackageCatalogue.FileName}（{CascadePackageCatalogue.CatalogueAssetPath}）。",
                    MessageType.Error);
                return;
            }

            DrawHeader();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var group in _catalogue.packages.GroupBy(entry => string.IsNullOrEmpty(entry.kind) ? "其它" : entry.kind))
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(group.Key, EditorStyles.boldLabel);
                foreach (var entry in group)
                    DrawEntry(entry);
            }

            EditorGUILayout.EndScrollView();
            DrawFooter();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(4f);
            var source = CascadePackageCatalogue.IsLocalMonorepo
                ? $"本地 monorepo：{CascadePackageCatalogue.RepositoryRoot}"
                : "git 安装（或未检出 Modules/Integrations 的拷贝）";
            EditorGUILayout.LabelField($"主包：{CascadePackageCatalogue.PackageRoot}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"来源：{source}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("前置 peer：com.cysharp.unitask 必须已在工程 manifest 里（主包编译期依赖）。", EditorStyles.miniLabel);
            _showPeers = EditorGUILayout.ToggleLeft("显示 peer 说明", _showPeers);
            EditorGUILayout.Space(2f);
        }

        private void DrawEntry(CascadePackageEntry entry)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var url = CascadePackageCatalogue.ResolveInstallUrl(entry, out var mode);
                _installed.TryGetValue(entry.name, out var installed);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{entry.displayName}  ({entry.name})", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(
                        installed != null ? $"已装 {installed.version}" : "未装",
                        EditorStyles.miniBoldLabel,
                        GUILayout.Width(120f));
                }

                EditorGUILayout.LabelField(entry.when ?? string.Empty, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(mode, EditorStyles.miniLabel);
                EditorGUILayout.SelectableLabel(url, EditorStyles.miniLabel, GUILayout.Height(16f));

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_busy != null))
                    {
                        if (GUILayout.Button(installed != null ? "重装/更新" : "安装", GUILayout.Width(90f)))
                            Add(entry, url);

                        if (installed != null && GUILayout.Button("移除", GUILayout.Width(60f)))
                            Remove(entry.name);
                    }

                    if (GUILayout.Button("复制 manifest 片段", GUILayout.Width(140f)))
                    {
                        EditorGUIUtility.systemCopyBuffer =
                            CascadePackageCatalogue.BuildManifestSnippet(entry, url);
                        _lastMessage = $"已复制 {entry.name} 的 manifest 片段。";
                    }
                }

                if (_showPeers && entry.peers != null && entry.peers.Length > 0)
                {
                    foreach (var peer in entry.peers)
                    {
                        EditorGUILayout.LabelField(
                            string.IsNullOrWhiteSpace(peer.url)
                                ? $"↳ peer {peer.name}：{peer.note}"
                                : $"↳ peer {peer.name}：{peer.note}\n   {peer.url}",
                            EditorStyles.wordWrappedMiniLabel);
                    }
                }
            }
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("刷新", GUILayout.Width(70f)))
                {
                    _catalogue = CascadePackageCatalogue.Load(forceReload: true);
                    StartList();
                }

                if (_busy != null)
                    EditorGUILayout.LabelField(_busy, EditorStyles.miniLabel);
                else if (!string.IsNullOrEmpty(_lastMessage))
                    EditorGUILayout.LabelField(_lastMessage, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void Add(CascadePackageEntry entry, string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            // Client.Add takes an absolute file: path for local packages (the manifest snippet keeps the
            // relative form Unity writes itself).
            var identifier = CascadePackageCatalogue.IsLocalMonorepo
                ? "file:" + Path.GetFullPath(Path.Combine(CascadePackageCatalogue.RepositoryRoot, entry.path)).Replace('\\', '/')
                : url;

            _busy = $"安装 {entry.name} …";
            _lastMessage = null;
            _mutationRequest = Client.Add(identifier);
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        private void Remove(string packageName)
        {
            _busy = $"移除 {packageName} …";
            _lastMessage = null;
            _mutationRequest = Client.Remove(packageName);
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

    }
}
