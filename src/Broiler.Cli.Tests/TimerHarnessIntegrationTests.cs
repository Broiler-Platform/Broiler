namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 3 integration tests: Timer Pump &amp; Test Harness Integration.
/// Validates that the Acid3-style test harness pattern — chaining tests
/// via setTimeout, body onload bootstrap, error isolation — works end-to-end.
/// </summary>
public class TimerHarnessIntegrationTests
{
    // ---------------------------------------------------------------
    //  3.1 — Timer pump: sequential test chaining via setTimeout
    // ---------------------------------------------------------------

    [Fact]
    public void Timer_Chained_Tests_All_Execute_Sequentially()
    {
        // Simulates the Acid3 pattern: an array of tests executed
        // one-by-one via setTimeout chaining.
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var tests = [
    function() { return 'test0'; },
    function() { return 'test1'; },
    function() { return 'test2'; },
    function() { return 'test3'; },
    function() { return 'test4'; }
];
var results = [];
var index = 0;
function runNext() {
    if (index < tests.length) {
        results.push(tests[index]());
        index++;
        setTimeout(runNext, 10);
    } else {
        var p = document.createElement('p');
        p.textContent = 'DONE:' + results.join(',');
        document.getElementById('out').appendChild(p);
    }
}
setTimeout(runNext, 10);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("DONE:test0,test1,test2,test3,test4", result);
    }

    [Fact]
    public void Timer_Large_Chain_Exceeds_Old_100_Limit()
    {
        // Verifies that >100 chained setTimeout calls all execute.
        // The old limit of 100 iterations would have cut this short.
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var count = 0;
var target = 150;
function tick() {
    count++;
    if (count < target) {
        setTimeout(tick, 0);
    } else {
        var p = document.createElement('p');
        p.textContent = 'count=' + count;
        document.getElementById('out').appendChild(p);
    }
}
setTimeout(tick, 0);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("count=150", result);
    }

    [Fact]
    public void Timer_Callbacks_Fire_In_Registration_Order()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var order = [];
setTimeout(function() { order.push('A'); }, 0);
setTimeout(function() { order.push('B'); }, 0);
setTimeout(function() { order.push('C'); }, 0);
setTimeout(function() {
    var p = document.createElement('p');
    p.textContent = order.join(',');
    document.getElementById('out').appendChild(p);
}, 0);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("A,B,C", result);
    }

    [Fact]
    public void Timer_IDs_Are_Unique_And_Clearable()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var ids = [];
