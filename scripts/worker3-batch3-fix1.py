from pathlib import Path

path = Path('tests/MachiVerseWorks.Server.Tests/RemoteMcpRequestGateTests.cs')
text = path.read_text(encoding='utf-8')
text = text.replace('maxConcurrent.ToString()', 'maxConcurrent.ToString(System.Globalization.CultureInfo.InvariantCulture)')
text = text.replace('requestsPerMinute.ToString()', 'requestsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture)')
path.write_text(text, encoding='utf-8')
