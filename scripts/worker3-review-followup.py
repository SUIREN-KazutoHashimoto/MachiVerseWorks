from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def replace_once(path: str, old: str, new: str) -> None:
    file = ROOT / path
    text = file.read_text(encoding="utf-8")
    if old not in text:
        raise RuntimeError(f"expected text not found in {path}")
    file.write_text(text.replace(old, new, 1), encoding="utf-8")


admin_path = "src/MachiVerseWorks.Server/AdminCommandExecutorV2.cs"
replace_once(
    admin_path,
    '''            try
            {
                await WriteWorldSaveAtomicallyAsync(path, data, overwrite: Eq(action, "save"), cancellationToken);
            }
            catch (IOException) when (Eq(action, "save-new") && File.Exists(path))
            {
                return new AdminCommandResult(AdminCommandResultCode.Conflict, $"World save '{path}' already exists.");
            }
''',
    '''            if (Eq(action, "save"))
            {
                await WriteWorldSaveAtomicallyAsync(path, data, cancellationToken);
            }
            else
            {
                try
                {
                    await WriteWorldSaveAtomicallyNewAsync(path, data, cancellationToken);
                }
                catch (IOException) when (File.Exists(path))
                {
                    return new AdminCommandResult(AdminCommandResultCode.Conflict, $"World save '{path}' already exists.");
                }
            }
''',
)

file = ROOT / admin_path
text = file.read_text(encoding="utf-8")
start = text.index("    private static async Task WriteWorldSaveAtomicallyAsync(")
end = text.index("    private AdminCommandResult RailwayMutate", start)
helpers = '''    private static Task WriteWorldSaveAtomicallyAsync(
        string path,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken) =>
        WriteWorldSaveAtomicallyCoreAsync(path, data, overwrite: true, cancellationToken);

    private static Task WriteWorldSaveAtomicallyNewAsync(
        string path,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken) =>
        WriteWorldSaveAtomicallyCoreAsync(path, data, overwrite: false, cancellationToken);

    private static async Task WriteWorldSaveAtomicallyCoreAsync(
        string path,
        ReadOnlyMemory<byte> data,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new IOException("Save path does not have a parent directory.");
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await stream.WriteAsync(data, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            if (overwrite && !OperatingSystem.IsWindows() && File.Exists(path))
            {
                File.SetUnixFileMode(tempPath, File.GetUnixFileMode(path));
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, path, overwrite);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch (IOException)
            {
                // Cleanup must not hide the original save result. A later save uses a unique temp name.
            }
            catch (UnauthorizedAccessException)
            {
                // Cleanup must not hide the original save result.
            }
        }
    }

'''
file.write_text(text[:start] + helpers + text[end:], encoding="utf-8")

water_test = "tests/MachiVerseWorks.Server.Tests/WaterSewerMessageMapperTests.cs"
replace_once(
    water_test,
    '''        var sewerNodes = Enumerable.Range(0, 512)
            .Select(index => world.CreateSewerNode(new WorldPoint(index, 0, -2), SewerNodeKind.Service))
            .ToArray();
        for (var index = 0; index < 512; index++)
            world.CreateWaterSewerServicePoint(waterNodes[index], sewerNodes[511 - index], 1d);
''',
    '''        var sewerNodes = Enumerable.Range(0, 512)
            .Select(index => world.CreateSewerNode(new WorldPoint(index, 0, -2), SewerNodeKind.Service))
            .ToArray();
        var buildings = Enumerable.Range(0, 512)
            .Select(index => world.CreateBuilding(new WorldVolume(index * 2, 10, 0, index * 2 + 1, 11, 1), BuildingKind.Residential))
            .ToArray();
        for (var index = 0; index < 512; index++)
            world.CreateWaterSewerServicePoint(waterNodes[index], sewerNodes[511 - index], 1d, buildingId: buildings[index]);
''',
)

print("worker-3 review followup applied")