for (var i = 0; i < 10; i++) {
    ids.push(setTimeout(function(){}, 0));
}
// Clear all
for (var i = 0; i < ids.length; i++) {
    clearTimeout(ids[i]);
}
// Verify uniqueness
var unique = ids.filter(function(v, i, a) { return a.indexOf(v) === i; });
var p = document.createElement('p');
p.textContent = 'unique=' + (unique.length === ids.length ? 'yes' : 'no') + ',count=' + ids.length;
document.getElementById('out').appendChild(p);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("unique=yes,count=10", result);
    }

    // ---------------------------------------------------------------
    //  3.2 — Error isolation: JS errors must not halt subsequent tests
    // ---------------------------------------------------------------

    [Fact]
    public void Error_In_Chained_Test_Does_Not_Halt_Subsequent_Tests()
    {
        // Simulates Acid3 pattern: try/catch around each test, continue on error
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var tests = [
    function() { return 'pass'; },
    function() { throw new Error('boom'); },
    function() { return 'pass'; },
    function() { undefined.property; },
    function() { return 'pass'; }
];
var score = 0;
var errors = 0;
var index = 0;
function runNext() {
    if (index < tests.length) {
        try {
            tests[index]();
            score++;
        } catch(e) {
            errors++;
        }
        index++;
        setTimeout(runNext, 0);
    } else {
        var p = document.createElement('p');
        p.textContent = 'score=' + score + ',errors=' + errors;
        document.getElementById('out').appendChild(p);
    }
}
setTimeout(runNext, 0);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("score=3,errors=2", result);
    }

    [Fact]
    public void DomBridge_Errors_Catchable_By_Try_Catch()
    {
        // Verifies that DomBridge errors (e.g. invalid operations) are
        // catchable by JavaScript try/catch, not fatal crashes.
        var html = @"<!DOCTYPE html>
<html><body>
<div id=""out""></div>
<script>
var results = [];
try {
    document.createElement('1invalid');
    results.push('F');
} catch(e) {
    results.push('caught-create');
}
try {
    var el = document.getElementById('nonexistent');
    results.push(el === null ? 'null-ok' : 'F');
} catch(e) {
    results.push('F');
}
var p = document.createElement('p');
p.textContent = results.join(',');
document.getElementById('out').appendChild(p);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("caught-create,null-ok", result);
    }

    // ---------------------------------------------------------------
    //  3.3 — Body onload event fires and bootstraps test runner
    // ---------------------------------------------------------------

    [Fact]
    public void Body_Onload_Fires_After_Script_Execution()
    {
        var html = @"<!DOCTYPE html>
<html><body onload=""
    var p = document.createElement('p');
    p.textContent = 'onload-fired';
    document.getElementById('out').appendChild(p);
"">
<div id=""out""></div>
<script>
var p = document.createElement('p');
p.textContent = 'script-ran';
document.getElementById('out').appendChild(p);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("script-ran", result);
        Assert.Contains("onload-fired", result);
    }

    [Fact]
    public void Body_Onload_Triggers_SetTimeout_Chain()
    {
        // Simulates the Acid3 pattern: body onload starts the test runner,
        // which chains tests via setTimeout.
        var html = @"<!DOCTYPE html>
<html><body onload=""update()"">
<div id=""out""></div>
<script>
var score = 0;
var tests = [
    function() { score++; return true; },
    function() { score++; return true; },
    function() { score++; return true; }
];
var index = 0;
function update() {
    if (index < tests.length) {
        try {
            tests[index]();
        } catch(e) {}
        index++;
        setTimeout(update, 10);
    } else {
        var p = document.createElement('p');
        p.textContent = 'score=' + score;
        document.getElementById('out').appendChild(p);
    }
}
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("score=3", result);
    }

    // ---------------------------------------------------------------
    //  3.4 — End-to-end Acid3 harness simulation
    // ---------------------------------------------------------------

    [Fact]
    public void Acid3_Harness_Simulation_Score_Greater_Than_Zero()
    {
        // Simplified Acid3 harness: 10 tests, chained via setTimeout,
        // score displayed in DOM, bucket classes updated.
        var html = @"<!DOCTYPE html>
<html>
<head>
<style>
.z { visibility: hidden; }
</style>
</head>
<body onload=""update()"">
<div class=""buckets"">
  <p id=""bucket1"" class=""z"">B1</p>
</div>
<p id=""result""><span id=""score"">0</span>/<span>10</span></p>
<div id=""log""></div>
<script>
var tests = [
    function() { return 1; },
    function() { return 1; },
    function() { return 1; },
    function() { return 1; },
    function() { return 1; },
    function() { return 1; },
    function() { return 1; },
    function() { return 1; },
    function() { return 1; },
    function() { return 1; }
];
var score = 0;
var index = 0;
var delay = 10;
function update() {
    if (index < tests.length) {
        try {
            var result = tests[index]();
            if (result) {
                var bucket = document.getElementById('bucket1');
                if (bucket) bucket.className += 'P';
                score++;
            }
        } catch(e) {}
        index++;
        document.getElementById('score').textContent = '' + score;
        setTimeout(update, delay);
    }
}
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // All 10 tests should pass, score should be 10
        Assert.Contains(">10<", result);
        // Bucket should have 10 P's appended
        Assert.Contains("zPPPPPPPPPP", result);
    }

    [Fact]
    public void Acid3_Harness_Simulation_With_Failing_Tests()
    {
        // Some tests pass, some fail — score should only count passes
        var html = @"<!DOCTYPE html>
<html><body onload=""update()"">
<p id=""result""><span id=""score"">0</span>/<span>6</span></p>
<div id=""out""></div>
<script>
var tests = [
    function() { return 1; },
    function() { throw new Error('fail'); },
    function() { return 1; },
    function() { return false; },
    function() { return 1; },
    function() { return 1; }
];
var score = 0;
var errors = 0;
var index = 0;
function update() {
    if (index < tests.length) {
        try {
            var result = tests[index]();
            if (result) {
                score++;
            } else {
                errors++;
            }
        } catch(e) {
            errors++;
        }
        index++;
        document.getElementById('score').textContent = '' + score;
        setTimeout(update, 10);
    } else {
        var p = document.createElement('p');
        p.textContent = 'done:score=' + score + ',errors=' + errors;
        document.getElementById('out').appendChild(p);
    }
}
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        // 4 tests pass (indices 0, 2, 4, 5), 2 fail (indices 1, 3)
        Assert.Contains("done:score=4,errors=2", result);
    }

    [Fact]
    public void Acid3_Score_Extraction_Via_QuerySelector()
    {
        // Verifies the exact pattern used by Acid3:
        // document.querySelector('#result').textContent shows the score
        var html = @"<!DOCTYPE html>
<html><body onload=""update()"">
<p id=""result""><span id=""score"">?</span><span>/</span><span>5</span></p>
<div id=""out""></div>
<script>
var tests = [
    function() { return 1; },
    function() { return 1; },
    function() { return 1; },
    function() { return 1; },
    function() { return 1; }
];
var score = 0;
var index = 0;
function update() {
    var span = document.getElementById('score');
    if (index < tests.length) {
        try {
            var r = tests[index]();
            if (r) score++;
        } catch(e) {}
        index++;
        span.textContent = '' + score;
        setTimeout(update, 10);
    } else {
        // After all tests: extract score via querySelector
        var scoreText = document.querySelector('#score').textContent;
        var p = document.createElement('p');
        p.textContent = 'extracted=' + scoreText;
        document.getElementById('out').appendChild(p);
    }
}
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("extracted=5", result);
    }
}
