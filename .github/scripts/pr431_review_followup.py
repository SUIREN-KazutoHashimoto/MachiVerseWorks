from pathlib import Path


def replace_once(path: str, old: str, new: str) -> None:
    file = Path(path)
    text = file.read_text(encoding="utf-8")
    if text.count(old) != 1:
        raise SystemExit(f"expected one match in {path}, found {text.count(old)}")
    file.write_text(text.replace(old, new, 1), encoding="utf-8")


csharp_sim_old = '''    private static void ValidateAcyclicParentGraph<T>(IEnumerable<(T Id, T? ParentId)> nodes, string entityName, string parameterName)
        where T : struct, IEquatable<T>
    {
        var parents = nodes.ToDictionary(static item => item.Id, static item => item.ParentId);
        foreach (var start in parents.Keys)
        {
            var seen = new HashSet<T>();
            var current = start;
            while (parents.TryGetValue(current, out var parent) && parent is { } parentId)
            {
                if (!seen.Add(current)) throw new ArgumentException($"{entityName} parent graph contains a cycle.", parameterName);
                current = parentId;
            }
        }
    }
'''

csharp_sim_new = '''    private static void ValidateAcyclicParentGraph<T>(IEnumerable<(T Id, T? ParentId)> nodes, string entityName, string parameterName)
        where T : struct, IEquatable<T>
    {
        var parents = nodes.ToDictionary(static item => item.Id, static item => item.ParentId);
        var states = new Dictionary<T, byte>(parents.Count);
        var path = new List<T>();
        foreach (var start in parents.Keys)
        {
            if (states.TryGetValue(start, out var startState) && startState == 2) continue;

            path.Clear();
            var current = start;
            while (true)
            {
                if (states.TryGetValue(current, out var state))
                {
                    if (state == 1)
                        throw new ArgumentException($"{entityName} parent graph contains a cycle.", parameterName);
                    break;
                }

                states[current] = 1;
                path.Add(current);
                if (!parents.TryGetValue(current, out var parent) || parent is not { } parentId) break;
                current = parentId;
            }

            foreach (var id in path) states[id] = 2;
        }
    }
'''
replace_once("src/MachiVerseWorks.Simulation/SimulationWorld.Environment.cs", csharp_sim_old, csharp_sim_new)

csharp_protocol_old = '''    private static bool AcyclicParents(IEnumerable<(ulong Id, ulong ParentId)> nodes)
    {
        var parents = nodes.ToDictionary(static item => item.Id, static item => item.ParentId);
        foreach (var start in parents.Keys)
        {
            var seen = new HashSet<ulong>();
            var current = start;
            while (parents.TryGetValue(current, out var parent) && parent != 0UL)
            {
                if (!seen.Add(current)) return false;
                current = parent;
            }
        }
        return true;
    }
'''

csharp_protocol_new = '''    private static bool AcyclicParents(IEnumerable<(ulong Id, ulong ParentId)> nodes)
    {
        var parents = nodes.ToDictionary(static item => item.Id, static item => item.ParentId);
        var states = new Dictionary<ulong, byte>(parents.Count);
        var path = new List<ulong>();
        foreach (var start in parents.Keys)
        {
            if (states.TryGetValue(start, out var startState) && startState == 2) continue;

            path.Clear();
            var current = start;
            while (true)
            {
                if (states.TryGetValue(current, out var state))
                {
                    if (state == 1) return false;
                    break;
                }

                states[current] = 1;
                path.Add(current);
                if (!parents.TryGetValue(current, out var parent) || parent == 0UL) break;
                current = parent;
            }

            foreach (var id in path) states[id] = 2;
        }
        return true;
    }
'''
replace_once("src/MachiVerseWorks.Protocol/WorldEnvironmentProtocolCodec.cs", csharp_protocol_old, csharp_protocol_new)
replace_once("src/MachiVerseWorks.Protocol/RegionalGenerationProtocolCodec.cs", csharp_protocol_old, csharp_protocol_new)

