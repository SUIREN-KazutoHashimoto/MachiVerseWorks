from pathlib import Path

path = Path('tests/MachiVerseWorks.Server.Tests/WebSocketRateLimitPolicyTests.cs')
text = path.read_text(encoding='utf-8')
text = text.replace('StringAssert.Contains(rateBlock, "detailCode, \\\"rateLimited\\\"");', 'StringAssert.Contains(rateBlock, "ProtocolErrorParameterKeys.DetailCode, \\\"rateLimited\\\"");')
path.write_text(text, encoding='utf-8')
