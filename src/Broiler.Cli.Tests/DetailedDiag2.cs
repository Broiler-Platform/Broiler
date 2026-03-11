using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;
using Broiler.Cli;

namespace Broiler.Cli.Tests
{
    public class DetailedDiag2
    {
        private readonly ITestOutputHelper _output;
        public DetailedDiag2(ITestOutputHelper output) { _output = output; }

        [Fact]
        public void Acid3_Error_Log()
        {
            var acid3Path = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "acid", "acid3", "acid3.html"));
            Assert.True(File.Exists(acid3Path));
            // Inject log capture into the acid3 HTML
            var html = File.ReadAllText(acid3Path);
            // Insert a log capture before the closing body tag
            html = html.Replace("</body>", @"<div id=""errorlog""></div>
<script>
var logEl = document.getElementById('errorlog');
if (logEl && typeof log !== 'undefined') {
    logEl.textContent = log;
}
</script>
</body>");
            var url = new Uri(acid3Path).AbsoluteUri;
            var result = CaptureService.ExecuteScriptsWithDom(html, url);

            // Extract error log
            var logMatch = Regex.Match(result, @"id=""errorlog""[^>]*>([^<]*)<");
            if (logMatch.Success && logMatch.Groups[1].Value.Length > 0)
            {
                foreach (var line in logMatch.Groups[1].Value.Split('\n'))
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        _output.WriteLine(line);
                }
            }
            else
            {
                _output.WriteLine("No error log found");
            }
        }
    }
}