web_old = '''function assertAcyclicParents(nodes: readonly (readonly [bigint, bigint])[], name: string): void {
  const parents = new Map(nodes);
  for (const start of parents.keys()) {
    const seen = new Set<bigint>(); let current = start;
    while (true) {
      const parent = parents.get(current); if (parent === undefined || parent === 0n) break;
      if (seen.has(current)) throw new ProtocolDecodeFailure(`${name} parent graph contains a cycle.`);
      seen.add(current); current = parent;
    }
  }
}
'''

web_new = '''function assertAcyclicParents(nodes: readonly (readonly [bigint, bigint])[], name: string): void {
  const parents = new Map(nodes);
  const states = new Map<bigint, 1 | 2>();
  const path: bigint[] = [];
  for (const start of parents.keys()) {
    if (states.get(start) === 2) continue;

    path.length = 0;
    let current = start;
    while (true) {
      const state = states.get(current);
      if (state === 1) throw new ProtocolDecodeFailure(`${name} parent graph contains a cycle.`);
      if (state === 2) break;

      states.set(current, 1);
      path.push(current);
      const parent = parents.get(current);
      if (parent === undefined || parent === 0n) break;
      current = parent;
    }

    for (const id of path) states.set(id, 2);
  }
}
'''
replace_once("src/web/src/world-environment-protocol.ts", web_old, web_new)
replace_once("src/web/src/regional-generation-protocol.ts", web_old, web_new)

admin_old = '''            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, path, overwrite: true);
'''
admin_new = '''            if (!OperatingSystem.IsWindows() && File.Exists(path))
            {
                File.SetUnixFileMode(tempPath, File.GetUnixFileMode(path));
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, path, overwrite: true);
'''
replace_once("src/MachiVerseWorks.Server/AdminCommandExecutorV2.cs", admin_old, admin_new)

readme_old = '''Save format versioning: adding or changing authoritative persisted schema requires a new `SaveFormatVersion`; format 11 is retained as a legacy Economy-family input, while current saves are written with the expanded-schema version.'''
readme_new = '''Save format versionの運用では、authoritativeな永続化schemaを追加・変更した場合は新しい`SaveFormatVersion`を割り当てます。format 11は旧Economy系Saveの入力互換用として維持し、現在のSaveは拡張schema用のversionで書き出します。'''
replace_once("src/MachiVerseWorks.Persistence/README.md", readme_old, readme_new)

admin_tests_path = Path("tests/MachiVerseWorks.Server.Tests/AdminCommandTests.cs")
admin_tests = admin_tests_path.read_text(encoding="utf-8")
if "using System.Reflection;" not in admin_tests:
    admin_tests = admin_tests.replace("using MachiVerseWorks.Simulation;\n", "using System.Reflection;\nusing MachiVerseWorks.Simulation;\n", 1)

marker = '''    private static AdminCommandRequest Request(AdminCommand command) => new(command, new TaskCompletionSource<AdminCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously));
'''
test = '''    [TestMethod]
    public async Task AtomicWorldSavePreservesExistingUnixMode()
    {
        if (OperatingSystem.IsWindows()) return;

        var directory = Path.Combine(Path.GetTempPath(), $"machiverse-admin-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "world.json");
        try
        {
            await File.WriteAllBytesAsync(path, new byte[] { 1, 2, 3 });
            var expectedMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            File.SetUnixFileMode(path, expectedMode);

            var method = typeof(AdminCommandExecutorV2).GetMethod(
                "WriteWorldSaveAtomicallyAsync",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            var replacement = new byte[] { 4, 5, 6, 7 };
            var task = (Task?)method!.Invoke(
                null,
                new object?[] { path, new ReadOnlyMemory<byte>(replacement), CancellationToken.None });
            Assert.IsNotNull(task);
            await task!;

            Assert.AreEqual(expectedMode, File.GetUnixFileMode(path));
            CollectionAssert.AreEqual(replacement, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

'''
if test not in admin_tests:
    if admin_tests.count(marker) != 1:
        raise SystemExit("AdminCommandTests insertion marker mismatch")
    admin_tests = admin_tests.replace(marker, test + marker, 1)
admin_tests_path.write_text(admin_tests, encoding="utf-8")

print("PR #431 review follow-up patch applied")
