using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using LogGrokCore.Data;
using LogGrokCore.Data.Index;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LogGrokCore.Data.Tests
{
    // End-to-end coverage of how the load pipeline treats lines that do not match the format regex.
    // The contract (locked in here so a refactor of LineProcessor cannot silently break it):
    //  * a parseable line starts a new record (gets a LineIndex entry);
    //  * an unparseable line (e.g. a stack-trace continuation) does NOT start a record and is shown
    //    as part of the preceding record, because LineProvider renders each record by its file
    //    offset range up to the next record.
    [TestClass]
    public class LineProcessorTests
    {
        private const string Format = @"^(?<Time>\d{2}:\d{2}:\d{2})\s(?<Level>[A-Z]+)\s(?<Message>.*)";

        private static void WithLoadedFile(string content, Action<LineIndex, LogFile> assert)
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, content, new UTF8Encoding(false));

                var logFile = new LogFile(path, 0);
                var meta = new LogMetaInformation(new LogFormat { Regex = Format, IndexedFields = new[] { "Level" } });
                var stringPool = new StringPool();
                var lineIndex = new LineIndex();
                var indexer = new Indexer();
                var consumer = new ParsedBufferConsumer(lineIndex, indexer, meta, stringPool);
                var parser = new RegexBasedLineParser(meta, onlyIndexed: true);
                var processor = new LineProcessor(logFile, meta, parser, consumer, stringPool);

                var encoding = logFile.Encoding;
                using (var stream = logFile.OpenForSequentialRead())
                {
                    new LoaderImpl(64 * 1024, processor)
                        .Load(stream, encoding.GetBytes("\r"), encoding.GetBytes("\n"), CancellationToken.None);
                }

                var sw = Stopwatch.StartNew();
                while (!lineIndex.IsFinished && sw.Elapsed < TimeSpan.FromSeconds(5))
                    Thread.Sleep(10);
                Assert.IsTrue(lineIndex.IsFinished, "the load pipeline did not finish in time");

                assert(lineIndex, logFile);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static string[] FetchTexts(LineIndex lineIndex, LogFile logFile, int count)
        {
            var provider = new LineProvider(lineIndex, logFile);
            var values = new (int, string)[count];
            provider.Fetch(0, values);
            var texts = new string[count];
            for (var i = 0; i < count; i++)
                texts[i] = values[i].Item2;
            return texts;
        }

        [TestMethod]
        public void AllParseableLines_EachIsItsOwnRecord()
        {
            WithLoadedFile(
                "10:00:00 INF a\r\n10:00:01 WRN b\r\n10:00:02 ERR c\r\n",
                (lineIndex, _) => Assert.AreEqual(3, lineIndex.Count));
        }

        [TestMethod]
        public void UnparseableLine_DoesNotStartARecord_AndMergesIntoPrevious()
        {
            WithLoadedFile(
                "10:00:00 INF First line\r\n   continuation without timestamp\r\n10:00:01 WRN Second line\r\n",
                (lineIndex, logFile) =>
                {
                    Assert.AreEqual(2, lineIndex.Count); // the continuation is not a separate record

                    var texts = FetchTexts(lineIndex, logFile, 2);
                    StringAssert.Contains(texts[0], "First line");
                    StringAssert.Contains(texts[0], "continuation without timestamp"); // shown with its record
                    StringAssert.Contains(texts[1], "Second line");
                    Assert.IsFalse(texts[1].Contains("continuation"));
                });
        }

        [TestMethod]
        public void MultipleContinuations_AllMergeIntoOneRecord()
        {
            WithLoadedFile(
                "10:00:00 INF head\r\n cont1\r\n cont2\r\n10:00:01 WRN next\r\n",
                (lineIndex, logFile) =>
                {
                    Assert.AreEqual(2, lineIndex.Count);

                    var texts = FetchTexts(lineIndex, logFile, 2);
                    StringAssert.Contains(texts[0], "head");
                    StringAssert.Contains(texts[0], "cont1");
                    StringAssert.Contains(texts[0], "cont2");
                    StringAssert.Contains(texts[1], "next");
                });
        }
    }
}
